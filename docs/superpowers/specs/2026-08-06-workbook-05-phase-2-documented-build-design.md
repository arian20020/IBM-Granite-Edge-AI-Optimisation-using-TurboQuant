# Workbook 05 Phase 2 Documented-Build Design

## Status

**Roadmap task:** `R8 — Write and approve the Phase 2 documented-build plan`

**Campaign:** `GTQ-WB05-MF-v1`

**Checkpoint:** `B1 — specification and implementation plan review`

**Planning branch:** `planning/workbook-05-phase-2-documented-build`

**Stacked base:** `testing/workbook-05-route-b-repair-implementation@8d2d7e8039fc084012fa66a10a78a08a8fb39a2f`

This document plans the build stage only. It does not compile OpenVINO, download a model, run Granite, prove codec activation, claim packed storage, or produce performance or quality results.

Route A is mandatory. Route B is conditional on the independently validated BR8 decision. While BR8 is running, every Route B instruction in this design remains disabled by default.

---

## 1. Purpose

Phase 2 must turn the source-admission locks into reproducible Windows builds whose provenance, commands, dependencies, outputs, resource use, warnings and decisions can be independently reviewed.

A successful build is necessary but insufficient for any TurboQuant claim. The build stage proves only that an exact source pair can be configured, compiled, installed and used for a narrow standard-cache diagnostic. Later checkpoints must separately prove codec activation, no fallback, packed K/V allocation, performance and quality.

The design therefore separates five concerns:

1. source and toolchain provenance;
2. exact documented command execution;
3. build-output traceability;
4. independent artifact validation;
5. scientific claim boundaries.

---

## 2. Pinned source boundary

### 2.1 Route A — merged control route

| Component | Repository | Exact commit | Role |
|---|---|---|---|
| OpenVINO Runtime | `https://github.com/openvinotoolkit/openvino.git` | `b9a1f201c109e0bed74763934f79483cf6c4cbf4` | merged Runtime and CPU-plugin source |
| OpenVINO GenAI | `https://github.com/openvinotoolkit/openvino.genai.git` | `05e5c7670b597746f858946974d11f38e3baf42f` | compatible GenAI candidate |

Controlling Runtime document:

```text
docs/dev/build_windows.md
```

Controlling GenAI document:

```text
src/docs/BUILD.md
```

The GenAI document explicitly prefers building OpenVINO and OpenVINO GenAI from source in the same environment. Phase 2 must follow that path rather than mixing a source-built GenAI with an unrelated archive.

### 2.2 Route B — experimental route

| Component | Repository | Exact commit | Role |
|---|---|---|---|
| Experimental OpenVINO Runtime | `https://github.com/EgorDuplensky/openvino.git` | `1827f6458d049de11c1a8203c793af67c99935dc` | QJL/Polar candidate Runtime |
| Route B repair module | this project repository | PR #49 exact reviewed head | applies the one-file CMake source-list repair |
| OpenVINO GenAI compatibility candidate | `https://github.com/openvinotoolkit/openvino.genai.git` | `05e5c7670b597746f858946974d11f38e3baf42f` | compatibility attempt only |

Route B enters the Phase 2 build matrix only when all of these are true:

- BR8 status is `ExecutableCandidate`;
- the exact BR8 artifact passes hosted validation;
- the artifact belongs to the same immutable PR #49 head;
- the project owner accepts the BR8 decision;
- no algorithm file changed during the bounded repair.

Any other BR8 outcome closes Route B as blocked for Phase 2 while Route A continues independently.

---

## 3. Platform and toolchain locks

The controlled machine is the existing labelled Windows Intel runner:

```text
runner: lenovo-pf4hmd0t-wb05
labels: self-hosted, Windows, X64, workbook05, intel-target
processor: 12th Gen Intel Core i5-12450H
physical memory: approximately 15.7 GiB
```

Required tools:

```text
Python: C:\Program Files\Python312\python.exe
Python version: 3.12.10
Git: first resolved git.exe application
CMake: C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe
Generator: Visual Studio 17 2022
Platform: x64
Configuration: Release
Build parallelism: 2
```

