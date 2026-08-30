# R4.1 T1 cross-feature integration remediation report

## Disposition

**READY FOR C0 REREVIEW.** The corrected T1-owned implementation subject is independently approved and fully exercised on this non-authoritative development machine. This is not an all-green product acceptance: eight unique intentional product-gap contracts are RED (ten failed test cases because one data-driven reserve contract has three failing rows), the unchanged Model Inspection suite has 22 fresh failures, and seven OpenVINO native-stage tests are skipped. No unexpected T1 regression RED was observed.

No production code was changed. No Intel-native, accelerator, packaging, visual, energy, real-model, or performance acceptance is claimed.

## Git identity and custody

| Item | Value |
| --- | --- |
| Frozen historical source commit/tree | `4748fe04f19afdf6b27c4c12502b84db325e7294` / `fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91` |
| R4.1 base commit/tree | `af799e8ba62bd4528568ab9ddcf72d82da762ca1` / `5b6d13707fbceb351ef161f72f220786d463d667` |
| R4.1 implementation subject commit/tree | `6f6d774681a735c67c655598e95cd8b9310cf015` / `3cfeed36bd0cdedf8e3b9274684f4c765a808cee` |
| Branch | `test/ucl-t1-cross-feature-remediation-r4-1` |
| Intended remote ref | `refs/remotes/origin/test/ucl-t1-cross-feature-remediation-r4-1` |

The base identities and required ancestry were verified before work. The implementation subject contains only `tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/**`. This report and the v2 receipt are added in a later handoff commit, which must remain a descendant of the immutable implementation subject.

## Changed paths by purpose

Behavioral tests and deterministic fixtures:

- `CrossFeaturePlanFixture.cs`
- `CrossRouteCompatibilityIntegrationTests.cs`
- `OptimizationReducerIdentityTests.cs`
- `PersistentOutputRecoveryIntegrationTests.cs`
- `PlanAndExecutionIntegrationTests.cs`
- `ProductionCompatibilityBindingIntegrationTests.cs`

Build-boundary containment and traceability:

- `GraniteEdgeAI.CrossFeature.IntegrationTests.csproj`
- `CompileLinkIntegrityTests.cs`
- `COVERAGE-MAP.md`

Owned production-seam proposal:

- `C0-R4.1-CROSS-FEATURE-SEAMS.patch`

The implementation delta is 10 paths, 822 insertions, and 437 deletions. There are no application, XAML, production project, or other worker changes.

## Review-finding resolution

### T1-R41-01: zero available memory

Confirmed. The old test expected a 512 MiB reserve when availability was zero. The corrected independent oracle implements `reserve = min(available, max(floor, available / 10))` with overflow-safe arithmetic and `budget = available - reserve`. It probes the internal production policy through the built application assembly rather than compiling a second copy and separately verifies the engine projection contract.

Coverage includes availability 0, 1 byte, below/exactly/immediately above 512 MiB, the 10% crossover and its next value, representative 8/16/32 GiB-class cases, `ulong.MaxValue`, reserve/budget invariants, installed-memory invariance, and fail-closed absent policy. The proportional reserve oracle is now correct but intentionally RED for three rows because production currently returns the fixed 512 MiB floor without bounding it by availability. The separate zero-budget engine projection is intentionally RED because the production projection throws instead of establishing a zero budget.

### T1-R41-02: source tokens versus behavior

Confirmed. Callable seams were replaced with compiled behavioral observations: the real optimization reducer is invoked with a reflected production plan instance; the public compatibility preflight rejects mismatched handoff authority; the published GGUF lookup is exercised with plan/output/manifest mutations; and controlled lifecycle/file fixtures observe retirement, publication, and staged-output effects.

The download and OpenVINO exact-result requirements cannot be proved through a current callable shared seam. Their tests remain narrowly labelled composition/static REDs and the coverage map states the property proved, nonclaim, owner, and follow-up. `PublishedGgufLookupRejectsNonExactResultAuthority` was added as a behavioral RED because changed output length is currently accepted even though plan, output identity, and manifest mutations are rejected. No static check is presented as native or end-to-end proof.

The five prior RED names map one-to-one without renaming:

| R4 name | R4.1 name | Disposition |
| --- | --- | --- |
| `TerminalSuccessWithSubstitutedJourneyIdentityIsSuppressed` | same | RED: real reducer accepts substituted terminal authority |
| `RetiringUnsealedLeaseLeavesNoStagedOutputOrPublication` | same | RED: staged output remains after retirement |
| `QuantizerChildEnvironmentIsAllowlistedAndDiagnosticsDisabled` | same | RED: child environment is not yet strictly allowlisted |
| `RecommendedModelDownloadActionIsFunctionallyWired` | same | RED: no callable verified-download boundary |
| `OpenVinoChatAndExportSourceCompositionRequiresExactResultAndLifecycleToken` | same | RED: no callable exact-result/lifecycle consumer |

Three additional unique product-gap contracts are intentionally RED: `ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative`, `ZeroAvailableMemoryProjectsAnEstablishedZeroBudgetWithoutThrowing`, and `PublishedGgufLookupRejectsNonExactResultAuthority`.

### T1-R41-03: compile-linked duplication

Confirmed and contained. The broad `shared/GraniteEdgeAI.GgufRuntime.Contracts/**/*.cs` and `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/**/*.cs` source links were removed and replaced with real project references. The project now references the real GGUF runtime contracts and compatibility core assemblies, while retaining the existing OpenVINO contracts project reference.

Every remaining wildcard was replaced by an exact file link. Exactly 60 temporary application/worker links remain; none is a wildcard, none is duplicated, and `CompileLinkIntegrityTests` fails if the set changes. Each exact link has a nine-field custody row in `COVERAGE-MAP.md`: requirement, test, actual compiled subject, layer, expected/current disposition, owner, follow-up, and limitation. They remain necessary because the WinUI application feature families are not independently referenceable by this test host, and the worker environment policy is not public from its owning assembly. These links prove only the linked managed behavior under the T1 build; they do not prove shipping-assembly or native equivalence.

The apply-checkable proposal `C0-R4.1-CROSS-FEATURE-SEAMS.patch` creates a warning-clean `GraniteEdgeAI.CrossFeature.Contracts` project with a compiled five-band catalog, requested-versus-observed download evidence, lifecycle-aware verified download/submission interfaces, and exact optimization-result Chat/export authority. It also gives C0 exact integration and migration steps. `git apply --check` passed. A transient apply plus Release build passed with 0 warnings and 0 errors, after which the patch was reverse-applied; the proposal is not represented as integrated production.

### T1-R41-04: Model Inspection 24 versus 22

The durable `c2db11d` report records 357 total, 333 passed, and 24 failed. Its exact historical failure inventory was recovered from `docs/audits/2026-08-28/T1-cross-feature-integration-tests-r2.md`:

1. `BuildWorkflowExecutesEveryGate2LayerWithoutRetainingRawResults`
2. `ControlledEvidenceWorkflowIsManualPreflightOnlyAndFailClosed`
3. `ControlledEvidencePreflightRejectsCampaignReferenceClassPinAndExecutionMutations`
4. `Task12WorkflowGuideAndEvidenceUseAsciiPunctuation`
5. `CleanupSourceListIsSortedUniqueAndContainsOnlyExistingFiles`
6. `CurrentCleanupScopeIsFullyInventoried`
7. `Projects_DeclareExactFailClosedDebugX64FixtureOwnership`
8. `ProjectBoundaryValidator_RejectsInMemoryMutation ("debug-only-condition")`
9. `ProjectBoundaryValidator_RejectsInMemoryMutation ("missing-default-remove")`
10. `ProjectBoundaryValidator_RejectsInMemoryMutation ("wildcard-include")`
11. `ProjectBoundaryValidator_RejectsInMemoryMutation ("update-instead-of-include")`
12. `ProjectBoundaryValidator_RejectsInMemoryMutation ("none-update-leakage")`
13. `ProjectBoundaryValidator_RejectsInMemoryMutation ("replacement-json")`
14. `ProjectBoundaryValidator_RejectsInMemoryMutation ("alternate-slash")`
15. `ProjectBoundaryValidator_RejectsInMemoryMutation ("alternate-case")`
16. `ProjectBoundaryValidator_RejectsInMemoryMutation ("property-indirected-condition")`
17. `ProjectBoundaryValidator_RejectsInMemoryMutation ("property-indirected-item")`
18. `ProjectBoundaryValidator_RejectsInMemoryMutation ("metadata-indirected-item")`
19. `ProjectBoundaryValidator_RejectsInMemoryMutation ("extra-fixture-item")`
20. `EvaluatedProjects_ExposeExactClosureOnlyForDebugX64WithMatchingRid`
21. `ModelInspectionFixtureGalleryBuildBoundary_EvaluatesOnboardingOnlyForDebugX64`
22. `DurableBoardAndBlockedReferences_AreExcludedFromAppAndTestPackages`
23. `VisualArtifactPrivacyScanner_AcceptsOnlySafeManifestAndApprovedPngMetadata`
24. `VisualArtifactPrivacyScanner_RejectsPathsIdentityModelMetadataPngTextAndTrx`

