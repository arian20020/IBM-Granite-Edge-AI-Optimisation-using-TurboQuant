using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Support;

internal sealed class VerifiedLlmFitFixture : IDisposable
{
    private const int MaximumCleanupAttempts = 500;

    private VerifiedLlmFitFixture(string root, string packageRoot, VerifiedTrustedTool tool)
    {
        Root = root;
        PackageRoot = packageRoot;
        Tool = tool;
    }

    private string Root { get; }

    internal string PackageRoot { get; }

    internal VerifiedTrustedTool Tool { get; }

    internal static VerifiedLlmFitFixture Create(string mode)
    {
        string sourceRoot = ResolveFakeToolRoot();
        string testBase = Path.Combine(Path.GetTempPath(), "hi-g3");
        string root = Path.Combine(testBase, Guid.NewGuid().ToString("N"));
        string approvedRoot = Path.Combine(root, "approved");
        string packageRoot = Path.Combine(approvedRoot, "package");
        Directory.CreateDirectory(packageRoot);

        foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourceFile, Path.Combine(packageRoot, Path.GetFileName(sourceFile)));
        }

        File.WriteAllText(Path.Combine(packageRoot, "fake-mode.txt"), mode);
        const string executableName = "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.exe";
        string executablePath = Path.Combine(packageRoot, executableName);
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath)))
            .ToLowerInvariant();
        string[] members = Directory.GetFiles(packageRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Select(static name => name!)
            .ToArray();
        TrustedToolPackageManifest manifest = new(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            executableName,
            hash,
            members,
            PeMachine.Amd64,
            TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
            [
                LlmFitCommandContract.CreateVersionCommand(),
                LlmFitCommandContract.CreateSystemCommand(),
            ]);
        TrustedToolVerificationResult verification = new TrustedToolPackageVerifier().Verify(
            approvedRoot,
            packageRoot,
            manifest);
        Assert.IsTrue(verification.IsVerified, $"Fixture verification failed: {verification.Failure}");
        return new(root, packageRoot, verification.Tool!);
    }

    public void Dispose()
    {
        Tool.Dispose();
        for (int attempt = 0; attempt < MaximumCleanupAttempts && Directory.Exists(Root); attempt++)
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt == MaximumCleanupAttempts - 1)
                {
                    throw;
                }

                Thread.Sleep(20);
            }
        }
    }

    private static string ResolveFakeToolRoot()
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
        string projectRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "ProcessFixtures",
            "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool");
        string[] candidates =
        [
            Path.Combine(projectRoot, "bin", "x64", configuration, "net8.0-windows10.0.19041.0", "win-x64"),
            Path.Combine(projectRoot, "bin", configuration, "net8.0-windows10.0.19041.0", "win-x64"),
        ];
        return candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(
                candidate,
                "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.exe"))) ??
            throw new InvalidOperationException("The harmless fake LLM Fit fixture was not built.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
