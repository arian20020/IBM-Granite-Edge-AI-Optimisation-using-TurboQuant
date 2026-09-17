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

The same instructions are available as the [standalone starter guide](docs/manuals/Granite-Start-Here.md).

Use **Granite-Edge-AI-Setup.zip** with this guide. No Git, Visual Studio or source build is needed. Use Windows 11 x64 and allow at least 20 GB of free space; optimisation may need more.

Run each numbered step separately. Copy each complete PowerShell code block using its copy button; do not copy Markdown link formatting, headings or the ``` markers. **If a step fails, stop: do not paste the next command. If setup files are missing or do not match, return to Step 1, download the correct ZIP and repeat Step 2. Do not skip the checks or continue with an older extracted folder.** Other errors, such as low storage or denied permission, need the action shown in the error message.

**What happens when:** Steps 1–2 download and extract the setup files. Step 3 only checks them. Step 4 installs Granite. Step 5 opens it. Step 6 downloads and prepares the models.

**Copy only the commands inside each code box**, one complete box at a time. Do not paste the surrounding explanation or step headings into PowerShell.

This is a self-signed test build. Installation and launch do not guarantee every model journey will work on another laptop. Smart App Control has blocked this version on one tested laptop even though it opened on others. Do not bypass Windows or organisational security policies.

### 1. Download the application ZIP

The command below opens the download page. **Click Download in your browser**, and wait for the file to finish downloading before Step 2.

Open ordinary **Windows PowerShell** and paste:

```powershell
Start-Process 'https://liveuclac-my.sharepoint.com/:u:/g/personal/ucab280_ucl_ac_uk/IQA3url8UXf3S5dWadOp1J_mAcnF6HngK5vyJivCi0lQr40?e=Sxcl88'
```

Save **Granite-Edge-AI-Setup.zip** in your normal Downloads folder (`C:\Users\<your username>\Downloads`). Names such as **Granite-Edge-AI-Setup (1).zip** or **Granite-Edge-AI-Setup (2).zip** are also accepted. Step 2 finds a copy with the correct checksum; you do not need to rename or delete earlier downloads. If your browser saves elsewhere, move the completed ZIP into your normal Downloads folder first.

Step 2 extracts it automatically to **C:\Downloads\Granite-Edge-AI-Setup-1.0.4**. You do not need to extract or move it manually. Models are downloaded separately in Step 6.

### 2. Verify and extract the ZIP

Paste this entire block into ordinary Windows PowerShell:

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteDownloads = Join-Path $env:USERPROFILE 'Downloads'
$graniteFolder = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.4'
$graniteExpectedZipHash = '6788E6E293EB1D7129733F4C7246AABA26F8BC5F068751C35806418C17391126'
$graniteStepOne = 'STOP: Return to STEP 1, download the correct Granite-Edge-AI-Setup.zip completely, then repeat Step 2. Do not bypass verification.'
try {
    $graniteCandidates = @(Get-ChildItem -LiteralPath $graniteDownloads -Filter 'Granite-Edge-AI-Setup*.zip' -File |
        Where-Object { $_.Name -match '^Granite-Edge-AI-Setup(?: \(\d+\))?\.zip$' } |
        Sort-Object LastWriteTime -Descending)
    $graniteZip = $null
    foreach ($graniteCandidate in $graniteCandidates) {
        Write-Host "Checking $($graniteCandidate.Name). Please wait."
        if ((Get-FileHash -LiteralPath $graniteCandidate.FullName -Algorithm SHA256).Hash -eq $graniteExpectedZipHash) {
            $graniteZip = $graniteCandidate.FullName
            break
        }
    }
    if (-not $graniteZip) { throw "No complete matching ZIP was found. $graniteStepOne" }
    Write-Host "Verified download: $graniteZip"
    foreach ($granitePath in @('C:\Downloads', $graniteFolder)) {
        if (Test-Path -LiteralPath $granitePath) {
            $graniteItem = Get-Item -LiteralPath $granitePath -Force
            if (-not $graniteItem.PSIsContainer -or ($graniteItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Destination is not a regular folder: $granitePath. Nothing was overwritten."
            }
        }
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $graniteFolder) {
        $graniteItems = @(Get-ChildItem -LiteralPath $graniteFolder -Recurse -Force)
        if ($graniteItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
            throw 'The existing setup folder contains linked items. Nothing was changed.'
        }
        $graniteArchive = [IO.Compression.ZipFile]::OpenRead($graniteZip)
        try {
            $graniteEntries = @($graniteArchive.Entries | Where-Object { $_.Name -ne '' })
            if (@($graniteItems | Where-Object { -not $_.PSIsContainer }).Count -ne $graniteEntries.Count) {
                throw 'The existing extraction differs. Rename only the old Granite-Edge-AI-Setup-1.0.4 folder, then repeat Step 2. Do not delete your models.'
            }
            foreach ($graniteEntry in $graniteEntries) {
                $graniteFile = Join-Path $graniteFolder $graniteEntry.FullName
                if (-not (Test-Path -LiteralPath $graniteFile -PathType Leaf)) { throw "Missing setup file. $graniteStepOne" }
                $graniteStream = $graniteEntry.Open()
                $graniteSha = [Security.Cryptography.SHA256]::Create()
                try { $graniteExpected = [BitConverter]::ToString($graniteSha.ComputeHash($graniteStream)).Replace('-', '') }
                finally { $graniteStream.Dispose(); $graniteSha.Dispose() }
                if ((Get-FileHash -LiteralPath $graniteFile -Algorithm SHA256).Hash -ne $graniteExpected) {
                    throw 'The existing extraction is old or different. Rename only the old Granite-Edge-AI-Setup-1.0.4 folder, then repeat Step 2. Nothing was overwritten.'
                }
            }
        } finally { $graniteArchive.Dispose() }
        Write-Host 'Already extracted and verified - continue to Step 3.'
    } else {
        New-Item -ItemType Directory -Path 'C:\Downloads' -Force | Out-Null
        [IO.Compression.ZipFile]::ExtractToDirectory($graniteZip, $graniteFolder)
        Write-Host 'ZIP verified and extracted - continue to Step 3.'
    }
} catch {
    throw
}
}
```

