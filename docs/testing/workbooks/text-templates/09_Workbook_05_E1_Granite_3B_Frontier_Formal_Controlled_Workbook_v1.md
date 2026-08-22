# 09 Workbook 05 E1 Granite 3B Frontier and Formal Controlled Workbook v1.0

**Workbook ID:** `WB-09`
**Controlled filename:** `09_Workbook_05_E1_Granite_3B_Frontier_Formal_Controlled_Workbook_v1.docx`
**Campaign ID:** `GTQ-WB05-MF-v1`
**Stage:** `E1`
**Status:** Initialised - no live result is entered

## Purpose

Discover the safe Granite 3B context frontier for each admitted symmetric configuration and then collect matched formal performance, memory, quality and perplexity evidence. This combines completion-plan Tasks R19 and R20 while preserving every repetition and keeping frontier discovery separate from formal statistics.

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

| Control | Fixed rule |
| --- | --- |
| Canonical source | docs/testing/workbooks/text-templates/09_Workbook_05_E1_Granite_3B_Frontier_Formal_Controlled_Workbook_v1.md |
| Generated working copy | docs/testing/workbooks/generated/09_Workbook_05_E1_Granite_3B_Frontier_Formal_Controlled_Workbook_v1.docx |
| Execution index | docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv |
| Configuration matrix | docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv |
| Evidence register | docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv |
| Allowed row states | Not started, Blocked pending predecessor, Passed, Failed, Blocked, Not applicable, Skipped by frontier, Infrastructure interrupted |
| Live authority | None until predecessor gates and an explicit self-hosted dispatch are accepted |

## Initial revision record

| Version | Date | Change | Affected scope | Status |
| --- | --- | --- | --- | --- |
| 1.0 | 2026-08-22 | Created the first controlled post-C workbook structure. | E1 | Current - initialised |


[[PAGEBREAK]]

# 1. Immutable experiment identity

| Identity field | Required value/evidence | Observed value | Hash/status |
| --- | --- | --- | --- |
| Model repository/revision | Accepted Granite 4.1 3B asset lock |  | Blocked pending predecessor |
| Converted model | Exact IR/config/tokenizer catalogue and hashes |  |  |
| Runtime/GenAI | Accepted commits and binary hashes |  |  |
| Prompt set | GTQ-PROMPTS-v1 |  |  |
| Rubric | GTQ-QUALITY-RUBRIC-v1 |  |  |
| P5 fixture | Frozen end-marker retrieval fixture + hash |  |  |
| Generation controls | Frozen seed/sampling/output-token limit |  |  |

# 2. Symmetric configuration set

| Configuration ID | Route | K algorithm | K precision | V algorithm | V precision | Planning K bytes | Planning V bytes | Role |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RA-SCALAR-U8-SYM | Route A | SCALAR | U8 | SCALAR | U8 | 136 | 136 | Formal control |
| RA-SCALAR-U4-SYM | Route A | SCALAR | U4 | SCALAR | U4 | 72 | 72 | Formal control |
| RA-TURBO-U4-SYM | Route A | TURBO | U4 | TURBO | U4 | 72 | 72 | Decision candidate |
| RA-TURBO-U3-SYM | Route A | TURBO | U3 | TURBO | U3 | 52 | 52 | Decision candidate |
| RB-TBQ4-SYM | Route B | TBQ4 | codec | TBQ4 | codec | 68 | 68 | Experimental candidate |
| RB-TBQ3-SYM | Route B | TBQ3 | codec | TBQ3 | codec | 52 | 52 | Experimental candidate |
| RB-QJL4-SYM | Route B | TBQ4_QJL | codec | TBQ4_QJL | codec | 88 | 88 | Experimental candidate |
| RB-QJL3-SYM | Route B | TBQ3_QJL | codec | TBQ3_QJL | codec | 72 | 72 | Experimental candidate |
| RB-POLAR4-SYM | Route B | POLAR4 | codec | POLAR4 | codec | ~68 | ~68 | Experimental candidate |
| RB-POLAR3-SYM | Route B | POLAR3 | codec | POLAR3 | codec | ~52 | ~52 | Experimental candidate |

