# Upstream llama.cpp Complete Measurement Design

## Objective

Replace every `Not measured` resource/perceived-latency field in WB-01 with repeatable measurements for UL-01 through UL-13, correct the UL-01 comparison basis, and record UL-13 as a project-workload pass while retaining the three failing upstream SYCL edge tests.

## Measurement contract

- Run each formal configuration three independent times after one unrecorded warm-up.
- Peak RAM MB is the maximum aggregate physical working set of the launched llama process and its descendants, sampled at no more than 100 ms intervals.
- KV MB is parsed from llama's runtime allocation line. If separate K/V buffers are reported, record their sum and preserve both raw values.
- TTFT ms is monotonic elapsed time from HTTP request initiation to the first streamed generated token from an already-ready `llama-server`. It includes request transfer, tokenization and prefill and excludes model loading.
- Store every raw sample, command, stdout, stderr, exit code, device identity, and a JSON summary under the corresponding UL evidence directory.
- Workbook cells contain the median of three valid samples. Failed samples are retained and rerun under a new sample ID; they are never silently discarded.

## Correctness rules

- The samplers must prove process-tree aggregation, peak selection, KV parsing, request-initiation-to-first-streamed-token timing, median calculation, and failed-run handling with automated tests before formal use.
- Formal commands retain each row's pinned model, context, KV formats, device, layer count, seed, sampling settings, and llama.cpp commit.
- UL-01 is a Gemma 1B Q4_K_M diagnostic and is not described as the highest-precision row. It receives P1-P4 testing before any cross-row quality comparison.
- UL-13 passes the project workload only if the formal SYCL benchmark and P1-P4 generation complete on `SYCL0: Intel UHD Graphics`. The separate 49/52 upstream repository-suite result remains visible as a backend limitation.

## Deliverables

- Tested measurement harness and tests.
- Three measured samples for every executable UL row.
- Corrected processed-results summary and WB-01 Markdown.
- Updated revision register, manifest, generated DOCX, and validation evidence.

## Error handling

The harness times out a stalled child, captures its descendants and logs, marks the sample invalid, and leaves all partial evidence intact. A missing KV allocation line or first response byte is a failed measurement, not zero. Device mismatch invalidates GPU samples.

## Validation

Run harness unit tests, validate all summary schemas and three-sample medians, scan WB-01 for blank or `Not measured` cells, regenerate the DOCX, run workbook revision/workspace validators, and perform document render QA where the installed toolchain permits it.
