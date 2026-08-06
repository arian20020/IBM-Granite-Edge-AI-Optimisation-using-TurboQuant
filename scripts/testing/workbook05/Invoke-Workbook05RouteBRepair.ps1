[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$PythonPath = 'C:\Program Files\Python312\python.exe'
)

# Stop on PowerShell errors and on references to undeclared variables. Native
# command exit codes are checked explicitly after every invocation.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Freeze the only external source revision authorised by Checkpoint B0.
$SourceRepository = 'https://github.com/EgorDuplensky/openvino.git'
$SourceCommit = '1827f6458d049de11c1a8203c793af67c99935dc'
$ExpectedBuildDocument = 'docs/dev/build_windows.md'
$ExpectedRepairPath = 'src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake'
$ExpectedBenchmarkPath = 'src/plugins/intel_cpu/tests/functional/custom/subgraph_tests/benchmark/x64/concat_sdp_kv_bench.cpp'
$ExpectedTarget = 'ov_cpu_func_subgraph_concat_sdp_turboq'
$CMakePath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    $Value |
        ConvertTo-Json -Depth 20 |
        Set-Content -LiteralPath $Path -Encoding utf8NoBOM
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

    # Each native command receives independent stdout and stderr logs plus a
    # machine-readable record of the exact invocation and timing.
    $safeName = $Name -replace '[^A-Za-z0-9._-]', '-'
    $stdoutPath = Join-Path $EvidenceDirectory "$safeName.stdout.log"
    $stderrPath = Join-Path $EvidenceDirectory "$safeName.stderr.log"
    $recordPath = Join-Path $EvidenceDirectory "$safeName.command.json"
    $startedUtc = [DateTime]::UtcNow

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

    # Echo logs into the workflow while preserving the original files.
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

    # Hash every evidence file except the manifest itself. Paths are relative,
    # forward-slash normalised and sorted for deterministic hosted validation.
    $manifestPath = Join-Path $EvidenceDirectory 'manifest.sha256'
    $entries = Get-ChildItem -LiteralPath $EvidenceDirectory -File -Recurse |
        Where-Object { $_.FullName -ne $manifestPath } |
        ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($EvidenceDirectory, $_.FullName).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relative"
        } |
        Sort-Object
    $entries | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
}

function Write-DecisionAndManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string]$Checkpoint,

        [Parameter(Mandatory = $true)]
        [string[]]$Reasons
    )

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

