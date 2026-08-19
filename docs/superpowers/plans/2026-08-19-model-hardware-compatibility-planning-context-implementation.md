# Decision 3 — Pragmatic Planning-Context Policy Implementation Plan

> **Execution skill:** Use `superpowers:executing-plans` or
> `superpowers:subagent-driven-development`.

**Decision:** 3 of 8  
**Status:** Approved implementation plan  
**Plan date:** 2026-08-19  
**Scope:** Decision 3 policy, tests, documentation, CI discovery, and evidence  
**Expected size:** One short implementation slice, approximately half to one focused
working day once Decision 1 and Decision 2 contracts are available  
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

It implements no hardware inspection, memory formula, candidate generation, ranking,
fit classification, or Runtime Verification.

---

## 2. Practical delivery rule

Keep the controls that protect correctness and evidence, but do not turn this small
policy into a large subsystem.

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
extra examples beyond the required cases
additional source scans beyond the required boundary scan
```

### Deliberately omitted

```text
separate architecture-test project
brittle reflection tests of exact method lists
concurrency stress for a stateless calculation
configuration file for 4,096
one remote commit per enum or value object
full packaged test run after every small file
```

---

## 3. Starting conditions

Before creating a Decision 3 source file:

1. work on a dedicated branch or worktree;
2. record the exact starting SHA;
3. confirm the branch contains the accepted Decision 1 contracts:
   `ContextTokenCount`, `CompatibilityContextMode`, and
   `CompatibilityContextRequest`;
4. confirm it contains the Decision 2 policy identity contracts:
   `CompatibilityPolicyIdentity`, `CompatibilityPolicyKind`, and
   `CompatibilityPolicyVersion`;
5. search for duplicate definitions and keep one canonical owner;
6. read the live `.github/workflows/build-and-test.yml`;
7. run the existing baseline verification appropriate to that branch.

If Decision 1 or Decision 2 implementation lives on another branch, base this slice
after those commits. Do not recreate or temporarily wrap their contracts.

`HardwareInspectionHandoff` is not needed to implement or unit-test this pure policy.
It is needed later when the complete Compatibility request and orchestrator are wired.

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

Do not add a temporary Compatibility orchestrator in this slice.

---

## 5. Global implementation constraints

- Reuse Decision 1 and Decision 2 types.
- Treat every `ContextTokenCount` as the total runtime context window, not prompt
  length or output-token limit.
- Keep `DefaultTargetTokens = 4_096`.
- Keep policy identity `PlanningContext / planning-context-v1`.
- Preserve an explicit user request exactly.
- Never silently clamp, round, bucket, or normalise a requested count.
- Treat the trusted declared model limit as the version-one upper bound.
- Return `NotEstablished` for missing, zero, or out-of-range model limits.
- Do not add automatic RoPE or YaRN extension.
- Keep the policy deterministic, stateless, synchronous, and side-effect free.
- Add comments for logical blocks and non-obvious invariants; avoid comments that
  merely repeat syntax.
- Do not log paths, raw model metadata, hardware identity, command lines, credentials,
  or machine/account information.
- Keep every reviewable commit internally complete. Never push a valid request mode
  that deliberately throws because another task is scheduled later.
- A changed context creates a new immutable request and Compatibility run; it never
  mutates a historical result.
- A later selected candidate must pass an explicit context to Runtime Verification;
  do not use `--ctx-size 0` as the application's planning choice.

---

## 6. TDD and commit strategy

Use grouped red/green cycles rather than a remote commit for each small type.

```text
1. write complete failing contract tests
2. implement enums, plan, resolution, and interface
3. write every failing policy-path test
4. implement ApplicationDefault and UserRequested together
5. add boundary checks
6. update README, CI discovery, and evidence
7. run final verification on the exact review head
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

