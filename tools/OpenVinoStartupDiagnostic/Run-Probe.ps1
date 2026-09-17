$ErrorActionPreference = 'Stop'
& {
    $package = Get-AppxPackage -Name '488d3892-c214-40c5-9a6a-1154c1e69fff'
    if (-not $package) { throw 'Granite is not installed for this Windows account. Run as the same user who opens Granite.' }
    $probe = Join-Path $PSScriptRoot 'OpenVinoStartupDiagnostic.exe'
    if (-not (Test-Path -LiteralPath $probe -PathType Leaf)) { throw 'Extract the complete diagnostic ZIP first.' }
    $report = Join-Path $PSScriptRoot ('OpenVino-startup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')
    "Package version: $($package.Version)" | Tee-Object -FilePath $report
    & $probe (Join-Path $package.InstallLocation 'OVRuntime') 2>&1 | Tee-Object -FilePath $report -Append
    $probeExit = $LASTEXITCODE
    Write-Host "Diagnostic exit code: $probeExit"
    Write-Host "Send this report to Arian: $report"
    Write-Host 'This is a standalone startup test, not a full in-app model inspection.'
}
