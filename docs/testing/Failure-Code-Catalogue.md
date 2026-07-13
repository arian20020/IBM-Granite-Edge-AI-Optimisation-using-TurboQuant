# Controlled Failure Code Catalogue

Use these codes consistently in raw run notes, `Failure-Register.csv` and the route workbooks.

| Code | Meaning |
|---|---|
| BF | Build failure |
| DEP | Missing, incompatible or unresolved dependency |
| WIN | Windows-specific issue |
| CMD | Invalid or failed command invocation |
| MODEL | Model file, conversion or metadata incompatibility |
| ARCH | Unsupported or mismatched model architecture |
| TOK | Tokenizer or chat-template problem |
| BASE | Conventional baseline failed or was not established |
| TQ-ACT | TurboQuant/TQ3_0/TBQ activation not proved |
| TQ-CRASH | Optimised route crashed or hung |
| TQ-FALLBACK | Optimised request silently or explicitly fell back |
| CPU | CPU execution-specific failure |
| GPU | GPU execution-specific failure |
| HYBRID | Unexpected or invalid mixed CPU/GPU placement |
| MEM | Memory measurement or allocation problem |
| PERF | Performance measurement or regression problem |
| QUAL | Output-quality or deterministic-validation failure |
| OOM | Out-of-memory condition or unsafe memory pressure |
| REPRO | Reproduction or repeated-run inconsistency |
| SCOPE | Test is outside agreed scope or blocked by a scope gate |

A failure remains visible after a successful retest. Link the retest run ID rather than replacing the failed record.
