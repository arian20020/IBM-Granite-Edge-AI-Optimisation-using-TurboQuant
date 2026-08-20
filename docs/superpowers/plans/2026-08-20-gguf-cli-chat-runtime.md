# GGUF CLI Chat Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a runnable Windows CPU MVP in approximately three focused development days: select an inspected local GGUF model, chat through WinUI while a protected internal supervisor drives pinned `llama-cli.exe --simple-io`, stop generation safely, and persist every prompt and response in dated local chat history.

**Architecture:** Add a new G1-owned contracts/transport/capabilities/client/worker stack parallel to the protected Model Inspection stack. WinUI sends strict framed commands over inherited pipes to a long-running protected supervisor. The supervisor starts only a manifest-verified CLI from a fixed package subtree, passes prompts over standard input, converts bounded CLI output into structured events, and owns the CLI in a kill-on-close Job. The chat feature stores versioned atomic per-user conversations and reconstructs the selected transcript after restart. C1 supplies complete approved runtime configurations; G1 validates and executes them without ranking or silent fallback.

**Tech Stack:** .NET 8, C# 12, WinUI 3 / Windows App SDK 2.2, MSTest 4.3.2 / Microsoft Testing Platform, Windows `CreateProcessW` + `STARTUPINFOEX` + Job objects, JSON with explicit framing, PowerShell packaging verification, pinned llama.cpp CPU build.

**Approved design:** `docs/superpowers/specs/2026-08-20-gguf-cli-chat-runtime-design.md`

**Planning baseline:** design inspection on `feature/model-inspection` at `ed97b4b4df5ec3732ffeeafe46819a397211d0e9`; plan authored after the repository moved to `feature/model-import-drag-drop`, with the design committed as `17e97dfd`.

---

## Delivery rules and cut line

- Build the MVP in a dedicated worktree created from the integration baseline approved by I0. Do not implement on a dirty feature branch.
- Do not edit or reuse `GraniteEdgeAI.ModelInspection.*` production projects. Reference their patterns only.
- Do not download a model or runtime during normal build/test. Real-runtime tests consume explicitly staged local artifacts.
- CPU is the only backend required for MVP acceptance.
- Vulkan, SYCL, `llama-quantize`, attachments, and TurboQuant remain disabled and out of the three-day critical path.
- Never put prompts, responses, absolute model paths, environment contents, or raw CLI logs in diagnostics/evidence.
- Never invoke `cmd.exe`, PowerShell, a batch file, a server/listener, or an executable found through `PATH`.
- Each task starts with a failing focused test, adds the smallest implementation, runs the focused test, and commits only its files.
- I0-owned edits are called out explicitly. G1 prepares handoff patches/contracts; I0 performs the final main-project, solution, navigation, and shared-resource integration.

## Fixed project map

| Path | Owner | Responsibility |
| --- | --- | --- |
| `shared/GraniteEdgeAI.GgufRuntime.Contracts` | G1 | Commands, events, IDs, states, limits, configuration, and fixed failures |
| `shared/GraniteEdgeAI.GgufRuntime.Transport` | G1 | Strict length-prefixed UTF-8 JSON framing and schema/version validation |
| `runtime/GraniteEdgeAI.GgufRuntime.Capabilities` | G1 | Trusted runtime manifest and complete-configuration validation |
| `workers/GraniteEdgeAI.GgufRuntime.Worker` | G1 | Protected supervisor and pinned CLI session lifecycle |
| `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient` | G1 | Manifest-verified supervisor launch, pipe conversation, cancellation, cleanup |
| `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime` | G1 | Chat domain, persistence, orchestration, view model, page, and controls |
| `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli` | G1 | Deterministic non-network CLI substitute for lifecycle/parser tests |
| `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.ProtocolTestWorker` | G1 | Malformed/stalled supervisor protocol cases |
| `tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests` | G1 | Contract invariants and privacy shape |
| `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests` | G1 | Framing, UTF-8, bounds, cancellation, backpressure |
| `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests` | G1 | Manifest/configuration trust policy |
| `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests` | G1 | CLI parser and supervisor state machine |
| `tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests` | G1 | Windows launch/environment/cleanup policy |
| `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests` | G1 | Real processes with fake CLI and optional staged real CLI/model |
| `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime` | G1 | Persistence, orchestration, view model, WinUI, accessibility |
| `IBM Granite with TurboQuant (Intel)/GgufRuntime.WorkerPackaging.targets` | I0 integration | Publish verified G1 worker/runtime package into fixed app subtree |
| `IBM Granite with TurboQuant (Intel).slnx` and main app `.csproj` | I0 | Solution/project references, XAML includes, packaging import |

