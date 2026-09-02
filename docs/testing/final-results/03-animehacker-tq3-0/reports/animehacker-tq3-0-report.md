# animehacker TQ3_0 Final Test Report

| Document control | Value |
| --- | --- |
| Route ID | animehacker-tq3-0 |
| Revision | 1.1 |
| Generated date | 2026-08-31 |
| Evidence IDs | animehacker-3409c4fff1f57d4b988a, animehacker-ca0c684bb478b8527653, animehacker-34ab10f0a8210699f64b, animehacker-d0a6c059ef53abfb592a, animehacker-79e759f5588cae2c50d5, animehacker-e73b2f1205e6c7f8d4d3, animehacker-9088849e3d9565254d7d, animehacker-44a8d70b058f1c5cac52, animehacker-6517f74c71a0537eb162, animehacker-c1863347fee0fae7618f, animehacker-17b6142958ab2916e9ed, animehacker-d25f303e05b5147dc9ca, animehacker-4ee219bf5ee960cb094d |

## 1. Title and document control

This publication is controlled by WB-03 revision 1.5 and the authenticated source set listed in the Evidence section.

### DC-01 — Document control

| Field | Value |
| --- | --- |
| Route | animehacker-tq3-0 |
| Campaign | wb-03-v1.5-2026-07-18 |
| Controlled source | WB-03 v1.5 |
| Canonical format | Markdown |

## 2. Technical summary

The final authority records seven completed runnable configurations and three resolved safety classifications. Zero unresolved failures does not mean zero historical failure attempts: eleven resolved source-ledger events and rejected runtime evidence remain auditable.

## 3. Key findings and decision-relevant evidence

The key findings below separate completed runtime evidence, safety classifications, and rejected historical evidence.

### KF-01 — Decision-relevant findings

| Finding | Evidence-bound conclusion |
| --- | --- |
| Runtime | Seven formal rows have exactly three explicitly included samples each. |
| Safety | AH-06, AH-07, and AH-10 remain safety-classified; no request metrics are fabricated. |
| TQ3_0 | CPU activation is validated; AH-09 is CPU-resident TQ3 KV with 1/41 SYCL layers offloaded. |
| Rejected evidence | Superseded AH-09 summaries are excluded from statistics but retained in the attempt/failure ledger. |

## 4. Repository, branch, commit, build, hardware, and software identity

The campaign identity is fixed to the recorded repository, machine, and operating-system environment.

### SYS-01 — Controlled identity

| Field | Value |
| --- | --- |
| Repository | https://github.com/animehacker/llama-turboquant |
| Commit | 5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc |
| CPU | 12th Gen Intel Core i5-12450H |
| RAM | 16.0 GB installed; 15.7 GB usable |
| GPU | Intel UHD Graphics; shared system memory |
| OS | Windows 11 Home 10.0.26200 build 26200 |

## 5. Objectives, scope, test matrix, and execution sequence

WB-03 defines AH-01 through AH-10. Final statuses come from reconciliation/state records; performance comes only from the seven selected runtime summaries.

## 6. Model, weight, cache-format, and backend availability

Availability preserves the full ten-row WB-03 matrix and its terminal status classification.

### AV-01 — Model, cache, and backend availability

| Test | Model | Cache | Backend | Status |
| --- | --- | --- | --- | --- |
| AH-01 | gemma-1b-q4-k-m | f16 | cpu | Passed |
| AH-02 | gemma-1b-q4-k-m | tq3_0 | cpu | Passed |
| AH-03 | granite-3b-bf16 | f16 | cpu | Passed |
| AH-04 | granite-3b-bf16 | q8_0 | cpu | Passed |
| AH-05 | granite-3b-bf16 | tq3_0 | cpu | Passed |
| AH-06 | granite-8b-q8-0 | f16 | cpu | Blocked |
| AH-07 | granite-8b-q8-0 | tq3_0 | cpu | Blocked |
| AH-08 | granite-3b-bf16 | f16 | sycl-partial | Passed |
| AH-09 | granite-3b-bf16 | tq3_0 | sycl-partial | Passed |
| AH-10 | granite-8b-q8-0 | tq3_0 | sycl-partial | Blocked |

## 7. Complete attempt accounting

Each matrix row has one terminal accounting record; the separate rejected AH-09 attempt remains in the canonical attempt ledger.

### AT-01 — Complete terminal attempt accounting

