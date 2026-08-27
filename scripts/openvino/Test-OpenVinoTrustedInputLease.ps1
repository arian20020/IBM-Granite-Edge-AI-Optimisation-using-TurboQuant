[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchiveRoot,
    [Parameter(Mandatory)][string]$FixtureRoot
)

$ErrorActionPreference = 'Stop'
$lease = $null
function Assert-Denied([string]$Name, [scriptblock]$Mutation) {
    $denied = $false
    try { & $Mutation } catch { $denied = $true }
    if (-not $denied) { throw "trusted-mutation-was-not-denied:$Name" }
}

try {
    Import-Module (Join-Path $PSScriptRoot 'OpenVinoTrustedInputLease.psm1') -Force
    [string[]]$roots = @(
        [IO.Path]::GetFullPath($ArchiveRoot),
        [IO.Path]::GetFullPath($FixtureRoot))
    $snapshot = @(Get-OpenVinoTrustedInputSnapshot -Roots $roots)
    $lease = New-OpenVinoTrustedInputLease -Snapshot $snapshot

    $file = @($snapshot | Where-Object { -not $_.IsDirectory })[0].FullPath
    $addProbe = Join-Path $roots[0] 'lease-add-probe.tmp'
    $renameProbe = $file + '.lease-rename-probe'
    Assert-Denied 'write' { [IO.File]::OpenWrite($file).Dispose() }
    Assert-Denied 'delete' { [IO.File]::Delete($file) }
    $addDenied = $false
    try { [IO.File]::WriteAllText($addProbe, 'probe') } catch { $addDenied = $true }
    if (-not $addDenied) {
        $deadline = [DateTime]::UtcNow.AddSeconds(3)
        while (-not $lease.MutationObserved -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 10
        }
        [IO.File]::Delete($addProbe)
        if (-not $lease.MutationObserved) { throw 'trusted-add-was-not-observed' }
    }
    Assert-Denied 'rename' { [IO.File]::Move($file, $renameProbe) }
    if ($addDenied) {
        Assert-OpenVinoTrustedInputLeaseUnchanged -Lease $lease
    }
    Assert-OpenVinoTrustedInputSnapshot -Roots $roots -Expected $snapshot

    $lease.Dispose()
    $lease = $null
    [IO.File]::WriteAllText($addProbe, 'released')
    [IO.File]::Delete($addProbe)
    [Console]::Out.WriteLine('trusted_input_lease_valid')
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    [Console]::Out.WriteLine('trusted_input_lease_invalid')
    exit 1
}
finally {
    if ($null -ne $lease) { $lease.Dispose() }
}
