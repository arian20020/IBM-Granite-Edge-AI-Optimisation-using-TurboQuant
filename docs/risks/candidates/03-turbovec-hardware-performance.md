# TurboVec, Intel hardware, memory and performance candidate risks

**Record state:** Candidate backlog — not yet assessed or baselined  
**Owner:** Arian B  
**Parent register:** `../Risk-Register.md`

These rows have been identified for review. Probability, impact, cause, trigger, mitigation, contingency, evidence and residual risk remain pending until the formal risk review.

| Risk ID | Category | Description | Record state | Probability | Impact | Owner | Status |
|---|---|---|---|---|---|---|---|
| R-099 | TurboVec | A suitable TurboVec implementation may not be found. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-100 | TurboVec | The selected TurboVec implementation may not have a clear licence. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-101 | TurboVec | TurboVec may not compile or run on Windows. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-102 | TurboVec | TurboVec may not support the target Intel hardware. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-103 | TurboVec | The exact TurboVec version or commit may not be pinned. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-104 | TurboVec | The input and output vector formats may not be understood correctly. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-105 | TurboVec | TurboVec may not integrate with the selected embedding system. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-106 | TurboVec | The knowledge-file import and text-extraction process may fail on some files. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-107 | TurboVec | Document chunking may split information in poor places. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-108 | TurboVec | Embedding generation may require too much memory or time. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-109 | TurboVec | TurboVec compression may reduce retrieval quality. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-110 | TurboVec | Relevant document sections may not be returned. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-111 | TurboVec | Irrelevant document sections may be passed into Granite. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-112 | TurboVec | Retrieval may make the final answer worse rather than better. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-113 | TurboVec | The TurboVec index may become corrupt. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-114 | TurboVec | A saved index may not match the document or embedding model that created it. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-115 | TurboVec | TurboVec may save storage but make indexing or retrieval slower. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-116 | TurboVec | The application may compare TurboVec against an unfair or unmatched baseline. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-117 | TurboVec | TurboVec may be described as compressing the original document rather than its vectors. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-118 | TurboVec | TurboVec implementation work may take too much time away from the core application. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-119 | TurboVec | The project may complete only a command-line TurboVec demonstration but describe it as full application integration. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-120 | TurboVec | Knowledge-file content may be stored in logs, temporary files or indexes without clear user control. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-121 | Intel hardware and memory | The application may detect the wrong CPU, GPU, NPU or memory information. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-122 | Intel hardware and memory | Shared GPU memory may be confused with dedicated GPU memory. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-123 | Intel hardware and memory | The application may recommend a model that does not actually fit in memory. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-124 | Intel hardware and memory | The application may reject a model that would actually fit safely. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-125 | Intel hardware and memory | The memory estimate may include only model weights and ignore the KV cache. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-126 | Intel hardware and memory | The estimate may ignore runtime memory, Windows memory and safety reserve. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-127 | Intel hardware and memory | The KV-cache formula may use the wrong layer, head, dimension or datatype values. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-128 | Intel hardware and memory | A model may load but leave too little memory for Windows to remain stable. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-129 | Intel hardware and memory | CPU and GPU memory may be counted twice. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-130 | Intel hardware and memory | A larger context length may cause an unexpected out-of-memory failure. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-131 | Intel hardware and memory | Intel GPU drivers may not support the selected runtime route. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-132 | Intel hardware and memory | A driver update may change performance or break compatibility. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-133 | Intel hardware and memory | A detected Intel NPU may not support the required model or operations. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-134 | Intel hardware and memory | Thermal throttling may reduce performance during long tests. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-135 | Intel hardware and memory | Battery mode may produce different results from plugged-in mode. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-136 | Intel hardware and memory | Background applications may affect RAM and performance results. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-137 | Intel hardware and memory | Results from one Intel computer may be presented as applying to every Intel computer. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-138 | Intel hardware and memory | The Automatic mode may choose an untested model, cache, runtime or device combination. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-139 | Performance | Model loading may take too long. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-140 | Performance | Time to first token may be too slow. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-141 | Performance | Generation speed may be too low for practical use. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-142 | Performance | TurboQuant or TurboVec may introduce more overhead than expected. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-143 | Performance | Performance measurements may change greatly between runs. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-144 | Performance | Warm-up runs may be included incorrectly in the final measurements. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-145 | Performance | Too few measured repetitions may produce misleading results. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-146 | Performance | Performance may be measured while Windows updates or antivirus scans are running. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-147 | Performance | Different power modes may make comparisons unfair. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-148 | Performance | Different context lengths may be compared as though they were identical tests. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-149 | Performance | llama.cpp and OpenVINO may use different model precisions, making speed comparisons unfair. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
| R-150 | Performance | Faster performance may be reported without mentioning a major quality loss. | Candidate | Pending assessment | Pending assessment | Arian B | Open |
