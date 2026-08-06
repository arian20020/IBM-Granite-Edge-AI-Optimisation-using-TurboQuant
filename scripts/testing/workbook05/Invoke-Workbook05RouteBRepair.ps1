[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

<#
.SYNOPSIS
Runs the bounded Workbook 05 Route B repair, build and repository-test campaign.

.DESCRIPTION
The script clones the exact experimental OpenVINO commit into runner temporary
storage, applies only the approved CMake source-list repair, configures by the
pinned Windows build document, inspects generated target membership, builds one
narrow functional-test target and executes six non-empty test filters.

A scientific blocker is preserved as a valid Blocked decision and does not make
the workflow fail. Provenance, contract or evidence-integrity failures throw.
No model, source tree, library or executable is copied into the evidence bundle.
#>

# Stop on PowerShell errors and undeclared variables. Every native command exit
# code is checked explicitly because Windows PowerShell does not do this itself.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Freeze the only external source and build boundary approved at Checkpoint B0.
$SourceRepository = 'https://github.com/EgorDuplensky/openvino.git'
$SourceCommit = '1827f6458d049de11c1a8203c793af67c99935dc'
$ExpectedBuildDocument = 'docs/dev/build_windows.md'
$ExpectedRepairPath = 'src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake'
$ExpectedBenchmarkPath = 'src/plugins/intel_cpu/tests/functional/custom/subgraph_tests/benchmark/x64/concat_sdp_kv_bench.cpp'
$ExpectedTarget = 'ov_cpu_func_subgraph_concat_sdp_turboq'
$ExpectedGenerator = 'Visual Studio 17 2022'
$CMakePath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'

function Write-Utf8NoBomText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Text
    )

    # Use the .NET encoding API because Windows PowerShell 5.1 does not support
    # the utf8NoBOM value accepted by newer PowerShell versions.
    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 24
    Write-Utf8NoBomText -Path $Path -Text ($json + "`n")
}

function Get-SafeRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    # Path.GetRelativePath is unavailable in the Windows PowerShell host used by
    # the controlled runner. Perform separator-aware containment first instead.
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fullPath = [IO.Path]::GetFullPath($Path)
    $rootPrefix = $fullRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Evidence path is outside its approved root: $fullPath"
    }
    return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
}

function Invoke-LoggedNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory
    )

    # Keep stdout, stderr and an exact machine-readable invocation record for
    # every native boundary. The command is never constructed through eval.
    if (-not (Test-Path -LiteralPath $EvidenceDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
    }
    $safeName = $Name -replace '[^A-Za-z0-9._-]', '-'
    $stdoutPath = Join-Path $EvidenceDirectory "$safeName.stdout.log"
    $stderrPath = Join-Path $EvidenceDirectory "$safeName.stderr.log"
    $recordPath = Join-Path $EvidenceDirectory "$safeName.command.json"
    $startedUtc = [DateTime]::UtcNow
    $exitCode = -1

    Push-Location $WorkingDirectory
    try {
        & $FilePath @ArgumentList 1> $stdoutPath 2> $stderrPath
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $endedUtc = [DateTime]::UtcNow
    $record = [ordered]@{
        name = $Name
        executable = $FilePath
        arguments = @($ArgumentList)
        working_directory = $WorkingDirectory
        started_utc = $startedUtc.ToString('o')
        ended_utc = $endedUtc.ToString('o')
        elapsed_seconds = [Math]::Round(($endedUtc - $startedUtc).TotalSeconds, 3)
        exit_code = $exitCode
        stdout_path = [IO.Path]::GetFileName($stdoutPath)
        stderr_path = [IO.Path]::GetFileName($stderrPath)
    }
    Write-JsonFile -Path $recordPath -Value $record

    # Echo the preserved logs into Actions for immediate diagnosis while keeping
    # the original files unchanged for independent validation.
    if (Test-Path -LiteralPath $stdoutPath -PathType Leaf) {
        Get-Content -LiteralPath $stdoutPath | Write-Host
    }
    if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
        Get-Content -LiteralPath $stderrPath | Write-Warning
    }

    return [pscustomobject]$record
}

