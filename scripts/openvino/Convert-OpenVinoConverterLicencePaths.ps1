[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$StageDirectory
)

$ErrorActionPreference = 'Stop'

try {
    $stageRoot = [IO.Path]::GetFullPath($StageDirectory).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($stageRoot) -or
        [IO.Path]::GetPathRoot($stageRoot).TrimEnd('\') -ieq $stageRoot.TrimEnd('\')) {
        throw 'invalid_stage_root'
    }

    $enumerationRoot = if ($stageRoot.StartsWith('\\')) {
        '\\?\UNC\' + $stageRoot.Substring(2)
    }
    else {
        '\\?\' + $stageRoot
    }
    $licenceFiles = @(
        Get-ChildItem -LiteralPath $enumerationRoot -File -Recurse |
            ForEach-Object {
                $relative = $_.FullName.Substring($enumerationRoot.Length + 1).Replace('\', '/')
                if ($relative -match '(?i)^packages/[^/]+\.dist-info/licenses/.+$') {
                    [pscustomobject]@{
                        File = $_
                        Relative = $relative
                    }
                }
            } |
            Sort-Object Relative
    )

    $licenceRoot = Join-Path $stageRoot 'licenses\wheel-files'
    [IO.Directory]::CreateDirectory($licenceRoot) | Out-Null
    $inventory = @()
    foreach ($item in $licenceFiles) {
        $sha256 = (Get-FileHash -LiteralPath $item.File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $stagedRelative = "licenses/wheel-files/$sha256.license"
        $destination = Join-Path $stageRoot $stagedRelative.Replace('/', '\')
        if ([IO.File]::Exists($destination)) {
            $existing = [IO.FileInfo]::new($destination)
            $existingHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($existing.Length -ne $item.File.Length -or $existingHash -cne $sha256) {
                throw 'licence_digest_collision'
            }
        }
        else {
            [IO.File]::Copy($item.File.FullName, $destination, $false)
        }
        $inventory += [ordered]@{
            sourcePath = $item.Relative
            stagedPath = $stagedRelative
            length = [long]$item.File.Length
            sha256 = $sha256
        }
        [IO.File]::Delete($item.File.FullName)
    }

    $inventoryDocument = [ordered]@{
        schemaVersion = 1
        purpose = 'Preserve wheel licence bytes and provenance within the Windows package path budget.'
        files = $inventory
    }
    $inventoryPath = Join-Path $stageRoot 'licenses\wheel-license-inventory.json'
    [IO.File]::WriteAllText(
        $inventoryPath,
        ($inventoryDocument | ConvertTo-Json -Depth 5),
        [Text.UTF8Encoding]::new($false))

    [Console]::Out.WriteLine('converter_licence_paths_normalized')
}
catch {
    [Console]::Out.WriteLine('converter_licence_paths_failed')
    exit 1
}
