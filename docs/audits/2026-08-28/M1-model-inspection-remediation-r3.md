# M1 Model Inspection remediation R3

## Disposition

M1 owns R3-001 through R3-004 and supports R3-016, R3-019, R3-021, and
R3-022. The evidence subject is implementation commit
`e1657fcac13594014d18105558edd5cf8c2d35af` with tree
`66eb2720142fcc91265996dea64b24574fb616c8`. The authoritative integration
base is `a5ef3558334e50587889140dafba194853938765` with tree
`90c34ab009b744d7b00866fb93e8dbc86363f1b2`; the frozen ancestor is
`4748fe04f19afdf6b27c4c12502b84db325e7294` with tree
`fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.

The M1-managed implementation is importable, but the combined gate is mixed.
The shared application project still admits three Model Optimization fixture
assets as four evaluated item kinds in seven prohibited build contexts. M1 is
forbidden to edit shared project composition. C0 must apply or supersede the
exact apply-checkable proposal at
`docs/audits/2026-08-28/proposals/M1-R3-shared-project-fixture-boundary.diff`.
Until C0 consumes that correction, M1 does not claim the production/package
fixture boundary or combined user journey is green.

## Production-reachable changes

- `ModelInspectionProjectionFactory.CreateGgufHandle` is defined in
  `Features/ModelInspection/Infrastructure/ModelInspectionProjectionFactory.cs`.
  Its live caller is `ModelInspectionHandoffRegistry.TryIssue`; registration
  remains the single `ModelInspectionHandoffRegistry` instance owned by
  `OnboardingShellPage`. Reissue and exact issued-handoff registration use the
  same factory without generating a second identity. The headless regression
  project proves the exact run ID, handoff ID, digest, length, GGUF route,
  path privacy, and invalidation retirement.
- `ModelInspectionProjectionFactory.CreateOpenVino` is defined in
  `Features/OpenVinoRoute/Inspection/ModelInspectionProjectionFactory.OpenVino.cs`.
  Its single live caller is `OpenVinoRouteService.InspectAsync`, reached through
  the existing `ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService`
  registration. `StartSessionAsync` validates that projection against the
  exact issued handoff before consuming the path-bearing lease. The adverse
  regression mutates the projection identity and proves descriptor custody is
  retained when validation rejects it.
- Both adapters consume already-issued route handoffs. GGUF and OpenVINO
  parsing and validation remain route-specific; both emit the same strict,
  bounded `ModelInspectionProjectionV2` field semantics. No local path,
  filename, free-form metadata, prompt, model bytes, or user/machine identity
  enters that projection.

## R3 issue evidence

| Issue | RED evidence | Implementation and GREEN evidence |
| --- | --- | --- |
| R3-001 | The R2 manifest row declared 402 executed but 402 passed plus 7 skipped. | The committed validator rejects arithmetic, stale identity, byte/hash/blob, ancestry, remote, privacy, and cleanliness mutations; focused tests pass 17/17. |
| R3-002 | The source-allowlist regression demonstrated that unresolved imported expressions could be accepted. | Package evaluation now executes imported targets and requires concrete contained outputs; package/fixture focused tests pass 5/5. |
| R3-003 | The mutation added an unlisted production `DebugFixtures` root and the former oracle missed it. | Production-root discovery rejects the mutation. The real matrix now exposes 28 unauthorized Model Optimization rows rather than hiding them; the exact C0 proposal passes `git apply --check`. |
| R3-004 | The GGUF headless project could not compile because the factory depended on OpenVINO types, and the live OpenVINO lease exposed no projection. | Route adapters are split and both live routes retain the exact issued projection. GGUF passes 2/2; OpenVINO live and mutation tests passed within the 404/404 managed run before a later policy block. Shared projection protocol tests pass 16/16. |

## Managed verification

| Gate | Result |
| --- | ---: |
| Model Inspection contracts excluding three separately rerun rows | 407/407 passed |
| Separately rerun contract rows | 3 failed: 1 shared-composition row, 2 PowerShell execution-policy rows |
| Model Inspection transport | 27/27 passed |
| Model Inspection worker | 78/78 passed |
| Model Inspection worker client | 92 passed, 27 policy failures, 0 skipped |
| Worker-process structural/parser slice | 8/8 passed |
| Model/hardware compatibility | 1050/1050 passed |
| Hardware Inspection foundation | 202/202 passed |
| Deterministic LLamaSharp | 191/191 passed |
| GGUF live handoff projection | 2/2 passed |
| Projection protocol contract | 16/16 passed |
| Package evaluation plus new-root mutation | 5/5 passed |
| Cleanup plus execution boundary | 4/4 passed |
| Cleanup inventory | 692/692, verifier 3/3 |

The two contract privacy tests fail before the checked-in scanner starts
because the host launches Windows PowerShell under a machine policy that
disables scripts. The same scanner is checked in and its behavior remains
covered by the 407 passing contract rows; no policy setting was weakened.

The current OpenVINO assembly rebuild succeeded with zero errors, but VSTest
discovery was then blocked on the generated OpenVINO worker-client assembly by
Application Control error `0x800711C7`. An earlier same-source OpenVINO run
passed 404 tests with seven declared controlled-stage skips. It is not counted
as an exact-tip passing command. The worker-client run reached 92 passing rows;
all 27 failures resolve the generated protocol fixture through the same trust
gate before any session starts. Worker-process publishing repeated the same
policy-controlled MSBuild child-node termination; the unaffected structural
slice passes 8/8.

The application-only design-time compile succeeded with zero errors. The
packaged unit source then compiled with zero errors and 27 pre-existing
nullable/obsolete-test warnings. Direct loose execution of the two packaged
projection tests fails before test logic with Windows App SDK
`REGDB_E_CLASSNOTREG`; the headless route regressions provide executable
coverage without weakening package activation.

## Native disposition

`nativeDisposition` is `blocked`. H1's fresh R3 handoff and native receipts
were independently verified before M1 entered the shared-lock phase: their
phase closure, cleanup, zero post-process count, exact handoff byte/hash join,
committed record hashes and byte counts, Git identities, ancestry, remote tip,
and managed-ledger arithmetic all agree.

M1 then acquired the shared native lock, discovered exactly one committed
`ProductionWorkerNativeCompletionTests` case, and ran it against the only
available same-worktree production-worker output. The case failed before model
inspection with `worker_handshake_invalid`; cleanup found zero worker processes
and the lock was released. Inspection showed that output was an interrupted,
incomplete build: its worker runtime configuration file had zero bytes. The
pinned SDK 10.0.301 is unavailable (only 10.0.400 is installed), the system
volume had 2.31 GiB free, and prior fresh worker publication attempts were terminated at the
Application Control boundary. M1 did not weaken policy, substitute another
commit's worker, or count the failed handshake as native execution. No passing
M1 native phase receipt is published.

## Changed paths

The R3 range from the prior M1 tip adds or changes 25 paths: the two normative
schemas, executable evidence validator and contracts, evaluated package and
five-root fixture contracts/support, exact C0 proposal, split live projection
adapters and route wiring, GGUF/OpenVINO regressions, cleanup ledger/source
list, and the approved design/plan. No shared project, solution, navigation,
composition, package reference, or Q1 optimization file was edited.

## Non-claims and remaining blockers

- No native GGUF load, native OpenVINO validation, controlled visual capture,
  hosted CI, signed-package execution, or Application Control pass is claimed.
- No raw TRX is committed; test-result files remain ignored and outside the
  evidence manifest.
- C0 must consume the shared fixture-boundary proposal and rerun the real
  seven-context matrix.
- Environment administration must provide a policy-authorized managed test
  closure and a PowerShell policy that permits the checked-in privacy scanner.
- A policy-authorized, exact-subject production-worker build using the pinned
  SDK must be provisioned before the controlled native case can be rerun.
