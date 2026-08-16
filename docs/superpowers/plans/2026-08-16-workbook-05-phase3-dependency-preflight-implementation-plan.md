# Workbook 05 Phase 3 Clean Dependency Preflight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Track progress by changing each `- [ ]` checkbox only after its stated verification has passed.

**Goal:** Implement a clean and reproducible Windows Python 3.12.10 dependency preflight for the reviewed IBM Granite 4.1 3B OpenVINO conversion toolchain, then validate the resulting text-only evidence independently without downloading, converting, loading, or executing a model.

**Architecture:** Add one dedicated GitHub Actions workflow with three boundaries: a GitHub-hosted repository contract, a manually confirmed Lenovo self-hosted collector, and a fresh GitHub-hosted validator. The collector creates one new `C:\w5c\dependency-preflight-<run-id>-<attempt>` workspace containing separate bootstrap and final virtual environments. It verifies immutable Optimum source trees, installs a committed hash-locked `pip-tools` bootstrap only in the bootstrap environment, generates a complete Windows/Python-specific ordinary-distribution lock, installs that lock into the untouched final environment, installs the two VCS projects from their verified local trees with dependency resolution and build isolation disabled, runs import/CLI/no-model checks, and writes `manifest.sha256` only after a schema-valid `Passed` decision. The existing Phase 3 offline fixture and blocked live asset-lock operation remain separate and unchanged.

**Tech Stack:** Python 3.12.10 standard library, `jsonschema==4.25.1`, Windows PowerShell 5.1, Git, pip, `pip-tools==7.5.0`, GitHub Actions, `unittest`, Draft 2020-12 JSON Schema, SHA-256 manifests.

## Approved boundaries

- Campaign: `GTQ-WB05-MF-v1`.
- Route: `route-a-merged-openvino`.
- Base interpreter: `C:\Program Files\Python312\python.exe`, exactly `Python 3.12.10`.
- Attempt root: `C:\w5c\dependency-preflight-<github.run_id>-<github.run_attempt>`.
- Existing attempt roots are rejected. They are never reused, repaired, reset, cleaned, or deleted.
- The preflight must not read from or write to `C:\w5m` except for constructing inert, never-opened path values in the repository-controlled argument-list test.
- Nothing under `C:\w5a` is modified.
- Pull requests may reach only the GitHub-hosted repository-contract job.
- The Lenovo collector requires `workflow_dispatch`, `refs/heads/main`, and `confirm_live_dependency_preflight == true` before runner assignment.
- Self-hosted labels are exactly `self-hosted`, `Windows`, `X64`, `workbook05`, and `intel-target`.
- Workflow permissions remain `contents: read` and `actions: read`.
- External actions are pinned to full 40-character commit SHAs; every checkout uses `persist-credentials: false`.
- `concurrency.cancel-in-progress` remains `false`.
- Every native command uses one explicit executable plus an ordered argument array. `Invoke-Expression`, `cmd /c`, generated shell command strings, and full inherited-environment capture are forbidden.
- Every native command has a finite deadline, concurrent stdout/stderr draining, a stable command ID, an allowlisted environment record, timestamps, exit code, and portable evidence paths.
- Only UTF-8 JSON, CSV, Markdown, logs, and SHA-256 manifests may enter the artifact.
- Model/tokenizer files, OpenVINO IR, source archives, wheels, executables, libraries, checkpoints, GGUF, ONNX, safetensors, ZIP/TAR/GZ/7z, symbolic links, junctions, and reparse payloads are forbidden.
- `manifest.sha256` is written last and only for a complete `Passed` attempt.
- Final JSON writes use a same-directory temporary file followed by an atomic move. A successful attempt leaves no `*.tmp` file.
- A failed or interrupted attempt preserves completed evidence and writes `failure.json`; it does not create a positive manifest or silently continue.
- All model and scientific authorisation flags remain `false`.
- The existing offline fixture script, Phase 3 asset workflow, asset-lock settings, accepted Runtime/GenAI evidence, Route B controls, and application production code remain unchanged.
- Every behaviour change follows RED → GREEN → refactor → focused commit.
- Comments explain each logical block and each security- or evidence-sensitive line.

## Exact reviewed dependency candidate

```text
optimum-intel @ git+https://github.com/huggingface/optimum-intel.git@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0
optimum @ git+https://github.com/huggingface/optimum.git@982e495540364f95da1e4b6f62d2d4e5907d08fd
transformers==5.5.0
huggingface-hub==1.21.0
nncf==3.2.0
openvino==2026.2.1
openvino-tokenizers==2026.2.1.0
```

The pinned `optimum-intel` source declares these runtime requirements:

```text
torch>=2.1
safetensors<0.8.0
optimum~=2.3.0
transformers>=4.51,<5.6
setuptools
huggingface-hub>=0.23.2,<1.22
nncf>=2.19.0
openvino>=2026.0
openvino-tokenizers>=2026.0
requests>=2.33,<3.0
```

The pinned `optimum` source declares:

```text
transformers>=4.29
torch>=1.11
packaging
numpy
huggingface_hub>=0.8.0
```

The ordinary lock input is therefore the exact five headline pins plus the reviewed non-VCS runtime/build requirements that are not already made stricter by those pins:

```text
transformers==5.5.0
huggingface-hub==1.21.0
nncf==3.2.0
openvino==2026.2.1
openvino-tokenizers==2026.2.1.0
torch>=2.1
safetensors<0.8.0
setuptools
requests>=2.33,<3.0
packaging
numpy
wheel
```

`optimum~=2.3.0` is deliberately excluded from the ordinary lock because the exact reviewed Optimum commit is installed separately from its verified local source tree. Tests must prove that the stronger exact pins satisfy the redundant source constraints and that no source requirement is silently omitted.

## Workspace and evidence layout

