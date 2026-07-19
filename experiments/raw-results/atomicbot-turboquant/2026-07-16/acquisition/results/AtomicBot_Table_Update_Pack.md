# AtomicBot Workbook Table Update Pack

## Repository and environment record
- Repository URL: https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant
- Branch/tag: DETACHED
- Pinned commit: 519f0c594a8e31467d2e2f2cf17054c9e7e11536
- Upstream llama.cpp base: See AB-Source-Audit.md; do not guess when history cannot establish it.
- Exposed formats: turbo2 / turbo3 / turbo4 â€” runtime activation status: True
- Overall status: Experimental

## Build and setup checklist
- AB-B01: Repository cloned, pinned and detached. See AtomicBot_Master_Summary.json and build logs.
- AB-B02: Clean Windows x64 CPU configuration. See AtomicBot_Master_Summary.json and build logs.
- AB-B03: Required tools built. See AtomicBot_Master_Summary.json and build logs.
- AB-B04: Repository-provided tests. See AtomicBot_Master_Summary.json and build logs.
- AB-B05: Vulkan build and device probe. See AtomicBot_Master_Summary.json and build logs.
- AB-B06: Generic SYCL build gate only; TurboQuant SYCL requires separate source support. See AtomicBot_Master_Summary.json and build logs.
- AB-B07: Cache flags and turbo strings. See AtomicBot_Master_Summary.json and build logs.
- AB-B08: Warnings and Intel limitations recorded. See AtomicBot_Master_Summary.json and build logs.

## Ordered test matrix and formal results

| Test | Model | Cache | Device | Context | Peak RAM MiB | KV MiB | TTFT ms | tok/s | Status |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| AB-01 | diagnostic | f16/f16 | cpu ngl=0 | 1024 | 930.449 | 26.0 | 2465.908 | 37.5 | Pass |
| AB-02 | diagnostic | turbo3/turbo3 | cpu ngl=0 | 1024 | 909.363 | 5.33 | 2885.978 | 30.8 | Pass |
| AB-03 | granite3b | f16/f16 | cpu ngl=0 | 2048 | 3662.859 | 160.0 | 5692.474 | 15.9 | Pass |
| AB-KV3-F16-4K | granite3b | f16/f16 | cpu ngl=0 | 4096 | 3822.984 | 320.0 | 5976.146 | 15.1 | Pass |
| AB-04 | granite3b | q8_0/q8_0 | cpu ngl=0 | 4096 | 3672.949 | 170.0 | 5396.835 | 15.9 | Pass |
| AB-05 | granite3b | turbo4/turbo4 | cpu ngl=0 | 4096 | 3675.57 | 170.13 | 6743.169 | 11.8 | Pass |
| AB-06 | granite3b | turbo3/turbo3 | cpu ngl=0 | 4096 | 3629.305 | 125.13 | 6455.344 | 11.8 | Pass |
| AB-07 | granite3b | turbo2/turbo2 | cpu ngl=0 | 4096 | 3593.484 | 89.38 | 7165.629 | 13.1 | Pass |
| AB-08F | granite8b | f16/f16 | cpu ngl=0 | 2048 | 8923.32 | 320.0 | 13624.632 | 6.8 | Pass |
| AB-KV8-F16-4K | granite8b | f16/f16 | cpu ngl=0 | 4096 |  |  |  |  | Research-only |
| AB-08Q | granite8b | q8_0/q8_0 | cpu ngl=0 | 4096 | 8943.141 | 340.0 | 13107.302 | 6.8 | Pass |
| AB-09 | granite8b | turbo4/turbo4 | cpu ngl=0 | 4096 | 8774.609 | 170.13 | 14847.131 | 5.9 | Pass |
| AB-10 | granite8b | turbo3/turbo3 | cpu ngl=0 | 4096 | 8728.656 | 125.13 | 15698.596 | 5.9 | Pass |
| AB-11 | granite3b | f16/f16 | vulkan ngl=1 | 4096 | 2906.387 | 320.0 | 5947.408 | 14.0 | Pass |
| AB-12 | granite3b | turbo3/turbo3 | vulkan ngl=1 | 4096 | 2756.082 | 125.13 | 8701.077 | 9.6 | Pass |
| AB-13 | granite3b | turbo3/turbo3 | vulkan ngl=999 | 4096 | 4540.41 | 125.13 | 10195.181 | 7.6 | Pass |
| AB-14 | granite8b | q8_0/q8_0 | vulkan ngl=1 | 4096 | 6076.316 | 340.0 | 12542.331 | 6.0 | Pass |
| AB-15 | granite8b | turbo3/turbo3 | vulkan ngl=1 | 4096 | 5918.672 | 125.13 | 12892.795 | 5.4 | Pass |
| AB-15M | granite8b | turbo3/turbo3 | vulkan ngl=999 | 4096 |  |  |  |  | Research-only |

## Device and fallback verification
Use each GPU test's formal summary and device evidence. A pass requires Vulkan0 KV placement, requested model offload and non-zero GPU samples. Any unsupported-operation or CPU fallback message prevents native-GPU classification.

## Quality and context
Copy rows from AtomicBot_Quality_Summary.csv. Manual semantic scoring remains required.

## Failure log
Copy AtomicBot_Failure_Log.csv.

## Final repository/runtime decision
- TurboQuant implementation class: Full candidate â€” requires runtime activation proof
- Granite 3B TurboQuant: Pass
- Granite 8B TurboQuant: Pass
- Silent fallback detected: Review failure log and GPU summaries
- Final status: Experimental
- Main evidence path: C:\Users\Student\atomicbot-retest-20260716\results\atomicbot\AtomicBot_Master_Summary.json
- Final reasoning: See AtomicBot_Final_Report.md.
