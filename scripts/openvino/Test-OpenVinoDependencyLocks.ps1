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
    $sentinels = @(
        'tbd',
        'n/a',
        'na',
        'unverified',
        'unknown',
        'none',
        'null',
        'pending',
        'unset',
        '-'
    )
    foreach ($gate in $required) {
        $review = @($Reviews | Where-Object { $_.gate -eq $gate })
        $reviewer = [string]$review[0].reviewer
        $reviewedAtUtc = [string]$review[0].reviewedAtUtc
        $disposition = [string]$review[0].disposition
        if ($review.Count -ne 1 -or
            [string]::IsNullOrWhiteSpace($reviewer) -or
            [string]::IsNullOrWhiteSpace($reviewedAtUtc) -or
            [string]::IsNullOrWhiteSpace($disposition) -or
            $sentinels -contains $reviewer.Trim().ToLowerInvariant() -or
            $sentinels -contains $reviewedAtUtc.Trim().ToLowerInvariant() -or
            $sentinels -contains $disposition.Trim().ToLowerInvariant()) {
            return $false
        }

        $ignored = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse($reviewedAtUtc, [ref]$ignored)) {
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

    $wheels = @($manifest.wheels)
    if ($wheels.Count -eq 0) {
        return $false
    }
    $requirementLines = @(
        Get-Content -LiteralPath (Join-Path $lockDirectory 'requirements.lock') |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -and -not $_.StartsWith('#', [StringComparison]::Ordinal) }
    )
    if ($requirementLines.Count -ne $wheels.Count) {
        return $false
    }
    $wheelNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $packageNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($wheel in $wheels) {
        $sourceUri = $null
        $requirement = '{0}=={1} --hash=sha256:{2}' -f `
            $wheel.package, $wheel.version, $wheel.sha256
        if ([string]$wheel.filename -cnotmatch '^[^\\/:*?"<>|]+\.whl$' -or
            [string]$wheel.package -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' -or
            [string]::IsNullOrWhiteSpace([string]$wheel.version) -or
            [Int64]$wheel.length -le 0 -or
            [string]$wheel.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            [string]$wheel.pythonAbi -notin @('cp313', 'abi3', 'py3') -or
            [string]$wheel.platform -notin @('win_amd64', 'any') -or
            [string]::IsNullOrWhiteSpace([string]$wheel.license) -or
            [string]$wheel.redistributionDisposition -cne 'include-wheel-license-files' -or
            -not [Uri]::TryCreate([string]$wheel.sourceUrl, [UriKind]::Absolute, [ref]$sourceUri) -or
            $sourceUri.Scheme -cne 'https' -or
            -not $wheelNames.Add([string]$wheel.filename) -or
            -not $packageNames.Add([string]$wheel.package) -or
            @($requirementLines | Where-Object { $_ -ceq $requirement }).Count -ne 1) {
            return $false
        }
    }

    $runtime = Get-Lock (Join-Path $lockDirectory 'python-runtime.lock.json')
    $runtimePath = Join-Path $Directory $runtime.filename
    $runtimeFile = Get-Item -LiteralPath $runtimePath
    if ([Int64]$runtime.length -le 0 -or
        $runtime.sha256 -notmatch '^[0-9a-f]{64}$' -or
        -not (Test-ReviewRecords $runtime.reviews) -or
        $runtimeFile.Length -ne [Int64]$runtime.length -or
        (Get-FileHash -LiteralPath $runtimePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $runtime.sha256) {
        return $false
    }

    $expected = @($runtime.filename) + @($wheels | ForEach-Object { $_.filename })
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

    foreach ($wheel in $wheels) {
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