# 3. Context frontier schedule

Start at 512 tokens, continue through 1,024, 2,048, 4,096, 8,192, 16,384, then double while supported and stable. Retry one potentially transient failure after cooldown. After a repeated failure, record the preceding stable point as the provisional frontier and mark higher points `Skipped by frontier`.

| Configuration | Context | Fixture | Attempt | Initial status | Exact marker | Safety/exit | Run/artifact/evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| RA-SCALAR-U8-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U8-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-SCALAR-U4-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U4-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RA-TURBO-U3-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ4-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-TBQ3-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL4-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-QJL3-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR4-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 512 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 1024 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 2048 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 4096 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 8192 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 16384 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 32768 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 65536 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |
| RB-POLAR3-SYM | 131072 | P5 end-marker retrieval fixture | 0 | Blocked pending predecessor |  |  |  |

# 4. Frontier summary

| Configuration | Last stable context | First repeated failure or declared limit | Frontier class | Peak private MB | Minimum available RAM | KV MB | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

[[PAGEBREAK]]

# 5. Formal repetition register

The common formal context must be supported by every compared configuration. One pilot and one warm-up remain visible but excluded; at least three measured repetitions are retained individually.

| Configuration | Rep role/no. | Statistics | Load ms | TTFT ms | Prompt tok/s | TPOT ms | Decode tok/s | Peak private MB |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RA-SCALAR-U8-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RB-TBQ4-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RB-TBQ3-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RB-QJL4-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RB-QJL3-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RB-POLAR4-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RB-POLAR3-SYM | Pilot | Excluded |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RB-TBQ4-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RB-TBQ3-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RB-QJL4-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RB-QJL3-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RB-POLAR4-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RB-POLAR3-SYM | Warm-up | Excluded |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RB-TBQ4-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RB-TBQ3-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RB-QJL4-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RB-QJL3-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RB-POLAR4-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RB-POLAR3-SYM | Measured 1 | Included |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RB-TBQ4-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RB-TBQ3-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RB-QJL4-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RB-QJL3-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RB-POLAR4-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RB-POLAR3-SYM | Measured 2 | Included |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RB-TBQ4-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RB-TBQ3-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RB-QJL4-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RB-QJL3-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RB-POLAR4-SYM | Measured 3 | Included |  |  |  |  |  |  |
| RB-POLAR3-SYM | Measured 3 | Included |  |  |  |  |  |  |

# 6. Formal system/resource fields

| Configuration/rep | Total generation ms | Input tokens | Output tokens | RAM before MB | Min available RAM MB | RAM after MB | K MB | V MB | CPU mean/peak | Device/placement | Exit/stability |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |  |

# 7. P1-P6 deterministic and rubric evaluation

Five dimensions are mandatory: correctness, instruction following, relevance/completeness, clarity/structure, and safety/faithfulness. Deterministic failure and critical caps cannot be overridden by a high subjective score. Both blinded pairwise presentation orders are retained.

