---
title: "Project Research Scope"
status: "curated"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/IBM Granite.docx"
  - "IBM Granite/IBM Granite Language Models/IBM Granite 4.1 Models.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
---

# Project research scope

The research asks how IBM Granite language models can run locally on Windows Intel PCs with lower memory use, useful speed and acceptable answer quality.

The work is split into five questions:

1. Which Granite model should be tested first?
2. How much memory is used by the model weights and the KV cache?
3. Can TurboQuant-style KV-cache compression reduce memory without unacceptable quality loss?
4. Which local runtime is suitable: llama.cpp, OpenVINO, Ollama or another tool?
5. Which CPU, GPU or NPU route works best on the target Intel computer?

The first practical model is Granite 4.1 3B. Larger 8B and 30B models are later targets after the basic workflow is stable.

## Project boundaries

The research supports a Windows desktop application, but the application should not hide experimental uncertainty. It should show measured results and explain which settings were actually tested.

The research does **not** prove that every repository works with Granite or every Intel device. Those points require project experiments.

## Further reading

- *Engineering Software Products*, Chapter 1 (product vision) and Chapter 3 (scenarios and stories).
- *Systems Engineering: Principles and Practice*, Chapters 5-6 (needs and requirements analysis).
- `windows-apps.pdf`, sections on Windows App SDK, WinUI and packaging.
