# OpenVINO Route Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a command-line-first OpenVINO route that reuses the established GGUF-facing WinUI lifecycle, runs official OpenVINO GenAI on Intel CPU and explicitly proven Intel GPU, converts and optimizes supported local Granite models, and then adds one genuinely activated experimental TurboQuant TBQ4 configuration with explicit verified fallback.

**Architecture:** WinUI owns route-neutral user interaction and operation state; format adapters translate that state into closed, versioned JSON-line protocols. Official OpenVINO, sealed conversion, and experimental TurboQuant run in separate protected processes. All route clients share the existing hardened Windows process/job-object boundary, but protocols, native dependency closures, device rules, manifests, evidence, and executables remain isolated. Inspection and publication are manifest-bound transactions, and only typed sanitized evidence crosses into UI or CI artifacts.

**Tech Stack:** .NET 10 SDK with `net8.0`/MSTest 4.3.2/Microsoft.Testing.Platform; WinUI 3; C++20/CMake/MSVC x64; OpenVINO Runtime 2026.3.0; OpenVINO GenAI and Tokenizers 2026.3.0.0; sealed CPython 3.13.15 converter with Optimum Intel 2.1.0, Optimum 2.3.0, Transformers 5.5.4, OpenVINO Python 2026.3.0, OpenVINO GenAI Python 2026.3.0.0, and NNCF 3.3.0; PowerShell build/evidence scripts; GitHub Actions hosted Windows and trusted UCL Intel self-hosted runner.

**Spec:** [Approved OpenVINO route design](../specs/2026-08-20-openvino-route-design.md)

## Global Constraints

- Execute every implementation step test-first: add one focused failing test, run it and observe the expected failure, implement the minimum behavior, rerun the focused test, run the affected regression set, then commit.
- Do not weaken, bypass, or duplicate the established protected-process controls. The reusable process facade introduced below remains in the existing worker-client assembly and the GGUF client must pass its complete regression suite after the refactor.
- Command-line workers are the primary inference architecture. WinUI controls them; ordinary users never need a terminal. Model paths, prompts, generated text, credentials, tokens, and configuration secrets never appear in process arguments.
- Keep official OpenVINO, converter, and TurboQuant executables, DLL closures, manifests, protocols, installation roots, and evidence types separate. TurboQuant failure must not affect the stable official route.
- Use no `AUTO`, `HETERO`, or implicit device fallback. An Intel GPU request is supported only when requested and actual execution devices match exactly.
- Use `ChatHistory` for retained turns. Do not use deprecated `start_chat()` or `finish_chat()` APIs.
- Never infer model or hardware unsuitability from a tool failure. Return only the fixed support-code taxonomy from the approved design.
- Preserve local privacy: no network model lookup, no remote code, no source-supplied Python/native extension, no inherited credentials/proxies, and no path/prompt/generated text/raw native output in CI evidence.
- Treat `DEP-01`, `DEP-02`, `FIX-01`, `C1-01`, `I0-01`, `GPU-01`, `UCL-01`, `TQ-01`, and `LIC-01` as fail-closed gates. A task may prepare a gate, but exposure cannot proceed until the named evidence exists.
- Existing `tests/TestFixtures/OpenVINO/` is the repository's canonical fixture root. Use `tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1/` rather than creating a competing `tests/TestData` root; this is the repository-layout refinement of the spec's fixture ownership statement.
- O1 may add files under the O1-owned boundaries. Edits to the solution, app project, packaging import, D1 composition, existing Model Inspection UI/ViewModel/factory/controls, route-neutral prompt contract, central capability registry, onboarding/navigation, shared central test registry, or generated RTM/evidence require the explicit I0 integration steps in Tasks 8 and 18.
- Before each commit, run `git status --short`, stage only the files named by that task, inspect `git diff --cached --check` and `git diff --cached`, and preserve unrelated user work.

---

## Delivery and Gate Map

| Increment | Exit gate | User-visible result |
| --- | --- | --- |
| MVP, Tasks 1-9 | `DEP-01`, `FIX-01`, `C1-01`, `I0-01`, `LIC-01`, hosted tests, UCL CPU exact-commit evidence | Existing OpenVINO GenAI directory is inspected and one bounded CPU prompt streams through the established WinUI template with cancellation and zero residue. |
| Stable route, Tasks 10-14 | `GPU-01`, `DEP-02`, conversion/optimization evidence | Two-turn official session, explicit Intel GPU where proven, sealed local conversion, and independently published FP16/INT8/INT4 artifacts. |
| TurboQuant, Tasks 15-17 | `TQ-01`, activation proof, matched quality/memory/performance evidence | One pinned Granite TBQ4/TBQ4 Intel CPU configuration works in normal WinUI prompting and offers explicit official/GGUF fallback. |
| Release closure, Task 18 | all audit, packaging, privacy, accessibility, cleanup, and traceability gates | Reproducible release candidate and complete I0 handoff. |

## Task 1: Freeze Dependency, License, and Build Identity

