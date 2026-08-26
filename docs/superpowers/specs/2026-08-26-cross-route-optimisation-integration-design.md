# Cross-route optimisation integration design

**Date:** 2026-08-26

**Status:** User-approved implementation design; corrected design/plan package under final three-pass consistency review

**Authoritative feature base:** `fix/hardware-inspection-loq-baseline` at
`85e889fa18f73cc19780b2af03598d1b76ac0e21`

**Implementation start:** the exact later planning handoff commit supplied as
`GEAI_EXPECTED_PLAN_COMMIT`; it must descend from feature base and planning-base
commit `092589c38981ad86bb73c7c97dff01ab8b5a6c8e` and contain this final design,
the execution plan, and the six byte-pinned visual oracles together.

## 1. Purpose

Complete the user journey from the existing model import, model inspection,
hardware inspection, and model/hardware compatibility decision through:

1. the choice to use an already compatible model or optimise it first;
2. mandatory optimisation when only a lower-memory admitted configuration fits;
3. optimisation preference selection and confirmation;
4. GGUF or OpenVINO execution behind one shared experience;
5. progress, cancellation, failure, replan, and success states; and
6. a truthful final choice to Chat or save the produced model/package.

The integration must preserve the completed hardware-aware compatibility work,
reuse finished component work where it remains compatible, and contain each
shared implementation exactly once.

## 2. Authority and input branches

The current hardware-aware branch is authoritative for model import, model
inspection, hardware inspection, compatibility decisions, the version-three
optimisation plan, navigation already implemented there, privacy, and approved
light visuals.

The following branches are bounded implementation inputs, not whole-branch
authorities:

| Concern | Branch | Reviewed tip |
|---|---|---|
| Hardware-aware compatibility and v3 handoff | `fix/hardware-inspection-loq-baseline` | `85e889fa18f73cc19780b2af03598d1b76ac0e21` |
| Canonical optimisation UI | `origin/feature/cross-route-optimisation-ui-v1` | `8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8` |
| OpenVINO route and optimisation adapter | `origin/feature/openvino-optimisation-adapter-v1` | `f0189ed187ba900f27bade5fde282ae4e99e8d7b` |
| Cross-route v2.1 historical contract input | `origin/feature/cross-route-optimisation-contracts-v2-1` | `e254385997392601102b16acf19244437803bdcc` |
| Production GGUF chat/runtime | `origin/feature/gguf-cli-chat-production` | `bacb3f4106e0191b05b870358342f8158765396d` |
| GGUF quantiser source | `https://github.com/ggml-org/llama.cpp.git` | `3f7c29d318e317b63f54c558bc69803963d7d88c` |

The current v3 contract supersedes v1/v2.1 for the new application journey and
all new plan issuance. The existing minimum-executable-version rule and any
explicitly isolated legacy v2 executor seam may remain for compatibility, but
neither is reachable from the new UI coordinator. Historical contract work may
supply compatible implementation details, but it must not create a second
journey authority or downgrade a v3 plan.

When a shared-file conflict occurs, the current hardware-aware branch wins by
default. A required route integration is then expressed as the smallest new
edit against the authoritative file and verified across both routes.

## 3. User journeys

### 3.0 OpenVINO import and inspection entry

The existing Model Import picker and Explorer drop paths continue to converge on
`SubmitInputAsync`. When that classifier accepts an `OpenVinoDirectory`, Model
Import registers the selected folder in an internal, immutable, operation-owned
custody record keyed by `ModelSelectionOperationId`; the path does not enter the
existing `OpenVinoInspectionRequestedEventArgs` or any shell/navigation state.

The shell replaces its current fail-closed `openvino-inspection-unavailable`
response only after the imported O1 `OpenVinoRouteService` is registered behind
an `IOpenVinoModelInspectionPort`. The port resolves the private operation
custody, snapshots the exact package manifest, runs static validation and the
device-neutral native worker validation, and adapts the successful O1 handoff
into the current six-field, path-free `ModelInspectionHandoff`. The current
shared Model Inspection page renders a route-neutral projection of those
bounded results; OpenVINO does not receive a second inspection page or theme.

For a valid OpenVINO package, the existing six handoff fields retain their exact
roles: `modelSha256` is O1's validated primary-model digest and
`modelLengthBytes` is that primary model's validated byte length. The complete
package-manifest digest and membership remain in private OpenVINO custody and
must match again at Hardware, Compatibility, execution, Chat, and Save
boundaries. A source-model folder that requires conversion remains the distinct
path-private conversion-required intent and is never represented as an
inspected OpenVINO package.

