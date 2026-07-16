# AtomicBot TurboQuant Source Audit

- Repository: `C:\Users\Student\experiments\granite_turboquant_intel\repositories\atomic-llama-cpp-turboquant`
- Commit: `519f0c594a8e31467d2e2f2cf17054c9e7e11536`
- Branch at pinning: `DETACHED`
- Preliminary classification: **Full candidate â€” requires runtime activation proof**

> This is a source-location audit, not runtime activation proof.

## turbo_cli_and_types

Captured matches: 500

```text
MTP.md:335:   command-buffer status 3 on turbo3 KV before this guard).
MTP.md:455:CTVD` — both target and assistant inherit the same default (`turbo3`).
MTP.md:458:`{model} × {f16-base, turbo3-base, f16-mtp, turbo3-mtp} × {short=128, long=512}
MTP.md:493:| gemma-E4B  | turbo3-base  | 53.41 | 53.45 | — | — | — | — |
MTP.md:494:| gemma-E4B  | turbo3-mtp   | **67.83** | **64.47** | 82.6% | 72.3% | **+27.0%** | **+20.6%** |
MTP.md:497:| gemma-26B  | turbo3-base  | 51.75 | 49.45 | — | — | — | — |
MTP.md:498:| gemma-26B  | turbo3-mtp   | **80.50** | **69.21** | 84.9% | 66.1% | **+55.6%** | **+40.0%** |
MTP.md:501:| gemma-31B  | turbo3-base  | 15.73 | 15.44 | — | — | — | — |
MTP.md:502:| gemma-31B  | turbo3-mtp   | **19.36** | **16.31** | 88.0% | 70.7% | **+23.1%** | **+5.6%** |
MTP.md:506:- **turbo3 MTP is the sweet spot across all three targets.** The asymmetric jump
MTP.md:507:  on 26B (+55.6% short, +40.0% long over `turbo3-base`) reflects that 26B is
MTP.md:514:  `turbo3` KV + MTP — this matrix only covers the homogeneous KV cells, but
MTP.md:515:  the practical lift on heterogeneous KV is in line with the `turbo3-mtp`
MTP.md:521:- **31B is bandwidth-bound** (`turbo3-base 15.73 > f16-base` on long was
MTP.md:523:  so turbo3 KV + MTP is the clear pick.
MTP.md:550:load that day (the `turbo3-mtp` long cell, the harder case, was unaffected).
NEXTN.md:160:| qwen-27B dense | turbo3-base    | 19.71 | 18.74 | — | — | — | — |
NEXTN.md:161:| qwen-27B dense | turbo3-nextn   | **20.75** | **19.73** | 85.5% | 78.7% | **+5.3%** | **+5.3%** |
NEXTN.md:164:| qwen-35B-A3B MoE | turbo3-base  | 61.84 | 62.01 | — | — | — | — |
NEXTN.md:165:| qwen-35B-A3B MoE | turbo3-nextn | **82.73** | **77.20** | 82.9% | 80.6% | **+33.8%** | **+24.5%** |
NEXTN.md:169:prompt lengths. Wins range from **+24% (turbo3, long)** to **+36% (f16, short)**, on top of
NEXTN.md:170:the +13% TurboQuant memory-bandwidth lift from `turbo3` KV.
NEXTN.md:178:−7.6% / −11.9% on long for f16-nextn / turbo3-nextn respectively). `turbo3` KV adds ~5% extra
NEXTN.md:184:| Bench log (mtime) | Path | 27B f16-nextn long (Δ vs f16-base) | 27B turbo3-nextn long (Δ vs turbo3-base) | Note |
NEXTN.md:206:| `qwen36-ud-v2-turbo3.txt` | `attn_q` / `attn_k` at `q6_K` (stack with TurboQuant3 KV) |
NEXTN.md:230:- **NextN-preserve mask (V1)** — every `blk.*.nextn.*` and `mtp.*` tensor pinned to `Q8_0`. The cost is ~10 MiB of file size; the win is that the draft head stays close to BF16 fidelity, which keeps `acceptance` high under `--spec-type nextn`. Plain UD quants compress the head at the same bit-width as the body and bleed acceptance under `turbo3` KV.
NEXTN.md:231:- **TurboQuant3-friendly mask (V2)** — attention Q/K bumped to `Q6_K`. This is the piece we tuned specifically for this fork: when KV is compressed to 3-bit via `-ctk turbo3 -ctv turbo3`, the attention scores see extra dequant noise on K, so giving Q/K a little more headroom on the weight side cancels most of it out.
NEXTN.md:262:| Qwen 3.6-35B-A3B-UDT-Q4_K_XL_MTP | `nextn` | turbo3 | F16 | recognised | OK | ~69 t/s |
NEXTN.md:263:| Gemma 4-26B-A4B-it-UD-Q4_K_XL    | `mtp`   | turbo3 | F16 | recognised | OK | ~55 t/s |
README.md:22:- **TurboQuant KV cache & weights: WHT-rotated low-bit quantization with backend-native kernels (Metal `TurboFlash`, CUDA, Vulkan, HIP). Use `-ctk turbo3 -ctv turbo3` for ~4.3× KV compression, or quantize weights to `TQ4_1S`/`TQ3_1S`. See [Compression below](#turboquant-kv-cache--weight-compression).**
README.md:29:- **This fork:** `--mmproj` can be loaded **alongside** `mtp` / `nextn` / `eagle3` speculative decoding on a single slot (validated on Qwen 3.6-35B-A3B-UDT + NextN + turbo3 KV and Gemma 4-26B-A4B + MTP + turbo3 KV). Draft acceleration applies to **text-only turns**; image-bearing turns fall back to plain target decoding (image still recognised). Other spec types remain disabled with multimodal. Details: [docs/speculative.md](./docs/speculative.md) and [NEXTN.md](./NEXTN.md) §10.
README.md:151:| gemma-E4B | turbo3-mtp  | **67.8** | **64.5** | 82.6 % | 72.3 % |
README.md:154:| gemma-26B | turbo3-mtp  | **80.5**  | **69.2** | 84.9 % | 66.1 % |
README.md:157:| gemma-31B | turbo3-mtp  | **19.4**  | **16.3** | 88.0 % | 70.7 % |
README.md:195:- **Composes with TurboQuant3 KV** (`-ctk turbo3 -ctv turbo3`) — on MoE
README.md:213:- **TurboQuant3-friendly mask** — `attn_q` / `attn_k` bumped to `Q6_K` so the file pairs cleanly with `-ctk turbo3 -ctv turbo3`.
README.md:217:Collection: [AtomicChat — Qwen 3.6 UDT](https://huggingface.co/collections/AtomicChat/qwen-36-udt-atomicchat-6a0481f5cc5a057c07759176). Full recipe & runbook: [docs/qwen-udt/RUNBOOK.md](docs/qwen-udt/RUNBOOK.md). Mask files: [`scripts/quantize-masks/qwen36-ud-{base,v1-nextn,v2-turbo3,v3-combined}.txt`](scripts/quantize-masks).
README.md:231:  -ctk turbo3 -ctv turbo3 -fa on \
README.md:242:  -c 8192 -ngl 99 -ngld 99 -ctk turbo3 -ctv turbo3 -fa on
README.md:267:| qwen-27B dense   | turbo3-base    | 19.7 | 18.7 | — | — |
README.md:268:| qwen-27B dense   | turbo3-nextn   | **20.8** | **19.7** | 85.5 % | 78.7 % |
README.md:271:| qwen-35B-A3B MoE | turbo3-base    | 61.8 | 62.0 | — | — |
README.md:272:| qwen-35B-A3B MoE | **turbo3-nextn** | **82.7** | **77.2** | 82.9 % | 80.6 % |
README.md:303:- **KV cache compression** — `TURBO2_0` / `TURBO3_0` / `TURBO4_0` (2/3/4-bit,
README.md:313:| `turbo2` | 2 | ~6.4× | maximum compression, intended for large-context budgets |
README.md:314:| `turbo3` | 3 | ~4.3× | **recommended default**; Metal `TurboFlash` decode kernel |
README.md:315:| `turbo4` | 4 | ~3.8× | highest accuracy of the family, safest fallback |
README.md:321:  -ctk turbo3 -ctv turbo3 -fa on
README.md:346:| Backend | KV `turbo2` / `turbo3` / `turbo4` | Weights `TQ3_1S` / `TQ4_1S` |
README.md:348:| Metal (Apple Silicon) | yes; `TurboFlash` flash-attn decode kernel for `turbo3` (off-by-default on Apple10 — see PR #91) | yes (V2.1 fused kernels) |
README.md:349:| CUDA (NVIDIA) | `turbo3` / `turbo4` (full); `turbo2` via reference path | `TQ4_1S` MUL_MAT_VEC |
README.md:350:| Vulkan | `turbo3` KV (FA + coopmat), `SET_ROWS` for `turbo2/4` | `TQ4_1S` (specialised MUL_MAT_VEC, SET_ROWS, CPY) |
README.md:351:| HIP / ROCm | `turbo3` KV; F16-K + TURBO-V mixed dispatch | reference |
README.md:369:`turbo2` / `turbo3` / `turbo4` KV and `TQ3_1S` / `TQ4_1S` weights work on every
README.md:377:(`turbo3` KV + MTP) is the right pick when the target model is bandwidth-bound
README.md:382:[NEXTN.md §7](NEXTN.md). The matrix bench shows that `turbo3` KV + NextN
README.md:384:**+24-36 % tps** over the `turbo3-base` baseline at single-slot), and lifts
README.md:385:the dense Qwen 3.6 27B by ~5 % on top of `turbo3-base` despite the model
README.md:789:    Composes with TurboQuant3 KV (`-ctk turbo3 -ctv turbo3`) — on Qwen 3.6
README.md:812:      -ctk turbo3 -ctv turbo3 -fa on \
README.md:823:      -c 8192 -ngl 99 -ngld 99 -ctk turbo3 -ctv turbo3 -fa on
README.md:852:    long contexts. Recommended default is **`turbo3`** (3-bit, ~4.3× vs F16,
README.md:859:      -ngl 99 -ctk turbo3 -ctv turbo3 -fa on
README.md:865:    -ctk turbo2 -ctv turbo2   # 2-bit KV, ~6.4x vs F16 (highest compression)
README.md:866:    -ctk turbo3 -ctv turbo3   # 3-bit KV, ~4.3x  (default sweet spot)
README.md:867:    -ctk turbo4 -ctv turbo4   # 4-bit KV, ~3.8x  (highest accuracy / fallback)
TURBOQUANT_UPSTREAM_MERGE.md:15:- Turbo KV A/B vs f16 baseline (gemma-4-12B-it-Q8_0): turbo4/turbo3 match f16,
TURBOQUANT_UPSTREAM_MERGE.md:16:  turbo2 coherent. The attn-rotation default-off policy holds (no double-rotate).
TURBOQUANT_UPSTREAM_MERGE.md:20:- **CUDA TurboQuant (Gabe Ortiz / signalnine, 27 commits):** turbo2/3/4 CUDA
TURBOQUANT_UPSTREAM_MERGE.md:35:- **Vulkan turbo3 KV cache:** dequant/get_rows/set_rows/cpy pipelines (the FA
TURBOQUANT_UPSTREAM_MERGE.md:68:## DEFERRED — Vulkan turbo3 flash-attention re-port (NOT lost)
TURBOQUANT_UPSTREAM_MERGE.md:71:sync. The fork's turbo3 Vulkan FA (`flash_attn_cm1.comp`,
TURBOQUANT_UPSTREAM_MERGE.md:73:newer FA changes; per decision, upstream's FA was taken and the turbo3 Vulkan FA
TURBOQUANT_UPSTREAM_MERGE.md:74:must be re-ported and validated on the AMD RDNA4 box. The turbo3 KV-cache Vulkan
TURBOQUANT_UPSTREAM_MERGE.md:78:- `a09bafedd` vulkan: restore turbo_wht op + turbo3/4 FA dispatch
TURBOQUANT_UPSTREAM_MERGE.md:79:- `ff8bb7394` (Simon Gardling) vulkan: fix and complete turbo3 KV cache support
TURBOQUANT_UPSTREAM_MERGE.md:80:- `a494833d0` / `0198d5819` (Tuklus-Labs) Vulkan turbo3 KV + coopmat FA
TURBOQUANT_UPSTREAM_MERGE.md:83:emits turbo3 FA SPIR-V, so the Vulkan build will need reconciliation on the AMD
TURBOQUANT_UPSTREAM_MERGE.md:88:- [ ] AMD RDNA4 box: build Vulkan, reconcile shader-gen, re-port turbo3 FA,
common/arg.cpp:400:    GGML_TYPE_TURBO2_0,
common/arg.cpp:401:    GGML_TYPE_TURBO3_0,
common/arg.cpp:402:    GGML_TYPE_TURBO4_0,
docs/qwen-udt/RUNBOOK.md:9:| `v2` | `scripts/quantize-masks/qwen36-ud-v2-turbo3.txt` | Bump `attn_q` / `attn_k` to `q6_K` (TurboQuant3 KV stack) |
docs/qwen-udt/RUNBOOK.md:108:export BENCH_MODES_FILTER='f16-nextn,turbo3-nextn'   # v1 focus
docs/rocm-mi300x-test-results.md:5:TurboQuant KV cache compression (turbo2/turbo3/turbo4) builds and runs correctly on AMD Instinct MI300X (gfx942) and MI355X (gfx950). MI300X requires zero code changes. MI355X requires adding CDNA4 arch defines to the HIP vendor header.
docs/rocm-mi300x-test-results.md:39:| turbo3 | ~25,200 | ~160 | **+3%** | 88% |
docs/rocm-mi300x-test-results.md:40:| turbo4 | 25,427 ± 17 | 161.1 ± 0.2 | **+4%** | 89% |
docs/rocm-mi300x-test-results.md:47:| turbo3 | 39,140 ± 475 | 162.3 ± 0.1 | 98% | 64% |
docs/rocm-mi300x-test-results.md:48:| turbo4 | 39,232 ± 508 | 214.1 ± 0.7 | 98% | **84%** |
docs/rocm-mi300x-test-results.md:54:3. **MI355X turbo4 decode at 84%** — turbo4 outperforms turbo3 in decode due to simpler 4-bit dequant.
docs/rocm-mi300x-test-results.md:55:4. **MI355X turbo3 decode at 64%** — the 3-bit codebook + sign extraction is more expensive on gfx950.
docs/rocm-mi300x-test-results.md:75:  -m model.gguf -ctk turbo3 -ctv turbo3 -ngl 99 -r 3 -p 512 -n 128
docs/speculative.md:47:  -ctk turbo3 -ctv turbo3 \
docs/speculative.md:48:  -ctkd turbo3 -ctvd turbo3 \
docs/speculative.md:174:Pair with `-ctk turbo3 -ctv turbo3` to compose with **TurboQuant** KV — on MoE targets (e.g. Qwen 3.6 35B-A3B) this combination is **+24-36% tps** over the `turbo3` baseline at single-slot in the matrix bench (see `NEXTN.md §7`).
ggml/include/ggml.h:432:        GGML_TYPE_TURBO2_0 = 42, // TurboQuant 2-bit KV cache: WHT + 2-bit PolarQuant
ggml/include/ggml.h:433:        GGML_TYPE_TURBO3_0 = 43, // TurboQuant 3-bit KV cache: WHT + 3-bit PolarQuant
ggml/include/ggml.h:434:        GGML_TYPE_TURBO4_0 = 44, // TurboQuant 4-bit KV cache: WHT + 4-bit PolarQuant
ggml/src/ggml-common.h:286:#define QK_TURBO3 128   // Block size 128: one block per rotation group, eliminates redundant norms
ggml/src/ggml-common.h:287:#define QK_TURBO3_GROUP 128  // rotation group size = head_dim
ggml/src/ggml-common.h:289:#define NL_TURBO3     (QK_TURBO3 / 16)   // non-vec FA iterations per block
ggml/src/ggml-common.h:290:#define NL_TURBO3_VEC (QK_TURBO3 / 4)    // vec FA iterations per block
ggml/src/ggml-common.h:293:    uint8_t    qs[QK_TURBO3 / 4];      //  8 bytes: lower 2-bit indices (4 per byte)
ggml/src/ggml-common.h:294:    uint8_t    signs[QK_TURBO3 / 8];   //  4 bytes: upper 1-bit of 3-bit index (8 per byte)
ggml/src/ggml-common.h:295:} block_turbo3_0;                       // 14 bytes total
ggml/src/ggml-common.h:296:static_assert(sizeof(block_turbo3_0) == sizeof(ggml_half) + QK_TURBO3/4 + QK_TURBO3/8, "wrong turbo3_0 block size/padding");
ggml/src/ggml-common.h:299:// TURBO4_USE_4BIT: switch between 4-bit PolarQuant (new) and 3-bit+QJL (legacy)
ggml/src/ggml-common.h:301:#ifndef TURBO4_USE_4BIT
ggml/src/ggml-common.h:302:#  define TURBO4_USE_4BIT 1
ggml/src/ggml-common.h:305:#define QK_TURBO4 128
ggml/src/ggml-common.h:307:#if TURBO4_USE_4BIT
ggml/src/ggml-common.h:314:    uint8_t    qs[QK_TURBO4 / 2];      // 64 bytes: 4-bit PolarQuant indices (nibble packed)
ggml/src/ggml-common.h:315:} block_turbo4_0;                       // 68 bytes total
ggml/src/ggml-common.h:316:static_assert(sizeof(block_turbo4_0) == 68, "wrong turbo4_0 block size");
ggml/src/ggml-common.h:324:    uint8_t    qs[QK_TURBO4 * 3 / 8];  // 48 bytes: 3-bit PolarQuant indices
ggml/src/ggml-common.h:325:    uint8_t    signs[QK_TURBO4 / 8];   // 16 bytes: 1-bit QJL signs
ggml/src/ggml-common.h:326:} block_turbo4_0;                       // 68 bytes total
ggml/src/ggml-common.h:327:static_assert(sizeof(block_turbo4_0) == 2*sizeof(ggml_half) + QK_TURBO4*3/8 + QK_TURBO4/8, "wrong turbo4_0 block size");
ggml/src/ggml-common.h:330:static_assert(QK_TURBO4 == 128, "turbo4 kernels assume QK_TURBO4 == 128");
ggml/src/ggml-common.h:336:#define QK_TURBO2 128   // Block size 128: one block per rotation group
```