function Write-EvidenceManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory
    )

    # Hash every evidence file except the manifest itself. Relative paths are
    # containment-checked, slash-normalised and sorted deterministically.
    $manifestPath = Join-Path $EvidenceDirectory 'manifest.sha256'
    $entries = Get-ChildItem -LiteralPath $EvidenceDirectory -File -Recurse |
        Where-Object { $_.FullName -ne $manifestPath } |
        ForEach-Object {
            $relative = Get-SafeRelativePath -Root $EvidenceDirectory -Path $_.FullName
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relative"
        } |
        Sort-Object
    Write-Utf8NoBomText -Path $manifestPath -Text (($entries -join "`n") + "`n")
}

function Write-DecisionAndManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Blocked', 'ExecutableCandidate')]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string]$Checkpoint,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

    # Repair evidence can establish only an executable candidate. Model,
    # performance and quality authorisations remain false in every outcome.
    $decision = [ordered]@{
        schema_version = '1.0'
        campaign_id = 'GTQ-WB05-MF-v1'
        route_id = 'route-b-experimental-qjl-polar'
        source_commit = $SourceCommit
        status = $Status
        checkpoint = $Checkpoint
        reasons = @($Reasons)
        granite_model_test_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
    Write-JsonFile -Path (Join-Path $EvidenceDirectory 'decision.json') -Value $decision
    Write-EvidenceManifest -EvidenceDirectory $EvidenceDirectory
}

# Resolve repository-controlled inputs before touching external source.
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    throw "Pinned Python was not found at: $PythonPath"
}
if (-not (Test-Path -LiteralPath $CMakePath -PathType Leaf)) {
    throw "Pinned Visual Studio CMake was not found at: $CMakePath"
}
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    throw 'RUNNER_TEMP is required for the isolated external workspace.'
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Evidence directory already exists and will not be reused: $OutputDirectory"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

# Use a run-attempt-specific workspace and refuse automatic reuse or cleanup of
# unexpected data. GitHub runner cleanup remains outside this script.
$runIdentity = if ($env:GITHUB_RUN_ID) {
    "$($env:GITHUB_RUN_ID)-$($env:GITHUB_RUN_ATTEMPT)"
}
else {
    [Guid]::NewGuid().ToString('N')
}
$WorkRoot = Join-Path $env:RUNNER_TEMP "workbook-05-route-b-$runIdentity"
if (Test-Path -LiteralPath $WorkRoot) {
    throw "External work directory already exists and will not be reused: $WorkRoot"
}
New-Item -ItemType Directory -Path $WorkRoot -Force:$false | Out-Null
$SourceRoot = Join-Path $WorkRoot 'openvino'
$BuildRoot = Join-Path $WorkRoot 'build'

