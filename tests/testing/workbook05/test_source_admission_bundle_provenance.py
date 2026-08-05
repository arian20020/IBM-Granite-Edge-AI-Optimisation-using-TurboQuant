from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.source_admission_bundle_validation import (
    PINNED_SOURCE_REPORTS,
    validate_source_admission_bundle,
)


ROOT = Path(__file__).resolve().parents[3]
SETTINGS_PATH = (
    ROOT
    / "experiments/granite_turboquant_intel/configurations/workbook05/"
    "source-admission-settings.json"
)
SOURCE_TREE_SCHEMA_PATH = (
    ROOT
    / "experiments/granite_turboquant_intel/schemas/workbook05/"
    "source-tree-report.schema.json"
)
ROUTE_A_ID = "route-a-merged-openvino"
ROUTE_B_ID = "route-b-experimental-qjl-polar"


class SourceAdmissionBundleProvenanceContractTests(unittest.TestCase):
    def test_validator_pins_match_orchestrator_source_roles_and_sources(
        self,
    ) -> None:
        settings = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))
        route_a = settings["routes"][ROUTE_A_ID]
        route_b = settings["routes"][ROUTE_B_ID]

        expected = {
            "routes/route-a/source-tree-runtime.json": {
                "source_role": "runtime",
                **route_a["runtime"],
            },
            "routes/route-a/source-tree-genai.json": {
                "source_role": "genai-compatibility-candidate",
                **route_a["genai"],
            },
            "routes/route-b/source-tree-runtime.json": {
                "source_role": "experimental-runtime",
                **route_b["runtime"],
            },
        }

        for evidence_path, configured in expected.items():
            with self.subTest(evidence_path=evidence_path):
                pinned = PINNED_SOURCE_REPORTS[evidence_path]
                self.assertEqual(
                    configured["source_role"],
                    pinned["source_role"],
                )
                self.assertEqual(
                    configured["repository_full_name"],
                    pinned["repository_full_name"],
                )
                self.assertEqual(
                    configured["origin_url"],
                    pinned["expected_origin_url"],
                )
                self.assertEqual(
                    configured["commit"],
                    pinned["expected_commit"],
                )

    def test_source_tree_schema_accepts_only_live_orchestrator_roles(
        self,
    ) -> None:
        schema = json.loads(
            SOURCE_TREE_SCHEMA_PATH.read_text(encoding="utf-8")
        )
        source_roles = schema["properties"]["source_role"]["enum"]

        self.assertEqual(
            [
                "runtime",
                "genai-compatibility-candidate",
                "experimental-runtime",
            ],
            source_roles,
        )

    def test_command_records_are_resolved_from_the_route_report_directory(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory) / "bundle"
            report_path = (
                bundle / "routes/route-a/source-tree-runtime.json"
            )
            command_path = (
                bundle
                / "routes/route-a/commands/runtime-status.command.json"
            )
            report_path.parent.mkdir(parents=True, exist_ok=True)
            command_path.parent.mkdir(parents=True, exist_ok=True)

            report = {
                "schema_version": "1.0",
                "campaign_id": "GTQ-WB05-MF-v1",
                "phase_id": "phase-1-source-admission",
                "route_id": ROUTE_A_ID,
                "source_role": "runtime",
                "repository_full_name": "openvinotoolkit/openvino",
                "expected_origin_url": (
                    "https://github.com/openvinotoolkit/openvino.git"
                ),
                "actual_origin_url": (
                    "https://github.com/openvinotoolkit/openvino.git"
                ),
                "expected_commit": (
                    "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
                ),
                "actual_commit": (
                    "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
                ),
                "working_tree_clean": True,
                "submodules_complete": True,
                "submodules": [],
                "command_records": [
                    "commands/runtime-status.command.json"
                ],
                "decision": "Passed",
                "decision_reason": "Exact source verification passed.",
            }
            report_path.write_text(
                json.dumps(report, indent=2) + "\n",
                encoding="utf-8",
            )
            command_path.write_text("{}\n", encoding="utf-8")

            issues = validate_source_admission_bundle(bundle, ROOT)
            command_record_issues = [
                issue
                for issue in issues
                if issue.code == "SOURCE_COMMAND_RECORD_MISSING"
                and issue.path
                == "routes/route-a/source-tree-runtime.json"
            ]

        self.assertEqual([], command_record_issues)


if __name__ == "__main__":
    unittest.main()
