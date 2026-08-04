# LLamaSharp VocabOnly Model Probe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the isolated LLamaSharp feasibility tool so it can probe one real local GGUF through the matched CPU runtime using `VocabOnly`, capture lightweight evidence, verify file preservation, and remain outside the WinUI application.

**Architecture:** Keep native smoke and model probing as separate modes in one console tool. The model-specific code lives in a `ModelProbe` folder, uses project-owned evidence records, and interacts with LLamaSharp only inside `VocabOnlyModelProbe` and `VocabOnlyEvidenceCollector`.

**Tech Stack:** .NET 8, C# 12, LLamaSharp 0.27.0, LLamaSharp.Backend.Cpu 0.27.0, MSTest 4.3.2, System.Text.Json, SHA-256.

## Global Constraints

- Target branch: `feature/model-inspection`.
- Preserve the native-smoke command.
- Pin `LLamaSharp` and `LLamaSharp.Backend.Cpu` to `0.27.0`.
- Keep mapped `llama.cpp` commit `3f7c29d318e317b63f54c558bc69803963d7d88c`.
- Use `ModelParams.VocabOnly = true`.
- Use `ModelParams.GpuLayerCount = 0`.
- Disable CUDA and Vulkan selection.
- Do not add LLamaSharp to the WinUI project.
- Do not create a context, KV cache or inference request.
- Do not add Vulkan, TurboQuant or OpenVINO.
- Open the model read-only.
- Reject an output path equal to the model path.
- Capture file identity before and after.
- Do not classify a final model outcome.
- Add a README for the new `ModelProbe` folder.
- Do not claim runtime success without a real Windows x64 model run.

---

### Task 1: Define command, integrity and progress contracts with tests

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelProbeSafetyValidatorTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelFileSnapshotServiceTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/NativeLoadProgressRecorderTests.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs`

**Interfaces:**
- Consumes future `SpikeOptionsParser`, `ModelProbeSafetyValidator`, `ModelFileSnapshotService`, `ModelFileIntegrityComparison`, `NativeLoadProgressRecorder`, and `JsonEvidenceWriter`.
- Produces executable contracts for the new command and safety boundaries.

- [ ] **Step 1: Test `--model` defaulting to the VocabOnly evidence path.**
- [ ] **Step 2: Test model, output and cancellation options in arbitrary order.**
- [ ] **Step 3: Test invalid, missing, duplicate and model-less cancellation arguments.**
- [ ] **Step 4: Test that evidence cannot overwrite the model.**
- [ ] **Step 5: Test SHA-256 snapshots and unchanged integrity.**
- [ ] **Step 6: Test detection of changed content, length or timestamp.**
- [ ] **Step 7: Test progress clamping and duplicate suppression.**
- [ ] **Step 8: Update evidence-writer tests for generic JSON and string enums.**
- [ ] **Step 9: Run the focused tests and record the expected RED failures.**
- [ ] **Step 10: Commit test contracts.**

---

### Task 2: Refactor shared CPU and JSON infrastructure

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/CpuNativeRuntimeConfiguration.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/JsonEvidenceWriter.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/NativeBackendSmokeProbe.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/Program.cs`
- Delete: `tools/ModelInspection.LlamaSharpSpike/SmokeEvidenceWriter.cs`

**Interfaces:**
- Produces `CpuNativeRuntimeConfiguration.Configure(...)`, `CpuNativeRuntimeConfiguration.Describe(...)`, and `JsonEvidenceWriter.WriteAsync<T>(...)`.
- Existing native smoke must retain the same output and exit behavior.

- [ ] **Step 1: Extract thread-safe CPU-only native configuration.**
- [ ] **Step 2: Extract selected-backend description.**
- [ ] **Step 3: Replace the smoke-only writer with generic atomic JSON writing.**
- [ ] **Step 4: Configure enum values as strings.**
- [ ] **Step 5: Update native smoke to use the shared helpers.**
- [ ] **Step 6: Run existing smoke unit tests.**
- [ ] **Step 7: Commit the infrastructure refactor.**

---

### Task 3: Implement model-file safety and progress helpers

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshot.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshotService.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelProbeSafetyValidator.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/NativeLoadProgressRecorder.cs`

**Interfaces:**
- Produces read-only file snapshots, integrity comparison, path-overwrite validation and `IProgress<float>` samples.

- [ ] **Step 1: Implement read-only asynchronous SHA-256 capture.**
- [ ] **Step 2: Implement before/after integrity comparison.**
- [ ] **Step 3: Implement canonical path fingerprinting.**
- [ ] **Step 4: Implement output/model path collision rejection.**
- [ ] **Step 5: Implement thread-safe native progress recording.**
- [ ] **Step 6: Run helper tests and confirm GREEN.**
- [ ] **Step 7: Document the folder boundary.**
- [ ] **Step 8: Commit helpers.**

---

### Task 4: Implement project-owned VocabOnly evidence

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbeResult.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyEvidenceCollector.cs`

**Interfaces:**
- Consumes `LLamaWeights` only inside the collector.
- Produces framework-neutral records for runtime, model, metadata, vocabulary, tokenizer, chat-template, progress, disposal and integrity evidence.

