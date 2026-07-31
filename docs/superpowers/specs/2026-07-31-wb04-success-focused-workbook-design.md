# WB-04 Success-Focused Workbook v1.8 Design

**Status:** Approved in principle by the user on 2026-07-31; written specification awaiting final review.

## Objective

Replace the dense WB-04 v1.7 presentation with one readable controlled v1.8 workbook that foregrounds verified successes, reports unsuccessful tests only as a compact ID-and-reason list, and preserves the complete governed evidence outside the visible workbook.

The document must look and read like WB-01 through WB-03: short factual sections, compact aggregate tables, rounded display values, concise practical interpretations, and no repeated per-sample evidence tables.

## Integrity boundary

The presentation may become smaller, but the evidence controls must not become weaker.

- The governed release evidence remains complete: 60 controlled IDs, 36 formal runtime configurations, 33 quality outcomes, three accepted runtime measurements, seven expected-rejection controls, and 26 terminal or not-launched runtime outcomes.
- The release-evidence builder and reconciler must continue to validate every controlled record, provenance hash, campaign identity, sample count, fallback state, and terminal classification before any presentation rows are rendered.
- Raw attempts, per-sample metrics, terminal classifications, exact values, paths, and SHA-256 hashes remain in `experiments/raw-results/openvino-turboquant/2026-07-30/`.
- The visible workbook must never say that all tests passed, that a quality score exists, or that a quality winner was selected.
- A displayed numeric result is admissible only when the reconciled row has `accepted=true`, `status=measured`, three accepted samples, `fallback=false`, complete aggregate metrics, and a matching evidence hash.

## Controlled revision

- Workbook version: `1.8`
- Revision-register record: `WR-036`
- Supersedes: `1.7` / `WR-035`
- Controlled filename remains `04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`.
- The canonical Markdown, generated DOCX, manifest hashes, revision register, and completion register must be regenerated and synchronized.

## Reader-facing structure

### 1. Repository, runtime, and host

Use a compact two-column facts table containing only information needed to interpret the results: patched OpenVINO/GenAI identity, frozen evidence identity, Granite artifact scope, Lenovo/Intel host, installed RAM, Intel UHD shared-memory GPU, and operating system.

### 2. Successful build and recovery checks

Show only verified positive gates:

- OV-B01, OV-B02, OV-B03, OV-B05, OV-B06, and OV-B07.
- Official OpenVINO GenAI source suite: 505/505 tests passed.
- CPU allocation observer discovery: 46 tests.
- CPU allocation observer run 1: 46/46.
- CPU allocation observer run 2: 46/46.
- Observer-enabled CPU plugin, CPU functional binary, `query_state()` check, and invalid-destination fail-closed check.

These checks must be labelled as build or recovery evidence, not formal Granite benchmark results.

### 3. Successful bounded diagnostics

Show OV-C02 and OV-C03 in one short table:

- U8 CPU STANDARD diagnostic: valid output, seven input tokens, four generated tokens, `fallback=false`, cleanup verified, concrete K/V state `f32/f32`.
- U4 CPU STANDARD diagnostic: valid output, seven input tokens, four generated tokens, `fallback=false`, cleanup verified, concrete K/V state `f32/f32`.

The table must explicitly say that these are diagnostic-only weight-artifact successes, not formal performance, scalar K/V, or quality passes.

### 4. Successful expected-rejection controls

Group the successful negative controls into no more than three concise rows:

- Scalar-state controls: OV-04/4096, OV-05/4096, OV-TQ-01/4096, and OV-TQ-02/4096 correctly rejected scalar K/V claims after concrete `f32/f32` state was observed.
- Property-boundary controls: OV-TQS-05 through OV-TQS-12 correctly rejected unsupported scalar/TurboQuant combinations.
- Device/codec controls: OV-TQ-18/1024, OV-TQ-19/256, OV-TQ-20/256, and OV-B11 correctly rejected GPU TurboQuant, QJL, and Polar properties before generation.

These rows are successful boundary checks, not benchmark or quality results, and must contain no invented numeric metrics.

### 5. Accepted formal runtime measurements

The visible formal results contain exactly these three rows:

1. OV-TQ-13, context 512, Granite 3B U8, CPU, TBQ4/TBQ4.
2. OV-TQ-14, context 512, Granite 3B U8, CPU, TBQ3/TBQ3.
3. OV-TQ-14, context 2048, Granite 3B U8, CPU, TBQ3/TBQ3.

All three rows must show three accepted samples, CPU execution, `fallback=false`, and verified cleanup.

To retain all important measured statistics without a single unmanageably wide table, use three compact three-row tables.

#### 5.1 Timing and throughput

Columns: test ID, context, load milliseconds, TTFT milliseconds, prompt tokens/second, TPOT milliseconds, decode tokens/second, generation duration milliseconds.

#### 5.2 Memory

Columns: test ID, context, peak working set MiB, peak private memory MiB, minimum available RAM MiB, actual K/V allocation MiB, cleanup result.

#### 5.3 Utilisation and run integrity

