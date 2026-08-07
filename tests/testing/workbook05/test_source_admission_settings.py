from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.testing.workbook05.schema_validation import validate_json_file


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SETTINGS_PATH = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/configurations/workbook05/source-admission-settings.json"
)
TEMPLATE_ROOT = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/manifests/templates/workbook05"
)
SCHEMA_ROOT = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/schemas/workbook05"
)


class SourceAdmissionSettingsTests(unittest.TestCase):
    def test_settings_pin_both_routes_and_safe_workspace(self) -> None:
        settings = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))

        self.assertEqual("C:\\wb05", settings["workspace_root"])
        self.assertEqual(85899345920, settings["minimum_free_bytes"])
        self.assertEqual(
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            settings["routes"]["route-a-merged-openvino"]["runtime"]["commit"],
        )
        self.assertEqual(
            "05e5c7670b597746f858946974d11f38e3baf42f",
            settings["routes"]["route-a-merged-openvino"]["genai"]["commit"],
        )
        self.assertEqual(
            "1827f6458d049de11c1a8203c793af67c99935dc",
            settings["routes"]["route-b-experimental-qjl-polar"]["runtime"]["commit"],
        )
        self.assertEqual(
            ["-G", "Visual Studio 17 2022", "-A", "x64", "-DENABLE_INTEL_GPU=OFF"],
            settings["route_a_configure_options"],
        )
        self.assertFalse(settings["allow_route_b_configure_while_blocked"])

    def test_route_b_preserves_the_qjl_contradiction(self) -> None:
        settings = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))
        requirements = settings["routes"]["route-b-experimental-qjl-polar"]
        codec_requirement = requirements["source_requirements"][0]

        self.assertIn("TURBO_QUANT_3_QJL", codec_requirement["required_tokens"])
        self.assertIn("TURBO_QUANT_4_QJL", codec_requirement["required_tokens"])
        self.assertEqual(
            ["not yet supported"],
            codec_requirement["contradictory_tokens"],
        )

    def test_route_b_expected_test_sources_cover_both_directory_forms(self) -> None:
        settings = json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))
        expected = set(
            settings["routes"]["route-b-experimental-qjl-polar"]
            ["expected_test_sources"]
        )

        self.assertEqual(
            {
                "instances/x64/concat_sdp_turboq.cpp",
                "x64/concat_sdp_turboq.cpp",
                "instances/common/concat_sdp_turboq.cpp",
                "common/concat_sdp_turboq.cpp",
            },
            expected,
        )

    def test_every_source_admission_template_passes_its_schema(self) -> None:
        bindings = {
            "source-tree-report-template.json": "source-tree-report.schema.json",
            "source-capability-report-template.json": "source-capability-report.schema.json",
            "cmake-test-discovery-report-template.json": "cmake-test-discovery-report.schema.json",
            "configure-probe-report-template.json": "configure-probe-report.schema.json",
            "source-admission-summary-template.json": "source-admission-summary.schema.json",
        }

        for template_name, schema_name in bindings.items():
            with self.subTest(template=template_name):
                issues = validate_json_file(
                    TEMPLATE_ROOT / template_name,
                    SCHEMA_ROOT / schema_name,
                )
                self.assertEqual([], issues)

    def test_configure_schema_forbids_build_install_and_package(self) -> None:
        schema = json.loads(
            (SCHEMA_ROOT / "configure-probe-report.schema.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(False, schema["properties"]["build_invoked"]["const"])
        self.assertEqual(False, schema["properties"]["install_invoked"]["const"])
        self.assertEqual(False, schema["properties"]["package_invoked"]["const"])


if __name__ == "__main__":
    unittest.main()
