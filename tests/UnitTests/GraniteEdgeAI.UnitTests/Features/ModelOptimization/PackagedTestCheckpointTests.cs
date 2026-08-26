using System.Text;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class PackagedTestCheckpointTests
{
    [TestMethod]
    public void ControlledCommandsProveInstallerPathFreshRecipeAndPassingTrx()
    {
        using Fixture fixture = new(total: 2, failed: 0, staleRecipe: false);
        var result = fixture.Invoke();
        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(File.ReadAllText(fixture.PathCapture), @"Microsoft Visual Studio\Installer");
        StringAssert.Contains(result.Output, "discovered=2");
        StringAssert.Contains(result.Output, "source/component evidence");
    }

    [TestMethod]
    public void TrxFailuresAreRejected()
    {
        using Fixture fixture = new(total: 2, failed: 1, staleRecipe: false);
        var result = fixture.Invoke();
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "failed=1");
    }

    [TestMethod]
    public void StaleRecipeIsRejectedBeforeRunner()
    {
        using Fixture fixture = new(total: 2, failed: 0, staleRecipe: true);
        var result = fixture.Invoke();
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "stale");
    }

    [TestMethod]
    public void ZeroDiscoveryIsRejected()
    {
        using Fixture fixture = new(total: 0, failed: 0, staleRecipe: false);
        var result = fixture.Invoke();
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "discovered=0");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"geai-checkpoint-{Guid.NewGuid():N}");
        private readonly int _failed;
        private readonly int _total;
        private readonly bool _stale;
        public string PathCapture => Path.Combine(_root, "path.txt");
        private string Recipe => Path.Combine(_root, "tests.build.appxrecipe");
        public Fixture(int total, int failed, bool staleRecipe)
        {
            _total = total; _failed = failed; _stale = staleRecipe;
            Directory.CreateDirectory(_root);
            File.WriteAllText(Recipe, "recipe");
            File.WriteAllText(Path.Combine(_root, "build.ps1"),
                $"[IO.File]::WriteAllText('{PathCapture.Replace("'", "''")}', $env:PATH); " +
                (_stale ? $"(Get-Item -LiteralPath '{Recipe.Replace("'", "''")}').LastWriteTimeUtc=[datetime]'2000-01-01Z'" :
                    $"Start-Sleep -Milliseconds 50; (Get-Item -LiteralPath '{Recipe.Replace("'", "''")}').LastWriteTimeUtc=[datetime]::UtcNow"));
            string trx = $"<?xml version=\"1.0\"?><TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><ResultSummary><Counters total=\"{_total}\" executed=\"{_total}\" passed=\"{_total - _failed}\" failed=\"{_failed}\" /></ResultSummary></TestRun>";
            File.WriteAllText(Path.Combine(_root, "runner.ps1"),
                "param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Rest) " +
                "$result=($Rest | Where-Object { $_ -like '/ResultsDirectory:*' }) -replace '^/ResultsDirectory:',''; " +
                $"[IO.File]::WriteAllText((Join-Path $result 'controlled.trx'), '{trx.Replace("'", "''")}')");
        }
        public OptimizationImportManifestTests.ProcessResult Invoke()
        {
            string script = Path.Combine(OptimizationImportManifestTests.FindRepositoryRoot(), "scripts", "verification", "Invoke-PackagedTestCheckpoint.ps1");
            return OptimizationImportManifestTests.RunPowerShell(_root,
                "-File", script, "-Filter", "Fake", "-EvidenceDirectory", Path.Combine(_root, "evidence"),
                "-BuildExecutable", "powershell.exe", "-BuildArguments", $"-NoProfile -File \"{Path.Combine(_root, "build.ps1")}\"",
                "-TestRunnerExecutable", "powershell.exe", "-TestRunnerPrefixArguments", $"-NoProfile -File \"{Path.Combine(_root, "runner.ps1")}\"",
                "-RecipePath", Recipe, "-RepositoryRoot", OptimizationImportManifestTests.FindRepositoryRoot());
        }
        public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
    }
}
