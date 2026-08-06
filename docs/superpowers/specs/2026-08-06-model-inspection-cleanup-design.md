# Model Inspection Complete Cleanup and Review Design

**Status:** Approved conversational design; written specification awaiting user review  
**Date:** 2026-08-06  
**Branch:** `refactor/model-inspection-cleanup`  
**Stacked base:** `feature/model-inspection-worker-host`  
**Base commit:** `a4138a613dd643abe12858eec5d1c3beb09e95e7`  
**Scope decision:** complete Model Inspection work to date, including production code, tests, process fixtures, workflows, LLamaSharp feasibility tooling, scripts and supporting documentation  
**Refactoring level:** structured behaviour-preserving cleanup  

---

## 1. Purpose

The Model Inspection work now spans three stacked feature slices:

1. the WinUI presentation and LLamaSharp feasibility foundation from PR #44;
2. the production worker integration design and Gate 1 contracts from PR #45;
3. the hardened worker host and process adapter from PR #47.

Before Gate 3 introduces the real LLamaSharp runtime into the production worker, the existing work will receive a complete professional cleanup and review.

The objective is not to make the code shorter for its own sake. The objective is to make every existing responsibility easier to understand, safer to maintain, easier to test and ready for the next integration gate without weakening verified behaviour.

The cleanup priority order is:

```text
correctness
    ↓
security and integrity
    ↓
clarity
    ↓
testability
    ↓
brevity
```

A shorter implementation is accepted only when every higher priority remains equal or improves.

---

## 2. Selected approach

The work will use a subsystem-by-subsystem cleanup rather than one repository-wide edit.

```text
1. WinUI presentation, navigation and application contracts
2. shared contracts, protocol and transport
3. WorkerClient domain and Windows process infrastructure
4. production worker host and abnormal-process fixture
5. LLamaSharp feasibility implementation and test support
6. contract, unit, architecture, process and trusted-model tests
7. workflows, scripts, READMEs, ADRs and evidence
8. cross-system review and exact-head verification
```

Each subsystem follows the same sequence:

```text
review current code
    ↓
record concrete findings
    ↓
confirm behaviour with existing or new characterization tests
    ↓
make the smallest useful refactoring
    ↓
run focused tests
    ↓
run affected regressions
    ↓
update the review inventory
    ↓
commit only when green
```

This approach keeps changes understandable, makes failures easier to locate and prevents one large cleanup diff from hiding security-sensitive changes.

---

## 3. Branch and pull request strategy

All cleanup work occurs on:

```text
refactor/model-inspection-cleanup
```

The branch is stacked on:

```text
feature/model-inspection-worker-host
```

The original PR histories will not be rewritten. The cleanup will be presented through a separate draft pull request so that:

- every refactoring can be reviewed independently;
- the existing Gate 1 and Gate 2 evidence remains traceable;
- rollback remains straightforward;
- unrelated Model Import, workbook and research work does not enter the diff;
- later Gate 3 work starts from a clean reviewed boundary.

If one cleanup phase becomes too large for meaningful review, it must be split into a stacked child pull request rather than forcing an oversized diff.

---

## 4. Complete scope

### 4.1 Included production and presentation code

The review includes all Model Inspection-related application code introduced or changed through PRs #44, #45 and #47, including the connected Model Import and Onboarding handoff where it participates in Model Inspection.

Included areas:

```text
IBM Granite with TurboQuant (Intel)/Features/ModelInspection/
IBM Granite with TurboQuant (Intel)/Features/ModelImport/
    only files involved in Model Inspection request creation and navigation
IBM Granite with TurboQuant (Intel)/Features/Onboarding/
    only files involved in Model Inspection navigation and stage state
shared/GraniteEdgeAI.ModelInspection.Contracts/
shared/GraniteEdgeAI.ModelInspection.Transport/
infrastructure/GraniteEdgeAI.ModelInspection.WorkerClient/
workers/GraniteEdgeAI.ModelInspection.Worker/
```

### 4.2 Included engineering and test code

