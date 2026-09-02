# Requirements Traceability Matrix — Working Baseline

> TurboVec checkpoint: **BLOCKED**, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, run `EXP-TV-COMP-001-20260902T225731Z-001`. R-M02 remains open; no product implementation is claimed.

**Document ID:** RTM-001  
**Version:** 1.3  
**Status:** Working baseline  
**Owner:** Arian B  
**Baseline date:** 14 July 2026  
**Source workbook SHA-256:** `88385b743cbc52921d71d5a0805a1d8fefd6b6c513c6507ad263a24b2e65ea50`  

Detailed requirement statements and acceptance criteria are in [MoSCoW v1.2](MoSCoW-Requirements-v1.2.md). Reverse indexes are in [Traceability Reverse Indexes v1.3](Traceability-Reverse-Indexes-v1.3.md).

## Active requirement matrix

| ID | Priority | Objectives | RQs | Work packages | Test/evidence | Evidence path |
|---|---|---|---|---|---|---|
| F-M01 | Must | O1; O11 | RQ4 | IM-01; FR-04 | AC-F-M01 | docs/evidence/requirements/F-M01/ |
| F-M02 | Must | O2 | RQ4 | IM-02 | AC-F-M02 | docs/evidence/requirements/F-M02/ |
| F-M03 | Must | O2; O8 | RQ4 | IM-03; IM-04 | AC-F-M03 | docs/evidence/requirements/F-M03/ |
| F-M04 | Must | O2; O8 | RQ4 | IM-06; IM-07 | AC-F-M04 | docs/evidence/requirements/F-M04/ |
| F-M05 | Must | O2; O3; O4 | RQ3; RQ4 | IM-05 | AC-F-M05 | docs/evidence/requirements/F-M05/ |
| F-M06 | Must | O1; O2; O8 | RQ4 | IM-07; RT-05; FR-02 | AC-F-M06 | docs/evidence/requirements/F-M06/ |
| F-M07 | Must | O3; O4 | RQ1; RQ3; RQ4 | HE-01; HE-02 | AC-F-M07 | docs/evidence/requirements/F-M07/ |
| F-M08 | Must | O3; O10 | RQ3; RQ4 | HE-03; HE-04 | AC-F-M08 | docs/evidence/requirements/F-M08/ |
| F-M09 | Must | O3; O4; O8 | RQ3; RQ4 | HE-07 | AC-F-M09 | docs/evidence/requirements/F-M09/ |
| F-M10 | Must | O4; O8 | RQ1; RQ3; RQ4 | HE-02; HE-05 | AC-F-M10 | docs/evidence/requirements/F-M10/ |
| F-M11 | Must | O4; O8 | RQ3; RQ4 | HE-06 | AC-F-M11 | docs/evidence/requirements/F-M11/ |
| F-M12 | Must | O4; O8 | RQ4 | HE-08 | AC-F-M12 | docs/evidence/requirements/F-M12/ |
| F-M13 | Must | O5; O6; O8 | RQ1; RQ4 | RT-01; RT-02; RT-03; RT-04 | AC-F-M13 | docs/evidence/requirements/F-M13/ |
| F-M14 | Must | O2; O7 | RQ4 | IM-04; QX-01; QX-02 | AC-F-M14 | docs/evidence/requirements/F-M14/ |
| F-M15 | Must | O1; O8 | RQ4 | IM-07; RT-05; FR-02 | AC-F-M15 | docs/evidence/requirements/F-M15/ |
| F-M16 | Should | O2 | RQ4 | DL-01; DL-02; DL-03 | AC-F-M16 | docs/evidence/requirements/F-M16/ |
| F-M17 | Should | O2; O11 | RQ4 | DL-02; DL-03 | AC-F-M17 | docs/evidence/requirements/F-M17/ |
| F-M18 | Must | O5; O6; O8 | RQ1; RQ4 | RT-01; RT-02; RT-05 | AC-F-M18 | docs/evidence/requirements/F-M18/ |
| F-M19 | Must | O5; O6; O8 | RQ4 | IM-03; RT-03; QX-03 | AC-F-M19 | docs/evidence/requirements/F-M19/ |
| F-M20 | Should | O6 | RQ4 | RT-04 | AC-F-M20 | docs/evidence/requirements/F-M20/ |
| F-M21 | Must | O5; O6; O8 | RQ1; RQ2; RQ4 | QX-03; QX-04 | AC-F-M21 | docs/evidence/requirements/F-M21/ |
| F-M22 | Must | O5; O6; O8 | RQ1; RQ4 | QX-04; RT-05 | AC-F-M22 | docs/evidence/requirements/F-M22/ |
| F-M23 | Should | O7 | RQ3; RQ4 | QX-01; QX-02 | AC-F-M23 | docs/evidence/requirements/F-M23/ |
| F-M24 | Should | O7; O11 | RQ3; RQ4 | QX-02 | AC-F-M24 | docs/evidence/requirements/F-M24/ |
| F-M28 | Must | O6; O8 | RQ4 | RT-03 | AC-F-M28 | docs/evidence/requirements/F-M28/ |
| R-M01 | Must | O8; O10 | RQ2 | QX-04; FR-03 | EXP-TQ-COMP-001 | experiments/processed-results/EXP-TQ-COMP-001/ |
| R-M02 | Must | O9 | RQ-TV | PD-10; TV-01 | DEC-TV-001 | docs/architecture/decisions/ADR-TurboVec.md |
| R-M03 | Must | O8 | RQ1; RQ2 | OV-01; OV-03 | EXP-OV-OFFICIAL-001 | experiments/raw-results/EXP-OV-OFFICIAL-001/ |
| R-M04 | Must | O6; O10 | RQ2; RQ3 | FR-03 | MET-MEM | experiments/processed-results/final-metrics/ |
| R-M05 | Must | O6; O10 | RQ1; RQ2; RQ3 | FR-03 | MET-PERF | experiments/processed-results/final-metrics/ |
| R-M06 | Must | O6; O10 | RQ2; RQ3 | PD-09; FR-03 | QUAL-FINAL | experiments/raw-results/quality/ |
| R-M07 | Must | O6; O10 | RQ2; RQ3 | FR-03 | CTX-FINAL | experiments/processed-results/context/ |
| R-M08 | Must | O11 | All RQs | PD-03; PD-08; FR-05 | EVID-AUDIT | experiments/manifests/ |
| R-M09 | Must | O11 | All RQs | PD-03; FR-03; FR-05 | FAIL-AUDIT | docs/testing/Failure-Register.md |
| R-M10 | Must | O11 | All RQs | FR-04; FR-05 | REPRO-001 | release-evidence/reproduction/ |
| R-M11 | Must | O8; O10 | RQ1 | PD-09; FR-03 | COMPAT-FINAL | experiments/processed-results/cross-route/ |
| R-M12 | Must | O10 | RQ3 | HE-04; FR-03 | MEM-BUDGET-001 | experiments/processed-results/memory-budgets/ |
| R-M14 | Must | O3; O10 | RQ3; RQ4 | HE-04; FR-03 | EST-VALID-001 | experiments/processed-results/EST-VALID-001/ |
| N-M01 | Must | O1; O5; O6 | RQ4 | RT-05; FR-02 | AC-N-M01 | docs/evidence/requirements/N-M01/ |
| N-M02 | Must | O1; O8; O9 | RQ4; RQ-TV | FR-02 | AC-N-M02 | docs/evidence/requirements/N-M02/ |
| N-M03 | Must | O1; O8 | RQ4 | IM-03; RT-03; QX-01; DL-02; TV-03 | AC-N-M03 | docs/evidence/requirements/N-M03/ |
| N-M04 | Must | O1; O8 | RQ4 | IM-03; RT-03; QX-01; DL-02; TV-03 | AC-N-M04 | docs/evidence/requirements/N-M04/ |
| N-M05 | Must | O5; O11 | RQ4 | RT-01; RT-02; QX-01; DL-02; TV-02 | AC-N-M05 | docs/evidence/requirements/N-M05/ |
| N-M06 | Must | O5; O7; O11 | RQ4 | RT-03; RT-05; QX-01; DL-02; TV-03 | AC-N-M06 | docs/evidence/requirements/N-M06/ |
| N-M07 | Must | O8; O10; O11 | RQ1; RQ2 | PD-09; FR-03 | AC-N-M07 | experiments/manifests/ |
| N-M08 | Must | O11 | RQ4 | PD-08; FR-04 | AC-N-M08 | release-evidence/clean-build/ |
| N-M09 | Must | O11 | RQ4 | PD-08; FR-05 | AC-N-M09 | release-evidence/repository-scan/ |
| N-M10 | Must | O8 | RQ4 | HE-02; QX-03; OV-02; TV-04 | AC-N-M10 | docs/evidence/requirements/N-M10/ |
| N-M11 | Must | O8; O11 | RQ1; RQ2 | QX-03; QX-04; OV-03; TV-03 | AC-N-M11 | docs/evidence/requirements/N-M11/ |
| N-M12 | Must | O5; O11 | RQ4 | PD-06; RT-01; RT-02 | AC-N-M12 | docs/evidence/requirements/N-M12/ |
| N-M13 | Must | O1; O8; O11 | RQ4 | IM-01; FR-02 | AC-N-M13 | docs/ux/accessibility/ |
| N-M14 | Must | O11 | RQ4 | FR-04; FR-05 | AC-N-M14 | release-evidence/ |
| G-M01 | Must | O11 | All RQs | PD-01 | AC-G-M01 | docs/planning/Project-Definition-v1.md |
| G-M02 | Must | O11 | All RQs | PD-04 | AC-G-M02 | docs/evidence/requirements/G-M02/ |
| G-M03 | Must | O11 | All RQs | PD-04; FR-01; FR-05 | AC-G-M03 | docs/requirements/Requirements-Traceability-Matrix.md |
| G-M04 | Must | O1; O5; O8; O9; O11 | RQ1; RQ4 | PD-06; PD-07 | AC-G-M04 | docs/architecture/ |
| G-M05 | Must | O11 | All RQs | PD-05; FR-05 | AC-G-M05 | docs/risks/ |
| G-M06 | Must | O11 | All RQs | PD-04; PD-08; FR-05 | AC-G-M06 | docs/planning/Change-Log.md |
| G-M07 | Must | O11 | RQ1; RQ2; RQ3 | PD-03 | AC-G-M07 | docs/evidence/Evidence-Recovery-Index.md |
| G-M08 | Must | O11 | RQ4 | FR-04 | AC-G-M08 | docs/manuals/ |
| G-M09 | Must | O11 | All RQs | FR-05 | AC-G-M09 | release-evidence/ |
| F-S01 | Should | O2 | RQ4 | IM-03 | AC-F-S01 | docs/evidence/requirements/F-S01/ |
| F-S02 | Should | O2; O8 | RQ1; RQ4 | IM-06; OV-02 | AC-F-S02 | docs/evidence/requirements/F-S02/ |
| F-S03 | Should | O2 | RQ4 | IM-06 | AC-F-S03 | docs/evidence/requirements/F-S03/ |
| F-S04 | Should | O3; O4 | RQ2; RQ3; RQ4 | HE-06; HE-07 | AC-F-S04 | docs/evidence/requirements/F-S04/ |
| F-S08 | Should | O2; O8 | RQ4 | IM-07 | AC-F-S08 | docs/evidence/requirements/F-S08/ |
| F-S11 | Should | O8 | RQ1; RQ4 | OV-01; OV-02 | AC-F-S11 | docs/evidence/requirements/F-S11/ |
| F-S12 | Should | O10; O11 | RQ1; RQ2; RQ3 | FR-03 | AC-F-S12 | docs/evidence/requirements/F-S12/ |
| F-S15 | Should | O8 | RQ1 | OV-01; OV-03 | AC-F-S15 | experiments/raw-results/openvino-conversion/ |
| N-S02 | Should | O11 | RQ4 | FR-04 | AC-N-S02 | release-evidence/installer/ |
| C-02 | Could | O10; O11 | — | FR-03; FR-05 | Scope review | docs/evidence/requirements/C-02/ |
| C-03 | Could | O8; O10 | — | FR-03 | Scope review | docs/evidence/requirements/C-03/ |

## Audit result

- Active requirements: **72**
- Active Must Haves: **56**
- Duplicate IDs: **0**
- Active Must Haves with complete required mapping: **56/56**
- Active requirements missing traceability: **0**

The Excel RTM controls editable status and validation.
