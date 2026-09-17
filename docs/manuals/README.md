# Manuals

## Beginner route

Start with [Run the app](Run-the-App.md) for the examiner's overview and [Granite-Start-Here.md](Granite-Start-Here.md) for every copy-and-paste setup command. These instructions use **Granite-Edge-AI-Setup.zip**, version **1.0.4.0**. No source build is needed.

| Starter-guide step | Action |
| --- | --- |
| 1–2 | Click Download in the browser, then verify and extract the app ZIP. |
| 3 | Check files only; **Nothing installed** means the check passed. |
| 4A–4B | Open administrator PowerShell, then install in that new window using your own account. |
| 5 | Launch in ordinary PowerShell and wait for the Granite window. |
| 6–7 | Download and prepare both models, then import a prepared model in Granite. |

Normal Downloads is `C:\Users\<your username>\Downloads`. The guide prepares files in `C:\Downloads`. Do not confuse the two locations or mix these steps with historical demonstration commands. If a command fails, stop before running the next step.

New to the project? Start with [Granite installation and model setup](Granite-Start-Here.md). It covers the packaged app, launch and model preparation without building the source. Then follow the user guide. [Run the app](Run-the-App.md) also retains the historical demonstration-computer details.

For a fresh checkout, read [Set up from source](Fresh-Computer-Setup.md). It maps the native inputs to the checked-in scripts and states which preparation steps are still missing.

1. [Run the app](Run-the-App.md): launch the prepared installation and check its identity.
2. [Download a model](Download-a-Model.md): built-in downloads, manual GGUF checks and the separate OpenVINO route.
3. [Build and installation](Build-and-Installation.md): packages, prerequisites and the remaining release-input gap.
4. [User manual](User-Manual.md): the normal model-to-chat workflow, offline use and local data.
5. [Known limitations and troubleshooting](Known-Limitations.md): support boundaries and recovery.
6. [Developer guide](Developer-Manual.md): code layout, tests and safe changes.
7. [Licence status](Licence-Status.md): original work, dependency notices and model terms.

These guides do not certify an untested installer or guarantee every model. See [release evidence](../../release-evidence/README.md) for the handover status.

See [manual verification status](Verification-Status.md) for the checks performed and the remaining limits.
