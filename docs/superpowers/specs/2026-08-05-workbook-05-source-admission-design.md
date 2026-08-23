# Workbook 05 Staged Source-Admission Design

## Status

Approved direction: **Approach C — staged parallel admission**.

This design governs `phase-1-source-admission` for campaign `GTQ-WB05-MF-v1`. It starts from the verified preflight foundation merged through PR #42 at `cb963347669f07c3bec5972f99d44950ccccbc42`.

The phase prepares and verifies source provenance. It does not authorise full OpenVINO compilation, model downloads, inference, performance measurements, quality scoring, or claims that QJL or PolarQuant work.

## Goal

Produce reproducible, independently validated source-admission evidence for:

- **Route A — `route-a-merged-openvino`**: official merged OpenVINO TurboQuant Runtime plus a pinned OpenVINO GenAI compatibility candidate.
- **Route B — `route-b-experimental-qjl-polar`**: the open experimental OpenVINO source containing candidate TurboQuant+QJL, PolarQuant, and asymmetric K/V paths.

The machine-readable `admission_status` must use the existing schema values exactly:

- `Admitted`
- `Candidate`
- `Blocked`

Human-readable meaning belongs in `decision_reason`. For this phase, `Admitted` means admitted only for entry into `phase-2-documented-build`; it does not mean runtime activation or benchmark support has been proven.

A route cannot become `Admitted` merely because source files, comments, constants, README claims, or pull-request checkboxes exist.

## Controlled provenance

### Route A

- Repository: `openvinotoolkit/openvino`
- Pull request: `#35853`
- Runtime merge commit: `b9a1f201c109e0bed74763934f79483cf6c4cbf4`
- Runtime base commit: `816d60598f9b37135cf7d9ee7ca11b75082896b2`
- GenAI repository: `openvinotoolkit/openvino.genai`
- GenAI compatibility-candidate commit: `05e5c7670b597746f858946974d11f38e3baf42f`

The merged Runtime source exposes `KEY_CACHE_QUANT_ALG` and `VALUE_CACHE_QUANT_ALG`, with `SCALAR` and `TURBO` algorithms. Integer cache precision selects the bit width, including `u3` and `u4`. Route A admission therefore targets the real merged interface rather than synthetic codec names.

### Route B

- Repository: `EgorDuplensky/openvino`
- Upstream pull request: `openvinotoolkit/openvino#35092`
- Experimental commit: `1827f6458d049de11c1a8203c793af67c99935dc`
- Base commit: `7de5a4fbb178a1de43f6bc3cccc95ff656abed05`
- Pull-request state at design time: open and unmerged

Route B remains experimental even when it builds. Its results must never be described as official merged OpenVINO support.

## Authoritative instructions

The phase uses the controlled document list in:

`experiments/granite_turboquant_intel/configurations/workbook05/pinned-document-sources.json`

The pinned OpenVINO Windows guide requires cloning the repository, initialising recursive submodules, creating a separate build directory, configuring with Visual Studio 17 2022, building Release, and adding the build’s TBB binaries to `PATH` before execution.

The pinned OpenVINO GenAI guide states that OpenVINO Runtime and OpenVINO GenAI should be built from source using the same build environment. Mixing a source-built GenAI library with an archive Runtime is not accepted for this campaign.

Captured build commands remain inert evidence during source admission. The workflow does not blindly execute README text.

## Selected staged approach

Route A and Route B are investigated in parallel only while work remains inexpensive and read-only.

### Route A sequence

1. Verify the exact Runtime and GenAI commits against their declared repositories.
2. Clone only the pinned repositories into the controlled short-path workspace.
3. Initialise recursive submodules and capture every submodule revision.
4. Verify source-tree cleanliness, exact origins, and exact `HEAD` values.
5. Preserve and hash the authoritative build documents.
6. Confirm the merged cache-algorithm and precision controls in source.
7. Freeze compiler, generator, Python, SDK, CPU-only option, and directory layout.
8. Run a non-building CMake configuration probe after the prior gates pass.
9. Set Route A to `Admitted` only when every source-admission proof passes; otherwise retain `Candidate` or set `Blocked` with a precise `decision_reason`.

### Route B sequence

1. Verify the exact experimental commit and base commit.
2. Clone the fork into a separate source directory and initialise recursive submodules.
3. Preserve and hash the experimental documentation.
4. Confirm source symbols and dispatch structures for QJL, PolarQuant, and independent K/V selection.
5. Reproduce blocker `RB-SRC-001` from actual CMake source-collection logic and generated metadata.
6. Calculate the intended and final functional-test source lists.
7. Do not begin a full Route B build while required test instances may be silently omitted.
8. When the blocker is confirmed, defer any minimal test-discovery-only correction to a separately reviewed phase; do not alter codec algorithms silently.
9. Set Route B to `Admitted` only after test-target completeness and every mandatory source proof pass. Otherwise retain `Candidate` or set `Blocked` with evidence.

