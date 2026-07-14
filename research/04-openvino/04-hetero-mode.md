---
title: "OpenVINO HETERO Mode"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/5. HETERO DEVICE mode.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OV-DEVICES-2026"
source_ids:
  - "SRC-OV-HETERO-2026"
  - "SRC-OV-DEVICES-2026"
---

# OpenVINO HETERO mode

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OV-HETERO-2026](../00-sources/official-documentation.md#src-ov-hetero-2026), [SRC-OV-DEVICES-2026](../00-sources/official-documentation.md#src-ov-devices-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



HETERO mode allows different parts of a graph to run on different devices when one device cannot execute every operation. [SRC-OV-HETERO-2026]

```text
Model graph
-> supported sections on preferred device
-> unsupported sections on fallback device
-> combined result
```

This can improve compatibility, but device transfers can add overhead. It should not be assumed to be faster than using one device.

## When to consider it

- an NPU or GPU supports most, but not all, operations;
- the fallback CPU can execute the remaining operations;
- measured performance and stability are acceptable.

## Evidence required

Record the device priority, actual operation placement, transfer overhead, latency and memory. Compare against CPU-only and single-accelerator baselines.

## Sources used

- [SRC-OV-HETERO-2026](../00-sources/official-documentation.md#src-ov-hetero-2026) — Heterogeneous execution.
- [SRC-OV-DEVICES-2026](../00-sources/official-documentation.md#src-ov-devices-2026) — Supported devices.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
