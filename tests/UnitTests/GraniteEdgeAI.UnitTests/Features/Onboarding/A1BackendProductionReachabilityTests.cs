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

    [TestMethod]
    public void BackendAuthorities_AreUniqueAcrossTheWholeProductionCandidate()
    {
        string source = ReadAllProduction();

        Assert.AreEqual(1, Occurrences(
            source, "new CompatibilityEvaluationOrchestrator("));
        Assert.AreEqual(1, Occurrences(
            source, "new OptimizationBackendCompositionFactory("));
        Assert.AreEqual(1, Occurrences(
            source, "OpenVinoOfficialWorkerAuthority.CreateInstallation("));
        Assert.AreEqual(1, Occurrences(
            source, "internal sealed class GgufChatCoordinator"));
        Assert.AreEqual(1, Occurrences(
            source, "internal sealed class ChatDemoController"));
        Assert.AreEqual(1, Occurrences(
            source, "new GgufChatCoordinator("));
    }

    [TestMethod]
    public void GgufChat_HasTwoIntentionalLaunchersAndOneLifetimeOwner()
    {
        string applicationRoot = Path.Combine(
            OptimizationImportManifestTests.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)");
        string callers = ReadProductionTree(
            applicationRoot,
            excludeFileName: "ChatDemoController.cs");
        string owner = ReadProduction(
            "Features", "GgufRuntime", "ChatDemoController.cs");

        Assert.AreEqual(2, Occurrences(
            callers, "ChatDemoController.CreateProductionAsync("));
        Assert.AreEqual(1, Occurrences(
            owner, "retirementTask = retirementStarter.Task;"));
        Assert.AreEqual(1, Occurrences(
            owner, "_ = CompleteRetirementAsync(retirementStarter);"));
    }

    [TestMethod]
    public void A1CompositionTypes_DoNotUseAmbientLastPublishedDirectory()
    {
        string optimization = ReadProduction(
            "Features", "ModelOptimization", "Application",
            "OptimizationBackendCompositionFactory.cs");
        string modelInspection = ReadProduction(
            "Features", "ModelInspection", "Services",
            "ModelInspectionServiceComposition.cs");

        Assert.AreEqual(0, Occurrences(
            optimization + modelInspection, "LastPublishedDirectory"));
    }

    private static string ReadProduction(params string[] relativePath) =>
        File.ReadAllText(Path.Combine(
            OptimizationImportManifestTests.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            Path.Combine(relativePath)));

    private static string ReadAllProduction()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        return string.Join(
            Environment.NewLine,
            new[]
            {
                Path.Combine(root, "IBM Granite with TurboQuant (Intel)"),
                Path.Combine(root, "infrastructure"),
                Path.Combine(root, "shared")
            }.Select(path => ReadProductionTree(path)));
    }

    private static string ReadProductionTree(
        string root,
        string? excludeFileName = null) =>
        string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}DebugFixtures{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    excludeFileName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

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
