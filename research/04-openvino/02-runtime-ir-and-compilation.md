---
title: "OpenVINO Runtime, IR and Compilation"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/3. OpenVINO Runtime.docx"
  - "OpenVINO/7. OpenVINO IR.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OV-CORE-2026"
source_ids:
  - "SRC-OV-IR-2026"
  - "SRC-OV-CORE-2026"
---

# OpenVINO Runtime, IR and compilation

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OV-IR-2026](../00-sources/official-documentation.md#src-ov-ir-2026), [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## OpenVINO IR

OpenVINO IR represents model calculations as a directed graph. [SRC-OV-IR-2026]

- The XML describes operations, ports, edges, tensor shapes and data types.
- The matching BIN stores large constant data such as model weights.

The graph can branch and merge. It is directed, but it is not necessarily one straight sequence.

## Runtime compilation

```text
IR graph
-> OpenVINO Runtime
-> selected device plugin
-> device-specific compiled model
-> CPU, GPU or NPU execution
```

The plugin may fuse operations, select kernels, choose layouts and precision, allocate memory and create an execution schedule. The compiled graph can therefore differ from the portable IR graph.

## Important boundary

The IR describes the model's mathematical work. OpenVINO does not infer an unsupported custom cache codec simply because a model uses attention. A TurboQuant/OpenVINO route needs explicit graph, operator or runtime support.

## Sources used

- [SRC-OV-IR-2026](../00-sources/official-documentation.md#src-ov-ir-2026) — OpenVINO IR format.
- [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026) — openvino.Core API.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
