# Workbook 05 Phase 3 Clean Dependency-Preflight Enablement Design

**Status:** Proposed design for project-owner review  
**Date:** 2026-08-16  
**Campaign:** `GTQ-WB05-MF-v1`  
**Route:** `route-a-merged-openvino`  
**Base revision:** `caa3250a772628ac4bb0b9b33f0d6a89ac716a3e`  
**Design branch:** `design/workbook-05-phase3-dependency-preflight`

## 1. Purpose

This change enables the next controlled Phase 3 checkpoint: a **real, clean Windows dependency preflight** for the reviewed IBM Granite 4.1 3B OpenVINO conversion toolchain.

The preflight will create a fresh Python 3.12.10 environment on the approved Lenovo self-hosted runner, resolve and install the exact reviewed dependency candidate, prove the identities of every source and ordinary package artifact, run no-model compatibility checks, and upload a text-only evidence bundle for independent validation on a clean GitHub-hosted runner.

This is deliberately narrower than model acquisition or conversion. It must not:

- contact the IBM Granite model repository;
- download model or tokenizer files;
- create OpenVINO model IR;
- load or execute a model;
- activate TurboQuant, QJL, PolarQuant, or scalar KV-cache quantisation;
- authorise any storage, performance, context-length, perplexity, or quality claim;
- automatically enable `live-asset-lock`.

A successful preflight means only that the conversion dependency environment is a reproducible, independently checked candidate for the later C1 asset-lock run.

## 2. Verified starting point

The accepted starting point is the merged `main` revision:

```text
caa3250a772628ac4bb0b9b33f0d6a89ac716a3e
```

The first post-merge Phase 3 rehearsal completed successfully as:

```text
Workflow: Workbook 05 Phase 3 assets
Run:      31915564397
Attempt:  1
Operation: offline-fixture
Artifact: workbook-05-phase3-assets-31915564397-1
SHA-256:  1e65a0607300207bfde6c19e25bc5d6d3077c10cef87d5e7c8cad08ac534e74a
```

That result proved the repository gate, self-hosted collection boundary, text-only artifact production, and hosted untrusted-data validation. It did not perform a live dependency installation or model operation.

The following current controls are retained:

- `Invoke-Workbook05Phase3DependencyPreflight.ps1` remains the repository-safe **offline fixture**;
- `workbook-05-phase3-assets.yml` continues to reject `live-asset-lock`;
- the accepted Phase 2 Runtime and GenAI installations under `C:\w5a` remain read-only;
- every later scientific-authorisation flag remains `false`;
- no generated package, wheel, model, tokenizer, IR, executable, DLL, or archive is committed to the repository or uploaded as evidence.

## 3. Approaches considered

### 3.1 Add dependency preflight as a third operation in the existing asset workflow

This would add an operation such as `live-dependency-preflight` beside `offline-fixture` and `live-asset-lock` in `.github/workflows/workbook-05-phase3-assets.yml`.

**Advantages**

- fewer workflow files;
- one familiar Actions entry point.

**Disadvantages**

- mixes package-environment preparation with model-asset acquisition;
- makes the already-sensitive live asset workflow harder to review;
- increases the risk that a dependency result is accidentally treated as model authorisation;
- conflicts with the existing offline fixture’s strict no-network/no-install static contract.

This approach is rejected.

### 3.2 Create a separate dependency-preflight workflow and a separate live collector

This introduces a dedicated manual workflow for dependency qualification while leaving the asset workflow blocked.

**Advantages**

- one workflow has one scientific and security purpose;
- the existing offline fixture remains unchanged and demonstrably network-free;
- pull requests can run repository contracts without reaching the laptop;
- live package activity is isolated from model paths and model commands;
- the resulting decision can be independently accepted before a later binding change enables asset acquisition.

**Disadvantages**

- adds one workflow and one live PowerShell entry point;
- requires an explicit owner-acceptance handoff before the asset workflow can use the result.

