# Workbook 05 Phase 3 C1 implementation status

**Campaign:** `GTQ-WB05-MF-v1`  
**Route:** `route-a-merged-openvino`  
**Package:** C1 — immutable model and conversion assets  
**C1 controls PR:** `#69`  
**Dependency-preflight implementation PR:** `#72`  
**Live C1 binding implementation:** `#82`  
**Status:** the clean dependency environment is independently accepted; PR `#82` binds that exact evidence to the live C1 model-acquisition and conversion boundary; live C1 evidence is not yet accepted

## Current checkpoint states

- Dependency-preflight repository implementation: **Complete**
- Live dependency-preflight acceptance: **Accepted**
- Accepted dependency identity: **Workflow run `32211117536`, attempt `1`**
- Live C1 binding implementation: **PR `#82`**
- Live C1 asset evidence: **Pending post-merge dispatch and acceptance**
- Granite model loading, generation, codec, performance, and quality evidence: **Not authorised by C1**

This status distinguishes repository implementation, accepted dependency evidence,
and live C1 asset evidence. A green repository workflow alone is not a model or
scientific result. C1 is complete only after PR `#82` is merged, a fresh `main`
`live-asset-lock` attempt passes all three workflow boundaries, its text-only
artifact is independently rehashed and validated, the retained source and converted
asset identities are checked, and the project owner accepts that exact attempt.

## Accepted dependency-preflight evidence

The accepted package-only environment is bound to:

```text
Workflow: Workbook 05 Phase 3 dependency preflight
Workflow run: 32211117536
Run attempt: 1
Project head: c417efd936a7fa2e871b689065b2f3b88636c1a1
Artifact ID: 9350956534
Artifact name: workbook-05-phase3-dependency-preflight-32211117536-1
GitHub artifact SHA-256:
b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21
Independent artifact SHA-256:
b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21
decision.json SHA-256:
429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49
Retained workspace:
C:\w5c\dependency-preflight-32211117536-1
```

The committed owner-acceptance record is:

```text
experiments/granite_turboquant_intel/manifests/campaigns/
GTQ-WB05-MF-v1/phase3/accepted-dependency-preflight.json
```

The accepted artifact passed its hosted untrusted-data validator and an independent
ZIP, path, manifest, decision, source, package, and scientific-flag inspection. The
accepted environment contains Python `3.12.10`, Optimum `2.3.0`, Optimum Intel
`2.2.0.dev0+a3b6012`, Transformers `5.5.0`, Hugging Face Hub `1.21.0`, NNCF
`3.2.0`, OpenVINO `2026.2.1`, OpenVINO Tokenizers `2026.2.1.0`, and the exact
reviewed VCS commits. It authorises only the following C1 binding verification;
model download and every scientific claim remain false in the dependency record.

The detailed operating procedure remains in
[`phase3-dependency-preflight-runbook.md`](phase3-dependency-preflight-runbook.md).

## What PR #82 adds

PR `#82` is the narrow dependency-to-C1 binding and live asset-lock candidate. It:

1. commits the exact accepted dependency identity;
2. verifies the supplied decision digest before retained-workspace access;
3. revalidates the committed acceptance, retained bundle, manifest, decision,
   observation, Python executable, and Optimum CLI;
4. keeps the offline fixture deterministic, network-free, and model-free;
5. admits `assets` to the controlled native-process adapter;
6. revalidates the accepted Route A Runtime and GenAI installations;
7. enforces the 50 GiB disk boundary and normal local-directory policy;
8. resolves the official Granite 4.1 3B request to one immutable Hub commit;
9. downloads exactly the advertised snapshot outside Git;
10. inventories and hashes source and tokenizer files;
11. invokes the reviewed INT4 asymmetric group-128 data-free conversion;
12. inventories and hashes every converted output;
13. publishes only text, JSON, CSV, and log evidence;
14. keeps all model-execution and scientific-authorisation fields false; and
15. sends the same-attempt artifact to an independent hosted validator.

The complete live operating procedure is in
[`phase3-asset-lock-runbook.md`](phase3-asset-lock-runbook.md).

## C1 task ledger

| Task | Repository state | Live-evidence state |
|---|---|---|
| 1. Closed JSON contracts and schema registry | Implemented | Used by fixture and live validators |
| 2. Deterministic hashing and controlled paths | Implemented | Dependency evidence accepted; live asset evidence pending |
| 3. Accepted Runtime/GenAI prerequisite revalidation | Implemented | Must rerun inside the live C1 attempt |
| 4. Disk and workspace preflight | Implemented | Must pass on the Lenovo before model access |
| 5. Immutable model resolution/download adapter | Implemented and tested with an injected fake Hub | Live Granite snapshot pending |
| 6. Conversion dependency and provenance controls | Implemented | Exact dependency environment accepted from run `32211117536` |
| 7. Atomic asset-lock orchestrator | Offline and live paths implemented in PR `#82` | Live run pending post-merge |
| 8. Diagnostic-candidate classification | Implemented | No live model inference classification belongs to C1 |
| 9. Untrusted asset-bundle validation | Implemented with adversarial fixtures | Live same-attempt artifact pending |
| 10. Phase 3 repository gate and workflow boundaries | Implemented | Post-merge `main` asset workflow pending |
| 11. Operator runbooks and non-claims | Updated for accepted dependency and bounded live C1 | Must be followed exactly |
| 12. Package verification and PR preparation | PR `#82` verification in progress | Owner merge approval and live C1 acceptance pending |

## Accepted read-only Phase 2 prerequisites

These inputs remain immutable and read-only:

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

The live C1 attempt must revalidate these decisions, source commits, required
installed files, and false scientific flags before it creates or touches the model
root.

## Post-merge live C1 boundary

After PR `#82` passes every exact-head check, is reviewed and merged, and a fresh
`main` application regression passes, the asset workflow is dispatched with:

```text
operation: live-asset-lock
confirm_live_asset_lock: checked
accepted_dependency_preflight_sha256:
429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49
```

The expected trust sequence is:

```text
hosted repository contract
        ↓
Lenovo dependency revalidation, model acquisition, and conversion
        ↓
hosted same-attempt artifact validation
        ↓
independent artifact, manifest, record, and retained-path inspection
        ↓
project-owner acceptance
```

The existing dependency workspace and every earlier failed C1/dependency attempt
must remain unchanged. A new workflow run uses a new C1 evidence workspace and new
immutable source/converted directory identities. No earlier attempt is repaired or
relabelled as successful.

## Current non-claims

Neither the accepted dependency preflight nor PR `#82` currently proves or
authorises:

```text
Granite model loading or text generation
CPU, model, or KV-cache placement
stateful SDPA/KV-cache execution
scalar U8/U4 cache activation
TurboQuant U3/U4 activation
QJL or PolarQuant activation
requested-versus-selected codec equality
fallback absence
packed K or V storage
full KV-cache allocation
TTFT, TPOT, prompt throughput, or decode throughput
peak inference RAM or maximum stable context
perplexity or P1–P6 quality
Granite 8B feasibility
```

Those remain later C2–C5 and Stage D/E evidence boundaries. Route B remains
separately blocked and is not reopened by C1 or dependency qualification.
