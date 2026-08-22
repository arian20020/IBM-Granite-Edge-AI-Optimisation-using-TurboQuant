#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
public sealed class ModelInspectionFixtureAdapterTests
{
    private const string ExpectedCanonicalPathSha256 =
        "9a7c48097a6fc38d061270f2bff446a24b24cb4732662420828ab45e5a4c56fe";

    private static readonly DateTimeOffset ExpectedIdentityUtc = new(
        2026,
        8,
        10,
        12,
        0,
        0,
        TimeSpan.Zero);

    [TestMethod]
    public void Adapter_AcceptsOnlyBrandedInputAndSnapshotsAnImmutablePlan()
    {
        MethodInfo[] factoryMethods = typeof(ModelInspectionFixtureAdapter)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name == nameof(
                ModelInspectionFixtureAdapter.CreatePlan))
            .ToArray();

        Assert.AreEqual(1, factoryMethods.Length);
        CollectionAssert.AreEqual(
            new[] { typeof(ValidatedModelInspectionFixtureInput) },
            factoryMethods[0]
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.AreEqual(
            typeof(ModelInspectionFixtureExecutionPlan),
            factoryMethods[0].ReturnType);

        ValidatedModelInspectionFixture fixture =
            ModelInspectionFixtureTestCatalogue.Get("MI-033");
        ModelInspectionFixtureExecutionPlan plan =
            ModelInspectionFixtureAdapter.CreatePlan(fixture.Input);

        Assert.IsFalse(ReferenceEquals(fixture.Input.Attempts, plan.Attempts));
        Assert.AreEqual(2, plan.Attempts.Count);
        Assert.AreEqual(1, plan.Attempts[0].Attempt);
        Assert.AreEqual(2, plan.Attempts[1].Attempt);
        Assert.IsFalse(ReferenceEquals(
            fixture.Input.Attempts[0].ServiceSteps,
            plan.Attempts[0].ServiceSteps));
    }

