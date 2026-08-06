# Workbook 05 Phase 2 Documented-Build Review Checklist

This checklist is the B1/R9 review gate for the Phase 2 documented-build specification and implementation. It does not claim that a live OpenVINO build has completed.

## Source and route boundary

- [x] Route A Runtime is pinned to `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- [x] Route A GenAI is pinned to `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- [x] Route B Runtime remains pinned to `EgorDuplensky/openvino@1827f6458d049de11c1a8203c793af67c99935dc` plus the reviewed one-file repair.
- [x] Route A and Route B have separate source, build, install and evidence roots.
- [x] Route B requires an independently validated `ExecutableCandidate` artifact and project-owner acceptance.
- [x] The available BR8 evidence does not satisfy that prerequisite.
- [x] Route B is recorded as `Blocked` for Phase 2.
- [x] Route A can continue independently.

## Controlling documents and commands

- [x] Runtime instructions are tied to `docs/dev/build_windows.md` at the exact Runtime commit.
- [x] GenAI instructions are tied to `src/docs/BUILD.md` at the exact GenAI commit.
- [x] Source acquisition freezes exact detached commits and recursive submodules.
- [x] Runtime configure, build and install command arrays are explicit.
- [x] GenAI configure, build and install command arrays are explicit.
- [x] GenAI uses a fresh workspace while consuming an accepted Runtime installation read-only.
- [x] Route B configure and narrow-target prerequisites are explicit but disabled by the blocked decision.
- [x] Unrecorded deviations are prohibited.

## Windows and resource controls

- [x] Python path/version, CMake path, generator, platform, configuration and parallelism are exact.
- [x] Short external roots `C:\w5a` and `C:\w5b` are required.
- [x] Existing run directories are rejected rather than silently reused or cleaned.
- [x] Git long-path handling is repository-scoped only.
- [x] Workflow checkout is sparse to avoid unrelated Windows path failures.
- [x] Each self-hosted job has a 360-minute timeout.
- [x] Resource sampling interval is two seconds.
- [x] Low-memory, high-commit and heartbeat stop thresholds are explicit.
- [x] Runner disconnects are classified separately from captured source-build failures.

## Evidence contracts

- [x] Command records preserve executable, argument array, working directory, environment allowlist, timestamps, exit code and log paths.
- [x] Deviation, dependency, resource, binary, compatibility and decision records are schema-controlled.
- [x] Binary records contain in-place hashes and never copy binaries into evidence.
- [x] Build decisions keep all model, activation, storage, performance and quality authorisations false.
- [x] Every complete build bundle requires a deterministic SHA-256 manifest.
- [x] Route B closure records exactly what the incomplete BR8 artifact proves and does not prove.

## Workflow security

- [x] The workflow is manual and same-repository only for live build stages.
- [x] Permissions are limited to `contents: read` and `actions: read`.
- [x] Actions are pinned by full commit SHA.
- [x] Checkout uses `persist-credentials: false` and sparse paths.
- [x] Self-hosted jobs require exact Intel labels.
- [x] Independent hosted validators download the exact run-ID/attempt artifact.
- [x] Hosted validators treat artifacts as untrusted data and never execute them.
- [x] `pull_request_target`, write permissions, model execution and merge automation are prohibited.
- [x] Python schema dependencies are installed from the pinned requirements file into runner-temporary storage and `PYTHONPATH` is restored.

## Artifact boundary

- [x] Uploaded artifacts permit text, JSON, CSV, Markdown and manifests only.
- [x] Executables, libraries, archives, wheels, model files, source trees, build trees and install trees are prohibited.
- [x] Secret-pattern and unsafe-path scans are required.
- [x] Large source/build/install trees remain outside Git and outside artifacts.

## Testing and verification

- [x] Behavioural changes were introduced through failing contract tests.
- [x] Valid and adversarial bundle fixtures exist.
- [x] Static PowerShell forbidden-command tests exist.
- [x] Workflow-contract tests exist.
- [x] A repository-level build-stage gate exists.
- [x] The actual PowerShell module is imported on Windows by the gate.
- [x] Full Workbook 05 regression and WinUI regression are required before R9 closure.
- [x] Zero-test or skipped-test green outcomes cannot pass Route B.
- [ ] Exact-head Windows repository gate passes.
- [ ] Exact-head WinUI build/test passes.
- [ ] Route A Runtime live build bundle passes hosted validation.
- [ ] Route A GenAI live build bundle passes hosted validation or records a truthful reviewed blocker.

## Scientific claim boundary

- [x] A build does not prove QJL or PolarQuant activation.
- [x] A build does not prove packed K/V allocation or no fallback.
- [x] A build does not prove memory reduction, context improvement or speed.
- [x] A build does not prove Granite compatibility or acceptable quality.
- [x] PagedAttention and prefill compression remain outside scope.
- [x] Route B remains experimental and is never presented as official OpenVINO support.

## Current decision

Task 8 implementation review is complete at the document and code-contract level, but its exact-head Windows verification remains open. Task 9 live Route A execution remains open. Task 10 is closed as `Blocked` from the available BR8 evidence.
