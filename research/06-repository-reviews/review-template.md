# Repository review template

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: Information to gather

> **Original document:** `Quantisation implementations/llama.cpp quantisation/Information to gather.docx`

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

Bits per value:

Block structure:

Metadata overhead:

Actual memory packing:

CPU support:

CUDA support:

ROCm support:

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

Files containing main implementation:

Ease of integration:

Maintenance risk:

How we could use it:

Final recommendation:

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-BOOK-SYSTEMS-ENGINEERING-2020`
- `SRC-BOOK-AI-ENGINEERING-2025`
