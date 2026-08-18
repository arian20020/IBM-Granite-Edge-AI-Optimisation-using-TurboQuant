import codecs
import hashlib
import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


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
EXPECTED_WORKFLOW_SHA256 = "9f14750368eef1a105332ccb54cd513ca92fd4af91dd8542efe5ededff304309"
INVALID_STDERR = "HI-RUNNER-STAGE0-INVALID: repository-only validation failed.\n"


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
        with tempfile.TemporaryDirectory() as temporary_directory:
            control_root = Path(temporary_directory) / "control"
            source_root = Path(temporary_directory) / "source"
            source_root.mkdir()
            subprocess.run(["git", "init", "--quiet", str(source_root)], check=True, timeout=20)
            subprocess.run(["git", "-C", str(source_root), "config", "user.email", "test@example.invalid"], check=True, timeout=20)
            subprocess.run(["git", "-C", str(source_root), "config", "user.name", "Contract Test"], check=True, timeout=20)
            (source_root / "identity.txt").write_text("approved identity\n", encoding="utf-8")
            subprocess.run(["git", "-C", str(source_root), "add", "identity.txt"], check=True, timeout=20)
            subprocess.run(["git", "-C", str(source_root), "commit", "--quiet", "-m", "identity"], check=True, timeout=20)
            source_sha = subprocess.run(["git", "-C", str(source_root), "rev-parse", "HEAD"], text=True, capture_output=True, check=True, timeout=20).stdout.strip()
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

            (source_root / "identity.txt").write_text("modified identity\n", encoding="utf-8")
            tracked_dirty = run_validator(**source_parameters)
            self.assertNotEqual(tracked_dirty.returncode, 0)
            self.assertEqual(normalized(tracked_dirty.stderr), INVALID_STDERR)
            subprocess.run(["git", "-C", str(source_root), "restore", "--worktree", "identity.txt"], check=True, timeout=20)

            (source_root / "untracked.txt").write_text("unsafe\n", encoding="utf-8")
            dirty = run_validator(**source_parameters)
            self.assertNotEqual(dirty.returncode, 0)
            self.assertEqual(normalized(dirty.stderr), INVALID_STDERR)

            (source_root / "untracked.txt").unlink()
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
        )
        self.assertEqual(len(mutations), 9)
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
        checkout = "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0"
        setup_python = "actions/setup-python@a26af69be951a213d495a4c3e4e4022e16d87065"
        uses = [line.strip() for line in text.splitlines() if line.strip().startswith("uses:")]
        self.assertEqual(uses, ["uses: " + checkout, "uses: " + setup_python, "uses: " + checkout])
        self.assertIn("          python-version: '3.12.10'\n", text)
        self.assertEqual(text.count("persist-credentials: false"), 2)
        self.assertIn("          ref: ${{ github.sha }}\n          path: control", text)

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
        self.assertIn("          ref: ${{ steps.approval.outputs.source_ref }}\n", text)
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
            self.assertNotIn(forbidden, text)

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

        for forbidden_path in (
            ".github/workflows/hardware-inspection-intel-runner-stage-a.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-b.yml",
            ".github/workflows/hardware-inspection-intel-runner-stage-d.yml",
            "scripts/hardware-inspection/Invoke-HardwareInspectionIntelOffline.ps1",
            "scripts/hardware-inspection/Disable-HardwareInspectionNetwork.ps1",
            "scripts/hardware-inspection/Enable-HardwareInspectionNetwork.ps1",
        ):
            self.assertFalse(
                (REPOSITORY_ROOT / forbidden_path).exists(),
                "forbidden Stage 0 inventory path exists: " + forbidden_path,
            )

        runbook_text = runbook_path.read_text(encoding="utf-8")
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
            self.assertIn(required_phrase, normalized_runbook)
        self.assertIn(
            "docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md",
            runbook_text,
        )


if __name__ == "__main__":
    unittest.main()
