## **4.2 Device Plugins**

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
