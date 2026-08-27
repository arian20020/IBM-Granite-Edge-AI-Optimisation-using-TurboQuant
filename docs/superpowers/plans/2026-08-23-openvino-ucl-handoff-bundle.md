# OpenVINO UCL Continuation Bundle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce one integrity-bound, offline-oriented ZIP that reconstructs the exact OpenVINO feature branch and verified local closure inputs while giving the UCL Intel laptop worker complete, truthful instructions for finishing hardware-dependent acceptance.

**Architecture:** Commit reusable handoff documentation and two narrowly scoped PowerShell tools: one deterministic builder and one fail-closed initializer. The builder packages a Git bundle, exact baseline source snapshot, three separately verified closure ZIPs, context documents, a closed payload inventory, and checksums into one outer ZIP. The initializer validates all hashes before cloning or extracting and records path-safe operational state; neither tool treats transferred inputs as UCL evidence.

**Tech Stack:** Git bundle/archive, Windows PowerShell 5.1, .NET `System.IO.Compression`, SHA-256, JSON, MSTest contract tests, existing OpenVINO manifest verifiers.

---

## File map

- Create `docs/handoffs/openvino-ucl/READ_FIRST.md`: human entry point and trust boundary.
- Create `docs/handoffs/openvino-ucl/CONTINUATION_PROMPT.md`: exhaustive prompt for the laptop worker.
- Create `scripts/openvino/handoff/Initialize-UclHandoff.ps1`: verify and reconstruct a handoff into an explicit empty destination.
- Create `scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1`: verify source inputs and build the deterministic payload/outer archives.
- Create `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs`: static and lightweight behavioral contracts for both scripts and documents.
- Modify `.superpowers/sdd/2026-08-20-openvino-route/progress.md`: record handoff construction without changing release acceptance.
- Create `.superpowers/sdd/2026-08-20-openvino-route/ucl-handoff-report.md`: final archive identity and validation report.
- Create outside Git: `C:\openvino-o1-handoff-output\OpenVino-UCL-Handoff-$shortCommit.zip` and matching `.sha256`.

### Task 1: Lock the handoff trust and file contracts

**Files:**
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs`

- [ ] **Step 1: Write failing asset and trust-boundary tests**

Create a test class that locates the repository using the existing `IBM Granite with TurboQuant (Intel).slnx` sentinel and asserts:

```csharp
[TestMethod]
public void HandoffAssetsExistAndKeepTransferredInputsOutOfAcceptance()
{
    string[] assets =
    [
        "docs/handoffs/openvino-ucl/READ_FIRST.md",
        "docs/handoffs/openvino-ucl/CONTINUATION_PROMPT.md",
        "scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1",
        "scripts/openvino/handoff/Initialize-UclHandoff.ps1"
    ];
    foreach (string asset in assets)
    {
        Assert.IsTrue(File.Exists(RepoPath(asset)), asset);
    }

    string prompt = File.ReadAllText(RepoPath(assets[1]));
    foreach (string required in new[]
    {
        "c1e0fe2f", "openvino_release_blocked", "openvino_release_accepted",
        "not trusted UCL evidence", "pinned Granite", "security/license",
        "normal WinUI", "GPU-01", "398 P1 atoms", "central registration"
    })
    {
        StringAssert.Contains(prompt, required);
    }
}
```

- [ ] **Step 2: Write failing builder/initializer security tests**

Assert that the builder requires explicit source/output/stage roots, invokes all three existing manifest verifiers, creates a Git bundle and source archive, and excludes TestResults/evidence/build output. Assert that the initializer requires an explicit destination, rejects nonempty destinations and aliased closure roots, verifies hashes before `git clone` or extraction, checks the exact feature ref/commit, and never defaults to `$HOME`, the repository root, or the machine temporary directory.

- [ ] **Step 3: Run the new class and observe RED**

Run:

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --configuration Release --filter "FullyQualifiedName~HandoffBundleContractTests" --minimum-expected-tests 1 --progress off --no-ansi
```

