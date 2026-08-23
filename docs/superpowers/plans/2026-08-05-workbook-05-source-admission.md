# Workbook 05 Staged Source-Admission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` task by task. Every behaviour change follows red, green, refactor and ends in a focused commit.

**Goal:** Implement and execute `phase-1-source-admission` for campaign `GTQ-WB05-MF-v1`, while making performance and quality evidence mandatory for every later measured inference run.

**Architecture:** A read-only Intel self-hosted job verifies exact OpenVINO sources under `C:\wb05`, captures source and toolchain evidence, audits Route B test discovery, and performs a Route A configure-only CMake probe. A separate GitHub-hosted Windows job validates the artifact as untrusted data. Phase 1 does not compile OpenVINO or run a model.

**Tech stack:** Python `3.12.10`, Windows PowerShell `5.1`, `jsonschema==4.25.1`, Git `2.53.0.windows.3`, CMake `4.3.1-msvc1`, Visual Studio Build Tools 2022, Windows SDK `10.0.28000.0`, JSON Schema Draft 2020-12, GitHub Actions, SHA-256.

## Global constraints

- Branch: `testing/workbook-05-source-admission`.
- Campaign: `GTQ-WB05-MF-v1`.
- Phase: `phase-1-source-admission`.
- Route A: `route-a-merged-openvino`.
- Route B: `route-b-experimental-qjl-polar`.
- Route A Runtime: `openvinotoolkit/openvino` at `b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- Route A GenAI: `openvinotoolkit/openvino.genai` at `05e5c7670b597746f858946974d11f38e3baf42f`.
- Route B Runtime: `EgorDuplensky/openvino` at `1827f6458d049de11c1a8203c793af67c99935dc`, base `7de5a4fbb178a1de43f6bc3cccc95ff656abed05`.
- Admission states are exactly `Candidate`, `Admitted`, and `Blocked`.
- Route B is always labelled experimental.
- `RB-SRC-001` remains open until generated evidence proves the intended test instances are included or a separately reviewed correction resolves it.
- External source and configure data stay under `C:\wb05`; no external source, binary, archive, wheel, model, or build output is committed or uploaded.
- Route A and Route B use separate source, build, install, temporary, and evidence directories.
- The Intel runner has only `contents: read` and `actions: read`.
- Fork pull requests cannot execute the Intel job.
- Actions are pinned to immutable SHAs and checkout uses `persist-credentials: false`.
- Phase 1 may run CMake generation only for Route A. It may not run `cmake --build`, install, package, model download, inference, perplexity, performance measurement, or quality scoring.
- Route B CMake configuration is forbidden while `RB-SRC-001` remains open.
- Intel Python is `C:\Program Files\Python312\python.exe`, version `3.12.10`.
- CMake is `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe`, version `4.3.1-msvc1`.
- Route A configure uses `Visual Studio 17 2022`, `x64`, Release-compatible generation, and `-DENABLE_INTEL_GPU=OFF`.
- Source presence is reported as `Present in source`, never `Executable`, `Activated`, or `Supported`.
- Quality controls are `GTQ-PROMPTS-v1` and `GTQ-QUALITY-RUBRIC-v1`.
- Weight quantisation and KV-cache compression remain separate comparison axes.
- Later successful measured runs are invalid without raw output, activation proof, fallback result, separate K/V allocation, performance metrics, resource metrics, deterministic quality checks, five rubric dimensions, caps, paired baseline, and quality delta.

## Files

```text
.github/workflows/workbook-05-source-admission.yml

docs/superpowers/specs/2026-08-05-workbook-05-quality-metrics-amendment.md
docs/testing/Decision-Log.md

experiments/granite_turboquant_intel/configurations/workbook05/
  measurement-controls.json
  source-admission-settings.json

experiments/granite_turboquant_intel/manifests/templates/workbook05/
  measured-run-manifest-template.json
  measurement-controls-report-template.json
  source-tree-report-template.json
  source-capability-report-template.json
  cmake-test-discovery-report-template.json
  configure-probe-report-template.json
  source-admission-summary-template.json

experiments/granite_turboquant_intel/schemas/workbook05/
  measured-run-manifest.schema.json
  measurement-controls-report.schema.json
  source-tree-report.schema.json
  source-capability-report.schema.json
  cmake-test-discovery-report.schema.json
  configure-probe-report.schema.json
  source-admission-summary.schema.json

