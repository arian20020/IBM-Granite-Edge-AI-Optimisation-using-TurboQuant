# Hardware Inspection Development Acceptance Runbook

This runbook executes the self-signed Hardware Inspection test MSIX exactly three
times in a disposable Azure Windows guest. It produces development evidence only.
It does not establish public publisher trust or Smart App Control compatibility.

## Fixed boundaries

- Use a Windows 11 x64 Generation 2 Azure VM with Trusted Launch, Secure Boot,
  and vTPM enabled. Use non-Spot capacity.
- Limit inbound RDP to the operator's current public IP. Never use an `Any`
  source rule.
- Keep the repository, signing private key, PFX/P12 files, credentials, model
  data, and unrelated logs on the development host.
- Transfer only these four files:
  `GraniteEdgeAI.UnitTests.msix`, `GraniteEdgeAI.cer`,
  `Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1`, and
  `bundle-manifest.json`.
- Do not disable or weaken Smart App Control, App Control, Defender, Secure
  Boot, vTPM, or any other Windows security control.
- Do not edit any bundle member after the host creates the bundle.

## 1. Prepare the bundle on the development host

From the physical repository worktree, create an empty directory outside the
repository and build roots. Run Windows PowerShell with the purpose-specific
certificate thumbprint:

```powershell
$bundle = Join-Path $env:USERPROFILE 'Downloads\GraniteEdgeAI-HardwareInspection-Azure-Bundle'
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File .\scripts\hardware-inspection\Invoke-SignedHardwareInspectionAcceptance.ps1 `
  -CertificateThumbprint '<40-HEX-THUMBPRINT>' `
  -DevelopmentBundleDirectory $bundle `
  -Configuration Debug
```

The command must report `Classification: development-only`, `BundleReady:
True`, `PublicTrustVerified: False`, `SmartAppControlVerified: False`, and
`FileCount: 4`. The host exports only the DER public certificate. It never
exports the private key and does not install the test package in bundle mode.

## 2. Start and connect to the disposable guest

1. In Azure Portal, start the existing test VM only when the bundle is ready.
2. Confirm its network security group permits RDP only from the current public
   IP. Update `My IP` if the address changed.
3. Download the RDP connection file from the VM's **Connect** page and connect
   from the development host.
4. In the guest, create `C:\GraniteAcceptance\Bundle` and
   `C:\GraniteAcceptance\Result` as separate empty physical directories.
5. Copy only the four bundle files into `C:\GraniteAcceptance\Bundle`. Do not
   copy the repository or any signing private key.

## 3. Execute exactly one three-run campaign

Open **Windows PowerShell as Administrator** in the guest and run:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File C:\GraniteAcceptance\Bundle\Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1 `
  -BundleDirectory C:\GraniteAcceptance\Bundle `
  -ResultDirectory C:\GraniteAcceptance\Result `
  -ConfirmDisposableGuest
```

The runner validates the closed inventory, canonical manifest, byte lengths,
SHA-256 hashes, public certificate, and certificate identity before elevation or
guest mutation. It refuses a pre-existing test package, imports only the public
certificate, normally installs the exact x64 Developer-signed MSIX, resolves the
registered `<PackageFamilyName>!App` AUMID, and performs exactly three sequential
activations with fresh tokens and no retry. Each activation has a fixed
180-second result timeout.

Success creates only
`C:\GraniteAcceptance\Result\development-acceptance.json`. The runner removes
its package registration, imported certificate, and raw token results before it
publishes that summary. A cleanup failure overrides success.

If the command fails, preserve only the stable error message for diagnosis. Do
not bypass a validation, weaken a Windows policy, manually install a loose
package, retry an individual repetition, or treat a partial campaign as
evidence.

## 4. Retrieve and close the Azure boundary

1. Copy only `development-acceptance.json` back to the development host.
2. Confirm the summary uses schema
   `granite.hardware-inspection.development-acceptance/v1`, classification
   `development-only`, three ordered passing repetitions, an empty `failures`
   array, and both trust flags set to `false`.
3. Stop the VM and confirm Azure reports it **Stopped (deallocated)**.
4. After the summary has been retrieved and validated, delete the entire Azure
   resource group used for the campaign. Confirm the VM, disk, public IP, network
   interface, network security group, and transferred bundle are gone.
5. Do not retain RDP files, guest certificates, MSIX files, disks, public IPs,
   raw token results, or guest state as evidence.

The only permitted conclusion is:

> Development acceptance passed in a disposable guest. Smart App Control and public-trust signing remain unverified.
