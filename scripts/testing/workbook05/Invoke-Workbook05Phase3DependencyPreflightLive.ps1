[CmdletBinding()]
param(
    # The exact checked-out project revision. The workflow supplies GITHUB_WORKSPACE.
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    # GitHub run identity. These values determine the only production workspace.
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$RunId,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$RunAttempt,

    # Fixed Python 3.12.10 application on the controlled Lenovo.
    [string]$BasePythonPath = 'C:\Program Files\Python312\python.exe',

    # SimulationMode is a repository-test seam only. The production workflow
    # contract forbids every simulation parameter and never supplies them.
    [switch]$SimulationMode,
    [string]$SimulationRoot = '',
    [string]$FailureStage = '',
    [ValidateSet('Blocked', 'IntegrityFailure', 'InfrastructureInterrupted')]
    [string]$SimulationFailureClassification = 'Blocked'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$StageOrder = @(
    'workspace-validation',
    'source-verification',
    'lock-generation',
    'normal-install',
    'vcs-install',
    'imports',
    'cli-help',
    'no-model-compatibility',
    'record-generation',
    'manifest-generation'
)
$CheckOrder = @(
    'resolver',
    'install',
    'imports',
    'cli_help',
    'no_model_compatibility',
    'remote_code_disabled'
)
$ClaimKeys = @(
    'model_download_authorised',
    'granite_model_test_authorised',
    'activation_claim_authorised',
    'packed_storage_claim_authorised',
    'performance_claim_authorised',
    'quality_claim_authorised'
)

if (
    -not [string]::IsNullOrWhiteSpace($FailureStage) -and
    $FailureStage -notin $StageOrder
) {
    throw "FailureStage is not an approved dependency stage: $FailureStage"
}

# ---------------------------------------------------------------------------
# Repository-only deterministic fixture boundary.
# ---------------------------------------------------------------------------
if ($SimulationMode) {
    if ([string]::IsNullOrWhiteSpace($SimulationRoot)) {
        throw 'SimulationRoot is required when SimulationMode is enabled.'
    }
    if (-not (Test-Path -LiteralPath $SimulationRoot -PathType Container)) {
        throw "SimulationRoot must already exist as a directory: $SimulationRoot"
    }
    if (-not (Test-Path -LiteralPath $BasePythonPath -PathType Leaf)) {
        throw "Simulation Python application is missing: $BasePythonPath"
    }

    # Mirror the live collector's fail-closed workspace rule inside the
    # repository-only fixture seam. Checking before Python starts preserves the
    # first causal message instead of replacing it with a generic exit code.
    $SimulationAttemptRoot = Join-Path `
        $SimulationRoot `
        "dependency-preflight-$RunId-$RunAttempt"
    if (Test-Path -LiteralPath $SimulationAttemptRoot) {
        throw (
            'C1 dependency-preflight workspace already exists: ' +
            $SimulationAttemptRoot
        )
    }

    $FixtureArguments = @(
        '-m',
        'scripts.testing.workbook05.phase3.dependency_preflight_fixture',
        '--output-root',
        $SimulationRoot,
        '--repository-root',
        $RepositoryRoot,
        '--run-id',
        $RunId,
        '--run-attempt',
        [string]$RunAttempt
    )
    if (-not [string]::IsNullOrWhiteSpace($FailureStage)) {
        $FixtureArguments += @(
            '--failure-stage',
            $FailureStage,
            '--failure-classification',
            $SimulationFailureClassification
        )
    }

    & $BasePythonPath @FixtureArguments
    $FixtureExitCode = $LASTEXITCODE
    if ($FixtureExitCode -ne 0) {
        throw (
            'Injected dependency-preflight simulation failed as requested ' +
            "with exit code $FixtureExitCode."
        )
    }
    return
}

if (
    -not [string]::IsNullOrWhiteSpace($SimulationRoot) -or
    -not [string]::IsNullOrWhiteSpace($FailureStage) -or
    $SimulationFailureClassification -ne 'Blocked'
) {
    throw 'Simulation parameters are forbidden during live dependency collection.'
}

# ---------------------------------------------------------------------------
# Fixed production identities and source candidates.
# ---------------------------------------------------------------------------
$CampaignId = 'GTQ-WB05-MF-v1'
$RouteId = 'route-a-merged-openvino'
$BootstrapLockSha256 = '44fec5a5c2b7fb0cd0aa36f52af1d664f65812f1b112ec1e78bc00c3c9ae288f'
$OptimumOrigin = 'https://github.com/huggingface/optimum.git'
$OptimumCommit = '982e495540364f95da1e4b6f62d2d4e5907d08fd'
$OptimumIntelOrigin = 'https://github.com/huggingface/optimum-intel.git'
$OptimumIntelCommit = 'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
$runIdentity = "dependency-preflight-$RunId-$RunAttempt"
$attemptRoot = Join-Path 'C:\w5c' $runIdentity

# Reviewed immutable Git command forms retained for static review:
# git init <source-root>
# git -C <source-root> remote add origin <exact-https-origin>
# git -C <source-root> fetch --no-tags --depth 1 origin <full-commit>
# git -C <source-root> checkout --detach <full-commit>
# git -C <source-root> remote get-url origin
# git -C <source-root> rev-parse HEAD
# git -C <source-root> status --porcelain --untracked-files=all
# git -C <source-root> ls-files
# The script never uses destructive checkout repair, shell evaluation, or model code.

$BuildModulePath = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\Workbook05.Build.psm1'
$ControlledModulePath = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\Workbook05.ControlledProcess.psm1'
Import-Module $BuildModulePath -Force -ErrorAction Stop
Import-Module $ControlledModulePath -Force -ErrorAction Stop

function Assert-PreflightNormalDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $Lowered = $Path.ToLowerInvariant()
    if (
        $Path.StartsWith('\\') -or
        $Lowered.StartsWith('\\?\') -or
        $Lowered.StartsWith('\\.\')
    ) {
        throw "$Label must not be a UNC or device path: $Path"
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label does not exist as a directory: $Path"
    }
    $Item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if (
        -not $Item.PSIsContainer -or
        ($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
        throw "$Label must be one normal local directory: $Path"
    }
    return $Item.FullName
}

function Write-PreflightUtf8Text {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Text
    )

    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        throw "Evidence parent does not exist: $Parent"
    }
    if (Test-Path -LiteralPath $Path) {
        throw "Final text evidence already exists: $Path"
    }
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Write-PreflightAtomicJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $Parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        throw "Evidence parent does not exist: $Parent"
    }
    $temporaryPath = "$Path.tmp"
    if (Test-Path -LiteralPath $Path) {
        throw "Final dependency evidence already exists: $Path"
    }
    if (Test-Path -LiteralPath $temporaryPath) {
        throw "Temporary dependency evidence already exists: $temporaryPath"
    }
    $temporaryCreated = $false
    try {
        Write-Wb05Json -Path $temporaryPath -Value $Value
        $temporaryCreated = $true
        [IO.File]::Move($temporaryPath, $Path)
    }
    catch {
        if ($temporaryCreated -and (Test-Path -LiteralPath $temporaryPath)) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
        throw
    }
}

