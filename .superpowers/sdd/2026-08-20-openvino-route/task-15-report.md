# Task 15 report: recovered TBQ4 CPU runtime

## Outcome

Recovered the narrow TurboQuant route from the exact OpenVINO 2026.3.0 release.
The pinned release commit already contains upstream PR 35853 and its CPU-SDPA
TurboQuant implementation, so the reproducible patch series is deliberately
empty. The closure accepts only IBM Granite 4.1 3B, CPU, non-paged SDPA, exact
head dimension 64, and symmetric TBQ4/TBQ4 (`u4` plus `TURBO`). TBQ3, QJL,
PolarQuant, GPU, PagedAttention, and prefill compression remain excluded.

No llama.cpp source was imported. Application registration and redistribution
remain disabled while the required external security and license decisions are
pending.

## Source and binary closure

- Exact OpenVINO release commit: `8a17657b995fd3b4a52f8484acfcf2bb61214623`.
- Included implementation merge: `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Compatible OpenVINO GenAI commit: `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`.
- Eighteen relevant upstream files are locked by path, byte length, and SHA-256.
- The verifier requires the exact clean source commit, implementation ancestry,
  strict JSON shapes, exact accepted tuple, a closed file set, and exact staged
  binary/license/test hashes.
- The MSVC x64 Release builder consumes only the already verified official
  archive closure and produces a separate, manifest-bound runtime directory.

## Native conformance

The codec test independently exercises the released lookup table and encoding:
frozen packed bytes, round trip, relative error, zero/saturation/non-finite
policy, norm metadata, exact head size and tail rejection, unaligned buffers,
and deterministic 32-thread output.

The dispatch test now executes—not merely compiles—a two-turn stateful SDPA
graph through the released CPU plugin. It verifies the fused attention execution
node, finite output, TurboQuant versus scalar-u4 error bounds, the forced scalar
negative path, and rejection of unknown algorithms and malformed precision.

## Fresh verification

- Full clean-source builder: `turboquant_runtime_built`.
- Source plus staged closure verifier: `turboquant_patch_closure_valid`.
- Accepted-tuple tamper: `turboquant_patch_closure_invalid` with exit code 1.
- Release CTest: 2/2 passed twice.
- MSVC AddressSanitizer Release: codec and stateful dispatch tests passed.
- PowerShell parser errors: zero.

Review and execution were performed inline per user direction; no subagent was
used. External security/license approval and real Granite activation evidence
remain explicit Task 16/17 gates and are not claimed here.
