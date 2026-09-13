[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Path,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{64}$')]
    [string]$ExpectedSha256,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SuccessMessage,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FailureMessage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$fullPath = [IO.Path]::GetFullPath($Path)
if (-not [IO.File]::Exists($fullPath)) {
    throw 'The file required for SHA-256 verification is absent.'
}

$item = [IO.FileInfo]::new($fullPath)
if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The file required for SHA-256 verification is a reparse point.'
}

$stream = [IO.File]::Open(
    $fullPath,
    [IO.FileMode]::Open,
    [IO.FileAccess]::Read,
    [IO.FileShare]::Read)
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $actual = [BitConverter]::ToString(
        $sha256.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
}
finally {
    $sha256.Dispose()
    $stream.Dispose()
}

if ($actual -cne $ExpectedSha256) {
    [Console]::Out.WriteLine($FailureMessage)
    exit 1
}

[Console]::Out.WriteLine($SuccessMessage)