scripts/testing/workbook05/
  measurement_controls.py
  workspace_policy.py
  source_verification.py
  source_capability.py
  cmake_test_discovery.py
  configure_probe.py
  source_admission_phase.py
  source_admission_bundle_validation.py
  Workbook05.SourceAdmission.psm1
  Invoke-Workbook05SourceAdmission.ps1

scripts/testing/Validate-Workbook05-SourceAdmission.ps1

tests/testing/workbook05/
  test_measurement_controls.py
  test_source_admission_settings.py
  test_workspace_policy.py
  test_source_verification.py
  test_source_capability.py
  test_cmake_test_discovery.py
  test_configure_probe.py
  test_source_admission_phase.py
  test_source_admission_bundle.py
  test_source_workflow_contract.py
  Invoke-SourceAdmissionModuleTests.ps1
  fixtures/source-admission/
```

## Stable interfaces

```python
@dataclass(frozen=True)
class MeasurementControlIssue:
    code: str
    path: str
    message: str


def capture_measurement_controls(
    repository_root: Path,
    configuration_path: Path,
    destination: Path,
) -> dict[str, Any]:
    ...


def validate_measurement_controls(
    report: Mapping[str, Any],
) -> list[MeasurementControlIssue]:
    ...


@dataclass(frozen=True)
class WorkspaceDecision:
    permitted: bool
    canonical_path: str
    reasons: tuple[str, ...]


def evaluate_workspace_path(
    requested: PureWindowsPath,
    allowed_root: PureWindowsPath = PureWindowsPath("C:/wb05"),
) -> WorkspaceDecision:
    ...


@dataclass(frozen=True)
class SourceTreeResult:
    report: dict[str, Any]
    command_records: tuple[dict[str, Any], ...]


def verify_source_tree(
    specification: Mapping[str, Any],
    source_directory: Path,
    evidence_directory: Path,
    timeout_seconds: int,
) -> SourceTreeResult:
    ...


@dataclass(frozen=True)
class CapabilityFinding:
    capability_id: str
    classification: str
    source_path: str
    matched_tokens: tuple[str, ...]
    missing_tokens: tuple[str, ...]
    contradictory_tokens: tuple[str, ...]
    sha256: str


def inspect_route_capabilities(
    source_root: Path,
    requirements: Sequence[Mapping[str, Any]],
) -> list[CapabilityFinding]:
    ...


@dataclass(frozen=True)
class CMakeDiscoveryDecision:
    blocker_confirmed: bool
    intended_sources: tuple[str, ...]
    final_sources: tuple[str, ...]
    omitted_sources: tuple[str, ...]
    reasons: tuple[str, ...]


def audit_target_per_test(
    cmake_text: str,
    test_root: Path,
    class_file_name: str,
    generated_metadata_text: str,
) -> CMakeDiscoveryDecision:
    ...


@dataclass(frozen=True)
class ConfigureProbeDecision:
    passed: bool
    command: tuple[str, ...]
    cache_values: Mapping[str, str]
    reasons: tuple[str, ...]


def build_route_a_configure_command() -> tuple[str, ...]:
    ...


def evaluate_route_a_configure_probe(
    command: Sequence[str],
    exit_code: int,
    cmake_cache_text: str,
) -> ConfigureProbeDecision:
    ...


@dataclass(frozen=True)
class PhaseDecision:
    route_a_status: str
    route_b_status: str
    checkpoint_status: str
    reasons: tuple[str, ...]


def calculate_phase_decision(
    route_a_record: Mapping[str, Any],
    route_b_record: Mapping[str, Any],
    measurement_report: Mapping[str, Any],
) -> PhaseDecision:
    ...
```

PowerShell functions:

```powershell
Test-Workbook05ExternalWorkspace -WorkspaceRoot 'C:\wb05'

Invoke-Workbook05RecordedCommand `
    -FilePath 'C:\Program Files\Git\cmd\git.exe' `
    -ArgumentList @('--version') `
    -WorkingDirectory 'C:\wb05' `
    -EvidenceDirectory 'C:\wb05\evidence\contract-test' `
    -CommandId 'contract-git-version' `
    -TimeoutSeconds 60
