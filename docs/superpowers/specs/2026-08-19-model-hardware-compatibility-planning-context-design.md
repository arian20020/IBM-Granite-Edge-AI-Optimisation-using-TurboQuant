# Model–Hardware Compatibility Decision 3 — Planning-Context Policy Design

**Decision:** 3 of 8
**Status:** Design approved in principle; implementation blocked at the entry gate
**Decision date:** 2026-08-19
**Audit date:** 2026-08-19
**Repository:** `arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant`
**Reviewed repository base:** `main@c417efd936a7fa2e871b689065b2f3b88636c1a1`
**Scope:** Planning-context policy design and future implementation prerequisites only
**Production code authorised by this document:** None

---

## 1. Decision summary

Model–Hardware Compatibility needs a context size before the user reaches the later
Configure Model step. Decision 3 defines the small, deterministic policy that will
eventually supply that planning input.

The approved version-one rules are:

```text
ApplicationDefault
→ min(4,096, trusted declared model context limit)

UserRequested
→ preserve the exact positive token count
→ never silently clamp the request

Trusted model context limit missing
→ NotEstablished / ModelContextLimitUnavailable

Trusted model context limit outside the supported numeric range
→ NotEstablished / ModelContextLimitOutOfRange
```

The `4,096` value is only the application's conservative **planning baseline**. It is
not:

```text
the model's maximum context
the maximum context that fits this computer
a memory estimate
a runtime verification result
a compatibility verdict
a promise that inference will succeed
```

A request above the model-declared limit remains visible as the exact request and is
marked `ExceedsModelLimit`. Later candidate generation may investigate lower
alternatives, but Decision 3 must not quietly replace what the user asked for.

---

## 2. Why the design is approved but implementation is blocked

The policy itself is sufficiently defined to preserve as an architectural decision.
Implementation must not begin on the reviewed repository base because its required
owners and contracts are not yet present there.

The reviewed base does not contain the approved implementations of:

```text
Decision 1
├── ContextTokenCount
├── CompatibilityContextMode
├── CompatibilityContextRequest
├── ModelInspectionHandoff
└── ModelHardwareCompatibilityRequest

Decision 2
├── CompatibilityPolicyIdentity
├── CompatibilityPolicyKind
├── CompatibilityPolicyVersion
└── final Compatibility result/progress contracts

Hardware Inspection
├── canonical HardwareSnapshot
├── actionable HardwareInspectionHandoff
└── integrated Block 3 navigation seam
```

Hardware Inspection Gate 1 also remains blocked. A pure Decision 3 policy does not
read hardware directly, but implementing it before the trusted Block 3 request seam
exists would create an orphaned module and encourage temporary duplicate contracts.
The programme therefore keeps the implementation gate closed until Decisions 1 and
2 and the required Hardware Inspection handoff are integrated on one verified base.

This is a sequencing block, not a rejection of the policy.

---

## 3. Purpose and single responsibility

Decision 3 answers exactly one question:

> Which context size should the baseline compatibility assessment use, and which
> context intention must later candidate generation try to preserve?

It does not answer:

```text
How much memory will the model need?
Which runtime or backend should be used?
How many layers should be offloaded?
Which KV-cache format should be selected?
Which context sizes should become candidates?
What is the maximum safe context?
Will the model run?
Did the selected configuration run?
```

Those questions belong to later estimation, supported-matrix, candidate-generation,
ranking, classification, and Runtime Verification decisions.

---

## 4. Ownership map

The one-owner rule prevents the same fact or decision from being recomputed in
several features.

| Concern | Canonical owner |
|---|---|
| Inspected model facts and downstream model projection | Model Inspection |
| Factual machine snapshot and downstream hardware projection | Hardware Inspection |
| Explicit context intent in the compatibility request | Decision 1 |
| Policy identity/version representation | Decision 2 |
| Baseline context and preservation target | Decision 3 |
| Candidate context ladder | Decision 8 |
| Weight, KV-cache, runtime-overhead and reserve estimates | Later resource-estimation decisions |
| Maximum estimated safe context | Later candidate evaluation |
| User-facing compatibility outcome | Compatibility classifier/orchestrator |
| Proof that a selected configuration actually initialises and runs | Runtime Verification |