```text
tools/ModelInspection.LlamaSharpSpike/
tools/ModelInspection.LlamaSharpSpike.Tests/
tools/ModelInspection.LlamaSharpSpike.NativeIntegrationTests/
tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
tools/ModelInspection.LlamaSharpSpike.TestSupport/
tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.Transport.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.Worker.Tests/
tests/UnitTests/GraniteEdgeAI.ModelInspection.WorkerClient.Tests/
tests/IntegrationTests/GraniteEdgeAI.ModelInspection.WorkerProcess.Tests/
tests/ProcessFixtures/GraniteEdgeAI.ModelInspection.ProtocolTestWorker/
relevant tests in tests/UnitTests/GraniteEdgeAI.UnitTests/
```

### 4.3 Included automation and documentation

```text
.github/workflows/build-and-test.yml
.github/workflows/llamasharp-feasibility-smoke.yml
.github/workflows/llamasharp-real-model-integration.yml
.github/workflows/model-inspection-transport-tests.yml
.github/workflows/model-inspection-worker-tests.yml
.github/workflows/model-inspection-worker-client-tests.yml
.github/workflows/model-inspection-worker-process-tests.yml
relevant scripts and fixture generators
source-adjacent READMEs
Model Inspection ADRs
Model Inspection specifications and plans
Model Inspection runbooks and coverage matrices
Model Inspection evidence indexes and current-state documents
PR descriptions for the reviewed stacked work
```

Historical evidence will be checked for correct context and links but will not be rewritten to imply that an old run tested new code.

### 4.4 Explicitly outside scope

This cleanup does not implement:

- the Gate 3 production LLamaSharp engine;
- new GGUF inspection behaviour;
- model outcome classification;
- `ModelInspectionService`;
- the final Model Inspection ViewModel;
- worker packaging or MSIX closure;
- Hardware Fit or LLM Fit;
- OpenVINO inspection;
- chat or `llama-cli` integration;
- TurboQuant, PolarQuant, QJL or TurboVec;
- x86, ARM64 or GPU worker support;
- unrelated Model Import visual redesign;
- unrelated workbook or benchmark changes.

A real defect discovered inside the cleanup scope may be corrected, but the correction must be identified as a defect fix and protected by a focused regression test.

---

## 5. Behaviour and compatibility that must not change

The cleanup must preserve:

- worker protocol version and JSON field compatibility;
- enum serialization and completion-state meanings;
- public contract meaning;
- stable infrastructure diagnostic codes;
- first-failure preservation;
- cooperative cancellation semantics;
- forced termination as operational failure rather than successful cancellation;
- startup, overall and cancellation-grace timeout behaviour;
- creation-time Job Object containment;
- exact inherited-handle allowlist;
- no uncontained launch fallback;
- trusted executable containment and architecture checks;
- child-environment allowlist and secret exclusion;
- bounded strict UTF-8 framing;
- bounded retained stderr with continued draining;
- model-path, chat-template and diagnostic privacy rules;
- production/test-fixture separation;
- current WinUI presentation and navigation behaviour;
- real-model evidence and original-model integrity rules;
- no HTTP server, TCP listener or required network access;
- all currently verified test behaviour.

Renames of internal types and methods are allowed. Changes to public contracts, protocol representations or security guarantees require a separate approved design and are not part of this cleanup.

---

## 6. Definition of clean and simple

### 6.1 One clear responsibility per unit

Every class and file must have one understandable primary responsibility.

For each production type the review must answer:

```text
what does this type own
what does it not own
what input does it accept
what output does it produce
what can fail
what must be disposed
what does it depend on
how is it tested
```

A class is split only when the extracted responsibility has a clear name, a stable boundary and independent tests. File count is not a quality metric.

### 6.2 Names explain intent

Namespaces, types, interfaces, methods, properties, enums and important local variables must use consistent domain language.

The review will question vague names such as:

```text
Helper
Manager
Handler
Processor
Data
Utils
```

Established public names remain stable unless a separate compatibility decision approves a change. Internal renaming is permitted where it removes genuine ambiguity.

Boolean names must read as clear conditions. Failure, state and ownership names must distinguish model outcomes from infrastructure failures and production behaviour from abnormal test behaviour.

### 6.3 Readable top-to-bottom control flow

A public or orchestration method should show the main sequence before low-level detail.

Methods will be checked for:

- suitable guard clauses;
- limited nesting;
- consistent abstraction level;
- meaningful intermediate values;
- no hidden side effects;
- no repeated validation;
- no unnecessary mutable state;
- no unclear boolean arguments;
- correct async and cancellation flow.

There is no arbitrary line-count limit. A longer method may remain when splitting it would obscure process sequencing, resource ownership or failure precedence.

### 6.4 Duplication is removed only when sharing improves clarity

Repeated behaviour may be consolidated when:

- the behaviour is genuinely identical;
- the shared responsibility has a clear domain name;
- security rules remain visible;
- failure messages remain useful;
- unrelated tests do not become coupled;
- setup conditions are not hidden.

Some duplication in tests is acceptable when it makes each scenario easier to understand.

### 6.5 Failure paths are as readable as success paths

For every process and native-boundary operation, a reader must be able to determine:

```text
what failed
which diagnostic is returned
which resources remain owned
how cleanup occurs
whether cleanup changes the original failure
```

Special attention is required for:

- partial launch;
- handshake failure;
- malformed protocol;
- cancellation timeout;
- process-tree cleanup;
- stderr collection;
- handle disposal;
- sensitive exception chains;
- worker terminal and process-exit inconsistency.

---

## 7. Comment and documentation style

Comments must explain why code exists, not restate the code.

Useful comment subjects include:

- security decisions;
- ownership transfer;
- unusual Windows behaviour;
- failure precedence;
- why a simpler-looking approach is unsafe;
- why a test fixture intentionally behaves abnormally.

The user-selected source comment style is mandatory:

- simple English;
- lower-case first letter;
- no full stop at the end;
- no comment for obvious code.

Examples:

```csharp
// keep stderr draining after the retained limit to prevent pipe blocking

// preserve the first failure because cleanup errors are secondary

// do not include the model path in the child environment
```

The same style applies to block comments and XML documentation written or revised by this cleanup. Existing comments are changed only when unclear, stale, misleading or inconsistent with the code.

Repository documentation and pull request prose continue to use normal professional sentence grammar.

---

## 8. Complete review inventory and coverage ledger

A review inventory must be created at:

```text
docs/reviews/model-inspection-cleanup-inventory.md
```

The inventory source set is the deduplicated union of:

1. every changed filename in PR #44;
2. every changed filename in PR #45;
3. every changed filename in PR #47;
4. every current file beneath the included Model Inspection roots;
5. every current Model Inspection workflow, script, ADR, README, plan, specification, runbook, matrix and evidence index;
6. any file discovered through project references or workflow invocation that materially participates in the reviewed behaviour.

This union prevents a file from being missed because it was renamed, moved, added after an earlier PR or referenced indirectly.

Every inventory row records:

```text
file
subsystem
primary responsibility
risk level
review status
findings
changes made
behaviour preserved
tests covering it
verification evidence
deferred work and reason
```

Allowed final review statuses:

```text
reviewed — no change needed
reviewed — cleaned
reviewed — defect fixed
reviewed — duplication removed
reviewed — documentation corrected
reviewed — intentionally deferred with reason
```

No relevant file may remain unclassified.

Risk levels:

```text
critical
    native handles, process creation, containment, protocol and privacy

high
    worker lifecycle, cancellation, hashing, LLamaSharp and workflows

medium
    contracts, test infrastructure, navigation and presentation logic

low
    simple presentation models, README wording and formatting
```

The implementation plan must include a repeatable inventory-completeness check so a new, moved or renamed file cannot silently disappear from the ledger.

---

## 9. Review passes

### Pass 1 — repository structure and dependency direction

Verify:

- each folder has one clear purpose;
- files live in the correct layer;
- no production project depends on test code;
- no WinUI project depends directly on LLamaSharp or worker implementation;
- no shared contract project depends on application, WinUI or infrastructure code;
- the feasibility tool is clearly distinguished from production infrastructure;
- source-adjacent READMEs match actual ownership.

### Pass 2 — names and domain vocabulary

Review every important namespace, type, interface, method, property, enum and local variable for clarity and consistency.

### Pass 3 — class responsibility and state validity

Inspect for:

- classes with multiple unrelated jobs;
- invalid constructible states;
- unnecessary interfaces or wrappers;
- hidden dependencies;
- static helpers that conceal ownership;
- too many constructor dependencies;
- duplicated state across unrelated objects.

### Pass 4 — method readability and async correctness

Inspect guard clauses, nesting, side effects, abstraction level, cancellation propagation, exception handling and state mutation.

### Pass 5 — contracts and protocol compatibility

Verify:

- immutability where appropriate;
- constructor and JSON validation;
- nullability correctness;
- impossible-state prevention;
- required versus optional field clarity;
- duplicate-property rejection;
- unknown additive-field tolerance;
- sequence-validator consistency;
- stable diagnostic and enum values;
- unchanged protocol compatibility.

### Pass 6 — Windows interop and resource ownership

Every native or asynchronous resource must have an explicit owner:

```text
process handle
thread handle
job handle
pipe handles
attribute-list memory
environment-block memory
verified executable handle
managed streams
cancellation registrations
stdout and stderr drain tasks
```

For each resource verify:

- acquisition;
- initial owner;
- ownership transfer;
- lifetime;
- partial-failure cleanup;
- idempotent disposal;
- cleanup exceptions;
- first-failure preservation;
- release on every path.

No explicit ownership code may be replaced with a shorter abstraction unless the new design is at least as safe and easier to prove.

### Pass 7 — concurrency, cancellation and timeout races

Review:

- concurrent sessions;
- blocked stream reads and writes;
- cancellation before launch;
- cancellation during handshake;
- active cancellation;
- cancellation after terminal output;
- startup and overall timeout races;
- process-exit races;
- duplicate cleanup;
- stale task completion;
- semaphore and lock ownership;
- shared mutable state.

Race-sensitive behaviour must use deterministic tests where practical. Repeated timing-sensitive tests are used as supporting evidence, not as a substitute for deterministic design.

### Pass 8 — security and privacy

Review these trust boundaries:

```text
untrusted model file
untrusted worker stdout
untrusted worker stderr
untrusted executable path
child environment
native runtime
abnormal test fixture
retained CI artifact
```

Verify:

- no model path on command line or child environment;
- no sensitive path through exception chains;
- no complete chat-template retention;
- no inherited secrets;
- no executable `PATH` search;
- no HTTP or TCP listener;
- bounded input and retained diagnostics;
- continued draining after stderr retention fills;
- executable containment and reparse-point checks;
- exact runtime identity validation;
- fail-closed malformed protocol handling;
- no production abnormal-test switches.

### Pass 9 — LLamaSharp feasibility implementation

Each feasibility file must receive one future disposition:

```text
extract into Gate 3 runtime
keep as feasibility front end
keep as test support
replace after verified extraction
remove after verified migration
```

Review:

- reusable runtime logic;
- command-line-only logic;
- evidence writing;
- process containment;
- duplicated hashing or validation;
- oversized probe routines;
- runtime identity;
- unsafe native calls;
- responsibilities that should move to the future runtime library;
- responsibilities that must remain engineering-only.

This cleanup prepares the code for Gate 3 but does not perform the production runtime extraction unless a separate approved plan explicitly includes a necessary behaviour-preserving move.

### Pass 10 — tests and test support

Every test must be reviewed for:

- meaningful behaviour coverage;
- clear condition and expected outcome;
- understandable arrange, act and assert flow;
- deterministic input;
- useful failure messages;
- correct category and layer;
- no ordering dependence;
- no shared mutable state;
- reliable cleanup;
- justified timing assumptions;
- no false positive caused by zero discovery;
- no helper that hides an important scenario condition.

The test architecture remains separated into:

```text
contract tests
unit tests
architecture fitness tests
real-process integration tests
contained native tests
trusted real-model tests
```

These layers are not merged merely to reduce project count.

### Pass 11 — workflows and scripts

Treat workflows and PowerShell as production code.

Verify:

- correct triggers and path filters;
- least required permissions;
- immutable action versions;
- repository-selected SDK;
- sparse-checkout completeness;
- understandable restore, build, publish and test order;
- compatible Microsoft Testing Platform commands;
- minimum expected test counts;
- separate production and fixture publish roots;
- exact TRX generation;
- explicit timeouts;
- orphan-process checks;
- privacy scans;
- fail-closed artifact upload;
- no accidental model or sensitive evidence upload;
- diagnostic output sufficient to identify the first failure.

