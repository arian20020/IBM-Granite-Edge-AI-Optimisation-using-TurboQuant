# LLamaSharp VocabOnly Model Probe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the isolated LLamaSharp feasibility tool so it can probe one real local GGUF through the matched CPU runtime using `VocabOnly`, capture lightweight evidence, verify file preservation, and remain outside the WinUI application.

**Architecture:** Native smoke and model probing remain separate modes in one console tool. Model-specific source lives under `ModelProbe`, returns project-owned evidence, and lets LLamaSharp types appear only in the probe/collector implementation.

**Tech Stack:** .NET 8, C# 12, LLamaSharp 0.27.0, LLamaSharp.Backend.Cpu 0.27.0, MSTest 4.3.2, System.Text.Json, SHA-256.

## Global constraints

- Target branch: `feature/model-inspection`.
- Preserve the native-smoke command.
- Pin both LLamaSharp packages to `0.27.0`.
- Keep mapped llama.cpp commit `3f7c29d318e317b63f54c558bc69803963d7d88c`.
- Use `VocabOnly = true` and `GpuLayerCount = 0`.
- Disable CUDA and Vulkan selection.
- Do not reference LLamaSharp from the WinUI project.
- Do not create a context, KV cache or inference request.
- Do not add Vulkan, TurboQuant or OpenVINO.
- Open the model read-only.
- Reject an output path equal to the model path.
- Capture file identity before and after.
- Redact the full canonical model path from serialized failures and native logs.
- Do not classify a final application model outcome.
- Do not claim success without a real Windows x64 run.

---

### Task 1: Define deterministic command, integrity and progress contracts

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelProbeSafetyValidatorTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelFileSnapshotServiceTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/NativeLoadProgressRecorderTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs`

- [x] Test `--model` and mode-specific default output.
- [x] Test model, output and cancellation options in arbitrary order.
- [x] Test invalid, missing, duplicate and model-less cancellation arguments.
- [x] Test that evidence cannot overwrite the model.
- [x] Test SHA-256 snapshots and unchanged integrity.
- [x] Test detection of changed content/length/hash.
- [x] Test progress clamping, duplicate suppression and snapshot independence.
- [x] Test generic JSON writing, atomic overwrite and string enums.
- [ ] Run the focused tests and record actual output.

Test source was created before the corresponding implementation, but this
connected GitHub editing environment could not execute .NET. No retrospective
RED or GREEN run is claimed.

---

### Task 2: Share CPU-native and JSON infrastructure

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/CpuNativeRuntimeConfiguration.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/JsonEvidenceWriter.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/NativeBackendSmokeProbe.cs`
- Delete: `tools/ModelInspection.LlamaSharpSpike/SmokeEvidenceWriter.cs`

- [x] Extract thread-safe CPU-only native configuration.
- [x] Extract selected-backend description.
- [x] Replace the smoke-only writer with generic atomic JSON writing.
- [x] Configure enums as readable JSON strings.
- [x] Update native smoke to use shared helpers.
- [ ] Run existing native-smoke tests.

---

### Task 3: Implement model-file safety and progress helpers

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshot.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshotService.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelProbeSafetyValidator.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/NativeLoadProgressRecorder.cs`

- [x] Implement asynchronous read-only SHA-256 capture.
- [x] Verify length and last-write time remain stable while hashing.
- [x] Implement before/after integrity comparison.
- [x] Implement canonical-path SHA-256 rather than path serialization.
- [x] Reject output/model path collision.
- [x] Implement thread-safe genuine native-progress recording.
- [x] Add source-adjacent folder documentation.
- [ ] Run helper tests.

---

### Task 4: Implement project-owned VocabOnly evidence

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbeResult.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyEvidenceCollector.cs`

- [x] Add `Succeeded`, `Cancelled` and `Failed` operation states.
- [x] Add runtime/package identity and selected CPU backend evidence.
- [x] Add before/after file identity and integrity evidence.
- [x] Add metadata, architecture, context, size, parameters and structural fields.
- [x] Add vocabulary and known special-token evidence.
- [x] Add fixed non-sensitive `Hello` tokenizer smoke.
- [x] Add chat-template presence, length and SHA-256 without full template text.
- [x] Add progress, memory and disposal fields.
- [x] Keep LLamaSharp/native objects out of every result type.

---

