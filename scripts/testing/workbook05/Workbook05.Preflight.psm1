<#
.SYNOPSIS
Collects and evaluates the read-only Workbook 05 Intel preflight.

.DESCRIPTION
Machine observation is kept separate from pure gate evaluation so the safety
rules can be tested without changing the computer. No function alters power,
PATH, page-file, service, tool-installation, or repository settings.
#>

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Workbook05AcSleepTimeoutSeconds {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PowerCfgOutput)

    # powercfg reports the AC timeout as a hexadecimal number of seconds.
    $match = [regex]::Match(
        $PowerCfgOutput,
        'Current AC Power Setting Index:\s*0x([0-9A-Fa-f]+)'
    )
    if (-not $match.Success) {
        throw 'Could not find the AC sleep-timeout value in powercfg output.'
    }
    return [Convert]::ToInt32($match.Groups[1].Value, 16)
}

function Invoke-Workbook05NativeText {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$Arguments = @()
    )

    # Capture both streams while preserving a native process failure.
    $global:LASTEXITCODE = 0
    $output = & $Executable @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "'$Executable' exited with code ${exitCode}: $(($output | Out-String).Trim())"
    }
    return (($output | Out-String).Trim())
}

function Test-Workbook05PreflightObservation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Observation,
        [Parameter(Mandatory)]$Settings
    )

    # Add every result to one list so JSON, Markdown, and the exit decision agree.
    $checks = New-Object System.Collections.Generic.List[object]
    function Add-Check {
        param(
            [string]$Name,
            [bool]$Required,
            [bool]$Passed,
            [string]$Expected,
            [AllowEmptyString()][string]$Actual
        )
        $checks.Add([pscustomobject][ordered]@{
            Name = $Name
            Required = $Required
            Passed = $Passed
            Expected = $Expected
            Actual = $Actual
        })
    }

    Add-Check 'Runner name' $true ($Observation.RunnerName -eq $Settings.expected_runner_name) $Settings.expected_runner_name ([string]$Observation.RunnerName)
    Add-Check 'Runner operating system' $true ($Observation.RunnerOs -eq $Settings.expected_runner_os) $Settings.expected_runner_os ([string]$Observation.RunnerOs)
    Add-Check 'Runner architecture' $true ($Observation.RunnerArch -eq $Settings.expected_runner_arch) $Settings.expected_runner_arch ([string]$Observation.RunnerArch)
    Add-Check 'Computer name' $true ($Observation.ComputerName -eq $Settings.expected_computer_name) $Settings.expected_computer_name ([string]$Observation.ComputerName)
    Add-Check 'Processor identity' $true ([string]$Observation.ProcessorName -like "*$($Settings.expected_cpu_substring)*") "Contains $($Settings.expected_cpu_substring)" ([string]$Observation.ProcessorName)
    Add-Check 'Restricted service account' $true ([string]$Observation.ServiceAccount -eq [string]$Settings.expected_service_account) ([string]$Settings.expected_service_account) ([string]$Observation.ServiceAccount)
    Add-Check '64-bit Windows' $true ([bool]$Observation.Is64BitOperatingSystem) 'True' ([string]$Observation.Is64BitOperatingSystem)
    Add-Check 'Installed physical memory' $true ([double]$Observation.PhysicalMemoryGiB -ge [double]$Settings.minimum_physical_memory_gib) "At least $($Settings.minimum_physical_memory_gib) GiB" "$($Observation.PhysicalMemoryGiB) GiB"
    Add-Check 'Available physical memory' $true ([double]$Observation.AvailableMemoryGiB -ge [double]$Settings.minimum_available_memory_gib) "At least $($Settings.minimum_available_memory_gib) GiB" "$($Observation.AvailableMemoryGiB) GiB"
    Add-Check 'System-drive free space' $true ([double]$Observation.SystemDriveFreeGiB -ge [double]$Settings.minimum_system_drive_free_gib) "At least $($Settings.minimum_system_drive_free_gib) GiB" "$($Observation.SystemDriveFreeGiB) GiB"
    Add-Check 'AC power' $true ((-not [bool]$Settings.require_ac_power) -or $Observation.PowerLineStatus -eq 'Online') 'Online' ([string]$Observation.PowerLineStatus)
    Add-Check 'AC sleep timeout' $true ([int]$Observation.AcSleepTimeoutSeconds -eq [int]$Settings.required_ac_sleep_timeout_seconds) "$($Settings.required_ac_sleep_timeout_seconds) seconds" "$($Observation.AcSleepTimeoutSeconds) seconds"
    Add-Check 'Python path' $true ([string]$Observation.PythonPath -eq [string]$Settings.python_path) ([string]$Settings.python_path) ([string]$Observation.PythonPath)
    Add-Check 'Python version' $true ([string]$Observation.PythonVersion -eq [string]$Settings.python_version) ([string]$Settings.python_version) ([string]$Observation.PythonVersion)

    $cmakePassed = $false
    try { $cmakePassed = ([version]$Observation.CMakeVersion -ge [version]$Settings.minimum_cmake_version) } catch { $cmakePassed = $false }
    Add-Check 'CMake version' $true $cmakePassed "At least $($Settings.minimum_cmake_version)" ([string]$Observation.CMakeVersion)

    $msbuildPassed = $false
    try { $msbuildPassed = ([version]$Observation.MSBuildVersion).Major -ge [int]$Settings.minimum_msbuild_major } catch { $msbuildPassed = $false }
    Add-Check 'MSBuild version' $true $msbuildPassed "Major at least $($Settings.minimum_msbuild_major)" ([string]$Observation.MSBuildVersion)
    Add-Check 'MSVC compiler' $true (-not [string]::IsNullOrWhiteSpace([string]$Observation.CompilerVersion)) 'Detected x64 compiler' ([string]$Observation.CompilerVersion)
    Add-Check 'Windows SDK' $true (-not [string]::IsNullOrWhiteSpace([string]$Observation.WindowsSdkVersion)) 'Detected SDK' ([string]$Observation.WindowsSdkVersion)
    Add-Check 'Git available' $true (-not [string]::IsNullOrWhiteSpace([string]$Observation.GitVersion)) 'Detected Git' ([string]$Observation.GitVersion)
    Add-Check 'Repository identity' $true ([string]$Observation.Repository -eq [string]$Settings.required_repository) ([string]$Settings.required_repository) ([string]$Observation.Repository)
    Add-Check 'Repository commit' $true ([string]$Observation.RepositoryCommit -match '^[0-9a-f]{40}$') '40-character lowercase commit' ([string]$Observation.RepositoryCommit)
    Add-Check 'Repository clean' $true ([bool]$Observation.RepositoryClean) 'True' ([string]$Observation.RepositoryClean)
    Add-Check 'Evidence write probe' $true ([bool]$Observation.EvidenceWriteProbePassed) 'True' ([string]$Observation.EvidenceWriteProbePassed)

    $failedRequired = @($checks | Where-Object { $_.Required -and -not $_.Passed })
    return [pscustomobject][ordered]@{
        OverallStatus = if ($failedRequired.Count -eq 0) { 'Passed' } else { 'Failed' }
        Checks = $checks.ToArray()
    }
}