Expected: failure because the four required handoff assets do not exist.

- [ ] **Step 4: Commit the RED contract**

```powershell
git add -- tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs
git commit -m "test(openvino): define UCL handoff bundle contract"
```

### Task 2: Write the complete operator and worker context

**Files:**
- Create: `docs/handoffs/openvino-ucl/READ_FIRST.md`
- Create: `docs/handoffs/openvino-ucl/CONTINUATION_PROMPT.md`

- [ ] **Step 1: Write `READ_FIRST.md`**

The document must state:

```markdown
# OpenVINO UCL handoff — read first

This archive is a continuation kit, not release evidence.

Baseline implementation: `c1e0fe2f`
Branch: `feature/openvino-route`

1. Copy the outer ZIP and its `.sha256` file to the UCL Intel laptop.
2. Verify the outer SHA-256 before extraction.
3. Extract into a new explicit directory.
4. Set explicit paths and run `$bundleRoot='D:\OpenVino-UCL-Handoff'; $destinationRoot='D:\OpenVino-UCL-Work'; & "$bundleRoot\tools\Initialize-UclHandoff.ps1" -BundleRoot $bundleRoot -DestinationRoot $destinationRoot`.
5. Open `D:\OpenVino-UCL-Work\repository` in Codex (or the explicit destination selected in step 4).
6. Paste the entire `CONTINUATION_PROMPT.md` into the new worker.

Transferred closures are verified local inputs only. They are not trusted UCL
evidence and cannot close hosted, hardware, security, license, RTM, or release
acceptance gates.
```

Add prerequisites: Windows x64, Git, PowerShell 5.1+, .NET SDK 10.0.301 or the repository-compatible pinned SDK, Visual Studio/MSBuild with WinUI test tooling, at least 8 GB free disk beyond the extracted bundle, authorized access to the pinned Granite model, and no secrets in evidence directories.

- [ ] **Step 2: Write the detailed continuation prompt**

Use these exact major sections and fill each with concrete facts from the plan and Task reports:

```markdown
# Prompt for the UCL Intel laptop Codex worker
## Mission and terminal condition
## Repository and payload identities
## End product and non-negotiable architecture
## Full Task 1–18 execution history
## Exact completed verification evidence
## Problems encountered and their resolutions
## Security and correctness rulings to preserve
## Prohibited shortcuts and false-completion traps
## Laptop discovery and prerequisite audit
## Authorized pinned-Granite acquisition and identity binding
## Official CPU trusted-UCL campaign
## TurboQuant matched campaign
## Normal WinUI and accessibility acceptance
## Optional physical Intel GPU / GPU-01 path
## Artifact privacy and terminal cleanup
## External security/license records
## Controlled RTM regeneration and traceability
## Ordered final release gate
## Failure-handling protocol
## Required final report and commits
```

The history must name commits `9cb832c2`, `1f57e352`, `64d82726`, `ff13289c`, and `c1e0fe2f`; record current authoritative stages and manifest digest; explain the stale Stage L protocol rejection, the 23,756-file converter cold-scan timeout correction, the packaged stale manifest assertion, the invalid solution-level RID build and corrected project-level `--runtime win-x64` build, bounded ZIP extraction, explicit cleanup root, and final candidate revalidation.

The remaining checklist must require genuine hosted/UCL evidence, a real pinned-Granite TBQ4/TBQ4 CPU-SDPA matched campaign, normal WinUI activation evidence, external security/license approval, optional GPU-01 only on eligible hardware, and I0-controlled regeneration of all 398 P1 atoms. It must explicitly keep TurboQuant unregistered/unpackaged until every mandatory gate closes.

- [ ] **Step 3: Scan documentation for placeholders and unsafe claims**

Run:

```powershell
rg -n "TBD|TODO|fill in|assume passed|self-attest|synthetic acceptance" docs/handoffs/openvino-ucl
git diff --check -- docs/handoffs/openvino-ucl
```

