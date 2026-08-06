# Workbook 05 Phase 2 Documented-Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build independently validated evidence contracts and controlled Windows workflows for the exact Route A Runtime/GenAI pair, then either build the accepted Route B candidate or close it transparently as blocked.

**Architecture:** Repository code defines schemas, validators and PowerShell orchestration, while large OpenVINO source/build/install trees remain under route-separated short paths on the Intel runner. Every self-hosted stage uploads text-only evidence to an independent hosted validator; no model or binary payload enters the repository or artifact.

**Tech Stack:** Python 3.12.10 standard library, Windows PowerShell 5.1, JSON Schema Draft 2020-12, GitHub Actions, Git, CMake 4.3.1-msvc1, Visual Studio 17 2022 generator, MSVC x64, OpenVINO Runtime and OpenVINO GenAI.

## Global Constraints

- Campaign ID is exactly `GTQ-WB05-MF-v1`.
- Route A Runtime is exactly `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Route A GenAI is exactly `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- Route B Runtime is exactly `EgorDuplensky/openvino@1827f6458d049de11c1a8203c793af67c99935dc` plus the reviewed one-file PR #49 repair.
- Route B is disabled unless BR8 is independently validated as `ExecutableCandidate` and accepted by the project owner.
- Windows generator is exactly `Visual Studio 17 2022`, platform `x64`, configuration `Release`.
- Python is exactly `C:\Program Files\Python312\python.exe`, version `3.12.10`.
- CMake is exactly `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe`.
- Build parallelism is `2`; each self-hosted job timeout is `360` minutes.
- Route A uses `C:\w5a`; Route B uses `C:\w5b`; existing run directories are never reused.
- Evidence artifacts contain text, JSON, CSV, Markdown and SHA-256 manifests only.
- Executables, libraries, archives, wheels, source trees, build trees, installations and models are prohibited from artifacts and Git.
- No task authorises Granite execution, codec activation claims, packed-storage claims, performance claims or quality claims.
- Every behaviour change follows red, green, refactor and ends in a focused commit.

---

## File map

### New schema and template files

```text
experiments/granite_turboquant_intel/manifests/schemas/workbook05/build-command-record.schema.json
experiments/granite_turboquant_intel/manifests/schemas/workbook05/build-deviation-record.schema.json
experiments/granite_turboquant_intel/manifests/schemas/workbook05/build-dependency-record.schema.json
experiments/granite_turboquant_intel/manifests/schemas/workbook05/build-resource-summary.schema.json
experiments/granite_turboquant_intel/manifests/schemas/workbook05/build-binary-record.schema.json
experiments/granite_turboquant_intel/manifests/schemas/workbook05/build-compatibility-attempt.schema.json
experiments/granite_turboquant_intel/manifests/schemas/workbook05/build-decision.schema.json
experiments/granite_turboquant_intel/manifests/templates/workbook05/build-stage-template.json
```

### New implementation files

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

### New tests

```text
tests/testing/workbook05/test_build_contracts.py
tests/testing/workbook05/test_build_bundle_validation.py
tests/testing/workbook05/test_build_powershell_contract.py
tests/testing/workbook05/test_build_workflow_contract.py
```

### Planning and review records

```text
docs/superpowers/specs/2026-08-06-workbook-05-phase-2-documented-build-design.md
docs/superpowers/plans/2026-08-06-workbook-05-phase-2-documented-build-implementation-plan.md
docs/superpowers/plans/2026-08-06-workbook-05-phase-2-documented-build-review-checklist.md
```

---

### Task 1: Define strict build evidence schemas

**Files:**
- Create the seven schema files and `build-stage-template.json` listed above.
- Create: `scripts/testing/workbook05/build_contracts.py`
- Test: `tests/testing/workbook05/test_build_contracts.py`

