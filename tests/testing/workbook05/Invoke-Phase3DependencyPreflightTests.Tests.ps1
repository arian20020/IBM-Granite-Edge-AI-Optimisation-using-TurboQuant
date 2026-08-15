[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $RepositoryRoot = '',

    [Parameter(Mandatory = $false)]
    [string] $PythonPath = 'python'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the exact repository under test rather than depending on the caller's
# current directory.
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (
        Resolve-Path `
            -LiteralPath (Join-Path $PSScriptRoot '..\..\..') `
            -ErrorAction Stop
    ).Path
}
else {
    $RepositoryRoot = (
        Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop
    ).Path
}

$Script = Join-Path `
    $RepositoryRoot `
    'scripts\testing\workbook05\Invoke-Workbook05Phase3DependencyPreflight.ps1'
if (-not (Test-Path -LiteralPath $Script -PathType Leaf)) {
    throw "Phase 3 dependency-preflight script is missing: $Script"
}
$SerializationProbeScript = Join-Path `
    $RepositoryRoot `
    'tests\testing\workbook05\Invoke-Phase3ObservationSerializationProbe.Tests.ps1'
if (-not (Test-Path -LiteralPath $SerializationProbeScript -PathType Leaf)) {
    throw "Dependency observation serialization probe is missing: $SerializationProbeScript"
}

$ScriptText = Get-Content -LiteralPath $Script -Raw -ErrorAction Stop

# The current package is intentionally an offline fixture. Its stage catalogue
# must stay stable so a later reviewed live implementation can use the same
# causal boundaries without silently skipping a prerequisite.
$ExpectedStages = @(
    'workspace-validation'
    'source-verification'
    'lock-generation'
    'normal-install'
    'vcs-install'
    'imports'
    'cli-help'
    'no-model-compatibility'
    'record-generation'
    'manifest-generation'
)
$PreviousIndex = -1
foreach ($Stage in $ExpectedStages) {
    $Index = $ScriptText.IndexOf("'$Stage'", [StringComparison]::Ordinal)
    if ($Index -lt 0) {
        throw "Dependency-preflight script does not declare stage: $Stage"
    }
    if ($Index -le $PreviousIndex) {
        throw "Dependency-preflight stage order is not deterministic at: $Stage"
    }
    $PreviousIndex = $Index
}

# Freeze the reviewed source identities and the explicit offline/non-authorising
# boundary. Real resolver/install controls do not belong in this fixture script.
foreach ($Required in @(
    'Python 3.12.10'
    'a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0'
    '982e495540364f95da1e4b6f62d2d4e5907d08fd'
    '--offline-fixture'
    'manifest.sha256'
    'Live dependency resolution is blocked in this revision.'
    'model_download_authorised = $false'
    'performance_claim_authorised = $false'
    'quality_claim_authorised = $false'
)) {
    if ($ScriptText.IndexOf($Required, [StringComparison]::Ordinal) -lt 0) {
        throw "Dependency-preflight script is missing staged control: $Required"
    }
}

# The offline rehearsal must not acquire a model, execute remote repository code,
# invoke a package/network process, or use dynamic command-string execution.
foreach ($Forbidden in @(
    '--trust-remote-code'
    'snapshot_download'
    'Invoke-Expression'
    'Start-Process'
    'Invoke-CapturedProcess'
    'git clone'
    'pip install'
    'git reset --hard'
    'git clean'
)) {
    if (
        $ScriptText.IndexOf(
            $Forbidden,
            [StringComparison]::OrdinalIgnoreCase
        ) -ge 0
    ) {
        throw "Offline dependency fixture contains forbidden token: $Forbidden"
    }
}