After the shared Model Inspection presentation reaches an eligible terminal
state, the normal Hardware Inspection path runs unchanged. A dedicated
`OpenVinoCompatibilityInputProjector` combines the exact current handoffs with
the private validated package facts and current OpenVINO capability evidence to
create `OpenVinoCompatibilityModelInput`; it does not infer missing model,
runtime, device, or package facts. Cancellation, stale selection callbacks,
package drift, missing native validation, or custody mismatch fails closed and
cannot advance to Hardware or Compatibility.

### 3.1 Current model already fits

The compatibility destination presents two primary choices:

- **Chat with current model** launches the inspected, compatible model without
  forcing optimisation.
- **Optimise first** changes Compatibility into its shared preference-selection
  state.

If optional optimisation is cancelled or fails, Chat with the original remains
available after its binding and compatibility are revalidated.

Direct Chat is not represented as a synthetic optimisation success. The
compatibility feature issues a separate, immutable, path-free
`CurrentModelLaunchHandoff` containing the route, model inspection run/handoff,
model digest and length, product Hardware run and snapshot digest, current
compatible runtime-configuration digest, and compatibility decision identity.
An `ICurrentModelChatLaunchAuthority` revalidates those values and privately
resolves the inspected source before calling the route-specific Chat launcher.

Choosing **Optimise first** asks the existing compatibility planning authority
to generate the capability-admitted optional frontier from the same current
model and hardware evidence. No optimisation plan exists until a preference is
selected. Each selection or evidence refresh issues a new v3 plan ID; an older
plan is never amended or reused.

### 3.2 Optimisation required

When the current format does not fit but at least one capability-admitted
alternative fits, the page states:

> This model needs to be quantised before it can be used on your computer.

It also explains that the user can choose how to balance quality and efficiency
and that the original model remains unchanged. Chat is unavailable until a
result has been created, validated, reinspected, and confirmed to fit.

### 3.3 No safe setup or insufficient evidence

- If no admitted alternative fits, the UI explains that no verified setup fits
  the current safe resource budget. It may suggest closing unused applications,
  choosing a smaller model, or reducing context, then rechecking.
- If model, hardware, runtime, tool, or capability evidence is unknown or stale,
  the result remains inconclusive and fails closed. Unknown is never presented
  as either a safe fit or a definitive no-fit result.
- The application never enumerates or terminates unrelated user applications.

### 3.4 Preference and confirmation

The Compatibility feature is the sole preference-selection and plan-issuance
owner. Its existing Automatic control and `ModelPreferenceSlider` remain the
only interactive selector. For an already-fitting model, **Optimise first**
changes the Compatibility page into this selection state. For an
optimisation-required model, it is the primary state shown after the result
summary.

The imported UO1 preference selector is not registered as a second selector.
The post-selection Optimization page begins with review/confirmation of the
exact `OptimizationSelectionHandoff`, followed by progress and destination
states. A confirmation command carries the current plan ID and configuration
digest; delayed commands for a previous selection are rejected. Even when two
slider values resolve to the same candidate, a reissue has a new plan ID, so an
ABA change cannot confirm an earlier plan accidentally.

The Compatibility selection state contains separate **Automatic** selection
and the established continuous preference slider with exactly these labels:

- Maximum efficiency
- Efficient
- Balanced
- High capability
- Maximum capability

Labels express user intent, not fixed quantisation formats. The compatibility
planner maps each selection to the best capability-admitted candidate for the
specific model, workload, hardware snapshot, installed tools, and safe resource
budget.

Each selection truthfully shows:

- expected quality and any quality warning;
- persistent weight format or runtime-only treatment;
- cache configuration;
- system/shared-memory peak;
- dedicated-memory use where established;
- safe memory budget and remaining headroom;
- temporary and persistent disk requirements;
- whether a new model copy/package will be created;
- device/runtime route; and
- evidence grade and relevant limitations.

Q2_K and experimental TurboQuant choices remain explicit, evidence-gated
fallbacks. They are never inferred from names or advertised merely because code
for them exists. The lowest-quality or experimental option requires an explicit
warning and confirmation.

#### Exact experimental consent

Compatibility owns experimental consent as well as preference selection. Before
opt-in, an experimental entry is not a candidate, cannot be selected, and
cannot appear as an admitted plan. The UI may state that a verified experimental
option is available, using bounded product copy derived from the current
capability entry, but it does not present estimated candidate results until the
user explicitly enables that exact option.