## wht_hadamard

Captured matches: 163

```text
README.md:22:- **TurboQuant KV cache & weights: WHT-rotated low-bit quantization with backend-native kernels (Metal `TurboFlash`, CUDA, Vulkan, HIP). Use `-ctk turbo3 -ctv turbo3` for ~4.3× KV compression, or quantize weights to `TQ4_1S`/`TQ3_1S`. See [Compression below](#turboquant-kv-cache--weight-compression).**
README.md:295:> Huge thanks for the original WHT-rotated quantization design, the reference
README.md:300:of WHT-rotated low-bit quantization formats with backend-native kernels. They
README.md:304:  WHT + PolarQuant). Selected at runtime via `-ctk` / `-ctv`.
README.md:305:- **Model weight compression** — `TQ3_1S` / `TQ4_1S` (3/4-bit, WHT-rotated
README.md:332:| `TQ3_1S` | 3 | 32 | 8-level Lloyd-Max + WHT rotation |
README.md:333:| `TQ4_1S` | 4 | 32 | 16-level Lloyd-Max + WHT rotation; fused Metal/Vulkan MUL_MAT_VEC kernels |
TURBOQUANT_UPSTREAM_MERGE.md:22:  WHT, InnerQ, sparse-V dequant, MLA fixes, cross-type VEC FA, D=640 MMA FA.
docs/rocm-mi300x-test-results.md:17:## WHT Kernel Correctness
docs/rocm-mi300x-test-results.md:19:Standalone roundtrip test (forward WHT → inverse WHT) confirms the Walsh-Hadamard Transform kernel works correctly on HIP with 64-wide wavefronts:
docs/rocm-mi300x-test-results.md:22:=== TurboQuant WHT Roundtrip Test (HIP/gfx942) ===
docs/rocm-mi300x-test-results.md:24:Forward WHT zeros: 0 / 512
ggml/include/ggml.h:432:        GGML_TYPE_TURBO2_0 = 42, // TurboQuant 2-bit KV cache: WHT + 2-bit PolarQuant
ggml/include/ggml.h:433:        GGML_TYPE_TURBO3_0 = 43, // TurboQuant 3-bit KV cache: WHT + 3-bit PolarQuant
ggml/include/ggml.h:434:        GGML_TYPE_TURBO4_0 = 44, // TurboQuant 4-bit KV cache: WHT + 4-bit PolarQuant
ggml/include/ggml.h:435:        GGML_TYPE_TQ3_1S  = 45, // TurboQuant 3-bit weight: WHT-rotated 8-level Lloyd-Max, block_size=32
ggml/include/ggml.h:436:        GGML_TYPE_TQ4_1S  = 46, // TurboQuant 4-bit weight: WHT-rotated 16-level Lloyd-Max, block_size=32
ggml/include/ggml.h:2582:    // TurboQuant Walsh-Hadamard Transform (O(d log d) rotation for KV cache compression)
ggml/include/ggml.h:2583:    // Applies WHT rotation to 128-element groups along ne[0]: sign1 → butterfly → sign2 → normalize
ggml/include/ggml.h:2584:    // direction: 0 = forward (signs1 → WHT → signs2), 1 = inverse (signs2 → WHT → signs1)
ggml/src/ggml-common.h:347:// TQ3_1S: WHT-rotated 3-bit weight quantization (8-level Lloyd-Max for N(0,1))
ggml/src/ggml-common.h:359:// TQ4_1S: WHT-rotated 4-bit weight quantization (16-level Lloyd-Max for N(0,1))
ggml/src/ggml-cpu/ops.cpp:5031:    // For turbo types: communicate WHT group size to the quantize function via global
ggml/src/ggml-cpu/ops.cpp:10829:// WHT sign arrays (must match Metal shader turbo_wht_signs1/2)
ggml/src/ggml-cpu/ops.cpp:10873:        // InnerQ forward: apply scale_inv BEFORE signs+WHT (for Q pre-rotation)
ggml/src/ggml-cpu/ops.cpp:10883:        // WHT butterfly (log2(group_size) stages)
ggml/src/ggml-cpu/ops.cpp:10898:            // InnerQ inverse: apply scale_inv AFTER WHT+signs (for V un-rotation)
ggml/src/ggml-cuda/convert.cu:507:// WHT via __shfl_xor_sync — 16× less compute than the per-element generic template.
ggml/src/ggml-cuda/convert.cu:813:            return dequantize_tq4_1s_warp_cuda<half>;  // fast warp-cooperative WHT
ggml/src/ggml-cuda/convert.cu:878:            return dequantize_tq4_1s_warp_cuda<float>;  // fast warp-cooperative WHT
ggml/src/ggml-cuda/ggml-cuda.cu:717:// cuBLAS dequant-to-f16 requires per-element inverse WHT.
ggml/src/ggml-cuda/ggml-cuda.cu:2899:        // Fused TQ weight mul_mat with pre-rotated activations via warp shuffle WHT
ggml/src/ggml-cuda/ggml-cuda.cu:5658:                   op->src[0]->ne[0] % 32 == 0;  // supports 32, 64, and 128 WHT groups
ggml/src/ggml-cuda/mmvq-tq.cu:537:    // Step 1: TQ4_1S → fp16 via warp-cooperative dequant (WHT in-warp)
ggml/src/ggml-cuda/set-rows.cu:221:// ---- TurboQuant3 set_rows: GROUP_SIZE-element groups with WHT rotation + norm correction ----
ggml/src/ggml-cuda/set-rows.cu:231://   4. Forward WHT (log2(GROUP_SIZE) butterfly stages, shared memory)
ggml/src/ggml-cuda/set-rows.cu:326:    // ---- Step 4: Forward WHT (signs1 → butterfly → signs2, normalized) ----
ggml/src/ggml-cuda/set-rows.cu:413:// ---- TurboQuant3 tail kernel: straight 3-bit quantize without WHT rotation ----
ggml/src/ggml-cuda/set-rows.cu:416:// elements can't use the 128-element WHT. They are quantised directly into
ggml/src/ggml-cuda/set-rows.cu:490:    // ---- Normalize (no WHT!) ----
ggml/src/ggml-cuda/set-rows.cu:552:    // Read WHT group size from op_params (set by llama-kv-cache.cpp based on head_dim).
ggml/src/ggml-cuda/set-rows.cu:572:    // Launch 1: full groups with WHT rotation
ggml/src/ggml-cuda/set-rows.cu:590:    // Launch 2: tail elements (no WHT, straight quantize)
ggml/src/ggml-cuda/set-rows.cu:603:// ---- TurboQuant2 set_rows: GROUP_SIZE-element groups with WHT rotation + norm correction ----
ggml/src/ggml-cuda/set-rows.cu:694:    // ---- Step 4: Forward WHT ----
ggml/src/ggml-cuda/set-rows.cu:772:// ---- TurboQuant2 tail kernel: straight 2-bit quantize without WHT rotation ----
ggml/src/ggml-cuda/set-rows.cu:840:    // ---- Normalize (no WHT!) ----
ggml/src/ggml-cuda/set-rows.cu:944:// ---- TurboQuant4 set_rows: 128-element groups with WHT rotation + 4-bit quantization ----
ggml/src/ggml-cuda/set-rows.cu:946:// turbo4 block size IS the WHT group size (128), so 1 CUDA block = 1 turbo4 block.
ggml/src/ggml-cuda/set-rows.cu:1036:    // ---- Step 4: Forward WHT (signs1 → butterfly → signs2, normalized) ----
ggml/src/ggml-cuda/set-rows.cu:1118:    // turbo4 block size = WHT group size = 128, always
ggml/src/ggml-cuda/turbo-wht.cu:8:// direction: 0 = forward (signs1 → WHT → signs2), 1 = inverse (signs2 → WHT → signs1)
ggml/src/ggml-cuda/turbo-wht.cu:15://   2. Radix-2 Hadamard butterfly (log2(group_size) stages, in-place)
ggml/src/ggml-cuda/turbo-wht.cu:19:// Q/V equalization. For forward (Q rotation): multiply BEFORE signs+WHT.
ggml/src/ggml-cuda/turbo-wht.cu:20:// For inverse (V un-rotation): multiply AFTER WHT+signs.
ggml/src/ggml-cuda/turbo-wht.cu:48:    // InnerQ forward: apply scale_inv BEFORE signs+WHT (for Q pre-rotation)
ggml/src/ggml-cuda/turbo-wht.cu:65:    // WHT butterfly — log2(group_size) stages.
ggml/src/ggml-cuda/turbo-wht.cu:98:    // InnerQ inverse: apply scale_inv AFTER WHT+signs (for V un-rotation)
ggml/src/ggml-metal/ggml-metal-impl.h:469:// Pass 2 args (merge partials + inverse WHT + write output)
ggml/src/ggml-metal/ggml-metal-ops.cpp:3090:            // ---- Pass 2: Merge partials + inverse WHT + write output ----
ggml/src/ggml-metal/ggml-metal-ops.cpp:3123:                // Need at least DV threads for the WHT butterfly
ggml/src/ggml-metal/turbo-wht.h:1:// TurboQuant Fast Walsh-Hadamard rotation for Metal
ggml/src/ggml-metal/turbo-wht.h:17:// --- Fast Walsh-Hadamard Transform (in-place, normalized) ---
ggml/src/ggml-quants.c:5588:            // WHT-rotated / TurboQuant types: just validate scales are not NaN/Inf
ggml/src/ggml-quants.h:116:// TQ3_1S: WHT-rotated 3-bit weight quantization (8-level Lloyd-Max)
ggml/src/ggml-quants.h:121:// TQ4_1S: WHT-rotated 4-bit weight quantization (16-level Lloyd-Max)
ggml/src/ggml-turbo-quant.c:27:/* Global: WHT group size for CPU quantize path (set by CPU SET_ROWS handler) */
ggml/src/ggml-turbo-quant.c:202:/* ---------- WHT sign arrays (must match CUDA/Metal, seed=42) ---------- */
ggml/src/ggml-turbo-quant.c:218:/* ---------- CPU forward WHT (in-place, group_size elements) ---------- */
ggml/src/ggml-turbo-quant.c:243:/* ---------- CPU inverse WHT (in-place, group_size elements) ----------
ggml/src/ggml-turbo-quant.c:246: * H is the unnormalized Hadamard butterfly with H*H = group_size * I, so
ggml/src/ggml-turbo-quant.c:274:/* ---------- TURBO3_0: 3-bit PolarQuant with WHT rotation ---------- */
ggml/src/ggml-turbo-quant.c:279:    // Read WHT group size from global (set by CPU SET_ROWS handler before each call).
ggml/src/ggml-turbo-quant.c:309:        // 3. Forward WHT rotation
ggml/src/ggml-turbo-quant.c:404:        /* 3. Forward WHT rotation */
ggml/src/ggml-turbo-quant.c:486:        /* Step 2: Forward WHT rotation (matches CUDA set_rows) */
ggml/src/ggml-turbo-quant.c:595:        /* No inverse WHT, dequant stays in the rotated domain.
ggml/src/ggml-turbo-quant.c:596:        * Q is WHT-rotated by the graph, so <Q_rot, K_rot> gives correct attention scores.
ggml/src/ggml-turbo-quant.c:597:        * The inverse WHT is applied to the attention output via GGML_OP_TURBO_WHT (direction=1) in the graph. 
ggml/src/ggml-turbo-quant.c:664:/* TQ3_1S / TQ4_1S: WHT-rotated weight quantization                  */
ggml/src/ggml-turbo-quant.c:680:/* WHT sign pattern (golden ratio hash, 32-element blocks) — shared by TQ3 and TQ4 */
ggml/src/ggml-turbo-quant.c:691:/* Forward RHT: sign flips -> WHT butterfly -> normalize */
ggml/src/ggml-turbo-quant.c:706:/* Inverse RHT: WHT butterfly -> normalize + unsign */
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5220:    // TurboQuant WHT (forward / inverse rotation, 128-element block)
ggml/src/ggml-vulkan/vulkan-shaders/copy_from_quant.comp:35:    // TQ4_1S requires full inverse WHT after centroid*scale dequant.
ggml/src/ggml-vulkan/vulkan-shaders/copy_from_quant.comp:52:    // Inverse WHT butterfly (5 stages for 32 elements)
ggml/src/ggml-vulkan/vulkan-shaders/copy_to_quant.comp:417:    // Step 3: normalize, then apply forward WHT: signs1 -> butterfly -> signs2
ggml/src/ggml-vulkan/vulkan-shaders/copy_to_quant.comp:474:// 2-bit pack, no signs byte). WHT tables and reduction structure are
ggml/src/ggml-vulkan/vulkan-shaders/copy_to_quant.comp:585:// 4-bit nibble pack, no signs byte). WHT tables and reduction structure
ggml/src/ggml-vulkan/vulkan-shaders/copy_to_quant.comp:738:    // WHT butterfly via subgroupShuffleXor
ggml/src/ggml-vulkan/vulkan-shaders/dequant_tq4_1s.comp:22:    // WHT sign pattern for inverse RHT normalization
ggml/src/ggml-vulkan/vulkan-shaders/dequant_tq4_1s.comp:48:    // Inverse WHT butterfly (5 stages for 32 elements) — matches CPU reference
ggml/src/ggml-vulkan/vulkan-shaders/mul_mat_vec_tq4_1s.comp:17:// WHT sign pattern for 32-element blocks (shared by TQ3 and TQ4)
ggml/src/ggml-vulkan/vulkan-shaders/mul_mat_vec_tq4_1s.comp:28:// rationale.  Short version: pre-rotate the activation block via forward WHT
ggml/src/ggml-vulkan/vulkan-shaders/mul_mat_vec_tq4_1s.comp:62:        // --- Stage 2: forward WHT butterfly in shared memory (5 stages) ---
ggml/src/ggml-vulkan/vulkan-shaders/turbo_wht.comp:15:// Pre-scramble sign vectors applied before and after the WHT.
ggml/src/ggml-vulkan/vulkan-shaders/vulkan-shaders-gen.cpp:817:    // TurboQuant Walsh-Hadamard Transform op (Q forward + kqv inverse rotation)
scripts/autoresearch/track-kv/program.md:21:TurboQuant KV cache compresses K and V tensors using PolarQuant (WHT rotation +
scripts/autoresearch/track-kv/program.md:76:- **Cross-head WHT (AmesianX)**: For models with head_dim=64, apply WHT across
scripts/autoresearch/track-weight/program.md:16:The TQ4_1S format stores WHT-rotated 4-bit weights with non-linear Lloyd-Max centroids.
scripts/autoresearch/track-weight/program.md:21:Then inverse WHT (Walsh-Hadamard Transform) to recover original weight space.
scripts/autoresearch/track-weight/program.md:23:The fused mmvq kernel avoids per-block inverse WHT by pre-rotating the activation
scripts/autoresearch/track-weight/program.md:24:vector (WHT forward) once, then the inner loop is just:
scripts/autoresearch/track-weight/program.md:32:- Activation pre-rotated to float scratch buffer via warp shuffle WHT
scripts/autoresearch/track-weight/program.md:87:- **F32 vs fp16 activation precision**: AmesianX notes WHT amplifies q8_1
src/llama-graph.cpp:2104:        // TurboQuant: inverse WHT on FA output when V values are WHT-rotated.
src/llama-graph.cpp:2106:        // Group size must come from K (which determines the WHT rotation), not V.
src/llama-graph.cpp:2184:        // TurboQuant: inverse WHT on attention output (non-FA path)
src/llama-graph.cpp:2213:    // TurboQuant: graph-side inverse WHT on attention output (undoes V rotation)
src/llama-graph.cpp:2378:    // TurboQuant pre-rotate-queries: O(d log d) WHT rotation via custom op
src/llama-graph.cpp:2396:    // Extract original V head_dim after inverse WHT (applied inside build_attn_mha).
src/llama-graph.cpp:2525:    // extract original V head_dim after inverse WHT.
src/llama-graph.cpp:2721:    // TurboQuant: if V was padded, extract original V head_dim after inverse WHT
src/llama-kv-cache.cpp:20:// orthonormal Walsh-Hadamard rotation matrix
src/llama-kv-cache.cpp:22:static void ggml_gen_hadamard(ggml_tensor * tensor) {
src/llama-kv-cache.cpp:299:        // affect dot products since WHT preserves inner products:
src/llama-kv-cache.cpp:300:        //   <WHT(Q_padded), WHT(K_padded)> = <Q_padded, K_padded> = <Q, K> + <0, 0> = <Q, K>
src/llama-kv-cache.cpp:369:        // For turbo types, pad K head_dim to next multiple of 128 for full WHT groups
src/llama-kv-cache.cpp:568:        // always create Hadamard rotation tensors for DeepSeek V3.2 DSA lightning
src/llama-kv-cache.cpp:585:            attn_rot_hadamard[n] = std::vector<float>(n*n);
```

