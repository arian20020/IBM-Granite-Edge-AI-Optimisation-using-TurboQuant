# Model Inspection Test Completeness Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the verified Model Inspection test, defect, CI, privacy, accessibility, and evidence gaps recorded in the completeness matrix without silently changing protocol policy or implementing downstream Model Inspection gates.

**Architecture:** Strengthen the existing layered test system from the inside out: shared evidence validation, protocol serialization and sequencing, bounded transport, worker host, real-process behavior, packaged WinUI behavior, workflow contracts, and retained evidence. Each task starts with a focused failing test or contract check, makes only the minimum in-scope production or workflow change, then updates the durable matrix with the exact evidence produced.

**Tech Stack:** C# 12; repository-pinned .NET SDK `10.0.301` with Microsoft Testing Platform; `net8.0` and `net8.0-windows10.0.19041.0`; MSTest `4.3.2`; Microsoft.NET.Test.Sdk `18.8.1`; Windows App SDK packaged tests through `vstest.console.exe`; PowerShell; GitHub Actions on `windows-latest`.

## Global Constraints

- Execute from an isolated worktree based on exact cleanup Phase 1 closure commit `8f00a64a40e916872abfd6b72721ef86f223bab9`. The documentation commit `d7df4a3` may be the starting head.
- This plan implements only the **test-completeness gate (Order 1)** from the approved roadmap. It does not implement cleanup Phases 2–8, LLamaSharp extraction, worker packaging into MSIX, evidence mapping, classification, the service, the ViewModel, outcome UI, Hardware Fit, conversion, OpenVINO, TurboQuant, chat, GPU, or non-x64 support.
- Preserve wire protocol version `1` and all currently approved public behavior. Do not make missing/default-valued JSON fields required, change enum values, strengthen hash formats, or invent new cross-field invariants unless a separate approved protocol decision exists.
- The two validator defects in `MI-DEF-001` and `MI-DEF-002` are in scope because they enforce already-declared non-null/integrity semantics. All other entries under “Protocol decisions requiring separate approval” remain deferred.
- Preserve strict UTF-8, LF framing, the `1 MiB` line limit, the `256 KiB` retained-stderr limit, fixed startup/overall/cancellation timeouts, job-object containment, path privacy, deterministic failure precedence, and Windows x64 scope.
- Follow red-green-refactor. For every production or workflow change, capture the focused RED command and failure, implement the smallest correction, rerun the focused test, then rerun every affected project.
- Do not weaken, delete, skip, or relabel existing tests to obtain green. Do not lower a CI test floor.
- Parameterized rows count as independently executed tests. Set final CI floors from the final discovered/TRX counts; never guess a count before discovery is complete.
- Raw test/coverage/process/package output stays in unique private temporary roots outside the repository and every upload tree. Only protected, privacy-scanned evidence is retained under `artifacts/model-inspection/test-completeness/` in CI or the approved evidence store. Commit only summaries, hashes, manifests, and privacy-safe excerpts.
- No model path, username, repository path, model bytes, GGUF payload, raw metadata, unrestricted stderr, dump, or binary may enter an uploaded artifact.
- After every task, update the affected `MI-TC-*`, `MI-QT-*`, `MI-DEF-*`, or `MI-DOC-*` row in `docs/testing/Model-Inspection-Test-Completeness-Matrix.md` with the exact test name, command, result, and evidence state.
- After every executed verification command, append a privacy-safe evidence-ledger row with a unique evidence ID, tested head, command outcome, protected artifact/log digest, privacy result, and matrix rows. Test-result rows also record the canonical result ID, exact scope, total, required classes, and zero-failure/zero-skip outcome. A `projects.countEvidence` or `workflowRuns.countEvidence` reference may point only to the exact anchored ledger row containing and matching all of those test-result fields.
- Every task that creates, moves, or deletes a file in the cleanup scope must update the sorted `docs/reviews/model-inspection-cleanup-source-files.txt` and exactly one factual row in `docs/reviews/model-inspection-cleanup-inventory.md` before any full Contracts run or commit. Update the existing inventory row when a reviewed file's responsibility, findings, behavior, tests, evidence, or deferral changes. Run `scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1` after the update and stage both review files with that task.
- Commit each completed task separately using the commit message given in that task.

## Locked File and Project Map

Production boundaries that may receive the two defect fixes:

```text
shared/GraniteEdgeAI.ModelInspection.Contracts/
shared/GraniteEdgeAI.ModelInspection.Transport/
workers/GraniteEdgeAI.ModelInspection.Worker/
```

Existing test boundaries to strengthen:

```text
tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/
tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/
tests/UnitTests/GraniteEdgeAI.UnitTests/
```

Workflow and documentation boundaries:

```text
.github/workflows/build-and-test.yml
.github/workflows/model-inspection-transport-tests.yml
.github/workflows/model-inspection-worker-tests.yml
.github/workflows/model-inspection-worker-client-tests.yml
.github/workflows/model-inspection-worker-process-tests.yml
.github/workflows/llamasharp-feasibility-smoke.yml
.github/workflows/llamasharp-real-model-integration.yml
docs/testing/Model-Inspection-Test-Completeness-Matrix.md
tests/README.md
docs/superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md
```

Stable interfaces consumed by this plan:

```csharp
public interface IInspectionWorkerClient
{
    Task<WorkerClientResult> ExecuteAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken);
}

public interface IWorkerInspectionEngine
{
    Task<WorkerEngineResult> InspectAsync(
        WorkerStartInspectionCommand command,
        IProgress<WorkerProgressMessage>? progress,
        CancellationToken cancellationToken);
}
```

## Execution Order

The dependency order is fixed:

1. Freeze the baseline and evidence ledger.
2. Repair the two proven contract defects.
3. Complete protocol and sequence coverage.
4. Complete bounded-transport coverage and repair only reproduced defects.
5. Complete worker-host lifecycle coverage.
6. Launch the actual production worker in the process suite.
7. Execute every dormant abnormal fixture scenario.
8. Execute the real packaged navigation and `Loaded` lifecycle.
9. Close presentation, filesystem, copy, and accessibility evidence.
10. Make CI discovery, floors, triggers, architecture checks, documentation, and cleanup inventory complete.
11. Hard-gate artifact upload on privacy scanning and make orphan checks unconditional.
12. Collect coverage and run targeted mutation checks.
13. Run the full verification ladder and publish the final evidence ledger.

---

### Task 1: Freeze the baseline and create the evidence ledger

**Files:**

- Verify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md`
- Verify: `docs/superpowers/plans/2026-08-08-model-inspection-test-completeness-gate.md`
- Create: `docs/testing/evidence/2026-08-08-model-inspection-test-completeness-ledger.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`
- Verify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/CleanupInventoryContractTests.cs`

**Consumes:** Exact historical baseline `8f00a64a40e916872abfd6b72721ef86f223bab9` and the read-only audit summarized by `MI-TC-001` through `MI-TC-063`.

**Produces:** A privacy-safe evidence ledger whose rows can move independently from `None` or `Partial` to `Adequate`.

- [ ] **Step 1: Prove the documentation baseline is internally complete**

Run:

```powershell
$matrixPath = 'docs/testing/Model-Inspection-Test-Completeness-Matrix.md'
$matrix = Get-Content $matrixPath -Raw
$families = [ordered]@{
    TC = 1..63
    QT = 1..19
    DEF = 1..5
    DOC = 1..9
}
$tableRows = [regex]::Matches(
    $matrix,
    '(?m)^\|\s+(MI-(TC|QT|DEF|DOC)-(\d{3}))\s+\|')

foreach ($family in $families.GetEnumerator()) {
    $actual = @($tableRows |
        Where-Object { $_.Groups[2].Value -eq $family.Key } |
        ForEach-Object { $_.Groups[3].Value })
    $expected = @($family.Value | ForEach-Object { $_.ToString('D3') })
    $duplicates = @($actual | Group-Object | Where-Object Count -ne 1)
    $difference = @(Compare-Object $expected ($actual | Sort-Object))
    if ($actual.Count -ne $expected.Count -or
        $duplicates.Count -ne 0 -or
        $difference.Count -ne 0) {
        throw "MI-$($family.Key) authoritative table rows are not unique, contiguous, and complete."
    }
}

$documents = @(
    $matrixPath
    'docs/superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md'
    'docs/superpowers/plans/2026-08-08-model-inspection-test-completeness-gate.md'
)
$forbidden = @('T' + 'BD', 'T' + 'ODO', 'PLACE' + 'HOLDER', 'UN' + 'KNOWN')
$matches = @(Select-String -Path $documents -Pattern $forbidden -CaseSensitive)
if ($matches.Count -ne 0) {
    $matches | ForEach-Object { Write-Host $_.ToString() }
    throw 'Completion governance contains an unresolved marker.'
}
```

Expected: contiguous complete `MI-TC-001..063`, `MI-QT-001..019`, `MI-DEF-001..005`, and `MI-DOC-001..009` families and no unresolved-marker match. This is a guard step; it must pass before code changes.

- [ ] **Step 2: Create the retained evidence ledger**

Use this exact schema:

```markdown
# Model Inspection Test Completeness Evidence Ledger

Baseline: 8f00a64a40e916872abfd6b72721ef86f223bab9
Plan: docs/superpowers/plans/2026-08-08-model-inspection-test-completeness-gate.md

| Evidence ID | Task | Result ID | Scope | Run ID | Protected TRX pattern | Head | Command | Total | Executed | Failed | Skipped | Required classes | Protected artifact or log SHA-256 | Privacy scan | Matrix rows |
|---|---|---|---|---|---|---|---|---:|---:|---:|---:|---|---|---|---|
```

Use an anchored identifier in the first cell, for example `<a id="MI-EV-0001"></a>MI-EV-0001`, and increment it without reuse. Encode a literal command pipe as `&#124;`; no ledger cell may contain a raw `|` or a newline. Test rows use one of the seven register scopes, a canonical result ID, a positive total equal to executed, `0` failed/skipped, and required class names sorted ordinally and separated by `; ` (`(none)` when empty). A canonical project row eligible for final `countEvidence` uses run ID `(project)` and its exact protected `<resultId>.trx` filename; a workflow-run row eligible for final `countEvidence` uses the exact registered `runId` and `protectedTrxPattern`. A development test run without a protected TRX uses run ID `(development)`, pattern `(none)`, and a protected-log digest and can never be selected by `countEvidence`. Non-test verification rows use a stable descriptive scope, result/run IDs `(none)`, protected pattern `(none)`, zero counters, and `(none)` required classes. Every row uses the full 40-character tested commit, a lowercase 64-hex digest of its protected artifact or log, and privacy value `passed`.

Do not add speculative result rows. Append a row only after executing its command on the recorded head. In the final `ExactHead` register, every `countEvidence` value uses the exact form `docs/testing/evidence/2026-08-08-model-inspection-test-completeness-ledger.md#MI-EV-NNNN`; the fragment is a required record selector, not optional link decoration.

Update the roadmap metadata status to `Test-completeness gate in progress; cleanup and production implementation pending`. This makes the first execution commit self-describing without implying that any cleanup phase or production gate has started.

- [ ] **Step 3: Keep the permanent cleanup inventory green**

Ensure these paths appear exactly once in ordinal order in `model-inspection-cleanup-source-files.txt` and have one factual inventory row each. The documentation-package commit registers the first three; Task 1 adds the new evidence-ledger path without duplicating them:

```text
docs/superpowers/plans/2026-08-08-model-inspection-test-completeness-gate.md
docs/superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md
docs/testing/Model-Inspection-Test-Completeness-Matrix.md
docs/testing/evidence/2026-08-08-model-inspection-test-completeness-ledger.md
```

The inventory rows identify them as current completion governance/evidence, use no speculative test result, and distinguish the historical cleanup Phase 1 source head from the current documentation head.

Run:

```powershell
& scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1
```

Expected: all cleanup inventory contract tests pass. This step precedes every later full contract run.

- [ ] **Step 4: Record repository identity without dirty-state ambiguity**

Run:

```powershell
git rev-parse HEAD
git status --short
dotnet --version
dotnet --info
```

Expected: the head and any already-approved documentation changes are explicit; SDK reports `10.0.301` or the repository-approved patch selected by `global.json`.

- [ ] **Step 5: Commit the evidence-ledger shell**

```powershell
git add docs/testing/Model-Inspection-Test-Completeness-Matrix.md `
  docs/testing/evidence/2026-08-08-model-inspection-test-completeness-ledger.md `
  docs/superpowers/plans/2026-08-08-model-inspection-test-completeness-gate.md `
  docs/superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "docs(model-inspection): add completeness audit ledger"
```

---

### Task 2: Repair and prove nested evidence validation

**Files:**

- Modify: `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/WorkerModelFileEvidence.cs`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/WorkerTokenizerEvidence.cs`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/WorkerInspectionEvidence.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/TestJson.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerEvidenceValidationTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerProtocolTests.cs`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** Existing public non-null tokenizer contract, `IntegrityPreserved` meaning, and completion-status exclusivity rules.

**Produces:** Direct evidence for `MI-DEF-001`, `MI-DEF-002`, nested validation, every runtime/model-file/observation guard, and each independent completion predicate.

- [ ] **Step 1: Add a single valid evidence factory**

Add `TestJson.CreateValidEvidence()` that returns a fully valid `WorkerInspectionEvidence` with:

- `WorkerVersion`, protocol version `1`, the approved runtime profile, non-empty LLamaSharp/backend/mapped-commit/native-library versions, `ProcessArchitecture = "X64"`, and a non-empty inspection mode;
- `UsesCuda = false`, `UsesVulkan = false`, and `GpuLayerCount = 0` for the approved CPU-only runtime;
- equal before/after length, `LastWriteTimeBeforeUtc`/`LastWriteTimeAfterUtc` UTC timestamps, and SHA-256 strings with `IntegrityPreserved = true`;
- valid configuration and tokenizer summaries;
- a non-null `KnownSpecialTokenIds` dictionary;
- a privacy-safe chat-template summary; and
- at least one valid `WorkerObservation`.

Every mutation test below must start from this factory so it reaches the intended predicate.

- [ ] **Step 2: Write RED tests for contradictory file integrity**

Use data rows to independently change `LengthAfter`, `LastWriteTimeAfterUtc`, and `Sha256After` while keeping `IntegrityPreserved = true`:

```csharp
[DataTestMethod]
[DataRow("length")]
[DataRow("timestamp")]
[DataRow("sha256")]
public void ModelFileIntegrityTrueRejectsContradictoryEvidence(string mutation)
{
    WorkerModelFileEvidence valid = TestJson.CreateValidEvidence().ModelFile;
    WorkerModelFileEvidence contradictory = mutation switch
    {
        "length" => valid with { LengthAfter = valid.LengthAfter + 1 },
        "timestamp" => valid with
        {
            LastWriteTimeAfterUtc =
                valid.LastWriteTimeAfterUtc.AddSeconds(1)
        },
        "sha256" => valid with { Sha256After = new string('B', 64) },
        _ => throw new InvalidOperationException()
    };

    Assert.ThrowsExactly<WorkerProtocolException>(
        contradictory.Validate);
}
```

Also prove that `IntegrityPreserved = false` remains valid even when the snapshots happen to match; this prevents the fix from inventing a reverse implication.

Run:

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~WorkerEvidenceValidationTests.ModelFileIntegrityTrueRejectsContradictoryEvidence"
```

Expected RED: no `WorkerProtocolException` is thrown for all three rows.

- [ ] **Step 3: Implement the minimum integrity predicate**

Inside `WorkerModelFileEvidence.Validate()`, after the existing primitive guards, add only:

```csharp
if (IntegrityPreserved)
{
    WorkerProtocolValidation.Require(
        LengthBefore == LengthAfter,
        nameof(IntegrityPreserved),
        "requires equal before/after lengths when true");
    WorkerProtocolValidation.Require(
        LastWriteTimeBeforeUtc == LastWriteTimeAfterUtc,
        nameof(IntegrityPreserved),
        "requires equal before/after timestamps when true");
    WorkerProtocolValidation.Require(
        string.Equals(
            Sha256Before,
            Sha256After,
            StringComparison.OrdinalIgnoreCase),
        nameof(IntegrityPreserved),
        "requires equal before/after SHA-256 values when true");
}
```

Do not add a new SHA-format policy in this task.

- [ ] **Step 4: Write RED tests for a null tokenizer dictionary**

Deserialize a completed message whose otherwise-valid evidence contains:

```json
"tokenizer": {
  "vocabularyCount": 32000,
  "vocabularyType": "BPE",
  "tokenizerSmokePassed": true,
  "tokenizerSmokeTokenCount": 4,
  "knownSpecialTokenIds": null
}
```

First deserialize the nested tokenizer JSON directly with a test-local `JsonSerializerOptions` that exactly mirrors the protocol's camel-case/string-enum settings. Assert the tokenizer object is non-null and `KnownSpecialTokenIds` is null, place it into `TestJson.CreateValidEvidence()`, and assert `WorkerInspectionEvidence.Validate()` rejects it with `WorkerProtocolException`. In a separate assertion, send the completed-message JSON through `WorkerProtocolJson.DeserializeMessage(...)` and assert the dispatcher rejects it with the same controlled exception. Do not claim the validating dispatcher returns an invalid object.

Expected RED: the completed message validates.

- [ ] **Step 5: Implement nested tokenizer validation**

Add:

```csharp
public void Validate()
{
    WorkerProtocolValidation.RequireNotNull(
        KnownSpecialTokenIds,
        nameof(KnownSpecialTokenIds));
}
```

Then capture and validate the tokenizer in `WorkerInspectionEvidence.Validate()`:

```csharp
WorkerTokenizerEvidence tokenizer = WorkerProtocolValidation.RequireNotNull(
    Tokenizer,
    nameof(Tokenizer));
tokenizer.Validate();
```

Do not add count-range or special-token semantics in this task.

- [ ] **Step 6: Isolate every completion exclusivity predicate**

Replace the three tests that currently fail on their first guard with six focused cases:

1. `Completed` requires evidence.
2. `Completed` forbids operational failure.
3. `Cancelled` forbids evidence.
4. `Cancelled` forbids operational failure.
5. `OperationalFailure` requires operational failure.
6. `OperationalFailure` forbids evidence.

Each case must make every unrelated predicate valid. Add positive validation for all three completion statuses.

- [ ] **Step 7: Directly cover every existing evidence validator**

Add data-driven mutations for:

- every non-empty/approved field in `WorkerRuntimeIdentity.Validate()`;
- every primitive guard in `WorkerModelFileEvidence.Validate()`;
- null runtime, model file, configuration, tokenizer, chat-template, observation collection, and null observation element in `WorkerInspectionEvidence.Validate()`;
- empty code/category/detail in `WorkerObservation.Validate()`; and
- empty code/message in `WorkerOperationalFailure.Validate()`.

Name each data row after the field it invalidates. Do not use a loop that hides which predicate failed.

- [ ] **Step 8: Run focused and full contract suites**

Before either Contracts command, register `Protocol/WorkerEvidenceValidationTests.cs`, update every affected inventory row, and run the cleanup verifier.

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~WorkerEvidenceValidationTests|FullyQualifiedName~WorkerProtocolTests"
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release
```

Expected GREEN: every discovered test passes with zero skipped; the discovered total is recorded in the evidence ledger.

- [ ] **Step 9: Update evidence and commit**

Update `MI-TC-025`, `MI-TC-026`, `MI-QT-007`, `MI-DEF-001`, and `MI-DEF-002` with exact test names and result head.

```powershell
git add shared/GraniteEdgeAI.ModelInspection.Contracts `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "fix(model-inspection): enforce nested evidence integrity"
```

---

### Task 3: Complete protocol JSON and sequence coverage without changing policy

**Files:**

- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/TestJson.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerProtocolJsonTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerCommandSequenceValidatorTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerMessageSequenceValidatorTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Protocol/WorkerProtocolWireManifestTests.cs`
- Modify only if a RED test proves a defect: `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/WorkerProtocolJson.cs`
- Modify only if a RED test proves a defect: `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/WorkerCommandSequenceValidator.cs`
- Modify only if a RED test proves a defect: `shared/GraniteEdgeAI.ModelInspection.Contracts/Protocol/WorkerMessageSequenceValidator.cs`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** Current protocol version, serializer options, duplicate-name policy, additive-field compatibility, and sequence state machines.

**Produces:** Direct evidence for all supported records, all 19 enum members, all 22 audited JSON behaviors, request-ID enforcement, and validator state preservation.

- [ ] **Step 1: Add complete valid factories**

Extend `TestJson` with one valid instance of `WorkerStartedMessage`, `WorkerProgressMessage` for all five stages and all five statuses, and `WorkerCompletedMessage` for each terminal status. Reuse `CreateValidEvidence()`; do not duplicate the evidence graph.

- [ ] **Step 2: Prove full record round trips**

Add one data row per supported command/message record. For each:

1. serialize with `WorkerProtocolJson`;
2. deserialize through the correct command or message dispatcher;
3. serialize again; and
4. compare the two parsed JSON documents with `JsonNode.DeepEquals`.

Also assert the concrete CLR type and call `Validate()`. This proves supported-record dispatch and stable round trips; it does not by itself prove that a consistently dropped, renamed, or defaulted property survives.

Add an independent wire manifest in `WorkerProtocolWireManifestTests` with exactly one entry for each of the 96 audited public serialized properties. Each entry records owning CLR type, CLR property, expected camel-case JSON name/path, a distinctive expected JSON value, JSON kind, and current missing/null/wrong-type disposition. Assert:

1. reflection over the supported root and nested protocol/evidence records produces exactly the same 96 `Type.Property` identities as the explicit manifest;
2. each valid factory sets the manifest's distinctive value rather than a default that could hide loss;
3. flattened serialized JSON contains exactly the expected paths and values; and
4. no manifest entry maps to a duplicate JSON path within its root record.

For every manifest entry, mutate a valid JSON document independently by removing the property, replacing it with `null`, and replacing it with a wrong JSON kind/type. Route the root through the public command/message dispatcher and assert the manifest's current accepted/rejected result and resulting value. Required guard failures use `WorkerProtocolException`; currently optional/defaultable behavior remains a named characterization. This is the direct field evidence for `MI-TC-028`, `MI-QT-008`, and `MI-QT-009`; the round-trip test remains a separate compatibility assertion.

In the same RED set, add a field-specific validator matrix that starts from a valid record and mutates one existing guard at a time:

- hello: version, kind, worker ID/version, PID, runtime profile, and architecture;
- start: version, kind, request ID, parent PID/time, absolute model path, null nested records, and scan/identity length mismatch;
- expected identity: positive length and UTC timestamp;
- quick scan: GGUF format plus every currently required non-empty/positive field;
- cancel and started: version, kind, and request ID;
- progress: version, kind, request ID, defined stage/status, total count, completed-count lower/upper bounds, and fraction including NaN/infinities/out-of-range; and
- completed: version, kind, request ID, defined status, and every independently isolated terminal predicate from Task 2.

Every invalid row asserts `WorkerProtocolException` and names the mutated field. Do not add a guard that production does not already declare.

- [ ] **Step 3: Cover exact JSON boundaries**

Add focused tests for:

- exactly `WorkerProtocol.MaximumMessageBytes` bytes accepted when the payload is a valid JSON object padded only with legal JSON whitespace;
- one byte over rejected;
- maximum nesting depth accepted and depth `+1` rejected;
- empty, whitespace-only, non-object, multiple-root, and trailing-token input rejected;
- invalid UTF-8, comments, trailing comma, UTF-8 BOM, CR, and LF rejected at the appropriate JSON or transport layer.

The exact-limit builder must count UTF-8 bytes, not UTF-16 characters.

- [ ] **Step 4: Complete discriminator and version tests**

Test missing, null, string, fractional, and wrong `protocolVersion`; missing, null, blank, non-string, wrong-cased property name, wrong-cased value, and unknown discriminator; and every valid command/message discriminator.

All invalid cases assert the same public exception type and a privacy-safe message. Do not assert internal parser wording.

- [ ] **Step 5: Complete duplicate, casing, enum, and additive-field tests**

Add direct cases for:

- a duplicate top-level name;
- a duplicate name in an object inside an array;
- a numeric enum;
- every `WorkerStageStatus` string;
- wrong casing for an ordinary property;
- unknown properties at the root, nested object, and array-object levels; and
- null and unsupported CLR values passed to serialization.

Unknown additive fields must remain accepted. Ordinary-property case behavior must be characterized as current behavior, not changed.

- [ ] **Step 6: Cover optional/default-valued field behavior as characterization**

For each currently optional/defaultable field called out in the matrix, assert the current deserialize result when omitted and when explicitly null. Label these tests `Characterization` and link them to the protocol-decision section of the matrix.

Do not add `JsonRequired`, `required`, or a new required-field table in this task.

- [ ] **Step 7: Complete command-sequence state tests**

Add direct cases for null/invalid commands and prove state remains unchanged after a rejected command. Then send the next valid command and assert it succeeds. Cover start, matching cancel, duplicate start, cancel-before-start, wrong request ID, and post-terminal input.

- [ ] **Step 8: Complete message-sequence state tests**

Add wrong request-ID cases for `Progress` and `Completed`; null/invalid messages; every `WorkerStageStatus`; lower/upper completed-stage-count bounds; decreasing count; decreasing stage; duplicate terminal; post-terminal input; and state preservation after every rejection.

Rename `Progress_DecreasingStageCountInvariant_RejectsInvalidCount` so its name matches the actual upper-bound behavior, and add a separate decreasing-count test.

- [ ] **Step 9: Run RED/GREEN and regression commands**

Before either Contracts command, register `Protocol/WorkerProtocolWireManifestTests.cs`, update every affected inventory row, and run the cleanup verifier.

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~WorkerProtocolJsonTests|FullyQualifiedName~WorkerCommandSequenceValidatorTests|FullyQualifiedName~WorkerMessageSequenceValidatorTests"
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release
```