**Files:**
- Create: `third-party/openvino-official/openvino-runtime.lock.json`
- Create: `third-party/openvino-official/openvino-genai.lock.json`
- Create: `third-party/openvino-official/openvino-tokenizers.lock.json`
- Create: `third-party/openvino-official/LICENSES.md`
- Create: `third-party/openvino-converter/python-runtime.lock.json`
- Create: `third-party/openvino-converter/requirements.lock`
- Create: `third-party/openvino-converter/wheel-manifest.json`
- Create: `third-party/openvino-converter/LICENSES.md`
- Create: `scripts/openvino/Test-OpenVinoDependencyLocks.ps1`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/DependencyLockContractTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj`

- [ ] Add `DependencyLockContractTests` asserting exact official tags/commits, nonzero archive lengths, lowercase 64-hex SHA-256 values, closed file inventories, license entries, and no floating URL/ref. Use the exact identities `2026.3.0`/`8a17657b995fd3b4a52f8484acfcf2bb61214623`, `2026.3.0.0`/`bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0`, and `2026.3.0.0`/`183c6f25cda2a469cba5eff8b72022d2d51ba0ca`.
- [ ] Run `dotnet test tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter DependencyLockContractTests` and confirm failure because the lock files do not exist.
- [ ] Acquire each official Windows archive from its pinned release, calculate length/SHA-256 locally, enumerate every shipped file, review redistribution licenses, and write the three official lock files. Do not commit downloaded binaries.
- [ ] Resolve the converter closure for CPython 3.13.15 and the exact package train in the header with `pip download --only-binary=:all: --platform win_amd64 --python-version 313`; write hashes for every wheel in `requirements.lock` and record filename, length, SHA-256, package license, and source URL in `wheel-manifest.json`.
- [ ] Implement `Test-OpenVinoDependencyLocks.ps1` to recompute a supplied archive/wheel directory, reject additions/omissions/hash mismatch, reject sdists, and emit only package identifiers and support codes.
- [ ] Run the focused contract tests and script against a copied closure; expect `Test Run Successful` and `dependency_lock_valid`. Tamper one byte in an operation-owned copy and expect `runtime_integrity_failed` without an absolute path in output.
- [ ] Record signed reviewer disposition for `DEP-01`, `DEP-02`, and `LIC-01` in the lock metadata. These fields must be concrete identities/timestamps and must reject empty or sentinel values.
- [ ] Commit: `build(openvino): pin official and converter dependency closures`

## Task 2: Define the Closed OpenVINO Contracts

**Files:**
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/GraniteEdgeAI.OpenVino.Contracts.csproj`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Protocol/OpenVinoProtocol.cs`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Protocol/OpenVinoProtocolJsonContext.cs`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Protocol/Commands.cs`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Protocol/Events.cs`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Inspection/OpenVinoPackageInspection.cs`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Generation/OpenVinoSessionModels.cs`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Evidence/OpenVinoRuntimeEvidence.cs`
- Create: `shared/GraniteEdgeAI.OpenVino.Contracts/Errors/OpenVinoSupportCode.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolJsonTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolSequenceTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/SupportCodeTests.cs`

- [ ] Add serialization tests for every command/event, unknown-member rejection, duplicate-property rejection, case sensitivity, 32-level JSON depth, 1 MiB UTF-8 line cap, 64 KiB prompt cap, 512 new-token cap, 4 MiB operation text cap, 32-turn cap, and stale UUID rejection.
- [ ] Run the focused tests and confirm compile failure because contract types are absent.
- [ ] Implement source-generated strict JSON contracts. The central command shape must be concrete:

```csharp
public sealed record StartSessionCommand(
    Guid SessionId,
    Guid InspectionRunId,
    string PackageManifestDigest,
    OpenVinoDeviceRequest Device,
    OpenVinoGenerationLimits Limits);

public sealed record PromptCommand(
    Guid SessionId,
    Guid TurnId,
    string Prompt,
    int RequestedNewTokens);

public sealed record TokenEvent(Guid SessionId, Guid TurnId, long Sequence, string Text);
```

- [ ] Define distinct `openvino.official/1` and `openvino.turboquant/1` protocol identifiers; define ordered session states `Hello`, `SessionStarted`, `GenerationStarted`, `Token*`, `TurnCompleted|TurnFailed`, and `SessionCompleted|SessionFailed|SessionCancelled`.
- [ ] Implement the full fixed support-code set from design section 18 as string-backed serialization values: `package_missing_resource`, `package_inconsistent_resource`, `package_unsafe_path`, `package_changed`, `package_unreadable`, `model_architecture_unsupported`, `model_task_unsupported`, `tokenizer_unsupported`, `runtime_integrity_failed`, `runtime_dependency_missing`, `runtime_load_failed`, `runtime_device_unavailable`, `runtime_device_mismatch`, `runtime_context_exceeded`, `runtime_protocol_failed`, `runtime_timed_out`, `operation_cancelled`, `conversion_preflight_failed`, `conversion_failed`, `conversion_output_invalid`, `conversion_publish_failed`, `optimization_unsupported`, `turboquant_unavailable`, and `turboquant_activation_unverified`. Tests must prove raw exception/native messages cannot serialize into the support-code field.
- [ ] Add exact `ModelInspectionHandoffV2` validation: schema `2`, lowercase canonical UUIDv4 IDs, `Ready|ReadyWithWarnings`, lowercase 64-hex model SHA-256, positive model length, opaque hardware handoff, canonical JSON at most 512 bytes, and no paths. Bind `modelSha256`/`modelLengthBytes` to `openvino_model.bin`.
- [ ] Run `dotnet test tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --filter "FullyQualifiedName~ProtocolJsonTests|FullyQualifiedName~ProtocolSequenceTests|FullyQualifiedName~SupportCodeTests"`; expect all focused tests to pass. Run the same project without the filter; expect zero failures.
- [ ] Commit: `feat(openvino): define strict route contracts`

## Task 3: Extract the Shared Protected-Worker Process Facade

**Files:**
- Create: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/ProtectedWorker/ProtectedWorkerLaunchSpec.cs`
- Create: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/ProtectedWorker/IProtectedWorkerSessionFactory.cs`
- Create: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/ProtectedWorker/ProtectedWorkerSession.cs`
- Create: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/ProtectedWorker/ProtectedWorkerSessionFactory.cs`
- Modify: `infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/InspectionWorkerClient.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/ProtectedWorkerSessionFactoryTests.cs`
- Modify: `tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj`

- [ ] Add tests proving a fixed executable plus fixed non-sensitive selector arguments launches suspended, receives only allowlisted inherited handles/environment, enters a kill-on-close job before resume, exposes bounded stdin/stdout/stderr, observes the parent, terminates the full tree, and reports cleanup within five seconds.
- [ ] Run `dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj --filter FullyQualifiedName~ProtectedWorkerSessionFactoryTests`; confirm compile failure for the missing facade.
- [ ] Implement this minimal public boundary in the existing assembly:

```csharp
public sealed record ProtectedWorkerLaunchSpec(
    VerifiedWorkerExecutable Executable,
    IReadOnlyList<string> FixedArguments,
    IReadOnlyDictionary<string, string> Environment,
    int MaximumStandardErrorBytes,
    TimeSpan StartupTimeout,
    TimeSpan CancellationGrace,
    TimeSpan CleanupTimeout);