This is the selected approach.

### 3.3 Run a local PowerShell script manually outside GitHub Actions

This would create the environment directly on the Lenovo and then upload or copy evidence manually.

**Advantages**

- simple to start locally.

**Disadvantages**

- weaker binding to an exact repository revision, workflow run, and attempt;
- easier to omit evidence or accidentally reuse state;
- no automatic independent hosted validation;
- harder to distinguish operator error from repository-controlled execution.

This approach is rejected.

## 4. Selected architecture

The selected design has three independent workflow boundaries:

```text
GitHub-hosted repository contract
              ↓
Lenovo self-hosted live dependency collection
              ↓
GitHub-hosted untrusted-data validation
```

### 4.1 Repository contract job

The pull-request and manual-dispatch entry job will:

- run only on a GitHub-hosted Windows runner;
- check out the exact immutable PR head or manual `main` SHA;
- use read-only `contents` and `actions` permissions;
- use actions pinned to full commit SHAs;
- install only the repository’s small pinned schema-validation dependency outside the checkout;
- run the complete `Validate-Workbook05-Phase3.ps1` gate;
- run focused static and simulated tests for the live collector and workflow;
- contain no model path, package credentials, self-hosted access, or live package installation.

Pull requests will stop here. They must never reach the Lenovo runner.

### 4.2 Self-hosted collection job

A manual dispatch from `main` will require an explicit Boolean confirmation, defaulting to `false`:

```text
confirm_live_dependency_preflight: true
```

No free-form workspace identity will be accepted from the operator. The workflow will derive the attempt identity from immutable GitHub values:

```text
dependency-preflight-<github.run_id>-<github.run_attempt>
```

The collector will target only the existing approved labels:

```text
self-hosted
Windows
X64
workbook05
intel-target
```

It will use process-scoped Windows PowerShell with `-NoProfile`, keep repository permissions read-only, and use `cancel-in-progress: false` so a newer dispatch cannot silently erase an earlier attempt.

### 4.3 Hosted validator job

The final job will:

- run on a fresh GitHub-hosted Windows runner;
- check out the same exact repository revision;
- download only the artifact from the same workflow run and attempt;
- treat every downloaded member strictly as untrusted data;
- execute no file from the artifact;
- run `dependency_bundle_validation` with `--require-passed`;
- reject binary payloads, archives, wheels, executables, libraries, models, IR, links, unsafe paths, secrets, hash drift, source drift, package drift, an unrecomputable decision, or any enabled later claim.

## 5. Trust boundaries

The design uses the following explicit trust model.

### 5.1 Repository code

Repository code is trusted only after the exact-head Phase 3 gate passes. A green result from an earlier commit is not transferable to a changed head.

### 5.2 Package indexes and downloaded distributions

Package indexes are discovery and transport sources, not identity authorities. Every ordinary distribution admitted to the environment must be present in the generated hash lock and its actual installed archive SHA-256 must be recorded from pip’s installation report.

The generated lock remains the installation authority. Pip’s `--report` output is evidence of what was selected and installed; it is not treated as a lock file.

### 5.3 Git source repositories

The two VCS packages are accepted only when all of the following match:

```text
repository name
exact HTTPS origin
full reviewed commit
clean working tree
complete tracked-file manifest
aggregate source-tree SHA-256
```

A matching package version alone is insufficient.

### 5.4 Self-hosted evidence

The Lenovo is the execution environment, not the final validator. Its artifact is considered untrusted until the hosted validator recomputes the manifest, lock identity, source identities, package relationships, decision, and non-claims.

### 5.5 Owner acceptance

A green workflow creates an **acceptance candidate**, not automatic authority. The project owner must independently download and hash the artifact, inspect the decision, calculate the exact `decision.json` SHA-256, and record the retained `C:\w5c` workspace identity.

