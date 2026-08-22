# Hardware Inspection Gate 3 LLM Fit Provider Design

**Status:** Approved
**Date:** 2026-08-22
**Parent design:** `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`
**Foundation:** Gate 2 through `05ad17a`

## 1. Goal

Implement the production-shaped, infrastructure-only LLM Fit command, parsing, validation, and evidence boundary on top of the verified Gate 2 tool/process foundation. Gate 3 must map the accepted Gate 1 Windows Intel JSON shape and deterministic adverse fixtures without bundling the unsigned candidate or activating product collection.

## 2. Chosen approach

Three approaches were considered:

1. **Clean-room Gate 3 provider on the Gate 2 foundation — selected.** Define small immutable Gate 3 contracts, parse with bounded `System.Text.Json`, and execute only verified manifest commands through `IExternalProcessRunner`. This keeps spike code and production code separate while reusing the spike only as behavioral evidence.
2. **Move the Gate 1 assessor into production.** This is faster, but it carries Gate-specific verdicts, schema-documentation diagnostics, and candidate concerns into a long-lived provider boundary.
3. **Reference the spike project from production.** This minimizes copying but creates the wrong dependency direction and risks packaging Gate tooling/evidence with the app.

The selected approach follows the repository's existing isolation pattern: experiments prove behavior; production code is independently specified, smaller, and tested against the accepted fixtures.

## 3. Scope and non-scope

Gate 3 includes:

- exact LLM Fit command identities and arguments;
- an internal JSON DTO/parser boundary;
- immutable provider evidence with safe diagnostics and a raw-output SHA-256;
- exact version-output validation for `llmfit 1.1.9`;
- mapping of process termination, exit, output-limit, identity, JSON, CPU/RAM, and GPU-consistency failures;
- deterministic fixtures for successful Windows Intel, CPU-only, additive schema, missing fields, duplicate properties, malformed JSON, numeric bounds, contradictory GPU fields, non-zero exit, timeout, cancellation, and output overflow;
- mapping of the accepted Gate 1 `valid-windows-intel.json` capture.

Gate 3 does not include:

- an LLM Fit executable, archive, download URL, release acquisition, signature exception, installer, or redistribution approval;
- Windows/DXGI/storage/NPU enrichment;
- canonical `HardwareSnapshot` resolution, tolerance/freshness policy, compatibility, model data, GGUF, OpenVINO, or fit recommendations;
- app/onboarding composition, `IHardwareInspectionService` activation, orchestration, progress, UI, or handoff changes;
- raw stdout/stderr persistence, logging, telemetry, network access, dashboard/server operation, or port observation.

`UnavailableHardwareInspectionService` remains the sole production composition.

## 4. Project placement and dependencies

Gate 3 remains in `GraniteEdgeAI.HardwareInspection.Foundation` because it consumes `VerifiedTrustedTool` and `IExternalProcessRunner` directly and must not depend on WinUI or app-domain types.

New source groups:

- `LlmFit/LlmFitCommandContract.cs` — exact tool/version/command constants and manifest command factories;
- `LlmFit/LlmFitSystemJsonParser.cs` — bounded tolerant parsing into an internal DTO and closed validation result;
- `LlmFit/LlmFitHardwareEvidence.cs` — immutable provider-specific evidence and diagnostic contracts;
- `LlmFit/LlmFitHardwareEvidenceProvider.cs` — version check, bounded system invocation, cancellation handling, and result mapping.

Tests live under the standalone foundation test project in `LlmFit/`. JSON fixtures live only in that test project and are copied to its test output, never the app package.

## 5. Command and identity contract

The provider accepts only a live `VerifiedTrustedTool`. Before execution it requires:

- `ToolId` equals `llmfit` using ordinal comparison;
- `Version` equals `1.1.9` using ordinal comparison;
- a manifest command named `version` with exactly `--version`;
- a manifest command named `system` with exactly `--no-dashboard`, `--json`, `system`.

`LlmFitCommandContract` creates the two `TrustedToolCommand` values so deployment/packaging work cannot silently drift from the provider. The provider still rechecks the verified command set fail-closed; it never accepts caller-provided arguments.

Each capture runs the version command first with a 5-second timeout and independent 4 KiB stdout/stderr limits. Successful output must contain exactly one nonblank line equal to `llmfit 1.1.9`, allowing only the final platform line terminator. The system command then runs with a 15-second timeout and independent 256 KiB stdout/stderr limits.

The Gate 2 runner remains the only process-launch boundary. Gate 3 introduces no `Process`, shell, command-line string, environment, working-directory, or retry API.

## 6. Parsing contract

The parser accepts a bounded UTF-8-decoded string supplied by the runner. It uses `JsonDocument` with comments and trailing commas disabled and a maximum nesting depth of 16.

Required structure:

- root is an object with one unique `system` object;
- required CPU/RAM fields are `total_ram_gb`, `available_ram_gb`, `cpu_cores`, and `cpu_name`;
- required GPU-shape fields are `has_gpu`, `gpu_count`, and `gpus`;
- the root, `system`, and every parsed `gpus` entry reject duplicate property names using ordinal comparison;
- unknown properties are accepted and ignored, preserving additive-schema tolerance;
- no property name is matched case-insensitively.

CPU/RAM validation:

- RAM values must be finite JSON numbers representable as `double`;
- `total_ram_gb` must be greater than zero and no greater than 16,384 GiB;
- `available_ram_gb` must be between zero and total RAM inclusive;
- `cpu_cores` must be an integer from 1 through 4,096;
- `cpu_name` must contain 1 through 256 Unicode scalar values after rejecting leading/trailing whitespace, control characters, unpaired surrogates, and NUL.