The known incompatible contract-test filter must be corrected and verified before structural cleanup begins.

### Pass 12 — documentation agreement

Compare:

```text
code
tests
workflow
specification
implementation plan
ADR
README
runbook
evidence
PR description
```

Correct:

- stale SDK versions;
- obsolete file names;
- incorrect test counts;
- inconsistent terminology;
- completed work described as pending;
- pending work described as complete;
- unsupported claims;
- missing limitations;
- design decisions that no longer match implementation.

Historical evidence keeps its original tested commit and result. New code receives new evidence.

---

## 10. Finding severity and resolution rules

Findings use four levels:

- **critical** — security, privacy, integrity, data loss, containment or crash risk;
- **important** — incorrect ownership, unreliable tests, misleading architecture or substantial maintenance risk;
- **minor** — local naming, comments, formatting or contained duplication;
- **suggestion** — optional improvement with no present maintenance cost.

Critical and important findings must be resolved before completion unless the user explicitly accepts a documented deferral. Every deferral must state:

- the reason;
- the risk;
- the safe current behaviour;
- the future gate or condition that will address it.

Findings must be recorded before changing complex code so the purpose of each refactoring remains traceable.

---

## 11. Baseline requirement

Before structural refactoring begins, the current branch must have a trustworthy executable baseline.

Required baseline actions:

1. correct the incompatible contract-test filter;
2. run all current Model Inspection contract, transport, worker, WorkerClient and process suites;
3. run the existing packaged WinUI regression suite;
4. record exact test counts and selected SDK;
5. confirm the privacy scan passes;
6. confirm no worker or fixture process remains;
7. retain the Gate 2 and packaged-test artifacts with identifiers, sizes and hashes;
8. complete the current Gate 2 final review.

The cleanup must not be blamed for a failure already present in the baseline.

---

## 12. Characterization tests and defect handling

Before changing complex behaviour, add a characterization test when existing coverage does not clearly preserve it.

Priority areas:

- partial Windows process creation failure;
- handle ownership transfer;
- first-failure preservation;
- cleanup failure as secondary evidence;
- cancellation races;
- stderr truncation with continued drain;
- terminal result and exit-code consistency;
- path-redaction exception chains;
- LLamaSharp evidence finalisation;
- workflow test discovery.

A test is added only when it protects a real behaviour or a discovered defect. Test count alone is not a quality target.

When a defect is found:

```text
reproduce with a focused failing test
    ↓
record the cause
    ↓
apply the smallest safe correction
    ↓
run focused and affected regression tests
    ↓
record it as a defect fix rather than a style refactoring
```

---

## 13. Cleanup phases and gates

### Phase 0 — baseline correction and inventory

- fix the workflow test-discovery issue;
- obtain green exact-head baseline evidence;
- create and verify the complete inventory;
- open the stacked draft cleanup PR;
- record initial findings without refactoring production behaviour.

### Phase 1 — WinUI, navigation and application contracts

Review Model Inspection presentation models, controls, page code-behind, Model Import request creation and Onboarding navigation.

Required evidence:

- focused presentation/navigation tests;
- application contract tests;
- packaged WinUI regression.

### Phase 2 — shared contracts, protocol and transport

Review immutable contracts, JSON handling, sequence validators, bounded UTF-8 reader/writer and associated tests.

Required evidence:

- contract tests;
- transport tests;
- worker and WorkerClient affected regressions;
- protocol compatibility review.

### Phase 3 — WorkerClient domain and Windows process infrastructure

Review trust policy, executable verification, environment policy, native structures, SafeHandle ownership, process creation, Job Object assignment, handle inheritance, session lifecycle, cancellation and failure accumulation.

Required evidence:

- WorkerClient unit tests;
- architecture fitness tests;
- complete real-process suite;
- repeated timing-sensitive subset;
- privacy and orphan checks.

This phase receives two manual reviews: correctness/readability and security/privacy.

### Phase 4 — production worker host and abnormal fixture

Review worker state machine, parent monitoring, terminal coordination, unavailable engine seam, exit codes and test-only abnormal scenarios.

