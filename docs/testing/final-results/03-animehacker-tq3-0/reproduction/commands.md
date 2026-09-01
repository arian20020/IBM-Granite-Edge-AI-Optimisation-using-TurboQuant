# Ordered reproduction commands

1. Normalize and render
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.llama_adapter import write_animehacker_route; write_animehacker_route(root)"
```
2. Export the owned Word PDF
```powershell
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/testing/cli/export_report.ps1 -DocxPath docs/testing/final-results/03-animehacker-tq3-0/reports/animehacker-tq3-0-report.docx -PdfPath docs/testing/final-results/03-animehacker-tq3-0/reports/animehacker-tq3-0-report.pdf -TimeoutSeconds 180
```
3. Finalize and validate
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.llama_adapter import finalize_animehacker_route; finalize_animehacker_route(root)"
```
4. Validate manifest
```powershell
& .tools/python311-portable/python.exe -c "from pathlib import Path; from scripts.testing.reporting.evidence import validate_sha256_manifest; root=Path.cwd(); errors=validate_sha256_manifest(root,root/'docs/testing/final-results/03-animehacker-tq3-0/evidence/manifest-sha256.txt'); print(errors); raise SystemExit(bool(errors))"
```
5. Run focused validation
```powershell
& .tools/python311-portable/python.exe -m pytest scripts/testing/tests/integration/test_final_results_animehacker.py -q
```