A later, separately reviewed binding change may then allow `live-asset-lock` to consume that exact accepted decision and retained workspace. Merely supplying an arbitrary 64-character string will never be sufficient.

## 6. Controlled storage layout

The live attempt will use a previously absent normal directory beneath `C:\w5c`:

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
  evidence\
    ...text-only retained evidence...
```

Rules:

- `C:\w5c` and every child in the attempt ancestry must be normal local directories;
- UNC paths, device paths, symbolic links, junctions, mount points, and other reparse points are rejected;
- an existing attempt directory is never reused, repaired, reset, or deleted automatically;
- package archives, wheels, build products, and source checkouts remain under `workspace` and are not uploaded;
- interruption preserves the attempt for diagnosis;
- a retry always receives a new run/attempt identity.

The dependency preflight will not read from or write to `C:\w5m`, and it will not modify accepted material under `C:\w5a`.

## 7. Reviewed dependency candidate

The direct candidate remains exactly:

```text
optimum-intel @ git+https://github.com/huggingface/optimum-intel.git@a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0
optimum @ git+https://github.com/huggingface/optimum.git@982e495540364f95da1e4b6f62d2d4e5907d08fd
transformers==5.5.0
huggingface-hub==1.21.0
nncf==3.2.0
openvino==2026.2.1
openvino-tokenizers==2026.2.1.0
```

The order, versions, origins, or commits may not drift automatically. A change to this set is a new candidate and requires a separate design and review.

## 8. Bootstrap and lock generation

### 8.1 Fresh interpreter boundary

The workflow first verifies the machine interpreter is exactly Python 3.12.10, then creates a new virtual environment inside the attempt workspace. The machine-wide Python installation is never modified.

The final evidence records:

- Python version;
- Python executable path and SHA-256;
- pip version;
- pip executable path and SHA-256.

### 8.2 Hash-locked bootstrap tools

The live collector needs `pip-tools==7.6.0` to generate the ordinary-distribution lock. The generator environment also pins `pip==26.1.2`, the latest patch in the pip 26.1 line that `pip-tools` 7.6.0 explicitly supports. A newer pip line is not admitted automatically because that would place the resolver outside the reviewed compatibility boundary. Installing the lock generator itself must not become an unrecorded dependency gap.

The implementation will therefore add a small repository-controlled bootstrap input containing exactly `pip-tools==7.6.0` and `pip==26.1.2`, plus a generated lock containing exact versions and SHA-256 hashes for every transitive ordinary dependency. The bootstrap installation will use pip hash-checking mode and will be recorded in text logs. No bootstrap package is admitted through an unpinned name-only request. The official wheel identities used to create the lock are `4bd99155b6d8de358a214b0865e1a2855a453570c1a83d40f7b564870b8657be` for `pip-tools` and `382ff9f685ee3bc25864f820aa50505825f10f5458ffff07e30a6d96e5715cab` for pip.

### 8.3 Source metadata validation before installation

Before resolving ordinary packages, the collector will read dependency and build-system metadata from the two immutable local source trees as data. Repository Python validation will compare the observed declarations to the reviewed source contract.

This prevents a pinned commit from silently introducing an unexpected dependency or build backend while still appearing to have the expected package name.

### 8.4 Ordinary-distribution lock

The collector will generate a new lock on the target Windows/Python environment using `pip-tools==7.6.0`, `pip==26.1.2`, and `--generate-hashes`. This matters because environment markers and compatible wheel selections can vary by Python and platform. Both generator versions are evidence-bearing configuration, not incidental machine state.

The input to lock generation will include:

- the exact five direct ordinary package pins;
- the reviewed runtime requirements extracted from the two pinned source trees;
- the reviewed build-system requirements required for local source installation.

The two VCS projects themselves are excluded from the ordinary lock and remain separately bound to their Git identities and complete source manifests.

The lock parser will reject:

- URLs, VCS lines, editable installs, alternate indexes, or moving references;
- a package without an exact `name==version` pin;
- a package without at least one SHA-256;
- duplicate canonical package names;
- a direct package version that differs from the reviewed candidate.

## 9. Installation sequence

The live collector retains the approved causal order:

```text
workspace-validation
source-verification
lock-generation
normal-install
vcs-install
imports
cli-help
no-model-compatibility
record-generation
manifest-generation
```

### 9.1 Normal distributions

The ordinary lock is installed into the fresh environment using pip hash-checking mode. The command will use an argument array rather than a command string and will produce a JSON installation report.

The repository validator will require every installed ordinary package to:

- appear in the lock;
- have the same version as the lock;
- carry an actual archive SHA-256 in the installation report;
- match one allowed SHA-256 from the lock.

Unexpected, duplicate, unhashed, or version-drifted distributions cause `IntegrityFailure`.

### 9.2 VCS packages

`optimum` and `optimum-intel` are installed from their already-verified local source directories. Dependency resolution is disabled. Build isolation is disabled so pip cannot fetch unrecorded build requirements behind the reviewed ordinary lock.

After installation, the collector records and checks:

- installed distribution names and versions;
- exact source commits;
- local source directories;
- clean source state and source-tree aggregate hashes.

The VCS packages must never appear as ordinary index distributions in the normal installation report.

## 10. Process execution and logging

All native commands will use the repository’s existing argument-preserving process adapter rather than `Invoke-Expression`, shell command strings, or unstructured `Start-Process` calls.

Each command record will contain:

- a stable command ID;
- exact executable path;
- an ordered argument array;
- working directory;
- allowlisted environment observations only;
- start/end timestamps;
- exit code;
- portable stdout/stderr evidence paths.

Secrets and complete inherited environments are never recorded. Git operations use public HTTPS origins without credentials. A non-zero exit code is preserved as the first causal result and cannot be hidden by a later successful command.

## 11. Required live checks

A `Passed` dependency decision requires all six checks in this exact order:

```text
resolver
install
imports
cli_help
no_model_compatibility
remote_code_disabled
```

### 11.1 Resolver

Proves the exact direct set produced one complete, hash-locked ordinary-distribution lock on the target environment.

### 11.2 Install

Proves the ordinary lock was installed with hashes enforced, the two local VCS packages were installed without dependency resolution, and `pip check` exited with code `0`.

### 11.3 Imports

Starts a new Python process and imports exactly:

```text
optimum
optimum.intel
transformers
nncf
openvino
```

The result records module file locations and installed distribution versions. Every imported module must resolve inside the fresh environment rather than a machine-wide package directory.

### 11.4 CLI help

Runs the exact environment-local `optimum-cli.exe --help` and requires exit code `0`.

### 11.5 No-model compatibility

Runs a repository-controlled Python check that imports the installed toolchain, validates the reviewed Optimum Intel dependency declarations, and constructs the approved OpenVINO conversion argument list without downloading or opening a model.

### 11.6 Remote code disabled

Inspects the structured conversion argument list as data and proves that `--trust-remote-code` is absent and the request’s `trust_remote_code` field is `false`.

No check may contact the Granite repository or execute an inference backend.

## 12. Evidence bundle

The uploaded artifact is text-only and uses the identity:

```text
workbook-05-phase3-dependency-preflight-<run>-<attempt>
```

At minimum it contains:

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
reports/normal-install-report.json
reports/vcs-packages.json
sources/optimum.json
sources/optimum-intel.json
sources/optimum-files.csv
sources/optimum-intel-files.csv
commands/command-index.json
logs/*.stdout.txt
logs/*.stderr.txt
```

