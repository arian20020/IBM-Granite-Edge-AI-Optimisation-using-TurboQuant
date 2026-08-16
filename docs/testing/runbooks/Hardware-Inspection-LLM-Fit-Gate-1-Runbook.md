# Hardware Inspection LLM Fit Gate 1 runbook

## Purpose

Use this runbook to produce the manual Gate 1 evidence for the single pinned
LLM Fit v1.1.9 Windows x64 candidate. The trusted Windows Intel capture and the
controlled offline test are distinct operations. Do not substitute evidence
from the authoring machine or another candidate.

Gate 1 evaluates only the candidate route, CPU/RAM JSON, bounded execution and
network/process observations. It does not approve production integration or
redistribution.

## Safety boundaries

- Run only on Windows x64 with an Intel processor and at least one Intel
  graphics adapter for the trusted capture.
- Use only the committed acquisition script and manifest. Never replace the
  project, executable, command or hashes with operator input.
- The only candidate commands are `--version` and
  `--no-dashboard --json system`.
- Do not run `serve`, the dashboard, REST endpoints, model recommendation or
  arbitrary arguments.
- Use a fresh direct child beneath
  `artifacts/hardware-inspection/llmfit` for every capture or test attempt.
- Do not pre-create an output directory. The Gate 1 CLI owns its fresh output.
- The scripts and tests do not disable or restore network interfaces. The
  operator owns that controlled machine step.
- Keep candidate binaries and all generated captures outside Git. The existing
  ignore rules cover `third-party/bin`, `artifacts` and `TestResults`.

## 1. Acquire the exact candidate

From the repository root in Windows PowerShell 5.1 or PowerShell 7:

```powershell
& "scripts\hardware-inspection\Acquire-HardwareInspectionLlmFitCandidate.ps1" `
    -RepositoryRoot (Resolve-Path ".").Path
if ($LASTEXITCODE -ne 0) {
    throw "Pinned LLM Fit acquisition failed with exit code $LASTEXITCODE."
}
```

The acquisition must verify the archive length and SHA-256, executable
SHA-256, AMD64 PE machine, exact flat package, and the freshly recorded
Authenticode observation without executing `llmfit.exe`.

Set the candidate variable only after acquisition succeeds:

```powershell
$env:GRANITE_LLMFIT_CANDIDATE_ROOT =
    (Resolve-Path "third-party\bin\llmfit\v1.1.9\win-x64").Path
```

The candidate-root value is machine-local operator input. Never paste it into
committed or uploaded evidence.

## 2. Restore and build once

```powershell
$TrustedProject =
    "tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj"

dotnet restore $TrustedProject --runtime win-x64
if ($LASTEXITCODE -ne 0) { throw "Trusted restore failed." }

dotnet build `
    $TrustedProject `
    --configuration Release `
    --runtime win-x64 `
    --no-restore
if ($LASTEXITCODE -ne 0) { throw "Trusted build failed." }
```

The capture and offline gate deliberately use `--no-restore --no-build`; do
not continue if this fixed build failed.

## 3. Capture the trusted Windows Intel reference

Create a unique path value without creating the directory:

```powershell
$env:GRANITE_LLMFIT_TRUSTED_OUTPUT =
    [System.IO.Path]::GetFullPath(
        (Join-Path `
            "artifacts\hardware-inspection\llmfit" `
            ("trusted-windows-intel-" +
                (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))))
$env:GRANITE_LLMFIT_GATE1_OUTPUT =
    $env:GRANITE_LLMFIT_TRUSTED_OUTPUT

& "scripts\hardware-inspection\Capture-HardwareInspectionWindowsReference.ps1" `
    -CandidateRoot $env:GRANITE_LLMFIT_CANDIDATE_ROOT `
    -OutputRoot $env:GRANITE_LLMFIT_GATE1_OUTPUT
if ($LASTEXITCODE -ne 0) {
    throw "Trusted Windows Intel capture failed with exit code $LASTEXITCODE."
}

$env:GRANITE_LLMFIT_WINDOWS_REFERENCE =
    Join-Path $env:GRANITE_LLMFIT_GATE1_OUTPUT "windows-reference.json"
```

The script resolves the repository from its own location and accepts no
project or command override. It validates both input paths and physical
disjointness before output creation. It reads only:

```text
Win32_Processor.Name
Win32_Processor.NumberOfLogicalProcessors
Win32_ComputerSystem.TotalPhysicalMemory
Win32_OperatingSystem.FreePhysicalMemory
Win32_VideoController.Name
```

It rejects a non-Windows-x64 or non-Intel CPU/GPU host with
`HI-GATE1-WRONG-TARGET` before candidate execution and without creating the
output. On the approved target it brackets the fixed CLI run with Windows RAM
readings, verifies the raw JSON hash against production evidence, derives the
CPU/RAM/GPU comparisons, hashes the Authenticode observation, and atomically
publishes `windows-reference.json`.

The reference contains no candidate/output/repository path, host/user name,
serial, UUID, BIOS identifier, PNP device ID, MAC/IP address, command output or
raw-derived LLM Fit CPU/GPU name.

## 4. Run the three trusted artifact tests

```powershell
dotnet test `
    --project $TrustedProject `
    --configuration Release `
    --no-restore `
    --no-build `
    --runtime win-x64 `
    --filter "TestCategory=TrustedWindowsIntel" `
    --minimum-expected-tests 3 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-trusted.trx" `
    --no-ansi
