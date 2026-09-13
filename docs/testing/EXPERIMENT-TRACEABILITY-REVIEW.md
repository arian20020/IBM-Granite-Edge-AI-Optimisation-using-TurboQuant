# Experiment traceability review

This page reviews the research requirements against the saved experiment
evidence. It is a handoff to the owner of the controlled Requirements
Traceability Matrix (RTM). It does not change the official requirement status
by itself.

The status words used here are:

- **Supported:** the experiment evidence covers the requirement within its
  stated limits.
- **Partly supported:** useful evidence exists, but an important planned part is
  missing.
- **Not completed:** the required study or independent check was not done.
- **Deferred:** the controlled scope moved the work out of the first release.

| Requirement | Review status | Evidence and remaining gap |
| --- | --- | --- |
| R-M01 | Partly supported | Matched standard-cache and TurboQuant observations exist in the [route packages](final-results/README.md), with activation evidence and controlled settings. The separate `EXP-TQ-COMP-001` package was not completed. |
| R-M02 | Supported | The TurboVec implementation and commit are pinned, the tests produced a decision, and the [decision record](../architecture/decisions/ADR-TurboVec.md) says demonstrator only. Public reuse still needs the recorded licence checks. |
| R-M03 | Supported | The [official OpenVINO package](final-results/05-openvino-official-upstream/README.md) records 15 passed, 5 failed and 25 blocked cases, including reproducible conversion and safety outcomes. |
| R-M04 | Partly supported | Memory values are saved where the route measured them. The package clearly separates peak working set from explicit KV allocation, but not every route captured every memory field. |
| R-M05 | Partly supported | Performance values are saved for completed measured cases. Blocked, failed-before-inference and unavailable cases correctly have no invented performance value. Some requested measures were not collected by every route. |
| R-M06 | Supported with limits | Fixed prompt sets and scoring guides were used within each campaign. The [quality policy](final-results/standards/quality-comparison-policy.md) prevents ranking incompatible route scores. Most quality outputs were generated once, and no human agreement study was done. |
| R-M07 | Partly supported | Selected 4K, long-prompt and one recovered 16K case provide bounded context evidence. A systematic step test across selected final configurations was not completed. |
| R-M08 | Supported with limits | All 169 intended inference attempts are present in the final ledger, with manifests and checksums. The validation record reports limited column-level re-derivability for some historical summaries. |
| R-M09 | Supported | Failed, blocked, rejected and unavailable work is kept in the [failure summary](final-results/catalog/failure-summary.csv) and route evidence. |
| R-M10 | Not completed | The package is self-contained and has written [validation instructions](final-results/REPRODUCING.md), but no independent developer reproduction is recorded. |
| R-M11 | Supported with limits | The packages show which selected configurations ran on the tested Intel system. Some placement evidence is partial, and there is no independent OpenVINO device trace for every row. |
| R-M12 | Partly supported | The tests give measured observations on the computer with about 16 GB of RAM. Physical 4 GB and 8 GB systems and a calibrated budget study were not tested. |
| R-M13 | Deferred; supporting study completed | Product integration remains deferred. The later TurboVec supporting study compared Exact, TQ2, TQ3 and TQ4, but it does not reactivate the requirement or approve application integration. |
| R-M14 | Not completed | The planned estimator study did not measure prediction error, false-safe results or false-unsafe results. |

## RTM update rule

The controlled RTM owner should review these links against each acceptance
criterion before changing a formal status. A passing package check proves that
the files are complete and consistent. It does not automatically prove every
research or application requirement.