| Test | Status | Executed | Backend | Reason |
| --- | --- | --- | --- | --- |
| AH-01 | Passed | Yes | cpu | Completed with explicitly included evidence |
| AH-02 | Passed | Yes | cpu | Completed with explicitly included evidence |
| AH-03 | Passed | Yes | cpu | Completed with explicitly included evidence |
| AH-04 | Passed | Yes | cpu | Completed with explicitly included evidence |
| AH-05 | Passed | Yes | cpu | Completed with explicitly included evidence |
| AH-06 | Blocked | No | cpu | Safety prerequisite blocked inference before a request; classification resolved with cleanup evidence |
| AH-07 | Blocked | No | cpu | Safety prerequisite blocked inference before a request; classification resolved with cleanup evidence |
| AH-08 | Passed | Yes | sycl-partial | Completed with explicitly included evidence |
| AH-09 | Passed | Yes | sycl-partial | Completed with explicitly included evidence |
| AH-10 | Blocked | No | sycl-partial | Safety prerequisite blocked inference before a request; classification resolved with cleanup evidence |

## 8. Performance results and repetition detail

Reader-facing metrics use stable decimal precision; only explicitly admitted samples contribute to the seven runnable rows.

### PF-01 — Performance and resource observations

| Test | TTFT ms | Decode tok/s | Peak WS MiB | KV MiB | CPU mean % | GPU mean % | Admission |
| --- | --- | --- | --- | --- | --- | --- | --- |
| AH-01 | 140.39 | 46.323 | 929.49 | 26.00 | 56.90 | 0.00 | included |
| AH-02 | 137.75 | 37.749 | 911.06 | 5.69 | 58.44 | 0.00 | included |
| AH-03 | 274.92 | 15.872 | 3663.23 | 160.00 | 62.04 | 0.00 | included |
| AH-04 | 243.23 | 15.819 | 3672.14 | 170.00 | 61.97 | 0.00 | included |
| AH-05 | 252.57 | 11.536 | 3575.03 | 70.00 | 62.70 | 0.00 | included |
| AH-06 | Not collected | Not collected | 8913.65 | 320.00 | Not collected | Not collected | safety evidence only; excluded from formal statistics |
| AH-07 | Not collected | Not collected | 8737.70 | 140.00 | Not collected | Not collected | safety evidence only; excluded from formal statistics |
| AH-08 | 309.95 | 15.184 | 3130.69 | 320.00 | 62.03 | 16.31 | included |
| AH-09 | 491.00 | 12.109 | 2891.66 | 70.00 | 63.14 | 12.61 | included |
| AH-10 | Not collected | Not collected | 8969.62 | 70.00 | Not collected | Not collected | safety evidence only; excluded from formal statistics |

*Missing request-window/OS observations remain literal Not collected; safety evidence is not a formal statistic.*

## 9. Quality methodology and results

The seven runnable rows retain 42 P1-P6 scores under the historical harsh content screen. Calibration is Not collected. This method is not directly comparable with OpenVINO.

### QL-01 — Historical prompt-score means

| Test | Mean /10 | Boundary |
| --- | --- | --- |
| AH-01 | 6.2250 | Historical route-specific screen |
| AH-02 | 3.6583 | Historical route-specific screen |
| AH-03 | 6.3917 | Historical route-specific screen |
| AH-04 | 6.7250 | Historical route-specific screen |
| AH-05 | 3.2750 | Historical route-specific screen |
| AH-08 | 6.2750 | Historical route-specific screen |
| AH-09 | 6.0583 | Historical route-specific screen |

## 10. Device/backend use and fallback verification

AH-08 used SYCL OpenCL partial placement. Recovered AH-09 used Level Zero level_zero:0 with CPU-resident TQ3 KV and 1/41 layers offloaded. Vulkan built only as supplementary evidence and is not a source-proven controlled TQ3 route.

## 11. Failures, blocks, deviations, and recovery attempts

Historical events remain distinct from the final unresolved-failure count and retain their original codes and resolutions.

### FL-01 — Historical failures, recoveries, and terminal classifications

| ID | Scope | Code | Resolution |
| --- | --- | --- | --- |
| AH-F01 | AH-B02 | DEP | Yes |
| AH-F02 | AH-B02 | BF | Yes |
| AH-F03 | AH-B05 | REPRO | Yes |
| AH-F04 | AH-B05 | REPRO | Yes |
| AH-F05 | AH-B06 | REPRO | Yes |
| AH-F06 | AH-B05 | REPRO | Yes |
| AH-F07 | AH-B05 | REPRO | Yes |
| AH-F08 | AH-06/AH-07 | SAFETY | Yes - safety-classified |
| AH-F09 | AH-09 | RECOVERY | Yes - recovered with bounded partial placement |
| AH-F11 | AH-10 | SAFETY | Yes - safety-classified |
| AH-F10 | AH-04/P5 | QUAL | Yes |

