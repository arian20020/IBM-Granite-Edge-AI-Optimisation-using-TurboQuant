# Model Inspection Completion Roadmap Design

| Metadata | Value |
|---|---|
| Status | Approved conversational design; written specification awaiting user review |
| Date | 2026-08-08 |
| Branch | `test/model-inspection-completeness-gate` |
| Stacked base | `refactor/model-inspection-cleanup` |
| Verified base commit | `8f00a64a40e916872abfd6b72721ef86f223bab9` |
| Feature endpoint | validated Model Inspection outcomes and controlled handoff contracts |
| Downstream boundary | conversion execution, Hardware Fit implementation, OpenVINO, TurboQuant, chat, GPU acceleration and non-x64 support remain separate programmes |

---

## 1. Purpose

This specification turns the read-only Model Inspection test-completeness audit
into one durable, ordered completion roadmap.

It answers three different questions without mixing them:

1. what Model Inspection implements and verifies today;
2. what work remains before the feature can be called complete;
3. which later product capabilities depend on Model Inspection but are not part
   of completing it.

The roadmap does not treat a design document as proof of implementation. It
requires traceability from a controlled workflow or approved engineering
decision through implementation, tests and exact-head evidence.

---

## 2. Definition of Model Inspection completion

Model Inspection is complete only when a validated GGUF request can travel
through the complete protected production path:

```text
Model Import
    -> immutable validated request
    -> Onboarding handoff
    -> Model Inspection page
    -> application service
    -> protected worker client
    -> packaged production worker
    -> one extracted LLamaSharp runtime implementation
    -> structured path-minimised evidence
    -> application evidence mapper and classifier
    -> immutable inspection result
    -> live accessible outcome presentation
    -> eligible downstream handoff contract
```

Completion requires all of the following:

- inspection begins automatically from the exact validated request;
- the source model is never modified;
- the production worker is launched through the existing fail-closed Windows
  process boundary;
- the worker uses the approved CPU-only LLamaSharp runtime closure;
- worker evidence remains factual, structured and path-minimised;
- the application, not the worker, owns user-facing classification;
- every supported model and operational outcome has deterministic precedence;
- live progress, cancellation, retry and stale-run suppression work;
- only `Ready` and `ReadyWithWarnings` may continue to Hardware Fit;
- `ConversionRequired` is emitted only when a tested conversion route is
  registered, without implementing conversion in this feature;
- the current overview and technical-details workflows are accessible and
  recoverable;
- the x64 packaged application contains the exact approved worker closure and
  no model, fixture or test evidence;
- offline, privacy, integrity, process containment, accessibility and stability
  gates pass on the exact final commit;
- every claim is backed by retained executable evidence.

Passing unit tests alone does not satisfy this definition.

---

## 3. Explicit downstream boundary

The following are dependencies or consumers, not Model Inspection completion
tasks:

| Capability | Model Inspection responsibility | Separate programme responsibility |
|---|---|---|
| Hardware Fit | expose an eligible handoff only for `Ready` and `ReadyWithWarnings` | inspect device fit and choose execution configuration |
| Model conversion | classify as `ConversionRequired` only when a tested route exists and expose the route identifier | display and execute conversion, validate converted output |
| OpenVINO | preserve future format/backend extension boundaries | implement and validate OpenVINO inspection or conversion |
| TurboQuant | expose factual model evidence needed by later selection | activate, validate and benchmark TurboQuant |
| Chat | provide a validated model result to later flows | package and operate the chat runtime |
| GPU/Vulkan | make no unsupported claim | implement, package and validate acceleration |
| x86/ARM64 | reject unsupported architecture clearly | create and validate separate native/package closures |

This separation prevents the roadmap from silently becoming the entire product
programme.

---

## 4. Controlling sources and precedence

The roadmap applies these sources in precedence order:

1. controlled Project OneDrive workflows and Workflow Register;
2. the Project Testing Standard;
3. current repository code and executable tests at the verified base;
4. later protected-worker ADRs and Gate 1/Gate 2 implementation evidence;
5. the current worker integration design;
6. retained LLamaSharp feasibility evidence and its dedicated coverage matrix;
7. older Model Inspection UX/design material where it has not been superseded.

