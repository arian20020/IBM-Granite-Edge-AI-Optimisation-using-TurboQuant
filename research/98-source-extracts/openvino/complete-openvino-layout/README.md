---
title: "Complete OpenVINO layout"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/Complete OpenVINO layout.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

Yes. The clearest way to understand the complete system is to separate it into **three connected levels**:

1\. Generative AI pipeline  
Converts text into tokens and manages generation  
  
2. LLM architecture  
Performs embeddings, attention or Mamba processing,  
feed-forward calculations and next-token prediction  
  
3. OpenVINO execution  
Represents those calculations as an IR graph,  
compiles the graph and executes it on the hardware

# **1. Complete end-to-end architecture**

User enters text  
│  
▼  
OpenVINO GenAI / Tokenizer  
Converts text into token IDs  
│  
▼  
Token IDs  
Example: \[4812, 318, 257, 9707\]  
│  
▼  
┌───────────────────────────────────────────────────────────┐  
│ MAIN LANGUAGE-MODEL GRAPH │  
│ │  
│ Token embedding │  
│ Converts every token ID into a numerical vector │  
│ │ │  
│ ▼ │  
│ Add or apply positional information │  
│ Helps the model understand token order │  
│ │ │  
│ ▼ │  
│ Repeated model blocks │  
│ ┌─────────────────────────────────────────────────────┐ │  
│ │ Normalisation │ │  
│ │ │ │ │  
│ │ ▼ │ │  
│ │ Attention or another sequence-processing block │ │  
│ │ │ │ │  
│ │ ▼ │ │  
│ │ Residual connection │ │  
│ │ │ │ │  
│ │ ▼ │ │  
│ │ Normalisation │ │  
│ │ │ │ │  
│ │ ▼ │ │  
│ │ Feed-forward network or Mixture-of-Experts │ │  
│ │ │ │ │  
│ │ ▼ │ │  
│ │ Residual connection │ │  
│ └─────────────────────────────────────────────────────┘ │  
│ │ │  
│ ▼ │  
│ Final normalisation │  
│ │ │  
│ ▼ │  
│ Language-model output projection │  
│ │ │  
│ ▼ │  
│ Logits: scores for every possible next token │  
└───────────────────────────────────────────────────────────┘  
│  
▼  
OpenVINO GenAI generation settings  
Temperature, top-k, top-p or greedy selection  
│  
▼  
Select the next token  
│  
▼  
Convert the token back into text  
│  
▼  
Add the token to the sequence and repeat

The tokenizer is normally separate from the main LLM graph. It converts text into the token IDs expected by the model, and the matching tokenizer used during training is necessary to reproduce the model’s intended behaviour.

# **2. Tokenisation and embeddings**

Suppose the user enters:

“OpenVINO runs models locally.”

The tokenizer may divide this into pieces:

\["Open", "VINO", " runs", " models", " locally", "."\]

Each token is assigned an integer ID:

\[8142, 3921, 703, 4219, 16281, 13\]

The main model does not directly understand those numbers as language. The embedding operation uses each ID to select a learned vector.

Token ID 8142  
│  
▼  
Embedding table  
│  
▼  
\[0.13, -0.27, 0.81, ...\]

For multiple tokens, the vectors are arranged into a tensor:

\[batch size, sequence length, hidden size\]

For example:

\[1, 6, 4096\]

This means:

- one input sequence;

- six tokens;

- a 4,096-value representation for each token.

Calling it a matrix is acceptable in a simplified explanation, but **tensor** is more accurate because the data commonly has three or more dimensions.

# **3. Positional information**

Attention considers relationships between tokens, but the model also needs to understand their positions.

For example:

“The dog chased the cat”

is different from:

“The cat chased the dog”

The tokens may be similar, but their order changes the meaning.

The model therefore applies positional information to the token representations. The exact method is architecture-specific. Some models add positional embeddings, while many modern LLMs apply techniques such as rotary positional encoding during attention.

The important beginner-level idea is:

Embedding  
=  
what the token represents  
  
Position information  
=  
where the token appears in the sequence

# **4. The repeated transformer block**

A conventional decoder-only transformer contains many repeated blocks.

Embedded token representations  
│  
▼  
Transformer block 1  
│  
▼  
Transformer block 2  
│  
▼  
Transformer block 3  
│  
▼  
...  
│  
▼  
Transformer block N

Each block normally contains:

Input hidden states  
│  
▼  
Normalisation  
│  
▼  
Self-attention  
│  
▼  
Residual addition  
│  
▼  
Normalisation  
│  
▼  
Feed-forward network  
│  
▼  
Residual addition

A **residual connection** carries the original input around a calculation and adds it back to the result:

Input ──────────────────────────────┐  
│ │  
▼ │  
Attention or feed-forward operation │  
│ │  
└──────────────► Add ◄────────────┘

