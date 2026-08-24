# Hardware Inspection Gate 9 Release Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce privacy-safe, exact-head engineering acceptance for the production Hardware Inspection route on the UCL Windows 11 x64 Intel laptop under controlled offline/no-endpoint conditions, while keeping public-release trust explicitly blocked unless a genuinely trusted signing identity passes.

**Architecture:** Add a second fixed activation host to the existing packaged WinUI test application. It invokes only `HardwareInspectionComposition.CreateProduction()` and emits a bounded local result; a fail-closed PowerShell controller owns target preflight, package installation, three repetitions, endpoint observation, cleanup, canonical summary publication, and the separate release-trust disposition. Finish with supplemental Block 2 traceability and evidence rather than manually changing stale generated RTM status.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK 2.2, MSTest 4.3.2 packaged tests, PowerShell 5.1, Python 3.11 contract tests, Windows package identity/X509/network/process APIs, signed MSIX development acceptance.

**Spec:** `docs/superpowers/specs/2026-08-24-hardware-inspection-gate-9-release-closure-design.md`

## Global Constraints

- Work only on `integration/hardware-inspection-intel-completion-v1` in the existing isolated worktree.
- Use the UCL Windows 11 x64 Intel laptop for Gate 9 target execution; the AMD Azure guest is not a supported-target substitute.
- Do not download, commit, redistribute, re-sign, or weaken verification of LLM Fit v1.1.9. The exact package must already exist under `%ProgramData%\GraniteEdgeAI\HardwareInspection\llmfit\1.1.9\win-x64`.
- Do not disable or bypass Smart App Control, Defender, Secure Boot, vTPM, firewall, certificate validation, or package identity checks.
- Network disconnection is a user-controlled precondition; the controller must not mutate adapter, proxy, firewall, or execution-policy state.
- Keep model input, compatibility/fit logic, Block 3, provider authority, the seven stages, four outcomes, lifecycle, and x86 exclusion unchanged.
- Never retain raw hardware values, stdout/stderr, exception messages, paths, usernames, hostnames, device/adapter names, addresses, identifiers, or package layouts in canonical evidence.
- Raw packages, certificates, per-run results, monitoring events, TRX, and machine-specific data stay local and untracked.
- A `Developer` signature always means `publicTrustVerified=false`; local certificate import cannot upgrade release trust.
- All behavior changes are test-first. Every task ends with focused GREEN verification and a narrow commit.

---

### Task 1: Extract the common bounded acceptance-result store

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Acceptance/HardwareInspectionAcceptanceResultStore.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Acceptance/HardwareInspectionAcceptanceResultStoreTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Acceptance/HardwareInspectionProcessAcceptanceHost.cs`

**Interfaces:**
- Consumes: a 32-character lowercase hexadecimal result token and canonical JSON bytes without their terminating LF.
- Produces: `internal static bool IsResultToken(string value)`, `internal static string GetResultPath(string token)`, and `internal static void WriteAtomically(string token, ReadOnlySpan<byte> json, int maximumBytes)`.

- [ ] **Step 1: Write RED result-store tests**

Add MSTest cases requiring invalid-token rejection, a maximum-size bound that includes the final LF, no BOM/CR, exactly one final LF, `CreateNew`/no-overwrite behavior, token containment under `%TEMP%\GraniteEdgeAI.HardwareInspection.Tests\Acceptance`, and temporary-file cleanup after a failed publication.

```csharp
[TestMethod]
public void WriteAtomically_PublishesExactUtf8WithOneLf()
{
    string token = Guid.NewGuid().ToString("N");
    ReadOnlySpan<byte> json = "{\"ok\":true}"u8;

    HardwareInspectionAcceptanceResultStore.WriteAtomically(token, json, 1024);

    byte[] actual = File.ReadAllBytes(
        HardwareInspectionAcceptanceResultStore.GetResultPath(token));
    CollectionAssert.AreEqual("{\"ok\":true}\n"u8.ToArray(), actual);
}
```

- [ ] **Step 2: Run the focused build/test and confirm RED**

Build Release/x64 with Visual Studio MSBuild. On a package-capable host, run the result-store class and require failures caused only by the missing store. Do not weaken Smart App Control to execute it locally.

- [ ] **Step 3: Implement the minimal store**

Use `Path.GetTempPath()`, fixed path segments, `FileMode.CreateNew`, `FileShare.None`, `Flush(true)`, a same-directory random temporary name, `File.Move`, and `finally` cleanup. Reject an empty payload, a payload containing CR/LF or UTF-8 BOM framing, invalid tokens, root escape, and `json.Length + 1 > maximumBytes`.

```csharp
internal static class HardwareInspectionAcceptanceResultStore
{
    internal static bool IsResultToken(string value) =>
        value.Length == 32 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static void WriteAtomically(
        string token,
        ReadOnlySpan<byte> json,
        int maximumBytes);
}
```

- [ ] **Step 4: Refactor the Gate 8 host without changing its schema**

Replace its private token/path/write helpers with the store. Preserve schema `granite.hardware-inspection.process-acceptance/v1`, the exact 87-test inventory, package identity, maximum-128 discovery bound, result maximum, atomicity, and exit codes.

- [ ] **Step 5: Verify and commit**

Build Release/x64; require the Gate 8 discovery test to retain exactly 87 unique tests. Run `git diff --check`, then commit:

```text
refactor(hardware-inspection): share bounded acceptance results
```

### Task 2: Add the production Gate 9 packaged host

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Acceptance/HardwareInspectionGate9AcceptanceHost.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/Acceptance/HardwareInspectionGate9AcceptanceHostTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml.cs`