GPU validation:

- `gpu_count` must be an integer from 0 through 64;
- `has_gpu` must equal `gpu_count > 0`;
- when false, `gpus` must be empty and `gpu_name` may be absent or null;
- when true, `gpus` must be nonempty; each entry is an object with a safe `name` and integer `count` from 1 through 64; the sum of entry counts must equal `gpu_count`;
- GPU entry names must be unique using ordinal-ignore-case comparison;
- a present top-level `gpu_name` must be null or a safe name; it is corroborative text, not a memory or backend authority;
- LLM Fit GPU VRAM, available GPU memory, unified-memory, backend, and bandwidth fields are ignored by Gate 3 because Gate 4 owns truthful DXGI semantics.

The parser never returns or stores raw JSON. It computes lowercase SHA-256 over the UTF-8 re-encoding of the bounded string returned by the Gate 2 runner and returns the hash with validation metadata.

## 7. Evidence contract

`LlmFitHardwareEvidence` is immutable and provider-specific. It contains:

- exact verified tool ID and version;
- UTC capture time;
- `LlmFitEvidenceState` (`Available`, `Invalid`, or `Unavailable`);
- nullable CPU name, logical processor count, total RAM GiB, and available RAM GiB;
- `LlmFitGpuDetectionState` (`Reported`, `NotReported`, or `Invalid`);
- reported GPU count and an immutable list of provider-reported GPU names only when consistent;
- lowercase 64-character raw-output SHA-256 when system stdout was available, otherwise null;
- an immutable, unique, ordinally ordered list of `LlmFitDiagnosticCode` values.

Available evidence requires all CPU/RAM fields and a consistent GPU shape. Invalid output may retain only individually validated fields; contradictory or malformed fields are absent and cannot masquerade as facts. Unavailable evidence contains no hardware facts.

The provider does not convert GiB to canonical bytes. Gate 6 owns unit normalization and tolerance against Windows facts; preserving LLM Fit's source units avoids hidden rounding and false precision.

## 8. Closed diagnostics and failure mapping

Diagnostics are an enum, not arbitrary strings. The approved values are:

- `ToolIdentityMismatch`
- `CommandContractMismatch`
- `VersionStartFailed`
- `VersionTimedOut`
- `VersionOutputLimitExceeded`
- `VersionCleanupFailed`
- `VersionNonZeroExit`
- `VersionOutputMismatch`
- `VersionCancelledUnexpectedly`
- `SystemStartFailed`
- `SystemTimedOut`
- `SystemOutputLimitExceeded`
- `SystemCleanupFailed`
- `SystemNonZeroExit`
- `SystemCancelledUnexpectedly`
- `JsonInvalid`
- `RequiredCpuRamMissing`
- `RequiredCpuRamInvalid`
- `GpuShapeMissing`
- `GpuInconsistent`

Process stderr and exception messages never become diagnostics. Start/cleanup/timeout/output errors map to unavailable evidence. JSON and field failures map to invalid evidence. Non-zero exit codes are not retained as user-facing text; tests may assert the branch only.

If the caller token is cancelled before or during either command, `CaptureAsync` throws `OperationCanceledException` with that token. A runner `Cancelled` result while the caller token is not cancelled maps to `VersionCancelledUnexpectedly` or `SystemCancelledUnexpectedly`; it does not fabricate cooperative cancellation.

## 9. Security and privacy properties

- Only verified manifest commands can execute.
- Verified-tool custody remains held for both commands and is never disposed by the provider.
- The provider has one attempt per command; there is no retry, fallback executable, PATH search, shell, network, dashboard, or listener.
- Output is bounded before parsing. Parsing allocates no collection based on an unbounded declared count.
- Raw JSON, stderr, paths, usernames, environment values, and machine identifiers are not retained in evidence or diagnostics.
- CPU/GPU names are bounded hardware facts. Tests use synthetic names; documentation records no real host identity.
- The unsigned Gate 1 candidate remains evaluation-only and absent from tracked files and packages.

## 10. Testing strategy

Test-first implementation is split into independently reviewable units:

1. command/evidence contracts and immutability;
2. tolerant parser success and additive-schema behavior;
3. parser adverse fixtures and numeric/string bounds;
4. provider identity/version/process mapping with a deterministic runner seam;
5. real-process provider tests through the verified harmless fake tool;
6. accepted Gate 1 fixture mapping and Gate 2/full regression verification.

Real-process tests verify exact arguments, no dashboard flag drift, successful version/system sequencing, timeout, cancellation, output overflow, and process cleanup. Unit tests never execute the real LLM Fit candidate.

Gate 3 closure requires:

- all new foundation tests pass with zero skipped;
- Gate 2 foundation, 174 deterministic Gate tests, packaged Hardware/model-handoff/onboarding tests, and Stage 0/A contracts remain green;
- Debug/Release x64 app builds have zero errors;
- no candidate, raw capture, TRX, or Gate evidence is newly tracked or packaged;
- production composition remains unavailable;
- independent code review has no unresolved Critical or Important finding.

## 11. Exit and next gate

Gate 3 exits with provider-specific LLM Fit evidence only. It does not produce `HardwareSnapshot`, a product run result, a handoff, or a compatibility decision.

Gate 4 is next and will add Windows processor/memory/OS, DXGI graphics, storage, and the provisional NPU probe. Gate 6 later owns source authority, unit normalization, tolerance, freshness, consistency, and canonical resolution.
