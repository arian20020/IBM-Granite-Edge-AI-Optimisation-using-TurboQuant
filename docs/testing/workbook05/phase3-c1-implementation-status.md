# Workbook 05 Phase 3 C1 implementation status

**Campaign:** `GTQ-WB05-MF-v1`  
**Route:** `route-a-merged-openvino`  
**Package:** C1 — immutable model and conversion assets  
**Pull request:** `#69`  
**Status:** Staged repository implementation candidate; live C1 acceptance has not started

## Meaning of this status

Three different statements must remain separate:

1. **Implemented** — repository code, schemas, tests, workflow controls, and
   operator documentation exist.
2. **Verified** — every required repository check passes on one exact
   pull-request head.
3. **Accepted live evidence** — a manually dispatched `main` workflow creates a
   digest-bound artifact, a clean hosted runner validates it as untrusted data,
   and the project owner accepts the same digest.

This pull request is a staged implementation candidate. Exact-head verification
must pass after every repair before the pull request is merged. No live model or
dependency result is claimed by this document.

## Staged task ledger

| Task | Repository boundary in this PR | Live-evidence boundary |
|---|---|---|
| 1. Closed JSON contracts and schema registry | Implemented | Templates only |
| 2. Deterministic hashing and controlled paths | Implemented | Ready for later collectors |
| 3. Accepted Runtime/GenAI prerequisite revalidation | Implemented | Must rerun before any live asset access |
| 4. Disk and workspace preflight | Implemented | Must rerun on the controlled laptop |
| 5. Immutable model resolution/download adapter | Implemented with a fake Hub boundary | No Granite snapshot downloaded |
| 6. Conversion dependency and provenance controls | Offline contracts, lock validation, and blocked fixture orchestration implemented | The clean Windows package collector is deferred to a separately reviewed enablement change |
| 7. Atomic asset-lock orchestrator | Offline fixture behavior implemented | Live mode remains blocked |
| 8. Diagnostic-candidate classification | Implemented with a digest-bound `PathEquivalent` rule | No candidate has live path-equivalence evidence |
| 9. Untrusted asset-bundle validation | Implemented with adversarial fixtures | No live asset bundle exists |
| 10. Phase 3 repository gate and workflow boundaries | Hosted PR gate plus manual-main offline fixture workflow implemented | Only `offline-fixture` is currently permitted |
| 11. Operator runbook and non-claim documentation | Implemented | Must be followed after merge |
| 12. Package verification and PR preparation | Exact-head checks required before merge | Post-merge rehearsal and owner acceptance remain pending |

## Permitted post-merge operation

The current workflow exposes:

```text
offline-fixture
live-asset-lock
```

`offline-fixture` is the only permitted operation. `live-asset-lock` deliberately
fails before model access until a later reviewed change verifies an independently
accepted dependency-preflight artifact and retained Windows workspace.

The safe first post-merge sequence is:

```text
Workbook 05 Phase 3 assets
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

## Accepted read-only prerequisites

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
C:\w5a\accepted-route-a-genai-31656417607-1\decision.json

GenAI source:
bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0

GenAI decision SHA-256:
0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79
```

These paths remain read-only.

## Current non-claims

No current C1 result proves or authorises:

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

Route B remains separately blocked and is not reopened by C1.
