# Decision 3 — Planning-Context Policy Implementation Plan

**Decision:** 3 of 8
**Status:** Implementation blocked at the entry gate
**Plan date:** 2026-08-19
**Audit date:** 2026-08-19
**Repository:** `arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant`
**Reviewed repository base:** `main@c417efd936a7fa2e871b689065b2f3b88636c1a1`
**Spec:** `docs/superpowers/specs/2026-08-19-model-hardware-compatibility-planning-context-design.md`
**Current authorised action:** Preserve the plan only; do not implement production code

> This plan becomes executable only after its entry gate passes. A future worker must
> re-read the repository and the live CI workflow at the exact implementation base
> before following any command or file path below.

---

## 1. Goal

Implement the pure, deterministic Decision 3 policy that:

```text
ApplicationDefault
→ min(4,096, trusted declared model context limit)

UserRequested
→ exact request preserved
→ never silently clamped

missing or unsupported model limit
→ typed NotEstablished result
```

The policy records `PlanningContext / planning-context-v1` and performs no I/O,
hardware inspection, resource estimation, candidate generation, UI work, or runtime
execution.

---

## 2. Why execution is currently blocked

The reviewed base does not contain the accepted Decision 1 and Decision 2
implementations or the production Hardware Inspection handoff required by the
integrated Block 3 request seam.

A future worker must not make the plan compile by creating temporary or duplicate:

```text
ContextTokenCount
CompatibilityContextRequest
CompatibilityContextMode
CompatibilityPolicyIdentity
CompatibilityPolicyKind
CompatibilityPolicyVersion
ModelInspectionHandoff
HardwareInspectionHandoff
ModelHardwareCompatibilityRequest
```

The correct response to a missing predecessor is to stop and integrate that
predecessor, not to guess its namespace or reproduce its shape.

---

## 3. Hard entry gate

Complete every item in this section before creating a Decision 3 source file.

### 3.1 Establish one immutable integrated base

Record:

```text
repository
base branch
base commit SHA
Decision 1 accepted commit SHA
Decision 2 accepted commit SHA
HardwareInspectionHandoff accepted commit SHA
date and reviewer
```

The base must contain all three accepted predecessor packages. It must not be the
historical Model Inspection branch merely because that branch was used during early
design.

### 3.2 Verify canonical predecessor files

Confirm the accepted Decision 1 and Decision 2 design and plan files exist at their
canonical paths:

```text
docs/superpowers/specs/
  2026-08-19-model-hardware-compatibility-input-contract-design.md
  2026-08-19-model-hardware-compatibility-result-contract-design.md

docs/superpowers/plans/
  2026-08-19-model-hardware-compatibility-input-contract-implementation.md
  2026-08-19-model-hardware-compatibility-result-contract-implementation.md
```

Confirm the production contracts exist under their approved owners. Record their
actual paths and namespaces instead of relying on this planning document.

### 3.3 Prove there are no duplicate owners

Run repository searches for every reused type. Review every match.

```powershell
git grep -n -E `
  "class ContextTokenCount|record ContextTokenCount|struct ContextTokenCount|" `
  "class CompatibilityContextRequest|record CompatibilityContextRequest|" `
  "enum CompatibilityContextMode"

git grep -n -E `
  "class CompatibilityPolicyIdentity|record CompatibilityPolicyIdentity|" `
  "enum CompatibilityPolicyKind|class CompatibilityPolicyVersion|" `
  "record CompatibilityPolicyVersion"

git grep -n -E `
  "class ModelInspectionHandoff|record ModelInspectionHandoff|" `
  "class HardwareInspectionHandoff|record HardwareInspectionHandoff|" `
  "class ModelHardwareCompatibilityRequest|record ModelHardwareCompatibilityRequest"
