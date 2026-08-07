# Workbook 05 Phase 2 Documented-Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build independently validated evidence contracts and controlled Windows workflows for the exact Route A Runtime/GenAI pair, then either build the accepted Route B candidate or close it transparently as blocked.

**Architecture:** Repository code defines schemas, validators and PowerShell orchestration. Large OpenVINO source/build/install trees stay under route-separated short paths on the Intel runner. Each self-hosted build stage uploads text-only evidence to an independent hosted validator; no model or binary payload enters Git or an artifact.

**Tech Stack:** Python 3.12.10 standard library, Windows PowerShell 5.1, repository JSON-schema validation conventions, GitHub Actions, Git, CMake 4.3.1-msvc1, Visual Studio 17 2022, MSVC x64, OpenVINO Runtime and OpenVINO GenAI.

## Global constraints

- Campaign ID: `GTQ-WB05-MF-v1`.
- Route A Runtime: `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Route A GenAI: `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- Route B Runtime: `EgorDuplensky/openvino@1827f6458d049de11c1a8203c793af67c99935dc` plus the reviewed one-file PR #49 repair.
- Route B stays disabled unless BR8 is independently validated as `ExecutableCandidate` and accepted by the project owner.
- Generator: `Visual Studio 17 2022`; platform: `x64`; configuration: `Release`.
- Python: `C:\Program Files\Python312\python.exe`, version `3.12.10`.
- CMake: `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe`.
- Parallelism: `2`; self-hosted job timeout: `360` minutes.
- Route A external root: `C:\w5a`; Route B external root: `C:\w5b`.
- Existing run directories are rejected; they are never reused or silently cleaned.
- Evidence permits text, JSON, CSV, Markdown and SHA-256 manifests only.
- EXE, DLL, LIB, PDB, wheel, archive, source-tree, build-tree, install-tree and model payloads are prohibited.
- No task authorises Granite execution, codec-activation claims, packed-storage claims, performance claims or quality claims.
- Every behaviour change follows red, green, refactor and ends in a focused commit.

---

## File map

### Schemas and template

```text
experiments/granite_turboquant_intel/schemas/workbook05/build-command-record.schema.json
experiments/granite_turboquant_intel/schemas/workbook05/build-deviation-record.schema.json
experiments/granite_turboquant_intel/schemas/workbook05/build-dependency-record.schema.json
experiments/granite_turboquant_intel/schemas/workbook05/build-resource-summary.schema.json
experiments/granite_turboquant_intel/schemas/workbook05/build-binary-record.schema.json
experiments/granite_turboquant_intel/schemas/workbook05/build-compatibility-attempt.schema.json
experiments/granite_turboquant_intel/schemas/workbook05/build-decision.schema.json
experiments/granite_turboquant_intel/manifests/templates/workbook05/build-stage-template.json
```

### Python and PowerShell implementation

```text
scripts/testing/workbook05/build_contracts.py
scripts/testing/workbook05/build_bundle_validation.py
scripts/testing/workbook05/Workbook05.Build.psm1
scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1
scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1
scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1
scripts/testing/Validate-Workbook05-BuildStage.ps1
.github/workflows/workbook-05-documented-build.yml
```

### Tests

```text
tests/testing/workbook05/test_build_contracts.py
tests/testing/workbook05/test_build_bundle_validation.py
tests/testing/workbook05/test_build_powershell_contract.py
tests/testing/workbook05/test_build_workflow_contract.py
```

---

### Task 1: Define strict build evidence contracts

**Files:**
- Create the seven schema files and template listed above.
- Create: `scripts/testing/workbook05/build_contracts.py`.
- Test: `tests/testing/workbook05/test_build_contracts.py`.

**Interfaces:**

```python
load_build_schemas(repository_root: Path) -> dict[str, dict]
validate_build_record(record_type: str, payload: object, repository_root: Path) -> list[str]
validate_build_stage_template(payload: object, repository_root: Path) -> list[str]
```

- [ ] **Step 1: Write the failing tests**

The test module must build valid fixtures for all seven record types and assert these behaviours:

