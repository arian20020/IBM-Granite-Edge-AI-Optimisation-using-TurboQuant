import base64
import codecs
import copy
import ctypes
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "workflows"
    / "hardware-inspection-intel-runner-stage-a.yml"
)
VALIDATOR_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Validate-HardwareInspectionIntelRunnerStageA.ps1"
)
RUNNER_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Invoke-HardwareInspectionIntelRunnerStageA.ps1"
)
RUNBOOK_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "testing"
    / "runbooks"
    / "Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md"
)
MANIFEST_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "hardware-inspection"
    / "llmfit-gate1-approved-source.json"
)

INVALID_VALIDATOR_STDERR = (
    "HI-RUNNER-STAGEA-INVALID: authorised deterministic validation failed.\n"
)
INVALID_RUNNER_STDERR = (
    "HI-RUNNER-STAGEA-TESTS-FAILED: deterministic validation failed.\n"
)
TASK8_IDENTITIES = (
    "ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics",
    "CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary",
    "StableFileIdentityAndProcessTreeCleanup_AreFailClosed",
)
FULL_ACTION_PINS = {
    "actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1",
    "actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065",
    "actions/setup-dotnet@d4c94342e560b34958eacfc5d055d21461ed1c5d",
    "actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f",
}
RUNNER_LABEL = re.compile(r"\Ahardware-gate1-[0-9a-f]{16}\Z")


def _required_file(test_case, path):
    test_case.assertTrue(path.is_file(), f"required Stage A file is missing: {path}")
    return path


def _strict_utf8(path):
    raw = path.read_bytes()
    if raw.startswith(codecs.BOM_UTF8):
        raise AssertionError(f"{path.name} must not have a UTF-8 BOM")
    if b"\r" in raw:
        raise AssertionError(f"{path.name} must use LF line endings")
    return raw.decode("utf-8", "strict")


