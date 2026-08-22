using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.TrustedTools;

[TestClass]
public sealed class TrustedToolContractTests
{
    private static readonly string[] ExpectedMembers = ["llmfit.exe", "LICENSE", "README.md"];

    [TestMethod]
    public void ManifestCopiesCommandsAndRequiredMembers()
    {
        List<string> members = ["llmfit.exe", "LICENSE", "README.md"];
        List<string> arguments = ["--version"];
        List<TrustedToolCommand> commands = [new("version", arguments)];

        TrustedToolPackageManifest manifest = CreateManifest(members, commands);
        members[0] = "changed.exe";
        arguments[0] = "serve";
        commands.Clear();

        CollectionAssert.AreEqual(
            ExpectedMembers,
            manifest.RequiredMembers.ToArray());
        Assert.AreEqual("--version", manifest.Commands.Single().Arguments.Single());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("../llmfit.exe")]
    [DataRow("folder/llmfit.exe")]
    [DataRow("C:\\llmfit.exe")]
    public void ManifestRejectsUnsafeExecutableMember(string executableMember)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TrustedToolPackageManifest(
                "llmfit",
                "1.1.9",
                executableMember,
                new string('a', 64),
                ["llmfit.exe", "LICENSE", "README.md"],
                PeMachine.Amd64,
                TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
                [new TrustedToolCommand("version", ["--version"])]));
    }

    [TestMethod]
    public void ManifestRejectsCaseCollidingInventory()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateManifest(
                ["llmfit.exe", "LICENSE", "license"],
                [new TrustedToolCommand("version", ["--version"])]));
    }

    [TestMethod]
    [DataRow("ABCDEF")]
    [DataRow("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [DataRow("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void ManifestRejectsNonCanonicalSha256(string sha256)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new TrustedToolPackageManifest(
                "llmfit",
                "1.1.9",
                "llmfit.exe",
                sha256,
                ["llmfit.exe", "LICENSE", "README.md"],
                PeMachine.Amd64,
                TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
                [new TrustedToolCommand("version", ["--version"])]));
    }

    [TestMethod]
    public void ManifestRejectsDuplicateCommandIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateManifest(
                ["llmfit.exe", "LICENSE", "README.md"],
                [
                    new TrustedToolCommand("version", ["--version"]),
                    new TrustedToolCommand("VERSION", ["--version"]),
                ]));
    }

    [TestMethod]
    public void ExternalProcessRequestRejectsUnboundedLimits()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ExternalProcessRequest("version", TimeSpan.Zero, 1024, 1024));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ExternalProcessRequest("version", TimeSpan.FromSeconds(1), 0, 1024));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ExternalProcessRequest("version", TimeSpan.FromSeconds(1), 1024, 0));
    }

    private static TrustedToolPackageManifest CreateManifest(
        IEnumerable<string> members,
        IEnumerable<TrustedToolCommand> commands) =>
        new(
            "llmfit",
            "1.1.9",
            "llmfit.exe",
            new string('a', 64),
            members,
            PeMachine.Amd64,
            TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
            commands);
}