```python
class BuildContractTests(unittest.TestCase):
    def test_valid_build_template_passes(self):
        self.assertEqual([], validate_build_stage_template(valid_template(), REPOSITORY_ROOT))

    def test_command_record_rejects_shell_string(self):
        record = valid_command_record()
        record["arguments"] = "--build C:/w5a/run/b-ov"
        errors = validate_build_record("command", record, REPOSITORY_ROOT)
        self.assertIn("arguments must be an array", "\n".join(errors))

    def test_decision_cannot_authorise_later_claims(self):
        record = valid_decision_record()
        record["granite_model_test_authorised"] = True
        errors = validate_build_record("decision", record, REPOSITORY_ROOT)
        self.assertIn("must be false", "\n".join(errors))

    def test_executed_deviation_must_be_approved(self):
        record = valid_deviation_record()
        record["approval_status"] = "Proposed"
        record["executed"] = True
        errors = validate_build_record("deviation", record, REPOSITORY_ROOT)
        self.assertIn("unapproved deviation", "\n".join(errors))

    def test_evidence_paths_reject_parent_traversal(self):
        record = valid_command_record()
        record["stdout_path"] = "../outside.log"
        errors = validate_build_record("command", record, REPOSITORY_ROOT)
        self.assertIn("unsafe evidence path", "\n".join(errors))
```

- [ ] **Step 2: Run and verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_contracts
```

Expected: import failure because `scripts.testing.workbook05.build_contracts` does not exist.

- [ ] **Step 3: Implement schemas and validator**

Schemas use Draft 2020-12 identifiers and `additionalProperties: false`. Required decision flags are all `const: false`:

```json
{
  "granite_model_test_authorised": false,
  "activation_claim_authorised": false,
  "packed_storage_claim_authorised": false,
  "performance_claim_authorised": false,
  "quality_claim_authorised": false
}
```

Allowed component statuses:

```python
{"Passed", "Failed", "Blocked", "Infrastructure interrupted", "Not applicable"}
```

The Python validator follows existing Workbook 05 conventions and adds semantic checks that JSON Schema alone cannot express:

- arguments must be a list of strings;
- evidence paths must be relative, slash-normalised and free of `..`, drive, UNC and NUL components;
- executed deviations must be `Approved`;
- binary records must set `copied_to_artifact` to `False`;
- a `BuildCandidate` route decision requires every required component to be `Passed`;
- every later-stage authorisation flag must remain false.

- [ ] **Step 4: Run focused and full Workbook 05 Python tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_contracts

& 'C:\Program Files\Python312\python.exe' -m unittest discover -v `
  -s tests/testing/workbook05 -p 'test_*.py'
```

Expected: zero failures.

- [ ] **Step 5: Commit**

```bash
git add experiments/granite_turboquant_intel/schemas/workbook05 \
        experiments/granite_turboquant_intel/manifests/templates/workbook05/build-stage-template.json \
        scripts/testing/workbook05/build_contracts.py \
        tests/testing/workbook05/test_build_contracts.py
git commit -m "feat(workbook-05): define documented-build evidence contracts"
```

---

### Task 2: Validate build artifacts as untrusted text-only bundles

**Files:**
- Create: `scripts/testing/workbook05/build_bundle_validation.py`.
- Create: `tests/testing/workbook05/test_build_bundle_validation.py`.

**Interfaces:**

```python
@dataclass(frozen=True)
class ExpectedBuildBundle:
    route_id: str
    component: str
    source_commit: str
    run_id: str
    run_attempt: int

@dataclass(frozen=True)
class ValidationIssue:
    code: str
    message: str
    path: str | None = None

validate_build_bundle(bundle_root: Path, expected: ExpectedBuildBundle) -> list[ValidationIssue]
```

- [ ] **Step 1: Write failing valid/adversarial fixture tests**

Reject manifest changes, unsafe paths, missing logs, wrong source identity, secret-like strings, upper-case prohibited suffixes, shell-string arguments, executed unapproved deviations, `copied_to_artifact=true`, false route admission and any true claim-authorisation flag.

- [ ] **Step 2: Verify RED**

Run the focused module and confirm the missing-validator import failure.

- [ ] **Step 3: Implement read-only validation**

The validator may read, parse, hash and report. It may not import, execute, dynamically load or shell evidence files. It always writes a Markdown report and returns exit code zero only when no issue exists.

- [ ] **Step 4: Verify valid and adversarial bundles**

Run the focused module and full Workbook 05 Python suite. Every adversarial fixture must fail for its named reason.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/build_bundle_validation.py \
        tests/testing/workbook05/test_build_bundle_validation.py
git commit -m "feat(workbook-05): validate documented-build bundles as untrusted data"
```

---

### Task 3: Extract a reusable Windows build-execution module

**Files:**
- Create: `scripts/testing/workbook05/Workbook05.Build.psm1`.
- Create: `tests/testing/workbook05/test_build_powershell_contract.py`.

**Exports:**