Expected: no placeholder or whitespace findings. Any mention of synthetic acceptance must be a prohibition, not an instruction or claim.

- [ ] **Step 4: Commit the documentation**

```powershell
git add -- docs/handoffs/openvino-ucl
git commit -m "docs(openvino): add complete UCL continuation context"
```

### Task 3: Implement the fail-closed initializer

**Files:**
- Create: `scripts/openvino/handoff/Initialize-UclHandoff.ps1`

- [ ] **Step 1: Implement explicit arguments and preflight**

Use mandatory validated strings:

```powershell
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$BundleRoot,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DestinationRoot
)
```

Resolve both paths, require the bundle root to exist, reject reparse points, require the destination not to exist or to be empty, and reject destination equality/ancestry with the bundle root. Do not derive either path from user-profile or temporary environment variables.

- [ ] **Step 2: Verify closed inventory and every payload hash before mutation**

Load `inventory/payloads.json` with `OpenVinoClosedJson.psm1` from an extracted bootstrap copy included under `tools/lib`. Require exact schema properties and roles: `gitBundle`, `sourceSnapshot`, `officialWorker`, `turboQuantWorker`, `converter`, and `context`. For each relative path, reject rooting or `..`, confirm file length, compute lowercase SHA-256, and compare ordinally. Verify `CHECKSUMS.sha256` describes the same payload set.

- [ ] **Step 3: Clone and verify the repository**

Create only the explicit destination after all input hashes pass. Run:

```powershell
git clone --branch feature/openvino-route --single-branch $bundlePath $repositoryDestination
git -C $repositoryDestination rev-parse HEAD
git -C $repositoryDestination status --porcelain
```

Require the exact `handoffCommit` from `payloads.json` and an empty status. Do not fetch from a network remote.

- [ ] **Step 4: Extract and verify distinct closures**

Extract the three closure ZIPs with `System.IO.Compression.ZipArchive`, rejecting rooted/traversal entries, duplicate destinations, reparse semantics, more than 30,000 entries per payload, any individual file over 2 GB, or cumulative extraction beyond the declared payload expansion limit. Extract to:

```text
$destinationRoot\closures\official
$destinationRoot\closures\turboquant
$destinationRoot\closures\converter
```

Require the roots to be distinct and invoke from the reconstructed repository:

```powershell
Test-OpenVinoOfficialWorkerManifest.ps1 -StageDirectory $officialRoot
Test-OpenVinoTurboQuantWorkerManifest.ps1 -StageDirectory $turboQuantRoot
Test-OpenVinoConverterWorkerManifest.ps1 -StageDirectory $converterRoot
```

- [ ] **Step 5: Write path-safe operational state**

Write `handoff-state.json` with schema version, handoff commit, archive SHA-256 values, manifest SHA-256 values, and relative closure roles only. Do not include absolute paths, machine/user identity, timestamps, prompts, output, or approval booleans. Emit exactly `openvino_ucl_handoff_initialized` and exit 0; on any exception emit exactly `openvino_ucl_handoff_invalid` and exit 1.

- [ ] **Step 6: Run focused contracts**

Run the Task 1 filter. Expected: initializer-related tests pass while builder-asset tests remain RED until Task 4.

- [ ] **Step 7: Commit**

```powershell
git add -- scripts/openvino/handoff/Initialize-UclHandoff.ps1 tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs
git commit -m "feat(openvino): add verified UCL handoff initializer"
```

### Task 4: Implement the deterministic bundle builder

