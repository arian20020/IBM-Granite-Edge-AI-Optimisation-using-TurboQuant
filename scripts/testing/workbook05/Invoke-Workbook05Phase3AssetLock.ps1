[CmdletBinding()]
param(
    # Repository-controlled scripts, schemas, templates, and fixtures are read
    # only from this exact root.
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    # Every attempt receives a previously absent evidence workspace.
    [Parameter(Mandatory = $true)]
    [string] $WorkspaceRoot,

    # Source and converted payloads stay outside the text-only evidence bundle.
    [Parameter(Mandatory = $true)]
    [string] $ModelRoot,

    # The approved Python executable is invoked by file path with an argument
    # array. The fixture tests may use the current hosted interpreter.
    [string] $PythonPath = 'python',

    # Offline fixture mode proves ordering, failure preservation, atomic records,
    # and payload separation without network access or model execution.
    [switch] $OfflineFixtureMode,

    # Test-only fault injection. Production callers leave this empty.
    [string] $FailureStage = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Keep the approved order in one literal list so code review and contract tests
# can prove that no stage is silently skipped or reordered.
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
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label does not exist as a directory: $Path"
    }

    $Item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if (
        ($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
        throw "$Label must not be a link or reparse point: $Path"
    }

    return $Item.FullName
}

function Write-AtomicJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [object] $Value
    )

    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }

    $TemporaryPath = "$Path.tmp"
    if (
        Test-Path -LiteralPath $Path -or
        Test-Path -LiteralPath $TemporaryPath
    ) {
        throw "Atomic evidence destination already exists: $Path"
    }

    $Json = $Value | ConvertTo-Json -Depth 64
    [IO.File]::WriteAllText(
        $TemporaryPath,
        $Json + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false)
    )

    # Move within one directory so the final record appears atomically.
    [IO.File]::Move($TemporaryPath, $Path)
}

function Write-Utf8Text {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Text
    )

    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    if (Test-Path -LiteralPath $Path) {
        throw "Evidence text destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $Path,
        $Text,
        [Text.UTF8Encoding]::new($false)
    )
}

