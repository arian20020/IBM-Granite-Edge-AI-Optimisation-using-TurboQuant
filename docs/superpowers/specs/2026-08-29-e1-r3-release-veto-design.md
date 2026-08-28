# E1 R3 independent release-veto design

## Scope

E1 owns R3-020 and supports R3-019, R3-021, and R3-022 at the final evidence and native-acceptance boundary. E1 does not repair producer-owned product defects. It independently evaluates R3-001 through R3-022 against one exact pushed C0 candidate and vetoes release whenever executable GREEN evidence is absent.

## Decision model

The evaluator has exactly three public dispositions:

- `CHANGES REQUIRED`: any known product defect remains, any issue lacks executable GREEN evidence, any locally runnable layer fails or is missing, or any identity/evidence join is invalid.
- `BLOCKED BY EXTERNAL ENVIRONMENT`: every locally runnable source, managed, integration, package-construction, identity, security, privacy, and cleanup check is GREEN; no product defect remains; only an independently proven external native or policy prerequisite is unavailable.
- `READY FOR CONTROLLED RELEASE REVIEW`: zero product-test failures, non-zero packaged discovery, every required authorized native journey passes, exact package/candidate identity is proven, zero owned descendants remain, and final E1 handoff/native receipts form a valid byte-for-byte join.

`CHANGES REQUIRED` takes precedence. Missing C0 candidate or producer evidence is not an external-environment disposition.

## Evidence contract

C0 supplies a sanitized R3 closure manifest committed at the exact candidate. It contains exactly one record for each stable ID R3-001 through R3-022. Every record declares whether a product defect remains and supplies at least one executable command result with positive discovery/execution/pass counts, zero failures, zero skips, and valid arithmetic. Evidence records bind a repository-relative report or manifest to exact Git blob bytes and SHA-256 at a separate evidence-subject commit/tree.

For production reachability, records that cover a new or corrected production API identify its definition, real production caller, composition/registration point, behavioral regression test, and an executable registration-count result equal to one. Manifest declarations alone do not turn a RED or missing behavioral result GREEN.

E1 verifies the manifest from the exact candidate Git object, not only the working tree. It also verifies candidate ancestry, candidate tree, pushed remote ref, report/evidence hashes and sizes, clean E1 worktree, and native receipt binding to the exact E1 handoff bytes. R2-or-earlier receipts are rejected.

## Runner order

Deterministic E1 tests may run on the provisional branch. Native stages require the exact C0 candidate and run in this order:

1. validate candidate commit/tree/remote ref and E1-only delta;
2. validate all producer handoffs, evidence manifests, native receipts, Git blobs, arithmetic, and R3 issue evidence;
3. build source, managed tests, integration tests, package construction, and E1 tests;
4. perform non-zero authoritative packaged discovery;
5. acquire the native lock atomically;
6. run Smoke, Failure, Acceptance, Restart, and RealModel journeys;
7. verify zero owned descendants before releasing the lock;
8. publish the E1 report, handoff, and native receipt only after all joins pass.

## Security and privacy

The implementation remains under `tests/E2ETests/**` plus the dated E1 audit report and these planning documents. It does not change application behavior, package signing, App Control, certificates, trust, firewall, models, or external tools. Committed evidence excludes usernames, hostnames, local paths, filenames, provider output, secrets, prompts, and model data. Raw TRX, screenshots, and UIA trees remain in ignored `TestResults` storage.

## Current environment disposition

The initial R3 preflight has no exact R3 C0 candidate, no handoff/native-receipt directories, no pinned SDK 10.0.301, and no installed exact candidate package or authorized native inputs. Therefore current evaluation is `CHANGES REQUIRED`; E1 may strengthen deterministic veto infrastructure but may not publish final receipts or claim native acceptance.
