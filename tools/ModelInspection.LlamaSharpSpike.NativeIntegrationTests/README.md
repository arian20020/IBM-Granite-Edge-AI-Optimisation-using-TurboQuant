# LLamaSharp hosted native integration tests

**Status:** Verified — `4/4` contained native tests passed on hosted Windows x64  
**Verification date:** 2026-08-04  
**Successful workflow run:** `30939159409`  
**CI tier:** Tier 1 — every relevant push and pull request  
**Shared support:** [Probe process test support](../ModelInspection.LlamaSharpSpike.TestSupport/README.md)  
**Evidence:** [Tier 1 runtime verification](../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)

## Purpose

This Microsoft.Testing.Platform project verifies the published LLamaSharp CPU
backend without requiring an external model.

Every native call runs in the published feasibility executable as a child
process. The MSTest host never loads LLamaSharp native libraries directly.

## Verified result

```text
Configuration:        Release / win-x64
Category:             NativeIntegration
Total:                4
Succeeded:            4
Failed:               0
Skipped:              0
Result:               PASS
```

The same workflow also built and published the feasibility executable, produced
valid direct CPU smoke evidence and passed the no-GGUF artifact gate.

## Scenarios

```text
Published CPU backend present
    → child exits 0
    → runtime-smoke JSON is valid

Native backend DLLs removed from sandbox
    → parent survives
    → child exits with controlled runtime-unavailable evidence

Native backend DLL replaced by invalid bytes
    → parent survives even if native startup terminates
    → nonzero child result and retained output/evidence

Runtime evidence contract
    → exact package/native identity
    → x64 CPU-only selection
    → no model-path property
```

## Environment

The suite requires:

```text
LLAMASHARP_SPIKE_PUBLISH_DIR
```

The directory must be a `Release / win-x64` publish containing:

```text
GraniteEdgeAI.ModelInspection.LlamaSharpSpike.exe
```

The test assembly has the deliberately shorter physical name:

```text
GraniteEdgeAI.LlamaSharp.NativeTests.dll
```

The namespace remains descriptive. Only the assembly name was shortened after
a hosted Windows runner proved that the original generated executable path
exceeded the process-start path supported by that environment.

## Local command

```powershell
$PublishDirectory = Join-Path `
    $env:TEMP `
    "GraniteEdgeAI-LlamaSharp-Publish"

Remove-Item `
    -LiteralPath $PublishDirectory `
    -Recurse `
    -Force `
    -ErrorAction SilentlyContinue

dotnet publish `
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $PublishDirectory

$env:LLAMASHARP_SPIKE_PUBLISH_DIR = $PublishDirectory

dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.NativeIntegrationTests\ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --filter "TestCategory=NativeIntegration" `
    --minimum-expected-tests 4
```

## Boundaries

- No `.gguf` is required or created.
- No model is opened.
- No WinUI project is loaded.
- CUDA and Vulkan remain disabled.
- Destructive DLL tests operate only on a disposable publish copy.
- Native aborts are child-process evidence, not MSTest-host failures.
- This result does not prove real-model loading, cancellation or malformed-GGUF
  handling; those belong to the trusted real-model tier.
