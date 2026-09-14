# Build and installation

There are two different tasks here: **using a verified app package** and **building the source**. A successful compilation alone does not supply every native runtime needed to inspect and optimise models.

## If you only want to use the app

If you are using the prepared project computer, start with [Run the app](Run-the-App.md) for its exact registered launch command. The steps below concern obtaining a package, not launching that existing copy.

1. Obtain the complete tested package and its checksum from the project owner. The repository's [release record](../../release-evidence/README.md) does not yet establish a public installer download.
2. Check the package identity and checksum before installing it. Do not use a loose executable copied from a different build.
3. Follow the instructions supplied with that package. For a signed MSIX, use Windows App Installer. Trust a development certificate only after checking its origin; do not disable Windows security to get past an unexplained warning.
4. Launch the registered app from Start and follow the [user manual](User-Manual.md).
5. Check GGUF and OpenVINO separately if you need both routes. Reaching the first page does not prove the runtime packages work.

A clean-machine install/uninstall test is not recorded. This is a limitation, not a reason to label an untested installer as verified.

## Developer prerequisites

The current project targets .NET 8 for Windows. The repository selects .NET SDK **10.0.301** in [global.json](../../global.json), with latest-patch roll-forward. The SDK builds code; the .NET Windows Desktop runtime runs the relevant managed tools. These are different dependencies.

Use Windows x64, Git, Visual Studio with MSBuild and the Windows/.NET desktop and C++ build components needed by WinUI/native workers, and the SDK selected by global.json. The project references Windows App SDK 2.2.0 and Windows SDK BuildTools 10.0.28000.2270. Read the project and lock files before changing versions.

Use a short checkout path such as `C:/src/granite`. Long paths have caused XAML compiler and native runtime problems. Run the commands below in a Visual Studio Developer PowerShell, at the repository root:

```powershell
git status --short
git rev-parse HEAD
dotnet --info
msbuild -version
```

Do not switch branches or rebuild over the only copy of a working package. Keep the tested installation and development output separate.

## Compile-only checks

The exact hosted recipe is in [build-and-test.yml](../../.github/workflows/build-and-test.yml). It deliberately disables selected native packaging requirements because those external inputs are not present on the hosted runner.

To restore the application:

```powershell
$appProject = 'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
msbuild $appProject /target:Restore /property:Configuration=Release /property:Platform=x64 /property:RuntimeIdentifier=win-x64
```

The following is the workflow's compile-only build, not an installer recipe. Run it as one command:

```powershell
msbuild $appProject /target:Build /maxCpuCount /verbosity:minimal /property:Configuration=Release /property:Platform=x64 /property:RuntimeIdentifier=win-x64 /property:PublishProfile= /property:PublishTrimmed=false /property:PublishReadyToRun=false /property:AppxPackageSigningEnabled=false /property:GenerateAppxPackageOnBuild=false /property:GgufQuantizerPackagingRequired=false /property:OpenVinoOfficialWorkerPackagingRequired=false /property:OpenVinoConverterPackagingRequired=false /property:OpenVinoTurboQuantPackagingRequired=false
```

Do not use this output as a tested release or remove packaging checks from the project. The checked-in workflow remains the source of truth if its options change.

## Building a runnable package

The table below is a dependency checklist, not a substitute for the final package recipe. External stage paths and hashes must come from the matching verified build, not from another installation.

| Component | Build input or source of instructions |
| --- | --- |
| Official OpenVINO | `OpenVinoOfficialWorkerStageDirectory` and `OpenVinoOfficialWorkerManifestSha256`. |
| OpenVINO converter | `OpenVinoConverterStageDirectory` and `OpenVinoConverterManifestSha256`. |
| OpenVINO TurboQuant | `OpenVinoTurboQuantWorkerStageDirectory` and `OpenVinoTurboQuantWorkerManifestSha256`. |
| GGUF quantizer | `GgufQuantizerStageDirectory` and `GgufQuantizerManifestSha256`; see [quantizer packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/GgufQuantization.WorkerPackaging.targets). |
| GGUF runtime | [Runtime packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/GgufRuntime.WorkerPackaging.targets) defines the build and manifest checks. |
| Hardware llama.cpp probe | [Probe packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/HardwareInspection.LlamaCppProbePackaging.targets) builds and verifies the probe and locates the Visual C++ runtime. |
| Model inspection worker | [Inspection packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/ModelInspection.WorkerPackaging.targets) publishes and verifies the worker. |

Before calling a package ready, the maintainer must record all of these inputs, the exact Visual Studio components, signing requirements and an installation test in the [handover record](../../release-evidence/Package-Handover.md). No complete, tested installer command is available in this guide yet.

A normal x64 build needs the verified native stages and their expected manifest hashes. A stage is a prepared directory containing a worker, its dependencies and a manifest of expected files.

Start with the checked-in packaging contracts:

- [Official OpenVINO packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/OpenVino.WorkerPackaging.targets)
- [OpenVINO converter packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/OpenVino.ConverterPackaging.targets)
- [OpenVINO TurboQuant packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/OpenVino.TurboQuantPackaging.targets)
- [GGUF runtime packaging](../../IBM%20Granite%20with%20TurboQuant%20%28Intel%29/GgufRuntime.WorkerPackaging.targets)

For example, official OpenVINO requires `OpenVinoOfficialWorkerStageDirectory` and `OpenVinoOfficialWorkerManifestSha256`. Other routes have their own inputs. Do not substitute the official runtime for a custom TurboQuant runtime or reuse a hash from a different stage.

[Build-OpenVinoOfficialWorker.ps1](../../scripts/openvino/Build-OpenVinoOfficialWorker.ps1) accepts an official archive directory, build directory and stage directory. The pinned archive definitions live under `third-party/openvino-official`. Read each build script's parameter block and validation before running it; a bare script invocation is not a complete build recipe.

The final distributable package recipe, external inputs and signing certificate still need to be recorded together in the release record. This guide cannot supply missing private/local inputs. If they are unavailable, stop at compilation and ask the maintainer for the verified release inputs.

Never update an accepted runtime hash merely to silence an error. Matching the package and its evidence is part of the product's behaviour.

## Verify a handover

Record the commit, tool versions, package filename, SHA-256 and actual launch result. Hash an exact file in PowerShell with `Get-FileHash -LiteralPath <package-path> -Algorithm SHA256` (replace the placeholder with its real path).

Test installation separately from compilation. Keep model weights outside Git and obtain them from the pinned catalogue or the documented model source. A fresh machine may need internet access for prerequisites and downloads even though later local inference can work offline.

Do not uninstall or reset an existing working app just to test this guide. Back up needed exports and data first, and perform clean-install testing in a separate suitable environment.