**Interfaces:**
- Produces: `load_build_schemas(repository_root: Path) -> dict[str, dict]`
- Produces: `validate_build_record(record_type: str, payload: object, repository_root: Path) -> list[str]`
- Produces: `validate_build_stage_template(payload: object, repository_root: Path) -> list[str]`
- Consumes: repository-root paths and JSON-compatible objects only.

- [ ] **Step 1: Write failing schema tests**

Create tests that require:

```python
class BuildContractTests(unittest.TestCase):
    def test_valid_build_template_passes(self):
        errors = validate_build_stage_template(valid_template(), REPOSITORY_ROOT)
        self.assertEqual([], errors)

    def test_command_record_rejects_shell_string(self):
        payload = valid_command_record()
        payload["arguments"] = "--build C:/w5a/b-ov"
        errors = validate_build_record("command", payload, REPOSITORY_ROOT)
        self.assertIn("arguments must be an array", "\n".join(errors))

    def test_decision_cannot_authorise_model_or_claims(self):
        payload = valid_decision()
        payload["granite_model_test_authorised"] = True
        errors = validate_build_record("decision", payload, REPOSITORY_ROOT)
        self.assertIn("must be false", "\n".join(errors))

    def test_deviation_must_be_approved_before_execution(self):
        payload = valid_deviation()
        payload["approval_status"] = "Proposed"
        payload["executed"] = True
        errors = validate_build_record("deviation", payload, REPOSITORY_ROOT)
        self.assertIn("unapproved deviation", "\n".join(errors))
```

- [ ] **Step 2: Run the focused tests and verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_contracts
```

Expected: import failure for `scripts.testing.workbook05.build_contracts`.

- [ ] **Step 3: Implement the schemas and validator**

Use JSON Schema Draft 2020-12 and fail closed on unknown properties. Required decision flags:

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

Safe evidence paths must be relative, slash-normalised, contain no `..`, drive prefix, UNC prefix or NUL.

- [ ] **Step 4: Run focused and full Workbook 05 Python tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_contracts

& 'C:\Program Files\Python312\python.exe' -m unittest discover -v `
  -s tests/testing/workbook05 -p 'test_*.py'
```

Expected: all tests pass with zero failures.

- [ ] **Step 5: Commit**

```bash
git add experiments/granite_turboquant_intel/manifests/schemas/workbook05 \
        experiments/granite_turboquant_intel/manifests/templates/workbook05/build-stage-template.json \
        scripts/testing/workbook05/build_contracts.py \
        tests/testing/workbook05/test_build_contracts.py
git commit -m "feat(workbook-05): define documented-build evidence contracts"
```

---

### Task 2: Implement adversarial text-only bundle validation

**Files:**
- Create: `scripts/testing/workbook05/build_bundle_validation.py`
- Create: `tests/testing/workbook05/test_build_bundle_validation.py`

**Interfaces:**
- Produces: `validate_build_bundle(bundle_root: Path, expected: ExpectedBuildBundle) -> list[ValidationIssue]`
- Produces CLI: `python -m scripts.testing.workbook05.build_bundle_validation --bundle <path> --route-id <id> --component <runtime|genai> --run-id <id> --run-attempt <n> --report <path>`
- Consumes the schemas from Task 1.

- [ ] **Step 1: Write adversarial fixture tests**

Require rejection of:

```python
FORBIDDEN_SUFFIXES = {
    ".exe", ".dll", ".lib", ".pdb", ".whl", ".zip", ".7z", ".tar",
    ".gz", ".onnx", ".xml", ".bin", ".gguf", ".safetensors"
}
```

Test cases must include:

- manifest hash mismatch;
- missing command stdout/stderr;
- executable payload disguised with upper-case suffix;
- unsafe relative path;
- secret-like token pattern;
- wrong route or source commit;
- command arguments stored as one shell string;
- unapproved executed deviation;
- binary record claiming `copied_to_artifact=true`;
- successful decision with a failed required component;
- Route B decision without accepted BR8 prerequisite;
- any claim-authorisation flag set true.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_bundle_validation
```

Expected: import failure for the new validator.

