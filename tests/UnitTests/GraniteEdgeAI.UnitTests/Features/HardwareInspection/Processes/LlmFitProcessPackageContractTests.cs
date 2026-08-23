using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Processes;

[TestClass]
[DoNotParallelize]
public sealed class LlmFitProcessPackageContractTests
{
    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public void SignedPackageContainsExactFlatAmd64Fixture()
    {
        using VerifiedPackagedToolFixture fixture =
            VerifiedPackagedToolFixture.CreateLlmFit("success");

        string packageBase = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(AppContext.BaseDirectory));
        Assert.IsTrue(fixture.PackageRoot.StartsWith(
            packageBase + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(Directory.EnumerateDirectories(fixture.PackageRoot).Any());
    }
}
