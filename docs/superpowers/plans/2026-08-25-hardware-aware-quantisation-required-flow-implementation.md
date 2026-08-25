# Hardware-Aware Quantisation-Required Flow Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to execute this plan task-by-task. Use `superpowers:test-driven-development` for every behaviour change and `superpowers:verification-before-completion` before claiming a task or the plan complete.

**Goal:** Turn the existing compatibility result into an honest, hardware-relative decision: use the imported model unchanged when it fits; show “This model needs to be quantised to run on your computer” when a verified lower-memory configuration fits; and show a genuine no-safe-configuration result only when no admitted configuration fits.

**Architecture:** Extend the existing C1 candidate frontier rather than creating a second compatibility calculator. Correct the OpenVINO contract so TurboQuant is represented as a KV-cache algorithm, add the GGUF Q2_K product floor and controlled requantisation evidence, then project baseline-versus-alternative fit into the existing immutable screen model. WinUI renders that projection with the established Model Download slider vocabulary and visual tokens. This increment ends at a validated optimisation-selection handoff; executors, optimisation progress, export, and chat remain downstream.

**Tech Stack:** C# 12, .NET 8, WinUI 3 / Windows App SDK 2.2, immutable records, MSTest 4.3.2, Visual Studio packaged AppContainer VSTest, PowerShell, existing C1 GGUF/OpenVINO estimators and optimisation frontier.

---

## Non-negotiable product rules

- Keep the existing five manual labels exactly: `Maximum efficiency`, `Efficient`, `Balanced`, `High capability`, and `Maximum capability`. `Automatic` remains a separate default, not a sixth slider position.
- A mode is an objective evaluated against the current machine, model, context, route capabilities, and safe memory budget. It is never a hard-coded quantisation format.
- Keep weight and KV-cache compression separate. TurboQuant is a KV-cache choice, not an OpenVINO weight format.
- GGUF weight candidates are: imported, BF16/F16 only when a genuine higher-precision source exists, Q8_0, Q6_K, Q5_K_M, Q4_K_M, Q3_K_M, and Q2_K as the controlled product floor.
- OpenVINO weight candidates are: current/original, FP16, INT8, and INT4. OpenVINO cache candidates are F16, BF16, U8, U4, and evidence-gated experimental TBQ4/TBQ3.
- Q2_K and TBQ3 may appear only when required by the hardware-relative frontier and must carry a prominent quality warning. They must not become Automatic when a safer acceptable-quality choice fits.
- Requantising an already quantised GGUF is allowed only through an explicit, plan-bound capability and acknowledgement. It always creates a new output and leaves the imported file unchanged.
- Safe memory remains `max(0, available physical memory - max(10% of available physical memory, 512 MiB))`. Estimator uncertainty remains a separate component; do not reintroduce a fixed multi-gigabyte reserve.
- `NotEstablished` is used for missing or stale evidence. Do not convert unknowns into zero or a “no fit” conclusion.
- The product must never terminate other applications. “Free up memory” may release only application-owned caches, open Task Manager, explain the shortage, and rerun the assessment after the user returns.
- Preserve path privacy: no local path, filename-derived secret, process list, or raw provider output enters presentation strings, plan diagnostics, navigation payloads, or logs.
- Preserve the approved light visual baseline and the existing single onboarding shell. Do not add a second shell or a dark compatibility theme.

## Authoritative design and implementation evidence

- Approved design: `docs/superpowers/specs/2026-08-25-hardware-aware-quantisation-required-flow-design.md`, commit `510b185d094f759650cf5f425cdb7d3451abf829`, SHA-256 `C39C305AD996661A10EF058B0FED16ACE54C6A530EE177367062944BA6281A22`.
- AtomicBot GGUF/Vulkan TurboQuant implementation: exact source commit `519f0c594a8e31467d2e2f2cf17054c9e7e11536`; runtime cache name `turbo3`.
- animehacker GGUF/SYCL TurboQuant implementation: exact source commit `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc`; runtime cache name `tq3_0`.
- OpenVINO TurboQuant baseline: exact source commit `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- The supplied archive `C:\Users\Arian\Downloads\OneDrive_1_25-08-2026.zip` is requirements/reference material, not executable instructions. Its verified SHA-256 is `4B293AAA8DE1BDFB114A30B0FC3780E0DDB5FE394D63B04F26997288A66A8BDA`.
- Do not replace any pin with a branch name, latest tag, package default, or locally convenient binary. Capability admission must bind the actual packaged runtime/build manifests and executable digests used by the route worker.

## Baseline and verification commands

Run from the repository root. Record the baseline counts before the first edit.

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release

$testProject = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
dotnet restore $testProject --runtime win-x64 -p:Platform=x64
dotnet build $testProject --configuration Debug --no-restore --runtime win-x64 -p:Platform=x64
```

