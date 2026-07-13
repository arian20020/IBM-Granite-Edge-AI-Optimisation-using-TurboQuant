<#
.SYNOPSIS
Captures a Windows machine manifest for controlled testing.

.PARAMETER EnvironmentId
Stable ID such as ENV-20260713-INTEL-LAPTOP-01.
#>

[CmdletBinding()]
param(
    # Require a stable environment ID so later runs can reference one frozen machine record.
    [Parameter(Mandatory)][ValidatePattern('^ENV-[A-Za-z0-9-]+$')][string]$EnvironmentId
)

# Stop immediately so a partial environment capture is not mistaken for a complete one.
$ErrorActionPreference = "Stop"

# Catch misspelled or undefined variables.
Set-StrictMode -Version Latest

# Resolve the repository root so the script works from any repository subdirectory.
$RepositoryRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if (-not $RepositoryRoot) {
    throw "Run this script from inside the Git repository."
}

# Enter the repository root before creating controlled paths.
Set-Location -LiteralPath $RepositoryRoot

# Create one immutable directory for the supplied environment ID.
$OutputDirectory = "experiments/granite_turboquant_intel/manifests/environments/$EnvironmentId"
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Environment ID already exists: $EnvironmentId"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

# Capture stable Windows and machine information through built-in CIM providers.
$ComputerSystem = Get-CimInstance Win32_ComputerSystem
$OperatingSystem = Get-CimInstance Win32_OperatingSystem
$Processors = @(Get-CimInstance Win32_Processor)
$VideoControllers = @(Get-CimInstance Win32_VideoController)
$LogicalDisk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$($env:SystemDrive)'"

# Read an optional command's first version line without failing when the tool is absent.
function Get-ToolVersion {
    param(
        # Name of the tool to execute.
        [Parameter(Mandatory)][string]$Command,

        # Arguments used to request the tool's version.
        [string[]]$Arguments = @("--version")
    )

    # Return an empty string when the command is not installed or not on PATH.
    $Resolved = Get-Command $Command -ErrorAction SilentlyContinue
    if (-not $Resolved) {
        return ""
    }

    # Capture only the first readable version line.
    try {
        return ((& $Command @Arguments 2>&1 | Select-Object -First 1) -join " ").Trim()
    }
    catch {
        return ""
    }
}

# Build a machine-readable manifest from the captured system information.
$Manifest = [ordered]@{
    schema_version = "1.0"
    environment_id = $EnvironmentId
    captured_at_utc = [DateTime]::UtcNow.ToString("o")
    captured_at_local = [DateTime]::Now.ToString("o")
    timezone = [TimeZoneInfo]::Local.Id
    machine = [ordered]@{
        name = $env:COMPUTERNAME
        role = "target-windows-intel-laptop"
        manufacturer = $ComputerSystem.Manufacturer
        model = $ComputerSystem.Model
        windows_caption = $OperatingSystem.Caption
        windows_version = $OperatingSystem.Version
        windows_build = $OperatingSystem.BuildNumber
        architecture = $OperatingSystem.OSArchitecture
    }
    cpu = @($Processors | ForEach-Object {
        [ordered]@{
            model = $_.Name.Trim()
            physical_cores = $_.NumberOfCores
            logical_processors = $_.NumberOfLogicalProcessors
            max_clock_mhz = $_.MaxClockSpeed
        }
    })
    gpu = @($VideoControllers | ForEach-Object {
        [ordered]@{
            model = $_.Name
            driver = $_.DriverVersion
            adapter_ram_bytes = [int64]$_.AdapterRAM
            status = $_.Status
        }
    })
    memory = [ordered]@{
        installed_ram_bytes = [int64]$ComputerSystem.TotalPhysicalMemory
        available_ram_at_capture_bytes = [int64]$OperatingSystem.FreePhysicalMemory * 1024
    }
    storage = [ordered]@{
        volume = $LogicalDisk.DeviceID
        free_bytes = [int64]$LogicalDisk.FreeSpace
        size_bytes = [int64]$LogicalDisk.Size
    }
    operating_conditions = [ordered]@{
        power_mode = ""
        power_source = ""
        thermal_state = ""
        background_load_notes = ""
    }
    toolchain = [ordered]@{
        git = Get-ToolVersion -Command "git"
        cmake = Get-ToolVersion -Command "cmake"
        ninja = Get-ToolVersion -Command "ninja"
        python = Get-ToolVersion -Command "python"
        powershell = $PSVersionTable.PSVersion.ToString()
        cl = Get-ToolVersion -Command "cl" -Arguments @()
    }
    capture_files = @("machine-manifest.json", "Get-ComputerInfo.txt")
    review_required = @(
        "Add Intel GPU shared/dedicated memory from the selected measurement method.",
        "Add NPU presence and driver.",
        "Add power mode, power source and thermal state.",
        "Add oneAPI, Vulkan and OpenVINO versions when applicable."
    )
    notes = ""
}

# Choose the machine-manifest output path.
$ManifestPath = Join-Path $OutputDirectory "machine-manifest.json"

# Save the structured manifest as UTF-8 JSON.
$Manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8

# Save a complete readable Windows snapshot beside the structured manifest.
Get-ComputerInfo | Format-List * | Out-File -LiteralPath (Join-Path $OutputDirectory "Get-ComputerInfo.txt") -Encoding UTF8

# Tell the operator where the controlled environment record was created.
Write-Host "Created environment manifest: $ManifestPath" -ForegroundColor Green

# Make the required manual review explicit before any run uses the environment ID.
Write-Host "Review and complete the fields listed under review_required before using the environment ID." -ForegroundColor Yellow
