using Windows.ApplicationModel;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

[TestClass]
[DoNotParallelize]
public sealed class SignedAcceptanceHostContractTests
{
    [TestMethod]
    [TestCategory("HardwareInspectionProcessAcceptance")]
    public void RunsInsideNormallyInstalledPackage()
    {
        PackageId identity = Package.Current.Id;

        Assert.AreEqual("GraniteEdgeAI.WinUI.UnitTests", identity.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(identity.FullName));
    }
}