public interface IProtectedWorkerSessionFactory
{
    Task<ProtectedWorkerSession> StartAsync(
        ProtectedWorkerLaunchSpec spec,
        CancellationToken cancellationToken);
}
```

- [ ] Refactor `InspectionWorkerClient` to consume the facade without changing its public results, support codes, timeout values, or protocol behavior.
- [ ] Run the focused tests, then the complete WorkerClient unit and ModelInspection worker-process suites. Expected result: all pre-existing GGUF/model-inspection tests and new facade tests pass.
- [ ] Add a contract test that rejects model paths/prompts/JSON payloads in `FixedArguments` and rejects environment keys outside the existing closed allowlist.
- [ ] Commit: `refactor(worker): share protected process session boundary`

## Task 4: Build the Route-Specific Managed Worker Client

**Files:**
- Create: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/GraniteEdgeAI.OpenVino.WorkerClient.csproj`
- Create: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/IOpenVinoWorkerClient.cs`
- Create: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoWorkerClient.cs`
- Create: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoWorkerClientOptions.cs`
- Create: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoWorkerInstallation.cs`
- Create: `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/OpenVinoConversation.cs`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.OpenVino.ProtocolTestWorker/GraniteEdgeAI.OpenVino.ProtocolTestWorker.csproj`
- Create: `tests/ProcessFixtures/GraniteEdgeAI.OpenVino.ProtocolTestWorker/Program.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/OpenVinoWorkerClientTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/ProtocolContainmentTests.cs`

- [ ] Write process-fixture scenarios for valid two-turn streaming, partial stop followed by another prompt, session cancellation, stale event, wrong protocol, malformed/oversized line, stdout after terminal, stderr overflow, startup/turn/idle/session timeout, child escape attempt, parent exit, and cleanup inventory.
- [ ] Run the new tests and observe compile failure for `IOpenVinoWorkerClient`.
- [ ] Implement `IOpenVinoWorkerClient.InspectAsync` and `StartSessionAsync`; `OpenVinoConversation` must permit one active turn, accept idempotent stop/cancel, discard stale IDs, enforce startup 5s/turn 10m/idle 5m/session 60m/cancel 5s/cleanup 5s, and dispose by cancelling then verifying process-tree exit.
- [ ] Use `BoundedUtf8LineReader/Writer` from the existing transport project; do not fork their logic. Keep typed parsing in the OpenVINO contracts project.
- [ ] Run focused valid and hostile scenarios, then all new WorkerClient/process tests. Expect valid scenarios to complete and every hostile scenario to return `runtime_protocol_failed`, `runtime_timed_out`, or `operation_cancelled` with no residue and bounded sanitized stderr.
- [ ] Run the existing ModelInspection WorkerClient/process suites again to prove the shared launcher remains compatible.
- [ ] Commit: `feat(openvino): add protected JSONL worker client`

## Task 5: Generate and Lock the Deterministic GenAI Fixture

**Files:**
- Create: `tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1/source/fixture-spec.json`
- Create: `tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1/package/` (generated safe model resources)
- Create: `tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1/manifest.json`
- Create: `tests/TestFixtures/OpenVINO/GenAI/TinySyntheticV1/LICENSE.txt`
- Create: `scripts/openvino/New-OpenVinoGenAiFixture.ps1`
- Create: `scripts/openvino/Test-OpenVinoGenAiFixture.ps1`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/FixtureContractTests.cs`

- [ ] Add fixture contract tests requiring the exact package resources `openvino_model.xml/.bin`, `openvino_tokenizer.xml/.bin`, `openvino_detokenizer.xml/.bin`, `config.json`, `generation_config.json`, and `tokenizer_config.json`; require a closed lowercase SHA-256/length manifest and redistribution license.
- [ ] Run the fixture tests and observe missing-fixture failures.
- [ ] Implement deterministic generation from reviewed static source tensors/config; generation must run offline, normalize timestamps/order, and reproduce byte-identical files on two clean operation-owned directories.
- [ ] Make the fixture a tiny text-only dense Granite-compatible GenAI package that produces a fixed bounded result with at most 32 generated tokens through the real pinned CPU `LLMPipeline`. It must contain no scripts, remote references, external data, symlinks/reparse points, or credentials.
- [ ] Run generation twice and compare manifest digests; expect equality. Run `Test-OpenVinoGenAiFixture.ps1`; expect `fixture_valid`. Independently load it in the native smoke tool introduced in Task 7 before closing `FIX-01`.
- [ ] Commit: `test(openvino): add deterministic GenAI fixture`

