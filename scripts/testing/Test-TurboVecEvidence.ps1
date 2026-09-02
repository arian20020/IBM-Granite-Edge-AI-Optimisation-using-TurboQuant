[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RunDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not [IO.Path]::IsPathFullyQualified($RunDirectory)) { throw 'Run directory must be absolute.' }
$Run=(Resolve-Path -LiteralPath $RunDirectory -ErrorAction Stop).Path
$Item=Get-Item -LiteralPath $Run -Force
if (-not $Item.PSIsContainer -or (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw 'Unsafe run directory.' }
$Terminal=Get-Content -LiteralPath (Join-Path $Run 'terminal.json') -Raw | ConvertFrom-Json
if ($Terminal.status -ne 'completed') { throw 'Evidence is not terminally complete.' }
foreach($Record in $Terminal.files) {
    if ($Record.name -match '[\\/]' -or $Record.name -eq 'terminal.json') { throw 'Invalid evidence filename.' }
    $Path=Join-Path $Run $Record.name
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing evidence file: $($Record.name)" }
    if ((Get-Item -LiteralPath $Path).Length -ne $Record.bytes) { throw "Byte mismatch: $($Record.name)" }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Record.sha256) { throw "Hash mismatch: $($Record.name)" }
}
$Arithmetic=Get-Content -LiteralPath (Join-Path $Run 'command-arithmetic.json') -Raw | ConvertFrom-Json
if ($Arithmetic.discovered -ne ($Arithmetic.executed+$Arithmetic.skipped)) { throw 'Invalid discovery arithmetic.' }
if ($Arithmetic.executed -ne ($Arithmetic.passed+$Arithmetic.failed)) { throw 'Invalid execution arithmetic.' }
$Results=Get-Content -LiteralPath (Join-Path $Run 'results.json') -Raw | ConvertFrom-Json
$Names=@($Results.configurations | ForEach-Object {$_.name} | Sort-Object)
if (($Names -join ',') -ne 'exact,tq2,tq3,tq4') { throw 'Configuration set mismatch.' }
if (@($Results.configurations | Where-Object {$_.warmup_batches -ne 5 -or $_.measured_batches -ne 30}).Count -ne 0) { throw 'Warm-up or measured count mismatch.' }
if (@($Results.configurations.embedding_matrix_sha256 | Sort-Object -Unique).Count -ne 1) { throw 'Embedding identity mismatch.' }
$Text=(Get-ChildItem -LiteralPath $Run -File -Filter '*.json*' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
if ($Text -match '(?i)[A-Z]:\\Users\\|PRIVATE_SENTINEL') { throw 'Private content detected.' }
Write-Host 'TurboVec evidence validation passed.'