If any test is RED, first decide whether it contradicts approved behavior or exposes an implementation defect. Fix only the latter. Record characterization results for the former.

- [ ] **Step 10: Update evidence and commit**

Update `MI-TC-025`, `MI-TC-027` through `MI-TC-030`, `MI-QT-008` through `MI-QT-010`, and `MI-QT-014`.

```powershell
git add shared/GraniteEdgeAI.ModelInspection.Contracts `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "test(model-inspection): complete protocol contract evidence"
```

---

### Task 4: Prove bounded transport at byte, cancellation, ownership, and concurrency boundaries

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/BoundedUtf8LineTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/BoundedUtf8LineConcurrencyTests.cs`
- Modify only after reproduced RED: `shared/GraniteEdgeAI.ModelInspection.Transport/BoundedUtf8LineReader.cs`
- Modify only after reproduced RED: `shared/GraniteEdgeAI.ModelInspection.Transport/BoundedUtf8LineWriter.cs`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** Strict UTF-8/LF framing, `1 MiB` maximum, caller-owned streams, serialized writes, and documented no-concurrent-disposal precondition.

**Produces:** Direct evidence for fragmented multibyte reads, exact limits, cancellation boundaries, fixed read-ahead, stream ownership, queued writes, and flush atomicity.

- [ ] **Step 1: Add the nine named transport tests**

Add exactly:

```text
ReadLineAsyncReassemblesOneByteReadsIncludingSplitMultibyteUtf8
ReadLineAsyncObservesPreCanceledTokenAtBufferedFrameBoundary
ReadLineAsyncRejectsExactLimitPayloadAtEofAsUnexpectedEndOfStream
WriteLineAsyncAcceptsPayloadAtExactLimit
WriteLineAsyncObservesPreCancellationWithoutWriting
DisposeLeavesCallerOwnedStreamUsable
ReadLineAsyncUsesFixedSizeReadAheadBuffer
WriteLineAsyncSerializesFlushWithinEachFrameTransaction
WriteLineAsyncCancellationWhileQueuedDoesNotWriteAndAllowsFollowingFrame
```

The successful fragmented-read payload must contain a multibyte character split across one-byte reads. The fixed-buffer probe records the largest requested read count; it must remain bounded by the implementation’s declared buffer size.

- [ ] **Step 2: Strengthen three questionable existing assertions**

- Compare the complete exact-limit payload, not only length and endpoints.
- Make the concurrent stream record `Write`, LF, and `Flush` events so a second frame cannot begin before the first flush finishes.
- After disposing the writer, successfully write directly to the caller-owned stream; `CanWrite` alone is insufficient.

- [ ] **Step 3: Run the focused RED set**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj `
  --configuration Release `
  --filter "Name~ReadLineAsyncObservesPreCanceledTokenAtBufferedFrameBoundary|Name~WriteLineAsyncSerializesFlushWithinEachFrameTransaction|Name~WriteLineAsyncCancellationWhileQueued"
```

Expected RED: the pre-cancelled buffered read may consume already-read bytes because cancellation is currently observed only on refill. Any other failure is evidence to diagnose, not permission for a broad rewrite.

- [ ] **Step 4: Apply the minimum reproduced reader correction**

If the expected RED is reproduced, put this before any buffer consumption in `ReadLineAsync`:

```csharp
cancellationToken.ThrowIfCancellationRequested();
```

Retain cancellation checks on stream refill. Do not redesign buffering.

- [ ] **Step 5: Resolve queued-writer behavior from evidence**

The queued-cancellation test must prove:

1. frame A holds the write gate;
2. frame B is cancelled while queued and writes zero bytes;
3. frame A completes its flush;
4. frame C then writes one intact frame.

If current behavior passes, make no production change. The current mutable-payload snapshot guarantee requires copying before the semaphore wait, so do not move that copy behind the gate. Characterize aggregate queued allocation as `MI-DEF-004` and defer any bounded pending-byte reservation to a separately approved transport resource-policy decision. Do not claim process-wide bounded memory unless a deterministic test proves it.

- [ ] **Step 6: Run focused and full transport suites**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~BoundedUtf8Line"
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj `
  --configuration Release
```

Expected GREEN: zero failed and zero skipped. Record the exact discovered count.

- [ ] **Step 7: Update evidence and commit**

Update `MI-TC-033` through `MI-TC-036`, `MI-QT-015` through `MI-QT-018`, `MI-DEF-003`, and `MI-DEF-004`.

```powershell
git add shared/GraniteEdgeAI.ModelInspection.Transport `
  tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "test(model-inspection): close bounded transport gaps"
```

---

### Task 5: Complete the worker-host lifecycle state machine

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/WorkerHostTests.cs`
- Modify only after reproduced RED: `workers/GraniteEdgeAI.ModelInspection.Worker/WorkerHost.cs`
- Modify only after reproduced RED: `workers/GraniteEdgeAI.ModelInspection.Worker/WorkerTerminalCoordinator.cs`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** One-request host lifecycle, injectable engine and parent-monitor seams, serialized progress, fixed stderr codes, and one-terminal coordination.

**Produces:** Direct evidence for successful completion, progress ordering, exception containment, cancel/EOF/parent-loss behavior, external cancellation, and terminal uniqueness.

- [ ] **Step 1: Refactor test setup without changing assertions**

Keep `RunHostAsync`, `CreateStart`, and `NeverLostParentMonitor`. Add test-only implementations:

- `CompletedEngine`, which reports valid monotonic progress and returns `WorkerEngineResult.Completed(validEvidence)`;
- `ThrowingEngine`, which throws an exception containing a sentinel secret;
- `CancellationAwareEngine`, which records cancellation and throws `OperationCanceledException`;
- `ParentLostMonitor`, which completes only when explicitly released; and
- a controllable input stream that can end, block, or supply a second command.

The valid evidence factory stays test-local unless sharing it would add a production dependency.

- [ ] **Step 2: Prove completed output ordering**

Add `CompletedEngineWritesProgressBeforeOneCompletedTerminal`. Parse every LF-delimited output frame and assert this exact order:

```text
Hello
Started
Progress (one or more, exact and monotonic)
Completed
EOF
```

Assert at least one exact `WorkerProgressMessage` with the expected request ID, stage, status, completed/total stage counts, and fraction. Assert every later progress frame is monotonic, then assert one terminal, `CompletionStatus.Completed`, non-null evidence, null operational failure, and exit code `WorkerExitCodes.Completed`.

- [ ] **Step 3: Prove exception containment**

Add `EngineExceptionMapsToFixedControlledFailureWithoutDetailLeak`. The throwing engine’s exception message must contain a model path and a sentinel. Assert:

- one `OperationalFailure` terminal;
- code `MI-OP-ENGINE-FAILED`;
- fixed public message;
- exit code `WorkerExitCodes.OperationalFailure`; and
- neither stdout nor stderr contains the model path, sentinel, exception type, or stack text.

- [ ] **Step 4: Prove protocol misuse has no terminal echo**

Add `MismatchedCancelWritesProtocolFailureAndNoTerminal`. Send a valid start followed by a cancel with another request ID. Assert hello and started may precede failure, but no completed frame is emitted, stderr contains only `MI-WORKER-PROTOCOL-FAILURE`, and the exit code is `WorkerExitCodes.ProtocolFailure`.

- [ ] **Step 5: Characterize EOF and parent loss**

Add:

```text
EndOfCommandStreamCancelsEngineAndWritesCancelledTerminal
ParentLossCancelsEngineAndWritesCancelledTerminal
```

Both must prove the engine token is cancelled, progress drains before terminal output, exactly one `Cancelled` terminal is emitted, and the exit code is `WorkerExitCodes.Cancelled`. These tests lock current protected-worker Gate 2 behavior; they do not redefine cancellation policy.

- [ ] **Step 6: Prove external cancellation uses the fixed operational path**

Cancel the caller token while the host is blocked waiting for start. Add `ExternalCancellationWritesFixedOperationalError` and assert:

- no model data or exception detail is written;
- stderr is exactly `MI-WORKER-OPERATION-CANCELLED` plus LF;
- no completed frame is emitted; and
- exit code is `WorkerExitCodes.OperationalFailure`.

- [ ] **Step 7: Race all terminal contenders**

Extend `TerminalCoordinatorAllowsOnlyOneWinner` to run at least 100 deterministic rounds where completion, cooperative cancellation, and parent loss are released together. Assert exactly one `TryBeginTerminal()` succeeds per round. Do not add sleeps; use barriers or `TaskCompletionSource` instances.

- [ ] **Step 8: Run the focused RED/GREEN cycle**

```powershell
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~WorkerHostTests"
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj `
  --configuration Release
```

Expected GREEN: every host branch passes with zero skipped. Modify production only for a reproduced contradiction with the approved lifecycle.

- [ ] **Step 9: Update evidence and commit**

Update `MI-TC-037` and `MI-TC-038`.

```powershell
git add workers/GraniteEdgeAI.ModelInspection.Worker `
  tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "test(model-inspection): complete worker host lifecycle evidence"
```

---

### Task 6: Launch the production worker in the real-process suite

**Files:**

- Create: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/PublishedWorker.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerProcessTestData.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/ProductionWorkerProcessTests.cs`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `.github/workflows/model-inspection-worker-process-tests.yml`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** The actual production `Program`, production executable resolver/client, and truthful unavailable protected-worker Gate 2 engine.

**Produces:** The first runtime proof that the built production worker—not only the fixture—completes the real contained process handshake.

- [ ] **Step 1: Write the RED production-process test**

Add `ProductionWorkerReturnsTruthfulUnavailableEngineFailure`:

```csharp
[TestMethod]
public async Task ProductionWorkerReturnsTruthfulUnavailableEngineFailure()
{
    await using PublishedWorker worker =
        await PublishedWorker.CreateAsync().ConfigureAwait(false);
    InspectionWorkerClient client =
        WorkerProcessTestData.CreateProductionClient(worker);

    WorkerClientResult result = await client.ExecuteAsync(
            WorkerProcessTestData.StartCommand(),
            progress: null,
            CancellationToken.None)
        .WaitAsync(TimeSpan.FromSeconds(15))
        .ConfigureAwait(false);

    result.Validate();
    Assert.IsNull(result.Failure);
    Assert.IsNotNull(result.TerminalMessage);
    Assert.AreEqual(
        WorkerCompletionStatus.OperationalFailure,
        result.TerminalMessage.CompletionStatus);
    Assert.AreEqual(
        "MI-OP-ENGINE-NOT-CONFIGURED",
        result.TerminalMessage.OperationalFailure?.Code);
    Assert.AreEqual(1, result.ExitCode);
    Assert.IsFalse(result.ForcedTermination);
}
```

Expected RED: no helper resolves/publishes the production executable, so the test does not compile.

- [ ] **Step 2: Add a production-worker publish helper**

Mirror `PublishedFixture` but use these immutable constants:

```csharp
private const string WorkerRootEnvironmentVariable =
    "GRANITE_GATE2_WORKER_ROOT";
private const string WorkerExecutableName =
    "GraniteEdgeAI.ModelInspection.Worker.exe";
private const string WorkerProjectRelativePath =
    "workers/GraniteEdgeAI.ModelInspection.Worker/" +
    "GraniteEdgeAI.ModelInspection.Worker.csproj";
```

Local mode publishes Release, `win-x64`, framework-dependent, `UseAppHost=true`, into a unique temporary directory and deletes only that owned directory in `DisposeAsync`. CI mode validates the controlled root and never deletes it.

- [ ] **Step 3: Add the production client factory**

`CreateProductionClient(PublishedWorker worker)` must use `worker.OutputDirectory`, the production executable name, no fixture scenario arguments, short test timeouts, and the same containment/environment/handle policy as every other process test.

- [ ] **Step 4: Generalize the process-leak assertion**

Replace the fixture-only implementation with:

```csharp
internal static Task AssertNoProcessRemainsAsync(string processName)
```

Retain the bounded poll and process disposal. Keep wrappers for the fixture and add `AssertNoProductionWorkerProcessRemainsAsync()`. Every production test calls the latter in `finally`.

- [ ] **Step 5: Run the focused process test**

```powershell
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj `
  --configuration Release `
  --runtime win-x64 `
  -p:Platform=x64 `
  --filter "FullyQualifiedName~ProductionWorkerProcessTests"
```

Expected GREEN: the actual worker emits trusted hello, accepts the start request over stdin, returns the fixed unavailable-engine terminal, exits `1`, and leaves no process behind.

- [ ] **Step 6: Publish and hash both executables in CI**

In both relevant workflows:

1. publish the worker and fixture once into separate controlled roots;
2. assign `GRANITE_GATE2_WORKER_ROOT` and `GRANITE_GATE2_FIXTURE_ROOT`;
3. generate a SHA-256 manifest for each root before tests;
4. run the process project;
5. regenerate hashes and fail if either tree changed; and
6. never upload the executable roots.

- [ ] **Step 7: Run the full process regression**

```powershell
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj `
  --configuration Release `
  --runtime win-x64 `
  -p:Platform=x64
```

- [ ] **Step 8: Update evidence and commit**

Update `MI-TC-039`, `MI-TC-046`, and the production-worker audit finding.

```powershell
git add tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests `
  .github/workflows/build-and-test.yml `
  .github/workflows/model-inspection-worker-process-tests.yml `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "test(model-inspection): exercise production worker process"
```

---

### Task 7: Execute every dormant abnormal-process fixture scenario

**Files:**

- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerHandshakeTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerProtocolIntegrityTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerCancellationAndTimeoutTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerEnvironmentAndHandleTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerLaunchContainmentTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerProcessTreeContainmentTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerStandardErrorTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerConcurrencyTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerProcessTestData.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/TestWorkerScenarioParserTests.cs`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj`
- Create: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json`
- Create: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerCrashAndHangTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/WorkerOutputBoundaryTests.cs`
- Create: `scripts/model-inspection/Verify-WorkerProcessScenarioCoverage.ps1`
- Modify only after reproduced RED: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/InspectionWorkerClient.cs`
- Modify: `.github/workflows/model-inspection-worker-process-tests.yml`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** The already-implemented test-only scenario parser/runner and existing WorkerClient failure precedence.

**Produces:** One executed real-process row for each of the 18 previously dormant scenarios, with deterministic public failure codes and no orphan.

- [ ] **Step 1: Add the handshake-invalid scenario rows**

Add these exact scenario/code pairs to the authoritative list, then consume its `HandshakeInvalid` projection through one `DynamicData` test:

```text
wrong-protocol-version  -> WorkerHandshakeInvalid
wrong-worker-process-id -> WorkerHandshakeInvalid
wrong-runtime-profile   -> WorkerHandshakeInvalid
wrong-architecture      -> WorkerHandshakeInvalid
text-before-hello       -> WorkerHandshakeInvalid
invalid-utf8            -> WorkerHandshakeInvalid
utf8-bom                -> WorkerHandshakeInvalid
malformed-json          -> WorkerHandshakeInvalid
duplicate-json-property -> WorkerHandshakeInvalid
```

For each row, assert null terminal, exact failure code, privacy-safe message/stderr, and no fixture process in `finally`.

- [ ] **Step 2: Add output-boundary tests**

Map `oversized-stdout-line` and `flood-stdout` to `WorkerOutputLimitExceeded`. Assert bounded completion time, forced cleanup when applicable, retained stderr at or below the configured maximum, and no terminal accepted from partial output.

- [ ] **Step 3: Add non-monotonic progress**

Map `non-monotonic-progress` to `WorkerProtocolInvalid`. Capture progress callbacks and prove the invalid frame is not reported to the caller after the sequence validator rejects it.

- [ ] **Step 4: Characterize and then lock crash precedence**

Run each crash scenario 20 times before changing production. Record the public failure code and the last accepted protocol state for every attempt. The expected current candidates are:

```text
crash-before-hello -> WorkerCrashed
crash-after-hello  -> WorkerCrashed
crash-after-start  -> WorkerProtocolInvalid
```

`crash-before-hello` fails before a request conversation becomes active. `crash-after-start` has accepted start but reaches EOF without the required terminal, matching `WorkerConversation.CompleteOutput()`. `crash-after-hello` can race between root-process exit, the start write, and EOF. If all 20 attempts for a scenario produce one outcome consistent with the existing precedence, lock that outcome in the test. If any scenario produces multiple outcomes, stop this task and obtain an explicit failure-precedence decision before changing `InspectionWorkerClient`; do not install an ad hoc post-handshake check or redefine the active-conversation EOF rule under this no-policy-change gate.

Assert bounded completion, no accepted terminal, no leaked path/detail, and no surviving process.

- [ ] **Step 5: Add timeout-precedence tests**

Use a `300 ms` handshake timeout and a `600 ms` overall timeout:

```text
hang-before-hello -> WorkerHandshakeTimeout
hang-after-hello  -> WorkerOverallTimeout
hang-after-start  -> WorkerOverallTimeout
```

Wrap each call in an outer `WaitAsync(TimeSpan.FromSeconds(5))` so a broken timeout cannot hang the test job.

- [ ] **Step 6: Prove the complete public scenario inventory is executed**

Create one authoritative `worker-process-scenarios.json` table. Each row contains the enum value, exact kebab-case name, execution route (`WorkerClientRoot`, `DirectFixtureRoot`, or `DescendantOnly`), assertion family, expected failure/completion outcome, owning test class, and a non-empty `requiredScopes` subset of `Focused`, `Main`, `Repeat`, `Crash`, and `Full`. Copy the manifest to test output through the test project. `WorkerProcessTestData` parses it into one validated `IReadOnlyList<WorkerProcessScenarioCase>`, and every scenario-owning class named in this task consumes projections or named lookups from that exact list. Do not duplicate scenario strings in attributes or a second inventory set. Single-scenario tables use `DynamicData`; composite concurrency/tree tests resolve their participating rows from the same list.

Extend `TestWorkerScenarioParserTests` with a set comparison over those executed data rows:

```text
all enum scenarios = all authoritative rows
LaunchProbe and ProbeUnrelatedHandle = DirectFixtureRoot rows consumed by WorkerLaunchContainmentTests
ChildProcessWait = one DescendantOnly row consumed only through SpawnChildAndWait/ExitRootWithLiveChild containment
all remaining rows = WorkerClientRoot rows consumed by a real-process test
```

`Verify-WorkerProcessScenarioCoverage.ps1` accepts mandatory `-ScenarioManifest`, `-TrxPath`, and `-ExpectedScope Focused|Main|Repeat|Crash|Full`. It parses the completed TRX and the same JSON manifest, compares executed data-row identities/classes with exactly the rows tagged for that scope, rejects duplicates/zero/skips/failures, and asserts `ChildProcessWait` is never launched as an independent root while both owning containment scenarios execute in the `Full` scope. `TestWorkerScenarioParserTests` separately proves enum/parser/manifest set equality, rejects an unknown/empty scope, and proves every independently launched root row belongs to `Full`. Adding a fixture mode must fail until it has an explicit route, scope tags, and consuming test; a listed-but-unexecuted root row must fail the post-run verifier.

- [ ] **Step 7: Make the focused workflow discover the new classes**

Add `ProductionWorkerProcessTests`, `WorkerCrashAndHangTests`, and `WorkerOutputBoundaryTests` to the explicit filter in `.github/workflows/model-inspection-worker-process-tests.yml`. Include applicable hang/output cases in the existing repeat filter. Add a separate fail-fast PowerShell loop that invokes the crash-precedence filter exactly 20 times, checks `$LASTEXITCODE` after every invocation, and enforces the discovered crash-row floor on every attempt. Raise the main, repeat, and crash floors from actual final discovery. Make the hosted main run and every repeat/crash attempt emit uniquely named private TRX; verify them with `ExpectedScope Main`, `Repeat`, and `Crash`, respectively, before scan/upload. The unfiltered local project TRX is the only `ExpectedScope Full` input.

- [ ] **Step 8: Run focused and full real-process tests**

```powershell
$ErrorActionPreference = 'Stop'
$processResults = Join-Path ([IO.Path]::GetTempPath()) ('mi-process-results-' + [Guid]::NewGuid().ToString('N'))
$focusedResults = Join-Path $processResults 'focused'
$fullResults = Join-Path $processResults 'full'
$crashResults = Join-Path $processResults 'crash'
New-Item -ItemType Directory -Path $focusedResults,$fullResults,$crashResults | Out-Null
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj `
  --configuration Release `
  --runtime win-x64 `
  -p:Platform=x64 `
  --filter "FullyQualifiedName~WorkerHandshakeTests|FullyQualifiedName~WorkerOutputBoundaryTests|FullyQualifiedName~WorkerCrashAndHangTests|FullyQualifiedName~WorkerProtocolIntegrityTests" `
  --results-directory $focusedResults `
  --report-trx `
  --report-trx-filename focused.trx
if ($LASTEXITCODE -ne 0) { throw "Focused process tests failed: $LASTEXITCODE" }
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj `
  --configuration Release `
  --runtime win-x64 `
  -p:Platform=x64 `
  --results-directory $fullResults `
  --report-trx `
  --report-trx-filename full.trx
if ($LASTEXITCODE -ne 0) { throw "Full process tests failed: $LASTEXITCODE" }
& scripts/model-inspection/Verify-WorkerProcessScenarioCoverage.ps1 `
  -ScenarioManifest tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json `
  -TrxPath (Join-Path $focusedResults 'focused.trx') `
  -ExpectedScope Focused
