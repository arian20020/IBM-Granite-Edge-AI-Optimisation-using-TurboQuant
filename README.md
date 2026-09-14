# Granite: local models on Windows

Granite is a WinUI 3 research application for running selected IBM Granite models on a Windows 11 Intel computer. It brings model import, inspection, hardware-fit estimates, configuration, optimisation, export and chat into one guided workflow.

The project also studies memory, speed and quality across selected llama.cpp and OpenVINO routes. **Those experiments are separate from application testing.** TurboVec was evaluated as a separate demonstration and is not integrated into the app.

## Start here

**New computer?** Read [Set up from source](docs/manuals/Fresh-Computer-Setup.md) first. It explains tools, cloning, runtime inputs, building and the remaining installation gap. **Already installed?** Use the demonstration launch instructions below. Downloading a model does not install the application.

## Main features

- Download one of the offered IBM Granite GGUF files, or import a local GGUF file or complete OpenVINO model folder.
- Check the model before checking the computer, with separate progress and recovery screens.
- Estimate memory fit using currently available RAM and a safety reserve.
- Review supported weight/cache configurations, including selected TurboQuant options where admitted by the runtime evidence and resource checks.
- Run optimisation, validate the output and export a model when the selected operation produces a saved model.
- Chat locally once the required model and runtime files are available.

The app is not a general model converter. Supported choices depend on the exact files and runtime; a model being selectable does not mean every configuration will work.

## Setup and first use

### Starting on another computer

Cloning this repository downloads the source code, not a complete installed app.
The current supported target is Windows 11 x64 on selected Intel hardware; the
project does not establish support for every laptop.

1. Install Git and the Windows development tools listed in the [build guide](docs/manuals/Build-and-Installation.md).
2. Open PowerShell and run the following commands. Choose another short folder if `C:/src/granite` already exists.

   ```powershell
   git clone https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant.git C:/src/granite
   Set-Location C:/src/granite
   git rev-parse HEAD
   ```

3. Follow the build guide to restore dependencies and compile. Its hosted-CI command is a compile-only check, not a runnable release package.
4. **Before packaging or launching**, obtain the exact native runtime stages and their matching manifest hashes. The [package handover](release-evidence/Package-Handover.md) lists the missing release details. These files are not all supplied by cloning the repository. Do not disable verification to get past missing inputs.
5. Once a complete package is supplied, follow its installation instructions and test both GGUF and OpenVINO inspection before treating the setup as ready. Then use the [model download guide](docs/manuals/Download-a-Model.md) and [user manual](docs/manuals/User-Manual.md).

**Current limit:** a complete clone-to-launch procedure has not yet been verified
on a fresh computer. Steps 4–5 describe the remaining handover and verification,
not completed installation instructions. The project must supply and test those
details before this can be presented as a self-service installation guide.

