# Workbook 05 Phase 3 C1 implementation status

**Campaign:** `GTQ-WB05-MF-v1`  
**Route:** `route-a-merged-openvino`  
**Package:** C1 — immutable model and conversion assets  
**Pull request:** `#69`  
**Status:** Repository implementation verified; live scientific acceptance not started

## Meaning of the status

This document separates three different statements that must not be collapsed:

1. **Implemented** — repository code, tests, schemas, workflow contracts, and
   operator documentation exist.
2. **Verified** — those repository-controlled checks pass on the exact current
   pull-request head.
3. **Accepted live evidence** — a manually dispatched `main` workflow produced a
   digest-bound artifact, a separate hosted runner validated it as untrusted
   data, and the project owner independently accepted the same digest.

C1 has reached the first two statements for its staged repository package. It
has **not** reached the third statement for the dependency environment or model
assets.

## Task ledger

| Task | Repository status | Live-evidence status |
|---|---|---|
| 1. Closed JSON contracts and schema registry | Implemented and verified | Templates only; no live C1 record yet |
| 2. Deterministic hashing and controlled paths | Implemented and verified | Used by later live collectors |
| 3. Accepted Runtime/GenAI prerequisite revalidation | Implemented and verified | Must rerun at the start of live asset collection |
| 4. Disk and workspace preflight | Implemented and verified | Must rerun on the controlled laptop |
| 5. Immutable model resolution/download adapter | Implemented and verified with a fake Hub boundary | No Granite snapshot downloaded |
| 6. Conversion dependency and provenance controls | Implemented and verified, including clean Windows live collector and hosted validator | `dependency-preflight` workflow has not run from `main` |
| 7. Atomic asset-lock orchestrator | Offline fixture behavior implemented and verified | Live mode remains blocked pending accepted dependency evidence |
| 8. Diagnostic-candidate classification | Implemented and verified | No candidate has live path-equivalence evidence |
| 9. Untrusted asset-bundle validation | Implemented and adversarially verified | No live asset bundle exists |
| 10. Phase 3 repository gate and workflow boundaries | Implemented and exact-head verified | Manual `main` operations have not run |
| 11. Operator runbook and recovery/non-claim documentation | Implemented and verified | Must be followed after merge |
| 12. Full package verification and PR preparation | Repository gate and exact-head CI passed | Post-merge workflow and owner acceptance remain pending |

## Exact staged workflow order

After the implementation PR is accepted and merged:

```text
1. offline-fixture
   repository contract
   -> controlled Intel fixture collection
   -> independent hosted artifact validation

2. dependency-preflight
   repository contract
   -> clean Windows Python 3.12.10 environment under C:\w5c
   -> hash-locked ordinary packages
   -> exact local VCS installs
   -> import/CLI/no-model checks
   -> independent hosted artifact validation
   -> project-owner digest acceptance

3. follow-up enablement PR
   verify the accepted dependency artifact, decision and retained workspace
   before any model repository access

4. live-asset-lock
   immutable Granite revision
   -> exact source/tokenizer files
   -> INT4 asymmetric group-128 ratio-1.0 conversion
   -> complete text-only evidence
   -> hosted validation
   -> project-owner digest acceptance
```

The workflow cannot skip directly from repository tests to model download.

## Current accepted prerequisites

```text
Runtime:
C:\w5a\phase2-31391119557-4\i-ov
Runtime decision:
C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json
Runtime source:
b9a1f201c109e0bed74763934f79483cf6c4cbf4
Runtime decision SHA-256:
5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38

GenAI:
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
