import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MATRIX_PATH = (
    ROOT / "experiments" / "manifests" / "animehacker-tq3-0" / "retest-matrix.json"
)

EXPECTED_IDS = {
    *(f"AH-B0{index}" for index in range(1, 9)),
    *(f"AH-{index:02d}" for index in range(1, 11)),
}
RUNTIME_METRICS = {
    "peak_ram_mb",
    "peak_private_bytes_mb",
    "available_ram_min_mb",
    "kv_mb",
    "ttft_ms",
    "prompt_tps",
    "decode_tps",
    "generation_duration_ms",
    "cpu_mean_pct",
    "cpu_median_pct",
    "cpu_peak_pct",
    "gpu_mean_pct",
    "gpu_median_pct",
    "gpu_peak_pct",
    "gpu_memory_peak_mb",
    "device_placement",
    "cleanup_result",
}


class AnimehackerMatrixTests(unittest.TestCase):
    def test_manifest_contains_every_controlled_workbook_case_once(self):
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        cases = load_matrix(MATRIX_PATH)
        ids = [case.test_id for case in cases]
        self.assertEqual(set(ids), EXPECTED_IDS)
        self.assertEqual(len(ids), 18)
        self.assertEqual(len(ids), len(set(ids)))

    def test_duplicate_ids_are_rejected(self):
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        payload = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        payload["cases"].append(payload["cases"][0])
        self._assert_invalid(payload, "duplicate test_id")

    def test_unknown_backend_status_and_guard_are_rejected(self):
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        payload = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        for field, value, message in (
            ("backend", "magic", "backend"),
            ("status", "probably", "status"),
            ("guard", "maybe", "guard"),
        ):
            changed = json.loads(json.dumps(payload))
            changed["cases"][8][field] = value
            with self.subTest(field=field):
                self._assert_invalid(changed, message)

    def test_all_8b_runtime_rows_are_memory_guarded(self):
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        guarded = {
            case.test_id
            for case in load_matrix(MATRIX_PATH)
            if case.guard == "memory"
        }
        self.assertEqual(guarded, {"AH-06", "AH-07", "AH-10"})

    def test_runtime_rows_require_complete_measurements_and_quality(self):
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        runtime = [case for case in load_matrix(MATRIX_PATH) if case.phase == "runtime"]
        self.assertEqual(len(runtime), 10)
        for case in runtime:
            with self.subTest(test_id=case.test_id):
                self.assertTrue(RUNTIME_METRICS.issubset(case.required_metrics))
                self.assertTrue(case.quality_required)
                self.assertEqual(case.formal_repetitions, 3)
                self.assertTrue(case.excluded_warmup)

    def test_tq3_rows_require_runtime_activation_proof(self):
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        tq3 = [case for case in load_matrix(MATRIX_PATH) if case.format == "TQ3_0"]
        self.assertGreater(len(tq3), 0)
        for case in tq3:
            with self.subTest(test_id=case.test_id):
                self.assertEqual(case.activation_requirement, "runtime-or-binary-source-linked")

    def test_controlled_gpu_rows_use_sycl_partial(self):
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        cases = {case.test_id: case for case in load_matrix(MATRIX_PATH)}
        for test_id in ("AH-08", "AH-09", "AH-10"):
            self.assertEqual(cases[test_id].backend, "sycl-partial")

    def test_checkpoint_replaces_state_atomically_and_rejects_unknown_status(self):
        from scripts.testing.campaigns.animehacker.state import checkpoint, load_state

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "state.json"
            checkpoint(path, {"revision": 1, "rows": {"AH-01": "running"}})
            checkpoint(path, {"revision": 2, "rows": {"AH-01": "passed"}})
            self.assertEqual(
                load_state(path),
                {"revision": 2, "rows": {"AH-01": "passed"}},
            )
            self.assertEqual(list(path.parent.glob("*.tmp")), [])
            with self.assertRaisesRegex(ValueError, "invalid row status"):
                checkpoint(path, {"revision": 3, "rows": {"AH-01": "probably"}})

    def _assert_invalid(self, payload: dict, message: str) -> None:
        from scripts.testing.campaigns.animehacker.matrix import load_matrix

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "matrix.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, message):
                load_matrix(path)


if __name__ == "__main__":
    unittest.main()
