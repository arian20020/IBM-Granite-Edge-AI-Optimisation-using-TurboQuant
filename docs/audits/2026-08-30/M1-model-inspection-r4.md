# M1 Model Inspection R4

Date: 2026-08-30
Worker: M1
Branch: `audit/ucl-m1-model-inspection-remediation-r4`
Base commit: `282a7690edd9bfbb48dbb324d09e76a7a154652e`
Base tree: `812c22ea640633c5e8835266b902fb802728eea9`
Frozen campaign commit: `4748fe04f19afdf6b27c4c12502b84db325e7294`
Frozen campaign tree: `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91`
Implementation subject: `044e65bd48ddc02e56d1ba3678235869cba8a059`
Implementation tree: `f1d8c0dff5edd481a092e6f4f11f374199f21e0f`

## Outcome

The M1-owned managed implementation is complete at the subject above. The
shared Model Inspection contract is the single public authority for the exact
six-field schema-v2 handoff. GGUF and OpenVINO retain route-specific parsing,
inspection, and custody, but their adapters now emit identical canonical bytes
with strict ordering, schema version 2, distinct UUIDv4 roles, lowercase
SHA-256, positive model length, and a 512-byte ceiling. No optimization,
hardware, path, filename, prompt, identity, provider-response, metadata, or
model-byte field enters the handoff.

OpenVINO inspection now preserves typed ready, warning, conversion-required,
incomplete, unsupported, dependency-unavailable, cancelled, timed-out,
invalid-evidence, and stale-evidence outcomes through inspection,
presentation, conversion validation, published-output reinspection, and chat
activation. Completed worker evidence is validated before handoff issuance.
Projection validation still occurs before the path-bearing lease is consumed,
and rejected, stale, cancelled, or superseded operations dispose their leases,
offers, sessions, and staging custody.

The exact-subject evidence disposition is `mixed`. All M1-owned managed rows
pass. One C0-owned shared project-composition assertion fails on 28 prohibited
Model Optimization fixture items; 27 WorkerClient cases are rejected before
session start by executable trust policy; and seven OpenVINO package-stage
cases are skipped because approved stage roots were not supplied. Debug x64
application and packaged-test builds stop at an existing non-M1 project
reference error, so no exact executable, file-13 screenshot set, native route
acceptance, or performance acceptance is claimed.

## Production implementation

- `ModelInspectionHandoffV2` in
  `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/ModelInspectionProjectionV2.cs`
  owns validation, canonical serialization, strict parsing, and the 512-byte
  bound. The former independently implemented OpenVINO handoff type and codec
  were removed. The GGUF codec and OpenVINO factory consume the shared type.
- GGUF `ModelInspectionProjectionFactory.CreateGgufHandle` remains live only
  through `ModelInspectionHandoffRegistry`. Focused coverage retains issue,
  claim, rollback, reissue, invalidation, exact identity, and no-reuse
  semantics. One registry instance remains registered by the existing shell.
- OpenVINO `ModelInspectionProjectionFactory.CreateOpenVino` remains live
  through `OpenVinoRouteService.InspectAsync`, which is obtained through
  `ModelInspectionServiceComposition.CreateDefaultOpenVinoRouteService`.
  `StartSessionAsync` validates the shared projection against the exact issued
  handoff before consuming the descriptor-bearing lease.
- `OpenVinoRouteInspectionResult`, `OpenVinoInspectionPresentationPolicy`, and
  `OpenVinoActivationOutcomePolicy` preserve typed terminal meanings. The page
  exposes typed `*WithResultAsync` activation methods; its existing Boolean
  methods are narrow final compatibility wrappers for the unchanged shell.
  Programming faults are no longer hidden by broad page catches.
- The sealed conversion pipeline preserves cancellation and timeout during
  validation and reinspection. Conversion-required source intent remains a
  one-time offer and never becomes inspected-model success.
- Evaluated MSBuild closure resolves a concrete `dotnet` host in deterministic
  explicit/current/SDK-root/standard-install/approved-fallback order. Tests
  mutation-protect construction order, candidate precedence, filename,
  existence, absolute normalization, and no-PATH behavior.
- All 13 workflow/test-infrastructure paths changed for Task 3 have current R4
  cleanup-ledger narratives. The cleanup inventory is exactly 705/705 and its
  verifier is 3/3.

## Behavioral RED/GREEN and mutation evidence

| Area | RED | GREEN and mutation evidence |
|---|---:|---|
| Canonical shared handoff and route parity | Shared 12/12 and OpenVINO 1/1 failed before authority centralization | Shared 28/28; OpenVINO factory 13/13; same-role UUID mutation failed, then 206/206 OpenVINO contract rows passed |
| Typed OpenVINO inspection | 22 failures in the 28-case new-behavior matrix | 28/28; route/state focus repeated 43/43; timeout mapping mutation failed 1/24; final typed focus 54/54 |
| Typed presentation | 7 failures in 11 direct behavior rows | 11/11; dependency disposition mutation failed 1/8; production page reachability retained |
| Portable MTP closure | 3/3 failed: 13 legacy workflow invocations, unavailable clean-environment host, no candidate policy | 3/3; selector/order focus 5/5; precedence, existence, and actual construction-order mutations each failed as intended |
| Completed OpenVINO evidence and single authority | 13/13 unit rows and 1/1 authority row failed | 14/14; invalid-to-stale mutation failed 8/8; OpenVINO contracts 202/202; exactly one public handoff authority |
| Conversion and activation typing | 17/17 failed on cancellation/timeout collapse and absent typed activation linkage | 17/17; cancellation-recognition mutation failed 2/6; focused adversarial aggregate 87/87 |