**Interfaces:**
- Consumes: `--hardware-inspection-gate9-acceptance --result-token <32 lowercase hex>` and `HardwareInspectionComposition.CreateProduction()`.
- Produces: `TryParseActivation(IReadOnlyList<string>, out string)`, `RunAsync(string) : Task<int>`, and local schema `granite.hardware-inspection.gate9-production-run/v1`.

- [ ] **Step 1: Write RED parsing and result-contract tests**

Require the exact four-argument command, lowercase token, exact package name `GraniteEdgeAI.WinUI.UnitTests`, fixed JSON property order, maximum 4 KiB result, safe outcome enum, `stageCount=7`, `handoffPresent=true`, `manifestFieldCount=19`, unique sorted diagnostic tokens, and no raw `Snapshot` serialization.

Use an internal injected overload so deterministic tests can supply an `IHardwareInspectionService` and package-identity predicate without reaching production tools:

```csharp
internal static Task<int> RunAsync(
    string resultToken,
    IHardwareInspectionService service,
    Func<bool> packageIdentityPresent);
```

Add cases for Completed, CompletedWithWarnings, failed, cancelled, wrong progress identity, duplicate/out-of-order/missing stages, missing handoff, wrong manifest count, unsafe diagnostic, service exception, and publication failure. Only completed/warning with seven ordered stages, a handoff, and 19 entries may exit zero.

- [ ] **Step 2: Run focused verification and confirm RED**

Build Release/x64. The intended RED is the missing Gate 9 host and launch branch; all Gate 8 code must still compile.

- [ ] **Step 3: Implement the host**

Use a fresh `Guid`, `Progress<HardwareInspectionRunProgress>`, and caller-independent bounded timeout/cancellation. Validate progress internally against the exact ordered `HardwareInspectionRunStage` values and monotonically increasing sequence. Emit only:

```json
{"schema":"granite.hardware-inspection.gate9-production-run/v1","packageIdentityPresent":true,"outcome":"CompletedWithWarnings","stageCount":7,"handoffPresent":true,"manifestFieldCount":19,"diagnostics":[]}
```

For failed outcomes, the only permitted diagnostic is `SafeDiagnosticCode`; reject unsafe or multiple arbitrary values. Catch exceptions only at the host boundary and return stable exit code 70 without serializing exception text.

- [ ] **Step 4: Add the isolated launch branch**

In `UnitTestApp.OnLaunched`, parse Gate 9 before falling through to Gate 8/normal MSTest. Create and activate one `UnitTestAppWindow`, set `UITestMethodAttribute.DispatcherQueue`, await the Gate 9 host, close the window in nested `finally`, and call `Environment.Exit(exitCode)`. Factor only the duplicated window/exit helper needed by the two acceptance modes.