**Files:**
- Create: `scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1`
- Modify: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs`

- [ ] **Step 1: Implement mandatory input parameters**

Require explicit repository, official stage, TurboQuant stage, converter stage, and output directory. Set `BaselineImplementationCommit` to the immutable full identity `c1e0fe2f0bc3717dbec168ac5d3abbe4ebfc6a1d`. Reject dirty tracked state, wrong branch, missing baseline ancestry, aliased stage roots, reparse roots, an output root inside the repository/stages, and a nonempty output directory.

- [ ] **Step 2: Verify all stage inputs before packaging**

Invoke the three existing manifest verifiers and require typed success. Compute each manifest hash. Confirm the official manifest is exactly `db46a1c79a6bd2199eb4d9434ba406a1de51a11b060a4108a100542bdf9e39d3`. Record file counts and expanded byte totals without recording source absolute paths.

- [ ] **Step 3: Create repository and context payloads**

Run:

```powershell
git -C $repository bundle create $gitBundlePath feature/openvino-route
git -C $repository archive --format=zip --output=$sourceSnapshotPath c1e0fe2f
git bundle verify $gitBundlePath
```

Copy the approved plan, design, progress file, Task 1–18 reports, release evidence catalogue, `READ_FIRST.md`, and `CONTINUATION_PROMPT.md` to `context`. Do not copy TestResults, `.git`, `bin`, `obj`, generated evidence roots, or untracked files.

- [ ] **Step 4: Create each closure ZIP independently**

Use `System.IO.Compression.ZipArchive` with optimal compression, normalized forward-slash relative entry names, sorted file order, no reparse points, no files outside the exact stage root, and no absolute paths in entry names. Reopen each ZIP and verify exact entry count, declared uncompressed bytes, and safe names.

- [ ] **Step 5: Generate closed inventory and checksums**

Write `inventory/payloads.json` from an ordered PowerShell object so the emitted values are actual measured identities rather than template text:

```powershell
$inventory = [ordered]@{
    schemaVersion = 1
    handoffCommit = $handoffCommit
    baselineImplementationCommit = 'c1e0fe2f0bc3717dbec168ac5d3abbe4ebfc6a1d'
    branch = 'feature/openvino-route'
    payloads = @($measuredPayloadRows)
}
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $inventoryPath -Encoding UTF8
```

Populate one row per required role and use actual positive lengths/counts. Generate `CHECKSUMS.sha256` in relative-path ordinal order. Copy the initializer and strict JSON module under `tools`.

- [ ] **Step 6: Create and hash the outer ZIP**

Create `OpenVino-UCL-Handoff-$($handoffCommit.Substring(0, 12)).zip` from the staging root, excluding the output archive itself. Write a sibling `.sha256` containing lowercase hash, two spaces, and the leaf filename. Always remove only the builder-owned unique staging directory in `finally`; never delete the output root, repository, or supplied stages.

- [ ] **Step 7: Complete builder contract tests**

Add static tests for sorted entry creation, traversal/reparse rejection, closed inventory roles, exact official digest, no local-stage paths in documents/inventory, and builder-owned cleanup. Run the focused class and expect all cases to pass.

- [ ] **Step 8: Commit**

```powershell
git add -- scripts/openvino/handoff/New-OpenVinoUclHandoff.ps1 tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs
git commit -m "feat(openvino): build portable UCL continuation bundle"
```

### Task 5: Verify repository regressions before constructing the large artifact

**Files:**
- Modify only if a verified defect is found in Task 2–4 files.

- [ ] **Step 1: Parse both PowerShell scripts**

Use `System.Management.Automation.Language.Parser.ParseFile`; expected zero parse errors.

- [ ] **Step 2: Run focused handoff contracts**

Expected: every `HandoffBundleContractTests` case passes, zero skipped.

- [ ] **Step 3: Run full OpenVINO contracts**

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --configuration Release --minimum-expected-tests 1 --progress off --no-ansi
```

Expected: all tests pass, including the existing 187 Task 18 contracts plus the new handoff cases.

- [ ] **Step 4: Run diff and repository hygiene checks**

Run `git diff --check`, inspect `git status --short`, and review every staged diff. Commit any verified corrections with narrowly scoped messages before bundle construction. Require a clean checkout.

### Task 6: Build and independently validate the handoff ZIP

**Files:**
- Create outside repository: `C:\openvino-o1-handoff-output\OpenVino-UCL-Handoff-$shortCommit.zip`
- Create outside repository: matching `.sha256`
- Create temporarily: unique builder and validation roots under explicit `C:\openvino-o1-handoff-*` directories.