The protected-worker decision supersedes older in-process LLamaSharp production
designs. The older documents remain valid sources for UX intent, automatic
start, the five semantic stages, outcome wording, Hardware Fit eligibility and
accessibility intentions.

Relevant controlled workflow identifiers are:

| Workflow ID | Roadmap ownership |
|---|---|
| `WF-IMP-001` | validated import and exact request handoff regression |
| `WF-INS-001` | automatic inspection start, progress, cancellation and completion |
| `WF-OVR-READY-001` | Ready and ReadyWithWarnings overview and eligible handoff |
| `WF-OVR-CONV-001` | ConversionRequired overview and tested-route handoff |
| `WF-OVR-UNSUP-001` | unsupported-model overview and recovery |
| `WF-OVR-INVALID-001` | invalid/incomplete overview and recovery |
| `WF-CONV-SCR-001` | downstream conversion boundary only |
| `WF-DIAG-SCR-001` | technical-details presentation |

Document validation of a workflow is not software execution evidence.

---

## 5. Current verified baseline

The roadmap begins from Phase 1 closure commit
`8f00a64a40e916872abfd6b72721ef86f223bab9`.

The current production boundary includes:

- validated GGUF quick scan and immutable request creation;
- import -> onboarding -> Model Inspection navigation;
- the Model Inspection presentation shell and initial five stages;
- application contracts and reusable presentation controls;
- protocol version 1 contracts and bounded strict UTF-8 transport;
- protected worker launch, handshake, containment, timeout and cancellation
  infrastructure;
- a production worker using a controlled unavailable engine;
- LLamaSharp CPU/VocabOnly feasibility tooling and retained deterministic,
  native and trusted-model evidence.

The current boundary does not include:

- a production LLamaSharp inspection engine;
- production evidence mappers or classifier;
- an application inspection service;
- a Model Inspection execution ViewModel;
- real page progress or a functional Cancel command;
- runtime-driven outcome screens;
- packaged worker/native runtime closure;
- a live Hardware Fit or conversion implementation.

The historical permanent baseline records 436 executed tests. That count is
evidence for the historical head, not a completion target.

---

## 6. Read-only completeness-audit result

The audit created 63 behavior clusters:

| Classification | Rows |
|---|---:|
| `IMPLEMENTED - TEST NOW` | 50 |
| `PARTIALLY IMPLEMENTED` | 6 |
| `NOT YET IMPLEMENTED` | 4 |
| `DEFERRED / OUT OF CURRENT GATE` | 2 |
| `SUPERSEDED` | 1 |

For the 56 implemented or partially implemented clusters:

| Direct evidence level | Rows |
|---|---:|
| adequate | 18 |
| partial | 29 |
| none | 9 |

The audit also identified 19 false-confidence-prone assertion patterns.

Two validator defects are directly visible in the current code and require
test-first reproduction before repair:

1. contradictory before/after file evidence can still claim
   `IntegrityPreserved = true`;
2. an explicit JSON `null` tokenizer dictionary can survive composed evidence
   validation despite the public non-null contract.

A buffered transport read that may ignore pre-cancellation remains a suspected
defect until a focused failing test reproduces it.

The full row-level findings belong in
`docs/testing/Model-Inspection-Test-Completeness-Matrix.md`. The existing
`docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md` remains separate and
is linked rather than copied.

---

## 7. Durable documentation structure

The completion programme uses three linked documents.

### 7.1 Verification matrix

Path:

`docs/testing/Model-Inspection-Test-Completeness-Matrix.md`

Owned content:

- all 63 audited behavior clusters;
- requirements/workflow links;
- implementation classification;
- existing test and evidence trace;
- realistic failure modes;
- coverage and test-quality findings;
- proposed actions and explicit deferrals;
- historical-versus-fresh evidence status.

### 7.2 Completion roadmap specification

Path:

`docs/superpowers/specs/2026-08-08-model-inspection-completion-roadmap-design.md`

Owned content:

- feature completion boundary;
- ordered gates and dependencies;
- responsibilities and non-claims;
- gate-level acceptance criteria;
- traceability and evidence policy.

### 7.3 Test-completeness implementation plan