## Task 6: Implement Device-Neutral Package Inspection

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Inspection/OpenVinoPackageSnapshotter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Inspection/OpenVinoStaticPackageInspector.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Inspection/OpenVinoPackagePolicy.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Inspection/OpenVinoInspectionHandoffFactory.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Inspection/OpenVinoStaticPackageInspectorTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Inspection/OpenVinoInspectionHandoffFactoryTests.cs`
- Create: `tests/TestFixtures/OpenVINO/Malformed/` (closed malformed fixture matrix)

- [ ] Write tests for missing/inconsistent resources, 4,097th entry, depth 17, JSON over 16 MiB/depth 33, XML over 256 MiB, DTD/entity/external resolution, symlink/junction/reparse/path escape, case collision, alternate data stream, executable/script content, unreadable/mutating files, invalid architecture/task/tokenizer, and canonical handoff over 512 bytes.
- [ ] Run focused tests and observe expected failures because inspectors do not exist.
- [ ] Implement descendant-only traversal with limits 4,096 entries/depth 16, safe optional allowlist, streaming XML with DTD/external resolution disabled, strict bounded JSON, case-insensitive uniqueness, reparse rejection, and full relative path/length/SHA-256 manifest digest.
- [ ] Open required resources with `FileShare.Read` only for the inspection operation, verify stable identity before/after hashing, and bind the downstream model identity specifically to `openvino_model.bin`.
- [ ] Validate `model_type=granite`, architecture exactly `GraniteForCausalLM`, text-only task `text-generation-with-past`, and tokenizer/config consistency. Reject GraniteMoE/hybrid/multimodal/speech and unrecognized optional files with fixed codes.
- [ ] Emit `Ready|ReadyWithWarnings` only after Task 7 native device-neutral `ov::Core::read_model` validation succeeds. Do not construct a hardware pipeline during inspection and do not claim compatibility.
- [ ] Run all malformed and happy-path tests, then the new project. Expect fixed support codes and no raw paths in results.
- [ ] Commit: `feat(openvino): add bounded package inspection`

## Task 7: Build the Official Native Inspector and CPU Session Worker

**Files:**
- Create: `workers/OpenVinoOfficial.Worker/CMakeLists.txt`
- Create: `workers/OpenVinoOfficial.Worker/src/main.cpp`
- Create: `workers/OpenVinoOfficial.Worker/src/protocol.cpp`
- Create: `workers/OpenVinoOfficial.Worker/src/package_inspector.cpp`
- Create: `workers/OpenVinoOfficial.Worker/src/session.cpp`
- Create: `workers/OpenVinoOfficial.Worker/src/runtime_evidence.cpp`
- Create: `workers/OpenVinoOfficial.Worker/tests/protocol_tests.cpp`
- Create: `workers/OpenVinoOfficial.Worker/tests/session_tests.cpp`
- Create: `scripts/openvino/Build-OpenVinoOfficialWorker.ps1`
- Create: `scripts/openvino/New-OpenVinoOfficialWorkerManifest.ps1`
- Create: `scripts/openvino/Test-OpenVinoOfficialWorkerManifest.ps1`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/OfficialCpuFixtureTests.cs`

- [ ] Add native protocol/state tests and .NET process tests for fixture inspection, real `LLMPipeline` construction on `CPU`, deterministic one-turn streaming, two sequential turns with explicit `ChatHistory`, stop partial success, cancellation, context overflow, corrupt tokenizer/model, runtime identity, parent exit, and zero process/handle/temp residue.
- [ ] Run CTest/.NET focused tests and observe missing executable failures.
- [ ] Implement a fixed-mode CLI (`--protocol openvino.official/1` is the only argument) that reads model/package/prompt data only from bounded stdin and writes typed JSONL only to stdout. Send diagnostics only to bounded stderr with path/prompt redaction.
- [ ] Harden native DLL loading to the verified worker directory, verify the complete runtime manifest before loading, clear ambient DLL search/current-directory behavior, and report exact Runtime/GenAI/Tokenizers build identities.
- [ ] For inspection call device-neutral `ov::Core::read_model` on model/tokenizer/detokenizer IR. For sessions construct `ov::genai::LLMPipeline(package, "CPU", properties)`, apply `ChatHistory`, authoritative token accounting, `StreamingStatus::STOP` for stop and `StreamingStatus::CANCEL` for cancellation.
- [ ] Report requested `CPU` and actual execution devices through bounded typed evidence. Reject unknown context limit/template/tokenization errors using the fixed taxonomy.
- [ ] Build Release x64 against only the Task 1 closure and create a file manifest. Run CTest, official process tests, fixture verifier, and dependency manifest verifier. Expect deterministic output and no loaded module outside Windows/system plus the verified closure.
- [ ] Close `FIX-01` only after the real pipeline test passes twice from clean directories.
- [ ] Commit: `feat(openvino): add official CPU CLI worker`

## Task 8: Integrate the MVP Through the Existing WinUI Prompt Surface

**Files (O1 additions):**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoRouteService.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoRouteSession.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoRouteStateMachine.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoPromptAdapter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OpenVinoRouteCapability.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/OpenVinoRouteStateMachineTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/OpenVinoPromptAdapterTests.cs`
- Modify with I0 owner: `IBM Granite with TurboQuant (Intel)/Infrastructure/ModelInspectionServiceComposition.cs`
- Modify with I0 owner: `IBM Granite with TurboQuant (Intel)/Features/ModelImport/ModelInspectionRequestFactory.cs` or its approved directory seam
- Modify with I0 owner: route-neutral prompt contract and central route/capability registry selected by `C1-01`
- Modify with I0 owner: `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- Modify with I0 owner: `IBM Granite with TurboQuant (Intel).slnx`
- Create with I0 owner: `IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets`
- Modify with I0 owner: existing Model Inspection/route UI tests and app composition tests

- [ ] Obtain the exact C1 route-neutral configuration/prompt contracts and I0 integration window; record concrete interface names and approved CPU limits in the integration commit. Stop here if `C1-01` or `I0-01` is not resolved.
- [ ] Write state-machine/adapter tests proving the common lifecycle `Idle -> Inspecting -> TerminalInspectionOutcome -> AwaitingConfiguration -> Loading -> SessionReady -> GeneratingTurn -> TurnCompleted -> SessionReady -> SessionCompleted`, one active operation, immutable terminal state, stale-event discard, cancellation on navigation/app close, and reset creating a new worker/process/session ID.
- [ ] Run focused tests and observe missing service/adapter failures.
- [ ] Implement O1 route classes over `IOpenVinoWorkerClient`; adapt streaming/stop/cancel/failure into the same route-neutral prompt events used by GGUF without importing GGUF quantization/runtime types.
- [ ] Add I0-owned integration tests first: `OpenVinoDirectory` selection stays directory-based, D1 selection remains route-correct, a schema-v2 Ready handoff enters the established shared page/template, CPU is the only initial configuration, and existing GGUF snapshots/behavior are unchanged.
- [ ] In the serial I0 commit, register projects, app composition, x64-only packaging, worker manifest verification, and the O1 adapter. Reuse existing front-end templates; add only route-specific labels, evidence rows, and unsupported/recovery copy. Do not create a parallel prompting page.
- [ ] Package the official worker under `OpenVino/Official/Worker` with manifest embedded and copied to output/publish; prove Debug/Release and unpackaged/MSIX resolution reject missing/tampered binaries.
- [ ] Run OpenVINO unit/contract/process tests, affected app tests, all ModelImport/ModelInspection tests, and a Release x64 app build. Expect one real CPU prompt and cancellation through the normal UI harness, no terminal window, no secret arguments, and zero worker/listener/temp residue.
- [ ] Commit O1 additions: `feat(openvino): integrate official route service`
- [ ] Commit serial I0 seam: `feat(app): register OpenVINO MVP route`

