# Model–Hardware Compatibility Decision 3 — Planning-Context Policy

**Decision:** 3 of 8  
**Status:** Approved and closed at planning level  
**Decision date:** 2026-08-19  
**Final review date:** 2026-08-19  
**Scope:** Baseline context selection and preservation of explicit context intent  
**Implementation status:** Planned, not yet implemented  
**Policy identity:** `PlanningContext / planning-context-v1`

---

## 1. Final decision

Model–Hardware Compatibility needs one context size before it can estimate model
resources or generate candidate configurations. Decision 3 provides that value through
one small, deterministic policy.

```text
ApplicationDefault
→ min(4,096, trusted declared model context limit)

UserRequested
→ preserve the exact positive requested token count
→ never silently clamp, round, or replace it

Declared model context absent or zero
→ NotEstablished / ModelContextLimitUnavailable

Declared model context greater than int.MaxValue
→ NotEstablished / ModelContextLimitOutOfRange
```

A user request above the declared model limit remains a **resolved planning result**
with `ExceedsModelLimit`. This keeps the user's intention truthful. A later mandatory
context gate rejects that baseline, while Decision 8 may generate explicit lower
alternatives.

The `4,096` value is an application-owned planning baseline. It is not:

```text
the model's maximum context
the maximum context that fits this computer
a RAM or VRAM estimate
a compatibility classification
a Runtime Verification result
a guarantee that inference will succeed
```

---

## 2. Meaning of a context token count

Every `ContextTokenCount` in Decision 3 represents the **total runtime context-window
capacity** used for planning, equivalent to the intended `n_ctx` or explicit
`--ctx-size` value.

It is not:

```text
expected prompt length
maximum generated-token count
current tokens already in use
model-file size
KV-cache memory
```

A later selected configuration budgets prompt tokens, generated tokens, chat-template
tokens, and other runtime tokens within this total window. Decision 3 does not divide
that budget.

---

## 3. Corrected programme position

An earlier revision described Decision 3 as programme-blocked because predecessor
production contracts were not integrated on one inspected `main` commit. That mixed
up two different questions:

```text
Have the project decisions and plans been completed?
vs.
Are their production contracts integrated on this exact code branch?
```

Decision 1 and Decision 2 already have approved designs and implementation plans.
Hardware Inspection also has its own approved architecture and plan.

The final position is:

- Decision 3 is complete at the planning level.
- Its future C# implementation reuses the actual Decision 1 and Decision 2 contracts
  on the selected implementation base.
- `HardwareInspectionHandoff` is needed by the eventual complete Compatibility request
  and orchestrator, but it is **not an input to this pure policy**.
- Repository sequencing is checked when implementation begins; it is not part of the
  Decision 3 policy semantics.

This document supersedes the previous programme-level entry-gate wording for
Decision 3.

---

## 4. Single responsibility and ownership

Decision 3 answers one question:

> Which context size should the baseline compatibility assessment use, and which
> context intention should later candidate generation try to preserve?

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

Decision 3 does not:

```text
inspect hardware
calculate weight or KV-cache memory
apply RAM or VRAM reserves
choose a runtime or backend
select GPU offload
create context candidates
rank configurations
calculate maximum safe context
claim that a model can run
```

Hardware Inspection must not choose a model-dependent context. A resource estimator
may assess whether a request appears affordable, but it must not rewrite what the user
requested.

---

## 5. Inputs and trust boundary

The policy consumes only:

```text
CompatibilityContextRequest
+
trusted declared model context length
```

### 5.1 CompatibilityContextRequest

Decision 1 owns the immutable request:

```text
ApplicationDefault
→ no user token value

UserRequested
→ one positive ContextTokenCount
```

Decision 3 reuses Decision 1's `ContextTokenCount`. It must not create another token
wrapper or replace it with a primitive `int` in the planning plan.

### 5.2 Trusted declared model context

The declared model context comes from authoritative Model Inspection evidence. The
policy must not infer it from:

```text
filename
model-family name
hard-coded Granite lookup table
online lookup
runtime default
hardware capacity
user request
```

The input is `ulong?` because inspected metadata can be absent or outside the
application's version-one positive-`int` representation. A usable value is converted
once into `ContextTokenCount`.

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

```text
request <= declared model limit
→ WithinModelLimit

request > declared model limit
→ ExceedsModelLimit
```

`ExceedsModelLimit` records a truthful planning conflict. It does not claim that the
request is runnable.

### 6.3 Missing or unsupported model limit

```text
null or zero
→ NotEstablished / ModelContextLimitUnavailable

greater than int.MaxValue
→ NotEstablished / ModelContextLimitOutOfRange
```

No guessed fallback is used.

### 6.4 No automatic context extension in version one

The trusted declared model limit is the version-one upper bound. Decision 3 does not
automatically apply:

```text
RoPE scaling
YaRN
family-specific extension rules
metadata overrides beyond the approved inspected limit
context extension beyond that limit
```

A future extension requires its own quality evidence, supported-runtime matrix, policy
version, compatibility treatment, and Runtime Verification coverage.

---

## 7. Contract shape

The implementation consists of five focused files:

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

The interface is justified because the later Compatibility orchestrator consumes a
versioned policy boundary. Version one does not require a second implementation.

### 7.2 Policy implementation

```text
CompatibilityPlanningContextPolicy
├── private constant DefaultTargetTokens = 4,096
├── identity PlanningContext / planning-context-v1
├── no mutable state
└── no external dependencies
```

The class is sealed, stateless, deterministic, thread-safe, synchronous, and
side-effect free.

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

### 7.4 Resolution and enums

```text
CompatibilityPlanningContextResolution
├── Status
├── Plan?
└── FailureReason?
```

```csharp
internal enum CompatibilityContextLimitRelationship
{
    Unspecified = 0,
    WithinModelLimit = 1,
    ExceedsModelLimit = 2,
}

internal enum CompatibilityPlanningContextResolutionStatus
{
    Unspecified = 0,
    Resolved = 1,
    NotEstablished = 2,
}

internal enum CompatibilityPlanningContextFailureReason
{
    Unspecified = 0,
    ModelContextLimitUnavailable = 1,
    ModelContextLimitOutOfRange = 2,
}
```

Zero remains reserved for invalid or uninitialised state.

---

## 8. Cross-property invariants

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

### 8.3 Resolution envelope

```text
Resolved
→ Plan exists
→ FailureReason is absent

NotEstablished
→ Plan is absent
→ FailureReason exists and is not Unspecified
```

Constructors or named factories reject contradictory values before creating an
instance.

---

## 9. Lifecycle after the policy decision

### 9.1 Context changes create a new run

Changing the context never mutates an existing request or result.

```text
existing request and result
→ retained as historical evidence

new context intention
→ new CompatibilityContextRequest
→ new run identity
→ fresh model-artifact revalidation
→ fresh resource inputs and memory observation
→ new planning result and final Compatibility result
```

### 9.2 Runtime receives an explicit context

A later selected candidate passes one explicit context token count into Runtime
Verification and final configuration.

The application must not use `--ctx-size 0`, or another implicit runtime default, as
its planning decision. This avoids behaviour changing silently when a pinned runtime
or model metadata changes.

---

## 10. Relationship to later Compatibility work

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
resource estimates
maximum estimated safe context
candidate identifiers
preservation status
ranking evidence
fit classification
```

Decision 8 remains the sole owner of the candidate context ladder. It may consume the
Decision 3 plan, but Decision 3 must not embed a hidden sequence such as
`1K, 2K, 4K, 8K, 16K, 32K`.

Later treatment is:

| Planning result | Later treatment |
|---|---|
| Resolved and within model limit | Continue through later gates |
| Resolved but request exceeds model limit | Baseline context gate fails; alternatives may be generated |
| Model limit unavailable | Mandatory context evidence remains unresolved |
| Model limit out of range | Mandatory context evidence remains unresolved |
| Invalid request object | Synchronous contract exception before execution |

---

## 11. Failure handling, security, and privacy

Expected evidence limitations return typed `NotEstablished` results. The policy has no
`OperationalFailure` because it performs no I/O and starts no process.

Programming-contract violations are synchronous exceptions:

```text
null request
→ ArgumentNullException

