# Upstream llama.cpp quality scoring — 2026-07-15

Scoring follows the repository rubric and applies format caps strictly. Scores are deliberately conservative: fluent but unsupported or structurally non-compliant answers lose credit.

| Test | P1 | P2 | P3 | P4 | P1-P4 mean | Notes |
|---|---:|---:|---:|---:|---:|---|
| UL-01 | 4.0 | 4.0 | 4.0 | 4.0 | 4.0 | Gemma Q4_K_M diagnostic, not a high-precision row. All four outputs hit objective correctness, format, or fact-retention caps. |
| UL-02 | 9.5 | 9.0 | 7.0 | 10.0 | 8.9 | Strongest high-precision reference; P3 contains unsupported labels. |
| UL-03 | 9.5 | 9.0 | 7.0 | 10.0 | 8.9 | Q8_0 retains the BF16 reference quality on this prompt set. |
| UL-04 | 4.0 | 8.0 | 6.5 | 10.0 | 7.1 | P1 required-term/slot miss triggers the 4-point cap. |
| UL-05 | 4.0 | 8.0 | 6.5 | 10.0 | 7.1 | P1-P4 match UL-04; KV quantisation did not change these answers. |
| UL-06 | 4.0 | 6.5 | 6.0 | 10.0 | 6.6 | Fluent but generic instruction answer; P1 capped. |
| UL-07 | 8.5 | 7.5 | 6.0 | 4.0 | 6.5 | P4 omits required upstream llama.cpp fact and is capped. |
| UL-08 | 4.0 | 7.5 | 6.0 | 10.0 | 6.9 | P1 capped; other answers acceptable but partly generic. |
| UL-09 | 4.0 | 8.5 | 6.5 | 10.0 | 7.3 | One-layer Vulkan preserves UL-05 answer quality. |
| UL-10 | 4.0 | 8.5 | 6.5 | 10.0 | 7.3 | Full Vulkan preserves UL-05 answer quality. |
| UL-11 | 4.0 | 7.0 | 6.0 | 10.0 | 6.8 | P1 capped; instruction answer remains generic. |
| UL-12 | 4.0 | 7.0 | 6.0 | 10.0 | 6.8 | Full Vulkan preserves UL-11 answer quality. |
| UL-13 | 4.0 | 9.0 | 4.0 | 10.0 | 6.8 | Project SYCL workload passes. P1 misses exact `KV cache`/slot coverage; P3 contradicts the expected memory direction; P4 is fully compliant. |

UL-05 extended prompts:

| Prompt | Score | Result |
|---|---:|---|
| P5 long-context retrieval | 4.0 | Retrieved `IXN-TQ-7319`, but omitted the required `MARKER:` prefix; exact-format cap applied. |
| P6 multi-turn stability | 2.0 | Turn 1 acknowledged the value, but turn 2 returned `4821` instead of exact `amber:4821`; stability cap applied. |

UL-05 P1-P6 mean: **5.9/10**.

Performance metrics are reported separately from quality. No quality score is inferred from throughput, model size, or backend.
