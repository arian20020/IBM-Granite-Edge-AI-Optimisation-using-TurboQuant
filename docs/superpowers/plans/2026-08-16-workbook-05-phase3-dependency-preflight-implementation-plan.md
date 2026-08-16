# Workbook 05 Phase 3 Clean Dependency Preflight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a clean, reproducible Windows Python 3.12.10 dependency preflight for the reviewed Granite 4.1 3B OpenVINO conversion toolchain, then validate its text-only evidence independently without downloading, converting, loading, or executing a model.

**Architecture:** A dedicated GitHub Actions workflow has three boundaries: a GitHub-hosted repository contract, a manually confirmed Lenovo self-hosted collector, and a fresh GitHub-hosted validator that treats the uploaded artifact only as untrusted data. The collector creates one fresh `C:\w5c\dependency-preflight-<run-id>-<attempt>` workspace, verifies immutable source trees, creates and installs hash-locked normal distributions, installs the two reviewed VCS packages from verified local source trees, runs import/CLI/no-model checks, and writes a manifest only after a `Passed` decision. Existing Phase 3 offline-fixture and live-asset-lock code remain separate and unchanged.

**Tech Stack:** Python 3.12.10 standard library, `jsonschema==4.25.1`, Windows PowerShell 5.1, Git, `pip`, `pip-tools==7.5.0`, GitHub Actions, unittest, Draft 2020-12 JSON Schema, SHA-256 manifests.

## Global constraints

- Campaign: `GTQ-WB05-MF-v1`.
- Route: `route-a-merged-openvino`.
- Base interpreter: `C:\Program Files\Python312\python.exe`, exactly `Python 3.12.10`.
- Workspace identity: `C:\w5c\dependency-preflight-<github.run_id>-<github.run_attempt>`.
- Existing workspace identities are rejected. They are never reused, repaired, reset, cleaned, or deleted.
- The workflow must never read from or write to `C:\w5m` and must not modify anything under `C:\w5a`.
- Pull requests may reach only the GitHub-hosted repository-contract job.
- The self-hosted collector requires `workflow_dispatch`, `refs/heads/main`, and `confirm_live_dependency_preflight == true`.
- Self-hosted labels are exactly `self-hosted`, `Windows`, `X64`, `workbook05`, and `intel-target`.
- Workflow permissions remain `contents: read` and `actions: read`.
- Every external action is pinned to a full 40-character commit SHA and every checkout uses `persist-credentials: false`.
- `concurrency.cancel-in-progress` remains `false` so one attempt cannot erase the evidence boundary of another.
- All native commands use an explicit executable and argument array. No `Invoke-Expression`, `cmd /c`, shell-generated command string, or full environment capture is permitted.
- Every native command has a finite deadline, concurrent stdout/stderr draining, a stable command ID, an allowlisted environment record, timestamps, exit code, and portable evidence paths.
- Text evidence only: UTF-8 JSON, CSV, Markdown, logs, and SHA-256 manifests.
- Prohibited artifact payloads include model/tokenizer files, OpenVINO IR, source archives, wheels, executables, libraries, checkpoints, GGUF, ONNX, safetensors, ZIP/TAR/GZ/7z, and symbolic-link/reparse payloads.
- `manifest.sha256` is written last and only for a complete `Passed` attempt.
- JSON finalisation uses same-directory temporary files followed by atomic replacement. A successful attempt leaves no `*.tmp` files.
- A failed or interrupted attempt preserves completed evidence and writes `failure.json`; it does not create a positive manifest or silently continue.
- All scientific authorisation flags remain `false`.
- The existing offline fixture script `Invoke-Workbook05Phase3DependencyPreflight.ps1`, Phase 3 asset workflow, asset-lock settings, Runtime/GenAI evidence, Route B controls, and application production code are not changed by this enablement.
- Every behavioural change follows RED → GREEN → refactor → focused commit.
- Code comments explain each logical block and every security- or evidence-sensitive line.

## Reviewed dependency candidate

```text
optimum-intel @ git+https://github.com/huggingface/optimum-intel.git@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0
optimum @ git+https://github.com/huggingface/optimum.git@982e495540364f95da1e4b6f62d2d4e5907d08fd
transformers==5.5.0
huggingface-hub==1.21.0
nncf==3.2.0
openvino==2026.2.1
openvino-tokenizers==2026.2.1.0
```

## Verified upstream facts that constrain implementation

- The pinned `optimum-intel` `setup.py` declares the reviewed ten-item `INSTALL_REQUIRE` list and the `optimum-cli=optimum.commands.optimum_cli:main` console entry point.
- The pinned `optimum` `setup.py` declares `transformers>=4.29`, `torch>=1.11`, `packaging`, `numpy`, and `huggingface_hub>=0.8.0`, plus the same `optimum-cli` entry point.
- Both pinned repositories contain `pyproject.toml` files without a `[build-system]` table; the collector therefore records that fact and uses only the already hash-locked local `setuptools`/`wheel` build prerequisites with build isolation disabled.
- pip's `--report` output is supported installation evidence but is not a lock-file format. The committed/generated hash lock remains the installation authority.
- `pip-compile` must run in the same Windows/Python environment as the target installation because environment markers and available distributions can change the result.
- `pip-compile --generate-hashes` is used to generate pip hash-checking input.
- GitHub job-level `if` expressions are evaluated before a job is routed to a runner; the branch/confirmation condition therefore protects the Lenovo before the self-hosted job is assigned.

## File map

### New files

```text
.github/workflows/workbook-05-phase3-dependency-preflight.yml
scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1
scripts/testing/workbook05/requirements.phase3-bootstrap.in
scripts/testing/workbook05/requirements.phase3-bootstrap.txt
scripts/testing/workbook05/phase3/dependency_lock_cli.py
scripts/testing/workbook05/phase3/dependency_source_contract.py
scripts/testing/workbook05/phase3/dependency_no_model_check.py
tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
tests/testing/workbook05/test_phase3_dependency_preflight_workflow_contract.py
tests/testing/workbook05/test_phase3_dependency_source_contract.py
tests/testing/workbook05/test_phase3_dependency_no_model_check.py
docs/testing/workbook05/phase3-dependency-preflight-runbook.md
```

