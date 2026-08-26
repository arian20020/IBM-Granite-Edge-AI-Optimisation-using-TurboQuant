using System.Text;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class ChangedPathAllowlistTests
{
    [TestMethod]
    public void AllowedEditWritesBomlessNulPathspec()
    {
        using TemporaryGitRepository repository = new();
        File.AppendAllText(Path.Combine(repository.Root, "allowed.txt"), "changed");
        string pathspec = Path.Combine(repository.Root, "paths.bin");
        var result = Invoke(repository.Root, pathspec, "allowed.txt", "unused.txt");
        Assert.AreEqual(0, result.ExitCode, result.Output);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("allowed.txt\0"), File.ReadAllBytes(pathspec));
    }

    [TestMethod]
    public void UnrelatedUntrackedFileFailsWithoutWritingPathspec()
    {
        using TemporaryGitRepository repository = new();
        File.WriteAllText(Path.Combine(repository.Root, "unrelated.txt"), "no");
        string pathspec = Path.Combine(Path.GetTempPath(), $"geai-{Guid.NewGuid():N}.bin");
        var result = Invoke(repository.Root, pathspec, "allowed.txt");
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        Assert.IsFalse(File.Exists(pathspec));
    }

    [TestMethod]
    public void ModifiedPathOutsideAllowlistFailsWithoutWritingPathspec()
    {
        using TemporaryGitRepository repository = new();
        File.AppendAllText(Path.Combine(repository.Root, "outside.txt"), "changed");
        string pathspec = Path.Combine(Path.GetTempPath(), $"geai-{Guid.NewGuid():N}.bin");
        var result = Invoke(repository.Root, pathspec, "allowed.txt");
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        Assert.IsFalse(File.Exists(pathspec));
    }

    private static OptimizationImportManifestTests.ProcessResult Invoke(string root, string pathspec, params string[] allowed)
    {
        string script = Path.Combine(OptimizationImportManifestTests.FindRepositoryRoot(), "scripts", "verification", "Assert-ChangedPaths.ps1");
        List<string> arguments = ["-File", script, "-Repository", root, "-AllowedPath", .. allowed, "-WritePathspec", pathspec];
        return OptimizationImportManifestTests.RunPowerShell(root, [.. arguments]);
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"geai-allowlist-{Guid.NewGuid():N}");
        public TemporaryGitRepository()
        {
            Directory.CreateDirectory(Root);
            Run("init"); Run("config", "user.email", "tests@example.invalid"); Run("config", "user.name", "Tests");
            File.WriteAllText(Path.Combine(Root, "allowed.txt"), "base");
            File.WriteAllText(Path.Combine(Root, "outside.txt"), "base");
            Run("add", "allowed.txt", "outside.txt"); Run("commit", "-m", "base");
        }
        private void Run(params string[] arguments)
        {
            System.Diagnostics.ProcessStartInfo start = new("git.exe") { WorkingDirectory = Root, UseShellExecute = false };
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode);
        }
        public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
    }
}