if ($LASTEXITCODE -ne 0) { throw "Trusted artifact validation failed." }
```

Required result: exactly 3 passed, 0 failed and 0 skipped. Read both the
console summary and TRX. `--minimum-expected-tests 3` does not prove that a
discovered test was not skipped.

Do not call the capture script again for these tests. They validate the exact
three-file retained output without rerunning the candidate.

## 5. Prepare the controlled offline run

Use a controlled target with no external network route. Through the approved
Windows/IT process, disable every non-loopback network interface before
starting the test. Confirm both conditions:

```powershell
[System.Net.NetworkInformation.NetworkInterface]::GetIsNetworkAvailable()

[System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
    Where-Object {
        $_.NetworkInterfaceType -ne
            [System.Net.NetworkInformation.NetworkInterfaceType]::Loopback -and
        $_.OperationalStatus -eq
            [System.Net.NetworkInformation.OperationalStatus]::Up
    }
```

The first command must be `False`; the second must return no interface.

Create another unique path value without creating it:

```powershell
$env:GRANITE_LLMFIT_OFFLINE_OUTPUT =
    [System.IO.Path]::GetFullPath(
        (Join-Path `
            "artifacts\hardware-inspection\llmfit" `
            ("trusted-offline-" +
                (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))))
$env:GRANITE_LLMFIT_GATE1_OUTPUT =
    $env:GRANITE_LLMFIT_OFFLINE_OUTPUT
```

## 6. Run the one offline test

```powershell
dotnet test `
    --project $TrustedProject `
    --configuration Release `
    --no-restore `
    --no-build `
    --runtime win-x64 `
    --filter "TestCategory=TrustedOffline" `
    --minimum-expected-tests 1 `
    --results-directory "TestResults\HardwareInspectionLlmFit" `
    --report-trx `
    --report-trx-filename "hardware-inspection-llmfit-offline.trx" `
    --no-ansi
$offlineExitCode = $LASTEXITCODE
```

Immediately restore the interfaces manually through the same approved
Windows/IT process, even if the test failed.

Required success result: exactly 1 passed, 0 failed and 0 skipped, with
`$offlineExitCode` equal to 0. Inspect the console and TRX.

If either network precondition was false, the expected result is a deliberate
test failure. Before touching the candidate, the test atomically writes this
minimal record to the required output path:

```text
<GRANITE_LLMFIT_OFFLINE_OUTPUT>\llmfit-gate1.evidence.json
```

Its disposition is `Blocked` and its only diagnostic is
`HI-GATE1-OFFLINE-PRECONDITION-FAILED`. It contains no fabricated candidate,
signature or hardware facts. Gate 1 remains open. After establishing the
offline environment, choose a new unique output path and rerun; never overwrite
the blocked attempt.

## 7. Inspect and retain the ignored evidence

Trusted output must contain exactly:

```text
llmfit-gate1.evidence.json
llmfit-system.raw.json
windows-reference.json
```

A successful offline output must contain exactly:

```text
llmfit-gate1.evidence.json
llmfit-system.raw.json
```

Confirm the trusted and offline TRX files each report zero skipped. Confirm no
`llmfit` process and no candidate-owned TCP listener or established connection
remains. Retain these ignored paths and the four environment-variable values
for the Task 9 report generator; do not commit the captures.

## Failure rules

- Missing category variables cause a precise skipped/inconclusive result.
- Once all variables are present, wrong/missing evidence is a failure, never a
  skip.
- `HI-GATE1-WRONG-TARGET` means no target evidence was produced.
- `HI-GATE1-OFFLINE-PRECONDITION-FAILED` means the candidate was not judged.
- A GPU identity `FieldLevelGap` does not invalidate passing CPU/RAM evidence.
- `gpu_vram_gb` is never accepted as dedicated or shared Intel Windows memory.
- Intel NPU detection remains `DetectionUnavailable`, never “absent.”
- Any socket, port 8787, residual process, integrity mismatch, version mismatch,
  invalid schema, privacy failure or CPU/RAM comparison failure rejects the
  candidate route.

Do not proceed to Gate 2 until Task 9 applies the documented disposition rule
to complete trusted and offline evidence.
