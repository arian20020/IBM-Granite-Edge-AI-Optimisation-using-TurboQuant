# LLamaSharp VocabOnly Model Probe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Status:** Corrected deterministic suite, Release build and real Granite VocabOnly probe passed; controlled cancellation remains  
**Last reviewed:** 2026-08-04

**Goal:** Extend the isolated LLamaSharp feasibility tool so it can probe one real local GGUF through the matched CPU runtime using `VocabOnly`, capture proportionate evidence, verify file preservation, and remain outside the WinUI application.

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
- Do not claim cancellation support until a controlled cancellation result exists.

---

## Task 1: Deterministic command, integrity and progress contracts

**Files:**
- `tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/ModelProbeSafetyValidatorTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/ModelFileSnapshotServiceTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/NativeLoadProgressRecorderTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/VocabOnlyMetadataProjectionTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/VocabOnlyModelProbeFailureTests.cs`
- `tools/ModelInspection.LlamaSharpSpike.Tests/PinnedApplicationRuntimeTests.cs`

- [x] Test `--model` and mode-specific default output.
- [x] Test model, output and cancellation options in arbitrary order.
- [x] Test invalid, missing, duplicate and model-less cancellation arguments.
- [x] Test that evidence cannot overwrite the model.
- [x] Test SHA-256 snapshots and unchanged integrity.
- [x] Test detection of changed content/length/hash.
- [x] Test progress clamping, duplicate suppression and snapshot independence.
- [x] Test generic JSON writing, atomic overwrite and string enums.
- [x] Add metadata-only structural projection tests.
- [x] Replace constant-to-literal assertions with project-file dependency-policy tests.
- [x] Correct the parser test to assert behaviour rather than incidental sentence wording.
- [x] Run the complete corrected target-laptop suite.

### Corrected deterministic result

```text
Project:       ModelInspection.LlamaSharpSpike.Tests
Configuration: Release / win-x64
Total:         28
Passed:        28
Failed:        0
Skipped:       0
Exit code:     0
```

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
- [x] Independently hash the real Granite model before and after both native runs.
- [x] Verify the original SHA-256 remained unchanged after the native abort and corrected success.
- [x] Run the corrected helper tests as part of the 28-test suite.

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
- [x] Verify the corrected collector against the real Granite model.

### Verified evidence from the controlled model

```text
Architecture:                granite
Model name:                  Granite 4.1 3b
File type:                   15
Quantisation version:        2
Tokenizer model:             gpt2
Context size:                131,072
Embedding size:              2,560
Layer count:                 40
Attention head count:        40
KV-head count:               8
Metadata count:              31
Vocabulary count:            100,352
Tokenizer smoke:             PASS / 1 token
Chat template:               present
Parameter count:             unavailable / null
```

The parameter count remains unavailable at this depth. This is represented as
`null`; it is not guessed from the filename and is not treated as zero.

---

## Task 5: VocabOnly orchestration

**Files:**
- `tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbe.cs`

- [x] Validate and snapshot the model before native loading.
- [x] Configure and dry-run the matched CPU backend.
- [x] Load asynchronously with `VocabOnly = true`, zero GPU layers, genuine progress and cancellation support.
- [x] Collect evidence only while the native handle is valid.
- [x] Dispose native weights in success/failure paths where a handle exists.
- [x] Capture post-probe identity and compare integrity.
- [x] Convert integrity change or unverifiable successful probe to controlled failure.
- [x] Map operational, native, load and cancellation failures to stable codes.
- [x] Record memory observations without treating them as final Hardware Fit estimates.
- [x] Redact the full canonical model path from serialized failures and logs.
- [x] Run the first controlled real Granite probe and diagnose the unsafe getter abort.
- [x] Rerun after removing unsafe VocabOnly getters.
- [x] Confirm JSON evidence is written.
- [x] Confirm deterministic native disposal and integrity fields.
- [ ] Run controlled cancellation and verify `Cancelled`, exit code `3`, evidence writing and file preservation.

### Corrected real-model result

```text
Run ID:                      20260804-154719
Model:                       granite-4.1-3b-Q4_K_M.gguf
Size:                        2,099,501,664 bytes
SHA-256 before and after:    662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
Completion status:           Succeeded
Exit code:                   0
Evidence schema:             1.1
Native progress samples:     1
Load duration:               398 ms
Native handle closed:        true
File preserved:              true
```

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
- [x] Run parser/writer/helper tests.
- [x] Build Release `win-x64` successfully.
- [ ] Execute and review the real cancellation path.

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
- [x] Record the first target-laptop abort and root cause.
- [x] Record the corrected 28/28 suite, Release build and successful real-model result in the nearest READMEs.
- [ ] Update the broader feature READMEs when controlled cancellation closes the feasibility stage.

---

## Task 8: Remaining cancellation verification

Run from Developer PowerShell at the repository root:

```powershell
$SpikeProject =
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj"

$ModelPath = Join-Path `
    $env:USERPROFILE `
    "Downloads\granite-4.1-3b-Q4_K_M.gguf"

$ExpectedModelHash = (
    Get-FileHash -LiteralPath $ModelPath -Algorithm SHA256
).Hash.ToLowerInvariant()

$RunId = Get-Date -Format "yyyyMMdd-HHmmss"
$CancellationOutput =
    "artifacts\model-inspection\llamasharp\runs\$RunId\vocab-only-cancellation.json"

dotnet run `
    --project $SpikeProject `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --model $ModelPath `
    --cancel-after-ms 1 `
    --output $CancellationOutput

$CancellationExitCode = $LASTEXITCODE

$ActualModelHashAfter = (
    Get-FileHash -LiteralPath $ModelPath -Algorithm SHA256
).Hash.ToLowerInvariant()
```

Required result:

```text
Completion status: Cancelled
Failure code:       MI-PROBE-CANCELLED
Exit code:          3
Evidence JSON:      present
File hash:          unchanged
```

If a one-millisecond timer cancels before model loading begins, that is still a
valid token-propagation result. A later integration test may also cancel after a
reported native progress sample when a slower controlled model or test hook is
available.

## Stop conditions

Stop immediately and preserve the first new failure when:

- the cancellation process terminates natively;
- no cancellation JSON is produced;
- completion is reported as model failure rather than cancellation;
- the before/after model hashes differ.

## Current execution state

- Matched CPU native smoke: **passed on target laptop**.
- Corrected deterministic suite: **28/28 passed**.
- Corrected Release `win-x64` build: **passed**.
- Corrected real Granite VocabOnly probe: **passed**.
- Runtime metadata, vocabulary, tokenizer and chat-template evidence: **captured**.
- Native model handle disposal: **verified**.
- Original model SHA-256 preservation: **verified**.
- Controlled cancellation: **pending**.
- Production `ILlamaModelProbe`, service, ViewModel, Vulkan and TurboQuant: **outside this slice**.
