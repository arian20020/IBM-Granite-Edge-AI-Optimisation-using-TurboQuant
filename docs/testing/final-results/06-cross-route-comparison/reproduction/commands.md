# Release verification command

Run from the repository root. This reads the five canonical route packages and validates the guarded comparison without rerunning inference or rewriting catalogs.

```powershell
python -m scripts.testing.cli.validate_results --route cross-route --output-root docs/testing/final-results
```
