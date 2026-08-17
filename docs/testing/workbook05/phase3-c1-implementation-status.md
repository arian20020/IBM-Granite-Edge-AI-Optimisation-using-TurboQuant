# Workbook 05 Phase 3 C1 implementation status

**Campaign:** `GTQ-WB05-MF-v1`  
**Route:** `route-a-merged-openvino`  
**Package:** C1 — immutable model and conversion assets  
**C1 control pull request:** `#69`  
**Dependency-preflight pull request:** `#72`  
**Status:** Tasks 1–12 repository changes implemented; exact-head verification and Task 12 closure blocked by GitHub billing/spending restriction; live dependency and C1 acceptance remain pending

## Current checkpoint states

- Dependency-preflight Tasks 1–12 changes: **Implemented**
- Exact-head repository verification and Task 12 closure: **Blocked by GitHub billing/spending restriction**
- Live dependency-preflight acceptance: **Pending**
- Live C1 asset locking: **Blocked**

`Implemented` means that the proposed code, closed schemas, deterministic
fixtures, tests, workflow controls, independent validator, and operator guidance
are present on the pull-request branch. It does not mean that Task 12 is closed or
that the branch has passed the required exact-head gates. GitHub is currently
refusing those hosted jobs because of the account billing/spending restriction, so
PR `#72` remains Draft and must not be approved or merged yet. Once that
infrastructure blocker is corrected, every required workflow must be rerun against
the new exact head. No live dependency environment or C1 asset has been accepted.

## Meaning of this status

Three different statements must remain separate:

1. **Implemented** — repository code, schemas, tests, workflow controls, and
   operator documentation exist as a review candidate.
2. **Verified and closed** — every required repository check passes on one exact
   pull-request head. Exact run IDs, test counts, and artifact digests are
   recorded in the controlling pull request because they change with each repair.
3. **Accepted live evidence** — a manually dispatched `main` workflow creates a
   digest-bound artifact, a clean hosted runner validates it strictly as
   untrusted data, the artifact and decision are independently rehashed, and the
   project owner accepts the same identities.

PR `#72` contains the repository implementation candidate for the clean Windows
conversion-dependency preflight. That does not mean the dependency environment
has already passed on the Lenovo. Task 12 closure requires the blocked exact-head
checks to pass first. Live acceptance remains pending until the later post-merge
manual workflow, hosted validation, independent rehash, retained workspace check,
and project-owner acceptance all succeed.

No model, conversion, codec, storage, performance, or quality result is claimed
by this document.

## C1 task ledger

| Task | Repository boundary | Live-evidence boundary |
|---|---|---|
| 1. Closed JSON contracts and schema registry | Implemented | Templates only |
| 2. Deterministic hashing and controlled paths | Implemented | Ready for later collectors |
| 3. Accepted Runtime/GenAI prerequisite revalidation | Implemented | Must rerun before any live asset access |
| 4. Disk and workspace preflight | Implemented | Must rerun on the controlled laptop |
| 5. Immutable model resolution/download adapter | Implemented with a fake Hub boundary | No Granite snapshot downloaded |
| 6. Conversion dependency and provenance controls | Clean dependency-preflight implementation candidate is provided by PR `#72` | Manual `main` run and owner acceptance still required |
| 7. Atomic asset-lock orchestrator | Offline fixture behaviour implemented | `live-asset-lock` remains blocked pending a separate dependency-binding change |
| 8. Diagnostic-candidate classification | Implemented with a digest-bound `PathEquivalent` rule | No candidate has live path-equivalence evidence |
| 9. Untrusted asset-bundle validation | Implemented with adversarial fixtures | No live asset bundle exists |
| 10. Phase 3 repository gate and workflow boundaries | Hosted PR gate plus manual-main offline fixture workflow implemented | Only `offline-fixture` is currently permitted |
| 11. Operator runbook and non-claim documentation | Implemented and cross-linked to the dependency runbook | Must be followed after merge |
| 12. Package verification and PR preparation | Blocked pending exact-head checks | Post-merge dependency proof, C1 rehearsal, and owner acceptance remain pending |

## Dependency-preflight repository ledger

PR `#72` implements these twelve repository boundaries as a verification candidate:

| Item | Repository result | Live result |
|---|---|---|
| 1. Closed decision, schema, and interruption precedence | Implemented and tested in focused development cycles | No live decision exists |
| 2. Hash-locked bootstrap using Python `3.12.10`, `pip-tools==7.6.0`, and `pip==26.1.2` | Implemented and tested in focused development cycles | No live bootstrap environment accepted |
| 3. Data-only Optimum and Optimum Intel source contracts | Implemented and tested in focused development cycles | Exact sources must still be acquired and hashed live |
| 4. No-model compatibility check | Implemented and tested without model, network, or conversion access | Must pass in the final live environment |
| 5. Controlled dependency command evidence | Implemented without weakening Runtime or GenAI defaults | No live command evidence accepted |
| 6. Fresh workspace and immutable source verification | Implemented with deterministic simulations | No `C:\\w5c` attempt accepted |
| 7. Separate bootstrap environment and target lock generation | Implemented with deterministic simulations | No target lock accepted from the Lenovo |
| 8. Untouched final environment and six ordered checks | Implemented with deterministic simulations | No final conversion environment accepted |
| 9. Atomic success/failure evidence and manifest-last rule | Implemented with deterministic simulations | No live evidence bundle exists |
| 10. Complete independent untrusted-data validator | Implemented with adversarial tests | No live artifact validated |
| 11. Hosted → Lenovo → hosted workflow | Implemented with read-only permissions and fixed inputs | Manual `main` dispatch not yet accepted |
| 12. Operator guidance and repository closure | Guidance implemented; closure blocked pending exact-head verification | Independent hashing and owner acceptance remain pending |

The dedicated operator instructions are in
[`phase3-dependency-preflight-runbook.md`](phase3-dependency-preflight-runbook.md).

## Current permitted operations

The C1 asset workflow exposes:

```text
offline-fixture
live-asset-lock
```

`offline-fixture` remains the only permitted C1 operation. `live-asset-lock`
deliberately fails before model access until a later reviewed change binds an
independently accepted dependency decision and its retained Windows workspace.

The safe C1 rehearsal remains:

```text
Actions
→ Workbook 05 Phase 3 assets
→ Run workflow

Use workflow from: main
operation: offline-fixture
confirm_live_asset_lock: unchecked
accepted_dependency_preflight_sha256: blank
```

Expected jobs:

```text
Verify Phase 3 repository contract
        ↓
Collect controlled C1 asset evidence
        ↓
Validate C1 artifact as untrusted data
```

The rehearsal performs no network model download, model conversion, or model
execution.

## Post-merge dependency proof

After PR `#72` passes all exact-head checks, is approved and merged, and a fresh
`main` application regression passes, the separate dependency workflow is
dispatched exactly as follows:

```text
Actions
→ Workbook 05 Phase 3 dependency preflight
→ Run workflow

Use workflow from: main
confirm_live_dependency_preflight: checked
```

Acceptance requires all of the following from one exact run and attempt:

```text
repository-contract Passed
Lenovo collection Passed
hosted untrusted-data validation Passed
exact main commit recorded
artifact name and GitHub digest recorded
artifact SHA-256 independently recalculated and matched
decision.json SHA-256 independently recalculated
retained C:\w5c workspace recorded
all model and scientific authorisation flags false
project-owner acceptance explicitly recorded
```

Even a fully accepted dependency preflight does not automatically enable
`live-asset-lock`. A later separate reviewed change must consume the exact
accepted decision digest and retained workspace identity. Until that change is
implemented, reviewed, merged, and verified, live Granite access remains blocked.

## Accepted read-only Phase 2 prerequisites

```text
Runtime installation:
C:\w5a\phase2-31391119557-4\i-ov

Runtime decision:
C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json

Runtime source:
b9a1f201c109e0bed74763934f79483cf6c4cbf4

Runtime decision SHA-256:
5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38

GenAI installation:
C:\w5a\phase2-31661571860-1\i-genai

GenAI decision:
C:\w5a\accepted-route-a-genai-31661571860-1\decision.json

GenAI source:
bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0

GenAI decision SHA-256:
0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79
```

These paths remain read-only. Their identities are supported by the Route A
Phase 2 closure record and must be revalidated before a later live C1 attempt.

## Current non-claims

No current C1 or dependency-preflight result proves or authorises:

```text
Granite download, conversion, loading, or generation
CPU, model, or KV-cache placement
stateful SDPA/KV-cache execution
scalar U8/U4 cache activation
TurboQuant U3/U4 activation
QJL or PolarQuant activation
fallback absence
packed K or V storage
full KV-cache allocation
TTFT, TPOT, prompt throughput, or decode throughput
peak inference RAM or maximum stable context
perplexity or P1–P6 quality
Granite 8B feasibility
```

Route B remains separately blocked and is not reopened by C1 or by dependency
qualification.
