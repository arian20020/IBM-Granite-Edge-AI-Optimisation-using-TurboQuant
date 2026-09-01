import tempfile
import unittest
from pathlib import Path


class AnimehackerBuildReconcileTests(unittest.TestCase):
    def test_ctest_summary_requires_zero_failed_tests(self):
        from scripts.testing.campaigns.animehacker.build_reconcile import parse_ctest_summary

        passed = parse_ctest_summary("100% tests passed, 0 tests failed out of 42")
        self.assertEqual(passed, {"total": 42, "failed": 0, "passed": 42})
        compact = parse_ctest_summary("100% tests passed out of 40")
        self.assertEqual(compact, {"total": 40, "failed": 0, "passed": 40})
        with self.assertRaisesRegex(ValueError, "failed tests"):
            parse_ctest_summary("98% tests passed, 1 tests failed out of 42")

    def test_required_binaries_must_exist_and_are_hashed(self):
        from scripts.testing.campaigns.animehacker.build_reconcile import inventory_binaries

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "llama-cli.exe").write_bytes(b"cli")
            (root / "llama-server.exe").write_bytes(b"server")
            result = inventory_binaries(root, ("llama-cli.exe", "llama-server.exe"))
            self.assertEqual({item["name"] for item in result}, {"llama-cli.exe", "llama-server.exe"})
            self.assertTrue(all(len(item["sha256"]) == 64 for item in result))
            with self.assertRaisesRegex(FileNotFoundError, "llama-bench.exe"):
                inventory_binaries(root, ("llama-bench.exe",))


if __name__ == "__main__":
    unittest.main()
