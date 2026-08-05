# Model Inspection Worker Integration — Production Design

**Status:** Approved conversational design; self-reviewed specification awaiting user approval  
**Date:** 2026-08-05  
**Branch:** `feature/model-inspection-runtime-integration`  
**Stacked base branch:** `feature/model-inspection`  
**Base commit:** `c3276d50fe39ff0db8ae679c236a5d02cf21fe14`  
**Scope:** Production GGUF Model Inspection contracts, protected worker process, LLamaSharp runtime boundary, classification, application service, ViewModel integration, packaging, testing, and documentation  
**Related chat decision:** Chat inference will later launch a pinned `llama-cli.exe` directly; it will not use an HTTP server or listening port

---

## 1. Executive summary

The application will inspect GGUF models through a dedicated short-lived local worker executable:

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

The worker performs the technical inspection and returns structured evidence. It does **not** select WinUI controls or decide the final user-facing model outcome. The application classifier converts reliable worker evidence into one of six model outcomes:

1. `Ready`
2. `ReadyWithWarnings`
3. `ConversionRequired`
4. `IncompletePackage`
5. `Unsupported`
6. `Invalid`

`Cancelled` and `OperationalFailure` remain execution states, not model outcomes.

The worker communicates through redirected standard input, standard output, and standard error. It does not open an HTTP server, listen on a TCP port, or require network access. The model path is sent through standard input rather than exposed on the process command line.

Chat remains a separate later route:

```text
GraniteEdgeAI.exe
    ↓
Chat service
    ↓
pinned llama-cli.exe
    ↓
local Granite inference
```

The worker and `llama-cli.exe` must each report an exact runtime identity. Before chat integration, their llama.cpp revisions must either be aligned or explicitly verified as compatible.

---

## 2. Context and verified evidence

The `feature/model-inspection` branch established:

- Model Import → Model Inspection navigation;
- onboarding-stage synchronisation;
- the initial Model Inspection page and reusable presentation controls;
- an isolated LLamaSharp CPU feasibility tool;
- deterministic, contained-native, and trusted-real-model test tiers;
- read-only hashing and model-integrity controls;
- path and chat-template privacy controls;
- native child-process containment for test execution.

Verified evidence:

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

A previous `VocabOnly` experiment also demonstrated that unsafe native access can cause a process-level llama.cpp abort before managed C# exception handling can recover. That evidence changes worker isolation from optional future hardening into a production requirement for Model Inspection.

Related evidence:

- `docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md`
- `docs/testing/evidence/2026-08-05-llamasharp-tier2-local-verification.md`
- `docs/testing/evidence/2026-08-05-model-inspection-branch-review.md`
- `docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md`

---

## 3. Relationship to the earlier design

This specification supersedes only the earlier decisions that deferred worker-process isolation or proposed direct in-process LLamaSharp loading.

Earlier design:

- `docs/superpowers/specs/2026-07-30-model-inspection-design.md`

Preserved:

- Page → ViewModel → service → runtime-probe layering;
- project-owned data contracts;
- deterministic classification;
- lightweight inspection before Hardware Fit;
- operational failures separated from model invalidity;
- only Ready outcomes may continue to Hardware Fit;
- one shared page and reusable presentation controls;
- stale-result protection and cancellable asynchronous work.

Superseded:

- direct `LlamaSharpModelProbe` inside WinUI;
- worker isolation being deferred;
- path-only navigation;
- combined `InvalidOrIncomplete` outcome;
- using the engineering feasibility executable as production infrastructure.

The feasibility tool remains an engineering front end. The worker will use the same extracted runtime implementation but will have its own protocol, lifecycle, packaging, and security boundary.

---

## 4. Goals

The implementation must:

1. replace the path-only handoff with an immutable validated `ModelInspectionRequest`;
2. preserve quick-scan evidence without coupling Model Inspection to Model Import internals;
3. run LLamaSharp/native llama.cpp outside WinUI;
4. use no HTTP, localhost server, listening port, or required internet access;
5. stream truthful progress through a versioned bounded protocol;
6. support cooperative cancellation and bounded forced cleanup;
7. preserve WinUI when the worker/native runtime crashes;
8. return project-owned evidence without native handles or XAML types;
9. classify user-facing outcomes in application code, not in the worker;
10. keep model outcomes, cancellation, and infrastructure failure separate;
11. preserve the selected model byte-for-byte;
12. prevent path, chat-template, model-byte, prompt, health-data, environment, and secret leakage;
13. include the exact worker closure in x64 build, publish, and MSIX outputs;
14. keep LLamaSharp and TurboQuant out of the WinUI project;
15. update source-adjacent READMEs and evidence only after executable verification.

