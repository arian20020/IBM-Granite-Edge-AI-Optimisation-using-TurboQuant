# Workbook 05 Staged Source-Admission Design

## Status

Approved direction: **Approach C — staged parallel admission**.

This design governs `phase-1-source-admission` for campaign `GTQ-WB05-MF-v1`. It begins only after the verified preflight foundation from PR #42 is merged into `main` at commit `cb963347669f07c3bec5972f99d44950ccccbc42`.

The design does not authorise full OpenVINO compilation, model downloads, inference, performance measurements, quality scoring, or claims that QJL or PolarQuant work. It prepares and verifies the exact source boundary required before those activities.

## Goal

Produce reproducible, independently validated source-admission evidence for:

- **Route A — `route-a-merged-openvino`**: official merged OpenVINO TurboQuant Runtime plus a pinned OpenVINO GenAI compatibility candidate.
- **Route B — `route-b-experimental-qjl-polar`**: the open experimental OpenVINO source containing candidate TurboQuant+QJL, PolarQuant, and asymmetric K/V paths.

At the end of this phase, each route must have one of these evidence-backed outcomes:

- `Admitted for documented build`
- `Candidate — additional proof required`
- `Blocked — named blocker with evidence`

A route cannot be called admitted merely because source files, comments, constants, README claims, or pull-request checkboxes exist.

## Controlled provenance

### Route A

- Repository: `openvinotoolkit/openvino`
- Pull request: `#35853`
- Runtime merge commit: `b9a1f201c109e0bed74763934f79483cf6c4cbf4`
- Runtime base commit: `816d60598f9b37135cf7d9ee7ca11b75082896b2`
- Repository: `openvinotoolkit/openvino.genai`
- GenAI compatibility-candidate commit: `05e5c7670b597746f858946974d11f38e3baf42f`

The merged Runtime source exposes `KEY_CACHE_QUANT_ALG` and `VALUE_CACHE_QUANT_ALG`, with `SCALAR` and `TURBO` algorithms. Integer cache precision selects the bit width, including `u3` and `u4`. Route A admission therefore targets the real merged interface rather than synthetic codec names.

### Route B

- Repository: `EgorDuplensky/openvino`
- Upstream pull request: `openvinotoolkit/openvino#35092`
- Experimental commit: `1827f6458d049de11c1a8203c793af67c99935dc`
- Base commit: `7de5a4fbb178a1de43f6bc3cccc95ff656abed05`
- Pull-request state at design time: open and unmerged

Route B remains experimental even if it builds. Its results must never be described as official merged OpenVINO support.

## Authoritative source documents

The phase uses the already controlled document list in:

`experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json`

The important instructions are:

- Route A OpenVINO Windows build guide at Runtime commit `b9a1f201...`
- Route A OpenVINO GenAI build guide at commit `05e5c767...`
- Route B OpenVINO Windows build guide at experimental commit `1827f645...`
- Route B codec and PolarQuant design documents at the same experimental commit

The OpenVINO Windows guide requires cloning the repository, initialising recursive submodules, creating a separate build directory, configuring with the Visual Studio 17 2022 generator, building Release, and adding the build’s TBB binaries to `PATH` before execution.

The pinned OpenVINO GenAI guide states that OpenVINO Runtime and OpenVINO GenAI should be built from source using the same build environment. Mixing a source-built GenAI library with an archive Runtime is not accepted for this campaign.

## Selected approach

Route A and Route B are investigated in parallel only while work remains inexpensive and read-only.

### Route A sequence

1. Verify that the exact Runtime and GenAI commits are reachable from their declared repositories.
2. Clone only the pinned repositories into the controlled short-path workspace.
3. Initialise recursive submodules and capture exact submodule revisions.
4. Verify source-tree cleanliness and exact `HEAD` values.
5. Preserve and hash all authoritative build documents.
6. Confirm that the merged source exposes the expected cache-algorithm and precision controls.
7. Freeze the planned compiler, generator, Python, SDK, CPU-only option, directory layout, and evidence layout.
8. Run a non-building CMake configuration probe only after the previous gates pass.
9. Mark Route A `Admitted for documented build` only when all source-admission proofs pass.