Hardware Inspection must never choose a context based on a model. Model Inspection
must never calculate hardware fit. Decision 3 must never inspect the machine.

---

## 5. Inputs

The future policy consumes only:

```text
CompatibilityContextRequest
+
trusted declared model context limit
```

### 5.1 Compatibility context request

Decision 1 owns the immutable request mode:

```text
ApplicationDefault
→ no user token value is carried

UserRequested
→ one positive ContextTokenCount is carried
```

Decision 3 must reuse the Decision 1 `ContextTokenCount` reference value object. It
must not introduce another integer wrapper, primitive token field, or default-zero
struct that can bypass validation.

### 5.2 Trusted declared model context limit

The model limit must come from authoritative inspected evidence carried by the
approved model-side boundary. Decision 3 must not infer it from:

```text
the filename
a model-family name
a hard-coded Granite lookup table
an online lookup
a runtime default
the user's requested context
hardware capacity
```

The version-one supported numeric representation is a positive `int` wrapped by
`ContextTokenCount`. A missing, zero, or greater-than-`int.MaxValue` source value
does not become a guessed number.

---

## 6. Selected policy

### 6.1 Application default

```text
baseline = min(4,096, declared model limit)
preservation target = baseline
relationship = WithinModelLimit
requested context = absent
```

Examples:

| Declared model limit | Baseline | Meaning |
|---:|---:|---|
| 131,072 | 4,096 | Use the conservative application baseline |
| 8,192 | 4,096 | Use the conservative application baseline |
| 4,096 | 4,096 | Baseline exactly matches the model limit |
| 2,048 | 2,048 | Respect the smaller model limit |

A smaller declared model limit is not treated as a failure. The application can still
evaluate that valid smaller context.

### 6.2 Explicit user request

```text
baseline = exact requested value
preservation target = exact requested value
requested context = exact requested value

if request <= declared model limit
→ WithinModelLimit

if request > declared model limit
→ ExceedsModelLimit
```

The second case remains a **resolved planning result**. It does not mean the request
is runnable. It records the conflict truthfully so later mandatory gates can reject the
baseline and candidate generation can consider explicit alternatives.

### 6.3 Missing or unsupported limit

```text
declared limit absent or zero
→ NotEstablished / ModelContextLimitUnavailable

declared limit greater than int.MaxValue
→ NotEstablished / ModelContextLimitOutOfRange
```

The policy has no operational-failure status because it performs no I/O, starts no
process, reads no file, and calls no external component. Unexpected exceptions
indicate a programming defect and are handled by the future Compatibility
orchestration boundary.

### 6.4 No automatic context extrapolation

Version one must not automatically apply:

```text
RoPE scaling overrides
YaRN overrides
context extension beyond the inspected model limit
filename-based extension rules
model-family extension tables
```

A future extension feature needs its own quality evidence, supported-runtime matrix,
policy version, compatibility treatment, and Runtime Verification coverage.

### 6.5 Explicit runtime context

A later selected candidate must pass an explicit context token count into Runtime
Verification and final configuration. The application must not use `--ctx-size 0` or
another implicit runtime default as its planning decision.

---

## 7. Alternatives considered

### 7.1 Use the model's full declared context automatically

Example:

```text
declared limit = 131,072
automatic baseline = 131,072
```

**Rejected.** This converts ordinary onboarding into a long-context stress test and
can make a useful model appear unsuitable on a constrained edge machine.

### 7.2 Use a fixed value and silently clamp every request

Example:

```text
user requests 32,768
application evaluates 8,192 without exposing the change
```

**Rejected.** The result would no longer describe the user's request and could
mislead both the user and later evidence.

### 7.3 Let Hardware Inspection choose the context

**Rejected.** Hardware Inspection owns factual machine evidence only and must not
receive model-dependent planning responsibility.

### 7.4 Let the memory estimator choose the user's intent

**Rejected.** An estimator may determine whether a context appears affordable, but
it must not rewrite what the user requested.

### 7.5 Generate all practical context candidates here

**Rejected.** That would merge Decision 3 with Decision 8 and make the policy harder
to test, version, and change independently.

---

