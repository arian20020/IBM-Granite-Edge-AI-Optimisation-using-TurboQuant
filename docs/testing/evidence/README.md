# Runtime verification evidence

This folder records evidence-backed runtime verification. Source presence, planned commands or a passing test on a different commit are not accepted as proof of the current gate.

## Model Inspection

- [LLamaSharp Tier 1 hosted verification — 2026-08-04](./2026-08-04-llamasharp-tier1-verification.md)
- [LLamaSharp Tier 2 trusted real-model verification — 2026-08-05](./2026-08-05-llamasharp-tier2-local-verification.md)
- [Protected worker Gate 1 contracts/protocol — 2026-08-05](./2026-08-05-model-inspection-worker-gate1-verification.md)
- [Protected worker Gate 2 process boundary — 2026-08-05/06](./2026-08-05-model-inspection-worker-gate2-verification.md)
- [Model Inspection cleanup Phase 0 baseline — 2026-08-07](./2026-08-06-model-inspection-cleanup-baseline.md)
- [Model Inspection cleanup Phase 1 evidence — 2026-08-08](./2026-08-07-model-inspection-cleanup-phase-1.md)

## Hardware Inspection

- [LLM Fit Gate 1 verification - 2026-08-15](./2026-08-15-hardware-inspection-gate1-llmfit-verification.md) - **Gate 1 Blocked and unsatisfied.** Gate 2 must not start; this record does not verify `F-M07`, `HE-01`, or `HE-02`.
- [llama.cpp capabilities Gate 5 development acceptance - 2026-08-23](./2026-08-23-hardware-inspection-gate-5-llamacpp-capabilities.md) - **Gate 5 development acceptance complete.** Public-trust signing, Smart App Control acceptance, production activation, and canonical snapshot resolution are not claimed.
- [Evidence resolution Gate 6 verification - 2026-08-24](./2026-08-24-hardware-inspection-gate-6-resolution.md) - **Gate 6 implementation verified with an explicit inline-review exception.** Deterministic canonical resolution is present; production activation and Gates 7-9 remain incomplete.

## Interpretation rule

Each record identifies the exact source head, workflow boundary, test/build/runtime results, artifact identity, integrity/privacy checks and explicit non-claims. Later results supersede earlier wording only for the exact scope and commit they verify.
