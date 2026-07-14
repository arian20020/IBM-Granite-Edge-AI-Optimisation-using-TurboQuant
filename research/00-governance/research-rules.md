---
title: "Research Evidence Rules"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
---

# Research evidence rules

## 1. Separate claims from evidence

Use these labels consistently:

- **Paper claim:** reported by the formal research paper.
- **Repository claim:** stated by a repository author or README.
- **Source-code finding:** behaviour identified in the supplied code review.
- **Project observation:** behaviour seen during this project's own run.
- **Project result:** a repeatable observation with saved logs, commands and environment details.

## 2. Pin all experimental inputs

Every experiment must record:

- repository URL, branch and commit;
- model name, file and checksum;
- runtime and build options;
- Windows version and driver versions;
- CPU, GPU, NPU, RAM and VRAM;
- cache types, context length and generation settings;
- test prompt or dataset version;
- raw logs and output files.

## 3. Do not treat compression ratio as total-memory reduction

A smaller KV cache does not shrink model weights, application memory, runtime buffers or every temporary tensor. Report both:

- KV-cache bytes; and
- peak process RAM/VRAM.

## 4. Compare like with like

Use the same model, prompt set, context lengths and generation settings when comparing baselines and optimised modes.

## 5. Quality comes before speed claims

A configuration is not successful only because it is smaller or faster. It must also pass output-quality, stability and long-context checks.

## 6. Record negative results

Crashes, unsupported routes, poor quality and slower performance are useful engineering evidence. Keep them in the test register rather than deleting them.

## Further reading

- *Systems Engineering: Principles and Practice*, Chapter 17 (test and evaluation) and Chapter 12 (risk management).
- *AI Engineering*, Chapters 3-4 (evaluation design and component-level evaluation).
- *Fundamentals of Software Architecture*, Chapter 2 (trade-off analysis).
## 7. Trace every external claim

Every important technical claim must use a source ID from `00-sources`. The original DOCX name alone is not an external citation.

## 8. Use public replacements

Do not rely on private SharePoint pages or inaccessible files for a public GitHub record. Preserve them for history, then replace their evidential role with public papers, official documentation or model cards.

## 9. Recheck fast-moving sources

Repository and documentation links were checked on 2026-07-14. Recheck them and record exact versions before the final dissertation and before each benchmark campaign.

## Provenance documents

- [`citation-rules.md`](citation-rules.md)
- [`claim-source-matrix.md`](claim-source-matrix.md)
- [`../00-sources/source-catalog.csv`](../00-sources/source-catalog.csv)