if ($LASTEXITCODE -ne 0) { throw "Focused scenario coverage failed: $LASTEXITCODE" }
& scripts/model-inspection/Verify-WorkerProcessScenarioCoverage.ps1 `
  -ScenarioManifest tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json `
  -TrxPath (Join-Path $fullResults 'full.trx') `
  -ExpectedScope Full
if ($LASTEXITCODE -ne 0) { throw "Full scenario coverage failed: $LASTEXITCODE" }
$processProject = 'tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj'
for ($attempt = 1; $attempt -le 20; $attempt++) {
    $attemptDirectory = Join-Path $crashResults $attempt
    $trxName = "crash-$attempt.trx"
    New-Item -ItemType Directory -Path $attemptDirectory | Out-Null
    dotnet test $processProject `
      --configuration Release `
      --runtime win-x64 `
      -p:Platform=x64 `
      --filter 'FullyQualifiedName~WorkerCrashAndHangTests.Crash' `
      --results-directory $attemptDirectory `
      --report-trx `
      --report-trx-filename $trxName
    if ($LASTEXITCODE -ne 0) {
        throw "Crash-precedence attempt $attempt failed: $LASTEXITCODE"
    }
    & scripts/model-inspection/Verify-WorkerProcessScenarioCoverage.ps1 `
      -ScenarioManifest tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json `
      -TrxPath (Join-Path $attemptDirectory $trxName) `
      -ExpectedScope Crash
    if ($LASTEXITCODE -ne 0) {
        throw "Crash-precedence scenario verification $attempt failed: $LASTEXITCODE"
    }
}
```

Expected GREEN: all 18 new scenario executions pass with zero skipped and both worker process names are absent after the suite.

- [ ] **Step 9: Update evidence and commit**

Update `MI-TC-042`, `MI-TC-044`, `MI-TC-045`, and `MI-TC-046`.

```powershell
git add infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient `
  tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests `
  scripts/model-inspection/Verify-WorkerProcessScenarioCoverage.ps1 `
  .github/workflows/model-inspection-worker-process-tests.yml `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "test(model-inspection): execute abnormal process inventory"
```

---

### Task 8: Execute the real navigation and `Loaded` lifecycle in packaged WinUI

**Files:**

- Create: `scripts/model-inspection/Invoke-PackagedModelInspectionTests.ps1`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestApp.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/UnitTestAppWindow.xaml.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs`
- Modify only after reproduced RED: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify only after reproduced RED: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml.cs`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** Existing immutable import request, shell event subscription, `Frame.Navigate`, page navigation contract, and four initial presentation factories.

**Produces:** Direct packaged evidence for exact request identity through the event pipeline, invalid parameters, subscription replacement, failed navigation, actual `Loaded`, repeated load, and new-request re-entry.

- [ ] **Step 1: Add one repeatable packaged-test runner**

Create `Invoke-PackagedModelInspectionTests.ps1` with:

```powershell
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $TestCaseFilter = '',
    [string] $ResultsDirectory =
        'TestResults/ModelInspection-Packaged'
)
```

The script must:

1. locate the repository root from its own path;
2. locate `MSBuild.exe` with `vswhere` and invoke that resolved executable rather than assuming `msbuild` is on `PATH`;
3. restore and build the application with that x64 MSBuild;
4. restore/build `GraniteEdgeAI.UnitTests.csproj` for `win-x64`;
5. resolve the generated `.build.appxrecipe`;
6. locate `vstest.console.exe` with `vswhere`;
7. pass `/TestCaseFilter:` only when non-empty;
8. write exactly one deterministic `packaged.trx` beneath `ResultsDirectory`;
9. propagate the runner exit code; and
10. parse the TRX, rejecting zero, failed, skipped/not-executed, aborted, timeout, or inconclusive results.

This script must use the same app-container runner as CI; it must not substitute `dotnet test`.

- [ ] **Step 2: Expose only test-host composition state**

In the test executable—not production—expose:

```csharp
internal static UnitTestAppWindow CurrentWindow { get; private set; } = null!;
internal Grid TestRootElement => TestRoot;
```

Set `CurrentWindow` in `UnitTestApp.OnLaunched`. Add a test helper that inserts a `Frame` into `TestRootElement`, awaits a `Loaded` `TaskCompletionSource` with a five-second timeout, and removes it in `finally`. Never create a second application instance.

- [ ] **Step 3: Strengthen the real import-event identity assertion**

In `ModelImportRequest_NavigatesStageFrameWithSameRequest`, capture `eventArguments.Request` from the real `ModelInspectionRequested` event before the shell handler runs. After invoking the actual Continue button, assert:

```csharp
Assert.IsNotNull(emittedRequest);
Assert.IsNotNull(inspectionPage);
Assert.AreSame(emittedRequest, inspectionPage.Request);
```

Keep the existing path/name assertions as secondary evidence.

- [ ] **Step 4: Add subscription replacement and detach tests**

Add:

```text
AttachModelImportPage_ReplacesPriorSubscription
SuccessfulInspectionNavigation_DetachesImportSubscription
AttachingSameImportPageTwice_DoesNotDuplicateNavigation
ShellUnloaded_DetachesImportSubscription
```

Drive requests through each page’s actual state/Continue action. A request from the replaced or detached page must not navigate or increment a navigation count; the current page must navigate exactly once.

Raise the shell's real `Unloaded` lifecycle for the unload case. A request from an unloaded page must not navigate or increment the navigation count.

- [ ] **Step 5: Prove controlled navigation failure**

Attach a `StageFrame.Navigating` handler that sets `eventArguments.Cancel = true`. Call `NavigateToModelInspection(request)` and assert:

- it returns `false`;
- `CurrentStage` and the stage indicator remain `ImportModel`;
- the import subscription remains active; and
- a later uncancelled request can still navigate with the same request instance.

This uses the real frame cancellation seam and requires no production-only test hook.

- [ ] **Step 6: Add null and wrong navigation-parameter tests**

Navigate a real `Frame` to `ModelInspectionPage` with `null` and with an unrelated object. Assert the navigation fails with the page’s controlled `ArgumentException`, no request is stored, and no partially initialized page remains active in the frame.

- [ ] **Step 7: Execute actual `Loaded` composition**

Add `Loaded_WithValidatedRequest_AppliesCompleteInitialPresentation`. Attach the navigated frame to the active test window, await the page `Loaded` event, then inspect the four named controls:

```text
InspectionOutcomeCardControl -> Hidden and Collapsed
InspectionModelCardControl   -> Compact, ModelSelected, file name only
InspectionContentCardControl -> five ordered stages, first Active, four Waiting
InspectionActionCardControl  -> Inspecting, visible disabled Cancel inspection
```

Assert no rendered/presentation string contains `request.ModelPath` or its directory. Assert the five stages and all semantic automation names, not brush identity alone.

- [ ] **Step 8: Prove repeated load and new-request re-entry**

Add:

```text
RepeatedLoaded_DoesNotReplaceInitialPresentationObjects
NavigateAwayAndBack_WithNewRequest_AppliesOnlyNewRequest
```

For repeated load, detach and reattach the same frame/page and assert the four presentation object references are unchanged. For re-entry, navigate away, navigate back with another request, await `Loaded`, assert the new filename is visible, the old path/name is absent, progress is reset to stage one, and the outcome remains hidden.

- [ ] **Step 9: Run the focused packaged RED/GREEN set**

```powershell
& scripts/model-inspection/Invoke-PackagedModelInspectionTests.ps1 `
  -Configuration Release `
  -TestCaseFilter 'FullyQualifiedName~OnboardingModelInspectionNavigationTests|FullyQualifiedName~ModelInspectionPageNavigationTests'
```

Expected GREEN: every filtered packaged test passes with zero skipped; the TRX count and SHA-256 are recorded.

- [ ] **Step 10: Update evidence and commit**

Update `MI-TC-009` through `MI-TC-013`, `MI-QT-001`, and `MI-QT-002`.

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/Onboarding" `
  "IBM Granite with TurboQuant (Intel)/Features/ModelInspection" `
  tests/UnitTests/GraniteEdgeAI.UnitTests `
  scripts/model-inspection `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "test(model-inspection): prove packaged navigation lifecycle"
```

---

### Task 9: Complete implemented presentation, filesystem, copy, and accessibility evidence

**Files:**

- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/ModelQuickScannerTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentTemplateSelectorTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionPresentationMappingTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionModelCardTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionContentCardTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionOutcomeCardTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/Controls/InspectionActionCardTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionContractTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/OnboardingStageIndicatorTests.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml`
- Create after execution: `docs/testing/evidence/2026-08-08-model-inspection-accessibility-verification.md`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** Existing scanner, selector, presentation models, card controls, semantic text, live-region implementation, and deliberately disabled pre-service Cancel action.

**Produces:** Complete evidence for currently implemented mappings and path classes, a fixed copy defect, packaged semantic checks, and retained Windows accessibility acceptance evidence.

- [ ] **Step 1: Add scanner filesystem-boundary tests**

Use unique test-owned temporary directories and explicit cleanup. Add:

```text
ScanAsync_WithDirectoryPath_ReturnsControlledFailure
ScanAsync_WithExclusiveLock_ReturnsControlledFailure
ScanAsync_WithReadOnlyValidFile_SucceedsWithoutMutation
ScanAsync_WithSpacesAndUnicodeName_PreservesSafeFileName
ScanAsync_WithLongWindowsPath_CompletesWithoutPathLeak
ScanAsync_FailureDiagnosticsNeverContainCanonicalPath
```

For the read-only case, capture bytes, SHA-256, length, last-write UTC, and attributes before and after. For failure cases, inspect every result/exception/diagnostic string and assert the canonical path and parent directory are absent. The long-path case creates a valid fixture beyond 260 characters using .NET APIs and must not be marked inconclusive.

- [ ] **Step 2: Complete selector routing as an exhaustive table**

For every defined `InspectionContentCardMode`, assert the exact template for:

- direct presentation item;
- wrapper object;
- `ContentControl`;
- `ContentPresenter`; and
- the supported null/default route.

Assert missing configured templates and undefined enum values fail with the existing controlled exception. Compare `Enum.GetValues<InspectionContentCardMode>()` with the table keys so a new mode cannot escape.

- [ ] **Step 3: Cover every current card mapping**

In `InspectionPresentationMappingTests`, use explicit expected dictionaries and set-equality guards for:

- all `InspectionCheckStatus` background/foreground/symbol mappings;
- all `InspectionContentStatus` background/foreground/symbol/visibility mappings;
- all `InspectionModelBadgeState` text, background, foreground, border, and automation names;
- all `InspectionModelCardMode` visual states;
- all `InspectionOutcomeTone` visual states;
- all `InspectionActionCardMode` visual states; and
- every implemented `ModelInspectionOutcome` eligibility result.

For undefined enum casts, preserve the current contract: selector/action/outcome routes that are already fail-fast assert `ArgumentOutOfRangeException`; model/content status helper routes with deliberate safe fallback assert that exact fallback. Do not silently turn a fallback into fail-fast behavior or create mappings for runtime states that production Gate 5 has not implemented.

Strengthen `ModelInspectionContractTests` at the same boundary:

- `RuntimeIdentity_PreservesCompleteApprovedClosure` compares the exact 13-property set and every value produced by `CreateRuntimeIdentity()`, including worker/runtime/package versions, mapped commit, architecture, inspection mode, CPU-only flags, and GPU layer count; and
- `ApplicationContractGraph_ContainsNoTransportNativeOrUiTypes` recursively walks base types, interfaces, constructors, methods, return/parameter types, properties, fields, events, arrays, nullable types, and generic arguments for every application-contract type, with cycle detection and exact forbidden assembly/type assertions.

This is the direct repair for `MI-QT-005`; property-only traversal is not sufficient.

- [ ] **Step 4: Inspect all rendered text for backend leakage**

Strengthen the existing backend-leak test to enumerate every title, message, status, action, badge, disclosure, automation name, and help-text property in each initial presentation/control. Reject `LLamaSharp`, `llama.cpp`, native backend names, local paths, and implementation class names.

- [ ] **Step 5: Reproduce and fix the visible copy defect**

Add a packaged assertion for the page explanation. Expected RED contains `runtime is support`. Apply only this XAML correction:

```text
We are checking that the model package, tokenizer, structure and runtime are supported before checking hardware fit.
```

Give the text block a stable `x:Name` if needed for the semantic assertion. Update `MI-DEF-005`.

- [ ] **Step 6: Strengthen automated accessibility assertions**

Exercise the actual four controls in their dedicated packaged test classes. For the initial page and every implemented card mode, assert:

- non-empty unique accessible names;
- visible status text communicates the state without relying on color/icon;
- disabled actions remain visibly and semantically disabled;
- logical tab order and keyboard focus for enabled actions;
- long model names wrap without horizontal scrolling at a narrow measured width;
- stage indicator `AutomationLiveSetting.Polite`;
- each stage change updates the full “Step N of 5” automation name; and
- repeating the same stage does not change semantic state.

Keep `CurrentStage_ProvidesAutomationPeerForLiveRegionNotifications` as automated wiring evidence; do not relabel it as proof that an OS-level UIA event was observed.

- [ ] **Step 7: Perform and retain the Windows accessibility acceptance**

On a Release packaged build, record exact OS build, app head, display scale, text scale, theme, high-contrast theme, keyboard, Narrator version, and Windows SDK AccEvent version. Execute:

1. keyboard-only import to Model Inspection;
2. focus visibility and order;
3. 200% text scale at the smallest supported window;
4. light/dark and high-contrast readability;
5. Windows animations disabled/reduced motion, proving no state meaning or action depends on motion;
6. Narrator reading of page, cards, statuses, and disabled Cancel;
7. AccEvent filtered to `LiveRegionChanged` while moving through all five stage values; and
8. repeated same-stage assignment proving no duplicate announcement.

The evidence document contains a row per check with `Pass` or `Fail`, observed result, screenshot/log hash where safe, and issue link for any failure. Do not commit a template with blank results. Any failure keeps `MI-TC-020`, `MI-TC-021`, or `MI-TC-022` open.

- [ ] **Step 8: Run focused and full packaged regressions**

```powershell
& scripts/model-inspection/Invoke-PackagedModelInspectionTests.ps1 `
  -Configuration Release `
  -TestCaseFilter 'FullyQualifiedName~GgufQuickScannerTests|FullyQualifiedName~ModelQuickScannerTests|FullyQualifiedName~InspectionContentTemplateSelectorTests|FullyQualifiedName~InspectionPresentationMappingTests|FullyQualifiedName~InspectionModelCardTests|FullyQualifiedName~InspectionContentCardTests|FullyQualifiedName~InspectionOutcomeCardTests|FullyQualifiedName~InspectionActionCardTests|FullyQualifiedName~OnboardingStageIndicatorTests'
& scripts/model-inspection/Invoke-PackagedModelInspectionTests.ps1 `
  -Configuration Release
```

Expected GREEN: the focused and complete packaged TRX files have nonzero totals, all executed tests pass, and none are skipped.

- [ ] **Step 9: Update evidence and commit**

Update `MI-TC-006`, `MI-TC-008`, `MI-TC-014` through `MI-TC-022`, `MI-TC-024`, `MI-QT-003` through `MI-QT-005`, and `MI-DEF-005`. Link the recursive surface and exact runtime-identity assertions explicitly to `MI-QT-005`. Leave runtime-fed classifier/UI rows explicitly partial or deferred.

```powershell
git add "IBM Granite with TurboQuant (Intel)/Features/ModelInspection" `
  tests/UnitTests/GraniteEdgeAI.UnitTests `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "test(model-inspection): close presentation and accessibility evidence"
```

---

### Task 10: Make test-project discovery, architecture fitness, floors, triggers, inventory, and current documentation complete

**Files:**

