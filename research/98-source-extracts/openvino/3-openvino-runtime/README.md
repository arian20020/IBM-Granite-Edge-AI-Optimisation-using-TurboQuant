---
title: "3. OpenVINO Runtime"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/3. OpenVINO Runtime.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

## **OpenVINO Runtime**

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
