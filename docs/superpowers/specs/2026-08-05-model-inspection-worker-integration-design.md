# Model Inspection Worker Integration — Production Design

**Status:** Approved design baseline; awaiting written-specification review  
**Date:** 2026-08-05  
**Branch:** `feature/model-inspection-runtime-integration`  
**Stacked base branch:** `feature/model-inspection`  
**Base commit:** `c3276d50fe39ff0db8ae679c236a5d02cf21fe14`  
**Scope:** Production GGUF Model Inspection contracts, protected worker process, LLamaSharp runtime boundary, classification, application service, ViewModel integration, packaging, testing, and documentation  
**Related chat decision:** Chat inference will later launch a pinned `llama-cli.exe` directly; it will not use an HTTP server or listening port

---

## 1. Executive summary

The application will inspect GGUF models through a dedicated local worker executable:

```text
GraniteEdgeAI.exe
    ↓
ModelInspectionViewModel
    ↓
IModelInspectionService
    ↓
ILlamaModelProbe
    ↓
WorkerProcessLlamaModelProbe
    ↓
GraniteEdgeAI.ModelInspection.Worker.exe
    ↓
LLamaSharp 0.27.0
    ↓
matched llama.cpp CPU runtime
```

The worker performs the technical inspection and returns structured evidence. It does **not** select WinUI controls or decide the final user-facing model outcome. The application classifier converts the returned evidence into one of six model outcomes:

1. `Ready`
2. `ReadyWithWarnings`
3. `ConversionRequired`
4. `IncompletePackage`
5. `Unsupported`
6. `Invalid`

`Cancelled` and `OperationalFailure` remain execution states, not model outcomes.

The worker communicates with the application through redirected standard input, standard output, and standard error. It does not open an HTTP server, listen on a TCP port, or require network access. The model path is sent through standard input rather than placed on the process command line.

Chat remains a separate later feature:

```text
GraniteEdgeAI.exe
    ↓
Chat service
    ↓
pinned llama-cli.exe
    ↓
local Granite inference
```

The inspection worker and `llama-cli.exe` must have recorded runtime identities. Before chat integration, their llama.cpp revisions must either be aligned or explicitly verified as compatible.

---

## 2. Context and evidence supporting the decision

The `feature/model-inspection` branch established the current presentation and feasibility foundation:

- Model Import → Model Inspection navigation;
- onboarding-stage synchronisation;
- the initial Model Inspection page and reusable presentation controls;
- an isolated LLamaSharp CPU feasibility tool;
- deterministic, contained native, and trusted real-model test tiers;
- read-only model hashing and integrity checks;
- path and chat-template privacy controls;
- native child-process containment for testing.

Verified evidence includes:

```text
Tier 1 deterministic tests:       170 / 170 passed
Tier 1 contained native tests:     4 / 4 passed
Tier 1 Release build/publish:      passed
Tier 1 CPU runtime smoke:          passed
Tier 1 artifact privacy gate:      passed

Tier 2 trusted tests:              20 / 20 passed
Tier 2 failed / skipped:           0 / 0
Controlled Granite hash changed:  no
Retained evidence files scanned:  56
Privacy findings:                 0
```

A previous `VocabOnly` experiment also demonstrated that unsafe native access can trigger a process-level llama.cpp abort before managed C# exception handling can recover. That observation changes worker-process isolation from a possible future hardening measure into a production requirement for Model Inspection.

Relevant evidence:

- `docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md`
- `docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md`
- `docs/testing/evidence/2026-08-05-model-inspection-branch-review.md`
- `docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md`

---

## 3. Relationship to the earlier Model Inspection design

This specification supersedes only the parts of the earlier design that deferred worker-process isolation or proposed direct in-process LLamaSharp loading.

Earlier design:

- `docs/superpowers/specs/2026-07-30-model-inspection-design.md`

Preserved decisions:

- WinUI page → ViewModel → application service → runtime probe layering;
- project-owned data contracts;
- deterministic application classifier;
- lightweight inspection before Hardware Fit;
- operational failures separated from model invalidity;
- Ready and Ready-with-warnings as the only Hardware Fit continuation outcomes;
- one shared page and reusable result/presentation components;
- stale-result protection and cancellable asynchronous work.

Superseded decisions:

- direct `LlamaSharpModelProbe` inside the WinUI process;
- worker-process isolation being deferred;
- passing only a path through navigation;
- treating `InvalidOrIncomplete` as one combined outcome;
- using the engineering feasibility executable as production infrastructure.

The feasibility tool remains an engineering front end. The production worker will use the same extracted runtime implementation but will have a separate protocol, lifecycle, packaging, and security boundary.

---

## 4. Goals

The implementation must:

1. replace the current path-only handoff with an immutable validated `ModelInspectionRequest`;
2. preserve the existing quick-scan evidence without coupling Model Inspection to Model Import internals;
3. run LLamaSharp and native llama.cpp outside the WinUI process;
4. communicate locally without HTTP, localhost, or a listening port;
5. stream truthful progress through a versioned bounded protocol;
6. support cooperative cancellation and forced cleanup after a bounded grace period;
7. retain the WinUI process when the worker or native runtime crashes;
8. return project-owned technical evidence without native handles or XAML types;
9. classify model outcomes in deterministic application code rather than the worker;
10. keep infrastructure failures, cancellation, and model outcomes separate;
11. preserve the selected model byte-for-byte;
12. prevent canonical local paths, full chat templates, model bytes, and secrets from leaking into results or retained logs;
13. include the worker and exact native dependencies in x64 build, publish, and MSIX outputs;
14. keep the WinUI application project free from LLamaSharp and TurboQuant references;
15. add source-adjacent READMEs, architecture records, tests, and verification evidence at every completed gate.

---

## 5. Non-goals

This design does not implement or claim:

- chat UI or chat orchestration;
- `llama-cli.exe` prompting or token streaming;
- an HTTP server, localhost endpoint, or listening port;
- full tensor allocation;
- context or KV-cache creation;
- token generation;
- TTFT, tokens-per-second, RAM, KV memory, or quality benchmarking;
- Vulkan initialisation or GPU offload;
- TurboQuant, PolarQuant, QJL, or TurboVec;
- OpenVINO model inspection;
- Hardware Fit or LLM Fit integration;
- x86 or ARM64 native worker support;
- worker auto-update or download;
- user-selectable worker executables;
- named-pipe IPC in protocol version 1;
- Windows App Service activation;
- exported end-user inspection reports.

These remain separate design and verification campaigns.

---

## 6. Alternatives considered

### 6.1 Direct LLamaSharp inside the WinUI process — rejected