Wait for the success message. This version uses its own setup folder, so an older `C:\Downloads\Granite-Edge-AI-Setup` folder can remain untouched. An existing matching extraction is reused; nothing is overwritten. If the versioned folder differs, rename only that setup folder and rerun this step. If Windows denies access to `C:\Downloads`, ask for help with that permission; downloading again will not fix a permission error.

### 3. Check the application files

In ordinary Windows PowerShell, paste:

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteSetup = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.4\Setup-Granite.ps1'
if (-not (Test-Path -LiteralPath $graniteSetup -PathType Leaf)) {
    throw 'STOP: Complete STEP 1 with the correct ZIP, then Step 2. Setup-Granite.ps1 is missing.'
}
Unblock-File -LiteralPath $graniteSetup
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteSetup -Step Check
if ($LASTEXITCODE -ne 0) { throw 'STOP: The check failed. For missing or mismatched setup files, repeat STEP 1 and Step 2. Otherwise follow the error above.' }
}
```

Expected: **Bundle hashes and Microsoft dependency signatures passed. Nothing installed.** This is a success message, not an error: Step 3 only checks the files. Continue to Step 4 to install the app. RemoteSigned applies only to the child PowerShell process; do not bypass a managed-device policy.

### 4. Install or update Granite

**4A — Open a new administrator window.** In your current PowerShell window, run:

```powershell
Start-Process powershell.exe -Verb RunAs
```

Approve the Windows prompt. **This command only opens another PowerShell window; it does not download or install anything.** If Windows requires someone else's account credentials, stop and ask for help: Granite must be installed for the account that will use it.

**4B — Install in the NEW window.** Its title should begin with **Administrator**. Paste the following block into that new window, not the original one. Administrator access is needed for certificate trust and dependencies; installation still requires your approval.

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteSetup = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.4\Setup-Granite.ps1'
if (-not (Test-Path -LiteralPath $graniteSetup -PathType Leaf)) {
    throw 'STOP: Complete STEP 1 with the correct ZIP, then Step 2. The setup script is missing.'
}
$granitePrincipal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $granitePrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'STOP: This is not the administrator window. Run Step 4A, then paste Step 4B into the NEW Administrator window. Do not download or extract again.'
}
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteSetup -Step Install
if ($LASTEXITCODE -ne 0) { throw 'STOP: Installation did not finish. Do not run Step 5. Follow the specific error above; only missing or mismatched setup files require Steps 1–2 again.' }
}
```

