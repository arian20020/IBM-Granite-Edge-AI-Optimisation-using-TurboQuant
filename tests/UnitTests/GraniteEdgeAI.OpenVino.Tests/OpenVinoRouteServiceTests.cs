using System.Reflection;
using System.Text;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoRouteServiceTests
{
    [TestMethod]
    public void SecurePackageVerificationUsesFactualPhaseCopyWithoutPercentages()
    {
        string[] messages =
        [
            OpenVinoRouteService.GetPackageVerificationMessage(0d),
            OpenVinoRouteService.GetPackageVerificationMessage(0.25d),
            OpenVinoRouteService.GetPackageVerificationMessage(0.5d),
            OpenVinoRouteService.GetPackageVerificationMessage(0.75d),
            OpenVinoRouteService.GetPackageVerificationMessage(0.95d)
        ];

        CollectionAssert.AllItemsAreUnique(messages);
        Assert.IsTrue(messages[0].Contains("large model file", StringComparison.Ordinal));
        Assert.IsTrue(messages[0].Contains("take up to a minute", StringComparison.Ordinal));
        Assert.IsTrue(messages[2].Contains("did not change", StringComparison.Ordinal));
        Assert.IsTrue(messages[^1].Contains("Finishing", StringComparison.Ordinal));
        Assert.IsFalse(messages.Any(static message => message.Contains('%')));
    }

    [TestMethod]
    public async Task OpenVinoInspectionPublishesTheSharedFiveStageProgressLifecycle()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        ProgressWorkerClient worker = new();
        OpenVinoRouteService service = Service(worker);
        List<ModelInspectionProgress> observed = [];

        OpenVinoRouteInspectionResult result = await service.InspectAsync(
            package.Root,
            new InlineProgress<ModelInspectionProgress>(observed.Add),
            CancellationToken.None);

        Assert.IsTrue(result.Outcome is OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings);
        Assert.IsTrue(observed.Count > 10);
        Assert.AreEqual(ModelInspectionStage.CheckModelPackage, observed[0].Stage);
        Assert.AreEqual(ModelInspectionStageStatus.Active, observed[0].StageStatus);
        Assert.AreEqual(0, observed[0].CompletedStageCount);
        Assert.IsTrue(
            observed.All(static update => update.StageFraction is null),
            "OpenVINO must use the same fractionless stage presentation as the GGUF route.");
        Assert.IsTrue(observed.Any(static update =>
            update.Stage == ModelInspectionStage.ValidateTokenizerAndChatSetup &&
            update.StageStatus == ModelInspectionStageStatus.Completed));
        ModelInspectionProgress terminal = observed[^1];
        Assert.AreEqual(
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            terminal.Stage);
        Assert.AreEqual(ModelInspectionStageStatus.Completed, terminal.StageStatus);
        Assert.AreEqual(5, terminal.CompletedStageCount);
        Assert.AreEqual(5, terminal.TotalStageCount);
        Assert.AreEqual(1, worker.InspectCount);
    }

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
    public async Task LiveRouteRetainsProjectionOfTheExactIssuedHandoff()
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
        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            package.Root,
            CancellationToken.None);
        OpenVinoRouteHandoffLease lease = result.HandoffLease!;

        PropertyInfo? property = typeof(OpenVinoRouteHandoffLease).GetProperty(
            "Projection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            property,
            "The live OpenVINO route does not retain its schema-v2 projection.");
        ModelInspectionProjectionV2 projection =
            (ModelInspectionProjectionV2)property.GetValue(lease)!;

        Assert.AreEqual(result.Handoff!.ModelInspectionHandoffId,
            projection.ModelInspectionHandoff.ModelInspectionHandoffId);
        Assert.AreEqual(result.Handoff.ModelInspectionRunId,
            projection.ModelInspectionHandoff.ModelInspectionRunId);
        Assert.AreEqual(result.Handoff.ModelSha256,
            projection.ModelInspectionHandoff.ModelSha256);
        Assert.AreEqual(result.Handoff.ModelLengthBytes,
            projection.ModelInspectionHandoff.ModelLengthBytes);
        Assert.AreEqual(ModelInspectionRoute.OpenVino, projection.ModelSource.Route);
        Assert.IsFalse(
            Encoding.UTF8.GetString(projection.ToCanonicalUtf8Json())
                .Contains(package.Root, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task MutatedProjectionIsRejectedBeforePathBearingLeaseConsumption()
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
        ModelInspectionProjectionV2 mutated = lease.Projection with
        {
            ModelInspectionHandoff = lease.Projection.ModelInspectionHandoff with
            {
                ModelInspectionHandoffId = Guid.NewGuid()
            }
        };
        FieldInfo? backingField = typeof(OpenVinoRouteHandoffLease).GetField(
            "<Projection>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField);
        backingField.SetValue(lease, mutated);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.StartSessionAsync(
                lease,
                _ => { },
                CancellationToken.None));

        Assert.IsTrue(
            lease.HasPathBearingDescriptor,
            "Projection mismatch consumed the path-bearing descriptor.");
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
        Assert.IsNull(result.ConversionOffer);
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
        Assert.IsNull(result.Handoff);
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

    [TestMethod]
    [DataRow(OpenVinoSupportCode.PackageMissingResource,
        OpenVinoRouteInspectionOutcome.IncompletePackage)]
    [DataRow(OpenVinoSupportCode.PackageInconsistentResource,
        OpenVinoRouteInspectionOutcome.IncompletePackage)]
    [DataRow(OpenVinoSupportCode.PackageUnsafePath,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.PackageChanged,
        OpenVinoRouteInspectionOutcome.StaleEvidence)]
    [DataRow(OpenVinoSupportCode.PackageUnreadable,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.ModelArchitectureUnsupported,
        OpenVinoRouteInspectionOutcome.Unsupported)]
    [DataRow(OpenVinoSupportCode.ModelTaskUnsupported,
        OpenVinoRouteInspectionOutcome.Unsupported)]
    [DataRow(OpenVinoSupportCode.TokenizerUnsupported,
        OpenVinoRouteInspectionOutcome.Unsupported)]
    [DataRow(OpenVinoSupportCode.RuntimeIntegrityFailed,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.RuntimeDependencyMissing,
        OpenVinoRouteInspectionOutcome.DependencyUnavailable)]
    [DataRow(OpenVinoSupportCode.RuntimeLoadFailed,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.RuntimeDeviceUnavailable,
        OpenVinoRouteInspectionOutcome.DependencyUnavailable)]
    [DataRow(OpenVinoSupportCode.RuntimeDeviceMismatch,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.RuntimeContextExceeded,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.RuntimeProtocolFailed,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.RuntimeTimedOut,
        OpenVinoRouteInspectionOutcome.TimedOut)]
    [DataRow(OpenVinoSupportCode.OperationCancelled,
        OpenVinoRouteInspectionOutcome.Cancelled)]
    [DataRow(OpenVinoSupportCode.ConversionPreflightFailed,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.ConversionFailed,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.ConversionOutputInvalid,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.ConversionPublishFailed,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.OptimizationUnsupported,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.TurboQuantUnavailable,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    [DataRow(OpenVinoSupportCode.TurboQuantActivationUnverified,
        OpenVinoRouteInspectionOutcome.InvalidEvidence)]
    public async Task WorkerFailureSupportCodeProducesTypedPathFreeNonReadyResult(
        OpenVinoSupportCode supportCode,
        OpenVinoRouteInspectionOutcome expectedOutcome)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new(command => new InspectionFailedEvent(
            command.InspectionRunId,
            supportCode));

        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            package.Root,
            CancellationToken.None);

        Assert.AreEqual(expectedOutcome, result.Outcome);
        Assert.IsNull(result.Handoff);
        Assert.IsNull(result.HandoffLease);
        Assert.IsNull(result.Configuration);
        Assert.IsNull(result.ConversionOffer);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(supportCode.ToProtocolValue(), result.Failure.SupportCode);
        Assert.IsFalse(result.ToString().Contains(
            package.Root,
            StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, worker.InspectCount);
    }

    [TestMethod]
    public async Task CallerCancellationProducesTypedPathFreeCancelledResult()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        FakeWorkerClient worker = new((_, token) =>
            Task.FromCanceled<IOpenVinoEvent>(token));

        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            package.Root,
            cancellation.Token);

        Assert.AreEqual(OpenVinoRouteInspectionOutcome.Cancelled, result.Outcome);
        Assert.IsNull(result.Handoff);
        Assert.IsNull(result.HandoffLease);
        Assert.IsNull(result.Configuration);
        Assert.IsNull(result.ConversionOffer);
        Assert.AreEqual(
            OpenVinoSupportCode.OperationCancelled.ToProtocolValue(),
            result.Failure?.SupportCode);
        Assert.IsFalse(result.ToString().Contains(
            package.Root,
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task UnexpectedWorkerTerminalProducesInvalidEvidenceWithoutCapability()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new(command =>
            new InspectionStartedEvent(command.InspectionRunId));

        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            package.Root,
            CancellationToken.None);

        Assert.AreEqual(
            OpenVinoRouteInspectionOutcome.InvalidEvidence,
            result.Outcome);
        Assert.IsNull(result.Handoff);
        Assert.IsNull(result.HandoffLease);
        Assert.IsNull(result.Configuration);
        Assert.IsNull(result.ConversionOffer);
        Assert.AreEqual(
            OpenVinoSupportCode.RuntimeProtocolFailed.ToProtocolValue(),
            result.Failure?.SupportCode);
    }

    [TestMethod]
    [DataRow("inspection-run")]
    [DataRow("package-digest")]
    [DataRow("model-digest")]
    [DataRow("model-length")]
    public async Task MismatchedCompletedEvidenceProducesTypedStaleResultWithoutCapability(
        string mismatch)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new(command =>
        {
            InspectionCompletedEvent completed = Completed(command);
            return mismatch switch
            {
                "inspection-run" => completed with { InspectionRunId = Guid.NewGuid() },
                "package-digest" => completed with { PackageManifestDigest = new string('b', 64) },
                "model-digest" => completed with { ModelSha256 = new string('c', 64) },
                "model-length" => completed with { ModelLengthBytes = completed.ModelLengthBytes + 1 },
                _ => throw new AssertFailedException("Unknown mismatch case.")
            };
        });

        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            package.Root,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoRouteInspectionOutcome.StaleEvidence, result.Outcome);
        AssertNoCapability(result);
        Assert.AreEqual(
            OpenVinoSupportCode.PackageChanged.ToProtocolValue(),
            result.Failure?.SupportCode);
    }

    [TestMethod]
    [DataRow("empty-run")]
    [DataRow("invalid-package-digest")]
    [DataRow("invalid-model-digest")]
    [DataRow("nonpositive-model-length")]
    [DataRow("main-model-not-parsed")]
    [DataRow("tokenizer-not-parsed")]
    [DataRow("detokenizer-not-parsed")]
    [DataRow("missing-build-evidence")]
    public async Task InvalidCompletedEvidenceProducesTypedInvalidResultWithoutCapability(
        string mutation)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new(command =>
        {
            InspectionCompletedEvent completed = Completed(command);
            return mutation switch
            {
                "empty-run" => completed with { InspectionRunId = Guid.Empty },
                "invalid-package-digest" => completed with { PackageManifestDigest = "invalid" },
                "invalid-model-digest" => completed with { ModelSha256 = "invalid" },
                "nonpositive-model-length" => completed with { ModelLengthBytes = 0 },
                "main-model-not-parsed" => completed with { MainModelParsed = false },
                "tokenizer-not-parsed" => completed with { TokenizerParsed = false },
                "detokenizer-not-parsed" => completed with { DetokenizerParsed = false },
                "missing-build-evidence" => completed with { BuildEvidence = null! },
                _ => throw new AssertFailedException("Unknown invalid evidence case.")
            };
        });

        OpenVinoRouteInspectionResult result = await Service(worker).InspectAsync(
            package.Root,
            CancellationToken.None);

        Assert.AreEqual(OpenVinoRouteInspectionOutcome.InvalidEvidence, result.Outcome);
        AssertNoCapability(result);
        Assert.AreEqual(
            OpenVinoSupportCode.RuntimeProtocolFailed.ToProtocolValue(),
            result.Failure?.SupportCode);
    }

    [TestMethod]
    public async Task ProgrammingFailureIsNotConvertedToAnOperationalResult()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        FakeWorkerClient worker = new((_, _) =>
            Task.FromException<IOpenVinoEvent>(
                new InvalidOperationException("programming defect")));

        InvalidOperationException failure =
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                Service(worker).InspectAsync(package.Root, CancellationToken.None));

        Assert.AreEqual("programming defect", failure.Message);
    }

    [TestMethod]
    [DataRow(OpenVinoRouteInspectionOutcome.ConversionRequired, "",
        OpenVinoInspectionPresentationKind.ConversionRequired,
        "Conversion required",
        "This supported Granite source must be converted before local prompting.",
        "Choose another model package.",
        "conversion_required", true)]
    [DataRow(OpenVinoRouteInspectionOutcome.IncompletePackage,
        "package_missing_resource",
        OpenVinoInspectionPresentationKind.IncompletePackage,
        "Incomplete package",
        "The local OpenVINO operation could not continue.",
        "Close the session and retry from model inspection.",
        "package_missing_resource", false)]
    [DataRow(OpenVinoRouteInspectionOutcome.Unsupported,
        "model_architecture_unsupported",
        OpenVinoInspectionPresentationKind.Unsupported,
        "Unsupported package",
        "The local OpenVINO operation could not continue.",
        "Close the session and retry from model inspection.",
        "model_architecture_unsupported", false)]
    [DataRow(OpenVinoRouteInspectionOutcome.DependencyUnavailable,
        "runtime_dependency_missing",
        OpenVinoInspectionPresentationKind.OperationalFailure,
        "OpenVINO unavailable",
        "The local OpenVINO operation could not continue.",
        "Close the session and retry from model inspection.",
        "runtime_dependency_missing", false)]
    [DataRow(OpenVinoRouteInspectionOutcome.Cancelled,
        "operation_cancelled",
        OpenVinoInspectionPresentationKind.Cancelled,
        "Inspection cancelled",
        "The local OpenVINO operation was cancelled.",
        "Start inspection again when ready.",
        "operation_cancelled", false)]
    [DataRow(OpenVinoRouteInspectionOutcome.TimedOut,
        "runtime_timed_out",
        OpenVinoInspectionPresentationKind.OperationalFailure,
        "Inspection timed out",
        "The local OpenVINO operation exceeded its time limit.",
        "Retry the operation.",
        "runtime_timed_out", false)]
    [DataRow(OpenVinoRouteInspectionOutcome.InvalidEvidence,
        "runtime_protocol_failed",
        OpenVinoInspectionPresentationKind.Invalid,
        "Invalid evidence",
        "The local OpenVINO operation could not continue.",
        "Close the session and retry from model inspection.",
        "runtime_protocol_failed", false)]
    [DataRow(OpenVinoRouteInspectionOutcome.StaleEvidence,
        "package_changed",
        OpenVinoInspectionPresentationKind.Invalid,
        "Package changed",
        "The local OpenVINO operation could not continue.",
        "Close the session and retry from model inspection.",
        "package_changed", false)]
    public void HeadlessPresentationPolicyProducesBoundedDisposition(
        OpenVinoRouteInspectionOutcome outcome,
        string supportCode,
        OpenVinoInspectionPresentationKind expectedKind,
        string expectedTitle,
        string expectedMessage,
        string expectedRecovery,
        string expectedDiagnostic,
        bool expectedWarning)
    {
        const string privatePath = @"C:\private\model.xml";
        PromptFailure? failure = string.IsNullOrEmpty(supportCode)
            ? null
            : new PromptFailure(supportCode, privatePath, privatePath);
        OpenVinoRouteInspectionResult result = new(
            outcome,
            HandoffLease: null,
            failure,
            Configuration: null);

        OpenVinoInspectionPresentationDisposition disposition =
            OpenVinoInspectionPresentationPolicy.Create(result);

        Assert.AreEqual(expectedKind, disposition.Kind);
        Assert.AreEqual(expectedTitle, disposition.Title);
        Assert.AreEqual(expectedMessage, disposition.Message);
        Assert.AreEqual(expectedRecovery, disposition.RecoveryAction);
        Assert.AreEqual(expectedDiagnostic, disposition.DiagnosticCode);
        Assert.AreEqual(expectedWarning, disposition.IsWarning);
        Assert.IsFalse(disposition.ToString().Contains(
            privatePath,
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow(OpenVinoRouteInspectionOutcome.Ready)]
    [DataRow(OpenVinoRouteInspectionOutcome.ReadyWithWarnings)]
    public void HeadlessPresentationPolicyRejectsReadyOutcome(
        OpenVinoRouteInspectionOutcome outcome)
    {
        OpenVinoRouteInspectionResult result = new(
            outcome,
            HandoffLease: null,
            Failure: null,
            Configuration: null);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            OpenVinoInspectionPresentationPolicy.Create(result));
    }

    [TestMethod]
    public void LivePageCallsHeadlessPresentationPolicy()
    {
        string repository = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repository,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "ModelInspectionPage.OpenVino.cs"));
        StringAssert.Contains(source,
            "OpenVinoInspectionPresentationPolicy.Create(result)");
    }

    private static OpenVinoRouteService Service(IOpenVinoWorkerClient worker) => new(
        new OpenVinoStaticPackageInspector(),
        new OpenVinoInspectionHandoffFactory(),
        worker,
        new UnusedChannelFactory());

    private static InspectionCompletedEvent Completed(StartInspectionCommand command) => new(
        command.InspectionRunId,
        command.PackageManifestDigest,
        command.ModelSha256,
        command.ModelLengthBytes,
        true,
        true,
        true,
        BuildEvidence());

    private static void AssertNoCapability(OpenVinoRouteInspectionResult result)
    {
        Assert.IsNull(result.Handoff);
        Assert.IsNull(result.HandoffLease);
        Assert.IsNull(result.Configuration);
        Assert.IsNull(result.ConversionOffer);
    }

    private static OpenVinoBuildEvidence BuildEvidence() => new(
        "2026.3.0-22451-8a17657b995-releases/2026/3",
        "2026.3.0.0-3277-bd8d6542e3c",
        "2026.3.0.0-703-183c6f25cda",
        new string('a', 64));

    private sealed class FakeWorkerClient : IOpenVinoWorkerClient
    {
        private readonly Func<StartInspectionCommand, CancellationToken,
            Task<IOpenVinoEvent>> inspect;

        internal FakeWorkerClient(Func<StartInspectionCommand, IOpenVinoEvent> inspect)
            : this((command, _) => Task.FromResult(inspect(command)))
        {
        }

        internal FakeWorkerClient(Func<StartInspectionCommand, CancellationToken,
            Task<IOpenVinoEvent>> inspect) => this.inspect = inspect;

        internal int InspectCount { get; private set; }

        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken)
        {
            InspectCount++;
            return inspect(command, cancellationToken);
        }

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("session start is not part of inspection");
    }

    private sealed class ProgressWorkerClient : IOpenVinoWorkerClient
    {
        internal int InspectCount { get; private set; }

        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException(
                "The progress-aware inspection overload must be used.");

        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            IProgress<InspectionProgressEvent>? progress,
            CancellationToken cancellationToken)
        {
            InspectCount++;
            foreach (OpenVinoInspectionStage stage in Enum.GetValues<OpenVinoInspectionStage>())
            {
                progress?.Report(new InspectionProgressEvent(
                    command.InspectionRunId,
                    stage));
            }

            return Task.FromResult<IOpenVinoEvent>(new InspectionCompletedEvent(
                command.InspectionRunId,
                command.PackageManifestDigest,
                command.ModelSha256,
                command.ModelLengthBytes,
                true,
                true,
                true,
                BuildEvidence()));
        }

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("session start is not part of inspection");
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
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
