---
title: "5. HETERO DEVICE mode"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/5. HETERO DEVICE mode.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

## **HETERO Device Mode**

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