The consent action adds only the exact current `EvidenceId` to
`optedInExperimentalEvidenceIds`. It is not a blanket TurboQuant, route, device,
or future-evidence consent. The grant is scoped to the current model inspection
handoff, product Hardware run and snapshot, capability snapshot, and planning
session, and is not carried to another model, hardware refresh, or application
session.

Opt-in regenerates the candidate frontier and issues a new v3 plan ID and
configuration digest. The v3 admission proof and canonical digest bind the
exact opted-in evidence ID. Revocation immediately removes that ID, invalidates
any plan that depended on it, and regenerates the nonexperimental frontier.
Evidence or snapshot drift has the same effect.

Confirmation and execution re-read the current exact consent set and require it
to contain the plan-bound evidence ID before accepting an experimental plan.
Missing, wrong-route, wrong-ID, stale, or revoked consent returns to planning;
it never substitutes a released candidate. Nonexperimental admission is
unchanged by unrelated consent entries. The confirmation surface repeats the
experimental and expected-quality warnings and requires an explicit final
confirmation.

### 3.5 Progress and completion

Both routes use one seven-stage visual sequence:

1. Preflight checks
2. Prepare files
3. Optimise model
4. Validate result
5. Check hardware fit
6. Test model
7. Finish safely

Route-specific substeps may appear in technical details, but the visible page
structure, spacing, status treatment, buttons, and recovery layout are shared.

After persistent success, the user can:

- **Chat with model** in the application; or
- **Save to this PC** as one `.gguf` file for GGUF or one complete portable
  `.zip` package for OpenVINO.

After runtime-only success, the page states:

> This optimisation changes how the model runs; it does not create a new model
> file.

It offers **Chat with model** and **Done**. It does not show a misleading model
Save action.

## 4. Architecture

### 4.1 One selection owner and one post-selection presentation feature

Compatibility remains the sole preference UI and plan-issuance owner.
`Features/ModelOptimization/**` is the sole post-selection optimisation feature
for review, confirmation, progress, recovery, and destination states. The UO1
implementation supplies that component baseline, then is adapted to the
authoritative v3 contracts and existing application theme. Its duplicate
preference state/control is not registered. There are no separate GGUF and
OpenVINO pages, progress controls, recovery cards, destination cards, themes,
fixture galleries, or presentation state types.

The visual implementation must match the approved modern light language used
by Model Import, Model Inspection, Hardware Inspection, and Compatibility:

- centred responsive content with a readable maximum width;
- consistent card radii, padding, typography, icons, and button hierarchy;
- no dark route-specific surface;
- natural vertical scrolling at compact heights;
- correct High Contrast behaviour;
- Windows 200% text support without clipping, overlap, or essential ellipsis;
- equal-height progress rows with vertically centred symbols, labels, and
  statuses; and
- one full-width disclosure pattern.

### 4.2 Shared coordinator

One `OptimizationJourneyCoordinator` owns:

- the accepted `OptimizationSelectionHandoff`;
- confirmation, progress, cancellation, recovery, and destination state
  transitions after Compatibility has issued the exact handoff;
- presentation projection into the canonical page;
- revalidation at confirmation and execution boundaries;
- dispatch through one executor port;
- progress normalization into the seven visible stages; and
- successful-result admission to Chat or Save.

The coordinator does not implement GGUF or OpenVINO conversion and does not
store a local path in its public state.

The public coordinator is a thin orchestrator around a pure
`OptimizationJourneyReducer`, a revalidation port, executor/progress port,
private registry, and destination ports. It does not accumulate those services'
implementation logic.

Exactly one execution attempt may be active. Each accepted Confirm creates a
monotonic, process-local attempt generation bound to the plan ID and
configuration digest. Confirm, Retry, and Back are disabled while the attempt
owns execution. Duplicate commands are ignored without starting work. The
coordinator owns and disposes one cancellation source per generation, publishes
one terminal result at most once, and ignores progress or completion callbacks
whose generation is no longer current. Cancel moves the current generation to
Cancelling; even a non-cooperative late adapter completion cannot register or
publish an output after cancellation or navigation invalidates that generation.

Page unload suspends presentation only; it does not transfer attempt ownership.
App shutdown cancels the owned attempt and runs bounded cleanup. Each Chat or
Save command similarly acquires one destination-command lease, disables only
that action while active, and releases the lease on success, cancellation, or
failure so the user can retry deliberately.

