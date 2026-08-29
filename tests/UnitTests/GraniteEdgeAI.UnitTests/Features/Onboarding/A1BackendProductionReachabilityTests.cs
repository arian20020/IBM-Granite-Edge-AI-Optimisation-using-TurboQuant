using GraniteEdgeAI.UnitTests.Features.ModelOptimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

[TestClass]
public sealed class A1BackendProductionReachabilityTests
{
    [TestMethod]
    public void CompatibilityOrchestrator_HasExactlyOneProductionRegistration()
    {
        string source = ReadProduction(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");

        Assert.AreEqual(1, Occurrences(
            source, "new CompatibilityEvaluationOrchestrator("));
    }

    [TestMethod]
    public void OptimizationFactory_HasExactlyOneProductionRegistration()
    {
        string source = ReadProduction(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");

        Assert.AreEqual(1, Occurrences(
            source, "new OptimizationBackendCompositionFactory("));
    }

    [TestMethod]
    public void OfficialWorkerAuthority_HasOneRegistrationAndNoDuplicateInventory()
    {
        string source = ReadProduction(
            "Features", "ModelInspection", "Services",
            "ModelInspectionServiceComposition.cs");

        Assert.AreEqual(1, Occurrences(
            source, "OpenVinoOfficialWorkerAuthority.CreateInstallation("));
        Assert.AreEqual(0, Occurrences(source, "OfficialBinaryMachines("));
        Assert.AreEqual(0, Occurrences(
            source, "OpenVinoWorkerInstallation installation = new("));
    }

    private static string ReadProduction(params string[] relativePath) =>
        File.ReadAllText(Path.Combine(
            OptimizationImportManifestTests.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            Path.Combine(relativePath)));

    private static int Occurrences(string source, string value)
    {
        int count = 0;
        for (int offset = 0;
             (offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0;
             offset += value.Length)
        {
            count++;
        }
        return count;
    }
}