The [full setup guide](docs/manuals/Fresh-Computer-Setup.md#4-prepare-the-runtime-inputs) links each runtime's source records and preparation scripts. It also identifies the converter and TurboQuant inputs whose preparation is still incomplete. Do not skip that section and assume a standard build includes them.

### Open the existing demonstration installation

On the prepared project computer, press **Windows + R**, paste the command below and press **Enter**. Close other Granite windows first so you do not mistake an old build for this copy.

```text
explorer.exe shell:AppsFolder\488d3892-c214-40c5-9a6a-1154c1e69fff_gqahnnh6hk88w!App
```

This opens the existing registered installation; it does **not** install the app on another computer. The [run guide for users and examiners](docs/manuals/Run-the-App.md) explains identity checks, what to do if launch fails, and the package needed for a separate PC. An independently tested installer is not yet supplied by this repository.

| You want to… | Open this |
| --- | --- |
| Try the app | [Run the app](docs/manuals/Run-the-App.md) |
| Set up a new computer | [Source setup and runtime inputs](docs/manuals/Fresh-Computer-Setup.md) |
| Obtain a model | [IBM Granite model downloads](docs/manuals/Download-a-Model.md) |
| Learn the workflow | [User manual](docs/manuals/User-Manual.md) |
| Understand a warning or missing choice | [Known limitations and troubleshooting](docs/manuals/Known-Limitations.md) |
| Work on the source | [Developer guide](docs/manuals/Developer-Manual.md) |
| See what actually passed | [Application test evidence](docs/testing/application-verification/README.md) |
| Read the experimental findings | [Final experiment results](docs/testing/final-results/README.md) |
| Check the release handover | [Release record](release-evidence/README.md) |

## What to expect

Choose a local GGUF file or complete OpenVINO folder, or use the recommended download route. The app inspects the model, checks the computer and presents available configurations before execution.

A hardware-fit result is an estimate, not a guarantee. Choices depend on the model, the exact verified runtime and current memory/disk checks. Weight quantisation and KV-cache optimisation are different settings; a lower-memory choice is not automatically faster or better.

The recorded checks used selected models on an Intel Windows computer with about 16 GB of RAM. They do not establish a universal minimum specification. Obtain the complete verified package before trying the app: a compile-only CI build does not include every native dependency.

### Your first model-to-chat run

1. Open the installed app. Select any offered download preference, or choose a local model. The built-in catalogue supplies IBM Granite 4.0 H Micro GGUF models; it does not download OpenVINO packages.
2. Wait for model inspection. Read warnings and use the recovery action shown if it cannot continue.
3. Run hardware inspection and read the fit result. Close memory-heavy programs if needed, then use the available recovery path for a new check.
4. Review the setup and optimise if offered. Wait for output validation before saving or using the result.
5. Open chat and try a harmless prompt, such as “What is the difference between a CPU and RAM?” Check the reply before relying on it.

For an offline demonstration, obtain all files first, disconnect, then start a new prompt. Do not expect model downloads to work offline. Before resetting or uninstalling, close the app and follow the [backup guidance](docs/manuals/User-Manual.md#keep-a-backup-before-repair-or-removal).

### Common setup problems

| Problem | What to do |
| --- | --- |
| Missing worker or integrity error | Check that the runtime stage and its hash belong to the same build. Do not replace individual DLLs or bypass checks. |
| App compiles but inspection fails | A compile-only output may lack required runtime inputs. Review the setup guide before treating it as a release. |
| Model does not fit | Available memory changes. Read the estimate and use the offered recovery or lower-memory choice. |
| Fewer slider choices than expected | Check the selected model, runtime identity, memory and disk limits. Missing choices are not automatically a display bug. |
| Need to report a problem | Include the commit/package, route and diagnostic code. Remove private paths and prompts; follow [support](SUPPORT.md). |

## Build, test and reproduce results

Use the [build guide](docs/manuals/Build-and-Installation.md) for commands and the [developer guide](docs/manuals/Developer-Manual.md) for the code structure. Do not rebuild over your only working installation.

The [test index](tests/README.md) explains the test projects and runners. Real-model tests require their documented inputs. Check the result files for executed, failed and skipped counts: a skipped test is not a pass. [CI scope](docs/testing/CI-Test-Scope.md) records exclusions and deferred issues.

For research results, start with the [experiment reproduction guide](docs/testing/final-results/REPRODUCING.md). Keep those results separate from application tests. A successful standalone runtime experiment does not prove that the application completed the same operation.

## Current evidence and limits

Three automated inspection journeys passed with no failures or skips: GGUF through compatibility, OpenVINO through enabled configuration, and invalid-GGUF recovery. The developer also reported a separate offline inspection, optimisation, export and chat journey.

These checks do not cover every model, slider choice or error case. Clean-machine installation and a formal user study were not confirmed, and some recovery and accessibility issues remain. See the linked evidence and limitations rather than treating a green CI result as full release acceptance.

## Repository layout

- `IBM Granite with TurboQuant (Intel)`: application source.
- `shared`, `infrastructure`, `runtime`, `workers`: rules, contracts and runtime integration.
- `tests`: application tests and fixtures.
- `experiments`: experiment inputs, scripts and results.
- `docs`: guides, design, evidence and project records.
- `report`: report-location information.
- `release-evidence`: release handover and its open checks.

For the project background, use the [scope baseline](docs/planning/Project-Definition-v1.md), [requirements](docs/requirements/Requirements-Traceability-Matrix.md), [traceability index](docs/traceability/README.md) and [documentation index](docs/README.md).

## Responsible use

For help, see [support](SUPPORT.md). Contributors should read [CONTRIBUTING.md](CONTRIBUTING.md). Security concerns have a separate [reporting guide](SECURITY.md). The [licence status](docs/manuals/Licence-Status.md) explains the outstanding owner decision.

This is a research prototype, not a clinical system or a replacement for professional judgement. Do not use real patient or pupil data. Review generated answers before relying on them.

Large models and third-party build outputs are not stored in Git. Preserve upstream licence notices when distributing runtime files. Do not assume a root project licence or redistribution permission that has not been supplied.

Measured results, estimates and published claims are kept separate. The recorded evidence—not the existence of a folder or test name—determines what is verified.
