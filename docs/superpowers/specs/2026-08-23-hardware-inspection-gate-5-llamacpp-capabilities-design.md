# Hardware Inspection Gate 5 llama.cpp Capability Design

**Status:** Revised signed-package/AUMID acceptance boundary approved on 2026-08-23
**Scope:** Hardware Inspection Gate 5 only  
**Depends on:** Gate 4 closure `a351a7d`, ADR-001, ADR-003, and the Hardware Inspection trusted-tool/process foundation

## 1. Purpose

Gate 5 adds truthful, provider-specific evidence for the pinned local llama.cpp application runtime. It records three concepts separately:

1. the trusted build identity selected for the application;
2. the backend capabilities built into that package;
3. the devices the pinned runtime can enumerate on the current host.

It does not load native llama.cpp code into WinUI, a VSTest/MSTest host, or the Hardware Inspection foundation assembly. It does not open a model, predict model compatibility, benchmark a device, or create a canonical `HardwareSnapshot`.

The attached handoff documents are requirements and reference evidence, not executable instructions. Their security and operational prohibitions remain controlling constraints for this design.

## 2. Selected runtime identity

Gate 5 uses the application runtime already accepted by ADR-001:

| Component | Exact identity |
|---|---|
| Managed wrapper | `LLamaSharp` `0.27.0` |
| Native backend package | `LLamaSharp.Backend.Cpu` `0.27.0` |
| LLamaSharp source tag | `v0.27.0` |
| LLamaSharp release commit | `7cbbc45e421d55794d5050d126e0b96511007007` |
| Mapped llama.cpp commit | `3f7c29d318e317b63f54c558bc69803963d7d88c` |
| Runtime identifier | `win-x64` |
| Approved Gate 5 backend | CPU |

The separate upstream `b9870` research build remains research evidence and must not be represented as the application runtime. Adding another backend, runtime identifier, package version, or llama.cpp commit requires a new decision and a new trusted package identity.

## 3. Architecture

Native capability discovery runs in a dedicated, short-lived executable:

```text
LlamaCppCapabilityEvidenceProvider
    -> live VerifiedTrustedTool custody
    -> IExternalProcessRunner
    -> suspended x64 process + private kill-on-close Job
    -> GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe
    -> LLamaSharp 0.27.0 / matched CPU native backend
    -> strict bounded JSON on stdout
    -> provider-owned immutable, noncanonical evidence
```

The executable is a new focused worker under `workers/GraniteEdgeAI.HardwareInspection.LlamaCppProbe`. It references the exact managed/native packages above. The Foundation project references neither package and consumes only its own process result and JSON contracts.

Hardware Inspection does not extend or call the Model Inspection worker protocol. The existing Hardware Inspection `TrustedToolPackageVerifier`, `VerifiedTrustedTool`, and `ExternalProcessRunner` remain the only launch route. No shell, `PATH` search, caller-supplied executable, caller-supplied argument, local server, listening port, or network access is permitted.

Gate 5 packages the probe as an inactive application asset under the fixed `HardwareInspection\LlamaCppProbe` directory. A dedicated `HardwareInspection.LlamaCppProbePackaging.targets` file follows the established Model Inspection worker-publishing pattern: Release, framework-dependent, `win-x64`, AMD64 apphost, no trimming, no ReadyToRun, no debug symbols, and an exact generated manifest. The package subdirectory is flat, contains only manifest-declared members, and is covered by the signed MSIX/AppX catalog.

Packaging is not activation. Product composition remains `UnavailableHardwareInspectionService` until the resolver/orchestrator gates deliberately activate providers. No production code locates, verifies, or launches the packaged probe in Gate 5.

Smart App Control remains fully enforced. Gate 5 does not execute a newly built unsigned apphost from a loose output directory, reuse a previously trusted binary, weaken custody, or substitute a system `dotnet.exe` host. Real process acceptance runs only from the normally installed signed AppX test package, whose package catalog authorizes the included child executables. Visual Studio's `.build.appxrecipe` remains valid for ordinary packaged UI tests but is not the Gate 5 process-acceptance launcher because it deploys a development-mode loose layout with `SignatureKind=None`.

