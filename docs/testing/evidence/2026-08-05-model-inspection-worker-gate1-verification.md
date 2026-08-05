# Model Inspection Worker Integration — Gate 1 Verification

**Gate:** 1 — contracts, protocol, immutable request handoff, and architectural boundary  
**Verification date:** 2026-08-05  
**Branch:** `feature/model-inspection-runtime-integration`  
**Verified code head:** `51bcd516c6905f767a2e7fe3e703dbb2c8642c4a`  
**Workflow:** `Build and test`  
**Workflow run:** `31026152100`  
**Workflow job:** `92375083011`  
**Result:** Passed

---

## 1. Purpose

Gate 1 establishes the stable, testable boundary required before a production worker process or LLamaSharp runtime is introduced.

The verified slice contains:

- immutable application-owned Model Inspection request and result contracts;
- a file-backed request factory at the Model Import navigation boundary;
- stale, changed, deleted, locked, directory, and non-GGUF selection rejection;
- request-based navigation from Model Import through the onboarding shell to Model Inspection;
- framework-neutral worker command, message, evidence, and runtime-identity contracts;
- strict JSON parsing and protocol sequence validation;
- separate application execution states and user-facing model outcomes;
- dependency-graph rules preventing WinUI or LLamaSharp from entering the shared contract project;
- CI enforcement of a meaningful contract-test floor;
- `ADR-003` for the protected Model Inspection worker boundary.

This evidence record verifies only Gate 1. It does not claim that the worker executable, LLamaSharp engine, classifier, service, ViewModel, packaging, or live inspection UI has been implemented.

---

## 2. Verified architecture boundary

```text
ModelImportPage
    ↓ ModelInspectionRequest
OnboardingShellPage
    ↓ same request instance
ModelInspectionPage

WinUI application
    → application-owned Model Inspection contracts
    → shared framework-neutral worker contracts

Shared worker contracts
    ✕ WinUI
    ✕ LLamaSharp
    ✕ native runtime types
```

The protected production direction remains:

```text
ModelInspectionPage
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
LLamaSharp 0.27.0 / matched llama.cpp CPU runtime
```

Only the first architecture block and the shared contracts required by the second block are implemented in Gate 1.

---

## 3. Hosted workflow results

The exact verified code head was checked out by GitHub Actions and reported as:

```text
51bcd516c6905f767a2e7fe3e703dbb2c8642c4a
```

### Fixture reproducibility

The workflow regenerated the GGUF fixtures and then checked both tracked and untracked fixture state.

```text
Result: passed
Tracked fixture changes: none
Untracked fixture changes: none
```

### Contract tests

```text
Project:
  tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/
  GraniteEdgeAI.ModelInspection.Contracts.Tests.csproj

Filter: TestCategory=Contract
Minimum required by CI: 41
Total: 64
Passed: 64
Failed: 0
Skipped: 0
Result: passed
```

The contract suite covers the shared dependency graph, protocol constants and records, strict JSON handling, command sequencing, message sequencing, progress monotonicity, terminal-result invariants, runtime identity, and workflow protection.

The workflow sparse checkout explicitly includes `.github/workflows`, allowing the source-level workflow contract test to inspect the real CI definition. The contract invocation uses:

```text
--minimum-expected-tests 41
```

This prevents the Gate 1 contract suite from silently collapsing to one or zero discovered tests.

### WinUI application build

```text
Configuration: Release
Platform/RID: x64 / win-x64
Build result: passed
Errors: 0
```

The hosted build reported existing XAML binding-notification warnings and one missing publish-profile warning during the packaged test-project build. They did not prevent compilation or test execution and are not treated as Gate 1 feature claims.

### Packaged WinUI/unit-test run

```text
Total: 207
Passed: 207
Failed: 0
Skipped: 0
Result: passed
```

This packaged run includes the Gate 1 request-factory and navigation regressions as well as the existing Model Import, GGUF scanner, presentation, onboarding, and contract tests compiled into the WinUI test application.

Notable Gate 1 regressions observed passing include:

- matching GGUF creates an immutable request;
- missing, changed-size, directory, non-GGUF, failed-scan, invalid-argument, and writer-held files fail closed;
- Continue raises the immutable request only after successful revalidation;
- modification or deletion after quick scan invalidates the selection without navigation;
- onboarding forwards the same request object;
- null onboarding request is rejected;
- Model Inspection receives the request and derives its displayed path from it;
- only Ready outcomes may continue to Hardware Fit;
- `ConversionRequired` without a verified conversion route is rejected;
- cancellation and operational failure remain separate from model outcomes.

---

## 4. Test artifact

```text
Artifact name: unit-test-results-31026152100-1
Artifact ID: 8938909593
Uploaded size: 54,247 bytes
SHA-256: efd536615529f44100ebaa98a15dfbc072a588283c5bcb9c6e54eb645ab02f39
Retention: 30 days
```

The artifact contains the packaged WinUI/unit-test TRX generated by the successful workflow job.

---

## 5. Security and privacy controls verified in Gate 1

Gate 1 verifies the following design and contract controls:

- the model path is held only in the application request and future worker start command, not in returned worker evidence;
- worker file evidence exposes a filename and canonical-path fingerprint rather than a canonical directory;
- complete chat-template text is not part of the worker evidence contract;
- application and shared contracts contain no native pointers or handles;
- the selected file is reopened read-only at the final navigation boundary;
- a concurrent writer is refused;
- changed, missing, deleted, or otherwise unstable selection state fails closed;
- technical navigation diagnostics retain the final filename rather than the full directory;
- strict JSON and sequence validators reject malformed or ambiguous protocol input;
- unknown worker observations are not silently accepted by the approved application contract.

This Gate 1 run does not execute a production worker, inspect process command lines, observe TCP listeners, or verify native resource disposal. Those controls belong to Gates 2 and 3.

---

## 6. Outcome and closure decision

Gate 1 acceptance is satisfied for the verified code head:

```text
[pass] pure shared contracts compile without WinUI or LLamaSharp
[pass] protocol records and constants are defined
[pass] strict JSON and state-machine tests pass
[pass] application execution and model-outcome contracts are separate
[pass] immutable quick-scan-to-inspection handoff is implemented
[pass] changed/deleted/locked selection fails closed
[pass] CI enforces the Gate 1 contract-test floor
[pass] WinUI application builds
[pass] packaged application/unit tests pass
[pass] ADR-003 and source-adjacent documentation record the selected boundary
```

**Gate 1 decision:** passed for code head `51bcd516c6905f767a2e7fe3e703dbb2c8642c4a`.

The next executable slice is Gate 2: worker host, bounded process transport, fake-engine seam, abnormal test worker, cancellation, timeout, and process-tree cleanup. Gate 2 must not introduce LLamaSharp yet.

---

## 7. Explicit non-claims

Gate 1 does not prove or claim:

- a production worker executable;
- a process adapter;
- a working hello handshake between two processes;
- worker crash or hang containment;
- cooperative process cancellation;
- forced process-tree cleanup;
- LLamaSharp metadata extraction;
- real Granite inspection through the production route;
- classifier or service behaviour;
- ViewModel integration or functional live progress;
- worker build, publish, or MSIX inclusion;
- x86 or ARM64 support;
- OpenVINO inspection;
- Hardware Fit or LLM Fit integration;
- chat inference;
- TurboQuant, PolarQuant, QJL, or TurboVec;
- performance, memory, quality, or compatibility conclusions.

---

## 8. Engineering basis

- **Fundamentals of Software Architecture** — stable component boundaries, dependency direction, cohesion, coupling, architecture fitness functions, and ADRs.
- **Code Complete** — information hiding, defensive interfaces, incremental integration, and developer testing.
- **Designing Secure Software** — fail-closed validation, trust boundaries, bounded untrusted input, and least exposure.
- **The Art of Unit Testing** — focused regression tests, test seams, and separation of contract tests from UI/process integration.
- **Why Programs Fail** — preserve the first reliable failure and keep infrastructure failure separate from a model conclusion.
- **Refactoring** — migrate the navigation contract in small behaviour-preserving steps rather than rewriting the complete flow.
- **Microsoft Windows application guidance (`windows-apps.pdf`)** — WinUI navigation lifecycle, packaged application testing, and separation between UI and non-UI logic.