### 4.3 Route executor boundary

One shared executor port accepts the exact v3 plan plus private, verified
execution contexts. A route dispatcher selects one implementation strictly by
`OptimizationExecutionPlan.Route`:

- the GGUF adapter maps the complete v3 GGUF payload to the native GGUF
  optimiser/quantiser and production llama.cpp runtime configuration;
- the OpenVINO adapter maps the complete v3 OpenVINO payload to the imported
  OpenVINO optimisation service and prompting route.

An adapter executes exactly the plan or returns a bounded terminal result. It
must not choose a different format, cache type, device, context, thread count,
batch size, tool, quality band, or persistence mode.

### 4.4 Private source and output resolution

`OptimizationSelectionHandoff` stays the six-property, path-free UI boundary.
The source location travels separately in a private source-custody record keyed
by model inspection handoff ID, model SHA-256, model length, and route. The
record is immutable, create-only, application-owned, and unavailable through
presentation or navigation APIs.

Execution does not verify a path and later reopen it unchecked. The private
route source resolver rejects reparse/symbolic-link inputs and creates a new
operation-owned staged snapshot while reading through the verified source
handle. GGUF bytes are hashed and length-checked during that copy. OpenVINO
copies only the exact package-manifest membership, revalidating each regular
file and the complete package digest before sealing the snapshot. Native tools
receive only the sealed operation-owned snapshot. The original is checked again
after execution to support the required source-unchanged attestation.

A route adapter can write only to its operation-owned staging location; it is
not given authority to write into the committed-output root. On apparent
success it returns a sealed staging candidate to the private output registry.
The registry validates it and promotes it through one generation-aware commit
transaction.

The registry's immutable key is the tuple of route, plan ID, configuration
SHA-256, output identity, and output-manifest SHA-256. Atomic registration also
requires the current attempt generation, source digest, source-unchanged
attestation, output size, and sealed app-owned staging identity. Registration is
create-only: an existing key, reused output identity, replaced manifest, stale
generation, or second terminal registration is rejected rather than updated.

Successful admission atomically moves the sealed candidate into the
registry-owned committed root and writes a durable commit receipt through an
atomic journal replacement. The receipt binds the complete immutable key,
attempt generation, source attestation, output size, manifest digest, and final
publication identity. The receipt is the sole reconstruction authority. A
crash between move and receipt may leave an uncommitted directory, but cannot
create an admitted result; startup removes or quarantines it. A rejected,
cancelled, stale, or interrupted candidate remains explicitly uncommitted and
is removed or quarantined by exact operation ownership.

The registry exposes the result and publishes the coordinator's success state
only after the receipt replacement is durably flushed. Receipt durability and
in-memory admission are therefore one commit point; there is no success-visible
state without a receipt.

Public `OptimizationExecutionResult` data remains limited to status, route,
plan and configuration identity, source attestation, output identity, manifest
digest, size, support code, and completion time. Chat and Save acquire
read-only verified leases; an entry cannot be deleted or replaced while leased.

Committed app-owned outputs survive a process restart. Startup removes only
uncommitted operation-owned staging and publication directories, then
reconstructs registry entries solely from valid commit receipts whose sealed
manifests, files, identities, generations, and digests still match. It never
admits every structurally valid manifest found on disk. Invalid receipt-backed
entries remain unavailable and are quarantined for bounded app-owned cleanup.
A verified result is retained until explicit discard or a storage policy
removes an unleased result that is no longer the current Chat model; automatic
cleanup never touches an imported source or external Save destination.

Chat and Save resolve the private output only after rechecking the plan binding,
manifest, and current file/package identity. Paths never enter navigation
payloads, presentation state, logs, diagnostics, telemetry, or support codes.

### 4.5 Chat and Save routing

One visible Chat intent dispatches by verified route:

- Current-model Chat uses the separate `CurrentModelLaunchHandoff` and
  `ICurrentModelChatLaunchAuthority`; it does not create an
  `OptimizationExecutionResult` or enter the output registry.
- GGUF uses the existing production `OpenProductionChatAsync`/
  `GgufChatLaunchRequest` path and complete approved runtime configuration.
- OpenVINO uses the existing OpenVINO prompting route and verified package or
  runtime profile.

One visible Save intent dispatches by successful result kind:

- GGUF persistent result: copy the verified `.gguf` atomically to the chosen
  destination.
- OpenVINO persistent result: export the complete verified package as one
  portable `.zip` without omitting manifest-bound files.
