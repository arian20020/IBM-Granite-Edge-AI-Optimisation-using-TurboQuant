# Hardware Inspection Gate 7 Orchestration and Activation Design

## 1. Scope and decision

Gate 7 turns the verified Gate 2-6 infrastructure into one production `IHardwareInspectionService`. It owns trusted-tool acquisition, bounded evidence collection, the seven application progress events, cancellation, resolution invocation, terminal outcome policy, handoff creation, and production composition.

The approved activation rule is fail closed: the real coordinator is composed in production, but it does not execute or trust an arbitrary LLM Fit installation. LLM Fit must be installed by an administrator at one fixed machine-wide location and must match the application-owned v1.1.9 manifest exactly. A missing or unverifiable installation returns `Failed`, `ApplicationRepairRequired`, and the stable code `HI-TOOL-NOT-AVAILABLE` or `HI-TOOL-INTEGRITY`; it never falls back to PATH search, download, a sibling manifest, or an unsigned bundled copy.

Gate 7 does not change the WinUI page or ViewModel, pace visual stages, interpret model data, calculate compatibility or fit, add NPU inference, download a tool, or claim supported-machine end-to-end acceptance. Those remain Gates 8-9.

## 2. Architectural approach

The implementation uses three focused internal units under `Features/HardwareInspection/Infrastructure/Orchestration`:

1. `HardwareToolAcquisition` owns the two closed trusted-tool identities. It resolves only the fixed administrator LLM Fit root and the fixed packaged llama.cpp probe root, converts application-owned manifests into `TrustedToolPackageManifest`, invokes `TrustedToolPackageVerifier`, and returns disposable custody or a closed acquisition diagnostic.
2. `HardwareEvidenceCollectionCoordinator` owns native-provider scheduling and a one-permit external-process lane. Independent Windows processor, Windows system, storage, DXGI, and NPU captures may overlap. Verified LLM Fit and llama.cpp captures may overlap native work but cannot run external child processes concurrently.
3. `HardwareInspectionService` owns public run semantics. It validates the caller identity/progress sink, emits the exact seven stages with strictly increasing sequence numbers, invokes collection and Gate 6 resolution exactly once, maps one terminal result, and creates a handoff only through `HardwareInspectionRunResult.CreateCompleted`.

`HardwareInspectionOutcomePolicy` remains a small pure helper so outcome and failure mapping can be exhaustively tested without provider or WinUI dependencies.

This split keeps tool trust, provider scheduling, and product outcomes separately reviewable. The service does not parse manifests, touch filesystem paths, or know provider command lines. The collector does not construct product outcomes or handoffs. Gate 6 remains the only canonical evidence resolver.

## 3. Trusted tool acquisition

### 3.1 LLM Fit

The production root is derived from `Environment.SpecialFolder.CommonApplicationData` plus the fixed relative directory `GraniteEdgeAI/HardwareInspection/llmfit/1.1.9/win-x64`. It is not supplied by the caller, environment variables, registry, configuration, command line, current directory, or PATH.

The application owns an exact `TrustedToolPackageManifest` for:

- tool ID `llmfit`;
- version `1.1.9`;
- executable `llmfit.exe` with SHA-256 `db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19`;
- AMD64 PE architecture;
- exact members `llmfit.exe`, `LICENSE`, and `README.md`;
- exact `version --version` and `system --no-dashboard --json system` command identities; and
- `FunctionalPassWithPackagingConcern` disposition, preserving the unsigned and transitive-notice non-claims.

The adjacent installation never supplies its own authority manifest. `TrustedToolPackageVerifier` retains no-follow custody across execution. The application does not redistribute, install, repair, download, or update LLM Fit.

### 3.2 llama.cpp probe

The llama.cpp root is the fixed packaged `HardwareInspection/LlamaCppProbe` directory. Its package-generated manifest is itself inside the signed MSIX and is parsed with strict UTF-8, closed schema, bounded member count, canonical relative names, one exact executable hash, and no duplicate properties. Acquisition converts it into the existing trusted-tool contract and requires the pinned Gate 5 tool ID, version, executable, AMD64 architecture, command set, and complete flat inventory.

