# A1 R3 Production Reachability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close R3-017 by replacing three duplicated inline composition paths with the existing A1 backend authorities and proving each has exactly one production registration.

**Architecture:** `OnboardingShellPage` will delegate compatibility evaluation and optimization construction to the existing A1 orchestrator/factory, while `ModelInspectionServiceComposition` will delegate official worker installation construction to the existing worker authority. No new facade, service locator, public contract, or route implementation is introduced; the inline duplicate owners are removed.

**Tech Stack:** C# 13, .NET 8/10 SDK tooling, WinUI 3, MSTest/Microsoft.Testing.Platform, PowerShell, Git.

**Spec:** `docs/superpowers/specs/2026-08-29-a1-r3-production-reachability-design.md`

## Global Constraints

- Start from commit `66e2a4a5a4509962cb3faee410c69c5d764e0854`, tree `dd4c9eef755e126ee6fab3a901fec56ddc267ab5`, on `audit/ucl-a1-remediation-r3`; never rewrite `audit/ucl-a1-remediation-r2`.
- Own R3-017. Support R3-018 only through production-composition verification; do not claim F1/Q1/T1 behavior.
- Change only the three approved composition files, focused app tests, a committed A1 verification driver, the A1 R3 report, and this plan/spec.
- Preserve public contracts, fail-closed behavior, exact manifest digest and byte validation, bounded streaming, cancellation/timeout, path custody, cleanup, original-model preservation, and native integrity.
- Do not add a second GGUF/OpenVINO implementation or a second registration.
- Never weaken, delete, rename, skip, whitelist, or narrow an existing failing test.
- Every behavioral production change follows RED, GREEN, refactor. Structural reachability checks supplement behavioral tests because exact caller count is the architectural defect.
- Receipt arithmetic is `executed = passed + failed + skipped` and `discovered >= executed`.
- No committed report, driver output, or receipt may contain a username, hostname, absolute worktree/evidence path, user filename, raw provider output, credential, token, prompt, or model data.
- Native disposition is `not-applicable` unless a separately authorized native stage actually runs.

---

### Task 1: Preflight the isolated R3 baseline

**Files:**
- Inspect: `global.json`
- Inspect: `docs/handoffs/2026-08-27-ucl-cross-route-native-validation-final-handoff.md`
- No repository edits

**Interfaces:**
- Consumes: R3 base commit/tree and the local .NET/Visual Studio toolchain.
- Produces: a recorded clean baseline with adequate disk, valid ancestry, known package/native availability, and executable test paths.

- [ ] **Step 1: Verify Git identity and isolation**

Run:

```powershell
$base = '66e2a4a5a4509962cb3faee410c69c5d764e0854'
$expectedTree = 'dd4c9eef755e126ee6fab3a901fec56ddc267ab5'
git rev-parse HEAD
git show -s --format=%T HEAD
git branch --show-current
git merge-base --is-ancestor a5ef3558334e50587889140dafba194853938765 $base
git status --porcelain
```

Expected: exact base commit/tree, branch `audit/ucl-a1-remediation-r3` or the committed design descendant, ancestry exit 0, and clean status.

- [ ] **Step 2: Verify disk, SDK, runtimes, test buildability, and native lock**

Run:

```powershell
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
Get-PSDrive -PSProvider FileSystem |
    Select-Object Name,@{N='FreeGB';E={[math]::Round($_.Free / 1GB, 2)}}
& $dotnet --list-sdks
& $dotnet --list-runtimes
Get-Content global.json
rg -n 'UCL-NATIVE-LOCK:' docs/handoffs
Test-Path 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
Test-Path 'tests\UnitTests\GraniteEdgeAI.OpenVino.WorkerClient.Tests\GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj'
```

Expected: at least 4 GiB free, .NET 8 runtime and a usable .NET 10 SDK, both projects present, and the last authoritative native lock is `IDLE`. If any expectation fails, stop and record the precise blocker without deleting other worktrees or build trees.

- [ ] **Step 3: Establish the baseline suite result**

Build the WorkerClient, compatibility, cross-feature, and x64 app unit projects using the available SDK selector. Run their complete test executables/packaged recipe with non-zero minimum discovery. Record the baseline counts separately from R3 evidence. Any product failure is investigated before proceeding; zero discovery is rejected.

