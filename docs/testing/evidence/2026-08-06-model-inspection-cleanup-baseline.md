# Model Inspection Cleanup Phase 0 Baseline Evidence

**Planned evidence date:** 2026-08-06  
**Executed and closed:** 2026-08-07  
**Branch:** `refactor/model-inspection-cleanup`  
**Stacked base branch:** `feature/model-inspection-worker-host`  
**Stacked base commit:** `a4138a613dd643abe12858eec5d1c3beb09e95e7`  
**Tested behaviour head:** `502f396daa213d857d86a656a2cd31e6adb93b9d`  
**Permanent verification workflow:** `Build and test`  
**Exact behaviour-baseline run:** `31186340296`  
**Exact behaviour-baseline job:** `92891719332`

## Purpose

This record closes the executable baseline portion of Phase 0 for the complete Model Inspection cleanup programme. Its purpose is to prove the behaviour, test, process-containment, privacy and inventory state that later cleanup phases must preserve.

Phase 0 does not refactor production Model Inspection code. It establishes a trustworthy starting point, a complete review inventory and fail-closed CI checks before structural cleanup begins.

The evidence filename retains the date fixed by the approved Phase 0 plan. Execution completed on 2026-08-07 after the GitHub Actions outage and the inventory-ordering defect were resolved.

## Scope boundary

The cleanup programme is stacked on `feature/model-inspection-worker-host` and covers the complete Model Inspection scope introduced or changed through PRs #44, #45 and #47.

The Phase 0 branch diff was reviewed against base commit `a4138a613dd643abe12858eec5d1c3beb09e95e7`. Phase 0 changes are limited to cleanup plans, review inventory/evidence and executable CI/contract verification. No production Model Inspection C#, XAML, protocol representation, worker runtime implementation, WorkerClient runtime implementation or LLamaSharp feasibility behaviour is refactored by this phase.

## Inventory completeness

The permanent cleanup inventory contains:

| Check | Result |
|---|---:|
| Expected Model Inspection source paths | 367 |
| Recorded source paths | 367 |
| Review ledger rows | 367 |
| Duplicate source paths | 0 |
| Missing source files | 0 |
| Source/ledger ordering mismatches | 0 |
| Initial rows outside `BeforeRefactoring` | 0 |

The source list is compared with `StringComparer.Ordinal`, is required to be unique, and must contain only existing files. The review ledger must contain exactly the same 367 paths in the same order. Generated `bin` and `obj` build products are excluded from source discovery.

The one-time inventory repair preserved the same source set and matching ledger rows while normalising them to deterministic ordinal order. Temporary repair workflows were removed after use and are not part of the permanent workflow set.

## Contract discovery floor red-green proof

The permanent workflow originally retained a minimum contract-test floor of 78 after the contract project had grown to 82 mandatory tests. Phase 0 tightened this fail-closed guard using an explicit red-green regression.

### Red

- Test-only commit: `3161f1d7bc24da0e354a59ea81bef2a73bec680d`
- Workflow run: `31185923219`
- Job: `92890296173`
- Discovered: 82
- Passed: 80
- Failed: 2
- Skipped: 0

Both failures were expected and proved the same mismatch: the regression test required `--minimum-expected-tests 82` while the workflow still declared 78.

### Green

- Workflow correction commit: `502f396daa213d857d86a656a2cd31e6adb93b9d`
- Correction: `--minimum-expected-tests 78` → `--minimum-expected-tests 82`
- No category filter was reintroduced
- The dedicated contract project continued to execute as one complete suite

This raises the minimum floor to the current mandatory contract count instead of allowing four contract tests to disappear silently.

## Exact-head behaviour baseline

Permanent `Build and test` run `31186340296` executed on tested behaviour head `502f396daa213d857d86a656a2cd31e6adb93b9d` and completed successfully.

Every required permanent workflow stage completed successfully:

1. sparse checkout of required build inputs
2. generated GGUF fixture reproducibility check
3. short-path build staging
4. .NET SDK setup
5. x64 MSBuild setup
6. tool-version capture
7. Gate 2 evidence-directory preparation
8. Model Inspection contract restore and test execution
9. Gate 2 project restore and build
10. separate production-worker and abnormal-fixture publish
11. transport tests
12. production worker-host tests
13. WorkerClient tests
14. real worker-process tests
15. WinUI application restore and build
16. packaged application/unit-test restore, build and execution
17. orphan worker/fixture process check
18. retained-evidence privacy scan
19. Gate 2 evidence artifact upload
20. packaged unit-test artifact upload

