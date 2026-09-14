# Set up Granite from source

This guide is for a new Windows development computer. It does not use the existing demonstration installation. Read the release-input section before spending time on a build: not all native inputs have a complete public preparation recipe yet.

## 1. Check the computer

Use Windows 11 x64 on suitable Intel hardware. Other laptops are not automatically supported. The recorded test computer had about 16 GB of RAM; this is not a guaranteed minimum. Allow extra disk space for downloads, native builds, models and temporary output.

Keep internet access on for tools, dependencies and model downloads. Offline chat needs those files first.

## 2. Install the tools

Install Git and the Visual Studio components listed in the [build guide](Build-and-Installation.md). That guide records the observed tool versions and distinguishes the .NET SDK from the runtimes needed to execute managed programs. Follow the repository's `global.json` and project lock files; do not replace pinned versions with arbitrary newer ones.

Open **Developer PowerShell for Visual Studio** from Start. This sets up the compiler tools for the commands below. Run:

```powershell
git --version
dotnet --info
msbuild -version
```

If a command is not found, finish installing or configuring that tool before continuing.

## 3. Clone the source

Use a short path. If the destination already exists, choose another folder rather than overwriting it.

```powershell
git clone https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant.git C:/src/granite
Set-Location C:/src/granite
git rev-parse HEAD
git status --short
```

Save the commit printed by the third command with your build notes. Run later repository commands from this folder. Cloning downloads code, not an installed application.

## 4. Prepare the runtime inputs

A stage is a folder containing a worker and its required files. The app checks these files against a manifest and an expected hash. A newly generated hash alone does not prove that a package is one of the supported configurations.

| Input | Exact source or preparation entry point | Important limit |
| --- | --- | --- |
| GGUF runtime | [Bundled package record](../../runtime/gguf/atomicbot/package-manifest.json) and its `archives` folder. The application build invokes [Stage-AtomicBotRuntime.ps1](../../scripts/gguf-runtime/Stage-AtomicBotRuntime.ps1). | The record fixes the source revision and both archive hashes. Do not substitute another upstream release. |
| GGUF quantizer | [Stage-AtomicBotQuantizer.ps1](../../scripts/gguf-quantization/Stage-AtomicBotQuantizer.ps1) accepts the bundled CPU archive and its pinned hash. | This is a separate stage from the runtime. |
| Official OpenVINO | Archive URLs, filenames, sizes and hashes are in [runtime](../../third-party/openvino-official/openvino-runtime.lock.json), [GenAI](../../third-party/openvino-official/openvino-genai.lock.json) and [tokenizers](../../third-party/openvino-official/openvino-tokenizers.lock.json) locks. Use [Build-OpenVinoOfficialWorker.ps1](../../scripts/openvino/Build-OpenVinoOfficialWorker.ps1). | Download the exact archives, not the current latest versions. |
| OpenVINO converter | [Python lock](../../third-party/openvino-converter/python-runtime.lock.json), [wheel manifest](../../third-party/openvino-converter/wheel-manifest.json), [requirements](../../third-party/openvino-converter/requirements.lock) and [converter builder](../../scripts/openvino/Build-OpenVinoConverterWorker.ps1). | The builder requires an existing `ClosureDirectory`: the complete matching Python/dependency set. These records are not yet a verified end-to-end preparation guide. |
| OpenVINO TurboQuant | [Source lock](../../third-party/openvino-turboquant/upstream.lock.json), [runtime builder](../../scripts/openvino/Build-OpenVinoTurboQuantRuntime.ps1) and [worker builder](../../scripts/openvino/Build-OpenVinoTurboQuantWorker.ps1). | The runtime builder already requires `VerifiedRuntimeDirectory`, as well as exact source and distribution inputs. It is not a bootstrap from a plain clone. |
| Model inspection and hardware probe | The application packaging targets build these components. See the [packaging input list](Build-and-Installation.md#building-a-runnable-package). | Keep their verification checks enabled. |

For example, the bundled quantizer can be staged with this command. Use a new, empty destination outside the checkout:

```powershell
powershell.exe -NoProfile -File scripts/gguf-quantization/Stage-AtomicBotQuantizer.ps1 -ArchivePath runtime/gguf/atomicbot/archives/llama-turboquant-windows-x64-cpu.zip -ExpectedArchiveSha256 21b751142163cfb15738cb5084abcb83444ce76453bd4aa33e9faeca1966ad3c -StageDirectory C:/granite-build/quantizer
if ($LASTEXITCODE -ne 0) { throw 'Quantizer staging failed. Stop here.' }
```

For the official OpenVINO worker, first obtain the archives named by the three locks and place them in `C:/granite-inputs/official`. Then use fresh build and stage directories:

```powershell
powershell.exe -NoProfile -File scripts/openvino/Build-OpenVinoOfficialWorker.ps1 -OfficialArchiveDirectory C:/granite-inputs/official -BuildDirectory C:/granite-build/official-build -StageDirectory C:/granite-build/official-stage
if ($LASTEXITCODE -ne 0) { throw 'Official worker build failed. Stop here.' }
```

These commands show the checked-in script interfaces. They have not been executed as a fresh-computer setup during this documentation review. The scripts enforce additional tool and input checks. Do not reuse the folders of a working installation as build destinations.

**Remaining input gap:** the converter closure and verified TurboQuant runtime still need a reproducible preparation procedure or a distributable, verified input bundle. Their exact identities and notices must be retained. Without them, this guide cannot promise the full application will run. Do not bypass the gates to hide this gap.

## 5. Restore and build

Follow the [restore and compile commands](Build-and-Installation.md#compile-only-checks). A successful compile-only check verifies compilation, not the presence of all runtime files.

For a runnable build, supply the stage directories and matching manifest hashes named in the [packaging table](Build-and-Installation.md#building-a-runnable-package). The complete values and signing/package recipe must be recorded together in the [package handover](../../release-evidence/Package-Handover.md). That handover is currently incomplete; there is no verified universal package command to paste here.

## 6. Install and open the completed package

This step applies only once the complete package, its expected hash and its dependency/signing instructions are available. Check the hash against the supplied record. Follow that package's installation instructions; do not disable Windows security or trust an unexplained certificate. Do not copy a loose EXE or use the demonstration computer's registration command as an installer.

Open Granite from Start. A visible opening page alone does not prove both runtime routes work. Check GGUF model inspection, OpenVINO model inspection and hardware inspection before calling the installation ready.

## 7. Get a model and use the app

Follow [Download a model](Download-a-Model.md), then the [user manual](User-Manual.md). Choose an offered download preference or import a local model. Let the app check it; a successful download does not guarantee memory fit. Use the available configuration, export and chat actions only after the required checks finish.

For failures, keep the diagnostic code and follow [troubleshooting](Known-Limitations.md). Never fix an integrity error by editing the expected hash or mixing runtime files from different builds.

## What has been verified

The repository contains the linked scripts and input records. The command examples have been syntax-checked, not verified as a complete fresh installation. There has been no clean-computer installation test. This remains a partially documented source setup, not a finished self-service installer. See [verification status](Verification-Status.md) for the existing checks.