```

---

### Task 1: Make performance and quality evidence mandatory

**Create:** measurement configuration, measured-run schema/template, measurement report schema/template, Python validator, and `test_measurement_controls.py`.  
**Modify:** `docs/testing/Decision-Log.md`.

- [ ] Write a failing test that requires separate `k_cache_allocated_bytes` and `v_cache_allocated_bytes` plus these quality fields: `raw_output_path`, `raw_output_sha256`, `deterministic_checks_path`, `deterministic_failures`, `dimension_scores`, `critical_caps`, `score_0_to_10`, `matched_baseline_run_id`, `paired_score_delta`, `judge_label_hidden`, `pairwise_order`, `adjudication_path`, and `material_degradation`.
- [ ] Write a failing test that hashes and validates `docs/testing/Metric-Definitions.md`, `GTQ-PROMPTS-v1`, `GTQ-QUALITY-RUBRIC-v1`, and the quality amendment.
- [ ] Run:

```powershell
& 'C:\Program Files\Python312\python.exe' -m unittest `
  tests.testing.workbook05.test_measurement_controls -v
```

Expected: import/file failure.

- [ ] Create `measurement-controls.json` with exact control paths, required prompt IDs `P1` through `P6`, and required metric names from `Metric-Definitions.md`.
- [ ] Implement a Draft 2020-12 measured-run schema with `additionalProperties: false`. A `Passed` run must include every applicable metric key; unavailable numeric observations are `null` only when a non-empty missing-data code explains them.
- [ ] Require model hash, tokenizer hash, weight quantisation, prompt ID, matched baseline, requested and verified K/V codecs, activation proof, fallback proof, raw output hash, five rubric dimensions, deterministic failures, caps, paired delta, and material-degradation classification.
- [ ] Implement `measurement_controls.py` to validate IDs, P1-P6, rubric weights summing to `1.0` within `1e-9`, file hashes, and the strict measured-run template.
- [ ] Append `TD-014`: quality and performance are separate first-class outcomes; matched comparisons hold weight quantisation fixed; incomplete evidence cannot pass.
- [ ] Run the focused test and the full Workbook 05 Python suite.
- [ ] Commit:

```text
test(workbook-05): require complete performance and quality evidence
```

### Task 2: Define source-admission settings and schemas

- [ ] Write `test_source_admission_settings.py` first. It must assert `C:\wb05`, the three exact repository commits, `ENABLE_INTEL_GPU=OFF`, timeouts, minimum free space `85899345920`, and `allow_route_b_configure_while_blocked=false`.
- [ ] Run the focused test and confirm missing-file failures.
- [ ] Create `source-admission-settings.json` with separate Route A Runtime, Route A GenAI, and Route B directories.
- [ ] Add exact Route A source requirements from PR #35853 and exact Route B source requirements from PR #35092.
- [ ] Include Route B’s `not yet supported` QJL comment as a contradictory token; do not hide it behind enum presence.
- [ ] Create strict schemas/templates for source trees, source capabilities, CMake discovery, configure probe, and final summary. Configure reports require `build_invoked=false`, `install_invoked=false`, and `package_invoked=false`.
- [ ] Validate every template against its schema and commit:

```text
test(workbook-05): define source-admission evidence contracts
```

### Task 3: Enforce the external workspace boundary

- [ ] Write Python tests for exact root, child paths, another drive, UNC, device paths, repository paths, and parent traversal.
- [ ] Write PowerShell tests proving a normal directory is accepted, a file is rejected, a reparse point is rejected, and unexpected existing data is not deleted.
- [ ] Implement `evaluate_workspace_path` using `PureWindowsPath`; reject traversal before normalisation.
- [ ] Implement `Test-Workbook05ExternalWorkspace`; reject `ReparsePoint` and create only known campaign directories.
- [ ] Implement `Invoke-Workbook05RecordedCommand` with `System.Diagnostics.Process`, argument-list invocation, separate stdout/stderr, timestamps, timeout, process-tree termination, and JSON command records. Never use `Invoke-Expression`.
- [ ] Run Python and PowerShell tests and commit:

```text
feat(workbook-05): enforce the external source workspace boundary
```

### Task 4: Verify or clone exact Git sources and submodules

- [ ] Build temporary local Git fixtures first and test exact origin/head, dirty tree, wrong origin, non-repository directory, malformed SHA, and recursive submodules.
- [ ] Implement lowercase 40-character SHA validation and repository allowlisting.
- [ ] For a missing Route A Runtime directory, execute and record exactly:

```text
git clone --no-checkout https://github.com/openvinotoolkit/openvino.git C:\wb05\source\route-a\openvino
git -C C:\wb05\source\route-a\openvino fetch --no-tags origin b9a1f201c109e0bed74763934f79483cf6c4cbf4
git -C C:\wb05\source\route-a\openvino checkout --detach b9a1f201c109e0bed74763934f79483cf6c4cbf4
git -C C:\wb05\source\route-a\openvino submodule sync --recursive
git -C C:\wb05\source\route-a\openvino submodule update --init --recursive
```