---

## 5. Non-goals

This design does not implement or claim:

- chat UI, `llama-cli` prompting, or token streaming;
- a local web server or port;
- full tensor allocation, context creation, KV cache, or generation;
- TTFT, throughput, RAM, KV memory, or quality benchmarking;
- Vulkan or GPU offload;
- TurboQuant, PolarQuant, QJL, or TurboVec;
- OpenVINO inspection;
- Hardware Fit/LLM Fit integration;
- x86 or ARM64 worker support;
- worker download, auto-update, or user-selected executables;
- named-pipe IPC in protocol version 1;
- Windows App Service activation;
- exported end-user inspection reports.

---

## 6. Alternatives considered

### 6.1 Direct LLamaSharp in WinUI — rejected

A native abort could terminate the complete application. Managed exception handling does not reliably contain process-level native termination.

### 6.2 `llama-cli.exe` for inspection and chat — rejected for inspection

Console logs are not a stable structured inspection API. Parsing revision-specific log wording would make progress, evidence, and classification fragile. `llama-cli.exe` remains the approved later chat route.

### 6.3 One custom worker for inspection and chat — rejected

It would mix short-lived inspection with long-running generation, duplicate `llama-cli` capability, and reduce cohesion.

### 6.4 Windows App Service — rejected for version 1

It adds activation, manifest, lifecycle, and deployment complexity without enough benefit for one short-lived local request.

### 6.5 Named pipe — deferred

A named pipe may help a future long-lived service. One worker per inspection is simpler through redirected streams, and `ILlamaModelProbe` keeps transport replaceable.

---

## 7. Selected architecture

### 7.1 Inspection route

```text
ModelImportPage
    ↓ immutable request
OnboardingShellPage
    ↓
ModelInspectionPage
    ↓ binding/commands
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

Commands travel downward. Progress and results return upward as immutable data.

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

This route is documented now and implemented later.

### 7.3 Project dependency rules

Allowed:

```text
WinUI application
    → shared worker contracts

Worker executable
    → shared worker contracts
    → LLamaSharp runtime library

LLamaSharp runtime library
    → LLamaSharp 0.27.0
    → LLamaSharp.Backend.Cpu 0.27.0

Engineering spike
    → LLamaSharp runtime library
```

Forbidden:

```text
WinUI application
    ✕ LLamaSharp
    ✕ LLamaSharp.Backend.Cpu
    ✕ worker implementation project
    ✕ engineering spike project

Shared contracts
    ✕ WinUI
    ✕ LLamaSharp
    ✕ worker/service implementations
```

---

## 8. Trust boundaries

```text
User-selected GGUF
        │ untrusted file
        ▼
Worker process
        │ volatile native boundary
        ▼
LLamaSharp / llama.cpp

Worker stdout
        │ untrusted bounded protocol
        ▼
WinUI application adapter
```

The worker is a safety container, not a trust elevation. Every worker message is validated.

Rules:

- model opened read-only;
- model path absolute and canonicalisable;
- no evidence-output path accepted by worker;
- no writes beside installed executable;
- no search of `PATH`, current directory, model directory, or Downloads for alternate binaries;
- worker resolved only from controlled build/installed root;
- no native pointer, handle, model bytes, complete chat template, or canonical directory in output;
- stdout parsed as untrusted data;
- stderr continuously drained, bounded for retention, and redacted;
- result/exit-code inconsistency fails operationally.

---

## 9. Project structure

```text
shared/
└── GraniteEdgeAI.ModelInspection.Contracts/
    ├── README.md
    ├── GraniteEdgeAI.ModelInspection.Contracts.csproj
    ├── Protocol/
    └── Evidence/

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
    └── Inspection/

IBM Granite with TurboQuant (Intel)/Features/ModelInspection/
├── README.md
├── ModelInspectionPage.xaml
├── ModelInspectionPage.xaml.cs
├── Contracts/
├── Runtime/
├── Classification/
├── Services/
├── ViewModels/
├── Controls/
├── Models/
└── Presentation/
```

The worker is a plain .NET 8 Windows console executable, not a WinUI application.

Files are split when a class gains more than one responsibility; speculative abstractions are prohibited.

---

## 10. Application request contracts

### 10.1 `ModelInspectionRequest`

Immutable fields:

```text
ModelPath
FileName
ExpectedModelFileIdentity
ValidatedQuickScanSnapshot
```

Invariants:

- `ModelPath` non-empty and absolute;
- `FileName == Path.GetFileName(ModelPath)` using Windows case-insensitive comparison;
- expected length positive;
- expected timestamp UTC;
- expected length equals quick-scan file size;
- format is `GGUF`;
- request created only from a successful current quick scan;
- no page, scanner, XAML, native, or LLamaSharp object retained.

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

The snapshot is copied from the successful internal quick-scan result. The worker treats it as comparison context, not as authoritative native evidence.

### 10.3 Handoff

`ModelInspectionRequestedEventArgs` changes from `string ModelPath` to `ModelInspectionRequest Request`.

```text
ModelImportPage
    → revalidates successful current state
    → captures current length/timestamp
    → creates request
    → raises event