- [ ] **Step 5: Verify and commit**

Build Release/x64 and Debug/x64 packaged tests with zero errors. Confirm the Gate 8 inventory remains 87 and neither activation parser accepts the other's command. Commit:

```text
test(hardware-inspection): add Gate 9 production host
```

### Task 3: Lock the sanitized Gate 9 summary contract

**Files:**
- Create: `scripts/hardware-inspection/Test-HardwareInspectionGate9Summary.ps1`
- Create: `tests/testing/hardware_inspection/test_gate9_summary_contract.py`
- Modify: `tests/testing/hardware_inspection/test_development_acceptance_bundle.py`

**Interfaces:**
- Consumes: one local summary path and optional exact expected 40-character lowercase commit.
- Produces: exit zero with no stdout/stderr for a valid summary; one fixed stderr line and nonzero exit for invalid input.

- [ ] **Step 1: Write RED Python contract tests**

Construct exact valid bytes and mutation cases for BOM, CR, missing/final extra LF, invalid UTF-8, duplicate/case-drifted/extra/missing properties, wrong schema/classification/commit, target false/missing, repetition order/count, unsafe diagnostics, inconsistent outcomes, cleanup/offline/endpoint false, failure entries, invalid signature kind, and contradictory disposition/trust.

```python
VALID = (
    b'{"schema":"granite.hardware-inspection.gate9-engineering-acceptance/v1",'
    b'"classification":"local-sanitized","evaluatedCommit":"' + b"a" * 40 + b'",'
    b'"target":{"windows11":true,"x64":true,"intel":true,"physical":true},'
    b'"offline":true,"noRelevantNetworkEndpointObserved":true,'
    b'"repetitions":['
    b'{"run":1,"packageIdentityPresent":true,"outcome":"CompletedWithWarnings",'
    b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,"diagnostics":[]},'
    b'{"run":2,"packageIdentityPresent":true,"outcome":"CompletedWithWarnings",'
    b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,"diagnostics":[]},'
    b'{"run":3,"packageIdentityPresent":true,"outcome":"CompletedWithWarnings",'
    b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,"diagnostics":[]}],'
    b'"cleanupVerified":true,"failures":[],'
    b'"releaseTrust":{"signatureKind":"Developer","publicTrustVerified":false,'
    b'"smartAppControlVerified":false},'
    b'"disposition":"EngineeringPassedReleaseBlocked"}\n'
)
```

Require exactly three ordered repetitions, each with package identity, Completed/CompletedWithWarnings, seven stages, handoff, 19 fields, and bounded diagnostics. `Developer` must imply both trust booleans false and blocked disposition. `Passed` requires both trust booleans true and signature kind `Enterprise` or `Store`.

- [ ] **Step 2: Run the new Python file and confirm RED**

Use Python 3.11 and process-scoped PowerShell bypass. Existing 63 contracts must remain unchanged.

- [ ] **Step 3: Implement the strict validator**

Read at most 16 KiB, validate framing before `ConvertFrom-Json`, parse with duplicate-property detection through `System.Text.Json` or an exact lexical contract, enforce exact ordered property sets, allowed scalar types/values, unique sorted safe diagnostics, and no additional content. Emit only:

```text
HI-GATE9-SUMMARY-INVALID: canonical summary validation failed.
```

to stderr on any failure.

- [ ] **Step 4: Verify and commit**

Run the new contract file, then the complete Hardware/runner Python suite. Commit:

```text
test(hardware-inspection): lock Gate 9 summary contract
```

### Task 4: Implement the offline Intel target controller

**Files:**
- Create: `scripts/hardware-inspection/Invoke-HardwareInspectionGate9Acceptance.ps1`
- Create: `tests/testing/hardware_inspection/test_gate9_acceptance_controller.py`
- Modify: `scripts/README.md`

**Interfaces:**
- Consumes: `-BundleDirectory`, `-ExpectedBundleSha256`, `-ExpectedCommit`, `-ConfirmSupportedIntelTarget`, and an optional `-ReleaseTrustRecord` path.
- Produces: `gate9-engineering-acceptance.json` only after three successful repetitions and complete cleanup.