Required evidence:

- worker unit tests;
- process fixture isolation tests;
- process integration tests;
- source scan proving no abnormal fixture switches entered production.

### Phase 5 — LLamaSharp feasibility implementation and test support

Review runtime identity, model safety, hashing, evidence collection, progress, cancellation, redaction, evidence writing, process runner and real-model support.

Required evidence depends on changed behaviour:

- deterministic unit tests;
- contained native tests;
- trusted real-model campaign where runtime or evidence behaviour changed;
- original model hash comparison;
- privacy and no-port checks.

### Phase 6 — test suite cleanup

Review and simplify test setup without hiding important scenario conditions. Preserve the separate test layers.

Required evidence:

- every affected suite;
- zero-test protection;
- deterministic cleanup;
- no orphan process;
- stable repeated runs for timing-sensitive scenarios.

### Phase 7 — workflows, scripts and documentation

Reconcile automation, READMEs, ADRs, plans, runbooks, evidence indexes and PR descriptions with final code.

Required evidence:

- workflow contract tests;
- script-focused tests where present;
- exact artifact and privacy checks;
- documentation link and terminology review.

### Phase 8 — final independent review and exact-head proof

Review the complete diff without relying on earlier phase notes.

Required outcomes:

- every inventory row complete;
- no unresolved critical or important finding;
- complete exact-head CI success;
- artifact metadata and hashes recorded;
- privacy scan passed;
- zero orphan processes;
- final PR body fully reconciled.

A phase cannot close until its focused tests, affected regressions, inventory entries and manual review are complete.

---

## 14. Commit discipline

Each commit must contain one coherent cleanup idea and explain its purpose.

Examples:

```text
refactor(model-inspection): clarify worker handshake validation
refactor(model-inspection): centralise process-session cleanup
test(model-inspection): simplify process scenario setup
docs(model-inspection): reconcile worker ownership guidance
```

Avoid vague messages such as:

```text
cleanup
fixes
refactor stuff
```

Commit and PR explanations must record:

- what was difficult to understand;
- what changed;
- why the new form is clearer;
- which behaviour remains unchanged;
- which tests prove that claim.

---

## 15. Prohibited cleanup patterns

Do not:

- run a repository-wide formatter that obscures meaningful edits;
- rename public contracts for preference alone;
- change protocol JSON names, values or meanings silently;
- merge unrelated abstractions to reduce file count;
- hide security checks inside vague generic helpers;
- replace explicit ownership with clever compact code;
- suppress analyzers without a documented technical reason;
- alter historical evidence to appear current;
- delete feasibility code before a verified Gate 3 replacement exists;
- combine production worker and abnormal fixture behaviour;
- mix unrelated feature or workbook work into the branch;
- claim improvement solely from fewer lines;
- use passing compilation as the only verification;
- introduce speculative abstractions for future gates.

---

## 16. Automated and manual quality gates

Use automated checks where they provide meaningful evidence:

- nullable reference analysis;
- warnings as errors in isolated Model Inspection libraries;
- relevant .NET analyzers;
- dependency and architecture fitness tests;
- protocol compatibility tests;
- forbidden dependency scans;
- focused formatting verification;
- minimum expected test counts;
- artifact privacy scans;
- orphan-process checks;
- exact-head workflow evidence.

Do not enable every possible analyzer rule without assessing its value for this repository.

Every subsystem also receives manual review for:

- clarity;
- cohesion;
- naming;
- ownership;
- failure handling;
- security and privacy;
- concurrency;
- test usefulness;
- documentation accuracy.

A subsystem is not complete merely because it compiles or has a green focused test.

---

## 17. Verification matrix

The required verification depth follows the changed boundary.

```text
presentation or navigation
    focused WinUI tests
    packaged application regression

application or worker contracts
    contract tests
    application contract tests
    affected consumers

protocol or transport
    contract tests
    transport tests
    worker tests
    WorkerClient tests
    process tests

Windows process infrastructure
    WorkerClient unit tests
    architecture fitness tests
    complete process suite
    repeated timing-sensitive subset
    orphan and privacy checks

worker host or fixture
    worker tests
    fixture isolation tests
    process suite

LLamaSharp feasibility runtime
    deterministic tests
    contained native tests
    trusted real-model campaign when required
    model hash comparison
    privacy and no-port checks

workflow or script
    workflow contract tests
    intended hosted workflow
    TRX and artifact validation
    zero-test protection
```