    [TestMethod]
    public void Adapter_AllMemberKindsExcludeFullFixtureAndExpectedContracts()
    {
        string[] violations = FindForbiddenAdapterDependencies(
            typeof(ModelInspectionFixtureAdapter));

        Assert.AreEqual(
            0,
            violations.Length,
            string.Join(Environment.NewLine, violations));

        string[] mutationViolations = FindForbiddenAdapterDependencies(
            typeof(ForbiddenAdapterShape));
        Assert.IsTrue(mutationViolations.Any(value =>
            value.Contains(nameof(ForbiddenAdapterShape.FullFixtureField),
                StringComparison.Ordinal)));
        Assert.IsTrue(mutationViolations.Any(value =>
            value.Contains(".ctor", StringComparison.Ordinal)));
        Assert.IsTrue(mutationViolations.Any(value =>
            value.Contains(nameof(ForbiddenAdapterShape.ExpectedProperty),
                StringComparison.Ordinal)));
        Assert.IsTrue(mutationViolations.Any(value =>
            value.Contains(nameof(ForbiddenAdapterShape.LeakExpected),
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Adapter_RequestAndFileEvidenceShareProductionCanonicalIdentity()
    {
        foreach (ValidatedModelInspectionFixture fixture in
                 ModelInspectionFixtureTestCatalogue.All)
        {
            ModelInspectionFixtureExecutionPlan plan =
                ModelInspectionFixtureAdapter.CreatePlan(fixture.Input);
            string independentDigest = ComputeCanonicalPathSha256(
                plan.Request.ModelPath);

            Assert.AreEqual(
                ExpectedCanonicalPathSha256,
                independentDigest,
                fixture.Id);
            Assert.AreEqual(
                plan.Request.FileName,
                Path.GetFileName(plan.Request.ModelPath),
                true,
                fixture.Id);

            foreach (ModelInspectionFileEvidence file in plan.Attempts
                         .SelectMany(attempt => attempt.ServiceSteps)
                         .Select(step => step.TerminalResult?.Result?.Evidence.File)
                         .OfType<ModelInspectionFileEvidence>())
            {
                Assert.AreEqual(plan.Request.FileName, file.FileName, fixture.Id);
                Assert.AreEqual(independentDigest,
                    file.CanonicalPathSha256,
                    true,
                    fixture.Id);
            }
        }
    }

    [TestMethod]
    public void Adapter_EagerlyValidatesEveryMappedGraphThroughProductionFactory()
    {
        MethodInfo createPlan = typeof(ModelInspectionFixtureAdapter)
            .GetMethod(
                nameof(ModelInspectionFixtureAdapter.CreatePlan),
                BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic) ??
            throw new AssertFailedException("CreatePlan was not found.");
        string productionFactoryIdentity =
            $"{typeof(ModelInspectionPresentationFactory).FullName}." +
            nameof(ModelInspectionPresentationFactory.Create);
        Assert.IsTrue(
            ReachableIlReferences(createPlan).Any(member =>
                string.Equals(
                    $"{member.DeclaringType?.FullName}.{member.Name}",
                    productionFactoryIdentity,
                    StringComparison.Ordinal)),
            "CreatePlan does not eagerly reach the production presentation factory.");

        int mappedGraphCount = 0;
        foreach (ValidatedModelInspectionFixture fixture in
                 ModelInspectionFixtureTestCatalogue.All)
        {
            ModelInspectionFixtureExecutionPlan plan =
                ModelInspectionFixtureAdapter.CreatePlan(fixture.Input);
            Assert.AreEqual(
                ModelInspectionFigmaState.InspectionProgress,
                CreateInitialPresentation(plan.Request).State,
                fixture.Id);

            foreach (ModelInspectionFixtureServiceStepPlan step in
                     plan.Attempts.SelectMany(attempt => attempt.ServiceSteps))
            {
                if (step.Progress is not null)
                {
                    mappedGraphCount++;
                    ModelInspectionPagePresentation presentation =
                        CreateProgressPresentation(plan.Request, step.Progress);
                    Assert.AreEqual(
                        ModelInspectionFigmaState.InspectionProgress,
                        presentation.State,
                        fixture.Id);
                    Assert.AreEqual(step.Progress.UserMessage,
                        presentation.RegionKeys.Progress.Detail,
                        fixture.Id);
                }

                if (step.TerminalResult is not null)
                {
                    mappedGraphCount++;
                    Assert.AreEqual(
                        InferCollapsedState(step.TerminalResult),
                        CreatePresentation(
                            plan.Request,
                            step.TerminalResult).State,
                        fixture.Id);
                }
            }
        }

        Assert.IsGreaterThan(0, mappedGraphCount);
    }

    [TestMethod]
    public void Adapter_MapsAllClosedProfilesToPresentableDomainGraphs()
    {
        (string Id, ModelInspectionOutcome Outcome,
            ModelInspectionFigmaState State)[] cases =
        [
            ("MI-002", ModelInspectionOutcome.Ready,
                ModelInspectionFigmaState.ReadyCollapsed),
            ("MI-044", ModelInspectionOutcome.Ready,
                ModelInspectionFigmaState.ReadyCollapsed),
            ("MI-004", ModelInspectionOutcome.ReadyWithWarnings,
                ModelInspectionFigmaState.ReadyWithWarningsCollapsed),
            ("MI-006", ModelInspectionOutcome.ConversionRequired,
                ModelInspectionFigmaState.ConversionRequiredCollapsed),
            ("MI-008", ModelInspectionOutcome.IncompletePackage,
                ModelInspectionFigmaState.IncompletePackage),
            ("MI-009", ModelInspectionOutcome.Unsupported,
                ModelInspectionFigmaState.Unsupported),
            ("MI-010", ModelInspectionOutcome.Invalid,
                ModelInspectionFigmaState.InvalidCollapsed)
        ];

        foreach ((string id, ModelInspectionOutcome expectedOutcome,
                 ModelInspectionFigmaState expectedState) in cases)
        {
            ValidatedModelInspectionFixture fixture =
                ModelInspectionFixtureTestCatalogue.Get(id);
            ModelInspectionFixtureExecutionPlan plan =
                ModelInspectionFixtureAdapter.CreatePlan(fixture.Input);
            ModelInspectionExecutionResult terminal = FindCompleted(plan);
            ModelInspectionResult result = terminal.Result ??
                throw new AssertFailedException($"{id} did not map a result.");

            Assert.AreEqual(
                @"C:\GraniteEdgeAI-Fixtures\" +
                    fixture.Input.Request.DisplayFileName,
                plan.Request.ModelPath,
                id);
            Assert.AreEqual(
                fixture.Input.Request.DisplayFileName,
                plan.Request.FileName,
                id);
            Assert.AreEqual(
                fixture.Input.Request.DisplayName,
                plan.Request.QuickScan.ModelName,
                id);
            Assert.AreEqual(1_610_612_736L,
                plan.Request.ExpectedFileIdentity.LengthBytes,
                id);
            Assert.AreEqual(
                plan.Request.ExpectedFileIdentity.LengthBytes,
                plan.Request.QuickScan.FileSizeBytes,
                id);
            Assert.AreEqual(
                ExpectedIdentityUtc,
                plan.Request.ExpectedFileIdentity.LastWriteTimeUtc,
                id);

            Assert.AreEqual(expectedOutcome, result.Outcome, id);
            Assert.AreEqual(plan.Request.FileName, result.Evidence.File.FileName, id);
            Assert.AreEqual(
                plan.Request.ExpectedFileIdentity.LengthBytes,
                result.Evidence.File.LengthBytes,
                id);
            Assert.AreEqual(
                plan.Request.ExpectedFileIdentity.LastWriteTimeUtc,
                result.Evidence.File.LastWriteTimeUtc,
                id);
            Assert.AreEqual(ExpectedIdentityUtc, result.StartedAtUtc, id);
            Assert.AreEqual(
                ExpectedIdentityUtc.AddSeconds(2),
                result.CompletedAtUtc,
                id);
            AssertApprovedRuntimeAndTokenizer(result.Evidence, id);

            if (expectedOutcome == ModelInspectionOutcome.ReadyWithWarnings)
            {
                Assert.AreEqual(false, result.Evidence.ChatTemplate.Present, id);
                Assert.AreEqual(1, result.Findings.Count, id);
                Assert.AreEqual(
                    "MI-WARN-CHAT-TEMPLATE-MISSING",
                    result.Findings[0].Code,
                    id);
                Assert.AreEqual(
                    ModelInspectionFindingSeverity.Warning,
                    result.Findings[0].Severity,
                    id);
            }
            else
            {
                Assert.AreEqual(true, result.Evidence.ChatTemplate.Present, id);
                Assert.AreEqual(0, result.Findings.Count, id);
            }

            Assert.AreEqual(
                expectedOutcome == ModelInspectionOutcome.ConversionRequired
                    ? "gguf-conversion-route-v1"
                    : null,
                result.VerifiedConversionRouteId,
                id);

            Assert.AreEqual(
                expectedState,
                CreatePresentation(plan.Request, terminal).State,
                $"{id} fell through the production presentation precondition.");
        }
    }

    [TestMethod]
    public void EvidenceProfiles_PreserveTheirClosedSemanticWitnesses()
    {
        ModelInspectionResult optional = FindCompleted(
            ModelInspectionFixtureAdapter.CreatePlan(
                ModelInspectionFixtureTestCatalogue.Get("MI-044").Input)).Result!;
        Assert.IsNull(optional.Evidence.Configuration.DeclaredContextLength);

        ModelInspectionResult missingMember = FindCompleted(
            ModelInspectionFixtureAdapter.CreatePlan(
                ModelInspectionFixtureTestCatalogue.Get("MI-008").Input)).Result!;
        ModelInspectionResult unsupported = FindCompleted(
            ModelInspectionFixtureAdapter.CreatePlan(
                ModelInspectionFixtureTestCatalogue.Get("MI-009").Input)).Result!;
        ModelInspectionResult invalid = FindCompleted(
            ModelInspectionFixtureAdapter.CreatePlan(
                ModelInspectionFixtureTestCatalogue.Get("MI-010").Input)).Result!;

        CollectionAssert.Contains(
            missingMember.Evidence.Observations.Select(value => value.Code).ToArray(),
            "MI-EVIDENCE-PACKAGE-MEMBER-MISSING");
        CollectionAssert.Contains(
            unsupported.Evidence.Observations.Select(value => value.Code).ToArray(),
            "MI-EVIDENCE-ARCHITECTURE-UNSUPPORTED");
        CollectionAssert.Contains(
            invalid.Evidence.Observations.Select(value => value.Code).ToArray(),
            "MI-EVIDENCE-CROSS-SOURCE-CONTRADICTION");
        Assert.AreNotEqual(
            ModelInspectionFixtureAdapter.CreatePlan(
                ModelInspectionFixtureTestCatalogue.Get("MI-010").Input)
                .Request.QuickScan.Architecture,
            invalid.Evidence.Configuration.Architecture);
    }

    [TestMethod]
    public void ProgressProfiles_MapEveryClosedStageStatusAndBoundedDetail()
    {
        HashSet<ModelInspectionStage> stages = [];
        HashSet<ModelInspectionStageStatus> statuses = [];
        bool sawFractionless = false;
        bool sawFractional = false;
        bool sawMaximum = false;

        foreach (ValidatedModelInspectionFixture fixture in
                 ModelInspectionFixtureTestCatalogue.All)
        {
            ModelInspectionFixtureExecutionPlan plan =
                ModelInspectionFixtureAdapter.CreatePlan(fixture.Input);
            ModelInspectionFixtureServiceStepDescriptor[] sourceSteps = fixture
                .Input.Attempts
                .SelectMany(attempt => attempt.ServiceSteps)
                .Where(step => step.Effect.Progress is not null)
                .ToArray();
            ModelInspectionProgress[] mapped = plan.Attempts
                .SelectMany(attempt => attempt.ServiceSteps)
                .Where(step => step.Progress is not null)
                .Select(step => step.Progress!)
                .ToArray();

            Assert.AreEqual(sourceSteps.Length, mapped.Length, fixture.Id);
            for (int index = 0; index < mapped.Length; index++)
            {
                ModelInspectionFixtureProgressDescriptor source =
                    sourceSteps[index].Effect.Progress!;
                ModelInspectionProgress actual = mapped[index];
                Assert.AreEqual((int)source.Stage, (int)actual.Stage, fixture.Id);
                Assert.AreEqual((int)source.Status, (int)actual.StageStatus, fixture.Id);
                Assert.AreEqual(source.CompletedStageCount,
                    actual.CompletedStageCount,
                    fixture.Id);
                Assert.AreEqual(5, actual.TotalStageCount, fixture.Id);
                Assert.AreEqual(source.Fraction, actual.StageFraction, fixture.Id);
                Assert.IsGreaterThan(0, actual.UserMessage.Length, fixture.Id);
                Assert.IsLessThanOrEqualTo(512, actual.UserMessage.Length, fixture.Id);
                ModelInspectionPagePresentation presentation =
                    CreateProgressPresentation(plan.Request, actual);
                Assert.AreEqual(
                    ModelInspectionFigmaState.InspectionProgress,
                    presentation.State,
                    fixture.Id);
                Assert.AreEqual(
                    actual.UserMessage,
                    presentation.RegionKeys.Progress.Detail,
                    fixture.Id);
                InspectionProgressRowsApplyResult applied =
                    presentation.ContentCard.ProgressRows.Apply(
                        presentation.ProgressRowsUpdate);
                Assert.IsFalse(applied.IsEmpty, fixture.Id);
                var currentRow = presentation.ContentCard.Items[
                    (int)actual.Stage - 1];
                string expectedFractionText =
                    actual.StageFraction is double fraction
                        ? $"{Math.Round(
                            fraction * 100d,
                            MidpointRounding.AwayFromZero):0}%"
                        : string.Empty;
                Assert.AreEqual(
                    actual.StageFraction,
                    currentRow.StageFraction,
                    fixture.Id);
                Assert.AreEqual(
                    expectedFractionText,
                    currentRow.StageFractionText,
                    fixture.Id);

                stages.Add(actual.Stage);
                statuses.Add(actual.StageStatus);
                sawFractionless |= actual.StageStatus ==
                    ModelInspectionStageStatus.Active &&
                    actual.StageFraction is null;
                sawFractional |= actual.StageFraction.HasValue;
                if (source.DetailProfile ==
                    ModelInspectionFixtureProgressDetailProfile.Maximum)
                {
                    sawMaximum = true;
                    Assert.AreEqual(512, actual.UserMessage.Length, fixture.Id);
                    Assert.IsTrue(actual.UserMessage.StartsWith(
                        "Inspection progress detail remains bounded",
                        StringComparison.Ordinal));
                }
            }
        }

        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionStage>(),
            stages.ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionStageStatus>(),
            statuses.ToArray());
        Assert.IsTrue(sawFractionless);
        Assert.IsTrue(sawFractional);
        Assert.IsTrue(sawMaximum);
    }

    [TestMethod]
    public void TerminalProfiles_AllPassProductionPresentationPreconditions()
    {
        HashSet<ModelInspectionExecutionStatus> statuses = [];
        foreach (ValidatedModelInspectionFixture fixture in
                 ModelInspectionFixtureTestCatalogue.All)
        {
            ModelInspectionFixtureExecutionPlan plan =
                ModelInspectionFixtureAdapter.CreatePlan(fixture.Input);
            foreach (ModelInspectionExecutionResult terminal in plan.Attempts
                         .SelectMany(attempt => attempt.ServiceSteps)
                         .Select(step => step.TerminalResult)
                         .OfType<ModelInspectionExecutionResult>())
            {
                ModelInspectionPagePresentation presentation =
                    CreatePresentation(plan.Request, terminal);
                statuses.Add(terminal.Status);
                Assert.AreNotEqual(
                    ModelInspectionFigmaState.InspectionProgress,
                    presentation.State,
                    fixture.Id);
                if (terminal.Status == ModelInspectionExecutionStatus.Cancelled)
                {
                    Assert.AreEqual(
                        ModelInspectionFigmaState.Cancelled,
                        presentation.State,
                        fixture.Id);
                }
                else if (terminal.Status ==
                         ModelInspectionExecutionStatus.OperationalFailure)
                {
                    Assert.AreEqual(
                        ModelInspectionFigmaState.OperationalFailure,
                        presentation.State,
                        fixture.Id);
                }
            }
        }

        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionExecutionStatus>(),
            statuses.ToArray());
    }

