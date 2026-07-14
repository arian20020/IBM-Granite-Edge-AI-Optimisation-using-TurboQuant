---
title: "Repository Review Template"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/Information to gather.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-BOOK-AI-ENGINEERING-2025"
source_ids:
  - "SRC-BOOK-SYSTEMS-ENGINEERING-2020"
  - "SRC-BOOK-AI-ENGINEERING-2025"
---

# Repository review template

> **Evidence basis:** The main factual claims in this note are traced to [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020), [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



Use this before a repository enters the implementation backlog. [SRC-BOOK-SYSTEMS-ENGINEERING-2020]

```text
Repository:
URL:
Owner:
Branch:
Commit:
Last relevant update:
Licence:
Purpose:
Claimed method:
Actual method:
Difference from formal TurboQuant:
Weight quantisation:
KV-cache quantisation:
Key-cache types:
Value-cache types:
Bits per value including metadata:
Block structure:
Actual physical packing:
CPU support:
CUDA support:
ROCm support:
Vulkan support:
SYCL support:
OpenVINO support:
NPU support:
Windows build:
Required tools:
Build command:
Run command:
Granite compatibility:
Tested Granite model:
Supported head dimensions:
Flash Attention:
Maximum tested context:
Published benchmarks:
Quality tests:
Memory tests:
Speed tests:
Known bugs:
Main implementation files:
Ease of integration:
Maintenance risk:
How the project could use it:
Final recommendation:
```

Use "not found" or "not tested" instead of guessing.

## Sources used

- [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020) — Systems Engineering: Principles and Practice, Third Edition.
- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025) — AI Engineering: Building Applications with Foundation Models.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
