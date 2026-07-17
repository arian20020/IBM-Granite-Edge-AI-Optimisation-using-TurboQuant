# Granite 4.1 3B

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This is the detailed research note for Granite 4.1 3B. It keeps the original explanation of what the model is, what the “3B” label means, why the instruct variant matters, and why this model is a practical first target for local Windows and Intel testing.

## Step-by-step way to evaluate Granite 4.1 3B

1. Identify the exact model and revision.
2. Record whether it is a base or instruct checkpoint.
3. Download or convert it into the format required by the chosen backend.
4. Verify the model hash and tokenizer files.
5. Run a short correctness prompt before any optimisation.
6. Measure memory, load time, time to first token and generation speed.
7. Repeat the same prompts after weight or KV-cache quantisation.
8. Keep the unoptimised result as the quality reference.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026), [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

Granite 4.1 3B is the main starting model for the project. The supplied research records it as an instruction model for long-context chat and tasks such as summarisation, extraction, question answering, RAG, coding, function calling and multilingual dialogue. [SRC-IBM-HF-GRANITE-41-3B-2026]

## Recorded architecture details

| Property | Recorded value |
|---|---:|
| Approximate parameters | 3.4 billion |
| Stored weight size before runtime overhead | About 6.8 GB |
| Embedding size | 2,560 |
| Transformer layers | 40 |
| Query heads | 40 |
| KV heads | 8 |
| MLP hidden size | 8,192 |
| Maximum sequence length recorded in the research | 131,072 tokens |

These values explain why even a small model needs optimisation. The model weights are only one part of memory. Inference also needs the KV cache, runtime buffers, framework memory and temporary tensors.

## Why the KV heads matter

The model records keys and values for earlier tokens at each attention layer. The cache grows with context length. Using fewer KV heads than query heads reduces cache size compared with storing a separate key and value head for every query head, but long contexts can still make the cache large.

## Baseline metrics

Measure the same items before and after optimisation:

- time to first token;
- prompt-processing speed;
- generation tokens per second;
- end-to-end response time;
- peak RAM and VRAM;
- KV-cache memory;
- maximum stable context;
- task and instruction-following accuracy;
- long-context retrieval;
- factual correctness and relevance.

## Questions the experiment must answer

- Did memory use fall?
- Did speed improve or become worse?
- Was answer quality damaged?
- Did the optimised configuration make local use more practical?

## Sources used

- [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026) — ibm-granite/granite-4.1-3b.
- [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026) — Granite 4.1 language model documentation.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: Granite 4.1 3B

> **Original document:** `IBM Granite/IBM Granite Language Models/IBM Granite 4.1 3B/Granite 4.1 3B.docx`

Granite 4.1 3B is an IBM instruct language model released by the IBM Granite Team. It is available on Hugging Face under the Apache 2.0 licence. The model card provides usage examples for Transformers, vLLM, SGLang, Docker, and quantised/local app options such as llama.cpp, Ollama, and LM Studio.

Long-context instruction following and chat-based tasks.

### Capabilities

Summarisation, text classification, text extraction, question answering, RAG, coding, function calling, multilingual dialogue, fill in the middle code completion.

### How we’ll use it for testing

It is the smallest Granite 4.1 language model, so we’ll test this first for local inference on Intel PCs.

### Details

Technically, Granite 4.1 3B has around 3.4 billion parameters, so the “3B” name is an approximate model-size label rather than the exact number. Its stored model weights take around 6.8 GB, which is already quite large before adding the extra memory needed during inference, such as the KV cache, runtime overhead, framework memory, and temporary processing memory. This shows why optimisation and quantisation are important, even for the smallest Granite 4.1 language model, if the aim is to run it locally on consumer-grade Intel AI PCs.

### Transformers

Granite 4.1 3B is built using [transformer](https://liveuclac-my.sharepoint.com/personal/ucab280_ucl_ac_uk/_layouts/15/doc.aspx?sourcedoc={a647ab25-0c12-48d6-9ff1-9a75a3b70d38}&action=edit) architecture. This means the model works by reading tokens from a prompt and predicting the next token one at a time. During this process, the model uses attention to decide which previous tokens are important for generating the next part of the answer. The attention system creates key and value information, which is stored in something called the KV cache during inference. The KV cache helps the model generate text faster because it does not need to recalculate all previous token information each time it creates a new token. However, the KV cache also uses extra memory, and this memory increases when the prompt or conversation becomes longer. Therefore, even though Granite 4.1 3B is the smallest Granite 4.1 language model, its model weights are already around 6.8 GB before adding runtime memory, inference overhead, and KV cache memory. This shows why optimisation and quantisation are important for this project, because the aim is to make local Granite inference more practical on consumer-grade Intel AI PCs.

#### Architecture Details

> **Archived image:** `43d29fe779364ff4e5e066eec794ffba650565d3.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.

Granite 4.1 3B is the smallest model in the Granite 4.1 language model family. It has an embedding size of 2560, 40 layers, 40 attention heads, 8 KV heads, an MLP hidden size of 8192, and a sequence length of 131,072 tokens. These architecture details matter because they affect how much memory and computing power the model needs during inference.

The smaller embedding size and smaller MLP hidden size make the 3B model lighter than the 8B and 30B models, which is why it is the best model to test first. However, the long sequence length means the model can handle very long prompts and conversations, which increases KV cache memory usage. This is important for the project because long-context local inference is useful, but it makes memory optimisation more necessary.

The model also uses 8 KV heads, which connects directly to the KV cache. During inference, the model stores key and value information so it can generate text faster without recalculating everything from the start. This improves speed, but it also increases memory usage as the prompt or conversation becomes longer. This supports the project’s focus on optimisation, quantisation, and possible KV cache compression.

### Benchmark Metrics

The benchmark testing will have two stages. First, Granite 4.1 3B will be tested normally before optimisation. This will create the baseline. Then, the optimised or quantised version of Granite 4.1 3B will be tested using the same prompts and metrics. This will allow us to compare the normal model against the optimised model.

There are many benchmark metrics provided by IBM, but here are the most important core metrics chosen by me:

1.  Time to first token: Shows how quickly the model appears to respond.

2.  Generation speed in tokens per second: Shows how quickly the answer is produced after it begins.

3.  Prompt-processing speed: Important for large documents and long conversation histories.

4.  End-to-end response time: Represents the user’s overall waiting time.

5.  Peak RAM usage: Shows whether the model is practical on an ordinary PC.

6.  Peak VRAM usage: Important when using GPU acceleration.

7.  KV-cache memory usage: The most direct memory metric for TurboQuant.

8.  Maximum usable context length: Shows whether optimisation enables longer conversations.

9.  Task accuracy: Shows whether quantisation damages the model’s core abilities.

10. Instruction-following accuracy: Important because Granite Micro is an instruction-following model.

11. Long-context retrieval accuracy: Essential for testing TurboQuant and KV-cache compression.

12. Factual correctness and answer relevance: Detects hallucinations or incorrect information.

We will then test OpenVINO, quantisation or TurboQuant and measure the same things again. We will then answer:

Did optimisation reduce memory usage?  
Did it make inference faster?  
Did it reduce answer quality?  
Is local inference more practical after optimisation?

## What this means for the project

A 3B model is small enough to expose integration problems without immediately exhausting memory. It is therefore a good first model for validating GGUF and OpenVINO routes, the WinUI import workflow and controlled performance measurements.

## Summary

- Granite 4.1 3B is the first practical model target.
- The exact checkpoint and instruct/base status must be recorded.
- Model quality must be checked before and after optimisation.
- Local compatibility still needs to be demonstrated on each backend.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-IBM-HF-GRANITE-41-3B-2026`
- `SRC-IBM-GRANITE-41-DOCS-2026`
