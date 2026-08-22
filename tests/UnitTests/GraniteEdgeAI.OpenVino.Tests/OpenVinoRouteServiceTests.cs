using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoRouteServiceTests
{
    [TestMethod]
    public async Task ReadyPackageProducesSchemaV2HandoffAndPathFreeCpuEvidence()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new(command => new InspectionCompletedEvent(
            command.InspectionRunId,
            command.PackageManifestDigest,
            command.ModelSha256,
            command.ModelLengthBytes,
            true,
            true,
            true,
            BuildEvidence()));
        OpenVinoRouteService service = Service(worker);

        OpenVinoRouteInspectionResult result = await service.InspectAsync(
            package.Root,
            CancellationToken.None);

        Assert.IsTrue(result.Outcome is OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings);
        Assert.IsNotNull(result.Handoff);
        Assert.IsNotNull(result.HandoffLease);
        Assert.AreEqual((ushort)2, result.Handoff.SchemaVersion);
        Assert.IsNotNull(result.Configuration);
        Assert.AreEqual("CPU", result.Configuration.Device);
        string publicResult = result.ToString();
        Assert.IsFalse(publicResult.Contains(package.Root, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, worker.InspectCount);
    }

    [TestMethod]
    public async Task AbandonedReadyHandoffRevokesItsPathBearingDescriptor()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new(command => new InspectionCompletedEvent(
            command.InspectionRunId,
            command.PackageManifestDigest,
            command.ModelSha256,
            command.ModelLengthBytes,
            true,
            true,
            true,
            BuildEvidence()));
        OpenVinoRouteService service = Service(worker);
        OpenVinoRouteInspectionResult result = await service.InspectAsync(
            package.Root,
            CancellationToken.None);
        OpenVinoRouteHandoffLease lease = result.HandoffLease!;

        lease.Dispose();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.StartSessionAsync(
                lease,
                _ => { },
                CancellationToken.None));
        Assert.IsFalse(lease.HasPathBearingDescriptor);
    }

    [TestMethod]
    public async Task RegistryActivatesOfficialSessionAndReturnsNeutralPresentation()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new(command => new InspectionCompletedEvent(
            command.InspectionRunId,
            command.PackageManifestDigest,
            command.ModelSha256,
            command.ModelLengthBytes,
            true,
            true,
            true,
            BuildEvidence()));
        ReadyChannel channel = new();
        OpenVinoRouteService service = new(
            new OpenVinoStaticPackageInspector(),
            new OpenVinoInspectionHandoffFactory(),
            worker,
            new ReadyChannelFactory(channel));
        OpenVinoRouteInspectionResult inspection = await service.InspectAsync(
            package.Root,
            CancellationToken.None);
        PromptRouteRegistry registry = new([service]);

        PromptRouteSessionActivation active = await registry.ActivateAsync(
            inspection.HandoffLease!,
            _ => { },
            CancellationToken.None);

        Assert.AreEqual(PromptRouteKind.OpenVino, active.Session.Capability.Kind);
        Assert.AreEqual(
            "OpenVINO GenAI · CPU · Official MVP",
            active.Presentation.CapabilitySummary);
        Assert.AreEqual(
            "Requested CPU · Running CPU",
            active.Presentation.ExecutionEvidence);
        Assert.AreEqual(
            "Verified official worker build",
            active.Presentation.BuildEvidence);
        Assert.IsFalse(inspection.HandoffLease!.HasPathBearingDescriptor);
        await active.Session.CancelAsync(CancellationToken.None);
        await active.Session.DisposeAsync();
        Assert.IsTrue(channel.DisposeCalled);
    }

    [TestMethod]
    public async Task NonReadyStaticOutcomeProducesNoHandoffAndDoesNotStartWorker()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        File.Delete(Path.Combine(package.Root, "openvino_model.bin"));
        FakeWorkerClient worker = new(_ => throw new AssertFailedException(
            "non-ready package must not start native inspection"));

        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            package.Root,
            CancellationToken.None);

        Assert.IsFalse(result.Outcome is OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings);
        Assert.IsNull(result.Handoff);
        Assert.IsNull(result.HandoffLease);
        Assert.IsNull(result.Configuration);
        Assert.AreEqual(0, worker.InspectCount);
    }

    [TestMethod]
    public async Task AcceptedGraniteSourceProducesAPathFreeOneTimeConversionOffer()
    {
        using TemporarySource source = TemporarySource.CopyFixture();
        FakeWorkerClient worker = new(_ => throw new AssertFailedException(
            "source conversion offer must not start native inspection"));

        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            source.Root,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoRouteInspectionOutcome.ConversionRequired, result.Outcome);
        Assert.IsNull(result.HandoffLease);
        Assert.IsNull(result.Failure);
        Assert.IsNull(result.Configuration);
        Assert.IsNotNull(result.ConversionOffer);
        Assert.AreEqual("GraniteForCausalLM", result.ConversionOffer.Evidence.Architecture);
        Assert.IsFalse(result.ToString().Contains(source.Root, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, worker.InspectCount);

        result.ConversionOffer.Dispose();
        Assert.ThrowsExactly<InvalidOperationException>(() => result.ConversionOffer.Consume());
    }

    private static OpenVinoRouteService Service(FakeWorkerClient worker) => new(
        new OpenVinoStaticPackageInspector(),
        new OpenVinoInspectionHandoffFactory(),
        worker,
        new UnusedChannelFactory());

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        new string('a', 64));

    private sealed class FakeWorkerClient(
        Func<StartInspectionCommand, IOpenVinoEvent> inspect) :
        IOpenVinoWorkerClient
    {
        internal int InspectCount { get; private set; }

        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken)
        {
            InspectCount++;
            return Task.FromResult(inspect(command));
        }

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("session start is not part of inspection");
    }

    private sealed class UnusedChannelFactory : IOpenVinoPromptChannelFactory
    {
        public Task<IOpenVinoPromptChannel> StartAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("channel is not part of inspection");
    }

    private sealed class ReadyChannelFactory(ReadyChannel channel) :
        IOpenVinoPromptChannelFactory
    {
        public Task<IOpenVinoPromptChannel> StartAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult<IOpenVinoPromptChannel>(channel);
    }

    private sealed class ReadyChannel : IOpenVinoPromptChannel
    {
        internal bool DisposeCalled { get; private set; }

        public Task<IOpenVinoEvent> PromptAsync(
            PromptCommand command,
            IProgress<TokenEvent>? progress,
            Action<GenerationStartedEvent>? generationStarted,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("prompt is not part of activation");

        public Task StopAsync(
            Guid expectedTurnId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CancelAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CloseAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TemporaryPackage : IDisposable
    {
        private TemporaryPackage(string root) => Root = root;

        internal string Root { get; }

        internal static TemporaryPackage CopyFixture()
        {
            string source = Path.Combine(
                AppContext.BaseDirectory,
                "TestFixtures",
                "OpenVINO",
                "GenAI",
                "TinySyntheticV1",
                "package");
            string root = Path.Combine(Path.GetTempPath(), $"ov-route-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            return new TemporaryPackage(root);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class TemporarySource : IDisposable
    {
        private TemporarySource(string root) => Root = root;

        internal string Root { get; }

        internal static TemporarySource CopyFixture()
        {
            string repository = FindRepositoryRoot();
            string fixture = Path.Combine(
                repository, "tests", "TestFixtures", "OpenVINO", "Converter",
                "TinyGraniteV1", "source");
            string root = Path.Combine(Path.GetTempPath(), $"ov-source-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            foreach (string file in Directory.EnumerateFiles(fixture))
            {
                File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
            }
            return new TemporarySource(root);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
