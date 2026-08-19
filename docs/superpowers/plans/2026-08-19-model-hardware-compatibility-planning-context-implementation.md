# Decision 3 — Pragmatic Planning-Context Policy Implementation Plan

> **Execution skill:** Use `superpowers:executing-plans` or
> `superpowers:subagent-driven-development`.

**Decision:** 3 of 8
**Status:** Approved implementation plan; ready for a future implementation slice
**Plan date:** 2026-08-19
**Scope:** Decision 3 production policy, focused tests, documentation, and CI discovery
**Target size:** One short implementation slice; approximately half to one working day,
excluding predecessor integration and CI queue time
**Spec:** `docs/superpowers/specs/2026-08-19-model-hardware-compatibility-planning-context-design.md`

---

## 1. Goal

Implement a pure policy that:

```text
ApplicationDefault
→ min(4,096, trusted declared model context limit)

UserRequested
→ exact request preserved
→ never silently clamped

missing, zero, or unsupported model limit
→ typed NotEstablished result
```

The policy records `PlanningContext / planning-context-v1`.

It does not estimate memory, inspect hardware, generate candidates, choose a runtime,
rank configurations, classify fit, or execute a model.

---

## 2. Practical delivery rule

This plan deliberately keeps the important engineering controls while avoiding
unnecessary ceremony.

### Must finish

```text
complete policy semantics
immutable plan and resolution invariants
focused deterministic tests
no external dependencies
feature README
CI discovery
one final packaged WinUI regression
concise F-M09 evidence
```

### Do only when quick and useful

```text
reciprocal links from Decision 1 and Decision 2 documents
extra explanatory examples
additional source scans beyond the required boundary scan
```

### Deferred

```text
separate architecture-test project
brittle reflection tests of exact method lists
concurrency stress tests for a stateless calculation
configuration file for 4,096
one remote commit per enum or value object
full packaged test run after every small file
```

---

## 3. Starting conditions

Before editing production code:

1. use a dedicated branch/worktree;
2. record the exact starting SHA;
3. confirm the branch contains the accepted Decision 1 contracts:
   `ContextTokenCount`, `CompatibilityContextMode`, and
   `CompatibilityContextRequest`;
4. confirm it contains the Decision 2 policy identity contracts:
   `CompatibilityPolicyIdentity`, `CompatibilityPolicyKind`, and
   `CompatibilityPolicyVersion`;
5. search for duplicate definitions and keep exactly one canonical owner;
6. read the current `.github/workflows/build-and-test.yml`;
7. run the existing baseline verification appropriate to that branch.

If Decision 1 or Decision 2 implementation is on another branch, base this work after
those commits. Do not recreate their contracts.

`HardwareInspectionHandoff` is not required to implement or test this pure policy. It
is required later when the full Compatibility request and orchestrator are wired.

---

## 4. File map

### Production

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

### Tests

```text
tests/UnitTests/GraniteEdgeAI.UnitTests/
└── Features/
    └── ModelHardwareCompatibility/
        ├── CompatibilityPlanningContextContractTests.cs
        └── CompatibilityPlanningContextPolicyTests.cs
```

### Documentation and evidence

```text
IBM Granite with TurboQuant (Intel)/
└── Features/
    └── ModelHardwareCompatibility/
        └── README.md

.github/workflows/build-and-test.yml

docs/evidence/requirements/F-M09/
└── decision-3-planning-context-policy.md
```

Do not create an orchestrator in this slice.

---

## 5. Global constraints

- Reuse Decision 1 and Decision 2 types.
- Keep `DefaultTargetTokens = 4_096`.
- Keep policy identity `PlanningContext / planning-context-v1`.
- Preserve an explicit user request exactly.
- Never silently clamp, round, bucket, or normalise a request.
- Treat the trusted declared model limit as the version-one upper bound.
- Return `NotEstablished` for missing, zero, or out-of-range model limits.
- Do not add automatic RoPE or YaRN extension.
- Keep the policy deterministic, stateless, side-effect free, and synchronous.
- Add beginner-readable comments for logical blocks and non-obvious invariants;
  avoid comments that merely restate syntax.
