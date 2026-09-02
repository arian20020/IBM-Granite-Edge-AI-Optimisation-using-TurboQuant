[CmdletBinding()]
param(
    # Repository-controlled inputs.
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    # Fresh text-evidence workspace for this attempt.
    [Parameter(Mandatory = $true)]
    [string] $WorkspaceRoot,

    # External root for disposable source and converted fixture payloads.
    [Parameter(Mandatory = $true)]
    [string] $ModelRoot,

    # Approved Python application used for deterministic manifest generation.
    [string] $PythonPath = 'python',

    # The only executable mode in this revision; live model work stays blocked.
    [switch] $OfflineFixtureMode,

    # Test-only deterministic interruption point.
    [string] $FailureStage = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The first occurrence of every identifier is intentionally kept in this order
# so the static contract can prove that stages were not silently rearranged.
$ApprovedStageOrder = @(
    'prerequisite-verification',
    'path-root-verification',
    'disk-preflight',
    'immutable-revision-resolution',
    'source-snapshot-download',
    'source-file-hash-inventory',
    'conversion-new-output-directory',
    'converted-file-hash-inventory',
    'schema-validation',
    'manifest-generation'
)

if (
    -not [string]::IsNullOrWhiteSpace($FailureStage) -and
    $FailureStage -notin $ApprovedStageOrder
) {
    throw "FailureStage is not an approved stage identifier: $FailureStage"
}

function Assert-NormalDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Label
    )

    # Reject absent paths, files, links, junctions, and other reparse points.
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label does not exist as a directory: $Path"
    }
    $Item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if (
        -not $Item.PSIsContainer -or
        ($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
        throw "$Label must be one normal directory: $Path"
    }
    return $Item.FullName
}

function Write-AtomicJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][object] $Value
    )

    # Create the immediate parent only, then publish through a same-directory move.
    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    $TemporaryPath = "$Path.tmp"
    if (
        (Test-Path -LiteralPath $Path) -or
        (Test-Path -LiteralPath $TemporaryPath)
    ) {
        throw "Atomic evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $TemporaryPath,
        (($Value | ConvertTo-Json -Depth 64) + [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false)
    )
    [IO.File]::Move($TemporaryPath, $Path)
}

function Write-Utf8Text {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text
    )

    # Evidence text is create-once and UTF-8 without a BOM.
    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    if (Test-Path -LiteralPath $Path) {
        throw "Evidence text destination already exists: $Path"
    }
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Write-InventoryCsv {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $AssetRoot,
        [Parameter(Mandatory = $true)][string] $OutputPath
    )

    # Bind each regular file to a portable relative path, size, and SHA-256.
    $ResolvedRoot = (Resolve-Path -LiteralPath $AssetRoot).Path
    $TrimCharacters = [char[]]@(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    )
    $Rows = @(
        Get-ChildItem -LiteralPath $ResolvedRoot -File -Recurse -Force |
            Where-Object {
                $_.FullName -notmatch '[\\/]\.cache[\\/]huggingface([\\/]|$)'
            } |
            ForEach-Object {
                $Relative = $_.FullName.Substring($ResolvedRoot.Length)
                $Relative = $Relative.TrimStart($TrimCharacters)
                [pscustomobject]@{
                    relative_path = $Relative.Replace(
                        [IO.Path]::DirectorySeparatorChar,
                        [char]'/'
                    )
                    size_bytes = [int64]$_.Length
                    sha256 = (
                        Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
                    ).Hash.ToLowerInvariant()
                }
            } |
            Sort-Object -Property `
                @{ Expression = { $_.relative_path.ToLowerInvariant() } }, `
                relative_path
    )
    if (@($Rows).Count -eq 0) {
        throw "Asset inventory is empty: $AssetRoot"
    }
    $Csv = $Rows | ConvertTo-Csv -NoTypeInformation
    Write-Utf8Text -Path $OutputPath -Text (($Csv -join "`n") + "`n")
}

# Validate the repository before reading any committed fixture or template.
$RepositoryRoot = Assert-NormalDirectory `
    -Path $RepositoryRoot `
    -Label 'Repository root'

