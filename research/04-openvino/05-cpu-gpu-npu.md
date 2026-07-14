# OpenVINO CPU, GPU and NPU

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This guide compares CPU, GPU and NPU roles without assuming one device is always best. The correct device depends on model support, operator coverage, memory, precision, driver support and the actual workload.

## Fair device comparison

1. Use the same model and prompt.
2. Keep generation settings identical.
3. Warm up each device in the same way.
4. Record requested and actual device.
5. Measure load time, first-token latency, throughput, memory and power where possible.
6. Note unsupported operations and fallback.
7. Select the device based on evidence rather than device marketing.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: 6. CPU, GPU AND NPU

> **Original document:** `OpenVINO/6. CPU, GPU AND NPU.docx`

#### **4.5 Intel CPU, GPU and NPU**

OpenVINO can run AI models on different types of Intel hardware. The main devices are the CPU, GPU and NPU. Each processor has a different purpose and is suited to different types of calculations.

##### **Intel CPU**

The CPU is a flexible, general-purpose processor that can perform a wide range of computing tasks. It normally provides the broadest model and operation compatibility, making it a reliable fallback when a GPU or NPU cannot support part of a model.

However, a CPU may be less efficient than specialised hardware when performing large numbers of parallel AI calculations.

##### **Intel GPU**

The GPU is designed to perform many similar calculations in parallel. This makes it well suited to AI workloads involving large matrix multiplications and other highly parallel mathematical operations.

A GPU can often process these workloads faster than a CPU. However, it is not automatically faster for every operation, and the model must still be supported by the GPU device plugin.

##### **Intel NPU**

The NPU, or Neural Processing Unit, is a specialised AI accelerator designed to perform supported neural-network calculations efficiently.

Its main advantage is lower power consumption, making it suitable for continuous local AI features such as speech recognition, image enhancement and other supported AI workloads. However, an NPU normally supports a narrower range of operations than a CPU or GPU.

It is important to understand that the NPU does not directly manage “neural-network connections.” Instead, it accelerates the mathematical operations used by neural networks.

| **Device** | **Main purpose**                      | **Main advantage**                           | **Main limitation**                           |
|------------|---------------------------------------|----------------------------------------------|-----------------------------------------------|
| CPU        | General-purpose processing            | Broad compatibility and flexibility          | May be slower for large parallel AI workloads |
| GPU        | Parallel mathematical processing      | Strong performance for large AI calculations | Not every model operation is supported        |
| NPU        | Specialised neural-network processing | Efficient AI execution with lower power use  | More limited operation and model support      |

A simplified comparison is shown below:

CPU  
Flexible and widely compatible  
Best as a reliable general-purpose option

GPU  
Performs many calculations in parallel  
Best for large and highly parallel AI operations

NPU  
Specialised for supported AI calculations  
Best for efficient, low-power local AI workloads

Overall, the CPU provides compatibility, the GPU provides strong parallel performance, and the NPU provides efficient AI acceleration. The most suitable device depends on the model, supported operations, memory requirements and desired balance between performance and power consumption.

## What this means for the project

Automatic mode can eventually choose a device, but that choice should be based on measured compatibility and memory rather than a fixed assumption that the NPU is always fastest.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-OV-DEVICES-2026`
- `SRC-OV-CORE-2026`
