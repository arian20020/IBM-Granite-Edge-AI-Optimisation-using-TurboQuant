# llama.cpp: detailed project guide

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Original research note:** The supplied `Local Chat apps/Llama.cpp.docx` file was blank. This guide therefore uses the project's recorded implementation decisions and the registered official llama.cpp source.

## Overview

llama.cpp is a C/C++ inference project for running many transformer language models locally. It is important to this project because it supports GGUF model files, command-line prompting, weight quantisation, KV-cache configuration and several hardware backends. It can be built as a native executable and called without requiring a cloud service.

The project should treat llama.cpp as one backend route, not as the entire application. The WinUI 3 front end should validate a model, prepare a request and call a stable backend layer. The backend layer can then execute llama.cpp and return generated text, metrics and understandable errors.

## Main concepts

### GGUF model files

GGUF is the model-file format most commonly used with llama.cpp. A GGUF file can contain model tensors, tokenizer information and metadata in one binary file. It may store full-precision or quantised weights. The file extension alone does not prove that a model is compatible; the architecture, metadata and llama.cpp revision still need to support it.

### Weight quantisation

Weight quantisation reduces the precision used to store the model's learned parameters. Examples such as Q8_0 and Q4_K_M describe different storage and reconstruction schemes. Lower-bit weights normally reduce model-file size and memory use, but may introduce more quality loss.

### KV-cache precision

The KV cache is separate from the model weights. A Q4 weight model can still use an F16 KV cache. Every test record must therefore state both the weight format and the K/V cache formats.

### Command-line interface

`llama-cli` can load a GGUF model and generate text directly in a terminal. This is useful for the project because a command-line route does not require a local web server or an open port. That is a better fit for restricted education and healthcare environments.

## Step-by-step Windows validation route

1. **Pin the repository revision.** Record the Git commit or tag before building.
2. **Build for x64 Release.** Confirm that the compiler and host tools are using x64 rather than x86.
3. **Run the repository tests.** Do not treat a successful compile as full validation.
4. **Record executable versions.** Capture `llama-cli --version` and the build configuration.
5. **Choose a small diagnostic model.** Use it to prove that the executable, tokenizer and prompt path work.
6. **Verify the model hash.** This prevents an incomplete or changed download being mistaken for a backend problem.
7. **Run a fixed prompt.** Capture standard output, standard error, command line and exit code.
8. **Check actual device assignment.** A requested GPU option may be ignored when the build lacks a usable GPU backend.
9. **Record weight and KV formats.** Keep these as separate fields.
10. **Measure the baseline.** Record load time, prompt processing, time to first token, tokens per second and peak memory.
11. **Move to Granite only after the diagnostic run works.** If Granite then fails, the failure is more likely to be model-format or architecture compatibility rather than the basic compiler toolchain.

## How this fits the WinUI application

```text
ModelImportPage
-> validate .gguf file
-> store the selected model path
-> backend service builds a safe llama-cli command
-> start the process without a shell
-> stream standard output and standard error
-> parse generated text and metrics
-> update the chat screen
-> keep the full command and logs in the evidence folder
```

The application should never concatenate untrusted text into a shell command. It should use a process API with a fixed executable path and separate argument values. It should also support cancellation and kill the child process cleanly when the user stops generation.

## What to test

- unsupported or corrupted GGUF files;
- missing tokenizer or metadata support;
- CPU-only build compared with GPU-enabled builds;
- long prompts and growing KV-cache memory;
- different weight quantisations with the same KV-cache precision;
- different KV-cache precisions with the same weights;
- cancellation while the model is loading and while it is generating;
- paths containing spaces and non-ASCII characters;
- output parsing when warnings are printed to standard error;
- model quality before and after optimisation.

## Limitations

llama.cpp changes quickly. Command-line options, supported model architectures and backend behaviour must be checked against the pinned revision used in the experiment. A fork that claims TurboQuant support must be reviewed and tested separately from upstream llama.cpp.

## Project decision

Use llama.cpp as the primary GGUF and command-line baseline. Keep it behind a backend interface so the application can also support the OpenVINO GenAI route. Do not require `llama-server` for the main restricted-environment workflow.

## Summary

- llama.cpp provides the main GGUF command-line route.
- The model weights and KV cache are separate optimisation targets.
- The exact repository revision and model hash must be recorded.
- A small diagnostic model should prove the toolchain before Granite is tested.
- The WinUI application should call the executable safely and stream its output without opening a local port.

## Sources used

- `SRC-LLAMACPP-GITHUB`
- `SRC-BOOK-AI-ENGINEERING-2025`
- `SRC-MS-WINDOWS-DESKTOP-2026`