function Get-Workbook05PreflightObservation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [string]$OutputDirectory = $env:RUNNER_TEMP
    )

    # Read physical identity and current resource headroom directly from Windows.
    $processor = Get-CimInstance Win32_Processor | Select-Object -First 1
    $computer = Get-CimInstance Win32_ComputerSystem
    $operatingSystem = Get-CimInstance Win32_OperatingSystem
    $systemDrive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($env:SystemRoot).TrimEnd(':'))

    # Power-line and timeout checks are observations only; no setting is changed.
    Add-Type -AssemblyName System.Windows.Forms
    $powerLineStatus = [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus.ToString()
    $powerCfgOutput = Invoke-Workbook05NativeText -Executable 'powercfg.exe' -Arguments @('/QUERY', 'SCHEME_CURRENT', 'SUB_SLEEP', 'STANDBYIDLE')
    $sleepTimeout = Get-Workbook05AcSleepTimeoutSeconds -PowerCfgOutput $powerCfgOutput

    # Use exact machine-wide Python so the service does not depend on a user profile.
    $pythonPath = 'C:\Program Files\Python312\python.exe'
    $pythonVersionText = Invoke-Workbook05NativeText -Executable $pythonPath -Arguments @('--version')
    $pythonVersion = ($pythonVersionText -replace '^Python\s+', '').Trim()
    $gitVersion = Invoke-Workbook05NativeText -Executable 'git.exe' -Arguments @('--version')

    # Discover the Build Tools installation with Microsoft's own inventory utility.
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswherePath -PathType Leaf)) {
        throw "vswhere.exe was not found at: $vswherePath"
    }
    $installationPath = (& $vswherePath -latest -products '*' -requires Microsoft.VisualStudio.Workload.VCTools -property installationPath | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace([string]$installationPath)) {
        throw 'Visual Studio C++ Build Tools installation was not found.'
    }
    $msbuildPath = (& $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
    $cmakePath = (& $vswherePath -latest -products '*' -find 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace([string]$msbuildPath) -or [string]::IsNullOrWhiteSpace([string]$cmakePath)) {
        throw 'MSBuild or Visual Studio CMake could not be located.'
    }
    $msbuildVersion = (Invoke-Workbook05NativeText -Executable $msbuildPath -Arguments @('-version', '-nologo')).Split([Environment]::NewLine)[-1].Trim()
    $cmakeVersionText = Invoke-Workbook05NativeText -Executable $cmakePath -Arguments @('--version')
    $cmakeVersion = ([regex]::Match($cmakeVersionText, 'cmake version\s+([^\s]+)')).Groups[1].Value

    $msvcRoot = Join-Path $installationPath 'VC\Tools\MSVC'
    $latestMsvc = Get-ChildItem -LiteralPath $msvcRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $compilerPath = Join-Path $latestMsvc.FullName 'bin\Hostx64\x64\cl.exe'
    if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) {
        throw "x64 compiler was not found at: $compilerPath"
    }
    $compilerVersion = (Get-Item -LiteralPath $compilerPath).VersionInfo.ProductVersion
    $sdkRoot = 'C:\Program Files (x86)\Windows Kits\10\Include'
    $sdk = Get-ChildItem -LiteralPath $sdkRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1

    # Read repository state without changing the checkout.
    $commit = (Invoke-Workbook05NativeText -Executable 'git.exe' -Arguments @('-C', $RepositoryRoot, 'rev-parse', 'HEAD')).Trim()
    $status = (Invoke-Workbook05NativeText -Executable 'git.exe' -Arguments @('-C', $RepositoryRoot, 'status', '--porcelain')).Trim()

    # Prove that the runner can write evidence only in the caller-owned output area.
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $probe = Join-Path $OutputDirectory '.preflight-write-probe'
    'probe' | Set-Content -LiteralPath $probe -Encoding UTF8
    $probePassed = (Get-Content -Raw -LiteralPath $probe).Trim() -eq 'probe'
    Remove-Item -LiteralPath $probe -Force

    return [pscustomobject][ordered]@{
        RunnerName = [string]$env:RUNNER_NAME
        RunnerOs = [string]$env:RUNNER_OS
        RunnerArch = [string]$env:RUNNER_ARCH
        ComputerName = [string]$env:COMPUTERNAME
        ProcessorName = [string]$processor.Name
        ServiceAccount = ((whoami 2>&1 | Out-String).Trim()).ToLowerInvariant()
        Is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
        PhysicalMemoryGiB = [math]::Round($computer.TotalPhysicalMemory / 1GB, 2)
        AvailableMemoryGiB = [math]::Round(($operatingSystem.FreePhysicalMemory * 1KB) / 1GB, 2)
        SystemDriveFreeGiB = [math]::Round($systemDrive.Free / 1GB, 2)
        PowerLineStatus = $powerLineStatus
        AcSleepTimeoutSeconds = $sleepTimeout
        PythonVersion = $pythonVersion
        PythonPath = $pythonPath
        GitVersion = $gitVersion
        CMakeVersion = $cmakeVersion
        CMakePath = [string]$cmakePath
        MSBuildVersion = $msbuildVersion
        MSBuildPath = [string]$msbuildPath
        CompilerVersion = [string]$compilerVersion
        CompilerPath = [string]$compilerPath
        WindowsSdkVersion = [string]$sdk.Name
        WindowsSdkPath = [string]$sdk.FullName
        Repository = [string]$env:GITHUB_REPOSITORY
        RepositoryCommit = $commit
        RepositoryClean = [string]::IsNullOrWhiteSpace($status)
        EvidenceWriteProbePassed = $probePassed
    }
}

