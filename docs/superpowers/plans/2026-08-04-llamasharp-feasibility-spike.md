# LLamaSharp Feasibility Spike Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add and verify the first isolated LLamaSharp feasibility slice: an exact-version CPU native-backend dry run that writes structured evidence without changing the WinUI application.

**Architecture:** A standalone .NET 8 console project references `LLamaSharp` and `LLamaSharp.Backend.Cpu` 0.27.0. It uses `NativeLibraryConfig.LLama.DryRun` to prove backend discovery, converts the result into project-owned JSON data, and remains separate from the production Model Inspection feature.

**Tech Stack:** .NET 8 console application, LLamaSharp 0.27.0, LLamaSharp.Backend.Cpu 0.27.0, MSTest 4.3.2, System.Text.Json.

## Global constraints

- Target branch: `feature/model-inspection`.
- Keep upstream `b9870` evidence as research only.
- Pin the application pair to `LLamaSharp` 0.27.0 and `LLamaSharp.Backend.Cpu` 0.27.0.
- Record mapped `llama.cpp` commit `3f7c29d318e317b63f54c558bc69803963d7d88c`.
- Do not add LLamaSharp references to the WinUI application project in this slice.
- Do not load a model in this slice.
- Disable CUDA and Vulkan selection for the CPU gate.
- Do not auto-download native libraries.
- Map runtime infrastructure failures to operational codes, never model outcomes.
- Generated local evidence belongs under ignored `artifacts/` until formally reviewed.
- Do not claim a successful Windows x64 run without fresh target-machine output.

---

### Task 1: Record the application runtime decision

**Files:**
- Create: `docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md`
- Create: `docs/superpowers/specs/2026-08-04-llamasharp-feasibility-spike-design.md`
- Create: `docs/superpowers/plans/2026-08-04-llamasharp-feasibility-spike.md`

**Interfaces:**
- Consumes: the approved Model Inspection development plan and selected runtime decision.
- Produces: one accepted runtime identity and the staged feasibility boundary used by every later task.

- [x] **Step 1: Record the matched managed/native pair.**
- [x] **Step 2: Separate the `b9870` research track from application claims.**
- [x] **Step 3: Record feasibility gates and non-claims.**
- [x] **Step 4: Commit the decision, design and plan.**

---

### Task 2: Define unit-test contracts before implementation

