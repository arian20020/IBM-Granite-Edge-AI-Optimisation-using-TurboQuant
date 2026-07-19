import json
import tempfile
import unittest
from pathlib import Path

from scripts.testing.official_openvino.matrix import load_matrix


ROOT = Path(__file__).resolve().parents[3]
MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"


class OfficialOpenVINOMatrixTests(unittest.TestCase):
    def test_matrix_contains_every_wb04_id_once(self):
        cases = load_matrix(MATRIX)
        expected = ({f"OV-B{i:02d}" for i in range(1, 13)} |
                    {f"OV-C{i:02d}" for i in range(1, 7)} |
                    {f"OV-{i:02d}" for i in range(1, 11)} |
                    {f"OV-TQS-{i:02d}" for i in range(1, 13)} |
                    {f"OV-TQ-{i:02d}" for i in range(1, 21)})
        self.assertEqual({case.test_id for case in cases}, expected)
        self.assertEqual(len(cases), len(expected))

    def test_formal_quality_and_guard_requirements_are_frozen(self):
        by_id = {case.test_id: case for case in load_matrix(MATRIX)}
        quality = {f"OV-TQ-{i:02d}" for i in range(1, 11)}
        self.assertEqual({key for key, case in by_id.items() if case.quality_required}, quality)
        self.assertEqual({key for key, case in by_id.items() if case.guard == "ram-2048-mib"},
                         {"OV-C04", "OV-C05", "OV-C06", "OV-07", "OV-08", "OV-09",
                          "OV-10", "OV-TQ-16", "OV-TQ-17"})
        self.assertEqual(by_id["OV-TQ-13"].contexts, (512, 2048, 4096, 8192))
        self.assertEqual(by_id["OV-TQ-14"].contexts, (512, 2048, 4096, 8192))

    def test_capability_sweep_preserves_all_independent_kv_combinations(self):
        sweep = [case for case in load_matrix(MATRIX) if case.phase == "capability"]
        combinations = {(case.k_algorithm, case.v_algorithm,
                         case.k_precision, case.v_precision) for case in sweep}
        self.assertEqual(len(sweep), 12)
        self.assertEqual(len(combinations), 12)

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