A Route B blocker does not prevent Route A from progressing.

## Target-machine directory boundary

All external source, build, install, and evidence material stays outside the project repository under:

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

Rules:

- Route A and Route B never share source, build, install, or temporary directories.
- Run-specific evidence becomes immutable after upload.
- OpenVINO trees, submodules, build outputs, DLLs, executables, wheels, archives, and models are never committed to the project repository.
- `C:\wb05` may be created only after proving it is not a file or unsafe reparse-point target.
- An existing source directory with an unexpected origin, commit, or dirty state is recorded as a blocker and is not reset or deleted automatically.

## Frozen toolchain and configure-probe policy

Evidence records absolute paths and versions for Git, CMake, Visual Studio, MSBuild, native x64 MSVC, Windows SDK, and Python.

Python is exactly:

`C:\Program Files\Python312\python.exe`, version `3.12.10`.

Route A’s Runtime configuration probe uses:

```text
Generator: Visual Studio 17 2022
Architecture: x64
Configuration: Release
ENABLE_INTEL_GPU: OFF
```

`ENABLE_INTEL_GPU=OFF` is a documented OpenVINO option selected because Workbook 05 is a controlled CPU experiment. It avoids an unnecessary graphics-driver dependency and backend ambiguity.

The probe may run CMake generation but may not run `cmake --build`, compile targets, install outputs, or package artifacts. GenAI compilation remains part of `phase-2-documented-build`.

## Components

### Source-admission configuration

A versioned configuration defines route IDs, repositories, immutable commits, origins, workspace roots, authoritative documents, expected source symbols, Route B test-source locations, toolchain requirements, CPU-only CMake options, and safety limits.

### Git source verifier

The verifier:

- rejects non-allowlisted repositories;
- requires lowercase 40-character commit SHAs;
- verifies exact remote commits;
- clones without changing the requested commit;
- initialises recursive submodules;
- records submodule path, URL, and commit;
- verifies origin and clean working tree;
- refuses destructive cleanup of unexpected directories.

### Static source-capability inspector

The inspector records source presence without support claims:

- Route A cache-algorithm properties and `SCALAR`/`TURBO` values;
- Route A `u3` and `u4` handling;
- Route B QJL selection and encode/decode paths;
- Route B PolarQuant selection and encode/decode paths;
- Route B independent K/V configuration or dispatch paths;
- referenced record-size constants and layouts.

A found symbol is classified as `Present in source`, never as `Executable`, `Activated`, or `Supported`.

### Route B CMake test-discovery auditor

The auditor reproduces `RB-SRC-001` and records:

- the first `GLOB_RECURSE` result;
- the second assignment to the same variable;
- replaced source paths;
- the final source list passed to the target;
- intended test instances that are omitted;
- generated metadata confirming or disproving the omission.

It does not edit the fork in this phase.

### Route A CMake configure probe

The probe uses the exact generator, x64, `ENABLE_INTEL_GPU=OFF`, and a fresh Route A build directory. It records command, working directory, environment, timestamps, exit code, stdout, stderr, `CMakeCache.txt`, and relevant generated metadata, then stops before compilation.

A Route B configure probe is forbidden while `RB-SRC-001` remains open.

### Evidence packager and hosted validator

The Intel runner uploads a source-admission artifact. A separate GitHub-hosted job validates it as untrusted data. The validator checks required files, schemas, hashes, route/commit/origin truthfulness, submodule completeness, source classifications, blocker calculation, unsafe paths, forbidden payloads, secret patterns, and checkpoint truthfulness. It never executes captured external commands or source code.

## Data flow

```text
Reviewed main or same-repository source-admission branch
        ↓
Read-only self-hosted Intel job
        ↓
Verify workspace safety and toolchain
        ↓
Clone exact allowlisted commits and recursive submodules
        ↓
Capture documents, Git state, source symbols, and Route B CMake evidence
        ↓
Route A CMake configure-only probe
        ↓
Generate decisions, schemas, hashes, and checkpoint candidate
        ↓
Upload immutable artifact
        ↓
Independent hosted validation as untrusted data
        ↓
Human review and later evidence-promotion pull request
```

The Intel runner retains only `contents: read` and `actions: read`.

## Admission proofs

### Route A