This helps preserve information as it moves through many model layers.

# **5. The complete attention calculation**

Inside a self-attention section, each token representation is projected into three representations:

Input token representations  
│  
┌─────┼─────┐  
│ │ │  
▼ ▼ ▼  
Query Key Value  
Q K V

These are produced using learned weight matrices:

*Q=XWQQ=XW_Q*Q=XWQ *K=XWKK=XW_K*K=XWK *V=XWVV=XW_V*V=XWV

Where:

- *XX*X is the tensor entering the attention layer;

- *WQW_Q*WQ contains the learned query-projection weights;

- *WKW_K*WK contains the key-projection weights;

- *WVW_V*WV contains the value-projection weights.

## **Query**

The query represents what a token is looking for from the other available tokens.

## **Key**

The key represents the information by which a token can be matched or identified.

## **Value**

The value contains the information that will be combined into the attention output.

A useful analogy is:

Query  
=  
“What information am I looking for?”  
  
Key  
=  
“What type of information do I contain?”  
  
Value  
=  
“What information should I provide if I am relevant?”

# **6. Query-key relevance scores**

The model compares queries with keys:

Query tensor  
│  
▼  
Matrix multiplication with transposed Key tensor  
│  
▼  
Raw attention scores

Mathematically:

*QKTQK^T*QKT

For example, the query for the word “it” may produce higher scores for earlier words that could explain what “it” refers to.

The scores are scaled:

*QKTdk\frac{QK^T}{\sqrt{d_k}}*dk QKT

where *dkd_k*dk is the size of each key vector.

They then pass through a causal mask and softmax:

*softmax(QKTdk)softmax\left(\frac{QK^T}{\sqrt{d_k}}\right)*softmax(dk QKT )

Finally, those attention weights are multiplied by the values:

*Attention(Q,K,V)=softmax(QKTdk)VAttention(Q,K,V) = softmax\left(\frac{QK^T}{\sqrt{d_k}}\right)V*Attention(Q,K,V)=softmax(dk QKT )V

This produces a weighted combination of the value vectors. The scaled dot-product attention formula and the use of parallel attention heads were introduced in the original Transformer architecture.

The complete process is:

Q × Kᵀ  
│  
▼  
Raw relevance scores  
│  
▼  
Scale by √dₖ  
│  
▼  
Apply causal attention mask  
│  
▼  
Softmax  
│  
▼  
Attention weights  
│  
▼  
Multiply by V  
│  
▼  
Attention output

# **7. Why a causal mask is required**

During next-token generation, a token must not see future tokens.

For this sequence:

The model runs locally

when processing “model,” the model may use:

The  
The model

but it must not use:

runs locally

because those tokens are considered future information during training or sequential generation.

The causal mask prevents attention to future positions:

Key token  
1 2 3 4  
Query 1 ✓ ✗ ✗ ✗  
Query 2 ✓ ✓ ✗ ✗  
Query 3 ✓ ✓ ✓ ✗  
Query 4 ✓ ✓ ✓ ✓

# **8. Multi-head attention**

The model does not normally perform only one attention calculation.

The hidden representation is divided into several attention heads:

Input representations  
│  
┌────┼────┬────┐  
│ │ │ │  
▼ ▼ ▼ ▼  
Head 1 Head 2 Head 3 Head 4  
│ │ │ │  
└────┴────┴────┘  
│  
▼  
Concatenate head outputs  
│  
▼  
Output projection

Different heads can learn different relationship patterns.

For example, one head might become useful for:

- nearby word relationships;

- sentence structure;

- references between pronouns and nouns;

- longer-range contextual relationships.

The heads operate on different learned projections of the same input. Their outputs are then joined and projected back into the model’s hidden size.

# **9. Query heads and KV heads**

Not every model uses the same number of query, key and value heads.

## **Standard multi-head attention**

32 query heads  
32 key heads  
32 value heads

## **Grouped-query attention**

32 query heads  
8 key heads  
8 value heads

Several query heads share one set of key and value heads.

## **Multi-query attention**

32 query heads  
1 key head  
1 value head

All query heads share the same key and value head.

Using fewer KV heads can reduce:

- KV-cache memory;

- memory movement;

- decoding overhead.

The important correction is that **having heads does not itself reduce work**. Memory is reduced when the architecture uses fewer key/value heads than query heads.

# **10. The KV cache**

During autoregressive generation, the model repeatedly predicts one new token.

Without a KV cache:

Generate token 1:  
calculate keys and values for the whole prompt  
  
Generate token 2:  
recalculate keys and values for the whole prompt  
and token 1  
  
Generate token 3:  
recalculate everything again

With a KV cache:

Generate token 1:  
calculate and store previous keys and values  
  
Generate token 2:  
reuse stored keys and values  
and calculate only the new token's values  
  
