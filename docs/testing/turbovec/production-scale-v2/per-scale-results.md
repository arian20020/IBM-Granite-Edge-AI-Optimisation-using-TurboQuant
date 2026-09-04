# Per-scale results and execution order

| Scale | Genuine chunks | Query embeddings | Formal repetitions | Status | Reason |
|---:|---:|---:|---:|---|---|
| 30 | 30 | 128 | 5 valid | Completed | readiness admitted |
| 1,000 | 1,000 | 128 | 0 | Blocked | eight CPU/RAM admission failures |
| 10,000 | 10,000 | 128 | 0 | Unexecuted | preceding formal block never admitted |
| 100,000 | 100,000 | 128 | 0 | Unexecuted | safety escalation could not be assessed from 10,000 scale |

## Scale 30 repetition ledger

| Repetition | Seed | Configuration order | Valid |
|---:|---:|---|---|
| 1 | 2026090430 | TQ3, TQ4, Exact, TQ2 | Yes |
| 2 | 2027090433 | TQ4, Exact, TQ2, TQ3 | Yes |
| 3 | 2028090436 | Exact, TQ2, TQ3, TQ4 | Yes |
| 4 | 2029090439 | TQ2, TQ3, TQ4, Exact | Yes |
| 5 | 2030090442 | TQ3, TQ4, Exact, TQ2 | Yes |

The complete 128-entry query order, individual measurements, cold/warm timings, lifecycle evidence and recovery samples are preserved in `benchmark.json`. Derived per-repetition quality values are in `evaluation.json`; median, mean, range, standard deviation and 95% confidence intervals are in `summary.json`.
