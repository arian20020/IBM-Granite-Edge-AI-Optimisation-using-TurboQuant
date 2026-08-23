using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

internal sealed class VerifiedPackagedToolFixture : IDisposable
{
    private static readonly HashSet<string> ClosedModes = new(StringComparer.Ordinal)
    {
        "assert-in-job",
        "invalid-json",
        "large-output",
        "nonzero",
        "sleep",
        "spawn-child",
        "spawn-child-exit",
        "success",
        "version-mismatch",
    };

    private VerifiedPackagedToolFixture(
        string packageRoot,
        string controlRoot,
        VerifiedTrustedTool tool)
    {
        PackageRoot = packageRoot;
        ControlRoot = controlRoot;
        Tool = tool;
    }

    internal string PackageRoot { get; }

    internal string ControlRoot { get; }

    internal VerifiedTrustedTool Tool { get; }

    internal static VerifiedPackagedToolFixture CreateLlmFit(
        string mode,
        bool useProductionIdentity = false)
    {
        if (!ClosedModes.Contains(mode))
        {
            throw new ArgumentException("The fixture mode is not closed.", nameof(mode));
        }

        string packageBase = Path.GetFullPath(AppContext.BaseDirectory);
        string packageRoot = Path.GetFullPath(Path.Combine(
            packageBase,
            "HardwareInspection",
            "TestTools",
            "LlmFitFake",
            mode));
        string relativePackage = Path.GetRelativePath(packageBase, packageRoot);
        if (Path.IsPathRooted(relativePackage) ||
            relativePackage.Equals("..", StringComparison.Ordinal) ||
            relativePackage.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The packaged fixture escaped the test package root.");
        }

        if (!Directory.Exists(packageRoot) ||
            Directory.EnumerateDirectories(packageRoot).Any())
        {
            throw new InvalidOperationException("The signed flat fixture package is unavailable.");
        }

        string modePath = Path.Combine(packageRoot, "fake-mode.txt");
        if (!string.Equals(
                File.ReadAllText(modePath),
                mode + Environment.NewLine,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The packaged fixture mode does not match its directory.");
        }

        const string executableName = "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool.exe";
        string executablePath = Path.Combine(packageRoot, executableName);
        string[] members = Directory.GetFiles(packageRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string executableHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath)))
            .ToLowerInvariant();
        TrustedToolPackageManifest manifest = new(
            useProductionIdentity ? LlmFitCommandContract.ToolId : "llmfit-fake",
            LlmFitCommandContract.Version,
            executableName,
            executableHash,
            members,
            PeMachine.Amd64,
            TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
            [LlmFitCommandContract.CreateVersionCommand(), LlmFitCommandContract.CreateSystemCommand()]);
        TrustedToolVerificationResult verification = new TrustedToolPackageVerifier().Verify(
            Path.GetDirectoryName(packageRoot)!,
            packageRoot,
            manifest);
        if (!verification.IsVerified)
        {
            throw new InvalidOperationException("The signed fixture package failed exact verification.");
        }

        string controlRoot = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI.HardwareInspection.Tests",
            "LlmFitFake",
            mode);
        DeleteOwnedControlRoot(controlRoot);
        Directory.CreateDirectory(controlRoot);
        return new(packageRoot, controlRoot, verification.Tool!);
    }

    public void Dispose()
    {
        Tool.Dispose();
        DeleteOwnedControlRoot(ControlRoot);
    }

    private static void DeleteOwnedControlRoot(string controlRoot)
    {
        string ownedParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI.HardwareInspection.Tests",
            "LlmFitFake"));
        string resolvedControlRoot = Path.GetFullPath(controlRoot);
        string relative = Path.GetRelativePath(ownedParent, resolvedControlRoot);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
            !ClosedModes.Contains(relative))
        {
            throw new InvalidOperationException("The fixture control root escaped its fixed owned parent.");
        }

        const int MaximumAttempts = 6;
        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            if (!Directory.Exists(resolvedControlRoot))
            {
                return;
            }

            FileAttributes attributes = File.GetAttributes(resolvedControlRoot);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("The fixture control root must not be a reparse point.");
            }

            try
            {
                Directory.Delete(resolvedControlRoot, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt < MaximumAttempts - 1)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(20 * (1 << attempt)));
                }
            }
        }

        throw new IOException("The fixture control root remained after bounded cleanup.");
    }
}