## Task 9: Add Hosted and Trusted UCL CPU Evidence

**Files:**
- Create: `.github/workflows/openvino-official-ci.yml`
- Create: `.github/workflows/openvino-ucl-intel.yml`
- Create: `scripts/openvino/Invoke-OpenVinoOfficialEvidence.ps1`
- Create: `scripts/openvino/Test-OpenVinoEvidencePrivacy.ps1`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/WorkflowContractTests.cs`

- [ ] Add workflow contract tests requiring SHA-pinned actions, least-privilege `contents: read`, immutable checked-out commit recording, x64 Release builds, exact dependency verification, minimum expected test counts, timeouts, artifact retention 30 days, `if: always()` cleanup/integrity, and no secrets/paths/prompts/model bytes/raw logs in artifacts.
- [ ] Run workflow tests and observe failure because workflows are absent.
- [ ] Implement hosted Windows CI for contracts, static inspection, native build/unit tests, deterministic fixture, process containment, app adapter tests, and package tamper tests.
- [ ] Implement manual-only trusted UCL workflow on labels `[self-hosted, Windows, X64, workbook05, intel-target]`. Require controlled immutable fixture/model outside workspace, read-only state, exact length/SHA-256, explicit authorization preflight (`UCL-01`), exact checked-out commit, Intel CPU/runtime inventory, cleanup verification, and post-run model integrity.
- [ ] Generate only typed sanitized JSON evidence: commit, lock/manifest identities, requested/actual CPU, runtime/build identity, test counts, bounded performance aggregates, cancellation/cleanup disposition. Never upload prompts/generated text/machine or account identity/paths.
- [ ] Run workflow contract and privacy tests locally; expect pass. Dispatch only after `UCL-01`; the exact commit must pass one-turn, two-turn, cancellation, hostile-input, manifest tamper, and zero-residue checks on the UCL Intel laptop.
- [ ] Mark the MVP accepted only when hosted and UCL evidence point to the same immutable commit and all MVP criteria in design section 24.1 pass.
- [ ] Commit: `ci(openvino): add official and UCL CPU evidence gates`

## Task 10: Add Explicit Intel GPU Execution

**Files:**
- Modify: `shared/GraniteEdgeAI.OpenVino.Contracts/Generation/OpenVinoSessionModels.cs`
- Modify: `workers/OpenVinoOfficial.Worker/src/session.cpp`
- Modify: `workers/OpenVinoOfficial.Worker/src/runtime_evidence.cpp`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/OfficialGpuTests.cs`
- Modify: `.github/workflows/openvino-ucl-intel.yml`
- Create: `scripts/openvino/Test-OpenVinoGpuEvidence.ps1`

- [ ] Add tests that accept only `GPU` or an enumerated `GPU.n`, reject `AUTO`/`HETERO`, fail when requested and actual devices differ, and return `runtime_device_unavailable` for missing/unsupported driver without changing drivers.
- [ ] Run GPU contract tests; observe expected failure because only CPU is allowed.
- [ ] Implement explicit device enumeration and selection. Pass the exact device string to `LLMPipeline`, collect actual execution device data, and make equality a terminal precondition before exposing the session.
- [ ] Extend UCL evidence with GPU/driver/plugin/runtime/config identity and forced negatives for nonexistent device plus CPU-resolution mismatch. Never silently retry on CPU.
- [ ] Run all CPU regressions locally. After identifying the UCL Intel GPU and driver (`GPU-01`), run one-turn/two-turn/cancellation/cleanup and requested/actual equality on the exact commit. If unavailable, keep GPU hidden and record the fixed driver-prerequisite result; do not block CPU stable route.
- [ ] Commit: `feat(openvino): add explicitly verified Intel GPU route`

## Task 11: Inspect Supported Source Folders and Seal the Converter

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Conversion/SourceModelInspector.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Conversion/SourceModelPolicy.cs`
- Create: `workers/OpenVinoConverter.Worker/converter/__main__.py`
- Create: `workers/OpenVinoConverter.Worker/converter/protocol.py`
- Create: `workers/OpenVinoConverter.Worker/converter/export.py`
- Create: `workers/OpenVinoConverter.Worker/converter/provenance.py`
- Create: `scripts/openvino/Build-OpenVinoConverterWorker.ps1`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Conversion/SourceModelInspectorTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/ConverterIsolationTests.cs`