## Day 1 — Trusted runtime vertical slice

### Task 1: Define strict runtime contracts and legal lifecycle

**Files:**
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/GraniteEdgeAI.GgufRuntime.Contracts.csproj`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Protocol/GgufProtocolVersion.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Protocol/GgufProtocolLimits.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Session/GgufSessionId.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Session/GgufSessionState.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Session/GgufSessionStateMachine.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Configuration/GgufRuntimeConfiguration.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Commands/GgufRuntimeCommand.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Events/GgufRuntimeEvent.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Contracts/Failures/GgufRuntimeFailure.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj`
- Create: `tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GgufContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GgufSessionStateMachineTests.cs`

- [ ] **Step 1: Write failing contract-shape and lifecycle tests.**

Test that `GgufRuntimeConfiguration` requires model identity/hash, exact runtime build, CPU backend, context/cache/thread/batch values, and evidence/profile IDs; command/event records require request/session IDs; no public diagnostic or result property contains `Path`, `Prompt`, `Response`, `Environment`, or `RawLog`; and only these transitions are legal:

```csharp
Created -> Starting -> Loading -> Ready
Ready -> Generating -> Ready
Generating -> Stopping -> Ready
any active -> Closing -> Closed
any nonterminal -> Failed -> Closing -> Closed
```

- [ ] **Step 2: Run the new contract project and confirm failure.**

Run:

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj"
```

Expected: FAIL because the contract project/types do not exist.

- [ ] **Step 3: Add minimal immutable records, discriminators, limits, and state validation.**

Use sealed records and closed enums. Define `StartSession`, `SubmitPrompt`, `StopGeneration`, and `CloseSession`; define loading, ready, response-started, text-delta, usage, completed, stopped, failure, and closed events. `StartSession` may carry a bounded ordered initial-turn list so a selected persisted chat can restore model context, but conversation content must remain absent from failures and diagnostics. Keep local paths in an internal launch-only value that never appears in events/failures.

- [ ] **Step 4: Run tests and build with warnings as errors.**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj"
dotnet build "shared/GraniteEdgeAI.GgufRuntime.Contracts/GraniteEdgeAI.GgufRuntime.Contracts.csproj" -c Release
```

Expected: PASS; no analyzer warnings.

- [ ] **Step 5: Commit the contract slice.**

```powershell
git add -- "shared/GraniteEdgeAI.GgufRuntime.Contracts" "tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests"
git commit -m "feat(runtime): define GGUF session contracts"
```

### Task 2: Add bounded framed transport

**Files:**
- Create: `shared/GraniteEdgeAI.GgufRuntime.Transport/GraniteEdgeAI.GgufRuntime.Transport.csproj`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Transport/GgufFrameReader.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Transport/GgufFrameWriter.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Transport/GgufProtocolSerializer.cs`
- Create: `shared/GraniteEdgeAI.GgufRuntime.Transport/GgufTransportException.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GgufFrameReaderTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GgufProtocolSerializerTests.cs`

- [ ] **Step 1: Write failing framing tests.**

Cover round trips, fragmented reads, zero/oversized lengths, early EOF, invalid UTF-8, unknown discriminators, version mismatch, duplicate JSON members, trailing content, cancellation, one-writer serialization, and bounded pending output.

- [ ] **Step 2: Run and confirm failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj"
```

Expected: FAIL because transport is absent.

- [ ] **Step 3: Implement four-byte little-endian length framing and strict UTF-8 JSON.**

Use exact-size reads, `UTF8Encoding(false, true)`, source-generated or explicitly closed serialization metadata, a single bounded writer queue, and cancellation-aware async I/O. Reject rather than ignore unknown protocol fields.

- [ ] **Step 4: Run focused and contract tests.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj"
dotnet test "tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj"
```

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "shared/GraniteEdgeAI.GgufRuntime.Transport" "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests"
git commit -m "feat(runtime): add bounded GGUF pipe transport"
```

### Task 3: Validate the fixed runtime package and complete C1 configuration

