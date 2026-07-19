import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts.testing.official_openvino.patch_identity import validate_patch_identity


EXPECTED = "7dea0459b2ac7d8dfd877fd9df6737674fd8371d"


class PatchIdentityTests(unittest.TestCase):
    def test_patch_identity_accepts_clean_expected_base_with_patch_commit(self):
        validate_patch_identity(
            {
                "base_commit": EXPECTED,
                "patch_commit": EXPECTED,
                "dirty": False,
            },
            EXPECTED,
        )

    def test_patch_identity_rejects_wrong_or_dirty_base(self):
        with self.assertRaisesRegex(ValueError, "base commit"):
            validate_patch_identity(
                {"base_commit": "wrong", "patch_commit": EXPECTED, "dirty": False},
                EXPECTED,
            )
        with self.assertRaisesRegex(ValueError, "dirty"):
            validate_patch_identity(
                {"base_commit": EXPECTED, "patch_commit": EXPECTED, "dirty": True},
                EXPECTED,
            )

    def test_patch_identity_rejects_missing_patch_commit(self):
        with self.assertRaisesRegex(ValueError, "patch commit"):
            validate_patch_identity(
                {"base_commit": EXPECTED, "dirty": False}, EXPECTED
            )

    def test_controller_is_idempotent_and_enables_windows_long_paths(self):
        repo_root = Path(__file__).resolve().parents[3]
        script = repo_root / "scripts/testing/prepare_openvino_turboquant_patch.ps1"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            upstream = root / "upstream"
            destination = root / "derived"
            evidence = root / "identity.json"
            subprocess.run(["git", "init", str(upstream)], check=True, capture_output=True)
            subprocess.run(
                ["git", "-C", str(upstream), "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-m", "base"],
                check=True,
                capture_output=True,
            )
            expected = subprocess.check_output(
                ["git", "-C", str(upstream), "rev-parse", "HEAD"], text=True
            ).strip()
            command = [
                "powershell", "-ExecutionPolicy", "Bypass", "-File", str(script),
                "-ExpectedCommit", expected, "-UpstreamPath", str(upstream),
                "-DestinationPath", str(destination), "-EvidencePath", str(evidence),
            ]
            subprocess.run(command, check=True, capture_output=True, text=True)
            subprocess.run(command, check=True, capture_output=True, text=True)

            record = json.loads(evidence.read_text(encoding="utf-8"))
            self.assertEqual(record["applied_patches"], [])
            self.assertEqual(
                subprocess.check_output(
                    ["git", "-C", str(destination), "config", "--bool", "core.longpaths"],
                    text=True,
                ).strip(),
                "true",
            )


if __name__ == "__main__":
    unittest.main()