# Create one fresh evidence workspace without reusing or deleting prior evidence.
if (Test-Path -LiteralPath $WorkspaceRoot) {
    throw "C1 evidence workspace already exists and will not be reused: $WorkspaceRoot"
}
$WorkspaceParent = Split-Path -Parent $WorkspaceRoot
if (-not (Test-Path -LiteralPath $WorkspaceParent -PathType Container)) {
    New-Item -ItemType Directory -Path $WorkspaceParent -Force:$false | Out-Null
}
Assert-NormalDirectory -Path $WorkspaceParent -Label 'Workspace parent' | Out-Null
New-Item -ItemType Directory -Path $WorkspaceRoot -Force:$false | Out-Null
$WorkspaceRoot = Assert-NormalDirectory `
    -Path $WorkspaceRoot `
    -Label 'C1 evidence workspace'

# Create or retain the external model root; unrelated owner files are preserved.
if (-not (Test-Path -LiteralPath $ModelRoot -PathType Container)) {
    $ModelParent = Split-Path -Parent $ModelRoot
    if (-not (Test-Path -LiteralPath $ModelParent -PathType Container)) {
        New-Item -ItemType Directory -Path $ModelParent -Force:$false | Out-Null
    }
    Assert-NormalDirectory -Path $ModelParent -Label 'Model-root parent' | Out-Null
    New-Item -ItemType Directory -Path $ModelRoot -Force:$false | Out-Null
}
$ModelRoot = Assert-NormalDirectory -Path $ModelRoot -Label 'Model asset root'

# These child directories contain text evidence only.
$StepDirectory = Join-Path $WorkspaceRoot 'steps'
$CommandDirectory = Join-Path $WorkspaceRoot 'commands'
$LogDirectory = Join-Path $WorkspaceRoot 'logs'
foreach ($Directory in @($StepDirectory, $CommandDirectory, $LogDirectory)) {
    New-Item -ItemType Directory -Path $Directory -Force:$false | Out-Null
}

$CompletedStages = [System.Collections.Generic.List[string]]::new()

function Start-ApprovedStage {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string] $Stage)

    # Enforce the exact next stage and retain an atomic started record.
    $ExpectedIndex = $CompletedStages.Count
    if ($ExpectedIndex -ge $ApprovedStageOrder.Count) {
        throw "Unexpected stage after the approved sequence: $Stage"
    }
    if ($ApprovedStageOrder[$ExpectedIndex] -ne $Stage) {
        throw (
            "Stage order violation. Expected '$($ApprovedStageOrder[$ExpectedIndex])' " +
            "but received '$Stage'."
        )
    }
    $CompletedStages.Add($Stage)
    $StepPath = Join-Path `
        $StepDirectory `
        ('{0:D2}-{1}.json' -f ($ExpectedIndex + 1), $Stage)
    Write-AtomicJson -Path $StepPath -Value ([ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        route_id = 'route-a-merged-openvino'
        stage = $Stage
        status = 'Started'
    })
    if ($FailureStage -eq $Stage) {
        throw "Injected fixture failure at stage: $Stage"
    }
}

