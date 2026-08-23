[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BundleRoot,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$DestinationRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Stop-Invalid {
    [Console]::Out.WriteLine('openvino_ucl_handoff_invalid')
    exit 1
}

function Test-ExactProperties {
    param(
        [Parameter(Mandatory)]$Value,
        [Parameter(Mandatory)][string[]]$Names
    )
    if ($Value -isnot [pscustomobject]) { return $false }
    $actual = @($Value.PSObject.Properties.Name)
    return $actual.Count -eq $Names.Count -and
        ($actual -join "`n") -ceq ($Names -join "`n")
}

function Get-NormalizedDirectoryPrefix {
    param([Parameter(Mandatory)][string]$Path)
    return [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') +
        [IO.Path]::DirectorySeparatorChar
}

function Test-PathDescendsFrom {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Path
    )
    return [IO.Path]::GetFullPath($Path).StartsWith(
        (Get-NormalizedDirectoryPrefix $Root),
        [StringComparison]::OrdinalIgnoreCase)
}

function Get-SafePayloadPath {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath
    )
    if ([IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath -match '(^|[\/])\.\.([\/]|$)' -or
        $RelativePath -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._/-]{0,255}$') {
        throw 'payload-path-invalid'
    }
    $path = [IO.Path]::GetFullPath((Join-Path $Root (
        $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))))
    if (-not (Test-PathDescendsFrom $Root $path)) {
        throw 'payload-path-outside-root'
    }
    return $path
}

function Get-LowerSha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-PayloadIntegrity {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)]$Inventory
    )
    $checksumPath = Join-Path $Root 'CHECKSUMS.sha256'
    $checksumLines = @(Get-Content -LiteralPath $checksumPath -Encoding UTF8 |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $checksums = [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::Ordinal)
    foreach ($line in $checksumLines) {
        if ($line -cnotmatch '^([0-9a-f]{64})  ([a-zA-Z0-9][a-zA-Z0-9._/-]{0,255})$' -or
            $checksums.ContainsKey($Matches[2])) {
            throw 'checksum-ledger-invalid'
        }
        $checksums.Add($Matches[2], $Matches[1])
    }
    if ($checksums.Count -ne @($Inventory.payloads).Count) {
        throw 'checksum-count-invalid'
    }

    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($payload in @($Inventory.payloads)) {
        if (-not (Test-ExactProperties $payload @(
                'role','relativePath','length','sha256','expandedFileCount',
                'expandedBytes','evidenceDisposition')) -or
            $payload.role -cnotmatch '^[a-z][a-zA-Z0-9]{1,31}$' -or
            $payload.length -le 0 -or
            $payload.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
            $payload.expandedFileCount -lt 0 -or
            $payload.expandedFileCount -gt 30000 -or
            $payload.expandedBytes -lt 0 -or
            $payload.expandedBytes -gt 4294967296 -or
            $payload.evidenceDisposition -cne 'transfer_input_only' -or
            -not $paths.Add([string]$payload.relativePath)) {
            throw 'payload-record-invalid'
        }
        $path = Get-SafePayloadPath $Root $payload.relativePath
        $item = Get-Item -LiteralPath $path -Force
        if ($item.PSIsContainer -or
            ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $item.Length -ne [long]$payload.length) {
            throw 'payload-file-invalid'
        }
        $actual = Get-LowerSha256 $path
        if ($actual -cne [string]$payload.sha256 -or
            -not $checksums.ContainsKey([string]$payload.relativePath) -or
            $checksums[[string]$payload.relativePath] -cne $actual) {
            throw 'payload-digest-invalid'
        }
    }
}

