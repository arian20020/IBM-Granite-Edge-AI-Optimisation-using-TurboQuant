# M1 R3 Model Inspection Closure Design

## Status and authority

This design implements the user-approved M1 portion of the Final R3 Closure
Contract. The authoritative starting point is
`0880253b44f9319bf5707185bfe3560eed1bf8b8` with tree
`fd8b3a12fed94f4f1aa7f881cbd2fc2dbf39a280`, on the new branch
`audit/ucl-m1-model-inspection-remediation-r3` in `C:\UCL-M1-R3`.

M1 directly owns R3-001, R3-002, R3-003, and R3-004. M1 supports R3-016
by making unresolved GGUF package closure fail visibly, R3-019 by respecting
the H1-to-M1 native order, R3-021 by deriving evidence from committed files,
and R3-022 by publishing truthful managed/native disposition. M1 does not
claim ownership of frontend, optimization, security-package production
targets, shared C0 composition, or the integrated end-to-end journey.

## Environment and isolation

The dirty main checkout is out of scope and must not be modified. All R3 work
occurs in the isolated worktree. Preflight found the required .NET 10.0.301 SDK
and .NET 8 runtime, 6.6 GB free on the workspace volume, committed native input
stages, no native lock, and no H1 handoff or native receipt. Managed work may
proceed. Native work may not begin until fresh H1 receipts validate.

The untouched R2 contract baseline builds with zero warnings and passes
387/387. That green baseline is not closure evidence: its tests do not detect
the four R3 defects.

## Chosen architecture

### Exact route-bound projection

`ModelInspectionProjectionFactory` remains the sole adapter into the shared
schema-v2 contract, but it will no longer create a second route handoff.
Instead it consumes the exact handoff already produced by each route:

- `CreateGguf(ModelInspectionHandoff handoff)` validates and projects the
  existing GGUF handoff.
- `CreateOpenVino(GraniteEdgeAI.OpenVino.Contracts.ModelInspectionHandoffV2 handoff)`
  validates and projects the existing OpenVINO handoff.

This preserves route-specific parsing and validation and prevents a second,
unrelated handoff ID from being generated during projection.

The live GGUF call site is `ModelInspectionHandoffRegistry.TryIssue`. After
`ModelInspectionHandoffProjector.TryProject` succeeds, the registry creates the
projection from that exact handoff and stores both in its one-use entry. The
registry exposes the projection only through a path-private lookup tied to the
handoff ID, and invalidation retires the entry as today. The production
registration remains the single `_handoffRegistry = new()` field on
`OnboardingShellPage`.

The live OpenVINO call site is `OpenVinoRouteService.InspectAsync`. After
`OpenVinoInspectionHandoffFactory.Create` succeeds, the route creates the
projection from that exact handoff and places it in the existing
`OpenVinoRouteHandoffLease`. `StartSessionAsync` validates the projection and
requires its run ID, handoff ID, digest, length, outcome, route, and model type
to match the lease handoff before consuming the path-bearing descriptor. The
production composition remains the single route created by
`ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService` and held
by `ModelInspectionPage`.

No new service locator, route registration, project reference, package
reference, or shared C0 composition change is introduced.

### Executed package-closure verification

The package-boundary contract will not accept any unresolved `$(...)`,
`@(...)`, or `%(...)` value by source-path allowlist. The imported-expression
allowlist and its booleans will be removed.

Package verification will use an MSBuild evaluation harness committed in the
contract test project. It imports the production project/targets, supplies
controlled stage roots and required properties, executes the relevant item
construction targets, and writes the final package-relevant item identities
and target paths to a bounded JSON result. Tests compare the normalized,
fully evaluated closure against prohibited roots. Any expression that survives
evaluation, missing result, duplicate target, escape outside the controlled
root, or non-zero MSBuild exit fails closed.

This behavior makes R3-016 visible without M1 editing the GGUF packaging target.
If the evaluated GGUF closure remains unresolved, M1 reports the exact external
owner blocker rather than claiming green.

### Complete production DebugFixtures coverage

Fixture-boundary tests will discover every production `Features/*/DebugFixtures`
root represented by the application project and filesystem. The current roots
are Model Inspection, Onboarding, Hardware Inspection, and Model Hardware
Compatibility. No static M1-only root list is authoritative.

For each discovered root, executable MSBuild evaluation proves:

- default, Release, x86, and ARM64 closures contain no item from the root;
- Debug x64 contains only the explicit items authorized by the production
  project condition;
- a newly introduced production root without matching exclusion/evaluation
  coverage fails the regression test;
- gallery opt-ins remain separate from ordinary Debug x64 where the production
  project defines a separate opt-in property.

