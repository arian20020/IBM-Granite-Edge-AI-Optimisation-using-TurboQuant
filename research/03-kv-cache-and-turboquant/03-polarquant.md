---
title: "PolarQuant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/4. PolarQuant.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-PAPER-POLARQUANT-2025"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# PolarQuant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-POLARQUANT-2025](../00-sources/primary-research-papers.md#src-paper-polarquant-2025), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



PolarQuant is a main approximation method for compressing vectors while trying to keep geometrical information useful. [SRC-PAPER-POLARQUANT-2025]

## Simplified flow

```text
Original KV vector
-> shared random rotation / preconditioning
-> separate magnitude from direction
-> convert direction into recursive polar angles
-> replace each angle with a low-bit codebook value
-> store radius/norm and compressed angles
-> reconstruct angles and radius
-> reverse the rotation
-> approximate original vector
```

## Random preconditioning

The rotation mixes the coordinates before quantisation. It does not compress the vector by itself. Its purpose is to spread concentrated or outlying information into a more regular distribution.

When the same orthogonal rotation is used consistently, norms and inner products are preserved before quantisation:

```text
(Rq)^T(Rk) = q^T k
```

Any later error comes from the low-bit approximation rather than the rotation alone.

## Why polar coordinates help

After preconditioning, the vector can be represented using radii and angles. Many later-level angles become concentrated around predictable regions. A codebook can place more representative values where angles are most likely to occur, reducing mean-squared error.

## Shared information and overhead

The practical method uses a shared rotation rather than a new matrix for every token. This avoids per-token matrix storage. It also aims to avoid the repeated block scale and zero-point overhead used by many ordinary quantisers, although norms/radii and code indices still require storage.

## What the project must not assume

- Rotation does not guarantee perfect balance.
- Low mean-squared error does not guarantee identical model output.
- The paper's mathematical projection and a repository's fast Walsh-Hadamard implementation are related ideas, but not automatically identical.
- Actual memory savings require packed storage, not only a simulated transformation.

## Sources used

- [SRC-PAPER-POLARQUANT-2025](../00-sources/primary-research-papers.md#src-paper-polarquant-2025) — PolarQuant: Quantizing KV Caches with Polar Transformation.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
