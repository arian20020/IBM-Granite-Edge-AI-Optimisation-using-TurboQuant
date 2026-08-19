# Model–Hardware Compatibility Decision 3 — Planning-Context Policy

**Decision:** 3 of 8
**Status:** Approved and closed at planning level
**Decision date:** 2026-08-19
**Final review date:** 2026-08-19
**Scope:** Baseline context selection and preservation of explicit user context intent
**Implementation status:** Planned, not yet implemented
**Policy identity:** `PlanningContext / planning-context-v1`

---

## 1. Decision

Model–Hardware Compatibility needs one context size before it can estimate model
resources or generate candidate configurations. Decision 3 supplies that planning
context through one small, deterministic policy.

Version one uses these rules:

```text
ApplicationDefault
→ min(4,096, trusted declared model context limit)

UserRequested
→ preserve the exact positive requested token count
→ never silently clamp or replace the request

Declared model context absent or zero
→ NotEstablished / ModelContextLimitUnavailable

Declared model context greater than int.MaxValue
→ NotEstablished / ModelContextLimitOutOfRange
```

A user request above the declared model limit remains a **resolved planning result**
with `ExceedsModelLimit`. This records the user's intention truthfully. A later
mandatory context gate rejects that baseline, and Decision 8 may generate explicit
lower alternatives.

The `4,096` value is an application-owned planning baseline. It is not:

```text
the model's maximum context
the maximum context that fits this computer
a RAM or VRAM estimate
a compatibility classification
a Runtime Verification result
a promise that inference will succeed
```

---

## 2. Correction to the earlier branch wording

The earlier revision on this branch described Decision 3 as programme-blocked because
Decision 1, Decision 2, and Hardware Inspection were not implemented on the inspected
`main` commit. That mixed up two different questions:

```text
Have the project decisions and plans been completed?
vs.
Are their production contracts already integrated on this exact code branch?
```

Decision 1 and Decision 2 already have approved designs and implementation plans, and
Hardware Inspection already has its own approved architecture and plan.

The final position is:

- Decision 3 is **complete at the planning level**.
- Its future C# implementation must reuse the actual Decision 1 and Decision 2
  contracts on the selected implementation base.
- `HardwareInspectionHandoff` is required by the eventual complete Compatibility
  request and orchestrator, but it is **not an input to this pure policy** and is not a
  reason to redesign or delay the Decision 3 policy module.
- Repository sequencing is checked when implementation begins; it is not part of the
  policy semantics.

This document supersedes the previous “implementation blocked at the entry gate”
wording for Decision 3.

---

## 3. Purpose and boundary

Decision 3 answers one question:

> Which context size should the baseline compatibility assessment use, and which
> context intention should later candidate generation try to preserve?

It does not answer:

```text
How much memory does the model need?
What RAM or VRAM reserve is required?
Which runtime or backend should be used?
How many layers should be offloaded?
Which KV-cache type should be used?
Which context values should become candidates?
What is the maximum estimated safe context?
Will the model actually initialise and run?
```

Those responsibilities remain in later Compatibility decisions and Runtime
Verification.

---

## 4. Ownership

| Concern | Canonical owner |
|---|---|
| Inspected model facts and declared model context | Model Inspection |
| Factual machine evidence | Hardware Inspection |
| Explicit default/requested context intent | Decision 1 |
| Policy kind and version value objects | Decision 2 |
| Baseline context and preservation target | Decision 3 |
| Resource formulas and safety reserves | Later Compatibility decisions |
| Candidate context ladder | Decision 8 |
| Fit classification and recommendation | Compatibility orchestrator/classifier |
| Proof that a selected configuration runs | Runtime Verification |

Decision 3 must not inspect hardware. Hardware Inspection must not choose a
model-dependent context. An estimator may assess whether a request appears affordable,
but it must not rewrite what the user requested.

---

## 5. Inputs

The policy consumes only:

```text
CompatibilityContextRequest
+
trusted declared model context length
```

### 5.1 CompatibilityContextRequest

Decision 1 owns the request:

```text
ApplicationDefault
→ no user token value

UserRequested
→ one positive ContextTokenCount
```

Decision 3 reuses Decision 1's `ContextTokenCount`. It must not create another token
wrapper or use a primitive `int` in the plan contract.

### 5.2 Declared model context

The declared model context comes from trusted Model Inspection evidence. The policy
must not infer it from:

```text
filename
model-family name
hard-coded Granite table
online lookup
runtime default
hardware capacity
user request
```

The policy accepts the inspected numeric value as `ulong?` because source metadata can
be absent or larger than the application's version-one `int` representation. A usable
value is converted once into `ContextTokenCount`.

---

