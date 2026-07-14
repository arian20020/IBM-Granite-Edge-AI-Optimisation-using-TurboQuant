---
title: "Full Granite Inference Guide"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/IBM Granite Language Models/Full Granite Inference Guide.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

# Granite Inference Guide

<img src="assets/cd46c2bee2687db88e6c5de670f38b65bc0f6579.png" style="width:6.26042in;height:4.17708in" />

## Input a prompt

## That prompt text is divided into tokens

A token can represent a word, part of a word, punctuation, or a special control marker. Each token has its own tokenID based on the models vocabulary.

## Model looks up the learned numerical representation based on that tokenID, which then gives that token a 2,560-value vector (for the 3B model)

For example: \[0.18, -0.71, 1.04, 0.03, ..., -0.22\]

## The entire prompt then becomes a matrix.

Each row of the matrix represents a token

## All the token vectors pass through the fixed stack of 40 transformer layers as a complete matrix.

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

# Inside a Transformer Layer

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
