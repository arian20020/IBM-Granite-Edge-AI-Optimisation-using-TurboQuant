# Granite: local models on Windows

Granite is a WinUI 3 research application for running selected IBM Granite models on a Windows 11 Intel computer. It brings model import, inspection, hardware-fit estimates, configuration, optimisation, export and chat into one guided workflow.

The project also studies memory, speed and quality across selected llama.cpp and OpenVINO routes. **Those experiments are separate from application testing.** TurboVec was evaluated as a separate demonstration and is not integrated into the app.

## Start here

### Run the app

On the prepared project computer, press **Windows + R**, paste the command below and press **Enter**. Close other Granite windows first so you do not mistake an old build for this copy.

```text
explorer.exe shell:AppsFolder\488d3892-c214-40c5-9a6a-1154c1e69fff_gqahnnh6hk88w!App
```

This opens the existing registered installation; it does **not** install the app on another computer. The [run guide for users and examiners](docs/manuals/Run-the-App.md) explains identity checks, what to do if launch fails, and the package needed for a separate PC. An independently tested installer is not yet supplied by this repository.

| You want to… | Open this |
| --- | --- |
| Try the app | [Run the app](docs/manuals/Run-the-App.md) |
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