function Invoke-BoundedSerializationProbe {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $ProbeScript,
        [Parameter(Mandatory = $true)][string] $ProbeRepositoryRoot
    )

    # The regression probe must fail quickly instead of consuming the complete
    # five-minute GitHub Actions step when Windows PowerShell enters pathological
    # JSON object expansion.
    $PowerShellExecutable = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -LiteralPath $PowerShellExecutable -PathType Leaf)) {
        throw "Windows PowerShell executable is missing: $PowerShellExecutable"
    }
    if ($ProbeScript.Contains('"') -or $ProbeRepositoryRoot.Contains('"')) {
        throw 'Serialization probe paths must not contain quote characters.'
    }

    $StartInfo = [Diagnostics.ProcessStartInfo]::new()
    $StartInfo.FileName = $PowerShellExecutable
    $StartInfo.Arguments = (
        '-NoLogo -NoProfile -ExecutionPolicy Bypass ' +
        '-File "{0}" -RepositoryRoot "{1}"' -f
            $ProbeScript,
            $ProbeRepositoryRoot
    )
    $StartInfo.UseShellExecute = $false
    $StartInfo.CreateNoWindow = $true
    $StartInfo.RedirectStandardOutput = $true
    $StartInfo.RedirectStandardError = $true

    $Process = [Diagnostics.Process]::new()
    $Process.StartInfo = $StartInfo
    try {
        if (-not $Process.Start()) {
            throw 'Dependency observation serialization probe did not start.'
        }

        # Start both asynchronous readers before waiting so redirected buffers
        # cannot block a child that emits diagnostic field markers.
        $StandardOutputTask = $Process.StandardOutput.ReadToEndAsync()
        $StandardErrorTask = $Process.StandardError.ReadToEndAsync()

        if (-not $Process.WaitForExit(30000)) {
            try {
                $Process.Kill()
            }
            catch {
                # Preserve the timeout as the primary causal failure.
            }
            $Process.WaitForExit()
            $ProbeStdout = $StandardOutputTask.Result
            $ProbeStderr = $StandardErrorTask.Result
            if (-not [string]::IsNullOrWhiteSpace($ProbeStdout)) {
                Write-Host $ProbeStdout.TrimEnd()
            }
            if (-not [string]::IsNullOrWhiteSpace($ProbeStderr)) {
                Write-Host $ProbeStderr.TrimEnd()
            }
            throw 'Dependency observation serialization probe exceeded 30 seconds.'
        }

        # Wait once more for redirected stream completion after process exit.
        $Process.WaitForExit()
        $ProbeStdout = $StandardOutputTask.Result
        $ProbeStderr = $StandardErrorTask.Result
        if (-not [string]::IsNullOrWhiteSpace($ProbeStdout)) {
            Write-Host $ProbeStdout.TrimEnd()
        }
        if (-not [string]::IsNullOrWhiteSpace($ProbeStderr)) {
            Write-Host $ProbeStderr.TrimEnd()
        }
        if ($Process.ExitCode -ne 0) {
            throw (
                'Dependency observation serialization probe exited with code ' +
                $Process.ExitCode + '.'
            )
        }
        if (
            $ProbeStdout.IndexOf(
                'Workbook 05 dependency observation serialization probe passed.',
                [StringComparison]::Ordinal
            ) -lt 0
        ) {
            throw 'Dependency observation serialization probe omitted its pass marker.'
        }
    }
    finally {
        $Process.Dispose()
    }
}

$FixtureRoot = Join-Path `
    ([IO.Path]::GetTempPath()) `
    ('wb05-phase3-dependency-' + [guid]::NewGuid().ToString('N'))
$SuccessWorkspace = Join-Path $FixtureRoot 'workspace-success'
$SuccessEvidence = Join-Path $FixtureRoot 'evidence-success'
$FailureWorkspace = Join-Path $FixtureRoot 'workspace-failure'
$FailureEvidence = Join-Path $FixtureRoot 'evidence-failure'