```text
C:\w5c\dependency-preflight-<run>-<attempt>\
  workspace\
    bootstrap-venv\
    environment\
    sources\
      optimum-intel\
      optimum\
    private-downloads\
    private-build\
    requirements.phase3-assets.in
  evidence\
    decision.json
    observation.json
    checks.json
    stage-order.json
    summary.md
    manifest.sha256                 # success only, written last
    failure.json                    # failed/interrupted attempts only
    locks\
      requirements.phase3-bootstrap.txt
      requirements.phase3-assets.txt
    reports\
      bootstrap-install-report.json
      bootstrap-packages.json
      source-contracts.json
      normal-install-report.json
      normal-packages.json
      vcs-packages.json
      final-environment-packages.json
      imports.json
      no-model-check.json
    sources\
      optimum.json
      optimum-intel.json
      optimum-files.csv
      optimum-intel-files.csv
    commands\
      command-index.json
    logs\
      *.command.json
      *.stdout.txt
      *.stderr.txt
      *.resources.json
      *.resources.csv
```

Source checkouts, package caches, wheel/build products, and virtual environments remain under `workspace` and never enter the artifact.

## File map

### New repository files

```text
.github/workflows/workbook-05-phase3-dependency-preflight.yml
scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1
scripts/testing/workbook05/requirements.phase3-bootstrap.in
scripts/testing/workbook05/requirements.phase3-bootstrap.txt
scripts/testing/workbook05/phase3/dependency_lock_cli.py
scripts/testing/workbook05/phase3/dependency_source_contract.py
scripts/testing/workbook05/phase3/dependency_no_model_check.py
tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
tests/testing/workbook05/test_phase3_dependency_lock_cli.py
tests/testing/workbook05/test_phase3_dependency_source_contract.py
tests/testing/workbook05/test_phase3_dependency_no_model_check.py
tests/testing/workbook05/test_phase3_dependency_import_check.py
tests/testing/workbook05/test_phase3_dependency_preflight_workflow_contract.py
docs/testing/workbook05/phase3-dependency-preflight-runbook.md
```

### Existing files to modify

