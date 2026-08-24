import codecs
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from pathlib import Path
from urllib.parse import unquote, urlsplit


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MANIFEST_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "hardware-inspection"
    / "llmfit-gate1-approved-source.json"
)
VALIDATOR_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Validate-HardwareInspectionIntelRunnerStage0.ps1"
)
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "workflows"
    / "hardware-inspection-intel-runner-stage0.yml"
)
EXPECTED_WORKFLOW_SHA256 = "db075979900b0e5ca5aef3588f64225221f91bba744018f20180470177061ab3"
EXPECTED_STAGE0_RUNBOOK_SHA256 = "27b006d1e28e6def738578d0fec7acd885f90005402772456b1dfc751e3f9e92"
INVALID_STDERR = "HI-RUNNER-STAGE0-INVALID: repository-only validation failed.\n"
GIT_INVALID_STDERR = "HI-RUNNER-STAGE0-GIT-INVALID: required Git capability is unavailable.\n"


def strict_json_object(text):
    def reject_duplicates(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate JSON key: " + key)
            result[key] = value
        return result

    return json.loads(text, object_pairs_hook=reject_duplicates)


def powershell_executable():
    for name in ("powershell.exe", "powershell", "pwsh.exe", "pwsh"):
        candidate = shutil.which(name)
        if candidate:
            return candidate
    raise unittest.SkipTest("PowerShell executable is not available")


def write_manifest(control_root, schema="1.0", ref="refs/heads/feature/hardware-inspection", sha="cc2e57ceb94e73e49f34fc383d5440a9047fba21", extra=""):
    manifest = control_root / ".github" / "hardware-inspection" / "llmfit-gate1-approved-source.json"
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(
        '{"schemaVersion":"' + schema + '","remoteFeatureRef":"' + ref + '","approvedTipSha":"' + sha + '"' + extra + '}',
        encoding="utf-8",
        newline="\n",
    )
    return manifest


def run_validator(**parameters):
    command = [
        powershell_executable(),
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(VALIDATOR_PATH),
    ]
    for name, value in parameters.items():
        command.extend(["-" + name, str(value)])
    return subprocess.run(command, text=True, capture_output=True, timeout=20, check=False)


def valid_dispatch_parameters(control_root):
    return {
        "Phase": "Dispatch",
        "ControlRoot": control_root,
        "WorkflowRef": "refs/heads/main",
        "DefaultBranch": "main",
        "Actor": "arian20020",
        "TriggeringActor": "arian20020",
        "RepositoryOwner": "arian20020",
        "RunAttempt": "1",
        "ConfirmRepositoryOnly": "true",
    }


def normalized(value):
    return value.replace("\r\n", "\n").replace("\r", "\n")


def _workflow_run_body(step):
    marker = "        run: |\n"
    if marker not in step:
        raise AssertionError("workflow step has no PowerShell run block")
    body = step.split(marker, 1)[1]
    lines = body.splitlines()
    if any(not line.startswith("          ") for line in lines if line):
        raise AssertionError("workflow run block indentation is not canonical")
    return "\n".join(line[10:] for line in lines) + "\n"


def _run_git_precheck(
    run_body,
    *,
    output="git version 2.51.0",
    mode="output",
    source=None,
    secondary_output=None,
):
    with tempfile.TemporaryDirectory() as temporary_directory:
        root = Path(temporary_directory)
        stub = root / "git.cmd"
        secondary_stub = root / "git-secondary.cmd"
        if mode == "output" or mode == "nonzero":
            stub.write_text(
                "@echo off\n"
                + "\n".join("@echo " + line for line in output.splitlines())
                + "\n@exit /b " + ("7" if mode == "nonzero" else "0") + "\n",
                encoding="ascii",
                newline="\r\n",
            )
        if secondary_output is not None:
            secondary_stub.write_text(
                "@echo off\n"
                + "\n".join("@echo " + line for line in secondary_output.splitlines())
                + "\n@exit /b 0\n",
                encoding="ascii",
                newline="\r\n",
            )
        wrapper = root / "git-precheck.ps1"
        wrapper.write_text(
            textwrap.dedent(
                """
                function Get-Command {
                  [CmdletBinding()]
                  param(
                    [string]$Name,
                    [string]$CommandType
                  )
                  if ($env:STAGE0_GIT_MODE -eq 'missing') {
                    return $null
                  }
                  if ($env:STAGE0_GIT_MODE -eq 'throwing') {
                    throw 'stub failure'
                  }
                  [pscustomobject]@{ Source = $env:STAGE0_GIT_SOURCE }
                  if (-not [string]::IsNullOrEmpty($env:STAGE0_GIT_SECONDARY_SOURCE)) {
                    [pscustomobject]@{ Source = $env:STAGE0_GIT_SECONDARY_SOURCE }
                  }
                }
                """
            ).lstrip()
            + run_body,
            encoding="utf-8",
            newline="\n",
        )
        environment = os.environ.copy()
        environment["STAGE0_GIT_MODE"] = mode
        environment["STAGE0_GIT_SOURCE"] = str(source if source is not None else stub)
        environment["STAGE0_GIT_SECONDARY_SOURCE"] = (
            str(secondary_stub) if secondary_output is not None else ""
        )
        return subprocess.run(
            [powershell_executable(), "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(wrapper)],
            text=True,
            capture_output=True,
            timeout=20,
            check=False,
            env=environment,
        )


def _workflow():
    if not WORKFLOW_PATH.is_file():
        raise AssertionError("Stage 0 workflow is missing")
    raw = WORKFLOW_PATH.read_bytes()
    if raw.startswith(codecs.BOM_UTF8):
        raise AssertionError("Stage 0 workflow must not have a UTF-8 BOM")
    if b"\r" in raw:
        raise AssertionError("Stage 0 workflow must use LF line endings")
    raw.decode("utf-8", "strict")
    return raw


def _assert_canonical_workflow(raw):
    actual = hashlib.sha256(raw).hexdigest()
    if actual != EXPECTED_WORKFLOW_SHA256:
        raise AssertionError("unexpected Stage 0 workflow digest: " + actual)


def _markdown_link_destinations(text):
    for match in re.finditer(
        r"\[[^\]]*\]\(\s*(?P<destination><[^>]*>|[^)\s]+)(?:\s+[^)]*)?\s*\)",
        text,
    ):
        destination = match.group("destination")
        if destination.startswith("<") and destination.endswith(">"):
            destination = destination[1:-1]
        path = unquote(urlsplit(destination).path).replace("\\", "/")
        yield path.rstrip("/").rsplit("/", 1)[-1]


def _is_stage0_hardware_script_candidate(path):
    normalized_path = path.replace("\\", "/")
    casefolded = normalized_path.casefold()
    if not casefolded.startswith("scripts/") or not casefolded.endswith(".ps1"):
        return False
    normalized = re.sub(r"[^a-z0-9]+", "", casefolded[:-4])
    has_hardware_identity = any(
        token in normalized for token in ("hardware", "intel", "runner")
    )
    has_operational_marker = (
        "inspection" in normalized
        or "inspect" in normalized
        or "intelrunner" in normalized
        or ("llm" in normalized and "fit" in normalized)
        or ("gate" in normalized and "1" in normalized)
        or re.search(r"stage[abcd]", normalized) is not None
        or "offline" in normalized
        or "candidate" in normalized
        or "acquire" in normalized
        or "acquisition" in normalized
        or "network" in normalized
        or "adapter" in normalized
    )
    return has_hardware_identity and has_operational_marker


def _is_stage0_gate1_runbook_candidate(path):
    normalized_path = path.replace("\\", "/")
    casefolded = normalized_path.casefold()
    if not casefolded.startswith("docs/testing/runbooks/") or not casefolded.endswith(".md"):
        return False
    normalized = re.sub(r"[^a-z0-9]+", "", casefolded)
    has_gate1_runbook_identity = (
        "gate" in normalized and "1" in normalized and "runbook" in normalized
    )
    hardware_inspection_group = (
        "hardware" in normalized
        and ("inspection" in normalized or "inspect" in normalized)
    )
    llm_fit_group = "llm" in normalized and "fit" in normalized
    return has_gate1_runbook_identity and (
        hardware_inspection_group or llm_fit_group
    )


def _is_development_acceptance_runbook_candidate(path):
    normalized_path = path.replace("\\", "/")
    casefolded = normalized_path.casefold()
    if not casefolded.startswith("docs/testing/runbooks/") or not casefolded.endswith(".md"):
        return False
    normalized = re.sub(r"[^a-z0-9]+", "", casefolded)
    return all(
        marker in normalized
        for marker in ("hardware", "inspection", "development", "acceptance", "runbook")
    )


def _stage0_inventory_paths(repository_root=REPOSITORY_ROOT):
    environment = {
        key: value
        for key, value in os.environ.items()
        if not key.casefold().startswith("git_")
    }
    result = subprocess.run(
        ["git", "-C", str(repository_root), "ls-files", "-z"],
        capture_output=True,
        timeout=20,
        check=False,
        env=environment,
    )
    if result.returncode != 0:
        raise AssertionError("Stage 0 inventory Git index query failed")
    try:
        tracked_paths = result.stdout.decode("utf-8", "strict").split("\0")
    except UnicodeDecodeError as error:
        raise AssertionError("Stage 0 inventory Git index is not UTF-8") from error
    if tracked_paths[-1:] != [""]:
        raise AssertionError("Stage 0 inventory Git index framing is invalid")
    tracked_paths.pop()
    candidate_paths = set(tracked_paths)
    try:
        for relative_root in (
            Path(".github") / "workflows",
            Path("scripts"),
            Path("docs") / "testing" / "runbooks",
        ):
            root = repository_root / relative_root
            if root.is_dir():
                for path in root.rglob("*"):
                    if path.is_file():
                        candidate_paths.add(path.relative_to(repository_root).as_posix())
                        if len(candidate_paths) > 100000:
                            raise AssertionError("Stage 0 inventory is unexpectedly large")
    except OSError as error:
        raise AssertionError("Stage 0 on-disk inventory query failed") from error
    paths = set()
    for relative in candidate_paths:
        normalized_path = relative.replace("\\", "/")
        casefolded = normalized_path.casefold()
        if casefolded.startswith(".github/workflows/"):
            paths.add(normalized_path)
        elif _is_stage0_hardware_script_candidate(normalized_path):
            paths.add(normalized_path)
        elif _is_stage0_gate1_runbook_candidate(normalized_path):
            paths.add(normalized_path)
        elif _is_development_acceptance_runbook_candidate(normalized_path):
            paths.add(normalized_path)
    return paths


def _is_stage0_workflow_alias(path):
    directory, separator, filename = path.replace("\\", "/").rpartition("/")
    if not separator or directory.casefold() != ".github/workflows":
        return False
    stem, separator, suffix = filename.rpartition(".")
    normalized = re.sub(r"[^a-z0-9]+", "", stem.casefold())
    return (
        bool(separator)
        and suffix.casefold() in ("yml", "yaml")
        and "hardwareinspection" in normalized
    )


def _assert_stage0_inventory(test_case, repository_paths):
    paths = set(repository_paths)
    workflow_paths = {path for path in paths if path.casefold().startswith(".github/workflows/")}
    test_case.assertEqual(workflow_paths, {
        ".github/workflows/build-and-test.yml", ".github/workflows/hardware-inspection-intel-runner-stage0.yml", ".github/workflows/hardware-inspection-intel-runner-stage-a.yml", ".github/workflows/hardware-inspection-llmfit-spike.yml", ".github/workflows/traceability-validation.yml", ".github/workflows/workbook-05-documented-build.yml", ".github/workflows/workbook-05-phase3-assets.yml", ".github/workflows/workbook-05-phase3-dependency-preflight.yml", ".github/workflows/workbook-05-preflight.yml", ".github/workflows/workbook-05-route-b-repair.yml", ".github/workflows/workbook-05-runner-smoke.yml", ".github/workflows/workbook-05-runtime-resume.yml", ".github/workflows/workbook-05-source-admission.yml",
    })
    stage_workflows = {
        path for path in paths if _is_stage0_workflow_alias(path)
    }
    test_case.assertEqual(
        stage_workflows,
        {
            ".github/workflows/hardware-inspection-intel-runner-stage0.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-a.yml",
            ".github/workflows/hardware-inspection-llmfit-spike.yml",
        },
    )
    hardware_scripts = {
        path
        for path in paths
        if _is_stage0_hardware_script_candidate(path)
    }
    test_case.assertEqual(
        hardware_scripts,
        {
            "scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1",
            "scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1",
            "scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1",
            "scripts/hardware-inspection/Acquire-HardwareInspectionLlmFitCandidate.ps1",
            "scripts/hardware-inspection/Capture-HardwareInspectionWindowsReference.ps1",
            "scripts/hardware-inspection/Write-HardwareInspectionLlmFitGate1Report.ps1",
            "scripts/hardware-inspection/New-LlamaCppProbeManifest.ps1",
            "scripts/hardware-inspection/Test-LlamaCppProbeManifest.ps1",
            "scripts/hardware-inspection/Invoke-SignedHardwareInspectionAcceptance.ps1",
            "scripts/hardware-inspection/Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1",
            "scripts/hardware-inspection/Test-HardwareInspectionGate9Summary.ps1",
        },
    )
    gate1_runbooks = {
        path
        for path in paths
        if _is_stage0_gate1_runbook_candidate(path)
    }
    test_case.assertEqual(gate1_runbooks, {
        "docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md",
    })
    development_acceptance_runbooks = {
        path
        for path in paths
        if _is_development_acceptance_runbook_candidate(path)
    }
    test_case.assertEqual(
        development_acceptance_runbooks,
        {
            "docs/testing/runbooks/Hardware-Inspection-Development-Acceptance-Runbook.md",
        },
    )
    for forbidden_path in (
        ".github/workflows/hardware-inspection-intel-runner-stage-b.yml",
        ".github/workflows/hardware-inspection-intel-runner-stage-d.yml",
        "scripts/hardware-inspection/Invoke-HardwareInspectionIntelOffline.ps1",
        "scripts/hardware-inspection/Disable-HardwareInspectionNetwork.ps1",
        "scripts/hardware-inspection/Enable-HardwareInspectionNetwork.ps1",
    ):
        test_case.assertNotIn(forbidden_path, paths)


def _assert_stage0_runbook_security(test_case, raw):
    if isinstance(raw, str):
        raw = raw.encode("utf-8")
    test_case.assertFalse(raw.startswith(codecs.BOM_UTF8))
    runbook_text = raw.decode("utf-8", "strict")
    normalized_runbook = " ".join(runbook_text.split())
    for required_phrase in (
        "Stage 0 is repository-only",
        "The UCL Intel laptop must remain disconnected from this stage",
        "Gate 1 remains Blocked",
        "Gate 2 must not start",
        "No LLM Fit candidate is acquired or executed",
        "future Stage A requires a separate approved plan",
        "The existing Workbook/TurboQuant runner must not be stopped, removed, relabelled, or contacted",
        "written UCL approval for the dedicated account, runner registration, repository and dependency execution, and evidence storage",
        "every repository writer must be UCL-authorised and trusted",
        "Actor, ref, label, environment, and approval-manifest checks are defence in depth, not substitutes",
    ):
        test_case.assertIn(required_phrase, normalized_runbook)
    gate1_path = "docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md"
    test_case.assertEqual(runbook_text.count(gate1_path), 1)
    for basename in _markdown_link_destinations(runbook_text):
        test_case.assertNotEqual(
            basename.casefold(),
            "hardware-inspection-llm-fit-gate-1-runbook.md",
        )

    # This canonical security/operator boundary changes only with a reviewed digest update.
    test_case.assertEqual(
        hashlib.sha256(raw).hexdigest(), EXPECTED_STAGE0_RUNBOOK_SHA256
    )

    parser_blocks = [
        block
        for block in re.findall(
            r"```powershell\n(.*?)\n```", runbook_text, flags=re.DOTALL
        )
        if "ParseFile" in block
    ]
    if parser_blocks:
        test_case.assertEqual(len(parser_blocks), 1)
        documented_parser_command = parser_blocks[0]
    else:
        legacy_parser_commands = [
            line
            for line in runbook_text.splitlines()
            if line.startswith("powershell.exe -NoProfile -Command ")
            and "ParseFile" in line
        ]
        test_case.assertEqual(len(legacy_parser_commands), 1)
        documented_parser_command = legacy_parser_commands[0]
    parser_result = subprocess.run(
        [
            powershell_executable(),
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            documented_parser_command,
        ],
        cwd=REPOSITORY_ROOT,
        text=True,
        capture_output=True,
        timeout=20,
        check=False,
    )
    test_case.assertEqual(
        parser_result.returncode,
        0,
        "documented PowerShell parser verification failed",
    )

    for heading in (
        "## Purpose",
        "## Authority",
        "## Preconditions",
        "## Future permission gates",
        "## Local contract verification",
        "## Manual hosted dispatch only",
        "## Expected result",
        "## Stop conditions",
        "## Deferred stages",
    ):
        test_case.assertIn(heading, runbook_text)
    for required_statement in (
        "Exactly one `hosted-preflight` job runs on `windows-latest`.",
        "The control checkout executes the validators and tests; the evaluated checkout is identity-only.",
        "No artifact is uploaded.",
        "Stage 0 generates, reserves, and consumes no runner label.",
        "Each future Stage A, B, and D gets a fresh one-time label under its separate approved plan.",
        "Stage C remains manual offline work without adapter automation.",
    ):
        test_case.assertIn(required_statement, runbook_text)
    test_case.assertIn(
        "gh workflow run hardware-inspection-intel-runner-stage0.yml `\n"
        "    --repo arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant `\n"
        "    --ref main `\n"
        "    -f confirm_repository_only=true",
        runbook_text,
    )
    for forbidden_claim in (
        r"\bGate 1 (?:has )?(?:passed|satisfied)\b",
        r"\bGate 2 (?:may|can) start\b",
        r"\blaptop (?:was|is) contacted\b",
        r"(?<!No )\bLLM Fit candidate (?:was|is) (?:acquired|executed)\b",
        r"\badapter (?:enable|disable) automation\b",
        r"(?<!No )\bartifact is uploaded\b",
        r"\bStage 0 (?:has |will )?(?:a )?self-hosted job\b",
    ):
        test_case.assertNotRegex(runbook_text, forbidden_claim)


class IntelRunnerStage0ContractTests(unittest.TestCase):
    def test_approval_manifest_accepts_only_exact_three_property_schema(self):
        self.assertTrue(MANIFEST_PATH.is_file(), "approval manifest is missing")
        raw = MANIFEST_PATH.read_bytes()
        self.assertLessEqual(len(raw), 4096)
        self.assertFalse(raw.startswith(codecs.BOM_UTF8))
        data = strict_json_object(raw.decode("utf-8", "strict"))
        self.assertEqual(list(data), ["schemaVersion", "remoteFeatureRef", "approvedTipSha"])
        self.assertEqual(data["schemaVersion"], "1.0")
        self.assertEqual(data["remoteFeatureRef"], "refs/heads/feature/hardware-inspection")
        self.assertRegex(data["approvedTipSha"], r"^(?!0{40}$)[0-9a-f]{40}$")

        with tempfile.TemporaryDirectory() as temporary_directory:
            control_root = Path(temporary_directory)
            invalid = [
                {"ref": "refs/heads/main"},
                {"sha": "CC2E57CEB94E73E49F34FC383D5440A9047FBA21"},
                {"sha": "0" * 40},
                {"extra": ',"computerName":"unsafe"'},
                {"extra": ',"schemaVersion":"1.0"'},
            ]
            for change in invalid:
                write_manifest(control_root, **change)
                result = run_validator(**valid_dispatch_parameters(control_root))
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(normalized(result.stderr), INVALID_STDERR)

    def test_approval_manifest_rejects_unsafe_ref_sha_and_dispatch_context(self):
        self.assertTrue(VALIDATOR_PATH.is_file(), "Stage 0 validator is missing")
        with tempfile.TemporaryDirectory() as temporary_directory:
            control_root = Path(temporary_directory)
            write_manifest(control_root)
            invalid_contexts = [
                {"Actor": "someone-else"},
                {"TriggeringActor": "someone-else"},
                {"WorkflowRef": "refs/heads/feature/hardware-inspection"},
                {"DefaultBranch": "trunk"},
                {"RunAttempt": "2"},
                {"ConfirmRepositoryOnly": "false"},
            ]
            for change in invalid_contexts:
                parameters = valid_dispatch_parameters(control_root)
                parameters.update(change)
                result = run_validator(**parameters)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(normalized(result.stderr), INVALID_STDERR)

            parameters = valid_dispatch_parameters(control_root)
            parameters["Phase"] = "TOP-SECRET-PHASE"
            result = run_validator(**parameters)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(normalized(result.stderr), INVALID_STDERR)
            self.assertNotIn("TOP-SECRET-PHASE", normalized(result.stdout))
            self.assertNotIn("TOP-SECRET-PHASE", normalized(result.stderr))
            self.assertNotIn(str(VALIDATOR_PATH), normalized(result.stdout))
            self.assertNotIn(str(VALIDATOR_PATH), normalized(result.stderr))

    def test_stage0_validator_accepts_only_matching_clean_source_identity(self):
        self.assertTrue(MANIFEST_PATH.is_file(), "approval manifest is missing")
        self.assertTrue(VALIDATOR_PATH.is_file(), "Stage 0 validator is missing")
        validator_text = VALIDATOR_PATH.read_text(encoding="utf-8")
        tracked_probe = "status --porcelain --untracked-files=no"
        untracked_probe = "ls-files --others --"
        self.assertEqual(validator_text.count(tracked_probe), 1)
        self.assertEqual(validator_text.count(untracked_probe), 1)
        self.assertLess(validator_text.index(tracked_probe), validator_text.index(untracked_probe))
        self.assertNotIn("status --porcelain --untracked-files=all", validator_text)
        self.assertNotIn("GIT_NO_LAZY_FETCH", validator_text)
        self.assertNotRegex(validator_text, r"ls-files --others[^\r\n]*--exclude")
        with tempfile.TemporaryDirectory() as temporary_directory:
            control_root = Path(temporary_directory) / "control"
            origin_root = Path(temporary_directory) / "origin"
            source_root = Path(temporary_directory) / "source"
            origin_root.mkdir()
            subprocess.run(["git", "init", "--quiet", str(origin_root)], check=True, timeout=20)
            subprocess.run(["git", "-C", str(origin_root), "config", "user.email", "test@example.invalid"], check=True, timeout=20)
            subprocess.run(["git", "-C", str(origin_root), "config", "user.name", "Contract Test"], check=True, timeout=20)
            subprocess.run(["git", "-C", str(origin_root), "config", "core.autocrlf", "false"], check=True, timeout=20)
            subprocess.run(["git", "-C", str(origin_root), "config", "uploadpack.allowFilter", "true"], check=True, timeout=20)
            (origin_root / ".gitignore").write_text("ignored-only.txt\n", encoding="utf-8", newline="\n")
            identity_path = origin_root / "docs" / "superpowers" / "specs" / "identity.md"
            identity_path.parent.mkdir(parents=True)
            identity_path.write_text("approved identity\n", encoding="utf-8", newline="\n")
            subprocess.run(["git", "-C", str(origin_root), "add", ".gitignore", "docs/superpowers/specs/identity.md"], check=True, timeout=20)
            subprocess.run(["git", "-C", str(origin_root), "commit", "--quiet", "-m", "identity"], check=True, timeout=20)
            source_sha = subprocess.run(["git", "-C", str(origin_root), "rev-parse", "HEAD"], text=True, capture_output=True, check=True, timeout=20).stdout.strip()
            identity_oid = subprocess.run(["git", "-C", str(origin_root), "rev-parse", "HEAD:docs/superpowers/specs/identity.md"], text=True, capture_output=True, check=True, timeout=20).stdout.strip()
            gitignore_oid = subprocess.run(["git", "-C", str(origin_root), "rev-parse", "HEAD:.gitignore"], text=True, capture_output=True, check=True, timeout=20).stdout.strip()

            subprocess.run(
                ["git", "clone", "--quiet", "--filter=blob:none", "--no-checkout", origin_root.as_uri(), str(source_root)],
                check=True,
                timeout=20,
            )
            seeded_identity_oid = subprocess.run(
                ["git", "-C", str(source_root), "hash-object", "-w", str(identity_path)],
                text=True,
                capture_output=True,
                timeout=20,
                check=True,
            ).stdout.strip()
            self.assertEqual(seeded_identity_oid, identity_oid)
            pre_checkout_missing = subprocess.run(
                ["git", "-C", str(source_root), "rev-list", "--objects", "--missing=print", source_sha],
                text=True,
                capture_output=True,
                timeout=20,
                check=True,
            )
            self.assertNotIn("?" + identity_oid, pre_checkout_missing.stdout.splitlines())
            self.assertIn("?" + gitignore_oid, pre_checkout_missing.stdout.splitlines())
            subprocess.run(["git", "-C", str(source_root), "sparse-checkout", "init", "--no-cone"], check=True, timeout=20)
            subprocess.run(
                ["git", "-C", str(source_root), "sparse-checkout", "set", "--no-cone", "/docs/superpowers/specs/"],
                check=True,
                timeout=20,
            )
            subprocess.run(["git", "-C", str(source_root), "checkout", "--quiet", "--detach", source_sha], check=True, timeout=20)
            self.assertFalse((source_root / ".gitignore").exists())
            self.assertTrue((source_root / "docs" / "superpowers" / "specs" / "identity.md").is_file())

            missing_objects = subprocess.run(
                ["git", "-C", str(source_root), "rev-list", "--objects", "--missing=print", source_sha],
                text=True,
                capture_output=True,
                timeout=20,
                check=True,
            )
            self.assertIn("?" + gitignore_oid, missing_objects.stdout.splitlines())
            origin_root.rename(Path(temporary_directory) / "unreachable-origin")
            old_status = subprocess.run(
                ["git", "-C", str(source_root), "status", "--porcelain", "--untracked-files=all"],
                text=True,
                capture_output=True,
                timeout=20,
                check=False,
            )
            self.assertNotEqual(old_status.returncode, 0)
            write_manifest(control_root, sha=source_sha)

            dispatch = run_validator(**valid_dispatch_parameters(control_root))
            self.assertEqual(dispatch.returncode, 0, normalized(dispatch.stderr))
            self.assertEqual(
                normalized(dispatch.stdout),
                "source_ref=refs/heads/feature/hardware-inspection\n"
                + "approved_sha=" + source_sha + "\nrepository_only=true\n",
            )

            source_parameters = valid_dispatch_parameters(control_root)
            source_parameters.update({"Phase": "Source", "SourceCheckoutRoot": source_root})
            source = run_validator(**source_parameters)
            self.assertEqual(source.returncode, 0, normalized(source.stderr))

            source_identity_path = source_root / "docs" / "superpowers" / "specs" / "identity.md"
            source_identity_path.write_text("modified identity\n", encoding="utf-8", newline="\n")
            tracked_dirty = run_validator(**source_parameters)
            self.assertNotEqual(tracked_dirty.returncode, 0)
            self.assertEqual(normalized(tracked_dirty.stderr), INVALID_STDERR)
            subprocess.run(
                ["git", "-C", str(source_root), "restore", "--worktree", "docs/superpowers/specs/identity.md"],
                check=True,
                timeout=20,
            )

            untracked_path = source_root / "docs" / "superpowers" / "specs" / "untracked.md"
            untracked_path.write_text("unsafe\n", encoding="utf-8", newline="\n")
            dirty = run_validator(**source_parameters)
            self.assertNotEqual(dirty.returncode, 0)
            self.assertEqual(normalized(dirty.stderr), INVALID_STDERR)

            untracked_path.unlink()
            ignored_path = source_root / "ignored-only.txt"
            ignored_path.write_text("unsafe even when ignored\n", encoding="utf-8", newline="\n")
            ignored_dirty = run_validator(**source_parameters)
            self.assertNotEqual(ignored_dirty.returncode, 0)
            self.assertEqual(normalized(ignored_dirty.stderr), INVALID_STDERR)

            ignored_path.unlink()
            write_manifest(control_root, sha="d" * 40)
            mismatched = run_validator(**source_parameters)
            self.assertNotEqual(mismatched.returncode, 0)
            self.assertEqual(normalized(mismatched.stderr), INVALID_STDERR)

            self.assertEqual(
                normalized(source.stdout),
                "# Hardware Inspection Intel runner preflight\n\n"
                "- Stage 0 only.\n"
                "- The Intel laptop was not contacted.\n"
                "- The LLM Fit candidate was not acquired or executed.\n"
                "- Gate 1 remains Blocked.\n"
                "- Gate 2 is prohibited.\n"
                "- Approved source ref: refs/heads/feature/hardware-inspection\n"
                "- Approved source SHA: " + source_sha + "\n",
            )

    def test_stage0_workflow_exposes_only_manual_trigger_and_hosted_runner(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8")
        pre_permissions = text.split("\npermissions:\n", 1)[0]
        self.assertEqual(
            pre_permissions,
            """name: Hardware Inspection Intel runner Stage 0

on:
  workflow_dispatch:
    inputs:
      confirm_repository_only:
        description: Confirm this run is repository-only and will not contact the Intel laptop
        required: true
        default: false
        type: boolean
""",
        )
        job_headers = [
            line
            for line in text.split("\njobs:\n", 1)[1].splitlines()
            if line.startswith("  ") and not line.startswith("    ") and line.endswith(":")
        ]
        self.assertEqual(job_headers, ["  hosted-preflight:"])
        self.assertEqual(text.count("runs-on: windows-latest"), 1)
        self.assertNotIn("self-hosted", text)

        guard = b"""    if: >-
      github.event_name == 'workflow_dispatch' &&
      github.ref == format('refs/heads/{0}', github.event.repository.default_branch) &&
      github.actor == github.repository_owner &&
      github.triggering_actor == github.repository_owner &&
      github.run_attempt == 1 &&
      inputs.confirm_repository_only == true
"""
        mutations = (
            raw + b"# comment-only mutation\n",
            raw.replace(b"runs-on: windows-latest", b"'runs-on': windows-latest", 1),
            raw.replace(b"working-directory: control", b"working-directory: evaluated", 1),
            raw.replace(b".\\control\\scripts", b".\\evaluated\\scripts", 1),
            raw.replace(
                b"      - name: Set up Python for repository contracts\n",
                b"      - uses: actions/cache@deadbeef\n\n      - name: Set up Python for repository contracts\n",
                1,
            ),
            raw.replace(
                b"\njobs:\n",
                b"\njobs:\n  contact-runner:\n    runs-on: windows-latest\n    steps:\n      - run: Get-ComputerInfo\n",
                1,
            ),
            raw.replace(
                guard,
                b"""    # guard expressions moved to comments
    # github.event_name == 'workflow_dispatch'
    if: true
""",
                1,
            ),
            raw.replace(b"ref: ${{ github.sha }}", b"ref: feature/hardware-inspection", 1),
            raw.replace(b"on:\n", b"on:\n  pull_request_target:\n  workflow_run:\n", 1),
            raw.replace(b"$minor -lt 28", b"$minor -lt 27", 1),
            raw.replace(b" | Select-Object -First 1", b"", 1),
        )
        self.assertEqual(len(mutations), 11)
        for mutation in mutations:
            self.assertNotEqual(hashlib.sha256(mutation).hexdigest(), EXPECTED_WORKFLOW_SHA256)
            with self.assertRaises(AssertionError):
                _assert_canonical_workflow(mutation)

    def test_stage0_workflow_uses_read_only_permissions_owner_and_default_guards(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8")
        self.assertIn("permissions:\n  contents: read\n", text)
        self.assertEqual(text.count("permissions:"), 1)
        self.assertNotIn("contents: write", text)
        self.assertIn(
            """    if: >-
      github.event_name == 'workflow_dispatch' &&
      github.ref == format('refs/heads/{0}', github.event.repository.default_branch) &&
      github.actor == github.repository_owner &&
      github.triggering_actor == github.repository_owner &&
      github.run_attempt == 1 &&
      inputs.confirm_repository_only == true
""",
            text,
        )
        self.assertIn("    timeout-minutes: 10\n", text)
        self.assertIn(
            "concurrency:\n  group: hardware-inspection-intel-runner-stage0\n  cancel-in-progress: false\n",
            text,
        )

    def test_stage0_workflow_pins_actions_and_drops_checkout_credentials(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8")
        expected_git_precheck = """      - name: Require sparse-checkout-capable Git
        shell: powershell
        run: |
          $ProgressPreference = 'SilentlyContinue'
          $ErrorActionPreference = 'Stop'
          $failure = 'HI-RUNNER-STAGE0-GIT-INVALID: required Git capability is unavailable.'
          try {
            $gitCommand = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($null -eq $gitCommand) {
              throw 'invalid'
            }
            $versionLines = @(& $gitCommand.Source --version 2>$null)
            if ($LASTEXITCODE -ne 0 -or $versionLines.Count -ne 1) {
              throw 'invalid'
            }
            $versionText = [string]$versionLines[0]
            if ($versionText -cnotmatch '\\Agit version (?<major>0|[1-9][0-9]*)\\.(?<minor>0|[1-9][0-9]*)\\.(?:0|[1-9][0-9]*)(?:\\.windows\\.(?:0|[1-9][0-9]*))?\\z') {
              throw 'invalid'
            }
            $major = [int]$Matches['major']
            $minor = [int]$Matches['minor']
            if ($major -lt 2 -or ($major -eq 2 -and $minor -lt 28)) {
              throw 'invalid'
            }
          }
          catch {
            [Console]::Error.WriteLine($failure)
            exit 1
          }
"""
        self.assertEqual(text.count("      - name: Require sparse-checkout-capable Git\n"), 1)
        precheck_start = text.index("      - name: Require sparse-checkout-capable Git\n")
        control_step_start = text.index("      - name: Check out default-branch controls\n")
        separator_start = text.index("\n\n      - name: Check out default-branch controls\n", precheck_start)
        precheck_step = text[precheck_start:separator_start + 1]
        self.assertNotIn("${{", precheck_step)
        self.assertNotRegex(precheck_step, r"(?i)candidate|invoke-webrequest|start-bitstransfer|curl|wget|netsh|git (?:clone|fetch|checkout|push)")
        self.assertNotRegex(precheck_step, r"(?<![A-Za-z0-9])[A-Za-z]:[\\/]")
        run_body = _workflow_run_body(precheck_step)

        for version in ("2.28.0", "2.51.0", "2.51.0.windows.2", "3.0.0"):
            result = _run_git_precheck(run_body, output="git version " + version)
            self.assertEqual(result.returncode, 0, normalized(result.stderr))
            self.assertEqual(normalized(result.stdout), "")
            self.assertEqual(normalized(result.stderr), "")

        first_application = _run_git_precheck(
            run_body,
            output="git version 2.55.0.windows.3",
            secondary_output="git version 2.27.99",
        )
        self.assertEqual(first_application.returncode, 0, normalized(first_application.stderr))
        self.assertEqual(normalized(first_application.stdout), "")
        self.assertEqual(normalized(first_application.stderr), "")

        invalid_first_application = _run_git_precheck(
            run_body,
            output="git version 2.27.99",
            secondary_output="git version 2.55.0.windows.3",
        )
        self.assertEqual(invalid_first_application.returncode, 1)
        self.assertEqual(normalized(invalid_first_application.stdout), "")
        self.assertEqual(normalized(invalid_first_application.stderr), GIT_INVALID_STDERR)

        for output in (
            "git version 2.27.99",
            "git version 2.51",
            "Git version 2.51.0",
            "git version 2.51.0\nmalformed-extra-line",
        ):
            result = _run_git_precheck(run_body, output=output)
            self.assertEqual(result.returncode, 1)
            self.assertEqual(normalized(result.stdout), "")
            self.assertEqual(normalized(result.stderr), GIT_INVALID_STDERR)

        for mode in ("nonzero", "throwing", "missing"):
            result = _run_git_precheck(run_body, mode=mode)
            self.assertEqual(result.returncode, 1)
            self.assertEqual(normalized(result.stdout), "")
            self.assertEqual(normalized(result.stderr), GIT_INVALID_STDERR)

        secret = r"C:\private\SECRET_TOKEN"
        result = _run_git_precheck(run_body, source=secret)
        self.assertEqual(result.returncode, 1)
        self.assertEqual(normalized(result.stdout), "")
        self.assertEqual(normalized(result.stderr), GIT_INVALID_STDERR)
        self.assertNotIn(secret, normalized(result.stdout) + normalized(result.stderr))

        self.assertEqual(precheck_step, expected_git_precheck)

        checkout = "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0"
        setup_python = "actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065"
        uses = [line.strip() for line in text.splitlines() if line.strip().startswith("uses:")]
        self.assertEqual(uses, ["uses: " + checkout, "uses: " + setup_python, "uses: " + checkout])
        self.assertIn("          python-version: '3.12.10'\n", text)
        self.assertEqual(text.count("persist-credentials: false"), 2)
        self.assertIn("          ref: ${{ github.sha }}\n          path: control", text)
        control_sparse_checkout = """          sparse-checkout: |
            .github/hardware-inspection
            .github/workflows
            docs/superpowers/specs
            docs/testing/runbooks
            scripts/hardware-inspection
            tests/testing/hardware_inspection
          sparse-checkout-cone-mode: true
"""
        evaluated_sparse_checkout = """          sparse-checkout: |
            /docs/superpowers/specs/
          sparse-checkout-cone-mode: false
"""

        self.assertEqual(text.count("          sparse-checkout: |\n"), 2)
        self.assertEqual(text.count("          sparse-checkout-cone-mode: true\n"), 1)
        self.assertEqual(text.count("          sparse-checkout-cone-mode: false\n"), 1)
        self.assertIn(control_sparse_checkout, text)
        self.assertIn(evaluated_sparse_checkout, text)
        self.assertNotIn("          filter:", text)
        self.assertNotIn("core.longpaths", text.casefold())

        control_start = text.index("      - name: Check out default-branch controls\n")
        control_end = text.index("      - name: Set up Python for repository contracts\n", control_start)
        evaluated_start = text.index("      - name: Check out approved source for identity comparison only\n")
        evaluated_end = text.index("      - name: Confirm approved source identity and publish safe summary\n", evaluated_start)
        control_step = text[control_start:control_end]
        evaluated_step = text[evaluated_start:evaluated_end]
        self.assertIn(control_sparse_checkout, control_step)
        self.assertNotIn(evaluated_sparse_checkout, control_step)
        self.assertIn(evaluated_sparse_checkout, evaluated_step)
        self.assertNotIn(control_sparse_checkout, evaluated_step)
        self.assertIn("          sparse-checkout-cone-mode: true\n", control_step)
        self.assertNotIn("          sparse-checkout-cone-mode: false\n", control_step)
        self.assertIn("          sparse-checkout-cone-mode: false\n", evaluated_step)
        self.assertNotIn("          sparse-checkout-cone-mode: true\n", evaluated_step)

    def test_stage0_workflow_reads_approved_source_without_free_form_sha_input(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8")
        self.assertIn("    inputs:\n      confirm_repository_only:\n", text)
        self.assertEqual(text.count("      confirm_repository_only:\n"), 1)
        self.assertNotIn("runner_label", text)
        self.assertNotIn("source_sha", text)
        self.assertNotIn("source_ref:", text)
        self.assertNotIn("input_sha", text)
        self.assertIn("          ref: ${{ steps.approval.outputs.approved_sha }}\n", text)
        self.assertNotIn("          ref: ${{ steps.approval.outputs.source_ref }}\n", text)
        self.assertIn("          path: evaluated\n", text)

    def test_stage0_workflow_executes_validators_only_from_control_checkout(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8")
        self.assertEqual(text.count("          path: control"), 1)
        self.assertEqual(text.count("          path: evaluated"), 1)
        self.assertEqual(text.count("Validate-HardwareInspectionIntelRunnerStage0.ps1"), 2)
        self.assertGreaterEqual(text.count(".\\control\\scripts\\hardware-inspection\\Validate-HardwareInspectionIntelRunnerStage0.ps1"), 2)
        self.assertNotIn(".\\evaluated\\scripts", text)
        steps_start = text.index("    steps:\n")
        self.assertEqual(text.count("      - name: Require sparse-checkout-capable Git\n"), 1)
        precheck_start = text.index("      - name: Require sparse-checkout-capable Git\n", steps_start)
        control_checkout_start = text.index("      - name: Check out default-branch controls\n", precheck_start)
        self.assertLess(precheck_start, control_checkout_start)
        self.assertNotIn("uses:", text[steps_start:precheck_start])
        self.assertEqual(text[steps_start:precheck_start], "    steps:\n")
        self.assertLess(text.index("path: control"), text.index("Run Stage 0 contracts"))
        self.assertLess(text.index("Run Stage 0 contracts"), text.index("Validate dispatch and approval manifest"))
        self.assertLess(text.index("Validate dispatch and approval manifest"), text.index("path: evaluated"))
        self.assertLess(text.index("path: evaluated"), text.index("Confirm approved source identity"))

    def test_stage0_workflow_has_no_self_hosted_registration_or_service_path(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8").lower()
        for forbidden in (
            "self-hosted",
            "config.cmd",
            "config.sh",
            "run.cmd",
            "run.sh",
            "svc install",
            "svc.cmd",
            "service install",
            "ephemeral",
            "get-computerinfo",
        ):
            self.assertNotIn(forbidden, text)

    def test_stage0_workflow_has_no_candidate_capture_report_or_offline_path(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8").lower()
        for forbidden in (
            "candidate",
            "capture",
            "report",
            "trusted tests",
            "llmfit",
            "hardware artifact",
            "dotnet restore",
            "dotnet build",
            "dotnet test",
            "adapter",
            "netsh",
            "offline",
        ):
            self.assertNotIn(forbidden, text)

    def test_stage0_workflow_has_no_raw_upload_or_operational_environment(self):
        raw = _workflow()
        _assert_canonical_workflow(raw)
        text = raw.decode("utf-8").lower()
        text_without_fixed_precheck = text.replace(
            "          $erroractionpreference = 'stop'\n",
            "",
        ).replace(
            "          $progresspreference = 'silentlycontinue'\n",
            "",
        )
        for forbidden in (
            "actions/upload-artifact",
            "upload-artifact",
            ".trx",
            "reference",
            "raw",
            "evidence",
            "granite",
            "secrets.",
            "contents: write",
            "id-token:",
            "environment:",
        ):
            self.assertNotIn(forbidden, text_without_fixed_precheck)

    def test_stage0_inventory_contains_only_approved_repository_controls(self):
        runbook_path = (
            REPOSITORY_ROOT
            / "docs"
            / "testing"
            / "runbooks"
            / "Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md"
        )
        design_path = (
            REPOSITORY_ROOT
            / "docs"
            / "superpowers"
            / "specs"
            / "2026-08-18-hardware-inspection-intel-runner-configuration-design.md"
        )
        scripts_readme_path = REPOSITORY_ROOT / "scripts" / "README.md"
        self.assertTrue(runbook_path.is_file(), "Stage 0 runbook is missing")
        self.assertTrue(design_path.is_file(), "Stage 0 design copy is missing")
        self.assertEqual(
            hashlib.sha256(design_path.read_bytes()).hexdigest(),
            "44916a51c7d4e1856b73564c8ce23e9b4ee60b20067c67a2edeff8c482e13cfe",
        )

        scripts_readme = scripts_readme_path.read_text(encoding="utf-8")
        self.assertIn("Validate-HardwareInspectionIntelRunnerStage0.ps1", scripts_readme)
        self.assertIn("Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md", scripts_readme)

        inventory_paths = _stage0_inventory_paths()
        _assert_stage0_inventory(self, inventory_paths)
        prior_git_dir = os.environ.get("GIT_DIR")
        os.environ["GIT_DIR"] = str(REPOSITORY_ROOT / "private-canary-invalid-git-dir")
        try:
            self.assertEqual(_stage0_inventory_paths(), inventory_paths)
        finally:
            if prior_git_dir is None:
                os.environ.pop("GIT_DIR", None)
            else:
                os.environ["GIT_DIR"] = prior_git_dir

        with tempfile.TemporaryDirectory() as temporary_directory:
            collector_root = Path(temporary_directory)
            script_alias = (
                collector_root
                / "scripts"
                / "hardware_inspection"
                / "Invoke-HardwareInspectionIntelOffline.ps1"
            )
            runbook_alias = (
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "hardware_inspection_llm_fit_gate_1_runbook.md"
            )
            runbook_copy_alias = (
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "Hardware-Inspection-LLM-Fit-Gate-1-Runbook-copy.md"
            )
            inserted_script_alias = (
                collector_root
                / "scripts"
                / "hardware-intel-inspection"
                / "Invoke-IntelRunnerStageA.ps1"
            )
            inserted_runbook_alias = (
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "Hardware-Inspection-Intel-LLM-Fit-Gate-1-Runbook.md"
            )
            selector_bypass_script_aliases = (
                collector_root
                / "scripts"
                / "hardware-inspect"
                / "Invoke-IntelRunnerStageB.ps1",
                collector_root
                / "scripts"
                / "intel-tools"
                / "Invoke-Runner-LLM-Secure-Fit.ps1",
                collector_root
                / "scripts"
                / "runner-tools"
                / "Invoke-Gate-Hardware-Review-1.ps1",
                collector_root
                / "scripts"
                / "hardware-tools"
                / "Invoke-Offline-Intel.ps1",
                collector_root
                / "scripts"
                / "runner-tools"
                / "Invoke-Intel-Hardware-Stage-C.ps1",
                collector_root
                / "scripts"
                / "hardware-tools"
                / "Invoke-Candidate.ps1",
                collector_root
                / "scripts"
                / "hardware-tools"
                / "Enable-NetworkAdapter.ps1",
                collector_root
                / "scripts"
                / "intel-tools"
                / "Invoke-HardwareAcquisition.ps1",
            )
            selector_bypass_runbook_aliases = (
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "Hardware-Inspection-Gate-1-Runbook.md",
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "Gate-Runbook-Hardware-1-Inspection.md",
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "LLM-Fit-Gate-1-Runbook.md",
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "Runbook-Gate-Review-LLM-1-Fit.md",
            )
            selector_noise_aliases = (
                collector_root
                / "scripts"
                / "hardware-tools"
                / "Invoke-Gate-Intel.ps1",
                collector_root / "scripts" / "misc" / "Invoke-StageB.ps1",
                collector_root
                / "scripts"
                / "hardware-tools"
                / "Invoke-Offline-Intel.txt",
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "Hardware-Inspection-Runbook.md",
                collector_root
                / "docs"
                / "testing"
                / "runbooks"
                / "LLM-Fit-Runbook.md",
            )
            script_alias.parent.mkdir(parents=True)
            runbook_alias.parent.mkdir(parents=True)
            inserted_script_alias.parent.mkdir(parents=True)
            script_alias.write_text("unsafe\n", encoding="utf-8", newline="\n")
            runbook_alias.write_text("unsafe\n", encoding="utf-8", newline="\n")
            runbook_copy_alias.write_text("unsafe\n", encoding="utf-8", newline="\n")
            inserted_script_alias.write_text("unsafe\n", encoding="utf-8", newline="\n")
            inserted_runbook_alias.write_text("unsafe\n", encoding="utf-8", newline="\n")
            for alias in (
                selector_bypass_script_aliases
                + selector_bypass_runbook_aliases
                + selector_noise_aliases
            ):
                alias.parent.mkdir(parents=True, exist_ok=True)
                alias.write_text("unsafe\n", encoding="utf-8", newline="\n")
            subprocess.run(
                ["git", "-C", str(collector_root), "init", "--quiet"],
                check=True,
                capture_output=True,
                timeout=20,
            )
            subprocess.run(
                ["git", "-C", str(collector_root), "add", "--", "scripts", "docs"],
                check=True,
                capture_output=True,
                timeout=20,
            )
            untracked_workflow = (
                collector_root
                / ".github"
                / "workflows"
                / "hardware-inspection-intel-untracked.yml"
            )
            untracked_workflow.parent.mkdir(parents=True)
            untracked_workflow.write_text("on: workflow_dispatch\n", encoding="utf-8", newline="\n")
            self.assertEqual(
                _stage0_inventory_paths(collector_root),
                {
                    "scripts/hardware_inspection/Invoke-HardwareInspectionIntelOffline.ps1",
                    "docs/testing/runbooks/hardware_inspection_llm_fit_gate_1_runbook.md",
                    "docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook-copy.md",
                    "scripts/hardware-intel-inspection/Invoke-IntelRunnerStageA.ps1",
                    "docs/testing/runbooks/Hardware-Inspection-Intel-LLM-Fit-Gate-1-Runbook.md",
                    "scripts/hardware-inspect/Invoke-IntelRunnerStageB.ps1",
                    "scripts/intel-tools/Invoke-Runner-LLM-Secure-Fit.ps1",
                    "scripts/runner-tools/Invoke-Gate-Hardware-Review-1.ps1",
                    "scripts/hardware-tools/Invoke-Offline-Intel.ps1",
                    "scripts/runner-tools/Invoke-Intel-Hardware-Stage-C.ps1",
                    "scripts/hardware-tools/Invoke-Candidate.ps1",
                    "scripts/hardware-tools/Enable-NetworkAdapter.ps1",
                    "scripts/intel-tools/Invoke-HardwareAcquisition.ps1",
                    "docs/testing/runbooks/Hardware-Inspection-Gate-1-Runbook.md",
                    "docs/testing/runbooks/Gate-Runbook-Hardware-1-Inspection.md",
                    "docs/testing/runbooks/LLM-Fit-Gate-1-Runbook.md",
                    "docs/testing/runbooks/Runbook-Gate-Review-LLM-1-Fit.md",
                    ".github/workflows/hardware-inspection-intel-untracked.yml",
                },
            )
        with tempfile.TemporaryDirectory() as temporary_directory:
            sparse_root = Path(temporary_directory)
            visible_workflow = sparse_root / ".github" / "workflows" / "visible.yml"
            hidden_script = (
                sparse_root
                / "scripts"
                / "hardware-inspect"
                / "Invoke-IntelRunnerStageB.ps1"
            )
            hidden_runbook = (
                sparse_root
                / "docs"
                / "testing"
                / "runbooks"
                / "Hardware-Inspection-Gate-1-Runbook.md"
            )
            visible_workflow.parent.mkdir(parents=True)
            hidden_script.parent.mkdir(parents=True)
            hidden_runbook.parent.mkdir(parents=True)
            visible_workflow.write_text("on: workflow_dispatch\n", encoding="utf-8", newline="\n")
            hidden_script.write_text("unsafe\n", encoding="utf-8", newline="\n")
            hidden_runbook.write_text("unsafe\n", encoding="utf-8", newline="\n")
            for arguments in (
                ("init", "--quiet"),
                ("config", "user.email", "stage0@example.invalid"),
                ("config", "user.name", "Stage 0"),
                ("add", "--", ".github", "scripts", "docs"),
                ("commit", "--quiet", "-m", "inventory fixture"),
                ("sparse-checkout", "init", "--no-cone"),
                ("sparse-checkout", "set", "--no-cone", "/.github/workflows/"),
            ):
                subprocess.run(
                    ["git", "-C", str(sparse_root), *arguments],
                    check=True,
                    capture_output=True,
                    timeout=20,
                )
            self.assertTrue(visible_workflow.is_file())
            self.assertFalse(hidden_script.exists())
            self.assertFalse(hidden_runbook.exists())
            self.assertEqual(
                _stage0_inventory_paths(sparse_root),
                {
                    ".github/workflows/visible.yml",
                    "scripts/hardware-inspect/Invoke-IntelRunnerStageB.ps1",
                    "docs/testing/runbooks/Hardware-Inspection-Gate-1-Runbook.md",
                },
            )
        with tempfile.TemporaryDirectory() as temporary_directory:
            with self.assertRaises(AssertionError):
                _stage0_inventory_paths(Path(temporary_directory))

        runbook_raw = runbook_path.read_bytes()
        runbook_text = runbook_raw.decode("utf-8", "strict")
        _assert_stage0_runbook_security(self, runbook_raw)

        with self.subTest(command="committed range"):
            self.assertIn("git diff --check origin/main...HEAD", runbook_text)

        readme_links = re.findall(
            r"\[`Hardware-Inspection-Intel-Runner-Stage-0-Runbook\.md`\]\(([^)]+)\)",
            scripts_readme,
        )
        self.assertEqual(
            readme_links,
            ["../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md"],
        )
        self.assertEqual(
            (scripts_readme_path.parent / readme_links[0]).resolve(),
            runbook_path.resolve(),
        )
        self.assertTrue(runbook_path.is_file())
        for mutation in (
            b"\nStage 0 reserves hardware-gate1-deadbeefdeadbeef.\n",
            b"\nStages A, B, and D reuse the same label.\n",
            b"\nGate 2 is allowed to start.\n",
            b"\nThe laptop will be contacted.\n",
            b"\n[Gate 1 runbook](Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md)\n",
        ):
            with self.subTest(mutation=mutation.decode("utf-8").strip()):
                with self.assertRaises(AssertionError):
                    _assert_stage0_runbook_security(self, runbook_raw + mutation)
        for mutation in (
            ".github/workflows/hardware-inspection-intel-runner-stage-a.yaml",
            ".github/workflows/Hardware-Inspection-Intel-Runner-Stage-A.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-a-copy.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-a-alternate.yml",
            ".github/workflows/hardware-inspection-stage-a.yml",
            ".github/workflows/hardware_inspection_intel_runner_stage_a.yml",
            ".github/workflows/hardware-inspection-intel-runner-phase-b.yml",
            ".github/workflows/intel-hardware-inspection-offline.yml",
            ".github/workflows/unrelated-new-name.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-b.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-c.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-d.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-x.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-c.yaml",
            ".github/workflows/Hardware-Inspection-Intel-Runner-Stage-C.yml",
            "scripts/hardware-inspection/Start-HardwareInspectionCandidate.ps1",
            "scripts/hardware-inspection/Invoke-HardwareInspectionIntelOffline.ps1",
            "scripts/hardware-inspection/Disable-HardwareInspectionNetwork.ps1",
            "scripts/hardware-inspection/Enable-HardwareInspectionNetwork.ps1",
            "scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA-copy.ps1",
            "scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA-alternate.ps1",
            "scripts/hardware-inspect/Invoke-IntelRunnerStageB.ps1",
            "scripts/intel-tools/Invoke-Runner-LLM-Secure-Fit.ps1",
            "scripts/runner-tools/Invoke-Gate-Hardware-Review-1.ps1",
            "scripts/hardware-tools/Invoke-Offline-Intel.ps1",
            "scripts/runner-tools/Invoke-Intel-Hardware-Stage-C.ps1",
            "docs/testing/runbooks/hardware_inspection_llm_fit_gate_1_runbook.md",
            "docs/testing/runbooks/Hardware-Inspection-Gate-1-Runbook.md",
            "docs/testing/runbooks/Gate-Runbook-Hardware-1-Inspection.md",
            "docs/testing/runbooks/LLM-Fit-Gate-1-Runbook.md",
            "docs/testing/runbooks/Runbook-Gate-Review-LLM-1-Fit.md",
            "docs/testing/runbooks/hardware_inspection_development_acceptance_runbook.md",
            "docs/testing/runbooks/Hardware-Inspection-Development-Acceptance-Runbook-copy.md",
        ):
            with self.subTest(inventory_mutation=mutation):
                with self.assertRaises(AssertionError):
                    _assert_stage0_inventory(self, inventory_paths | {mutation})


if __name__ == "__main__":
    unittest.main()
