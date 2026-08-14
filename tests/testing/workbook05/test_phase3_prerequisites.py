from __future__ import annotations

import base64
import json
import os
import shutil
import subprocess
import sys
import unittest
from dataclasses import dataclass
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

from scripts.testing.workbook05.phase3 import prerequisites
from scripts.testing.workbook05.phase3.contracts import validate_phase3_record
from scripts.testing.workbook05.phase3.hashing import sha256_file
from scripts.testing.workbook05.phase3.prerequisites import verify_prerequisites

REPOSITORY_ROOT = Path(__file__).resolve().parents[3]

RUNTIME_DECISION_BASE64 = (
    "ew0KICAgICJzY2hlbWFfdmVyc2lvbiI6ICAiMS4wIiwNCiAgICAiY2FtcGFpZ25faWQiOiAgIkdUUS1XQjA1LU1GLXYxIiwNCiAgICAicmVjb3JkX3R5cGUiOiAgImJ1aWxkLWRlY2lzaW9uIiwNCiAgICAicm91dGVfaWQiOiAgInJvdXRlLWEtbWVyZ2VkLW9wZW52aW5vIiwNCiAgICAiY29tcG9uZW50IjogICJydW50aW1lIiwNCiAgICAic291cmNlX2NvbW1pdCI6ICAiYjlhMWYyMDFjMTA5ZTBiZWQ3NDc2MzkzNGY3OTQ4M2NmNmM0Y2JmNCIsDQogICAgInN0YXR1cyI6ICAiUGFzc2VkIiwNCiAgICAicmVhc29ucyI6ICBbDQogICAgICAgICAgICAgICAgICAgICJUaGUgZXhhY3QgcGlubmVkIFJvdXRlIEEgT3BlblZJTk8gUnVudGltZSByZXN1bWVkIGZyb20gdGhlIGluZGVwZW5kZW50bHkgdmFsaWRhdGVkIHRpbWVvdXQgd29ya3NwYWNlLCBjb21wbGV0ZWQgaXRzIGluY3JlbWVudGFsIGJ1aWxkIGFuZCBpbnN0YWxsLCByZXRhaW5lZCBib3RoIHJlcXVpcmVkIEdlbkFJIGZyb250ZW5kIGhlYWRlcnMsIGFuZCBwcm9kdWNlZCBoYXNoYWJsZSBvdXRwdXRzLiBObyBtb2RlbCBvciBzY2llbnRpZmljIGNsYWltIGlzIGF1dGhvcmlzZWQuIg0KICAgICAgICAgICAgICAgIF0sDQogICAgInJlcXVpcmVkX2NvbXBvbmVudHMiOiAgWw0KDQogICAgICAgICAgICAgICAgICAgICAgICAgICAgXSwNCiAgICAiZ3Jhbml0ZV9tb2RlbF90ZXN0X2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgImFjdGl2YXRpb25fY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZSwNCiAgICAicGFja2VkX3N0b3JhZ2VfY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZSwNCiAgICAicGVyZm9ybWFuY2VfY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZSwNCiAgICAicXVhbGl0eV9jbGFpbV9hdXRob3Jpc2VkIjogIGZhbHNlDQp9Cg=="
)
GENAI_DECISION_BASE64 = (
    "ew0KICAgICJzY2hlbWFfdmVyc2lvbiI6ICAiMS4wIiwNCiAgICAiY2FtcGFpZ25faWQiOiAgIkdUUS1XQjA1LU1GLXYxIiwNCiAgICAicmVjb3JkX3R5cGUiOiAgImJ1aWxkLWRlY2lzaW9uIiwNCiAgICAicm91dGVfaWQiOiAgInJvdXRlLWEtbWVyZ2VkLW9wZW52aW5vIiwNCiAgICAiY29tcG9uZW50IjogICJnZW5haSIsDQogICAgInNvdXJjZV9jb21taXQiOiAgImJkOGQ2NTQyZTNjYTFhYzMwMDQyZDVkOGQ0MjAyY2UwMGI1ZjRhZjAiLA0KICAgICJzdGF0dXMiOiAgIlBhc3NlZCIsDQogICAgInJlYXNvbnMiOiAgWw0KICAgICAgICAgICAgICAgICAgICAiVGhlIGV4YWN0IEdlbkFJIHNvdXJjZSBidWlsdCBhZ2FpbnN0IHRoZSBleGFjdCBhY2NlcHRlZCBSb3V0ZSBBIFJ1bnRpbWUgcGFja2FnZS4gTm8gbW9kZWwsIGFjdGl2YXRpb24sIHN0b3JhZ2UsIGZhbGxiYWNrLCBwZXJmb3JtYW5jZSwgb3IgcXVhbGl0eSBjbGFpbSBpcyBhdXRob3Jpc2VkLiINCiAgICAgICAgICAgICAgICBdLA0KICAgICJyZXF1aXJlZF9jb21wb25lbnRzIjogIFsNCg0KICAgICAgICAgICAgICAgICAgICAgICAgICAgIF0sDQogICAgImdyYW5pdGVfbW9kZWxfdGVzdF9hdXRob3Jpc2VkIjogIGZhbHNlLA0KICAgICJhY3RpdmF0aW9uX2NsYWltX2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgInBhY2tlZF9zdG9yYWdlX2NsYWltX2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgInBlcmZvcm1hbmNlX2NsYWltX2F1dGhvcmlzZWQiOiAgZmFsc2UsDQogICAgInF1YWxpdHlfY2xhaW1fYXV0aG9yaXNlZCI6ICBmYWxzZQ0KfQo="
)


