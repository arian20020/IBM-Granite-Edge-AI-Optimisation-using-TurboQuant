# O1 OpenVINO optimisation adapter handoff

Date: 2026-08-24

Status: `SOURCE_READY_WITH_NATIVE_BLOCKERS`

Branch: `feature/openvino-optimisation-adapter-v1`

## Authoritative history

- Original OpenVINO/C1 V1 merge: `706d3cc4ca32364703eb2cea599a1557f8536e42`.
- C1 V2 merge: `a259776e16bcc2d98521aaf1d8775f83871fd417`.
- C1 V2 merge parents, in order:
  `8f4a7f559470d5024decdf72b5c522c470ff9333` and
  `892bc689627142e5ffbd0ef0c12d2c5e952bd5a2`.
- C1 V2.1 merge: `07ca7f9252f8a82768bfcf6ab096e2c9b779c9b9`.
- C1 V2.1 merge parents, in order:
  `a23bf5c5c55735fb4a23a92fd266714023de0743` and
  `e254385997392601102b16acf19244437803bdcc`.
- Source implementation tip before the final Task 3 handoff commit:
  `8f1ee8f484e2c10a944d2ee7059313093a34ff14`.
- Authoritative remote C1 tips resolve exactly to `892bc689...` and
  `e2543859...`; both are ancestors of the O1 source tip.

C1 V2/V2.1 files remain frozen after their authorized merge. O1 has not merged
into I0 or `main`, and Task 3 did not push.

## V2 accepting seam

`OpenVinoOptimizationPlanAdapter.Adapt` accepts only plans for which
`IsExecutableBy(2)` is true, `ContractVersion` is 2, every route is OpenVINO,
and `ExecutionPayload.OpenVino` is the sole route payload. Missing, GGUF, mixed,
mismatched, non-executable, or digest-disagreeing payloads return a bounded C1
`ReplanRequired` result before native work.

The adapter maps only the authoritative V2 payload. Configuration ID, device,
maturity, evidence ID, source/target weights, KV precision, all compiled-cache
facts, complete-package/persistence fact, all four OpenVINO build values, all
six optimizer versions, and TurboQuant absence are checked against current
evidence. `ConfigurationSha256` is recomputed through the public V2 issuer. A
focused compiled guard verifies that the adapter's configuration-identity
subgraph calls that public issuer exactly once and contains no local
cryptography, encoding/string-building, or JSON canonicalization and no hidden
invocation.

Runtime-only is derived only from equal payload source and target precision.
Unequal precision remains persistent conversion. The five released CPU
admissions retain their published IDs. O1 continues to advertise no TurboQuant
optimization candidate and rejects a payload or live evidence containing a
TurboQuant build identity.

For a raw V2 package with no prior O1 provenance, source precision is taken
exactly from the already verified C1 V2 OpenVINO payload; it is no longer
defaulted to Fp16. If provenance is present, its recorded output precision is
still read and must match the payload source precision or execution returns
`ReplanRequired` before native launch. The legacy V1 raw-package default remains
Fp16.

The explicitly named `OpenVinoOptimizationLegacyRequestV1` and
`OptimizeLegacyV1Async` compatibility seam remains isolated. V1 registry
admission now occurs only in `OptimizeLegacyV1Async`; the shared
`OptimizeCoreAsync` contains no V1 registry lookup.

The compiled static guard starts at public `ExecuteAsync`, follows every
same-module method operand for `call`, `callvirt`, `newobj`, `ldftn`, and
`ldvirtftn` without a namespace filter, records all IL instructions to reject
`calli`, and rejects an exact V1 `GetRequired` reference, any semantic O1
candidate-catalog lookup, reflection invocation/creation, runtime-binder/
`CallSite` dispatch, and non-allowlisted delegate invocation. Separately, it
enumerates the explicitly linked O1 production namespace roots in the
production module, counts authoritative payload-field overlap across properties
and ordinary/backing fields, and restricts substantial overlap to the explicit
candidate/provenance/profile evidence allowlist. Every production property or
field named `ExecutionPayload` must use a frozen C1 payload type.

This is a static compiled-reference guard over the shared accepting core. It
does not claim arbitrary runtime reachability through external or virtual
dispatch, and it does not reject unrelated result-evidence canonicalizers.
Existing source-snapshot verification delegates are exceptions only at exact
containing-method plus exact-delegate-type pairs. The same exact-site rule
applies to the service's disk-space and operation-ID delegates. Those methods
are directly checked for V1 lookup, reflection/dynamic dispatch, and `calli`,
and the delegate signatures cannot carry C1 plan/payload/candidate/
configuration authority types. This preserves testable package-verification
seams without granting a signature-wide invocation exception.

## Trusted execution and durable evidence

The service creates `TrustedSourceContext.ForPlan(plan,
<package>/openvino_model.bin)`, verifies it, and reveals the path only after
verification. The canonical revealed parent must equal the supplied package
root. This happens before package reads and again after independent live reads
at `BeforeStaging` and `BeforePublish`, before native conversion or publication.

