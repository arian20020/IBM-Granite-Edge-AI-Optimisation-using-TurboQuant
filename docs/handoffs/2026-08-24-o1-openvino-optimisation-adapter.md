# O1 OpenVINO optimisation adapter handoff

Date: 2026-08-24

Status: `DONE_WITH_CONCERNS`

Branch: `feature/openvino-optimisation-adapter-v1`

## Immutable branch identities

- OpenVINO base: `c7c7ae34210caa3d0af03643ea1fa4966bb8b296`.
- Authoritative frozen C1 contract: `f999443279ae3505f7df1c686f98c5163c190acd` from `origin/feature/cross-route-optimisation-contracts-v1`.
- C1 merge / O1 implementation base: `706d3cc4ca32364703eb2cea599a1557f8536e42`.
- Merge parents, in order: `c7c7ae34210caa3d0af03643ea1fa4966bb8b296` and `f999443279ae3505f7df1c686f98c5163c190acd`.
- O1 implementation tip before this handoff document: `b9767606c7553a5d71d1ee3d4d60ccb9f0aee1c7`.
- C1 is an ancestor of the implementation tip. The remote C1 ref resolved to the exact expected SHA during final verification.

The C1 merge's only manually resolved shared-file conflict was the explicitly authorised `MainWindow.xaml.cs` resolution. It retains C1's conditional initial fixture-gallery/onboarding navigation and registers `AppWindow.Closing += AppWindow_Closing;` after the `#endif`, so the complete existing OpenVINO shutdown path runs in both builds.

## Integration seams

- Service composition factory: internal `ModelInspectionServiceComposition.CreateDefaultOpenVinoOptimizationService(OpenVinoRouteService)`. On Windows x64 it resolves the approved converter closure and creates `OpenVinoOptimizationService` with `SealedOpenVinoOptimizationPipeline`.
- Capability input: public `OpenVinoOptimizationToolVersions`, `OpenVinoOptimizationCapabilityAdmission`, and `OpenVinoOptimizationCapabilityEvidence`.
- Capability output: `OpenVinoOptimizationCapabilityProjector.Project(evidence)` returns frozen C1 `OpenVinoCapabilityPayload`. It reports evidence and does not select a preference or objective.
- Exact adapter: `OpenVinoOptimizationPlanAdapter.Adapt(OptimizationExecutionPlan, OptimizationCapabilitySnapshot, string currentSourceSha256, ulong currentSourceLengthBytes)` returns `OpenVinoOptimizationAdaptation` with `Ready` plus one exact candidate or `ReplanRequired` plus a typed C1 `OptimizationSupportCode`.
- Current-state input: `OpenVinoOptimizationCurrentState` independently supplies the current capability snapshot, model-inspection run/handoff IDs, product-hardware run ID, and hardware snapshot SHA-256.
- Execution request: public `OpenVinoOptimizationRequest(SourceDirectory, DestinationDirectory, Plan, CurrentState, Confirmed)`.
- Accepting execution result: `OpenVinoOptimizationService.ExecuteAsync(...)` returns frozen C1 `OptimizationExecutionResult`. Persistent plans return `SucceededPersistent`; Original returns `SucceededRuntimeProfile`; drift returns `ReplanRequired`; cancellation/failure results contain bounded support codes and no path or raw worker output.
- Route-level compatibility seam: `OptimizeAsync(...)` returns `OpenVinoOptimizationResult`. The fixed registry is available only through the explicitly named `OpenVinoOptimizationLegacyRequestV1` / `OptimizeLegacyV1Async` migration seam.

No optimisation UI or navigation wiring was added by O1. The owning coordinator/UI worker must supply live capability evidence and current model/hardware journey state when it wires the public request seam.

## Released capability envelope

The projector releases exactly five official CPU admissions:

| Evidence ID | Weights | KV cache |
|---|---|---|
| `OV-STD-CPU-ORIGINAL-01` | Original | route default |
| `OV-STD-CPU-FP16-01` | FP16 | route default |
| `OV-STD-CPU-AUTO-01` | INT8 | route default |
| `OV-STD-CPU-INT8-U8-01` | INT8 | U8 |
| `OV-STD-CPU-INT4-U8-01` | INT4 | U8 |

