# H1 hardware remediation R3

## Scope and disposition

H1 owns the hardware/native phase of **R3-019** and supports **R3-021** and
**R3-022** for H1 evidence, packaging, native execution, timeout, and cleanup.
This report does not claim closure of the combined H1→M1→Q1 sequence, the
integrated application journey, App Control acceptance, or the combined R3
campaign.

The authoritative H1 R2 base is
`e000ee4f7b1ecc68cac79d774d47e659c96b661c`
(tree `dc40d2effa2a0363094a069c118fda486b836260`). The frozen
ancestor remains `4748fe04f19afdf6b27c4c12502b84db325e7294`
(tree `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`).
The R3 evidence subject is
`32b0cab7e4c6c30b4b3d3c3588f7fc394e9d26eb`
(tree `115a4f250bb81e63c8e97941daa4c94ad41288a7`).

## R3 implementation

The R3 change adds a committed-evidence closure validator, deterministic
managed verification runner, bounded native runner, and behavioral Pester
regressions. The validator rejects stale campaigns, invalid arithmetic,
zero discovery, duplicate or oversized JSON, Git tree/ancestry/ref drift,
dirty worktrees, missing committed result blobs, false native cleanup, and
native receipts that do not hash the exact handoff bytes.

The R2 hardware behavior remains production-reachable and unchanged:

| Contract | Definition | Production caller | Composition/registration | Behavioral test |
| --- | --- | --- | --- | --- |
| Snapshot identity | `Features/HardwareInspection/Domain/HardwareSnapshotIdentity.cs:31` | `HardwareSnapshot.cs:79` | Hardware inspection production graph | Foundation identity/privacy tests |
| Memory safety budget | `shared/.../SystemMemoryBudget.cs:59` | `SafetyPolicy.cs:66` | Compatibility production policy | `SystemMemoryBudgetCalculatorTests` |
| Hardware graph | `HardwareInspectionComposition.cs:9` | `OnboardingShellPage.xaml.cs:108` | exactly one `CreateProduction` caller | `HardwareInspectionCompositionTests` |
| Probe package | `HardwareInspection.LlamaCppProbePackaging.targets:66` | application project import at line 650 | exactly one application import | `HardwareInspectionPackaging.Tests.ps1` |

The production reachability gate observed exactly one non-test caller for each
identity, budget, composition, and application-import relationship.

## RED-GREEN evidence

RED was captured outside Git below the private H1 results root:

- Task 1: the R2 manifest regression discovered one test and failed because the
  closure entry point was absent.
- Task 2: two controlled runner tests failed because the managed runner was
  absent.
- Task 4: four native fail-closed tests failed because the native runner was
  absent.
- The real managed run additionally exposed an inline PowerShell row-construction
  defect and missing pinned `DotNetHostPath` propagation; the corrected runner
  was rerun on the exact evidence subject.

GREEN on the evidence subject:

| Gate | Discovered / executed | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Hardware Foundation | 202 / 202 | 202 | 0 | 0 |
| llama.cpp probe/tool/fake-tool | 22 / 22 | 22 | 0 | 0 |
| Model/hardware compatibility | 1060 / 1060 | 1060 | 0 | 0 |
| H1 PowerShell, including R3 closure/native regressions | 30 / 30 | 30 | 0 | 0 |
| Debug x64 application build | 1 / 1 | 0 | 1 | 0 |
| Debug x64 unit build | 1 / 1 | 1 | 0 | 0 |
| Static package matrix | 4 / 4 | 4 | 0 | 0 |
| Manifest, privacy/duplication, reachability, diff/privacy scans | 4 / 4 | 4 | 0 | 0 |
| Model Inspection package boundary | 1 / 1 | 0 | 1 | 0 |
| **Total** | **1325 / 1325** | **1323** | **2** | **0** |

The Debug x64 application build reached an OpenVINO-owned package prerequisite
and failed because `OpenVinoOfficialWorkerStageDirectory` was not supplied.
The separate Model Inspection boundary remained failed on the inherited GGUF
`@(_GgufRuntimePublishedFiles)` expression. Neither row is represented as
passing. The Debug x64 unit build, all four static configurations, and the
H1-owned package manifest/privacy/duplication gates passed.

## Native phase

The shared lock was absent and the pre-run probe-process count was zero. H1
atomically acquired the native slot and executed the existing
manifest-verified packaged probe:

| Command | Exit | stdout bytes | stderr bytes | Timeout |
| --- | ---: | ---: | ---: | ---: |
| `identity --format json-v1` | 0 | 366 | 0 | no |
| `capabilities --format json-v1` | 0 | 148 | 0 | no |

Both commands used a 30-second bound. Post-process count was zero, cleanup was
verified, and the shared native lock was released. Only byte counts and stable
disposition fields are committed; provider output is excluded.

## Changed paths

- `docs/superpowers/specs/2026-08-29-h1-r3-closure-design.md`
- `docs/superpowers/plans/2026-08-29-h1-r3-closure.md`
- `scripts/audits/h1_r3_closure.py`
- `scripts/audits/Test-H1R3Closure.ps1`
- `scripts/audits/Invoke-H1R3ManagedVerification.ps1`
- `scripts/audits/Invoke-H1R3NativeVerification.ps1`
- `tests/PowerShell/H1R3Closure.Tests.ps1`
- this report and the three files in `docs/audits/2026-08-29/evidence`

## Security, privacy, and external blockers

No tool or asset was downloaded. No App Control setting, signing rule, trust
root, certificate, firewall rule, hash, manifest, onboarding/navigation,
shared composition, GGUF packaging, Model Inspection, or OpenVINO behavior was
changed. Committed evidence contains no raw provider response, host/user
identity, local provider path, credential, token, prompt, model data, or
private evidence path.

External continuation is still required from M1 and Q1 for the remainder of
R3-019, from C0 for integrated composition/reconciliation, and from E1 for
current integrated package/App Control/end-to-end acceptance under R3-020 and
R3-022. H1 makes no package-wide or combined-campaign success claim.
