using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
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

    private static readonly HashSet<string> LlamaCppClosedModes = new(StringComparer.Ordinal)
    {
        "assert-in-job",
        "identity-mismatch",
        "invalid-json",
        "large-output",
        "nonzero",
        "sleep",
        "spawn-child",
        "spawn-child-exit",
        "success",
    };

    private VerifiedPackagedToolFixture(
        string packageRoot,
        string controlRoot,
        string controlFamily,
        IReadOnlySet<string> closedModes,
        VerifiedTrustedTool tool,
        string? ownedWritablePackageRoot = null)
    {
        PackageRoot = packageRoot;
        ControlRoot = controlRoot;
        _controlFamily = controlFamily;
        _closedModes = closedModes;
        _ownedWritablePackageRoot = ownedWritablePackageRoot;
        Tool = tool;
    }

    private readonly string _controlFamily;
    private readonly IReadOnlySet<string> _closedModes;
    private readonly string? _ownedWritablePackageRoot;

    internal string PackageRoot { get; }

    internal string ControlRoot { get; }

    internal VerifiedTrustedTool Tool { get; }

    internal static VerifiedPackagedToolFixture CreateLlmFit(
        string mode,
        bool useProductionIdentity = false,
        bool useWritableCopy = false)
    {
        if (!ClosedModes.Contains(mode))
        {
            throw new ArgumentException("The fixture mode is not closed.", nameof(mode));
        }

        string packageBase = Path.GetFullPath(AppContext.BaseDirectory);
        string packagedRoot = Path.GetFullPath(Path.Combine(
            packageBase,
            "HardwareInspection",
            "TestTools",
            "LlmFitFake",
            mode));
        string relativePackage = Path.GetRelativePath(packageBase, packagedRoot);
        if (Path.IsPathRooted(relativePackage) ||
            relativePackage.Equals("..", StringComparison.Ordinal) ||
            relativePackage.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The packaged fixture escaped the test package root.");
        }

        if (!Directory.Exists(packagedRoot) ||
            Directory.EnumerateDirectories(packagedRoot).Any())
        {
            throw new InvalidOperationException("The signed flat fixture package is unavailable.");
        }

        string modePath = Path.Combine(packagedRoot, "fake-mode.txt");
        if (!File.ReadAllBytes(modePath).SequenceEqual(
                Encoding.ASCII.GetBytes(mode + Environment.NewLine)))
        {
            throw new InvalidOperationException("The packaged fixture mode does not match its directory.");
        }

        string? ownedWritablePackageRoot = null;
        string packageRoot = packagedRoot;
        if (useWritableCopy)
        {
            ownedWritablePackageRoot = GetWritableLlmFitPackageRoot(mode);
            DeleteOwnedWritableLlmFitPackageRoot(ownedWritablePackageRoot);
            Directory.CreateDirectory(ownedWritablePackageRoot);
            foreach (string sourcePath in Directory.GetFiles(packagedRoot, "*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(sourcePath, Path.Combine(
                    ownedWritablePackageRoot,
                    Path.GetFileName(sourcePath)));
            }

            packageRoot = ownedWritablePackageRoot;
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
        return new(
            packageRoot,
            controlRoot,
            "LlmFitFake",
            ClosedModes,
            verification.Tool!,
            ownedWritablePackageRoot);
    }

    internal static VerifiedPackagedToolFixture CreateLlamaCpp(string mode)
    {
        if (!LlamaCppClosedModes.Contains(mode))
        {
            throw new ArgumentException("The fixture mode is not closed.", nameof(mode));
        }

        string packageBase = Path.GetFullPath(AppContext.BaseDirectory);
        string packageRoot = Path.GetFullPath(Path.Combine(
            packageBase,
            "HardwareInspection",
            "TestTools",
            "LlamaCppProbeFake",
            mode));
        AssertStrictPackageDescendant(packageBase, packageRoot);
        if (!Directory.Exists(packageRoot) || Directory.EnumerateDirectories(packageRoot).Any())
        {
            throw new InvalidOperationException("The signed flat fixture package is unavailable.");
        }

        string modePath = Path.Combine(packageRoot, "fake-mode.txt");
        if (!File.ReadAllBytes(modePath).SequenceEqual(
                Encoding.ASCII.GetBytes(mode + Environment.NewLine)))
        {
            throw new InvalidOperationException("The packaged fixture mode does not match its directory.");
        }

        const string executableName = LlamaCppCapabilityCommandContract.ExecutableName;
        string executablePath = Path.Combine(packageRoot, executableName);
        string[] members = Directory.GetFiles(packageRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Select(static name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string executableHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath)))
            .ToLowerInvariant();
        TrustedToolPackageManifest manifest = new(
            LlamaCppCapabilityCommandContract.ToolId,
            LlamaCppCapabilityCommandContract.Version,
            executableName,
            executableHash,
            members,
            PeMachine.Amd64,
            TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
            [
                LlamaCppCapabilityCommandContract.CreateIdentityCommand(),
                LlamaCppCapabilityCommandContract.CreateCapabilitiesCommand(),
            ]);
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
            "LlamaCppProbeFake",
            mode);
        DeleteOwnedControlRoot(controlRoot, "LlamaCppProbeFake", LlamaCppClosedModes);
        Directory.CreateDirectory(controlRoot);
        return new(
            packageRoot,
            controlRoot,
            "LlamaCppProbeFake",
            LlamaCppClosedModes,
            verification.Tool!);
    }

    public void Dispose()
    {
        Tool.Dispose();
        DeleteOwnedControlRoot(ControlRoot, _controlFamily, _closedModes);
        if (_ownedWritablePackageRoot is not null)
        {
            DeleteOwnedWritableLlmFitPackageRoot(_ownedWritablePackageRoot);
        }
    }

    private static string GetWritableLlmFitPackageRoot(string mode) => Path.Combine(
        Path.GetTempPath(),
        "GraniteEdgeAI.HardwareInspection.Tests",
        "WritablePackages",
        "LlmFitFake",
        mode);

    private static void DeleteOwnedWritableLlmFitPackageRoot(string packageRoot)
    {
        string ownedParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI.HardwareInspection.Tests",
            "WritablePackages",
            "LlmFitFake"));
        DeleteOwnedLeafDirectory(
            packageRoot,
            ownedParent,
            ClosedModes,
            "The writable fixture package");
    }

    private static void AssertStrictPackageDescendant(string packageBase, string packageRoot)
    {
        string relativePackage = Path.GetRelativePath(packageBase, packageRoot);
        if (Path.IsPathRooted(relativePackage) ||
            relativePackage.Equals("..", StringComparison.Ordinal) ||
            relativePackage.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The packaged fixture escaped the test package root.");
        }
    }

    private static void DeleteOwnedControlRoot(
        string controlRoot,
        string controlFamily = "LlmFitFake",
        IReadOnlySet<string>? closedModes = null)
    {
        closedModes ??= ClosedModes;
        string ownedParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI.HardwareInspection.Tests",
            controlFamily));
        DeleteOwnedLeafDirectory(
            controlRoot,
            ownedParent,
            closedModes,
            "The fixture control root");
    }

    private static void DeleteOwnedLeafDirectory(
        string path,
        string ownedParent,
        IReadOnlySet<string> allowedLeaves,
        string description)
    {
        string resolvedPath = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(ownedParent, resolvedPath);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
            !allowedLeaves.Contains(relative))
        {
            throw new InvalidOperationException($"{description} escaped its fixed owned parent.");
        }

        const int MaximumAttempts = 6;
        for (int attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            if (!Directory.Exists(resolvedPath))
            {
                return;
            }

            FileAttributes attributes = File.GetAttributes(resolvedPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"{description} must not be a reparse point.");
            }

            try
            {
                Directory.Delete(resolvedPath, recursive: true);
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

        throw new IOException($"{description} remained after bounded cleanup.");
    }
}