Fresh detached execution of the same suite at `c2db11d`, at the pinned `af799e8` base, and at the R4.1 subject each discovered 357 and produced the identical 22-name failure set:

1. `BuildWorkflowExecutesEveryGate2LayerWithoutRetainingRawResults`
2. `CleanupSourceListIsSortedUniqueAndContainsOnlyExistingFiles`
3. `ControlledEvidencePreflightRejectsCampaignReferenceClassPinAndExecutionMutations`
4. `ControlledEvidenceWorkflowIsManualPreflightOnlyAndFailClosed`
5. `CurrentCleanupScopeIsFullyInventoried`
6. `DurableBoardAndBlockedReferences_AreExcludedFromAppAndTestPackages`
7. `EvaluatedProjects_ExposeExactClosureOnlyForDebugX64WithMatchingRid`
8. `ModelInspectionFixtureGalleryBuildBoundary_EvaluatesOnboardingOnlyForDebugX64`
9. `ProjectBoundaryValidator_RejectsInMemoryMutation ("alternate-case")`
10. `ProjectBoundaryValidator_RejectsInMemoryMutation ("alternate-slash")`
11. `ProjectBoundaryValidator_RejectsInMemoryMutation ("debug-only-condition")`
12. `ProjectBoundaryValidator_RejectsInMemoryMutation ("extra-fixture-item")`
13. `ProjectBoundaryValidator_RejectsInMemoryMutation ("metadata-indirected-item")`
14. `ProjectBoundaryValidator_RejectsInMemoryMutation ("missing-default-remove")`
15. `ProjectBoundaryValidator_RejectsInMemoryMutation ("none-update-leakage")`
16. `ProjectBoundaryValidator_RejectsInMemoryMutation ("property-indirected-condition")`
17. `ProjectBoundaryValidator_RejectsInMemoryMutation ("property-indirected-item")`
18. `ProjectBoundaryValidator_RejectsInMemoryMutation ("replacement-json")`
19. `ProjectBoundaryValidator_RejectsInMemoryMutation ("update-instead-of-include")`
20. `ProjectBoundaryValidator_RejectsInMemoryMutation ("wildcard-include")`
21. `Projects_DeclareExactFailClosedDebugX64FixtureOwnership`
22. `Task12WorkflowGuideAndEvidenceUseAsciiPunctuation`

Set comparison: 22 still fail; the two visual privacy scanner tests are newly passing in every fresh run; zero are newly failing, renamed, undiscovered, blocked, or skipped. There are no Model Inspection test/script changes between `c2db11d` and `af799e8`, and the same two tests pass when the old commit is rerun now. The historical report grouped these failures with local PowerShell execution policy, and the scanners invoke `powershell.exe`; the old bound TRX/stderr is unavailable for a more specific attribution. Therefore the two-test delta is classified as an environment/policy difference, not a proven production or test correction. The historical 24-failure fact is preserved separately; only the fresh 22-failure observations are called fresh observations, not inherited corrections.

## Verification ledger

TRX paths are repository-relative under ignored `TestResults/`; files are not committed. `Selected` includes skipped outcomes. Durations come from TRX start/finish timestamps.