```text
New-Wb05ExternalWorkspace
Invoke-Wb05LoggedProcess
Start-Wb05ResourceSampler
Stop-Wb05ResourceSampler
Write-Wb05Json
Write-Wb05Manifest
Assert-Wb05SafePath
Get-Wb05BinaryRecords
Restore-Wb05Environment
```

- [ ] **Step 1: Write failing static and behavioural tests**

Require `ProcessStartInfo`, concurrent stdout/stderr drains, argument arrays, reparse-point rejection, existing-run rejection, two-second sampling, memory/commit/heartbeat stops and environment restoration. Forbid `Invoke-Expression`, machine-wide Git configuration, registry edits, `git reset --hard`, `git clean` and recursive deletion of `C:\w5a` or `C:\w5b`.

- [ ] **Step 2: Verify RED**

Run `tests.testing.workbook05.test_build_powershell_contract` and confirm the missing module failure.

- [ ] **Step 3: Implement the module**

Reuse the already proven Windows argument quoting and asynchronous stream-draining behaviour from `Invoke-Workbook05RouteBRepair.ps1`, but place each responsibility in one exported function. Add beginner-readable comments to every logical block.

- [ ] **Step 4: Import smoke test and regression**

```powershell
Import-Module '.\scripts\testing\workbook05\Workbook05.Build.psm1' -Force
Get-Command -Module Workbook05.Build
```

Expected: all nine exports exist and all Workbook 05 tests pass.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/Workbook05.Build.psm1 \
        tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "feat(workbook-05): add controlled Windows build execution module"
```

---

### Task 4: Implement Route A Runtime orchestration

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`.
- Extend: `tests/testing/workbook05/test_build_powershell_contract.py`.

**Exact paths per run identity:**

```text
C:\w5a\<run-identity>\ov
C:\w5a\<run-identity>\b-ov
C:\w5a\<run-identity>\i-ov
```

- [ ] **Step 1: Write failing exact-command tests**

Require exact repository/commit, detached fetch, recursive submodules and these configure values:

```text
-G Visual Studio 17 2022
-A x64
-DCMAKE_BUILD_TYPE=Release
-DENABLE_INTEL_GPU=OFF
-DENABLE_INTEL_NPU=OFF
-DENABLE_TESTS=OFF
-DENABLE_FUNCTIONAL_TESTS=OFF
-DENABLE_SAMPLES=ON
-DENABLE_PYTHON=ON
-DENABLE_WHEEL=OFF
-DPython3_EXECUTABLE=C:\Program Files\Python312\python.exe
```

- [ ] **Step 2: Verify RED**

Confirm the missing orchestrator failure.

- [ ] **Step 3: Implement source acquisition, configure, build and install**

Follow the exact approved order: init, repository-scoped long paths, remote, exact fetch, detached checkout, recursive submodules, configure, Release build with parallelism 2, Release install. Capture every command, resource sample, warning, dependency and in-place binary hash.

- [ ] **Step 4: Simulate decision paths**

Injected fake process results must prove `Passed`, `Failed`, `Blocked` and `Infrastructure interrupted` without compiling OpenVINO during unit tests.

- [ ] **Step 5: Verify and commit**

Run focused/full tests, then commit as:

```text
feat(workbook-05): orchestrate exact Route A Runtime build
```

---

### Task 5: Implement source-matched Route A GenAI orchestration

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1`.
- Extend: `tests/testing/workbook05/test_build_powershell_contract.py`.

- [ ] **Step 1: Write failing prerequisite and command tests**

Require a passed Runtime decision, exact Runtime source commit, exactly one installed `OpenVINOConfig.cmake`, exact GenAI commit, separate GenAI paths, `ENABLE_PYTHON=ON`, `ENABLE_JS=OFF` and no archive path.

- [ ] **Step 2: Verify RED**

Confirm the missing orchestrator failure.

- [ ] **Step 3: Implement configure/build/install**

Configure with the exact installed Runtime CMake package, build Release with parallelism 2 and install separately. Record a retained compatibility attempt only when source identity and all commands pass.

- [ ] **Step 4: Verify environment restoration**

Prove `PATH`, `PYTHONPATH`, `OPENVINO_LIB_PATHS` and `OpenVINO_DIR` are restored in success and failure cases.

- [ ] **Step 5: Verify and commit**

```text
feat(workbook-05): orchestrate source-matched Route A GenAI build
```

---

### Task 6: Implement fail-closed Route B build gating

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1`.
- Extend: `tests/testing/workbook05/test_build_powershell_contract.py`.
- Reuse: `scripts/testing/workbook05/route_b_repair.py`.

- [ ] **Step 1: Write failing prerequisite tests**

