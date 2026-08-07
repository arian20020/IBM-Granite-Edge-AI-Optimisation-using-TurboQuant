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
- [x] The pinned GenAI BUILD.md permits the explicit Windows `OpenVINO_DIR`, `PYTHONPATH`, `OPENVINO_LIB_PATHS` and `PATH` setup used by the orchestrator.
- [x] Route B configure and narrow-target prerequisites are explicit but disabled by the blocked decision.
- [x] Unrecorded deviations are prohibited.

## Windows and resource controls

- [x] Python path/version, CMake path, generator, platform, configuration and parallelism are exact.
- [x] Short external roots `C:\w5a` and `C:\w5b` are required.
- [x] Existing run directories are rejected rather than silently reused or cleaned.
- [x] Git long-path handling is repository-scoped only.
- [x] Workflow checkout is sparse to avoid unrelated Windows path failures while still including every data/document family consumed by the full Workbook 05 suite.
- [x] Each self-hosted job has a 360-minute timeout.
- [x] Resource sampling interval is two seconds.
- [x] Low-memory, high-commit and heartbeat stop thresholds are explicit.
- [x] Resource summaries carry route/component/command identity and `record_type=build-resource-summary`, allowing hosted schema validation.
- [x] Runtime and GenAI resource-safety terminations are classified as `Infrastructure interrupted`, not as captured upstream source failures.
- [x] Runner disconnects are classified separately from captured source-build failures.

## Evidence contracts

- [x] Command records preserve executable, argument array, working directory, environment allowlist, timestamps, exit code and log paths.
- [x] Deviation, dependency, resource, binary, compatibility and decision records are schema-controlled.
- [x] `binaries.json` and `dependencies.json` collections are expanded item-by-item and independently schema/identity validated.
- [x] Artifact containment checks apply to semantic evidence paths, not legitimate absolute machine-tool metadata such as `python_path`, `git_path` or `cmake_path`.
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
- [x] Strict UTF-8/control-byte integrity tests cover all new Phase 2 PowerShell controls.
- [x] The original corrupted Runtime/module blobs reproduce the intended source-integrity failure condition.
- [x] The repaired Runtime/module sources are stored by GitHub as ordinary readable UTF-8 text.
- [x] Focused contracts cover typed resource summaries, JSON record collections, machine-path semantics and GenAI safety-stop classification.
- [x] Zero-test or skipped-test green outcomes cannot pass Route B.
- [x] Exact implementation head `838df2253e92869ac8ac8413f24b7b5b4fa7f8ca` passed the complete Windows repository gate in workflow run `31141888900`, job `92753405696`: full Workbook 05 discovery ran 212 tests with 212 passing; the explicit workflow/security rerun ran 15 tests with 15 passing; the gate emitted `WORKBOOK05_BUILD_STAGE_GATE_PASS`.
- [x] The same exact implementation head passed the independent WinUI workflow in run `31141886760`, job `92753362124`: application build succeeded, packaged unit/UI-thread execution ran 134 tests with 134 passing, zero errors, and one non-fatal `NETSDK1198` publish-profile warning.
- [x] WinUI test-result artifact `unit-test-results-31141886760-1`, artifact ID `8980148692`, was uploaded with SHA-256 `561130c4f0ff1bd1ce8f678452bc8c2ab147d980d76a6e2062a2ba33ef6206b5`.
- [ ] Route A Runtime live build bundle passes hosted validation.
- [ ] Route A GenAI live build bundle passes hosted validation or records a truthful reviewed blocker.

## Pull-request boundary review

- [x] PR #50 currently changes 34 repository files, all within workflow, documentation, Workbook 05 schemas/manifests, testing scripts, and tests.
- [x] PR #50 adds no WinUI application production source file.
- [x] PR #50 commits no external OpenVINO source tree or source file.
- [x] PR #50 commits no executable, library, archive, wheel, model, GGUF, safetensors, or other binary/model payload.
- [x] GitHub reports no inline review threads on PR #50.
- [x] The stacked draft/merge boundary remains unchanged; this checklist does not authorise merging any dependent PR.

## GitHub Actions recovery evidence

- [x] Earlier GitHub Actions runner-allocation/webhook failures were kept separate from repository failures.
- [x] After service recovery, exact-head CI reproduced and isolated the PowerShell interpolation defect and incomplete sparse checkout rather than treating them as infrastructure failures.
- [x] The interpolation defect was repaired with a RED/GREEN regression contract.
- [x] The sparse-checkout defect was repaired using the already-proven source-admission workflow as the comparison pattern, with a RED/GREEN regression contract covering every full-suite input family.
- [x] Subsequent exact implementation-head Windows and WinUI runs completed successfully as recorded above.

## Scientific claim boundary

- [x] A build does not prove QJL or PolarQuant activation.
- [x] A build does not prove packed K/V allocation or no fallback.
- [x] A build does not prove memory reduction, context improvement or speed.
- [x] A build does not prove Granite compatibility or acceptable quality.
- [x] PagedAttention and prefill compression remain outside scope.
- [x] Route B remains experimental and is never presented as official OpenVINO support.

## Current decision

Task 8 has passed its implementation-head repository gate, WinUI regression, and PR-boundary review. This checklist-only commit records those results but does not itself alter executable behaviour. Fresh CI for the resulting documentation head is still required before the final Task 8 closure is claimed.

Task 9 live Route A Runtime/GenAI execution remains open. The reviewed workflow exposes those stages through `workflow_dispatch`; the safe deployment/dispatch boundary must be available before either stage is run. No Granite model, activation, packed-storage, performance, or quality work is authorised by Task 8.

Task 10 remains closed as `Blocked` from the available BR8 evidence.