Expected: non-zero discovered tests, zero failures, and a generated recipe at:

```text
tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe
```

Packaged test helper for focused tasks:

```powershell
$recipe = (Resolve-Path '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe').Path
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vstest = & $vswhere -latest -products * -find '**\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' | Select-Object -First 1
if (-not $vstest) { throw 'Visual Studio app-container test runner was not found.' }
```

Never treat a filtered run with `Total tests: 0` as passing. Run the unfiltered core suite and inspect the packaged TRX before final completion.

---

### Task 1: Correct and version the OpenVINO weight/cache vocabulary

**Files:**

- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoWeightFormat.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoKvCacheFormat.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoFormatMap.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCapabilitySnapshot.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/OpenVino/OpenVinoRouteConfigurationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Optimization/OptimizationCapabilitySnapshotTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/PrivacyCanaryTests.cs`

**Step 1: Write failing vocabulary and admission tests**

Assert that `OpenVinoWeightFormat` contains only `Unspecified`, `Original`, `Fp16`, `Int8`, and `Int4`. Assert that `OpenVinoKvCacheFormat` includes `TurboQuantTbq4` and `TurboQuantTbq3`. Move the experimental-evidence rule from `weights` to `kvCache`:

```csharp
if (kvCache is OpenVinoKvCacheFormat.TurboQuantTbq4
        or OpenVinoKvCacheFormat.TurboQuantTbq3
    && level != SupportLevel.Experimental)
{
    throw new ArgumentException(
        "TurboQuant KV-cache configurations require experimental evidence.",
        nameof(level));
}
```

Also assert that U4/TBQ4/TBQ3 cache estimates decrease monotonically without changing weight bytes.

**Step 2: Run the focused tests and observe RED**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~OpenVinoRouteConfigurationTests|FullyQualifiedName~OptimizationCapabilitySnapshotTests'
```

Expected: failures because TurboQuant is still a weight enum and absent from the cache enum.

**Step 3: Implement the smallest semantic correction**

- Remove the two TurboQuant members from `OpenVinoWeightFormat`.
- Add them to `OpenVinoKvCacheFormat` after U4.
- Remove TurboQuant weight bit-width and persistent-conversion mappings from `OpenVinoFormatMap`.
- Add cache-byte mappings using route evidence; do not invent a bit-width when the pinned implementation describes a block layout.
- Gate experimental support on the cache member and exact evidence ID.
- Update the privacy canary only for deliberate new string-carrying members.

**Step 4: Run GREEN and commit**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~OpenVinoRouteConfigurationTests|FullyQualifiedName~OptimizationCapabilitySnapshotTests|FullyQualifiedName~PrivacyCanaryTests'
git diff --check
git add -- 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino' 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCapabilitySnapshot.cs' 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests'
git commit -m 'fix(compatibility): model TurboQuant as OpenVINO KV cache'
```

### Task 2: Publish a digest-safe v3 OpenVINO execution payload

**Files:**

- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationExecutionPlan.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Execution/OpenVinoExecutionPayload.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Execution/OptimizationExecutionPayload.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCanonicalizer.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationPlanIssuer.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/OptimizationExecutionContractV2Tests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/OptimizationExecutionContractV3Tests.cs`

**Step 1: Freeze v2 before changing it**

Add golden-vector tests that construct every existing v2 OpenVINO payload and assert its canonical bytes and SHA-256. These vectors are immutable regression evidence. Do not update them after implementation.

**Step 2: Write failing v3 tests**

The v3 payload must distinguish cache precision from cache algorithm:

```csharp
public enum OpenVinoKvCacheAlgorithm
{
    Released = 1,
    TurboQuant = 2
}

public enum OpenVinoKvCachePrecision
{
    ReleasedDefault = 1,
    F16 = 2,
    Bf16 = 3,
    U8 = 4,
    U4 = 5,
    Tbq4 = 6,
    Tbq3 = 7
}
```

Test that:

