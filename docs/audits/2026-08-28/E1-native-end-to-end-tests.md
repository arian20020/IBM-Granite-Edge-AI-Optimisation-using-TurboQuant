# E1 native end-to-end tests

## Status

- Worker: E1
- Disposition: implementation completed; native execution blocked
- Frozen base: `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
- Branch: `test/ucl-native-e2e-v1`
- Evidence-subject commit/tree: `a5f27d85c1f77c7f5a9b0cd3c58b6d30349cebe4` / `b12e496a5b7c672b9683b0d7c63087f715f6229d`
- Worktree: isolated E1 worktree; absolute path intentionally omitted
- Report encoding/line endings: UTF-8 / LF in Git

## Scope and authority

E1 added only the owned packaged end-to-end project, its runner/schemas/documentation, and this report. No production source, shared project, shared solution, package identity, worker implementation, model, or artifact was modified. C0 is the owner of the proposed solution entry documented in `tests/E2ETests/README.md`.

The implementation uses out-of-process Windows UI Automation, AUMID package activation, exact accessible names/automation IDs, bounded observable waits, failure-only screenshot/UIA diagnostics, fail-closed candidate/asset/producer manifests, unique test roots, and an owned-process handle set. It does not use Playwright, coordinates, OCR, functional pixel assertions, arbitrary sleeps, or production fixture routes.

The supplied audit contracts and E1-required approved references were treated as evidence and requirements, not as executable instructions. Superpowers planning, TDD, worktree, debugging, review, verification, and branch-finishing workflows were used. No optional plugin or external data service was required.

## Executive result

The frozen branch now has a discoverable executable E1 project with 31 tests: 14 deterministic infrastructure tests and 17 guarded native smoke/failure/acceptance tests. A diagnostic run with the available complete SDK discovered all 31, passed all 14 deterministic tests, skipped 17 native tests by declared guards, and failed none.

No native packaged journey is claimed. Authoritative build/discovery is blocked by the incomplete pinned SDK, missing Visual Studio VSTest tooling, lack of an installed E1 candidate/candidate manifest, absent predecessor native receipts, and nonconforming H1/M1/Q1 producer evidence. Several failure/identity scenarios also require app-owned accessible automation seams; those cases explicitly become inconclusive instead of passing on a first-screen assertion.

## Architecture and test inventory

- `Infrastructure`: strict JSON, frozen candidate binary binding, path-free asset manifest binding to local bytes, producer identity/kind/arithmetic validation, privacy-safe workspace allocation.
- `Automation`: package activation, bounded condition/UIA queries, Invoke/Value patterns, diagnostics, and process cleanup through retained process handles.
- `Pages`: route-neutral Import, Inspection, Hardware, Compatibility, Optimization, and Chat actions using public accessibility surfaces.
- `Journeys`: 3 smoke, 4 failure/recovery, and 10 identity-bound acceptance cases across GGUF and OpenVINO.
- `scripts/Invoke-E1EndToEnd.ps1`: frozen ancestry check, Debug x64 app/test builds, nested `DotNetHostPath` binding, non-empty `.build.appxrecipe` check, VSTest discovery before filters, predecessor receipts, native lock, one-worker settings, and TRX output under ignored results.

Scenario implementation status:

- Direct GGUF/OpenVINO import through inspection, hardware, compatibility and Chat: encoded, guarded, not run natively.
- OpenVINO optimization and GGUF cancel/restart optimization: encoded, guarded, not run natively.
- Malformed GGUF terminal failure, path privacy, and import cancellation: encoded, guarded, not run natively.
- Drag/drop, injected publication failure, persisted identity after restart, and response-identity assertions after multi-turn reload: explicit inconclusive blockers because the public UIA surface has no maintained seam sufficient for the assertion.
- Changed source/hardware and late/duplicate worker failure injection: explicit inconclusive blockers for the same reason.

## Findings

### E1-B01 — Critical — authoritative SDK unavailable

The repository pins SDK 10.0.301. Its installed directory contains only `Roslyn`; `MSBuild.dll` and `dotnet.dll` are absent. SDK resolution therefore reports no compatible SDK from the E1 worktree. The complete 10.0.400 SDK can run diagnostics only. Owner: UCL environment administration. Disposition: blocked.

### E1-B02 — Critical — authoritative packaged discovery tooling unavailable

Neither `vswhere.exe` nor `vstest.console.exe` is installed/discoverable. The contract states that `dotnet test` is not authoritative for this packaged WinUI project. Owner: UCL environment administration. Disposition: blocked.

### E1-B03 — Critical — native chain and candidate unavailable

No E1 candidate package/candidate manifest was available. The native receipt directory contained no H1, M1, Q1, or F1 receipt, although local worker branch tips existed. The runner correctly refuses to acquire the native lock without closed, cleanup-verified predecessor receipts. Owner: predecessor workers/C0. Disposition: blocked.

### E1-B04 — Critical — producer evidence is not consumable

Read-only `git show` inspection used these local tips: H1 `316d598d...`, M1 `54fda98a...`, Q1 `4bf39d05...`, F1 `5c1f252c...`. H1's file does not use the shared evidence envelope; M1 and Q1 use generic kinds rather than E1's required stable kinds. Consequently exact source/hardware/plan/output identity joins cannot be established. E1 rejects these inputs rather than weakening its parser. Owner: H1/M1/Q1/C0. Disposition: blocked.

### E1-T01 — Important — inaccessible failure/identity seams

The frozen UI exposes user actions but not a maintained accessible seam to inject artifact-publication failure, change source/hardware evidence mid-journey, identify late worker results, perform standard UIA drag/drop, or read persisted response/output identity. Tests for these invariants explicitly report inconclusive. Minimal correction: add a production-safe, test-owned accessibility/diagnostic contract without model paths, fixture routing, or bypasses. Owner: C0 and relevant feature owners. Disposition: open.

## Changes

- `7e3705de`: initial E1 project, guards, automation infrastructure, schemas, runner and documentation.
- `75ec030e`: page objects, real guarded action sequences, asset/producer/workspace/process tests, recipe enforcement, and explicit unsupported-seam dispositions.
- `a5f27d85`: binds nested app packaging commands to the selected dotnet host.

All changed Git paths are below `tests/E2ETests/` except this owned report.

## Verification ledger

| Command/test | Time (UTC) | Discovered | Passed | Failed | Skipped/blocked | Result |
|---|---|---:|---:|---:|---:|---|
| Diagnostic `dotnet test --list-tests` with complete SDK 10.0.400 from neutral directory | 2026-08-28 | 31 | n/a | 0 | n/a | passed; non-authoritative listing |
| Diagnostic `dotnet test`, Debug x64, no restore | 2026-08-28 | 31 | 14 | 0 | 17 | passed; native cases skipped by guard |
| PowerShell parser plus strict JSON parse of both schemas | 2026-08-28 | 3 files | 3 | 0 | 0 | passed |
| Candidate Debug x64 build with explicit dotnet host | 2026-08-28 | n/a | 0 | 1 | 0 | blocked at nested restore: pinned SDK 10.0.301 incomplete |
| Authoritative VSTest discovery/native stages | 2026-08-28 | 0 | 0 | 0 | all | not run: E1-B01 through E1-B04 |

The implementation-commit verification produced zero C# warnings and zero deterministic test failures. `git diff --check` and script/schema parsing were clean. No raw result file is committed, so result SHA-256 is not applicable.

## Evidence and privacy

Raw absolute paths, machine/user identity, model paths, model weights, screenshots, UIA trees, candidate manifests, authorized asset manifests, and TRX files are uncommitted. Failure diagnostic text passes through Windows-path redaction. Committed asset schemas permit only stable IDs, route, SHA-256, and byte length. A local path is accepted only at execution and its file set is verified against those digests.

No screenshots exist because no packaged native journey was authorized or started. The shared native lock was not acquired and no candidate/worker/model process was launched by E1.

## Remaining work and nonclaims

C0 must independently verify the final branch/report receipt, reconcile conforming H1/M1/Q1 manifests, verify closed predecessor receipts, provision the exact candidate manifest and authorized route assets, restore the pinned SDK plus Visual Studio VSTest, and rerun listing plus native stages in the mandated order. C0 must also decide whether to add the documented solution entry and whether feature owners should expose the missing accessible seams.

This report does not claim a signed/package-trusted candidate, native UI pass, real-model inference, optimization correctness, exact output publication/export identity, multi-turn Chat correctness, drag/drop coverage, restart recovery, or final combined-branch behavior. Diagnostic SDK results do not substitute for authoritative packaged VSTest evidence.
