from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.schema_validation import validate_json_file
from scripts.testing.workbook05.source_capability import (
    inspect_route_capabilities,
    write_capability_report,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_ROOT = (
    REPOSITORY_ROOT
    / "tests/testing/workbook05/fixtures/source-admission"
)
SETTINGS_PATH = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/configurations/workbook05/source-admission-settings.json"
)
REPORT_SCHEMA = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/schemas/workbook05/source-capability-report.schema.json"
)


FIXTURE_BINDINGS = {
    "src/inference/dev_api/openvino/runtime/internal_properties.hpp": {
        "route-a-merged-openvino": "route-a-internal-properties.hpp",
        "route-b-experimental-qjl-polar": "route-b-internal-properties.hpp",
    },
    "src/plugins/intel_cpu/src/config.cpp": {
        "route-a-merged-openvino": "route-a-config.cpp",
    },
    "src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/codecs/turboq_quantize.hpp": {
        "route-a-merged-openvino": "route-a-turboq-quantize.hpp",
    },
    "src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/codecs/turboq_codec.hpp": {
        "route-a-merged-openvino": "route-a-turboq-codec.hpp",
    },
    "src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/attn_quant_turboq.cpp": {
        "route-b-experimental-qjl-polar": "route-b-attn-quant-turboq.cpp",
    },
    "src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/attn_quant_turboq.hpp": {
        "route-b-experimental-qjl-polar": "route-b-attn-quant-turboq.hpp",
    },
    "src/plugins/intel_cpu/src/nodes/kernels/scaled_attn/polar_codecs.hpp": {
        "route-b-experimental-qjl-polar": "route-b-polar-codecs.hpp",
    },
}


def _settings() -> dict[str, object]:
    return json.loads(SETTINGS_PATH.read_text(encoding="utf-8"))


def _materialise_source(root: Path, route_id: str) -> None:
    for source_path, route_bindings in FIXTURE_BINDINGS.items():
        fixture_name = route_bindings.get(route_id)
        if fixture_name is None:
            continue
        destination = root / source_path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes((FIXTURE_ROOT / fixture_name).read_bytes())


class SourceCapabilityTests(unittest.TestCase):
    def test_route_a_findings_are_present_in_source_only(self) -> None:
        settings = _settings()
        requirements = settings["routes"]["route-a-merged-openvino"]
        with tempfile.TemporaryDirectory() as directory:
            source_root = Path(directory) / "source"
            _materialise_source(source_root, "route-a-merged-openvino")

            findings = inspect_route_capabilities(
                source_root,
                requirements["source_requirements"],
            )

        self.assertEqual(4, len(findings))
        self.assertTrue(
            all(finding.classification == "Present in source" for finding in findings)
        )
        self.assertNotIn(
            "Supported",
            {finding.classification for finding in findings},
        )

    def test_route_b_preserves_qjl_contradiction_and_other_source_presence(self) -> None:
        settings = _settings()
        requirements = settings["routes"]["route-b-experimental-qjl-polar"]
        with tempfile.TemporaryDirectory() as directory:
            source_root = Path(directory) / "source"
            _materialise_source(source_root, "route-b-experimental-qjl-polar")

            findings = inspect_route_capabilities(
                source_root,
                requirements["source_requirements"][:-1],
            )

        by_id = {finding.capability_id: finding for finding in findings}
        property_finding = by_id["RB-CAP-001-codec-properties"]
        self.assertEqual("Contradictory", property_finding.classification)
        self.assertEqual(("not yet supported",), property_finding.contradictory_tokens)
        self.assertIn("TURBO_QUANT_3_QJL", property_finding.matched_tokens)
        self.assertEqual(
            "Present in source",
            by_id["RB-CAP-002-qjl-write-read-paths"].classification,
        )
        self.assertEqual(
            "Present in source",
            by_id["RB-CAP-003-polar-api"].classification,
        )
        self.assertEqual(
            "Present in source",
            by_id["RB-CAP-004-polar-decode-codecs"].classification,
        )

    def test_missing_required_token_is_explicit(self) -> None:
        settings = _settings()
        requirement = copy.deepcopy(
            settings["routes"]["route-a-merged-openvino"]["source_requirements"][0]
        )
        requirement["required_tokens"].append("THIS_TOKEN_DOES_NOT_EXIST")
        with tempfile.TemporaryDirectory() as directory:
            source_root = Path(directory) / "source"
            _materialise_source(source_root, "route-a-merged-openvino")

            finding = inspect_route_capabilities(source_root, [requirement])[0]

        self.assertEqual("Missing", finding.classification)
        self.assertEqual(
            ("THIS_TOKEN_DOES_NOT_EXIST",),
            finding.missing_tokens,
        )

    def test_hash_is_over_the_complete_unmodified_file(self) -> None:
        settings = _settings()
        requirement = settings["routes"]["route-a-merged-openvino"]
        requirement = requirement["source_requirements"][0]
        expected_data = (FIXTURE_ROOT / "route-a-internal-properties.hpp").read_bytes()
        with tempfile.TemporaryDirectory() as directory:
            source_root = Path(directory) / "source"
            _materialise_source(source_root, "route-a-merged-openvino")

            finding = inspect_route_capabilities(source_root, [requirement])[0]

        self.assertEqual(hashlib.sha256(expected_data).hexdigest(), finding.sha256)

    def test_source_path_cannot_escape_the_verified_root(self) -> None:
        requirement = {
            "capability_id": "ESCAPE",
            "path": "../outside.txt",
            "required_tokens": ["secret"],
            "contradictory_tokens": [],
        }
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "outside.txt").write_text("secret", encoding="utf-8")
            source_root = root / "source"
            source_root.mkdir()

            with self.assertRaisesRegex(ValueError, "escapes"):
                inspect_route_capabilities(source_root, [requirement])

    def test_report_writes_bounded_excerpts_and_passes_schema(self) -> None:
        settings = _settings()
        requirements = settings["routes"]["route-b-experimental-qjl-polar"]
        requirements = requirements["source_requirements"][:-1]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source_root = root / "source"
            evidence_root = root / "evidence"
            _materialise_source(source_root, "route-b-experimental-qjl-polar")

            report = write_capability_report(
                source_root,
                requirements,
                "route-b-experimental-qjl-polar",
                evidence_root,
            )
            issues = validate_json_file(
                evidence_root / "source-capabilities.json",
                REPORT_SCHEMA,
            )

            self.assertEqual([], issues)
            for finding in report["findings"]:
                excerpt = evidence_root / finding["excerpt_path"]
                self.assertTrue(excerpt.is_file())
                self.assertLess(excerpt.stat().st_size, 8192)

        self.assertEqual("Candidate", report["decision"])


if __name__ == "__main__":
    unittest.main()
