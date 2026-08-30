# A1 R4.1 Backend Fault-Boundary Remediation Plan

> **Execution authority:** `A1-R4.1-REMEDIATION-MASTER-PROMPT.md`; its design is already approved. This plan is the required pre-edit requirement-to-evidence map for branch `audit/ucl-a1-remediation-r4-1` in `C:\R4-A1-41`.

## Fixed identities and boundaries

- R4 base commit/tree: `3a1f2df54e21e0881b5f5f1e8d9b52ef33c279a6` / `94a637078e1e481329dac3d81da168f5e8111cce`.
- R4 implementation subject/tree: `4db647ac...` / `9a68aa...` (retain exact full identities in final evidence from Git).
- Branch/ref: `audit/ucl-a1-remediation-r4-1` / `refs/heads/audit/ucl-a1-remediation-r4-1`.
- No `MainWindow`, XAML, navigation-registration, project/solution-registration, package-manifest, Model Inspection schema/projection, Q1 result/publication/export, F1 styling/download, or S1 security-policy changes.
- Managed verification only on this non-authoritative machine. No Intel-native, device, performance, package-install, app-host, or screenshot acceptance claim unless direct authoritative evidence is actually available.

## Requirement-to-evidence matrix

| Requirement | RED proof on exact R4 base | Minimal production change | GREEN / mutation proof | Durable evidence |
|---|---|---|---|---|
| CRIT-001 expected Chat faults | Controller lifetime tests inject history I/O/runtime operational failures and assert no delayed retirement throw or raw retention | Introduce bounded typed Chat support status; classify only named operational failures | Operations complete safely; retirement awaits all phases; no raw message/path in retained/reported state | Subject-bound controller/filter TRX and ledger rows |
| CRIT-001 unexpected Chat faults | Inject programming faults in event and teardown phases | Narrow injectable privacy-safe reporter storing only stable code/classification; first report is idempotent | Exactly one report, bounded state, raw text/path absent, all cleanup phases run | Fault-boundary tests plus source/privacy scan |
| CRIT-001 retirement/import/shutdown | Concurrent/reentrant retirement, teardown fault, Import Model, and shell shutdown tests | One published retirement task; task-returning handlers; import cleanup in `try/finally`; shutdown continues cleanup | Same task observed, session disposed once, navigation exactly once, shutdown completes remaining phases | Controller/shell TRX; semantic caller tests |
| IMP-001 typed compatibility capture | Typed unavailable, exact-token cancellation, and unrelated programming-fault tests | Sealed operational unavailability with reason enum; orchestrator catches only that type | Expected fallback only; exact cancellation token preserved; arbitrary `InvalidOperationException` propagates | Compatibility orchestrator/source TRX |
| IMP-002 compatibility page reporting | Host-loaded page tests for expected typed unavailability and unexpected evaluator fault | Page owns injectable safe reporter; `async void` immediately awaits task method; safe presentation remains VM-owned | Expected case renders with zero reports; unexpected case renders and reports one stable safe code without raw data | Page/VM/fault-reporter TRX |
| IMP-003 transactional Chat creation | Factory tests inject verify/session/init failures before and after preparation, cancellation, cleanup failure, and success | `CreateInitializedProductionAsync` owns construction+initialization and cleans once on failure while preserving primary exception/token and safe cleanup fact | Both live callers receive only initialized owner; exactly-once disposal; primary failure preserved | Factory and semantic caller TRX |
| IMP-004 no production demo authority | Release-closure test finds demo constructor/session production type | Delete production demo constructor and `DemoGgufChatSession.cs`; keep any preview fake test-only | Release build/source semantic closure has no fake/preview/no-model Chat authority | Release build and closure test |
| IMP-005 semantic uniqueness | Mutation-style alternate construction/registration attempts violate observable invariant | Add behavior-first composition probes through live entry points; retain scans as supplemental | Exactly one compatibility authority, route-exact optimization authority, official OpenVINO worker authority, initialized Chat owner | A1 reachability/composition TRX |
| IMP-006 independent review | N/A until immutable subject exists | No production change | Independent read-only reviewer gets exact base/subject trees, diff, prompt, prior R4 report, callers; all Critical/Important findings resolved and re-reviewed if code changes | Committed review document |
| IMP-007 final repeat evidence | Reject all historical R4 Chat TRX as non-authoritative | N/A | Three consecutive full lifetime-filter runs, fresh distinct TRX, expected discovery/counts, times/bytes/SHA-256 | External manifest and verification ledger |