Tests operate on evaluated Compile, Page, Content, None, EmbeddedResource, and
PRIResource items rather than matching source strings.

### Reproducible evidence arithmetic

A committed evidence validator will parse manifests and receipts as JSON and
enforce:

- `executed == passed + failed + skipped`;
- `discovered >= executed`;
- exact report/manifest bytes and SHA-256;
- evidence subject commit/tree distinct from final tip/tree;
- frozen ancestry and pushed remote identity;
- clean worktree and R3-only artifact paths;
- receipt-to-manifest identity and, when native is run, native receipt binding
  to the exact handoff receipt bytes.

The R3 report, evidence manifest, and external handoff receipt are generated
from committed files at the exact tested commit. R2 receipts are not reused or
accepted as R3 evidence. Missing H1 prerequisites produce `nativeDisposition:
not-run` and a truthful blocker; they do not become passing rows.

## Production reachability

| Production API | Definition | Real caller | Composition/registration | Behavioral regression |
| --- | --- | --- | --- | --- |
| `ModelInspectionProjectionFactory.CreateGguf` | Model Inspection infrastructure | `ModelInspectionHandoffRegistry.TryIssue` | single onboarding `_handoffRegistry` | issuing a real completed handoff yields an exact matching schema-v2 projection; stale/mismatched issuance yields none |
| `ModelInspectionProjectionFactory.CreateOpenVino` | Model Inspection infrastructure | `OpenVinoRouteService.InspectAsync` | `ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService` | real static/native route evidence yields a lease whose projection matches the exact handoff |
| OpenVINO lease projection validation | `OpenVinoRouteHandoffLease` / `StartSessionAsync` | live route session startup | same single OpenVINO route | mutated projection identity is rejected before descriptor consumption or worker session start |
| Evidence arithmetic validator | committed audit validation script | R3 evidence/receipt publication workflow | invoked directly by M1 publication commands | invalid R2 arithmetic fails; valid R3 arithmetic and joins pass |

No new production registration is required. Existing registry and route
registrations remain exactly one each and are verified behaviorally.

## RED-GREEN verification design

R3-001 RED uses the committed validator against a fixture with the R2 defect:
`executed` excludes seven skipped tests. It must exit non-zero with an arithmetic
error. GREEN uses the R3 manifest and receipt and verifies all joins and bytes.

R3-002 RED evaluates a controlled imported package target whose final closure
retains an item/property expression. The test must fail without consulting a
source allowlist. GREEN evaluates controlled resolved closures. Production GGUF
closure is then evaluated separately and remains an owner blocker if unresolved.

R3-003 RED mutates a temporary application project with a new production
DebugFixtures root lacking complete exclusions and conditions. The evaluator
must report that root. GREEN covers every real root across Debug x64 and
non-target configurations.

R3-004 RED tests invoke the actual GGUF registry and OpenVINO route and observe
that no schema-v2 projection is produced/retained on the R2 baseline. GREEN
observes exact route/result/handoff identity, then mutates each identity field
and verifies fail-closed behavior before downstream activation.

Existing Model Inspection contracts, transport, worker, worker-client,
worker-process, deterministic runtime, OpenVINO managed route, hardware-handoff,
fixture configuration, package/privacy, duplication, and `git diff --check`
gates run after the focused GREEN tests. Builds and tests report discovered,
executed, passed, failed, and skipped separately; no zero-discovery, skipped,
blocked, build-only, or mock-only row is called passing behavior.

## Security and privacy invariants

Projection remains capped at 1024 UTF-8 bytes and canonical parsing remains
fail closed. It exposes no path, filename, username, hostname, raw provider
output, credentials, tokens, prompts, or model data. Digest, byte length,
UUIDv4 role separation, route, model type, and outcome remain exact.

No native trust root, package signing, manifest, certificate, firewall,
Application Control, timeout, cancellation, streaming hash, worker cleanup, or
protocol limit is weakened. Test stage roots are controlled temporary roots;
they are never packaged or committed. Publication uses a same-directory
temporary file and atomic rename only when the destination does not exist.

## Evidence and delivery

Implementation/tests are committed before report/manifest artifacts. The
evidence subject identifies the implementation commit/tree, not the later
evidence commit. The final branch alone is pushed. The external receipt is
published only after schema, arithmetic, Git blobs, hashes, byte counts,
ancestry, remote ref, clean worktree, privacy, and native-order checks pass.

The final response lists owned/supporting R3 IDs, exact base/tip/tree, changed
paths, production callers and registrations, RED and GREEN evidence, non-zero
totals, builds/package results, native disposition, external blockers, artifact
hashes, pushed remote ref, and confirmation that main was neither merged nor
pushed.
