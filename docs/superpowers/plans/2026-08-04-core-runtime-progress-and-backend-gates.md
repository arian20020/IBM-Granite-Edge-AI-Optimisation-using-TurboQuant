# Core Runtime Progress and Backend Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the initial Model Inspection progress tracker explicitly represent lightweight core-runtime compatibility while documenting Vulkan and TurboQuant as later backend-verification gates.

**Architecture:** Extract initial progress construction from `ModelInspectionPage` into one focused presentation factory. Protect the exact five-row semantics with WinUI unit tests, update the page to consume the factory, and record the core-runtime/backend boundary in an ADR and source-adjacent documentation.

**Tech Stack:** WinUI 3, C# 12/.NET 8, MSTest AppContainer tests, Markdown architecture records.

## Global Constraints

- Target branch: `feature/model-inspection`.
- Keep exactly five visible Model Inspection progress rows.
- Final visible stage text: `Confirm core runtime compatibility`.
- Do not add CPU, Vulkan, GPU, Hardware Fit or TurboQuant as Model Inspection rows.
- Do not add any new runtime package to the WinUI application project.
- Do not run or claim Vulkan/TurboQuant support in this slice.
- Keep the page presentation-only; no ViewModel, service or runtime execution is introduced.
- Add a README to every new meaningful source folder.
- Preserve existing UI geometry and card contracts.

---

### Task 1: Record the architecture boundary

**Files:**
- Create: `docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md`
- Modify: `docs/architecture/decisions/README.md`

**Interfaces:**
- Consumes: ADR-001 runtime identity and the approved progress/backend-gate design.
- Produces: one accepted rule separating lightweight core inspection from later Vulkan/TurboQuant backend verification.

- [x] **Step 1: Add ADR-002 with context, decision, alternatives, consequences and review triggers.**
- [x] **Step 2: Add ADR-002 to the decision index.**
- [x] **Step 3: Verify the ADR does not claim successful CPU, Vulkan or TurboQuant execution.**
- [x] **Step 4: Commit the architecture record.**

---

### Task 2: Define the progress contract with tests

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs`

**Interfaces:**
- Consumes: `InitialInspectionProgressPresentationFactory.Create()`.
- Produces: executable contract for stage count, order, wording, active state and connector behavior.

- [x] **Step 1: Write a test asserting the exact five ordered titles.**

```csharp
InspectionContentCardPresentation presentation =
    InitialInspectionProgressPresentationFactory.Create();

CollectionAssert.AreEqual(
    new[]
    {
        "Check model package",
        "Read model configuration",
        "Validate tokenizer and chat setup",
        "Validate model structure",
        "Confirm core runtime compatibility"
    },
    presentation.Items.Select(item => item.Title).ToArray());
```

- [x] **Step 2: Write a test asserting only stage 1 is active and stages 2–5 are waiting.**
- [x] **Step 3: Write a test asserting stage 5 has no connector.**
- [x] **Step 4: Write a test rejecting `Vulkan`, `TurboQuant`, `GPU` and `hardware fit` in visible stage titles.**
- [x] **Step 5: Write a test asserting `0 of 5 checks complete`.**
- [ ] **Step 6: Run the focused test and observe the expected RED failure because the factory does not exist.**
- [x] **Step 7: Commit the test contract.**

The test source was created before the factory source, but the connected GitHub editing environment could not execute the packaged WinUI test app. No retrospective RED-run claim is made.

---

### Task 3: Implement the initial progress presentation factory

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/InitialInspectionProgressPresentationFactory.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`

**Interfaces:**
- Consumes: `InspectionContentCardPresentation`, `InspectionContentItemPresentation`, `InspectionContentCardMode`, `InspectionContentStatus`, and WinUI `Visibility`.
- Produces: `internal static InspectionContentCardPresentation InitialInspectionProgressPresentationFactory.Create()`.

- [x] **Step 1: Create the factory with the exact five ordered stages.**
- [x] **Step 2: Keep stage 1 active with visible detail and stages 2–5 waiting.**
- [x] **Step 3: Give every stage a meaningful internal detail string without displaying waiting-stage detail initially.**
- [x] **Step 4: Set connectors on stages 1–4 only.**
- [x] **Step 5: Move the progress-item helper into the factory.**
- [x] **Step 6: Update `ModelInspectionPage` to call the factory.**
- [x] **Step 7: Remove the duplicated private progress-construction methods from the page.**
- [x] **Step 8: Document the factory's narrow presentation responsibility and non-goals.**
- [ ] **Step 9: Run the focused test and confirm GREEN.**
- [ ] **Step 10: Run the full WinUI test project.**
- [x] **Step 11: Commit the implementation.**

