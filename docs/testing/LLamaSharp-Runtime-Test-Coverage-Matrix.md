# LLamaSharp Runtime Test Coverage Matrix

**Document ID:** TEST-COV-LLAMASHARP-001  
**Status:** Tier 1 verified; Tier 2 source compiles; trusted execution pending  
**Last reviewed:** 2026-08-04  
**Runtime under test:** `LLamaSharp 0.27.0` + `LLamaSharp.Backend.Cpu 0.27.0`  
**Mapped llama.cpp commit:** `3f7c29d318e317b63f54c558bc69803963d7d88c`  
**Related design:** [LLamaSharp runtime test architecture](../superpowers/specs/2026-08-04-llamasharp-runtime-test-architecture-design.md)  
**Fresh Tier 1 evidence:** [2026-08-04 verification record](./evidence/2026-08-04-llamasharp-tier1-verification.md)

## Purpose

This register prevents a risk from being treated as covered merely because a
test suite exists. Every known failure scenario at the current CPU-native and
`VocabOnly` boundary is linked to:

- an executable deterministic test;
- a contained child-process native test;
- a trusted real-model test; or
- an explicit deferral and future evidence route.

A source file or clean compile proves implementation readiness only. A row is
marked `Verified` only when fresh execution evidence covering that row has been
reviewed.

## Verified evidence

### Historical baseline before the expansion

```text
Previous deterministic suite:       28 / 28 passed
Release win-x64 build:               passed
Published CPU native smoke:          passed
Controlled Granite VocabOnly probe:  passed
Original Granite SHA-256 preserved:  yes
Native handle closed:                yes
```

### Expanded Tier 1 verification

```text
Workflow run:                        30939159409
Deterministic suite:                 170 / 170 passed
Trusted test assembly compile:       passed without model execution
Release win-x64 build:               passed, 0 warnings, 0 errors
Framework-dependent publish:         passed
Direct CPU native smoke:             passed
Contained native integration:        4 / 4 passed
No-GGUF artifact scan:               passed
Privacy-gated evidence upload:       passed
```

The trusted real-model suite is therefore compile-ready, but its model,
cancellation, malformed-input, file-access, privacy and network scenarios are
not verified until the manual self-hosted workflow completes.

## Status vocabulary

| Status | Meaning |
|---|---|
| Verified | Fresh execution evidence has been reviewed |
| Compile verified | Source and analyzers build, but the scenario has not executed |
| Source implemented | Automated check exists; fresh execution pending |
| Planned — Tier 2 | Defined by the trusted-runner implementation plan but not yet implemented or confirmed |
| Deferred | Not safely or proportionately automatable at this stage; future route recorded |
| Outside scope | Belongs to a later CPU, Vulkan, TurboQuant, UI, or Hardware Fit gate |

## Tier 1 — dependency and architecture policy

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-DP-001 | LLamaSharp managed version drifts | Project policy | `SpikeProject_PinsApprovedManagedAndCpuBackendPackages` | Exact `0.27.0` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-002 | CPU backend version drifts | Project policy | `SpikeProject_PinsApprovedManagedAndCpuBackendPackages` | Exact `0.27.0` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-003 | Floating or ranged NuGet version introduced | Project policy | `SpikeProject_UsesOnlyExactNonFloatingPackageVersions` | Test rejects wildcard/range | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-004 | CUDA dependency enters CPU feasibility project | Project policy | `SpikeProject_DoesNotReferenceGpuOrTurboQuantDependencies` | No CUDA include | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-005 | Vulkan dependency enters CPU feasibility project | Project policy | `SpikeProject_DoesNotReferenceGpuOrTurboQuantDependencies` | No Vulkan include | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-006 | TurboQuant dependency enters lightweight CPU probe | Project policy | `SpikeProject_DoesNotReferenceGpuOrTurboQuantDependencies` | No TurboQuant include | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-007 | Experimental runtime leaks into WinUI project | Architecture | `WinUiApplicationProject_DoesNotReferenceExperimentalRuntimePackages` | No LLamaSharp/TurboQuant include | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-008 | MTP runner configuration removed | Test infrastructure | `DeterministicTestProject_RemainsConfiguredForMicrosoftTestingPlatform` | Required project properties remain true | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-009 | Mapped llama.cpp identity changes silently | Runtime identity | `RuntimeIdentitySource_RecordsMappedAndResearchCommitsSeparately` | Exact mapped commit | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-DP-010 | `b9870` research runtime is confused with application runtime | Runtime identity | `RuntimeIdentitySource_RecordsMappedAndResearchCommitsSeparately` | Distinct commits and research tag | Tier 1 | MTP log | Verified — run `30939159409` |

