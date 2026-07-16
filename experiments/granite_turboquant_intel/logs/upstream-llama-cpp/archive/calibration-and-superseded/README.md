# Calibration and Superseded Evidence

This archive retains upstream llama.cpp pilot, calibration and superseded measurement runs for auditability. Nothing here should be used as a current WB-01 result unless a later controlled revision explicitly restores it.

## Why these files are archived

- `UL-XX-METRICS-R001` used the earlier CLI/startup timing approach and was superseded by `UL-XX-SERVER-METRICS-R001`.
- `UL-01-METRICS-CALIBRATION-*` and `UL-01-TTFT-CALIBRATION-R001` document measurement development.
- `UL-01-R001` and `UL-01-R002` are pilots superseded by the formal `UL-01-R003` benchmark.
- `UL-13-QUALITY-R001` is superseded by the corrected `UL-13-QUALITY-R002` run.

The current results remain in the test-ID folders two levels above this directory. See [the upstream evidence guide](../../README.md).

`move-manifest.csv` records every source path, archive path, file count, total bytes and deterministic tree SHA-256 used to verify that this reorganisation did not change raw evidence.

Do not edit archived raw files to improve formatting or remove warnings. If sensitive data is ever discovered, stop publication and follow the repository redaction/change-control process rather than silently rewriting evidence.