## TDD execution sequence

1. Add genuinely behavioral RED tests for the compatibility exception taxonomy and page boundary. Run only the smallest filters and retain failing output in the working transcript.
2. Add RED tests for Chat operation/retirement fault handling, transactionally initialized creation, import navigation, shutdown completion, and Release demo closure.
3. Add or strengthen semantic composition tests so alternative syntax cannot create a second live authority.
4. Implement the narrow compatibility operational exception and safe application-fault reporter; turn the compatibility tests GREEN.
5. Implement bounded Chat failure state, idempotent non-throwing retirement, task-returning handlers, and transactional initialized factory; turn Chat/shell tests GREEN.
6. Remove Release demo authority and turn Release-closure/semantic composition tests GREEN.
7. Run affected suites, full managed verification, Debug/Release x64 builds, package gate, and app-host probes with honest blockers/nonclaims.
8. Commit the immutable implementation subject. Generate all raw evidence only after that subject exists.
9. Run the complete Chat lifetime filter three consecutive times without sleeps, using a distinct TRX path each time.
10. Request genuinely independent read-only review against the immutable subject. Fix every Critical/Important finding; if code/tests change, create a new subject and invalidate/rerun affected evidence and review.
11. Create external raw evidence under `C:\UCL-AUDIT-HANDOFFS\R4-A1-R4-1\<subject>\`, SHA-256 manifest, committed verification ledger/report/review/receipt, schema validation, documentation-only final commit, push, and direct remote closure.

## Method-level C0 merge rules for anticipated shared edits

- `CompatibilityEvaluationOrchestrator.CaptureFreshAsync`: preserve H1's future fresh-source seam; change only the catch taxonomy to the A1-owned sealed operational type. C0 resolution keeps H1 provider logic and reapplies this exact catch boundary.
- `WindowsCompatibilityFreshResourcesSource.CaptureAsync`: preserve H1 hardware/storage acquisition; map only named inaccessible/unreadable/stale/inconsistent outcomes to the A1 reason enum, always preserving the caller token.
- `CompatibilityPage.Page_Loaded` / `ActivateAsync`: preserve F1 visual/rendering behavior and navigation; only route the async boundary through the reporter after the VM has published its safe presentation.
- `OnboardingShellPage` Chat launch/import/shutdown methods: preserve shell navigation, page ownership, and all non-Chat sequencing; replace only split Chat initialization and unsafe teardown awaits. C0 keeps other-owner shell changes and reapplies the transactional factory plus awaited retirement boundary.
- `GgufCurrentModelChatRouteLauncher.LaunchAsync`: preserve route validation and show callback; replace the two-step factory/initialize sequence with the transactional initialized factory.
- `A1BackendProductionReachabilityTests`: preserve supplementary full-tree scans while adding behavioral uniqueness assertions; do not convert other-owner composition into A1-owned registrations.

## Completion checks

- `git diff --check` zero and clean handoff status.
- Every claimed test/build row includes exact command, UTC start/end, exit code, arithmetic, artifact bytes/hash, disposition, blocker, and nonclaims.
- Receipt validates against supplied R4 v2 schema and uses `transport: "remote"`, `bundle: null`, `evidenceManifest: null`.
- Push only the assigned branch; direct `git ls-remote` equals local tip and tree; no merge/rebase/force-push/main modification.