def _strict_json_object(raw):
    def reject_duplicates(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate JSON key: " + key)
            result[key] = value
        return result

    return json.loads(raw.decode("utf-8", "strict"), object_pairs_hook=reject_duplicates)


def _assert_approval_manifest(test_case, raw):
    test_case.assertFalse(raw.startswith(codecs.BOM_UTF8))
    test_case.assertNotIn(b"\r", raw)
    data = _strict_json_object(raw)
    test_case.assertEqual(
        list(data), ["schemaVersion", "remoteFeatureRef", "approvedTipSha"]
    )
    test_case.assertEqual(data["schemaVersion"], "1.0")
    test_case.assertEqual(
        data["remoteFeatureRef"], "refs/heads/feature/hardware-inspection"
    )
    test_case.assertRegex(data["approvedTipSha"], r"^(?!0{40}$)[0-9a-f]{40}$")


def _assert_summary_upload(test_case, document):
    upload_steps = [
        step
        for _, step in _all_steps(document)
        if str(step.get("uses", "")).startswith("actions/upload-artifact@")
    ]
    test_case.assertEqual(len(upload_steps), 1)
    upload_with = upload_steps[0].get("with", {})
    upload_path = json.dumps(upload_with).casefold()
    test_case.assertIn("summary", upload_path)
    test_case.assertNotIn(".trx", upload_path)
    test_case.assertNotRegex(upload_path, r"(?:[a-z]:\\|\\\\|runner\.|computername|hostname)")


def _assert_no_host_or_path_leaks(test_case, text):
    test_case.assertNotRegex(text, r"(?i)\$\{\{\s*(?:runner\.|github\.workspace|github\.event\.runner)")
    test_case.assertNotRegex(text, r"(?i)\$env:(?:computername|username|userdomain|hostname)")
    test_case.assertNotRegex(text, r"(?i)(?:[a-z]:\\|\\\\[^\s\\]+\\|/home/|/Users/)")


def _strip_yaml_comment(value):
    quote = None
    escaped = False
    for index, character in enumerate(value):
        if quote:
            if escaped:
                escaped = False
            elif character == "\\" and quote == '"':
                escaped = True
            elif character == quote:
                quote = None
        elif character in "'\"":
            quote = character
        elif character == "#" and (index == 0 or value[index - 1].isspace()):
            return value[:index].rstrip()
    return value.rstrip()


def _yaml_scalar(value):
    value = _strip_yaml_comment(value.strip())
    if value in ("", "null", "~"):
        return None
    if value in ("true", "True", "TRUE"):
        return True
    if value in ("false", "False", "FALSE"):
        return False
    if value.startswith("[") and value.endswith("]"):
        return [_yaml_scalar(item) for item in value[1:-1].split(",") if item.strip()]
    if value[:1] in ("'", '"') and value[-1:] == value[:1]:
        if value[0] == "'":
            return value[1:-1].replace("''", "'")
        return json.loads(value)
    return value


def _yaml_tokens(raw):
    text = raw.decode("utf-8", "strict") if isinstance(raw, bytes) else raw
    if text.startswith("\ufeff") or "\r" in text:
        raise AssertionError("workflow encoding must be UTF-8 without BOM and LF-only")
    tokens = []
    for line_number, line in enumerate(text.splitlines(), 1):
        if "\t" in line:
            raise AssertionError(f"workflow contains a tab on line {line_number}")
        content = line.lstrip(" ")
        if not content or content.startswith("#"):
            continue
        tokens.append((len(line) - len(content), content, line_number))
    return tokens


def _yaml_key_value(content):
    quote = None
    escaped = False
    for index, character in enumerate(content):
        if quote:
            if escaped:
                escaped = False
            elif character == "\\" and quote == '"':
                escaped = True
            elif character == quote:
                quote = None
        elif character in "'\"":
            quote = character
        elif character == ":" and (index + 1 == len(content) or content[index + 1].isspace()):
            key = content[:index].strip()
            if key[:1] in ("'", '"') and key[-1:] == key[:1]:
                key = _yaml_scalar(key)
            return key, content[index + 1 :].lstrip()
    return None


def _yaml_load(raw):
    tokens = _yaml_tokens(raw)

    def parse_block(position, indent):
        if position >= len(tokens) or tokens[position][0] < indent:
            return {}, position
        is_list = tokens[position][0] == indent and tokens[position][1].startswith("- ")
        result = [] if is_list else {}
        while position < len(tokens):
            current_indent, content, line_number = tokens[position]
            if current_indent < indent:
                break
            if current_indent != indent:
                raise AssertionError(f"unexpected YAML indentation on line {line_number}")
            if is_list:
                if not content.startswith("- "):
                    break
                item = content[2:].strip()
                key_value = _yaml_key_value(item)
                position += 1
                if key_value is None:
                    result.append(_yaml_scalar(item))
                    continue
                key, value = key_value
                item_map = {}
                if value in ("|", ">-", ">"):
                    block, position = parse_scalar_block(position, indent, value)
                    item_map[key] = block
                elif value:
                    item_map[key] = _yaml_scalar(value)
                elif position < len(tokens) and tokens[position][0] > indent:
                    item_map[key], position = parse_block(position, tokens[position][0])
                else:
                    item_map[key] = None
                while position < len(tokens) and tokens[position][0] > indent:
                    child_indent, child_content, child_line = tokens[position]
                    child_pair = _yaml_key_value(child_content)
                    if child_pair is None:
                        raise AssertionError(f"invalid YAML list mapping on line {child_line}")
                    child_key, child_value = child_pair
                    position += 1
                    if child_key in item_map:
                        raise AssertionError(f"duplicate YAML key {child_key!r}")
                    if child_value in ("|", ">-", ">"):
                        item_map[child_key], position = parse_scalar_block(
                            position, child_indent, child_value
                        )
                    elif child_value:
                        item_map[child_key] = _yaml_scalar(child_value)
                    elif position < len(tokens) and tokens[position][0] > child_indent:
                        item_map[child_key], position = parse_block(
                            position, tokens[position][0]
                        )
                    else:
                        item_map[child_key] = None
                result.append(item_map)
            else:
                if content.startswith("- "):
                    break
                key_value = _yaml_key_value(content)
                if key_value is None:
                    raise AssertionError(f"invalid YAML mapping on line {line_number}")
                key, value = key_value
                if key in result:
                    raise AssertionError(f"duplicate YAML key {key!r}")
                position += 1
                if value in ("|", ">-", ">"):
                    result[key], position = parse_scalar_block(position, indent, value)
                elif value:
                    result[key] = _yaml_scalar(value)
                elif position < len(tokens) and tokens[position][0] > indent:
                    result[key], position = parse_block(position, tokens[position][0])
                else:
                    result[key] = None
        return result, position

    def parse_scalar_block(position, parent_indent, style):
        lines = []
        while position < len(tokens) and tokens[position][0] > parent_indent:
            block_indent, content, _ = tokens[position]
            lines.append(" " * max(0, block_indent - parent_indent - 2) + content)
            position += 1
        if style == "|":
            return "\n".join(lines) + "\n", position
        return " ".join(line.strip() for line in lines), position

    document, position = parse_block(0, tokens[0][0] if tokens else 0)
    if position != len(tokens):
        raise AssertionError("unparsed YAML content")
    return document


def _workflow(test_case):
    path = _required_file(test_case, WORKFLOW_PATH)
    raw = path.read_bytes()
    return raw, _yaml_load(raw)


def _steps(document, job_name):
    job = document["jobs"][job_name]
    return job.get("steps", [])


def _all_steps(document):
    for job_name, job in document.get("jobs", {}).items():
        for step in job.get("steps", []):
            yield job_name, step


def _powershell_executable():
    for name in ("powershell.exe", "powershell", "pwsh.exe", "pwsh"):
        executable = shutil.which(name)
        if executable:
            return executable
    raise unittest.SkipTest("PowerShell executable is not available")


def _powershell_ast(test_case, path):
    _required_file(test_case, path)
    powershell_path = str(path).replace("'", "''")
    command = "$path = '" + powershell_path + "'\n" + r"""
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { exit 2 }
foreach ($node in $ast.FindAll({ param($candidate) $candidate -is [System.Management.Automation.Language.CommandAst] }, $true)) {
  [Console]::WriteLine('COMMAND:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($node.Extent.Text)))
}
foreach ($node in $ast.FindAll({ param($candidate) $candidate -is [System.Management.Automation.Language.StringConstantExpressionAst] }, $true)) {
  [Console]::WriteLine('STRING:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($node.Value)))
}
"""
    result = subprocess.run(
        [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command", command],
        text=True,
        capture_output=True,
        timeout=20,
        check=False,
    )
    test_case.assertEqual(result.returncode, 0, result.stderr)
    commands = []
    strings = []
    for line in result.stdout.splitlines():
        if line.startswith("COMMAND:"):
            commands.append(base64.b64decode(line[8:]).decode("utf-8"))
        elif line.startswith("STRING:"):
            strings.append(base64.b64decode(line[7:]).decode("utf-8"))
    return commands, strings


def _powershell_ast_text(test_case, path):
    _required_file(test_case, path)
    return _strict_utf8(path), *_powershell_ast(test_case, path)


def _powershell_text_ast(test_case, text):
    with tempfile.TemporaryDirectory() as temporary_directory:
        path = Path(temporary_directory) / "mutated.ps1"
        path.write_text(text, encoding="utf-8", newline="\n")
        return _powershell_ast(test_case, path)


def _assert_workflow_shape(test_case, document):
    test_case.assertEqual(list(document), ["name", "on", "permissions", "concurrency", "jobs"])
    test_case.assertEqual(list(document["on"]), ["workflow_dispatch"])
    inputs = document["on"]["workflow_dispatch"]["inputs"]
    test_case.assertEqual(list(inputs), ["runner_label", "confirm_authorised_runner"])
    test_case.assertEqual(inputs["runner_label"]["required"], True)
    test_case.assertEqual(inputs["runner_label"]["type"], "string")
    test_case.assertEqual(inputs["confirm_authorised_runner"]["required"], True)
    test_case.assertEqual(inputs["confirm_authorised_runner"]["type"], "boolean")
    test_case.assertEqual(inputs["confirm_authorised_runner"]["default"], False)
    test_case.assertEqual(document["permissions"], {"contents": "read"})
    test_case.assertEqual(list(document["jobs"]), ["hosted-preflight", "deterministic-runner"])
    test_case.assertIn("workflow_dispatch", document["jobs"]["hosted-preflight"]["if"])
    guard = document["jobs"]["deterministic-runner"]["if"]
    for expression in (
        "github.ref == format('refs/heads/{0}', github.event.repository.default_branch)",
        "github.actor == github.repository_owner",
        "github.triggering_actor == github.repository_owner",
        "github.run_attempt == 1",
        "inputs.confirm_authorised_runner == true",
    ):
        test_case.assertIn(expression, guard)


def _assert_runner_routing(test_case, document, validator_text):
    jobs = document["jobs"]
    test_case.assertEqual(list(jobs), ["hosted-preflight", "deterministic-runner"])
    test_case.assertEqual(jobs["hosted-preflight"]["runs-on"], "windows-latest")
    preflight_outputs = jobs["hosted-preflight"].get("outputs", {})
    test_case.assertEqual(
        set(preflight_outputs), {"approved_sha", "source_ref", "runner_label", "eligible"}
    )
    test_case.assertRegex(
        str(preflight_outputs["runner_label"]),
        r"\$\{\{\s*steps\.[A-Za-z0-9_-]+\.outputs\.runner_label\s*\}\}",
    )
    hosted_run_text = "\n".join(
        str(step.get("run", "")) for step in jobs["hosted-preflight"].get("steps", [])
    )
    test_case.assertRegex(hosted_run_text, r"(?i)(?:RunnerLabel|runner_label)")
    runner = jobs["deterministic-runner"]
    test_case.assertEqual(runner["needs"], "hosted-preflight")
    test_case.assertEqual(runner["runs-on"], "${{ needs.hosted-preflight.outputs.runner_label }}")
    test_case.assertRegex(validator_text, r"hardware-gate1-\[0-9a-f\]\{16\}")
    test_case.assertNotIn("runs-on: ${{ inputs.runner_label }}", _strict_utf8(WORKFLOW_PATH))
    test_case.assertNotIn("self-hosted", _strict_utf8(WORKFLOW_PATH).lower())


def _invoke(script, arguments, environment=None):
    command = [
        _powershell_executable(),
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(script),
    ]
    for key, value in arguments.items():
        command.extend(["-" + key, str(value)])
    return subprocess.run(
        command,
        text=True,
        capture_output=True,
        timeout=30,
        check=False,
        env=environment,
    )


def _invoke_with_literal_runner_label(script, arguments, runner_label, environment=None):
    def powershell_literal(value):
        return "'" + str(value).replace("'", "''") + "'"

    command = "$runnerLabel = " + powershell_literal(runner_label) + "\n& " + powershell_literal(script)
    for key, value in arguments.items():
        if key != "RunnerLabel":
            command += " -" + key + " " + powershell_literal(value)
    command += " -RunnerLabel $runnerLabel"
    return subprocess.run(
        [
            _powershell_executable(),
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            command,
        ],
        text=True,
        capture_output=True,
        timeout=30,
        check=False,
        env=environment,
    )


def _stage_a_trx_fixture(kind):
    namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
    if kind == "Deterministic":
        names = [f"Deterministic_{index:03d}" for index in range(174)]
        assembly = "HardwareInspection.LlmFitSpike.Tests.dll"
        class_name = "HardwareInspection.LlmFitSpike.Tests.DeterministicFixture"
    else:
        names = list(TASK8_IDENTITIES)
        assembly = "HardwareInspection.LlmFitSpike.IntegrationTests.dll"
        class_name = "HardwareInspection.LlmFitSpike.IntegrationTests.LlmFitCandidateIntegrationTests"
    records = []
    for index, name in enumerate(names, 1):
        test_id = f"{index:08x}-0000-0000-0000-000000000001"
        execution_id = f"{index:08x}-0000-0000-0000-000000000002"
        records.append((name, test_id, execution_id))
    results = "".join(
        f'<UnitTestResult testName="{name}" outcome="Passed" testId="{test_id}" executionId="{execution_id}" />'
        for name, test_id, execution_id in records
    )
    definitions = "".join(
        f'<UnitTest name="{name}" storage="{assembly}" id="{test_id}"><Execution id="{execution_id}" /><TestMethod codeBase="{assembly}" className="{class_name}" name="{name}" /></UnitTest>'
        for name, test_id, execution_id in records
    )
    entries = "".join(
        f'<TestEntry testId="{test_id}" executionId="{execution_id}" />'
        for _, test_id, execution_id in records
    )
    total = len(records)
    counters = (
        f'total="{total}" executed="{total}" passed="{total}" failed="0" error="0" '
        'timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" '
        'notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" '
        'inProgress="0" pending="0"'
    )
    return (
        f'<TestRun xmlns="{namespace}"><Results>{results}</Results>'
        f'<TestDefinitions>{definitions}</TestDefinitions><TestEntries>{entries}</TestEntries>'
        f'<ResultSummary outcome="Passed"><Counters {counters} /></ResultSummary></TestRun>'
    )


def _invoke_runner_trx_fixture(path, kind):
    script_literal = str(RUNNER_PATH).replace("'", "''")
    path_literal = str(path).replace("'", "''")
    command = (
        "$ErrorActionPreference = 'Stop'\n"
        + ". '"
        + script_literal
        + "' -EvaluatedRoot 'x' -ApprovedSha ('0' * 40) -LocalWorkRoot 'x' -SummaryJsonPath 'x' -SummaryMarkdownPath 'x'\n"
        + "Read-HardwareInspectionIntelRunnerStageATrx -Path '"
        + path_literal
        + "' -Kind '"
        + kind
        + "' | Out-Null\n[Console]::Write('ok')"
    )
    return subprocess.run(
        [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command", command],
        text=True,
        capture_output=True,
        timeout=20,
        check=False,
    )


def _validator_arguments(control_root, **changes):
    arguments = {
        "Phase": "Hosted",
        "ControlRoot": control_root,
        "WorkflowRef": "refs/heads/main",
        "DefaultBranch": "main",
        "Actor": "arian20020",
        "TriggeringActor": "arian20020",
        "RepositoryOwner": "arian20020",
        "RunAttempt": "1",
        "Confirmation": "true",
        "RunnerLabel": "hardware-gate1-0123456789abcdef",
    }
    arguments.update(changes)
    return arguments


def _validator_environment():
    environment = os.environ.copy()
    for name in (
        "GRANITE_LLMFIT_CANDIDATE_ROOT",
        "GRANITE_LLMFIT_TRUSTED_OUTPUT",
        "GRANITE_LLMFIT_GATE1_OUTPUT",
        "GRANITE_LLMFIT_WINDOWS_REFERENCE",
        "GRANITE_LLMFIT_OFFLINE_OUTPUT",
        "GRANITE_LLMFIT_FAKE_TOOL_ROOT",
    ):
        environment.pop(name, None)
    return environment


def _git_output(path, *arguments):
    result = subprocess.run(
        ["git", "-C", str(path), *arguments],
        text=True,
        capture_output=True,
        timeout=20,
        check=False,
    )
    if result.returncode:
        raise AssertionError(result.stderr)
    return result.stdout.strip()


def _create_clean_checkout(root):
    root.mkdir()
    _git_output(root, "init")
    _git_output(root, "config", "user.email", "stage-a@example.invalid")
    _git_output(root, "config", "user.name", "Stage A")
    (root / "identity.txt").write_text("stage-a\n", encoding="utf-8", newline="\n")
    _git_output(root, "add", "identity.txt")
    _git_output(root, "commit", "-m", "Stage A fixture")
    return _git_output(root, "rev-parse", "HEAD")


def _write_approval_manifest(control_root, approved_sha, raw=None):
    manifest = control_root / ".github" / "hardware-inspection" / MANIFEST_PATH.name
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_bytes(
        raw
        if raw is not None
        else (
            '{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection",'
            f'"approvedTipSha":"{approved_sha}"}}'
        ).encode("utf-8")
    )


def _assert_invalid_validator_result(test_case, result):
    test_case.assertNotEqual(result.returncode, 0)
    test_case.assertEqual(result.stderr.replace("\r\n", "\n"), INVALID_VALIDATOR_STDERR)
    test_case.assertEqual(result.stdout, "")


class _ExclusiveFileLock:
    def __init__(self, path):
        self.path = path
        self.handle = None

    def __enter__(self):
        if os.name != "nt":
            return False
        kernel32 = ctypes.windll.kernel32
        handle = kernel32.CreateFileW(
            str(self.path), 0x80000000, 0, None, 3, 0x80, None
        )
        if handle == ctypes.c_void_p(-1).value:
            return False
        self.handle = handle
        return True

    def __exit__(self, exc_type, exc_value, traceback):
        if self.handle is not None:
            ctypes.windll.kernel32.CloseHandle(self.handle)


class IntelRunnerStageAContractTests(unittest.TestCase):
    def test_stage_a_workflow_is_manual_default_branch_owner_and_first_attempt_only(self):
        raw, document = _workflow(self)
        _assert_workflow_shape(self, document)
        self.assertNotIn("pull_request", document["on"])
        for mutation in (
            raw.replace(b"on:\n  workflow_dispatch:", b"on:\n  push:\n  workflow_dispatch:", 1),
            raw.replace(b"github.run_attempt == 1", b"github.run_attempt == 2", 1),
            raw.replace(b"github.actor == github.repository_owner", b"github.actor == 'other'", 1),
            raw.replace(b"github.triggering_actor == github.repository_owner", b"github.triggering_actor == 'other'", 1),
            raw.replace(b"inputs.confirm_authorised_runner == true", b"true", 1),
        ):
            with self.subTest(mutation=mutation[:40]):
                with self.assertRaises(AssertionError):
                    _assert_workflow_shape(self, _yaml_load(mutation))

    def test_stage_a_workflow_uses_hosted_preflight_then_exact_one_time_label(self):
        _, document = _workflow(self)
        validator_text = _strict_utf8(_required_file(self, VALIDATOR_PATH))
        _powershell_ast(self, VALIDATOR_PATH)
        _assert_runner_routing(self, document, validator_text)
        self.assertEqual(len(document["jobs"]), 2)
        mutations = []
        missing_output = copy.deepcopy(document)
        del missing_output["jobs"]["hosted-preflight"]["outputs"]["runner_label"]
        mutations.append(missing_output)
        raw_input_output = copy.deepcopy(document)
        raw_input_output["jobs"]["hosted-preflight"]["outputs"]["runner_label"] = "${{ inputs.runner_label }}"
        mutations.append(raw_input_output)
        fixed_label = copy.deepcopy(document)
        fixed_label["jobs"]["deterministic-runner"]["runs-on"] = "hardware-gate1-0123456789abcdef"
        mutations.append(fixed_label)
        second_job = copy.deepcopy(document)
        second_job["jobs"]["extra-self-hosted"] = {"runs-on": "self-hosted"}
        mutations.append(second_job)
        for mutation in mutations:
            with self.subTest(mutation=repr(mutation)[:60]):
                with self.assertRaises(AssertionError):
                    _assert_runner_routing(self, mutation, validator_text)

    def test_stage_a_workflow_reads_only_the_default_branch_approval_manifest(self):
        raw, document = _workflow(self)
        manifest_path = _required_file(self, MANIFEST_PATH)
        manifest_raw = manifest_path.read_bytes()
        _assert_approval_manifest(self, manifest_raw)
        self.assertEqual(list(document["jobs"]), ["hosted-preflight", "deterministic-runner"])
        hosted_text = "\n".join(str(step) for step in _steps(document, "hosted-preflight"))
        runner_text = "\n".join(str(step) for step in _steps(document, "deterministic-runner"))
        self.assertIn("default_branch", hosted_text)
        self.assertIn("llmfit-gate1-approved-source.json", hosted_text)
        self.assertIn("approved_sha", hosted_text)
        self.assertIn("source_ref", hosted_text)
        self.assertIn("approved_sha", runner_text)
        self.assertNotIn("feature/hardware-inspection", runner_text)
        self.assertNotRegex(runner_text, r"ref['\"]?\s*[:=]\s*['\"]?refs/heads/")
        runner_checkouts = [
            step
            for step in _steps(document, "deterministic-runner")
            if str(step.get("uses", "")).startswith("actions/checkout@")
        ]
        self.assertGreaterEqual(len(runner_checkouts), 2)
        self.assertTrue(
            any("approved_sha" in str(step.get("with", {}).get("ref", "")) for step in runner_checkouts)
        )
        for step in runner_checkouts:
            ref = str(step.get("with", {}).get("ref", ""))
            self.assertNotIn("feature/hardware-inspection", ref)
            self.assertNotRegex(ref, r"(?i)refs/heads/")
        for mutation in (
            manifest_raw + b'\n{"extra":"malformed"}',
            manifest_raw.replace(b'"schemaVersion":"1.0"', b'"schemaVersion":"2.0"', 1),
            manifest_raw.replace(b'"approvedTipSha":"', b'"approvedTipSha":"not-a-sha-', 1),
            manifest_raw.replace(
                b'"approvedTipSha":"',
                b'"approvedTipSha":"cc2e57ceb94e73e49f34fc383d5440a9047fba21","approvedTipSha":"',
                1,
            ),
        ):
            with self.subTest(manifest_mutation=mutation[:50]):
                with self.assertRaises((AssertionError, ValueError, json.JSONDecodeError)):
                    _assert_approval_manifest(self, mutation)
        branch_checkout = copy.deepcopy(document)
        branch_runner_checkouts = [
            step
            for step in _steps(branch_checkout, "deterministic-runner")
            if str(step.get("uses", "")).startswith("actions/checkout@")
        ]
        branch_runner_checkouts[-1].setdefault("with", {})["ref"] = "refs/heads/feature/hardware-inspection"
        with self.assertRaises(AssertionError):
            mutated_runner_text = "\n".join(str(step) for step in _steps(branch_checkout, "deterministic-runner"))
            self.assertNotIn("feature/hardware-inspection", mutated_runner_text)
            self.assertNotRegex(mutated_runner_text, r"ref['\"]?\s*[:=]\s*['\"]?refs/heads/")
        for mutation in (
            raw.replace(b"github.event.repository.default_branch", b"feature/hardware-inspection", 1),
            raw.replace(b"approved_sha", b"input_sha", 1),
        ):
            with self.subTest(mutation=mutation[:60]):
                mutated = _yaml_load(mutation)
                with self.assertRaises(AssertionError):
                    mutated_hosted = "\n".join(str(step) for step in _steps(mutated, "hosted-preflight"))
                    self.assertIn("default_branch", mutated_hosted)
                    self.assertIn("approved_sha", mutated_hosted)

    def test_stage_a_workflow_pins_actions_and_drops_checkout_credentials(self):
        raw, document = _workflow(self)
        uses = []
        checkout_steps = []
        for _, step in _all_steps(document):
            if "uses" in step:
                uses.append(step["uses"])
                if step["uses"].startswith("actions/checkout@"):
                    checkout_steps.append(step)
        self.assertEqual(set(uses), FULL_ACTION_PINS)
        self.assertGreaterEqual(len(checkout_steps), 2)
        for step in checkout_steps:
            self.assertEqual(step.get("with", {}).get("persist-credentials"), False)
        for mutation in (
            raw.replace(b"actions/setup-dotnet@d4c94342e560b34958eacfc5d055d21461ed1c5d", b"actions/setup-dotnet@v4", 1),
            raw.replace(b"persist-credentials: false", b"persist-credentials: true", 1),
        ):
            with self.subTest(mutation=mutation[:60]):
                mutated = _yaml_load(mutation)
                with self.assertRaises(AssertionError):
                    mutated_uses = [
                        step["uses"]
                        for _, step in _all_steps(mutated)
                        if "uses" in step
                    ]
                    self.assertEqual(set(mutated_uses), FULL_ACTION_PINS)
                    self.assertTrue(
                        all(
                            step.get("with", {}).get("persist-credentials") is False
                            for _, step in _all_steps(mutated)
                            if str(step.get("uses", "")).startswith("actions/checkout@")
                        )
                    )

    def test_stage_a_workflow_executes_only_the_two_deterministic_categories(self):
        raw, document = _workflow(self)
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        command_text = re.sub(r"\s+", " ", "\n".join(commands))
        ast_text = "\n".join(commands + strings)
        self.assertEqual(command_text.count("TestCategory=Deterministic"), 1)
        self.assertEqual(command_text.count("TestCategory=Task8Deterministic"), 1)
        self.assertIn("174", ast_text)
        self.assertIn("3", ast_text)
        self.assertEqual(sum(identity in ast_text for identity in TASK8_IDENTITIES), 3)
        self.assertNotIn("TrustedWindowsIntel", command_text)
        self.assertNotIn("TrustedOffline", command_text)
        self.assertNotIn("TestCategory=", runner_text.split("TestCategory=Deterministic", 1)[0])
        for mutation in (
            runner_text.replace("174", "173", 1),
            runner_text.replace(TASK8_IDENTITIES[0], TASK8_IDENTITIES[0] + "Extra", 1),
        ):
            mutated_commands, _ = _powershell_text_ast(self, mutation)
            with self.subTest(mutation=mutation[:50]):
                with self.assertRaises(AssertionError):
                    mutated_command_text = "\n".join(mutated_commands)
                    self.assertRegex(mutated_command_text, r"(?i)floor[^\n]*174")

    def test_stage_a_workflow_has_no_candidate_capture_offline_or_adapter_path(self):
        raw, document = _workflow(self)
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        executable_text = "\n".join(
            [str(step.get("run", "")) for _, step in _all_steps(document)]
            + commands
            + strings
        ).casefold()
        for identity in TASK8_IDENTITIES:
            executable_text = executable_text.replace(identity.casefold(), "")
        allowed_absence_check = "third-party\\bin\\llmfit\\v1.1.9\\win-x64"
        self.assertEqual(executable_text.count(allowed_absence_check), 1)
        executable_text = executable_text.replace(allowed_absence_check, "")
        for forbidden in (
            "candidate",
            "capture",
            "report",
            "trustedwindowsintel",
            "trustedoffline",
            "adapter",
            "netsh",
            "disable-netadapter",
            "enable-netadapter",
            "start-process",
            "invoke-webrequest",
            "curl",
            "wget",
            "granite_llmfit_",
        ):
            self.assertNotIn(forbidden, executable_text)
        for mutation in (
            runner_text.replace("Set-StrictMode", "Start-Process candidate; Set-StrictMode", 1),
            runner_text.replace("Set-StrictMode", "TrustedWindowsIntel; Set-StrictMode", 1),
            runner_text.replace("Set-StrictMode", "TrustedOffline; Set-StrictMode", 1),
            runner_text.replace("Set-StrictMode", "Disable-NetAdapter -Name Ethernet; Set-StrictMode", 1),
        ):
            mutated_commands, mutated_strings = _powershell_text_ast(self, mutation)
            with self.subTest(mutation=mutation[:50]):
                with self.assertRaises(AssertionError):
                    mutated_executable = "\n".join(mutated_commands + mutated_strings).casefold()
                    self.assertNotIn("candidate", mutated_executable)
                    self.assertNotIn("trustedwindowsintel", mutated_executable)
                    self.assertNotIn("trustedoffline", mutated_executable)
                    self.assertNotIn("disable-netadapter", mutated_executable)

    def test_stage_a_workflow_has_bounded_timeout_and_non_cancelling_concurrency(self):
        raw, document = _workflow(self)
        self.assertEqual(document["concurrency"], {
            "group": "hardware-inspection-llmfit-authorised",
            "cancel-in-progress": False,
        })
        self.assertEqual(document["jobs"]["hosted-preflight"]["timeout-minutes"], 15)
        self.assertEqual(document["jobs"]["deterministic-runner"]["timeout-minutes"], 35)
        for mutation in (
            raw.replace(b"cancel-in-progress: false", b"cancel-in-progress: true", 1),
            raw.replace(b"timeout-minutes: 35", b"timeout-minutes: 350", 1),
        ):
            with self.subTest(mutation=mutation[:60]):
                mutated = _yaml_load(mutation)
                with self.assertRaises(AssertionError):
                    self.assertEqual(mutated["concurrency"]["cancel-in-progress"], False)
                    self.assertEqual(mutated["jobs"]["deterministic-runner"]["timeout-minutes"], 35)

    def test_stage_a_validator_rejects_invalid_context_with_fixed_output(self):
        validator_text, commands, strings = _powershell_ast_text(self, VALIDATOR_PATH)
        self.assertIn("HI-RUNNER-STAGEA-INVALID", "\n".join(strings + commands + [validator_text]))
        for parameter in (
            "Phase", "ControlRoot", "WorkflowRef", "DefaultBranch", "Actor",
            "TriggeringActor", "RepositoryOwner", "RunAttempt", "Confirmation",
            "RunnerLabel", "SourceCheckoutRoot", "EvaluatedRoot", "ApprovedSha",
            "GitHubOutputPath", "SummaryPath",
        ):
            self.assertRegex(validator_text, rf"\$\(?{parameter}\)?")
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture_root = Path(temporary_directory)
            control_root = fixture_root / "control"
            control_root.mkdir()
            checkout_root = fixture_root / "checkout"
            approved_sha = _create_clean_checkout(checkout_root)
            _write_approval_manifest(control_root, approved_sha)
            environment = _validator_environment()
            for change in (
                {"Phase": "Invalid", "Confirmation": "canary"},
                {"Actor": "someone-else"},
                {"WorkflowRef": "refs/heads/feature/hardware-inspection"},
                {"RunnerLabel": "hardware-gate1-0123456789ABCDEG"},
                {"ControlRoot": r"\\localhost\stage-a-canary"},
                {"ControlRoot": r"\\?\C:\stage-a-canary"},
                {"ControlRoot": r"\\.\C:\stage-a-canary"},
            ):
                result = _invoke(
                    VALIDATOR_PATH,
                    _validator_arguments(control_root, **change),
                    environment,
                )
                _assert_invalid_validator_result(self, result)
                self.assertNotIn("canary", result.stdout + result.stderr)

            hosted_output = fixture_root / "hosted-output.txt"
            hosted = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=hosted_output,
                ),
                environment,
            )
            self.assertEqual(hosted.returncode, 0, hosted.stderr)
            self.assertEqual(hosted.stdout, "")
            self.assertEqual(hosted.stderr, "")
            self.assertEqual(
                hosted_output.read_bytes(),
                (
                    "source_ref=refs/heads/feature/hardware-inspection\n"
                    f"approved_sha={approved_sha}\n"
                    "runner_label=hardware-gate1-0123456789abcdef\n"
                    "eligible=true\n"
                ).encode("utf-8"),
            )
            hosted_output.write_bytes(b"prior-content-canary\n")
            with _ExclusiveFileLock(hosted_output) as locked:
                if locked:
                    entries_before = {path.name for path in fixture_root.iterdir()}
                    locked_result = _invoke(
                        VALIDATOR_PATH,
                        _validator_arguments(
                            control_root,
                            SourceCheckoutRoot=checkout_root,
                            GitHubOutputPath=hosted_output,
                        ),
                        environment,
                    )
            if os.name == "nt":
                self.assertTrue(locked, "exclusive output lock fixture is unavailable")
            if locked:
                _assert_invalid_validator_result(self, locked_result)
                self.assertEqual(hosted_output.read_bytes(), b"prior-content-canary\n")
                self.assertEqual({path.name for path in fixture_root.iterdir()}, entries_before)
            hosted_output.write_bytes(b"hosted-output-reset\n")
            for malformed_label in (
                "hardware-gate1-0123456789abcdef\n",
                "hardware-gate1-0123456789abcdef\r\n",
            ):
                result = _invoke_with_literal_runner_label(
                    VALIDATOR_PATH,
                    _validator_arguments(
                        control_root,
                        SourceCheckoutRoot=checkout_root,
                        GitHubOutputPath=hosted_output,
                    ),
                    malformed_label,
                    environment,
                )
                _assert_invalid_validator_result(self, result)
                self.assertNotIn("hardware-gate1-0123456789abcdef", result.stdout + result.stderr)

            reparse_control_root = fixture_root / "reparse-control-root"
            try:
                reparse_control_root.symlink_to(control_root, target_is_directory=True)
            except OSError:
                pass
            else:
                result = _invoke(
                    VALIDATOR_PATH,
                    _validator_arguments(
                        reparse_control_root,
                        SourceCheckoutRoot=checkout_root,
                        GitHubOutputPath=hosted_output,
                    ),
                    environment,
                )
                _assert_invalid_validator_result(self, result)

            forged_git_environment = environment.copy()
            forged_git_environment["GIT_DIR"] = str(checkout_root / ".git")
            forged_git_environment["GIT_WORK_TREE"] = str(checkout_root)
            forged_source = fixture_root / "forged-source"
            forged_source.mkdir()
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=forged_source,
                    GitHubOutputPath=hosted_output,
                ),
                forged_git_environment,
            )
            _assert_invalid_validator_result(self, result)

            for manifest_raw in (
                b'{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection","approvedTipSha":"' + approved_sha.encode("ascii") + b'","extra":true}',
                b'{"remoteFeatureRef":"refs/heads/feature/hardware-inspection","schemaVersion":"1.0","approvedTipSha":"' + approved_sha.encode("ascii") + b'"}',
                b'{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection","approvedTipSha":"' + approved_sha.encode("ascii") + b'","approvedTipSha":"' + approved_sha.encode("ascii") + b'"}',
                b'\xef\xbb\xbf{"schemaVersion":"1.0"}',
                b'{\xc2\xa0"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection","approvedTipSha":"' + approved_sha.encode("ascii") + b'"}',
                b'{\x0c"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection","approvedTipSha":"' + approved_sha.encode("ascii") + b'"}',
                b'{\x0b"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection","approvedTipSha":"' + approved_sha.encode("ascii") + b'"}',
            ):
                _write_approval_manifest(control_root, approved_sha, manifest_raw)
                result = _invoke(
                    VALIDATOR_PATH,
                    _validator_arguments(
                        control_root,
                        SourceCheckoutRoot=checkout_root,
                        GitHubOutputPath=hosted_output,
                    ),
                    environment,
                )
                _assert_invalid_validator_result(self, result)
            _write_approval_manifest(control_root, approved_sha)

            _write_approval_manifest(control_root, "a" * 40)
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=hosted_output,
                ),
                environment,
            )
            _assert_invalid_validator_result(self, result)
            _write_approval_manifest(control_root, approved_sha)

            (checkout_root / "identity.txt").write_text("dirty\n", encoding="utf-8", newline="\n")
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=hosted_output,
                ),
                environment,
            )
            _assert_invalid_validator_result(self, result)
            _git_output(checkout_root, "checkout", "--", "identity.txt")
            (checkout_root / "untracked.txt").write_text("dirty\n", encoding="utf-8", newline="\n")
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=hosted_output,
                ),
                environment,
            )
            _assert_invalid_validator_result(self, result)
            (checkout_root / "untracked.txt").unlink()

            invalid_git_root = fixture_root / "not-a-git-checkout"
            invalid_git_root.mkdir()
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=invalid_git_root,
                    GitHubOutputPath=hosted_output,
                ),
                environment,
            )
            _assert_invalid_validator_result(self, result)
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=fixture_root / "missing" / "output.txt",
                ),
                environment,
            )
            _assert_invalid_validator_result(self, result)

            summary_path = fixture_root / "stage-a-summary.md"
            runner = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    Phase="Runner",
                    ApprovedSha=approved_sha,
                    EvaluatedRoot=checkout_root,
                    SummaryPath=summary_path,
                ),
                environment,
            )
            self.assertEqual(runner.returncode, 0, runner.stderr)
            self.assertEqual(runner.stdout, "")
            self.assertEqual(runner.stderr, "")
            summary = summary_path.read_bytes()
            self.assertFalse(summary.startswith(codecs.BOM_UTF8))
            self.assertNotIn(b"\r", summary)
            self.assertIn(approved_sha.encode("ascii"), summary)
            self.assertNotIn(b"hardware-gate1", summary)
            self.assertNotIn(str(checkout_root).encode("utf-8"), summary)

            for environment_name in (
                "GRANITE_LLMFIT_CANDIDATE_ROOT",
                "GRANITE_LLMFIT_TRUSTED_OUTPUT",
                "GRANITE_LLMFIT_GATE1_OUTPUT",
                "GRANITE_LLMFIT_WINDOWS_REFERENCE",
                "GRANITE_LLMFIT_OFFLINE_OUTPUT",
                "GRANITE_LLMFIT_FAKE_TOOL_ROOT",
            ):
                operational_environment = environment.copy()
                operational_environment[environment_name] = "privacy-canary"
                result = _invoke(
                    VALIDATOR_PATH,
                    _validator_arguments(
                        control_root,
                        Phase="Runner",
                        ApprovedSha=approved_sha,
                        EvaluatedRoot=checkout_root,
                        SummaryPath=summary_path,
                    ),
                    operational_environment,
                )
                _assert_invalid_validator_result(self, result)
                self.assertNotIn("privacy-canary", result.stdout + result.stderr)

            candidate_root = checkout_root / "third-party" / "bin" / "llmfit" / "v1.1.9" / "win-x64"
            candidate_root.mkdir(parents=True)
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    Phase="Runner",
                    ApprovedSha=approved_sha,
                    EvaluatedRoot=checkout_root,
                    SummaryPath=summary_path,
                ),
                environment,
            )
            _assert_invalid_validator_result(self, result)
            shutil.rmtree(candidate_root.parents[3])
            result = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    Phase="Runner",
                    ApprovedSha="b" * 40,
                    EvaluatedRoot=checkout_root,
                    SummaryPath=fixture_root / "missing" / "summary.md",
                ),
                environment,
            )
            _assert_invalid_validator_result(self, result)

    def test_stage_a_runner_rejects_dirty_wrong_sha_or_operational_environment(self):
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        executable = "\n".join(commands + strings)
        for required_parameter in (
            "EvaluatedRoot", "ApprovedSha", "LocalWorkRoot", "SummaryJsonPath", "SummaryMarkdownPath",
        ):
            self.assertRegex(runner_text, rf"\${required_parameter}\b")
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            arguments = {
                "EvaluatedRoot": root,
                "ApprovedSha": "0" * 40,
                "LocalWorkRoot": root / "work",
                "SummaryJsonPath": root / "summary.json",
                "SummaryMarkdownPath": root / "summary.md",
            }
            for environment_name in (None, "GRANITE_LLMFIT_MODEL_ROOT"):
                environment = os.environ.copy()
                if environment_name:
                    environment[environment_name] = "canary"
                result = _invoke(RUNNER_PATH, arguments, environment)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stderr.replace("\r\n", "\n"), INVALID_RUNNER_STDERR)
                self.assertNotIn("canary", result.stdout + result.stderr)
        self.assertRegex(executable, r"(?i)status\s+--porcelain|clean")
        self.assertRegex(executable, r"(?i)rev-parse\s+HEAD|approvedsha")
        self.assertRegex(executable, r"GRANITE_LLMFIT_")
        self.assertRegex(runner_text, r"\$workRoot\s+-cne\s+\$evaluated")

    def test_stage_a_runner_requires_exact_trx_identities_and_zero_nonpassing(self):
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        executable = "\n".join(commands + strings)
        for identity in TASK8_IDENTITIES:
            self.assertEqual(executable.count(identity), 1)
        self.assertIn("174", executable)
        self.assertIn("3", executable)
        self.assertRegex(executable, r"(?i)non.?passing|failed|skipped|not.?executed")
        self.assertRegex(executable, r"(?i)DTD|DtdProcessing|XmlReaderSettings")
        mutations = (
            (runner_text.replace("174", "173"), lambda text: self.assertIn("174", text)),
            (
                runner_text.replace("nonPassing", "nonPassingRelaxed"),
                lambda text: self.assertRegex(text, r"(?<![A-Za-z])nonPassing(?![A-Za-z])"),
            ),
        )
        for mutation, predicate in mutations:
            mutated_commands, mutated_strings = _powershell_text_ast(self, mutation)
            with self.subTest(mutation=mutation[:50]):
                with self.assertRaises(AssertionError):
                    mutated = "\n".join(mutated_commands + mutated_strings)
                    predicate(mutated)
        with tempfile.TemporaryDirectory() as temporary_directory:
            fixture_root = Path(temporary_directory)
            for kind in ("Deterministic", "Task8Deterministic"):
                valid = _stage_a_trx_fixture(kind)
                fixture = fixture_root / (kind + ".trx")
                fixture.write_text(valid, encoding="utf-8", newline="\n")
                result = _invoke_runner_trx_fixture(fixture, kind)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(result.stdout, "ok")
                mutations = {
                    "duplicate-id": valid.replace(
                        'testId="00000002-0000-0000-0000-000000000001"',
                        'testId="00000001-0000-0000-0000-000000000001"',
                        1,
                    ),
                    "missing-entry": valid.replace("<TestEntries>", "<TestEntries>", 1).replace(
                        '<TestEntry testId="00000001-0000-0000-0000-000000000001" executionId="00000001-0000-0000-0000-000000000002" />',
                        "",
                        1,
                    ),
                    "relabelled-id": valid.replace(
                        f'testName="{TASK8_IDENTITIES[0] if kind == "Task8Deterministic" else "Deterministic_000"}"',
                        'testName="Relabelled"',
                        1,
                    ),
                    "dtd": valid.replace("<TestRun ", "<!DOCTYPE TestRun [<!ENTITY xxe SYSTEM 'file:///x'>]><TestRun ", 1),
                    "stale-extra": valid.replace("</Results>", '<UnitTestResult testName="Stale" outcome="Passed" testId="ffffffff-0000-0000-0000-000000000001" executionId="ffffffff-0000-0000-0000-000000000002" /></Results>', 1),
                    "nonpass": valid.replace('outcome="Passed"', 'outcome="Failed"', 1),
                    "counters": valid.replace('passed="', 'passed="999', 1),
                    "missing-counters": valid.replace("<Counters ", "<MissingCounters ", 1).replace(" /></ResultSummary>", " /></ResultSummary>", 1),
                }
                for name, mutated in mutations.items():
                    with self.subTest(kind=kind, mutation=name):
                        fixture.write_text(mutated, encoding="utf-8", newline="\n")
                        result = _invoke_runner_trx_fixture(fixture, kind)
                        self.assertNotEqual(result.returncode, 0)

    def test_stage_a_summary_is_allowlisted_and_raw_artifacts_stay_local(self):
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        executable = "\n".join(commands + strings)
        self.assertIn('"schemaVersion"', executable)
        self.assertIn('"evaluatedSha"', executable)
        self.assertIn('"deterministicPassed"', executable)
        self.assertIn('"task8DeterministicPassed"', executable)
        self.assertIn('"nonPassing"', executable)
        for forbidden in ("MachineName", "upload"):
            self.assertNotIn(forbidden.casefold(), executable.casefold())
        self.assertIn("deterministic.trx", executable.casefold())
        self.assertIn("task8.trx", executable.casefold())
        self.assertNotIn("Write-Output", executable)
        if WORKFLOW_PATH.is_file():
            _, document = _workflow(self)
            _assert_summary_upload(self, document)
            workflow_runs = "\n".join(str(step.get("run", "")) for _, step in _all_steps(document))
            _assert_no_host_or_path_leaks(self, workflow_runs)
            trx_upload = copy.deepcopy(document)
            trx_steps = [
                step for _, step in _all_steps(trx_upload)
                if str(step.get("uses", "")).startswith("actions/upload-artifact@")
            ]
            trx_steps[0].setdefault("with", {})["path"] = "local\\deterministic.trx"
            with self.assertRaises(AssertionError):
                _assert_summary_upload(self, trx_upload)
        mutations = (
            (
                runner_text.replace('"nonPassing"', '"host" : "canary", "nonPassing"', 1),
                lambda text: self.assertNotIn("host", text.casefold()),
            ),
            (
                runner_text.replace("Set-StrictMode", "Write-Output 'C:\\Users\\canary'; Set-StrictMode", 1),
                lambda text: _assert_no_host_or_path_leaks(self, text),
            ),
            (
                runner_text.replace("Set-StrictMode", "Write-Output $env:COMPUTERNAME; Set-StrictMode", 1),
                lambda text: _assert_no_host_or_path_leaks(self, text),
            ),
        )
        for mutation, predicate in mutations:
            mutated_commands, mutated_strings = _powershell_text_ast(self, mutation)
            with self.subTest(mutation=mutation[:50]):
                with self.assertRaises(AssertionError):
                    mutated = "\n".join(mutated_commands + mutated_strings)
                    predicate(mutated)

    def test_stage_a_inventory_cannot_activate_stage_b_c_d_or_gate_2(self):
        _required_file(self, WORKFLOW_PATH)
        _required_file(self, VALIDATOR_PATH)
        _required_file(self, RUNNER_PATH)
        _required_file(self, RUNBOOK_PATH)
        workflow_paths = {
            path.relative_to(REPOSITORY_ROOT).as_posix()
            for path in (REPOSITORY_ROOT / ".github" / "workflows").rglob("*")
            if path.is_file()
        }
        stage_a_paths = {
            path for path in workflow_paths
            if re.search(r"hardware-inspection-intel-runner-stage-[a-d]\.ya?ml\Z", path, re.I)
        }
        self.assertEqual(stage_a_paths, {WORKFLOW_PATH.relative_to(REPOSITORY_ROOT).as_posix()})
        forbidden = {"stage-b", "stage-c", "stage-d", "gate-2", "gate2"}
        for path in workflow_paths:
            self.assertFalse(any(token in path.casefold() for token in forbidden), path)
        for mutation in (
            workflow_paths | {".github/workflows/hardware-inspection-intel-runner-stage-b.yml"},
            workflow_paths | {".github/workflows/hardware-inspection-intel-runner-gate-2.yml"},
        ):
            with self.subTest(mutation=sorted(mutation)[-1]):
                with self.assertRaises(AssertionError):
                    mutated_stage_paths = {
                        path for path in mutation
                        if re.search(r"hardware-inspection-intel-runner-stage-[a-d]\.ya?ml\Z", path, re.I)
                    }
                    self.assertEqual(
                        mutated_stage_paths,
                        {WORKFLOW_PATH.relative_to(REPOSITORY_ROOT).as_posix()},
                    )


if __name__ == "__main__":
    unittest.main()
