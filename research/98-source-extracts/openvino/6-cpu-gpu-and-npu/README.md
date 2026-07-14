---
title: "6. CPU, GPU AND NPU"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/6. CPU, GPU AND NPU.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

## **4.5 Intel CPU, GPU and NPU**

OpenVINO can run AI models on different types of Intel hardware. The main devices are the CPU, GPU and NPU. Each processor has a different purpose and is suited to different types of calculations.

### **Intel CPU**

The CPU is a flexible, general-purpose processor that can perform a wide range of computing tasks. It normally provides the broadest model and operation compatibility, making it a reliable fallback when a GPU or NPU cannot support part of a model.

However, a CPU may be less efficient than specialised hardware when performing large numbers of parallel AI calculations.

### **Intel GPU**

The GPU is designed to perform many similar calculations in parallel. This makes it well suited to AI workloads involving large matrix multiplications and other highly parallel mathematical operations.

A GPU can often process these workloads faster than a CPU. However, it is not automatically faster for every operation, and the model must still be supported by the GPU device plugin.

### **Intel NPU**

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
