[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git([string] $Directory, [string[]] $Arguments) {
    $rawOutput = & git -C $Directory @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output = $rawOutput | Out-String
    if ($exitCode -ne 0) { throw "git $($Arguments -join ' ') failed:`n$output" }
    return $output.Trim()
}

function Write-Utf8([string] $Path, [string] $Content) {
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-Guard([string[]] $GuardArguments) {
    $arguments = @(
        'run', '--no-build', '--configuration', 'Release',
        '--project', $script:ProjectPath, '--'
    ) + $GuardArguments
    $rawOutput = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output = $rawOutput | Out-String
    return [pscustomobject]@{ ExitCode = $exitCode; Output = $output.Trim() }
}

function Assert-Exit([object] $Result, [int] $Expected, [string] $Message) {
    if ($Result.ExitCode -ne $Expected) {
        throw "$Message`nExpected exit $Expected, got $($Result.ExitCode).`n$($Result.Output)"
    }
}

$repoRoot = (& git rev-parse --show-toplevel 2>$null | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) { throw 'Run from inside the repository.' }
$script:ProjectPath = Join-Path $repoRoot 'tools/GraniteFrontendGuard/GraniteFrontendGuard.csproj'

& dotnet build $script:ProjectPath --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Unable to build GraniteFrontendGuard for regression tests.' }

$testId = [guid]::NewGuid().ToString('N')
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("granite-frontend-guard-repo-" + $testId)
$artifactRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("granite-frontend-guard-artifacts-" + $testId)
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

try {
    Invoke-Git $tempRoot @('init', '--initial-branch=main') | Out-Null
    Invoke-Git $tempRoot @('config', 'user.email', 'frontend-guard@example.invalid') | Out-Null
    Invoke-Git $tempRoot @('config', 'user.name', 'Granite Frontend Guard Test') | Out-Null

    Write-Utf8 (Join-Path $tempRoot '.frontend-worker/v2/implementation-lock.yml') @'
version: 2
state_file: .frontend-worker/v2/.state/authorization.json
'@
    Write-Utf8 (Join-Path $tempRoot 'shared/config.json') "{`"mode`":`"stable`"}`n"
    Write-Utf8 (Join-Path $tempRoot 'App/MainPage.xaml') @'
<Page
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Button x:Name="StartButton" Click="Start_Click" Command="{Binding RunCommand}" IsEnabled="{Binding CanRun}" />
    </Grid>
</Page>
'@
    Write-Utf8 (Join-Path $tempRoot 'App/MainPage.xaml.cs') @'
namespace Demo;

public sealed partial class MainPage
{
    private readonly Service service = new();

    public void Run()
    {
        service.Execute("alpha");
    }
}

internal sealed class Service
{
    public void Execute(string value) { }
}
'@
    Invoke-Git $tempRoot @('add', '.') | Out-Null
    Invoke-Git $tempRoot @('commit', '-m', 'baseline') | Out-Null

    $before = Join-Path $artifactRoot 'before.json'
    $after = Join-Path $artifactRoot 'after.json'
    $report = Join-Path $artifactRoot 'report.json'

    Assert-Exit (Invoke-Guard @('snapshot', '--repo', $tempRoot, '--output', $before)) 0 'Baseline snapshot failed.'

    Write-Utf8 (Join-Path $tempRoot 'App/MainPage.xaml') @'
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <Button
            x:Name="StartButton"
            Click="Start_Click"
            Command="{Binding RunCommand}"
            IsEnabled="{Binding CanRun}" />
    </Grid>
</Page>
'@
    Assert-Exit (Invoke-Guard @('snapshot', '--repo', $tempRoot, '--output', $after)) 0 'Reformatted XAML snapshot failed.'
    Assert-Exit (Invoke-Guard @('compare', '--before', $before, '--after', $after, '--output', $report)) 0 'Harmless XAML reformat produced a false contract difference.'
    Invoke-Git $tempRoot @('checkout', '--', 'App/MainPage.xaml') | Out-Null

    $codePath = Join-Path $tempRoot 'App/MainPage.xaml.cs'
    $code = Get-Content -LiteralPath $codePath -Raw
    Write-Utf8 $codePath ($code.Replace('Execute("alpha")', 'Execute("beta")'))
    Assert-Exit (Invoke-Guard @('snapshot', '--repo', $tempRoot, '--output', $after)) 0 'Invocation-change snapshot failed.'
    $invocationCompare = Invoke-Guard @('compare', '--before', $before, '--after', $after, '--output', $report)
    Assert-Exit $invocationCompare 1 'Invocation argument change was not rejected.'
    $invocationReport = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if (-not (@($invocationReport.differences.category) -contains 'invocationEdges')) {
        throw 'Invocation argument change did not produce an invocationEdges difference.'
    }
    Invoke-Git $tempRoot @('checkout', '--', 'App/MainPage.xaml.cs') | Out-Null

    Write-Utf8 (Join-Path $tempRoot 'shared/config.json') "{`"mode`":`"changed`"}`n"
    Assert-Exit (Invoke-Guard @('snapshot', '--repo', $tempRoot, '--output', $after)) 0 'Protected JSON snapshot failed.'
    $hashCompare = Invoke-Guard @('compare', '--before', $before, '--after', $after, '--output', $report)
    Assert-Exit $hashCompare 1 'Protected non-code file change was not rejected.'
    $hashReport = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if (-not (@($hashReport.differences.category) -contains 'protectedFileHashes')) {
        throw 'Protected JSON change did not produce a protectedFileHashes difference.'
    }
    Invoke-Git $tempRoot @('checkout', '--', 'shared/config.json') | Out-Null

    Write-Utf8 (Join-Path $tempRoot 'App/Forbidden.xaml') '<Page />'
    $bootstrap = Invoke-Guard @('verify-bootstrap', '--repo', $tempRoot, '--base', 'HEAD')
    Assert-Exit $bootstrap 1 'Forbidden untracked bootstrap path was not rejected.'
    Remove-Item -LiteralPath (Join-Path $tempRoot 'App/Forbidden.xaml') -Force

    Write-Host 'PASS: harmless XAML reformat is stable.'
    Write-Host 'PASS: invocation argument changes are detected.'
    Write-Host 'PASS: protected non-code changes are detected.'
    Write-Host 'PASS: untracked forbidden bootstrap paths are detected.'
    exit 0
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
}