    [TestMethod]
    public void FailureProfiles_MapFixedSafeCodesAndBoundedDetail()
    {
        Dictionary<ModelInspectionFixtureFailureProfile, string> expectedCodes =
            new()
            {
                [ModelInspectionFixtureFailureProfile.WorkerStartFailure] =
                    "MI-OP-WORKER-START",
                [ModelInspectionFixtureFailureProfile.WorkerTimeout] =
                    "MI-OP-WORKER-TIMEOUT",
                [ModelInspectionFixtureFailureProfile.WorkerCrashEarlyExit] =
                    "MI-OP-WORKER-EARLY-EXIT",
                [ModelInspectionFixtureFailureProfile.MalformedWorkerResponse] =
                    "MI-OP-WORKER-INVALID-RESPONSE",
                [ModelInspectionFixtureFailureProfile.CancellationUnconfirmed] =
                    "MI-OP-CANCELLATION-UNCONFIRMED"
            };
        HashSet<ModelInspectionFixtureFailureProfile> seen = [];
        bool sawMaximum = false;

        foreach (ValidatedModelInspectionFixture fixture in
                 ModelInspectionFixtureTestCatalogue.All)
        {
            ModelInspectionFixtureExecutionPlan plan =
                ModelInspectionFixtureAdapter.CreatePlan(fixture.Input);
            ModelInspectionFixtureServiceStepDescriptor[] sourceSteps = fixture
                .Input.Attempts
                .SelectMany(attempt => attempt.ServiceSteps)
                .Where(step => step.Effect.FailureProfile.HasValue)
                .ToArray();
            ModelInspectionOperationalFailure[] mapped = plan.Attempts
                .SelectMany(attempt => attempt.ServiceSteps)
                .Where(step => step.TerminalResult?.Failure is not null)
                .Select(step => step.TerminalResult!.Failure!)
                .ToArray();
            Assert.AreEqual(sourceSteps.Length, mapped.Length, fixture.Id);

            for (int index = 0; index < mapped.Length; index++)
            {
                ModelInspectionFixtureFailureProfile profile =
                    sourceSteps[index].Effect.FailureProfile!.Value;
                ModelInspectionOperationalFailure actual = mapped[index];
                seen.Add(profile);
                Assert.AreEqual(expectedCodes[profile], actual.Code, fixture.Id);
                Assert.IsGreaterThan(0, actual.UserMessage.Length, fixture.Id);
                Assert.IsLessThanOrEqualTo(512, actual.UserMessage.Length, fixture.Id);
                Assert.IsGreaterThan(0, actual.TechnicalDetail.Length, fixture.Id);
                Assert.IsLessThanOrEqualTo(512, actual.TechnicalDetail.Length, fixture.Id);
                StringAssert.DoesNotContain(actual.UserMessage, @"C:\",
                    StringComparison.OrdinalIgnoreCase);
                StringAssert.DoesNotContain(actual.TechnicalDetail, @"C:\",
                    StringComparison.OrdinalIgnoreCase);

                if (sourceSteps[index].Effect.FailureDetailProfile ==
                    ModelInspectionFixtureFailureDetailProfile.Maximum)
                {
                    sawMaximum = true;
                    Assert.AreEqual(512, actual.UserMessage.Length, fixture.Id);
                    Assert.AreEqual(512, actual.TechnicalDetail.Length, fixture.Id);
                }
            }
        }

        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFixtureFailureProfile>(),
            seen.ToArray());
        Assert.IsTrue(sawMaximum);
    }

    [TestMethod]
    public void DeferredProfiles_MapOnlyDeclaredOwnerAndReleaseCheckpoint()
    {
        Dictionary<ModelInspectionFixtureDeferredEventKind,
            (string Capture, string Release)> expected =
            new()
            {
                [ModelInspectionFixtureDeferredEventKind.StaleProgress] =
                    ("capture-old-progress", "old-progress"),
                [ModelInspectionFixtureDeferredEventKind.StaleResultSnapshot] =
                    ("capture-old-result", "old-result"),
                [ModelInspectionFixtureDeferredEventKind.StaleMotion] =
                    ("capture-old-motion", "old-motion"),
                [ModelInspectionFixtureDeferredEventKind.StaleAnnouncement] =
                    ("capture-old-announcement", "old-announcement")
            };
        ModelInspectionFixtureDeferredEvent[] deferred =
            ModelInspectionFixtureTestCatalogue.All
                .Select(fixture => ModelInspectionFixtureAdapter.CreatePlan(
                    fixture.Input))
                .SelectMany(plan => plan.DeferredEvents)
                .ToArray();

        Assert.AreEqual(4, deferred.Length);
        CollectionAssert.AreEquivalent(
            expected.Keys.ToArray(),
            deferred.Select(value => value.Kind).ToArray());
        foreach (ModelInspectionFixtureDeferredEvent value in deferred)
        {
            Assert.AreEqual(1, value.OwnerAttempt);
            Assert.AreEqual(expected[value.Kind].Capture,
                value.CaptureCheckpoint);
            Assert.AreEqual(expected[value.Kind].Release,
                value.ReleaseCheckpoint);
        }
    }

    [TestMethod]
    public void DebugFixtures_SourceRecursivelyExcludesForbiddenIoAndComposition()
    {
        string runtimeDirectory = FindDebugFixturesSourceDirectory();
        string[] sources = Directory.GetFiles(
            runtimeDirectory,
            "*.cs",
            SearchOption.AllDirectories);
        Assert.IsGreaterThanOrEqualTo(5, sources.Length);
        string[] forbiddenSourceTokens =
        [
            "System.Diagnostics.Process",
            "ProcessStartInfo",
            "Process.Start",
            "File.",
            "Directory.",
            "FileStream",
            "FileInfo",
            "DirectoryInfo",
            "FileOpenPicker",
            "FolderPicker",
            "System.Net",
            "HttpClient",
            "Socket",
            "Task.Delay",
            "DispatcherQueueModelInspectionMilestoneScheduler",
            "ModelInspectionWorkerComposition",
            "ModelInspectionServiceComposition",
            "Task.Delay"
        ];
        foreach (string path in sources)
        {
            string source = File.ReadAllText(path);
            foreach (string forbidden in forbiddenSourceTokens)
            {
                Assert.IsFalse(
                    source.Contains(forbidden, StringComparison.Ordinal),
                    $"{Path.GetFileName(path)} contains forbidden token {forbidden}.");
            }
        }
    }

    [TestMethod]
    public void DebugFixtures_IlRecursivelyExcludesForbiddenIoAndComposition()
    {
        Type[] runtimeTypes = typeof(ModelInspectionFixtureAdapter).Assembly
            .GetTypes()
            .Where(type => IsDebugFixtureNamespace(type.Namespace))
            .ToArray();
        Assert.IsGreaterThanOrEqualTo(5, runtimeTypes.Length);
        foreach (MemberInfo referenced in IlReferences(runtimeTypes))
        {
            string identity =
                $"{referenced.DeclaringType?.FullName}.{referenced.Name}";
            Assert.IsFalse(
                IsForbiddenRuntimeReference(identity),
                $"Forbidden Debug runtime IL reference: {identity}");
        }

        Type[] constructionRoots = runtimeTypes
            .Concat(SelfAndNestedTypes(typeof(ModelInspectionPage)))
            .Distinct()
            .ToArray();
        (MethodBase Owner, ConstructorInfo Constructor)[] pageConstructions =
            NewObjectConstructors(constructionRoots)
                .Where(value => value.Constructor.DeclaringType ==
                    typeof(ModelInspectionPage))
                .ToArray();
        Assert.AreEqual(
            1,
            pageConstructions.Length,
            "The Debug closure must construct ModelInspectionPage exactly once through CreateForFixture.");
        foreach ((MethodBase owner, ConstructorInfo constructor) in
                 pageConstructions)
        {
            Assert.AreEqual("CreateForFixture", owner.Name);
            Assert.AreEqual(typeof(ModelInspectionPage), owner.DeclaringType);
            Assert.IsTrue(
                IsApprovedModelInspectionPageConstructor(constructor),
                $"{owner.DeclaringType?.FullName}.{owner.Name} creates " +
                $"ModelInspectionPage through unapproved constructor " +
                $"{constructor}.");
        }

        Assert.IsTrue(IsApprovedModelInspectionPageConstructor(
            typeof(ModelInspectionPage),
            isPrivate: true,
            [
                typeof(IModelInspectionService),
                typeof(Func<IModelInspectionRenderDispatcher>),
                typeof(Func<
                    IModelInspectionRenderDispatcher,
                    IModelInspectionStartupPresentationBarrier>),
                typeof(Func<IModelInspectionAnimationDriver>),
                typeof(Func<IModelInspectionMotionSettings>),
                typeof(Func<IModelInspectionMilestoneScheduler>),
                typeof(bool),
                typeof(Action<ResourceDictionary>)
            ]));
        ConstructorInfo? zeroArgument = typeof(ModelInspectionPage)
            .GetConstructor(Type.EmptyTypes);
        Assert.IsNotNull(zeroArgument);
        Assert.IsFalse(IsApprovedModelInspectionPageConstructor(zeroArgument));
    }

    private static ModelInspectionExecutionResult FindCompleted(
        ModelInspectionFixtureExecutionPlan plan) =>
        plan.Attempts
            .SelectMany(attempt => attempt.ServiceSteps)
            .Select(step => step.TerminalResult)
            .First(result => result?.Status ==
                ModelInspectionExecutionStatus.Completed)!;

    private static ModelInspectionPagePresentation CreatePresentation(
        ModelInspectionRequest request,
        ModelInspectionExecutionResult result)
    {
        DelegateCommand command = new(_ => { }, _ => true);
        return ModelInspectionPresentationFactory.Create(
            request,
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, 1),
                isRunActive: false,
                isCancellationRequested: false,
                progress: null,
                terminalResult: result),
            new ModelInspectionPresentationCommands(command, command, command),
            isDisclosureExpanded: false,
            new InspectionProgressRows());
    }

    private static ModelInspectionPagePresentation CreateProgressPresentation(
        ModelInspectionRequest request,
        ModelInspectionProgress progress)
    {
        DelegateCommand command = new(_ => { }, _ => true);
        var progressRows = new InspectionProgressRows();
        progressRows.Reset(new ModelInspectionRenderKey(1, 0));
        return ModelInspectionPresentationFactory.Create(
            request,
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, 1),
                isRunActive: true,
                isCancellationRequested: false,
                progress,
                terminalResult: null),
            new ModelInspectionPresentationCommands(command, command, command),
            isDisclosureExpanded: false,
            progressRows);
    }

    private static ModelInspectionPagePresentation CreateInitialPresentation(
        ModelInspectionRequest request)
    {
        DelegateCommand command = new(_ => { }, _ => true);
        return ModelInspectionPresentationFactory.Create(
            request,
            new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(1, 0),
                isRunActive: true,
                isCancellationRequested: false,
                progress: null,
                terminalResult: null),
            new ModelInspectionPresentationCommands(command, command, command),
            isDisclosureExpanded: false,
            new InspectionProgressRows());
    }

    private static ModelInspectionFigmaState InferCollapsedState(
        ModelInspectionExecutionResult terminal) => terminal.Status switch
        {
            ModelInspectionExecutionStatus.Cancelled =>
                ModelInspectionFigmaState.Cancelled,
            ModelInspectionExecutionStatus.OperationalFailure =>
                ModelInspectionFigmaState.OperationalFailure,
            ModelInspectionExecutionStatus.Completed => terminal.Result?.Outcome switch
            {
                ModelInspectionOutcome.Ready =>
                    ModelInspectionFigmaState.ReadyCollapsed,
                ModelInspectionOutcome.ReadyWithWarnings =>
                    ModelInspectionFigmaState.ReadyWithWarningsCollapsed,
                ModelInspectionOutcome.ConversionRequired =>
                    ModelInspectionFigmaState.ConversionRequiredCollapsed,
                ModelInspectionOutcome.IncompletePackage =>
                    ModelInspectionFigmaState.IncompletePackage,
                ModelInspectionOutcome.Unsupported =>
                    ModelInspectionFigmaState.Unsupported,
                ModelInspectionOutcome.Invalid =>
                    ModelInspectionFigmaState.InvalidCollapsed,
                _ => throw new AssertFailedException(
                    "A completed fixture result has no inferable outcome.")
            },
            _ => throw new AssertFailedException(
                "A fixture result has no inferable terminal state.")
        };

    private static string ComputeCanonicalPathSha256(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string canonicalPath = OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath)))
            .ToLowerInvariant();
    }

    private static void AssertApprovedRuntimeAndTokenizer(
        ModelInspectionEvidence evidence,
        string id)
    {
        Assert.AreEqual("GraniteEdgeAI.ModelInspection.Worker",
            evidence.Runtime.WorkerId,
            id);
        Assert.AreEqual("1.0.0", evidence.Runtime.WorkerVersion, id);
        Assert.AreEqual(1, evidence.Runtime.ProtocolVersion, id);
        Assert.AreEqual(
            "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
            evidence.Runtime.RuntimeProfile,
            id);
        Assert.AreEqual("0.27.0", evidence.Runtime.LLamaSharpVersion, id);
        Assert.AreEqual("0.27.0", evidence.Runtime.BackendPackageVersion, id);
        Assert.AreEqual(
            "3f7c29d318e317b63f54c558bc69803963d7d88c",
            evidence.Runtime.MappedLlamaCppCommit,
            true,
            id);
        Assert.AreEqual("llama.dll", evidence.Runtime.NativeLibraryName, id);
        Assert.AreEqual("X64", evidence.Runtime.ProcessArchitecture, id);
        Assert.AreEqual("VocabOnly", evidence.Runtime.InspectionMode, id);
        Assert.IsFalse(evidence.Runtime.UsesCuda, id);
        Assert.IsFalse(evidence.Runtime.UsesVulkan, id);
        Assert.AreEqual(0, evidence.Runtime.GpuLayerCount, id);
        Assert.AreEqual(true, evidence.Tokenizer.TokenizerSmokePassed, id);
        Assert.AreEqual(4, evidence.Tokenizer.TokenizerSmokeTokenCount, id);
    }

    private static string FindDebugFixturesSourceDirectory(
        [CallerFilePath] string sourcePath = "")
    {
        DirectoryInfo? cursor = new FileInfo(sourcePath).Directory;
        while (cursor is not null && !File.Exists(Path.Combine(
                   cursor.FullName,
                   "IBM Granite with TurboQuant (Intel).slnx")))
        {
            cursor = cursor.Parent;
        }

        Assert.IsNotNull(cursor, "Repository root was not found.");
        return Path.Combine(
            cursor.FullName,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "DebugFixtures");
    }

    private static bool IsApprovedModelInspectionPageConstructor(
        ConstructorInfo constructor) =>
        IsApprovedModelInspectionPageConstructor(
            constructor.DeclaringType,
            constructor.IsPrivate,
            constructor
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());

    private static bool IsApprovedModelInspectionPageConstructor(
        Type? declaringType,
        bool isPrivate,
        IReadOnlyList<Type> parameterTypes)
    {
        Type[] approvedParameters =
        [
            typeof(IModelInspectionService),
            typeof(Func<IModelInspectionRenderDispatcher>),
            typeof(Func<
                IModelInspectionRenderDispatcher,
                IModelInspectionStartupPresentationBarrier>),
            typeof(Func<IModelInspectionAnimationDriver>),
            typeof(Func<IModelInspectionMotionSettings>),
            typeof(Func<IModelInspectionMilestoneScheduler>),
            typeof(bool),
            typeof(Action<ResourceDictionary>)
        ];
        return declaringType == typeof(ModelInspectionPage) &&
            isPrivate &&
            parameterTypes.SequenceEqual(approvedParameters);
    }

    private static string[] FindForbiddenAdapterDependencies(Type adapterType)
    {
        const BindingFlags AllDeclared = BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;
        List<string> violations = [];

        foreach (FieldInfo field in adapterType.GetFields(AllDeclared))
        {
            AddIfForbidden(field, field.FieldType, violations);
        }

        foreach (PropertyInfo property in adapterType.GetProperties(AllDeclared))
        {
            AddIfForbidden(property, property.PropertyType, violations);
            foreach (ParameterInfo parameter in property.GetIndexParameters())
            {
                AddIfForbidden(property, parameter.ParameterType, violations);
            }
        }

        foreach (EventInfo eventInfo in adapterType.GetEvents(AllDeclared))
        {
            if (eventInfo.EventHandlerType is not null)
            {
                AddIfForbidden(eventInfo, eventInfo.EventHandlerType, violations);
            }
        }

        foreach (MethodInfo method in adapterType.GetMethods(AllDeclared))
        {
            AddIfForbidden(method, method.ReturnType, violations);
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AddIfForbidden(method, parameter.ParameterType, violations);
            }
        }

        foreach (ConstructorInfo constructor in
                 adapterType.GetConstructors(AllDeclared))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                AddIfForbidden(constructor,
                    parameter.ParameterType,
                    violations);
            }
        }

        return violations.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddIfForbidden(
        MemberInfo member,
        Type candidate,
        ICollection<string> violations)
    {
        if (ContainsForbiddenAdapterType(candidate))
        {
            violations.Add(
                $"{member.DeclaringType?.FullName}.{member.Name}:" +
                candidate.FullName);
        }
    }

    private static bool ContainsForbiddenAdapterType(Type candidate)
    {
        if (candidate.IsByRef || candidate.IsPointer || candidate.IsArray)
        {
            return ContainsForbiddenAdapterType(candidate.GetElementType()!);
        }

        Type definition = candidate.IsGenericType
            ? candidate.GetGenericTypeDefinition()
            : candidate;
        if (definition == typeof(ValidatedModelInspectionFixture) ||
            definition == typeof(ModelInspectionFixtureDescriptor) ||
            definition.Name.StartsWith(
                "ModelInspectionExpected",
                StringComparison.Ordinal))
        {
            return true;
        }

        return candidate.IsGenericType && candidate
            .GetGenericArguments()
            .Any(ContainsForbiddenAdapterType);
    }

    private static IEnumerable<Type> SelfAndNestedTypes(Type root)
    {
        yield return root;
        foreach (Type nested in root.GetNestedTypes(
                     BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (Type candidate in SelfAndNestedTypes(nested))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<(
        MethodBase Owner,
        ConstructorInfo Constructor)> NewObjectConstructors(
        IEnumerable<Type> types)
    {
        foreach (Type type in types)
        {
            foreach (MethodBase method in type
                         .GetMethods(BindingFlags.Instance |
                             BindingFlags.Static |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly)
                         .Cast<MethodBase>()
                         .Concat(type.GetConstructors(BindingFlags.Instance |
                             BindingFlags.Static |
                             BindingFlags.Public |
                             BindingFlags.NonPublic)))
            {
                MethodBody? body = method.GetMethodBody();
                if (body?.GetILAsByteArray() is not byte[] il)
                {
                    continue;
                }

                foreach ((OpCode opcode, int token) in
                         ReadMemberTokenInstructions(il))
                {
                    if (opcode != OpCodes.Newobj)
                    {
                        continue;
                    }

                    MemberInfo? member = null;
                    try
                    {
                        member = method.Module.ResolveMember(
                            token,
                            type.IsGenericType
                                ? type.GetGenericArguments()
                                : null,
                            method.IsGenericMethod
                                ? method.GetGenericArguments()
                                : null);
                    }
                    catch (ArgumentException)
                    {
                        // Non-member metadata tokens are irrelevant here.
                    }

                    if (member is ConstructorInfo constructor)
                    {
                        yield return (method, constructor);
                    }
                }
            }
        }
    }

    private static IEnumerable<MemberInfo> IlReferences(
        IEnumerable<Type> types)
    {
        foreach (Type type in types)
        {
            foreach (MethodBase method in type
                         .GetMethods(BindingFlags.Instance |
                             BindingFlags.Static |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly)
                         .Cast<MethodBase>()
                         .Concat(type.GetConstructors(BindingFlags.Instance |
                             BindingFlags.Static |
                             BindingFlags.Public |
                             BindingFlags.NonPublic)))
            {
                MethodBody? body = method.GetMethodBody();
                if (body?.GetILAsByteArray() is not byte[] il)
                {
                    continue;
                }

                foreach (int token in ReadMemberTokens(il))
                {
                    MemberInfo? member = null;
                    try
                    {
                        member = method.Module.ResolveMember(
                            token,
                            type.IsGenericType
                                ? type.GetGenericArguments()
                                : null,
                            method.IsGenericMethod
                                ? method.GetGenericArguments()
                                : null);
                    }
                    catch (ArgumentException)
                    {
                        // Non-member metadata tokens are irrelevant here.
                    }

                    if (member is not null)
                    {
                        yield return member;
                    }
                }
            }
        }
    }

    private static IEnumerable<MemberInfo> ReachableIlReferences(
        MethodInfo root)
    {
        Queue<MethodInfo> pending = new([root]);
        HashSet<MethodInfo> visited = [];
        while (pending.TryDequeue(out MethodInfo? method))
        {
            if (!visited.Add(method))
            {
                continue;
            }

            foreach (MemberInfo referenced in IlReferencesFor(method))
            {
                yield return referenced;
                if (referenced is MethodInfo called &&
                    called.DeclaringType == root.DeclaringType)
                {
                    pending.Enqueue(called);
                }
            }
        }
    }

    private static IEnumerable<MemberInfo> IlReferencesFor(MethodBase method)
    {
        Type declaringType = method.DeclaringType ??
            throw new InvalidOperationException(
                "IL method has no declaring type.");
        MethodBody? body = method.GetMethodBody();
        if (body?.GetILAsByteArray() is not byte[] il)
        {
            yield break;
        }

        foreach (int token in ReadMemberTokens(il))
        {
            MemberInfo? member = null;
            try
            {
                member = method.Module.ResolveMember(
                    token,
                    declaringType.IsGenericType
                        ? declaringType.GetGenericArguments()
                        : null,
                    method.IsGenericMethod
                        ? method.GetGenericArguments()
                        : null);
            }
            catch (ArgumentException)
            {
                // Non-member metadata tokens are irrelevant here.
            }

            if (member is not null)
            {
                yield return member;
            }
        }
    }

    private static IEnumerable<int> ReadMemberTokens(byte[] il)
    {
        foreach ((OpCode _, int token) in ReadMemberTokenInstructions(il))
        {
            yield return token;
        }
    }

    private static IEnumerable<(OpCode OpCode, int Token)>
        ReadMemberTokenInstructions(byte[] il)
    {
        int offset = 0;
        while (offset < il.Length)
        {
            OpCode opcode = il[offset++] == 0xfe
                ? MultiByteOpCodes[il[offset++]]
                : SingleByteOpCodes[il[offset - 1]];
            int size = OperandSize(opcode.OperandType, il, offset);
            if (opcode.OperandType is OperandType.InlineMethod or
                OperandType.InlineField or
                OperandType.InlineType or
                OperandType.InlineTok)
            {
                yield return (opcode, BitConverter.ToInt32(il, offset));
            }

            offset += size;
        }
    }

    private static int OperandSize(
        OperandType operandType,
        byte[] il,
        int offset) => operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or
            OperandType.ShortInlineI or
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or
            OperandType.InlineField or
            OperandType.InlineI or
            OperandType.InlineMethod or
            OperandType.InlineSig or
            OperandType.InlineString or
            OperandType.InlineTok or
            OperandType.InlineType or
            OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                4 + (BitConverter.ToInt32(il, offset) * 4),
            _ => throw new InvalidOperationException(
                $"Unknown IL operand type {operandType}.")
        };

    private static bool IsForbiddenRuntimeReference(string identity) =>
        identity.StartsWith("System.Diagnostics.Process", StringComparison.Ordinal) ||
        identity.StartsWith("System.IO.File", StringComparison.Ordinal) ||
        identity.StartsWith("System.IO.Directory", StringComparison.Ordinal) ||
        identity.StartsWith("System.Net.", StringComparison.Ordinal) ||
        identity.Equals(
            "System.Threading.Tasks.Task.Delay",
            StringComparison.Ordinal) ||
        identity.Contains("FileOpenPicker", StringComparison.Ordinal) ||
        identity.Contains("FolderPicker", StringComparison.Ordinal) ||
        identity.Contains(
            "DispatcherQueueModelInspectionMilestoneScheduler",
            StringComparison.Ordinal) ||
        identity.Contains("ModelInspectionWorkerComposition", StringComparison.Ordinal) ||
        identity.Contains("ModelInspectionServiceComposition", StringComparison.Ordinal);

    private static bool IsDebugFixtureNamespace(string? value)
    {
        const string Root =
            "GraniteEdgeAI.Features.ModelInspection.DebugFixtures";
        return string.Equals(value, Root, StringComparison.Ordinal) ||
            value?.StartsWith(Root + ".", StringComparison.Ordinal) is true;
    }

    private sealed class ForbiddenAdapterShape
    {
        internal ValidatedModelInspectionFixture? FullFixtureField;

        internal ForbiddenAdapterShape(
            ModelInspectionFixtureDescriptor descriptor,
            ValidatedModelInspectionFixture fixture)
        {
            _ = descriptor;
            FullFixtureField = fixture;
        }

        internal ModelInspectionExpectedScreen? ExpectedProperty { get; set; }

        internal ModelInspectionExpectedAction LeakExpected(
            ValidatedModelInspectionFixture fixture) =>
            throw new NotSupportedException(fixture.Id);
    }

    private static readonly OpCode[] SingleByteOpCodes = BuildOpCodes(false);

    private static readonly OpCode[] MultiByteOpCodes = BuildOpCodes(true);

    private static OpCode[] BuildOpCodes(bool multiByte)
    {
        OpCode[] result = new OpCode[256];
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opcode)
            {
                continue;
            }

            ushort value = unchecked((ushort)opcode.Value);
            if (multiByte == (value > byte.MaxValue))
            {
                result[value & byte.MaxValue] = opcode;
            }
        }

        return result;
    }
}

