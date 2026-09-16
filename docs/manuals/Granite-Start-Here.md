# Granite Edge AI: installation and model setup

Use **Granite-Edge-AI-Setup.zip** with this guide. No Git, Visual Studio or source build is needed. Use Windows 11 x64 and allow at least 20 GB of free space; optimisation may need more.

Run each numbered step separately. Copy each complete PowerShell code block using its copy button; do not copy Markdown link formatting, headings or the ``` markers. **If a step fails, stop: do not paste the next command. If setup files are missing or do not match, return to Step 1, download the correct ZIP and repeat Step 2. Do not skip the checks or continue with an older extracted folder.** Other errors, such as low storage or denied permission, need the action shown in the error message.

This is a self-signed test build. Its clean installation and upgrade still need testing on another laptop. Do not bypass your organisation's security policies.

## 1. Download the application ZIP

The command below opens the download page. **Click Download in your browser**, and wait for the file to finish downloading before Step 2.

Open ordinary **Windows PowerShell** and paste:

```powershell
Start-Process 'https://liveuclac-my.sharepoint.com/:u:/g/personal/ucab280_ucl_ac_uk/IQA3url8UXf3S5dWadOp1J_mAcnF6HngK5vyJivCi0lQr40?e=Sxcl88'
```

Save **Granite-Edge-AI-Setup.zip** in your normal Downloads folder (`C:\Users\<your username>\Downloads`). Names such as **Granite-Edge-AI-Setup (1).zip** or **Granite-Edge-AI-Setup (2).zip** are also accepted. Step 2 finds a copy with the correct checksum; you do not need to rename or delete earlier downloads. If your browser saves elsewhere, move the completed ZIP into your normal Downloads folder first.

Step 2 extracts it automatically to **C:\Downloads\Granite-Edge-AI-Setup-1.0.3**. You do not need to extract or move it manually. Models are downloaded separately in Step 6.

## 2. Verify and extract the ZIP

Paste this entire block into ordinary Windows PowerShell:

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteDownloads = Join-Path $env:USERPROFILE 'Downloads'
$graniteFolder = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.3'
$graniteExpectedZipHash = '8054E6219BF5BB568C93A354801234FD1F0C2D23BA0D93AF2EAE69D35EC8A697'
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
                throw 'The existing extraction differs. Rename only the old Granite-Edge-AI-Setup-1.0.3 folder, then repeat Step 2. Do not delete your models.'
            }
            foreach ($graniteEntry in $graniteEntries) {
                $graniteFile = Join-Path $graniteFolder $graniteEntry.FullName
                if (-not (Test-Path -LiteralPath $graniteFile -PathType Leaf)) { throw "Missing setup file. $graniteStepOne" }
                $graniteStream = $graniteEntry.Open()
                $graniteSha = [Security.Cryptography.SHA256]::Create()
                try { $graniteExpected = [BitConverter]::ToString($graniteSha.ComputeHash($graniteStream)).Replace('-', '') }
                finally { $graniteStream.Dispose(); $graniteSha.Dispose() }
                if ((Get-FileHash -LiteralPath $graniteFile -Algorithm SHA256).Hash -ne $graniteExpected) {
                    throw 'The existing extraction is old or different. Rename only the old Granite-Edge-AI-Setup-1.0.3 folder, then repeat Step 2. Nothing was overwritten.'
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

## 3. Check the application files

In ordinary Windows PowerShell, paste:

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteSetup = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.3\Setup-Granite.ps1'
if (-not (Test-Path -LiteralPath $graniteSetup -PathType Leaf)) {
    throw 'STOP: Complete STEP 1 with the correct ZIP, then Step 2. Setup-Granite.ps1 is missing.'
}
Unblock-File -LiteralPath $graniteSetup
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteSetup -Step Check
if ($LASTEXITCODE -ne 0) { throw 'STOP: The check failed. For missing or mismatched setup files, repeat STEP 1 and Step 2. Otherwise follow the error above.' }
}
```

