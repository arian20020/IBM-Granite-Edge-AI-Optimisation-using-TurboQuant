---
title: "KV Cache and TurboQuant Research"
status: "curated"
version: "1.0"
last_updated: "2026-07-14"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
---

# KV cache and TurboQuant research

This section moves from the basic KV cache to practical low-bit compression.

1. [`01-kv-cache-basics.md`](01-kv-cache-basics.md)
2. [`02-kv-cache-quantisation.md`](02-kv-cache-quantisation.md)
3. [`03-polarquant.md`](03-polarquant.md)
4. [`04-turboquant-and-qjl.md`](04-turboquant-and-qjl.md)
5. [`05-evaluation-plan.md`](05-evaluation-plan.md)
6. [`06-implementation-landscape.md`](06-implementation-landscape.md)

## Key distinction

Formal TurboQuant research and GitHub implementations are not automatically the same. Many repositories use a fast rotation and low-bit codebook but omit, replace or change the formal QJL correction stage. Each repository review therefore records both the claimed method and the actual method found in the supplied analysis.

## Further reading

- *AI Engineering*, Chapter 9 (inference optimisation and performance metrics).
- *Systems Engineering: Principles and Practice*, Chapter 17 (test and evaluation).
