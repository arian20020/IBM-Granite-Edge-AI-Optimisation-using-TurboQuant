using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.OpenVino.Tests.Conversion;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoConversionTypedOutcomeTests
{
    [TestMethod]
    [DataRow(false, OpenVinoSupportCode.OperationCancelled)]
    [DataRow(false, OpenVinoSupportCode.RuntimeTimedOut)]
    [DataRow(true, OpenVinoSupportCode.OperationCancelled)]
    [DataRow(true, OpenVinoSupportCode.RuntimeTimedOut)]
    public async Task SealedPipelinePreservesTypedInspectionFailure(
        bool reinspectPublished,
        OpenVinoSupportCode supportCode)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        OpenVinoRouteService routeService = Service(new FailedInspectionWorker(supportCode));
        SealedOpenVinoConversionPipeline pipeline = new(
            Path.GetTempPath(),
            new string('a', 64),
            routeService);

        OpenVinoConversionException failure = reinspectPublished
            ? await Assert.ThrowsExactlyAsync<OpenVinoConversionException>(() =>
                pipeline.ReinspectPublishedAsync(package.Root, CancellationToken.None))
            : await Assert.ThrowsExactlyAsync<OpenVinoConversionException>(() =>
                pipeline.ValidateAsync(package.Root, CancellationToken.None));

        Assert.AreEqual(supportCode, failure.SupportCode);
    }

    [TestMethod]
    [DataRow(ConversionFailureStage.Validation)]
    [DataRow(ConversionFailureStage.Reinspection)]
    public async Task ConversionServiceReturnsCancelledAndCleansCustodyForTypedCancellation(
        ConversionFailureStage failureStage)
    {
        using TemporarySource source = TemporarySource.CopyFixture();
        TypedFailurePipeline pipeline = new(failureStage, OpenVinoSupportCode.OperationCancelled);
        OpenVinoConversionService service = new(pipeline, _ => true);

        OpenVinoConversionResult result = await service.ConvertAsync(
            new OpenVinoConversionRequest(source.Root, source.Destination, Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoConversionStatus.Cancelled, result.Status);
        Assert.AreEqual(OpenVinoSupportCode.OperationCancelled, result.SupportCode);
        Assert.AreEqual(Guid.Empty, result.InspectionRunId);
        Assert.IsFalse(Directory.Exists(source.Destination));
        Assert.AreEqual(0, Directory.EnumerateDirectories(
            Path.GetDirectoryName(source.Destination)!,
            ".granite-openvino-*.staging").Count());
    }

    private static OpenVinoRouteService Service(IOpenVinoWorkerClient worker) => new(
        new OpenVinoStaticPackageInspector(),
        new OpenVinoInspectionHandoffFactory(),
        worker,
        new UnusedChannelFactory());

    private sealed class FailedInspectionWorker(OpenVinoSupportCode supportCode) :
        IOpenVinoWorkerClient
    {
        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult<IOpenVinoEvent>(new InspectionFailedEvent(
                command.InspectionRunId,
                supportCode));

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("Session start is outside inspection.");
    }

    private sealed class UnusedChannelFactory : IOpenVinoPromptChannelFactory
    {
        public Task<IOpenVinoPromptChannel> StartAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("Channel start is outside inspection.");
    }

    public enum ConversionFailureStage
    {
        Validation,
        Reinspection
    }

    private sealed class TypedFailurePipeline(
        ConversionFailureStage failureStage,
        OpenVinoSupportCode supportCode) : IOpenVinoConversionPipeline
    {
        public Task<OpenVinoConverterCompletion> ConvertAsync(
            OpenVinoConverterInvocation invocation,
            CancellationToken cancellationToken)
        {
            File.WriteAllBytes(
                Path.Combine(invocation.StagingDirectory, "artifact.bin"),
                [1, 2, 3]);
            return Task.FromResult(OpenVinoConverterCompletion.CreateTestInstance());
        }

        public Task<OpenVinoConversionValidation> ValidateAsync(
            string stagingDirectory,
            CancellationToken cancellationToken) =>
            failureStage == ConversionFailureStage.Validation
                ? Task.FromException<OpenVinoConversionValidation>(
                    new OpenVinoConversionException(supportCode))
                : Task.FromResult(OpenVinoConversionValidation.CreateTestInstance());

        public Task SmokeAsync(
            OpenVinoConversionValidation validation,
            string stagingDirectory,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public void BeforePublish()
        {
        }

        public Task<Guid> ReinspectPublishedAsync(
            string destinationDirectory,
            CancellationToken cancellationToken) =>
            failureStage == ConversionFailureStage.Reinspection
                ? Task.FromException<Guid>(new OpenVinoConversionException(supportCode))
                : Task.FromResult(Guid.NewGuid());
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
            string root = Path.Combine(
                Path.GetTempPath(),
                $"ov-conversion-outcome-{Guid.NewGuid():N}");
            CopyDirectory(source, root);
            return new TemporaryPackage(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class TemporarySource : IDisposable
    {
        private TemporarySource(string root, string destination)
        {
            Root = root;
            Destination = destination;
        }

        internal string Root { get; }

        internal string Destination { get; }

        internal static TemporarySource CopyFixture()
        {
            string repository = FindRepositoryRoot();
            string fixture = Path.Combine(
                repository,
                "tests",
                "TestFixtures",
                "OpenVINO",
                "Converter",
                "TinyGraniteV1",
                "source");
            string parent = Path.Combine(
                Path.GetTempPath(),
                $"ov-conversion-service-outcome-{Guid.NewGuid():N}");
            string root = Path.Combine(parent, "source");
            CopyDirectory(fixture, root);
            return new TemporarySource(root, Path.Combine(parent, "converted"));
        }

        public void Dispose()
        {
            string? parent = Path.GetDirectoryName(Root);
            if (parent is not null && Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
