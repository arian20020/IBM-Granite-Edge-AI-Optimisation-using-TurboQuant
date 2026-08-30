# A1 R4.1 subject-bound verification ledger

Implementation subject: `7dbf42bd3a72ba8b11c8342ad0abb150e0268c3d`

Tree: `bf3c1da94688e50b673d660ff0b79a459ac26755`

External manifest: `C:\UCL-AUDIT-HANDOFFS\R4-A1-R4-1\7dbf42bd3a72ba8b11c8342ad0abb150e0268c3d\EVIDENCE-MANIFEST.json`

Manifest SHA-256/bytes: `c04f43b177e6eadfa88f84702070b0d5c73cd66354e6e770f1fe0a276b9749e1` / 21,372

All raw TRX/build logs were parsed outside Git and then overwritten with privacy-safe tombstones. The manifest hashes the 19 sanitized records, not raw machine-bearing output.

## Test ledger

| Record | Discovered | Executed | Passed | Failed | Skipped | Exit | Disposition |
|---|---:|---:|---:|---:|---:|---:|---|
| `compatibility.json` | 1,052 | 1,052 | 1,052 | 0 | 0 | 0 | Passed |
| `crossfeature.json` | 53 | 53 | 53 | 0 | 0 | 0 | Passed |
| `openvino-workerclient.json` | 16 | 16 | 16 | 0 | 0 | 0 | Passed |
| `gguf-workerclient.json` | 12 | 12 | 12 | 0 | 0 | 0 | Passed |
| `semantic-composition.json` | 25 | 25 | 25 | 0 | 0 | 0 | Passed; supplemental |
| `model-inspection-affected.json` | 17 | 17 | 17 | 0 | 0 | 0 | Passed |
| `compatibility-affected.json` | 27 | 27 | 27 | 0 | 0 | 0 | Passed |
| `chat-affected.json` | 56 | 56 | 56 | 0 | 0 | 0 | Passed |
| `model-inspection-contracts.json` | 357 | 357 | 335 | 22 | 0 | 1 | Inherited failures, unsuppressed |

Principal non-overlapping arithmetic excludes semantic overlap and repeat runs: 1,590 discovered/executed, 1,568 passed, 22 failed, 0 skipped.

## Consecutive final Chat lifetime runs

| Run | UTC start | UTC end | Count | Sanitized bytes | SHA-256 |
|---|---|---|---:|---:|---|
| 1 | `2026-08-30T16:44:35.4830417Z` | `2026-08-30T16:44:40.6003568Z` | 18/18 | 1,068 | `c7b5fac831cf59d5f7782512889b4402ca674fce5b626953bde1a9eb97df6c50` |
| 2 | `2026-08-30T16:44:41.8297802Z` | `2026-08-30T16:44:45.8055092Z` | 18/18 | 1,068 | `0addb1ca01e51a6f859374c578c273aaddcdad1f82fdc7fc286e56f6c235e85e` |
| 3 | `2026-08-30T16:44:47.3242651Z` | `2026-08-30T16:44:51.0694987Z` | 18/18 | 1,068 | `5a45725ee106ad110c68d6b117ae87356a07e724b1c1cbe69c2e40b9a732643d` |

The executions were consecutive, used the complete expected filter, and had no sleeps.

## Build, package, and app ledger

| Record | Exit | Warnings | Disposition |
|---|---:|---:|---|
| `build-app-debug.json` | 0 | 0 | Source/component Debug x64 passed |
| `build-app-release.json` | 0 | 0 | Source/component Release x64 passed |
| `build-tests-debug.json` | 0 | 27 occurrences | Passed; unchanged Model Import/Model Inspection fixture/analyzer paths only |
| `build-tests-release.json` | 0 | 14 occurrences | Passed; seven unique unchanged nullable sites repeated by build phases |
| `production-package-gate.json` | 1 | 0 | Blocked: missing external `GgufQuantizerStageDirectory` |
| `built-app-launch.json` | -532462766 | n/a | Blocked before window: Windows App SDK activation class not registered |
| `closure-scans.json` | 0 | n/a | Passed |

The source/component builds explicitly disable unavailable native packaging and therefore do not constitute package or native acceptance. The strongest package gate explicitly requires every native stage and fails closed. Because launch created no window, the smoke path and screenshot gate could not execute.

## Closure scans

- `git diff --check base..subject`: exit 0.
- Production `A1BackendProductionAuthorities.Shared` callers: 5.
- Named direct compatibility orchestrator constructions: 0.
- Named direct optimization factory constructions: 0.
- Unguarded production Chat initializer outside the authority root: 0.
- Official-worker installation calls outside the authority root: 0.
- Production `DemoGgufChatSession`: 0.
- Sensitive-pattern matches in added production lines: 0.
- Changed-path warnings: 0.
- Orphan Granite app/worker processes: 0.
- Worktree changes at code-subject verification: 0.

Seven fake path/provider strings occur only in tests as privacy sentinels. Durable records contain no username, hostname, absolute repository path, prompt/model data, provider output, raw exception message, stack trace, or secret.

The A1 receipt validates against the supplied R4 worker handoff receipt v2 schema.

## Nonclaims

No Intel-native, native-runtime, TurboQuant, OpenVINO, GGUF-runtime, package-install, hardware, performance, app-host, or screenshot acceptance is claimed from this machine.
