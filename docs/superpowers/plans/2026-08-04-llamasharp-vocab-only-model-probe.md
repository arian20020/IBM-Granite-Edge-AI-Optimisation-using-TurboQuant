# LLamaSharp VocabOnly Model Probe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Status:** First target-laptop run completed and diagnosed; metadata-only correction implemented; verification rerun pending.  
**Last reviewed:** 2026-08-04

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
- Do not claim success without a complete real-model run and evidence file.

---

## Task 1: Deterministic command, integrity and progress contracts

**Files:**
- `tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/ModelProbeSafetyValidatorTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/ModelFileSnapshotServiceTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/NativeLoadProgressRecorderTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/VocabOnlyMetadataProjectionTests.cs`

- [x] Test `--model` and mode-specific default output.
- [x] Test model, output and cancellation options in arbitrary order.
- [x] Test invalid, missing, duplicate and model-less cancellation arguments.
- [x] Test that evidence cannot overwrite the model.
- [x] Test SHA-256 snapshots and unchanged integrity.
- [x] Test detection of changed content/length/hash.
- [x] Test progress clamping, duplicate suppression and snapshot independence.
- [x] Test generic JSON writing, atomic overwrite and string enums.
- [x] Add metadata-only structural projection tests.
- [x] Execute the first target-laptop test run.
- [x] Diagnose the single failing parser assertion as incidental wording coupling.
- [x] Change the parser test to assert both required option names semantically.
- [ ] Rerun the full deterministic suite after the correction.

### First deterministic test result

```text
Total:      25
Passed:     24
Failed:      1
Skipped:     0
```

The failure was not parser behaviour. The test required one exact contiguous
phrase while the production message contained the same two required options
with introductory wording. The corrected test now checks that the message
contains both `--cancel-after-ms` and `--model` independently.

---

## Task 2: Shared CPU-native and JSON infrastructure

**Files:**
- `tools/ModelInspection.LlamaSharpSpike/CpuNativeRuntimeConfiguration.cs`
- `tools/ModelInspection.LlamaSharpSpike/JsonEvidenceWriter.cs`
- `tools/ModelInspection.LlamaSharpSpike/NativeBackendSmokeProbe.cs`

- [x] Extract thread-safe CPU-only native configuration.
- [x] Extract selected-backend description.
- [x] Replace the smoke-only writer with generic atomic JSON writing.
- [x] Configure enums as readable JSON strings.
- [x] Update native smoke to use shared helpers.
- [x] Run the target-laptop native CPU smoke.
- [x] Verify exact package/native identity, x64 process, AVX-512, CUDA false and Vulkan false.

### Native CPU smoke result

```text
Result:                     PASS
LLamaSharp:                 0.27.0
LLamaSharp.Backend.Cpu:     0.27.0
Mapped llama.cpp:           3f7c29d318e317b63f54c558bc69803963d7d88c
Process architecture:       X64
Selected library:           LLama
AVX level:                  AVX-512
CUDA:                       false
Vulkan:                     false
```

---

## Task 3: Model-file safety and progress helpers

**Files:**
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshot.cs`
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshotService.cs`
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelProbeSafetyValidator.cs`
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/NativeLoadProgressRecorder.cs`

- [x] Implement asynchronous read-only SHA-256 capture.
- [x] Verify length and last-write time remain stable while hashing.
- [x] Implement before/after integrity comparison.
- [x] Implement canonical-path SHA-256 rather than path serialization.
- [x] Reject output/model path collision.
- [x] Implement thread-safe genuine native-progress recording.
- [x] Add source-adjacent folder documentation.
- [x] Independently hash the real Granite model before and after the first run.
- [x] Verify the original SHA-256 remained unchanged after native process termination.
- [ ] Rerun helper tests after the complete correction set.

---

## Task 4: Project-owned VocabOnly evidence

**Files:**
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbeResult.cs`
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyMetadataProjection.cs`
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyEvidenceCollector.cs`

- [x] Add `Succeeded`, `Cancelled` and `Failed` operation states.
- [x] Add runtime/package identity and selected CPU backend evidence.
- [x] Add before/after file identity and integrity evidence.
- [x] Add vocabulary and known special-token evidence.
- [x] Add fixed non-sensitive `Hello` tokenizer smoke.
- [x] Add chat-template presence, length and SHA-256 without full template text.
- [x] Add progress, memory and disposal fields.
- [x] Keep LLamaSharp/native objects out of every result type.
- [x] Diagnose that `VocabOnly` returns before native hyperparameters are populated.
- [x] Remove native hyperparameter accessors from the VocabOnly collector.
- [x] Add architecture-scoped GGUF metadata projection.
- [x] Make unavailable evidence nullable rather than representing it as zero.
- [x] Bump the VocabOnly evidence schema to `1.1`.
- [ ] Verify the corrected collector against the real Granite model.

### Root cause established from the first real-model run

The first collector called LLamaSharp properties such as:

```text
HeadCount
KVHeadCount
LayerCount
ContextSize
EmbeddingSize
```

The mapped llama.cpp implementation deliberately returns from hyperparameter
loading when `vocab_only` is enabled. Therefore no native layer/head arrays are
initialised at this depth.

`HeadCount` called llama.cpp's `n_head(0)`. Since `n_layer` was zero, the native
code reached:

```text
GGML_ABORT("fatal error")
```

The process terminated before C# exception handling or JSON writing could
complete.

The correction now projects safe structural values from ordinary GGUF metadata
and leaves unavailable runtime-derived fields null.

---

## Task 5: VocabOnly orchestration

**Files:**
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbe.cs`

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
- [x] Run the first controlled real Granite probe.
- [x] Record the process-level native abort and unchanged model hash.
- [ ] Rerun after removing unsafe VocabOnly getters.
- [ ] Confirm JSON evidence is written.
- [ ] Confirm deterministic native disposal and integrity fields.