### Existing files to modify

```text
scripts/testing/workbook05/Workbook05.ControlledProcess.psm1
scripts/testing/workbook05/phase3/conversion.py
scripts/testing/workbook05/phase3/dependency_lock.py
scripts/testing/workbook05/phase3/dependency_preflight.py
scripts/testing/workbook05/phase3/dependency_import_check.py
scripts/testing/workbook05/phase3/dependency_bundle_validation.py
experiments/granite_turboquant_intel/schemas/workbook05/conversion-dependency-preflight.schema.json
scripts/testing/Validate-Workbook05-Phase3.ps1
tests/testing/workbook05/test_phase3_dependency_preflight.py
tests/testing/workbook05/test_phase3_dependency_lock.py
tests/testing/workbook05/test_phase3_dependency_bundle_validation.py
tests/testing/workbook05/test_build_powershell_contract.py
docs/testing/workbook05/phase3-c1-implementation-status.md
docs/testing/workbook05/phase3-asset-lock-runbook.md
```

### Explicitly unchanged files

```text
scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflight.ps1
scripts/testing/workbook05/Invoke-Workbook05Phase3AssetLock.ps1
.github/workflows/workbook-05-phase3-assets.yml
experiments/granite_turboquant_intel/configurations/workbook05/phase3-asset-lock-settings.json
IBM Granite with TurboQuant (Intel)/**
```

---

### Task 1: Extend the closed decision model for real live outcomes

**Files:**
- Modify: `scripts/testing/workbook05/phase3/dependency_preflight.py`.
- Modify: `scripts/testing/workbook05/phase3/dependency_lock.py`.
- Modify: `experiments/granite_turboquant_intel/schemas/workbook05/conversion-dependency-preflight.schema.json`.
- Test: `tests/testing/workbook05/test_phase3_dependency_preflight.py`.
- Test: `tests/testing/workbook05/test_phase3_dependency_lock.py`.

**Interfaces:**

```python
DependencyCheck.status in {
    "Passed",
    "Failed",
    "Blocked",
    "IntegrityFailure",
    "InfrastructureInterrupted",
}

collect_dependency_preflight_record(...) -> dict[str, object]
build_dependency_preflight_record(observation: dict[str, object]) -> dict[str, object]
```

- [ ] **Step 1: Write failing regression tests**

Add tests proving that:

```python
def test_interrupted_check_is_not_misreported_as_dependency_failure(self):
    checks = list(_checks())
    checks[1] = DependencyCheck(
        name="install",
        status="InfrastructureInterrupted",
        exit_code=-1,
    )
    record = _record(checks=tuple(checks))
    self.assertEqual("InfrastructureInterrupted", record["status"])


def test_live_optimum_intel_git_local_version_is_accepted(self):
    observation = _observation()
    observation["vcs_packages"][0]["version"] = "2.3.0.dev0+a3b6012"
    record = build_dependency_preflight_record(observation)
    self.assertEqual("Passed", record["status"])
```

Also validate the resulting record against the Draft 2020-12 schema.

- [ ] **Step 2: Run and verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight `
  tests.testing.workbook05.test_phase3_dependency_lock
```

Expected: `InfrastructureInterrupted` is rejected and the Git-derived local version is classified as an integrity failure.

- [ ] **Step 3: Implement the minimal status/version changes**

Use explicit precedence so an integrity defect cannot be hidden by an interruption:

```python
if integrity_reasons:
    status = "IntegrityFailure"
elif any(check.status == "InfrastructureInterrupted" for check in checks):
    status = "InfrastructureInterrupted"
elif failed_checks:
    status = "Blocked"
else:
    status = "Passed"
```

Accept only this source-derived Optimum Intel version shape:

```python
_OPTIMUM_INTEL_LIVE_VERSION = re.compile(
    r"^2\.3\.0\.dev0(?:\+[0-9a-f]{7,40})?$"
)
```

Add `InfrastructureInterrupted` to the schema's check and overall decision enums. Do not change any scientific authorisation constant.

- [ ] **Step 4: Run focused tests and the schema suite**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_schema_registry
```

Expected: zero failures.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_preflight.py \
        scripts/testing/workbook05/phase3/dependency_lock.py \
        experiments/granite_turboquant_intel/schemas/workbook05/conversion-dependency-preflight.schema.json \
        tests/testing/workbook05/test_phase3_dependency_preflight.py \
        tests/testing/workbook05/test_phase3_dependency_lock.py
git commit -m "feat(workbook05): distinguish interrupted dependency preflights"
```

---

### Task 2: Generalise hash-lock verification and commit the bootstrap lock

**Files:**
- Create: `scripts/testing/workbook05/requirements.phase3-bootstrap.in`.
- Create: `scripts/testing/workbook05/requirements.phase3-bootstrap.txt`.
- Create: `scripts/testing/workbook05/phase3/dependency_lock_cli.py`.
- Modify: `scripts/testing/workbook05/phase3/dependency_lock.py`.
- Modify: `tests/testing/workbook05/test_phase3_dependency_lock.py`.
- Create focused CLI tests in: `tests/testing/workbook05/test_phase3_dependency_lock_cli.py`.

**Interfaces:**

```python
parse_hash_locked_requirements(
    text: str,
    *,
    required_direct_versions: dict[str, str] | None = None,
    forbidden_names: frozenset[str] = frozenset(),
) -> tuple[LockedDistribution, ...]

parse_install_report_against_lock(
    report: dict[str, object],
    lock: tuple[LockedDistribution, ...],
    *,
    forbidden_names: frozenset[str] = frozenset(),
) -> tuple[DependencyPackage, ...]

python -m scripts.testing.workbook05.phase3.dependency_lock_cli \
  validate-install --lock ... --report ... --kind bootstrap --output ...
```