---

### Task 2: Add RED production-reachability and cancellation regressions

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingCompatibilityNavigationTests.cs`

**Interfaces:**
- Consumes: `OptimizationImportManifestTests.FindRepositoryRoot()`, the real onboarding compatibility flow, and existing R2 production files.
- Produces: four regressions that fail on R2 because all three production registrations are missing and inline compatibility swallows an unrequested cancellation.

- [ ] **Step 1: Add the architectural reachability fitness tests**

Create this test class:

```csharp
using GraniteEdgeAI.UnitTests.Features.ModelOptimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

[TestClass]
public sealed class A1BackendProductionReachabilityTests
{
    [TestMethod]
    public void CompatibilityOrchestrator_HasExactlyOneProductionRegistration()
    {
        string source = ReadProduction(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");

        Assert.AreEqual(1, Occurrences(
            source, "new CompatibilityEvaluationOrchestrator("));
    }

    [TestMethod]
    public void OptimizationFactory_HasExactlyOneProductionRegistration()
    {
        string source = ReadProduction(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");

        Assert.AreEqual(1, Occurrences(
            source, "new OptimizationBackendCompositionFactory("));
    }

    [TestMethod]
    public void OfficialWorkerAuthority_HasOneRegistrationAndNoDuplicateInventory()
    {
        string source = ReadProduction(
            "Features", "ModelInspection", "Services",
            "ModelInspectionServiceComposition.cs");

        Assert.AreEqual(1, Occurrences(
            source, "OpenVinoOfficialWorkerAuthority.CreateInstallation("));
        Assert.AreEqual(0, Occurrences(source, "OfficialBinaryMachines("));
        Assert.AreEqual(0, Occurrences(
            source, "OpenVinoWorkerInstallation installation = new("));
    }

    private static string ReadProduction(params string[] relativePath) =>
        File.ReadAllText(Path.Combine(
            OptimizationImportManifestTests.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            Path.Combine(relativePath)));

    private static int Occurrences(string source, string value)
    {
        int count = 0;
        for (int offset = 0;
             (offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0;
             offset += value.Length)
        {
            count++;
        }
        return count;
    }
}
```

- [ ] **Step 2: Add the behavioral cancellation regression**

Add to `OnboardingCompatibilityNavigationTests`:

```csharp
[UITestMethod]
[TestCategory("WinUI")]
public async Task CompatibilityEvaluation_DoesNotConvertDependencyCancellationToFallback()
{
    ModelInspectionExecutionResult terminal = Terminal();
    ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
    ModelInspectionPage source = CreateSourcePage();
    var shell = new OnboardingShellPage(
        static (frame, request) => frame.Navigate(typeof(ModelInspectionPage), request),
        new CountingHardwareService(), null, null,
        new CancellingFreshResourcesSource(),
        (_, _) => terminal,
        (frame, page) =>
        {
            page.StartAutomatically = false;
            frame.Content = page;
            return true;
        });
    shell.AttachModelInspectionPage(source);
    Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
    var frame = (Frame)shell.FindName("StageFrame");
    var hardwarePage = (HardwareInspectionPage)frame.Content;
    HardwareInspectionHandoff hardwareHandoff = HardwareInspectionHandoff.Create(
        shell.CurrentProductHardwareRunId,
        HardwareInspectionOutcome.Completed,
        HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());
    Assert.IsTrue(await shell.NavigateToCompatibilityAsync(
        hardwarePage,
        new HardwareInspectionCompletedEventArgs(hardwareHandoff)));
    var compatibilityPage = (CompatibilityPage)frame.Content;

    await Assert.ThrowsExactlyAsync<OperationCanceledException>(
        compatibilityPage.ViewModel.StartAsync);
}

private sealed class CancellingFreshResourcesSource
    : ICompatibilityFreshResourcesSource
{
    public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromException<CompatibilityFreshResourcesInput>(
            new OperationCanceledException("dependency cancelled"));
}
```

- [ ] **Step 3: Run the four tests and retain RED output**

Build the x64 Release packaged unit host, then run:

```powershell
$filter = 'FullyQualifiedName~A1BackendProductionReachabilityTests|FullyQualifiedName~CompatibilityEvaluation_DoesNotConvertDependencyCancellationToFallback'
& $vstest $recipe "/TestCaseFilter:$filter" "/Logger:trx;LogFileName=a1-r3-red.trx" "/ResultsDirectory:$results"
```

Expected: four executed failures. The three registration counts are 0 (and duplicate worker inventory remains present); the cancellation test reports no thrown `OperationCanceledException` because R2 converted it to fallback. A compilation error, zero discovery, or unrelated failure is not accepted as RED.

- [ ] **Step 4: Commit the RED regressions**

```powershell
git add -- 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingCompatibilityNavigationTests.cs'
git commit -m 'test(backend): expose R3 production reachability gap'
```

---

### Task 3: Wire compatibility evaluation into production

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs:683-803`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingCompatibilityNavigationTests.cs`

**Interfaces:**
- Consumes: `CompatibilityEvaluationOrchestrator.EvaluateAuthorityAsync`, `CompatibilityEvaluationOrchestrator.EvaluateBoundAsync`, existing prepared GGUF/OpenVINO inputs, and `TimeProvider.System`.
- Produces: one production orchestrator registration and cancellation-preserving evaluation delegates for all three compatibility branches.

- [ ] **Step 1: Construct one orchestrator at the production boundary**

Immediately before the compatibility route branch, add:

```csharp
var evaluator = new CompatibilityEvaluationOrchestrator(
    _compatibilityFreshResourcesSource,
    TimeProvider.System);
```

- [ ] **Step 2: Replace both production-authority lambdas**

Use these delegates in the GGUF and OpenVINO `CompatibilityPage` constructors:

```csharp
(optedInEvidence, token) => evaluator.EvaluateAuthorityAsync(
    optedInEvidence,
    production!.Evaluate,
    token)
```

```csharp
(optedInEvidence, token) => evaluator.EvaluateAuthorityAsync(
    optedInEvidence,
    openVinoProduction!.Evaluate,
    token)
```

- [ ] **Step 3: Replace the bound fallback lambda**

Use:

```csharp
token => evaluator.EvaluateBoundAsync(
    (CompatibilityFreshResourcesInput fresh,
        out CompatibilityProductionInput? input) =>
        hasGguf
            ? preparedGguf!.TryBindFresh(fresh, out input)
            : preparedOpenVino!.TryBindFresh(fresh, out input),
    token)
```

Delete the three displaced capture/`Task.Run`/catch bodies. Do not add another fallback catch in the shell.

- [ ] **Step 4: Run compatibility registration and cancellation GREEN**

Run the identical packaged filter from Task 2, restricted to the compatibility registration and cancellation methods, plus all `CompatibilityEvaluationOrchestratorTests`.

Expected: non-zero execution and all selected tests pass.

- [ ] **Step 5: Commit the compatibility composition**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs'
git commit -m 'refactor(backend): compose compatibility orchestrator'
```

---

### Task 4: Wire route-exact optimization composition

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs:884-961`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationBackendCompositionFactoryTests.cs`

**Interfaces:**
- Consumes: `OptimizationBackendCompositionFactory.TryCreate(OptimizationJourneyEntryContext, out OptimizationBackendComposition?)`.
- Produces: exactly one route factory registration and the existing shell-owned active coordinator/context/output/executor references.

- [ ] **Step 1: Replace direct backend construction**

Keep the existing `appRoot` calculation, then replace direct route construction with:

```csharp
var factory = new OptimizationBackendCompositionFactory(
    _modelSourceCustodyRegistry,
    appRoot,
    _activeGgufAuthority,
    _activeOpenVinoAuthority,
    _activeOpenVinoOptimizationService,
    TimeProvider.System);
if (!factory.TryCreate(
        entry,
        out OptimizationBackendComposition? backend))
{
    return;
}

_activeOpenVinoExecutor = backend.Executor as OpenVinoOptimizationExecutor;
OptimizationJourneyCoordinator coordinator = backend.Coordinator;
IOptimizationAttemptContextFactory contextFactory = backend.ContextFactory;
OptimizationOutputRegistry outputs = backend.Outputs;
```

Delete direct GGUF/OpenVINO runner, executor, revalidator, context factory, output registry, router, and coordinator construction. Leave page/event/state assignment unchanged.

- [ ] **Step 2: Run factory registration and complete factory behavior GREEN**

Run the packaged registration method plus all `OptimizationBackendCompositionFactoryTests`.

Expected: exactly one production registration and all route/missing/duplicate/mismatch behavior tests pass.

- [ ] **Step 3: Run affected onboarding optimization/journey tests**

Run all packaged tests whose fully qualified names contain `OptimizationJourney`, `OptimizationBackend`, `GgufOptimizationExecutor`, or `Onboarding`.

Expected: non-zero execution and no failures/skips introduced by this change.

- [ ] **Step 4: Commit the optimization composition**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs'
git commit -m 'refactor(backend): compose route-exact optimization backend'
```

---

### Task 5: Wire the official OpenVINO worker authority

**Files:**
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionServiceComposition.cs:49-170`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/A1BackendProductionReachabilityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/OpenVinoOfficialWorkerAuthorityTests.cs`

**Interfaces:**
- Consumes: `OpenVinoOfficialWorkerAuthority.CreateInstallation(string approvedWorkerRoot, string expectedManifestDigest)` and `OpenVinoWorkerInstallation.ExpectedBuildEvidence`.
- Produces: exactly one production official-worker authority registration after the unchanged packaged-manifest digest comparison.

- [ ] **Step 1: Replace duplicated worker construction**

After the existing manifest digest comparison, use:

```csharp
OpenVinoWorkerInstallation installation =
    OpenVinoOfficialWorkerAuthority.CreateInstallation(
        workerRoot,
        expectedManifestDigest);
OpenVinoWorkerClient client = new(
    OpenVinoWorkerClientOptions.CreateDefault(installation));
return new OpenVinoRouteService(
    client,
    installation.ExpectedBuildEvidence);
```

Delete the local `OpenVinoBuildEvidence` literals and the complete `OfficialBinaryMachines` method. Remove only imports that become unused.

- [ ] **Step 2: Run worker registration and authority behavior GREEN**

Run the packaged `OfficialWorkerAuthority_HasOneRegistrationAndNoDuplicateInventory` test and the complete standalone WorkerClient test executable with `--minimum-expected-tests 16` or the newly discovered higher count.

Expected: one registration, no duplicate inventory, and every WorkerClient test passes.

- [ ] **Step 3: Run affected Model Inspection contract/unit tests**

Run the complete Model Inspection contracts project and the packaged Model Inspection service/composition filters. Do not treat guarded native tests as passes; record declared skips separately.

- [ ] **Step 4: Commit the worker authority composition**

```powershell
git add -- 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Services/ModelInspectionServiceComposition.cs'
git commit -m 'refactor(openvino): compose official worker authority'
```

---

### Task 6: Full verification and review

**Files:**
- Verify all changed production/test/driver files
- No report edit until test evidence is final

**Interfaces:**
- Consumes: committed R3 implementation and the standard build/test entry points committed in the repository.
- Produces: fresh affected-suite counts and hashes bound to one implementation subject commit/tree.

- [ ] **Step 1: Run the committed affected test entry points**

Run the complete WorkerClient, compatibility, and cross-feature test executables plus the packaged R3/onboarding/optimization/inspection filters. Store TRX output beneath the ignored `TestResults/Audit-20260829/A1-R3/` directory. Record the exact repository-relative command lines in the report so every row is reproducible from committed projects and tests. Reject zero-discovery, build-only, mock-only, blocked, or skipped rows as passing behavior.

- [ ] **Step 2: Run build/package checks**

Confirm zero warnings/errors for changed backend projects. Build the Release x64 application and packaged unit recipe. Record inherited warnings separately; do not suppress them or claim them as A1 passes.

- [ ] **Step 3: Run architecture/security/privacy scans**

```powershell
git diff --check 66e2a4a5a4509962cb3faee410c69c5d764e0854..HEAD
rg -n 'new CompatibilityEvaluationOrchestrator\(' 'IBM Granite with TurboQuant (Intel)'
rg -n 'new OptimizationBackendCompositionFactory\(' 'IBM Granite with TurboQuant (Intel)'
rg -n 'OpenVinoOfficialWorkerAuthority\.CreateInstallation\(' 'IBM Granite with TurboQuant (Intel)'
rg -n 'OfficialBinaryMachines|OpenVinoWorkerInstallation installation = new' 'IBM Granite with TurboQuant (Intel)'
git diff --unified=0 66e2a4a5a4509962cb3faee410c69c5d764e0854..HEAD |
    Select-String -Pattern 'C:\\Users\\|BEGIN (RSA|OPENSSH|EC|DSA) PRIVATE KEY|(?i)(api[_-]?key|password|secret)\s*[:=]'
```

Expected: exactly one production registration for each A1 component, zero displaced authority, zero prohibited path/secret hits, and clean diff checks.

- [ ] **Step 4: Perform a fresh self-review**

Review every base-to-tip production diff for ownership, cancellation, fallback, route authority, hashes, cleanup, paths, nullable warnings, and public-contract changes. Fix only confirmed A1 defects through a new RED/GREEN cycle and rerun affected checks.

- [ ] **Step 5: Commit any verification-only corrections**

Do not amend implementation evidence after it has been reported. If no correction is needed, retain the existing focused commits.

---

### Task 7: Report, final-tip verification, push, and receipt

**Files:**
- Create: `docs/audits/2026-08-29/A1-backend-architecture-clean-code-r3.md`
- Atomically publish outside the repository: `A1.json` in the designated handoff directory

**Interfaces:**
- Consumes: final TRX files generated from committed test entry points, exact Git objects, report bytes/hash, remote ref, and supplied receipt schema.
- Produces: a clean pushed A1 R3 branch and a schema-valid receipt with `evidenceManifest: null`.

- [ ] **Step 1: Write the reconciliation report**

The report lists R3-017 as owned and closed only if all three callers are present and green. It lists R3-018 as supported, identifies R3-008/R3-014 and all other ledger items as external, includes definition/caller/registration/test mappings, RED and GREEN evidence, complete non-zero totals, build/package results, native disposition, reproducibility command, security/privacy scans, and nonclaims.

- [ ] **Step 2: Commit the report**

```powershell
git add -- 'docs/audits/2026-08-29/A1-backend-architecture-clean-code-r3.md'
git diff --cached --check
git commit -m 'docs(audit): report A1 R3 production reachability'
```

- [ ] **Step 3: Re-run the affected test entry points on the final tip**

The final test evidence must record the report commit as its tested commit/tree. Re-run the same repository-relative commands recorded in the report, recompute all totals and hashes from fresh TRX files, and confirm the worktree remains clean because results are ignored.

- [ ] **Step 4: Compute final handoff facts externally**

```powershell
git rev-parse HEAD
git show -s --format=%T HEAD
git status --porcelain
Get-FileHash -Algorithm SHA256 'docs/audits/2026-08-29/A1-backend-architecture-clean-code-r3.md'
(Get-Item 'docs/audits/2026-08-29/A1-backend-architecture-clean-code-r3.md').Length
$worktreeBlob = git hash-object 'docs/audits/2026-08-29/A1-backend-architecture-clean-code-r3.md'
$committedBlob = git rev-parse 'HEAD:docs/audits/2026-08-29/A1-backend-architecture-clean-code-r3.md'
if ($worktreeBlob -ne $committedBlob) { throw 'Report bytes differ from the committed Git blob.' }
```

Verify the worktree file bytes equal the Git blob bytes.

- [ ] **Step 5: Push only the worker branch and verify transport**

```powershell
git push -u origin audit/ucl-a1-remediation-r3
git ls-remote origin refs/heads/audit/ucl-a1-remediation-r3
```

Expected: remote object equals local final tip. Never merge or push main. If push is unavailable after bounded retries, create and verify the authority-prescribed bundle.

- [ ] **Step 6: Validate and atomically publish the receipt**

Create a temporary sibling receipt containing exact frozen/base/final identities, report record, remote ref or bundle, `evidenceManifest: null`, arithmetic-checked totals, and `nativeDisposition: not-applicable`. Validate it against the supplied handoff schema, recompute every fact, verify the remote again, then rename the temporary sibling over `A1.json` only after every check succeeds.

- [ ] **Step 7: Final integrity check**

Verify the published receipt again, confirm its report hash/bytes and final tip/tree against Git, confirm the remote ref, confirm clean status, and state explicitly that main was neither merged nor pushed.
