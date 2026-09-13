using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using System;
using System.IO;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal static class LlmFitToolAuthority
{
    private const string ExecutableSha256 =
        "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19";

    internal static TrustedToolPackageManifest Manifest { get; } = new(
        LlmFitCommandContract.ToolId,
        LlmFitCommandContract.Version,
        "llmfit.exe",
        ExecutableSha256,
        ["llmfit.exe", "LICENSE", "README.md"],
        PeMachine.Amd64,
        TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
        [
            LlmFitCommandContract.CreateVersionCommand(),
            LlmFitCommandContract.CreateSystemCommand(),
        ]);

    internal static string GetProductionPackageRoot()
    {
        string commonApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonApplicationData))
        {
            throw new InvalidOperationException(
                "Machine-wide application data is unavailable.");
        }

        return Path.GetFullPath(Path.Combine(
            commonApplicationData,
            "GraniteEdgeAI",
            "HardwareInspection",
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            "win-x64"));
    }

    internal static TrustedToolVerificationResult Verify(
        TrustedToolPackageVerifier verifier,
        string approvedRoot,
        string packageRoot)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        return verifier.Verify(approvedRoot, packageRoot, Manifest);
    }
}