- [ ] **Step 1: Write failing generic-lock tests**

Require exact pins, SHA-256 hashes, one canonical package name, no URL/VCS/editable/index directives, and report-to-lock archive digest equality for both bootstrap and normal locks. Preserve the existing normal-lock rule that forbids `optimum` and `optimum-intel`.

```python
def test_bootstrap_lock_requires_pip_tools_7_5_0_and_hashes(self):
    lock = parse_hash_locked_requirements(
        "pip-tools==7.5.0 --hash=sha256:" + "a" * 64 + "\n",
        required_direct_versions={"pip-tools": "7.5.0"},
    )
    self.assertEqual(["pip-tools"], [item.name for item in lock])


def test_generic_report_rejects_unlocked_distribution(self):
    with self.assertRaisesRegex(ValueError, "not present in the lock"):
        parse_install_report_against_lock(report_with_extra_click(), lock)
```

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_dependency_lock_cli
```

Expected: missing generic parameters/CLI failures.

- [ ] **Step 3: Refactor behind the existing public behaviour**

Keep `parse_normal_install_report(...)` as a compatibility wrapper around the generic validator. The refactor must not weaken any existing normal-distribution checks.

The CLI writes one atomic JSON object containing:

```json
{
  "kind": "bootstrap",
  "lock_sha256": "64 lowercase hex characters",
  "package_count": 7,
  "packages": []
}
```

The concrete count comes from the generated lock; tests calculate it rather than hard-coding a speculative value.

- [ ] **Step 4: Generate the real bootstrap lock on exact Windows Python**

Create this input file:

```text
pip-tools==7.5.0
```

Then run:

```powershell
$ErrorActionPreference = 'Stop'
$python = 'C:\Program Files\Python312\python.exe'
$version = ((& $python --version 2>&1) | Out-String).Trim()
if ($version -ne 'Python 3.12.10') {
    throw "Expected Python 3.12.10, observed $version"
}

$root = Join-Path $env:TEMP ('wb05-bootstrap-lock-' + [Guid]::NewGuid().ToString('N'))
& $python -m venv $root
$venvPython = Join-Path $root 'Scripts\python.exe'

& $venvPython -m pip install --disable-pip-version-check 'pip-tools==7.5.0'
if ($LASTEXITCODE -ne 0) { throw 'Temporary pip-tools installation failed.' }

$observed = ((& $venvPython -c "import importlib.metadata; print(importlib.metadata.version('pip-tools'))") | Out-String).Trim()
if ($observed -ne '7.5.0') { throw "Expected pip-tools 7.5.0, observed $observed" }

& $venvPython -m piptools compile `
    --no-config `
    --resolver=backtracking `
    --generate-hashes `
    --strip-extras `
    --allow-unsafe `
    --no-emit-index-url `
    --no-emit-trusted-host `
    --output-file 'scripts/testing/workbook05/requirements.phase3-bootstrap.txt' `
    'scripts/testing/workbook05/requirements.phase3-bootstrap.in'
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap lock generation failed.' }

Remove-Item -LiteralPath $root -Recurse -Force
```

The generated file must contain `pip-tools==7.5.0`, exact versions for every transitive package, and at least one SHA-256 per distribution. It must contain no VCS/URL/editable/index directive.

- [ ] **Step 5: Validate the committed lock**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_dependency_lock_cli
```

Expected: zero failures and the real committed lock parses successfully.

- [ ] **Step 6: Commit**

```bash
git add scripts/testing/workbook05/requirements.phase3-bootstrap.in \
        scripts/testing/workbook05/requirements.phase3-bootstrap.txt \
        scripts/testing/workbook05/phase3/dependency_lock.py \
        scripts/testing/workbook05/phase3/dependency_lock_cli.py \
        tests/testing/workbook05/test_phase3_dependency_lock.py \
        tests/testing/workbook05/test_phase3_dependency_lock_cli.py
git commit -m "feat(workbook05): add hash-locked preflight bootstrap"
```

---

### Task 3: Validate pinned source metadata without executing source code

**Files:**
- Create: `scripts/testing/workbook05/phase3/dependency_source_contract.py`.
- Modify: `scripts/testing/workbook05/phase3/conversion.py`.
- Test: `tests/testing/workbook05/test_phase3_dependency_source_contract.py`.

**Interfaces:**

```python
@dataclass(frozen=True)
class SourcePackageContract:
    name: str
    version: str
    runtime_requirements: tuple[str, ...]
    console_entry_points: tuple[str, ...]
    build_system_declared: bool

inspect_source_contract(name: str, root: Path) -> SourcePackageContract
validate_reviewed_source_contracts(
    optimum_root: Path,
    optimum_intel_root: Path,
) -> dict[str, object]
```

- [ ] **Step 1: Add exact reviewed constants**

In `conversion.py`, add the pinned Optimum runtime requirements alongside the existing Optimum Intel list:

```python
REVIEWED_OPTIMUM_CONSTRAINTS = (
    "transformers>=4.29",
    "torch>=1.11",
    "packaging",
    "numpy",
    "huggingface_hub>=0.8.0",
)

REVIEWED_OPTIMUM_ENTRY_POINT = (
    "optimum-cli=optimum.commands.optimum_cli:main"
)
```

Keep the existing Optimum Intel constraints unchanged.

- [ ] **Step 2: Write failing static-parser tests**

Use temporary `setup.py`, version, and `pyproject.toml` fixtures. Assert exact extraction, rejection of dynamic/non-literal dependency construction, rejection of an unexpected `[build-system]`, rejection of a changed console entry point, and rejection of an unexpected dependency.

```python
def test_parser_reads_literal_setup_metadata_without_exec(self):
    contract = inspect_source_contract("optimum", fixture_root)
    self.assertEqual("2.3.0", contract.version)
    self.assertEqual(REVIEWED_OPTIMUM_CONSTRAINTS, contract.runtime_requirements)


