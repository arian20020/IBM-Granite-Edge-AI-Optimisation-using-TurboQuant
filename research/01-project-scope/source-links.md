# Research source links

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025), [SRC-PAPER-POLARQUANT-2025](../00-sources/primary-research-papers.md#src-paper-polarquant-2025), [SRC-PAPER-QJL-2024](../00-sources/primary-research-papers.md#src-paper-qjl-2024), [SRC-PAPER-KIVI-2024](../00-sources/primary-research-papers.md#src-paper-kivi-2024), [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017), [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026), plus 7 repository/source entries listed below. Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

The earlier package stored only a small set of bare links. The revised package uses stable source IDs and a full source catalogue. [SRC-PAPER-TURBOQUANT-2025]

## Formal papers

- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — formal TurboQuant paper.
- [SRC-PAPER-POLARQUANT-2025](../00-sources/primary-research-papers.md#src-paper-polarquant-2025) — formal PolarQuant paper.
- [SRC-PAPER-QJL-2024](../00-sources/primary-research-papers.md#src-paper-qjl-2024) — formal QJL paper.
- [SRC-PAPER-KIVI-2024](../00-sources/primary-research-papers.md#src-paper-kivi-2024) — asymmetric 2-bit KV-cache quantisation paper.
- [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017) — transformer foundation paper.

## Official platform and model sources

- [IBM and Granite documentation](../00-sources/official-documentation.md)
- [Official Granite model cards](../00-sources/model-cards.md)
- [OpenVINO documentation](../00-sources/official-documentation.md)
- [Microsoft Windows app documentation](../00-sources/official-documentation.md)
- [Canonical and experimental repositories](../00-sources/github-repositories.md)

## Secondary links

The Google Research blog and `turbo-quant.com` guide remain available in [`secondary-sources.md`](../00-sources/secondary-sources.md), but they are not used as the sole evidence for algorithmic claims.

## Retired private source

A private SharePoint URL was present in the original research. It cannot support a public GitHub record because another reader cannot open it. Its existence is preserved in the lossless source layer, while its curated claims are now supported by public IBM documentation and official model cards.

## Complete machine-readable register

Use [`source-catalog.csv`](../00-sources/source-catalog.csv) for filtering, audit and future updates.

## Sources used

- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.
- [SRC-PAPER-POLARQUANT-2025](../00-sources/primary-research-papers.md#src-paper-polarquant-2025) — PolarQuant: Quantizing KV Caches with Polar Transformation.
- [SRC-PAPER-QJL-2024](../00-sources/primary-research-papers.md#src-paper-qjl-2024) — QJL: 1-Bit Quantized JL Transform for KV Cache Quantization with Zero Overhead.
- [SRC-PAPER-KIVI-2024](../00-sources/primary-research-papers.md#src-paper-kivi-2024) — KIVI: A Tuning-Free Asymmetric 2bit Quantization for KV Cache.
- [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017) — Attention Is All You Need.
- [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026) — Granite 4.1 language model documentation.
- [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026) — ibm-granite/granite-4.1-3b.
- [SRC-OV-GENAI-GITHUB](../00-sources/github-repositories.md#src-ov-genai-github) — openvinotoolkit/openvino.genai.
- [SRC-OV-GENAI-2026](../00-sources/official-documentation.md#src-ov-genai-2026) — Generative AI workflow.
- [SRC-OV-LLMPIPELINE-2025](../00-sources/official-documentation.md#src-ov-llmpipeline-2025) — openvino_genai.LLMPipeline (2025 documentation snapshot).
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-GOOGLE-TURBOQUANT-BLOG](../00-sources/secondary-sources.md#src-google-turboquant-blog) — TurboQuant: redefining AI efficiency with extreme compression.
- [SRC-TURBOQUANT-TOOLS](../00-sources/secondary-sources.md#src-turboquant-tools) — How to use TurboQuant.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: Sitesto help with research

> **Original document:** `Sitesto help with research.docx`

LLama.cpp:

[llama.cpp/README.md at master · ggml-org/llama.cpp](https://github.com/ggml-org/llama.cpp/blob/master/README.md)

OpenVINO GenAI:

[openvinotoolkit/openvino.genai: Run Generative AI models with simple C++/Python API and using OpenVINO Runtime](https://github.com/openvinotoolkit/openvino.genai)

Inference with OpenVINO GenAI:

<https://docs.openvino.ai/2025/openvino-workflow-generative/inference-with-genai.html>

openvino_genai.LLMPipeline

[openvino_genai.LLMPipeline — OpenVINO™ documentation — Version(2025)](https://docs.openvino.ai/2025/api/genai_api/_autosummary/openvino_genai.LLMPipeline.html#openvino-genai-llmpipeline)

TurboQuant:

[TurboQuant: Redefining AI efficiency with extreme compression](https://research.google/blog/turboquant-redefining-ai-efficiency-with-extreme-compression/)

[How to Use TurboQuant — Getting Started Guide \| TurboQuant Tools](https://turbo-quant.com/how-to-use-turboquant)

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-PAPER-TURBOQUANT-2025`
- `SRC-PAPER-POLARQUANT-2025`
- `SRC-PAPER-QJL-2024`
- `SRC-PAPER-KIVI-2024`
- `SRC-PAPER-TRANSFORMER-2017`
- `SRC-IBM-GRANITE-41-DOCS-2026`
- `SRC-IBM-HF-GRANITE-41-3B-2026`
- `SRC-OV-GENAI-GITHUB`
- `SRC-OV-GENAI-2026`
- `SRC-OV-LLMPIPELINE-2025`
- `SRC-LLAMACPP-GITHUB`
- `SRC-GOOGLE-TURBOQUANT-BLOG`
- `SRC-TURBOQUANT-TOOLS`