The source CSV files contain only portable relative paths, sizes, and SHA-256 values. They do not contain source code. The bundle contains no package archive, wheel, source archive, executable, library, model file, tokenizer file, or OpenVINO IR.

`manifest.sha256` is written last. Every JSON record is written to a same-directory temporary file, validated, flushed, and atomically moved to its final name. A successful run contains no `*.tmp` files.

## 13. Decision semantics

The final decision uses the existing closed `conversion-dependency-preflight` schema.

### 13.1 `Passed`

All source identities, lock relationships, package identities, and required no-model checks passed.

A `Passed` decision still has:

```text
model_download_authorised: false
granite_model_test_authorised: false
activation_claim_authorised: false
packed_storage_claim_authorised: false
performance_claim_authorised: false
quality_claim_authorised: false
```

### 13.2 `Blocked`

The evidence is structurally trustworthy, but at least one executable compatibility check did not pass. Examples include `pip check`, imports, or CLI help returning a non-zero exit code.

### 13.3 `IntegrityFailure`

An identity or evidence relationship cannot be trusted. Examples include the wrong commit, dirty source, lock mismatch, unhashed artifact, package drift, unsafe path, decision disagreement, or unexpected payload.

### 13.4 `InfrastructureInterrupted`

GitHub runner disconnection, power loss, or an externally terminated process is not an algorithm or package-compatibility failure. The workflow may be red while the retained `failure.json` classifies the first proven cause as infrastructure interruption.

