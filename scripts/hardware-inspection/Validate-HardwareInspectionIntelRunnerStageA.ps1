[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Phase,

    [Parameter(Mandatory = $true)]
    [string]$ControlRoot,

    [Parameter(Mandatory = $true)]
    [string]$WorkflowRef,

    [Parameter(Mandatory = $true)]
    [string]$DefaultBranch,

    [Parameter(Mandatory = $true)]
    [string]$Actor,

    [Parameter(Mandatory = $true)]
    [string]$TriggeringActor,

    [Parameter(Mandatory = $true)]
    [string]$RepositoryOwner,

    [Parameter(Mandatory = $true)]
    [string]$RunAttempt,

    [Parameter(Mandatory = $true)]
    [string]$Confirmation,

    [Parameter(Mandatory = $true)]
    [string]$RunnerLabel,

    [string]$SourceCheckoutRoot,
    [string]$EvaluatedRoot,
    [string]$ApprovedSha,
    [string]$GitHubOutputPath,
    [string]$SummaryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Stop-StageAValidation {
    [Console]::Error.WriteLine('HI-RUNNER-STAGEA-INVALID: authorised deterministic validation failed.')
    exit 1
}

function Test-UnsafePathPrefix {
    param([string]$Path)

    return $Path -match '^(?:\\\\|//|\\\\\?|\\\\\.|\\\?\?)'
}

function Assert-NoReparseComponent {
    param([string]$FullPath)

    $root = [System.IO.Path]::GetPathRoot($FullPath)
    if ([string]::IsNullOrEmpty($root)) {
        throw 'Path root is unavailable.'
    }
    $rootItem = Get-Item -LiteralPath $root -Force
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Path root is not normal.'
    }
    $remainder = $FullPath.Substring($root.Length).TrimEnd([char]'\', [char]'/')
    $current = $root
    if (-not [string]::IsNullOrEmpty($remainder)) {
        foreach ($component in ($remainder -split '[\\/]+')) {
            if ([string]::IsNullOrEmpty($component)) {
                continue
            }
            $current = Join-Path -Path $current -ChildPath $component
            if (Test-Path -LiteralPath $current) {
                $item = Get-Item -LiteralPath $current -Force
                if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw 'Path contains a reparse component.'
                }
            }
        }
    }
}

function Resolve-NormalExistingDirectory {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or (Test-UnsafePathPrefix -Path $Path)) {
        throw 'Directory path is invalid.'
    }
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (Test-UnsafePathPrefix -Path $fullPath) {
        throw 'Directory path is invalid.'
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw 'Directory is unavailable.'
    }
    Assert-NoReparseComponent -FullPath $fullPath
    return $fullPath
}

function Resolve-NormalExistingFile {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or (Test-UnsafePathPrefix -Path $Path)) {
        throw 'File path is invalid.'
    }
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (Test-UnsafePathPrefix -Path $fullPath) {
        throw 'File path is invalid.'
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw 'File is unavailable.'
    }
    Assert-NoReparseComponent -FullPath $fullPath
    return $fullPath
}

function Resolve-NormalOutputPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or (Test-UnsafePathPrefix -Path $Path)) {
        throw 'Output path is invalid.'
    }
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (Test-UnsafePathPrefix -Path $fullPath) {
        throw 'Output path is invalid.'
    }
    $parent = [System.IO.Path]::GetDirectoryName($fullPath)
    if ([string]::IsNullOrEmpty($parent)) {
        throw 'Output parent is invalid.'
    }
    $null = Resolve-NormalExistingDirectory -Path $parent
    if (Test-Path -LiteralPath $fullPath) {
        $null = Resolve-NormalExistingFile -Path $fullPath
    }
    return $fullPath
}

function Get-ApprovedManifestSha {
    param([string]$Root)

    $manifestPath = Join-Path -Path $Root -ChildPath '.github\hardware-inspection\llmfit-gate1-approved-source.json'
    $manifestPath = Resolve-NormalExistingFile -Path $manifestPath
    $bytes = [System.IO.File]::ReadAllBytes($manifestPath)
    if ($bytes.Length -lt 1 -or $bytes.Length -gt 4096) {
        throw 'Manifest size is invalid.'
    }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw 'Manifest has a byte-order mark.'
    }
    $encoding = New-Object System.Text.UTF8Encoding($false, $true)
    $text = $encoding.GetString($bytes)
    $match = [System.Text.RegularExpressions.Regex]::Match(
        $text,
        '\A[ \t\r\n]*\{[ \t\r\n]*"schemaVersion"[ \t\r\n]*:[ \t\r\n]*"1\.0"[ \t\r\n]*,[ \t\r\n]*"remoteFeatureRef"[ \t\r\n]*:[ \t\r\n]*"refs/heads/feature/hardware-inspection"[ \t\r\n]*,[ \t\r\n]*"approvedTipSha"[ \t\r\n]*:[ \t\r\n]*"((?!0{40}")[0-9a-f]{40})"[ \t\r\n]*\}[ \t\r\n]*\z',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
    )
    if (-not $match.Success) {
        throw 'Manifest schema is invalid.'
    }
    return $match.Groups[1].Value
}