Path:

`docs/superpowers/plans/2026-08-08-model-inspection-test-completeness-gate.md`

Owned content:

- exact files and test methods;
- expected RED and GREEN results;
- focused and regression commands;
- coherent commit boundaries;
- Windows CI and evidence-retention procedure;
- exact completion conditions for the immediate gate.

Later cleanup and production gates receive their own approved child plans.
They are not expanded into speculative implementation steps here.

---

## 8. Ordered completion programme

No gate begins until the previous gate has executable evidence and no
unresolved critical or important blocker.

The inherited documents use two different numbering schemes: cleanup Phases 2
through 8 and production Gates 3 through 6. This roadmap always prefixes those
names with `cleanup` or `production`; the left-hand `Order` column is the
unambiguous end-to-end execution sequence.

| Order | Gate | Current status | Exit result |
|---:|---|---|---|
| 0 | Phase 0 baseline and Phase 1 WinUI cleanup | verified at `8f00a64` | trustworthy protected starting point |
| 1 | test-completeness gate | audit complete; repository closure not started | current implementation has defensible verification coverage |
| 2 | cleanup Phase 2 - contracts, protocol and transport | not started | compatible, reviewed shared boundary |
| 3 | cleanup Phase 3 - WorkerClient and Windows infrastructure | not started | reviewed process/security ownership |
| 4 | cleanup Phase 4 - worker host and fixture | not started | reviewed worker state machine and fixture separation |
| 5 | cleanup Phase 5 - LLamaSharp feasibility | not started | extraction dispositions and refreshed feasibility evidence |
| 6 | cleanup Phase 6 - test architecture | not started | readable, deterministic, layer-correct tests |
| 7 | cleanup Phase 7 - workflows and documentation | not started | automation and current documentation agree |
| 8 | cleanup Phase 8 - independent closure | not started | exact-head cleanup evidence and review decision |
| 9 | production Gate 3 - extracted LLamaSharp engine | not started | one production runtime implementation connected to worker |
| 10 | production Gate 4 - packaging | not started | verified x64 packaged worker/native closure |
| 11 | production Gate 5 - mapping, classifier and service | not started | deterministic application result pipeline |
| 12 | production Gate 6 - ViewModel and WinUI | not started | live accessible inspection workflow |
| 13 | Model Inspection release closure | not started | exact-head end-to-end acceptance evidence |

---

## 9. Gate 1 - test-completeness gate

### Objective

Close meaningful verification gaps in the system that already exists without
implementing later feature behavior.

### Ordered work

1. Commit the complete verification matrix and approved implementation plan.
2. Add focused tests for the two evidence-validator defects.
3. Repair only the invariant behavior proven RED by those tests.
4. Complete protocol field, enum, JSON, validator and sequence coverage without
   silently changing protocol version 1.
5. Add transport fragmentation, exact-boundary, cancellation, ownership and
   concurrent-flush tests.
6. Add worker-host success, exception, EOF, cancel-mismatch and parent-loss
   coverage.
7. Launch the actual production worker in a real-process smoke test.
8. Execute the currently unused malformed, crash, hang and output-flood fixture
   scenarios through the real process boundary.
9. Test actual `ModelInspectionPage.Loaded` composition, request identity,
   navigation failures, subscription lifecycle and re-entry.
10. Complete current presentation-selector, card-state, filesystem-path,
    privacy and live-region coverage.
11. Retain manual Windows evidence for keyboard, focus, screen reader, high
    contrast, text scaling and resize behavior.
12. Add meaningful test floors, complete project discovery, dependency-aware
    workflow triggers, privacy-gated uploads and unconditional orphan checks.
13. collect line and branch coverage as a gap detector and selectively mutation
    test high-risk validators, transport and worker state paths.
14. Run every affected test layer, repeated process stability, trusted real
    model, privacy, integrity, no-port and zero-orphan gates.
15. Parse raw artifacts and record exact final-head evidence in a stacked draft
    pull request.

### Protocol decision boundary

The gate may characterize current behavior, but these changes need a separate
approved protocol decision:

- making every defaultable JSON property formally required;
- distinguishing missing evidence sections from empty unavailable sections;
- enforcing an exact 64-hex SHA representation;
- defining chat-template presence/length/hash consistency;
- defining new cross-field progress constraints;
- deriving `IntegrityPreserved` instead of serializing it.

### Exit conditions

- all 56 current behavior clusters have adequate direct evidence or an accepted
  explicit deferral;
- the two validator defects are reproduced and repaired test-first;
- no unresolved suspected defect remains unclassified;
- all required tests are discovered and zero are skipped;
- coverage contains no unexplained high-risk branch gap;
- privacy and orphan checks cannot be bypassed by failure ordering;
- exact-head Windows and trusted-model evidence is retained and reviewed.

---

## 10. Gates 2 through 8 - remaining cleanup programme

The existing cleanup master plan remains authoritative for these phases. The
test-completeness gate is an inserted prerequisite and does not erase or
silently declare the remaining cleanup complete.

### Gate 2 - shared contracts, protocol and transport

1. Review every serialized member, enum, validator state and transport owner.
2. Separate compatibility characterization from approved behavior changes.
3. Simplify only behind passing tests.
4. Run contracts, transport, worker, WorkerClient and process regressions.
5. Record protocol compatibility evidence and close every Phase 2 ledger row.

### Gate 3 - WorkerClient and Windows infrastructure

1. Build a complete ownership map for handles, allocations, streams, jobs and
   drain tasks.
2. Review partial-launch failure, cancellation races, timeout precedence,
   process-tree cleanup and concurrent sessions.
3. Preserve the no-fallback, allowlisted-environment, reparse rejection,
   inherited-handle allowlist and creation-time containment rules.
4. Run repeated timing-sensitive process campaigns and a separate
   security/privacy review.
5. Close Phase 3 only with zero orphan processes.

### Gate 4 - worker host and abnormal fixture

1. Review host states, terminal ownership, engine lifetime, parent monitoring,
   stdin EOF and exit-code rules.
2. Prove every abnormal scenario remains fixture-only.
3. Keep production free of hidden crash, hang or test switches.
4. Run host, fixture-isolation and real-process suites.
5. Record source-separation evidence.

### Gate 5 - LLamaSharp feasibility

1. Classify every feasibility file as extract, retain, replace after extraction
   or remove after migration.
2. Review runtime identity, file integrity, hashing, metadata, tokenizer,
   template, progress, cancellation, redaction and evidence writing.
3. Remove no existing coverage during cleanup.
4. Run deterministic, contained native and controlled real-model verification.
5. Reconfirm original model SHA, privacy and absence of a network listener.

### Gate 6 - test architecture

1. Review every test name, assertion, helper, cleanup path and timing
   assumption.
2. Strengthen the 19 false-confidence-prone patterns identified by the audit.
3. Preserve clear separation among unit, contract, transport, process,
   packaged UI, native and trusted-model layers.
4. Preserve zero-test safeguards and deterministic fixture ownership.
5. Re-run all affected suites and repeated process stability.

### Gate 7 - workflows and documentation

1. Reconcile workflow commands, triggers, floors, permissions, artifacts,
   privacy and orphan handling.
2. Reconcile READMEs, ADRs, specs, plans, matrices, runbooks and PR bodies with
   current implementation.
3. Preserve historical claims while adding clear supersession notes.
4. Validate all current documentation links and status vocabulary.
5. Close only after hosted workflows pass.

### Gate 8 - independent cleanup closure

1. Review the complete branch diff without relying on earlier conclusions.
2. Recheck every critical/high-risk ownership, security, privacy and failure
   path.
3. Verify every cleanup ledger row has one allowed final disposition.
4. Run exact-head CI and the required trusted-model campaign.
5. Record run/job IDs, test totals, skips, artifacts, sizes, digests, privacy
   and orphan results.
6. Resolve or explicitly accept every critical or important finding.
7. Reconcile final evidence and draft PR with the exact commit.

---

## 11. Production Gate 3 - extracted LLamaSharp engine

### Objective

Move the proven feasibility implementation into one production runtime library
and connect it to the protected worker without creating a second independent
implementation.

### Target boundary