OnboardingShellPage
    → navigates with request

ModelInspectionPage
    → validates parameter type
    → initialises ViewModel
```

The guarded request method must reject stale/missing state independently of button enablement.

---

## 11. Worker protocol

### 11.1 Transport

One compact JSON object per UTF-8 line:

```text
stdin   → application commands
stdout  → protocol messages only
stderr  → redacted diagnostics only
```

Configuration:

```text
UseShellExecute = false
RedirectStandardInput = true
RedirectStandardOutput = true
RedirectStandardError = true
CreateNoWindow = true
```

The model path is never a command-line argument.

The protocol uses UTF-8 without a byte-order mark. Newlines inside strings are JSON-escaped. Both sides use a bounded line reader that stops once the byte limit is exceeded; ordinary unbounded `ReadLineAsync` is not sufficient for untrusted worker output.

Stderr is always drained to prevent pipe backpressure. Retention stops at its limit, but draining continues until process exit.

### 11.2 Constants

```text
Protocol version:                 1
Maximum command/message line:     1 MiB UTF-8
Maximum retained stderr:          256 KiB UTF-8
Maximum JSON depth:               named bounded value
Startup handshake timeout:        5 seconds
Overall inspection timeout:       5 minutes
Graceful cancellation timeout:    5 seconds
Requests per worker:              1
Terminal results per request:     1
```

All are named constants and directly tested.

### 11.3 JSON validation

Within protocol version 1:

- unknown additional properties tolerated;
- duplicate property names rejected at every protocol object level;
- missing required properties rejected;
- unknown command/message kinds rejected;
- unknown enum values rejected;
- invalid UTF-8 rejected;
- trailing non-whitespace after the JSON object rejected;
- breaking changes require protocol version 2;
- no automatic protocol downgrade.

### 11.4 Hello handshake

Worker output:

```json
{
  "protocolVersion": 1,
  "messageType": "hello",
  "workerId": "granite-edge-ai-model-inspection",
  "workerVersion": "1.0.0",
  "workerProcessId": 5678,
  "runtimeProfile": "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
  "processArchitecture": "X64"
}
```

Hello is connection-scoped and has no request ID because no request exists yet.

The adapter verifies:

- protocol version;
- exact worker ID;
- worker version policy;
- `workerProcessId == Process.Id`;
- exact runtime profile;
- x64 architecture;
- timeout;
- no preceding stdout text.

Failure is operational.

### 11.5 Start command

```json
{
  "protocolVersion": 1,
  "commandType": "startInspection",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534",
  "parentProcessId": 1234,
  "parentProcessStartTimeUtc": "2026-08-05T11:00:00.0000000Z",
  "modelPath": "C:\\Models\\granite.gguf",
  "expectedFileIdentity": {
    "lengthBytes": 2099501664,
    "lastWriteTimeUtc": "2026-08-05T00:00:00.0000000Z"
  },
  "quickScan": {
    "format": "GGUF",
    "architecture": "granite",
    "modelName": "Granite 4.1 3B",
    "parameterSizeLabel": "3B",
    "quantisation": "Q4_K_M",
    "fileSizeBytes": 2099501664,
    "declaredContextLength": 131072,
    "ggufVersion": 3
  }
}
```

Rules:

- request ID canonical GUID;
- parent PID positive and start time UTC;
- worker verifies parent PID and start time together to reduce PID-reuse ambiguity;
- path absolute/canonicalised before access;
- one start only;
- malformed/duplicate start → protocol failure, exit `2`;
- model path never echoed.

### 11.6 Started message

```json
{
  "protocolVersion": 1,
  "messageType": "started",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534"
}
```

### 11.7 Progress

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

Stable stages:

1. `CheckModelPackage`
2. `ReadModelConfiguration`
3. `ValidateTokenizerAndChatSetup`
4. `ValidateModelStructure`
5. `ConfirmCoreRuntimeCompatibility`

Rules:

- stage never moves backward;
- completed count never decreases and remains `0..5`;
- fraction nullable;
- only genuine fractions emitted;
- finite fraction constrained to `0..1`;
- no NaN/infinity;
- consecutive duplicates may be coalesced.

### 11.8 Cancel command

```json
{
  "protocolVersion": 1,
  "commandType": "cancelInspection",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534"
}
```

Rules:

- valid only after start and before terminal result;
- duplicate cancel idempotent;
- wrong request ID is protocol failure;
- worker cancels linked token;
- worker attempts disposal and final integrity verification;
- cooperative completion emits one Cancelled terminal result and exit `3`.

### 11.9 Terminal message

```json
{
  "protocolVersion": 1,
  "messageType": "completed",
  "requestId": "37b89687-2da7-4daf-bd75-a3c16c235534",
  "completionStatus": "Completed",
  "evidence": {},
  "cancellation": null,
  "operationalFailure": null
}
```

Statuses:

- `Completed`: diagnostic workflow completed with reliable evidence. The model may still classify as any of the six outcomes.
- `Cancelled`: cooperative cancellation completed, native resources were disposed, and integrity was verified.
- `OperationalFailure`: reliable classification evidence could not be produced.

Terminal invariants:

```text
Completed
    evidence required
    cancellation null
    operationalFailure null