Generate token 3:  
reuse the cache again

The cache stores:

Keys from previous tokens  
Values from previous tokens

It does not normally need to store previous query tensors because the query is primarily needed for the current attention calculation.

Conceptually:

Current token representation  
│  
├──► New query  
│ │  
│ ▼  
│ Compare with all cached keys  
│  
├──► New key ─────► Add to KV cache  
│  
└──► New value ───► Add to KV cache

The KV cache is normally organised across:

Transformer layers  
×  
Sequences or batches  
×  
KV heads  
×  
Token positions  
×  
Head dimensions

This is why the cache becomes larger when:

- context length increases;

- batch size increases;

- more sequences run simultaneously;

- the model has more attention layers;

- the model has more KV heads;

- a higher cache precision is used.

OpenVINO-supported serving paths expose reduced KV-cache precisions such as U8 and U4 alongside F16 and FP32 in supported configurations.

# **11. Prefill and decoding**

LLM inference has two main phases.

## **Prefill**

The complete user prompt is initially processed.

Prompt:  
“Explain how OpenVINO executes an LLM.”  
  
All prompt tokens  
│  
▼  
Model processes them  
│  
▼  
Initial KV cache is created  
│  
▼  
First generated token

## **Decode**

After prefill, tokens are generated one at a time:

Previous token  
│  
▼  
Run one generation step  
│  
▼  
Update KV cache  
│  
▼  
Produce next token  
│  
▼  
Repeat

The KV cache is especially important during decoding because it avoids repeatedly processing all earlier keys and values.

# **12. The feed-forward network**

After attention, the token representations normally pass through a feed-forward network.

A simplified version is:

Attention output  
│  
▼  
Linear projection  
│  
▼  
Activation function  
│  
▼  
Linear projection  
│  
▼  
Feed-forward output

The feed-forward network processes each token position using learned transformations.

The activation is not necessarily ReLU. Modern LLMs may use model-specific activations and gated structures such as GELU, SiLU or SwiGLU.

Therefore, a safe description is:

The attention output passes through a feed-forward network containing learned linear transformations and a model-specific activation function.

# **13. Mixture-of-Experts**

Some models replace one ordinary feed-forward network with a Mixture-of-Experts structure.

Input representation  
│  
▼  
Router  
Decides which experts are relevant  
│  
┌───┴────┐  
▼ ▼  
Expert 2 Expert 6  
│ │  
└───┬────┘  
▼  
Combined output

The model may contain many experts, but only a selected subset is used for each token.

This allows the model to contain more total parameters without using every parameter for every token.

IBM states that selected Granite 4.0 models combine the hybrid Mamba-2/transformer architecture with a Mixture-of-Experts strategy.

# **14. Final projection and logits**

After the final model block, the representation passes through a final normalisation and output projection.

Final hidden representation  
│  
▼  
Language-model output projection  
│  
▼  
One score for every possible vocabulary token

Suppose the model has a vocabulary of 50,000 tokens. It may produce:

\[1, 50000\]

for the newest token position.

These scores are called **logits**.

Example:

Token “OpenVINO” → 9.8  
Token “the” → 8.2  
Token “model” → 7.6  
Token “banana” → -2.1

Logits are not yet probabilities.

The generation layer uses them to select the next token.

# **15. Next-token selection**

OpenVINO GenAI can apply generation settings such as:

Greedy selection  
Choose the token with the highest score  
  
Temperature  
Adjust how predictable or varied the selection is  
  
Top-k  
Only consider the k highest-scoring tokens  
  
Top-p  
Consider the smallest token group whose  
combined probability reaches a threshold

The selected token is decoded into text and also added back to the input sequence:

Current sequence  
│  
▼  
Model predicts next token  
│  
▼  
Selected next token  
│  
├──► Display as text  
│  
└──► Add to sequence for next generation step

OpenVINO GenAI sits above OpenVINO Runtime and manages this generative pipeline, including tokenisation and repeated generation calls.

# **16. How the architecture becomes an OpenVINO IR graph**

The architectural diagram uses labels such as:

Embedding  
Attention  
Feed-forward network  
Final projection

However, the actual OpenVINO IR graph may contain much more detailed operations.

For example, one attention block may become:

Parameter  
│  
▼  
Normalisation operations  
│  
├────────────┬────────────┐  
▼ ▼ ▼  
MatMul Q MatMul K MatMul V  
│ │ │  
▼ ▼ ▼  
Reshape Reshape Reshape  
│ │ │  
▼ ▼ ▼  
Transpose Transpose Transpose  
│ │ │  
└──────┬─────┘ │  
▼ │  
MatMul QKᵀ │  
│ │  
▼ │  
Multiply scale │  
│ │  
▼ │  
Add mask │  
│ │  
▼ │  
Softmax │  
│ │  
└───────┬──────────┘  
▼  
MatMul  
│  
▼  
Reshape  
│  
▼  
Output projection  
│  
▼  
Residual Add