If the current signed version (1.0.4.0) or a newer one is installed, the script says so and skips installation without scanning every installed file. An older signed version can be updated after approval; a development registration is not replaced.

When asked, type **INSTALL** to approve trusting the supplied test certificate in **Trusted People**, installing missing .NET 8 x64 and Windows App Runtime dependencies, and installing or updating Granite. No root certificate or private signing key is supplied. The script checks the expected certificate automatically; you do not need to contact me to confirm its thumbprint. The certificate expires on 14 December 2026.

**Wait for “Installed” or a message saying this version or a newer one is already installed. Only then continue to Step 5.** If a restart is requested, restart Windows and repeat Step 4. Close the administrator window after installation succeeds.

If you see **“Run Windows PowerShell as administrator”**, repeat Step 4A and use the new window. Your verified download is not the problem. The packaged script's generic “return to Step 1” warning applies only to missing or mismatched setup files, not this permissions error.

### 5. Open Granite

Open ordinary Windows PowerShell and paste:

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteSetup = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.4\Setup-Granite.ps1'
if (-not (Test-Path -LiteralPath $graniteSetup -PathType Leaf)) {
    throw 'STOP: Return to STEP 1, download the correct ZIP and complete Step 2.'
}
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteSetup -Step Launch
if ($LASTEXITCODE -ne 0) { throw 'STOP: Launch failed. Check the error above; complete Step 4 if Granite is missing or outdated. For missing setup files, return to STEP 1.' }
}
```

**This step requests the app to open. Wait until the Granite window actually appears.** A command returning without an error does not prove that Windows allowed the app to start. No model is needed just to open it. Launch checks the installed version without repeatedly hashing installed files.

If it says **“Granite is not installed for this account”**, installation has not completed for your current Windows account: return to Step 4, not the download step. If Smart App Control blocks a file or no window appears, stop and report the notification or error. Re-downloading the same verified ZIP does not resolve a security-policy block.

### 6. Download and prepare the models — do not skip preparation

Use the official IBM download for GGUF below. For OpenVINO, use the tested model ZIP: an official download of this exact prepared ZIP has not been verified. **Click Download on each model page** and wait for both files to finish. These commands open your browser; they do not download automatically.

**GGUF — official IBM `granite-4.1-3b-Q4_K_M.gguf`:**

```powershell
Start-Process 'https://huggingface.co/ibm-granite/granite-4.1-3b-GGUF/blob/ab4701481089b58a082ef63cc1cee738887293ff/granite-4.1-3b-Q4_K_M.gguf'
```

This pinned IBM file matches the size and SHA-256 checked by the preparation command.

**OpenVINO — tested `Granite-4.1-3B-OpenVINO-Raw.zip`:**

```powershell
Start-Process 'https://liveuclac-my.sharepoint.com/:u:/g/personal/ucab280_ucl_ac_uk/IQBTmsCaY2ZkRJexAvCVJcB0Af_foYWoiaQXUF46MchpAog?e=vSJLt9'
```

[IBM's original Granite 4.1 3B model](https://huggingface.co/ibm-granite/granite-4.1-3b) is the upstream source, not a ready-to-import OpenVINO package. Use the ZIP above for these steps; do not substitute the original model files or a different converted model.

**GGUF fallback — only if the official download is unavailable:**

```powershell
Start-Process 'https://liveuclac-my.sharepoint.com/:u:/g/personal/ucab280_ucl_ac_uk/IQC0ragzwssPRoyStBSqIVv3AXJsY_GpjQ5lFjM0HWZF9u8?e=Lfanrf'
```

Download the GGUF from only one source; both links are for the same verified file.

Download:

- `granite-4.1-3b-Q4_K_M.gguf`
- `Granite-4.1-3B-OpenVINO-Raw.zip`

Save both files in your normal Downloads folder or `C:\Downloads`. Keep the exact model filenames without `(1)` suffixes and wait for downloads to finish. The command below also searches up to three folder levels inside those locations, including extracted OneDrive folders. It verifies the files before copying them directly into `C:\Downloads`; original downloads are kept. Do not use incomplete `.partial` files.

**Only if you used the old whole-folder link:** first extract the outer OneDrive collection ZIP into Downloads. Leave the OpenVINO model ZIP inside it zipped. The preparation command can find the two model downloads in the extracted folders; it cannot search inside an unopened outer ZIP. You can also place the two files directly into Downloads yourself.

The downloads total about 7.36 GB and OpenVINO extraction needs about another 6.82 GB. Copying files from another folder can need another 7.36 GB on C:. The command checks free space before copying or repairing output. If the prepared folder has missing or truncated model files, or contains only a second folder of the same model name, it preserves the previous folder under a unique backup name and extracts a fresh copy from the verified ZIP. Nothing is deleted. Unexpected extra items and checksum mismatches are rejected rather than overwritten. Other programs can still consume space while extraction runs; stop on any error.

**If you already extracted OpenVINO manually:** keep its original model ZIP available too. The command needs the verified ZIP to check or prepare the model. Moving an extracted folder alone does not remove Windows download marks.

**IMPORTANT: Run the following command after downloading the OpenVINO ZIP and before importing it. Otherwise Windows download marks can cause `package_unsafe_path`.** It finds and verifies both complete downloads, copies them to `C:\Downloads` if needed, extracts OpenVINO directly to **C:\Downloads\Granite-4.1-3B-OpenVINO-Raw**, and removes the download marks from verified files. It does not open File Explorer or download the models through PowerShell.

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteModelsScript = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.4\Prepare-GraniteModels.ps1'
if (-not (Test-Path -LiteralPath $graniteModelsScript -PathType Leaf)) {
    throw 'STOP: You need the correct application ZIP first. Return to STEP 1 and complete Step 2, then repeat this model-preparation command.'
}
if ((Get-FileHash -LiteralPath $graniteModelsScript -Algorithm SHA256).Hash -ne 'B2598F07CBF140FEA32628CEB28A06F37767B8574E4447C731346618B3F90743') {
    throw 'STOP: This is an old or different model-preparation script. Download the correct ZIP in STEP 1 and repeat Step 2.'
}
Unblock-File -LiteralPath $graniteModelsScript

# Find complete model downloads, including files inside extracted folders.
$graniteDestination = 'C:\Downloads'
$graniteBrowserDownloads = Join-Path $env:USERPROFILE 'Downloads'
function Assert-GraniteRegularPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "STOP: Linked paths are not supported: $current"
            }
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}
function Get-GraniteDownloads([string]$Root) {
    Assert-GraniteRegularPath $Root
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { return }
    $queue = [Collections.Generic.Queue[object]]::new()
    $queue.Enqueue(@{ Path = $Root; Depth = 0 })
    $count = 0
    while ($queue.Count -gt 0) {
        $folder = $queue.Dequeue()
        foreach ($item in Get-ChildItem -LiteralPath $folder.Path -Force) {
            $count++
            if ($count -gt 20000) { throw 'STOP: Too many items to search. Place the two model downloads directly in Downloads and retry.' }
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($item.PSIsContainer) {
                if ($folder.Depth -lt 3) { $queue.Enqueue(@{ Path = $item.FullName; Depth = $folder.Depth + 1 }) }
            } elseif ($item.Name -in @('granite-4.1-3b-Q4_K_M.gguf', 'Granite-4.1-3B-OpenVINO-Raw.zip')) {
                $item
            }
        }
    }
}
$graniteFiles = @(foreach ($root in @($graniteDestination, $graniteBrowserDownloads) | Select-Object -Unique) { Get-GraniteDownloads $root })
$graniteVerified = @()
foreach ($expected in @(
    @{ Name = 'granite-4.1-3b-Q4_K_M.gguf'; Bytes = 2099501664; Hash = '662B0626CD58F443BAEA23559B469DF6576A81D349649C59413B36A9FB32EB29' },
    @{ Name = 'Granite-4.1-3B-OpenVINO-Raw.zip'; Bytes = 5257095946; Hash = '28DD8AF79F2A2A1CB2EA91E5BE18DAD02CEF5681AF3266160533842C743043C6' }
)) {
    $target = Join-Path $graniteDestination $expected.Name
    Assert-GraniteRegularPath $target
    if (Test-Path -LiteralPath $target) {
        $targetItem = Get-Item -LiteralPath $target
        if ($targetItem.PSIsContainer -or $targetItem.Length -ne $expected.Bytes -or
            (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expected.Hash) {
            throw "STOP: An existing destination differs: $target. Nothing will overwrite it. Move that conflicting item aside yourself before retrying."
        }
        $source = $target
    } else {
        $source = $null
        foreach ($candidate in $graniteFiles | Where-Object { $_.Name -eq $expected.Name -and $_.Length -eq $expected.Bytes }) {
            Assert-GraniteRegularPath $candidate.FullName
            Write-Host "Verifying $($candidate.FullName). Please wait."
            if ((Get-FileHash -LiteralPath $candidate.FullName -Algorithm SHA256).Hash -eq $expected.Hash) { $source = $candidate.FullName; break }
        }
        if (-not $source) { throw "STOP: No verified $($expected.Name) found. Download it using STEP 6, or extract only the outer OneDrive collection ZIP. Keep the OpenVINO model ZIP. Search covers Downloads and up to three folder levels below it." }
    }
    $graniteVerified += @{ Source = $source; Target = $target; Hash = $expected.Hash }
}
# Check the expected extraction and free space before copying or renaming anything.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$graniteZipSource = ($graniteVerified | Where-Object { $_.Target.EndsWith('.zip') }).Source
$graniteArchive = [IO.Compression.ZipFile]::OpenRead($graniteZipSource)
try {
    $graniteMembers = @($graniteArchive.Entries | Where-Object { $_.Name -ne '' })
    $graniteNames = @{}
    [long]$graniteExtractBytes = 0
    foreach ($member in $graniteMembers) {
        $prefix = 'Granite-4.1-3B-OpenVINO-Raw/'
        if (-not $member.FullName.StartsWith($prefix, [StringComparison]::Ordinal)) { throw 'STOP: Unexpected ZIP layout.' }
        $name = $member.FullName.Substring($prefix.Length)
        if ($name -notmatch '^[A-Za-z0-9_.-]+$' -or $name -in '.', '..' -or $graniteNames.ContainsKey($name)) { throw 'STOP: Unexpected ZIP member.' }
        $graniteNames[$name] = $member.Length
        $graniteExtractBytes += $member.Length
    }
} finally { $graniteArchive.Dispose() }
$graniteModelFolder = Join-Path $graniteDestination 'Granite-4.1-3B-OpenVINO-Raw'
Assert-GraniteRegularPath $graniteModelFolder
$graniteRepair = $false
$graniteNeedsExtraction = -not (Test-Path -LiteralPath $graniteModelFolder)
if (-not $graniteNeedsExtraction) {
    if (-not (Test-Path -LiteralPath $graniteModelFolder -PathType Container)) { throw 'STOP: Model destination is not a folder.' }
    $children = @(Get-ChildItem -LiteralPath $graniteModelFolder -Force)
    if ($children.Count -eq 1 -and $children[0].PSIsContainer -and $children[0].Name -eq 'Granite-4.1-3B-OpenVINO-Raw') {
        Assert-GraniteRegularPath $children[0].FullName
        $graniteRepair = $true
    } else {
        foreach ($child in $children) {
            Assert-GraniteRegularPath $child.FullName
            if ($child.PSIsContainer -or -not $graniteNames.ContainsKey($child.Name)) { throw 'STOP: The model folder contains unexpected items. Nothing was changed; do not delete your files.' }
            if ($child.Length -ne $graniteNames[$child.Name]) { $graniteRepair = $true }
        }
        if ($children.Count -ne $graniteNames.Count) { $graniteRepair = $true }
    }
    $graniteNeedsExtraction = $graniteRepair
}
[long]$graniteRequired = 1GB
foreach ($file in $graniteVerified) {
    if ($file.Source -ne $file.Target) { $graniteRequired += (Get-Item -LiteralPath $file.Source).Length }
}
if ($graniteNeedsExtraction) { $graniteRequired += $graniteExtractBytes }
$graniteDrive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($graniteDestination))
if ($graniteDrive.AvailableFreeSpace -lt $graniteRequired) {
    throw ('STOP: Not enough free storage. Need about {0:N1} GiB free on {1}; available: {2:N1} GiB. Free space and rerun Step 6. Existing files were not changed.' -f ($graniteRequired / 1GB), $graniteDrive.Name, ($graniteDrive.AvailableFreeSpace / 1GB))
}
New-Item -ItemType Directory -Path $graniteDestination -Force | Out-Null
foreach ($file in $graniteVerified) {
    if ($file.Source -ne $file.Target) {
        Write-Host "Copying verified download to $($file.Target). The original is kept."
        [IO.File]::Copy($file.Source, $file.Target, $false)
        if ((Get-FileHash -LiteralPath $file.Target -Algorithm SHA256).Hash -ne $file.Hash) { throw 'STOP: Copied file verification failed. Do not continue.' }
    }
}

# Preserve recognised incomplete or double-folder output; never overwrite it.
if ($graniteRepair) {
    Assert-GraniteRegularPath $graniteModelFolder
    $backupName = 'Granite-4.1-3B-OpenVINO-Raw-backup-' + [guid]::NewGuid().ToString('N')
    Rename-Item -LiteralPath $graniteModelFolder -NewName $backupName
    Write-Host "Previous folder preserved at $(Join-Path $graniteDestination $backupName). Preparing a fresh extraction."
}
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteModelsScript -ModelsDirectory 'C:\Downloads'
if ($LASTEXITCODE -ne 0) { throw 'STOP: Model preparation failed. Follow the error above and do not import an incomplete folder. Missing model downloads require STEP 6; missing setup files require STEP 1.' }

# Check every extracted file against the verified ZIP before final success.
$graniteArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $graniteDestination 'Granite-4.1-3B-OpenVINO-Raw.zip'))
try {
    $entries = @($graniteArchive.Entries | Where-Object { $_.Name -ne '' })
    $outputFiles = @(Get-ChildItem -LiteralPath $graniteModelFolder -Force)
    if ($outputFiles.Count -ne $entries.Count) { throw 'STOP: Extraction is incomplete. Do not import the model.' }
    foreach ($entry in $entries) {
        $path = Join-Path $graniteModelFolder $entry.Name
        Assert-GraniteRegularPath $path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -ne $entry.Length) { throw "STOP: Missing or incomplete file: $path" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $expectedHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $expectedHash) { throw "STOP: Extracted file checksum mismatch: $path" }
    }
} finally { $graniteArchive.Dispose() }
$outputFiles | ForEach-Object { Unblock-File -LiteralPath $_.FullName }
Unblock-File -LiteralPath $graniteModelFolder
$graniteMarks = @(@(Get-Item -LiteralPath $graniteModelFolder) + $outputFiles | ForEach-Object {
    Get-Item -LiteralPath $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
})
if ($graniteMarks.Count -gt 0) { throw 'STOP: Windows download marks remain. Do not import; send the full Step 6 output for help.' }
Write-Host "STEP 6 COMPLETE: OpenVINO files verified. Import $graniteModelFolder"
}
```

