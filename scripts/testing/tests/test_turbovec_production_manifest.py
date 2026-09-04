import hashlib
import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[3]
MANIFEST = ROOT / "experiments/manifests/turbovec/production-scale-v2/preflight.json"


class ProductionScalePreflightTests(unittest.TestCase):
    def test_preflight_closes_verified_identity_and_baseline_arithmetic(self):
        document = json.loads(MANIFEST.read_text(encoding="utf-8"))

        self.assertEqual("2.0", document["schema_version"])
        self.assertEqual("turbovec-production-scale-final-evaluation-v2", document["campaign_id"])
        self.assertEqual("EXP-TV-COMP-001", document["experiment_id"])
        self.assertEqual(
            {
                "branch": "test/ucl-turbovec-pdf-feasibility-v1",
                "commit": "07998fb7766887689a222dd0c8726d821fa33169",
                "tree": "ed68db942aa26c2e66218c10012c914a0e3c361e",
            },
            document["base"],
        )
        self.assertEqual(39, document["baseline_tests"]["controlled_python"]["passed"])
        self.assertEqual(5, document["baseline_tests"]["pdf_direct"]["passed"])
        self.assertEqual(0, document["baseline_tests"]["pdf_dotnet_bridge"]["discovered"])
        self.assertEqual(5, document["baseline_tests"]["pdf_dotnet_bridge"]["exit_code"])
        self.assertEqual(
            "EXP-TV-COMP-001-20260904T025427Z-090",
            document["diagnostic_reproduction"]["run_id"],
        )
        self.assertTrue(document["diagnostic_reproduction"]["quality_and_storage_match"])
        self.assertFalse(document["diagnostic_reproduction"]["formal_timing_evidence"])
        self.assertEqual(
            "b160db803175b71f6edb2b6a5beb922cc6a17bfb070f7cf800bb729119a5965a",
            document["governing_request"]["sha256"],
        )

        allowed = {
            "schema_version", "campaign_id", "experiment_id", "base", "execution_branch",
            "governing_request", "dependencies", "baseline_tests", "diagnostic_reproduction",
        }
        self.assertEqual(allowed, set(document))

    def test_recorded_external_artifact_hashes_are_well_formed(self):
        document = json.loads(MANIFEST.read_text(encoding="utf-8"))
        for key in ("terminal_sha256", "summary_sha256", "pdf_trx_sha256"):
            value = document["diagnostic_reproduction"][key]
            self.assertEqual(64, len(value))
            int(value, 16)


if __name__ == "__main__":
    unittest.main()