## polarquant

Captured matches: 20

```text
README.md:304:  WHT + PolarQuant). Selected at runtime via `-ctk` / `-ctv`.
ggml/include/ggml.h:432:        GGML_TYPE_TURBO2_0 = 42, // TurboQuant 2-bit KV cache: WHT + 2-bit PolarQuant
ggml/include/ggml.h:433:        GGML_TYPE_TURBO3_0 = 43, // TurboQuant 3-bit KV cache: WHT + 3-bit PolarQuant
ggml/include/ggml.h:434:        GGML_TYPE_TURBO4_0 = 44, // TurboQuant 4-bit KV cache: WHT + 4-bit PolarQuant
ggml/src/ggml-common.h:280:// TurboQuant 3-bit MSE-only: 3-bit PolarQuant indices (no QJL)
ggml/src/ggml-common.h:298:// TurboQuant 4-bit: 3-bit PolarQuant indices + 1-bit QJL signs
ggml/src/ggml-common.h:299:// TURBO4_USE_4BIT: switch between 4-bit PolarQuant (new) and 3-bit+QJL (legacy)
ggml/src/ggml-common.h:308:// 4-bit PolarQuant: 16 optimal centroids, nibble packed, no QJL
ggml/src/ggml-common.h:314:    uint8_t    qs[QK_TURBO4 / 2];      // 64 bytes: 4-bit PolarQuant indices (nibble packed)
ggml/src/ggml-common.h:318:// Legacy 3-bit PolarQuant + 1-bit QJL (original paper design)
ggml/src/ggml-common.h:324:    uint8_t    qs[QK_TURBO4 * 3 / 8];  // 48 bytes: 3-bit PolarQuant indices
ggml/src/ggml-common.h:332:// TurboQuant 2-bit: 2-bit PolarQuant indices only (no QJL)
ggml/src/ggml-turbo-quant.c:2: * TurboQuant: KV cache compression via PolarQuant + QJL
ggml/src/ggml-turbo-quant.c:274:/* ---------- TURBO3_0: 3-bit PolarQuant with WHT rotation ---------- */
ggml/src/ggml-turbo-quant.c:371:/* ---------- TURBO2_0: 2-bit PolarQuant (no QJL) ---------- */
ggml/src/ggml-turbo-quant.c:459:/* ---------- TURBO4_0: 3-bit PolarQuant + 1-bit QJL ---------- */
ggml/src/ggml-turbo-quant.c:543:        /* 4-bit PolarQuant: nibble pack into qs[64] */
ggml/src/ggml-turbo-quant.c:580:    /* 4-bit PolarQuant: nibble unpack → centroid → inverse rotate → scale */
scripts/autoresearch/track-kv/program.md:21:TurboQuant KV cache compresses K and V tensors using PolarQuant (WHT rotation +
tests/test-backend-ops.cpp:6542:// This validates the full quantization pipeline: f32 -> WHT -> PolarQuant -> turbo3
```

## qjl

Captured matches: 21

```text
ggml/src/ggml-common.h:280:// TurboQuant 3-bit MSE-only: 3-bit PolarQuant indices (no QJL)
ggml/src/ggml-common.h:298:// TurboQuant 4-bit: 3-bit PolarQuant indices + 1-bit QJL signs
ggml/src/ggml-common.h:299:// TURBO4_USE_4BIT: switch between 4-bit PolarQuant (new) and 3-bit+QJL (legacy)
ggml/src/ggml-common.h:308:// 4-bit PolarQuant: 16 optimal centroids, nibble packed, no QJL
ggml/src/ggml-common.h:318:// Legacy 3-bit PolarQuant + 1-bit QJL (original paper design)
ggml/src/ggml-common.h:319:// Per block: norm(fp16) + rnorm(fp16) + 3-bit indices (48 bytes) + 1-bit QJL signs (16 bytes)
ggml/src/ggml-common.h:323:    ggml_half  rnorm;                   //  2 bytes: residual norm for QJL scale
ggml/src/ggml-common.h:325:    uint8_t    signs[QK_TURBO4 / 8];   // 16 bytes: 1-bit QJL signs
ggml/src/ggml-common.h:332:// TurboQuant 2-bit: 2-bit PolarQuant indices only (no QJL)
ggml/src/ggml-metal/turbo-matrices.h:1:// Auto-generated TurboQuant rotation and QJL matrices
ggml/src/ggml-metal/turbo-matrices.h:2:// Generated from Python turboquant with seed=42 (rotation) and seed=1042 (QJL)
ggml/src/ggml-metal/turbo-wht.h:3:// Generated with seed=42 (rotation) and seed=1042 (QJL)
ggml/src/ggml-metal/turbo-wht.h:11:// --- QJL sign arrays ---
ggml/src/ggml-turbo-quant.c:2: * TurboQuant: KV cache compression via PolarQuant + QJL
ggml/src/ggml-turbo-quant.c:121:/* ---------- QJL projection matrix (lazy init, seed-based) ---------- */
ggml/src/ggml-turbo-quant.c:371:/* ---------- TURBO2_0: 2-bit PolarQuant (no QJL) ---------- */
ggml/src/ggml-turbo-quant.c:459:/* ---------- TURBO4_0: 3-bit PolarQuant + 1-bit QJL ---------- */
ggml/src/ggml-turbo-quant.c:532:        /* Step 5: QJL */
ggml/src/ggml-turbo-quant.c:550:        /* Legacy 3-bit + QJL: pack 3-bit indices + QJL signs */
ggml/src/ggml-turbo-quant.c:601:    /* Legacy 3-bit + QJL dequant */
ggml/src/ggml-vulkan/vulkan-shaders/copy_to_quant.comp:587:// rnorm field for ABI parity with the legacy 3-bit + QJL layout.
```

## weight_types

Captured matches: 200