The workflow must record the actual Visual Studio compiler, Windows SDK, CMake, Git and Python versions. A version mismatch is a blocker unless it is recorded as an approved deviation before compilation.

---

## 4. External workspace architecture

Large source trees, submodules, generated projects, binaries and installations remain outside the project repository and outside uploaded artifacts.

### 4.1 Route A paths

```text
C:\w5a\<run-id>-<attempt>\
  ov\                 exact Runtime source
  genai\              exact GenAI source
  b-ov\               Runtime build directory
  i-ov\               Runtime install directory
  b-genai\            GenAI build directory
  i-genai\            GenAI install directory
```

### 4.2 Route B paths

```text
C:\w5b\<run-id>-<attempt>\
  ov\                 exact experimental Runtime source plus reviewed repair
  genai\              exact GenAI compatibility source, only if authorised
  b-ov\               experimental Runtime build directory
  i-ov\               experimental Runtime install directory
  b-genai\            compatibility build directory
  i-genai\            compatibility install directory
```

### 4.3 Evidence roots

```text
%RUNNER_TEMP%\workbook-05-phase-2-build\route-a-runtime
%RUNNER_TEMP%\workbook-05-phase-2-build\route-a-genai
%RUNNER_TEMP%\workbook-05-phase-2-build\route-b-runtime
%RUNNER_TEMP%\workbook-05-phase-2-build\route-b-genai
```

Every run uses a new `<run-id>-<attempt>` directory. Existing work directories are never silently cleaned, reset or reused. A pre-existing path causes a fail-closed stop.

Short roots are required because OpenVINO recursive submodules and generated files can exceed traditional Windows path limits. `core.longpaths=true` is scoped only to the external source repository and individual Git invocations; no machine-wide Git or registry setting is changed.

---

## 5. Documented source acquisition sequence

The pinned Windows document requires obtaining the repository and initialising recursive submodules before configuring. To freeze an exact commit, the workflow uses the equivalent reproducible sequence below instead of following a moving branch tip:

```powershell
# Create an empty external repository at the approved short path.
git -c core.longpaths=true init <source-root>

# Apply long-path handling only inside this external repository.
git -C <source-root> config core.longpaths true

# Register the exact approved upstream repository.
git -C <source-root> remote add origin <repository-url>

# Fetch only the exact approved commit.
git -c core.longpaths=true -C <source-root> fetch --depth=1 origin <commit>

# Detach HEAD at the exact commit so branch movement cannot affect the build.
git -c core.longpaths=true -C <source-root> checkout --detach <commit>

# Follow the controlling build document by initialising every recursive submodule.
git -c core.longpaths=true -C <source-root> submodule update --init --recursive
```

The evidence must record:

- requested and actual remote URL;
- requested and actual HEAD;
- clean status before any approved Route B repair;
- recursive submodule status and count;
- controlling document SHA-256;
- source tree path;
- every source-acquisition command, working directory, stdout, stderr and exit code.

---

## 6. Route A Runtime build design

### 6.1 Configure command

```powershell
& $CMakePath `
  -S $RouteARuntimeSource `
  -B $RouteARuntimeBuild `
  -G 'Visual Studio 17 2022' `
  -A x64 `
  -DCMAKE_BUILD_TYPE=Release `
  -DENABLE_INTEL_GPU=OFF `
  -DENABLE_INTEL_NPU=OFF `
  -DENABLE_TESTS=OFF `
  -DENABLE_FUNCTIONAL_TESTS=OFF `
  -DENABLE_SAMPLES=ON `
  -DENABLE_PYTHON=ON `
  -DENABLE_WHEEL=OFF `
  '-DPython3_EXECUTABLE=C:\Program Files\Python312\python.exe'
```

The Windows document explicitly permits `ENABLE_INTEL_GPU=OFF`. Disabling NPU, full tests and wheel generation is a project-approved resource-control deviation for the 15.7 GiB CPU-only target. Samples and Python remain enabled to support the later standard-cache diagnostic and compatibility inspection.

### 6.2 Build command

```powershell
& $CMakePath `
  --build $RouteARuntimeBuild `
  --config Release `
  --parallel 2 `
  --verbose