## 14. Failure and recovery

On any failure:

1. preserve the attempt workspace and any text evidence already written;
2. do not create a positive manifest or `Passed` decision;
3. write one atomic `failure.json` with every later claim disabled;
4. record the completed stage sequence and first causal message;
5. do not reset, clean, repair, or delete either source tree automatically;
6. do not rerun immediately without inspecting the first failing command and retained logs;
7. use a new run/attempt identity for the next execution.

The implementation must distinguish package incompatibility from infrastructure interruption and evidence-integrity failure.

## 15. Test-first implementation strategy

Implementation will follow a red-green-refactor sequence.

### 15.1 Workflow and static contracts

Tests will first fail until the new workflow proves:

- pull requests cannot reach the self-hosted collector;
- manual collection is restricted to `main` and explicit confirmation;
- exact runner labels and timeouts are present;
- permissions remain read-only;
- actions are pinned to full SHAs;
- the same exact revision and same-attempt artifact flow through all jobs;
- no model operation or binary artifact upload is present.

### 15.2 Live collector simulation

The live collector will expose a test seam that substitutes deterministic fake Git, pip, import, and CLI process results while exercising the real orchestration, path checks, atomic writes, stage order, and decision construction.

Tests will cover:

- successful simulated collection creates a recomputable `Passed` text bundle;
- wrong source origin or commit fails before package installation;
- dirty or linked source trees are rejected;
- an existing workspace is preserved and rejected;
- lock hash drift becomes `IntegrityFailure`;
- an unreported or unexpected installed distribution is rejected;
- VCS installation without `--no-deps` or with build isolation is rejected by contract;
- import or CLI failure becomes `Blocked` rather than `Passed`;
- `--trust-remote-code` is rejected before process execution;
- injected interruption preserves `failure.json` and omits the acceptance manifest;
- no temporary file remains after success.

### 15.3 Hosted validator adversarial tests

Adversarial fixtures will prove rejection of:

- changed or duplicate manifest entries;
- path traversal and case-colliding paths;
- secrets;
- links and non-regular files;
- wheels, archives, executables, libraries, model files, and IR;
- wrong source identities;
- an altered lock or pip report;
- an unrecomputable `Passed` decision;
- any true model or scientific-authorisation flag.

### 15.4 Full regression gates

Before merge, one exact PR head must pass:

```text
Build and test
Workbook 05 documented build
Workbook 05 Route A Runtime controlled resume contract
Workbook 05 Phase 3 assets — repository contract
Workbook 05 Phase 3 dependency preflight — repository contract
```

The existing offline fixture must continue to pass unchanged.

## 16. Planned repository changes

The implementation plan is expected to add or modify the following surfaces.

### New files

