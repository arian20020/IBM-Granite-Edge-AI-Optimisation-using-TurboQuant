# Model Inspection Branch Review

**Date:** 2026-08-05  
**Branch:** `feature/model-inspection`  
**Pull request:** `#44`  
**Reviewed head before final CI:** `47d1b0cbcddb064c093940525062b3347544e29f`  
**Review type:** Whole-branch evidence and high-risk static review

## Scope

The pull request contains application presentation, navigation, runtime
feasibility, deterministic tests, contained native tests, trusted real-model
tests, workflows and extensive architecture/evidence documentation.

The review prioritised components capable of:

- terminating a process through native code;
- modifying or leaking the controlled model;
- uploading unsafe evidence;
- hiding the original failure cause;
- crossing the WinUI/runtime dependency boundary;
- producing stale or misleading Model Inspection UI state.

Reviewed high-risk files and areas included:

```text
.github/workflows/llamasharp-feasibility-smoke.yml
.github/workflows/llamasharp-real-model-integration.yml

tools/ModelInspection.LlamaSharpSpike/Program.cs
tools/ModelInspection.LlamaSharpSpike/JsonEvidenceWriter.cs
tools/ModelInspection.LlamaSharpSpike/ModelProbe/VocabOnlyModelProbe.cs
tools/ModelInspection.LlamaSharpSpike/ModelProbe/ModelFileSnapshotService.cs
tools/ModelInspection.LlamaSharpSpike/ModelProbe/ProbeResultFinalizer.cs
tools/ModelInspection.LlamaSharpSpike/ModelProbe/SensitiveTextRedactor.cs

tools/ModelInspection.LlamaSharpSpike.TestSupport/ProbeProcessRunner.cs
tools/ModelInspection.LlamaSharpSpike.TestSupport/ArtifactPrivacyScanner.cs
tools/ModelInspection.LlamaSharpSpike.TestSupport/SocketObservation.cs

tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
    Cancellation/
    Security/
    FileAccess/
    MalformedModels/
    RealModel/

IBM Granite with TurboQuant (Intel)/Features/ModelInspection/
IBM Granite with TurboQuant (Intel)/Features/Onboarding/
IBM Granite with TurboQuant (Intel).csproj
```

The review also reconciled the coverage matrix, runbook, feature hierarchy,
source-adjacent READMEs and pull-request description with the fresh Tier 2
evidence.

## Executable evidence considered

### Tier 1

```text
Deterministic tests:          170 / 170 passed
Contained native tests:       4 / 4 passed
Release win-x64 build:        passed
Framework-dependent publish:  passed
CPU runtime smoke:            passed
Artifact/privacy gate:        passed
```

### Tier 2 local trusted gate

```text
Trusted real-model tests:     20 / 20 passed
Failed / skipped:             0 / 0
Model SHA-256 unchanged:      yes
Evidence files scanned:       56
Privacy findings:             0
```

The executable evidence is recorded separately in the Tier 1 and Tier 2
verification documents.

## Blocking findings discovered and corrected

### BR-001 — trusted workflow used an unavailable PowerShell API

**Finding**

The trusted workflow privacy scan used:

```powershell
[IO.Path]::GetRelativePath(...)
```

The same call failed in the actual Visual Studio Developer PowerShell host after
the otherwise successful 20-test campaign. Leaving it in the workflow would
allow a future service-account run to fail after tests and integrity checks had
passed.

**Correction**

The workflow now uses:

- normalised full evidence-root paths;
- a directory-separator-qualified root prefix;
- a case-insensitive containment check;
- substring-derived display paths.

The workflow also requires the staged model to be read-only and uses a
separator-qualified workspace prefix when proving the model is outside the
checkout.

**Status:** Corrected.

### BR-002 — trusted-workflow edits did not trigger Tier 1 validation

**Finding**

The Tier 1 workflow path filters did not include:

```text
.github/workflows/llamasharp-real-model-integration.yml
```

A future edit to the security-sensitive trusted workflow could therefore avoid
the normal hosted validation workflow.

**Correction**