- TBQ4/TBQ3 require `OpenVinoKvCacheAlgorithm.TurboQuant` plus the exact `TurboQuantBuildIdentity`.
- Released precisions forbid a TurboQuant identity.
- All cache fields change `ConfigurationSha256`.
- A v2 executor refuses v3.
- Frozen v2 canonical vectors remain unchanged.

**Step 3: Implement explicit contract-version canonicalisation**

Do not make the canonicalizer read a global version constant and silently reinterpret old fields. Pass the plan version into the canonicalizer and branch explicitly:

```csharp
return contractVersion switch
{
    2 => CanonicalizeV2(...),
    3 => CanonicalizeV3(...),
    _ => throw new ArgumentOutOfRangeException(nameof(contractVersion))
};
```

Set newly issued plans to v3. Preserve a read/verification path for v2 but do not issue new v2 plans. Append v3 cache algorithm, precision, and TurboQuant identity in fixed ordinal order.

**Step 4: Make issuer agreement exact**

`OptimizationPlanIssuer.RequireOpenVinoAgreement` must map the route cache to both payload fields and reject any disagreement. It must not infer a TurboQuant build from a format.

**Step 5: Run GREEN and commit**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~OptimizationExecutionContractV2Tests|FullyQualifiedName~OptimizationExecutionContractV3Tests|FullyQualifiedName~OptimizationPlanBindingTests'
git diff --check
git add -- 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization' 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants'
git commit -m 'feat(compatibility): version OpenVINO cache execution contract'
```

### Task 3: Add GGUF Q2_K and controlled requantisation

**Files:**

- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufWeightFormat.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufWeightFormatMap.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateGenerator.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationCapabilitySnapshot.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/CrossRouteCandidateGenerator.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/GgufRequantisationPolicy.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Candidates/CandidateGeneratorTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Candidates/CrossRouteCandidateGeneratorTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/OptimizationPreferenceInvariantTests.cs`

**Step 1: Write failing Q2_K and source-policy tests**

Cover these cases:

- Q2_K maps to `WeightQuantisation.Q2_K` and its estimator bit width.
- Q2_K is excluded when workload minimum quality is above `Poor`.
- A genuine higher-precision source permits ordinary conversion.
- An already quantised source without higher-precision source remains excluded by default.
- It becomes eligible only when all controlled-requantisation fields are present: exact quantiser identity/hash, source digest/length, explicit acknowledgement, new-output requirement, and original-preservation rule.
- No path is stored in the policy or returned exclusion.

Suggested shape:

```csharp
public sealed record GgufRequantisationPolicy(
    bool ExplicitlyAcknowledged,
    bool PreserveOriginal,
    bool RequireNewOutput,
    string EvidenceId);
```

Bind the policy to the execution plan; do not treat a UI checkbox alone as authorization.

**Step 2: Run RED**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~CandidateGeneratorTests|FullyQualifiedName~CrossRouteCandidateGeneratorTests|FullyQualifiedName~OptimizationPreferenceInvariantTests'
```

**Step 3: Implement Q2_K and the narrow exception**

- Add `Q2K` as the final `GgufWeightFormat` member.
- Map it to `WeightQuantisation.Q2_K` and `OptimizationAssessment.Poor`.
- Keep the existing default fail-closed rule in `TryResolvePreparationKind`.
- Add a separate controlled branch; do not globally relax `HasHigherPrecisionSource`.
- Require the generated candidate to create a persistent artifact and carry the quantiser identity in the v3 payload.

**Step 4: Protect Automatic and higher-quality modes**

Add invariants proving Automatic never selects Q2_K while any fitting `Acceptable`-or-better candidate exists. Maximum efficiency may select Q2_K only when it is the lowest-memory admitted candidate and the UI warning flag is true.

**Step 5: Run GREEN and commit**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~CandidateGeneratorTests|FullyQualifiedName~CrossRouteCandidateGeneratorTests|FullyQualifiedName~OptimizationPreferenceInvariantTests'
git diff --check
git add -- 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf' 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates' 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization' 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests'
git commit -m 'feat(compatibility): add controlled GGUF low-memory frontier'
```

### Task 4: Generate complete hardware-relative frontiers for both routes

**Files:**

- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/CrossRouteCandidateGenerator.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationPreferenceResolver.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/OpenVino/OpenVinoResourceEstimator.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Candidates/CrossRouteCandidateGeneratorTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Optimization/OptimizationPreferenceResolverTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/MetamorphicPropertyTests.cs`

**Step 1: Write failing cross-route matrix tests**

Build small fixture matrices proving:

- decreasing weight/cache precision never increases estimated memory for an otherwise identical complete configuration;
- cache compression changes KV bytes, not weight quality;
- weight compression changes weight bytes and quality;
- TBQ3 -> TBQ4 -> U4 -> U8 -> F16/BF16 fallback is considered only when exact capability evidence admits each member;
- GGUF TurboQuant implementations remain exact backend-specific capabilities (`turbo3` Vulkan and `tq3_0` SYCL), never generic “TurboQuant available”;
- missing estimates remain typed exclusions;
- the five manual bands are monotonic over the safe Pareto frontier.

**Step 2: Run RED**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~CrossRouteCandidateGeneratorTests|FullyQualifiedName~OptimizationPreferenceResolverTests|FullyQualifiedName~MetamorphicPropertyTests'
```

**Step 3: Implement objective-based selection**

Keep `SafeCandidateFrontier` and proportional band positions. Extend candidate metrics with presentation-safe quality warning data rather than fixed percentage targets:

```csharp
public enum OptimizationQualityNotice
{
    None = 0,
    SomeQualityReduction,
    NoticeableQualityReduction,
    SignificantQualityReduction
}
```

Derive the notice from the selected candidate’s evidence-backed coarse quality and whether it requantises. Do not claim measured perplexity or a numeric quality percentage.

**Step 4: Run GREEN and commit**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~CrossRouteCandidateGeneratorTests|FullyQualifiedName~OptimizationPreferenceResolverTests|FullyQualifiedName~MetamorphicPropertyTests'
git diff --check
git add -- 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core' 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests'
git commit -m 'feat(compatibility): rank hardware-relative optimisation modes'
```

### Task 5: Correct baseline-versus-alternative screen-state projection

**Files:**

- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityScreenProjection.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilitySetupView.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityOptimizationView.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityScreenProjectionTests.cs`

**Step 1: Write the four decision-table tests**

```text
baseline fits                         => EstimatedCompatible
baseline fails + admitted alternative fits => OptimisationRequired
baseline fails + no admitted alternative fits => NoEstimatedSafeConfiguration
baseline/alternative evidence unknown or stale => NotEstablished
```

Also retain a narrow-fit warning as a property/tone of the selected setup; do not overload `OptimisationRequired` with “only just fits.”

**Step 2: Add a flattened optimisation view**

The screen model must expose enough immutable information to render modes without re-running selection in WinUI:

```csharp
public sealed record CompatibilityOptimizationView(
    string RecommendedLabel,
    int? RecommendedSliderValue,
    IReadOnlyList<CompatibilityOptimizationModeView> Modes,
    bool RequiresPersistentArtifact,
    bool RequiresRequantisationAcknowledgement,
    OptimizationQualityNotice QualityNotice);
```

Use enum/code values where localisation owns wording. Do not expose paths, provider payloads, or mutable candidates.

**Step 3: Implement the decision table**

Identify the imported/current configuration explicitly from `CandidatePreparation.RuntimeProfileOnly` and its source-bound fingerprint. Do not infer baseline from list order. Identify safe alternatives from the admitted frontier.

**Step 4: Run GREEN and commit**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release --filter 'FullyQualifiedName~CompatibilityScreenProjectionTests'
git diff --check
git add -- 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation' 'tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation'
git commit -m 'fix(compatibility): distinguish quantisation-required outcomes'
```

### Task 6: Render the approved Optimisation required WinUI state

**Files:**

- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityPresentationFactory.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Presentation/CompatibilityTheme.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/DebugFixtures/CompatibilityFixtureCatalogue.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/Visual/CompatibilityRenderedStateTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityPresentationFactoryTests.cs`

**Step 1: Write failing presentation tests**

Require exact primary copy:

```text
This model needs to be quantised to run on your computer
Your current model needs more memory than this computer can safely spare. A smaller version can run here.
You can choose how you want to balance memory use and expected quality.
```

Require all five exact labels, separate Automatic choice, expected-quality text for every mode, a warning for Q2_K/TBQ3, and an explicit “new copy; original unchanged” warning for requantisation.

Add layout contract assertions for:

- light theme resources only;
- one centred responsive content column;
- no nested onboarding shell;
- no empty top card;
- minimum 44 px hit targets;
- readable 200% text scaling;
- mode rows with vertically centred labels/statuses;
- existing Model Download slider geometry/tokens reused rather than copied with divergent values.

