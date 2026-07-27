import unittest

from scripts.testing.official_openvino.matrix import FORMAL_METRICS
from scripts.testing.official_openvino.runner import (
    EXPECTED_REJECTION_METRIC_VALUE,
    expected_rejection_runtime_record,
    terminal_runtime_record,
    validate_runtime_records,
)


class OfficialOpenVINORunnerTests(unittest.TestCase):
    def test_terminal_record_populates_every_metric_with_status_and_source(self):
        row = terminal_runtime_record("OV-02", "conversion-memory-gate", "conversion.json")
        self.assertEqual(len(row["metrics"]), 13)
        self.assertTrue(all(value["status"].startswith("not-measured:") for value in row["metrics"].values()))
        self.assertTrue(all(value["source"] == "conversion.json" for value in row["metrics"].values()))
        audit = validate_runtime_records([row], {"OV-02"})
        self.assertFalse(audit["accepted"])
        self.assertEqual(audit["failed_count"], 1)

    def test_runtime_reconciliation_requires_every_expected_id(self):
        with self.assertRaisesRegex(ValueError, "missing runtime"):
            validate_runtime_records([], {"OV-01"})

    def test_complete_numeric_runtime_record_is_accepted(self):
        row = {
            "test_id": "OV-02",
            "terminal": False,
            "expected_outcome": "pass",
            "status": "passed",
            "source": "summary.json",
            "cleanup_process_count": 0,
            "pilot_passed": True,
            "warmup_excluded": True,
            "accepted_sample_count": 3,
            "metrics": {
                name: {
                    "value": (
                        {
                            "mean": 2.0,
                            "median": 2.0,
                            "peak": 3.0,
                            "count": 3,
                            "query_succeeded": True,
                        }
                        if name in {"cpu_percent", "gpu_percent"}
                        else 1.0
                    ),
                    "status": "measured",
                    "source": "summary.json",
                }
                for name in FORMAL_METRICS
            },
        }
        audit = validate_runtime_records([row], {"OV-02"})
        self.assertTrue(audit["accepted"])
        self.assertEqual(audit["accepted_count"], 1)
        self.assertEqual(audit["failed_count"], 0)

    def test_null_metric_never_counts_as_an_accepted_runtime(self):
        row = terminal_runtime_record("OV-02", "placeholder", "old.json")
        row["terminal"] = False
        row["status"] = "passed"
        row["expected_outcome"] = "pass"
        row["pilot_passed"] = True
        row["warmup_excluded"] = True
        row["accepted_sample_count"] = 3
        audit = validate_runtime_records([row], {"OV-02"})
        self.assertFalse(audit["accepted"])
        self.assertEqual(audit["failed_count"], 1)

    def test_expected_rejection_uses_explicit_non_numeric_value_and_hash(self):
        row = expected_rejection_runtime_record(
            "OV-TQ-19",
            "algorithm QJL rejected before launch",
            "attempt/rejection.json",
            "a" * 64,
        )
        self.assertTrue(all(
            metric["value"] == EXPECTED_REJECTION_METRIC_VALUE
            for metric in row["metrics"].values()
        ))
        audit = validate_runtime_records(
            [row], {"OV-TQ-19"}, expected_rejections={"OV-TQ-19"}
        )
        self.assertTrue(audit["accepted"])
        self.assertEqual(audit["expected_rejection_pass_count"], 1)

    def test_duplicate_and_unexpected_runtime_rows_are_rejected(self):
        row = terminal_runtime_record("OV-01", "placeholder", "old.json")
        with self.assertRaisesRegex(ValueError, "duplicate runtime"):
            validate_runtime_records([row, row], {"OV-01"})
        with self.assertRaisesRegex(ValueError, "unexpected runtime"):
            validate_runtime_records([row], {"OV-02"})


if __name__ == "__main__":
    unittest.main()