Every released admission is CPU-only, latency hint, one stream, exactly 4096 context tokens, compiled cache disabled, `DeclaredSupported`, and does not require experimental evidence. The exact bound tool versions are OpenVINO `2026.3.0`, OpenVINO GenAI `2026.3.0.0`, NNCF `3.3.0`, Optimum `2.3.0`, Optimum Intel `2.1.0`, and Transformers `5.5.4`.

The projector makes no GPU/NPU, enabled compiled-cache, alternate stream/context, F16/BF16/U4 KV-cache, or experimental claim.

### TurboQuant nonclaim

TBQ4 and TBQ3 are not optimisation capabilities in this adapter. Frozen C1 models TBQ4 weights as a persistent conversion, while the existing OpenVINO TurboQuant prompting route only applies runtime TBQ4 KV cache and produces no persistent TBQ4 weight package. O1 has no exact converter/artifact/provenance executor for that persistent plan. Even complete prompting-route activation evidence therefore publishes no TBQ optimisation admission; a counterfactual experimental TBQ4 plan fails closed with `ReplanRequired/ToolNotAdmitted`. Official OpenVINO admissions remain available for C1 replanning.

## Revalidation and transaction boundaries

The plan-bound path validates the frozen route, configuration identity, exact evidence entry, full capability snapshot identity/hash/payload, source SHA-256/length, model inspection run/handoff/model identity, and hardware run/snapshot identity:

1. during initial preflight;
2. immediately before transaction or runtime-profile staging; and
3. immediately before atomic publication.

Configuration identity is recomputed through the frozen C1 issuer rather than by copying C1's internal canonicalizer. Capability/source/model/hardware drift returns a typed replan result before publication; no replacement candidate is selected.

Persistent execution preserves the existing order: retained source snapshot and inspection, operation-owned staging, conversion, output validation, runtime smoke, schema-v2 provenance write, final revalidation, atomic publish, reinspection, rollback if reinspection fails, terminal progress, and operation-owned cleanup. Successful persistent results bind a validated output identity, output manifest SHA-256, and non-zero output size.

Original is runtime-only. It performs no conversion, persistent-model validation, smoke, reinspection, or model-package publication. It writes one schema-v2 bound runtime profile through the hardened `ConversionTransaction` staging/atomic-move path, then returns `SucceededRuntimeProfile` with the profile identity and size.

## Durable binding and legacy isolation

Schema-v2 persistent provenance and schema-v2 runtime profiles bind:

- C1 contract version, route, workload/constraints, candidate contexts, preference, shared-band fact, and plan creation time;
- optimisation plan ID and C1 configuration SHA-256;
- capability snapshot ID and SHA-256;
- model inspection run/handoff IDs, model SHA-256, and model length;
- product hardware run ID and hardware snapshot SHA-256;
- source manifest, exact runtime technical configuration, generated configuration ID, an O1 execution-configuration SHA-256, and the full plan-binding SHA-256;
- for persistent output, validation/smoke dispositions, exact optimizer versions, output files, and output manifest SHA-256.

Reads recompute the O1 execution and plan-binding digests and fail closed on tampering. Schema-v2 validation does not consult a fixed registry. Schema-v1 provenance retains fixed-ID validation only for the explicitly versioned legacy migration path.

Production optimisation search found `GetRequired(` once, in `OpenVinoOptimizationLegacyRegistryV1.GetRequired(OpenVinoOptimizationObjective)`. All `OpenVinoOptimizationObjective` references are confined to that legacy type/registry and `LegacyObjectiveV1`; the accepting request and `ExecuteAsync` path carry a frozen C1 plan and never call the lookup. Focused adapter reflection/behavior tests require the single strict source-bound adapter entry point and exact no-objective mapping; legacy tests call only `OptimizeLegacyV1Async`.

## Fresh verification evidence

All commands ran from `C:\O1` using portable Git and portable .NET SDK `10.0.301`. Release test apphosts were disabled with `UseAppHost=false` after Windows Application Control rejected the unsigned generated OpenVINO contract-test `.exe`; the unchanged managed test assembly then ran through the portable `dotnet` host.