function Export-Workbook05PreflightEvidence {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Observation,
        [Parameter(Mandatory)]$Evaluation,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $generated = [DateTime]::UtcNow.ToString('o')
    $lowerChecks = @(
        $Evaluation.Checks | ForEach-Object {
            [ordered]@{
                name = [string]$_.Name
                required = [bool]$_.Required
                passed = [bool]$_.Passed
                expected = [string]$_.Expected
                actual = [string]$_.Actual
            }
        }
    )
    $report = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        generated_at_utc = $generated
        observation = $Observation
        checks = $lowerChecks
        overall_status = [string]$Evaluation.OverallStatus
    }
    $report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'preflight-report.json') -Encoding UTF8
    $Observation | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'environment-snapshot.json') -Encoding UTF8

    $summary = @(
        '# Workbook 05 two-route preflight'
        ''
        "Overall status: **$($Evaluation.OverallStatus)**"
        ''
        '| Check | Required | Result | Expected | Actual |'
        '| --- | --- | --- | --- | --- |'
    )
    foreach ($check in $Evaluation.Checks) {
        $result = if ($check.Passed) { 'PASS' } else { 'FAIL' }
        $expected = ([string]$check.Expected).Replace('|', '\|').Replace("`r", '').Replace("`n", '<br>')
        $actual = ([string]$check.Actual).Replace('|', '\|').Replace("`r", '').Replace("`n", '<br>')
        $summary += "| $($check.Name) | $($check.Required) | $result | $expected | $actual |"
    }
    $summary | Set-Content -LiteralPath (Join-Path $OutputDirectory 'preflight-summary.md') -Encoding UTF8
}

Export-ModuleMember -Function @(
    'Get-Workbook05AcSleepTimeoutSeconds',
    'Get-Workbook05PreflightObservation',
    'Test-Workbook05PreflightObservation',
    'Export-Workbook05PreflightEvidence'
)
