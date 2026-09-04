import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "scripts/testing/Invoke-TurboVecPdfCampaign.ps1"


class PdfCampaignScriptTests(unittest.TestCase):
    def test_script_executes_expanded_suite_and_emits_hash_bound_manifest(self):
        shell = shutil.which("pwsh") or shutil.which("powershell")
        self.assertIsNotNone(shell)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "evidence"
            completed = subprocess.run(
                [shell, "-NoProfile", "-File", str(SCRIPT), "-OutputRoot", str(output)],
                cwd=ROOT, capture_output=True, text=True, timeout=60,
            )
            self.assertEqual(0, completed.returncode, completed.stderr + completed.stdout)
            manifest = json.loads((output / "manifest.json").read_text(encoding="utf-8-sig"))
            self.assertEqual(10, manifest["tests"]["discovered"])
            self.assertEqual(10, manifest["tests"]["passed"])
            self.assertEqual(0, manifest["tests"]["failed"])
            self.assertEqual(0, manifest["tests"]["skipped"])
            self.assertEqual(0, manifest["exit_code"])
            self.assertEqual(64, len(manifest["trx"]["sha256"]))
            self.assertTrue((output / manifest["trx"]["name"]).is_file())


if __name__ == "__main__":
    unittest.main()
