# Upstream and toolchain verification

Campaign: `turbovec-production-scale-final-evaluation-v2`

Pinned source: TurboVec 1.0.0 at commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`

## Outcome

All requested commands were executed. The repository baseline and both Rust test suites passed. The upstream Python suite reproduced one known Windows long-path failure. Strict Clippy failed because 38 warnings in the pinned upstream source were promoted to errors. No upstream source was changed to make these checks pass.

| Check | Discovered | Executed | Passed | Failed | Skipped | Exit |
|---|---:|---:|---:|---:|---:|---:|
| Repository non-TurboVec baseline | 111 | 111 | 111 | 0 | 0 | 0 |
| Upstream Python | 479 | 322 | 321 | 1 | 157 | 1 |
| Rust core release tests | 475 | 475 | 475 | 0 | 0 | 0 |
| Rust/Python release tests | 6 | 6 | 6 | 0 | 0 | 0 |
| Strict Clippy command | 1 | 1 | 0 | 1 | 0 | 101 |

The Python failure is only `test_atomic_save_round_trips_a_long_sidecar_name`; its platform classification is recorded in `windows-long-path-investigation.md`. Optional Python integrations and platform-specific cases account for the 157 skips.

Clippy was run as:

```text
cargo +1.89.0 clippy --release --locked --workspace --all-targets -- -D warnings
```

The diagnostics are maintainability/style findings such as empty lines after documentation comments, complex types, argument counts, range-loop suggestions and newer standard-library idioms. They are not evidence of a retrieval failure or a Critical/Important security defect, but strict lint cleanliness is not claimed.

## Toolchain identity

- `rustc 1.89.0 (29483883e 2025-08-04)`
- `cargo 1.89.0 (c24e10642 2025-06-23)`
- User-scoped minimal rustup toolchain; PATH was not modified by the installer.
- Official rustup endpoint installer: 12,721,664 bytes, SHA-256 `6f4bef66261261fcb43131be8720bab817d403a09edec7455c371974b90bdb7e`.
- Windows reported no Authenticode signature for the downloaded bootstrap executable. Installation still used the official HTTPS endpoint; no security control or signing policy was disabled.

## Evidence

The final manifest is `experiments/raw-results/turbovec/production-scale-v2/upstream/manifest.json` (5,609 bytes; SHA-256 `f633abecbcc87ac64bec4ebc76241a09a38bb683018f0ec82222ac7c967e759e`). It binds every stdout/stderr log by byte count and SHA-256. The earlier wrapper attempt is retained as `upstream-attempt-001-incomplete`; its child commands finished, but a PowerShell process-tree wait did not return, so it is not used for headline arithmetic.

## Interpretation

The upstream tests support TurboVec's core and Python-binding correctness under the pinned toolchain. They do not erase the Windows path limitation or the strict-lint failure. Neither finding is silently counted as a pass, and neither was repaired in third-party source during this campaign.
