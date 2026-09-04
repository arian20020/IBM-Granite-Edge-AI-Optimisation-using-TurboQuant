# Final TurboVec research report

## Result

**Disposition: `BLOCKED`.** TurboVec works in the controlled 30-chunk experiment, but the required production-scale comparison could not lawfully begin. One 30-chunk readiness window passed. Eight consecutive 1,000-chunk readiness windows failed the fixed CPU and/or RAM-stability criteria, including two low-overhead, three-second-sampling attempts. The protocol prohibits weakening thresholds, cherry-picking a busy run or terminating unrelated processes. Therefore 1,000, 10,000 and 100,000 chunk benchmarks are unexecuted, Gate A and Gate B cannot pass, and production integration is not authorized.

## Controlled design

The comparison used one frozen, L2-normalised `float32` Granite embedding matrix for Exact, TQ2, TQ3 and TQ4. Each valid repetition used the same 128 query embeddings, 96 final evaluation queries (84 answerable and 12 absent-answer), independent relevance labels, five warm-up batches and 30 measured batches. Five repetitions used a rotating balanced configuration order. Embedding generation was measured separately from index/query work.

The corpus generator produces deterministic, non-private material spanning prose, technical, education and non-sensitive healthcare topics, with headings, tables, near duplicates, distractors, Unicode and multiple synthetic source documents. Larger embedding artifacts contain 1,000, 10,000 and 100,000 distinct generated chunks; they are not duplicated padding.

## Completed 30-chunk result

Headline values are medians of five valid repetitions. Latency is warm p95 for a full query batch. These small-scale figures characterize fixed overhead and must not be treated as production-scale performance.

| Format | Recall@10 | Relative nDCG@10 | Source accuracy | Page accuracy | p95 ms | Slowdown vs Exact | Stored bytes | Exact/candidate ratio | Incremental WS bytes |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Exact | 0.7679 | 1.0000 | 0.3810 | 0.3690 | 0.7083 | 1.0000 | 53,854 | 1.0000 | 135,168 |
| TQ2 | 0.7083 | 0.9664 | 0.3690 | 0.3690 | 1.8172 | 2.9289 | 256,147 | 0.2102 | 819,200 |
| TQ3 | 0.7679 | 0.9786 | 0.3571 | 0.3571 | 2.8819 | 3.7714 | 458,739 | 0.1174 | 32,768 |
| TQ4 | 0.7857 | 0.9980 | 0.3571 | 0.3452 | 2.6212 | 3.3813 | 458,803 | 0.1174 | 303,104 |

All four formats completed build, query, save, unload, reload, post-reload comparison, corruption rejection and cleanup in all five repetitions. Ratios below 1 mean the serialized candidate was larger than equivalent Exact storage at this tiny scale. Working-set deltas are noisy at this scale and are reported, not over-interpreted.

Source and page accuracy mean top-1 retrieval provenance accuracy against frozen relevance labels. They are not generated-answer-quality scores. No answer generator or human answer-quality rubric was tested.

## Machine and conditions

Measurements ran on a Lenovo 83ER with an Intel Core i5-12450H (8 cores, 12 logical processors), 16,857,817,088 bytes physical RAM and Windows 11 Home 10.0.26200 build 26200. AC power and the Balanced scheme were stable; security protections remained enabled. The accepted 30-chunk readiness window covered 60.329706 seconds, averaged 7.4702% CPU, varied available RAM by 1.2844%, and had a 6,589,911,040-byte minimum.

Across the formal 30-chunk repetitions, configuration-level available RAM before execution ranged from 6,568,751,104 to 6,639,235,072 bytes and after cleanup from 6,555,836,416 to 6,633,660,416 bytes. Every between-run recovery check passed. Peak experiment-process working set across configurations ranged up to 45,895,680 bytes.

## Verification

- Campaign Python regression suite: 76 passed, 23 subtests passed.
- Repository non-TurboVec baseline: 111/111 passed.
- PDF extraction matrix: 10/10 passed; no production PDF integration.
- Upstream Python: 479 discovered, 322 executed, 321 passed, 1 failed, 157 skipped.
- Rust core release: 475/475 passed.
- Rust/Python release: 6/6 passed.
- Strict Clippy: failed because 38 upstream warnings were promoted to errors.
- Windows long-sidecar case: reproducible `FileNotFoundError` with host `LongPathsEnabled=0`.

The historical 31-check harness described the earlier campaign snapshot; the current suite grew through regression-first additions and now contains 76 passing tests. Counts refer to different revisions and are not added together.

## Conclusion

The valid evidence proves controlled small-scale operation and lifecycle correctness, not a production-scale benefit. Because no genuine 10,000-chunk formal block was admitted, the existing demonstrator evidence cannot be reconsidered under Gate A. The campaign itself returns `BLOCKED` due to persistent external host load. Application code, frontend code and `main` were not modified.