try {
    # Record the exact machine state used for the experiment.
    $pythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne 'Python 3.12.10') {
        throw "Expected Python 3.12.10, found: $pythonVersion"
    }
    $gitCommand = Get-Command -Name 'git.exe' -CommandType Application -ErrorAction Stop |
        Select-Object -First 1
    $gitPath = $gitCommand.Source
    $gitVersion = ((& $gitPath --version 2>&1) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Git version check failed with code $LASTEXITCODE."
    }
    $cmakeVersion = ((& $CMakePath --version 2>&1) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "CMake version check failed with code $LASTEXITCODE."
    }
    $os = Get-CimInstance Win32_OperatingSystem
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
    Write-JsonFile -Path (Join-Path $OutputDirectory 'environment.json') -Value ([ordered]@{
        python = $pythonVersion
        python_path = $PythonPath
        git = $gitVersion
        git_path = $gitPath
        cmake = $cmakeVersion
        cmake_path = $CMakePath
        requested_generator = $ExpectedGenerator
        operating_system = $os.Caption
        operating_system_version = $os.Version
        cpu = $cpu.Name
        logical_processors = $cpu.NumberOfLogicalProcessors
        total_visible_memory_kib = [int64]$os.TotalVisibleMemorySize
        free_physical_memory_kib_before = [int64]$os.FreePhysicalMemory
        runner_name = $env:RUNNER_NAME
        runner_os = $env:RUNNER_OS
        run_id = $env:GITHUB_RUN_ID
        run_attempt = $env:GITHUB_RUN_ATTEMPT
    })

    # Follow the exact pinned Windows build sequence: clone, initialise recursive
    # submodules, generate with Visual Studio 17 2022, then use cmake --build.
    $sourceCommandDirectory = Join-Path $OutputDirectory 'source-commands'
    New-Item -ItemType Directory -Path $sourceCommandDirectory | Out-Null
    $init = Invoke-LoggedNativeCommand -Name 'git-init' -FilePath $gitPath -ArgumentList @('init', $SourceRoot) -WorkingDirectory $WorkRoot -EvidenceDirectory $sourceCommandDirectory
    if ($init.exit_code -ne 0) { throw 'git init failed.' }
    $remote = Invoke-LoggedNativeCommand -Name 'git-remote-add' -FilePath $gitPath -ArgumentList @('-C', $SourceRoot, 'remote', 'add', 'origin', $SourceRepository) -WorkingDirectory $WorkRoot -EvidenceDirectory $sourceCommandDirectory
    if ($remote.exit_code -ne 0) { throw 'git remote add failed.' }
    $fetch = Invoke-LoggedNativeCommand -Name 'git-fetch' -FilePath $gitPath -ArgumentList @('-C', $SourceRoot, 'fetch', '--depth=1', 'origin', $SourceCommit) -WorkingDirectory $WorkRoot -EvidenceDirectory $sourceCommandDirectory
    if ($fetch.exit_code -ne 0) { throw 'git fetch failed.' }
    $checkout = Invoke-LoggedNativeCommand -Name 'git-checkout' -FilePath $gitPath -ArgumentList @('-C', $SourceRoot, 'checkout', '--detach', $SourceCommit) -WorkingDirectory $WorkRoot -EvidenceDirectory $sourceCommandDirectory
    if ($checkout.exit_code -ne 0) { throw 'git checkout failed.' }
    $submodules = Invoke-LoggedNativeCommand -Name 'git-submodules' -FilePath $gitPath -ArgumentList @('-C', $SourceRoot, 'submodule', 'update', '--init', '--recursive') -WorkingDirectory $WorkRoot -EvidenceDirectory $sourceCommandDirectory
    if ($submodules.exit_code -ne 0) { throw 'Recursive submodule initialisation failed.' }

    # Recalculate provenance from the cloned tree rather than trusting requested
    # values. Every required pinned source file must be present as a regular file.
    $actualRemote = ((& $gitPath -C $SourceRoot remote get-url origin) | Out-String).Trim()
    $actualHead = ((& $gitPath -C $SourceRoot rev-parse HEAD) | Out-String).Trim()
    $statusBefore = @(& $gitPath -C $SourceRoot status --porcelain=v1)
    $submoduleStatus = @(& $gitPath -C $SourceRoot submodule status --recursive)
    if ($actualRemote -ne $SourceRepository) {
        throw "Unexpected Route B origin: $actualRemote"
    }
    if ($actualHead -ne $SourceCommit) {
        throw "Unexpected Route B commit: $actualHead"
    }
    if ($statusBefore.Count -ne 0) {
        throw 'The freshly cloned Route B source is not clean.'
    }
    $invalidSubmodules = @($submoduleStatus | Where-Object { $_ -match '^[\-+U]' })
    if ($invalidSubmodules.Count -ne 0) {
        throw "Recursive submodule provenance is incomplete: $($invalidSubmodules -join '; ')"
    }

    $buildDocumentPath = Join-Path $SourceRoot $ExpectedBuildDocument
    $repairSourcePath = Join-Path $SourceRoot $ExpectedRepairPath
    $benchmarkSourcePath = Join-Path $SourceRoot $ExpectedBenchmarkPath
    foreach ($requiredPath in @($buildDocumentPath, $repairSourcePath, $benchmarkSourcePath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required pinned source file is missing: $requiredPath"
        }
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'source-provenance.json') -Value ([ordered]@{
        repository = $actualRemote
        commit = $actualHead
        clean_before_repair = $true
        recursive_submodule_count = $submoduleStatus.Count
        recursive_submodules_complete = $true
        build_document = $ExpectedBuildDocument
        build_document_sha256 = (Get-FileHash -LiteralPath $buildDocumentPath -Algorithm SHA256).Hash.ToLowerInvariant()
        repair_source_sha256 = (Get-FileHash -LiteralPath $repairSourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
        benchmark_source_sha256 = (Get-FileHash -LiteralPath $benchmarkSourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    })

    # Apply the one-file CMake repair from the project-controlled Python module.
    $repairEvidence = Join-Path $OutputDirectory 'repair'
    Push-Location $RepositoryRoot
    try {
        & $PythonPath -m scripts.testing.workbook05.route_b_repair `
            --source-root $SourceRoot `
            --evidence-root $repairEvidence `
            --source-commit $SourceCommit
        $repairExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($repairExitCode -ne 0) {
        throw "Route B repair module exited with code $repairExitCode."
    }

    # Reject algorithm drift or any second changed external file.
    $changedFiles = @(& $gitPath -C $SourceRoot diff --name-only)
    if ($changedFiles.Count -ne 1 -or $changedFiles[0].Replace('\', '/') -ne $ExpectedRepairPath) {
        throw "Repair changed an unexpected external file set: $($changedFiles -join ', ')"
    }
    & $gitPath -C $SourceRoot diff --check
    if ($LASTEXITCODE -ne 0) {
        throw "External repair patch failed git diff --check with code $LASTEXITCODE."
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'changed-files.json') -Value ([ordered]@{
        expected = @($ExpectedRepairPath)
        actual = @($changedFiles | ForEach-Object { $_.Replace('\', '/') })
        algorithm_files_changed = $false
    })

    # Configure using the exact generator shown by the pinned Windows document.
    # Additional flags disable the optional GPU plugin and expose per-test targets.
    $configureArgs = @(
        '-S', $SourceRoot,
        '-B', $BuildRoot,
        '-G', $ExpectedGenerator,
        '-A', 'x64',
        '-DENABLE_INTEL_GPU=OFF',
        '-DENABLE_TESTS=ON',
        '-DENABLE_CPU_SPECIFIC_TARGET_PER_TEST=ON'
    )
    $configure = Invoke-LoggedNativeCommand -Name 'cmake-configure' -FilePath $CMakePath -ArgumentList $configureArgs -WorkingDirectory $WorkRoot -EvidenceDirectory $OutputDirectory
    if ($configure.exit_code -ne 0) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR3' -Reasons @("The exact documented CMake configure command failed with code $($configure.exit_code).")
        return
    }

    # Record only selected cache values; the complete generated tree stays in
    # runner temporary storage and is never uploaded.
    $cachePath = Join-Path $BuildRoot 'CMakeCache.txt'
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        throw 'CMake configure returned success without CMakeCache.txt.'
    }
    $cacheLines = Get-Content -LiteralPath $cachePath
    $cacheNames = @(
        'CMAKE_HOME_DIRECTORY',
        'CMAKE_GENERATOR',
        'CMAKE_GENERATOR_PLATFORM',
        'ENABLE_INTEL_GPU',
        'ENABLE_TESTS',
        'ENABLE_CPU_SPECIFIC_TARGET_PER_TEST'
    )
    $cacheValues = [ordered]@{}
    foreach ($name in $cacheNames) {
        $line = $cacheLines | Where-Object { $_ -match "^$([Regex]::Escape($name)):[^=]+=" } | Select-Object -First 1
        $cacheValues[$name] = if ($line) { ($line -split '=', 2)[1] } else { $null }
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'cmake-cache-summary.json') -Value ([ordered]@{
        sha256 = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash.ToLowerInvariant()
        values = $cacheValues
    })
    if ($cacheValues.CMAKE_GENERATOR -ne $ExpectedGenerator -or
        $cacheValues.CMAKE_GENERATOR_PLATFORM -ne 'x64' -or
        $cacheValues.ENABLE_INTEL_GPU -ne 'OFF' -or
        $cacheValues.ENABLE_TESTS -ne 'ON' -or
        $cacheValues.ENABLE_CPU_SPECIFIC_TARGET_PER_TEST -ne 'ON') {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR3' -Reasons @('Generated CMake cache does not match the reviewed generator, platform and feature controls.')
        return
    }

    # Prove the generated narrow target contains both the class and x64 instance
    # source. Source presence in the clone is not enough.
    $projectFile = Get-ChildItem -LiteralPath $BuildRoot -Filter "$ExpectedTarget.vcxproj" -File -Recurse |
        Select-Object -First 1
    if (-not $projectFile) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR3' -Reasons @("Generated target project was not found: $ExpectedTarget")
        return
    }
    $projectText = Get-Content -LiteralPath $projectFile.FullName -Raw
    $membership = @([Regex]::Matches($projectText, 'Include="([^"]*concat_sdp_turboq\.cpp)"') |
        ForEach-Object { $_.Groups[1].Value.Replace('\', '/') } |
        Sort-Object -Unique)
    $hasClass = @($membership | Where-Object { $_ -match '/classes/concat_sdp_turboq\.cpp$' }).Count -gt 0
    $hasX64 = @($membership | Where-Object { $_ -match '/x64/concat_sdp_turboq\.cpp$' }).Count -gt 0
    Write-JsonFile -Path (Join-Path $OutputDirectory 'target-membership.json') -Value ([ordered]@{
        target = $ExpectedTarget
        project_sha256 = (Get-FileHash -LiteralPath $projectFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        matching_sources = @($membership)
        class_source_present = $hasClass
        x64_instance_present = $hasX64
    })
    if (-not $hasClass -or -not $hasX64) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR3' -Reasons @('Generated target metadata does not contain both the class and x64 instance source.')
        return
    }

    # Build only the repaired functional-test target with conservative parallelism
    # so the controlled laptop is not exhausted by a full OpenVINO build.
    $build = Invoke-LoggedNativeCommand -Name 'cmake-build-route-b-target' -FilePath $CMakePath -ArgumentList @('--build', $BuildRoot, '--config', 'Release', '--target', $ExpectedTarget, '--parallel', '2') -WorkingDirectory $WorkRoot -EvidenceDirectory $OutputDirectory
    if ($build.exit_code -ne 0) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR6' -Reasons @("Narrow Route B test target failed to build with code $($build.exit_code).")
        return
    }

    # Locate the built test binary without copying it into evidence.
    $testExecutable = Get-ChildItem -LiteralPath $SourceRoot -Filter "$ExpectedTarget.exe" -File -Recurse |
        Select-Object -First 1
    if (-not $testExecutable) {
        $testExecutable = Get-ChildItem -LiteralPath $BuildRoot -Filter "$ExpectedTarget.exe" -File -Recurse |
            Select-Object -First 1
    }
    if (-not $testExecutable) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR6' -Reasons @('The narrow test target built but its executable was not found.')
        return
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'test-binary-metadata.json') -Value ([ordered]@{
        name = $testExecutable.Name
        size_bytes = $testExecutable.Length
        sha256 = (Get-FileHash -LiteralPath $testExecutable.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        binary_copied_to_artifact = $false
    })

    # Follow the pinned Windows document by adding the built test directory and
    # discovered TBB binary directories to PATH before running the test binary.
    $tbbDlls = @(Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'temp') -Filter 'tbb*.dll' -File -Recurse -ErrorAction SilentlyContinue)
    $runtimeDirectories = @($testExecutable.DirectoryName)
    $runtimeDirectories += @($tbbDlls | ForEach-Object { $_.DirectoryName })
    $runtimeDirectories = @($runtimeDirectories | Where-Object { $_ } | Sort-Object -Unique)
    $originalPath = $env:PATH
    $env:PATH = (($runtimeDirectories -join ';') + ';' + $originalPath)
    Write-JsonFile -Path (Join-Path $OutputDirectory 'runtime-path-summary.json') -Value ([ordered]@{
        test_directory = $testExecutable.DirectoryName
        tbb_directory_count = @($tbbDlls | ForEach-Object { $_.DirectoryName } | Sort-Object -Unique).Count
        runtime_directory_count = $runtimeDirectories.Count
        path_restored_after_campaign = $true
    })

    try {
        # Discover tests before selecting any filter. Zero-test green outcomes are
        # explicitly rejected.
        $env:OV_TURBOQ_ROTATION = 'wht'
        $discovery = Invoke-LoggedNativeCommand -Name 'gtest-discovery' -FilePath $testExecutable.FullName -ArgumentList @('--gtest_list_tests', '--gtest_color=no') -WorkingDirectory $testExecutable.DirectoryName -EvidenceDirectory $OutputDirectory
        if ($discovery.exit_code -ne 0) {
            Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR7' -Reasons @("GTest discovery failed with code $($discovery.exit_code).")
            return
        }
        $discoveryText = Get-Content -LiteralPath (Join-Path $OutputDirectory 'gtest-discovery.stdout.log') -Raw
        $discoveredCount = @($discoveryText -split "`r?`n" | Where-Object { $_ -match '^\s{2,}\S' }).Count
        $discoverySummary = [ordered]@{
            discovered_test_count = $discoveredCount
            has_f32_named_cases = $discoveryText -match 'Prc=f32'
            has_baseline_none_none_cases = $discoveryText -match 'Prc=f32.*K=none_V=none'
            has_qjl3_cases = $discoveryText -match 'tbq3_qjl'
            has_qjl4_cases = $discoveryText -match 'tbq4_qjl'
            has_polar3_cases = $discoveryText -match 'polar3'
            has_polar4_cases = $discoveryText -match 'polar4'
            has_asymmetric_none_tbq_cases = $discoveryText -match 'K=none_V=tbq4'
        }
        Write-JsonFile -Path (Join-Path $OutputDirectory 'test-discovery-summary.json') -Value $discoverySummary
        if ($discoveredCount -le 0) {
            Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR7' -Reasons @('The narrow target reported zero discovered tests.')
            return
        }

        # Execute one f32 baseline plus QJL 4/3-bit, Polar 4/3-bit and one
        # asymmetric K=f32/V=TurboQuant filter. Every filter must run non-zero
        # tests, return zero and report no skipped cases.
        $filters = [ordered]@{
            baseline_f32 = '*Prc=f32*K=none_V=none*'
            qjl4 = '*Prc=f32*K=tbq4_qjl_V=tbq4_qjl*'
            qjl3 = '*Prc=f32*K=tbq3_qjl_V=tbq3_qjl*'
            polar4 = '*Prc=f32*K=polar4_V=polar4*'
            polar3 = '*Prc=f32*K=polar3_V=polar3*'
            asymmetric_f32_tbq4 = '*Prc=f32*K=none_V=tbq4*'
        }
        $testResults = @()
        $anyFailure = $false
        foreach ($entry in $filters.GetEnumerator()) {
            $result = Invoke-LoggedNativeCommand -Name "test-$($entry.Key)" -FilePath $testExecutable.FullName -ArgumentList @("--gtest_filter=$($entry.Value)", '--gtest_color=no') -WorkingDirectory $testExecutable.DirectoryName -EvidenceDirectory $OutputDirectory
            $outputText = Get-Content -LiteralPath (Join-Path $OutputDirectory "test-$($entry.Key).stdout.log") -Raw
            $runMatch = [Regex]::Match($outputText, 'Running\s+(\d+)\s+tests?')
            $runCount = if ($runMatch.Success) { [int]$runMatch.Groups[1].Value } else { 0 }
            $skipMatch = [Regex]::Match($outputText, '(?m)^\[\s*SKIPPED\s*\]\s+(\d+)\s+tests?')
            $skippedCount = if ($skipMatch.Success) { [int]$skipMatch.Groups[1].Value } else { 0 }
            $passed = ($result.exit_code -eq 0 -and $runCount -gt 0 -and $skippedCount -eq 0)
            if (-not $passed) {
                $anyFailure = $true
            }
            $testResults += [ordered]@{
                id = $entry.Key
                filter = $entry.Value
                exit_code = $result.exit_code
                run_count = $runCount
                skipped_count = $skippedCount
                passed = $passed
            }
        }
        if ($testResults.Count -ne 6) {
            $anyFailure = $true
        }
        Write-JsonFile -Path (Join-Path $OutputDirectory 'test-results.json') -Value $testResults

        if ($anyFailure) {
            Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR7' -Reasons @('One or more required Route B filters failed, skipped or executed zero tests.')
            return
        }

        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'ExecutableCandidate' -Checkpoint 'BR8' -Reasons @('The bounded CMake repair, generated membership, narrow build and six selected repository filters passed. This does not prove model activation, packed allocation, no fallback, performance or quality.')
    }
    finally {
        # Restore process environment even when discovery or a selected test fails.
        $env:PATH = $originalPath
        Remove-Item Env:OV_TURBOQ_ROTATION -ErrorAction SilentlyContinue
    }
}
catch {
    # Integrity, provenance and orchestration failures are fatal. Preserve a
    # minimal machine-readable record and hash all evidence produced so far.
    $failure = [ordered]@{
        schema_version = '1.0'
        status = 'IntegrityFailure'
        source_commit = $SourceCommit
        message = $_.Exception.Message
        granite_model_test_authorised = $false
        performance_claim_authorised = $false
        quality_claim_authorised = $false
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'integrity-failure.json') -Value $failure
    Write-EvidenceManifest -EvidenceDirectory $OutputDirectory
    throw
}
