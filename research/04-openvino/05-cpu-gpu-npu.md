---
title: "OpenVINO CPU, GPU and NPU"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/6. CPU, GPU AND NPU.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OV-CORE-2026"
source_ids:
  - "SRC-OV-DEVICES-2026"
  - "SRC-OV-CORE-2026"
---

# OpenVINO CPU, GPU and NPU

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OV-DEVICES-2026](../00-sources/official-documentation.md#src-ov-devices-2026), [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



| Device | Main strength | Main risk |
|---|---|---|
| CPU | Broad compatibility and easy fallback | May have lower throughput for large models |
| Intel GPU | High parallel compute and useful shared/discrete memory routes | Driver, memory and operation support vary |
| Intel NPU | Low-power AI acceleration | Model and operation support can be narrower |

## Testing order

1. Confirm CPU correctness.
2. Test GPU with the same model and prompts.
3. Test NPU only when the model is supported.
4. Try AUTO or HETERO after single-device baselines are known.

## Report separately

- speed;
- memory;
- stability;
- power if available;
- unsupported operations or fallback behaviour.

A backend is only "best" for the project when it satisfies the required quality, memory and user-experience targets, not simply when one speed number is highest. [SRC-OV-DEVICES-2026]

## Sources used

- [SRC-OV-DEVICES-2026](../00-sources/official-documentation.md#src-ov-devices-2026) — Supported devices.
- [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026) — openvino.Core API.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
