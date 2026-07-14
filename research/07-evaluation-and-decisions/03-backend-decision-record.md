# Backend decision record

> **Decision status:** Accepted for the current Windows milestone
> **Version:** 3.0
> **Last updated:** 14 July 2026

## Decision

Use two local backend routes behind one application-facing interface:

1. **llama.cpp CLI for GGUF models**;
2. **OpenVINO GenAI direct API for OpenVINO models and Intel-focused acceleration**.

Do not make a local HTTP server a requirement for the core application.

## Context

The application targets users in education and healthcare-style environments. Some machines may work offline, may block local ports, or may have strict policies around background services. The application also needs to support two model ecosystems: portable GGUF files and Intel-focused OpenVINO representations.

## Why llama.cpp is included

- native C/C++ implementation;
- strong GGUF support;
- command-line prompting;
- weight and KV-cache configuration;
- useful upstream baseline for TurboQuant-related forks;
- can run without a local web server.

## Why OpenVINO GenAI is included

- direct Intel CPU, GPU and NPU route;
- model compilation and device plugins;
- generation-focused pipeline APIs;
- suitable for a native backend service;
- provides a route for testing device selection and fallback.

## Why one backend is not enough

llama.cpp gives broad GGUF portability and a direct way to compare community forks. OpenVINO gives the Intel-specific execution path required by the project brief. Keeping both behind an interface prevents the WinUI pages from becoming tied to one executable or one model format.

## Proposed application-facing interface

```text
IModelRuntime
- ValidateModelAsync(...)
- LoadModelAsync(...)
- GenerateAsync(...)
- CancelAsync(...)
- GetCapabilitiesAsync(...)
- GetMetricsAsync(...)
- UnloadAsync(...)
```

Each backend can implement this interface while returning the same application-level result types.

## Security and process rules

- use fixed executable paths and separate process arguments;
- do not build shell command strings from user input;
- capture standard output and error separately;
- support cancellation and process cleanup;
- do not expose a port by default;
- do not send prompts, documents or telemetry to a cloud service without explicit design and consent;
- validate model paths and file pairs before loading.

## Consequences

### Positive

- supports both GGUF and OpenVINO models;
- works offline;
- supports controlled comparison;
- avoids mandatory local-server deployment;
- keeps future backend changes away from the UI.

### Costs

- two backends require separate build, packaging and test routes;
- metrics and errors must be normalised;
- model compatibility differs between backends;
- cancellation and token streaming need a shared design.

## Rejected alternatives

### Server-only architecture

Rejected as the required core route because it introduces ports, service lifecycle and policy issues that are unnecessary for the first desktop application.

### LM Studio or Ollama as the product backend

Useful for research, but rejected as a required dependency because the final application should not depend on another desktop application or background service.

### OpenVINO-only route

Rejected because GGUF and llama.cpp are important for portability, existing quantised models and comparison with TurboQuant forks.

### llama.cpp-only route

Rejected because the project specifically needs an Intel/OpenVINO acceleration investigation.

## Review trigger

Review this decision when:

- a backend no longer supports the selected Granite model;
- packaging becomes impractical;
- a direct library integration becomes more reliable than process execution;
- target organisation policy changes;
- experimental evidence shows one route cannot meet the quality or performance gates.

## Sources used

- `SRC-LLAMACPP-GITHUB`
- `SRC-OV-GENAI-2026`
- `SRC-MS-WINDOWS-DESKTOP-2026`
- `SRC-BOOK-FUNDAMENTALS-SOFTWARE-ARCHITECTURE`