- [ ] For Route A GenAI, use `https://github.com/openvinotoolkit/openvino.genai.git`, directory `C:\wb05\source\route-a\openvino.genai`, and commit `05e5c7670b597746f858946974d11f38e3baf42f`.
- [ ] For Route B, use `https://github.com/EgorDuplensky/openvino.git`, directory `C:\wb05\source\route-b\openvino`, and commit `1827f6458d049de11c1a8203c793af67c99935dc`.
- [ ] Existing directories are reused only when origin, exact `HEAD`, and clean status already match. Never run destructive reset or clean.
- [ ] Record Git version, origin, head, status, `.gitmodules`, and recursive submodule path/URL/SHA.
- [ ] Run tests and commit:

```text
feat(workbook-05): verify pinned source trees and submodules
```

### Task 5: Classify source capabilities without support claims

- [ ] Create source fixtures from exact pinned files and write failing Route A and Route B tests.
- [ ] Route A findings require `CacheQuantAlgorithm`, `SCALAR`, `TURBO`, `key_cache_quant_alg`, `value_cache_quant_alg`, `u3`, `u4`, and TurboQuant codec/quantise files.
- [ ] Route B findings require QJL enums, `not yet supported`, Polar enums, key/value codec properties, QJL write/read tokens where present, Polar encode/decode paths, and asymmetric K/V dispatch.
- [ ] Implement exact-file, exact-token inspection with whole-file SHA-256 and bounded excerpts.
- [ ] Every positive classification is exactly `Present in source`.
- [ ] Commit:

```text
feat(workbook-05): classify codec source capabilities
```

### Task 6: Reproduce `RB-SRC-001`

- [ ] Create fixtures containing both `instances/x64/concat_sdp_turboq.cpp` and `x64/concat_sdp_turboq.cpp`, plus both common paths.
- [ ] Write a failing test proving the second `GLOB_RECURSE` assignment replaces the first architecture and common lists.
- [ ] Add a generated-target fixture that omits the first paths and a second fixture containing all intended paths.
- [ ] Implement a focused parser only for `LIST_OF_TEST_ARCH_INSTANCES` and `LIST_OF_TEST_COMMON_INSTANCES`; calculate source-order replacement and compare with generated metadata.
- [ ] Report intended, final, and omitted sources. Do not modify the fork.
- [ ] Commit:

```text
test(workbook-05): reproduce the Route B test-discovery blocker
```

### Task 7: Add Route A configure-only evidence

- [ ] Write command-construction and cache-evaluation tests first.
- [ ] The exact command tuple is:

```text
C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe
-S
C:\wb05\source\route-a\openvino
-B
C:\wb05\build\route-a-runtime
-G
Visual Studio 17 2022
-A
x64
-DENABLE_INTEL_GPU=OFF
```

- [ ] Reject `--build`, `--install`, `--target`, Route B paths, GPU enabled, and non-empty unexpected build directories.
- [ ] A passing `CMakeCache.txt` requires:

```text
CMAKE_HOME_DIRECTORY=C:/wb05/source/route-a/openvino
CMAKE_GENERATOR=Visual Studio 17 2022
CMAKE_GENERATOR_PLATFORM=x64
ENABLE_INTEL_GPU=OFF
```

- [ ] Add deterministic fake-CMake integration tests without compiling.
- [ ] Preserve command, working directory, environment, timestamps, exit code, stdout, stderr, cache, and cache hash.
- [ ] Commit:

```text
feat(workbook-05): add the Route A configure-only gate
```

### Task 8: Calculate truthful route and checkpoint decisions

- [ ] Write tests for: Route A admitted and Route B blocked; Route A configure failure; invalid measurement controls.
- [ ] Strengthen source admission so every passed proof has a safe relative evidence path, decision reason is non-empty, and open or accepted-risk blockers prevent admission.
- [ ] Assemble reports, controlled records, summary JSON/Markdown, checkpoint candidate, and final hash manifest.
- [ ] Route A may be `Admitted` while Route B is `Blocked`; checkpoint `phase-1-source-admission` may be `Passed` because the approved campaign allows Route A continuation.
- [ ] The Intel job never edits the repository checkpoint.
- [ ] Commit:

```text
feat(workbook-05): calculate source-admission outcomes
```

### Task 9: Build the read-only Windows orchestrator

