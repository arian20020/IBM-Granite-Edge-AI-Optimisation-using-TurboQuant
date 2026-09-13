using System.Text.RegularExpressions;

using System.Diagnostics;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class PackagedTestCheckpointTests
{
    [TestMethod]
    public void ProductionWrapperExposesOnlyFilterAndEvidenceDirectory()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string script = Path.Combine(root, "scripts", "verification", "Invoke-PackagedTestCheckpoint.ps1");
        string wrapper = File.ReadAllText(script);
        const string disableNodeReuse = "$env:MSBUILDDISABLENODEREUSE = '1'";
        const string nodeReuseArgument = "'/nodeReuse:false'";
        const string disableSharedCompilation = "'/property:UseSharedCompilation=false'";
        const string disableSharedCompilationEnvironment = "$env:UseSharedCompilation = 'false'";
        const string disableDotnetBuildServer = "$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'";
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(wrapper, System.Text.RegularExpressions.Regex.Escape(disableNodeReuse)).Count, "Production wrapper must disable MSBuild node reuse exactly once.");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(wrapper, System.Text.RegularExpressions.Regex.Escape(nodeReuseArgument)).Count, "Production build must pass /nodeReuse:false exactly once.");
        Assert.AreEqual(1, Regex.Matches(wrapper, Regex.Escape(disableSharedCompilation)).Count, "Production build must disable shared compilation exactly once.");
        Assert.AreEqual(1, Regex.Matches(wrapper, Regex.Escape(disableSharedCompilationEnvironment)).Count, "Production wrapper must disable shared compilation in the environment exactly once.");
        Assert.AreEqual(1, Regex.Matches(wrapper, Regex.Escape(disableDotnetBuildServer)).Count, "Production wrapper must disable the .NET CLI MSBuild server exactly once.");
        Assert.IsTrue(wrapper.IndexOf(disableNodeReuse, StringComparison.Ordinal) < wrapper.IndexOf("$buildAction = {", StringComparison.Ordinal), "Node reuse must be disabled before build execution.");
        Assert.IsTrue(wrapper.IndexOf(disableSharedCompilationEnvironment, StringComparison.Ordinal) < wrapper.IndexOf("$buildAction = {", StringComparison.Ordinal), "Shared compilation must be disabled in the environment before build execution.");
        Assert.IsTrue(wrapper.IndexOf(disableDotnetBuildServer, StringComparison.Ordinal) < wrapper.IndexOf("$buildAction = {", StringComparison.Ordinal), "The .NET CLI MSBuild server must be disabled before build execution.");
        Assert.IsTrue(
            wrapper.IndexOf(nodeReuseArgument, StringComparison.Ordinal) < wrapper.IndexOf(disableSharedCompilation, StringComparison.Ordinal) &&
            wrapper.IndexOf(disableSharedCompilation, StringComparison.Ordinal) < wrapper.IndexOf("'/verbosity:minimal'", StringComparison.Ordinal),
            "Shared compilation must be disabled immediately within the serial build argument block.");
        Assert.IsTrue(
            Regex.IsMatch(wrapper, @"(?s)\$testAction\s*=\s*\{.*?Invoke-PackagedProcess.*?-TimeoutSeconds\s+1800.*?\r?\n\s*\}"),
            "The packaged VSTest action must have the reviewed 1,800-second bound.");
        string evidence = Path.Combine(Path.GetTempPath(), $"geai-wrapper-reject-{Guid.NewGuid():N}");
        var result = OptimizationImportManifestTests.RunPowerShell(root, 30,
            "-File", script, "-Filter", "Fake", "-EvidenceDirectory", evidence,
            "-BuildExecutable", "powershell.exe", "-BuildArguments", "-NoProfile -Command exit 7");
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "parameter cannot be found", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(Directory.Exists(evidence), "Parameter binding must reject overrides before build discovery or evidence creation.");
    }

    [TestMethod]
    public void TimeoutTerminationContractUsesKillOnCloseJobAndBoundedStreams()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string runnerFile = File.ReadAllText(Path.Combine(root, "tests", "UnitTests", "GraniteEdgeAI.UnitTests",
            "Features", "ModelOptimization", "OptimizationImportManifestTests.cs"));
        Match runnerMatch = Regex.Match(runnerFile,
            @"(?s)internal static class Task1ProcessRunner(?<body>.*?)\r?\n}\r?\n\r?\n\[TestClass\]");
        Assert.IsTrue(runnerMatch.Success, "Task1ProcessRunner source boundary was not found.");
        string runner = runnerMatch.Groups["body"].Value;
        foreach (string coreName in new[] { "PackagedCheckpoint.Core.psm1", "CrossRouteImportManifest.Core.psm1" })
        {
            string core = File.ReadAllText(Path.Combine(root, "scripts", "verification", coreName));
            Assert.IsFalse(Regex.IsMatch(core, @"WaitForExit\s*\(\s*\)"), $"Every process wait in {coreName} must be bounded.");
            Assert.IsFalse(Regex.IsMatch(core, @"catch\s*\{\s*\}"), $"Termination failures in {coreName} must not be swallowed.");
        }
        string packagedCore = File.ReadAllText(Path.Combine(root, "scripts", "verification", "PackagedCheckpoint.Core.psm1"));
        StringAssert.Contains(packagedCore, "JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE");
        StringAssert.Contains(packagedCore, "AssignProcessToJobObject");
        StringAssert.Contains(packagedCore, "TerminateJobObject");
        StringAssert.Contains(packagedCore, "CREATE_SUSPENDED");
        StringAssert.Contains(packagedCore, "EXTENDED_STARTUPINFO_PRESENT");
        StringAssert.Contains(packagedCore, "PROC_THREAD_ATTRIBUTE_HANDLE_LIST");
        StringAssert.Contains(packagedCore, "InitializeProcThreadAttributeList");
        StringAssert.Contains(packagedCore, "UpdateProcThreadAttribute");
        StringAssert.Contains(packagedCore, "CreateProcessW");
        StringAssert.Contains(packagedCore, "ResumeThread");
        StringAssert.Contains(packagedCore, "GetAuthoritativeExitCode");
        StringAssert.Contains(packagedCore, "Injected checkpoint process construction failure.");
        StringAssert.Contains(packagedCore, "post-termination stream capture");
        StringAssert.Contains(packagedCore, "Task]::WaitAll");
        Assert.IsFalse(packagedCore.Contains("GetStdHandle", StringComparison.Ordinal), "Suspended launch must not inherit ambient standard handles.");
        Assert.IsTrue(packagedCore.Contains("Marshal.WriteIntPtr(inheritedHandles, 0, stdinRead)", StringComparison.Ordinal));
        Assert.IsTrue(packagedCore.Contains("Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, stdoutWrite)", StringComparison.Ordinal));
        Assert.IsTrue(packagedCore.Contains("Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size * 2, stderrWrite)", StringComparison.Ordinal));
        Match constructionCatch = Regex.Match(packagedCore, @"(?s)catch \{\s*Exception cleanupFailure = null;(?<body>.*?)\s*throw;\s*\}");
        Assert.IsTrue(constructionCatch.Success, "Owned-process construction cleanup block was not found.");
        Assert.IsTrue(
            constructionCatch.Groups["body"].Value.IndexOf("job.Dispose()", StringComparison.Ordinal) <
            constructionCatch.Groups["body"].Value.IndexOf("TerminateProcess(information.hProcess", StringComparison.Ordinal),
            "Construction cleanup must close the kill-on-close Job before its bounded native fallback.");
        Assert.IsFalse(runner.Contains("process.WaitForExit();", StringComparison.Ordinal), "C# timeout cleanup must use a bounded verified wait.");
        Assert.IsFalse(Regex.IsMatch(runner, @"catch\s*\{\s*\}"), "C# termination failures must not be swallowed.");
        Assert.AreEqual(2, Regex.Matches(runner, @"\bTerminateExactOwnedTree\s*\(").Count,
            "The exact-owned-tree helper must have one definition and one call from the timeout branch.");
        Match timeoutBranch = Regex.Match(runner,
            @"(?s)if \(!process\.WaitForExit\(checked\(timeoutSeconds \* 1000\)\)\)(?<body>.*?)\r?\n\s*}\r?\n\s*int exitCode");
        Assert.IsTrue(timeoutBranch.Success, "The bounded C# timeout branch was not found.");
        StringAssert.Contains(timeoutBranch.Groups["body"].Value,
            "TerminateExactOwnedTree(process, processId, processStartTimeUtc)");
        Assert.IsTrue(runner.Contains("int processId = process.Id;", StringComparison.Ordinal),
            "The owned PID must be captured before the timeout wait.");
        Assert.IsTrue(runner.Contains("DateTime processStartTimeUtc = process.StartTime.ToUniversalTime();", StringComparison.Ordinal),
            "The owned process start time must be captured before the timeout wait.");
        Assert.IsTrue(runner.Contains("Environment.SpecialFolder.System", StringComparison.Ordinal));
        Assert.IsTrue(runner.Contains("expectedStartTimeUtc", StringComparison.Ordinal));
        StringAssert.Contains(runner, "RunConstructionFaultProbe");
        StringAssert.Contains(runner, "Injected Task1 owned-process construction failure.");
        StringAssert.Contains(runner, "WaitForSingleObject");
        Match ownedConstructionCatch = Regex.Match(runner,
            @"(?s)catch\s*\{(?<body>.*?)Task1 owned process construction cleanup failed\.(?:.*?)\r?\n\s*throw;\s*\}");
        Assert.IsTrue(ownedConstructionCatch.Success, "Transactional Task1OwnedProcess construction cleanup was not found.");
        Assert.IsTrue(
            ownedConstructionCatch.Groups["body"].Value.IndexOf("job.Dispose()", StringComparison.Ordinal) <
            ownedConstructionCatch.Groups["body"].Value.IndexOf("TerminateProcess(information.Process", StringComparison.Ordinal),
            "Post-assignment construction cleanup must close the kill-on-close Job before native-handle fallback.");
        foreach (string managedCleanup in new[] { "output?.Dispose()", "error?.Dispose()", "process?.Dispose()" })
            StringAssert.Contains(ownedConstructionCatch.Groups["body"].Value, managedCleanup);

        string command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        HashSet<int> before = GetProcessIds("ping");
        Assert.ThrowsExactly<TimeoutException>(() => Task1ProcessRunner.Run(command, root,
            new[] { "/d", "/s", "/c", "ping.exe -n 120 127.0.0.1 >nul" }, 1));
        AssertNoNewProcesses("ping", before, "ordinary timeout cleanup");

        before = GetProcessIds("ping");
        TimeoutException fallback = Assert.ThrowsExactly<TimeoutException>(() => Task1ProcessRunner.Run(command, root,
            new[] { "/d", "/s", "/c", "ping.exe -n 120 127.0.0.1 >nul" }, 1,
            forceVerifiedTimeoutFallback: true));
        Assert.AreEqual("cmd.exe timed out; verified taskkill fallback terminated its exact process tree.", fallback.Message);
        AssertNoNewProcesses("ping", before, "forced verified taskkill fallback");

        before = GetProcessIds("ping");
        InvalidOperationException constructionFault = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Task1ProcessRunner.RunConstructionFaultProbe(command, root,
                new[] { "/d", "/s", "/c", "start \"\" /b ping.exe -n 120 127.0.0.1 >nul 2>nul & ping.exe -n 120 127.0.0.1 >nul" },
                constructionFaultDelayMilliseconds: 500));
        Assert.AreEqual("Injected Task1 owned-process construction failure.", constructionFault.Message);
        AssertNoNewProcesses("ping", before, "transactional construction cleanup");
    }

    private static HashSet<int> GetProcessIds(string processName)
    {
        HashSet<int> processIds = new();
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            using (process)
                processIds.Add(process.Id);
        }
        return processIds;
    }

    private static void AssertNoNewProcesses(string processName, HashSet<int> before, string scenario)
    {
        Stopwatch deadline = Stopwatch.StartNew();
        int[] leaked;
        do
        {
            leaked = GetProcessIds(processName).Where(id => !before.Contains(id)).ToArray();
            if (leaked.Length == 0) break;
            Thread.Sleep(50);
        }
        while (deadline.Elapsed < TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, leaked.Length, $"{scenario} leaked {processName} PIDs: {string.Join(",", leaked)}");
    }

    [TestMethod]
    public void CoreIsExplicitlyNonPublishableAndEvidenceIsPathPrivateCreateNewOwned()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string core = File.ReadAllText(Path.Combine(root, "scripts", "verification", "PackagedCheckpoint.Core.psm1"));
        string wrapper = File.ReadAllText(Path.Combine(root, "scripts", "verification", "Invoke-PackagedTestCheckpoint.ps1"));
        StringAssert.Contains(core, "$script:EvidenceClassification = 'nonpublishable-core'");
        StringAssert.Contains(wrapper, "FileMode]::CreateNew");
        StringAssert.Contains(core, "ReparsePoint");
        StringAssert.Contains(core, "Operation ownership");
        StringAssert.Contains(core, "Publishable = [bool]$false");
        Assert.IsFalse(core.Contains("ExecutableIdentities", StringComparison.Ordinal), "Core must not accept caller-supplied executable identities.");
        Assert.IsFalse(wrapper.Contains("ExecutableIdentities", StringComparison.Ordinal), "Production wrapper must derive executable identities itself.");
        foreach (string forbiddenEvidenceKey in new[] { "recipePath =", "trxPath =", "buildExecutable =", "runnerExecutable =" })
            Assert.IsFalse(wrapper.Contains(forbiddenEvidenceKey, StringComparison.OrdinalIgnoreCase), $"Evidence leaks a private path through {forbiddenEvidenceKey}.");
        Assert.IsFalse(wrapper.Contains("Write-Output $result.RunnerOutput", StringComparison.Ordinal), "Raw runner output is not publishable evidence.");
        StringAssert.Contains(wrapper, "source/component evidence only; non-Release, non-native, non-package");
        Assert.AreEqual(1, Regex.Matches(wrapper, Regex.Escape("[IO.FileMode]::CreateNew")).Count, "Checkpoint publication must use CreateNew exactly once.");
        Match evidenceBlock = Regex.Match(wrapper, @"(?s)\$evidence = \[ordered\]@\{(?<body>.*?)\r?\n    \}\r?\n    \$utf8");
        Assert.IsTrue(evidenceBlock.Success, "Production evidence block was not found.");
        Assert.IsFalse(Regex.IsMatch(evidenceBlock.Groups["body"].Value, @"(?i)(recipePath|trxPath|executablePath|runnerOutput|operationDirectory|evidenceRoot)"), "Published evidence must not contain local path or raw runner fields.");
        Match trxReader = Regex.Match(core, @"(?s)function Read-PackagedTrxSnapshot(?<body>.*?)\r?\nfunction Get-StrictTrxCounters");
        Assert.IsTrue(trxReader.Success, "Strict TRX reader source boundary was not found.");
        Assert.AreEqual(1, Regex.Matches(trxReader.Groups["body"].Value, Regex.Escape("[IO.File]::ReadAllBytes($full)")).Count, "The exact TRX bytes must be captured once.");
    }

    [TestMethod]
    public void CheckpointBindsFilterTestAndPayloadIdentityAndPublishesNoRawTrx()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string core = File.ReadAllText(Path.Combine(root, "scripts", "verification", "PackagedCheckpoint.Core.psm1"));
        string wrapper = File.ReadAllText(Path.Combine(root, "scripts", "verification", "Invoke-PackagedTestCheckpoint.ps1"));

        foreach (string marker in new[]
        {
            "Assert-PackagedRepositoryIndexFlags", "core.fsmonitor=false", "core.fsmonitor=true", "core.hooksPath=NUL",
            "TestRunId", "TestIdentities", "FilterSha256", "PayloadSetSha256",
            "UnitTestAssemblySha256", "UnitTestAssemblyPath", "Remove-PackagedPublishedTrx", "Create-PackagedProcessJob",
            "ls-files', '-f', '-z'", "OutputBytes", "className", "methodName", "FilterUsed", "Test identity does not satisfy the exact requested filter"
        })
            Assert.IsTrue((core + wrapper).Contains(marker, StringComparison.Ordinal), $"Missing checkpoint hardening marker: {marker}");

        Assert.IsTrue(wrapper.Contains("'/target:Rebuild'", StringComparison.Ordinal), "Production checkpoint must rebuild instead of accepting incremental stale output.");
        Assert.IsTrue(wrapper.Contains("filter = $result.Filter", StringComparison.Ordinal), "Sanitized evidence must bind the exact requested filter.");
        Assert.IsTrue(wrapper.Contains("testIdentities = $result.TestIdentities", StringComparison.Ordinal), "Sanitized evidence must bind exact test identities.");
        Assert.IsTrue(wrapper.Contains("payloadSetSha256 = $result.PayloadSetSha256", StringComparison.Ordinal), "Sanitized evidence must bind the recipe payload set.");
        Assert.IsFalse(wrapper.Contains("'/target:Build'", StringComparison.Ordinal), "Incremental Build is not sufficient checkpoint evidence.");
        Assert.IsFalse(core.Contains("'write-tree'", StringComparison.Ordinal), "Repository identity must be read-only.");
        Assert.IsFalse(Regex.IsMatch(core, @"UTF8\.GetBytes\(\$(indexEntries|indexFlags|fsmonitorFlags|rawStatus)"), "Git identity snapshots must compare original stdout bytes, not decoded/re-encoded text.");
        Match indexFlags = Regex.Match(core, @"(?s)function Assert-PackagedRepositoryIndexFlags(?<body>.*?)\r?\nfunction Resolve-PackagedRepository");
        Assert.IsTrue(indexFlags.Success, "Index-flag validation source boundary was not found.");
        Assert.IsTrue(
            indexFlags.Groups["body"].Value.IndexOf("'ls-files', '-f', '-z'", StringComparison.Ordinal) <
            indexFlags.Groups["body"].Value.IndexOf("'ls-files', '-v', '-z'", StringComparison.Ordinal),
            "The exact core.fsmonitor=true query must be the first index read.");
        Assert.IsTrue(core.Contains("$fullyQualifiedName = [string]$identity.className + '.' + [string]$identity.methodName", StringComparison.Ordinal), "TRX filters must bind canonical className.methodName identities.");
    }

    [TestMethod]
    public void AllCheckpointCoreCasesRunInOneBatchDriver()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string batchRoot = Path.Combine(Path.GetTempPath(), $"geai-checkpoint-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(batchRoot);
        try
        {
            string driver = Path.Combine(batchRoot, "driver.ps1");
            File.WriteAllText(driver, BatchDriver);
            string module = Path.Combine(root, "scripts", "verification", "PackagedCheckpoint.Core.psm1");
            var result = OptimizationImportManifestTests.RunPowerShell(batchRoot, 300,
                "-File", driver, "-ModulePath", module, "-BatchRoot", batchRoot);
            Assert.AreEqual(0, result.ExitCode, result.Output);
            StringAssert.Contains(result.Output, "CHECKPOINT_BATCH_CASES=36");
            StringAssert.Contains(result.Output, "CHECKPOINT_COUNTER_CASES=87");
            StringAssert.Contains(result.Output, "CHECKPOINT_TIMEOUT_CASES=2");
            StringAssert.Contains(result.Output, "CHECKPOINT_NATIVE_EXIT_CASES=1");
            StringAssert.Contains(result.Output, "CHECKPOINT_CONSTRUCTION_FAULT_CASES=1");
            StringAssert.Contains(result.Output, "CHECKPOINT_PARENT_EXIT_CASES=1");
            StringAssert.Contains(result.Output, "CHECKPOINT_IDENTITY_SWAP_CASES=1");
            StringAssert.Contains(result.Output, "CHECKPOINT_INDEX_FLAG_CASES=3");
        }
        finally
        {
            DeleteExactOwnedCheckpointBatch(batchRoot, TimeSpan.FromSeconds(10));
        }
    }

    private static void DeleteExactOwnedCheckpointBatch(string root, TimeSpan timeout)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        string? parent = Path.GetDirectoryName(fullRoot);
        string name = Path.GetFileName(fullRoot);
        const string prefix = "geai-checkpoint-batch-";
        if (parent is null || !StringComparer.OrdinalIgnoreCase.Equals(Path.TrimEndingDirectorySeparator(parent), tempRoot) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Checkpoint cleanup root is not an exact operation-owned temporary child.");
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        Stopwatch stopwatch = Stopwatch.StartNew();
        DeleteCheckpointEntry(fullRoot, stopwatch, timeout);
    }

    private static void DeleteCheckpointEntry(string path, Stopwatch stopwatch, TimeSpan timeout)
    {
        if (!Directory.Exists(path) && !File.Exists(path)) return;
        if (stopwatch.Elapsed >= timeout) throw new TimeoutException("Checkpoint batch cleanup exceeded its bounded duration.");
        FileAttributes attributes = File.GetAttributes(path);
        bool directory = (attributes & FileAttributes.Directory) != 0;
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            if (directory) Directory.Delete(path, false); else File.Delete(path);
            return;
        }
        if (directory)
        {
            foreach (string entry in Directory.GetFileSystemEntries(path)) DeleteCheckpointEntry(entry, stopwatch, timeout);
            Directory.Delete(path, false);
            return;
        }
        if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        File.Delete(path);
    }

    private const string BatchDriver = """
        param([string]$ModulePath,[string]$BatchRoot)
        $ErrorActionPreference='Stop'
        Import-Module -Force -Name $ModulePath
        $checkpointModule=Get-Module -Name PackagedCheckpoint.Core
        $verificationRoot=Split-Path -Parent $ModulePath
        $crossRouteModule=Join-Path -Path $verificationRoot -ChildPath 'CrossRouteImportManifest.Core.psm1'
        Import-Module -Force -Name $crossRouteModule
        $structurePaths=@(
          $crossRouteModule
          $ModulePath
          (Join-Path -Path $verificationRoot -ChildPath 'Assert-ChangedPaths.ps1')
          (Join-Path -Path $verificationRoot -ChildPath 'Test-CrossRouteImportManifest.ps1')
          (Join-Path -Path $verificationRoot -ChildPath 'Invoke-PackagedTestCheckpoint.ps1')
        )
        Assert-VerificationPowerShellStructure -Path $structurePaths|Out-Null
        function Get-PingIds {
          [CmdletBinding()]
          param()
          $ids=New-Object 'Collections.Generic.List[int]'
          foreach($process in [Diagnostics.Process]::GetProcessesByName('ping')){
            try{$ids.Add($process.Id)}finally{$process.Dispose()}
          }
          return @($ids)
        }
        $command=Join-Path -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) -ChildPath 'cmd.exe'
        $timeoutCases=@('checkpoint','cross-route')
        foreach($timeoutCase in $timeoutCases){
          $before=@(Get-PingIds)
          $timedOut=$false
          try{
            if($timeoutCase-eq'checkpoint'){
              Invoke-PackagedProcess -FilePath $command -WorkingDirectory $BatchRoot -Arguments @('/d','/s','/c','ping.exe -n 120 127.0.0.1 >nul') -TimeoutSeconds 1|Out-Null
            }else{
              Invoke-CrossRouteProcess -FilePath $command -WorkingDirectory $BatchRoot -Arguments @('/d','/s','/c','ping.exe -n 120 127.0.0.1 >nul') -TimeoutSeconds 1|Out-Null
            }
          }catch{
            if($_.Exception.Message-notmatch'timed out'){throw}
            $timedOut=$true
          }
          if(-not$timedOut){throw('timeout case unexpectedly completed: '+$timeoutCase)}
          $deadline=[DateTime]::UtcNow.AddSeconds(5)
          do{
            $leaked=@(Get-PingIds|Where-Object{$before-notcontains$_})
            if($leaked.Count-eq0){break}
            Start-Sleep -Milliseconds 50
          }while([DateTime]::UtcNow-lt$deadline)
          if($leaked.Count-ne0){throw('timeout case leaked ping children: '+$timeoutCase+' '+($leaked-join','))}
        }
        'CHECKPOINT_TIMEOUT_CASES='+$timeoutCases.Count
        $nonzero=Invoke-PackagedProcess -FilePath $command -WorkingDirectory $BatchRoot -Arguments @('/d','/s','/c','exit /b 37') -TimeoutSeconds 10
        if($nonzero.ExitCode-ne37){throw('native nonzero exit was not preserved: '+$nonzero.ExitCode)}
        if($nonzero.OutputBytes.GetType()-ne[byte[]]-or$nonzero.ErrorBytes.GetType()-ne[byte[]]){throw'native process result did not preserve exact stdout/stderr bytes'}
        'CHECKPOINT_NATIVE_EXIT_CASES=1'
        $beforeConstruction=@(Get-PingIds)
        $constructionStart=New-Object Diagnostics.ProcessStartInfo
        $constructionStart.FileName=$command
        $constructionStart.WorkingDirectory=$BatchRoot
        $constructionStart.UseShellExecute=$false
        $constructionStart.CreateNoWindow=$true
        $constructionStart.RedirectStandardOutput=$true
        $constructionStart.RedirectStandardError=$true
        $constructionStart.Arguments='/d /s /c start "" /b ping.exe -n 120 127.0.0.1 ^>nul 2^>nul ^& ping.exe -n 120 127.0.0.1 ^>nul'
        $constructionRejected=$false
        try{
          [PackagedCheckpointOwnedProcess]::Start($constructionStart,500)|Out-Null
        }catch{
          $constructionException=$_.Exception
          while($null-ne$constructionException.InnerException){$constructionException=$constructionException.InnerException}
          if($constructionException.Message-cne'Injected checkpoint process construction failure.'){throw}
          $constructionRejected=$true
        }
        if(-not$constructionRejected){throw'construction fault injection unexpectedly returned an owned process'}
        $constructionDeadline=[DateTime]::UtcNow.AddSeconds(5)
        do{
          $constructionLeaked=@(Get-PingIds|Where-Object{$beforeConstruction-notcontains$_})
          if($constructionLeaked.Count-eq0){break}
          Start-Sleep -Milliseconds 50
        }while([DateTime]::UtcNow-lt$constructionDeadline)
        if($constructionLeaked.Count-ne0){throw('construction fault leaked ping children: '+($constructionLeaked-join','))}
        'CHECKPOINT_CONSTRUCTION_FAULT_CASES=1'
        $beforeExit=@(Get-PingIds)
        $powershell=Join-Path -Path (Join-Path -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) -ChildPath 'WindowsPowerShell\v1.0') -ChildPath 'powershell.exe'
        $spawn='Start-Process -FilePath $env:ComSpec -ArgumentList @(''/d'',''/s'',''/c'',''ping.exe -n 120 127.0.0.1 >nul 2>nul'') -WindowStyle Hidden'
        Invoke-PackagedProcess -FilePath $powershell -WorkingDirectory $BatchRoot -Arguments @('-NoProfile','-NonInteractive','-Command',$spawn) -TimeoutSeconds 10|Out-Null
        $exitDeadline=[DateTime]::UtcNow.AddSeconds(5)
        do{
          $exitLeaked=@(Get-PingIds|Where-Object{$beforeExit-notcontains$_})
          if($exitLeaked.Count-eq0){break}
          Start-Sleep -Milliseconds 50
        }while([DateTime]::UtcNow-lt$exitDeadline)
        if($exitLeaked.Count-ne0){throw('normal parent exit leaked job-owned ping children: '+($exitLeaked-join','))}
        'CHECKPOINT_PARENT_EXIT_CASES=1'
        $identityPath=Join-Path -Path $BatchRoot -ChildPath 'identity-swap.exe'
        [IO.File]::WriteAllText($identityPath,'before',(New-Object Text.UTF8Encoding($false)))
        $identityBefore=& $checkpointModule {param($Path)Get-PackagedRegularFileIdentity -Path $Path -Label 'Synthetic executable'} $identityPath
        [IO.File]::WriteAllText($identityPath,'after',(New-Object Text.UTF8Encoding($false)))
        $identityRejected=$false
        try{
          & $checkpointModule {param($Before,$Path)$after=Get-PackagedRegularFileIdentity -Path $Path -Label 'Synthetic executable';Assert-PackagedFileIdentityUnchanged -Before $Before -After $after -Label 'Synthetic executable'} $identityBefore $identityPath
        }catch{$identityRejected=$true}
        if(-not$identityRejected){throw'Executable identity swap unexpectedly passed'}
        'CHECKPOINT_IDENTITY_SWAP_CASES=1'
        $indexFlagCases=@(
          [pscustomobject]@{Name='uppercase-valid';Kind='Fsmonitor';Text="H tracked.txt`0";Expected=$null},
          [pscustomobject]@{Name='lowercase-fsmonitor';Kind='Fsmonitor';Text="h tracked.txt`0";Expected='Repository contains an fsmonitor-valid tracked entry.'},
          [pscustomobject]@{Name='missing-nul';Kind='Fsmonitor';Text='H tracked.txt';Expected='Git fsmonitor-index flags is not terminated by an exact NUL record boundary.'}
        )
        foreach($indexFlagCase in $indexFlagCases){
          $indexFlagMessage=$null
          try{
            & $checkpointModule {param($Bytes,$Kind)Assert-PackagedIndexFlagRecords -Bytes $Bytes -Kind $Kind} ([Text.Encoding]::UTF8.GetBytes($indexFlagCase.Text)) $indexFlagCase.Kind
          }catch{$indexFlagMessage=$_.Exception.Message}
          if($null-eq$indexFlagCase.Expected-and$null-ne$indexFlagMessage){throw('valid index flag record rejected: '+$indexFlagMessage)}
          if($null-ne$indexFlagCase.Expected-and-not[StringComparer]::Ordinal.Equals($indexFlagMessage,[string]$indexFlagCase.Expected)){throw('index flag case wrong rejection: '+$indexFlagCase.Name+': '+$indexFlagMessage)}
        }
        'CHECKPOINT_INDEX_FLAG_CASES='+$indexFlagCases.Count
        function Git {
          [CmdletBinding()]
          param([Parameter(Mandatory)][string]$Repository,[Parameter(Mandatory)][string[]]$Arguments)
          $result=Invoke-PackagedProcess -FilePath git.exe -WorkingDirectory $Repository -Arguments (@('-C',$Repository)+$Arguments) -TimeoutSeconds 30
          if($result.ExitCode-ne0){throw $result.Error}
          $result.Output.Trim()
        }
        $modes=@(
          'valid','stale-dependency','past-recipe','future-recipe','missing-recipe','missing-trx','wrong-trx-name','past-trx','future-trx','malformed-trx',
          'zero-discovery','runner-exit','recipe-mismatch','repository-mutation','repo-head-mutation','repo-index-mutation',
          'build-array','build-hashtable','build-extra','build-exit-type','build-null','build-exit-null',
          'test-array','test-hashtable','test-extra','test-exit-type','test-null','filter-trx-mismatch','filter-display-name-only','filter-action-mismatch','test-path-null',
          'evidence-inside-repository','recipe-identity-mutation','stale-payload','future-payload','hidden-index-flag'
        )
        $expectedRejections=@{
          'past-recipe'='Recipe timestamp is stale or future-dated.';'future-recipe'='Recipe timestamp is stale or future-dated.';'missing-recipe'='Fresh recipe must exist as an exact regular file.';
          'missing-trx'='Operation-owned TRX must exist as an exact regular file.';'wrong-trx-name'='Test runner did not report the exact operation-owned TRX path.';'past-trx'='TRX timestamp is stale or future-dated for the bounded run.';'future-trx'='TRX timestamp is stale or future-dated for the bounded run.';'malformed-trx'='TRX is malformed or unsafe XML.';
          'zero-discovery'='TRX requires total=executed=passed>0.';'runner-exit'='Test runner failed with exit code 7.';'recipe-mismatch'='Test runner did not receive the exact fresh recipe.';
          'repository-mutation'='Repository root, HEAD, tree, byte-exact index flags, or raw status changed during checkpoint.';'repo-head-mutation'='Repository root, HEAD, tree, byte-exact index flags, or raw status changed during checkpoint.';'repo-index-mutation'='Repository root, HEAD, tree, byte-exact index flags, or raw status changed during checkpoint.';
          'build-array'='Build action must return exactly one PSCustomObject.';'build-hashtable'='Build action must return exactly one PSCustomObject.';'build-extra'='Build action result has an invalid property set.';'build-exit-type'='Build action result property has an invalid exact type: ExitCode';'build-null'='Build action must return exactly one PSCustomObject.';'build-exit-null'='Build action result property has an invalid exact type: ExitCode';
          'test-array'='Test action must return exactly one PSCustomObject.';'test-hashtable'='Test action must return exactly one PSCustomObject.';'test-extra'='Test action result has an invalid property set.';'test-exit-type'='Test action result property has an invalid exact type: ExitCode';'test-null'='Test action must return exactly one PSCustomObject.';
          'filter-trx-mismatch'='Test identity does not satisfy the exact requested filter.';'filter-display-name-only'='Test identity does not satisfy the exact requested filter.';'filter-action-mismatch'='Test action did not use the exact requested filter.';'test-path-null'='Test action result property has an invalid exact type: RecipePathUsed';
          'evidence-inside-repository'='EvidenceDirectory must be outside the source repository.';'recipe-identity-mutation'='Fresh recipe identity changed during the bounded operation.';'stale-payload'='Recipe references a stale or future-dated generated payload.';'future-payload'='Recipe references a stale or future-dated generated payload.';'hidden-index-flag'='Repository contains an assume-unchanged or skip-worktree tracked entry.'
        }
        if($modes.Count-ne36-or$expectedRejections.Count-ne34-or@($modes|Where-Object{$_-notin@('valid','stale-dependency')-and-not$expectedRejections.ContainsKey($_)}).Count-ne0){throw'fixed checkpoint rejection contract is incomplete'}
        $failures=New-Object 'Collections.Generic.List[string]'
        foreach($mode in $modes){
          $expectedSuccess=$mode-in@('valid','stale-dependency')
          $root=Join-Path $BatchRoot ('case-'+$mode)
          $repository=Join-Path $root 'repository'
          [IO.Directory]::CreateDirectory($repository)|Out-Null
          Git -Repository $repository -Arguments @('init','--quiet')|Out-Null
          Git -Repository $repository -Arguments @('config','user.email','tests@example.invalid')|Out-Null
          Git -Repository $repository -Arguments @('config','user.name','Tests')|Out-Null
          [IO.File]::WriteAllText((Join-Path $repository '.gitignore'),"obj/`nevidence/`n",(New-Object Text.UTF8Encoding($false)))
          [IO.File]::WriteAllText((Join-Path $repository 'tracked.txt'),'clean',(New-Object Text.UTF8Encoding($false)))
          Git -Repository $repository -Arguments @('add','.gitignore','tracked.txt')|Out-Null
          Git -Repository $repository -Arguments @('commit','--quiet','-m','fixture')|Out-Null
          if($mode-eq'hidden-index-flag'){Git -Repository $repository -Arguments @('update-index','--assume-unchanged','tracked.txt')|Out-Null}
          $recipe=Join-Path $repository 'obj/tests.build.appxrecipe'
          $payload=Join-Path $repository 'obj/GraniteEdgeAI.UnitTests.dll'
          $dependency=Join-Path $repository 'obj/ThirdParty.Dependency.dll'
          $evidence=if($mode-eq'evidence-inside-repository'){Join-Path $repository 'evidence'}else{Join-Path $root 'evidence'}
          [IO.Directory]::CreateDirectory($evidence)|Out-Null
          $build={param($context)
            if($mode-ne'missing-recipe'){
              [IO.Directory]::CreateDirectory((Split-Path -Parent $context.RecipePath))|Out-Null
              [IO.File]::WriteAllText($payload,'fresh-test-assembly',(New-Object Text.UTF8Encoding($false)))
              if($mode-eq'stale-payload'){(Get-Item $payload).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(-1)}
              if($mode-eq'future-payload'){(Get-Item $payload).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(1)}
              $escaped=[Security.SecurityElement]::Escape($payload)
              $dependencyEntry=''
              if($mode-eq'stale-dependency'){
                [IO.File]::WriteAllText($dependency,'vendor-bytes',(New-Object Text.UTF8Encoding($false)))
                (Get-Item $dependency).LastWriteTimeUtc=[DateTime]::UtcNow.AddYears(-1)
                $dependencyEscaped=[Security.SecurityElement]::Escape($dependency)
                $dependencyEntry='<AppxPackagedFile Include="'+$dependencyEscaped+'"><PackagePath>ThirdParty.Dependency.dll</PackagePath></AppxPackagedFile>'
              }
              $recipeXml='<?xml version="1.0"?><Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><ItemGroup><AppxPackagedFile Include="'+$escaped+'"><PackagePath>GraniteEdgeAI.UnitTests.dll</PackagePath></AppxPackagedFile>'+$dependencyEntry+'</ItemGroup></Project>'
              [IO.File]::WriteAllText($context.RecipePath,$recipeXml,(New-Object Text.UTF8Encoding($false)))
              if($mode-eq'past-recipe'){(Get-Item $context.RecipePath).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(-1)}
              if($mode-eq'future-recipe'){(Get-Item $context.RecipePath).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(1)}
            }
            if($mode-eq'build-array'){return @([pscustomobject]@{ExitCode=0},[pscustomobject]@{ExitCode=0})}
            if($mode-eq'build-hashtable'){return @{ExitCode=0}}
            if($mode-eq'build-extra'){return [pscustomobject]@{ExitCode=0;Unexpected='value'}}
            if($mode-eq'build-exit-type'){return [pscustomobject]@{ExitCode='0'}}
            if($mode-eq'build-null'){return $null}
            if($mode-eq'build-exit-null'){return [pscustomobject]@{ExitCode=$null}}
            [pscustomobject]@{ExitCode=0}
          }
          $run={param($context)
            if($mode-eq'repository-mutation'){[IO.File]::AppendAllText((Join-Path $repository 'tracked.txt'),'changed')}
            if($mode-eq'repo-index-mutation'){[IO.File]::AppendAllText((Join-Path $repository 'tracked.txt'),'index');Git -Repository $repository -Arguments @('add','tracked.txt')|Out-Null}
            if($mode-eq'repo-head-mutation'){[IO.File]::AppendAllText((Join-Path $repository 'tracked.txt'),'head');Git -Repository $repository -Arguments @('add','tracked.txt')|Out-Null;Git -Repository $repository -Arguments @('commit','--quiet','-m','mutation')|Out-Null}
            if($mode-eq'recipe-identity-mutation'){[IO.File]::AppendAllText($context.RecipePath,'mutated')}
            if($mode-eq'test-null'){return $null}
            if($mode-eq'missing-trx'){return [pscustomobject]@{ExitCode=[int]0;Output=[string]'';Error=[string]'';RecipePathUsed=[string]$context.RecipePath;TrxPathUsed=[string]$context.TrxPath;FilterUsed=[string]$context.Filter}}
            $target=if($mode-eq'wrong-trx-name'){Join-Path $context.ResultsDirectory 'wrong.trx'}else{$context.TrxPath}
            $attributes='total="2" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0"'
            if($mode-eq'zero-discovery'){$attributes=$attributes-replace'total="2" executed="2" passed="2"','total="0" executed="0" passed="0"'}
            $testRunId='11111111-1111-1111-1111-111111111111'
            $testOne='22222222-2222-2222-2222-222222222222'
            $testTwo='33333333-3333-3333-3333-333333333333'
            $executionOne='44444444-4444-4444-4444-444444444444'
            $executionTwo='55555555-5555-5555-5555-555555555555'
            $testPrefix=if($mode-eq'filter-trx-mismatch'){'Other'}else{'Fixture'}
            $results='<Results><UnitTestResult executionId="'+$executionOne+'" testId="'+$testOne+'" testName="'+$testPrefix+'.One" outcome="Passed" /><UnitTestResult executionId="'+$executionTwo+'" testId="'+$testTwo+'" testName="'+$testPrefix+'.Two" outcome="Passed" /></Results>'
            $payloadXml=[Security.SecurityElement]::Escape($payload)
            $definitionPrefix=if($mode-eq'filter-display-name-only'){'Other'}else{$testPrefix}
            $definitions='<TestDefinitions><UnitTest id="'+$testOne+'" name="'+$testPrefix+'.One" storage="'+$payloadXml+'"><Execution id="'+$executionOne+'" /><TestMethod codeBase="'+$payloadXml+'" className="'+$definitionPrefix+'" name="'+$definitionPrefix+'.One" /></UnitTest><UnitTest id="'+$testTwo+'" name="'+$testPrefix+'.Two" storage="'+$payloadXml+'"><Execution id="'+$executionTwo+'" /><TestMethod codeBase="'+$payloadXml+'" className="'+$definitionPrefix+'" name="'+$definitionPrefix+'.Two" /></UnitTest></TestDefinitions>'
            $xml=if($mode-eq'malformed-trx'){'not xml'}else{'<?xml version="1.0"?><TestRun id="'+$testRunId+'" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'+$results+$definitions+'<ResultSummary><Counters '+$attributes+' /></ResultSummary></TestRun>'}
            [IO.File]::WriteAllText($target,$xml,(New-Object Text.UTF8Encoding($false)))
            if($mode-eq'past-trx'){(Get-Item $target).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(-1)}
            if($mode-eq'future-trx'){(Get-Item $target).LastWriteTimeUtc=[DateTime]::UtcNow.AddHours(1)}
            $usedRecipe=if($mode-eq'recipe-mismatch'){Join-Path $repository 'obj/wrong.appxrecipe'}else{$context.RecipePath}
            $runnerExitCode=if($mode-eq'runner-exit'){[int]7}else{[int]0}
            $normal=[pscustomobject]@{ExitCode=$runnerExitCode;Output=[string]'runner';Error=[string]'';RecipePathUsed=[string]$usedRecipe;TrxPathUsed=[string]$target;FilterUsed=[string]$context.Filter}
            if($mode-eq'filter-action-mismatch'){$normal.FilterUsed='FullyQualifiedName~Other'}
            if($mode-eq'test-array'){return @($normal,$normal)}
            if($mode-eq'test-hashtable'){return @{ExitCode=0;Output='runner';Error='';RecipePathUsed=$usedRecipe;TrxPathUsed=$target;FilterUsed=$context.Filter}}
            if($mode-eq'test-extra'){return [pscustomobject]@{ExitCode=[int]0;Output=[string]'runner';Error=[string]'';RecipePathUsed=[string]$usedRecipe;TrxPathUsed=[string]$target;FilterUsed=[string]$context.Filter;Unexpected='value'}}
            if($mode-eq'test-exit-type'){return [pscustomobject]@{ExitCode='0';Output=[string]'runner';Error=[string]'';RecipePathUsed=[string]$usedRecipe;TrxPathUsed=[string]$target;FilterUsed=[string]$context.Filter}}
            if($mode-eq'test-path-null'){return [pscustomobject]@{ExitCode=[int]0;Output=[string]'runner';Error=[string]'';RecipePathUsed=$null;TrxPathUsed=[string]$target;FilterUsed=[string]$context.Filter}}
            $normal
          }
          $succeeded=$false;$caughtMessage=$null
          try{
            $resultItems=@(Invoke-PackagedCheckpointCore -RepositoryRoot $repository -RecipePath $recipe -EvidenceDirectory $evidence -Filter 'FullyQualifiedName~Fixture' -BuildAction $build -TestAction $run)
            if($resultItems.Count-ne1-or$resultItems[0]-isnot[pscustomobject]){throw 'core must return exactly one PSCustomObject'}
            $result=$resultItems[0]
            $succeeded=$true
            if(-not$expectedSuccess){$failures.Add($mode+': unexpectedly succeeded')}
            if($result.Discovered-ne2-or$result.Failed-ne0){throw 'valid counters mismatch'}
            if($result.Filter-cne'FullyQualifiedName~Fixture'-or$result.FilterSha256-cne'b148ff40547b30893100e7d2ea5398675be796b1a677710d48b524981fd73212'){throw 'valid filter identity mismatch'}
            if(@($result.TestIdentities).Count-ne2-or$result.TestRunId-cne'11111111-1111-1111-1111-111111111111'){throw 'valid TRX identity mismatch'}
            $expectedPayloadCount=if($mode-eq'stale-dependency'){2}else{1}
            if($result.PayloadCount-ne$expectedPayloadCount-or$result.PayloadSetSha256-cnotmatch'^[0-9a-f]{64}$'-or$result.UnitTestAssemblySha256-cnotmatch'^[0-9a-f]{64}$'){throw 'valid payload identity mismatch'}
            if($result.EvidenceClass-cne'nonpublishable-core'-or$result.Publishable-ne$false){throw 'valid evidence classification mismatch'}
            $expectedProperties=@('CompletedUtc','Discovered','EvidenceClass','EvidenceRoot','Executed','Failed','Filter','FilterSha256','OperationDirectory','Passed','PayloadCount','PayloadSetSha256','PostHead','PostIndexTree','PostTree','PreHead','PreIndexTree','PreTree','Publishable','RecipeLastWriteUtc','RecipeLength','RecipeName','RecipeSha256','StartedUtc','TestIdentities','TestRunId','TrxLastWriteUtc','TrxLength','TrxName','TrxSha256','UnitTestAssemblyLastWriteUtc','UnitTestAssemblyLength','UnitTestAssemblySha256')
            $actualProperties=@($result.PSObject.Properties.Name|Sort-Object)
            if(($actualProperties-join"`0")-cne(($expectedProperties|Sort-Object)-join"`0")){throw 'valid core result property set mismatch'}
            if($result.Discovered.GetType()-ne[int]-or$result.Publishable.GetType()-ne[bool]-or$result.RecipeLength.GetType()-ne[long]){throw 'valid core result exact type mismatch'}
          }catch{
            $caughtMessage=$_.Exception.Message
          }
          if($expectedSuccess-and$null-ne$caughtMessage){$failures.Add($mode+': '+$caughtMessage)}
          if(-not$expectedSuccess-and-not$succeeded){
            if($null-eq$caughtMessage){$failures.Add($mode+': no rejection message')}
             elseif(-not[StringComparer]::Ordinal.Equals($caughtMessage,[string]$expectedRejections[$mode])){$failures.Add($mode+': wrong rejection: '+$caughtMessage)}
          }
          if(Test-Path -LiteralPath $evidence){if(@(Get-ChildItem -LiteralPath $evidence -Recurse -Filter checkpoint.json).Count-ne0){$failures.Add($mode+': core published checkpoint evidence')}}
          if(-not$expectedSuccess-and@(Get-ChildItem -LiteralPath $evidence -Directory -Filter 'operation-*').Count-ne0){$failures.Add($mode+': failed core retained an operation child')}
          if($expectedSuccess-and$succeeded){Remove-PackagedOwnedDirectory -EvidenceRoot $evidence -OperationDirectory $result.OperationDirectory}
        }
        if($failures.Count-ne0){throw($failures-join"`n")}
        'CHECKPOINT_BATCH_CASES='+$modes.Count
        $counterNames=@('total','executed','passed','failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')
        $nonPassingNames=@('failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')
        $counterCases=New-Object 'Collections.Generic.List[object]'
        foreach($name in $counterNames){
          $counterCases.Add([pscustomobject]@{Name='missing-'+$name;Kind='missing';Counter=$name})
          $counterCases.Add([pscustomobject]@{Name='type-'+$name;Kind='type';Counter=$name})
          $counterCases.Add([pscustomobject]@{Name='negative-'+$name;Kind='negative';Counter=$name})
          $counterCases.Add([pscustomobject]@{Name='overflow-'+$name;Kind='overflow';Counter=$name})
        }
        foreach($name in $nonPassingNames){$counterCases.Add([pscustomobject]@{Name='nonpassing-'+$name;Kind='nonpassing';Counter=$name})}
        $counterCases.Add([pscustomobject]@{Name='incoherent-total';Kind='incoherent';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='zero-discovery';Kind='zero';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='extra-attribute';Kind='extra';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='duplicate-summary';Kind='duplicate-summary';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='duplicate-counters';Kind='duplicate-counters';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='missing-result';Kind='missing-result';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='bad-result-id';Kind='bad-result-id';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='missing-definition';Kind='missing-definition';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='duplicate-execution';Kind='duplicate-execution';Counter='total'})
        $counterCases.Add([pscustomobject]@{Name='failed-result';Kind='failed-result';Counter='total'})
        if($counterCases.Count-ne87){throw('fixed counter rejection contract count changed: '+$counterCases.Count)}
        foreach($case in $counterCases){
          $expectedCounterFragment=switch($case.Kind){
            'missing'{'TRX Counters must contain exactly the 16 canonical attributes.'}
            'type'{'TRX counter is not a canonical nonnegative Int32: '+$case.Counter}
            'negative'{'TRX counter is not a canonical nonnegative Int32: '+$case.Counter}
            'overflow'{'TRX counter is not a canonical nonnegative Int32: '+$case.Counter}
            'nonpassing'{'TRX contains a non-passing or other outcome: '+$case.Counter}
            'incoherent'{'TRX requires total=executed=passed>0.'}
            'zero'{'TRX requires total=executed=passed>0.'}
            'extra'{'TRX Counters must contain exactly the 16 canonical attributes.'}
            'duplicate-summary'{'TRX must contain exactly one ResultSummary element.'}
            'duplicate-counters'{'TRX must contain exactly one Counters element.'}
            'missing-result'{'TRX UnitTestResult count must equal the total counter.'}
            'bad-result-id'{'TRX result contains an invalid test or execution GUID.'}
            'missing-definition'{'TRX UnitTest definition count must equal its result count.'}
            'duplicate-execution'{'TRX contains a duplicate execution identity.'}
            'failed-result'{'TRX result outcome must be exactly Passed.'}
            default{throw('counter case lacks exact rejection contract: '+$case.Kind)}
          }
          $values=[ordered]@{total='2';executed='2';passed='2';failed='0';error='0';timeout='0';aborted='0';inconclusive='0';passedButRunAborted='0';notRunnable='0';notExecuted='0';disconnected='0';warning='0';completed='0';inProgress='0';pending='0'}
          if($case.Kind-eq'missing'){$values.Remove($case.Counter)}
          elseif($case.Kind-eq'type'){$values[$case.Counter]='1.0'}
          elseif($case.Kind-eq'negative'){$values[$case.Counter]='-1'}
          elseif($case.Kind-eq'overflow'){$values[$case.Counter]='2147483648'}
          elseif($case.Kind-eq'incoherent'){$values.total='3'}
          elseif($case.Kind-eq'zero'){$values.total='0';$values.executed='0';$values.passed='0'}
          elseif($case.Kind-eq'nonpassing'){
            $values.passed='1';$values[$case.Counter]='1'
            if($case.Counter-eq'notExecuted'){$values.executed='1'}
          }
          $attributes=@($values.GetEnumerator()|ForEach-Object{$_.Key+'="'+$_.Value+'"'})-join' '
          if($case.Kind-eq'extra'){$attributes+=' unexpected="0"'}
          $trx=Join-Path $BatchRoot ('counter-'+$case.Name+'.trx')
          $summary='<ResultSummary><Counters '+$attributes+' /></ResultSummary>'
          if($case.Kind-eq'duplicate-summary'){$summary+=$summary}
          elseif($case.Kind-eq'duplicate-counters'){$summary='<ResultSummary><Counters '+$attributes+' /><Counters '+$attributes+' /></ResultSummary>'}
          $resultXml='<Results><UnitTestResult executionId="44444444-4444-4444-4444-444444444444" testId="22222222-2222-2222-2222-222222222222" testName="Fixture.One" outcome="Passed" /><UnitTestResult executionId="55555555-5555-5555-5555-555555555555" testId="33333333-3333-3333-3333-333333333333" testName="Fixture.Two" outcome="Passed" /></Results>'
          $definitionXml='<TestDefinitions><UnitTest id="22222222-2222-2222-2222-222222222222" name="Fixture.One" storage="GraniteEdgeAI.UnitTests.dll"><Execution id="44444444-4444-4444-4444-444444444444" /><TestMethod codeBase="GraniteEdgeAI.UnitTests.dll" className="Fixture" name="Fixture.One" /></UnitTest><UnitTest id="33333333-3333-3333-3333-333333333333" name="Fixture.Two" storage="GraniteEdgeAI.UnitTests.dll"><Execution id="55555555-5555-5555-5555-555555555555" /><TestMethod codeBase="GraniteEdgeAI.UnitTests.dll" className="Fixture" name="Fixture.Two" /></UnitTest></TestDefinitions>'
          if($case.Kind-eq'missing-result'){$resultXml='<Results><UnitTestResult executionId="44444444-4444-4444-4444-444444444444" testId="22222222-2222-2222-2222-222222222222" testName="Fixture.One" outcome="Passed" /></Results>'}
          elseif($case.Kind-eq'bad-result-id'){$resultXml=$resultXml-replace'22222222-2222-2222-2222-222222222222','not-a-guid'}
          elseif($case.Kind-eq'missing-definition'){$definitionXml='<TestDefinitions><UnitTest id="22222222-2222-2222-2222-222222222222" name="Fixture.One" storage="GraniteEdgeAI.UnitTests.dll"><Execution id="44444444-4444-4444-4444-444444444444" /><TestMethod codeBase="GraniteEdgeAI.UnitTests.dll" className="Fixture" name="Fixture.One" /></UnitTest></TestDefinitions>'}
          elseif($case.Kind-eq'duplicate-execution'){$resultXml=$resultXml-replace'55555555-5555-5555-5555-555555555555','44444444-4444-4444-4444-444444444444';$definitionXml=$definitionXml-replace'<Execution id="55555555-5555-5555-5555-555555555555"','<Execution id="44444444-4444-4444-4444-444444444444"'}
          elseif($case.Kind-eq'failed-result'){$resultXml=$resultXml-replace'outcome="Passed"','outcome="Failed"'}
          [IO.File]::WriteAllText($trx,('<?xml version="1.0"?><TestRun id="11111111-1111-1111-1111-111111111111" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'+$resultXml+$definitionXml+$summary+'</TestRun>'),(New-Object Text.UTF8Encoding($false)))
          $counterMessage=$null
          try{& $checkpointModule { param($Path) Get-StrictTrxCounters -TrxPath $Path -RunStartUtc ([DateTime]::UtcNow.AddMinutes(-1)) -RunEndUtc ([DateTime]::UtcNow.AddMinutes(1)) } $trx|Out-Null}catch{$counterMessage=$_.Exception.Message}
          if($null-eq$counterMessage){throw('counter mutation unexpectedly passed: '+$case.Name)}
          if(-not[StringComparer]::Ordinal.Equals($counterMessage,[string]$expectedCounterFragment)){throw('counter mutation wrong rejection: '+$case.Name+': '+$counterMessage)}
        }
        'CHECKPOINT_COUNTER_CASES='+$counterCases.Count
        """;
}