- [ ] **Step 1: Write RED controller structure and adversarial tests**

Require strict parameters, Administrator check, Windows 11/x64, exact Intel manufacturer allowlist, fixed non-virtual manufacturer/model rejection rules, disconnected physical adapters, exact four-file bundle/manifest/hash/signature, exact ProgramData LLM Fit package, no reparse points, registered-AUMID activation, three runs, a maximum 180-second result timeout, 20 ms bounded endpoint polling, descendant ownership by parent PID, zero relevant TCP Listen/Bound and UDP endpoints, no residual processes, owned package removal, token-result cleanup, and canonical summary validation before publication.

Assert source absence of `Set-NetAdapter`, `Disable-NetAdapter`, `Enable-NetAdapter`, `New-NetFirewallRule`, `Set-NetFirewallProfile`, proxy mutation, persistent `Set-ExecutionPolicy`, Smart App Control/Defender mutation, download/network clients, wildcard deletion, and raw host-value serialization.

- [ ] **Step 2: Confirm RED**

Run only `test_gate9_acceptance_controller.py`; failures must identify the missing controller, not an environment prerequisite.

- [ ] **Step 3: Implement fail-closed preflight**

Use Windows-owned CIM/network/package APIs. Convert target data immediately into booleans, then clear/drop raw objects before summary construction. Resolve every path with `GetFullPath`, reject filesystem roots/reparse ancestors, require fixed filenames and SHA-256, and validate the existing bundle manifest before installing.

The candidate root is fixed, never a parameter:

```powershell
$llmFitRoot = Join-Path $env:ProgramData (
    'GraniteEdgeAI\HardwareInspection\llmfit\1.1.9\win-x64'
)
```

Require exact `llmfit.exe`, `LICENSE`, and `README.md` inventory and the production executable SHA-256 `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19`.

- [ ] **Step 4: Implement activation, observation, and cleanup**

Use `IApplicationActivationManager` as the existing signed-acceptance script does. Track only the activated PID and descendants discovered through exact parent relationships. Poll `Get-NetTCPConnection` and `Get-NetUDPEndpoint` for owned PIDs; set one boolean on any endpoint and retain no address/port. Validate each raw production result, delete it, terminate/observe the owned tree, and remove only the exact package installed by this invocation.

Always run cleanup through nested `finally`. Do not publish a summary if monitoring completeness, result validation, package removal, or process cleanup is uncertain.

- [ ] **Step 5: Implement trust disposition without promotion**

For `Developer`, write `publicTrustVerified=false`, `smartAppControlVerified=false`, and `EngineeringPassedReleaseBlocked`. If a release-trust record is supplied, require its package payload hash to equal the installed package and validate it through Task 5 before allowing `Passed`.

- [ ] **Step 6: Verify and commit**

Run controller and summary contract tests plus the full Python floor. Parse the PowerShell AST, run `git diff --check`, and commit:

```text
feat(hardware-inspection): add Gate 9 target acceptance
```

### Task 5: Add the separate release-trust validator

**Files:**
- Create: `scripts/hardware-inspection/Test-HardwareInspectionGate9ReleaseTrust.ps1`
- Create: `tests/testing/hardware_inspection/test_gate9_release_trust.py`

**Interfaces:**
- Consumes: a canonical local release-trust record, exact package SHA-256, expected publisher, and expected commit.
- Produces: exit zero only for a matching `Enterprise`/`Store` package with public chain and Smart App Control verified; fixed nonzero failure otherwise.

- [ ] **Step 1: Write RED release-trust mutations**

Cover Developer/self-signed, local private-root substitution, wrong package hash, wrong commit/publisher, invalid/expired/not-yet-valid certificate, missing code-signing EKU, invalid chain, absent timestamp when required, Smart App Control false/unavailable, extra fields, unsafe text, and mismatched signature kind.

- [ ] **Step 2: Implement the strict record validator**

