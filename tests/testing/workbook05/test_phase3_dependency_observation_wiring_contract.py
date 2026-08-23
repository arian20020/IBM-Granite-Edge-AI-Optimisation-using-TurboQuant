from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
LIVE_SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1"
)
OBSERVATION_CLI_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/phase3/dependency_observation_cli.py"
)


class Phase3DependencyObservationWiringContractTests(unittest.TestCase):
    """Freeze the Python observation boundary before changing the live collector."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.live_script = LIVE_SCRIPT_PATH.read_text(encoding="utf-8")
        cls.observation_cli = OBSERVATION_CLI_PATH.read_text(encoding="utf-8")

    def test_live_collector_uses_one_controlled_python_observation_command(self) -> None:
        command_id = "-CommandId 'observation-materialize'"
        self.assertIn(command_id, self.live_script)

        start = self.live_script.index(command_id)
        end = self.live_script.index("-CommandId 'decision-recompute'", start)
        command = self.live_script[start:end]

        # The collector may pass only explicit paths and scalar identities. It
        # must not pipe the large heterogeneous pip report through PowerShell's
        # legacy JSON serializer again.
        required_tokens = (
            "-FilePath $BasePythonPath",
            "'scripts.testing.workbook05.phase3.dependency_observation_cli'",
            "'--output', $ObservationPath",
            "'--generated-at-utc', $ObservationGeneratedAtUtc",
            "'--workspace-root', $attemptRoot",
            "'--python-version', $ObservedPythonVersion",
            "'--python-executable-path', $FinalPython",
            "'--python-executable-sha256', $FinalPythonSha256",
            "'--pip-version', $ObservedPipVersion",
            "'--pip-executable-path', $FinalPip",
            "'--pip-executable-sha256', $FinalPipSha256",
            "'--bootstrap-lock', $RetainedBootstrapLock",
            "'--bootstrap-install-report', $BootstrapReport",
            "'--normal-lock', $OrdinaryLock",
            "'--normal-install-report', $NormalReport",
            "Join-Path $SourceEvidenceDirectory 'optimum.json'",
            "Join-Path $SourceEvidenceDirectory 'optimum-intel.json'",
            "Join-Path $ReportDirectory 'vcs-packages.json'",
            "'--checks', $ChecksPath",
            "Join-Path $ReportDirectory 'final-environment-packages.json'",
            "-WorkingDirectory $RepositoryRoot | Out-Null",
        )
        for token in required_tokens:
            with self.subTest(token=token):
                self.assertIn(token, command)
        self.assertEqual(2, command.count("'--source-tree'"))

    def test_checks_are_published_before_observation_and_decision(self) -> None:
        checks_path = "$ChecksPath = Join-Path $EvidenceRoot 'checks.json'"
        checks_write = "-Path $ChecksPath"
        observation = "-CommandId 'observation-materialize'"
        decision = "-CommandId 'decision-recompute'"

        for token in (checks_path, checks_write, observation, decision):
            with self.subTest(token=token):
                self.assertIn(token, self.live_script)

        self.assertLess(self.live_script.index(checks_path), self.live_script.index(observation))
        self.assertLess(self.live_script.index(checks_write), self.live_script.index(observation))
        self.assertLess(self.live_script.index(observation), self.live_script.index(decision))
        self.assertEqual(1, self.live_script.count(checks_write))

    def test_live_collector_no_longer_builds_the_observation_in_powershell(self) -> None:
        forbidden_tokens = (
            "$BootstrapLockText = Get-Content",
            "$BootstrapReportText = Get-Content",
            "$NormalLockText = Get-Content",
            "$NormalReportObject = Get-Content",
            "$Observation = [ordered]@{",
            "Write-PreflightAtomicJson -Path $ObservationPath -Value $Observation",
        )
        for token in forbidden_tokens:
            with self.subTest(token=token):
                self.assertNotIn(token, self.live_script)

    def test_python_boundary_preserves_bytes_hashes_and_atomic_publication(self) -> None:
        required_tokens = (
            "payload = path.read_bytes()",
            'payload.decode("utf-8")',
            "hashlib.sha256(payload).hexdigest()",
            'temporary.open("x", encoding="utf-8", newline="\\n")',
            "handle.flush()",
            "os.fsync(handle.fileno())",
            "os.rename(temporary, path)",
            "ensure_ascii=False",
        )
        for token in required_tokens:
            with self.subTest(token=token):
                self.assertIn(token, self.observation_cli)


if __name__ == "__main__":
    unittest.main()
