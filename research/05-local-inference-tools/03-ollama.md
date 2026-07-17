# Ollama

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This document explains Ollama as a simple local model-management and inference tool. It records why Ollama is useful for quick experimentation while also noting that its service-oriented workflow may not match environments where local ports or background services are restricted.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: Ollama

> **Original document:** `Local Chat apps/Ollama.docx`

#### What it is

Ollama is a local model runner and model-management system.

#### It allows users to:

- Download language models.

- Load and run them locally

- Chat through a terminal or desktop interface

- Customise model behaviour

- Expose models through a local API

- Connect models to apps like VS code and coding agents

- Import and quantise compatible models

### How Ollama differs from LM Studio

| **LM Studio**                          | **Ollama**                                  |
|----------------------------------------|---------------------------------------------|
| Primarily graphical                    | Primarily command-line and API focused      |
| Built-in polished chat workspace       | Simpler model-running interface             |
| Manages models visually                | Manages models using commands               |
| Local API available                    | Local API is a central part of the platform |
| Good for exploring model settings      | Good for integrating models into software   |
| Usually downloads pre-quantised models | Can download, import and quantise models    |
| Hides more technical detail            | Exposes a simpler developer workflow        |

Ollama can quantise compatible FP16 and FP32 models itself. It supports the ollama create --quantize workflow, although that is separate from TurboQuant and is not required for our first experiment.

### Important Ollama concepts

#### Model name

An Ollama model name may look like:

granite4:3b

Here:

granite4 = model family  
3b = model tag or variant

#### Model tag

The tag identifies a particular model version or size.

Examples include:

granite4:350m  
granite4:1b  
granite4:3b  
granite4:3b-h  
granite4:7b-a1b-h  
granite4:32b-a9b-h

#### Ollama server

The Ollama application runs a background server that loads models and accepts requests from the terminal or other applications.

#### Modelfile

A Modelfile is a configuration blueprint used to create a customised Ollama model. It can define:

- the underlying model;

- temperature;

- context length;

- system prompt;

- prompt template;

- adapters;

- example messages.

Ollama describes a Modelfile as the blueprint for creating and sharing customised models.

Example:

FROM granite4:3b

PARAMETER temperature 0.2  
PARAMETER num_ctx 4096

SYSTEM """  
You are a patient computer science tutor.  
Explain technical concepts using simple English.  
"""

#### Local API

My application can send requests to Ollama rather than implementing model inference directly:

Your user interface  
↓  
HTTP request  
↓  
Ollama API  
↓  
Granite model  
↓  
Response

This is one reason Ollama is useful to study before directly integrating llama.cpp.

### Is Ollama local or cloud-based?

Ollama supports both local and cloud models.

## What this means for the project

Ollama can be used for quick checks, but a server-based route is not the preferred final design for NHS-style machines where ports or background services may be restricted.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-OLLAMA-QUICKSTART`
- `SRC-OLLAMA-API`
- `SRC-OLLAMA-USAGE`
- `SRC-OLLAMA-IMPORT`
