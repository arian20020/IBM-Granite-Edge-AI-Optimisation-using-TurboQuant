---
title: "1. Understanding KV Cache"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/1. Understanding KV Cache.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

# Why do we need to cache?

Instead of recalculating the key and value vectors for every previous token whenever a new token is generated, the model stores them in the KV cache. These stored vectors are then reused in future attention calculations. This increases memory usage because the cache grows with the conversation length, but it greatly reduces repeated computation and makes token generation much faster.

# Attention

Essentially it is a framework that the model uses that established one thing:

Which earlier tokens are relevant for generating the next token?

## Q, K, V

At each attention layer, the model transforms the token embedding representation into three versions of it:

<img src="assets/20a8266e6e7736ea0a6b6395eba12b7a18170ebd.png" style="width:1.82317in;height:2.00028in" />

Where:

- x is the token's current vector representation.

- W_Q, W_K, and W_V are learned matrices.

- Q, K, and V are resulting vectors.

(Learned matrices are numerical transformation rules whose values are adjusted during training according to how wrong the model’s predictions are. They transform token representations into useful internal vectors, such as queries, keys and values, so the model becomes better at predicting the next token.)

## Query

It is essentially it is what the current token is looking for, it is looking for what previous info is useful for this current token.

### Key

Determines what type of token it is

### Value

It is a vector that contains the content of the useful tokens that should be retrieved.

## The Attention Formula

<img src="assets/ce95b9425c1dfdd836ed4c9e1f5f321735bb0b90.png" style="width:4.85484in;height:0.82303in" />

<img src="assets/74e138cff7bc6f1ffdcfdc838b63e009d978181b.png" style="width:2.85456in;height:3.40673in" />

QK^T essentially calculates the dot product of each query and each key. It then generates a number, the higher it is the more relebant the earlier token is to the current token.

We then scale the scores by dividing by the square root of the head dimension. This helps to prevent the scores from being too large, so softmax can be more reliable.

We then apply the casual mask: We also must make sure that if we are looking at a current token, we do not take into consideration future tokens,only past tokens. This is because we want the model to predict future tokens without looking at the actual future.

We then apply softmax, which converts the scors into positive weights that add to1.

We finally multiply the attention weight by all the value vectors.

## The matrix shape

The Q,K,V matrices are projections from the input embeddings of shape (b, h, T, d_head).

Where:

- B is the batch size: i.e., how many sequences are being processed together. For one conversation b = 1.

- H is the number of attention or KV heads.

- T is the sequence length, meaning how many tokens are currently being processed.

- D_head is the number of values inside the head’s vector.

## Why can the previous K and V vectors be cached

Because earlier tokens cannot look at future tokens. So the model stores the previous keys and values in a cache. So we would have

K cache: k1, k2

V cache: v1, v2

Then when we're at token 3, we have q3, k3, v3.

It then uses q3 with k1, k2, k3 and retrieves the information from v1,v2,v3.

It then appends k3, and v3 into the cache before moving onto token 4.

## We only need the current query during generation

So, if we have 3 tokens processed, we only need to process token 4’s query and check it against all the cached keys: \[k1, ..., kt\]

So each decoding step is:

Calculate the newest Q, K and V

↓

Compare the newest Q with all cached K

↓

Combine all cached V using the attention weights

↓

Store the newest K and V

↓

Predict the next token

## Every layer has a separate cache

A transformer has many attention layers and each of them has a different token embedded representation and its own learned matrices. So every key value vectors produced are different after each layer.

And so, each KV cache will be different, and so we will need one for each layer. As you can imagine, the KV cache memory can get really large.

## Prefill and decoding

**Prefill**

Suppose the user enters a prompt containing 500 tokens. During prefill, the model processes 500 tokens and creates the initial KV cache.

**Decoding**

The model then generates 1 token at a time:

Generate Token 501

↓

add k501 and v501

Generate Token 502

↓

add k502 and v502

Generate Token 503

↓

add k503 and v503

## So, the sequence is:

Current token enters a layer

↓

Create its Q, K and V

↓

Add/combine its K and V with the existing cache

↓

Compare its Q with all cached K

↓

Use the attention scores to combine all cached V

↓

Produce an updated representation

↓

After all model layers, predict the next token

Source: [Caching · Hugging Face](https://huggingface.co/docs/transformers/cache_explanation)

# Multi-head Attention

Multi-head attention applies scaled dot-product attention separately to every attention head, and those head calculations are carried out in parallel. Each head uses its own projected queries, keys and values, so each head can produce different attention scores and a different output. The head outputs are then concatenated and passed through the final output matrix W^O:

<img src="assets/9f3e41b9358c8925a09763cdc9a7fe32207725d2.png" style="width:5.79247in;height:3.57342in" />

Where W^O is called the **output projection matrix**.

It mixes the information produced by the different heads and converts it into the final output representation for the attention layer.

So the formula is basically:

Head outputs joined together

↓

multiply by learned output matrix WO

↓

final multi-head attention output

Without *W^O*, the head results would simply sit beside one another.

<img src="assets/fdbf63730542f7ecb80f12f177535e2baa77aa5d.png" style="width:4.55208in;height:6.26042in" />

Source: [1706.03762](https://arxiv.org/pdf/1706.03762)