Expected: **Bundle hashes and Microsoft dependency signatures passed. Nothing installed.** This reads the bundle files and may take a short while. It does not install anything. RemoteSigned applies only to the child PowerShell process; do not bypass a managed-device policy.

## 4. Install or update Granite

Close the ordinary PowerShell window. Open **Windows PowerShell as administrator** using your own account. This is needed for certificate trust and dependencies. No certificate is added until you approve installation.

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteSetup = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.3\Setup-Granite.ps1'
if (-not (Test-Path -LiteralPath $graniteSetup -PathType Leaf)) {
    throw 'STOP: Complete STEP 1 with the correct ZIP, then Step 2. The setup script is missing.'
}
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteSetup -Step Install
if ($LASTEXITCODE -ne 0) { throw 'STOP: Installation did not finish. For missing or mismatched setup files, repeat STEP 1 and Step 2. Otherwise follow the specific error above.' }
}
```

If the current signed version (1.0.3.0) or a newer one is installed, the script says so and skips installation without scanning every installed file. An older signed version can be updated after approval; a development registration is not replaced.

When asked, type **INSTALL** to approve trusting the supplied test certificate in **Trusted People**, installing missing .NET 8 x64 and Windows App Runtime dependencies, and installing or updating Granite. No root certificate or private signing key is supplied. The script checks the expected certificate automatically; you do not need to contact me to confirm its thumbprint. The certificate expires on 14 December 2026.

If a restart is requested, restart Windows and repeat this step. Otherwise wait for success, then close administrator PowerShell.

## 5. Open Granite

Open ordinary Windows PowerShell and paste:

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteSetup = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.3\Setup-Granite.ps1'
if (-not (Test-Path -LiteralPath $graniteSetup -PathType Leaf)) {
    throw 'STOP: Return to STEP 1, download the correct ZIP and complete Step 2.'
}
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteSetup -Step Launch
if ($LASTEXITCODE -ne 0) { throw 'STOP: Launch failed. Check the error above; complete Step 4 if Granite is missing or outdated. For missing setup files, return to STEP 1.' }
}
```

**This is the step that opens the app.** No model is needed just to open it. Launch checks the installed version without repeatedly hashing installed files.

## 6. Download and prepare the models — do not skip preparation

Run each command below to open that model's individual download page. **Click Download on each page** and wait for both files to finish downloading. These commands open your browser; they do not download automatically.

**GGUF — `granite-4.1-3b-Q4_K_M.gguf`:**

```powershell
Start-Process 'https://liveuclac-my.sharepoint.com/:u:/g/personal/ucab280_ucl_ac_uk/IQC0ragzwssPRoyStBSqIVv3AXJsY_GpjQ5lFjM0HWZF9u8?e=Lfanrf'
```

**OpenVINO — `Granite-4.1-3B-OpenVINO-Raw.zip`:**

```powershell
Start-Process 'https://liveuclac-my.sharepoint.com/:u:/g/personal/ucab280_ucl_ac_uk/IQBTmsCaY2ZkRJexAvCVJcB0Af_foYWoiaQXUF46MchpAog?e=vSJLt9'
```

Download:

- `granite-4.1-3b-Q4_K_M.gguf`
- `Granite-4.1-3B-OpenVINO-Raw.zip`

Save them in your normal Downloads folder or `C:\Downloads`. Keep these exact names without `(1)` suffixes. The command also accepts earlier downloads in `C:\Downloads\Granite-Models`. Wait for both downloads to finish. Together they are about 7.36 GB; extraction needs about another 6.82 GB. Do not use `.partial` files or extract the ZIP manually.

**Only if you used the old whole-folder download link:** the two links above download the model files individually, so skip this optional note and continue to the preparation command below. If you already downloaded the whole OneDrive model folder as an outer ZIP, follow these steps to find the two files; extracting that outer ZIP into Downloads alone may leave them inside another folder.