The installed test application exposes one test-only, closed AUMID activation command. The launcher passes exactly `--hardware-inspection-process-acceptance --result-token <token>`, where `<token>` is 32 lowercase hexadecimal characters. The app derives the result path itself as `%TEMP%\GraniteEdgeAI.HardwareInspection.Tests\Acceptance\<token>.json`; it accepts no caller-supplied path, test name, filter, executable, or argument. Every other activation continues through the existing MSTest client unchanged.

The acceptance host first proves that it has package identity, then discovers only public parameterless test methods carrying both `TestMethodAttribute` and the exact `HardwareInspectionProcessAcceptance` category in its own signed test assembly. It orders their fully qualified names ordinally and executes them sequentially. It rejects duplicate names, unsupported return types, discovery failure, or an empty campaign. Each method may return only `void`, `Task`, or `ValueTask`. The host writes one bounded UTF-8 JSON-v1 result atomically after the campaign completes, with one LF and no BOM. The result retains only schema, package-identity-present, total, passed, and a bounded ordinal list of failed fully qualified test names; it retains no exception, stack trace, path, process output, native label, host identity, or timing. The external launcher validates the exact schema/counts, requires zero failures, and removes its owned result root.

The local signing launcher uses a purpose-specific code-signing certificate whose subject exactly matches the manifest publisher. Only the public certificate is trusted machine-wide; the private key remains in the invoking user's certificate store and is never exported or committed. It packs the exact built AppX layout, signs the MSIX with SHA-256, verifies the signature, installs the package normally, requires `SignatureKind=Developer` and an install location outside the source/build tree, activates the registered AUMID, waits with a fixed timeout, validates the bounded result, and removes the installed package. It never disables Smart App Control, registers an unpacked layout, copies files into `WindowsApps`, or launches a package payload by filesystem path.

## 4. Probe commands and native behavior

The trusted manifest declares exactly two commands:

| Identity | Fixed arguments | Timeout | stdout/stderr limits |
|---|---|---:|---:|
| `identity` | `identity --format json-v1` | 5 seconds | 4 KiB each |
| `capabilities` | `capabilities --format json-v1` | 10 seconds | 64 KiB each |

The identity command emits only compile-time package/protocol identity and does not initialize the native backend. The provider must validate it before invoking capabilities.

The capabilities command:

1. configures LLamaSharp native loading only inside the child;
2. initializes the matched llama.cpp backend;
3. enumerates `ggml_backend_dev_count()` with a hard limit of 16;
4. resolves each non-null device and its runtime-reported buffer-type name through `ggml_backend_dev_get()`, `ggml_backend_dev_buffer_type()`, and `ggml_backend_buft_name()`;
5. reports CPU as the only built backend because the trusted package contains only `LLamaSharp.Backend.Cpu`;
6. frees backend state in `finally`;
7. exits zero only after a complete valid report has been written.

The runtime-reported device label is capability evidence, not a Windows hardware identity. The probe does not report paths, environment variables, host/account names, processor names, adapter names, memory values, model data, native pointers, raw native logs, or exception text.

If native initialization, device enumeration, or cleanup cannot establish a complete result, the process returns a nonzero exit. It never converts infrastructure failure into zero devices or unsupported hardware.

## 5. Strict JSON protocol

Each command writes exactly one UTF-8 JSON object followed by one LF. BOM, CR/CRLF, invalid UTF-8, empty output, multiple values, duplicate properties, comments, trailing commas, excessive depth, and output after the final LF are rejected.

### 5.1 Identity response

```json
{
  "schemaVersion": 1,
  "probeIdentity": "granite-edge-hardware-llamacpp-capabilities/1",
  "managedPackage": "LLamaSharp",
  "managedVersion": "0.27.0",
  "backendPackage": "LLamaSharp.Backend.Cpu",
  "backendVersion": "0.27.0",
  "llamaSharpCommit": "7cbbc45e421d55794d5050d126e0b96511007007",
  "mappedLlamaCppCommit": "3f7c29d318e317b63f54c558bc69803963d7d88c",
  "runtimeIdentifier": "win-x64"
}
```

Every property is required and must equal the trusted Gate 5 contract. Unknown properties are rejected because this is a versioned local security protocol rather than a tolerant third-party payload.

