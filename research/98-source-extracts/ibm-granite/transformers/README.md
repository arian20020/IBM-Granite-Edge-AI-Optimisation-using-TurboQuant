---
title: "Transformers"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/Transformers.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

# What is it?

A neural network design used by LLMs.

# What it does

When you prompt, model doesn't see the words like we do, it sees tokens.

So, when you write a prompt “Explain AI simply”, the tokens become \[“Explain”, “AI”, “simply”\].

The transformer will look at tokens and predict what token should come next, e.g.:

Explain AI simply → "Artificial"  
Explain AI simply Artificial → "intelligence"  
Explain AI simply Artificial intelligence → "is"

So it keeps predicting the next token until it is creates a full answer. So that is the concept of inferences.

# Attention

This is the most important idea behind Transformers.

It means that the model decides which previous tokens are important for predicting the next one.

E.g. “Arian put his laptop in the bag because it was expensive”

The model needs to understand that expensive is related to the laptop and not the bag.

Attention helps the model connect related tokens.

So, Attention helps the model calculate which previous tokens are most relevant to the token it is currently processing. It gives different importance scores to those tokens and combines their information, helping the model understand relationships, meaning, and context more effectively.

# Summary

A transformer architecture processes the tokens in a prompt and uses attention to understand the relationships between them. The model then predicts the most likely next token based on the previous tokens. It repeats this process one token at a time until it produces a complete answer.
