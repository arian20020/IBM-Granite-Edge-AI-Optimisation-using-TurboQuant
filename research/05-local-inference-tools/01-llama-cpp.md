---
title: "llama.cpp"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Local Chat apps/Llama.cpp.docx"
  - "Repository-analysis documents"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-LLAMACPP-GITHUB"
source_ids:
  - "SRC-LLAMACPP-GITHUB"
---

# llama.cpp

> **Evidence basis:** The main factual claims in this note are traced to [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



llama.cpp is an open-source C/C++ runtime for local language-model inference, especially GGUF models. It supports several hardware backends and exposes command-line tools, a server and benchmark programs. [SRC-LLAMACPP-GITHUB]

## Project relevance

It is the main practical route for testing the supplied TurboQuant-style forks because those repositories modify llama.cpp's:

- cache data types;
- quantisation and dequantisation functions;
- cache-writing operations;
- attention kernels;
- command-line cache options.

## Important separation

- **Weight quantisation** changes the stored GGUF model file.
- **KV-cache quantisation** changes runtime storage for previous tokens and is selected when inference starts.

## Integration shape

```text
WinUI application
-> local process or local HTTP API
-> llama.cpp server
-> Granite GGUF model
-> selected CPU/GPU backend and cache format
```

Do not expose an experimental cache option to normal users until the project has validated the exact model, backend and hardware combination.

## Sources used

- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