def test_dynamic_dependency_expression_is_rejected(self):
    (fixture_root / "setup.py").write_text(
        "REQUIRED_PKGS = load_requirements()\nsetup(install_requires=REQUIRED_PKGS)\n",
        encoding="utf-8",
    )
    with self.assertRaisesRegex(ValueError, "literal"):
        inspect_source_contract("optimum", fixture_root)
```

- [ ] **Step 3: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_source_contract
```

Expected: missing-module failure.

- [ ] **Step 4: Implement an AST/TOML data-only parser**

Use `ast.parse` and `ast.literal_eval` for the exact named constants and `setup(...)` keyword arguments. Use `tomllib` only to inspect `pyproject.toml`. Never import or execute `setup.py`.

```python
module = ast.parse(setup_path.read_text(encoding="utf-8"), filename=str(setup_path))
value = ast.literal_eval(assignment.value)
```

The output record must state:

```json
{
  "source_metadata_execution": false,
  "build_system_declared": false,
  "installer_build_mode": "setuptools-no-build-isolation",
  "reviewed_build_tools": ["setuptools", "wheel"]
}
```

- [ ] **Step 5: Run focused and conversion tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_source_contract `
  tests.testing.workbook05.test_phase3_conversion
```

Expected: zero failures.

- [ ] **Step 6: Commit**

```bash
git add scripts/testing/workbook05/phase3/conversion.py \
        scripts/testing/workbook05/phase3/dependency_source_contract.py \
        tests/testing/workbook05/test_phase3_dependency_source_contract.py
git commit -m "feat(workbook05): inspect pinned dependency sources as data"
```

---

### Task 4: Add the repository-controlled no-model compatibility check

**Files:**
- Create: `scripts/testing/workbook05/phase3/dependency_no_model_check.py`.
- Test: `tests/testing/workbook05/test_phase3_dependency_no_model_check.py`.

**Interfaces:**

```python
run_no_model_check(
    optimum_root: Path,
    optimum_intel_root: Path,
) -> dict[str, object]

python -m scripts.testing.workbook05.phase3.dependency_no_model_check \
  --optimum-root ... \
  --optimum-intel-root ... \
  --output ...
```

- [ ] **Step 1: Write failing tests**

Prove that the check:

- validates both static source contracts;
- validates the exact reviewed direct candidate;
- constructs the exact conversion argument array with `ConversionRequest`;
- sets `trust_remote_code=False`;
- rejects `--trust-remote-code` anywhere in the resulting arguments;
- does not open a model path, contact a model repository, import Optimum, or call a subprocess;
- writes JSON atomically and leaves no temporary file.

```python
def test_no_model_check_constructs_safe_arguments_only(self):
    record = run_no_model_check(optimum_root, optimum_intel_root)
    self.assertFalse(record["trust_remote_code"])
    self.assertNotIn("--trust-remote-code", record["conversion_arguments"])
    self.assertFalse(record["model_opened"])
    self.assertFalse(record["network_contacted"])
```

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_no_model_check
```

Expected: missing-module failure.

- [ ] **Step 3: Implement the minimum data-only check**

Construct paths as inert argument values only:

```python
request = ConversionRequest(
    optimum_cli=Path(r"C:\w5c\dependency-preflight-check\venv\Scripts\optimum-cli.exe"),
    source_model_path=Path(r"C:\w5m\not-opened-source"),
    output_directory=Path(r"C:\w5m\not-created-output"),
    trust_remote_code=False,
)
arguments = build_optimum_argument_list(request)
```

Do not call the executable and do not test whether either model path exists.

- [ ] **Step 4: Run focused tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_no_model_check `
  tests.testing.workbook05.test_phase3_conversion
```

Expected: zero failures.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_no_model_check.py \
        tests/testing/workbook05/test_phase3_dependency_no_model_check.py
git commit -m "feat(workbook05): add no-model conversion compatibility check"
```

---

### Task 5: Extend the controlled process adapter without changing existing build behaviour

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.ControlledProcess.psm1`.
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`.
- Test behaviour in: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.

**Interface change:**

```powershell
Invoke-Wb05ControlledLoggedProcess `
    -Component 'dependency-preflight' `
    -LogFileExtension 'txt' `
    -MaximumElapsedSeconds 900
```

- [ ] **Step 1: Write failing contract tests**

Require `dependency-preflight` in the component `ValidateSet` and an optional log extension that defaults to `log` so all Runtime/GenAI call sites retain their current filenames.

```python
def test_controlled_process_supports_dependency_preflight_without_changing_default_logs(self):
    self.assertIn("'dependency-preflight'", self.text)
    self.assertIn("[string]$LogFileExtension = 'log'", self.text)
```

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_powershell_contract
```

Expected: missing component/log-extension assertions.

- [ ] **Step 3: Implement the narrow adapter extension**

```powershell
[ValidateSet('runtime', 'genai', 'dependency-preflight')]
[string]$Component,

[ValidateSet('log', 'txt')]
[string]$LogFileExtension = 'log'
```

Build paths with the validated extension:

```powershell
$stdoutPath = Join-Path $EvidenceDirectory "$safeId.stdout.$LogFileExtension"
$stderrPath = Join-Path $EvidenceDirectory "$safeId.stderr.$LogFileExtension"
```

Do not alter argument quoting, deadlines, process-tree termination, resource sampling, environment allowlisting, or existing return fields.

- [ ] **Step 4: Run existing deadline and build tests**

```powershell
& '.\tests\testing\workbook05\Invoke-BuildProcessDeadlineTests.Tests.ps1'

& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_powershell_contract
```

Expected: existing `.log` behaviour and deadline evidence still pass.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/Workbook05.ControlledProcess.psm1 \
        tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "refactor(workbook05): admit controlled dependency commands"
```

---

### Task 6: Implement fresh workspace and immutable source-verification stages

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1`.
- Create: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.

**Entry point:**

