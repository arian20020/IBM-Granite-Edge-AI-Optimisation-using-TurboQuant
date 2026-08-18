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
        '\A\s*\{\s*"schemaVersion"\s*:\s*"1\.0"\s*,\s*"remoteFeatureRef"\s*:\s*"refs/heads/feature/hardware-inspection"\s*,\s*"approvedTipSha"\s*:\s*"((?!0{40}")[0-9a-f]{40})"\s*\}\s*\z',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
    )
    if (-not $match.Success) {
        throw 'Manifest schema is invalid.'
    }
    return $match.Groups[1].Value
}

function Get-ExactGitHead {
    param([string]$Root)

    $null = Get-Command -Name git -ErrorAction Stop
    $lines = @(& git -C $Root rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $lines.Count -ne 1) {
        throw 'Git identity is unavailable.'
    }
    $head = [string]$lines[0]
    if ($head -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Git identity is invalid.'
    }
    return $head
}

function Assert-CleanGitCheckout {
    param([string]$Root)

    $tracked = @(& git -C $Root status --porcelain --untracked-files=no 2>$null)
    if ($LASTEXITCODE -ne 0 -or $tracked.Count -ne 0) {
        throw 'Git checkout is not clean.'
    }
    $untracked = @(& git -C $Root ls-files --others -- 2>$null)
    if ($LASTEXITCODE -ne 0 -or $untracked.Count -ne 0) {
        throw 'Git checkout is not clean.'
    }
}

function Write-Utf8NoBomFile {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
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

    if ($Phase -ceq 'Hosted') {
        if ([string]::IsNullOrWhiteSpace($SourceCheckoutRoot) -or
            [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
            throw 'Hosted inputs are missing.'
        }
        $sourceRoot = Resolve-NormalExistingDirectory -Path $SourceCheckoutRoot
        $sourceHead = Get-ExactGitHead -Root $sourceRoot
        if ($sourceHead -cne $manifestSha) {
            throw 'Source identity is not approved.'
        }
        Assert-CleanGitCheckout -Root $sourceRoot
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
    $evaluatedHead = Get-ExactGitHead -Root $evaluatedRoot
    if ($evaluatedHead -cne $manifestSha) {
        throw 'Evaluated identity is not approved.'
    }
    Assert-CleanGitCheckout -Root $evaluatedRoot
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