- Runtime-only result: no Save-model intent exists.

Destination selection occurs only after success. Cancellation or Save failure
must not delete or invalidate the verified operation-owned result.

Save uses an operating-system brokered destination handle where available and
never treats display text as a path. It rechecks required space immediately
before writing. Existing content is replaced only after explicit picker/OS
overwrite confirmation; otherwise a collision fails without modifying either
item. Publication writes a random, non-identifying `CreateNew` temporary item in
the destination directory, streams and hashes the complete output, flushes it,
then performs one atomic rename/replace. Destination-parent identity is checked
before publication; reparse/symbolic-link targets, identity changes, and
cross-volume replacement are rejected. Cancellation or failure removes only
the exact temporary item and leaves any pre-existing destination unchanged.

OpenVINO ZIP entries come from the sealed package manifest only. Entry names
are normalized relative `/` names and reject roots, drive/UNC prefixes,
backslashes, empty or `.`/`..` segments, alternate-data-stream colons, device
names, links, and control characters. Exact and ordinal-ignore-case duplicate
names are rejected. Archive membership must equal the manifest membership,
including pinned licence and notice files, with no missing or additional entry.
Every entry size and digest is checked while archiving and before atomic
publication.

## 5. OpenVINO import boundary

The OpenVINO branch contributes only route-owned implementation and evidence:

- `Features/OpenVinoRoute/**`;
- `shared/GraniteEdgeAI.OpenVino.Contracts/**`;
- `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**`;
- necessary worker packaging targets and project references; and
- focused OpenVINO route tests.

Production composition does not import the older branch's Model Inspection
composition owner. One integration-owned, path-private resolver starts from the
packaged application's installed location, resolves fixed package-relative
converter, official-worker, and TurboQuant-worker directories, and revalidates
their canonical manifests, member hashes, and compile-time manifest identities.
Only its sealed tool contexts may construct the single OpenVINO route service,
optimisation pipeline, and prompting adapter. Missing, swapped, extra, escaped,
or mismatched package content makes OpenVINO unavailable with bounded copy; no
tool path enters a handoff, presentation, navigation payload, diagnostic, or log.

The following older OpenVINO-branch versions are not imported over the current
branch:

- Model Import;
- Model Inspection;
- Hardware Inspection;
- Compatibility;
- Onboarding;
- shared optimisation UI or theme;
- `MainWindow.xaml.cs`;
- application/project registration files as whole-file replacements; or
- shared navigation code.

Necessary references, packaging imports, registrations, and navigation hooks
are recreated as minimal edits in the authoritative files. The OpenVINO worker
may complete native route behaviour on the UCL laptop but must not edit shared
UI or create an OpenVINO-specific user journey.

## 6. GGUF import boundary

The GGUF chat/runtime branch contributes its route-owned runtime contracts,
transport, worker client, `Features/GgufRuntime/**`, packaging target, and
focused tests. Its older compatibility, import, inspection, onboarding,
`MainWindow`, application-resource, project, and navigation versions do not
replace the current branch. Required integration changes are reapplied
minimally.

The GGUF optimisation adapter must consume the v3 execution payload directly,
verify the source and pinned quantiser/tool identities, preserve the original,
validate and reinspect the result, and return only a sealed operation-owned
staging candidate. The private registry alone promotes and receipts it into the
committed-output root. After that admission, Chat receives the exact approved
runtime configuration.

Persistent GGUF quantisation uses a separately built and packaged
`llama-quantize.exe` from official `ggml-org/llama.cpp` commit
`3f7c29d318e317b63f54c558bc69803963d7d88c`, the same mapped upstream identity
already approved for the current LLamaSharp runtime. A repository-owned build
script checks out that exact commit, builds only the CPU x64 quantiser with
network-independent Release flags, inventories its complete runtime dependency
closure and licences, and writes a canonical package manifest containing the
source URL/commit, build flags/toolchain, package ID, executable relative path,
SHA-256, dependency hashes, supported target-format allowlist, and operational
limits. The binary is staged, not committed.

Packaging and execution require both the verified stage directory and the exact
lowercase manifest SHA-256. Missing or different source, manifest, executable,
dependency, licence, architecture, format allowlist, or hash keeps persistent
GGUF candidates unavailable. The Chat/runtime package never contains
`llama-quantize.exe`, and production never downloads or builds it on demand.

### 6.1 Bounded import procedure

No input branch is merged wholesale. Before each component import, the
integration record freezes:

- source ref and exact commit SHA;
- selected source commits where a coherent route-owned range exists;
- every imported path and source blob SHA-256;
- the allowed destination-path set;
- project, package, generated-source, target, resource, protocol, and runtime
  dependencies discovered by the dependency-closure audit; and
- every required shared-file integration patch and its named owner.

The component is imported into one reviewable commit. A forbidden-path diff
check rejects changes outside the manifest. Missing closure is not solved by
copying another shared file: it is recorded, then satisfied through the smallest
compatible reference or integration patch against the current authority. After
each import, route contracts compile, focused tests run with non-zero discovery,
the application builds, and the current hardware/compatibility regression slice
runs before the next component is admitted.

The committed handoff retains the import manifest and maps every adapted file
back to its source blob or identifies it as new integration code. This provides
provenance even when old whole-file versions of `MainWindow`, project files,
navigation, import, inspection, or compatibility are intentionally excluded.

## 7. No-duplication rules

The integrated application contains exactly one of each shared concern:

- preference selector and plan-issuance UI, owned by Compatibility;
- optimisation page and XAML control set;
- optimisation theme and presentation state model;
- journey coordinator and state machine;
- v3 execution-plan and result contracts;
- executor port and route dispatcher;
- output registry;
- destination page and Chat/Save intent definitions;
- route registration; and
- application navigation destination.

GGUF and OpenVINO implementations are thin route adapters behind the shared
ports. They do not copy orchestration, UI, navigation, export policy, progress
state, error copy, or privacy logic.

Static structure tests must reject duplicate public type names, optimisation
XAML/page registrations, resource keys, executor registrations, and any v1/v2
path reachable from the new UI coordinator. An explicitly named legacy v2 seam
may remain only if it is isolated and unreachable from that journey. Repository
review must also verify that imported branches did not create parallel feature
folders with equivalent responsibilities.

A checked-in ownership allowlist maps each responsibility above to its one
permitted path, type, resource dictionary, project item, navigation target, and
runtime registration. Tests assert exactly one reachable implementation rather
than relying only on equal type names: they inspect project includes, XAML page
roots, merged dictionaries, intent handlers, dispatcher registrations, and
navigation destinations, and they reject equivalent unowned implementations in
alternate feature directories.

## 8. State and failure semantics

The closed execution statuses remain:

- `SucceededPersistent`
- `SucceededRuntimeProfile`
- `Cancelled`
- `Failed`
- `ReplanRequired`

### 8.1 Replan required

Source, model, hardware, capability, tool, or configuration drift returns
`ReplanRequired`. No work begins or continues under a substituted plan. The
journey returns to compatibility, obtains fresh evidence, issues a new plan ID,
and asks the user to review it.

### 8.2 Failure

Execution failure publishes no output. The route removes incomplete,
operation-owned staging data, verifies that the original is unchanged, and
shows bounded recovery guidance. Optional optimisation also offers Chat with
the revalidated original; required optimisation does not.

### 8.3 Cancellation

Cancellation stops the complete operation-owned process tree, drains bounded
output, removes incomplete staging data, and leaves the original unchanged.
It never terminates unrelated user processes.

### 8.4 Success

Persistent success requires conversion, validation, smoke testing,
reinspection, manifest creation, atomic publication, and a verified unchanged
source. Runtime-only success requires a verified profile and unchanged source
and publishes no model artifact.

### 8.5 Destination failures

A Save or Chat-launch failure does not invalidate a successful optimisation.
The result remains available for another destination attempt. Any later
identity mismatch fails closed and requires revalidation rather than using a
stale or replaced artifact.

### 8.6 Closed failure mapping

No native exception, filesystem message, tool output, or path reaches the UI.
Route adapters map planning-input drift to the existing bounded replan codes and
map disk, staging, conversion, validation, smoke, reinspection, publication,
cancellation, and unexpected failures to their existing
`OptimizationSupportCode` values. Failure to seal or atomically register an
output is `PublicationFailed`; a callback for a stale attempt generation is
suppressed and cannot produce a terminal result.

Destination operations use a separate closed `DestinationSupportCode` set:

- `None`
- `CancelledByUser`
- `ResultUnavailable`
- `IdentityMismatch`
- `UnsafeDestination`
- `DestinationChanged`
- `InsufficientSpace`
- `PublicationFailed`
- `UnexpectedFailure`