try {
    Start-ApprovedStage -Stage 'prerequisite-verification'

    # Fail closed before any live model or package operation.
    if (-not $OfflineFixtureMode) {
        throw (
            'Live asset acquisition remains blocked until the clean Windows ' +
            'dependency-preflight decision and its hash lock are accepted. ' +
            'Use OfflineFixtureMode only for repository verification.'
        )
    }

    # Write the committed synthetic prerequisite template.
    $PrerequisiteTemplate = Join-Path `
        $RepositoryRoot `
        'experiments\granite_turboquant_intel\manifests\templates\workbook05\phase3-prerequisite-proof-template.json'
    $PrerequisitePayload = Get-Content `
        -LiteralPath $PrerequisiteTemplate `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    Write-AtomicJson `
        -Path (Join-Path $WorkspaceRoot 'prerequisite-proof.json') `
        -Value $PrerequisitePayload

    Start-ApprovedStage -Stage 'path-root-verification'

    # Revalidate both roots before creating fixture payloads.
    Assert-NormalDirectory -Path $WorkspaceRoot -Label 'Evidence workspace' |
        Out-Null
    Assert-NormalDirectory -Path $ModelRoot -Label 'Model root' | Out-Null

    Start-ApprovedStage -Stage 'disk-preflight'

    # Record deterministic fixture capacity while granting no later authority.
    Write-AtomicJson `
        -Path (Join-Path $WorkspaceRoot 'disk-preflight.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            status = 'Passed'
            free_bytes = [int64]53687091200
            required_free_bytes = [int64]53687091200
            deletion_authorised = $false
            deletion_performed = $false
            model_download_authorised = $false
            granite_model_test_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
            fixture_mode = $true
        })

    Start-ApprovedStage -Stage 'immutable-revision-resolution'

    # Read a network-free fake Hub response and retain its immutable revision.
    $HubFixturePath = Join-Path `
        $RepositoryRoot `
        'tests\testing\workbook05\fixtures\phase3\assets\fake_hub_manifest.json'
    $ResolvedModel = Get-Content `
        -LiteralPath $HubFixturePath `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    Write-AtomicJson `
        -Path (Join-Path $WorkspaceRoot 'resolved-model.json') `
        -Value $ResolvedModel

    Start-ApprovedStage -Stage 'source-snapshot-download'

    # Materialise source-shaped fixture files outside the uploaded bundle.
    $RevisionPrefix = $ResolvedModel.resolved_revision.Substring(0, 8)
    $SourceParent = Join-Path $ModelRoot 'sources'
    if (-not (Test-Path -LiteralPath $SourceParent -PathType Container)) {
        New-Item -ItemType Directory -Path $SourceParent -Force:$false | Out-Null
    }
    $SourceDirectory = Join-Path $SourceParent "granite41-3b-$RevisionPrefix"
    if (Test-Path -LiteralPath $SourceDirectory) {
        throw (
            'Source asset destination already exists and will not be reused: ' +
            $SourceDirectory
        )
    }
    New-Item -ItemType Directory -Path $SourceDirectory -Force:$false |
        Out-Null
    foreach ($RelativePath in @($ResolvedModel.siblings)) {
        $NativeRelative = $RelativePath.Replace(
            [char]'/',
            [IO.Path]::DirectorySeparatorChar
        )
        $TargetPath = Join-Path $SourceDirectory $NativeRelative
        $TargetParent = Split-Path -Parent $TargetPath
        if (-not (Test-Path -LiteralPath $TargetParent -PathType Container)) {
            New-Item -ItemType Directory -Path $TargetParent -Force:$false |
                Out-Null
        }
        [IO.File]::WriteAllText(
            $TargetPath,
            "fixture:$RelativePath`n",
            [Text.UTF8Encoding]::new($false)
        )
    }

    Start-ApprovedStage -Stage 'source-file-hash-inventory'

    # Upload only path, size, and digest metadata for source-shaped files.
    Write-InventoryCsv `
        -AssetRoot $SourceDirectory `
        -OutputPath (Join-Path $WorkspaceRoot 'source-files.csv')

    Start-ApprovedStage -Stage 'conversion-new-output-directory'

    # Create fresh converted fixtures plus a structured non-shell command record.
    $ConvertedParent = Join-Path $ModelRoot 'converted'
    if (-not (Test-Path -LiteralPath $ConvertedParent -PathType Container)) {
        New-Item -ItemType Directory -Path $ConvertedParent -Force:$false |
            Out-Null
    }
    $ConvertedDirectory = Join-Path `
        $ConvertedParent `
        "granite41-3b-int4a-g128-r100-$RevisionPrefix"
    if (Test-Path -LiteralPath $ConvertedDirectory) {
        throw (
            'Converted asset destination already exists and will not be reused: ' +
            $ConvertedDirectory
        )
    }
    New-Item -ItemType Directory -Path $ConvertedDirectory -Force:$false |
        Out-Null

    $ConvertedFixtureFiles = [ordered]@{
        'openvino_model.xml' = '<model fixture="true" />'
        'openvino_model.bin' = 'fixture-binary-metadata-placeholder'
        'config.json' = '{"fixture":true}'
        'tokenizer.json' = '{"fixture":true}'
        'tokenizer_config.json' = '{"fixture":true}'
    }
    foreach ($Name in $ConvertedFixtureFiles.Keys) {
        [IO.File]::WriteAllText(
            (Join-Path $ConvertedDirectory $Name),
            [string]$ConvertedFixtureFiles[$Name],
            [Text.UTF8Encoding]::new($false)
        )
    }

    $ConversionArguments = @(
        'export',
        'openvino',
        '--model',
        $SourceDirectory,
        '--task',
        'text-generation-with-past',
        '--weight-format',
        'int4',
        '--group-size',
        '128',
        '--ratio',
        '1.0',
        $ConvertedDirectory
    )
    Write-AtomicJson `
        -Path (Join-Path $CommandDirectory 'conversion.json') `
        -Value ([ordered]@{
            file_path = 'C:/w5c/fixture/Scripts/optimum-cli.exe'
            arguments = $ConversionArguments
            exit_code = 0
            fixture_mode = $true
        })
    Write-Utf8Text `
        -Path (Join-Path $LogDirectory 'conversion.stdout.txt') `
        -Text "Offline fixture conversion completed.`n"
    Write-Utf8Text `
        -Path (Join-Path $LogDirectory 'conversion.stderr.txt') `
        -Text ''

    Start-ApprovedStage -Stage 'converted-file-hash-inventory'

    # Upload only path, size, and digest metadata for converted fixture files.
    Write-InventoryCsv `
        -AssetRoot $ConvertedDirectory `
        -OutputPath (Join-Path $WorkspaceRoot 'converted-files.csv')

    Start-ApprovedStage -Stage 'schema-validation'

    # Closed Python contract tests validate these templates independently.
    $TemplateRoot = Join-Path `
        $RepositoryRoot `
        'experiments\granite_turboquant_intel\manifests\templates\workbook05'
    $AssetPayload = Get-Content `
        -LiteralPath (Join-Path $TemplateRoot 'model-asset-lock-template.json') `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    $ConversionPayload = Get-Content `
        -LiteralPath (Join-Path $TemplateRoot 'model-conversion-record-template.json') `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    Write-AtomicJson `
        -Path (Join-Path $WorkspaceRoot 'asset-lock.json') `
        -Value $AssetPayload
    Write-AtomicJson `
        -Path (Join-Path $WorkspaceRoot 'conversion-record.json') `
        -Value $ConversionPayload

    Start-ApprovedStage -Stage 'manifest-generation'

    # Write every remaining text file before producing manifest.sha256 last.
    Write-AtomicJson `
        -Path (Join-Path $WorkspaceRoot 'stage-order.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            stages = @($CompletedStages)
            fixture_mode = $true
        })
    Write-Utf8Text `
        -Path (Join-Path $WorkspaceRoot 'summary.md') `
        -Text (
            "# Workbook 05 C1 offline fixture`n`n" +
            "This run proves orchestration controls only. It authorises no model " +
            "execution, codec activation, storage, performance, or quality claim.`n"
        )

    $ManifestPath = Join-Path $WorkspaceRoot 'manifest.sha256'
    $PreviousLocation = Get-Location
    try {
        Set-Location -LiteralPath $RepositoryRoot
        & $PythonPath `
            -m scripts.testing.workbook05.hash_manifest `
            --root $WorkspaceRoot `
            --output $ManifestPath
        if ($LASTEXITCODE -ne 0) {
            throw "Hash-manifest generation exited with code $LASTEXITCODE."
        }
    }
    finally {
        Set-Location -LiteralPath $PreviousLocation
    }

    # Return one explicit, fully non-authorising result.
    $global:LASTEXITCODE = 0
    [pscustomobject]@{
        status = 'Passed'
        workspace = $WorkspaceRoot
        source_directory = $SourceDirectory
        converted_directory = $ConvertedDirectory
        fixture_mode = $true
        model_execution_authorised = $false
        codec_activation_claim_authorised = $false
        packed_storage_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
}
catch {
    # Capture the causal error before pipeline variables can replace $_.
    $FailureMessage = $_.Exception.Message

    # A failed attempt must not retain a manifest that resembles acceptance.
    $ManifestPath = Join-Path $WorkspaceRoot 'manifest.sha256'
    if (Test-Path -LiteralPath $ManifestPath -PathType Leaf) {
        [IO.File]::Delete($ManifestPath)
    }

    # Remove only orphaned temporary files inside this fresh evidence workspace.
    Get-ChildItem `
        -LiteralPath $WorkspaceRoot `
        -File `
        -Recurse `
        -Filter '*.tmp' `
        -ErrorAction SilentlyContinue |
        ForEach-Object { [IO.File]::Delete($_.FullName) }

    # Retain one atomic failure record with every later claim disabled.
    $FailureClass = if ($OfflineFixtureMode) { 'Failed' } else { 'Blocked' }
    $FailurePath = Join-Path $WorkspaceRoot 'failure.json'
    if (-not (Test-Path -LiteralPath $FailurePath)) {
        Write-AtomicJson -Path $FailurePath -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            status = 'Failed'
            failure_class = $FailureClass
            completed_stages = @($CompletedStages)
            message = $FailureMessage
            model_execution_authorised = $false
            codec_activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
    }

    $global:LASTEXITCODE = 1
    throw
}