function Expand-SafeArchive {
    param(
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][int]$ExpectedFileCount,
        [Parameter(Mandatory)][long]$ExpectedExpandedBytes
    )
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        if ($archive.Entries.Count -ne $ExpectedFileCount -or
            $archive.Entries.Count -gt 30000) {
            throw 'archive-entry-count-invalid'
        }
        $destinations = [Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        [long]$expandedBytes = 0
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrWhiteSpace($entry.Name) -or
                [IO.Path]::IsPathRooted($entry.FullName) -or
                $entry.FullName -match '(^|[\/])\.\.([\/]|$)' -or
                $entry.FullName.IndexOf(':') -ge 0 -or
                $entry.Length -lt 0 -or $entry.Length -gt 2147483648) {
                throw 'archive-entry-traversal'
            }
            $unixType = (($entry.ExternalAttributes -shr 16) -band 0xF000)
            if ($unixType -eq 0xA000) { throw 'archive-entry-reparse-invalid' }
            if ([long]$entry.Length -gt ($ExpectedExpandedBytes - $expandedBytes)) {
                throw 'expanded-size-invalid'
            }
            $relative = $entry.FullName.Replace('/', [IO.Path]::DirectorySeparatorChar)
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $relative))
            if (-not (Test-PathDescendsFrom $Destination $target) -or
                -not $destinations.Add($target)) {
                throw 'duplicate-entry'
            }
            $parent = Split-Path -Parent $target
            $null = New-Item -ItemType Directory -Path $parent -Force
            $input = $entry.Open()
            $output = [IO.File]::Open($target, [IO.FileMode]::CreateNew,
                [IO.FileAccess]::Write, [IO.FileShare]::None)
            try {
                $buffer = [byte[]]::new(81920)
                [long]$copied = 0
                while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    if ([long]$read -gt ([long]$entry.Length - $copied)) {
                        throw 'expanded-size-invalid'
                    }
                    $output.Write($buffer, 0, $read)
                    $copied += [long]$read
                }
                if ($copied -ne [long]$entry.Length) {
                    throw 'archive-entry-length-invalid'
                }
            }
            finally {
                $output.Dispose()
                $input.Dispose()
            }
            $expandedBytes += [long]$entry.Length
        }
        if ($expandedBytes -ne $ExpectedExpandedBytes) {
            throw 'expanded-size-invalid'
        }
    }
    finally { $archive.Dispose() }
}