Existing unrelated warnings are not hidden or falsely claimed as corrected. Any warning introduced by Model Inspection code must be fixed or explicitly justified.

---

## 18. Independent final review

After all cleanup phases, perform a fresh complete review of:

- the full branch diff;
- every unresolved inventory entry;
- every changed public surface;
- every security-sensitive ownership path;
- test coverage and false-positive risk;
- workflow correctness;
- documentation agreement;
- unsupported claims;
- accidental scope expansion.

The review report lists findings first, ordered by severity. A statement such as “no blocking findings” is allowed only when the evidence supports it.

Security-sensitive code receives a distinct second review focused on ways the simplification could weaken containment, privacy, failure handling or resource cleanup.

---

## 19. Pull request and evidence requirements

The draft cleanup PR must contain:

- purpose and exact scope;
- stacked base and commit identity;
- complete subsystem list;
- before-and-after examples for important improvements;
- every meaningful rename and structural change;
- behaviours deliberately preserved;
- defects discovered and corrected;
- security and privacy review;
- exact test counts;
- workflow run and job identifiers;
- artifact names, IDs, sizes and hashes;
- privacy and orphan-process results;
- limitations and deferred work;
- Gate 3 preparation decisions;
- reviewer focus.

Final evidence must refer to the exact final commit. An earlier successful code commit is not sufficient for closing the cleanup branch.

---

## 20. Definition of done

The cleanup is complete only when:

- the complete inventory source set has been generated and checked;
- every relevant file has one final recorded disposition;
- every production type has a clear responsibility;
- important names have been reviewed;
- resource ownership and partial-failure paths have been reviewed;
- no known duplication remains without a documented reason;
- comments follow the approved simple style and explain non-obvious reasoning;
- no stale or misleading comment remains;
- tests are readable, deterministic and correctly layered;
- workflows are understandable and cannot pass through zero test discovery;
- protocol, security, privacy and cancellation behaviour are preserved;
- feasibility code has a recorded Gate 3 disposition;
- documentation agrees with the final code;
- no unresolved critical or important issue remains;
- the exact final commit passes the required complete verification campaign;
- privacy scanning passes;
- no worker or fixture process remains;
- final artifacts and hashes are recorded;
- the final PR description is complete and accurate.

---

## 21. Engineering basis

### Refactoring: Improving the Design of Existing Code

Use small behaviour-preserving transformations, characterization tests and frequent verification. Refactoring is not mixed invisibly with feature development.

### Code Complete: A Practical Handbook of Software Construction

Use meaningful names, cohesive routines, consistent abstraction, defensive boundaries, readable control flow and incremental integration.

### Designing Secure Software: A Guide for Developers

Preserve trust boundaries, least exposure, fail-closed behaviour, sensitive-data minimisation, explicit resource ownership and security-focused testing.

### The Art of Unit Testing

Keep unit, contract, process and trusted-runtime tests distinct. Use seams and test doubles without allowing test infrastructure to enter production code.

### Why Programs Fail: A Guide to Systematic Debugging

Establish a reproducible baseline, preserve the first proven failure and distinguish root cause from later cleanup symptoms.

### Fundamentals of Software Architecture

Use clear component boundaries, dependency-direction fitness tests, cohesive responsibilities and explicit trade-off decisions.

### Systems Engineering Principles and Practice

Maintain traceability from requirement to implementation, verification, evidence and final acceptance.

### windows-apps.pdf

Keep WinUI presentation separate from view-independent application logic and native-process infrastructure. Preserve controlled Windows packaging and lifecycle boundaries for later gates.

---

## 22. Final design decision

The Model Inspection cleanup will be a complete, inventory-driven, behaviour-preserving professional review of all work delivered through PRs #44, #45 and #47.

It will simplify internal structure where that improves understanding, but it will not trade away correctness, security, privacy, testability or evidence. Every relevant file will be reviewed, every important change will be tested and the exact final commit will receive full verification before the cleanup is declared complete.
