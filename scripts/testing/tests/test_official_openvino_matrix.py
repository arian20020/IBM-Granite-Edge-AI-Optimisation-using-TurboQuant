import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.campaigns.openvino.matrix import (
    RUNTIME_ALGORITHMS,
    SEMANTIC_SCALAR_REJECTION_IDS,
    execution_contract,
    load_matrix,
)


ROOT = Path(__file__).resolve().parents[3]
MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"


class OfficialOpenVINOMatrixTests(unittest.TestCase):
    def test_matrix_freezes_receipt_references_and_every_execution_contract_field(self):
        raw = json.loads(MATRIX.read_text(encoding="utf-8"))
        self.assertEqual(
            raw.get("source_identity"),
            {
                "path": "experiments/raw-results/openvino-turboquant/2026-07-30/provenance/source-identity-bounded.json",
                "sha256": "44f4bfc111c78258320c0788ddba288f0f54840845cefac6fdfcae9b772e6574",
                "scope": "declared campaign input; not row execution proof",
                "upstream_commit": "7dea0459b2ac7d8dfd877fd9df6737674fd8371d",
                "patch_commit": "00edae3bfd40a968c964ea4878128dceeeb22d1a",
                "derived_tree": "2e872dd4817c42d91cb7c3094954d7b56fa12b0a",
            },
        )
        self.assertEqual(
            raw.get("build_identity"),
            {
                "path": "experiments/raw-results/openvino-turboquant/2026-07-30/provenance/build-00edae3b-attempt-001/build-provenance.json",
                "sha256": "57fb318a55db56fb409a60f0b1d516a988f8543c0446153e63efdee3a748e262",
                "scope": "declared campaign input; not row execution proof",
                "status": "passed",
                "patch_commit": "00edae3bfd40a968c964ea4878128dceeeb22d1a",
            },
        )
        required = {
            "key_cache_precision",
            "value_cache_precision",
            "requested_device",
            "runtime_key_algorithm",
            "runtime_value_algorithm",
            "norm_correction",
            "attention_path",
            "execution_route",
            "expected_outcome",
            "suitable_host_required",
            "numeric_generation_metrics_expected",
        }
        self.assertTrue(all(required <= set(case) for case in raw["cases"]))

        for case in load_matrix(MATRIX):
            contract = execution_contract(case)
            self.assertEqual(case.key_cache_precision, case.k_precision)
            self.assertEqual(case.value_cache_precision, case.v_precision)
            self.assertEqual(case.requested_device, case.device.upper())
            for field in required - {
                "key_cache_precision",
                "value_cache_precision",
                "requested_device",
            }:
                self.assertEqual(getattr(case, field), getattr(contract, field))

    def test_matrix_rejects_missing_or_drifting_frozen_execution_contract_fields(self):
        payload = json.loads(MATRIX.read_text(encoding="utf-8"))
        expected_source = {
            "path": "experiments/raw-results/openvino-turboquant/2026-07-30/provenance/source-identity-bounded.json",
            "sha256": "44f4bfc111c78258320c0788ddba288f0f54840845cefac6fdfcae9b772e6574",
            "scope": "declared campaign input; not row execution proof",
            "upstream_commit": "7dea0459b2ac7d8dfd877fd9df6737674fd8371d",
            "patch_commit": "00edae3bfd40a968c964ea4878128dceeeb22d1a",
            "derived_tree": "2e872dd4817c42d91cb7c3094954d7b56fa12b0a",
        }
        expected_build = {
            "path": "experiments/raw-results/openvino-turboquant/2026-07-30/provenance/build-00edae3b-attempt-001/build-provenance.json",
            "sha256": "57fb318a55db56fb409a60f0b1d516a988f8543c0446153e63efdee3a748e262",
            "scope": "declared campaign input; not row execution proof",
            "status": "passed",
            "patch_commit": "00edae3bfd40a968c964ea4878128dceeeb22d1a",
        }
        payload["source_identity"] = expected_source
        payload["build_identity"] = expected_build
        for case in load_matrix(MATRIX):
            contract = execution_contract(case)
            raw_case = next(
                item for item in payload["cases"] if item["test_id"] == case.test_id
            )
            raw_case.update({
                "key_cache_precision": case.k_precision,
                "value_cache_precision": case.v_precision,
                "requested_device": case.device.upper(),
                "runtime_key_algorithm": contract.runtime_key_algorithm,
                "runtime_value_algorithm": contract.runtime_value_algorithm,
                "norm_correction": contract.norm_correction,
                "attention_path": (
                    "not-applicable-non-runtime"
                    if contract.execution_route == "non-runtime"
                    else "not-produced-by-expected-rejection"
                    if contract.expected_outcome == "expected-rejection"
                    else "stateful_sdpa_reference_codec"
                    if contract.execution_route == "patched-stateful"
                    else "stateful_sdpa_standard"
                ),
                "execution_route": contract.execution_route,
                "expected_outcome": contract.expected_outcome,
                "suitable_host_required": contract.suitable_host_required,
                "numeric_generation_metrics_expected": contract.numeric_generation_metrics_expected,
            })
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "matrix.json"
            payload["cases"][0].pop("attention_path")
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "attention_path"):
                load_matrix(path)

            payload = json.loads(MATRIX.read_text(encoding="utf-8"))
            payload["cases"][0]["expected_outcome"] = "expected-rejection"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "expected_outcome"):
                load_matrix(path)

    def test_matrix_contains_every_wb04_id_once(self):
        cases = load_matrix(MATRIX)
        expected = ({f"OV-B{i:02d}" for i in range(1, 13)} |
                    {f"OV-C{i:02d}" for i in range(1, 7)} |
                    {f"OV-{i:02d}" for i in range(1, 11)} |
                    {f"OV-TQS-{i:02d}" for i in range(1, 13)} |
                    {f"OV-TQ-{i:02d}" for i in range(1, 21)})
        self.assertEqual({case.test_id for case in cases}, expected)
        self.assertEqual(len(cases), len(expected))

    def test_quality_records_cover_every_granite_runtime_candidate(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        quality = (
            {f"OV-{i:02d}" for i in range(2, 11)}
            | {f"OV-TQ-{i:02d}" for i in range(1, 19)}
        )
        self.assertEqual({key for key, case in by_id.items() if case.quality_required}, quality)
        raw = json.loads(MATRIX.read_text(encoding="utf-8"))
        self.assertEqual(
            {
                row["test_id"]
                for row in raw["cases"]
                if row.get("quality_required") is True
            },
            quality,
        )

    def test_large_host_guard_and_context_requirements_are_frozen(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        self.assertEqual({key for key, case in by_id.items() if case.guard == "ram-2048-mib"},
                         {"OV-C04", "OV-C05", "OV-C06", "OV-07", "OV-08", "OV-09",
                          "OV-10", "OV-TQ-16", "OV-TQ-17"})
        self.assertTrue(all(
            execution_contract(by_id[key]).suitable_host_required
            for key in {"OV-C04", "OV-C05", "OV-C06", "OV-07", "OV-08", "OV-09",
                        "OV-10", "OV-TQ-16", "OV-TQ-17"}
        ))
        self.assertEqual(by_id["OV-TQ-13"].contexts, (512, 2048, 4096, 8192))
        self.assertEqual(by_id["OV-TQ-14"].contexts, (512, 2048, 4096, 8192))

    def test_capability_sweep_preserves_all_independent_kv_combinations(self):
        sweep = [case for case in load_matrix(MATRIX) if case.phase == "capability"]
        combinations = {(case.k_algorithm, case.v_algorithm,
                         case.k_precision, case.v_precision) for case in sweep}
        self.assertEqual(len(sweep), 12)
        self.assertEqual(len(combinations), 12)

    def test_matrix_to_runtime_translation_is_explicit_and_never_lowercase_passthrough(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        for case in by_id.values():
            contract = execution_contract(case)
            self.assertEqual(contract.controlled_test_id, case.test_id)
            self.assertIn(contract.expected_outcome, {"pass", "expected-rejection"})
            self.assertFalse(
                contract.runtime_key_algorithm in {"standard", "scalar", "tbq3", "tbq4"}
            )
            self.assertFalse(
                contract.runtime_value_algorithm in {"standard", "scalar", "tbq3", "tbq4"}
            )
            if contract.expected_outcome == "pass" and contract.execution_route == "patched-stateful":
                self.assertIn(contract.runtime_key_algorithm, RUNTIME_ALGORITHMS)
                self.assertIn(contract.runtime_value_algorithm, RUNTIME_ALGORITHMS)

    def test_semantic_scalar_rejections_declare_standard_state_and_no_runtime_output(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        self.assertEqual(
            SEMANTIC_SCALAR_REJECTION_IDS,
            frozenset({"OV-04", "OV-05", "OV-TQ-01", "OV-TQ-02"}),
        )
        for test_id in SEMANTIC_SCALAR_REJECTION_IDS:
            contract = execution_contract(by_id[test_id])
            self.assertEqual(contract.execution_route, "expected-rejection")
            self.assertEqual(contract.expected_outcome, "expected-rejection")
            self.assertEqual(contract.runtime_key_algorithm, "STANDARD")
            self.assertEqual(contract.runtime_value_algorithm, "STANDARD")
            self.assertFalse(contract.norm_correction)
            self.assertEqual(
                contract.attention_path, "not-produced-by-expected-rejection"
            )
            self.assertFalse(contract.suitable_host_required)
            self.assertFalse(contract.numeric_generation_metrics_expected)
            self.assertFalse(contract.requires_actual_cache_precision_proof)

        for test_id in {"OV-08", "OV-09"}:
            self.assertEqual(
                execution_contract(by_id[test_id]).execution_route,
                "upstream-scalar",
            )

    def test_formal_key_or_value_only_rows_use_standard_on_uncompressed_side(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        expected = {
            "OV-TQ-07": ("TBQ4", "STANDARD", "u4", "f16"),
            "OV-TQ-08": ("STANDARD", "TBQ4", "f16", "u4"),
            "OV-TQ-09": ("TBQ3", "STANDARD", "u3", "f16"),
            "OV-TQ-10": ("STANDARD", "TBQ3", "f16", "u3"),
        }
        for test_id, values in expected.items():
            case = by_id[test_id]
            contract = execution_contract(case)
            self.assertEqual(
                (contract.runtime_key_algorithm, contract.runtime_value_algorithm,
                 case.k_precision, case.v_precision),
                values,
            )
            self.assertEqual(contract.execution_route, "patched-stateful")
            self.assertEqual(contract.expected_outcome, "pass")

    def test_norm_mode_is_explicit_for_turboquant_formal_rows(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        norm_on = {f"OV-TQ-{i:02d}" for i in range(3, 11)} | {
            "OV-TQ-13", "OV-TQ-14", "OV-TQ-15", "OV-TQ-16", "OV-TQ-17", "OV-TQ-18"
        }
        for test_id in norm_on:
            self.assertTrue(execution_contract(by_id[test_id]).norm_correction)
        self.assertFalse(execution_contract(by_id["OV-TQ-11"]).norm_correction)
        self.assertFalse(execution_contract(by_id["OV-TQ-12"]).norm_correction)

    def test_unsupported_boundaries_are_declared_expected_rejections(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        expected_rejections = (
            {f"OV-TQS-{i:02d}" for i in range(5, 13)}
            | {"OV-TQ-18", "OV-TQ-19", "OV-TQ-20"}
            | set(SEMANTIC_SCALAR_REJECTION_IDS)
        )
        self.assertEqual(
            {
                case.test_id
                for case in by_id.values()
                if execution_contract(case).expected_outcome == "expected-rejection"
            },
            expected_rejections,
        )
        for test_id in expected_rejections:
            self.assertFalse(execution_contract(by_id[test_id]).numeric_generation_metrics_expected)

    def test_missing_quality_candidate_flag_is_rejected(self):
        payload = json.loads(MATRIX.read_text(encoding="utf-8"))
        row = next(item for item in payload["cases"] if item["test_id"] == "OV-02")
        row.pop("quality_required")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "matrix.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(
                ValueError, "quality_required must cover every Granite runtime candidate"
            ):
                load_matrix(path)

    def test_duplicate_and_unknown_values_are_rejected(self):
        payload = json.loads(MATRIX.read_text(encoding="utf-8"))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "matrix.json"
            payload["cases"].append(dict(payload["cases"][0]))
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "duplicate test id"):
                load_matrix(path)
            payload["cases"].pop()
            payload["cases"][0]["device"] = "magic"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "unknown device"):
                load_matrix(path)


if __name__ == "__main__":
    unittest.main()
