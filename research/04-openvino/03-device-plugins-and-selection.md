# OpenVINO device plugins and selection

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

OpenVINO uses device plugins so the same high-level API can target different hardware. This guide explains what a plugin is, how a device is selected, and why the requested device must be checked against the device that actually executed the work.

## Device-selection sequence

1. Query available devices.
2. Record the exact device names returned by OpenVINO.
3. Select CPU, GPU, NPU, AUTO, MULTI or HETERO deliberately.
4. Compile the model with that device string.
5. Capture runtime properties and logs.
6. Verify whether fallback occurred.
7. Report requested and actual execution devices separately.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: 4. Device plugins

> **Original document:** `OpenVINO/4. Device plugins.docx`

#### **4.2 Device Plugins**

OpenVINO uses device plugins to connect the OpenVINO Runtime API to different types of hardware. A device plugin acts as a bridge between OpenVINO Runtime and the selected processor, allowing the same model to be prepared and executed on a CPU, GPU or NPU.

The Runtime API provides a common set of commands that the application can use. The selected device plugin then translates these commands into hardware-specific operations.

Common OpenVINO device names include:

- CPU – runs inference on the processor;

- GPU – runs inference on a supported graphics processor;

- NPU – runs inference on a supported neural processing unit;

- AUTO – allows OpenVINO to select a suitable available device automatically;

- HETERO – allows different parts of a model to be assigned to different supported devices.

A target device can be selected when the model is compiled:

core.compile_model(model, "CPU");  
core.compile_model(model, "GPU");  
core.compile_model(model, "NPU");  
core.compile_model(model, "AUTO");

A simplified workflow is shown below:

Application  
│  
▼  
OpenVINO Runtime API  
│  
▼  
Selected device plugin  
│  
▼  
CPU / GPU / NPU

The same OpenVINO IR model may be usable across several devices because the Runtime API remains consistent. However, successful execution still depends on whether the model’s operations, tensor shapes and numerical precisions are supported by the selected device plugin.

Overall, device plugins allow OpenVINO Runtime to compile and execute models on different hardware without requiring the application to manage each device directly.

## What this means for the project

The application should never display only the requested device. It should also record the actual device and any fallback so users are not told that an NPU or GPU was used when execution happened on the CPU.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-OV-CORE-2026`
- `SRC-OV-DEVICES-2026`