Picker overwrite refusal is `CancelledByUser`; an unconfirmed collision does
not overwrite and is reported as `DestinationChanged`. Every caught destination
adapter, destination-registry, Chat-launch, or Save exception is translated at
its boundary to one of these bounded destination codes. Route-executor
exceptions remain governed by the `OptimizationSupportCode` mapping above. The
user copy is selected locally from the code and never includes exception text
or adapter-supplied data.

## 9. Integration sequence

1. Create an isolated integration worktree from exact base `85e889fa...` and
   record clean baseline verification.
2. Fetch and verify every named input tip.
3. Record the UI import manifest, import the canonical post-selection
   presentation controls once without its duplicate preference owner, and
   migrate them to v3 without importing stale shared files.
4. Complete Compatibility-owned optional-plan issuance and the typed
   current-model Chat handoff, then implement the shared coordinator,
   executor/progress ports, private source/output registries, and destination
   routing with tests.
5. Import route-owned GGUF runtime/chat code, build/freeze the separate pinned
   quantiser package, and complete the v3 GGUF optimiser
   adapter.
6. Prove the complete GGUF journey locally: import through Chat and Save.
7. Import route-owned OpenVINO contracts, services, worker client, packaging,
   and tests only.
8. Adapt OpenVINO to the same v3 coordinator and connect its path-private
   Import -> shared Model Inspection -> Hardware -> Compatibility entry without
   changing or duplicating shared UI.
9. Prove the complete OpenVINO journey on the UCL laptop, including Chat and
   package export.
10. Run combined regressions, native visual review, packaged Debug/Release
    builds, end-to-end tests, privacy scans, duplication checks, and final code
    review before integration.

## 10. Verification

### 10.1 Contract and structure

- Exact v3 canonical vectors and plan/handoff invariants remain green.
- the new UI coordinator accepts only the exact current v3 handoff; any retained
  legacy v2 executor seam remains explicitly isolated and unreachable from it.
- One implementation exists for every shared concern in section 7.
- Every bounded import matches its commit/path/blob manifest, contains complete
  dependencies, and changes no forbidden shared path.
- Route adapters accept only their own route and reject mixed or incomplete
  payloads.
- All test filters used as evidence discover a non-zero count.

### 10.2 Component behaviour

- Automatic and all five manual preference bands.
- Quality warnings and exact evidence-ID experimental consent: absence before
  opt-in, frontier/plan reissue after opt-in, confirmation/execution
  revalidation, revocation and evidence-drift invalidation, wrong/missing/stale
  ID rejection on both routes, and no expansion of nonexperimental admission.
- Current-fit, optimisation-required, no-fit, and inconclusive branches.
- Current-fit optional-plan issuance, unique reissue plan IDs, stale-confirm
  rejection, and the separate current-model Chat authority.
- Revalidation, replan, retry, cancellation, process-tree cleanup, rollback,
  and original preservation.
- Duplicate Confirm/Retry/Cancel, delayed progress/completion, page unload,
  shutdown, terminal-once publication, generation invalidation, and
  destination-command leases.
- Private source custody, operation-owned snapshotting, immutable output
  registration, lease/retention/restart reconstruction, TOCTOU resistance, and
  path-leak canaries.
- GGUF single-file export and OpenVINO complete-package ZIP export, including
  overwrite consent, destination identity change, link/reparse rejection,
  insufficient space, cancellation cleanup, archive traversal and
  case-collision rejection, exact manifest membership, and licence/notice
  preservation.
- Runtime-only Save suppression and explanatory copy.
- Chat/Save failure recovery without result loss.

### 10.3 Shared visuals

The following locally preserved, user-approved HTML boards are the exact visual
oracles. Before visual work begins, verify each byte hash. If an oracle is
missing or mismatched, visual acceptance is blocked rather than reconstructed
from memory.