unsupported mode or contradictory construction
→ ArgumentException or ArgumentOutOfRangeException
```

The future Compatibility orchestration boundary owns unexpected exception mapping.

Decision 3 receives only the information it needs. It must not receive, store, or log:

```text
absolute model paths
raw model metadata
hardware identifiers
machine or account identity
command lines
stdout or stderr
credentials or tokens
```

A missing model limit fails closed as `NotEstablished`; it is never replaced with a
convenient guess.

---

## 12. Simplicity and time-boxing

The first release intentionally does not add:

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

One interface, one policy, one immutable plan, one resolution envelope, and one enum
file are sufficient.

The implementation plan uses four coherent tasks rather than many tiny tasks. It keeps
the essential TDD, invariant checks, security boundary, CI discovery, and final packaged
regression while avoiding brittle reflection tests, concurrency stress for a stateless
calculation, one remote commit per tiny type, and repeated full-suite runs after every
file.

---

## 13. Minimum test set

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
concurrency harness is required.

---

## 14. Definitions of done

Decision 3 is **planning-complete** when this design and its pragmatic implementation
plan are reviewed together and no unresolved policy ambiguity remains.

Its future implementation is complete when:

1. Decision 1 and Decision 2 value objects are reused rather than copied;
2. every behaviour in Section 13 passes focused tests;
3. plan and resolution invariants reject contradictory construction;
4. the policy has no hardware, estimator, file, process, network, UI, clock, or random
   dependency;
5. the packaged WinUI route discovers and passes both Decision 3 test classes;
6. the feature README explains context-window meaning, the 4,096 baseline, and all
   non-claims;
7. a concise F-M09 evidence record identifies the exact implementation SHA and test
   result;
8. the final diff contains no Decision 4–8 behaviour;
9. a selected candidate later passes an explicit context value to Runtime Verification;
10. changing context creates a new immutable request and run.

---

## 15. Rejected and deferred ideas

Rejected for version one:

```text
copy the 16,384 prototype example into product policy
allocate the full trained context by default
silently clamp explicit requests
infer context from a filename or family table
let Hardware Inspection choose context
let the estimator rewrite user intent
use an implicit runtime context default
support automatic context extension
```

Deferred to later decisions:

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

## 16. Technical and engineering basis

The external rationale was rechecked on 2026-08-19:

- Official `llama.cpp` completion documentation describes `--ctx-size` default `4096`
  and `0` as loading the value from the model.
- IBM's official Granite 4.1 3B model card reports a sequence length of `131,072`.

These sources support an explicit conservative baseline. They do not prove that 4,096
fits every computer or that 131,072 is practical on an edge device.

Engineering guidance used:

- **Systems Engineering: Principles and Practice**, Chapters 6, 7, 11 and 17:
  explicit requirements, functional allocation, decision inputs, and traceable tests.
- **Fundamentals of Software Architecture**, Chapters 2, 3, 6 and 21:
  trade-offs, cohesion, low coupling, fitness functions, and decision records.
- **Engineering Software Products**, Chapters 8–10:
  input validation, failure management, focused testing, and controlled delivery.
- **Designing Secure Software**, Chapters 3, 4, 6 and 10:
  exposure minimisation, least information, fail-secure defaults, and input handling.
- **The Art of Unit Testing**, Chapters 7–10:
  trustworthy and maintainable tests at the appropriate level.
- **Code Complete**, Chapters 3, 5, 8, 22 and 28:
  sufficient preparation, information hiding, defensive contracts, developer testing,
  and configuration control.
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

Decision 3 is closed at the planning level. Decision 4 can now be discussed without
reopening context selection unless an upstream contract or project requirement changes.