```text
GraniteEdgeAI.exe
    ↓
LLamaSharp
    ↓
llama.cpp
```

Advantages:

- fewer projects;
- no process protocol;
- simpler initial wiring.

Reasons for rejection:

- a native abort can terminate the complete WinUI process;
- managed `try/catch` cannot reliably contain process-level native termination;
- native dependencies would enter the application deployment boundary directly;
- testing failure paths would require risking the UI test host.

### 6.2 `llama-cli.exe` for both inspection and chat — rejected for inspection

Advantages:

- one native executable family;
- fewer custom native integration components.

Reasons for rejection:

- console log wording is not a stable structured inspection API;
- parsing ordinary llama.cpp logs would couple the application to revision-specific text;
- lightweight metadata, tokenizer, chat-template, integrity, and progress contracts are easier to guarantee through the tested LLamaSharp path;
- inspection and generation have different lifecycle and evidence needs.

`llama-cli.exe` remains the approved later chat route.

### 6.3 One custom worker for both inspection and chat — rejected

Reasons for rejection:

- combines short-lived inspection with long-running generation sessions;
- increases protocol, state, streaming, and cancellation complexity;
- duplicates capabilities already provided by `llama-cli.exe`;
- reduces component cohesion.

### 6.4 Windows App Service — rejected for protocol version 1

Reasons for rejection:

- requires additional activation, manifest, lifecycle, and deployment complexity;
- does not improve the narrow one-client/one-worker inspection use case enough to justify that complexity;
- standard redirected streams already provide local, port-free communication.

### 6.5 Named pipe — deferred

A named pipe could support a long-lived bidirectional worker. The first version requires only one request per short-lived process, so redirected standard streams are simpler and proportionate. The `ILlamaModelProbe` boundary allows a future transport replacement without changing the page, ViewModel, service, or classifier.

---

## 7. Selected system architecture

### 7.1 Model Inspection route

```text
ModelImportPage
    ↓ immutable validated request
OnboardingShellPage
    ↓
ModelInspectionPage
    ↓ binding and commands
ModelInspectionViewModel
    ↓
IModelInspectionService
    ↓
ModelInspectionService
    ├── ILlamaModelProbe
    │       ↓
    │   WorkerProcessLlamaModelProbe
    │       ↓
    │   IInspectionWorkerProcess
    │       ↓
    │   GraniteEdgeAI.ModelInspection.Worker.exe
    │       ↓
    │   IWorkerInspectionEngine
    │       ↓
    │   LlamaSharpInspectionEngine
    │       ↓
    │   LLamaSharp / matched llama.cpp CPU runtime
    │
    └── ModelInspectionClassifier
```

Commands travel down the dependency chain. Progress and results return upward as ordinary immutable data.

### 7.2 Chat route

```text
ChatPage
    ↓
ChatViewModel
    ↓
IChatService
    ↓
ILlamaCliProcessAdapter
    ↓
pinned llama-cli.exe
```

This route is documented now but implemented later.

### 7.3 Dependency direction

Allowed project references:

```text
WinUI application
    → shared worker-contract project

Worker executable
    → shared worker-contract project
    → LLamaSharp runtime library

LLamaSharp runtime library
    → LLamaSharp 0.27.0
    → LLamaSharp.Backend.Cpu 0.27.0

Engineering spike
    → LLamaSharp runtime library
```

Forbidden references:

```text
WinUI application
    ✕ LLamaSharp
    ✕ LLamaSharp.Backend.Cpu
    ✕ worker project
    ✕ engineering spike project

Shared contracts
    ✕ WinUI
    ✕ LLamaSharp
    ✕ worker implementation
    ✕ application service implementation
```

---

## 8. Trust boundaries

The selected GGUF is untrusted local input.

```text
User-selected model file
        │ untrusted file boundary
        ▼
Worker process
        │ native-runtime boundary
        ▼
LLamaSharp / llama.cpp
        │ bounded JSON protocol boundary
        ▼
WinUI application
```

The worker process is a safety container, not a trust elevation. The application validates every worker message even though the worker ships with the app.

Trust rules:

- the model is opened read-only;
- the model path must be absolute and canonicalisable;
- the worker must not accept an evidence-output path;
- the worker must not write beside the installed executable;
- the worker must not search the current directory or `PATH` for alternate native binaries;
- the application resolves the worker only from its controlled installed/build output location;
- the worker returns no native pointer, handle, model bytes, complete chat template, or canonical directory path;
- stdout is treated as untrusted protocol input by the application;
- stderr is bounded and redacted before retention;
- an inconsistent exit code and terminal result is an operational protocol failure.

---

## 9. Project structure

```text
shared/
└── GraniteEdgeAI.ModelInspection.Contracts/
    ├── README.md
    ├── GraniteEdgeAI.ModelInspection.Contracts.csproj
    ├── Protocol/
    │   ├── WorkerProtocol.cs
    │   ├── WorkerMessageKind.cs
    │   ├── WorkerCommandKind.cs
    │   ├── WorkerHelloMessage.cs
    │   ├── WorkerStartInspectionCommand.cs
    │   ├── WorkerCancelInspectionCommand.cs
    │   ├── WorkerStartedMessage.cs
    │   ├── WorkerProgressMessage.cs
    │   ├── WorkerCompletedMessage.cs
    │   └── WorkerOperationalFailure.cs
    └── Evidence/
        ├── WorkerInspectionEvidence.cs
        ├── WorkerRuntimeIdentity.cs
        ├── WorkerModelFileEvidence.cs
        ├── WorkerModelConfigurationEvidence.cs
        ├── WorkerTokenizerEvidence.cs
        ├── WorkerChatTemplateEvidence.cs
        ├── WorkerModelStructureEvidence.cs
        └── WorkerObservation.cs

runtime/
└── GraniteEdgeAI.ModelInspection.LlamaSharp/
    ├── README.md
    ├── GraniteEdgeAI.ModelInspection.LlamaSharp.csproj
    ├── IWorkerInspectionEngine.cs
    ├── LlamaSharpInspectionEngine.cs
    ├── RuntimeConfiguration.cs
    ├── FileIdentityService.cs
    ├── NativeProgressRecorder.cs
    ├── EvidenceCollector.cs
    ├── FailureMapper.cs
    └── SensitiveTextRedactor.cs

workers/
└── GraniteEdgeAI.ModelInspection.Worker/
    ├── README.md
    ├── GraniteEdgeAI.ModelInspection.Worker.csproj
    ├── Program.cs
    ├── WorkerHost.cs
    ├── Protocol/
    │   ├── WorkerCommandReader.cs
    │   ├── WorkerMessageWriter.cs
    │   ├── WorkerProtocolStateMachine.cs
    │   └── WorkerProtocolLimits.cs
    └── Inspection/
        └── WorkerInspectionCoordinator.cs

IBM Granite with TurboQuant (Intel)/
└── Features/
    └── ModelInspection/
        ├── README.md
        ├── ModelInspectionPage.xaml
        ├── ModelInspectionPage.xaml.cs
        ├── Contracts/
        │   ├── README.md
        │   ├── ModelInspectionRequest.cs
        │   ├── ValidatedQuickScanSnapshot.cs
        │   ├── ExpectedModelFileIdentity.cs
        │   ├── ModelInspectionExecutionResult.cs
        │   ├── ModelInspectionResult.cs
        │   ├── ModelInspectionFinding.cs
        │   └── ModelInspectionEnums.cs
        ├── Runtime/
        │   ├── README.md
        │   ├── ILlamaModelProbe.cs
        │   ├── WorkerProcessLlamaModelProbe.cs
        │   ├── IInspectionWorkerProcess.cs
        │   ├── InspectionWorkerProcess.cs
        │   ├── InspectionWorkerPathResolver.cs
        │   ├── WorkerRequestMapper.cs
        │   └── WorkerResultMapper.cs
        ├── Classification/
        │   ├── README.md
        │   └── ModelInspectionClassifier.cs
        ├── Services/
        │   ├── README.md
        │   ├── IModelInspectionService.cs
        │   └── ModelInspectionService.cs
        ├── ViewModels/
        │   ├── README.md
        │   └── ModelInspectionViewModel.cs
        ├── Controls/
        ├── Models/
        └── Presentation/
```

