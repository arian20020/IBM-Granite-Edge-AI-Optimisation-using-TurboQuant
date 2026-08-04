# LLamaSharp hosted native integration tests

**Status:** Model-free child-process integration source  
**CI tier:** Tier 1 — every relevant push and pull request  
**Shared support:** [Probe process test support](../ModelInspection.LlamaSharpSpike.TestSupport/README.md)

## Purpose

This Microsoft.Testing.Platform project verifies the published LLamaSharp CPU
backend without requiring an external model.

Every native call runs in the published feasibility executable as a child
process. The MSTest host never loads LLamaSharp native libraries directly.

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
- CUDA and Vulkan must remain disabled.
- Destructive DLL tests operate only on a disposable publish copy.
- Native aborts are child-process evidence, not MSTest-host failures.
