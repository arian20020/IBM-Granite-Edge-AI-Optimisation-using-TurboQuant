# Runtime verification evidence

This folder records evidence-backed runtime verification results. Source presence or planned commands are not treated as proof of a passing gate.

## LLamaSharp Model Inspection

- [Tier 1 hosted verification — 2026-08-04](./2026-08-04-llamasharp-tier1-verification.md)
- [Tier 2 local trusted real-model verification — 2026-08-05](./2026-08-05-llamasharp-tier2-local-verification.md)
- [Production worker integration Gate 1 verification — 2026-08-05](./2026-08-05-model-inspection-worker-gate1-verification.md)

## Interpretation rule

Each evidence record must identify:

- the exact commit or branch state tested;
- the selected runtime and model identity;
- the commands or workflow boundary used;
- observed test, build and runtime results;
- integrity and privacy checks;
- explicit non-claims and deferred gates.

Later results supersede earlier status wording only for the exact scope they verify.