Files may be split further when a class gains more than one responsibility, but unrelated abstractions must not be added speculatively.

---

## 10. Application contracts

### 10.1 `ModelInspectionRequest`

The application request is immutable and contains:

```text
ModelPath
FileName
ExpectedModelFileIdentity
ValidatedQuickScanSnapshot
```

Required invariants:

- `ModelPath` is non-empty;
- `FileName` is the final filename only;
- `ExpectedModelFileIdentity.LengthBytes` is positive;
- `ExpectedModelFileIdentity.LastWriteTimeUtc` is present;
- `ValidatedQuickScanSnapshot.Format` is `GGUF` for this worker;
- the quick-scan outcome was successful before request creation;
- the request contains no Model Import page, scanner, XAML control, native handle, or LLamaSharp type.

### 10.2 `ValidatedQuickScanSnapshot`

```text
Format
ModelName
Architecture
ParameterSizeLabel
Quantisation
FileSizeBytes
DeclaredContextLength
GgufVersion
```

This snapshot is copied from the successful quick-scan result. Model Inspection does not retain or depend directly on the internal `ModelQuickScanResult` type.

### 10.3 Navigation handoff

The current `ModelInspectionRequestedEventArgs` changes from carrying `string ModelPath` to carrying `ModelInspectionRequest Request`.

```text
ModelImportPage
    → validates current successful state
    → captures current file identity
    → creates ModelInspectionRequest
    → raises ModelInspectionRequested

OnboardingShellPage
    → receives request
    → navigates to ModelInspectionPage with request

ModelInspectionPage
    → validates parameter type
    → creates or initialises ModelInspectionViewModel
```

A disabled button is not the only guard. The guarded request-creation method must independently reject missing or stale validated state.

---

## 11. Worker protocol

### 11.1 Transport

The transport is one compact UTF-8 JSON object per line:

```text
stdin   → application commands
stdout  → worker protocol messages only
stderr  → bounded redacted diagnostics only
```

Process configuration:

```text
UseShellExecute = false
RedirectStandardInput = true
RedirectStandardOutput = true
RedirectStandardError = true
CreateNoWindow = true
```

The model path is not supplied as a command-line argument.

### 11.2 Protocol constants

```text
Protocol version:                 1
Maximum command/message line:     1 MiB UTF-8
Maximum retained stderr:          256 KiB UTF-8
Startup handshake timeout:        5 seconds
Overall inspection timeout:       5 minutes
Graceful cancellation timeout:    5 seconds
Requests per worker process:      1
Terminal results per request:     1
```

These values are named constants, covered by tests, and not repeated as unexplained literals.

### 11.3 Compatibility policy

Within protocol version 1:

- unknown additional JSON properties are tolerated;
- missing required properties fail validation;
- unknown command or message kinds fail validation;
- unknown enum values fail validation;
- breaking field or sequence changes require protocol version 2;
- the application does not silently downgrade to an older protocol;
- the application validates the worker runtime profile before sending the model path.

### 11.4 Handshake

The worker immediately writes one `hello` message:

```json
{
  "protocolVersion": 1,
  "messageType": "hello",
  "workerVersion": "1.0.0",
  "runtimeProfile": "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
  "processArchitecture": "X64"
}
```

The `hello` message is connection-scoped and deliberately has no `requestId`, because no inspection request exists yet.

The application verifies:

- supported protocol version;
- expected worker identity/version policy;
- exact runtime profile;
- `X64` process architecture;
- the handshake arrives within five seconds;
- stdout contained no preceding non-protocol text.

Failure becomes `OperationalFailure`.

### 11.5 Start command

After a valid handshake, the application writes one start command:

```json
{
  "protocolVersion": 1,
  "commandType": "startInspection",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534",
  "parentProcessId": 1234,
  "modelPath": "C:\\Models\\granite.gguf",
  "expectedFileIdentity": {
    "lengthBytes": 2099501664,
    "lastWriteTimeUtc": "2026-08-05T00:00:00Z"
  },
  "quickScan": {
    "format": "GGUF",
    "architecture": "granite",
    "modelName": "Granite 4.1 3B",
    "parameterSizeLabel": "3B",
    "quantisation": "Q4_K_M",
    "declaredContextLength": 131072,
    "ggufVersion": 3
  }
}
```

Request rules:

- `requestId` is a canonical GUID string;
- `parentProcessId` is positive;
- `modelPath` is absolute;
- `modelPath` is canonicalised before file access;
- request length is bounded before deserialization;
- the worker accepts exactly one start command;
- malformed or duplicate starts return protocol failure and exit code `2`;
- the model path is never echoed in stdout or stderr.

### 11.6 Started message

```json
{
  "protocolVersion": 1,
  "messageType": "started",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534"
}
```

### 11.7 Progress message

```json
{
  "protocolVersion": 1,
  "messageType": "progress",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534",
  "stage": "ReadModelConfiguration",
  "stageStatus": "Active",
  "completedStageCount": 1,
  "totalStageCount": 5,
  "stageFraction": null
}
```

Stable protocol stages:

1. `CheckModelPackage`
2. `ReadModelConfiguration`
3. `ValidateTokenizerAndChatSetup`
4. `ValidateModelStructure`
5. `ConfirmCoreRuntimeCompatibility`

Progress rules:

- stage order cannot move backwards;
- completed count cannot decrease;
- completed count must be in `0..5`;
- `stageFraction` is nullable;
- only genuine runtime fractions are reported;
- `NaN` and infinity are not serializable protocol values;
- finite fractions are constrained to `0..1`;
- duplicated consecutive progress may be coalesced;
- the ViewModel maps protocol stages to existing presentation rows.

### 11.8 Cancel command

```json
{
  "protocolVersion": 1,
  "commandType": "cancelInspection",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534"
}
```

Rules:

- cancellation is valid only after a start command and before a terminal result;
- repeated cancel commands are idempotent;
- wrong request ID is a protocol failure;
- cancellation reaches a linked `CancellationTokenSource` in the worker;
- the worker still attempts final model-integrity verification;
- the worker emits one cancelled terminal result when cooperative cancellation completes;
- the application kills the complete process tree after the five-second grace period if the worker does not exit.

### 11.9 Terminal completion message

The worker operation status is separate from the application model outcome.

```json
{
  "protocolVersion": 1,
  "messageType": "completed",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534",
  "completionStatus": "Completed",
  "evidence": {}
}
```

Worker completion statuses:

- `Completed` — the worker completed the diagnostic workflow and returned reliable technical evidence. The evidence may still classify the model as Ready, warning, conversion required, incomplete, unsupported, or invalid.
- `Cancelled` — the user/application cancelled the operation.
- `OperationalFailure` — reliable model classification evidence could not be produced because the worker, protocol, filesystem, runtime infrastructure, integrity verification, or native backend failed operationally.

This distinction prevents a technically successful diagnosis of an invalid model from being confused with a failed worker operation.

### 11.10 Exit codes

```text
0 = worker operation completed and terminal status is Completed
1 = terminal status is OperationalFailure
2 = invalid command, invalid protocol, or protocol state violation
3 = terminal status is Cancelled
```

The application validates the terminal message against the process exit code.

Examples:

```text
Completed + exit 0               valid
Cancelled + exit 3               valid
OperationalFailure + exit 1      valid
Completed + exit 1               inconsistency → operational failure
No terminal JSON + crash code    worker crash → operational failure
Timeout + forced kill            timeout → operational failure
User cancel + forced kill        cancelled with forced-termination diagnostic
```

---

## 12. Protocol state machine

```text
Created
    ↓ worker writes hello
HelloSent
    ↓ application sends start
InspectionStarted
    ├── zero or more progress messages
    ├── application may send cancel
    ├── stdin EOF / parent exit requests cancellation
    ↓
TerminalMessageSent
    ↓
ProcessExit
```

Invalid sequences:

- output before hello;
- two hello messages;
- progress before start;
- request-scoped message with wrong request ID;
- backward stage transition;
- two terminal messages;
- message after terminal completion;
- exit without a terminal message, except process-level crash;
- start after cancellation or completion;
- more than one inspection request per worker.

Every invalid sequence is covered by a test-only protocol worker or worker unit test.

---

## 13. Worker lifecycle and orphan prevention

### 13.1 Startup

The application:

1. resolves the worker path from the controlled application installation/build location;
2. confirms the current process architecture is x64;
3. confirms the worker file exists;
4. starts the worker without a shell;
5. begins asynchronous stdout and stderr readers immediately;
6. waits at most five seconds for hello;
7. validates hello;
8. sends the start command.

### 13.2 Normal completion

The application:

1. reads progress until one terminal message;
2. waits for process exit;
3. verifies the exit-code/result pairing;
4. awaits both redirected-stream readers;
5. disposes process resources;
6. maps worker evidence to application evidence;
7. calls the classifier only for `Completed` worker status.

### 13.3 Cancellation

The application:

1. sends one cancel command;
2. marks cancellation requested;
3. waits up to five seconds;
4. kills the complete process tree when the worker does not exit;
5. awaits stream readers;
6. disposes the process;
7. returns application execution status `Cancelled`;
8. records whether cancellation was cooperative or forced.

### 13.4 Parent disappearance

The worker must not continue indefinitely if the WinUI application terminates.

The worker cancels when either:

- standard input reaches EOF before normal completion; or
- the recorded parent process no longer exists.

A future Windows Job Object may add stronger kill-on-parent-close semantics. Parent-PID monitoring plus stdin EOF is the required first implementation.

### 13.5 Overall timeout

The adapter enforces a five-minute overall timeout beginning immediately after process start.

On timeout:

- send cancellation when possible;
- wait the normal cancellation grace period;
- kill the complete process tree;
- return `OperationalFailure` with `MI-OP-WORKER-TIMEOUT`;
- do not call the model classifier.

---

## 14. Worker technical evidence

### 14.1 Runtime identity

```text
WorkerVersion
ProtocolVersion
RuntimeProfile
LLamaSharpVersion
BackendPackageVersion
MappedLlamaCppCommit
NativeLibraryName
ProcessArchitecture
InspectionMode
UsesCuda
UsesVulkan
GpuLayerCount
```

Expected first profile:

```text
LLamaSharp:             0.27.0
CPU backend:            0.27.0
Mapped llama.cpp:       3f7c29d318e317b63f54c558bc69803963d7d88c
Runtime identifier:     win-x64
Inspection mode:        VocabOnly
CUDA:                   false
Vulkan:                 false
GPU layers:             0
```

### 14.2 Model file evidence

```text
FileName
LengthBefore
LengthAfter
LastWriteTimeBefore
LastWriteTimeAfter
Sha256Before
Sha256After
IntegrityPreserved
```

The canonical full path is never returned.

### 14.3 Configuration evidence

```text
Architecture
ModelName
FileType
QuantisationVersion
TokenizerModel
DeclaredContextLength
EmbeddingSize
LayerCount
AttentionHeadCount
KvHeadCount
ParameterCount
```

Unavailable values remain `null`. Values are not guessed from the filename.

### 14.4 Tokenizer evidence

```text
VocabularyCount
VocabularyType
TokenizerSmokePassed
TokenizerSmokeTokenCount
KnownSpecialTokenIds
```

The smoke input is fixed, non-sensitive, and versioned.

### 14.5 Chat-template evidence

```text
Present
LengthCharacters
Sha256
```

The complete chat template does not cross the worker boundary.

### 14.6 Observations

The worker returns technical observations, not final application findings.

```text
Code
TechnicalCategory
TechnicalDetail
```

Examples:

```text
MI-OBS-CHAT-TEMPLATE-MISSING
MI-OBS-OPTIONAL-METADATA-MISSING
MI-OBS-MODEL-STRUCTURE-MALFORMED
MI-OBS-SPLIT-SHARD-MISSING
MI-OBS-ARCHITECTURE-UNSUPPORTED
MI-OBS-TOKENIZER-UNSUPPORTED
```

Observation codes are stable and testable. User-facing titles and recommended actions belong to the application classifier/presentation layer.

---

## 15. File continuity and integrity

### 15.1 Before worker launch

The application request carries the file length and last-write timestamp captured from the validated selection.

### 15.2 Worker preflight

The worker:

- canonicalises the path;
- confirms it is a file;
- opens it read-only;
- compares current length and timestamp with the request;
- computes SHA-256;
- records the pre-inspection snapshot.

A continuity mismatch is operational evidence that the selected file changed after quick scan. It is not automatically proof that the new file is invalid.

### 15.3 Worker postflight

The worker:

- disposes native resources;
- captures a second read-only snapshot;
- compares path fingerprint, length, timestamp, and SHA-256;
- makes changed or unverifiable integrity outrank success or cancellation.

`IntegrityPreserved=false` or unverifiable integrity produces `OperationalFailure` and bypasses model classification.

---

## 16. Application execution and classification model

### 16.1 Execution status

```text
Completed
Cancelled
OperationalFailure
```

Invariants:

- `Completed` requires worker evidence and one classified `ModelInspectionResult`;
- `Cancelled` has no model outcome;
- `OperationalFailure` has no model outcome and includes a stable operational diagnostic;
- the classifier is never called for Cancelled or OperationalFailure.

### 16.2 Model outcomes

```text
Ready
ReadyWithWarnings
ConversionRequired
IncompletePackage
Unsupported
Invalid
```

### 16.3 Classifier precedence

The first applicable blocking rule wins:

```text
1. Invalid
2. IncompletePackage
3. Unsupported
4. ConversionRequired
5. ReadyWithWarnings
6. Ready
```

Operational failures and cancellation are handled before this list.

### 16.4 Invalid

Use only when reliable technical evidence establishes that the file itself is malformed or corrupt.

Examples:

- invalid GGUF header;
- truncated metadata;
- impossible field encoding;
- corrupt required tokenizer metadata;
- internally inconsistent required structure.

### 16.5 Incomplete package

Use when the package is recognisable but a required companion or component is missing.

Examples:

- missing split GGUF shard;
- required vocabulary data absent;
- required model component missing.

A missing optional chat template alone is not incomplete.

### 16.6 Unsupported

Use when the model/package is readable but the pinned runtime cannot support a required feature.

Examples:

- unsupported architecture;
- unsupported tokenizer implementation;
- unsupported GGUF feature.

### 16.7 Conversion required

Use only when a tested and versioned conversion/preparation route exists.

Required fields:

```text
VerifiedConversionRouteId
RecommendedAction
```

Without a verified route, the classifier returns `Unsupported`, not `ConversionRequired`.

### 16.8 Ready with warnings

Use when all required checks pass but non-blocking findings exist.

Examples:

- chat template missing but configurable later;
- optional metadata unavailable;
- declared context needs review during Hardware Fit;
- filename and embedded model name differ.

### 16.9 Ready

Use only when:

- required package checks pass;
- runtime recognises the model;
- tokenizer evidence is usable;
- structure evidence passes;
- integrity is preserved;
- no blocking findings exist;
- no warnings exist.

### 16.10 Hardware Fit continuation

```text
Ready                  → allowed
ReadyWithWarnings      → allowed
ConversionRequired     → blocked
IncompletePackage      → blocked
Unsupported            → blocked
Invalid                → blocked
Cancelled              → blocked
OperationalFailure     → blocked
```

---

## 17. Application service responsibility

`ModelInspectionService` coordinates the use case:

1. validate the application request;
2. publish `CheckModelPackage` progress;
3. call `ILlamaModelProbe`;
4. map worker progress to application progress;
5. map worker technical evidence to application evidence;
6. bypass classification for cancellation/operational failure;
7. classify completed evidence;
8. create one immutable execution result;
9. preserve cancellation and stale-run identity.

The service does not:

- reference XAML controls;
- navigate pages;
- construct native LLamaSharp types;
- read worker stdout directly;
- choose visual colours/icons;
- perform Hardware Fit.

---

## 18. ViewModel responsibility

`ModelInspectionViewModel` owns observable page state:

```text
Request
PageState
CurrentStage
StagePresentations
CurrentStageFraction
Result
Findings
CanCancel
CanRetry
CanChooseAnotherModel
CanContinueToHardwareFit
```

Commands:

- start inspection automatically after valid navigation and loaded state;
- cancel inspection;
- retry inspection;
- choose another model;
- show finding details;
- continue to Hardware Fit when eligible.

Stale-result rule:

- each run has a unique run identity;
- a replacement run publishes its identity before cancelling the previous run;
- progress and result callbacks check the current identity;
- an old run cannot update the page after retry, replacement, removal, or navigation.

The current disabled Cancel presentation becomes enabled only while a real active cancellable service run exists. Supporting text must not claim that the user can return or cancel unless the corresponding command is functional.

---

## 19. Security and privacy requirements

### 19.1 Process launch

- absolute controlled executable path;
- no shell execution;
- no command-line model path;
- no arbitrary user-supplied executable;
- controlled working directory;
- redirected streams read asynchronously;
- complete process-tree termination on timeout/forced cancellation.

### 19.2 Protocol input

- line length checked before deserialization;
- UTF-8 decoding errors fail closed;
- JSON depth bounded by serializer settings;
- required fields validated explicitly;
- request ID compared ordinally;
- message sequence checked by a state machine;
- unknown message kinds rejected;
- additional known-version fields tolerated.

### 19.3 Sensitive data

Never return or retain:

- canonical model directory path;
- complete chat-template text;
- model bytes;
- prompt or health information;
- environment variables;
- native pointers/handles;
- arbitrary native logs without redaction.

Allowed:

- filename;
- path fingerprint hash;
- model SHA-256;
- metadata keys/values approved by the evidence contract;
- bounded redacted technical diagnostics.

### 19.4 Network

The worker:

- opens no HTTP endpoint;
- opens no listening TCP port;
- performs no download;
- requires no internet access;
- is verified with process-owned TCP observation tests.

### 19.5 Package trust

The application starts the worker only from its signed/controlled package or local build output. It does not search `PATH`, the model directory, downloads, or current working directory.

---

## 20. Packaging and deployment

### 20.1 Packaged application

The existing WinUI application is configured as a packaged application. The x64 package must include a dedicated worker folder containing:

```text
GraniteEdgeAI.ModelInspection.Worker.exe
GraniteEdgeAI.ModelInspection.Contracts.dll
GraniteEdgeAI.ModelInspection.LlamaSharp.dll
LLamaSharp.dll
LLamaSharp.Backend.Cpu dependencies
matched native llama.cpp CPU libraries
required .NET runtime configuration files
```

The application resolves the worker from the installed package location or controlled build output, not from `PATH`.

### 20.2 Read-only installed location

The worker treats its installation directory as read-only.

It does not:

- write evidence beside the executable;
- unpack native libraries into the package folder;
- modify package files;
- create temporary files under the installation root.

Protocol results are streamed through stdout. Any later retained application diagnostics must use the application’s controlled local-data/evidence directory.

### 20.3 x64-only first production boundary

Only `win-x64` has verified LLamaSharp/native evidence.

Therefore:

- the first production worker is built and packaged only for x64;
- x64 application build/publish/MSIX outputs must contain the complete worker closure;
- the adapter checks `RuntimeInformation.ProcessArchitecture == X64` before launch;
- x86/ARM64 application builds must not silently launch an x64 worker;
- unsupported process architecture returns `MI-OP-WORKER-ARCHITECTURE-UNSUPPORTED`;
- no x86/ARM64 Model Inspection support claim is made until separate native and packaging gates pass.

The application may continue compiling for other platforms, but this feature remains explicitly unavailable there.

### 20.4 Package verification

Automated checks inspect:

1. worker Release build output;
2. application publish output;
3. generated x64 MSIX contents.

They verify:

- worker executable exists;
- shared contracts exist;
- runtime library exists;
- LLamaSharp assemblies exist;
- approved CPU native library exists;
- CUDA and Vulkan worker binaries are absent;
- no GGUF is included;
- no development evidence or test fixture is included;
- worker handshake runtime profile matches the packaged dependency policy.

---

## 21. Chat and `llama-cli.exe` compatibility boundary

The chat feature will later start a pinned `llama-cli.exe` directly and read/write redirected streams. It will not use this inspection worker.

Before chat integration:

- record exact `llama-cli` tag/commit/build identity;
- compare it with the LLamaSharp mapped llama.cpp commit;
- align revisions where practical; otherwise perform a formal compatibility campaign;
- record the decision in an ADR;
- ensure a model is not approved by one runtime and silently executed by an incompatible runtime;
- add separate packaging, cancellation, streaming, privacy, and no-port tests.

Current research identity remains distinct:

```text
llama.cpp tag:    b9870
commit:           2d973636e292ee6f75fadcf08d29cb33511f509f
```

No compatibility claim is made in this specification.

---

## 22. Test architecture

Tests are separated by what they prove.

```text
tests/
├── ContractTests/
│   └── GraniteEdgeAI.ModelInspection.Contracts.Tests/
├── UnitTests/
│   ├── GraniteEdgeAI.UnitTests/
│   └── GraniteEdgeAI.ModelInspection.Worker.Tests/
├── IntegrationTests/
│   └── GraniteEdgeAI.ModelInspection.WorkerIntegrationTests/
└── ProcessFixtures/
    └── GraniteEdgeAI.ModelInspection.ProtocolTestWorker/
```

The production worker contains no hidden test commands such as `--crash`, `--hang`, or `--send-invalid-json`. A separate test-only worker simulates hostile and abnormal process behaviour.

### 22.1 Contract tests

- JSON round-trip for every command/message;
- exact protocol version;
- enum string representation;
- required-field validation;
- unknown-field tolerance;
- unknown-kind rejection;
- maximum line size;
- canonical GUID request IDs;
- nullable unavailable evidence;
- no WinUI or LLamaSharp type in the contract graph;
- no model-path output property;
- no full chat-template property;
- runtime profile stability.

### 22.2 Worker unit tests

Using a fake `IWorkerInspectionEngine`:

- hello exactly once;
- hello before request;
- one start accepted;
- second start rejected;
- valid progress ordering;
- exactly one terminal result;
- completed evidence;
- cancelled completion;
- operational failure;
- malformed command;
- oversized input;
- wrong request ID;
- idempotent cancellation;
- stdout contains protocol JSON only;
- stderr path redaction;
- exit-code/result consistency;
- stdin EOF cancellation;
- parent-process disappearance cancellation.

### 22.3 Adapter process tests

Using the test-only protocol worker:

- worker missing;
- worker start failure;
- hello timeout;
- malformed hello;
- unsupported protocol;
- wrong runtime profile;
- wrong architecture;
- invalid JSON;
- oversized stdout line;
- wrong request ID;
- out-of-order progress;
- backward progress;
- duplicate terminal result;
- exit before terminal result;
- crash;
- hang/overall timeout;
- graceful cancellation;
- forced process-tree cancellation;
- stderr truncation;
- result/exit-code mismatch;
- proper stream/resource disposal.

### 22.4 Runtime-library deterministic tests

Migrate and preserve the existing verified contracts for:

- package/version policy;
- CPU-only configuration;
- path validation;
- hashing and integrity;
- cancellation;
- metadata projection;
- tokenizer smoke;
- chat-template hashing;
- progress normalisation;
- exception mapping;
- sensitive-text redaction;
- result precedence;
- evidence-type isolation.

No existing passing coverage may be silently dropped during extraction from the feasibility tool.

### 22.5 Production worker integration tests

Using the real production worker:

- exact Granite success;
- three-run repeatability;
- post-preflight cancellation;
- native-load cancellation;
- every committed malformed GGUF fixture;
- deterministic random bytes;
- missing model;
- directory supplied as model;
- locked model;
- file continuity mismatch;
- model SHA-256 unchanged;
- canonical path absent from stdout/stderr/evidence;
- full chat template absent;
- no process-owned TCP listener/established connection;
- worker/native crash contained outside the test host;
- native resources disposed;
- x64 runtime identity exact.

### 22.6 Classifier tests

- Ready;
- ReadyWithWarnings;
- ConversionRequired with verified route;
- conversion candidate without verified route → Unsupported;
- IncompletePackage;
- Unsupported;
- Invalid;
- Invalid outranks warning;
- Incomplete outranks conversion;
- Unsupported outranks warning;
- only Ready outcomes permit Hardware Fit;
- operational failure never reaches classifier;
- cancellation never reaches classifier.

### 22.7 Service tests

Using fake `ILlamaModelProbe`:

- request validation;
- progress mapping;
- completed evidence classification;
- cancellation propagation;
- operational-failure separation;
- immutable result construction;
- classifier called exactly once when applicable;
- classifier not called otherwise;
- stale-run suppression;
- no XAML dependency.

### 22.8 ViewModel tests

Using fake `IModelInspectionService`:

- valid navigation initialisation;
- automatic inspection start;
- five-stage progress;
- only one active spinner;
- genuine nullable fraction;
- Cancel enabled only while active;
- Cancel command;
- cooperative/forced cancellation presentation;
- all six completed outcomes;
- operational failure;
- retry;
- stale progress/result ignored;
- Hardware Fit continuation only for Ready outcomes;
- page state does not change from a superseded run.

### 22.9 WinUI tests

- request navigation parameter;
- initial model card uses quick-scan snapshot;
- progress control updates;
- Cancel command wiring;
- outcome/content/action cards use ViewModel state;
- disabled actions are not presented as available;
- keyboard focus and automation names;
- layout remains stable across progress and results;
- large text scaling and high contrast remain readable.

### 22.10 Packaging tests

- x64 worker project publishes;
- application x64 publish includes worker closure;
- x64 MSIX contains approved worker files;
- MSIX contains no model or test fixture;
- WinUI project has no LLamaSharp package reference;
- worker package contains CPU dependencies only;
- worker path resolver locates local build and installed package layouts;
- non-x64 process returns explicit unsupported-architecture operation result.

---

## 23. Test-driven implementation rule

Every behaviour change follows:

```text
1. Add one focused failing test.
2. Run it and preserve the expected failure.
3. Add the smallest production implementation.
4. Run the focused test until green.
5. Run the affected test project.
6. Run broader application/runtime gates.
7. Update README and evidence only after executable verification.
```

Tests must fail for the intended reason before production code is added. Existing source-presence tests cannot substitute for behavioural tests where executable verification is practical.

---

## 24. Incremental delivery gates

### Gate 1 — contracts, protocol, and architecture record

Deliver:

- stacked branch and draft PR;
- shared contract project;
- application request/result contracts;
- protocol state machine contract;
- contract tests;
- `ADR-003` worker-process decision;
- README hierarchy for new roots.

Acceptance:

- pure contracts compile without WinUI/LLamaSharp;
- contract tests pass;
- old path-only navigation is not yet removed unless the complete request mapping is implemented in the same green task;
- no worker/native code is introduced before protocol tests define it.

### Gate 2 — worker host and test-only process fixture

Deliver:

- production worker shell with fake engine seam;
- hello/start/progress/cancel/completion protocol;
- test-only abnormal worker;
- worker and adapter process tests;
- timeout/cancellation/orphan cleanup.

Acceptance:

- no LLamaSharp needed for worker protocol tests;
- native crash/hang simulations cannot terminate the test host;
- stdout/stderr rules pass;
- process resources close on every path.

### Gate 3 — extracted LLamaSharp runtime engine

Deliver:

- runtime library extracted from proven feasibility source;
- engineering spike updated to consume the shared runtime library;
- production worker connected to the runtime engine;
- existing deterministic/native/trusted tests migrated without lost coverage.

Acceptance:

- exact Granite passes through the production worker;
- cancellation passes;
- malformed cases remain contained;
- model SHA remains unchanged;
- no port/path leak;
- no duplicated independent LLamaSharp implementation remains.

### Gate 4 — packaging boundary

Deliver:

- x64 worker inclusion in application output and MSIX;
- controlled path resolver;
- package-content tests;
- explicit non-x64 architecture handling.

Acceptance:

- build, publish, and MSIX contain complete approved worker closure;
- installed/build worker starts and handshakes;
- no model/test/evidence leakage into package;
- WinUI project remains free of LLamaSharp.

### Gate 5 — classifier and application service

Deliver:

- deterministic classifier;
- service orchestration;
- execution/result distinction;
- progress mapping;
- service and classifier tests.

Acceptance:

- all outcomes and precedence verified;
- operational/cancelled routes bypass classifier;
- only Ready outcomes permit Hardware Fit continuation.

### Gate 6 — ViewModel and WinUI integration

Deliver:

- complete request handoff from Model Import;
- ModelInspectionViewModel;
- functional progress and Cancel;
- real result/finding/action presentations;
- retry and stale-run protection;
- UI/accessibility tests.

Acceptance:

- end-to-end production-worker inspection updates the existing page;
- native worker failure does not close the app;
- Cancel works;
- all user-visible states are honest and recoverable;
- full application build and packaged tests pass.

No gate begins until the preceding gate has executable evidence and no unresolved blocker.

---

## 25. README and evidence policy

Every new responsibility folder receives a README containing:

- purpose;
- owned responsibilities;
- forbidden responsibilities;
- dependencies;
- inputs and outputs;
- lifecycle;
- error/cancellation behaviour;
- security/privacy boundary;
- tests;
- verified status;
- deferred work;
- related ADR/spec/evidence.

Required hierarchy:

```text
shared/README.md
shared/GraniteEdgeAI.ModelInspection.Contracts/README.md
runtime/README.md
runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/README.md
workers/README.md
workers/GraniteEdgeAI.ModelInspection.Worker/README.md
Features/ModelInspection/README.md
Features/ModelInspection/Contracts/README.md
Features/ModelInspection/Runtime/README.md
Features/ModelInspection/Classification/README.md
Features/ModelInspection/Services/README.md
Features/ModelInspection/ViewModels/README.md
```

Evidence records go under:

```text
docs/testing/evidence/
```

A README or PR must not say `passed`, `implemented`, `supported`, or `packaged` until fresh executable evidence supports that exact claim.

Each pull request description must include:

- purpose and architecture boundary;
- exact files/components changed;
- why the change was selected;
- test-first evidence;
- current build/test results;
- security/privacy controls;
- known limitations;
- non-claims;
- review focus;
- next gate.

---

## 26. Branch and pull-request strategy

```text
feature/model-inspection
    verified feasibility/presentation foundation
        ↓ stacked branch
feature/model-inspection-runtime-integration
    production worker/application integration
```

The new draft PR targets `feature/model-inspection` while PR #44 is open.

After PR #44 merges:

- update the stacked branch from `main` without rewriting reviewed evidence unnecessarily;
- retarget the new PR to `main`;
- review the resulting diff;
- rerun all affected CI gates;
- preserve the design/spec commit history.

The implementation PR remains draft until its current gate has complete evidence. Later gates may remain in the same stacked PR only while the diff remains reviewable; otherwise a further stacked PR is preferred over an unreviewable monolith.

---

## 27. Acceptance criteria for the complete worker integration

The complete slice is accepted only when:

```text
[ ] Shared contracts compile with no WinUI or LLamaSharp dependencies.
[ ] Protocol version, limits, and state machine tests pass.
[ ] Worker unit tests pass.
[ ] Adapter process tests pass.
[ ] Production worker real-model integration tests pass.
[ ] Exact controlled Granite passes through the production worker.
[ ] Three-run repeatability passes.
[ ] Cooperative cancellation passes.
[ ] Forced cancellation terminates the process tree and leaves WinUI/test host alive.
[ ] Worker/native crash remains outside the WinUI/test host.
[ ] Original model SHA-256 remains unchanged.
[ ] No TCP endpoint is observed.
[ ] No canonical model path leaks through stdout, stderr, application evidence, or retained logs.
[ ] No complete chat-template text crosses the worker boundary.
[ ] x64 build output contains the worker closure.
[ ] x64 publish output contains the worker closure.
[ ] x64 MSIX contains the worker closure.
[ ] MSIX contains no model, test fixture, or test evidence.
[ ] Non-x64 execution is explicitly rejected without attempting to start the x64 worker.
[ ] WinUI application project has no LLamaSharp/TurboQuant dependency.
[ ] Classifier outcome and precedence tests pass.
[ ] Service cancellation/operational/stale-run tests pass.
[ ] ViewModel and WinUI progress/cancel/outcome tests pass.
[ ] Full application build and packaged test workflow pass.
[ ] Source-adjacent READMEs are current.
[ ] ADR-003 is accepted and agrees with code.
[ ] Verification evidence is recorded.
[ ] Whole-slice static and executable review finds no unresolved blocker.
```

---

## 28. Risks and trade-offs

### 28.1 Additional packaging complexity

The worker adds executable and native dependencies to the package. Mitigation: explicit package-content tests and x64-only support until other architectures are independently verified.

### 28.2 Protocol maintenance

The application and worker must evolve compatibly. Mitigation: versioned envelopes, additive-field policy, strict state-machine tests, and runtime-profile handshake.

### 28.3 Cancellation may require forced termination

Native code may not honour cancellation immediately. Mitigation: cooperative request first, bounded grace period second, full process-tree kill last, and explicit diagnostic recording.

### 28.4 Parent application may terminate

A child worker could otherwise become orphaned. Mitigation: stdin EOF cancellation and parent-process monitoring; consider a Windows Job Object as later hardening.

### 28.5 Runtime mismatch with chat

LLamaSharp and `llama-cli.exe` may map to different llama.cpp commits. Mitigation: no chat compatibility claim until alignment or a formal compatibility campaign is complete.

### 28.6 Large branch history

The base branch is already substantial. Mitigation: stacked PR, gate-based commits, detailed PR context, and further decomposition if reviewability degrades.

---

## 29. Deferred hardening

Explicitly deferred, not forgotten:

- Windows Job Object kill-on-close;
- filesystem-link alias/hard-link protection beyond current canonical path checks;
- x86 and ARM64 workers;
- signed binary hash attestation inside the application;
- named-pipe transport;
- persistent worker pooling;
- physically disconnected-network acceptance test;
- crash dump capture with privacy controls;
- full CPU allocation/generation;
- Vulkan/TurboQuant/OpenVINO;
- `llama-cli.exe` chat integration.

Each deferred item requires a separate risk/evidence route before it can become a claim.

---

## 30. Engineering basis

### Fundamentals of Software Architecture

Applied guidance:

- modularity, cohesion, and coupling;
- stable interfaces around volatile native infrastructure;
- component-based decomposition;
- trade-off analysis rather than one universally “best” architecture;
- ADRs for consequential architecture decisions.

Relevant themes: Chapters 3, 8, and 21.

### Code Complete

Applied guidance:

- consistent abstraction levels;
- information hiding;
- cohesive classes and routines;
- defensive programming at external boundaries;
- developer testing;
- incremental integration rather than big-bang integration.

Relevant themes: Chapters 5, 6, 8, 22, and 29.

### Designing Secure Software

Applied guidance:

- explicit trust boundaries;
- least exposure and attack-surface minimisation;
- no unnecessary listening service;
- bounded untrusted input;
- fail-secure protocol handling;
- sensitive-data minimisation;
- secure interface design;
- security-focused testing.

### The Art of Unit Testing

Applied guidance:

- distinguish unit tests from integration/process tests;
- use interfaces, fakes, seams, and test doubles;
- keep native and process dependencies out of ordinary application unit tests;
- make tests deterministic and focused on one behavioural contract.

### Why Programs Fail

Applied guidance:

- preserve the first observable failure at the process boundary;
- separate symptom from cause;
- capture stdout, stderr, exit code, timing, and protocol state;
- reproduce failures with controlled process fixtures;
- avoid attributing infrastructure failure to the model.

### Refactoring

Applied guidance:

- extract the proven LLamaSharp implementation into one shared runtime library;
- avoid duplicate production and feasibility implementations;
- move behaviour in small verified steps;
- preserve tests while changing structure.

### Windows application guidance

The project’s `windows-apps.pdf` and current Microsoft documentation support:

- WinUI through the Windows App SDK;
- MVVM/data-binding/test separation;
- packaged WinUI deployment and MSIX verification;
- explicit runtime and process design covering safe command construction, standard streams, exit codes, timeouts, cancellation, and cleanup;
- using application packaging and installed-location APIs rather than searching arbitrary executable locations.

---

## 31. Primary external references

Official Microsoft sources used to verify platform assumptions:

- `ProcessStartInfo.RedirectStandardInput`  
  <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardinput>
- `ProcessStartInfo.RedirectStandardOutput`  
  <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput>
- `ProcessStartInfo.UseShellExecute`  
  <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute>
- `Process.Kill(Boolean)`  
  <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill>
- `Process.WaitForExitAsync`  
  <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexitasync>
- Windows App SDK packaged-app deployment  
  <https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-packaged-apps>
- Windows application package/deployment overview  
  <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/>
- `Package.InstalledLocation`  
  <https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.package.installedlocation>

The implementation must re-check current official documentation when packaging or API details are coded.

---

## 32. Final decision summary

Approved decisions:

```text
Inspection runtime:
    dedicated short-lived worker executable

Native inspection API:
    LLamaSharp 0.27.0 / matched CPU llama.cpp runtime

Chat runtime:
    pinned llama-cli.exe in a later separate feature

Network:
    no HTTP server, no listening port, no required internet

Transport:
    bounded compact JSON lines over redirected stdin/stdout

Diagnostics:
    bounded redacted stderr

Crash safety:
    native work outside WinUI process

Model outcome owner:
    application classifier, not worker

Cancellation:
    cooperative request, then bounded process-tree termination

Packaging:
    signed/controlled x64 application package with worker closure

Architecture support:
    x64 only until separately verified

Development method:
    gate-based TDD, incremental integration, source-adjacent documentation,
    fresh evidence before claims
```

No production implementation begins until this written specification is reviewed and approved.