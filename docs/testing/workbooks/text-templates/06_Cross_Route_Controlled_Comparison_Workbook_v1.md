# 06 Cross Route Controlled Comparison Workbook v1

**Source file:** `06_Cross_Route_Controlled_Comparison_Workbook_v1.docx`  
**SHA-256:** `325b0e99ae46c4f7d5ebfcb5364af416a5213d228ce074aee3e3ee7591bbfffc`

## Cross-Repository and Runtime Controlled Comparison Workbook

Controlled retest template v1. Source: 06_Cross_Route_Editable_Comparison_Workbook.docx. Source SHA-256: accea4698f611e4695917cbc69c5fadd45f2cee3c786c11020b301ffd8bc2760. Historical results were removed from this working copy; the original source document is preserved under docs/testing/source-material/original-workbooks/.

Purpose: combine only validated results from the five completed route workbooks into one controlled decision view.

Use this workbook only after source route results are validated. Every value must cite exact test IDs, run IDs, evidence paths and commits.

# 1. Scope and completion status

| Field | Record |
|---|---|
| Laptop | Lenovo-PF4HMD0T - Intel Core i5-12450H - 16 GB RAM - Intel UHD Graphics |
| Models | Known-supported diagnostic model; selected Granite 3B; selected Granite 8B |
| NPU | Excluded |
| Formal context |  |
| Fixed prompt set version |  |
| Comparison date |  |
| Status values | Not started / In progress / Pass / Fail / Blocked / Research-only / Not supported |
| Decision values | Accepted / Accepted with limitations / Experimental / Fallback / Research-only / Rejected / Not supported |

## 2. Route completion and role

| Route | Purpose | Build | Diagnostic | Granite 3B | Granite 8B | Final role |
|---|---|---|---|---|---|---|
| Upstream llama.cpp | Standard GGUF reference |  |  |  |  |  |
| AtomicBot | llama.cpp TurboQuant candidate |  |  |  |  |  |
| animehacker | TQ3_0 comparator |  |  |  |  |  |
| Official OpenVINO | Official CPU/GPU fallback |  |  |  |  |  |
| Custom OpenVINO | CPU SDPA TBQ4/TBQ3 |  |  |  |  |  |

# 3. Model and device compatibility matrix

| Route | 3B CPU | 3B GPU | 8B CPU | 8B GPU | TurboQuant CPU | TurboQuant GPU | CPU fallback / hybrid notes |
|---|---|---|---|---|---|---|---|
| Upstream llama.cpp |  |  |  |  |  |  |  |
| AtomicBot |  |  |  |  |  |  |  |
| animehacker |  |  |  |  |  |  |  |
| Official OpenVINO |  |  |  |  |  |  |  |
| Custom OpenVINO |  |  |  |  |  |  |  |

# 4. Granite 3B results in one view

| Route | Configuration | Device | Context | Peak RAM MB | KV MB | Load ms | TTFT ms | Tok/s | Quality /10 | Stable? | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|
| llama.cpp high-quality |  |  |  |  |  |  |  |  |  |  |  |
| llama.cpp practical |  |  |  |  |  |  |  |  |  |  |  |
| AtomicBot best standard |  |  |  |  |  |  |  |  |  |  |  |
| AtomicBot best TQ |  |  |  |  |  |  |  |  |  |  |  |
| animehacker standard |  |  |  |  |  |  |  |  |  |  |  |
| animehacker TQ3_0 |  |  |  |  |  |  |  |  |  |  |  |
| OpenVINO high-quality |  |  |  |  |  |  |  |  |  |  |  |
| OpenVINO practical |  |  |  |  |  |  |  |  |  |  |  |
| OpenVINO GPU |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV standard |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ4 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ3 |  |  |  |  |  |  |  |  |  |  |  |

## 5. Granite 3B conclusions

| Field | Record |
|---|---|
| Best quality configuration |  |
| Lowest-memory configuration |  |
| Fastest CPU configuration |  |
| Fastest GPU configuration |  |
| Best practical configuration |  |
| Best TurboQuant configuration |  |
| Maximum stable context |  |
| Main limitations |  |

# 6. Granite 8B results in one view

| Route | Configuration | Device | Context | Peak RAM MB | KV MB | Load ms | TTFT ms | Tok/s | Quality /10 | Stable? | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|
| llama.cpp high-quality fit |  |  |  |  |  |  |  |  |  |  |  |
| llama.cpp practical |  |  |  |  |  |  |  |  |  |  |  |
| AtomicBot best standard |  |  |  |  |  |  |  |  |  |  |  |
| AtomicBot best TQ |  |  |  |  |  |  |  |  |  |  |  |
| animehacker standard |  |  |  |  |  |  |  |  |  |  |  |
| animehacker TQ3_0 |  |  |  |  |  |  |  |  |  |  |  |
| OpenVINO high-quality fit |  |  |  |  |  |  |  |  |  |  |  |
| OpenVINO practical |  |  |  |  |  |  |  |  |  |  |  |
| OpenVINO GPU |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV standard |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ4 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ3 |  |  |  |  |  |  |  |  |  |  |  |