Cover:

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
relationship inconsistent with request and model limit is rejected
wrong policy kind or version is rejected
Resolved requires a plan and no failure reason
NotEstablished requires no plan and one non-Unspecified reason
```

Use a focused command only when its output proves that the intended class was
discovered and at least one test executed. Otherwise use the existing packaged runner
or a build-only red proof. Do not weaken the project for a convenient command.

### Minimum implementation

- Add the three enums from the design.
- Add deeply immutable `CompatibilityPlanningContextPlan`.
- Add `CompatibilityPlanningContextResolution` with private construction and named
  factories:
  - `Resolved(plan)`
  - `NotEstablished(reason)`
- Add the policy interface exactly as specified.
- Validate every cross-property invariant before assigning properties.

### Verify

```text
focused contract tests green
test project compiles
one canonical ContextTokenCount
one canonical policy identity family
```

Commit the complete contract package together.

---

## 8. Task 2 — Complete policy and behaviour tests

### Files

```text
CompatibilityPlanningContextPolicy.cs
CompatibilityPlanningContextPolicyTests.cs
```

### Write all failing cases first

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

The class has one parameterless construction path, no mutable instance state, and no
file, process, network, UI, time, hardware, or estimator dependency.

### Verify

```text
focused policy tests green
focused contract tests green
all Decision 3 tests green
```

Commit the policy and complete behaviour tests together.

---

## 9. Task 3 — Boundary guard, README, CI, and evidence

### 9.1 Boundary scan

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

Also scan production code for rejected behaviour:

```text
16,384 as an automatic default
--ctx-size 0 as the application decision
silent clamp
automatic RoPE or YaRN extension
embedded context ladder values
```

Explanatory README/design text may mention rejected choices; production code may not.

### 9.2 Feature README

Explain in beginner-readable language:

```text
ContextTokenCount = total runtime context window
ApplicationDefault = min(4,096, declared model limit)
UserRequested = exact request, never silently clamped
missing model limit = not established
4,096 is not a model limit or fit guarantee
maximum safe context is calculated later
Decision 8 owns candidate context values
changing context starts a new Compatibility run
selected candidates use an explicit runtime context
```

### 9.3 CI discovery

Preserve the existing packaged WinUI route. Append both fully qualified Decision 3
test classes to the workflow's required-class list without removing any existing class.

### 9.4 F-M09 evidence

Create a concise record containing:

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

Do not include raw local paths, private UCL information, machine identity,
credentials, or unredacted logs.

### 9.5 Optional cross-links

Add one reciprocal Decision 3 link to Decision 1 and Decision 2 documents only when
their canonical paths are present and the edits are trivial. Do not expand the slice for
this.

Commit the README, CI assertion, and evidence together.

---

## 10. Task 4 — Final verification

Run fresh verification on the exact head intended for review.

### Static checks

```text
git diff --check
repository-supported format verification
duplicate-owner search
prohibited-dependency scan
rejected-behaviour scan
```

### Build and packaged tests

Use the live workflow as authority:

```text
fixture reproducibility
WinUI Release x64 restore and build
test-project restore and build
packaged app-container execution
TRX parsing
non-zero discovery and execution
all executed tests passed
both Decision 3 classes passed
artifact upload
existing privacy and security gates
```

A focused test command supplements this result; it does not replace packaged
verification.

### Manual diff review

Confirm:

```text
one interface
one sealed policy implementation
one immutable plan
one resolution envelope
one enum file
two focused test classes
4,096 model-aware default
exact request preservation
typed missing-limit outcomes
planning-context-v1 identity
total context-window semantics
new request/run after context change
explicit runtime context handoff
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

Only after these checks pass may the implementation PR be marked ready.

---

## 11. Acceptance checklist

- [ ] Uses canonical Decision 1 and Decision 2 types.
- [ ] Treats context count as the total runtime context window.
- [ ] `ApplicationDefault` selects `min(4,096, model limit)`.
- [ ] Explicit requests are preserved exactly.
- [ ] Above-limit requests are `Resolved / ExceedsModelLimit`.
- [ ] Missing and zero limits are `ModelContextLimitUnavailable`.
- [ ] Values above `int.MaxValue` are `ModelContextLimitOutOfRange`.
- [ ] Plan and resolution invariants reject contradictory construction.
- [ ] Policy identity is `PlanningContext / planning-context-v1`.
- [ ] Policy has no hardware, estimator, file, process, UI, network, time, or random
      dependency.
- [ ] Decision 8 remains the sole candidate-context owner.
- [ ] Context changes create new immutable requests and runs.
- [ ] Selected candidates later pass explicit context values to Runtime Verification.
- [ ] README and evidence state all non-claims.
- [ ] Packaged WinUI CI discovers and passes both Decision 3 test classes.
- [ ] Final retained evidence is tied to the exact reviewed SHA.

---

## 12. Stop conditions

Stop and report the exact blocker rather than guessing when:

```text
Decision 1 or Decision 2 contract shape differs from this plan
duplicate canonical types exist
the intended focused tests are not discovered
the packaged test route fails
the declared model context source is ambiguous
a proposed change introduces Decision 4–8 behaviour
```

A missing `HardwareInspectionHandoff` does not block this pure policy module. It
blocks only later request composition or orchestration that actually consumes that
handoff.

---

## 13. Final scope statement

This plan implements only:

```text
context-window intent
baseline selection
preservation target
relationship to the declared model limit
typed not-established outcomes
policy identity
```

All resource and fit calculations remain in later decisions. This narrow boundary is
why Decision 3 can be implemented quickly without sacrificing correctness.