| Configuration | Prompt | Raw-output SHA-256 | Deterministic result | Correctness | Instruction | Relevance | Clarity | Safety |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RA-SCALAR-U8-SYM | P1 |  |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | P2 |  |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | P3 |  |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | P4 |  |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | P5 |  |  |  |  |  |  |  |
| RA-SCALAR-U8-SYM | P6 |  |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | P1 |  |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | P2 |  |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | P3 |  |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | P4 |  |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | P5 |  |  |  |  |  |  |  |
| RA-SCALAR-U4-SYM | P6 |  |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | P1 |  |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | P2 |  |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | P3 |  |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | P4 |  |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | P5 |  |  |  |  |  |  |  |
| RA-TURBO-U4-SYM | P6 |  |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | P1 |  |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | P2 |  |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | P3 |  |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | P4 |  |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | P5 |  |  |  |  |  |  |  |
| RA-TURBO-U3-SYM | P6 |  |  |  |  |  |  |  |
| RB-TBQ4-SYM | P1 |  |  |  |  |  |  |  |
| RB-TBQ4-SYM | P2 |  |  |  |  |  |  |  |
| RB-TBQ4-SYM | P3 |  |  |  |  |  |  |  |
| RB-TBQ4-SYM | P4 |  |  |  |  |  |  |  |
| RB-TBQ4-SYM | P5 |  |  |  |  |  |  |  |
| RB-TBQ4-SYM | P6 |  |  |  |  |  |  |  |
| RB-TBQ3-SYM | P1 |  |  |  |  |  |  |  |
| RB-TBQ3-SYM | P2 |  |  |  |  |  |  |  |
| RB-TBQ3-SYM | P3 |  |  |  |  |  |  |  |
| RB-TBQ3-SYM | P4 |  |  |  |  |  |  |  |
| RB-TBQ3-SYM | P5 |  |  |  |  |  |  |  |
| RB-TBQ3-SYM | P6 |  |  |  |  |  |  |  |
| RB-QJL4-SYM | P1 |  |  |  |  |  |  |  |
| RB-QJL4-SYM | P2 |  |  |  |  |  |  |  |
| RB-QJL4-SYM | P3 |  |  |  |  |  |  |  |
| RB-QJL4-SYM | P4 |  |  |  |  |  |  |  |
| RB-QJL4-SYM | P5 |  |  |  |  |  |  |  |
| RB-QJL4-SYM | P6 |  |  |  |  |  |  |  |
| RB-QJL3-SYM | P1 |  |  |  |  |  |  |  |
| RB-QJL3-SYM | P2 |  |  |  |  |  |  |  |
| RB-QJL3-SYM | P3 |  |  |  |  |  |  |  |
| RB-QJL3-SYM | P4 |  |  |  |  |  |  |  |
| RB-QJL3-SYM | P5 |  |  |  |  |  |  |  |
| RB-QJL3-SYM | P6 |  |  |  |  |  |  |  |
| RB-POLAR4-SYM | P1 |  |  |  |  |  |  |  |
| RB-POLAR4-SYM | P2 |  |  |  |  |  |  |  |
| RB-POLAR4-SYM | P3 |  |  |  |  |  |  |  |
| RB-POLAR4-SYM | P4 |  |  |  |  |  |  |  |
| RB-POLAR4-SYM | P5 |  |  |  |  |  |  |  |
| RB-POLAR4-SYM | P6 |  |  |  |  |  |  |  |
| RB-POLAR3-SYM | P1 |  |  |  |  |  |  |  |
| RB-POLAR3-SYM | P2 |  |  |  |  |  |  |  |
| RB-POLAR3-SYM | P3 |  |  |  |  |  |  |  |
| RB-POLAR3-SYM | P4 |  |  |  |  |  |  |  |
| RB-POLAR3-SYM | P5 |  |  |  |  |  |  |  |
| RB-POLAR3-SYM | P6 |  |  |  |  |  |  |  |

# 8. Quality, perplexity and matched-baseline summary

| Configuration | Overall quality /10 | Matched baseline | Quality delta | Material degradation? | Perplexity | Perplexity delta | Adjudication/evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

# 9. Formal metric summary

| Configuration | Common context | Median TTFT (range) | Median prompt tok/s (range) | Median TPOT (range) | Median decode tok/s (range) | Median peak private MB (range) | K/V/total MB | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |

**E1 acceptance rule:** every admitted symmetric configuration has a bounded frontier and either matched independently validated formal results or an explicit blocker. No average may hide a failed prompt or a missing repetition.