```text
runtime/GraniteEdgeAI.ModelInspection.LlamaSharp/
    GraniteEdgeAI.ModelInspection.LlamaSharp.csproj
    LlamaSharpInspectionEngine.cs
    RuntimeConfiguration.cs
    FileIdentityService.cs
    NativeProgressRecorder.cs
    EvidenceCollector.cs
    FailureMapper.cs
    SensitiveTextRedactor.cs
```

The final file split may change during the Gate 3 design, but responsibilities
must remain cohesive and the worker stays free of application/XAML concerns.

### Ordered work

1. Approve the Gate 3 extraction design and exact package/runtime identity.
2. Create the runtime project with dependency and architecture fitness tests.
3. Extract file snapshot, hashing, metadata, tokenizer, chat-template, progress,
   failure and redaction behavior from the feasibility boundary.
4. Make the feasibility tool consume the extracted implementation.
5. Implement the worker engine adapter behind the existing
   `IWorkerInspectionEngine` seam.
6. Preserve worker protocol version 1 and path-minimised evidence.
7. Dispose every native/model/context resource through controlled lifetime
   ownership.
8. Map native/managed failure into stable operational codes without returning
   paths, template text or exception chains.
9. Prove cooperative cancellation and forced process containment.
10. Run exact Granite success and repeat campaigns, both cancellation scopes,
    malformed/random/missing/directory/locked input, continuity mismatch,
    source SHA preservation, privacy and no-port checks.
11. Record representative CPU/memory behavior before deciding Job Object
    resource limits.
12. Retain exact package and native binary identities.

### Exit conditions

- one production runtime implementation exists;
- the feasibility tool and worker use that implementation;
- no LLamaSharp type crosses the worker protocol or application boundary;
- exact controlled-model evidence is factual and repeatable;
- cancellation, privacy, integrity, resource disposal and no-port gates pass;
- no production abnormal-test switch exists.

---

## 12. Production Gate 4 - packaging

### Objective

Prove that the packaged x64 application contains and resolves one immutable,
complete and CPU-only worker runtime closure.

### Ordered work

1. Approve the package layout and fixed worker subdirectory.
2. Include the production worker, contracts, runtime library, LLamaSharp managed
   assembly, exact CPU native dependencies and required runtime configuration.
3. Resolve packaged runs from `Package.Current.InstalledLocation.Path` and
   controlled local runs from `AppContext.BaseDirectory` only.
4. Canonicalise the root and candidate, require separator-qualified containment
   and reject reparse points or architecture mismatch.
5. Never search `PATH`, the current working directory or arbitrary locations.
6. Treat installed content as read-only and stream evidence through the
   protocol.
7. Verify executable and dependency hashes or the approved package identity
   mechanism before launch.
8. Inspect publish and MSIX contents for exact closure.
9. Prove absence of GGUF files, test fixtures, evidence, CUDA/Vulkan binaries
   and LLamaSharp packages in the WinUI process.
10. Reject non-x64 architecture with a stable operational result before launch.
11. Revisit compatible process mitigations and resource limits using Gate 3
    measurements.
12. Run unpackaged controlled-build, application publish and packaged MSIX
    campaigns.

### Exit conditions

- the exact approved x64 closure is present once;
- packaged and controlled local resolution are deterministic and contained;
- no untrusted fallback exists;
- package contents match runtime-profile policy;
- non-x64 behavior is explicit and tested;
- full packaged build, installation and launch evidence passes.

---

## 13. Production Gate 5 - evidence mapping, classifier and service

### Objective

Convert factual worker evidence into one deterministic immutable application
result without putting user-facing policy in the worker.

### Components

```text
Features/ModelInspection/
    Runtime/          worker-to-application evidence mappers
    Classification/   outcome rules and precedence
    Services/         orchestration, progress, cancellation and run identity
```

### Ordered work

1. Approve exact mapper, classifier and service contracts.
2. Map every protocol evidence field into application-owned immutable types.
3. Reject required quick-scan/worker evidence conflicts before classification.
4. Define and test the complete model-outcome precedence: `Invalid`,
   `IncompletePackage`, `Unsupported`, `ConversionRequired`,
   `ReadyWithWarnings`, `Ready`.
5. Keep `Cancelled` and `OperationalFailure` as execution outcomes that bypass
   model classification.
