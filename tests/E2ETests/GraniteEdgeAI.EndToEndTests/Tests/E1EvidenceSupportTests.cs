using System.Diagnostics;
using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class E1EvidenceSupportTests
{
    private const string CandidateCommit = "b5d2cd34c57368efb9b122cddf16c2ffa2d3895e";
    private const string CandidateTree = "a3e4d82095caa9688f30d2463fa5971c788fd33f";
    private const string PreflightClass = "GraniteEdgeAI.EndToEndTests.Tests.R4TwoPhaseIssueEvidenceVerifierTests";
    private const string PreflightMethod = "Exact_schema_v4_candidate_closure_and_catalog_are_committed_and_pushed";
    private const string PostClass = "GraniteEdgeAI.EndToEndTests.Tests.R4TwoPhaseIssueEvidenceVerifierTests";
    private const string PostMethod = "Exact_E1_post_acceptance_evidence_is_candidate_bound_and_fail_closed";

    [TestMethod]
    public void Fake_vstest_fake_assembly_and_reparse_inputs_are_rejected()
    {
        using TestDirectory directory = TestDirectory.Create();
        string fakeVstest = Path.Combine(directory.Path, "vstest.cmd");
        string fakeAssembly = Path.Combine(directory.Path, "fake.dll");
        File.WriteAllText(fakeVstest, "@exit /b 0");
        File.WriteAllBytes(fakeAssembly, [1, 2, 3]);
        string target = Path.Combine(directory.Path, "target");
        string junction = Path.Combine(directory.Path, "junction");
        Directory.CreateDirectory(target);
        File.Copy(fakeAssembly, Path.Combine(target, "fake.dll"));
        CreateJunction(junction, target);

        string command = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module '{{SupportModule}}' -Force
            $rejected = 0
            foreach ($case in @(
                { Assert-E1TrustedVSTestPath -Path '{{Ps(fakeVstest)}}' -InstallationRoot '{{Ps(directory.Path)}}' },
                { Assert-E1EvidenceAssembly -Path '{{Ps(fakeAssembly)}}' -ExpectedOutputRoot '{{Ps(Path.Combine(directory.Path, "expected"))}}' -ImplementationCommit '{{new string('a', 40)}}' -FreshSinceUtc ([DateTime]::UtcNow.AddMinutes(-1)) },
                { Assert-E1EvidenceAssembly -Path '{{Ps(Path.Combine(junction, "fake.dll"))}}' -ExpectedOutputRoot '{{Ps(directory.Path)}}' -ImplementationCommit '{{new string('a', 40)}}' -FreshSinceUtc ([DateTime]::UtcNow.AddMinutes(-1)) }
            )) {
                try { & $case; throw 'case unexpectedly accepted' } catch { if ($_.Exception.Message -eq 'case unexpectedly accepted') { throw }; $rejected++ }
            }
            if ($rejected -ne 3) { throw 'negative path matrix was incomplete' }
            """;

        try
        {
            ProcessResult result = PowerShell(command, FindRepositoryRoot());
            Assert.AreEqual(0, result.ExitCode, result.Output);
        }
        finally
        {
            if (Directory.Exists(junction)) Directory.Delete(junction);
        }
    }

    [TestMethod]
    public void Poisoned_ProgramFiles_environment_cannot_redirect_dotnet_vswhere_or_vstest()
    {
        using TestDirectory directory = TestDirectory.Create();
        string command = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module '{{SupportModule}}' -Force
            $trustedVSTest = Get-E1TrustedVSTest
            $trustedDotNet = Get-E1TrustedDotNet
            $trustedMSBuild = Get-E1TrustedMSBuild
            $env:ProgramFiles = '{{Ps(directory.Path)}}'
            ${env:ProgramFiles(x86)} = '{{Ps(directory.Path)}}'
            $observedVSTest = Get-E1TrustedVSTest
            $observedDotNet = Get-E1TrustedDotNet
            $observedMSBuild = Get-E1TrustedMSBuild
            if ($observedVSTest -ne $trustedVSTest -or $observedDotNet -ne $trustedDotNet -or $observedMSBuild -ne $trustedMSBuild) { throw 'environment redirected trusted tool discovery' }
            if ($observedVSTest.StartsWith('{{Ps(directory.Path)}}') -or $observedDotNet.StartsWith('{{Ps(directory.Path)}}') -or $observedMSBuild.StartsWith('{{Ps(directory.Path)}}')) { throw 'poisoned root was trusted' }
            """;

        ProcessResult result = PowerShell(command, FindRepositoryRoot());
        Assert.AreEqual(0, result.ExitCode, result.Output);
    }

    [TestMethod]
    public void Trx_requires_fresh_exact_single_passing_identity()
    {
        using TestDirectory directory = TestDirectory.Create();
        DateTime started = DateTime.UtcNow.AddSeconds(-1);
        string valid = WriteTrx(directory.Path, "valid.trx", PreflightClass, PreflightMethod, "Passed", "Completed", 1, 1, 1, 0, 0);
        string stale = WriteTrx(directory.Path, "stale.trx", PreflightClass, PreflightMethod, "Passed", "Completed", 1, 1, 1, 0, 0);
        File.SetLastWriteTimeUtc(stale, started.AddMinutes(-5));
        string wrong = WriteTrx(directory.Path, "wrong.trx", PreflightClass, "Wrong_test", "Passed", "Completed", 1, 1, 1, 0, 0);
        string failed = WriteTrx(directory.Path, "failed.trx", PreflightClass, PreflightMethod, "Failed", "Failed", 1, 1, 0, 1, 0);
        string skipped = WriteTrx(directory.Path, "skipped.trx", PreflightClass, PreflightMethod, "NotExecuted", "Completed", 1, 0, 0, 0, 1);
        string missing = Path.Combine(directory.Path, "missing.trx");

        string command = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module '{{SupportModule}}' -Force
            $started = ([DateTimeOffset]'{{started:o}}').UtcDateTime
            Assert-E1AuthoritativeTrx -Path '{{Ps(valid)}}' -ExpectedResultsRoot '{{Ps(directory.Path)}}' -RepositoryRoot '{{Ps(directory.Path)}}' -ExpectedClass '{{PreflightClass}}' -ExpectedMethod '{{PreflightMethod}}' -InvocationStartedUtc $started
            $rejected = 0
            foreach ($path in @('{{Ps(stale)}}','{{Ps(wrong)}}','{{Ps(failed)}}','{{Ps(skipped)}}','{{Ps(missing)}}')) {
                try { Assert-E1AuthoritativeTrx -Path $path -ExpectedResultsRoot '{{Ps(directory.Path)}}' -RepositoryRoot '{{Ps(directory.Path)}}' -ExpectedClass '{{PreflightClass}}' -ExpectedMethod '{{PreflightMethod}}' -InvocationStartedUtc $started; throw 'case unexpectedly accepted' }
                catch { if ($_.Exception.Message -eq 'case unexpectedly accepted') { throw }; $rejected++ }
            }
            if ($rejected -ne 5) { throw 'negative TRX matrix was incomplete' }
            """;

        ProcessResult result = PowerShell(command, FindRepositoryRoot());
        Assert.AreEqual(0, result.ExitCode, result.Output);
    }

    [TestMethod]
    public void Trx_under_a_reparse_results_root_is_rejected()
    {
        using TestDirectory directory = TestDirectory.Create();
        string target = Path.Combine(directory.Path, "target");
        string junction = Path.Combine(directory.Path, "results");
        Directory.CreateDirectory(target);
        string trx = WriteTrx(target, "result.trx", PreflightClass, PreflightMethod, "Passed", "Completed", 1, 1, 1, 0, 0);
        CreateJunction(junction, target);
        try
        {
            string command = $$"""
                $ErrorActionPreference = 'Stop'
                Import-Module '{{SupportModule}}' -Force
                Assert-E1AuthoritativeTrx -Path '{{Ps(Path.Combine(junction, "result.trx"))}}' -ExpectedResultsRoot '{{Ps(junction)}}' -RepositoryRoot '{{Ps(directory.Path)}}' -ExpectedClass '{{PreflightClass}}' -ExpectedMethod '{{PreflightMethod}}' -InvocationStartedUtc ([DateTime]::UtcNow.AddMinutes(-1))
                """;
            ProcessResult result = PowerShell(command, FindRepositoryRoot());
            Assert.AreNotEqual(0, result.ExitCode, result.Output);
        }
        finally
        {
            if (Directory.Exists(junction)) Directory.Delete(junction);
        }
    }

    [TestMethod]
    public void Trx_with_a_reparse_parent_above_the_results_root_is_rejected()
    {
        using TestDirectory directory = TestDirectory.Create();
        string target = Path.Combine(directory.Path, "target");
        string junction = Path.Combine(directory.Path, "TestResults");
        string results = Path.Combine(target, "Audit", "Evidence");
        Directory.CreateDirectory(results);
        WriteTrx(results, "result.trx", PreflightClass, PreflightMethod, "Passed", "Completed", 1, 1, 1, 0, 0);
        CreateJunction(junction, target);
        try
        {
            string logicalResults = Path.Combine(junction, "Audit", "Evidence");
            string command = $$"""
                $ErrorActionPreference = 'Stop'
                Import-Module '{{SupportModule}}' -Force
                Assert-E1AuthoritativeTrx -Path '{{Ps(Path.Combine(logicalResults, "result.trx"))}}' -ExpectedResultsRoot '{{Ps(logicalResults)}}' -RepositoryRoot '{{Ps(directory.Path)}}' -ExpectedClass '{{PreflightClass}}' -ExpectedMethod '{{PreflightMethod}}' -InvocationStartedUtc ([DateTime]::UtcNow.AddMinutes(-1))
                """;
            ProcessResult result = PowerShell(command, FindRepositoryRoot());
            Assert.AreNotEqual(0, result.ExitCode, result.Output);
        }
        finally
        {
            if (Directory.Exists(junction)) Directory.Delete(junction);
        }
    }

    [TestMethod]
    public void Subject_boundary_allows_only_explicit_audit_files_after_implementation_commit()
    {
        using TestDirectory directory = TestDirectory.Create();
        Git(directory.Path, "init");
        Git(directory.Path, "config", "user.name", "E1 Test");
        Git(directory.Path, "config", "user.email", "e1@example.invalid");
        File.WriteAllText(Path.Combine(directory.Path, "source.cs"), "sealed class Source {}\n");
        Git(directory.Path, "add", "source.cs");
        Git(directory.Path, "commit", "-m", "implementation");
        string implementation = Git(directory.Path, "rev-parse", "HEAD");
        string audit = Path.Combine(directory.Path, "docs", "audits", "2026-08-30", "E1-r4-independent-acceptance-v2.md");
        Directory.CreateDirectory(Path.GetDirectoryName(audit)!);
        File.WriteAllText(audit, "audit\n");
        Git(directory.Path, "add", ".");
        Git(directory.Path, "commit", "-m", "audit");

        string allowed = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module '{{SupportModule}}' -Force
            Assert-E1ImplementationBoundary -RepositoryRoot '{{Ps(directory.Path)}}' -ImplementationCommit '{{implementation}}'
            """;
        ProcessResult allowedResult = PowerShell(allowed, directory.Path);
        Assert.AreEqual(0, allowedResult.ExitCode, allowedResult.Output);

        File.AppendAllText(Path.Combine(directory.Path, "source.cs"), "// changed\n");
        Git(directory.Path, "add", "source.cs");
        Git(directory.Path, "commit", "-m", "code after subject");
        ProcessResult rejectedResult = PowerShell(allowed, directory.Path);
        Assert.AreNotEqual(0, rejectedResult.ExitCode, rejectedResult.Output);
    }

    [TestMethod]
    public void Trusted_vstest_executes_and_parses_the_two_exact_authoritative_tests()
    {
        string root = FindRepositoryRoot();
        using TestDirectory directory = TestDirectory.Create();
        using JsonDocument post = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs/audits/2026-08-30/evidence/E1-r4-post-acceptance-v2.json")));
        string implementation = post.RootElement.GetProperty("implementationSubjectCommit").GetString()!;
        string tree = post.RootElement.GetProperty("implementationSubjectTree").GetString()!;
        string assembly = typeof(E1EvidenceSupportTests).Assembly.Location;
        string closure = Path.Combine(root, "docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json");

        string command = $$"""
            $ErrorActionPreference = 'Stop'
            Import-Module '{{SupportModule}}' -Force
            $env:GRANITE_E2E_REPOSITORY_ROOT = '{{Ps(root)}}'
            $env:GRANITE_E2E_CANDIDATE_COMMIT = '{{CandidateCommit}}'
            $env:GRANITE_E2E_CANDIDATE_TREE = '{{CandidateTree}}'
            $env:GRANITE_E2E_CANDIDATE_REMOTE = 'origin'
            $env:GRANITE_E2E_CANDIDATE_REMOTE_REF = 'refs/heads/integration/ucl-r4-e1-issued-base-v2'
            $env:GRANITE_E2E_R3_CLOSURE_MANIFEST = '{{Ps(closure)}}'
            $env:GRANITE_E2E_R3_CLOSURE_RELATIVE_PATH = 'docs/audits/2026-08-30/evidence/C0-r4-issue-closure.json'
            $env:GRANITE_E2E_IMPLEMENTATION_COMMIT = '{{implementation}}'
            $env:GRANITE_E2E_IMPLEMENTATION_TREE = '{{tree}}'
            $vstest = Get-E1TrustedVSTest
            $cases = @(
                @('{{PreflightClass}}','{{PreflightMethod}}','preflight.trx'),
                @('{{PostClass}}','{{PostMethod}}','post.trx')
            )
            foreach ($case in $cases) {
                $started = [DateTime]::UtcNow
                $arguments = New-E1AuthoritativeVSTestArguments -AssemblyPath '{{Ps(assembly)}}' -ExpectedClass $case[0] -ExpectedMethod $case[1] -ResultsRoot '{{Ps(directory.Path)}}' -TrxFileName $case[2]
                & $vstest @arguments
                if ($LASTEXITCODE -ne 0) { throw "VSTest failed: $LASTEXITCODE" }
                Assert-E1AuthoritativeTrx -Path (Join-Path '{{Ps(directory.Path)}}' $case[2]) -ExpectedResultsRoot '{{Ps(directory.Path)}}' -RepositoryRoot '{{Ps(directory.Path)}}' -ExpectedClass $case[0] -ExpectedMethod $case[1] -InvocationStartedUtc $started
            }
            """;

        ProcessResult result = PowerShell(command, root, timeoutMilliseconds: 60_000);
        Assert.AreEqual(0, result.ExitCode, result.Output);
    }

    private static string SupportModule => Path.Combine(FindRepositoryRoot(),
        "tests/E2ETests/GraniteEdgeAI.EndToEndTests/scripts/E1EvidenceSupport.psm1");

    private static string WriteTrx(string root, string name, string className, string method, string testOutcome,
        string runOutcome, int total, int executed, int passed, int failed, int notExecuted)
    {
        string path = Path.Combine(root, name);
        File.WriteAllText(path, $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results><UnitTestResult testId="11111111-1111-1111-1111-111111111111" testName="{{method}}" outcome="{{testOutcome}}" /></Results>
              <TestDefinitions><UnitTest name="{{method}}" id="11111111-1111-1111-1111-111111111111"><TestMethod className="{{className}}" name="{{method}}" /></UnitTest></TestDefinitions>
              <ResultSummary outcome="{{runOutcome}}"><Counters total="{{total}}" executed="{{executed}}" passed="{{passed}}" failed="{{failed}}" notExecuted="{{notExecuted}}" /></ResultSummary>
            </TestRun>
            """);
        return path;
    }

    private static void CreateJunction(string link, string target)
    {
        ProcessResult result = PowerShell($"New-Item -ItemType Junction -Path '{Ps(link)}' -Target '{Ps(target)}' | Out-Null", target);
        Assert.AreEqual(0, result.ExitCode, result.Output);
    }

    private static ProcessResult PowerShell(string command, string workingDirectory, int timeoutMilliseconds = 30_000)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell did not start.");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.IsTrue(process.WaitForExit(timeoutMilliseconds), "PowerShell timed out.");
        return new ProcessResult(process.ExitCode, output);
    }

    private static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git.exe") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Git did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, error);
        return output.Trim();
    }

    private static string FindRepositoryRoot()
    {
        string? path = AppContext.BaseDirectory;
        while (path is not null)
        {
            if (File.Exists(Path.Combine(path, "Directory.Build.props")) && Directory.Exists(Path.Combine(path, "tests/E2ETests"))) return path;
            path = Directory.GetParent(path)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    private static string Ps(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private sealed record ProcessResult(int ExitCode, string Output);
}
