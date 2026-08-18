import codecs
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


if __name__ == "__main__":
    unittest.main()