function Get-PreflightSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Hash input is missing: $Path"
    }
    return (
        Get-FileHash -LiteralPath $Path -Algorithm SHA256
    ).Hash.ToLowerInvariant()
}

$CompletedStages = [System.Collections.Generic.List[string]]::new()
$CurrentStage = $null
$CommandRows = [System.Collections.Generic.List[object]]::new()
$CheckRows = [System.Collections.Generic.List[object]]::new()
$SourceRows = [System.Collections.Generic.List[object]]::new()

function Start-PreflightStage {
    param([Parameter(Mandatory = $true)][string]$Stage)

    $ExpectedIndex = $CompletedStages.Count
    if (
        $ExpectedIndex -ge $StageOrder.Count -or
        $StageOrder[$ExpectedIndex] -ne $Stage
    ) {
        throw (
            "Dependency stage-order violation. Expected " +
            "'$($StageOrder[$ExpectedIndex])', received '$Stage'."
        )
    }
    $script:CurrentStage = $Stage
}

function Complete-PreflightStage {
    param([Parameter(Mandatory = $true)][string]$Stage)

    if ($script:CurrentStage -ne $Stage) {
        throw "Cannot complete inactive dependency stage: $Stage"
    }
    $script:CompletedStages.Add($Stage)
    $script:CurrentStage = $null
}

function Add-PreflightCheck {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Status,
        [AllowNull()][Nullable[int]]$ExitCode
    )

    $ExpectedIndex = $CheckRows.Count
    if (
        $ExpectedIndex -ge $CheckOrder.Count -or
        $CheckOrder[$ExpectedIndex] -ne $Name
    ) {
        throw "Dependency check order changed at: $Name"
    }
    $CheckRows.Add([ordered]@{
        name = $Name
        status = $Status
        exit_code = if ($null -eq $ExitCode) { $null } else { [int]$ExitCode }
    })
}

$EvidenceRoot = $null
$WorkspaceRoot = $null
$CommandDirectory = $null
$ReportDirectory = $null
$LockDirectory = $null
$SourceEvidenceDirectory = $null