### Route B sequence

1. Verify that the exact experimental commit and base commit are reachable.
2. Clone the fork into a separate source directory and initialise recursive submodules.
3. Preserve and hash the experimental documentation.
4. Confirm that source symbols and dispatch structures for QJL, PolarQuant, and independent K/V selection exist at the pinned commit.
5. Reproduce blocker `RB-SRC-001` by inspecting the actual CMake source-collection logic and generated configuration data.
6. Determine the exact intended versus generated functional-test source lists.
7. Do not begin a full Route B build while required test instances may be silently omitted.
8. When the blocker is real, design a minimal test-discovery-only correction in a later reviewed phase; do not silently alter codec algorithms.
9. Mark Route B `Admitted for documented build` only after the test-target completeness proof and every other mandatory source proof passes.

Route A may proceed even when Route B remains blocked.

## Target-machine directory boundary

All external source, build, install, and evidence material must stay outside the project repository under a short Windows path:

```text
C:\wb05\
├── source\
│   ├── route-a\
│   │   ├── openvino\
│   │   └── openvino.genai\
│   └── route-b\
│       └── openvino\
├── build\
│   ├── route-a-runtime\
│   ├── route-a-genai\
│   └── route-b-runtime\
├── install\
│   ├── route-a\
│   └── route-b\
└── evidence\
    └── <source-admission-run-id>\
```

Requirements:

- Route A and Route B never share source, build, install, or temporary directories.
- A run-specific evidence directory is immutable after upload.
- External OpenVINO source trees, submodules, build outputs, DLLs, executables, wheels, archives, and models are never committed to the project repository.
- The workflow may create `C:\wb05` only after confirming it is not a file or unsafe reparse-point target.
- Existing controlled source directories with a mismatched origin or commit are not reused; they are recorded as a blocker rather than reset destructively.

## Frozen toolchain and build-probe policy

The source-admission evidence must record absolute paths and versions for:

- Git
- CMake
- Visual Studio installation
- MSBuild
- native x64 MSVC compiler
- Windows SDK
- Python `C:\Program Files\Python312\python.exe`, version `3.12.10`

Route A’s planned Runtime configuration must use:

```text
Generator: Visual Studio 17 2022
Architecture: x64
Configuration: Release
ENABLE_INTEL_GPU: OFF
```

`ENABLE_INTEL_GPU=OFF` is a documented OpenVINO option and is selected because Workbook 05 is a controlled CPU experiment. This prevents the default GPU-plugin build from adding an unnecessary graphics-driver dependency or creating ambiguity about the intended backend.

The configuration probe may run CMake generation but may not run `cmake --build`, compile targets, install outputs, or package artifacts in this phase.

The GenAI admission record must preserve the requirement that Runtime and GenAI use the same build environment. GenAI compilation remains a later `phase-2-documented-build` activity.

## Components

### 1. Source-admission configuration

A versioned configuration file defines:

- route IDs;
- repositories and immutable commits;
- expected origins;
- allowed workspace roots;
- authoritative documents;
- expected source symbols;
- expected Route B test-source locations;
- toolchain requirements;
- CPU-only CMake options;
- timeouts and maximum repository sizes where practical.

### 2. Git source verifier

A focused verifier must:

- reject non-allowlisted repositories;
- require lowercase 40-character commit SHAs;
- query the remote for the exact commit;
- clone without changing the requested commit;
- initialise recursive submodules;
- record every submodule path, URL, and commit;
- verify exact origin URL and clean working tree;
- refuse destructive cleanup of an unexpected existing directory.

### 3. Static source-capability inspector

The inspector produces evidence, not support claims. It records whether the pinned source contains:

- Route A cache-algorithm properties and `SCALAR`/`TURBO` enum values;
- Route A `u3` and `u4` cache-precision handling;
- Route B QJL identifiers and encode/decode paths;
- Route B PolarQuant identifiers and encode/decode paths;
- Route B independent K/V configuration or dispatch paths;
- referenced record-size constants and layouts.