Columns: test ID, context, CPU mean/median/peak percent, GPU mean/median/peak percent, CPU sample count, GPU sample count, accepted run count, fallback count, evidence reference.

Displayed values are rounded consistently to two or three decimal places. Exact source values remain authoritative in the governed JSON summaries. Evidence references use short labels `E1`, `E2`, and `E3` rather than long paths inside metric tables.

### 6. Tests that did not complete

Use a short bulleted list, not a large result table. Every bullet contains test IDs followed by one primary reason.

- Diagnostic-only/no formal benchmark: OV-01/1024.
- Missing validated FP16 artifact: OV-C01 and OV-02/2048.
- RAM safety floor reached locally: OV-03/4096, OV-06/4096, OV-TQ-13/2048, OV-TQ-03 through OV-TQ-12 at 4096, OV-TQ-13 at 4096/8192, OV-TQ-14 at 4096/8192, and OV-TQ-15/4096.
- Larger host required: OV-C04 through OV-C06, OV-07 through OV-10, OV-TQ-16/4096, and OV-TQ-17/4096.
- Strict activation proof incomplete: OV-B08 through OV-B10, OV-B12, and OV-TQS-01 through OV-TQS-04 lacked the required unambiguous CP1/K/V-state proof.
- Governed quality campaign incomplete: the three measured runtime rows produced no complete P1-P6 response set because the quality worker reached the fixed RAM floor.

The list may state that full terminal evidence is preserved in the governed evidence package, but it must not reproduce per-attempt logs or long hashes.

### 7. Quality boundary

Use a short prominent note:

> Runtime measurements completed for three configurations. No governed P1-P6 quality campaign completed, so WB-04 has no numeric quality score, no quality-qualified pass, and no winner.

Do not show the former 33-row quality-terminal table.

### 8. Final decision and evidence index

Use a compact two-column `Field | Record` decision table covering:

- Proven runtime scope.
- Best observed runtime facts without declaring a quality winner.
- CPU-only and no-fallback boundary.
- No accepted GPU measurement.
- No non-TurboQuant formal benchmark.
- Recommended continuation on a higher-memory host.

Follow it with a narrow evidence index:

- E1: OV-TQ-13/512 measurement summary and SHA-256.
- E2: OV-TQ-14/512 measurement summary and SHA-256.
- E3: OV-TQ-14/2048 measurement summary and SHA-256.
- Full governed evidence root and reconciliation-input hash.

## Presentation rules

- Reuse the established WB-01 through WB-03 visual system: landscape Letter, Aptos/Aptos Display, blue headings, blue striped tables, repeated headers, and concise two-column summaries.
- Keep prose short and factual.
- Use no per-sample tables in the visible workbook.
- Use no long evidence paths in wide metric tables.
- Use real bullet paragraphs for the incomplete-test list.
- Keep status language precise: `runtime measured`, `diagnostic-only`, or `passed expected rejection`; never shorten these to an unconditional `passed` where quality did not complete.
- Preserve all important aggregate timing, memory, K/V, CPU, GPU, cleanup, sample-count, and fallback metrics for the three measured rows.

## Implementation architecture

1. Keep the full release-evidence production and provenance validation unchanged in scope.
2. Refactor WB-04 finalization so full reconciliation happens before presentation filtering.
3. Introduce an explicit presentation selector derived from reconciled outcomes, not a hand-maintained list of arbitrary numeric values.
4. Render the v1.8 success-focused Markdown sections from the validated selection.
5. Replace the old visible-all-60-ID rule with two independent gates:
   - evidence gate: full 60-ID/36-runtime/33-quality reconciliation remains mandatory;
   - presentation gate: exactly three numeric measured rows, grouped expected rejections, the complete compact incomplete-test list, and the quality disclosure must be visible.
6. Update the DOCX release audit for v1.8 structure, table count, zero blank cells, revision history, ZIP integrity, and prohibited-claim checks.
7. Regenerate the controlled DOCX and synchronize the three CSV registers.

## Verification requirements

- Unit tests first for the presentation selector, three-row metrics rendering, failed-test grouping, quality disclosure, and continued full-evidence reconciliation.
- Full `scripts/testing/tests` suite must pass.
- The finalizer check-only gate must accept the full evidence package.
- The DOCX structural release audit must pass with zero blank table cells, the expected v1.8 table count, visible `WR-036` revision, and valid ZIP structure.
- Manifest Markdown and DOCX SHA-256 values must match the generated files.
- WB-04 rows in the revision, manifest, and completion registers must have no blank fields.
- `git diff --check` must pass.
- Attempt DOCX rendering with the packaged renderer. If LibreOffice remains unavailable, perform structural DOCX QA and explicitly disclose that visual page rendering could not be completed.

## Non-goals

- Do not rerun model workloads.
- Do not invent or infer a quality score.
- Do not delete raw evidence or terminal classifications.
- Do not weaken provenance validation to make the smaller presentation pass.
- Do not change WB-01, WB-02, WB-03, WB-05, or WB-06 content.
