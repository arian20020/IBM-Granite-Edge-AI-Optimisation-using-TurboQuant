[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$JsonPath,
    [Parameter(Mandatory)][ValidateRange(1, 1048576)][int]$MaximumBytes,
    [Parameter(Mandatory)][ValidateRange(1, 64)][int]$MaximumDepth
)

$ErrorActionPreference = 'Stop'
try {
    Import-Module (Join-Path $PSScriptRoot 'OpenVinoClosedJson.psm1') -Force
    $null = Get-OpenVinoClosedJsonText -Path $JsonPath `
        -MaximumBytes $MaximumBytes -MaximumDepth $MaximumDepth
    [Console]::Out.WriteLine('closed_json_valid')
    exit 0
}
catch {
    [Console]::Out.WriteLine('closed_json_invalid')
    exit 1
}
