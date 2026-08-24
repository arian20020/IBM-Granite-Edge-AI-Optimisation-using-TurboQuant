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
  invocation-owned package and temporary state. With
  `-DevelopmentBundleDirectory`, it instead prepares a hash-bound four-file
  development bundle without installing a package or exporting a private key.
  It does not activate production Hardware Inspection or weaken Smart App
  Control.
- [`hardware-inspection/Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1`](hardware-inspection/Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1)
  validates that closed bundle in an elevated disposable Windows guest, normally
  installs the exact x64 Developer package, activates the registered AUMID
  exactly three times without retry, writes one bounded development-only
  summary, and proves cleanup of invocation-owned package, certificate, and raw
  result state.
- [`hardware-inspection/Invoke-HardwareInspectionGate9Acceptance.ps1`](hardware-inspection/Invoke-HardwareInspectionGate9Acceptance.ps1)
  runs the exact production Hardware Inspection route three times on the
  supported physical Windows 11 x64 Intel laptop. The operator must make the
  network-disconnection precondition user-controlled and physically disconnected;
  the controller never changes adapters, firewall, proxy, Defender, or Smart App
  Control. It verifies the sibling bundle ZIP hash, the extracted four-file
  bundle, fixed ProgramData LLM Fit package, process tree, endpoint observations,
  package removal, and raw-result cleanup before publishing sanitized evidence.
- [`hardware-inspection/Test-HardwareInspectionGate9Summary.ps1`](hardware-inspection/Test-HardwareInspectionGate9Summary.ps1)
  validates the exact 16 KiB canonical Gate 9 summary. A Developer package can
  produce only `EngineeringPassedReleaseBlocked`; public release requires a
  separately validated Enterprise or Store trust record for the same package.
- The operator boundary and verification commands are in
  [`Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md).
- The separately authorised Stage A stopped-runner, one-time registration,
  privacy review, deregistration, and exact cleanup procedure is in
  [`Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md).
- The bundle-only Azure transfer, three-run disposable-guest campaign, exact
  nonclaim, and resource-group deletion procedure is in
  [`Hardware-Inspection-Development-Acceptance-Runbook.md`](../docs/testing/runbooks/Hardware-Inspection-Development-Acceptance-Runbook.md).
