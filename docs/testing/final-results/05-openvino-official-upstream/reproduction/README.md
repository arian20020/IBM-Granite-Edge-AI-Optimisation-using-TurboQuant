# Release verification command

Run from the repository root. This validates the settled package through the supported contributor CLI. These checks do not rerun benchmarks or quality scoring, regenerate reports, invoke Word, or rewrite catalogs. They do not modify evidence.

```powershell
python -m scripts.testing.cli.validate_results --route official-openvino --output-root docs/testing/final-results
```

Internal normalizers and finalizers are provenance implementation details, not an additional reproduction command surface.

## Authority boundary

- `experiments/raw-results/retained/openvino-official-upstream/2026-08-30/fv2-missing-model-attempts/consolidated/official-openvino-detailed-results.csv` (ecd7ea7c9e71d235d39c4bd29c00b52a7bc4ccdb2b151b7cfe9b6ba4e84c9116) is authoritative for all 45 final statuses.
- `experiments/raw-results/retained/openvino-official-upstream/2026-08-30/fv1/experimental-openvino-quality-details.csv` (b74e4c0f599d7ef7eaf170a0bbf40cd25d9ddb2a56f232bd04a6036fe4aabdf5) and the indexed fv1 raw results supply observations only for the 15 fv2-passed cases.
- `outputs/openvino-official-upstream-results/Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx` (1d5fc2893e0c7f412140b3fa1a26c4a0c18e3c65ecfa356e80549dc4cd10aff7) is the revised workbook copied byte-identically into `../evidence/source/` as evidence-only/nonportable.
- `outputs/openvino-official-upstream-results/Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30.xlsx` (b3e26eae69c3854dec26536c6d141943572292f8a9ea331cb4de1d88c76b32b4) is indexed as prior evidence and is not duplicated.

The primary handoff is `../reports/openvino-official-upstream-results.xlsx`; its adjacent provenance receipt records the 15 absolute-path replacements and source/output hashes. Validation does not rerun inference or quality adjudication. Missing passed evidence, a published metric on a non-passed fv2 row, a source conflict, or a missing final failure manifest remains a blocking finding.
