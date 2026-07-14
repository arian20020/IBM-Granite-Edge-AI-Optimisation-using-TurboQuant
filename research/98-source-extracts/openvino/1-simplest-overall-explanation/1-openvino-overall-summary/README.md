---
title: "1.OpenVINO Overall Summary"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/1. Simplest overall explanation/1.OpenVINO Overall Summary.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

## **OpenVINO, OpenVINO IR and OpenVINO GenAI**

OpenVINO is a software toolkit used to prepare, optimise and run AI models efficiently, mainly on Intel CPUs, GPUs and NPUs. It is designed for local inference, meaning that the model runs directly on the user’s computer rather than relying on a cloud service.

OpenVINO IR is OpenVINO’s native model representation. It stores the model’s computation graph and weights in a format that OpenVINO can understand, optimise and compile for the selected hardware.

OpenVINO GenAI is a higher-level library for running generative AI models such as large language models. It manages tasks including tokenisation, text generation, streaming, chat handling and KV-cache management. OpenVINO GenAI uses OpenVINO Runtime as the underlying inference engine that executes the model locally on the CPU, GPU or NPU.

Application  
│  
▼  
OpenVINO GenAI  
Manages generative AI tasks  
│  
▼  
OpenVINO Runtime  
Compiles and executes the model  
│  
▼  
CPU / GPU / NPU  
Performs the calculations