- [ ] **Step 3: Implement validation without executing evidence**

The module may use only file reads, JSON parsing, regular expressions, path validation and SHA-256. It must never import, launch, dynamically load or shell-execute evidence content.

Always write a Markdown report containing:

```text
route, component, run ID, attempt, files checked, hashes checked,
issues, prohibited payload scan, secret scan, final PASS/FAIL
```

Return exit code `0` only when `issues == []`.

- [ ] **Step 4: Verify valid and adversarial bundles**

Run the focused tests and complete Workbook 05 Python suite. Expected: valid fixture passes; every adversarial fixture fails for its named reason.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/build_bundle_validation.py \
        tests/testing/workbook05/test_build_bundle_validation.py
git commit -m "feat(workbook-05): validate documented-build bundles as untrusted data"
```

---

### Task 3: Extract a reusable Windows build execution module

**Files:**
- Create: `scripts/testing/workbook05/Workbook05.Build.psm1`
- Create: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- Produces: `New-Wb05ExternalWorkspace`
- Produces: `Invoke-Wb05LoggedProcess`
- Produces: `Start-Wb05ResourceSampler`
- Produces: `Stop-Wb05ResourceSampler`
- Produces: `Write-Wb05Json`
- Produces: `Write-Wb05Manifest`
- Produces: `Assert-Wb05SafePath`
- Produces: `Get-Wb05BinaryRecords`
- Produces: `Restore-Wb05Environment`

- [ ] **Step 1: Write static and behavioural contract tests**

Tests must assert that the module:

- does not contain `Invoke-Expression`, `Start-Process -ArgumentList` with a joined untrusted shell string, machine-wide Git configuration, registry edits, `git reset --hard`, `git clean`, or recursive deletion of `C:\w5a`/`C:\w5b`;
- uses `ProcessStartInfo` with separate stdout/stderr asynchronous drains;
- records arguments as an array;
- rejects reparse-point workspace roots;
- rejects an existing run directory;
- samples every two seconds;
- stops below 1.5 GiB available RAM for 10 seconds;
- stops above 90% commit for 10 seconds;
- records process-tree peaks;
- restores environment variables in `finally`.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_powershell_contract
```

- [ ] **Step 3: Implement the module**

Use the proven Windows argument-quoting and concurrent stream-draining approach from `Invoke-Workbook05RouteBRepair.ps1`, but separate it into focused exported functions. Every logical block must have beginner-readable comments.

The sampler writes:

```text
resource-samples.csv
resource-summary.json
```

`Invoke-Wb05LoggedProcess` returns a record object and never throws solely because stderr contains text; it throws only for process-start/integrity failures. Callers decide how to classify a non-zero exit.

- [ ] **Step 4: Run static tests and import smoke test**

```powershell
Import-Module '.\scripts\testing\workbook05\Workbook05.Build.psm1' -Force
Get-Command -Module Workbook05.Build
```

Expected: all named exported functions are present.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/Workbook05.Build.psm1 \
        tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "feat(workbook-05): add controlled Windows build execution module"
```

---

### Task 4: Implement Route A Runtime build orchestration

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1`
- Extend: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- CLI parameters: `RepositoryRoot`, `OutputDirectory`, `RunIdentity`, `PythonPath`.
- Produces component bundle: `route-a-runtime`.
- Consumes functions from `Workbook05.Build.psm1`.

- [ ] **Step 1: Write failing orchestration tests**

Require exact constants and command arrays for:

```text
repository: https://github.com/openvinotoolkit/openvino.git
commit: b9a1f201c109e0bed74763934f79483cf6c4cbf4
source root: C:\w5a\<run>\ov
build root: C:\w5a\<run>\b-ov
install root: C:\w5a\<run>\i-ov
```

Required configure arguments:

```text
-S <source> -B <build> -G "Visual Studio 17 2022" -A x64
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

Run the focused PowerShell contract tests. Expected: missing orchestrator failure.

- [ ] **Step 3: Implement source acquisition, configure, build and install**

Execute in documented order:

```text
git init
repository-scoped core.longpaths
git remote add
git fetch exact commit
git checkout --detach exact commit
git submodule update --init --recursive
cmake configure
cmake --build --config Release --parallel 2 --verbose
cmake --install --config Release --prefix <install>
```

Capture document hash, CMake cache summary, all commands, resource samples, dependency metadata, binary metadata and a component decision. Hash binaries in place; never copy them to evidence.

- [ ] **Step 4: Add simulated command-adapter tests**

Use injected fake process results to prove `Passed`, `Failed`, `Blocked` and `Infrastructure interrupted` decisions without compiling OpenVINO in unit tests.

- [ ] **Step 5: Run all focused and repository tests**

Expected: all Workbook 05 Python/PowerShell contracts pass; no live build occurs from the test suite.

- [ ] **Step 6: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1 \
        tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "feat(workbook-05): orchestrate exact Route A Runtime build"
```

---

### Task 5: Implement Route A GenAI build orchestration

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1`
- Extend: `tests/testing/workbook05/test_build_powershell_contract.py`

**Interfaces:**
- CLI parameters additionally include `RuntimeInstallDirectory` and `RuntimeDecisionPath`.
- Produces component bundle: `route-a-genai`.

- [ ] **Step 1: Write failing prerequisite and command tests**

Require:

- Runtime decision status `Passed`;
- exact Runtime source commit in the prerequisite record;
- exactly one installed `OpenVINOConfig.cmake`;
- exact GenAI repository and commit;
- separate GenAI source/build/install directories;
- `ENABLE_JS=OFF` and no archive download path.

- [ ] **Step 2: Verify RED**

Run the focused test and confirm missing orchestrator failure.

- [ ] **Step 3: Implement exact source-built compatibility orchestration**

Configure arguments:

```text
-S <genai-source> -B <genai-build> -G "Visual Studio 17 2022" -A x64
-DCMAKE_BUILD_TYPE=Release
-DOpenVINO_DIR=<resolved OpenVINOConfig.cmake parent>
-DENABLE_PYTHON=ON
-DENABLE_JS=OFF
-DPython3_EXECUTABLE=C:\Program Files\Python312\python.exe
```

Build and install with Release and parallelism 2. Record a compatibility attempt with `retained=true` only when Runtime identity matches and all commands pass.

- [ ] **Step 4: Verify environment restoration and mismatched Runtime rejection**

Tests must prove that `PATH`, `PYTHONPATH`, `OPENVINO_LIB_PATHS` and `OpenVINO_DIR` are restored after success and failure.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1 \
        tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "feat(workbook-05): orchestrate source-matched Route A GenAI build"
```

---

### Task 6: Implement fail-closed Route B build gating

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1`
- Extend: `tests/testing/workbook05/test_build_powershell_contract.py`
- Reuse: `scripts/testing/workbook05/route_b_repair.py`

**Interfaces:**
- Parameters include `Br8BundleDirectory`, `Br8ExpectedArtifactDigest`, `Br8AcceptedByProjectOwner`.
- Produces `Not applicable` or blocked record without touching external source when prerequisites fail.
- Produces Route B Runtime/GenAI build bundles only after every prerequisite passes.

- [ ] **Step 1: Write prerequisite rejection tests**

Reject before Git or CMake when:

- BR8 decision is not `ExecutableCandidate`;
- manifest fails;
- artifact digest differs;
- source commit differs;
- owner acceptance is false;
- `algorithm_files_changed` is true;
- any of the six BR8 cases is missing or did not pass.

- [ ] **Step 2: Verify RED**

Run the focused tests and confirm the orchestrator is absent.

- [ ] **Step 3: Implement the conditional Runtime sequence**

After prerequisite acceptance:

- acquire exact experimental source;
- apply `route_b_repair.py`;
- prove only `src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake` changed;
- configure with tests and per-target exposure enabled;
- build the narrow target and re-run the six cases;
- only then build/install the full Runtime with parallelism 2;
- optionally attempt GenAI compatibility in separate paths.

All Route B decisions continue to set model/activation/storage/performance/quality authorisations to false.

- [ ] **Step 4: Verify that blocked Route B never blocks Route A records**

Add a combined fixture where Route A passes and Route B is `Not applicable`. The phase build summary must preserve Route A as a valid build candidate.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1 \
        tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "feat(workbook-05): gate conditional Route B documented builds"
```

