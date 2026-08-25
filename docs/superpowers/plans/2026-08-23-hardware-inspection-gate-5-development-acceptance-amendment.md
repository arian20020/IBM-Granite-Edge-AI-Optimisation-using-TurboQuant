# Hardware Inspection Gate 5 Development Acceptance Amendment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a hash-bound, self-signed test-MSIX bundle on the development host and execute exactly three registered-AUMID acceptance repetitions inside a disposable Windows guest.

**Architecture:** Extend the existing proven signing launcher with a preparation-only parameter set so private-key operations remain host-only. Add one standalone elevated guest runner that validates the entire closed bundle before importing the public certificate, installing the package, activating its AUMID, writing a development-only summary, and cleaning all guest state.

**Tech Stack:** Windows PowerShell 5.1, AppX/MSIX cmdlets, Windows SDK `makeappx.exe` and `signtool.exe`, `IApplicationActivationManager`, Python `unittest` contract/integration tests.

**Spec:** `docs/superpowers/specs/2026-08-23-hardware-inspection-gate-5-development-acceptance-amendment-design.md`

## Global Constraints

- Never disable or weaken Smart App Control, App Control, Defender, Secure Boot, or vTPM.
- Never export or copy a private key; the bundle contains only a DER public certificate.
- Preserve package name `GraniteEdgeAI.WinUI.UnitTests`, publisher `CN=GraniteEdgeAI`, version `1.0.0.0`, x64 architecture, application ID `App`, and the existing closed AUMID command.
- Bundle schema is `granite.hardware-inspection.development-acceptance-bundle/v1`; campaign schema is `granite.hardware-inspection.development-acceptance/v1`.
- Bundle input is read-only in the guest; output is a separate empty writable directory.
- A development campaign is exactly three sequential repetitions with fresh 32-lowercase-hex tokens and no retry.
- Development evidence always records `publicTrustVerified: false` and `smartAppControlVerified: false`.
- The original public-trust/SAC release requirement remains open.

---

### Task 1: Add the preparation-only signed bundle mode

**Files:**
- Modify: `scripts/hardware-inspection/Invoke-SignedHardwareInspectionAcceptance.ps1`
- Create: `tests/testing/hardware_inspection/test_development_acceptance_bundle.py`

**Interfaces:**
- Consumes the existing certificate thumbprint, configuration, exact built test MSIX, staging inventory, signing, and verification logic.
- Produces `Invoke-SignedHardwareInspectionAcceptance.ps1 -CertificateThumbprint <40-hex> -DevelopmentBundleDirectory <absolute-empty-directory>`.
- Produces exactly `GraniteEdgeAI.UnitTests.msix`, `GraniteEdgeAI.cer`, `Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1`, and `bundle-manifest.json`.

- [ ] **Step 1: Write the RED bundle-boundary tests**

Add subprocess tests that invoke the real script and require rejection of a relative output path, filesystem root, repository-contained output, dirty output directory, and output/bundle reparse points. Add a source inventory mutation test so a fourth payload filename or private-key extension fails the closed inventory helper.

- [ ] **Step 2: Run RED and confirm the missing parameter**

Run:

```powershell
python -m unittest tests.testing.hardware_inspection.test_development_acceptance_bundle -v
```

Expected: failures identify the absent `DevelopmentBundleDirectory` parameter and bundle contract.

- [ ] **Step 3: Implement preparation-only mode**

Add an optional absolute `DevelopmentBundleDirectory` parameter. Before any staging work, require a non-root path outside the repository, test output, AppPackages, and owned temporary roots; require the directory to exist, be empty, and contain no reparse point. In bundle mode, do not require or mutate `LocalMachine\TrustedPeople`.

After the existing exact package staging, payload signing, manifest regeneration, MSIX packing, and Authenticode verification pass, copy the MSIX and guest runner, export the public certificate with `Export-Certificate -Type CERT`, and reject any private-key extension. Write canonical JSON with package identity fields and an ordinal three-member `(name,length,sha256)` array, UTF-8 without BOM plus one LF. Re-read and verify the manifest and exact four-file inventory before returning a privacy-safe summary. Retain existing install/AUMID behavior when the new parameter is absent.

- [ ] **Step 4: Run GREEN and build a real temporary bundle**

Run the focused Python suite, then invoke the real script against an owned empty temporary output directory using certificate `380F89E98D799D39B7C38D5BCC94765208F032B7`. Require four files, valid MSIX signature, public certificate without private key, exact hashes/lengths, and no installed test package.

- [ ] **Step 5: Commit Task 1**

```powershell
git add scripts/hardware-inspection/Invoke-SignedHardwareInspectionAcceptance.ps1 `
  tests/testing/hardware_inspection/test_development_acceptance_bundle.py