function Write-InventoryCsv {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $AssetRoot,

        [Parameter(Mandatory = $true)]
        [string] $OutputPath
    )

    $ResolvedRoot = (Resolve-Path -LiteralPath $AssetRoot).Path
    $Rows = @(
        Get-ChildItem -LiteralPath $ResolvedRoot -File -Recurse -Force |
            Where-Object {
                $_.FullName -notmatch '[\\/]\.cache[\\/]huggingface([\\/]|$)'
            } |
            ForEach-Object {
                $Relative = $_.FullName.Substring($ResolvedRoot.Length).TrimStart('\', '/')
                [pscustomobject]@{
                    relative_path = $Relative.Replace('\', '/')
                    size_bytes   = [int64]$_.Length
                    sha256       = (
                        Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
                    ).Hash.ToLowerInvariant()
                }
            } |
            Sort-Object -Property @{ Expression = { $_.relative_path.ToLowerInvariant() } }, relative_path
    )

    if (@($Rows).Count -eq 0) {
        throw "Asset inventory is empty: $AssetRoot"
    }

    $Csv = $Rows | ConvertTo-Csv -NoTypeInformation
    Write-Utf8Text -Path $OutputPath -Text (($Csv -join "`n") + "`n")
}

$RepositoryRoot = Assert-NormalDirectory `
    -Path $RepositoryRoot `
    -Label 'Repository root'

if (Test-Path -LiteralPath $WorkspaceRoot) {
    throw "C1 evidence workspace already exists and will not be reused: $WorkspaceRoot"
}

$WorkspaceParent = Split-Path -Parent $WorkspaceRoot
if (-not (Test-Path -LiteralPath $WorkspaceParent -PathType Container)) {
    New-Item -ItemType Directory -Path $WorkspaceParent -Force:$false | Out-Null
}
Assert-NormalDirectory -Path $WorkspaceParent -Label 'Workspace parent' | Out-Null
New-Item -ItemType Directory -Path $WorkspaceRoot -Force:$false | Out-Null
$WorkspaceRoot = Assert-NormalDirectory -Path $WorkspaceRoot -Label 'C1 evidence workspace'

if (-not (Test-Path -LiteralPath $ModelRoot -PathType Container)) {
    $ModelParent = Split-Path -Parent $ModelRoot
    if (-not (Test-Path -LiteralPath $ModelParent -PathType Container)) {
        New-Item -ItemType Directory -Path $ModelParent -Force:$false | Out-Null
    }
    New-Item -ItemType Directory -Path $ModelRoot -Force:$false | Out-Null
}
$ModelRoot = Assert-NormalDirectory -Path $ModelRoot -Label 'Model asset root'

$StepDirectory = Join-Path $WorkspaceRoot 'steps'
$CommandDirectory = Join-Path $WorkspaceRoot 'commands'
$LogDirectory = Join-Path $WorkspaceRoot 'logs'
New-Item -ItemType Directory -Path $StepDirectory -Force:$false | Out-Null
New-Item -ItemType Directory -Path $CommandDirectory -Force:$false | Out-Null
New-Item -ItemType Directory -Path $LogDirectory -Force:$false | Out-Null

$CompletedStages = [System.Collections.Generic.List[string]]::new()

function Start-ApprovedStage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Stage
    )

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
    $StepPath = Join-Path $StepDirectory ('{0:D2}-{1}.json' -f ($ExpectedIndex + 1), $Stage)
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

    if (-not $OfflineFixtureMode) {
        throw (
            'Live asset acquisition remains blocked until the clean Windows ' +
            'dependency-preflight decision and its hash lock are accepted. '
            'Use OfflineFixtureMode only for repository verification.'
        )
    }

    # Fixture mode uses the committed closed template and performs no machine or
    # network mutation outside the caller-provided temporary roots.
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
    Assert-NormalDirectory -Path $WorkspaceRoot -Label 'Evidence workspace' | Out-Null
    Assert-NormalDirectory -Path $ModelRoot -Label 'Model root' | Out-Null

    Start-ApprovedStage -Stage 'disk-preflight'
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
    $RevisionPrefix = $ResolvedModel.resolved_revision.Substring(0, 8)
    $SourceParent = Join-Path $ModelRoot 'sources'
    if (-not (Test-Path -LiteralPath $SourceParent -PathType Container)) {
        New-Item -ItemType Directory -Path $SourceParent -Force:$false | Out-Null
    }
    $SourceDirectory = Join-Path $SourceParent "granite41-3b-$RevisionPrefix"
    if (Test-Path -LiteralPath $SourceDirectory) {
        throw "Source asset destination already exists and will not be reused: $SourceDirectory"
    }
    New-Item -ItemType Directory -Path $SourceDirectory -Force:$false | Out-Null

    foreach ($RelativePath in @($ResolvedModel.siblings)) {
        $TargetPath = Join-Path $SourceDirectory ($RelativePath.Replace('/', '\'))
        $TargetParent = Split-Path -Parent $TargetPath
        if (-not (Test-Path -LiteralPath $TargetParent -PathType Container)) {
            New-Item -ItemType Directory -Path $TargetParent -Force:$false | Out-Null
        }
        [IO.File]::WriteAllText(
            $TargetPath,
            "fixture:$RelativePath`n",
            [Text.UTF8Encoding]::new($false)
        )
    }

    Start-ApprovedStage -Stage 'source-file-hash-inventory'
    Write-InventoryCsv `
        -AssetRoot $SourceDirectory `
        -OutputPath (Join-Path $WorkspaceRoot 'source-files.csv')

    Start-ApprovedStage -Stage 'conversion-new-output-directory'
    $ConvertedParent = Join-Path $ModelRoot 'converted'
    if (-not (Test-Path -LiteralPath $ConvertedParent -PathType Container)) {
        New-Item -ItemType Directory -Path $ConvertedParent -Force:$false | Out-Null
    }
    $ConvertedDirectory = Join-Path `
        $ConvertedParent `
        "granite41-3b-int4a-g128-r100-$RevisionPrefix"
    if (Test-Path -LiteralPath $ConvertedDirectory) {
        throw "Converted asset destination already exists and will not be reused: $ConvertedDirectory"
    }
    New-Item -ItemType Directory -Path $ConvertedDirectory -Force:$false | Out-Null

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
    Write-InventoryCsv `
        -AssetRoot $ConvertedDirectory `
        -OutputPath (Join-Path $WorkspaceRoot 'converted-files.csv')

    Start-ApprovedStage -Stage 'schema-validation'
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

    $PreviousLocation = Get-Location
    try {
        Set-Location -LiteralPath $RepositoryRoot
        & $PythonPath `
            -m scripts.testing.workbook05.hash_manifest `
            --root $WorkspaceRoot `
            --output (Join-Path $WorkspaceRoot 'manifest.sha256')
        if ($LASTEXITCODE -ne 0) {
            throw "Hash-manifest generation exited with code $LASTEXITCODE."
        }
    }
    finally {
        Set-Location -LiteralPath $PreviousLocation
    }

    [pscustomobject]@{
        status = 'Passed'
        workspace = $WorkspaceRoot
        source_directory = $SourceDirectory
        converted_directory = $ConvertedDirectory
        fixture_mode = $true
        model_execution_authorised = $false
        codec_activation_claim_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
}
catch {
    # Preserve every completed evidence file and add one atomic failure record.
    # Only orphaned temporary files inside the new evidence workspace are
    # removed; external source and conversion trees are never deleted here.
    Get-ChildItem `
        -LiteralPath $WorkspaceRoot `
        -File `
        -Recurse `
        -Filter '*.tmp' `
        -ErrorAction SilentlyContinue |
        ForEach-Object {
            [IO.File]::Delete($_.FullName)
        }

    $FailurePath = Join-Path $WorkspaceRoot 'failure.json'
    if (-not (Test-Path -LiteralPath $FailurePath)) {
        Write-AtomicJson -Path $FailurePath -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            status = 'Failed'
            failure_class = if ($OfflineFixtureMode) {
                'Failed'
            }
            else {
                'Blocked'
            }
            completed_stages = @($CompletedStages)
            message = $_.Exception.Message
            model_execution_authorised = $false
            codec_activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
    }
    throw
}
