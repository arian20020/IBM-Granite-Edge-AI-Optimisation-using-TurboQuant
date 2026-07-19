# Task 4 report: independent KV configuration and bounded telemetry

## Outcome

Implemented exact, case-sensitive `STANDARD`/`TBQ3`/`TBQ4` parsing for independent key and value selection, the norm-correction flag, exact removal of the three project properties before plugin compilation, and a bounded JSON activation record. The pipeline stores the requested configuration for later cache integration; this checkpoint does **not** claim TurboQuant runtime activation.

The ordered artifact is `experiments/patches/openvino-turboquant/0002-kv-config-telemetry.patch`; `0001-tbq-codec.patch` was not rewritten. The final strict-controller reproduction produced clean derived commit `122f50ebf9bcfff8835633b42ef1df0499ae834a` from base `7dea0459b2ac7d8dfd877fd9df6737674fd8371d`, applying `0001-tbq-codec.patch` then `0002-kv-config-telemetry.patch`.

## TDD and tests

Initial RED: the focused CMake target failed because `turboquant_config.cpp` and the public configuration types did not exist.

Review-fix RED command:

`turboquant_config_tests.exe --gtest_filter=TurboQuantConfig.RejectsUnknownTurboQuantPropertyWithoutMutation:TurboQuantConfig.RejectsWrongTypesWithoutPartialMutation:TurboQuantConfig.SerializerProducesParseableJsonAndRejectsInvalidUtf8`

Exact result: 0 passed, 3 failed. Unknown `TURBOQUANT_*` names were accepted, wrong types erased the earlier valid key before throwing, and invalid UTF-8 was serialized without rejection. A second RED compile for the integration tests failed because `extract_turboquant_before_boundary`, `inactive_activation_telemetry_json`, and `turboquant_build_commit` did not exist.

GREEN before export: the focused executable passed 10/10 tests. The Python schema suite passed 2/2 while invoking `turboquant_config_tests.exe --emit-activation-json`, parsing its actual serializer output with Python's JSON parser, and validating the exact schema.

Final reproduced GREEN: after committing the revised ordered patch and recreating the derived checkout through the strict controller, `openvino_genai_obj` passed in 690.4 seconds, the focused target rebuilt and passed 13/13, the connected Python schema suite passed 2/2, and patch-identity tests passed 10/10.

Coverage now includes all nine K/V combinations, case-sensitive values, unknown-prefix rejection, wrong `ov::Any` types, transactional no-partial-mutation behavior, exact removal, norm parsing, valid JSON escaping, invalid UTF-8 rejection, bounded fields/records, extraction before a model boundary, configuration lifetime, and inactive production telemetry identity.

Second-review UTF-8 RED: the three-test boundary filter ran one passing test and two failing tests. Four invalid scalar categories were still accepted (three-byte overlong, UTF-16 surrogate, four-byte overlong, and values above U+10FFFF), and a non-hexadecimal 40-character build commit was accepted. GREEN: the focused suite passed 13/13 after adding scalar-range checks and hexadecimal commit validation. Valid minimum/maximum two-, three-, and four-byte sequences are parsed by `nlohmann::json`; invalid leads, continuations, truncation, overlong encodings, surrogates, and out-of-range scalars are rejected.

Production integration compile command:

`cmake --build R:\external\official-openvino\2026-07-19\build-genai-turboquant --config Release --target openvino_genai_obj -- /m:4`

Result: exit 0 after 686.1 seconds. MSBuild compiled the real `turboquant_config.cpp`, `pipeline_base.cpp`, and the object-library pipeline sources, then produced `openvino_genai_obj.lib`. Existing upstream numeric-conversion warnings were emitted; there were no production integration compile errors.

The same target was rebuilt after strict reproduction and exited 0 after 690.4 seconds, proving the final derived checkout compiles the production integration.

## Pinned-revision adaptation

The pinned revision has no `tests/cpp/unit/continuous_batching` tree or `genai_unit_tests` target. The test is therefore `tests/cpp/turboquant_config.cpp`, remains in the normal `tests_continuous_batching` glob, and has a focused `turboquant_config_tests` target. `pipeline.cpp` and `utils.hpp` are additionally changed because this revision needs constructor-level extraction around the real `read_model` boundary.

Each properties-based production pipeline construction now emits exactly one JSON object after the implementation is created and its parsed configuration retained. Until Task 5 it explicitly records activated K/V as `STANDARD`, attention path `not_activated`, expected/actual bytes as zero, and fallback true only when TurboQuant was requested. The build embeds the exact derived Git commit at configure time. `model_hash` is a deterministic 64-bit FNV-1a content fingerprint: for an in-memory model it covers the exact XML string; for a path it covers sorted relative filenames and every file byte below that path. It is an identity fingerprint, not a cryptographic integrity claim.

## Concerns

CMake emits existing tokenizer-submodule Windows long-path diagnostics, third-party deprecation warnings, and upstream numeric-conversion warnings. With a realistic 20-minute allowance, `openvino_genai_obj` passed. Runtime cache allocation or TurboQuant activation is not claimed.
