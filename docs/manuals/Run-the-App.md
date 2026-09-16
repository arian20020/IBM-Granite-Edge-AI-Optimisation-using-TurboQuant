# Run the app

To install and run the packaged app, follow the [README setup steps](../../README.md#setup-and-first-use). They cover the ZIP download, verification, installation, launch and both model downloads. Git and Visual Studio are not needed. The instructions below also document the existing demonstration installation.

The **prepared project computer** means the Windows account where the developer has already installed and tested Granite. It does not mean any computer that has downloaded this repository. If you are using your own PC, read the separate-PC section below before trying commands.

## On the project demonstration computer

These steps open the registered copy used by the recorded inspection tests. They avoid old shortcuts that may open a different build.

1. Finish any active work and close other Granite windows.
2. Press **Windows + R**.
3. Paste this exact command, then press **Enter**:

   Copy only the line inside the box, not the box markers. This command belongs in the Windows Run box, not in the app's chat field.

```text
explorer.exe shell:AppsFolder\488d3892-c214-40c5-9a6a-1154c1e69fff_gqahnnh6hk88w!App
```

4. Wait for the Granite window to appear. If Windows cannot find it, stop and use the checks below. Do not choose a similarly named old build.
5. Follow [Download a model](Download-a-Model.md) for a concrete first-run example using the Balanced GGUF download. The same guide explains manual import and the separate OpenVINO package requirement.
6. Follow the [user manual](User-Manual.md) through inspection, hardware fit and chat. Available choices depend on the model and free memory.

The registration and executable hash below are historical records from the earlier manual review, not the current 1.0.3.0 installer. Use the starter guide for that package.

| Item | Recorded value |
| --- | --- |
| Package name | `488d3892-c214-40c5-9a6a-1154c1e69fff` |
| Package family name | `488d3892-c214-40c5-9a6a-1154c1e69fff_gqahnnh6hk88w` |
| Package version / architecture | `1.0.0.0` / x64 |
| Application ID | `App` |
| Local installation | `%LOCALAPPDATA%\GraniteRestoredMain-20260914` |
| Executable | `IBM Granite with TurboQuant (Intel).exe` |

Executable SHA-256:

```text
1A101404C469BA0C036C399AAE2BE647DF6B3CB834AD5E094510F4C00673A430
```

This is not an installer checksum. The folder date and package version alone do not prove which source commit produced the app. See the [release record](../../release-evidence/README.md).

### If it does not open

Click Windows Start, type **Windows PowerShell**, and open it normally. Do not choose **Run as administrator**. Copy both lines below into that window and press Enter:

```powershell
Get-AppxPackage -Name '488d3892-c214-40c5-9a6a-1154c1e69fff' |
    Select-Object Name, Version, PackageFamilyName, InstallLocation, Status
```

No output means that package is not registered for your Windows account. Use the [README installation steps](../../README.md#setup-and-first-use); do not register a random build folder.

If a package is listed, compare its family name and location with the table above. To check the recorded local executable without changing it:

```powershell
$graniteExe = Join-Path $env:LOCALAPPDATA 'GraniteRestoredMain-20260914\IBM Granite with TurboQuant (Intel).exe'
Get-FileHash -LiteralPath $graniteExe -Algorithm SHA256
```

A different hash or location needs investigation. Do not overwrite files to force a match. If the app opens but reports a missing worker or failed verification, follow [troubleshooting](Known-Limitations.md#troubleshooting).

## On an examiner's own computer

The command above is for an existing registration. It does not install the app and is not a portable download link.

Follow the [README setup steps](../../README.md#setup-and-first-use), which include the package link and checksum. Step 4 installs the app, updates an older signed package after approval or recognises a current signed installation, Step 5 opens it, and Step 6 explains browser downloads for GGUF and OpenVINO followed by local verification and extraction. Use the README commands in order rather than repeating the demonstration-computer checks above: a packaged installation normally has a different installation location.

The first two commands in Step 6 open the individual GGUF and OpenVINO download pages in your browser. Click **Download** for each model file, wait for both downloads to finish, then run the mandatory preparation block to verify, extract and remove download marks from the verified files. The commands do not perform the downloads for you.

This is a self-signed test package. Installation on a separate laptop and both model journeys have not yet been verified. The [package handover checklist](../../release-evidence/Package-Handover.md) records broader release checks; supplying the ZIP does not establish that they all passed.

Do not copy only the EXE, use the compile-only CI output as a release, or disable Windows security to get an unknown package running. A source checkout is not an installed application. Developers should use the separate [build guide](Build-and-Installation.md).

## What to record for the report

Record the date, computer, package identity, model, steps and actual result. Opening the window proves launch only. Successful inspection and chat are separate checks. Trying this on the prepared computer is a demonstration check, not proof of a clean installation on another PC.
