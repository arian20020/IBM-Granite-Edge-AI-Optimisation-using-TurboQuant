---
title: "Ollama"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Local Chat apps/Ollama.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OLLAMA-IMPORT"
source_ids:
  - "SRC-OLLAMA-QUICKSTART"
  - "SRC-OLLAMA-API"
  - "SRC-OLLAMA-USAGE"
  - "SRC-OLLAMA-IMPORT"
---

# Ollama

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OLLAMA-QUICKSTART](../00-sources/official-documentation.md#src-ollama-quickstart), [SRC-OLLAMA-API](../00-sources/official-documentation.md#src-ollama-api), [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage), [SRC-OLLAMA-IMPORT](../00-sources/official-documentation.md#src-ollama-import). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



Ollama is a local model runner and management system with a command-line and API-focused workflow. [SRC-OLLAMA-QUICKSTART]

It can:

- download and run local models;
- expose a local API;
- create customised models through a `Modelfile`;
- configure a system prompt, temperature and context length;
- integrate with developer tools;
- import and quantise compatible models through its own workflow.

## Difference from LM Studio

| LM Studio | Ollama |
|---|---|
| Graphical exploration | Command/API-centred workflow |
| Visual model management | Command-based model management |
| Polished local chat | Simple integration service |
| Usually chooses available pre-quantised files | Can create/import compatible model variants |

## Project use

Ollama is useful as a simple local-service reference:

```text
WinUI UI -> HTTP request -> Ollama server -> Granite -> response
```

Its ordinary model quantisation is separate from TurboQuant KV-cache compression. It should not be used as evidence that TurboQuant is supported unless that exact path is implemented and tested.

## Sources used

- [SRC-OLLAMA-QUICKSTART](../00-sources/official-documentation.md#src-ollama-quickstart) — Ollama quickstart.
- [SRC-OLLAMA-API](../00-sources/official-documentation.md#src-ollama-api) — Ollama API introduction.
- [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage) — Ollama generate endpoint and performance fields.
- [SRC-OLLAMA-IMPORT](../00-sources/official-documentation.md#src-ollama-import) — Importing a model into Ollama.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