- Create: `tests/model-inspection-test-projects.json`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/TestProjectInventoryContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/HostedArtifactReconciliationContractTests.cs`
- Create: `scripts/model-inspection/Verify-ModelInspectionHostedArtifacts.ps1`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ContractGraphTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Gate2ProjectGraphTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/Gate2ArchitectureFitnessTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/CleanupInventoryContractTests.cs`
- Modify: `shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `.github/workflows/model-inspection-transport-tests.yml`
- Modify: `.github/workflows/model-inspection-worker-tests.yml`
- Modify: `.github/workflows/model-inspection-worker-client-tests.yml`
- Modify: `.github/workflows/model-inspection-worker-process-tests.yml`
- Modify: `.github/workflows/llamasharp-feasibility-smoke.yml`
- Modify: `.github/workflows/llamasharp-real-model-integration.yml`
- Modify: `tests/README.md`
- Modify: `docs/architecture/diagrams/CI-Build-and-Test-Workflow.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`
- Modify: `docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md`
- Modify: `docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md`
- Modify: `docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md`
- Modify: `docs/superpowers/specs/2026-08-05-model-inspection-worker-integration-design.md`
- Modify: `docs/superpowers/specs/2026-08-05-model-inspection-worker-gate-2-host-process-adapter-design.md`
- Modify: `docs/testing/evidence/2026-08-05-model-inspection-worker-gate1-verification.md`
- Modify: `docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md`
- Modify: `docs/testing/evidence/2026-08-07-model-inspection-cleanup-phase-1.md`
- Modify: `docs/testing/evidence/README.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`

**Consumes:** All executable project files, final local/TRX discovery from Tasks 2–9, current workflow definitions, and the permanent cleanup inventory.

**Produces:** One machine-readable nine-project/scoped-run register; an executable hosted-artifact reconciler; exact solution/workflow reachability; meaningful non-decreasing floors; complete dependency triggers; complete architecture allowlists; current documentation; and an inventory that cannot silently omit a Model Inspection root.

- [ ] **Step 1: Write a RED executable-project discovery contract**

Discover every repository `.csproj` that either:

- contains `<IsTestProject>true</IsTestProject>`; or
- contains `<ProjectCapability Include="TestContainer" />`.

Assert that discovery equals these nine executable projects exactly:

```text
tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj
tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj
tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj
tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj
tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj
tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj
tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj
tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj
tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj
```

The test also asserts the protocol fixture and LLama test-support project are classified as support executables/libraries, not executable test projects.

- [ ] **Step 2: Create the machine-readable project register**

Use one object with a canonical full-project layer and a separate emitted-run layer:

```json
{
  "evidenceState": "Development",
  "evidenceHead": null,
  "projects": [
    {
      "project": "repository-relative csproj",
      "resultId": "stable unique full-project result identifier",
      "runner": "MicrosoftTestingPlatform or PackagedVSTest",
      "minimumExpectedTests": 1,
      "countEvidence": null,
      "requiredClasses": []
    }
  ],
  "workflowRuns": [
    {
      "runId": "stable unique emitted-TRX identifier",
      "projectResultId": "one canonical projects[].resultId",
      "workflow": "repository-relative workflow",
      "step": "exact workflow step name",
      "scope": "Full, Focused, Main, Repeat, Crash, Native, or Trusted",
      "protectedTrxPattern": "unique protected artifact-relative filename pattern",
      "minimumExpectedTests": 1,
      "countEvidence": null,
      "requiredClasses": [],
      "scenarioScope": "Focused, Main, Repeat, Crash, Full, or null",
      "campaign": "ModelFree, Native, or Trusted",
      "coverageProducer": false,
      "protectedCoveragePattern": null,
      "localInvocation": {
        "command": "dotnet or one approved repository-relative PowerShell runner",
        "arguments": ["one literal argument per item; reporting, floor enforcement, and coverage instrumentation omitted"]
      }
    }
  ]
}
```

`minimumExpectedTests` above describes the positive-integer schema, not a value to copy. Use these exact unique full-project result IDs: `contracts`, `transport`, `worker`, `worker-client`, `worker-process`, `packaged`, `llama-deterministic`, `llama-native`, and `llama-trusted`. Populate five projects from the final observed execution counts in Tasks 2–9 and populate WorkerClient only after the fresh full-project discovery in Step 6. Populate the three LLama entries from the audited authoritative retained totals `170`, `4`, and `20`; these are non-decreasing interim floors, not exact-head closure evidence. Task 13 must refresh all nine project entries after Tasks 10–12 have added their final tests. Canonical project evidence uses scope `Full`, except `llama-native` and `llama-trusted`, which use `Native` and `Trusted`. The contract rejects zero, a duplicate `resultId`, a floor below the current observed count, or a runner mismatch.

Task 10 commits the register in the only permitted interim lifecycle: `evidenceState` exactly `Development`, `evidenceHead` null, and every project/run `countEvidence` null. This is deliberate: Task 10 has discovered counts but the Task 11 protector/invoker and Task 13 exact-head replay do not yet exist. `TestProjectInventoryContractTests` rejects a non-null reference or non-null head in `Development` and rejects any unknown top-level, project, workflow-run, or local-invocation property outside the exact schema. Task 13 atomically changes the state to `ExactHead`, writes its full 40-hex tested head, and requires every reference to use the anchored ledger form. No other state or mixed resolved/unresolved register is valid.

Create one `workflowRuns` row for every TRX any managed workflow can retain, including permanent aggregate executions, focused workflows, and every process `Main`, `Repeat`, and `Crash` output. A protected TRX must match exactly one row by workflow and `protectedTrxPattern`; step remains provenance but is not part of artifact identity. Narrow runs use their own observed non-decreasing floor and required-class subset; they do not pretend to equal the canonical full-project total. `scope` is exactly one of the seven values in the schema. Every `worker-process` row uses one of `Focused|Main|Repeat|Crash|Full` and its non-null `scenarioScope` equals `scope`; `Main|Repeat|Crash` are forbidden on non-process rows, while a non-process `Focused` row may exist with null `scenarioScope`. `llama-native` is exactly `Native/Native` scope/campaign and `llama-trusted` is exactly `Trusted/Trusted`; no other result ID may use those scopes or campaigns. In `ExactHead`, parse the selected ledger row rather than merely checking its base file: its evidence ID must be unique; its result ID, scope, run ID, protected pattern, total, and required classes must equal the register row; its tested head must equal `evidenceHead`; and its counters, recomputed protected digest, and privacy fields must show a complete privacy-passed run. The contract rejects a fragment-free, nonexistent, duplicate, unrelated, stale-shape, or mismatched exact-head evidence row; an unknown scope or invalid process/campaign coupling; duplicate or overlapping `(workflow, protectedTrxPattern)` identities; an unknown project result ID; an unregistered TRX-producing step; or a retained/uploaded TRX for which no run row exists.

`campaign` is a closed `ModelFree|Native|Trusted` value. `localInvocation.command` is exactly `dotnet` or one repository-relative `.ps1` runner in a closed allowlist asserted by `BuildWorkflowContractTests`; `arguments` is a literal test-selection token array and omits result-directory/TRX-reporting arguments, minimum-floor enforcement, and coverage instrumentation. The Task 11 invoker owns reporting and reads/enforces the current register floor/classes itself; it must not emit coverage. Never store a shell command string or use `Invoke-Expression`. `BuildWorkflowContractTests` proves each token array is equivalent to the registered workflow step after removing only controlled result/report tokens, the exact floor token/value equal to `minimumExpectedTests`, and, for a declared canonical coverage producer, the exact coverage tokens owned by Task 12's collector. It also proves `Native` and `Trusted` rows have the corresponding campaign and that every other row is `ModelFree`. Until Task 12, every row has `coverageProducer: false` and null `protectedCoveragePattern`; no undeclared selection or coverage difference is ignored.

Lock this canonical project/result/runner mapping; neither the register nor reconciliation code may infer or swap it:

| Project | `resultId` | Runner |
|---|---|---|
| `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj` | `contracts` | `MicrosoftTestingPlatform` |
| `tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj` | `transport` | `MicrosoftTestingPlatform` |
| `tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj` | `worker` | `MicrosoftTestingPlatform` |
| `tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj` | `worker-client` | `MicrosoftTestingPlatform` |
| `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj` | `worker-process` | `MicrosoftTestingPlatform` |
| `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj` | `packaged` | `PackagedVSTest` |
| `tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj` | `llama-deterministic` | `MicrosoftTestingPlatform` |
| `tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj` | `llama-native` | `MicrosoftTestingPlatform` |
| `tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj` | `llama-trusted` | `MicrosoftTestingPlatform` |

`TestProjectInventoryContractTests` asserts set equality with these nine exact triples as well as discovery equality. A register whose projects are complete but whose result IDs or runners are exchanged must fail.

The same contract parses the fixed Markdown ledger schema. Against synthetic `ExactHead` register fixtures it resolves every `countEvidence` fragment to exactly one anchored row and adds negatives for a missing fragment, nonexistent fragment, duplicate evidence ID, wrong result ID, wrong scope, swapped run ID, swapped protected pattern, wrong total, changed required-class set, malformed or different tested head, changed/non-64-hex protected digest, non-passed privacy result, and incomplete counters. Against the real Task 10 `Development` register it instead requires null head/references. Two otherwise identical Main/Repeat/Crash records must not be interchangeable. Merely pointing at an existing ledger or evidence file must never satisfy the exact-head contract.

- [ ] **Step 3: Put every production and test project in the solution**

Add the omitted Contracts production/test projects and all five LLamaSharp production/support/test projects:

```text
shared/GraniteEdgeAI.ModelInspection.Contracts/GraniteEdgeAI.ModelInspection.Contracts.csproj
tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj
tools/ModelInspection.LlamaSharpSpike/ModelInspection.LlamaSharpSpike.csproj
tools/ModelInspection.LlamaSharpSpike.TestSupport/ModelInspection.LlamaSharpSpike.TestSupport.csproj
tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj
tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj
tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj
```

Keep Windows process projects mapped to x64 where required. Assert the set of all 17 repository project files equals the solution project set.

- [ ] **Step 4: Replace finite dependency blacklists with exact allowlists**

In `ContractGraphTests`, parse project XML and assert Contracts has:

- no project references;
- only approved framework references;
- no NuGet/runtime/native/WinUI package references; and
- no output asset that introduces Transport, Worker, WorkerClient, LLamaSharp, OpenVINO, TurboQuant, or Windows App SDK.

In protected-worker Gate 2 graph/fitness tests, include the Contracts project in:

- `ApprovedGate2ProjectPaths`;
- production source-root scans;
- package/reference scans; and
- build-policy assertions.

First make the policy test RED against Contracts, then add the same explicit build safeguards used by Transport:

```xml
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<Deterministic>true</Deterministic>
<AllowUnsafeBlocks>false</AllowUnsafeBlocks>
```

Assert the exact allowed directed graph:

```text
Contracts -> none
Transport -> none
Worker -> Contracts + Transport
WorkerClient -> Contracts + Transport
WinUI -> no WorkerClient/worker/runtime dependency in this gate
```

- [ ] **Step 5: Parse exact workflow steps instead of searching the whole file**

Refactor `BuildWorkflowContractTests` around a helper that extracts one named step’s keys and `run` block. For every registered `workflowRuns` row assert:

- the exact project variable/path is invoked in the registered step;
- no unintended filter hides tests;
- the expected runner/reporting syntax is used;
- a nonzero/final floor is enforced;
- `$LASTEXITCODE` or action failure semantics fail the job;
- the step appears in a reachable job; and
- artifact/evidence handling references the correct result directory and emits the row's unique protected TRX pattern.

Comments or unrelated steps must not satisfy these assertions. Parse every managed upload path and assert that each retained TRX maps by workflow plus protected relative filename to exactly one `workflowRuns` row and that every row is reachable from its named execution step; source text outside those steps cannot satisfy the mapping. Reject duplicate or wildcard-overlapping protected patterns within one workflow even when their step names differ, because hosted artifact resolution does not use the producing step.

Create `Verify-ModelInspectionHostedArtifacts.ps1` with this exact interface:

```powershell
param(
    [Parameter(Mandatory)][string] $ArtifactRoot,
    [Parameter(Mandatory)][string] $ArtifactIndexPath,
    [Parameter(Mandatory)][string] $RunHandoffPath,
    [Parameter(Mandatory)][string] $RegisterPath,
    [Parameter(Mandatory)][string] $CoverageLedgerPath,
    [Parameter(Mandatory)][string] $ScenarioManifestPath,
    [Parameter(Mandatory)][string] $OutputSummaryPath
)
```

The verifier fails closed and writes only a privacy-safe JSON summary. It must:

1. require a seven-workflow handoff marked `verified-success`, with one unique run ID per expected workflow, one exact candidate SHA, and a distinct local `EvidenceHead`; require the register to be `ExactHead` with that `EvidenceHead`, while all hosted runs bind independently to the candidate SHA;
2. require the index to cover every downloaded directory exactly once, contain nonempty service artifact IDs/names/digests and positive service sizes, and contain every independently queried job ID/name with `success` conclusion for its exact run;
3. enumerate every protected TRX under `ArtifactRoot`, resolve its workflow from the indexed run/artifact directory, and match workflow plus protected relative filename to exactly one `workflowRuns` row;
4. reject an extra, missing, multiply matched, raw, or unregistered TRX;
5. reject a scope outside the exact seven-value set; require every `worker-process` row's `scenarioScope` to equal its `Focused|Main|Repeat|Crash|Full` scope, and require null `scenarioScope` on every non-process row;
6. parse counters and class identities, require zero failed/skipped/not-executed/aborted/timeout/inconclusive results, and apply the row's own floor/class subset;
7. require `Full` rows to equal their canonical `projects` count/classes, while never applying that equality to a valid narrow scope;
8. invoke `Verify-WorkerProcessScenarioCoverage.ps1` for every `worker-process` row with the already-validated exact scenario scope;
9. resolve every protected Cobertura file by owning workflow and protected relative filename to exactly one of the six canonical full-project ledger mappings, reject a missing/extra/duplicate/narrow-scope coverage file, require only opaque `mi-source-<sha256>.cs` identifiers, and compare package/class/line/branch identities and counters exactly;
10. reject every artifact file not accounted for by the TRX, coverage, hash-manifest, or approved bounded-summary schemas; and
11. produce a summary marked `verified-success` and bound separately to the hosted candidate SHA and local `EvidenceHead`, containing a sorted `relative-file-id:size:sha256` manifest, its SHA-256 directory digest, scoped test totals/classes, coverage counters, and run/job/artifact service metadata without copying raw content or local paths. The manifest covers the downloaded payloads and artifact/job index but explicitly excludes `OutputSummaryPath` itself so the digest is not self-referential.

`HostedArtifactReconciliationContractTests` launches the script against synthetic privacy-safe trees and proves: one valid full+narrow TRX mapping plus the canonical coverage set passes; swapped/duplicate/unmatched run rows and same-workflow cross-step overlapping patterns fail; missing/duplicate/failed job rows fail; unknown/typo scopes and invalid native/trusted project/campaign tuples fail; a process row with null or mismatched scenario scope fails; a non-process Main/Repeat/Crash row or non-null scenario scope fails; a focused total is not compared with the full total; floor/class/scenario mismatches fail; raw/path-bearing TRX fails; missing/extra/duplicate/narrow-scope Cobertura, unknown coverage patterns, or changed branch counters fail; a failed/stale-head handoff fails; and the same files in a different enumeration order produce the same manifest digest.

- [ ] **Step 6: Raise every floor from final discovery**

After the Task 10 test sources compile, rerun the six managed protected-worker Gates 1 and 2 projects, including the previously omitted WorkerClient project, and append their exact discovered/executed totals to the evidence ledger:

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj --configuration Release
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj --configuration Release
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj --configuration Release
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj --configuration Release -p:Platform=x64
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj --configuration Release --runtime win-x64 -p:Platform=x64
& scripts/model-inspection/Invoke-PackagedModelInspectionTests.ps1 -Configuration Release
```

Keep or raise all existing floors and add the missing ones:

- permanent WorkerClient and real-process commands receive `--minimum-expected-tests`;
- packaged TRX parsing enforces the final full-project total;
- packaged required-class checks include the priority Model Inspection navigation, `Loaded`, presentation, scanner, and stage-indicator classes;
- focused process main/repeat floors include the Task 6/7 classes;
- deterministic LLamaSharp floor becomes the audited retained `170`, not `1`;
- contained native floor remains at least the audited retained `4`; and
- trusted real-model floor remains at least the audited retained `20`.

Derive the Task 10 numbers from these complete-project counters and record them in the register. Never lower an existing floor, even if a filtered local command discovers fewer tests; correct the filter instead. These numbers are reconciled once more in Task 13 after the privacy, coverage, and mutation tasks add tests.

- [ ] **Step 7: Complete dependency path triggers**

Every managed focused workflow includes `global.json`, its own workflow file, and any directly invoked script/config/fixture roots in addition to project dependencies. At minimum:

- WorkerClient focused workflow includes `shared/GraniteEdgeAI.ModelInspection.Transport/**`;
- worker-process workflow includes Contracts, Transport, Worker, WorkerClient, fixture, process tests, and its workflow file;
- worker-host workflow includes Contracts and Transport;
- transport workflow includes Contracts only if it directly consumes the protocol limit;
- LLama workflows include spike, test support, corresponding test projects, `global.json`, and workflow files.

Add a contract test that computes each registered project's transitive project-reference closure and combines it with the explicit non-project inputs above. For a path-filtered workflow, assert every resulting root appears in both `push.paths` and `pull_request.paths`. Treat a permanent workflow with no `paths` key, such as `build-and-test.yml`, as universal coverage and assert that it remains unfiltered; do not add a restrictive path filter merely to satisfy the contract.

- [ ] **Step 8: Make cleanup discovery complete and dispositions meaningful**

Add these four omitted sibling roots to `CompleteRoots`:

```text
tools/ModelInspection.LlamaSharpSpike.TestSupport
tools/ModelInspection.LlamaSharpSpike.Tests
tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests
tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests
```

Enforce the six authoritative final review dispositions from the approved cleanup design:

```text
reviewed — no change needed
reviewed — cleaned
reviewed — defect fixed
reviewed — duplication removed
reviewed — documentation corrected
reviewed — intentionally deferred with reason
```

Migrate each bare `reviewed` row from its actual “Findings”, “Changes made”, and “Deferred work and reason” columns. `reviewed — intentionally deferred with reason` additionally requires a named destination/owner and rationale in the deferred-work column. Regenerate the sorted source list/inventory for every new file in Tasks 1–10, then run the inventory verifier.

- [ ] **Step 9: Correct current documentation without rewriting history**

- Replace `tests/README.md`’s one-project/108-current claim with the nine-project register and identify eight MTP executables plus one packaged VSTest executable.
- Update `CI-Build-and-Test-Workflow.md` to show Contracts, all protected-worker Gate 2 layers, packaged app-container VSTest, privacy gates, orphan gate, and both artifact classes.
- Change `Features/README.md` so the protected worker is implemented and the real LLamaSharp engine is the next production gate.
- Add current-status/supersession links to older protected-worker Gates 1 and 2 design/evidence pages; preserve their historical run IDs and claims.
- Explain that `401259...` is the cleanup Phase 1 source-review head while `8f00a64...` is its closure head.

- [ ] **Step 10: Run architecture, workflow, inventory, and solution checks**

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~ContractGraphTests|FullyQualifiedName~Gate2ProjectGraphTests|FullyQualifiedName~Gate2ArchitectureFitnessTests|FullyQualifiedName~BuildWorkflowContractTests|FullyQualifiedName~TestProjectInventoryContractTests|FullyQualifiedName~HostedArtifactReconciliationContractTests|FullyQualifiedName~CleanupInventoryContractTests"
dotnet build "IBM Granite with TurboQuant (Intel).slnx" `
  --configuration Release `
  -p:Platform=x64
```

Expected GREEN: nine test projects, 17 solution projects, complete trigger closures, all nonzero final floors, valid dispositions, and zero build warnings/errors.

- [ ] **Step 11: Update evidence and commit**

Update `MI-TC-031`, `MI-TC-032`, `MI-TC-052`, `MI-TC-057`, `MI-QT-006`, `MI-QT-011` through `MI-QT-013`, and `MI-DOC-001` through `MI-DOC-009`.

```powershell
git add .github/workflows `
  "IBM Granite with TurboQuant (Intel).slnx" `
  "IBM Granite with TurboQuant (Intel)/Features/README.md" `
  tests `
  scripts/model-inspection/Verify-ModelInspectionHostedArtifacts.ps1 `
  docs `
  shared/GraniteEdgeAI.ModelInspection.Contracts
git commit -m "test(model-inspection): enforce complete test discovery"
```

---

### Task 11: Make privacy scans and orphan verification hard gates

**Files:**

- Create: `scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1`
- Create: `scripts/model-inspection/Protect-ModelInspectionTrx.ps1`
- Create: `scripts/model-inspection/Invoke-ModelInspectionRegisteredRuns.ps1`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `.github/workflows/model-inspection-worker-process-tests.yml`
- Modify: `.github/workflows/llamasharp-feasibility-smoke.yml`
- Modify: `.github/workflows/llamasharp-real-model-integration.yml`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/ArtifactPrivacyGateTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/RegisteredRunInvokerContractTests.cs`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** The Task 10 scoped-run register/tokenized invocations, bounded TRX/JSON/hash evidence, known worker/tool process names, workflow step outcomes, and the rule that unsafe or failed executions do not publish trusted artifacts.

**Produces:** One reusable fail-closed privacy scanner, one exact-head registered-run invoker built on the scanner/protector, upload conditions tied to both scan and execution success, and always-run final process checks for production worker, fixture descendants, and LLama apphosts.

- [ ] **Step 1: Write RED workflow-contract tests**

For every `actions/upload-artifact` step, parse the exact step and assert its `if:` expression contains:

- `always()` where post-failure scanning/cleanup must still occur;
- the matching privacy-scan step ID with `outcome == 'success'`; and
- every execution/model-integrity step whose success is required to trust that artifact.

Specifically reject the current unconditional protected-worker Gate 2 and packaged uploads and the LLama uploads that omit their test-step outcome.

- [ ] **Step 2: Create a reusable fail-closed scanner**

`Test-ModelInspectionArtifactPrivacy.ps1` accepts:

```powershell
param(
    [string[]] $Path = @(),
    [string[]] $ApprovedModelIdentity = @(),
    [switch] $SelfTest
)
```

The scanner:

- resolves every path and rejects paths outside the supplied artifact roots;
- recursively inspects every retained file, including TRX/XML/JSON/log/text;
- rejects `.gguf`, `.safetensors`, model `.bin`, dumps, executable/native binaries, archives, and unknown binary formats;
- detects GGUF magic regardless of extension;
- rejects a file whose paired `sha256:length` identity matches an approved controlled model;
- scans decoded text for Windows/UNC/extended/user-home model paths, repository-root fragments, repository-relative source/result paths, usernames, bearer tokens, API keys, secrets/passwords, raw chat templates, and unredacted exception chains;
- permits only explicitly listed bounded evidence extensions;
- reports only the relative filename and rule ID, never the sensitive match; and
- exits nonzero on unreadable files, decoding ambiguity, an empty expected evidence root, or any match.

`-SelfTest` creates safe and unsafe temporary fixtures, proving each rule fires and safe evidence passes, then removes only its own unique temporary directory.

- [ ] **Step 3: Add executable scanner tests**

`ArtifactPrivacyGateTests` launches the script against test-owned fixtures and asserts:

```text
safe bounded TRX/JSON/hash manifest -> exit 0
user/model path in TRX             -> nonzero
secret in JSON                     -> nonzero
renamed GGUF magic                 -> nonzero
approved model hash/length copy    -> nonzero
unknown binary                     -> nonzero
unreadable evidence                -> nonzero
```

Capture only exit code and sanitized scanner output.

`Protect-ModelInspectionTrx.ps1` accepts mandatory `-InputPath` and `-OutputPath`; it refuses identical input/output paths or an output nested under the private raw-results root.

Then generate an actual TRX from one filtered existing contract test into a private, non-upload root. Prove the raw TRX is rejected if it contains runner/user/absolute-path data. The protection script must parse XML and produce a second valid TRX that:

- retains test IDs, class/method names, outcomes, durations, counters, and timestamps;
- replaces run-user/computer/test-run names with fixed neutral values;
- removes deployment/result-file/collector attachment paths;
- replaces `codeBase` and every source/result root with fixed neutral non-path identifiers; repository-relative paths are forbidden too;
- redacts output text with the same privacy rules; and
- re-parses both files to prove counters and result identities/outcomes are identical.

Scan the sanitized TRX and require success. Never copy the raw TRX into an upload root.

After the scanner and protector are green, create `Invoke-ModelInspectionRegisteredRuns.ps1` with this exact interface:

```powershell
param(
    [Parameter(Mandatory)][string] $RegisterPath,
    [Parameter(Mandatory)][string] $RawResultsDirectory,
    [Parameter(Mandatory)][string] $ProtectedResultsDirectory,
    [Parameter(Mandatory)][ValidateSet('ModelFree','Native','Trusted')][string] $Campaign,
    [Parameter(Mandatory)][string] $TestedHead,
    [Parameter(Mandatory)][string] $ScenarioManifestPath,
    [string] $ControlledModelPath
)
```

The invoker rechecks that `HEAD` equals `TestedHead`, selects every and only register row in the requested campaign, and executes its validated tokenized `localInvocation` without `Invoke-Expression`. It owns a unique raw `<runId>` directory and report name, requires exactly one completed nonzero TRX per row, rejects a total below the row's existing `minimumExpectedTests` or any missing existing `requiredClasses`, invokes the scenario verifier when `scenarioScope` is non-null, protects the TRX to `<ProtectedResultsDirectory>/<runId>.trx`, privacy-scans the complete protected directory, and writes/merges a privacy-safe `scoped-run-index.json`. Each index object has exactly `runId`, `protectedTrxPattern`, `testedHead`, `total`, `classes`, and `protectedTrxSha256`; the digest is recomputed from the named protected file. It rejects duplicate outputs, a raw/safe file outside its roots, a campaign mismatch, an unapproved command, a missing controlled model for `Trusted`, a failed privacy/scenario/orphan check, or a register row it cannot execute. Raw files and the controlled model remain outside the repository and every upload tree.

`RegisteredRunInvokerContractTests` uses a bounded `dotnet --no-build` invocation of an existing harmless filtered test for the valid path, synthetic invalid registers/TRX fixtures for fail-closed paths, and the real Task 11 protector/scanner. It proves closed campaign selection, literal argument handling, exact one-row/one-file naming, run-ID/pattern index binding, digest recomputation, scenario dispatch, and rejection of duplicate/missing/extra output, command-injection text, stale head, and privacy/orphan failure. The nested valid-path filter must exclude `RegisteredRunInvokerContractTests` to prevent recursion. A changed workflow invocation must fail its equivalence contract until both the workflow and token array agree.

- [ ] **Step 4: Give every relevant execution and scan step a stable ID**

In the permanent workflow use:

```text
contract_tests
transport_tests
worker_tests
worker_client_tests
worker_process_tests
packaged_tests
gate2_privacy_scan
packaged_privacy_scan
gate2_orphan_check
worker_process_orphan_check
llama_native_orphan_check
llama_trusted_orphan_check
```

Use similarly explicit IDs for deterministic LLama tests, contained native tests, trusted real-model tests, model-integrity verification, and each LLama artifact scan.

- [ ] **Step 5: Scan every upload root**

- Protected-worker Gate 2 converts private raw TRX into privacy-safe TRX, then scans only the sanitized TRX and hash manifests in the upload root.
- Packaged handling separately converts private raw TRX and scans only sanitized TRX under the packaged upload root.
- Tier 1 scan covers the complete proposed Tier 1 upload tree, not only GGUF-named files.
- Trusted-model scan covers the complete proposed trusted upload tree and receives the controlled model’s paired approved `sha256:length` identity.

All scan steps use `if: ${{ always() }}` so an unsafe artifact cannot hide behind an earlier test failure.

- [ ] **Step 6: Gate upload on scan and execution**

Use logical conditions equivalent to:

```yaml
if: >-
  ${{
    always() &&
    steps.gate2_privacy_scan.outcome == 'success' &&
    steps.contract_tests.outcome == 'success' &&
    steps.transport_tests.outcome == 'success' &&
    steps.worker_tests.outcome == 'success' &&
    steps.worker_client_tests.outcome == 'success' &&
    steps.worker_process_tests.outcome == 'success' &&
    steps.gate2_orphan_check.outcome == 'success'
  }}
```

The packaged upload depends on `packaged_privacy_scan`, `packaged_tests`, and the permanent workflow's `gate2_orphan_check`. Tier 1 depends on its deterministic/native execution steps, scan, and `llama_native_orphan_check`. Trusted-model upload depends on trusted tests, post-test model integrity, scan, and `llama_trusted_orphan_check`. The focused process upload, if retained separately, depends on `worker_process_orphan_check`. A failed/skipped execution, failed scan, or failed/skipped orphan check means no upload.

