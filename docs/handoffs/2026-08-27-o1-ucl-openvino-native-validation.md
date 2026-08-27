# O1 OpenVINO UCL Validation Handoff

## Scope and source

- Owner: UCL-O1 (OpenVINO route)
- Branch: `validation/ucl-openvino-native-v1`
- Frozen base: `f599c358181bd4da44087ab0c64d36d02d23316a`
- Tip: the commit containing this handoff and its focused route-local repair
- Source gate: passed before edits; local and remote frozen refs matched and the
  worktree was clean.

Only OpenVINO route production code, OpenVINO-specific tests, and this handoff
were changed. No application project, packaging, onboarding, shared UI,
navigation, or shared ModelHardwareCompatibility source was edited.

## Route-local repair

The integrated shared planner now filters preference resolution through V3
admission proofs, while the frozen O1 contract and public legacy issuer still
require exact C1 V2/V2.1 plans. Imported O1 tests were consequently unable to
create their V2 fixtures. A test-only factory now reconstructs the internal
selection boundary and still delegates all candidate/payload agreement checks
to the public V2 issuer.

The production route also accepted a frozen V2 plan in its adapter and then
rejected the same plan while validating durable provenance because that check
required only the current contract version. Durable plan validation now accepts
the contract's supported executable range (minimum through current). The focused
published-provenance regression changed from `ValidationFailed` to a verified
persistent success.

Two test defects were repaired without weakening coverage:

- shared-project references are resolved to exact repository top-level layers,
  avoiding a false match on the project name `GraniteEdgeAI.GgufRuntime.Contracts`;
- the accepting-core architecture proof now verifies delegation to the bound
  plan's `MatchesExecutionPayload` authority, which replaced the stale direct
  issuer-call assertion.

## Managed evidence

Commands were run with SDK 10.0.301 and `-p:UseAppHost=false`.

| Suite | Discovered | Passed | Failed | Skipped | Result |
|---|---:|---:|---:|---:|---|
| OpenVINO contracts | 204 | 204 | 0 | 0 | Pass |
| OpenVINO route unit | 405 | 400 | 0 | 5 | Native-stage blocked |
| OpenVINO WorkerClient | 13 | 13 | 0 | 0 | Pass |
| OpenVINO worker-process integration | 69 | 52 | 2 | 15 | Native-stage blocked |
| **Total** | **691** | **669** | **2** | **20** | **Blocked on native evidence** |

Exact commands:

```powershell
dotnet test tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj -c Release -p:UseAppHost=false
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false
dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj -c Release -p:UseAppHost=false
dotnet test tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj -c Release -p:UseAppHost=false
```

Focused regression evidence:

- OpenVINO optimization filter: 135 discovered, 135 passed.
- accepting-core architecture plus both formerly timing-out protocol tests:
  3 discovered, 3 passed.

The two integration failures are explicit missing-stage failures from
`TurboQuantWorkerTests`; both require `OPENVINO_TURBOQUANT_WORKER_STAGE`.
The 20 skips require the sealed converter stage, official worker stage, or an
authorized physical Intel GPU. A native-stage skip is recorded as a blocker,
not a pass.

## Native evidence matrix

| Required OpenVINO evidence | Status | Evidence / blocker |
|---|---|---|
| C0 native lock | BLOCKED | Exact token `UCL-NATIVE-LOCK: O1` not received |
| Converter stage and manifest SHA-256 | BLOCKED | Controlled stage variables not supplied |
| Official worker stage and manifest SHA-256 | BLOCKED | Controlled stage variables not supplied |
| TurboQuant worker stage and manifest SHA-256 | BLOCKED | Controlled stage variables not supplied |
| Controlled model/package fixture | BLOCKED | Controlled fixture variables not supplied |
| Picker and supported drag/drop | NOT RUN | Native lock prohibits application launch |
| Model Inspection | NOT RUN | Native lock prohibits worker launch |
| Verified Hardware Inspection / non-zero usable memory | NOT RUN | Native lock prohibits native journey |
| Compatibility and direct-fit/required-conversion decision | NOT RUN | Native lock prohibits native journey |
| Canonical six preference labels/order | MANAGED ONLY | Route-managed assertions pass; UI journey not run |
| Ordered seven-stage optimisation / one spinner | NOT RUN | Native lock prohibits application and converter launch |
| Result reinspection and source/output immutability | MANAGED ONLY | Managed route regressions pass; native result unavailable |
| Chat stepper, Enter, and Shift+Enter behavior | NOT RUN | Shared UI; native lock prohibits application launch |
| Verified export outcome | NOT RUN | No authorized native output exists |
| Same-size screenshots | NOT CAPTURED | Native lock prohibits application launch |
| Process cleanup | PASS | O1 launched no application, worker, converter, runtime, or registration process |

No stage identity or manifest digest can be truthfully reported until C0 supplies
the controlled stages and lock. No private path, native output, or host identity
has been recorded here.

## Shared correction requested from C0

The shared public V2 issuer still requires an `OptimizationSelection`, but the
only public resolver now rejects every proof-less frozen V2 candidate and the
selection constructor is internal. O1 tests require a test-only reflective
fixture to exercise the still-supported V2 route. C0 should decide centrally
whether to provide a supported legacy-selection test seam, migrate the frozen
O1 contract, or remove the public V2 issuer after all consumers migrate. O1 did
not change shared ModelHardwareCompatibility code.

## Nonclaims and continuation

This handoff does not claim native OpenVINO validation, application registration,
UI parity, TurboQuant activation, export, screenshots, or whole-application
completion. After C0 sends the exact lock token and controlled stage identities,
O1 must verify the sealed closures, run the complete OpenVINO product journey,
fill the native rows above with evidence, clean up only processes O1 launched,
and return `UCL-NATIVE-LOCK: IDLE`.