### Task 5: Implement VocabOnly orchestration

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbe.cs`

- [x] Validate and snapshot the model before native loading.
- [x] Configure and dry-run the matched CPU backend.
- [x] Load asynchronously with `VocabOnly = true`, zero GPU layers, genuine progress and cancellation.
- [x] Collect evidence only while the native handle is valid.
- [x] Dispose native weights in success/failure paths where a handle exists.
- [x] Capture post-probe identity and compare integrity.
- [x] Convert integrity change or unverifiable successful probe to controlled failure.
- [x] Map operational, native, load and cancellation failures to stable codes.
- [x] Record memory observations without treating them as final Hardware Fit estimates.
- [x] Redact the full canonical model path from serialized failures and logs.
- [ ] Run one controlled real Granite integration probe.

---

### Task 6: Extend the command-line application

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike/SpikeOptionsParser.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/Program.cs`

- [x] Preserve no-model native-smoke mode.
- [x] Add `--model`, `--output` and `--cancel-after-ms` in arbitrary order.
- [x] Choose the correct default output by mode.
- [x] Reject unsafe output/model equality before probing.
- [x] Connect `Ctrl+C` and optional timed cancellation.
- [x] Write evidence for success, failure and cancellation.
- [x] Use exit codes `0`, `1`, `2` and `3`.
- [ ] Run parser/writer/helper tests.
- [ ] Build Release `win-x64`.

---

### Task 7: Reconcile README hierarchy

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`

- [x] Document both native-smoke and VocabOnly modes.
- [x] Document the new CLI, exit codes and evidence schema.
- [x] Document CPU-only, VocabOnly and no-context boundaries.
- [x] Add the `ModelProbe` folder to the README hierarchy.
- [x] State that source exists but real Granite verification remains pending.
- [x] State that the WinUI page remains unconnected.
- [x] Preserve Vulkan and TurboQuant as later gates.
- [x] Record exact local verification commands.

---

### Task 8: Final source verification

Pre-slice commit:

```text
f640c306bda3e44b85c3ce4412a55707c5e569f1
```

- [x] Compare the branch from the pre-slice commit.
- [x] Confirm no WinUI application project file was changed.
- [x] Confirm no Vulkan or TurboQuant dependency was added.
- [x] Confirm the selected model is opened only with `FileAccess.Read`.
- [x] Confirm output/model collision protection exists.
- [x] Confirm serialized results omit the full canonical model path.
- [x] Record that test/build/runtime evidence is still pending rather than claiming success.

## Source verification evidence

The branch diff from `f640c306...` contains only:

```text
LLamaSharp feasibility source under tools/
deterministic feasibility test source
ModelProbe and parent READMEs
design and implementation-plan Markdown
```

It does not modify:

```text
IBM Granite with TurboQuant (Intel).csproj
application XAML or C# source
Vulkan dependencies
TurboQuant dependencies
OpenVINO dependencies
```

Model access is implemented through a `FileStream` created with:

```text
FileMode.Open
FileAccess.Read
FileShare.Read
```

The evidence output is rejected when its canonical path equals the model path.
Before/after length, last-write time and SHA-256 are compared. Canonical model
paths are represented by SHA-256 fingerprints and redacted from errors/logs.

## Verification commands

Run from Developer PowerShell at the repository root:

```powershell
$SpikeProject =
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$SpikeTests =
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj"

# Restore exact packages.
dotnet restore $SpikeTests --runtime win-x64

# Run deterministic unit tests.
dotnet test $SpikeTests `
    --configuration Release `
    --no-restore `
    --runtime win-x64

# Build the Windows x64 tool.
dotnet build $SpikeProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64

# Preserve and verify native-library-only smoke mode.
dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --output "artifacts\model-inspection\llamasharp\runtime-smoke.json"

# Probe a controlled, provenance-recorded Granite GGUF.
dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --model "C:\Models\<controlled-granite-model>.gguf" `
    --output "artifacts\model-inspection\llamasharp\vocab-only-model-probe.json"
```

Expected exit codes:

```text
0 = requested smoke/probe succeeded and evidence was written
1 = controlled failure and evidence was written where possible
2 = invalid or unsafe arguments
3 = controlled cancellation and evidence was written
```

## Current execution state

- Design, source, deterministic test contracts and README reconciliation are on `feature/model-inspection`.
- No test pass, Release build, native-smoke pass or real-model pass is claimed yet.
- The next action is fresh Windows verification followed by review of one controlled Granite evidence file.
- WinUI integration, Vulkan, TurboQuant and final outcome classification remain outside this slice.