Route A may become `Admitted` only when:

1. documents are captured and hashed;
2. Runtime and GenAI commits are reachable;
3. recursive submodules are complete;
4. origins, exact heads, and clean trees are verified;
5. expected merged properties are present in source;
6. the frozen toolchain satisfies documented requirements;
7. the CPU-only CMake configure probe succeeds;
8. generated metadata identifies the pinned source and intended options;
9. no blocker remains.

Runtime activation, packed allocation, and no-silent-fallback are later conformance proofs and remain pending.

### Route B

Route B may become `Admitted` only when:

1. documents are captured and hashed;
2. experimental and base commits are reachable;
3. recursive submodules are complete;
4. origin, exact head, and clean tree are verified;
5. QJL and PolarQuant selection paths are present;
6. encode/decode paths are present;
7. independent K/V paths are present;
8. `RB-SRC-001` is disproved or resolved by a separately reviewed test-only correction;
9. generated metadata proves required repository tests are included;
10. no additional blocker remains.

When the test-discovery blocker remains open, Route B is `Blocked`; Route A continues independently.

## Error handling and stop conditions

The affected route stops and records evidence when a commit is unreachable, an origin differs, an existing directory is unexpected, submodule initialisation fails, a tree is dirty, the frozen toolchain is unavailable, storage falls below the existing preflight threshold, documents mismatch their commits, expected source controls are absent, Route B instances are omitted, CMake configuration fails, or evidence validation fails.

A network or infrastructure failure may be retried once with unchanged inputs. A repeated infrastructure failure leaves the route `Candidate` with a retry requirement in `decision_reason`; it is not reclassified as a source-code failure.

## Security boundary

- Workflows run only from `main` or the exact same-repository source-admission branch.
- Fork pull requests cannot execute the self-hosted job.
- Actions are pinned to immutable SHAs.
- Checkout uses `persist-credentials: false`.
- The job token has no write permission.
- External documents and source are untrusted input.
- Captured README commands remain inert.
- No personal access token, Hugging Face token, model credential, or private data is accepted.
- The validator rejects secrets, paths escaping evidence root, executables, libraries, archives, wheels, and model files.

## Testing strategy

Implementation is test-driven.

Unit and integration tests cover immutable commit validation, allowlists, safe workspace resolution, unexpected-directory refusal, origin and clean-tree evaluation, submodule parsing, Route A source classification, Route B QJL/Polar/asymmetric classification, repeated CMake-variable assignment detection, intended-versus-final source calculation, admission decisions, hashing, unsafe-bundle rejection, and configure-probe records.

Fixtures include valid and invalid tiny repositories, submodule manifests, mismatched origins, dirty trees, unsafe workspaces, Route A snippets, Route B snippets, the exact repeated-`GLOB_RECURSE` pattern, generated metadata with and without missing instances, and successful/failed configure records.

The first live workflow must prove exact Intel runner identity, pinned repositories and commits, recursive submodules, complete artifact generation, independent hosted validation, and no repository write. The existing WinUI `Build and test` workflow must remain green on the same final head.

## Repository outputs

The implementation may add or update only controlled workflows, configurations, schemas, manifests, scripts, tests, fixtures, plans, and documentation. External source trees and generated build outputs remain outside the repository.

Validated live evidence remains an Actions artifact until a separate trusted promotion workflow is reviewed.

## Acceptance criteria

The phase implementation is complete when:

1. every behavioural rule has a regression test;
2. all Workbook 05 Python tests pass on Python 3.12.10;
3. PowerShell tests pass on Windows PowerShell 5.1;
4. the workflow is read-only and branch-restricted;
5. Route A commits and recursive submodules are verified on the Intel laptop;
6. Route A source controls are captured without overclaiming execution;
7. Route A’s CPU-only configure probe succeeds without compilation;
8. Route B commit and recursive submodules are verified;
9. Route B QJL, PolarQuant, encode/decode, and asymmetric paths are classified without overclaiming execution;
10. `RB-SRC-001` is reproduced or disproved from captured evidence;
11. the hosted validator accepts the exact Intel artifact;
12. the checkpoint records `phase-1-source-admission` truthfully;
13. the existing WinUI build and packaged tests remain green;
14. each route’s `admission_status` and `decision_reason` match the evidence;
15. no OpenVINO compilation, model download, inference, performance measurement, or quality scoring occurs.

## Deferred work

Deferred work includes full Runtime and GenAI compilation, any Route B correction, codec conformance, Granite acquisition, memory-frontier discovery, formal performance and quality runs, results-branch promotion, and final Workbook 05 conclusions.