```text
README.md:22:- **TurboQuant KV cache & weights: WHT-rotated low-bit quantization with backend-native kernels (Metal `TurboFlash`, CUDA, Vulkan, HIP). Use `-ctk turbo3 -ctv turbo3` for ~4.3× KV compression, or quantize weights to `TQ4_1S`/`TQ3_1S`. See [Compression below](#turboquant-kv-cache--weight-compression).**
README.md:305:- **Model weight compression** — `TQ3_1S` / `TQ4_1S` (3/4-bit, WHT-rotated
README.md:332:| `TQ3_1S` | 3 | 32 | 8-level Lloyd-Max + WHT rotation |
README.md:333:| `TQ4_1S` | 4 | 32 | 16-level Lloyd-Max + WHT rotation; fused Metal/Vulkan MUL_MAT_VEC kernels |
README.md:336:# Convert / re-quantize an F16/F32 GGUF to TQ4_1S.
README.md:337:llama-quantize model-f16.gguf model-tq4_1s.gguf TQ4_1S
README.md:340:`TQ4_1S` typically delivers ~25-35 % size reduction vs Q8_0 with single-digit-%
README.md:346:| Backend | KV `turbo2` / `turbo3` / `turbo4` | Weights `TQ3_1S` / `TQ4_1S` |
README.md:349:| CUDA (NVIDIA) | `turbo3` / `turbo4` (full); `turbo2` via reference path | `TQ4_1S` MUL_MAT_VEC |
README.md:350:| Vulkan | `turbo3` KV (FA + coopmat), `SET_ROWS` for `turbo2/4` | `TQ4_1S` (specialised MUL_MAT_VEC, SET_ROWS, CPY) |
README.md:369:`turbo2` / `turbo3` / `turbo4` KV and `TQ3_1S` / `TQ4_1S` weights work on every
README.md:871:    for weight quantization (`TQ4_1S` / `TQ3_1S`) and the per-backend support
TURBOQUANT_UPSTREAM_MERGE.md:21:  kernels, TQ4_1S/TQ3_1S native + fused mul_mat_vec, warp-cooperative dequant,
ggml/include/ggml.h:435:        GGML_TYPE_TQ3_1S  = 45, // TurboQuant 3-bit weight: WHT-rotated 8-level Lloyd-Max, block_size=32
ggml/include/ggml.h:436:        GGML_TYPE_TQ4_1S  = 46, // TurboQuant 4-bit weight: WHT-rotated 16-level Lloyd-Max, block_size=32
ggml/src/ggml-common.h:347:// TQ3_1S: WHT-rotated 3-bit weight quantization (8-level Lloyd-Max for N(0,1))
ggml/src/ggml-common.h:359:// TQ4_1S: WHT-rotated 4-bit weight quantization (16-level Lloyd-Max for N(0,1))
ggml/src/ggml-common.h:363:#define QK_TQ4_1S 32
ggml/src/ggml-common.h:367:    uint8_t   qs[QK_TQ4_1S / 2];      // 16 bytes: 4-bit indices nibble-packed
ggml/src/ggml-cpu/ggml-cpu.c:442:    [GGML_TYPE_TQ3_1S] = {
ggml/src/ggml-cpu/ggml-cpu.c:448:    [GGML_TYPE_TQ4_1S] = {
ggml/src/ggml-cpu/ggml-cpu.c:3512:// TQ3_1S vec_dot: dequantize tq3_1s block to f32, then dot with q8_0.
ggml/src/ggml-cpu/ggml-cpu.c:3522:    ggml_get_type_traits(GGML_TYPE_TQ3_1S)->to_float(vx, tmp, n);
ggml/src/ggml-cpu/ggml-cpu.c:3538:// TQ4_1S vec_dot: dequantize tq4_1s block to f32, then dot with q8_0.
ggml/src/ggml-cpu/ggml-cpu.c:3548:    ggml_get_type_traits(GGML_TYPE_TQ4_1S)->to_float(vx, tmp, n);
ggml/src/ggml-cpu/ops.cpp:686:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cpu/ops.cpp:687:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cpu/ops.cpp:1139:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cpu/ops.cpp:1140:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cpu/ops.cpp:1271:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cpu/ops.cpp:1272:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cpu/ops.cpp:4442:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cpu/ops.cpp:4443:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cpu/ops.cpp:4721:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cpu/ops.cpp:4722:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cpu/ops.cpp:4947:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cpu/ops.cpp:4948:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cpu/ops.cpp:5682:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cpu/ops.cpp:5683:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/convert.cu:506:// Fast warp-cooperative TQ4_1S dequant: one warp per 32-element block.
ggml/src/ggml-cuda/convert.cu:547:    dequantize_block_cuda<QK_TQ4_1S, QR_TQ4_1S, dequantize_tq4_1s, dst_t>(vx, y, ne00, ne01, ne02, ne03, s01, s02, s03, stream);
ggml/src/ggml-cuda/convert.cu:812:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/convert.cu:814:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cuda/convert.cu:815:            return dequantize_block_cont_cuda<QK_TQ3_0, QR_TQ3_1S, dequantize_tq3_1s>;
ggml/src/ggml-cuda/convert.cu:877:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/convert.cu:879:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cuda/convert.cu:880:            return dequantize_block_cont_cuda<QK_TQ3_0, QR_TQ3_1S, dequantize_tq3_1s>;
ggml/src/ggml-cuda/convert.cu:912:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/convert.cu:913:            return dequantize_block_cuda<QK_TQ4_1S, QR_TQ4_1S, dequantize_tq4_1s>;
ggml/src/ggml-cuda/convert.cu:914:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cuda/convert.cu:915:            return dequantize_block_cuda<QK_TQ3_0, QR_TQ3_1S, dequantize_tq3_1s>;
ggml/src/ggml-cuda/convert.cu:968:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/convert.cu:969:            return dequantize_block_cuda<QK_TQ4_1S, QR_TQ4_1S, dequantize_tq4_1s>;
ggml/src/ggml-cuda/convert.cu:970:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cuda/convert.cu:971:            return dequantize_block_cuda<QK_TQ3_0, QR_TQ3_1S, dequantize_tq3_1s>;
ggml/src/ggml-cuda/getrows.cu:224:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/getrows.cu:225:            // TurboQuant TQ4_1S: per-pair dequant (QR=1 -> 2 consecutive elements), mirrors convert.cu.
ggml/src/ggml-cuda/getrows.cu:226:            get_rows_cuda_q<QK_TQ4_1S, QR_TQ4_1S, dequantize_tq4_1s>(src0_d, src1_d, dst_d,
ggml/src/ggml-cuda/getrows.cu:229:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cuda/getrows.cu:230:            // TurboQuant TQ3_1S: per-pair dequant (QR=1 -> 2 consecutive elements), mirrors convert.cu.
ggml/src/ggml-cuda/getrows.cu:231:            get_rows_cuda_q<QK_TQ3_0, QR_TQ3_1S, dequantize_tq3_1s>(src0_d, src1_d, dst_d,
ggml/src/ggml-cuda/ggml-cuda.cu:715:// TQ4_1S load-time q8_0 conversion: ON by default for best prefill speed.
ggml/src/ggml-cuda/ggml-cuda.cu:716:// Native TQ4_1S decode is faster (+29-33%) but prefill is 2× slower because
ggml/src/ggml-cuda/ggml-cuda.cu:732:    // TQ4_1S → q8_0 load-time conversion (opt-in: GGML_TQ_CONVERT_Q8=1)
ggml/src/ggml-cuda/ggml-cuda.cu:733:    if (ggml_tq_convert_q8() && tensor->type == GGML_TYPE_TQ4_1S && offset == 0 && size == ggml_nbytes(tensor)) {
ggml/src/ggml-cuda/ggml-cuda.cu:736:        // Upload TQ4_1S to a temp GPU buffer
ggml/src/ggml-cuda/ggml-cuda.cu:741:        // Convert TQ4_1S (tmp) → q8_0 (tensor->data, which has q8_0-sized allocation)
ggml/src/ggml-cuda/ggml-cuda.cu:892:    // TQ4_1S → q8_0 load-time conversion: allocate q8_0-sized space if opted in
ggml/src/ggml-cuda/ggml-cuda.cu:893:    if (ggml_tq_convert_q8() && tensor->type == GGML_TYPE_TQ4_1S) {
ggml/src/ggml-cuda/ggml-cuda.cu:894:        // q8_0 block: 34 bytes per 32 elements. TQ4_1S block: 20 bytes per 32 elements.
ggml/src/ggml-cuda/ggml-cuda.cu:895:        const int64_t n_blocks = ggml_nelements(tensor) / QK_TQ4_1S;
ggml/src/ggml-cuda/ggml-cuda.cu:2778:    const bool is_tq_weight = (src0->type == GGML_TYPE_TQ4_1S || src0->type == GGML_TYPE_TQ3_1S);
ggml/src/ggml-cuda/ggml-cuda.cu:2823:    const bool is_tq_weight = (src0->type == GGML_TYPE_TQ4_1S || src0->type == GGML_TYPE_TQ3_1S);
ggml/src/ggml-cuda/ggml-cuda.cu:2902:    } else if (!split && is_tq_weight && src0->type == GGML_TYPE_TQ4_1S) {
ggml/src/ggml-cuda/ggml-cuda.cu:2903:        // Large prefill: runtime TQ4_1S → q8_0 scratch conversion + cuBLAS
ggml/src/ggml-cuda/ggml-cuda.cu:2926:    const bool is_tq_weight_id = (src0->type == GGML_TYPE_TQ4_1S || src0->type == GGML_TYPE_TQ3_1S);
ggml/src/ggml-cuda/ggml-cuda.cu:3566:        const bool is_tq_w = (node->src[0]->type == GGML_TYPE_TQ4_1S || node->src[0]->type == GGML_TYPE_TQ3_1S);
ggml/src/ggml-cuda/ggml-cuda.cu:5479:                    case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/ggml-cuda.cu:5480:                    case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cuda/ggml-cuda.cu:5501:                    case GGML_TYPE_TQ4_1S:
ggml/src/ggml-cuda/ggml-cuda.cu:5502:                    case GGML_TYPE_TQ3_1S:
ggml/src/ggml-cuda/mmvq-tq.cu:2: * Fused mul_mat for TQ4_1S / TQ3_1S weight types.
ggml/src/ggml-cuda/mmvq-tq.cu:5: * ne[1]>8: runtime TQ4_1S→q8_0 scratch + cuBLAS tensor core GEMM
ggml/src/ggml-cuda/mmvq-tq.cu:15:// Pre-rotate activation to q8_1 format (for TQ4_1S dp4a path)
ggml/src/ggml-cuda/mmvq-tq.cu:58:// TQ4_1S: dp4a path with fixed int8 centroid LUT + q8_1 activation
ggml/src/ggml-cuda/mmvq-tq.cu:103:// Pre-rotate activation to half (for TQ3_1S scalar path)
ggml/src/ggml-cuda/mmvq-tq.cu:137:// Multi-token TQ4_1S dp4a kernel (ncols_dst ≤ 8)
ggml/src/ggml-cuda/mmvq-tq.cu:155:    const int blocks_per_row = ncols_x / QK_TQ4_1S;
ggml/src/ggml-cuda/mmvq-tq.cu:210:// Multi-token TQ3_1S scalar kernel (ncols_dst ≤ 8)
ggml/src/ggml-cuda/mmvq-tq.cu:265:// TQ4_1S scalar/half kernel (AMD fallback — no dp4a)
ggml/src/ggml-cuda/mmvq-tq.cu:266:// Same pattern as TQ3_1S: pre-rotated half activations, scalar centroid lookup.
ggml/src/ggml-cuda/mmvq-tq.cu:290:    const int blocks_per_row = ncols_x / QK_TQ4_1S;
ggml/src/ggml-cuda/mmvq-tq.cu:302:            const float act = __half2float(vy_rot[j * stride_col_y + ib * QK_TQ4_1S + lane]);
ggml/src/ggml-cuda/mmvq-tq.cu:324:// AMD: uses scalar half path for TQ4_1S (dp4a regresses on RDNA4)
ggml/src/ggml-cuda/mmvq-tq.cu:364:    GGML_ASSERT(src0->type == GGML_TYPE_TQ4_1S || src0->type == GGML_TYPE_TQ3_1S);
ggml/src/ggml-cuda/mmvq-tq.cu:381:    const bool use_dp4a = !GGML_CUDA_CC_IS_AMD(cc) && src0->type == GGML_TYPE_TQ4_1S;
ggml/src/ggml-cuda/mmvq-tq.cu:384:        // NVIDIA TQ4_1S: dp4a int8 path (optimized for Turing+ dp4a throughput)
ggml/src/ggml-cuda/mmvq-tq.cu:411:        // Scalar half path: TQ3_1S (all vendors) + TQ4_1S on AMD (dp4a regresses on RDNA4)
ggml/src/ggml-cuda/mmvq-tq.cu:424:        const bool is_tq4 = (src0->type == GGML_TYPE_TQ4_1S);
ggml/src/ggml-cuda/mmvq-tq.cu:466:// Load-time conversion: TQ4_1S → q8_0 (opt-in via GGML_TQ_CONVERT_Q8=1)
ggml/src/ggml-cuda/mmvq-tq.cu:504:    GGML_ASSERT(n_elements % QK_TQ4_1S == 0);
ggml/src/ggml-cuda/mmvq-tq.cu:505:    const int n_blocks = n_elements / QK_TQ4_1S;
ggml/src/ggml-cuda/mmvq-tq.cu:514:// Large prefill: runtime TQ4_1S → q8_0 scratch + q8_0→fp16 dequant + cuBLAS
ggml/src/ggml-cuda/mmvq-tq.cu:522:    GGML_ASSERT(src0->type == GGML_TYPE_TQ4_1S);
ggml/src/ggml-cuda/mmvq-tq.cu:537:    // Step 1: TQ4_1S → fp16 via warp-cooperative dequant (WHT in-warp)
ggml/src/ggml-cuda/mmvq-tq.cu:540:        const to_fp16_cuda_t to_fp16 = ggml_get_to_fp16_cuda(GGML_TYPE_TQ4_1S);
ggml/src/ggml-metal/ggml-metal-device.cpp:833:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-metal/ggml-metal-device.cpp:835:                nsg = N_SG_TQ3_1S;
ggml/src/ggml-metal/ggml-metal-device.cpp:836:                nr0 = N_R0_TQ3_1S;
ggml/src/ggml-metal/ggml-metal-device.cpp:837:                smem = 32*sizeof(float)*N_R0_TQ3_1S;
ggml/src/ggml-metal/ggml-metal-device.cpp:839:        case GGML_TYPE_TQ4_1S:
ggml/src/ggml-metal/ggml-metal-device.cpp:841:                nsg = N_SG_TQ4_1S;
ggml/src/ggml-metal/ggml-metal-device.cpp:842:                nr0 = N_R0_TQ4_1S;
ggml/src/ggml-metal/ggml-metal-device.cpp:843:                smem = 32*sizeof(float)*N_R0_TQ4_1S;
ggml/src/ggml-metal/ggml-metal-device.cpp:963:// TQ3_1S / TQ4_1S rotated variant: uses dequantize_*_rotated (no inverse RHT)
ggml/src/ggml-metal/ggml-metal-device.cpp:1005:// TQ3_1S / TQ4_1S rotated MUL_MAT_ID variant
ggml/src/ggml-metal/ggml-metal-device.cpp:1034:// TQ3_1S / TQ4_1S activation pre-rotation pipeline (shared by both)
ggml/src/ggml-metal/ggml-metal-device.cpp:1162:        case GGML_TYPE_TQ3_1S:
ggml/src/ggml-metal/ggml-metal-device.cpp:1164:                nsg = N_SG_TQ3_1S;
ggml/src/ggml-metal/ggml-metal-device.cpp:1165:                nr0 = N_R0_TQ3_1S;
```

## cache_flags

Captured matches: 56

