# Controlled Testing Workbooks

The six workbooks follow the exact route order, test IDs, planned configurations and result sections found in the supplied testing folder.

## Canonical source-controlled templates

The reviewable versions live under `text-templates/`:

1. `01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md`
2. `02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.md`
3. `03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md`
4. `04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
5. `05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.md`
6. `06_Cross_Route_Controlled_Comparison_Workbook_v1.md`

These Markdown files are canonical because Git can display and compare every field. The DOCX files are generated working artefacts rather than the only copy of the testing data.

## Generate the Word workbooks

```powershell
python .\scripts\testing\Generate-Controlled-Workbooks.py
```

The command writes the six `.docx` files to `docs/testing/workbooks/generated/`. Install the pinned dependency first when required:

```powershell
python -m pip install -r .\scripts\testing\requirements.txt
```

## Completion rule

A workbook section is complete only when its source run ID, processed-result path, evidence path and Git commit are recorded in `../Workbook-Completion-Register.csv`.

Do not type an important measured value only into Word. Enter it in the appropriate CSV register first so it remains machine-readable and traceable, then copy or generate it into the reporting workbook.