**Files:**
- Create: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/GraniteEdgeAI.GgufRuntime.Capabilities.csproj`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/Manifest/GgufRuntimeManifest.cs`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/Manifest/GgufRuntimeManifestVerifier.cs`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/Manifest/VerifiedGgufRuntimePackage.cs`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/Configuration/GgufRuntimeConfigurationValidator.cs`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/Configuration/GgufCapabilityMatrix.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/GgufRuntimeManifestVerifierTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/GgufRuntimeConfigurationValidatorTests.cs`
- Create: `tests/TestFixtures/GgufRuntimePackage/README.md`
- Create: `tests/TestFixtures/GgufRuntimePackage/cpu-fixture-manifest.json`

- [ ] **Step 1: Write failing trust and compatibility tests.**

Cover canonical containment, `..`, rooted entries, reparse escape, missing/unlisted files, length/hash/architecture mismatch, duplicate roles, wrong executable role, unsupported backend, incomplete configuration, model hash mismatch, and attempted implicit fallback.

- [ ] **Step 2: Run and confirm failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests.csproj"
```

Expected: FAIL because capability policy is absent.

- [ ] **Step 3: Implement verification and CPU-only MVP capability validation.**

Manifest members contain relative path, length, SHA-256, PE architecture, role, exact source commit, build flags, and license reference. The resolver starts at an injected fixed package root and never searches PATH/CWD/registry. The matrix answers support questions only; it never ranks or modifies the C1 configuration.

- [ ] **Step 4: Run tests and a privacy scan of fixture diagnostics.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests.csproj"
rg -n "[A-Za-z]:\\\\|prompt|response|token=" "tests/TestFixtures/GgufRuntimePackage"
```

Expected: tests PASS; `rg` returns no sensitive fixture data.

- [ ] **Step 5: Commit.**

```powershell
git add -- "runtime/GraniteEdgeAI.GgufRuntime.Capabilities" "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests" "tests/TestFixtures/GgufRuntimePackage"
git commit -m "feat(runtime): verify pinned GGUF runtime packages"
```

### Task 4: Build the deterministic fake CLI and supervisor state machine

**Files:**
- Create: `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli/GraniteEdgeAI.GgufRuntime.FakeCli.csproj`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli/Program.cs`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli/FakeCliOptions.cs`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/GraniteEdgeAI.GgufRuntime.Worker.csproj`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/Program.cs`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/GgufWorkerHost.cs`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliSession.cs`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliArgumentBuilder.cs`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliOutputParser.cs`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufSessionCoordinator.cs`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/IGgufCliProcess.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GgufCliArgumentBuilderTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GgufCliOutputParserTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GgufSessionCoordinatorTests.cs`

- [ ] **Step 1: Write failing argument, parser, and lifecycle tests.**

Assert that only allowlisted arguments are generated (`--model`, CPU/offload zero, context, cache, threads, batch, conversation/simple-I/O flags proven for the pin); prompts never appear in argv; ready/delta/completion markers are bounded; stderr cannot become chat text; illegal commands fail without state mutation; and only one generation runs.

- [ ] **Step 2: Run worker tests and confirm failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj"
```

Expected: FAIL because worker/session types do not exist.

- [ ] **Step 3: Implement the fake CLI and minimal supervisor core.**

Fake CLI modes: ready/two-turn stream, slow stream, ignore graceful stop, malformed output, stderr flood, early exit, and hang. It must open no socket. The worker is `WinExe`, communicates only through inherited pipes, writes prompts to CLI stdin, and converts only recognized pinned output to structured events.

- [ ] **Step 4: Run focused tests and fake CLI manually through redirected streams.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj"
@("first test turn", "second test turn") | dotnet run --project "tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli/GraniteEdgeAI.GgufRuntime.FakeCli.csproj" -- --scenario two-turn
```

Expected: tests PASS; fake CLI waits for redirected input and emits only documented deterministic markers.

- [ ] **Step 5: Commit.**

```powershell
git add -- "workers/GraniteEdgeAI.GgufRuntime.Worker" "tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli" "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests"
git commit -m "feat(runtime): supervise simple-io CLI sessions"
```

### Task 5: Add protected Windows supervisor launch and pipe client