- [ ] **Step 1: Reverify closure inputs**

Use:

```powershell
powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File scripts/openvino/Test-OpenVinoOfficialWorkerManifest.ps1 -StageDirectory C:\openvino-o1-task16-official-stage
powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File scripts/openvino/Test-OpenVinoTurboQuantWorkerManifest.ps1 -StageDirectory C:\openvino-o1-task16-worker-stage-f
powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File scripts/openvino/Test-OpenVinoConverterWorkerManifest.ps1 -StageDirectory C:\openvino-o1-task13-converter-stage-p
```

Expected: each typed verifier reports valid.

- [ ] **Step 2: Run the builder**

Invoke the builder with the exact repository and three stage paths plus `C:\openvino-o1-handoff-output`. Expected: `openvino_ucl_handoff_created`, one outer ZIP, and one checksum file.

- [ ] **Step 3: Verify the outer checksum and listing**

Recompute SHA-256, compare ordinally with the `.sha256`, open the outer ZIP using `ZipFile.OpenRead`, reject duplicate/rooted/traversal entries, and confirm the required top-level documents, tools, inventory, repository payloads, and three closure archives.

- [ ] **Step 4: Test-extract and run the initializer**

Extract the outer ZIP into a unique validation root. Run `Initialize-UclHandoff.ps1` into a different explicit empty root. Expected: `openvino_ucl_handoff_initialized`, exact handoff commit, clean repository, three distinct verified closures, and path-safe `handoff-state.json`.

- [ ] **Step 5: Run the reconstructed repository smoke checks**

From the reconstructed checkout, run the handoff contract class and the no-evidence release gate. Expected: contracts pass; release gate emits exactly `openvino_release_blocked` and exits 1.

- [ ] **Step 6: Audit archive exclusions**

Confirm no archive entry begins with or contains `.git/`, `TestResults/`, `/bin/`, `/obj/`, local evidence output, temporary stage names, user-profile paths, or the mirrored Stage B. The Git bundle is the only repository-history container. Confirm context/inventory text contains no username, machine name, absolute local source path, prompt/generated output, credential, or approval claim.

### Task 7: Record the final transfer identity

**Files:**
- Create: `.superpowers/sdd/2026-08-20-openvino-route/ucl-handoff-report.md`
- Modify: `.superpowers/sdd/2026-08-20-openvino-route/progress.md`

- [ ] **Step 1: Write the handoff report**

Record the exact handoff commit, outer ZIP absolute delivery path, byte length, lowercase SHA-256, nested payload hashes/sizes/counts, bootstrap result, reconstructed commit/status, manifest verifier results, contract counts, exclusions audit, and the remaining external acceptance blockers. State explicitly that no UCL evidence was generated on this machine.

- [ ] **Step 2: Update progress without changing acceptance state**

Append a handoff entry stating that the portable continuation kit is complete while Task 18 remains locally complete/external-release-blocked.

- [ ] **Step 3: Review and commit only the report files**

```powershell
git add -f -- .superpowers/sdd/2026-08-20-openvino-route/ucl-handoff-report.md .superpowers/sdd/2026-08-20-openvino-route/progress.md
git diff --cached --check
git commit -m "docs(openvino): record UCL continuation handoff"
```

- [ ] **Step 4: Preserve the non-self-referential handoff identity**

Define `handoffCommit` as the clean code/context commit used to construct the Git bundle. The later report-only commit is not included in the archive and does not change its identity. Record both commits in the user-facing delivery response, but require the laptop initializer to check out only `handoffCommit`.

- [ ] **Step 5: Final delivery**

Provide clickable paths to the ZIP, `.sha256`, committed prompt, design, plan, and report. Report the ZIP size and hash, state that the source worktree is clean, and restate the laptop’s first command. Do not claim `openvino_release_accepted` until the laptop and governance gates genuinely pass.
