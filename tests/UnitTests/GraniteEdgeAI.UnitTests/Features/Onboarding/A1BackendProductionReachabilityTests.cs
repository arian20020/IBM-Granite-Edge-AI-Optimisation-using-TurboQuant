using GraniteEdgeAI.UnitTests.Features.ModelOptimization;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ApplicationComposition;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ModelOptimization.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.UnitTests.Features.GgufRuntime;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

[TestClass]
public sealed class A1BackendProductionReachabilityTests
{
    [TestMethod]
    public async Task CompatibilityAuthority_CapturesOneFreshFactSetPerEvaluation()
    {
        var source = new CountingFreshSource();
        var orchestrator = A1BackendProductionAuthorities.Shared.CreateCompatibility(
            source,
            TimeProvider.System);
        int evaluatorCalls = 0;

        await orchestrator.EvaluateAuthorityAsync(
            new HashSet<string>(StringComparer.Ordinal),
            (fresh, optedIn, evaluatedAtUtc, cancellationToken) =>
            {
                evaluatorCalls++;
                return new CompatibilityEvaluation(
                    CompatibilityEngine.RunWithAvailableAdapters(cancellationToken),
                    null,
                    null);
            },
            CancellationToken.None);

        Assert.AreEqual(1, source.CaptureCount);
        Assert.AreEqual(1, evaluatorCalls);
    }

    [TestMethod]
    public void ProductionRegistryRejectsASecondAuthorityCompositionRoute()
    {
        A1BackendProductionAuthorities first =
            A1BackendProductionAuthorities.Shared;

        Assert.AreEqual(4, first.RegistrationCount);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            A1BackendProductionAuthorities.CreateAdditionalRouteForValidation());
        Assert.AreEqual(4, first.RegistrationCount);
    }

    [TestMethod]
    public async Task ForgedTokenCannotBypassTheProductionRoot()
    {
        object forgedToken = new();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CompatibilityEvaluationOrchestrator.CreateForAuthority(
                forgedToken,
                new CountingFreshSource(),
                TimeProvider.System));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ChatDemoController.CreateInitializedProductionAsync(
                forgedToken,
                null!,
                null!,
                CancellationToken.None));

        Assert.IsTrue(typeof(CompatibilityEvaluationOrchestrator)
            .GetConstructors(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)
            .All(constructor => constructor.IsPrivate));
        Assert.IsTrue(typeof(OptimizationBackendCompositionFactory)
            .GetConstructors(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)
            .All(constructor => constructor.IsPrivate));
    }

    [TestMethod]
    public void OfficialWorkerAuthority_ExposesOneClosedImmutableInventory()
    {
        const string digest =
            "1111111111111111111111111111111111111111111111111111111111111111";
        var installation = A1BackendProductionAuthorities.Shared
            .CreateOfficialWorkerInstallation(
            Path.GetFullPath("official-worker"),
            digest);
        var inventory = (IDictionary<string, OpenVinoWorkerBinaryMachine>)
            installation.ExpectedBinaryMachines;

        Assert.HasCount(9, inventory);
        Assert.ThrowsExactly<NotSupportedException>(() => inventory.Add(
            "second-authority.dll",
            OpenVinoWorkerBinaryMachine.Amd64));
    }

    [TestMethod]
    public void BackendAuthorities_AreUniqueAcrossTheWholeProductionCandidate()
    {
        string source = ReadAllProduction();

        Assert.AreEqual(1, Occurrences(
            source, ".CreateCompatibility("));
        Assert.AreEqual(1, Occurrences(
            source, ".CreateOptimization("));
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
            callers, ".CreateInitializedChatAsync("));
        Assert.AreEqual(0, Occurrences(
            callers, ".InitializeAsync();"));
        Assert.AreEqual(1, Occurrences(
            owner, "retirementTask = retirementStarter.Task;"));
        Assert.AreEqual(1, Occurrences(
            owner, "_ = CompleteRetirementAsync(retirementStarter);"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task GgufChatAuthority_TransfersOneInitializedRetirableOwner()
    {
        await new ChatDemoControllerInitializationTests()
            .SuccessfulFactoryReturnsInitializedOwnerAndRetiresOnce();
    }

    [TestMethod]
    public void ReleaseChatProductionTreeContainsNoDemoAuthority()
    {
        string applicationRoot = Path.Combine(
            OptimizationImportManifestTests.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)");
        string source = ReadProductionTree(applicationRoot);

        Assert.AreEqual(0, Occurrences(source, "DemoGgufChatSession"));
        Assert.AreEqual(0, Occurrences(source, "Preview mode"));
        Assert.AreEqual(0, Occurrences(source, "No model loaded"));
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

    private sealed class CountingFreshSource : ICompatibilityFreshResourcesSource
    {
        internal int CaptureCount { get; private set; }

        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken)
        {
            CaptureCount++;
            return ValueTask.FromResult(CompatibilityFreshResourcesInput.Create(
                8UL * 1024 * 1024 * 1024,
                availableDedicatedDeviceMemoryBytes: null,
                32UL * 1024 * 1024 * 1024,
                DateTimeOffset.UtcNow));
        }
    }
}