Cancelled
    evidence null
    cancellation required
    operationalFailure null

OperationalFailure
    evidence null
    cancellation null
    operationalFailure required
```

Runtime identity in Completed evidence must match hello identity. A mismatch is operational failure.

### 11.10 Exit codes

```text
0 = Completed
1 = OperationalFailure
2 = invalid protocol/command/state
3 = cooperative Cancelled
```

The adapter cross-checks result and exit code.

### 11.11 Forced termination rule

Only a cooperative terminal result may become application status `Cancelled`.

If cancellation grace expires:

```text
application kills worker process tree
    ↓
terminal result/integrity proof unavailable
    ↓
OperationalFailure
MI-OP-WORKER-CANCELLATION-TIMEOUT
```

The UI may explain that the inspection could not stop cleanly, but it must not classify the model or claim verified cancellation. This fail-closed rule resolves the conflict between user intent and unavailable integrity evidence.

---

## 12. Protocol state machine

```text
Created
    ↓ hello
HelloSent
    ↓ start
InspectionStarted
    ├── zero or more progress messages
    ├── optional cancel command
    ├── stdin EOF / parent disappearance requests cancellation
    ↓
TerminalMessageSent
    ↓
ProcessExit
```

Invalid:

- output before/two hello messages;
- progress before start;
- wrong request ID;
- backward progress;
- duplicate terminal result;
- message after terminal result;
- start after cancel/completion;
- more than one request;
- normal exit without terminal result.

A process-level crash may exit without terminal JSON; the adapter records it as operational failure.

---

## 13. Lifecycle and cleanup

### Startup

1. Resolve fixed worker path.
2. Reject non-x64 process before launch.
3. Confirm file exists and is contained under controlled root.
4. Start without shell.
5. Start bounded asynchronous stdout and continuously drained stderr readers immediately.
6. Validate hello within five seconds.
7. Send start command.

### Normal completion

1. Validate progress/terminal sequence.
2. Wait for process exit.
3. Cross-check exit code.
4. Await both stream readers.
5. Dispose process resources.
6. Map evidence.
7. Call classifier only for Completed status.

### Cooperative cancellation

1. Send cancel once.
2. Wait five seconds.
3. Accept Cancelled only with valid terminal result, matching exit `3`, and verified integrity.
4. Dispose resources.

### Forced cancellation

1. Kill complete process tree after grace expiry.
2. Continue draining streams until closure.
3. Return OperationalFailure, not Cancelled.
4. Never classify the model.

### Parent disappearance

Worker requests cancellation when:

- stdin reaches EOF before normal completion; or
- parent PID no longer exists; or
- parent start time no longer matches.

A Windows Job Object is deferred hardening.

### Overall timeout

Five minutes from process start. On timeout:

- request cancellation when possible;
- wait grace period;
- kill process tree;
- return `MI-OP-WORKER-TIMEOUT`;
- do not classify.

---

## 14. Worker evidence

### Runtime identity

```text
WorkerId
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

First profile:

```text
LLamaSharp:          0.27.0
CPU backend:         0.27.0
Mapped llama.cpp:    3f7c29d318e317b63f54c558bc69803963d7d88c
RID:                 win-x64
Mode:                VocabOnly
CUDA/Vulkan:         false/false
GPU layers:          0
```

