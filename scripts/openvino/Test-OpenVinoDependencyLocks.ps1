[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ClosureDirectory,

    [ValidateSet('Official', 'Converter', 'All')]
    [string]$Scope = 'All'
)

$ErrorActionPreference = 'Stop'

function Write-SupportCode {
    param(
        [Parameter(Mandatory)]
        [string]$Code,
        [int]$ExitCode = 1
    )

    [Console]::Out.WriteLine($Code)
    exit $ExitCode
}

function Get-RepositoryRoot {
    $root = [System.IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot '..\..'))
    if (-not (Test-Path -LiteralPath (Join-Path $root 'global.json'))) {
        throw 'repository-root-not-found'
    }

    return $root
}

function Get-Lock {
    param([string]$Path)

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Test-ReviewRecords {
    param($Reviews)

    $required = @('DEP-01', 'DEP-02', 'LIC-01')
    foreach ($gate in $required) {
        $review = @($Reviews | Where-Object { $_.gate -eq $gate })
        if ($review.Count -ne 1 -or
            [string]::IsNullOrWhiteSpace($review[0].reviewer) -or
            [string]::IsNullOrWhiteSpace($review[0].reviewedAtUtc) -or
            [string]::IsNullOrWhiteSpace($review[0].disposition) -or
            $review[0].reviewer -eq 'TBD' -or
            $review[0].reviewedAtUtc -eq 'TBD' -or
            $review[0].disposition -eq 'TBD') {
            return $false
        }

        $ignored = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse($review[0].reviewedAtUtc, [ref]$ignored)) {
            return $false
        }
    }

    return $true
}

function Test-ArchiveInventory {
    param(
        [string]$ArchivePath,
        $ExpectedFiles
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $actual = @($archive.Entries |
            ForEach-Object { "$($_.FullName)`u001f$($_.Length)" } |
            Sort-Object)
    }
    finally {
        $archive.Dispose()
    }

    $expected = @($ExpectedFiles |
        ForEach-Object { "$($_.path)`u001f$($_.length)" } |
        Sort-Object)

    if ($actual.Count -ne $expected.Count) {
        return $false
    }

    for ($index = 0; $index -lt $actual.Count; $index++) {
        if ($actual[$index] -cne $expected[$index]) {
            return $false
        }
    }

    return $true
}

function Test-OfficialClosure {
    param(
        [string]$RepositoryRoot,
        [string]$Directory
    )

    $lockDirectory = Join-Path $RepositoryRoot 'third-party\openvino-official'
    $lockPaths = @(
        'openvino-runtime.lock.json',
        'openvino-genai.lock.json',
        'openvino-tokenizers.lock.json'
    ) | ForEach-Object { Join-Path $lockDirectory $_ }
    $locks = @($lockPaths | ForEach-Object { Get-Lock $_ })

    $expectedNames = @($locks | ForEach-Object { $_.archive.fileName } | Sort-Object)
    $actualFiles = @(Get-ChildItem -LiteralPath $Directory -File)
    $actualNames = @($actualFiles | ForEach-Object { $_.Name } | Sort-Object)
    if ($actualNames.Count -ne $expectedNames.Count) {
        return $false
    }

    for ($index = 0; $index -lt $expectedNames.Count; $index++) {
        if ($actualNames[$index] -cne $expectedNames[$index]) {
            return $false
        }
    }

    foreach ($lock in $locks) {
        if ($lock.release.tag -match '(?i)(latest|main|master)' -or
            $lock.release.commit -notmatch '^[0-9a-f]{40}$' -or
            $lock.archive.sha256 -notmatch '^[0-9a-f]{64}$' -or
            [Int64]$lock.archive.length -le 0 -or
            @($lock.files).Count -eq 0 -or
            @($lock.licenses).Count -eq 0 -or
            -not (Test-ReviewRecords $lock.reviews)) {
            return $false
        }

        $archivePath = Join-Path $Directory $lock.archive.fileName
        $archive = Get-Item -LiteralPath $archivePath
        if ($archive.Length -ne [Int64]$lock.archive.length -or
            (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $lock.archive.sha256 -or
            -not (Test-ArchiveInventory $archivePath $lock.files)) {
            return $false
        }
    }

    return $true
}

function Test-ConverterClosure {
    param(
        [string]$RepositoryRoot,
        [string]$Directory
    )

    $lockDirectory = Join-Path $RepositoryRoot 'third-party\openvino-converter'
    $manifest = Get-Lock (Join-Path $lockDirectory 'wheel-manifest.json')
    if ($manifest.closureStatus -ne 'resolved') {
        Write-SupportCode 'converter_closure_unresolved'
    }

    if (-not (Test-ReviewRecords $manifest.reviews)) {
        return $false
    }

    $runtime = Get-Lock (Join-Path $lockDirectory 'python-runtime.lock.json')
    $expected = @($runtime.filename) + @($manifest.wheels | ForEach-Object { $_.filename })
    $actual = @(Get-ChildItem -LiteralPath $Directory -File)
    if (@($actual | Where-Object { $_.Extension -ne '.whl' -and $_.Name -ne $runtime.filename }).Count -ne 0 -or
        $actual.Count -ne $expected.Count) {
        return $false
    }

    foreach ($name in $expected) {
        if (@($actual | Where-Object { $_.Name -ceq $name }).Count -ne 1) {
            return $false
        }
    }

    foreach ($wheel in $manifest.wheels) {
        $file = Get-Item -LiteralPath (Join-Path $Directory $wheel.filename)
        if ($file.Length -ne [Int64]$wheel.length -or
            $wheel.sha256 -notmatch '^[0-9a-f]{64}$' -or
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -cne $wheel.sha256) {
            return $false
        }
    }

    return $true
}

try {
    $root = Get-RepositoryRoot
    if (-not (Test-Path -LiteralPath $ClosureDirectory -PathType Container)) {
        Write-SupportCode 'runtime_integrity_failed'
    }

    if ($Scope -eq 'Official' -or $Scope -eq 'All') {
        if (-not (Test-OfficialClosure $root $ClosureDirectory)) {
            Write-SupportCode 'runtime_integrity_failed'
        }
    }

    if ($Scope -eq 'Converter' -or $Scope -eq 'All') {
        if (-not (Test-ConverterClosure $root $ClosureDirectory)) {
            Write-SupportCode 'runtime_integrity_failed'
        }
    }

    Write-SupportCode 'dependency_lock_valid' 0
}
catch {
    Write-SupportCode 'runtime_integrity_failed'
}