## Exact-subject managed verification

These ten non-overlapping commands form the receipt arithmetic. Discovery
equals execution, and `2,585 = 2,550 + 28 + 7`.

| Gate | Discovered | Passed | Failed | Skipped | Disposition |
|---|---:|---:|---:|---:|---|
| Model Inspection contracts | 428 | 427 | 1 | 0 | Mixed: sole C0 fixture-composition row |
| Model Inspection transport | 27 | 27 | 0 | 0 | Passed |
| Model Inspection worker | 78 | 78 | 0 | 0 | Passed |
| Model Inspection WorkerClient | 119 | 92 | 27 | 0 | Mixed: generated executable rejected before session start |
| Worker-process structural/package slice | 8 | 8 | 0 | 0 | Passed |
| Model/Hardware Compatibility | 1,050 | 1,050 | 0 | 0 | Passed |
| Hardware Inspection Foundation | 202 | 202 | 0 | 0 | Passed |
| Deterministic LLamaSharp | 191 | 191 | 0 | 0 | Passed |
| GGUF live handoff projection | 2 | 2 | 0 | 0 | Passed |
| OpenVINO managed route | 480 | 473 | 0 | 7 | Mixed: approved native stage roots absent |
| **Aggregate** | **2,585** | **2,550** | **28** | **7** | **Mixed** |

Additional exact-subject checks passed: cleanup verifier 3/3 at 705/705,
`git diff --check`, the sensitive/path scan, forbidden projection-field scan,
single schema/type and registry scans, zero owned post-test processes, and
`git apply --check` for
`docs/audits/2026-08-28/proposals/M1-R3-shared-project-fixture-boundary.diff`.

Independent requirements and adversarial security reviews found no remaining
Critical or Important issue after the final corrections. The final focused
adversarial review passed 87/87.

## Build, application, visual, and native disposition

The exact-subject Debug x64 application build and packaged-test build both
exit nonzero before the M1 page compiles because two existing non-M1 app files
cannot resolve the already-built `GraniteEdgeAI.GgufRuntime.Capabilities`
namespace. The worker structural/package gate still passes 8/8, but no exact
application executable is produced. Consequently the required exact-app
launch, both-route file-13 smoke, 100%/200% scaling checks, screenshots, and
visual defect ledger are blocked before activation. No fixture screenshot or
different commit's executable substitutes for that evidence.

`nativeDisposition` is `blocked`. This non-authoritative development machine
does not establish Intel-native GGUF/OpenVINO execution, signed-package or App
Control acceptance, installed-layout acceptance, performance, quality, or
end-to-end visual acceptance.

## C0 integration note

1. Merge implementation subject
   `044e65bd48ddc02e56d1ba3678235869cba8a059` and preserve
   `shared/GraniteEdgeAI.ModelInspection.Contracts/Evidence/ModelInspectionProjectionV2.cs`
   as the sole schema-v2 handoff/projection authority. Do not restore the
   removed OpenVINO duplicate codec.
2. Preserve the live GGUF caller through `ModelInspectionHandoffRegistry` and
   the live OpenVINO caller through `OpenVinoRouteService.InspectAsync` plus
   exact `OpenVinoRouteHandoffLease` validation-before-consumption.
3. Preserve `OpenVinoRouteInspectionResult`,
   `OpenVinoInspectionPresentationPolicy`, `OpenVinoChatActivationResult`, and
   `OpenVinoActivationOutcomePolicy` across merge conflict resolution.
4. Q1 must consume the six canonical fields at the subsequent optimization
   seam. Do not add Q1 optimization, hardware, export, or product-result fields
   to the M1 handoff. Preserve `modelInspectionRunId`,
   `modelInspectionHandoffId`, `modelSha256`, and `modelLengthBytes` exactly.
5. Apply or supersede the still-apply-checkable shared project fixture-boundary
   proposal, then rerun the seven-context evaluated closure and require zero
   prohibited fixture items.
6. Integrate the S1 trust-boundary branch before rerunning WorkerClient and
   process acceptance, supply independently verified OpenVINO stage roots, and
   rebuild the exact Debug x64 app/package before the file-13 visual and native
   gates.

## Scope and non-claims

No XAML, shell, project, solution, package-composition, Q1 optimization,
hardware-fact, or F1 frontend file was changed. No machine policy, trust,
signing, registry, firewall, or installation setting was weakened. No raw TRX,
model, prompt, local path, filename, user/machine identity, or provider output
is committed as evidence. No failed, skipped, blocked, build-only, structural,
or mock-only row is represented as native or user-journey success.
