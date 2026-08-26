# Cross-route optimisation import ledger

This ledger freezes the authorities and verification rules for bounded component imports. The JSON beside this file is the machine authority; this document is its review narrative.

| Component | Pinned authority | Status |
|---|---|---|
| UO1 | `origin/feature/cross-route-optimisation-ui-v1@8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8` | Pending |
| GGUF runtime | `origin/feature/gguf-cli-chat-production@bacb3f4106e0191b05b870358342f8158765396d` | Pending |
| OpenVINO route | `origin/feature/openvino-optimisation-adapter-v1@f0189ed187ba900f27bade5fde282ae4e99e8d7b` | Pending |

The planning base is `092589c38981ad86bb73c7c97dff01ab8b5a6c8e`; historical contracts are pinned at `e254385997392601102b16acf19244437803bdcc`; the separately built llama.cpp quantiser source is pinned at `3f7c29d318e317b63f54c558bc69803963d7d88c`.

No component is currently authorized for import. A component becomes authorized only after its JSON record is changed to `Verified` and contains complete source provenance, destination classifications, dependency closure, excluded shared paths, integration patch groups, and verification commands. Exact files retain commit:path:blob byte identity. Adapted files are reconstructed from a single declared authority base through a hash-pinned tracked patch. Created files have no invented source blob and must be proven absent from the declared integration base and created from `/dev/null` by their hash-pinned patch.

## Ruled dependency closures and exclusions

These lists and the JSON verification-command arrays mirror the machine authority exactly. They are populated while the components remain Pending so a later import cannot widen ownership or substitute a command while supplying evidence.

### UO1

Closure:

- `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/**`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/**`

Excluded:

- `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml`
- `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationSelectionLayoutTests.cs`

The only ruled patch path is `docs/handoffs/import-patches/uo1-v3-adaptation.patch`.

Verification commands:

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GraniteEdgeAI.UnitTests.Features.ModelOptimization'`

Import commands, in order:

- `$reviewPathspec = Join-Path $env:TEMP 'uo1-import-pathspec.bin'`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component UO1 -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component UO1 -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified`

### GGUF runtime

Closure:

- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/**`
- `shared/GraniteEdgeAI.GgufRuntime.Contracts/**`
- `shared/GraniteEdgeAI.GgufRuntime.Transport/**`
- `runtime/GraniteEdgeAI.GgufRuntime.Capabilities/**`
- `runtime/GraniteEdgeAI.GgufRuntime.NativeAdapter/**`
- `infrastructure/GraniteEdgeAI.GgufRuntime.WorkerClient/**`
- `workers/GraniteEdgeAI.GgufRuntime.Worker/**`
- `IBM Granite with TurboQuant (Intel)/GgufRuntime.WorkerPackaging.targets`
- `scripts/gguf-runtime/**`
- `scripts/Run-ChatPreview.ps1`
- `third-party/licenses/LICENSE.LLamaSharp.txt`
- `third-party/licenses/LICENSE.llama.cpp.txt`
- `tests/TestFixtures/Generate-GgufFixtures.ps1`
- `tests/TestFixtures/Generate-GgufHeaderFixtures.ps1`
- `tests/TestFixtures/Generate-GgufMetadataFixtures.ps1`
- `tests/ContractTests/GraniteEdgeAI.GgufRuntime.Contracts.Tests/**`
- `tests/IntegrationTests/GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Capabilities.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.GgufRuntime.NativeAdapter.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Transport.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.GgufRuntime.Worker.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.GgufRuntime.WorkerClient.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/**`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufFixtureIntegrityTests.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/QuickScan/GgufQuickScannerTests.cs`
- `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- `IBM Granite with TurboQuant (Intel).slnx`

Excluded:

- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/**`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/**`
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/**`
- `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/**`
- `IBM Granite with TurboQuant (Intel)/Features/Onboarding/**`
- `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`
- `IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufInspectedModelLaunchFactory.cs`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/GgufRuntime/GgufInspectedModelLaunchFactoryTests.cs`

The only ruled patch path is `docs/handoffs/import-patches/gguf-runtime-integration.patch`.

Verification commands:

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\gguf-runtime\Invoke-GgufChatVerification.ps1`
- `dotnet test --project $core --configuration Release`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GgufRuntime'`

Import commands, in order:

- `$reviewPathspec = Join-Path $env:TEMP 'gguf-runtime-import-pathspec.bin'`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component GgufRuntime -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component GgufRuntime -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified`

### OpenVINO route

Closure:

- `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/**`
- `shared/GraniteEdgeAI.OpenVino.Contracts/**`
- `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**`
- `IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptRouteContract.cs`
- `IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptRouteRegistry.cs`
- `IBM Granite with TurboQuant (Intel)/Features/Prompting/PromptSessionPresenter.cs`
- `workers/OpenVinoConverter.Worker/**`
- `workers/OpenVinoOfficial.Worker/**`
- `workers/OpenVinoTurboQuant.Worker/**`
- `third-party/openvino-converter/**`
- `third-party/openvino-official/**`
- `third-party/openvino-turboquant/**`
- `scripts/openvino/**`
- `tests/TestFixtures/OpenVINO/**`
- `IBM Granite with TurboQuant (Intel)/OpenVino.WorkerPackaging.targets`
- `IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets`
- `IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ArchitectureBoundaryTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/DependencyLockContractTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/FixtureContractTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GpuDeviceContractTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolJsonTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolSequenceTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/SupportCodeTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/Task7ProtocolExtensionTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj`
- `tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/**`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/**`
- `IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/**`
- `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`
- `IBM Granite with TurboQuant (Intel).slnx`
- `tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj`
- `scripts/gguf-runtime/Invoke-GgufChatVerification.ps1`

Excluded:

- `IBM Granite with TurboQuant (Intel)/Features/ModelImport/**`
- `IBM Granite with TurboQuant (Intel)/Features/ModelInspection/**`
- `IBM Granite with TurboQuant (Intel)/Features/HardwareInspection/**`
- `IBM Granite with TurboQuant (Intel)/Features/ModelHardwareCompatibility/**`
- `IBM Granite with TurboQuant (Intel)/Features/Onboarding/**`
- `IBM Granite with TurboQuant (Intel)/MainWindow.xaml.cs`
- `tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoV2TestPayload.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/WorkflowContractTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/TurboQuantSourceContractTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/HandoffBundleContractTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/PackagingContractTests.cs`
- `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/packages.lock.json`

The ruled patch paths are `docs/handoffs/import-patches/openvino-v3-adaptation.patch`, `docs/handoffs/import-patches/openvino-shared-integration.patch`, and `docs/handoffs/import-patches/gguf-verifier-openvino-integration.patch`. The shared-integration patch may use only the exact destination HEAD and its ruled project, solution, packaging, executor, and UnitTests-project paths. Its other exact integration-owned creation members are `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackagedToolContextResolver.cs`, `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoProductionComposition.cs`, `tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoProductionCompositionTests.cs`, and `tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj`; no sibling path is implied. The pinned O1 `packages.lock.json` is excluded rather than imported. The GGUF-verifier patch may use only a destination first-parent Task-11 predecessor and only `scripts/gguf-runtime/Invoke-GgufChatVerification.ps1`. No other OpenVINO destination may use mutable integration ancestry.

Verification commands:

- `dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false`
- `dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj -c Release -p:UseAppHost=false`
- `dotnet test tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj -c Release -p:UseAppHost=false --filter 'FullyQualifiedName~OptimizationEndToEndTests'`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OpenVinoProductionCompositionTests'`

Import commands, in order:

- `$reviewPathspec = Join-Path $env:TEMP 'geai-task13-pathspec.bin'`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component OpenVino -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component OpenVino -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified`

A component import commit must change only its manifest-approved destination paths, declared integration patch paths, and these two manifest files. Before staging or publishing a review artifact, the verifier rejects repository-local `core.fsmonitor`, `core.hooksPath`, and `core.untrackedCache`.

The canonical staging rule is `verified no-filter blob hashing and literal update-index cacheinfo`. Production staging must use `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verification\Test-CrossRouteImportManifest.ps1 -Component <UO1|GgufRuntime|OpenVino> -SourceRepository <source> -DestinationRepository <destination> -StageVerified`; the verifier hashes each verified regular file with trusted Git `hash-object -w --no-filters -- <literal-path>`, binds that object to the verified SHA-256 bytes, writes its exact mode, object, and canonical path with literal `update-index --add --cacheinfo`, then performs strict staged-state verification. `-WriteAllowedPathspec` creates only a locked review artifact and must not be passed to `git add`.