1. Extract the outer OneDrive ZIP, then open the resulting folder.
2. Find `granite-4.1-3b-Q4_K_M.gguf` and `Granite-4.1-3B-OpenVINO-Raw.zip`.
3. Move those **two files directly into `C:\Downloads`**, not a folder inside it. Alternatively, copy both files directly out of the outer ZIP into your normal Downloads folder (`C:\Users\<your username>\Downloads`). Wait for copying to finish.
4. **Keep the OpenVINO model ZIP zipped**, then run the preparation command below. The command extracts it to `C:\Downloads\Granite-4.1-3B-OpenVINO-Raw` and removes Windows download marks after verification.

For example, the files should be at:

```text
C:\Downloads\granite-4.1-3b-Q4_K_M.gguf
C:\Downloads\Granite-4.1-3B-OpenVINO-Raw.zip
```

They must not remain at a path such as `C:\Downloads\IBM Granite Models\...`. The preparation command does not search nested folders or read models from inside the outer ZIP. Do not overwrite existing files blindly; if copies already exist, use the preparation command to check them first. Keeping the outer ZIP and its extraction requires additional disk space.

If you already extracted the OpenVINO model manually, you still need its original ZIP in Downloads and must run preparation. Merely moving the extracted model folder does not remove Windows download marks. An existing output folder must match the verified ZIP; do not bypass a mismatch error.

**IMPORTANT: Run the following command after downloading the OpenVINO ZIP and before importing it. Otherwise Windows download marks can cause `package_unsafe_path`.** It verifies both complete downloads, extracts OpenVINO directly to **C:\Downloads\Granite-4.1-3B-OpenVINO-Raw**, and removes the download marks from verified files. It does not open File Explorer or download the models through PowerShell.

```powershell
& {
$ErrorActionPreference = 'Stop'
$graniteModelsScript = 'C:\Downloads\Granite-Edge-AI-Setup-1.0.3\Prepare-GraniteModels.ps1'
if (-not (Test-Path -LiteralPath $graniteModelsScript -PathType Leaf)) {
    throw 'STOP: You need the correct application ZIP first. Return to STEP 1 and complete Step 2, then repeat this model-preparation command.'
}
if ((Get-FileHash -LiteralPath $graniteModelsScript -Algorithm SHA256).Hash -ne 'B2598F07CBF140FEA32628CEB28A06F37767B8574E4447C731346618B3F90743') {
    throw 'STOP: This is an old or different model-preparation script. Download the correct ZIP in STEP 1 and repeat Step 2.'
}
Unblock-File -LiteralPath $graniteModelsScript
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File $graniteModelsScript -ModelsDirectory 'C:\Downloads'
if ($LASTEXITCODE -ne 0) { throw 'STOP: Model preparation failed. Follow the error above and do not import an incomplete folder. Missing model downloads require STEP 6; missing setup files require STEP 1.' }
}
```

Wait for **GGUF ready** and **Select this OpenVINO folder in Granite**. Large-file verification takes time. An existing model folder is reused only if its files match the verified ZIP; different or incomplete folders are not overwritten. Keep the model folder in place after importing it.

## 7. Select a model

In Granite, use **Choose model** and try one route at a time:

If you downloaded the whole model folder, complete the outer-ZIP instructions and preparation command in Step 6 first. Do not select the downloaded collection folder or either ZIP in Granite.

- **GGUF:** select `granite-4.1-3b-Q4_K_M.gguf` at the exact **GGUF ready** path printed by Step 6.
- **OpenVINO:** select the complete folder `C:\Downloads\Granite-4.1-3B-OpenVINO-Raw`. Do not select its ZIP, XML or BIN individually. There should not be another Raw folder inside it.

Follow inspection, hardware fit and configuration. Available choices depend on the laptop's RAM, free storage and verified runtime support. Once chat opens, try: **Explain what a computer processor does in two sentences.**

If storage is low, free up space before optimisation. If Granite asks you to import again, use **Import model again** and repeat the checks. Do not replace runtime files or bypass verification.

## If something still goes wrong

Send the full error message and step number, together with the laptop's Windows version, processor and RAM. There is no need to contact me if the steps complete successfully.