Use a maximum 8 KiB canonical UTF-8/LF schema. Accept no certificate bytes or chain details in retained evidence—only exact package hash, commit, publisher identity token, `Enterprise|Store`, and verified booleans. Never mutate certificate stores. Emit fixed stderr only:

```text
HI-GATE9-RELEASE-TRUST-INVALID: release trust validation failed.
```

- [ ] **Step 3: Prove Developer remains blocked**

Validate a fixture matching the existing `CN=GraniteEdgeAI` developer certificate and require rejection. Validate a synthetic structurally correct trusted record only at the pure parser layer; do not claim real trust from the fixture.

- [ ] **Step 4: Verify and commit**

Run all Gate 9 Python contract files and commit:

```text
test(hardware-inspection): enforce Gate 9 release trust
```

### Task 6: Build and review the exact-head Gate 9 development bundle

**Files:**
- Modify only if a failing contract proves necessary: `scripts/hardware-inspection/Invoke-SignedHardwareInspectionAcceptance.ps1`
- Test: `tests/testing/hardware_inspection/test_development_acceptance_bundle.py`
- Generated outside repository: `GraniteEdgeAI-HardwareInspection-Gate9-Bundle-v1/` and ZIP

**Interfaces:**
- Consumes: exact reviewed code head and existing purpose-specific developer signing identity.
- Produces: the established four-file development bundle; Gate 9 controller remains a reviewed repository script and is not substituted inside the bundle.

- [ ] **Step 1: Rebuild from the physical exact-head worktree**

Build Debug/x64 packaged tests and Release/x64 app MSIX with Visual Studio MSBuild, portable SDK 10.0.301, `RuntimeIdentifier=win-x64`, `GenerateAppxPackageOnBuild=true`, `AppxBundle=Never`, and zero errors.

- [ ] **Step 2: Publish and verify the development bundle**

Use `Invoke-SignedHardwareInspectionAcceptance.ps1 -DevelopmentBundleDirectory` with the existing developer identity. Require exactly `GraniteEdgeAI.UnitTests.msix`, `GraniteEdgeAI.cer`, `Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1`, and `bundle-manifest.json`; validate every manifest length/hash and the ZIP SHA-256.

- [ ] **Step 3: Inline exact-range review**

Review activation isolation, production composition, result atomicity/bounds/privacy, target preflight, endpoint ownership, race/timeout/cancellation, package/process cleanup, certificate non-promotion, x86 exclusion, and every summary field. Fix findings test-first. Record that review is inline, not independent.

- [ ] **Step 4: Commit any review corrections**

If code changes, rebuild and regenerate the bundle from the new exact head. Commit each correction narrowly; otherwise do not create an empty review commit.

### Task 7: Execute supported-target engineering acceptance

**Files:**
- Local untracked input: exact LLM Fit v1.1.9 three-file package under the fixed ProgramData root
- Local untracked input: exact Gate 9 bundle
- Local untracked output: `gate9-engineering-acceptance.json`

**Interfaces:**
- Consumes: physically disconnected UCL Windows 11 x64 Intel laptop and explicit `-ConfirmSupportedIntelTarget`.
- Produces: one canonical sanitized summary or no summary on failure.

- [ ] **Step 1: Establish the controlled target**

Confirm the UCL laptop is the Intel physical target, close relevant processes, disconnect Wi-Fi/Ethernet without using the controller to change them, and verify the exact ProgramData tool inventory. Keep Smart App Control, Defender, firewall, Secure Boot, and other controls unchanged.

- [ ] **Step 2: Run the controller once**

Invoke Administrator PowerShell with process-scoped execution-policy bypass only for the signed reviewed script. Supply exact bundle ZIP SHA-256 and exact 40-character reviewed commit. Require three ordered production repetitions.

- [ ] **Step 3: Validate before reconnecting**

Run `Test-HardwareInspectionGate9Summary.ps1` locally. Require target booleans true, offline true, no relevant endpoint observed, three successful Completed/CompletedWithWarnings runs, seven stages, handoff true, 19 fields, cleanup true, empty failures, Developer trust false, and `EngineeringPassedReleaseBlocked` unless real release-trust evidence exists.

- [ ] **Step 4: Restore user-controlled connectivity and transfer only the summary**