- Do not log paths, hardware identity, command lines, raw metadata, credentials, or
  machine/account information.
- Keep every reviewable commit internally complete; never push a valid request mode
  that deliberately throws because the next task has not been completed.

---

## 6. TDD and commit strategy

Use local red/green cycles, but keep remote history simple.

```text
1. write the complete failing contract tests
2. implement enums, plan, resolution, and interface
3. write all failing policy-path tests
4. implement ApplicationDefault and UserRequested together
5. add boundary/purity checks
6. update README, CI discovery, and evidence
7. run final verification
```

Recommended reviewable commits:

```text
test/feat: add planning context contracts
test/feat: implement planning context policy
docs/test: record Decision 3 verification
```

Local red commits may be squashed. Do not commit the former temporary
`InvalidOperationException` for `UserRequested`.

---

## 7. Task 1 — Contracts and invariants

### Files

```text
CompatibilityPlanningContextEnums.cs
CompatibilityPlanningContextPlan.cs
CompatibilityPlanningContextResolution.cs
ICompatibilityPlanningContextPolicy.cs
CompatibilityPlanningContextContractTests.cs
```

### Write failing tests first

Test:

```text
all enums reserve zero for Unspecified
only approved version-one enum values exist
valid ApplicationDefault plan is accepted
valid UserRequested plan within the model limit is accepted
valid UserRequested plan above the model limit is accepted
ApplicationDefault with a requested value is rejected
ApplicationDefault above the model limit is rejected
UserRequested without a requested value is rejected
baseline or preservation target differing from the request is rejected
relationship inconsistent with the two token counts is rejected
wrong policy kind/version is rejected
Resolved requires a plan and no failure reason
NotEstablished requires no plan and one non-Unspecified reason
```

Run a focused test command only if output proves that the class was discovered and at
least one test executed. Otherwise use the existing packaged runner or build-only red
proof. Do not weaken the project to obtain a convenient command.

### Minimum implementation

- Add the three enums from the design.
- Add deeply immutable `CompatibilityPlanningContextPlan`.
- Add `CompatibilityPlanningContextResolution` with private construction and named
  factories:
  - `Resolved(plan)`
  - `NotEstablished(reason)`
- Add the small policy interface exactly as specified.
- Validate all cross-property invariants before assigning properties.

### Verify

```text
focused contract tests green
test project compiles
no duplicate ContextTokenCount or policy identity type
```

Commit the complete contract package together.

---

## 8. Task 2 — Complete policy

### Files

```text
CompatibilityPlanningContextPolicy.cs
CompatibilityPlanningContextPolicyTests.cs
```

### Write all failing behaviour tests first

Required cases:

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

null limit
→ NotEstablished / ModelContextLimitUnavailable

zero limit
→ NotEstablished / ModelContextLimitUnavailable

int.MaxValue
→ valid supported limit

int.MaxValue + 1
→ NotEstablished / ModelContextLimitOutOfRange

null request
→ ArgumentNullException

same inputs repeated
→ value-equivalent result

Identity
→ PlanningContext / planning-context-v1
```

### Implement every path together

Algorithm:

```text
validate request
resolve declared model limit

if limit missing or zero
    return ModelContextLimitUnavailable

if limit > int.MaxValue
    return ModelContextLimitOutOfRange

if mode = ApplicationDefault
    baseline = min(4,096, limit)
    return default plan

if mode = UserRequested
    preserve exact request
    compare request with limit
    return requested plan

otherwise
    throw an out-of-range contract exception
