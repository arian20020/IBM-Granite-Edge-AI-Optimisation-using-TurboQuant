# Upstream llama.cpp Source and CPU Build Evidence

## Scope

This evidence covers the controlled upstream `llama.cpp` preparation and
CPU build checkpoints completed before repository test execution.

It does **not** claim that UL-B04 repository tests, IBM Granite inference,
performance measurement, quality evaluation, or TurboQuant activation have
passed.

## Controlled identities

| Item | Value |
|---|---|
| Route | `upstream-llama-cpp` |
| Workbook | `WB-01` |
| Environment | `ENV-20260714-INTEL-LAPTOP-01` |
| Repository | `REPO-UPSTREAM-LLAMA-CPP-B9870` |
| Source tag | `b9870` |
| Source commit | `2d973636e292ee6f75fadcf08d29cb33511f509f` |
| Corrected build | `BUILD-UL-CPU-B9870-002` |
| Corrected configuration | `CONFIG-UPSTREAM-CPU-X64-002` |
| Testing branch | `testing/upstream-llama-cpp-formal-execution` |

## Completed checkpoints

| Run | Result | Meaning |
|---|---|---|
| `UL-B01-R002` | Failed | Initial harness/native stderr handling failure preserved. |
| `UL-B01-R003` | Failed | Empty-string evidence helper failure preserved. |
| `UL-B01-R004` | Passed | Exact source tag, commit, remote and clean-tree gate passed after Windows long-path recovery. |
| `UL-B02-R001` | Passed | Initial CPU configuration completed. |
| `UL-B03-R001` | Failed | Optional prebuilt UI asset embedding failed because `loading.html` was missing. |
| `UL-B02-R002` | Passed | Corrected clean CPU configuration disabled local and prebuilt UI provisioning. |
| `UL-B03-R002` | Passed | Corrected x64 CPU Release build produced all required binaries. |

## Required binaries

| Binary | Size (bytes) | SHA-256 |
|---|---:|---|
| `llama-cli.exe` | 7208448 | `bb911e3d75f79e477f91b0478af3abdd68c50fa157d7b9dcacb249ce4e2d0b3c` |
| `llama-server.exe` | 8165888 | `eeca81a8ac1c6e1e3f550498dc4eb26c5c0e42f84a9eecfb4f5a188a99efc78e` |
| `llama-bench.exe` | 4174848 | `fa8676d9d08a14c668fed1d449dd85b72aeb9f93a337695c50919607c3f19f3b` |
| `llama-perplexity.exe` | 5732352 | `7fbbc0f0e51eacc8f198e8fad2af3184c1622d7d19e056de1c98f3a16955ac65` |

## Primary evidence

- `experiments/granite_turboquant_intel/manifests/environments/ENV-20260714-INTEL-LAPTOP-01/`
- `experiments/granite_turboquant_intel/manifests/runs/upstream-llama-cpp/`
- `experiments/granite_turboquant_intel/manifests/builds/upstream-llama-cpp/BUILD-UL-CPU-B9870-002/`
- `experiments/granite_turboquant_intel/logs/upstream-llama-cpp/`
- `experiments/granite_turboquant_intel/notes/upstream-llama-cpp/`
- `docs/testing/Environment-Register.csv`
- `docs/testing/Repository-Register.csv`
- `docs/testing/Build-Register.csv`
- `docs/testing/Test-Run-Register.csv`
- `docs/testing/Evidence-Index.csv`
- `docs/testing/Failure-Register.csv`

## Validation result

The pre-UL-B04 validator reported:

- 66 passed checks;
- 0 warnings;
- 0 failed checks;
- all generated JSON manifests parsed;
- the corrected CMake cache contained all 14 required settings;
- all four binary sizes and SHA-256 hashes matched the inventory;
- no model or compiled binary was placed inside the project repository;
- no whitespace errors or conflict markers were present.

## Remaining work

1. Commit and push this evidence checkpoint.
2. Run `UL-B04` repository-provided tests.
3. Update these registers again immediately after UL-B04.
4. Update canonical `WB-01` and regenerate its controlled DOCX.
5. Begin model/runtime tests only after build and repository-test gates pass.
