---
title: "Citation and Provenance Rules"
status: "governance"
version: "2.0"
last_updated: "2026-07-14"
---

# Citation and provenance rules

## 1. Use source IDs in research notes

Write a source marker after the statement or paragraph it supports:

```markdown
The KV cache stores keys and values from earlier tokens [SRC-HF-TRANSFORMERS-CACHE].
```

Then list the source in the document's `## Sources used` section.

## 2. Prefer the strongest available evidence

Use sources in this order:

1. formal research paper or official model card;
2. official technical documentation;
3. pinned source code;
4. repository README or official research blog;
5. secondary guide.

A secondary guide must not be the only evidence for a central technical claim.

## 3. Separate four kinds of statement

- **External fact:** supported by a source ID.
- **Source-code finding:** observed in a pinned repository review.
- **Project decision:** a design or test choice made by this project.
- **Project result:** reproduced by this project with logs, commands and environment evidence.

Do not phrase a project decision as if a paper required it. Do not phrase a repository claim as a reproduced project result.

## 4. Cite exact versions

For repositories, record URL, branch, commit and review date. For tools, record version. For model files, record the model identifier, filename and checksum.

## 5. Handle changing sources

The catalogue records access date `2026-07-14`. Before final report submission:

- re-open time-sensitive official documentation;
- check that repository URLs still exist;
- verify that the pinned commit is available;
- update the catalogue without changing established source IDs.

## 6. Handle inaccessible sources

The private SharePoint URL is retained only in the lossless source layer. It must not be used as evidence in a public GitHub repository or dissertation without a public replacement.

## 7. Dissertation references

The `[SRC-...]` IDs are internal research-control identifiers. In the final dissertation, convert them to the referencing style required by UCL and use [`00-sources/references.bib`](../00-sources/references.bib) as a starting point.

## Engineering basis

- [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020): requirements and test traceability, configuration management and risk.
- [SRC-BOOK-ENGINEERING-SOFTWARE-PRODUCTS-2021](../00-sources/books-and-project-guidance.md#src-book-engineering-software-products-2021): product documentation and code management.
- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025): component-level evaluation and inference metrics.