## 8. Future contract shape

The following types are authorised in principle only after the implementation entry
gate passes.

### 8.1 Policy interface

```csharp
internal interface ICompatibilityPlanningContextPolicy
{
    CompatibilityPolicyIdentity Identity { get; }

    CompatibilityPlanningContextResolution Resolve(
        CompatibilityContextRequest request,
        ulong? declaredModelContextLength);
}
```

The interface deliberately accepts no:

```text
HardwareSnapshot
SystemMemorySnapshot
model path
file handle
estimator
candidate generator
ViewModel
XAML type
clock
process
network client
configuration file
```

### 8.2 Policy implementation

```text
CompatibilityPlanningContextPolicy
├── DefaultTargetTokens = 4,096
└── Identity = PlanningContext / planning-context-v1
```

The implementation is stateless, deterministic, thread-safe, and side-effect free.

### 8.3 Plan

```text
CompatibilityPlanningContextPlan
├── Mode
├── BaselineContextTokens
├── PreservationTargetTokens
├── ModelContextLimitTokens
├── RequestedContextTokens?
├── RelationshipToModelLimit
└── PolicyIdentity
```

### 8.4 Resolution

```text
CompatibilityPlanningContextResolution
├── Status
├── Plan?
└── FailureReason?
```

Version-one enums:

```text
CompatibilityContextLimitRelationship
├── Unspecified = 0
├── WithinModelLimit = 1
└── ExceedsModelLimit = 2

CompatibilityPlanningContextResolutionStatus
├── Unspecified = 0
├── Resolved = 1
└── NotEstablished = 2

CompatibilityPlanningContextFailureReason
├── Unspecified = 0
├── ModelContextLimitUnavailable = 1
└── ModelContextLimitOutOfRange = 2
```

Zero remains reserved for invalid or uninitialised state.

---

## 9. Cross-property invariants

### 9.1 Application default plan

```text
Mode = ApplicationDefault
RequestedContextTokens = null
BaselineContextTokens > 0
BaselineContextTokens <= ModelContextLimitTokens
PreservationTargetTokens = BaselineContextTokens
RelationshipToModelLimit = WithinModelLimit
PolicyIdentity.Kind = PlanningContext
PolicyIdentity.Version = planning-context-v1
```

### 9.2 User-requested plan

```text
Mode = UserRequested
RequestedContextTokens exists
RequestedContextTokens > 0
BaselineContextTokens = RequestedContextTokens
PreservationTargetTokens = RequestedContextTokens
RelationshipToModelLimit agrees with the two token counts
PolicyIdentity.Kind = PlanningContext
PolicyIdentity.Version = planning-context-v1
```

### 9.3 Resolution envelope

```text
Resolved
→ Plan exists
→ FailureReason absent

NotEstablished
→ Plan absent
→ FailureReason exists
→ FailureReason is not Unspecified
```

Constructors or named factories must reject contradictory combinations immediately.
They must not create invalid objects and rely on later callers to notice.

---

## 10. Relationship to the later result contract

Decision 3 supplies:

```text
RequestedContextTokens
BaselineContextTokens
PreservationTargetTokens
ModelContextLimitTokens
RelationshipToModelLimit
PlanningContextPolicyIdentity
```

Later decisions supply:

```text
MaximumEstimatedSafeContextTokens
MaximumSafeContextCandidateId
PreservationStatus
resource estimates
candidate evidence
classification
```

The final Compatibility result must record the exact policy identity used. Decision 3
must not pre-populate later fields with provisional or guessed values.

---

## 11. Relationship to Decision 8

Decision 8 is the sole owner of the candidate context ladder.

It may consume:

```text
BaselineContextTokens
PreservationTargetTokens
ModelContextLimitTokens
RelationshipToModelLimit
```

It then decides which practical lower or higher values should be evaluated. Decision 3
must not create a hidden list such as `1K, 2K, 4K, 8K, 16K, 32K`; those values require
their own supported-matrix, resource, ranking, and evidence rules.

---

## 12. Context changes after a result

A changed context creates a new immutable request and a new Compatibility run.

