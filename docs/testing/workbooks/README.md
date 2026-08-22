# Controlled Testing Workbooks

The controlled workbook set preserves route order, test identities, planned configurations, evidence references, failure states and bounded conclusions for the Granite–TurboQuant investigation.

## Canonical sources

Git-reviewable workbook content is stored under `text-templates/`. Generated DOCX files under `generated/` are deterministic working/reporting artefacts, not the only authoritative record. The unified workbook catalogue is `Controlled-Workbook-Manifest.csv`.

## Workbook set

| Workbook | Stage/scope | Initial or current role |
| --- | --- | --- |
| WB-01 | Upstream llama.cpp | Dependable GGUF control baseline |
| WB-02 | AtomicBot TurboQuant | Existing TurboQuant comparator |
| WB-03 | animehacker TQ3_0 | Existing TQ3 comparator |
| WB-04 | Official OpenVINO | Route A merged controls and official comparisons |
| WB-05 | Custom OpenVINO | Route B experimental controls and 36-pair matrix |
| WB-06 | Cross-route | Existing route-level comparison |
| WB-07 | D1 | Codec conformance and storage reconciliation |
| WB-08 | D2 | Diagnostic K/V capability sweep and measured memory order |
| WB-09 | E1 | Granite 3B context frontier and formal evaluation |
| WB-10 | E2 | Granite 8B safety and feasibility gate |
| WB-11 | E3 | Granite 30B lowest-weight-first bounded feasibility |
| WB-12 | E4 | Asymmetric, cross-family, ablation and repeatability tests |
| WB-13 | F | Independent evidence ingestion and final closure |

## Post-C shared controls

- `../Workbook-05-Post-C-Execution-Index-v1.csv` — 372 controlled execution and closure records;
- `../Workbook-05-Post-C-Configuration-Matrix-v1.csv` — route/configuration admission and storage controls;
- `../Workbook-05-Post-C-Evidence-Register-v1.csv` — append-only independent evidence intake;
- `../Workbook-05-Post-C-Revision-Register-v1.csv` — WB-07 through WB-13 revision history;
- `Workbook-05-Post-C-Pack-Manifest-v1.json` — pack membership, dependencies and non-claims.

## Generate and validate

```powershell
python -m pip install -r .\scripts\testing\requirements.txt
python .\scripts\testing\workbook05\bootstrap_post_c_workbooks.py --repository-root .
powershell -File .\scripts\testing\Validate-Workbook05-PostC-Workbooks.ps1
```

The workbook pack is only an execution structure until independently validated hardware artifacts populate it. Blank measured fields mean **not measured**, never zero.