- [ ] **Step 7: Make focused process orphan verification unconditional**

Add a final verification step to `.github/workflows/model-inspection-worker-process-tests.yml` with `id: worker_process_orphan_check` and `if: ${{ always() }}`. Place it after execution/scanning but before any upload step. It checks both exact process names:

```text
GraniteEdgeAI.ModelInspection.Worker
GraniteEdgeAI.ModelInspection.ProtocolTestWorker
```

Because the descendant fixture is another copy of the same apphost, this covers fixture descendants too. If found, record only IDs/names, terminate those exact processes for runner hygiene, then fail the job. Add the production name to every per-iteration check as well.

Give the equivalent existing always-run step in `build-and-test.yml` the ID `gate2_orphan_check`, place it before all protected-worker Gate 2 and packaged uploads, and require that outcome in every permanent upload condition. It checks the same two exact names after both real-process and packaged execution; no upload may proceed when the check is failed or skipped.

- [ ] **Step 8: Add always-run LLama apphost checks**

At the end of contained native and trusted-model execution, but before uploads, add always-run steps with IDs `llama_native_orphan_check` and `llama_trusted_orphan_check`. Check the exact apphost process name `GraniteEdgeAI.ModelInspection.LlamaSharpSpike`. Add a contract assertion tying that value to `PublishedProbeLocation.ExecutableFileName`. Do not scan or terminate generic `dotnet`, `pwsh`, or shell processes. Fail after exact-name cleanup if any remain.

- [ ] **Step 9: Run scanner and workflow contracts**

Before this full Contracts run, register all three new scripts plus `ArtifactPrivacyGateTests.cs` and `RegisteredRunInvokerContractTests.cs` in the cleanup source list/inventory and run the cleanup verifier, as required by the global inventory checkpoint.

```powershell
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -SelfTest
$privateResults = Join-Path ([System.IO.Path]::GetTempPath()) `
  ("mi-private-trx-" + [Guid]::NewGuid().ToString("N"))
$safeResults = Join-Path ([System.IO.Path]::GetTempPath()) `
  ("mi-safe-trx-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $safeResults | Out-Null
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~WorkerProtocolTests.WorkerProtocol_UsesApprovedIdentityLimitsAndTimeouts" `
  --results-directory $privateResults `
  --report-trx `
  --report-trx-filename raw.trx
& scripts/model-inspection/Protect-ModelInspectionTrx.ps1 `
  -InputPath (Join-Path $privateResults "raw.trx") `
  -OutputPath (Join-Path $safeResults "result.trx")
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 `
  -Path $safeResults
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~ArtifactPrivacyGateTests|FullyQualifiedName~RegisteredRunInvokerContractTests|FullyQualifiedName~BuildWorkflowContractTests"
```

Expected GREEN: unsafe fixtures are rejected, safe fixtures pass, every upload is execution+scan gated, and every relevant workflow has an always-run exact-name orphan check.

- [ ] **Step 10: Update evidence and commit**

Update `MI-TC-053`, `MI-TC-054`, and the upload/orphan audit findings.

```powershell
git add scripts/model-inspection `
  .github/workflows `
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit -m "fix(model-inspection): hard-gate retained evidence"
```

---

### Task 12: Add line/branch coverage and targeted mutation evidence

**Files:**

- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj`
- Modify: `tools/ModelInspection.LlamaSharpSpike.Tests/Metadata/VocabOnlyCollectorSourceContractTests.cs`
- Modify: `tests/model-inspection-test-projects.json`
- Create: `scripts/model-inspection/Collect-ModelInspectionCoverage.ps1`
- Create: `scripts/model-inspection/Protect-ModelInspectionCoverage.ps1`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `.github/workflows/llamasharp-feasibility-smoke.yml`
- Modify: `tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/BuildWorkflowContractTests.cs`
- Create after execution: `docs/testing/evidence/2026-08-08-model-inspection-coverage-and-mutation.md`
- Create after execution: `docs/testing/evidence/2026-08-08-model-inspection-coverage-ledger.json`
- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`

**Consumes:** The final focused suites, Microsoft Testing Platform, executable failure-mode tests, and the coverage-as-gap-detector rule.

**Produces:** Parseable Cobertura line/branch evidence for model-free managed boundaries and proof that selected high-risk defects are killed by the intended tests, without an arbitrary coverage percentage gate.

- [ ] **Step 1: Add the supported MTP coverage extension**

Add this exact test-only package to the six listed MTP projects:

```xml
<PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage"
                  Version="18.8.1" />
```

Keep the existing test SDK/MTP/TRX versions pinned. The command contract comes from the [official Microsoft Testing Platform code-coverage documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage).

- [ ] **Step 2: Create a deterministic coverage collector**

`Collect-ModelInspectionCoverage.ps1`:

- creates the unique raw result directory under `$env:TEMP`, outside the repository and every upload root, and creates only the protected output under `artifacts/model-inspection/test-completeness/coverage/protected`;
- invokes each model-free project with:

```text
--coverage
--coverage-output <unique-project-path>
--coverage-output-format cobertura
```

- supplies Release, x64/runtime arguments where the project requires them;
- rejects a missing, empty, malformed, or duplicate Cobertura file;
- passes each raw XML through `Protect-ModelInspectionCoverage.ps1`, which replaces `<sources>` with a fixed non-path value, replaces every `filename` with a stable opaque `mi-source-<sha256>.cs` identifier, rejects outside-repository raw filenames, and proves package/class names, line/branch identities, and all counters are unchanged after reparse; no absolute or repository-relative path enters protected XML;
- requires line and branch metrics to be present for each owned production assembly;
- excludes test assemblies from the summarized production table;
- records uncovered classes/lines/branches but does not fail on a percentage; and
- runs the artifact privacy scanner against only the protected XML before any retention.

Raw Cobertura is private transient input only: it must never be created beneath `artifacts/`, copied into the repository, or included in any upload path. The only coverage upload root is the protected subtree `artifacts/model-inspection/test-completeness/coverage/protected/**`.

The process suite measures the in-process WorkerClient/contract/transport code; it does not claim child-process worker coverage. Worker-host coverage comes from its in-process unit project.

- [ ] **Step 3: Make CI collect coverage without hiding ordinary test results**

Emit coverage from exactly six canonical unfiltered full-project commands: Contracts, Transport, Worker, WorkerClient, and WorkerProcess in `build-and-test.yml`, plus deterministic LLama in `llamasharp-feasibility-smoke.yml`. Add coverage flags/output paths only to those named steps. Focused workflows and every process `Focused`, `Main`, `Repeat`, or `Crash` command must not pass a coverage flag, create Cobertura, or include a coverage path in its upload. Keep native/trusted TRX reporting and existing floors. Narrow coverage uploads to `artifacts/model-inspection/test-completeness/coverage/protected/**`; the raw `$env:TEMP` directory is never copied, scanned as upload evidence, or uploaded.

Update the exact six corresponding `workflowRuns` entries in `tests/model-inspection-test-projects.json` to `coverageProducer: true` with their unique `protectedCoveragePattern`; keep every other row false/null and preserve the register's `Development`/null-evidence lifecycle until Task 13. Their tokenized `localInvocation.arguments` remains the uninstrumented test-semantic command used by the Task 11 exact-head replay. `BuildWorkflowContractTests` may normalize away coverage arguments only for those six rows and must prove that each removed token exactly matches the registered coverage pattern/collector contract. This makes the Task 12 workflow edits and register one atomic change while ensuring the Task 13 registered-run invoker never creates duplicate or narrow coverage.

Extend `BuildWorkflowContractTests` with exact-step assertions for the six canonical coverage producers and negative assertions over every other managed command/upload. Require one unique protected Cobertura pattern per canonical project and exact agreement with each row's `coverageProducer`/`protectedCoveragePattern`; reject an unregistered, duplicate, false/null-mismatched, or narrow-scope coverage producer. The permanent workflow's aggregate artifact may carry the five distinct canonical files; the LLama feasibility artifact carries only the deterministic file.

- [ ] **Step 4: Run and map coverage gaps**

```powershell
& scripts/model-inspection/Collect-ModelInspectionCoverage.ps1
```

For every uncovered branch in an owned production class:

1. map it to an existing `MI-TC-*` row;
2. add a missing realistic test when the branch is reachable and meaningful;
3. record generated/unreachable/defensive branches with a concrete rationale; and
4. rerun the affected project and collector.

Generate `2026-08-08-model-inspection-coverage-ledger.json` from the six canonical protected XML files only. For each file record the canonical project result ID, owning workflow/step, unique protected Cobertura pattern and tested head; then record package/class identity, opaque source ID, line number/hits, branch condition/coverage, and document/package/class line/branch counters. Sort every collection ordinally and reject duplicate identities. Assert the ledger has exactly those six canonical mappings and no narrow scope. The ledger contains no source path or raw XML excerpt, passes the artifact privacy scanner, and reparses to the same counters as every protected Cobertura file. The Markdown evidence page explains gaps/rationales and links this machine-readable ledger; Task 13 uses the JSON for exact hosted reconciliation.

Do not add tests merely to execute trivial auto-properties or generated XAML.

Replace lexical-only evidence for `MI-QT-019` with a semantic member-call contract in `VocabOnlyCollectorSourceContractTests`. Decode `call`, `callvirt`, and `newobj` tokens from `VocabOnlyEvidenceCollector.Collect` and its private helpers, resolve each token through `Module.ResolveMethod`, and assert the exact forbidden LLama/native hyperparameter getter identities are absent while the approved metadata projection, chat-template factory, vocabulary, and tokenizer calls are present. Keep the existing source-string scan only as a supplementary diagnostic; aliases, whitespace, or local renames must not evade the semantic assertion.

- [ ] **Step 5: Run targeted mutations in disposable worktrees**

Before creating mutation worktrees, register the two new coverage scripts in the cleanup source list/inventory, run the inventory verifier and affected tests, and stage every non-mutant Task 12 implementation/test/script/workflow change. Review the staged diff to prove it contains no mutant, then create one temporary local candidate commit:

```powershell
git diff --cached --check
git commit -m "test(model-inspection): stage mutation candidate"
$mutationCandidate = git rev-parse HEAD
```

From `$mutationCandidate`, create one verified disposable worktree per mutant. Apply one mutation at a time with `apply_patch`, run only the named detecting test, require a nonzero test exit, record the failing test, then remove that validated disposable worktree. Never commit a mutant. This temporary commit is amended into the single final Task 12 commit in Step 8; it is not pushed.

Run these mutations:

| Mutant | Required detecting evidence |
|---|---|
| remove one `IntegrityPreserved` equality guard | `ModelFileIntegrityTrueRejectsContradictoryEvidence` row |
| remove `tokenizer.Validate()` from composed evidence | null-known-token test |
| stop recursive duplicate-name detection | nested/array duplicate JSON test |
| remove the initial reader cancellation check | buffered pre-cancel transport test |
| release writer serialization before flush | flush-transaction concurrency test |
| allow a second terminal winner | terminal race test |
| accept EOF without a terminal after start | crash-after-start/exit-without-terminal process test |
| reconstruct the import request before navigation | real event-pipeline `AreSame` test |
| remove the page repeated-Loaded guard | repeated-Loaded presentation-reference test |
| remove one privacy/upload outcome condition | workflow contract test |
| remove `always()` from final orphan check | workflow contract test |
| replace one metadata-projection read with `LLamaWeights.ContextSize` or another forbidden native getter | semantic resolved-member-call contract |

If a mutant survives, remove its worktree, strengthen the correct-layer test on the main worktree, rerun RED/GREEN, stage and amend the temporary candidate, refresh `$mutationCandidate`, and repeat that mutant from the new clean candidate. Do not report a mutation score; report every named mutant as killed or still open.

- [ ] **Step 6: Regenerate final coverage and retain a privacy-safe coverage/mutation record**

After the final mutant is killed and after the last survivor-driven test amendment, run the complete collector once more from the updated `$mutationCandidate` source/test tree:

```powershell
& scripts/model-inspection/Collect-ModelInspectionCoverage.ps1
if ($LASTEXITCODE -ne 0) { throw "Final post-mutation coverage collection failed: $LASTEXITCODE" }
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 `
  -Path artifacts/model-inspection/test-completeness/coverage/protected
if ($LASTEXITCODE -ne 0) { throw "Final protected coverage privacy scan failed: $LASTEXITCODE" }
```

Regenerate both the Markdown coverage/mutation record and `2026-08-08-model-inspection-coverage-ledger.json` from this final protected output. Reparse the JSON against every protected Cobertura file and require exact package/class/source/line/branch identity and counter equality. No earlier pre-survivor ledger may remain. Stage the regenerated records for the final Step 8 amend.

Record:

- exact candidate head and clean/dirty state;
- package and tool versions;
- one row per coverage file with path-minimized assembly, covered/total lines and branches, and SHA-256;
- every uncovered high-risk branch disposition;
- one row per named mutant with patch digest, detecting test, nonzero exit, and sanitized log digest; and
- privacy-scan result.

No source path, username, raw mutation diff containing a model path, or coverage binary is committed.

Before the full Contracts regression, register both new coverage scripts, the coverage/mutation evidence document, and the machine-readable coverage ledger in the cleanup source list/inventory and run the cleanup verifier.

- [ ] **Step 7: Run all affected regressions**

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj --configuration Release
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj --configuration Release
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj --configuration Release
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj --configuration Release -p:Platform=x64
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj --configuration Release --runtime win-x64 -p:Platform=x64
dotnet test tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj --configuration Release --minimum-expected-tests 170
```

- [ ] **Step 8: Update evidence and commit**

Update `MI-TC-055`, `MI-QT-019`, and every matrix row whose evidence was strengthened after coverage/mutation review.

```powershell
git add tests `
  tools/ModelInspection.LlamaSharpSpike.Tests `
  scripts/model-inspection `
  .github/workflows `
  docs/testing `
  docs/reviews/model-inspection-cleanup-source-files.txt `
  docs/reviews/model-inspection-cleanup-inventory.md
git commit --amend -m "test(model-inspection): add coverage and mutation evidence"
```

Expected: exactly one final Task 12 commit contains all non-mutant changes and privacy-safe evidence; the temporary candidate message no longer appears in branch history.

---

### Task 13: Verify the exact closure head and publish the handoff record

**Files:**

- Modify: `docs/testing/Model-Inspection-Test-Completeness-Matrix.md`
- Complete: `docs/testing/evidence/2026-08-08-model-inspection-test-completeness-ledger.md`
- Create: `docs/testing/evidence/2026-08-08-model-inspection-test-completeness-closure.md`
- Modify: `docs/reviews/model-inspection-cleanup-source-files.txt`
- Modify: `docs/reviews/model-inspection-cleanup-inventory.md`
- Modify: `tests/model-inspection-test-projects.json`
- Modify: `tests/README.md`
- Modify: `docs/testing/evidence/README.md`
- Modify: `docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md`
- Modify: `docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`
- Modify: `.github/workflows/build-and-test.yml`
- Modify: `.github/workflows/model-inspection-transport-tests.yml`
- Modify: `.github/workflows/model-inspection-worker-tests.yml`
- Modify: `.github/workflows/model-inspection-worker-client-tests.yml`
- Modify: `.github/workflows/model-inspection-worker-process-tests.yml`
- Modify: `.github/workflows/llamasharp-feasibility-smoke.yml`
- Modify: `.github/workflows/llamasharp-real-model-integration.yml`
- Verify: `scripts/model-inspection/Verify-ModelInspectionHostedArtifacts.ps1`
- Verify: `tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json`
- Verify: `docs/testing/evidence/2026-08-08-model-inspection-coverage-ledger.json`
- Update: draft pull-request body after the final commit; do not edit repository files after exact-head CI

**Consumes:** Completed Tasks 1–12, all fresh local evidence, the trusted controlled model, hosted Windows workflows, and the approved roadmap.

**Produces:** An independently reviewable test-completeness closure candidate at one immutable commit, with all open cleanup/production work routed to the ordered roadmap and no false claim that Model Inspection itself is finished.

- [ ] **Step 1: Use verification-before-completion**

Load `superpowers:verification-before-completion` and `superpowers:requesting-code-review`. Review `git diff` and the full commit range against the exact base. Have independent reviewers inspect the completed Tasks 1–12 across contracts/protocol/transport, worker/WorkerClient/process, packaged WinUI/accessibility/filesystem, workflows/privacy/orphan/coverage/evidence, and roadmap/matrix traceability. Confirm no downstream production Gates 3–6 implementation, protocol-policy change, model asset, generated output, or unrelated user change entered the branch. Resolve every critical/important source, test, script, and workflow finding, commit those corrections, and require a clean worktree before Step 2.

- [ ] **Step 2: Re-run the complete local Windows ladder**

Run, in order:

```powershell
$ErrorActionPreference = 'Stop'
$rawResults = Join-Path ([IO.Path]::GetTempPath()) ('mi-final-raw-' + [Guid]::NewGuid().ToString('N'))
$safeResults = Join-Path ([IO.Path]::GetTempPath()) ('mi-final-safe-' + [Guid]::NewGuid().ToString('N'))
$preClosureHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the tested head: $LASTEXITCODE" }
$repositoryRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the repository root: $LASTEXITCODE" }
$preClosureStatus = @(git status --short)
if ($LASTEXITCODE -ne 0) { throw "Unable to read pre-closure status: $LASTEXITCODE" }
if ($preClosureStatus.Count -ne 0) { throw 'The complete ladder must start from a clean committed head.' }
$closureOnlyPaths = @(
  'docs/testing/Model-Inspection-Test-Completeness-Matrix.md'
  'docs/testing/evidence/2026-08-08-model-inspection-test-completeness-ledger.md'
  'docs/testing/evidence/2026-08-08-model-inspection-test-completeness-closure.md'
  'docs/reviews/model-inspection-cleanup-source-files.txt'
  'docs/reviews/model-inspection-cleanup-inventory.md'
  'tests/model-inspection-test-projects.json'
  'tests/README.md'
  'docs/testing/evidence/README.md'
  'docs/testing/evidence/2026-08-05-model-inspection-worker-gate2-verification.md'
  'docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md'
  'IBM Granite with TurboQuant (Intel)/Features/README.md'
  '.github/workflows/build-and-test.yml'
  '.github/workflows/model-inspection-transport-tests.yml'
  '.github/workflows/model-inspection-worker-tests.yml'
  '.github/workflows/model-inspection-worker-client-tests.yml'
  '.github/workflows/model-inspection-worker-process-tests.yml'
  '.github/workflows/llamasharp-feasibility-smoke.yml'
  '.github/workflows/llamasharp-real-model-integration.yml'
)
$priorRegister = Get-Content -LiteralPath tests/model-inspection-test-projects.json -Raw | ConvertFrom-Json
if ($priorRegister.evidenceState -cne 'Development' -or
    $null -ne $priorRegister.evidenceHead -or
    @($priorRegister.projects | Where-Object { $null -ne $_.countEvidence }).Count -ne 0 -or
    @($priorRegister.workflowRuns | Where-Object { $null -ne $_.countEvidence }).Count -ne 0) {
    throw 'The pre-closure register is not in the required Development/null-evidence lifecycle.'
}
$locatorPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-final-$preClosureHead.locator.json")
[ordered]@{
  RepositoryRoot = $repositoryRoot
  TestedHead = $preClosureHead
  RawResults = $rawResults
  SafeResults = $safeResults
  ClosureOnlyPaths = $closureOnlyPaths
  RegisterSnapshot = $priorRegister
} |
  ConvertTo-Json -Depth 12 |
  Set-Content -LiteralPath $locatorPath -Encoding utf8
$resultNames = @('contracts','transport','worker','worker-client','worker-process','packaged','llama-deterministic','llama-native','llama-trusted')
foreach ($name in $resultNames) {
    New-Item -ItemType Directory -Path (Join-Path $rawResults $name) -Force | Out-Null
}
New-Item -ItemType Directory -Path $safeResults -Force | Out-Null

dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj --configuration Release --results-directory (Join-Path $rawResults 'contracts') --report-trx --report-trx-filename contracts.trx
if ($LASTEXITCODE -ne 0) { throw "Contracts failed: $LASTEXITCODE" }
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj --configuration Release --results-directory (Join-Path $rawResults 'transport') --report-trx --report-trx-filename transport.trx
if ($LASTEXITCODE -ne 0) { throw "Transport failed: $LASTEXITCODE" }
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj --configuration Release --results-directory (Join-Path $rawResults 'worker') --report-trx --report-trx-filename worker.trx
if ($LASTEXITCODE -ne 0) { throw "Worker failed: $LASTEXITCODE" }
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj --configuration Release -p:Platform=x64 --results-directory (Join-Path $rawResults 'worker-client') --report-trx --report-trx-filename worker-client.trx
if ($LASTEXITCODE -ne 0) { throw "WorkerClient failed: $LASTEXITCODE" }
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj --configuration Release --runtime win-x64 -p:Platform=x64 --results-directory (Join-Path $rawResults 'worker-process') --report-trx --report-trx-filename worker-process.trx
if ($LASTEXITCODE -ne 0) { throw "Worker process failed: $LASTEXITCODE" }
& scripts/model-inspection/Invoke-PackagedModelInspectionTests.ps1 -Configuration Release -ResultsDirectory (Join-Path $rawResults 'packaged')
if ($LASTEXITCODE -ne 0) { throw "Packaged tests failed: $LASTEXITCODE" }
dotnet test tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj --configuration Release --minimum-expected-tests 170 --results-directory (Join-Path $rawResults 'llama-deterministic') --report-trx --report-trx-filename llama-deterministic.trx
if ($LASTEXITCODE -ne 0) { throw "Deterministic LLama tests failed: $LASTEXITCODE" }
& scripts/model-inspection/Collect-ModelInspectionCoverage.ps1
if ($LASTEXITCODE -ne 0) { throw "Coverage collection failed: $LASTEXITCODE" }
& scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1
if ($LASTEXITCODE -ne 0) { throw "Cleanup inventory failed: $LASTEXITCODE" }

$localTrx = @(Get-ChildItem -LiteralPath $rawResults -Filter '*.trx' -File -Recurse)
if ($localTrx.Count -ne 7) { throw "Expected seven model-free/local TRX files, found $($localTrx.Count)." }
foreach ($trx in $localTrx) {
    $safeName = "$($trx.Directory.Name).trx"
    & scripts/model-inspection/Protect-ModelInspectionTrx.ps1 -InputPath $trx.FullName -OutputPath (Join-Path $safeResults $safeName)
    if ($LASTEXITCODE -ne 0) { throw "TRX protection failed for $($trx.Name): $LASTEXITCODE" }
}
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $safeResults
if ($LASTEXITCODE -ne 0) { throw "Local TRX privacy scan failed: $LASTEXITCODE" }
$scopedRawResults = Join-Path $rawResults 'scoped'
$scopedSafeResults = Join-Path $safeResults 'scoped'
& scripts/model-inspection/Invoke-ModelInspectionRegisteredRuns.ps1 `
  -RegisterPath tests/model-inspection-test-projects.json `
  -RawResultsDirectory $scopedRawResults `
  -ProtectedResultsDirectory $scopedSafeResults `
  -Campaign ModelFree `
  -TestedHead $preClosureHead `
  -ScenarioManifestPath tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json
if ($LASTEXITCODE -ne 0) { throw "Registered model-free runs failed: $LASTEXITCODE" }
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $safeResults
if ($LASTEXITCODE -ne 0) { throw "Model-free scoped evidence privacy scan failed: $LASTEXITCODE" }
```