```

Expected result: exactly one canonical definition of each type. Multiple definitions,
ambiguous ownership, or missing definitions close the gate.

### 3.4 Verify the clean baseline

Re-read `.github/workflows/build-and-test.yml` at the exact base. Run its complete
restore, build, fixture, packaged-test, TRX-validation, privacy, and artifact checks
without removing existing gates.

The reviewed `main` workflow currently uses:

```text
MSBuild Release x64 for the WinUI application
dotnet build for the test source
the generated .build.appxrecipe
vstest.console.exe app-container execution
TRX counter and required-class validation
fixture reproducibility
```

The live workflow at implementation time is authoritative.

### 3.5 Establish the focused red/green runner

Try the fastest supported route without changing project semantics.

A direct filtered `dotnet test` command is accepted only when retained output proves:

```text
the intended Decision 3 class was discovered
at least one test was executed
the expected red or green outcome occurred
```

If the packaged WinUI test project does not support that route, use the existing
app-container runner with a supported class filter, or run the complete packaged
suite. Do not convert the project into a different test architecture merely to obtain
a convenient command.

Record the successful focused command and its tool versions in the implementation
evidence.

### 3.6 Entry-gate disposition

Create a short evidence record with exactly one disposition:

```text
Pass
→ implementation may begin

Blocked
→ stop and name the missing or failing prerequisite
```

No partial production commit is allowed after a blocked disposition.

---

## 4. Isolated implementation workspace

After the gate passes:

1. create an isolated worktree or equivalent workspace;
2. create a new Decision 3 feature branch from the recorded base SHA;
3. verify the working tree is clean;
4. run the accepted baseline test route once more;
5. retain the starting SHA in the branch evidence.

Suggested branch name:

```text
feature/model-hardware-compatibility-planning-context
```

Do not implement directly on `main`, a historical stacked branch, or a branch with
unrelated changes.

---

## 5. File map

Use the actual namespaces and paths established by the entry gate. The expected
Decision 3 package is:

### 5.1 Production files

```text
IBM Granite with TurboQuant (Intel)/
└── Features/
    └── ModelHardwareCompatibility/
        └── Application/
            └── PlanningContext/
                ├── ICompatibilityPlanningContextPolicy.cs
                ├── CompatibilityPlanningContextPolicy.cs
                ├── CompatibilityPlanningContextPlan.cs
                ├── CompatibilityPlanningContextResolution.cs
                └── CompatibilityPlanningContextEnums.cs
```

### 5.2 Test files

```text
tests/UnitTests/GraniteEdgeAI.UnitTests/
└── Features/
    └── ModelHardwareCompatibility/
        ├── CompatibilityPlanningContextContractTests.cs
        └── CompatibilityPlanningContextPolicyTests.cs
```

### 5.3 Documentation and evidence files

Modify only after production tests are green:

```text
IBM Granite with TurboQuant (Intel)/
└── Features/
    └── ModelHardwareCompatibility/
        └── README.md

.github/workflows/build-and-test.yml

the accepted Decision 1 and Decision 2 documents at their canonical paths