A found symbol counts only as `Present in source`, never as `Executable`, `Activated`, or `Supported`.

### 4. Route B CMake test-discovery auditor

The auditor must reproduce `RB-SRC-001` from source and configuration data. It must identify:

- the first `GLOB_RECURSE` result;
- the second assignment to the same variable;
- which source paths are replaced;
- the final source list passed to the target;
- intended test instances that are omitted;
- whether generated build-system metadata confirms the omission.

The auditor must not edit the fork in this phase.

### 5. CMake configure probe

The Route A probe must:

- use the exact Visual Studio 17 2022 generator;
- target x64;
- set `ENABLE_INTEL_GPU=OFF`;
- use a fresh, route-specific build directory;
- record command, working directory, environment, timestamps, exit code, stdout, and stderr;
- preserve `CMakeCache.txt` and relevant generated metadata as evidence;
- stop before compilation.

A Route B configure probe is allowed only after `RB-SRC-001` is either disproved or resolved through a separately reviewed correction.

### 6. Evidence packager and hosted validator

The Intel runner uploads a source-admission artifact. A separate GitHub-hosted job validates it as untrusted data.

The validator checks:

- required files and schemas;
- SHA-256 manifest consistency;
- route/commit/origin truthfulness;
- submodule completeness;
- source-capability claims against captured snippets or hashes;
- Route B blocker calculation;
- absence of executable or model payloads;
- absence of credentials and unsafe paths;
- checkpoint generation and phase status.

The hosted job never executes captured external commands or source code.

## Data flow

```text
Reviewed main commit
        ↓
Read-only self-hosted Intel job
        ↓
Verify workspace safety and toolchain
        ↓
Clone exact allowlisted commits and submodules
        ↓
Capture documents, Git state, source symbols, and Route B CMake evidence
        ↓
Optional Route A CMake configure-only probe
        ↓
Generate schemas, hashes, source-admission decisions, and checkpoint candidate
        ↓
Upload immutable artifact
        ↓
Independent GitHub-hosted validation as untrusted data
        ↓
Human-reviewed admission decision and later evidence-promotion PR
```

The Intel runner retains `contents: read` and `actions: read`. It does not push to the repository.

## Admission decisions

### Route A mandatory proofs

Route A may become `Admitted for documented build` only when all are true:

1. authoritative documents captured and hashed;
2. Runtime commit reachable;
3. GenAI commit reachable;
4. recursive submodules complete;
5. exact origins and clean source trees verified;
6. expected merged properties visible in source;
7. frozen toolchain satisfies documented requirements;
8. CPU-only Route A CMake configure probe succeeds;
9. generated metadata identifies the pinned source and intended options;
10. no blocking discrepancy remains.

Runtime activation, packed allocation, and no-silent-fallback remain later conformance proofs. They are not falsely completed during source admission.

### Route B mandatory proofs

Route B may become `Admitted for documented build` only when all are true:

1. authoritative documents captured and hashed;
2. experimental commit and base commit reachable;
3. recursive submodules complete;
4. exact origin and clean source tree verified;
5. QJL source-selection path visible;
6. PolarQuant source-selection path visible;
7. encode and decode source paths visible;
8. independent K/V source paths visible;
9. `RB-SRC-001` is disproved or resolved with a separately reviewed test-only correction;
10. generated target metadata proves required repository tests are included;
11. no additional blocking discrepancy remains.

No Route B full configure/build is required when the test-discovery blocker remains open. The correct phase outcome is `Blocked`, with Route A continuing independently.

## Error handling and stop conditions

The workflow stops the affected route and records a blocker when:

- a remote commit is unreachable;
- an origin URL differs from the controlled value;
- an existing source directory contains unexpected data;
- recursive submodule initialisation fails;
- a source tree is dirty before inspection;
- the required toolchain is absent or differs from the frozen path;
- free system-drive storage falls below the preflight threshold;
- authoritative documents differ from their pinned commits;
- expected Route A controls are absent;
- Route B source-selection paths are absent;
- Route B test instances are omitted;
- CMake configure exits non-zero;
- evidence hashing, schema validation, or hosted validation fails.