```powershell
& '.\scripts\testing\workbook05\Invoke-Workbook05Phase3DependencyPreflightLive.ps1' `
    -RepositoryRoot $env:GITHUB_WORKSPACE `
    -RunId $env:GITHUB_RUN_ID `
    -RunAttempt $env:GITHUB_RUN_ATTEMPT `
    -BasePythonPath 'C:\Program Files\Python312\python.exe'
```

- [ ] **Step 1: Write failing simulation tests for the first two stages**

The test script must use a temporary normal local root through a hidden `-SimulationMode` seam. Production calls must reject a custom root or command invoker unless simulation mode is explicitly enabled.

Test these cases:

```text
fresh normal workspace accepted
existing workspace rejected without deletion
file/junction/symlink/reparse root rejected
UNC/device path rejected
wrong source origin rejected
wrong source commit rejected
dirty source rejected
tracked symlink rejected
source failure occurs before any install command
```

The simulation command invoker returns predetermined command records; it never reaches the network.

- [ ] **Step 2: Verify RED**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1'
```

Expected: live script is missing.

- [ ] **Step 3: Implement strict entry validation and stage ledger**

Use this exact stage order:

```powershell
$StageOrder = @(
    'workspace-validation',
    'source-verification',
    'lock-generation',
    'normal-install',
    'vcs-install',
    'imports',
    'cli-help',
    'no-model-compatibility',
    'record-generation',
    'manifest-generation'
)
```

Derive—not accept—the workspace identity:

```powershell
$runIdentity = "dependency-preflight-$RunId-$RunAttempt"
$workspaceRoot = Join-Path 'C:\w5c' $runIdentity
```

Verify the base interpreter before creating the venv. Record the base interpreter only as a prerequisite; the decision records the fresh venv interpreter.

- [ ] **Step 4: Implement immutable Git acquisition and source manifests**

For each source, use explicit commands equivalent to:

```text
git init <source-root>
git -C <source-root> remote add origin <exact-https-origin>
git -C <source-root> fetch --no-tags --depth 1 origin <full-commit>
git -C <source-root> checkout --detach <full-commit>
git -C <source-root> remote get-url origin
git -C <source-root> rev-parse HEAD
git -C <source-root> status --porcelain --untracked-files=all
git -C <source-root> ls-files
```

Every call goes through `Invoke-Wb05ControlledLoggedProcess` with `-Component dependency-preflight`, `-LogFileExtension txt`, and a finite deadline. Never use `git reset --hard`, `git clean`, moving branches, tags, or implicit default-branch checkout.

Pass the `git ls-files` output to the existing `source_tree_manifest.py` to write:

```text
sources/optimum.json
sources/optimum-intel.json
sources/optimum-files.csv
sources/optimum-intel-files.csv
```

- [ ] **Step 5: Run source-stage simulations**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'workspace-source'
```

Expected: all source-boundary cases pass and no install command appears after a failed source check.

- [ ] **Step 6: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1 \
        tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
git commit -m "feat(workbook05): verify fresh preflight sources"
```

---

### Task 7: Implement lock generation and controlled installation stages

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1`.
- Modify: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.
- Use: `scripts/testing/workbook05/phase3/dependency_lock_cli.py`.
- Use: `scripts/testing/workbook05/phase3/dependency_source_contract.py`.

- [ ] **Step 1: Write failing simulations for resolver/install boundaries**

Require exact command order and flags:

```text
venv creation
bootstrap install with --require-hashes and --report
bootstrap report validation
source metadata validation
pip-compile with --no-config and --generate-hashes
normal lock validation
normal install with --require-hashes and --report
normal install report validation
optimum local install with --no-deps --no-build-isolation
optimum-intel local install with --no-deps --no-build-isolation
pip check
```

Reject:

```text
unhashed bootstrap or normal lock
URL/VCS/editable/index directive in either normal lock
normal lock direct-version drift
unexpected installed distribution
archive digest absent from or different from the lock
optimum or optimum-intel in the normal lock/report
VCS install missing --no-deps
VCS install missing --no-build-isolation
source metadata drift
```

- [ ] **Step 2: Verify RED**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'lock-install'
```

Expected: lock/install stages are not implemented.

- [ ] **Step 3: Create the fresh environment and install the bootstrap lock**

Use only the venv interpreter:

```powershell
& $BasePythonPath -m venv $venvRoot
$venvPython = Join-Path $venvRoot 'Scripts\python.exe'

& $venvPython -m pip install `
    --disable-pip-version-check `
    --require-hashes `
    --report $bootstrapReportPath `
    -r $bootstrapLockPath
```

Do not upgrade the machine-wide Python or pip. Record the venv Python/pip paths, versions, and file hashes after creation.

- [ ] **Step 4: Validate source metadata before resolution**

Run the data-only source contract CLI before `pip-compile`. Save `reports/source-contracts.json`. A mismatch is `IntegrityFailure`, not a reason to ask pip to resolve a different candidate.

- [ ] **Step 5: Generate and validate the normal lock in the target environment**

Write a workspace-local input containing only:

```text
transformers==5.5.0
huggingface-hub==1.21.0
nncf==3.2.0
openvino==2026.2.1
openvino-tokenizers==2026.2.1.0
```

Then execute:

```text
python -m piptools compile
--no-config
--resolver=backtracking
--generate-hashes
--strip-extras
--allow-unsafe
--no-emit-index-url
--no-emit-trusted-host
--output-file <workspace>/evidence/locks/requirements.phase3-assets.txt
<workspace>/requirements.phase3-assets.in
```

The exact paths are separate argument-array items. Validate the lock before installation.

- [ ] **Step 6: Install normal and VCS packages**

Normal distributions use the generated lock and `reports/normal-install-report.json`. The two VCS packages are installed in this order from their verified local trees:

```text
optimum
optimum-intel
```

Each local install includes exactly:

```text
--no-deps
--no-build-isolation
```

It must not contain an index URL, VCS URL, branch, tag, or remote-code option. Run `python -m pip check` afterward.

Write `reports/vcs-packages.json` from `importlib.metadata` observations plus the already verified full source commits; do not infer commit identity from the installed version string.

- [ ] **Step 7: Run lock/install simulations and focused Python tests**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'lock-install'

& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_dependency_lock_cli `
  tests.testing.workbook05.test_phase3_dependency_source_contract
