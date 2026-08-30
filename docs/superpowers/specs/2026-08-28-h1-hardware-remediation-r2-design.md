# H1 Hardware Remediation R2 Design

## Authority and scope

This lane starts from C0 commit `a5ef3558334e50587889140dafba194853938765`
and tree `90c34ab009b744d7b00866fb93e8dbc86363f1b2`. The frozen ancestor is
`4748fe04f19afdf6b27c4c12502b84db325e7294` with tree
`fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`.

H1 owns Hardware Inspection facts, resolution, probes, packaging, memory-budget
calculation and the hardware-to-compatibility boundary. Shared onboarding,
navigation, composition, Model Inspection behavior and OpenVINO runtime behavior
remain unchanged.

## Typed memory boundary

Four roles remain distinct throughout calculation and presentation:

- total physical memory;
- currently available physical memory;
- proportional safety reserve;
- executable model budget.

Each role receives a dedicated immutable value type. Construction validates its
own domain, while a single `SystemMemoryBudgetCalculator` validates the
cross-value invariant and calculates the reserve with integer arithmetic:

`reserve = min(available, max(ceil(available / 10), 512 MiB))`

`executable = available - reserve`

The calculation never multiplies an untrusted `ulong`, never underflows and
cannot exceed the current available-memory observation. Fit assessment,
presentation and optimization issuance consume the same result rather than
copying the formula. Compatibility factories accept role-specific values at the
boundary so a caller cannot substitute installed memory for currently available
memory without an explicit, reviewable conversion.

## Stable hardware snapshot identity

Every usable `HardwareSnapshot` publishes a `HardwareSnapshotIdentity` containing
the existing snapshot UUID and a lowercase SHA-256 digest. A private canonicalizer
length-prefixes normalized typed facts, invariant numeric values, UTC timestamps,
ordered enum values and safe evidence provenance. It exposes only the digest,
never the canonical buffer or provider output. Local paths, usernames, hostnames
and raw native text are not inputs.

`HardwareInspectionHandoff` requires the inspection UUID to equal the snapshot
UUID. GGUF and OpenVINO compatibility preparation retain the exact snapshot
digest from the handoff instead of manufacturing a different digest from a
partial compatibility projection. Existing hardware-facts digests remain a
separate authority and are not substituted for the snapshot identity.

## Controlled packaging

The llama.cpp probe publish target retains restore, publish, VC runtime
resolution, manifest generation and manifest verification. After verification,
MSBuild emits `Content` through task outputs from the resolved controlled file
items; no package-relevant `Content Include="@(...)"` expression remains in the
project graph. The package stays flat, bounded and manifest-exact.

H1 debug fixture/gallery source remains in the repository for audit history but
is excluded from app and packaged-test compilation/content for every
configuration. Hardware package tests evaluate Debug x64 and non-target graphs,
reject unresolved package expressions and reject audit, reference, fixture,
gallery, evidence, raw-result and private-path content.

## Process safety and evidence

Existing bounded timeouts, cancellation propagation, Job-object child cleanup,
strict process exit handling, tool-manifest verification and fail-closed errors
are preserved. Focused process tests prove those contracts after packaging
changes.

The implementation/tests commit becomes the evidence subject. Verification then
produces the schema-v1 H1 manifest with stable output kinds
`hardwareSnapshot`, `availableMemory` and `safetyBudget`, followed by the report
commit. Raw logs remain outside Git. The final receipt is schema-validated and
atomically published only after the worker branch is pushed and clean.
