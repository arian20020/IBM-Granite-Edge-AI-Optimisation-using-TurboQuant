# Granite: local models on Windows

Granite is a WinUI 3 research application for running selected IBM Granite models on a Windows 11 Intel computer. It brings model import, inspection, hardware-fit estimates, configuration, optimisation, export and chat into one guided workflow.

The project also studies memory, speed and quality across selected llama.cpp and OpenVINO routes. **Those experiments are separate from application testing.** TurboVec was evaluated as a separate demonstration and is not integrated into the app.

## Start here

**To run the app:** follow [Packaged app installation and model setup](#setup-and-first-use) below. No Git, Visual Studio or source build is needed. **To develop the app:** use [Set up from source](docs/manuals/Fresh-Computer-Setup.md); its runtime-input requirements still apply.

## Main features

- Download one of the offered IBM Granite GGUF files, or import a local GGUF file or complete OpenVINO model folder.
- Check the model before checking the computer, with separate progress and recovery screens.
- Estimate memory fit using currently available RAM and a safety reserve.
- Review supported weight/cache configurations, including selected TurboQuant options where admitted by the runtime evidence and resource checks.
- Run optimisation, validate the output and export a model when the selected operation produces a saved model.
- Chat locally once the required model and runtime files are available.

The app is not a general model converter. Supported choices depend on the exact files and runtime; a model being selectable does not mean every configuration will work.

## Setup and first use

Follow these steps to install **Granite-Edge-AI-Setup.zip** and download the models.
You do not need Git, Visual Studio or the source repository to run the app.

Use a Windows 11 x64 laptop with at least 20 GB of free storage. More space
may be needed for optimisation.
This is a self-signed test build; installation on a separate laptop and both
model journeys have not yet been verified. Do not install over an existing
Granite installation or bypass your organisation's security policies.

Run each numbered step separately. If you see an error, stop and send the
complete error message to me. Do not continue to the next step.

### 1. Download the application ZIP

Open ordinary **Windows PowerShell** and paste this command to open the
OneDrive download page in your browser. When the page opens, click **Download**
to save the application ZIP:

```powershell
Start-Process 'https://liveuclac-my.sharepoint.com/:u:/g/personal/ucab280_ucl_ac_uk/IQA3url8UXf3S5dWadOp1J_mAcnF6HngK5vyJivCi0lQr40?e=Sxcl88'
```

Click **Download** and save the file as **Granite-Edge-AI-Setup.zip**
in your normal **Downloads** folder (`C:\Users\<your username>\Downloads`).
Wait until the download finishes before continuing to Step 2.
Keep its exact filename. The next command extracts it into
**C:\Downloads\Granite-Edge-AI-Setup**, so no manual moving or extraction is needed.

The ZIP is about 599 MB and does not include the model downloads.

### 2. Check and extract it

Open **Windows PowerShell** from Start normally, not as administrator.
Copy and paste this entire block, then press Enter:

```powershell
$ErrorActionPreference = 'Stop'
$graniteZip = Join-Path $env:USERPROFILE 'Downloads\Granite-Edge-AI-Setup.zip'
$graniteFolder = 'C:\Downloads\Granite-Edge-AI-Setup'

if (-not (Test-Path -LiteralPath $graniteZip)) {
    throw 'The ZIP was not found. Check its filename and Downloads location.'
}
if ((Get-FileHash -LiteralPath $graniteZip -Algorithm SHA256).Hash -ne
    'B8119FE614377D1141F70AC89C7E94D184933C796F91A83197E08AC00E8579E3') {
    throw 'The ZIP checksum does not match. Stop and contact Arian.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $graniteFolder) {
    $existingFolder = Get-Item -LiteralPath $graniteFolder
    if (-not $existingFolder.PSIsContainer -or
        ($existingFolder.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The destination is not a regular folder. Nothing was changed.'
    }
    $existingItems = @(Get-ChildItem -LiteralPath $graniteFolder -Force -Recurse)
    if ($existingItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw 'The existing folder contains linked files or folders. Nothing was changed.'
    }
    $archive = [IO.Compression.ZipFile]::OpenRead($graniteZip)
    try {
        $files = @($archive.Entries | Where-Object { $_.Name -ne '' })
        if (@($existingItems | Where-Object { -not $_.PSIsContainer }).Count -ne $files.Count) {
            throw 'The existing folder has missing or extra files. Nothing was changed; contact me.'
        }
        foreach ($entry in $files) {
            $localFile = Join-Path $graniteFolder $entry.FullName
            if (-not (Test-Path -LiteralPath $localFile -PathType Leaf)) {
                throw "Missing file: $($entry.FullName). Nothing was changed."
            }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try {
                $expected = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
            } finally {
                $stream.Dispose()
                $sha.Dispose()
            }
            if ((Get-FileHash -LiteralPath $localFile -Algorithm SHA256).Hash -ne $expected) {
                throw "File differs: $($entry.FullName). Nothing was changed; contact me."
            }
        }
    } finally {
        $archive.Dispose()
    }
    'Already extracted and verified - continue to the next step.'
} else {
    New-Item -ItemType Directory -Path 'C:\Downloads' -Force | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($graniteZip, $graniteFolder)
    'ZIP verified and extracted successfully - continue to the next step.'
}
```

Wait for either success message. An existing folder is reused only if all its
files match the verified ZIP; no existing files are overwritten.
Do not run commands from inside the ZIP.
If Windows denies permission to create or write to `C:\Downloads`, stop and
ask for help; administrator approval may be needed. If your browser saves
downloads elsewhere, change only `$graniteZip` to the downloaded ZIP's full path.

### 3. Check the application files

In the same ordinary PowerShell window, paste:

```powershell
Set-Location ('C:\Downloads\Granite-Edge-AI-Setup')
Unblock-File -LiteralPath .\Setup-Granite.ps1
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Setup-Granite.ps1 -Step Check
```

Expected message: **Bundle hashes and Microsoft dependency signatures passed.
Nothing installed.** This checks files without installing them.

The script is unblocked only after the ZIP check. RemoteSigned applies to
the child PowerShell process, not permanently to Windows. If a managed-device
policy prevents execution, stop and ask your IT team; do not bypass it.

### 4. Install Granite or check the existing installation

Close the ordinary PowerShell window. Find **Windows PowerShell** in Start,
right-click it and select **Run as administrator**. Approve the Windows prompt.
Use your own Windows account; if Windows requires another person's account,
stop and ask for help.

Paste:

```powershell
$ErrorActionPreference = 'Stop'
Set-Location 'C:\Downloads\Granite-Edge-AI-Setup'
$installed = Get-AppxPackage -Name '488d3892-c214-40c5-9a6a-1154c1e69fff'

if ($installed) {
    if ($installed.Status -ne 'Ok' -or
        [string]::IsNullOrWhiteSpace($installed.InstallLocation)) {
        throw 'The existing registration needs attention. Nothing was changed.'
    }
    'Granite is already installed - continue to Step 5.'
} else {
    powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Setup-Granite.ps1 -Step Install
    if ($LASTEXITCODE -ne 0) { throw 'Installation did not finish successfully. Stop here.' }
}
```

If Windows reports that Granite is already installed with an OK package status
and an installation location, installation is skipped without changing the app,
certificates or dependencies. This quick check does not scan installed files
or test model inference. The ZIP and bundle checks above remain unchanged.

For a new installation, type **INSTALL** when asked and press Enter. This approves:

- Installing Microsoft .NET 8 x64 if it is missing.
- Trusting the supplied test certificate in Windows Trusted People.
- Installing Granite and its Microsoft Windows App Runtime dependency.

This is a test certificate, not a public production certificate. The supplied
certificate should have this thumbprint:

`94AF865FEBB2530DD2042016ADCA237D0887936D`

If the thumbprint differs, stop and contact me. Otherwise, continue with installation.

It expires on 14 December 2026. No root certificate is added and no private
signing key is supplied. The script refuses to replace an existing Granite
installation. If a restart is requested, restart Windows and repeat this step.
Otherwise wait for the **Installed** or **Granite is already installed**
message, then close administrator PowerShell.

### 5. Open Granite

Open ordinary **Windows PowerShell** again and paste:

```powershell
Set-Location ('C:\Downloads\Granite-Edge-AI-Setup')
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\Setup-Granite.ps1 -Step Launch
```

The model-selection page should appear. No model is needed just to open it.
If the window does not appear, report that before proceeding.

### 6. Download the two models

Download the models through your browser, not through the setup script.

First, open ordinary **Windows PowerShell** and paste this to create
the destination folder silently and open the OneDrive model folder in your browser.
When the page opens, download each model file individually using **Download**;
do not download the whole folder:

```powershell
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path 'C:\Downloads\Granite-Models' -Force | Out-Null
Start-Process 'https://liveuclac-my.sharepoint.com/:f:/g/personal/ucab280_ucl_ac_uk/IgDJtuZvOAVISJoSvMclk0xEAbjwPBGSTtz3a87kS2cPxjo?e=Vm9uMC'
```

Open the [OneDrive model folder](https://liveuclac-my.sharepoint.com/:f:/g/personal/ucab280_ucl_ac_uk/IgDJtuZvOAVISJoSvMclk0xEAbjwPBGSTtz3a87kS2cPxjo?e=Vm9uMC)
and download these two files individually, not the whole folder:

- `granite-4.1-3b-Q4_K_M.gguf`
- `Granite-4.1-3B-OpenVINO-Raw.zip`

Save both in **C:\Downloads\Granite-Models**. If your browser saves them in
your normal Downloads folder, move the completed files into this folder.
Keep their exact filenames, without suffixes such as `(1)`.
If the GGUF file is already there and was verified earlier, do not download it again.

The downloads total about **7.36 GB**. OpenVINO extraction needs about another
**6.82 GB** of free space. Wait until both browser downloads finish.
Do not rename or use an incomplete `.partial` file left by an earlier attempt;
the command below ignores those files.

In ordinary PowerShell, paste this entire block. It checks the downloaded
files and extracts OpenVINO; it does not download anything:

```powershell
$ErrorActionPreference = 'Stop'
$graniteModels = 'C:\Downloads\Granite-Models'
$graniteGguf = Join-Path $graniteModels 'granite-4.1-3b-Q4_K_M.gguf'
$graniteOpenVinoZip = Join-Path $graniteModels 'Granite-4.1-3B-OpenVINO-Raw.zip'
$graniteExtracted = Join-Path $graniteModels 'OpenVINO-Extracted'

$graniteChecks = @(
    @{
        Path = $graniteGguf
        Bytes = 2099501664
        Hash = '662B0626CD58F443BAEA23559B469DF6576A81D349649C59413B36A9FB32EB29'
    },
    @{
        Path = $graniteOpenVinoZip
        Bytes = 5257095946
        Hash = '28DD8AF79F2A2A1CB2EA91E5BE18DAD02CEF5681AF3266160533842C743043C6'
    }
)

foreach ($graniteCheck in $graniteChecks) {
    if (-not (Test-Path -LiteralPath $graniteCheck.Path -PathType Leaf)) {
        throw "File not found: $($graniteCheck.Path). Check its name and location."
    }
    if ((Get-Item -LiteralPath $graniteCheck.Path).Length -ne $graniteCheck.Bytes) {
        throw "File size does not match: $($graniteCheck.Path). Do not use it."
    }
    Write-Host "Verifying $($graniteCheck.Path). This may take a few minutes."
    if ((Get-FileHash -LiteralPath $graniteCheck.Path -Algorithm SHA256).Hash -ne $graniteCheck.Hash) {
        throw "Checksum does not match: $($graniteCheck.Path). Stop and contact me."
    }
    Write-Host "Verified: $($graniteCheck.Path)"
}

if (Test-Path -LiteralPath $graniteExtracted) {
    throw 'OpenVINO-Extracted already exists. Nothing was overwritten. If it was successfully extracted earlier, use that folder in Step 7; otherwise contact me.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
Write-Host 'Extracting OpenVINO. Please wait.'
[IO.Compression.ZipFile]::ExtractToDirectory($graniteOpenVinoZip, $graniteExtracted)
'Both downloads verified and OpenVINO extracted - continue to Step 7.'
```

Wait for the success message before continuing. Verification reads the full
files, so it is not instant. If extraction fails, do not use the incomplete
folder or overwrite it; contact me with the error. If Windows denies write
access or the OneDrive link is unavailable, stop and contact me.

### 7. Select a model in Granite

Try one model at a time:

**GGUF:** choose **Choose model**, select the GGUF option, and open:

`C:\Downloads\Granite-Models\granite-4.1-3b-Q4_K_M.gguf`

**OpenVINO:** choose the OpenVINO option and select this complete folder:

`C:\Downloads\Granite-Models\OpenVINO-Extracted\Granite-4.1-3B-OpenVINO-Raw`

Do not select its ZIP, XML or BIN file individually. Keep all files together.
Follow inspection, hardware fit and the available configuration steps. When
chat becomes available, try: **Explain what a computer processor does in two sentences.**

Available configurations depend on the laptop's free memory and storage.
Opening the app does not prove that either model will run. Report any error
with its full message; do not replace runtime files or bypass verification.

### If you need help

If anything goes wrong, send me the error message and let me know which step
you reached. Your Windows version, processor and RAM would also help me
check the problem.

### Building from source instead

Cloning this repository supplies source code, not the packaged application above. Follow the [source setup guide](docs/manuals/Fresh-Computer-Setup.md) and [build guide](docs/manuals/Build-and-Installation.md) for development tools and exact runtime inputs. Hosted-CI build commands are compile-only checks, not runnable installer recipes. A complete clone-to-launch procedure on a fresh computer remains unverified; do not bypass runtime integrity checks or rebuild over a working installation.

| You want to… | Open this |
| --- | --- |
| Try the app | [Install and open the packaged app](#setup-and-first-use) |
| Set up a new computer | [Packaged app and model setup](#setup-and-first-use) |
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