### File evidence

```text
FileName
CanonicalPathFingerprint
LengthBefore/After
LastWriteTimeBefore/After
Sha256Before/After
IntegrityPreserved
```

No full path returned.

### Configuration/structure

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

Unavailable values remain null and are never guessed.

### Tokenizer

```text
VocabularyCount
VocabularyType
TokenizerSmokePassed
TokenizerSmokeTokenCount
KnownSpecialTokenIds
```

### Chat template

```text
Present
LengthCharacters
Sha256
```

No complete template text.

### Technical observations

```text
Code
Domain
Impact
TechnicalDetail
```

The application maintains an explicit registry of supported observation codes. Any unknown observation from the exact worker profile causes `MI-OP-UNRECOGNISED-WORKER-OBSERVATION`; it is not silently ignored or guessed.

User-facing wording and conversion-route selection remain application responsibilities.

---

## 15. File continuity, integrity, and evidence conflicts

### Request continuity

The request carries length/timestamp from the current validated selection.

### Worker preflight

Worker:

- canonicalises path;
- confirms normal file;
- opens read-only;
- compares requested length/timestamp;
- computes SHA-256;
- records snapshot.

A continuity mismatch means the selected file changed after quick scan and returns operational failure, not a model outcome.

### Postflight

After native disposal, worker captures a second snapshot and compares path fingerprint, length, timestamp, and SHA-256.

Changed or unverifiable integrity outranks Completed or Cancelled and becomes OperationalFailure.

### Cross-source evidence consistency

Before classification, the application service compares required quick-scan and worker evidence:

- format;
- file length;
- architecture where both are available;
- GGUF version where both are available.

A required conflict returns `MI-OP-EVIDENCE-CONFLICT` and bypasses classification because the two trusted stages disagree.

Optional differences such as filename/display-name wording may become warnings through explicit rules.

---

## 16. Execution and outcome model

### Execution status

```text
Completed
Cancelled
OperationalFailure
```

- Completed requires evidence and one classified result.
- Cancelled requires cooperative worker cancellation and verified integrity.
- OperationalFailure has no model outcome.
- Classifier is called only for Completed.

### Model outcomes and precedence

```text
1. Invalid
2. IncompletePackage
3. Unsupported
4. ConversionRequired
5. ReadyWithWarnings
6. Ready
```

### Invalid

Only reliable evidence that the model itself is malformed/corrupt: invalid header, truncated metadata, impossible encoding, corrupt required tokenizer data, or inconsistent required structure.

A native crash while examining malformed input remains OperationalFailure unless independent reliable evidence already establishes invalidity.

### IncompletePackage

Recognisable package with a required component missing, such as split shard or required vocabulary component. Missing optional chat template alone is not incomplete.

### Unsupported

Readable model requiring architecture, tokenizer, or GGUF feature unsupported by the pinned runtime.

### ConversionRequired

Only when the application’s versioned conversion-route registry contains a tested route. Worker never chooses the route. Without a verified route, return Unsupported.

### ReadyWithWarnings

All blocking checks pass, but explicit non-blocking findings exist: missing configurable chat template, optional metadata absent, context review needed, or harmless display-name mismatch.

### Ready

Required package/runtime/tokenizer/structure checks pass, integrity is preserved, and no warning/blocking finding exists.

### Hardware Fit continuation

Only Ready and ReadyWithWarnings are eligible.

---

## 17. Service and ViewModel responsibilities

### `ModelInspectionService`

- validate request;
- call probe;
- map progress;
- map worker evidence;
- enforce evidence-consistency checks;
- bypass classifier for cancellation/failure;
- classify Completed evidence;
- return immutable result;
- preserve run identity.

It does not reference XAML, navigate, parse raw stdout, or construct LLamaSharp objects.

### `ModelInspectionViewModel`

Owns observable page state, stage presentations, nullable genuine fraction, result/findings, and action availability.

Commands:

- automatic start after valid page initialisation;
- cancel;
- retry;
- choose another model;
- show details;
- continue to Hardware Fit when eligible.

Stale-run protection mirrors Model Import: publish replacement run identity before cancelling the old run; all callbacks verify current identity.

The Cancel button becomes enabled only during a real active run. Forced cancellation timeout is displayed as operational failure, not successful cancellation.

---

## 18. Security and privacy

### Launch

- absolute fixed executable path;
- path canonicalised and proven contained under build/installed root using a separator-qualified root check;
- no shell;
- no command-line model path;
- no arbitrary executable;
- working directory fixed to worker directory;
- async bounded stream handling;
- process-tree termination on timeout.