| Screen family | Approved oracle | SHA-256 | Exact accepted scope |
|---|---|---|---|
| Model Inspection template | `docs/ux/visual-oracles/model-inspection-balanced-full-approval-v3.html` | `D44CFCE9C53BCD9D0EAAA41CA8BA9DED434F68845BBF943351518366FBB20CFF` | `.viewport` geometry, light tokens, card/row/disclosure/action layout, responsive presets; the old disabled future-action attributes are not behavior authority |
| Hardware Inspection direction B | `docs/ux/visual-oracles/hardware-layout-direction-b-refinement-v3.html` | `6C677D5E9BF9F2F58F1F404ECA3798CD6D17C68911F966CA21DEC01CA902FB4B` | only `.direction[data-choice="b"] .screen`; A/C comparison boards and review chrome are excluded |
| Compatibility light layout | `docs/ux/visual-oracles/compat-visual-style-v3.html` | `3FFC2F3664363F20908DEDF110E52B53CEE0860FB24C096BEDE4D7DB979A03DF` | only `.card[data-choice="a"] .hi-page` structure/geometry/tokens; its old action labels are replaced by `Chat with current model` and `Optimise first` |
| Optimisation choice | `docs/ux/visual-oracles/optimisation-choice-page-v1.html` | `434F04BA81E1EB9EF1F88B3C2F2CCA357103B7FE3B6C03F952681280C85921AE` | `.mockup` application page only; the A/B approval controls beneath it are review chrome |
| Optimisation spectrum | `docs/ux/visual-oracles/optimisation-spectrum-v1.html` | `D74F3B93DF5C38419CF6ADAD45A308FE78745EB9D23DB607B411858E054EB9AF` | frontier/quality-memory concept plus `data-choice="keep-automatic"`; old spectrum labels yield to the exact labels in §3.4 and the choice-page oracle |
| Shared optimisation flow | `docs/ux/visual-oracles/shared-optimisation-flow.html` | `D669771600EE0D3A56EF3B793C6C06A9882048B88F46F433FED006E5033292F0` | route-neutral ownership/sequence only; not pixel or product-copy authority |

These preserved boards are exact visual references within the scoped regions,
not executable product specifications. This design owns behavior, copy, enabled
actions, privacy, and route semantics whenever a board contains earlier review
copy or disabled placeholders. No worker may copy comparison/review chrome into
the application or use a stale board label to override an approved journey.

The canonical fixture gallery covers selection, confirmation, every progress
stage, cancellation, failure, replan, persistent success, and runtime-only
success at compact, standard, and wide layouts, High Contrast, and actual
Windows 200% text scaling. Native screenshots must show centred light layouts,
consistent spacing, vertically aligned icons/text/statuses, equal progress
rows, full-width disclosures, readable buttons, and no clipping or duplicate
onboarding shell. Each state receives three passes: first inventory every visual
and interaction defect against the oracle; second correct geometry, hierarchy,
spacing, alignment, typography, colour, focus and responsive behavior; third
recapture and challenge the result for remaining inconsistency across GGUF and
OpenVINO. A state is not accepted merely because its automated layout tests
pass.

### 10.4 End-to-end routes

GGUF must prove:

`Import → Inspect → Hardware → Compatibility → Optimise → Validate/Reinspect → Chat`

and the same journey ending in `.gguf` Save.

OpenVINO must prove the identical visible journey ending in OpenVINO Chat and
complete `.zip` Save. Native converter, worker, packaging, and prompting
evidence is gathered on the UCL Intel laptop when unavailable locally. An
unavailable native prerequisite remains a blocker, not a pass.

### 10.5 Whole application

- Preserve the existing 1,431-test hardware/compatibility packaged baseline.
- Run complete compatibility, optimisation UI, GGUF, OpenVINO, import,
  inspection, hardware, navigation, privacy, and process-lifecycle suites.
- Build packaged Debug/x64 and Release/x64 with zero errors.
- Run native smoke/E2E tests from the built package.
- Run `git diff --check`, privacy/path scans, duplicate-registration scans, and
  final independent specification and quality reviews.
- Finish with a clean committed worktree and recorded immutable evidence.

## 11. Completion criteria

The feature is complete only when:

1. both GGUF and OpenVINO use the same approved screens and shared coordinator;
2. current-fit direct Chat, optional optimisation, and optimisation-required
   journeys use their specified typed authorities and behave as specified;
3. each route executes only an exact revalidated v3 plan;
4. GGUF can Chat and save a verified `.gguf` result;
5. OpenVINO can Chat and save a verified complete package;
6. runtime-only results truthfully omit model Save;
7. originals remain verified unchanged across success, failure, and
   cancellation;
8. compact, wide, High Contrast, and 200% text visuals pass native review;
9. source/output custody, concurrent commands, cancellation, restart, and
   Save/ZIP publication satisfy the atomicity and privacy rules in this design;
10. no shared code, screen, contract, resource, or registration is duplicated,
    and every component import has complete immutable provenance;
11. all required suites and packaged end-to-end checks pass with non-zero
    discovery; and
12. no native or environment blocker is represented as completed evidence.

No push, merge to `main`, release publication, or destructive user-process
management is implied by this design. Those actions require the normal
implementation and integration workflow.