**Files:**
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GraniteEdgeAI.GgufRuntime.WorkerClient.csproj`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeClient.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeClientOptions.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeSession.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufWorkerEnvironmentPolicy.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/Windows/GgufWorkerProcessLauncher.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/Windows/GgufWorkerJob.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/Windows/GgufWorkerPipeSet.cs`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/Windows/GgufNativeMethods.cs`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.ProtocolTestWorker/GraniteEdgeAI.GgufRuntime.ProtocolTestWorker.csproj`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.ProtocolTestWorker/Program.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GgufWorkerEnvironmentPolicyTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GgufWorkerProcessLauncherTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufWorkerLifecycleTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufWorkerContainmentTests.cs`

- [ ] **Step 1: Write failing environment, handle, and cleanup tests.**

Cover stripped credentials/proxies/model-hub tokens/runtime overrides/diagnostics; exact inherited handle allowlist; explicit application path; no shell; parent cancellation; worker crash; pipe loss; timeout; close idempotence; Job empty; bounded stdout/stderr; no socket listener.

- [ ] **Step 2: Run and confirm failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj"
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" --filter FullyQualifiedName~Containment
```

Expected: FAIL because the client/fixtures are absent.

- [ ] **Step 3: Implement the Windows launch boundary.**

Port only proven low-level patterns from the Model Inspection client into new G1-owned types; do not reference or modify its internals. Use `CreateProcessW`, `STARTUPINFOEX`, a handle list, minimal environment block, suspended launch plus Job assignment/resume if needed to eliminate escape, kill-on-close, and bounded asynchronous collectors.

- [ ] **Step 4: Run unit and process tests.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj"
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj"
```

Expected: PASS; every terminal-path assertion observes an empty Job and no listener.

- [ ] **Step 5: Commit.**

```powershell
git add -- "infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient" "tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.ProtocolTestWorker" "tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests" "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests"
git commit -m "feat(runtime): contain the GGUF supervisor process"
```

### Task 6: Complete streaming, stop/reload, and two-turn process behavior

**Files:**
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufCliSession.cs`
- Modify: `workers/GraniteEdgeAI.GgufRuntime.Worker/Session/GgufSessionCoordinator.cs`
- Modify: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/GgufRuntimeSession.cs`
- Modify: `tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli/Program.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufTwoTurnConversationTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufStopAndReloadTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufOutputBoundTests.cs`

- [ ] **Step 1: Write failing two-turn, stop, and flood tests.**

Assert ordered deltas, request/session correlation, one active generation, partial text retained on stop, `StoppedNeedsReload` when graceful stop fails, automatic child reconstruction before the next approved turn, capped stderr/stdout, and close cleanup after every scenario.

- [ ] **Step 2: Run and confirm the new scenarios fail.**

```powershell
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" --filter "FullyQualifiedName~TwoTurn|FullyQualifiedName~StopAndReload|FullyQualifiedName~OutputBound"
```

Expected: FAIL on missing stop/reload/turn behavior.

- [ ] **Step 3: Implement minimal semantics.**

Attempt only the interruption mechanism proven for the pinned CLI. On deadline, terminate the CLI child—not the supervisor—emit `StoppedNeedsReload`, preserve the partial response status, and rebuild the CLI with the same approved configuration plus the supervisor's bounded in-memory accepted-turn buffer before accepting the next prompt. After an app restart, the app supplies the selected persisted transcript as bounded initial turns in a new `StartSession`.

- [ ] **Step 4: Run all Day-1 tests.**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj"
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj"
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests.csproj"
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj"
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj"
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj"
```

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "workers/GraniteEdgeAI.GgufRuntime.Worker" "infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient" "tests/ProcessFixtures/GraniteEdgeAI.GgufRuntime.FakeCli" "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests"
git commit -m "feat(runtime): stream and stop GGUF generations"
```

**Day-1 checkpoint:** The process test harness completes two fake-CLI turns, stops a slow turn, reconstructs when required, closes, and observes no child/listener. If this checkpoint fails, do not start optional UI polish or any accelerator work.

## Day 2 — Persistent WinUI chat vertical slice

### Task 7: Add versioned atomic chat records and dated grouping

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatConversation.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatMessage.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatCompletionStatus.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatHistoryGroup.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/IChatHistoryStore.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/AtomicJsonChatHistoryStore.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatHistoryPolicy.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatHistoryGrouper.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History/ChatTitlePolicy.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/History/ChatHistoryStoreTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/History/ChatHistoryGrouperTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/History/ChatHistoryPrivacyTests.cs`

