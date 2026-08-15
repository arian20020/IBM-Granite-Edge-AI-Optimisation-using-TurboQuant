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

function New-OwnedOsTempDirectory {
    param([Parameter(Mandatory = $true)][string] $Prefix)

    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $candidate = [System.IO.Path]::GetFullPath(
        [System.IO.Path]::Combine($tempRoot, $Prefix + [System.Guid]::NewGuid().ToString('N')))
    $directorySeparators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $tempPrefix = $tempRoot.TrimEnd($directorySeparators) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The generated temporary directory was outside the operating-system temporary root.'
    }

    if ([System.IO.Directory]::Exists($candidate) -or [System.IO.File]::Exists($candidate)) {
        throw 'The generated temporary directory already exists.'
    }

    [void][System.IO.Directory]::CreateDirectory($candidate)
    return $candidate
}

function Get-Sha256Lower {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-NoExistingReparseSegments {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string[]] $Segments
    )

    $current = $Root
    foreach ($segment in $Segments) {
        $current = [System.IO.Path]::Combine($current, $segment)
        if (([System.IO.Directory]::Exists($current) -or [System.IO.File]::Exists($current)) -and
            (Test-ReparsePoint -LiteralPath $current)) {
            throw 'The fixed LLM Fit destination chain contains a reparse point.'
        }
    }
}

function Assert-PinnedManifest {
    param([Parameter(Mandatory = $true)] $Manifest)

    $expectedRequiredFiles = @('llmfit.exe', 'LICENSE', 'README.md')
    if ($Manifest.schemaVersion -ne '1.0' -or
        $Manifest.version -ne '1.1.9' -or
        $Manifest.releaseTag -ne 'v1.1.9' -or
        $Manifest.archive.fileName -ne 'llmfit-v1.1.9-x86_64-pc-windows-msvc.zip' -or
        $Manifest.archive.downloadUri -ne 'https://github.com/AlexsJones/llmfit/releases/download/v1.1.9/llmfit-v1.1.9-x86_64-pc-windows-msvc.zip' -or
        [long]$Manifest.archive.lengthBytes -le 0 -or
        $Manifest.archive.sha256 -notmatch '^[0-9a-f]{64}$' -or
        $Manifest.executable.relativePath -ne 'llmfit.exe' -or
        $Manifest.executable.sha256 -notmatch '^[0-9a-f]{64}$' -or
        $Manifest.executable.peMachine -ne 'AMD64' -or
        $Manifest.executable.authenticodePolicy -ne 'ObserveAndRecord') {
        throw 'The committed LLM Fit candidate manifest does not contain the expected pinned contract.'
    }

    $requiredFiles = @($Manifest.requiredFiles)
    if ($requiredFiles.Count -ne $expectedRequiredFiles.Count) {
        throw 'The committed manifest required-file set is invalid.'
    }

    for ($index = 0; $index -lt $expectedRequiredFiles.Count; $index++) {
        if ($requiredFiles[$index] -cne $expectedRequiredFiles[$index]) {
            throw 'The committed manifest required-file set is invalid.'
        }
    }
}