6. Treat unknown unsafe evidence/observation states as operational failure,
   never guessed readiness.
7. Implement a versioned conversion-route registry interface.
8. Emit `ConversionRequired` only when that registry has a tested applicable
   route; otherwise emit `Unsupported`.
9. Implement `ModelInspectionService` request validation, worker invocation,
   progress mapping, consistency checks, classifier invocation and immutable
   result creation.
10. Preserve run identity and suppress stale callbacks/results.
11. Implement cooperative cancellation and retain forced timeout as operational
    failure.
12. Keep the service free of XAML, navigation, raw stdout parsing and
    LLamaSharp/native types.
13. Test every mapper field, conflict, outcome, precedence, cancellation path,
    failure path and stale-run race.

### Exit conditions

- every evidence field is deliberately mapped, rejected or documented as
  unavailable;
- all model and execution outcomes have deterministic precedence;
- only completed evidence reaches the classifier;
- Ready and ReadyWithWarnings are the only Hardware Fit-eligible outcomes;
- the service is deterministic, cancellable, stale-safe and UI-independent.

---

## 14. Production Gate 6 - ViewModel and WinUI

### Objective

Turn the current presentation shell into the complete live, accessible Model
Inspection workflow without moving native/process responsibilities into the UI.

### Components

```text
Features/ModelInspection/
    ViewModels/       observable run state and commands
    Presentation/     pure result-to-card presentation factories
    Controls/         reusable WinUI rendering
    ModelInspectionPage.xaml(.cs)  lifecycle, binding and navigation host
```

### Ordered work

1. Approve ViewModel states, commands, page lifecycle and navigation ownership.
2. Initialise the ViewModel with the exact immutable request passed through
   onboarding.
3. Start inspection automatically only after the valid page/view state is
   ready.
4. Project genuine live worker progress into the five semantic stages.
5. Enable Cancel only while a real run is active.
6. Display cooperative cancellation as Cancelled and forced timeout as
   OperationalFailure.
7. Implement retry, choose-another-model and stale-run suppression.
8. Build pure presentation factories for every model and execution outcome.
9. Render Ready and ReadyWithWarnings with the correct Hardware Fit action.
10. Render ConversionRequired only with a registered route handoff.
11. Render Unsupported and Invalid/incomplete with understandable recovery.
12. Render operational failure with retry and privacy-safe technical details.
13. Implement the technical-details workflow without displaying full local
    paths, full chat templates or exception chains.
14. Preserve outcome meaning through text/icon/automation semantics rather than
    color alone.
15. Implement logical keyboard/tab order, visible focus, accessible names,
    correct live-region events, high contrast, approximately 200% text scaling,
    resize stability and reduced-motion-safe behavior.
16. Test correct/wrong/missing request, automatic start, re-entry, navigate
    away/back, real progress, cancellation races, retry, every outcome/action,
    binding refresh and stale callbacks on the packaged UI thread.
17. Retain manual Narrator/Accessibility Insights/keyboard/scaling evidence in
    addition to automation.

### Exit conditions

- the page reflects real service state rather than a static inspecting shell;
- functional cancellation has correct cooperative and forced-timeout meaning;
- every current controlled overview/diagnostic workflow is reachable and
  recoverable;
- only eligible outcomes expose Hardware Fit continuation;
- accessibility requirements have both automated and retained manual evidence;
- no page or ViewModel launches processes, parses protocol bytes or owns native
  resources.

---

## 15. Model Inspection release closure

### Objective

Prove the complete feature at the exact final commit rather than inferring
completion from individual green layers.

### Ordered work

1. Run packaged `WF-IMP-001 -> WF-INS-001` happy and recovery paths.
2. Run every implemented overview and technical-details workflow.
3. Verify all Hardware Fit and conversion handoff eligibility rules without
   executing downstream features.
4. Run missing, directory, locked, changed, malformed, unsupported and hostile
   input campaigns.
5. Run cooperative cancellation, forced timeout, crash, hang, malformed
   protocol and parent-loss recovery campaigns.