### Protocol

- bounded byte-aware line reader;
- bounded JSON depth;
- duplicate-property rejection;
- required-field validation;
- exact request ID;
- strict state machine;
- unknown kind/code fails closed;
- unknown additive fields tolerated.

### Sensitive data

Never retain/return:

- canonical model directory;
- complete chat template;
- model bytes;
- prompt or health information;
- environment variables;
- native handles/pointers;
- raw unbounded native logs;
- raw protocol lines after successful parsing.

Allowed:

- filename;
- path fingerprint;
- model SHA-256;
- approved metadata;
- bounded redacted diagnostics.

### Network

No HTTP endpoint, TCP listener, download, or required internet. Verify exact process PID with TCP observation tests.

---

## 19. Packaging and architecture support

### Worker closure

The x64 package must include a fixed worker subfolder containing:

```text
GraniteEdgeAI.ModelInspection.Worker.exe
GraniteEdgeAI.ModelInspection.Contracts.dll
GraniteEdgeAI.ModelInspection.LlamaSharp.dll
LLamaSharp.dll
approved LLamaSharp CPU backend/native dependencies
runtime configuration files
```

### Path resolution

Resolver order:

1. packaged run: `Package.Current.InstalledLocation.Path`;
2. controlled local build/test: `AppContext.BaseDirectory`;
3. append fixed worker subdirectory and filename;
4. canonicalise root and candidate;
5. require separator-qualified candidate containment;
6. never search `PATH` or working directory.

Installed location is treated as read-only. Worker streams evidence; it does not write there.

### x64-only first boundary

Only win-x64 is verified.

- worker built/packaged for x64 only;
- adapter rejects non-x64 before process start with `MI-OP-WORKER-ARCHITECTURE-UNSUPPORTED`;
- x86/ARM64 builds do not silently include/launch x64 worker;
- UI gives a plain operational explanation;
- no non-x64 support claim until separate native/package verification.

### Package verification

Inspect worker build, application publish, and x64 MSIX for:

- complete approved worker closure;
- exact CPU native dependency;
- absence of CUDA/Vulkan worker binaries;
- absence of GGUF, test fixtures, and evidence;
- runtime-profile/package-policy consistency.

---

## 20. Chat compatibility boundary

Chat later launches pinned `llama-cli.exe` directly through a separate process adapter, with no port.

Before integration:

- record exact CLI tag/commit/build;
- compare with LLamaSharp mapped commit;
- align or run formal compatibility campaign;
- record ADR;
- add packaging, streaming, cancellation, privacy, and no-port tests.

Current research identity remains separate:

```text
llama.cpp tag: b9870
commit:        2d973636e292ee6f75fadcf08d29cb33511f509f
```

No compatibility claim is made here.

---

## 21. Test architecture

```text
tests/
├── ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/
├── UnitTests/GraniteEdgeAI.UnitTests/
├── UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/
├── IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerIntegrationTests/
└── ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/
```

The production worker contains no hidden crash/hang/test modes. A test-only worker simulates abnormal processes.

### Contract tests

Round-trip, version, enum strings, required fields, unknown-field tolerance, duplicate rejection, unknown-kind rejection, size/depth/UTF-8 limits, GUIDs, nullable evidence, no WinUI/LLamaSharp graph, no path/template property, runtime-profile stability.

### Worker unit tests

Hello once/before request; one start; progress order; one terminal result; all statuses; malformed/oversized input; wrong ID; idempotent cancel; stdout JSON only; stderr redaction; exit consistency; EOF and parent-disappearance cancellation.

### Adapter process tests

Missing/start failure; hello timeout/malformed/version/profile/architecture; invalid/duplicate/oversized JSON; wrong ID; bad progress; duplicate/no terminal; crash; hang; overall timeout; cooperative cancel; cancellation-timeout forced kill → OperationalFailure; stderr truncation while continuous draining; exit mismatch; resource disposal.

### Runtime deterministic tests

Preserve all existing package-policy, CPU configuration, path/hash/integrity, cancellation, metadata, tokenizer, template, progress, failure, redaction, precedence, and type-isolation coverage. No coverage silently dropped during extraction.

### Production worker integration

Exact Granite success; three runs; both cancellation scopes; malformed fixtures; random bytes; missing/directory/locked model; continuity mismatch; SHA unchanged; no path/template leak; no TCP endpoint; native resources disposed; x64 identity exact.

Real native termination is recorded and contained when a controlled fixture naturally produces it. The implementation must not introduce a production backdoor solely to manufacture a native crash. Generic crash containment is proved deterministically with the test-only worker.