```

### 6.3 Install command

```powershell
& $CMakePath `
  --install $RouteARuntimeBuild `
  --config Release `
  --prefix $RouteARuntimeInstall
```

### 6.4 Runtime environment

Before a diagnostic or GenAI build, the workflow must invoke the generated setup script when present:

```powershell
. (Join-Path $RouteARuntimeInstall 'setupvars.ps1')
```

It must then locate the single installed `OpenVINOConfig.cmake` file and set `OpenVINO_DIR` to that file's parent directory. Zero or multiple matches block the GenAI build.

The workflow must preserve and restore `PATH`, `PYTHONPATH`, `OPENVINO_LIB_PATHS` and `OpenVINO_DIR` after each job boundary.

---

## 7. Route A GenAI build design

### 7.1 Configure command

```powershell
& $CMakePath `
  -S $RouteAGenAISource `
  -B $RouteAGenAIBuild `
  -G 'Visual Studio 17 2022' `
  -A x64 `
  -DCMAKE_BUILD_TYPE=Release `
  "-DOpenVINO_DIR=$ResolvedOpenVINOConfigDirectory" `
  -DENABLE_PYTHON=ON `
  -DENABLE_JS=OFF `
  '-DPython3_EXECUTABLE=C:\Program Files\Python312\python.exe'
```

`ENABLE_JS=OFF` is a pre-approved scope reduction because Workbook 05 requires the C++/Python GenAI boundary, not JavaScript bindings. Other GenAI defaults remain unchanged unless an approved deviation record says otherwise.

### 7.2 Build command

```powershell
& $CMakePath `
  --build $RouteAGenAIBuild `
  --config Release `
  --parallel 2 `
  --verbose
```

### 7.3 Install command

```powershell
& $CMakePath `
  --install $RouteAGenAIBuild `
  --config Release `
  --prefix $RouteAGenAIInstall
```

The GenAI build is accepted only when it resolves against the exact Route A Runtime installation rather than an unrelated system package.

---

## 8. Route B conditional build design

Route B uses the same source acquisition, compiler, Release configuration, conservative parallelism and evidence contract as Route A, with these additional gates:

1. verify the exact experimental Runtime commit;
2. apply the reviewed project repair module;
3. prove exactly one external source file changed;
4. run `git diff --check`;
5. hash the repair patch and changed file;
6. configure with `ENABLE_TESTS=ON`, `ENABLE_FUNCTIONAL_TESTS=ON` and `ENABLE_CPU_SPECIFIC_TARGET_PER_TEST=ON`;
7. build the narrow repaired target first;
8. require non-zero test discovery and the validated six-case catalogue;
9. only then permit a full Runtime build attempt;
10. treat GenAI as a compatibility attempt, never as assumed compatibility.

The initial Route B configure command is:

```powershell
& $CMakePath `
  -S $RouteBRuntimeSource `
  -B $RouteBRuntimeBuild `
  -G 'Visual Studio 17 2022' `
  -A x64 `
  -DCMAKE_BUILD_TYPE=Release `
  -DENABLE_INTEL_GPU=OFF `
  -DENABLE_INTEL_NPU=OFF `
  -DENABLE_TESTS=ON `
  -DENABLE_FUNCTIONAL_TESTS=ON `
  -DENABLE_CPU_SPECIFIC_TARGET_PER_TEST=ON `
  -DENABLE_PYTHON=ON `
  -DENABLE_WHEEL=OFF `
  '-DPython3_EXECUTABLE=C:\Program Files\Python312\python.exe'