**Step 2: Run packaged focused tests and observe RED**

```powershell
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Debug --runtime win-x64 -p:Platform=x64
& $vstest $recipe '/Platform:x64' '/TestCaseFilter:FullyQualifiedName~ModelHardwareCompatibility' '/Logger:trx;LogFileName=compatibility-red.trx'
```

**Step 3: Implement the stable visual tree**

- Keep the page header centred.
- Replace `OnlyJustFits` copy for `OptimisationRequired` with the approved quantisation-required copy.
- Add the mode selector into the existing page tree; do not navigate to a separate duplicate shell.
- Bind the slider to `OptimizationPreferenceSelection.Manual(value)` and Automatic to `OptimizationPreferenceSelection.Automatic()`.
- Show route-specific selected setup details (weight format, cache format, context, estimated peak, safe budget, headroom) without exposing backend implementation trivia as user copy.
- Primary action: `Choose optimisation`; secondary action: `Back`.
- Keep the currently approved light cards, spacing, button styles, and stepper.

**Step 4: Add fixture coverage**

Add fixtures for:

- GGUF Q4 current -> Q3 fits;
- GGUF current -> Q2_K only, with strong warning;
- GGUF requantisation, original unchanged;
- OpenVINO INT8 -> INT4 fits;
- OpenVINO cache-only U8/U4 improvement;
- experimental TBQ4 and TBQ3 opt-in;
- no safe candidate;
- unknown evidence;
- 8 GB constrained and 16 GB ordinary-memory examples.

**Step 5: Run GREEN and commit**

```powershell
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Debug --runtime win-x64 -p:Platform=x64
& $vstest $recipe '/Platform:x64' '/TestCaseFilter:FullyQualifiedName~ModelHardwareCompatibility' '/Logger:trx;LogFileName=compatibility-green.trx'
git diff --check
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility'
git commit -m 'feat(compatibility): show hardware-aware optimisation choices'
```

### Task 7: Add the safe memory-recovery action

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/ICompatibilityMemoryRecovery.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/WindowsCompatibilityMemoryRecovery.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityMemoryRecoveryTests.cs`

**Step 1: Write failing safety tests**

Require the recovery adapter to expose only:

```csharp
internal interface ICompatibilityMemoryRecovery
{
    Task ReleaseApplicationMemoryAsync(CancellationToken cancellationToken);
    Task OpenTaskManagerAsync(CancellationToken cancellationToken);
}
```

Tests must prove:

- it contains no process-enumeration or process-termination API;
- app-owned caches are cleared through registered callbacks;
- Task Manager is opened with `UseShellExecute = true` and no user-derived arguments;
- the compatibility check reruns after recovery;
- stale recovery/check results cannot overwrite a newer attempt;
- no process names or memory consumers enter presentation text or diagnostics.

**Step 2: Implement narrowly**

Do not use `Process.GetProcesses`, `Kill`, WMI termination, or elevation. `GC.Collect` may be used only after explicit app-owned cache release, never presented as reclaiming a promised amount of RAM.

**Step 3: Run GREEN and commit**

```powershell
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Debug --runtime win-x64 -p:Platform=x64
& $vstest $recipe '/Platform:x64' '/TestCaseFilter:FullyQualifiedName~CompatibilityMemoryRecoveryTests' '/Logger:trx;LogFileName=memory-recovery.trx'
git diff --check
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility'
git commit -m 'feat(compatibility): add safe guided memory recovery'
```

### Task 8: Produce the optimisation-selection handoff, without executing it

**Files:**

- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/OptimizationSelectionHandoff.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OptimizationSelectionHandoffTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingFlowTests.cs`

**Step 1: Write failing boundary tests**

The handoff carries identities and the immutable v3 plan, not a path or a mutable UI selection:

```csharp
internal sealed record OptimizationSelectionHandoff(
    string ModelInspectionRunId,
    string ModelInspectionHandoffId,
    string ProductHardwareRunId,
    Guid OptimizationPlanId,
    string ConfigurationSha256,
    OptimizationExecutionPlan Plan);
```

Require exact agreement among the handoff, plan binding, capability snapshot, source SHA-256/length, and route. A stale hardware snapshot or changed slider selection must require a newly issued plan.

**Step 2: Implement navigation only**