A Route B blocker does not roll back or invalidate valid Route A evidence.

Network and infrastructure failures are recorded separately from source failures and may be retried once without changing inputs. A second identical infrastructure failure leaves the route `Candidate — retry required`; it does not become a source-code failure.

## Security boundary

- Workflows run only from `main` or the exact same-repository reviewed source-admission branch.
- Pull requests from forks cannot execute the self-hosted job.
- Actions are pinned to immutable commit SHAs.
- Checkout uses `persist-credentials: false`.
- The job token has no repository-write permission.
- External source documents and source code are treated as untrusted input.
- Captured README commands remain inert evidence.
- No personal access token, Hugging Face token, model credential, or private data is accepted.
- The validator rejects common token patterns, executables, libraries, archives, wheels, model files, and paths escaping the evidence root.

## Testing strategy

Implementation follows test-driven development.

### Unit tests

Tests cover:

- immutable commit and allowlist validation;
- safe workspace-path resolution;
- existing-directory refusal;
- origin and clean-tree evaluation;
- submodule-manifest parsing;
- Route A source-symbol classification;
- Route B QJL/Polar/source-dispatch classification;
- repeated CMake-variable assignment detection;
- expected-versus-final test-source list calculation;
- admission-decision calculation;
- evidence hashing and unsafe-bundle rejection.

### Integration tests

Repository fixtures simulate:

- a valid tiny Git repository with a submodule manifest;
- a mismatched origin;
- a dirty tree;
- an unsafe workspace;
- Route A source snippets;
- Route B source snippets;
- the exact repeated-`GLOB_RECURSE` pattern from `target_per_test.cmake`;
- generated metadata with and without omitted instances;
- successful and failed CMake configure-probe records.

### Live validation

The first production workflow must prove:

- exact Intel runner identity;
- exact pinned repositories and commits;
- exact recursive submodule state;
- complete source-admission artifact generation;
- independent hosted validation;
- no repository write by the Intel job.

The existing WinUI `Build and test` workflow must remain green on the same final repository head.

## Repository outputs

The implementation stage will add or update only controlled scripts, schemas, manifests, tests, workflows, and documentation. It will not commit cloned OpenVINO trees or build outputs.

Expected project-repository outputs include:

- a Phase 1 workflow;
- source-admission configuration;
- source-verification and static-inspection modules;
- Route B CMake-audit module;
- source-admission evidence schema updates where required;
- unit and integration fixtures;
- a Phase 1 implementation plan;
- a detailed draft pull request.

Validated live evidence remains an Actions artifact until a separate trusted promotion workflow is reviewed.

## Acceptance criteria

The implementation is complete when:

1. every new behavioural rule has a regression test;
2. all Workbook 05 tests pass on Python 3.12.10;
3. PowerShell tests pass on Windows PowerShell 5.1;
4. the Phase 1 workflow is read-only and branch-restricted;
5. Route A exact commits and submodules are verified on the Intel laptop;
6. Route A source controls are captured as present-in-source evidence;
7. Route A CPU-only CMake configure probe succeeds without compiling;
8. Route B exact commit and submodules are verified;
9. Route B QJL, PolarQuant, encode/decode, and asymmetric source paths are classified without overclaiming execution;
10. `RB-SRC-001` is reproduced or disproved from captured evidence;
11. the hosted validator accepts the exact Intel artifact;
12. the checkpoint records `phase-1-source-admission` truthfully;
13. the existing WinUI application build and packaged tests remain green;
14. the pull request states clearly whether each route is admitted, candidate, or blocked;
15. no OpenVINO compilation, model download, inference, performance measurement, or quality scoring is performed.

## Deferred work

The following remain outside this design:

- full OpenVINO Runtime compilation;
- OpenVINO GenAI compilation and installation;
- any Route B correction beyond a separately reviewed test-discovery-only change;
- codec conformance execution;
- Granite model acquisition or conversion;
- memory-frontier discovery;
- formal performance and quality runs;
- results-branch promotion automation;
- final Workbook 05 conclusions.
