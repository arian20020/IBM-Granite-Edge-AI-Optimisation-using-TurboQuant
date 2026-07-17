# OpenVINO HETERO mode

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

HETERO mode allows different operations from one model graph to be assigned to different devices. It should not be described as automatic magic. The model is analysed, supported operations are assigned, unsupported operations may fall back, and the resulting split needs to be measured.

## Step-by-step HETERO reasoning

1. Identify the preferred device.
2. Identify a fallback device.
3. Compile using an explicit HETERO priority.
4. Inspect which operations were assigned to each device.
5. Check whether data-transfer overhead removes the expected benefit.
6. Compare the result with single-device CPU and GPU/NPU baselines.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: 5. HETERO DEVICE mode

> **Original document:** `OpenVINO/5. HETERO DEVICE mode.docx`

#### **HETERO Device Mode**

OpenVINO’s HETERO device mode allows one AI model to be divided across multiple supported hardware devices. Different parts of the model are assigned to the devices that can execute them most effectively.

For example, the GPU may process large parallel operations such as matrix multiplications, while the CPU handles operations that are not supported by the GPU plugin.

OpenVINO model graph  
│  
▼  
Check which operations each device supports  
│  
▼  
Split the graph into subgraphs  
│  
├── GPU subgraph  
│  
└── CPU subgraph  
│  
▼  
Execute the model and combine the results

A CPU and GPU can be selected using the following device string:

ov::CompiledModel compiled_model =  
core.compile_model(model, "HETERO:GPU,CPU");

OpenVINO first checks which model operations are supported by each selected device. It then divides the computation graph into smaller subgraphs and compiles each subgraph for its assigned device.

HETERO mode does not necessarily mean that every device performs calculations at the same time. Instead, different sections of the model are executed by different devices according to the structure of the graph.

The main benefit of HETERO mode is improved compatibility. If one device cannot execute every operation in the model, unsupported operations can be assigned to another device.

However, HETERO mode is not automatically faster than using a single device. Moving data between devices and coordinating multiple subgraphs can introduce additional processing overhead. The configuration must therefore be tested and benchmarked before it is selected as the preferred option.

Overall, HETERO mode allows OpenVINO to divide a model across devices such as the CPU and GPU, improving hardware compatibility while potentially making use of the strengths of each device.

## What this means for the project

HETERO should be added only after single-device baselines work. Otherwise, a mixed-device result is difficult to debug or attribute.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-OV-HETERO-2026`
- `SRC-OV-DEVICES-2026`
