# Workbook 05 Phase 2 Documented-Build Review Checklist

This checklist is the B1 approval gate for the Phase 2 documented-build specification and implementation plan. It does not authorise compilation until the project owner accepts the reviewed plan.

## Source and route boundary

- [x] Route A Runtime is pinned to `openvinotoolkit/openvino@b9a1f201c109e0bed74763934f79483cf6c4cbf4`.
- [x] Route A GenAI is pinned to `openvinotoolkit/openvino.genai@05e5c7670b597746f858946974d11f38e3baf42f`.
- [x] Route B Runtime remains pinned to `EgorDuplensky/openvino@1827f6458d049de11c1a8203c793af67c99935dc` plus the reviewed one-file repair.
- [x] Route A and Route B have separate source, build, install and evidence roots.
- [x] Route B is disabled unless BR8 is independently validated as `ExecutableCandidate` and accepted by the project owner.
- [x] Route A can continue independently when Route B is blocked.

## Controlling documents and commands

- [x] Runtime instructions are tied to `docs/dev/build_windows.md` at the exact Runtime commit.
- [x] GenAI instructions are tied to `src/docs/BUILD.md` at the exact GenAI commit.
- [x] Source acquisition preserves the documented clone/submodule/configure/build order while freezing exact detached commits.
- [x] Runtime configure, build and install commands are written explicitly.
- [x] GenAI configure, build and install commands are written explicitly.
- [x] Route B configure and narrow-target prerequisites are written explicitly.
- [x] Pre-approved deviations are enumerated and no other deviation may execute without an approved record.

## Windows and resource controls

- [x] Python path/version, CMake path, generator, platform, configuration and parallelism are exact.
- [x] Short external roots `C:\w5a` and `C:\w5b` are required.
- [x] Existing run directories are rejected rather than silently reused or cleaned.
- [x] Git long-path handling is repository-scoped only.
- [x] Each self-hosted job has a 360-minute timeout.
- [x] Resource sampling interval is two seconds.
- [x] Low-memory, high-commit and heartbeat stop thresholds are explicit.
- [x] Runner disconnects are classified separately from captured source-build failures.

## Evidence contracts

- [x] Command records preserve executable, argument array, working directory, environment allowlist, timestamps, exit code and log paths.
- [x] Deviation records preserve source document, original step, exact proposed change, reason, risk and approval.
- [x] Dependency records include path, source, version/hash and purpose.
- [x] Build resource summaries include process-tree and machine-memory evidence.
- [x] Binary records contain in-place hashes and state that binaries are not copied to the artifact.
- [x] Compatibility attempts preserve retained/rejected reasons.
- [x] Build decisions keep all model, activation, storage, performance and quality authorisations false.
- [x] Every evidence bundle has a deterministic SHA-256 manifest.

## Workflow security

- [x] The planned workflow is manual and same-repository only.
- [x] Permissions are limited to `contents: read` and `actions: read`.
- [x] Actions are pinned by full commit SHA.
- [x] Checkout uses `persist-credentials: false`.
- [x] Self-hosted jobs require exact Intel labels.
- [x] Independent hosted validators download the exact run-ID/attempt artifact.
- [x] Hosted validators treat artifacts as untrusted data and never execute them.
- [x] `pull_request_target`, write permissions, model execution and merge automation are prohibited.

## Artifact boundary

- [x] Uploaded artifacts permit text, JSON, CSV, Markdown and manifests only.
- [x] EXE, DLL, LIB, PDB, wheel, archive, model, source-tree, build-tree and install-tree payloads are prohibited.
- [x] Secret-pattern and unsafe-path scans are required.
- [x] Binary output metadata contains hashes only.
- [x] Large source/build/install trees remain outside Git and outside artifacts.

## Testing and verification

- [x] Every behavioural task begins with a failing test.
- [x] Valid and adversarial bundle fixtures are planned.
- [x] Static PowerShell forbidden-command tests are planned.
- [x] Workflow-contract tests are planned.
- [x] A repository-level build-stage gate is planned.
- [x] Full Workbook 05 regression and WinUI regression are required before R9 closure.
- [x] Zero-test or skipped-test green outcomes cannot pass Route B.
- [x] Review threads, changed-file boundaries, secrets and payload suffixes are checked before readiness.

## Scientific claim boundary

- [x] A build does not prove QJL or PolarQuant activation.
- [x] A build does not prove packed K/V allocation or no fallback.
- [x] A build does not prove memory reduction, context improvement or speed.
- [x] A build does not prove Granite compatibility or acceptable quality.
- [x] PagedAttention and prefill compression remain outside scope.
- [x] Route B remains experimental and is never presented as official OpenVINO support.

## B1 decision

- [x] Project owner approved continuing the implementation in the conversation on 2026-08-06.
- [x] Project owner approved inline execution beginning with Task 1.

Checkpoint B1 is accepted for Task 1 only. Later tasks remain subject to their own red-green verification and checkpoint evidence. No OpenVINO compilation or model execution is authorised by this checklist update alone.