### Classifier

All six outcomes, precedence, conversion registry requirement, only Ready continuation, unknown observation → OperationalFailure, cancellation/failure bypass.

### Service

Request validation, progress/evidence mapping, required conflict detection, classifier invocation rules, cancellation, immutable result, stale suppression, no XAML.

### ViewModel/WinUI

Valid request, automatic start, five stages, one spinner, nullable fraction, Cancel only while active, cooperative cancellation, forced-timeout operational state, all outcomes, retry, stale suppression, continuation policy, card/action binding, keyboard, automation, scaling, high contrast, stable layout.

### Packaging

x64 worker publish; application publish/MSIX closure; no model/fixtures/evidence; no LLamaSharp in WinUI project; CPU only; installed/local resolver; non-x64 explicit rejection.

---

## 22. Test-driven implementation rule

```text
1. Add focused failing test.
2. Run and preserve intended failure.
3. Add smallest production code.
4. Run focused test green.
5. Run affected project.
6. Run broader gates.
7. Update README/evidence only after verification.
```

Source-presence tests do not substitute for behavioural tests where execution is practical.

---

## 23. Incremental gates

### Gate 1 — contracts, protocol, ADR

Shared contract project, application contracts, state-machine contract, contract tests, `ADR-003`, root/source READMEs, stacked draft PR.

Do not add worker/native code before protocol tests define it.

### Gate 2 — worker host and test process

Worker shell with fake engine seam, protocol, abnormal test worker, adapter process tests, timeout/cancellation/orphan cleanup. No LLamaSharp required yet.

### Gate 3 — extracted LLamaSharp engine

Extract proven source into one runtime library; update spike to consume it; connect worker; migrate all coverage. Exact Granite/cancellation/malformed/privacy/integrity/no-port gates must pass. No duplicate independent LLamaSharp implementation.

### Gate 4 — packaging

x64 worker closure, fixed resolver, MSIX tests, non-x64 handling. Build, publish, and MSIX must pass.

### Gate 5 — classifier and service

All outcomes/precedence, evidence conflicts, execution separation, progress, stale identity.

### Gate 6 — ViewModel and UI

Complete request handoff, real progress, functional cooperative Cancel, forced-timeout operational recovery, outcomes/actions/retry/stale protection, accessibility.

No gate begins until the previous gate has executable evidence and no unresolved blocker.

---

## 24. README and evidence policy

Every new responsibility folder gets a README covering purpose, owned/forbidden responsibility, dependencies, input/output, lifecycle, errors/cancellation, security/privacy, tests, verified status, deferred work, and related ADR/spec/evidence.

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

Evidence: `docs/testing/evidence/`.

No README/PR says passed, implemented, supported, or packaged without fresh exact-scope evidence.

PR descriptions include purpose, boundary, changes, rationale, test-first evidence, results, security/privacy, limitations, non-claims, review focus, and next gate.

---

## 25. Branch and PR strategy

```text
feature/model-inspection
    ↓ stacked
feature/model-inspection-runtime-integration
```

New draft PR targets `feature/model-inspection` while PR #44 is open. After #44 merges, update from `main`, retarget, review diff, rerun gates, and preserve design history.

Further stacked PRs are preferred if the integration diff stops being reviewable.

---

## 26. Complete acceptance criteria

```text
[ ] Pure contracts compile without WinUI/LLamaSharp.
[ ] Protocol/version/limit/state tests pass.
[ ] Worker unit tests pass.
[ ] Adapter process tests pass.
[ ] Exact Granite passes through production worker.
[ ] Three-run repeatability passes.
[ ] Cooperative cancellation passes with verified integrity.
[ ] Cancellation timeout kills tree and returns OperationalFailure.
[ ] Simulated crash remains outside WinUI/test host.
[ ] Any naturally observed native abort is contained.
[ ] Original model SHA remains unchanged.
[ ] No TCP endpoint observed.
[ ] No canonical path or full template leaks.
[ ] x64 build/publish/MSIX contain worker closure.
[ ] Package contains no model/fixture/evidence.
[ ] Non-x64 rejects before worker start.
[ ] WinUI project has no LLamaSharp/TurboQuant dependency.
[ ] Classifier/service/ViewModel/WinUI tests pass.
[ ] Full application build and packaged workflow pass.
[ ] READMEs, ADR-003, evidence, and code agree.
[ ] Whole-slice review has no unresolved blocker.
```

---

## 27. Risks and deferred hardening

Risks: packaging complexity, protocol maintenance, uncooperative native cancellation, orphan process, chat runtime mismatch, large stacked history.