Reject before Git or CMake when BR8 is not `ExecutableCandidate`, its manifest/digest/source differs, owner acceptance is false, algorithm files changed, or any required six-case result is absent/non-passing.

- [ ] **Step 2: Verify RED**

Confirm the missing orchestrator failure.

- [ ] **Step 3: Implement conditional execution**

After prerequisite acceptance only: acquire exact source, apply the one-file repair, prove the changed-file set, configure tests/per-target exposure, build/re-run the narrow six-case gate, then permit full Runtime build/install. Treat GenAI as a separate compatibility attempt.

- [ ] **Step 4: Prove independent route decisions**

A fixture with Route A `Passed` and Route B `Not applicable` must retain Route A as a build candidate.

- [ ] **Step 5: Verify and commit**

```text
feat(workbook-05): gate conditional Route B documented builds
```

---

### Task 7: Add the staged workflow and repository gate

**Files:**
- Create: `.github/workflows/workbook-05-documented-build.yml`.
- Create: `scripts/testing/Validate-Workbook05-BuildStage.ps1`.
- Create: `tests/testing/workbook05/test_build_workflow_contract.py`.

- [ ] **Step 1: Write failing workflow security tests**

Require manual dispatch, exact stages, `contents: read`, `actions: read`, pinned Action SHAs, `persist-credentials: false`, exact Intel labels, 360-minute self-hosted timeout, same-repository restrictions, `cancel-in-progress: false` and independent `windows-latest` validation. Forbid `pull_request_target`, write permissions, model URLs/execution, binary upload patterns and merge steps.

- [ ] **Step 2: Verify RED**

Confirm missing workflow/gate failure.

- [ ] **Step 3: Implement the repository gate**

Run all Workbook 05 Python tests, PowerShell tests, schema/template tests, workflow contract, forbidden-command scan and `git diff --check`. Print `WORKBOOK05_BUILD_STAGE_GATE_PASS` only at the end.

- [ ] **Step 4: Implement staged jobs**

Manual stage values:

```text
route-a-runtime
route-a-genai
route-b-runtime
route-b-genai
```

Each self-hosted job runs the gate before collection. Each hosted job downloads the exact run-ID/attempt artifact and invokes the untrusted validator. Upload text evidence with `if: always()` so scientific failures remain reviewable.

- [ ] **Step 5: Verify and commit**

```text
ci(workbook-05): add independently validated documented-build workflow
```

**Checkpoint B2:** synthetic valid/adversarial bundles behave correctly, all security tests pass and no model execution is possible.

---

### Task 8: Static closure and live build execution

**Files:**
- Update the Phase 2 review checklist and PR evidence only.

- [ ] **Step 1: Run the exact-head static gate and WinUI regression**

Record head SHA and exact test counts.

- [ ] **Step 2: Review the PR boundary**

Confirm no application source, OpenVINO source, binary, model, archive or secret entered Git. Resolve or record every review thread.

- [ ] **Step 3: Dispatch and validate Route A Runtime**

Inspect exact source/submodules, configure cache, build/install commands, warnings, resources, dependencies, binary hashes and hosted artifact validation.

- [ ] **Step 4: Dispatch and validate Route A GenAI**

Verify it resolved against the exact Route A Runtime install and record the compatibility result.

- [ ] **Step 5: Decide Route B**

Use the final BR8 artifact only. When accepted, run the conditional Route B workflow. Otherwise populate dependent rows as `Blocked`/`Not applicable` with exact evidence.

- [ ] **Step 6: Record final build decisions**

Route A and Route B must each be either executable with validated build evidence or formally blocked. No ambiguous middle state is allowed.

**Checkpoint B3:** Route A Runtime/GenAI build, install and diagnostic evidence pass without an activation claim.

**Checkpoint B4:** Route B is executable with evidence or formally closed as blocked. Granite remains disabled until activation, packed-storage and no-fallback gates.

---

## Plan self-review

- **Spec coverage:** source locks, commands, paths, deviations, resource controls, records, hosted validation and Route B gating are mapped to Tasks 1-8.
- **Placeholder scan:** there are no `TBD`, `TODO`, `implement later` or unspecified validation actions. Angle-bracket notation is used only for documented runtime values such as a run identity, not for missing design decisions.
- **Type consistency:** Python function names, PowerShell exports, route IDs, component names and decision states are consistent across tasks.
- **Scope:** model acquisition, formal inference, activation proof, packed-storage proof, performance and quality measurement remain excluded.

## Execution mode

Use inline execution with `superpowers:executing-plans`. Execute one task at a time and stop at every failed verification or scientific blocker.
