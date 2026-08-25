using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Processes;

[TestClass]
[DoNotParallelize]
public sealed class LlamaCppProcessPackageContractTests
{
    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public void SignedPackageContainsRuntimeIndependentFlatFixture()
    {
        using VerifiedPackagedToolFixture fixture =
            VerifiedPackagedToolFixture.CreateLlamaCpp("success");

        CollectionAssert.AreEqual(
            new[]
            {
                "GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe",
                "fake-mode.txt",
            },
            Directory.GetFiles(fixture.PackageRoot)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
                .ToArray());
        CollectionAssert.AreEqual(
            "success\r\n"u8.ToArray(),
            File.ReadAllBytes(Path.Combine(fixture.PackageRoot, "fake-mode.txt")));
    }
}
