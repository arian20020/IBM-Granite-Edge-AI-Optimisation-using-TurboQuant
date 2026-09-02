Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the repository root from this test file so the test imports the exact
# committed shared build module rather than a synthetic copy.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$modulePath = Join-Path $repositoryRoot 'scripts\testing\workbook05\Workbook05.Build.psm1'
Import-Module $modulePath -Force -ErrorAction Stop

function Assert-Equal {
    param(
        # The actual value comes from the real shared cache parser.
        [Parameter(Mandatory = $true)]
        [object]$Actual,

        # The expected value documents the reviewed cache semantics.
        [Parameter(Mandatory = $true)]
        [object]$Expected,

        # Each failure message explains the intended behavior in beginner terms.
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    # Throw a readable failure so the existing Workbook 05 gate can stop
    # immediately without adding another PowerShell test-framework dependency.
    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', found '$Actual'."
    }
}

# Reproduce the production input shape: CMakeCache.txt becomes a string array
# that legitimately contains blank lines between NAME:TYPE=value entries.
$cacheLines = @(
    'CMAKE_GENERATOR:INTERNAL=Visual Studio 17 2022',
    '',
    'ENABLE_INTEL_GPU:BOOL=OFF',
    '',
    'Python3_EXECUTABLE:FILEPATH=C:\Program Files\Python312\python.exe',
    'WB05_EQUALS_VALUE:STRING=left=right'
)

# Prove that blank lines do not prevent an ordinary cache value from being read.
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'CMAKE_GENERATOR') `
    -Expected 'Visual Studio 17 2022' `
    -Message 'The parser must ignore blank lines and preserve the generator value.'

# Prove that the reviewed Boolean cache value is returned exactly as CMake wrote it.
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'ENABLE_INTEL_GPU') `
    -Expected 'OFF' `
    -Message 'The parser must return the requested Boolean cache value.'

# Prove that normal Windows path characters survive parsing unchanged.
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'Python3_EXECUTABLE') `
    -Expected 'C:\Program Files\Python312\python.exe' `
    -Message 'The parser must preserve Windows path punctuation.'

# Prove that only the first equals sign separates the cache key/type from its value.
Assert-Equal `
    -Actual (Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'WB05_EQUALS_VALUE') `
    -Expected 'left=right' `
    -Message 'The parser must split only on the first equals sign.'

# Prove that a missing cache key is represented as null instead of inventing a
# default value; the caller remains responsible for its fail-closed policy.
$missing = Get-Wb05CMakeCacheValue -Lines $cacheLines -Name 'DOES_NOT_EXIST'
if ($null -ne $missing) {
    throw 'The parser must return null for a missing cache key.'
}

# Leave a controlled success code because the repository gate checks native-style
# exit state after each discovered PowerShell test script completes.
$global:LASTEXITCODE = 0
Write-Host 'Workbook 05 CMake cache parser PowerShell tests passed.'
