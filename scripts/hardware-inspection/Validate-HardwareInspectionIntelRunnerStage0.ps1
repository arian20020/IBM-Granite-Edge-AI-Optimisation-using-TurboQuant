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
    [string]$ConfirmRepositoryOnly,

    [string]$SourceCheckoutRoot,
    [string]$GitHubOutputPath,
    [string]$SummaryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Fail-RepositoryOnlyValidation {
    [Console]::Error.WriteLine('HI-RUNNER-STAGE0-INVALID: repository-only validation failed.')
    exit 1
}

try {
    if (($Phase -cne 'Dispatch' -and $Phase -cne 'Source') -or
        $DefaultBranch -cne 'main' -or
        $WorkflowRef -cne 'refs/heads/main' -or
        $RepositoryOwner -cne 'arian20020' -or
        $Actor -cne $RepositoryOwner -or
        $TriggeringActor -cne $RepositoryOwner -or
        $RunAttempt -cne '1' -or
        $ConfirmRepositoryOnly -cne 'true') {
        throw 'Invalid repository-only dispatch context.'
    }

    if (-not (Test-Path -LiteralPath $ControlRoot -PathType Container)) {
        throw 'Control root is unavailable.'
    }

    $approvalDirectory = Join-Path -Path $ControlRoot -ChildPath '.github\\hardware-inspection'
    $manifestPath = Join-Path -Path $approvalDirectory -ChildPath 'llmfit-gate1-approved-source.json'
    if (-not (Test-Path -LiteralPath $approvalDirectory -PathType Container) -or
        -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw 'Approval manifest is unavailable.'
    }

    $bytes = [System.IO.File]::ReadAllBytes($manifestPath)
    if ($bytes.Length -lt 1 -or $bytes.Length -gt 4096) {
        throw 'Approval manifest length is invalid.'
    }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw 'Approval manifest has a byte-order mark.'
    }

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $manifestText = $strictUtf8.GetString($bytes)
    $manifestPattern = '^\s*\{\s*"schemaVersion"\s*:\s*"1\.0"\s*,\s*"remoteFeatureRef"\s*:\s*"refs/heads/feature/hardware-inspection"\s*,\s*"approvedTipSha"\s*:\s*"((?!0{40})[0-9a-f]{40})"\s*\}\s*$'
    if ($manifestText -cnotmatch $manifestPattern) {
        throw 'Approval manifest schema is invalid.'
    }
    $approvedSha = $Matches[1]
    $sourceRef = 'refs/heads/feature/hardware-inspection'

    if ($Phase -ceq 'Dispatch') {
        $dispatchOutput = 'source_ref=' + $sourceRef + [char]10 +
            'approved_sha=' + $approvedSha + [char]10 +
            'repository_only=true' + [char]10
        if (-not [string]::IsNullOrEmpty($GitHubOutputPath)) {
            Write-Utf8NoBomFile -Path $GitHubOutputPath -Content $dispatchOutput
        }
        [Console]::Out.Write($dispatchOutput)
        exit 0
    }

    if ([string]::IsNullOrEmpty($SourceCheckoutRoot) -or
        -not (Test-Path -LiteralPath $SourceCheckoutRoot -PathType Container)) {
        throw 'Source checkout is unavailable.'
    }

    $null = Get-Command -Name git -ErrorAction Stop
    $headLines = @(& git -C $SourceCheckoutRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw 'Source identity cannot be determined.'
    }
    $sourceSha = ($headLines -join [char]10).Trim()
    if ($sourceSha -cne $approvedSha) {
        throw 'Source identity does not match approval.'
    }

    $statusLines = @(& git -C $SourceCheckoutRoot status --porcelain --untracked-files=all 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($statusLines -join [char]10).Length -ne 0) {
        throw 'Source checkout is not clean.'
    }

    $summary = '# Hardware Inspection Intel runner preflight' + [char]10 + [char]10 +
        '- Stage 0 only.' + [char]10 +
        '- The Intel laptop was not contacted.' + [char]10 +
        '- The LLM Fit candidate was not acquired or executed.' + [char]10 +
        '- Gate 1 remains Blocked.' + [char]10 +
        '- Gate 2 is prohibited.' + [char]10 +
        '- Approved source ref: ' + $sourceRef + [char]10 +
        '- Approved source SHA: ' + $sourceSha + [char]10
    if (-not [string]::IsNullOrEmpty($SummaryPath)) {
        Write-Utf8NoBomFile -Path $SummaryPath -Content $summary
    }
    [Console]::Out.Write($summary)
    exit 0
}
catch {
    Fail-RepositoryOnlyValidation
}
