# Functional Requirements

> TurboVec checkpoint: **BLOCKED**, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, run `EXP-TV-COMP-001-20260902T225731Z-001`. F-M25, F-M26 and F-M27 remain deferred and unimplemented.

**Baseline:** MoSCoW v1.2 / RTM v1.3  
**Owner:** Arian B  
**Presentation change:** CHG-014 / CR-014 — no scope change  

> The controlled workbook and full RTM contain rationale, mappings, acceptance criteria, verification methods and evidence paths.

| ID | Priority | Release Role | Lifecycle | Requirement |
|---|---|---|---|---|
| F-M01 | Must | Core | Active | The application must open on the target Windows 11 x64 Intel computer. |
| F-M02 | Must | Core | Active | The application must let the user select a supported local model. |
| F-M03 | Must | Core | Active | The application must validate a selected input before using it. |
| F-M04 | Must | Core | Active | The application must show the correct inspection result state. |
| F-M05 | Must | Core | Active | The application must display the model details needed for compatibility and memory checks. |
| F-M06 | Must | Core | Active | The application must show plain-English reasons for warnings or failures. |
| F-M07 | Must | Core | Active | The application must read the hardware information needed for fit analysis. |
| F-M08 | Must | Core | Active | The application must estimate peak memory for a supported configuration. |
| F-M09 | Must | Core | Active | The application must show whether a configuration is likely to fit, needs optimisation, or has no verified safe option. |
| F-M10 | Must | Core | Active | The application must generate only complete configurations valid for the current model, runtime and device. |
| F-M11 | Must | Core | Active | The application must provide Automatic, Quality, Balanced and Efficiency modes when valid alternatives exist. |
| F-M12 | Must | Core | Active | The application must show the chosen complete configuration before starting an operation. |
| F-M13 | Must | Core | Active | The application must run local chat with at least one supported Granite GGUF model. |
| F-M14 | Must | Core | Active | The application must keep the original model file unchanged. |
| F-M15 | Must | Core | Active | The application must give the user a clear next step after a failure. |
| F-M18 | Must | Core | Active | The WinUI application must launch and control supported local command-line runtimes without requiring the user to enter terminal commands. |
| F-M19 | Must | Core | Active | Long-running operations included in the active first-release workflow must stream progress or output and support safe cancellation. |
| F-M21 | Must | App-integrated Experimental | Active | The application must run at least one verified TurboQuant-enabled Granite configuration end to end. |
| F-M22 | Must | Core | Active | A dependable upstream llama.cpp configuration must remain available when the TurboQuant route is unavailable or fails. |
| F-M28 | Must | Core | Active | The user must be able to copy generated chat output. |
| F-M16 | Should | Should | Active | The application should download at least one approved recommended Granite model from a fixed trusted source. |
| F-M17 | Should | Should | Active | If model downloading is implemented, the application should prevent partial, corrupt or unverified downloads from being used as valid models. |
| F-M20 | Should | Should | Active | The local chat should support at least two user turns in the same session. |
| F-M23 | Should | Should | Active | The application should create one new validated GGUF model artefact through a supported weight-quantisation workflow. |
| F-M24 | Should | Should | Active | If the application generates a new model artefact, it should create a processing manifest containing source/output hashes, tool/version, settings and result. |
| F-S01 | Should | Should | Active | The application should support drag-and-drop model import. |
| F-S02 | Should | Should | Active | The application should recognise complete OpenVINO IR model folders. |
| F-S03 | Should | Should | Active | The application should recognise selected Hugging Face/Safetensors model folders. |
| F-S04 | Should | Should | Active | The user should be able to change the requested context length before configuration selection. |
| F-S08 | Should | Should | Active | The user should be able to copy or save the technical inspection report. |
| F-S11 | Should | Should | Active | The application should provide one official OpenVINO GenAI inference route after its integration gate passes. |
| F-S12 | Should | Should | Active | The application should export benchmark results as CSV or JSON. |
| F-S15 | Should | Should | Active | The project should provide one verified source-to-OpenVINO model-preparation route. |
| C-02 | Could | Could | Active | The application could keep a local benchmark history. |
| C-03 | Could | Could | Active | The application could show charts for memory and speed trade-offs. |
| C-04 | Could | Deferred | Deferred | The application could support selected non-Granite GGUF models. |
| C-05 | Could | Deferred | Deferred | The project could explore DirectML as another Windows route. |
| C-06 | Could | Deferred | Deferred | The project could test an Intel NPU when suitable hardware is available. |
| C-07 | Could | Deferred | Deferred | The project could explore macOS and Metal after the Windows release. |
| C-08 | Could | Deferred | Deferred | The project could explore a cross-platform CMake backend. |
| C-09 | Could | Deferred | Deferred | The project could try a model near 32 billion parameters on stronger hardware. |
| C-10 | Could | Deferred | Deferred | The application could support more than one model session at a time. |
| F-M25 | Could | Deferred | Deferred | A future release could import and preserve at least one supported text-based knowledge file for a bounded retrieval workflow. |
| F-M26 | Could | Deferred | Deferred | A future release could create an uncompressed embedding baseline and use a pinned TurboVec implementation to compress or optimise vectors. |
| F-M27 | Could | Deferred | Deferred | A future release could retrieve relevant sections from a local index and provide them to the Granite chat workflow. |
| F-S05 | Should | Superseded | Superseded | The user should be able to cancel a long task. |
| F-S06 | Should | Superseded | Superseded | Chat answers should appear while they are being generated. |
| F-S07 | Should | Superseded | Superseded | The chat should support more than one turn in the same session. |
| F-S09 | Should | Superseded | Superseded | The application should create a new GGUF file through a supported weight-quantisation step. |
| F-S10 | Should | Superseded | Superseded | The application should save a processing record for each new model file. |
| F-S13 | Should | Superseded | Superseded | The main local inference route should not need a local web server or network port. |
| F-S14 | Should | Superseded | Superseded | The application should expose an AtomicBot TurboQuant route only after its gate passes. |
| C-01 | Could | Superseded | Superseded | The application could help the user find or download suitable models. |

Do not edit this category file independently of the controlled RTM workbook.
