# Storage and memory accounting

The serializer accounting includes equivalent vector data, index metadata, document/chunk metadata, auxiliary lookup structures and serialization overhead for every format. `storage ratio = equivalent Exact bytes / equivalent candidate bytes`; below 1 means the candidate is larger.

| Format | Vector bytes | Index metadata | Document/chunk metadata | Auxiliary | Total bytes | Ratio |
|---|---:|---:|---:|---:|---:|---:|
| Exact | See raw repetition record | See raw | See raw | See raw | 53,854 | 1.0000 |
| TQ2 | See raw repetition record | See raw | See raw | See raw | 256,147 | 0.2102 |
| TQ3 | See raw repetition record | See raw | See raw | See raw | 458,739 | 0.1174 |
| TQ4 | 451,155 | 326 | 6,954 | 368 | 458,803 | 0.1174 |

The tiny 30-vector case is dominated by fixed TurboVec structures, so it cannot answer the production-scale storage question. The exact component values for every format and repetition are in `benchmark.json` and were used directly for the totals.

Memory records distinguish total/available system RAM, process baseline, peak process working set and incremental peak. Embedding-model memory is excluded because embeddings were generated in a separate phase. All repetition recovery checks returned CPU below 10% twice consecutively and RAM within approximately 5% of the matched baseline. No paging or hard-floor event occurred in the completed block. The higher-scale measurements do not exist because admission failed before candidate construction.