- [ ] **Step 1: Add completion-state and failure-code records.**
- [ ] **Step 2: Add model-file and integrity evidence records.**
- [ ] **Step 3: Add runtime model evidence fields.**
- [ ] **Step 4: Add vocabulary, special-token and tokenizer-smoke evidence.**
- [ ] **Step 5: Add chat-template presence, length and SHA-256.**
- [ ] **Step 6: Add selected metadata values and complete sorted key list.**
- [ ] **Step 7: Keep native objects out of every result type.**
- [ ] **Step 8: Commit evidence contracts and collector.**

---

### Task 5: Implement the VocabOnly probe orchestration

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbe.cs`

**Interfaces:**
- Produces `Task<VocabOnlyModelProbeResult> RunAsync(string modelPath, CancellationToken cancellationToken)`.
- Uses `ModelParams` with `VocabOnly = true`, `GpuLayerCount = 0`, `UseMemorymap = true`, and `UseMemoryLock = false`.

- [ ] **Step 1: Validate and snapshot the model before native loading.**
- [ ] **Step 2: configure and dry-run the matched CPU backend.**
- [ ] **Step 3: asynchronously load with genuine progress and cancellation.**
- [ ] **Step 4: collect evidence while the native handle is valid.**
- [ ] **Step 5: dispose in every success/failure/cancellation path.**
- [ ] **Step 6: capture post-probe identity and compare integrity.**
- [ ] **Step 7: map operational and load exceptions to stable feasibility codes.**
- [ ] **Step 8: record memory observations without presenting them as final estimates.**
- [ ] **Step 9: commit probe orchestration.**

---

### Task 6: Extend the command-line application

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike/SpikeOptionsParser.cs`
- Modify: `tools/ModelInspection.LlamaSharpSpike/Program.cs`

**Interfaces:**
- Existing no-model mode returns native smoke.
- `--model` returns VocabOnly model-probe evidence.
- Exit codes: `0` success, `1` failure, `2` invalid arguments, `3` cancellation.

- [ ] **Step 1: Parse model, output and cancellation options in any order.**
- [ ] **Step 2: choose the correct default output path by mode.**
- [ ] **Step 3: reject unsafe output/model equality before probing.**
- [ ] **Step 4: connect `Ctrl+C` and optional timed cancellation.**
- [ ] **Step 5: write evidence for success, failure and cancellation.**
- [ ] **Step 6: preserve native smoke behavior.**
- [ ] **Step 7: run parser, writer and helper tests.**
- [ ] **Step 8: build Release `win-x64`.**
- [ ] **Step 9: commit command integration.**

---

### Task 7: Reconcile READMEs and implementation tracking

**Files:**
- Modify: `tools/ModelInspection.LlamaSharpSpike/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`
- Modify: `docs/superpowers/plans/2026-08-04-llamasharp-feasibility-spike.md`
- Modify: `docs/superpowers/plans/2026-08-04-llamasharp-vocab-only-model-probe.md`

**Interfaces:**
- Produces accurate current-state documentation and target-machine commands.

- [ ] **Step 1: document the new CLI and evidence schema.**
- [ ] **Step 2: document `VocabOnly`, CPU-only and no-context boundaries.**
- [ ] **Step 3: log the new ModelProbe folder in the README hierarchy.**
- [ ] **Step 4: state that source exists but a real Granite run remains pending.**
- [ ] **Step 5: state that the WinUI page is still not connected.**
- [ ] **Step 6: preserve Vulkan and TurboQuant as later gates.**
- [ ] **Step 7: record exact verification commands and pending evidence.**
- [ ] **Step 8: commit documentation.**

---

### Task 8: Final source verification

**Files:**
- Modify: `docs/superpowers/plans/2026-08-04-llamasharp-vocab-only-model-probe.md`

- [ ] **Step 1: compare from pre-slice commit `f640c306bda3e44b85c3ce4412a55707c5e569f1`.**
- [ ] **Step 2: confirm no WinUI project package change.**
- [ ] **Step 3: confirm no Vulkan or TurboQuant dependency.**
- [ ] **Step 4: confirm the model is never opened for write.**
- [ ] **Step 5: record available test/build evidence or explicitly leave it pending.**
- [ ] **Step 6: commit verification status.**

## Verification Commands

```powershell
$SpikeProject =
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$SpikeTests =
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj"

# Restore and run deterministic tests.
dotnet restore $SpikeTests --runtime win-x64

dotnet test $SpikeTests `
    --configuration Release `
    --no-restore `
    --runtime win-x64

# Compile the exact Windows x64 tool.
dotnet build $SpikeProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64

# Preserve the existing native-library-only smoke.
dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --output "artifacts\model-inspection\llamasharp\runtime-smoke.json"

# Run the new controlled model probe.
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
0 = requested probe succeeded and evidence was written
1 = controlled failure and evidence was written where possible
2 = invalid or unsafe arguments
3 = controlled cancellation and evidence was written
```

## Current Execution State

- Design and this implementation plan are recorded.
- Source, tests and README reconciliation follow in this slice.
- No real Granite run is claimed until fresh Windows evidence is reviewed.
- WinUI integration, Vulkan and TurboQuant remain outside this plan.