- [ ] **Step 1: Write failing history tests.**

Cover immediate `New chat` creation, locally derived bounded title, ordered messages, every user/assistant message, stopped/incomplete status, restart reload, Today/Yesterday/Previous 7 Days/older boundaries with injected clock/time zone, atomic replacement, interrupted write recovery, one corrupt record not blocking others, size limits, delete-one, clear-all, retention, and absence of absolute model paths.

- [ ] **Step 2: Run and confirm failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~Features.GgufRuntime.History --no-restore
```

Expected: FAIL because history types do not exist.

- [ ] **Step 3: Implement the local per-user store.**

Inject the app-local root and clock for tests. Write each versioned conversation to a same-directory temporary file, flush, atomically replace, and enumerate records with bounded reads. Quarantine by safe random ID or ignore corrupt records without copying content into diagnostics. Store model/profile IDs, never absolute paths.

- [ ] **Step 4: Run focused tests.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~Features.GgufRuntime.History --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/History" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/History"
git commit -m "feat(chat): persist dated local conversations"
```

### Task 8: Orchestrate runtime events into durable chat state

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/IGgufChatSession.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatSessionAdapter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services/GgufChatCoordinator.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ViewModels/ChatPageViewModel.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ViewModels/ChatConversationViewModel.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ViewModels/ChatMessageViewModel.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ViewModels/ChatHistoryGroupViewModel.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ViewModels/AsyncCommand.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services/GgufChatCoordinatorTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ViewModels/ChatPageViewModelTests.cs`

- [ ] **Step 1: Write failing orchestration tests with a fake session/store.**

Cover initialize/reload, immediate new-chat history entry, selection restoring every message, prompt saved before runtime submission, deltas updating one assistant row, completion saved, stop preserving partial text/status, fixed-category errors, retry as a new turn, stale event rejection, one generation at a time, and close cancellation.

- [ ] **Step 2: Run and confirm failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~GgufChatCoordinatorTests|FullyQualifiedName~ChatPageViewModelTests" --no-restore
```

Expected: FAIL because orchestration/view models are absent.

- [ ] **Step 3: Implement minimal coordinator and view models.**

Keep the absolute model path inside the runtime adapter only. Project events through request/session IDs, persist after each user append and assistant state transition, marshal observable changes to the UI dispatcher, and expose explicit `CanSend`, `CanStop`, loading, empty, ready, failed, and context-warning state.

- [ ] **Step 4: Run focused plus history tests.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~Features.GgufRuntime --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Services" "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ViewModels" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Services" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ViewModels"
git commit -m "feat(chat): orchestrate persistent GGUF conversations"
```

### Task 9: Build the approved chat page and centered Stop control

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatHistoryItem.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatMessageBubble.xaml.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/Controls/ChatComposerTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatPageTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/ChatAccessibilityTests.cs`

- [ ] **Step 1: Write failing native WinUI tests.**

Assert the approved rail/actions/groups/transcript/header/composer structure; all prompts/responses render in order; Send changes to Stop while generating; the stop square is centered independently of the label; the plus button is accessible but disabled/reserved; selection loads full history; keyboard focus order, accessible names/live region, high contrast, 200% scaling, narrow-width rail collapse, long text wrapping, and streaming scroll behavior.

- [ ] **Step 2: Run and confirm failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~ChatPageTests|FullyQualifiedName~ChatComposerTests|FullyQualifiedName~ChatAccessibilityTests" --no-restore
```

Expected: FAIL because XAML controls are absent.

- [ ] **Step 3: Implement the approved visual hierarchy.**

Use a two-column adaptive Grid, rounded conversation surface, grouped `ListView` history, virtualized transcript list, fixed composer, model/runtime/evidence header, and visual states for empty/loading/generating/error. Put the stop square in its own fixed-size Grid with centered alignments; do not compose `■ Stop` as one text run.

- [ ] **Step 4: Run all GgufRuntime app tests.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~Features.GgufRuntime --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime"
git commit -m "feat(chat): add the persistent GGUF chat page"
```

### Task 10: Prepare and apply the I0 navigation/project integration handoff

