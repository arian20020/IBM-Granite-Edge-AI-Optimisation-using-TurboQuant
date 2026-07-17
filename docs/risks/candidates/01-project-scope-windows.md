# Project, scope, Windows, model-import and llama.cpp candidate risks

**Record state:** Candidate backlog — not yet assessed or baselined  
**Owner:** Arian B  
**Parent register:** `../Risk-Register.md`

These rows have been identified for review. Probability, impact, cause, trigger, mitigation, contingency, evidence and residual risk remain pending until the formal risk review.

| Risk ID | Category | Description | Record state | Probability | Impact | Owner | Status |
|---|---|---|---|---|---|---|---|
| R-003 | Project and schedule | Too much work may be included in the first release. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-004 | Project and schedule | Optional features may take time away from the main Windows, OpenVINO, TurboQuant and TurboVec work. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-005 | Project and schedule | The project may spend too much time on documentation and not enough time on working software. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-006 | Project and schedule | The project may spend too much time developing features and leave too little time for testing and the final report. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-007 | Project and schedule | A late technical problem may force important features to be removed. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-008 | Project and schedule | Delayed feedback from supervisors, IBM or Intel may cause rework. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-009 | Project and schedule | One delayed work package may block several later tasks. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-010 | Project and schedule | The workload may be too large for one developer. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-011 | Project and schedule | The project may keep adding new ideas instead of protecting the agreed first-release scope. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-012 | Project and schedule | Important Must-Have work may be started too late. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-013 | Project and schedule | The application may be demonstrated before it is stable enough. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-014 | Requirements and scope | The controlled requirements may not match the scope intended for implementation. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-015 | Requirements and scope | OpenVINO may remain marked as a Should Have even though it is intended to be part of the main release. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-016 | Requirements and scope | TurboVec may remain marked as deferred even though it is intended to be implemented. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-017 | Requirements and scope | The Project Definition, MoSCoW requirements and RTM may describe different project scopes. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-018 | Requirements and scope | A requirement may be changed without updating all related documents. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-019 | Requirements and scope | A requirement may be too vague to test properly. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-020 | Requirements and scope | A requirement may not have clear acceptance criteria. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-021 | Requirements and scope | A requirement may have no linked test or evidence location. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-022 | Requirements and scope | A feature may be described as complete even though only its documentation exists. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-023 | Requirements and scope | A task may be marked Verified before its evidence has been reviewed. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-024 | Requirements and scope | A change may be made without using the Change Request process. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-025 | Requirements and scope | Deferred or unfinished work may accidentally be described as implemented. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-026 | Windows and WinUI 3 | The application may work in Visual Studio but fail after installation. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-027 | Windows and WinUI 3 | The application may fail on the target Windows 11 x64 Intel computer. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-028 | Windows and WinUI 3 | The project may accidentally build an x86 or ARM64 component instead of x64. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-029 | Windows and WinUI 3 | A required native DLL or executable may be missing from the release package. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-030 | Windows and WinUI 3 | Windows Defender, SmartScreen or antivirus software may block the application or runtime. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-031 | Windows and WinUI 3 | File pickers may work during development but fail in the packaged application. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-032 | Windows and WinUI 3 | A Windows App SDK or NuGet update may break previously working code. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-033 | Windows and WinUI 3 | Long-running work may freeze the user interface. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-034 | Windows and WinUI 3 | The application may show old or incorrect status information during background work. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-035 | Windows and WinUI 3 | The application may close while a model process is still running. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-036 | Windows and WinUI 3 | Cancellation may stop the UI but not stop the child process. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-037 | Windows and WinUI 3 | Temporary files may remain after an error or cancellation. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-038 | Windows and WinUI 3 | The application may fail to recover after a runtime crash. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-039 | Windows and WinUI 3 | The packaged application may not find model or runtime files because paths change after installation. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-040 | Model import and validation | The application may accept an unsupported model as valid. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-041 | Model import and validation | A valid model may be rejected incorrectly. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-042 | Model import and validation | A corrupt or incomplete model file may be accepted. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-043 | Model import and validation | A GGUF file may use an unsupported architecture or tokenizer. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-044 | Model import and validation | An OpenVINO folder may contain an XML file without its required BIN file. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-045 | Model import and validation | The wrong XML and BIN files may be paired together. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-046 | Model import and validation | A model may be downloaded from an unofficial or unsafe source. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-047 | Model import and validation | A partial download may be used as though it were complete. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-048 | Model import and validation | A downloaded model may not match its expected SHA-256 hash. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-049 | Model import and validation | Model size or metadata may be read incorrectly. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-050 | Model import and validation | The model family, weight format or context limit may be identified incorrectly. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-051 | Model import and validation | The application may overwrite or damage the original model file. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-052 | Model import and validation | An already quantised model may be quantised again through an unsuitable route. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-053 | Model import and validation | A persistent model conversion may be confused with a runtime-only KV-cache setting. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-054 | Model import and validation | A generated model artefact may not load successfully after conversion. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-055 | llama.cpp | The pinned llama.cpp version may fail to build on the target computer. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-056 | llama.cpp | llama.cpp repository tests may fail. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-057 | llama.cpp | The selected Granite model may not load in the pinned llama.cpp version. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-058 | llama.cpp | llama.cpp may change its command-line options and break the application adapter. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-059 | llama.cpp | The application may parse llama.cpp output incorrectly. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-060 | llama.cpp | Warnings may be treated as successful results. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-061 | llama.cpp | Normal output may be treated as an error. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-062 | llama.cpp | The runtime may exit without returning a useful error message. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-063 | llama.cpp | Streaming output may stop or become corrupted. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-064 | llama.cpp | The process may hang and never return control to the application. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-065 | llama.cpp | The upstream llama.cpp fallback may fail when it is needed. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-066 | llama.cpp | A later llama.cpp update may change model quality, memory use or performance. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