@dataclass(frozen=True)
class PrerequisiteFixture:
    runtime_install: Path
    runtime_decision: Path
    genai_install: Path
    genai_decision: Path

    def as_kwargs(self) -> dict[str, Path]:
        return {
            "runtime_install": self.runtime_install,
            "runtime_decision": self.runtime_decision,
            "genai_install": self.genai_install,
            "genai_decision": self.genai_decision,
        }


def _write_required_files(root: Path, relative_paths: tuple[str, ...]) -> None:
    for index, relative in enumerate(relative_paths, start=1):
        path = root.joinpath(*relative.split("/"))
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(f"fixture-{index}:{relative}".encode("utf-8"))


def make_valid_prerequisite_fixture(directory: str | Path) -> PrerequisiteFixture:
    base = Path(directory)
    runtime_install = base / "runtime" / "i-ov"
    genai_install = base / "genai" / "i-genai"
    runtime_install.mkdir(parents=True)
    genai_install.mkdir(parents=True)
    _write_required_files(runtime_install, prerequisites.RUNTIME_REQUIRED_FILES)
    _write_required_files(genai_install, prerequisites.GENAI_REQUIRED_FILES)

    runtime_decision = base / "accepted-runtime" / "decision.json"
    genai_decision = base / "accepted-genai" / "decision.json"
    runtime_decision.parent.mkdir()
    genai_decision.parent.mkdir()
    runtime_decision.write_bytes(base64.b64decode(RUNTIME_DECISION_BASE64))
    genai_decision.write_bytes(base64.b64decode(GENAI_DECISION_BASE64))
    return PrerequisiteFixture(
        runtime_install=runtime_install,
        runtime_decision=runtime_decision,
        genai_install=genai_install,
        genai_decision=genai_decision,
    )


def _rewrite_decision(path: Path, mutate) -> str:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    mutate(payload)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return sha256_file(path)


