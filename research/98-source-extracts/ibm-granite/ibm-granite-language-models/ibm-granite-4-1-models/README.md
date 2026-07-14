---
title: "IBM Granite 4.1 Models"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/IBM Granite Language Models/IBM Granite 4.1 Models.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

Granite 4.1 models have 3B, 8B, and 30B sizes:

| **Model**       | **Priority**            | **Why**                                                                      |
|-----------------|-------------------------|------------------------------------------------------------------------------|
| granite-4.1-3b  | **Main starting model** | Smallest, easiest to run locally, best for your first baseline experiment.   |
| granite-4.1-8b  | **Second target**       | More realistic LLM quality, but heavier than 3B. Good after 3B works.        |
| granite-4.1-30b | **Stretch/end goal**    | Closest to the bigger project aim, but probably too heavy for early testing. |

In terms of IBM Granite models, this project will focus on the IBM Granite 4.1 language models. These models come in three main sizes: 3B, 8B, and 30B. The 3B model is the smallest, so it should use the least RAM and VRAM, making it the most realistic model to test first for local inference. However, because it is smaller, it is likely to be less capable than the larger models.

After testing the 3B model, the next step would be to test the 8B model. The 8B model should provide better output quality, but it will also require more memory and computing power. This makes it useful for testing whether optimisation methods such as OpenVINO, quantisation, and compression can make larger Granite models more practical to run locally.

The 30B model is the largest and most capable model in this group. It is also the most demanding in terms of RAM, VRAM, and hardware requirements. For this project, the 30B model should be treated as the final target or stretch goal. If the earlier tests with the 3B and 8B models are successful, the project can then investigate whether good-quality local inference is possible with the 30B model on Intel AI PC hardware.
