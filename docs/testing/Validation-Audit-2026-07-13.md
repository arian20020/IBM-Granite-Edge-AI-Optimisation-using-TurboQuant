# Controlled Testing Workspace Validation Audit

**Audit date:** 13 July 2026  
**Repository:** `arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant`  
**Initial testing-structure baseline:** merge commit `82d927baa29ef9b091f27365113ed40c1e5c4629`  
**Current repository baseline re-read:** `e869f18b6d0d6e858789008ece8782d54ac610eb`  
**Source package:** `Testing (1)(1).zip`

## 1. Audit conclusion

The first testing-structure change was a useful foundation, but it was not sufficient to run the complete workbook-driven campaign without later reconstructing important information.

The main gaps were:

1. the exact testing standard, feasibility-plan content and workbook structures were not represented in Git-reviewable form;
2. the test catalogue was empty;
3. the source package contained seven workbook files and nineteen DOCX files, which differed from the assumed six-file inventory;
4. the testing standard required a broader `/tests` hierarchy;
5. the operational procedure required the formal `experiments/granite_turboquant_intel/` evidence root;
6. the run register did not contain every value required by the workbooks;
7. quality scales differed between source workbooks;
8. no controlled blank retest versions existed for routes whose source workbook already contained historical results;
9. no frozen prompt set, rubric, source hash manifest or repeatable workspace validator existed.

The corrected workspace closes these structural gaps and was checked against the current repository after the later WinUI and requirement-evidence changes. The testing controls extend the existing evidence structure rather than replacing it.

## 2. Source-package validation

- All nineteen DOCX files opened successfully.
- All paragraph and table content was extracted into nineteen Git-reviewable Markdown records.
- Every source entry records the original archive path, size and SHA-256.
- Seven source workbook files rendered successfully during review.
- Six blank controlled-retest workbooks were derived from the appropriate source workbooks.
- Historical result values were removed from active controlled copies.
- Static test IDs, configuration rows, rubrics and source citations were retained.
- Active quality scoring was standardized to a 0-10 scale while the original source content remains unchanged.
- All six controlled Word workbooks were rendered and visually checked.
- Canonical Markdown workbook templates and a deterministic DOCX generator were prepared so the workbooks are not opaque binary-only records.

## 3. Exact workbook inventory

| Active workbook | Source workbook |
|---|---|
| WB-01 Upstream llama.cpp | `01_Upstream_llama.cpp_Editable_Test_Workbook.docx` |
| WB-02 AtomicBot TurboQuant | `AtomicBot_TurboQuant_Workbook_Quality_Verifier_v3.docx` |
| WB-03 animehacker TQ3_0 | `03_animehacker_TQ3_0_Completed_Test_Workbook_Quality10_Checked (1).docx` |
| WB-04 Official OpenVINO | `04_Official_OpenVINO_Editable_Test_Workbook.docx` |
| WB-05 Custom OpenVINO TurboQuant | `05_Custom_OpenVINO_TurboQuant_Editable_Test_Workbook.docx` |
| WB-06 Cross-route comparison | `06_Cross_Route_Editable_Comparison_Workbook.docx` |

The additional completed OpenVINO-with-TurboQuant workbook is preserved as historical evidence and a field-completeness reference. It is not used as the blank controlled template.

## 4. Test-ID validation

The controlled catalogue contains 105 unique exact IDs, including:

- `UL-B01` to `UL-B07` and `UL-01` to `UL-13`;
- `AB-B01` to `AB-B08` plus all AtomicBot formal and matched-baseline IDs;
- `AH-B01` to `AH-B08` and `AH-01` to `AH-10`;
- `OV-B01` to `OV-B07`, `OV-C01` to `OV-C06`, and `OV-01` to `OV-10`;
- `OVT-B01` to `OVT-B08` and `OVT-01` to `OVT-09`.

Each exact ID has a traceability row. Retests add a run suffix, such as `UL-04-R002`, without changing the original workbook test ID.

## 5. Workbook-completion coverage

The corrected controls gather the information needed to complete every workbook section:

- repository, commit, branch, licence and tool capabilities;
- clean build, compiler, dependencies, warnings and repository tests;
- machine, driver, memory, power and background-load state;
- model provenance, revision, tokenizer, chat template, file size and SHA-256;
- exact command, environment variables, sampling, context and output limit;
- requested and actual device/backend, layer placement, KV placement and fallback proof;
- TurboQuant/TQ3_0/TBQ activation evidence rather than flag acceptance alone;
- load time, TTFT, prompt throughput, TPOT, decode rate and total time;
- process-tree RAM, private bytes, available memory, device memory and utilization;
- P1-P6 raw outputs, deterministic checks and the weighted 0-10 rubric;
- failures, root cause, fix, retest and unresolved limitation;
- evidence paths, hashes, commits, reviewer and workbook update status;
- matched, partially matched or non-comparable cross-route status.

The source operational rule is preserved: pilot first, one excluded warm-up, then at least three measured repetitions unless a documented safety gate prevents it.

## 6. Structural validation result

The prepared structural validation records a passing audit of:

- required control files and scripts;
- nineteen source records and original hashes;
- six canonical workbook templates;
- six generated DOCX files and visual rendering;
- 105 unique test IDs and 105 traceability rows;
- 154 workbook-completion rows;
- 38 build/setup rows;
- 61 planned inference configurations;
- 61 device-verification rows;
- 60 route/prompt/role quality-evaluation rows;
- rectangular CSV files and valid JSON;
- the six-prompt controlled evaluation set;
- rubric weights summing to 1.0;
- the full testing-standard folder hierarchy;
- campaign log files remaining committable;
- absence of model weights and compiled runtime binaries.

A final typographical audit also corrected the malformed `UL –01` display to the exact controlled ID `UL-01`.

## 7. Honest readiness boundary

The repository is structurally ready to **start** the controlled campaign. It is not test-complete.

The following can only be supplied through execution on the target Windows Intel machine:

- final pinned repository, model and tool revisions;
- real build and dependency outcomes;
- actual device, graph, layer and KV-cache placement;
- memory, performance, utilization, quality and stability results;
- observed failures, fixes and retests;
- final route and application-integration decisions.

The workspace is designed so those results can be captured once, reviewed in Git, and used to fill every controlled workbook without guessing later.
