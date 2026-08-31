# Upstream llama.cpp Final Test Report

| Document control | Value |
| --- | --- |
| Route ID | upstream-llama-cpp |
| Revision | R1 |
| Generated date | 2026-07-16 |
| Evidence IDs | upstream-llama-cpp-1bf6d02f2d93, upstream-llama-cpp-27c47275b8fc, upstream-llama-cpp-3a280ff9538d, upstream-llama-cpp-9ba512818e81, upstream-llama-cpp-a36016f66e02, upstream-llama-cpp-db711113b8e9 |

## 1. Title and document control

### DC-01 — Document identity and authority

| Field | Value |
| --- | --- |
| Route | upstream-llama-cpp |
| Campaign | wb-01-v1.4-2026-07-16 |
| Controlled source | WB-01 v1.4 |
| Pinned upstream commit | 2d973636e292ee6f75fadcf08d29cb33511f509f |
| Canonical report | workbook/source/upstream-llama-cpp-final-report.md |

WB-01 v1.4, its controlled evidence index, and the indexed upstream log tree are the authority. Later empty register fields and a README-only raw-results folder are not observations.

## 2. Technical summary

All 13 intended project workloads completed. The result is a conditional pass because the broader SYCL repository suite retained three edge-case failures even though UL-13 project inference passed.

### AC-01 — Attempt status summary

| Status | Count |
| --- | --- |
| Passed | 13 |

> Note: Historical missing values are displayed as Not collected; they are never converted to zero.

## 3. Key findings and decision-relevant evidence

Recommendation labels are deliberately narrow and describe only this observed campaign.

### KF-01 — Bounded decision labels

| Role | Test ID | Boundary |
| --- | --- | --- |
| best observed CPU | UL-08 | UL-08 only; observed campaign label |
| best observed Intel GPU | UL-10 | UL-10 only; observed campaign label |
| recorded fallback | UL-05 | UL-05 only; recorded fallback label |

*These labels do not establish causal or universal superiority.*

## 4. Repository, branch, commit, build, hardware, and software identity

Identity values below are retained from WB-01 rather than inferred from the reviewing machine.

### ID-01 — Repository and system identity

| Field | Value |
| --- | --- |
| url | https://github.com/ggml-org/llama.cpp |
| tag | b9870 |
| commit | 2d973636e292ee6f75fadcf08d29cb33511f509f |
| workbook_id | WB-01 |
| workbook_revision | 1.4 |
| source_date | 2026-07-16 |
| best_observed_cpu_test_id | UL-08 |
| best_observed_intel_gpu_test_id | UL-10 |
| recorded_fallback_test_id | UL-05 |
| recommendation_scope | UL-08 only as best observed CPU; UL-10 only as best observed Intel GPU; UL-05 only as the recorded fallback. |
| raw_results_status | Not collected |
| raw_results_reason | README placeholder only; no raw result observations |
| performance_register_status | Not collected |
| performance_register_reason | No UL-01 through UL-13 rows; repetition evidence predates the register |
| historical_missing_metric_display | Not collected |
| setup_scope_ids | ['UL-B01', 'UL-B02', 'UL-B03', 'UL-B04', 'UL-B05', 'UL-B06', 'UL-B07'] |
| machine_id | LENOVO-PF4HMD0T |
| cpu | 12th Gen Intel Core i5-12450H; 8 cores; 12 logical processors |
| ram_bytes | 16857817088 |
| gpu | Intel UHD Graphics; driver 32.0.101.7076 |
| npu | Not applicable |
| gpu_dedicated_memory | Not collected |
| os | Windows 11 Home 10.0.26200 build 26200, 64-bit |
| compiler | MSVC via Visual Studio 18 2026 |
| cmake | 4.3.1-msvc1 |
| vulkan_sdk | 1.4.350.0 |
| sycl | Intel oneAPI 2026.1 SYCL |
| performance_register_rows | 0 |

## 5. Objectives, scope, test matrix, and execution sequence

The intended sequence moved from CPU precision/cache baselines through Vulkan partial/full offload and finally the SYCL project workload.

### MX-01 — Intended matrix

| Test ID | Model | Weight | Cache | Backend | Source status |
| --- | --- | --- | --- | --- | --- |
| UL-01 | gemma-3-1b-diagnostic | q4_k_m | f16-f16 | cpu | Passed |
| UL-02 | granite-4.1-3b | bf16 | f16-f16 | cpu | Passed |
| UL-03 | granite-4.1-3b | q8_0 | f16-f16 | cpu | Passed |
| UL-04 | granite-4.1-3b | q4_k_m | f16-f16 | cpu | Passed |
| UL-05 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | cpu | Passed with quality caveats |
| UL-06 | granite-4.1-8b | q8_0 | f16-f16 | cpu | Passed |
| UL-07 | granite-4.1-8b | q4_k_m | f16-f16 | cpu | Passed with quality caveat |
| UL-08 | granite-4.1-8b | q4_k_m | q8_0-q8_0 | cpu | Passed with quality caveats |
| UL-09 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | vulkan | Passed |
| UL-10 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | vulkan | Passed with performance regression |
| UL-11 | granite-4.1-8b | q4_k_m | q8_0-q8_0 | vulkan | Passed |
| UL-12 | granite-4.1-8b | q4_k_m | q8_0-q8_0 | vulkan | Passed with performance regression |
| UL-13 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | sycl | Passed with edge-suite limitation |

## 6. Model, weight, cache-format, and backend availability

Availability is represented by the 13 completed matrix rows. No raw-results observations or absent model combinations were invented.

### AV-01 — Observed model/backend availability

| Test ID | Model | Weight | Cache | Backend | Status |
| --- | --- | --- | --- | --- | --- |
| UL-01 | gemma-3-1b-diagnostic | q4_k_m | f16-f16 | cpu | Passed |
| UL-02 | granite-4.1-3b | bf16 | f16-f16 | cpu | Passed |
| UL-03 | granite-4.1-3b | q8_0 | f16-f16 | cpu | Passed |
| UL-04 | granite-4.1-3b | q4_k_m | f16-f16 | cpu | Passed |
| UL-05 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | cpu | Passed |
| UL-06 | granite-4.1-8b | q8_0 | f16-f16 | cpu | Passed |
| UL-07 | granite-4.1-8b | q4_k_m | f16-f16 | cpu | Passed |
| UL-08 | granite-4.1-8b | q4_k_m | q8_0-q8_0 | cpu | Passed |
| UL-09 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | vulkan | Passed |
| UL-10 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | vulkan | Passed |
| UL-11 | granite-4.1-8b | q4_k_m | q8_0-q8_0 | vulkan | Passed |
| UL-12 | granite-4.1-8b | q4_k_m | q8_0-q8_0 | vulkan | Passed |
| UL-13 | granite-4.1-3b | q4_k_m | q8_0-q8_0 | sycl | Passed |

## 7. Complete attempt accounting

Each intended workload appears exactly once in the normalized terminal ledger.

### AT-01 — Complete attempt accounting

| Test ID | Attempt ID | Executed | Status | Source status | Evidence IDs |
| --- | --- | --- | --- | --- | --- |
| UL-01 | UL-01--attempt-001 | True | Passed | Passed | EVID-aea1bd923757ec615910, EVID-542379703687899894d2, EVID-d880347742015fc025fb, EVID-61e121c613348e578f5a |
| UL-02 | UL-02--attempt-001 | True | Passed | Passed | EVID-4dc08d3a2bdda40ad9e7, EVID-1b295a270e8a6304f13b, EVID-6c19cd913b4347b2c9f9, EVID-0dc8aa686dae5140d31c |
| UL-03 | UL-03--attempt-001 | True | Passed | Passed | EVID-79bb6ff9b451c38f0778, EVID-f26edcb1ffa2a6c2e28f, EVID-fa49bead0b5993bf34b7, EVID-e6aee1caf0abb9964914 |
| UL-04 | UL-04--attempt-001 | True | Passed | Passed | EVID-2e060658a2dc3b4e3365, EVID-687f1fc2a369f8dad883, EVID-4ef3717d9c1f93c8df75, EVID-abda4f1d29fe9a36303f |
| UL-05 | UL-05--attempt-001 | True | Passed | Passed with quality caveats | EVID-971776b67f432946b411, EVID-007d654aa76d80d4b6ab, EVID-8af62d2b05d8e93dcb51, EVID-62af25b8ed4b321850d7 |
| UL-06 | UL-06--attempt-001 | True | Passed | Passed | EVID-d8c13363b2602c8a66e0, EVID-fd0224096c5549d58f14, EVID-519ce4e0d1680278ec11, EVID-c3845e7624a55f94b53e |
| UL-07 | UL-07--attempt-001 | True | Passed | Passed with quality caveat | EVID-7d01cf406e852f9deffb, EVID-4e09787a7389b242c5b1, EVID-f7ad83e1b0b937e4628b, EVID-87a9a5c5ccdbc3c16734 |
| UL-08 | UL-08--attempt-001 | True | Passed | Passed with quality caveats | EVID-398081adc25b883b15a7, EVID-a8f5bcd3955b33cb5a76, EVID-8d0aa3ac45f9b6f001dc, EVID-4fa6112c29b541b2c7ce |
| UL-09 | UL-09--attempt-001 | True | Passed | Passed | EVID-65a3a7e31b728dabc7b5, EVID-dde39454f9d6bb983684, EVID-7207ba6e4af8412844ca, EVID-41758af920b6764091f7 |
| UL-10 | UL-10--attempt-001 | True | Passed | Passed with performance regression | EVID-922fe80006d168c5c124, EVID-97bca65896e91b10fbf1, EVID-ca3579ea633d77ba2f8c, EVID-cd445466f7ba4301f982 |
| UL-11 | UL-11--attempt-001 | True | Passed | Passed | EVID-31d12cddb482b9a98116, EVID-2fe63e5fb08e2d2921a4, EVID-b1cb35f6efebf4ba7c70, EVID-cccd0e0ded6e7ee89b16 |
| UL-12 | UL-12--attempt-001 | True | Passed | Passed with performance regression | EVID-06a4562128adf7b60626, EVID-a3610920c58945bbd9a8, EVID-3ea09db4a5d6a21e8569, EVID-08efec7d9b73b0368e72 |
| UL-13 | UL-13--attempt-001 | True | Passed | Passed with edge-suite limitation | EVID-d335afb5774f9fb254b4, EVID-c254e8693d31edba0dd2, EVID-2935db523c5521649574, EVID-f093cb37e7e80ffb6e7e |

## 8. Performance results and repetition detail

All included source observations are expanded. Prompt/decode samples come from llama-bench (three per row, except five for UL-13); TTFT and process-tree memory come from three llama-server samples per row. The two series are not falsely represented as one synchronized run.

### PF-01 — Performance summaries

| Test ID | Prompt tok/s | Decode tok/s | TTFT ms | Peak bytes | KV bytes |
| --- | --- | --- | --- | --- | --- |
| UL-01 | 96.324 | 37.191 | 602.99 | 981467136 | 27262976 |
| UL-02 | 28.676 | 6.353 | 1430.20 | 7038418944 | 167772160 |
| UL-03 | 20.642 | 11.393 | 2394.29 | 4015845376 | 335544320 |
| UL-04 | 33.993 | 15.979 | 945.70 | 4014776320 | 335544320 |
| UL-05 | 15.181 | 15.771 | 939.58 | 3857338368 | 178257920 |
| UL-06 | 9.806 | 4.703 | 7246.13 | 9311297536 | 335544320 |
| UL-07 | 16.189 | 7.087 | 3172.40 | 9700397056 | 671088640 |
| UL-08 | 9.294 | 7.092 | 2978.72 | 9384980480 | 356515840 |
| UL-09 | 79.856 | 15.214 | 1677.30 | 2682462208 | 178257920 |
| UL-10 | 84.497 | 9.632 | 1044.02 | 4556427264 | 178257920 |
| UL-11 | 31.794 | 6.219 | 3339.78 | 6053822464 | 356515840 |
| UL-12 | 33.785 | 4.385 | 2492.26 | 10809311232 | 356515840 |
| UL-13 | 40.844 | 7.212 | 2314.26 | 5531197440 | 178257920 |

### RP-01 — Included repetition detail

| Test ID | Repetition | Prompt tok/s | Decode tok/s | TTFT ms | Peak bytes | Input tokens | Output tokens | Evidence ID |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UL-01 | bench-001 | 120.63900 | 37.13540 | Not collected | Not collected | Not collected | Not collected | EVID-aea1bd923757ec615910 |
| UL-01 | bench-002 | 96.32430 | 37.19110 | Not collected | Not collected | Not collected | Not collected | EVID-aea1bd923757ec615910 |
| UL-01 | bench-003 | 88.74770 | 37.23930 | Not collected | Not collected | Not collected | Not collected | EVID-aea1bd923757ec615910 |
| UL-01 | server-001 | Not collected | Not collected | 627.5466 | 981467136 | Not collected | Not collected | EVID-542379703687899894d2 |
| UL-01 | server-002 | Not collected | Not collected | 597.3746 | 981577728 | Not collected | Not collected | EVID-d880347742015fc025fb |
| UL-01 | server-003 | Not collected | Not collected | 602.9852 | 981413888 | Not collected | Not collected | EVID-61e121c613348e578f5a |
| UL-02 | bench-001 | 28.67570 | 6.41507 | Not collected | Not collected | Not collected | Not collected | EVID-4dc08d3a2bdda40ad9e7 |
| UL-02 | bench-002 | 28.89510 | 6.35260 | Not collected | Not collected | Not collected | Not collected | EVID-4dc08d3a2bdda40ad9e7 |
| UL-02 | bench-003 | 28.64230 | 6.27687 | Not collected | Not collected | Not collected | Not collected | EVID-4dc08d3a2bdda40ad9e7 |
| UL-02 | server-001 | Not collected | Not collected | 1489.5014 | 7038459904 | Not collected | Not collected | EVID-1b295a270e8a6304f13b |
| UL-02 | server-002 | Not collected | Not collected | 1430.2001 | 7038418944 | Not collected | Not collected | EVID-6c19cd913b4347b2c9f9 |
| UL-02 | server-003 | Not collected | Not collected | 1371.8886 | 7038058496 | Not collected | Not collected | EVID-0dc8aa686dae5140d31c |
| UL-03 | bench-001 | 20.64250 | 11.12860 | Not collected | Not collected | Not collected | Not collected | EVID-79bb6ff9b451c38f0778 |
| UL-03 | bench-002 | 20.64540 | 11.47020 | Not collected | Not collected | Not collected | Not collected | EVID-79bb6ff9b451c38f0778 |
| UL-03 | bench-003 | 20.54190 | 11.39260 | Not collected | Not collected | Not collected | Not collected | EVID-79bb6ff9b451c38f0778 |
| UL-03 | server-001 | Not collected | Not collected | 2414.5663 | 4015755264 | Not collected | Not collected | EVID-f26edcb1ffa2a6c2e28f |
| UL-03 | server-002 | Not collected | Not collected | 2328.6925 | 4016193536 | Not collected | Not collected | EVID-fa49bead0b5993bf34b7 |
| UL-03 | server-003 | Not collected | Not collected | 2394.2935 | 4015845376 | Not collected | Not collected | EVID-e6aee1caf0abb9964914 |
| UL-04 | bench-001 | 34.02450 | 15.83760 | Not collected | Not collected | Not collected | Not collected | EVID-2e060658a2dc3b4e3365 |
| UL-04 | bench-002 | 33.96960 | 15.97910 | Not collected | Not collected | Not collected | Not collected | EVID-2e060658a2dc3b4e3365 |
| UL-04 | bench-003 | 33.99280 | 15.99570 | Not collected | Not collected | Not collected | Not collected | EVID-2e060658a2dc3b4e3365 |
| UL-04 | server-001 | Not collected | Not collected | 1047.2666 | 4014850048 | Not collected | Not collected | EVID-687f1fc2a369f8dad883 |
| UL-04 | server-002 | Not collected | Not collected | 945.6951 | 4014563328 | Not collected | Not collected | EVID-4ef3717d9c1f93c8df75 |
| UL-04 | server-003 | Not collected | Not collected | 931.9757 | 4014776320 | Not collected | Not collected | EVID-abda4f1d29fe9a36303f |
| UL-05 | bench-001 | 15.22770 | 15.45440 | Not collected | Not collected | Not collected | Not collected | EVID-971776b67f432946b411 |
| UL-05 | bench-002 | 15.17750 | 15.77110 | Not collected | Not collected | Not collected | Not collected | EVID-971776b67f432946b411 |
| UL-05 | bench-003 | 15.18060 | 15.79110 | Not collected | Not collected | Not collected | Not collected | EVID-971776b67f432946b411 |
| UL-05 | server-001 | Not collected | Not collected | 939.5772 | 3857244160 | Not collected | Not collected | EVID-007d654aa76d80d4b6ab |
| UL-05 | server-002 | Not collected | Not collected | 912.7687 | 3857338368 | Not collected | Not collected | EVID-8af62d2b05d8e93dcb51 |
| UL-05 | server-003 | Not collected | Not collected | 964.3897 | 3857350656 | Not collected | Not collected | EVID-62af25b8ed4b321850d7 |
| UL-06 | bench-001 | 9.90098 | 4.68777 | Not collected | Not collected | Not collected | Not collected | EVID-d8c13363b2602c8a66e0 |
| UL-06 | bench-002 | 9.76948 | 4.70327 | Not collected | Not collected | Not collected | Not collected | EVID-d8c13363b2602c8a66e0 |
| UL-06 | bench-003 | 9.80585 | 4.71672 | Not collected | Not collected | Not collected | Not collected | EVID-d8c13363b2602c8a66e0 |
| UL-06 | server-001 | Not collected | Not collected | 5842.2045 | 9311297536 | Not collected | Not collected | EVID-fd0224096c5549d58f14 |
| UL-06 | server-002 | Not collected | Not collected | 7246.1296 | 9311277056 | Not collected | Not collected | EVID-519ce4e0d1680278ec11 |
| UL-06 | server-003 | Not collected | Not collected | 7387.8036 | 9311342592 | Not collected | Not collected | EVID-c3845e7624a55f94b53e |
| UL-07 | bench-001 | 16.00760 | 6.95731 | Not collected | Not collected | Not collected | Not collected | EVID-7d01cf406e852f9deffb |
| UL-07 | bench-002 | 16.34890 | 7.08699 | Not collected | Not collected | Not collected | Not collected | EVID-7d01cf406e852f9deffb |
| UL-07 | bench-003 | 16.18920 | 7.10160 | Not collected | Not collected | Not collected | Not collected | EVID-7d01cf406e852f9deffb |
| UL-07 | server-001 | Not collected | Not collected | 2754.2656 | 9700397056 | Not collected | Not collected | EVID-4e09787a7389b242c5b1 |
| UL-07 | server-002 | Not collected | Not collected | 3609.7757 | 9701158912 | Not collected | Not collected | EVID-f7ad83e1b0b937e4628b |
| UL-07 | server-003 | Not collected | Not collected | 3172.4011 | 9699885056 | Not collected | Not collected | EVID-87a9a5c5ccdbc3c16734 |
| UL-08 | bench-001 | 9.28470 | 7.09238 | Not collected | Not collected | Not collected | Not collected | EVID-398081adc25b883b15a7 |
| UL-08 | bench-002 | 9.35608 | 7.04576 | Not collected | Not collected | Not collected | Not collected | EVID-398081adc25b883b15a7 |
| UL-08 | bench-003 | 9.29410 | 7.11693 | Not collected | Not collected | Not collected | Not collected | EVID-398081adc25b883b15a7 |
| UL-08 | server-001 | Not collected | Not collected | 2978.7173 | 9384955904 | Not collected | Not collected | EVID-a8f5bcd3955b33cb5a76 |
| UL-08 | server-002 | Not collected | Not collected | 3107.2132 | 9385127936 | Not collected | Not collected | EVID-8d0aa3ac45f9b6f001dc |
| UL-08 | server-003 | Not collected | Not collected | 2642.8067 | 9384980480 | Not collected | Not collected | EVID-4fa6112c29b541b2c7ce |
| UL-09 | bench-001 | 79.85580 | 15.63750 | Not collected | Not collected | Not collected | Not collected | EVID-65a3a7e31b728dabc7b5 |
| UL-09 | bench-002 | 79.86300 | 15.03490 | Not collected | Not collected | Not collected | Not collected | EVID-65a3a7e31b728dabc7b5 |
| UL-09 | bench-003 | 79.73790 | 15.21420 | Not collected | Not collected | Not collected | Not collected | EVID-65a3a7e31b728dabc7b5 |
| UL-09 | server-001 | Not collected | Not collected | 2597.3276 | 2682462208 | Not collected | Not collected | EVID-dde39454f9d6bb983684 |
| UL-09 | server-002 | Not collected | Not collected | 1677.2991 | 2682392576 | Not collected | Not collected | EVID-7207ba6e4af8412844ca |
| UL-09 | server-003 | Not collected | Not collected | 1432.9372 | 2682658816 | Not collected | Not collected | EVID-41758af920b6764091f7 |
| UL-10 | bench-001 | 84.49730 | 9.61043 | Not collected | Not collected | Not collected | Not collected | EVID-922fe80006d168c5c124 |
| UL-10 | bench-002 | 84.51930 | 9.63225 | Not collected | Not collected | Not collected | Not collected | EVID-922fe80006d168c5c124 |
| UL-10 | bench-003 | 84.46810 | 9.63789 | Not collected | Not collected | Not collected | Not collected | EVID-922fe80006d168c5c124 |
| UL-10 | server-001 | Not collected | Not collected | 1044.0499 | 4556288000 | Not collected | Not collected | EVID-97bca65896e91b10fbf1 |
| UL-10 | server-002 | Not collected | Not collected | 1028.2958 | 4556427264 | Not collected | Not collected | EVID-ca3579ea633d77ba2f8c |
| UL-10 | server-003 | Not collected | Not collected | 1044.0224 | 4556492800 | Not collected | Not collected | EVID-cd445466f7ba4301f982 |
| UL-11 | bench-001 | 31.78140 | 6.40695 | Not collected | Not collected | Not collected | Not collected | EVID-31d12cddb482b9a98116 |
| UL-11 | bench-002 | 31.81230 | 6.20216 | Not collected | Not collected | Not collected | Not collected | EVID-31d12cddb482b9a98116 |
| UL-11 | bench-003 | 31.79390 | 6.21917 | Not collected | Not collected | Not collected | Not collected | EVID-31d12cddb482b9a98116 |
| UL-11 | server-001 | Not collected | Not collected | 3546.0856 | 6053822464 | Not collected | Not collected | EVID-2fe63e5fb08e2d2921a4 |
| UL-11 | server-002 | Not collected | Not collected | 3339.7796 | 6053679104 | Not collected | Not collected | EVID-b1cb35f6efebf4ba7c70 |
| UL-11 | server-003 | Not collected | Not collected | 3333.7068 | 6053859328 | Not collected | Not collected | EVID-cccd0e0ded6e7ee89b16 |
| UL-12 | bench-001 | 33.78770 | 4.38737 | Not collected | Not collected | Not collected | Not collected | EVID-06a4562128adf7b60626 |
| UL-12 | bench-002 | 33.78250 | 4.38377 | Not collected | Not collected | Not collected | Not collected | EVID-06a4562128adf7b60626 |
| UL-12 | bench-003 | 33.78540 | 4.38512 | Not collected | Not collected | Not collected | Not collected | EVID-06a4562128adf7b60626 |
| UL-12 | server-001 | Not collected | Not collected | 2492.2619 | 10808979456 | Not collected | Not collected | EVID-a3610920c58945bbd9a8 |
| UL-12 | server-002 | Not collected | Not collected | 2486.2224 | 10809401344 | Not collected | Not collected | EVID-3ea09db4a5d6a21e8569 |
| UL-12 | server-003 | Not collected | Not collected | 2507.6400 | 10809311232 | Not collected | Not collected | EVID-08efec7d9b73b0368e72 |
| UL-13 | bench-001 | 40.65420 | 7.21900 | Not collected | Not collected | Not collected | Not collected | EVID-d335afb5774f9fb254b4 |
| UL-13 | bench-002 | 40.71130 | 7.19868 | Not collected | Not collected | Not collected | Not collected | EVID-d335afb5774f9fb254b4 |
| UL-13 | bench-003 | 40.84420 | 7.21807 | Not collected | Not collected | Not collected | Not collected | EVID-d335afb5774f9fb254b4 |
| UL-13 | bench-004 | 40.86660 | 7.20978 | Not collected | Not collected | Not collected | Not collected | EVID-d335afb5774f9fb254b4 |
| UL-13 | bench-005 | 40.86990 | 7.21180 | Not collected | Not collected | Not collected | Not collected | EVID-d335afb5774f9fb254b4 |
| UL-13 | server-001 | Not collected | Not collected | 2328.6049 | 5531197440 | Not collected | Not collected | EVID-c254e8693d31edba0dd2 |
| UL-13 | server-002 | Not collected | Not collected | 2314.2572 | 5530185728 | Not collected | Not collected | EVID-2935db523c5521649574 |
| UL-13 | server-003 | Not collected | Not collected | 2250.5695 | 5546741760 | Not collected | Not collected | EVID-f093cb37e7e80ffb6e7e |