| Verification | Result |
|---|---|
| C1 full contract/unit project, Release x64 | 659 total, 659 passed, 0 failed, 0 skipped |
| OpenVINO contract project, Release x64 | 204 total, 204 passed, 0 failed, 0 skipped |
| OpenVINO component project, Release x64 | 368 total, 363 passed, 0 failed, 5 skipped |
| OpenVINO worker-client project, Release x64 | 13 total, 13 passed, 0 failed, 0 skipped |
| Full worker-process integration project, Release x64 | 63 total, 47 passed, 2 failed, 14 skipped |
| Converter isolation filter | 5 total, 4 passed, 0 failed, 1 skipped |
| Protocol containment filter | 34 total, 34 passed, 0 failed, 0 skipped |
| Stable-route acceptance filter | 12 total, 1 passed, 0 failed, 11 skipped |
| Optimisation end-to-end filter | 2 total, 0 passed, 0 failed, 2 skipped |
| Worker-process integration project build, Release x64 | succeeded, 0 warnings, 0 errors |
| Checked-in OpenVINO GenAI fixture PowerShell verifier | `fixture_valid`, exit 0 |
| Debug x64 app restore phase | exit 0 |
| Debug x64 app build phase | exit 0, 0 errors, one `NETSDK1198` missing `win-x64.pubxml` warning |

The five component skips require `GRANITE_OPENVINO_OFFICIAL_WORKER_STAGE`. Converter isolation and both optimisation E2Es require `GRANITE_OPENVINO_CONVERTER_STAGE`. The stable-route filter's native cases require the converter stage and two independently built official stages; its one static case passed. The full integration project's only failures are the two real TurboQuant worker tests, both throwing the explicit environmental requirement `OPENVINO_TURBOQUANT_WORKER_STAGE is required`; its 14 skips are likewise native-stage/GPU gated. The independent protocol-containment filter passed all 34 cases.

The 204-contract suite includes the PowerShell closure, dependency-lock, workflow, packaging, manifest, privacy, cleanup, fixture, protocol, and architecture source/behavior contracts. Direct manifest/activation/cleanup/release scripts were not run against invented inputs.

The established two-phase Debug x64 compile used:

```powershell
dotnet msbuild 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' /t:Restore /m /nologo /v:minimal /p:Configuration=Debug /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:OpenVinoOfficialWorkerPackagingRequired=false /p:GenerateAppxPackageOnBuild=false
dotnet msbuild 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj' /t:Build /m /nologo /v:minimal /p:Configuration=Debug /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:OpenVinoOfficialWorkerPackagingRequired=false /p:GenerateAppxPackageOnBuild=false
```

This is the user-authorised packaging-disabled compile check. It does not replace final official-worker packaging verification.

## Remaining native and packaging requirements

Final activation still requires verified, mutually consistent inputs rather than a VM or fabricated local stage:

- an official OpenVINO worker stage and manifest SHA-256, including two independent official closures where the stable acceptance campaign requires A/B builds;
- a verified converter stage for converter isolation and persistent FP16/INT8/INT4 end-to-end execution;
- a verified TurboQuant worker stage and its campaign/activation evidence for the separate prompting route (not TBQ optimisation admission);
- exact model identity/length, hosted and UCL evidence roots/files, operation root, evidence commit, and external security/license records required by `Invoke-OpenVinoReleaseGate.ps1`;
- final Release x64 packaging with the verified official-worker stage/digest, followed by privacy, cleanup, traceability, and the ordered release gate.

At verification time all relevant stage variables were unset and no official/converter/TurboQuant stage manifest existed under `C:\O1`. Native packaging and activation are therefore unverified, not passed.

## Boundary audit and repository state

`706d3cc4..b9767606` changes only the approved O1 optimisation production files, their named OpenVINO component/integration tests and narrow test-project C1 references, plus the approved plan. It contains no optimisation XAML/UI, navigation, `MainWindow`, GGUF, shared C1 contract, shared worker protocol, app project/solution, UO1, or I0 change. The earlier authorised merge resolution is outside that implementation range.

Before this handoff write, the branch was clean; there were no unmerged paths or exact Git conflict markers, `git diff --check` exited 0, and no owned OpenVINO worker/converter/process-fixture process remained. Task 5 made no production-code change, push, target-branch merge, PR, or external publication. The C1 import merge described above is the only merge in this O1 branch history.
