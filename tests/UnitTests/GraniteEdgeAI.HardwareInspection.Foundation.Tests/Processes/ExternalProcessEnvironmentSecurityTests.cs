using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.HardwareInspection.LlmFitFakeTool;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Processes;

[TestClass]
[DoNotParallelize]
public sealed class ExternalProcessEnvironmentSecurityTests
{
    private const string Sentinel = "GRANITE_SECURITY_AUDIT_SENTINEL";

    [TestMethod]
    public async Task VerifiedTrustedToolReceivesClosedEnvironmentWithoutParentSecretsOrPath()
    {
        string? previous = Environment.GetEnvironmentVariable(Sentinel);
        try
        {
            Environment.SetEnvironmentVariable(Sentinel, "credential-shaped-parent-value");
            using var package = new VerifiedEnvironmentToolPackage();

            ExternalProcessResult result = await new ExternalProcessRunner().RunAsync(
                package.Tool,
                new ExternalProcessRequest(
                    "system",
                    TimeSpan.FromSeconds(20),
                    64 * 1024,
                    64 * 1024),
                CancellationToken.None);

            Assert.AreEqual(ExternalProcessTerminationReason.Exited, result.TerminationReason);
            Assert.AreEqual(
                0,
                result.ExitCode,
                "The verified tool observed parent secrets, PATH/proxy/profiler injection, or enabled .NET diagnostics.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Sentinel, previous);
        }
    }

    private sealed class VerifiedEnvironmentToolPackage : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "geai-s1-hardware-environment-" + Guid.NewGuid().ToString("N"));

        internal VerifiedEnvironmentToolPackage()
        {
            string packageRoot = Path.Combine(_root, "assert-closed-environment");
            Directory.CreateDirectory(packageRoot);
            string fixtureDirectory = Path.GetDirectoryName(typeof(FakeToolMarker).Assembly.Location)!;
            const string assemblyName = "GraniteEdgeAI.HardwareInspection.LlmFitFakeTool";
            foreach (string source in Directory.EnumerateFiles(fixtureDirectory, assemblyName + "*"))
            {
                File.Copy(source, Path.Combine(packageRoot, Path.GetFileName(source)));
            }

            File.WriteAllText(
                Path.Combine(packageRoot, "fake-mode.txt"),
                "assert-closed-environment" + Environment.NewLine);
            const string executableName = assemblyName + ".exe";
            string executablePath = Path.Combine(packageRoot, executableName);
            string[] members = Directory.EnumerateFiles(packageRoot)
                .Select(Path.GetFileName)
                .Select(static name => name!)
                .Order(StringComparer.Ordinal)
                .ToArray();
            using FileStream executable = File.OpenRead(executablePath);
            string executableHash = Convert.ToHexString(SHA256.HashData(executable))
                .ToLowerInvariant();
            var manifest = new TrustedToolPackageManifest(
                "s1-environment-fixture",
                "1.0.0",
                executableName,
                executableHash,
                members,
                PeMachine.Amd64,
                TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
                [new TrustedToolCommand("system", ["--no-dashboard", "--json", "system"])]);
            TrustedToolVerificationResult verification =
                new TrustedToolPackageVerifier().Verify(_root, packageRoot, manifest);
            Tool = verification.Tool
                ?? throw new InvalidOperationException(
                    $"The audit fixture was not verified: {verification.Failure}.");
        }

        internal VerifiedTrustedTool Tool { get; }

        public void Dispose()
        {
            Tool.Dispose();
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
