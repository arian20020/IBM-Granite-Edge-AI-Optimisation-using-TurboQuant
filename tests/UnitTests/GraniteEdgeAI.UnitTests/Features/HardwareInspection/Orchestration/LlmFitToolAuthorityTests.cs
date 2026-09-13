using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using System.Buffers.Binary;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[TestCategory("HardwareInspectionGate7Acceptance")]
public sealed class LlmFitToolAuthorityTests
{
    [TestMethod]
    public void Manifest_PinsTheOnlyApprovedAdministratorPackageAndCommands()
    {
        TrustedToolPackageManifest manifest = LlmFitToolAuthority.Manifest;

        Assert.AreEqual(LlmFitCommandContract.ToolId, manifest.ToolId);
        Assert.AreEqual(LlmFitCommandContract.Version, manifest.Version);
        Assert.AreEqual("llmfit.exe", manifest.ExecutableRelativePath);
        Assert.AreEqual(
            "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19",
            manifest.ExecutableSha256);
        CollectionAssert.AreEqual(
            new[] { "llmfit.exe", "LICENSE", "README.md" },
            manifest.RequiredMembers.ToArray());
        Assert.AreEqual(PeMachine.Amd64, manifest.RequiredMachine);
        Assert.AreEqual(
            TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
            manifest.Disposition);
        Assert.AreEqual(2, manifest.Commands.Count);
        AssertCommand(
            manifest.Commands[0],
            LlmFitCommandContract.VersionCommandIdentity,
            "--version");
        AssertCommand(
            manifest.Commands[1],
            LlmFitCommandContract.SystemCommandIdentity,
            "--no-dashboard",
            "--json",
            "system");
    }

    [TestMethod]
    public void ProductionRoot_IsTheFixedMachineWideVersionedDirectory()
    {
        string commonApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData);
        string expected = Path.GetFullPath(Path.Combine(
            commonApplicationData,
            "GraniteEdgeAI",
            "HardwareInspection",
            "llmfit",
            "1.1.9",
            "win-x64"));

        string actual = LlmFitToolAuthority.GetProductionPackageRoot();

        Assert.AreEqual(expected, actual);
        Assert.IsTrue(Path.IsPathFullyQualified(actual));
        MethodInfo method = typeof(LlmFitToolAuthority).GetMethod(
            nameof(LlmFitToolAuthority.GetProductionPackageRoot),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.AreEqual(0, method.GetParameters().Length);
        Assert.IsFalse(typeof(LlmFitToolAuthority).GetMethods(BindingFlags.Public |
                BindingFlags.Static)
            .Any(candidate => candidate.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(string))));
    }

    [TestMethod]
    public void Verify_RejectsSyntheticPackageThatCannotMatchPinnedHash()
    {
        using PackageFixture fixture = PackageFixture.Create();

        TrustedToolVerificationResult result = LlmFitToolAuthority.Verify(
            new TrustedToolPackageVerifier(),
            fixture.ApprovedRoot,
            fixture.PackageRoot);

        Assert.IsFalse(result.IsVerified);
        Assert.AreEqual(TrustedToolVerificationFailure.HashMismatch, result.Failure);
        Assert.IsNull(result.Tool);
    }

    [TestMethod]
    public void Verify_RejectsMissingExtraOutsideAndReparsePackagesBeforeExecution()
    {
        using (PackageFixture missing = PackageFixture.Create())
        {
            File.Delete(Path.Combine(missing.PackageRoot, "README.md"));
            AssertFailure(missing, TrustedToolVerificationFailure.InventoryMismatch);
        }

        using (PackageFixture extra = PackageFixture.Create())
        {
            File.WriteAllText(Path.Combine(extra.PackageRoot, "unexpected.txt"), "x");
            AssertFailure(extra, TrustedToolVerificationFailure.InventoryMismatch);
        }

        using (PackageFixture outside = PackageFixture.Create(outsideApprovedRoot: true))
        {
            AssertFailure(outside, TrustedToolVerificationFailure.PathEscape);
        }

        using (PackageFixture reparse = PackageFixture.Create())
        {
            string realPackage = reparse.PackageRoot + "-real";
            Directory.Move(reparse.PackageRoot, realPackage);
            Directory.CreateSymbolicLink(reparse.PackageRoot, realPackage);
            AssertFailure(reparse, TrustedToolVerificationFailure.ReparsePoint);
        }
    }

    private static void AssertCommand(
        TrustedToolCommand command,
        string identity,
        params string[] arguments)
    {
        Assert.AreEqual(identity, command.Identity);
        CollectionAssert.AreEqual(arguments, command.Arguments.ToArray());
    }

    private static void AssertFailure(
        PackageFixture fixture,
        TrustedToolVerificationFailure expected)
    {
        TrustedToolVerificationResult result = LlmFitToolAuthority.Verify(
            new TrustedToolPackageVerifier(),
            fixture.ApprovedRoot,
            fixture.PackageRoot);
        result.Tool?.Dispose();
        Assert.AreEqual(expected, result.Failure);
    }

    private static byte[] CreatePeImage()
    {
        byte[] bytes = new byte[0x98];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c, 4), 0x80);
        bytes[0x80] = (byte)'P';
        bytes[0x81] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84, 2), 0x8664);
        return bytes;
    }

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string root, string approvedRoot, string packageRoot)
        {
            Root = root;
            ApprovedRoot = approvedRoot;
            PackageRoot = packageRoot;
        }

        internal string Root { get; }
        internal string ApprovedRoot { get; }
        internal string PackageRoot { get; }

        internal static PackageFixture Create(bool outsideApprovedRoot = false)
        {
            string root = Path.Combine(Path.GetTempPath(), $"hi-g7-authority-{Guid.NewGuid():N}");
            string approvedRoot = Path.Combine(root, "approved");
            string packageRoot = outsideApprovedRoot
                ? Path.Combine(root, "outside", "package")
                : Path.Combine(approvedRoot, "package");
            Directory.CreateDirectory(approvedRoot);
            Directory.CreateDirectory(packageRoot);
            File.WriteAllBytes(Path.Combine(packageRoot, "llmfit.exe"), CreatePeImage());
            File.WriteAllText(Path.Combine(packageRoot, "LICENSE"), "license");
            File.WriteAllText(Path.Combine(packageRoot, "README.md"), "readme");
            return new(root, approvedRoot, packageRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
