# Task 8 Report: Existing WinUI OpenVINO MVP Route

## Status and scope

DONE_WITH_CONCERNS, awaiting independent review.

- Base: `875e6c0802e50a095b6be07259f65b2a03226a56` (Task 7 approved).
- O1 implementation commit: `6814e0cf feat(openvino): integrate official route service`.
- I0 integration commit message reserved by the plan:
  `feat(app): register OpenVINO MVP route`.
- Branch/worktree: `feature/openvino-route` in `C:\openvino-o1`.
- Task 8 implements only the official CPU MVP. It adds no GPU, conversion,
  TurboQuant, Task 9 workflow, or hidden fallback.
- The ledger remains `in progress` until an independent review reports no
  Critical or Important findings.

The concrete C1 candidate is `openvino.official.cpu`, requested/actual device
`CPU`, maximum context 4,096 tokens, and default/maximum requested new tokens
128. The real packaged fixture journey uses an internal test-harness-only value
of two new tokens; production/default registration remains 128.

## Existing seams and conflict discovery

The base had no C1 registry and no route-neutral prompt contract. The exact
existing shared seams were:

- `ModelImportPage`, its `IModelSelectionClassifier` /
  `BoundedModelSelectionClassifier`, `ModelInspectionRequested`, and the
  directory-specific `OpenVinoInspectionRequested` event;
- `OnboardingShellPage.StageFrame` and its existing transition into
  `ModelInspectionPage`;
- the single established `ModelInspectionPage`, its model/content/action/outcome
  cards, focus/live-region behavior, and `ModelInspectionServiceComposition`;
- the existing GGUF `ModelInspectionRequest` and protected worker composition.

The concrete integration conflicts found before shared edits were:

1. C1/route-neutral prompting did not exist at the base. Task 8 therefore adds
   the smallest neutral `Features.Prompting` contract/registry, and registers an
   official OpenVINO adapter without inventing a fake GGUF generation backend.
2. The app and solution did not reference the already approved OpenVINO
   contracts/client projects. The first app compile produced 120 missing-type
   errors and established the valid I0 compile gap.
3. The base directory classifier treated the canonical Task 5 three-IR GenAI
   resource set as an ambiguous multi-model directory. The classifier now accepts
   only the exact three matched stems `openvino_model`, `openvino_tokenizer`, and
   `openvino_detokenizer`; arbitrary multiple-pair directories remain rejected.
4. Computing expected build evidence from the mutable packaged manifest would
   have repeated Task 7 self-authentication. The packaging target instead requires
   the caller's pinned digest at build time and embeds it as assembly metadata.
   Runtime hashing only compares the installed manifest to that immutable value.
5. Six broad packaged GGUF/ModelImport tests used nonexistent `C:\Models\*.gguf`
   paths and were rejected by the real bounded classifier before their injected
   scanner ran. This was an invalid test fixture, not a Task 8 product regression.
   A test-only asserting classifier now selects exactly `ModelSelectionRoute.Gguf`;
   production classifier code was not changed for this correction.

## O1 route implementation

`Features.OpenVinoRoute` now owns:

- `OpenVinoRouteStateMachine`, with one operation/session/turn identity, closed
  legal transitions, immutable terminal states, stale-event rejection, and fresh
  reset identities;
- `OpenVinoRouteService`, which performs static inspection, invokes Task 7 native
  inspection, creates schema-v2 handoffs, keeps selected paths in a private
  handoff registry, and exposes only path-free inspection/capability results;
- `OpenVinoRouteSession` and `OpenVinoPromptAdapter`, which map the official
  worker client to ordered route-neutral events, bound prompts/requests, support
  partial STOP followed by same-session reuse, CANCEL/close/disposal, suppress
  stale output, and map failures to fixed path-free recovery text;
- `OpenVinoRouteCapability`, with the single fixed CPU candidate and no GPU or
  quantization surface.

The route-neutral common ground is `PromptRouteKind`, `PromptEventKind`,
`PromptEvent`, `PromptTurnResult`, `PromptRouteCapability`,
`IPromptRouteSession`, `IPromptRouteAdapter`, and `PromptRouteRegistry` under
`Features.Prompting`. Architecture tests register GGUF and OpenVINO kinds and
reflect every public neutral signature to prove that OpenVINO, Llama,
TurboQuant, and quantization runtime types do not leak into the neutral surface.

## I0 app, page, and packaging integration

- ModelImport retains a raw OpenVINO directory internally, leaves
  `SelectedModelPath` null, and carries the raw value only when Continue enters
  the OpenVINO inspection/service boundary. GGUF retains its existing quick-scan
  and `ModelInspectionRequested` route.
- Onboarding sends both formats into the same existing `ModelInspectionPage`.
  No OpenVINO page or second visual system was added.
- The OpenVINO partial uses the established inspection cards/actions and adds a
  prompt card to that same page with shared theme resources, accessible names,
  live status, programmatic focus restoration, bounded Send, STOP, and CANCEL.
- Ready is shown only after static plus official native validation and schema-v2
  handoff. Non-ready outcomes remain on inspection/recovery surfaces.
- The page displays exact `Requested CPU · Running CPU` semantics and immutable
  Runtime/GenAI/Tokenizers/worker-manifest build evidence. No selected path is
  copied into presentation, configuration, hardware, capability, failure, or
  build evidence.
- UI callbacks marshal worker events through `DispatcherQueue`. Navigation
  retires the lifetime, cancels it, awaits active inspection/prompt/STOP/CANCEL
  work through the owned cleanup task, then cancels/disposes the session. A
  failed turn disposes its channel immediately before page retirement. Task 7's
  parent-loss containment remains the process-level app-close backstop.
- Default composition resolves `OpenVino\Official\Worker` from
  `AppContext.BaseDirectory`. The same target marks all 17 entries for output,
  publish, and package paths, covering unpackaged output and MSIX layout.
  Arbitrary worker-root injection remains internal/test-only.