> Note: Input-token and output-token counts were not collected in this historical repetition series.

## 9. Quality methodology and results

Quality preserves the original 2026-07-15 upstream llama.cpp adjudication under the tracked GTQ-QUALITY-RUBRIC-v1 and frozen GTQ-PROMPTS-v1 contracts: five weighted dimensions, deterministic gates, anchors, strict format caps, and other critical caps. P1-P4 were scored for all rows; UL-05 additionally used P5 long-context retrieval and P6 multi-turn stability.

### QM-01 — Original quality method

| Item | Value |
| --- | --- |
| Rubric | GTQ-QUALITY-RUBRIC-v1 |
| Prompt set | GTQ-PROMPTS-v1 |
| Dimension weights | 30% correctness; 25% instruction/format; 20% completeness; 15% relevance/coherence; 10% stability/integrity |
| Scoring | Conservative manual adjudication after deterministic gates; critical caps preserved |
| Generation | temperature 0.0; top_p 1.0; seed 42; max_output_tokens 256 |
| Coverage | P1-P4 all rows; P5-P6 UL-05 only |
| Calibration | Not collected |
| Score increments | Not collected |

### QS-01 — Original quality results

| Test ID | Prompt count | Mean /10 | Rubric |
| --- | --- | --- | --- |
| UL-01 | 4 | 4.000 | GTQ-QUALITY-RUBRIC-v1 |
| UL-02 | 4 | 8.875 | GTQ-QUALITY-RUBRIC-v1 |
| UL-03 | 4 | 8.875 | GTQ-QUALITY-RUBRIC-v1 |
| UL-04 | 4 | 7.125 | GTQ-QUALITY-RUBRIC-v1 |
| UL-05 | 6 | 5.750 | GTQ-QUALITY-RUBRIC-v1 |
| UL-06 | 4 | 6.625 | GTQ-QUALITY-RUBRIC-v1 |
| UL-07 | 4 | 6.500 | GTQ-QUALITY-RUBRIC-v1 |
| UL-08 | 4 | 6.875 | GTQ-QUALITY-RUBRIC-v1 |
| UL-09 | 4 | 7.250 | GTQ-QUALITY-RUBRIC-v1 |
| UL-10 | 4 | 7.250 | GTQ-QUALITY-RUBRIC-v1 |
| UL-11 | 4 | 6.750 | GTQ-QUALITY-RUBRIC-v1 |
| UL-12 | 4 | 6.750 | GTQ-QUALITY-RUBRIC-v1 |
| UL-13 | 4 | 6.750 | GTQ-QUALITY-RUBRIC-v1 |

> Note: These historical scores are not directly comparable with OpenVINO objective-quality scores or later llama.cpp campaigns unless a separate comparability assessment confirms compatible prompts, rubric, denominator, and adjudication.

## 10. Device/backend use and fallback verification

CPU rows used CPU placement; Vulkan rows recorded one-layer or full-layer placement; UL-13 used full reported SYCL placement. Fallback language is limited to the recorded evidence.

### DV-01 — Backend and fallback interpretation

| Test ID | Backend | Interpretation |
| --- | --- | --- |
| UL-01 | cpu | No fallback claim added |
| UL-02 | cpu | No fallback claim added |
| UL-03 | cpu | No fallback claim added |
| UL-04 | cpu | No fallback claim added |
| UL-05 | cpu | recorded fallback |
| UL-06 | cpu | No fallback claim added |
| UL-07 | cpu | No fallback claim added |
| UL-08 | cpu | No fallback claim added |
| UL-09 | vulkan | No fallback claim added |
| UL-10 | vulkan | No fallback claim added |
| UL-11 | vulkan | No fallback claim added |
| UL-12 | vulkan | No fallback claim added |
| UL-13 | sycl | No fallback claim added |

## 11. Failures, blocks, deviations, and recovery attempts

The entries below are historical failures, limitations, quality deviations, or performance deviations retained by WB-01; they are not reclassified as terminal failures of the 13 completed workloads.

### FL-01 — Historical failure and deviation register

| Failure ID | Test ID | Code | Description | Resolution | Evidence IDs |
| --- | --- | --- | --- | --- | --- |
| UL-F03--UL-13 | UL-13 | GPU | SYCL broad suite passed 49/52; FP64 unsupported, device lost, and two 0xc0000409 crashes. | Project workload resolved; edge tests unresolved | EVID-d335afb5774f9fb254b4, upstream-llama-cpp-b9f424464ca0 |
| UL-F04--UL-05 | UL-05 | QUAL | Long-context output omitted required literal prefix. | No | EVID-971776b67f432946b411, EVID-c7b93a7337fba1ef57e3 |
| UL-F05--UL-05 | UL-05 | QUAL | Multi-turn exact value degraded from `amber:4821` to `4821`. | No | EVID-971776b67f432946b411, EVID-b4a16bd806a44a4651e9 |
| UL-F06--UL-10 | UL-10 | PERF | Full Vulkan offload reduced decode throughput relative to one-layer offload. | Configuration issue, not test failure | EVID-922fe80006d168c5c124 |
| UL-F06--UL-12 | UL-12 | PERF | Full Vulkan offload reduced decode throughput relative to one-layer offload. | Configuration issue, not test failure | EVID-06a4562128adf7b60626, EVID-922fe80006d168c5c124 |

### DV-02 — Scoped deviation and precedence ledger

| Deviation ID | Source item | Scope type | Scope test IDs | Nonterminal | Description | Disposition | Evidence IDs |
| --- | --- | --- | --- | --- | --- | --- | --- |
| UL-DEV-HIST-01 | UL-F01 | setup | UL-B05 | True | R001 cache validator reported failure although required keys existed. | Yes, R002/R003 | upstream-llama-cpp-1d7c7f12ccbf |
| UL-DEV-HIST-02 | UL-F02 | setup | UL-B05 | True | Vulkan repository suite initially passed 51/52. | Yes, 52/52 | upstream-llama-cpp-bbb63ecd783d |
| UL-DEV-HIST-03 | UL-F03 | mixed | UL-B06, UL-13 | True | SYCL broad suite passed 49/52; FP64 unsupported, device lost, and two 0xc0000409 crashes. | Project workload resolved; edge tests unresolved | EVID-d335afb5774f9fb254b4, upstream-llama-cpp-b9f424464ca0 |
| UL-DEV-HIST-04 | UL-F04 | prompt | UL-05 | True | Long-context output omitted required literal prefix. | No | EVID-c7b93a7337fba1ef57e3 |
| UL-DEV-HIST-05 | UL-F05 | prompt | UL-05 | True | Multi-turn exact value degraded from `amber:4821` to `4821`. | No | EVID-b4a16bd806a44a4651e9 |
| UL-DEV-HIST-06 | UL-F06 | multi-test | UL-10, UL-12 | True | Full Vulkan offload reduced decode throughput relative to one-layer offload. | Configuration issue, not test failure | EVID-06a4562128adf7b60626, EVID-922fe80006d168c5c124 |
| UL-DEV-HIST-07 | UL-F07 | campaign | UL-01, UL-02, UL-03, UL-04, UL-05, UL-06, UL-07, UL-08, UL-09, UL-10, UL-11, UL-12, UL-13 | True | Initial run omitted peak RAM, KV MB and TTFT because llama-bench does not emit all three. | Yes | upstream-llama-cpp-db711113b8e9 |
| UL-DEV-REGISTER-TEST-RUN |  | campaign | UL-01, UL-02, UL-03, UL-04, UL-05, UL-06, UL-07, UL-08, UL-09, UL-10, UL-11, UL-12, UL-13 | True | Current Test-Run register contains zero exact UL-01 through UL-13 rows. | Indexed logs and WB-01 are the admitted historical authorities; no register rows were invented. | upstream-llama-cpp-0d0e0a927263 |
| UL-DEV-REGISTER-PERFORMANCE |  | campaign | UL-01, UL-02, UL-03, UL-04, UL-05, UL-06, UL-07, UL-08, UL-09, UL-10, UL-11, UL-12, UL-13 | True | Current Performance register contains zero exact UL-01 through UL-13 rows. | Indexed repetition logs take precedence; no later-register equality is claimed. | upstream-llama-cpp-2c104a7f446f |
| UL-DEV-UL13-PERFORMANCE |  | test | UL-13 | True | WB formal values are 40.789 prompt / 7.211 decode tok/s; indexed repetitions compute 40.844 / 7.212. | Normalized summaries use indexed repetition medians and preserve WB values as a documented divergence. | upstream-llama-cpp-27c47275b8fc, EVID-d335afb5774f9fb254b4, EVID-c254e8693d31edba0dd2, EVID-2935db523c5521649574, EVID-f093cb37e7e80ffb6e7e |
| UL-DEV-UL05-QUALITY |  | test | UL-05 | True | Legacy WB/quality prose displays P1-P6 mean 5.9; arithmetic over prompt scores is 5.750. | Prompt-level scores are canonical; both historical display and arithmetic value remain explicit. | upstream-llama-cpp-27c47275b8fc, upstream-llama-cpp-3a280ff9538d |
| UL-DEV-DECISION-LABELS |  | campaign | UL-01, UL-02, UL-03, UL-04, UL-05, UL-06, UL-07, UL-08, UL-09, UL-10, UL-11, UL-12, UL-13 | True | Legacy WB labels name UL-04/UL-03 CPU and fallback choices and UL-09 GPU; approved publication labels are UL-08/UL-10/UL-05 only. | Approved bounded publication labels take precedence without rewriting legacy WB prose. | upstream-llama-cpp-27c47275b8fc |

## 12. Limitations, uncertainty, robustness checks, and claim boundaries

This is one laptop, one pinned upstream revision, and one historical prompt method. Repetition counts support observed medians, not population confidence intervals.

The current Test-Run and Performance registers each contain zero exact UL-01 through UL-13 rows. Indexed log evidence is therefore the repetition authority. UL-13 WB formal throughput and UL-05 displayed aggregate quality diverge from arithmetic source values; both are retained in the scoped deviation ledger with explicit precedence. The raw-results folder is a README placeholder only.

> Note: No result establishes healthcare safety, educational efficacy, universal model quality, or causal superiority.

## 13. Reproduction guidance

Regeneration normalizes existing evidence and does not rerun inference. Use the repository-relative inputs listed in reproduction/README.md.

### RE-01 — Canonical reproduction locations

| Item | Path |
| --- | --- |
| Controlled workbook | docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md |
| Quality source | experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/quality-scoring-2026-07-15.md |
| Resource summary | experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/resource-metrics-2026-07-16.json |
| Evidence index | docs/testing/Evidence-Index.csv |
| Canonical output | docs/testing/final-results/01-upstream-llama-cpp/results/ |

## 14. Evidence index and hashes

Every admitted source has a repository-relative path, byte count, and verified SHA-256 digest.

### EV-01 — Complete admitted evidence index