Raise one typed `OptimizationRequested` event and let `MainWindow` move to the existing optimisation destination if available. If the downstream screen is absent, keep the button visible-disabled with honest “Coming later” copy; do not create a fake executor or claim optimisation occurred.

Preserve one onboarding shell and advance from step 3 to step 4 exactly once.

**Step 3: Run GREEN and commit**

```powershell
dotnet build '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj' --configuration Debug --runtime win-x64 -p:Platform=x64
& $vstest $recipe '/Platform:x64' '/TestCaseFilter:FullyQualifiedName~OptimizationSelectionHandoffTests|FullyQualifiedName~Onboarding' '/Logger:trx;LogFileName=optimisation-handoff.trx'
git diff --check
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility' 'IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features'
git commit -m 'feat(onboarding): hand off selected optimisation plan'
```

### Task 9: Full regression, native visual review, and handoff

**Files:**

- Modify if needed: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/DebugFixtures/CompatibilityFixtureCatalogue.cs`
- Create: `docs/handoffs/2026-08-25-hardware-aware-quantisation-required-flow.md`

**Step 1: Run the complete core suite**

```powershell
dotnet test --project '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj' --configuration Release
```

Expected: non-zero discovered count and zero failed tests.

**Step 2: Build and run the complete packaged suite**

```powershell
$testProject = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
dotnet restore $testProject --runtime win-x64 -p:Platform=x64
dotnet build $testProject --configuration Debug --no-restore --runtime win-x64 -p:Platform=x64

$resultDirectory = '.\TestResults\HardwareAwareOptimisation\Debug'
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$resultsPath = (Resolve-Path $resultDirectory).Path
& $vstest $recipe '/Platform:x64' '/Logger:trx;LogFileName=full.trx' "/ResultsDirectory:$resultsPath"
```

Inspect the TRX: non-zero total, zero failed, and no unexpected skips in ModelHardwareCompatibility or Onboarding.

**Step 3: Run the fixture gallery and inspect native WinUI**

Build the app with `-p:CompatibilityFixtureGallery=true`. Review every fixture at normal scale and 200% text scale, maximised and at the supported minimum window size. Confirm:

- light background and centred content;
- one onboarding shell;
- no clipped card or footer;
- exact approved copy and mode labels;
- Q2_K/TBQ3 warning prominence;
- vertically centred icons, labels, and status text;
- no empty cards;
- keyboard order, visible focus, screen-reader names, and 44 px targets;
- memory figures reconcile: weight + KV cache + runtime/buffers + uncertainty = estimated peak; safe budget is shown separately.

**Step 4: Check scope and privacy**

```powershell
rg -n "Process\.GetProcesses|\.Kill\(|TerminateProcess|ManagementObjectSearcher" 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility'
rg -n "[A-Za-z]:\\|Users\\|Downloads\\" 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility' 'shared/GraniteEdgeAI.ModelHardwareCompatibility.Core'
git diff --check
git status --short
```

Expected: no termination API; no newly introduced path-bearing user output; clean diff check.

**Step 5: Write the handoff and commit**

Record branch/base/tip, changed paths, exact test commands/counts, TRX path, fixture-gallery review, v2 golden-vector result, v3 contract shape, known experimental TurboQuant gates, and the explicit downstream boundary: no optimiser execution, progress screen, export, or chat was implemented in this increment.

```powershell
git add -- 'docs/handoffs/2026-08-25-hardware-aware-quantisation-required-flow.md'
git commit -m 'docs(compatibility): hand off quantisation-required flow'
git status --short
```

Expected: clean worktree after the final commit.

---

## Completion definition

This plan is complete only when all of the following are true:

1. The imported/current setup is evaluated separately from alternatives.
2. Current-fails/alternative-fits produces the exact quantisation-required message.
3. Current-fits never pushes the user into unnecessary quantisation.
4. No-fit and unknown-evidence remain distinct.
5. Both GGUF and OpenVINO produce complete, capability-admitted frontiers.
6. TurboQuant is represented as cache compression with exact evidence and experimental gating.
7. Q2_K and requantisation are controlled, warned, plan-bound fallbacks.
8. Every mode uses the existing Model Download label and states expected quality.
9. The screen matches the approved light, centred, single-shell visual system.
10. Guided memory recovery never terminates another process.
11. The typed v3 optimisation-selection handoff is ready for the downstream optimisation screen.
12. Core and packaged suites pass with non-zero discovered counts, the native gallery is reviewed, and the worktree is clean.