- [ ] Add source tests for exact allowlist `model_type=granite`, `GraniteForCausalLM`, `text-generation-with-past`, local Safetensors index/shards, no missing/duplicate tensor shards, tokenizer completeness, no remote identifier/code/import/plugin/native extension, and rejection of PyTorch pickle/bin, GraniteMoE/hybrid/multimodal/speech.
- [ ] Add converter isolation tests for `python.exe -I -s -E`, closed module path, read-only wheel closure, no user/system site, no Hub cache, no credentials/proxies, `HF_HUB_OFFLINE=1`, `TRANSFORMERS_OFFLINE=1`, blocked network, fixed stdin protocol, no data arguments, and 120-minute operation timeout.
- [ ] Run tests and observe missing inspector/worker failures.
- [ ] Implement source snapshot locking with relative path/length/SHA-256 manifest and held `FileShare.Read` handles. Implement converter with `local_files_only=True`, `trust_remote_code=False`, `library="transformers"`, `task="text-generation-with-past"`, `weight_format="fp16"`, and complete tokenizer/detokenizer IR export.
- [ ] Verify the worker imports only modules from the sealed closure and source data is never on `sys.path`. Attempt DNS/socket access in the isolation test and expect denial while conversion still succeeds from local input.
- [ ] Run source matrix, isolation, dependency lock, and malformed-output tests. Expect only typed progress/failure and no path/native trace in parent-visible diagnostics.
- [ ] Commit: `feat(openvino): add sealed offline Granite converter`

## Task 12: Implement Atomic Conversion Publication and UI Flow

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Conversion/OpenVinoConversionService.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Conversion/ConversionTransaction.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Conversion/OpenVinoProvenance.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Conversion/ConversionTransactionTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/ConversionEndToEndTests.cs`
- Modify with I0 owner: established shared route page/template and route-neutral action registry

- [ ] Add tests for explicit confirmation, absent sibling destination, source/destination overlap and alias, reparse escape, insufficient space, fresh operation-owned staging, source mutation, cancellation at every stage, converter failure, invalid output, failed smoke, atomic rename failure, late completion, retry with new ID, and exact cleanup only.
- [ ] Run focused tests and observe missing transaction failures.
- [ ] Implement the transaction order exactly: reinspect/lock source; preflight/confirm; create sibling staging; run sealed converter; native reinspect; real official CPU pipeline smoke; write sanitized provenance; atomic rename to absent destination; reinspect published result; issue new inspection run identity.
- [ ] Implement provenance fields from design section 14.1 and a strict test that rejects any raw path/account/host/prompt/answer/credential/environment/native output.
- [ ] Integrate through the existing shared front-end template with route-specific conversion action, bounded progress, cancel/retry, and fixed recovery copy. The UI must distinguish unsuitable source, conversion failure, hardware failure, and cancellation.
- [ ] Run end-to-end conversion on a controlled dense Granite Safetensors model with network denied. Verify source manifest unchanged, final package independently Ready, smoke and quality dispositions separate, and failure/cancel leaves final absent and no unowned deletion.
- [ ] Commit O1: `feat(openvino): publish converted packages atomically`
- [ ] Commit I0 seam: `feat(app): expose OpenVINO conversion action`

## Task 13: Add Standard Persistent and Runtime Optimization

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationService.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Optimization/OpenVinoOptimizationCandidate.cs`
- Create: `workers/OpenVinoConverter.Worker/converter/optimize.py`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoOptimizationTests.cs`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/OptimizationEndToEndTests.cs`
- Modify with I0/C1 owner: central route/capability registry

- [ ] Add tests separating persistent FP16/INT8/INT4 weight packages from runtime KV-cache `u8`/`u4` and disposable compiled cache. Reject GGUF quantization names/flags and any claim that runtime cache is a converted artifact.
- [ ] Run focused tests and observe failures for missing optimization types.
- [ ] Implement fresh-staging/atomic-publication/reinspection/load/generation/provenance for FP16 baseline, INT8 weight-only, then INT4 weight-only. Never expand an already compressed source and call it restored FP16.
- [ ] Implement runtime candidates beginning with released defaults, then `u8` KV-cache only after exact CPU evidence, and `u4`/independent key-value precision only per C1-approved evidenced tuple. Bind compiled caches to runtime/plugin/device/driver/model/config identities and treat them as deletable.
- [ ] Have C1 map Automatic/Quality/Balanced/Efficiency objectives to a closed candidate; O1 accepts only that exact candidate and reports requested/actual precision/device.
- [ ] For every exposed tuple run deterministic smoke, independent quality rubric, memory, TTFT, throughput, context scaling, cancellation, corruption, and cleanup. Keep quality acceptance separate from generation success.
- [ ] Run all conversion and official route regressions. Expect independently Ready artifacts and no source mutation or silent candidate fallback.
- [ ] Commit O1: `feat(openvino): add standard optimization pipeline`
- [ ] Commit C1/I0 registration: `feat(config): register evidenced OpenVINO candidates`

## Task 14: Prove the Complete Stable Official Route

