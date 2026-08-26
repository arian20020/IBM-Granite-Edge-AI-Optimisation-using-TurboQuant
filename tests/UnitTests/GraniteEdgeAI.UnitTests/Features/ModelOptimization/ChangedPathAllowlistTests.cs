using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class ChangedPathAllowlistTests
{
    [TestMethod]
    public void WrapperStaticallyRequiresCanonicalNonReparseOutputOwnership()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "verification", "Assert-ChangedPaths.ps1"));
        StringAssert.Contains(script, "[CmdletBinding(PositionalBinding = $false)]");
        StringAssert.Contains(script, "ReparsePoint");
        StringAssert.Contains(script, "CREATE_NEW");
        StringAssert.Contains(script, "WritePathspec must be outside the repository worktree.");
        StringAssert.Contains(script, "OrdinalIgnoreCase");
        StringAssert.Contains(script, "NormalizationForm.FormC");
        StringAssert.Contains(script, "core.fsmonitor=false");
        StringAssert.Contains(script, "ConvertFrom-NulGitRecords -Bytes $result.StdoutBytes");
        Assert.IsFalse(script.Contains("AllowedValue", StringComparison.Ordinal),
            "Every configured repository-local safety key must be rejected, including disabling-looking values.");
        Assert.IsFalse(script.Contains("$configuredText", StringComparison.Ordinal),
            "Local configuration presence must be decided from exact NUL-delimited bytes.");
        StringAssert.Contains(script, "core.hooksPath=NUL");
        StringAssert.Contains(script, "core.untrackedCache");
        StringAssert.Contains(script, "[switch]$StageVerified");
        StringAssert.Contains(script, "GIT_INDEX_FILE");
        StringAssert.Contains(script, "hash-object', '-w', '--no-filters'");
        StringAssert.Contains(script, "update-index', '--add', '--cacheinfo'");
        StringAssert.Contains(script, "Publish-VerifiedTemporaryIndex");
        StringAssert.Contains(script, "Repository-local executable Git configuration is forbidden.");
        StringAssert.Contains(script, "Index contains assume-unchanged path:");
        StringAssert.Contains(script, "Index contains skip-worktree path:");
        StringAssert.Contains(script, "Index contains fsmonitor-valid path:");
        StringAssert.Contains(script, "$tag -cnotmatch '^[A-Za-z]$'");
        StringAssert.Contains(script, "ls-files', '--stage', '-z'");
        StringAssert.Contains(script, "ls-files', '-f', '-z'");
        StringAssert.Contains(script, "'config', '--local', '--null', '--get-all'");
        StringAssert.Contains(script, "$result.StdoutBytes.Length -eq 0");
        int fsmonitorInspection = script.IndexOf("$fsmonitorFlags = Invoke-ChangedPathGit", StringComparison.Ordinal);
        int fsmonitorDecode = script.IndexOf(
            "$fsmonitorRecords = @(ConvertFrom-NulGitRecords -Bytes $fsmonitorFlags.StdoutBytes)",
            StringComparison.Ordinal);
        int fsmonitorValidation = script.IndexOf(
            "Assert-NoFsmonitorValidRecords -Record $fsmonitorRecords",
            StringComparison.Ordinal);
        int stagedIndexInspection = script.IndexOf("$entries = Invoke-ChangedPathGit", StringComparison.Ordinal);
        Assert.AreEqual(1, Regex.Matches(script,
            Regex.Escape("$fsmonitorRecords = @(ConvertFrom-NulGitRecords -Bytes $fsmonitorFlags.StdoutBytes)")).Count,
            "The exact first-read fsmonitor bytes must be decoded once.");
        Assert.AreEqual(1, Regex.Matches(script,
            Regex.Escape("Assert-NoFsmonitorValidRecords -Record $fsmonitorRecords")).Count,
            "The exact decoded fsmonitor records must be validated once.");
        Assert.IsTrue(
            fsmonitorInspection >= 0 &&
            fsmonitorInspection < fsmonitorDecode &&
            fsmonitorDecode < fsmonitorValidation &&
            fsmonitorValidation < stagedIndexInspection,
            "Fsmonitor-valid inspection must flow from the first raw query through NUL decoding into validation before any other index read.");
        Assert.IsFalse(script.Contains("'write-tree'", StringComparison.Ordinal),
            "Index identity must use a read-only representation rather than git write-tree.");
        foreach (string marker in new[]
        {
            "CreateFileW", "GetFinalPathNameByHandleW", "FILE_FLAG_OPEN_REPARSE_POINT",
            "CREATE_NEW", "FlushFileBuffers"
        })
            Assert.IsTrue(script.Contains(marker, StringComparison.Ordinal),
                $"Pathspec publication lacks handle-bound marker: {marker}");
        Assert.IsFalse(script.Contains("[IO.File]::Move", StringComparison.Ordinal),
            "Path-based temporary-file publication is vulnerable to parent rebinding.");

        string plan = File.ReadAllText(Path.Combine(root, "docs", "superpowers", "plans",
            "2026-08-26-cross-route-optimisation-integration.md"));
        MatchCollection consumers = System.Text.RegularExpressions.Regex.Matches(plan,
            @"(?m)^git .*--pathspec-from-file=.*--pathspec-file-nul\s*$");
        Assert.AreEqual(0, consumers.Count, "A closed pathspec must never be non-import staging authority.");
        MatchCollection verifiedStaging = Regex.Matches(plan,
            @"(?m)^powershell .*Assert-ChangedPaths\.ps1 .* -StageVerified\s*$");
        Assert.AreEqual(20, verifiedStaging.Count, "The fixed non-import verified-staging inventory drifted.");
        MatchCollection commits = Regex.Matches(plan, @"(?m)^git .*\bcommit -m ");
        Assert.AreEqual(23, commits.Count, "The fixed plan commit inventory drifted.");
        foreach (Match commit in commits)
            StringAssert.Contains(commit.Value,
                "git -c core.fsmonitor=false -c core.hooksPath=NUL -c commit.gpgSign=false commit -m ");
        StringAssert.Contains(plan,
            "FullyQualifiedName~OptimizationImportManifestVerifierProcessTests");

        string[] expectedTaskOnePaths =
        [
            "docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json",
            "docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md",
            "docs/superpowers/plans/2026-08-26-cross-route-optimisation-integration.md",
            "scripts/verification/Test-CrossRouteImportManifest.ps1",
            "scripts/verification/CrossRouteImportManifest.Core.psm1",
            "scripts/verification/Assert-ChangedPaths.ps1",
            "scripts/verification/Invoke-PackagedTestCheckpoint.ps1",
            "scripts/verification/PackagedCheckpoint.Core.psm1",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/OptimizationImportManifestTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/ChangedPathAllowlistTests.cs",
            "tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelOptimization/PackagedTestCheckpointTests.cs"
        ];
        Match taskOne = Regex.Match(plan,
            @"(?s)### Task 1:.*?\*\*Files:\*\*(?<files>.*?)- \[ \] \*\*Step 1:.*?Assert-ChangedPaths\.ps1 .*?-AllowedPath @\((?<allowlist>.*?)\) -StageVerified.*?implementation commit changes exactly eleven paths\.");
        Assert.IsTrue(taskOne.Success, "Task 1 exact eleven-path specification was not found.");
        string[] listedPaths = Regex.Matches(taskOne.Groups["files"].Value, @"`(?<path>[^`]+)`")
            .Select(match => match.Groups["path"].Value).ToArray();
        string[] allowedPaths = Regex.Matches(taskOne.Groups["allowlist"].Value, @"'(?<path>[^']+)'")
            .Select(match => match.Groups["path"].Value).ToArray();
        CollectionAssert.AreEqual(expectedTaskOnePaths, listedPaths, "Task 1 file list is not the exact ruled scope.");
        CollectionAssert.AreEqual(expectedTaskOnePaths, allowedPaths, "Task 1 allowlist is not the exact ruled scope.");
    }

    [TestMethod]
    public void TemporaryRepositoryCleanupIsExactBoundedAndReparseSafe()
    {
        string source = File.ReadAllText(Path.Combine(
            OptimizationImportManifestTests.FindRepositoryRoot(), "tests", "UnitTests", "GraniteEdgeAI.UnitTests",
            "Features", "ModelOptimization", "ChangedPathAllowlistTests.cs"));
        string[] requiredMarkers =
        [
            "DeleteExact" + "OwnedTempRoot",
            "ValidateExact" + "OwnedTempRoot",
            "Guid." + "TryParseExact",
            "FileAttributes." + "ReparsePoint",
            "Directory.Delete(path, " + "false)",
            "FileAttributes." + "ReadOnly",
            "Stopwatch." + "StartNew()",
            "FileShare." + "None",
            ".geai-operation-owner",
            "AggregateException(" + "\"Owned temporary cleanup failed.\""
        ];
        foreach (string marker in requiredMarkers)
            Assert.IsTrue(source.Contains(marker, StringComparison.Ordinal), $"Missing safe-cleanup marker: {marker}");
        Assert.IsFalse(source.Contains("Directory.Delete(" + "Root, true)", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("catch" + " { }", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AllChangedPathCasesRunInOneBatchDriver()
    {
        ExactOwnedTempRootIdentity batchOwnership = CreateExactOwnedTempRoot("geai-changed-path-batch-");
        string batchRoot = batchOwnership.Root;
        List<TemporaryGitRepository> repositories = [];
        string? junction = null;
        try
        {
            TemporaryGitRepository allowed = AddRepository();
            File.AppendAllText(Path.Combine(allowed.Root, "allowed.txt"), "changed");
            TemporaryGitRepository untracked = AddRepository();
            File.WriteAllText(Path.Combine(untracked.Root, "unrelated.txt"), "no");
            TemporaryGitRepository outside = AddRepository();
            File.AppendAllText(Path.Combine(outside.Root, "outside.txt"), "changed");
            TemporaryGitRepository clean = AddRepository();
            TemporaryGitRepository literal = AddRepository();
            literal.Write("[literal].txt", "literal pathspec");
            TemporaryGitRepository renameAllowed = AddRepository();
            renameAllowed.Rename("allowed.txt", "renamed.txt");
            TemporaryGitRepository renameRejected = AddRepository();
            renameRejected.Rename("allowed.txt", "renamed.txt");
            TemporaryGitRepository copyAllowed = AddRepository();
            copyAllowed.CopyAndStage("outside.txt", "allowed-copy.txt");
            TemporaryGitRepository copyRejected = AddRepository();
            copyRejected.CopyAndStage("outside.txt", "allowed-copy.txt");
            TemporaryGitRepository fsmonitor = AddRepository();
            fsmonitor.Configure("core.fsmonitor", "false");
            TemporaryGitRepository hooks = AddRepository();
            hooks.Configure("core.hooksPath", "NUL");
            TemporaryGitRepository untrackedCache = AddRepository();
            untrackedCache.Configure("core.untrackedCache", "false");
            TemporaryGitRepository emptyFsmonitor = AddRepository();
            emptyFsmonitor.Configure("core.fsmonitor", "");
            TemporaryGitRepository emptyHooks = AddRepository();
            emptyHooks.Configure("core.hooksPath", "");
            TemporaryGitRepository emptyUntrackedCache = AddRepository();
            emptyUntrackedCache.Configure("core.untrackedCache", "");
            TemporaryGitRepository assumeUnchanged = AddRepository();
            assumeUnchanged.SetIndexFlag("--assume-unchanged", "allowed.txt");
            TemporaryGitRepository skipWorktree = AddRepository();
            skipWorktree.SetIndexFlag("--skip-worktree", "allowed.txt");
            TemporaryGitRepository fsmonitorValid = AddRepository();

            List<BatchCase> cases =
            [
                Case("allowed-bomless-nul", allowed.Root, ["allowed.txt", "unused.txt"], true,
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("allowed.txt\0"))),
                Case("literal-pathspec-name", literal.Root, ["[literal].txt"], true,
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("[literal].txt\0")),
                    verifyLiteralConsumer: true),
                Case("rename-both-endpoints", renameAllowed.Root, ["allowed.txt", "renamed.txt"], true,
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("renamed.txt\0allowed.txt\0"))),
                Case("copy-both-endpoints", copyAllowed.Root, ["outside.txt", "allowed-copy.txt"], true,
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("allowed-copy.txt\0outside.txt\0"))),
                Case("unrelated-untracked", untracked.Root, ["allowed.txt"], false, null, null,
                    "Changed path is outside the allowlist: unrelated.txt"),
                Case("modified-outside", outside.Root, ["allowed.txt"], false, null, null,
                    "Changed path is outside the allowlist: outside.txt"),
                Case("rename-missing-source-endpoint", renameRejected.Root, ["renamed.txt"], false, null, null,
                    "Changed path is outside the allowlist: allowed.txt"),
                Case("copy-missing-source-endpoint", copyRejected.Root, ["allowed-copy.txt"], false, null, null,
                    "Changed path is outside the allowlist: outside.txt"),
                Case("pathspec-inside-repository", allowed.Root, ["allowed.txt"], false, null,
                    Path.Combine(allowed.Root, "owned-pathspec.bin"),
                    "WritePathspec must be outside the repository worktree."),
                Case("alternate-data-stream", clean.Root, ["allowed.txt:payload"], false, null, null,
                    "A repository-relative non-traversing path is required."),
                Case("reserved-device", clean.Root, ["CON.txt"], false, null, null,
                    "A path segment cannot use a reserved device name."),
                Case("trailing-dot", clean.Root, ["allowed."], false, null, null,
                    "A path segment cannot have a trailing dot or space."),
                Case("trailing-space", clean.Root, ["allowed "], false, null, null,
                    "A path segment cannot have a trailing dot or space."),
                Case("backslash", clean.Root, ["folder\\allowed.txt"], false, null, null,
                    "A canonical repository-relative path without backslashes is required."),
                Case("case-collision", clean.Root, ["owned/Path.txt", "owned/path.txt"], false, null, null,
                    "Allowlist contains an OrdinalIgnoreCase or Unicode normalization collision: owned/path.txt"),
                Case("unicode-alias", clean.Root, ["owned/caf\u00e9.txt", "owned/cafe\u0301.txt"], false, null, null,
                    "A path must use Unicode NormalizationForm.FormC."),
                Case("repository-local-fsmonitor", fsmonitor.Root, ["allowed.txt"], false, null, null,
                    "Repository-local core.fsmonitor is forbidden."),
                Case("repository-local-hooks", hooks.Root, ["allowed.txt"], false, null, null,
                    "Repository-local core.hooksPath is forbidden."),
                Case("repository-local-untracked-cache", untrackedCache.Root, ["allowed.txt"], false, null, null,
                    "Repository-local core.untrackedCache is forbidden."),
                Case("repository-local-empty-fsmonitor", emptyFsmonitor.Root, ["allowed.txt"], false, null, null,
                    "Repository-local core.fsmonitor is forbidden."),
                Case("repository-local-empty-hooks", emptyHooks.Root, ["allowed.txt"], false, null, null,
                    "Repository-local core.hooksPath is forbidden."),
                Case("repository-local-empty-untracked-cache", emptyUntrackedCache.Root, ["allowed.txt"], false, null, null,
                    "Repository-local core.untrackedCache is forbidden."),
                Case("assume-unchanged-index", assumeUnchanged.Root, ["allowed.txt"], false, null, null,
                    "Index contains assume-unchanged path: allowed.txt"),
                Case("skip-worktree-index", skipWorktree.Root, ["allowed.txt"], false, null, null,
                    "Index contains skip-worktree path: allowed.txt"),
                Case("fsmonitor-valid-index", fsmonitorValid.Root, ["allowed.txt"], false, null, null,
                    "Index contains fsmonitor-valid path: allowed.txt",
                    fsmonitorValidationRecords: ["h allowed.txt", "H outside.txt"])
            ];

            string sentinelPath = Path.Combine(batchRoot, "sentinel.bin");
            byte[] sentinel = Encoding.UTF8.GetBytes("pre-existing sentinel must survive");
            File.WriteAllBytes(sentinelPath, sentinel);
            TemporaryGitRepository sentinelRepository = AddRepository();
            File.AppendAllText(Path.Combine(sentinelRepository.Root, "allowed.txt"), "changed");
            cases.Add(new("pre-existing-sentinel-preserved", sentinelPath, sentinelRepository.Root, ["allowed.txt"], false,
                null, Convert.ToBase64String(sentinel),
                "WritePathspec target must be absent; a pre-existing sentinel is never overwritten."));

            string junctionTarget = Path.Combine(batchRoot, "junction-target");
            junction = Path.Combine(batchRoot, "junction-parent");
            Directory.CreateDirectory(junctionTarget);
            string trustedCommand = ResolveTrustedExecutable(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), "System32 cmd");
            var junctionResult = Task1ProcessRunner.Run(trustedCommand, batchRoot,
                ["/d", "/c", "mklink", "/J", junction, junctionTarget], 30);
            Assert.AreEqual(0, junctionResult.ExitCode,
                $"The mandatory real-junction fixture could not be created: {junctionResult.Output}");
            cases.Add(Case("real-junction-output-ancestry", clean.Root, ["allowed.txt"], false, null,
                Path.Combine(junction, "paths.bin"), $"ReparsePoint ancestry is forbidden: {junction}"));
            Assert.AreEqual(27, cases.Count, "Changed-path case cardinality must remain fixed and non-vacuous.");

            string casePath = Path.Combine(batchRoot, "cases.json");
            string driverPath = Path.Combine(batchRoot, "driver.ps1");
            File.WriteAllText(casePath, JsonSerializer.Serialize(cases));
            File.WriteAllText(driverPath, BatchDriver);
            string scriptPath = Path.Combine(OptimizationImportManifestTests.FindRepositoryRoot(),
                "scripts", "verification", "Assert-ChangedPaths.ps1");
            var result = OptimizationImportManifestTests.RunPowerShell(batchRoot, 300,
                "-File", driverPath, "-ScriptPath", scriptPath, "-CasePath", casePath);
            Assert.AreEqual(0, result.ExitCode, result.Output);
            StringAssert.Contains(result.Output, "CHANGED_PATH_BATCH_CASES=27");

            TemporaryGitRepository AddRepository()
            {
                TemporaryGitRepository repository = new();
                repositories.Add(repository);
                return repository;
            }

            BatchCase Case(string name, string repository, string[] allowedPaths, bool expectedSuccess,
                string? expectedPathspecBase64 = null, string? pathspec = null, string? expectedError = null,
                bool verifyLiteralConsumer = false, string[]? fsmonitorValidationRecords = null) =>
                new(name, pathspec ?? Path.Combine(batchRoot, $"{name}.bin"), repository, allowedPaths,
                    expectedSuccess, expectedPathspecBase64, null, expectedError, verifyLiteralConsumer,
                    fsmonitorValidationRecords);
        }
        finally
        {
            List<Exception> cleanupFailures = [];
            foreach (TemporaryGitRepository repository in repositories)
            {
                try
                {
                    repository.Dispose();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add(new InvalidOperationException(
                        $"Owned repository cleanup failed: {repository.Root}", exception));
                }
            }
            try
            {
                DeleteExactOwnedTempRoot(batchOwnership, TimeSpan.FromSeconds(5));
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(new InvalidOperationException(
                    $"Owned batch cleanup failed: {batchRoot}", exception));
            }
            if (cleanupFailures.Count != 0)
                throw new AggregateException("Owned temporary cleanup failed.", cleanupFailures);
        }
    }

    private sealed record BatchCase(
        string Name,
        string Pathspec,
        string Repository,
        string[] AllowedPaths,
        bool ExpectedSuccess,
        string? ExpectedPathspecBase64,
        string? ExpectedSentinelBase64,
        string? ExpectedError,
        bool VerifyLiteralConsumer = false,
        string[]? FsmonitorValidationRecords = null);

    private const string BatchDriver = """
        param([string]$ScriptPath,[string]$CasePath)
        $ErrorActionPreference='Stop'
        $modulePath=Join-Path -Path (Split-Path -Parent $ScriptPath) -ChildPath 'CrossRouteImportManifest.Core.psm1'
        $verificationModule=Import-Module -Force -Name $modulePath -PassThru
        $safeGitArguments=@('-c','core.fsmonitor=false','-c','core.hooksPath=NUL','-c','core.untrackedCache=false','-c','core.preloadIndex=false','--literal-pathspecs')
        $cases=Get-Content -Raw -LiteralPath $CasePath|ConvertFrom-Json
        if(@($cases).Count-ne27){throw 'Changed-path batch cardinality drifted from 27.'}
        $failures=New-Object 'Collections.Generic.List[string]'
        $scriptTokens=$null
        $scriptErrors=$null
        $scriptAst=[Management.Automation.Language.Parser]::ParseFile($ScriptPath,[ref]$scriptTokens,[ref]$scriptErrors)
        if($scriptErrors.Count-ne0){throw 'Changed-path production script did not parse.'}
        foreach($functionName in @('Normalize-GitPath','Assert-NoFsmonitorValidRecords')){
          $definitions=@($scriptAst.FindAll({param($node)$node-is[Management.Automation.Language.FunctionDefinitionAst]-and$node.Name-ceq$functionName},$true))
          if($definitions.Count-ne1){throw('Expected exactly one production function: '+$functionName)}
          . ([scriptblock]::Create($definitions[0].Extent.Text))
        }
        function Invoke-DriverGit {
          [CmdletBinding()]
          param([Parameter(Mandatory)][string]$Repository,[Parameter(Mandatory)][string[]]$Arguments)
          Invoke-CrossRouteGit -Repository $Repository -Arguments ($safeGitArguments+$Arguments)
        }
        foreach($case in @($cases)){
          try{
            $headBefore=(Invoke-DriverGit -Repository $case.Repository -Arguments @('rev-parse','--verify','--end-of-options','HEAD')).Stdout.Trim()
            $treeBefore=(Invoke-DriverGit -Repository $case.Repository -Arguments @('rev-parse','--verify','--end-of-options','HEAD^{tree}')).Stdout.Trim()
            $indexBefore=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('ls-files','--stage','-z')).StdoutBytes)
            $flagsBefore=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('ls-files','-v','-z')).StdoutBytes)
            $fsmonitorFlagsBefore=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('ls-files','-f','-z')).StdoutBytes)
            $statusBefore=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('status','--porcelain=v1','-z','--untracked-files=all')).StdoutBytes)
            $succeeded=$false
            $rejectionMessage=$null
            try{
              if($null-ne$case.FsmonitorValidationRecords){
                Assert-NoFsmonitorValidRecords -Record @('H allowed.txt','H outside.txt')
                $malformedMessage=$null
                try{Assert-NoFsmonitorValidRecords -Record @('malformed')}catch{$malformedMessage=$_.Exception.Message}
                if(-not[StringComparer]::Ordinal.Equals($malformedMessage,'Git fsmonitor-valid output was malformed.')){throw 'Malformed fsmonitor record did not receive its exact rejection.'}
                foreach($malformedRecord in @('1 allowed.txt','! allowed.txt')){
                  $malformedTagMessage=$null
                  try{Assert-NoFsmonitorValidRecords -Record @($malformedRecord)}catch{$malformedTagMessage=$_.Exception.Message}
                  if(-not[StringComparer]::Ordinal.Equals($malformedTagMessage,'Git fsmonitor-valid output was malformed.')){throw('Non-alphabetic fsmonitor tag did not receive its exact rejection: '+$malformedRecord)}
                }
                Assert-NoFsmonitorValidRecords -Record @($case.FsmonitorValidationRecords)
              }else{
                & $ScriptPath -Repository $case.Repository -AllowedPath @($case.AllowedPaths) -WritePathspec $case.Pathspec|Out-Null
                $succeeded=$true
              }
            }catch{
              if($case.ExpectedSuccess){throw}
              $rejectionMessage=$_.Exception.Message
            }
            if($succeeded-ne[bool]$case.ExpectedSuccess){throw 'success state differed from expectation'}
            if((-not[bool]$case.ExpectedSuccess)-and(-not[StringComparer]::Ordinal.Equals($rejectionMessage,[string]$case.ExpectedError))){
              throw('expected exact error "'+$case.ExpectedError+'" but received "'+$rejectionMessage+'"')
            }
            if($null-ne$case.ExpectedPathspecBase64){
              $actual=[Convert]::ToBase64String([IO.File]::ReadAllBytes($case.Pathspec))
              if($actual-cne[string]$case.ExpectedPathspecBase64){throw 'pathspec bytes differed'}
              if([bool]$case.VerifyLiteralConsumer){
                $dryRun=Invoke-DriverGit -Repository $case.Repository -Arguments @('add','--dry-run',('--pathspec-from-file='+$case.Pathspec),'--pathspec-file-nul')
                if($dryRun.ExitCode-ne0){throw 'literal pathspec consumer dry-run failed'}
              }
            }
            if($null-ne$case.ExpectedSentinelBase64){
              $actual=[Convert]::ToBase64String([IO.File]::ReadAllBytes($case.Pathspec))
              if($actual-cne[string]$case.ExpectedSentinelBase64){throw 'pre-existing pathspec sentinel changed'}
            }
            if((-not[bool]$case.ExpectedSuccess)-and($null-eq$case.ExpectedSentinelBase64)-and(Test-Path -LiteralPath $case.Pathspec)){throw 'rejected case wrote a pathspec'}
            $headAfter=(Invoke-DriverGit -Repository $case.Repository -Arguments @('rev-parse','--verify','--end-of-options','HEAD')).Stdout.Trim()
            $treeAfter=(Invoke-DriverGit -Repository $case.Repository -Arguments @('rev-parse','--verify','--end-of-options','HEAD^{tree}')).Stdout.Trim()
            $indexAfter=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('ls-files','--stage','-z')).StdoutBytes)
            $flagsAfter=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('ls-files','-v','-z')).StdoutBytes)
            $fsmonitorFlagsAfter=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('ls-files','-f','-z')).StdoutBytes)
            $statusAfter=[Convert]::ToBase64String((Invoke-DriverGit -Repository $case.Repository -Arguments @('status','--porcelain=v1','-z','--untracked-files=all')).StdoutBytes)
            if($headAfter-cne$headBefore-or$treeAfter-cne$treeBefore-or$indexAfter-cne$indexBefore-or$flagsAfter-cne$flagsBefore-or$fsmonitorFlagsAfter-cne$fsmonitorFlagsBefore-or$statusAfter-cne$statusBefore){throw 'repository HEAD/tree/index/status mutated'}
          }catch{$failures.Add($case.Name+': '+$_.Exception.Message)}
        }
        if($failures.Count-ne0){throw($failures-join"`n")}
        'CHANGED_PATH_BATCH_CASES=27'
        """;

    private sealed record ExactOwnedTempRootIdentity(
        string Root,
        string OwnerMarkerPath,
        FileStream OwnerStream);

    private static ExactOwnedTempRootIdentity CreateExactOwnedTempRoot(string prefix)
    {
        string root = Path.Combine(Path.GetTempPath(), $"{prefix}{Guid.NewGuid():N}");
        string fullRoot = ValidateExactOwnedTempRoot(root);
        if (Directory.Exists(fullRoot) || File.Exists(fullRoot))
            throw new InvalidOperationException("Fresh owned temporary root unexpectedly exists.");
        Directory.CreateDirectory(fullRoot);
        if ((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Owned temporary root became a reparse point during creation.");

        string marker = Path.Combine(fullRoot, ".geai-operation-owner");
        FileStream stream = new(marker, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096,
            FileOptions.WriteThrough);
        stream.WriteByte(1);
        stream.Flush(true);
        return new(fullRoot, marker, stream);
    }

    private static void DeleteExactOwnedTempRoot(ExactOwnedTempRootIdentity ownership, TimeSpan timeout)
    {
        string fullRoot = ValidateExactOwnedTempRoot(ownership.Root);
        string marker = Path.GetFullPath(ownership.OwnerMarkerPath);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(marker), fullRoot) ||
            !StringComparer.Ordinal.Equals(Path.GetFileName(marker), ".geai-operation-owner") ||
            ownership.OwnerStream.SafeFileHandle.IsClosed)
            throw new InvalidOperationException("Owned temporary cleanup lacks its exact live owner marker.");
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastFailure = null;
        bool ownerReleased = false;
        try
        {
            while (true)
            {
                try
                {
                    if ((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0 ||
                        (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Owned temporary identity was replaced by a reparse point.");
                    foreach (string entry in Directory.GetFileSystemEntries(fullRoot))
                    {
                        if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(entry), marker))
                            continue;
                        string quarantined = MoveOwnedEntryForDeletion(entry, fullRoot);
                        DeleteOwnedEntry(quarantined, stopwatch, timeout);
                    }
                    lastFailure = null;
                }
                catch (IOException exception)
                {
                    lastFailure = exception;
                }
                catch (UnauthorizedAccessException exception)
                {
                    lastFailure = exception;
                }

                string[] remaining = Directory.GetFileSystemEntries(fullRoot)
                    .Where(entry => !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(entry), marker))
                    .ToArray();
                if (remaining.Length == 0)
                    break;
                if (stopwatch.Elapsed >= timeout)
                    throw new IOException($"Timed out deleting exact owned temporary root: {fullRoot}", lastFailure);
                Thread.Sleep(25);
            }

            if (ownership.OwnerStream.Length != 1)
                throw new InvalidOperationException("Owned temporary marker content changed during cleanup.");
            ownership.OwnerStream.Dispose();
            ownerReleased = true;
            if ((File.GetAttributes(marker) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidOperationException("Owned temporary marker identity changed before deletion.");
            File.Delete(marker);
            Directory.Delete(fullRoot, false);
        }
        finally
        {
            if (!ownerReleased)
                ownership.OwnerStream.Dispose();
        }
    }

    private static string MoveOwnedEntryForDeletion(string path, string ownedParent)
    {
        string fullPath = Path.GetFullPath(path);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(fullPath), Path.GetFullPath(ownedParent)))
            throw new InvalidOperationException("Cleanup entry escaped its exact owned parent.");
        string quarantine = Path.Combine(ownedParent, $".geai-delete-{Guid.NewGuid():N}");
        FileAttributes attributes = File.GetAttributes(fullPath);
        if ((attributes & FileAttributes.Directory) != 0)
            Directory.Move(fullPath, quarantine);
        else
            File.Move(fullPath, quarantine);
        return quarantine;
    }

    private static string ResolveTrustedExecutable(string path, string label)
    {
        string fullPath = Path.GetFullPath(path);
        for (string? cursor = fullPath; !string.IsNullOrWhiteSpace(cursor); cursor = Path.GetDirectoryName(cursor))
        {
            FileAttributes attributes = File.GetAttributes(cursor);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"{label} has reparse-point ancestry: {cursor}");
            string? parent = Path.GetDirectoryName(cursor);
            if (string.Equals(parent, cursor, StringComparison.OrdinalIgnoreCase))
                break;
        }
        FileInfo item = new(fullPath);
        if (!item.Exists || (item.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new InvalidOperationException($"{label} must be an exact regular non-reparse file.");
        return fullPath;
    }

    private static string ResolveTrustedGit() => ResolveTrustedExecutable(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
        "Program Files Git");

    private static string ValidateExactOwnedTempRoot(string root)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        string? parent = Path.GetDirectoryName(fullRoot);
        if (parent is null || !StringComparer.OrdinalIgnoreCase.Equals(
                Path.TrimEndingDirectorySeparator(parent), tempRoot))
            throw new InvalidOperationException("Cleanup root must be an exact child of the temporary directory.");

        string name = Path.GetFileName(fullRoot);
        string? prefix = new[] { "geai-allowlist-", "geai-changed-path-batch-" }
            .SingleOrDefault(candidate => name.StartsWith(candidate, StringComparison.Ordinal));
        if (prefix is null || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Cleanup root must have an exact operation-owned GUID name.");
        return fullRoot;
    }

    private static void DeleteOwnedEntry(
        string path,
        System.Diagnostics.Stopwatch stopwatch,
        TimeSpan timeout)
    {
        if (stopwatch.Elapsed >= timeout)
            throw new TimeoutException("Owned temporary cleanup exceeded its bounded duration.");

        FileAttributes attributes = File.GetAttributes(path);
        bool isDirectory = (attributes & FileAttributes.Directory) != 0;
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            if (isDirectory)
                Directory.Delete(path, false);
            else
                File.Delete(path);
            return;
        }

        if (isDirectory)
        {
            foreach (string entry in Directory.GetFileSystemEntries(path))
            {
                string quarantined = MoveOwnedEntryForDeletion(entry, path);
                DeleteOwnedEntry(quarantined, stopwatch, timeout);
            }
            Directory.Delete(path, false);
            return;
        }

        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        File.Delete(path);
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        private readonly ExactOwnedTempRootIdentity _ownership;
        public string Root => _ownership.Root;

        public TemporaryGitRepository()
        {
            _ownership = CreateExactOwnedTempRoot("geai-allowlist-");
            try
            {
                Run("init", "--quiet");
                File.AppendAllText(Path.Combine(Root, ".git", "info", "exclude"), ".geai-operation-owner\n");
                Run("config", "user.email", "tests@example.invalid");
                Run("config", "user.name", "Tests");
                Run("config", "core.autocrlf", "false");
                File.WriteAllText(Path.Combine(Root, "allowed.txt"), "base");
                File.WriteAllText(Path.Combine(Root, "outside.txt"), "base");
                Run("add", "allowed.txt", "outside.txt");
                Run("commit", "--quiet", "-m", "base");
            }
            catch
            {
                DeleteExactOwnedTempRoot(_ownership, TimeSpan.FromSeconds(5));
                throw;
            }
        }

        private void Run(params string[] arguments)
        {
            var result = Task1ProcessRunner.Run(ResolveTrustedGit(), Root, arguments);
            Assert.AreEqual(0, result.ExitCode, result.Output);
        }

        public void Write(string relativePath, string content) =>
            File.WriteAllText(Path.Combine(Root, relativePath), content);

        public void Rename(string source, string destination) =>
            Run("mv", source, destination);

        public void CopyAndStage(string source, string destination)
        {
            Run("config", "status.renames", "copies");
            File.Copy(Path.Combine(Root, source), Path.Combine(Root, destination));
            File.AppendAllText(Path.Combine(Root, source), " changed after copy detection source");
            Run("add", source, destination);
        }

        public void Configure(string name, string value) =>
            Run("config", name, value);

        public void SetIndexFlag(string flag, string path) =>
            Run("update-index", flag, "--", path);

        public void Dispose() => DeleteExactOwnedTempRoot(_ownership, TimeSpan.FromSeconds(5));
    }
}
