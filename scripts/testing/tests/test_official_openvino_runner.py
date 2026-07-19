import unittest

from scripts.testing.official_openvino.runner import terminal_runtime_record, validate_runtime_records


class OfficialOpenVINORunnerTests(unittest.TestCase):
    def test_terminal_record_populates_every_metric_with_status_and_source(self):
        row = terminal_runtime_record("OV-02", "conversion-memory-gate", "conversion.json")
        self.assertEqual(len(row["metrics"]), 13)
        self.assertTrue(all(value["status"].startswith("not-measured:") for value in row["metrics"].values()))
        self.assertTrue(all(value["source"] == "conversion.json" for value in row["metrics"].values()))

    def test_runtime_reconciliation_requires_every_expected_id(self):
        with self.assertRaisesRegex(ValueError, "missing runtime"):
            validate_runtime_records([], {"OV-01"})


if __name__ == "__main__":
    unittest.main()
