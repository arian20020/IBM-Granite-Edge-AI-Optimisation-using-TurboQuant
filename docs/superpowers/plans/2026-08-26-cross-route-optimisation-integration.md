# Cross-route Optimisation Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete one production journey from the existing compatibility decision through route-correct GGUF or OpenVINO optimisation, Chat, and safe local Save without duplicating shared UI, orchestration, contracts, navigation, or registrations.

**Design:** `docs/superpowers/specs/2026-08-26-cross-route-optimisation-integration-design.md`

**Architecture:** Compatibility remains the only preference and v3 plan-issuance owner. A single post-selection `ModelOptimization` page and generation-safe coordinator dispatch exact plans to thin GGUF/OpenVINO adapters; private source/output registries own paths and atomic publication, while typed path-free handoffs drive navigation. Route code is imported through immutable path/blob manifests instead of branch merges, and the current hardware-aware branch wins every shared-file conflict.

**Tech Stack:** C# 13, .NET 8/10 projects, WinUI 3, Windows App SDK 2.2, MSTest/Microsoft.Testing.Platform, Visual Studio packaged VSTest, PowerShell, llama.cpp, OpenVINO, Git.

---

## Working rules and immutable inputs

Implement in a new isolated worktree created from the committed tip of `fix/hardware-inspection-loq-baseline`. Before creating it, verify that the branch contains planning-base commit `092589c38981ad86bb73c7c97dff01ab8b5a6c8e`, which is immediately above authoritative feature base `85e889fa18f73cc19780b2af03598d1b76ac0e21`. The final corrected design, plan, and six visual-oracle files are authoritative together only at the later immutable planning handoff commit supplied through `GEAI_EXPECTED_PLAN_COMMIT`; do not infer it from a moving branch, prose, or the worker's checkout.

The execution environment must also supply `GEAI_SOURCE_REPOSITORY` as the resolved absolute path of a clean local application Git repository that contains the first five exact application authority commits in the table. The external llama.cpp source commit is independently verified inside the operation-owned clone in Task 10A. Treat the local application path as operational input: validate it with `Resolve-Path`, `git rev-parse --show-toplevel`, and `git cat-file -e <sha>^{commit}` for the five application SHAs; never write it into a public handoff, presentation model, navigation payload, diagnostic, log, screenshot, or committed evidence document.

| Input | Exact authority |
|---|---|
| Current hardware-aware application | committed tip of `fix/hardware-inspection-loq-baseline`, containing `092589c38981ad86bb73c7c97dff01ab8b5a6c8e` |
| Canonical post-selection UI | `origin/feature/cross-route-optimisation-ui-v1@8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8` |
| OpenVINO route | `origin/feature/openvino-optimisation-adapter-v1@f0189ed187ba900f27bade5fde282ae4e99e8d7b` |
| Historical v2.1 input | `origin/feature/cross-route-optimisation-contracts-v2-1@e254385997392601102b16acf19244437803bdcc` |
| Production GGUF Chat/runtime | `origin/feature/gguf-cli-chat-production@bacb3f4106e0191b05b870358342f8158765396d` |
| Official GGUF quantiser source | `https://github.com/ggml-org/llama.cpp.git@3f7c29d318e317b63f54c558bc69803963d7d88c` |

Do not merge any input branch. Do not import its `ModelImport`, `ModelInspection`, `HardwareInspection`, `ModelHardwareCompatibility`, `Onboarding`, `MainWindow`, `App`, solution, or project file wholesale. Apply shared project/resource/navigation edits manually against the current branch. Keep `OptimizationSelectionHandoff` at exactly six public get-only properties. Never put a path in a handoff, presentation model, navigation payload, diagnostic, support code, log, or test snapshot.

## Target file structure

The following paths lock ownership before implementation:

