# Hardware Inspection LLM Fit deterministic tests

This Microsoft Testing Platform project verifies the Gate 1 spike without
downloading or running the real LLM Fit candidate. Process tests execute only
the repository's harmless fake tool fixture. The suite is suitable for hosted
Windows CI and is separate from the manually gated Windows Intel and controlled
offline integration routes.

Run the full deterministic category:

```powershell
dotnet restore `
    "tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj" `
    --runtime win-x64
if ($LASTEXITCODE -ne 0) { throw "Deterministic restore failed: $LASTEXITCODE" }

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

The suite covers the pinned manifest and fixed commands, archive/executable
integrity and PE checks, strict CPU/RAM JSON assessment, bounded process
execution and tree cleanup, socket/dashboard observation, evidence allowlists,
atomic writes, and Gate 1 orchestration. `--minimum-expected-tests` protects
discovery only; CI additionally parses its ephemeral TRX and requires every
result to pass before publishing a sanitized count summary. Raw TRX is never an
uploaded artifact.

These tests make no trusted hardware, offline-network, production approval,
WinUI, GPU-memory, NPU, or model-compatibility claim.
