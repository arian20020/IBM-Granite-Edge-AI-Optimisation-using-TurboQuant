using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.WorkerClient.Tests;

[TestClass]
public sealed class OpenVinoWorkerClientTests
{
    private static readonly string[] BoundaryMethods =
        ["InspectAsync", "StartSessionAsync"];

    private static readonly string[] ConversationLifecycleMethods =
        ["CancelAsync", "CloseAsync", "DisposeAsync", "PromptAsync", "StopAsync"];
    private static readonly string[] NativeEnvironmentKeys =
        ["SystemRoot", "WINDIR"];

    [TestMethod]
    public void PublicBoundaryExposesInspectionAndManagedSessionOperations()
    {
        Type boundary = typeof(IOpenVinoWorkerClient);

        CollectionAssert.AreEquivalent(
            BoundaryMethods,
            boundary.GetMethods()
                .Select(static method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    [TestMethod]
    public void InspectionProgressIsForwardedOnlyAfterRunIdentityAndOrderValidation()
    {
        Guid runId = Guid.NewGuid();
        OpenVinoConversationValidator validator = new();
        validator.Accept(new HelloEvent(
            OpenVinoProtocol.OfficialProtocolId,
            BuildEvidence()));
        validator.Accept(new StartInspectionCommand(
            runId,
            Path.GetFullPath("package"),
            new string('a', 64),
            new string('b', 64),
            1));
        List<InspectionProgressEvent> observed = [];
        InlineProgress<InspectionProgressEvent> progress = new(observed.Add);

        Assert.IsFalse(OpenVinoWorkerClient.TryAcceptInspectionEvent(
            new InspectionStartedEvent(Guid.NewGuid()),
            validator,
            runId,
            progress,
            out _));
        Assert.IsTrue(OpenVinoWorkerClient.TryAcceptInspectionEvent(
            new InspectionStartedEvent(runId),
            validator,
            runId,
            progress,
            out _));
        Assert.IsFalse(OpenVinoWorkerClient.TryAcceptInspectionEvent(
            new InspectionProgressEvent(
                Guid.NewGuid(),
                OpenVinoInspectionStage.ManifestVerified),
            validator,
            runId,
            progress,
            out _));

        foreach (OpenVinoInspectionStage stage in
            Enum.GetValues<OpenVinoInspectionStage>())
        {
            Assert.IsTrue(OpenVinoWorkerClient.TryAcceptInspectionEvent(
                new InspectionProgressEvent(runId, stage),
                validator,
                runId,
                progress,
                out _));
        }

        CollectionAssert.AreEqual(
            Enum.GetValues<OpenVinoInspectionStage>(),
            observed.Select(static item => item.Stage).ToArray());
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoWorkerClient.TryAcceptInspectionEvent(
                new HelloEvent(
                    OpenVinoProtocol.OfficialProtocolId,
                    BuildEvidence()),
                validator,
                runId,
                progress,
                out _));
    }

    [TestMethod]
    public void ConversationExposesExplicitGracefulCloseAlongsideCancellation()
    {
        CollectionAssert.AreEquivalent(
            ConversationLifecycleMethods,
            typeof(OpenVinoConversation)
                .GetMethods()
                .Where(static method =>
                    method.DeclaringType == typeof(OpenVinoConversation) &&
                    !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        System.Reflection.MethodInfo stop = typeof(OpenVinoConversation)
            .GetMethods()
            .Single(static method =>
                method.DeclaringType == typeof(OpenVinoConversation) &&
                method.Name == "StopAsync");
        CollectionAssert.AreEqual(
            new[] { typeof(Guid), typeof(CancellationToken) },
            stop.GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .ToArray(),
            "STOP must carry the caller-confirmed turn identity to the " +
            "protected conversation boundary.");
    }

    [TestMethod]
    public void ProductionDefaultsEnforceEveryReviewedBound()
    {
        OpenVinoWorkerInstallation installation = new(
            Path.GetFullPath("worker-root"),
            "OpenVino.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            BuildEvidence(),
            Amd64Policy("OpenVino.Worker.exe"));

        OpenVinoWorkerClientOptions options =
            OpenVinoWorkerClientOptions.CreateDefault(installation);

        Assert.AreEqual(TimeSpan.FromSeconds(5), options.StartupTimeout);
        Assert.AreEqual(TimeSpan.FromMinutes(10), options.TurnTimeout);
        Assert.AreEqual(TimeSpan.FromMinutes(5), options.IdleTimeout);
        Assert.AreEqual(TimeSpan.FromMinutes(60), options.SessionTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(5), options.CancellationGrace);
        Assert.AreEqual(TimeSpan.FromSeconds(5), options.CleanupTimeout);
        Assert.AreEqual(256 * 1024, options.MaximumRetainedStandardErrorBytes);
        Assert.AreEqual(OpenVinoProtocol.MaximumLineBytes, options.MaximumLineBytes);
    }

    [TestMethod]
    public void InstallationKeepsOfficialAndTurboQuantIdentitiesDistinct()
    {
        string root = Path.GetFullPath("worker-root");
        OpenVinoWorkerInstallation official = new(
            root,
            "Official.exe",
            OpenVinoProtocol.OfficialProtocolId,
            BuildEvidence(),
            Amd64Policy("Official.exe"));
        OpenVinoWorkerInstallation turboQuant = new(
            root,
            "TurboQuant.exe",
            OpenVinoProtocol.TurboQuantProtocolId,
            BuildEvidence() with
            {
                TurboQuantBuild = new TurboQuantBuildEvidence(
                    new string('a', 40),
                    new string('b', 40),
                    new string('c', 64),
                    new string('d', 64))
            },
            Amd64Policy("TurboQuant.exe"));

        official.Validate();
        turboQuant.Validate();

        Assert.AreNotEqual(official.ExpectedProtocolId, turboQuant.ExpectedProtocolId);
    }

    [TestMethod]
    public void TestPoliciesMayShortenButCannotRelaxReviewedCeilings()
    {
        OpenVinoWorkerInstallation installation = new(
            Path.GetFullPath("worker-root"),
            "OpenVino.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            BuildEvidence(),
            Amd64Policy("OpenVino.Worker.exe"));
        OpenVinoWorkerClientOptions relaxed =
            OpenVinoWorkerClientOptions.CreateDefault(installation) with
            {
                TurnTimeout = TimeSpan.FromMinutes(11)
            };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(relaxed.Validate);
    }

    [TestMethod]
    public void NativeWorkerParentCaptureOmitsDotnetRootsAndHostileAuthority()
    {
        IReadOnlyDictionary<string, string?> captured =
            OpenVinoWorkerClient.CaptureParentEnvironment();

        CollectionAssert.AreEquivalent(
            NativeEnvironmentKeys,
            captured.Keys.ToArray());
        Assert.IsFalse(captured.ContainsKey("DOTNET_ROOT"));
        Assert.IsFalse(captured.ContainsKey("DOTNET_ROOT_X64"));
        Assert.IsFalse(captured.ContainsKey("PATH"));
        Assert.IsFalse(captured.ContainsKey("COMSPEC"));
    }

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "runtime-test-build",
        "genai-test-build",
        "tokenizers-test-build",
        new string('1', 64));

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private static Dictionary<string, OpenVinoWorkerBinaryMachine> Amd64Policy(
        string executable) => new Dictionary<string, OpenVinoWorkerBinaryMachine>(
            StringComparer.Ordinal)
        {
            [executable] = OpenVinoWorkerBinaryMachine.Amd64
        };
}
