using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.TurboQuant;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Tests.TurboQuant;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class TurboQuantRouteAdapterTests
{
    private const string Commit = "64d8272666af8855dab306ee026d43b6986be519";
    private const string Model = "ibm-granite/granite-4.1-3b";
    private static readonly string PackageDigest = new('1', 64);
    private static readonly string ModelDigest = new('2', 64);
    private static readonly string[] EvidenceLabels =
        ["Maturity", "KV cache", "Build", "Activation", "Matched evidence"];

    [TestMethod]
    public void ExactCompleteEvidenceIsVisibleExperimentalAndActive()
    {
        TurboQuantActivationState state = Policy().Evaluate(Evidence());

        Assert.AreEqual(TurboQuantActivationDisposition.Active, state.Disposition);
        Assert.IsTrue(state.IsExperimentalVisible);
        Assert.AreEqual("Experimental", state.Maturity);
        CollectionAssert.AreEqual(
            EvidenceLabels,
            state.EvidenceRows.Select(row => row.Label).ToArray());
        Assert.IsFalse(state.EvidenceRows.Any(row =>
            row.Value.Contains("dispatch", StringComparison.OrdinalIgnoreCase) ||
            row.Value.Contains("record", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [DataRow("commit")]
    [DataRow("model-id")]
    [DataRow("package")]
    [DataRow("model")]
    [DataRow("length")]
    [DataRow("device")]
    [DataRow("build")]
    [DataRow("runtime-build")]
    [DataRow("source")]
    [DataRow("implementation")]
    [DataRow("binary")]
    [DataRow("runtime-manifest")]
    [DataRow("closure")]
    [DataRow("security")]
    [DataRow("license")]
    [DataRow("actual-device")]
    public void FoundationalMismatchIsUnavailableAndHidden(string mismatch)
    {
        TurboQuantCampaignEvidence evidence = mismatch switch
        {
            "commit" => Evidence() with { EvidenceCommit = new string('f', 40) },
            "model-id" => Evidence() with { ModelId = "ibm-granite/other" },
            "package" => Evidence() with { PackageManifestSha256 = new string('4', 64) },
            "model" => Evidence() with { ModelSha256 = new string('3', 64) },
            "length" => Evidence() with { ModelLength = 89 },
            "device" => Evidence() with { RequestedDevice = "GPU" },
            "build" => Evidence() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    WorkerManifestDigest = new string('4', 64)
                }
            },
            "runtime-build" => Evidence() with
            {
                BuildEvidence = BuildEvidence() with { RuntimeBuild = "different" }
            },
            "source" => Evidence() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    TurboQuantBuild = BuildEvidence().TurboQuantBuild! with
                    {
                        SourceCommit = new string('d', 40)
                    }
                }
            },
            "implementation" => Evidence() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    TurboQuantBuild = BuildEvidence().TurboQuantBuild! with
                    {
                        ImplementationCommit = new string('e', 40)
                    }
                }
            },
            "binary" => Evidence() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    TurboQuantBuild = BuildEvidence().TurboQuantBuild! with
                    {
                        PatchSeriesDigest = new string('d', 64)
                    }
                }
            },
            "runtime-manifest" => Evidence() with
            {
                BuildEvidence = BuildEvidence() with
                {
                    TurboQuantBuild = BuildEvidence().TurboQuantBuild! with
                    {
                        RuntimeManifestDigest = new string('e', 64)
                    }
                }
            },
            "closure" => Evidence() with { WorkerClosureVerified = false },
            "security" => Evidence() with { SecurityReviewApproved = false },
            "license" => Evidence() with { LicenseReviewApproved = false },
            "actual-device" => Evidence() with { ActualExecutionDevices = ["CPU", "GPU"] },
            _ => throw new AssertInconclusiveException()
        };

        TurboQuantActivationState state = Policy().Evaluate(evidence);

        Assert.AreEqual(TurboQuantActivationDisposition.Unavailable, state.Disposition);
        Assert.IsFalse(state.IsExperimentalVisible);
        Assert.IsFalse(state.CanActivate);
    }

    [TestMethod]
    [DataRow("activation")]
    [DataRow("model-sdpa")]
    [DataRow("requested-codec")]
    [DataRow("actual-codec")]
    [DataRow("activation-origin")]
    [DataRow("rubric")]
    [DataRow("baseline")]
    [DataRow("smoke")]
    [DataRow("quality")]
    [DataRow("memory")]
    [DataRow("performance")]
    [DataRow("repeatability")]
    [DataRow("context")]
    [DataRow("cancellation")]
    [DataRow("cleanup")]
    [DataRow("corruption")]
    [DataRow("turns")]
    [DataRow("streaming")]
    public void IncompleteCampaignIsVisibleButNeverActive(string missing)
    {
        TurboQuantCampaignEvidence evidence = missing switch
        {
            "activation" => Evidence() with { Activation = null },
            "model-sdpa" => Evidence() with
            {
                Activation = Activation() with { ModelSdpaNodeCount = 0 }
            },
            "requested-codec" => Evidence() with
            {
                Activation = Activation() with
                {
                    RequestedKeyCodec = TurboQuantCodec.ScalarU4
                }
            },
            "actual-codec" => Evidence() with
            {
                Activation = Activation() with
                {
                    ActualValueCodec = TurboQuantCodec.ScalarU4
                }
            },
            "activation-origin" => Evidence() with
            {
                Activation = Activation() with
                {
                    EvidenceOrigin = TurboQuantEvidenceOrigin.ParsedConsoleText
                }
            },
            "rubric" => Evidence() with { QualityRubricId = "wrong" },
            "baseline" => Evidence() with { MatchedOfficialBaseline = false },
            "smoke" => Evidence() with { DeterministicSmokePassed = false },
            "quality" => Evidence() with { QualityPassed = false },
            "memory" => Evidence() with { MemoryReductionPassed = false },
            "performance" => Evidence() with { PerformancePassed = false },
            "repeatability" => Evidence() with { RepeatabilityPassed = false },
            "context" => Evidence() with { ContextScalingPassed = false },
            "cancellation" => Evidence() with { CancellationPassed = false },
            "cleanup" => Evidence() with { CleanupPassed = false },
            "corruption" => Evidence() with { CorruptionPassed = false },
            "turns" => Evidence() with { CompletedTurnCount = 1 },
            "streaming" => Evidence() with { StreamingPassed = false },
            _ => throw new AssertInconclusiveException()
        };

        TurboQuantActivationState state = Policy().Evaluate(evidence);

        Assert.AreEqual(TurboQuantActivationDisposition.Unverified, state.Disposition);
        Assert.IsTrue(state.IsExperimentalVisible);
        Assert.IsFalse(state.CanActivate);
        Assert.IsTrue(state.EvidenceRows.Any(row =>
            row.Value.Contains("Unverified", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AdapterActivatesOnlyAnActiveApprovedStateAndKeepsExperimentalBranding()
    {
        RecordingSessionFactory factory = new();
        TurboQuantRouteAdapter adapter = new(factory, Policy().Evaluate(Evidence()));
        FakeActivation inner = new(PromptRouteKind.OpenVino);

        PromptRouteSessionActivation activation = await adapter.ActivateAsync(
            new TurboQuantRouteActivation(inner, PackageDigest, ModelDigest, 88),
            _ => { },
            CancellationToken.None);

        Assert.AreSame(inner, factory.Activation);
        Assert.AreEqual("openvino.turboquant", activation.Session.Capability.RouteId);
        Assert.AreEqual("Experimental", activation.Session.Capability.Maturity);
        StringAssert.Contains(activation.Presentation.CapabilitySummary, "Experimental");
        StringAssert.Contains(activation.Presentation.ExecutionEvidence, "TBQ4/TBQ4");
        StringAssert.Contains(activation.Presentation.BuildEvidence, "Verified");
    }

    [TestMethod]
    public async Task AdapterRejectsUnverifiedStateBeforeStartingAWorker()
    {
        RecordingSessionFactory factory = new();
        TurboQuantRouteAdapter adapter = new(
            factory,
            Policy().Evaluate(Evidence() with { QualityPassed = false }));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            adapter.ActivateAsync(
                new TurboQuantRouteActivation(
                    new FakeActivation(PromptRouteKind.OpenVino),
                    PackageDigest, ModelDigest, 88),
                _ => { },
                CancellationToken.None));
        Assert.AreEqual(0, factory.StartCount);
    }

    [TestMethod]
    [DataRow("package")]
    [DataRow("model")]
    [DataRow("length")]
    public async Task AdapterRejectsAnyPackageIdentityMismatchBeforeWorkerStart(
        string mismatch)
    {
        RecordingSessionFactory factory = new();
        TurboQuantRouteAdapter adapter = new(factory, Policy().Evaluate(Evidence()));
        TurboQuantRouteActivation activation = new(
            new FakeActivation(PromptRouteKind.OpenVino),
            mismatch == "package" ? new string('9', 64) : PackageDigest,
            mismatch == "model" ? new string('8', 64) : ModelDigest,
            mismatch == "length" ? 89 : 88);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            adapter.ActivateAsync(activation, _ => { }, CancellationToken.None));
        Assert.AreEqual(0, factory.StartCount);
    }

    [TestMethod]
    public void UnavailableStateCannotBeConstructedAsAnExposedAdapter()
    {
        RecordingSessionFactory factory = new();
        TurboQuantActivationState unavailable = Policy().Evaluate(
            Evidence() with { SecurityReviewApproved = false });

        Assert.ThrowsExactly<ArgumentException>(() =>
            new TurboQuantRouteAdapter(factory, unavailable));
    }

    [TestMethod]
    public void PublicAdapterRejectsOfficialWorkerBinding()
    {
        OpenVinoBuildEvidence officialBuild = BuildEvidence() with
        {
            TurboQuantBuild = null
        };
        OpenVinoRouteService officialService = new(
            new RejectingWorkerClient(),
            officialBuild);

        Assert.ThrowsExactly<ArgumentException>(() =>
            new TurboQuantRouteAdapter(
                officialService,
                Policy().Evaluate(Evidence())));
    }

    [TestMethod]
    public async Task WrappedSessionKeepsRouteIdentityAndDelegatesLifecycle()
    {
        RecordingSessionFactory factory = new();
        TurboQuantRouteAdapter adapter = new(factory, Policy().Evaluate(Evidence()));
        PromptRouteSessionActivation activation = await adapter.ActivateAsync(
            new TurboQuantRouteActivation(
                new FakeActivation(PromptRouteKind.OpenVino),
                PackageDigest, ModelDigest, 88),
            _ => { },
            CancellationToken.None);
        Guid turnId = Guid.NewGuid();

        _ = await activation.Session.GenerateAsync("hello", 2, CancellationToken.None);
        await activation.Session.StopActiveTurnAsync(turnId, CancellationToken.None);
        await activation.Session.CancelActiveTurnAsync(turnId, CancellationToken.None);
        await activation.Session.CloseAsync(CancellationToken.None);
        await activation.Session.DisposeAsync();

        Assert.AreEqual("openvino.turboquant", activation.Session.Capability.RouteId);
        Assert.AreEqual(1, factory.Session.GenerateCount);
        Assert.AreEqual(turnId, factory.Session.StoppedTurnId);
        Assert.AreEqual(turnId, factory.Session.CancelledTurnId);
        Assert.AreEqual(1, factory.Session.CloseCount);
        Assert.AreEqual(1, factory.Session.DisposeCount);
    }

    [TestMethod]
    public void CentralRegistrationAndPackagingRemainClosedWithoutExternalProof()
    {
        string root = FindRepositoryRoot();
        string composition = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features", "ModelInspection", "Services",
            "ModelInspectionServiceComposition.cs"));
        string packaging = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "OpenVino.WorkerPackaging.targets"));

        Assert.IsFalse(composition.Contains(
            "CreateDefaultTurboQuant", StringComparison.Ordinal));
        Assert.IsFalse(packaging.Contains("TurboQuant", StringComparison.Ordinal));
    }

    private static TurboQuantActivationPolicy Policy() => new(new TurboQuantApprovedTuple(
        Commit,
        Model,
        PackageDigest,
        ModelDigest,
        88,
        BuildEvidence()));

    private static TurboQuantCampaignEvidence Evidence() => new(
        Commit,
        Model,
        PackageDigest,
        ModelDigest,
        88,
        "CPU",
        ["CPU"],
        BuildEvidence(),
        WorkerClosureVerified: true,
        SecurityReviewApproved: true,
        LicenseReviewApproved: true,
        Activation(),
        "GTQ-QUALITY-RUBRIC-v1",
        MatchedOfficialBaseline: true,
        DeterministicSmokePassed: true,
        MemoryReductionPassed: true,
        QualityPassed: true,
        PerformancePassed: true,
        RepeatabilityPassed: true,
        ContextScalingPassed: true,
        CancellationPassed: true,
        CleanupPassed: true,
        CorruptionPassed: true,
        StreamingPassed: true,
        CompletedTurnCount: 2);

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        new string('a', 64),
        new TurboQuantBuildEvidence(
            "f5f594dc0c9e5961785f0d17743486d52eac87e7",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            new string('b', 64),
            new string('c', 64)));

    private static TurboQuantActivationEvent Activation() => new(
        Guid.NewGuid(), Guid.NewGuid(),
        TurboQuantCodec.Tbq4, TurboQuantCodec.Tbq4,
        TurboQuantCodec.Tbq4, TurboQuantCodec.Tbq4,
        TurboQuantAttentionPath.Sdpa,
        64, 2, 22, 32, 32, 128, 704, 2816,
        TurboQuantEvidenceOrigin.OpenVinoProfilingApi,
        ForcedScalarNegative: true);

    private sealed record FakeActivation(PromptRouteKind Kind) : IPromptRouteActivation;

    private sealed class RejectingWorkerClient : IOpenVinoWorkerClient
    {
        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("worker invocation is not expected");

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("worker invocation is not expected");
    }

    private sealed class RecordingSessionFactory : ITurboQuantSessionFactory
    {
        public FakeSession Session { get; } = new();
        public int StartCount { get; private set; }
        public IPromptRouteActivation? Activation { get; private set; }

        public Task<IPromptRouteSession> StartAsync(
            IPromptRouteActivation activation,
            Action<PromptEvent> eventSink,
            CancellationToken cancellationToken)
        {
            StartCount++;
            Activation = activation;
            return Task.FromResult<IPromptRouteSession>(Session);
        }
    }

    private sealed class FakeSession : IPromptRouteSession
    {
        public int GenerateCount { get; private set; }
        public Guid? StoppedTurnId { get; private set; }
        public Guid? CancelledTurnId { get; private set; }
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }

        public PromptRouteCapability Capability => OpenVinoRouteCapability.PromptCapability;
        public Task<PromptTurnResult> GenerateAsync(string prompt, int requestedNewTokens, CancellationToken cancellationToken) =>
            Task.FromResult(Generate());
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopActiveTurnAsync(Guid workerConfirmedTurnId, CancellationToken cancellationToken)
        {
            StoppedTurnId = workerConfirmedTurnId;
            return Task.CompletedTask;
        }
        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CancelActiveTurnAsync(Guid workerConfirmedTurnId, CancellationToken cancellationToken)
        {
            CancelledTurnId = workerConfirmedTurnId;
            return Task.CompletedTask;
        }
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        private PromptTurnResult Generate()
        {
            GenerateCount++;
            return new PromptTurnResult(PromptTurnStatus.Completed, "ok", 1, 1, null);
        }
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
