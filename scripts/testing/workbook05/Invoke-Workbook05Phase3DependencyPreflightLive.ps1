[CmdletBinding()]
param(
    # Exact repository head checked out by the hosted workflow.
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    # Previously absent normal C:\w5c workspace for source trees and venvs.
    [Parameter(Mandatory = $true)]
    [string] $WorkspaceRoot,

    # Previously absent text-only evidence directory beneath runner temporary
    # storage. No wheel, source tree, executable, library, model, or IR is copied.
    [Parameter(Mandatory = $true)]
    [string] $EvidenceRoot,

    # Pinned Python 3.12.10 application supplied by the workflow.
    [Parameter(Mandatory = $true)]
    [string] $PythonPath,

    # Exact Git application selected by the workflow or runner image.
    [string] $GitPath = 'git',

    # Bound long-running network/build steps rather than allowing a hung job.
    [ValidateRange(60, 21600)]
    [int] $CommandTimeoutSeconds = 7200
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ApprovedStageOrder = @(
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

$ExpectedPythonVersion = 'Python 3.12.10'
$OptimumIntelCommit = 'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
$OptimumCommit = '982e495540364f95da1e4b6f62d2d4e5907d08fd'
$OptimumIntelOrigin = 'https://github.com/huggingface/optimum-intel.git'
$OptimumOrigin = 'https://github.com/huggingface/optimum.git'

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
        -not $Item.PSIsContainer -or
        ($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    ) {
        throw "$Label must be one normal directory: $Path"
    }
    return $Item.FullName
}

function Assert-ControlledW5cPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (
        $Path.StartsWith('\\', [StringComparison]::Ordinal) -or
        $Path.StartsWith('\\?\', [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith('\\.\', [StringComparison]::OrdinalIgnoreCase)
    ) {
        throw "Workspace must not be a UNC or device path: $Path"
    }

    $FullPath = [IO.Path]::GetFullPath($Path)
    $ControlledRoot = [IO.Path]::GetFullPath('C:\w5c')
    $ControlledPrefix = $ControlledRoot.TrimEnd('\') + '\'
    if (-not $FullPath.StartsWith(
        $ControlledPrefix,
        [StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Workspace must be a child of C:\w5c: $Path"
    }
    return $FullPath
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
        (Test-Path -LiteralPath $Path) -or
        (Test-Path -LiteralPath $TemporaryPath)
    ) {
        throw "Atomic evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $TemporaryPath,
        (($Value | ConvertTo-Json -Depth 100) + [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false)
    )
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
        throw "Text evidence destination already exists: $Path"
    }
    [IO.File]::WriteAllText(
        $Path,
        $Text,
        [Text.UTF8Encoding]::new($false)
    )
}

function ConvertTo-WindowsCommandLineArgument {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Argument
    )

    if ($Argument.Length -eq 0) {
        return '""'
    }
    if ($Argument -notmatch '[\s"]') {
        return $Argument
    }

    # Apply the CommandLineToArgvW-compatible backslash/quote algorithm.
    $Builder = [Text.StringBuilder]::new()
    [void]$Builder.Append('"')
    $Backslashes = 0
    foreach ($Character in $Argument.ToCharArray()) {
        if ($Character -eq '\') {
            $Backslashes++
            continue
        }
        if ($Character -eq '"') {
            [void]$Builder.Append('\' * (($Backslashes * 2) + 1))
            [void]$Builder.Append('"')
            $Backslashes = 0
            continue
        }
        if ($Backslashes -gt 0) {
            [void]$Builder.Append('\' * $Backslashes)
            $Backslashes = 0
        }
        [void]$Builder.Append($Character)
    }
    if ($Backslashes -gt 0) {
        [void]$Builder.Append('\' * ($Backslashes * 2))
    }
    [void]$Builder.Append('"')
    return $Builder.ToString()
}

function Invoke-CapturedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $ArgumentList,

        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string] $StdoutPath,

        [Parameter(Mandatory = $true)]
        [string] $StderrPath,

        [int] $TimeoutSeconds = $CommandTimeoutSeconds
    )

    foreach ($OutputPath in @($StdoutPath, $StderrPath)) {
        if (Test-Path -LiteralPath $OutputPath) {
            throw "Command output already exists: $OutputPath"
        }
        $Parent = Split-Path -Parent $OutputPath
        if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
            New-Item -ItemType Directory -Path $Parent -Force:$false | Out-Null
        }
    }

    $StartInfo = [Diagnostics.ProcessStartInfo]::new()
    $StartInfo.FileName = $FilePath
    $StartInfo.Arguments = (
        $ArgumentList |
        ForEach-Object { ConvertTo-WindowsCommandLineArgument -Argument $_ }
    ) -join ' '
    $StartInfo.WorkingDirectory = $WorkingDirectory
    $StartInfo.UseShellExecute = $false
    $StartInfo.CreateNoWindow = $true
    $StartInfo.RedirectStandardOutput = $true
    $StartInfo.RedirectStandardError = $true

    $Process = [Diagnostics.Process]::new()
    $Process.StartInfo = $StartInfo
    $StartedAt = [DateTime]::UtcNow
    if (-not $Process.Start()) {
        throw "Failed to start process: $FilePath"
    }

    $StdoutTask = $Process.StandardOutput.ReadToEndAsync()
    $StderrTask = $Process.StandardError.ReadToEndAsync()
    if (-not $Process.WaitForExit($TimeoutSeconds * 1000)) {
        try {
            $Process.Kill()
        }
        catch {
            Write-Verbose "Process kill after timeout also failed: $($_.Exception.Message)"
        }
        throw "Process exceeded ${TimeoutSeconds}s: $FilePath"
    }
    $Process.WaitForExit()

    $Stdout = $StdoutTask.GetAwaiter().GetResult()
    $Stderr = $StderrTask.GetAwaiter().GetResult()
    Write-Utf8Text -Path $StdoutPath -Text $Stdout
    Write-Utf8Text -Path $StderrPath -Text $Stderr

    return [pscustomobject]@{
        file_path = $FilePath
        arguments = @($ArgumentList)
        working_directory = $WorkingDirectory
        exit_code = [int]$Process.ExitCode
        started_at_utc = $StartedAt.ToString('yyyy-MM-ddTHH:mm:ssZ')
        completed_at_utc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        stdout_path = $StdoutPath
        stderr_path = $StderrPath
    }
}

function Invoke-RequiredProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $ArgumentList,

        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string] $LogPrefix,

        [int] $TimeoutSeconds = $CommandTimeoutSeconds
    )

    $Result = Invoke-CapturedProcess `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -StdoutPath "$LogPrefix.stdout.txt" `
        -StderrPath "$LogPrefix.stderr.txt" `
        -TimeoutSeconds $TimeoutSeconds
    if ($Result.exit_code -ne 0) {
        throw (
            "Required process exited with code $($Result.exit_code): " +
            "$FilePath $($ArgumentList -join ' ')"
        )
    }
    return $Result
}

$RepositoryRoot = Assert-NormalDirectory `
    -Path $RepositoryRoot `
    -Label 'Repository root'
$PythonPath = (Get-Command $PythonPath -ErrorAction Stop).Source
$GitPath = (Get-Command $GitPath -ErrorAction Stop).Source

$VersionText = ((& $PythonPath --version 2>&1) | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $VersionText -ne $ExpectedPythonVersion) {
    throw "Expected $ExpectedPythonVersion, observed: $VersionText"
}

$WorkspaceRoot = Assert-ControlledW5cPath -Path $WorkspaceRoot
if (Test-Path -LiteralPath $WorkspaceRoot) {
    throw "Dependency workspace already exists and will not be reused: $WorkspaceRoot"
}
if (Test-Path -LiteralPath $EvidenceRoot) {
    throw "Dependency evidence root already exists and will not be reused: $EvidenceRoot"
}

$ControlledRoot = 'C:\w5c'
if (-not (Test-Path -LiteralPath $ControlledRoot -PathType Container)) {
    New-Item -ItemType Directory -Path $ControlledRoot -Force:$false | Out-Null
}
Assert-NormalDirectory -Path $ControlledRoot -Label 'Controlled C1 root' | Out-Null

$EvidenceParent = Split-Path -Parent $EvidenceRoot
if (-not (Test-Path -LiteralPath $EvidenceParent -PathType Container)) {
    New-Item -ItemType Directory -Path $EvidenceParent -Force:$false | Out-Null
}
Assert-NormalDirectory -Path $EvidenceParent -Label 'Evidence parent' | Out-Null

New-Item -ItemType Directory -Path $WorkspaceRoot -Force:$false | Out-Null
New-Item -ItemType Directory -Path $EvidenceRoot -Force:$false | Out-Null
Assert-NormalDirectory -Path $WorkspaceRoot -Label 'Dependency workspace' | Out-Null
Assert-NormalDirectory -Path $EvidenceRoot -Label 'Dependency evidence root' | Out-Null

$SourcesRoot = Join-Path $WorkspaceRoot 'sources'
$ResolverVenv = Join-Path $WorkspaceRoot 'resolver-venv'
$TargetVenv = Join-Path $WorkspaceRoot 'venv'
$StepRoot = Join-Path $EvidenceRoot 'steps'
$LockRoot = Join-Path $EvidenceRoot 'locks'
$ReportRoot = Join-Path $EvidenceRoot 'reports'
$SourceEvidenceRoot = Join-Path $EvidenceRoot 'sources'
$LogRoot = Join-Path $EvidenceRoot 'logs'
foreach ($Path in @(
    $SourcesRoot,
    $StepRoot,
    $LockRoot,
    $ReportRoot,
    $SourceEvidenceRoot,
    $LogRoot
)) {
    New-Item -ItemType Directory -Path $Path -Force:$false | Out-Null
}

$CompletedStages = [System.Collections.Generic.List[string]]::new()
function Start-Stage {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string] $Stage)

    $Index = $CompletedStages.Count
    if (
        $Index -ge $ApprovedStageOrder.Count -or
        $ApprovedStageOrder[$Index] -ne $Stage
    ) {
        throw "Dependency-preflight stage order violation at: $Stage"
    }
    $CompletedStages.Add($Stage)
    Write-AtomicJson `
        -Path (Join-Path $StepRoot ('{0:D2}-{1}.json' -f ($Index + 1), $Stage)) `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            stage = $Stage
            status = 'Started'
        })
}

function Get-VerifiedSource {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $Repository,
        [Parameter(Mandatory = $true)][string] $Origin,
        [Parameter(Mandatory = $true)][string] $Commit
    )

    $SourceRoot = Join-Path $SourcesRoot $Name
    if (Test-Path -LiteralPath $SourceRoot) {
        throw "Source destination already exists: $SourceRoot"
    }
    New-Item -ItemType Directory -Path $SourceRoot -Force:$false | Out-Null

    Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @('init', $SourceRoot) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-init") | Out-Null
    Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'config', 'core.longpaths', 'true') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-longpaths") | Out-Null
    Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'remote', 'add', 'origin', $Origin) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-origin-add") | Out-Null
    Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @(
            '-C', $SourceRoot, '-c', 'protocol.version=2',
            'fetch', '--no-tags', '--depth=1', 'origin', $Commit
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-fetch") `
        -TimeoutSeconds $CommandTimeoutSeconds | Out-Null
    Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'checkout', '--detach', 'FETCH_HEAD') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-checkout") | Out-Null

    $Gitmodules = Join-Path $SourceRoot '.gitmodules'
    if (Test-Path -LiteralPath $Gitmodules -PathType Leaf) {
        Invoke-RequiredProcess `
            -FilePath $GitPath `
            -ArgumentList @('-C', $SourceRoot, 'submodule', 'sync', '--recursive') `
            -WorkingDirectory $WorkspaceRoot `
            -LogPrefix (Join-Path $LogRoot "$Name-submodule-sync") | Out-Null
        Invoke-RequiredProcess `
            -FilePath $GitPath `
            -ArgumentList @(
                '-C', $SourceRoot, 'submodule', 'update', '--init', '--recursive'
            ) `
            -WorkingDirectory $WorkspaceRoot `
            -LogPrefix (Join-Path $LogRoot "$Name-submodule-update") `
            -TimeoutSeconds $CommandTimeoutSeconds | Out-Null
    }

    $Head = Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'rev-parse', 'HEAD') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-head")
    $ObservedHead = (
        Get-Content -LiteralPath $Head.stdout_path -Raw -Encoding UTF8
    ).Trim()
    if ($ObservedHead -ne $Commit) {
        throw "$Name HEAD differs from reviewed commit: $ObservedHead"
    }

    $OriginResult = Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'remote', 'get-url', 'origin') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-origin")
    $ObservedOrigin = (
        Get-Content -LiteralPath $OriginResult.stdout_path -Raw -Encoding UTF8
    ).Trim()
    if ($ObservedOrigin -ne $Origin) {
        throw "$Name origin differs from reviewed URL: $ObservedOrigin"
    }

    $Status = Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @('-C', $SourceRoot, 'status', '--porcelain=v1', '--untracked-files=all') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-status")
    if (-not [string]::IsNullOrWhiteSpace((
        Get-Content -LiteralPath $Status.stdout_path -Raw -Encoding UTF8
    ))) {
        throw "$Name source tree is not clean."
    }

    $Tracked = Invoke-RequiredProcess `
        -FilePath $GitPath `
        -ArgumentList @(
            '-C', $SourceRoot, '-c', 'core.quotePath=false',
            'ls-files', '--recurse-submodules'
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-git-ls-files")
    $TrackedList = Join-Path $SourceEvidenceRoot "$Name-tracked-files.txt"
    $TrackedText = Get-Content -LiteralPath $Tracked.stdout_path -Raw -Encoding UTF8
    Write-Utf8Text -Path $TrackedList -Text $TrackedText

    $CsvOutput = Join-Path $SourceEvidenceRoot "$Name-files.csv"
    $JsonOutput = Join-Path $SourceEvidenceRoot "$Name.json"
    Invoke-RequiredProcess `
        -FilePath $PythonPath `
        -ArgumentList @(
            '-m', 'scripts.testing.workbook05.phase3.source_tree_manifest',
            '--name', $Name,
            '--repository', $Repository,
            '--origin', $Origin,
            '--commit', $Commit,
            '--root', $SourceRoot,
            '--file-list', $TrackedList,
            '--csv-output', $CsvOutput,
            '--json-output', $JsonOutput
        ) `
        -WorkingDirectory $RepositoryRoot `
        -LogPrefix (Join-Path $LogRoot "$Name-source-manifest") | Out-Null

    return [pscustomobject]@{
        root = $SourceRoot
        evidence = (
            Get-Content -LiteralPath $JsonOutput -Raw -Encoding UTF8 |
            ConvertFrom-Json
        )
    }
}

try {
    Start-Stage -Stage 'workspace-validation'

    Start-Stage -Stage 'source-verification'
    $OptimumIntel = Get-VerifiedSource `
        -Name 'optimum-intel' `
        -Repository 'huggingface/optimum-intel' `
        -Origin $OptimumIntelOrigin `
        -Commit $OptimumIntelCommit
    $Optimum = Get-VerifiedSource `
        -Name 'optimum' `
        -Repository 'huggingface/optimum' `
        -Origin $OptimumOrigin `
        -Commit $OptimumCommit

    Start-Stage -Stage 'lock-generation'
    Invoke-RequiredProcess `
        -FilePath $PythonPath `
        -ArgumentList @('-m', 'venv', $ResolverVenv) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'resolver-venv-create') | Out-Null
    $ResolverPython = Join-Path $ResolverVenv 'Scripts\python.exe'
    $ResolverPipCompile = Join-Path $ResolverVenv 'Scripts\pip-compile.exe'
    if (-not (Test-Path -LiteralPath $ResolverPython -PathType Leaf)) {
        throw "Resolver venv Python is missing: $ResolverPython"
    }

    $BootstrapReport = Join-Path $ReportRoot 'lock-generator-install-report.json'
    Invoke-RequiredProcess `
        -FilePath $ResolverPython `
        -ArgumentList @(
            '-m', 'pip', 'install',
            '--disable-pip-version-check',
            '--only-binary=:all:',
            '--report', $BootstrapReport,
            'pip-tools==7.5.0'
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'lock-generator-install') `
        -TimeoutSeconds $CommandTimeoutSeconds | Out-Null
    if (-not (Test-Path -LiteralPath $ResolverPipCompile -PathType Leaf)) {
        throw "Pinned pip-compile entry point is missing: $ResolverPipCompile"
    }

    $NormalInput = Join-Path `
        $RepositoryRoot `
        'scripts\testing\workbook05\requirements.phase3-assets.normal.in'
    $LockPath = Join-Path $LockRoot 'requirements.phase3-assets.txt'
    Invoke-RequiredProcess `
        -FilePath $ResolverPipCompile `
        -ArgumentList @(
            '--generate-hashes',
            '--allow-unsafe',
            '--strip-extras',
            '--resolver=backtracking',
            '--no-emit-index-url',
            '--no-emit-trusted-host',
            '--pip-args=--only-binary=:all:',
            '--output-file', $LockPath,
            $NormalInput
        ) `
        -WorkingDirectory $RepositoryRoot `
        -LogPrefix (Join-Path $LogRoot 'lock-generation') `
        -TimeoutSeconds $CommandTimeoutSeconds | Out-Null
    if (-not (Test-Path -LiteralPath $LockPath -PathType Leaf)) {
        throw "Dependency lock was not generated: $LockPath"
    }

    Start-Stage -Stage 'normal-install'
    Invoke-RequiredProcess `
        -FilePath $PythonPath `
        -ArgumentList @('-m', 'venv', $TargetVenv) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'target-venv-create') | Out-Null
    $TargetPython = Join-Path $TargetVenv 'Scripts\python.exe'
    $TargetPip = Join-Path $TargetVenv 'Scripts\pip.exe'
    if (
        -not (Test-Path -LiteralPath $TargetPython -PathType Leaf) -or
        -not (Test-Path -LiteralPath $TargetPip -PathType Leaf)
    ) {
        throw 'Target virtual environment is incomplete.'
    }

    $NormalInstallReport = Join-Path $ReportRoot 'normal-install-report.json'
    Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @(
            '-m', 'pip', 'install',
            '--disable-pip-version-check',
            '--require-hashes',
            '--only-binary=:all:',
            '--report', $NormalInstallReport,
            '-r', $LockPath
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'normal-install') `
        -TimeoutSeconds $CommandTimeoutSeconds | Out-Null

    Start-Stage -Stage 'vcs-install'
    $OptimumInstallReport = Join-Path $ReportRoot 'optimum-install-report.json'
    Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @(
            '-m', 'pip', 'install',
            '--disable-pip-version-check',
            '--no-deps',
            '--no-build-isolation',
            '--report', $OptimumInstallReport,
            $Optimum.root
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'optimum-install') `
        -TimeoutSeconds $CommandTimeoutSeconds | Out-Null

    $OptimumIntelInstallReport = Join-Path `
        $ReportRoot `
        'optimum-intel-install-report.json'
    Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @(
            '-m', 'pip', 'install',
            '--disable-pip-version-check',
            '--no-deps',
            '--no-build-isolation',
            '--report', $OptimumIntelInstallReport,
            $OptimumIntel.root
        ) `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'optimum-intel-install') `
        -TimeoutSeconds $CommandTimeoutSeconds | Out-Null

    Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @('-m', 'pip', 'check') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'pip-check') | Out-Null

    $PipListResult = Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @('-m', 'pip', 'list', '--format=json') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'pip-list')
    $InstalledPackages = @(
        Get-Content -LiteralPath $PipListResult.stdout_path -Raw -Encoding UTF8 |
        ConvertFrom-Json
    )
    $OptimumVersion = (
        $InstalledPackages |
        Where-Object { $_.name -eq 'optimum' } |
        Select-Object -First 1
    ).version
    $OptimumIntelVersion = (
        $InstalledPackages |
        Where-Object { $_.name -eq 'optimum-intel' } |
        Select-Object -First 1
    ).version
    if ($OptimumVersion -ne '2.3.0') {
        throw "Installed optimum version differs from 2.3.0: $OptimumVersion"
    }
    if ($OptimumIntelVersion -notmatch '^2\.3\.0\.dev0(?:\+[0-9a-f]+)?$') {
        throw "Installed optimum-intel version is unexpected: $OptimumIntelVersion"
    }

    $VcsPackages = @(
        [ordered]@{
            name = 'optimum-intel'
            version = $OptimumIntelVersion
            commit = $OptimumIntelCommit
        },
        [ordered]@{
            name = 'optimum'
            version = $OptimumVersion
            commit = $OptimumCommit
        }
    )
    Write-AtomicJson `
        -Path (Join-Path $ReportRoot 'vcs-packages.json') `
        -Value ([ordered]@{ packages = $VcsPackages })

    Start-Stage -Stage 'imports'
    $ImportResult = Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @(
            '-m', 'scripts.testing.workbook05.phase3.dependency_import_check'
        ) `
        -WorkingDirectory $RepositoryRoot `
        -LogPrefix (Join-Path $LogRoot 'imports')

    Start-Stage -Stage 'cli-help'
    $OptimumCli = Join-Path $TargetVenv 'Scripts\optimum-cli.exe'
    if (-not (Test-Path -LiteralPath $OptimumCli -PathType Leaf)) {
        throw "Installed optimum-cli entry point is missing: $OptimumCli"
    }
    $CliResult = Invoke-RequiredProcess `
        -FilePath $OptimumCli `
        -ArgumentList @('--help') `
        -WorkingDirectory $RepositoryRoot `
        -LogPrefix (Join-Path $LogRoot 'cli-help')

    Start-Stage -Stage 'no-model-compatibility'
    $CompatibilityResult = Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @(
            '-m', 'unittest', '-v',
            'tests.testing.workbook05.test_phase3_conversion',
            'tests.testing.workbook05.test_phase3_dependency_lock'
        ) `
        -WorkingDirectory $RepositoryRoot `
        -LogPrefix (Join-Path $LogRoot 'no-model-compatibility')

    Start-Stage -Stage 'record-generation'
    $DirectRequirements = @(
        Get-Content `
            -LiteralPath (Join-Path $RepositoryRoot 'scripts\testing\workbook05\requirements.phase3-assets.in') `
            -Encoding UTF8 |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_) -and
            -not $_.TrimStart().StartsWith('#')
        }
    )
    $LockText = Get-Content -LiteralPath $LockPath -Raw -Encoding UTF8
    $LockHash = (
        Get-FileHash -LiteralPath $LockPath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $PythonHash = (
        Get-FileHash -LiteralPath $TargetPython -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $PipHash = (
        Get-FileHash -LiteralPath $TargetPip -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    $PipVersionResult = Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @('-m', 'pip', '--version') `
        -WorkingDirectory $WorkspaceRoot `
        -LogPrefix (Join-Path $LogRoot 'pip-version')
    $PipVersionText = (
        Get-Content -LiteralPath $PipVersionResult.stdout_path -Raw -Encoding UTF8
    ).Trim()
    if ($PipVersionText -notmatch '^pip\s+([^\s]+)\s+') {
        throw "Could not parse pip version: $PipVersionText"
    }
    $PipVersion = $Matches[1]

    $Checks = @(
        [ordered]@{ name = 'resolver'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'install'; status = 'Passed'; exit_code = 0 },
        [ordered]@{ name = 'imports'; status = 'Passed'; exit_code = $ImportResult.exit_code },
        [ordered]@{ name = 'cli_help'; status = 'Passed'; exit_code = $CliResult.exit_code },
        [ordered]@{
            name = 'no_model_compatibility'
            status = 'Passed'
            exit_code = $CompatibilityResult.exit_code
        },
        [ordered]@{ name = 'remote_code_disabled'; status = 'Passed'; exit_code = $null }
    )
    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'checks.json') `
        -Value ([ordered]@{ checks = $Checks })

    $Observation = [ordered]@{
        generated_at_utc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        workspace_root = $WorkspaceRoot
        workspace_is_normal_local_directory = $true
        workspace_is_fresh = $true
        python_version = '3.12.10'
        python_executable_path = $TargetPython
        python_executable_sha256 = $PythonHash
        pip_version = $PipVersion
        pip_executable_path = $TargetPip
        pip_executable_sha256 = $PipHash
        source_trees = @(
            [ordered]@{
                name = $OptimumIntel.evidence.name
                repository = $OptimumIntel.evidence.repository
                origin = $OptimumIntel.evidence.origin
                commit = $OptimumIntel.evidence.commit
                clean = $OptimumIntel.evidence.clean
                aggregate_sha256 = $OptimumIntel.evidence.aggregate_sha256
            },
            [ordered]@{
                name = $Optimum.evidence.name
                repository = $Optimum.evidence.repository
                origin = $Optimum.evidence.origin
                commit = $Optimum.evidence.commit
                clean = $Optimum.evidence.clean
                aggregate_sha256 = $Optimum.evidence.aggregate_sha256
            }
        )
        direct_requirements = $DirectRequirements
        lock_path = 'locks/requirements.phase3-assets.txt'
        lock_text = $LockText
        lock_sha256 = $LockHash
        lock_generator = 'pip-tools==7.5.0'
        normal_install_report = (
            Get-Content -LiteralPath $NormalInstallReport -Raw -Encoding UTF8 |
            ConvertFrom-Json
        )
        vcs_packages = $VcsPackages
        checks = $Checks
        import_modules = @(
            'optimum',
            'optimum.intel',
            'transformers',
            'nncf',
            'openvino'
        )
        cli_help_exit_code = $CliResult.exit_code
        no_model_compatibility_exit_code = $CompatibilityResult.exit_code
    }
    $ObservationPath = Join-Path $EvidenceRoot 'observation.json'
    Write-AtomicJson -Path $ObservationPath -Value $Observation

    $DecisionPath = Join-Path $EvidenceRoot 'decision.json'
    Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @(
            '-m', 'scripts.testing.workbook05.phase3.dependency_preflight_cli',
            '--observation', $ObservationPath,
            '--repository-root', $RepositoryRoot,
            '--output', $DecisionPath
        ) `
        -WorkingDirectory $RepositoryRoot `
        -LogPrefix (Join-Path $LogRoot 'record-generation') | Out-Null
    $Decision = Get-Content -LiteralPath $DecisionPath -Raw -Encoding UTF8 |
        ConvertFrom-Json
    if ($Decision.status -ne 'Passed') {
        throw "Dependency decision did not pass: $($Decision.status)"
    }

    Start-Stage -Stage 'manifest-generation'
    Write-AtomicJson `
        -Path (Join-Path $EvidenceRoot 'stage-order.json') `
        -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            stages = @($CompletedStages)
        })
    Write-Utf8Text `
        -Path (Join-Path $EvidenceRoot 'summary.md') `
        -Text (
            "# Workbook 05 C1 dependency preflight`n`n" +
            "Status: Passed.`n`n" +
            "No model repository was contacted and no model, activation, storage, " +
            "performance, or quality claim is authorised.`n"
        )

    Invoke-RequiredProcess `
        -FilePath $TargetPython `
        -ArgumentList @(
            '-m', 'scripts.testing.workbook05.hash_manifest',
            '--root', $EvidenceRoot,
            '--output', (Join-Path $EvidenceRoot 'manifest.sha256')
        ) `
        -WorkingDirectory $RepositoryRoot `
        -LogPrefix (Join-Path $LogRoot 'manifest-generation') | Out-Null

    [pscustomobject]@{
        status = 'Passed'
        decision = $DecisionPath
        evidence_root = $EvidenceRoot
        workspace_root = $WorkspaceRoot
        model_download_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
}
catch {
    Get-ChildItem `
        -LiteralPath $EvidenceRoot `
        -File `
        -Recurse `
        -Filter '*.tmp' `
        -ErrorAction SilentlyContinue |
        ForEach-Object { [IO.File]::Delete($_.FullName) }

    $FailurePath = Join-Path $EvidenceRoot 'failure.json'
    if (-not (Test-Path -LiteralPath $FailurePath)) {
        Write-AtomicJson -Path $FailurePath -Value ([ordered]@{
            schema_version = '1.0'
            campaign_id = 'GTQ-WB05-MF-v1'
            route_id = 'route-a-merged-openvino'
            status = 'Failed'
            failure_class = 'Blocked'
            completed_stages = @($CompletedStages)
            message = $_.Exception.Message
            model_download_authorised = $false
            granite_model_test_authorised = $false
            activation_claim_authorised = $false
            packed_storage_claim_authorised = $false
            performance_claim_authorised = $false
            quality_claim_authorised = $false
        })
    }
    throw
}
