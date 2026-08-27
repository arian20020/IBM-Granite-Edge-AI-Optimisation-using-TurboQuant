using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class PromptRouteContractRedTests
{
    [TestMethod]
    public async Task RegistryActivatesTestGgufSessionThroughNeutralPresenter()
    {
        FakeSession session = new(Capability(PromptRouteKind.Gguf, "gguf.local"));
        FakeActivation activation = new(PromptRouteKind.Gguf);
        FakeActivatingAdapter adapter = new(session);
        PromptRouteRegistry registry = new([adapter]);

        PromptRouteSessionActivation active = await registry.ActivateAsync(
            activation,
            _ => { },
            CancellationToken.None);
        PromptSessionPresenter presenter = new(active.Presentation);
        presenter.Apply(new PromptEvent(
            PromptEventKind.GeneratingTurn,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            "CPU",
            ["CPU"]));
        presenter.Apply(new PromptEvent(
            PromptEventKind.TextDelta,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "fixture",
            null,
            "CPU",
            ["CPU"]));
        presenter.Apply(new PromptEvent(
            PromptEventKind.TurnCompleted,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            "CPU",
            ["CPU"]));

        Assert.AreSame(session, active.Session);
        Assert.AreSame(activation, adapter.Activation);
        Assert.AreEqual("GGUF test adapter", presenter.State.CapabilitySummary);
        Assert.AreEqual("fixture", presenter.State.ResponseText);
        Assert.IsTrue(presenter.State.SendEnabled);
        Assert.IsFalse(presenter.State.StopEnabled);
        Assert.IsTrue(presenter.State.CancelEnabled);
    }

    [TestMethod]
    public void TerminalEventsDisableEveryPromptActionAndSurfaceSafeSupportCode()
    {
        PromptSessionPresenter presenter = new(new PromptRoutePresentation(
            "OpenVINO GenAI · CPU · Official MVP",
            "Requested CPU · Running CPU",
            "Verified official worker build",
            "Local CPU session ready."));
        presenter.Apply(new PromptEvent(
            PromptEventKind.Failed,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            new PromptFailure(
                "runtime_protocol_failed",
                "The local prompt could not continue.",
                "Choose the package again."),
            "CPU",
            ["CPU"]));

        Assert.IsFalse(presenter.State.SendEnabled);
        Assert.IsFalse(presenter.State.StopEnabled);
        Assert.IsFalse(presenter.State.CancelEnabled);
        StringAssert.Contains(
            presenter.State.ResponseText,
            "Support code: runtime_protocol_failed");
        Assert.IsFalse(presenter.State.ResponseText.Contains('\\'));
        Assert.IsFalse(presenter.State.ResponseText.Contains(":/", StringComparison.Ordinal));

        presenter.Apply(new PromptEvent(
            PromptEventKind.SessionCompleted,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            "CPU",
            ["CPU"]));
        Assert.IsFalse(presenter.State.CancelEnabled);
    }

    [TestMethod]
    public void PresenterOwnsActiveTurnAndPublishesExactCancellationTerminal()
    {
        PromptSessionPresenter presenter = new(new PromptRoutePresentation(
            "Test route",
            "Requested CPU Â· Running CPU",
            "Test evidence",
            "Ready"));
        Guid turnId = Guid.NewGuid();
        int completedBefore = presenter.State.CompletedTurnCount;

        presenter.Apply(Event(PromptEventKind.GeneratingTurn, turnId));

        Assert.IsNull(presenter.State.ActiveTurnId);
        Assert.IsFalse(presenter.State.CancelEnabled);
        Assert.AreEqual(
            PromptEventKind.GeneratingTurn,
            presenter.State.LastEventKind);
        presenter.Apply(Event(PromptEventKind.GenerationConfirmed, turnId));
        Assert.AreEqual(turnId, presenter.State.ActiveTurnId);
        Assert.IsTrue(presenter.State.CancelEnabled);
        long generatingRevision = presenter.State.EventRevision;

        presenter.Apply(Event(PromptEventKind.CancellingSession, turnId));
        presenter.Apply(Event(PromptEventKind.Cancelled, turnId: null));

        Assert.IsNull(presenter.State.ActiveTurnId);
        Assert.AreEqual(PromptEventKind.Cancelled, presenter.State.LastEventKind);
        Assert.AreEqual(completedBefore, presenter.State.CompletedTurnCount);
        Assert.IsGreaterThan(generatingRevision, presenter.State.EventRevision);
        Assert.IsFalse(presenter.State.SendEnabled);
        Assert.IsFalse(presenter.State.StopEnabled);
        Assert.IsFalse(presenter.State.CancelEnabled);
    }

    [TestMethod]
    public void SharedPromptTemplateUsesOnlyRouteNeutralPresentationFields()
    {
        string repositoryRoot = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "ModelInspectionPage.xaml"));
        int promptStart = xaml.IndexOf("x:Name=\"PromptSurface\"", StringComparison.Ordinal);
        Assert.IsTrue(promptStart >= 0);
        string promptTemplate = xaml[promptStart..];

        StringAssert.Contains(promptTemplate, "x:Name=\"PromptExecutionEvidenceText\"");
        StringAssert.Contains(promptTemplate, "x:Name=\"PromptBuildEvidenceText\"");
        Assert.IsFalse(promptTemplate.Contains("OpenVINO", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(promptTemplate.Contains("OpenVino", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ProductionCompositionExposesNoArbitraryVerifiedWorkerRootSeam()
    {
        string repositoryRoot = FindRepositoryRoot();
        string page = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "ModelInspectionPage.OpenVino.cs"));
        string composition = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "Services",
            "ModelInspectionServiceComposition.cs"));

        Assert.IsFalse(page.Contains(
            "OpenVinoRouteServiceFactory",
            StringComparison.Ordinal));
        Assert.IsFalse(composition.Contains(
            "internal static OpenVinoRouteService CreateOpenVinoRouteService",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void SharedPageExposesBoundedConversionCancelAndRetryActions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string page = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "ModelInspectionPage.OpenVino.cs"));

        StringAssert.Contains(page, "ActionId = \"convert-openvino\"");
        StringAssert.Contains(page, "ActionId = \"cancel-openvino-conversion\"");
        StringAssert.Contains(page, "ActionId = \"retry-openvino-conversion\"");
        StringAssert.Contains(page, "The source will not be changed.");
        StringAssert.Contains(page, "No package was published.");
        StringAssert.Contains(page, "Hardware check failed");
        StringAssert.Contains(page, "Conversion cancelled");
    }

    [TestMethod]
    public void RegistryAcceptsBothRouteKindsWithoutRuntimeSpecificContractTypes()
    {
        PromptRouteRegistry registry = new(
        [
            new FakeAdapter(Capability(PromptRouteKind.Gguf, "gguf.local")),
            new FakeAdapter(Capability(PromptRouteKind.OpenVino, "openvino.official"))
        ]);

        Assert.AreEqual("gguf.local",
            registry.GetRequired(PromptRouteKind.Gguf).Capability.RouteId);
        Assert.AreEqual("openvino.official",
            registry.GetRequired(PromptRouteKind.OpenVino).Capability.RouteId);

        Type neutralContract = typeof(IPromptRouteAdapter);
        string[] forbidden = neutralContract.Assembly.GetTypes()
            .Where(type => type.Namespace == "GraniteEdgeAI.Features.Prompting")
            .SelectMany(PublicApiTypes)
            .SelectMany(SignatureTypes)
            .Select(type => $"{type.Namespace}.{type.Name}")
            .Where(name => name.Contains("OpenVino", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Llama", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("TurboQuant", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Quantization", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), forbidden,
            string.Join(", ", forbidden));
    }

    [TestMethod]
    public void RegistryFailsClosedOnDuplicateOrMissingRoute()
    {
        PromptRouteCapability capability =
            Capability(PromptRouteKind.OpenVino, "openvino.official");

        Assert.ThrowsExactly<ArgumentException>(() => new PromptRouteRegistry(
            [new FakeAdapter(capability), new FakeAdapter(capability)]));
        PromptRouteRegistry registry = new([new FakeAdapter(capability)]);
        Assert.ThrowsExactly<KeyNotFoundException>(() =>
            registry.GetRequired(PromptRouteKind.Gguf));
    }

    [TestMethod]
    public void OfficialOpenVinoAdapterRegistersOnlyTheApprovedCpuCandidate()
    {
        PromptRouteCapability capability =
            OpenVinoRouteCapability.PromptCapability;

        Assert.AreEqual(PromptRouteKind.OpenVino, capability.Kind);
        Assert.AreEqual("openvino.official", capability.RouteId);
        Assert.AreEqual("openvino.official.cpu", capability.ConfigurationId);
        Assert.AreEqual("CPU", capability.Device);
        Assert.AreEqual("Official MVP", capability.Maturity);
        Assert.AreEqual(4_096, capability.MaximumContextTokens);
        Assert.AreEqual(128, capability.DefaultRequestedNewTokens);
        Assert.AreEqual(128, capability.MaximumRequestedNewTokens);
    }

    private static PromptRouteCapability Capability(
        PromptRouteKind kind,
        string routeId) => new(
            kind,
            routeId,
            $"{routeId}.cpu",
            "test backend",
            "CPU",
            "test",
            4_096,
            128,
            128);

    private static PromptEvent Event(PromptEventKind kind, Guid? turnId) => new(
        kind,
        Guid.NewGuid(),
        Guid.NewGuid(),
        turnId,
        null,
        null,
        "CPU",
        ["CPU"]);

    private static IEnumerable<Type> PublicApiTypes(Type type)
    {
        yield return type;
        foreach (Type item in type.GetProperties().Select(property => property.PropertyType)
                     .Concat(type.GetMethods().Select(method => method.ReturnType))
                     .Concat(type.GetMethods().SelectMany(method =>
                         method.GetParameters().Select(parameter => parameter.ParameterType))))
        {
            yield return item;
        }
    }

    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        yield return type;
        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in SignatureTypes(argument))
            {
                yield return nested;
            }
        }
    }

    private sealed class FakeAdapter(PromptRouteCapability capability) :
        IPromptRouteAdapter
    {
        public PromptRouteCapability Capability { get; } = capability;
    }

    private sealed record FakeActivation(PromptRouteKind Kind) :
        IPromptRouteActivation;

    private sealed class FakeActivatingAdapter(FakeSession session) :
        IPromptRouteAdapter
    {
        public PromptRouteCapability Capability => session.Capability;

        public IPromptRouteActivation? Activation { get; private set; }

        public Task<PromptRouteSessionActivation> ActivateAsync(
            IPromptRouteActivation activation,
            Action<PromptEvent> eventSink,
            CancellationToken cancellationToken)
        {
            Activation = activation;
            return Task.FromResult(new PromptRouteSessionActivation(
                session,
                new PromptRoutePresentation(
                    "GGUF test adapter",
                    "Requested CPU · Running CPU",
                    "Test-only evidence",
                    "GGUF test session ready.")));
        }
    }

    private sealed class FakeSession(PromptRouteCapability capability) :
        IPromptRouteSession
    {
        public PromptRouteCapability Capability { get; } = capability;

        public Task<PromptTurnResult> GenerateAsync(
            string prompt,
            int requestedNewTokens,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CancelAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CloseAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