try {
    # First isolate the exact top-level field that creates an unsafe object graph
    # for Windows PowerShell JSON serialization. This probe has no filesystem,
    # package, network, model, or scientific side effect.
    Write-Host 'WB05_DEP_TEST:serialization-probe:start'
    Invoke-BoundedSerializationProbe `
        -ProbeScript $SerializationProbeScript `
        -ProbeRepositoryRoot $RepositoryRoot
    Write-Host 'WB05_DEP_TEST:serialization-probe:complete'

    # Execute the complete offline fixture through its real public interface.
    Write-Host 'WB05_DEP_TEST:success-invocation:start'
    Set-PSDebug -Trace 1
    try {
        $Result = & $Script `
            -RepositoryRoot $RepositoryRoot `
            -WorkspaceRoot $SuccessWorkspace `
            -EvidenceRoot $SuccessEvidence `
            -PythonPath $PythonPath `
            -OfflineFixtureMode
    }
    finally {
        Set-PSDebug -Off
    }
    Write-Host 'WB05_DEP_TEST:success-invocation:return'
    if ($LASTEXITCODE -ne 0) {
        throw "Offline dependency fixture exited with code $LASTEXITCODE."
    }

    # The bundle is text-only and contains enough information for a separate
    # validator to recompute the blocked decision.
    foreach ($Relative in @(
        'decision.json'
        'observation.json'
        'locks\requirements.phase3-assets.txt'
        'reports\normal-install-report.json'
        'reports\vcs-packages.json'
        'sources\optimum.json'
        'sources\optimum-intel.json'
        'checks.json'
        'logs\imports.stdout.txt'
        'logs\imports.stderr.txt'
        'logs\cli-help.stdout.txt'
        'logs\cli-help.stderr.txt'
        'logs\no-model-compatibility.stdout.txt'
        'logs\no-model-compatibility.stderr.txt'
        'summary.md'
        'stage-order.json'
        'manifest.sha256'
    )) {
        $RequiredPath = Join-Path $SuccessEvidence $Relative
        if (-not (Test-Path -LiteralPath $RequiredPath -PathType Leaf)) {
            throw "Successful dependency fixture is missing: $Relative"
        }
    }

    # A rehearsal may prove orchestration only; it must remain Blocked and grant
    # no model or scientific authority.
    $Decision = Get-Content `
        -LiteralPath (Join-Path $SuccessEvidence 'decision.json') `
        -Raw |
        ConvertFrom-Json
    if ($Decision.status -ne 'Blocked') {
        throw "Offline dependency decision was not Blocked: $($Decision.status)"
    }
    foreach ($Flag in @(
        'model_download_authorised'
        'granite_model_test_authorised'
        'activation_claim_authorised'
        'packed_storage_claim_authorised'
        'performance_claim_authorised'
        'quality_claim_authorised'
    )) {
        if ($Decision.$Flag -ne $false) {
            throw "Offline dependency decision enabled forbidden flag: $Flag"
        }
    }
    if (@($Decision.checks).Count -ne 6) {
        throw 'Offline dependency decision did not record exactly six checks.'
    }

    # The synthetic lock still exercises deterministic hash parsing, while VCS
    # identities remain separately pinned rather than embedded as moving lines.
    $LockText = Get-Content `
        -LiteralPath (Join-Path $SuccessEvidence 'locks\requirements.phase3-assets.txt') `
        -Raw
    if ($LockText.IndexOf('--hash=sha256:', [StringComparison]::Ordinal) -lt 0) {
        throw 'Offline dependency lock does not contain SHA-256 rows.'
    }
    if (
        $LockText.IndexOf(
            'git+https://',
            [StringComparison]::OrdinalIgnoreCase
        ) -ge 0
    ) {
        throw 'Offline dependency lock contains a moving VCS requirement.'
    }

    # Prove that the recorded stages match the approved sequence byte-for-byte.
    $StageOrder = Get-Content `
        -LiteralPath (Join-Path $SuccessEvidence 'stage-order.json') `
        -Raw |
        ConvertFrom-Json
    $ObservedStages = @($StageOrder.stages)
    if (($ObservedStages -join '|') -ne ($ExpectedStages -join '|')) {
        throw (
            'Observed dependency stage order differs from the approved order: ' +
            ($ObservedStages -join ', ')
        )
    }

    $FinalResult = @($Result) | Select-Object -Last 1
    if ($FinalResult.status -ne 'Blocked') {
        throw "Offline dependency result was not Blocked: $($FinalResult.status)"
    }
    if ($FinalResult.model_download_authorised -ne $false) {
        throw 'Offline dependency result authorised model download.'
    }
    if (
        @(Get-ChildItem `
            -LiteralPath $SuccessEvidence `
            -Filter '*.tmp' `
            -Recurse `
            -File `
            -ErrorAction SilentlyContinue).Count -ne 0
    ) {
        throw 'Successful dependency fixture left temporary records behind.'
    }
    Write-Host 'WB05_DEP_TEST:success-assertions:complete'

    # Inject one causal failure and require preserved non-authorising evidence,
    # no acceptance manifest, and no leftover temporary files.
    $FailedAsExpected = $false
    Write-Host 'WB05_DEP_TEST:failure-invocation:start'
    try {
        & $Script `
            -RepositoryRoot $RepositoryRoot `
            -WorkspaceRoot $FailureWorkspace `
            -EvidenceRoot $FailureEvidence `
            -PythonPath $PythonPath `
            -OfflineFixtureMode `
            -FailureStage 'imports' > $null
    }
    catch {
        $FailedAsExpected = $true
        Write-Host 'WB05_DEP_TEST:failure-invocation:caught'
    }
    if (-not $FailedAsExpected) {
        throw 'Injected dependency fixture failure did not stop orchestration.'
    }

    $FailurePath = Join-Path $FailureEvidence 'failure.json'
    if (-not (Test-Path -LiteralPath $FailurePath -PathType Leaf)) {
        throw 'Failed dependency fixture did not retain failure.json.'
    }
    if (Test-Path -LiteralPath (Join-Path $FailureEvidence 'manifest.sha256')) {
        throw 'Failed dependency fixture created an acceptance manifest.'
    }
    $Failure = Get-Content -LiteralPath $FailurePath -Raw | ConvertFrom-Json
    foreach ($Flag in @(
        'model_download_authorised'
        'granite_model_test_authorised'
        'activation_claim_authorised'
        'packed_storage_claim_authorised'
        'performance_claim_authorised'
        'quality_claim_authorised'
    )) {
        if ($Failure.$Flag -ne $false) {
            throw "Failed dependency evidence enabled forbidden flag: $Flag"
        }
    }
    if (
        @(Get-ChildItem `
            -LiteralPath $FailureEvidence `
            -Filter '*.tmp' `
            -Recurse `
            -File `
            -ErrorAction SilentlyContinue).Count -ne 0
    ) {
        throw 'Failed dependency fixture left temporary records behind.'
    }
    Write-Host 'WB05_DEP_TEST:failure-assertions:complete'

    $global:LASTEXITCODE = 0
    Write-Host 'Workbook 05 Phase 3 dependency-fixture PowerShell tests passed.'
}
finally {
    Write-Host 'WB05_DEP_TEST:cleanup:start'
    if (Test-Path -LiteralPath $FixtureRoot) {
        Remove-Item `
            -LiteralPath $FixtureRoot `
            -Recurse `
            -Force `
            -ErrorAction SilentlyContinue
    }
    Write-Host 'WB05_DEP_TEST:cleanup:complete'
}
