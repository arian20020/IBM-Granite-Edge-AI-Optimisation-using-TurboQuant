import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "scripts/testing/Invoke-TurboVecUpstreamVerification.ps1"


class UpstreamVerificationContractTests(unittest.TestCase):
    def test_runner_is_fail_closed_and_reconciles_every_command(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("Set-StrictMode -Version Latest", text)
        self.assertIn("$ErrorActionPreference = 'Stop'", text)
        self.assertIn("ccab9f325e6ce2a270a87daf01ae4e443bcf2d49", text)
        self.assertIn("1.89.0", text)
        self.assertIn("discovered", text)
        self.assertIn("attempted", text)
        self.assertIn("executed", text)
        self.assertIn("passed", text)
        self.assertIn("failed", text)
        self.assertIn("skipped", text)
        self.assertIn("blocked", text)
        self.assertIn("unexecuted", text)
        self.assertIn("Arithmetic mismatch", text)
        self.assertNotIn("Invoke-Expression", text)


if __name__ == "__main__":
    unittest.main()