```

Expected: zero failures.

- [ ] **Step 8: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1 \
        tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
git commit -m "feat(workbook05): resolve and install locked dependencies"
```

---

### Task 8: Add imports, CLI, decision, failure, and manifest finalisation

**Files:**
- Modify: `scripts/testing/workbook05/phase3/dependency_import_check.py`.
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1`.
- Modify: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.
- Add focused Python tests to: `tests/testing/workbook05/test_phase3_dependency_import_check.py`.

- [ ] **Step 1: Write failing import-boundary tests**

Add `--expected-environment-root` and reject any imported module whose resolved `__file__` is outside the fresh venv. The test uses temporary fake packages to prove both accepted and escaped module paths.

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_import_check
```

Expected: missing option/path enforcement.

- [ ] **Step 3: Implement the exact six checks**

The final `checks.json` order is:

```text
resolver
install
imports
cli_help
no_model_compatibility
remote_code_disabled
```

- `imports`: run `dependency_import_check.py` in a new venv Python process and require all five exact modules.
- `cli_help`: execute the venv's `optimum-cli.exe --help` and require exit code `0`.
- `no_model_compatibility`: run `dependency_no_model_check.py` and require exit code `0`.
- `remote_code_disabled`: derive `Passed` only from the no-model record's explicit `trust_remote_code=false` and absence of `--trust-remote-code`; do not execute another tool.

- [ ] **Step 4: Build observation and decision atomically**

Write:

```text
observation.json
checks.json
stage-order.json
commands/command-index.json
summary.md
```

Then invoke the existing `dependency_preflight_cli.py` to create `decision.json`. The command index contains unique command IDs and portable paths to command records/stdout/stderr/resource evidence.

- [ ] **Step 5: Implement failure and interruption behaviour**

In one outer `try/catch/finally`:

```powershell
catch {
    # Preserve the first causal message and completed stages.
    Write-Wb05AtomicJson -Path $failurePath -Value $failureRecord
    throw
}
```

Rules:

- preserve the first causal exception;
- record current stage and completed stages;
- record `InfrastructureInterrupted` for controlled process deadline/safety termination;
- do not overwrite an existing final record;
- do not write `manifest.sha256` unless `decision.status == 'Passed'`;
- remove no evidence on failure;
- leave no `*.tmp` after a successful finalisation.

- [ ] **Step 6: Write the manifest last**

Only after the decision is schema-valid and `Passed`:

```powershell
& $venvPython -m scripts.testing.workbook05.hash_manifest `
    --root $evidenceRoot `
    --output (Join-Path $evidenceRoot 'manifest.sha256')
```

No later step may change any bundle member.

- [ ] **Step 7: Run complete collector simulations**

Test success, import failure, CLI failure, no-model failure, trust-remote-code rejection, command timeout, thrown simulation exception, no manifest on failure, manifest last on success, and no temporary file after success.

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1'
```

Expected: all simulations pass without network or model access.

- [ ] **Step 8: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_import_check.py \
        scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1 \
        tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1 \
        tests/testing/workbook05/test_phase3_dependency_import_check.py
git commit -m "feat(workbook05): finalise dependency preflight evidence"
```

---

### Task 9: Harden independent validation for the complete live bundle

**Files:**
- Modify: `scripts/testing/workbook05/phase3/dependency_bundle_validation.py`.
- Modify: `tests/testing/workbook05/test_phase3_dependency_bundle_validation.py`.
- Modify only if a shared helper is required: `scripts/testing/workbook05/hash_manifest.py`.

**Required successful bundle:**

```text
decision.json
observation.json
checks.json
stage-order.json
summary.md
manifest.sha256
locks/requirements.phase3-bootstrap.txt
locks/requirements.phase3-assets.txt
reports/bootstrap-install-report.json
reports/bootstrap-packages.json
reports/normal-install-report.json
reports/normal-packages.json
reports/vcs-packages.json
reports/source-contracts.json
reports/no-model-check.json
sources/optimum.json
sources/optimum-intel.json
sources/optimum-files.csv
sources/optimum-intel-files.csv
commands/command-index.json
logs/*.command.json
logs/*.stdout.txt
logs/*.stderr.txt
logs/*.resources.json
logs/*.resources.csv
```

- [ ] **Step 1: Expand the valid fixture first**

Make the in-test valid bundle include every required live member and all cross-record relationships. Confirm the old validator fails RED because it does not understand the new evidence.

- [ ] **Step 2: Add adversarial tests**

Reject each of these independently:

```text
missing required member
changed/added/duplicate manifest member
parent traversal or backslash path
case-colliding bundle path
secret/token pattern
symlink/reparse/non-regular member
forbidden suffix in any case
bootstrap lock/report mismatch
normal lock/report mismatch
unexpected installed distribution
VCS package in normal lock/report
wrong VCS origin or commit
source CSV duplicate/case collision
source CSV row hash/size/path drift
source JSON file_count or aggregate mismatch
source-contract drift
check order/status mismatch
command-index duplicate/missing/unsafe record
command record pointing to missing stdout/stderr
stage-order mismatch
unrecomputable Passed decision
any true scientific authorisation
```

- [ ] **Step 3: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_bundle_validation
```

Expected: new live valid fixture and adversarial cases fail for missing implementation.

- [ ] **Step 4: Implement read-only cross-validation**

The validator may only enumerate, read, parse, hash, and compare. It must never import, execute, load, or shell an artifact member.

Recompute source aggregate hashes from CSV rows using the same canonical record format as `sha256_tree`:

```python
line = f"{relative_path}\0{size_bytes}\0{sha256}\n".encode("utf-8")
aggregate.update(line)
```

Validate both locks and reports with the generic lock validator. Rebuild the decision from `observation.json` and compare exact object equality with `decision.json`.

- [ ] **Step 5: Run focused and full Python suites**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_bundle_validation

& 'C:\Program Files\Python312\python.exe' -m unittest discover -v `
  -s tests/testing/workbook05 -p 'test_*.py'