6. Run concurrent/repeated inspections and a stability/soak campaign.
7. Prove source-model SHA preservation and no production/temp model copy.
8. Prove no full local path, template text, secret or exception chain is
   retained in UI, logs, TRX or uploaded artifacts.
9. Prove no unexpected network listener and no required network access.
10. Prove no worker, fixture or descendant process remains after every
    campaign.
11. Run keyboard, screen-reader, live-region, high-contrast, scaling and resize
    acceptance.
12. Run complete exact-head CI with zero required skips and meaningful test
    floors.
13. Download and independently parse retained artifacts where practical.
14. Record commit/tree, workflow/run/job IDs, test totals, failures, skips,
    warnings, artifact IDs, sizes and digests.
15. Review the full diff and resolve every critical/important finding or obtain
    an explicit documented deferral.
16. Reconcile matrices, READMEs, ADRs, runbooks, evidence and draft PR body with
    the exact commit.
17. Obtain explicit user acceptance before merge or downstream execution.

### Exit conditions

- every controlled current Model Inspection workflow has executable evidence;
- no required test is skipped or silently undiscovered;
- all protected security/privacy/process/integrity guarantees pass;
- accessibility acceptance evidence is retained;
- evidence and documentation agree with the exact final commit;
- the draft PR remains unmerged until explicit approval.

---

## 16. Cross-cutting invariants

Every gate preserves these rules unless a separate approved design changes
them:

- worker protocol version `1`, serialized names, enum values and additive-field
  compatibility;
- stable diagnostic codes and first-failure precedence;
- cancellation timeout/forced termination remains operational failure;
- creation-time Job Object containment and exact inherited-handle allowlist;
- no uncontained process-launch fallback;
- absolute contained executable resolution and reparse rejection;
- allowlisted child environment with no model path or secret forwarding;
- bounded strict UTF-8 framing and bounded stderr retention with continuous
  draining;
- production/test-fixture source and package separation;
- model-path, template, exception-chain and artifact privacy;
- source-model byte integrity;
- no HTTP server, TCP listener or required network service;
- no XAML type in shared/runtime/worker infrastructure;
- no LLamaSharp/native type in application contracts or UI state;
- only Ready and ReadyWithWarnings may progress to Hardware Fit.

---

## 17. Error and recovery model

Model outcomes and execution outcomes remain separate.

### Model outcomes

- `Ready`
- `ReadyWithWarnings`
- `ConversionRequired`
- `Unsupported`
- `IncompletePackage`
- `Invalid`

### Execution outcomes

- `Cancelled`
- `OperationalFailure`

The worker returns evidence or an operational terminal result. It never chooses
a user-facing model outcome.

The application service validates evidence consistency before classification.
Cancellation and operational failure bypass classification. Unknown unsafe
evidence fails closed as an operational failure.

Every blocking UI state provides a safe recovery action such as retry, choose
another model, view privacy-safe details or follow a registered downstream
route.

---

## 18. Traceability model

Traceability runs in both directions:

```text
controlled workflow / requirement
    -> roadmap gate
    -> implementation boundary
    -> verification-matrix row
    -> test and retained evidence

test and retained evidence
    -> implementation boundary
    -> roadmap gate
    -> controlled workflow / requirement
```

Each gate records:

- entry condition;
- owned responsibilities and forbidden responsibilities;
- source and test roots;
- relevant workflow/requirement identifiers;
- realistic failure modes;
- test layers;
- security/privacy relevance;
- exact evidence required;
- explicit non-claims and deferrals;
- final status.

Allowed roadmap statuses are:

- `not started`;
- `blocked` with the exact blocker;
- `in progress`;
- `verified` with exact-head evidence;
- `deferred` with owner, destination and rationale.

---

## 19. Test and evidence architecture

The project keeps distinct evidence layers:

1. pure/unit tests for validators, mappings, classifiers and state machines;
2. contract/fitness tests for dependencies, serialization and workflow
   discovery;
3. transport tests for bounded byte/framing behavior;
4. WorkerClient tests for orchestration and Windows policy abstractions;
5. real-process tests for CreateProcessW, pipes, Job Objects and cleanup;
6. worker-host tests for protocol state and engine ownership;
7. packaged WinUI/UI-thread tests for XAML, lifecycle and binding behavior;
8. contained native tests for LLamaSharp/native feasibility;
9. trusted controlled-model tests for exact factual evidence;
10. manual Windows accessibility and operational acceptance where automation is
    insufficient.