function Assert-PeAmd64 {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $stream = [System.IO.File]::Open($LiteralPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        if ($stream.Length -lt 0x40) {
            throw 'The candidate executable is too short to contain a PE header.'
        }

        $reader = New-Object System.IO.BinaryReader($stream, [System.Text.Encoding]::UTF8, $true)
        try {
            if ($reader.ReadByte() -ne [byte][char]'M' -or $reader.ReadByte() -ne [byte][char]'Z') {
                throw 'The candidate executable does not contain an MZ header.'
            }

            $stream.Position = 0x3c
            $peOffset = $reader.ReadInt32()
            if ($peOffset -le 0 -or $peOffset -gt ($stream.Length - 6)) {
                throw 'The candidate executable contains an invalid PE header offset.'
            }

            $stream.Position = $peOffset
            $signature = $reader.ReadBytes(4)
            if ($signature.Length -ne 4 -or
                $signature[0] -ne [byte][char]'P' -or
                $signature[1] -ne [byte][char]'E' -or
                $signature[2] -ne 0 -or
                $signature[3] -ne 0) {
                throw 'The candidate executable contains an invalid PE signature.'
            }

            $machine = $reader.ReadUInt16()
            if ($machine -ne 0x8664) {
                throw 'The candidate executable is not an AMD64 PE image.'
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Test-ZipEntryIsLinkLike {
    param([Parameter(Mandatory = $true)][System.IO.Compression.ZipArchiveEntry] $Entry)

    $unixFileType = (($Entry.ExternalAttributes -shr 16) -band 0xF000)
    $dosReparse = ($Entry.ExternalAttributes -band [int][System.IO.FileAttributes]::ReparsePoint)
    return ($unixFileType -eq 0xA000 -or $dosReparse -ne 0)
}

function Copy-ValidatedArchiveMembers {
    param(
        [Parameter(Mandatory = $true)][string] $ArchivePath,
        [Parameter(Mandatory = $true)][string] $PackagePath,
        [Parameter(Mandatory = $true)][string] $ExpectedReleaseFolder
    )

    $expectedMembers = @('llmfit.exe', 'LICENSE', 'README.md')
    $acceptedEntries = @{}
    $seenEntryNames = @{}
    $archiveStream = [System.IO.File]::Open($ArchivePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $zip = New-Object System.IO.Compression.ZipArchive(
            $archiveStream,
            [System.IO.Compression.ZipArchiveMode]::Read,
            $false)
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
                if ($expectedMembers -cnotcontains $leafName) {
                    throw 'The archive contains an unexpected member.'
                }

                if ($acceptedEntries.ContainsKey($leafName)) {
                    throw 'The archive contains duplicate or case-colliding members.'
                }

                $acceptedEntries.Add($leafName, $entry)
            }

            foreach ($requiredMember in $expectedMembers) {
                if (-not $acceptedEntries.ContainsKey($requiredMember)) {
                    throw 'The archive does not contain every required member.'
                }

                $destinationFile = [System.IO.Path]::Combine($PackagePath, $requiredMember)
                $inputStream = $acceptedEntries[$requiredMember].Open()
                try {
                    $outputStream = [System.IO.File]::Open(
                        $destinationFile,
                        [System.IO.FileMode]::CreateNew,
                        [System.IO.FileAccess]::Write,
                        [System.IO.FileShare]::None)
                    try {
                        $inputStream.CopyTo($outputStream)
                    }
                    finally {
                        $outputStream.Dispose()
                    }
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
    finally {
        $archiveStream.Dispose()
    }
}

$downloadTemp = $null
$packageTemp = $null
try {
    $rootItem = Get-Item -LiteralPath $RepositoryRoot -Force
    if (-not $rootItem.PSIsContainer) {
        throw 'RepositoryRoot must identify a directory.'
    }

    $canonicalRoot = [System.IO.Path]::GetFullPath($rootItem.FullName)
    if (Test-ReparsePoint -LiteralPath $canonicalRoot) {
        throw 'RepositoryRoot must not be a reparse point.'
    }

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
    if ([System.IO.Path]::GetPathRoot($packageTemp) -ne [System.IO.Path]::GetPathRoot($destination)) {
        throw 'Atomic placement requires the operating-system temporary directory and repository to share a volume.'
    }

    $archivePath = [System.IO.Path]::Combine($downloadTemp, [string]$manifest.archive.fileName)
    Invoke-WebRequest -Uri ([string]$manifest.archive.downloadUri) -OutFile $archivePath -UseBasicParsing

    $archiveInfo = Get-Item -LiteralPath $archivePath
    if ($archiveInfo.Length -ne [long]$manifest.archive.lengthBytes) {
        throw 'The downloaded archive length does not match the pinned manifest.'
    }

    $archiveHash = Get-Sha256Lower -LiteralPath $archivePath
    if (-not [string]::Equals($archiveHash, [string]$manifest.archive.sha256, [System.StringComparison]::Ordinal)) {
        throw 'The downloaded archive SHA-256 does not match the pinned manifest.'
    }

    $releaseFolder = [System.IO.Path]::GetFileNameWithoutExtension([string]$manifest.archive.fileName)
    Copy-ValidatedArchiveMembers -ArchivePath $archivePath -PackagePath $packageTemp -ExpectedReleaseFolder $releaseFolder

    foreach ($requiredFile in @($manifest.requiredFiles)) {
        $requiredPath = [System.IO.Path]::Combine($packageTemp, [string]$requiredFile)
        if (-not [System.IO.File]::Exists($requiredPath) -or (Test-ReparsePoint -LiteralPath $requiredPath)) {
            throw 'A required flattened package file is missing or unsafe.'
        }
    }

    $executablePath = [System.IO.Path]::Combine($packageTemp, [string]$manifest.executable.relativePath)
    $executableHash = Get-Sha256Lower -LiteralPath $executablePath
    if (-not [string]::Equals($executableHash, [string]$manifest.executable.sha256, [System.StringComparison]::Ordinal)) {
        throw 'The candidate executable SHA-256 does not match the pinned manifest.'
    }

    Assert-PeAmd64 -LiteralPath $executablePath

    $signature = Get-AuthenticodeSignature -LiteralPath $executablePath
    $signaturePresent = ($null -ne $signature.SignerCertificate)
    $signerSubject = $null
    $signerThumbprint = $null
    if ($signaturePresent) {
        $signerSubject = [string]$signature.SignerCertificate.Subject
        $signerThumbprint = [string]$signature.SignerCertificate.Thumbprint
    }

    $rawSignatureStatus = $signature.Status.ToString()
    if ((-not $signaturePresent -and $rawSignatureStatus -ne 'NotSigned') -or
        ($signaturePresent -and
            ($rawSignatureStatus -eq 'NotSigned' -or
                [string]::IsNullOrWhiteSpace($signerSubject) -or
                $signerThumbprint -notmatch '^[0-9A-Fa-f]{40}$'))) {
        throw 'The Authenticode status and local signer-certificate observation are inconsistent.'
    }

    $observation = [ordered]@{
        executableSha256 = $executableHash
        rawStatus = $rawSignatureStatus
        signaturePresent = $signaturePresent
        signerSubject = $signerSubject
        signerThumbprint = $signerThumbprint
        checkedAtUtc = [System.DateTime]::UtcNow.ToString('o', [System.Globalization.CultureInfo]::InvariantCulture)
    }
    $observationPath = [System.IO.Path]::Combine($packageTemp, 'authenticode-observation.json')
    $observationJson = $observation | ConvertTo-Json -Compress
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($observationPath, $observationJson, $utf8WithoutBom)

    Copy-Item -LiteralPath $archivePath -Destination ([System.IO.Path]::Combine($packageTemp, [string]$manifest.archive.fileName))

    $destinationParent = [System.IO.Path]::GetDirectoryName($destination)
    [void][System.IO.Directory]::CreateDirectory($destinationParent)
    Assert-NoExistingReparseSegments -Root $canonicalRoot -Segments @(
        'third-party',
        'bin',
        'llmfit',
        'v1.1.9',
        'win-x64')
    if ([System.IO.Directory]::Exists($destination) -or [System.IO.File]::Exists($destination)) {
        throw 'The pinned LLM Fit destination appeared during acquisition; acquisition will not overwrite it.'
    }

    [System.IO.Directory]::Move($packageTemp, $destination)
    $packageTemp = $null
    Write-Output 'Pinned LLM Fit candidate acquired and verified without execution.'
}
finally {
    if ($null -ne $downloadTemp -and [System.IO.Directory]::Exists($downloadTemp)) {
        Remove-Item -LiteralPath $downloadTemp -Recurse -Force
    }

    if ($null -ne $packageTemp -and [System.IO.Directory]::Exists($packageTemp)) {
        Remove-Item -LiteralPath $packageTemp -Recurse -Force
    }
}