```text
scripts/testing/workbook05/Workbook05.ControlledProcess.psm1
scripts/testing/workbook05/phase3/conversion.py
scripts/testing/workbook05/phase3/dependency_preflight.py
scripts/testing/workbook05/phase3/dependency_lock.py
scripts/testing/workbook05/phase3/dependency_preflight_cli.py
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

### Files explicitly unchanged

```text
scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflight.ps1
scripts/testing/workbook05/Invoke-Workbook05Phase3AssetLock.ps1
.github/workflows/workbook-05-phase3-assets.yml
experiments/granite_turboquant_intel/configurations/workbook05/phase3-asset-lock-settings.json
IBM Granite with TurboQuant (Intel)/**
```

---

### Task 1: Extend the closed decision contract for live bootstrap evidence and interruption semantics

**Files:**
- Modify: `scripts/testing/workbook05/phase3/dependency_preflight.py`.
- Modify: `scripts/testing/workbook05/phase3/dependency_lock.py`.
- Modify: `scripts/testing/workbook05/phase3/dependency_preflight_cli.py`.
- Modify: `experiments/granite_turboquant_intel/schemas/workbook05/conversion-dependency-preflight.schema.json`.
- Test: `tests/testing/workbook05/test_phase3_dependency_preflight.py`.
- Test: `tests/testing/workbook05/test_phase3_dependency_lock.py`.

**Contract additions:**

```python
DependencyCheck.status in {
    "Passed",
    "Failed",
    "Blocked",
    "IntegrityFailure",
    "InfrastructureInterrupted",
}
```

Add a required `bootstrap_lock` object to the decision:

```json
{
  "path": "locks/requirements.phase3-bootstrap.txt",
  "sha256": "64 lowercase hex characters",
  "install_report_path": "reports/bootstrap-install-report.json",
  "install_report_sha256": "64 lowercase hex characters",
  "normal_distribution_count": 0,
  "all_normal_artifacts_hashed": true
}
```

- [ ] **Step 1: Write failing regression tests**

Add tests proving:

```python
def test_interrupted_check_is_not_misreported_as_package_failure(self):
    checks = list(_checks())
    checks[1] = DependencyCheck(
        name="install",
        status="InfrastructureInterrupted",
        exit_code=-1,
    )
    record = _record(checks=tuple(checks))
    self.assertEqual("InfrastructureInterrupted", record["status"])


def test_bootstrap_lock_and_report_are_bound_into_decision(self):
    record = _record()
    self.assertEqual(
        "locks/requirements.phase3-bootstrap.txt",
        record["bootstrap_lock"]["path"],
    )
    self.assertRegex(record["bootstrap_lock"]["sha256"], r"^[0-9a-f]{64}$")
```

Also prove that a bootstrap lock/report digest mismatch is `IntegrityFailure`, every scientific flag stays false, and a Git-derived Optimum Intel local version matching `2.3.0.dev0+<commit-prefix>` is accepted only when its full source commit is independently correct.

- [ ] **Step 2: Run and verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight `
  tests.testing.workbook05.test_phase3_dependency_lock
```

Expected: failures for the missing `bootstrap_lock`, unsupported interruption status, and strict old Optimum Intel version rule.

- [ ] **Step 3: Implement explicit status precedence**

```python
if integrity_reasons:
    status = "IntegrityFailure"
elif any(check.status == "InfrastructureInterrupted" for check in checks):
    status = "InfrastructureInterrupted"
elif any(check.status != "Passed" for check in checks):
    status = "Blocked"
else:
    status = "Passed"
```

Integrity failure always outranks an interruption so identity drift cannot be hidden.

- [ ] **Step 4: Implement the exact source-derived version rule**

```python
_OPTIMUM_INTEL_VERSION = re.compile(
    r"^2\.3\.0\.dev0(?:\+[0-9a-f]{7,40})?$"
)
```

The version pattern supplements—never replaces—the full reviewed source commit check.

- [ ] **Step 5: Update schema and CLI input plumbing**

Add `bootstrap_lock` and `InfrastructureInterrupted` to the closed schema. Extend `build_dependency_preflight_record(...)` and its CLI observation input so bootstrap lock/report identities are recomputed from bytes rather than trusted as supplied summaries.

- [ ] **Step 6: Run focused/schema tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_schema_registry
```

Expected: zero failures.

- [ ] **Step 7: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_preflight.py \
        scripts/testing/workbook05/phase3/dependency_lock.py \
        scripts/testing/workbook05/phase3/dependency_preflight_cli.py \
        experiments/granite_turboquant_intel/schemas/workbook05/conversion-dependency-preflight.schema.json \
        tests/testing/workbook05/test_phase3_dependency_preflight.py \
        tests/testing/workbook05/test_phase3_dependency_lock.py
git commit -m "feat(workbook05): bind live dependency preflight identities"
```

---

### Task 2: Generalise hash-lock verification and commit the hash-locked bootstrap

**Files:**
- Create: `scripts/testing/workbook05/requirements.phase3-bootstrap.in`.
- Create: `scripts/testing/workbook05/requirements.phase3-bootstrap.txt`.
- Create: `scripts/testing/workbook05/phase3/dependency_lock_cli.py`.
- Create: `tests/testing/workbook05/test_phase3_dependency_lock_cli.py`.
- Modify: `scripts/testing/workbook05/phase3/dependency_lock.py`.
- Modify: `tests/testing/workbook05/test_phase3_dependency_lock.py`.

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
```

CLI:

```text
python -m scripts.testing.workbook05.phase3.dependency_lock_cli validate-lock ...
python -m scripts.testing.workbook05.phase3.dependency_lock_cli validate-report ...
```

- [ ] **Step 1: Write failing generic lock/report tests**

Require exact `name==version` pins, one or more SHA-256 hashes, canonical-name uniqueness, no URL/VCS/editable/index directive, and report archive digest equality. Preserve the existing rule that `optimum` and `optimum-intel` are forbidden in the ordinary lock/report.

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_dependency_lock_cli
```

Expected: import/signature failures for the generic API and CLI.

- [ ] **Step 3: Refactor behind compatibility wrappers**

Keep `parse_normal_install_report(...)` as a wrapper around the new generic implementation so existing callers and tests do not lose behaviour.

The CLI writes atomic JSON with the computed lock digest, package count, and actual package identities. It rejects an existing output or `*.tmp` file.

- [ ] **Step 4: Add the bootstrap input**

```text
pip-tools==7.5.0
```

- [ ] **Step 5: Generate the bootstrap lock in a disposable Python 3.12.10 environment**

The one-time repository-maintenance generation is not live acceptance evidence. Verify the exact official `pip-tools` 7.5.0 wheel digest before using it to generate the committed lock:

```powershell
$ErrorActionPreference = 'Stop'
$python = 'C:\Program Files\Python312\python.exe'
$expectedWheel = '69758e4e5a65f160e315d74db46246fdbb30d549f1ed0c4236d057122c9b0f18'
$root = Join-Path $env:TEMP ('wb05-bootstrap-lock-' + [Guid]::NewGuid().ToString('N'))
$download = Join-Path $root 'download'
$venv = Join-Path $root 'venv'
New-Item -ItemType Directory -Path $download -Force:$false | Out-Null

& $python -m pip download `
    --isolated `
    --disable-pip-version-check `
    --only-binary=:all: `
    --no-deps `
    --dest $download `
    'pip-tools==7.5.0'
if ($LASTEXITCODE -ne 0) { throw 'pip-tools wheel download failed.' }

$wheel = @(Get-ChildItem -LiteralPath $download -Filter '*.whl' -File)
if ($wheel.Count -ne 1) { throw 'Expected exactly one pip-tools wheel.' }
$actualWheel = (Get-FileHash -LiteralPath $wheel[0].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualWheel -ne $expectedWheel) { throw "Unexpected pip-tools wheel digest: $actualWheel" }

& $python -m venv $venv
$venvPython = Join-Path $venv 'Scripts\python.exe'
& $venvPython -m pip install `
    --isolated `
    --disable-pip-version-check `
    $wheel[0].FullName
if ($LASTEXITCODE -ne 0) { throw 'Disposable pip-tools installation failed.' }

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

The disposable generator is never accepted merely because it ran. The committed output must pass parser tests and a separate clean hash-enforced installation simulation before use.

- [ ] **Step 6: Validate the real committed bootstrap lock**

Require `pip-tools==7.5.0`, exact transitive pins, hashes for every distribution, no VCS/URL/editable/index directive, and deterministic LF text.

- [ ] **Step 7: Run focused tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_lock `
  tests.testing.workbook05.test_phase3_dependency_lock_cli
```

Expected: zero failures.

- [ ] **Step 8: Commit**

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

### Task 3: Validate pinned source metadata as data and synthesise the complete ordinary-lock input

**Files:**
- Modify: `scripts/testing/workbook05/phase3/conversion.py`.
- Create: `scripts/testing/workbook05/phase3/dependency_source_contract.py`.
- Create: `tests/testing/workbook05/test_phase3_dependency_source_contract.py`.

**Interfaces:**

```python
@dataclass(frozen=True)
class SourcePackageContract:
    name: str
    base_version: str
    runtime_requirements: tuple[str, ...]
    console_entry_points: tuple[str, ...]
    build_system_declared: bool

inspect_source_contract(name: str, root: Path) -> SourcePackageContract
validate_reviewed_source_contracts(
    optimum_root: Path,
    optimum_intel_root: Path,
) -> dict[str, object]
build_reviewed_normal_requirement_input() -> tuple[str, ...]
```

- [ ] **Step 1: Add exact reviewed constants**

Add these immutable catalogues to `conversion.py`:

```python
REVIEWED_OPTIMUM_CONSTRAINTS = (
    "transformers>=4.29",
    "torch>=1.11",
    "packaging",
    "numpy",
    "huggingface_hub>=0.8.0",
)

REVIEWED_NORMAL_REQUIREMENT_INPUT = (
    "transformers==5.5.0",
    "huggingface-hub==1.21.0",
    "nncf==3.2.0",
    "openvino==2026.2.1",
    "openvino-tokenizers==2026.2.1.0",
    "torch>=2.1",
    "safetensors<0.8.0",
    "setuptools",
    "requests>=2.33,<3.0",
    "packaging",
    "numpy",
    "wheel",
)
```

Retain the current exact Optimum Intel constraints and both exact console entry points.

- [ ] **Step 2: Write failing parser and synthesis tests**

Use temporary fixtures to prove exact extraction without execution. Reject dynamic/non-literal dependency construction, an unexpected `[build-system]`, changed entry point, changed version, missing source requirement, or additional source requirement.

Prove the ordinary input:

- contains every exact five direct pin;
- contains `torch`, `safetensors`, `setuptools`, `requests`, `packaging`, `numpy`, and `wheel`;
- excludes `optimum` and `optimum-intel`;
- contains no duplicate canonical package name;
- satisfies every reviewed source constraint either directly, through a stronger exact pin, or through the separately verified VCS Optimum source.

- [ ] **Step 3: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_source_contract
```

Expected: missing-module/constants failures.

- [ ] **Step 4: Implement an AST/TOML data-only parser**

Use `ast.parse` and `ast.literal_eval` for exact named list/tuple constants and `setup(...)` keyword arguments. Read version files directly. Use `tomllib` only to inspect `pyproject.toml`. Never import or execute `setup.py`.

```python
module = ast.parse(
    setup_path.read_text(encoding="utf-8"),
    filename=str(setup_path),
)
requirements = ast.literal_eval(requirement_assignment.value)
```

The source-contract report records:

```json
{
  "source_metadata_execution": false,
  "build_system_declared": false,
  "installer_build_mode": "setuptools-no-build-isolation",
  "reviewed_build_tools": ["setuptools", "wheel"]
}
```

- [ ] **Step 5: Implement deterministic requirements input writing**

Write the reviewed tuple exactly, one line per requirement, LF-terminated. Reject an existing output instead of overwriting it. Keep the generated input under the attempt workspace, not the repository.

- [ ] **Step 6: Run focused and conversion tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_source_contract `
  tests.testing.workbook05.test_phase3_conversion
```

Expected: zero failures.

- [ ] **Step 7: Commit**

```bash
git add scripts/testing/workbook05/phase3/conversion.py \
        scripts/testing/workbook05/phase3/dependency_source_contract.py \
        tests/testing/workbook05/test_phase3_dependency_source_contract.py
git commit -m "feat(workbook05): validate conversion source metadata"
```

---

### Task 4: Add the repository-controlled no-model compatibility check

**Files:**
- Create: `scripts/testing/workbook05/phase3/dependency_no_model_check.py`.
- Create: `tests/testing/workbook05/test_phase3_dependency_no_model_check.py`.

**Interface:**

```python
run_no_model_check(
    environment_root: Path,
    optimum_root: Path,
    optimum_intel_root: Path,
) -> dict[str, object]
```

- [ ] **Step 1: Write failing tests**

Prove that the check:

- validates both static source contracts;
- imports the installed conversion modules from the supplied final environment;
- verifies the exact installed direct package versions;
- constructs the exact conversion argument array through `ConversionRequest` and `build_optimum_argument_list`;
- sets `trust_remote_code=False`;
- rejects `--trust-remote-code` anywhere in the argument array;
- does not call a subprocess, open/test a model path, contact a network, or create an output directory;
- writes JSON atomically and leaves no temporary file.

Patch `subprocess`, socket creation, and model-path filesystem methods to raise if used.

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_no_model_check
```

Expected: missing-module failure.

- [ ] **Step 3: Implement the inert argument construction**

Use the actual `ConversionRequest` field names:

```python
request = ConversionRequest(
    optimum_cli=(
        environment_root / "Scripts" / "optimum-cli.exe"
    ),
    source_directory=Path(r"C:\w5m\sources\not-opened-source"),
    output_directory=Path(r"C:\w5m\converted\not-created-output"),
    trust_remote_code=False,
)
arguments = build_optimum_argument_list(request)
```

Those two `C:\w5m` values are inert strings required by the approved conversion-path contract. The check must not call `exists`, `resolve`, `open`, `mkdir`, or an external process on either value.

- [ ] **Step 4: Record exact observations**

The output includes installed module/package versions, source-contract result, ordered conversion arguments, `trust_remote_code=false`, `model_opened=false`, `network_contacted=false`, and `process_executed=false`.

- [ ] **Step 5: Run focused tests**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_no_model_check `
  tests.testing.workbook05.test_phase3_conversion
```

Expected: zero failures.

- [ ] **Step 6: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_no_model_check.py \
        tests/testing/workbook05/test_phase3_dependency_no_model_check.py
git commit -m "feat(workbook05): add no-model conversion compatibility check"
```

---

### Task 5: Extend the controlled process adapter narrowly for preflight evidence

**Files:**
- Modify: `scripts/testing/workbook05/Workbook05.ControlledProcess.psm1`.
- Modify: `tests/testing/workbook05/test_build_powershell_contract.py`.
- Exercise behaviour in: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.

**Interface additions:**

```powershell
Invoke-Wb05ControlledLoggedProcess `
    -Component 'dependency-preflight' `
    -LogFileExtension 'txt' `
    -AtomicJsonEvidence `
    -MaximumElapsedSeconds 900
```

- [ ] **Step 1: Write failing contract tests**

Require:

```text
'dependency-preflight' in the component ValidateSet
[string]$LogFileExtension = 'log'
[ValidateSet('log', 'txt')]
[switch]$AtomicJsonEvidence
```

The default must preserve existing Runtime/GenAI `.log` filenames.

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_powershell_contract
```

Expected: missing component/log/atomic-mode assertions.

- [ ] **Step 3: Implement the narrow extension**

When `-AtomicJsonEvidence` is present, write the command record and resource-summary JSON to `<final>.tmp`, then move within the same directory after successful serialization. Reject an existing final or temporary path. Logs and CSV remain streamed evidence.

Do not change quoting, process-tree discovery/termination, memory/commit safety stops, elapsed-time deadline, environment allowlisting, or existing return fields.

- [ ] **Step 4: Run existing behavioural tests**

```powershell
& '.\tests\testing\workbook05\Invoke-BuildProcessDeadlineTests.Tests.ps1'

& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_build_powershell_contract
```

Expected: the existing deadline test still passes and existing call sites still use `.log` by default.

- [ ] **Step 5: Commit**

```bash
git add scripts/testing/workbook05/Workbook05.ControlledProcess.psm1 \
        tests/testing/workbook05/test_build_powershell_contract.py
git commit -m "refactor(workbook05): admit controlled dependency commands"
```

---

### Task 6: Implement fresh workspace creation and immutable source verification

**Files:**
- Create: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1`.
- Create: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.

**Production entry point:**

```powershell
& '.\scripts\testing\workbook05\Invoke-Workbook05Phase3DependencyPreflightLive.ps1' `
    -RepositoryRoot $env:GITHUB_WORKSPACE `
    -RunId $env:GITHUB_RUN_ID `
    -RunAttempt $env:GITHUB_RUN_ATTEMPT `
    -BasePythonPath 'C:\Program Files\Python312\python.exe'
```

- [ ] **Step 1: Write failing simulation tests**

Use a hidden `-SimulationMode` seam with a temporary normal local root and deterministic command adapter. The workflow/static contract must forbid simulation parameters in production dispatch.

Test:

```text
fresh normal workspace accepted
existing workspace rejected without deletion
file/symlink/junction/reparse root rejected
UNC/device/parent-traversal path rejected
wrong source origin rejected
wrong source commit rejected
dirty source rejected
tracked link/reparse file rejected
source failure stops before bootstrap or install
```

- [ ] **Step 2: Verify RED**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'workspace-source'
```

Expected: live script missing.

- [ ] **Step 3: Implement fixed identity and stage ledger**

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

$runIdentity = "dependency-preflight-$RunId-$RunAttempt"
$attemptRoot = Join-Path 'C:\w5c' $runIdentity
```

Do not accept an operator-supplied production workspace, source revision, package version, model path, or command string.

- [ ] **Step 4: Implement immutable Git acquisition**

For each source, execute explicit argument-array commands equivalent to:

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

Never use `git reset --hard`, `git clean`, a branch, a tag, or implicit default-branch checkout.

Store the `git ls-files` text outside the source root so generating evidence cannot dirty the source.

- [ ] **Step 5: Generate complete source identities**

Call the existing `source_tree_manifest.py` with the verified tracked list to create both source JSON records and CSV manifests. The helper must reject a tracked link/reparse chain, duplicate/case-colliding path, empty tracked list, or file outside the source root.

- [ ] **Step 6: Run source simulations**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'workspace-source'
```

Expected: all cases pass and no later command is recorded after a failed source check.

- [ ] **Step 7: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1 \
        tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
git commit -m "feat(workbook05): verify fresh preflight sources"
```

---

### Task 7: Implement the separate bootstrap environment and target-specific lock generation

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1`.
- Modify: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.
- Use: `scripts/testing/workbook05/phase3/dependency_lock_cli.py`.
- Use: `scripts/testing/workbook05/phase3/dependency_source_contract.py`.

- [ ] **Step 1: Write failing bootstrap/lock simulations**

Require this exact causal order:

```text
verify committed bootstrap lock
create bootstrap-venv
install bootstrap lock with --require-hashes and --report
validate bootstrap report against bootstrap lock
validate source metadata as data
write complete ordinary requirements input
generate ordinary lock with --generate-hashes
validate ordinary lock
```

Reject an unhashed bootstrap member, bootstrap report drift, changed source metadata, missing ordinary runtime requirement, VCS package in ordinary input/lock, direct-version drift, URL/VCS/editable/index directive, and duplicate canonical name.

- [ ] **Step 2: Verify RED**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'bootstrap-lock'
```

Expected: bootstrap/lock stages missing.

- [ ] **Step 3: Create only the bootstrap environment**

```text
<BasePython> -m venv <attempt>/workspace/bootstrap-venv
<bootstrap-python> -m pip install
  --isolated
  --no-input
  --disable-pip-version-check
  --require-hashes
  --report <evidence>/reports/bootstrap-install-report.json
  -r <repository>/scripts/testing/workbook05/requirements.phase3-bootstrap.txt
```

Copy the committed bootstrap lock byte-for-byte into `evidence/locks` only after validating it. Set `TEMP`, `TMP`, and package cache paths to attempt-local private directories for the controlled command, then restore the previous process environment.

- [ ] **Step 4: Validate source metadata before resolution**

Run the data-only source contract helper with base Python and write `reports/source-contracts.json`. A mismatch is `IntegrityFailure`; it never causes automatic candidate/version adjustment.

- [ ] **Step 5: Write the complete ordinary input**

Write exactly `REVIEWED_NORMAL_REQUIREMENT_INPUT` to `workspace/requirements.phase3-assets.in`. Prove that each source requirement is covered and that `optimum`/`optimum-intel` remain separate VCS identities.

- [ ] **Step 6: Generate the target lock with bootstrap Python**

Use separate array items equivalent to:

```text
python -m piptools compile
--no-config
--resolver=backtracking
--generate-hashes
--strip-extras
--allow-unsafe
--no-emit-index-url
--no-emit-trusted-host
--output-file <evidence>/locks/requirements.phase3-assets.txt
<workspace>/requirements.phase3-assets.in
```

The bootstrap and final environments use the same Windows host and Python 3.12.10 base, but no bootstrap package enters the final environment.

- [ ] **Step 7: Validate generated lock before target installation**

Require exact five direct pins, all transitive entries exactly pinned and hashed, no VCS/URL/editable/index directive, and no `optimum`/`optimum-intel` distribution.

- [ ] **Step 8: Run simulations/focused tests**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'bootstrap-lock'

& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_lock_cli `
  tests.testing.workbook05.test_phase3_dependency_source_contract
```

Expected: zero failures.

- [ ] **Step 9: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1 \
        tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
git commit -m "feat(workbook05): generate target dependency lock"
```

---

### Task 8: Install the untouched final environment and execute the six no-model checks

**Files:**
- Modify: `scripts/testing/workbook05/phase3/dependency_import_check.py`.
- Create: `tests/testing/workbook05/test_phase3_dependency_import_check.py`.
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1`.
- Modify: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.

- [ ] **Step 1: Write failing import-boundary tests**

Add `--expected-environment-root` and reject any imported module whose resolved `__file__` is outside the final environment. Use temporary fake packages to prove accepted and escaped paths.

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_import_check
```

Expected: missing path enforcement.

- [ ] **Step 3: Create the untouched final environment after lock acceptance**

```text
<BasePython> -m venv <attempt>/workspace/environment
```

Verify Python 3.12.10. Record final Python and pip versions, paths, and executable SHA-256 values after environment creation. The decision’s Python/pip identity refers to this final environment, never the bootstrap environment.

- [ ] **Step 4: Install ordinary distributions with hash checking**

```text
<final-python> -m pip install
  --isolated
  --no-input
  --disable-pip-version-check
  --require-hashes
  --report <evidence>/reports/normal-install-report.json
  -r <evidence>/locks/requirements.phase3-assets.txt
```

Validate the report against the lock before VCS installation. Every actual archive digest must match an allowed lock digest.

- [ ] **Step 5: Install VCS packages from verified local trees**

Install in this order:

```text
optimum
optimum-intel
```

Each command includes exactly:

```text
--no-deps
--no-build-isolation
```

It uses attempt-local `TEMP`, `TMP`, and cache paths. It contains no VCS URL, branch, tag, index override, or remote-code option. The previously locked `setuptools` and `wheel` satisfy the reviewed local build boundary.

- [ ] **Step 6: Verify final package inventory**

Write `normal-packages.json`, `vcs-packages.json`, and `final-environment-packages.json`. The final environment may contain only:

```text
pip itself
ordinary distributions from the generated lock
optimum from the reviewed local commit
optimum-intel from the reviewed local commit
```

Any other distribution is `IntegrityFailure`. The two VCS distributions must not occur in the ordinary lock/report.

- [ ] **Step 7: Run `pip check`**

A non-zero result makes the `install` check `Blocked`; it must not be rewritten as a resolver success.

- [ ] **Step 8: Run the exact six checks in order**

```text
resolver
install
imports
cli_help
no_model_compatibility
remote_code_disabled
```

- `imports`: new final-Python process imports exactly `optimum`, `optimum.intel`, `transformers`, `nncf`, and `openvino`; every module file stays under the final environment.
- `cli_help`: exact final `optimum-cli.exe --help`, exit code `0` required.
- `no_model_compatibility`: run the Task 4 helper with final Python, verified source roots, and final environment root.
- `remote_code_disabled`: derive from the structured no-model record only; require `trust_remote_code=false` and no `--trust-remote-code` argument.

- [ ] **Step 9: Run install/check simulations**

Cover normal report drift, unexpected final package, missing `--no-deps`, missing `--no-build-isolation`, `pip check` failure, escaped import, CLI failure, no-model failure, and remote-code drift.

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'install-checks'

& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_import_check `
  tests.testing.workbook05.test_phase3_dependency_no_model_check
```

Expected: zero failures.

- [ ] **Step 10: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_import_check.py \
        tests/testing/workbook05/test_phase3_dependency_import_check.py \
        scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1 \
        tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
git commit -m "feat(workbook05): qualify final conversion environment"
```

---

### Task 9: Finalise atomic success/failure evidence and write the manifest last

**Files:**
- Modify: `scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1`.
- Modify: `tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1`.

- [ ] **Step 1: Write failing finalisation tests**

Cover:

```text
success creates observation, checks, stage order, command index, summary, decision
success writes manifest after every other final file
success leaves no *.tmp
Blocked decision produces no acceptance manifest
IntegrityFailure produces failure.json and no manifest
controlled deadline/safety termination becomes InfrastructureInterrupted
first causal message is preserved
completed stages are preserved
existing final or temporary record is never overwritten
```

- [ ] **Step 2: Verify RED**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1' `
    -FocusedArea 'finalisation'
```

Expected: finalisation assertions fail.

- [ ] **Step 3: Implement private atomic JSON writing**

```powershell
function Write-PreflightAtomicJson {
    param([string]$Path, [object]$Value)

    $temporary = $Path + '.tmp'
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        throw "Final evidence already exists: $Path"
    }
    if (Test-Path -LiteralPath $temporary) {
        throw "Temporary evidence already exists: $temporary"
    }
    Write-Wb05Json -Path $temporary -Value $Value
    [IO.File]::Move($temporary, $Path)
}
```

Use it for collector-owned JSON. Controlled command JSON uses Task 5 atomic mode.

- [ ] **Step 4: Build a recomputable observation and decision**

`observation.json` contains raw identities/relationships required by `build_dependency_preflight_record(...)`, including bootstrap lock/report bytes, normal lock/report bytes, VCS packages, source trees, checks, final Python/pip identity, and exact direct requirements. `decision.json` is produced by the repository CLI and validated against the closed schema.

- [ ] **Step 5: Build the command index and stage ledger**

Require unique command IDs and portable paths to each command record, stdout, stderr, resource summary, and resource CSV. `stage-order.json` contains the fixed full order plus the completed prefix.

- [ ] **Step 6: Implement failure classification**

`failure.json` contains:

```text
schema_version
campaign_id
record_type = dependency-preflight-failure
route_id
run_id
run_attempt
workspace_root
current_stage
completed_stages
classification = Blocked | IntegrityFailure | InfrastructureInterrupted
first_causal_message
recorded_at_utc
all six scientific authorisation flags = false
```

A controlled process deadline, memory/commit safety stop, runner cancellation evidence, or externally terminated process is not called a package incompatibility. An identity mismatch remains `IntegrityFailure`.

- [ ] **Step 7: Write `manifest.sha256` only after `Passed`**

Invoke the existing hash-manifest module only after every final record exists and `decision.status == 'Passed'`. No subsequent command may mutate the bundle.

- [ ] **Step 8: Run complete PowerShell simulations**

```powershell
& '.\tests\testing\workbook05\Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1'
```

Expected: every success/failure/interruption scenario passes without network or model access.

- [ ] **Step 9: Commit**

```bash
git add scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1 \
        tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
git commit -m "feat(workbook05): finalise dependency preflight evidence"
```

---

### Task 10: Harden independent validation for the complete live bundle

**Files:**
- Modify: `scripts/testing/workbook05/phase3/dependency_bundle_validation.py`.
- Modify: `tests/testing/workbook05/test_phase3_dependency_bundle_validation.py`.
- Modify only when shared hardening is required: `scripts/testing/workbook05/hash_manifest.py`.

- [ ] **Step 1: Expand the valid fixture first**

Build one complete valid live bundle matching the layout above. Confirm RED because the current validator does not validate bootstrap/final-inventory/command-index/source-contract relationships.

- [ ] **Step 2: Add named adversarial tests**

Reject independently:

```text
missing required success member
changed, added, missing, malformed, or duplicate manifest member
parent traversal, backslash, absolute, or case-colliding path
secret/token/private-key pattern
symlink, junction, reparse, or non-regular member
forbidden suffix in any letter case
bootstrap lock/report mismatch
normal lock/report mismatch
unexpected final environment distribution
VCS package in ordinary lock/report
wrong VCS origin or commit
source CSV duplicate/case collision
source CSV hash/size/path drift
source JSON file_count or aggregate drift
source-contract drift
check order/status drift
command-index duplicate/missing/unsafe record
command record pointing to missing stdout/stderr/resource evidence
stage-order drift
unrecomputable decision
non-Passed decision under --require-passed
any true scientific authorisation
```

- [ ] **Step 3: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_bundle_validation
```

Expected: new cases fail for missing validation.

- [ ] **Step 4: Implement read-only validation**

The validator may enumerate, `lstat`, read, parse, hash, and compare. It must never import, execute, dynamically load, or shell an artifact member.

Recompute source aggregate hashes from CSV rows using the existing tree-hash record format:

```python
line = f"{relative_path}\0{size_bytes}\0{sha256}\n".encode("utf-8")
aggregate.update(line)
```

Validate both locks/reports through the generic lock validator. Rebuild the decision from `observation.json` and require exact object equality with `decision.json`.

- [ ] **Step 5: Validate the final environment set**

Require the final package inventory to equal pip plus the union of ordinary locked distributions and two exact VCS packages. A package omitted from reports or introduced by local install fails closed.

- [ ] **Step 6: Run focused/full Python suites**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_bundle_validation

& 'C:\Program Files\Python312\python.exe' -m unittest discover -v `
  -s tests/testing/workbook05 -p 'test_*.py'
```

Expected: zero failures.

- [ ] **Step 7: Commit**

```bash
git add scripts/testing/workbook05/phase3/dependency_bundle_validation.py \
        tests/testing/workbook05/test_phase3_dependency_bundle_validation.py
git commit -m "feat(workbook05): validate complete dependency bundles"
```

Add `scripts/testing/workbook05/hash_manifest.py` only if a tested shared hardening change was actually required.

---

### Task 11: Add the dedicated three-boundary workflow

**Files:**
- Create: `.github/workflows/workbook-05-phase3-dependency-preflight.yml`.
- Create: `tests/testing/workbook05/test_phase3_dependency_preflight_workflow_contract.py`.

- [ ] **Step 1: Write the failing workflow contract**

Require jobs:

```text
repository-contract
collect-dependencies
validate-dependencies
```

Require the collector condition to contain:

```yaml
github.event_name == 'workflow_dispatch'
github.ref == 'refs/heads/main'
inputs.confirm_live_dependency_preflight == true
```

Also require exact labels, read-only permissions, full action SHAs, credential-free checkouts, `cancel-in-progress: false`, exact-revision checkout, same-attempt artifact identity, `--require-passed`, and absence of model download/conversion execution.

- [ ] **Step 2: Verify RED**

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest -v `
  tests.testing.workbook05.test_phase3_dependency_preflight_workflow_contract
```

Expected: workflow missing.

- [ ] **Step 3: Implement the hosted repository contract**

Use `windows-latest`, Python 3.12.10, repository-pinned `jsonschema` outside the checkout, focused simulation tests, and the complete Phase 3 gate. This job has no self-hosted access, conversion environment, or model path.

- [ ] **Step 4: Implement the confirmed Lenovo collector**

Use exact labels and a 240-minute timeout. Re-verify fixed base Python, run the complete Phase 3 gate, then call the live collector. Upload the exact attempt evidence with `if: ${{ always() }}` and identity:

```text
workbook-05-phase3-dependency-preflight-${{ github.run_id }}-${{ github.run_attempt }}
```

The workflow supplies only repository root, run ID, attempt, and fixed Python path. It never accepts a workspace, source revision, dependency version, model path, or command string from the operator.

- [ ] **Step 5: Implement independent hosted validation**

Use a fresh hosted Windows runner, the same exact repository revision, and the exact same-attempt artifact. Run:

```text
python -m scripts.testing.workbook05.phase3.dependency_bundle_validation
--bundle-root <runner-temp bundle>
--repository-root <checkout>
--require-passed
--report <runner-temp report>
```

Never add the artifact to `PATH` or `PYTHONPATH`; never invoke a file from it.

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

### Task 12: Add operator guidance, close the repository gate, and verify one exact PR head

**Files:**
- Create: `docs/testing/workbook05/phase3-dependency-preflight-runbook.md`.
- Modify: `docs/testing/workbook05/phase3-c1-implementation-status.md`.
- Modify: `docs/testing/workbook05/phase3-asset-lock-runbook.md`.
- Modify only if needed: `scripts/testing/Validate-Workbook05-Phase3.ps1`.
- Review every file changed in Tasks 1–11.

- [ ] **Step 1: Write the beginner-friendly runbook**

Explain what the preflight proves and does not prove, exact workflow input, machine preparation, stage order, workspace/evidence locations, successful artifact contents, status meanings, failure preservation, independent artifact/decision hashing, and project-owner acceptance.

State clearly that a successful preflight still does not enable `live-asset-lock` automatically.

- [ ] **Step 2: Update C1 status truthfully**

Describe dependency-preflight repository implementation as complete only after all repository tests pass. Live acceptance remains pending until the manual `main` run, hosted validation, independent rehash, and owner acceptance. Keep live asset locking blocked until a separate binding change consumes the exact accepted decision and retained workspace.

- [ ] **Step 3: Cross-link the asset-lock runbook**

Point its dependency section to the dedicated runbook without changing current dispatch instructions or removing the existing live block.

- [ ] **Step 4: Extend the Phase 3 forbidden-payload scan only if required**

If new repository fixture roots are introduced, include them in `Validate-Workbook05-Phase3.ps1`. Do not weaken or replace the established BuildStage gate.

- [ ] **Step 5: Run focused tests**

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

- [ ] **Step 6: Run the complete Phase 3 gate**

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

- [ ] **Step 7: Run the exact application regression recipe**

Follow the repository README and `.github/workflows/build-and-test.yml`; do not invent a different build/test route. Retain and inspect the actual TRX rather than relying only on a green status.

- [ ] **Step 8: Run final hygiene/security checks**

```powershell
git diff --check
git status --short

git ls-files |
    Select-String -Pattern '\.(safetensors|bin|xml|onnx|exe|dll|lib|pyd|whl|zip|tar|gz|7z)$'

git grep -n -I -E 'Invoke-Expression|git reset --hard|git clean|cmd /c' -- `
  .github/workflows/workbook-05-phase3-dependency-preflight.yml `
  scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1

git grep -n -I -E 'HF_TOKEN|HUGGING_FACE_HUB_TOKEN|Authorization: Bearer|BEGIN (RSA|OPENSSH|EC) PRIVATE KEY' -- `
  docs/testing/workbook05 `
  docs/superpowers `
  scripts/testing/workbook05
```

Expected: no whitespace/conflict errors, no newly tracked prohibited payload, no forbidden execution/destructive pattern, and no secret-like value.

- [ ] **Step 9: Review every approved requirement**

Classify every design point as:

```text
implemented and tested
retained explicitly as a non-claim
or deferred explicitly to the later live-asset-lock binding change
```

Reject any placeholder, TODO, fake digest, shortened controlling commit, moving branch/tag, unexplained version, or undocumented deviation.

- [ ] **Step 10: Verify the exact current PR head in GitHub**

Require these checks on one exact SHA:

```text
Build and test
Workbook 05 documented build
Workbook 05 Route A Runtime controlled resume contract
Workbook 05 Phase 3 assets — repository contract
Workbook 05 Phase 3 dependency preflight — repository contract
```

A green run from an earlier commit is not transferable. Inspect job logs and the retained unit-test artifact where available.

- [ ] **Step 11: Update Draft PR #72 with full context**

Document exact sources/versions, two-environment bootstrap design, lock/input derivation, trust boundaries, every changed file, RED/GREEN evidence, final test counts, known warnings, what remains blocked, failure recovery, and the post-merge live-proof sequence.

Keep the PR Draft until the project owner approves the implemented boundary.

- [ ] **Step 12: Commit documentation/gate closure**

```bash
git add docs/testing/workbook05/phase3-dependency-preflight-runbook.md \
        docs/testing/workbook05/phase3-c1-implementation-status.md \
        docs/testing/workbook05/phase3-asset-lock-runbook.md \
        scripts/testing/Validate-Workbook05-Phase3.ps1
git commit -m "test(workbook05): close dependency preflight repository gate"
```

Omit `Validate-Workbook05-Phase3.ps1` when no gate modification was needed.

---

## Post-merge live proof — explicitly outside implementation execution

After the exact implementation head is approved, merged, and a fresh `main` application regression passes, manually dispatch:

```text
Actions
→ Workbook 05 Phase 3 dependency preflight
→ Run workflow

Use workflow from: main
confirm_live_dependency_preflight: checked
```

Acceptance requires:

```text
repository contract Passed
Lenovo collection Passed
hosted untrusted-data validation Passed
exact main commit recorded
artifact SHA-256 independently recalculated
GitHub artifact digest matched
exact decision.json SHA-256 independently recalculated
retained C:\w5c workspace identity recorded
project-owner acceptance explicitly recorded
all model/scientific authorisation flags false
```

A successful preflight still does not download Granite, run conversion, or enable live asset locking. A later separate reviewed change must bind the accepted decision digest and retained workspace into the existing `live-asset-lock` gate.

## Professional references used to verify this plan

- pip installation-report specification: used only as observed install evidence, not as a lock authority.
- pip secure/hash-checking installation guidance: all ordinary artifacts must be exactly pinned and hash admitted.
- pip-tools reproducibility guidance: lock generation occurs for the same Windows/Python target and uses `--generate-hashes`.
- GitHub Actions repository policy guidance: third-party actions remain pinned to complete commit SHAs.
- GitHub workflow-context guidance: the job-level manual-main-confirmation condition is evaluated before self-hosted runner routing.
- Exact pinned `optimum-intel` and `optimum` `setup.py`/`pyproject.toml` contents: used to define the source contracts, runtime requirements, entry point, version handling, and no-build-system condition.

## Textbook basis

- *Designing Secure Software*, Chapters 2–4, 6–7, 10, and 12–13: explicit trust boundaries, least functionality, secure defaults, untrusted-input validation, fail-closed evidence, and independent checking.
- *The Art of Unit Testing*, Chapters 7–10: trustworthy tests, maintainability, connected test levels, test recipes, and delivery-pipeline confidence.
- *Why Programs Fail*, Chapters 3–6 and 13–16: reproduce the first causal divergence, preserve observations, isolate failure, distinguish infrastructure interruption, and verify the correction.
- *Code Complete*, Chapters 3, 8, 22–23, and 28–29: upstream prerequisites, defensive programming, developer testing, retained records, configuration management, and incremental integration.
- *Refactoring*, Chapter 1: protect behaviour with self-checking tests and make the smallest safe structural change before adding behaviour.
- *Systems Engineering: Principles and Practice*, Chapters 13–17: reduce uncertainty through component qualification, staged integration, traceable Test and Evaluation, and evidence-based advancement.
- *Engineering Software Products*, Chapters 7–10: secure/reliable programming, automated testing, DevOps automation, and controlled code management.