## 6. Policy behaviour

### 6.1 ApplicationDefault

```text
baseline = min(4,096, declared model limit)
preservation target = baseline
requested context = absent
relationship = WithinModelLimit
```

| Declared model limit | Baseline |
|---:|---:|
| 131,072 | 4,096 |
| 8,192 | 4,096 |
| 4,096 | 4,096 |
| 2,048 | 2,048 |

A valid model limit below 4,096 is not a warning. The policy simply respects the
smaller limit.

### 6.2 UserRequested

```text
baseline = exact request
preservation target = exact request
requested context = exact request
```

Relationship:

```text
request <= declared model limit
→ WithinModelLimit

request > declared model limit
→ ExceedsModelLimit
```

`ExceedsModelLimit` does not mean the request is runnable. It preserves the request
and exposes the conflict for later gates.

### 6.3 Missing or unsupported model limit

```text
null or zero
→ NotEstablished / ModelContextLimitUnavailable

greater than int.MaxValue
→ NotEstablished / ModelContextLimitOutOfRange
```

No guessed fallback is used.

### 6.4 Version-one upper bound

The trusted declared model limit is the version-one upper bound. Decision 3 does not
automatically apply:

```text
RoPE scaling
YaRN
metadata overrides
family-specific extension rules
context extension beyond the inspected limit
```

Context extension requires its own evidence, runtime support matrix, quality
evaluation, policy version, and Runtime Verification coverage.

---

## 7. Contract shape

The implementation remains deliberately small.

```text
ICompatibilityPlanningContextPolicy
CompatibilityPlanningContextPolicy
CompatibilityPlanningContextPlan
CompatibilityPlanningContextResolution
CompatibilityPlanningContextEnums
```

### 7.1 Policy interface

```csharp
internal interface ICompatibilityPlanningContextPolicy
{
    CompatibilityPolicyIdentity Identity { get; }

    CompatibilityPlanningContextResolution Resolve(
        CompatibilityContextRequest request,
        ulong? declaredModelContextLength);
}
```

The interface is retained because the later Compatibility orchestrator will consume a
versioned policy boundary. No second implementation is required for version one.

### 7.2 Policy implementation

```text
CompatibilityPlanningContextPolicy
├── private constant DefaultTargetTokens = 4,096
├── identity PlanningContext / planning-context-v1
├── no mutable state
└── no external dependencies
```

The class is sealed, stateless, deterministic, thread-safe, and side-effect free.

### 7.3 Plan

```text
CompatibilityPlanningContextPlan
├── Mode
├── BaselineContextTokens
├── PreservationTargetTokens
├── RequestedContextTokens?
├── ModelContextLimitTokens
├── RelationshipToModelLimit
└── PolicyIdentity
```

### 7.4 Resolution

```text
CompatibilityPlanningContextResolution
├── Status
├── Plan?
└── FailureReason?
```

Enums:

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

## 8. Invariants

### 8.1 ApplicationDefault plan

```text
Mode = ApplicationDefault
RequestedContextTokens = null
BaselineContextTokens > 0
BaselineContextTokens <= ModelContextLimitTokens
PreservationTargetTokens = BaselineContextTokens
RelationshipToModelLimit = WithinModelLimit
PolicyIdentity = PlanningContext / planning-context-v1
```

### 8.2 UserRequested plan

```text
Mode = UserRequested
RequestedContextTokens exists
BaselineContextTokens = RequestedContextTokens
PreservationTargetTokens = RequestedContextTokens
RelationshipToModelLimit agrees with request and model limit
PolicyIdentity = PlanningContext / planning-context-v1
```

### 8.3 Resolution

```text
Resolved
→ Plan exists
→ FailureReason is absent

NotEstablished
→ Plan is absent
→ FailureReason exists and is not Unspecified
```

Constructors or named factories reject contradictory values immediately.

---

## 9. Failure handling

Expected evidence limitations are returned as typed `NotEstablished` results. The
policy does not return `OperationalFailure` because it performs no I/O and starts no
process.

Programming-contract violations are synchronous exceptions:

```text
null request
→ ArgumentNullException

unsupported request mode or contradictory plan construction
→ ArgumentException or ArgumentOutOfRangeException
```

The future Compatibility orchestration boundary owns unexpected exception mapping.

---

## 10. Security and privacy

Decision 3 follows least-information and fail-secure design. It receives only the
context request and the model limit.

It must not receive, store, or log:

```text
absolute model paths
raw model metadata
hardware identifiers
machine or account identity
command lines
stdout or stderr
credentials or tokens
```

A missing model limit becomes `NotEstablished`; it is never replaced with a convenient
guess.

---

## 11. Simplicity and time-boxing

