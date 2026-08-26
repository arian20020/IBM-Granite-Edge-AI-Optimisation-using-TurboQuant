[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$AllowedPath,
    [Parameter(Mandatory)][string]$WritePathspec,
    [Parameter(ValueFromRemainingArguments)][string[]]$RemainingAllowedPath
)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $WritePathspec) { Remove-Item -LiteralPath $WritePathspec -Force }

function Normalize-GitPath([string]$Path) {
    $value = $Path.Replace('\','/')
    while ($value.StartsWith('./', [StringComparison]::Ordinal)) { $value=$value.Substring(2) }
    $segments=@($value.Split('/'))
    if ([string]::IsNullOrWhiteSpace($value) -or $value.StartsWith('/') -or $value -match '^[A-Za-z]:' -or
        $value.Contains([char]0) -or $value -match '[\x00-\x1f\x7f]' -or @($segments | Where-Object { $_ -eq '.' -or $_ -eq '..' -or $_ -eq '' }).Count) { throw 'A repository-relative non-traversing path is required.' }
    return $value
}

function Test-Allowed([string]$Path) {
    foreach ($ruleValue in $script:rules) {
        if ($ruleValue.EndsWith('/**', [StringComparison]::Ordinal)) {
            $prefix = $ruleValue.Substring(0, $ruleValue.Length - 3).TrimEnd('/')
            if ($Path -eq $prefix -or $Path.StartsWith($prefix + '/', [StringComparison]::Ordinal)) { return $true }
        } elseif ($Path -ceq $ruleValue) { return $true }
    }
    return $false
}

$repositoryRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Repository).Path).TrimEnd('\','/')
$gitRoot = [IO.Path]::GetFullPath(((git -C $repositoryRoot rev-parse --show-toplevel) -replace '/', [IO.Path]::DirectorySeparatorChar)).TrimEnd('\','/')
if ($LASTEXITCODE -or -not [StringComparer]::OrdinalIgnoreCase.Equals($repositoryRoot, $gitRoot)) { throw 'Repository must be an exact Git worktree root.' }
$allAllowedPaths=@($AllowedPath)+@($RemainingAllowedPath)
$script:rules = @($allAllowedPaths | ForEach-Object { Normalize-GitPath $_ })
foreach ($rule in $script:rules) { if ($rule.Contains('*') -and -not $rule.EndsWith('/**',[StringComparison]::Ordinal)) { throw 'Only a trailing /** owned prefix is supported.' } }

$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = 'git.exe'; $start.WorkingDirectory = $repositoryRoot; $start.UseShellExecute = $false
$start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
$start.Arguments = 'status --porcelain=v1 -z --untracked-files=all'
$process = [Diagnostics.Process]::Start($start)
$memory = New-Object IO.MemoryStream
$process.StandardOutput.BaseStream.CopyTo($memory); $errorText = $process.StandardError.ReadToEnd(); $process.WaitForExit()
if ($process.ExitCode) { throw 'Unable to inspect repository changes.' }
$rawBytes = $memory.ToArray()
$records = New-Object 'Collections.Generic.List[string]'; $strictUtf8=New-Object Text.UTF8Encoding($false,$true)
$recordStart = 0
for ($byteIndex = 0; $byteIndex -lt $rawBytes.Length; $byteIndex++) {
    if ($rawBytes[$byteIndex] -eq 0) {
        if ($byteIndex -gt $recordStart) { $records.Add($strictUtf8.GetString($rawBytes, $recordStart, $byteIndex - $recordStart)) }
        $recordStart = $byteIndex + 1
    }
}
if ($recordStart -ne $rawBytes.Length) { throw 'Git porcelain output was not NUL terminated.' }
$changed = New-Object 'Collections.Generic.List[string]'
for ($index = 0; $index -lt $records.Count; $index++) {
    $record = $records[$index]
    if ($record.Length -lt 4 -or $record[2] -ne ' ') { throw 'Unexpected Git porcelain record.' }
    $status = $record.Substring(0,2); $path = Normalize-GitPath $record.Substring(3)
    $paths = @($path)
    if ($status.Contains('R') -or $status.Contains('C')) {
        if (++$index -ge $records.Count) { throw 'Incomplete rename/copy record.' }
        $paths += Normalize-GitPath $records[$index]
    }
    foreach ($candidate in $paths) {
        if (-not (Test-Allowed $candidate)) { throw "Changed path is outside the allowlist: $candidate" }
        if (-not $changed.Contains($candidate)) { $changed.Add($candidate) }
    }
}

$parent = Split-Path -Parent ([IO.Path]::GetFullPath($WritePathspec))
if ($parent -and -not (Test-Path -LiteralPath $parent)) { [IO.Directory]::CreateDirectory($parent) | Out-Null }
$bytes = New-Object 'Collections.Generic.List[byte]'
foreach ($path in $changed) { $bytes.AddRange([Text.Encoding]::UTF8.GetBytes($path)); $bytes.Add(0) }
[IO.File]::WriteAllBytes($WritePathspec, $bytes.ToArray())
Write-Output "Allowed changed paths: $($changed.Count)"
