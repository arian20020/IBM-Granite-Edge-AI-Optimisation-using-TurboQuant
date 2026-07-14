# OpenVINO Runtime, IR and compilation

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This document explains three connected ideas: OpenVINO Runtime, OpenVINO IR and model compilation. The IR describes the graph and weights. The runtime loads that representation. Compilation prepares the graph for a selected device and creates the executable form used during inference.

## Step-by-step runtime flow

1. Obtain or convert the model.
2. Keep the XML graph and BIN weights together when using the traditional IR pair.
3. Create an OpenVINO `Core` object.
4. Read the model.
5. Choose a device or automatic device strategy.
6. Compile the model.
7. Create an inference request or a GenAI pipeline.
8. Run inputs and collect outputs.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OV-IR-2026](../00-sources/official-documentation.md#src-ov-ir-2026), [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## OpenVINO IR

OpenVINO IR represents model calculations as a directed graph. [SRC-OV-IR-2026]

- The XML describes operations, ports, edges, tensor shapes and data types.
- The matching BIN stores large constant data such as model weights.

The graph can branch and merge. It is directed, but it is not necessarily one straight sequence.

## Runtime compilation

```text
IR graph
-> OpenVINO Runtime
-> selected device plugin
-> device-specific compiled model
-> CPU, GPU or NPU execution
```

The plugin may fuse operations, select kernels, choose layouts and precision, allocate memory and create an execution schedule. The compiled graph can therefore differ from the portable IR graph.

## Important boundary

The IR describes the model's mathematical work. OpenVINO does not infer an unsupported custom cache codec simply because a model uses attention. A TurboQuant/OpenVINO route needs explicit graph, operator or runtime support.

## Sources used

- [SRC-OV-IR-2026](../00-sources/official-documentation.md#src-ov-ir-2026) — OpenVINO IR format.
- [SRC-OV-CORE-2026](../00-sources/official-documentation.md#src-ov-core-2026) — openvino.Core API.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: 3. OpenVINO Runtime

> **Original document:** `OpenVINO/3. OpenVINO Runtime.docx`

#### **OpenVINO Runtime**

OpenVINO Runtime is the main execution engine within the OpenVINO toolkit. Its role is to load an OpenVINO model, prepare it for the selected hardware and run inference.

The main responsibilities of OpenVINO Runtime include:

- reading the OpenVINO model;

- analysing and optimising the model’s computation graph;

- selecting or accepting a target device;

- compiling the model for a CPU, GPU or NPU;

- creating inference requests;

- executing the model;

- returning the generated output to the application.

A simplified workflow is shown below:

OpenVINO IR model  
│  
▼  
OpenVINO Runtime  
- reads the model  
- optimises the computation graph  
- compiles it for the selected device  
- executes inference  
│  
▼  
CPU / GPU / NPU

OpenVINO Runtime distinguishes between an ov::Model and an ov::CompiledModel.

ov::Model  
=  
A hardware-independent representation  
of the model loaded into OpenVINO

ov::CompiledModel  
=  
A version of the model that has been  
prepared for a specific target device

For example, the model may first be loaded as an ov::Model and then compiled for an Intel GPU. The resulting compiled model is used during inference, while the original OpenVINO IR files normally remain unchanged.

Overall, OpenVINO Runtime can be understood as the engine that loads, optimises, compiles and executes OpenVINO models on the selected local hardware.

A simplified C++ workflow is:

\#include \<openvino/openvino.hpp\>

int main() {  
ov::Core core;

std::shared_ptr\<ov::Model\> model =  
core.read_model("model.xml");

ov::CompiledModel compiled_model =  
core.compile_model(model, "GPU");

ov::InferRequest request =  
compiled_model.create_infer_request();

// Set input tensors.  
// Run inference.  
// Read output tensors.  
}

## Original research: 7. OpenVINO IR

> **Original document:** `OpenVINO/7. OpenVINO IR.docx`

#### **OpenVINO IR as an Intermediate Representation**

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

#### **OpenVINO IR Model Files**

An OpenVINO IR model normally consists of two matching files:

model.xml  
model.bin

These files work together to describe the model and store the numerical data required for inference.

##### **XML File**

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

##### **BIN File**

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

## What this means for the project

The model-import page must validate both files of an XML/BIN pair and keep their paths together. The backend should report conversion and compilation failures in simple language.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-OV-IR-2026`
- `SRC-OV-CORE-2026`