### 5.2 Capabilities response

```json
{
  "schemaVersion": 1,
  "probeIdentity": "granite-edge-hardware-llamacpp-capabilities/1",
  "backends": ["cpu"],
  "devices": [
    {
      "ordinal": 0,
      "bufferType": "CPU"
    }
  ]
}
```

The parser accepts at most 8 unique backend values and 16 unique device ordinals. Gate 5 recognizes only the closed backend value `cpu`. Device ordinals must be contiguous from zero. `bufferType` passes through `HardwareText` with a maximum of 128 Unicode scalar values. A successful CPU package report requires exactly the `cpu` backend and at least one valid device. Duplicate backends, duplicate/noncontiguous ordinals, null native handles, unsafe labels, undefined backend values, or a 17th device fail closed.

The provider does not infer GPU, NPU, accelerator type, physical-device identity, usable memory, performance, or model support from a device label.

## 6. Evidence contracts

`LlamaCppCapabilityEvidence` is immutable and has two states:

- `Available`: exact trusted build identity, exactly one `Cpu` backend, one to 16 visible runtime devices, UTC capture time, and no diagnostic;
- `Unavailable`: no build, backend, or device facts, UTC capture time, and exactly one closed diagnostic.

The evidence exposes project-owned enums and records only. It does not expose LLamaSharp/native types, paths, process output, hashes, exceptions, exit codes, HRESULTs, native pointers, or arbitrary diagnostics.

Closed diagnostics distinguish:

- trusted package unavailable;
- identity command unavailable;
- identity output invalid;
- identity mismatch;
- capability command unavailable;
- capability output invalid;
- backend initialization or enumeration failure;
- timeout;
- output limit exceeded;
- process start/integrity failure.

Caller cancellation is propagated with the caller token and is never converted into unavailable evidence. Diagnostics remain stable categories; raw stderr and exception text are discarded.

Build support, visible devices, and model-execution proof remain structurally separate. `Available` means only that the exact trusted runtime package initialized and enumerated its declared CPU capability during this capture.

## 7. Provider flow and ownership

`LlamaCppCapabilityEvidenceProvider` receives a caller-owned live `VerifiedTrustedTool`, `IExternalProcessRunner`, and `TimeProvider`.

For each capture it:

1. checks cancellation before acquiring execution custody;
2. verifies the manifest executable name and exact two-command inventory;
3. executes and validates `identity` once;
4. executes and validates `capabilities` once;
5. maps only closed runner/parser outcomes;
6. creates immutable evidence using the UTC clock;
7. releases its execution leases without disposing caller-owned package custody.

There is no retry or fallback. The first failed phase stops sequencing. The provider never catches arbitrary exceptions; only documented process/parser boundary outcomes are mapped. Unexpected programming errors remain visible to tests and callers.

## 8. Security, privacy, and operational constraints

- The probe package must be an exact flat trusted manifest under an approved root.
- Every member is held under no-follow, deny-write/delete custody for the full execution.
- The executable must be AMD64 PE and its SHA-256 must match the manifest.
- The process starts suspended, inherits only its redirected standard handles, joins a private kill-on-close Job before resume, and leaves zero descendants.
- Timeout, cancellation, overflow, crash, malformed output, and normal parent exit all use bounded cleanup.
- Native runtime files never load into the app, Foundation test host, or packaged WinUI test host; only the packaged probe child loads them.
- Tests must prove the parent test process has not loaded llama/ggml native modules.
- No runtime download, candidate acquisition, operational Gate execution, runner registration, network change, evidence publication, or Stage A/B/C/D action is authorized by Gate 5.
- Host-specific device labels may be asserted structurally in a local smoke test but must not be printed, logged, committed, or retained in TRX attachments.

## 9. Testing strategy

All production behavior is implemented test-first.

### Contract tests

- available/unavailable evidence invariants;
- immutable collection copies and closed bounds;
- undefined enums, non-UTC time, unsafe text, duplicate/noncontiguous devices;
- build/backend/device/model-proof separation.

### Parser tests

