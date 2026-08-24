# O1 OpenVINO C1 V2 source handoff

Date: 2026-08-24

Branch: `feature/openvino-optimisation-adapter-v1`

Status: `READY_FOR_COORDINATOR_REVIEW_WITH_NATIVE_BLOCKERS`

## Exact contract imports

| Import | Merge | First parent | Second parent / authoritative C1 tip |
|---|---|---|---|
| C1 V2 | `a259776e16bcc2d98521aaf1d8775f83871fd417` | `8f4a7f559470d5024decdf72b5c522c470ff9333` | `892bc689627142e5ffbd0ef0c12d2c5e952bd5a2` |
| C1 V2.1 | `07ca7f9252f8a82768bfcf6ab096e2c9b779c9b9` | `a23bf5c5c55735fb4a23a92fd266714023de0743` | `e254385997392601102b16acf19244437803bdcc` |

Both second parents exactly equal their remote C1 branch tips and are ancestors
of O1. C1/shared contract files were not modified after the V2.1 merge.

## Source contract delivered

- The accepting adapter requires `IsExecutableBy(2)` and contract version 2.
- `plan.ExecutionPayload.OpenVino` is the sole execution authority; missing,
  GGUF, mixed, or mismatched unions fail closed before native work.
- Every execution-affecting payload value and current build/tool evidence is
  checked exactly, including the slash-bearing official runtime build identity.
- C1 `ConfigurationSha256` is recomputed with the public V2 issuer. The adapter
  configuration-identity subgraph is statically required to call that issuer
  exactly once and to contain no local SHA/string/JSON canonicalization.
- TurboQuant build identity remains rejected and O1 makes no TurboQuant
  optimization claim.
- `TrustedSourceContext` verifies and gates source disclosure before package
  access, before native staging, and before publication.
- Schema-v2 provenance/profile evidence binds the exact opaque payload and its
  SHA-256, plan ID, contract version, C1 configuration digest, live identities,
  and actual completion evidence.
- Runtime-only, persistent conversion, atomic publication, rollback,
  reinspection, cancellation, cleanup, and bounded-result behavior remain.
- The V1 compatibility seam is explicitly named, and its registry admission is
  performed only by `OptimizeLegacyV1Async`, outside shared `OptimizeCoreAsync`.
- A compiled static guard starts at `ExecuteAsync`, traverses same-module
  `call`, `callvirt`, `newobj`, `ldftn`, and `ldvirtftn` operands without a
  namespace filter, covers the strict adapter, and rejects exact V1
  `GetRequired`, semantic O1 candidate-catalog lookups, and hidden
  reflection/dynamic invocation APIs.
- Semantic member/field-overlap invariants require the frozen C1 union and
  OpenVINO payload types and admit substantial overlap only for the explicit
  route-native candidate/result-evidence types. This does not claim arbitrary
  runtime reachability through external or virtual dispatch and does not reject
  unrelated evidence canonicalizers.

## Fresh verification evidence

| Suite | Result |
|---|---|
| OpenVINO component | 402 total; 397 passed; 0 failed; 5 skipped |
| C1 V2.1 | 771 total; 771 passed; 0 failed; 0 skipped |
| OpenVINO contracts | 204 total; 204 passed; 0 failed; 0 skipped |
| OpenVINO worker-client | 13 total; 13 passed; 0 failed; 0 skipped |
| Full worker-process integration, stable rerun | 65 total; 48 passed; 2 failed; 15 skipped |
| Optimization E2E/architecture | 5 total; 2 passed; 0 failed; 3 skipped |

The complete source suites and source-only architecture proof pass. Native
observations remain separate: five component skips require the official worker;
the three optimization E2Es require the converter (and the plan-bound service
E2E also requires official stage A); the full integration run has 15 absent
stage/GPU skips and two explicit missing-TurboQuant-stage failures.

The two-phase Debug x64 application compile passed: restore exit 0, build exit
0, zero errors, one `NETSDK1198` missing-publish-profile warning. The nested
worker build used the portable SDK through `DotNetHostPath`. Packaging-disabled
flags were used only because verified official-worker inputs do not exist in
this environment. This is not Release packaging verification.

## Native nonclaims and blockers

No verified converter, official A/B worker, TurboQuant worker, or authorized
physical GPU stage was available. Consequently, no native conversion,
official-worker execution, TurboQuant execution, GPU, activation, or Release
packaging pass is claimed. Final Release packaging still requires verified
closures and digests plus unchanged WAC, manifest, executable-hash, privacy,
cleanup, traceability, and ordered release gates.

## Repository boundary

From the original V2 merge, the only shared C1 changes are those imported by
the authorized V2.1 merge. From V2.1 onward, changes are confined to the
existing O1 optimization implementation and named OpenVINO tests/handoffs.
There are no UI/XAML/navigation, `MainWindow`, GGUF executor, shared protocol,
project, solution, I0, or `main` changes.

Task 3 records this handoff in
`docs(openvino): hand off C1 V2 source integration` and stops before push. The
coordinator must perform fresh final review, push only this feature branch, and
verify the remote tip equals the local tip.