Reconnect through normal user controls after cleanup. Transfer only `gate9-engineering-acceptance.json`; do not transfer raw results, monitoring events, package layouts, certificate private material, host values, or logs.

### Task 8: Close traceability, ADRs, and final evidence

**Files:**
- Create: `docs/architecture/decisions/ADR-004-hardware-inspection-trusted-tools-and-release-evidence.md`
- Create: `docs/testing/hardware-inspection/Hardware-Inspection-Block-2-Traceability.md`
- Create: `docs/testing/evidence/2026-08-24-hardware-inspection-gate-9-release-closure.md`
- Modify: `docs/architecture/decisions/README.md`
- Modify: `docs/testing/evidence/README.md`
- Modify: `docs/evidence/requirements/F-M07/README.md`
- Modify: `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation/README.md`
- Modify: `docs/reviews/2026-08-22-hardware-inspection-integration-preservation-matrix.md`

**Interfaces:**
- Consumes: validated Gate 9 summary/hash, exact reviewed head, Gates 1-8 evidence, and final regression/audit results.
- Produces: truthful supplemental Block 2 traceability and final disposition without editing generated RTM status.

- [ ] **Step 1: Write ADR-004**

Record fixed ProgramData/package roots, manifest/hash/PE verification, retained custody, port-free standard streams, isolated native probe, x64-only production, LLM Fit packaging concern, NPU deferral, engineering/public-trust split, alternatives rejected, consequences, and review triggers.

- [ ] **Step 2: Write the supplemental traceability map**

Map F-M07/AC-F-M07/HE-01/HE-02, F-M18, F-M19, N-M12, N-M13, and G-M04 to exact production files, automated test classes, gate evidence, target summary, and status. Do not mark a criterion Verified if the target summary did not prove it. Explain why the stale generated RTM remains untouched.

- [ ] **Step 3: Update requirement evidence and indexes**

Replace the F-M07 placeholder row with bounded evidence links and a result supported by the actual Gate 9 disposition. Add ADR/evidence index entries. Update Foundation and preservation docs with exact hashes, counts, warnings, external blockers, and non-claims.

- [ ] **Step 4: Run final fresh verification**

Require:

```text
Foundation: 201 or greater, zero failed/skipped
Probe: 22 or greater, zero failed/skipped
Hardware/runner Python: prior 63 plus every new Gate 9 contract, zero failed/skipped
Gate 8 guest evidence: immutable v13 87/87 x3
Gate 9 target evidence: exact three successful repetitions
Release/x64 app MSIX: zero errors
Debug/x64 test MSIX: zero errors
x86 graph: zero Hardware Infrastructure compile items and zero Foundation references
```

Run whitespace, conflict-marker, dependency, binary/artifact, private-path, raw-output, network/shell, model-coupling, absolute-path, package-inventory, and working-tree scans. Validate every evidence link.

- [ ] **Step 5: Record the only truthful final state**

Use `Passed` only with matching real public-trust and Smart App Control evidence. Otherwise record `EngineeringPassedReleaseBlocked`, state that Hardware Inspection implementation is engineering-complete, and list the trusted publisher/reputation item as the sole external release blocker. Do not call the public release ready.

- [ ] **Step 6: Commit final evidence**

Run `git diff --check`, verify no generated artifacts are staged, and commit:

```text
docs(hardware-inspection): record Gate 9 release disposition
```

### Task 9: Final branch verification and handoff

**Files:**
- Verify only; modify files only for a proved finding.

- [ ] **Step 1: Re-read the Gate 9 spec and this plan line by line**

Create a completion checklist mapping every requirement to a fresh command result or reviewed evidence path. Any missing item remains explicit.

- [ ] **Step 2: Use verification-before-completion**

Freshly run all applicable final commands, read their full exit codes/counts, and inspect `git status --short --branch`. Do not rely on earlier output or the target controller's success line alone.

- [ ] **Step 3: Finish the development branch**

Use `superpowers:finishing-a-development-branch` only after all repository-controlled work and evidence commits are complete. Present integration choices without pushing, opening a PR, merging, or deleting the worktree unless the user explicitly authorizes that external state change.