```

A Route B GenAI build must use a separate build and install directory and must be recorded as one compatibility attempt with an explicit retained/rejected reason.

---

## 9. Command evidence contract

Every native command creates three immutable text records:

```text
<command-id>.command.json
<command-id>.stdout.log
<command-id>.stderr.log
```

The JSON record must contain:

```json
{
  "schema_version": "1.0",
  "command_id": "route-a-runtime-configure",
  "route_id": "route-a-merged-openvino",
  "component": "runtime",
  "executable": "C:/.../cmake.exe",
  "arguments": [],
  "working_directory": "C:/w5a/...",
  "environment_allowlist": {},
  "started_utc": "ISO-8601",
  "ended_utc": "ISO-8601",
  "elapsed_seconds": 0.0,
  "exit_code": 0,
  "stdout_path": "route-a-runtime-configure.stdout.log",
  "stderr_path": "route-a-runtime-configure.stderr.log"
}
```

The implementation must preserve argument boundaries by using a safe native-process adapter rather than `Invoke-Expression` or string concatenation.

---

## 10. Build resource evidence

Each configure, build and install command must capture:

- elapsed wall-clock time;
- process-tree peak working set;
- process-tree peak private bytes where available;
- lowest available physical memory;
- highest Windows commit percentage;
- CPU utilisation samples;
- command exit code;
- machine restart or runner-disconnect evidence;
- warnings and errors without filtering them out.

Sampling interval:

```text
2 seconds during configure/build/install
```

Safety thresholds:

```text
available physical memory below 1.5 GiB for 10 seconds: terminate and block
Windows commit usage above 90% for 10 seconds: terminate and block
no process heartbeat for 15 minutes: terminate and block
per-job GitHub timeout: 360 minutes
```

A cancelled or disconnected build is not a failed source build unless the command itself produced a captured non-zero exit. Infrastructure interruption receives a separate status.

---

## 11. Dependency and binary evidence

The evidence artifact contains metadata and hashes only. It must not contain DLLs, EXEs, LIBs, PDBs, wheels, ZIPs, source trees or model files.

For every retained output, record:

```json
{
  "relative_path": "bin/intel64/Release/openvino.dll",
  "size_bytes": 0,
  "sha256": "lowercase hex",
  "producer_command_id": "route-a-runtime-build",
  "component": "runtime",
  "configuration": "Release",
  "copied_to_artifact": false
}
```

Dependency records must include TBB and any discovered runtime dependency path, source, version when available, file hash and why it is required.

---

## 12. Deviation protocol

A deviation is any command, flag, dependency, path, target, version or order change from this approved design or the pinned source documents.

No unrecorded deviation may execute.

Required deviation record:

```json
{
  "schema_version": "1.0",
  "deviation_id": "DEV-RA-001",
  "route_id": "route-a-merged-openvino",
  "component": "runtime",
  "document": "docs/dev/build_windows.md",
  "document_commit": "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
  "original_step": "documented command or requirement",
  "proposed_change": "exact changed command or value",
  "reason": "specific evidence-backed reason",
  "risk": "what could become incomparable or unsafe",
  "approval_status": "Approved",
  "approved_by": "project-owner or reviewed-plan identifier",
  "approved_utc": "ISO-8601",
  "evidence_paths": []
}
```

Allowed approval states are `Proposed`, `Approved`, `Rejected` and `Superseded`. Only `Approved` may execute.

The design pre-approves these deviations:

- exact detached-commit acquisition instead of a moving default-branch clone;
- short external work roots;
- repository-scoped `core.longpaths=true`;
- `ENABLE_INTEL_GPU=OFF`;
- `ENABLE_INTEL_NPU=OFF`;
- Runtime `ENABLE_TESTS=OFF` and `ENABLE_FUNCTIONAL_TESTS=OFF` for Route A production build;
- `ENABLE_WHEEL=OFF`;
- GenAI `ENABLE_JS=OFF`;
- conservative `--parallel 2`.

No other deviation is pre-approved.

---

## 13. Workflow architecture

The Phase 2 workflow will be manual and read-only. It uses separate self-hosted stages so a long Runtime build does not consume the entire timeout needed for GenAI.

```text
hosted contract validation
        |
        v
Route A Runtime build (self-hosted, max 360 min)
        |
        v
Route A Runtime artifact validation (hosted)
        |
        v
Route A GenAI build (self-hosted, max 360 min)
        |
        v