```text
MTP.md:91:- `--gpu-layers-draft / -ngld`, `-ctkd / -ctvd` — placement and KV typing for
NEXTN.md:231:- **TurboQuant3-friendly mask (V2)** — attention Q/K bumped to `Q6_K`. This is the piece we tuned specifically for this fork: when KV is compressed to 3-bit via `-ctk turbo3 -ctv turbo3`, the attention scores see extra dequant noise on K, so giving Q/K a little more headroom on the weight side cancels most of it out.
README.md:22:- **TurboQuant KV cache & weights: WHT-rotated low-bit quantization with backend-native kernels (Metal `TurboFlash`, CUDA, Vulkan, HIP). Use `-ctk turbo3 -ctv turbo3` for ~4.3× KV compression, or quantize weights to `TQ4_1S`/`TQ3_1S`. See [Compression below](#turboquant-kv-cache--weight-compression).**
README.md:195:- **Composes with TurboQuant3 KV** (`-ctk turbo3 -ctv turbo3`) — on MoE
README.md:213:- **TurboQuant3-friendly mask** — `attn_q` / `attn_k` bumped to `Q6_K` so the file pairs cleanly with `-ctk turbo3 -ctv turbo3`.
README.md:231:  -ctk turbo3 -ctv turbo3 -fa on \
README.md:242:  -c 8192 -ngl 99 -ngld 99 -ctk turbo3 -ctv turbo3 -fa on
README.md:304:  WHT + PolarQuant). Selected at runtime via `-ctk` / `-ctv`.
README.md:309:### KV cache types (`-ctk` / `-ctv`)
README.md:321:  -ctk turbo3 -ctv turbo3 -fa on
README.md:789:    Composes with TurboQuant3 KV (`-ctk turbo3 -ctv turbo3`) — on Qwen 3.6
README.md:812:      -ctk turbo3 -ctv turbo3 -fa on \
README.md:823:      -c 8192 -ngl 99 -ngld 99 -ctk turbo3 -ctv turbo3 -fa on
README.md:859:      -ngl 99 -ctk turbo3 -ctv turbo3 -fa on
README.md:865:    -ctk turbo2 -ctv turbo2   # 2-bit KV, ~6.4x vs F16 (highest compression)
README.md:866:    -ctk turbo3 -ctv turbo3   # 3-bit KV, ~4.3x  (default sweet spot)
README.md:867:    -ctk turbo4 -ctv turbo4   # 4-bit KV, ~3.8x  (highest accuracy / fallback)
common/arg.cpp:2055:        {"-ctk", "--cache-type-k"}, "TYPE",
common/arg.cpp:2068:        {"-ctv", "--cache-type-v"}, "TYPE",
common/arg.cpp:3551:        {"--spec-draft-type-k", "-ctkd", "--cache-type-k-draft"}, "TYPE",
common/arg.cpp:3564:        {"--spec-draft-type-v", "-ctvd", "--cache-type-v-draft"}, "TYPE",
docs/backend/snapdragon/developer.md:57:      -t 4 --ctx-size 8192 --batch-size 128 -ctk q8_0 -ctv q8_0 -fa on -ngl 99 --device HTP0,HTP1,HTP2,HTP3 -no-cnv -f surfing.txt
docs/function-calling.md:333:> Beware of extreme KV quantizations (e.g. `-ctk q4_0`), they can substantially degrade the model's tool calling performance.
docs/multi-gpu.md:44:| `-ctk` | `--cache-type-k` | `f32` \| `f16` \| `bf16` \| `q8_0` \| `q4_0` \| ... | `f16` | KV cache type for K. |
docs/multi-gpu.md:45:| `-ctv` | `--cache-type-v` | same as `-ctk` | `f16` | KV cache type for V. |
docs/multi-gpu.md:83:llama-cli -m model.gguf -sm tensor -ctk f16 -ctv f16
docs/multi-gpu.md:121:| Startup error *"simultaneous use of SPLIT_MODE_TENSOR and KV cache quantization not implemented"* | Use `-ctk f16 -ctv f16` (or `bf16`/`f32`) with `--split-mode tensor`. |
docs/rocm-mi300x-test-results.md:75:  -m model.gguf -ctk turbo3 -ctv turbo3 -ngl 99 -r 3 -p 512 -n 128
docs/speculative.md:35:- **`--gpu-layers-draft` / `-ngld`** and **`-ctkd` / `-ctvd`** still apply to how the **assistant tensors** are placed and typed when the assistant GGUF is loaded; the target uses `-ngl` and `-ctk`/`-ctv`.
docs/speculative.md:37:Example (paths illustrative). **TurboQuant** KV on the target: `-ctk`/`-ctv`. Assistant-side cache types follow the draft flags if you use them for offload/quant selection.
docs/speculative.md:47:  -ctk turbo3 -ctv turbo3 \
docs/speculative.md:48:  -ctkd turbo3 -ctvd turbo3 \
docs/speculative.md:174:Pair with `-ctk turbo3 -ctv turbo3` to compose with **TurboQuant** KV — on MoE targets (e.g. Qwen 3.6 35B-A3B) this combination is **+24-36% tps** over the `turbo3` baseline at single-slot in the matrix bench (see `NEXTN.md §7`).
docs/speculative.md:342:--spec-draft-type-k, -ctkd, --cache-type-k-draft  TYPE
docs/speculative.md:346:--spec-draft-type-v, -ctvd, --cache-type-v-draft  TYPE
ggml/src/ggml-turbo-quant.c:6: * GGML_TYPE_TURBO4_0 (4-bit) for use as --cache-type-k turboN in llama-server.
scripts/autoresearch/track-kv/program.md:16:- Benchmark: `llama-bench -ngl 99 -p 512 -n 128 -r 3 --cache-type-k turbo3 --cache-type-v turbo3`
scripts/autoresearch/track-kv/program.md:18:- Also test: `--cache-type-k turbo4 --cache-type-v turbo4` and `--cache-type-k turbo2 --cache-type-v turbo2`
tools/cli/README.md:53:| `-ctk, --cache-type-k TYPE` | KV cache data type for K<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_CACHE_TYPE_K) |
tools/cli/README.md:54:| `-ctv, --cache-type-v TYPE` | KV cache data type for V<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_CACHE_TYPE_V) |
tools/cli/README.md:97:| `--spec-draft-type-k, -ctkd, --cache-type-k-draft TYPE` | KV cache data type for K for the draft model<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_SPEC_DRAFT_CACHE_TYPE_K) |
tools/cli/README.md:98:| `--spec-draft-type-v, -ctvd, --cache-type-v-draft TYPE` | KV cache data type for V for the draft model<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_SPEC_DRAFT_CACHE_TYPE_V) |
tools/completion/README.md:136:| `-ctk, --cache-type-k TYPE` | KV cache data type for K<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_CACHE_TYPE_K) |
tools/completion/README.md:137:| `-ctv, --cache-type-v TYPE` | KV cache data type for V<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_CACHE_TYPE_V) |
tools/completion/README.md:180:| `--spec-draft-type-k, -ctkd, --cache-type-k-draft TYPE` | KV cache data type for K for the draft model<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_SPEC_DRAFT_CACHE_TYPE_K) |
tools/completion/README.md:181:| `--spec-draft-type-v, -ctvd, --cache-type-v-draft TYPE` | KV cache data type for V for the draft model<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_SPEC_DRAFT_CACHE_TYPE_V) |
tools/llama-bench/README.md:57:  -ctk, --cache-type-k <t>                  (default: f16)
tools/llama-bench/README.md:58:  -ctv, --cache-type-v <t>                  (default: f16)
tools/llama-bench/llama-bench.cpp:446:    printf("  -ctk, --cache-type-k <t>                    (default: %s)\n", join(transform_to_str(cmd_params_defaults.type_k, ggml_type_name), ",").c_str());
tools/llama-bench/llama-bench.cpp:447:    printf("  -ctv, --cache-type-v <t>                    (default: %s)\n", join(transform_to_str(cmd_params_defaults.type_v, ggml_type_name), ",").c_str());
tools/llama-bench/llama-bench.cpp:616:            } else if (arg == "-ctk" || arg == "--cache-type-k") {
tools/llama-bench/llama-bench.cpp:636:            } else if (arg == "-ctv" || arg == "--cache-type-v") {
tools/server/README.md:71:| `-ctk, --cache-type-k TYPE` | KV cache data type for K<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_CACHE_TYPE_K) |
tools/server/README.md:72:| `-ctv, --cache-type-v TYPE` | KV cache data type for V<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_CACHE_TYPE_V) |
tools/server/README.md:114:| `--spec-draft-type-k, -ctkd, --cache-type-k-draft TYPE` | KV cache data type for K for the draft model<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_SPEC_DRAFT_CACHE_TYPE_K) |
tools/server/README.md:115:| `--spec-draft-type-v, -ctvd, --cache-type-v-draft TYPE` | KV cache data type for V for the draft model<br/>allowed values: f32, f16, bf16, q8_0, q4_0, q4_1, iq4_nl, q5_0, q5_1<br/>(default: f16)<br/>(env: LLAMA_ARG_SPEC_DRAFT_CACHE_TYPE_V) |
```

## vulkan

Captured matches: 184

