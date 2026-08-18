import base64
import codecs
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
    command = r"""
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($args[0], [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { exit 2 }
foreach ($node in $ast.FindAll({ param($candidate) $candidate -is [System.Management.Automation.Language.CommandAst] }, $true)) {
  [Console]::WriteLine('COMMAND:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($node.Extent.Text)))
}
foreach ($node in $ast.FindAll({ param($candidate) $candidate -is [System.Management.Automation.Language.StringConstantExpressionAst] }, $true)) {
  [Console]::WriteLine('STRING:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($node.Value)))
}
"""
    result = subprocess.run(
        [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command", command, str(path)],
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
    test_case.assertEqual(jobs["hosted-preflight"]["runs-on"], "windows-latest")
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


class IntelRunnerStageAContractTests(unittest.TestCase):
    def test_stage_a_workflow_is_manual_default_branch_owner_and_first_attempt_only(self):
        raw, document = _workflow(self)
        _assert_workflow_shape(self, document)
        self.assertNotIn("pull_request", document["on"])
        for mutation in (
            raw.replace(b"on:\n  workflow_dispatch:", b"on:\n  push:\n  workflow_dispatch:", 1),
            raw.replace(b"github.run_attempt == 1", b"github.run_attempt == 2", 1),
            raw.replace(b"github.actor == github.repository_owner", b"github.actor == 'other'", 1),
            raw.replace(b"inputs.confirm_authorised_runner == true", b"true", 1),
        ):
            with self.subTest(mutation=mutation[:40]):
                with self.assertRaises(AssertionError):
                    _assert_workflow_shape(self, _yaml_load(mutation))

    def test_stage_a_workflow_uses_hosted_preflight_then_exact_one_time_label(self):
        raw, document = _workflow(self)
        validator_text = _strict_utf8(_required_file(self, VALIDATOR_PATH))
        _powershell_ast(self, VALIDATOR_PATH)
        _assert_runner_routing(self, document, validator_text)
        self.assertEqual(len(document["jobs"]), 2)
        for mutation in (
            raw.replace(
                b"${{ needs.hosted-preflight.outputs.runner_label }}",
                b"${{ inputs.runner_label }}",
                1,
            ),
            raw.replace(
                b"${{ needs.hosted-preflight.outputs.runner_label }}",
                b"hardware-gate1-0123456789abcdef",
                1,
            ),
            raw.replace(b"jobs:\n", b"jobs:\n  extra-self-hosted:\n    runs-on: self-hosted\n", 1),
        ):
            with self.subTest(mutation=mutation[:60]):
                mutated = _yaml_load(mutation)
                with self.assertRaises(AssertionError):
                    _assert_runner_routing(self, mutated, validator_text)

    def test_stage_a_workflow_reads_only_the_default_branch_approval_manifest(self):
        raw, document = _workflow(self)
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
            _, mutated_commands, _ = _powershell_text_ast(self, mutation)
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
            runner_text.replace("param(", "Start-Process candidate; param(", 1),
            runner_text.replace("param(", "TrustedWindowsIntel; param(", 1),
            runner_text.replace("param(", "Disable-NetAdapter -Name Ethernet; param(", 1),
        ):
            _, mutated_commands, mutated_strings = _powershell_text_ast(self, mutation)
            with self.subTest(mutation=mutation[:50]):
                with self.assertRaises(AssertionError):
                    mutated_executable = "\n".join(mutated_commands + mutated_strings).casefold()
                    self.assertNotIn("candidate", mutated_executable)
                    self.assertNotIn("trustedwindowsintel", mutated_executable)
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
            "RunnerLabel",
        ):
            self.assertRegex(validator_text, rf"\$\(?{parameter}\)?")
        with tempfile.TemporaryDirectory() as temporary_directory:
            control_root = Path(temporary_directory)
            manifest = control_root / ".github" / "hardware-inspection" / MANIFEST_PATH.name
            manifest.parent.mkdir(parents=True)
            manifest.write_text(
                '{"schemaVersion":"1.0","remoteFeatureRef":"refs/heads/feature/hardware-inspection",'
                '"approvedTipSha":"cc2e57ceb94e73e49f34fc383d5440a9047fba21"}',
                encoding="utf-8",
                newline="\n",
            )
            for change in (
                {"Phase": "Invalid", "Confirmation": "canary"},
                {"Actor": "someone-else"},
                {"WorkflowRef": "refs/heads/feature/hardware-inspection"},
                {"RunnerLabel": "hardware-gate1-0123456789ABCDEG"},
            ):
                result = _invoke(VALIDATOR_PATH, _validator_arguments(control_root, **change))
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stderr.replace("\r\n", "\n"), INVALID_VALIDATOR_STDERR)
                self.assertNotIn("canary", result.stdout + result.stderr)

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
            (runner_text.replace("174", "173", 1), lambda text: self.assertIn("174", text)),
            (
                runner_text.replace("nonPassing", "nonPassingRelaxed", 1),
                lambda text: self.assertRegex(text, r"(?<![A-Za-z])nonPassing(?![A-Za-z])"),
            ),
        )
        for mutation, predicate in mutations:
            _, mutated_commands, mutated_strings = _powershell_text_ast(self, mutation)
            with self.subTest(mutation=mutation[:50]):
                with self.assertRaises(AssertionError):
                    mutated = "\n".join(mutated_commands + mutated_strings)
                    predicate(mutated)

    def test_stage_a_summary_is_allowlisted_and_raw_artifacts_stay_local(self):
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        _, document = _workflow(self)
        executable = "\n".join(commands + strings)
        self.assertIn('"schemaVersion"', executable)
        self.assertIn('"evaluatedSha"', executable)
        self.assertIn('"deterministicPassed"', executable)
        self.assertIn('"task8DeterministicPassed"', executable)
        self.assertIn('"nonPassing"', executable)
        for forbidden in ("stdout", "stderr", ".trx", "raw", "ComputerName", "MachineName", "upload"):
            self.assertNotIn(forbidden.casefold(), executable.casefold())
        upload_steps = [
            step for _, step in _all_steps(document)
            if str(step.get("uses", "")).startswith("actions/upload-artifact@")
        ]
        self.assertEqual(len(upload_steps), 1)
        upload_path = json.dumps(upload_steps[0].get("with", {})).casefold()
        self.assertIn("summary", upload_path)
        self.assertNotIn(".trx", upload_path)
        for mutation in (
            runner_text.replace('"nonPassing"', '"host" : "canary", "nonPassing"', 1),
            runner_text.replace("SummaryJsonPath", "TrxUploadPath", 1),
        ):
            _, mutated_commands, mutated_strings = _powershell_text_ast(self, mutation)
            with self.subTest(mutation=mutation[:50]):
                with self.assertRaises(AssertionError):
                    mutated = "\n".join(mutated_commands + mutated_strings).casefold()
                    self.assertNotIn("canary", mutated)
                    self.assertNotIn("trxuploadpath", mutated)

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
