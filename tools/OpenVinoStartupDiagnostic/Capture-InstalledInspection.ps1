# Run from administrator Windows PowerShell while Granite is open normally.
# Does not launch Granite, change permissions or change application files.
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this capture script in administrator Windows PowerShell. Keep Granite running normally, not as administrator.'
}
$package = Get-AppxPackage -Name '488d3892-c214-40c5-9a6a-1154c1e69fff'
if (-not $package) { throw 'Granite is not installed for this account. Use the same Windows account as the app.' }
$root = $package.InstallLocation.TrimEnd('\') + '\'
$appProcesses = @(Get-Process | Where-Object {
    try { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and $_.ProcessName -notmatch 'Worker' }
    catch { $false }
})
if ($appProcesses.Count -ne 1) {
    throw 'Open one instance of the installed Granite app normally, leave it on model selection, and run this script again.'
}
$appProcessId = $appProcesses[0].Id
$folder = Join-Path $env:USERPROFILE ('Downloads\Granite-InApp-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder | Out-Null
$etl = Join-Path $folder 'local-only.etl'
$report = Join-Path $folder 'Granite-in-app-errors.txt'
$sessionName = 'GraniteInspection-' + [guid]::NewGuid().ToString('N')
Write-Host 'This records .NET exceptions for 45 seconds. The raw local trace can contain exceptions from other .NET apps.'
Write-Host 'Only the Granite-filtered text report should be shared; inspect it for private paths first.'
$started = $false
try {
    & logman.exe create trace $sessionName -o $etl -p 'Microsoft-Windows-DotNETRuntime' 0x8000 5 -f bincirc -max 16 -ets
    if ($LASTEXITCODE -ne 0) { throw 'Windows could not start tracing. Do not change security settings to force it.' }
    $started = $true
    Write-Host 'NOW import the OpenVINO model in Granite and reproduce the inspection failure. Do not restart Granite.'
    Start-Sleep -Seconds 45
} finally {
    if ($started) {
        & logman.exe stop $sessionName -ets
        if ($LASTEXITCODE -ne 0) { Write-Warning "Trace stop failed. Run: logman stop $sessionName -ets" }
    }
}
$records = [Collections.Generic.List[string]]::new()
$records.Add("Package version: $($package.Version)")
$records.Add("Granite process ID: $appProcessId")
$records.Add('This captures managed exceptions in the installed app; caught exceptions are not automatically the cause.')
$count = 0
Get-WinEvent -Path $etl -Oldest -ErrorAction Stop | Where-Object {
    $_.ProcessId -eq $appProcessId -and $_.ProviderName -eq 'Microsoft-Windows-DotNETRuntime' -and $_.Id -eq 80
} | ForEach-Object {
    $count++
    $records.Add($_.ToXml())
}
$records.Add("Captured exception events: $count")
if ($count -eq 0) {
    $records.Add('No managed exception events captured. This does not establish success; the worker may have reported a failure without a managed exception, or capture timing may have missed it.')
}
$records | Set-Content -LiteralPath $report -Encoding UTF8
Write-Host "Capture finished. Send this text report: $report"
Write-Host 'Do not send local-only.etl: it can contain unrelated process data.'
