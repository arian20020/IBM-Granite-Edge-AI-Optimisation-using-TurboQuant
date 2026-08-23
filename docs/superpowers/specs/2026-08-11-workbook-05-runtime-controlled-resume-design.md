# Workbook 05 Route A Runtime Controlled-Resume Design

## Status

Approved for implementation on 11 August 2026.

## Problem

Route A Runtime workflow run `31391119557`, attempt `4`, reached the repository-controlled six-hour GitHub Actions job timeout while the pinned OpenVINO Runtime was still compiling. The build step ran for `5h 59m 17s`, then GitHub cancelled the process before the process adapter could write the build command record or before the Runtime orchestrator could write `decision.json`.

The retained artifact is valid timeout evidence, not an accepted Runtime:

- artifact name: `workbook-05-build-route-a-runtime-31391119557-4`
- artifact ID: `9113026088`
- GitHub-recorded SHA-256: `sha256:b2b9f0ca8528f1d6135d03c77ac964f8799ffc8989cc1a03515a6d5c7c73c1a4`
- independently calculated SHA-256: `sha256:b2b9f0ca8528f1d6135d03c77ac964f8799ffc8989cc1a03515a6d5c7c73c1a4`
- repository head: `fb5d3349aae9d7acb6bd8132cbffa2303e40cabd`
- local workspace: `C:\w5a\phase2-31391119557-4`
- local CMake cache SHA-256 recorded by the artifact: `b5c0606efa261a9525f5562eb973919d508f5242a567f82a46d0b0b9df725c77`

The timeout did not prove a source failure. Across `7,682` samples, Windows commit remained below the reviewed `90%` limit and available memory remained above the reviewed `1.5 GiB` floor. The earlier disk and uncontrolled compiler-concurrency defects were already corrected.

## Goal

Resume the existing incremental OpenVINO Runtime build only after re-establishing the exact source, build-cache, workspace, artifact and machine-control boundaries. The resumed run must either complete with a schema-valid decision or stop itself early enough to preserve a complete `Infrastructure interrupted` decision before GitHub reaches its outer job timeout.

## Selected architecture

A dedicated manual workflow, `Workbook 05 Route A Runtime controlled resume`, is added rather than adding conditional resume behavior to the ordinary fresh-build workflow.

```text
Exact attempt-4 artifact + local attempt-4 workspace
                ↓
Hosted repository gate
                ↓
Read-only GitHub artifact identity check
                ↓
Download exact prior text-only artifact
                ↓
Validate prior artifact as untrusted timeout evidence
                ↓
Revalidate local source, submodules, paths and CMake cache
                ↓
Run the same incremental CMake/MSBuild build command
                ↓
Install and verify the two GenAI frontend headers
                ↓
Upload new text-only evidence
                ↓
Independent hosted validation of the new decision
```

The ordinary `route-a-runtime` path remains a fresh-workspace build. The controlled resume is a separate, explicit recovery path.

## Workflow boundary

The new workflow supports `workflow_dispatch` and a pull-request repository gate. The self-hosted resume job runs only for manual dispatch from the same repository.

Default inputs are bound to the exact approved prerequisite:

- resume workspace: `C:\w5a\phase2-31391119557-4`
- prior workflow run: `31391119557`
- prior run attempt: `4`
- prior artifact: `workbook-05-build-route-a-runtime-31391119557-4`
- recorded artifact digest: `sha256:b2b9f0ca8528f1d6135d03c77ac964f8799ffc8989cc1a03515a6d5c7c73c1a4`
- independently verified digest: `sha256:b2b9f0ca8528f1d6135d03c77ac964f8799ffc8989cc1a03515a6d5c7c73c1a4`
- prior repository head: `fb5d3349aae9d7acb6bd8132cbffa2303e40cabd`
- expected CMake cache digest: `b5c0606efa261a9525f5562eb973919d508f5242a567f82a46d0b0b9df725c77`

The job-level timeout is `720` minutes. The workflow keeps `contents: read` and `actions: read`, pinned actions, `persist-credentials: false`, exact runner labels, same-repository guards and `cancel-in-progress: false`.

## Prior-artifact identity check

Before the previous artifact is consumed, a small PowerShell boundary queries the GitHub Actions artifact API using the workflow-scoped read-only token. It requires:

1. the recorded and independently verified digests to match exactly;
2. exactly one artifact with the requested name under the requested run;
3. the artifact to be unexpired;
4. the GitHub-recorded digest to equal both supplied digests;
5. the artifact workflow head SHA to equal the expected prior repository head.

The token is never written to evidence, command arguments, output files or logs.

## Untrusted timeout-artifact validation

A dedicated Python validator accepts only the known Runtime timeout-evidence shape. It verifies:

- manifest membership and every SHA-256 entry;
- text-only payloads, safe relative paths, file-size limits and secret patterns;
- exact route, component, OpenVINO source commit, run ID and run attempt;
- exact workspace/source/build/install paths;
- exact source repository, source commit, clean state and complete recursive submodules;
- exact build-document digest;
- successful configure command and generated cache controls;
- exact CMake cache digest;
- the presence of a non-empty Runtime build resource trace;
- the absence of `decision.json`, `integrity-failure.json` and a completed Runtime build command record.

The last condition is specific to this externally cancelled attempt: a normal completed or internally interrupted build must use the ordinary build-bundle validator instead.

## Local workspace requalification

The resume orchestrator treats the local workspace as untrusted mutable state. Before running CMake, it requires:

- no active `cmake`, `MSBuild`, `cl`, `ninja` or `vctip` process;
- the workspace and required child directories to resolve beneath `C:\w5a`;
- no checked directory component to be a reparse point;
- the source repository URL and detached HEAD to match the pinned OpenVINO source;
- a clean source tree;
- complete recursive submodules;
- the pinned Windows build document to match the prior artifact hash;
- the local `CMakeCache.txt` hash to match both the prior artifact and the explicit expected digest;
- all materialised CMake controls to match the reviewed CPU, Python, frontend and disabled-surface configuration;
- the install directory to be absent or empty before the resumed install.

Any mismatch becomes an integrity failure. No workspace is cleaned, reset, rewritten or silently replaced.

## Incremental build and controlled deadline

The resume uses the same reviewed command:

```text
cmake --build <existing b-ov> --config Release --parallel 1 --verbose -- /p:CL_MPCount=1
```

CMake/MSBuild may reuse valid completed object files from the existing build directory.

The process adapter gains an optional elapsed-time boundary. The resume orchestrator establishes an eleven-hour internal deadline and passes the remaining seconds to monitored native commands. If the boundary is reached, the resource sampler terminates descendants before the root process, records the reason, returns control to the orchestrator and allows it to write:

```text
status: Infrastructure interrupted
```

The twelve-hour GitHub job limit therefore remains an outer emergency boundary, not the normal mechanism for stopping the build.

Memory and commit thresholds remain unchanged:

- minimum available memory: `1.5 GiB` for five consecutive two-second samples;
- maximum Windows commit: `90%` for five consecutive two-second samples;
- compiler-level and native build parallelism: `1`.

## Completion and evidence

After a successful incremental build, the resume orchestrator:

1. installs to the existing workspace’s `i-ov` directory;
2. verifies the ONNX and TensorFlow conversion-extension headers required by the pinned GenAI tokenizer source;
3. records current TBB dependency hashes;
4. hashes installed binaries in place without copying executable payloads into the artifact;
5. writes a normal Route A Runtime `decision.json`;
6. keeps all model, activation, packed-storage, performance and quality authorisations `false`;
7. writes a complete SHA-256 manifest.

The resulting artifact is newly bound to the new workflow run and attempt. The previous timeout artifact remains historical evidence and is never rewritten or reclassified as passed.

## Testing strategy

The repair is implemented test-first.

1. Python contract tests first require the dedicated workflow, exact inputs, read-only guards, 720-minute job, prior-artifact download, same-run evidence upload and hosted validation.
2. Python validator tests build synthetic timeout bundles and verify success plus rejection of hash drift, identity drift, unexpected decisions, unsafe payloads, changed cache identity and wrong workspace paths.
3. PowerShell contract tests require the resume script’s path/source/cache/process checks and unchanged compiler limits.
4. A PowerShell executable test runs a deliberately sleeping child with a short process-adapter deadline and requires a recorded controlled stop instead of an externally cancelled step.
5. The complete Workbook 05 gate and normal WinUI application regression run on the exact final branch head.
6. After merge, the live controlled-resume workflow is dispatched and its resulting artifact is independently inspected before Runtime acceptance.

## Non-goals

This repair does not:

- increase compiler concurrency;
- weaken memory, commit, path, source or artifact validation;
- change OpenVINO Runtime or GenAI source commits;
- modify the old timeout artifact or accepted narrow Runtime;
- download or run a Granite model;
- activate TurboQuant, PolarQuant or QJL;
- produce inference performance, KV-cache or P1–P6 quality measurements;
- modify WinUI application production code;
- change Route B.

## Acceptance criteria

Repository acceptance requires all new regressions, the complete Workbook 05 gate and the normal application test suite to pass on one exact head.

Live Runtime acceptance additionally requires:

- prior artifact identity and timeout-bundle validation passed;
- local workspace requalification passed;
- build exit code `0`;
- install exit code `0`;
- no resource or elapsed-time stop;
- both required frontend headers present;
- at least one installed binary record;
- `decision.json` status `Passed`;
- independent hosted bundle validation passed;
- downloaded ZIP SHA-256 matches GitHub’s recorded digest.