```

The implementation has:

```text
one parameterless construction path
no mutable instance state
no file/process/network/UI/time dependency
no hardware or estimator input
```

### Verify

```text
focused policy tests green
focused contract tests green
all Decision 3 tests green
```

Commit the complete policy and tests together.

---

## 9. Task 3 — Boundary guard, documentation, and CI

### Boundary scan

Review the Decision 3 production folder for prohibited dependencies:

```text
HardwareSnapshot
HardwareInspection
SystemMemorySnapshot
memory estimator
candidate generator
ProcessStartInfo
FileStream
HttpClient
Microsoft.UI.Xaml
DateTime
DateTimeOffset
Stopwatch
Random
```

Expected production matches: none.

Also search for rejected behaviour:

```text
16,384 as an automatic default
--ctx-size 0 as the application decision
silent clamp
automatic RoPE/YaRN extension
context ladder values
```

Explanatory README/design text may mention rejected designs; production code may not.

### README

Explain in simple language:

```text
ApplicationDefault = min(4,096, declared model limit)
UserRequested = exact request, never silently clamped
missing model limit = not established
4,096 is not a model limit or fit guarantee
maximum safe context is calculated later
Decision 8 owns candidate context values
```

### CI discovery

Preserve the current packaged WinUI route. Append both fully qualified Decision 3 test
classes to the workflow's existing required-class list, without removing any existing
class.

### Evidence

Create a concise F-M09 record containing:

```text
requirement and Decision 3 identity
base SHA and implementation SHA
policy version
test class names
focused test result
packaged TRX totals
relevant PR
explicit non-claims
```

Do not include raw paths, machine identity, private UCL information, credentials, or
unredacted logs.

### Optional cross-links

Add one reciprocal Decision 3 link to Decision 1 and Decision 2 documents only when
their canonical paths are present and the edits are trivial. This is not allowed to
expand the slice.

Commit documentation, CI discovery, and evidence together.

---

## 10. Task 4 — Final verification

Run fresh verification on the exact head intended for review.

### Static checks

```text
git diff --check
format verification supported by the repository
duplicate-owner search
prohibited-dependency scan
rejected-behaviour scan
```

### Build and tests

Use the live workflow as authority:

```text
fixture reproducibility
WinUI Release x64 restore/build
test-project restore/build
packaged app-container execution
TRX parsing
non-zero discovery and execution
all executed tests passed
both Decision 3 classes passed
artifact upload
existing privacy/security gates
```

A focused command supplements this result; it does not replace packaged verification.

### Manual diff review

Confirm:

```text
one interface
one sealed policy implementation
one immutable plan
one resolution envelope
one enum file
two focused test classes
4,096 default
exact user-request preservation
typed missing-limit outcomes
planning-context-v1 identity
no Decision 4–8 logic
```

### Evidence discipline

Do not claim:

```text
maximum safe context
hardware fit
runtime support
candidate ranking
successful model load
Hardware Inspection completion
```

Only after these checks pass should the implementation PR be marked ready.

---

## 11. Acceptance checklist

- [ ] Uses the canonical Decision 1 and Decision 2 types.
- [ ] `ApplicationDefault` selects `min(4,096, model limit)`.
- [ ] Explicit requests are preserved exactly.
- [ ] Above-limit requests are `Resolved / ExceedsModelLimit`.
- [ ] Missing and zero limits are `ModelContextLimitUnavailable`.
- [ ] Values above `int.MaxValue` are `ModelContextLimitOutOfRange`.
- [ ] All plan and resolution invariants reject contradictory construction.
- [ ] Policy identity is `PlanningContext / planning-context-v1`.
- [ ] Policy has no hardware, estimator, file, process, UI, network, time, or random
      dependency.
- [ ] Decision 8 remains the sole candidate-context owner.
- [ ] README and evidence state all non-claims.
- [ ] Packaged WinUI CI discovers and passes both Decision 3 test classes.
- [ ] Final retained evidence is tied to the exact reviewed SHA.

---

## 12. Stop conditions

Stop and report the exact blocker rather than guessing when:

```text
Decision 1 or Decision 2 contract shape differs from this plan
duplicate canonical types exist
the focused runner discovers zero intended tests
the packaged test route fails
the declared model context source is ambiguous
a proposed change would introduce Decision 4–8 behaviour
```

A missing `HardwareInspectionHandoff` does not block the pure policy module. It blocks
only later complete request composition/orchestration work that actually requires that
handoff.

---

## 13. Final scope statement

This plan implements only:

```text
context intent
baseline selection
relationship to model limit
typed not-established outcomes
policy identity
```

It leaves all resource and fit calculations to the remaining decisions. That narrow
scope is the main reason Decision 3 can be implemented quickly without sacrificing
correctness.