Wait for the final **STEP 6 COMPLETE: OpenVINO files verified** message. Earlier messages from the packaged script are not the end of this step. Large-file verification takes time. The final check compares every extracted file with the verified ZIP, removes Windows download marks from those verified files and the model folder, then checks that none remain. This addresses the download-mark cause of `package_unsafe_path`, not every possible unsafe-path error. Close and reopen Granite, then import the exact prepared folder again; do not re-extract the ZIP afterwards. Keep the model folder in place after importing it.

### 7. Select a model

In Granite, use **Choose model** and try one route at a time:

If you downloaded the whole model folder, complete the outer-ZIP instructions and preparation command in Step 6 first. Do not select the downloaded collection folder or either ZIP in Granite.

- **GGUF:** select `granite-4.1-3b-Q4_K_M.gguf` at the exact **GGUF ready** path printed by Step 6.
- **OpenVINO:** select the complete folder `C:\Downloads\Granite-4.1-3B-OpenVINO-Raw`. Do not select its ZIP, XML or BIN individually. There should not be another Raw folder inside it.

Follow inspection, hardware fit and configuration. Available choices depend on the laptop's RAM, free storage and verified runtime support. Once chat opens, try: **Explain what a computer processor does in two sentences.**

If storage is low, free up space before optimisation. If Granite asks you to import again, use **Import model again** and repeat the checks. Do not replace runtime files or bypass verification.

### If something still goes wrong

Send the full error message and step number, together with the laptop's Windows version, processor and RAM. There is no need to contact me if the steps complete successfully.

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
