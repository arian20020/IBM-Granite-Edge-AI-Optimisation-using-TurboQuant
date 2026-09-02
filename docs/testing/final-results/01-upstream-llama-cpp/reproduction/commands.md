# Release verification commands

Run from the repository root in PowerShell. These commands validate the settled package; they do not rerun benchmarks, regenerate reports, invoke Word, or modify evidence.

```powershell
python -m scripts.testing.cli.validate_results --route upstream-llama --output-root docs/testing/final-results
python -m pytest scripts/testing/tests/integration/test_final_results_upstream_llama.py -q
```

The supported CLI is the contributor-facing command surface. Adapter internals are provenance context, not a second command interface.