**Files:**
- Create: `scripts/openvino/Invoke-OpenVinoStableAcceptance.ps1`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/StableRouteAcceptanceTests.cs`
- Modify: `.github/workflows/openvino-official-ci.yml`
- Modify: `.github/workflows/openvino-ucl-intel.yml`

- [ ] Encode complete-stable acceptance tests: deterministic inspection, CPU two-turn ChatHistory, reset creates a new process/session, stop then continue, cancellation/tree cleanup, explicit GPU where proven, offline conversion/source preservation, FP16/INT8/INT4 publication, standard KV settings per evidence, tamper/corruption/timeout cases, and shared UI parity with GGUF.
- [ ] Run the acceptance filter and observe failure until all prior increments are registered and evidence files exist.
- [ ] Implement the acceptance script to verify immutable commit, lock/build/model/config/device/driver identities, test counts, privacy schema, and absence of residue; do not rerun or silently substitute a missing capability.
- [ ] Run hosted stable acceptance and trusted UCL CPU plus available GPU on the same immutable commit. Expect all mandatory CPU results; GPU may remain hidden only with explicit `runtime_device_unavailable` evidence.
- [ ] Commit: `test(openvino): close stable route acceptance`

## Task 15: Recover or Port the Narrow TBQ4 CPU Codec

**Files:**
- Create: `third-party/openvino-turboquant/upstream.lock.json`
- Create: `third-party/openvino-turboquant/patches/series.json`
- Create: `third-party/openvino-turboquant/LICENSES.md`
- Create: `third-party/openvino-turboquant/README.md`
- Create: `scripts/openvino/Build-OpenVinoTurboQuantRuntime.ps1`
- Create: `scripts/openvino/Test-OpenVinoTurboQuantPatchClosure.ps1`
- Create: `workers/OpenVinoTurboQuant.Worker/native/tbq4_codec_tests.cpp`
- Create: `workers/OpenVinoTurboQuant.Worker/native/tbq4_dispatch_tests.cpp`

- [ ] Resolve `TQ-01` by identifying the historic audited custom branch/commit. If its complete reproducible source is unavailable, create a reviewable patch series containing only symmetric TBQ4 key/value encode/decode and CPU SDPA dispatch for the accepted head dimension; do not include TBQ3, QJL, PolarQuant, GPU, PagedAttention, or prefill compression.
- [ ] Write algorithm tests before runtime changes: golden packed bytes, round trip, saturation/zero/NaN policy, lane/tail/head-dimension bounds, alignment, malformed metadata, deterministic multithreading, and forced-disabled dispatch.
- [ ] Run codec tests against unpatched pinned source; expect missing symbol/dispatch failure.
- [ ] Apply the audited patch series to a clean source tree at the locked commit, build MSVC Release x64 CPU runtime plus compatible GenAI, and produce complete source/patch/binary/license manifests.
- [ ] Run codec/dispatch tests under sanitizing/debug instrumentation where supported and Release conformance twice. Expect exact packed representation and nonzero dispatch only for the approved TBQ4/TBQ4 tuple.
- [ ] Have license/security reviewers approve the fork closure. No experimental binary is packaged or registered before `TQ-01` and `LIC-01` are closed.
- [ ] Commit: `feat(turboquant): add audited TBQ4 CPU runtime patch`

## Task 16: Build and Verify the Separate TurboQuant Worker

**Files:**
- Create: `workers/OpenVinoTurboQuant.Worker/CMakeLists.txt`
- Create: `workers/OpenVinoTurboQuant.Worker/src/main.cpp`
- Create: `workers/OpenVinoTurboQuant.Worker/src/session.cpp`
- Create: `workers/OpenVinoTurboQuant.Worker/src/activation_evidence.cpp`
- Create: `scripts/openvino/New-OpenVinoTurboQuantWorkerManifest.ps1`
- Create: `scripts/openvino/Test-OpenVinoTurboQuantActivation.ps1`
- Create: `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/TurboQuantWorkerTests.cs`
- Create: `.github/workflows/openvino-turboquant-ucl.yml`

- [ ] Add tests for distinct `openvino.turboquant/1` handshake, exact custom build/patch identity, requested TBQ4/TBQ4, actual key/value codecs, nonzero dispatch/encoded-record counts, packed bytes for the tested head dimension, measured KV allocation reduction, forced negative, stale/malformed evidence, streaming, stop/cancel, two turns, and cleanup.
- [ ] Run tests against the official worker and observe protocol/build/activation failures.
- [ ] Implement the separate worker over the protected facade and separate verified dependency directory. Activation evidence must be typed numeric/enum fields originating from runtime counters, never parsed console text.
- [ ] Run one pinned IBM Granite model/hash on Intel CPU CPU-SDPA through the CLI. Compare against the exact official OpenVINO model/prompt/generation settings and record fixed-prompt quality rubric, repeatability, context scaling, memory, TTFT, throughput, cancellation, cleanup, and corruption results.
- [ ] Run `Test-OpenVinoTurboQuantActivation.ps1` and expect `turboquant_active` only when every proof field agrees. Disable the patch/codec and expect `turboquant_activation_unverified`.
- [ ] Implement manual UCL workflow with the trusted labels, immutable commit/model/closure checks, sanitized 30-day artifacts, and `always()` cleanup. Do not combine its install root or manifest with official OpenVINO.
- [ ] Commit: `feat(turboquant): add verified experimental CLI worker`

## Task 17: Integrate TurboQuant and Explicit Verified Fallback

**Files:**
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantRouteAdapter.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantActivationState.cs`
- Create: `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantFallbackService.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/TurboQuant/TurboQuantRouteAdapterTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/TurboQuant/TurboQuantFallbackServiceTests.cs`
- Modify with I0/C1 owner: central route/capability registry, shared prompt page evidence presentation, packaging target

- [ ] Add tests proving `Experimental` is visible only for the exact pinned Granite/model hash and approved Intel CPU configuration with verified closure; `Active` requires all Task 16 proof; missing/false/mismatched proof is `Unavailable|Unverified`, never active.
- [ ] Add fallback tests: failure offers user-confirmed official OpenVINO first and applicable GGUF second; no automatic execution, no silent backend/cache switch, and the next session reports its real route rather than retaining TurboQuant branding.
- [ ] Run focused tests and observe missing adapter/fallback failures.
- [ ] Implement the adapter through the same route-neutral prompt surface and lifecycle as GGUF/official OpenVINO. Stream, stop, cancel, reset, stale-event filtering, navigation cleanup, and accessibility use the established templates.
- [ ] Add explicit bounded evidence rows: `Experimental`, requested/actual TBQ4/TBQ4, verified build identity, activation verified/unverified, and matched memory/quality/performance disposition. Do not show raw counters/logs outside the evidence details policy.
- [ ] In the serial I0/C1 commit, register only the proven tuple and package the separate worker/closure under `OpenVino/TurboQuant/Worker`; verify tamper or absence leaves the official worker operational.
- [ ] Run normal WinUI E2E on the UCL Intel laptop for pinned Granite TBQ4 prompting, two turns, reset, stop, cancellation, forced activation-negative, official fallback, and applicable GGUF fallback. This closes `F-M21`, `F-M22`, `N-M11`, and `DR-WF-011` only if evidence passes.
- [ ] Commit O1: `feat(app): integrate verified TurboQuant route`
- [ ] Commit I0/C1 registration: `feat(config): expose proven TBQ4 experimental tuple`

