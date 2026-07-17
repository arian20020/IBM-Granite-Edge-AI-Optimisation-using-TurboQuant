# Common repository and backend test sequence

> **Document status:** Controlled step-by-step test guide
> **Version:** 3.0
> **Last updated:** 14 July 2026

## Purpose

Every candidate repository and backend must go through the same sequence. This prevents a project from looking successful only because it used a different model, shorter prompt, different context length or more capable hardware.

## Phase 1: Freeze the test item

1. Record repository URL, branch, tag and commit SHA.
2. Save the remote list and working-tree status.
3. Record the licence.
4. Identify the upstream repository and upstream revision.
5. List the files changed by the fork.
6. Record compiler, CMake, Python and dependency versions.

## Phase 2: Understand the implementation before running it

1. Locate the quantisation data types and structures.
2. Locate the quantisation and dequantisation functions.
3. Locate the attention or KV-cache integration point.
4. Determine whether keys, values, weights or residuals are compressed.
5. Determine which bit widths are real implementations and which are only command-line labels.
6. Identify CPU, CUDA, Vulkan, SYCL, OpenVINO or other device-specific code.
7. Compare the code with the paper algorithm and record missing stages.

## Phase 3: Build a clean upstream baseline

1. Clone the pinned upstream revision into a separate folder.
2. Configure a Release x64 build.
3. Record all build options.
4. Build the required executables and libraries.
5. Run the upstream test suite.
6. Save build logs and test results.
7. Run a small diagnostic model.

A fork result is not meaningful until the upstream baseline works on the same machine.

## Phase 4: Build the candidate repository

1. Use a separate clean build directory.
2. Start with the repository's documented configuration.
3. Record every required workaround.
4. Do not silently disable failed tests.
5. Save standard output, standard error and exit codes.
6. Confirm architecture and actual device support.

## Phase 5: Run a diagnostic model

1. Use a small model to verify loading and token generation.
2. Verify the model SHA-256.
3. Use a fixed prompt and fixed seed where supported.
4. Confirm requested and actual execution device.
5. Record weight and KV formats.
6. Capture peak memory and timing output.

## Phase 6: Run the Granite baseline

1. Use the chosen Granite checkpoint without the candidate optimisation.
2. Keep prompt, context length, threads, batch settings and generation parameters fixed.
3. Run correctness prompts first.
4. Run performance prompts after correctness is established.
5. Repeat runs and report variation.

## Phase 7: Enable one optimisation at a time

Examples:

- change only the weight format;
- change only the K-cache format;
- change only the V-cache format;
- enable only the first TurboQuant stage;
- enable the QJL residual stage separately where possible;
- change only the execution device.

This isolates the reason for each result.

## Phase 8: Measure quality and performance

Record:

- model and tokenizer identity;
- weight precision;
- K and V cache precision;
- context length;
- peak RAM and device memory;
- KV-cache size;
- model load time;
- time to first token;
- prompt-processing speed;
- generation tokens per second;
- output quality score;
- crashes, warnings and fallback;
- repeat-to-repeat variation.

## Phase 9: Classify the result

- **Pass:** reproducible, correct, evidence complete and quality gate met.
- **Conditional pass:** works but has a documented limitation or narrow hardware/model requirement.
- **Fail:** does not build, load or produce an acceptable result.
- **Blocked:** cannot be tested because a required model, device, dependency or licence is unavailable.
- **Not implemented:** the claimed feature cannot be found in the code.

## Phase 10: Preserve evidence

Store:

```text
manifests/
commands/
build-logs/
test-logs/
metrics/
quality-outputs/
checksums/
decision-records/
```

The final report should be able to trace every table row back to these files.

## Original research checklist

- Can the repository be downloaded and cloned?
- Can it be built and run?
- Which models can it run?
- Can it run IBM Granite?
- Does it work on Intel CPU, GPU or NPU hardware?
- Does it quantise the KV cache?
- Is TurboQuant actually implemented?
- How much memory is saved?
- What happens to speed and quality?

## Sources used

- `SRC-BOOK-AI-ENGINEERING-2025`
- `SRC-BOOK-ENGINEERING-SOFTWARE-PRODUCTS`
- `SRC-BOOK-SYSTEMS-ENGINEERING`
- `SRC-LLAMACPP-GITHUB`
- `SRC-OV-BENCHMARK-2026`