**Files:**
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/PinnedApplicationRuntimeTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs`

**Interfaces:**
- Consumes: exact constants and command/evidence contracts from the design.
- Produces: executable tests for `PinnedApplicationRuntime`, `SpikeOptionsParser` and `SmokeEvidenceWriter`.

- [x] **Step 1: Create the MSTest project with exact package versions.**
- [x] **Step 2: Test the exact LLamaSharp/backend/llama.cpp pins and research separation.**
- [x] **Step 3: Test default, output, help and invalid command-line inputs.**
- [x] **Step 4: Test JSON writing, parseability and overwrite behavior.**
- [ ] **Step 5: Run the tests and confirm the expected RED compilation failures because production types do not yet exist.**
- [x] **Step 6: Commit the test contracts.**

The source was written test-first, but no local RED command output was available in the connected GitHub editing environment. The plan does not retroactively claim a RED run.

---

### Task 3: Implement the isolated smoke project

**Files:**
- Create: `tools/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike/README.md`
- Create: `tools/ModelInspection.LlamaSharpSpike/ModelInspection.LlamaSharpSpike.csproj`
- Create: `tools/ModelInspection.LlamaSharpSpike/PinnedApplicationRuntime.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/SpikeOptionsParser.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/NativeBackendSmokeResult.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/SmokeEvidenceWriter.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/NativeBackendSmokeProbe.cs`
- Create: `tools/ModelInspection.LlamaSharpSpike/Program.cs`
- Create: `.github/workflows/llamasharp-feasibility-smoke.yml`

**Interfaces:**
- Consumes: `LLama.Native.NativeLibraryConfig`, `LLama.Abstractions.INativeLibrary`, `System.Text.Json`.
- Produces:
  - `PinnedApplicationRuntime` exact constants;
  - `SpikeOptionsParser.Parse(...)`;
  - `NativeBackendSmokeProbe.Run()`;
  - `SmokeEvidenceWriter.WriteAsync(...)`;
  - exit codes `0`, `1`, and `2`;
  - Windows-hosted test/build/dry-run evidence when the workflow executes.

- [x] **Step 1: Create the console project with exact LLamaSharp package references.**
- [x] **Step 2: Add exact runtime identity constants.**
- [x] **Step 3: Add the small command-line parser.**
- [x] **Step 4: Add the project-owned smoke result and log records.**
- [x] **Step 5: Add atomic JSON evidence writing.**
- [x] **Step 6: Add the CPU-only native backend dry-run probe.**
- [x] **Step 7: Add the console entry point and controlled exit codes.**
- [ ] **Step 8: Run unit tests and confirm GREEN.**
- [ ] **Step 9: Build the console project in Release.**
- [x] **Step 10: Commit the implementation source.**
- [x] **Step 11: Add a read-only Windows workflow for tests, build, dry run and artifact upload.**

---

### Task 4: Update dependency and feature documentation

**Files:**
- Modify: `docs/risks/Licence-Register.md`
- Modify: `docs/architecture/decisions/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`

**Interfaces:**
- Consumes: selected package identities and spike paths.
- Produces: current dependency, architecture and non-claim documentation.

- [x] **Step 1: Add LLamaSharp and its CPU backend to the licence register.**
- [x] **Step 2: Link Model Inspection documentation to ADR-001 and the spike.**
- [x] **Step 3: State that the WinUI project still has no LLamaSharp reference.**
- [x] **Step 4: Preserve the distinction between research and application runtimes.**
- [x] **Step 5: Index ADR-001 and commit documentation updates.**

---

### Task 5: Verify on the Windows x64 target

**Files produced locally:**
- `artifacts/model-inspection/llamasharp/runtime-smoke.json`

**Interfaces:**
- Consumes: the built smoke tool and published CPU backend package.
- Produces: one target-machine runtime smoke result and logs.

- [ ] **Step 1: Pull the branch and restore the test project.**
- [ ] **Step 2: Run all spike unit tests.**
- [ ] **Step 3: Build the spike project in Release.**
- [ ] **Step 4: Run the spike with an explicit output path.**
- [ ] **Step 5: Confirm exit code `0`, JSON parseability and CPU-only backend metadata.**
- [ ] **Step 6: Confirm the WinUI project file still has no LLamaSharp package reference.**
- [ ] **Step 7: Preserve stdout, stderr, command, JSON and package-lock information for review.**
- [ ] **Step 8: Stop and classify any failure before attempting model loading.**

---

### Task 6: Plan Slice 2 only after the native smoke is accepted

**Files:**
- Create later: the controlled GGUF request/integrity design and implementation plan.

- [ ] **Step 1: Review the Windows workflow and target-machine smoke evidence.**
- [ ] **Step 2: Decide the controlled real Granite fixture and hash.**
- [ ] **Step 3: Define `--model`, integrity and `VocabOnly` contracts.**
- [ ] **Step 4: Write failing tests before adding model loading.**

## Verification commands

Run from the repository root on Windows:

```powershell
dotnet restore `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --runtime win-x64

dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --configuration Release `
    --no-restore `
    --runtime win-x64

dotnet build `
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --no-restore `
    --runtime win-x64

dotnet run `
    --project "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --no-build `
    --runtime win-x64 `
    -- `
    --output "artifacts\model-inspection\llamasharp\runtime-smoke.json"
```

Expected final command behavior:

```text
0 = CPU backend dry run succeeded and evidence was written
1 = controlled runtime/evidence failure
2 = invalid command-line arguments
```

## Current execution state

- Runtime decision, design, implementation plan, source, tests, documentation and Windows workflow are on `feature/model-inspection`.
- No GREEN test output, Release build output or native dry-run output is claimed yet.
- The dedicated workflow is the first hosted verification route; the target-laptop run remains separately required.
- The WinUI application project has not been given a LLamaSharp package reference.
- Slice 2 remains blocked until the native smoke evidence is reviewed.