**Files (I0-owned changes):**
- Modify: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify: `IBM Granite with TurboQuant (Intel).slnx`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingStage.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/OnboardingShellPage.xaml.cs`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/Onboarding/README.md`
- Create: `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingChatNavigationTests.cs`
- Create: `docs/handoffs/2026-08-20-g1-gguf-chat-i0-integration.md`

- [ ] **Step 1: G1 writes the failing navigation contract test and handoff document without editing I0 files.**

The test specifies: an accepted inspected-model/runtime-configuration handoff advances to `ChatPage`; Back/new import detaches the old page/session; no feature searches for a parent Frame; and startup behavior remains unchanged until onboarding completes.

- [ ] **Step 2: Run the test and record the expected integration failure.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter FullyQualifiedName~OnboardingChatNavigationTests --no-restore
```

Expected: FAIL because I0 has not registered the route/project references.

- [ ] **Step 3: I0 reviews and applies the minimal integration edits.**

Add project references and XAML pages/resources, register new G1 projects in `.slnx`, import the packaging target from Task 11, and add the explicit inspection/configuration-to-chat transition. Do not make `ChatPage` manipulate shared navigation.

- [ ] **Step 4: Run navigation and protected onboarding/model-inspection regressions.**

```powershell
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" --filter "FullyQualifiedName~OnboardingChatNavigationTests|FullyQualifiedName~OnboardingModelInspectionNavigationTests|FullyQualifiedName~ModelInspectionPageNavigationTests" --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit with I0 attribution/approval recorded in the handoff.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" "IBM Granite with TurboQuant (Intel).slnx" "IBM Granite with TurboQuant (Intel)/Features/Onboarding" "IBM Granite with TurboQuant (Intel)/Features/README.md" "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingChatNavigationTests.cs" "docs/handoffs/2026-08-20-g1-gguf-chat-i0-integration.md"
git commit -m "feat(app): integrate the GGUF chat route"
```

**Day-2 checkpoint:** Through WinUI, a user creates multiple chats, sends/stops fake-CLI turns, selects history entries, restarts the app, and recovers every ordered prompt/response. If real runtime staging is unavailable, the fake-CLI vertical slice must still be complete and demonstrable.

## Day 3 — Packaging, real CPU smoke, and hardening

### Task 11: Package the pinned CLI and supervisor without network acquisition

**Prerequisite:** An authorized, locally staged llama.cpp CPU build with exact source commit, reproducible build flags, architecture, dependency closure, and license notices. The plan's research candidate `d59d455fd8ea09e5a2e87ce2a9d668267ffb5ccd` is not product support proof and must not be promoted unless the controlled build/evidence gate approves it.

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/GgufRuntime.WorkerPackaging.targets`
- Create: `scripts/gguf-runtime/New-GgufRuntimeManifest.ps1`
- Create: `scripts/gguf-runtime/Test-GgufRuntimeManifest.ps1`
- Create: `scripts/gguf-runtime/Test-GgufRuntimePackageClosure.ps1`
- Create: `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/README.md`
- Create: `workers/GraniteEdgeAI.GgufRuntime.Worker/README.md`
- Create: `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/README.md`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRuntimePackageTests.cs`
- Create: `tests/TestFixtures/GgufRuntimePackage/approved-runtime-input.example.json`

- [ ] **Step 1: Write failing package-closure tests.**

Assert the fixed `GgufRuntime/Worker` and `GgufRuntime/Cli` roles, exact allowlist, no PDB/raw logs/test fixture/server binary/quantizer/accelerator library in the CPU MVP, manifest before launch, embedded/copy manifest equality, license presence, and no network-fetch target or script.

- [ ] **Step 2: Run and confirm failure.**

```powershell
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" --filter FullyQualifiedName~GgufRuntimePackageTests
```

Expected: FAIL because packaging and approved input are absent.

- [ ] **Step 3: Implement deterministic publish/copy/manifest verification.**

Model the controlled mechanics on `ModelInspection.WorkerPackaging.targets` but use a separate fixed subtree and scripts. Accept only an explicit staged-runtime input property. Fail when absent for package builds; never download or discover a CLI. Hash after final copy and verify before adding package content.

- [ ] **Step 4: Build the x64 app package and verify closure.**

```powershell
dotnet build "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" -c Release -p:Platform=x64 -p:GgufRuntimeInputRoot="<APPROVED_LOCAL_RUNTIME_ROOT>"
powershell -NoProfile -File "scripts/gguf-runtime/Test-GgufRuntimePackageClosure.ps1" -PackageRoot "<BUILT_PACKAGE_ROOT>"
```

Expected: PASS with exact manifest membership. Replace angle-bracket paths with authorized local paths; never commit them.

- [ ] **Step 5: Commit scripts/targets/docs only; do not commit runtime binaries.**

```powershell
git add -- "IBM Granite with TurboQuant (Intel)/GgufRuntime.WorkerPackaging.targets" "scripts/gguf-runtime" "runtime/GraniteEdgeAI.GgufRuntime.Capabilities/README.md" "workers/GraniteEdgeAI.GgufRuntime.Worker/README.md" "infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/README.md" "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRuntimePackageTests.cs" "tests/TestFixtures/GgufRuntimePackage/approved-runtime-input.example.json"
git commit -m "build(runtime): package the pinned GGUF CLI"
```

### Task 12: Gate the optional real-model CPU integration smoke

**Prerequisite:** An authorized local, non-sensitive, inference-capable GGUF fixture. The existing `N-001-vocab-only-spm.gguf` is zero-tensor/vocabulary-only and must never be used as generation evidence.

**Files:**
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/ControlledGgufRuntimeConfiguration.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GgufRealModelSmokeTests.cs`
- Create: `tests/TestFixtures/GGUF/controlled-inference-model.example.json`
- Modify: `tests/TestFixtures/GGUF/README.md`
- Modify: `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/README.md`

