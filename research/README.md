---
title: "IBM Granite and TurboQuant Research"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "All 44 supplied research DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
---

# IBM Granite and TurboQuant Research

This folder is the GitHub-ready version of the supplied research. It now has three connected layers:

1. **External provenance (`00-sources`)** - formal papers, official documentation, model cards, repository URLs, reliability labels and BibTeX.
2. **Curated research (`01` to `07`)** - shorter, clearer notes written in simple English and organised by topic, with source IDs.
3. **Lossless source preservation (`98` and `99`)** - every supplied document is retained as a Markdown extraction and as the exact original DOCX.

This structure solves the conflict between **condensing the research** and **not losing any content**. The curated layer removes repetition and gives the project a clear reading order. The source-preservation layer keeps every detail, image, command, table and earlier draft for audit purposes.

## Recommended reading order

1. [`01-project-scope`](01-project-scope/README.md)
2. [`02-ibm-granite`](02-ibm-granite/README.md)
3. [`03-kv-cache-and-turboquant`](03-kv-cache-and-turboquant/README.md)
4. [`04-openvino`](04-openvino/README.md)
5. [`05-local-inference-tools`](05-local-inference-tools/README.md)
6. [`06-repository-reviews`](06-repository-reviews/README.md)
7. [`07-evaluation-and-decisions`](07-evaluation-and-decisions/README.md)

## Folder structure

```text
research/
├── 00-governance/                 Traceability, claim matrix, citation rules and audit reports
├── 00-sources/                    Papers, official docs, model cards, repos and BibTeX
├── 01-project-scope/              Research purpose and official source links
├── 02-ibm-granite/                Granite family, model choice and inference flow
├── 03-kv-cache-and-turboquant/    KV cache, quantisation, PolarQuant, QJL and TurboQuant
├── 04-openvino/                   Runtime, IR, plugins, HETERO and Intel devices
├── 05-local-inference-tools/      llama.cpp, LM Studio and Ollama
├── 06-repository-reviews/         Standardised reviews of practical implementations
├── 07-evaluation-and-decisions/   Common tests, metrics and decision rules
├── 98-source-extracts/            Full Markdown extraction of every supplied DOCX
└── 99-original-docx/              Exact original files, unchanged
```

## Evidence rules

- A repository's README claim is not treated as proof that the feature works.
- **Actual method** means the behaviour recorded from source-code inspection in the supplied research.
- **Published result** means a result reported by a paper or repository author, not a result reproduced by this project.
- **Project result** should only be used after the exact commit, hardware, command, model and output evidence have been recorded.
- Repository dates and compatibility notes are preserved **as recorded in the supplied research**. Pin and recheck them before running a new experiment.

## Main research conclusion

The research supports a two-route project strategy:

- **GGUF / llama.cpp route:** use practical TurboQuant-style repositories to test real packed KV-cache compression.
- **OpenVINO route:** use Intel CPU, GPU or NPU execution as a separate baseline and optimisation path.

No supplied repository provides a finished, proven TurboQuant implementation for every Intel backend. The most relevant practical candidates are therefore treated as experiments, not finished dependencies.

## Textbook and platform guidance used for the organisation

- *Systems Engineering: Principles and Practice*, Chapters 3, 6, 12 and 17: lifecycle, requirements, risk and test traceability.
- *Engineering Software Products*, Chapters 1-4 and the testing/DevOps chapters: product vision, user needs, architecture and evidence-led development.
- *AI Engineering: Building Applications with Foundation Models*, Chapters 3, 4 and 9: evaluation methodology, model selection and inference optimisation.
- *Fundamentals of Software Architecture*, Chapters 2-6 and 19: trade-offs, quality attributes and architecture decisions.
- *The UX Book*, Chapters 10, 20-27 and 29: requirements, prototypes and user-centred evaluation.
- `windows-apps.pdf`: Windows App SDK, WinUI, design, packaging and deployment guidance.

## Source system

- Start with [`00-sources/README.md`](00-sources/README.md) to see the external bibliography.
- Use [`00-governance/claim-source-matrix.md`](00-governance/claim-source-matrix.md) to see which evidence supports each important claim.
- Use [`00-governance/source-register.md`](00-governance/source-register.md) to trace each original DOCX through the conversion process.
- Read [`00-governance/source-audit-report.md`](00-governance/source-audit-report.md) for the list of provenance issues that were repaired.