docs/evidence/requirements/F-M09/
└── decision-3-planning-context-policy.md
```

Do not create a temporary orchestrator. The real Compatibility orchestration plan
will consume this policy after the later estimation, supported-matrix, candidate,
ranking, and classification decisions exist.

---

## 6. Global implementation constraints

- Reuse all Decision 1 and Decision 2 types; do not recreate or wrap them.
- Keep `DefaultTargetTokens` equal to `4_096` for `planning-context-v1`.
- Preserve an explicit user request exactly.
- Do not silently clamp, round, bucket, or normalise a requested token count.
- Treat the trusted declared model limit as the version-one upper bound.
- Do not add automatic RoPE or YaRN extension.
- Return typed `NotEstablished` for missing, zero, or numerically unsupported limits.
- Keep the policy pure, deterministic, stateless, thread-safe, and side-effect free.
- Do not add hardware, memory, estimator, process, file, UI, network, clock, or
  configuration dependencies.
- Do not generate candidate context values; Decision 8 owns that ladder.
- Add beginner-readable comments for each logical block and every non-obvious
  invariant. Comments explain why a rule exists, not merely what the syntax says.
- Never log a model path, hardware identity, command line, stdout, stderr, token,
  credential, or user/machine identity.
- Do not push a branch that exposes a deliberately incomplete valid request path.

---

## 7. Test and commit strategy

Every production behaviour starts with a focused failing test. However, the remote
review history must never expose a policy that accepts `ApplicationDefault` while
throwing only because `UserRequested` is scheduled for the next task.

Use this sequence:

```text
1. add all version-one policy-path tests
2. observe the expected red result
3. implement all version-one policy paths coherently
4. observe the focused green result
5. add negative invariant tests
6. refactor while green
7. run wider and packaged regression
8. commit the complete behaviour
```

Local red/green commits may be used during development, but squash or keep them
local before review. Do not commit an `InvalidOperationException` such as
"UserRequested support is implemented in the next test step."

Recommended reviewable commit groups:

```text
test/feat: add complete planning-context contracts
test/feat: implement complete planning-context policy
test: guard planning-context purity
docs: record Decision 3 evidence and CI discovery
```

Each remote commit must leave every valid public request mode implemented and tested.

---

## 8. Task 1 — Add enums and resolution contracts

### 8.1 Failing tests

Create `CompatibilityPlanningContextContractTests` and first prove:

```text
all enums reserve zero for Unspecified
only the approved version-one enum members exist
Resolved carries a non-null plan and no reason
NotEstablished carries no plan and one non-Unspecified reason
null plan is rejected
Unspecified failure reason is rejected
contradictory resolution payloads cannot be constructed
```

Run the focused command established by the entry gate and retain the expected red
result.

### 8.2 Minimum implementation

Add:

```text
CompatibilityContextLimitRelationship
CompatibilityPlanningContextResolutionStatus
CompatibilityPlanningContextFailureReason
CompatibilityPlanningContextResolution
```

Use named factories:

```text
Resolved(plan)
NotEstablished(reason)
```

Keep the constructor private so invalid combinations cannot be assembled elsewhere.

### 8.3 Green and commit

Run:

```text
focused contract tests
all Decision 3 tests currently present
```

Commit only when every test passes and no unrelated file changed.

---

## 9. Task 2 — Add the immutable plan and invariants

### 9.1 Failing tests

Add positive construction tests for:

```text
valid ApplicationDefault plan
valid UserRequested plan within the model limit
valid UserRequested plan above the model limit
```

Add negative tests for:

```text
Unspecified mode
Unspecified relationship
null token objects
wrong policy kind
wrong policy version where version validation is owned here
ApplicationDefault carrying RequestedContextTokens
ApplicationDefault baseline above model limit
ApplicationDefault preservation target differing from baseline
UserRequested without RequestedContextTokens
UserRequested baseline differing from requested value
UserRequested preservation target differing from requested value
relationship disagreeing with token counts
```

### 9.2 Minimum implementation

Add `CompatibilityPlanningContextPlan` as a deeply immutable value. Its constructor
validates every cross-property rule before assigning a property.

Do not expose setters, mutable collections, or a parameterless deserialisation path.

### 9.3 Green and commit

Run the focused contract tests, then all currently available non-UI/pure tests through
the supported runner. Commit the complete immutable plan and its tests together.

---

## 10. Task 3 — Implement the complete pure policy

### 10.1 Add all failing policy-path tests first

Create `CompatibilityPlanningContextPolicyTests` covering:

```text
ApplicationDefault + 131,072
→ Resolved / 4,096

ApplicationDefault + 4,096
→ Resolved / 4,096

ApplicationDefault + 2,048
→ Resolved / 2,048

UserRequested 16,384 + 131,072
→ exact request / WithinModelLimit

UserRequested 16,384 + 8,192
→ exact request / ExceedsModelLimit

missing limit
→ NotEstablished / ModelContextLimitUnavailable

zero limit
→ NotEstablished / ModelContextLimitUnavailable

int.MaxValue limit
→ valid supported limit

int.MaxValue + 1
→ NotEstablished / ModelContextLimitOutOfRange

null request
→ ArgumentNullException

same input repeated
→ value-equivalent output

Identity
→ PlanningContext / planning-context-v1
```

Run the focused suite and retain the expected red result.

### 10.2 Add the interface

Add only:

```csharp
internal interface ICompatibilityPlanningContextPolicy
{
    CompatibilityPolicyIdentity Identity { get; }

