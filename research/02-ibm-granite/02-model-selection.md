# Granite 4.1 model selection

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This document explains how the Granite 4.1 model choices were narrowed down for the project. The aim is not simply to choose the largest model. The aim is to choose models that can be tested fairly on local Intel hardware and that give useful comparisons between quality, memory use and speed.

## Step-by-step model-selection method

1. Confirm the task: local instruction-following and text generation.
2. Check whether the model is an instruct model rather than only a base model.
3. Record parameter count, model format, context length and licence.
4. Estimate weight memory and KV-cache memory separately.
5. Start with the smaller model as a controlled baseline.
6. Add a larger model only when the smaller route is stable.
7. Keep the original and optimised variants so quality and performance can be compared fairly.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026), [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026), [SRC-IBM-HF-GRANITE-41-8B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-8b-2026), [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

Use a staged model order rather than starting with the largest model. [SRC-IBM-GRANITE-41-DOCS-2026]

| Model | Project role | Reason |
|---|---|---|
| Granite 4.1 3B | First baseline | Lowest expected memory and easiest local starting point |
| Granite 4.1 8B | Second target | Better quality, but a more demanding test of optimisation |
| Granite 4.1 30B | Stretch target | Closest to the large-model goal, but unsuitable for the first experiment |

## Decision rule

Do not move to the next model only because the previous model launched once. Move forward after the baseline is repeatable and includes:

- successful loading and generation;
- peak RAM/VRAM;
- time to first token;
- prompt and decode speed;
- quality results;
- saved command, commit and logs.

This staged approach reduces risk and makes failures easier to diagnose.

## Sources used

- [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026) — Granite 4.1 language model documentation.
- [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026) — ibm-granite/granite-4.1-3b.
- [SRC-IBM-HF-GRANITE-41-8B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-8b-2026) — ibm-granite/granite-4.1-8b.
- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025) — AI Engineering: Building Applications with Foundation Models.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: IBM Granite 4.1 Models

> **Original document:** `IBM Granite/IBM Granite Language Models/IBM Granite 4.1 Models.docx`

Granite 4.1 models have 3B, 8B, and 30B sizes:

| **Model**       | **Priority**            | **Why**                                                                      |
|-----------------|-------------------------|------------------------------------------------------------------------------|
| granite-4.1-3b  | **Main starting model** | Smallest, easiest to run locally, best for your first baseline experiment.   |
| granite-4.1-8b  | **Second target**       | More realistic LLM quality, but heavier than 3B. Good after 3B works.        |
| granite-4.1-30b | **Stretch/end goal**    | Closest to the bigger project aim, but probably too heavy for early testing. |

In terms of IBM Granite models, this project will focus on the IBM Granite 4.1 language models. These models come in three main sizes: 3B, 8B, and 30B. The 3B model is the smallest, so it should use the least RAM and VRAM, making it the most realistic model to test first for local inference. However, because it is smaller, it is likely to be less capable than the larger models.

After testing the 3B model, the next step would be to test the 8B model. The 8B model should provide better output quality, but it will also require more memory and computing power. This makes it useful for testing whether optimisation methods such as OpenVINO, quantisation, and compression can make larger Granite models more practical to run locally.

The 30B model is the largest and most capable model in this group. It is also the most demanding in terms of RAM, VRAM, and hardware requirements. For this project, the 30B model should be treated as the final target or stretch goal. If the earlier tests with the 3B and 8B models are successful, the project can then investigate whether good-quality local inference is possible with the 30B model on Intel AI PC hardware.

## What this means for the project

Granite 4.1 3B is the practical first validation model. A larger Granite model can become the stress test after the model-loading, prompting, logging and quality-scoring pipeline is stable.

## Summary

- Start with a stable smaller baseline.
- Record weights and KV cache separately.
- Use the same prompts and settings for original and optimised models.
- Move to larger models only after the full evidence pipeline works.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-IBM-GRANITE-41-DOCS-2026`
- `SRC-IBM-HF-GRANITE-41-3B-2026`
- `SRC-IBM-HF-GRANITE-41-8B-2026`
- `SRC-BOOK-AI-ENGINEERING-2025`
