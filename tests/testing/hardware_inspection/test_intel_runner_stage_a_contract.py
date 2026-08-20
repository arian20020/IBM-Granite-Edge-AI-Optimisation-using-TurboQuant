import base64
import codecs
import copy
import ctypes
import hashlib
import json
import os
import re
import signal
import shutil
import subprocess
import tempfile
import time
import unittest
import xml.etree.ElementTree as ET
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
TESTING_INDEX_PATH = REPOSITORY_ROOT / "docs" / "testing" / "README.md"
SCRIPTS_INDEX_PATH = REPOSITORY_ROOT / "scripts" / "README.md"
PLAN_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "superpowers"
    / "plans"
    / "2026-08-18-hardware-inspection-intel-runner-stage-a.md"
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
ONE_RUN_DEPENDENCY_DIRECTORIES = {
    "DOTNET_INSTALL_DIR": "sdk",
    "DOTNET_CLI_HOME": "cli-home",
    "NUGET_PACKAGES": "nuget-packages",
    "NUGET_HTTP_CACHE_PATH": "nuget-http-cache",
    "NUGET_PLUGINS_CACHE_PATH": "nuget-plugins-cache",
    "NUGET_SCRATCH": "nuget-scratch",
}
RUNNER_LABEL = re.compile(r"\Ahardware-gate1-[0-9a-f]{16}\Z")
EXPECTED_WORKFLOW_SHA256 = "953167cdfcb983ae6d0ca00831d35fcdf826570001ab7721f1b835ab423c37a5"
EXPECTED_RUNBOOK_SHA256 = "cfef60a33c09e11fbd913a406c25852cad4ca263011fe42091f28059e9f1a51c"
STAGEA_POWERSHELL_SHELL = (
    r'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe '
    r'-NoLogo -NoProfile -NonInteractive '
    '-Command "$ErrorActionPreference = \'Stop\'; $global:LASTEXITCODE = 0; & \'{0}\'; '
    'if (-not $?) { if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; exit 1 }; '
    'exit $LASTEXITCODE"'
)
GIT_INVALID_STDERR = (
    "HI-RUNNER-STAGEA-GIT-INVALID: required Git capability is unavailable.\n"
)


def _required_file(test_case, path):
    test_case.assertTrue(path.is_file(), f"required Stage A file is missing: {path}")
    return path


def _strict_utf8(path):
    raw = path.read_bytes()
    if raw.startswith(codecs.BOM_UTF8):
        raise AssertionError(f"{path.name} must not have a UTF-8 BOM")
    without_crlf = raw.replace(b"\r\n", b"")
    if b"\r" in without_crlf:
        raise AssertionError(f"{path.name} contains a lone carriage return")
    if b"\r\n" in raw and b"\n" in without_crlf:
        raise AssertionError(f"{path.name} mixes LF and CRLF line endings")
    return raw.decode("utf-8", "strict").replace("\r\n", "\n")


def _assert_canonical_file_bytes(test_case, path, allow_crlf=False):
    raw = path.read_bytes()
    test_case.assertFalse(raw.startswith(codecs.BOM_UTF8))
    raw.decode("utf-8", "strict")
    without_crlf = raw.replace(b"\r\n", b"")
    test_case.assertNotIn(b"\r", without_crlf)
    if allow_crlf and b"\r\n" in raw:
        test_case.assertNotIn(b"\n", without_crlf)
        test_case.assertTrue(raw.endswith(b"\r\n"))
        test_case.assertFalse(raw.endswith(b"\r\n\r\n"))
    else:
        test_case.assertNotIn(b"\r", raw)
        test_case.assertTrue(raw.endswith(b"\n"))
        test_case.assertFalse(raw.endswith(b"\n\n"))


def _assert_pre_registration_identity_privacy(test_case, runbook):
    for required_text in (
        "before registration, use the ucl-approved local no-echo procedure",
        "actual windows computer name",
        "expected runner-group display value",
        "both values must be explicitly ucl-approved and non-identifying",
        "an unknown or unsafe identity is a hard stop",
        "do not print, echo, or store either value",
        "repository, chat, retained command history, screenshot, or log",
        "workflow masks cannot remediate this pre-step metadata",
        "do not rename the laptop unless ucl separately authorises the rename",
    ):
        test_case.assertIn(required_text, runbook)


def _assert_pre_step_proxy_and_debug_privacy(test_case, runbook):
    for required_text in (
        "before registration and again before `run.cmd`",
        "ucl-approved local no-echo procedure",
        "runner-consumed proxy settings and configuration",
        "`http_proxy`, `https_proxy`, and `no_proxy` case-insensitively",
        "runner `.env` and service context",
        "absent or its exact displayed metadata must be explicitly ucl-approved and non-identifying",
        "unknown or unsafe proxy metadata is a hard stop",
        "do not print, echo, or store a proxy uri or proxy metadata",
        "do not clear or reconfigure proxy state merely to continue",
        "`actions_runner_debug`, `actions_step_debug`, and `runner_debug`",
        "runner trace and print-log controls",
        "must all be absent before start",
        "pre-step debug or trace disclosure is irreversible",
    ):
        test_case.assertIn(required_text, runbook)


def _assert_pre_step_hook_absence(test_case, runbook):
    for required_text in (
        "before registration and repeat after registration immediately before `run.cmd`",
        "`actions_runner_hook_job_started`",
        "`actions_runner_hook_job_completed`",
        "effective process, user, and system environment",
        "fresh runner-root `.env`",
        "either value or entry is a hard stop",
        "never execute, clear, or repair it",
        "hooks run outside workflow steps",
        "the workflow's first guard is too late",
    ):
        test_case.assertIn(required_text, runbook)


def _assert_pre_step_action_cache_override_absence(test_case, runbook):
    for required_text in (
        "before registration and again immediately before `run.cmd`",
        "`actions_runner_action_archive_cache`",
        "`actions_runner_symlink_cached_actions`",
        "effective process, user, and machine/system environment",
        "fresh runner-root `.env`",
        "any value or entry is a hard stop",
        "must not execute, clear, repair, or override it merely to continue",
        "action materialisation before the first workflow step",
        "first-step workflow check is defence in depth only",
    ):
        test_case.assertIn(required_text, runbook)


def _assert_one_run_dependency_cleanup_runbook(test_case, runbook):
    for required_text in (
        "one-run sdk and nuget state",
        "canonical stage a phase directory",
        "before `actions/setup-dotnet`",
        "fresh, absent, ordinary direct children",
        "program files, userprofile, a browser profile, onedrive, a network location, or an unrelated machine-wide cache",
        "remove only all three separately revalidated exact targets",
    ):
        test_case.assertIn(required_text, runbook)
    for variable, child in ONE_RUN_DEPENDENCY_DIRECTORIES.items():
        test_case.assertIn(f"`{variable.casefold()}`", runbook)
        test_case.assertIn(f"`{child}`", runbook)


def _assert_one_run_dependency_plan_order(test_case, plan):
    for required_text in (
        "create and bind the canonical one-run sdk/nuget phase root and its six fresh dependency children before setup-dotnet",
        "set up .net from `evaluated/global.json` using the runnercontext-bound sdk/nuget paths",
        "revalidate the existing phase root and all six dependency children",
        "require the direct output child to be absent",
    ):
        test_case.assertIn(required_text, plan)


def _assert_hidden_prompt_token_handling(test_case, runbook):
    for required_text in (
        "do not run or paste github's displayed token-bearing command",
        "use it only to obtain the trusted repository url and transient token",
        "prove `actions_runner_input_token` is absent",
        "omit `--token`. enter the token only at the runner's hidden secret prompt",
        "hard stop if the supported runner does not offer a non-echoing prompt",
        ".\\config.cmd --url <trusted-repository-url> --name <fresh-non-identifying-runner-name> --ephemeral --no-default-labels --labels <fresh-label> --work <fresh-work-directory>",
        "for `config.cmd remove`, likewise omit `--token`",
        "enter the time-limited removal token only at its hidden secret prompt",
    ):
        test_case.assertIn(required_text, runbook)
    code_blocks = re.findall(r"```(?:text)?\n(.*?)```", runbook, re.DOTALL)
    test_case.assertTrue(code_blocks)
    for code_block in code_blocks:
        test_case.assertNotIn("--token", code_block)
        test_case.assertNotIn("actions_runner_input_token", code_block)


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
    test_case.assertEqual(upload_with.get("include-hidden-files"), False)
    test_case.assertNotIn("hidden-files", upload_with)
    upload_path = json.dumps(upload_with).casefold()
    test_case.assertIn("summary", upload_path)
    test_case.assertNotIn(".trx", upload_path)
    test_case.assertNotRegex(upload_path, r"(?:[a-z]:\\|\\\\|runner\.|computername|hostname)")


def _assert_no_host_or_path_leaks(test_case, text):
    text = text.replace("\\\\?\\", "")
    test_case.assertNotRegex(text, r"(?i)\$\{\{\s*(?:runner\.|github\.workspace|github\.event\.runner)")
    test_case.assertNotRegex(text, r"(?i)\$env:(?:computername|username|userdomain|hostname)")
    test_case.assertNotRegex(text, r"(?i)(?:[a-z]:\\|\\\\[^\s\\]+\\|/home/|/Users/)")


def _assert_no_forbidden_runner_commands(test_case, commands):
    executable = "\n".join(commands).casefold()
    for forbidden in (
        "start-process", "invoke-webrequest", "curl", "wget",
        "trustedwindowsintel", "trustedoffline", "disable-netadapter",
        "enable-netadapter", "netsh", "invoke-expression",
    ):
        test_case.assertNotIn(forbidden, executable)


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
    if re.fullmatch(r"0|[1-9][0-9]*", value):
        return int(value)
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
    test_case.assertFalse(raw.startswith(codecs.BOM_UTF8))
    test_case.assertNotIn(b"\r", raw)
    test_case.assertEqual(hashlib.sha256(raw).hexdigest(), EXPECTED_WORKFLOW_SHA256)
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
    for job_name in ("hosted-preflight", "deterministic-runner"):
        guard = document["jobs"][job_name]["if"]
        for expression in (
            "github.event_name == 'workflow_dispatch'",
            "github.ref == format('refs/heads/{0}', github.event.repository.default_branch)",
            "github.actor == github.repository_owner",
            "github.triggering_actor == github.repository_owner",
            "github.run_attempt == 1",
            "inputs.confirm_authorised_runner == true",
        ):
            test_case.assertIn(expression, guard)
    hosted_guard = re.sub(r"\s+", "", document["jobs"]["hosted-preflight"]["if"])
    runner_guard = re.sub(r"\s+", "", document["jobs"]["deterministic-runner"]["if"])
    required_guard = "github.event_name=='workflow_dispatch'&&github.ref==format('refs/heads/{0}',github.event.repository.default_branch)&&github.actor==github.repository_owner&&github.triggering_actor==github.repository_owner&&github.run_attempt==1&&inputs.confirm_authorised_runner==true"
    test_case.assertEqual(hosted_guard, required_guard)
    test_case.assertEqual(runner_guard, required_guard + "&&needs.hosted-preflight.outputs.eligible=='true'")


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
    runner_steps = jobs["deterministic-runner"]["steps"]
    runner_names = [step.get("name") for step in runner_steps]
    required_order = [
        "Reject debug controls and mask runner metadata",
        "Check out default-branch controls",
        "Validate runner context before evaluated checkout",
        "Check out approved evaluated source",
        "Validate evaluated source before execution",
        "Set up .NET from the evaluated source",
        "Run authorised deterministic validation",
        "Validate Stage A summary artifacts before upload",
        "Upload Stage A summary",
        "Publish validated Stage A summary",
        "Check Stage A residue",
    ]
    test_case.assertEqual(runner_names, required_order)
    debug_step = runner_steps[0]
    test_case.assertIn("${{ runner.debug }}", str(debug_step))
    test_case.assertIn("RUNNER_DEBUG", str(debug_step.get("run", "")))
    test_case.assertIn("RunnerContext", str(runner_steps[2].get("run", "")))
    test_case.assertIn("-Phase Runner", str(runner_steps[4].get("run", "")))
    test_case.assertEqual(runner_steps[-1].get("if"), "${{ always() }}")
    test_case.assertEqual(runner_steps[-1].get("timeout-minutes"), 2)


def _normalized_powershell(text):
    return re.sub(r"\s+", " ", str(text)).strip()


def _assert_workflow_executable_chain(test_case, document):
    hosted_steps = _steps(document, "hosted-preflight")
    runner_steps = _steps(document, "deterministic-runner")
    test_case.assertEqual(
        [step.get("name") for step in hosted_steps],
        [
            "Reject debug controls before runner eligibility",
            "Check out default-branch controls",
            "Set up Python for control contracts",
            "Run Stage 0 and Stage A control contracts",
            "Parse the default-branch llmfit-gate1-approved-source.json approval manifest",
            "Check out approved source identity only",
            "Validate approved source and runner label",
        ],
    )
    def ast_commands(step):
        commands, _ = _powershell_text_ast(test_case, str(step.get("run", "")))
        return [_normalized_powershell(command) for command in commands]

    test_case.assertEqual(
        ast_commands(hosted_steps[3]),
        [
            "python -m unittest -v "
            "tests.testing.hardware_inspection.test_intel_runner_stage0_contract "
            "tests.testing.hardware_inspection.test_intel_runner_stage_a_contract"
        ],
    )
    expected_stage0 = (
        "& '.\\control\\scripts\\hardware-inspection\\Validate-HardwareInspectionIntelRunnerStage0.ps1' ` "
        "-Phase Dispatch ` -ControlRoot (Join-Path $env:GITHUB_WORKSPACE 'control') ` "
        "-WorkflowRef $env:STAGE0_WORKFLOW_REF ` -DefaultBranch $env:STAGE0_DEFAULT_BRANCH ` "
        "-Actor $env:STAGE0_ACTOR ` -TriggeringActor $env:STAGE0_TRIGGERING_ACTOR ` "
        "-RepositoryOwner $env:STAGE0_REPOSITORY_OWNER ` -RunAttempt $env:STAGE0_RUN_ATTEMPT ` "
        "-ConfirmRepositoryOnly $env:STAGE0_CONFIRMATION ` -GitHubOutputPath $env:GITHUB_OUTPUT"
    )
    expected_hosted = (
        "& '.\\control\\scripts\\hardware-inspection\\Validate-HardwareInspectionIntelRunnerStageA.ps1' ` "
        "-Phase Hosted ` -ControlRoot (Join-Path $env:GITHUB_WORKSPACE 'control') ` "
        "-SourceCheckoutRoot (Join-Path $env:GITHUB_WORKSPACE 'source-identity') ` "
        "-WorkflowRef $env:STAGEA_WORKFLOW_REF ` -DefaultBranch $env:STAGEA_DEFAULT_BRANCH ` "
        "-Actor $env:STAGEA_ACTOR ` -TriggeringActor $env:STAGEA_TRIGGERING_ACTOR ` "
        "-RepositoryOwner $env:STAGEA_REPOSITORY_OWNER ` -RunAttempt $env:STAGEA_RUN_ATTEMPT ` "
        "-Confirmation $env:STAGEA_CONFIRMATION ` -RunnerLabel $env:STAGEA_RUNNER_LABEL ` "
        "-GitHubOutputPath $env:GITHUB_OUTPUT"
    )
    expected_context = (
        "& '.\\control\\scripts\\hardware-inspection\\Validate-HardwareInspectionIntelRunnerStageA.ps1' ` "
        "-Phase RunnerContext ` -ControlRoot (Join-Path $env:GITHUB_WORKSPACE 'control') ` "
        "-EvaluatedRoot (Join-Path $env:GITHUB_WORKSPACE 'evaluated') ` "
        "-ApprovedSha $env:STAGEA_APPROVED_SHA ` -WorkflowRef $env:STAGEA_WORKFLOW_REF ` "
        "-DefaultBranch $env:STAGEA_DEFAULT_BRANCH ` -Actor $env:STAGEA_ACTOR ` "
        "-TriggeringActor $env:STAGEA_TRIGGERING_ACTOR ` -RepositoryOwner $env:STAGEA_REPOSITORY_OWNER ` "
        "-RunAttempt $env:STAGEA_RUN_ATTEMPT ` -Confirmation $env:STAGEA_CONFIRMATION ` "
        "-RunnerLabel $env:STAGEA_RUNNER_LABEL ` -RunnerTemp $env:RUNNER_TEMP ` "
        "-RunnerWorkspace $env:RUNNER_WORKSPACE ` "
        "-DependencyEnvironmentPath $env:GITHUB_ENV"
    )
    expected_runner_validation = (
        "& '.\\control\\scripts\\hardware-inspection\\Validate-HardwareInspectionIntelRunnerStageA.ps1' ` "
        "-Phase Runner ` -ControlRoot (Join-Path $env:GITHUB_WORKSPACE 'control') ` "
        "-EvaluatedRoot (Join-Path $env:GITHUB_WORKSPACE 'evaluated') ` "
        "-ApprovedSha $env:STAGEA_APPROVED_SHA ` -WorkflowRef $env:STAGEA_WORKFLOW_REF ` "
        "-DefaultBranch $env:STAGEA_DEFAULT_BRANCH ` -Actor $env:STAGEA_ACTOR ` "
        "-TriggeringActor $env:STAGEA_TRIGGERING_ACTOR ` -RepositoryOwner $env:STAGEA_REPOSITORY_OWNER ` "
        "-RunAttempt $env:STAGEA_RUN_ATTEMPT ` -Confirmation $env:STAGEA_CONFIRMATION ` "
        "-RunnerLabel $env:STAGEA_RUNNER_LABEL"
    )
    expected_runner = (
        "& '.\\control\\scripts\\hardware-inspection\\Invoke-HardwareInspectionIntelRunnerStageA.ps1' ` "
        "-EvaluatedRoot 'evaluated' ` -ApprovedSha $env:STAGEA_APPROVED_SHA ` "
        "-LocalWorkRoot $localWorkRoot ` -SummaryJsonPath 'stage-a-export/stage-a-summary.json' ` "
        "-SummaryMarkdownPath 'stage-a-export/stage-a-summary.md'"
    )
    test_case.assertEqual(
        [command for command in ast_commands(hosted_steps[4]) if command.startswith("& ")],
        [expected_stage0],
    )
    test_case.assertEqual(
        [command for command in ast_commands(hosted_steps[6]) if command.startswith("& ")],
        [expected_hosted],
    )
    test_case.assertEqual(
        [command for command in ast_commands(runner_steps[2]) if command.startswith("& ")],
        [expected_context],
    )
    test_case.assertEqual(
        [command for command in ast_commands(runner_steps[4]) if command.startswith("& ")],
        [expected_runner_validation],
    )
    test_case.assertEqual(
        [command for command in ast_commands(runner_steps[6]) if command.startswith("& ")],
        [expected_runner],
    )
    test_case.assertEqual(
        [
            command
            for command in ast_commands(runner_steps[7])
            if command.startswith("Assert-ExactNormalFile ")
        ],
        [
            "Assert-ExactNormalFile 'stage-a-export/stage-a-summary.json' "
            "$utf8.GetBytes($expectedJson)",
            "Assert-ExactNormalFile 'stage-a-export/stage-a-summary.md' "
            "$utf8.GetBytes($expectedMarkdown)",
        ],
    )


