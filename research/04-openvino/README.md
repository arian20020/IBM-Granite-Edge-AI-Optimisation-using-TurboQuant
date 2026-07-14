---
title: "OpenVINO Research"
status: "curated"
version: "1.0"
last_updated: "2026-07-14"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
---

# OpenVINO research

OpenVINO is treated as a separate Intel optimisation route from llama.cpp/TurboQuant.

- [`01-overview.md`](01-overview.md)
- [`02-runtime-ir-and-compilation.md`](02-runtime-ir-and-compilation.md)
- [`03-device-plugins-and-selection.md`](03-device-plugins-and-selection.md)
- [`04-hetero-mode.md`](04-hetero-mode.md)
- [`05-cpu-gpu-npu.md`](05-cpu-gpu-npu.md)
- [`06-end-to-end-llm-flow.md`](06-end-to-end-llm-flow.md)

OpenVINO can optimise and execute a model graph on supported Intel hardware. It does not automatically turn a third-party llama.cpp TurboQuant cache type into an OpenVINO feature.

## Further reading

- `windows-apps.pdf` for the Windows application layer.
- *Fundamentals of Software Architecture*, Chapters 2 and 4-6 for runtime trade-offs and measurable quality attributes.
