import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider
from scripts.testing.turbovec.runner import run_fixture_campaign


ROOT = Path(__file__).resolve().parents[3]
ORCHESTRATOR = ROOT / "scripts/testing/Invoke-TurboVecFeasibility.ps1"
VALIDATOR = ROOT / "scripts/testing/Test-TurboVecEvidence.ps1"


def pwsh(script, *args):
    return subprocess.run(["pwsh", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script), *map(str,args)], cwd=ROOT, capture_output=True, text=True, check=False)


class EvidenceScriptTests(unittest.TestCase):
    def test_scripts_have_strict_safe_contract(self):
        for path in (ORCHESTRATOR, VALIDATOR):
            text=path.read_text(encoding="utf-8")
            self.assertIn("Set-StrictMode -Version Latest",text); self.assertIn("$ErrorActionPreference = 'Stop'",text)
            self.assertNotIn("Invoke-Expression",text)

    def test_orchestrator_rejects_relative_asset_paths(self):
        result=pwsh(ORCHESTRATOR,"-Mode","Preflight","-PythonExe","python.exe","-ModelRoot","relative-model","-TurboVecWheel","relative.whl","-OutputRoot","relative-output","-RunId","EXP-TV-COMP-001-20260902T120000Z-010")
        self.assertNotEqual(0,result.returncode)

    def test_validator_accepts_closed_run_and_rejects_corruption(self):
        with tempfile.TemporaryDirectory() as directory:
            run_id="EXP-TV-COMP-001-20260902T120000Z-011"; run_fixture_campaign(DeterministicEmbeddingProvider(),Path(directory),run_id); run=Path(directory)/run_id
            self.assertEqual(0,pwsh(VALIDATOR,"-RunDirectory",run).returncode)
            (run/"results.json").write_text("{}",encoding="utf-8")
            self.assertNotEqual(0,pwsh(VALIDATOR,"-RunDirectory",run).returncode)


if __name__=="__main__": unittest.main()