The app never searches outside its installed package. Manifest or inventory failure is an application-repair failure. Custody remains alive until both external captures have terminated and cleanup has completed.

### 3.3 Acquisition result

The acquisition API returns either both verified disposable tools or one closed code:

- `ToolNotAvailable` -> `HI-TOOL-NOT-AVAILABLE`;
- `ToolIntegrityFailure` -> `HI-TOOL-INTEGRITY`;
- `PackagedProbeUnavailable` -> `HI-RUNTIME-PACKAGE`.

It carries no path, exception message, hash, username, host name, or arbitrary text. Partial custody is disposed before failure returns.

## 4. Collection lifecycle and concurrency

`HardwareEvidenceCollectionCoordinator.CollectAsync` receives verified tool custody and the run cancellation token. It schedules the following captures once:

- `WindowsProcessorEvidenceProvider.CaptureAsync`;
- `WindowsSystemSnapshotProvider.CaptureAsync`;
- `WindowsStorageEvidenceProvider.CaptureAsync`;
- `DxgiGraphicsEvidenceProvider.CaptureAsync`;
- `INeuralProcessorProbe.CaptureAsync`;
- `ILlmFitHardwareEvidenceProvider.CaptureAsync`; and
- `ILlamaCppCapabilityEvidenceProvider.CaptureAsync`.

The two external providers pass through one `SemaphoreSlim(1, 1)` lane. Waiting for that lane is cancellable. A permit is released in `finally`; tool custody outlives every task. Native captures do not consume this lane, and no ordering guarantee beyond the one-process limit is claimed.

Provider-returned unavailable evidence remains evidence. Failure of one optional provider does not cancel siblings. The closed `WindowsSystemSnapshotException` is converted into `WindowsSystemEvidenceObservation.Unavailable` using its closed diagnostic category and a UTC attempt time. `OperationCanceledException` associated with the run token is never converted to evidence. Any other exception cancels the linked sibling token, awaits all started tasks to observe cleanup, and returns closed orchestration failure without exception text.

Collection creates exactly one `CollectedHardwareEvidence` only after all seven tasks reach a safe terminal state. It never resolves incrementally and never retries within a run.

## 5. Progress and identity

`RunAsync` requires a non-empty caller-supplied `inspectionId`, a non-null progress sink, and a non-cancelled token. It creates one linked cancellation source for the run and one local monotonic sequence counter.

The service emits each existing `HardwareInspectionRunStage` exactly once and in enum order:

1. `StartingHardwareInspection` before tool acquisition;
2. `ReadingProcessorInformation` before scheduling processor capture;
3. `ReadingSystemMemory` before scheduling Windows system and storage capture;
4. `DetectingGraphicsHardware` before scheduling DXGI and NPU capture;
5. `CheckingLocalInferenceRuntimes` before scheduling LLM Fit and llama.cpp capture;
6. `NormalisingHardwareInformation` after collection succeeds and immediately before the sole Gate 6 resolver call;
7. `CreatingHardwareReport` after resolution returns and before terminal mapping.

Every event contains the same inspection ID and sequence `1..7`. `IProgress<T>.Report` is treated as an application callback boundary: if it throws, the run cancels, awaits cleanup, and returns `Failed/ApplicationRepairRequired/HI-PROGRESS-CALLBACK` without emitting further progress. Gate 7 does not delay stages by 500 ms; Gate 8 owns the already approved visual queue and stale-event filtering.

Cancellation before or during acquisition/collection/resolution returns `CreateCancelled(inspectionId)`. No progress or completed result is emitted after cancellation wins. Retry identity and rejection of stale callbacks remain enforced by the existing ViewModel and receive Gate 8 integration coverage.

## 6. Resolution and terminal outcome policy

The service constructs `HardwareEvidenceResolver` with the same injected `TimeProvider` used for application timing and calls `Resolve(inspectionId, evidence)` exactly once.

Terminal mapping is closed and occurs once:

- successful resolution with zero diagnostics -> `Completed`;
- successful resolution with one or more closed diagnostics -> `CompletedWithWarnings`;
- failed resolution -> `Failed/CriticalEvidence/HI-EVIDENCE-UNRESOLVED`;
- verified provider timeout/start/cleanup failure that prevents resolution -> `Failed/TransientOperation/HI-PROVIDER-UNAVAILABLE`;
- trusted-tool acquisition or integrity failure -> the application-repair mappings in section 3.3;
- unexpected orchestration/callback failure -> `Failed/ApplicationRepairRequired/HI-ORCHESTRATION-FAILED` or `HI-PROGRESS-CALLBACK`;
- cooperative cancellation -> `Cancelled`.

The current Gate 6 policy always records `InstructionSetsUnavailable`; therefore a real successful Gate 7 run is truthfully `CompletedWithWarnings` until a separately approved instruction-set provider exists. Tests still cover the pure zero-diagnostic `Completed` branch so the four-outcome contract remains complete.

Only `Completed` and `CompletedWithWarnings` carry the usable snapshot and the existing path-free `HardwareInspectionHandoff`. Failed and cancelled results contain neither snapshot nor handoff. No outcome contains a model compatibility or fit decision.

## 7. Production composition

Production onboarding replaces `UnavailableHardwareInspectionService.Instance` with a single composition-created `HardwareInspectionService` on x64 only after all Gate 7 tests and audits pass. Composition creates:

- `TrustedToolPackageVerifier`;
- the fixed trusted-tool acquisition source;
- one `ExternalProcessRunner` shared by both external providers;
- existing Windows processor/system/storage/DXGI providers;
- `UnavailableNeuralProcessorProbe` until the NPU spike approves a replacement;
- existing LLM Fit and llama.cpp evidence providers;
- the collection coordinator;
- `HardwareEvidenceResolver(TimeProvider.System)`; and
- the production service.

Non-x64 graphs retain no Hardware Inspection infrastructure or Foundation reference. Composition performs no tool verification or hardware capture at app startup; work begins only when `RunAsync` is invoked.

## 8. Testing and acceptance

All production behavior is implemented test-first.

Pure packaged tests cover:

- exact seven-stage identity and sequence;
- single collection and single resolution;
- `Completed`, `CompletedWithWarnings`, `Failed`, and `Cancelled`;
- handoff only for completed outcomes;
- pre-cancellation and cancellation in tool wait, native capture, external capture, and post-collection boundary;
- progress callback failure and no later progress;
- optional provider unavailability without sibling cancellation;
- unexpected provider failure with linked sibling cancellation and awaited cleanup;
- one-permit external concurrency and native/external overlap;
- Windows system closed-exception conversion;
- no retries and no partial resolution;
- exact failure-kind and safe-code mapping; and
- production composition uses the real service without executing at construction.

Trusted-tool tests cover fixed roots, exact embedded LLM Fit authority, strict packaged-manifest parsing, missing/tampered/wrong-architecture/extra-member packages, custody disposal, no PATH/environment/registry override, and privacy-safe diagnostics. Real-process Foundation custody tests remain authoritative for child termination and output bounds.

Gate exit requires fresh Foundation and probe suites, Hardware/runner Python contracts, focused Gate 6-7 packaged tests, the authoritative Hardware Inspection/model-handoff/onboarding packaged regression, Debug/x64 packaged build, Release/x64 MSIX build, evaluated non-x64 exclusion, source/dependency/privacy audits, and review. No certificate, binary, package, TRX, raw provider output, host fact, private path, or tool installation is committed.

## 9. Non-claims

- Gate 7 does not redistribute or approve the unsigned LLM Fit candidate.
- A local administrator installation is not accepted unless the application-owned manifest verifies it exactly.
- Gate 7 does not establish public-trust signing or supported-machine Smart App Control acceptance.
- Gate 7 does not complete WinUI integration, visual pacing, accessibility acceptance, or supported Intel-machine end-to-end evidence.
- Gate 7 does not calculate model compatibility, model fit, usable graphics memory, primary GPU, Intel classification, or NPU presence by inference.
- The full Hardware Inspection feature remains incomplete until Gates 8-9 pass.