    CompatibilityPlanningContextResolution Resolve(
        CompatibilityContextRequest request,
        ulong? declaredModelContextLength);
}
```

A genuine interface is justified because later orchestration will consume a versioned
policy boundary and tests may replace it. Do not add additional interfaces for the
plan or resolution values.

### 10.3 Implement every version-one path

Implement the complete algorithm in one coherent production change:

```text
validate request
resolve trusted model limit
if limit cannot be represented
    return typed NotEstablished

if ApplicationDefault
    baseline = min(4,096, limit)
    return Resolved default plan

if UserRequested
    preserve exact request
    compare request with limit
    return Resolved user plan
```

There is no temporary fallback branch and no "implemented next" exception.

### 10.4 Green and commit

Run:

```text
focused policy tests
focused contract tests
all Decision 3 tests
the supported wider test layer
```

Only then commit the complete policy behaviour.

---

## 11. Task 4 — Prove policy purity

### 11.1 Reflection guards

Add tests that prove:

```text
the concrete policy has one parameterless constructor
the declared public surface contains only Identity and Resolve
the policy is sealed
the policy stores no mutable instance state
```

Avoid brittle tests of private method names or harmless implementation detail.

### 11.2 Dependency guards

Use architecture tests or source scans to reject references from the Decision 3 folder
to:

```text
HardwareSnapshot
HardwareInspection
SystemMemorySnapshot
memory estimator
candidate generator
ProcessStartInfo
FileStream
Microsoft.UI.Xaml
HttpClient
DateTime, DateTimeOffset, Stopwatch, or random sources
```

Review matches manually so comments explaining a rejected dependency do not create
false failures.

### 11.3 Determinism and concurrency

Where practical, run the same valid inputs repeatedly and concurrently and compare
the value outputs. The test must not depend on timing.

### 11.4 Green and commit

Run the complete Decision 3 suite. Commit the purity guards separately so reviewers
can see the architectural protection clearly.

---

## 12. Task 5 — Documentation, traceability, and CI discovery

### 12.1 Feature README

Explain in beginner-readable terms:

```text
ApplicationDefault = min(4,096, declared model limit)
UserRequested = exact request, never silently clamped
missing model limit = not established
no context extrapolation in version one
maximum safe context is calculated later
```

State explicitly that `4,096` is not a model limit, maximum-safe result, runtime
verification, or fit guarantee.

### 12.2 Decision references

Update the accepted Decision 1 and Decision 2 documents to link to the implemented
Decision 3 policy without claiming that Decisions 4–8 are complete.

### 12.3 Requirement evidence

Create the F-M09 evidence record with:

```text
requirement and decision identifiers
base and implementation SHAs
policy version
test class names
test runner and tool versions
TRX counters
relevant commit and PR
non-claims
reviewer
```

Do not include raw local paths, machine identity, tokens, private UCL information,
or unredacted command output.

### 12.4 CI discovery

Update the live `.github/workflows/build-and-test.yml` only as needed to prove the
two Decision 3 classes are discovered and pass in the existing packaged run.

The current workflow's `$requiredClasses` mechanism is a suitable pattern. Preserve
all existing required classes and append the fully qualified Decision 3 class names.
Do not replace the app-container route with direct `dotnet test`.

### 12.5 Green and commit

Run the complete authoritative packaged suite and independently parse the retained
TRX. Commit documentation, traceability, and CI discovery after the evidence exists.

---

## 13. Task 6 — Final verification gate

### 13.1 Source-boundary scans

Run searches for forbidden implementation leakage:

```powershell
git grep -n -E `
  "HardwareSnapshot|HardwareInspection|SystemMemorySnapshot|" `
  "ProcessStartInfo|FileStream|Microsoft\.UI\.Xaml|HttpClient|" `
  "DateTimeOffset\.UtcNow|DateTime\.UtcNow|Stopwatch|Random" -- `
  "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/PlanningContext"
```

Run searches for forbidden Decision 8 behaviour:

```powershell
git grep -n -E `
  "context ladder|candidate context|1_024|2_048|8_192|16_384|32_768" -- `
  "IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/Application/PlanningContext"