internal static class ModelInspectionFixtureTestCatalogue
{
    private static readonly Lazy<ModelInspectionFixtureCatalogue> Catalogue =
        new(LoadCatalogue, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static IReadOnlyList<ValidatedModelInspectionFixture> All =>
        Catalogue.Value.Fixtures;

    internal static ValidatedModelInspectionFixture Get(string id) =>
        Catalogue.Value.Fixtures.Single(fixture => fixture.Id == id);

    private static ModelInspectionFixtureCatalogue LoadCatalogue()
    {
        string root = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures");
        ModelInspectionFixtureDocumentSource schemaSource = Read(
            root,
            "model-inspection-fixture.schema.json");
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(schemaSource);
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(
                Read(root,
                    "model-inspection-fixture-coverage-policy.json"),
                schema);
        ModelInspectionFixtureDocumentSource[] descriptors = Directory
            .GetFiles(root, "MI-*.fixture.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new ModelInspectionFixtureDocumentSource(
                Path.GetFileName(path),
                File.ReadAllBytes(path)))
            .ToArray();
        return ModelInspectionFixtureCatalogue.LoadDescriptors(
            descriptors,
            policy,
            schema);
    }

    private static ModelInspectionFixtureDocumentSource Read(
        string root,
        string fileName) => new(
            fileName,
            File.ReadAllBytes(Path.Combine(root, fileName)));
}
#endif
