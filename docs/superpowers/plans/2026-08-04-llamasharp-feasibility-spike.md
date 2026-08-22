# LLamaSharp Feasibility Spike Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Status:** Slice 1 source implemented; Windows verification pending. Slice 2 source is tracked separately in [the VocabOnly model-probe plan](2026-08-04-llamasharp-vocab-only-model-probe.md).

**Goal:** Establish the exact matched LLamaSharp CPU native-library boundary without changing the WinUI application.

**Architecture:** A standalone .NET console project references `LLamaSharp` and `LLamaSharp.Backend.Cpu` 0.27.0, configures CPU-only selection, calls `NativeLibraryConfig.LLama.DryRun`, and writes project-owned JSON evidence.

## Runtime identity

```text
LLamaSharp                  0.27.0
LLamaSharp.Backend.Cpu      0.27.0
Mapped llama.cpp commit     3f7c29d318e317b63f54c558bc69803963d7d88c
Initial application RID     win-x64
```

The standalone research runtime remains separate:

```text
llama.cpp b9870
2d973636e292ee6f75fadcf08d29cb33511f509f
```

## Constraints

- Keep LLamaSharp outside the WinUI project until feasibility evidence passes.
- Disable CUDA and Vulkan for this first gate.
- Do not auto-download native libraries.
- Use operational failure codes rather than model outcomes.
- Write local evidence under ignored `artifacts/`.
- Do not claim a pass without fresh Windows command output.

## Slice 1 implementation

### Architecture and dependency records

- [x] Add ADR-001.
- [x] Record the managed/backend/native revision pair.
- [x] Separate research and application runtime claims.
- [x] Add LLamaSharp and the CPU backend to the licence register.

### Isolated tool

- [x] Create `tools/ModelInspection.LlamaSharpSpike`.
- [x] Pin exact package versions.
- [x] Add `PinnedApplicationRuntime`.
- [x] Add command parsing for native smoke.
- [x] Add project-owned native-smoke result records.
- [x] Add CPU-only `NativeLibraryConfig.LLama.DryRun`.
- [x] Add atomic JSON evidence writing.
- [x] Use controlled exit codes.
- [x] Add source-adjacent README documentation.

### Deterministic tests

- [x] Protect exact runtime pins.
- [x] Protect research/application separation.
- [x] Test smoke command parsing.
- [x] Test parseable JSON and atomic overwrite.
- [ ] Execute tests and capture actual results.

### Windows integration

- [x] Add a read-only Windows workflow for restore, tests, Release build, native smoke and artifact upload.
- [ ] Capture a completed workflow or target-laptop run.
- [ ] Confirm exit code `0`.
- [ ] Confirm CPU backend identity, CUDA `false`, Vulkan `false`.
- [ ] Review runtime logs and JSON evidence.
- [ ] Preserve command, package and output evidence.

## Slice 2 handoff

The next feasibility depth is now designed and implemented in source under:

```text
tools/ModelInspection.LlamaSharpSpike/ModelProbe/
```

It adds:

```text
controlled local GGUF
VocabOnly = true
GpuLayerCount = 0
read-only pre/post SHA-256
metadata/vocabulary/tokenizer/chat-template evidence
genuine progress
cancellation
disposal
integrity comparison
```

Its implementation and remaining verification are tracked in:

- [LLamaSharp VocabOnly model-probe plan](2026-08-04-llamasharp-vocab-only-model-probe.md)
- [LLamaSharp VocabOnly model-probe design](../specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)

## Verification commands

```powershell
$SpikeProject =
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$SpikeTests =
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj"

dotnet restore $SpikeTests --runtime win-x64

dotnet test $SpikeTests `
    --configuration Release `
    --no-restore `
    --runtime win-x64

dotnet build $SpikeProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64

dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --output "artifacts\model-inspection\llamasharp\runtime-smoke.json"
```

Expected exit codes:

```text
0 = native CPU backend smoke succeeded and evidence was written
1 = controlled runtime or evidence-writing failure
2 = invalid arguments
```

## Current execution state

- Slice 1 decision, source, tests, workflow and documentation are committed.
- Slice 2 model-probe source and documentation are committed separately.
- No deterministic test pass, Release build pass, native-smoke pass or real-model pass is claimed yet.
- The WinUI project still has no LLamaSharp, Vulkan or TurboQuant package reference.
