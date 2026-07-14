---
title: "Repository Reviews"
status: "curated"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "All files under Quantisation implementations/llama.cpp quantisation/GitHub repos"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
---

# Repository reviews

Every review uses the same questions:

- What does the repository claim?
- What does the inspected implementation actually do?
- Is the cache physically packed?
- Which hardware paths contain custom kernels?
- Has Granite been tested?
- What evidence exists for quality, memory and speed?
- How should the project use the repository?

Start with [`comparison-matrix.md`](comparison-matrix.md), then open a repository page for the reasoning behind its classification.

## Important warning

Repository status is recorded from the supplied research. Before testing, fetch the repository again, pin a commit and repeat the source review. Do not silently replace the reviewed commit with a newer one.

## Further reading

- *Systems Engineering: Principles and Practice*, Chapters 12 and 17.
- *AI Engineering*, Chapters 3, 4 and 9.
- *Fundamentals of Software Architecture*, Chapter 2 on trade-offs.
