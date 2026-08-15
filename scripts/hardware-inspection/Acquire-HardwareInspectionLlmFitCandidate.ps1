[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

function Test-ReparsePoint {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $attributes = [System.IO.File]::GetAttributes($LiteralPath)
    return (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
}

function Get-DirectoryPrefix {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $separators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    return $LiteralPath.TrimEnd($separators) + [System.IO.Path]::DirectorySeparatorChar
}

function Test-CanonicalContainment {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Candidate
    )

    $canonicalRoot = [System.IO.Path]::GetFullPath($Root)
    $canonicalCandidate = [System.IO.Path]::GetFullPath($Candidate)
    return $canonicalCandidate.StartsWith(
        (Get-DirectoryPrefix -LiteralPath $canonicalRoot),
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-RepositoryRoot {
    param([Parameter(Mandatory = $true)][string] $ExpectedRoot)

    if (-not [System.IO.Directory]::Exists($ExpectedRoot)) {
        throw 'The validated repository root no longer exists.'
    }

    $rootItem = Get-Item -LiteralPath $ExpectedRoot -Force
    $canonicalRoot = [System.IO.Path]::GetFullPath($rootItem.FullName)
    if (-not [string]::Equals($canonicalRoot, $ExpectedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $rootItem.PSIsContainer -or
        (Test-ReparsePoint -LiteralPath $canonicalRoot)) {
        throw 'The repository root identity is no longer valid.'
    }
}

function Assert-OwnedOsTempPath {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if (-not [System.IO.Directory]::Exists($tempRoot) -or (Test-ReparsePoint -LiteralPath $tempRoot)) {
        throw 'The operating-system temporary root is unavailable or unsafe.'
    }

    $canonicalPath = [System.IO.Path]::GetFullPath($LiteralPath)
    $leafName = [System.IO.Path]::GetFileName($canonicalPath)
    if (-not (Test-CanonicalContainment -Root $tempRoot -Candidate $canonicalPath) -or
        (-not $leafName.StartsWith('hardware-inspection-llmfit-download-', [System.StringComparison]::Ordinal) -and
            -not $leafName.StartsWith('hardware-inspection-llmfit-package-', [System.StringComparison]::Ordinal))) {
        throw 'The temporary path is not an owned LLM Fit acquisition directory.'
    }
}

function New-OwnedOsTempDirectory {
    param([Parameter(Mandatory = $true)][string] $Prefix)

    if ($Prefix -ne 'hardware-inspection-llmfit-download-' -and
        $Prefix -ne 'hardware-inspection-llmfit-package-') {
        throw 'The temporary-directory prefix is not approved.'
    }

    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $candidate = [System.IO.Path]::GetFullPath(
        [System.IO.Path]::Combine($tempRoot, $Prefix + [System.Guid]::NewGuid().ToString('N')))
    Assert-OwnedOsTempPath -LiteralPath $candidate
    if ([System.IO.Directory]::Exists($candidate) -or [System.IO.File]::Exists($candidate)) {
        throw 'The generated temporary directory already exists.'
    }

    [void][System.IO.Directory]::CreateDirectory($candidate)
    if (Test-ReparsePoint -LiteralPath $candidate) {
        throw 'The generated temporary directory is a reparse point.'
    }

    return $candidate
}

function Remove-OwnedOsTempDirectory {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    Assert-OwnedOsTempPath -LiteralPath $LiteralPath
    if ([System.IO.File]::Exists($LiteralPath)) {
        throw 'An owned temporary directory path was replaced by a file.'
    }

    if ([System.IO.Directory]::Exists($LiteralPath)) {
        if (Test-ReparsePoint -LiteralPath $LiteralPath) {
            throw 'An owned temporary directory path was replaced by a reparse point.'
        }

        Remove-Item -LiteralPath $LiteralPath -Recurse -Force
    }
}

function Invoke-OwnedTempCleanup {
    param([Parameter(Mandatory = $true)][AllowNull()][string[]] $Paths)

    $cleanupErrors = New-Object System.Collections.Generic.List[System.Exception]
    foreach ($path in $Paths) {
        if ($null -eq $path) {
            continue
        }

        try {
            Remove-OwnedOsTempDirectory -LiteralPath $path
        }
        catch {
            $cleanupErrors.Add($_.Exception)
        }
    }

    if ($cleanupErrors.Count -ne 0) {
        throw (New-Object System.AggregateException(
            'One or more owned temporary directories could not be cleaned.',
            $cleanupErrors.ToArray()))
    }
}

function Assert-NoExistingReparseSegments {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string[]] $Segments
    )

    $current = [System.IO.Path]::GetFullPath($Root)
    foreach ($segment in $Segments) {
        $current = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($current, $segment))
        if (-not (Test-CanonicalContainment -Root $Root -Candidate $current)) {
            throw 'The fixed destination chain escaped the repository root.'
        }

        if (([System.IO.Directory]::Exists($current) -or [System.IO.File]::Exists($current)) -and
            (Test-ReparsePoint -LiteralPath $current)) {
            throw 'The fixed LLM Fit destination chain contains a reparse point.'
        }
    }
}

function Assert-PinnedManifest {
    param([Parameter(Mandatory = $true)] $Manifest)

    if ($Manifest.schemaVersion -cne '1.0' -or
        $Manifest.candidateId -cne 'llmfit-v1.1.9-win-x64' -or
        $Manifest.version -cne '1.1.9' -or
        $Manifest.releaseTag -cne 'v1.1.9' -or
        $Manifest.releaseCommit -cne 'a02e13f1013ed69889ff44426a651bf7c68c292e' -or
        [string]$Manifest.publishedAtUtc -cne '2026-08-09T17:07:55Z' -or
        $Manifest.archive.fileName -cne 'llmfit-v1.1.9-x86_64-pc-windows-msvc.zip' -or
        $Manifest.archive.downloadUri -cne 'https://github.com/AlexsJones/llmfit/releases/download/v1.1.9/llmfit-v1.1.9-x86_64-pc-windows-msvc.zip' -or
        [long]$Manifest.archive.lengthBytes -ne 5255910 -or
        $Manifest.archive.sha256 -cne 'a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738' -or
        $Manifest.executable.relativePath -cne 'llmfit.exe' -or
        $Manifest.executable.sha256 -cne 'db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19' -or
        $Manifest.executable.peMachine -cne 'AMD64' -or
        $Manifest.executable.authenticodePolicy -cne 'ObserveAndRecord' -or
        $Manifest.license.spdx -cne 'MIT' -or
        $Manifest.license.relativePath -cne 'LICENSE') {
        throw 'The committed LLM Fit candidate manifest does not match the exact pinned contract.'
    }

    $requiredFiles = @($Manifest.requiredFiles)
    $expectedRequiredFiles = @('llmfit.exe', 'LICENSE', 'README.md')
    $versionCommand = @($Manifest.commands.version)
    $systemCommand = @($Manifest.commands.system)
    if ($requiredFiles.Count -ne 3 -or
        $requiredFiles[0] -cne $expectedRequiredFiles[0] -or
        $requiredFiles[1] -cne $expectedRequiredFiles[1] -or
        $requiredFiles[2] -cne $expectedRequiredFiles[2] -or
        $versionCommand.Count -ne 1 -or
        $versionCommand[0] -cne '--version' -or
        $systemCommand.Count -ne 3 -or
        $systemCommand[0] -cne '--no-dashboard' -or
        $systemCommand[1] -cne '--json' -or
        $systemCommand[2] -cne 'system') {
        throw 'The committed manifest lists or commands do not match the exact pinned contract.'
    }
}

function Get-Sha256LowerFromStream {
    param([Parameter(Mandatory = $true)][System.IO.Stream] $Stream)

    if (-not $Stream.CanRead -or -not $Stream.CanSeek) {
        throw 'A stable readable and seekable stream is required for SHA-256.'
    }

    $Stream.Position = 0
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash($Stream)
    }
    finally {
        $sha256.Dispose()
    }

    return (($hashBytes | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Assert-PeAmd64FromStream {
    param([Parameter(Mandatory = $true)][System.IO.Stream] $Stream)

    if (-not $Stream.CanRead -or -not $Stream.CanSeek -or $Stream.Length -lt 0x40) {
        throw 'The candidate executable is too short to contain a PE header.'
    }

    $Stream.Position = 0
    $reader = New-Object System.IO.BinaryReader($Stream, [System.Text.Encoding]::UTF8, $true)
    try {
        if ($reader.ReadByte() -ne [byte][char]'M' -or $reader.ReadByte() -ne [byte][char]'Z') {
            throw 'The candidate executable does not contain an MZ header.'
        }

        $Stream.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        if ($peOffset -le 0 -or $peOffset -gt ($Stream.Length - 6)) {
            throw 'The candidate executable contains an invalid PE header offset.'
        }

        $Stream.Position = $peOffset
        $signature = $reader.ReadBytes(4)
        if ($signature.Length -ne 4 -or
            $signature[0] -ne [byte][char]'P' -or
            $signature[1] -ne [byte][char]'E' -or
            $signature[2] -ne 0 -or
            $signature[3] -ne 0) {
            throw 'The candidate executable contains an invalid PE signature.'
        }

        if ($reader.ReadUInt16() -ne 0x8664) {
            throw 'The candidate executable is not an AMD64 PE image.'
        }
    }
    finally {
        $reader.Dispose()
    }
}

function Test-ZipEntryIsLinkLike {
    param([Parameter(Mandatory = $true)][System.IO.Compression.ZipArchiveEntry] $Entry)

    $unixFileType = (($Entry.ExternalAttributes -shr 16) -band 0xF000)
    $dosReparse = ($Entry.ExternalAttributes -band [int][System.IO.FileAttributes]::ReparsePoint)
    return ($unixFileType -eq 0xA000 -or $dosReparse -ne 0)
}

function Copy-StreamToNewFile {
    param(
        [Parameter(Mandatory = $true)][System.IO.Stream] $SourceStream,
        [Parameter(Mandatory = $true)][string] $DestinationPath
    )

    $SourceStream.Position = 0
    $outputStream = [System.IO.File]::Open(
        $DestinationPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    try {
        $SourceStream.CopyTo($outputStream)
        $outputStream.Flush()
    }
    finally {
        $outputStream.Dispose()
    }
}

function Copy-ValidatedArchiveMembers {
    param(
        [Parameter(Mandatory = $true)][System.IO.Stream] $ArchiveStream,
        [Parameter(Mandatory = $true)][string] $PackagePath,
        [Parameter(Mandatory = $true)][string] $ExpectedReleaseFolder
    )

    $expectedMembers = @('llmfit.exe', 'LICENSE', 'README.md')
    $acceptedEntries = @{}
    $seenEntryNames = @{}
    $ArchiveStream.Position = 0
    $zip = New-Object System.IO.Compression.ZipArchive(
        $ArchiveStream,
        [System.IO.Compression.ZipArchiveMode]::Read,
        $true)
    try {
        foreach ($entry in $zip.Entries) {
            $entryName = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($entryName) -or
                [System.IO.Path]::IsPathRooted($entryName) -or
                $entryName -match '^[A-Za-z]:' -or
                $entryName.Contains('\') -or
                (Test-ZipEntryIsLinkLike -Entry $entry)) {
                throw 'The archive contains an unsafe entry.'
            }

            if ($seenEntryNames.ContainsKey($entryName)) {
                throw 'The archive contains duplicate or case-colliding entry names.'
            }

            $seenEntryNames.Add($entryName, $true)
            $segments = @($entryName.Split('/'))
            $isDirectory = [string]::IsNullOrEmpty($entry.Name)
            $segmentsToCheck = $segments
            if ($isDirectory -and $segments.Count -gt 0 -and $segments[$segments.Count - 1] -eq '') {
                $segmentsToCheck = @($segments[0..($segments.Count - 2)])
            }

            foreach ($segment in $segmentsToCheck) {
                if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..') {
                    throw 'The archive contains a traversal-like entry.'
                }
            }

            if ($isDirectory) {
                if ($entryName -cne ($ExpectedReleaseFolder + '/')) {
                    throw 'The archive contains an unexpected directory layout.'
                }

                continue
            }

            if ($segments.Count -ne 2 -or $segments[0] -cne $ExpectedReleaseFolder) {
                throw 'The archive contains a file outside the expected release folder.'
            }

            $leafName = $segments[1]
            if ($expectedMembers -cnotcontains $leafName -or $acceptedEntries.ContainsKey($leafName)) {
                throw 'The archive contains an unexpected, duplicate, or case-colliding member.'
            }

            $acceptedEntries.Add($leafName, $entry)
        }

        if ($acceptedEntries.Count -ne $expectedMembers.Count) {
            throw 'The archive does not contain exactly the required members.'
        }

        foreach ($requiredMember in $expectedMembers) {
            if (-not $acceptedEntries.ContainsKey($requiredMember)) {
                throw 'The archive does not contain every required member.'
            }

            $destinationFile = [System.IO.Path]::Combine($PackagePath, $requiredMember)
            $inputStream = $acceptedEntries[$requiredMember].Open()
            try {
                Copy-StreamToNewFile -SourceStream $inputStream -DestinationPath $destinationFile
            }
            finally {
                $inputStream.Dispose()
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Get-FreshAuthenticodeObservation {
    param([Parameter(Mandatory = $true)][string] $ExecutablePath)

    $signature = Get-AuthenticodeSignature -LiteralPath $ExecutablePath
    $signaturePresent = ($null -ne $signature.SignerCertificate)
    $signerSubject = $null
    $signerThumbprint = $null
    if ($signaturePresent) {
        $signerSubject = [string]$signature.SignerCertificate.Subject
        $signerThumbprint = [string]$signature.SignerCertificate.Thumbprint
    }

    $rawStatus = $signature.Status.ToString()
    if ((-not $signaturePresent -and $rawStatus -ne 'NotSigned') -or
        ($signaturePresent -and
            ($rawStatus -eq 'NotSigned' -or
                [string]::IsNullOrWhiteSpace($signerSubject) -or
                $signerThumbprint -notmatch '^[0-9A-Fa-f]{40}$'))) {
        throw 'The Authenticode status and signer-certificate observation are inconsistent.'
    }

    return [pscustomobject]@{
        RawStatus = $rawStatus
        SignaturePresent = $signaturePresent
        SignerSubject = $signerSubject
        SignerThumbprint = $signerThumbprint
    }
}

function Test-PathLikeObservationValue {
    param([Parameter(Mandatory = $true)][string] $Value)

    return $Value.Contains('..') -or
        $Value.Contains('/') -or
        $Value.Contains('\') -or
        $Value -match '^[A-Za-z]:' -or
        [System.IO.Path]::IsPathRooted($Value)
}

function Read-StrictObservation {
    param(
        [Parameter(Mandatory = $true)][System.IO.Stream] $ObservationStream,
        [Parameter(Mandatory = $true)][string] $ExecutableHash,
        [Parameter(Mandatory = $true)] $FreshAuthenticode
    )

    if (-not $ObservationStream.CanRead -or -not $ObservationStream.CanSeek -or
        $ObservationStream.Length -le 0 -or $ObservationStream.Length -gt 16384) {
        throw 'The Authenticode observation has an invalid byte length.'
    }

    $ObservationStream.Position = 0
    $bytes = New-Object byte[] ([int]$ObservationStream.Length)
    $offset = 0
    while ($offset -lt $bytes.Length) {
        $read = $ObservationStream.Read($bytes, $offset, $bytes.Length - $offset)
        if ($read -le 0) {
            throw 'The Authenticode observation ended unexpectedly.'
        }

        $offset += $read
    }

    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw 'The Authenticode observation must be UTF-8 without a byte-order mark.'
    }

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $json = $strictUtf8.GetString($bytes)
    try {
        $parsed = $json | ConvertFrom-Json
    }
    catch {
        throw 'The Authenticode observation is not valid JSON.'
    }

    $allowedProperties = @(
        'executableSha256',
        'rawStatus',
        'signaturePresent',
        'signerSubject',
        'signerThumbprint',
        'checkedAtUtc')
    $properties = @($parsed.PSObject.Properties)
    $propertyTokenCount = ([regex]::Matches($json, '"(?:[^"\\]|\\.)*"\s*:')).Count
    $seenProperties = @{}
    foreach ($property in $properties) {
        if ($allowedProperties -cnotcontains $property.Name -or $seenProperties.ContainsKey($property.Name)) {
            throw 'The Authenticode observation contains an unknown or duplicate property.'
        }

        $seenProperties.Add($property.Name, $true)
    }

    if ($properties.Count -ne 6 -or $propertyTokenCount -ne 6) {
        throw 'The Authenticode observation must contain exactly six unique properties.'
    }

    foreach ($requiredProperty in $allowedProperties) {
        if (-not $seenProperties.ContainsKey($requiredProperty)) {
            throw 'The Authenticode observation is missing a required property.'
        }
    }

    if ($parsed.executableSha256 -isnot [string] -or
        $parsed.executableSha256 -notmatch '^[0-9a-f]{64}$' -or
        -not [string]::Equals($parsed.executableSha256, $ExecutableHash, [System.StringComparison]::Ordinal) -or
        $parsed.rawStatus -isnot [string] -or
        @('UnknownError', 'Valid', 'NotSigned', 'HashMismatch', 'NotTrusted') -cnotcontains $parsed.rawStatus -or
        $parsed.signaturePresent -isnot [bool] -or
        $parsed.checkedAtUtc -isnot [string]) {
        throw 'The Authenticode observation contains an invalid value or type.'
    }

    $checkedAt = [System.DateTimeOffset]::MinValue
    if (-not [System.DateTimeOffset]::TryParseExact(
            $parsed.checkedAtUtc,
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::None,
            [ref]$checkedAt) -or $checkedAt.Offset -ne [System.TimeSpan]::Zero) {
        throw 'The Authenticode observation timestamp is not a valid UTC instant.'
    }

    if ($parsed.signaturePresent) {
        if ($parsed.signerSubject -isnot [string] -or
            [string]::IsNullOrWhiteSpace($parsed.signerSubject) -or
            (Test-PathLikeObservationValue -Value $parsed.signerSubject) -or
            $parsed.signerThumbprint -isnot [string] -or
            $parsed.signerThumbprint -notmatch '^[0-9A-Fa-f]{40}$' -or
            $parsed.rawStatus -eq 'NotSigned') {
            throw 'The signed Authenticode observation is invalid.'
        }
    }
    elseif ($null -ne $parsed.signerSubject -or
        $null -ne $parsed.signerThumbprint -or
        $parsed.rawStatus -ne 'NotSigned') {
        throw 'The unsigned Authenticode observation is invalid.'
    }

    if ($parsed.signaturePresent -ne $FreshAuthenticode.SignaturePresent -or
        $parsed.rawStatus -ne $FreshAuthenticode.RawStatus -or
        -not [string]::Equals(
            [string]$parsed.signerSubject,
            [string]$FreshAuthenticode.SignerSubject,
            [System.StringComparison]::Ordinal) -or
        -not [string]::Equals(
            [string]$parsed.signerThumbprint,
            [string]$FreshAuthenticode.SignerThumbprint,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The Authenticode observation does not agree with fresh local facts.'
    }

    return $parsed
}

function Assert-FlatPackageLayout {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)] $Manifest
    )

    $canonicalPackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
    if (-not [System.IO.Directory]::Exists($canonicalPackageRoot) -or
        (Test-ReparsePoint -LiteralPath $canonicalPackageRoot)) {
        throw 'The package root is missing or is a reparse point.'
    }

    $expectedMembers = @(
        [string]$Manifest.archive.fileName,
        'llmfit.exe',
        'LICENSE',
        'README.md',
        'authenticode-observation.json')
    $entries = @([System.IO.Directory]::EnumerateFileSystemEntries($canonicalPackageRoot))
    if ($entries.Count -ne $expectedMembers.Count) {
        throw 'The package must contain exactly five flat members.'
    }

    $members = @{}
    foreach ($entry in $entries) {
        $canonicalEntry = [System.IO.Path]::GetFullPath($entry)
        $memberName = [System.IO.Path]::GetFileName($canonicalEntry)
        $parent = [System.IO.Path]::GetDirectoryName($canonicalEntry)
        $attributes = [System.IO.File]::GetAttributes($canonicalEntry)
        if (-not (Test-CanonicalContainment -Root $canonicalPackageRoot -Candidate $canonicalEntry) -or
            -not [string]::Equals($parent, $canonicalPackageRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
            ($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            ($attributes -band [System.IO.FileAttributes]::Directory) -ne 0 -or
            $expectedMembers -cnotcontains $memberName -or
            $members.ContainsKey($memberName)) {
            throw 'The package contains an unsafe, unexpected, nested, or case-colliding member.'
        }

        $members.Add($memberName, $canonicalEntry)
    }

    foreach ($expectedMember in $expectedMembers) {
        if (-not $members.ContainsKey($expectedMember)) {
            throw 'The package is missing an expected member.'
        }
    }

    return $members
}

function Close-StableStreams {
    param([Parameter(Mandatory = $true)][hashtable] $Streams)

    $closeErrors = New-Object System.Collections.Generic.List[System.Exception]
    foreach ($stream in @($Streams.Values)) {
        try {
            $stream.Dispose()
        }
        catch {
            $closeErrors.Add($_.Exception)
        }
    }

    if ($closeErrors.Count -ne 0) {
        throw (New-Object System.AggregateException(
            'One or more stable package streams could not be closed.',
            $closeErrors.ToArray()))
    }
}

function Assert-CompletePackage {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)] $Manifest
    )

    $members = Assert-FlatPackageLayout -PackageRoot $PackageRoot -Manifest $Manifest
    $streams = @{}
    try {
        foreach ($memberName in @($members.Keys)) {
            $memberPath = [string]$members[$memberName]
            $stream = [System.IO.File]::Open(
                $memberPath,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::Read)
            $attributes = [System.IO.File]::GetAttributes($memberPath)
            if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                ($attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
                $stream.Dispose()
                throw 'A stable package stream resolved to an unsafe member.'
            }

            $streams.Add($memberName, $stream)
        }

        $archiveStream = $streams[[string]$Manifest.archive.fileName]
        if ($archiveStream.Length -ne [long]$Manifest.archive.lengthBytes -or
            -not [string]::Equals(
                (Get-Sha256LowerFromStream -Stream $archiveStream),
                [string]$Manifest.archive.sha256,
                [System.StringComparison]::Ordinal)) {
            throw 'The complete package archive does not match the pinned identity.'
        }

        $executableStream = $streams['llmfit.exe']
        $executableHash = Get-Sha256LowerFromStream -Stream $executableStream
        if (-not [string]::Equals(
                $executableHash,
                [string]$Manifest.executable.sha256,
                [System.StringComparison]::Ordinal)) {
            throw 'The complete package executable does not match the pinned identity.'
        }

        Assert-PeAmd64FromStream -Stream $executableStream
        $freshAuthenticode = Get-FreshAuthenticodeObservation -ExecutablePath ([string]$members['llmfit.exe'])
        $null = Read-StrictObservation `
            -ObservationStream $streams['authenticode-observation.json'] `
            -ExecutableHash $executableHash `
            -FreshAuthenticode $freshAuthenticode
        $null = Assert-FlatPackageLayout -PackageRoot $PackageRoot -Manifest $Manifest
    }
    finally {
        Close-StableStreams -Streams $streams
    }
}

function Assert-PublicationBoundary {
    param(
        [Parameter(Mandatory = $true)][string] $CanonicalRoot,
        [Parameter(Mandatory = $true)][string] $PackageTemp,
        [Parameter(Mandatory = $true)][string] $Destination
    )

    Assert-RepositoryRoot -ExpectedRoot $CanonicalRoot
    Assert-OwnedOsTempPath -LiteralPath $PackageTemp
    if (-not [System.IO.Directory]::Exists($PackageTemp) -or (Test-ReparsePoint -LiteralPath $PackageTemp)) {
        throw 'The owned package staging root is missing or unsafe.'
    }

    $canonicalPackageTemp = [System.IO.Path]::GetFullPath((Get-Item -LiteralPath $PackageTemp -Force).FullName)
    if (-not [string]::Equals($canonicalPackageTemp, $PackageTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The owned package staging root identity changed.'
    }

    $canonicalDestination = [System.IO.Path]::GetFullPath($Destination)
    if (-not (Test-CanonicalContainment -Root $CanonicalRoot -Candidate $canonicalDestination)) {
        throw 'The pinned destination escaped the validated repository root.'
    }

    Assert-NoExistingReparseSegments -Root $CanonicalRoot -Segments @(
        'third-party',
        'bin',
        'llmfit',
        'v1.1.9',
        'win-x64')
    if ([System.IO.Directory]::Exists($canonicalDestination) -or [System.IO.File]::Exists($canonicalDestination)) {
        throw 'The pinned destination exists immediately before publication.'
    }

    if (-not [string]::Equals(
            [System.IO.Path]::GetPathRoot($canonicalPackageTemp),
            [System.IO.Path]::GetPathRoot($canonicalDestination),
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Atomic placement requires staging and destination to share a volume.'
    }
}

function Assert-PlacedBoundary {
    param(
        [Parameter(Mandatory = $true)][string] $CanonicalRoot,
        [Parameter(Mandatory = $true)][string] $Destination
    )

    Assert-RepositoryRoot -ExpectedRoot $CanonicalRoot
    Assert-NoExistingReparseSegments -Root $CanonicalRoot -Segments @(
        'third-party',
        'bin',
        'llmfit',
        'v1.1.9',
        'win-x64')
    $canonicalDestination = [System.IO.Path]::GetFullPath($Destination)
    if (-not (Test-CanonicalContainment -Root $CanonicalRoot -Candidate $canonicalDestination) -or
        -not [System.IO.Directory]::Exists($canonicalDestination) -or
        (Test-ReparsePoint -LiteralPath $canonicalDestination)) {
        throw 'The placed package is outside the validated root, missing, or a reparse point.'
    }

    $placedItem = Get-Item -LiteralPath $canonicalDestination -Force
    if (-not [string]::Equals(
            [System.IO.Path]::GetFullPath($placedItem.FullName),
            $canonicalDestination,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The placed package identity changed.'
    }
}

function Remove-OwnedPlacedDestination {
    param(
        [Parameter(Mandatory = $true)][string] $CanonicalRoot,
        [Parameter(Mandatory = $true)][string] $Destination,
        [Parameter(Mandatory = $true)] $Manifest
    )

    Assert-PlacedBoundary -CanonicalRoot $CanonicalRoot -Destination $Destination
    $null = Assert-FlatPackageLayout -PackageRoot $Destination -Manifest $Manifest
    Remove-Item -LiteralPath $Destination -Recurse -Force
}

$downloadTemp = $null
$packageTemp = $null
$destinationOwned = $false
try {
    $rootItem = Get-Item -LiteralPath $RepositoryRoot -Force
    if (-not $rootItem.PSIsContainer) {
        throw 'RepositoryRoot must identify a directory.'
    }

    $canonicalRoot = [System.IO.Path]::GetFullPath($rootItem.FullName)
    Assert-RepositoryRoot -ExpectedRoot $canonicalRoot
    $globalJsonPath = [System.IO.Path]::Combine($canonicalRoot, 'global.json')
    $manifestPath = [System.IO.Path]::Combine(
        $canonicalRoot,
        'tools',
        'HardwareInspection.LlmFitSpike',
        'Candidates',
        'llmfit-v1.1.9-win-x64.json')
    if (-not [System.IO.File]::Exists($globalJsonPath) -or -not [System.IO.File]::Exists($manifestPath)) {
        throw 'RepositoryRoot does not contain the required committed repository files.'
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-PinnedManifest -Manifest $manifest
    $destination = [System.IO.Path]::Combine(
        $canonicalRoot,
        'third-party',
        'bin',
        'llmfit',
        'v1.1.9',
        'win-x64')
    if (-not (Test-CanonicalContainment -Root $canonicalRoot -Candidate $destination)) {
        throw 'The pinned destination escaped the repository root.'
    }

    Assert-NoExistingReparseSegments -Root $canonicalRoot -Segments @(
        'third-party',
        'bin',
        'llmfit',
        'v1.1.9',
        'win-x64')
    if ([System.IO.Directory]::Exists($destination) -or [System.IO.File]::Exists($destination)) {
        throw 'The pinned LLM Fit destination already exists; acquisition will not overwrite it.'
    }

    $downloadTemp = New-OwnedOsTempDirectory -Prefix 'hardware-inspection-llmfit-download-'
    $packageTemp = New-OwnedOsTempDirectory -Prefix 'hardware-inspection-llmfit-package-'
    $archivePath = [System.IO.Path]::Combine($downloadTemp, [string]$manifest.archive.fileName)
    Invoke-WebRequest -Uri ([string]$manifest.archive.downloadUri) -OutFile $archivePath -UseBasicParsing

    $archiveStream = [System.IO.File]::Open(
        $archivePath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        if ($archiveStream.Length -ne [long]$manifest.archive.lengthBytes) {
            throw 'The downloaded archive length does not match the pinned manifest.'
        }

        $archiveHash = Get-Sha256LowerFromStream -Stream $archiveStream
        if (-not [string]::Equals(
                $archiveHash,
                [string]$manifest.archive.sha256,
                [System.StringComparison]::Ordinal)) {
            throw 'The downloaded archive SHA-256 does not match the pinned manifest.'
        }

        $releaseFolder = [System.IO.Path]::GetFileNameWithoutExtension([string]$manifest.archive.fileName)
        Copy-ValidatedArchiveMembers `
            -ArchiveStream $archiveStream `
            -PackagePath $packageTemp `
            -ExpectedReleaseFolder $releaseFolder
        Copy-StreamToNewFile `
            -SourceStream $archiveStream `
            -DestinationPath ([System.IO.Path]::Combine($packageTemp, [string]$manifest.archive.fileName))
    }
    finally {
        $archiveStream.Dispose()
    }

    $executablePath = [System.IO.Path]::Combine($packageTemp, [string]$manifest.executable.relativePath)
    $executableStream = [System.IO.File]::Open(
        $executablePath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::Read)
    try {
        $executableHash = Get-Sha256LowerFromStream -Stream $executableStream
        if (-not [string]::Equals(
                $executableHash,
                [string]$manifest.executable.sha256,
                [System.StringComparison]::Ordinal)) {
            throw 'The candidate executable SHA-256 does not match the pinned manifest.'
        }

        Assert-PeAmd64FromStream -Stream $executableStream
        $freshAuthenticode = Get-FreshAuthenticodeObservation -ExecutablePath $executablePath
        $observation = [ordered]@{
            executableSha256 = $executableHash
            rawStatus = $freshAuthenticode.RawStatus
            signaturePresent = $freshAuthenticode.SignaturePresent
            signerSubject = $freshAuthenticode.SignerSubject
            signerThumbprint = $freshAuthenticode.SignerThumbprint
            checkedAtUtc = [System.DateTime]::UtcNow.ToString(
                'o',
                [System.Globalization.CultureInfo]::InvariantCulture)
        }
        $observationPath = [System.IO.Path]::Combine($packageTemp, 'authenticode-observation.json')
        $observationJson = $observation | ConvertTo-Json -Compress
        $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($observationPath, $observationJson, $utf8WithoutBom)
    }
    finally {
        $executableStream.Dispose()
    }

    Assert-CompletePackage -PackageRoot $packageTemp -Manifest $manifest
    $destinationParent = [System.IO.Path]::GetDirectoryName($destination)
    [void][System.IO.Directory]::CreateDirectory($destinationParent)
    Assert-PublicationBoundary `
        -CanonicalRoot $canonicalRoot `
        -PackageTemp $packageTemp `
        -Destination $destination

    [System.IO.Directory]::Move($packageTemp, $destination)
    $packageTemp = $null
    $destinationOwned = $true
    try {
        Assert-PlacedBoundary -CanonicalRoot $canonicalRoot -Destination $destination
        Assert-CompletePackage -PackageRoot $destination -Manifest $manifest
    }
    catch {
        $publicationError = $_.Exception
        if ($destinationOwned) {
            try {
                Remove-OwnedPlacedDestination `
                    -CanonicalRoot $canonicalRoot `
                    -Destination $destination `
                    -Manifest $manifest
                $destinationOwned = $false
            }
            catch {
                throw (New-Object System.AggregateException(
                    'Post-publication validation and safe rollback both failed.',
                    [System.Exception[]]@($publicationError, $_.Exception)))
            }
        }

        throw $publicationError
    }

    Write-Output 'Pinned LLM Fit candidate acquired and verified without execution.'
}
finally {
    Invoke-OwnedTempCleanup -Paths @($downloadTemp, $packageTemp)
}