### First real-model input and result

```text
Model:
granite-4.1-3b-Q4_K_M.gguf

Size:
2,099,501,664 bytes

SHA-256 before and after:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29

Native result:
llama-hparams.cpp:35: fatal error

Windows exit code:
-1073740791

JSON result:
not written because the process was terminated by native code
```

This is recorded as a probe implementation failure at the selected depth, not
as an invalid or unsupported Granite outcome.

---

## Task 6: Command-line application

**Files:**
- `tools/ModelInspection.LlamaSharpSpike/SpikeOptionsParser.cs`
- `tools/ModelInspection.LlamaSharpSpike/Program.cs`

- [x] Preserve no-model native-smoke mode.
- [x] Add `--model`, `--output` and `--cancel-after-ms` in arbitrary order.
- [x] Choose the correct default output by mode.
- [x] Reject unsafe output/model equality before probing.
- [x] Connect `Ctrl+C` and optional timed cancellation.
- [x] Write evidence for success, failure and cancellation when native code returns control.
- [x] Use exit codes `0`, `1`, `2` and `3`.
- [x] Diagnose and correct the brittle parser test.
- [ ] Rerun parser/writer/helper tests.
- [ ] Rebuild Release `win-x64`.

---

## Task 7: README hierarchy

**Files:**
- `tools/ModelInspection.LlamaSharpSpike/README.md`
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- `IBM Granite with TurboQuant (Intel)/Features/README.md`

- [x] Document both native-smoke and VocabOnly modes.
- [x] Document CPU-only, VocabOnly and no-context boundaries.
- [x] Add the `ModelProbe` folder to the README hierarchy.
- [x] State that the WinUI page remains unconnected.
- [x] Preserve Vulkan and TurboQuant as later gates.
- [x] Record the first target-laptop run, native abort and unchanged hash in the nearest README.
- [x] Record the root-cause correction and pending rerun.
- [ ] Update broader feature documentation after the corrected runtime result is known.

---

## Task 8: Verification rerun

Run from Developer PowerShell at the repository root:

```powershell
$SpikeProject =
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$SpikeTests =
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj"

$ModelPath = Join-Path `
    $env:USERPROFILE `
    "Downloads\granite-4.1-3b-Q4_K_M.gguf"

# Pull the correction.
git pull --ff-only origin feature/model-inspection

# Restore, test and build.
dotnet restore $SpikeTests --runtime win-x64

dotnet test $SpikeTests `
    --configuration Release `
    --no-restore `
    --runtime win-x64 `
    --minimum-expected-tests 1

dotnet build $SpikeProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64

# Run a new, uniquely identified model probe.
$RunId = Get-Date -Format "yyyyMMdd-HHmmss"
$ProbeOutput =
    "artifacts\model-inspection\llamasharp\runs\$RunId\vocab-only-model-probe.json"

dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --model $ModelPath `
    --output $ProbeOutput
```

Expected exit codes:

```text
0 = requested probe succeeded and evidence was written
1 = controlled failure and evidence was written where possible
2 = invalid or unsafe arguments
3 = controlled cancellation and evidence was written
```

## Stop conditions

Stop immediately and preserve the first new failure when:

- any deterministic test fails;
- Release build fails;
- the native process terminates again;
- no JSON is produced;
- the before/after model hashes differ.

If the native process still terminates before the corrected collector runs, the
next architecture review is process isolation behind the existing
`ILlamaModelProbe` boundary. Do not classify the model as invalid.

## Current execution state

- Matched CPU native smoke: **passed on target laptop**.
- First deterministic suite: **24 passed, 1 wording-coupled failure; corrected**.
- First real Granite VocabOnly run: **native abort diagnosed**.
- Original model SHA-256: **unchanged**.
- Metadata-only collector correction: **implemented**.
- Corrected test/build/real-model rerun: **pending**.
- WinUI integration, Vulkan, TurboQuant and final outcome classification: **outside this slice**.
