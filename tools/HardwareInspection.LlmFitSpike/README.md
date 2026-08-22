# Hardware Inspection LLM Fit Gate 1 spike

This isolated Windows x64 engineering tool evaluates one integrity-pinned LLM
Fit candidate as a possible read-only CPU/RAM fact source. It is not the
Hardware Inspection feature, is not referenced by WinUI, and does not approve
the candidate for production bundling or redistribution.

## Pinned candidate and boundary

Gate 1 pins LLM Fit `v1.1.9`, upstream commit
`a02e13f1013ed69889ff44426a651bf7c68c292e`, the committed archive and
executable SHA-256 values, and AMD64 PE identity. This version is fixed so a
release change cannot silently alter the observed schema or behavior. A new
version requires a new manifest and a new Gate 1 decision.

Only these candidate argument arrays are permitted:

```text
--version
--no-dashboard --json system
```

`--no-dashboard --json system` is the sole system command because Gate 1 needs
read-only structured facts without activating a UI or service. `serve`, the
dashboard, REST endpoints, model recommendation, shell execution, and arbitrary
arguments are prohibited. The runner uses no shell, bounds both output streams,
enforces timeout/cancellation, observes candidate-owned sockets, and terminates
the owned process tree.

## Exact commands

Acquire and verify the pinned package:

```powershell
& "scripts\hardware-inspection\Acquire-HardwareInspectionLlmFitCandidate.ps1" `
    -RepositoryRoot (Resolve-Path ".").Path
if ($LASTEXITCODE -ne 0) { throw "Pinned candidate acquisition failed: $LASTEXITCODE" }
$env:GRANITE_LLMFIT_CANDIDATE_ROOT =
    (Resolve-Path "third-party\bin\llmfit\v1.1.9\win-x64").Path
```

Run deterministic contracts (the harmless fixture is published automatically
by the test support when no explicit fixture root is configured):

```powershell
dotnet test `
    --project "tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --filter "TestCategory=Deterministic" `
    --minimum-expected-tests 174 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-deterministic.trx" `
    --no-ansi
if ($LASTEXITCODE -ne 0) { throw "Deterministic contracts failed: $LASTEXITCODE" }
```

Run the trusted Windows Intel route only after following the capture procedure
in the [Gate 1 runbook](../../docs/testing/runbooks/Hardware-Inspection-LLM-Fit-Gate-1-Runbook.md):

```powershell
$env:GRANITE_LLMFIT_TRUSTED_OUTPUT =
    [System.IO.Path]::GetFullPath(
        (Join-Path `
            "artifacts\hardware-inspection\llmfit" `
            ("trusted-windows-intel-" + (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))))
$env:GRANITE_LLMFIT_GATE1_OUTPUT = $env:GRANITE_LLMFIT_TRUSTED_OUTPUT
& "scripts\hardware-inspection\Capture-HardwareInspectionWindowsReference.ps1" `
    -CandidateRoot $env:GRANITE_LLMFIT_CANDIDATE_ROOT `
    -OutputRoot $env:GRANITE_LLMFIT_GATE1_OUTPUT
if ($LASTEXITCODE -ne 0) { throw "Trusted capture failed: $LASTEXITCODE" }

$env:GRANITE_LLMFIT_WINDOWS_REFERENCE =
    Join-Path $env:GRANITE_LLMFIT_GATE1_OUTPUT "windows-reference.json"
dotnet test `
    --project "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj" `
    --configuration Release --runtime win-x64 --no-restore --no-build `
    --filter "TestCategory=TrustedWindowsIntel" `
    --minimum-expected-tests 3 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx --report-trx-filename "hardware-inspection-llmfit-trusted.trx" `
    --no-ansi
if ($LASTEXITCODE -ne 0) { throw "Trusted Windows Intel tests failed: $LASTEXITCODE" }
```

Run the separately controlled offline route only after an operator has disabled
all non-loopback access; neither the test nor a script changes the network:

```powershell
$env:GRANITE_LLMFIT_OFFLINE_OUTPUT =
    [System.IO.Path]::GetFullPath(
        (Join-Path `
            "artifacts\hardware-inspection\llmfit" `
            ("trusted-offline-" + (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))))
$env:GRANITE_LLMFIT_GATE1_OUTPUT = $env:GRANITE_LLMFIT_OFFLINE_OUTPUT
dotnet test `
    --project "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj" `
    --configuration Release --runtime win-x64 --no-restore --no-build `
    --filter "TestCategory=TrustedOffline" `
    --minimum-expected-tests 1 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx --report-trx-filename "hardware-inspection-llmfit-offline.trx" `
    --no-ansi
if ($LASTEXITCODE -ne 0) { throw "Controlled offline test failed: $LASTEXITCODE" }
```

After the operational artifacts exist, generate the retained report from the
clean evaluated-source commit:

```powershell
& "scripts\hardware-inspection\Write-HardwareInspectionLlmFitGate1Report.ps1" `
    -EvidenceJson (Join-Path $env:GRANITE_LLMFIT_TRUSTED_OUTPUT "llmfit-gate1.evidence.json") `
    -WindowsReferenceJson (Join-Path $env:GRANITE_LLMFIT_TRUSTED_OUTPUT "windows-reference.json") `
    -OfflineEvidenceJson (Join-Path $env:GRANITE_LLMFIT_OFFLINE_OUTPUT "llmfit-gate1.evidence.json") `
    -DeterministicTrx "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-deterministic.trx" `
    -TrustedWindowsTrx "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-trusted.trx" `
    -OfflineTrx "TestResults\HardwareInspectionLlmFit\hardware-inspection-llmfit-offline.trx" `
    -OutputMarkdown "docs\testing\evidence\2026-08-15-hardware-inspection-gate1-llmfit-verification.md" `
    -RepositoryCommit (git rev-parse HEAD)
```

All six inputs are mandatory for a complete candidate decision. A strict
prerequisite-`Blocked` route may omit genuinely unavailable later artifacts,
but only when the exact trusted/offline TRX outcomes and minimal allowlisted
envelope positively prove that prerequisite; there is no free-form Blocked
flag. An early `Rejected` route likewise needs an actual failing artifact or
TRX. Missing files by themselves stop report generation.

The generator recognizes the four disposition strings in the evidence schema,
but the current six inputs contain no independent controlled security/legal
disposition. Consequently an `AcceptedForFunctionalEvaluation` claim is
recorded as inconsistent and forces `Rejected`; removing the pending dependency
diagnostic is not evidence of an approval. The strongest currently reachable
passing result is `FunctionalPassWithPackagingConcern`.

## Evidence, privacy, and open packaging issues

Raw candidate JSON, Windows hardware names, candidate/output paths, stdout,
stderr, host/user identity, serials, device IDs, and network identifiers stay in
ignored local artifacts. The report generator retains only fixed candidate
identity when actually evaluated, exact test counts, derived comparison
booleans/counts/deltas, allowlisted diagnostics, and hashes. Raw TRX remains
local because TRX can contain machine identity and absolute paths.

The package declares MIT and its `LICENSE` must be retained. Two production
redistribution blockers remain explicit: v1.1.9 was observed unsigned despite
the upstream signing claim, and the transitive dependency-license/notice
inventory is pending. Source documentation also drifts from the observed JSON
schema. On Windows Intel, the candidate does not establish dedicated versus
shared GPU memory and cannot establish Intel NPU presence or absence.

Gate 1 proves only the candidate route and CPU/RAM behavior. It does not verify
`F-M07`, `HE-01`, or `HE-02`. Gate 2 owns reusable external-tool manifest,
integrity and process-execution foundations; it must preserve the fixed-command,
no-shell, timeout, cancellation, process-tree, bounded-stream, privacy and
no-dashboard contracts rather than copying this spike CLI into production.