```

Expected: zero failures.

- [ ] **Step 6: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_bundle_validation.py \
        scripts/testing/workbook05/hash_manifest.py \
        tests/testing/workbook05/test_phase3_dependency_bundle_validation.py
git commit -m "feat(workbook05): validate complete dependency bundles"
```

Omit `hash_manifest.py` from the commit if no shared change was required.

---

### Task 10: Add the dedicated three-boundary workflow

**Files:**
- Create: `.github/workflows/workbook-05-phase3-dependency-preflight.yml`.
- Create: `tests/testing/workbook05/test_phase3_dependency_preflight_workflow_contract.py`.

- [ ] **Step 1: Write the failing workflow contract**

Require jobs named:

```text
repository-contract
collect-dependencies
validate-dependencies
```

Require the collector condition to contain all three clauses:

```yaml
github.event_name == 'workflow_dispatch'
github.ref == 'refs/heads/main'
inputs.confirm_live_dependency_preflight == true
```

Also require exact runner labels, read-only permissions, 40-character action SHAs, credential-free checkouts, `cancel-in-progress: false`, same-revision checkout, same-attempt artifact identity, `--require-passed`, and no model/download/conversion tokens.

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight_workflow_contract
```

Expected: workflow file is missing.

- [ ] **Step 3: Implement the GitHub-hosted repository contract**

Use `windows-latest`, Python `3.12.10`, the pinned validation dependency directory, the complete Phase 3 gate, and focused live-collector simulation tests. This job has no self-hosted runner, no conversion environment, and no model path.

- [ ] **Step 4: Implement the manually confirmed Lenovo collector**

Use exact labels and a `240`-minute job timeout. Verify the fixed base Python, run the complete Phase 3 gate, then call the live collector. Upload the exact attempt directory with `if: ${{ always() }}` and artifact identity:

```text
workbook-05-phase3-dependency-preflight-${{ github.run_id }}-${{ github.run_attempt }}
```

The workflow supplies only repository root, run ID, attempt, and fixed Python path. It does not accept a workspace path, source commit, package version, model path, or command string as user input.

- [ ] **Step 5: Implement independent hosted validation**

The validator checks out the same exact revision, runs the complete repository gate, downloads the exact same-attempt artifact, and runs:

```text
python -m scripts.testing.workbook05.phase3.dependency_bundle_validation
--bundle-root <runner-temp path>
--repository-root <checkout>
--require-passed
--report <runner-temp report>
```

It never adds an artifact directory to `PATH` or `PYTHONPATH` and never invokes a file from the artifact.

- [ ] **Step 6: Run workflow contracts**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight_workflow_contract `
  tests.testing.workbook05.test_phase3_asset_workflow_contract `
  tests.testing.workbook05.test_build_workflow_contract
```

Expected: zero failures and the existing asset workflow remains unchanged.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/workbook-05-phase3-dependency-preflight.yml \
        tests/testing/workbook05/test_phase3_dependency_preflight_workflow_contract.py
git commit -m "ci(workbook05): add clean dependency preflight workflow"
```

---

### Task 11: Add operator guidance while preserving the live-asset-lock block

**Files:**
- Create: `docs/testing/workbook05/phase3-dependency-preflight-runbook.md`.
- Modify: `docs/testing/workbook05/phase3-c1-implementation-status.md`.
- Modify: `docs/testing/workbook05/phase3-asset-lock-runbook.md`.

- [ ] **Step 1: Write the dependency-preflight runbook**

Explain, in beginner-friendly language:

```text
what the preflight proves
what it does not prove
exact workflow and input
machine preparation
exact stage order
workspace/evidence locations
successful artifact contents
how to interpret Passed/Blocked/IntegrityFailure/InfrastructureInterrupted
how to preserve and inspect a failed attempt
how to independently hash the artifact and decision
how project-owner acceptance is recorded
```

State explicitly that success still does not enable `live-asset-lock` automatically.

- [ ] **Step 2: Update C1 status truthfully**

Change Task 6 from “deferred” to “implemented but not yet accepted live” only after all repository tests pass. Keep live asset locking blocked until a later separately reviewed binding change consumes an exact accepted decision digest and retained workspace.

- [ ] **Step 3: Cross-link the asset-lock runbook**

Replace any generic dependency-preflight instructions with a link to the dedicated runbook, but do not change its dispatch instructions or unblock the current `live-asset-lock` operation.

- [ ] **Step 4: Run documentation/security scans**

```powershell
git diff --check

git grep -n -I -E 'HF_TOKEN|HUGGING_FACE_HUB_TOKEN|Authorization: Bearer|BEGIN (RSA|OPENSSH|EC) PRIVATE KEY' -- `
  docs/testing/workbook05 `
  docs/superpowers
```

Expected: no whitespace error and no secret-like value.

- [ ] **Step 5: Commit**

```bash
git add docs/testing/workbook05/phase3-dependency-preflight-runbook.md \
        docs/testing/workbook05/phase3-c1-implementation-status.md \
        docs/testing/workbook05/phase3-asset-lock-runbook.md
git commit -m "docs(workbook05): document dependency preflight operation"
```

---

### Task 12: Integrate the gate, perform complete verification, and prepare PR review

**Files:**
- Modify: `scripts/testing/Validate-Workbook05-Phase3.ps1` only to include any new repository-controlled dependency-bundle fixture root in its forbidden-payload scan.
- Review all files changed by Tasks 1–11.
- Update Draft PR `#72` with exact implementation and verification context.