*All eleven historical events remain visible. The formal runtime status has zero unresolved failures, not zero historical attempts.*

## 12. Limitations, uncertainty, robustness checks, and claim boundaries

This historical route did not collect every OS metric for safety-stopped rows; those fields are Not collected, never zero. The evidence supports a conditional research comparator, not production Intel GPU integration or a full-GPU TQ3 claim.

> Note: Rejected/superseded summaries are provenance evidence only and never contribute to formal aggregates.

## 13. Reproduction guidance

Use the ordered commands in reproduction/commands.md to rebuild the route from immutable evidence, export the owned Word PDF, finalize it, and validate checksums.

## 14. Evidence index and hashes

This reader-facing table lists the bounded key authority set. The complete 742-row audit inventory remains in evidence/evidence-index.csv.

### EV-01 — Key authenticated evidence

| Evidence ID | Role | Repository-relative path | SHA-256 |
| --- | --- | --- | --- |
| animehacker-3409c4fff1f57d4b988a | controlled-workbook-markdown | docs/testing/workbooks/text-templates/03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.md | f4b86b1aaca43745c2a701e38c6571968596ef586e8fd775b69aa4b7983770b0 |
| animehacker-ca0c684bb478b8527653 | intended-matrix | experiments/manifests/animehacker-tq3-0/retest-matrix.json | c254679cabac29b2cb2f8c6528cf1fb371161ebfb0a11f7607bd464c3f56484c |
| animehacker-34ab10f0a8210699f64b | reconciliation | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/reconciliation.json | 02f6dea288f1b704747ec07b0adacd2644990e645eb7fa45ff32fa71098ebc15 |
| animehacker-d0a6c059ef53abfb592a | raw-evidence | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/runtime/state.json | 25bd4e668fe6f443ec338f82d8e6db8cbb5e75dc44a10c200a52ce32003b4cbf |
| animehacker-79e759f5588cae2c50d5 | raw-evidence | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/runtime-recovery/state.json | 6bfd0e30352e587ef1036bdee4ea5fd474cd530c50c882412e3e9cef877d9cea |
| animehacker-e73b2f1205e6c7f8d4d3 | reconciliation | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/build-cpu/reconciliation-final.json | 4347b7dd353001b35bb13d5d1c304e970cae533c10b4102dd918e46731d6c28b |
| animehacker-9088849e3d9565254d7d | reconciliation | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/build-sycl/reconciliation.json | b60e97f3855bda1ec0e4d9858e40d7f3c21f669735940aa6788c8c1446b01241 |
| animehacker-44a8d70b058f1c5cac52 | reconciliation | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/build-vulkan/reconciliation.json | 57871c024dc5c425692ec436974ac397088a6c43192448719e13b61cbadfddda |
| animehacker-6517f74c71a0537eb162 | quality-evidence | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/quality/adjudication.json | 5123c0fd2016159fb2b48ea2bedfa039af30798112d18b6f52fe914322fe521b |
| animehacker-c1863347fee0fae7618f | raw-evidence | experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/quality-recovery/quality-adjudications.json | 4fb179143e2867023d264c5b5d73cbfe3fa5901c81338a78f5e8d41b3cdc37d9 |
| animehacker-17b6142958ab2916e9ed | quality-register | docs/testing/Quality-Evaluation-Register.csv | 4d8e327caf025090b61c917152ba3f656209376e2365b11c40eeaf0c3236797c |
| animehacker-d25f303e05b5147dc9ca | prompt-contract | experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json | 9ba512818e81e0ba8da3ddc89cf040dd3b779d1edc41db23d3a896d778de807f |
| animehacker-4ee219bf5ee960cb094d | quality-rubric | experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json | a36016f66e02c9e28f0938cf81335dc4b522e9f92b7d9bad3031f90b7ef91d90 |

## 15. Revision history

Revision 1.1 records the source-authentication and reader-presentation hardening applied after independent review.

### RH-01 — Revision history

| Revision | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-08-31 | Initial unified final-results publication from WB-03 v1.5 evidence |
| 1.1 | 2026-08-31 | Authenticated complete source entities and publication-ready evidence presentation |
