import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
MATRIX_PATH = (
    ROOT / "experiments" / "manifests" / "atomicbot-turboquant" / "retest-matrix.json"
)

EXPECTED_IDS = {
    *(f"AB-B0{index}" for index in range(1, 9)),
    "AB-01", "AB-02", "AB-03", "AB-KV3-F16-4K", "AB-04", "AB-05",
    "AB-06", "AB-07", "AB-08F", "AB-KV8-F16-4K", "AB-08Q", "AB-09",
    "AB-10", "AB-11", "AB-12", "AB-13", "AB-14", "AB-15", "AB-15M",
}


class AtomicBotMatrixTests(unittest.TestCase):
    def test_manifest_contains_every_controlled_workbook_case_once(self):
        from scripts.testing.campaigns.atomicbot.matrix import load_matrix

        cases = load_matrix(MATRIX_PATH)
        ids = [case.test_id for case in cases]
        self.assertEqual(set(ids), EXPECTED_IDS)
        self.assertEqual(len(ids), len(set(ids)))

    def test_duplicate_ids_are_rejected(self):
        from scripts.testing.campaigns.atomicbot.matrix import load_matrix

        payload = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        payload["cases"].append(payload["cases"][0])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "matrix.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "duplicate test_id"):
                load_matrix(path)

    def test_unknown_backend_and_guard_are_rejected(self):
        from scripts.testing.campaigns.atomicbot.matrix import load_matrix

        payload = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        payload["cases"][8]["backend"] = "magic"
        payload["cases"][8]["guard"] = "maybe"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "matrix.json"
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "backend"):
                load_matrix(path)

    def test_only_guarded_high_memory_cases_use_memory_guard(self):
        from scripts.testing.campaigns.atomicbot.matrix import load_matrix

        guarded = {case.test_id for case in load_matrix(MATRIX_PATH) if case.guard == "memory"}
        self.assertEqual(guarded, {"AB-KV8-F16-4K", "AB-15M"})

    def test_checkpoint_replaces_state_atomically_and_loads_it(self):
        from scripts.testing.campaigns.atomicbot.state import checkpoint, load_state

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "state.json"
            checkpoint(path, {"revision": 1, "rows": {"AB-01": "running"}})
            checkpoint(path, {"revision": 2, "rows": {"AB-01": "complete"}})
            self.assertEqual(
                load_state(path),
                {"revision": 2, "rows": {"AB-01": "complete"}},
            )
            self.assertEqual(list(path.parent.glob("*.tmp")), [])

    def test_checkpoint_status_is_strict(self):
        from scripts.testing.campaigns.atomicbot.state import checkpoint

        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "invalid row status"):
                checkpoint(
                    Path(directory) / "state.json",
                    {"revision": 1, "rows": {"AB-01": "probably"}},
                )


if __name__ == "__main__":
    unittest.main()