Initial, before-staging, and before-publish checks independently reload and
verify the plan, inspection run/handoff, model digest/length, hardware
run/snapshot, capability snapshot, exact live build/tool evidence, V2 payload,
and recomputed C1 configuration digest.

Persistent schema-v2 provenance and runtime-only schema-v2 profiles retain an
opaque exact serialization of `ExecutionPayload.OpenVino` plus its SHA-256,
contract version 2, plan ID, and C1 `ConfigurationSha256`. Actual device, KV
precision, output weight precision, persistence result, and complete optimizer
dictionary must agree with the payload before publication.

The hardened lifecycle is otherwise unchanged:

- persistent conversion retains snapshotting, operation-owned staging,
  validation, smoke, final revalidation, atomic publication, reinspection,
  rollback, cancellation, cleanup, and typed `ReinspectionFailed` behavior;
- runtime-only execution performs no converter call and publishes no model
  package, but retains the hardened atomic profile transaction and all three
  live checks;
- terminal results remain bounded and contain no source path or raw tool output.

## Native V2 E2E

`ProjectedC1PlanPublishesSchemaV2PackageThroughSealedPipeline` is gated by both
the verified converter stage and official-worker stage. It projects live O1
capability evidence, issues the exact C1 V2 payload, checks strict adaptation,
calls `ExecuteAsync`, uses the sealed converter/official-worker path, and reads
schema-v2 provenance after publication. No replacement stage or digest is
invented.

The stage variables were absent during the latest fix-round verification. The
focused optimization class therefore discovered eight tests: five source-only
architecture tests passed and all three native E2Es skipped. No native pass is
claimed.

## Verification evidence

All commands ran from `C:\O1` with portable .NET SDK `10.0.301` and
`UseAppHost=false` for test execution.

The following totals are carry-forward pre-fix evidence. They were executed
before the service and architecture guard changed in fix round 1, so they are
not fresh post-fix passes:

| Carry-forward verification | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| OpenVINO component | 402 | 397 | 0 | 5 |
| C1 V2.1 complete project | 771 | 771 | 0 | 0 |
| OpenVINO contract project | 204 | 204 | 0 | 0 |
| OpenVINO worker-client | 13 | 13 | 0 | 0 |
| Worker-process integration, stable rerun | 65 | 48 | 2 | 15 |

Fresh final verification at `26a7711825bb746aa88657434f2de5a519258a9e` is:

| Post-fix verification | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Source-only architecture | 5 | 5 | 0 | 0 |
| Optimization E2E/architecture filter | 8 | 5 | 0 | 3 |
| OpenVINO component after source-precision fix | 405 | 400 | 0 | 5 |
| C1 V2.1 complete project | 771 | 771 | 0 | 0 |
| OpenVINO contract project | 204 | 204 | 0 | 0 |
| OpenVINO worker-client | 13 | 13 | 0 | 0 |
| Worker-process integration | 69 | 52 | 2 | 15 |

The integration test project also compiled fresh in Release with exit 0, zero
warnings, and zero errors.

The five component skips require `GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE`.
The 15 integration skips cover absent converter, official A/B, and physical GPU
inputs. The two integration failures require
`OPENVINO_TURBOQUANT_WORKER_STAGE`; they remain failures, not source passes.
One protocol case timed out in the first full integration observation, then
passed focused and in the stable full rerun; no Code Integrity 3033/3077 event
was found in the checked window.

A fresh focused legacy unit attempt after the service change executed zero
tests: Windows Application Control rejected the rebuilt unsigned test assembly
with `0x800711C7`. That observation is an environmental block, not a pass, and
no signing, staging, hash, or WAC control was bypassed.

The two-phase Debug x64 application build completed with restore exit 0 and
build exit 0, zero errors, and one `NETSDK1198` warning for absent
`win-x64.pubxml`. `DotNetHostPath` pointed nested worker packaging commands at
the same portable SDK. The build used
`OpenVinoOfficialWorkerPackagingRequired=false` and
`GenerateAppxPackageOnBuild=false` only because verified official-worker inputs
were absent. It is a Debug compile check, not Release packaging verification.

## Remaining native and release blockers

- verified official OpenVINO worker A/B closures and manifest digests;
- a verified converter closure for FP16/INT8/INT4 native export and V2 E2E;
- a verified TurboQuant worker stage for the separate prompting-route tests,
  without creating a TurboQuant optimization claim;
- authorized physical GPU evidence where required;
- exact release evidence inputs and final Release x64 packaging, privacy,
  cleanup, traceability, manifest, executable-hash, and ordered release gates.

Windows Application Control, native staging, closure/manifest verification,
executable hashes, and packaging gates were not weakened.

## Scope and delivery

The post-V2.1 O1 range modifies only the five existing OpenVINO optimization
production files and their named OpenVINO unit/integration tests. Frozen C1,
shared protocol, UI/XAML/navigation, `MainWindow`, GGUF executor, project,
solution, I0, and `main` paths are unchanged after the authorized import.

Task 3 records this handoff in
`docs(openvino): hand off C1 V2 source integration`. The coordinator owns final
whole-branch review, fresh verification, and the user-authorized push of only
`feature/openvino-optimisation-adapter-v1`.
