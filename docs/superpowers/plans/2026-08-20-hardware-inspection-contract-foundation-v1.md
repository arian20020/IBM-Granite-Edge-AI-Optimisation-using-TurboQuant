# Hardware Inspection Contract Foundation v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a small immutable Hardware Inspection snapshot, actionable handoff, and fresh-memory provider seam that later UI and compatibility work can consume safely.

**Architecture:** Pure domain values live under `Features/HardwareInspection/Domain`; application-facing outcome, handoff, and memory-provider contracts live under `Features/HardwareInspection/Application`. Constructors validate invariants and copy collections so provider-owned mutable state cannot escape. The existing MSTest project tests the public surface and fail-closed behavior.

**Tech Stack:** C# 12, .NET 8, MSTest, Windows application project.

---

### Task 1: Lock the public surface with failing tests

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionContractTests.cs`

- [ ] **Step 1: Write tests for exact enums, snapshot validation, handoff eligibility, privacy exclusions, and fresh memory.**

Use reflection for exact public shapes and direct construction for behavioral invariants. Include valid evidence for `processor.name`, `memory.installedBytes`, `memory.osUsableBytes`, and `memory.availableBytes`.

- [ ] **Step 2: Run the focused tests and verify RED.**

Run:

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -c Debug -p:Platform=x64 --filter "FullyQualifiedName~HardwareInspectionContractTests" --no-restore
```

Expected: compilation fails because the Hardware Inspection types do not exist.

- [ ] **Step 3: Commit the RED test.**

```powershell
git add -- "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionContractTests.cs"
git commit -m "test(hardware-inspection): lock contract foundation"
```

### Task 2: Implement immutable domain facts

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain/HardwareInspectionEnums.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain/HardwareFacts.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain/HardwareEvidence.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain/HardwareSnapshot.cs`

- [ ] **Step 1: Add exact enums.**

Define NPU presence, snapshot usability, evidence source, evidence resolution, confidence, runtime backend, and processor instruction-set members used by the design. Do not add provider-specific DTO members.

- [ ] **Step 2: Add validated scalar facts.**

Use sealed records for processor, memory, graphics, NPU, storage, OS, runtime device, and runtime capability facts. Reject blank identity strings, non-positive counts, non-UTC timestamps, and impossible byte relationships.

- [ ] **Step 3: Add the evidence manifest.**

Copy entries into a read-only collection, reject duplicate canonical field keys, and restrict safe diagnostic codes to lowercase ASCII tokens containing letters, digits, dots, and hyphens.

- [ ] **Step 4: Add `HardwareSnapshot`.**

Copy all collections. For `Usable`, require the four v1 critical evidence keys in a resolved state. Preserve dedicated/shared graphics memory as separate values.

- [ ] **Step 5: Run the focused tests.**

Use the Task 1 command. Expected: domain tests pass; application/handoff tests remain red until Task 3.

- [ ] **Step 6: Commit the domain implementation.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Domain"
git commit -m "feat(hardware-inspection): add canonical snapshot contract"
```

### Task 3: Implement the application handoff and memory seam

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Application/HardwareInspectionOutcome.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Application/HardwareInspectionHandoff.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Application/IAvailableMemoryProvider.cs`

- [ ] **Step 1: Add the four terminal outcomes.**

The enum must contain exactly `Completed`, `CompletedWithWarnings`, `Failed`, and `Cancelled`.

- [ ] **Step 2: Add `HardwareInspectionHandoff.Create`.**

Reject `Guid.Empty`, non-actionable outcomes, null snapshots, and snapshots whose usability is not `Usable`. Store exactly `InspectionId` and `Snapshot` as public properties.

- [ ] **Step 3: Add the fresh-memory contract.**

Define `AvailableMemorySnapshot` with positive bytes and UTC time validation, and `IAvailableMemoryProvider.CaptureAsync(CancellationToken)` returning `ValueTask<AvailableMemorySnapshot>`.

- [ ] **Step 4: Run the focused tests and verify GREEN.**

Use the Task 1 command. Expected: all `HardwareInspectionContractTests` pass.

- [ ] **Step 5: Commit the application contract.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/Application"
git commit -m "feat(hardware-inspection): add actionable handoff contract"
```

### Task 4: Publish a downstream contract snapshot and verify

**Files:**
- Create: `docs/testing/hardware-inspection/Hardware-Inspection-Contract-v1.md`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionContractTests.cs`

- [ ] **Step 1: Record the exact contract.**

Document every public type/property, enum value, unit, nullability rule, required evidence key, privacy exclusion, handoff eligibility rule, and the owner commit containing Tasks 1–3.

- [ ] **Step 2: Bind the snapshot in tests.**

Assert the document exists, names the owner commit, lists both handoff properties, and contains the GGUF/OpenVINO-neutral boundary: model formats are interpreted only by Model Inspection and Block 3, never Hardware providers.

- [ ] **Step 3: Run focused and regression tests.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -c Debug -p:Platform=x64 --filter "FullyQualifiedName~HardwareInspectionContractTests"
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -c Debug -p:Platform=x64
git diff --check
```

Expected: all tests pass and `git diff --check` emits no output.

- [ ] **Step 4: Commit the frozen snapshot.**

```powershell
git add -- "docs/testing/hardware-inspection/Hardware-Inspection-Contract-v1.md" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/HardwareInspection/HardwareInspectionContractTests.cs"
git commit -m "docs(hardware-inspection): publish contract snapshot"
```

### Task 5: Final verification and handoff

- [ ] **Step 1: Confirm exact scope and clean tree.**

```powershell
git status --short
git diff --check origin/main...HEAD
git diff --name-only origin/main...HEAD
```

- [ ] **Step 2: Re-run the focused test from clean committed bytes.**

Run the focused Task 4 command. Expected: pass.

- [ ] **Step 3: Report branch, commits, tests, contract path, and explicit non-claims.**

State that no provider, hardware execution, compatibility calculation, UI, workflow, candidate, laptop, or Gate status changed.