- [ ] **Step 1: Run focused tests first**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_dependency_lock_cli `
  tests.testing.workbook05.test_phase3_dependency_source_contract `
  tests.testing.workbook05.test_phase3_dependency_no_model_check `
  tests.testing.workbook05.test_phase3_dependency_import_check `
  tests.testing.workbook05.test_phase3_dependency_bundle_validation `
  tests.testing.workbook05.test_phase3_dependency_preflight_workflow_contract

& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1'
```

Expected: zero failures.

- [ ] **Step 2: Run the complete Phase 3 repository gate**

```powershell
& '.\scripts\testing\Validate-Workbook05-Phase3.ps1' `
    -RepositoryRoot (Get-Location).Path `
    -PythonPath 'C:\Program Files\Python312\python.exe'
```

Required final markers:

```text
WORKBOOK05_BUILD_STAGE_GATE_PASS
WORKBOOK05_PHASE3_GATE_PASS
```

- [ ] **Step 3: Run application regression**

Use the repository's exact README/build workflow commands; do not invent a different local recipe. At minimum, confirm the same restore/build/test boundary as `.github/workflows/build-and-test.yml` and retain the TRX.

- [ ] **Step 4: Run final repository hygiene checks**

```powershell
git diff --check
git status --short

git ls-files |
    Select-String -Pattern '\.(safetensors|bin|xml|onnx|exe|dll|lib|pyd|whl|zip|tar|gz|7z)$'

git grep -n -I -E 'Invoke-Expression|git reset --hard|git clean|cmd /c' -- `
  .github/workflows/workbook-05-phase3-dependency-preflight.yml `
  scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1
```

Expected: no whitespace/conflict errors, no newly tracked prohibited payload, and no forbidden execution/destructive pattern.

- [ ] **Step 5: Review exact scope against the approved design**

Confirm every design requirement is either:

```text
implemented and tested
explicitly retained as a non-claim
explicitly deferred to the later live-asset-lock binding change
```

Confirm no placeholder, TODO, fake hash, shortened commit, moving branch/tag, or unexplained source/version exists.

- [ ] **Step 6: Push and verify the exact PR head**

Require these GitHub checks on one exact current head:

```text
Build and test
Workbook 05 documented build
Workbook 05 Route A Runtime controlled resume contract
Workbook 05 Phase 3 assets — repository contract
Workbook 05 Phase 3 dependency preflight — repository contract
```

Do not rely on a green run from an earlier commit. Inspect the actual job logs and retained unit-test artifact where available.

- [ ] **Step 7: Update the detailed PR body**

The PR must explain:

```text
why a separate preflight exists
exact sources and versions
hash-lock/bootstrap method
source-metadata method
collector/validator trust boundaries
all changed files
all tests and exact counts
known warnings
what remains blocked
post-merge manual live-proof sequence
rollback/recovery behaviour
```

Keep the PR in Draft until the project owner approves the implementation boundary.

- [ ] **Step 8: Commit any final gate/documentation adjustment**

```bash
git add scripts/testing/Validate-Workbook05-Phase3.ps1
git commit -m "test(workbook05): close dependency preflight repository gate"
```

Skip this commit when the gate required no modification.

---

## Post-merge live proof — not part of implementation PR execution

After the exact implementation head is approved, merged, and a fresh `main` application regression passes, manually dispatch:

```text
Actions
→ Workbook 05 Phase 3 dependency preflight
→ Run workflow

Use workflow from: main
confirm_live_dependency_preflight: checked
```

The accepted result requires:

```text
repository contract: Passed
Lenovo collection: Passed
hosted untrusted-data validation: Passed
artifact SHA-256 independently recalculated
exact main commit recorded
decision.json SHA-256 independently recalculated
retained C:\w5c workspace identity recorded
project-owner acceptance explicitly recorded
all scientific authorisation flags false
```

A successful preflight still does not download Granite or enable live asset locking. A later, separate reviewed change must bind the accepted decision digest and retained workspace into the existing `live-asset-lock` gate.

## Professional references used for this plan

- pip installation report specification: `https://pip.pypa.io/en/latest/reference/installation-report/`
- pip secure/hash-checking installation guidance: `https://pip.pypa.io/en/stable/topics/secure-installs/`
- pip-tools reproducibility, environment, and `--generate-hashes` guidance: `https://pip-tools.readthedocs.io/en/stable/`
- GitHub Actions full-SHA policy: `https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-github-actions-settings-for-a-repository`
- GitHub self-hosted runner labels/routing: `https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job`
- GitHub workflow contexts and pre-routing job conditions: `https://docs.github.com/en/actions/reference/workflows-and-actions/contexts`
- Pinned Optimum Intel metadata: `huggingface/optimum-intel@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0/setup.py`
- Pinned Optimum metadata: `huggingface/optimum@982e495540364f95da1e4b6f62d2d4e5907d08fd/setup.py`

## Textbook basis

- *Designing Secure Software*, Chapters 2–4, 6–7, 10, and 12–13: trust boundaries, untrusted input, least functionality, secure defaults, fail-closed handling, and independent security validation.
- *The Art of Unit Testing*, Chapters 7–10: trustworthy/maintainable tests, test recipes, connected test levels, and delivery-pipeline confidence.
- *Why Programs Fail*, Chapters 3–6 and 13–16: reproduce the first causal divergence, preserve observations, isolate failure, distinguish infrastructure interruption, and verify the correction.
- *Code Complete*, Chapters 3, 8, 22–23, 28–29: upstream prerequisites, defensive programming, developer testing, retained records, configuration management, and incremental integration.
- *Refactoring*, Chapter 1: protect behaviour with self-checking tests and make the smallest safe structural change before adding behaviour.
- *Systems Engineering: Principles and Practice*, Chapters 13–17: reduce uncertainty through component qualification, staged integration, traceable Test and Evaluation, and evidence-based advancement.
- *Engineering Software Products*, Chapters 7–10: secure/reliable programming, automated testing, DevOps automation, and controlled code management.