```text
NEXTN.md:100:- **Metal / Vulkan**: GDN partial rollback quality may still be upstream-limited; see PR #22400 notes in the project plan.
README.md:22:- **TurboQuant KV cache & weights: WHT-rotated low-bit quantization with backend-native kernels (Metal `TurboFlash`, CUDA, Vulkan, HIP). Use `-ctk turbo3 -ctv turbo3` for ~4.3× KV compression, or quantize weights to `TQ4_1S`/`TQ3_1S`. See [Compression below](#turboquant-kv-cache--weight-compression).**
README.md:72:- Vulkan and SYCL backend support
README.md:333:| `TQ4_1S` | 4 | 32 | 16-level Lloyd-Max + WHT rotation; fused Metal/Vulkan MUL_MAT_VEC kernels |
README.md:350:| Vulkan | `turbo3` KV (FA + coopmat), `SET_ROWS` for `turbo2/4` | `TQ4_1S` (specialised MUL_MAT_VEC, SET_ROWS, CPY) |
README.md:363:| Linux x64 | `llama-turboquant-linux-x64-vulkan.{zip,tar.gz}` | Vulkan + portable CPU |
README.md:365:| Windows x64 | `llama-turboquant-windows-x64-vulkan.zip` | Vulkan + portable CPU |
README.md:603:| [Vulkan](docs/build.md#vulkan) | GPU |
README.md:854:    CUDA / Vulkan / HIP).
TURBOQUANT_UPSTREAM_MERGE.md:35:- **Vulkan turbo3 KV cache:** dequant/get_rows/set_rows/cpy pipelines (the FA
TURBOQUANT_UPSTREAM_MERGE.md:68:## DEFERRED — Vulkan turbo3 flash-attention re-port (NOT lost)
TURBOQUANT_UPSTREAM_MERGE.md:70:Upstream evolved the Vulkan flash-attention stack further than the fork's last
TURBOQUANT_UPSTREAM_MERGE.md:71:sync. The fork's turbo3 Vulkan FA (`flash_attn_cm1.comp`,
TURBOQUANT_UPSTREAM_MERGE.md:72:`flash_attn_dequant.glsl`, `ggml-vulkan.cpp` dispatch) conflicted with upstream's
TURBOQUANT_UPSTREAM_MERGE.md:73:newer FA changes; per decision, upstream's FA was taken and the turbo3 Vulkan FA
TURBOQUANT_UPSTREAM_MERGE.md:74:must be re-ported and validated on the AMD RDNA4 box. The turbo3 KV-cache Vulkan
TURBOQUANT_UPSTREAM_MERGE.md:80:- `a494833d0` / `0198d5819` (Tuklus-Labs) Vulkan turbo3 KV + coopmat FA
TURBOQUANT_UPSTREAM_MERGE.md:82:Note: the Vulkan backend cannot be built on the M5 (no Vulkan); shader-gen still
TURBOQUANT_UPSTREAM_MERGE.md:83:emits turbo3 FA SPIR-V, so the Vulkan build will need reconciliation on the AMD
TURBOQUANT_UPSTREAM_MERGE.md:88:- [ ] AMD RDNA4 box: build Vulkan, reconcile shader-gen, re-port turbo3 FA,
docs/backend/VirtGPU.md:23:| Linux    | Under development | ggml-vulkan | not working | Working locally, CI running into deadlocks
docs/backend/VirtGPU.md:50:        BackendLib[GGML Backend library<br/>Metal / Vulkan / CPU / ...]
docs/backend/VirtGPU.md:130:- Target backend libraries (libggml-metal, libggml-vulkan, etc.)
docs/backend/VirtGPU.md:169:    patched. However, setting this flag breaks the Vulkan/Venus normal
docs/backend/VirtGPU/configuration.md:52:- **Warning**: Breaks normal Vulkan/Venus functionality
docs/backend/VirtGPU/configuration.md:83:- **Purpose**: Path to the actual GGML backend library (Metal, CUDA, Vulkan, etc.)
docs/backend/VirtGPU/configuration.md:93:  # macOS or Linux with Vulkan backend
docs/backend/VirtGPU/configuration.md:94:  export APIR_LLAMA_CPP_GGML_LIBRARY_PATH="/opt/llama.cpp/lib/libggml-vulkan.so"
docs/backend/VirtGPU/configuration.md:113:  # Vulkan backend
docs/build.md:23:* [Vulkan](#vulkan)
docs/build.md:405:## Vulkan
docs/build.md:412:Download and install the [`Vulkan SDK`](https://vulkan.lunarg.com/sdk/home#windows) with the default settings.
docs/build.md:414:Launch `w64devkit.exe` and run the following commands to copy Vulkan dependencies:
docs/build.md:417:cp /VulkanSDK/$SDK_VERSION/Bin/glslc.exe $W64DEVKIT_HOME/bin/
docs/build.md:418:cp /VulkanSDK/$SDK_VERSION/Lib/vulkan-1.lib $W64DEVKIT_HOME/x86_64-w64-mingw32/lib/
docs/build.md:419:cp -r /VulkanSDK/$SDK_VERSION/Include/* $W64DEVKIT_HOME/x86_64-w64-mingw32/include/
docs/build.md:421:Name: Vulkan-Loader
docs/build.md:422:Description: Vulkan Loader
docs/build.md:431:cmake -B build -DGGML_VULKAN=ON
docs/build.md:443:Download and install the [`Vulkan SDK`](https://vulkan.lunarg.com/sdk/home#windows) with the default settings.
docs/build.md:448:cmake -B build -DGGML_VULKAN=ON
docs/build.md:452:Now you can load the model in conversation mode using `Vulkan`
docs/build.md:472:cmake -B build -DGGML_VULKAN=ON
docs/build.md:478:You don't need to install the Vulkan SDK. It will be installed inside the container.
docs/build.md:490:#### Using the LunarG Vulkan SDK
docs/build.md:492:First, follow the official LunarG instructions for the installation and setup of the Vulkan SDK in the [Getting Started with the Linux Tarball Vulkan SDK](https://vulkan.lunarg.com/doc/sdk/latest/linux/getting_started.html) guide.
docs/build.md:495:> After completing the first step, ensure that you have used the `source` command on the `setup_env.sh` file inside of the Vulkan SDK in your current terminal session. Otherwise, the build won't work. Additionally, if you close out of your terminal, you must perform this step again if you intend to perform a build. However, there are ways to make this persistent. Refer to the Vulkan SDK guide linked in the first step for more information about any of this.
docs/build.md:504:SPIRV-Headers (`spirv/unified1/spirv.hpp`) are required for the Vulkan backend and are **not** always pulled in by the Vulkan loader dev package alone. Other distros use names such as `spirv-headers` (Ubuntu / Debian / Arch), or `spirv-headers-devel` (Fedora / openSUSE). On Windows, the LunarG Vulkan SDK’s `Include` directory already contains these headers.
docs/build.md:515:cmake -B build -DGGML_VULKAN=1
docs/build.md:531:Generally, follow LunarG's [Getting Started with the MacOS Vulkan SDK](https://vulkan.lunarg.com/doc/sdk/latest/mac/getting_started.html) guide for installation and setup of the Vulkan SDK. There are two options of Vulkan drivers on macOS, both of which implement translation layers to map Vulkan to Metal. They can be hot-swapped by setting the `VK_ICD_FILENAMES` environment variable to point to the respective ICD JSON file.
docs/build.md:533:Check the box for "KosmicKrisp" during the LunarG Vulkan SDK installation.
docs/build.md:535:Set environment variable for the LunarG Vulkan SDK after installation (and optionally add to your shell profile for persistence):
docs/build.md:542:MoltenVK is the default Vulkan driver installed with the LunarG Vulkan SDK on macOS, so you can use the above environment variable settings as is.
docs/build.md:556:cmake -B build -DGGML_VULKAN=1 -DGGML_METAL=OFF
docs/build.md:772:In most cases, it is possible to build and use multiple backends at the same time. For example, you can build llama.cpp with both CUDA and Vulkan support by using the `-DGGML_CUDA=ON -DGGML_VULKAN=ON` options with CMake. At runtime, you can specify which backend devices to use with the `--device` option. To see a list of available devices, use the `--list-devices` option.
docs/docker.md:31:- `ghcr.io/ggml-org/llama.cpp:full-vulkan`: Same as `full` but compiled with Vulkan support. (platforms: `linux/amd64`, `linux/arm64`)
docs/docker.md:32:- `ghcr.io/ggml-org/llama.cpp:light-vulkan`: Same as `light` but compiled with Vulkan support. (platforms: `linux/amd64`, `linux/arm64`)
docs/docker.md:33:- `ghcr.io/ggml-org/llama.cpp:server-vulkan`: Same as `server` but compiled with Vulkan support. (platforms: `linux/amd64`, `linux/arm64`)
docs/ops.md:15:| Operation | BLAS | CANN | CPU | CUDA | MTL | OpenCL | SYCL | Vulkan | WebGPU | ZenDNN | zDNN |
examples/simple-cmake-pkg/README.md:11:When hardware acceleration libraries are used (e.g. CUDA, Metal, Vulkan, etc.), the appropriate dependencies will be searched for automatically. So, for example, when finding a package
ggml/include/ggml-metal.h:6:// A similar interface can be created for other GPU backends (e.g. Vulkan, CUDA, etc.)
ggml/include/ggml-vulkan.h:10:#define GGML_VK_NAME "Vulkan"
ggml/src/ggml-backend-reg.cpp:46:#include "ggml-vulkan.h"
ggml/src/ggml-backend-reg.cpp:130:        GGML_LOG_DEBUG("Vulkan backend disabled by GGML_DISABLE_VULKAN environment variable\n");
ggml/src/ggml-opencl/ggml-opencl.cpp:5224:                // Match the Vulkan backend: only F32 -> F32, S_v in {16, 32, 64, 128}.
ggml/src/ggml-vulkan/ggml-vulkan.cpp:1:#include "ggml-vulkan.h"
ggml/src/ggml-vulkan/ggml-vulkan.cpp:3:#if defined(GGML_VULKAN_RUN_TESTS) || defined(GGML_VULKAN_CHECK_RESULTS)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:8:// See https://github.com/KhronosGroup/Vulkan-Hpp?tab=readme-ov-file#extensions--per-device-function-pointers-
ggml/src/ggml-vulkan/ggml-vulkan.cpp:25:// installed Vulkan headers predate the extension.
ggml/src/ggml-vulkan/ggml-vulkan.cpp:38:// LunarG Vulkan SDK on Windows typically provides <spirv-headers/spirv.hpp>.
ggml/src/ggml-vulkan/ggml-vulkan.cpp:96:#include "ggml-vulkan-shaders.hpp"
ggml/src/ggml-vulkan/ggml-vulkan.cpp:153:#ifdef GGML_VULKAN_DEBUG
ggml/src/ggml-vulkan/ggml-vulkan.cpp:157:#endif // GGML_VULKAN_DEBUG
ggml/src/ggml-vulkan/ggml-vulkan.cpp:1848:        std::cerr << "----------------\nVulkan Timings:" << std::endl;
ggml/src/ggml-vulkan/ggml-vulkan.cpp:2204:#ifdef GGML_VULKAN_CHECK_RESULTS
ggml/src/ggml-vulkan/ggml-vulkan.cpp:2457:#if defined(GGML_VULKAN_COOPMAT2_DECODE_VECTOR_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:3609:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:3960:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:3991:#if defined(VK_KHR_cooperative_matrix) && defined(GGML_VULKAN_COOPMAT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4010:#if defined(VK_KHR_shader_bfloat16) && defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4031:#if defined(VK_NV_cooperative_matrix2) && defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4045:#if defined(VK_KHR_shader_bfloat16) && defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4069:#if defined(VK_NV_cooperative_matrix2) && defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4087:#if defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4118:#if defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4148:#endif  // defined(VK_NV_cooperative_matrix2) && defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4149:#if defined(VK_KHR_cooperative_matrix) && defined(GGML_VULKAN_COOPMAT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4179:#if defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4240:#if defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4271:#endif  // defined(VK_KHR_cooperative_matrix) && defined(GGML_VULKAN_COOPMAT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4350:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4396:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4441:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4518:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4594:#if defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4711:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4734:#endif // GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4763:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4785:#endif // GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT
ggml/src/ggml-vulkan/ggml-vulkan.cpp:4788:#if !defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5300:#if defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5337:#if defined(VK_KHR_cooperative_matrix) && defined(GGML_VULKAN_COOPMAT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5429:#if defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5434:#if defined(VK_KHR_cooperative_matrix) && defined(GGML_VULKAN_COOPMAT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5550:#if defined(GGML_VULKAN_COOPMAT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5558:#if defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5566:#if defined(GGML_VULKAN_INTEGER_DOT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5571:#if defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5600:        vk::PhysicalDeviceVulkan11Properties vk11_props;
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5601:        vk::PhysicalDeviceVulkan12Properties vk12_props;
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5656:            // is available in the Vulkan SDK.
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5784:        VkPhysicalDeviceVulkan11Features vk11_features;
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5789:        VkPhysicalDeviceVulkan12Features vk12_features;
ggml/src/ggml-vulkan/ggml-vulkan.cpp:5958:#if defined(VK_NV_cooperative_matrix2) && defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:6027:#if defined(VK_KHR_shader_bfloat16) && defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:6067:#ifdef GGML_VULKAN_VALIDATE
ggml/src/ggml-vulkan/ggml-vulkan.cpp:6147:#if defined(VK_KHR_shader_bfloat16) && defined(GGML_VULKAN_BFLOAT16_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:6203:#ifndef GGML_VULKAN_RUN_TESTS
ggml/src/ggml-vulkan/ggml-vulkan.cpp:6364:#if defined(GGML_VULKAN_COOPMAT_GLSLC_SUPPORT)
ggml/src/ggml-vulkan/ggml-vulkan.cpp:6369:#if defined(GGML_VULKAN_COOPMAT2_GLSLC_SUPPORT)
```

## sycl

Captured matches: 500

