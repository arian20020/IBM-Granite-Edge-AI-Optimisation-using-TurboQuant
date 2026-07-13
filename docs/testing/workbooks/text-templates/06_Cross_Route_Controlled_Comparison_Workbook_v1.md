# 06 Cross Route Controlled Comparison Workbook v1.1

**Controlled filename:** `06_Cross_Route_Controlled_Comparison_Workbook_v1.docx`  
**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`  
**Original source:** `06_Cross_Route_Editable_Comparison_Workbook.docx`  
**Original source SHA-256:** `accea4698f611e4695917cbc69c5fadd45f2cee3c786c11020b301ffd8bc2760`

## Cross-Repository and Runtime Controlled Comparison Workbook

Controlled retest revision 1.1. Historical results were removed from this working copy; the original source document is preserved under `docs/testing/source-material/original-workbooks/`.

Purpose: combine only validated results from the five completed route workbooks into one controlled decision view. Record exact versions, commands, logs and evidence paths. Freeze formal settings only after pilot tests succeed.

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
| Official OpenVINO | Official CPU/GPU baseline plus merged TBQ3/TBQ4 |  |  |  |  |  |
| Custom OpenVINO | Experimental CPU SDPA TBQ, QJL and Polar codec suite |  |  |  |  |  |

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
| Official OV standard |  |  |  |  |  |  |  |  |  |  |  |
| Official OV TBQ4 |  |  |  |  |  |  |  |  |  |  |  |
| Official OV TBQ3 |  |  |  |  |  |  |  |  |  |  |  |
| Official OV GPU/fallback |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV standard |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ4 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ3 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ4+QJL |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ3+QJL |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV Polar4 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV Polar3 |  |  |  |  |  |  |  |  |  |  |  |

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
| Official OV standard |  |  |  |  |  |  |  |  |  |  |  |
| Official OV TBQ4 |  |  |  |  |  |  |  |  |  |  |  |
| Official OV TBQ3 |  |  |  |  |  |  |  |  |  |  |  |
| Official OV GPU/fallback |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV standard |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ4 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ3 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ4+QJL |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV TBQ3+QJL |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV Polar4 |  |  |  |  |  |  |  |  |  |  |  |
| Custom OV Polar3 |  |  |  |  |  |  |  |  |  |  |  |

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
| Official OpenVINO | TBQ4 (U4/U4) |  |  |  |  |  |  | Merged official CPU SDPA |  |
| Official OpenVINO | TBQ3 (U3/U3) |  |  |  |  |  |  | Merged official CPU SDPA |  |
| Official OpenVINO | TBQ4/TBQ3 asymmetric |  |  |  |  |  |  | Merged official CPU SDPA |  |
| Official OpenVINO | TBQ key-only/value-only |  |  |  |  |  |  | Merged official CPU SDPA |  |
| Custom OpenVINO | TBQ4 |  |  |  |  |  |  | Experimental branch |  |
| Custom OpenVINO | TBQ3 |  |  |  |  |  |  | Experimental branch |  |
| Custom OpenVINO | TBQ4+QJL |  |  |  |  |  |  | Experimental branch |  |
| Custom OpenVINO | TBQ3+QJL |  |  |  |  |  |  | Experimental branch |  |
| Custom OpenVINO | Polar4 |  |  |  |  |  |  | Experimental branch |  |
| Custom OpenVINO | Polar3 |  |  |  |  |  |  | Experimental branch |  |
| Custom OpenVINO | Best asymmetric/cross-family pair |  |  |  |  |  |  | Experimental branch |  |

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
| Official OpenVINO CPU standard |  |  |  |  |  |  |  |
| Official OpenVINO CPU TBQ4 |  |  |  |  |  |  |  |
| Official OpenVINO CPU TBQ3 |  |  |  |  |  |  |  |
| Official OpenVINO GPU/fallback |  |  |  |  |  |  |  |
| Custom OpenVINO TBQ4 |  |  |  |  |  |  |  |
| Custom OpenVINO TBQ3 |  |  |  |  |  |  |  |
| Custom OpenVINO TBQ4+QJL |  |  |  |  |  |  |  |
| Custom OpenVINO TBQ3+QJL |  |  |  |  |  |  |  |
| Custom OpenVINO Polar4 |  |  |  |  |  |  |  |
| Custom OpenVINO Polar3 |  |  |  |  |  |  |  |
| Custom OpenVINO best asymmetric pair |  |  |  |  |  |  |  |

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
| A compression ratio was achieved | Your measured memory results reproduce it. |
| Quality loss is negligible | The fixed evaluation shows no unacceptable degradation. |
| Official OpenVINO TurboQuant works | A pinned merged official runtime exposes TBQ3/TBQ4, activation is verified, and matched tests complete without silent fallback. |
| Custom OpenVINO QJL works | The pinned experimental Runtime/GenAI pair proves QJL dispatch, record layout, output correctness and repeatable benefit. |
| Custom OpenVINO PolarQuant works | The pinned experimental Runtime/GenAI pair proves Polar dispatch, output correctness and repeatable benefit. |
| An OpenVINO codec is suitable for application integration | The exact codec/runtime pair passes reproducibility, quality, stability, maintenance and licensing gates; mere execution is insufficient. |
| Results apply broadly | The conclusion is limited to the tested models, commits and Intel i5-12450H laptop. |
