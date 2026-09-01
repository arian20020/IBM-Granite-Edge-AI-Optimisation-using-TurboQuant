import unittest

from scripts.testing.campaigns.openvino.diagnostics import (
    classify_diagnostic,
    reconcile_diagnostics,
)


class OfficialOpenVINODiagnosticTests(unittest.TestCase):
    def test_success_requires_acceptance_activation_and_expected_allocation(self):
        record = {
            "test_id": "OV-TQS-01", "exit_code": 0, "accepted": True,
            "activated": True, "expected_bytes": 4096, "actual_bytes": 4096,
            "fallback": False, "requested_device": "CPU", "actual_device": "CPU",
            "evidence": ["probe.json"],
        }
        self.assertEqual(classify_diagnostic(record), "passed")
        record["actual_bytes"] = 8192
        self.assertEqual(classify_diagnostic(record), "allocation-mismatch")

    def test_nonzero_official_test_exit_is_failed(self):
        record = {
            "test_id": "OV-B10", "exit_code": 1, "accepted": True,
            "activated": True, "expected_bytes": 1, "actual_bytes": 1,
            "fallback": False, "requested_device": "CPU", "actual_device": "CPU",
            "evidence": ["official-test.log"],
        }
        self.assertEqual(classify_diagnostic(record), "official-test-failed")

    def test_reconciler_requires_all_ids_and_zero_failures(self):
        record = {
            "test_id": "OV-TQS-01", "exit_code": 0, "accepted": True,
            "activated": True, "expected_bytes": 64, "actual_bytes": 64,
            "fallback": False, "requested_device": "CPU", "actual_device": "CPU",
            "evidence": ["probe.json"],
        }
        self.assertTrue(reconcile_diagnostics([record], {"OV-TQS-01"})["accepted"])
        with self.assertRaisesRegex(ValueError, "missing diagnostic"):
            reconcile_diagnostics([record], {"OV-TQS-01", "OV-TQS-02"})
        failed = dict(record, exit_code=2)
        with self.assertRaisesRegex(ValueError, "failed diagnostic"):
            reconcile_diagnostics([failed], {"OV-TQS-01"})

    def test_missing_evidence_is_never_a_pass(self):
        record = {
            "test_id": "OV-TQS-01", "exit_code": 0, "accepted": True,
            "activated": True, "expected_bytes": 64, "actual_bytes": 64,
            "fallback": False, "requested_device": "CPU", "actual_device": "CPU",
            "evidence": [],
        }
        self.assertEqual(classify_diagnostic(record), "missing-evidence")

    def test_sourced_unsupported_boundary_is_terminal_not_failed(self):
        record = {
            "test_id": "OV-TQS-01", "exit_code": 0, "accepted": False,
            "activated": False, "expected_bytes": 0, "actual_bytes": 0,
            "fallback": False, "requested_device": "CPU", "actual_device": "CPU",
            "terminal_classification": "unsupported-by-source",
            "evidence": ["source-audit.json"],
        }
        self.assertEqual(classify_diagnostic(record), "unsupported-by-source")
        result = reconcile_diagnostics([record], {"OV-TQS-01"})
        self.assertTrue(result["accepted"])
        self.assertEqual(result["terminal_count"], 1)

    def test_nonallocation_capability_probe_can_pass_without_fake_bytes(self):
        record = {
            "test_id": "OV-B01", "exit_code": 0, "accepted": True,
            "activated": True, "proof_kind": "capability",
            "expected_bytes": 0, "actual_bytes": 0,
            "fallback": False, "requested_device": "host", "actual_device": "host",
            "evidence": ["python-openvino.json"],
        }
        self.assertEqual(classify_diagnostic(record), "passed")


if __name__ == "__main__":
    unittest.main()
