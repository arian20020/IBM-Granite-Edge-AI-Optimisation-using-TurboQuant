# TurboVec supporting feasibility rerun — 11 September 2026

## Outcome

**Recommendation: do not integrate TurboVec into this application from this evidence.** The quiet-host rerun completed at 1,000 and 10,000 distinct documents with Exact, TQ2, TQ3 and TQ4. Every block contains five valid repetitions and passes the independent campaign validator. No TurboVec configuration passed the locked 10,000-document Gate A quality requirements, so the disposition is `DEMONSTRATOR_ONLY` and no configuration is selected.

This is a supporting rerun, not a replacement for the historical formal v2 decision. The original formal campaign remains `BLOCKED` because its admitted environment could not reach the production-scale blocks. This rerun used Python 3.13.15 instead of the pinned Python 3.12.10 and a freshly exported token-output OpenVINO model. The model weights matched the earlier export by SHA-256, but the complete export was not byte-identical.

## Decision-relevant 10,000-document medians

| Format | Recall@10 | Recall@10 vs Exact | Relative nDCG@10 | p95 latency (ms) | Slowdown vs Exact | Storage ratio | Gate A |
|---|---:|---:|---:|---:|---:|---:|---|
| Exact | 0.2024 | 1.0000 | 1.0000 | 110.0517 | 1.0000 | 1.0000 | Reference |
| TQ2 | 0.1012 | 0.2708 | 0.4125 | 3.0801 | 0.0298 | 4.7686 | Fail |
| TQ3 | 0.1607 | 0.4729 | 0.7561 | 4.0255 | 0.0407 | 3.6352 | Fail |
| TQ4 | 0.1310 | 0.6396 | 0.7382 | 4.9119 | 0.0498 | 3.6351 | Fail |

Gate A requires Recall@10 at least 0.90, relative nDCG@10 at least 0.95, p95 slowdown no more than 1.10, storage ratio at least 2.00, and successful lifecycle checks. TQ2, TQ3 and TQ4 passed speed, storage and lifecycle criteria but failed both retrieval-quality criteria.

## Interpretation

TurboVec produced a large retrieval-speed improvement and a 3.64–4.77x serialized-storage reduction at 10,000 documents. Those benefits do not compensate for the measured loss of relevant results. This matters especially for the project's education and healthcare use cases, where omitted or incorrectly ranked supporting material can undermine provenance and trust.

The Exact baseline also had low absolute Recall@10 (0.2024) and source accuracy (0.0833). That warns that the embedding/export/corpus combination itself needs improvement. The defensible TurboVec comparison is therefore the candidate-to-Exact degradation on the same frozen vectors, not a claim that TurboVec alone caused all low absolute quality. Even on that matched comparison, candidate Recall@10 against Exact was only 0.2708–0.6396 and relative nDCG@10 was 0.4125–0.7561, below the locked requirements.

The conditional 100,000-document scale was not run. It is an escalation scale, and the 10,000-document admission gate already rejected every candidate. Running it would increase cost without making any candidate eligible.

## Evidence map

- `results.csv`: compact medians for direct comparison.
- `decision.json`: machine-readable thresholds, outcome, limitations and claim boundary.
- Raw 1,000 block: `experiments/raw-results/turbovec/production-scale-v2/EXP-TV-COMP-001-20260911-SUPPORT-1000-001`.
- Raw 10,000 block: `experiments/raw-results/turbovec/production-scale-v2/EXP-TV-COMP-001-20260911-SUPPORT-10000-001`.
- Each raw block contains `benchmark.json`, `evaluation.json`, `summary.json` and a hash-binding `terminal.json`.
- `readiness-1000.json` and `readiness-10000.json`: admitted quiet-host decisions used by the runs.

No generated answer was scored, no mixed-PDF retrieval block was executed, and no production or frontend file was changed by this experiment.