All discovered tests must execute and pass with zero skipped/not-executed/inconclusive/aborted/timeout results.

- [ ] **Step 3: Re-run contained native and trusted-model campaigns**

Run from the repository root on the approved Windows x64 target. `GRANITE_TEST_MODEL_PATH` must already identify the staged read-only controlled model outside the repository:

```powershell
$ErrorActionPreference = 'Stop'
$spikeProject = 'tools/ModelInspection.LlamaSharpSpike/ModelInspection.LlamaSharpSpike.csproj'
$nativeProject = 'tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj'
$trustedProject = 'tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj'
$publishDirectory = Join-Path ([IO.Path]::GetTempPath()) ('mi-llama-publish-' + [Guid]::NewGuid().ToString('N'))
$trustedEvidence = Join-Path ([IO.Path]::GetTempPath()) ('mi-llama-evidence-' + [Guid]::NewGuid().ToString('N'))
$modelPath = [IO.Path]::GetFullPath($env:GRANITE_TEST_MODEL_PATH)
$model = Get-Item -LiteralPath $modelPath -ErrorAction Stop
$repositoryRoot = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
if ($modelPath.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The controlled model must remain outside the repository.'
}
if ($model.Name -cne 'granite-4.1-3b-Q4_K_M.gguf' -or
    $model.Length -ne [int64]2099501664 -or
    -not $model.IsReadOnly) {
    throw 'Controlled-model filename, length, or read-only state is invalid.'
}
$expectedHash = '662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29'
$hashBefore = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hashBefore -ne $expectedHash) { throw 'Controlled-model SHA-256 is invalid.' }

$preClosureHead = git rev-parse HEAD
$locatorPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-final-$preClosureHead.locator.json")
if (-not (Test-Path -LiteralPath $locatorPath -PathType Leaf)) {
    throw 'Step 2 private/safe result locator is unavailable.'
}
$resultLocator = Get-Content -LiteralPath $locatorPath -Raw | ConvertFrom-Json
$rawResults = [string]$resultLocator.RawResults
$safeResults = [string]$resultLocator.SafeResults
$currentHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $currentHead -cne [string]$resultLocator.TestedHead) {
    throw 'The native/trusted campaign no longer matches the clean head tested in Step 2.'
}
$currentRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0 -or $currentRoot -cne [string]$resultLocator.RepositoryRoot) {
    throw 'The result locator belongs to a different repository.'
}

$campaignFailures = [System.Collections.Generic.List[System.Exception]]::new()
try {
    New-Item -ItemType Directory -Path $publishDirectory,$trustedEvidence | Out-Null
    dotnet publish $spikeProject --configuration Release --runtime win-x64 --self-contained false --output $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw "Probe publish failed: $LASTEXITCODE" }
    $env:LLAMASHARP_SPIKE_PUBLISH_DIR = $publishDirectory
    $env:LLAMASHARP_REAL_MODEL_EVIDENCE_DIR = $trustedEvidence

    dotnet test $nativeProject --configuration Release --runtime win-x64 --filter 'TestCategory=NativeIntegration' --minimum-expected-tests 4 --results-directory (Join-Path $rawResults 'llama-native') --report-trx --report-trx-filename llama-native.trx
    if ($LASTEXITCODE -ne 0) { throw "Contained native tests failed: $LASTEXITCODE" }
    dotnet test $trustedProject --configuration Release --runtime win-x64 --filter 'TestCategory=RealModelIntegration' --minimum-expected-tests 20 --results-directory (Join-Path $rawResults 'llama-trusted') --report-trx --report-trx-filename llama-trusted.trx
    if ($LASTEXITCODE -ne 0) { throw "Trusted real-model tests failed: $LASTEXITCODE" }

    & scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 `
      -Path $trustedEvidence `
      -ApprovedModelIdentity "$expectedHash`:2099501664"
    if ($LASTEXITCODE -ne 0) { throw "Trusted evidence privacy scan failed: $LASTEXITCODE" }

    $nativeTrx = @(
        Get-Item -LiteralPath (Join-Path (Join-Path $rawResults 'llama-native') 'llama-native.trx')
        Get-Item -LiteralPath (Join-Path (Join-Path $rawResults 'llama-trusted') 'llama-trusted.trx')
    )
    if ($nativeTrx.Count -ne 2 -or
        $nativeTrx[0].Directory.Name -cne 'llama-native' -or $nativeTrx[0].Name -cne 'llama-native.trx' -or
        $nativeTrx[1].Directory.Name -cne 'llama-trusted' -or $nativeTrx[1].Name -cne 'llama-trusted.trx') {
        throw 'The exact canonical native/trusted TRX pair is missing or misnamed.'
    }
    foreach ($trx in $nativeTrx) {
        $safeName = "$($trx.Directory.Name).trx"
        & scripts/model-inspection/Protect-ModelInspectionTrx.ps1 -InputPath $trx.FullName -OutputPath (Join-Path $safeResults $safeName)
        if ($LASTEXITCODE -ne 0) { throw "TRX protection failed for $($trx.Name): $LASTEXITCODE" }
    }
    & scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $safeResults
    if ($LASTEXITCODE -ne 0) { throw "Final safe TRX privacy scan failed: $LASTEXITCODE" }

    $scopedRawResults = Join-Path $rawResults 'scoped'
    $scopedSafeResults = Join-Path $safeResults 'scoped'
    & scripts/model-inspection/Invoke-ModelInspectionRegisteredRuns.ps1 `
      -RegisterPath tests/model-inspection-test-projects.json `
      -RawResultsDirectory $scopedRawResults `
      -ProtectedResultsDirectory $scopedSafeResults `
      -Campaign Native `
      -TestedHead $preClosureHead `
      -ScenarioManifestPath tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json `
      -ControlledModelPath $modelPath
    if ($LASTEXITCODE -ne 0) { throw "Registered native runs failed: $LASTEXITCODE" }
    & scripts/model-inspection/Invoke-ModelInspectionRegisteredRuns.ps1 `
      -RegisterPath tests/model-inspection-test-projects.json `
      -RawResultsDirectory $scopedRawResults `
      -ProtectedResultsDirectory $scopedSafeResults `
      -Campaign Trusted `
      -TestedHead $preClosureHead `
      -ScenarioManifestPath tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json `
      -ControlledModelPath $modelPath
    if ($LASTEXITCODE -ne 0) { throw "Registered trusted runs failed: $LASTEXITCODE" }
    & scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $safeResults
    if ($LASTEXITCODE -ne 0) { throw "All scoped evidence privacy scan failed: $LASTEXITCODE" }

    $canonicalResultIds = @(
        'contracts','transport','worker','worker-client','worker-process',
        'packaged','llama-deterministic','llama-native','llama-trusted'
    )
    $allRawTrx = @(
        $canonicalResultIds | ForEach-Object {
            Get-ChildItem -LiteralPath (Join-Path $rawResults $_) -Filter '*.trx' -File -Recurse
        }
    )
    $allSafeTrx = @(Get-ChildItem -LiteralPath $safeResults -Filter '*.trx' -File)
    if ($allRawTrx.Count -ne 9 -or $allSafeTrx.Count -ne 9) {
        throw "Expected nine raw and nine protected TRX files; found $($allRawTrx.Count) and $($allSafeTrx.Count)."
    }
}
catch {
    # Preserve the primary publish/test/privacy failure. Integrity and process
    # cleanup below are independent invariants and must not mask it.
    $campaignFailures.Add($_.Exception)
}
finally {
    try {
        $hashAfter = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hashAfter -ne $hashBefore) {
            throw [InvalidOperationException]::new('The controlled model changed during testing.')
        }
    }
    catch {
        $campaignFailures.Add($_.Exception)
    }

    $orphanNames = @(
        'GraniteEdgeAI.ModelInspection.Worker'
        'GraniteEdgeAI.ModelInspection.ProtocolTestWorker'
        'GraniteEdgeAI.ModelInspection.LlamaSharpSpike'
    )
    $orphan = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $orphanNames -contains $_.ProcessName })
    if ($orphan.Count -ne 0) {
        $orphan | Stop-Process -Force -ErrorAction SilentlyContinue
        $campaignFailures.Add([InvalidOperationException]::new(
            'A worker, fixture, or LLamaSharp apphost remained after final testing and was cleaned.'))
    }
}

if ($campaignFailures.Count -eq 1) {
    throw $campaignFailures[0]
}
if ($campaignFailures.Count -gt 1) {
    throw [AggregateException]::new(
        'The final native/trusted campaign failed more than one independent invariant.',
        $campaignFailures.ToArray())
}
```

Require:

- deterministic `170` minimum;
- contained native `4` minimum;
- trusted real-model `20` minimum;
- exact controlled model SHA-256 `662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29`;
- exact length `2099501664`;
- model integrity unchanged;
- privacy scan passed;
- no listener/port introduced; and
- no worker, fixture, feasibility apphost, or descendant remains.

- [ ] **Step 4: Complete the matrix and closure record before the final commit**

For every `MI-TC-*`, `MI-QT-*`, `MI-DEF-*`, and `MI-DOC-*` entry:

- link exact test(s), command, result count, and artifact/log digest;
- mark fixed defects resolved;
- retain honest partial/deferred states for production Gates 3–6 work;
- record approved protocol-policy decisions as still deferred;
- distinguish historical evidence from this fresh candidate evidence; and
- route every remaining task to the ordered completion-roadmap gate.

The committed closure record states: **this is the test-completeness closure candidate (Order 1); hosted exact-head verification and independent artifact inspection are pending, and the overall Model Inspection feature is not yet complete.** Keep `MI-TC-056` pending in the committed matrix. Only the post-run pull-request attestation may declare the test-completeness gate verified.

- [ ] **Step 5: Review the closure-only changes**

Repeat the independent review for the new matrix/ledger/closure and reconciliation changes. Inspect:

1. Contracts/protocol/transport;
2. worker/WorkerClient/process and failure precedence;
3. packaged WinUI/accessibility/filesystem;
4. workflows/privacy/orphan/coverage/evidence; and
5. roadmap/matrix traceability and downstream non-claims.

Resolve every critical/important finding or record an approved owner/destination/rationale. If a correction touches production source, test source, a project file, or an execution/protection script, commit it and discard the Step 2–3 result locator: return to Step 2 and rerun the complete nine-project/native/trusted ladder from that new clean head. Documentation-only corrections and numeric register/workflow-floor reconciliation remain allowed before Step 6; the final Contracts/build/inventory checks still validate them.

- [ ] **Step 6: Perform the final nine-project reconciliation**

After review corrections and after every Task 10–12 test has been added, parse the fresh canonical and registered-run MTP/TRX results from Steps 2–3 and update `tests/model-inspection-test-projects.json`, every workflow minimum, every focused filter/repeat minimum, and every required-class list. Assert exact agreement for all nine canonical executable projects and one-to-one registration of every emitted workflow TRX. Full-project entries use the fresh nine local totals; every `workflowRuns` entry uses the protected result freshly produced for its exact `runId` on the same `$preClosureHead`. Compare against the immutable pre-edit register snapshot in the private locator: no floor may decrease and every prior required class must remain in the final list and fresh TRX. Project/result/runner triples and every run-semantic field—workflow, step, scope, scenario/campaign coupling, protected patterns, coverage metadata, and tokenized local invocation—remain byte-for-byte equivalent after canonical projection. Only lifecycle/head, count-evidence anchors, non-decreasing numeric floors, and required-class supersets may change; any selection/filter/invocation change requires a corrective commit and return to Step 2. No Tasks 7–11 row is reused for closure, even when its filter is unchanged. The LLama project entries must come from the fresh deterministic/native/trusted results, not the earlier `170/4/20` retained baseline alone.

Register the new closure document and every file created since the last inventory checkpoint, update factual rows for changed workflows/tests/docs, and regenerate the sorted cleanup source list. Then run:

```powershell
$ErrorActionPreference = 'Stop'
$expectedResultIds = @(
    'contracts','transport','worker','worker-client','worker-process',
    'packaged','llama-deterministic','llama-native','llama-trusted'
)
$preClosureHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the pre-closure head: $LASTEXITCODE" }
$locatorPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-final-$preClosureHead.locator.json")
if (-not (Test-Path -LiteralPath $locatorPath -PathType Leaf)) {
    throw 'The Step 2/3 result locator is unavailable.'
}
$resultLocator = Get-Content -LiteralPath $locatorPath -Raw | ConvertFrom-Json
if ($preClosureHead -cne [string]$resultLocator.TestedHead) {
    throw 'The local evidence belongs to a different committed head; return to Step 2.'
}
$currentRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0 -or $currentRoot -cne [string]$resultLocator.RepositoryRoot) {
    throw 'The result locator belongs to a different repository.'
}
$trackedChanges = @(git diff --name-only $resultLocator.TestedHead --)
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect post-test tracked changes: $LASTEXITCODE" }
$untrackedChanges = @(git ls-files --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect post-test untracked changes: $LASTEXITCODE" }
$changedPaths = @($trackedChanges; $untrackedChanges) |
  Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
  Sort-Object -Unique
$unexpectedChanges = @($changedPaths | Where-Object { $_ -cnotin @($resultLocator.ClosureOnlyPaths) })
if ($unexpectedChanges.Count -ne 0) {
    throw "Source/test inputs changed after Step 2: $($unexpectedChanges -join ', ')."
}
$rawResults = [string]$resultLocator.RawResults
$safeResults = [string]$resultLocator.SafeResults
$priorProjects = @($resultLocator.RegisterSnapshot.projects)
$priorRuns = @($resultLocator.RegisterSnapshot.workflowRuns)
if ($priorProjects.Count -ne 9 -or $priorRuns.Count -eq 0) {
    throw 'The pre-edit register snapshot is absent or incomplete.'
}
$rawTrx = @(
    $expectedResultIds | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $rawResults $_) -Filter '*.trx' -File -Recurse
    }
)
$safeTrx = @(Get-ChildItem -LiteralPath $safeResults -Filter '*.trx' -File)
if ($rawTrx.Count -ne 9 -or $safeTrx.Count -ne 9) {
    throw "Expected nine raw and nine protected TRX files; found $($rawTrx.Count) and $($safeTrx.Count)."
}
$rawIds = @($rawTrx | ForEach-Object Directory | ForEach-Object Name)
$safeIds = @($safeTrx | ForEach-Object BaseName)
if (@($rawIds | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @($safeIds | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @(Compare-Object $expectedResultIds ($rawIds | Sort-Object)).Count -ne 0 -or
    @(Compare-Object $expectedResultIds ($safeIds | Sort-Object)).Count -ne 0) {
    throw 'The raw/protected TRX result-ID set does not match the nine-project register.'
}

function Read-TrxSummary([IO.FileInfo] $File) {
    [xml] $document = Get-Content -LiteralPath $File.FullName -Raw
    $counters = $document.TestRun.ResultSummary.Counters
    if ($null -eq $counters) { throw "TRX counters are absent from $($File.Name)." }
    $values = [ordered]@{}
    foreach ($name in @('total','executed','passed','failed','error','timeout','aborted','inconclusive','notExecuted','warning')) {
        $text = $counters.GetAttribute($name)
        $values[$name] = if ([string]::IsNullOrEmpty($text)) { 0 } else { [int]$text }
    }
    $values.Classes = @(
        $document.TestRun.TestDefinitions.UnitTest.TestMethod.className |
          Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
          Sort-Object -Unique
    )
    [pscustomobject] $values
}

$parsedResults = [ordered]@{}
foreach ($resultId in $expectedResultIds) {
    $rawFile = @($rawTrx | Where-Object { $_.Directory.Name -eq $resultId })[0]
    $safeFile = @($safeTrx | Where-Object { $_.BaseName -eq $resultId })[0]
    $raw = Read-TrxSummary $rawFile
    $safe = Read-TrxSummary $safeFile
    if (($raw | ConvertTo-Json -Depth 4 -Compress) -cne ($safe | ConvertTo-Json -Depth 4 -Compress)) {
        throw "TRX protection changed counters or class identities for $resultId."
    }
    if ($safe.total -le 0 -or $safe.executed -ne $safe.total -or $safe.passed -ne $safe.total -or
        $safe.failed -ne 0 -or $safe.error -ne 0 -or $safe.timeout -ne 0 -or
        $safe.aborted -ne 0 -or $safe.inconclusive -ne 0 -or $safe.notExecuted -ne 0) {
        throw "TRX execution is incomplete or failed for $resultId."
    }
    $priorProject = @($priorProjects | Where-Object { $_.resultId -ceq $resultId })[0]
    $missingPriorClasses = @($priorProject.requiredClasses | Where-Object { $_ -notin $safe.Classes })
    if ($null -eq $priorProject -or $safe.total -lt [int]$priorProject.minimumExpectedTests -or
        $missingPriorClasses.Count -ne 0) {
        throw "Canonical evidence regressed the prior floor/classes for $resultId."
    }
    $parsedResults[$resultId] = $safe
}

$summaryRows = @(
    $parsedResults.GetEnumerator() | ForEach-Object {
        [pscustomobject][ordered]@{
            resultId = $_.Key
            total = $_.Value.total
            classes = @($_.Value.Classes)
        }
    }
)
$summaryPath = Join-Path $safeResults 'reconciliation-summary.json'
$summaryRows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $safeResults
if ($LASTEXITCODE -ne 0) { throw "Reconciliation-summary privacy scan failed: $LASTEXITCODE" }
$summaryRows | Select-Object resultId,total | Format-Table -AutoSize
```

Use that privacy-safe summary only for the nine canonical full-project floors/classes. Apply the canonical project/result/runner mapping from Task 10. Use the registered-run invoker outputs from Steps 2–3 for every `workflowRuns` row; earlier Tasks 7–11 evidence remains development evidence but is never final closure `countEvidence`. Never derive a narrow floor/class subset from the nine-project summary. Atomically set `evidenceState` to `ExactHead`, `evidenceHead` to `$preClosureHead`, every project/run floor to the exact total in its fresh evidence row, and every `countEvidence` to the fixed ledger path plus its unique `#MI-EV-NNNN` selector. The selected row must match result ID, scope, exact run ID, protected pattern, `$preClosureHead`, total, required classes, complete zero-failure/zero-skip counters, recomputed protected TRX digest, and privacy-passed outcome. Do not copy raw TRX or temporary paths into the repository.

After the edits, start a fresh shell and independently reparse both result sets; do not depend on variables from the summarization shell:

```powershell
$ErrorActionPreference = 'Stop'
$canonicalProjects = @(
    [pscustomobject]@{ project='tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj'; resultId='contracts'; runner='MicrosoftTestingPlatform' }
    [pscustomobject]@{ project='tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj'; resultId='transport'; runner='MicrosoftTestingPlatform' }
    [pscustomobject]@{ project='tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj'; resultId='worker'; runner='MicrosoftTestingPlatform' }
    [pscustomobject]@{ project='tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj'; resultId='worker-client'; runner='MicrosoftTestingPlatform' }
    [pscustomobject]@{ project='tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj'; resultId='worker-process'; runner='MicrosoftTestingPlatform' }
    [pscustomobject]@{ project='tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'; resultId='packaged'; runner='PackagedVSTest' }
    [pscustomobject]@{ project='tools/ModelInspection.LlamaSharpSpike.Tests/ModelInspection.LlamaSharpSpike.Tests.csproj'; resultId='llama-deterministic'; runner='MicrosoftTestingPlatform' }
    [pscustomobject]@{ project='tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/ModelInspection.LlamaSharpSpike.NativeIntegrationTests.csproj'; resultId='llama-native'; runner='MicrosoftTestingPlatform' }
    [pscustomobject]@{ project='tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj'; resultId='llama-trusted'; runner='MicrosoftTestingPlatform' }
)
$expectedResultIds = @($canonicalProjects.resultId)
$preClosureHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve the pre-closure head: $LASTEXITCODE" }
$locatorPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-final-$preClosureHead.locator.json")
if (-not (Test-Path -LiteralPath $locatorPath -PathType Leaf)) {
    throw 'The Step 2/3 result locator is unavailable.'
}
$resultLocator = Get-Content -LiteralPath $locatorPath -Raw | ConvertFrom-Json
if ($preClosureHead -cne [string]$resultLocator.TestedHead) {
    throw 'The local evidence belongs to a different committed head; return to Step 2.'
}
$currentRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0 -or $currentRoot -cne [string]$resultLocator.RepositoryRoot) {
    throw 'The result locator belongs to a different repository.'
}
$trackedChanges = @(git diff --name-only $resultLocator.TestedHead --)
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect post-test tracked changes: $LASTEXITCODE" }
$untrackedChanges = @(git ls-files --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw "Unable to inspect post-test untracked changes: $LASTEXITCODE" }
$changedPaths = @($trackedChanges; $untrackedChanges) |
  Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
  Sort-Object -Unique
$unexpectedChanges = @($changedPaths | Where-Object { $_ -cnotin @($resultLocator.ClosureOnlyPaths) })
if ($unexpectedChanges.Count -ne 0) {
    throw "Source/test inputs changed after Step 2: $($unexpectedChanges -join ', ')."
}
$rawResults = [string]$resultLocator.RawResults
$safeResults = [string]$resultLocator.SafeResults
$priorProjects = @($resultLocator.RegisterSnapshot.projects)
$priorRuns = @($resultLocator.RegisterSnapshot.workflowRuns)
if ($priorProjects.Count -ne 9 -or $priorRuns.Count -eq 0) {
    throw 'The pre-edit register snapshot is absent or incomplete.'
}
$rawTrx = @(
    $expectedResultIds | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $rawResults $_) -Filter '*.trx' -File -Recurse
    }
)
$safeTrx = @(Get-ChildItem -LiteralPath $safeResults -Filter '*.trx' -File)
if ($rawTrx.Count -ne 9 -or $safeTrx.Count -ne 9) {
    throw "Expected nine raw and nine protected TRX files; found $($rawTrx.Count) and $($safeTrx.Count)."
}
$rawIds = @($rawTrx | ForEach-Object Directory | ForEach-Object Name)
$safeIds = @($safeTrx | ForEach-Object BaseName)
if (@($rawIds | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @($safeIds | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @(Compare-Object $expectedResultIds ($rawIds | Sort-Object) -CaseSensitive).Count -ne 0 -or
    @(Compare-Object $expectedResultIds ($safeIds | Sort-Object) -CaseSensitive).Count -ne 0) {
    throw 'The raw/protected TRX result-ID set is not canonical.'
}

function Read-FinalTrxSummary([IO.FileInfo] $File) {
    [xml] $document = Get-Content -LiteralPath $File.FullName -Raw
    $counters = $document.TestRun.ResultSummary.Counters
    if ($null -eq $counters) { throw "TRX counters are absent from $($File.Name)." }
    $values = [ordered]@{}
    foreach ($name in @('total','executed','passed','failed','error','timeout','aborted','inconclusive','notExecuted','warning')) {
        $text = $counters.GetAttribute($name)
        $values[$name] = if ([string]::IsNullOrEmpty($text)) { 0 } else { [int]$text }
    }
    $values.Classes = @(
        $document.TestRun.TestDefinitions.UnitTest.TestMethod.className |
          Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
          Sort-Object -Unique
    )
    [pscustomobject] $values
}

$parsedResults = [ordered]@{}
$parsedDigests = [ordered]@{}
foreach ($resultId in $expectedResultIds) {
    $rawFile = @($rawTrx | Where-Object { $_.Directory.Name -ceq $resultId })[0]
    $safeFile = @($safeTrx | Where-Object { $_.BaseName -ceq $resultId })[0]
    $raw = Read-FinalTrxSummary $rawFile
    $safe = Read-FinalTrxSummary $safeFile
    if (($raw | ConvertTo-Json -Depth 4 -Compress) -cne ($safe | ConvertTo-Json -Depth 4 -Compress)) {
        throw "TRX protection changed counters or class identities for $resultId."
    }
    if ($safe.total -le 0 -or $safe.executed -ne $safe.total -or $safe.passed -ne $safe.total -or
        $safe.failed -ne 0 -or $safe.error -ne 0 -or $safe.timeout -ne 0 -or
        $safe.aborted -ne 0 -or $safe.inconclusive -ne 0 -or $safe.notExecuted -ne 0) {
        throw "TRX execution is incomplete or failed for $resultId."
    }
    $priorProject = @($priorProjects | Where-Object { $_.resultId -ceq $resultId })[0]
    $missingPriorClasses = @($priorProject.requiredClasses | Where-Object { $_ -notin $safe.Classes })
    if ($null -eq $priorProject -or $safe.total -lt [int]$priorProject.minimumExpectedTests -or
        $missingPriorClasses.Count -ne 0) {
        throw "Canonical evidence regressed the prior floor/classes for $resultId."
    }
    $parsedResults[$resultId] = $safe
    $parsedDigests[$resultId] = (Get-FileHash -LiteralPath $safeFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}

$summaryPath = Join-Path $safeResults 'reconciliation-summary.json'
$retainedSummary = @(Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json)
$currentSummary = @(
    $parsedResults.GetEnumerator() | ForEach-Object {
        [pscustomobject][ordered]@{ resultId=$_.Key; total=$_.Value.total; classes=@($_.Value.Classes) }
    }
)
if (($retainedSummary | ConvertTo-Json -Depth 5 -Compress) -cne
    ($currentSummary | ConvertTo-Json -Depth 5 -Compress)) {
    throw 'The privacy-safe reconciliation summary is stale.'
}

$registerDocument = Get-Content -LiteralPath tests/model-inspection-test-projects.json -Raw | ConvertFrom-Json
if ($registerDocument.evidenceState -cne 'ExactHead' -or
    $registerDocument.evidenceHead -cne $preClosureHead) {
    throw 'The final register is not bound to the exact locally tested head.'
}
$registeredProjects = if ($registerDocument.PSObject.Properties.Name -contains 'projects') {
    @($registerDocument.projects)
} else {
    throw 'The final register must use the projects/workflowRuns schema.'
}
$registeredRuns = @($registerDocument.workflowRuns)
$scopedRawRoot = Join-Path $rawResults 'scoped'
$scopedSafeRoot = Join-Path $safeResults 'scoped'
$scopedIndexPath = Join-Path $scopedSafeRoot 'scoped-run-index.json'
if (-not (Test-Path -LiteralPath $scopedRawRoot -PathType Container) -or
    -not (Test-Path -LiteralPath $scopedSafeRoot -PathType Container) -or
    -not (Test-Path -LiteralPath $scopedIndexPath -PathType Leaf)) {
    throw 'Fresh registered-run raw, protected, or index evidence is missing.'
}
$scopedRawTrx = @(Get-ChildItem -LiteralPath $scopedRawRoot -Filter '*.trx' -File -Recurse)
$scopedSafeTrx = @(Get-ChildItem -LiteralPath $scopedSafeRoot -Filter '*.trx' -File)
$registeredRunIds = @($registeredRuns.runId | Sort-Object)
$scopedRawIds = @($scopedRawTrx | ForEach-Object Directory | ForEach-Object Name | Sort-Object)
$scopedSafeIds = @($scopedSafeTrx | ForEach-Object BaseName | Sort-Object)
if ($registeredRuns.Count -eq 0 -or
    $scopedRawTrx.Count -ne $registeredRuns.Count -or
    $scopedSafeTrx.Count -ne $registeredRuns.Count -or
    @($registeredRunIds | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @($scopedRawIds | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @($scopedSafeIds | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @(Compare-Object $registeredRunIds $scopedRawIds -CaseSensitive).Count -ne 0 -or
    @(Compare-Object $registeredRunIds $scopedSafeIds -CaseSensitive).Count -ne 0) {
    throw 'Registered-run raw/protected TRX identities are missing, extra, or duplicate.'
}
$scopedIndex = @(Get-Content -LiteralPath $scopedIndexPath -Raw | ConvertFrom-Json)
if ($scopedIndex.Count -ne $registeredRuns.Count -or
    @($scopedIndex | Group-Object runId | Where-Object Count -ne 1).Count -ne 0) {
    throw 'The registered-run protected index is missing or duplicate.'
}
$scopedResults = [ordered]@{}
$scopedDigests = [ordered]@{}
foreach ($run in $registeredRuns) {
    $runId = [string]$run.runId
    $rawFile = @($scopedRawTrx | Where-Object { $_.Directory.Name -ceq $runId })[0]
    $safeFile = @($scopedSafeTrx | Where-Object { $_.BaseName -ceq $runId })[0]
    $indexRow = @($scopedIndex | Where-Object { $_.runId -ceq $runId })[0]
    $raw = Read-FinalTrxSummary $rawFile
    $safe = Read-FinalTrxSummary $safeFile
    $digest = (Get-FileHash -LiteralPath $safeFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if (($raw | ConvertTo-Json -Depth 4 -Compress) -cne ($safe | ConvertTo-Json -Depth 4 -Compress) -or
        $safe.total -le 0 -or $safe.executed -ne $safe.total -or $safe.passed -ne $safe.total -or
        $safe.failed -ne 0 -or $safe.error -ne 0 -or $safe.timeout -ne 0 -or
        $safe.aborted -ne 0 -or $safe.inconclusive -ne 0 -or $safe.notExecuted -ne 0) {
        throw "Registered-run TRX is changed, incomplete, skipped, or failed for $runId."
    }
    $priorRun = @($priorRuns | Where-Object { $_.runId -ceq $runId })[0]
    $missingPriorClasses = @($priorRun.requiredClasses | Where-Object { $_ -notin $safe.Classes })
    if ($null -eq $priorRun -or $safe.total -lt [int]$priorRun.minimumExpectedTests -or
        $missingPriorClasses.Count -ne 0) {
        throw "Registered-run evidence regressed the prior floor/classes for $runId."
    }
    $indexClasses = @($indexRow.classes | Sort-Object)
    $safeClasses = @($safe.Classes | Sort-Object)
    if ($indexRow.runId -cne $runId -or
        $indexRow.protectedTrxPattern -cne [string]$run.protectedTrxPattern -or
        $indexRow.testedHead -cne $preClosureHead -or
        [int]$indexRow.total -ne $safe.total -or
        ($indexClasses -join "`n") -cne ($safeClasses -join "`n") -or
        $indexRow.protectedTrxSha256 -cne $digest) {
        throw "The protected registered-run index is stale or mismatched for $runId."
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$run.scenarioScope)) {
        & scripts/model-inspection/Verify-WorkerProcessScenarioCoverage.ps1 `
          -ScenarioManifest tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json `
          -TrxPath $safeFile.FullName `
          -ExpectedScope ([string]$run.scenarioScope)
        if ($LASTEXITCODE -ne 0) { throw "Scenario coverage failed for registered run $runId`: $LASTEXITCODE" }
    }
    $scopedResults[$runId] = $safe
    $scopedDigests[$runId] = $digest
}
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $scopedSafeRoot
if ($LASTEXITCODE -ne 0) { throw "Registered-run protected evidence privacy scan failed: $LASTEXITCODE" }