`OpenVino.WorkerPackaging.targets` is fail-closed for x64 builds. It requires
`OpenVinoOfficialWorkerStageDirectory` and a lowercase caller-supplied
`OpenVinoOfficialWorkerManifestSha256`, runs the Task 7 manifest verifier,
independently compares the manifest digest, and then includes the exact closed
17-file inventory under `OpenVino\Official\Worker`. Missing files, changed files,
wrong digest, missing properties, and a tampered then re-manifested closure all
fail before packaging. A normal x64 app build with no supplied stage also fails
by policy; this is expected packaging evidence rather than a product regression.

## TDD evidence

Tests were authored and observed RED before their corresponding production or
shared edits:

| RED group | Nonzero RED evidence | Final focused evidence |
| --- | --- | --- |
| O1 state/adapter | 1/1 sentinel failed to compile because route APIs did not exist; the authored state/adapter group then exercised 12 methods. | State 6 methods and adapter 6 methods are included in the 147/147 suite. |
| Route service | Missing service/session/handoff APIs produced compiler RED before service composition. | `OpenVinoRouteServiceTests`: 2 methods green. |
| Neutral C1 seam | 1/1 failed because `Features.Prompting` and registry types did not exist. | `PromptRouteContractRedTests`: 3 methods green, including reflection no-leak proof and GGUF/OpenVINO registration parity. |
| App references | First shared compile produced 120 missing OpenVINO contract/client type errors. | Release x64 app and packaged test app compile cleanly with the approved references. |
| ModelImport/onboarding | Canonical three-IR directory failed as ambiguous and same-page handoff tests were RED. | `OpenVinoModelImportIntegrationTests`: 3 methods green; broad GGUF/ModelImport/onboarding regressions green. |
| Prompt surface | 2/2 packaged UI tests failed because `PromptSurface` and its controls did not exist. | `OpenVinoPromptSurfaceTests`: 2 methods green with names, visibility, controls, live region, and focus. |
| Packaging | Missing properties, wrong digest, inventory tamper, and re-manifested tamper were captured as negative behavior. | `OpenVinoWorkerPackagingTargetTests`: 5 methods green. |
| Real packaged E2E | Initial test-only compile produced nine missing page/composition APIs. Behavior RED then found the canonical-directory ambiguity, the intentional 128-token fixture context failure, and missing immediate failed-turn cleanup. | Both packaged E2Es pass: exact fixture response `fixture`, STOP/reuse, CANCEL/navigation cleanup, default-limit failure cleanup, and missing/tampered no-launch recovery. |

The new focused files contain 29 test methods: state 6, adapter 6, service 2,
neutral registry 3, packaging 5, ModelImport/onboarding 3, prompt surface 2, and
real packaged E2E 2. The canonical classifier regression adds one further method.

## Final verification matrix

All final verification was run sequentially. `GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE`
pointed to the approved Task 7 stage B, and the process suite also used approved
stage A where its tests require two independent closures.

```powershell
$env:GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE = 'C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-review4-stage-b'
$env:OPENVINO_OFFICIAL_WORKER_STAGE_A = 'C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-review4-stage-a'
$env:OPENVINO_OFFICIAL_WORKER_STAGE_B = 'C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-review4-stage-b'
```

| Gate and exact command | Result |
| --- | --- |
| `dotnet test --project tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj --configuration Release --no-restore` | 147/147 passed in 41.981 s, including all 5 packaging tests. |
| `dotnet test --project tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --configuration Release --no-restore` | 60/60 passed in 42.425 s. |
| `dotnet test --project tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj --configuration Release --no-restore` | 13/13 passed in 4.170 s. |
| `dotnet test --project tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj --configuration Release --no-restore` | First load-sensitive run: 37/39 in 1 m 50.385 s; details below. One approved full rerun: 39/39 in 1 m 14.007 s. |
| `vstest.console.exe "C:\openvino-o1\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe" /Platform:x64 "/TestCaseFilter:ClassName=GraniteEdgeAI.UnitTests.ModelImportPageStateMachineTests\|ClassName=GraniteEdgeAI.UnitTests.ImportModelCardTests" "/Logger:console;Verbosity=minimal"` | Corrected existing fixtures: 16/16 passed in 313 ms. |
| `vstest.console.exe "C:\openvino-o1\tests\UnitTests\GraniteEdgeAI.UnitTests\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\GraniteEdgeAI.UnitTests.build.appxrecipe" /Platform:x64 "/TestCaseFilter:FullyQualifiedName~ModelImport\|FullyQualifiedName~ModelInspection\|FullyQualifiedName~Onboarding\|FullyQualifiedName~Gguf" "/Logger:trx;LogFileName=C:\openvino-o1\TestResults\task8-affected-fixed.trx" "/Logger:console;Verbosity=minimal"` | 662/662 passed in 2 minutes, including both real OpenVINO E2Es and existing GGUF/ModelImport/inspection/onboarding coverage. |

The first process run's two timing results were:

- `ProtocolContainmentTests.CallerCancellationOfActiveTurnCancelsOwningSession`
  received `RuntimeTimedOut` during `StartSession`;
- the hostile `child-escape-attempt` case expected `RuntimeProtocolFailed` but
  received `RuntimeTimedOut`.

The worker and test-host residue count was zero after that run. Per the review
ruling, no deadlines or Task 7 source were changed. The exact caller-cancellation
test passed 1/1 in 5.346 s when isolated, and the one permitted full-suite rerun
passed 39/39. This is retained as load/timing evidence, not hidden. The documented
legacy delayed-handshake test was not rerun and no legacy timeout was weakened.

The broad packaged UI run initially exposed six deterministic invalid-fixture
failures and passed 656/662. After the test-only GGUF classifier correction, the
two affected classes passed 16/16 and the one broad rerun passed 662/662. The
production GGUF classifier path was not altered by that correction.

## Release x64 package evidence