The first release intentionally does **not** add:

```text
context ladder service
context database
model-family default dictionary
hardware-specific default
configuration file for one constant
context cache
network lookup
UI message provider
automatic context extension
separate architecture-test project
```

The interface, one policy, one immutable plan, one resolution envelope, and one enum
file are sufficient.

The implementation plan is deliberately reduced to four tasks. It keeps the important
tests and final packaged regression, but removes brittle reflection tests, a remote
commit for every tiny type, and repeated full-suite runs after each file.

---

## 12. Minimum test set

The implementation must prove:

```text
ApplicationDefault + 131,072
→ 4,096

ApplicationDefault + 4,096
→ 4,096

ApplicationDefault + 2,048
→ 2,048

UserRequested 16,384 + 131,072
→ exact request / WithinModelLimit

UserRequested 16,384 + 8,192
→ exact request / ExceedsModelLimit

missing limit
→ NotEstablished / ModelContextLimitUnavailable

zero limit
→ NotEstablished / ModelContextLimitUnavailable

int.MaxValue
→ supported

int.MaxValue + 1
→ NotEstablished / ModelContextLimitOutOfRange

same inputs
→ value-equivalent output

invalid plan combinations
→ rejected

invalid resolution combinations
→ rejected

policy identity
→ PlanningContext / planning-context-v1
```

No model file, hardware fixture, process, network access, UI thread, clock, or
concurrency harness is needed.

---

## 13. Definition of done

Decision 3 is **planning-complete now**.

Its future implementation is complete when:

1. the Decision 1 and Decision 2 value objects are reused, not copied;
2. all policy behaviours in Section 12 pass focused tests;
3. plan and resolution invariants reject contradictory construction;
4. the policy has no hardware, estimator, file, process, network, UI, or clock
   dependency;
5. the existing packaged WinUI test route discovers and passes the Decision 3 tests;
6. the feature README explains the 4,096 baseline and all non-claims;
7. a concise F-M09 evidence record identifies the implementation SHA and test result;
8. the final diff contains no Decision 4–8 logic.

---

## 14. Deferred to later decisions

```text
weight-memory formula
KV-cache formula
runtime overhead
RAM and VRAM reserves
maximum estimated safe context
candidate context ladder
candidate ranking
runtime/backend selection
GPU offload
final fit classification
Runtime Verification
```

These are intentionally outside Decision 3, not missing requirements.

---

## 15. External rationale

The policy uses an explicit application-owned value rather than relying on an implicit
runtime choice.

Verified on 2026-08-19:

- Official `llama.cpp` completion documentation describes `--ctx-size` default `4096`
  and `0` as loading the value from the model.
- IBM's official Granite 4.1 3B model card reports a sequence length of `131,072`.

These sources support the choice of a conservative baseline, but they do not prove that
4,096 fits every computer or that 131,072 is practical on an edge device.

Primary sources:

- `ggml-org/llama.cpp`, `tools/completion/README.md`
- `ibm-granite/granite-4.1-3b`, official model card

---

## 16. Engineering basis

- **Systems Engineering: Principles and Practice**, Chapters 6, 7, 11 and 17:
  explicit requirements, functional allocation, decision inputs, and traceable tests.
- **Fundamentals of Software Architecture**, Chapters 2, 3, 6 and 21:
  trade-offs, cohesion, low coupling, small fitness functions, and decision records.
- **Engineering Software Products**, Chapters 8–10:
  input validation, failure management, focused testing, and controlled delivery.
- **Designing Secure Software**, Chapters 3, 4, 6 and 10:
  exposure minimisation, least information, fail-secure defaults, and untrusted-input
  handling.
- **The Art of Unit Testing**, Chapters 7–10:
  trustworthy, maintainable tests at the appropriate level.
- **Code Complete**, Chapters 3, 5, 8, 22 and 28:
  sufficient upstream preparation, information hiding, defensive contracts, developer
  testing, and configuration control.
- **AI Engineering**, Chapters 5 and 9:
  context efficiency, explicit evaluation assumptions, memory constraints, and
  inference optimisation.
- **Build desktop apps for Windows**:
  preserve the existing WinUI 3 and packaged Windows test/deployment route.

---

## 17. Final summary

```text
CompatibilityContextRequest
        +
trusted declared model context length
        ↓
CompatibilityPlanningContextPolicy
        ↓
CompatibilityPlanningContextResolution
        ├── Resolved
        │   └── immutable planning plan
        └── NotEstablished
            └── stable evidence reason
```

Decision 3 is now closed at the planning level. The next architecture discussion may
move to Decision 4 without reopening context selection unless an upstream contract or
project requirement changes.
