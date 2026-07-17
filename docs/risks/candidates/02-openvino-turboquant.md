# OpenVINO and TurboQuant candidate risks

**Record state:** Candidate backlog — not yet assessed or baselined  
**Owner:** Arian B  
**Parent register:** `../Risk-Register.md`

These rows have been identified for review. Probability, impact, cause, trigger, mitigation, contingency, evidence and residual risk remain pending until the formal risk review.

| Risk ID | Category | Description | Record state | Probability | Impact | Owner | Status |
|---|---|---|---|---|---|---|---|
| R-067 | OpenVINO | The Granite model may fail to convert to OpenVINO format. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-068 | OpenVINO | The converted OpenVINO model may fail to load. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-069 | OpenVINO | The model may load but fail during text generation. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-070 | OpenVINO | The model tokenizer or generation configuration may be missing or incompatible. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-071 | OpenVINO | OpenVINO may run on a different device from the one requested. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-072 | OpenVINO | The application may report Intel GPU use while OpenVINO actually used the CPU. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-073 | OpenVINO | NPU detection may be mistaken for a working NPU inference route. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-074 | OpenVINO | CPU, GPU and NPU behaviour may differ between OpenVINO versions. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-075 | OpenVINO | OpenVINO conversion may reduce model quality. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-076 | OpenVINO | OpenVINO weight compression may be confused with KV-cache compression. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-077 | OpenVINO | OpenVINO GenAI APIs may change and break the adapter. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-078 | OpenVINO | OpenVINO dependencies may not be packaged correctly with the application. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-079 | OpenVINO | OpenVINO may require drivers or software not installed on the user's machine. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-080 | OpenVINO | An official OpenVINO route may work, but the custom TurboQuant/OpenVINO route may fail. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-081 | OpenVINO | llama.cpp and OpenVINO results may be compared unfairly using different model formats or settings. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-082 | TurboQuant | The TurboQuant fork may fail to compile on Windows. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-083 | TurboQuant | The selected TurboQuant route may not support the target Intel hardware. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-084 | TurboQuant | A TurboQuant command-line option may be accepted without TurboQuant actually running. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-085 | TurboQuant | The application may show the requested TurboQuant mode instead of the actual mode used. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-086 | TurboQuant | TurboQuant may save memory but reduce output quality too much. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-087 | TurboQuant | TurboQuant may save memory but make generation slower. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-088 | TurboQuant | TurboQuant may become unstable at longer context lengths. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-089 | TurboQuant | TurboQuant may cause crashes, hangs or memory leaks. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-090 | TurboQuant | The selected Granite model may not work correctly with the chosen TurboQuant implementation. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-091 | TurboQuant | The implementation may support only part of the formal TurboQuant method. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-092 | TurboQuant | A practical PolarQuant-style implementation may incorrectly be described as full TurboQuant. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-093 | TurboQuant | QJL may be claimed as active when it is not used by the selected format. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-094 | TurboQuant | The compressed KV cache may not be physically smaller even though a TurboQuant flag was used. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-095 | TurboQuant | Keys and values may require different compression formats, but the application may force one format for both. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-096 | TurboQuant | Certain attention-head sizes may require padding and reduce the expected memory saving. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-097 | TurboQuant | A fallback to normal cache formats may not be shown clearly to the user. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-098 | TurboQuant | The application may claim a published compression ratio that was not achieved in project testing. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