---

### Task 4: Reconcile feature documentation

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Models/README.md`

**Interfaces:**
- Consumes: ADR-002, new factory path and the approved engineering gate ladder.
- Produces: current source hierarchy, user-visible stage semantics and explicit backend-verification non-claims.

- [x] **Step 1: Add `Presentation/README.md` to the Model Inspection folder tree and child links.**
- [x] **Step 2: Replace `Confirm runtime support` with `Confirm core runtime compatibility`.**
- [x] **Step 3: Document the seven engineering gates separately from the five UI rows.**
- [x] **Step 4: State that ordinary Vulkan and TurboQuant validation belongs after core inspection and Hardware Fit selection.**
- [x] **Step 5: Record that the current WinUI application still has no Vulkan or TurboQuant runtime package.**
- [x] **Step 6: Clarify that the `Models` folder contains presentation data while the new `Presentation` folder contains construction behavior.**
- [x] **Step 7: Commit the documentation updates.**

---

### Task 5: Final verification

**Files:**
- Modify: `docs/superpowers/plans/2026-08-04-core-runtime-progress-and-backend-gates.md`

- [x] **Step 1: Confirm all new and modified source paths exist on the branch.**
- [x] **Step 2: Confirm `ModelInspectionPage.xaml.cs` no longer contains `Confirm runtime support`.**
- [x] **Step 3: Confirm the application `.csproj` has no Vulkan or TurboQuant package reference.**
- [x] **Step 4: Compare from the pre-slice commit and verify no runtime package or workflow was added.**
- [x] **Step 5: Record actual test/build evidence or explicitly record that target Windows verification remains pending.**
- [x] **Step 6: Commit the verification status.**

## Verification Evidence

Repository inspection confirmed these source paths exist on `feature/model-inspection`:

```text
Features/ModelInspection/Presentation/
    InitialInspectionProgressPresentationFactory.cs
    README.md

tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/
    InitialInspectionProgressPresentationTests.cs

docs/architecture/decisions/
    ADR-002-core-inspection-versus-backend-verification.md
```

`ModelInspectionPage.xaml.cs` now imports the `Presentation` namespace and calls:

```csharp
InitialInspectionProgressPresentationFactory.Create();
```

The former private progress-construction method and its helper were removed from the page.

A comparison from pre-slice commit
`86e10d56472ff55d36b0d65c185625f6af467310` showed only:

```text
Model Inspection C# presentation/page source
one WinUI test file
feature/source-adjacent READMEs
ADR/design/plan Markdown
```

No application project file, runtime package declaration or workflow was added or modified by this slice.

The current application project package section still contains only:

```text
Microsoft.Windows.SDK.BuildTools
Microsoft.WindowsAppSDK
```

No `LLamaSharp.Backend.Vulkan`, TurboQuant or other new backend package is present in the WinUI application project.

## Verification Commands

Run from the repository root in Developer PowerShell:

```powershell
$TestProject =
    "tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj"

# Restore and compile the packaged WinUI test project.
dotnet restore $TestProject `
    --runtime win-x64 `
    -p:Platform=x64

dotnet build $TestProject `
    --configuration Release `
    --no-restore `
    --runtime win-x64 `
    -p:Platform=x64

# Verify no backend dependency was introduced into the application project.
Select-String `
    -LiteralPath `
        "IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj" `
    -Pattern "LLamaSharp.Backend.Vulkan|TurboQuant"
```

The final `Select-String` command should return no matches.

Run the packaged AppContainer test recipe through Visual Studio/Test Explorer or the existing `build-and-test.yml` workflow to execute `[UITestMethod]` tests.

## Current Execution State

- ADR-002, design, plan, source, focused test contract and README hierarchy are on `feature/model-inspection`.
- The UI still contains five rows and now uses `Confirm core runtime compatibility` as the final row.
- No Vulkan or TurboQuant backend has been added to the application.
- No CPU, Vulkan or TurboQuant execution success is claimed by this slice.
- A fresh Windows build and packaged WinUI test run remain required before claiming compilation and test success.
- The next runtime action remains the previously scaffolded matched LLamaSharp CPU smoke, followed by controlled CPU model inspection only after its evidence is reviewed.