## Tier 1 — command-line contracts

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-CLI-001 | No arguments select wrong mode | CLI | `Parse_WithoutArguments_UsesNativeSmokeDefaults` | Native-smoke defaults | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-002 | `--help` or `-h` not recognised | CLI | `Parse_WithHelpOption_ReturnsHelpRequest` | Help request | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-003 | Help mixed with execution options | CLI | `Parse_WithHelpAndAnotherOption_ReturnsControlledError` | Controlled argument error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-004 | Model path with spaces/Unicode is altered | CLI | `Parse_WithSpaceOrUnicodeModelPath_PreservesValue` | Exact value preserved | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-005 | Option order changes parsing | CLI | operation/native cancellation order tests | Same parsed options | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-006 | Required option value is missing | CLI | `Parse_WithMissingOptionValue_ReturnsControlledError` | Controlled argument error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-007 | Blank model/output path accepted | CLI | `Parse_WithWhitespacePath_ReturnsControlledError` | Controlled argument error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-008 | Cancellation used without model | CLI | operation/native no-model tests | Error identifies cancellation option and `--model` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-009 | Both cancellation modes accepted together | CLI | `Parse_WithBothCancellationModes_ReturnsControlledError` | Mutually exclusive error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-010 | Zero/negative/decimal/non-numeric/overflow delay accepted | CLI | invalid delay matrix | Controlled positive-integer error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-011 | Duplicate option silently overwrites earlier value | CLI | duplicate option matrix | Controlled duplicate error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-012 | Unknown argument ignored | CLI | `Parse_WithUnknownArgument_ReturnsControlledError` | Controlled unknown-argument error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CLI-013 | Null argument collection accepted | CLI | `Parse_WithNullArguments_ThrowsArgumentNullException` | `ArgumentNullException` | Tier 1 | MTP log | Verified — run `30939159409` |