OpenVINO IR represents the graph using operation nodes, ports, edges, tensor shapes, element types and constants. The XML describes the graph while the matching BIN file stores the large constant data used by it.

# **17. The graph is directed, but not strictly sequential**

The model is not necessarily one straight chain.

For example, Q, K and V can branch from the same input:

┌──► Query projection  
Input hidden states ├──► Key projection  
└──► Value projection

Independent branches may potentially be processed in parallel.

The graph can also merge:

Original input ─────────────────┐  
▼  
Processed attention result ───► Add

Therefore:

Directed  
=  
every connection has a defined direction  
  
Computation graph  
=  
operations are connected according to  
which tensors they consume and produce  
  
Not necessarily sequential  
=  
independent branches may run in parallel  
and later merge

# **18. What the device plugin does with the graph**

The IR graph remains hardware-independent.

OpenVINO IR graph  
│  
▼  
OpenVINO Runtime  
│  
▼  
Selected device plugin  
CPU, GPU or NPU

The device plugin may:

- check operation compatibility;

- fuse several operations;

- select hardware-specific kernels;

- select tensor layouts;

- choose supported numerical precisions;

- allocate memory;

- create an execution schedule;

- prepare the compiled model.

For example:

IR graph:  
  
MatMul  
│  
▼  
Add  
│  
▼  
Activation

may become:

Compiled GPU graph:  
  
Fused MatMul + Add + Activation kernel

The compiled graph may therefore look different from the original IR graph. OpenVINO IR is the portable representation, while the compiled model is the device-specific execution representation.

# **19. The complete OpenVINO execution picture**

User text  
│  
▼  
OpenVINO GenAI tokenizer  
│  
▼  
Token IDs  
│  
▼  
OpenVINO IR language-model graph  
│  
├── Embedding operations  
├── Position-related operations  
├── Attention or Mamba operations  
├── Normalisation  
├── Residual additions  
├── Feed-forward or expert operations  
└── Final projection  
│  
▼  
OpenVINO Runtime  
Reads and validates the graph  
│  
▼  
Device plugin  
Compiles and optimises the graph  
│  
▼  
Compiled model  
Prepared for CPU, GPU or NPU  
│  
▼  
Hardware executes inference  
│  
▼  
Logits  
│  
▼  
OpenVINO GenAI selects a token  
│  
▼  
Token is converted into text  
│  
▼  
Generation repeats

# **20. Important Granite 4.0 difference**

The architecture above describes a conventional transformer-based LLM.

However, **Granite 4.0 is a hybrid Mamba-2/transformer model family**. This means its repeated stack does not consist entirely of normal attention blocks. It combines:

- Mamba-2 sequence-state blocks;

- a smaller number of transformer attention blocks;

- Mixture-of-Experts components in selected models.

A simplified Granite 4.0 view is:

Token embeddings  
│  
▼  
Mamba-2 block  
│  
▼  
Mamba-2 block  
│  
▼  
Mamba-2 block  
│  
▼  
Transformer attention block  
│  
▼  
Mamba-2 block  
│  
▼  
Mamba-2 block  
│  
▼  
Repeated hybrid structure  
│  
▼  
Final projection  
│  
▼  
Logits

Mamba-style sequence-state blocks maintain and update a compact internal state rather than comparing every token with every previous token through ordinary self-attention. IBM explains that Granite 4.0 combines these state-space blocks with transformer blocks to improve efficiency on long sequences.

This matters for TurboQuant because:

Transformer attention blocks  
=  
produce key and value tensors  
and use a KV cache  
  
Mamba-2 blocks  
=  
use a different internal sequence state  
rather than a conventional attention KV cache

Therefore, for Granite 4.0, TurboQuant would apply to the **KV caches belonging to the transformer attention blocks**, not automatically to every layer in the model.

# **Final mental model**

The user’s text is first converted into token IDs. The main model converts these IDs into embedding vectors and processes them through repeated model blocks. In a transformer attention block, token representations are projected into query, key and value tensors. Queries and keys produce scaled, masked attention scores, which are passed through SoftMax and used to combine the values. Previous keys and values can be stored in the KV cache for reuse during generation. The outputs then pass through residual connections, normalisation and feed-forward or expert components. The final projection produces logits, and OpenVINO GenAI selects the next token.

OpenVINO IR represents all these mathematical calculations as a directed graph of detailed operations, tensors and connections. OpenVINO Runtime passes that hardware-independent graph to the selected CPU, GPU or NPU plugin, which creates an optimised compiled graph for the hardware. Granite 4.0 modifies the conventional picture by combining transformer attention blocks with more memory-efficient Mamba-2 sequence-state blocks.
