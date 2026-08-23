# Workbook 05 Phase 3 Clean Dependency-Preflight Runbook

## Purpose

This runbook explains how to qualify the exact Python conversion environment that will later be used to acquire and convert the selected IBM Granite 4.1 3B model.

The dependency preflight is deliberately separate from model acquisition. A successful result proves that the reviewed conversion dependencies can be reproduced on the controlled Windows Intel laptop and that the resulting text evidence can be independently validated. It does **not** prove that a Granite model can be downloaded, converted, loaded, or executed.

## What this checkpoint proves

A `Passed` result proves all of the following for one exact workflow run and attempt:

- the repository contract passed on the exact checked-out commit;
- Python is exactly `3.12.10`;
- the committed bootstrap lock is unchanged;
- the bootstrap environment was created separately from the final conversion environment;
- the reviewed Optimum and Optimum Intel repositories were acquired at their exact commits;
- both source trees had the reviewed HTTPS origin, detached commit, clean status, tracked-file catalogue, and aggregate SHA-256;
- both source trees matched the repository-controlled data-only metadata contract;
- the ordinary Windows/Python dependency lock was generated with `pip-tools==7.6.0`;
- ordinary distributions were installed with `--require-hashes` and their pip report matched the lock;
- Optimum and Optimum Intel were installed from the verified local source trees with `--no-deps --no-build-isolation`;
- `pip check`, fresh-process imports, `optimum-cli --help`, the no-model compatibility check, and the remote-code-disabled check passed;
- every command, stage, package, source, lock, report, and decision relationship was retained as text evidence;
- a clean GitHub-hosted runner validated the exact same-attempt artifact strictly as untrusted data.

## What this checkpoint does not prove

Even after a successful and accepted run, all of the following remain unproved and unauthorised:

```text
Granite repository access
model or tokenizer download
OpenVINO model conversion
model loading or token generation
CPU or GPU inference placement
TurboQuant activation
QJL or PolarQuant activation
packed K/V storage
memory, latency, throughput, or context results
perplexity or P1–P6 quality results
live-asset-lock enablement
```

A separate reviewed change must later bind the exact accepted dependency decision and retained workspace to the blocked `live-asset-lock` operation.

## Fixed identities

| Component | Fixed identity |
|---|---|
| Campaign | `GTQ-WB05-MF-v1` |
| Route | `route-a-merged-openvino` |
| Base Python | `C:\Program Files\Python312\python.exe` |
| Python version | `3.12.10` |
| Bootstrap generator | `pip-tools==7.6.0` |
| Bootstrap pip | `pip==26.1.2` |
| Bootstrap lock SHA-256 | `44fec5a5c2b7fb0cd0aa36f52af1d664f65812f1b112ec1e78bc00c3c9ae288f` |
| Optimum origin | `https://github.com/huggingface/optimum.git` |
| Optimum commit | `982e495540364f95da1e4b6f62d2d4e5907d08fd` |
| Optimum version | `2.3.0` |
| Optimum Intel origin | `https://github.com/huggingface/optimum-intel.git` |
| Optimum Intel commit | `a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0` |
| Optimum Intel base version | `2.2.0.dev0` |

The operator cannot replace these values through workflow inputs.

## Before running

1. Confirm PR #72 has been reviewed, approved, merged, and followed by a successful fresh `main` application regression.
2. Confirm the Lenovo runner is connected and shown as `Idle` under **Settings → Actions → Runners**.
3. Keep the laptop plugged into power and connected to stable networking.
4. Prevent Windows from sleeping during the run.
5. Close avoidable high-memory programs.
6. Confirm `C:\w5c` exists as a normal local directory and is not a link, junction, mount point, UNC path, or device path.
7. Do not create the expected attempt directory manually.
8. Do not delete or reuse an earlier attempt directory.

## Manual workflow dispatch

In GitHub:

```text
Actions
→ Workbook 05 Phase 3 dependency preflight
→ Run workflow
```

Use exactly:

| Field | Value |
|---|---|
| Use workflow from | `main` |
| `confirm_live_dependency_preflight` | Checked / `true` |

There are no operator inputs for source commits, dependency versions, workspace paths, model paths, or command strings.

## Workflow boundaries

The workflow has three jobs:

```text
repository-contract
        ↓
collect-dependencies
        ↓
validate-dependencies
```

### 1. Repository contract

Runs on a clean GitHub-hosted Windows runner. It:

- checks out the exact revision without credentials;
- installs only the pinned repository validation dependency;
- runs the deterministic dependency-preflight simulations;
- runs the complete Workbook 05 Phase 3 repository gate.

This job cannot reach the Lenovo or perform live dependency collection.

### 2. Collect dependencies