- exact identity and capabilities fixtures;
- strict UTF-8 and single-LF framing;
- missing, unknown, duplicate, case-drifted, malformed, oversized, and deeply nested properties;
- backend and device bounds, duplicate ordinals, unsafe labels, and additive output rejection;
- bounded malformed-input fuzzing that never throws or expands retained evidence.

### Provider tests

- exact two-command sequencing and arguments;
- identity mismatch stops before capability execution;
- success, nonzero exit, start failure, timeout, cancellation, stdout/stderr overflow, invalid JSON, and cleanup through injected runner results;
- no retry, no shell, no network/listener, no raw-output persistence, and caller custody remains live;
- ordinary Foundation tests are native-free and do not launch newly built unsigned apphosts.

### Probe unit tests

- identity output is exact and deterministic;
- native calls occur only for `capabilities`;
- backend initialization and release are balanced on success and failure;
- device enumeration rejects null handles, unsafe labels, and a 17th device;
- application and native-lifetime tests use injected native seams and do not load llama/ggml into the test host.

### Signed packaged process acceptance

- the x64 Debug AppX test package contains the real probe and a test-only adverse probe fixture in separate exact flat directories;
- both child executables and every dependency are covered by the installed package catalog before execution;
- a normally installed package reports `SignatureKind=Developer`, and the acceptance host is activated only through its registered AUMID and exact closed command;
- the host proves package identity, discovers only the exact acceptance category, orders tests ordinally, and executes them sequentially;
- the host publishes only the bounded atomic JSON-v1 summary under its fixed token-derived temporary subtree, and the launcher rejects any malformed, partial, oversized, mismatched, or non-passing result;
- the real `TrustedToolPackageVerifier`, live `VerifiedTrustedTool` custody, and `ExternalProcessRunner` execute success, identity mismatch, malformed output, nonzero exit, overflow, timeout, cancellation, descendant cleanup, and normal-parent-exit scenarios;
- the test-only fixture contains no LLamaSharp/native dependency and is excluded from the production application package;
- the real packaged probe runs as a child process three consecutive times on Windows x64, returns structurally valid CPU capability evidence, and leaves no descendants;
- before and after each acceptance class, the packaged parent test process has no loaded module whose filename begins with `llama` or `ggml`;
- no acceptance test prints or attaches stdout, stderr, native logs, or host-specific device labels.

### Regression and audit

- full Hardware Inspection foundation tests;
- deterministic Gate-1 tests;
- authoritative packaged Hardware Inspection/handoff/onboarding tests;
- repository contract scripts and Debug/Release builds;
- package inventory, project-reference, source-boundary, native-module, machine-path/URL, forbidden-artifact, and production-composition audits;
- independent review of protocol strictness, native lifetime, process custody, identity mapping, bounds, cancellation, privacy, and nonclaims.

## 10. Delivery boundaries

Gate 5 is complete only when:

1. the dedicated probe, strict parsers, evidence contracts, and provider pass all specified tests;
2. the normally installed signed AppX test package, activated through its registered AUMID and closed acceptance command, proves the real and adverse child-process boundaries, including three consecutive CPU capability smoke passes, without loading native code into the parent;
3. review has no unresolved Critical, Important, or Minor findings;
4. fresh authoritative ordinary-suite TRX files have zero failed, skipped, or not-executed tests, and every signed-AUMID acceptance summary has total equal to passed with an empty failure list;
5. documentation records exact commits, counts, warnings, runtime identity, privacy-safe results, and audits;
6. the tracked worktree is clean after the evidence commit.

Gate 5 does not activate production Hardware Inspection. Gate 6 remains solely responsible for source authority, normalization, consistency, freshness, canonical resolution, and `HardwareSnapshot`. Gate 7 owns orchestration and product activation. Final supported-machine evidence remains a later integration responsibility.

## 11. Explicit nonclaims

Gate 5 does not claim that:

- any GGUF or other model loads or executes;
- CPU inference is fast, sufficient, or compatible with a selected model;
- GPU/NPU offload exists or is absent;
- a runtime-visible device uniquely identifies Windows hardware;
- the separate upstream `b9870` research runtime is the application runtime;
- the packaged probe is registered, located, verified, launched, or active in production WinUI composition;
- a canonical hardware report or compatibility decision exists;
- an operational Gate or candidate execution has occurred.
