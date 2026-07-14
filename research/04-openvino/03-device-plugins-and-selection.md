---
title: "OpenVINO Device Plugins and Selection"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/4. Device plugins.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OV-DEVICES-2026"
source_ids:
  - "SRC-OV-CORE-2026"
  - "SRC-OV-DEVICES-2026"
---

# OpenVINO device plugins and selection

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026), [SRC-OV-DEVICES-2026](../00-sources/official-documentation.md#src-ov-devices-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



A device plugin translates the hardware-independent graph into work suitable for a specific target. [SRC-OV-CORE-2026]

Typical device names include:

- `CPU`
- `GPU`
- `NPU`
- meta-devices such as `AUTO`, `MULTI` or `HETERO` when supported by the installed runtime.

## What a plugin does

- checks whether operations are supported;
- applies device-specific graph changes;
- chooses kernels and tensor layouts;
- manages memory;
- compiles the executable representation;
- schedules inference.

## Project rule

Do not choose a device only by its name. Measure the same model and workload on each available route. A GPU can be fast for one model but limited by memory. An NPU may have lower power use but narrower operation support. A CPU may be the most compatible fallback.

## Sources used

- [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026) — openvino.Core API.
- [SRC-OV-DEVICES-2026](../00-sources/official-documentation.md#src-ov-devices-2026) — Supported devices.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