An operation-owned final stage was copied to
`C:\Users\Arian\AppData\Local\Temp\granite-o1-task8-app-stage-final` from approved
Task 7 stage B, verified, used once, and deleted after the build. Before use it had:

- `worker_manifest_valid`;
- exactly 17 closure files;
- manifest SHA-256
  `0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe`;
- worker SHA-256
  `51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94`.

The final build command was:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' `
  'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' `
  -restore -target:Build -maxCpuCount -verbosity:minimal `
  -property:Configuration=Release -property:Platform=x64 `
  -property:RuntimeIdentifier=win-x64 -property:PublishProfile= `
  -property:PublishTrimmed=false -property:PublishReadyToRun=false `
  -property:AppxPackageSigningEnabled=false `
  -property:GenerateAppxPackageOnBuild=false `
  -property:OpenVinoOfficialWorkerStageDirectory='C:\Users\Arian\AppData\Local\Temp\granite-o1-task8-app-stage-final' `
  -property:OpenVinoOfficialWorkerManifestSha256=0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe
```

MSBuild 18.7.8 exited 0 after printing both `worker_manifest_valid` and
`worker_manifest_digest_valid`. The Release x64 app output contains the exact
17 files with the same manifest and worker hashes. Both generated Release x64
`.build.appxrecipe` files contain 17 `OpenVino\Official\Worker` entries. The
compiled app DLL contains the exact pinned manifest digest string; packaged E2E
default composition also resolved it successfully, proving that build metadata
was carried into the running app.

The immutable evidence displayed by the app is:

- Runtime `2026.3.0-22451-8a17657b995-releases/2026/3`;
- GenAI `2026.3.0.0-3277-bd8d6542e3c`;
- Tokenizers `2026.3.0.0-703-183c6f25cda`;
- the pinned manifest digest above.

## Independent verifier and audit evidence

- `Test-OpenVinoOfficialWorkerManifest.ps1 -StageDirectory <final-app-output-worker>`:
  `worker_manifest_valid`, exit 0.
- `Test-OpenVinoDependencyLocks.ps1 -ClosureDirectory C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-official-archives -Scope Official`:
  `dependency_lock_valid`, exit 0.
- `Test-OpenVinoGenAiFixture.ps1 -FixtureRoot tests\TestFixtures\OpenVINO\GenAI\TinySyntheticV1`:
  `fixture_valid`, exit 0.
- Process/temp audit after all tests/build/cleanup: zero OpenVINO worker
  processes and zero `granite-o1-task8*` temp entries.
- Tracked-artifact audit: 4,572 tracked files, zero tracked ZIP, wheel, DLL,
  executable, PDB, LIB, or OBJ artifacts. No operation-owned build/stage path is
  tracked; the one textual name match is the legitimate source script
  `scripts/openvino/Build-OpenVinoOfficialWorker.ps1`.
- `git diff --check`: exit 0.
- Worker launch remains `CreateNoWindow`, and only the fixed protocol selector is
  a worker CLI argument. Selected package paths stay on protected stdin; no
  secret or selected path is placed in arguments or visible terminals.

Two diagnostic invocations are explicitly excluded from green evidence:

1. A combined shell command invoked the dependency-lock verifier on the wrong
   closure kind (the packaged 17-file runtime rather than the three official
   archives) and then masked its nonzero exit by running a later command. The
   isolated correct archive invocation above is authoritative.
2. Windows PowerShell/.NET Framework could not reflect the .NET 8 app assembly
   because it could not resolve `System.Runtime, Version=8.0.0.0`. The generated
   source/package tests, binary digest-presence check, and real packaged default
   composition provide the accepted embedded-pin evidence.

## Self-review and remaining concerns

- **Path lifetime/privacy:** the absolute model directory exists only in the
  internal ModelImport event, page inspection lifetime, private service registry,
  and protected worker commands. It is cleared on retirement and absent from all
  presentation/configuration/hardware/capability/build records and recovery copy.
- **Trust boundary:** the caller supplies and pins the manifest digest at build;
  the app never derives expected evidence from the mutable packaged manifest.
  Missing, tampered, and tampered/re-manifested closures fail before launch.
- **Resolution:** `AppContext.BaseDirectory` plus output/publish/package metadata
  yields the same relative closed worker root for unpackaged x64 and MSIX.
- **Threading/lifetime:** worker callbacks marshal to the UI dispatcher;
  navigation owns cancellation plus an awaitable cleanup task; failed turns retire
  immediately; STOP retains a ready reusable session; CANCEL/close/dispose are
  terminal and late events are identity-filtered. Process-level parent-loss
  containment covers abrupt app close.
- **Shared UI:** there is one `ModelInspectionPage`; existing cards, actions,
  focus, accessibility, High Contrast, scaling, and reduced-motion coverage remain
  in the 662-test affected suite.
- **Concern retained:** two first-run process outcomes changed to the same bounded
  timeout only under full-suite load. Isolation and the one approved rerun were
  green, no residue remained, and no deadline was changed. Independent review
  should evaluate this timing evidence.
- **Concern retained:** direct assembly reflection was unavailable from the local
  Windows PowerShell host; runtime packaged composition and binary/source evidence
  cover the pin, but an independent reviewer may prefer a net8 metadata-reader
  probe.

Task 8 intentionally remains in progress pending that review.

## Independent review fix round 1/5

The round-1 findings were verified against the exact production paths before
editing. Work remained inside Task 8: no GPU, conversion, TurboQuant, Task 9,
protocol, Task 7 native, or deadline changes were made.

### Focused RED/GREEN evidence

- **Production picker:** the first authored picker/E2E changes produced two
  `CS1739` errors for the absent `pickOpenVinoPathAsync` seam. The page now
  routes the OpenVINO selection through `OpenVINOFolderPicker`, normalizes the
  folder, and submits it through the ordinary classifier path. The focused
  picker class passed 9/9 and the real packaged E2E enters through Browse and
  Continue rather than `SubmitInputAsync`.
- **Fail-safe cancellation:** two parameterized cancellation tests failed 2/2
  because `runtime_timed_out` and `runtime_protocol_failed` escaped. One
  idempotent terminal teardown now maps every cancellation outcome, terminalizes
  the route, and disposes the channel in `finally`; the cancellation/close
  focus passed 4/4. The native E2E cancels an active turn, observes no late
  output, disables every terminal prompt action, and leaves zero worker residue.
- **Revocable raw-path lifetime:** the lease tests first produced four missing-
  API compiler errors and the import test failed 1/1 because a transferred
  directory remained reusable. Ready handoffs are now disposable, single-use
  leases; the service retains no path registry, stale/abandoned leases revoke
  their descriptor, the page disposes every unconsumed lease, Continue clears
  import state before raising the request, and navigation clears an untransferred
  selection. Service tests passed 3/3, the original import/onboarding focus
  passed 17/17, and the added navigation-away lifetime test passed 1/1.
- **Route-neutral activation/presentation:** the first contract test produced
  four missing-type compiler errors; official activation then produced the
  expected lease-to-neutral-activation compiler error; the XAML contract failed
  0/1 on the OpenVINO-named evidence controls. `PromptRouteRegistry` now
  activates `IPromptRouteSession` through `IPromptRouteActivation`, returns
  route-neutral presentation data, and the shared page uses
  `PromptSessionPresenter` for events, response, actions, support codes, and
  evidence. A test-only GGUF adapter drives the same presenter without adding a
  GGUF runtime. Combined neutral/service tests passed 10/10; prompt XAML tests
  passed 2/2. A source contract also failed before and passed after removal of
  the arbitrary internal verified-root factory seam.
- **Async transition/app-close ownership:** the first two tests produced seven
  missing-API compiler errors, then passed 2/2 after the shell and main window
  gained awaitable retirement owners. The first broad affected run exposed a
  valid existing regression: a cancelled Frame navigation had already retired
  its inspection page. Navigation now validates the Frame transition first and
  awaits all retirement before publishing the next stage/subscription owner;
  cancelled navigation leaves the original page live. The two new owner tests
  plus the existing cancelled-navigation regression passed 3/3.
- **Packaged end-to-end closure:** the strengthened canonical method sends a
  successful real turn after STOP, cancels a subsequent active native turn with
  no late output, and returns through the shell-owned transition. Missing and
  tampered installed closure variants enter the same default production
  composition, never use an injected worker root, show fixed path-free recovery
  codes, and launch no worker. The first combined run passed canonical but used
  an over-specific negative assertion; after correcting that test-only
  classifier to the controlled inspection diagnostic surface, the exact class
  passed 2/2 in 19 seconds.

### Fix-round verification

| Gate | Result |
|---|---|
| `GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE=<approved stage B>; dotnet test --project tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj --configuration Release --no-restore` | 155/155 passed in 34.965 s, including all five packaging tests. |
| OpenVINO contract suite | 60/60 passed in 34.114 s. |
| OpenVINO worker-client suite | 13/13 passed in 3.591 s. |
| Official process suite with approved stage A+B | 39/39 passed in 1 m 03.947 s; the legacy delayed-handshake test was not invoked. |
| Exact packaged OpenVINO E2E class | 2/2 passed in 19 s. |
| ModelImport/onboarding/GGUF affected partition | 165/165 passed. |
| Model Inspection controls partition | 78/78 passed. |
| Navigation/layout/prompt/native-E2E partition | 43/43 passed. |
| Model Inspection visual partition | 32/32 passed. |
| Presentation/runtime/service/view-model/contracts partition | 330/330 passed. |
| Classifier/composition partition | 18/18 passed. |

The first monolithic affected run retained its two diagnostics: the valid
cancelled-navigation regression above and one downstream closed render-window
error; after six minutes it was terminated and its exact orphaned test process
was removed. After the fix, a second monolithic host passed 531 tests with zero
failures and then lost communication; a ModelInspection-only host similarly
passed 384 with zero failures before losing communication. No process survived
either abort. Clean smaller packaged hosts cover the affected filters above and
all terminate green; the infrastructure aborts are retained, not counted as
green evidence.

The final Release x64 app build used an operation-owned copy of approved Task 7
stage B with exactly 17 files, manifest SHA-256
`0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe`,
and worker SHA-256
`51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94`.
MSBuild exited 0 after `worker_manifest_valid` and
`worker_manifest_digest_valid`. The output contains the same 17 files and
hashes, its appxrecipe contains 17 worker entries, and the app DLL contains the
caller-pinned manifest digest. Independent manifest, official dependency-lock,
and GenAI fixture verifiers each exited 0.

The final audit found zero OpenVINO worker or `C:\openvino-o1` test/build
processes, 4,584 tracked files, zero tracked ZIP/wheel/DLL/EXE/PDB/LIB/OBJ
artifacts, and `git diff --check` exited 0. The only `granite-o1-task8*` temp
entry is the policy-retained verified stage documented below.

### Round-1 self-review and retained concern

- The raw directory now exists only from ModelImport through the private
  OpenVINO inspection/lease/session boundary. It is revoked on transfer,
  selection reset, navigation, stale result, non-ready result, lease abandonment,
  and session activation; presentation, capability, hardware, build evidence,
  support copy, and neutral contracts contain no path.
- The expected manifest digest remains caller-pinned build metadata. Runtime
  composition hashes the installed manifest only for comparison with that pin;
  no mutable closure can self-authenticate. Both unpackaged/MSIX resolution keep
  the same fixed relative 17-file root and no arbitrary verified-root API exists.
- The shared controller consumes only neutral sessions/events/presentation.
  OpenVINO handoff plumbing remains private to inspection, the GGUF test adapter
  is test-only, and there is still exactly one `ModelInspectionPage`.
- Cancellation is one idempotent teardown; timeout, protocol failure, ordinary
  cancellation, navigation, shutdown, close, and disposal all terminalize and
  dispose. UI updates marshal through the dispatcher and late events are
  lifetime-filtered.
- **Retained operation-owned temp residue:** policy rejected two native
  `Remove-Item -LiteralPath ... -Recurse` attempts before process creation even
  after both the implementer and parent independently resolved the exact target.
  The verified copy remains at
  `C:\Users\Arian\AppData\Local\Temp\granite-o1-task8-fix-round1-final`.
  No alternate shell or policy bypass was used. This is not tracked or consumed
  by the app build after verification, but independent review should remove it
  when policy permits.

Task 8 remains in progress after fix round 1/5 awaiting scoped re-review.

## Independent review fix round 2/5

The three open findings were verified against the exact implementation before
production edits. Focused RED/GREEN evidence was:

- **Observer-independent cancellation teardown:**
  `dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release --filter FullyQualifiedName~OpenVinoPromptAdapterTests.Throwing --no-restore`
  failed 0/4 because exceptions from both `CancellingSession` and terminal
  observers escaped. Publication is now contained, state terminalization occurs
  independently, and exactly-once channel disposal completes before terminal
  publication. The complete adapter class passed 13/13, including throwing
  initial/terminal observers, timeout and protocol cancellation failures,
  repeated Cancel/Dispose, and the active native cancellation race.
- **One shell-owned navigation transaction:** the new external slow-retirement
  test initially failed 1/2 with retirement count 2. The page now preserves one
  OpenVINO cleanup task, navigation and app-close share the shell's current
  transaction, and the frame plus fresh import page remain inert until
  subscription/stage ownership commits. Cancelled Frame navigation stays live,
  enabled, subscribed, and performs zero retirement. The focused pair passed
  2/2. A broader navigation run then correctly exposed two synchronous page
  lifecycle regressions (41/43); restoring synchronous non-OpenVINO retirement
  while retaining the one cached OpenVINO task produced 64/64 across page
  navigation, onboarding ownership, OpenVINO import/prompt surface, and both
  packaged native E2Es.
- **Deterministic active-CANCEL ownership:** the route-neutral presenter test
  first failed compilation with eight missing-member errors. It now exposes
  `ActiveTurnId`, `LastEventKind`, `EventRevision`, and `CompletedTurnCount` and
  the neutral contract class passed 8/8. The strengthened native E2E first
  passed 1/2 and reported exact terminal `Failed`: the active worker's
  `operation_cancelled` outcome raced through the adapter's generic failure
  mapping. A focused unit reproduction failed 0/1 because generation returned a
  failed result instead of throwing cancellation. The adapter now maps that
  outcome to exactly one cancelled terminal without a failed event. The exact
  packaged E2E class then passed 2/2 in 11 seconds: it observed a nonempty active
  turn ID before Cancel, exact `Cancelled`, no `TurnCompleted`, no turn result,
  no late text/event revision, disabled terminal actions, post-STOP reuse, and
  zero worker residue. The missing/tampered default-composition journey remained
  green and launched no worker.

### Fix-round 2 verification

| Gate | Result |
|---|---|
| Approved-stage OpenVINO route/unit/packaging suite | 161/161 passed in 32.420 s. |
| OpenVINO contract suite | 60/60 passed in 33.607 s. |
| OpenVINO worker-client suite | 13/13 passed in 3.362 s. |
| Official process suite with approved Task 7 stages A+B | 39/39 passed in 1 m 05.433 s; the unrelated legacy delayed-handshake suite was not invoked. |
| Exact packaged OpenVINO E2E class | 2/2 passed in 11 s. |
| Affected packaged page-navigation/onboarding/OpenVINO import/prompt/native partition | 64/64 passed in 29 s. |
| Release x64 app build with caller-pinned stage/digest | MSBuild exited 0 after `worker_manifest_valid` and `worker_manifest_digest_valid`. |
| Independent closure verifiers | Manifest `worker_manifest_valid`; official dependency locks `dependency_lock_valid`; canonical GenAI fixture `fixture_valid`. |

The final app output contains exactly 17 official worker files, manifest SHA-256
`0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe`,
worker SHA-256
`51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94`,
17 appxrecipe entries, and the compiled app DLL contains the caller-pinned
manifest digest. The final audit found zero OpenVINO worker or repository
test/build processes, 4,585 tracked files, zero tracked binary/archive/build
artifacts, and `git diff --check` exited 0.

Round-1 closures remain intact: the production picker and revocable raw-path
lease were not broadened; no path entered presenter state, evidence, capability,
or diagnostics; the registry/presenter remain route-neutral; default composition
still resolves only the fixed installed worker root against the caller-pinned
digest; there is one `ModelInspectionPage`; default/max generation remains 128
and only the packaged harness requests 32. The policy-retained verified stage at
`C:\Users\Arian\AppData\Local\Temp\granite-o1-task8-fix-round1-final`
remains unchanged and was not removed through another shell.

Task 8 remains in progress after fix round 2/5 awaiting scoped re-review.

## Independent review fix round 3/5

All three findings were confirmed against the round-2 code before production
edits. The focused RED/GREEN sequence was:

- **Concurrent teardown join:** the new slow-disposal test was authored with
  the worker-confirmation test before implementation; the first focused command
  failed compilation because the required confirmation-aware channel member did
  not exist. The completed adapter test holds channel disposal behind a
  controllable gate, starts concurrent Cancel and Dispose, and proves both tasks
  remain incomplete until release, both then complete, the throwing terminal
  observer does not escape, terminal state/event occurs once, and underlying
  cancellation/disposal counts are exactly one. All adapter plus neutral
  presenter tests passed 24/24.
- **Worker-confirmed active ownership:** the managed conversation now forwards
  the validated real `generationStarted` protocol event; the route adapter
  correlates its session and turn IDs before publishing route-neutral
  `GenerationConfirmed`. Local pre-call `GeneratingTurn` keeps Stop/Cancel
  disabled and owns no active turn. A second test-first compile produced two
  missing `CancelActiveTurnAsync` errors. The neutral session API now carries the
  worker-confirmed turn ID; the official adapter atomically validates that ID
  against its confirmed active snapshot before beginning cancellation. The
  focused test rejects a wrong ID before channel contact and accepts the exact
  ID with one cancel/dispose. The packaged native E2E waits for this real
  confirmation for both STOP and CANCEL, then retains post-STOP reuse, exact
  cancelled terminal, no TurnCompleted/result, no late text/revision, default
  missing/tampered composition, and zero residue.
- **Retirement-failure recovery:** the injected slow then throwing retirement
  test failed 0/1 because the exception escaped after Frame navigation. The
  shell now catches only retirement failure after the old page has left the
  Frame, commits the visible fresh import page as the controlled recovery owner,
  restores frame/page interaction, aligns subscriptions and stage, and returns
  false. The test proves the recovery page can immediately raise the next real
  inspection request and app-close joins the already completed owner without a
  second retirement. It passed with the exact two native E2Es, 3/3.

The first packaged run after introducing confirmation passed the navigation
recovery but failed canonical STOP because the old test invoked STOP before the
worker acknowledgment and the controller correctly kept it disabled. That
assertion aborted its journey; the following negative test then failed its
zero-worker precondition. No process remained by the cleanup audit. After STOP
was changed to await the same real acknowledgment, the exact three methods
passed 3/3 in 11 seconds.

### Fix-round 3 verification

| Gate | Result |
|---|---|
| Approved-stage OpenVINO route/unit/packaging suite | 164/164 passed in 30.898 s. |
| OpenVINO contract suite | 60/60 passed in 32.852 s. |
| OpenVINO worker-client suite | 13/13 passed in 3.991 s. |
| Official process suite with approved Task 7 stages A+B | 39/39 passed in 1 m 04.087 s; the unrelated legacy delayed-handshake suite was not invoked. |
| Exact recovery plus packaged native E2Es | 3/3 passed in 11 s. |
| Affected packaged navigation/onboarding/OpenVINO import/prompt/native partition | 65/65 passed in 25 s. |
| Release x64 app build with caller-pinned stage/digest | MSBuild exited 0 after `worker_manifest_valid` and `worker_manifest_digest_valid`. |
| Independent closure verifiers | Manifest `worker_manifest_valid`; official dependency locks `dependency_lock_valid`; canonical GenAI fixture `fixture_valid`. |

The Release output again contains exactly 17 official worker files, 17 recipe
entries, manifest SHA-256
`0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe`,
worker SHA-256
`51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94`,
and the caller-pinned digest in the app DLL. Round-1/2 closures remain intact:
no raw path entered the neutral acknowledgment/session/presenter, evidence,
capability, or diagnostics; no public arbitrary worker root exists; default/max
tokens remain 128; one shared `ModelInspectionPage` remains; and no Task 9,
GPU, conversion, or TurboQuant work was performed. The policy-retained stage
`C:\Users\Arian\AppData\Local\Temp\granite-o1-task8-fix-round1-final`
remains untouched.

The final audit found zero OpenVINO worker or repository test/build processes,
4,585 tracked files, zero tracked binary/archive/build artifacts, and
`git diff --check` exited 0.

Task 8 remains in progress after fix round 3/5 awaiting scoped re-review.

## Independent review fix round 4/5

The three remaining findings were verified against exact head
`02566af84025a8042df57c78013825de4a874781` before production edits. The
focused test-first RED/GREEN sequence was:

- **Reentrant teardown publication:**
  `GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE=<approved stage B>; dotnet test
  --project tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj
  --configuration Release
  --filter FullyQualifiedName~CancellingObserverReentrantDisposeJoinsPublishedSlowTeardown
  --no-restore` failed 0/1: reentrant Dispose completed while the authoritative
  slow channel disposal was still blocked. Cancel/Dispose and channel disposal
  now publish `RunContinuationsAsynchronously` completion placeholders under
  the teardown lock before starting cancellation, observer publication, or
  underlying disposal. The same command passed 1/1; both returned operations
  remained incomplete until release, then completed successfully with one
  cancelling event, one cancelled event, one cancel, and one channel disposal.
  Presentation observers still run outside the teardown lock and cannot escape.
- **Exact-turn terminal arbitration and STOP:** the pre-ack STOP regression
  first failed 0/1 because STOP reached the channel from a merely local turn.
  The exact-turn STOP test then failed compilation with three missing API
  errors, and the terminal-owner state tests failed compilation with fourteen
  missing owner/gate errors. A single state-machine gate now records the real,
  correlated `GenerationStarted` turn and atomically arbitrates confirmed STOP,
  confirmed active cancellation, prompt completion, and prompt failure. Wrong,
  stale, or unconfirmed STOP/CANCEL IDs fail before channel contact. The
  cancellation-first success/failure boundary tests suppress late token,
  `Failed`, and `TurnCompleted` publication and map generation to cancellation;
  the prompt-first test reenters exact active cancellation from the terminal
  observer after the atomic prompt claim and rejects it before channel contact.
  The focused adapter/state partition passed 30/30.
- **Reentrant navigation transaction publication:** the exact two-test packaged
  command initially failed 0/2. A `Frame.Navigating` reentrant Return observed
  an old completed task, and retirement-callback reentry created another async
  task box. The shell now publishes one cached completion placeholder and
  `CurrentNavigationTask` under a private navigation lock before `Frame.Navigate`
  or retirement can call application code. Reentrant/concurrent Return calls and
  shutdown join that task; one destination, navigation, and retirement occur.
  Slow success remains inert until commit. Slow throwing retirement returns
  controlled false, leaves the visible import page interactive and subscribed,
  and the wrapper settles the published transaction even if recovery itself
  encounters an unexpected exception. The exact pair passed 2/2; completed
  transactions are cleared so the existing cancelled-navigation retry remains a
  legitimate new transaction.

### Fix-round 4 verification

The approved stage variables remained:

```powershell
$env:GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE = 'C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-review4-stage-b'
$env:OPENVINO_OFFICIAL_WORKER_STAGE_A = 'C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-review4-stage-a'
$env:OPENVINO_OFFICIAL_WORKER_STAGE_B = 'C:\Users\Arian\AppData\Local\Temp\granite-o1-task7-review4-stage-b'
```

| Gate and exact command | Result |
| --- | --- |
| `dotnet test --project tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj --configuration Release --no-restore` | 172/172 passed in 29.515 s, including all packaging tests and 30/30 adapter/state arbitration tests. |
| `dotnet test --project tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj --configuration Release --no-restore` | 60/60 passed in 32.987 s. |
| `dotnet test --project tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj --configuration Release --no-restore` | 13/13 passed in 3.462 s. |
| `dotnet test --project tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj --configuration Release --no-restore` with approved stages A+B | 39/39 passed in 1 m 06.266 s; the unrelated legacy delayed-handshake suite was not invoked. |
| `vstest.console.exe <packaged appxrecipe> /Platform:x64 "/TestCaseFilter:ClassName=GraniteEdgeAI.UnitTests.Features.ModelInspection.OpenVinoPackagedUiEndToEndTests\|FullyQualifiedName~ReentrantConcurrentReturnAndShutdownJoinOnePublishedNavigationTransaction\|FullyQualifiedName~ThrowingRetirementCommitsInteractiveOwnedRecoveryDestination" "/Logger:console;Verbosity=minimal"` | Exact recovery plus native E2Es: 4/4 passed in 10 s. The real STOP and CANCEL paths wait for the correlated worker acknowledgment; reuse, cancelled-without-completion, no-late-revision, default negative composition, and residue assertions remain green. |
| `vstest.console.exe <packaged appxrecipe> /Platform:x64 "/TestCaseFilter:FullyQualifiedName~ModelInspectionPageNavigationTests\|FullyQualifiedName~OnboardingModelInspectionNavigationTests\|FullyQualifiedName~OpenVinoModelImportIntegrationTests\|FullyQualifiedName~OpenVinoPromptSurfaceTests\|FullyQualifiedName~OpenVinoPackagedUiEndToEndTests" "/Logger:console;Verbosity=minimal"` | Affected packaged navigation/onboarding/OpenVINO import/prompt/native partition: 66/66 passed in 23 s. |
| Release x64 app `MSBuild.exe` with `Configuration=Release`, `Platform=x64`, `RuntimeIdentifier=win-x64`, approved stage B, and caller-pinned manifest digest | Exit 0 after `worker_manifest_valid` and `worker_manifest_digest_valid`. |
| Independent manifest, official dependency-lock, and canonical GenAI fixture verifiers | Exit 0 with `worker_manifest_valid`, `dependency_lock_valid`, and `fixture_valid`. |

The final Release output contains exactly 17 official worker files. Its manifest
SHA-256 is
`0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe` and
worker SHA-256 is
`51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94`.
Both Release x64 app recipes contain exactly 17
`OpenVino\Official\Worker` entries, and the compiled app DLL contains the exact
caller-pinned manifest digest.

The final audit found zero OpenVINO worker processes, zero repository test/build
processes, 4,585 tracked files, zero tracked ZIP/wheel/DLL/EXE/PDB/LIB/OBJ
artifacts, and `git diff --check` exited 0. Prior closures remain intact: the
production picker and revocable raw-path lease were not broadened; registry,
presenter, capability/evidence, and diagnostics remain route-neutral/path-free;
the fixed installed 17-file root and caller pin remain the only production
composition; there is one `ModelInspectionPage`; default/max requested tokens
remain 128 and the canonical packaged smoke remains at most 32; no Task 9, GPU,
conversion, or TurboQuant work was performed.

The policy-retained directory
`C:\Users\Arian\AppData\Local\Temp\granite-o1-task8-fix-round1-final` is still
the sole `granite-o1-task8*` temp entry. It remains untouched; no alternate shell
or cleanup-policy bypass was used.

Task 8 remains in progress after fix round 4/5 awaiting scoped re-review.

## Independent review fix round 5/5

The two final findings were verified against exact clean head
`67890505cd7bfc2e6162f2fc78837c570bd55e2c` before production edits. Scope
remained within Task 8: no Task 9, GPU, conversion, TurboQuant, native worker,
protocol-version, product-limit, or deadline work was added.

### Root causes and focused RED

- **Exact worker failure identity and STOP ownership:** `TurnFailedEvent` was
  the only returned turn terminal that bypassed adapter session/turn identity
  checks. Separately, route state entered `StoppingTurn` and then awaited an
  unscoped channel STOP. Prompt completion could therefore move the route to
  `SessionReady` while the STOP write remained paused, allowing a subsequent
  turn to begin before that write. The focused adapter command covering a wrong
  session, a stale prior turn, and the paused STOP race failed 0/3: both wrong
  failures surfaced the attacker-selected `runtime_load_failed`, and the first
  generation completed before paused STOP dispatch. The worker-client API test
  failed 0/1 because `StopAsync` exposed only `CancellationToken`; the focused
  process build then failed with the expected missing exact-turn overload
  errors.
- **Shutdown/navigation publication:** `ShutdownAsync` read the current
  navigation task under the Return lock but published no shutdown owner or
  flag. A Return reentered from retirement after that snapshot and could publish
  another navigation transaction. The first diagnostic deliberately reentered
  shutdown at the same pre-publication boundary and caused the packaged test
  host to abort through recursive retirement; it is retained as diagnostic, not
  green evidence. The revised barrier test cancelled any attempted Frame
  navigation so it produced a clean 0/1 RED: the throwing retirement escaped
  `ShutdownAsync`, while attempted Return publication and distinct task
  ownership remained observable.

### Fixes and focused GREEN

- `IOpenVinoPromptChannel.StopAsync` and the public protected
  `OpenVinoConversation.StopAsync` now require the expected worker-confirmed
  turn ID. The conversation validates that ID against its protected active turn
  before constructing or writing `StopTurnCommand`; an exact pre-confirmation
  call is an inactionable no-op, no active turn is an idempotent no-op, and a
  different active ID fails closed as `runtime_protocol_failed` before worker
  contact. A new real process scenario stops turn one, starts and confirms turn
  two, rejects a stale turn-one STOP, proves turn two remains active, and then
  stops turn two with its exact ID.
- The adapter now uses an async turn-terminal operation gate. Confirmed STOP
  owns that gate from state claim through channel dispatch; prompt completion,
  prompt failure, and prompt cancellation resolve through the same gate. Route
  state therefore remains `StoppingTurn` while dispatch is paused, so no next
  prompt can start and the prior prompt cannot release ownership early. No
  external operation is awaited under a monitor lock. The neutral unscoped
  convenience method still derives the current confirmed ID and immediately
  enters this exact-ID path.
- Every worker turn event used by the adapter validates exact identity before
  effects: `GenerationStartedEvent`, `TokenEvent`, `TurnCompletedEvent`, and now
  `TurnFailedEvent`. A wrong/stale failure cannot supply the current failure
  code; it fails closed as the fixed path-free protocol taxonomy. Once one
  terminal owner settles, late fragments and competing failure/completion paths
  cannot publish another terminal event.
- The shell now has one lifecycle lock for Return and shutdown publication.
  `ShutdownAsync` publishes one asynchronously continued completion task and
  shutdown flag before reading any active navigation or calling application
  code. Reentrant/concurrent shutdown callers receive that same task. Return
  calls continue joining an already-published active navigation task, including
  after shutdown publication; with no active transaction, all later Returns
  receive one shared `false` task and cannot call `Frame.Navigate`.
- Shutdown first joins any active navigation, then snapshots and retires the
  exact inspection page still owned, once. Retirement failure is contained,
  subscriptions are detached, and the visible content plus Frame remain
  disabled/non-hit-testable for close. Active navigation retains its original
  result semantics while its destination is kept inert after shutdown. The
  deterministic tests cover reentrant/concurrent Return, an already-active
  transaction, a throwing retirement, one destination, one retirement, exact
  task identity/results, stale subscription rejection, and no deadlock.

Focused GREEN evidence was:

| Gate | Result |
| --- | --- |
| Wrong/stale failure plus paused exact-STOP race | 3/3 passed. The race uses synchronous continuation barriers rather than a timing sleep. |
| Complete adapter/state arbitration partition | 33/33 passed. |
| Exact worker-client STOP API contract | 1/1 passed. |
| Protected stale-STOP process scenario | 1/1 passed in 4.663 s. |
| Shutdown/navigation lifecycle focus, including throwing retirement | 5/5 passed in 1 s. |

### Fix-round 5 verification

All final verification commands were run sequentially with approved Task 7
stage B for route/build/package tests and approved stages A+B for process tests.

| Gate | Result |
| --- | --- |
| `dotnet test --project tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj --configuration Release --no-restore` | Fresh final run: 175/175 passed in 29.279 s, including packaging and 33/33 adapter/state tests. |
| OpenVINO contract suite | 60/60 passed in 31.502 s. |
| OpenVINO worker-client suite | 13/13 passed in 2.096 s. |
| Official protected-process suite with approved Task 7 stages A+B | 40/40 passed in 1 m 02.935 s, including the new exact stale-STOP scenario; the unrelated legacy delayed-handshake suite was not invoked. |
| Packaged recovery/native E2E plus the three lifecycle race/recovery methods | 5/5 passed in 11 s. Canonical STOP/reuse, exact CANCEL, no late revision, negative default composition, and residue checks remained green. |
| Affected packaged Model Inspection navigation/onboarding/OpenVINO import/prompt/native partition | 67/67 passed in 23 s. |
| Standalone Release x64 app build with approved stage B and caller-pinned manifest digest | MSBuild 18.7.8 exited 0 after `worker_manifest_valid` and `worker_manifest_digest_valid`. |
| Independent installed-manifest, official dependency-lock, and canonical GenAI fixture verifiers | Exit 0 with `worker_manifest_valid`, `dependency_lock_valid`, and `fixture_valid`. |

The Release output contains exactly 17 official worker files. Manifest SHA-256
is `0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe`;
worker SHA-256 is
`51f5c5579251b2d9a81bb1c743acdc3f361abd6e297782701d04ea4691311c94`.
Both Release x64 app recipes contain exactly 17
`OpenVino\Official\Worker` package entries, and the compiled app DLL contains
the exact caller-pinned manifest digest.

The final audit found zero OpenVINO/protocol-fixture worker processes, zero
repository test/build processes, 4,585 tracked files, zero tracked
ZIP/wheel/DLL/EXE/PDB/LIB/OBJ artifacts, and `git diff --check` exited 0. Prior
closures remain intact: the production picker and revocable raw-path lease were
not broadened; registry, presenter, capability/evidence, and diagnostics remain
route-neutral/path-free; the fixed installed 17-file root and caller pin remain
the only production composition; there is one `ModelInspectionPage`;
default/max requested tokens remain 128 and the canonical packaged smoke remains
at most 32; no Task 9, GPU, conversion, or TurboQuant work was performed.

The policy-retained directory
`C:\Users\Arian\AppData\Local\Temp\granite-o1-task8-fix-round1-final` remains
the sole `granite-o1-task8*` temp entry. It was not touched, consumed, or removed
through another shell. No functional concern remains from this fix round, but
Task 8 stays in progress after the final round awaiting scoped re-review.
