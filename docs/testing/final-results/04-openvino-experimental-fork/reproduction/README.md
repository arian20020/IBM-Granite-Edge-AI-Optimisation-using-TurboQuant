# Release verification command

Run from the repository root. This validates the settled package through the supported contributor CLI. These checks do not rerun benchmarks or quality scoring, regenerate reports, invoke Word, or rewrite catalogs. They do not modify evidence.

```powershell
python -m scripts.testing.cli.validate_results --route experimental-openvino --output-root docs/testing/final-results
```

Internal normalizers and finalizers are provenance implementation details, not an additional reproduction command surface.

## Frozen inputs

- `experiments/raw-results/retained/openvino-experimental-fork/2026-08-30/fv6/experimental-openvino-detailed-results.csv` — SHA-256 `2f57390980664a79cdcc2272152fd316cd315360afd5ab3b715bba9fd70ebc9f`
- `experiments/raw-results/retained/openvino-experimental-fork/2026-08-30/fv6/experimental-openvino-quality-details.csv` — SHA-256 `834c2caa9c7eecc89043078ffbd9bba2b637b2ed82be8d412c6011c7f7fd978e`
- `outputs/openvino-experimental-fork-results/Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx` — SHA-256 `09820a5b19edb11efd7640f1d9a13802b82fb464ab6e14622998d2565c5aca83`
- The companion comparison, coverage, `rows.json`, 27 raw-result JSON files, and 48 hash-named prompt inputs under `experiments/raw-results/openvino-experimental-fork/2026-08-30/fv6/` are individually listed and hashed in `../evidence/evidence-index.csv`.

Validation only reads normalized evidence; it does not rerun inference or quality adjudication. Do not replace unavailable observations with zero, infer missing hardware, or copy values from another campaign.

The source XLSX is retained byte-identically under `../evidence/source/` as evidence-only/nonportable. The primary handoff is `../reports/openvino-experimental-fork-results.xlsx`; its adjacent provenance receipt records the 55 absolute-path replacements and source/output hashes. The 275 formulas have no cached results and may appear blank in non-calculating readers until Excel recalculation.

`../evidence/manifest-sha256.txt` is an exact receipt for the complete route file set. It is rebuilt only after the final file set is known.
