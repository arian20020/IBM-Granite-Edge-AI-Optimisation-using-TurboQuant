---
title: "7. OpenVINO IR"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/7. OpenVINO IR.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

## **OpenVINO IR as an Intermediate Representation**

OpenVINO IR stands for **Intermediate Representation**. It is OpenVINO’s native, hardware-independent model format and is used as the middle stage between the original trained model and the final device-specific compiled model.

AI models are commonly developed and trained using frameworks such as PyTorch or TensorFlow. Models may also be downloaded through platforms such as Hugging Face, although Hugging Face itself is mainly a model hub and software ecosystem rather than a model format.

Before the model can run efficiently through OpenVINO, it can be converted into OpenVINO IR. This gives OpenVINO a standard representation of the model that it can understand, analyse and optimise.

Original trained model  
PyTorch, TensorFlow or ONNX  
│  
▼  
OpenVINO IR  
Hardware-independent OpenVINO representation  
│  
▼  
OpenVINO Runtime compiles the model  
for the selected device  
│  
▼  
CPU / GPU / NPU  
executes inference

OpenVINO IR is called an intermediate representation because it sits between the model’s original form and the compiled representation prepared for a particular hardware device.

The IR model itself is not limited to one processor. OpenVINO Runtime can use the same compatible IR model as the starting point for compilation on different devices, such as a CPU, GPU or NPU.

Overall, OpenVINO IR provides a standard model representation that allows OpenVINO to interpret, optimise and prepare a trained model for efficient inference on supported local hardware.

## **OpenVINO IR Model Files**

An OpenVINO IR model normally consists of two matching files:

model.xml  
model.bin

These files work together to describe the model and store the numerical data required for inference.

### **XML File**

The .xml file describes the structure of the model. It acts as a blueprint that explains how the model is organised and how information moves through it.

It contains information such as:

- model inputs and outputs;

- mathematical operations;

- tensor shapes;

- tensor data types;

- connections between operations;

- operation settings and attributes;

- references to constant data stored in the BIN file.

For example, the XML file may describe a matrix multiplication operation and identify where its weight values are stored inside the corresponding BIN file.

### **BIN File**

The .bin file stores the model’s large numerical constants in binary form.

This can include:

- model weights;

- biases;

- compressed or quantised weight values;

- quantisation scales and zero-point values where applicable;

- other constant tensors used by the model.

The XML file normally does not store all of these numerical values directly. Instead, it uses offsets and sizes to point to the correct locations inside the BIN file.

model.xml  
Describes the model’s structure  
and explains how the data should be used  
  
│  
▼  
  
model.bin  
Stores the model’s weights  
and other numerical constants

A simple comparison is:

XML file = model blueprint and instructions  
  
BIN file = numerical data and model weights

Both files are required and must correspond to each other. If the BIN file is removed, renamed or separated from its matching XML file, OpenVINO will normally be unable to load the model correctly.

Overall, the XML file explains what operations the model performs and how they are connected, while the BIN file stores the numerical values used by those operations.