---

### Task 7: Add the secure staged workflow and repository gate

**Files:**
- Create: `.github/workflows/workbook-05-documented-build.yml`
- Create: `scripts/testing/Validate-Workbook05-BuildStage.ps1`
- Create: `tests/testing/workbook05/test_build_workflow_contract.py`

**Interfaces:**
- Manual input `stage` values: `route-a-runtime`, `route-a-genai`, `route-b-runtime`, `route-b-genai`.
- Artifact name: `workbook-05-build-<route>-<component>-${{ github.run_id }}-${{ github.run_attempt }}`.

- [ ] **Step 1: Write workflow security tests**

Require:

```yaml
permissions:
  contents: read
  actions: read
```

Also require exact runner labels, pinned Action SHAs, `persist-credentials: false`, `cancel-in-progress: false`, manual dispatch, same-repository branch restriction, 360-minute self-hosted timeout and independent `windows-latest` validation.

Reject workflow text containing model URLs, model execution, binary upload globs, `pull_request_target`, broad write permissions, `Invoke-Expression`, or direct merge steps.

- [ ] **Step 2: Verify RED**

Run `test_build_workflow_contract`; expected missing workflow/gate failure.

- [ ] **Step 3: Implement the repository gate**

`Validate-Workbook05-BuildStage.ps1` must run:

```text
all Workbook 05 Python tests
all Workbook 05 PowerShell tests
schema/template validation
build workflow-contract validation
forbidden-command scan
git diff --check
```

It prints `WORKBOOK05_BUILD_STAGE_GATE_PASS` only after every check succeeds.

- [ ] **Step 4: Implement staged workflow jobs**

Every self-hosted job runs the repository gate before its orchestrator. Every hosted job downloads the exact same-attempt artifact, reruns repository tests and invokes `build_bundle_validation.py`.

Upload evidence with `if: always()` so a scientific `Failed`/`Blocked` decision remains reviewable. Integrity failures still fail the job.

- [ ] **Step 5: Run full static verification**

```powershell
& '.\scripts\testing\Validate-Workbook05-BuildStage.ps1' `
  -RepositoryRoot (Get-Location).Path `
  -PythonPath 'C:\Program Files\Python312\python.exe'
```

Expected: all suites pass and final gate line appears once.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/workbook-05-documented-build.yml \
        scripts/testing/Validate-Workbook05-BuildStage.ps1 \
        tests/testing/workbook05/test_build_workflow_contract.py