Runs on the controlled Lenovo only after the hosted repository contract passes and only for a confirmed manual dispatch from `main`.

It creates exactly:

```text
C:\w5c\dependency-preflight-<run-id>-<attempt>\
├── workspace\
│   ├── bootstrap-venv\
│   ├── environment\
│   ├── sources\
│   ├── temporary\
│   └── cache\
└── evidence\
    ├── commands\
    ├── locks\
    ├── reports\
    ├── sources\
    ├── observation.json
    ├── decision.json or failure.json
    ├── checks.json
    ├── command-index.json
    ├── stage-order.json
    ├── summary.md
    └── manifest.sha256 only after Passed
```

An existing attempt directory is rejected. It is never cleaned, reset, repaired, or reused automatically.

### 3. Validate dependencies

Runs on a second clean GitHub-hosted Windows runner. It downloads the exact same-attempt artifact and treats every member only as untrusted data.

The validator may enumerate, `lstat`, read, parse, hash, and compare artifact members. It never imports, executes, dynamically loads, or adds artifact content to `PATH` or `PYTHONPATH`.

## Fixed stage order

The Lenovo collector must complete these stages in order:

```text
1. workspace-validation
2. source-verification
3. lock-generation
4. normal-install
5. vcs-install
6. imports
7. cli-help
8. no-model-compatibility
9. record-generation
10. manifest-generation
```

A later stage cannot repair or reinterpret a failed earlier stage.

## Fixed check order

The decision records exactly:

```text
resolver
install
imports
cli_help
no_model_compatibility
remote_code_disabled
```

All six must be `Passed` for a successful decision.

## Status meanings

### `Passed`

Every required identity, lock, installation, check, record, and hosted validation passed. The result is eligible for independent owner review, but is not automatically accepted.

### `Blocked`

The intended dependency operation could not complete for a package or compatibility reason that was not an integrity violation or infrastructure interruption.

### `IntegrityFailure`

A hash, source, path, package, schema, stage, command, or trust-boundary identity differed from the reviewed contract. Do not repair the evidence or continue to model acquisition.

### `InfrastructureInterrupted`

The runner, network, controlled deadline, safety controller, or external process termination interrupted the attempt. This is not automatically a package incompatibility.

## Failure preservation

A failed attempt must retain:

- the exact current stage;
- all completed stages;
- the first causal message;
- the failure classification;
- command stdout, stderr, records, and resource evidence already produced;
- all model and scientific authorisation flags as `false`.

A failed attempt must not contain an acceptance manifest. Do not edit a failed workspace to make it pass. A retry receives a new GitHub run attempt and therefore a new immutable directory.

## Successful artifact review

After all three jobs pass:

1. Record the exact `main` commit, workflow run ID, and run attempt.
2. Download the artifact named:

   ```text
   workbook-05-phase3-dependency-preflight-<run-id>-<attempt>
   ```

3. Calculate the complete artifact ZIP SHA-256 independently.
4. Compare it with GitHub's recorded artifact digest.
5. Extract into a new temporary directory.
6. Verify `manifest.sha256` against every retained member.
7. Read `decision.json` and require `status: Passed`.
8. Recalculate the exact `decision.json` SHA-256 independently.
9. Confirm every model and scientific authorisation flag is `false`.
10. Confirm the retained Lenovo workspace still exists under the exact `C:\w5c` run identity.
11. Record explicit project-owner acceptance of:
    - exact repository commit;
    - run ID and attempt;
    - artifact name and SHA-256;
    - decision SHA-256;
    - retained workspace path.

Do not enable `live-asset-lock` from a green workflow screen alone.

## Recovery order

When a run fails:

```text
identify the first failed boundary
→ preserve the exact workspace and artifact
→ inspect failure.json
→ inspect the indexed command record
→ inspect stdout and stderr
→ inspect resource and termination evidence
→ distinguish integrity, dependency, and infrastructure causes
→ add a focused regression when code is defective
→ repair through a reviewed PR
→ rerun from a fresh main attempt
```

Do not change several dependency versions at once, delete failed evidence, bypass hash checking, enable remote model code, weaken Windows security policy, or move directly to Granite conversion.

## Engineering basis

This checkpoint follows the staged integration and Test and Evaluation approach in *Systems Engineering: Principles and Practice* (Chapters 16–17), trustworthy multi-level delivery testing in *The Art of Unit Testing* (Chapters 7 and 10), exact prerequisite and configuration control in *Code Complete* (Chapters 3, 22–23, and 28–29), systematic first-divergence diagnosis in *Why Programs Fail*, and secure defaults, allowlists, narrow trust boundaries, and untrusted-input validation in *Designing Secure Software*.