- Core planning authority: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Planning/**`
- Exact route payload composition port: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Execution/IOptimizationExecutionPayloadComposer.cs`
- Compatibility evaluation wrapper: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEvaluation.cs`
- Private imported-source custody: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelSourceCustodyRegistry.cs`
- Typed compatibility exits: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/**`
- One post-selection UI/coordinator: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/**`
- GGUF route-owned runtime: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/**`, `shared/GraniteEdgeAI.GgufRuntime.*`, and `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/**`
- OpenVINO route-owned implementation: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/**`, `shared/GraniteEdgeAI.OpenVino.Contracts/**`, and `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**`
- Route-neutral Model Inspection seam: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/IModelInspectionPageSession.cs` and route-owned session adapters; the existing `ModelInspectionPage.xaml` and theme remain the sole UI.
- Private OpenVINO package custody: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackageCustodyRegistry.cs`
- OpenVINO compatibility projection: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/OpenVinoCompatibilityInputProjector.cs`
- Shell integration extensions: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.ModelSourceCustody.cs`, `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.OpenVinoInspection.cs`, and `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.Optimization.cs`; `OnboardingShellPage.xaml.cs` receives only the three named calls into those partials.
- Shared import records: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json` (machine authority) and `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md` (review narrative)

The four waves are serial at their boundaries: shared authority/UI, GGUF, OpenVINO, then destinations/final integration. Within a wave, workers may edit only disjoint paths named by its tasks.

## Verification command block

Run these exact commands from the integration worktree. Every filtered test result used as evidence must report a discovered count greater than zero.

```powershell
$ErrorActionPreference = 'Stop'
$tp = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'
$core = '.\tests\UnitTests\GraniteEdgeAI.ModelHardwareCompatibility.Tests\GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj'
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'
$recipe = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'

dotnet test --project $core --configuration Release
dotnet restore $tp --runtime win-x64 -p:Platform=x64 --disable-build-servers -m:1
dotnet build $tp --configuration Debug --no-restore --runtime win-x64 -p:Platform=x64 -p:BuildInParallel=false --disable-build-servers -m:1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~ModelHardwareCompatibility'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~ModelOptimization'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~Onboarding'
git diff --check
```

Expected at every checkpoint: each command exits `0`, each filter discovers at least one test, and `git diff --check` prints nothing.

## Wave 1 — authoritative planning, shared UI, and coordinator

### Task 1: Create the isolated integration branch and immutable import ledger

**Files:**
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md`
- Modify: `docs/superpowers/plans/2026-08-26-cross-route-optimisation-integration.md`
- Modify: `scripts/verification/Test-CrossRouteImportManifest.ps1`
- Create: `scripts/verification/CrossRouteImportManifest.Core.psm1`
- Modify: `scripts/verification/Assert-ChangedPaths.ps1`
- Modify: `scripts/verification/Invoke-PackagedTestCheckpoint.ps1`
- Create: `scripts/verification/PackagedCheckpoint.Core.psm1`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationImportManifestTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/ChangedPathAllowlistTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/PackagedTestCheckpointTests.cs`

- [ ] **Step 1: Create and verify the worktree**

```powershell
git -C C:\GEAI-LOQ status --short
git -C C:\GEAI-LOQ merge-base --is-ancestor 092589c38981ad86bb73c7c97dff01ab8b5a6c8e fix/hardware-inspection-loq-baseline
$expectedPlanCommit = $env:GEAI_EXPECTED_PLAN_COMMIT
if ($expectedPlanCommit -notmatch '^[0-9a-f]{40}$') { throw 'GEAI_EXPECTED_PLAN_COMMIT must be the exact lowercase 40-hex planning handoff commit.' }
$sourceRepository = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $env:GEAI_SOURCE_REPOSITORY -ErrorAction Stop).Path).TrimEnd('\','/')
$gitSourceRoot = [IO.Path]::GetFullPath(((git -C $sourceRepository rev-parse --show-toplevel) -replace '/', [IO.Path]::DirectorySeparatorChar)).TrimEnd('\','/')
if (-not [StringComparer]::OrdinalIgnoreCase.Equals($gitSourceRoot, $sourceRepository)) { throw 'GEAI_SOURCE_REPOSITORY must be the exact repository root.' }
foreach ($sha in @('092589c38981ad86bb73c7c97dff01ab8b5a6c8e','8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8','f0189ed187ba900f27bade5fde282ae4e99e8d7b','e254385997392601102b16acf19244437803bdcc','bacb3f4106e0191b05b870358342f8158765396d')) { git -C $sourceRepository cat-file -e "$sha`^{commit}"; if ($LASTEXITCODE) { throw "Missing application authority commit $sha." } }
$planTip = git -C C:\GEAI-LOQ rev-parse fix/hardware-inspection-loq-baseline
if ($planTip -ne $expectedPlanCommit) { throw "Planning branch tip $planTip does not match approved plan commit $expectedPlanCommit." }
git -C C:\GEAI-LOQ worktree add C:\GEAI-XRoute -b feature/cross-route-optimisation-integration-v1 $expectedPlanCommit
git -C C:\GEAI-XRoute status --short
git -C C:\GEAI-XRoute fetch origin refs/heads/feature/cross-route-optimisation-ui-v1:refs/remotes/origin/feature/cross-route-optimisation-ui-v1 refs/heads/feature/openvino-optimisation-adapter-v1:refs/remotes/origin/feature/openvino-optimisation-adapter-v1 refs/heads/feature/cross-route-optimisation-contracts-v2-1:refs/remotes/origin/feature/cross-route-optimisation-contracts-v2-1 refs/heads/feature/gguf-cli-chat-production:refs/remotes/origin/feature/gguf-cli-chat-production
$oracles = @{
  'docs/ux/visual-oracles/model-inspection-balanced-full-approval-v3.html'='d44cfce9c53bcd9d0eaaa41ca8ba9ded434f68845bbf943351518366fbb20cff';
  'docs/ux/visual-oracles/hardware-layout-direction-b-refinement-v3.html'='6c677d5e9bf9f2f58f1f404eca3798cd6d17c68911f966ca21dec01ca902fb4b';
  'docs/ux/visual-oracles/compat-visual-style-v3.html'='3ffc2f3664363f20908dedf110e52b53cee0860fb24c096bede4d7db979a03df';
  'docs/ux/visual-oracles/optimisation-choice-page-v1.html'='434f04ba81e1eb9ef1f88b3c2f2cca357103b7fe3b6c03f952681280c85921ae';
  'docs/ux/visual-oracles/optimisation-spectrum-v1.html'='d74f3b93df5c38419cf6adad45a308fe78745eb9d23db607b411858e054eb9af';
  'docs/ux/visual-oracles/shared-optimisation-flow.html'='d669771600ee0d3a56ef3b793c6c06a9882048b88f46f433fed006e5033292f0'
}
foreach($entry in $oracles.GetEnumerator()){ $actual=(Get-FileHash -LiteralPath (Join-Path 'C:\GEAI-XRoute' $entry.Key) -Algorithm SHA256).Hash.ToLowerInvariant(); if($actual -cne $entry.Value){ throw "Visual oracle mismatch: $($entry.Key)" } }
```

Expected: both status commands are empty, the ancestor check exits `0`, the new worktree HEAD equals `$expectedPlanCommit`, and every fetched ref resolves to the exact authority SHA in the table.

- [ ] **Step 2: Write a failing immutable-authority test**

Create `OptimizationImportManifestTests.cs` with a test that loads the machine JSON, asserts all six authority SHAs above, rejects merge-based import methods, requires every component to declare `Pending` or `Verified`, and requires `Verified` components to contain non-empty destination, dependency, integration-patch, and verification arrays plus source records whenever source blobs are imported. Every destination declares `Exact`, `Adapted`, or `Created`: `Exact` requires commit:path/blob-bound byte identity; `Adapted` requires source commit/path/blob/SHA-256, destination, patch-group ID, tracked patch path/SHA-256, and final result SHA-256; `Created` is permitted only for integration-owned files absent from the input branch and requires an integration-base commit/tree, tracked creation patch from `/dev/null`, patch SHA-256, destination, and final result SHA-256, with no invented source blob. Controlled temporary Git repositories must non-vacuously exercise valid and invalid Exact, Adapted, Created, authority/policy, allowlist, patch, worktree, staged-blob, staged-mode and rename/copy cases. All import mutation cases run serially inside one hidden PowerShell batch-driver process that imports `CrossRouteImportManifest.Core.psm1` once; no DataRow launches a process. A second process test runs the verifier against a Pending component and requires a non-zero exit, proving an empty ledger cannot authorize import. `ChangedPathAllowlistTests` process-tests `Assert-ChangedPaths.ps1`: one allowed edit writes a NUL-delimited pathspec; one unrelated untracked file and one path outside the allowlist each fail without writing a pathspec. `PackagedTestCheckpointTests` tests controlled fake build/runner orchestration only through `PackagedCheckpoint.Core.psm1`; all checkpoint mutation cases run serially inside one hidden PowerShell batch-driver process that imports the module once. It proves Installer PATH injection, operation-owned recipe/TRX enforcement, strict counter parsing, repository identity stability and rejection of stale/future/malformed evidence. The production wrapper exposes only `-Filter` and optional `-EvidenceDirectory`, derives every executable/repository/artifact identity itself, and cannot emit accepted evidence through the fake seam. Every remaining C# launcher shares one process-wide `SemaphoreSlim(1,1)`, uses `UseShellExecute=false`, `CreateNoWindow=true`, redirected streams, a hard timeout, and kills the entire process tree on timeout; fixture Git processes obey the same hidden serialized discipline.

```csharp
[TestMethod]
public void ManifestPinsEveryAuthorityAndBansWholeBranchMerges()
{
    string path = Path.Combine(FindRepositoryRoot(), "docs", "handoffs",
        "2026-08-26-cross-route-optimisation-import-manifest.json");
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
    string canonical = document.RootElement.GetRawText();
    foreach (string sha in new[] {
        "092589c38981ad86bb73c7c97dff01ab8b5a6c8e",
        "8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8",
        "f0189ed187ba900f27bade5fde282ae4e99e8d7b",
        "e254385997392601102b16acf19244437803bdcc",
        "bacb3f4106e0191b05b870358342f8158765396d",
        "3f7c29d318e317b63f54c558bc69803963d7d88c" })
        StringAssert.Contains(canonical, sha);
    Assert.IsFalse(canonical.Contains("git merge ", StringComparison.OrdinalIgnoreCase));
}

private static string FindRepositoryRoot()
{
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "IBM Granite with TurboQuant (Intel).slnx")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Repository root was not found.");
}
```

- [ ] **Step 3: Run the test and observe the missing-file failure**

Bootstrap this first red without the not-yet-created checkpoint helper: prepend the Visual Studio Installer directory to process `PATH`, restore/build UnitTests serially for Debug x64 with all worker/quantiser packaging disabled and app-package generation false, prove the UnitTests recipe is newer than the build checkpoint, then invoke VS Community VSTest directly with `/TestCaseFilter:FullyQualifiedName~OptimizationImportManifestTests` and a TRX. Parse the TRX and require exactly one or more discovered tests, at least one failure, and a failure naming the absent manifest. A missing recipe, build failure, zero discovery, or missing test harness is not the intended red and must be repaired before continuing.

- [ ] **Step 4: Create the manifest with exact authority tables**

For each imported component, the JSON records source ref/SHA, status, selected source paths, Git blob IDs, computed SHA-256 values, destination paths, dependency closure, excluded shared paths, manual integration patches, and verification commands. Start UO1, GGUF runtime, and OpenVINO route as `Pending`; each import task must populate every required array and change only its own component to `Verified` before source files can be staged. The Markdown mirrors those facts for review and includes this rule verbatim: `A component import commit must change only its manifest-approved destination paths, declared integration patch paths, and these two manifest files.`

`Test-CrossRouteImportManifest.ps1` remains a fail-closed, non-injectable production wrapper over reusable operations exported by `CrossRouteImportManifest.Core.psm1`. The verifier accepts `-Component`, `-SourceRepository`, `-DestinationRepository`, optional locked-review-artifact `-WriteAllowedPathspec`, `-StageVerified`, and optional `-VerifyStaged`, parses the JSON with strict duplicate-key rejection, and requires `Verified`. For every source record it normalizes a relative non-traversing Git path, resolves the declared pinned authority commit and path with `git ls-tree` plus `git rev-parse <commit>:<path>`, requires mode `100644` or `100755`, rejects symlink/submodule/tree modes, and requires the resulting object ID to equal the recorded blob ID before obtaining bytes with `git cat-file blob` and computing SHA-256. A merely reachable or prose-named blob is never accepted. For `Exact`, it compares imported destination bytes directly. Each `Adapted` or `Created` patch group declares exactly one base authority commit/tree; the verifier seeds an owned temporary Git tree with commit:path-bound base blobs, confirms every `Created` destination is absent at that base, verifies the tracked unified patch SHA-256, rejects absolute/traversal/undeclared paths, requires `/dev/null` creation only for declared `Created` files, runs `git apply --check` then `git apply`, hashes every reconstructed result, and requires both the declared result SHA-256 and actual destination bytes to match. Files from different authority/base commits require separate patch groups. It checks that every staged path is in `allowedDestinationPaths`, a declared patch path, or one of the two manifest files, and exits non-zero on missing dependency/patch/verification arrays. `-StageVerified` hashes the already verified bytes with trusted Git `hash-object -w --no-filters -- <literal-path>`, binds each OID back to its verified SHA-256 bytes, stages the exact declared mode/OID/path in an operation-owned temporary index, verifies the complete temporary state, revalidates the original repository/index identity, and atomically publishes the complete verified index. `-WriteAllowedPathspec` writes only a locked review artifact and must never feed `git add`. It never trusts hashes merely because they appear in prose.

`Assert-ChangedPaths.ps1` accepts repository root, one or more exact files or trailing-`/**` owned prefixes, optional review-only `-WritePathspec`, and `-StageVerified`. It rejects repository-local fsmonitor, hooks, untracked-cache, filter, and signing/external-program configuration plus hidden assume-unchanged/skip-worktree/fsmonitor-valid entries, preserves byte-exact status/index/flag identity, parses `git status --porcelain=v1 -z`, and fails on every modified/untracked path outside the allowlist. `-StageVerified` hashes each exact regular file with trusted Git `hash-object -w --no-filters`, applies additions/deletions with literal `update-index` plumbing in an operation-owned temporary index, verifies the exact endpoint set and bytes, revalidates the original repository/index identity, and atomically publishes the complete index. A review pathspec never feeds `git add`. Every later non-import commit must use `-StageVerified`; its commit must disable hooks and signing with the stated `-c` overrides. Direct `git add` is prohibited.

`Invoke-PackagedTestCheckpoint.ps1` accepts only a mandatory `-Filter` and optional evidence directory. It prepends `C:\Program Files (x86)\Microsoft Visual Studio\Installer` to process `PATH`, derives the exact clean repository, VS Community MSBuild/VSTest, UnitTests project and repository-contained recipe, records the pre-build HEAD/tree, removes the exact prior recipe, performs serial win-x64 restore/build with `OpenVinoConverterPackagingRequired=false`, `OpenVinoOfficialWorkerPackagingRequired=false`, `OpenVinoTurboQuantPackagingRequired=false`, `GgufQuantizerPackagingRequired=false`, and `GenerateAppxPackageOnBuild=false`, and requires the exact recipe to be recreated during the bounded operation. It invokes VSTest into an operation-owned evidence directory with an exact GUID TRX name, strictly parses the exact TRX and complete coherent counters, requires non-zero execution/discovery and zero non-passing outcomes, then requires unchanged clean HEAD/tree. It records pre/post identities plus executable and recipe hashes and labels the result as source/component evidence that cannot be used as Release/native/package evidence. `PackagedCheckpoint.Core.psm1` owns testable orchestration and strict parsing primitives but cannot itself publish accepted production checkpoint evidence. Every filtered packaged command in later tasks uses the wrapper so stale binaries cannot pass.

- [ ] **Step 5: Run the focused test and commit**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OptimizationImportManifestTests|FullyQualifiedName~OptimizationImportManifestVerifierProcessTests|FullyQualifiedName~ChangedPathAllowlistTests|FullyQualifiedName~PackagedTestCheckpointTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component UO1 -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD
$pendingExit = $LASTEXITCODE
if ($pendingExit -eq 0) { throw 'Pending UO1 manifest unexpectedly verified.' }
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json','docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md','docs/superpowers/plans/2026-08-26-cross-route-optimisation-integration.md','scripts/verification/Test-CrossRouteImportManifest.ps1','scripts/verification/CrossRouteImportManifest.Core.psm1','scripts/verification/Assert-ChangedPaths.ps1','scripts/verification/Invoke-PackagedTestCheckpoint.ps1','scripts/verification/PackagedCheckpoint.Core.psm1','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationImportManifestTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/ChangedPathAllowlistTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/PackagedTestCheckpointTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "docs(optimisation): freeze bounded component imports"
```

Expected: all three focused test families pass with non-zero discovery, the verifier refuses Pending UO1, and the implementation commit changes exactly eleven paths.

### Task 2: Preserve authoritative candidates in a path-free planning session

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Planning/CompatibilityPlanningSession.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Execution/IOptimizationExecutionPayloadComposer.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEvaluation.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEngine.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityScreenProjection.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Presentation/CompatibilityPlanningSessionTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/PrivacyCanaryTests.cs`

- [ ] **Step 1: Write failing authority and privacy tests**

Cover: a current-fit result retains an optional admitted frontier; required optimisation retains its frontier; no-fit/inconclusive has no session; `Resolve` returns the exact frontier candidate; issuing requires a matching route composer; changing selection issues a new plan ID; stale hardware/capability or missing exact consent fails closed; reflection finds no path-like public member.

```csharp
[TestMethod]
public void CurrentFit_CanIssueOptionalPlanWithoutUiReconstruction()
{
    CompatibilityEvaluation evaluation = CompatibilityEngine.EvaluateProduction(CurrentFitInput());
    Assert.AreEqual(CompatibilityScreenState.EstimatedCompatible, evaluation.Screen.State);
    Assert.IsNotNull(evaluation.PlanningSession);
    OptimizationExecutionPlan plan = evaluation.PlanningSession.Issue(
        OptimizationPreferenceSelection.Manual(60),
        new ExactFixturePayloadComposer(evaluation.PlanningSession.Route),
        CurrentHardwareAuthority(), TimeProvider.System);
    Assert.AreEqual(3, plan.ContractVersion);
    Assert.AreEqual(evaluation.PlanningSession.Route, plan.Route);
}
```

- [ ] **Step 2: Run core tests and verify failure**

Run `dotnet test --project $core --configuration Release`. Expected: compile failure because `CompatibilityEvaluation`, `EvaluateProduction`, and `CompatibilityPlanningSession` are absent.

- [ ] **Step 3: Add the exact planning types**

Use these public shapes; keep all candidate and evidence collections private and immutable.

```csharp
public interface IOptimizationExecutionPayloadComposer
{
    OptimizationRoute Route { get; }
    OptimizationExecutionPayload Compose(OptimizationCandidate candidate);
}

public sealed record CompatibilityEvaluation(
    CompatibilityScreenModel Screen,
    CompatibilityPlanningSession? PlanningSession,
    CurrentCompatibleConfiguration? CurrentConfiguration);

public sealed record CurrentCompatibleConfiguration(
    OptimizationRoute Route,
    OptimizationExecutionPayload ExactExecutionPayload,
    string RuntimeConfigurationSha256,
    string CompatibilityDecisionId);

public sealed class CompatibilityPlanningSession
{
    public OptimizationRoute Route { get; }
    public OptimizationExecutionPlan Issue(
        OptimizationPreferenceSelection preference,
        IOptimizationExecutionPayloadComposer composer,
        OptimizationIssuanceAuthority currentHardwareAuthority,
        TimeProvider timeProvider);
}
```

The private constructor receives the generated frontier, capability snapshot, workload, journey binding, model layer count, current hardware authority, and exact opted-in evidence set. `CurrentCompatibleConfiguration.ExactExecutionPayload` is the path-free, fully typed GGUF or OpenVINO runtime configuration that produced the compatible decision; its canonical digest must equal `RuntimeConfigurationSha256`. `Issue` must call `OptimizationPreferenceResolver.Resolve`, require `composer.Route == candidate.Route`, call `composer.Compose(candidate)`, verify payload/candidate agreement, then call the existing `OptimizationPlanIssuer.Issue`. It must never infer tool, format, cache, context, device, threads, batch size, or persistence defaults.

- [ ] **Step 4: Return an evaluation without breaking current callers**

Add `CompatibilityEngine.EvaluateProduction(CompatibilityProductionInput)` and make existing `Run(CompatibilityProductionInput)` return `.Screen`. Change projection so admitted alternatives are retained for both `EstimatedCompatible` and `OptimisationRequired`; only the rendered required-state selector remains visible by default. Return `null` planning authority for inconclusive/no-fit results.

- [ ] **Step 5: Run the full core suite and commit**

```powershell
dotnet test --project $core --configuration Release
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/**','tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(compatibility): retain authoritative optimisation planning session"
```

Expected: all core tests pass; the suite count is not lower than its pre-task baseline.

### Task 3: Bind exact experimental consent and plan reissue

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/Planning/CompatibilityPlanningSession.cs`
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Presentation/CompatibilityEvaluation.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/OptimizationPlanBindingTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityViewModelTests.cs`

- [ ] **Step 1: Write failing consent lifecycle tests**

Test absence before opt-in, exact current `EvidenceId` only, new frontier/plan ID after opt-in, explicit final confirmation, revocation, wrong-route/wrong-ID rejection, evidence drift invalidation, and no effect on unrelated released candidates.

```csharp
[TestMethod]
public async Task RevokingExactExperimentalConsentInvalidatesDependentPlan()
{
    var vm = CreateViewModelWithExperimentalEvidence("tbq-evidence-7");
    await vm.SetExperimentalConsentAsync("tbq-evidence-7", true);
    Guid admitted = vm.CurrentOptimizationHandoff!.OptimizationPlanId;
    await vm.SetExperimentalConsentAsync("tbq-evidence-7", false);
    Assert.AreNotEqual(admitted, vm.CurrentOptimizationHandoff?.OptimizationPlanId);
    Assert.IsFalse(vm.CanConfirmExperimentalPlan);
}
```

- [ ] **Step 2: Run focused tests and verify failure**

Run core tests plus packaged `FullyQualifiedName~CompatibilityViewModelTests`. Expected: compile failure for the new consent API.

- [ ] **Step 3: Implement exact-evidence consent**

Store an immutable exact-ID set scoped to the current model handoff, Hardware run/snapshot, capability snapshot, and planning-session generation. `SetExperimentalConsentAsync` must cancel/invalidate the prior plan, regenerate from the production input with the updated exact-ID set, and issue a fresh plan only after revalidation. On drift/model change/page retirement, clear the set. Confirmation must require the plan admission proof’s exact experimental ID to remain present.

Add one bounded notice row and checkbox to the existing Compatibility selector. Derive all copy locally; never render provider evidence text. Repeat the quality/experimental warning on confirmation.

- [ ] **Step 4: Run focused and full compatibility tests and commit**

```powershell
dotnet test --project $core --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~ModelHardwareCompatibility'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/**','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/**','tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/**','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(compatibility): bind exact experimental optimisation consent"
```

Expected: both suites pass with non-zero discovery; privacy canaries report no evidence/path leak.

### Task 4: Add private source custody and a separate current-model Chat authority

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelSourceCustodyRegistry.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/CurrentModelLaunchHandoff.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/ICurrentModelChatLaunchAuthority.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/CurrentModelChatLaunchRegistry.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/CurrentModelLaunchContext.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/OptimizationJourneyEntryContext.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/CompatibilityContinueRequest.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.ModelSourceCustody.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelSourceCustodyRegistryTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CurrentModelLaunchHandoffTests.cs`

- [ ] **Step 1: Write failing binding, privacy, and retirement tests**

```csharp
[TestMethod]
public void Resolve_RequiresExactHandoffDigestLengthAndRoute()
{
    var registry = new ModelSourceCustodyRegistry();
    registry.Register(SourceRecord());
    Assert.IsTrue(registry.TryAcquire(ExactKey(), out ModelSourceLease? lease));
    Assert.IsFalse(registry.TryAcquire(ExactKey() with { ModelLengthBytes = 1 }, out _));
    lease!.Dispose();
}
```

Also assert the handoff’s public properties are only route, model inspection run/handoff IDs, model SHA-256/length, product Hardware run/snapshot digest, runtime-configuration digest, and compatibility-decision identity. Reflection and serialized-event tests must find no path.

- [ ] **Step 2: Run focused tests and verify missing types**

Build the packaged tests and run filters `FullyQualifiedName~ModelSourceCustodyRegistryTests|FullyQualifiedName~CurrentModelLaunchHandoffTests`. Expected: compile failure.

- [ ] **Step 3: Implement immutable custody and typed exits**

```csharp
internal sealed record CompatibilityContinueRequest
{
    internal sealed record ChatCurrent(CurrentModelLaunchHandoff Handoff) : CompatibilityContinueRequest;
    internal sealed record Optimize(OptimizationSelectionHandoff Handoff) : CompatibilityContinueRequest;
}

internal sealed record CurrentModelChatLaunchResult(
    bool Succeeded,
    CurrentModelChatSupportCode SupportCode);

internal enum CurrentModelChatSupportCode
{
    None,
    CancelledByUser,
    BindingMismatch,
    SourceUnavailable,
    RuntimeUnavailable,
    LaunchFailed
}

internal interface ICurrentModelChatLaunchAuthority
{
    Task<CurrentModelChatLaunchResult> LaunchAsync(
        CurrentModelLaunchHandoff handoff,
        CancellationToken cancellationToken);
}

internal sealed record CurrentModelLaunchContext(
    CurrentModelLaunchHandoff Handoff,
    OptimizationExecutionPayload ExactExecutionPayload,
    ModelSourceCustodyKey SourceKey);

internal enum OptimizationJourneyOrigin { Required, Optional }

internal sealed record OptimizationJourneyEntryContext(
    OptimizationSelectionHandoff OptimizationHandoff,
    OptimizationJourneyOrigin Origin,
    CurrentModelLaunchHandoff? CurrentModelFallback);
```

Register custody through `OnboardingShellPage.ModelSourceCustody.cs` at the one hook inside `NavigateToHardwareInspection`, where the eligible handoff and originating `ModelInspectionPage.Request.ModelPath` coexist. Key it by handoff ID, SHA-256, length, and route; return only a disposable private lease to a route launcher. The private `CurrentModelLaunchContext` carries the exact `CurrentCompatibleConfiguration.ExactExecutionPayload` and source key beside—not inside—the path-free handoff; creation recomputes and compares its canonical configuration digest. `OptimizationJourneyEntryContext` is also path-free and marks the journey as Required or Optional; only Optional may carry the matching current-model fallback. `CurrentModelChatLaunchRegistry` returns unavailable until Task 11 or 14 registers the matching route implementation. Retire entries only on model-selection replacement, Model Inspection handoff replacement/reissue, handoff invalidation, or shell disposal—not on an optimisation preference/consent plan reissue. Current Chat must revalidate compatibility decision, model/Hardware/runtime/capability bindings before resolving custody; it never creates an optimisation result.

- [ ] **Step 4: Run the focused packaged tests and commit**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~ModelSourceCustodyRegistryTests|FullyQualifiedName~CurrentModelLaunchHandoffTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/**','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/**','IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.ModelSourceCustody.cs','IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/**','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/**','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(compatibility): add private source custody and current chat handoff"
```

Expected: all focused tests pass and no public member exposes a path or source record.

### Task 5: Issue typed optimisation/current-Chat exits from Compatibility

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/OptimizationRequestedEventArgs.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/CurrentModelChatRequestedEventArgs.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityPlanIssuanceTests.cs`

- [ ] **Step 1: Write failing journey tests**

Assert the ViewModel/page contract: when an injected route availability says the complete route is registered, current fit exposes `Chat with current model` and `Optimise first`, required optimisation exposes only optimisation continuation, and selecting any band issues an exact v3 plan. No-fit/inconclusive exposes neither; same-band ABA reissue has a fresh plan ID; stale confirmation is rejected. With production availability still false in this wave, no executable destination is advertised.

```csharp
[TestMethod]
public async Task CurrentFit_OffersSeparateCurrentChatAndOptionalOptimization()
{
    CompatibilityViewModel vm = await CreateCurrentFitViewModelAsync();
    Assert.IsTrue(vm.CanChatWithCurrentModel);
    Assert.IsTrue(vm.CanOptimiseFirst);
    vm.BeginOptionalOptimization();
    vm.SelectManualPreference(60);
    Assert.IsNotNull(vm.CurrentOptimizationHandoff);
    Assert.AreEqual(3, vm.CurrentOptimizationHandoff!.Plan.ContractVersion);
}
```

- [ ] **Step 2: Run focused tests and verify failure**

Run the packaged build and the two named filters. Expected: compile/test failure because typed events and optional current-fit state do not exist.

- [ ] **Step 3: Implement exact event shapes and state transitions**

```csharp
internal sealed class OptimizationRequestedEventArgs(OptimizationJourneyEntryContext context) : EventArgs
{
    internal OptimizationJourneyEntryContext Context { get; } = context;
}

internal sealed class CurrentModelChatRequestedEventArgs(CurrentModelLaunchHandoff handoff) : EventArgs
{
    internal CurrentModelLaunchHandoff Handoff { get; } = handoff;
}
```

`CompatibilityViewModel` must retain the current immutable `CompatibilityEvaluation`, call its planning session to issue plans, and then call `OptimizationSelectionHandoff.TryCreate`. It must never construct a candidate/payload from rendered mode strings. `BeginOptionalOptimization` reveals the existing selector for current-fit; issuance raises an Optional `OptimizationJourneyEntryContext` carrying the exact matching current-model fallback. Required optimisation raises Required with a null fallback. Back/cancel before execution returns to current-fit actions. Clear the current handoff before any async refresh and ignore completion from an old `_attemptGeneration`. Tests prove a required journey can never manufacture a current-model fallback.

- [ ] **Step 4: Preserve honest production unavailability until route registration**

The page can emit typed events in tests with a complete fixture composer/authority, but the production shell keeps `continueDestinationAvailable: false` and its current no-op handler until Task 16 registers both verified route destinations after Tasks 11, 13, and 14 pass. A missing route must render unavailable and cannot be represented by a throwing composer or synthetic current-Chat configuration. Do not edit `MainWindow` and do not create a second onboarding shell.

- [ ] **Step 5: Run compatibility/navigation tests and commit**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~CompatibilityPlanIssuanceTests|FullyQualifiedName~CompatibilityViewModelTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/OptimizationRequestedEventArgs.cs','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Contracts/CurrentModelChatRequestedEventArgs.cs','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/ViewModels/CompatibilityViewModel.cs','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/CompatibilityPage.xaml.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityPlanIssuanceTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/CompatibilityViewModelTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(onboarding): route typed compatibility decisions"
```

Expected: current fit, required optimisation, no-fit, inconclusive, and stale confirmation tests pass; production still advertises no destination before real route registration.

### Task 6: Import and adapt the canonical post-selection UI once

**Files:**
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationConfigurationCard.xaml`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationConfigurationCard.xaml.cs`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationConfirmationCard.xaml`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationConfirmationCard.xaml.cs`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationDestinationCard.xaml`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationDestinationCard.xaml.cs`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationOutcomeCard.xaml`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationOutcomeCard.xaml.cs`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationProgressCard.xaml`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationProgressCard.xaml.cs`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationRecoveryCard.xaml`
- Import: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationRecoveryCard.xaml.cs`
- Adapt: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml`
- Adapt: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml.cs`
- Adapt: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Presentation/OptimizationIntent.cs`
- Adapt: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Presentation/OptimizationPresentationFactory.cs`
- Adapt: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Presentation/OptimizationPresentationState.cs`
- Adapt: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Presentation/OptimizationTheme.xaml`
- Adapt: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/DebugFixtures/**`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md`
- Create: `docs/handoffs/import-patches/uo1-v3-adaptation.patch`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/**`

- [ ] **Step 1: Populate and verify the UO1 import manifest**

Use `git ls-tree -r` and `git show` against `8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8`; record every permitted source blob and computed SHA-256. Explicitly forbid `Controls/OptimizationPreferenceCard.xaml`, `.xaml.cs`, and `OptimizationSelectionLayoutTests.cs`. Record that all other changed paths are rejected.

```powershell
$uo1 = '8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8'
git cat-file -e "$uo1^{commit}"
git ls-tree -r $uo1 -- 'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization' 'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization'
```

Expected: the exact commit exists and the tree lists the approved UO1 files. Do not invoke the strict component verifier while UO1 remains `Pending`; the single accepting verifier call occurs only after Step 2 records every exact/adapted result and changes UO1 to `Verified`.

- [ ] **Step 2: Import permitted blobs into one reviewable commit**

Copy only manifest-approved paths from the exact Git blobs. Preserve exact files as `Exact`. For every adapted file, generate the single tracked `uo1-v3-adaptation.patch` against the declared base blobs, record its SHA-256 and each final result SHA-256, then change UO1 to `Verified`. Before commit, reject any diff path outside `Features/ModelOptimization`, its focused tests, both manifests, and that patch. Do not copy UO1 App, project, MainWindow, Onboarding, Compatibility, selector, or resource-registration files.

- [ ] **Step 3: Write failing v3-only presentation tests**

Assert no `Selecting`, `PreferenceChanged`, or `ReviewConfigurationRequested`; page construction requires an exact `OptimizationJourneyEntryContext`; confirmation carries both plan ID and configuration SHA; stage labels are exactly the seven approved labels; persistent success is Chat/Save; runtime-only success is Chat/Done with `This optimisation changes how the model runs; it does not create a new model file.`; Optional cancellation/failure is Retry/Back/`Chat with original model`, Required cancellation/failure is Retry/Back without original Chat; closed support-code copy cannot include adapter text.

```csharp
[TestMethod]
public void RuntimeOnlySuccess_HasChatAndDoneButNoSave()
{
    OptimizationPresentationState state = Factory.SuccessRuntimeProfile(Result());
    CollectionAssert.AreEqual(new[] { OptimizationCommand.Chat, OptimizationCommand.Done }, state.Commands.ToArray());
    Assert.IsFalse(state.Commands.Contains(OptimizationCommand.Save));
    StringAssert.Contains(state.Body, "does not create a new model file");
}
```

- [ ] **Step 4: Adapt the page to post-selection-only v3 state**

Delete/unreach the selection state and preference event. Start from a display-only confirmation projected from `handoff.Plan.Preference` and the exact v3 candidate/payload. Replace string/Tag action identifiers with this closed command enum:

```csharp
internal enum OptimizationCommand
{
    Confirm,
    Cancel,
    Retry,
    BackToCompatibility,
    Chat,
    ChatWithOriginal,
    Save,
    Done
}
```

Use the current light model-import/inspection/hardware theme values; keep one centred responsive content column, natural scrolling, equal-height progress rows, full-width disclosure, High Contrast resources, and 200% text wrapping. The design owns current copy, actions, and enabled state; the scoped oracle regions own geometry/tokens only, so do not copy their stale disabled placeholders or review controls. Do not register a second theme, page, gallery, or selector.

- [ ] **Step 5: Run UI tests and commit the bounded import**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.ModelOptimization'
git diff --check
git diff --name-only HEAD | ForEach-Object { if ($_ -notmatch '^(IBM Granite with TurboQuant \(Intel\)/Features/ModelOptimization/|tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/|docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest\.(json|md)$|docs/handoffs/import-patches/uo1-v3-adaptation\.patch$)') { throw "Forbidden UO1 path: $_" } }
$reviewPathspec = Join-Path $env:TEMP 'uo1-import-pathspec.bin'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component UO1 -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component UO1 -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(optimisation): import canonical post-selection experience"
```

Expected: all ModelOptimization tests pass with non-zero discovery and the commit contains no preference card.

### Task 7: Implement the pure journey reducer and generation-safe coordinator

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationExecutionResult.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationJourneyState.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationJourneyReducer.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationJourneyCoordinator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/IOptimizationExecutor.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/IOptimizationRevalidator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationExecutorRouter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationProgress.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/OptimizationAttemptContext.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationJourneyReducerTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationJourneyCoordinatorTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Optimization/OptimizationExecutionResultSourceIntegrityTests.cs`

- [ ] **Step 1: Write failing closed-state and concurrency tests**

Cover confirmation, seven monotonic stages, cancellation, failure, replan, persistent/runtime success, duplicate Confirm/Retry/Cancel, delayed callbacks, page unload, shutdown, terminal once, stale generation, checked generation overflow/no-wrap, and one destination-command lease per action. Required-versus-Optional is immutable reducer input: cancelled/failed Optional retains only the exact `CurrentModelLaunchHandoff` from its entry context and exposes `ChatWithOriginal`; Required always has a null fallback and never exposes that command. Add explicit presentation/coordinator/navigation tests for Optional cancel, Optional failure, stale fallback, Required cancel, and Required failure. Add source-integrity factory tests proving failed, cancelled, and replan results carry the observed `SourceUnchanged` value and successful output admission requires true.

```csharp
[TestMethod]
public async Task LateSuccessAfterCancellationCannotPublish()
{
    var executor = new ControllableExecutor();
    var coordinator = CreateCoordinator(executor);
    await coordinator.ConfirmAsync();
    long generation = coordinator.State.Generation;
    await coordinator.CancelAsync();
    executor.Complete(generation, PersistentSuccess());
    Assert.AreEqual(OptimizationJourneyKind.Cancelled, coordinator.State.Kind);
    Assert.AreEqual(0, OutputRegistry.RegisterCalls);
}
```

- [ ] **Step 2: Run focused tests and verify missing types**

Build and run `FullyQualifiedName~OptimizationJourneyReducerTests|FullyQualifiedName~OptimizationJourneyCoordinatorTests`. Expected: compile failure.

- [ ] **Step 3: Implement the closed state and ports**

```csharp
internal enum OptimizationJourneyKind
{
    Confirmation,
    Running,
    Cancelling,
    Cancelled,
    Failed,
    ReplanRequired,
    SucceededPersistent,
    SucceededRuntimeProfile
}

internal interface IOptimizationExecutor
{
    OptimizationRoute Route { get; }
    Task<OptimizationExecutionResult> ExecuteAsync(
        OptimizationExecutionPlan plan,
        OptimizationAttemptContext context,
        IProgress<OptimizationProgress> progress,
        CancellationToken cancellationToken);
}

internal sealed record OptimizationAttemptContext(
    long Generation,
    StagedSourceSnapshot Source,
    string OperationStagingRootIdentity);
```

The reducer must be pure: `(state, event) -> state`. Its initial state is created from the immutable `OptimizationJourneyEntryContext`; it never infers Optional from screen copy or the presence of a fitting candidate. The coordinator owns one checked monotonic process-local generation and one cancellation source, validates before confirmation and immediately before execution, starts exactly one route executor, suppresses callbacks whose generation/plan/configuration do not match, and publishes at most one terminal state. On Optional cancellation/failure, `ChatWithOriginal` routes the retained handoff through `CurrentModelChatLaunchRegistry`, which performs full current revalidation; launch failure leaves the recovery state intact. Required cancellation/failure cannot call that registry. Keep the public `OptimizationExecutionResult` member set unchanged, but require every terminal factory to receive the executor's observed source-integrity attestation rather than hard-coding true; unknown or unperformed final rehash is false and cannot admit output. `checked(current + 1)` overflow permanently retires that coordinator instance and returns to fresh planning; it never wraps. Page unload only detaches presentation; shutdown cancels and performs bounded cleanup.

- [ ] **Step 4: Normalize route progress into the seven stages**

`OptimizationProgress` contains generation, plan ID, configuration SHA, stage enum, bounded fraction, and locally selected status key. Reject decreasing stages/fractions, unknown stages, wrong bindings, and post-terminal updates. Never accept free-form route text.

- [ ] **Step 5: Run coordinator/UI tests and commit**

```powershell
dotnet test --project $core --configuration Release --filter 'FullyQualifiedName~OptimizationExecutionResultSourceIntegrityTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OptimizationJourneyReducerTests|FullyQualifiedName~OptimizationJourneyCoordinatorTests|FullyQualifiedName~OptimizationProgressCardTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Optimization/OptimizationExecutionResult.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Application/**','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/**','tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Optimization/OptimizationExecutionResultSourceIntegrityTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(optimisation): add generation-safe shared coordinator"
```

Expected: concurrency and stale-callback tests pass deterministically.

### Task 8: Implement sealed source snapshots and the durable output registry

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationSourceResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/StagedSourceSnapshot.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationOutputRegistry.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationOutputLease.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationCommitReceipt.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/OptimizationCommitJournal.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationSourceResolverTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationOutputRegistryTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationRestartRecoveryTests.cs`

- [ ] **Step 1: Write failing custody, atomicity, and restart tests**

Test reparse/link rejection, source mutation during copy, exact GGUF hash/length, exact OpenVINO manifest membership, staging-only executor authority, registry-created same-volume staging, cross-volume promotion rejection, staging-root reparse rejection, immutable output key, duplicate/stale/second-terminal rejection, crash before/after move and receipt, durable-flush-before-success, lease protection, quarantine, and path privacy.

```csharp
[TestMethod]
public async Task RestartNeverAdmitsMovedOutputWithoutDurableReceipt()
{
    await Fixture.MoveSealedCandidateWithoutWritingReceiptAsync();
    OptimizationOutputRegistry registry = await Fixture.RestartAsync();
    Assert.AreEqual(0, registry.AdmittedCount);
    Assert.IsTrue(Fixture.IsQuarantinedOrRemoved);
}
```

- [ ] **Step 2: Run focused tests and verify failure**

Run the three named packaged filters. Expected: compile failure.

- [ ] **Step 3: Implement sealed source resolution**

Resolve the private source lease and reject reparse/symbolic links. The registry—not a route adapter—creates the random operation-owned staging directory beneath a verified non-reparse app root on the same volume as the final committed root, then returns a sealed staging lease. Read through verified handles. For GGUF, hash and length-check during copy. For OpenVINO, copy only manifest-listed regular files and recompute the complete package digest. Seal the snapshot read-only for the adapter and rehash the original after execution.

- [ ] **Step 4: Implement one durable admission transaction**

```csharp
internal sealed record OptimizationOutputKey(
    OptimizationRoute Route,
    Guid OptimizationPlanId,
    string ConfigurationSha256,
    string OutputIdentity,
    string OutputManifestSha256);

internal sealed record OptimizationCommitReceipt(
    OptimizationOutputKey Key,
    long AttemptGeneration,
    string SourceSha256,
    bool SourceUnchanged,
    ulong OutputSizeBytes,
    string SealedStagingIdentity,
    string PublicationIdentity);
```

The executor receives only its registry-created operation staging lease. On apparent success it returns a sealed candidate; the registry validates route/plan/config/source/source-unchanged attestation/generation/sealed staging identity/manifest/size, rechecks same volume and non-reparse ancestry, atomically moves it to the committed root, atomically replaces and durably flushes the receipt journal, then exposes success. Cross-volume or caller-created staging is rejected rather than copied. Startup reconstructs only receipt-backed entries whose source attestation, staging/publication identities, bytes, and manifests still verify. Invalid or unreceipted app-owned items are quarantined; imported sources and external Save destinations are never cleanup targets.

- [ ] **Step 5: Run storage, coordinator, and privacy tests and commit**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OptimizationSourceResolverTests|FullyQualifiedName~OptimizationOutputRegistryTests|FullyQualifiedName~OptimizationRestartRecoveryTests|FullyQualifiedName~Privacy'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Storage/**','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(optimisation): seal sources and receipt verified outputs"
```

Expected: all storage/crash/privacy cases pass; no success is visible without a durable receipt.

## Wave 2 — GGUF runtime, quantisation, Chat, and result admission

### Task 9: Import the route-owned production GGUF runtime with immutable closure

**Files:**
- Import: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/**`
- Import: `shared/GraniteEdgeAI.GgufRuntime.Contracts/**`
- Import: `shared/GraniteEdgeAI.GgufRuntime.Transport/**`
- Import: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/**`
- Import: `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/**`
- Import: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/**`
- Import: `workers/GraniteEdgeAI.GgufRuntime.Worker/**`
- Import: `IBM Granite with TurboQuant (Intel)/GgufRuntime.WorkerPackaging.targets`
- Import: `scripts/gguf-runtime/**`
- Import: `scripts/Run-ChatPreview.ps1`
- Import: `third-party/licenses/LICENSE.LLamaSharp.txt`
- Import: `third-party/licenses/LICENSE.llama.cpp.txt`
- Import: GGUF contract/unit/integration/process-fixture tests named in the manifest
- Modify minimally: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify minimally: `IBM Granite with TurboQuant (Intel).slnx`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json`
- Create: `docs/handoffs/import-patches/gguf-runtime-integration.patch`

- [ ] **Step 1: Freeze the complete GGUF dependency closure**

Against `bacb3f4106e0191b05b870358342f8158765396d`, record every route-owned source blob and SHA-256 for app feature, contracts, transport, capabilities, native adapter, worker client/worker, targets, scripts, licences, tests, and process fixtures. Mark `Features/GgufRuntime/GgufInspectedModelLaunchFactory.cs` as excluded/obsolete because it invents CPU defaults from Model Inspection. Mark all source-branch App/MainWindow/Onboarding/Compatibility/Import/Inspection/project/solution versions forbidden.

- [ ] **Step 2: Write a failing dependency/ownership test**

Extend `OptimizationImportManifestTests` to assert the exact GGUF tip, the obsolete factory exclusion, and presence of every required project/packaging dependency. Add a structure assertion that no current coordinator path references `GgufInspectedModelLaunchFactory`.

- [ ] **Step 3: Import route-owned paths and make minimal registration edits**

Import each approved blob from Git. Add only required `ProjectReference`, solution project entries, XAML theme resource, and `GgufRuntime.WorkerPackaging.targets` import to current authoritative files. Preserve current App, MainWindow, Onboarding, Compatibility, and existing build settings. Record route-owned blobs as `Exact`. Record every shared project/solution edit as one `Adapted` patch group based on the exact planning-commit blobs, write `gguf-runtime-integration.patch`, and pin the patch SHA-256 plus each final result SHA-256 in both manifests. The verifier must reconstruct those shared results before GgufRuntime becomes `Verified`; no untracked/manual integration edit is accepted.

- [ ] **Step 4: Run the production GGUF component gate**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1
dotnet test --project $core --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GgufRuntime'
git diff --check
```

Expected: seven GGUF component projects and the Release x64 self-contained app build pass; core and packaged GGUF tests pass with non-zero discovery.

Run `Test-CrossRouteImportManifest.ps1 -Component GgufRuntime` with the same source/destination repositories before staging; expected exit `0` and byte equality for every imported blob.

- [ ] **Step 5: Enforce import boundaries and commit**

Check every changed path against the populated manifest. Any unapproved shared path aborts the commit. Commit:

```powershell
$reviewPathspec = Join-Path $env:TEMP 'gguf-runtime-import-pathspec.bin'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component GgufRuntime -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component GgufRuntime -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(gguf): import verified chat runtime closure"
```

Expected: the commit contains only manifest-approved blobs plus named minimal integration edits.

### Task 10A: Freeze and build the official GGUF quantiser package

**Files:**
- Create: `third-party/llama-quantize/source.lock.json`
- Create: `scripts/gguf-quantization/Build-VerifiedQuantizer.ps1`
- Create: `scripts/gguf-quantization/Test-GgufQuantizerPackage.ps1`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/QuantizerSourceLockTests.cs`

- [ ] **Step 1: Write a failing source-lock test**

Require source URL `https://github.com/ggml-org/llama.cpp.git`, commit `3f7c29d318e317b63f54c558bc69803963d7d88c`, target `llama-quantize`, architecture `x64`, configuration `Release`, `GGML_NATIVE=OFF`, `GGML_OPENMP=OFF`, `LLAMA_CURL=OFF`, static libraries, the existing `third-party/licenses/LICENSE.llama.cpp.txt`, and an empty network-fetch list. Expected red: source lock is missing.

- [ ] **Step 2: Add the canonical source lock and verifier scripts**

`Build-VerifiedQuantizer.ps1` accepts explicit `-StageDirectory`, resolves it to an empty non-reparse directory, clones only the official repository into an operation-owned temp directory, checks out the exact detached commit, verifies `HEAD`, configures MSVC x64 Release with the locked flags, and builds only `llama-quantize`. Parse `dumpbin /dependents` through a closed classifier: Windows system DLLs are recorded as OS-provided and never copied; VC/UCRT runtime DLLs must resolve from the selected Visual Studio/redist identity and are copied only when app-local deployment is required; every other dependency must be produced by the same locked build or the package fails. Copy only the classified app-local closure plus the MIT notice and write `llama-quantize.package.manifest.json`. The canonical manifest records schema `1`, package ID `granite-edge-ai-llama-quantize-x64`, source URL/commit, CMake flags, VS/MSVC/CMake versions, OS-provided dependency names, relative app-local files and lowercase SHA-256 values, executable relative path, architecture, exact allowed tokens `Q2_K,Q3_K_M,Q4_K_M,Q5_K_M,Q6_K,Q8_0`, maximum source/output bytes, timeout, output caps, and licence identity. Unknown/unresolved/extra dependencies fail. It never commits or downloads a binary at runtime.

```powershell
$stage = 'C:\GEAI-Tools\llama-quantize-3f7c29d-x64'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-quantization\Build-VerifiedQuantizer.ps1 -StageDirectory $stage
$manifest = Join-Path $stage 'llama-quantize.package.manifest.json'
$manifestSha = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-quantization\Test-GgufQuantizerPackage.ps1 -StageDirectory $stage -ExpectedManifestSha256 $manifestSha
```

Expected: both scripts exit `0`; manifest source commit is exact; every listed file/hash exists; unlisted files, wrong architecture, missing licence, and any hash change fail.

- [ ] **Step 3: Run the source-lock test and commit source authority only**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests.csproj -c Release --filter 'FullyQualifiedName~QuantizerSourceLockTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('third-party/llama-quantize/source.lock.json','scripts/gguf-quantization/Build-VerifiedQuantizer.ps1','scripts/gguf-quantization/Test-GgufQuantizerPackage.ps1','tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests.csproj','tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/QuantizerSourceLockTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "build(gguf): freeze official quantiser source"
```

Expected: non-zero discovered tests pass; no executable or stage directory is committed.

### Task 10B: Add closed quantisation contracts and exact format mapping

**Files:**
- Create: `shared/GraniteEdgeAI.GgufQuantization.Contracts/GraniteEdgeAI.GgufQuantization.Contracts.csproj`
- Create: `shared/GraniteEdgeAI.GgufQuantization.Contracts/GgufQuantizationProtocol.cs`
- Create: `shared/GraniteEdgeAI.GgufQuantization.Contracts/GgufQuantizationCommand.cs`
- Create: `shared/GraniteEdgeAI.GgufQuantization.Contracts/GgufQuantizationEvent.cs`
- Create: `shared/GraniteEdgeAI.GgufQuantization.Contracts/GgufQuantizationSupportCode.cs`
- Create: `runtime/GraniteEdgeAI.GgufQuantization.Capabilities/GraniteEdgeAI.GgufQuantization.Capabilities.csproj`
- Create: `runtime/GraniteEdgeAI.GgufQuantization.Capabilities/GgufQuantizerFormatMap.cs`
- Create: `runtime/GraniteEdgeAI.GgufQuantization.Capabilities/GgufRequantizationAuthorization.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.GgufQuantization.Contracts.Tests/GraniteEdgeAI.GgufQuantization.Contracts.Tests.csproj`
- Create: `tests/ContractTests/GraniteEdgeAI.GgufQuantization.Contracts.Tests/GgufQuantizationContractTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/GgufQuantizerFormatMapTests.cs`

- [ ] **Step 1: Write failing protocol/format tests**

The command contains protocol/correlation/plan/config IDs, source/output opaque tokens, exact source and target weight enums, tool-manifest SHA, requantisation-policy version, and optional `RequantizationAuthorizationSha256` only. Assert no path/free-form args or generic arbitrary-argument flag. Map only Q2_K→`Q2_K`, Q3_K_M→`Q3_K_M`, Q4_K_M→`Q4_K_M`, Q5_K_M→`Q5_K_M`, Q6_K→`Q6_K`, Q8_0→`Q8_0`; Imported and TurboQuant cache formats throw. `GgufRequantizationAuthorization` canonicalizes the exact plan ID, configuration SHA, model SHA/length, source format, target format, quantiser manifest SHA, and policy version. It exists only when the sealed `GgufRequantisationPolicy` explicitly admits that source→target reduction; its lowercase SHA-256 is carried in the command and terminal event. Tests prove same-format/unquantized input omits it, quantized reduction requires it, and any field substitution changes/rejects the digest. Expected red: types absent.

- [ ] **Step 2: Implement the closed records/map and run green**

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.GgufQuantization.Contracts.Tests/GraniteEdgeAI.GgufQuantization.Contracts.Tests.csproj -c Release
dotnet test tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('shared/GraniteEdgeAI.GgufQuantization.Contracts/**','runtime/GraniteEdgeAI.GgufQuantization.Capabilities/**','tests/ContractTests/GraniteEdgeAI.GgufQuantization.Contracts.Tests/**','tests/UnitTests/GraniteEdgeAI.GgufQuantization.Capabilities.Tests/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(gguf): add closed quantisation contracts"
```

Expected: both projects discover tests and pass; reflection confirms no path or arbitrary argument property.

### Task 10C: Add the supervised quantiser worker and client

**Files:**
- Create: `infrastructure/GraniteEdgeAI.GgufQuantization.WorkerClient/GraniteEdgeAI.GgufQuantization.WorkerClient.csproj`
- Create: `infrastructure/GraniteEdgeAI.GgufQuantization.WorkerClient/GgufQuantizationWorkerClient.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufQuantization.WorkerClient/GgufQuantizerPackageVerifier.cs`
- Create: `workers/GraniteEdgeAI.GgufQuantization.Worker/GraniteEdgeAI.GgufQuantization.Worker.csproj`
- Create: `workers/GraniteEdgeAI.GgufQuantization.Worker/Program.cs`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.GgufQuantization.FakeQuantizer/GraniteEdgeAI.GgufQuantization.FakeQuantizer.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests/GgufQuantizationWorkerClientTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests.csproj`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests/GgufQuantizationProcessTests.cs`

- [ ] **Step 1: Write failing worker/client behavior tests**

Cover success, malformed frame, wrong manifest/executable hash, wrong plan/config, unsupported token, token substitution, replay, cross-operation token, changed source identity, escaped/reparse working root, existing output, cross-volume output, non-zero exit, timeout, cancellation, descendant cleanup, oversize stdout/stderr, and no shell. Add exact argument tests: unquantized/same-format runs never contain `--allow-requantize`; a policy-authorized quantized reduction contains it exactly once; missing, replayed, wrong-policy, wrong-source-format, wrong-target-format, or altered authorization digest fails before process start. Expected red: client/worker missing.

- [ ] **Step 2: Implement one bounded invocation**

The verifier recomputes the canonical manifest hash and every file hash before returning a private `TrustedToolContext`. Client supervision reuses the existing job/process/pipes pattern, uses `ProcessStartInfo.ArgumentList`, `UseShellExecute=false`, hidden window, bounded environment/output/timeout, and kills the owned job on cancel. Before start, the client must receive the Task 8 registry-issued source lease and same-volume output-staging lease; it may create only ACL-restricted, non-reparse children through methods on that lease and rejects any caller path, independent root, volume mismatch, reparse ancestor, or stale generation. It opens the source read-only plus output staging with no-follow semantics. A second inherited private bootstrap pipe carries the path-bearing source/output handle identities and exact leased operation root only to the trusted worker; it is never serialized in the public protocol, logged, or echoed. The worker binds each random opaque command token to exactly one bootstrap entry, checks operation/generation, final path beneath the sealed leased root where applicable, volume, file ID, and open-handle identity, consumes the map once, and rejects substitution/replay/cross-operation use. Only then does it invoke `llama-quantize` with the internally resolved source path, output staging path, and exact mapped target token. It adds literal `--allow-requantize` exactly once in the tool's option position before positional source/output arguments only after recomputing and matching a non-empty plan-bound `GgufRequantizationAuthorization`; no other command may carry the flag. No raw text or path crosses its result.

- [ ] **Step 3: Run process tests and commit**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests.csproj -c Release
dotnet test tests/IntegrationTests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests.csproj -c Release --filter 'FullyQualifiedName~GgufQuantizationProcessTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('infrastructure/GraniteEdgeAI.GgufQuantization.WorkerClient/**','workers/GraniteEdgeAI.GgufQuantization.Worker/**','tests/ProcessFixtures/GraniteEdgeAI.GgufQuantization.FakeQuantizer/**','tests/UnitTests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests/**','tests/IntegrationTests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(gguf): supervise bounded quantiser worker"
```

Expected: all lifecycle cases pass with non-zero discovery and no descendant remains.

### Task 10D: Package the quantiser as a separate verified tool

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/GgufQuantization.WorkerPackaging.targets`
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`
- Modify: `scripts/gguf-runtime/Invoke-GgufChatVerification.ps1`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/GgufQuantizerPackagingTests.cs`

- [ ] **Step 1: Write failing packaging-boundary tests**

Assert required properties `GgufQuantizerStageDirectory`, `GgufQuantizerManifestSha256`, and `GgufQuantizerPackagingRequired`; exact manifest/file verification before copy; quantiser absent from GGUF Chat package; no network target; Release fails when stage/hash missing; source-only tests may explicitly set required false.

- [ ] **Step 2: Implement the packaging target and run green**

Import the target after worker packaging targets. It resolves only verified manifest members beneath the explicit stage root, rejects reparse/missing/additional/hash-mismatched files, and copies them to a distinct `Tools/GgufQuantizer` package directory. Amend `Invoke-GgufChatVerification.ps1` here—before Task 11—with typed `GgufQuantizerStageDirectory`, `GgufQuantizerManifestSha256`, `GgufQuantizerPackagingRequired`, `GenerateAppxPackageOnBuild`, and `SkipApplicationBuild` parameters. The first four are forwarded unchanged to every nested application build. `SkipApplicationBuild` is accepted only for the script's source/component-test phase, omits every application build/package step, and is rejected if any packaging-required flag is true. Process tests prove no value is dropped and no source-only run can be mistaken for Release/package evidence.

```powershell
dotnet build 'IBM Granite with TurboQuant (Intel).slnx' -c Debug -p:Platform=x64 -p:GgufQuantizerPackagingRequired=false -p:BuildInParallel=false -m:1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GgufQuantizerPackagingTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/GgufQuantization.WorkerPackaging.targets','IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj','IBM Granite with TurboQuant (Intel).slnx','scripts/gguf-runtime/Invoke-GgufChatVerification.ps1','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/GgufQuantizerPackagingTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "build(gguf): package separate verified quantiser"
```

Expected: source-only Debug passes; a separate packaging test with the verified Task 10A stage/hash passes; missing/wrong stage/hash fails.

### Task 11: Execute exact v3 GGUF plans and launch verified Chat

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/Gguf/GgufExecutionPayloadComposer.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/Gguf/GgufOptimizationExecutor.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/Gguf/GgufResultValidator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/GgufOptimizationProductionAuthorityProvider.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/GgufCompatibilityInputProjector.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufOptimizationChatLaunchFactory.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufCurrentModelChatLaunchAuthority.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufChatLaunchRequest.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/GgufOptimizationExecutorTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/GgufOptimizationProductionAuthorityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/GgufOptimizationChatLaunchFactoryTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/GgufCurrentModelChatLaunchAuthorityTests.cs`
- Test: `tests/IntegrationTests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests/GgufOptimizationEndToEndTests.cs`

- [ ] **Step 1: Write failing exact-plan execution tests**

Cover v3-only, GGUF-only, full binding/capability/tool/source freshness at confirmation/staging/launch/promotion, exact build/source/device/context/K/V cache/GPU layers/Flash Attention/threads/batch/profile/token limit/weight format, plan-bound `GgufRequantizationAuthorization` and exact conditional `--allow-requantize`, source unchanged, validation/reinspection, path-free result, and no result on cancel/fail/replan.

- [ ] **Step 2: Run focused tests and verify failure**

Run filters for `GgufOptimizationExecutorTests` and `GgufOptimizationChatLaunchFactoryTests`. Expected: compile failure.

- [ ] **Step 3: Compose and execute without defaults**

`GgufOptimizationProductionAuthorityProvider` first verifies the imported GGUF runtime manifest and Task 10A quantiser package/manifest against their packaged assembly metadata, then combines those exact tool/build/hash identities with the current `HardwareInspectionHandoff`, inspected GGUF evidence, workload, binding, and admitted format/runtime limits. It produces the sealed `GgufCapabilityPayload`, exact current `OptimizationExecutionPayload`, `OptimizationIssuanceAuthority`, and optional/required frontier—never presentation-derived defaults. Modify `PreparedGgufCompatibilityInput.TryBindFresh` and `GgufCompatibilityInputProjector` to call the sealed-authority `CompatibilityProductionInput.Create` overload. Missing/unpackaged/stale/wrong runtime, quantiser, device, Hardware snapshot, model identity, or capability evidence returns no planning session and no current Chat configuration.

`GgufExecutionPayloadComposer` maps the selected authoritative candidate and admitted capability into the complete existing v3 `GgufExecutionPayload`; absence of any required tool/runtime value rejects issuance. `GgufOptimizationExecutor` verifies the trusted source/tool contexts, passes only sealed staging to the worker, reports seven normalized stages, validates GGUF header/size/new SHA-256, reinspects the result against the same hardware authority, rehashes the original unchanged, and returns a sealed staging candidate to the output registry.

- [ ] **Step 4: Replace obsolete default-based Chat launch**

`GgufOptimizationChatLaunchFactory` accepts a verified current-source lease or output lease plus the exact confirmed v3 runtime payload. It constructs `GgufRuntimeConfiguration` field-for-field and rejects unsupported admitted backends; it never downgrades a GPU plan to CPU. Keep public `OptimizationExecutionResult` unchanged; carry the output SHA-256 and length only in the sealed output manifest/private `OptimizationOutputLease`, which Chat revalidates without changing the source-bound plan’s model digest.

`GgufCurrentModelChatLaunchAuthority` implements `ICurrentModelChatLaunchAuthority`. It atomically claims `CurrentModelLaunchContext`, requires a GGUF exact execution payload whose canonical digest equals the handoff runtime digest, reacquires the exact current source lease, rechecks model/Hardware/compatibility/capability freshness, then delegates to the same launch factory. Cancellation or launch failure rolls back the claim for retry; source/model/hardware drift invalidates it. Task 16 registers this authority once in `CurrentModelChatLaunchRegistry`; tests cover missing registration, wrong route/digest/runtime/tool, stale source/hardware, retry, and successful direct current-model Chat.

- [ ] **Step 5: Run GGUF E2E and commit**

```powershell
$quantizerStage = 'C:\GEAI-Tools\llama-quantize-3f7c29d-x64'
$quantizerManifest = Join-Path $quantizerStage 'llama-quantize.package.manifest.json'
$quantizerManifestSha = (Get-FileHash -LiteralPath $quantizerManifest -Algorithm SHA256).Hash.ToLowerInvariant()
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-quantization\Test-GgufQuantizerPackage.ps1 -StageDirectory $quantizerStage -ExpectedManifestSha256 $quantizerManifestSha
$env:GEAI_GGUF_QUANTIZER_STAGE_DIRECTORY = $quantizerStage
$env:GEAI_GGUF_QUANTIZER_MANIFEST_SHA256 = $quantizerManifestSha
dotnet test .\tests\IntegrationTests\GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests\GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests.csproj -c Release --filter 'FullyQualifiedName~GgufOptimizationEndToEndTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1 -GgufQuantizerStageDirectory $quantizerStage -GgufQuantizerManifestSha256 $quantizerManifestSha -GgufQuantizerPackagingRequired:$true -GenerateAppxPackageOnBuild:$false
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GgufOptimization|FullyQualifiedName~GgufOptimizationChatLaunchFactory|FullyQualifiedName~GgufOptimizationProductionAuthorityTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/Gguf/**','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/GgufOptimizationProductionAuthorityProvider.cs','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/GgufCompatibilityInputProjector.cs','IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/**','scripts/gguf-runtime/Invoke-GgufChatVerification.ps1','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/**','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/GgufOptimizationProductionAuthorityTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/**','tests/UnitTests/GraniteEdgeAI.GgufRuntime.Tests/**','tests/IntegrationTests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(gguf): execute v3 plans and launch verified chat"
```

Expected: the process filter discovers its required cases with zero Failed, Skipped, Inconclusive, or NotExecuted outcomes; quantise-to-admitted-output and Chat launch pass; cancellation/failure publish nothing; original SHA remains unchanged.

## Wave 3 — bounded OpenVINO import and v3 adaptation

### Task 12: Freeze the complete route-owned OpenVINO closure

**Files:**
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationImportManifestTests.cs`

- [ ] **Step 1: Freeze the O1 manifest and forbid stale authorities**

Record exact blobs from `f0189ed187ba900f27bade5fde282ae4e99e8d7b`. The allowlist is the explicit route-owned set in Step 3. Keep the machine component `Pending` in this documentation-only task. Explicitly forbid O1 ModelImport, ModelInspection, HardwareInspection, ModelHardwareCompatibility, Onboarding, MainWindow, App/resources/navigation, whole project/solution files, v1/v2 core/contracts/tests, `.github/workflows`, research/evidence, direct destination publication, and any second UI/coordinator/selector/theme/gallery. Mark `OpenVinoV2TestPayload.cs` excluded. Import only these O1 contract-test sources: `ArchitectureBoundaryTests.cs`, `DependencyLockContractTests.cs`, `FixtureContractTests.cs`, `GpuDeviceContractTests.cs`, `ProtocolJsonTests.cs`, `ProtocolSequenceTests.cs`, `SupportCodeTests.cs`, and `Task7ProtocolExtensionTests.cs`; explicitly exclude the pinned O1 `packages.lock.json` and create the current project file around the eight sources. Exclude legacy `WorkflowContractTests.cs`, `TurboQuantSourceContractTests.cs`, `HandoffBundleContractTests.cs`, and `PackagingContractTests.cs` because they assert forbidden O1 workflows/evidence topology. New integration-owned packaging and UCL evidence tests replace those responsibilities in Tasks 13 and 17.

- [ ] **Step 2: Write failing closure and single-prompt-router tests**

Extend manifest tests to require all three Prompting primitives and assert `PromptRouteRegistry` is only a route adapter registry behind the single shared Chat destination; it must not own a page or navigation target.

- [ ] **Step 3: Record the complete allowed source and destination sets**

Populate exact source blob/destination entries for `shared/GraniteEdgeAI.OpenVino.Contracts/**`, `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**`, `Features/OpenVinoRoute/**`, the three exact `Features/Prompting` files, all three workers, all three third-party locks, `scripts/openvino/**`, `tests/TestFixtures/OpenVINO/**`, named test/process-fixture projects, and `OpenVino.WorkerPackaging.targets`. Record each required project/solution/packaging edit but do not import source in this task.

- [ ] **Step 4: Run manifest/compatibility tests before source import**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OptimizationImportManifestTests'
dotnet test --project $core --configuration Release
git diff --check
```

Expected: manifest and current compatibility tests pass with non-zero discovery and no source import exists yet.

- [ ] **Step 5: Commit the reviewed immutable closure**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json','docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationImportManifestTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "docs(openvino): freeze verified route closure"
```

Expected: exactly the two manifest files and their focused test are committed.

### Task 13: Adapt OpenVINO to the shared v3 coordinator and registry

**Files:**
- Import: `shared/GraniteEdgeAI.OpenVino.Contracts/**`
- Import: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**`
- Import: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/**`
- Import: `IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptRouteContract.cs`
- Import: `IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptRouteRegistry.cs`
- Import: `IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptSessionPresenter.cs`
- Import: `workers/OpenVinoConverter.Worker/**`
- Import: `workers/OpenVinoOfficial.Worker/**`
- Import: `workers/OpenVinoTurboQuant.Worker/**`
- Import: `third-party/openvino-converter/**`
- Import: `third-party/openvino-official/**`
- Import: `third-party/openvino-turboquant/**`
- Import: `scripts/openvino/**`
- Import: `tests/TestFixtures/OpenVINO/**`
- Import: `IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets`
- Create: `IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets`
- Create: `IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets`
- Import/adapt: OpenVINO contract/unit/integration/process-fixture tests enumerated in the Task 12 manifest, with only its eight exact contract-test sources and with the four legacy workflow/evidence-topology contract tests plus the pinned O1 `packages.lock.json` explicitly absent
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj`
- Modify minimally: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify minimally: `IBM Granite with TurboQuant (Intel).slnx`
- Modify minimally: `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationPlanAdapter.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCapabilityProjector.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCandidate.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationProvenance.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationService.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/SealedOpenVinoOptimizationPipeline.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/OpenVinoExecutionPayloadComposer.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/OpenVinoOptimizationExecutor.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackagedToolContextResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoProductionComposition.cs`
- Modify: `scripts/gguf-runtime/Invoke-GgufChatVerification.ps1`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json`
- Modify: `docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md`
- Create: `docs/handoffs/import-patches/openvino-v3-adaptation.patch`
- Create: `docs/handoffs/import-patches/openvino-shared-integration.patch`
- Create: `docs/handoffs/import-patches/gguf-verifier-openvino-integration.patch`
- Test: OpenVINO optimisation unit and E2E tests in their imported projects
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoProductionCompositionTests.cs`

- [ ] **Step 1: Import the exact manifest and apply minimal registrations**

Import every Task 12-approved blob, excluding `OpenVinoV2TestPayload.cs` and the pinned O1 contract-test `packages.lock.json`. Preserve unmodified entries as `Exact`. Use three mechanically independent patch groups: `openvino-v3-adaptation.patch` is based only on O1 `f0189ed...` route-owned blobs; `openvino-shared-integration.patch` is based only on the immediately preceding integration commit's current project/solution/UnitTests blobs and uses explicit `Created` entries for integration-owned composers, executors, packaging targets, and exactly `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackagedToolContextResolver.cs`, `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoProductionComposition.cs`, `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoProductionCompositionTests.cs`, and `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj` from `/dev/null`; no sibling path is authorized. `gguf-verifier-openvino-integration.patch` is based only on the exact Task 11 GGUF verification-script blob. Record each base commit:path/blob (when present), base tree, creation absence proof, patch SHA-256, and final result SHA-256 before changing OpenVino to `Verified`; mixing files from different bases in one patch group is prohibited.

Add x64 project references to OpenVINO Contracts and WorkerClient, compile-time `AssemblyMetadata` for the converter, official-worker, and TurboQuant-worker manifest SHA-256 values, and import all three packaging targets after existing worker imports. `OpenVino.ConverterPackaging.targets` and `OpenVino.TurboQuantPackaging.targets` follow the same fail-closed design as the official-worker target: explicit stage directory, lowercase manifest SHA-256, regular-file/non-reparse closure, manifest/member hash verification, no additions, and distinct package-relative directories `OpenVino\Converter\Worker`, the imported official target's exact `OpenVino\Official\Worker`, and `OpenVino\TurboQuant\Worker`. Release/native evidence requires converter, official, and TurboQuant packaging; missing or wrong stage/hash fails. For source-only packaged tests, the UnitTests application reference may set `OpenVinoConverterPackagingRequired=false`, `OpenVinoOfficialWorkerPackagingRequired=false`, `OpenVinoTurboQuantPackagingRequired=false`, and `GenerateAppxPackageOnBuild=false` only when `CrossRouteNativeEvidenceBuild != true`; never use those bypasses for Release/package evidence. Add the eight OpenVINO projects/tests to `.slnx` with AnyCPU for contracts and x64 mappings for native projects. Extend the already integration-owned GGUF verification script only through its separate patch group by adding and forwarding all three OpenVINO packaging-required controls; process tests prove exact forwarding, rejection of invalid combinations, and that `CrossRouteNativeEvidenceBuild=true` cannot disable any required package.

`OpenVinoPackagedToolContextResolver` is the sole path-private production resolver. It starts from `Package.Current.InstalledLocation` internally, resolves only the three fixed package-relative directories, rejects reparse/escape/missing/additional files, rereads each canonical manifest and every member hash, and requires each manifest digest to equal its corresponding assembly metadata before returning sealed converter/official/TurboQuant tool contexts. No path or package object leaves this app-layer type. `OpenVinoProductionComposition` consumes those sealed contexts to construct exactly one `OpenVinoRouteService`, `SealedOpenVinoOptimizationPipeline`, worker client, and optional TurboQuant adapter; it never calls O1's excluded `ModelInspectionServiceComposition` or infers a root. A mismatch makes only OpenVINO unavailable with a bounded support code. Unit/process tests cover exact success, each missing/wrong manifest/member/metadata/root, reparse escape, swap between tool roles, double registration, and no path-bearing public/log output. Task 14 consumes this composition, and Task 16 registers the one resulting route authority.

- [ ] **Step 2: Rewrite tests to demand v3-only exact execution**

Remove the stale V2 payload helper and assert: v3 only; exact route/payload/capability/build identity; complete binding/config digest at confirmation, staging, launch, promotion; current v3 execution authorities; no legacy registry/direct publish; staging-only output; runtime-only has no artifact; no TurboQuant candidate without exact admitted authority; slash-bearing build identity remains byte-identical.

- [ ] **Step 3: Run the focused OpenVINO optimisation filter and verify red**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false --filter 'FullyQualifiedName~GraniteEdgeAI.OpenVino.Tests.Optimization'
```

Expected: non-zero discovered failures referencing v2/direct-publication behavior.

- [ ] **Step 4: Port the adapter to exact v3 semantics**

Make `OpenVinoExecutionPayloadComposer` the only current capability-to-payload composer. `OpenVinoOptimizationExecutor` accepts the exact v3 plan and sealed private context, revalidates all bindings at each boundary, calls the route service with operation-owned staging handles rather than raw source/destination paths, and returns only a sealed candidate or runtime-profile result. Remove coordinator reachability to `OpenVinoOptimizationLegacyRegistryV1`, `OptimizeLegacyV1Async`, v1/v2 digest/issuer paths, and direct `ConversionTransaction.Publish()`.

- [ ] **Step 5: Map progress/failures and preserve originals**

Normalize route events to the shared seven stages. Map every native/tool/validation exception to an existing bounded `OptimizationSupportCode`; never forward exception/tool text. Validate conversion/runtime profile, smoke-test, reinspect, rehash the original, and allow the shared registry alone to promote persistent output.

- [ ] **Step 6: Run OpenVINO unit/integration gates and commit**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj -c Release -p:UseAppHost=false
dotnet test tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj -c Release -p:UseAppHost=false --filter 'FullyQualifiedName~OptimizationEndToEndTests'
dotnet test --project $core --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OpenVinoProductionCompositionTests'
git diff --check
$reviewPathspec = Join-Path $env:TEMP 'geai-task13-pathspec.bin'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component OpenVino -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component OpenVino -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(openvino): import route and execute exact v3 plans"
```

Expected: all locally available tests pass. A native skipped/unavailable converter, worker, or prompting stage remains explicitly blocked for Task 17’s UCL run.

### Task 14: Complete the path-private OpenVINO Import → Inspection → Hardware → Compatibility seam

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/IModelInspectionPageSession.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/GgufModelInspectionPageSession.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionDisplayDescriptor.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionPageSessionSnapshot.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionPageCommand.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/ModelInspectionViewModel.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/IOpenVinoModelInspectionPort.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/IOpenVinoImportCustody.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoModelInspectionPort.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoModelInspectionPageSession.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoModelInspectionHandoffAdapter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackageCustodyRegistry.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoOptimizationSourceResolver.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoCurrentModelChatLaunchAuthority.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/OpenVinoCompatibilityInputProjector.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.OpenVinoInspection.cs`
- Modify one authoritative shell file at exactly two named hooks: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/OpenVinoModelInspectionPageSessionTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoCurrentModelChatLaunchAuthorityTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoCompatibilityInputProjectorTests.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingOpenVinoInspectionNavigationTests.cs`
- Extend: existing Model Import operation/privacy tests and imported O1 inspection/route tests

- [ ] **Step 1: Write failing import-custody and route-selection tests**

Prove that picker and Explorer drop still converge only through `ModelImportPage.SubmitInputAsync`; `OpenVinoInspectionRequestedEventArgs` contains only `ModelSelectionOperationId` and display name; the shell retrieves the accepted folder only through `TryGetAcceptedFolderLocalPath` for the currently attached page and matching operation; stale, cancelled, replaced, or detached operations are rejected. Keep `SourceModelConversionRequestedEventArgs` separate: a source-model folder produces conversion-required intent and never enters this inspected-package seam.

```csharp
[TestMethod]
public async Task AcceptedOpenVinoFolder_UsesPrivateOperationCustody()
{
    ModelImportPage page = CreatePage();
    OpenVinoInspectionRequestedEventArgs? request = null;
    page.OpenVinoInspectionRequested += (_, value) => request = value;

    await page.SubmitInputAsync(ValidOpenVinoFolderInput());

    Assert.IsNotNull(request);
    Assert.IsTrue(page.TryGetAcceptedFolderLocalPath(request.OperationId, out string? localPath));
    Assert.IsFalse(string.IsNullOrWhiteSpace(localPath));
    Assert.IsFalse(request.GetType().GetProperties().Any(
        property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)));
}
```

- [ ] **Step 2: Write failing single-page and exact-handoff tests**

Introduce a route-neutral page-session boundary; do not construct a synthetic GGUF `ModelInspectionRequest` for an OpenVINO folder and do not add a second XAML page or theme. The existing GGUF constructor wraps its unchanged request/service in `GgufModelInspectionPageSession`. The page accepts only `IModelInspectionPageSession`, renders the existing `ModelInspectionPagePresentation`, and emits the current internal six-field `ModelInspectionHandoff` only after the active session atomically claims it.

```csharp
internal interface IModelInspectionPageSession : IDisposable
{
    ModelInspectionDisplayDescriptor Display { get; }
    ModelInspectionPageSessionSnapshot Snapshot { get; }
    event EventHandler? SnapshotChanged;
    Task StartAsync(CancellationToken cancellationToken);
    bool TryExecute(ModelInspectionPageCommand command);
    bool TryClaimHardwareHandoff(out ModelInspectionHandoff? handoff);
}
```

`ModelInspectionPageSessionSnapshot` contains only the current `ModelInspectionPagePresentation`, footer status, run-active flag, and monotonic generation; it contains no source path, worker event, exception, or route-specific native object. The command enum is exactly `Cancel`, `Retry`, `ChooseAnother`, `ToggleDetails`, and `CheckHardware`. The descriptor contains route kind plus path-free model name/format/size display values only. `GgufModelInspectionPageSession` wraps the current service, ViewModel, render coordinator, and presentation factory without changing GGUF classification. Refactor `ModelInspectionPresentationFactory` to consume the descriptor for display copy while leaving GGUF result/evidence classification unchanged. Golden presentation tests must prove every pre-existing GGUF fixture is byte-for-byte equal before and after this refactor.

- [ ] **Step 3: Implement the bounded OpenVINO inspection port red-green**

`OpenVinoModelInspectionPort` is the only shell-callable entry. It accepts the operation ID plus an `IOpenVinoImportCustody` implemented by the currently attached Model Import page; the port atomically claims the matching private path lease itself, so the raw directory never enters the shell handler or navigation state. Its route service comes only from the already verified Task 13 `OpenVinoProductionComposition`; the port cannot construct or accept a caller-supplied tool root. It invokes `OpenVinoRouteService.InspectAsync` and returns a session—not a path, worker event, or exception. `OpenVinoModelInspectionPageSession` maps static/native inspection progress and the closed O1 outcome/support codes into the existing five-row Model Inspection presentation states. A native validation failure, package drift, cancellation, protocol failure, missing worker/build evidence, or non-ready outcome cannot issue a handoff.

On `Ready` or `ReadyWithWarnings`, `OpenVinoModelInspectionHandoffAdapter` validates and copies the exact six logical O1 values—schema version, handoff UUID, run UUID, eligible outcome, lowercase primary `openvino_model.bin` SHA-256, and positive primary-model length—into the current internal `ModelInspectionHandoff`. Serialize both contracts canonically in the test and assert field-for-field equality; never reinterpret the package-manifest digest as the model digest.

- [ ] **Step 4: Retain one revocable package lease behind the path-free handoff**

`OpenVinoPackageCustodyRegistry` owns the O1 `OpenVinoRouteHandoffLease`, immutable static/native package evidence, and exact capability snapshot keyed by the full tuple `(modelInspectionHandoffId, modelInspectionRunId, modelSha256, modelLengthBytes, route, packageManifestSha256)`. It supports one atomic register, one hardware-binding claim, proved rollback, explicit invalidation, and disposal. `OpenVinoOptimizationSourceResolver` implements the Task 8 private source-resolution port and returns only a sealed package/source lease after the full tuple, Hardware run/snapshot, plan/configuration, and current package manifest revalidate. The same registry supplies `OpenVinoCurrentModelChatLaunchAuthority`, which validates the exact current OpenVINO execution payload/digest and calls the O1 prompt route; Task 16 registers it once. It exposes no path-bearing public/presentation member. Back, re-import, stale navigation, failed hardware inspection, cancellation, or shell retirement disposes the exact lease; no other entry is removed.

- [ ] **Step 5: Project OpenVINO into the existing Hardware and Compatibility flow**

Replace only the body of `ModelImportPage_OpenVinoInspectionRequested` with a call into `OnboardingShellPage.OpenVinoInspection.cs`. Navigate the same `StageFrame` to the same `ModelInspectionPage`; then use the existing `ModelInspectionPage_HardwareInspectionRequested` and `HardwareInspectionPage` unchanged. At hardware completion, route by the custody registry: GGUF continues through `GgufCompatibilityInputProjector`; OpenVINO claims its package lease and uses `OpenVinoCompatibilityInputProjector` to combine the exact O1 model/package/capability evidence with the exact `HardwareInspectionHandoff` and issue the shared compatibility input. Missing, stale, mismatched, or unverified evidence fails closed and cannot create `CompatibilityPage`.

The shell partial owns this route switch and cleanup. The existing authoritative shell file receives exactly two calls—`TryBeginOpenVinoInspection(...)` in the import handler and `TryPrepareOpenVinoCompatibility(...)` beside the current GGUF projector branch. Do not duplicate navigation, inspection, hardware, compatibility, source-custody, or optimization code.

- [ ] **Step 6: Run the full pre-optimization route gates and commit exact paths**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false --filter 'FullyQualifiedName~Inspection|FullyQualifiedName~OpenVinoRouteServiceTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OpenVinoModelInspectionPageSessionTests|FullyQualifiedName~OpenVinoCurrentModelChatLaunchAuthorityTests|FullyQualifiedName~OnboardingOpenVinoInspectionNavigationTests|FullyQualifiedName~OpenVinoCompatibilityInputProjectorTests|FullyQualifiedName~ModelImportOperationLifecycleTests|FullyQualifiedName~ModelImportPrivacyBoundaryTests'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~ModelInspectionPresentationFactoryTests|FullyQualifiedName~OnboardingFolderInspectionNavigationTests|FullyQualifiedName~GgufCompatibilityInputProjectorTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/IModelInspectionPageSession.cs','IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/GgufModelInspectionPageSession.cs','IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionDisplayDescriptor.cs','IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionPageSessionSnapshot.cs','IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Application/ModelInspectionPageCommand.cs','IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs','IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ViewModels/ModelInspectionViewModel.cs','IBM Granite with TurboQuant (Intel)/Features/ModelInspection/Presentation/ModelInspectionPresentationFactory.cs','IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/**','IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Infrastructure/OpenVinoCompatibilityInputProjector.cs','IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.OpenVinoInspection.cs','IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/OpenVinoModelInspectionPageSessionTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoCurrentModelChatLaunchAuthorityTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelHardwareCompatibility/OpenVinoCompatibilityInputProjectorTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingOpenVinoInspectionNavigationTests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/**','tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/**') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(openvino): complete import inspection compatibility seam"
```

Expected: both GGUF and OpenVINO reach the same Hardware and Compatibility pages; the existing OpenVINO-unavailable rejection is gone only when the verified port is registered; source folders remain conversion-only; every filter discovers tests and passes; the commit contains no XAML/theme duplicate and no path-bearing boundary.

## Wave 4 — destinations, navigation, visual parity, and final proof

### Task 15A: Implement the single verified Chat router

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/DestinationSupportCode.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/DestinationResult.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OptimizationChatRouter.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationChatRouterTests.cs`

- [ ] **Step 1: Write failing closed-code and Chat-lease tests**

```csharp
internal enum DestinationSupportCode
{
    None,
    CancelledByUser,
    ResultUnavailable,
    IdentityMismatch,
    UnsafeDestination,
    DestinationChanged,
    InsufficientSpace,
    PublicationFailed,
    UnexpectedFailure
}

internal sealed record DestinationResult(
    bool Succeeded,
    DestinationSupportCode SupportCode);
```

Test exactly those nine values, one active Chat lease, failure/cancellation releases it, a Chat failure retains the verified result, and all exceptions become local bounded copy without paths or adapter text.

- [ ] **Step 2: Implement one route-dispatched Chat intent**

Current-model Chat calls `ICurrentModelChatLaunchAuthority` and translates its closed support code to `DestinationSupportCode`. Optimized GGUF acquires a verified registry output/source lease and calls `GgufOptimizationChatLaunchFactory`. OpenVINO acquires a verified package/runtime-profile lease and calls the imported prompting service through the reconciled `PromptRouteRegistry`. Reject mixed route/plan/config/output identities. The router owns no UI and returns only `DestinationResult`.

- [ ] **Step 3: Run Chat security tests and commit exact files**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OptimizationChatRouterTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/DestinationSupportCode.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/DestinationResult.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OptimizationChatRouter.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationChatRouterTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(optimisation): route verified chat destinations"
```

Expected: current GGUF, optimized GGUF, and OpenVINO Chat tests discover and pass; mixed identities and unavailable route authorities fail closed.

### Task 15B: Implement atomic GGUF Save

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/IDestinationPicker.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OptimizationDestinationService.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/GgufModelExporter.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/GgufModelExporterTests.cs`

- [ ] **Step 1: Write failing GGUF atomic-publication tests**

Cover picker cancellation, one active Save lease, explicit overwrite only, unconfirmed collision, reparse/link target, changed parent identity, cross-volume replacement, insufficient space, random non-identifying `CreateNew` temp, stream hash/length, flush, atomic rename/replace, cancellation cleanup, preservation of the existing destination, and verified-result retention after destination failure.

- [ ] **Step 2: Implement brokered GGUF Save**

Use an OS picker/handle through `IDestinationPicker`; never treat display text as a path. Immediately before writing, recheck free space and destination-parent identity. Create a random same-directory temporary item with CreateNew semantics, stream and hash, flush file and directory metadata where supported, then perform one atomic rename/replace after explicit overwrite approval. Remove only the exact owned temporary item on cancellation/failure.

- [ ] **Step 3: Run GGUF Save tests and commit exact files**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GgufModelExporterTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/IDestinationPicker.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OptimizationDestinationService.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/GgufModelExporter.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/GgufModelExporterTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(gguf): save verified model atomically"
```

Expected: every GGUF Save security/cancellation/retry case passes with non-zero discovery and failed publication leaves the registry result available.

### Task 15C: Implement complete OpenVINO ZIP Save

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OpenVinoArchiveEntry.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OpenVinoPackageExporter.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OpenVinoPackageExporterTests.cs`

- [ ] **Step 1: Write failing OpenVINO ZIP safety tests**

Cover exact manifest membership; license/notice preservation; root/drive/UNC/backslash/empty/dot/traversal/colon/device/control/link rejection; exact and ordinal-ignore-case duplicate rejection; per-entry size/SHA validation; final ZIP hash/flush/atomic publication; and no Save action for runtime-only results.

```csharp
[DataRow("../model.xml")]
[DataRow("C:/model.xml")]
[DataRow("folder\\model.xml")]
[DataRow("folder/CON")]
[DataRow("folder/model.xml:stream")]
[TestMethod]
public void UnsafeArchiveEntryIsRejected(string entry)
    => Assert.ThrowsException<UnsafeDestinationException>(() => OpenVinoArchiveEntry.Require(entry));
```

- [ ] **Step 2: Build the archive from the sealed manifest, not directory enumeration**

Acquire the verified OpenVINO output lease and enumerate its immutable manifest entries only. Reopen each file through the sealed custody API, verify entry identity/length/SHA before and during streaming, preserve required license/notice files, write deterministic relative ZIP entry names, finalize and flush through the Task 15B atomic destination transaction, then hash the completed ZIP. A changed/missing/extra entry, runtime-only result, or lease mismatch publishes nothing.

- [ ] **Step 3: Run OpenVINO Save tests and commit exact files**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OpenVinoPackageExporterTests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OpenVinoArchiveEntry.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Destinations/OpenVinoPackageExporter.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OpenVinoPackageExporterTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(openvino): save complete verified package"
```

Expected: complete persistent packages save as one verified `.zip`; runtime-only results expose no Save; every unsafe-entry and drift case fails before publication.

### Task 16: Wire one shell destination and enforce semantic no-duplication

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.Optimization.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify after screenshot audit when a red visual test proves a defect: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml.cs`
- Modify after screenshot audit when a red visual test proves a defect: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/**`
- Modify after screenshot audit when a red visual test proves a defect: `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Presentation/OptimizationTheme.xaml`
- Create: `docs/handoffs/2026-08-26-cross-route-optimisation-ownership.json`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationOwnershipTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OptimizationNavigationTests.cs`
- Extend: ModelOptimization accessibility/responsive/visual contract tests

- [ ] **Step 1: Write failing ownership and navigation tests**

The ownership JSON must name exactly one permitted path/type/resource/project item/navigation target/registration for selector, plan issuer, optimization page, theme, presentation state, reducer, coordinator, executor dispatcher, output registry, destination router, and shell destination. Scan project includes, XAML roots, merged dictionaries, intent handlers, dispatcher registrations, and navigation calls; reject v1/v2 reachability and equivalent unowned implementations.

```csharp
[TestMethod]
public void SharedOptimizationResponsibilitiesHaveOneReachableOwner()
{
    OwnershipGraph graph = OwnershipGraph.Load(RepositoryRoot);
    foreach (OwnershipRule rule in graph.Rules)
        Assert.AreEqual(1, graph.ReachableImplementations(rule).Count, rule.Responsibility);
}
```

- [ ] **Step 2: Run ownership/navigation tests and verify failure**

Build packaged tests and run `FullyQualifiedName~OptimizationOwnershipTests|FullyQualifiedName~OptimizationNavigationTests`. Expected: missing ownership map/navigation destination failure.

- [ ] **Step 3: Wire the typed handoff transactionally**

In `OnboardingShellPage.Optimization.cs`, resolve and register exactly one verified `OpenVinoProductionComposition`, one `GgufCurrentModelChatLaunchAuthority`, and one `OpenVinoCurrentModelChatLaunchAuthority` in the single registries; absent or mismatched route packages leave only that route unavailable. Accept only current `OptimizationJourneyEntryContext`, create one coordinator/page, attach handlers exactly once, navigate the existing `StageFrame`, and advance to `OnboardingStage.ConfigureModel` only after navigation succeeds. On Back/replan, detach/retire the page and return to the existing Compatibility instance or obtain fresh evidence. On Done/Chat, advance only after the destination succeeds. Do not edit `MainWindow` for optimization navigation.

- [ ] **Step 4: Wire page commands to coordinator leases**

Confirmation passes the currently displayed plan ID and configuration SHA. Progress, Cancel, Retry, Chat, `ChatWithOriginal`, Save, Done, and Back are typed `OptimizationCommand` values. `ChatWithOriginal` exists only in Optional cancellation/failure presentation, dispatches the retained current-model handoff through the current-model authority, and never uses an optimisation output. Disable Confirm/Retry/Back while execution owns the generation; disable only the active Chat or Save action while its destination lease is active. Ignore commands from a retired page instance.

- [ ] **Step 5: Perform the three-pass screenshot audit and prove shared responsive visuals**

First hash all six approved visual oracles in design §10.3 and stop visual acceptance on any mismatch. Build fixtures for confirmation; all seven active/completed stages; cancel; failure; replan; persistent success; runtime-only success; GGUF/OpenVINO content; compact, standard, wide; High Contrast; and actual Windows 200% text. Pass 1 records every discrepancy in structure, hierarchy, centring, whitespace rhythm, card geometry, typography, icon/text/status alignment, button treatment, focus/keyboard behavior, scrolling, contrast, clipping and route parity. Pass 2 adds a failing visual/layout/accessibility test for each objective defect, then makes the smallest XAML/token/presentation correction. Pass 3 recaptures every affected state, compares it again to the approved oracle and its corresponding GGUF/OpenVINO state, and repeats red-green until no unresolved defect remains. Assert one centred responsive column, light backgrounds, current card/button tokens, equal progress row heights, vertically centred symbols/names/status, one full-width disclosure, natural scrolling, and no clipped/overlapping/essential-ellipsis content. Capture native screenshots into the existing approved screenshot convention; do not add route-specific screens and do not change backend behavior for visual convenience.

- [ ] **Step 6: Run UI/navigation/ownership tests and commit**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OptimizationNavigationTests|FullyQualifiedName~OptimizationOwnershipTests|FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.ModelOptimization'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.Optimization.cs','IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OptimizationPage.xaml.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/**','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Presentation/OptimizationTheme.xaml','docs/handoffs/2026-08-26-cross-route-optimisation-ownership.json','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/**','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OptimizationNavigationTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "feat(onboarding): complete shared optimisation journey"
```

Expected: one shell, one page/theme/coordinator/router/registration, and all visual states pass.

### Task 17: Prove both complete native journeys and close integration evidence

**Files:**
- Create: `tests/runsettings/OneWorker.runsettings`
- Create: `scripts/verification/Invoke-CrossRouteFinalVerification.ps1`
- Create: `scripts/verification/Capture-CrossRouteVisualEvidence.ps1`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/CrossRouteGgufJourneyE2ETests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/CrossRouteOpenVinoJourneyE2ETests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/CrossRouteVisualCaptureTests.cs`
- Create: `docs/handoffs/2026-08-26-cross-route-optimisation-final-handoff.md`
- Extend only when required by a red test: the exact owning GGUF/OpenVINO integration test project
- Modify only if a test exposes a defect: the exact owning source/test path from Tasks 1–16

- [ ] **Step 1: Write the two failing complete-journey tests**

Each route suite contains four complete shared-shell journeys: (1) current fit `Import -> Inspect -> Hardware -> Compatibility -> Chat with current model`; (2) current fit -> Optional optimise -> cancel/fail -> revalidated `Chat with original model`; (3) optimisation required -> Optimize -> Validate/Reinspect -> Chat; and (4) the same required path ending in `.gguf` Save for GGUF or complete `.zip` Save for OpenVINO. It also asserts required cancellation/failure has no original-Chat action, and no-fit/inconclusive has neither Chat nor Optimize continuation. Test names must make the mutation explicit: wrong route, stale handoff, altered plan/configuration, changed original, unregistered destination, incomplete ZIP, path-bearing payload, or second shell/page owner causes failure. Use the real route components, current-model authorities, shared pages, reducer/coordinator, and shell navigation; fake only OS picker/process boundaries when the native executable is tested separately.

Run both packaged filters before adding any missing seam. Expected: non-zero discovered failures at the first incomplete production boundary, not a fixture/setup error. Fix that owner using a focused red-green cycle; never add a bypass to the journey test.

- [ ] **Step 2: Create the serial runner and verification orchestrator**

`OneWorker.runsettings` is checked in and contains exactly one MSTest worker with `MethodLevel` scope. `Invoke-CrossRouteFinalVerification.ps1` requires these parameters and validates them before running anything:

```powershell
param(
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [Parameter(Mandatory)][string]$OpenVinoConverterStageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$OpenVinoConverterManifestSha256,
    [Parameter(Mandatory)][string]$OpenVinoOfficialWorkerStageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$OpenVinoOfficialWorkerManifestSha256,
    [Parameter(Mandatory)][string]$OpenVinoTurboQuantWorkerStageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$OpenVinoTurboQuantWorkerManifestSha256,
    [Parameter(Mandatory)][string]$OpenVinoControlledFixtureDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$OpenVinoControlledFixtureManifestSha256,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$OpenVinoControlledPackageManifestSha256,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$OpenVinoControlledModelSha256,
    [Parameter(Mandatory)][ValidateRange(1,[long]::MaxValue)][long]$OpenVinoControlledModelLengthBytes,
    [Parameter(Mandatory)][string]$GgufQuantizerStageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$GgufQuantizerManifestSha256,
    [Parameter(Mandatory)][string]$StandardVisualEvidenceDirectory,
    [Parameter(Mandatory)][string]$Text200VisualEvidenceDirectory,
    [Parameter(Mandatory)][string]$HighContrastVisualEvidenceDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory)
```

The script requires a clean worktree and exact HEAD before testing, records `git rev-parse HEAD` and `git rev-parse 'HEAD^{tree}'`, creates a fresh owned evidence directory, verifies converter `converter-manifest.json`, official `worker-manifest.json`, TurboQuant `worker-manifest.json` plus its referenced runtime/patch closure, the controlled OpenVINO fixture's outer `manifest.json`, static package-manifest digest, primary-model SHA/length/membership, and the GGUF quantiser manifest against the caller-supplied identities, then invokes each command below. Before invoking any native or packaged process it writes an internal canonical closure snapshot for all five external inputs: resolved root identity, manifest bytes and digest, sorted relative member names, member lengths and SHA-256 values, and—for the controlled fixture—the distinct outer-manifest, package-manifest, primary-model SHA and primary-model length bindings. The outer fixture manifest SHA, package-manifest digest, model SHA, and model length are four distinct values and may never be substituted for one another. It parses every TRX as XML and requires the exact mandatory test names, discovered/executed count greater than zero, `failed=0`, and zero Skipped, Inconclusive, NotExecuted, or missing outcomes for native/package evidence. After every native/package/visual command and again immediately before evidence success, it independently rebuilds each canonical closure snapshot from disk and requires byte-for-byte equality with the corresponding pre-run snapshot; a missing, added, renamed, reparse-point, length-changed, or hash-changed member invalidates the run. It hashes every TRX/build/package/native report and finally proves HEAD, tree, clean status, and all five external-input closures did not change. It may use `SkipApplicationBuild=true` only for the explicitly labelled GGUF source/component regression; it must not disable converter/official/TurboQuant/GGUF packaging, app-package generation, or application build for Release/native/package evidence.

`Capture-CrossRouteVisualEvidence.ps1` accepts `-ExpectedCommit`, `-EvidenceDirectory`, and `-EnvironmentProfile Standard|Text200|HighContrast`. It refuses a dirty or mismatched worktree, queries the active Windows DPI/text scale and High Contrast state, and rejects a profile whose observed environment does not match its name: Standard requires text scale 100% and High Contrast off; Text200 requires text scale 200% and High Contrast off; HighContrast requires High Contrast on and records its actual text scale. Display DPI may vary but is always recorded. It sets only a private evidence-directory environment variable, invokes the packaged `FullyQualifiedName~CrossRouteVisualCaptureTests` filter through the fresh-recipe helper, and validates the generated JSON manifest. `CrossRouteVisualCaptureTests` uses the existing WinUI `WinUiRenderHost`/`RenderTargetBitmap` harness for component-state captures at 1024×768, 1440×1024, and 1920×1080, and a real packaged WinUI `Window` containing exactly one `OnboardingShellPage` for full-shell captures at the supported minimum 600×900 and the runner's maximized work area. It drives real navigation for both routes and the complete current-fit/required/no-fit/inconclusive/confirmation/seven-stage/cancel/failure/replan/result/destination inventory. Full-shell assertions prove one footer/stepper, one scroll owner, heading/action reachability by scrolling, no duplicate shell, and no clipped or overlay-hidden essential content. UIA tests prove tab order, keyboard activation, focus visibility/restoration, unique names, roles, `AutomationProperties`, live status announcements, and High Contrast resources. Each PNG record includes route, state, component-or-shell viewport, pixel dimensions, DPI/text scale, High Contrast, SHA-256, oracle SHA-256/scope, and immutable tested commit/tree. It fails on missing states, wrong dimensions, clipping/overlap, dark surfaces in the light route UI, route-owned structural drift, stale copy/action behavior, or identical hashes for viewports expected to reflow. The script writes only outside the repository and rechecks clean HEAD/tree afterward.

- [ ] **Step 3: Commit all code and tests before collecting final evidence**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~CrossRouteGgufJourneyE2ETests|FullyQualifiedName~CrossRouteOpenVinoJourneyE2ETests'
git diff --check
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('tests/runsettings/OneWorker.runsettings','scripts/verification/Invoke-CrossRouteFinalVerification.ps1','scripts/verification/Capture-CrossRouteVisualEvidence.ps1','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/CrossRouteGgufJourneyE2ETests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/CrossRouteOpenVinoJourneyE2ETests.cs','tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/CrossRouteVisualCaptureTests.cs') -StageVerified
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "test(optimisation): prove both complete route journeys"
git status --short
```

Expected: both complete-journey filters pass with non-zero discovery and the worktree is clean. If a red test required production correction, commit that focused correction and its test first; then make this test-harness commit. Final evidence may test only this clean immutable tip.

- [ ] **Step 4: Run contract, component, Debug, packaged, and GGUF native regressions**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test --project $core --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1 -OpenVinoConverterPackagingRequired:$false -OpenVinoOfficialWorkerPackagingRequired:$false -OpenVinoTurboQuantPackagingRequired:$false -GgufQuantizerPackagingRequired:$false -GenerateAppxPackageOnBuild:$false -SkipApplicationBuild:$true
dotnet test tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj -c Release
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj -c Release -p:UseAppHost=false
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false
dotnet restore $tp --runtime win-x64 -p:Platform=x64 --disable-build-servers -m:1
dotnet build $tp --configuration Debug --no-restore --runtime win-x64 -p:Platform=x64 -p:BuildInParallel=false -p:OpenVinoConverterPackagingRequired=false -p:OpenVinoOfficialWorkerPackagingRequired=false -p:OpenVinoTurboQuantPackagingRequired=false -p:GgufQuantizerPackagingRequired=false -p:GenerateAppxPackageOnBuild=false --disable-build-servers -m:1
& $vs $recipe '/Platform:x64' '/Settings:.\tests\runsettings\OneWorker.runsettings' "/Logger:trx;LogFileName=packaged-full.trx" "/ResultsDirectory:$EvidenceDirectory"
```

Across Tasks 10D and 13, `Invoke-GgufChatVerification.ps1` must accept the six explicit Boolean controls `OpenVinoConverterPackagingRequired`, `OpenVinoOfficialWorkerPackagingRequired`, `OpenVinoTurboQuantPackagingRequired`, `GgufQuantizerPackagingRequired`, `GenerateAppxPackageOnBuild`, and `SkipApplicationBuild`; an unknown parameter, invalid combination, or silently dropped build property is a failure. Expected: zero failures; the full packaged run discovers at least the existing 1,431 tests plus all new tests.

- [ ] **Step 5: Prove native GGUF Chat and Save against the staged quantiser**

The final script sets `GEAI_GGUF_QUANTIZER_STAGE_DIRECTORY` and `GEAI_GGUF_QUANTIZER_MANIFEST_SHA256` from its validated arguments, then runs this exact process suite into the evidence directory:

```powershell
dotnet test tests/IntegrationTests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests/GraniteEdgeAI.GgufQuantization.WorkerProcess.Tests.csproj -c Release --filter 'FullyQualifiedName~GgufOptimizationEndToEndTests' --logger "trx;LogFileName=gguf-native-e2e.trx" --results-directory $EvidenceDirectory
```

It parses the TRX and requires the expected mandatory test names, non-zero execution, zero failures, and zero Skipped/Inconclusive/NotExecuted. The test verifies exact v3 plan/configuration, quantiser source/package/executable hash, plan-bound requantisation authorization and conditional flag, runtime identity, original unchanged, receipt-backed output, launched runtime configuration, saved output SHA/length, cancellation cleanup, and no path leakage. The maximum-efficiency format remains capability- and quality-gated; no hard-coded format substitution is accepted. The packaged GGUF cases run after the real Release UnitTests package build in Step 6, not against the source-only Debug recipe.

- [ ] **Step 6: Build Release x64 and prove native OpenVINO Chat and ZIP Save on the UCL Intel laptop**

Use the same committed integration SHA and all three verified OpenVINO stages. Build the Release UnitTests package—which contains the `CrossRoute*JourneyE2ETests` and builds the real application reference with production packaging—using Visual Studio MSBuild:

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$releaseCheckpoint = Get-Date
& $msbuild $tp /restore /target:Build /maxCpuCount:1 /verbosity:minimal /property:Configuration=Release /property:Platform=x64 /property:RuntimeIdentifier=win-x64 /property:CrossRouteNativeEvidenceBuild=true /property:GenerateAppxPackageOnBuild=true /property:AppxBundle=Never /property:AppxPackageSigningEnabled=false "/property:OpenVinoConverterStageDirectory=$OpenVinoConverterStageDirectory" "/property:OpenVinoConverterManifestSha256=$OpenVinoConverterManifestSha256" /property:OpenVinoConverterPackagingRequired=true "/property:OpenVinoOfficialWorkerStageDirectory=$OpenVinoOfficialWorkerStageDirectory" "/property:OpenVinoOfficialWorkerManifestSha256=$OpenVinoOfficialWorkerManifestSha256" /property:OpenVinoOfficialWorkerPackagingRequired=true "/property:OpenVinoTurboQuantWorkerStageDirectory=$OpenVinoTurboQuantWorkerStageDirectory" "/property:OpenVinoTurboQuantWorkerManifestSha256=$OpenVinoTurboQuantWorkerManifestSha256" /property:OpenVinoTurboQuantPackagingRequired=true "/property:GgufQuantizerStageDirectory=$GgufQuantizerStageDirectory" "/property:GgufQuantizerManifestSha256=$GgufQuantizerManifestSha256" /property:GgufQuantizerPackagingRequired=true
$releaseRecipe = '.\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe'
if (-not (Test-Path $releaseRecipe) -or (Get-Item $releaseRecipe).LastWriteTimeUtc -le $releaseCheckpoint.ToUniversalTime()) { throw 'Fresh Release UnitTests appxrecipe was not produced.' }
```

After the Release build, the final script locates the newly written Release `.build.appxrecipe`, proves its timestamp is later than the Release checkpoint and records its SHA-256, then runs these exact native and packaged suites:

```powershell
$env:GRANITE_OPENVINO_CONVERTER_STAGE = $OpenVinoConverterStageDirectory
$env:GRANITE_OPENVINO_CONVERTER_MANIFEST_SHA256 = $OpenVinoConverterManifestSha256
$env:OPENVINO_OFFICIAL_WORKER_STAGE_A = $OpenVinoOfficialWorkerStageDirectory
$env:OPENVINO_OFFICIAL_WORKER_MANIFEST_SHA256 = $OpenVinoOfficialWorkerManifestSha256
$env:OPENVINO_TURBOQUANT_WORKER_STAGE = $OpenVinoTurboQuantWorkerStageDirectory
$env:OPENVINO_UCL_CONTROLLED_FIXTURE_ROOT = $OpenVinoControlledFixtureDirectory
$env:OPENVINO_UCL_CONTROLLED_FIXTURE_MANIFEST_SHA256 = $OpenVinoControlledFixtureManifestSha256
$env:OPENVINO_TURBOQUANT_PACKAGE_ROOT = Join-Path $OpenVinoControlledFixtureDirectory 'package'
$env:OPENVINO_TURBOQUANT_PACKAGE_MANIFEST_SHA256 = $OpenVinoControlledPackageManifestSha256
$env:OPENVINO_TURBOQUANT_MODEL_SHA256 = $OpenVinoControlledModelSha256
$env:OPENVINO_TURBOQUANT_MODEL_LENGTH = $OpenVinoControlledModelLengthBytes.ToString([Globalization.CultureInfo]::InvariantCulture)
dotnet test tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj -c Release --filter 'FullyQualifiedName~OptimizationEndToEndTests|FullyQualifiedName~TurboQuantWorkerTests' --logger "trx;LogFileName=openvino-native-e2e.trx" --results-directory $EvidenceDirectory
& $vs $releaseRecipe '/Platform:x64' '/Settings:.\tests\runsettings\OneWorker.runsettings' '/TestCaseFilter:FullyQualifiedName~CrossRouteGgufJourneyE2ETests|FullyQualifiedName~CrossRouteOpenVinoJourneyE2ETests' "/Logger:trx;LogFileName=packaged-cross-route-e2e.trx" "/ResultsDirectory:$EvidenceDirectory"
```

Both TRX files must contain every enumerated mandatory case with zero Failed, Skipped, Inconclusive, or NotExecuted outcomes. The suite verifies Import → shared Inspection → Hardware → Compatibility → Optimize → Validate/Reinspect → Chat and complete portable `.zip` Save; exact converter/official/TurboQuant package membership and manifest hashes; licenses/notices; prompting; original unchanged; exact v3 bindings; and identical shared light UI state. If any converter, official-worker, TurboQuant, package, or native prerequisite is unavailable, record a blocker and do not mark OpenVINO complete.

- [ ] **Step 7: Run privacy, duplication, visual, and immutable-worktree gates**

The three visual campaigns are collected separately from the same immutable commit because changing Windows text scale or High Contrast is an operator-controlled OS action, not something the application or test script may silently mutate. On a runner already configured for each named profile, execute exactly one of these commands; each capture script rejects the wrong observed profile and re-proves the immutable commit/tree:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Capture-CrossRouteVisualEvidence.ps1 -ExpectedCommit $ExpectedCommit -EvidenceDirectory $StandardVisualEvidenceDirectory -EnvironmentProfile Standard
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Capture-CrossRouteVisualEvidence.ps1 -ExpectedCommit $ExpectedCommit -EvidenceDirectory $Text200VisualEvidenceDirectory -EnvironmentProfile Text200
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Capture-CrossRouteVisualEvidence.ps1 -ExpectedCommit $ExpectedCommit -EvidenceDirectory $HighContrastVisualEvidenceDirectory -EnvironmentProfile HighContrast
```

The final verification script treats those directories as immutable input. It validates their manifests, tested commit/tree, environment facts, complete state/route/viewport matrix, PNG hashes, and oracle hashes before running these exact audits; it saves their complete output and fails on any command error:

```powershell
dotnet test --project $core --configuration Release --filter 'FullyQualifiedName~Privacy|FullyQualifiedName~PathPrivacy|FullyQualifiedName~Boundary'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~Privacy|FullyQualifiedName~PathPrivacy|FullyQualifiedName~Boundary'
rg -n --hidden --glob '!**/bin/**' --glob '!**/obj/**' '(?i)(path|file(name)?|directory|folder|root|location|uri|source|destination|stage)' 'IBM Granite with TurboQuant (Intel)/Features/ModelImport' 'IBM Granite with TurboQuant (Intel)/Features/ModelInspection' 'IBM Granite with TurboQuant (Intel)/Features/HardwareInspection' 'IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility' 'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization' 'IBM Granite with TurboQuant (Intel)/Features/Onboarding' 'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute' 'IBM Granite with TurboQuant (Intel)/Features/GgufRuntime' 'IBM Granite with TurboQuant (Intel)/Features/Prompting' shared infrastructure workers | Tee-Object (Join-Path $EvidenceDirectory 'privacy-name-audit.txt')
rg -n --hidden --glob '!**/bin/**' --glob '!**/obj/**' 'new\s+(OptimizationPage|OptimizationJourneyCoordinator|OptimizationChatRouter|OnboardingShellPage)|x:Class="[^"]*(OptimizationPage|OnboardingShellPage)"' 'IBM Granite with TurboQuant (Intel)' | Tee-Object (Join-Path $EvidenceDirectory 'ownership-audit.txt')
git diff --check
```

The two reflection/semantic canary suites inspect every public handoff, contract, event, presentation, navigation, support-code, diagnostic, and log shape across both routes. The script applies an explicit source-scan privacy allowlist containing only private custody/resolver/packaging implementations and test fixtures; every public/presentation/navigation/log match and every unclassified alias match is fatal. It applies an exact ownership allowlist requiring one page, one coordinator/router, and one shell type; every duplicate is fatal. Compare every captured image against the six tracked approved HTML oracles using the design's exact scoped-region hierarchy and record a state-by-state audit for centring, readable scale, spacing rhythm, aligned icons/text/status, equal row heights, full-width disclosures, consistent modern buttons, focus/keyboard behavior, natural scrolling, no dark route UI, no duplicate shell, and no clipped/overlapping/hidden essential content. Any visual defect is fixed in the owning XAML/presentation test through a new red-green commit; rerun both route screenshots after each fix.

- [ ] **Step 8: Complete the three review passes**

Provide three independent reviewers the approved design, this plan, import/ownership manifests, immutable tested commit/tree, test/TRX/native evidence, and screenshots: (1) exact behavior/specification and route parity; (2) code, security, privacy, lifecycle, and supply-chain quality; (3) whole-application regression, packaging, visual/accessibility, and evidence binding. Resolve every Critical/Important finding with a focused red-green commit, repeat Steps 3–7 from the new immutable tip, and rerun all three reviews. Minor findings must be fixed or explicitly dispositioned in the handoff.

- [ ] **Step 9: Commit a docs-only immutable final handoff**

Only after the immutable evidence command returns successfully and all three reviewers pass, create `docs/handoffs/2026-08-26-cross-route-optimisation-final-handoff.md` with `apply_patch`, deriving every count and SHA from the validated evidence index rather than copied console prose. The handoff records branch/base, tested commit/tree, every commit, import blob hashes, build/test counts, TRX/report/package SHA-256 values, UCL environment and worker/quantiser manifest identities, both route E2E evidence, visual evidence, three review dispositions, privacy/duplication results, known blockers, and nonclaims. Recompute and compare each referenced artifact hash before staging. No source or test change may accompany this evidence commit.

```powershell
$testedHead = git rev-parse HEAD
$testedTree = git rev-parse 'HEAD^{tree}'
if (git status --porcelain) { throw 'The tested worktree is not clean.' }
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-CrossRouteFinalVerification.ps1 -ExpectedCommit $testedHead -OpenVinoConverterStageDirectory $env:GEAI_OPENVINO_CONVERTER_STAGE_DIRECTORY -OpenVinoConverterManifestSha256 $env:GEAI_OPENVINO_CONVERTER_MANIFEST_SHA256 -OpenVinoOfficialWorkerStageDirectory $env:GEAI_OPENVINO_OFFICIAL_STAGE_DIRECTORY -OpenVinoOfficialWorkerManifestSha256 $env:GEAI_OPENVINO_OFFICIAL_MANIFEST_SHA256 -OpenVinoTurboQuantWorkerStageDirectory $env:GEAI_OPENVINO_TURBOQUANT_STAGE_DIRECTORY -OpenVinoTurboQuantWorkerManifestSha256 $env:GEAI_OPENVINO_TURBOQUANT_MANIFEST_SHA256 -OpenVinoControlledFixtureDirectory $env:GEAI_OPENVINO_FIXTURE_DIRECTORY -OpenVinoControlledFixtureManifestSha256 $env:GEAI_OPENVINO_FIXTURE_MANIFEST_SHA256 -OpenVinoControlledPackageManifestSha256 $env:GEAI_OPENVINO_PACKAGE_MANIFEST_SHA256 -OpenVinoControlledModelSha256 $env:GEAI_OPENVINO_MODEL_SHA256 -OpenVinoControlledModelLengthBytes ([long]$env:GEAI_OPENVINO_MODEL_LENGTH_BYTES) -GgufQuantizerStageDirectory $env:GEAI_GGUF_QUANTIZER_STAGE_DIRECTORY -GgufQuantizerManifestSha256 $env:GEAI_GGUF_QUANTIZER_MANIFEST_SHA256 -StandardVisualEvidenceDirectory $env:GEAI_VISUAL_STANDARD_EVIDENCE_DIRECTORY -Text200VisualEvidenceDirectory $env:GEAI_VISUAL_TEXT200_EVIDENCE_DIRECTORY -HighContrastVisualEvidenceDirectory $env:GEAI_VISUAL_HIGH_CONTRAST_EVIDENCE_DIRECTORY -EvidenceDirectory $env:GEAI_FINAL_EVIDENCE_DIRECTORY
if ((git rev-parse HEAD) -ne $testedHead -or (git rev-parse 'HEAD^{tree}') -ne $testedTree -or (git status --porcelain)) { throw 'Repository changed during evidence collection.' }
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Assert-ChangedPaths.ps1 -Repository $PWD -AllowedPath @('docs/handoffs/2026-08-26-cross-route-optimisation-final-handoff.md') -StageVerified
if ((git diff --cached --name-only) -ne 'docs/handoffs/2026-08-26-cross-route-optimisation-final-handoff.md') { throw 'Final evidence commit is not docs-only.' }
git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m "docs(optimisation): record cross-route integration evidence"
git diff --check HEAD^ HEAD
git status --short
```

Expected: the evidence commit is the direct child of the immutable tested commit, changes exactly one Markdown file, and leaves a clean worktree. No push, PR, main merge, release publication, or user-process termination occurs as part of this plan.

## Completion gate

### Design-to-task traceability

| Approved design concern | Owning task(s) |
|---|---|
| Authority/input-branch precedence and bounded imports | 1, 6, 9, 10A, 12, 13 |
| Current-fit direct Chat and optional optimisation | 2, 4, 5, 11, 15A, 16 |
| Optimisation-required/no-fit/inconclusive behavior | 2, 3, 5 |
| Automatic/five bands and exact experimental consent | 2, 3, 5 |
| One post-selection UI and seven-stage visuals | 6, 7, 16 |
| Generation-safe reducer/coordinator | 7 |
| Exact v3 route execution | 2, 10B–10D, 11, 13 |
| Private source/package custody and sealed staging | 4, 8, 11, 13, 14 |
| Durable output registry and restart reconstruction | 8 |
| OpenVINO Import → Inspection → Hardware → Compatibility | 12, 13, 14, 16, 17 |
| GGUF runtime, quantisation, Chat, and `.gguf` Save | 9, 10A–10D, 11, 15A, 15B, 17 |
| OpenVINO execution, prompting, Chat, and complete ZIP Save | 12, 13, 14, 15A, 15C, 17 |
| Destination atomicity and closed failure codes | 15A–15C |
| One shell/navigation/registration and semantic no-duplication | 5, 6, 14, 16 |
| Native visual/accessibility and screenshot-oracle proof | 6, 16, 17 |
| Whole-app regressions and immutable evidence | 17 |

Do not call the feature complete until all of these statements are backed by recorded evidence:

- Compatibility is the only selector and issues exact fresh v3 plans.
- Current-fit supports separate direct Chat and optional optimisation; required optimisation cannot Chat early.
- GGUF and OpenVINO use one shared post-selection page/coordinator and identical visible states.
- Both adapters execute only exact, freshly revalidated, capability-admitted plans.
- Persistent GGUF results can Chat and atomically Save as one verified `.gguf`.
- Persistent OpenVINO results can Chat and atomically Save as one complete verified `.zip`.
- Runtime-only results show Chat/Done and explicitly state that no new model file exists.
- Originals remain unchanged across success, failure, cancellation, and destination retry.
- Private source/output custody, durable receipt reconstruction, generation suppression, and destination publication tests pass.
- Exact experimental evidence consent, revocation, drift, and quality warnings pass for both routes.
- Native compact/wide/High Contrast/200% visuals match the approved light template.
- The full packaged regression is at least 1,431 existing tests plus new tests with zero failures.
- No shared selector, page, theme, reducer, coordinator, dispatcher, registry, destination router, registration, or navigation target is duplicated.
- Unavailable native prerequisites remain recorded blockers, never successful evidence.