git commit -m "ci(workbook-05): add independently validated documented-build workflow"
```

---

### Task 8: Add the Phase 2 review checklist and close R9

**Files:**
- Create: `docs/superpowers/plans/2026-08-06-workbook-05-phase-2-documented-build-review-checklist.md`

- [ ] **Step 1: Record line-by-line acceptance items**

Include source locks, route separation, exact commands, pre-approved deviations, resource thresholds, schemas, workflow permissions, prohibited payloads, test counts, unresolved threads and explicit non-claims.

- [ ] **Step 2: Run final repository verification on the exact head**

Run the build-stage gate, complete Workbook 05 test suite and WinUI build/tests. Record exact counts and head SHA.

- [ ] **Step 3: Review changed files and PR threads**

Confirm no application source, OpenVINO source, binary, model, archive or secret entered the repository. Confirm zero unresolved review threads.

- [ ] **Step 4: Update the draft PR**

Include implementation details, TDD red/green evidence, exact files, test counts, security boundary, known risks and the live-build boundary. Do not mark ready until all static checks pass.

- [ ] **Step 5: Commit documentation corrections if required**

```bash
git add docs/superpowers/plans/2026-08-06-workbook-05-phase-2-documented-build-review-checklist.md
git commit -m "docs(workbook-05): record Phase 2 build review gate"
```

**Checkpoint B2:** Synthetic valid/adversarial bundles behave correctly, workflow security tests pass, and no model execution is possible.

---

### Task 9: Execute and validate Route A Runtime and GenAI builds

**Files:**
- No repository code change unless a reproduced defect requires TDD correction.
- Update PR/checkpoint records with workflow evidence.

- [ ] **Step 1: Dispatch `route-a-runtime` on the exact reviewed head**

Keep the Intel laptop powered, plugged in, awake and connected. Do not use the machine interactively during the build.

- [ ] **Step 2: Inspect Runtime artifact**

Verify command order, origin, commit, recursive submodules, document hash, configure cache, build/install exit codes, warnings, resources, dependency records, binary hashes and hosted validation.

- [ ] **Step 3: Dispatch `route-a-genai` using the accepted Runtime installation**

The GenAI job must verify the exact Runtime decision and installed CMake package before configuring.

- [ ] **Step 4: Inspect GenAI artifact**

Verify the exact GenAI commit, source-built Runtime identity, compatibility record, build/install results and independent hosted validation.

- [ ] **Step 5: Run the standard-cache diagnostic defined for R10**

This diagnostic may use only the approved tiny diagnostic asset and codecs disabled. Record raw output and hashes. Do not describe TurboQuant as active.

- [ ] **Step 6: Record Route A decision**

Accept `BuildCandidate` only when Runtime, GenAI, diagnostic and both hosted validators pass.

**Checkpoint B3:** Route A Runtime and GenAI are built, installed, hashed and diagnostically usable without an activation claim.

---

### Task 10: Execute Route B or close it as blocked

**Files:**
- Update execution-index and PR evidence records only after BR8 and Route A decisions are known.

- [ ] **Step 1: Read the final BR8 decision**

Do not infer it from source or workflow colour. Use the exact validated artifact and owner acceptance record.

- [ ] **Step 2A: When BR8 is accepted as `ExecutableCandidate`, dispatch Route B Runtime**

Inspect the repeated narrow conformance gate, full Runtime build/install, patch boundary, binary hashes and hosted validation.

- [ ] **Step 2B: When BR8 is blocked, populate every dependent row**

Set each affected execution-index row to `Blocked` or `Not applicable`, with exact blocker ID, evidence path and continuation work package.

- [ ] **Step 3: Attempt Route B GenAI only after Runtime passes**

Record every compatibility attempt and retain/reject reason. A mismatch is a scientific blocker, not a reason to substitute Route A binaries.

- [ ] **Step 4: Record final Route B build decision**

No ambiguous middle state is allowed: executable with validated build evidence, or formally blocked.

**Checkpoint B4:** Route B is executable with evidence or formally closed as blocked. Granite model testing remains disabled until later activation, storage and no-fallback gates.

---

## Plan self-review

- **Spec coverage:** Every R8 requirement maps to Tasks 1-10: source locks and commands in Tasks 4-6; evidence contracts in Tasks 1-3; workflow and hosted validation in Task 7; live builds in Tasks 9-10.
- **Placeholder scan:** The plan uses named runtime variables only where the implementation interface defines them. It contains no `TBD`, `TODO`, `implement later` or unspecified validation step.
- **Type consistency:** Python validator names, PowerShell module exports, route IDs, component names and decision states are consistent across tasks.
- **Scope:** The plan excludes model acquisition, formal inference, activation proof, packed-storage proof, performance and quality measurement.

## Execution mode

Use **inline execution with `superpowers:executing-plans`**, because the current environment has GitHub connector access but no independent subagent execution tool. Execute one task at a time and stop at every failed verification or scientific blocker.
