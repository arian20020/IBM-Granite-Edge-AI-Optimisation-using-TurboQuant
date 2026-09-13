using System.Buffers.Binary;
using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.TrustedTools;

[TestClass]
public sealed class TrustedToolPackageVerifierTests
{
    [TestMethod]
    public void VerifyAcceptsExactFlatAmd64Package()
    {
        using PackageFixture fixture = PackageFixture.Create();

        TrustedToolVerificationResult result = new TrustedToolPackageVerifier().Verify(
            fixture.ApprovedRoot,
            fixture.PackageRoot,
            fixture.Manifest);

        Assert.IsTrue(result.IsVerified);
        Assert.IsNotNull(result.Tool);
        Assert.AreEqual(Path.GetFullPath(fixture.PackageRoot), result.Tool.PackageRoot);
        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(fixture.PackageRoot, "llmfit.exe")),
            result.Tool.ExecutablePath);
        Assert.AreEqual("llmfit", result.Tool.ToolId);
        Assert.AreEqual("1.1.9", result.Tool.Version);
        Assert.AreEqual(
            TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
            result.Tool.Disposition);
        Assert.IsTrue(result.Tool.Commands.ContainsKey("version"));
        Assert.IsNull(result.Failure);
        result.Tool.Dispose();
    }

    [TestMethod]
    public void VerifyRejectsPackageOutsideApprovedRoot()
    {
        using PackageFixture fixture = PackageFixture.Create(packageOutsideApprovedRoot: true);

        AssertFailure(fixture, TrustedToolVerificationFailure.PathEscape);
    }

    [TestMethod]
    public void VerifyRejectsMissingMember()
    {
        using PackageFixture fixture = PackageFixture.Create();
        File.Delete(Path.Combine(fixture.PackageRoot, "README.md"));

        AssertFailure(fixture, TrustedToolVerificationFailure.InventoryMismatch);
    }

    [TestMethod]
    public void VerifyRejectsExtraMember()
    {
        using PackageFixture fixture = PackageFixture.Create();
        File.WriteAllText(Path.Combine(fixture.PackageRoot, "unexpected.txt"), "unexpected");

        AssertFailure(fixture, TrustedToolVerificationFailure.InventoryMismatch);
    }

    [TestMethod]
    public void VerifyRejectsNestedMember()
    {
        using PackageFixture fixture = PackageFixture.Create();
        Directory.CreateDirectory(Path.Combine(fixture.PackageRoot, "nested"));
        File.WriteAllText(Path.Combine(fixture.PackageRoot, "nested", "file.txt"), "unexpected");

        AssertFailure(fixture, TrustedToolVerificationFailure.InventoryMismatch);
    }

    [TestMethod]
    public void VerifyAcceptsCaseInsensitiveInventoryWhileRetainingExecutableSpelling()
    {
        using PackageFixture fixture = PackageFixture.Create();
        File.Move(
            Path.Combine(fixture.PackageRoot, "LICENSE"),
            Path.Combine(fixture.PackageRoot, "license"));

        TrustedToolVerificationResult result = new TrustedToolPackageVerifier().Verify(
            fixture.ApprovedRoot,
            fixture.PackageRoot,
            fixture.Manifest);

        Assert.IsTrue(result.IsVerified);
        result.Tool!.Dispose();
    }

    [TestMethod]
    public void VerifyRejectsExecutableHashMismatch()
    {
        using PackageFixture fixture = PackageFixture.Create();
        File.AppendAllText(Path.Combine(fixture.PackageRoot, "llmfit.exe"), "changed");

        AssertFailure(fixture, TrustedToolVerificationFailure.HashMismatch);
    }

    [TestMethod]
    public void VerifyRejectsInvalidPeImage()
    {
        using PackageFixture fixture = PackageFixture.Create(executableBytes: [0x4d, 0x5a]);

        AssertFailure(fixture, TrustedToolVerificationFailure.PeInvalid);
    }

    [TestMethod]
    public void VerifyRejectsNonAmd64PeImage()
    {
        using PackageFixture fixture = PackageFixture.Create(
            executableBytes: CreatePeImage(machine: 0x014c));

        AssertFailure(fixture, TrustedToolVerificationFailure.PeArchitectureMismatch);
    }

    [TestMethod]
    public void VerifyRejectsPackageDirectoryReparsePoint()
    {
        using PackageFixture fixture = PackageFixture.Create();
        string realPackage = fixture.PackageRoot + "-real";
        Directory.Move(fixture.PackageRoot, realPackage);
        Directory.CreateSymbolicLink(fixture.PackageRoot, realPackage);

        AssertFailure(fixture, TrustedToolVerificationFailure.ReparsePoint);
    }

    [TestMethod]
    public void VerifyRejectsMemberReparsePoint()
    {
        using PackageFixture fixture = PackageFixture.Create();
        string license = Path.Combine(fixture.PackageRoot, "LICENSE");
        string target = Path.Combine(fixture.Root, "real-license");
        File.Move(license, target);
        File.CreateSymbolicLink(license, target);

        AssertFailure(fixture, TrustedToolVerificationFailure.ReparsePoint);
    }

    [TestMethod]
    public void VerifyRejectsReparsePointBetweenApprovedAndPackageRoot()
    {
        using PackageFixture fixture = PackageFixture.Create();
        string realContainer = Path.Combine(fixture.ApprovedRoot, "real-container");
        string linkedContainer = Path.Combine(fixture.ApprovedRoot, "linked-container");
        Directory.CreateDirectory(realContainer);
        Directory.Move(fixture.PackageRoot, Path.Combine(realContainer, "package"));
        Directory.CreateSymbolicLink(linkedContainer, realContainer);
        fixture.PackageRoot = Path.Combine(linkedContainer, "package");

        AssertFailure(fixture, TrustedToolVerificationFailure.ReparsePoint);
    }

    [TestMethod]
    public void VerifyRejectsPackageChangedToReparsePointBeforeCustodyOpen()
    {
        using PackageFixture fixture = PackageFixture.Create();
        string movedPackage = fixture.PackageRoot + "-real";
        TrustedToolPackageVerifier verifier = new(
            afterDirectoryInspection: () =>
            {
                Directory.Move(fixture.PackageRoot, movedPackage);
                Directory.CreateSymbolicLink(fixture.PackageRoot, movedPackage);
            },
            afterInitialInventory: null);

        TrustedToolVerificationResult result = verifier.Verify(
            fixture.ApprovedRoot,
            fixture.PackageRoot,
            fixture.Manifest);

        result.Tool?.Dispose();
        Assert.AreEqual(TrustedToolVerificationFailure.ReparsePoint, result.Failure);
    }

    [TestMethod]
    public void VerifyReportsMutationAfterInitialInventory()
    {
        using PackageFixture fixture = PackageFixture.Create();
        TrustedToolPackageVerifier verifier = new(() =>
            File.Delete(Path.Combine(fixture.PackageRoot, "README.md")));

        TrustedToolVerificationResult result = verifier.Verify(
            fixture.ApprovedRoot,
            fixture.PackageRoot,
            fixture.Manifest);

        Assert.AreEqual(
            TrustedToolVerificationFailure.PackageChangedDuringVerification,
            result.Failure);
    }

    [TestMethod]
    public void VerifyRejectsMemberChangedToReparsePointAfterInitialInventory()
    {
        using PackageFixture fixture = PackageFixture.Create();
        string member = Path.Combine(fixture.PackageRoot, "README.md");
        string target = Path.Combine(fixture.Root, "replacement-readme");
        File.WriteAllText(target, "readme");
        TrustedToolPackageVerifier verifier = new(() =>
        {
            File.Delete(member);
            File.CreateSymbolicLink(member, target);
        });

        TrustedToolVerificationResult result = verifier.Verify(
            fixture.ApprovedRoot,
            fixture.PackageRoot,
            fixture.Manifest);

        Assert.AreEqual(
            TrustedToolVerificationFailure.PackageChangedDuringVerification,
            result.Failure);
    }

    [TestMethod]
    public void VerifiedToolRetainsDenyWriteDeleteCustodyUntilDisposed()
    {
        using PackageFixture fixture = PackageFixture.Create();
        TrustedToolVerificationResult result = new TrustedToolPackageVerifier().Verify(
            fixture.ApprovedRoot,
            fixture.PackageRoot,
            fixture.Manifest);
        Assert.IsNotNull(result.Tool);
        string executable = result.Tool.ExecutablePath;

        Assert.ThrowsExactly<IOException>(() => File.WriteAllBytes(executable, CreatePeImage()));
        Assert.ThrowsExactly<IOException>(() =>
            Directory.Move(fixture.PackageRoot, fixture.PackageRoot + "-moved"));

        result.Tool.Dispose();
        File.WriteAllBytes(executable, CreatePeImage());
    }

    private static void AssertFailure(
        PackageFixture fixture,
        TrustedToolVerificationFailure expected)
    {
        TrustedToolVerificationResult result = new TrustedToolPackageVerifier().Verify(
            fixture.ApprovedRoot,
            fixture.PackageRoot,
            fixture.Manifest);

        Assert.IsFalse(result.IsVerified);
        Assert.IsNull(result.Tool);
        Assert.AreEqual(expected, result.Failure);
    }

    private static byte[] CreatePeImage(ushort machine = 0x8664)
    {
        byte[] bytes = new byte[0x98];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c, 4), 0x80);
        bytes[0x80] = (byte)'P';
        bytes[0x81] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84, 2), machine);
        return bytes;
    }

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(
            string root,
            string approvedRoot,
            string packageRoot,
            TrustedToolPackageManifest manifest)
        {
            Root = root;
            ApprovedRoot = approvedRoot;
            PackageRoot = packageRoot;
            Manifest = manifest;
        }

        internal string Root { get; }

        internal string ApprovedRoot { get; }

        internal string PackageRoot { get; set; }

        internal TrustedToolPackageManifest Manifest { get; }

        internal static PackageFixture Create(
            bool packageOutsideApprovedRoot = false,
            byte[]? executableBytes = null)
        {
            string root = Path.Combine(Path.GetTempPath(), $"hi-verifier-{Guid.NewGuid():N}");
            string approvedRoot = Path.Combine(root, "approved");
            string packageRoot = packageOutsideApprovedRoot
                ? Path.Combine(root, "outside", "package")
                : Path.Combine(approvedRoot, "package");
            Directory.CreateDirectory(approvedRoot);
            Directory.CreateDirectory(packageRoot);

            byte[] executable = executableBytes ?? CreatePeImage();
            File.WriteAllBytes(Path.Combine(packageRoot, "llmfit.exe"), executable);
            File.WriteAllText(Path.Combine(packageRoot, "LICENSE"), "license");
            File.WriteAllText(Path.Combine(packageRoot, "README.md"), "readme");
            string hash = Convert.ToHexString(SHA256.HashData(executable)).ToLowerInvariant();
            TrustedToolPackageManifest manifest = new(
                "llmfit",
                "1.1.9",
                "llmfit.exe",
                hash,
                ["llmfit.exe", "LICENSE", "README.md"],
                PeMachine.Amd64,
                TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
                [new TrustedToolCommand("version", ["--version"])]);
            return new PackageFixture(root, approvedRoot, packageRoot, manifest);
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