```

Review every match. Test data and explanatory comments are not automatically
production violations.

### 13.2 Formatting and diff checks

Run the repository-supported formatting check and:

```powershell
git diff --check
git status --short
```

Expected result:

```text
no formatting changes required
no whitespace errors
only intended Decision 3 files changed
```

### 13.3 Complete CI-equivalent validation

Run every current workflow gate, including:

```text
fixture reproducibility
restore
WinUI Release x64 build
test-project build
packaged app-container test execution
TRX non-zero discovery/execution checks
all-pass counter checks
required-class checks
privacy and artifact checks present on the base
```

Do not infer success from a green badge alone. Download and independently inspect the
retained TRX or equivalent structured artifact.

### 13.4 Design review checklist

Confirm:

```text
one policy interface
one policy implementation
one immutable plan
one resolution envelope
one enum file
complete default and user-requested paths
no silent clamp
no model-family lookup
no context extrapolation
no hardware dependency
no estimator dependency
no UI/process/file/network/time dependency
no context ladder
4,096 default target
planning-context-v1 identity
all negative invariants tested
Decision 3 test classes proven in packaged CI
```

### 13.5 Pull request

Open a detailed draft pull request. The body explains:

```text
why a context is required before Configure Model
why 4,096 is a planning baseline
why user requests are never silently clamped
why maximum safe context is later work
which predecessor commits were reused
which production and test files were added
all verification commands and counters
all non-claims
remaining risks and deferred Decisions 4–8
```

Do not mark the PR ready until independent review confirms the evidence and exact
head SHA.

---

## 14. Acceptance matrix

| Input | Expected Decision 3 result |
|---|---|
| ApplicationDefault, limit 131,072 | Resolved; baseline and preservation target 4,096 |
| ApplicationDefault, limit 4,096 | Resolved; baseline and preservation target 4,096 |
| ApplicationDefault, limit 2,048 | Resolved; baseline and preservation target 2,048 |
| UserRequested 16,384, limit 131,072 | Resolved; exact request; WithinModelLimit |
| UserRequested 16,384, limit 8,192 | Resolved; exact request; ExceedsModelLimit |
| Any valid request, limit absent | NotEstablished; ModelContextLimitUnavailable |
| Any valid request, limit zero | NotEstablished; ModelContextLimitUnavailable |
| Any valid request, limit `int.MaxValue + 1` | NotEstablished; ModelContextLimitOutOfRange |
| Contradictory plan payload | Synchronous construction exception |
| Invalid request object | Synchronous contract exception |

---

## 15. Implementation non-claims

Completing this plan does not establish:

```text
weight-memory estimation
KV-cache formula correctness
runtime overhead
RAM or VRAM reserves
maximum safe context
candidate context ladder
candidate ranking
backend or device support
GPU offload
expected performance
quality preservation
final compatibility outcome
Runtime Verification success
Hardware Inspection Gate 1 completion
```

Those claims require their own decisions and evidence.

---

## 16. Textbook basis

- **Systems Engineering: Principles and Practice**, Chapters 6, 11, 13, and 17:
  prerequisite control, explicit assumptions, risk reduction, and test traceability.
- **Fundamentals of Software Architecture**, Chapters 2, 3, 21, and 22:
  trade-off analysis, cohesive boundaries, architectural decision records, and risk
  visibility.
- **Designing Secure Software**, Chapters 2–4, 6–7, 10, and 12:
  trust-boundary analysis, least information, fail-secure contracts, design review,
  input validation, and security testing.
- **The Art of Unit Testing**, Chapters 7, 8, and 10: trustworthy and maintainable
  tests, appropriate test levels, and a deliberate test recipe.
- **Code Complete**, Chapters 3, 5, 8, 22, and 28: upstream prerequisites,
  information hiding, defensive programming, developer testing, and configuration
  management.
- **Build desktop apps for Windows**: retain the real WinUI 3 and Windows App SDK
  build, packaging, and app-container test environment.

---

## 17. Current disposition

```text
Design
→ approved in principle

Implementation plan
→ corrected and preserved

Implementation
→ blocked

Next permitted programme action
→ implement and integrate the accepted Decision 1 and Decision 2 packages and the
  required HardwareInspectionHandoff, then re-run Section 3 against one immutable
  base
```

No Decision 3 production file should be created before that disposition changes from
`Blocked` to `Pass`.