The trusted workflow path was added to both Tier 1 `push` and `pull_request`
path filters.

**Status:** Corrected.

## Reviewed boundaries with no blocking finding

### Native/model process containment

- `ProbeProcessRunner` starts native/model work outside the MSTest host.
- arguments use `ProcessStartInfo.ArgumentList` rather than shell command
  construction;
- stdout, stderr, exit code, PID and duration are retained;
- timeouts and caller cancellation kill the complete child process tree;
- a native abort cannot terminate the complete test campaign.

### Model integrity

- snapshots open the selected file with read-only access;
- SHA-256, length and last-write time are captured;
- post-probe integrity applies to successful and cancelled operations;
- a changed or unverifiable model outranks success/cancellation;
- the trusted campaign independently verified an unchanged 2 GB Granite model.

### Evidence privacy

- canonical model paths are redacted from failures and native logs;
- full chat-template text is not serialised;
- evidence contracts exclude native handles, pointers and XAML objects;
- JSON is serialised before temporary-file creation;
- failed writes preserve earlier valid evidence;
- retained evidence is scanned for GGUF files, model-sized files, oversized
  files and exact model-copy hashes;
- artifact upload remains fail-closed behind integrity and privacy outcomes.

### VocabOnly evidence depth

- the corrected collector uses GGUF metadata and vocabulary operations safe at
  `VocabOnly` depth;
- unsafe native hyperparameter getters that caused the earlier abort remain
  excluded by source-contract tests;
- unavailable parameter count remains nullable instead of being guessed.

### Runtime and application dependency boundary

- LLamaSharp remains isolated under `tools/`;
- the WinUI application project has no LLamaSharp, Vulkan or TurboQuant package
  reference;
- the page does not hold native handles or classify runtime failures;
- project-owned interfaces remain the planned production boundary.

### WinUI presentation

- the current action-card XAML contains one visual-state hierarchy rather than
  the earlier duplicated state groups;
- the content template selector safely handles WinUI bootstrap calls;
- onboarding owns cross-feature navigation;
- the page currently applies presentation state only and does not make false
  runtime calls.

## Non-blocking findings and deferred work

### DR-001 — current Cancel UI is intentionally non-functional

The initial page displays a visible disabled Cancel action and supporting text.
The text should not promise a usable return/cancel action once production work
begins unless the command is enabled and wired through the service. This belongs
to the next ViewModel/application slice.

### DR-002 — navigation currently passes a path only

The production handoff should use an immutable `ModelInspectionRequest`
containing validated quick-scan and file-identity data rather than passing only
a string path.

### DR-003 — worker-process architecture needs a formal production ADR

A real native abort bypassed managed exception handling. The reviewed evidence
supports a protected worker process behind `ILlamaModelProbe`, but the worker
protocol, packaging, lifecycle and version contract require a focused design
and ADR before implementation.

### DR-004 — observer shutdown can receive later hardening

The current process observer is cooperatively cancelled and all verified
observers terminate correctly. A separately bounded observer-shutdown timeout
may be added as a later hardening measure if production observers become more
complex.

### DR-005 — filesystem-link aliases remain deferred

Symlink, junction and hard-link alias protection is not represented by the
verified 20-test campaign and remains explicitly deferred in the coverage
matrix.

### DR-006 — self-hosted service-account execution remains later

The local target-machine runtime gate is closed. The restricted self-hosted
workflow remains a deployment/workflow-registration and service-account access
check after the workflow exists on the default branch.

### DR-007 — later runtime gates remain separate

This branch does not cover full CPU allocation/generation, Vulkan, TurboQuant,
PolarQuant, QJL, OpenVINO or Hardware Fit.

## Review conclusion

No unresolved blocking finding remains in the reviewed native, model-integrity,
privacy, workflow, evidence or current WinUI presentation boundaries after
BR-001 and BR-002 were corrected.

The branch should remain draft until the fresh hosted workflows for the final
head complete successfully. After those checks are reviewed, PR #44 can be
moved to ready-for-review without claiming that the production Model Inspection
service or later backend gates are complete.