def _assert_one_run_dependency_state(test_case, document, validator_text, runner_text):
    runner_steps = _steps(document, "deterministic-runner")
    context_step = next(
        step
        for step in runner_steps
        if step.get("name") == "Validate runner context before evaluated checkout"
    )
    setup_step = next(
        step
        for step in runner_steps
        if step.get("name") == "Set up .NET from the evaluated source"
    )
    execution_step = next(
        step
        for step in runner_steps
        if step.get("name") == "Run authorised deterministic validation"
    )
    context_text = str(context_step.get("run", ""))
    execution_text = str(execution_step.get("run", ""))
    residue_text = str(runner_steps[-1].get("run", ""))
    test_case.assertIn("-Phase RunnerContext", context_text)
    test_case.assertIn("-DependencyEnvironmentPath $env:GITHUB_ENV", context_text)
    test_case.assertIn(
        "if (Test-Path -LiteralPath $phaseRoot) { throw $stateFailure }",
        validator_text,
    )
    test_case.assertIn("[System.IO.FileAttributes]::ReparsePoint", validator_text)
    test_case.assertIn("[System.IO.DriveType]::Fixed", validator_text)
    test_case.assertIn(
        "Write-Utf8NoBomFile -Target $environmentTarget -Content $environmentContent",
        validator_text,
    )
    test_case.assertIn("$env:GITHUB_ENV", context_text)
    for variable, child in ONE_RUN_DEPENDENCY_DIRECTORIES.items():
        test_case.assertIn(f"{variable} = '{child}'", validator_text)
        test_case.assertIn(f"{variable} = '{child}'", residue_text)
        test_case.assertIn(variable, execution_text)
        test_case.assertIn(variable, runner_text)
    for name, value in {
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_NOLOGO": "1",
        "DOTNET_MULTILEVEL_LOOKUP": "0",
        "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "0",
    }.items():
        test_case.assertIn(f"{name}={value}", validator_text)
        test_case.assertIn(name, execution_text)
        test_case.assertIn(name, runner_text)
    test_case.assertEqual(
        setup_step.get("uses"),
        "actions/setup-dotnet@d4c94342e560b34958eacfc5d055d21461ed1c5d",
    )
    test_case.assertEqual(
        setup_step.get("with"), {"global-json-file": "evaluated/global.json"}
    )
    test_case.assertLess(runner_steps.index(context_step), runner_steps.index(setup_step))
    test_case.assertLess(runner_steps.index(setup_step), runner_steps.index(execution_step))
    test_case.assertIn(
        "$localWorkRoot = Resolve-NormalFixedDirectory (Join-Path $runnerTemp 'hardware-inspection-stage-a')",
        execution_text,
    )
    test_case.assertNotIn(
        "New-Item -ItemType Directory -Path $localWorkRoot", execution_text
    )
    test_case.assertIn(
        "[System.IO.Path]::GetDirectoryName($dotnetApplication) -ieq $dependencyDirectories['DOTNET_INSTALL_DIR']",
        runner_text,
    )