Mitigations are the x64 package gate, versioned protocol, fail-closed forced termination, EOF/parent monitoring, compatibility ADR, and gated PR decomposition.

Deferred:

- Windows Job Object kill-on-close;
- filesystem-link/hard-link strengthening;
- x86/ARM64 workers;
- runtime binary hash attestation;
- named pipe/persistent worker;
- physically disconnected acceptance;
- privacy-controlled crash dumps;
- full CPU/Vulkan/TurboQuant/OpenVINO;
- `llama-cli` chat integration.

---

## 28. Engineering basis

### Fundamentals of Software Architecture

Modularity, cohesion/coupling, stable component interfaces, trade-off analysis, and ADRs (notably Chapters 3, 8, and 21).

### Code Complete

Consistent abstraction, information hiding, cohesive classes, defensive boundaries, developer testing, and incremental integration (notably Chapters 5, 6, 8, 22, and 29).

### Designing Secure Software

Trust boundaries, least exposure, no unnecessary listening service, bounded untrusted input, fail-secure protocol handling, sensitive-data minimisation, secure interfaces, and security testing.

### The Art of Unit Testing

Separate unit/integration/process tests; use seams, fakes, and test doubles; keep native/process dependencies out of normal application tests.

### Why Programs Fail

Preserve the first failure at the process boundary; separate cause from symptom; retain bounded stdout/stderr/exit/timing/protocol evidence; do not blame the model for infrastructure failure.

### Refactoring

Extract one tested runtime implementation, avoid duplicated feasibility/production logic, move in small verified steps, preserve behaviour while changing structure.

### Windows application guidance

The project’s `windows-apps.pdf` and current Microsoft guidance support WinUI/Windows App SDK, MVVM/test separation, packaged/MSIX deployment, safe process construction, standard-stream handling, exit interpretation, timeouts, cancellation, cleanup, and controlled installed-location resolution.

---

## 29. Primary external references

Official Microsoft sources:

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
- Windows App SDK packaged deployment  
  <https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-packaged-apps>
- Windows package/deployment overview  
  <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/>
- `Package.InstalledLocation`  
  <https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.package.installedlocation>

Implementation must re-check current official documentation when packaging/API details are coded.

---

## 30. Final self-review and adjustments

The specification was reviewed for placeholders, contradictions, ambiguity, scope, testability, security, and unsupported claims.

Adjustments made before this revision:

1. **Handshake identity:** `hello` is connection-scoped and does not require a request ID before a request exists. It now includes stable worker ID and process ID.
2. **Operation versus outcome:** worker `Completed` means the diagnostic workflow completed; it does not mean the model is Ready. Invalid/unsupported models can be reliable completed diagnoses.
3. **Forced cancellation:** forced kill is now OperationalFailure because terminal/integrity proof is unavailable. Only cooperative verified cancellation is Cancelled.
4. **Stream safety:** both sides require byte-bounded line readers; stderr is continuously drained even after retention truncation.
5. **JSON ambiguity:** duplicate properties are rejected; unknown additive fields remain compatible.
6. **Parent monitoring:** parent PID is paired with start time to reduce PID-reuse ambiguity; EOF is also monitored.
7. **Evidence conflicts:** required quick-scan/worker disagreement bypasses classification as operational evidence conflict.
8. **Unknown observations:** no unknown worker code is silently ignored or guessed.
9. **Packaging containment:** fixed installed/build roots use canonical separator-qualified containment; no `PATH` search.
10. **Architecture claim:** first production worker is explicitly x64-only.
11. **Native crash testing:** deterministic crash containment uses a test-only worker; production code receives no hidden crash command.
12. **Installed-folder writes:** production worker streams evidence and treats package installation as read-only.

Placeholder scan: no `TBD`, `TODO`, or unresolved decision remains in the approved first-version scope.

---

## 31. Final decision summary

```text
Inspection:
    dedicated short-lived x64 worker
    LLamaSharp 0.27.0 / matched CPU llama.cpp

Chat later:
    pinned llama-cli.exe

Network:
    no server, no port, no required internet

Transport:
    bounded UTF-8 JSON lines over stdin/stdout
    bounded redacted continuously drained stderr

Outcome ownership:
    application classifier

Cancellation:
    cooperative verified cancellation → Cancelled
    forced termination → OperationalFailure

Packaging:
    controlled x64 build/publish/MSIX worker closure

Method:
    gate-based TDD, incremental integration,
    README/evidence updates only after verification
```

No production implementation begins until this written specification is reviewed and approved.