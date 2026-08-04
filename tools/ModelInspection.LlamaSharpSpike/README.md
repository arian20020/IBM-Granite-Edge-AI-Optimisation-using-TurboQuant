# Model Inspection LLamaSharp feasibility spike

**Status:** Slice 1 implementation — target-machine verification pending  
**Decision:** [ADR-001](../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)  
**Design:** [LLamaSharp feasibility spike](../../docs/superpowers/specs/2026-08-04-llamasharp-feasibility-spike-design.md)

## Purpose

This console project proves the selected managed/native application pair before
LLamaSharp is added to the WinUI application.

```text
LLamaSharp 0.27.0
        ↓
LLamaSharp.Backend.Cpu 0.27.0
        ↓
llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The first slice performs only a native backend dry run. It does not load or
inspect a model.

## Why this is separate from the WinUI application

Native runtime selection and packaging are high-risk integration boundaries.
Keeping the first experiment in a console project means:

- the WinUI package remains unchanged;
- native loading failures are easier to reproduce;
- logs and exit codes are not hidden by page lifecycle behavior;
- the project can be built and run independently;
- no feasibility-only type becomes a production UI contract.

## Current flow

```text
Program
    → parses --output or --help
    → runs NativeBackendSmokeProbe
    → configures LLamaSharp for CPU only
    → calls NativeLibraryConfig.LLama.DryRun(...)
    → converts the result to NativeBackendSmokeResult
    → writes JSON atomically with SmokeEvidenceWriter
    → returns a controlled exit code
```

## Exact dependency identity

| Item | Value |
|---|---|
| Managed package | `LLamaSharp` `0.27.0` |
| CPU backend package | `LLamaSharp.Backend.Cpu` `0.27.0` |
| LLamaSharp tag | `v0.27.0` |
| LLamaSharp release commit | `7cbbc45e421d55794d5050d126e0b96511007007` |
| Mapped llama.cpp commit | `3f7c29d318e317b63f54c558bc69803963d7d88c` |
| Intended first application RID | `win-x64` |

The separate upstream research runtime remains:

```text
b9870 / 2d973636e292ee6f75fadcf08d29cb33511f509f
```

It is not the runtime loaded by this tool.

## Commands

Run from the repository root in Developer PowerShell:

```powershell
dotnet restore `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj"

dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --configuration Release

dotnet build `
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --runtime win-x64

dotnet run `
    --project "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    -- `
    --output "artifacts\model-inspection\llamasharp\runtime-smoke.json"
```

Show help:

```powershell
dotnet run `
    --project "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    -- `
    --help
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Native CPU backend dry run succeeded and JSON was written |
| `1` | Controlled runtime or evidence-writing failure |
| `2` | Invalid command-line arguments |

## Evidence

Default path:

```text
artifacts/model-inspection/llamasharp/runtime-smoke.json
```

The local `artifacts/` folder is ignored by Git. Do not move the result into
formal evidence until the command, environment, packages and output have been
reviewed.

The JSON records:

- exact managed and backend package pins;
- expected native llama.cpp commit;
- actual process architecture, OS and .NET runtime;
- selected native library type and metadata where available;
- LLamaSharp native-loader logs;
- controlled operational failure details.

## Safety boundary

- CUDA selection is disabled.
- Vulkan selection is disabled.
- Runtime auto-download is not used.
- No model path is accepted in Slice 1.
- No model file is opened or modified.
- Failures are operational failures, not model outcomes.
- The WinUI application project has no LLamaSharp reference from this slice.

## Next slice

After a successful Windows x64 dry run, the next slice will add a controlled
real Granite GGUF request, pre/post integrity evidence and a `VocabOnly`
feasibility probe. It will not begin until the native backend smoke result is
reviewed.

## Tests

The adjacent test project verifies:

- exact runtime pins;
- research/application separation;
- command-line parsing;
- JSON writing and overwrite behavior.

The actual `DryRun` is an integration check and must execute on the target
machine rather than being disguised as a deterministic unit test.