function Invoke-PreflightCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$CommandId,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [ValidateRange(1, [int]::MaxValue)][int]$MaximumElapsedSeconds = 900
    )

    $Result = Invoke-Wb05ControlledLoggedProcess `
        -CommandId $CommandId `
        -RouteId $RouteId `
        -Component 'dependency-preflight' `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -EvidenceDirectory $CommandDirectory `
        -EvidenceRoot $EvidenceRoot `
        -MaximumElapsedSeconds $MaximumElapsedSeconds `
        -EnvironmentAllowlist ([ordered]@{
            TEMP = $env:TEMP
            TMP = $env:TMP
            PIP_CACHE_DIR = $env:PIP_CACHE_DIR
        }) `
        -LogFileExtension 'txt' `
        -AtomicJsonEvidence

    $CommandRows.Add([ordered]@{
        command_id = $CommandId
        stage = $CurrentStage
        record_path = (
            $Result.record_path.Substring($EvidenceRoot.Length).TrimStart('\').Replace('\', '/')
        )
        stdout_path = (
            $Result.stdout_path.Substring($EvidenceRoot.Length).TrimStart('\').Replace('\', '/')
        )
        stderr_path = (
            $Result.stderr_path.Substring($EvidenceRoot.Length).TrimStart('\').Replace('\', '/')
        )
        resource_summary_path = (
            $Result.resource_summary_path.Substring($EvidenceRoot.Length).TrimStart('\').Replace('\', '/')
        )
        resource_csv_path = (
            $Result.resource_csv_path.Substring($EvidenceRoot.Length).TrimStart('\').Replace('\', '/')
        )
    })
    if ($Result.record.exit_code -ne 0) {
        throw (
            "Controlled dependency command failed: $CommandId " +
            "(exit_code=$($Result.record.exit_code))."
        )
    }
    return $Result
}

function Read-PreflightStdout {
    param([Parameter(Mandatory = $true)][object]$Result)

    return (
        Get-Content -LiteralPath $Result.stdout_path -Raw -Encoding UTF8
    ).Trim()
}

function ConvertTo-PreflightSourceCsv {
    [CmdletBinding()]
    param(
        # Source rows are explicit PSCustomObject records with the three fields
        # consumed by the independent Python bundle validator.
        [Parameter(Mandatory = $true)][object[]]$Rows
    )

    if ($Rows.Count -eq 0) {
        throw 'Source CSV requires at least one row.'
    }

    # ConvertTo-Csv returns one string per row. Join those strings explicitly
    # with LF, then append exactly one final LF so the artifact is deterministic
    # across Windows PowerShell versions and contains no ambiguous + / -join
    # operator ordering.
    $CsvLines = @($Rows | ConvertTo-Csv -NoTypeInformation)
    return (($CsvLines -join "`n") + "`n")
}

function Write-PreflightSourceIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$Origin,
        [Parameter(Mandatory = $true)][string]$Commit,
        [Parameter(Mandatory = $true)][string]$SourceRoot,
        [Parameter(Mandatory = $true)][string[]]$TrackedFiles
    )

    $Rows = [System.Collections.Generic.List[object]]::new()
    $CanonicalLines = [Text.StringBuilder]::new()
    foreach ($Relative in $TrackedFiles | Sort-Object) {
        if (
            [string]::IsNullOrWhiteSpace($Relative) -or
            $Relative.Contains('..') -or
            [IO.Path]::IsPathRooted($Relative)
        ) {
            throw "Unsafe tracked source path for ${Name}: $Relative"
        }
        $NativeRelative = $Relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $Candidate = Join-Path $SourceRoot $NativeRelative
        if (-not (Test-Path -LiteralPath $Candidate -PathType Leaf)) {
            throw "Tracked source member is missing for ${Name}: $Relative"
        }
        $Item = Get-Item -LiteralPath $Candidate -Force -ErrorAction Stop
        if (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Tracked source member is linked for ${Name}: $Relative"
        }
        $Digest = Get-PreflightSha256 -Path $Candidate

        # PSCustomObject exposes only the intended source-evidence properties to
        # ConvertTo-Csv. A raw ordered dictionary would serialize dictionary
        # implementation members instead of these three columns.
        $Rows.Add([pscustomobject][ordered]@{
            relative_path = $Relative.Replace('\', '/')
            size_bytes = [int64]$Item.Length
            sha256 = $Digest
        })
        [void]$CanonicalLines.Append(
            "$($Relative.Replace('\', '/'))`0$([int64]$Item.Length)`0$Digest`n"
        )
    }
    if ($Rows.Count -eq 0) {
        throw "Tracked source catalogue is empty for: $Name"
    }

    $AggregateBytes = [Text.Encoding]::UTF8.GetBytes($CanonicalLines.ToString())
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $Aggregate = (
            [BitConverter]::ToString($Hasher.ComputeHash($AggregateBytes))
        ).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $Hasher.Dispose()
    }

    $CsvPath = Join-Path $SourceEvidenceDirectory "$Name.csv"
    $CsvText = ConvertTo-PreflightSourceCsv -Rows @($Rows)
    Write-PreflightUtf8Text `
        -Path $CsvPath `
        -Text $CsvText
    $Record = [ordered]@{
        name = $Name
        repository = $Repository
        origin = $Origin
        commit = $Commit
        clean = $true
        file_count = $Rows.Count
        aggregate_sha256 = $Aggregate
        manifest_path = "sources/$Name.csv"
    }
    Write-PreflightAtomicJson `
        -Path (Join-Path $SourceEvidenceDirectory "$Name.json") `
        -Value $Record
    $SourceRows.Add($Record)
    return $Record
}

