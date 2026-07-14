---
title: "Granite 4.1 3B"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/IBM Granite Language Models/IBM Granite 4.1 3B/Granite 4.1 3B.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

Granite 4.1 3B is an IBM instruct language model released by the IBM Granite Team. It is available on Hugging Face under the Apache 2.0 licence. The model card provides usage examples for Transformers, vLLM, SGLang, Docker, and quantised/local app options such as llama.cpp, Ollama, and LM Studio.

# What it is designed for

Long-context instruction following and chat-based tasks.

# Capabilities

Summarisation, text classification, text extraction, question answering, RAG, coding, function calling, multilingual dialogue, fill in the middle code completion.

# How we’ll use it for testing

It is the smallest Granite 4.1 language model, so we’ll test this first for local inference on Intel PCs.

# Details

Technically, Granite 4.1 3B has around 3.4 billion parameters, so the “3B” name is an approximate model-size label rather than the exact number. Its stored model weights take around 6.8 GB, which is already quite large before adding the extra memory needed during inference, such as the KV cache, runtime overhead, framework memory, and temporary processing memory. This shows why optimisation and quantisation are important, even for the smallest Granite 4.1 language model, if the aim is to run it locally on consumer-grade Intel AI PCs.

# Transformers

Granite 4.1 3B is built using [transformer](https://liveuclac-my.sharepoint.com/personal/ucab280_ucl_ac_uk/_layouts/15/doc.aspx?sourcedoc={a647ab25-0c12-48d6-9ff1-9a75a3b70d38}&action=edit) architecture. This means the model works by reading tokens from a prompt and predicting the next token one at a time. During this process, the model uses attention to decide which previous tokens are important for generating the next part of the answer. The attention system creates key and value information, which is stored in something called the KV cache during inference. The KV cache helps the model generate text faster because it does not need to recalculate all previous token information each time it creates a new token. However, the KV cache also uses extra memory, and this memory increases when the prompt or conversation becomes longer. Therefore, even though Granite 4.1 3B is the smallest Granite 4.1 language model, its model weights are already around 6.8 GB before adding runtime memory, inference overhead, and KV cache memory. This shows why optimisation and quantisation are important for this project, because the aim is to make local Granite inference more practical on consumer-grade Intel AI PCs.

## Architecture Details



> **Archived image:** `43d29fe779364ff4e5e066eec794ffba650565d3.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.



Granite 4.1 3B is the smallest model in the Granite 4.1 language model family. It has an embedding size of 2560, 40 layers, 40 attention heads, 8 KV heads, an MLP hidden size of 8192, and a sequence length of 131,072 tokens. These architecture details matter because they affect how much memory and computing power the model needs during inference.

The smaller embedding size and smaller MLP hidden size make the 3B model lighter than the 8B and 30B models, which is why it is the best model to test first. However, the long sequence length means the model can handle very long prompts and conversations, which increases KV cache memory usage. This is important for the project because long-context local inference is useful, but it makes memory optimisation more necessary.

The model also uses 8 KV heads, which connects directly to the KV cache. During inference, the model stores key and value information so it can generate text faster without recalculating everything from the start. This improves speed, but it also increases memory usage as the prompt or conversation becomes longer. This supports the project’s focus on optimisation, quantisation, and possible KV cache compression.

# Benchmark Metrics

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
