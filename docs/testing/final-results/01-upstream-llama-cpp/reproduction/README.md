# Release verification command

Run from the repository root. This validates the settled package through the supported contributor CLI. These checks do not rerun benchmarks or quality scoring, regenerate reports, invoke Word, or rewrite catalogs. They do not modify evidence.

```powershell
python -m scripts.testing.cli.validate_results --route upstream-llama --output-root docs/testing/final-results
```

Internal normalizers and finalizers are provenance implementation details, not an additional reproduction command surface.