# Resolve and verify all controlled paths before touching external source.
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    throw "Pinned Python was not found at: $PythonPath"
}
if (-not (Test-Path -LiteralPath $CMakePath -PathType Leaf)) {
    throw "Pinned Visual Studio CMake was not found at: $CMakePath"
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "Evidence directory already exists and will not be reused: $OutputDirectory"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force:$false | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

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
    # Record the controlled machine tools before cloning or configuring.
    $pythonVersion = ((& $PythonPath --version 2>&1) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $pythonVersion -ne 'Python 3.12.10') {
        throw "Expected Python 3.12.10, found: $pythonVersion"
    }
    $gitVersion = ((& git --version 2>&1) | Out-String).Trim()
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
        git = $gitVersion
        cmake = $cmakeVersion
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

    # Follow the pinned Windows build document: clone the repository, initialise
    # recursive submodules, generate with Visual Studio 17 2022, then build via
    # cmake --build. The exact commit is fetched directly and checked out detached.
    $cloneLog = Join-Path $OutputDirectory 'source-commands'
    New-Item -ItemType Directory -Path $cloneLog | Out-Null
    $init = Invoke-LoggedNativeCommand -Name 'git-init' -FilePath 'git' -ArgumentList @('init', $SourceRoot) -WorkingDirectory $WorkRoot -EvidenceDirectory $cloneLog
    if ($init.exit_code -ne 0) { throw 'git init failed.' }
    $remote = Invoke-LoggedNativeCommand -Name 'git-remote-add' -FilePath 'git' -ArgumentList @('-C', $SourceRoot, 'remote', 'add', 'origin', $SourceRepository) -WorkingDirectory $WorkRoot -EvidenceDirectory $cloneLog
    if ($remote.exit_code -ne 0) { throw 'git remote add failed.' }
    $fetch = Invoke-LoggedNativeCommand -Name 'git-fetch' -FilePath 'git' -ArgumentList @('-C', $SourceRoot, 'fetch', '--depth=1', 'origin', $SourceCommit) -WorkingDirectory $WorkRoot -EvidenceDirectory $cloneLog
    if ($fetch.exit_code -ne 0) { throw 'git fetch failed.' }
    $checkout = Invoke-LoggedNativeCommand -Name 'git-checkout' -FilePath 'git' -ArgumentList @('-C', $SourceRoot, 'checkout', '--detach', $SourceCommit) -WorkingDirectory $WorkRoot -EvidenceDirectory $cloneLog
    if ($checkout.exit_code -ne 0) { throw 'git checkout failed.' }
    $submodules = Invoke-LoggedNativeCommand -Name 'git-submodules' -FilePath 'git' -ArgumentList @('-C', $SourceRoot, 'submodule', 'update', '--init', '--recursive') -WorkingDirectory $WorkRoot -EvidenceDirectory $cloneLog
    if ($submodules.exit_code -ne 0) { throw 'Recursive submodule initialisation failed.' }

    # Recalculate provenance from the cloned workspace rather than trusting the
    # requested values written above.
    $actualRemote = ((& git -C $SourceRoot remote get-url origin) | Out-String).Trim()
    $actualHead = ((& git -C $SourceRoot rev-parse HEAD) | Out-String).Trim()
    $statusBefore = @(& git -C $SourceRoot status --porcelain=v1)
    $submoduleStatus = @(& git -C $SourceRoot submodule status --recursive)
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
    Write-JsonFile -Path (Join-Path $OutputDirectory 'source-provenance.json') -Value ([ordered]@{
        repository = $actualRemote
        commit = $actualHead
        clean_before_repair = $true
        recursive_submodule_count = $submoduleStatus.Count
        recursive_submodules_complete = $true
        build_document = $ExpectedBuildDocument
        build_document_sha256 = (Get-FileHash -LiteralPath (Join-Path $SourceRoot $ExpectedBuildDocument) -Algorithm SHA256).Hash.ToLowerInvariant()
        repair_source_sha256 = (Get-FileHash -LiteralPath (Join-Path $SourceRoot $ExpectedRepairPath) -Algorithm SHA256).Hash.ToLowerInvariant()
        benchmark_source_sha256 = (Get-FileHash -LiteralPath (Join-Path $SourceRoot $ExpectedBenchmarkPath) -Algorithm SHA256).Hash.ToLowerInvariant()
    })

    # Apply the one-file CMake repair and generate a reviewable patch plus hashes.
    $repairEvidence = Join-Path $OutputDirectory 'repair'
    & $PythonPath -m scripts.testing.workbook05.route_b_repair `
        --source-root $SourceRoot `
        --evidence-root $repairEvidence `
        --source-commit $SourceCommit
    if ($LASTEXITCODE -ne 0) {
        throw "Route B repair module exited with code $LASTEXITCODE."
    }

    # Refuse algorithm drift or any second changed external file.
    $changedFiles = @(& git -C $SourceRoot diff --name-only)
    if ($changedFiles.Count -ne 1 -or $changedFiles[0].Replace('\', '/') -ne $ExpectedRepairPath) {
        throw "Repair changed an unexpected external file set: $($changedFiles -join ', ')"
    }
    & git -C $SourceRoot diff --check
    if ($LASTEXITCODE -ne 0) {
        throw "External repair patch failed git diff --check with code $LASTEXITCODE."
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'changed-files.json') -Value ([ordered]@{
        expected = @($ExpectedRepairPath)
        actual = @($changedFiles | ForEach-Object { $_.Replace('\', '/') })
        algorithm_files_changed = $false
    })

    # Generate the Visual Studio solution using the exact generator documented by
    # the pinned source. Extra flags enable only the test boundary being examined
    # and disable the optional GPU plugin as instructed by that document.
    $configureArgs = @(
        '-S', $SourceRoot,
        '-B', $BuildRoot,
        '-G', 'Visual Studio 17 2022',
        '-A', 'x64',
        '-DENABLE_INTEL_GPU=OFF',
        '-DENABLE_TESTS=ON',
        '-DENABLE_CPU_SPECIFIC_TARGET_PER_TEST=ON'
    )
    $configure = Invoke-LoggedNativeCommand -Name 'cmake-configure' -FilePath $CMakePath -ArgumentList $configureArgs -WorkingDirectory $WorkRoot -EvidenceDirectory $OutputDirectory
    if ($configure.exit_code -ne 0) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR3' -Reasons @("CMake configure failed with code $($configure.exit_code).")
        return
    }

    # Extract only controlled cache values and relevant target membership. The
    # generated project files remain in runner temp and are never uploaded.
    $cachePath = Join-Path $BuildRoot 'CMakeCache.txt'
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        throw 'CMake configure returned success without CMakeCache.txt.'
    }
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
        $line = Get-Content -LiteralPath $cachePath | Where-Object { $_ -match "^$([Regex]::Escape($name)):[^=]+=" } | Select-Object -First 1
        $cacheValues[$name] = if ($line) { ($line -split '=', 2)[1] } else { $null }
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'cmake-cache-summary.json') -Value ([ordered]@{
        sha256 = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash.ToLowerInvariant()
        values = $cacheValues
    })

    $projectFile = Get-ChildItem -LiteralPath $BuildRoot -Filter "$ExpectedTarget.vcxproj" -File -Recurse | Select-Object -First 1
    if (-not $projectFile) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR3' -Reasons @("Generated target project was not found: $ExpectedTarget")
        return
    }
    $projectText = Get-Content -LiteralPath $projectFile.FullName -Raw
    $membership = @([Regex]::Matches($projectText, 'Include="([^"]*concat_sdp_turboq\.cpp)"') | ForEach-Object { $_.Groups[1].Value.Replace('\', '/') } | Sort-Object -Unique)
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
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR3' -Reasons @('Generated target metadata does not contain both the class and X64 instance source.')
        return
    }

    # Build only the narrow per-test target, using two parallel jobs to avoid
    # exhausting the controlled laptop while still producing executable evidence.
    $build = Invoke-LoggedNativeCommand -Name 'cmake-build-route-b-target' -FilePath $CMakePath -ArgumentList @('--build', $BuildRoot, '--config', 'Release', '--target', $ExpectedTarget, '--parallel', '2') -WorkingDirectory $WorkRoot -EvidenceDirectory $OutputDirectory
    if ($build.exit_code -ne 0) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR6' -Reasons @("Narrow Route B test target failed to build with code $($build.exit_code).")
        return
    }

    $testExecutable = Get-ChildItem -LiteralPath $SourceRoot -Filter "$ExpectedTarget.exe" -File -Recurse | Select-Object -First 1
    if (-not $testExecutable) {
        $testExecutable = Get-ChildItem -LiteralPath $BuildRoot -Filter "$ExpectedTarget.exe" -File -Recurse | Select-Object -First 1
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

    # Discover tests before executing any filter. A zero-test green result is
    # explicitly rejected.
    $env:OV_TURBOQ_ROTATION = 'wht'
    $discovery = Invoke-LoggedNativeCommand -Name 'gtest-discovery' -FilePath $testExecutable.FullName -ArgumentList @('--gtest_list_tests', '--gtest_color=no') -WorkingDirectory $testExecutable.DirectoryName -EvidenceDirectory $OutputDirectory
    if ($discovery.exit_code -ne 0) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR7' -Reasons @("GTest discovery failed with code $($discovery.exit_code).")
        return
    }
    $discoveryText = Get-Content -LiteralPath (Join-Path $OutputDirectory 'gtest-discovery.stdout.log') -Raw
    $discoveredCount = @($discoveryText -split "`r?`n" | Where-Object { $_ -match '^\s{2,}\S' }).Count
    if ($discoveredCount -le 0) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR7' -Reasons @('The narrow target reported zero discovered tests.')
        return
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'test-discovery-summary.json') -Value ([ordered]@{
        discovered_test_count = $discoveredCount
        has_f32_named_cases = $discoveryText -match 'Prc=f32'
        has_qjl3_cases = $discoveryText -match 'tbq3_qjl'
        has_qjl4_cases = $discoveryText -match 'tbq4_qjl'
        has_polar3_cases = $discoveryText -match 'polar3'
        has_polar4_cases = $discoveryText -match 'polar4'
        has_asymmetric_none_tbq_cases = $discoveryText -match 'K=none_V=tbq4'
    })

    # Execute one narrow filter for each required candidate family and one
    # asymmetric case. Preserve every raw result without loosening thresholds.
    $filters = [ordered]@{
        qjl4 = '*K=tbq4_qjl_V=tbq4_qjl*'
        qjl3 = '*K=tbq3_qjl_V=tbq3_qjl*'
        polar4 = '*K=polar4_V=polar4*'
        polar3 = '*K=polar3_V=polar3*'
        asymmetric_f32_tbq4 = '*K=none_V=tbq4*'
    }
    $testResults = @()
    $anyFailure = $false
    foreach ($entry in $filters.GetEnumerator()) {
        $result = Invoke-LoggedNativeCommand -Name "test-$($entry.Key)" -FilePath $testExecutable.FullName -ArgumentList @("--gtest_filter=$($entry.Value)", '--gtest_color=no') -WorkingDirectory $testExecutable.DirectoryName -EvidenceDirectory $OutputDirectory
        $outputText = Get-Content -LiteralPath (Join-Path $OutputDirectory "test-$($entry.Key).stdout.log") -Raw
        $match = [Regex]::Match($outputText, 'Running\s+(\d+)\s+tests?')
        $runCount = if ($match.Success) { [int]$match.Groups[1].Value } else { 0 }
        if ($result.exit_code -ne 0 -or $runCount -le 0) {
            $anyFailure = $true
        }
        $testResults += [ordered]@{
            id = $entry.Key
            filter = $entry.Value
            exit_code = $result.exit_code
            run_count = $runCount
            passed = ($result.exit_code -eq 0 -and $runCount -gt 0)
        }
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'test-results.json') -Value $testResults

    if ($anyFailure) {
        Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'Blocked' -Checkpoint 'BR7' -Reasons @('One or more required Route B conformance filters failed or executed zero tests.')
        return
    }

    Write-DecisionAndManifest -EvidenceDirectory $OutputDirectory -Status 'ExecutableCandidate' -Checkpoint 'BR8' -Reasons @('The bounded repair, generated membership, narrow build and selected repository conformance tests passed. Model activation remains unauthorised.')
}
catch {
    # Integrity, provenance or contract failures are fatal. Preserve a small
    # machine-readable record, hash all existing evidence and rethrow.
    $failure = [ordered]@{
        schema_version = '1.0'
        status = 'IntegrityFailure'
        source_commit = $SourceCommit
        message = $_.Exception.Message
        granite_model_test_authorised = $false
    }
    Write-JsonFile -Path (Join-Path $OutputDirectory 'integrity-failure.json') -Value $failure
    Write-EvidenceManifest -EvidenceDirectory $OutputDirectory
    throw
}
