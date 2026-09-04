import json
from pathlib import Path
import tempfile
import unittest

from scripts.testing.turbovec.final_package import validate_final_package


class FinalPackageTests(unittest.TestCase):
    def test_repository_package_is_complete_and_hash_bound(self):
        root = Path(__file__).parents[3]
        result = validate_final_package(root)
        self.assertEqual("BLOCKED", result["disposition"])
        self.assertEqual(13, result["required_document_count"])
        self.assertEqual(3, result["verified_formal_files"])

    def test_rejects_a_manifest_with_an_unsupported_disposition(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            report = root / "docs/testing/turbovec/production-scale-v2"
            report.mkdir(parents=True)
            (report / "evidence-manifest.json").write_text(
                json.dumps({"disposition": "MAYBE", "formal_runs": []}), encoding="utf-8"
            )
            with self.assertRaises(ValueError):
                validate_final_package(root)


if __name__ == "__main__":
    unittest.main()