def _run_inline_powershell(body, prelude="", environment=None, cwd=None, timeout=30):
    command = prelude + "\n& {\n" + body + "\n}"
    return subprocess.run(
        [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command", command],
        text=True,
        capture_output=True,
        timeout=timeout,
        check=False,
        env=environment,
        cwd=cwd,
    )


def _invoke(script, arguments, environment=None):
    command = [
        _powershell_executable(),
        "-NoProfile",
        "-NonInteractive",
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
        f'<ResultSummary outcome="Completed"><Counters {counters} /></ResultSummary></TestRun>'
    )


def _trx_direct_element_counts(text):
    root = ET.fromstring(text)
    containers = {
        element.tag.rsplit("}", 1)[-1]: element
        for element in root
        if isinstance(element.tag, str)
    }
    return tuple(
        sum(isinstance(child.tag, str) for child in containers[name])
        for name in ("Results", "TestDefinitions", "TestEntries")
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


def _runner_pure_command(body):
    script_literal = str(RUNNER_PATH).replace("'", "''")
    command = (
        "$ErrorActionPreference = 'Stop'\n"
        + ". '"
        + script_literal
        + "' -EvaluatedRoot 'x' -ApprovedSha ('0' * 40) -LocalWorkRoot 'x' -SummaryJsonPath 'x' -SummaryMarkdownPath 'x'\n"
        + body
    )
    return [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command", command]


def _invoke_runner_pure(body, environment=None, timeout=20):
    return subprocess.run(
        _runner_pure_command(body),
        text=True,
        capture_output=True,
        timeout=timeout,
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


def _mixed_git_environment(root, base_environment):
    actual_git = shutil.which("git", path=base_environment.get("PATH"))
    if not actual_git:
        raise AssertionError("Git fixture application is unavailable")
    root.mkdir()
    first_directory = root / "git-first"
    later_directory = root / "git-later"
    first_directory.mkdir()
    later_directory.mkdir()
    marker = root / "approved-git-invocations.txt"
    approved_git = first_directory / "git.cmd"
    approved_git.write_text(
        "@echo off\n"
        + "@echo approved>>\""
        + str(marker)
        + "\"\n"
        + "@\""
        + str(Path(actual_git).resolve())
        + "\" %*\n"
        + "@exit /b %errorlevel%\n",
        encoding="ascii",
        newline="\r\n",
    )
    later_git = later_directory / "git.exe"
    shutil.copy2(actual_git, later_git)
    environment = base_environment.copy()
    environment["PATH"] = (
        str(first_directory)
        + os.pathsep
        + str(later_directory)
        + os.pathsep
        + environment.get("PATH", "")
    )
    return environment, approved_git.resolve(), later_git.resolve(), marker


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
        self.assertEqual(hashlib.sha256(raw).hexdigest(), EXPECTED_WORKFLOW_SHA256)
        digest_mutation = raw.replace(b"name:", b"name: mutated-", 1)
        self.assertNotEqual(
            hashlib.sha256(digest_mutation).hexdigest(), EXPECTED_WORKFLOW_SHA256
        )
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
        _assert_workflow_executable_chain(self, document)
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
        manual_validation = copy.deepcopy(document)
        manual_validation["jobs"]["hosted-preflight"]["steps"][6]["run"] = (
            "# manually validated; the validator call was intentionally omitted"
        )
        no_op_runner = copy.deepcopy(document)
        no_op_runner["jobs"]["deterministic-runner"]["steps"][6]["run"] = (
            "Write-Host 'skipped'"
        )
        extra_contract = copy.deepcopy(document)
        extra_contract["jobs"]["hosted-preflight"]["steps"][3]["run"] += (
            " tests.testing.hardware_inspection.unreviewed_contract"
        )
        no_op_artifact_validation = copy.deepcopy(document)
        no_op_artifact_validation["jobs"]["deterministic-runner"]["steps"][7]["run"] = (
            "# summaries were manually inspected"
        )
        for mutation in (
            manual_validation,
            no_op_runner,
            extra_contract,
            no_op_artifact_validation,
        ):
            with self.subTest(executable_chain=repr(mutation)[:60]):
                with self.assertRaises(AssertionError):
                    _assert_workflow_executable_chain(self, mutation)

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
        self.assertIn(
            "approved_sha",
            json.dumps(document["jobs"]["hosted-preflight"]["outputs"]),
        )
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
        inline_steps = []
        for job_name, step in _all_steps(document):
            if "run" in step:
                inline_steps.append(step)
                with self.subTest(
                    inline_powershell=job_name + ":" + str(step.get("name", ""))
                ):
                    self.assertEqual(step.get("shell"), STAGEA_POWERSHELL_SHELL)
                    _powershell_text_ast(self, str(step.get("run", "")))
        self.assertEqual(len(inline_steps), 11)
        self.assertNotIn("shell: powershell", raw.decode("utf-8"))
        self.assertNotIn("-ExecutionPolicy", raw.decode("utf-8"))
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

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            hostile_marker = root / "hostile-profile-loaded.txt"
            payload_marker = root / "payload-ran.txt"
            hostile_profile = root / "Microsoft.PowerShell_profile.ps1"
            payload = root / "payload.ps1"
            hostile_profile.write_text(
                "[IO.File]::WriteAllText('"
                + str(hostile_marker).replace("'", "''")
                + "','hostile-profile-loaded')\n",
                encoding="utf-8",
                newline="\n",
            )
            payload.write_text(
                "[IO.File]::WriteAllText('"
                + str(payload_marker).replace("'", "''")
                + "','payload-ran')\n",
                encoding="utf-8",
                newline="\n",
            )
            wrapper = (
                "$ErrorActionPreference = 'Stop'; $global:LASTEXITCODE = 0; & '"
                + str(payload).replace("'", "''")
                + "'; if (-not $?) { if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; "
                "exit 1 }; exit $LASTEXITCODE"
            )
            shell_result = subprocess.run(
                [
                    r"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    wrapper,
                ],
                text=True,
                capture_output=True,
                timeout=20,
                check=False,
            )
            self.assertEqual(shell_result.returncode, 0, shell_result.stderr)
            self.assertTrue(payload_marker.is_file())
            self.assertFalse(hostile_marker.exists())
            control = subprocess.run(
                [
                    _powershell_executable(),
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    ". '"
                    + str(hostile_profile).replace("'", "''")
                    + "'",
                ],
                text=True,
                capture_output=True,
                timeout=20,
                check=False,
            )
            self.assertEqual(control.returncode, 0, control.stderr)
            self.assertTrue(hostile_marker.is_file())

            native_failure = root / "native-failure.ps1"
            native_failure.write_text(
                '& $env:ComSpec /d /c "exit 23"\n', encoding="utf-8", newline="\n"
            )
            native_wrapper = wrapper.replace(
                str(payload).replace("'", "''"),
                str(native_failure).replace("'", "''"),
                1,
            )
            native_result = subprocess.run(
                [
                    r"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    native_wrapper,
                ],
                text=True,
                capture_output=True,
                timeout=20,
                check=False,
            )
            self.assertEqual(native_result.returncode, 23)

            powershell_failure = root / "powershell-failure.ps1"
            powershell_failure.write_text(
                "Write-Error 'private-canary'\n", encoding="utf-8", newline="\n"
            )
            powershell_wrapper = wrapper.replace(
                str(payload).replace("'", "''"),
                str(powershell_failure).replace("'", "''"),
                1,
            )
            powershell_result = subprocess.run(
                [
                    r"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    powershell_wrapper,
                ],
                text=True,
                capture_output=True,
                timeout=20,
                check=False,
            )
            self.assertNotEqual(powershell_result.returncode, 0)
        for mutation in (
            raw.replace(b"actions/setup-dotnet@d4c94342e560b34958eacfc5d055d21461ed1c5d", b"actions/setup-dotnet@v4", 1),
            raw.replace(b"persist-credentials: false", b"persist-credentials: true", 1),
            raw.replace(b"-NoProfile", b"-NoProfileRemoved", 1),
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
                    self.assertTrue(
                        all(
                            step.get("shell") == STAGEA_POWERSHELL_SHELL
                            for _, step in _all_steps(mutated)
                            if "run" in step
                        )
                    )

    def test_stage_a_workflow_executes_only_the_two_deterministic_categories(self):
        raw, document = _workflow(self)
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        validator_text = _strict_utf8(VALIDATOR_PATH)
        _assert_no_forbidden_runner_commands(self, commands)
        _assert_one_run_dependency_state(
            self, document, validator_text, runner_text
        )
        dependency_mutations = (
            ("DOTNET_INSTALL_DIR = 'sdk'", "DOTNET_INSTALL_DIR = 'C:\\Program Files\\dotnet'"),
            ("DOTNET_CLI_HOME = 'cli-home'", "DOTNET_CLI_HOME_MISSING = 'cli-home'"),
            ("NUGET_PACKAGES = 'nuget-packages'", "NUGET_PACKAGES_MISSING = 'nuget-packages'"),
            ("NUGET_HTTP_CACHE_PATH = 'nuget-http-cache'", "NUGET_HTTP_CACHE_PATH = '..\\outside'"),
            ("if (Test-Path -LiteralPath $phaseRoot) { throw $stateFailure }", "if ($false) { throw $stateFailure }"),
            ("[System.IO.FileAttributes]::ReparsePoint", "[System.IO.FileAttributes]::Normal"),
        )
        for original, replacement in dependency_mutations:
            mutated_validator = validator_text.replace(original, replacement)
            self.assertNotEqual(mutated_validator, validator_text)
            with self.subTest(dependency_state_mutation=original):
                with self.assertRaises((AssertionError, ValueError)):
                    _assert_one_run_dependency_state(
                        self, document, mutated_validator, runner_text
                    )
        mutated_document = copy.deepcopy(document)
        context_step = next(
            step
            for step in _steps(mutated_document, "deterministic-runner")
            if step.get("name") == "Validate runner context before evaluated checkout"
        )
        context_step["run"] = str(context_step["run"]).replace(
            "$env:GITHUB_ENV", "$env:GITHUB_OUTPUT", 1
        )
        with self.assertRaises(AssertionError):
            _assert_one_run_dependency_state(
                self, mutated_document, validator_text, runner_text
            )
        command_text = re.sub(r"\s+", " ", "\n".join(strings))
        ast_text = "\n".join(commands + strings)
        self.assertEqual(command_text.count("TestCategory=Deterministic"), 1)
        self.assertEqual(command_text.count("TestCategory=Task8Deterministic"), 1)
        self.assertEqual(
            len(re.findall(r"(?<![A-Za-z-])--report-trx(?![A-Za-z-])", command_text)),
            2,
        )
        self.assertEqual(command_text.count("--report-trx-filename"), 2)
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
        run_step = next(
            step
            for step in _steps(document, "deterministic-runner")
            if step.get("name") == "Run authorised deterministic validation"
        )
        run_commands, _ = _powershell_text_ast(self, str(run_step["run"]))
        normalized_run_commands = [_normalized_powershell(command) for command in run_commands]
        first_creation = next(
            index
            for index, command in enumerate(normalized_run_commands)
            if command.startswith("New-Item -ItemType Directory")
        )
        self.assertLess(
            normalized_run_commands.index("Resolve-NormalFixedDirectory $env:GITHUB_WORKSPACE"),
            first_creation,
        )
        self.assertLess(
            normalized_run_commands.index("Resolve-NormalFixedDirectory $env:RUNNER_TEMP"),
            first_creation,
        )
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            runner_temp = root / "runner-temp"
            runner_temp.mkdir()
            dependency_root = runner_temp / "hardware-inspection-stage-a"
            dependency_root.mkdir()
            environment = os.environ.copy()
            environment["RUNNER_TEMP"] = str(runner_temp)
            environment["GITHUB_WORKSPACE"] = str(root)
            environment["STAGEA_APPROVED_SHA"] = "a" * 40
            for variable, child in ONE_RUN_DEPENDENCY_DIRECTORIES.items():
                child_path = dependency_root / child
                child_path.mkdir()
                environment[variable] = str(child_path)
            environment.update(
                {
                    "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
                    "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
                    "DOTNET_NOLOGO": "1",
                    "DOTNET_MULTILEVEL_LOOKUP": "0",
                    "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "0",
                }
            )
            for unsafe_runner_temp in (
                r"\\localhost\stage-a-canary",
                r"\\?\C:\stage-a-canary",
                r"\\.\C:\stage-a-canary",
            ):
                unsafe_environment = environment.copy()
                unsafe_environment["RUNNER_TEMP"] = unsafe_runner_temp
                result = _run_inline_powershell(
                    run_step["run"], environment=unsafe_environment, cwd=root
                )
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(
                    result.stderr.replace("\r\n", "\n"),
                    "HI-RUNNER-STAGEA-EXECUTION-INVALID: deterministic execution failed.\n",
                )
                self.assertFalse((root / "stage-a-export").exists())
            runner_temp_target = root / "runner-temp-target"
            runner_temp_target.mkdir()
            runner_temp_reparse = root / "runner-temp-reparse"
            try:
                runner_temp_reparse.symlink_to(
                    runner_temp_target, target_is_directory=True
                )
            except OSError:
                pass
            else:
                reparse_environment = environment.copy()
                reparse_environment["RUNNER_TEMP"] = str(runner_temp_reparse)
                result = _run_inline_powershell(
                    run_step["run"], environment=reparse_environment, cwd=root
                )
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(
                    result.stderr.replace("\r\n", "\n"),
                    "HI-RUNNER-STAGEA-EXECUTION-INVALID: deterministic execution failed.\n",
                )
                self.assertFalse((root / "stage-a-export").exists())
            create_failure = str(run_step["run"]).replace(
                "New-Item -ItemType Directory -Path $exportDirectory -ErrorAction Stop | Out-Null",
                "throw 'C:\\Users\\private-canary\\create-failure'",
                1,
            )
            self.assertNotEqual(create_failure, run_step["run"])
            result = _run_inline_powershell(
                create_failure, environment=environment, cwd=root
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(
                result.stderr.replace("\r\n", "\n"),
                "HI-RUNNER-STAGEA-EXECUTION-INVALID: deterministic execution failed.\n",
            )
            self.assertNotIn("private-canary", result.stdout + result.stderr)

            stale_export_root = root / "stage-a-export"
            stale_export_root.mkdir()
            result = _run_inline_powershell(
                run_step["run"], environment=environment, cwd=root
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(
                result.stderr.replace("\r\n", "\n"),
                "HI-RUNNER-STAGEA-EXECUTION-INVALID: deterministic execution failed.\n",
            )
            stale_export_root.rmdir()
            missing_dependency = dependency_root / "nuget-scratch"
            missing_dependency.rmdir()
            result = _run_inline_powershell(
                run_step["run"], environment=environment, cwd=root
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(
                result.stderr.replace("\r\n", "\n"),
                "HI-RUNNER-STAGEA-EXECUTION-INVALID: deterministic execution failed.\n",
            )

    def test_stage_a_workflow_has_no_candidate_capture_offline_or_adapter_path(self):
        raw, document = _workflow(self)
        runner_text, commands, strings = _powershell_ast_text(self, RUNNER_PATH)
        workflow_run_text = "\n".join(
            str(step.get("run", "")) for _, step in _all_steps(document)
        )
        executable_text = workflow_run_text.casefold()
        for identity in TASK8_IDENTITIES:
            executable_text = executable_text.replace(identity.casefold(), "")
        allowed_absence_check = "third-party\\bin\\llmfit\\v1.1.9\\win-x64"
        self.assertEqual(runner_text.casefold().count(allowed_absence_check), 1)
        for forbidden in (
            "candidate",
            "capture",
            "report generator",
            "report publication",
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
                    _assert_no_forbidden_runner_commands(self, mutated_commands)
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
        runner_steps = _steps(document, "deterministic-runner")
        hosted_steps = _steps(document, "hosted-preflight")
        hosted_debug_step = hosted_steps[0]
        first_step = runner_steps[0]
        final_step = runner_steps[-1]
        self.assertEqual(hosted_debug_step.get("timeout-minutes"), 2)
        self.assertEqual(first_step.get("timeout-minutes"), 2)
        self.assertEqual(final_step.get("timeout-minutes"), 2)
        self.assertEqual(final_step.get("if"), "${{ always() }}")

        def residue_result(step, mode):
            debug_name = mode.split(":", 1)[1] if mode.startswith("debug:") else None
            action_cache_name = (
                mode.split(":", 1)[1]
                if mode.startswith("action-cache:")
                else None
            )
            if mode == "process":
                process_body = "@([pscustomobject]@{ ProcessName = 'llmfit-private-canary' })"
                connection_body = "@()"
            elif mode == "listener":
                process_body = "@()"
                connection_body = "@([pscustomobject]@{ State = 'Listen'; LocalPort = 8787 })"
            elif mode == "query-failure":
                process_body = "@()"
                connection_body = "throw 'C:\\Users\\private-canary\\query-failure'"
            else:
                process_body = "@()"
                connection_body = "@()"
            prelude = (
                "function Get-Process { [CmdletBinding()] param() "
                + process_body
                + " }\nfunction Get-NetTCPConnection { [CmdletBinding()] param() "
                + connection_body
                + " }"
            )
            environment = os.environ.copy()
            for name in list(environment):
                if name.upper().startswith("GIT_"):
                    environment.pop(name, None)
            for name in (
                "ACTIONS_STEP_DEBUG",
                "ACTIONS_RUNNER_DEBUG",
                "RUNNER_DEBUG",
                "STAGEA_RUNNER_DEBUG",
                "ACTIONS_RUNNER_ACTION_ARCHIVE_CACHE",
                "ACTIONS_RUNNER_SYMLINK_CACHED_ACTIONS",
            ):
                environment.pop(name, None)
            if debug_name is not None:
                environment[debug_name] = "false"
            if action_cache_name is not None:
                environment[action_cache_name] = "private-canary"
            with tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                runner_temp = root / "runner-temp"
                runner_workspace = root / "runner-workspace"
                runner_temp.mkdir()
                runner_workspace.mkdir()
                git_probe_marker = root / "git-command-probed.txt"
                if mode.startswith("git:"):
                    git_mode = mode.split(":", 1)[1]
                    git_stub = root / "git.cmd"
                    second_git_stub = root / "git.exe"
                    if git_mode in ("old", "multiple-first-invalid"):
                        version_output = "git version 2.27.99"
                    elif git_mode == "malformed":
                        version_output = "git version 2.51\nprivate-canary"
                    else:
                        version_output = "git version 2.51.0.windows.2"
                    git_stub.write_text(
                        "@echo off\n"
                        + "\n".join("@echo " + line for line in version_output.splitlines())
                        + "\n@exit /b 0\n",
                        encoding="ascii",
                        newline="\r\n",
                    )
                    second_version = (
                        "git version 2.27.99"
                        if git_mode == "multiple-first-valid"
                        else "git version 2.51.0"
                    )
                    second_git_stub.write_text(
                        "@echo off\n@echo " + second_version + "\n@exit /b 0\n",
                        encoding="ascii",
                        newline="\r\n",
                    )
                    if git_mode == "missing":
                        git_outputs = "return @()"
                    elif git_mode in (
                        "multiple-first-valid",
                        "multiple-first-invalid",
                    ):
                        git_outputs = (
                            "[pscustomobject]@{ Source = '"
                            + str(git_stub).replace("'", "''")
                            + "' }; [pscustomobject]@{ Source = '"
                            + str(second_git_stub).replace("'", "''")
                            + "' }"
                        )
                    else:
                        git_outputs = (
                            "[pscustomobject]@{ Source = '"
                            + str(git_stub).replace("'", "''")
                            + "' }"
                        )
                    prelude += (
                        "\nfunction Get-Command { [CmdletBinding()] param("
                        "[Parameter(Position=0)][string]$Name, [object]$CommandType, [switch]$All) "
                        "if ($Name -cne 'git') { throw 'private-canary-unexpected-command' }; "
                        + git_outputs
                        + " }"
                    )
                elif mode == "git-env":
                    environment["gIt_DiR"] = "private-canary"
                    prelude += (
                        "\nfunction Get-Command { [CmdletBinding()] param("
                        "[Parameter(Position=0)][string]$Name, [object]$CommandType, [switch]$All) "
                        "[IO.File]::WriteAllText('"
                        + str(git_probe_marker).replace("'", "''")
                        + "','called'); throw 'private-canary-git-probed' }"
                    )
                if mode == "workspace-unc":
                    workspace_value = r"\\localhost\stage-a-canary"
                elif mode == "workspace-device":
                    workspace_value = r"\\?\C:\stage-a-canary"
                else:
                    workspace_value = str(root)
                if mode == "stale-control-hooks":
                    stale = root / "control" / ".git" / "hooks"
                    stale.mkdir(parents=True)
                    (stale / "pre-commit").write_text(
                        "private-canary\n", encoding="utf-8", newline="\n"
                    )
                elif mode == "stale-evaluated-config":
                    stale = root / "evaluated" / ".git"
                    stale.mkdir(parents=True)
                    (stale / "config").write_text(
                        "private-canary\n", encoding="utf-8", newline="\n"
                    )
                environment.update(
                    {
                        "GITHUB_WORKSPACE": workspace_value,
                        "RUNNER_TEMP": str(runner_temp),
                        "RUNNER_WORKSPACE": str(runner_workspace),
                        "USERNAME": "stage-a-test",
                    }
                )
                result = _run_inline_powershell(
                    step["run"], prelude, environment=environment, cwd=root
                )
                result.git_probe_marker_exists = git_probe_marker.exists()
                return result

        for step, failure in (
            (first_step, "HI-RUNNER-STAGEA-ENVIRONMENT-INVALID: runner environment is invalid.\n"),
            (final_step, "HI-RUNNER-STAGEA-RESIDUE-INVALID: fixed residue check failed.\n"),
        ):
            clean = residue_result(step, "clean")
            self.assertEqual(clean.returncode, 0, clean.stderr)
            for mode in ("process", "listener", "query-failure"):
                with self.subTest(step=step.get("name"), residue=mode):
                    result = residue_result(step, mode)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(result.stderr.replace("\r\n", "\n"), failure)
                    self.assertNotIn("private-canary", result.stdout + result.stderr)
        clean_hosted_debug = residue_result(hosted_debug_step, "clean")
        self.assertEqual(clean_hosted_debug.returncode, 0, clean_hosted_debug.stderr)
        self.assertEqual(clean_hosted_debug.stdout, "")
        self.assertNotIn("eligible", str(hosted_debug_step.get("run", "")).casefold())
        for step in (hosted_debug_step, first_step):
            run_text = str(step.get("run", ""))
            self.assertIn(
                "Get-Command git -CommandType Application -All -ErrorAction SilentlyContinue | Select-Object -First 1",
                run_text,
            )
            self.assertIn("StartsWith('GIT_'", run_text)
            self.assertIn("git version (?<major>", run_text)
            self.assertIn("GITHUB_WORKSPACE", run_text)
            self.assertIn("'control'", run_text)
            self.assertIn("'evaluated'", run_text)
            for mode in (
                "git:missing",
                "git:old",
                "git:malformed",
                "git:multiple-first-invalid",
                "git-env",
            ):
                with self.subTest(step=step.get("name"), git_precheck=mode):
                    result = residue_result(step, mode)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(result.stdout, "")
                    self.assertEqual(
                        result.stderr.replace("\r\n", "\n"), GIT_INVALID_STDERR
                    )
                    self.assertNotIn("private-canary", result.stdout + result.stderr)
                    if mode == "git-env":
                        self.assertFalse(result.git_probe_marker_exists)
            ordered_result = residue_result(step, "git:multiple-first-valid")
            self.assertEqual(ordered_result.returncode, 0, ordered_result.stderr)
            self.assertNotIn(
                "private-canary", ordered_result.stdout + ordered_result.stderr
            )
            for mode in (
                "workspace-unc",
                "workspace-device",
                "stale-control-hooks",
                "stale-evaluated-config",
            ):
                with self.subTest(step=step.get("name"), checkout_freshness=mode):
                    result = residue_result(step, mode)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(result.stdout, "")
                    self.assertEqual(
                        result.stderr.replace("\r\n", "\n"),
                        "HI-RUNNER-STAGEA-ENVIRONMENT-INVALID: runner environment is invalid.\n",
                    )
                    self.assertNotIn("private-canary", result.stdout + result.stderr)
        for debug_name in (
            "ACTIONS_STEP_DEBUG",
            "ACTIONS_RUNNER_DEBUG",
            "RUNNER_DEBUG",
            "STAGEA_RUNNER_DEBUG",
        ):
            for step, failure in (
                (hosted_debug_step, "HI-RUNNER-STAGEA-DEBUG-INVALID: workflow debug logging is prohibited.\n"),
                (first_step, "HI-RUNNER-STAGEA-ENVIRONMENT-INVALID: runner environment is invalid.\n"),
            ):
                with self.subTest(step=step.get("name"), debug=debug_name):
                    result = residue_result(step, "debug:" + debug_name)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(result.stdout, "")
                    self.assertEqual(result.stderr.replace("\r\n", "\n"), failure)
        for action_cache_name in (
            "ACTIONS_RUNNER_ACTION_ARCHIVE_CACHE",
            "ACTIONS_RUNNER_SYMLINK_CACHED_ACTIONS",
        ):
            for step, failure in (
                (hosted_debug_step, "HI-RUNNER-STAGEA-DEBUG-INVALID: workflow debug logging is prohibited.\n"),
                (first_step, "HI-RUNNER-STAGEA-ENVIRONMENT-INVALID: runner environment is invalid.\n"),
            ):
                with self.subTest(
                    step=step.get("name"), action_cache_override=action_cache_name
                ):
                    self.assertIn(action_cache_name, str(step.get("run", "")))
                    result = residue_result(
                        step, "action-cache:" + action_cache_name
                    )
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(result.stdout, "")
                    self.assertEqual(result.stderr.replace("\r\n", "\n"), failure)
                    self.assertNotIn(
                        "private-canary", result.stdout + result.stderr
                    )
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
        self.assertGreaterEqual(validator_text.count("[System.IO.DriveType]::Fixed"), 2)
        self.assertIn("[System.IO.FileAttributes]::ReparsePoint", validator_text)
        self.assertIn("$normalEvaluatedParent -ine $controlParentPath", validator_text)
        self.assertIn("Test-Path -LiteralPath $anticipatedEvaluatedRoot", validator_text)
        self.assertIn(
            "Get-Command git -CommandType Application -All -ErrorAction SilentlyContinue | Select-Object -First 1",
            validator_text,
        )
        self.assertIn("git version (?<major>", validator_text)
        self.assertNotIn("Get-Command git.exe", validator_text)
        for mutation, required in (
            (
                validator_text.replace("[System.IO.DriveType]::Fixed", "[System.IO.DriveType]::Network"),
                "[System.IO.DriveType]::Fixed",
            ),
            (
                validator_text.replace("[System.IO.FileAttributes]::ReparsePoint", "[System.IO.FileAttributes]::Normal"),
                "[System.IO.FileAttributes]::ReparsePoint",
            ),
        ):
            with self.assertRaises(AssertionError):
                self.assertIn(required, mutation)
        for parameter in (
            "Phase", "ControlRoot", "WorkflowRef", "DefaultBranch", "Actor",
            "TriggeringActor", "RepositoryOwner", "RunAttempt", "Confirmation",
            "RunnerLabel", "SourceCheckoutRoot", "EvaluatedRoot", "ApprovedSha",
            "GitHubOutputPath", "RunnerTemp", "RunnerWorkspace",
            "DependencyEnvironmentPath",
        ):
            self.assertRegex(validator_text, rf"\$\(?{parameter}\)?")
        self.assertNotRegex(validator_text, r"\$\(?SummaryPath\)?")
        with tempfile.TemporaryDirectory() as line_ending_directory:
            line_ending_root = Path(line_ending_directory)
            crlf_script = line_ending_root / "fresh-checkout.ps1"
            crlf_script.write_bytes(b"$value = 1\r\n$value | Out-Null\r\n")
            self.assertEqual(
                _strict_utf8(crlf_script), "$value = 1\n$value | Out-Null\n"
            )
            _powershell_ast(self, crlf_script)
            for raw in (
                b"$value = 1\r\n$value | Out-Null\n",
                b"$value = 1\r$value | Out-Null\r",
                codecs.BOM_UTF8 + b"$value = 1\r\n",
            ):
                crlf_script.write_bytes(raw)
                with self.assertRaises(AssertionError):
                    _strict_utf8(crlf_script)
            for source in (VALIDATOR_PATH, RUNNER_PATH):
                normalized = _strict_utf8(source)
                fresh_checkout_script = line_ending_root / source.name
                fresh_checkout_script.write_bytes(
                    normalized.replace("\n", "\r\n").encode("utf-8")
                )
                self.assertEqual(_strict_utf8(fresh_checkout_script), normalized)
                _powershell_ast(self, fresh_checkout_script)
        for arguments in ({}, {"Phase": ""}):
            with self.subTest(validator_binding=arguments):
                result = _invoke(VALIDATOR_PATH, arguments, _validator_environment())
                _assert_invalid_validator_result(self, result)
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
            runner_literal = str(RUNNER_PATH).replace("'", "''")
            cleanup_marker = fixture_root / "add-type-cleanup.txt"
            cleanup_literal = str(cleanup_marker).replace("'", "''")
            add_type_failure = subprocess.run(
                [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command",
                 "function Add-Type { throw 'C:\\Users\\canary\\compiler-failure' }\n"
                 "function Get-Process { [IO.File]::WriteAllText('" + cleanup_literal + "','attempted'); @() }\n"
                 "function Get-NetTCPConnection { @() }\n"
                 "& '" + runner_literal + "' -EvaluatedRoot 'x' -ApprovedSha ('a' * 40) -LocalWorkRoot 'x' -SummaryJsonPath 'x' -SummaryMarkdownPath 'x'"],
                text=True, capture_output=True, timeout=20, check=False,
            )
            self.assertNotEqual(add_type_failure.returncode, 0)
            self.assertEqual(add_type_failure.stdout, "")
            self.assertEqual(add_type_failure.stderr.replace("\r\n", "\n"), INVALID_RUNNER_STDERR)
            self.assertNotIn("canary", add_type_failure.stderr)
            self.assertEqual(cleanup_marker.read_text(encoding="utf-8"), "attempted")

            runner_text = _strict_utf8(RUNNER_PATH)
            collection_marker = fixture_root / "collection-init-cleanup.txt"
            collection_literal = str(collection_marker).replace("'", "''")
            collection_runner = fixture_root / "mutated-collection-runner.ps1"
            collection_mutation = runner_text.replace(
                "$script:StageAOwnedProcesses = New-Object System.Collections.ArrayList",
                "throw 'C:\\Users\\canary\\collection-init-failure'",
                1,
            )
            self.assertNotEqual(collection_mutation, runner_text)
            collection_runner.write_text(
                collection_mutation, encoding="utf-8", newline="\n"
            )
            collection_literal_runner = str(collection_runner).replace("'", "''")
            collection_failure = subprocess.run(
                [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command",
                 "function Get-Process { [IO.File]::WriteAllText('" + collection_literal + "','attempted'); @() }\n"
                 "function Get-NetTCPConnection { @() }\n"
                 "& '" + collection_literal_runner + "' -EvaluatedRoot 'x' -ApprovedSha ('a' * 40) -LocalWorkRoot 'x' -SummaryJsonPath 'x' -SummaryMarkdownPath 'x'"],
                text=True, capture_output=True, timeout=20, check=False,
            )
            self.assertNotEqual(collection_failure.returncode, 0)
            self.assertEqual(collection_failure.stdout, "")
            self.assertEqual(
                collection_failure.stderr.replace("\r\n", "\n"),
                INVALID_RUNNER_STDERR,
            )
            self.assertNotIn("canary", collection_failure.stderr)
            self.assertEqual(collection_marker.read_text(encoding="utf-8"), "attempted")

            unregister_marker = fixture_root / "unregister-cleanup.txt"
            unregister_literal = str(unregister_marker).replace("'", "''")
            mutated_runner = fixture_root / "mutated-unregister-runner.ps1"
            unregister_mutation = runner_text.replace(
                "[HardwareInspection.StageA.CancellationState]::Remove()",
                "throw 'C:\\Users\\canary\\unregister-failure'",
                1,
            )
            self.assertNotEqual(unregister_mutation, runner_text)
            mutated_runner.write_text(unregister_mutation, encoding="utf-8", newline="\n")
            mutated_literal = str(mutated_runner).replace("'", "''")
            unregister_failure = subprocess.run(
                [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command",
                 "function Get-Process { [IO.File]::WriteAllText('" + unregister_literal + "','attempted'); @() }\n"
                 "function Get-NetTCPConnection { @() }\n"
                 "& '" + mutated_literal + "' -EvaluatedRoot 'x' -ApprovedSha ('a' * 40) -LocalWorkRoot 'x' -SummaryJsonPath 'x' -SummaryMarkdownPath 'x'"],
                text=True, capture_output=True, timeout=20, check=False,
            )
            self.assertNotEqual(unregister_failure.returncode, 0)
            self.assertEqual(unregister_failure.stdout, "")
            self.assertEqual(
                unregister_failure.stderr.replace("\r\n", "\n"),
                INVALID_RUNNER_STDERR,
            )
            self.assertNotIn("canary", unregister_failure.stderr)
            self.assertEqual(unregister_marker.read_text(encoding="utf-8"), "attempted")

            install_marker = fixture_root / "install-cleanup.txt"
            install_literal = str(install_marker).replace("'", "''")
            install_runner = fixture_root / "mutated-install-runner.ps1"
            install_mutation = runner_text.replace(
                "[HardwareInspection.StageA.CancellationState]::Install()",
                "throw 'C:\\Users\\canary\\install-failure'",
                1,
            )
            self.assertNotEqual(install_mutation, runner_text)
            install_runner.write_text(install_mutation, encoding="utf-8", newline="\n")
            install_runner_literal = str(install_runner).replace("'", "''")
            install_failure = subprocess.run(
                [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command",
                 "function Get-Process { [IO.File]::WriteAllText('" + install_literal + "','attempted'); @() }\n"
                 "function Get-NetTCPConnection { @() }\n"
                 "& '" + install_runner_literal + "' -EvaluatedRoot 'x' -ApprovedSha ('a' * 40) -LocalWorkRoot 'x' -SummaryJsonPath 'x' -SummaryMarkdownPath 'x'"],
                text=True, capture_output=True, timeout=20, check=False,
            )
            self.assertNotEqual(install_failure.returncode, 0)
            self.assertEqual(install_failure.stdout, "")
            self.assertEqual(
                install_failure.stderr.replace("\r\n", "\n"),
                INVALID_RUNNER_STDERR,
            )
            self.assertNotIn("canary", install_failure.stderr)
            self.assertEqual(install_marker.read_text(encoding="utf-8"), "attempted")

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
            expected_hosted_output = hosted_output.read_bytes()
            mixed_git_environment, approved_git, later_git, git_marker = (
                _mixed_git_environment(fixture_root / "mixed-validator-git", environment)
            )
            mixed_output = fixture_root / "mixed-git-output.txt"
            mixed_git = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=mixed_output,
                ),
                mixed_git_environment,
            )
            self.assertEqual(mixed_git.returncode, 0, mixed_git.stderr)
            self.assertEqual(mixed_git.stdout, "")
            self.assertEqual(mixed_git.stderr, "")
            self.assertTrue(approved_git.is_file())
            self.assertTrue(later_git.is_file())
            self.assertGreaterEqual(
                len(git_marker.read_text(encoding="ascii").splitlines()), 6
            )
            precreated_empty_output = fixture_root / "precreated-empty-output.txt"
            precreated_empty_output.write_bytes(b"")
            precreated = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=precreated_empty_output,
                ),
                environment,
            )
            self.assertEqual(precreated.returncode, 0, precreated.stderr)
            self.assertEqual(precreated.stdout, "")
            self.assertEqual(precreated.stderr, "")
            self.assertEqual(precreated_empty_output.read_bytes(), expected_hosted_output)
            hosted_output.write_bytes(b"unlocked-nonempty-canary\n")
            unlocked_nonempty = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    SourceCheckoutRoot=checkout_root,
                    GitHubOutputPath=hosted_output,
                ),
                environment,
            )
            _assert_invalid_validator_result(self, unlocked_nonempty)
            self.assertEqual(
                hosted_output.read_bytes(), b"unlocked-nonempty-canary\n"
            )
            locked_output = fixture_root / "locked-empty-output.txt"
            locked_output.write_bytes(b"")
            with _ExclusiveFileLock(locked_output) as locked:
                if locked:
                    entries_before = {path.name for path in fixture_root.iterdir()}
                    locked_result = _invoke(
                        VALIDATOR_PATH,
                        _validator_arguments(
                            control_root,
                            SourceCheckoutRoot=checkout_root,
                            GitHubOutputPath=locked_output,
                        ),
                        environment,
                    )
            if os.name == "nt":
                self.assertTrue(locked, "exclusive output lock fixture is unavailable")
            if locked:
                _assert_invalid_validator_result(self, locked_result)
                self.assertEqual(locked_output.read_bytes(), b"")
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

            runner_temp = fixture_root / "runner-temp"
            runner_workspace = fixture_root / "runner-workspace"
            runner_temp.mkdir()
            runner_workspace.mkdir()
            anticipated_evaluated = fixture_root / "evaluated"
            wrong_evaluated_parent = fixture_root / "wrong-parent"
            wrong_evaluated_parent.mkdir()
            dependency_environment = fixture_root / "dependency-environment.txt"
            dependency_environment.write_bytes(b"")
            context_arguments = _validator_arguments(
                control_root,
                Phase="RunnerContext",
                ApprovedSha=approved_sha,
                EvaluatedRoot=anticipated_evaluated,
                RunnerTemp=runner_temp,
                RunnerWorkspace=runner_workspace,
                DependencyEnvironmentPath=dependency_environment,
            )
            valid_context = _invoke(
                VALIDATOR_PATH, context_arguments, environment
            )
            self.assertEqual(valid_context.returncode, 0, valid_context.stderr)
            self.assertEqual(valid_context.stdout, "")
            self.assertEqual(valid_context.stderr, "")
            dependency_root = runner_temp / "hardware-inspection-stage-a"
            environment_lines = dependency_environment.read_text(
                encoding="utf-8"
            ).splitlines()
            self.assertEqual(len(environment_lines), 11)
            for variable, child in ONE_RUN_DEPENDENCY_DIRECTORIES.items():
                child_path = dependency_root / child
                self.assertTrue(child_path.is_dir())
                self.assertIn(f"{variable}={child_path}", environment_lines)
            for setting in (
                "DOTNET_CLI_TELEMETRY_OPTOUT=1",
                "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1",
                "DOTNET_NOLOGO=1",
                "DOTNET_MULTILEVEL_LOOKUP=0",
                "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0",
            ):
                self.assertIn(setting, environment_lines)
            shutil.rmtree(dependency_root)
            dependency_environment.write_bytes(b"")
            nonfixed_validator = fixture_root / "nonfixed-drive-validator.ps1"
            nonfixed_validator.write_text(
                validator_text.replace(
                    "[System.IO.DriveType]::Fixed",
                    "[System.IO.DriveType]::Network",
                ),
                encoding="utf-8",
                newline="\n",
            )
            simulated_nonfixed = _invoke(
                nonfixed_validator, context_arguments, environment
            )
            _assert_invalid_validator_result(self, simulated_nonfixed)
            for changes in (
                {"ApprovedSha": "b" * 40},
                {"RunnerTemp": r"\\localhost\stage-a-canary"},
                {"RunnerTemp": r"\\?\C:\stage-a-canary"},
                {"RunnerTemp": r"\\.\C:\stage-a-canary"},
                {"RunnerWorkspace": r"\\localhost\stage-a-canary"},
                {"RunnerWorkspace": r"\\?\C:\stage-a-canary"},
                {"RunnerWorkspace": r"\\.\C:\stage-a-canary"},
                {"EvaluatedRoot": r"\\localhost\stage-a-canary"},
                {"EvaluatedRoot": wrong_evaluated_parent / "evaluated"},
            ):
                invalid_context_arguments = dict(context_arguments)
                invalid_context_arguments.update(changes)
                result = _invoke(
                    VALIDATOR_PATH, invalid_context_arguments, environment
                )
                _assert_invalid_validator_result(self, result)
            context_git_environment = environment.copy()
            context_git_environment["gIt_DiR"] = "privacy-canary"
            result = _invoke(
                VALIDATOR_PATH, context_arguments, context_git_environment
            )
            _assert_invalid_validator_result(self, result)
            for environment_name in (
                "GRANITE_LLMFIT_CANDIDATE_ROOT",
                "GRANITE_LLMFIT_TRUSTED_OUTPUT",
                "GRANITE_LLMFIT_GATE1_OUTPUT",
                "GRANITE_LLMFIT_WINDOWS_REFERENCE",
                "GRANITE_LLMFIT_OFFLINE_OUTPUT",
                "GRANITE_LLMFIT_FAKE_TOOL_ROOT",
            ):
                context_environment = environment.copy()
                context_environment[environment_name] = "privacy-canary"
                result = _invoke(
                    VALIDATOR_PATH, context_arguments, context_environment
                )
                _assert_invalid_validator_result(self, result)
            reparse_runner_temp = fixture_root / "runner-temp-reparse"
            try:
                reparse_runner_temp.symlink_to(runner_temp, target_is_directory=True)
            except OSError:
                pass
            else:
                reparse_arguments = dict(context_arguments)
                reparse_arguments["RunnerTemp"] = reparse_runner_temp
                result = _invoke(
                    VALIDATOR_PATH, reparse_arguments, environment
                )
                _assert_invalid_validator_result(self, result)
            reparse_runner_workspace = fixture_root / "runner-workspace-reparse"
            try:
                reparse_runner_workspace.symlink_to(
                    runner_workspace, target_is_directory=True
                )
            except OSError:
                pass
            else:
                reparse_arguments = dict(context_arguments)
                reparse_arguments["RunnerWorkspace"] = reparse_runner_workspace
                result = _invoke(
                    VALIDATOR_PATH, reparse_arguments, environment
                )
                _assert_invalid_validator_result(self, result)
            stale_work_root = runner_temp / "hardware-inspection-stage-a"
            stale_work_root.mkdir()
            result = _invoke(
                VALIDATOR_PATH, context_arguments, environment
            )
            _assert_invalid_validator_result(self, result)
            stale_work_root.rmdir()
            anticipated_evaluated.mkdir()
            (anticipated_evaluated / ".git").mkdir()
            (anticipated_evaluated / ".git" / "config").write_text(
                "private-canary\n", encoding="utf-8", newline="\n"
            )
            result = _invoke(
                VALIDATOR_PATH, context_arguments, environment
            )
            _assert_invalid_validator_result(self, result)
            self.assertNotIn("private-canary", result.stdout + result.stderr)
            shutil.rmtree(anticipated_evaluated)

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

            runner = _invoke(
                VALIDATOR_PATH,
                _validator_arguments(
                    control_root,
                    Phase="Runner",
                    ApprovedSha=approved_sha,
                    EvaluatedRoot=checkout_root,
                ),
                environment,
            )
            self.assertEqual(runner.returncode, 0, runner.stderr)
            self.assertEqual(runner.stdout, "")
            self.assertEqual(runner.stderr, "")

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
        self.assertIn(
            "Get-Command git -CommandType Application -All -ErrorAction SilentlyContinue | Select-Object -First 1",
            runner_text,
        )
        self.assertIn("git version (?<major>", runner_text)
        self.assertNotIn("Get-Command git.exe", runner_text)
        self.assertIn(
            "Invoke-StageAGitProcess $application @('--version')",
            runner_text,
        )
        self.assertNotIn("$versionLines = @(& $application --version", runner_text)
        self.assertEqual(
            runner_text.count("Invoke-StageAGitProcess $gitApplication"), 6
        )
        for arguments in (
            {},
            {
                "EvaluatedRoot": "",
                "ApprovedSha": "",
                "LocalWorkRoot": "",
                "SummaryJsonPath": "",
                "SummaryMarkdownPath": "",
            },
        ):
            with self.subTest(runner_binding=arguments):
                result = _invoke(RUNNER_PATH, arguments, os.environ.copy())
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stdout, "")
                self.assertEqual(
                    result.stderr.replace("\r\n", "\n"), INVALID_RUNNER_STDERR
                )
        pre_function_text = runner_text.split("function Initialize-StageACappedDrain", 1)[0]
        self.assertIn("$script:StageAOwnedProcesses = $null", pre_function_text)
        self.assertIn("$script:StageAProcessJob = $null", pre_function_text)
        self.assertNotIn("New-Object System.Collections.ArrayList", pre_function_text)
        for required_runtime in (
            "JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE",
            "SetInformationJobObject",
            "CREATE_SUSPENDED",
            "EXTENDED_STARTUPINFO_PRESENT",
            "PROC_THREAD_ATTRIBUTE_HANDLE_LIST",
            "PROC_THREAD_ATTRIBUTE_JOB_LIST",
            "InitializeProcThreadAttributeList(IntPtr.Zero, 2",
            "UpdateProcThreadAttribute(\n                    attributeList, 0, new IntPtr(PROC_THREAD_ATTRIBUTE_JOB_LIST)",
            "TerminateJobObject",
            "QueryInformationJobObject",
            "Process.GetProcessById",
            "Initialize-StageARuntime",
            "bool terminated = TerminateProcess(created.hProcess, 1)",
            "uint waitResult = WaitForSingleObject(created.hProcess, 5000)",
            "!terminated || waitResult != WAIT_OBJECT_0",
            "throw new AggregateException(primaryFailure, containmentFailure)",
        ):
            self.assertIn(required_runtime, runner_text)
        self.assertNotIn("AssignProcessToJobObject", runner_text)
        self.assertNotIn("job.AssignHandle", runner_text)
        suspended_start = runner_text.index("public static ContainedProcess StartSuspendedAssigned")
        job_attribute = runner_text.index(
            "new IntPtr(PROC_THREAD_ATTRIBUTE_JOB_LIST)", suspended_start
        )
        create_process = runner_text.index("if (!CreateProcess(", job_attribute)
        resume = runner_text.index("ResumeThread(created.hThread)", suspended_start)
        retained_handle = runner_text.index("process.Handle", suspended_start)
        close_stdout_writer = runner_text.index("CloseHandle(stdoutWrite)", suspended_start)
        self.assertLess(job_attribute, create_process)
        self.assertLess(create_process, close_stdout_writer)
        self.assertLess(close_stdout_writer, retained_handle)
        self.assertLess(retained_handle, resume)
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
        self.assertIn("Test-StageADisjointPaths $workRoot $evaluated", runner_text)
        for required_hardening in (
            "Test-StageADisjointPaths",
            "DirectorySeparatorChar",
            "ProcessJob",
            "CancellationState",
            "Console.CancelKeyPress += handler",
            "private const int Active = 0",
            "private const int Cancelled = 1",
            "private const int Completed = 2",
            "eventArgs.Cancel = true",
            "Interlocked.CompareExchange(ref state, Cancelled, Active)",
            "public static bool TryComplete()",
            "[HardwareInspection.StageA.CancellationState]::Install()",
            "[HardwareInspection.StageA.CancellationState]::TryComplete()",
            "[HardwareInspection.StageA.CancellationState]::Remove()",
            "[HardwareInspection.StageA.CancellationState]::IsCancellationRequested",
            "StageAOwnedProcesses",
            "Wait-StageAProcess",
            "StageAMaximumProcessStreamBytes",
            "Copy-StageAProcessStream",
        ):
            self.assertIn(required_hardening, runner_text)
        self.assertNotIn("[System.ConsoleCancelEventHandler]", runner_text)
        self.assertNotIn("$script:StageACancelled", runner_text)
        cancel_handler = runner_text.index("private static void HandleCancel")
        cancel_cas = runner_text.index(
            "Interlocked.CompareExchange(ref state, Cancelled, Active)", cancel_handler
        )
        cancel_acknowledgement = runner_text.index(
            "eventArgs.Cancel = true", cancel_handler
        )
        self.assertLess(cancel_cas, cancel_acknowledgement)
        for first, second, expected_success in (
            ("C:\\stage-a\\source", "C:\\stage-a\\work", True),
            ("C:\\stage-a\\source", "C:\\stage-a\\source\\child", False),
            ("C:\\stage-a\\source\\child", "C:\\stage-a\\source", False),
            ("C:\\stage-a\\source", "C:\\stage-a\\source", False),
        ):
            result = _invoke_runner_pure(
                "Test-StageADisjointPaths '" + first + "' '" + second + "'"
            )
            self.assertEqual(result.returncode == 0, expected_success)
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            target = root / "target"
            target.mkdir()
            reparse = root / "ancestor-reparse"
            try:
                reparse.symlink_to(target, target_is_directory=True)
            except OSError:
                pass
            else:
                result = _invoke_runner_pure(
                    "Test-StageANormalExistingPath '" + str(reparse).replace("'", "''") + "' $true | Out-Null"
                )
                self.assertNotEqual(result.returncode, 0)
        with tempfile.TemporaryDirectory() as temporary_directory:
            checkout = Path(temporary_directory) / "checkout"
            _create_clean_checkout(checkout)
            top_level = _git_output(checkout, "rev-parse", "--show-toplevel")
            git_directory = _git_output(checkout, "rev-parse", "--absolute-git-dir")
            for git_path in (top_level, git_directory):
                result = _invoke_runner_pure(
                    "Test-StageANormalExistingPath '" + git_path.replace("'", "''") + "' $true | Out-Null"
                )
                self.assertEqual(result.returncode, 0, result.stderr)
            log_root = Path(temporary_directory) / "sequential-git"
            log_root.mkdir()
            result = _invoke_runner_pure(
                "Initialize-StageARuntime\n"
                + "try {\n"
                + "$git = Resolve-StageAGitApplication '"
                + str(log_root / "git-version").replace("'", "''")
                + "'\n"
                + "Invoke-StageAProcess $git @('-C','"
                + str(checkout).replace("'", "''")
                + "','rev-parse','--show-toplevel') '"
                + str(log_root / "git-top").replace("'", "''")
                + "' 20 $true $true | Out-Null\n"
                + "Invoke-StageAProcess $git @('-C','"
                + str(checkout).replace("'", "''")
                + "','rev-parse','HEAD') '"
                + str(log_root / "git-head").replace("'", "''")
                + "' 20 $true $true | Out-Null\n} finally { Stop-StageAOwnedProcesses }"
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            mixed_environment, approved_git, later_git, git_marker = (
                _mixed_git_environment(
                    Path(temporary_directory) / "mixed-runner-git",
                    os.environ.copy(),
                )
            )
            mixed_log_root = Path(temporary_directory) / "mixed-git-logs"
            mixed_log_root.mkdir()
            mixed_result = _invoke_runner_pure(
                "Initialize-StageARuntime\n"
                + "try { $git = Resolve-StageAGitApplication '"
                + str(mixed_log_root / "approved-version").replace("'", "''")
                + "'\n"
                + "if ([System.IO.Path]::GetFullPath($git) -ine '"
                + str(approved_git).replace("'", "''")
                + "') { throw 'private-canary-wrong-git' }\n"
                + "Invoke-StageAGitProcess $git @('--version') '"
                + str(mixed_log_root / "git-version").replace("'", "''")
                + "' 20 | Out-Null } finally { Stop-StageAOwnedProcesses }",
                environment=mixed_environment,
            )
            self.assertEqual(mixed_result.returncode, 0, mixed_result.stderr)
            self.assertEqual(mixed_result.stdout, "")
            self.assertEqual(mixed_result.stderr, "")
            self.assertTrue(approved_git.is_file())
            self.assertTrue(later_git.is_file())
            self.assertGreaterEqual(
                len(git_marker.read_text(encoding="ascii").splitlines()), 2
            )
        timeout_fixture = _invoke_runner_pure(
            "$process = New-Object System.Diagnostics.Process\n"
            "$process.StartInfo = New-Object System.Diagnostics.ProcessStartInfo\n"
            "$process.StartInfo.FileName = $env:ComSpec\n"
            "$process.StartInfo.Arguments = '/c ping -n 3 127.0.0.1 > nul'\n"
            "$process.StartInfo.UseShellExecute = $false\n"
            "$null = $process.Start()\n"
            "try { Wait-StageAProcess $process 0 } finally { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() } }"
        )
        self.assertNotEqual(timeout_fixture.returncode, 0)
        with tempfile.TemporaryDirectory() as temporary_directory:
            log_path = Path(temporary_directory) / "high-volume.log"
            body = (
                "Initialize-StageARuntime\n"
                "$tool = (Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                "try { try { Invoke-StageAProcess $tool @('-NoProfile','-NonInteractive','-Command',\"[Console]::Out.Write([string]::new([char]120, 17825792))\") '"
                + str(log_path).replace("'", "''")
                + "' 20 | Out-Null } catch { } } finally { Stop-StageAOwnedProcesses }\n"
                "if (-not (Test-Path '"
                + str(log_path).replace("'", "''")
                + ".stdout')) { exit 2 }\n"
                "if ((Get-Item '"
                + str(log_path).replace("'", "''")
                + ".stdout').Length -gt 16MB) { exit 3 }"
            )
            result = _invoke_runner_pure(body)
            self.assertEqual(result.returncode, 0, result.stderr)
            small_log_path = Path(temporary_directory) / "over-four-kib.log"
            small_body = (
                "Initialize-StageARuntime\n"
                "$tool = (Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                "try { Invoke-StageAProcess $tool @('-NoProfile','-NonInteractive','-Command',\"[Console]::Out.Write([string]::new([char]121, 8192))\") '"
                + str(small_log_path).replace("'", "''")
                + "' 20 | Out-Null } finally { Stop-StageAOwnedProcesses }\n"
                "if ((Get-Item '"
                + str(small_log_path).replace("'", "''")
                + ".stdout').Length -ne 8192) { exit 4 }"
            )
            result = _invoke_runner_pure(small_body)
            self.assertEqual(result.returncode, 0, result.stderr)

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            log_path = root / "environment.log"
            environment = os.environ.copy()
            environment.update(
                {
                    "SAFE_CANARY": "ordinary-safe-value",
                    "GITHUB_STEP_SUMMARY": "command-file-canary",
                    "github_output": "output-canary",
                    "GITHUB_ENV": "environment-canary",
                    "GITHUB_PATH": "path-canary",
                    "Actions_Cache_URL": "actions-canary",
                    "RUNNER_TEMP": "runner-canary",
                    "stagea_private": "stagea-canary",
                    "DOTNET_INSTALL_DIR": "sdk-canary",
                    "DOTNET_CLI_HOME": "cli-canary",
                    "NUGET_PACKAGES": "packages-canary",
                    "NUGET_HTTP_CACHE_PATH": "http-cache-canary",
                    "NUGET_PLUGINS_CACHE_PATH": "plugins-cache-canary",
                    "NUGET_SCRATCH": "scratch-canary",
                }
            )
            child_command = (
                "[Console]::Out.Write('safe=' + $env:SAFE_CANARY + ';github=' + "
                "$env:GITHUB_STEP_SUMMARY + ';output=' + $env:GITHUB_OUTPUT + "
                "';env=' + $env:GITHUB_ENV + ';path=' + $env:GITHUB_PATH + "
                "';actions=' + $env:Actions_Cache_URL + ';runner=' + $env:RUNNER_TEMP + "
                "';stagea=' + $env:stagea_private + ';sdk=' + $env:DOTNET_INSTALL_DIR + "
                "';cli=' + $env:DOTNET_CLI_HOME + ';packages=' + $env:NUGET_PACKAGES + "
                "';http=' + $env:NUGET_HTTP_CACHE_PATH + ';plugins=' + $env:NUGET_PLUGINS_CACHE_PATH + "
                "';scratch=' + $env:NUGET_SCRATCH)"
            )
            body = (
                "Initialize-StageARuntime\n"
                "$tool = (Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                "try { Invoke-StageAProcess $tool @('-NoProfile','-NonInteractive','-Command',"
                + "'"
                + child_command.replace("'", "''")
                + "') '"
                + str(log_path).replace("'", "''")
                + "' 20 | Out-Null } finally { Stop-StageAOwnedProcesses }"
            )
            result = _invoke_runner_pure(body, environment=environment)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(
                (Path(str(log_path) + ".stdout")).read_text(encoding="utf-8"),
                "safe=ordinary-safe-value;github=;output=;env=;path=;actions=;runner=;stagea=;"
                "sdk=sdk-canary;cli=cli-canary;packages=packages-canary;"
                "http=http-cache-canary;plugins=plugins-cache-canary;scratch=scratch-canary",
            )

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            marker = root / "assignment-failure-marker.txt"
            log_path = root / "assignment-failure.log"
            child_script = (
                "[IO.File]::WriteAllText('"
                + str(marker).replace("'", "''")
                + "','evaluated-code-ran')"
            )
            child_encoded = base64.b64encode(
                child_script.encode("utf-16-le")
            ).decode("ascii")
            assignment_failure_runner = root / "assignment-failure-runner.ps1"
            assignment_failure_mutation = runner_text.replace(
                "            Invoke-HardwareInspectionIntelRunnerStageAInternal\n",
                "            $script:StageAProcessJob.Dispose()\n"
                "            $tool = (Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                "            Invoke-StageAProcess $tool @('-NoProfile','-NonInteractive','-EncodedCommand','"
                + child_encoded
                + "') '"
                + str(log_path).replace("'", "''")
                + "' 20 | Out-Null\n",
                1,
            )
            self.assertNotEqual(assignment_failure_mutation, runner_text)
            assignment_failure_runner.write_text(
                assignment_failure_mutation, encoding="utf-8", newline="\n"
            )
            result = _invoke(
                assignment_failure_runner,
                {
                    "EvaluatedRoot": root,
                    "ApprovedSha": "a" * 40,
                    "LocalWorkRoot": root,
                    "SummaryJsonPath": root / "summary.json",
                    "SummaryMarkdownPath": root / "summary.md",
                },
                os.environ.copy(),
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(result.stdout, "")
            self.assertEqual(
                result.stderr.replace("\r\n", "\n"), INVALID_RUNNER_STDERR
            )
            time.sleep(0.1)
            self.assertFalse(marker.exists(), "suspended evaluated code ran before assignment")

        final_test_index = runner_text.index("'task8.log'")
        self.assertLess(
            runner_text.index(
                "Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)",
                final_test_index,
            ),
            runner_text.index("Read-HardwareInspectionIntelRunnerStageATrx", final_test_index),
        )
        final_residue_index = runner_text.index("Test-StageAResidualProcesses", final_test_index)
        self.assertLess(
            runner_text.index(
                "Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)",
                final_residue_index,
            ),
            runner_text.index("$json =", final_residue_index),
        )
        outer_cleanup = runner_text.rindex("Stop-StageAOwnedProcesses; Test-StageAResidualProcesses")
        outer_publication = runner_text.index("Write-StageAAtomicUtf8 $publishJsonPath", outer_cleanup)
        cancel_after_json = runner_text.index(
            "Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)",
            outer_publication,
        )
        markdown_publication = runner_text.index(
            "Write-StageAAtomicUtf8 $publishMarkdownPath", cancel_after_json
        )
        cancel_after_markdown = runner_text.index(
            "Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)",
            markdown_publication,
        )
        completion_boundary = runner_text.index(
            "$completionWon = [HardwareInspection.StageA.CancellationState]::TryComplete()",
            cancel_after_markdown,
        )
        outer_unregister = runner_text.index(
            "[HardwareInspection.StageA.CancellationState]::Remove()", completion_boundary
        )
        final_completion_decision = runner_text.index(
            "-not $completionWon", outer_unregister
        )
        fixed_failure = runner_text.index(
            "WriteLine($script:StageAFailure)", final_completion_decision
        )
        self.assertLess(outer_cleanup, outer_publication)
        self.assertLess(outer_publication, cancel_after_json)
        self.assertLess(cancel_after_json, markdown_publication)
        self.assertLess(markdown_publication, cancel_after_markdown)
        self.assertLess(cancel_after_markdown, completion_boundary)
        self.assertLess(completion_boundary, outer_unregister)
        self.assertLess(outer_unregister, final_completion_decision)
        self.assertLess(final_completion_decision, fixed_failure)

        cancellation_race_type = (
            "    public static class CancellationRaceProbe {\n"
            "        public static void Run() {\n"
            "            int requestWins = 0; int completionWins = 0;\n"
            "            for (int round = 0; round < 100; round++) {\n"
            "                CancellationState.Install();\n"
            "                bool requestWon = false; bool completionWon = false;\n"
            "                using (var start = new ManualResetEvent(false)) {\n"
            "                    var request = new Thread(() => { start.WaitOne(); if ((round & 1) != 0) Thread.Sleep(2); requestWon = CancellationState.Request(); });\n"
            "                    var complete = new Thread(() => { start.WaitOne(); if ((round & 1) == 0) Thread.Sleep(2); completionWon = CancellationState.TryComplete(); });\n"
            "                    request.Start(); complete.Start(); start.Set(); request.Join(); complete.Join();\n"
            "                }\n"
            "                if (requestWon == completionWon) throw new InvalidOperationException(\"race did not linearize\");\n"
            "                if (CancellationState.IsCancellationRequested != requestWon) throw new InvalidOperationException(\"terminal state mismatch\");\n"
            "                if (requestWon) requestWins++; else completionWins++;\n"
            "                CancellationState.Remove();\n"
            "            }\n"
            "            if (requestWins == 0 || completionWins == 0) throw new InvalidOperationException(\"both outcomes were not covered\");\n"
            "        }\n"
            "    }\n\n"
        )
        cancellation_race_runner_text = runner_text.replace(
            "    public sealed class ProcessJob : IDisposable {\n",
            cancellation_race_type + "    public sealed class ProcessJob : IDisposable {\n",
            1,
        )
        self.assertNotEqual(cancellation_race_runner_text, runner_text)
        with tempfile.TemporaryDirectory() as temporary_directory:
            cancellation_race_runner = (
                Path(temporary_directory) / "cancellation-race-runner.ps1"
            )
            cancellation_race_runner.write_text(
                cancellation_race_runner_text, encoding="utf-8", newline="\n"
            )
            runner_literal = str(cancellation_race_runner).replace("'", "''")
            cancellation_race = subprocess.run(
                [
                    _powershell_executable(),
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-Command",
                    ". '"
                    + runner_literal
                    + "' -EvaluatedRoot 'x' -ApprovedSha ('0' * 40) -LocalWorkRoot 'x' -SummaryJsonPath 'x' -SummaryMarkdownPath 'x'; "
                    "Initialize-StageARuntime; "
                    "[HardwareInspection.StageA.CancellationRaceProbe]::Run()",
                ],
                text=True,
                capture_output=True,
                timeout=30,
                check=False,
            )
        self.assertEqual(cancellation_race.returncode, 0, cancellation_race.stderr)
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            mutated_runner = root / "late-cancel-runner.ps1"
            late_cancel = runner_text.replace(
                "            Invoke-HardwareInspectionIntelRunnerStageAInternal\n",
                "            $null = [HardwareInspection.StageA.CancellationState]::Request()\n",
                1,
            )
            self.assertNotEqual(late_cancel, runner_text)
            mutated_runner.write_text(late_cancel, encoding="utf-8", newline="\n")
            result = _invoke(
                mutated_runner,
                {
                    "EvaluatedRoot": root,
                    "ApprovedSha": "a" * 40,
                    "LocalWorkRoot": root,
                    "SummaryJsonPath": root / "summary.json",
                    "SummaryMarkdownPath": root / "summary.md",
                },
                os.environ.copy(),
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(result.stdout, "")
            self.assertEqual(
                result.stderr.replace("\r\n", "\n"), INVALID_RUNNER_STDERR
            )
            self.assertFalse((root / "summary.json").exists())
            self.assertFalse((root / "summary.md").exists())

        pending_replacement = (
            "            $script:StageAPendingEvaluatedRoot = $EvaluatedRoot\n"
            "            $script:StageAPendingSummaryJsonPath = $SummaryJsonPath\n"
            "            $script:StageAPendingSummaryMarkdownPath = $SummaryMarkdownPath\n"
            "            $script:StageAPendingSummaryJson = "
            "'{\"schemaVersion\":\"1.0\",\"evaluatedSha\":\"' + $ApprovedSha + "
            "'\",\"deterministicPassed\":174,\"task8DeterministicPassed\":3,\"nonPassing\":0}'\n"
            "            $script:StageAPendingSummaryMarkdown = "
            "\"# Hardware Inspection Intel Stage A`n`n- Evaluated SHA: $ApprovedSha`n"
            "- Deterministic passed: 174`n- Task8 deterministic passed: 3`n- Non-passing: 0`n\"\n"
        )
        seeded_runner = runner_text.replace(
            "            Invoke-HardwareInspectionIntelRunnerStageAInternal\n",
            pending_replacement,
            1,
        )
        self.assertNotEqual(seeded_runner, runner_text)
        for mutation_name, mutation in (
            (
                "cleanup-cancel",
                seeded_runner.replace(
                    "try { Stop-StageAOwnedProcesses; Test-StageAResidualProcesses }",
                    "try { Stop-StageAOwnedProcesses; $null = [HardwareInspection.StageA.CancellationState]::Request(); Test-StageAResidualProcesses }",
                    1,
                ),
            ),
            (
                "publication-cancel",
                seeded_runner.replace(
                    "                    Write-StageAAtomicUtf8 $publishJsonPath $script:StageAPendingSummaryJson\n"
                    "                    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)\n",
                    "                    Write-StageAAtomicUtf8 $publishJsonPath $script:StageAPendingSummaryJson\n"
                    "                    $null = [HardwareInspection.StageA.CancellationState]::Request()\n"
                    "                    Assert-StageACondition (-not [HardwareInspection.StageA.CancellationState]::IsCancellationRequested)\n",
                    1,
                ),
            ),
        ):
            with self.subTest(cancellation_seam=mutation_name), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                mutated_runner = root / (mutation_name + "-runner.ps1")
                mutated_runner.write_text(mutation, encoding="utf-8", newline="\n")
                evaluated = root / "evaluated"
                evaluated.mkdir()
                json_path = root / "summary.json"
                markdown_path = root / "summary.md"
                result = _invoke(
                    mutated_runner,
                    {
                        "EvaluatedRoot": evaluated,
                        "ApprovedSha": "a" * 40,
                        "LocalWorkRoot": root,
                        "SummaryJsonPath": json_path,
                        "SummaryMarkdownPath": markdown_path,
                    },
                    os.environ.copy(),
                )
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stdout, "")
                self.assertEqual(
                    result.stderr.replace("\r\n", "\n"), INVALID_RUNNER_STDERR
                )
                if mutation_name == "cleanup-cancel":
                    self.assertFalse(json_path.exists())
                else:
                    self.assertTrue(json_path.is_file())
                    self.assertNotIn(b"tampered", json_path.read_bytes())
                self.assertFalse(markdown_path.exists())

        if os.name == "nt":
            with tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                evaluated = root / "evaluated"
                evaluated.mkdir()
                ready = root / "cancel-ready.txt"
                child_pid_path = root / "cancel-child-pid.txt"
                log_path = root / "cancel-child.log"
                json_path = root / "summary.json"
                markdown_path = root / "summary.md"
                child_script = (
                    "[IO.File]::WriteAllText('"
                    + str(child_pid_path).replace("'", "''")
                    + "',[string]$PID); "
                    + "[IO.File]::WriteAllText('"
                    + str(ready).replace("'", "''")
                    + "','ready'); [Threading.Thread]::Sleep(60000)"
                )
                child_encoded = base64.b64encode(
                    child_script.encode("utf-16-le")
                ).decode("ascii")
                cancellation_runner_text = runner_text.replace(
                    "            Invoke-HardwareInspectionIntelRunnerStageAInternal\n",
                    "            $tool = (Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                    "            Invoke-StageAProcess $tool @('-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand','"
                    + child_encoded
                    + "') '"
                    + str(log_path).replace("'", "''")
                    + "' 30 | Out-Null\n",
                    1,
                )
                self.assertNotEqual(cancellation_runner_text, runner_text)
                cancellation_runner = root / "live-cancellation-runner.ps1"
                cancellation_runner.write_text(
                    cancellation_runner_text, encoding="utf-8", newline="\n"
                )
                command = [
                    _powershell_executable(),
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-File",
                    str(cancellation_runner),
                    "-EvaluatedRoot",
                    str(evaluated),
                    "-ApprovedSha",
                    "a" * 40,
                    "-LocalWorkRoot",
                    str(root / "work"),
                    "-SummaryJsonPath",
                    str(json_path),
                    "-SummaryMarkdownPath",
                    str(markdown_path),
                ]
                child_pid = None
                process = subprocess.Popen(
                    command,
                    text=True,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    creationflags=subprocess.CREATE_NEW_PROCESS_GROUP,
                )
                try:
                    for _ in range(500):
                        if ready.exists() or process.poll() is not None:
                            break
                        time.sleep(0.02)
                    self.assertTrue(ready.exists(), "live cancellation child never became ready")
                    child_pid = int(child_pid_path.read_text(encoding="utf-8"))
                    os.kill(process.pid, signal.CTRL_BREAK_EVENT)
                    stdout, stderr = process.communicate(timeout=20)
                    self.assertNotEqual(process.returncode, 0)
                    self.assertEqual(stdout, "")
                    self.assertEqual(
                        stderr.replace("\r\n", "\n"), INVALID_RUNNER_STDERR
                    )
                    self.assertFalse(json_path.exists())
                    self.assertFalse(markdown_path.exists())
                    for _ in range(250):
                        probe = subprocess.run(
                            [
                                _powershell_executable(),
                                "-NoProfile",
                                "-NonInteractive",
                                "-Command",
                                "if (Get-Process -Id "
                                + str(child_pid)
                                + " -ErrorAction SilentlyContinue) { exit 1 }",
                            ],
                            capture_output=True,
                            timeout=5,
                            check=False,
                        )
                        if probe.returncode == 0:
                            break
                        time.sleep(0.02)
                    self.assertEqual(probe.returncode, 0, "cancelled job descendant survived")
                finally:
                    if process.poll() is None:
                        process.kill()
                        process.communicate(timeout=5)
                    if child_pid is not None:
                        subprocess.run(
                            ["taskkill.exe", "/PID", str(child_pid), "/F"],
                            capture_output=True,
                            timeout=5,
                            check=False,
                        )

            with tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                ready = root / "root-ready.txt"
                release = root / "release.txt"
                child_pid_path = root / "child-pid.txt"
                log_path = root / "tree.log"
                literal = lambda value: "'" + str(value).replace("'", "''") + "'"
                child_script = "[Threading.Thread]::Sleep(60000)"
                child_encoded = base64.b64encode(
                    child_script.encode("utf-16-le")
                ).decode("ascii")
                parent_script = (
                    "$ErrorActionPreference='Stop'\n"
                    + "[IO.File]::WriteAllText("
                    + literal(ready)
                    + ",[string]$PID)\n"
                    + "$deadline=[DateTime]::UtcNow.AddSeconds(15)\n"
                    + "while(-not (Test-Path -LiteralPath "
                    + literal(release)
                    + ")) { if([DateTime]::UtcNow -ge $deadline){exit 9}; Start-Sleep -Milliseconds 20 }\n"
                    + "$info=New-Object Diagnostics.ProcessStartInfo\n"
                    + "$info.FileName=(Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                    + "$info.Arguments='-NoProfile -NonInteractive -EncodedCommand "
                    + child_encoded
                    + "'\n"
                    + "$info.UseShellExecute=$true\n"
                    + "$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden\n"
                    + "$child=[Diagnostics.Process]::Start($info)\n"
                    + "[IO.File]::WriteAllText("
                    + literal(child_pid_path)
                    + ",[string]$child.Id)\n"
                )
                parent_encoded = base64.b64encode(
                    parent_script.encode("utf-16-le")
                ).decode("ascii")
                body = (
                    "Initialize-StageARuntime\n"
                    "$tool=(Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                    "try { Invoke-StageAProcess $tool @('-NoProfile','-NonInteractive','-EncodedCommand','"
                    + parent_encoded
                    + "') "
                    + literal(log_path)
                    + " 25 | Out-Null } finally { Stop-StageAOwnedProcesses }"
                )
                process = subprocess.Popen(
                    _runner_pure_command(body),
                    text=True,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                )
                child_pid = None
                try:
                    for _ in range(500):
                        if ready.exists():
                            break
                        if process.poll() is not None:
                            break
                        time.sleep(0.02)
                    self.assertTrue(ready.exists(), "job fixture root never reached its release gate")
                    time.sleep(0.2)
                    release.write_text("release\n", encoding="utf-8", newline="\n")
                    stdout, stderr = process.communicate(timeout=30)
                    self.assertEqual(process.returncode, 0, stderr)
                    self.assertEqual(stdout, "")
                    child_pid = int(child_pid_path.read_text(encoding="utf-8"))
                    for _ in range(250):
                        probe = subprocess.run(
                            [
                                _powershell_executable(),
                                "-NoProfile",
                                "-NonInteractive",
                                "-Command",
                                "if (Get-Process -Id "
                                + str(child_pid)
                                + " -ErrorAction SilentlyContinue) { exit 1 }",
                            ],
                            capture_output=True,
                            timeout=5,
                            check=False,
                        )
                        if probe.returncode == 0:
                            break
                        time.sleep(0.02)
                    self.assertEqual(probe.returncode, 0, "job descendant survived cleanup")
                finally:
                    if process.poll() is None:
                        process.kill()
                        process.communicate(timeout=5)
                    if child_pid is not None:
                        subprocess.run(
                            ["taskkill.exe", "/PID", str(child_pid), "/F"],
                            capture_output=True,
                            timeout=5,
                            check=False,
                        )

            with tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                evaluated = root / "evaluated"
                evaluated.mkdir()
                json_path = root / "summary.json"
                markdown_path = root / "summary.md"
                child_ready = root / "overwriter-ready.txt"
                child_pid_path = root / "overwriter-pid.txt"
                log_path = root / "overwriter-parent.log"
                literal = lambda value: "'" + str(value).replace("'", "''") + "'"
                child_script = (
                    "[IO.File]::WriteAllText("
                    + literal(child_ready)
                    + ",'ready')\n"
                    + "$deadline=[DateTime]::UtcNow.AddSeconds(20)\n"
                    + "while(-not (Test-Path -LiteralPath "
                    + literal(json_path)
                    + ")) { if([DateTime]::UtcNow -ge $deadline){exit 9}; Start-Sleep -Milliseconds 5 }\n"
                    + "[IO.File]::WriteAllText("
                    + literal(json_path)
                    + ",'tampered-by-descendant')\n"
                    + "Start-Sleep -Seconds 20\n"
                )
                child_encoded = base64.b64encode(
                    child_script.encode("utf-16-le")
                ).decode("ascii")
                parent_script = (
                    "$ErrorActionPreference='Stop'\n"
                    + "$info=New-Object Diagnostics.ProcessStartInfo\n"
                    + "$info.FileName=(Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                    + "$info.Arguments='-NoProfile -NonInteractive -EncodedCommand "
                    + child_encoded
                    + "'\n"
                    + "$info.UseShellExecute=$true\n"
                    + "$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden\n"
                    + "$child=[Diagnostics.Process]::Start($info)\n"
                    + "[IO.File]::WriteAllText("
                    + literal(child_pid_path)
                    + ",[string]$child.Id)\n"
                    + "$deadline=[DateTime]::UtcNow.AddSeconds(10)\n"
                    + "while(-not (Test-Path -LiteralPath "
                    + literal(child_ready)
                    + ")) { if([DateTime]::UtcNow -ge $deadline){exit 8}; Start-Sleep -Milliseconds 10 }\n"
                )
                parent_encoded = base64.b64encode(
                    parent_script.encode("utf-16-le")
                ).decode("ascii")
                lingering_replacement = (
                    "            $tool = (Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source\n"
                    "            Invoke-StageAProcess $tool @('-NoProfile','-NonInteractive','-EncodedCommand','"
                    + parent_encoded
                    + "') "
                    + literal(log_path)
                    + " 20 | Out-Null\n"
                    + pending_replacement
                )
                lingering_runner_text = runner_text.replace(
                    "            Invoke-HardwareInspectionIntelRunnerStageAInternal\n",
                    lingering_replacement,
                    1,
                )
                self.assertNotEqual(lingering_runner_text, runner_text)
                lingering_runner = root / "lingering-overwriter-runner.ps1"
                lingering_runner.write_text(
                    lingering_runner_text, encoding="utf-8", newline="\n"
                )
                child_pid = None
                try:
                    result = _invoke(
                        lingering_runner,
                        {
                            "EvaluatedRoot": evaluated,
                            "ApprovedSha": "a" * 40,
                            "LocalWorkRoot": root,
                            "SummaryJsonPath": json_path,
                            "SummaryMarkdownPath": markdown_path,
                        },
                        os.environ.copy(),
                    )
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertEqual(result.stdout, "")
                    self.assertEqual(result.stderr, "")
                    child_pid = int(child_pid_path.read_text(encoding="utf-8"))
                    expected_json = (
                        '{"schemaVersion":"1.0","evaluatedSha":"'
                        + ("a" * 40)
                        + '","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}'
                    ).encode("utf-8")
                    time.sleep(0.2)
                    self.assertEqual(json_path.read_bytes(), expected_json)
                    self.assertNotIn(b"tampered", markdown_path.read_bytes())
                    probe = subprocess.run(
                        [
                            _powershell_executable(),
                            "-NoProfile",
                            "-NonInteractive",
                            "-Command",
                            "if (Get-Process -Id "
                            + str(child_pid)
                            + " -ErrorAction SilentlyContinue) { exit 1 }",
                        ],
                        capture_output=True,
                        timeout=5,
                        check=False,
                    )
                    self.assertEqual(probe.returncode, 0, "summary overwriter survived cleanup")
                finally:
                    if child_pid is not None:
                        subprocess.run(
                            ["taskkill.exe", "/PID", str(child_pid), "/F"],
                            capture_output=True,
                            timeout=5,
                            check=False,
                        )

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
                trx_namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
                foreign_definition = valid.replace(
                    "<UnitTest ", '<UnitTest xmlns="" ', 1
                ).replace(
                    "<Execution ", f'<Execution xmlns="{trx_namespace}" ', 1
                ).replace(
                    "<TestMethod ", f'<TestMethod xmlns="{trx_namespace}" ', 1
                )
                wrong_definition = valid.replace(
                    "<UnitTest ", "<AlternateUnitTest ", 1
                ).replace("</UnitTest>", "</AlternateUnitTest>", 1)
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
                    "foreign-result-child": valid.replace(
                        "<UnitTestResult ", '<UnitTestResult xmlns="" ', 1
                    ),
                    "wrong-result-child": valid.replace(
                        "<UnitTestResult ", "<AlternateUnitTestResult ", 1
                    ),
                    "foreign-definition-child": foreign_definition,
                    "wrong-definition-child": wrong_definition,
                    "foreign-entry-child": valid.replace(
                        "<TestEntry ", '<TestEntry xmlns="" ', 1
                    ),
                    "wrong-entry-child": valid.replace(
                        "<TestEntry ", "<AlternateTestEntry ", 1
                    ),
                }
                for counter_name in ("notRunnable", "disconnected", "warning", "completed", "inProgress", "pending"):
                    mutations["counter-" + counter_name] = valid.replace(
                        counter_name + '=\"0\"', counter_name + '=\"1\"', 1
                    )
                mutations.update(
                    {
                        "summary-outcome": valid.replace('ResultSummary outcome="Completed"', 'ResultSummary outcome="Aborted"', 1),
                        "missing-summary-outcome": valid.replace('ResultSummary outcome="Completed"', 'ResultSummary', 1),
                        "passed-but-aborted": valid.replace('passedButRunAborted="0"', 'passedButRunAborted="1"', 1),
                        "missing-passed-but-aborted": valid.replace(' passedButRunAborted="0"', '', 1),
                    }
                )
                for name, mutated in mutations.items():
                    with self.subTest(kind=kind, mutation=name):
                        if name in {
                            "foreign-result-child",
                            "wrong-result-child",
                            "foreign-definition-child",
                            "wrong-definition-child",
                            "foreign-entry-child",
                            "wrong-entry-child",
                        }:
                            self.assertEqual(
                                _trx_direct_element_counts(mutated),
                                _trx_direct_element_counts(valid),
                            )
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
        for required_privacy_check in ("llmfit", ".trx"):
            self.assertIn(required_privacy_check.casefold(), runner_text.casefold())
        if WORKFLOW_PATH.is_file():
            _, document = _workflow(self)
            _assert_summary_upload(self, document)
            workflow_runs = "\n".join(str(step.get("run", "")) for _, step in _all_steps(document))
            _assert_no_host_or_path_leaks(self, workflow_runs)
            artifact_validator = next(
                step["run"]
                for step in _steps(document, "deterministic-runner")
                if step.get("name") == "Validate Stage A summary artifacts before upload"
            )
            approved_sha = "a" * 40
            expected_artifact_json = (
                '{"schemaVersion":"1.0","evaluatedSha":"'
                + approved_sha
                + '","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}'
            ).encode("utf-8")
            expected_artifact_markdown = (
                "# Hardware Inspection Intel Stage A\n\n- Evaluated SHA: "
                + approved_sha
                + "\n- Deterministic passed: 174\n- Task8 deterministic passed: 3\n- Non-passing: 0\n"
            ).encode("utf-8")

            def artifact_fixture(
                json_bytes,
                markdown_bytes,
                file_reparse=False,
                ancestor_reparse=False,
                lock_json=False,
            ):
                with tempfile.TemporaryDirectory() as temporary_directory:
                    root = Path(temporary_directory)
                    if ancestor_reparse:
                        actual_export = root / "actual-export"
                        actual_export.mkdir()
                        export = root / "stage-a-export"
                        try:
                            export.symlink_to(actual_export, target_is_directory=True)
                        except OSError:
                            return None
                    else:
                        export = root / "stage-a-export"
                        export.mkdir()
                    json_path = export / "stage-a-summary.json"
                    markdown_path = export / "stage-a-summary.md"
                    json_path.write_bytes(json_bytes)
                    markdown_path.write_bytes(markdown_bytes)
                    if file_reparse:
                        target = root / "summary-target.json"
                        target.write_bytes(json_bytes)
                        json_path.unlink()
                        try:
                            json_path.symlink_to(target)
                        except OSError:
                            return None
                    environment = os.environ.copy()
                    environment["STAGEA_APPROVED_SHA"] = approved_sha
                    if lock_json:
                        with _ExclusiveFileLock(json_path) as locked:
                            if not locked:
                                return None
                            return _run_inline_powershell(
                                artifact_validator,
                                environment=environment,
                                cwd=root,
                            )
                    return _run_inline_powershell(
                        artifact_validator,
                        environment=environment,
                        cwd=root,
                    )

            exact_artifacts = artifact_fixture(
                expected_artifact_json, expected_artifact_markdown
            )
            self.assertEqual(exact_artifacts.returncode, 0, exact_artifacts.stderr)
            self.assertEqual(exact_artifacts.stdout, "")
            self.assertEqual(exact_artifacts.stderr, "")
            for case_name, json_bytes, markdown_bytes in (
                ("malformed-json", b"{}", expected_artifact_markdown),
                ("extra-json-byte", expected_artifact_json + b"x", expected_artifact_markdown),
                ("malformed-markdown", expected_artifact_json, b"malformed"),
                ("extra-markdown-byte", expected_artifact_json, expected_artifact_markdown + b"x"),
            ):
                with self.subTest(artifact_validation=case_name):
                    result = artifact_fixture(json_bytes, markdown_bytes)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(result.stdout, "")
                    self.assertEqual(
                        result.stderr.replace("\r\n", "\n"),
                        "HI-RUNNER-STAGEA-ARTIFACTS-INVALID: summary validation failed.\n",
                    )
            for case_name, kwargs in (
                ("file-reparse", {"file_reparse": True}),
                ("ancestor-reparse", {"ancestor_reparse": True}),
                ("locked-json", {"lock_json": True}),
            ):
                with self.subTest(artifact_validation=case_name):
                    result = artifact_fixture(
                        expected_artifact_json,
                        expected_artifact_markdown,
                        **kwargs,
                    )
                    if result is not None:
                        self.assertNotEqual(result.returncode, 0)
                        self.assertEqual(result.stdout, "")
                        self.assertEqual(
                            result.stderr.replace("\r\n", "\n"),
                            "HI-RUNNER-STAGEA-ARTIFACTS-INVALID: summary validation failed.\n",
                        )
            trx_upload = copy.deepcopy(document)
            trx_steps = [
                step for _, step in _all_steps(trx_upload)
                if str(step.get("uses", "")).startswith("actions/upload-artifact@")
            ]
            trx_steps[0].setdefault("with", {})["path"] = "local\\deterministic.trx"
            with self.assertRaises(AssertionError):
                _assert_summary_upload(self, trx_upload)
            publisher = next(
                step["run"]
                for step in _steps(document, "deterministic-runner")
                if step.get("name") == "Publish validated Stage A summary"
            )
            def publish_fixture(source_bytes, target_bytes, source_reparse=False, target_reparse=False):
                with tempfile.TemporaryDirectory() as temporary_directory:
                    root = Path(temporary_directory)
                    export = root / "stage-a-export"
                    export.mkdir()
                    source = export / "stage-a-summary.md"
                    target = root / "github-summary.md"
                    source.write_bytes(source_bytes)
                    target.write_bytes(target_bytes)
                    if source_reparse or target_reparse:
                        link = root / ("source-link.md" if source_reparse else "target-link.md")
                        destination = source if source_reparse else target
                        try:
                            link.symlink_to(destination)
                        except OSError:
                            return None
                        if source_reparse:
                            source.unlink()
                            link.rename(source)
                        else:
                            target.unlink()
                            link.rename(target)
                    literal = lambda value: "'" + str(value).replace("'", "''") + "'"
                    command = (
                        "$env:GITHUB_STEP_SUMMARY = " + literal(target) + "\n"
                        "$env:STAGEA_APPROVED_SHA = '" + ("a" * 40) + "'\n"
                        "& {\n" + publisher + "\n}"
                    )
                    result = subprocess.run(
                        [_powershell_executable(), "-NoProfile", "-NonInteractive", "-Command", command],
                        text=True, capture_output=True, cwd=root, timeout=20, check=False,
                    )
                    return result, target.read_bytes() if target.exists() else b""
            expected_publish = (
                "# Hardware Inspection Intel Stage A\n\n- Evaluated SHA: " + ("a" * 40)
                + "\n- Deterministic passed: 174\n- Task8 deterministic passed: 3\n- Non-passing: 0\n"
            ).encode("utf-8")
            valid_publish = publish_fixture(expected_publish, b"")
            self.assertIsNotNone(valid_publish)
            self.assertEqual(valid_publish[0].returncode, 0, valid_publish[0].stderr)
            self.assertEqual(valid_publish[1], expected_publish)
            for source_bytes, target_bytes in ((b"malformed", b""), (expected_publish + b"x", b""), (expected_publish, b"nonempty")):
                result = publish_fixture(source_bytes, target_bytes)
                self.assertNotEqual(result[0].returncode, 0)
                self.assertEqual(result[0].stderr.replace("\r\n", "\n"), "HI-RUNNER-STAGEA-SUMMARY-INVALID: summary publication failed.\n")
            for source_reparse, target_reparse in ((True, False), (False, True)):
                result = publish_fixture(expected_publish, b"", source_reparse, target_reparse)
                if result is not None:
                    self.assertNotEqual(result[0].returncode, 0)
        sha = "a" * 40
        valid_json = (
            '{"schemaVersion":"1.0","evaluatedSha":"'
            + sha
            + '","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}'
        )
        valid_markdown = (
            "# Hardware Inspection Intel Stage A\n\n"
            + "- Evaluated SHA: "
            + sha
            + "\n- Deterministic passed: 174\n- Task8 deterministic passed: 3\n- Non-passing: 0\n"
        )
        def privacy_result(markdown):
            literal = lambda value: "'" + value.replace("'", "''") + "'"
            return _invoke_runner_pure(
                "Assert-StageASummaryPrivacy "
                + literal(valid_json)
                + " "
                + literal(markdown)
                + " "
                + literal(sha)
            )
        self.assertEqual(privacy_result(valid_markdown).returncode, 0)
        for malformed_markdown in (
            '{"x":1}',
            '<Results><UnitTestResult /></Results>',
            valid_markdown + "appended content\n",
        ):
            with self.subTest(malformed_markdown=malformed_markdown[:20]):
                self.assertNotEqual(privacy_result(malformed_markdown).returncode, 0)
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            evaluated = root / "evaluated"
            evaluated.mkdir()
            summary_json = root / "summary.json"
            summary_markdown = root / "summary.md"
            summary_markdown.write_bytes(b"")
            literal = lambda value: "'" + str(value).replace("'", "''") + "'"
            publish = _invoke_runner_pure(
                "$json = Get-StageAOutputPath "
                + literal(summary_json)
                + " "
                + literal(evaluated)
                + " $false\n$markdown = Get-StageAOutputPath "
                + literal(summary_markdown)
                + " "
                + literal(evaluated)
                + " $true\nWrite-StageAAtomicUtf8 $json 'json' $false\n"
                + "Write-StageAAtomicUtf8 $markdown 'markdown' $true"
            )
            self.assertNotEqual(publish.returncode, 0)
            self.assertFalse(summary_json.exists())
            self.assertEqual(summary_markdown.read_bytes(), b"")
            nonempty_markdown = root / "nonempty-summary.md"
            nonempty_markdown.write_bytes(b"preserve")
            rejected_nonempty = _invoke_runner_pure(
                "Get-StageAOutputPath "
                + literal(nonempty_markdown)
                + " "
                + literal(evaluated)
                + " $true | Out-Null"
            )
            self.assertNotEqual(rejected_nonempty.returncode, 0)
            self.assertEqual(nonempty_markdown.read_bytes(), b"preserve")
            preexisting_json = root / "preexisting-summary.json"
            preexisting_json.write_bytes(b"")
            rejected_json = _invoke_runner_pure(
                "Get-StageAOutputPath "
                + literal(preexisting_json)
                + " "
                + literal(evaluated)
                + " $false | Out-Null"
            )
            self.assertNotEqual(rejected_json.returncode, 0)
            summary_directory = root / "summary-directory"
            summary_directory.mkdir()
            rejected_directory = _invoke_runner_pure(
                "Get-StageAOutputPath "
                + literal(summary_directory)
                + " "
                + literal(evaluated)
                + " $true | Out-Null"
            )
            self.assertNotEqual(rejected_directory.returncode, 0)
            reparse_target = root / "summary-target.md"
            reparse_target.write_bytes(b"")
            reparse_summary = root / "summary-reparse.md"
            try:
                reparse_summary.symlink_to(reparse_target)
            except OSError:
                pass
            else:
                rejected_reparse = _invoke_runner_pure(
                    "Get-StageAOutputPath "
                    + literal(reparse_summary)
                    + " "
                    + literal(evaluated)
                    + " $true | Out-Null"
                )
                self.assertNotEqual(rejected_reparse.returncode, 0)
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
        for canonical_path in (
            WORKFLOW_PATH,
            PLAN_PATH,
            RUNBOOK_PATH,
        ):
            _assert_canonical_file_bytes(self, canonical_path)
        _assert_canonical_file_bytes(
            self,
            Path(__file__).resolve(),
            allow_crlf=True,
        )
        for powershell_path in (VALIDATOR_PATH, RUNNER_PATH):
            _assert_canonical_file_bytes(self, powershell_path, allow_crlf=True)
        runbook_raw = RUNBOOK_PATH.read_bytes()
        runbook = _strict_utf8(RUNBOOK_PATH).casefold()
        _assert_pre_registration_identity_privacy(self, runbook)
        _assert_pre_step_proxy_and_debug_privacy(self, runbook)
        _assert_pre_step_hook_absence(self, runbook)
        _assert_pre_step_action_cache_override_absence(self, runbook)
        _assert_one_run_dependency_cleanup_runbook(self, runbook)
        _assert_hidden_prompt_token_handling(self, runbook)
        plan = _strict_utf8(PLAN_PATH).casefold()
        _assert_one_run_dependency_plan_order(self, plan)
        for required_plan_text in (
            "actual windows computer name",
            "expected runner-group display",
            "local no-echo",
            "pre-step metadata",
            "workflow masks cannot remediate",
            "http_proxy",
            "https_proxy",
            "no_proxy",
            "actions_runner_debug",
            "actions_step_debug",
            "runner_debug",
            "trace/print-log controls",
            "actions_runner_hook_job_started",
            "actions_runner_hook_job_completed",
            "actions_runner_action_archive_cache",
            "actions_runner_symlink_cached_actions",
            "dotnet_install_dir",
            "dotnet_cli_home",
            "nuget_packages",
            "nuget_http_cache_path",
            "nuget_plugins_cache_path",
            "nuget_scratch",
            "one-run sdk and nuget state",
            "effective process/user/system environment",
            "fresh runner-root `.env`",
            "actions_runner_input_token",
            "hidden secret prompt",
            "omit `--token`",
            "do not clear, reconfigure, or rename",
        ):
            self.assertIn(required_plan_text, plan)
        identity_privacy_mutations = (
            (
                "before registration, use the ucl-approved local no-echo procedure",
                "after registration, use the ucl-approved local no-echo procedure",
            ),
            (
                "explicitly ucl-approved and non-identifying",
                "accepted by the operator",
            ),
            (
                "do not print, echo, or store either value",
                "print both values for review",
            ),
            (
                "workflow masks cannot remediate this pre-step metadata",
                "workflow masks remediate this metadata",
            ),
            (
                "do not rename the laptop unless ucl separately authorises the rename",
                "rename the laptop before registration",
            ),
        )
        for original, replacement in identity_privacy_mutations:
            self.assertIn(original, runbook)
            with self.subTest(identity_privacy_mutation=original):
                with self.assertRaises(AssertionError):
                    _assert_pre_registration_identity_privacy(
                        self, runbook.replace(original, replacement, 1)
                    )
        proxy_privacy_mutations = (
            (
                "unknown or unsafe proxy metadata is a hard stop",
                "unknown proxy metadata may continue",
            ),
            (
                "do not clear or reconfigure proxy state merely to continue",
                "clear proxy state before continuing",
            ),
            (
                "must all be absent before start",
                "may remain enabled before start",
            ),
        )
        for original, replacement in proxy_privacy_mutations:
            self.assertIn(original, runbook)
            with self.subTest(proxy_privacy_mutation=original):
                with self.assertRaises(AssertionError):
                    _assert_pre_step_proxy_and_debug_privacy(
                        self, runbook.replace(original, replacement, 1)
                    )
        hook_absence_mutations = (
            (
                "either value or entry is a hard stop",
                "either hook may run",
            ),
            (
                "never execute, clear, or repair it",
                "clear the hook and continue",
            ),
        )
        for original, replacement in hook_absence_mutations:
            self.assertIn(original, runbook)
            with self.subTest(hook_absence_mutation=original):
                with self.assertRaises(AssertionError):
                    _assert_pre_step_hook_absence(
                        self, runbook.replace(original, replacement, 1)
                    )
        action_cache_override_mutations = (
            (
                "before registration and again immediately before `run.cmd`",
                "after registration",
            ),
            (
                "any value or entry is a hard stop",
                "an approved value may continue",
            ),
            (
                "must not execute, clear, repair, or override it merely to continue",
                "clear the override and continue",
            ),
            (
                "first-step workflow check is defence in depth only",
                "first-step workflow check replaces the local boundary",
            ),
        )
        for original, replacement in action_cache_override_mutations:
            self.assertIn(original, runbook)
            with self.subTest(action_cache_override_mutation=original):
                with self.assertRaises(AssertionError):
                    _assert_pre_step_action_cache_override_absence(
                        self, runbook.replace(original, replacement, 1)
                    )
        dependency_cleanup_mutations = (
            (
                "remove only all three separately revalidated exact targets",
                "remove only the canonical stage a phase directory",
            ),
            ("`nuget-scratch`", "`unmanaged-nuget-scratch`"),
        )
        for original, replacement in dependency_cleanup_mutations:
            self.assertIn(original, runbook)
            with self.subTest(dependency_cleanup_mutation=original):
                with self.assertRaises(AssertionError):
                    _assert_one_run_dependency_cleanup_runbook(
                        self, runbook.replace(original, replacement)
                    )
        with self.assertRaises(AssertionError):
            _assert_one_run_dependency_plan_order(
                self,
                plan.replace("before setup-dotnet", "after setup-dotnet", 1),
            )
        token_handling_mutations = (
            (
                "omit `--token`",
                "include `--token`",
            ),
            (
                "enter the token only at the runner's hidden secret prompt",
                "pass the token on the command line",
            ),
        )
        for original, replacement in token_handling_mutations:
            self.assertIn(original, runbook)
            with self.subTest(token_handling_mutation=original):
                with self.assertRaises(AssertionError):
                    _assert_hidden_prompt_token_handling(
                        self, runbook.replace(original, replacement, 1)
                    )
        for required_text in (
            "written ucl approval",
            "complete dispatch, queue, registration, and job window",
            "dedicated non-admin",
            "mutual ntfs isolation",
            "no browser, pat, or retained secret",
            "github's current supported runner",
            "github-displayed sha-256",
            "transfer the verified archive, never an extracted runner tree",
            "recompute its sha-256 on the laptop immediately before extraction",
            "runner.listener",
            "runner.worker",
            "scheduled auto-start",
            "dispatch while the hardware runner is absent",
            "second dispatch",
            "sole expected queued run",
            "triggering actor",
            "attempt `1`",
            "confirmation input",
            "fresh one-time label",
            "no other queued or running job targets the fresh label",
            "fresh non-identifying runner name",
            "--name <fresh-non-identifying-runner-name>",
            "fresh, empty, dedicated work directory",
            "outside onedrive, network, and profile locations",
            "no reparse point in its ancestor chain",
            "--ephemeral --no-default-labels --labels <fresh-label> --work <fresh-work-directory>",
            "transient one-hour registration token",
            "run.cmd",
            "174 deterministic",
            "3 task 8 deterministic",
            "privacy-safe artifact",
            "json-only privacy-safe artifact",
            "hardware-inspection-stage-a-summary-<run-id>-1",
            "`schemaversion`",
            "`evaluatedsha`",
            "`deterministicpassed`",
            "`task8deterministicpassed`",
            "`nonpassing`",
            "`schemaversion` must equal `\"1.0\"`",
            "`evaluatedsha` must equal the exact approved sha",
            "`deterministicpassed` must equal `174`",
            "`task8deterministicpassed` must equal `3`",
            "`nonpassing` must equal `0`",
            "retained for 3 days",
            "sanitised remote log",
            "raw trx, detailed restore/build/test logs",
            "artifactstringshape_rejectspathsandfreetextwithgenericdiagnostics",
            "captureinterval_thirtysecondsplusonetickisoutsideboundary",
            "stablefileidentityandprocesstreecleanup_arefailclosed",
            "automatic deregistration",
            "time-limited removal flow",
            "bounded diagnostic window",
            "`_temp`",
            "`_diag`",
            "canonical stage a phase directory",
            "approval manifest at the exact default-branch run commit",
            "`.github/hardware-inspection/llmfit-gate1-approved-source.json`",
            "runner installation directory: inspect `_diag`",
            "work directory: inspect `_temp`",
            "no registration or removal token may be requested",
            "defender, edr, applocker, firewall, tls, and powershell policy",
            "do not disable, bypass, weaken, or reconfigure",
            "no hardware evidence",
            "no candidate evidence",
            "no trusted intel evidence",
            "no offline evidence",
            "no gate 1 evidence",
            "does not permit gate 2",
            "writer-trust failure",
            "wrong, changed, or non-sole queued run",
            "fixed or reused label",
            "operational environment variable",
            "debug logging",
            "unverified cleanup target",
            "global process-name kill",
            "recursive parent deletion",
            "outside the one authorised interactive `run.cmd` session",
            "stage b",
        ):
            self.assertIn(required_text, runbook)

        self.assertEqual(
            hashlib.sha256(runbook_raw).hexdigest(), EXPECTED_RUNBOOK_SHA256
        )
        runbook_digest_mutation = runbook_raw.replace(
            b"# Hardware Inspection", b"# Mutated Hardware Inspection", 1
        )
        self.assertNotEqual(runbook_digest_mutation, runbook_raw)
        self.assertNotEqual(
            hashlib.sha256(runbook_digest_mutation).hexdigest(),
            EXPECTED_RUNBOOK_SHA256,
        )

        for forbidden_text in (
            "hardware-inspection-llm-fit-gate-1-runbook.md",
            "trustedwindowsintel",
            "trustedoffline",
            "granite_llmfit_",
            "get-netadapter",
            "disable-netadapter",
            "enable-netadapter",
            "invoke-webrequest",
            "stop-process",
            "taskkill",
            "remove-item",
            "gh workflow run",
        ):
            self.assertNotIn(forbidden_text, runbook)

        testing_index = _strict_utf8(TESTING_INDEX_PATH)
        scripts_index = _strict_utf8(SCRIPTS_INDEX_PATH)
        self.assertIn(
            "[Stage A operator runbook](runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md)",
            testing_index,
        )
        self.assertIn(
            "[`Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md)",
            scripts_index,
        )
        self.assertIn(
            "[Stage A context validator](../../scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1)",
            testing_index,
        )
        self.assertIn(
            "[Stage A deterministic runner](../../scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1)",
            testing_index,
        )
        self.assertIn("Validate-HardwareInspectionIntelRunnerStageA.ps1", scripts_index)
        self.assertIn("Invoke-HardwareInspectionIntelRunnerStageA.ps1", scripts_index)
        self.assertIn("Gate 1 remains Blocked", testing_index)
        self.assertNotIn(
            "Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md",
            testing_index + scripts_index,
        )
        for evidence_id in ("F-M07", "HE-01", "HE-02"):
            self.assertIn(evidence_id, testing_index)
        workflow_paths = {
            path.relative_to(REPOSITORY_ROOT).as_posix()
            for path in (REPOSITORY_ROOT / ".github" / "workflows").rglob("*")
            if path.is_file()
        }
        self.assertEqual(workflow_paths, {
            ".github/workflows/build-and-test.yml", ".github/workflows/hardware-inspection-intel-runner-stage0.yml", ".github/workflows/hardware-inspection-intel-runner-stage-a.yml", ".github/workflows/traceability-validation.yml", ".github/workflows/workbook-05-documented-build.yml", ".github/workflows/workbook-05-phase3-assets.yml", ".github/workflows/workbook-05-phase3-dependency-preflight.yml", ".github/workflows/workbook-05-preflight.yml", ".github/workflows/workbook-05-route-b-repair.yml", ".github/workflows/workbook-05-runner-smoke.yml", ".github/workflows/workbook-05-runtime-resume.yml", ".github/workflows/workbook-05-source-admission.yml",
        })
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
            workflow_paths | {".github/workflows/unrelated-new-name.yml"},
        ):
            with self.subTest(mutation=sorted(mutation)[-1]):
                with self.assertRaises(AssertionError):
                    self.assertEqual(mutation, workflow_paths)
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