| Suite | Selected | Passed | Failed | Skipped | Duration | TRX | Bytes | SHA-256 |
| --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- |
| T1 baseline at `af799` | 99 | 94 | 5 | 0 | 3.602s | `R41-T1-baseline-crossfeature/baseline.trx` | 156914 | `8f23488ebdda330b14c47a63808aaa39e488795bd52e2c94fc090fe23ed3b7eb` |
| T1 corrected full | 111 | 101 | 10 | 0 | 2.968s | `R41-T1-final-crossfeature/R41-T1-crossfeature.trx` | 180264 | `ad3c8f7b807f0e7c5f0d61a132e3abea504438f73a1cff7e58c6713c8bc48180` |
| T1 memory focus | 14 | 10 | 4 | 0 | 0.525s | `R41-T1-final-memory/R41-T1-memory.trx` | 27910 | `ffb68476cd0221aef7b22ff7d468c8a77c31a077e5d0034dbaf5302f19fd420a` |
| T1 download/exact focus | 8 | 5 | 3 | 0 | 2.217s | `R41-T1-final-download-exact/R41-T1-download-exact.trx` | 16265 | `6e94afcd22acf5cff07b62fbf11e5fab50b1c8fcfde7f9f54a7b5c1625007889` |
| T1 compile boundary | 3 | 3 | 0 | 0 | 0.580s | `R41-T1-final-compile-boundary/R41-T1-compile-boundary.trx` | 5514 | `f985860a0d977ab5d119776e3220c8d5e8759270023773e051df0b89c4fcba7a` |
| Compatibility | 1050 | 1050 | 0 | 0 | 3.529s | `R41-T1-final-compat/compat.trx` | 1472979 | `54192f2de4854c76f4e5315f041a637040a1f1e1fb5ccd165b81decca070ef41` |
| Model Inspection at fresh `c2` | 357 | 335 | 22 | 0 | 105.290s | `R41-T1-model-inspection-c2/c2.trx` | 709395 | `c849e1fa049b0ca19ef8055cd875e1a2caea69ec80de058de372d0165ab720b2` |
| Model Inspection at `af799` | 357 | 335 | 22 | 0 | 58.470s | `R41-T1-model-inspection-af799/af799.trx` | 705664 | `cf656df0e62d5bc212fb33716f6b23230c1173e57bdbba1a7f89f45afa7ff72a` |
| Model Inspection at subject | 357 | 335 | 22 | 0 | 66.240s | `R41-T1-final-micontracts/micontracts.trx` | 697094 | `a5f06bb064e094286abf24f5f3467e4e9b8151dd30e5a3b20a402e7cc5bc288c` |
| OpenVINO contracts | 205 | 205 | 0 | 0 | 166.552s | `R41-T1-final-ovcontracts/ovcontracts.trx` | 295225 | `03ebe4df1310551d69411d27d3cf01149ac6e0fbaaea0a384a13e1b412f19cfa` |
| OpenVINO WorkerClient | 13 | 13 | 0 | 0 | 2.703s | `R41-T1-final-ovclient/ovclient.trx` | 19718 | `a25f8d5cf19e350294106a38c36a40fa230c496286de8026b2aac6f3d48bfdd1` |
| OpenVINO unit | 409 | 402 | 0 | 7 | 42.514s | `R41-T1-final-ovtests/ovtests.trx` | 606929 | `1735ae1b2f1dfc7d92d8c784e6a30668ab6838c004245a6c017be872e0643c08` |
| GGUF quant contracts | 7 | 7 | 0 | 0 | 1.278s | `R41-T1-final-gqcontracts/gqcontracts.trx` | 10888 | `aca5a9b41a6e0be49a224e9e052750638d9fe70719b53c8857c9b1a466630e67` |
| GGUF quant WorkerClient | 8 | 8 | 0 | 0 | 3.361s | `R41-T1-final-gqclient/gqclient.trx` | 12765 | `712b8f1ceaa0b3ee72aac49771f77167cd08f77edf5d7281640f20dbdc3c0819` |
| GGUF runtime contracts | 12 | 12 | 0 | 0 | 1.234s | `R41-T1-final-grcontracts/grcontracts.trx` | 17513 | `045694db26308d4f3309fa1e2bb6ae74573064dff038a1d4ec452111734f3e1c` |
| GGUF runtime WorkerClient | 9 | 9 | 0 | 0 | 1.687s | `R41-T1-final-grclient/grclient.trx` | 14150 | `476ae5d98098e85da34afc481cbbcf471931d1bd9fbd6c0651ddf899be1069aa` |

Receipt aggregate excludes baseline and focused reruns to prevent double counting. The ten authoritative owner/full suites total **2,181 discovered/selected, 2,142 passed, 32 failed, 7 skipped**; `2,142 + 32 + 7 = 2,181`. Raw TRX `executed` excludes the seven declared skips, while receipt `executed` follows the selected-outcome convention. The 32 failures are exactly 10 T1 intentional cases plus 22 fresh Model Inspection failures.

## Commands and command validity

The T1 project was built Release with 0 warnings/errors. Valid discovery was:

```powershell
dotnet test tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests.csproj -c Release --no-build --list-tests
```

It discovered 111 tests. An earlier `-- --list-tests` attempt was invalid for this runner and executed zero; it is explicitly rejected as evidence and was replaced by the command above. The exact full and focused invocations were:

```powershell
dotnet test tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=R41-T1-crossfeature.trx" --results-directory TestResults/R41-T1-final-crossfeature
dotnet test tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests.csproj -c Release --no-build --filter "Name~ProportionalReserveIsBoundedByAvailabilityAndBudgetNeverNegative|Name=ZeroAvailableMemoryProjectsAnEstablishedZeroBudgetWithoutThrowing|Name=InstalledMemoryAloneCannotChangeAnAvailableMemoryReserve|Name=MachineMemoryRejectsIncoherentBudgets" --logger "trx;LogFileName=R41-T1-memory.trx" --results-directory TestResults/R41-T1-final-memory
dotnet test tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests.csproj -c Release --no-build --filter "Name=DownloadedSourceFolderPublishesConversionIntentWithoutPath|Name=OpenVinoChatAndExportSourceCompositionRequiresExactResultAndLifecycleToken|Name~PickerAndExplorerDropUseTheSamePathPrivatePipeline|Name=PublishedGgufIdentityIsReusedByChatAndExportAfterRestart|Name=PublishedGgufLookupRejectsNonExactResultAuthority|Name=RecommendedModelDownloadActionIsFunctionallyWired|Name=RuntimeOnlyOpenVinoSuccessCannotMasqueradeAsDownloadableModel" --logger "trx;LogFileName=R41-T1-download-exact.trx" --results-directory TestResults/R41-T1-final-download-exact
dotnet test tests/IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests/GraniteEdgeAI.CrossFeature.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~GraniteEdgeAI.CrossFeature.IntegrationTests.CompileLinkIntegrityTests" --logger "trx;LogFileName=R41-T1-compile-boundary.trx" --results-directory TestResults/R41-T1-final-compile-boundary
```

Compatibility, Model Inspection, OpenVINO, and GGUF owner projects were restored and built Release, then executed using the supported Microsoft.Testing.Platform path:

```powershell
dotnet vstest tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/bin/Release/net8.0/GraniteEdgeAI.ModelHardwareCompatibility.Tests.dll --Logger:"trx;LogFileName=compat.trx" --ResultsDirectory:TestResults/R41-T1-final-compat
dotnet vstest tests/ContractTests/GraniteEdgeAI.ModelInspection.Contracts.Tests/bin/Release/net8.0/GraniteEdgeAI.ModelInspection.Contracts.Tests.dll --Logger:"trx;LogFileName=micontracts.trx" --ResultsDirectory:TestResults/R41-T1-final-micontracts
dotnet vstest tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/bin/Release/net8.0/GraniteEdgeAI.OpenVino.Contracts.Tests.dll --Logger:"trx;LogFileName=ovcontracts.trx" --ResultsDirectory:TestResults/R41-T1-final-ovcontracts
dotnet vstest tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/bin/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.OpenVino.WorkerClient.Tests.dll --Logger:"trx;LogFileName=ovclient.trx" --ResultsDirectory:TestResults/R41-T1-final-ovclient
dotnet vstest tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/bin/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.OpenVino.Tests.dll --Logger:"trx;LogFileName=ovtests.trx" --ResultsDirectory:TestResults/R41-T1-final-ovtests
dotnet vstest tests/ContractTests/GraniteEdgeAI.GgufQuantization.Contracts.Tests/bin/Release/net8.0/GraniteEdgeAI.GgufQuantization.Contracts.Tests.dll --Logger:"trx;LogFileName=gqcontracts.trx" --ResultsDirectory:TestResults/R41-T1-final-gqcontracts
dotnet vstest tests/UnitTests/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests/bin/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.GgufQuantization.WorkerClient.Tests.dll --Logger:"trx;LogFileName=gqclient.trx" --ResultsDirectory:TestResults/R41-T1-final-gqclient
dotnet vstest tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/bin/Release/net8.0/GraniteEdgeAI.GgufRuntime.Contracts.Tests.dll --Logger:"trx;LogFileName=grcontracts.trx" --ResultsDirectory:TestResults/R41-T1-final-grcontracts
dotnet vstest tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/bin/Release/net8.0-windows10.0.19041.0/win-x64/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests.dll --Logger:"trx;LogFileName=grclient.trx" --ResultsDirectory:TestResults/R41-T1-final-grclient
```

Using `dotnet test` for the compatibility project was a rejected preliminary invocation because the repository's Microsoft.Testing.Platform configuration does not accept the VSTest project path; the supported DLL invocation produced 1050/1050.

Both managed app builds used the exact app project, `Platform=x64`, `RuntimeIdentifier=win-x64`, `--no-restore`, and package generation disabled; only unavailable producer requirements were explicitly disabled for the managed build:

```powershell
dotnet build "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" -c Debug --no-restore -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:GenerateAppxPackageOnBuild=false -p:AppxBundle=Never -p:OpenVinoOfficialWorkerPackagingRequired=false -p:OpenVinoConverterPackagingRequired=false -p:OpenVinoTurboQuantPackagingRequired=false -p:GgufQuantizerPackagingRequired=false -p:HardwareInspectionLlamaCppProbeSkipPackaging=true
dotnet build "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" -c Release --no-restore -m:1 -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:GenerateAppxPackageOnBuild=false -p:AppxBundle=Never -p:OpenVinoOfficialWorkerPackagingRequired=false -p:OpenVinoConverterPackagingRequired=false -p:OpenVinoTurboQuantPackagingRequired=false -p:GgufQuantizerPackagingRequired=false -p:HardwareInspectionLlamaCppProbeSkipPackaging=true
```

Debug passed with 0 warnings/errors; Release passed with 0 warnings/errors in 49.68s. A preliminary Debug design-time build was invalid for acceptance and failed `WMC1006` because design-time mode skipped referenced dependency builds; the normal managed build above supersedes it.

The strict package gate did not disable producer requirements:

```powershell
dotnet build "IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj" -c Release --no-restore -m:1 -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never
```

It failed closed after 58.65s with 0 warnings and one error: `GgufQuantizerStageDirectory is required when GGUF quantizer packaging is enabled.` This is an environment/native producer block, not a T1 regression and not a package pass.

Additional final checks: `git diff --check af799e8..6f6d774` exited 0; 64 declared test method names had zero duplicates; 60 explicit compile items had zero duplicates and zero wildcards; the changed-diff scan found zero actual user-home/worktree paths, private-key markers, passwords, API keys, bearer credentials, or token assignments. TRX counters, byte sizes, and SHA-256 values were independently re-read from the final files.

## Application launch and native evidence

The exact Debug and Release executable paths were respectively:

- `IBM Granite with TurboQuant (Intel)/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/IBM Granite with TurboQuant (Intel).exe`
- `IBM Granite with TurboQuant (Intel)/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/IBM Granite with TurboQuant (Intel).exe`

Each was 152576 bytes with SHA-256 `05cb9376f4d6010a3f2d3ce0102bfe5a9d24b4ff1ce901f5a7393947b526f430`. Bounded hidden `Start-Process` launches of both exact files exited before creating a window with `System.TypeInitializationException`; the inner COM exception was `0x80040154 (REGDB_E_CLASSNOTREG)` while activating the Windows App Runtime deployment class. Stderr is retained under ignored `TestResults/R41-T1-final-launch/`.

No journey, visual, scaling, keyboard, control-wiring, screenshot, package-install, or native execution evidence could truthfully be collected. No screenshot was fabricated. E1/C0 must provision/register the matching Windows App Runtime and all native producer stages, build/install the package, then rerun the exact journey and native matrix on the authoritative Intel machine.

## Independent review

An independent reviewer initially identified two Important issues: the reducer test had been changed into a public-preflight test instead of preserving the original reducer requirement, and the proposed catalog did not distinguish requested authority from observed evidence. Corrections restored the original reducer RED while keeping a separate public preflight GREEN, and split `PinnedModelArtifact` from `DownloadedArtifactEvidence` with exact equality checks. The reviewer also requested an apply/build proof for the C0 project and a warning correction (`Dictionary` concrete type); both were completed.

Final independent rereview approved the subject with no Critical or Important findings. It explicitly verified reserve/projection separation, independent memory arithmetic, adversarial real-reducer RED plus public preflight, requested/observed download authority, honest C0 integration and 60-link migration, nine-field coverage/custody, exact 24-to-22 reconciliation evidence, patch apply-check/build, privacy, and preservation of the five original product gaps.

## C0 integration action and nonclaims

C0 should apply/review `C0-R4.1-CROSS-FEATURE-SEAMS.patch`, integrate the shared catalog and exact requested/observed download boundary, implement lifecycle-aware verified publication/submission and exact-result Chat/export consumers, then migrate the 60 exact links into referenceable owner assemblies. C0/Q1/S1/F1 owners must close the eight unique RED requirements and rerun T1 against integrated production. E1 must perform package, launch, visual, real-model, Intel-native, accelerator, cancellation/process, and performance acceptance.

This report claims managed test implementation and evidence only. It does not claim that compile-linked sources equal the shipping assembly, that source composition proves reachability, that a managed fake proves OpenVINO/GGUF/TurboQuant/native execution, that the package builds or installs, or that the integrated product is complete.