Route A GenAI artifact validation (hosted)
        |
        +---- optional Route B Runtime/GenAI stages after BR8 approval
```

Workflow controls:

```text
permissions: contents read, actions read
persist-credentials: false
pinned action SHAs only
cancel-in-progress: false
same-repository branch restriction
exact Intel runner labels
manual dispatch only
no pull-request code from forks on the self-hosted runner
no model URLs or model execution
```

Each hosted validator downloads the exact artifact name containing both run ID and attempt number. It treats the bundle as untrusted data and never executes evidence payloads.

---

## 14. Build decisions

Each component receives one status:

```text
Passed
Failed
Blocked
Infrastructure interrupted
Not applicable
```

A route-level build decision may be `BuildCandidate` only when all required components pass and every required artifact passes hosted validation.

Route A requirements:

- Runtime exact source and submodules valid;
- Runtime configure, build and install exit zero;
- Runtime outputs hashed;
- GenAI exact source and submodules valid;
- GenAI resolves against the exact Runtime installation;
- GenAI configure, build and install exit zero;
- standard-cache diagnostic completes later in R10;
- no unsupported TurboQuant activation claim.

Route B requirements:

- accepted BR8 prerequisite;
- exact experimental source and approved repair;
- narrow target and six-case evidence retained;
- Runtime configure, build and install exit zero;
- compatibility attempt recorded for GenAI;
- every failure or incompatibility remains visible;
- no model route proceeds until activation, packed-storage and no-fallback proof.

---

## 15. Stop conditions

Stop the current route immediately when:

- origin, commit or recursive submodules differ;
- the source or output path is outside its approved root;
- an existing run directory would be overwritten;
- a required controlling document is missing or its hash changes;
- an unapproved deviation is requested;
- the compiler, SDK or Python lock is violated;
- configure, build or install returns non-zero;
- resource safety thresholds are exceeded;
- the runner disconnects before evidence is durable;
- binary hashes cannot be reconciled to one producer command;
- hosted validation finds a hash mismatch, secret or prohibited payload;
- Route B prerequisites are absent.

Route A failure does not convert Route B into a control route. Route B failure does not stop a valid Route A build from continuing.

---

## 16. Explicit non-claims

Phase 2 build evidence does not prove:

- QJL or PolarQuant model activation;
- packed K-cache or V-cache allocation;
- no fallback to F32, F16, U8 or another codec;
- memory reduction;
- increased context length;
- faster TTFT or token generation;
- acceptable quality;
- Granite 3B or 8B compatibility;
- official OpenVINO support;
- PagedAttention support;
- prefill compression.

Those claims remain assigned to later conformance, capability, frontier and formal-quality checkpoints.

---

## 17. B1 acceptance checklist

Checkpoint B1 is accepted only when the specification and implementation plan contain:

- exact Runtime and GenAI source locks;
- exact controlling document paths;
- exact route-separated workspace and evidence roots;
- explicit configure/build/install commands;
- environment and dependency handling;
- timeouts and resource safety rules;
- command, deviation, dependency, binary and decision records;
- independent hosted artifact validation;
- Route B conditional gating;
- prohibited payload and secret rules;
- explicit non-claims;
- no placeholders or ambiguous route mixing.

No build workflow or compilation starts under R8 itself.

---

## 18. Engineering-practice basis

This design follows the project baseline:

- **Code Complete** — upstream prerequisites, configuration management, developer test records, precise measurement and controlled integration.
- **Designing Secure Software** — least privilege, fail-secure defaults, untrusted-input validation, narrow trust boundaries and minimal artifact exposure.
- **The Art of Unit Testing** — trustworthy non-zero tests, separated test levels and delivery-pipeline controls.
- **Why Programs Fail** — preserve observations, commands and environments so failures can be reproduced and causes isolated.
- **Refactoring** — keep Route B repair and build planning separate from algorithm changes.
- **Systems Engineering: Principles and Practice** — staged development, work breakdown, test traceability, configuration management and explicit decision gates.
- **windows-apps.pdf** — Windows development toolchain and deployment context for the application that will eventually consume the built native components.