- [ ] **Step 1: Write a gated integration test that fails only when configured artifacts violate policy.**

When environment/config is absent, skip with the exact documented reason. When present, verify model and runtime hashes before launch, run load + two turns + stop + close, assert non-empty bounded deltas/lifecycle rather than prose equality, and re-hash the source model afterward.

- [ ] **Step 2: Run without local configuration.**

```powershell
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" --filter FullyQualifiedName~GgufRealModelSmokeTests
```

Expected: SKIP with `controlled GGUF runtime/model not configured`; no download/network/process residue.

- [ ] **Step 3: Supply approved paths/hashes outside source control and run the controlled smoke.**

```powershell
$env:GRANITE_GGUF_RUNTIME_TEST_CONFIG = "<APPROVED_LOCAL_CONFIG_JSON>"
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" --filter FullyQualifiedName~GgufRealModelSmokeTests
Remove-Item Env:GRANITE_GGUF_RUNTIME_TEST_CONFIG
```

Expected: PASS; two turns stream, stop is classified, model hash is unchanged, Job is empty, and no listener exists.

- [ ] **Step 4: Scan test results/evidence for sensitive values.**

```powershell
rg -n "[A-Za-z]:\\\\|GRANITE_GGUF_RUNTIME_TEST_CONFIG|prompt|response" TestResults artifacts -g "*.trx" -g "*.json" -g "*.log"
```

Expected: no absolute model/runtime path or chat content. Delete/redact generated evidence if the scan finds any before continuing.

- [ ] **Step 5: Commit tests/docs only.**

```powershell
git add -- "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests" "tests/TestFixtures/GGUF/controlled-inference-model.example.json" "tests/TestFixtures/GGUF/README.md"
git commit -m "test(runtime): gate controlled GGUF inference smoke"
```

### Task 13: Run full hardening, regression, and executable handoff

**Files:**
- Create: `docs/evidence/2026-08-20-g1-gguf-chat-mvp-verification.md`
- Create: `docs/handoffs/2026-08-20-g1-gguf-chat-mvp.md`
- Modify: `IBM Granite with TurboQuant (Intel)/Features/README.md`
- Create: `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/README.md`
- Modify: `tests/README.md`

- [ ] **Step 1: Run format/whitespace and all new project tests.**

```powershell
git diff --check
dotnet test "tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/GraniteEdgeAI.GgufRuntime.Contracts.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/GraniteEdgeAI.GgufRuntime.Transport.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/GraniteEdgeAI.GgufRuntime.Worker.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.csproj" -c Release
dotnet test "tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -c Release --filter FullyQualifiedName~Features.GgufRuntime --no-restore
```

Expected: PASS except the explicitly documented real-model test may SKIP when approved artifacts are absent.

- [ ] **Step 2: Run protected Model Inspection and onboarding regressions.**