class Phase3PrerequisiteTests(unittest.TestCase):
    def test_exact_prerequisites_produce_schema_valid_proof(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            proof = verify_prerequisites(**fixture.as_kwargs())

            self.assertEqual("Passed", proof["status"])
            self.assertEqual(3, len(proof["runtime"]["required_files"]))
            self.assertEqual(4, len(proof["genai"]["required_files"]))
            self.assertEqual(
                [],
                validate_phase3_record("prerequisite-proof", proof, REPOSITORY_ROOT),
            )

    def test_changed_runtime_decision_is_integrity_failure(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            fixture.runtime_decision.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Runtime decision SHA-256 mismatch"):
                verify_prerequisites(**fixture.as_kwargs())

    def test_changed_genai_decision_is_integrity_failure(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            fixture.genai_decision.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "GenAI decision SHA-256 mismatch"):
                verify_prerequisites(**fixture.as_kwargs())

    def test_wrong_runtime_source_commit_is_rejected_after_hash_check(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            digest = _rewrite_decision(
                fixture.runtime_decision,
                lambda payload: payload.__setitem__("source_commit", "0" * 40),
            )
            with patch.object(prerequisites, "EXPECTED_RUNTIME_DECISION_SHA256", digest):
                with self.assertRaisesRegex(ValueError, "source_commit mismatch"):
                    verify_prerequisites(**fixture.as_kwargs())

    def test_wrong_genai_source_commit_is_rejected_after_hash_check(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            digest = _rewrite_decision(
                fixture.genai_decision,
                lambda payload: payload.__setitem__("source_commit", "0" * 40),
            )
            with patch.object(prerequisites, "EXPECTED_GENAI_DECISION_SHA256", digest):
                with self.assertRaisesRegex(ValueError, "source_commit mismatch"):
                    verify_prerequisites(**fixture.as_kwargs())

    def test_later_claim_authorisation_is_rejected_after_hash_check(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            digest = _rewrite_decision(
                fixture.runtime_decision,
                lambda payload: payload.__setitem__("performance_claim_authorised", True),
            )
            with patch.object(prerequisites, "EXPECTED_RUNTIME_DECISION_SHA256", digest):
                with self.assertRaisesRegex(ValueError, "unexpectedly authorises scientific claim"):
                    verify_prerequisites(**fixture.as_kwargs())

    def test_missing_openvino_config_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            (fixture.runtime_install / "runtime" / "cmake" / "OpenVINOConfig.cmake").unlink()
            with self.assertRaisesRegex(ValueError, "Required Runtime file is missing"):
                verify_prerequisites(**fixture.as_kwargs())

    def test_missing_genai_dll_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            (fixture.genai_install / "runtime" / "bin" / "intel64" / "Release" / "openvino_genai.dll").unlink()
            with self.assertRaisesRegex(ValueError, "Required GenAI file is missing"):
                verify_prerequisites(**fixture.as_kwargs())

    def test_reparse_point_install_is_rejected(self) -> None:
        with TemporaryDirectory() as directory:
            fixture = make_valid_prerequisite_fixture(directory)
            link = Path(directory) / "runtime-link"
            try:
                link.symlink_to(fixture.runtime_install, target_is_directory=True)
            except (OSError, NotImplementedError):
                self.skipTest("Directory symlink creation is unavailable on this runner.")
            with self.assertRaisesRegex(ValueError, "link or reparse point"):
                verify_prerequisites(
                    runtime_install=link,
                    runtime_decision=fixture.runtime_decision,
                    genai_install=fixture.genai_install,
                    genai_decision=fixture.genai_decision,
                )

    @unittest.skipUnless(os.name == "nt", "PowerShell behavior test requires Windows")
    def test_powershell_wrapper_preserves_existing_output_on_failure(self) -> None:
        powershell = shutil.which("powershell.exe") or shutil.which("powershell")
        if powershell is None:
            self.skipTest("Windows PowerShell is unavailable.")
        script = REPOSITORY_ROOT / "tests" / "testing" / "workbook05" / "Invoke-Phase3PrerequisiteTests.Tests.ps1"
        completed = subprocess.run(
            [
                powershell,
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(script),
                "-RepositoryRoot",
                str(REPOSITORY_ROOT),
                "-PythonPath",
                sys.executable,
            ],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
            timeout=120,
        )
        self.assertEqual(
            0,
            completed.returncode,
            msg=f"STDOUT:\n{completed.stdout}\nSTDERR:\n{completed.stderr}",
        )


if __name__ == "__main__":
    unittest.main()
