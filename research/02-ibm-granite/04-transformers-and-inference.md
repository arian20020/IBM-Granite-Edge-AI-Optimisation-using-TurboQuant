# Transformers and Granite inference

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This document connects the transformer architecture to the actual Granite inference flow. It explains tokens, embeddings, attention, queries, keys, values, layers, logits and token generation in the same order that information moves through the model.

## High-level inference flow

```text
User text
-> tokenizer
-> token IDs
-> embeddings
-> transformer layers and attention
-> output logits
-> select the next token
-> append the token
-> repeat until generation ends
```

The KV cache appears inside this repeated process. It stores earlier key and value vectors so they do not need to be recomputed for every new token.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017), [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache), [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## Simple mental model

A language model does not read a sentence in the same way a person does. It follows this loop: [SRC-PAPER-TRANSFORMER-2017]

```text
Text prompt
-> tokens
-> token IDs
-> embedding vectors
-> repeated transformer layers
-> scores for the next token
-> choose one token
-> add it to the sequence
-> repeat
```

## Attention

Attention helps the model decide which earlier tokens matter for the current calculation. Each layer creates three versions of its input:

- **Query:** what the current token is looking for.
- **Key:** how each stored token can be matched.
- **Value:** the information supplied when a token is relevant.

A query is compared with earlier keys. The resulting scores are normalised and used to combine the matching values.

## Granite 4.1 3B layer flow

For the recorded 3B architecture:

1. RMSNorm rescales the input so calculations remain stable.
2. Query, key and value projections are created.
3. Position information is applied to queries and keys.
4. Keys and values are saved in that layer's KV cache.
5. Each query head compares against the key/value head shared by its group.
6. Attention produces a context-aware result.
7. A feed-forward block processes the result.
8. Residual connections preserve and combine earlier information.
9. The output enters the next layer.

The same process happens across 40 layers. Every layer has its own KV cache because each layer receives a different, more contextual representation.

## Prefill and decoding

- **Prefill:** the full prompt is processed and the first KV cache is created.
- **Decode:** one new token is processed at a time while earlier keys and values are reused.

This reuse is why the KV cache improves speed. It is also why long conversations consume more memory.

The detailed diagrams and numerical examples are retained in the full source extracts.

## Sources used

- [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017) — Attention Is All You Need.
- [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache) — Transformers caching explanation.
- [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026) — ibm-granite/granite-4.1-3b.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: Full Granite Inference Guide

> **Original document:** `IBM Granite/IBM Granite Language Models/Full Granite Inference Guide.docx`

> **Archived image:** `cd46c2bee2687db88e6c5de670f38b65bc0f6579.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.

#### Input a prompt

#### That prompt text is divided into tokens

A token can represent a word, part of a word, punctuation, or a special control marker. Each token has its own tokenID based on the models vocabulary.

#### Model looks up the learned numerical representation based on that tokenID, which then gives that token a 2,560-value vector (for the 3B model)

For example: \[0.18, -0.71, 1.04, 0.03, ..., -0.22\]

#### The entire prompt then becomes a matrix.

Each row of the matrix represents a token

#### All the token vectors pass through the fixed stack of 40 transformer layers as a complete matrix.

Each transformer layer updates the representation of each token using information from the surrounding tokens. As we move through the layers, the token representations become more contextual and helpful for predicting the next token. So, the whole matrix enters transformer layer 1.

Layer 1 receives:

> \[
>
> token 1 vector: \[2,560 values\]
>
> token 2 vector: \[2,560 values\]
>
> token 3 vector: \[2,560 values\]
>
> ...
>
> token n vector: \[2,560 values\]
>
> \]
>
> Layer 1 produces an output matrix of the same shape.
>
> That matrix then enters layer 2.
>
> This keeps happening until Layer 40 (for the 3B model).

### Inside a Transformer Layer

1)  **Before the attention block, the layer first applies RMSNorm, where it slightly rescales the numbers in the embedded matrix so that they are more manageable. So, calculations remain stable.**

2)  **After this, the layer then makes three different versions of that matrix:**

> ┌─ Query converter → Query information
>
> Rescaled matrix ─────┼─ Key conver*ter* → Key information
>
> └─ Value converter → Value information
>
> In the code:

- q_proj means Query projection;

- k_proj means Key projection;

- v_proj means Value projection.

> Those are 3 transformations of the same input matrix, not 3 pieces of the matrix.
>
> So, every token has a 40 query vectors of size 64 from the query projection, while the key and value projection produces 8 vectors of size 64. The 40 query heads are arranged into 8 groups of 5, where each group shares the same key and value vectors produced by a KV head.

3)  **Add token-position information to Queries and Keys before comparing them using the RoPE. (value vectors remain unchanged)**

So now we can distinguish between “laptop” at position 3 and “laptop” at position 20.

4)  **Store or retrieve the Keys and Values**

During the original prompt:

The keys and values are produced for each prompt token within that layer’s KV cache. This happens in all 40 layers seperately. So layer 1 would look like this:

Layer 1 KV cache

KV Head 1 KV Head 2 ... KV Head 8

“Arian” K1 and V1 K2 and V2 K8 and V8

“likes” K1 and V1 K2 and V2 K8 and V8

“AI” K1 and V1 K2 and V2 K8 and V8

> Then after layer 1 will produce an updated matrix by finishing its attentions and MLP calculations and it will enter layer 2.
>
> Layer 2 will then create its own Q, K, V representation:
>
> Layer 2 K and V for “Arian”
>
> Layer 2 K and V for “likes”
>
> Layer 2 K and V for “AI”
>
> All these values will be different from layer 1’s K and V vectors because layer 2 receives more context-aware token representations.
>
> In the end every transformer layer has its own cache:
>
> Layer 1 cache → Layer 1’s K and V vectors
>
> Layer 2 cache → Layer 2’s K and V vectors
>
> Layer 3 cache → Layer 3’s K and V vectors
>
> ...
>
> Layer 40 cache → Layer 40’s K and V vectors

**3) Compare each query with the Keys to produce an attention score**

Query Head 1 belongs to the group that uses KV Head 1. It compares Query vector with the Key vector from KV Head 1 for every allowed token position.

Q1 for “AI” compared with K1 for “Arian”

Q1 for “AI” compared with K1 for “likes”

Q1 for “AI” compared with K1 for “AI”

The way the model compares is to perform a dot product on the Query and each key, and each results produces an attention score:

Q1_AI compared with K1_Arian → score: 0.7

Q1_AI compared with K1_likes → score: 1.1

Q1_AI compared with K1_AI → score: 2.4

Each query head has its own comparison. And although query heads 1-5 have the same key and value vectors (from KV Head 1), they have different query vectors, and so they can produce different scores:

Q1_AI compared with K1_Arian → 0.7

Q2_AI compared with K1_Arian → 1.8

Q3_AI compared with K1_Arian → 0.2

Q4_AI compared with K1_Arian → 1.1

Q5_AI compared with K1_Arian → 0.5

**4) After prompt has been processed (i.e. gone through all 40 layers), the model will predict its first output token.**

Suppose model predicts “because”

The sequence is now

Arian \| likes \| AI \| because

Now “because” enters Layer 1, and layer 1 creates:

40 new query vectors for “because”

8 new key vectors for “because”

8 new value vectors for “because”

So the model has the old Layer 1 cache, and now it adds the new token’s K and V vectors, so Layer 1’s cache is now:

KV Head 1 KV Head 2 ... KV Head 8

“Arian” K1 and V1 K2 and V2 K8 and V8

“likes” K1 and V1 K2 and V2 K8 and V8

“AI” K1 and V1 K2 and V2 K8 and V8

“because” K1 and V1 K2 and V2 K8 and V8

## Original research: Transformers

> **Original document:** `IBM Granite/Transformers.docx`

A neural network design used by LLMs.

### What it does

When you prompt, model doesn't see the words like we do, it sees tokens.

So, when you write a prompt “Explain AI simply”, the tokens become \[“Explain”, “AI”, “simply”\].

The transformer will look at tokens and predict what token should come next, e.g.:

Explain AI simply → "Artificial"  
Explain AI simply Artificial → "intelligence"  
Explain AI simply Artificial intelligence → "is"

So it keeps predicting the next token until it is creates a full answer. So that is the concept of inferences.

### Attention

This is the most important idea behind Transformers.

It means that the model decides which previous tokens are important for predicting the next one.

E.g. “Arian put his laptop in the bag because it was expensive”

The model needs to understand that expensive is related to the laptop and not the bag.

Attention helps the model connect related tokens.

So, Attention helps the model calculate which previous tokens are most relevant to the token it is currently processing. It gives different importance scores to those tokens and combines their information, helping the model understand relationships, meaning, and context more effectively.

### Summary

A transformer architecture processes the tokens in a prompt and uses attention to understand the relationships between them. The model then predicts the most likely next token based on the previous tokens. It repeats this process one token at a time until it produces a complete answer.

## What this means for the project

Understanding the inference loop is essential because weight quantisation, KV-cache quantisation and device acceleration affect different stages. The application must report these choices separately.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-PAPER-TRANSFORMER-2017`
- `SRC-HF-TRANSFORMERS-CACHE`
- `SRC-IBM-HF-GRANITE-41-3B-2026`