```powershell
dotnet test "tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/GraniteEdgeAI.ModelInspection.Transport.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/GraniteEdgeAI.ModelInspection.Worker.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj" -c Release
dotnet test "tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj" -c Release
dotnet test "tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj" -c Release --filter "FullyQualifiedName~Features.ModelInspection|FullyQualifiedName~Features.Onboarding" --no-restore
```

Expected: PASS with no protected production-file changes.

- [ ] **Step 3: Run packaged manual acceptance on a clean-machine-style x64 environment.**

Checklist:

1. Import/select a compatible inspected local model.
2. Start chat without viewing/typing a CLI command.
3. Send two turns and observe streaming.
4. Stop a long turn; partial output is marked stopped/incomplete.
5. Create at least three new chats; each appears immediately.
6. Restart; every prompt/response and date group is restored.
7. Delete one chat, then test clear-all/retention in a disposable profile.
8. Close during generation; verify worker/CLI exit and no listener.
9. Move/modify the model; verify `model changed since inspection`, not fallback.
10. Inspect diagnostic/evidence output for prompt/path absence.

- [ ] **Step 4: Write evidence and handoff with exact results.**

Record commit, branch, runtime source commit/build flags/hash, model fixture ID/hash (not path), each command/result/count, skips, package closure, process/listener checks, limitations, and deferred milestones. Do not claim a backend or real-model pass that was skipped.

- [ ] **Step 5: Commit the documentation closure.**

```powershell
git add -- "docs/evidence/2026-08-20-g1-gguf-chat-mvp-verification.md" "docs/handoffs/2026-08-20-g1-gguf-chat-mvp.md" "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/README.md" "IBM Granite with TurboQuant (Intel)/Features/README.md" "tests/README.md"
git commit -m "docs(runtime): close the GGUF chat MVP evidence"
```

**Day-3/MVP exit:** A packaged x64 CPU build runs the approved WinUI chat flow with an imported compatible model, full dated persistent transcripts, reliable Stop behavior, fixed trust/error boundaries, and verified child-process cleanup. If the real runtime/model prerequisites are unavailable, report the executable fake-CLI vertical slice and the blocked controlled smoke honestly; do not relabel it as full MVP acceptance.

---

## Post-MVP gated backlog

These items consume the remaining supplied G1 requirements but must begin only after the CPU MVP is accepted.

### Gate A: Intel Vulkan and SYCL capability proofs

1. Pin separate reproducible builds and manifests per backend.
2. Add backend/device enumeration evidence without ranking.
3. Run supported Intel hardware matrices for load, two turns, stop, cleanup, output quality, latency, memory, and fallback transparency.
4. Expose a backend only after C1 approves a complete configuration and evidence grade.
5. Retain the approved CPU configuration as a separate explicit fallback; never switch silently.

### Gate B: Explicit GGUF quantization artifact operation

1. Add `GraniteEdgeAI.GgufQuantization` contracts/client/worker separate from chat sessions.
2. Pin and manifest-verify `llama-quantize`; derive formats from that executable.
3. Reject unsuitable sources and all implicit re-quantization.
4. Preserve/hash the source; write same-volume temp; flush, validate, re-inspect, smoke, atomic rename.
5. Record provenance and present a new artifact without implying restored precision.

### Gate C: Experimental application-integrated TurboQuant

1. Require an exact fork/commit/build pin, license review, supported Granite model evidence, and fixed artifact/runtime boundary.
2. Implement only behind an experimental capability flag and separate C1-approved configuration.
3. Compare against the upstream llama.cpp CPU baseline with identical model/prompts/limits.
4. Prove deterministic fallback to a separately approved upstream configuration.
5. Remove/disable the capability when provenance, compatibility, or quality evidence is incomplete.

### Gate D: Later chat enhancements

- optional per-user encryption-at-rest after a concrete Windows protection design and recovery tests;
- rename/retention settings polish;
- attachments only after file-type, size, parsing, privacy, and prompt-injection boundaries are designed;
- multi-session support only after resource arbitration and isolation are specified.

---

## Final planning constraints

This plan authorizes no implementation, runtime/model download, workflow dispatch, push, pull request, or merge. Before execution, I0 must select the integration baseline and create/approve the dedicated worktree. During execution, stop at any missing runtime/model/license/I0 prerequisite and preserve the fake-CLI and CPU cut lines rather than broadening scope.