## Tier 1 — path, file, and integrity safety

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-FS-001 | Evidence path equals model path | Path safety | exact collision test | Rejected | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-002 | `.` / `..` alias bypasses collision check | Path safety | dot and parent-segment tests | Rejected after canonicalisation | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-003 | Relative/absolute alias bypasses collision check | Path safety | relative/absolute test | Rejected | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-004 | Windows case-only alias bypasses collision check | Path safety | Windows case variant test | Rejected | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-005 | Blank or invalid path reaches file/native code | Path safety | blank and null-character tests | Controlled validation error | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-006 | Known bytes hash incorrectly | File identity | `CaptureAsync_RecordsExpectedFileIdentity` | Exact SHA-256 | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-007 | Empty file hash incorrectly | File identity | empty-file snapshot test | Standard empty SHA-256 | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-008 | Read-only model cannot be inspected | File access | read-only snapshot test | Snapshot succeeds; attribute preserved | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-009 | Missing file classification is uncontrolled | File access | missing snapshot/probe tests | `FileNotFoundException` / `MI-OP-MODEL-FILE-NOT-FOUND` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-010 | Directory accepted as model file | File access | directory snapshot test | File-not-found-style rejection | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-011 | Pre-cancelled token ignored | Cancellation | pre-cancel snapshot test | `OperationCanceledException` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-012 | Cancellation during hashing ignored | Cancellation | blocking hasher test | `OperationCanceledException` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-013 | Path fingerprint changes for same canonical path | File identity | stable fingerprint tests | Same SHA-256 fingerprint | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-014 | Changed path/length/timestamp/hash is missed | Integrity | per-field integrity tests | Corresponding flag false; `IsPreserved=false` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-015 | Cancellation conceals changed model | Result finalisation | cancelled + changed integrity test | `MI-OP-MODEL-INTEGRITY-CHANGED` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-016 | Cancellation conceals unverifiable integrity | Result finalisation | cancelled + missing integrity test | `MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-FS-017 | Existing symlink/junction/hard-link alias bypass | Path safety | Filesystem-link process tests | Same underlying file rejected | Tier 2 | Trusted test log | Planned — Tier 2; environment capability recorded |

## Tier 1 — metadata, vocabulary-adjacent evidence, and progress

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-MD-001 | Architecture-scoped Granite values project incorrectly | Metadata | complete Granite projection test | Exact values | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-MD-002 | Missing/blank architecture leaks structure | Metadata | missing/blank architecture tests | Structure null | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-MD-003 | Unknown architecture cannot use its own prefix | Metadata | unknown architecture test | Selected prefix used | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-MD-004 | Other architecture values leak into selected model | Metadata | multi-prefix/wrong-prefix tests | No leakage | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-MD-005 | Missing or blank optional values represented as data | Metadata | optional/empty string tests | Null | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-MD-006 | Zero metadata is silently converted to null | Metadata | zero-values test | Zero preserved | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-MD-007 | Negative/overflow/malformed/culture-specific numbers accepted | Metadata | numeric failure matrix | Null | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-MD-008 | Unsafe native VocabOnly getters reintroduced | Source contract | collector source-contract tests | Forbidden expressions absent | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CHAT-001 | Absent chat-template key appears present | Evidence | absent-key test | `Present=false` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CHAT-002 | Empty/whitespace template is silently rewritten | Evidence | template matrix | Present with exact length/hash | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-CHAT-003 | Unicode template hashes inconsistently | Evidence | Unicode/stable-hash tests | Stable UTF-8 SHA-256 | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROG-001 | NaN enters JSON evidence | Progress | NaN test | Ignored | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROG-002 | Infinity or out-of-range fraction enters evidence | Progress | infinity/clamp tests | Normalised to `0..1` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROG-003 | Consecutive duplicate progress bloats evidence | Progress | duplicate test | Duplicate omitted | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROG-004 | Non-consecutive repeat is incorrectly removed | Progress | non-consecutive test | Preserved | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROG-005 | Concurrent callbacks corrupt progress state | Progress | multi-thread test | Finite valid snapshot | Tier 1 | MTP log | Verified — run `30939159409` |

## Tier 1 — diagnostics, privacy, evidence writing, and type isolation

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-ERR-001 | File/runtime/load exceptions map to wrong diagnostic | Failure mapping | `ProbeFailureMapperTests` matrix | Stable `MI-*` code | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-ERR-002 | Unexpected exception becomes model outcome | Failure mapping | unexpected exception test | `MI-OP-RUNTIME-INSPECTION-FAILED` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PRIV-001 | Canonical model path leaks in message | Redaction | `SensitiveTextRedactorTests` | `<model-path>` replacement | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PRIV-002 | Windows case variant bypasses redaction | Redaction | case-insensitive redaction test | All variants removed | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PRIV-003 | Native logs retain path | Redaction | log-copy test | Every entry redacted | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-JSON-001 | Parent evidence directory is not created | Evidence writer | parent-directory test | Directory and JSON created | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-JSON-002 | JSON naming/enum format drifts | Evidence writer | camel-case/string-enum tests | Contract remains readable | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-JSON-003 | Failed serialization replaces valid evidence | Evidence writer | unserialisable evidence test | Original file unchanged | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-JSON-004 | Cancellation replaces valid evidence | Evidence writer | pre-cancel test | Original file unchanged | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-JSON-005 | Locked/directory destination leaves temp files | Evidence writer | locked/directory tests | Failure and no `*.tmp-*` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-EVID-001 | LLamaSharp/native/XAML type escapes evidence graph | Evidence contract | reflection graph test | Project/framework-safe types only | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-EVID-002 | Full chat template or model path is serialized | Evidence contract | serialization privacy test | Text/path absent; hash retained | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-EVID-003 | Nullable unavailable values become guessed zeroes | Evidence contract | schema/nullable test | Null at safe depth | Tier 1 | MTP log | Verified — run `30939159409` |

## Tier 1 — contained native backend tests

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-PROC-001 | Child stdout/stderr/exit code not captured | Process support | `RunAsync_WhenProcessExits_CapturesStreamsAndExitCode` | Exact observations | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROC-002 | Hung child blocks CI | Process support | timeout test | Tree killed; `TimedOut` | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROC-003 | Caller cancellation leaves process alive | Process support | caller-cancel test | Tree killed; cancellation propagated | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROC-004 | Quoted/Unicode argument altered | Process support | argument preservation test | Exact child output | Tier 1 | MTP log | Verified — run `30939159409` |
| LS-PROC-005 | Published CPU backend cannot load | Native integration | `NativeBackendSmokeProcessTests` | Exit `0`; exact CPU evidence | Tier 1 | Workflow log + JSON | Verified — run `30939159409` |
| LS-PROC-006 | Missing native DLL kills test host | Native integration | `MissingNativeBackendProcessTests` | Parent survives; controlled exit/evidence | Tier 1 | Workflow log | Verified — run `30939159409` |
| LS-PROC-007 | Invalid native image kills test host | Native integration | `InvalidNativeBackendProcessTests` | Parent survives; nonzero child result | Tier 1 | Workflow log | Verified — run `30939159409` |
| LS-PROC-008 | Native smoke evidence leaks model fields | Native integration | `RuntimeEvidenceProcessTests` | Schema `1.0`; no model path/GGUF | Tier 1 | Workflow log + JSON | Verified — run `30939159409` |
| LS-ART-001 | CI uploads a GGUF | Artifact gate | workflow pre-upload scan | Workflow fails before upload | Tier 1 | Workflow log | Verified — run `30939159409` |

## Tier 2 — trusted real-model and hostile-input coverage

The complete Tier 2 project restored and compiled with analyzers in hosted
workflow run `30939159409`. The individual behaviours below remain unverified
until they execute with the controlled model on the trusted runner.

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-RM-001 | Controlled model path/hash/length is wrong | Preconditions | controlled configuration tests | Fail before native call | Tier 2 | Trusted MTP log | Compile verified; trusted execution pending |
| LS-RM-002 | Real Granite success contract regresses | Runtime/model | Granite success test | Exact schema/runtime/model fields; exit `0` | Tier 2 | JSON + log | Compile verified; previous manual baseline verified; expanded execution pending |
| LS-RM-003 | Repeated probing leaks resources or becomes nondeterministic | Runtime/model | three-run repeatability test | Stable evidence; handles closed | Tier 2 | Three JSON files | Compile verified; trusted execution pending |
| LS-CAN-001 | Whole-operation cancellation is misclassified | Cancellation | `--cancel-after-ms` child test | `Cancelled`, `MI-PROBE-CANCELLED`, exit `3` or explicit integrity precedence | Tier 2 | JSON + log | Compile verified; trusted execution pending |
| LS-CAN-002 | Native-load cancellation does not reach LLamaSharp | Cancellation | `--cancel-native-after-ms` child test | Controlled cancellation after runtime selection | Tier 2 | JSON + log | Compile verified; trusted execution pending |
| LS-MAL-001 | Committed malformed fixture kills test host | Hostile input | every manifest `I-*` fixture in child process | Parent survives; hash unchanged; nonzero result | Tier 2 | Matrix JSON/Markdown | Compile verified; trusted execution pending |
| LS-MAL-002 | Random bytes with `.gguf` extension are accepted | Hostile input | deterministic random fixture test | Nonzero controlled/contained result | Tier 2 | Log | Compile verified; trusted execution pending |
| LS-ACC-001 | Locked model is misclassified or modified | File access | locked-model child test | Controlled file failure; hash unchanged | Tier 2 | Log + hash | Compile verified; trusted execution pending |
| LS-ACC-002 | Locked/unavailable output hides probe result | File access | locked-output and invalid-parent tests | Controlled evidence-write failure | Tier 2 | Log | Compile verified; trusted execution pending |
| LS-SEC-001 | Full local path leaks to JSON/stdout/stderr | Privacy | real/malformed process privacy tests | Canonical path absent | Tier 2 | Scan report | Compile verified; trusted execution pending |
| LS-SEC-002 | Probe opens listening or established TCP socket | Offline/security | process socket observation | No process-owned TCP endpoint observed | Tier 2 | Socket observation | Compile verified; trusted execution pending |
| LS-SEC-003 | Evidence artifact contains model copy/hash/size | Artifact security | pre-upload privacy scan | No `.gguf`, model hash, or model-sized file | Tier 2 | Scan report | Compile verified; trusted execution pending |

## Explicit deferrals and later gates

| Risk ID | Failure scenario | Layer | Automated test | Expected result/code | CI tier | Evidence | Status / deferral |
|---|---|---|---|---|---|---|---|
| LS-DEF-001 | Physically disconnected network | Security | Manual disconnected-machine run | Probe completes offline | Later manual gate | Security evidence | Deferred; Actions requires network infrastructure |
| LS-DEF-002 | True x86 native library loaded by x64 process | Native ABI | Controlled x86 fixture | Architecture mismatch contained | Later native-fixture gate | Process log | Deferred; exact licensed x86 fixture not yet selected |
| LS-DEF-003 | Disk completely full during evidence write | File system | Disposable constrained volume | Existing evidence preserved | Later environment gate | Log | Deferred; unsafe on shared runner |
| LS-DEF-004 | Valid real GGUF with no chat template | Model evidence | Provenance-recorded model | `Present=false`, no crash | Later controlled-model gate | JSON | Deferred; suitable model not selected |
| LS-DEF-005 | Hard child memory limit | Process containment | Windows Job Object | Child terminated and recorded | Later hardening gate | Process log | Deferred; not required for first process harness |
| LS-DEF-006 | Power loss / operating-system termination | Resilience | Recovery/manual test | No model corruption; partial evidence not accepted | Later resilience gate | Manual evidence | Deferred; cannot be safely automated in normal CI |
| LS-CPU-001 | Full CPU weight/tensor allocation | Runtime verification | Full-load adapter tests | Load/context/generation results | Later CPU gate | Runtime evidence | Outside scope |
| LS-VK-001 | Vulkan backend initialisation and offload | GPU backend | Vulkan baseline suite | Correct device/offload/no fallback | Later Vulkan gate | GPU evidence | Outside scope |
| LS-TQ-001 | TurboQuant CPU/Vulkan correctness | Optimisation backend | TurboQuant campaign | Formats activate; memory/quality/performance logged | Later TurboQuant gate | Campaign evidence | Outside scope |
| LS-UI-001 | WinUI Cancel button and live progress | Presentation/application | ViewModel/UI tests | Command reaches service; state remains controlled | Later application integration | UI evidence | Outside scope |

## Closure rule

Tier 1 is closed for the model-free CPU boundary because the deterministic,
trusted compile, direct smoke, contained native and artifact-security gates all
completed successfully in workflow run `30939159409`.

Tier 2 closes only after the manual trusted workflow produces reviewed success,
cancellation, hostile-input, privacy, network-observation, disposal and
integrity evidence.