```text
.github/workflows/workbook-05-phase3-dependency-preflight.yml
scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1
scripts/testing/workbook05/requirements.phase3-bootstrap.in
scripts/testing/workbook05/requirements.phase3-bootstrap.txt
scripts/testing/workbook05/phase3/dependency_no_model_check.py
tests/testing/workbook05/Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1
tests/testing/workbook05/test_phase3_dependency_preflight_workflow_contract.py
tests/testing/workbook05/test_phase3_dependency_preflight_live_contract.py
docs/testing/workbook05/phase3-dependency-preflight-runbook.md
```

### Existing files likely updated

```text
scripts/testing/Validate-Workbook05-Phase3.ps1
scripts/testing/workbook05/phase3/dependency_bundle_validation.py
experiments/granite_turboquant_intel/configurations/workbook05/phase3-asset-lock-settings.json
docs/testing/workbook05/phase3-asset-lock-runbook.md
docs/testing/workbook05/phase3-c1-implementation-status.md
```

The exact implementation plan may reduce this list by reusing an existing tested helper, but it must not combine the live collector with the offline fixture merely to reduce file count.

## 17. Acceptance criteria

The enablement implementation is ready to merge only when:

1. the design and implementation plan have been reviewed;
2. focused tests first demonstrate the missing live boundary;
3. all focused tests pass after implementation;
4. the complete Phase 3 gate passes on the exact current PR head;
5. the normal application regression remains green;
6. the PR changes only dependency-preflight, validation, workflow, test, and documentation surfaces;
7. no model, tokenizer, IR, package binary, executable, library, wheel, or archive is committed;
8. `live-asset-lock` remains blocked after merge.

Post-merge acceptance then requires one new manual `main` run in which:

- the self-hosted collector produces a `Passed` decision;
- the hosted validator accepts the exact same-attempt artifact with `--require-passed`;
- GitHub’s artifact SHA-256 matches an independently calculated SHA-256;
- the extracted `decision.json` SHA-256 is independently calculated and recorded;
- the exact retained `C:\w5c` workspace is recorded;
- every model and scientific-authorisation flag remains false.

Only after that owner acceptance will a separate binding design consider enabling the live Granite asset lock.

## 18. Professional-source verification

The design was checked against the current official packaging and workflow guidance:

- pip’s installation-report specification states that `pip install --report` records what was installed, while explicitly warning that the report is not itself a lock-file format. This design therefore uses the generated hash lock as authority and the report as independently cross-checked observation evidence.
- pip documents `--require-hashes` as requiring hashes for repeatable installs.
- pip-tools documents that `pip-compile --generate-hashes` creates a hash-checking lock and should run in the same target Python environment because dependency resolution can vary by environment.
- GitHub supports repository policies requiring actions to be pinned to full-length commit SHAs; this design retains the repository’s existing full-SHA action policy.

## 19. Textbook basis

- *Systems Engineering: Principles and Practice*, Chapters 16–17: qualify integration stages independently and preserve traceability from test configuration to evidence before advancing to system-level evaluation.
- *Designing Secure Software*, Chapters 3–4 and 10: minimise attack surface, use allowlists, remain secure by default, fail securely, and treat externally produced artifacts as untrusted input.
- *The Art of Unit Testing*, Chapters 7 and 10: require trustworthy regression tests and a delivery pipeline whose higher-level evidence connects to lower-level contracts.
- *Why Programs Fail*, Chapters 4, 6, and 15: reproduce one controlled condition, preserve the first causal failure, and verify that the correction removed the cause without introducing another defect.
- *Code Complete*, Chapters 22–23 and 29: use developer testing, retained debugging evidence, and incremental integration rather than combining several unverified stages.

## 20. Final design decision

Proceed with a **separate, manual-main dependency-preflight workflow and a separate live PowerShell collector**. Preserve the current offline fixture and continue to block live model asset acquisition. Produce a text-only, digest-bound, independently validated dependency decision, then require explicit owner acceptance before any later workflow can consume it.
