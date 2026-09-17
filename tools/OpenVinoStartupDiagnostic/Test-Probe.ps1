param([Parameter(Mandatory)][string]$Probe, [Parameter(Mandatory)][string]$Runtime)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Probe)) { throw 'Diagnostic probe has not been built.' }
$output = & $Probe 2>&1 | Out-String
if ($LASTEXITCODE -ne 2) { throw 'Missing arguments must fail with exit 2.' }
$output = & $Probe ($Runtime + '-missing') 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or $output -notmatch 'FAILED') { throw 'Missing runtime must fail explicitly.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('GraniteProbe-Rejected-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $fixture 'worker-manifest.json')
$output = & $Probe $fixture 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or $output -notmatch 'FAILED' -or $output -match 'PROTECTED LAUNCH PASSED') {
    throw 'An untrusted manifest must be rejected before process launch.'
}
if ($output -notmatch 'OpenVinoWorkerClosureResolver') { throw 'Failure evidence must identify the failing check.' }
$output = & $Probe $Runtime 2>&1 | Out-String
$output
if ($LASTEXITCODE -ne 0 -or $output -notmatch 'HELLO VERIFIED' -or $output -notmatch 'CLEANUP VERIFIED') {
    throw 'Trusted local runtime must start, verify hello and clean up.'
}
'Probe checks passed.'