function Invoke-ManifestVerifier {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$ScriptName,
        [Parameter(Mandatory)][string]$StageRoot
    )
    $scriptPath = Join-Path $Repository "scripts\openvino\$ScriptName"
    $result = @(& powershell.exe -NoLogo -NoProfile -NonInteractive `
        -ExecutionPolicy Bypass -File $scriptPath -StageDirectory $StageRoot 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'manifest-verification-failed' }
}

try {
    $bundle = [IO.Path]::GetFullPath($BundleRoot)
    $destination = [IO.Path]::GetFullPath($DestinationRoot)
    if (-not (Test-Path -LiteralPath $bundle -PathType Container)) { Stop-Invalid }
    $bundleItem = Get-Item -LiteralPath $bundle -Force
    if ($bundleItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { Stop-Invalid }
    if ($destination -ieq $bundle -or
        (Test-PathDescendsFrom $bundle $destination) -or
        (Test-PathDescendsFrom $destination $bundle)) {
        throw 'roots-aliased'
    }
    if (Test-Path -LiteralPath $destination) {
        $destinationItem = Get-Item -LiteralPath $destination -Force
        if (-not $destinationItem.PSIsContainer -or
            ($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            @(Get-ChildItem -LiteralPath $destination -Force).Count -ne 0) {
            throw 'destination-not-empty'
        }
    }

    $modulePath = Join-Path $bundle 'tools\lib\OpenVinoClosedJson.psm1'
    $inventoryPath = Join-Path $bundle 'inventory\payloads.json'
    Import-Module $modulePath -Force
    $raw = Get-OpenVinoClosedJsonText -Path $inventoryPath `
        -MaximumBytes 131072 -MaximumDepth 8
    $inventory = $raw | ConvertFrom-Json -ErrorAction Stop
    if (-not (Test-ExactProperties $inventory @(
            'schemaVersion','handoffCommit','baselineImplementationCommit',
            'branch','payloads')) -or
        $inventory.schemaVersion -ne 1 -or
        $inventory.handoffCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $inventory.baselineImplementationCommit -cnotmatch '^[0-9a-f]{40}$' -or
        $inventory.branch -cne 'feature/openvino-route') {
        throw 'inventory-invalid'
    }
    $roles = @($inventory.payloads | ForEach-Object { $_.role })
    foreach ($requiredRole in @(
            'gitBundle','sourceSnapshot','officialWorker','turboQuantWorker',
            'converter','context')) {
        if ($requiredRole -notin $roles) { throw 'inventory-role-missing' }
    }
    Test-PayloadIntegrity -Root $bundle -Inventory $inventory

    $null = New-Item -ItemType Directory -Path $destination -Force
    $repositoryDestination = Join-Path $destination 'repository'
    $bundleRow = @($inventory.payloads | Where-Object role -ceq 'gitBundle')
    if ($bundleRow.Count -ne 1) { throw 'git-bundle-record-invalid' }
    $bundlePath = Get-SafePayloadPath $bundle $bundleRow[0].relativePath
    @(& git bundle verify $bundlePath 2>&1) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'git-bundle-invalid' }
    @(& git clone --branch feature/openvino-route --single-branch `
        $bundlePath $repositoryDestination 2>&1) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'git-clone-failed' }
    $actualCommit = [string](& git -C $repositoryDestination rev-parse HEAD)
    $status = @(& git -C $repositoryDestination status --porcelain)
    if ($actualCommit.Trim().ToLowerInvariant() -cne $inventory.handoffCommit -or
        $status.Count -ne 0) {
        throw 'repository-identity-invalid'
    }

    $closures = Join-Path $destination 'closures'
    $null = New-Item -ItemType Directory -Path $closures
    $closureSpecs = @(
        [pscustomobject]@{ Role='officialWorker'; Leaf='official'; Verifier='Test-OpenVinoOfficialWorkerManifest.ps1'; Manifest='worker-manifest.json' },
        [pscustomobject]@{ Role='turboQuantWorker'; Leaf='turboquant'; Verifier='Test-OpenVinoTurboQuantWorkerManifest.ps1'; Manifest='worker-manifest.json' },
        [pscustomobject]@{ Role='converter'; Leaf='converter'; Verifier='Test-OpenVinoConverterWorkerManifest.ps1'; Manifest='converter-manifest.json' }
    )
    $statePayloads = @()
    foreach ($spec in $closureSpecs) {
        $row = @($inventory.payloads | Where-Object role -ceq $spec.Role)
        if ($row.Count -ne 1) { throw 'closure-record-invalid' }
        $root = Join-Path $closures $spec.Leaf
        $null = New-Item -ItemType Directory -Path $root
        Expand-SafeArchive `
            -ArchivePath (Get-SafePayloadPath $bundle $row[0].relativePath) `
            -Destination $root `
            -ExpectedFileCount ([int]$row[0].expandedFileCount) `
            -ExpectedExpandedBytes ([long]$row[0].expandedBytes)
        Invoke-ManifestVerifier -Repository $repositoryDestination `
            -ScriptName $spec.Verifier -StageRoot $root
        $statePayloads += [ordered]@{
            role = $spec.Role
            archiveSha256 = [string]$row[0].sha256
            manifestSha256 = Get-LowerSha256 (Join-Path $root $spec.Manifest)
        }
    }
    $resolvedClosureRoots = @($closureSpecs | ForEach-Object {
        Get-NormalizedDirectoryPrefix (Join-Path $closures $_.Leaf) })
    if (@($resolvedClosureRoots | Select-Object -Unique).Count -ne 3) {
        throw 'roots-aliased'
    }

    $state = [ordered]@{
        schemaVersion = 1
        handoffCommit = [string]$inventory.handoffCommit
        baselineImplementationCommit = [string]$inventory.baselineImplementationCommit
        branch = [string]$inventory.branch
        payloads = $statePayloads
    }
    $state | ConvertTo-Json -Depth 4 | Set-Content `
        -LiteralPath (Join-Path $destination 'handoff-state.json') `
        -Encoding UTF8
    [Console]::Out.WriteLine('openvino_ucl_handoff_initialized')
    exit 0
}
catch {
    Stop-Invalid
}