- [ ] Write static tests forbidding build/install/package commands, model URLs, `git reset --hard`, `git clean`, recursive removal of `C:\wb05`, `Invoke-Expression`, and Route B configure execution.
- [ ] Implement this order: preflight validation; workspace validation; measurement-control capture; Route A Runtime verification; Route A GenAI verification; Route B verification; document capture; capability inspection; Route B CMake audit; conditional Route A configure probe; decisions; hashes.
- [ ] Source blockers produce truthful evidence rather than infrastructure failure. Integrity or orchestration failures return non-zero.
- [ ] Add beginner-readable comments to every block.
- [ ] Commit:

```text
feat(workbook-05): orchestrate staged source admission
```

### Task 10: Validate the artifact as untrusted data

- [ ] Write valid and adversarial bundle tests first.
- [ ] Reject hash changes, false admission, wrong origin/commit, incomplete submodules, Route B admission with confirmed blocker, Route A admission without valid configure cache, invalid measurement-control hashes, permissive measured-run schemas, unsafe paths, secrets, executables, libraries, archives, wheels, and model files.
- [ ] Implement the hosted CLI using this concrete workflow layout:

```powershell
python -m scripts.testing.workbook05.source_admission_bundle_validation `
  --bundle-root 'D:\a\_temp\workbook-05-source-admission' `
  --repository-root $env:GITHUB_WORKSPACE `
  --summary 'D:\a\_temp\workbook-05-source-admission-validation.md'
```

- [ ] Always write a Markdown summary; return zero only with no issues.
- [ ] Commit:

```text
test(workbook-05): validate source-admission evidence bundles
```

### Task 11: Add the two-job GitHub workflow

- [ ] Write workflow-contract tests first.
- [ ] Require `contents: read`, `actions: read`, same-repository branch `testing/workbook-05-source-admission`, manual `main` or that branch only, exact Intel labels, pinned actions, no persisted credentials, and `cancel-in-progress: false`.
- [ ] Intel timeout is 180 minutes. Sparse checkout includes workflows, plans/specs, testing docs, prompts, rubrics, Workbook 05 controls, scripts, and tests.
- [ ] Run all Workbook 05 Python and PowerShell tests before collection.
- [ ] Upload `workbook-05-source-admission-${{ github.run_id }}-${{ github.run_attempt }}` with `if: always()`.
- [ ] Hosted `windows-latest` downloads the exact artifact, runs all Workbook 05 tests, and invokes the untrusted validator without executing external source.
- [ ] Commit:

```text
ci(workbook-05): add staged source-admission evidence flow
```

### Task 12: Add the repository-level Phase 1 gate

- [ ] Create `Validate-Workbook05-SourceAdmission.ps1`.
- [ ] Run all Workbook 05 Python tests, PowerShell tests, controlled-workspace validator, OpenVINO structural validator, measurement-control validation, schema/template validation, and `git diff --check`.
- [ ] Print these lines only after success:

```text
WORKBOOK 05 SOURCE ADMISSION SCAFFOLD: PASS
No OpenVINO build, model download, inference, or benchmark was performed.
```

- [ ] Add this gate to the Intel workflow and commit:

```text
test(workbook-05): add the source-admission scaffold gate
```

### Task 13: Execute live Phase 1 and review evidence

- [ ] Run the final static gate and record exact head/test counts.
- [ ] Trigger the source-admission workflow while the Intel laptop is powered, plugged in, online, awake, and running the service.
- [ ] Inspect exact commits, origins, recursive submodules, source findings, Route B omission evidence, Route A cache, measurement hashes, decisions, checkpoint, and artifact digest.
- [ ] Verify the hosted job downloaded the same-attempt artifact and passed every integrity/security/admission check.
- [ ] Run the WinUI build/test workflow on the same head and require non-zero discovery and all packaged tests passing.
- [ ] Update draft PR #46 with implementation details, quality contract, route outcomes, workflow IDs, artifact ID/hash, test counts, defects and fixes, explicit non-claims, and next-phase boundary.
- [ ] Check the diff, permissions, unresolved threads, secrets, payload suffixes, and false support claims.
- [ ] Mark ready only after all evidence is green. Do not merge automatically.

## Final verification

```powershell
& '.\scripts\testing\Validate-Workbook05-SourceAdmission.ps1'
```

Live completion additionally requires green Intel collection, green hosted validation, and green WinUI build/tests on the same branch head.

## Explicit non-claims

Passing Phase 1 does not prove full Runtime or GenAI compilation, runtime activation, QJL or Polar quality, packed cache size, Granite compatibility, context limits, latency, throughput, memory savings, perplexity, or quality scores. It proves only that the exact source boundary is controlled, each route has a truthful admission decision, and later measured runs cannot omit the project’s central quality question.
