# Automation Scripts

Store repeatable build, test, benchmark, evidence-validation and release scripts here.

## Hardware Inspection

- `hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1`
  validates the hosted, repository-only Stage 0 dispatch context, strict approval
  manifest, and approved source identity. It does not contact a self-hosted
  runner or execute Hardware Inspection code.
- The operator boundary and verification commands are in
  [`Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md).