function Acquire-PreflightSource {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$Origin,
        [Parameter(Mandatory = $true)][string]$Commit,
        [Parameter(Mandatory = $true)][string]$GitPath
    )

    $SourceRoot = Join-Path (Join-Path $WorkspaceRoot 'sources') $Name
    $Parent = Split-Path -Parent $SourceRoot
    if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
        New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
    }
    if (Test-Path -LiteralPath $SourceRoot) {
        throw "Source workspace already exists: $SourceRoot"
    }

    Invoke-PreflightCommand `
        -CommandId "source-$Name-init" `
        -FilePath $GitPath `
        -ArgumentList @('init', $SourceRoot) `
        -WorkingDirectory $WorkspaceRoot | Out-Null
    Invoke-PreflightCommand `
        -CommandId "source-$Name-origin" `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'remote', 'add', 'origin', $Origin) `
        -WorkingDirectory $WorkspaceRoot | Out-Null
    Invoke-PreflightCommand `
        -CommandId "source-$Name-fetch" `
        -FilePath $GitPath `
        -ArgumentList @(
            '-C', $SourceRoot, 'fetch', '--no-tags', '--depth', '1',
            'origin', $Commit
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -MaximumElapsedSeconds 1800 | Out-Null
    Invoke-PreflightCommand `
        -CommandId "source-$Name-checkout" `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'checkout', '--detach', $Commit) `
        -WorkingDirectory $WorkspaceRoot | Out-Null

    $OriginResult = Invoke-PreflightCommand `
        -CommandId "source-$Name-origin-check" `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'remote', 'get-url', 'origin') `
        -WorkingDirectory $WorkspaceRoot
    if ((Read-PreflightStdout $OriginResult) -ne $Origin) {
        throw "Source origin identity mismatch for: $Name"
    }
    $HeadResult = Invoke-PreflightCommand `
        -CommandId "source-$Name-head" `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'rev-parse', 'HEAD') `
        -WorkingDirectory $WorkspaceRoot
    if ((Read-PreflightStdout $HeadResult) -ne $Commit) {
        throw "Source commit identity mismatch for: $Name"
    }
    $StatusResult = Invoke-PreflightCommand `
        -CommandId "source-$Name-status" `
        -FilePath $GitPath `
        -ArgumentList @(
            '-C', $SourceRoot, 'status', '--porcelain', '--untracked-files=all'
        ) `
        -WorkingDirectory $WorkspaceRoot
    if (-not [string]::IsNullOrWhiteSpace((Read-PreflightStdout $StatusResult))) {
        throw "Source checkout is dirty for: $Name"
    }
    $TrackedResult = Invoke-PreflightCommand `
        -CommandId "source-$Name-tracked" `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'ls-files') `
        -WorkingDirectory $WorkspaceRoot
    $Tracked = @(
        (Read-PreflightStdout $TrackedResult) -split "`r?`n" |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    Write-PreflightSourceIdentity `
        -Name $Name `
        -Repository $Repository `
        -Origin $Origin `
        -Commit $Commit `
        -SourceRoot $SourceRoot `
        -TrackedFiles $Tracked | Out-Null
    return $SourceRoot
}

