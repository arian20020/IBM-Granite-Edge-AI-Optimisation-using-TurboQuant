$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$targetsPath = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\HardwareInspection.LlamaCppProbePackaging.targets'
$appProjectPath = Join-Path $repositoryRoot 'IBM Granite with TurboQuant (Intel)\IBM Granite with TurboQuant (Intel).csproj'
$testProjectPath = Join-Path $repositoryRoot 'tests\UnitTests\GraniteEdgeAI.UnitTests\GraniteEdgeAI.UnitTests.csproj'

Describe 'Hardware Inspection package contracts' {
    It 'does not publish an unresolved hardware item expression' {
        $text = [IO.File]::ReadAllText($targetsPath)
        $text | Should Not Match '<Content\s+Include="@\(_HardwareInspection'
        $text | Should Not Match 'PackageRoot\)\\\*'
        $text | Should Match 'TaskParameter="ConsoleOutput" ItemName="_HardwareInspectionLlamaCppProbeVerifiedFiles"'
        $text | Should Match '<CreateItem Include="@\(_HardwareInspectionLlamaCppProbeVerifiedFiles\);'
    }

    It 'keeps H1 fixture sources out of loose package content' {
        $app = [IO.File]::ReadAllText($appProjectPath)
        $tests = [IO.File]::ReadAllText($testProjectPath)
        $app | Should Not Match '<(?:Content|None|EmbeddedResource|PRIResource)\s+Include="Features\\(?:HardwareInspection|ModelHardwareCompatibility)\\DebugFixtures'
        $tests | Should Not Match '<(?:Content|None|EmbeddedResource|PRIResource)\s+Include="Features\\HardwareInspection\\DebugFixtures'
    }

    It 'does not introduce audit reference evidence or private package roots' {
        foreach ($path in @($targetsPath, $appProjectPath, $testProjectPath)) {
            $text = [IO.File]::ReadAllText($path)
            $text | Should Not Match '(?i)(?:docs\\audits|reference\\|evidence\\|C:\\Users\\|raw-result)'
        }
    }
}
