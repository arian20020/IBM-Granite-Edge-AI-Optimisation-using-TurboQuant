using System.Security.Cryptography;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests.Conversion;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoConversionServiceTests
{
    private static readonly string[] SuccessfulCalls =
        ["convert", "validate", "smoke", "reinspect"];
    private static readonly OpenVinoConversionStage[] SuccessfulStages =
    [
        OpenVinoConversionStage.Preflight,
        OpenVinoConversionStage.Converting,
        OpenVinoConversionStage.ValidatingOutput,
        OpenVinoConversionStage.SmokeTesting,
        OpenVinoConversionStage.Publishing,
        OpenVinoConversionStage.Reinspecting,
        OpenVinoConversionStage.Completed
    ];

    [TestMethod]
    public async Task ExplicitConfirmationIsRequiredBeforeAnyWork()
    {
        using ConversionFixture fixture = ConversionFixture.Create();
        RecordingPipeline pipeline = new();
        OpenVinoConversionService service = new(pipeline, _ => true);

        OpenVinoConversionResult result = await service.ConvertAsync(
            new OpenVinoConversionRequest(fixture.Source, fixture.Destination, Confirmed: false),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoConversionStatus.Failed, result.Status);
        Assert.AreEqual(OpenVinoSupportCode.ConversionPreflightFailed, result.SupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.IsFalse(Directory.Exists(fixture.Destination));
    }

    [TestMethod]
    public async Task SuccessfulOrderWritesSanitizedProvenancePublishesAndReinspects()
    {
        using ConversionFixture fixture = ConversionFixture.Create();
        RecordingPipeline pipeline = new();
        List<OpenVinoConversionStage> progress = [];
        OpenVinoConversionService service = new(pipeline, _ => true);

        OpenVinoConversionResult result = await service.ConvertAsync(
            new OpenVinoConversionRequest(fixture.Source, fixture.Destination, Confirmed: true),
            new InlineProgress<OpenVinoConversionProgress>(value => progress.Add(value.Stage)),
            CancellationToken.None);

        Assert.AreEqual(OpenVinoConversionStatus.Published, result.Status);
        Assert.IsNull(result.SupportCode);
        Assert.AreNotEqual(Guid.Empty, result.OperationId);
        Assert.AreNotEqual(Guid.Empty, result.InspectionRunId);
        CollectionAssert.AreEqual(
            SuccessfulCalls,
            pipeline.Calls.ToArray());
        CollectionAssert.AreEqual(
            SuccessfulStages,
            progress.ToArray());
        string provenance = File.ReadAllText(
            Path.Combine(fixture.Destination, OpenVinoProvenance.FileName));
        StringAssert.Contains(provenance, "sourceManifestSha256");
        StringAssert.Contains(provenance, "validationDisposition");
        StringAssert.Contains(provenance, "smokeDisposition");
        Assert.IsFalse(provenance.Contains(fixture.Source, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(provenance.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(provenance.Contains("prompt-secret", StringComparison.Ordinal));
        Assert.IsFalse(provenance.Contains("answer-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task OneTimeOfferKeepsItsSourceSnapshotPinnedThroughConversion()
    {
        using ConversionFixture fixture = ConversionFixture.Create();
        using SourceModelInspectionResult source = new SourceModelInspector().Inspect(fixture.Source);
        using OpenVinoConversionOffer offer = new(source, fixture.Source);
        OpenVinoConversionService service = new(new RecordingPipeline(), _ => true);

        OpenVinoConversionResult result = await service.ConvertAsync(
            offer,
            confirmed: true,
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoConversionStatus.Published, result.Status);
        Assert.AreEqual(
            Path.Combine(fixture.Root, "source-openvino"),
            result.PublishedDirectory,
            ignoreCase: true);
        Assert.ThrowsExactly<InvalidOperationException>(() => offer.Consume());
    }

    [TestMethod]
    public async Task ConverterValidationSmokeAndPublishFailuresLeaveFinalAbsent()
    {
        foreach (PipelineFailure failure in new[]
        {
            PipelineFailure.Converter,
            PipelineFailure.Validation,
            PipelineFailure.Smoke,
            PipelineFailure.Publish,
            PipelineFailure.Reinspection
        })
        {
            using ConversionFixture fixture = ConversionFixture.Create();
            RecordingPipeline pipeline = new(failure, fixture.Destination);
            OpenVinoConversionService service = new(pipeline, _ => true);

            OpenVinoConversionResult result = await service.ConvertAsync(
                new OpenVinoConversionRequest(fixture.Source, fixture.Destination, Confirmed: true),
                progress: null,
                CancellationToken.None);

            Assert.AreEqual(OpenVinoConversionStatus.Failed, result.Status, failure.ToString());
            Assert.IsNotNull(result.SupportCode, failure.ToString());
            if (failure != PipelineFailure.Publish)
            {
                Assert.IsFalse(Directory.Exists(fixture.Destination), failure.ToString());
            }
            Assert.AreEqual(
                0,
                Directory.EnumerateDirectories(
                    Path.GetDirectoryName(fixture.Destination)!,
                    ".granite-openvino-*.staging").Count(),
                failure.ToString());
        }
    }

    [TestMethod]
    public async Task InsufficientSpaceFailsBeforeStagingOrConverter()
    {
        using ConversionFixture fixture = ConversionFixture.Create();
        RecordingPipeline pipeline = new();
        OpenVinoConversionService service = new(pipeline, _ => false);

        OpenVinoConversionResult result = await service.ConvertAsync(
            new OpenVinoConversionRequest(fixture.Source, fixture.Destination, Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoSupportCode.ConversionPreflightFailed, result.SupportCode);
        Assert.AreEqual(0, pipeline.Calls.Count);
        Assert.AreEqual(0, Directory.EnumerateDirectories(
            Path.GetDirectoryName(fixture.Destination)!, ".granite-openvino-*.staging").Count());
    }

    [TestMethod]
    public async Task CancellationAndLateConverterCompletionCannotPublish()
    {
        using ConversionFixture fixture = ConversionFixture.Create();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingPipeline pipeline = new(entered, release);
        OpenVinoConversionService service = new(pipeline, _ => true);
        using CancellationTokenSource cancellation = new();
        Task<OpenVinoConversionResult> active = service.ConvertAsync(
            new OpenVinoConversionRequest(fixture.Source, fixture.Destination, Confirmed: true),
            progress: null,
            cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        release.SetResult();

        OpenVinoConversionResult result = await active.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(OpenVinoConversionStatus.Cancelled, result.Status);
        Assert.AreEqual(OpenVinoSupportCode.OperationCancelled, result.SupportCode);
        Assert.IsFalse(Directory.Exists(fixture.Destination));
        Assert.AreEqual(0, Directory.EnumerateDirectories(
            Path.GetDirectoryName(fixture.Destination)!, ".granite-openvino-*.staging").Count());
    }

    [TestMethod]
    public async Task CancellationAtEveryPrecommitStageLeavesFinalAbsent()
    {
        OpenVinoConversionStage[] cancellableStages =
        [
            OpenVinoConversionStage.Preflight,
            OpenVinoConversionStage.Converting,
            OpenVinoConversionStage.ValidatingOutput,
            OpenVinoConversionStage.SmokeTesting,
            OpenVinoConversionStage.Publishing,
            OpenVinoConversionStage.Reinspecting
        ];
        foreach (OpenVinoConversionStage stage in cancellableStages)
        {
            using ConversionFixture fixture = ConversionFixture.Create();
            using CancellationTokenSource cancellation = new();
            OpenVinoConversionService service = new(new RecordingPipeline(), _ => true);

            OpenVinoConversionResult result = await service.ConvertAsync(
                new OpenVinoConversionRequest(fixture.Source, fixture.Destination, Confirmed: true),
                new InlineProgress<OpenVinoConversionProgress>(value =>
                {
                    if (value.Stage == stage) cancellation.Cancel();
                }),
                cancellation.Token);

            Assert.AreEqual(OpenVinoConversionStatus.Cancelled, result.Status, stage.ToString());
            Assert.IsFalse(Directory.Exists(fixture.Destination), stage.ToString());
            Assert.AreEqual(0, Directory.EnumerateDirectories(
                Path.GetDirectoryName(fixture.Destination)!,
                ".granite-openvino-*.staging").Count(), stage.ToString());
        }
    }

    [TestMethod]
    public async Task SourceMutationDuringConversionFailsClosedWithoutPublication()
    {
        using ConversionFixture fixture = ConversionFixture.Create();
        OpenVinoConversionService service = new(
            new RecordingPipeline(PipelineFailure.SourceMutation), _ => true);

        OpenVinoConversionResult result = await service.ConvertAsync(
            new OpenVinoConversionRequest(fixture.Source, fixture.Destination, Confirmed: true),
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoConversionStatus.Failed, result.Status);
        Assert.AreEqual(OpenVinoSupportCode.PackageChanged, result.SupportCode);
        Assert.IsFalse(Directory.Exists(fixture.Destination));
    }

    private enum PipelineFailure
    {
        None, Converter, Validation, Smoke, Publish, Reinspection, SourceMutation
    }

    private sealed class RecordingPipeline : IOpenVinoConversionPipeline
    {
        private readonly PipelineFailure failure;
        private readonly string? destination;
        private readonly TaskCompletionSource? entered;
        private readonly TaskCompletionSource? release;

        public RecordingPipeline(
            PipelineFailure failure = PipelineFailure.None,
            string? destination = null)
        {
            this.failure = failure;
            this.destination = destination;
        }

        public RecordingPipeline(TaskCompletionSource entered, TaskCompletionSource release)
        {
            this.entered = entered;
            this.release = release;
        }

        public List<string> Calls { get; } = [];

        public async Task<OpenVinoConverterCompletion> ConvertAsync(
            OpenVinoConverterInvocation invocation,
            CancellationToken cancellationToken)
        {
            Calls.Add("convert");
            if (entered is not null)
            {
                entered.SetResult();
                await release!.Task;
            }
            if (failure == PipelineFailure.Converter)
            {
                throw new OpenVinoConversionException(OpenVinoSupportCode.ConversionFailed);
            }
            File.WriteAllBytes(Path.Combine(invocation.StagingDirectory, "artifact.bin"), [1, 2, 3]);
            if (failure == PipelineFailure.SourceMutation)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(invocation.SourceDirectory, "tokenizer.json"), " ");
                    throw new AssertFailedException("The locked source unexpectedly allowed mutation.");
                }
                catch (IOException)
                {
                    throw new OpenVinoConversionException(OpenVinoSupportCode.PackageChanged);
                }
            }
            return OpenVinoConverterCompletion.CreateTestInstance();
        }

        public Task<OpenVinoConversionValidation> ValidateAsync(
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            Calls.Add("validate");
            if (failure == PipelineFailure.Validation)
            {
                throw new OpenVinoConversionException(OpenVinoSupportCode.ConversionOutputInvalid);
            }
            return Task.FromResult(OpenVinoConversionValidation.CreateTestInstance());
        }

        public Task SmokeAsync(
            OpenVinoConversionValidation validation,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            Calls.Add("smoke");
            if (failure == PipelineFailure.Smoke)
            {
                throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeLoadFailed);
            }
            return Task.CompletedTask;
        }

        public Task<Guid> ReinspectPublishedAsync(
            string destinationDirectory,
            CancellationToken cancellationToken)
        {
            Calls.Add("reinspect");
            if (failure == PipelineFailure.Reinspection)
            {
                throw new OpenVinoConversionException(OpenVinoSupportCode.ConversionOutputInvalid);
            }
            return Task.FromResult(Guid.NewGuid());
        }

        public void BeforePublish()
        {
            if (failure == PipelineFailure.Publish)
            {
                Directory.CreateDirectory(destination!);
            }
        }

    }

    private sealed class ConversionFixture : IDisposable
    {
        private ConversionFixture(string root, string source, string destination)
        {
            Root = root;
            Source = source;
            Destination = destination;
        }
        public string Root { get; }
        public string Source { get; }
        public string Destination { get; }

        public static ConversionFixture Create()
        {
            string repository = FindRepositoryRoot();
            string fixtureSource = Path.Combine(
                repository, "tests", "TestFixtures", "OpenVINO", "Converter",
                "TinyGraniteV1", "source");
            string root = Path.Combine(
                Path.GetTempPath(), "GraniteEdgeAI-Conversion-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string output = Path.Combine(root, "output");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(output);
            foreach (string file in Directory.EnumerateFiles(fixtureSource))
            {
                File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
            }
            return new ConversionFixture(root, source, Path.Combine(output, "converted"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