git commit -m "feat(hardware-inspection): prepare isolated acceptance bundle"
```

---

### Task 2: Add the fail-closed disposable Windows guest runner

**Files:**
- Create: `scripts/hardware-inspection/Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1`
- Modify: `tests/testing/hardware_inspection/test_development_acceptance_bundle.py`

**Interfaces:**
- Consumes `-BundleDirectory <absolute-path>` and `-ResultDirectory <absolute-empty-path>`.
- Produces one `development-acceptance.json` summary only after exactly three passing registered-AUMID activations.

- [ ] **Step 1: Write RED validation-before-mutation tests**

Invoke the real guest script against bounded temporary bundles. Require malformed UTF-8, BOM/CRLF, duplicate/unknown JSON properties, extra/missing files, wrong byte length/hash, wrong package identity, same input/output path, dirty result directory, and reparse points to fail before the script reaches its elevation, certificate, or AppX mutation boundary. Assert zero new result files.

- [ ] **Step 2: Run RED and confirm the guest runner is absent**

Run:

```powershell
python -m unittest tests.testing.hardware_inspection.test_development_acceptance_bundle -v
```

Expected: guest-runner cases fail because the script does not exist.

- [ ] **Step 3: Implement strict bundle validation and guest execution**

Parse the manifest with DTD prohibited and duplicate-property detection, then enforce exact property order, values, framing, file inventory, lengths, and hashes before checking elevation. Validate the public certificate subject, thumbprint, validity, code-signing EKU, and absence of a private key. Require a disposable-guest confirmation switch, import only the public certificate into `LocalMachine\TrustedPeople`, normally install the exact MSIX, validate `SignatureKind=Developer` and closed identity, and activate `<PackageFamilyName>!App` exactly three times through `IApplicationActivationManager`.

Reuse the existing 64-KiB one-LF acceptance grammar. Each repetition must have `total=passed`, no failures, and a fresh token. Atomically write a bounded canonical development-only summary. In `finally`, remove only the exact invocation-installed package, the invocation-imported certificate, and token results; then prove absence. Cleanup failure overrides campaign success.

- [ ] **Step 4: Run GREEN and static privacy/security audits**

Run the focused Python suite and Stage 0 inventory test. Audit that the guest script contains no PFX/P12/private-key export, policy changes, loose package registration, payload-by-path launch, retry, network operation, raw exception/result retention, or caller-supplied AUMID/argument.

- [ ] **Step 5: Commit Task 2**

```powershell
git add scripts/hardware-inspection/Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1 `
  tests/testing/hardware_inspection/test_development_acceptance_bundle.py
git commit -m "test(hardware-inspection): run acceptance in disposable guest"
```

---

### Task 3: Document and execute Azure development acceptance

**Files:**
- Modify: `scripts/README.md`
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `tests/testing/hardware_inspection/test_intel_runner_stage0_contract.py`
- Create: `docs/testing/runbooks/Hardware-Inspection-Development-Acceptance-Runbook.md`
- Create after a valid campaign: `docs/testing/evidence/2026-08-23-hardware-inspection-gate-5-development-acceptance.md`

**Interfaces:**
- Documents preparation, transfer, Azure execution, result retrieval, VM/resource-group deletion, and the exact nonclaim.
- Adds only the two new approved scripts and runbook to the Stage 0 closed inventory.

- [ ] **Step 1: Write RED Stage 0 inventory expectations**

Add the new host/guest scripts and runbook to the exact approved inventory and require the security selector to continue rejecting aliases, network-changing scripts, certificate/private-key artifacts, and operational runner variants.

- [ ] **Step 2: Run RED, update documentation, and run GREEN**

Document an Azure Windows 11 x64 Gen2 Trusted Launch VM with Secure Boot/vTPM, non-Spot capacity, source-IP-scoped RDP, no repository/private-key transfer, bundle-only upload, and deletion of the entire resource group after retrieving the bounded summary. State exactly: `Development acceptance passed in a disposable guest. Smart App Control and public-trust signing remain unverified.` Run the focused and full Hardware Inspection Python suites.

- [ ] **Step 3: Execute the real Azure campaign**

Create the real bundle on the host, transfer only its four files, run the elevated guest script with disposable-guest confirmation, retrieve only `development-acceptance.json`, validate the closed schema and three passing repetitions locally, then delete the Azure resource group. Do not retain RDP files, public IPs, VM disks, certificates, MSIX files, or raw guest state as evidence.

- [ ] **Step 4: Run regressions and record evidence**

Run the Foundation 201-test suite, probe 22-test suite, full Hardware Inspection Python suite, Debug x64 test MSIX build, Release x64 application MSIX build, package inventories, installed-package cleanup, `git diff --check`, and repository privacy/artifact audits. Record exact counts and the development-only classification; leave public-trust/SAC evidence pending.

- [ ] **Step 5: Request review and commit evidence**

Use `superpowers:requesting-code-review`, resolve every finding test-first, then use `superpowers:verification-before-completion`. Commit documentation and evidence only after the real three-run campaign and Azure resource-group deletion are proven.

