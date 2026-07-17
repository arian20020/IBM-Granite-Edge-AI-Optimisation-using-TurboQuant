# IBM Granite and TurboQuant research library

This is the detailed GitHub-ready research record for the IXN IBM Granite with TurboQuant project.

## What changed in version 3

- Removed YAML front matter from every Markdown file, so GitHub and other Markdown viewers no longer show YAML parsing errors.
- Expanded the main curated documents so they retain the original explanations, examples and step-by-step learning order.
- Condensed only exact repetition and conversion noise rather than aggressively summarising the research.
- Preserved all 44 original DOCX files and all 56 extracted PNG assets in the controlled ZIP; Git keeps the complete written research and visible diagram markers.
- Kept full source extracts as a recovery and audit layer.
- Retained the external source catalogue, claim-to-source matrix, citation rules and reliability labels.

## Research layers

1. **`00-governance`** — preservation, traceability, citation and validation rules.
2. **`00-sources`** — external papers, official documentation, model cards, repositories and books.
3. **`01` to `07`** — the main detailed research documents intended for normal reading.
4. **`98-source-extracts`** — direct written extracts from every supplied DOCX.
5. **`99-original-docx`** — pointers and checksums for the exact Word files stored in the controlled ZIP.

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
├── 00-governance/
├── 00-sources/
├── 01-project-scope/
├── 02-ibm-granite/
├── 03-kv-cache-and-turboquant/
├── 04-openvino/
├── 05-local-inference-tools/
├── 06-repository-reviews/
├── 07-evaluation-and-decisions/
├── 98-source-extracts/
└── 99-original-docx/
```

## Evidence rules

- A paper result is labelled as a paper result until reproduced by this project.
- A repository README claim is not treated as a verified implementation result.
- Model, backend and hardware compatibility must be tested using pinned versions.
- Weight format, K-cache format and V-cache format are recorded separately.
- Requested and actual execution devices are recorded separately.
- Failed and blocked tests remain in the evidence record.

## Original-file preservation

All 44 original DOCX files and all 56 extracted PNG assets are stored in the controlled v3 ZIP. Git includes the full written extracts, original relative paths, SHA-256 records and a visible marker at every diagram position. The `99-original-docx` folder explains how to retrieve the binaries.
