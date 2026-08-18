[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $EvaluatedRoot,
    [Parameter(Mandatory)][string] $ApprovedSha,
    [Parameter(Mandatory)][string] $LocalWorkRoot,
    [Parameter(Mandatory)][string] $SummaryJsonPath,
    [Parameter(Mandatory)][string] $SummaryMarkdownPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:StageAFailure = 'HI-RUNNER-STAGEA-TESTS-FAILED: deterministic validation failed.'
$script:StageATrxNamespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
$script:StageAMaximumTrxBytes = 16MB
$script:StageAMaximumProcessStreamBytes = 16MB # Per stdout/stderr log; excess is drained and discarded.
$script:StageAOwnedProcesses = New-Object System.Collections.ArrayList
$script:StageACancelled = $false

if ($null -eq ('HardwareInspection.StageA.CappedDrain' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Threading.Tasks;
namespace HardwareInspection.StageA {
    public static class CappedDrain {
        public static async Task<bool> CopyAsync(Stream source, string path, long maximumBytes) {
            var buffer = new byte[1048576]; long written = 0; bool truncated = false;
            using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1048576, true)) {
                int count;
                while ((count = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0) {
                    var accepted = (int)Math.Min((long)count, Math.Max(0L, maximumBytes - written));
                    if (accepted != 0) { await destination.WriteAsync(buffer, 0, accepted).ConfigureAwait(false); written += accepted; }
                    if (accepted != count) { truncated = true; }
                }
                await destination.FlushAsync().ConfigureAwait(false);
            }
            return truncated;
        }
    }
}
'@
}

function Assert-StageACondition {
    param([bool] $Condition)
    if (-not $Condition) { throw $script:StageAFailure }
}

function Test-StageANormalExistingPath {
    param([string] $Path, [bool] $Directory)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-StageACondition (-not ($fullPath.StartsWith('\\', [System.StringComparison]::Ordinal) -or $fullPath.StartsWith('\\?\', [System.StringComparison]::Ordinal)))
    $item = Get-Item -LiteralPath $fullPath -Force
    $component = $item
    while ($null -ne $component) {
        Assert-StageACondition (($component.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0)
        $component = $component.Parent
    }
    Assert-StageACondition ($item.PSIsContainer -eq $Directory)
    $drive = New-Object System.IO.DriveInfo($fullPath.Substring(0, 3))
    Assert-StageACondition ($drive.DriveType -eq [System.IO.DriveType]::Fixed)
    return $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
}

function Test-StageADisjointPaths {
    param([string] $First, [string] $Second)
    $separator = [System.IO.Path]::DirectorySeparatorChar
    Assert-StageACondition ($First -ine $Second)
    Assert-StageACondition (-not $First.StartsWith($Second + $separator, [System.StringComparison]::OrdinalIgnoreCase))
    Assert-StageACondition (-not $Second.StartsWith($First + $separator, [System.StringComparison]::OrdinalIgnoreCase))
}

function Get-StageAOutputPath {
    param([string] $Path, [string] $Evaluated)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    Assert-StageACondition (-not ($fullPath.StartsWith('\\', [System.StringComparison]::Ordinal) -or $fullPath.StartsWith('\\?\', [System.StringComparison]::Ordinal)))
    $parent = Split-Path -LiteralPath $fullPath -Parent
    Assert-StageACondition (-not [string]::IsNullOrWhiteSpace($parent))
    $parent = Test-StageANormalExistingPath $parent $true
    Assert-StageACondition (-not (Test-Path -LiteralPath $fullPath))
    Test-StageADisjointPaths $fullPath $Evaluated
    return $fullPath
}

function ConvertTo-StageACommandLine {
    param([string[]] $ArgumentList)
    return (($ArgumentList | ForEach-Object {
        '"' + $_.Replace('\\', '\\').Replace('"', '\"') + '"'
    }) -join ' ')
}

function Wait-StageAProcess {
    param([System.Diagnostics.Process] $Process, [int] $TimeoutSeconds)
    $deadline = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not $Process.HasExited) {
        Assert-StageACondition (-not $script:StageACancelled)
        Assert-StageACondition ($deadline.Elapsed.TotalSeconds -lt $TimeoutSeconds)
        Start-Sleep -Milliseconds 100
    }
}

function Copy-StageAProcessStream {
    param([System.IO.Stream] $Source, [string] $Path)
    return [HardwareInspection.StageA.CappedDrain]::CopyAsync($Source, $Path, $script:StageAMaximumProcessStreamBytes)
}

function Stop-StageAOwnedProcesses {
    $wasCancelled = $script:StageACancelled
    $script:StageACancelled = $false
    $windowsDirectory = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Windows)
    $taskkillPath = [System.IO.Path]::GetFullPath((Join-Path $windowsDirectory 'System32\taskkill.exe'))
    try {
        foreach ($owned in @($script:StageAOwnedProcesses)) {
            try {
            $current = Get-Process -Id $owned.Id -ErrorAction SilentlyContinue
            if ($null -ne $current -and $current.StartTime.ToUniversalTime().Ticks -eq $owned.StartTicks) {
                $null = Invoke-StageAProcess $taskkillPath @('/PID',[string]$owned.Id,'/T','/F') ($owned.LogPath + '.cleanup') 30 $false
            }
            } catch { throw }
        }
    } finally {
        $script:StageAOwnedProcesses.Clear()
        $script:StageACancelled = $wasCancelled
    }
}

function Invoke-StageAProcess {
    param([string] $Application, [string[]] $ArgumentList, [string] $LogPath, [int] $TimeoutSeconds, [bool] $RegisterOwned = $true, [bool] $ReturnOutput = $false)
    $information = New-Object System.Diagnostics.ProcessStartInfo
    $information.FileName = $Application
    $information.Arguments = ConvertTo-StageACommandLine $ArgumentList
    $information.UseShellExecute = $false
    $information.CreateNoWindow = $true
    $information.RedirectStandardOutput = $true
    $information.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $information
    Assert-StageACondition ($process.Start())
    if ($RegisterOwned) { [void]$script:StageAOwnedProcesses.Add([pscustomobject]@{ Id = $process.Id; StartTicks = $process.StartTime.ToUniversalTime().Ticks; LogPath = $LogPath }) }
    $stdoutPath = $LogPath + '.stdout'
    $stderrPath = $LogPath + '.stderr'
    $outputTask = Copy-StageAProcessStream $process.StandardOutput.BaseStream $stdoutPath
    $errorTask = Copy-StageAProcessStream $process.StandardError.BaseStream $stderrPath
    $deadline = [System.Diagnostics.Stopwatch]::StartNew()
    while ((-not $outputTask.IsCompleted) -or (-not $errorTask.IsCompleted) -or (-not $process.HasExited)) {
            Assert-StageACondition (-not $script:StageACancelled)
            Assert-StageACondition ($deadline.Elapsed.TotalSeconds -lt $TimeoutSeconds)
            Start-Sleep -Milliseconds 20
    }
    $truncated = $outputTask.GetAwaiter().GetResult() -or $errorTask.GetAwaiter().GetResult()
    Assert-StageACondition (-not $truncated)
    Assert-StageACondition ($process.ExitCode -eq 0)
    if (-not $ReturnOutput) { return }
    Assert-StageACondition ((Get-Item -LiteralPath $stdoutPath).Length -le 4096)
    return (Get-Content -LiteralPath $stdoutPath -Raw).Trim()
}

function Read-HardwareInspectionIntelRunnerStageATrx {
    param([Parameter(Mandatory)][string] $Path, [Parameter(Mandatory)][ValidateSet('Deterministic','Task8Deterministic')][string] $Kind)
    $file = Get-Item -LiteralPath $Path -Force
    Assert-StageACondition (-not $file.PSIsContainer -and (($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -eq 0) -and $file.Length -gt 0 -and $file.Length -le $script:StageAMaximumTrxBytes)
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    Assert-StageACondition ($hash.Length -eq 32)
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = $script:StageAMaximumTrxBytes
    $memory = New-Object System.IO.MemoryStream(,$bytes)
    $reader = [System.Xml.XmlReader]::Create($memory, $settings)
    $document = New-Object System.Xml.XmlDocument
    $document.XmlResolver = $null
    try { $document.Load($reader) }
    finally { $reader.Dispose(); $memory.Dispose() }
    Assert-StageACondition ($document.DocumentElement.LocalName -ceq 'TestRun' -and $document.DocumentElement.NamespaceURI -ceq $script:StageATrxNamespace)
    $manager = New-Object System.Xml.XmlNamespaceManager($document.NameTable)
    $manager.AddNamespace('t', $script:StageATrxNamespace)
    $resultNodes = @($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $manager))
    $expectedCount = if ($Kind -ceq 'Deterministic') { 174 } else { 3 }
    Assert-StageACondition ($resultNodes.Count -eq $expectedCount)
    $resultNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $resultIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $executionIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $results = @()
    foreach ($node in $resultNodes) {
        $name = [string]$node.GetAttribute('testName')
        $testId = [string]$node.GetAttribute('testId')
        $executionId = [string]$node.GetAttribute('executionId')
        Assert-StageACondition ($node.GetAttribute('outcome') -ceq 'Passed')
        $null = [guid]::Parse($testId); $null = [guid]::Parse($executionId)
        Assert-StageACondition (-not [string]::IsNullOrWhiteSpace($name) -and $resultNames.Add($name) -and $resultIds.Add($testId) -and $executionIds.Add($executionId))
        $results += [pscustomobject]@{ Name = $name; TestId = $testId; ExecutionId = $executionId }
    }
    if ($Kind -ceq 'Task8Deterministic') {
        $task8Names = @(
            'ArtifactStringShape_RejectsPathsAndFreeTextWithGenericDiagnostics',
            'CaptureInterval_ThirtySecondsPlusOneTickIsOutsideBoundary',
            'StableFileIdentityAndProcessTreeCleanup_AreFailClosed'
        )
        foreach ($task8Name in $task8Names) { Assert-StageACondition ($resultNames.Contains($task8Name)) }
    }
    $definitionNodes = @($document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $manager))
    Assert-StageACondition ($definitionNodes.Count -eq $expectedCount)
    $definitions = @{}
    $expectedAssembly = if ($Kind -ceq 'Deterministic') { 'HardwareInspection.LlmFitSpike.Tests.dll' } else { 'HardwareInspection.LlmFitSpike.IntegrationTests.dll' }
    foreach ($node in $definitionNodes) {
        $testId = [string]$node.GetAttribute('id')
        $execution = $node.SelectSingleNode('t:Execution', $manager)
        $method = $node.SelectSingleNode('t:TestMethod', $manager)
        $name = [string]$node.GetAttribute('name')
        Assert-StageACondition ($null -ne $execution -and $null -ne $method -and -not $definitions.ContainsKey($testId))
        $null = [guid]::Parse($testId); $null = [guid]::Parse([string]$execution.GetAttribute('id'))
        Assert-StageACondition ($name -ceq [string]$method.GetAttribute('name'))
        Assert-StageACondition ([System.IO.Path]::GetFileName([string]$node.GetAttribute('storage')) -ieq $expectedAssembly)
        Assert-StageACondition ([System.IO.Path]::GetFileName([string]$method.GetAttribute('codeBase')) -ieq $expectedAssembly)
        if ($Kind -ceq 'Deterministic') {
            Assert-StageACondition ([string]$method.GetAttribute('className') -clike 'HardwareInspection.LlmFitSpike.Tests.*')
        } else {
            $expectedTask8Class = 'HardwareInspection.LlmFitSpike.IntegrationTests.LlmFit' + [char]67 + 'andidateIntegrationTests'
            Assert-StageACondition ([string]$method.GetAttribute('className') -ceq $expectedTask8Class)
        }
        $definitions[$testId] = [pscustomobject]@{ Name = $name; ExecutionId = [string]$execution.GetAttribute('id') }
    }
    $entryNodes = @($document.SelectNodes('/t:TestRun/t:TestEntries/t:TestEntry', $manager))
    Assert-StageACondition ($entryNodes.Count -eq $expectedCount)
    $entries = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entryNodes) {
        $entryTestId = [string]$entry.GetAttribute('testId')
        $entryExecutionId = [string]$entry.GetAttribute('executionId')
        Assert-StageACondition ($entries.Add($entryTestId + '|' + $entryExecutionId))
    }
    foreach ($result in $results) {
        Assert-StageACondition ($definitions.ContainsKey($result.TestId))
        $definition = $definitions[$result.TestId]
        Assert-StageACondition ($definition.Name -ceq $result.Name -and $definition.ExecutionId -ceq $result.ExecutionId -and $entries.Contains($result.TestId + '|' + $result.ExecutionId))
    }
    $summaryNodes = @($document.SelectNodes('/t:TestRun/t:ResultSummary', $manager))
    Assert-StageACondition ($summaryNodes.Count -eq 1 -and $summaryNodes[0].GetAttribute('outcome') -ceq 'Completed')
    $counterNodes = @($document.SelectNodes('/t:TestRun/t:ResultSummary/t:Counters', $manager))
    Assert-StageACondition ($counterNodes.Count -eq 1)
    $counters = $counterNodes[0]
    foreach ($name in @('total','executed','passed','failed','error','timeout','aborted','inconclusive','passedButRunAborted','notExecuted','notRunnable','disconnected','warning','completed','inProgress','pending')) {
        $attribute = $counters.Attributes[$name]
        Assert-StageACondition ($null -ne $attribute -and $attribute.Value -cmatch '\A[0-9]+\z')
    }
    Assert-StageACondition ([int]$counters.GetAttribute('total') -eq $expectedCount -and [int]$counters.GetAttribute('executed') -eq $expectedCount -and [int]$counters.GetAttribute('passed') -eq $expectedCount)
    foreach ($name in @('failed','error','timeout','aborted','inconclusive','passedButRunAborted','notExecuted','notRunnable','disconnected','warning','completed','inProgress','pending')) { Assert-StageACondition ([int]$counters.GetAttribute($name) -eq 0) }
    return [pscustomobject]@{ Passed = $expectedCount; NonPassing = 0 }
}

function Write-StageAAtomicUtf8 {
    param([string] $Path, [string] $Text)
    $parent = Split-Path -LiteralPath $Path -Parent
    $temporary = Join-Path $parent ('.stagea-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = New-Object System.IO.FileStream($temporary, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $writer = New-Object System.IO.StreamWriter($stream, (New-Object System.Text.UTF8Encoding($false)))
            try { $writer.Write($Text); $writer.Flush(); $stream.Flush($true) }
            finally { $writer.Dispose() }
        } finally { $stream.Dispose() }
        [System.IO.File]::Move($temporary, $Path)
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Assert-StageASummaryPrivacy {
    param([string] $Json, [string] $Markdown, [string] $Sha)
    $expectedJson = '{"schemaVersion":"1.0","evaluatedSha":"' + $Sha + '","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}'
    $expectedMarkdown = "# Hardware Inspection Intel Stage A`n`n- Evaluated SHA: $Sha`n- Deterministic passed: 174`n- Task8 deterministic passed: 3`n- Non-passing: 0`n"
    Assert-StageACondition ($Json -ceq $expectedJson)
    Assert-StageACondition ($Markdown -ceq $expectedMarkdown)
    foreach ($text in @($Json, $Markdown)) {
        Assert-StageACondition ($text -notmatch '(?i)(?:[a-z]:\\|\\\\|/home/|/users/|stdout|stderr|\.trx|<\?xml|<testrun|\b(?:candidate|llmfit|json|cpu|gpu|hostname|computername|username|ip|mac)\b)')
    }
}

function Test-StageAResidualProcesses {
    $residual = @(Get-Process | Where-Object { $_.ProcessName -match '(?i)(llmfit|fake.*tool)' })
    Assert-StageACondition ($residual.Count -eq 0)
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort 8787 -ErrorAction SilentlyContinue)
    Assert-StageACondition ($listeners.Count -eq 0)
}

function Invoke-HardwareInspectionIntelRunnerStageAInternal {
    foreach ($name in [System.Environment]::GetEnvironmentVariables().Keys) {
        $environmentName = [string]$name
        Assert-StageACondition (-not ($environmentName.StartsWith('GIT_', [System.StringComparison]::OrdinalIgnoreCase) -or $environmentName.StartsWith('GRANITE_LLMFIT_', [System.StringComparison]::OrdinalIgnoreCase)))
    }
    $evaluated = Test-StageANormalExistingPath $EvaluatedRoot $true
    $workRoot = Test-StageANormalExistingPath $LocalWorkRoot $true
    Test-StageADisjointPaths $workRoot $evaluated
    $summaryJson = Get-StageAOutputPath $SummaryJsonPath $evaluated
    $summaryMarkdown = Get-StageAOutputPath $SummaryMarkdownPath $evaluated
    Assert-StageACondition ($summaryJson -ine $summaryMarkdown)
    Assert-StageACondition ($ApprovedSha -cmatch '\A[0-9a-f]{40}\z' -and $ApprovedSha -cne ('0' * 40))
    $gitDirectory = Join-Path $evaluated '.git'
    $null = Test-StageANormalExistingPath $gitDirectory $true
    $git = @(Get-Command git.exe -CommandType Application | Select-Object -First 1)
    Assert-StageACondition ($git.Count -eq 1)
    $blockedDirectory = Join-Path $evaluated 'third-party\bin\llmfit\v1.1.9\win-x64'
    Assert-StageACondition (-not (Test-Path -LiteralPath $blockedDirectory))
    $runDirectory = Join-Path $workRoot ('stagea-' + [guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
    $runDirectory = Test-StageANormalExistingPath $runDirectory $true
    $gitTopLog = Join-Path $runDirectory 'git-top'
    $gitDirLog = Join-Path $runDirectory 'git-dir'
    $gitHeadLog = Join-Path $runDirectory 'git-head'
    $gitStatusLog = Join-Path $runDirectory 'git-status'
    $gitDiffLog = Join-Path $runDirectory 'git-diff'
    $gitDiffCachedLog = Join-Path $runDirectory 'git-diff-cached'
    $topLevel = Test-StageANormalExistingPath (Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'rev-parse','--show-toplevel') $gitTopLog 30 $true $true) $true
    $absoluteGitDirectory = Test-StageANormalExistingPath (Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'rev-parse','--absolute-git-dir') $gitDirLog 30 $true $true) $true
    $head = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'rev-parse','HEAD') $gitHeadLog 30 $true $true
    Assert-StageACondition ($topLevel -ieq $evaluated -and $absoluteGitDirectory -ieq $gitDirectory -and $head -ceq $ApprovedSha)
    $null = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'status','--porcelain=v1','--untracked-files=all') $gitStatusLog 30
    Assert-StageACondition ([string]::IsNullOrEmpty((Get-Content -LiteralPath ($gitStatusLog + '.stdout') -Raw)))
    $null = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'diff','--quiet') $gitDiffLog 30
    $null = Invoke-StageAProcess $git[0].Source @('-C',$evaluated,'diff','--cached','--quiet') $gitDiffCachedLog 30
    $dotnet = @(Get-Command dotnet.exe -CommandType Application | Select-Object -First 1)
    Assert-StageACondition ($dotnet.Count -eq 1)
    $projects = @(
        'tools\HardwareInspection.LlmFitSpike.Tests\HardwareInspection.LlmFitSpike.Tests.csproj',
        'tools\HardwareInspection.LlmFitSpike.IntegrationTests\HardwareInspection.LlmFitSpike.IntegrationTests.csproj'
    )
    foreach ($project in $projects) {
        $projectPath = Join-Path $evaluated $project
        Assert-StageACondition (Test-Path -LiteralPath $projectPath -PathType Leaf)
        $name = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
        $null = Invoke-StageAProcess $dotnet[0].Source @('restore',$projectPath,'--runtime','win-x64','-p:Configuration=Release') (Join-Path $runDirectory ($name + '.restore.log')) 300
        $null = Invoke-StageAProcess $dotnet[0].Source @('build',$projectPath,'--configuration','Release','--runtime','win-x64','--no-restore') (Join-Path $runDirectory ($name + '.build.log')) 300
    }
    $resultsDirectory = Join-Path $runDirectory 'results'
    [System.IO.Directory]::CreateDirectory($resultsDirectory) | Out-Null
    $deterministicProject = Join-Path $evaluated $projects[0]
    $task8Project = Join-Path $evaluated $projects[1]
    $null = Invoke-StageAProcess $dotnet[0].Source @('test',$deterministicProject,'--configuration','Release','--runtime','win-x64','--no-restore','--no-build','--filter','TestCategory=Deterministic','--minimum-expected-tests','174','--results-directory',$resultsDirectory,'--report-trx','--report-trx-filename','deterministic.trx','--no-ansi') (Join-Path $runDirectory 'deterministic.log') 300
    $null = Invoke-StageAProcess $dotnet[0].Source @('test',$task8Project,'--configuration','Release','--runtime','win-x64','--no-restore','--no-build','--filter','TestCategory=Task8Deterministic','--minimum-expected-tests','3','--results-directory',$resultsDirectory,'--report-trx','--report-trx-filename','task8.trx','--no-ansi') (Join-Path $runDirectory 'task8.log') 300
    $deterministic = Read-HardwareInspectionIntelRunnerStageATrx (Join-Path $resultsDirectory 'deterministic.trx') 'Deterministic'
    $task8 = Read-HardwareInspectionIntelRunnerStageATrx (Join-Path $resultsDirectory 'task8.trx') 'Task8Deterministic'
    Test-StageAResidualProcesses
    $json = '{"schemaVersion":"1.0","evaluatedSha":"' + $ApprovedSha + '","deterministicPassed":174,"task8DeterministicPassed":3,"nonPassing":0}'
    $markdown = "# Hardware Inspection Intel Stage A`n`n- Evaluated SHA: $ApprovedSha`n- Deterministic passed: 174`n- Task8 deterministic passed: 3`n- Non-passing: 0`n"
    Assert-StageASummaryPrivacy $json $markdown $ApprovedSha
    Write-StageAAtomicUtf8 $summaryJson $json
    Write-StageAAtomicUtf8 $summaryMarkdown $markdown
}

if ($MyInvocation.InvocationName -ne '.') {
    $primaryFailure = $null
    $cleanupFailure = $null
    $cancelHandler = [System.ConsoleCancelEventHandler]{ param($sender, $eventArgs); $eventArgs.Cancel = $true; $script:StageACancelled = $true }
    try {
        [System.Console]::add_CancelKeyPress($cancelHandler)
        Invoke-HardwareInspectionIntelRunnerStageAInternal
    }
    catch { $primaryFailure = $_ }
    finally { [System.Console]::remove_CancelKeyPress($cancelHandler) }
    try { Stop-StageAOwnedProcesses; Test-StageAResidualProcesses }
    catch { $cleanupFailure = $_ }
    if ($null -ne $primaryFailure -or $null -ne $cleanupFailure) {
        [Console]::Error.WriteLine($script:StageAFailure)
        exit 1
    }
}