function Assert-NoGitEnvironment {
    foreach ($name in [System.Environment]::GetEnvironmentVariables().Keys) {
        if (([string]$name).StartsWith('GIT_', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Git environment is present.'
        }
    }
}

function Resolve-GitApplication {
    $applications = @(
        Get-Command -Name git -CommandType Application -ErrorAction Stop |
            Select-Object -First 1
    )
    if ($applications.Count -ne 1 -or [string]::IsNullOrWhiteSpace($applications[0].Source)) {
        throw 'Git application is unavailable.'
    }
    return [string]$applications[0].Source
}

function Invoke-ExactGitLine {
    param(
        [string]$Root,
        [string]$GitApplication,
        [string[]]$GitArguments
    )

    $lines = @(& $GitApplication -C $Root @GitArguments 2>$null)
    if ($LASTEXITCODE -ne 0 -or $lines.Count -ne 1) {
        throw 'Git command did not return one line.'
    }
    return [string]$lines[0]
}

function Assert-ExactGitCheckout {
    param(
        [string]$Root,
        [string]$GitApplication
    )

    $expectedGitDirectory = Resolve-NormalExistingDirectory -Path (Join-Path -Path $Root -ChildPath '.git')
    $topLevel = Invoke-ExactGitLine -Root $Root -GitApplication $GitApplication -GitArguments @('rev-parse', '--show-toplevel')
    $normalTopLevel = Resolve-NormalExistingDirectory -Path $topLevel
    if (-not [string]::Equals($normalTopLevel, $Root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Git work tree does not match checkout root.'
    }
    $gitDirectory = Invoke-ExactGitLine -Root $Root -GitApplication $GitApplication -GitArguments @('rev-parse', '--absolute-git-dir')
    $normalGitDirectory = Resolve-NormalExistingDirectory -Path $gitDirectory
    if (-not [string]::Equals($normalGitDirectory, $expectedGitDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Git directory does not match checkout root.'
    }
}

function Get-ExactGitHead {
    param(
        [string]$Root,
        [string]$GitApplication
    )

    $head = Invoke-ExactGitLine -Root $Root -GitApplication $GitApplication -GitArguments @('rev-parse', 'HEAD')
    if ($head -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Git identity is invalid.'
    }
    return $head
}

function Assert-CleanGitCheckout {
    param(
        [string]$Root,
        [string]$GitApplication
    )

    $tracked = @(& $GitApplication -C $Root status --porcelain --untracked-files=no 2>$null)
    if ($LASTEXITCODE -ne 0 -or $tracked.Count -ne 0) {
        throw 'Git checkout is not clean.'
    }
    $untracked = @(& $GitApplication -C $Root ls-files --others -- 2>$null)
    if ($LASTEXITCODE -ne 0 -or $untracked.Count -ne 0) {
        throw 'Git checkout is not clean.'
    }
}

function Write-Utf8NoBomFile {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = [System.IO.Path]::GetDirectoryName($Path)
    $temporaryPath = Join-Path -Path $directory -ChildPath ('.stagea-' + [System.Guid]::NewGuid().ToString('N') + '.tmp')
    $stream = $null
    $writer = $null
    try {
        $stream = New-Object System.IO.FileStream(
            $temporaryPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None
        )
        $writer = New-Object System.IO.StreamWriter(
            $stream,
            (New-Object System.Text.UTF8Encoding($false)),
            1024,
            $true
        )
        $writer.Write($Content)
        $writer.Flush()
        $stream.Flush($true)
        $writer.Dispose()
        $writer = $null
        $stream.Dispose()
        $stream = $null
        if ([System.IO.File]::Exists($Path)) {
            [System.IO.File]::Replace($temporaryPath, $Path, $null)
        }
        else {
            [System.IO.File]::Move($temporaryPath, $Path)
        }
        $temporaryPath = $null
    }
    finally {
        if ($null -ne $writer) {
            $writer.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
        if ($null -ne $temporaryPath -and [System.IO.File]::Exists($temporaryPath)) {
            [System.IO.File]::Delete($temporaryPath)
        }
    }
}

try {
    if (($Phase -cne 'Hosted' -and $Phase -cne 'Runner') -or
        $WorkflowRef -cne 'refs/heads/main' -or
        $DefaultBranch -cne 'main' -or
        $Actor -cne 'arian20020' -or
        $TriggeringActor -cne 'arian20020' -or
        $RepositoryOwner -cne 'arian20020' -or
        $RunAttempt -cne '1' -or
        $Confirmation -cne 'true' -or
        $RunnerLabel -cnotmatch '\Ahardware-gate1-[0-9a-f]{16}\z') {
        throw 'Dispatch context is invalid.'
    }

    $normalControlRoot = Resolve-NormalExistingDirectory -Path $ControlRoot
    $manifestSha = Get-ApprovedManifestSha -Root $normalControlRoot
    $sourceRef = 'refs/heads/feature/hardware-inspection'
    Assert-NoGitEnvironment
    $gitApplication = Resolve-GitApplication

    if ($Phase -ceq 'Hosted') {
        if ([string]::IsNullOrWhiteSpace($SourceCheckoutRoot) -or
            [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
            throw 'Hosted inputs are missing.'
        }
        $sourceRoot = Resolve-NormalExistingDirectory -Path $SourceCheckoutRoot
        Assert-ExactGitCheckout -Root $sourceRoot -GitApplication $gitApplication
        $sourceHead = Get-ExactGitHead -Root $sourceRoot -GitApplication $gitApplication
        if ($sourceHead -cne $manifestSha) {
            throw 'Source identity is not approved.'
        }
        Assert-CleanGitCheckout -Root $sourceRoot -GitApplication $gitApplication
        $outputPath = Resolve-NormalOutputPath -Path $GitHubOutputPath
        $output = 'source_ref=' + $sourceRef + [char]10 +
            'approved_sha=' + $manifestSha + [char]10 +
            'runner_label=' + $RunnerLabel + [char]10 +
            'eligible=true' + [char]10
        Write-Utf8NoBomFile -Path $outputPath -Content $output
        exit 0
    }

    if ([string]::IsNullOrWhiteSpace($ApprovedSha) -or
        [string]::IsNullOrWhiteSpace($EvaluatedRoot) -or
        [string]::IsNullOrWhiteSpace($SummaryPath) -or
        $ApprovedSha -cne $manifestSha) {
        throw 'Runner inputs are invalid.'
    }
    foreach ($environmentName in @(
        'GRANITE_LLMFIT_CANDIDATE_ROOT',
        'GRANITE_LLMFIT_TRUSTED_OUTPUT',
        'GRANITE_LLMFIT_GATE1_OUTPUT',
        'GRANITE_LLMFIT_WINDOWS_REFERENCE',
        'GRANITE_LLMFIT_OFFLINE_OUTPUT',
        'GRANITE_LLMFIT_FAKE_TOOL_ROOT'
    )) {
        if ($null -ne [System.Environment]::GetEnvironmentVariable($environmentName)) {
            throw 'Operational environment is present.'
        }
    }
    $evaluatedRoot = Resolve-NormalExistingDirectory -Path $EvaluatedRoot
    $candidateDirectory = Join-Path -Path $evaluatedRoot -ChildPath 'third-party\bin\llmfit\v1.1.9\win-x64'
    if (Test-Path -LiteralPath $candidateDirectory -PathType Container) {
        throw 'Candidate directory is present.'
    }
    Assert-ExactGitCheckout -Root $evaluatedRoot -GitApplication $gitApplication
    $evaluatedHead = Get-ExactGitHead -Root $evaluatedRoot -GitApplication $gitApplication
    if ($evaluatedHead -cne $manifestSha) {
        throw 'Evaluated identity is not approved.'
    }
    Assert-CleanGitCheckout -Root $evaluatedRoot -GitApplication $gitApplication
    $normalSummaryPath = Resolve-NormalOutputPath -Path $SummaryPath
    $summary = '# Stage A deterministic-only validation' + [char]10 + [char]10 +
        'Status: authorised deterministic validation complete.' + [char]10 +
        'Approved SHA: ' + $manifestSha + [char]10
    Write-Utf8NoBomFile -Path $normalSummaryPath -Content $summary
    exit 0
}
catch {
    Stop-StageAValidation
}