Coverage is a gap detector, not a score target. A high percentage does not
replace failure-mode, boundary, mutation or end-to-end evidence.

Every closure record distinguishes:

- fresh execution from historical evidence;
- verification from validation, evaluation and benchmarking;
- test method count from actual parameterized execution count;
- green badge from independently reviewed raw artifacts.

---

## 20. Documentation safeguards

- Historical evidence is preserved and not rewritten into current claims.
- The LLamaSharp coverage matrix remains a separate specialist register.
- Audit findings are not described as executed test results.
- Future requirements are not described as implemented.
- Superseded architecture is retained with a pointer to the later decision.
- No requirement ID is invented.
- No `TBD`, vague “add more tests” or unowned deferral is permitted.
- Current source/evidence commit identities remain distinct.
- Every “verified” status includes exact commit and execution evidence.
- Every document uses repository-relative links where practical.

---

## 21. Risks and decision gates

| Risk | Control |
|---|---|
| completeness work expands into Gate 3 | classify every row and preserve the current/future boundary |
| cleanup changes protected behavior | characterize first and preserve protocol/security invariants |
| duplicated LLamaSharp implementation | extract once and make the spike and worker share it |
| packaging launches the wrong binary | fixed contained root, identity verification and no fallback |
| worker evidence decides UX policy | keep classification in application layer |
| stale callback overwrites a newer run | immutable run identity and callback suppression |
| cancellation is reported as success | preserve cooperative Cancelled versus forced OperationalFailure |
| accessibility is inferred from XAML properties | combine packaged automation with manual Windows evidence |
| privacy scan runs but unsafe artifact uploads | make every upload depend on the appropriate scan and execution result |
| test count creates false confidence | use behavior traceability, branch analysis and selective mutation |
| downstream work is absorbed into Model Inspection | keep handoff contracts separate from downstream execution |

Any proposed public/protocol/security behavior change receives its own design
approval before implementation.

---

## 22. Specification acceptance criteria

This roadmap specification is acceptable when:

- the completion boundary ends at validated outcomes and handoff contracts;
- all remaining cleanup and production gates are ordered with entry/exit
  meaning;
- current implementation, future requirements and downstream work are clearly
  separated;
- the test-completeness audit summary is represented accurately;
- no historical evidence is upgraded into a fresh claim;
- protected process, protocol, privacy and integrity guarantees are explicit;
- accessibility requires executable and manual evidence;
- the three-document structure has one clear owner for each kind of content;
- no placeholder, contradiction or ambiguous ownership remains;
- the next action after approval is the row-level verification matrix followed
  by the exact test-completeness implementation plan.

---

## 23. Engineering basis

Repository sources used:

- [Model Inspection worker integration design](./2026-08-05-model-inspection-worker-integration-design.md)
- [Gate 2 worker/process design](./2026-08-05-model-inspection-worker-gate-2-host-process-adapter-design.md)
- [cleanup design](./2026-08-06-model-inspection-cleanup-design.md)
- [cleanup master plan](../plans/2026-08-06-model-inspection-cleanup-master.md)
- [LLamaSharp runtime coverage matrix](../../testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)
- Gate 1, Gate 2, Phase 0 and Phase 1 evidence under
  `docs/testing/evidence/`
- current production, test, fixture, workflow and documentation source at
  commit `8f00a64a40e916872abfd6b72721ef86f223bab9`

External read-only project sources used:

- Project Workflow Register v1.2;
- `WF-IMP-001`, `WF-INS-001`, the four Model Overview workflows,
  `WF-CONV-SCR-001` and `WF-DIAG-SCR-001`;
- Project Testing Standard;
- Full Project Summary;
- Full Implementation Guide.

Current official Microsoft Windows accessibility guidance was used for the
automation-plus-manual evidence model, keyboard/focus expectations, text
scaling and live-region verification.

`windows-apps.pdf` was present but was not text-extracted in the audit
environment, so this specification does not claim it as a read source. No
unavailable textbook is claimed as read.
