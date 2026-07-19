# Task 5 report: CPU SDPA compressed-cache integration

## Status

**BLOCKED — no production integration or `0003` patch was created.**

The Task 5 plan assumes that host-side `KVCacheManager` storage participates in CPU SDPA. At pinned OpenVINO GenAI revision `7dea0459b2ac7d8dfd877fd9df6737674fd8371d` (strict derived commit `122f50ebf9bcfff8835633b42ef1df0499ae834a`), those are different execution architectures:

- CPU SDPA is compiled as one stateful model. `pipeline_stateful.cpp` compiles the entire model and creates one `ov::InferRequest`; generation is delegated to `get_lm_encoded_results`. KV `ReadValue`/`Assign` state stays inside that request, so the host pipeline has no per-layer callback at which it can encode new K/V or decode the current attention slice.
- `KVCacheManager` discovers and binds only explicit `key_cache.*` / `value_cache.*` model inputs. Those inputs belong to the continuous-batching PagedAttention path, which Task 5 explicitly requires rejecting.
- `pipeline_static.cpp` is not a CPU external-cache path. Its only pipeline kind is `STATEFUL`, and it calls `compile_decoder_for_npu`; each token is again one whole-model `m_request.infer()`.

Consequently, adding `TurboQuantKVCache` beside `KVCacheManager` could only produce a standalone codec/owner fixture. It could not feed CPU SDPA without either:

1. modifying the OpenVINO graph before compilation to replace every KV `ReadValue`/`Assign` edge with explicit compressed-cache inputs/outputs and decode/encode operations, or
2. adding plugin/custom-op support for compressed KV state and SDPA decode.

Both options require files and interfaces outside Task 5's allowed set and substantially more design work. Decompressing the whole cache around each `InferRequest` would also allocate an equivalent full-precision cache and violate the no-shadow-cache and slice-only requirements.

## Exact evidence

- `src/cpp/src/llm/pipeline_stateful.cpp:51-83`: CPU path compiles the complete model and creates one infer request; no cache manager is constructed.
- `src/cpp/src/llm/pipeline_stateful.cpp:491-492`: generation hands that request directly to `get_lm_encoded_results`.
- `src/cpp/src/llm/pipeline_static.cpp:74-83`: the only static pipeline kind is `STATEFUL`.
- `src/cpp/src/llm/pipeline_static.cpp:102-113`: static construction uses `compile_decoder_for_npu` and creates one infer request.
- `src/cpp/src/llm/pipeline_static.cpp:299-303,337-359`: only token/mask/position tensors are set; each prefill/decode step invokes the whole compiled model.
- `src/cpp/src/continuous_batching/cache/kv_cache_manager.hpp:35-53`: manager eligibility requires explicit `key_cache.*` and `value_cache.*` inputs.
- `src/cpp/src/continuous_batching/cache/kv_cache_manager.hpp:159-251`: manager allocates full OpenVINO tensors and binds them to the request; it has no CPU-SDPA layer callback.
- `src/cpp/src/llm/pipeline.cpp:225-245`: `ContinuousBatchingAdapter` is the PagedAttention branch; ordinary CPU falls through to `StatefulLLMPipeline`.
- The prescribed `tests/cpp/unit/continuous_batching` directory does not exist.
- The prescribed `build-genai-turboquant/bin/Release/genai_unit_tests.exe` does not exist; this revision uses `tests_continuous_batching` plus the focused targets introduced by patches 0001/0002.

## TDD status

RED was not fabricated. A deterministic owner-only test would fail because a new class is missing, but it would not prove integration with CPU SDPA, attention execution, or removal of full-precision state. Since no testable production seam exists in the permitted files, proceeding to GREEN would create the mock integration the task explicitly forbids.

## History and artifacts

- Ordered `0001-tbq-codec.patch` and `0002-kv-config-telemetry.patch` are unchanged.
- Strict derived commit remains `122f50ebf9bcfff8835633b42ef1df0499ae834a`.
- Production object build evidence at controlling commit `4271e8a` remains valid and was not recharacterized as runtime activation.
- No `0003` patch exists, no derived production files were changed, and no QJL, Polar, GPU, or PagedAttention support is claimed.

## Required design decision to unblock

Expand scope to a graph-transformation/plugin integration. The smallest credible next plan is to externalize or replace stateful KV edges before compilation, define a CPU operation that decodes the exact SDPA-consumed slice, and test the transformed two-layer graph through a real CPU `InferRequest`. That design must specify supported model layouts, prefill policy, state lifetime, and how transient decoded memory is accounted before implementation resumes.
