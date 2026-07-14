---
title: "2. What OpenVINO Does"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/2. What OpenVINO Actually Does/2. What OpenVINO Does.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

## **OpenVINO as an Inference and Deployment Toolkit**

OpenVINO is primarily an AI inference and deployment toolkit. Its main purpose is to take models that have already been trained and prepare them for efficient local execution on supported hardware, particularly Intel CPUs, GPUs and NPUs.

OpenVINO does not normally train AI models from scratch. Model training is usually completed using frameworks such as PyTorch or TensorFlow before the model is introduced into the OpenVINO workflow.

The main OpenVINO process includes:

- importing or converting the trained model;

- representing the model as an OpenVINO computation graph;

- optimising the graph;

- optionally compressing the model;

- compiling the model for a selected device;

- running inference;

- returning the result to the application.

A simplified workflow is shown below:

Already-trained model  
│  
▼  
Import or convert the model  
│  
▼  
Optimise and optionally compress it  
│  
▼  
Compile it for CPU, GPU or NPU  
│  
▼  
Run inference locally  
│  
▼  
Return the result to the application

OpenVINO can work with models originating from frameworks and formats such as PyTorch, TensorFlow and ONNX. However, the exact conversion process and hardware compatibility depend on the model architecture, operations and target device.

Overall, OpenVINO focuses on making trained AI models more practical, efficient and suitable for local deployment rather than developing or training the models themselves.
