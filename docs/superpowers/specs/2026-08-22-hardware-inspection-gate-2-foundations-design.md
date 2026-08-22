# Hardware Inspection Gate 2 Foundations Design

**Status:** Approved for implementation under the Hardware Inspection production design

**Date:** 2026-08-22

**Parent specification:** `docs/superpowers/specs/2026-08-15-hardware-inspection-production-design.md`

## Purpose

Gate 2 creates the reusable, candidate-neutral foundations required by later Hardware Inspection providers. It does not register a production `IHardwareInspectionService`, parse LLM Fit JSON, collect graphics or NPU evidence, create a `HardwareSnapshot`, or enable Compatibility Continue.

Gate 1 closed behaviorally as `FunctionalPassWithPackagingConcern`. The v1.1.9 candidate may define the tested command and output boundary for architectural work, but it must not be embedded or redistributed while the unsigned-binary discrepancy and transitive dependency-licence inventory remain unresolved.

## Project boundary

Create a non-WinUI class library at `infrastructure/GraniteEdgeAI.HardwareInspection.Foundation`. It owns only Windows system snapshots, trusted-package verification, and bounded external-process execution. The project references no application UI, Model Inspection type, model metadata, GGUF/OpenVINO type, or candidate-specific DTO.

Create a matching standalone MSTest project at `tests/UnitTests/GraniteEdgeAI.HardwareInspection.Foundation.Tests`. Tests run through Microsoft Testing Platform without an AppContainer and use the existing harmless LLM Fit fake-process fixture for real process-boundary tests.

The existing Hardware Inspection Domain and Application contracts remain in the app assembly during Gate 2. Gate 3 and Gate 4 will add thin adapters from foundation results into provider evidence. Moving the existing contracts to another assembly is deliberately excluded because it would create a high-collision migration without improving Gate 2 safety.

## Windows system snapshot

`WindowsSystemSnapshotProvider` returns one immutable `WindowsSystemSnapshot` containing:

- physically installed RAM bytes;
- OS-usable physical RAM bytes;
- currently available physical RAM bytes;
- a UTC capture timestamp;
- safe OS name, version, and architecture values.

The production implementation uses `GetPhysicallyInstalledSystemMemory` and `GlobalMemoryStatusEx`. It validates unit conversion and enforces `installed >= usable >= available`. A native failure, overflow, non-UTC timestamp, or impossible relationship produces a typed `WindowsSystemSnapshotException` with a stable diagnostic code and no path, host name, user name, stack trace, or native free-form message.

Native calls and time are injected behind internal seams so deterministic tests can exercise zero available memory, overflow, native failure, cancellation-before-call, and impossible relationships. Gate 2 does not query processor identity, graphics, storage, or NPU state.

## Trusted package verification

`TrustedToolPackageManifest` is constructed only from application-owned, already-parsed values. It contains a tool ID, version, package-relative executable, exact lowercase SHA-256, required flat member names, AMD64 requirement, and fixed command identities. It never accepts a package root, executable path, or arguments from user input.

`TrustedToolPackageVerifier` receives an approved package root and the manifest. It:

1. resolves canonical paths;
2. requires the package root and every ancestor beneath the approved root to be ordinary directories;
3. rejects traversal, rooted manifest members, duplicate/case-colliding members, reparse points, and nested or unexpected members;
4. opens stable read-only handles without write sharing;
5. verifies the executable SHA-256 and AMD64 PE headers;
6. verifies the exact required inventory immediately before returning success.

Verification returns only immutable approved identities and canonical paths needed by the process runner. Failures use a closed diagnostic enum. Raw paths appear only in the internal success object consumed inside Infrastructure; they never enter Domain, Presentation, logs, or user diagnostics.

Gate 2 does not decide Authenticode trust or redistribution approval. The manifest carries the Gate 1 packaging disposition so later composition can remain fail-closed while that disposition is unresolved.

## Bounded external-process execution

`ExternalProcessRunner` consumes only a verified executable and an `ExternalProcessRequest` whose argument vector must match one manifest-declared command identity. It never accepts a shell command string.

The runner always sets `UseShellExecute=false`, redirects standard output/error, disables window creation, supplies no arbitrary working directory, and starts one direct child. It enforces:

- UTF-8 output with independent stdout/stderr byte caps;
- a positive bounded timeout;
- caller cancellation;
- complete process-tree termination on timeout, cancellation, output overflow, or observation failure;
- a bounded cleanup wait;
- immutable exit code, duration, bounded output, and termination reason;
- no retry and no interpretation of provider JSON.

Output overflow is a failure, not silent truncation. Cancellation remains distinguishable from timeout. The runner does not inspect sockets in Gate 2; Gate 1 already established the candidate route, and later LLM Fit composition retains the no-listener acceptance guard.

## Integration boundary

The app project references the new foundation project, but no production service is switched during Gate 2. `UnavailableHardwareInspectionService` remains the sole app composition. This proves the new infrastructure cannot accidentally activate hardware collection.

The solution includes both new projects. The app may add a `WindowsAvailableMemoryProvider` adapter implementing the existing `IAvailableMemoryProvider`, backed by `WindowsSystemSnapshotProvider`, but that adapter is not registered until the production coordinator gate.

## Test strategy

All production behavior is test-first.

- Windows snapshot tests cover valid values, zero available bytes, UTC capture, cancellation, native failures, overflow, and impossible byte relationships.
- Manifest tests cover blank/duplicate/unsafe values and immutable copies.
- Package tests cover exact inventory, hash, PE architecture, traversal, nested members, case collisions, reparse members/ancestors, package mutation, and missing files.
- Process tests use the real fake executable to cover fixed arguments, stdout/stderr caps, non-zero exit, timeout, caller cancellation, process-tree cleanup, and no shell invocation.
- Composition tests prove the app still constructs `UnavailableHardwareInspectionService` and that no candidate path or Gate 1 artifact is packaged.
- Regression runs include the 174 deterministic Gate tests, the packaged Hardware Inspection suite, Stage 0/A Python contracts, and Release/x64 app build.

## Completion criteria

Gate 2 is complete when:

- the foundation and test projects build with zero errors;
- every new deterministic test passes with zero skipped;
- the existing Gate, packaged Hardware Inspection, and Stage 0/A regressions remain green;
- no LLM Fit candidate, raw capture, TRX, host identity, credential, or machine path is tracked;
- the app remains fail-closed;
- the preservation matrix records Gate 2 as complete and Gate 3 as the next provider-specific step.