## Task 18: Close Packaging, UX, Security, and Audit Acceptance

**Files:**
- Create: `scripts/openvino/Invoke-OpenVinoReleaseGate.ps1`
- Create: `scripts/openvino/Test-OpenVinoCleanupInventory.ps1`
- Create: `scripts/openvino/Test-OpenVinoArtifactPrivacy.ps1`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ArchitectureBoundaryTests.cs`
- Create: `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/PackagingContractTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Accessibility/OpenVinoRouteAccessibilityTests.cs`
- Create: `docs/evidence/openvino/README.md`
- Modify with I0 owner: generated RTM/evidence catalogue and shared test registration

- [ ] Add architecture tests enforcing allowed project directions and absence of OpenVINO/TurboQuant/native/converter dependencies in presentation/shared route-neutral projects. Enforce separate official/TurboQuant install roots and manifests.
- [ ] Add packaging tests for unpackaged/MSIX x64, exact manifest before load, no ambient DLL resolution, no Python/user packages in official route, no converter credentials/cache, tamper/missing dependency failure, and stable official operation when experimental closure is removed.
- [ ] Add UX/accessibility tests for established shared templates, keyboard/focus, screen-reader names, status announcements without token spam, stop/cancel/reset, bounded recovery copy, Experimental labeling, explicit fallback confirmation, and no terminal requirement.
- [ ] Run the new tests first and observe failures for missing release scripts/evidence catalogue entries.
- [ ] Implement cleanup inventory checks covering worker/converter/TurboQuant processes and descendants, pipes/listeners, file locks, operation staging, compiled cache ownership, and late events after every terminal path.
- [ ] Implement artifact privacy validation over every hosted/UCL JSON/TRX/log/archive; reject absolute paths, usernames/hostnames, environment dumps, prompts, generated text, model/tokenizer bytes, credentials, raw stderr, or untyped native output.
- [ ] Update the I0-owned RTM with direct evidence for all 398 P1 atoms; `MI-SEAM-001..028`; P2/P3 findings; `F-M18`, `F-M20`, `F-M21`, `F-M22`; `N-M02`, `N-M11`; and `DR-WF-008`, `DR-WF-010`, `DR-WF-011`, `DR-WF-013..015`. Link each requirement to test/evidence identity and immutable commit.
- [ ] Run the release gate in this order: dependency/license locks; contracts; static/malformed inspection; official native unit/process; converter isolation/transaction; optimization; TurboQuant conformance/activation; app unit/visual/accessibility; GGUF regressions; Release x64 package; hosted evidence; trusted UCL evidence; privacy; cleanup; traceability.
- [ ] Expected final result: exact MVP, complete stable route, and TurboQuant acceptance criteria all pass; unsupported GPU/NPU/deferred codecs remain hidden or explicitly unsupported; no open gate is represented as completed without its concrete evidence.
- [ ] Stage only planned files, run `git diff --cached --check`, review the full staged diff, and commit: `test(openvino): close route release gates`

## Final Verification Commands

Run from the repository root on the exact candidate commit. Commands may be split by the implementation worker to respect CI timeouts, but none may be omitted:

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --configuration Release --minimum-expected-tests 1
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj --configuration Release --minimum-expected-tests 1
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj --configuration Release --minimum-expected-tests 1
dotnet test tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj --configuration Release --minimum-expected-tests 1
dotnet test tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests.csproj --configuration Release --minimum-expected-tests 1
dotnet test tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests.csproj --configuration Release --minimum-expected-tests 1
dotnet test tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj --configuration Release --filter "FullyQualifiedName~ModelImport|FullyQualifiedName~ModelInspection|FullyQualifiedName~OpenVino" --minimum-expected-tests 1
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/openvino/Test-OpenVinoDependencyLocks.ps1
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/openvino/Test-OpenVinoGenAiFixture.ps1
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/openvino/Invoke-OpenVinoReleaseGate.ps1
dotnet build "IBM Granite with TurboQuant (Intel).slnx" --configuration Release -p:Platform=x64
git status --short
```

Expected outcome: every test command reports `Test Run Successful`, each verifier returns its typed success disposition, the Release x64 solution build succeeds, and `git status --short` contains no implementation residue beyond user-owned pre-existing changes. The trusted UCL workflows must separately show the same immutable commit for official CPU, any exposed GPU tuple, and TurboQuant activation.

## Acceptance Checklist

- [ ] Command-line prompting is the primary backend and normal WinUI prompting requires no terminal interaction.
- [ ] GGUF and OpenVINO share front-end templates, route-neutral lifecycle, and protected-process foundation while retaining format-specific implementation and evidence.
- [ ] Existing OpenVINO package inspection is bounded, mutation-resistant, device-neutral, and emits the exact schema-v2 handoff.
- [ ] Official CPU performs real deterministic one-turn MVP and at least two sequential ChatHistory turns; stop, cancel, reset, timeout, app close, and parent exit clean up the entire process tree.
- [ ] Intel GPU is exposed only for an explicit exact device with requested/actual equality evidence on UCL hardware; NPU remains deferred.
- [ ] Dense Granite Safetensors conversion is sealed/offline, source-preserving, independently validated, smoked, atomically published, and provenance-bound.
- [ ] FP16/INT8/INT4 persistent artifacts and runtime KV/compiled-cache settings remain distinct and individually evidenced.
- [ ] TurboQuant executes one pinned Granite TBQ4/TBQ4 CPU-SDPA configuration in normal WinUI flow with direct activation, packed representation, dispatch, memory, quality, performance, cancellation, and forced-negative proof.
- [ ] TurboQuant remains Experimental and offers explicit verified official OpenVINO or applicable GGUF fallback without silent switching.
- [ ] Hosted and UCL evidence is typed, sanitized, immutable-commit-bound, retained for 30 days, and proves cleanup/model integrity.
- [ ] All security, privacy, accessibility, packaging, regression, architecture-boundary, and requirements-traceability gates pass.
