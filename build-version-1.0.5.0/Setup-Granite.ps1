param([ValidateSet('Check','Install','Models','Launch')][string]$Step = 'Check')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$packageName = '488d3892-c214-40c5-9a6a-1154c1e69fff'
$packagePublisher = 'CN=Granite Local Preview'
$packageVersion = [version]'1.0.5.0'
function Assert-Hash([string]$Path,[string]$Hash) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) {
        throw "Checksum mismatch: $Path. Stop; do not use this file."
    }
}
function Get-PreviewPackage {
    @(Get-AppxPackage -Name $packageName | Where-Object { $_.Publisher -eq $packagePublisher } |
        Sort-Object Version -Descending | Select-Object -First 1)
}
function Check-Bundle {
    Assert-Hash (Join-Path $PSScriptRoot 'Prepare-GraniteModels.ps1') 'B2598F07CBF140FEA32628CEB28A06F37767B8574E4447C731346618B3F90743'
    Assert-Hash (Join-Path $PSScriptRoot 'Granite-Laptop-Test-x64.msix') 'BF9CC156C9E098EF3E6CE4B6A8F15D2BA01FE603B3632D84A91108C34F1BC998'
    Assert-Hash (Join-Path $PSScriptRoot 'Granite-Test.cer') 'F24DEDF2CE0AFB9A88B295C72FFFEE63EBEB4DB859326F3FC7CC5F2259DE084F'
    Assert-Hash (Join-Path $PSScriptRoot 'Microsoft.WindowsAppRuntime.2.msix') 'A71C600B2FB719C8D70120151965891F360857C1FF3D9D5C47ED92622A60F665'
    $netInstaller = Join-Path $PSScriptRoot 'dotnet-runtime-8.0.31-win-x64.exe'
    if ((Get-FileHash -LiteralPath $netInstaller -Algorithm SHA512).Hash -ne '2dc2f346a4bcb53ab15ab6ab84348c02eb31b0a3d4e8e06aac73b4c89f1b001af01943ed4c5735fa5585f1dd731a942d2034577dfe937f4454367ff2d6edf929') {
        throw '.NET installer checksum mismatch.'
    }
    foreach ($signedFile in @($netInstaller,(Join-Path $PSScriptRoot 'Microsoft.WindowsAppRuntime.2.msix'))) {
        if ((Get-AuthenticodeSignature -FilePath $signedFile).Status -ne 'Valid') {
            throw "Microsoft signature could not be validated: $signedFile"
        }
    }
}
try {
    if ($Step -eq 'Check') {
        Check-Bundle
        Write-Host 'Bundle hashes and Microsoft dependency signatures passed. Nothing installed.'
        return
    }
    if ($Step -eq 'Install') {
        if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Use Windows PowerShell (powershell.exe), not PowerShell 7.' }
        if ((Get-CimInstance Win32_OperatingSystem).BuildNumber -lt 22000) { throw 'Use Windows 11 for this test.' }
        if ((Get-CimInstance Win32_Processor | Select-Object -First 1).Architecture -ne 9) { throw 'An x64 laptop is required.' }
        Check-Bundle
        $existing = @(Get-PreviewPackage)
        if ($existing.Count -gt 0) {
            if ($existing[0].Status -ne 'Ok' -or $existing[0].IsDevelopmentMode) {
                throw 'The existing preview is not a supported signed package. Nothing was changed.'
            }
            if ([version]$existing[0].Version -ge $packageVersion) {
                Write-Host 'This preview or a newer one is already installed. Continue to Launch.'
                return
            }
        }
        $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw 'Run Windows PowerShell as administrator using your own account.'
        }
        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $PSScriptRoot 'Granite-Test.cer'))
        if ($certificate.Thumbprint -ne 'B19308FA005A6F4E566F44078B27C01B4EDA872B' -or
            $certificate.Subject -ne $packagePublisher -or $certificate.NotAfter -le (Get-Date)) {
            throw 'The supplied certificate is unexpected or expired.'
        }
        Write-Host 'This trusts the self-signed preview certificate for the computer and installs dependencies and Granite Local Preview alongside the earlier app.'
        Write-Host 'Certificate thumbprint: B19308FA005A6F4E566F44078B27C01B4EDA872B'
        if ((Read-Host 'Type INSTALL to approve these changes on this test laptop') -cne 'INSTALL') { throw 'Installation cancelled.' }
        $dotnetExe = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
        $hasRuntime = $false
        if (Test-Path -LiteralPath $dotnetExe) { $hasRuntime = [bool]((& $dotnetExe --list-runtimes) -match '^Microsoft.NETCore.App 8\.') }
        if (-not $hasRuntime) {
            $process = Start-Process -FilePath (Join-Path $PSScriptRoot 'dotnet-runtime-8.0.31-win-x64.exe') -ArgumentList '/install /passive /norestart' -WindowStyle Hidden -Wait -PassThru
            if ($process.ExitCode -eq 3010) { throw '.NET installed and requested a restart. Restart Windows, then repeat Install.' }
            if ($process.ExitCode -ne 0) { throw ".NET installer failed: $($process.ExitCode)" }
        }
        Import-Certificate -FilePath (Join-Path $PSScriptRoot 'Granite-Test.cer') -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
        Add-AppxPackage -Path (Join-Path $PSScriptRoot 'Granite-Laptop-Test-x64.msix') -DependencyPath (Join-Path $PSScriptRoot 'Microsoft.WindowsAppRuntime.2.msix')
        $confirmed = @(Get-PreviewPackage)
        if ($confirmed.Count -ne 1 -or [version]$confirmed[0].Version -lt $packageVersion) { throw 'Preview installation was not confirmed.' }
        Write-Host 'Installed. Close this administrator window and run the Launch step in ordinary PowerShell.'
        return
    }
    if ($Step -eq 'Launch') {
        $app = @(Get-PreviewPackage)
        if ($app.Count -ne 1 -or [version]$app[0].Version -lt $packageVersion) {
            throw 'Granite Local Preview 1.0.5.0 is not installed for this account. Complete Step 4.'
        }
        Start-Process explorer.exe -ArgumentList ('shell:AppsFolder\{0}!App' -f $app[0].PackageFamilyName)
        return
    }
    if ($Step -eq 'Models') {
        & (Join-Path $PSScriptRoot 'Prepare-GraniteModels.ps1')
    }
} catch {
    Write-Host 'STOP: Do not continue to the next step. Follow the specific error and keep your model files.' -ForegroundColor Yellow
    throw
}