## 7. Granite 8B conclusions

| Field | Record |
|---|---|
| Does 8B fit safely? |  |
| Best quality configuration |  |
| Lowest-memory configuration |  |
| Fastest CPU configuration |  |
| Best GPU/hybrid configuration |  |
| Best TurboQuant configuration |  |
| Maximum stable context |  |
| Main limitations |  |

# 8. TurboQuant evidence comparison

| Route | TQ format | Activation confirmed? | No silent fallback? | Execution device | Memory reduction | Speed effect | Quality effect | Implementation class | Evidence |
|---|---|---|---|---|---|---|---|---|---|
| AtomicBot | turbo4 |  |  |  |  |  |  |  |  |
| AtomicBot | turbo3 |  |  |  |  |  |  |  |  |
| AtomicBot | turbo2 |  |  |  |  |  |  |  |  |
| animehacker | TQ3_0 |  |  |  |  |  |  |  |  |
| Custom OpenVINO | TBQ4 |  |  |  |  |  |  |  |  |
| Custom OpenVINO | TBQ3 |  |  |  |  |  |  |  |  |

## 9. Operational comparison

| Criterion | Upstream llama.cpp | AtomicBot | animehacker | Official OpenVINO | Custom OpenVINO |
|---|---|---|---|---|---|
| Windows reproducibility |  |  |  |  |  |
| Granite 3B support |  |  |  |  |  |
| Granite 8B support |  |  |  |  |  |
| Intel CPU support |  |  |  |  |  |
| Intel GPU support |  |  |  |  |  |
| Fallback clarity |  |  |  |  |  |
| Memory efficiency |  |  |  |  |  |
| Speed |  |  |  |  |  |
| Quality preservation |  |  |  |  |  |
| Maximum context |  |  |  |  |  |
| Stability |  |  |  |  |  |
| Failure diagnostics |  |  |  |  |  |
| Application integration difficulty |  |  |  |  |  |
| Repository/runtime maturity |  |  |  |  |  |

# 10. Final route decisions

| Route | Status | Application role | Supported models | Supported hardware | Best configuration | Main reason | Evidence path |
|---|---|---|---|---|---|---|---|
| Upstream llama.cpp |  |  |  |  |  |  |  |
| AtomicBot CPU TurboQuant |  |  |  |  |  |  |  |
| AtomicBot GPU TurboQuant |  |  |  |  |  |  |  |
| animehacker TQ3_0 |  |  |  |  |  |  |  |
| Official OpenVINO CPU |  |  |  |  |  |  |  |
| Official OpenVINO GPU |  |  |  |  |  |  |  |
| Custom OpenVINO TBQ4 |  |  |  |  |  |  |  |
| Custom OpenVINO TBQ3 |  |  |  |  |  |  |  |

## 11. Final recommendation summary

| Field | Record |
|---|---|
| Primary GGUF route |  |
| Fallback GGUF route |  |
| Primary OpenVINO route |  |
| Experimental OpenVINO route |  |
| Recommended Granite 3B configuration |  |
| Recommended Granite 8B configuration |  |
| Recommended CPU mode |  |
| Recommended GPU/hybrid mode |  |
| Routes retained as research-only |  |
| Routes rejected |  |
| Main evidence-based conclusion |  |

# 12. Claims control

| Claim | Allowed only when |
|---|---|
| TurboQuant works on Intel GPU | TurboQuant-specific GPU placement is verified; GPU activity alone is insufficient. |
| Granite supports TurboQuant | The selected Granite model generates with TurboQuant active and no silent fallback. |
| A compression ratio was achieved | Measured memory results reproduce it under matched conditions. |
| Quality loss is negligible | The fixed evaluation shows no unacceptable degradation and limitations are stated. |
| OpenVINO TurboQuant works | The custom Runtime, GenAI pair and TBQ configuration are reproducibly successful. |
| Results apply broadly | The conclusion is explicitly limited to the tested models, commits and Intel laptop. |

# 13. Comparison validity control

Every row must be labelled `Matched`, `Partially matched` or `Not directly comparable` in `Cross-Route-Comparison-Register.csv`. Never calculate a percentage difference across different model revisions, weight precisions, contexts, prompts, devices or metric definitions without clearly labelling the mismatch.