## Exact TRX results

Both artifacts from run `31186340296` were downloaded and their TRX files were parsed independently rather than relying only on the workflow badge.

| Test layer | Total | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| Contracts | 82 | 82 | 82 | 0 | 0 |
| Transport | 20 | 20 | 20 | 0 | 0 |
| Worker host | 4 | 4 | 4 | 0 | 0 |
| WorkerClient | 86 | 86 | 86 | 0 | 0 |
| Worker process integration | 27 | 27 | 27 | 0 | 0 |
| Packaged WinUI/application unit tests | 207 | 207 | 207 | 0 | 0 |
| **Total** | **426** | **426** | **426** | **0** | **0** |

The packaged WinUI/application test run therefore remained green after the Phase 0 CI and inventory changes.

## Process containment and privacy

Run `31186340296` completed the permanent post-test safeguards successfully.

- `Check for orphaned Gate 2 processes`: passed
- Remaining `GraniteEdgeAI.ModelInspection.Worker` processes: 0
- Remaining `GraniteEdgeAI.ModelInspection.ProtocolTestWorker` processes: 0
- `Scan Gate 2 evidence for sensitive content`: passed
- Retained Windows user-path pattern findings: 0
- Retained bearer-token pattern findings: 0
- Retained API-key/password/secret pattern findings: 0

The safeguards execute under `always()` so they are not skipped merely because an earlier test fails.

## Retained artifacts

GitHub retained both expected exact-run artifacts.

| Artifact | ID | GitHub size | SHA-256 |
|---|---:|---:|---|
| `gate2-verification-31186340296-1` | `8997334224` | 44,617 bytes | `43d132c0dab489aed98df439c5e5e4743f4cf6c12ed3acea4420211097555380` |
| `unit-test-results-31186340296-1` | `8997334791` | 47,445 bytes | `91a19770525c4435809d323ca5d76c676ae4a58fe82bf907e8ee946edd608f25` |

The downloaded archives reproduced the same SHA-256 digests reported by GitHub.

The Gate 2 artifact contains the five Gate 2 TRX files and the publish manifest. The unit-test artifact contains the packaged WinUI/application TRX file.

## Published process identity evidence

The exact-run publish manifest records:

```text
source_commit=502f396daa213d857d86a656a2cd31e6adb93b9d
worker_file=GraniteEdgeAI.ModelInspection.Worker.exe
worker_sha256=C78D6B384DB56369B4B4EFEAD115FD88359A947B401B150A3FF34154AEE5C7D8
fixture_file=GraniteEdgeAI.ModelInspection.ProtocolTestWorker.exe
fixture_sha256=2E225624AC7F583FB50A95284B32E2AC8DC946655641768CC20EF70CF61FF207
```

The production worker and abnormal process fixture therefore remain separately published and separately identifiable.

## Behaviour intentionally not changed

Phase 0 does not change:

- worker protocol version 1 or serialized JSON meaning
- diagnostic-code meaning or first-failure precedence
- cooperative cancellation or forced-termination semantics
- startup, overall or cancellation-grace timeout meaning
- Windows Job Object containment
- inherited-handle allowlist
- executable containment or x64 verification
- environment allowlist
- bounded strict UTF-8 framing
- bounded retained stderr with continued draining
- model-path, chat-template, exception-chain or artifact privacy controls
- production/test-fixture separation
- current Model Inspection WinUI presentation/navigation behaviour
- real-model evidence or model-byte integrity rules

## Known limitation carried forward

The production worker still uses the controlled unavailable inspection engine established by Gate 2. Phase 0 does not connect the real LLamaSharp inspection engine, classifier, application service, ViewModel execution or packaging closure. Those are later implementation gates and must not be inferred from this cleanup baseline.

## Evidence chronology and exact-head closure

This document records the tested behaviour baseline at commit `502f396daa213d857d86a656a2cd31e6adb93b9d`. Committing an evidence document necessarily advances the Git branch head, so the document does not claim to contain its own commit SHA.

After this evidence record is committed, the permanent `Build and test` workflow must run once more on that documentation-only branch head. The resulting exact-head closure run is recorded in the stacked cleanup pull request. Phase 1 must not begin unless that final run is successful.

This avoids a recursive evidence-edit cycle while still preserving exact traceability between tested behaviour, the evidence record, and the final branch-head verification.
