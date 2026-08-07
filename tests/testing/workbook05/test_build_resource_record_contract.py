from __future__ import annotations

import unittest
from pathlib import Path


# Resolve the real committed PowerShell module. These are static contract tests
# because the Linux-side review environment cannot execute Windows PowerShell.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
BUILD_MODULE = (
    REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"
)


class BuildResourceRecordContractTests(unittest.TestCase):
    """Require resource summaries to be first-class schema-validatable records."""

    @classmethod
    def setUpClass(cls) -> None:
        # Strict UTF-8 reading deliberately fails if source corruption returns.
        cls.text = BUILD_MODULE.read_text(encoding="utf-8", errors="strict")

    def test_sampler_accepts_record_identity(self) -> None:
        # The sampler must receive the exact identity already known by the safe
        # process adapter rather than inventing or inferring it in a background job.
        required_tokens = (
            "[string]$RouteId",
            "[string]$Component",
            "[string]$CommandId",
            "-RouteId $RouteId",
            "-Component $Component",
            "-CommandId $CommandId",
        )
        for token in required_tokens:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_sampler_writes_full_resource_schema_identity(self) -> None:
        # Without these fields the independent bundle validator cannot recognise
        # *.resources.json as build-resource-summary records and schema-check them.
        required_tokens = (
            "campaign_id = 'GTQ-WB05-MF-v1'",
            "record_type = 'build-resource-summary'",
            "route_id = $RouteId",
            "component = $Component",
            "command_id = $CommandId",
            "sample_interval_seconds = $SampleIntervalSeconds",
            "heartbeat_timeout_seconds = $HeartbeatTimeoutSeconds",
            "safety_stop_triggered = $safetyStopTriggered",
        )
        for token in required_tokens:
            with self.subTest(token=token):
                self.assertIn(token, self.text)


if __name__ == "__main__":
    unittest.main()