```text
old request and result
→ retained as historical evidence

new user context
→ new CompatibilityContextRequest
→ new run identity
→ fresh model-artifact revalidation
→ fresh resource inputs
→ new planning decision
→ new estimates and result
```

The application must not mutate the historical request or silently patch an existing
result.

---

## 13. Security, privacy, and trust boundaries

Decision 3 minimises exposure by receiving only the facts it needs.

It must not log or carry:

```text
absolute model paths
command lines
raw model metadata
hardware identifiers
machine or account identity
stdout or stderr
tokens, credentials, or secrets
```

A missing model limit fails closed as `NotEstablished`; it is not replaced with a
convenient guess. This follows least-information, economy-of-design, and fail-secure
principles.

Because the policy has no I/O, its unit tests need no model file, process, network
access, hardware fixture, UI thread, or personal machine evidence.

---

## 14. Future test strategy

The future implementation requires fast deterministic policy tests and the existing
packaged WinUI verification route. These are different layers and must not be
confused.

### 14.1 Pure policy tests

Required cases:

```text
ApplicationDefault + 131,072 limit
→ Resolved / 4,096

ApplicationDefault + 2,048 limit
→ Resolved / 2,048

UserRequested 16,384 + 131,072 limit
→ exact request / WithinModelLimit

UserRequested 16,384 + 8,192 limit
→ exact request / ExceedsModelLimit

missing limit
→ NotEstablished / ModelContextLimitUnavailable

zero limit
→ NotEstablished / ModelContextLimitUnavailable

limit greater than int.MaxValue
→ NotEstablished / ModelContextLimitOutOfRange

same inputs repeated
→ value-equivalent output

wrong policy identity
→ construction rejected

contradictory mode/request pair
→ construction rejected

relationship inconsistent with token counts
→ construction rejected

invalid resolution payload
→ construction rejected
```

### 14.2 Architecture guards

Tests or source checks must prove that the policy has:

```text
one parameterless construction path
only Identity and Resolve as its public contract
no hardware dependency
no memory-estimator dependency
no process, file, UI, network, or clock dependency
no candidate-ladder implementation
```

### 14.3 Packaged WinUI regression

The reviewed repository uses an app-container `.build.appxrecipe` and
`vstest.console.exe` for the authoritative unit and UI-thread run. A future
implementation must preserve that route and prove through TRX inspection that the
Decision 3 test classes were discovered and passed.

A direct `dotnet test --filter` command may be used only if the implementation entry
gate proves that it actually discovers and executes the pure tests on the integrated
base. It must not replace packaged verification or cause the project configuration to
be weakened merely to obtain a faster command.

---

## 15. Implementation entry gate

Implementation may begin only when one integrated branch satisfies every item below.

### 15.1 Immutable base identity

```text
exact base branch name recorded
exact base commit SHA recorded
working tree clean
base contains the accepted Decision 1 and Decision 2 commits
base contains the accepted Hardware Inspection handoff commit
```

### 15.2 Required production contracts

The expected Decision 1, Decision 2, and Hardware Inspection types must exist under
their canonical owners. Their exact paths and namespaces must be recorded before a
Decision 3 file is created.

At minimum, the base must provide:

```text
ContextTokenCount
CompatibilityContextMode
CompatibilityContextRequest
CompatibilityPolicyIdentity
CompatibilityPolicyKind
CompatibilityPolicyVersion
ModelInspectionHandoff
HardwareInspectionHandoff
ModelHardwareCompatibilityRequest
```

### 15.3 Required document identities

The accepted Decision 1 design and plan and the accepted Decision 2 design and plan
must exist at their canonical repository paths. No second filename may be created to
work around a missing predecessor.

### 15.4 Duplicate-owner scan

Repository searches must prove there is only one owner for each reused type. A worker
must stop if duplicate token-count, context-request, policy-identity, or handoff types
exist.

### 15.5 Baseline verification

Before adding Decision 3 code:

```text
restore succeeds
WinUI Release x64 build succeeds
test project build succeeds
authoritative packaged test run succeeds
TRX reports non-zero discovery and execution
all executed tests pass
fixture reproducibility and privacy checks pass where present
```

### 15.6 Focused-test runner proof

