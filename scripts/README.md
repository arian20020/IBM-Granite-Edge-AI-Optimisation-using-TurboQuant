# Automation Scripts

Store repeatable build, test, benchmark, evidence-validation and release scripts here.

## Hardware Inspection

- [`hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1`](hardware-inspection/Validate-HardwareInspectionIntelRunnerStage0.ps1)
  validates the hosted, repository-only Stage 0 dispatch context, strict approval
  manifest, and approved source identity. It does not contact a self-hosted
  runner or execute Hardware Inspection code.
- [`hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1`](hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1)
  validates the authorised Stage A dispatch, manifest, one-time label, fixed-local
  runner context, exact checkout, and fixed output boundary.
- [`hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1`](hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1)
  owns contained local restore/build/test, exact TRX validation, bounded cleanup,
  and the five-property JSON plus fixed Markdown summaries. Detailed logs and raw
  TRX stay local; no hardware, candidate, trusted-Intel, offline, or Gate 1
  evidence is produced.
- [`hardware-inspection/New-LlamaCppProbeManifest.ps1`](hardware-inspection/New-LlamaCppProbeManifest.ps1)
  creates the detached canonical inventory and SHA-256 manifest for the inactive
  packaged llama.cpp capability probe.
- [`hardware-inspection/Test-LlamaCppProbeManifest.ps1`](hardware-inspection/Test-LlamaCppProbeManifest.ps1)
  verifies the probe's flat AMD64 package, exact member inventory, canonical
  manifest, and executable hash without launching it.
- [`hardware-inspection/Invoke-SignedHardwareInspectionAcceptance.ps1`](hardware-inspection/Invoke-SignedHardwareInspectionAcceptance.ps1)
  prepares and normally installs the closed signed test MSIX, activates only its
  registered AUMID acceptance command, validates the bounded result, and removes
  invocation-owned package and temporary state. It does not activate production
  Hardware Inspection or weaken Smart App Control.
- The operator boundary and verification commands are in
  [`Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md).
- The separately authorised Stage A stopped-runner, one-time registration,
  privacy review, deregistration, and exact cleanup procedure is in
  [`Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md).