| Evidence ID | Role | SHA-256 | Bytes | Repository-relative path |
| --- | --- | --- | --- | --- |
| EVID-004c0b4f19f867b05c27 | indexed-log | ad6d08238cd347557dc164f4fdf2d407ef4b0473e90eeab483c4d30a9214b721 | 119151 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-007d654aa76d80d4b6ab | indexed-log | 6685f6cdcfcade67991a1cbf2928a0ce8080c8aa9a923444702a6ccda8ed4ff8 | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-0109b66e65441d5d24ab | indexed-log | 98abeeb5d72ca3e4fd4c873dca2421fcbd4654b16c43b8c45afca6add019b7cc | 2716 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P2-stdout.txt |
| EVID-017d230eed276c3135a8 | indexed-log | 29c3a37b9d9c48196547fe5062055f4f077d1b1dc2e15aa1f7a6e54aa24cfa09 | 120548 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-01b4cb0377f6e08860db | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-01cb6afe3b020c9d254b | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-01f2981a7fc5c74955e7 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-022b67391f816856cced | indexed-log | 932e9c8cc52603bf0f1e2faeccf36e644362668c0422ef36a2a6e61906ad8cc9 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-3/measurement.json |
| EVID-033e07a027e8767e937f | indexed-log | c22234c663b43aa6cf63ead64629a289ced041fae4bc239866900493f9117cad | 1130 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P2-stderr.txt |
| EVID-03542f7661583c28bd75 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-0384b5e57409af056102 | indexed-log | 920fa8101aa18c9f94b402de3494b30fda26e1cd72794f0643fcb29108d3ee52 | 703 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-1/command.json |
| EVID-03d80140542eba5f57a4 | indexed-log | 015bddc214400f82b9d0a4cae600b470934fc3d9ada89af56202ae873a46ec74 | 211147 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-2/events.jsonl |
| EVID-050753c62226885f7196 | indexed-log | c3d5738d0fb24a4fce310321c49d8f2f658ab4cf43b84b5a3e31c9b3c652313f | 2187 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-R001/bench-stderr.jsonl |
| EVID-064acb655acc60728f60 | indexed-log | 724df017419dd411f30e93aaac16677fe649ba388d34791ea83c24ce4a0df10c | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/warmup/measurement.json |
| EVID-064ae138527b8cec0561 | indexed-log | 5a45171b11b54121444bc99aa79a1005bee07a951faf08cb641f30d52e51123d | 102315 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-065b49fa455d459468fa | indexed-log | bd525531da41d4987a74aaa2386adf7b72c31b90d6a7ee83d417567eb08ed5e1 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-3/measurement.json |
| EVID-068bd0a3602ab4a5ceda | indexed-log | 285cbc4b7646391112c22e72a51587ef3d394a6af1b7968fcfb63d5d24d16046 | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-3/measurement.json |
| EVID-06a4562128adf7b60626 | indexed-log | 86e3118e8f74ad08e16620c4bdede6840bc6cfc7010651d2685738a22d312358 | 2275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-R001/bench-stdout.jsonl |
| EVID-06af12a03a8811b8da58 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P6-R001/P6-turn1-stderr.txt |
| EVID-06d1f793f82d948290a0 | indexed-log | 47b67fb3103640fb5983a165765e9f176d705660c1034d5480acc3c9cf1dec50 | 115129 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/warmup/stderr.txt |
| EVID-06dd53fda3f269b99c48 | indexed-log | be7a0bfa52ed215d3f8825b076ef4747b4029d2a3c3f067488f44ba008284f31 | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-2/measurement.json |
| EVID-0719c0796234b7f95fd6 | indexed-log | 732e05979190a21e938df14dcf73f1e6556bc76693a2933eb71963d0db03f814 | 2475 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-R001/bench-stderr.jsonl |
| EVID-0742c6ef091a02557ce4 | indexed-log | 9863b3b342196d1ea216fa1fe35eaf24cce01c29172f2ea4445cefecc35d2f28 | 1410 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-2/stdout.txt |
| EVID-077abf951f42ae5ab8f2 | indexed-log | 70bd1a8d047587fca2b617d694db40ac75f27d49c2538f771e7c4f3444e33cd9 | 2223 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-R003/bench-stderr.jsonl |
| EVID-07bc3cb180418e69e33f | indexed-log | 2ea0bc2ebc6984ca8c1f221d8a1e54e5bb209e6c8eeefbb3e59f55b9a6091c80 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-3/measurement.json |
| EVID-07ee26fa0e34658dfc7c | indexed-log | 4e5f34d21495e0503022b4190046c7eb2590d8f40e29635bfa293690c84e1c99 | 103512 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-084184d9b86cd1aac5ca | indexed-log | 7458376ba94b8ea02d4c0ae7be8eb25dd144615149dfa817e7fb5dbbc0c7de63 | 133353 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-2/stderr.txt |
| EVID-085a17bdf9e4ce2324ab | indexed-log | c36840d24bfc031b6ce7b97e2f6d9a623cf34c25ac75845b344ede9e2556c077 | 215519 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-08efec7d9b73b0368e72 | indexed-log | 8201bce59271f97c7f6c639f9c78b9f607646b5f6fb803f7b0e5c556dbd4fc7e | 273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-09f98fcb3b15c1f35594 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P1-stderr.txt |
| EVID-0ac81d4d9b39c70c5f32 | indexed-log | b8bda32e3b6b60bb67904a2e4a52385b3297977fbe6507c222482fe5aeb18e1d | 133353 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/warmup/stderr.txt |
| EVID-0b074756492d51d8f416 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-0b6f4448111985565454 | indexed-log | 13db46398d19d0e64018e9df8f4ec3592eb73b12a7368a7aaa5c11dfd341086b | 273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-0bc6f9ceb4b0e9d556a3 | indexed-log | 9cbe37b6f85aeaac66db1f71d1e30965c57949470acc995c217b5d4c4ff41bc8 | 701 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/warmup/command.json |
| EVID-0cc583642bc37a21a271 | indexed-log | 8eb17b20003bdb243d8f57879a44ed758430d4c274c00fa188d58cf12915577f | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/warmup/stdout.txt |
| EVID-0db1f5d9058232f35823 | indexed-log | 8eb17b20003bdb243d8f57879a44ed758430d4c274c00fa188d58cf12915577f | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-1/stdout.txt |
| EVID-0dc20f0234fda0f4ffba | indexed-log | 0f03af5ab996eee04754ebb97d11188e4bc6348a3380777284c8564e090c91b3 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-3/command.json |
| EVID-0dc8aa686dae5140d31c | indexed-log | ea718787856d20db1683ae7674206d6f1e499088d6a57067673af555aae60105 | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-0dce5833cd0ea1325615 | indexed-log | c49545ced405fd40bca6fe345f013fc9ca6b5f73d11ccf540587f51c737cd587 | 116801 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/warmup/stderr.txt |
| EVID-0dfc01a5054a689e507e | indexed-log | 8bce6d44033472c658303d0aa2b18847dab73a8260d1fc94406790c7cd46f832 | 1127 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P6-R001/P6-turn1-stdout.txt |
| EVID-1022fd7a783d8592fe7b | indexed-log | b679fac057c3d11ffb85ca7bdaf6f66ce4635ebaa58ec9d40fcbcf4c36e5c3d2 | 119147 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-10a6cc68a48b54fa4b5e | indexed-log | 03a6207c7dc4a21c831d826d4963542b6dfa5f6edc9b73a4beb20b990a76d84d | 215435 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-10fe16e34d118773f81a | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P3-stderr.txt |
| EVID-11458328dcd15e23091c | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P1-stderr.txt |
| EVID-11e5261303c17e6c4d20 | indexed-log | 986ed458f20107647ef6db024da8272011807611606a3ceb8df762e38aab0470 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-2/measurement.json |
| EVID-122c5c385dfd65f6af5a | indexed-log | 2d7b5eb97d70a1596f0e37feb6b090e53c4008583505edd60d4ddcd1e53bb2d3 | 133357 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-1/stderr.txt |
| EVID-12317a849c3e36c6153b | indexed-log | 413e918cf76b15b90bdd55dc8de6f74c65f4fe62633e0b6f90dfd10a1670700c | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-3/stdout.txt |
| EVID-1274161c354d45d91944 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-12be6391c40b09a75e8c | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P4-stderr.txt |
| EVID-136c51ec266f480c5ed3 | indexed-log | 20ab7e0f380c66d84d11ba01071dd6e0c9b7cb8074047ab0d9f1c5d5087c9406 | 1564 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P1-stdout.txt |
| EVID-13c96e4058f6a071b1f8 | indexed-log | b0fd0588ccccf8e71e51e8e184c9a6f726a8a89c293ada7f13b292caed8eb6e4 | 703 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-1/command.json |
| EVID-1486d43af04bd063e437 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-149a24ed228514dd3e38 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-14f3ce4742bec09605b5 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P4-stderr.txt |
| EVID-1581ecfc51a3dc088304 | indexed-log | 99440980ae8894a863b1c8d54c90fcafb4433ad41d7e1465d3ecee8d65c9f751 | 211134 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-3/events.jsonl |
| EVID-159f913bfa92020ee8e8 | indexed-log | 551b7560c7c4f38e253d38b575020b1a34ee207d3c3773a0b6c3598619179799 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-2/measurement.json |
| EVID-163b2d35cc70093c518e | indexed-log | f318802335f352151af984d7b978e4e1434e0f41608f92f9c570c55ab23c0527 | 185817 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-17a6ccf1ee19173d70f2 | indexed-log | 33bc405a5fa93fb5e5c8b3b565937c4ca657fee36bddf878916fee2774817ad9 | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-2/measurement.json |
| EVID-17b388dca1ade85bac4f | indexed-log | fa31ca5aa5230526c2f7a8d04cee3739ce1358c43618db8fe18846672eb98286 | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-3/command.json |
| EVID-17bb12cd6bf6badaa3e3 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P2-stderr.txt |
| EVID-17e68663f96577a6778a | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-180127125f780a0a8cf0 | indexed-log | 50b804a5308eda57abb877b373625a4283c048c22120d4393182bd1a2aeec6f3 | 703 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-3/command.json |
| EVID-1813d6e6d888555c74eb | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-18480285856116f56243 | indexed-log | 6ab936482cf374520e68c7a6e8cead108627369774f1a15a6854ce8fa21996fd | 118338 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-2/stderr.txt |
| EVID-18a41b7d320742092b94 | indexed-log | c33b089cc36985cb5fccb96f76a20042f0498d6b83b18ede7186437d2f5bffe4 | 133357 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-2/stderr.txt |
| EVID-18d5882c9ee702cd9b6c | indexed-log | caf8a65f3524364eb318d4952111784d96f436857b5c2e0cf4c9473e81342ff6 | 116220 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-3/stderr.txt |
| EVID-198d37f5d1033d211653 | indexed-log | b5e038eeaf33bebc2ad9234d7bd94297a759b1d6c395b1cd9b95b0044b27a054 | 219901 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-3/events.jsonl |
| EVID-198fcdc48629636d1cbb | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-19fc3bb0380379f40eab | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P1-stderr.txt |
| EVID-1a47aa87f219a92b6d72 | indexed-log | 62743ac21448954ba6f52b71ac19390dea19a5a66c28e29fd6907e875cc353fb | 116825 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-2/stderr.txt |
| EVID-1b0f498d6076884f69df | indexed-log | ec1d6ebca0dd07d25fffc22ca59260cc9385e4289e4dcac85fc7ce8c056aeaca | 211059 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-2/events.jsonl |
| EVID-1b295a270e8a6304f13b | indexed-log | db9c4c97b85a7df169432dd0b5210a1a27c47e5a915edadfb524873015ba6b1f | 277 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-1b7458084296d86c6d0a | indexed-log | d5234b992368caa5efa466e02cb0e9ceab8619dabce2b6997bb13f67827e5474 | 214484 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-1/events.jsonl |
| EVID-1b75d2f026ef109b611c | indexed-log | 4b9c780fcd32b57dd15dfec3b2eb8d5e27ea5cd66c60aac1131fa659f3b25acb | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-1/measurement.json |
| EVID-1bcdaeb58d755d594440 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P2-stderr.txt |
| EVID-1c76c11875d1a9bbfbd1 | indexed-log | ef2803eb70433d72ff1b07eadd4ee898e588f02537a69af0c14efd590997d739 | 101982 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-1ca3d168d4ad4fc74a97 | indexed-log | c5172c3c1fe42947448d29ad557874b5cf0770b1c672bd2a51e1d4a98752e690 | 1430 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/warmup/stdout.txt |
| EVID-1ce413e51919c1d37efc | indexed-log | 6ecb1285a2f28a24931c744032b93bc90d5b893202e9929cffcb9f317e1cf791 | 1470 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P3-stdout.txt |
| EVID-1d44d46c1b03c65e7385 | indexed-log | 21c86bb842ea816205c77dc8a5bbe4567b31e33e037f490dae99a9f9483915cf | 115260 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-3/stderr.txt |
| EVID-1db365a381871f59284b | indexed-log | b71702c90eb110293f5e909c601bc8dc9a718482b4d64ed7833f7954c29716bb | 104099 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-1f45fcd568b67c8b0599 | indexed-log | fd3e62a3ed473103494dbb9cff1455c11950e3984dcd0875484417127d88faca | 116801 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-1/stderr.txt |
| EVID-202af1b60fec61732503 | indexed-log | 7babe85fcef9b10ea3c0c6966bb7bd419ea10a8880a8e06912f8deed1d39a1bc | 1826 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P1-stdout.txt |
| EVID-20495d074e6e0c0650be | indexed-log | f527d4979874415ff80dcf86781838fa86c4bec381103ff2df00c1882d29d838 | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-3/stdout.txt |
| EVID-20e1db8be7c3b244d0ca | indexed-log | 398e520049cd2c0ff884d95436dac7a88d794db60afb06b33f6c681f39f75cc5 | 102315 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-20eb9ef90742f92e13fa | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P1-stderr.txt |
| EVID-21564e51f2e64b2a2572 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-219be3e430fd807395d3 | indexed-log | 2de9ba4b9a55befd13bad15c71ae31fd7a810a67e1f883fa0249d43787ed4146 | 217225 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-2/events.jsonl |
| EVID-21b70b22101ca8696da4 | indexed-log | 6838c6d5a6132f4683797a44fca229d52983ca26d4a3bc4b0738cb539920f549 | 693 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-3/command.json |
| EVID-21d0b5379044b2a45038 | indexed-log | 8f06cd53da2e2f450da41074a2e0fe04f51c95468e822ef7cad7e0443df365af | 214568 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-2/events.jsonl |
| EVID-22074a33990f153113fa | indexed-log | f9dd1a7a2f683fd77f37a7f23848cebbb8f29c7c373a1f77dca566939e3cd7d3 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-1/command.json |
| EVID-22a934f0c35543e6a0cb | indexed-log | 0f11c23b6f94ed853c0b31313434a66a99bb69425baeee667afe3720d1a4f444 | 186360 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-248ac70f0cea82ad46e2 | indexed-log | 41aa5a42848c5c97cb19892e8148bb8a0598f9cb3b8834497369fd3877ca445e | 1563 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P1-stdout.txt |
| EVID-25a1d5f4a9fe4038c4d5 | indexed-log | 48f0a95cd8317fb25166c04b36d623df897829178cdace4b9a20c395be1db654 | 116220 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-2/stderr.txt |
| EVID-25e976b7af898bb7518b | indexed-log | 11de021de083badd4901040da8d0bc6c390254b3539cc4a5817761208a969649 | 275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-26100d253220a4d4255e | indexed-log | 9b17f361f0ab20b1e665670c8bdad6aff1c3ce41b35e5421b247e0b879b0ef11 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/warmup/command.json |
| EVID-262914275708e7226f8c | indexed-log | 116df7572a4409e0a118d5b80c43dc546de94ad79430c0d4f294fb2ab4b0062f | 209951 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-2/events.jsonl |
| EVID-26e7de2a904c4f707070 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P2-stderr.txt |
| EVID-27f7c541ba932b5493d6 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P3-stderr.txt |
| EVID-28058dbf5bc854354677 | indexed-log | 1821cc18cd08ae35af7c416d22dd120ba73f1bd972304de69d88d16414d0a39a | 217352 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-1/events.jsonl |
| EVID-287723cdf0dc3bdb5e72 | indexed-log | 469f0bb679a370fda742ba2e994110a8a7b6b7f1245fefe47e664233f98a137c | 188432 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-2935db523c5521649574 | indexed-log | 1576358bc9613fded719146f1f9ca11d564f97804b7af488a9c33b744f016d37 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-29e2b84d498dc33d7152 | indexed-log | 748f36cabc9cdc62604b173161b87d5ac3afb41e2478e696bcbb51ca0704b9ac | 104093 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-2a0e82b9221fdd815dc0 | indexed-log | 750492d9f9fe61e5110225e105e8d479f97ab167eec99724b50c73a1b90f3d2c | 215435 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-2a37ffe95cc8dbf0322b | indexed-log | 7cdcc444624e3cbaed3defc6e7e701ef24a6a7685ed625c2a432aa4ece468205 | 133510 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-1/stderr.txt |
| EVID-2a63591ace4a278a53a7 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-TTFT-CALIBRATION-R001/stdout.txt |
| EVID-2a79322a284d072501b3 | indexed-log | 3c4373eb4d3bd12230e2dfff145abf654c7c4d4d68b0f5443de2d9c28a2df4e3 | 120552 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-2ab426354183627fab17 | indexed-log | 83fdae5414241d7bb228613c1efdfa1583ae2fef91f26e812b646110ff028787 | 214631 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/warmup/events.jsonl |
| EVID-2ac59737134df0246c48 | indexed-log | 9cfe0893ef7813d7ec656f9f299e38d19034c3679fc06b242d2615f5ca89818e | 209909 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R005/events.jsonl |
| EVID-2b40d565548a464a9a34 | indexed-log | f0a565d6c7926a4b2eefefb42f7c060ab78a756b9e872ff9f1897968170e1210 | 115129 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-2/stderr.txt |
| EVID-2b96716c28df3a7fc7c0 | indexed-log | 8171dc430f127efa55f97833c5d18c6f2296baf0736911affebc758463e2d0a7 | 247048 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/warmup/events.jsonl |
| EVID-2c21476b9c8c970b5980 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-2ce25fb742815f15dfd4 | indexed-log | 1ffc535659e04a17149466545b16e1fdfe5dc716f2a9b4fc611f9d1b45ec898c | 104355 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-2ce94cf59d1712b0636d | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P2-stderr.txt |
| EVID-2d02597c2b0fcf314b50 | indexed-log | 948a3452ef8773c3d7fab803e35077fa58513735a9a9fc74f353dc3af62a4980 | 211844 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/warmup/events.jsonl |
| EVID-2d2d6bd5399ce336d5a4 | indexed-log | 408c333c93030bd924e03a1d3636c894352e719dc6e84df59233b87c7b5c5985 | 241255 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-2/events.jsonl |
| EVID-2d5b3f8dd96ac5af44fb | indexed-log | 8c7c5cb04655a1799b399431f2ed703845e99e690417ff88c686c17472f63f6c | 220198 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/warmup/events.jsonl |
| EVID-2dca1ee74204bbee5db7 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P4-stderr.txt |
| EVID-2df2d7346dc1dac73efe | indexed-log | c22234c663b43aa6cf63ead64629a289ced041fae4bc239866900493f9117cad | 1130 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P1-stderr.txt |
| EVID-2e060658a2dc3b4e3365 | indexed-log | 7972c8023e57a9018ca9d906c4b1b1083364546bb480675866b568e86f3a9429 | 2213 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-R001/bench-stdout.jsonl |
| EVID-2e27d2f4b5f26b487ad8 | indexed-log | 902a640b1c990a2cfbdc1d5b99d89069dfd23deaefd30d857ea1b58c6177eba9 | 185980 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-2fa6f22d57e6347d3b4d | indexed-log | f153039559a5a323daf3fa6931f62a19cd9d823929149508cbd2af34c8952192 | 1836 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P4-stdout.txt |
| EVID-2fe63e5fb08e2d2921a4 | indexed-log | a227848ff6cb2db371cfe9fe29bf797cae3171e8a95cb81e042cd3c147f41ae7 | 273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-30644892fbf9d3cfea89 | indexed-log | f0b24f90e758e8982270f2420966fd42cd2c960a92a24b4eacd8058584e6a3e2 | 193130 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-30a681ae73c0ecfef426 | indexed-log | e768b411b4571a58999840d46bd1f1d4f1e42888d8ed87144d6596881c074ce3 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-3/measurement.json |
| EVID-30da3ad944686854aece | indexed-log | a9d2196ff2119afa3e6c5c415be0c048550ca9826e1b1bb4aa87f702c301e662 | 103601 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-316dfc0dc321131f20e8 | indexed-log | 46b2f2a2a616196d582d1d2bb8bdc570b0b52f0d59cea6623ac36a281d917a9c | 186145 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-31914326b06aeea2a841 | indexed-log | 7fd5605ddc77ad16304319e4c926abaf81de0c62999e1ad525e23fd6c42210a1 | 115126 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-2/stderr.txt |
| EVID-31a56671ad1fcac86f08 | indexed-log | 40ccfd8678bf176e4a4d34f8ac888533c380a887484fbae46c9a3d7d1a6286bd | 1408 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/warmup/stdout.txt |
| EVID-31d12cddb482b9a98116 | indexed-log | 0043d3d8457b105f5ed82e513ed0b59e8078aa3bb2f2b8be81e0e716947c3520 | 2275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-R001/bench-stdout.jsonl |
| EVID-3244d10afd72c5b4ef70 | indexed-log | ed48819441e9399f616616bcff6b646bd8c2c6b19f513335da5a6f684a98b06d | 101982 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-33437ea3843509fdbbba | indexed-log | f9e5ad56c6e672086a2b8b18b1ec61ffdf76d97f884f2cb21263255604132fad | 211068 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-3/events.jsonl |
| EVID-33d8942e428c45c3931d | indexed-log | 1742b0bba88b0595ec976e824f102e40fa4f78c013d3e7706ad1ed635a85dabf | 2456 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P3-stdout.txt |
| EVID-34f7f68ef6bd5a5c2704 | indexed-log | e957bc0a5c96aa6593a19e981fb8e5e3b354342744dd1eeb98da4a6d71969c68 | 1429 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P3-stdout.txt |
| EVID-34fcce884edaeb759552 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-35362825d6e5ad83df16 | indexed-log | 1ae22017bf67864243de00027ed5e6e8c4b5d0208b97000880429ca59f4bdb4c | 181736 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-35d17470c6888199e22c | indexed-log | 83c3dd5fcd080fb52d42586b2a8a54d0c8467f2f4497e2b8f832428251b15b6a | 116273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-2/stderr.txt |
| EVID-3611554f609997e33f8c | indexed-log | 69ca3068c27510d7bc5d86bc41de21232069993ddda6f80456722c9122efae24 | 104098 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-36b045a46ad359044f7b | indexed-log | c7f2b35798ce8eeef1c28aaf59af9194fa3f1e071db6a7ccc8be0f0ed5d443ed | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-2/command.json |
| EVID-374d77a6074aa9e0a6a7 | indexed-log | 7d8612573229c2dce5a225c86f9d5a0febf77a2f6626332beacc0f7820b31e65 | 186388 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-37799afc59624d0652e4 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-380a2faf371cb0d3ba53 | indexed-log | f3e5d314562c787aaaf72de5a066ad183624bd401e3284a961178ffa5ccc3bcd | 1422 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-3/stdout.txt |
| EVID-38f19c9e78d9c61b2788 | indexed-log | c2b87663ab3bab5ab4e601567b355c86baff15cc7e5eb3f37ae68aba4bc6d55b | 189 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-2/measurement.json |
| EVID-397bf1998566309b13a5 | indexed-log | 541efd426a27221e6da8661faab0cf1c55b592fbcacb81212535542ade454407 | 115129 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-1/stderr.txt |
| EVID-398081adc25b883b15a7 | indexed-log | 13e0264faf17bfe38f4bbda180935f067b60e27e428d24b42b6bfe6fcfb85cb4 | 2219 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-R001/bench-stdout.jsonl |
| EVID-398f61bf03b9b1ee3f9e | indexed-log | 595a143124d611d2a1fc5b351a060149360386e4ed8967e7d6364be513da4316 | 220093 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-1/events.jsonl |
| EVID-3a024c1c5725993be72c | indexed-log | c076a8ae740eb59fd4d01a7c5584440d8783c2a6026021b792d75eccc4d38213 | 1550 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P2-stdout.txt |
| EVID-3a21dc2a4e4fd6f30016 | indexed-log | 68388fcc2d1edee3a0ea83debc35a734bdb82ee13298686859efff1ee439cca4 | 1434 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R005/stdout.txt |
| EVID-3afcfc7060a1521338ca | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-3c8421caef0f7d9957d9 | indexed-log | c22234c663b43aa6cf63ead64629a289ced041fae4bc239866900493f9117cad | 1130 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P4-stderr.txt |
| EVID-3c88f26c0d8a1925bd9a | indexed-log | c22234c663b43aa6cf63ead64629a289ced041fae4bc239866900493f9117cad | 1130 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P3-stderr.txt |
| EVID-3caa2c762b97a9b5340d | indexed-log | 4422f1d41333e282e86ec392467940e0619b87228d2448ce7d44ac81756cce30 | 189 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-1/measurement.json |
| EVID-3cab28e47e59268e094e | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R001/stderr.txt |
| EVID-3d07c46b9c677f61c8ca | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-3d321a45fb2f2ff83d86 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-3deebbad54f060c92b3b | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P1-stderr.txt |
| EVID-3e18a934056c25c404c5 | indexed-log | afa0d704fe2c1bcecdc253807db72ac83ffc6621ea9e7c0e129ccd02e3750913 | 116244 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-3/stderr.txt |
| EVID-3ea09db4a5d6a21e8569 | indexed-log | d4176f7d65d936a9d4d50ae0d193deac30bea4d1250792461f5f8345c6ef6691 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-3f71a36676dad1e7cf74 | indexed-log | 798a31332c545125318f52eae686831b47b45069495f03a594fd603cf1c83b93 | 217280 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-2/events.jsonl |
| EVID-3f8c1aa9c37ecdd8b4b4 | indexed-log | d45678494ea70b485f96cc4fbd12b970e19157befdace31ee2d3a3495745301b | 187628 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-TTFT-CALIBRATION-R001/events.jsonl |
| EVID-408dad8f4ca96c5db7b0 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P3-stderr.txt |
| EVID-40d8c88ab8d5ceafb7d6 | indexed-log | 8b346cc5d50ecb81c5668323982a3354da8f5ed47b64a40ad7af1a5af053fada | 3526 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P1-stdout.txt |
| EVID-413db3135d698df67f6d | indexed-log | dad1a28b8e2d0b2258efffe8105315babd7bfc461730dacdd215120a30388316 | 119147 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-41758af920b6764091f7 | indexed-log | b5480c5b74e97a9d933acaff4424b5a1017c26f727741000b5958f48108a9ba9 | 277 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-4246717cb5512320574c | indexed-log | 5fd246caee1dd4a2f75cde29b70be22d1dbc50f682ac7afa4de460265ceccdcb | 132926 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-1/stderr.txt |
| EVID-4342209225893aa76da0 | indexed-log | 80a8deeb81cf47719a7957b89fe57b90d3f452d2c7c520f7ed99c06d96415c1f | 1756 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P2-stdout.txt |
| EVID-456dccdd630e64d5a479 | indexed-log | 6e1aebda0496c7168d15f2483b540828bbb2d9e2f4fe15e937a09a638eadd898 | 189 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/warmup/measurement.json |
| EVID-462af0cb10381811aee5 | indexed-log | ae6c7497300b08c395cd26a289d25c7c4ca159f6e8c72cbd976d59c1a775e59b | 209966 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-1/events.jsonl |
| EVID-46d53dd8b00a6f673044 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-46e16b7cc8d3f8130081 | indexed-log | 1ad4ae8d0b7fbac9912a3b2f54845207329848887186d45de2c16726aa60d2d4 | 184378 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-47b2148f9f4c1dd625ee | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R001/P1-stderr.txt |
| EVID-482302152c3929e6d4bd | indexed-log | 571432a3baa99186e3bbb1975bc3405c1d1701ebe01517d9b42ad19ebf3245ca | 537 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R001/P1-command.txt |
| EVID-4847ac5e4c45d8029c99 | indexed-log | 4ac1085abe818d9396295263df1289344d6edafea89db2734eff4a7d8dd12c57 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-1/measurement.json |
| EVID-49766283754582623b37 | indexed-log | c89adf36d036eabba89e1caf2bf9d7e9180103af6c49ec92028e7866f2febb5f | 188 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/warmup/measurement.json |
| EVID-4acc8ab5edf3bced0e3f | indexed-log | 83ddf54309b80417ee493265607205b92c83bbcede02161b3f42ea149361db5c | 1842 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P4-stdout.txt |
| EVID-4af64b0c2d7501e0eddf | indexed-log | 6398c82f1f47cfba639ee92f16e233d818678cc17018a5859d13b340771a0845 | 210293 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-1/events.jsonl |
| EVID-4b90889af5db07cf2b3d | indexed-log | b74dd345ca10d3a941ac10d9edb31692c5904d01aa6cd0f42f8a0f2b803c8b0e | 210442 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-2/events.jsonl |
| EVID-4c72fac7f9991050b663 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P4-stderr.txt |
| EVID-4c81b343fa3603ce98a2 | indexed-log | 0de63db261d236b7f23098c8acef027303b2347d0e70a6a7734890c48e6c1aec | 181686 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-4cad51edca81a35e5278 | indexed-log | fb6cfd65a83a919d4af0c89916757f75a0e2f341f937b7466ab04fc193913a0a | 188 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/warmup/measurement.json |
| EVID-4cfa4eba1c8c19f67104 | indexed-log | 39b391cf85fcb41886a837d16c0d211468c71d7a7cb1f701f083d98989f390d0 | 119151 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-4db8ec9b0b506bf1faf0 | indexed-log | abe117c6eb0db334d7c6386b450f959c72c835de8409774ca324f1f8195e6e53 | 2472 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-R001/bench-stderr.jsonl |
| EVID-4dc08d3a2bdda40ad9e7 | indexed-log | c3d5738d0fb24a4fce310321c49d8f2f658ab4cf43b84b5a3e31c9b3c652313f | 2187 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-R001/bench-stdout.jsonl |
| EVID-4e09787a7389b242c5b1 | indexed-log | d9bf02d6777400e1ab17f12c023d67836d9588bf0078e44eac46fc8de7f5b4b4 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-4e363114de95869e8252 | indexed-log | 84ff717e71a6320b14c15c05c5bc9092e9896c65a6f1994d68d9a2f19cf99860 | 115129 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-3/stderr.txt |
| EVID-4edda489cc939810557a | indexed-log | a6db867a969ce31a951832a7c9da4f349836b4483f0fbc3ce673d43cbb470f74 | 116244 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-1/stderr.txt |
| EVID-4ef3717d9c1f93c8df75 | indexed-log | 3cd40e1143811d3e373600098e92044fb8f1c662190e37ffed7d5ca4ffd7fe1d | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-4f575973a98e7932f4bf | indexed-log | f2ce325a4a352cce7fc7999e41f49a45aa2a177caccd8862f5ef24cc8f0a0716 | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-1/stdout.txt |
| EVID-4f953836e062a76d9d36 | indexed-log | 4b782fb446879dd8d2ff61b1cd007e6bd35aed12fbeda61c196f2db063765fcb | 100776 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-4fa6112c29b541b2c7ce | indexed-log | 76edd3108f693b749a402a13ca9852205cf45eed16f7a058ea6623c8e258e86d | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-4feaa552a09b389c150e | indexed-log | e7ee82dd529f4cac40d4df926cae3f6897c87ec995e025ac281def60d8e814b4 | 1243 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R001/stdout.txt |
| EVID-504aed15155e8cce9f39 | indexed-log | 342ad28a14ca524356ffab1f5520d8e57573edbb05c5fcee5cebaf6f28c9e94d | 1470 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P3-stdout.txt |
| EVID-507807c59e0726294766 | indexed-log | 7843e68845a409071fa66eca6b6b38a5602826c5aef3c67a64e16d216c07e19d | 102563 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-507c2cca86bb38739714 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-5121dae4986e0b8a5e6e | indexed-log | 607bbb12b72ee7ec2f10b19b210327da7ddde5e695d7e47b8ee3b3c430dfb3b4 | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/warmup/stdout.txt |
| EVID-5136d5ffabb124b654a4 | indexed-log | 1183f896113ccff1825cb95ca13d06dfdec25f56b41a671ae4d20d2e70ed891b | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-519ce4e0d1680278ec11 | indexed-log | 7bc4f1b08d63f1702b82d3b76d1d385531915686613588d01e8399d5a1155f09 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-52f1140de0665b83e2eb | indexed-log | cf096165943a997e03ab10db99d88c2d22ca0d47da8c25f3dc7b118ca6618224 | 702 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/warmup/command.json |
| EVID-5302b0eb284506b29b87 | indexed-log | 25d437e6d00068c197e2dfd494d37c0a99acab34832fff4a9c19cd99c7cd83d8 | 1745 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P2-stdout.txt |
| EVID-53637ca7b1c5f6152396 | indexed-log | 0d265bec7af85c99e9847963a87c240fceabc434e1fbee7e5e836dfb13ef6598 | 215427 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-53b83dcd95719a0739dd | indexed-log | 77ec2aeb9784e8fa44542c0849ffd4c90f01d0567dbbb08f088e288a76e7ff0b | 116273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R004/stderr.txt |
| EVID-53fcdb2ef4bef54d2a99 | indexed-log | b12431906348812fc8e5f80eee3048d668aa7ec257a030871cd98a0c41566af0 | 133510 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/warmup/stderr.txt |
| EVID-542379703687899894d2 | indexed-log | a98a3ad5f1cf9cde0c55a784631c7fa041ce25de7e2e7407864492df522d60b6 | 268 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-5474a373a71925a093e9 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P3-stderr.txt |
| EVID-5481cc010a055964cb1b | indexed-log | 18b5827cbe4de7f62b6348a84e5ff35b3dab7d04be1fa0e4e0ccf551dfc52816 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/warmup/stdout.txt |
| EVID-54ace6bbbd6d8b9b9e48 | indexed-log | 8c86e114a2df96fa97d2e0e27dea248b82dd22afc11e9e696f1b397e54343580 | 2475 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-R001/bench-stderr.jsonl |
| EVID-5646941597cd0045a707 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P3-stderr.txt |
| EVID-57008170c0b6508cfcce | indexed-log | 9fc404283b72e47d9f4cef50e17110ff1368de468cb87a96ee5cda2a31f80ab7 | 275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-57605920d1382b71b565 | indexed-log | a84d689fd6de7a50f722ec32f1e9c7f1601452d9711985254cdac51e59b30360 | 216094 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-578735f55b23c6a59b7c | indexed-log | 9d4288b6e144e0c319c17b78481eb215e04d01725dc02b8b0abe94f18d0a5041 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-1/stdout.txt |
| EVID-5810e509ec564ddd734d | indexed-log | 42134ec69be3ded345d6f89deda58c9b04a5eaf19ce71a587d3eed02abc4ac69 | 2386 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P3-stdout.txt |
| EVID-581b13e9b9cc87ae5039 | indexed-log | 19a228ce96c7787a40b32774e21488b20ecb050c012dbcf9f8e20530d1b9c64a | 215580 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-5827fa9fbf7f0d332310 | indexed-log | ff8b6e4c0aacd2210c639b3c414e6f9d2f3781c5ea35884965da8b2ab0126530 | 2656 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P1-stdout.txt |
| EVID-588861c3df6ccdacca7c | indexed-log | 1d573d1d1b18703442a1565ac3199fd3616d3af2acdcac6df8eac7601e743aa2 | 1433 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P3-stdout.txt |
| EVID-58db586bcd6b6b2f0707 | indexed-log | 7972c8023e57a9018ca9d906c4b1b1083364546bb480675866b568e86f3a9429 | 2213 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-R001/bench-stderr.jsonl |
| EVID-58ecc257b721dd3132fa | indexed-log | bc9ff92e6ac15d331e3a61b878d65a795686b631e35fc4abc171025ad4177eaa | 270 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-5a17150099b389dee6e2 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-5ac7155c067c33c9650c | indexed-log | 99e1a889cc4a11d213c153b2d1e2bfc2ed3540c31746b6073b6176b65bc53500 | 218150 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/warmup/events.jsonl |
| EVID-5aed9227d3108dd96ef8 | indexed-log | e45454073b2ba498cf1993531a052bcd7875297467b47b2967a8744d002f878c | 212301 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/warmup/events.jsonl |
| EVID-5b38a5eea5c549bdb502 | indexed-log | a97357021d4f73b084445c1b2515a7ec02f00bb72e3779d023fce716d8629064 | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-1/command.json |
| EVID-5c63d23358d712ba71bf | indexed-log | 792a9e507e42a8e9c304885f2bb0d099aa8da1bb951bf4801bc5f11258db6bbd | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-1/stdout.txt |
| EVID-5cf3811211ab73208379 | indexed-log | aff2a472c341d8c04a7db0a9ce301104c50bd0478db3e7187d4dfb58a69b00c9 | 116273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/warmup/stderr.txt |
| EVID-5f33b6d302db4a6e1df3 | indexed-log | 97d24d34fc7ac42189cc276d3dafce4c64ea5ee2837d76826e76206fce809b63 | 1686 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R003/stdout.txt |
| EVID-5f3c224324ebc7d65e13 | indexed-log | 3f45047729cd3419838107dd5bfedded7e6168fa20bb77790cc000f8da3a63ce | 120552 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-611257a44c89ac02dd83 | indexed-log | 89c981f1c8c90606e35e071d5d6dc8513ba696647027e82effb75a6ea9f4bff9 | 115126 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-3/stderr.txt |
| EVID-61166eff1854d46be5da | indexed-log | aa0515183d2f2548b3f68fc353bc8cc0ee0326742f0af6d367e82155be4faadb | 1890 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P4-stdout.txt |
| EVID-618ba83ade22cbecb68c | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P2-stderr.txt |
| EVID-61e121c613348e578f5a | indexed-log | ce301eca28216f66a27df0812b76c41a832868a70d02143e88c36e755a5d5248 | 275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-6284228329cfa9c3e5f5 | indexed-log | 99934d24c1be1651b5fc1e535be3b7d4e89e6d3602a90745a201862446f71b25 | 189886 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-62a30276ebef5a2484af | indexed-log | f015f98751427effc82c30f8b4b3a27f18a22aa36ba04deec30f50f3f5ad3f75 | 704 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-1/command.json |
| EVID-62af25b8ed4b321850d7 | indexed-log | 5f726d3f093f62aba3fa42accd60436151b390db440be535b9a84b33c9747e0b | 274 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-63622d05471269817135 | indexed-log | 86c6afaeb97686a2b6e6fd22457fa66d57c6daf550af75176065aea9a44e7a96 | 701 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/warmup/command.json |
| EVID-63c804ed52551738db02 | indexed-log | 7d076963e8eb14390686eec56d8b2c0a3aec25059efc9d615c0eeadde250cf3b | 277 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-63deec983a33fba90bbe | indexed-log | 40d9af2ce5d1083f11fac487d826523919583cf00fe8dba598aed6e1ea809856 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-1/stdout.txt |
| EVID-64355050321187ec8d1d | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P3-stderr.txt |
| EVID-650a1d6472d5dd48a83c | indexed-log | 0a14d4606cb15e73163a9bfe88a10392c45faa2f374c11aa2d530feab52b8d3f | 116825 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/warmup/stderr.txt |
| EVID-655098095e027e083248 | indexed-log | a7d0dcd1da292c61f7449f08d429f348f591ff3cf5b58e905b53a69d1d24edd8 | 1695 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P1-stdout.txt |
| EVID-6592afe8e5306edd8e41 | indexed-log | d0279769b5f96f2870e33476e495849da5818b3df02b3d9a57d078b9d27f1036 | 116273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R006/stderr.txt |
| EVID-65a3a7e31b728dabc7b5 | indexed-log | 0a07d9bf10ca5e1bbc12b34cb35f4497fb461af30980de26a03b41b0e41b984e | 2267 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-R001/bench-stdout.jsonl |
| EVID-6602a2992c617f2f52fd | indexed-log | 78cdeb40f34be60fd5ff2c1d6372f91b95f4e20ef2d84bba77626313de1bad96 | 103512 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-66d94ab7495edc7f8168 | indexed-log | 5981e270ec17109f8a807d3e04685ab13467661d654dce65916ccfd4ffd2d320 | 105640 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-67302b278b9628e8b23c | indexed-log | 48a0569e44d5f771de5a9708db7c692d5e9b55b0ba20f4b1aef91d1eebd6200d | 215672 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-678242ee055f1c848b01 | indexed-log | fa0eea8715defd179e1aa6b7b65f4bc74a42372897e82182c55efafaa5f1c656 | 1520 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R004/stdout.txt |
| EVID-67a68d92ec69b0750ae7 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-67fa0b5c20f247793763 | indexed-log | b61be8301d1e4780b746dbdcfeb656f4acc781027c2b22607fde3b782121d277 | 2467 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-R001/bench-stderr.jsonl |
| EVID-67ff1a1caa090791ab8a | indexed-log | e02efeda2f1f2a9801e91684e5c7fa622106d85cdb0fd147a0618d27916309c8 | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/warmup/measurement.json |
| EVID-682f1f3d7fc05d1526a9 | indexed-log | 89de88bc79848e69684719b40a65816afd4b65f3e9a518b66269f705e4325807 | 104355 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-687f1fc2a369f8dad883 | indexed-log | 70a27d6cd5b85a5448a8036f6206affbde0856de04d3ffe4a01710ee7ce07181 | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-68bd8d8f108b72075228 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-6a22026aabfc2f2b00a7 | indexed-log | 3f875a690bc9a77b067187be4861759249d948f2ea18f9587f1461bbc6fe5c9c | 133517 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-2/stderr.txt |
| EVID-6a255260bf9f133866c2 | indexed-log | fd26210f67e3a76a559cda3b96fa87afdf1d0ea6c99433a9a526521f2846b02d | 692 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/warmup/command.json |
| EVID-6a464db75acea9d1c74d | indexed-log | 7b2356d1480cc0a0147fcd1e573bd8ca6249251bf3d3af6ad9c278fdf6b7c31b | 188227 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-6a4996316e3aa1718b2b | indexed-log | d8d6928391ecbe443a0c7820b3bf22cccd309a980e88e2ddf81a16dbad0c0328 | 133517 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-1/stderr.txt |
| EVID-6a81ea4c0a07a452966f | indexed-log | 9a6959fc52e454731cda1fe26238a1b81f91714572ff5aa271b1a6f38c59c83d | 1430 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-3/stdout.txt |
| EVID-6b61c4d85ac5dc28b4ea | indexed-log | b2991a0747c57984dd5e3f58ead2f5c4ed789cb9a1761bd545d8a822d295056f | 116273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-1/stderr.txt |
| EVID-6bf3661642f62596e388 | indexed-log | 129e46211effb61021cf52e3e527b1cad66415e72a8a140506ff080e42f6558b | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-2/stdout.txt |
| EVID-6c19cd913b4347b2c9f9 | indexed-log | 35e9a840f21cda19e725e314d5e652b99be97af58d8ff2fe70d6e14c82d17b78 | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-6c3d040b82c8b5e672a6 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P1-stderr.txt |
| EVID-6c46387f8fa956313c71 | indexed-log | 76420d129aef21af187c844b9474af3d9f115081cdd0d2cb82dd5447422d076f | 115126 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/warmup/stderr.txt |
| EVID-6ca22f8cde4b8bd044ac | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R002/stdout.txt |
| EVID-6cbe8f7a0fc90701b7bf | indexed-log | deb6df2b5130f7be1c9a3be31223506df321eac0c7352b652fe49513b70ac839 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-3/stdout.txt |
| EVID-6d01128a07a8c7d24473 | indexed-log | a7b7b57ff0bcb366fd66d5dba07da5c0123b7381bf4a34fef257d50205c32a86 | 216507 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-6d62704dd08a21df70c5 | indexed-log | 6cfcef492f6cee60b3f70ba9157aead6bf454c75a6eff5849c9d72d85cca5057 | 219800 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-2/events.jsonl |
| EVID-6d83da42e7c36d676765 | indexed-log | 2b9d6a9c1bdfce1b7a978d5445615656d4f4523057a50e141fa2951a26e01254 | 116244 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-2/stderr.txt |
| EVID-6de921efdb12aa826110 | indexed-log | f673ee97a63362d762890b532fd956241040eb3102780963cd27f9ce0e378054 | 1407 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-1/stdout.txt |
| EVID-6e2d878b9d380b0f1b37 | indexed-log | 67e909740f199866e9972c24cca63095d313168688fd6693cd19ac3d2133a45b | 104100 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-6e3e53a8a2df89c6a7ec | indexed-log | b75aca25fccc9cf874a436cc06e035ca158571283ad512c6b02b2ab4a7ad16e5 | 2218 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-R001/bench-stderr.jsonl |
| EVID-6f97a41654bc4d5610ff | indexed-log | 44291c53b68ce4f90756b76050c4fdc315cf4415443ca6355473f4742b44da3c | 218077 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-7089679d0bd6775f6a45 | indexed-log | 661675aa8571b3f2e68073594c4df5ba5e12c0f0aa9c53767a9be15154fb9129 | 104093 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-715e926421afcc86825c | indexed-log | a486da3ec024588dae309719a9b5fe5e42f3a6378825ce24849df2573a4dc4e4 | 186361 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-7187ee95ab7fcead4806 | indexed-log | 4bea415cb37ed57534872ef8b3777743dc48d4a0579f9d7bee4ff18b4cadb5bd | 186341 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-71dfb215144fef13f09c | indexed-log | 5d0de6d6c062fd8f9b6026284e58c97255f3245f426ab0ddd3d7295fa5933963 | 115260 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/warmup/stderr.txt |
| EVID-7207ba6e4af8412844ca | indexed-log | e84f17a14547fd5e6033a4856db2cf17bc4689fc87b13f26e7273a085aafeaca | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-726eefab2af5666a755b | indexed-log | e5bbcb55dfaf4a752d089633e05f1cafbad260fbbebc1b701f1d5d41035262ad | 209950 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-3/events.jsonl |
| EVID-72987c011301d7f5031e | indexed-log | 05a4e93b072a1266456efa229cd7fe7943c90c2e420c6405b4c8af2b34819417 | 700 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-3/command.json |
| EVID-72ca63c7a0f1f428e469 | indexed-log | d702e760c5926f4147e7facaa7d3ccfabe49188db4c922d0c8f5837867957381 | 212457 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-1/events.jsonl |
| EVID-730c907202aa89fb8a65 | indexed-log | f940a049232ea38d87cfba6266abe722b818c9a1dacf2841c6f62f5e9c007394 | 186505 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-73da23182ee6926808e7 | indexed-log | c6913c19a5a90a0c9c3229235b12b450a49f3c8c160b9a74b0e4f4415fb1026a | 211055 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-1/events.jsonl |
| EVID-740468bebc32140727fe | indexed-log | 5d78ce7a3d8fc755bb07a6fd6b1d3b6d947f0e5d7a5fad8c5262d1c62f2d76a3 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R006/measurement.json |
| EVID-763498f885d234193412 | indexed-log | f3905a1c4416b3237a60b099500c3794acc65fdf7b99e9015f4d6d8e9abfea9a | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-3/stdout.txt |
| EVID-76f1541111f3c203c49a | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P3-stderr.txt |
| EVID-76f49468f82f41ce1d7e | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R003/stderr.txt |
| EVID-77412c5f5d2c1bd20c07 | indexed-log | 5a1a061084c69da5d83dc70ba56e20f85ce30b9af89b189a5fb9cc2508354db9 | 1434 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-1/stdout.txt |
| EVID-77b6258a2c3e149aa96d | indexed-log | 07724b0ae4b780971d0fd487ca7020dd690413419aa38686248130c8bc4b25c9 | 221637 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/warmup/events.jsonl |
| EVID-77f02d88aa0f74929e7b | indexed-log | 163375e1016fc451fe94c401364b7aeb5267fa56f14b12479f1bb99f184f5247 | 1422 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-2/stdout.txt |
| EVID-781696f29aa7e02af308 | indexed-log | 97535379d622a80cd2cd13c30ddd962b74ca541fcfa89d4ff5560eae0682e61d | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-2/measurement.json |
| EVID-78ad19c26cc8eef22652 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-78c1df7b1f7bb713f351 | indexed-log | a52f84d4adfd3c835eaa9a9495cb16da78c2b8e22d505535e7b767a0577084aa | 100912 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-7930971fa0782c8549d7 | indexed-log | dd1ebe18a4986f570f8f1c0bfee9e95969205cfad0869e8646a180669d837cdf | 116220 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/warmup/stderr.txt |
| EVID-795392d33f04b9a33e51 | indexed-log | 9e9e510e9017ef402298f3eebc190bcfa43c5b61fcb967f279d769c1fd75aa6c | 1843 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P4-stdout.txt |
| EVID-79bb6ff9b451c38f0778 | indexed-log | 3b8a50971dee255411e03fbecd85a4a68f4b8d315bf4eef4e0624671acebe20b | 2193 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-R001/bench-stdout.jsonl |
| EVID-79c9e385ec5206b71143 | indexed-log | 5b35e724395d0a9b1aadd45a7892d5a3c7addc7ce02053745b27ed26ef52d7aa | 242454 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/warmup/events.jsonl |
| EVID-7a9a10426c556a89b18f | indexed-log | 6528df447f3c5d5bdf5094f23af88717018b5114712136faefbf764a88b6c7ac | 118338 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-3/stderr.txt |
| EVID-7b0512b10496956cfdc3 | indexed-log | 09e38b826edd4af8b0d44a5f2d81b24f7f11c65e6946b017d774c9373873cde8 | 247471 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-3/events.jsonl |
| EVID-7b4915636fcb8fdf9346 | indexed-log | 6855c7231e4eea1a50c1202ec944f532760f510f2751ce4ef15858a8ac2632a9 | 696 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/warmup/command.json |
| EVID-7b9ba09e482e34511017 | indexed-log | 6e781e53521d1856dbc88bc3c47f4b393722acfc381b9abd864e33a91fee2d07 | 1410 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-3/stdout.txt |
| EVID-7c8330d4f6b3313e3cb5 | indexed-log | 92687783290cba35edb979cfce8f1e325910202a28aa985441cfbbea42393929 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/warmup/command.json |
| EVID-7cfde05cab69d013eaab | indexed-log | dc65426f8a12dd6c85999b0cc5d9823487b9d823368f7b20df8df4e60dea7005 | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-2/stdout.txt |
| EVID-7d01cf406e852f9deffb | indexed-log | b75aca25fccc9cf874a436cc06e035ca158571283ad512c6b02b2ab4a7ad16e5 | 2218 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-R001/bench-stdout.jsonl |
| EVID-7d3563495ac482e59af7 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P4-stderr.txt |
| EVID-7d3c17d8c59ea344c14d | indexed-log | 73afa3b464f1be40fc4dbbe0bef003cb4bcdca6533ae8b09e50eb7567bda58eb | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-3/measurement.json |
| EVID-7f6b9e1dbd62d88b8eee | indexed-log | 03337083bc6f78d995582f93631cd77614a577ae4b00989b72706bada00c1e8d | 115260 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-2/stderr.txt |
| EVID-7fc3be7e4c665f755134 | indexed-log | 9bfc138b88ee86a0d4fdc86bf90063b40d50229bf4bd75de9f26628ee0cdbca6 | 1410 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-1/stdout.txt |
| EVID-801ffc1e8e4914b84773 | indexed-log | c6411f37c36e711e9aa11a550ab5231f6ec3d377cdea5a4e2ef0291e5dff8371 | 102315 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-8022469f50c7b6de3324 | indexed-log | 9733d9262a8b805eaf386a14e502c741b4aa97798d13aeafcde3d345063ac985 | 209977 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/warmup/events.jsonl |
| EVID-8034b0b1c81a51ec592a | indexed-log | ea142917f345024c54ed8d9f3e0e623cbba8ca41819290582957ee4e24734e36 | 3110 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P4-stdout.txt |
| EVID-804c79f4d5af53f04759 | indexed-log | 9822264067dd77d301e41571b042a65694a82459e145ea8bdee91fd6234925ca | 133357 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/warmup/stderr.txt |
| EVID-804d30a13963833726ed | indexed-log | c9aa5c6b005146146be21418720ef51cf8fd0684eb52b14f9787aac4228f351d | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/warmup/measurement.json |
| EVID-80b20222775d4b2a0d12 | indexed-log | 3265b192c1c224aba29a73d0c5a8f2f2b957785ebacba97f7ca6cd093c8afcf8 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-3/command.json |
| EVID-81532d951476e58cf248 | indexed-log | ef20d271d31cb904672e3f1f1d146c902ccbeca6dcc42e79ad0024833317f522 | 133510 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-3/stderr.txt |
| EVID-820dc4549afbda0802b2 | indexed-log | 94a867e700f503d04a3fc0f9b43fe39e4bce2bc41549a7081faad4f1eccc7fb7 | 696 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/warmup/command.json |
| EVID-8220b5345db5377f5893 | indexed-log | dd4e97a8286605581adf92cabb1844afb37c841530b3974d10694926ca99f84e | 692 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/warmup/command.json |
| EVID-82438280bd32346593ea | indexed-log | 8c5f84d2e75c92073fc4fc3cdd7668a65c559e325692ff533c979838bd874953 | 1456 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P3-stdout.txt |
| EVID-82fb765062ec03a5e6f9 | indexed-log | e7d65c7a2dca0cac1f9dd632da3e1b9e941a39d76eb8935256fb1b2e487e780a | 1434 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R006/stdout.txt |
| EVID-8311f8eb81f3ca4f0501 | indexed-log | fce5e2ea2dcd931728aae6553d2dad4aff03ab50056c7fcef3f613e885442959 | 246145 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-2/events.jsonl |
| EVID-83756450e27a8d04475f | indexed-log | 33aaae48bfda576be69c2131236122aea139fc96b07546411923380b89722754 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-1/command.json |
| EVID-838810eb07e4503f8374 | indexed-log | 8200bed20ef518121d27cb8105175cf3367c6f2067cea511c869761cbc2463d2 | 104093 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-8391e44c42731ebd924e | indexed-log | c52954e3b4b8905580ae3bd9dfdf648150e09a56aab5c1f27839dd44f51447e0 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-2/command.json |
| EVID-8459537a9c4ee9d08fb3 | indexed-log | e06cf7e7cb3e211640d8cab853f381482ca20640f306d6a26781690d4349825f | 707 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-2/command.json |
| EVID-846dfc0d3f3db4e525a8 | indexed-log | 76989a9604c913051bb5f5d1667ebd9c498b883d1e8a821e5913f0f9fb0341ec | 704 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-1/command.json |
| EVID-84763c0ab1b9bf164104 | indexed-log | f8c5feb06edc016eb7acfc53dfe1b6c4e650eb2732c7628fd30102391e9e4d25 | 100776 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-847e5223bf0be272564b | indexed-log | 95017be7cedae3ca53ab840e252e19383954ad773cbd8c6cc5e843c966adad93 | 700 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-1/command.json |
| EVID-85e294b312470df3eb78 | indexed-log | 498069e97897abaad064d7f3297a46e1ad23c1c3d9eda7577dca447aa26a6ab4 | 1434 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-2/stdout.txt |
| EVID-8694e9f3359db7405677 | indexed-log | e783961c89df7da02d2756771fa30bad946c20f2f77f230023ef5086da093e0b | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-86a29bb6285b4c1591a9 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-86f650b1330ae446696f | indexed-log | 92ad3371f04c7f06c13a6bba54714e9a819a450d1e3db7125aed7cfc99f29d9d | 1409 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/warmup/stdout.txt |
| EVID-873ea341e38bae06041d | indexed-log | 80d155dc17633f1502995f3dbccb02872bc9184f6c937a0f73d6dd400bb41247 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-2/stdout.txt |
| EVID-87a9a5c5ccdbc3c16734 | indexed-log | 7294b07d9f61fa0653f27fde01dd4ffcb66ecb95bdf3fc12472aab751dbc6bb9 | 275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-87b6ceef3db5418a41b1 | indexed-log | b45973ae83a940721c65eb2bcbc005c4a320b2f3f345d4cb5aa8a758a8958b97 | 100776 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-880048c93d39a37c8727 | indexed-log | e5fab02157884a36a15b9c13403a288a127626c76a22d1d84ec4d27deb793101 | 104355 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-TTFT-CALIBRATION-R001/stderr.txt |
| EVID-8835dfbc7ea7faffa623 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-88a0432d5701f1dd4484 | indexed-log | 2f6851da74a65e000e5c4d3c11bf261433bad52f72fcc6eede5b007fc2852ac6 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/warmup/measurement.json |
| EVID-88d69d1177b7c2bcaf33 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R004/stderr.txt |
| EVID-89199536b0794a788322 | indexed-log | ccebe119e88a8d4981a3fe84d2b34b6171104b27742d301a3888169ef79427b6 | 184361 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-896bc9c6d99616165325 | indexed-log | 94eef6c5aabd3e67f2c340df65b0efc2ec338ec5da2baaecd565f4914ec1b7d3 | 704 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-3/command.json |
| EVID-89a9178f7e777fcfa271 | indexed-log | a86e83b4e94d4a33f92cb7421822824c3c8aae85e250fb81b9b09186d65d57bd | 188 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/warmup/measurement.json |
| EVID-89e35166ee4f5e3f2502 | indexed-log | bef932f44b381541731c0a7d349f04ca136ed47a6cb65a97642aafc977ebdc57 | 212624 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/warmup/events.jsonl |
| EVID-8a07c37dc3627fef9661 | indexed-log | 3c4a573baec5534a6a891a4ab225a9eee298020edcd52ef982d77dfeba3d6888 | 120552 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-8a4d362e54ebbf4f6ef6 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P2-stderr.txt |
| EVID-8af62d2b05d8e93dcb51 | indexed-log | ee20888573513779e2c75c6eb30f696737ca1fd6229badfa4ecad83c6557ee42 | 277 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-8b2bf3517ba328a5e857 | indexed-log | 0684892a8a5d1191ddb34199744ade1797be0fd4af5b25a3fd9975cd6719d4e1 | 693 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-1/command.json |
| EVID-8b965d5d71a2e37e6363 | indexed-log | d40dc23895034fe983511506878bed563ee7ee245a6dc0933ee067e2701ba144 | 1875 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P4-stdout.txt |
| EVID-8c2235fc3767d140f34d | indexed-log | 3fa4b50f0a06a6cc7ca5f96cbcbeb474e37a28928f9ee7b16de1058cb4d046d3 | 702 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/warmup/command.json |
| EVID-8c9944dd141e04aad5e4 | indexed-log | 1c80f4cbd4ac38de6d8ece767263219677b727d7a1ed1cdd0e3645630cc29551 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-3/measurement.json |
| EVID-8ce7ca2a39b0992cdc51 | indexed-log | e9740914c569e6adabb2283fd9e41a2d4b6553f18ca8cb23148be75725164983 | 1697 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P2-stdout.txt |
| EVID-8d0aa3ac45f9b6f001dc | indexed-log | d687a3563e0c7ffa032c5eb774ad7096f7a23e73691df145b7ed078a32bc74a9 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-8d1d59d6855843f07a3f | indexed-log | 695063973f88271953dd2ee560051e1bbb250a998098e0bcd884f27c2c5e8634 | 184919 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-8d7d0d5d7adbe0443679 | indexed-log | 99f80d0670499ca78538825e4165ebeb373a838890359dae2cd78cd73298e602 | 193 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-1/measurement.json |
| EVID-8df48ab81b6f8acba4d9 | indexed-log | 7d41f06f22686c502871263724705883cafc222cc7da675a025e8b9a3e8f121a | 120548 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-8e60b5385bfb8d37aebd | indexed-log | f6218ae00f7627323bfcbdfd454fc23a79ea67048c2d9bcb3d59abf270d35f33 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-2/stdout.txt |
| EVID-8e8e349a5ca1feef28dc | indexed-log | 3b8a50971dee255411e03fbecd85a4a68f4b8d315bf4eef4e0624671acebe20b | 2193 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-R001/bench-stderr.jsonl |
| EVID-8e98d34c6152839bdd05 | indexed-log | 23ea77c340e0d1218e5ebd32a8c7112529f6fd428243fbd5b9ffc2b355e189dc | 1686 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P1-stdout.txt |
| EVID-8fbab45320fc32fa6d5a | indexed-log | 0da80f1345944bf500cd97a75d0915fe15d47cf2337842b025abd87ea88d7517 | 188 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/warmup/measurement.json |
| EVID-90cfc29fa7a99a7cc0a8 | indexed-log | c931d5673baee3b23989a9155102ae561b05c819d6037e3db1da15d1021f1a3d | 186932 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-913c862e4e0ba9f3859d | indexed-log | 18adcf300443bff1fa822c992fb2f96f00325123924e43f60c215936cf5db0fd | 210118 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R004/events.jsonl |
| EVID-91895652f4a3550866e6 | indexed-log | 2a583d8c5af4489958dfa2765de61ca2b548b564b425c6740915322376ecd2c0 | 184386 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-91abae1e4c4034d97888 | indexed-log | d35c9e2dfa3f83899045d57849d2fed185e24243393f23191e58d43f6b6d7246 | 1755 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P2-stdout.txt |
| EVID-922fe80006d168c5c124 | indexed-log | a9cff2fa9be5004abc1451a5860e0cbf59e2bc8f7f8a5e61c89e2bbad2203407 | 2272 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-R001/bench-stdout.jsonl |
| EVID-9276334113d46bf4a9f5 | indexed-log | 416dfbad18ebe2bfaf64b272ef3a78da2c66e9986e3c42658a6e8b3a3d8f1374 | 216519 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-9277194253472ee7acb6 | indexed-log | 464675ebb1ef5d162ba84c1902a31a8c6cd41530d6b489b6488df47119c0119e | 212521 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-2/events.jsonl |
| EVID-92859daa664ecd19372f | indexed-log | 8d659e5d16d72b0a2603356d5205582b81cb4995ab0b49485386aa808ec1218c | 190266 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-92e19df6f16a983fb1af | indexed-log | d14e1fcb59af13074dd6bdf32cca07fab0f7513594749d8949bad664214e0cef | 1409 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-3/stdout.txt |
| EVID-92f2ce5379478eeb9619 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-92f9b8fd755d254c19a5 | indexed-log | 2fbf1d914e841fda5ff348d5fad94a34db4c5f7d4a74631f2c4a82b89ce08589 | 200 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R005/measurement.json |
| EVID-93487abf751fda60c24a | indexed-log | a95be3a569be851aa1773da424b08e3b486a1d262c26f31efa7d814f678e4297 | 133357 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-3/stderr.txt |
| EVID-93693c895747c18d07de | indexed-log | 5008fbb3ca8b3761fec6dbf4baa0f11af8c8b7828330aa528e112343e09371c0 | 104355 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-95ae8738ac07f0b1a50b | indexed-log | 518289a96f6b7380420c5f9337fb030c2e3bb1f92aae70577105858fabedc515 | 703 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-2/command.json |
| EVID-963c9bc62d46b5807b3f | indexed-log | 003f60959382e88b360e9eb8a6781f7243ec5a59476f099daae168c04629c595 | 283 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-TTFT-CALIBRATION-R001/measurement.json |
| EVID-971776b67f432946b411 | indexed-log | cfee131b748b6d7447af94269c1209e32b1a76626723de9126724d698e20e0f1 | 2218 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-R001/bench-stdout.jsonl |
| EVID-9755cad6b59f220b8ca6 | indexed-log | d2e304119bd1919030b4535dfc47efbc919e9cf5965070e0ebb3a5f4858d5efb | 217554 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-3/events.jsonl |
| EVID-9764bd986aa840a55bbc | indexed-log | cd3534ae7fa3056a6680b4d97a6ecb047d52cb9b8f28438b44de61628744655a | 116244 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/warmup/stderr.txt |
| EVID-97bca65896e91b10fbf1 | indexed-log | 7532f8c5136ad3a4267446dc28c831cc4629dc302e09e606fc6900e1b8e2246b | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-98022eceae0631558b47 | indexed-log | 9af5d7efd9c703ad2af29ee411cb75a8abb6d9587895192698d25b3a58d7f56c | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-3/command.json |
| EVID-9999a50fb695e7f9d19f | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P1-stderr.txt |
| EVID-99f64f955c70bb5c2dba | indexed-log | 6ddad7e36ea702c3d7f5c82fdaa716a6203040a7175aca5393d24e709b83b72d | 189 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-3/measurement.json |
| EVID-9a921c1af0b448580558 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P1-stderr.txt |
| EVID-9b6616c2134ae4ca7f91 | indexed-log | eb393f771ad5a6ec6ee21c92c9c03bc0b98450c81496fd4502240e1135b1a508 | 2632 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R001/P1-stdout.txt |
| EVID-9b6b216340b62b3595a8 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P4-stderr.txt |
| EVID-9b6dbdac698441afc8f5 | indexed-log | c5988002a79ade62629a1cdc1fc899bd6f6c5815ce295120661d75c0374502b8 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/warmup/stdout.txt |
| EVID-9b80700b1ee50fa80a67 | indexed-log | f96c8df60f294278a6624e97ac92306e35bfe31535d7e0f1d6d80d03b2f52e6a | 1422 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/warmup/stdout.txt |
| EVID-9b9c70e0be2a799ff9ee | indexed-log | 2154668427074214dcaed2dea2c368418f6b8d77e63484b87c4d562b03babd32 | 241325 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-3/events.jsonl |
| EVID-9be12374e2f3e7597a9a | indexed-log | 056805c94daa2899c348d3bafa3905a274e9ab6dff1574b2c97322d62022ee8e | 186309 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-9c238849ff688cefbf41 | indexed-log | f74fbb67ff8f3ac103fd852c1ec3aa8e1ac87d5c171f4d937d8b83bf4168d517 | 192750 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-9c533d97786dd6f10879 | indexed-log | ece7840a47d3fa2208811c7df0a2aac3b1139b3f7eb7e4b536a7319bdde782b9 | 118339 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/warmup/stderr.txt |
| EVID-9c764139aa979c9eac21 | indexed-log | 973dae18f182131239350d51962619050a830e6360162eef2b02008b44b91018 | 245777 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-3/events.jsonl |
| EVID-9cc9d3d1e667ac04fe16 | indexed-log | 99952d45482f9c7736d40a6b299c821df5f24c4e9d06525ca52cb271082cb359 | 700 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-1/command.json |
| EVID-9ed72f9921076ffcb739 | indexed-log | 569507f01107177dac3c494a1f25548f9db7bd164c285facb748818b676cc206 | 145 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R003/stderr.txt |
| EVID-9ef31ab5288bf4475c33 | indexed-log | d125a99e578da49005cbaaedb3a003c8b81b9d422b1fd758f3f2a3b1385cc862 | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-9fbd9805ec789ddb5f65 | indexed-log | 6615dbf616561f1525f4765bc957a4ab080a1a341be9b2b6e679080dff79741c | 3009 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R003/events.jsonl |
| EVID-a0519742694179e614d7 | indexed-log | d3180feab0a74c2a669d3c69e9e2970f91874630cc813917659b5e0347f3b0e3 | 709 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R006/command.json |
| EVID-a0c3c63c531b792b911d | indexed-log | 679920230a7e57aff5e038efe6e3b382565d272b7277d92c61650e403372a4ab | 707 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-3/command.json |
| EVID-a21529dc782c2fc88a37 | indexed-log | 3e8dcf6ec8021dcfb9f9a6c2d99fe5c2882abd8ba4cd90b2fcfc541cda49605b | 1825 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P1-stdout.txt |
| EVID-a26e21391aa260e708b6 | indexed-log | 697e1a3ea50d6c9cd53f662b25564217900165e39287ea2e5757ad7aa57c0811 | 709 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R005/command.json |
| EVID-a2a55aa23b02e5e13ee1 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-a2e3913cc1b93bacd565 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P4-stderr.txt |
| EVID-a2fc8e1a0348dfe323d2 | indexed-log | e5c14c77883d06cb1b04bfa41c3805477cc0faa25fb021a743c7aed73d387704 | 1564 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P1-stdout.txt |
| EVID-a3117c83be2c9a595c4a | indexed-log | e3f8cf1c04d916ae5522f45e6b90a17810355c50cb42998dca989430376a9e09 | 119151 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-a3209c4a331098b34aff | indexed-log | 1f357759e8e4142eb8634d720566dc05d52e441b7507adf57be158469d30e14d | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-3/command.json |
| EVID-a3610920c58945bbd9a8 | indexed-log | b987571127efb4de991c4d3c168fed3f1a9335513cc4dba5291f28dd70c6d832 | 279 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-a3ee11e19dc768ff3b67 | indexed-log | f113917c92a2689fc60f6ccc83f1182ca3db372f4f86b52068ac01efa402c571 | 240914 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-1/events.jsonl |
| EVID-a41106468b58ca026ed8 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P2-stderr.txt |
| EVID-a691d82230e4a3ba3054 | indexed-log | 9cac8d50db89ebf99639549b3af336d89587d9e8865a6f059f8b4727a4018eed | 1642 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P2-stdout.txt |
| EVID-a6ea2569f00daacb7857 | indexed-log | 1c1b091a4ee837451216f457246ba285619515a71e85f9ac7da07bd254947b55 | 700 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-3/command.json |
| EVID-a712512b0fefdc13986e | indexed-log | 1c724fe1a53fbcf93f946ad79191ace2d82f62f2f46bfabca35d99ab627b8707 | 274 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-a77cba7447de9bd04652 | indexed-log | 5ba98bb4e045d798c9893d5f9ce8f4488f53380a559e072002dec84cb0f334d1 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-2/measurement.json |
| EVID-a7826325dcfbb646f7e8 | indexed-log | 13ace339d5cacdce10313bd4e184849ba4a94504353b9aadc35cd2c3221f03d1 | 1430 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-1/stdout.txt |
| EVID-a791e2809a1f392369f2 | indexed-log | c9d10a8e5df9857ace0345f609a0c22990799425aec313aea0bee4a5e9600ea5 | 1843 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P4-stdout.txt |
| EVID-a8f5bcd3955b33cb5a76 | indexed-log | dfd1bcaf1e97f8a0a4eac39f5a1376ad0a65f9bae9ac361873573ff7380c4b54 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-a98f7e620244a57f778a | indexed-log | 6a184d21da4277ea9e013de5c00d561de6a38141fd6e91ea10542fa97881adf4 | 1430 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-2/stdout.txt |
| EVID-a99237fef7d7fc8c866b | indexed-log | c7f451424ba56fd4f63052821c7072d70b0c4c873329bf52409d26f6fae84b43 | 116273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R005/stderr.txt |
| EVID-a9c3f8b0e3a331c5f0d0 | indexed-log | 556f8d9117222e76c50424c18d66db319ea038106476de37ae674d966138da1a | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-3/stdout.txt |
| EVID-ab05e12cf59f556ab310 | indexed-log | 737da615c1cc9602320a40c28958fd5c36115fc3ce3c8e74e7ca723fe48dcef7 | 185920 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-abd725f59e5c77556d9b | indexed-log | cf1d7bd0dd9f1ab3371274b7d78b7c46b380bc98cfdd54346a8c8857908f2968 | 193 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-3/measurement.json |
| EVID-abda4f1d29fe9a36303f | indexed-log | d523f4e6a1c6d501b2d62fa79c93cab21f3d9147240319da52a5b5582561c21b | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-ac69b64ae017f9388a2e | indexed-log | a00a49ebf26417c88686d716ab230f91d8e6f274cf9d17f1301baacee32065c7 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/warmup/stdout.txt |
| EVID-acdcce438e2ed9b818f4 | indexed-log | 00170a1d73d3a5d22f29fd52049046ab57b0ba6983a5b824b38a7fd50fe9b905 | 120548 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-acf74ced79f63ac3d541 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P3-stderr.txt |
| EVID-ad00b401c4d14600c8ea | indexed-log | d5768410c9250bfb8a87b6a61271f4b9ed0a8a1e5ebdf998ead6469d514ab437 | 101982 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-ad6917b2f6add381ce8d | indexed-log | ca4a54f76f59f3b3cbf8e3cb6f311b42ef7e23345d3a1fd360c434723e541dde | 246364 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-1/events.jsonl |
| EVID-addabe5127b45d5b5079 | indexed-log | d5301e712db81a47d677dfa9a61300e31d5741597db4a071aeb8e1c88c0e0d27 | 100776 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-ae904db8870f8bbac180 | indexed-log | ce3b6dbf3a2a288cb0c0d845f8bc304a3673e0a0093b642f3ce0d0df78d4b8dc | 133510 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-2/stderr.txt |
| EVID-aea1bd923757ec615910 | indexed-log | 70bd1a8d047587fca2b617d694db40ac75f27d49c2538f771e7c4f3444e33cd9 | 2223 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-R003/bench-stdout.jsonl |
| EVID-af0fb06308ec367766aa | indexed-log | 818f66aabd695699c008d624047048abd9c5b5c7b89c364358922f3131b43cc7 | 703 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-2/command.json |
| EVID-af8c18bc5e05b0347dda | indexed-log | aa7f3a73eb9a1f087fa2cd5d34303a76ed6053548d9e6931f98309817148ff74 | 1538 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P2-stdout.txt |
| EVID-afaa09a31104fcacec7f | indexed-log | 07b2c1cb953ee72b03d2c3a05f73c800a98100773fa63bd65908d0d86d46be90 | 193 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-3/measurement.json |
| EVID-afb7ee229fe0148a0b83 | indexed-log | 0095b7995975d2a2910f35c9410383fb0d0965f461e69ac6a2a2393e57d9036d | 189 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-3/measurement.json |
| EVID-afdb49e8741853ca7fb3 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-b0f26c60b130e6e81def | indexed-log | 4d3077734d3788b0be17f374c4875b5dfa2ff8da005b11f5777c0d1237fe50e1 | 1429 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-QUALITY-R002/P3-stdout.txt |
| EVID-b130ace487458ffdd48c | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-b18275d39fd5319580a9 | indexed-log | 0f0186d975eb086e83be548fa263ac0603e6e5b602558072611e0333a39959f8 | 1890 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P4-stdout.txt |
| EVID-b1a527cce79eeebb34fa | indexed-log | 055648bf5ca1114845051826b9baeeb2633739fddaa5d5e86614e0131d49291c | 212452 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-3/events.jsonl |
| EVID-b1cb35f6efebf4ba7c70 | indexed-log | 3684d1020bdb3b73ca8bee90f031040e0a25a4230c2d6d7d03f0f976516aae3a | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-b281b182199b9fc18309 | indexed-log | eff5c87f88d21e41eced5eb5390ef708de477ce35668d17e9ae3465ed908375e | 184927 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-b2cd2ce501adc3a43125 | indexed-log | fc605fc88653db07c709f1202a6af481cc6f89a1ba7121765d044edb6ceef951 | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-b343e870073146e94f3f | indexed-log | 3e0c8ac2b7a4eee5de2373248580e24d5f8daf14b5d28017b04664b7c8d13b2a | 215783 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-b3d40225e077704ff49f | indexed-log | 83f7488b88d20f668ad8be0ad83088226d839452a1439f8990ab48712d3de6f5 | 115260 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-1/stderr.txt |
| EVID-b4550e202105b9b86781 | indexed-log | 15f8cc1d4259dc2beabe7870e5979889d065ac5d11aa576b29956629c35e72be | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/warmup/stdout.txt |
| EVID-b4a16bd806a44a4651e9 | indexed-log | 3c010c4009a9a19dcce9414cb6f7ddb8af7df42c4b15b8fa9626a8e8b6ebd163 | 1228 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P6-R001/P6-turn2-with-history-stdout.txt |
| EVID-b4daa6cf5d0cce41d454 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-b534bfc3010c965563d9 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-b5428f5c034269ac6864 | indexed-log | 92cd7c53e50645d58324f30d12ca9cfb558478d3114679113ee2b94699b5fe18 | 119151 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-b59f8455a05f3602a0ba | indexed-log | 318771a42c8f0be532d1b244c1484605f76dccd7019d23e986c56475e5f818d9 | 2191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-R001/bench-stderr.jsonl |
| EVID-b5bb987e0f0ef0a1b23b | indexed-log | 0a5deaa2f54efd041fb14d5007407abfaad02ea0dfe5042ebc9a5e034a05c751 | 211105 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-3/events.jsonl |
| EVID-b5c621836fa32e57f686 | indexed-log | e27cd82c7dcd8d639007736e08b3612dcd83011c3eab4eca502a6f45be301c98 | 704 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-3/command.json |
| EVID-b6147a370f787075c162 | indexed-log | 147e931fb0a9c46a15bcbd90752a557f56fc8f659a0f68781fe25ebdad293b03 | 116825 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-1/stderr.txt |
| EVID-b6e30e353fcf8f51e77b | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P2-stderr.txt |
| EVID-b705cdc6c9dc9bccde07 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-b7a983765afd38108a12 | indexed-log | 2038a8a616d5d26517e3ec90f7d1b1a4c1047f0cdd046afeb76893080706285c | 3200 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P4-stdout.txt |
| EVID-b7bd014b0c162a0cf157 | indexed-log | 4469b9825cac61ca6e456a42b871bd9c712f26c7c40451068cb3c6918e7292fb | 247586 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-2/events.jsonl |
| EVID-b8416e35c7b77ebf44e9 | indexed-log | 2ddfa6468a2720b97cc788f8bdb5a9948c40dfe6974545bd325d7a876d129279 | 215551 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-b84fa4b519575e1da8ce | indexed-log | 8d1d6729cb5c31aff6c7dc3ca7c5e207af0b1ca6ef9022725dec2632fd76e812 | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/warmup/stdout.txt |
| EVID-b8d263c57716b72bd5c5 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-b8dba38be7991a469f97 | indexed-log | 71f78f04e17eed949032a3b92f99215bc903f2c6037be46d1ae0856d647f22d5 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-2/measurement.json |
| EVID-b942c9ca9ba7da70670e | indexed-log | 8a2fe08e6340ec6c0641b5bd25b716360244ef978b44b695d65b67d69ecf8c3f | 1470 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P3-stdout.txt |
| EVID-b9452a65c823853b51ec | indexed-log | 2c1cebc00d7e81898ffad37d08867514d53acc38b2b41b19099a4a27216cd264 | 249415 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/warmup/events.jsonl |
| EVID-b98c4120b95cd06ee96f | indexed-log | 032b03cbb908b332a0872cd392592409c5cb5cdd23a9f6c13cfe4f9407467fed | 1434 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-3/stdout.txt |
| EVID-ba016326a2686ade36bf | indexed-log | 0ca29f0641a986ba7a2d0d02477a867c8bf8c91439a40fe9b04e0594a10fc13e | 185600 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-baa2d597e7b72e2c357f | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P2-stderr.txt |
| EVID-bab4408e56146cde6958 | indexed-log | d4f85ef9bdb6579119f61b8efd2904f5051a2da85664b2f71ae627cd7883f90f | 1407 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-2/stdout.txt |
| EVID-bad0cd2063671978b18f | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-R002/p1-stderr.txt |
| EVID-bae2f882f1d64ce720cc | indexed-log | d61d221e26424d976bc336211e6798809af1d169f1f7fe11fdd759d2bc6ffffc | 1413 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-QUALITY-R002/P3-stdout.txt |
| EVID-baeb3e52c3c6f9ee973b | indexed-log | 13e0264faf17bfe38f4bbda180935f067b60e27e428d24b42b6bfe6fcfb85cb4 | 2219 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-R001/bench-stderr.jsonl |
| EVID-bb4c27c65a53cf20360e | indexed-log | 9b1142fd0ceaf5206d18d0ae31bf136907dfa08950fbca458123bb0a419be2bc | 189 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-2/measurement.json |
| EVID-bbcb1896321a3665328e | indexed-log | bac97e227ed262b2100f372108842bb7cc8f363160c08d643441658b877b0446 | 693 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-2/command.json |
| EVID-bbd70dad7b529d75129d | indexed-log | e5168d9ab9d2d31103b6f6332d1e9aeabb0e2e803e1cae080cf4473ce1ed5216 | 119147 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-bc14c80a6bcf03286361 | indexed-log | 593927a38b7c4d4fc5269deaa58f8b9b2e34962f21a33371c2b051613c5fa87b | 241892 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-2/events.jsonl |
| EVID-bcbfe47919ac839234d5 | indexed-log | eb06b04e8e6fe6cc961c0e485492daf588b78198de81e8a289ece821a7f61d28 | 183 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-1/measurement.json |
| EVID-bcc2b80d1eb0075af0ac | indexed-log | 5bde81079040aec3538aa9c63a7ab075a4b5084237f9330ce59b1f2a54f44d54 | 1765 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P2-stdout.txt |
| EVID-bcebdbfff2d0c65a1535 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P4-stderr.txt |
| EVID-bd422b90610894f21b1f | indexed-log | c514688e1b1022895a9d7e4494e8d36931ca5c407a5936ad9a3cefcaa7ee44fe | 133353 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-3/stderr.txt |
| EVID-bd7764acf8eff5b8ef1a | indexed-log | fe4588db027987a89a650b53856ae5add819d04256208e390266ae3e8a0d90b5 | 698 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-2/command.json |
| EVID-bdde4d5a434c6defeed1 | indexed-log | 220228773ba37196b2edc089342ae054edde14bad0514bca9a0d526c5a3b1a89 | 214371 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-3/events.jsonl |
| EVID-be0d04049b2967f60143 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-be80080abfb69437d38b | indexed-log | c297eb1e0c901a524bbcb8fd188f2886b07077658328f1d45999ad18332c4139 | 184884 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-be8fe2f82a73df51bdb5 | indexed-log | 924eee6c9ee42afbba5429587f71b72cde9bb2248ba468b23a2d452d5b1cbf59 | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-2/command.json |
| EVID-bef31acc300ddbbaa828 | indexed-log | 2f88045e9aed1fdbf6e1a3d7fb024b3997710354780893ddae7c5e81bc3d597f | 116825 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-3/stderr.txt |
| EVID-bf084c4e04517439cf7b | indexed-log | 1395b5b11bef2d9e7fa4033d36e68cdfbbe2118c54e3b1416fb11c3b21a77a2d | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-2/stdout.txt |
| EVID-bf24ae35afd401c455f4 | indexed-log | 6023ce352c9ba7c77c12ef3838f3eb6da1a12a68fc6dc457752c64543ea33f6c | 1410 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/warmup/stdout.txt |
| EVID-bfb561e06a2f1cb3854c | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P3-stderr.txt |
| EVID-bfc41ead89d41aed4438 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P4-stderr.txt |
| EVID-c00d28553fafeb53ee3b | indexed-log | 827598d8b0618e5c89d326771a1b08b92ee2db9bb508149d0445f8f88ad27288 | 354 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-R002/command.txt |
| EVID-c059a4917e450e51caee | indexed-log | 04ca3f8700c762596db74ea3a4f51b41575efe9697566282193233e1aa041795 | 189 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/warmup/measurement.json |
| EVID-c0c4b5a527465a284899 | indexed-log | 0ce150cb43041353ecd77f51b97bb31529735230af81a18d56eb21dc6484b1d7 | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-1/stdout.txt |
| EVID-c11da5ee48a8975b267a | indexed-log | c833e68c7eba5f83979cde4249d3bfd2fd700f840b837a87ee045e00ab32f301 | 204 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R003/measurement.json |
| EVID-c197ee505d590f340d71 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P4-stderr.txt |
| EVID-c254e8693d31edba0dd2 | indexed-log | 68c5157e6359a4b7f6d799cc5cee0da9cd47912242ebfcdaea6a915d41a74f22 | 277 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-c2ecf22a11eed39be0f8 | indexed-log | 8fcd7163308bae6bebb2a449d1887ce15a4f70466859d3bd945625da09935748 | 185774 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-c318f96432e2228dd84f | indexed-log | d75fc0faa886bd1879870bf1fa422fdefc402721cf691788f52fe1cbc586dcca | 216419 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-c31f88b88a7fad98672d | indexed-log | 1d4cafcdfb3c5d0351f07454d428b996040d967c6da6170b30cb68ac8728195e | 30 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R002/stderr.txt |
| EVID-c3845e7624a55f94b53e | indexed-log | f789ee14bc6ff7c677fd5a36f1bfd513e85e48a608add39d9d4a4dae4d914170 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-c45db021d5134297009e | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-c4fc67ea44fd3c4a7420 | indexed-log | 0aee35b4681157fc8e1f993e5fb9fe1768b8c7cec782f2619947e9afaabdaf90 | 214001 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/warmup/events.jsonl |
| EVID-c56879699f00c33641c2 | indexed-log | 87595e67998873529592cb8bdca23e21353fe5d63fe8eccba8b272ef2f0153d5 | 119147 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-c56d8a503165a63bb8d2 | indexed-log | 9fd41f424fbce81cb1133443a5d317c47deb7aad97b1a6a7ac6e242083ead91a | 188 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/warmup/measurement.json |
| EVID-c57a1cda7b502f0944e0 | indexed-log | 8888c738b42d9d2f74302b454de78897d710659958a586ae00e49f8e18c7b503 | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-1/command.json |
| EVID-c60693ad92f5bc7f2ecf | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-c7a3dae92f58e8847eca | indexed-log | 448e6a507c2e2db075517414afcbc175b8202e6e9fe95ceea576a4b57d37e7bb | 185162 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-c7b663de06ba9df3870a | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P2-stderr.txt |
| EVID-c7b93a7337fba1ef57e3 | indexed-log | d7894c7c15e5f86d272ef78d44211c5863ec6d94c5d057caec2b6dbbfdd39727 | 1599 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R004/stdout.txt |
| EVID-c7e1ed8d63603f6c12f0 | indexed-log | 514a31b0a8b64e0f744349c59fd27ad79d76a502cdf29fbae615e6b8170572b0 | 186394 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/warmup/events.jsonl |
| EVID-c8160948baabf7e41a9e | indexed-log | d69aa3fae82d888cd905a196b2ad14a6939900b798d95c7f9deb29b91f415171 | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-3/measurement.json |
| EVID-c89fbaaa7509c1fc242c | indexed-log | 64b8944eb400d9e3e36fdf7d94b1cef1d757d055ff111e063bd5c71ad8175e9e | 217181 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-3/events.jsonl |
| EVID-c8fc232de0f9b7fad98f | indexed-log | 4069862274768f5b1422ead8c1176a957506d5aa6b27a22f8dacfc585e96383f | 209972 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R006/events.jsonl |
| EVID-c965da82bbd07a323bbd | indexed-log | c9939dc1b7132780f408f270387f42c6ddb1474ec7f3d201d5d52d58a4e96490 | 1563 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P1-stdout.txt |
| EVID-c999430da978ad621444 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-c9c123d7bd872f96094e | indexed-log | af52491e75687eebc65a156059cc1cacc6c9eb98f43fd8f3f8e1f853ac73a370 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-1/measurement.json |
| EVID-c9c922f6d846774271e4 | indexed-log | d41213ad5cf5ee8b6a704145021c2c7bcfb82d0b8222056407cb7f52a579df5f | 102315 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-c9d8d133f710ebd8cc30 | indexed-log | 81fef5122d4912fcb252e58f864ea51b7ecbc7c24f026ac8fd73208273357e20 | 1500 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P2-stdout.txt |
| EVID-c9e12e06e4f47cf0da84 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-c9f880dbc764cf6dccb7 | indexed-log | 40f29c0e35601e6f4654f78c8afa86ef582214d23484de84edbdd4ed6791e574 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-1/measurement.json |
| EVID-ca3579ea633d77ba2f8c | indexed-log | 8059f504bad32ad50a63382120cba3802afff97baebf2cbe9bac5c53f9e0902c | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-ca377aa6bb9482b9299c | indexed-log | 843d8f56512aaa1f714d1da7663ec3f07aa49e3cf3dc34c6bae19f5e818e262d | 1696 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P1-stdout.txt |
| EVID-cbb57ae268249199e001 | indexed-log | 8f563d15807e95731fd5794632a86cd9d96378a67c387503d894f089103f0b99 | 102563 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-cbb8b429673c3802d1d0 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P1-stderr.txt |
| EVID-cbe0be7bf5d471f672d1 | indexed-log | 9f0a1cfa9a548ee1bb6c2d401f57e43b7cd9cfc708c744a9f2a6864ad518bcbd | 133517 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-3/stderr.txt |
| EVID-cc79fce891daae7dff38 | indexed-log | d545baf69e6b5f2043986cb559cdf5e81f88959933af81e184bf3c8718f02610 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-1/measurement.json |
| EVID-cccd0e0ded6e7ee89b16 | indexed-log | e547a9c6da1eb7ade7895eb6dc0b09072a169e7592f29e2598bf2226eab38cf9 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-ccf71715f46da1be9796 | indexed-log | 37959edcc9011a00fc5149fe6dbbcfe4f10e0c7960ff2088188ab72e1056531d | 186459 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-cd434870fba1e787b83c | indexed-log | 03c5797e9628b3643061f6463e45b8fb3a5e255965fb3aa902d4ccf3b3ae13d1 | 272 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-cd445466f7ba4301f982 | indexed-log | 23beeec9017d179e4f0f3adb86af4ad0075711973f9fb170a6539b91813a6ce8 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-cdab9c181871e001a6d5 | indexed-log | 5bc2f939ef6af1c37528f2e6ad4dd1a1a82fd23a9814ed6636bad651bed38de2 | 704 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-2/command.json |
| EVID-cdc060316235fc2dd38c | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-R002/bench-stderr.txt |
| EVID-cf3718895d086cb7ddd6 | indexed-log | 1fa5f811cbcff3c7be9731c70aa726edbef00178d4a7c35a52b6308010a98a95 | 185066 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-cf7e7239a0002be59f88 | indexed-log | 36a6ed832d37b2b5a01c93776472547d7e57b189e4dd2206c048346417cb4fb8 | 1681 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R003/stdout.txt |
| EVID-cfa0498b011533b9de8b | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-d02818d9e2614279c97c | indexed-log | 1c6ba1bc30f9557baf3c677ea0e483823d0386833e39c8f96fbd6164fe72e378 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-2/measurement.json |
| EVID-d11430fa44871b5406f4 | indexed-log | 4e0b9224ff19476d83308e9a911c0edb40412bbae612e72744df4e7c773ee227 | 118339 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-1/stderr.txt |
| EVID-d246cf5cd2b93cc7e65f | indexed-log | 4e5d8ccbec6cb555c398627ab0cc09d52761f3918e510b8005d16dbc4f303628 | 120552 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-d2c0f13edf2d0134d63f | indexed-log | 820611f49d6f81388b29afc0ba1d1061e99fe1903d757a1e1829a2860b7b1a04 | 691 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/warmup/command.json |
| EVID-d2c9ac2ba6c2cceb363d | indexed-log | de43c2892ae8fdac877405746ebbd5d1276b4ca3213d21e932886563f1cd8338 | 101982 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-d32b72f5de5c6dd2313f | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-d335afb5774f9fb254b4 | indexed-log | afef97685d06e6fc7814b6420c624927c025d2145c8ba1e4e4bd0e4f795303b4 | 4710 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-R002/bench-stdout.jsonl |
| EVID-d411c26c6a55309b02ef | indexed-log | 4893dd2e183c6317fb1ea2a19ac0ddcd3baba395f66f2f856419444fa5c18c06 | 247578 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-1/events.jsonl |
| EVID-d444d3f151bca87626f1 | indexed-log | 22a3591b071ed236fbc885a280c2aafb3b50adcee4397fd94e1f546d552445ae | 211177 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-1/events.jsonl |
| EVID-d4d71f2fc8581004339a | indexed-log | 37d65677dac9fe1ed75426c4fb94fbe47617ec79c47b6d2021c9a7b27f25ea5e | 700 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/sample-2/command.json |
| EVID-d5516f52fed87ab53aad | indexed-log | d841c1be06c9015e84826c049bd2754b6fbb655a48a2557ff7848c6405a13391 | 275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-d5c7b10f4ad0b48f49c5 | indexed-log | 5153bede0e00c05217280ebbc9987b6a277abb279514aceb30d508c5d02d6ea5 | 692 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/warmup/command.json |
| EVID-d63c00be5a1cbcd18383 | indexed-log | 295200f2266a1c4d08ede63d8390ccc01ef754c34dd9b6a82743f127b5d7c684 | 217346 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-1/events.jsonl |
| EVID-d693f3653ff3e1b1c39c | indexed-log | b6b67847c5b8583571dbd30569dcb7c28195bad91c854fb5c093eab16109fa77 | 704 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-2/command.json |
| EVID-d69d8b91ab2b48351c38 | indexed-log | 6f118b07336ff7bf6d792213fdcab077d88f27e7a7b411c853665bd0e7a142c3 | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-1/measurement.json |
| EVID-d71eec285e52ec178f3d | indexed-log | 5b370e30f044a15cfaeb645216b7a0be5a1d85337d9b848e3dd600d5b0ea1b83 | 133517 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/warmup/stderr.txt |
| EVID-d723672e27de54068b2e | indexed-log | eea562fd83ea3f5e3bedc222c01c3778d173985ff2f8e9f1eed48b05841dd505 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-1/measurement.json |
| EVID-d7b3a169f078defd671f | indexed-log | fd86692edb9d6a36bb2bf508fdee967428e96a5a3bb37d510c0048a8a125a051 | 104355 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-d80041d77333584642c3 | indexed-log | 783b6c604d42800370d1a200ce7bbdd901a793db9004067c5e16537e44d93b6a | 241681 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/warmup/events.jsonl |
| EVID-d81c3d883263e557368e | indexed-log | 1f97df2a0bcdeefa2079cfefe002085b23908bd00ed8477148dff6e366307ea7 | 185486 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-d81f25162e388e05a593 | indexed-log | 2240a464013eef04ecb3f1f39d24d6de4e84b3264fe9ca053d56859e0919a101 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-2/stdout.txt |
| EVID-d822be198d374d5ff4f8 | indexed-log | fa91b08e10e9ad28753ec935bee9390b4c3b831987b9e1645e65314a12cb372e | 1693 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P1-stdout.txt |
| EVID-d859efcd4254117ee5d6 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P2-stderr.txt |
| EVID-d880347742015fc025fb | indexed-log | 147d8ae670edd0dda255941dcc65d3c22955f0c22359e8ccf24eac9bbae3a777 | 275 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-d8c13363b2602c8a66e0 | indexed-log | 318771a42c8f0be532d1b244c1484605f76dccd7019d23e986c56475e5f818d9 | 2191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-R001/bench-stdout.jsonl |
| EVID-d8de56364d19833e6521 | indexed-log | 65ed6a3ddf96980777157a2e8f74bca3ea25f537240491f95f572352b17b8fb6 | 1863 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P4-stdout.txt |
| EVID-d913e72ec5b15fef3903 | indexed-log | 194899da60085c3189ad6e20eb41b832ca486b87052fee2d046aa1347030a7dc | 1407 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-3/stdout.txt |
| EVID-d978297182650d9ecd4b | indexed-log | 483b3801658b146838c35e387cbd8afd35cc598efa9da9f2c8828cf9fae45288 | 1744 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P2-stdout.txt |
| EVID-d9e003633eca31bf7655 | indexed-log | 5899e0760df0c40ae08f876d514732605596e92ee6a057968dd567581f06a812 | 1831 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-QUALITY-R001/P1-stdout.txt |
| EVID-d9f85d2e842c90b96170 | indexed-log | 7033b4dc42b03a7e493dcb7b0851351da516fc39771034af010bf0d0e3157563 | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/warmup/measurement.json |
| EVID-da6134fa05242e38bc42 | indexed-log | 253b8db01c1d0a0b0d3e3ff0216751b1848423c234bf53187fc2a0a031cce5a3 | 2006 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-R002/p1-stdout.txt |
| EVID-daead2bcb4eec3d9ab36 | indexed-log | 63d8a93f7c8c9bfedea8a0c3abaca4ac2509a0b5c8eee1229f8f032ceeb1a20c | 183 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/warmup/measurement.json |
| EVID-db7138b9b4cedca727ef | indexed-log | 7192db11e8330c08cdf179ddaa78291259802a81ce5da3e6d94b433fe0a4a032 | 186265 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-dba4df92f263dfdded6b | indexed-log | d6b76ecec0269ee95703624a07df40ac95ba0e2ca07dea6eb5b9170bed4bb2aa | 100912 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-dc3cd172c4ca6e41b3d5 | indexed-log | 170e1da716bbe9ace89348e46257c7356e22e1c3bf18fd13592d79c1876338ad | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R004/measurement.json |
| EVID-dcb9eb8c3be89e6de8ab | indexed-log | b8be6d52fe8540db9948df9c8423b83bfec934b047ecaae349911053c3bd0c3e | 188 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-METRICS-R001/warmup/measurement.json |
| EVID-dcd56b129eb3daaf94eb | indexed-log | a5eb128693a487829a4747c19d8a48c34019060f35dad84ca8f468e81181f03e | 193 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-2/measurement.json |
| EVID-dd546185217d55fbf2f1 | indexed-log | 90e660e837c131c6d6b6e471961447fcb1768009366c095981195dc2d77fb3dc | 705 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/warmup/command.json |
| EVID-dd60cb6331c6914fcad0 | indexed-log | d294ca14eb00c3a054b592d70bddf7a37b73d9daf889ee52f2ed68b098623933 | 181667 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-ddc1c0204c4974d6c3ed | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-dde39454f9d6bb983684 | indexed-log | 0ef0ca6b5db5711e7b3946ca9cc6ed687cf159eb21966b35905b92d82de2191a | 277 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-ddfff2a308f7f3c5e95d | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P2-stderr.txt |
| EVID-de1b44b4533574e522b0 | indexed-log | b735fde36e68913d3afc139358dba3246e360a6fcb5adfcee1518977c53ba787 | 1738 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-QUALITY-R001/P2-stdout.txt |
| EVID-de617ed318863cf421d1 | indexed-log | afdd81983423a4f4db953bade472e2d75ad55b6989d56a418c2502630fc3c3c3 | 216807 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-2/events.jsonl |
| EVID-deb8588fe6f052ef34c2 | indexed-log | 54796b6855621c81838530b2513a19e86754dbcb6d724da47030f743146feecb | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-1/stdout.txt |
| EVID-e070a312bfca3dbb6e9e | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P4-stderr.txt |
| EVID-e0cf39ac252b43b8e7d3 | indexed-log | 92499581a968b4039ec1f78b3d7dd5b366207a2c982741cf589ed1385fff563f | 2582 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-QUALITY-R003/P2-stdout.txt |
| EVID-e158736e939103e76c4d | indexed-log | 0a387acb0c9d7f13840fa05ec53c1e60ea07786a1b94d94862a45555e0fc4ba5 | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-3/stdout.txt |
| EVID-e23ed1e1d3fdcec31dc8 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-3/stdout.txt |
| EVID-e2c25f4707e6c01601be | indexed-log | 0ac70b24254767f7157d349480bf9000ea87065fda1aafb27aca2a480029bbae | 740 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R004/command.json |
| EVID-e2ecb3029c2dce12f598 | indexed-log | 2e9a7aed75dda30d484a8279acc7ddd61ffbe9964be4989879b2a198dcbd1533 | 184337 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-3/events.jsonl |
| EVID-e497307b544879022ce8 | indexed-log | 0f68648872b0016fd5362d65579b8004198fda56eba922db748bfcdca01671a2 | 1409 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-1/stdout.txt |
| EVID-e5800f2e8a37d1c403fe | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-e5f1fecf682929398d4c | indexed-log | f880fc9da99f34a4af13734203682f3fe25e718d36d5ec055129d17ae95e8052 | 707 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-1/command.json |
| EVID-e65ae405494dfcce0b70 | indexed-log | 24ccac66e56f0f61c2c4080d749e79f856a0185885cbd9e51dd2c6b99652fed1 | 241299 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-1/events.jsonl |
| EVID-e6aee1caf0abb9964914 | indexed-log | b69cc9a12e881956193abe606987c6bc0c25e2c02f05f9f0b0e1c8d1f022e00d | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-e6bf5fd617d80e679492 | indexed-log | cfee131b748b6d7447af94269c1209e32b1a76626723de9126724d698e20e0f1 | 2218 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-R001/bench-stderr.jsonl |
| EVID-e71ce18d8d383fc61e45 | indexed-log | 68175e5b80a7d713fce5df461377c6aa5abe15a2ca04c204d3f48069dc0c2b57 | 726 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-CALIBRATION-R003/command.json |
| EVID-e72f9b21584ff0fe86d2 | indexed-log | 34c70dcdc920a7b05b2fa7568f4c6316e0397e76d50717d30a952d76808b6d8d | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-1/command.json |
| EVID-e795e7545d46acf2f8f1 | indexed-log | 83d6559e6ada223c92fcf7dbc1346b6a14572404954bfc5357bafe0c0cfb196a | 1860 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-QUALITY-R001/P4-stdout.txt |
| EVID-e81f69d03428ee60ca30 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-e863cc16719bc0c84cd2 | indexed-log | 6218e9dccd0088dbb5dfcf9a4b531e01f9282f0e19fed8ebc29fde2d94da83c7 | 1836 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P4-stdout.txt |
| EVID-e8ddc777493d89bf32ea | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-e9dd25f4ccce2c791546 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P1-stderr.txt |
| EVID-ea14066435f0daf3b465 | indexed-log | 3a1d051653e4462c0ec1f324468f1d90c09caed8f6a031fe05c1d3f096aa4ff1 | 103512 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-ea544c76176656ba0752 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P4-stderr.txt |
| EVID-ea74728006e5a179574c | indexed-log | bf9dc643890438bc4d021b84aea3e837868e3aadadc61d42b170b21ead535bb3 | 1414 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P3-stdout.txt |
| EVID-ea8fbd95cba69d6aa853 | indexed-log | 9847d4ff0893f6c5ca5ee6a871c008a46c3e7ac27238ec8e70c1e13ce9167d25 | 191 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-2/measurement.json |
| EVID-eb3cc23f03782311cc74 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-eb9159b664507a8e8428 | indexed-log | 059d981c3913477e509a69bc6fca72ea9828c689288d6806e2834834dfb9bca7 | 116220 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-METRICS-R001/sample-1/stderr.txt |
| EVID-ebf9008e1372d6c6c997 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-QUALITY-R002/P3-stderr.txt |
| EVID-ec7edbd49754e8f5dd00 | indexed-log | 77ee1afaf12b8f8cb3668be44bd717c6b66e40ae21d1fa635d4b6c64b9600037 | 116273 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/sample-3/stderr.txt |
| EVID-ecfe6528f9f00128d3e6 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-ed4150b94bf8712dec84 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-QUALITY-R001/P3-stderr.txt |
| EVID-edb9c684eb321dd5cd9e | indexed-log | e37c50abfa6d5d586b3cc8d0312a97dee7ce825103729dc0014db91bf65d8515 | 1434 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-METRICS-R001/warmup/stdout.txt |
| EVID-ee1ab47271f404dbec82 | indexed-log | 5d4f5b021734e5c27ff67343c4c243635f3c2c7706216e4d5d9cd8980f32a66c | 1423 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-3/stdout.txt |
| EVID-ee3cf416bbfc75ea97ea | indexed-log | a58fdb769931ab6dfa53b1e603bd0250c475c9f519a0e2fee0e86822e0c4d61a | 40 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-R001/p1-stderr.txt |
| EVID-eec8b5d331017e91f44f | indexed-log | fcc612cff222164908cb045890ae030729ce1b974daec83ba36bd55c5264a6be | 703 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-3/command.json |
| EVID-eedb9bbf6d14acbac1a4 | indexed-log | 6d54d3da29eee60561830505cd08b0c5a79dc6c077b25a02baea9cc7f0306542 | 216377 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-ef363f930ba0f02136e0 | indexed-log | 566e831eba39b494e3285208c09504db601966b35be9432e2031419e01cca267 | 100912 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-2/stderr.txt |
| EVID-ef46924c6fbaf37604b7 | indexed-log | 11c03878a882b9d96dd3548d0b97963b59dabaa0a5994e5d94ce154a9711ae09 | 1862 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-QUALITY-R001/P4-stdout.txt |
| EVID-ef75ffed271f4241fc73 | indexed-log | 64927cb94df81141c10968de1b14ab11b5af369e8a3c8145ca06313b0ffa4f21 | 1429 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-QUALITY-R002/P3-stdout.txt |
| EVID-efdd5062c49c1404ee6f | indexed-log | 001ac230b301be7e13e906419276359ec311f337d3c00f6e975eb8e4a71c322e | 700 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-METRICS-R001/sample-2/command.json |
| EVID-f0221d86a6e43b2fbf9a | indexed-log | 0c80a3e737f00854be63097ec52d61a7bdc507615ec516ea30b90a571b74d85c | 694 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-METRICS-R001/sample-2/command.json |
| EVID-f053a8a7fc865a6f540f | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-f08c8e1151df55cced75 | indexed-log | f2e0d4a9c04ad5bbe78f4790435b710e86df168dc43726f33bc8d3a56e03bbda | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-1/measurement.json |
| EVID-f093cb37e7e80ffb6e7e | indexed-log | 9b441dc61c08c294625e3c0741461bd574499734a31c3999789539d18b0f83c5 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-SERVER-METRICS-R001/sample-3/measurement.json |
| EVID-f094030d74fa1c960d7c | indexed-log | d015887880641bbb5bb27fc6eabb9b920894a0974bbb20b0d1bb9bed46991227 | 100912 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-3/stderr.txt |
| EVID-f0d6b18e2fd69463fe46 | indexed-log | c3121925f75c7e65a177c92a236b332307ad7908e101efd35da45a4f4e0bb85c | 1426 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-METRICS-R001/sample-2/stdout.txt |
| EVID-f0dec4c577168955e55f | indexed-log | 31e03bca3ee208488ccdcbc246c0a4e10f7b3e2959877034ee8ff67d4b229c88 | 1470 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P3-stdout.txt |
| EVID-f0fe17e57dba030f0334 | indexed-log | 5780b7210ee595167fb71dcb53def0880721bb88fd3f355cf748f4320c905cb8 | 102563 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/sample-1/stderr.txt |
| EVID-f16b479b5b7b7ba401d0 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P6-R001/P6-turn2-with-history-stderr.txt |
| EVID-f1fb0dfea18d076824a8 | indexed-log | db91e3fd263dd4a6b67d260c7339dc8e8e8fa6f0c601ff8dcdda3e3c86f31a08 | 242820 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-METRICS-R001/sample-3/events.jsonl |
| EVID-f26edcb1ffa2a6c2e28f | indexed-log | eb99126f0cbee0ad35bd07af0840123a24a4f4fab202be2817251226d6503ea0 | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-f2a6b8a2ad35c856cdf0 | indexed-log | 163375e1016fc451fe94c401364b7aeb5267fa56f14b12479f1bb99f184f5247 | 1422 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-METRICS-R001/sample-1/stdout.txt |
| EVID-f3064d5fffeffdf1b15e | indexed-log | a22e83542ae138181b5675ee49bb06b38a0088fbb35ffc420e803da7f28caa14 | 115126 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-METRICS-R001/sample-1/stderr.txt |
| EVID-f390da572dba03830afb | indexed-log | 86d596bed1ba2da7d29c46941db321a805015ec76940910c8e7a1ddf3d2e285a | 187 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-2/measurement.json |
| EVID-f4c501becf4a1d3784fd | indexed-log | 7c92847e568a9e1075edbf9ee0fbc10b145e81a00c5f308699056d479e711b3f | 216127 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-f5e1da16d7db73f65539 | indexed-log | 54e6701ed255d2480362865b07ca27b15598e0bd66a638be515740c2e09a34ef | 116801 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-3/stderr.txt |
| EVID-f5f84cc5de4b2d94c571 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-R001/p1-stdout.txt |
| EVID-f60101bc8ad9b984f0dc | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-QUALITY-R001/P1-stderr.txt |
| EVID-f61a6913d1e997b2891c | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-SERVER-METRICS-R001/sample-2/stdout.txt |
| EVID-f646547bda8ffad6bbdd | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-QUALITY-R002/P1-stderr.txt |
| EVID-f712e61a4fa6a05abb49 | indexed-log | 5e55bc1b8b8e909e35e27bb8d4a33da3379b69f29680ccceb21b854e982dbbf4 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-METRICS-R001/sample-1/measurement.json |
| EVID-f723095ab67ea161dd4c | indexed-log | 9d99e1c8ebbb95662838b65750601573eeb92968b6bf8da9444530a2efe25a86 | 116801 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-METRICS-R001/sample-2/stderr.txt |
| EVID-f7a3068d399539e6ff5b | indexed-log | 9b9956f76472d85affd707816bdf1e90afb7ecc70c368921533393dfc0a559a5 | 1409 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-METRICS-R001/sample-2/stdout.txt |
| EVID-f7ad83e1b0b937e4628b | indexed-log | c6d6eef230da5cce8d4b41a0ce5978f71836731c6e0d3ee053436742a17b376f | 278 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-f7c29d869779c01f75b5 | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/sample-1/stdout.txt |
| EVID-f7fc0f7f6311791bde32 | indexed-log | e5ebb4522e06c8c244724b3ce5199a29a12d84b5b6cfbcd8214f697b236edb19 | 209 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-QUALITY-R001/P3-stderr.txt |
| EVID-f87114e2009e425bbcbf | indexed-log | e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 | 0 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-SERVER-METRICS-R001/warmup/stdout.txt |
| EVID-f92877cde2cef1f629e2 | indexed-log | 86b92d5aed459810e2c8e28391c58e100d6c367f334a8a3c4d7eac8aba54cdcd | 102563 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-f9a908b45e483681bfbe | indexed-log | 73c262cc97f55f66e64499bc5c0e0b1fad1f207a08fd4893b5c024bb6f485922 | 186483 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-SERVER-METRICS-R001/sample-1/events.jsonl |
| EVID-fa49bead0b5993bf34b7 | indexed-log | f566d2c0286dd37403c1f36eac7b6983e1348632ad3a6ee2a688e3a91285684c | 276 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-SERVER-METRICS-R001/sample-2/measurement.json |
| EVID-fadca9b36d06b3f687e4 | indexed-log | fa423899f62092e422f3532e56c86ffbfcbab41d57427b0aff0321043005ad31 | 192 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-1/measurement.json |
| EVID-fd0224096c5549d58f14 | indexed-log | 56b6e343d11adf54068e2e663e49a3103c33a9982b41b156f7a7d1e42f10ec3c | 277 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-SERVER-METRICS-R001/sample-1/measurement.json |
| EVID-fd393b5512d71f0046be | indexed-log | 53fbb9d54b93f9bef8f4689d362f5d94c6f7789d1df72866b4a736b9e5230e39 | 120548 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-SERVER-METRICS-R001/warmup/stderr.txt |
| EVID-fdc07c3c9c649188bf11 | indexed-log | 566c5490174efa67774ac5680772c771bba0b8dedd7254ff372e68bd1c60d135 | 190 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-METRICS-R001/sample-3/measurement.json |
| EVID-fed20448840c47d1ab36 | indexed-log | e57390325384b47f38de750ac3607c9fc3e2fb1b0a20126eff572618e7e5a01d | 103512 | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-SERVER-METRICS-R001/sample-1/stderr.txt |
| upstream-llama-cpp-0d0e0a927263 | test-run-register | 0d0e0a9272639e1cebccf5ef59d7f7ddad53378524abd6a8c15762255992a539 | 44219 | docs/testing/Test-Run-Register.csv |
| upstream-llama-cpp-175d58cb2e63 | failure-register | 175d58cb2e63e26209f1379ee127b4163070ac6661e989e2a29b62a5bec19784 | 15252 | docs/testing/Failure-Register.csv |
| upstream-llama-cpp-1bf6d02f2d93 | evidence-index | 1bf6d02f2d931a4bcfce91b4a93d696d1a66b86e0b5ce0e7f81e8d950e8e8bb2 | 935095 | docs/testing/Evidence-Index.csv |
| upstream-llama-cpp-1d7c7f12ccbf | historical-deviation-evidence | 1d7c7f12ccbf717f81e5d38a7b4b77000910a34e3cede6b0341fff9b8efc00b9 | 538 | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B05/UL-B05-R002/R001-cache-validation-failure-analysis.md |
| upstream-llama-cpp-27c47275b8fc | controlled-workbook-markdown | 27c47275b8fc2682cab7787485bece5c4ceeb02a429a2128a8af2b9fc0785936 | 20369 | docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md |
| upstream-llama-cpp-2c104a7f446f | performance-register | 2c104a7f446f7360d7a366b1eeaba887f4a719a087e07f82b51602aa6202b58d | 49207 | docs/testing/Performance-Measurement-Register.csv |
| upstream-llama-cpp-380883e79205 | revision-register | 380883e7920541ed2b59889a40fad29626b5b0e3b9d06fdfbfe280ee1e945f64 | 19453 | docs/testing/Workbook-Revision-Register.csv |
| upstream-llama-cpp-3a280ff9538d | quality-scoring | 3a280ff9538d934ad9eea92255a9f25e104a41723ee51e8d3e02433681e78a1d | 2250 | experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/quality-scoring-2026-07-15.md |
| upstream-llama-cpp-40a64f36600f | raw-results-placeholder | 40a64f36600f574e5fde6a6c01debb4a545c725d19efdbdf03b5e81e9e1f9ca8 | 1099 | experiments/raw-results/upstream-llama-cpp/README.md |
| upstream-llama-cpp-9ba512818e81 | quality-prompts | 9ba512818e81e0ba8da3ddc89cf040dd3b779d1edc41db23d3a896d778de807f | 4621 | experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json |
| upstream-llama-cpp-a36016f66e02 | quality-rubric | a36016f66e02c9e28f0938cf81335dc4b522e9f92b7d9bad3031f90b7ef91d90 | 2214 | experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json |
| upstream-llama-cpp-b9f424464ca0 | historical-deviation-evidence | b9f424464ca06b75caba88242738b17dca057fb0447e288fe9cec37565252751 | 1009 | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B06/UL-B06-R002/repository-test-failure-analysis.md |
| upstream-llama-cpp-bbb63ecd783d | historical-deviation-evidence | bbb63ecd783d85c496a304d309d82edf196eee0f9ea2c5aba170025df47f16e6 | 648 | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B05/UL-B05-R002/repository-test-failure-analysis.md |
| upstream-llama-cpp-db711113b8e9 | resource-summary | db711113b8e9b26c5cfd145c7a98cd7b0acb458b8635d4cf76cdb49ba6efbb80 | 1518 | experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/resource-metrics-2026-07-16.json |

## 15. Revision history

This generated report is revision R1; it does not alter WB-01 revision history.

### RV-01 — Revision history

| Revision | Date | Change |
| --- | --- | --- |
| R1 | 2026-07-16 | Initial unified evidence-bound publication from WB-01 v1.4 |

Markdown is canonical. DOCX and PDF are generated derivatives validated separately.