The worker must experimentally establish the supported red/green command on that
exact base and retain evidence that the intended Decision 3 test class was executed.
A command that merely compiles or reports zero discovered tests is not accepted.

If any entry-gate item fails, the worker stops without creating temporary contracts or
partial production code.

---

## 16. Definition of done

### 16.1 Design completion — satisfied by this document

The design is considered approved in principle when:

1. the policy rules and ownership boundaries are unambiguous;
2. alternatives and trade-offs are recorded;
3. the implementation entry gate is explicit;
4. non-claims and deferrals are visible;
5. the design does not claim predecessor implementation exists.

### 16.2 Implementation completion — not yet satisfied

Decision 3 becomes implementation-complete only when:

1. the entry gate passed on an immutable integrated base;
2. the canonical Decision 1 and Decision 2 types were reused unchanged;
3. all policy paths and negative invariants are tested;
4. explicit requests are never silently clamped;
5. missing or unsupported limits return typed `NotEstablished`;
6. no hardware, estimator, UI, process, file, network, or clock dependency entered;
7. `planning-context-v1` is recorded through the Decision 2 identity type;
8. the packaged WinUI test run proves test discovery and success;
9. documentation and traceability are updated;
10. a detailed review confirms no Decision 4–8 behaviour was implemented.

---

## 17. Non-claims and deferred work

This decision does not implement or establish:

```text
model-weight memory
KV-cache memory
runtime overhead
RAM or VRAM safety reserve
maximum safe context
candidate context values
backend support
GPU offload
expected speed
quality preservation
compatibility classification
Runtime Verification
Hardware Inspection completion
```

The absence of these calculations is deliberate. They belong to later decisions.

---

## 18. External technical basis

The current official `llama.cpp` completion documentation describes `--ctx-size`
with a default of `4,096` and supports `0` to load the model value. Decision 3 uses an
explicit application-owned value so the result does not depend on an implicit runtime
default that may change.

The official IBM Granite 4.1 3B model card reports a sequence length of `131,072`.
That value demonstrates why a model capability ceiling must not automatically become
the default allocation on a memory-constrained edge device.

Primary references:

- [llama.cpp completion context options](https://github.com/ggml-org/llama.cpp/blob/master/tools/completion/README.md)
- [IBM Granite 4.1 3B model card](https://huggingface.co/ibm-granite/granite-4.1-3b)

These external facts support the rationale. The project policy remains versioned and
must be re-reviewed if its technical assumptions change.

---

## 19. Textbook alignment

- **Systems Engineering: Principles and Practice**, Chapters 6, 11, 13, and 17:
  establish requirements and prerequisites, make assumptions explicit, reduce
  uncertainty before construction, and retain test traceability.
- **Fundamentals of Software Architecture**, Chapters 2, 3, 21, and 22: analyse
  trade-offs, keep a cohesive policy boundary, record the decision, and expose
  unresolved architectural risk rather than hiding it.
- **Designing Secure Software**, Chapters 2–4, 6–7, 10, and 12: minimise exposed
  information, identify trust boundaries, fail securely, review the design, and test
  untrusted or contradictory inputs.
- **The Art of Unit Testing**, Chapters 7, 8, and 10: keep tests trustworthy,
  maintainable, readable, and placed at the correct test level.
- **Code Complete**, Chapters 3, 5, 8, 22, and 28: satisfy upstream prerequisites,
  manage complexity, enforce defensive contracts, automate developer testing, and
  control tool and configuration versions.
- **Build desktop apps for Windows**: preserve the repository's WinUI 3,
  Windows App SDK, packaging, and app-container verification model instead of
  inventing a console-only test route.

---

## 20. Final design view

```text
CompatibilityContextRequest
        +
trusted declared model context limit
        ↓
CompatibilityPlanningContextPolicy
        ↓
CompatibilityPlanningContextResolution
        ├── Resolved
        │   └── CompatibilityPlanningContextPlan
        │       ├── baseline
        │       ├── preservation target
        │       ├── model limit
        │       ├── request relationship
        │       └── planning-context-v1
        │
        └── NotEstablished
            └── stable failure reason
```

The design is intentionally small. It records one truthful baseline and one preserved
intention without pretending that memory estimation, candidate generation, or runtime
proof has already happened.
