# Q1 R4 optimisation result binding

## Decision

Q1 returns a production correction with managed validation and an external native block. The immutable validated candidate base is commit `218ad08fb4ebad7b39d90fd2f906c4f234d66495`, tree `78c96061690b8ede285b83712219ba05af34a5dc`. The implementation subject is commit `8769ec80b2474cb5d638d1ad0c4b4ffdbe2ac38d`, tree `b068ca7f35b7b0b79fff294b8d59ed77df7acdbd`.

## Correction

- Added one fixed two-route `OptimizationDestinationFacade` accepting only the exact `OptimizationExecutionResult`.
- Added typed GGUF and OpenVINO Chat targets and typed export dispositions carrying route, plan, execution, configuration, output identity, manifest SHA-256, and length.
- Removed the disabled `LastPublishedDirectory` compatibility seam.
- Preserved `TryGetPublishedOutput`, `CreateChatTargetAsync`, and `ExportPersistentAsync` on the OpenVINO executor.
- Persistent GGUF Chat now reopens, rehashes, and retains a read handle that denies write/delete until target disposal. Runtime-only GGUF and OpenVINO targets retain their source leases.
- Facade cancellation is checked before and after target resolution; a late-cancelled target is disposed. Provider-substituted Chat/export identities fail closed.
- GGUF temporary-export cleanup failure is observable and sanitized; OpenVINO destination rejection and cleanup failure remain distinct typed dispositions.

## Exact C0 integration sequence

1. After issuing the selected plan, construct `GgufOptimizationDestinationRoute(plan, outputRegistry, sourceCustody)` and `OpenVinoOptimizationDestinationRoute(openVinoExecutor)`, then construct one `OptimizationDestinationFacade` from those two exact routes. Retire all three with the optimization journey; never register them globally.
2. Optional optimization and required optimization use the existing coordinator and exact issued plan. Direct Chat continues through the current-model handoff, not this facade.
3. Optimized Chat calls `await facade.CreateChatTargetAsync(state.Result, lifecycleToken)`. Match the typed target. For `OpenVinoDestinationChatTarget`, pass `Target` to `ActivateOpenVinoChatTargetAsync`. For `GgufOptimizationChatTarget`, build the launch request only from `VerifiedModelPath`, `VerifiedModelSha256`, and `RuntimeOptions`. Keep the target undisposed until activation/controller ownership has ended.
4. Persistent export obtains an absent picker destination and calls `await facade.ExportPersistentAsync(state.Result, destination, maximumBytes, lifecycleToken)`. Branch on every typed disposition; success is accepted only when all returned identities equal `state.Result`.
5. Runtime-only export receives `RuntimeOnly` before any route export begins and must keep export disabled.
6. On retry, back, replacement, navigation, shutdown, or completion, cancel the lifecycle token, await in-flight calls, dispose any target, retire the coordinator/executors/facade, and finally retire source custody.

## Managed verification

| Gate | Discovered | Executed | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| Q1 identity/recovery and destination facade | 47 | 47 | 47 | 0 | 0 |
| OpenVINO optimization | 55 | 55 | 55 | 0 | 0 |
| OpenVINO contracts | 202 | 202 | 202 | 0 | 0 |
| OpenVINO worker client | 17 | 17 | 17 | 0 | 0 |
| GGUF quantization contracts | 7 | 7 | 7 | 0 | 0 |
| GGUF quantization worker client | 19 | 19 | 19 | 0 | 0 |
| Cross-feature integration | 114 | 114 | 114 | 0 | 0 |
| **Non-overlapping total** | **461** | **461** | **461** | **0** | **0** |

The cross-feature run used a temporary uncommitted `global.json` SDK selection of installed SDK 10.0.400 because child `dotnet msbuild` probes cannot inherit the wrapper for absent pinned SDK 10.0.301. The file was restored byte-for-byte before commit. The Debug x64 UnitTests/application build passed with zero errors and 13 inherited warnings using the H1 SDK shim on `PATH`.

TDD recorded two behavioral RED failures for runtime-only export and late cancellation, then GREEN. A deliberate mutation allowing write/delete sharing on the retained GGUF handle made the 47-test Q1 suite fail; restoring `FileShare.Read` returned 47/47.

Independent adversarial review initially found late-cancellation lease retention, export receipt substitution, and persistent GGUF path TOCTOU. All were corrected; re-review found no remaining Critical or Important security/code issue. Requirements review withdrew production-wiring as Q1-forbidden C0 ownership and identified adapter identity, cleanup observability, and production-provider coverage; these were corrected, including a real GGUF route/lease test.

## Native, package, and visual disposition

The exact Q1 native authorization token/lock was not published. Per the entry gate, Q1 did not launch the app, package, quantizer, converter, worker, real model, Chat/export journey, screenshots, or performance execution. H1 also records the packaged WinUI runner handshake as blocked. These rows are blocked, not passed; no output-format/load/export native identity, screenshot, responsiveness, Intel performance, or release claim is made.

## Ownership and overlaps

Owned changes are limited to six Q1 paths: the destination facade, OpenVINO adapter, OpenVINO compatibility-seam removal, GGUF exporter cleanup, GGUF output custody, and focused Q1 tests. No XAML, shell, navigation, project, solution, package, M1 schema, M1 inspection behavior, H1 hardware behavior, T1 registration, or `main` change exists.

- H1 overlap: exact model/hardware binding is consumed and the display-only trust boundary is unchanged.
- M1 overlap: existing OpenVINO activation target is wrapped, not redefined; no schema or inspection projection changes.
- T1 overlap: focused Q1 tests were added without changing cross-feature project composition.
- C0 overlap: shell construction/calls remain for C0 and must follow the exact sequence above; no conflict was silently resolved.

## Intake rule

C0 must verify remote equality, ancestry from the validated base, the six-path owned diff, implementation subject/tree, report/manifest/receipt hashes and bytes, 461-test arithmetic, and the explicit native blockers. C0 may integrate the production correction only together with the typed facade call sequence; it must not restore `LastPublishedDirectory`, pre-copy an ambient path, or start E1 before accepting Q1 and freezing a new post-Q1 candidate.