$ledgerPath = 'docs/testing/evidence/2026-08-08-model-inspection-test-completeness-ledger.md'
if (-not (Test-Path -LiteralPath $ledgerPath -PathType Leaf)) {
    throw 'The retained test-completeness evidence ledger is missing.'
}
$ledgerRecords = @{}
foreach ($line in Get-Content -LiteralPath $ledgerPath -Encoding utf8) {
    if ($line -notmatch '^\|\s*<a id="MI-EV-\d{4}"></a>MI-EV-\d{4}\s*\|') { continue }
    $cells = @($line.Trim().Trim([char]'|').Split([char]'|') | ForEach-Object { $_.Trim() })
    if ($cells.Count -ne 16 -or
        $cells[0] -cnotmatch '^<a id="(?<anchor>MI-EV-\d{4})"></a>(?<display>MI-EV-\d{4})$' -or
        $Matches.anchor -cne $Matches.display) {
        throw "Malformed evidence-ledger row: $line"
    }
    $evidenceId = [string]$Matches.anchor
    if ($ledgerRecords.ContainsKey($evidenceId)) {
        throw "Duplicate evidence-ledger ID: $evidenceId"
    }
    [int]$total = 0
    [int]$executed = 0
    [int]$failed = 0
    [int]$skipped = 0
    if (-not [int]::TryParse($cells[8], [ref]$total) -or
        -not [int]::TryParse($cells[9], [ref]$executed) -or
        -not [int]::TryParse($cells[10], [ref]$failed) -or
        -not [int]::TryParse($cells[11], [ref]$skipped) -or
        $total -lt 0 -or $executed -lt 0 -or $failed -lt 0 -or $skipped -lt 0) {
        throw "Invalid evidence-ledger counters for $evidenceId."
    }
    $requiredClasses = if ($cells[12] -ceq '(none)') {
        @()
    } else {
        @($cells[12].Split([char]';') | ForEach-Object { $_.Trim() } |
          Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    $canonicalClasses = @($requiredClasses | Sort-Object -Unique)
    if (($requiredClasses -join "`n") -cne ($canonicalClasses -join "`n")) {
        throw "Evidence-ledger classes are not unique and ordinal for $evidenceId."
    }
    if ([string]::IsNullOrWhiteSpace($cells[1]) -or
        [string]::IsNullOrWhiteSpace($cells[2]) -or
        [string]::IsNullOrWhiteSpace($cells[3]) -or
        [string]::IsNullOrWhiteSpace($cells[4]) -or
        [string]::IsNullOrWhiteSpace($cells[5]) -or
        $cells[6] -cnotmatch '^[0-9a-f]{40}$' -or
        [string]::IsNullOrWhiteSpace($cells[7]) -or
        $cells[13] -cnotmatch '^[0-9a-f]{64}$' -or
        $cells[14] -cne 'passed' -or
        [string]::IsNullOrWhiteSpace($cells[15])) {
        throw "Evidence-ledger identity, digest, privacy, or traceability is invalid for $evidenceId."
    }
    $ledgerRecords[$evidenceId] = [pscustomobject]@{
        EvidenceId = $evidenceId
        Task = $cells[1]
        ResultId = $cells[2]
        Scope = $cells[3]
        RunId = $cells[4]
        ProtectedTrxPattern = $cells[5]
        Head = $cells[6]
        Command = $cells[7]
        Total = $total
        Executed = $executed
        Failed = $failed
        Skipped = $skipped
        RequiredClasses = @($requiredClasses)
        Digest = $cells[13]
        Privacy = $cells[14]
        MatrixRows = $cells[15]
    }
}
if ($ledgerRecords.Count -eq 0) { throw 'The evidence ledger contains no anchored records.' }

function Resolve-CountEvidenceRecord([string] $Reference) {
    $prefix = "$ledgerPath#"
    if ([string]::IsNullOrWhiteSpace($Reference) -or
        -not $Reference.StartsWith($prefix, [StringComparison]::Ordinal)) {
        throw "Count evidence must use the canonical anchored ledger reference: $Reference"
    }
    $evidenceId = $Reference.Substring($prefix.Length)
    if ($evidenceId -cnotmatch '^MI-EV-\d{4}$' -or -not $ledgerRecords.ContainsKey($evidenceId)) {
        throw "Count evidence selects a nonexistent ledger record: $Reference"
    }
    $ledgerRecords[$evidenceId]
}

function Assert-CountEvidenceRecord(
    [string] $Reference,
    [string] $ExpectedResultId,
    [string] $ExpectedScope,
    [string] $ExpectedRunId,
    [string] $ExpectedProtectedTrxPattern,
    [int] $ExpectedTotal,
    [object[]] $ExpectedClasses,
    [string] $ExpectedHead,
    [string] $ExpectedDigest) {
    $record = Resolve-CountEvidenceRecord $Reference
    $registeredClasses = @($ExpectedClasses | ForEach-Object { [string]$_ } | Sort-Object)
    $recordClasses = @($record.RequiredClasses | Sort-Object)
    if ($record.ResultId -cne $ExpectedResultId -or
        $record.Scope -cne $ExpectedScope -or
        $record.RunId -cne $ExpectedRunId -or
        $record.ProtectedTrxPattern -cne $ExpectedProtectedTrxPattern -or
        $record.Head -cne $ExpectedHead -or
        $record.Total -ne $ExpectedTotal -or
        $record.Executed -ne $record.Total -or
        $record.Failed -ne 0 -or $record.Skipped -ne 0 -or
        ($registeredClasses -join "`n") -cne ($recordClasses -join "`n") -or
        $ExpectedDigest -cnotmatch '^[0-9a-f]{64}$' -or
        $record.Digest -cne $ExpectedDigest) {
        throw "Count-evidence record $($record.EvidenceId) does not match $ExpectedResultId/$ExpectedScope/$ExpectedRunId."
    }
}

$canonicalTriples = @($canonicalProjects | ForEach-Object { "$($_.project)|$($_.resultId)|$($_.runner)" } | Sort-Object)
$actualTriples = @($registeredProjects | ForEach-Object { "$($_.project)|$($_.resultId)|$($_.runner)" } | Sort-Object)
$priorTriples = @($priorProjects | ForEach-Object { "$($_.project)|$($_.resultId)|$($_.runner)" } | Sort-Object)
if ($registeredProjects.Count -ne 9 -or
    @($actualTriples | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @(Compare-Object $canonicalTriples $actualTriples -CaseSensitive).Count -ne 0 -or
    @(Compare-Object $priorTriples $actualTriples -CaseSensitive).Count -ne 0) {
    throw 'The register does not contain the canonical project/result/runner triples.'
}
function Get-RunSemanticIdentity($Run) {
    [pscustomobject][ordered]@{
        runId = [string]$Run.runId
        projectResultId = [string]$Run.projectResultId
        workflow = [string]$Run.workflow
        step = [string]$Run.step
        scope = [string]$Run.scope
        protectedTrxPattern = [string]$Run.protectedTrxPattern
        scenarioScope = $Run.scenarioScope
        campaign = [string]$Run.campaign
        coverageProducer = [bool]$Run.coverageProducer
        protectedCoveragePattern = $Run.protectedCoveragePattern
        localInvocation = [pscustomobject][ordered]@{
            command = [string]$Run.localInvocation.command
            arguments = @($Run.localInvocation.arguments | ForEach-Object { [string]$_ })
        }
    } | ConvertTo-Json -Depth 6 -Compress
}
$priorRunSemantics = @($priorRuns | ForEach-Object { Get-RunSemanticIdentity $_ } | Sort-Object)
$currentRunSemantics = @($registeredRuns | ForEach-Object { Get-RunSemanticIdentity $_ } | Sort-Object)
if (@(Compare-Object $priorRunSemantics $currentRunSemantics -CaseSensitive).Count -ne 0) {
    throw 'A registered workflow run changed test semantics after exact-head execution; return to Step 2.'
}
$duplicateRunIds = @($registeredRuns | Group-Object runId | Where-Object Count -ne 1)
$duplicateArtifactKeys = @(
    $registeredRuns |
      ForEach-Object { "$($_.workflow)|$($_.protectedTrxPattern)" } |
      Group-Object |
      Where-Object Count -ne 1
)
$validRunScopes = @('Full','Focused','Main','Repeat','Crash','Native','Trusted')
$validScenarioScopes = @('Focused','Main','Repeat','Crash','Full')
$validCampaigns = @('ModelFree','Native','Trusted')
$invalidRuns = @(
    $registeredRuns |
      Where-Object {
          $runEvidenceReference = [string]$_.countEvidence
          $isProcessRun = [string]$_.projectResultId -ceq 'worker-process'
          $isNativeRun = [string]$_.projectResultId -ceq 'llama-native'
          $isTrustedRun = [string]$_.projectResultId -ceq 'llama-trusted'
          $scenarioScopeText = [string]$_.scenarioScope
          $coveragePatternText = [string]$_.protectedCoveragePattern
          [string]::IsNullOrWhiteSpace([string]$_.runId) -or
          $_.projectResultId -notin $expectedResultIds -or
          $_.scope -notin $validRunScopes -or
          $_.campaign -notin $validCampaigns -or
          ($isNativeRun -and ($_.scope -cne 'Native' -or $_.campaign -cne 'Native')) -or
          (-not $isNativeRun -and ($_.scope -ceq 'Native' -or $_.campaign -ceq 'Native')) -or
          ($isTrustedRun -and ($_.scope -cne 'Trusted' -or $_.campaign -cne 'Trusted')) -or
          (-not $isTrustedRun -and ($_.scope -ceq 'Trusted' -or $_.campaign -ceq 'Trusted')) -or
          (-not $isProcessRun -and $_.scope -in @('Main','Repeat','Crash')) -or
          ([bool]$_.coverageProducer -and
              ($_.scope -cne 'Full' -or [string]::IsNullOrWhiteSpace($coveragePatternText))) -or
          (-not [bool]$_.coverageProducer -and $null -ne $_.protectedCoveragePattern) -or
          ($isProcessRun -and
              ($_.scope -notin $validScenarioScopes -or
               [string]::IsNullOrWhiteSpace($scenarioScopeText) -or
               $scenarioScopeText -cne [string]$_.scope)) -or
          (-not $isProcessRun -and $null -ne $_.scenarioScope) -or
          [int]$_.minimumExpectedTests -le 0 -or
          [string]::IsNullOrWhiteSpace([string]$_.workflow) -or
          [string]::IsNullOrWhiteSpace([string]$_.step) -or
          [string]::IsNullOrWhiteSpace([string]$_.protectedTrxPattern) -or
          $null -eq $_.localInvocation -or
          [string]::IsNullOrWhiteSpace([string]$_.localInvocation.command) -or
          @($_.localInvocation.arguments).Count -eq 0 -or
          [string]::IsNullOrWhiteSpace($runEvidenceReference) -or
          -not $runEvidenceReference.StartsWith("$ledgerPath#", [StringComparison]::Ordinal)
      }
)
if ($registeredRuns.Count -lt 9 -or $duplicateRunIds.Count -ne 0 -or
    $duplicateArtifactKeys.Count -ne 0 -or $invalidRuns.Count -ne 0) {
    throw 'The emitted workflow-run registry is missing, duplicate, or invalid.'
}
foreach ($run in $registeredRuns) {
    $result = $scopedResults[[string]$run.runId]
    $priorRun = @($priorRuns | Where-Object { $_.runId -ceq [string]$run.runId })[0]
    $removedPriorClasses = @($priorRun.requiredClasses | Where-Object { $_ -notin @($run.requiredClasses) })
    if ([int]$run.minimumExpectedTests -ne $result.total -or
        [int]$run.minimumExpectedTests -lt [int]$priorRun.minimumExpectedTests -or
        $removedPriorClasses.Count -ne 0) {
        throw "The registered-run floor is stale for $($run.runId)."
    }
    $missingClasses = @($run.requiredClasses | Where-Object { $_ -notin $result.Classes })
    if ($missingClasses.Count -ne 0) {
        throw "Required classes are absent for $($run.runId): $($missingClasses -join ', ')."
    }
    Assert-CountEvidenceRecord `
      -Reference ([string]$run.countEvidence) `
      -ExpectedResultId ([string]$run.projectResultId) `
      -ExpectedScope ([string]$run.scope) `
      -ExpectedRunId ([string]$run.runId) `
      -ExpectedProtectedTrxPattern ([string]$run.protectedTrxPattern) `
      -ExpectedTotal ([int]$result.total) `
      -ExpectedClasses ([object[]]@($run.requiredClasses)) `
      -ExpectedHead $preClosureHead `
      -ExpectedDigest ([string]$scopedDigests[[string]$run.runId])
}
foreach ($canonical in $canonicalProjects) {
    $project = @($registeredProjects | Where-Object { $_.project -ceq $canonical.project })[0]
    $result = $parsedResults[$canonical.resultId]
    $priorProject = @($priorProjects | Where-Object { $_.resultId -ceq $canonical.resultId })[0]
    $removedPriorClasses = @($priorProject.requiredClasses | Where-Object { $_ -notin @($project.requiredClasses) })
    if ([int]$project.minimumExpectedTests -ne $result.total -or
        [int]$project.minimumExpectedTests -lt [int]$priorProject.minimumExpectedTests -or
        $removedPriorClasses.Count -ne 0) {
        throw "The floor is stale for $($canonical.resultId)."
    }
    $missingClasses = @($project.requiredClasses | Where-Object { $_ -notin $result.Classes })
    if ($missingClasses.Count -ne 0) {
        throw "Required classes are absent for $($canonical.resultId): $($missingClasses -join ', ')."
    }
    $evidenceScope = if ($canonical.resultId -ceq 'llama-native') {
        'Native'
    } elseif ($canonical.resultId -ceq 'llama-trusted') {
        'Trusted'
    } else {
        'Full'
    }
    Assert-CountEvidenceRecord `
      -Reference ([string]$project.countEvidence) `
      -ExpectedResultId ([string]$canonical.resultId) `
      -ExpectedScope $evidenceScope `
      -ExpectedRunId '(project)' `
      -ExpectedProtectedTrxPattern "$($canonical.resultId).trx" `
      -ExpectedTotal ([int]$result.total) `
      -ExpectedClasses ([object[]]@($project.requiredClasses)) `
      -ExpectedHead $preClosureHead `
      -ExpectedDigest ([string]$parsedDigests[$canonical.resultId])
}

& scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1
if ($LASTEXITCODE -ne 0) { throw "Cleanup inventory reconciliation failed: $LASTEXITCODE" }
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj `
  --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Final Contracts reconciliation failed: $LASTEXITCODE" }
dotnet build "IBM Granite with TurboQuant (Intel).slnx" `
  --configuration Release `
  -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Final solution build failed: $LASTEXITCODE" }
git diff --check
if ($LASTEXITCODE -ne 0) { throw "Final diff check failed: $LASTEXITCODE" }
```

Expected: the register, workflow steps/floors/filters, required classes, cleanup inventory, full Contracts suite, and all 17 solution projects agree after the last test addition.

- [ ] **Step 7: Commit the closure candidate**

```powershell
git add docs/testing `
  docs/reviews `
  docs/architecture/decisions/ADR-003-protected-model-inspection-worker.md `
  .github/workflows `
  tests/model-inspection-test-projects.json `
  tests/README.md `
  "IBM Granite with TurboQuant (Intel)/Features/README.md"
git commit -m "docs(model-inspection): publish test completeness closure candidate"
git status --short
```

Expected: clean worktree. The commit message and repository documents remain candidate/pending until hosted evidence is inspected.

- [ ] **Step 8: Verify the immutable candidate locally**

```powershell
$ErrorActionPreference = 'Stop'
$candidateHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve candidate head: $LASTEXITCODE" }
$candidateStatus = @(git status --short)
if ($LASTEXITCODE -ne 0) { throw "Unable to read candidate status: $LASTEXITCODE" }
if ($candidateStatus.Count -ne 0) { throw 'Candidate worktree is not clean.' }
dotnet test tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Immutable-candidate Contracts failed: $LASTEXITCODE" }
dotnet build "IBM Granite with TurboQuant (Intel).slnx" --configuration Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Immutable-candidate solution build failed: $LASTEXITCODE" }
& scripts/model-inspection/Verify-ModelInspectionCleanupInventory.ps1
if ($LASTEXITCODE -ne 0) { throw "Immutable-candidate inventory failed: $LASTEXITCODE" }
$verifiedHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw "Unable to re-read candidate head: $LASTEXITCODE" }
if ($verifiedHead -ne $candidateHead) { throw 'Candidate head changed during local verification.' }
$verifiedStatus = @(git status --short)
if ($LASTEXITCODE -ne 0 -or $verifiedStatus.Count -ne 0) {
    throw 'Candidate worktree changed during local verification.'
}
$candidateBranch = git branch --show-current
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($candidateBranch)) {
    throw 'Verified candidate branch is unavailable.'
}
$repositoryRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve repository root: $LASTEXITCODE" }
$exactRegister = Get-Content -LiteralPath tests/model-inspection-test-projects.json -Raw | ConvertFrom-Json
$evidenceHead = [string]$exactRegister.evidenceHead
if ($exactRegister.evidenceState -cne 'ExactHead' -or $evidenceHead -cnotmatch '^[0-9a-f]{40}$') {
    throw 'The immutable candidate register lacks an exact local evidence head.'
}
$parentLine = git rev-list --parents -n 1 $candidateHead
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve candidate parents: $LASTEXITCODE" }
$parentParts = @($parentLine -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($parentParts.Count -ne 2 -or
    $parentParts[0] -cne $candidateHead -or $parentParts[1] -cne $evidenceHead) {
    throw 'The closure candidate is not the single closure-only child of its locally tested evidence head.'
}
$locatorPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-final-$evidenceHead.locator.json")
if (-not (Test-Path -LiteralPath $locatorPath -PathType Leaf)) {
    throw 'The local exact-head result locator is unavailable for candidate-diff verification.'
}
$resultLocator = Get-Content -LiteralPath $locatorPath -Raw | ConvertFrom-Json
$candidateChanges = @(git diff --name-only --no-renames $evidenceHead $candidateHead --)
if ($LASTEXITCODE -ne 0) { throw "Unable to verify the closure-only candidate diff: $LASTEXITCODE" }
$unexpectedCandidateChanges = @(
    $candidateChanges | Where-Object { $_ -cnotin @($resultLocator.ClosureOnlyPaths) }
)
if ([string]$resultLocator.RepositoryRoot -cne $repositoryRoot -or
    [string]$resultLocator.TestedHead -cne $evidenceHead -or
    $unexpectedCandidateChanges.Count -ne 0) {
    throw "Candidate changed non-closure inputs: $($unexpectedCandidateChanges -join ', ')."
}
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $rootBytes = [Text.Encoding]::UTF8.GetBytes($repositoryRoot.ToLowerInvariant())
    $repositoryKey = ([BitConverter]::ToString($sha256.ComputeHash($rootBytes))).Replace('-','').ToLowerInvariant()
}
finally {
    $sha256.Dispose()
}
$candidateRecordPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-verified-candidate-$repositoryKey.json")
[ordered]@{
    RepositoryRoot = $repositoryRoot
    CandidateHead = $candidateHead
    EvidenceHead = $evidenceHead
    CandidateBranch = $candidateBranch
} | ConvertTo-Json | Set-Content -LiteralPath $candidateRecordPath -Encoding utf8
```

Any failure requires a new corrective commit and repetition from Step 2. Do not run hosted closure workflows for a locally failing candidate.

- [ ] **Step 9: Run hosted workflows on the immutable candidate commit**

Push the branch, snapshot the matching run IDs that already exist, dispatch every workflow explicitly against that branch, capture exactly one new run ID, and require every run's `headSha` to equal `$candidateHead`. Do not compare the service's second-granularity `createdAt` value with the local clock:

```powershell
$ErrorActionPreference = 'Stop'
$repositoryRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve repository root: $LASTEXITCODE" }
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $rootBytes = [Text.Encoding]::UTF8.GetBytes($repositoryRoot.ToLowerInvariant())
    $repositoryKey = ([BitConverter]::ToString($sha256.ComputeHash($rootBytes))).Replace('-','').ToLowerInvariant()
}
finally {
    $sha256.Dispose()
}
$candidateRecordPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-verified-candidate-$repositoryKey.json")
if (-not (Test-Path -LiteralPath $candidateRecordPath -PathType Leaf)) {
    throw 'The immutable-candidate local-verification record is unavailable.'
}
$candidateRecord = Get-Content -LiteralPath $candidateRecordPath -Raw | ConvertFrom-Json
$candidateHead = [string]$candidateRecord.CandidateHead
$evidenceHead = [string]$candidateRecord.EvidenceHead
$candidateBranch = [string]$candidateRecord.CandidateBranch
$currentHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $currentHead -cne $candidateHead) {
    throw 'HEAD differs from the locally verified immutable candidate.'
}
$currentBranch = git branch --show-current
if ($LASTEXITCODE -ne 0 -or $currentBranch -cne $candidateBranch) {
    throw 'Branch differs from the locally verified immutable candidate.'
}
$candidateStatus = @(git status --short)
if ($LASTEXITCODE -ne 0 -or $candidateStatus.Count -ne 0) {
    throw 'The locally verified immutable candidate is no longer clean.'
}
if ($repositoryRoot -cne [string]$candidateRecord.RepositoryRoot) {
    throw 'The immutable-candidate record belongs to a different repository.'
}
$exactRegister = Get-Content -LiteralPath tests/model-inspection-test-projects.json -Raw | ConvertFrom-Json
if ($exactRegister.evidenceState -cne 'ExactHead' -or
    [string]$exactRegister.evidenceHead -cne $evidenceHead) {
    throw 'The immutable candidate no longer matches its local evidence head.'
}
git push --set-upstream origin $candidateBranch
if ($LASTEXITCODE -ne 0) { throw "Candidate push failed: $LASTEXITCODE" }

$workflows = @(
  'build-and-test.yml'
  'model-inspection-transport-tests.yml'
  'model-inspection-worker-tests.yml'
  'model-inspection-worker-client-tests.yml'
  'model-inspection-worker-process-tests.yml'
  'llamasharp-feasibility-smoke.yml'
  'llamasharp-real-model-integration.yml'
)
$hostedRecordPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-hosted-runs-$candidateHead.json")
$runIds = [ordered]@{}
foreach ($workflow in $workflows) {
    $existingJson = gh run list `
      --workflow $workflow `
      --branch $candidateBranch `
      --commit $candidateHead `
      --event workflow_dispatch `
      --limit 100 `
      --json databaseId
    if ($LASTEXITCODE -ne 0) { throw "Unable to snapshot existing runs for $workflow." }
    $existingRunIds = @($existingJson | ConvertFrom-Json | ForEach-Object { [int64]$_.databaseId })

    gh workflow run $workflow --ref $candidateBranch
    if ($LASTEXITCODE -ne 0) { throw "Dispatch failed for $workflow." }

    $run = $null
    for ($poll = 0; $poll -lt 30 -and $null -eq $run; $poll++) {
        Start-Sleep -Seconds 2
        $listedJson = gh run list `
          --workflow $workflow `
          --branch $candidateBranch `
          --commit $candidateHead `
          --event workflow_dispatch `
          --limit 100 `
          --json databaseId,headSha,status,conclusion,url
        if ($LASTEXITCODE -ne 0) { throw "Unable to list dispatched runs for $workflow." }
        $newRuns = @(
            $listedJson |
              ConvertFrom-Json |
              Where-Object { [int64]$_.databaseId -notin $existingRunIds }
        )
        if ($newRuns.Count -gt 1) {
            throw "More than one new exact-head dispatch exists for $workflow; stop and disambiguate."
        }
        if ($newRuns.Count -eq 1) { $run = $newRuns[0] }
    }
    if ($null -eq $run -or $run.headSha -ne $candidateHead) {
        throw "No exact-head dispatch was found for $workflow."
    }
    $runIds[$workflow] = [int64]$run.databaseId
    $capturedRows = @(
        $runIds.GetEnumerator() | ForEach-Object {
            [pscustomobject][ordered]@{ Workflow=$_.Key; RunId=[int64]$_.Value }
        }
    )
    [ordered]@{
        RepositoryRoot = $repositoryRoot
        CandidateHead = $candidateHead
        EvidenceHead = $evidenceHead
        CandidateBranch = $candidateBranch
        VerificationState = 'captured-pending'
        Runs = $capturedRows
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $hostedRecordPath -Encoding utf8
}

$runRows = @(
    $runIds.GetEnumerator() | ForEach-Object {
        [pscustomobject][ordered]@{ Workflow=$_.Key; RunId=[int64]$_.Value }
    }
)
[ordered]@{
    RepositoryRoot = $repositoryRoot
    CandidateHead = $candidateHead
    EvidenceHead = $evidenceHead
    CandidateBranch = $candidateBranch
    VerificationState = 'captured-pending'
    Runs = $runRows
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $hostedRecordPath -Encoding utf8

$hostedFailures = [System.Collections.Generic.List[System.Exception]]::new()
$hostedResultRows = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $runIds.GetEnumerator()) {
    gh run watch $entry.Value --exit-status
    $watchFailed = $LASTEXITCODE -ne 0
    $resultJson = gh run view $entry.Value --json headSha,conclusion,url
    if ($LASTEXITCODE -ne 0) {
        $hostedResultRows.Add([pscustomobject][ordered]@{
            Workflow=$entry.Key
            RunId=[int64]$entry.Value
            HeadSha=$null
            Conclusion=$null
            Url=$null
            WatchSucceeded=(-not $watchFailed)
        })
        $hostedFailures.Add([InvalidOperationException]::new(
            "Hosted result could not be read for $($entry.Key)."))
        continue
    }
    $result = $resultJson | ConvertFrom-Json
    $hostedResultRows.Add([pscustomobject][ordered]@{
        Workflow=$entry.Key
        RunId=[int64]$entry.Value
        HeadSha=[string]$result.headSha
        Conclusion=[string]$result.conclusion
        Url=[string]$result.url
        WatchSucceeded=(-not $watchFailed)
    })
    if ($watchFailed -or $result.headSha -ne $candidateHead -or $result.conclusion -ne 'success') {
        $hostedFailures.Add([InvalidOperationException]::new(
            "Hosted result does not verify $candidateHead for $($entry.Key)."))
    }
}
$verificationState = if ($hostedFailures.Count -eq 0) { 'verified-success' } else { 'verified-failed' }
[ordered]@{
    RepositoryRoot = $repositoryRoot
    CandidateHead = $candidateHead
    EvidenceHead = $evidenceHead
    CandidateBranch = $candidateBranch
    VerificationState = $verificationState
    Runs = $hostedResultRows.ToArray()
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $hostedRecordPath -Encoding utf8
if ($hostedFailures.Count -eq 1) { throw $hostedFailures[0] }
if ($hostedFailures.Count -gt 1) {
    throw [AggregateException]::new('One or more hosted workflows failed.', $hostedFailures.ToArray())
}
```

Do not change repository files after these runs. This avoids creating a new, unverified head merely to write the run ID.

- [ ] **Step 10: Independently inspect retained artifacts**

Download every artifact into a unique directory outside the repository and query the Actions artifact API for the service-recorded name, size, and digest:

```powershell
$ErrorActionPreference = 'Stop'
$repositoryRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve repository root: $LASTEXITCODE" }
$candidateHead = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve candidate head: $LASTEXITCODE" }
$candidateBranch = git branch --show-current
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($candidateBranch)) {
    throw 'Candidate branch is unavailable.'
}
$candidateStatus = @(git status --short)
if ($LASTEXITCODE -ne 0 -or $candidateStatus.Count -ne 0) {
    throw 'Artifact inspection requires the unchanged clean candidate.'
}
$hostedRecordPath = Join-Path ([IO.Path]::GetTempPath()) ("mi-hosted-runs-$candidateHead.json")
if (-not (Test-Path -LiteralPath $hostedRecordPath -PathType Leaf)) {
    throw 'The hosted run-ID handoff record is unavailable.'
}
$hostedRecord = Get-Content -LiteralPath $hostedRecordPath -Raw | ConvertFrom-Json
$exactRegister = Get-Content -LiteralPath tests/model-inspection-test-projects.json -Raw | ConvertFrom-Json
if ([string]$hostedRecord.RepositoryRoot -cne $repositoryRoot -or
    [string]$hostedRecord.CandidateHead -cne $candidateHead -or
    [string]$hostedRecord.CandidateBranch -cne $candidateBranch -or
    $exactRegister.evidenceState -cne 'ExactHead' -or
    [string]$hostedRecord.EvidenceHead -cne [string]$exactRegister.evidenceHead) {
    throw 'The hosted run-ID handoff does not match this immutable candidate.'
}
if ([string]$hostedRecord.VerificationState -cne 'verified-success') {
    throw 'The hosted run-ID handoff is not marked verified-success.'
}
$expectedWorkflows = @(
  'build-and-test.yml'
  'model-inspection-transport-tests.yml'
  'model-inspection-worker-tests.yml'
  'model-inspection-worker-client-tests.yml'
  'model-inspection-worker-process-tests.yml'
  'llamasharp-feasibility-smoke.yml'
  'llamasharp-real-model-integration.yml'
)
$handoffWorkflows = @($hostedRecord.Runs.Workflow)
if (@($handoffWorkflows | Group-Object | Where-Object Count -ne 1).Count -ne 0 -or
    @(Compare-Object $expectedWorkflows ($handoffWorkflows | Sort-Object) -CaseSensitive).Count -ne 0) {
    throw 'The hosted handoff does not contain the exact seven workflow identities.'
}
$repository = gh repo view --json nameWithOwner --jq '.nameWithOwner'
if ($LASTEXITCODE -ne 0) { throw "Unable to resolve GitHub repository identity: $LASTEXITCODE" }
$jobIndexRows = [System.Collections.Generic.List[object]]::new()
$runIds = [ordered]@{}
foreach ($runRow in @($hostedRecord.Runs)) {
    if ($runIds.Contains([string]$runRow.Workflow)) {
        throw "Duplicate hosted workflow handoff: $($runRow.Workflow)."
    }
    if ([string]$runRow.HeadSha -cne $candidateHead -or
        [string]$runRow.Conclusion -cne 'success' -or
        $runRow.WatchSucceeded -ne $true) {
        throw "The retained hosted outcome is not successful for $($runRow.Workflow)."
    }
    $runApiJson = gh api "repos/$repository/actions/runs/$($runRow.RunId)"
    if ($LASTEXITCODE -ne 0) { throw "Unable to re-query hosted run $($runRow.RunId)." }
    $runApi = $runApiJson | ConvertFrom-Json
    $apiWorkflowPath = ([string]$runApi.path).Split('@')[0]
    $expectedWorkflowPath = ".github/workflows/$($runRow.Workflow)"
    if ([string]$runApi.head_sha -cne $candidateHead -or
        [string]$runApi.conclusion -cne 'success' -or
        [string]$runApi.event -cne 'workflow_dispatch' -or
        $apiWorkflowPath -cne $expectedWorkflowPath) {
        throw "GitHub no longer reports an exact-head successful dispatch for $($runRow.Workflow)."
    }
    $jobsJson = gh api "repos/$repository/actions/runs/$($runRow.RunId)/jobs?per_page=100"
    if ($LASTEXITCODE -ne 0) { throw "Unable to query jobs for $($runRow.Workflow)." }
    $jobs = $jobsJson | ConvertFrom-Json
    $jobRows = @($jobs.jobs)
    if ($jobs.total_count -le 0 -or $jobs.total_count -ne $jobRows.Count) {
        throw "Hosted job enumeration is empty or incomplete for $($runRow.Workflow)."
    }
    foreach ($job in $jobRows) {
        if ([int64]$job.id -le 0 -or [string]::IsNullOrWhiteSpace([string]$job.name) -or
            [string]$job.conclusion -cne 'success') {
            throw "A hosted job is missing identity or success for $($runRow.Workflow)."
        }
        $jobIndexRows.Add([pscustomobject][ordered]@{
            Workflow = [string]$runRow.Workflow
            RunId = [int64]$runRow.RunId
            JobId = [int64]$job.id
            JobName = [string]$job.name
            Conclusion = [string]$job.conclusion
        })
    }
    $runIds[[string]$runRow.Workflow] = [int64]$runRow.RunId
}
if ($runIds.Count -ne 7) { throw "Expected seven hosted run IDs, found $($runIds.Count)." }
$artifactRoot = Join-Path ([IO.Path]::GetTempPath()) ('mi-hosted-artifacts-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $artifactRoot | Out-Null
$artifactIndexRows = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $runIds.GetEnumerator()) {
    $metadataJson = gh api "repos/$repository/actions/runs/$($entry.Value)/artifacts?per_page=100"
    if ($LASTEXITCODE -ne 0) { throw "Artifact metadata lookup failed for $($entry.Key)." }
    $metadata = $metadataJson | ConvertFrom-Json
    $workflowText = Get-Content (Join-Path '.github/workflows' $entry.Key) -Raw
    $requiresArtifact = $workflowText -match 'actions/upload-artifact@'
    if ($requiresArtifact -and $metadata.total_count -lt 1) {
        throw "No artifact was retained for evidence-producing workflow $($entry.Key)."
    }
    foreach ($artifact in $metadata.artifacts) {
        if ($artifact.expired -or $artifact.size_in_bytes -le 0 -or [string]::IsNullOrWhiteSpace($artifact.digest)) {
            throw "Artifact metadata is incomplete for $($artifact.name)."
        }
        $destination = Join-Path $artifactRoot ("$($entry.Value)-$($artifact.id)")
        gh run download $entry.Value --name $artifact.name --dir $destination
        if ($LASTEXITCODE -ne 0) { throw "Artifact download failed for $($artifact.name)." }
        $artifactIndexRows.Add([pscustomobject][ordered]@{
            Workflow = $entry.Key
            RunId = [int64]$entry.Value
            ArtifactId = [int64]$artifact.id
            ArtifactName = [string]$artifact.name
            ServiceSize = [int64]$artifact.size_in_bytes
            ServiceDigest = [string]$artifact.digest
            RelativeDirectory = Split-Path $destination -Leaf
        })
    }
}
$artifactIndexPath = Join-Path $artifactRoot 'artifact-index.json'
[ordered]@{
    CandidateHead = $candidateHead
    Jobs = $jobIndexRows.ToArray()
    Artifacts = $artifactIndexRows.ToArray()
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $artifactIndexPath -Encoding utf8
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $artifactRoot
if ($LASTEXITCODE -ne 0) { throw "Downloaded artifact privacy scan failed: $LASTEXITCODE" }
$outputSummaryPath = Join-Path $artifactRoot 'hosted-reconciliation-summary.json'
& scripts/model-inspection/Verify-ModelInspectionHostedArtifacts.ps1 `
  -ArtifactRoot $artifactRoot `
  -ArtifactIndexPath $artifactIndexPath `
  -RunHandoffPath $hostedRecordPath `
  -RegisterPath tests/model-inspection-test-projects.json `
  -CoverageLedgerPath docs/testing/evidence/2026-08-08-model-inspection-coverage-ledger.json `
  -ScenarioManifestPath tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/worker-process-scenarios.json `
  -OutputSummaryPath $outputSummaryPath
if ($LASTEXITCODE -ne 0) { throw "Hosted artifact reconciliation failed: $LASTEXITCODE" }
$hostedSummary = Get-Content -LiteralPath $outputSummaryPath -Raw | ConvertFrom-Json
if ([string]$hostedSummary.VerificationState -cne 'verified-success' -or
    [string]$hostedSummary.CandidateHead -cne $candidateHead -or
    [string]$hostedSummary.EvidenceHead -cne [string]$exactRegister.evidenceHead) {
    throw 'Hosted artifact summary is not a successful exact-candidate attestation.'
}
& scripts/model-inspection/Test-ModelInspectionArtifactPrivacy.ps1 -Path $artifactRoot
if ($LASTEXITCODE -ne 0) { throw "Hosted reconciliation summary privacy scan failed: $LASTEXITCODE" }
```

Read `hosted-reconciliation-summary.json` and use it as the sole artifact-content input to the external attestation. It must show every downloaded TRX and protected Cobertura file matched, every scoped rule passed, and the sorted local manifest/directory digest. Record the independently re-queried run/job/artifact IDs, service sizes/digests, local digest, scoped counts, privacy/integrity/no-port/orphan outcomes in the draft PR body against `$candidateHead`; do not commit this post-run attestation or quote raw artifact content.

- [ ] **Step 11: Declare the gate result truthfully**

The test-completeness gate (Order 1) closes only if:

```text
all current implemented/partial rows have correct-layer evidence
all named mutations are killed
all nine test projects are discoverable and floor-protected
all required local and hosted runs are green
all uploads are scan+execution gated
all accessibility/manual checks pass
controlled model integrity and privacy pass
zero relevant processes remain
final commit equals every hosted run head
repository matrix/ledger/closure consistently say candidate and hosted-pending
register and workflows agree with parsed exact-head counts
the draft PR contains the immutable hosted verification attestation
```

If any condition fails, keep the row and gate open. On success, update only the draft PR/external programme record to state that the test-completeness gate (Order 1) is verified at `$candidateHead`; the committed matrix retains `MI-TC-056` as hosted-pending because editing it would create a new, unverified head. Then begin cleanup Phase 2 from the roadmap; do not skip directly to production Gate 3.