```text
CMakeLists.txt:167:llama_option_depr(WARNING     LLAMA_SYCL                GGML_SYCL)
CMakeLists.txt:168:llama_option_depr(WARNING     LLAMA_SYCL_F16            GGML_SYCL_F16)
README.md:72:- Vulkan and SYCL backend support
README.md:597:| [SYCL](docs/backend/SYCL.md) | Intel and Nvidia GPU |
ci/README.md:18:# with SYCL support
ci/README.md:20:GG_BUILD_SYCL=1 bash ./ci/run.sh ./tmp/results ./tmp/mnt
docs/backend/OPENCL.md:21:The llama.cpp OpenCL backend is designed to enable llama.cpp on **Qualcomm Adreno GPU** firstly via OpenCL. Thanks to the portabilty of OpenCL, the OpenCL backend can also run on certain Intel GPUs such as those that do not have [SYCL](/docs/backend/SYCL.md) support although the performance is not optimal.
docs/backend/SYCL.md:1:# llama.cpp for SYCL
docs/backend/SYCL.md:20:**SYCL** is a high-level parallel programming model designed to improve developers productivity writing code across various hardware accelerators such as CPUs, GPUs, and FPGAs. It is a single-source language designed for heterogeneous computing and based on standard C++17.
docs/backend/SYCL.md:24:- **DPCPP** *(Data Parallel C++)*: The primary oneAPI SYCL implementation, which includes the icpx/icx Compilers.
docs/backend/SYCL.md:28:### Llama.cpp + SYCL
docs/backend/SYCL.md:30:The llama.cpp SYCL backend is primarily designed for **Intel GPUs**.
docs/backend/SYCL.md:31:SYCL cross-platform capabilities enable support for other vendor GPUs as well.
docs/backend/SYCL.md:47:The release packages for Ubuntu 24.04 x64 (FP32/FP16) only include the binary files of the llama.cpp SYCL backend. They require the target machine to have pre-installed Intel GPU drivers and oneAPI packages that are the same version as the build package. To get the version and installation info, refer to [.github/workflows/release.yml#L713](../../.github/workflows/release.yml#L713): ubuntu-24-sycl -> Download & Install oneAPI.
docs/backend/SYCL.md:97:  - Support to assign main GPU by **--main-gpu**, replace $GGML_SYCL_DEVICE.
docs/backend/SYCL.md:105:  - Create SYCL backend for Intel GPU.
docs/backend/SYCL.md:120:SYCL backend supports Intel GPU Family:
docs/backend/SYCL.md:156:To get the supported LLMs, GPUs, and performance reference, please check [Performance of llama.cpp on Intel GPU with SYCL backend](https://github.com/ggml-org/llama.cpp/discussions/23313).
docs/backend/SYCL.md:162:Please refer to [Docker with SYCL](../docker.md#docker-with-sycl) for details.
docs/backend/SYCL.md:204:SYCL backend depends on:
docs/backend/SYCL.md:222:Upon a successful installation, SYCL is enabled for the available Intel devices, along with relevant libraries such as oneAPI oneDNN for Intel GPUs.
docs/backend/SYCL.md:233:In order to check the available SYCL devices on the machine, please use the `sycl-ls` command.
docs/backend/SYCL.md:241:When targeting an intel GPU, the user should expect one or more devices among the available SYCL devices. Please make sure that at least one GPU is present via `sycl-ls`, for instance `[level_zero:gpu]` in the sample output below:
docs/backend/SYCL.md:266:cmake -B build -DGGML_SYCL=ON -DCMAKE_C_COMPILER=icx -DCMAKE_CXX_COMPILER=icpx
docs/backend/SYCL.md:269:cmake -B build -DGGML_SYCL=ON -DCMAKE_C_COMPILER=icx -DCMAKE_CXX_COMPILER=icpx -DGGML_SYCL_F16=ON
docs/backend/SYCL.md:276:instructions, which can be circumvented by setting the environment variable `SYCL_PROGRAM_COMPILE_OPTIONS`
docs/backend/SYCL.md:295:Similar to the native `sycl-ls`, available SYCL devices can be queried as follow:
docs/backend/SYCL.md:301:This command will only display the selected backend that is supported by SYCL. The default backend is level_zero. For example, in a system with 2 *Intel GPU* it would look like the following:
docs/backend/SYCL.md:303:found 2 SYCL devices:
docs/backend/SYCL.md:351:In two device selection modes, the default SYCL backend is level_zero, you can choose other backend supported by SYCL by setting environment variable ONEAPI_DEVICE_SELECTOR.
docs/backend/SYCL.md:377:detect 1 SYCL GPUs: [0] with top Max compute units:512
docs/backend/SYCL.md:381:use 1 SYCL GPUs: [0] with Max compute units:512
docs/backend/SYCL.md:396:Note, the package includes the SYCL running time and all depended dll files, no need to install oneAPI package and activte them.
docs/backend/SYCL.md:408:SYCL backend depends on:
docs/backend/SYCL.md:441:In the oneAPI command line, run the following to print the available SYCL devices:
docs/backend/SYCL.md:447:There should be one or more *level-zero* GPU devices displayed as **[ext_oneapi_level_zero:gpu]**. Below is example of such output detecting an *Intel Iris Xe* GPU as a Level-zero SYCL device:
docs/backend/SYCL.md:483:cmake -B build -G "Ninja" -DGGML_SYCL=ON -DCMAKE_C_COMPILER=cl -DCMAKE_CXX_COMPILER=icx  -DCMAKE_BUILD_TYPE=Release
docs/backend/SYCL.md:486:cmake -B build -G "Ninja" -DGGML_SYCL=ON -DCMAKE_C_COMPILER=cl -DCMAKE_CXX_COMPILER=icx  -DCMAKE_BUILD_TYPE=Release -DGGML_SYCL_F16=ON
docs/backend/SYCL.md:497:cmake -DGGML_SYCL_F16=ON --preset x64-windows-sycl-release
docs/backend/SYCL.md:516:You can use Visual Studio to open the `llama.cpp` folder directly as a CMake project. Before compiling, select one of the SYCL CMake presets:
docs/backend/SYCL.md:536:cmake -B build -G "Visual Studio 17 2022" -T "Intel C++ Compiler 2025" -A x64 -DGGML_SYCL=ON -DCMAKE_BUILD_TYPE=Release
docs/backend/SYCL.md:539:If you prefer to use the Intel C++ Compiler only for `ggml-sycl`, ensure that `ggml` and its backend libraries are built as shared libraries ( i.e. `-DBUILD_SHARED_LIBRARIES=ON`, this is default behaviour):
docs/backend/SYCL.md:542:cmake -B build -G "Visual Studio 17 2022" -A x64 -DGGML_SYCL=ON -DCMAKE_BUILD_TYPE=Release \
docs/backend/SYCL.md:543:      -DSYCL_INCLUDE_DIR="C:\Program Files (x86)\Intel\oneAPI\compiler\latest\include" \
docs/backend/SYCL.md:544:      -DSYCL_LIBRARY_DIR="C:\Program Files (x86)\Intel\oneAPI\compiler\latest\lib"
docs/backend/SYCL.md:554:2. Right-click on `ggml-sycl` and select **Properties**.
docs/backend/SYCL.md:558:4. In the right panel, find **Enable SYCL Offload** and set it to `Yes`.
docs/backend/SYCL.md:566:Properties -> C/C++ -> DPC++ -> Enable SYCL Offload (Yes)
docs/backend/SYCL.md:569:Now, you can build `llama.cpp` with the SYCL backend as a Visual Studio project.
docs/backend/SYCL.md:575:- You can avoid specifying `SYCL_INCLUDE_DIR` and `SYCL_LIBRARY_DIR` in the CMake command by setting the environment variables:
docs/backend/SYCL.md:577:    - `SYCL_INCLUDE_DIR_HINT`
docs/backend/SYCL.md:579:    - `SYCL_LIBRARY_DIR_HINT`
docs/backend/SYCL.md:600:Similar to the native `sycl-ls`, available SYCL devices can be queried as follow:
docs/backend/SYCL.md:606:This command will only display the selected backend that is supported by SYCL. The default backend is level_zero. For example, in a system with 2 *Intel GPU* it would look like the following:
docs/backend/SYCL.md:608:found 2 SYCL devices:
docs/backend/SYCL.md:652:In two device selection modes, the default SYCL backend is level_zero, you can choose other backend supported by SYCL by setting environment variable ONEAPI_DEVICE_SELECTOR.
docs/backend/SYCL.md:679:detect 1 SYCL GPUs: [0] with top Max compute units:512
docs/backend/SYCL.md:685:use 1 SYCL GPUs: [0] with Max compute units:512
docs/backend/SYCL.md:695:| GGML_SYCL          | ON (mandatory)                        | Enable build with SYCL code path.           |
docs/backend/SYCL.md:696:| GGML_SYCL_TARGET   | INTEL *(default)*                     | Set the SYCL target device type.            |
docs/backend/SYCL.md:697:| GGML_SYCL_DEVICE_ARCH | Optional                           | Set the SYCL device architecture. Setting the device architecture can improve the performance. See the table [--offload-arch](https://github.com/intel/llvm/blob/sycl/sycl/doc/design/OffloadDesign.md#--offload-arch) for a list of valid architectures. |
docs/backend/SYCL.md:698:| GGML_SYCL_F16      | OFF *(default)* \|ON *(optional)*     | Enable FP16 build with SYCL code path. (1.) |
docs/backend/SYCL.md:699:| GGML_SYCL_GRAPH    | ON *(default)* \|OFF *(Optional)*     | Enable build with [SYCL Graph extension](https://github.com/intel/llvm/blob/sycl/sycl/doc/extensions/experimental/sycl_ext_oneapi_graph.asciidoc). |
docs/backend/SYCL.md:700:| GGML_SYCL_DNN      | ON *(default)* \|OFF *(Optional)*     | Enable build with oneDNN.                   |
docs/backend/SYCL.md:701:| GGML_SYCL_HOST_MEM_FALLBACK | ON *(default)* \|OFF *(Optional)* | Allow host memory fallback when device memory is full during quantized weight reorder. Enables inference to continue at reduced speed (reading over PCIe) instead of failing. Requires Linux kernel 6.8+. |
docs/backend/SYCL.md:702:| GGML_SYCL_SUPPORT_LEVEL_ZERO | ON *(default)* \|OFF *(Optional)* | Enable Level Zero API for device memory allocation. Requires Level Zero headers/library at build time and Intel GPU driver (Level Zero runtime) at run time. Reduces system RAM usage during multi-GPU inference. |
docs/backend/SYCL.md:703:| CMAKE_C_COMPILER   | `icx` *(Linux)*, `icx/cl` *(Windows)* | Set `icx` compiler for SYCL code path.      |
docs/backend/SYCL.md:704:| CMAKE_CXX_COMPILER | `icpx` *(Linux)*, `icx` *(Windows)*   | Set `icpx/icx` compiler for SYCL code path. |
docs/backend/SYCL.md:706:1. FP32 or FP16 have different performance impact to LLM. Recommended to test them for better prompt processing performance on your models. You need to rebuild the code after change `GGML_SYCL_F16=OFF/ON`.
docs/backend/SYCL.md:712:| GGML_SYCL_DEBUG   | 0 (default) or 1 | Enable log function by macro: GGML_SYCL_DEBUG                                                                             |
docs/backend/SYCL.md:713:| GGML_SYCL_ENABLE_FLASH_ATTN | 1 (default) or 0| Enable Flash-Attention. It can reduce memory usage. The performance impact depends on the LLM.|
docs/backend/SYCL.md:714:| GGML_SYCL_DISABLE_OPT | 0 (default) or 1 | Disable optimize features for Intel GPUs. (Recommended to 1 for Intel devices older than Gen 10) |
docs/backend/SYCL.md:715:| GGML_SYCL_DISABLE_GRAPH | 0 or 1 (default) | Disable running computations through SYCL Graphs feature. Disabled by default because SYCL Graph is still on development, no better performance. |
docs/backend/SYCL.md:716:| GGML_SYCL_ENABLE_LEVEL_ZERO | 1 (default) or 0 | Use Level Zero API for device memory allocation instead of SYCL. Reduces system RAM usage on Intel dGPUs by avoiding DMA-buf/TTM host memory staging. Requires GGML_SYCL_SUPPORT_LEVEL_ZERO=ON at build time. |
docs/backend/SYCL.md:717:| GGML_SYCL_DISABLE_DNN | 0 (default) or 1 | Disable running computations through oneDNN and always use oneMKL. |
docs/backend/SYCL.md:718:| GGML_SYCL_ENABLE_VMM | 0 or 1 (default) | Enable the virtual-memory device pool. |
docs/backend/SYCL.md:720:| UR_L0_ENABLE_RELAXED_ALLOCATION_LIMITS | 0 (default) or 1 | Allow SYCL/Unified Runtime Level Zero device allocations larger than 4 GiB. llama.cpp's direct Level Zero allocation path requests the relaxed maximum-size limit itself when GGML_SYCL_ENABLE_LEVEL_ZERO=1. |
docs/backend/SYCL.md:728:| DEBUG_SYCL_POOL | Enable device memory pool logging on teardown. Useful for profiling allocations. |
docs/backend/SYCL.md:729:| DEBUG_SYCL_MALLOC | Enable verbose per-call logging of device pool alloc/free operations. |
docs/backend/SYCL.md:786:- Can I report Ollama issue on Intel GPU to llama.cpp SYCL backend?
docs/backend/SYCL.md:792:  It's same for other projects including llama.cpp SYCL backend.
docs/backend/SYCL.md:794:- `Native API failed. Native API returns: 39 (UR_RESULT_ERROR_OUT_OF_DEVICE_MEMORY)`, `ggml_backend_sycl_buffer_type_alloc_buffer: can't allocate 3503030272 Bytes of memory on device`, or `failed to allocate SYCL0 buffer`
docs/backend/SYCL.md:805:  With the default `GGML_SYCL_ENABLE_LEVEL_ZERO=1`, llama.cpp requests Level Zero's relaxed maximum-size allocation limit directly. If Level Zero support is disabled at build time or runtime and the allocation goes through SYCL/Unified Runtime instead, enable support for allocations larger than 4 GiB by:
docs/backend/SYCL.md:812:Please add the `[SYCL]` prefix/tag in issues/PRs titles to help the SYCL contributors to check/address them without delay.
docs/build.md:19:* [SYCL](#sycl)
docs/build.md:114:Building through oneAPI compilers will make avx_vnni instruction set available for intel processors that do not support avx512 and avx512_vnni. Please note that this build config **does not support Intel GPU**. For Intel GPU support, please refer to [llama.cpp for SYCL](./backend/SYCL.md).
docs/build.md:140:## SYCL
docs/build.md:142:SYCL is a higher-level programming model to improve programming productivity on various hardware accelerators.
docs/build.md:144:llama.cpp based on SYCL is used to **support Intel GPU** (Data Center Max series, Flex series, Arc series, Built-in GPU and iGPU).
docs/build.md:146:For detailed info, please refer to [llama.cpp for SYCL](./backend/SYCL.md).
docs/docker.md:28:- `ghcr.io/ggml-org/llama.cpp:full-intel`: Same as `full` but compiled with SYCL support. (platforms: `linux/amd64`)
docs/docker.md:29:- `ghcr.io/ggml-org/llama.cpp:light-intel`: Same as `light` but compiled with SYCL support. (platforms: `linux/amd64`)
docs/docker.md:30:- `ghcr.io/ggml-org/llama.cpp:server-intel`: Same as `server` but compiled with SYCL support. (platforms: `linux/amd64`)
docs/docker.md:144:## Docker With SYCL
docs/docker.md:154:You may want to pass in some different `ARGS`, depending on the SYCL environment supported by your container host, as well as the GPU architecture.
docs/docker.md:157:The resulting images, are essentially the same as the non-SYCL images:
docs/docker.md:165:After building locally, usage is similar to the non-SYCL examples, but you'll need to add the `--device` flag.
docs/docker.md:178:- You may need to install Intel GPU driver on the **host** machine *(Please refer to the [Linux configuration](./backend/SYCL.md#linux) for details)*.
docs/ops.md:15:| Operation | BLAS | CANN | CPU | CUDA | MTL | OpenCL | SYCL | Vulkan | WebGPU | ZenDNN | zDNN |
examples/sycl/README.md:3:This example program provides the tools for llama.cpp for SYCL on Intel GPU.
examples/sycl/README.md:9:|llama-ls-sycl-device| List all SYCL devices with ID, compute capability, max work group size, etc.|Support|
examples/sycl/README.md:13:List all SYCL devices with ID, compute capability, max work group size, etc.
examples/sycl/README.md:15:1. Build the llama.cpp for SYCL for the specified target *(using GGML_SYCL_TARGET)*.
examples/sycl/README.md:17:2. Enable oneAPI running environment *(if GGML_SYCL_TARGET is set to INTEL -default-)*
examples/sycl/README.md:32:found 2 SYCL devices:
examples/sycl/ls-sycl-device.cpp:8:#include "ggml-sycl.h"
ggml/include/ggml-sycl.h:12:#define GGML_SYCL_NAME "SYCL"
ggml/include/ggml-sycl.h:13:#define GGML_SYCL_MAX_DEVICES 48
ggml/include/ggml-sycl.h:41:// SYCL doesn't support registering host memory, keep here for reference
ggml/src/ggml-backend-reg.cpp:41:#ifdef GGML_USE_SYCL
ggml/src/ggml-backend-reg.cpp:42:#include "ggml-sycl.h"
ggml/src/ggml-backend-reg.cpp:122:#ifdef GGML_USE_SYCL
ggml/src/ggml-common.h:61:#elif defined(GGML_COMMON_DECL_SYCL)
ggml/src/ggml-common.h:92:#if defined(GGML_COMMON_DECL_CUDA) || defined(GGML_COMMON_DECL_HIP) || defined(GGML_COMMON_DECL_SYCL)
ggml/src/ggml-common.h:578:#elif defined(GGML_COMMON_IMPL_SYCL)
ggml/src/ggml-sycl/add-id.hpp:1:#ifndef GGML_SYCL_ADD_ID_HPP
ggml/src/ggml-sycl/add-id.hpp:2:#define GGML_SYCL_ADD_ID_HPP
ggml/src/ggml-sycl/add-id.hpp:8:#endif // GGML_SYCL_ADD_ID_HPP
ggml/src/ggml-sycl/backend.hpp:13:#ifndef GGML_SYCL_BACKEND_HPP
ggml/src/ggml-sycl/backend.hpp:14:#define GGML_SYCL_BACKEND_HPP
```

## flash_attention

Captured matches: 500

```text
README.md:22:- **TurboQuant KV cache & weights: WHT-rotated low-bit quantization with backend-native kernels (Metal `TurboFlash`, CUDA, Vulkan, HIP). Use `-ctk turbo3 -ctv turbo3` for ~4.3× KV compression, or quantize weights to `TQ4_1S`/`TQ3_1S`. See [Compression below](#turboquant-kv-cache--weight-compression).**
README.md:314:| `turbo3` | 3 | ~4.3× | **recommended default**; Metal `TurboFlash` decode kernel |
README.md:317:Typical invocation with full GPU offload + Flash-Attention:
README.md:348:| Metal (Apple Silicon) | yes; `TurboFlash` flash-attn decode kernel for `turbo3` (off-by-default on Apple10 — see PR #91) | yes (V2.1 fused kernels) |
README.md:362:| macOS arm64 | `llama-turboquant-macos-arm64.{zip,tar.gz}` | Metal (`TurboFlash`) + CPU |
README.md:370:backend per the table above; the Metal-only `TurboFlash` flash-attn decode
README.md:851:    Flash-Attention enabled — to cut KV memory traffic and footprint at
README.md:853:    accelerated by `TurboFlash` on Metal and dedicated kernels on
TURBOQUANT_UPSTREAM_MERGE.md:71:sync. The fork's turbo3 Vulkan FA (`flash_attn_cm1.comp`,
TURBOQUANT_UPSTREAM_MERGE.md:72:`flash_attn_dequant.glsl`, `ggml-vulkan.cpp` dispatch) conflicted with upstream's
benches/dgx-spark/dgx-spark.md:32:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 20, n_threads_batch = 20
benches/dgx-spark/dgx-spark.md:80:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 20, n_threads_batch = 20
benches/dgx-spark/dgx-spark.md:128:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 20, n_threads_batch = 20
benches/dgx-spark/dgx-spark.md:176:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 20, n_threads_batch = 20
benches/dgx-spark/dgx-spark.md:224:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 20, n_threads_batch = 20
benches/dgx-spark/dgx-spark.md:272:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 20, n_threads_batch = 20
benches/mac-m2-ultra/mac-m2-ultra.md:19:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 16, n_threads_batch = 16
benches/mac-m2-ultra/mac-m2-ultra.md:67:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 16, n_threads_batch = 16
benches/mac-m2-ultra/mac-m2-ultra.md:115:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 16, n_threads_batch = 16
benches/mac-m2-ultra/mac-m2-ultra.md:163:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 16, n_threads_batch = 16
benches/mac-m2-ultra/mac-m2-ultra.md:211:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 16, n_threads_batch = 16
benches/mac-m2-ultra/mac-m2-ultra.md:259:main: n_kv_max = 270336, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = -1, n_threads = 16, n_threads_batch = 16
benches/nemotron/nemotron-dgx-spark.md:33:main: n_kv_max = 303104, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = 99, n_threads = 20, n_threads_batch = 20
benches/nemotron/nemotron-dgx-spark.md:79:main: n_kv_max = 303104, n_batch = 2048, n_ubatch = 2048, flash_attn = 1, is_pp_shared = 0, is_tg_separate = 0, n_gpu_layers = 99, n_threads = 20, n_threads_batch = 20
common/arg.cpp:1386:    add_opt(common_arg({ "-fa", "--flash-attn" }, "[on|off|auto]",
common/arg.cpp:1387:                       string_format("set Flash Attention use ('on', 'off', or 'auto', default: '%s')",
common/arg.cpp:1388:                                     llama_flash_attn_type_name(params.flash_attn_type)),
common/arg.cpp:1391:                               params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
common/arg.cpp:1393:                               params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
common/arg.cpp:1395:                               params.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
common/arg.cpp:1398:                                   string_format("error: unknown value for --flash-attn: '%s'\n", value.c_str()));
common/common.cpp:1587:    cparams.flash_attn_type   = params.flash_attn_type;
common/common.h:473:    enum llama_flash_attn_type   flash_attn_type   = LLAMA_FLASH_ATTN_TYPE_AUTO; // whether to use Flash Attention
docs/backend/CANN.md:309:### Basic Flash Attention Support
docs/backend/SYCL.md:61:  - Support Flash-Attention: less memory usage, performance impact depends on LLM.
docs/backend/SYCL.md:713:| GGML_SYCL_ENABLE_FLASH_ATTN | 1 (default) or 0| Enable Flash-Attention. It can reduce memory usage. The performance impact depends on the LLM.|
docs/backend/snapdragon/README.md:279:      `GGML_HEXAGON_OPFILTER="FLASH_ATTN_EXT" llama-completion ...` - Disable Flash Attention on Hexagon (falls back to CPU or GPU)
docs/multi-gpu.md:43:| `-fa` | `--flash-attn` | `on` \| `off` \| `auto` | `auto` | Required when using `--split-mode tensor` and/or quantized V cache. Supported (and therefore enabled by default) for most combinations of models and backends. |
docs/multi-gpu.md:86:- `--flash-attn off` or (`--flash-attn auto` resolving to `off` when it isn't supported) is a hard error.
docs/multi-gpu.md:120:| Startup error *"SPLIT_MODE_TENSOR requires flash_attn to be enabled"* | Add `-fa on` or remove `-fa off`. |
examples/diffusion/diffusion-cli.cpp:141:    ctx_params.flash_attn_type      = params.flash_attn_type;
ggml/include/ggml.h:2415:    GGML_API struct ggml_tensor * ggml_flash_attn_ext(
ggml/include/ggml.h:2425:    GGML_API void ggml_flash_attn_ext_set_prec(
ggml/include/ggml.h:2429:    GGML_API enum ggml_prec ggml_flash_attn_ext_get_prec(
ggml/include/ggml.h:2432:    GGML_API void ggml_flash_attn_ext_add_sinks(
ggml/include/ggml.h:2436:    // TODO: needs to be adapted to ggml_flash_attn_ext
ggml/include/ggml.h:2437:    GGML_API struct ggml_tensor * ggml_flash_attn_back(
ggml/src/ggml-backend-meta.cpp:747:    auto handle_flash_attn_ext = [&](const std::vector<ggml_backend_meta_split_state> & src_ss) -> ggml_backend_meta_split_state {
ggml/src/ggml-backend-meta.cpp:965:                split_state = handle_flash_attn_ext(src_ss);
ggml/src/ggml-cann/aclnn_ops.cpp:3858:void ggml_cann_flash_attn_ext(ggml_backend_cann_context & ctx, ggml_tensor * dst) {
ggml/src/ggml-cann/aclnn_ops.h:874: * @brief   Performs the Flash Attention extended operator using the CANN backend.
ggml/src/ggml-cann/aclnn_ops.h:876: * @details This function implements the memory-efficient Flash Attention algorithm
ggml/src/ggml-cann/aclnn_ops.h:886:void ggml_cann_flash_attn_ext(ggml_backend_cann_context & ctx, ggml_tensor * dst);
ggml/src/ggml-cann/ggml-cann.cpp:1999:            ggml_cann_flash_attn_ext(ctx, dst);
ggml/src/ggml-cpu/ggml-cpu.c:2041:                ggml_compute_forward_flash_attn_ext(params, tensor);
ggml/src/ggml-cpu/ggml-cpu.c:2048:                ggml_compute_forward_flash_attn_back(params, masked, tensor);
ggml/src/ggml-cpu/ggml-cpu.c:2984:                        const int64_t mxDn = MAX(D, ne11) * 2; // *2 because of S and SM in ggml_compute_forward_flash_attn_back
ggml/src/ggml-cpu/ops.cpp:8348:static void ggml_compute_forward_flash_attn_ext_f16_one_chunk(
ggml/src/ggml-cpu/ops.cpp:8586:static void ggml_compute_forward_flash_attn_ext_tiled(
ggml/src/ggml-cpu/ops.cpp:8876:static void ggml_flash_attn_ext_reduce_partials(
ggml/src/ggml-cpu/ops.cpp:8946:static void ggml_compute_forward_flash_attn_ext_f16(
ggml/src/ggml-cpu/ops.cpp:9012:                ggml_compute_forward_flash_attn_ext_f16_one_chunk(
ggml/src/ggml-cpu/ops.cpp:9025:        ggml_flash_attn_ext_reduce_partials(params, dst, nth, chunk_size);
ggml/src/ggml-cpu/ops.cpp:9072:                ggml_compute_forward_flash_attn_ext_tiled(params, dst, ir0, ir1);
ggml/src/ggml-cpu/ops.cpp:9074:                ggml_compute_forward_flash_attn_ext_f16_one_chunk(params, dst, ir0, ir1, 0, nek1, nullptr, 0);
ggml/src/ggml-cpu/ops.cpp:9082:void ggml_compute_forward_flash_attn_ext(
ggml/src/ggml-cpu/ops.cpp:9090:                ggml_compute_forward_flash_attn_ext_f16(params, dst);
ggml/src/ggml-cpu/ops.cpp:9099:// ggml_compute_forward_flash_attn_back
ggml/src/ggml-cpu/ops.cpp:9101:static void ggml_compute_forward_flash_attn_back_f32(
ggml/src/ggml-cpu/ops.cpp:9416:void ggml_compute_forward_flash_attn_back(
ggml/src/ggml-cpu/ops.cpp:9426:                ggml_compute_forward_flash_attn_back_f32(params, masked, dst);
ggml/src/ggml-cpu/ops.h:90:void ggml_compute_forward_flash_attn_ext(const struct ggml_compute_params * params, struct ggml_tensor * dst);
ggml/src/ggml-cpu/ops.h:91:void ggml_compute_forward_flash_attn_back(
ggml/src/ggml-cpu/spacemit/ime.cpp:1036:                forward_flash_attn_ext_f16(params, op);
ggml/src/ggml-cpu/spacemit/ime.cpp:1149:    void forward_flash_attn_ext_f16(const ggml_compute_params * params, ggml_tensor * dst) {
ggml/src/ggml-cpu/spacemit/ime.cpp:1172:            ggml_compute_forward_flash_attn_ext(params, dst);
ggml/src/ggml-cpu/spacemit/ime.cpp:1215:                spacemit_kernels::rvv::forward_flash_attn_ext_f16_tiled_vlen1024_vf16(
ggml/src/ggml-cpu/spacemit/ime.cpp:1219:                spacemit_kernels::rvv::forward_flash_attn_ext_f16_one_chunk_vlen1024_vf16(
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:37:static inline bool flash_attn_ext_supported_d_vlen1024_vf16(int64_t d) {
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:41:static inline bool flash_attn_ext_supported_shape_vlen1024_vf16(int64_t DK, int64_t DV) {
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:42:    return flash_attn_ext_supported_d_vlen1024_vf16(DK) && flash_attn_ext_supported_d_vlen1024_vf16(DV);
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:651:static void flash_attn_ext_f16_one_chunk_inner_vlen1024_vf16_mrow(float **            pq,
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:667:    GGML_ASSERT(flash_attn_ext_supported_shape_vlen1024_vf16(DK, DV));
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:913:static void flash_attn_ext_f16_one_chunk_inner_vlen1024_vf16_m1(const float *       pq,
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:927:    GGML_ASSERT(flash_attn_ext_supported_shape_vlen1024_vf16(DK, DV));
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:1121:void forward_flash_attn_ext_f16_one_chunk_vlen1024_vf16(const ggml_compute_params * params,
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:1146:    GGML_ASSERT(flash_attn_ext_supported_shape_vlen1024_vf16(DK, DV));
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:1262:            flash_attn_ext_f16_one_chunk_inner_vlen1024_vf16_mrow<4>(  //
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:1283:            flash_attn_ext_f16_one_chunk_inner_vlen1024_vf16_mrow<2>(  //
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:1293:            flash_attn_ext_f16_one_chunk_inner_vlen1024_vf16_m1(                             //
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:1305:void forward_flash_attn_ext_f16_tiled_vlen1024_vf16(const ggml_compute_params * params,
ggml/src/ggml-cpu/spacemit/rvv_kernels.cpp:1330:    GGML_ASSERT(flash_attn_ext_supported_shape_vlen1024_vf16(DK, DV));
ggml/src/ggml-cpu/spacemit/rvv_kernels.h:47:void forward_flash_attn_ext_f16_one_chunk_vlen1024_vf16(const ggml_compute_params * params,
ggml/src/ggml-cpu/spacemit/rvv_kernels.h:54:void forward_flash_attn_ext_f16_tiled_vlen1024_vf16(const ggml_compute_params * params,
ggml/src/ggml-cuda/fattn-tile.cu:5:void ggml_cuda_flash_attn_ext_tile(ggml_backend_cuda_context & ctx, ggml_tensor * dst) {
ggml/src/ggml-cuda/fattn-tile.cu:11:            ggml_cuda_flash_attn_ext_tile_case< 40,  40>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:15:            ggml_cuda_flash_attn_ext_tile_case< 64,  64>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:19:            ggml_cuda_flash_attn_ext_tile_case< 72,  72>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:23:            ggml_cuda_flash_attn_ext_tile_case< 80,  80>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:27:            ggml_cuda_flash_attn_ext_tile_case< 96,  96>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:31:            ggml_cuda_flash_attn_ext_tile_case<112, 112>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:35:            ggml_cuda_flash_attn_ext_tile_case<128, 128>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:39:            ggml_cuda_flash_attn_ext_tile_case<192, 128>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:43:            ggml_cuda_flash_attn_ext_tile_case<256, 256>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:47:            ggml_cuda_flash_attn_ext_tile_case<320, 256>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:51:            ggml_cuda_flash_attn_ext_tile_case<512, 512>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:57:            ggml_cuda_flash_attn_ext_tile_case<576, 512>(ctx, dst);
ggml/src/ggml-cuda/fattn-tile.cu:61:            ggml_cuda_flash_attn_ext_tile_case<640, 512>(ctx, dst);
ggml/src/ggml-cuda/fattn-wmma-f16.cu:26:static __global__ void flash_attn_ext_f16(
ggml/src/ggml-cuda/fattn-wmma-f16.cu:543:void ggml_cuda_flash_attn_ext_wmma_f16_case(ggml_backend_cuda_context & ctx, ggml_tensor * dst) {
ggml/src/ggml-cuda/fattn-wmma-f16.cu:557:        fattn_kernel = flash_attn_ext_f16<
ggml/src/ggml-cuda/fattn-wmma-f16.cu:561:        fattn_kernel = flash_attn_ext_f16<
ggml/src/ggml-cuda/fattn-wmma-f16.cu:567:void ggml_cuda_flash_attn_ext_wmma_f16(ggml_backend_cuda_context & ctx, ggml_tensor * dst) {
ggml/src/ggml-cuda/fattn-wmma-f16.cu:571:    const enum ggml_prec prec = ggml_flash_attn_ext_get_prec(KQV);
ggml/src/ggml-cuda/fattn-wmma-f16.cu:579:                    ggml_cuda_flash_attn_ext_wmma_f16_case< 64, cols_per_block, float>(ctx, dst);
ggml/src/ggml-cuda/fattn-wmma-f16.cu:582:                    ggml_cuda_flash_attn_ext_wmma_f16_case< 80, cols_per_block, float>(ctx, dst);
ggml/src/ggml-cuda/fattn-wmma-f16.cu:585:                    ggml_cuda_flash_attn_ext_wmma_f16_case< 96, cols_per_block, float>(ctx, dst);
ggml/src/ggml-cuda/fattn-wmma-f16.cu:588:                    ggml_cuda_flash_attn_ext_wmma_f16_case<112, cols_per_block, float>(ctx, dst);
ggml/src/ggml-cuda/fattn-wmma-f16.cu:591:                    ggml_cuda_flash_attn_ext_wmma_f16_case<128, cols_per_block, float>(ctx, dst);
ggml/src/ggml-cuda/fattn-wmma-f16.cu:594:                    ggml_cuda_flash_attn_ext_wmma_f16_case<256, cols_per_block, float>(ctx, dst);
```