try {
    # -----------------------------------------------------------------------
    # 1. Fresh workspace and fixed tool identities.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'workspace-validation'
    $RepositoryRoot = Assert-PreflightNormalDirectory `
        -Path $RepositoryRoot `
        -Label 'Repository root'
    if (-not (Test-Path -LiteralPath $BasePythonPath -PathType Leaf)) {
        throw "Fixed Python application is missing: $BasePythonPath"
    }
    $BasePythonVersion = & $BasePythonPath --version 2>&1
    if ($LASTEXITCODE -ne 0 -or "$BasePythonVersion" -notmatch 'Python 3\.12\.10') {
        throw "Fixed Python must remain Python 3.12.10: $BasePythonVersion"
    }

    $ControlledRoot = 'C:\w5c'
    $ControlledRoot = Assert-PreflightNormalDirectory `
        -Path $ControlledRoot `
        -Label 'C1 dependency-preflight root'
    if (Test-Path -LiteralPath $attemptRoot) {
        throw "C1 dependency-preflight workspace already exists: $attemptRoot"
    }
    New-Item -ItemType Directory -Path $attemptRoot -Force:$false | Out-Null
    $attemptRoot = Assert-PreflightNormalDirectory `
        -Path $attemptRoot `
        -Label 'C1 dependency-preflight workspace'

    $WorkspaceRoot = Join-Path $attemptRoot 'workspace'
    $EvidenceRoot = Join-Path $attemptRoot 'evidence'
    foreach ($Directory in @(
        $WorkspaceRoot,
        $EvidenceRoot,
        (Join-Path $WorkspaceRoot 'sources'),
        (Join-Path $WorkspaceRoot 'temporary'),
        (Join-Path $WorkspaceRoot 'cache'),
        (Join-Path $EvidenceRoot 'locks'),
        (Join-Path $EvidenceRoot 'reports'),
        (Join-Path $EvidenceRoot 'sources'),
        (Join-Path $EvidenceRoot 'commands')
    )) {
        New-Item -ItemType Directory -Path $Directory -Force:$false | Out-Null
    }
    $CommandDirectory = Join-Path $EvidenceRoot 'commands'
    $ReportDirectory = Join-Path $EvidenceRoot 'reports'
    $LockDirectory = Join-Path $EvidenceRoot 'locks'
    $SourceEvidenceDirectory = Join-Path $EvidenceRoot 'sources'

    $OriginalTemp = $env:TEMP
    $OriginalTmp = $env:TMP
    $OriginalPipCache = $env:PIP_CACHE_DIR
    $env:TEMP = Join-Path $WorkspaceRoot 'temporary'
    $env:TMP = $env:TEMP
    $env:PIP_CACHE_DIR = Join-Path $WorkspaceRoot 'cache'
    Complete-PreflightStage -Stage 'workspace-validation'

    # -----------------------------------------------------------------------
    # 2. Immutable source acquisition and complete tracked-file identities.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'source-verification'
    $GitCommand = Get-Command `
        -Name git `
        -CommandType Application `
        -All `
        -ErrorAction Stop |
        Select-Object -First 1
    $OptimumRoot = Acquire-PreflightSource `
        -Name 'optimum' `
        -Repository 'huggingface/optimum' `
        -Origin $OptimumOrigin `
        -Commit $OptimumCommit `
        -GitPath $GitCommand.Source
    $OptimumIntelRoot = Acquire-PreflightSource `
        -Name 'optimum-intel' `
        -Repository 'huggingface/optimum-intel' `
        -Origin $OptimumIntelOrigin `
        -Commit $OptimumIntelCommit `
        -GitPath $GitCommand.Source
    Complete-PreflightStage -Stage 'source-verification'

    # -----------------------------------------------------------------------
    # 3. Isolated bootstrap and same-target ordinary dependency lock.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'lock-generation'
    $CommittedBootstrapLock = Join-Path `
        $RepositoryRoot `
        'scripts\testing\workbook05\requirements.phase3-bootstrap.txt'
    if ((Get-PreflightSha256 $CommittedBootstrapLock) -ne $BootstrapLockSha256) {
        throw 'Committed bootstrap lock integrity mismatch.'
    }
    $RetainedBootstrapLock = Join-Path `
        $LockDirectory `
        'requirements.phase3-bootstrap.txt'
    [IO.File]::WriteAllBytes(
        $RetainedBootstrapLock,
        [IO.File]::ReadAllBytes($CommittedBootstrapLock)
    )
    if ((Get-PreflightSha256 $RetainedBootstrapLock) -ne $BootstrapLockSha256) {
        throw 'Retained bootstrap lock integrity mismatch.'
    }

    $BootstrapEnvironment = Join-Path $WorkspaceRoot 'bootstrap-venv'
    Invoke-PreflightCommand `
        -CommandId 'bootstrap-create-venv' `
        -FilePath $BasePythonPath `
        -ArgumentList @('-m', 'venv', $BootstrapEnvironment) `
        -WorkingDirectory $WorkspaceRoot | Out-Null
    $BootstrapPython = Join-Path $BootstrapEnvironment 'Scripts\python.exe'
    $BootstrapReport = Join-Path `
        $ReportDirectory `
        'bootstrap-install-report.json'
    Invoke-PreflightCommand `
        -CommandId 'bootstrap-install-lock' `
        -FilePath $BootstrapPython `
        -ArgumentList @(
            '-m', 'pip', 'install', '--isolated', '--no-input',
            '--disable-pip-version-check', '--require-hashes',
            '--report', $BootstrapReport, '-r', $RetainedBootstrapLock
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -MaximumElapsedSeconds 1800 | Out-Null
    Invoke-PreflightCommand `
        -CommandId 'bootstrap-validate-report' `
        -FilePath $BasePythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_lock_cli',
            'validate-report',
            '--lock', $RetainedBootstrapLock,
            '--report', $BootstrapReport,
            '--required-direct', 'pip-tools==7.6.0',
            '--required-direct', 'pip==26.1.2',
            '--forbidden-name', 'optimum',
            '--forbidden-name', 'optimum-intel',
            '--output', (Join-Path $ReportDirectory 'bootstrap-validation.json')
        ) `
        -WorkingDirectory $RepositoryRoot | Out-Null

    $SourceContractReport = Join-Path $ReportDirectory 'source-contracts.json'
    $OrdinaryInput = Join-Path $WorkspaceRoot 'requirements.phase3-assets.in'
    Invoke-PreflightCommand `
        -CommandId 'source-contracts' `
        -FilePath $BasePythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_source_contract_cli',
            '--optimum-root', $OptimumRoot,
            '--optimum-intel-root', $OptimumIntelRoot,
            '--report', $SourceContractReport,
            '--requirements-output', $OrdinaryInput
        ) `
        -WorkingDirectory $RepositoryRoot | Out-Null

    $OrdinaryLock = Join-Path $LockDirectory 'requirements.phase3-assets.txt'
    Invoke-PreflightCommand `
        -CommandId 'ordinary-lock-compile' `
        -FilePath $BootstrapPython `
        -ArgumentList @(
            '-m', 'piptools', 'compile', '--no-config',
            '--resolver=backtracking', '--generate-hashes', '--strip-extras',
            '--allow-unsafe', '--no-emit-index-url', '--no-emit-trusted-host',
            '--no-header', '--output-file', $OrdinaryLock, $OrdinaryInput
        ) `
        -WorkingDirectory $RepositoryRoot `
        -MaximumElapsedSeconds 3600 | Out-Null

    $NormalPolicyArguments = @(
        '--required-direct', 'transformers==5.5.0',
        '--required-direct', 'huggingface-hub==1.21.0',
        '--required-direct', 'nncf==3.2.0',
        '--required-direct', 'openvino==2026.2.1',
        '--required-direct', 'openvino-tokenizers==2026.2.1.0',
        '--forbidden-name', 'optimum',
        '--forbidden-name', 'optimum-intel'
    )
    Invoke-PreflightCommand `
        -CommandId 'ordinary-lock-validate' `
        -FilePath $BasePythonPath `
        -ArgumentList (@(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_lock_cli',
            'validate-lock', '--lock', $OrdinaryLock,
            '--output', (Join-Path $ReportDirectory 'normal-lock-validation.json')
        ) + $NormalPolicyArguments) `
        -WorkingDirectory $RepositoryRoot | Out-Null
    Add-PreflightCheck -Name 'resolver' -Status 'Passed' -ExitCode 0
    Complete-PreflightStage -Stage 'lock-generation'

    # -----------------------------------------------------------------------
    # 4. Untouched final environment and hash-enforced ordinary packages.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'normal-install'
    $FinalEnvironment = Join-Path $WorkspaceRoot 'environment'
    Invoke-PreflightCommand `
        -CommandId 'final-create-venv' `
        -FilePath $BasePythonPath `
        -ArgumentList @('-m', 'venv', $FinalEnvironment) `
        -WorkingDirectory $WorkspaceRoot | Out-Null
    $FinalPython = Join-Path $FinalEnvironment 'Scripts\python.exe'
    $FinalPip = Join-Path $FinalEnvironment 'Scripts\pip.exe'
    $NormalReport = Join-Path $ReportDirectory 'normal-install-report.json'
    Invoke-PreflightCommand `
        -CommandId 'final-install-normal' `
        -FilePath $FinalPython `
        -ArgumentList @(
            '-m', 'pip', 'install', '--isolated', '--no-input',
            '--disable-pip-version-check', '--require-hashes',
            '--report', $NormalReport, '-r', $OrdinaryLock
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -MaximumElapsedSeconds 7200 | Out-Null
    Invoke-PreflightCommand `
        -CommandId 'final-validate-normal-report' `
        -FilePath $BasePythonPath `
        -ArgumentList (@(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_lock_cli',
            'validate-report', '--lock', $OrdinaryLock,
            '--report', $NormalReport,
            '--output', (Join-Path $ReportDirectory 'normal-report-validation.json')
        ) + $NormalPolicyArguments) `
        -WorkingDirectory $RepositoryRoot | Out-Null
    Complete-PreflightStage -Stage 'normal-install'

    # -----------------------------------------------------------------------
    # 5. Exact local source installs, inventory, and pip check.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'vcs-install'
    foreach ($LocalSource in @($OptimumRoot, $OptimumIntelRoot)) {
        $SourceName = Split-Path -Leaf $LocalSource
        Invoke-PreflightCommand `
            -CommandId "vcs-install-$SourceName" `
            -FilePath $FinalPython `
            -ArgumentList @(
                '-m', 'pip', 'install', '--isolated', '--no-input',
                '--disable-pip-version-check', '--no-deps',
                '--no-build-isolation', $LocalSource
            ) `
            -WorkingDirectory $WorkspaceRoot `
            -MaximumElapsedSeconds 1800 | Out-Null
    }

    # pip check is retained as a separate causal boundary.
    Invoke-PreflightCommand `
        -CommandId 'final-pip-check' `
        -FilePath $FinalPython `
        -ArgumentList @('-m', 'pip', 'check') `
        -WorkingDirectory $WorkspaceRoot | Out-Null
    $PackageListResult = Invoke-PreflightCommand `
        -CommandId 'final-package-list' `
        -FilePath $FinalPython `
        -ArgumentList @('-m', 'pip', 'list', '--format=json') `
        -WorkingDirectory $WorkspaceRoot
    $FinalPackageText = Get-Content `
        -LiteralPath $PackageListResult.stdout_path `
        -Raw `
        -Encoding UTF8
    $FinalPackages = $FinalPackageText | ConvertFrom-Json
    Write-PreflightAtomicJson `
        -Path (Join-Path $ReportDirectory 'final-environment-packages.json') `
        -Value ([ordered]@{ packages = @($FinalPackages) })
    $VcsPackages = @(
        [ordered]@{
            name = 'optimum'
            version = [string](
                $FinalPackages |
                    Where-Object { $_.name -eq 'optimum' } |
                    Select-Object -First 1 -ExpandProperty version
            )
            commit = $OptimumCommit
        },
        [ordered]@{
            name = 'optimum-intel'
            version = [string](
                $FinalPackages |
                    Where-Object { $_.name -eq 'optimum-intel' } |
                    Select-Object -First 1 -ExpandProperty version
            )
            commit = $OptimumIntelCommit
        }
    )
    if (
        [string]::IsNullOrWhiteSpace($VcsPackages[0].version) -or
        [string]::IsNullOrWhiteSpace($VcsPackages[1].version)
    ) {
        throw 'The final environment omitted one reviewed local VCS package.'
    }
    Write-PreflightAtomicJson `
        -Path (Join-Path $ReportDirectory 'vcs-packages.json') `
        -Value ([ordered]@{ packages = $VcsPackages })
    Add-PreflightCheck -Name 'install' -Status 'Passed' -ExitCode 0
    Complete-PreflightStage -Stage 'vcs-install'

    # -----------------------------------------------------------------------
    # 6. Fresh-process import path confinement.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'imports'
    Invoke-PreflightCommand `
        -CommandId 'final-import-check' `
        -FilePath $FinalPython `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_import_check',
            '--expected-environment-root', $FinalEnvironment,
            '--output', (Join-Path $ReportDirectory 'imports.json')
        ) `
        -WorkingDirectory $RepositoryRoot | Out-Null
    Add-PreflightCheck -Name 'imports' -Status 'Passed' -ExitCode 0
    Complete-PreflightStage -Stage 'imports'

    # -----------------------------------------------------------------------
    # 7. Exact installed console entry point.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'cli-help'
    $OptimumCli = Join-Path $FinalEnvironment 'Scripts\optimum-cli.exe'
    Invoke-PreflightCommand `
        -CommandId 'final-cli-help' `
        -FilePath $OptimumCli `
        -ArgumentList @('--help') `
        -WorkingDirectory $WorkspaceRoot | Out-Null
    Add-PreflightCheck -Name 'cli_help' -Status 'Passed' -ExitCode 0
    Complete-PreflightStage -Stage 'cli-help'

    # -----------------------------------------------------------------------
    # 8. Inert conversion-argument construction and remote-code proof.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'no-model-compatibility'
    $NoModelRecord = Join-Path $ReportDirectory 'no-model-compatibility.json'
    Invoke-PreflightCommand `
        -CommandId 'final-no-model-check' `
        -FilePath $FinalPython `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_no_model_check',
            '--environment-root', $FinalEnvironment,
            '--optimum-root', $OptimumRoot,
            '--optimum-intel-root', $OptimumIntelRoot,
            '--output', $NoModelRecord
        ) `
        -WorkingDirectory $RepositoryRoot | Out-Null
    $NoModel = Get-Content `
        -LiteralPath $NoModelRecord `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    if (
        $NoModel.trust_remote_code -ne $false -or
        $NoModel.model_opened -ne $false -or
        $NoModel.network_contacted -ne $false -or
        $NoModel.process_executed -ne $false
    ) {
        throw 'No-model compatibility evidence crossed its approved boundary.'
    }
    Add-PreflightCheck `
        -Name 'no_model_compatibility' `
        -Status 'Passed' `
        -ExitCode 0
    Add-PreflightCheck `
        -Name 'remote_code_disabled' `
        -Status 'Passed' `
        -ExitCode $null
    Complete-PreflightStage -Stage 'no-model-compatibility'

    # -----------------------------------------------------------------------
    # 9. Recomputable observation, decision, indexes, and summary.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'record-generation'
    $PythonVersionResult = Invoke-PreflightCommand `
        -CommandId 'final-python-version' `
        -FilePath $FinalPython `
        -ArgumentList @('--version') `
        -WorkingDirectory $WorkspaceRoot
    $ObservedPythonVersion = (
        (Read-PreflightStdout $PythonVersionResult) -replace '^Python\s+', ''
    )
    if ($ObservedPythonVersion -ne '3.12.10') {
        throw "Final Python version drifted: $ObservedPythonVersion"
    }
    $PipVersionResult = Invoke-PreflightCommand `
        -CommandId 'final-pip-version' `
        -FilePath $FinalPython `
        -ArgumentList @('-m', 'pip', '--version') `
        -WorkingDirectory $WorkspaceRoot
    $ObservedPipVersion = (
        (Read-PreflightStdout $PipVersionResult) -split '\s+'
    )[1]

    $BootstrapLockText = Get-Content `
        -LiteralPath $RetainedBootstrapLock `
        -Raw `
        -Encoding UTF8
    $BootstrapReportText = Get-Content `
        -LiteralPath $BootstrapReport `
        -Raw `
        -Encoding UTF8
    $NormalLockText = Get-Content `
        -LiteralPath $OrdinaryLock `
        -Raw `
        -Encoding UTF8
    $NormalReportObject = Get-Content `
        -LiteralPath $NormalReport `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json

    $Observation = [ordered]@{
        generated_at_utc = [DateTime]::UtcNow.ToString('o')
        simulation_mode = $false
        workspace_root = $attemptRoot
        workspace_is_normal_local_directory = $true
        workspace_is_fresh = $true
        python_version = $ObservedPythonVersion
        python_executable_path = $FinalPython
        python_executable_sha256 = Get-PreflightSha256 $FinalPython
        pip_version = $ObservedPipVersion
        pip_executable_path = $FinalPip
        pip_executable_sha256 = Get-PreflightSha256 $FinalPip
        source_trees = @($SourceRows)
        direct_requirements = @(
            'transformers==5.5.0',
            'huggingface-hub==1.21.0',
            'nncf==3.2.0',
            'openvino==2026.2.1',
            'openvino-tokenizers==2026.2.1.0'
        )
        bootstrap_lock_path = 'locks/requirements.phase3-bootstrap.txt'
        bootstrap_lock_text = $BootstrapLockText
        bootstrap_lock_sha256 = Get-PreflightSha256 $RetainedBootstrapLock
        bootstrap_install_report_path = 'reports/bootstrap-install-report.json'
        bootstrap_install_report_text = $BootstrapReportText
        bootstrap_install_report_sha256 = Get-PreflightSha256 $BootstrapReport
        lock_path = 'locks/requirements.phase3-assets.txt'
        lock_text = $NormalLockText
        lock_sha256 = Get-PreflightSha256 $OrdinaryLock
        lock_generator = 'pip-tools==7.6.0'
        normal_install_report = $NormalReportObject
        vcs_packages = $VcsPackages
        checks = @($CheckRows)
        import_modules = @(
            'optimum',
            'optimum.intel',
            'transformers',
            'nncf',
            'openvino'
        )
        cli_help_exit_code = 0
        no_model_compatibility_exit_code = 0
        final_environment_packages = @($FinalPackages)
        source_contracts_path = 'reports/source-contracts.json'
        no_model_compatibility_path = 'reports/no-model-compatibility.json'
        command_index_path = 'command-index.json'
        stage_order_path = 'stage-order.json'
    }
    $ObservationPath = Join-Path $EvidenceRoot 'observation.json'
    Write-PreflightAtomicJson -Path $ObservationPath -Value $Observation

    $DecisionPath = Join-Path $EvidenceRoot 'decision.json'
    Invoke-PreflightCommand `
        -CommandId 'decision-recompute' `
        -FilePath $BasePythonPath `
        -ArgumentList @(
            '-m',
            'scripts.testing.workbook05.phase3.dependency_decision_cli',
            '--observation', $ObservationPath,
            '--output', $DecisionPath
        ) `
        -WorkingDirectory $RepositoryRoot | Out-Null
    $decision = Get-Content `
        -LiteralPath $DecisionPath `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    if ($decision.status -ne 'Passed') {
        throw (
            'Dependency decision did not pass: ' +
            (@($decision.reasons) -join '; ')
        )
    }

    Write-PreflightAtomicJson `
        -Path (Join-Path $EvidenceRoot 'checks.json') `
        -Value ([ordered]@{ checks = @($CheckRows) })
    Write-PreflightAtomicJson `
        -Path (Join-Path $EvidenceRoot 'command-index.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            record_type = 'dependency-command-index'
            commands = @($CommandRows)
        })
    Complete-PreflightStage -Stage 'record-generation'

    # -----------------------------------------------------------------------
    # 10. Manifest publication after every final record and only after Passed.
    # -----------------------------------------------------------------------
    Start-PreflightStage -Stage 'manifest-generation'
    Complete-PreflightStage -Stage 'manifest-generation'
    Write-PreflightAtomicJson `
        -Path (Join-Path $EvidenceRoot 'stage-order.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            stage_order = $StageOrder
            completed_stages = @($CompletedStages)
            current_stage = $null
        })
    Write-PreflightUtf8Text `
        -Path (Join-Path $EvidenceRoot 'summary.md') `
        -Text (
            "# Workbook 05 clean dependency preflight`n`n" +
            "Status: Passed`n`n" +
            "This evidence qualifies the conversion dependency environment only. " +
            "No model was downloaded, converted, opened, or executed, and no " +
            "codec, storage, performance, or quality claim is authorised.`n"
        )

    # The decision.status -ne 'Passed' check above is the final scientific gate.
    $ManifestPath = Join-Path $EvidenceRoot 'manifest.sha256'
    & $BasePythonPath `
        -m scripts.testing.workbook05.hash_manifest `
        --root $EvidenceRoot `
        --output $ManifestPath
    if ($LASTEXITCODE -ne 0) {
        throw "manifest.sha256 generation exited with code $LASTEXITCODE."
    }
}
catch {
    $FailureMessage = $_.Exception.Message
    if ($null -ne $EvidenceRoot -and (Test-Path -LiteralPath $EvidenceRoot)) {
        $ManifestPath = Join-Path $EvidenceRoot 'manifest.sha256'
        $FailurePath = Join-Path $EvidenceRoot 'failure.json'
        if (
            -not (Test-Path -LiteralPath $ManifestPath) -and
            -not (Test-Path -LiteralPath $FailurePath)
        ) {
            $Classification = if (
                $FailureMessage -match '(?i)hash|identity|integrity|source|path|workspace|lock'
            ) {
                'IntegrityFailure'
            }
            elseif (
                $FailureMessage -match '(?i)deadline|cancel|terminated|runner|safety|interrupt'
            ) {
                'InfrastructureInterrupted'
            }
            else {
                'Blocked'
            }
            $Failure = [ordered]@{
                schema_version = '1.0'
                campaign_id = $CampaignId
                record_type = 'dependency-preflight-failure'
                route_id = $RouteId
                run_id = $RunId
                run_attempt = $RunAttempt
                workspace_root = $attemptRoot
                current_stage = $CurrentStage
                completed_stages = @($CompletedStages)
                classification = $Classification
                first_causal_message = $FailureMessage
                recorded_at_utc = [DateTime]::UtcNow.ToString('o')
                model_download_authorised = $false
                granite_model_test_authorised = $false
                activation_claim_authorised = $false
                packed_storage_claim_authorised = $false
                performance_claim_authorised = $false
                quality_claim_authorised = $false
            }
            Write-PreflightAtomicJson -Path $FailurePath -Value $Failure
            $StagePath = Join-Path $EvidenceRoot 'stage-order.json'
            if (-not (Test-Path -LiteralPath $StagePath)) {
                Write-PreflightAtomicJson `
                    -Path $StagePath `
                    -Value ([ordered]@{
                        schema_version = '1.0'
                        stage_order = $StageOrder
                        completed_stages = @($CompletedStages)
                        current_stage = $CurrentStage
                    })
            }
        }
    }
    throw
}
finally {
    if (Get-Variable -Name OriginalTemp -ErrorAction SilentlyContinue) {
        if ($null -eq $OriginalTemp) {
            Remove-Item Env:TEMP -ErrorAction SilentlyContinue
        }
        else {
            $env:TEMP = $OriginalTemp
        }
        if ($null -eq $OriginalTmp) {
            Remove-Item Env:TMP -ErrorAction SilentlyContinue
        }
        else {
            $env:TMP = $OriginalTmp
        }
        if ($null -eq $OriginalPipCache) {
            Remove-Item Env:PIP_CACHE_DIR -ErrorAction SilentlyContinue
        }
        else {
            $env:PIP_CACHE_DIR = $OriginalPipCache
        }
    }
    Remove-Module 'Workbook05.ControlledProcess' -Force -ErrorAction SilentlyContinue
    Remove-Module 'Workbook05.Build' -Force -ErrorAction SilentlyContinue
}
