#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using System.Reflection.Emit;
using System.ComponentModel;
using System.Text;
using DebugFixturePreset = GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets.ModelInspectionFixturePreset;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
public sealed class ModelInspectionFixtureGalleryTests
{
    private const string PackageRoot =
        "ms-appx:///Fixtures/";

    [TestMethod]
    public async Task PackageLoader_ReadsFixedCatalogueOnceAndPublishesCoverage()
    {
        CountingReader reader = CountingReader.Valid();
        var loader = new ModelInspectionFixturePackageLoader(reader);

        ValidatedModelInspectionFixtureCoverageCatalogue result =
            await loader.LoadAsync();

        Assert.AreEqual(49, result.Catalogue.Fixtures.Count);
        CollectionAssert.AreEqual(ExpectedPackageUris(), reader.Requests);
        Assert.AreEqual(51, reader.Requests.Count);
        Assert.IsTrue(reader.Requests.All(uri => reader.Count(uri) == 1));
        Assert.AreSame(result, loader.CoverageCatalogue);
    }

    [TestMethod]
    public async Task PackageLoader_SequentialAndOverlappingCallersShareOneReadOnceCatalogue()
    {
        var reader = new GatedCatalogueReader();
        var loader = new ModelInspectionFixturePackageLoader(reader);
        Task<ValidatedModelInspectionFixtureCoverageCatalogue> first =
            loader.LoadAsync();
        await reader.FirstReadStarted.WaitAsync(TimeSpan.FromSeconds(10));
        Task<ValidatedModelInspectionFixtureCoverageCatalogue> overlapping =
            loader.LoadAsync();
        Assert.IsFalse(overlapping.IsCompleted,
            "The overlapping caller must join the in-flight atomic load.");

        reader.ReleaseFirstRead();
        ValidatedModelInspectionFixtureCoverageCatalogue[] concurrent =
            await Task.WhenAll(first, overlapping);
        ValidatedModelInspectionFixtureCoverageCatalogue sequential =
            await loader.LoadAsync();

        Assert.AreSame(concurrent[0], concurrent[1]);
        Assert.AreSame(concurrent[0], sequential);
        Assert.AreSame(concurrent[0], loader.CoverageCatalogue);
        CollectionAssert.AreEqual(ExpectedPackageUris(),
            reader.Requests.ToArray());
        Assert.AreEqual(51, reader.Requests.Count);
        Assert.IsTrue(reader.Requests.All(uri => reader.Count(uri) == 1));
    }

    [TestMethod]
    public async Task PackageLoader_InvalidDocumentFailsAtomicallyWithSafeDiagnostic()
    {
        CountingReader reader = CountingReader.Valid();
        string fixtureUri = PackageRoot +
            "MI-049-operational-failure-detail-copy-maximum.fixture.json";
        reader.Replace(fixtureUri, Encoding.UTF8.GetBytes("{}"));
        var loader = new ModelInspectionFixturePackageLoader(reader);

        ModelInspectionFixtureGalleryLoadException error =
            await Assert.ThrowsExactlyAsync<
                ModelInspectionFixtureGalleryLoadException>(
                () => loader.LoadAsync());

        Assert.IsNull(loader.CoverageCatalogue);
        Assert.AreEqual(51, reader.Requests.Count);
        StringAssert.Contains(error.Diagnostic, "MI-049");
        Assert.IsFalse(error.Diagnostic.Contains("C:\\", StringComparison.Ordinal));
        Assert.IsFalse(error.Diagnostic.Contains("{"));
    }

    [DataTestMethod]
    [DataRow("schema")]
    [DataRow("policy")]
    [DataRow("descriptor")]
    [DataRow("coverage")]
    [DataRow("read")]
    public async Task PackageLoader_EveryBoundaryFailurePublishesNothing(
        string failure)
    {
        CountingReader reader = CountingReader.Valid();
        string schemaUri = ExpectedPackageUris()[0];
        string policyUri = ExpectedPackageUris()[1];
        string fixtureUri = PackageRoot +
            "MI-049-operational-failure-detail-copy-maximum.fixture.json";
        switch (failure)
        {
            case "schema":
                reader.Replace(schemaUri, Encoding.UTF8.GetBytes("{}"));
                break;
            case "policy":
                reader.Replace(policyUri, Encoding.UTF8.GetBytes("{}"));
                break;
            case "descriptor":
                reader.Replace(fixtureUri, Encoding.UTF8.GetBytes("{}"));
                break;
            case "coverage":
                reader.ReplaceText(
                    fixtureUri,
                    "MI-049 operational failure detail copy maximum",
                    "MI-049 altered but structurally valid title");
                break;
            case "read":
                reader.ThrowOn(fixtureUri);
                break;
            default:
                Assert.Fail($"Unknown mutation {failure}.");
                break;
        }

        var loader = new ModelInspectionFixturePackageLoader(reader);
        await Assert.ThrowsExactlyAsync<
            ModelInspectionFixtureGalleryLoadException>(
                () => loader.LoadAsync());

        Assert.IsNull(loader.CoverageCatalogue);
        Assert.IsTrue(reader.Requests.Count <= 51);
        Assert.IsTrue(reader.Requests.All(uri => reader.Count(uri) == 1));
    }

    [TestMethod]
    public void PackageLoader_IlClosesTheExactValidationPipeline()
    {
        MemberInfo[] references = ReachableIlReferences(
            typeof(ModelInspectionFixturePackageLoader)).ToArray();
        Assert.IsTrue(references.Any(reference =>
            reference.DeclaringType == typeof(ModelInspectionFixtureCatalogue) &&
            reference.Name == nameof(ModelInspectionFixtureCatalogue.VerifySchema)));
        Assert.IsTrue(references.Any(reference =>
            reference.DeclaringType == typeof(ModelInspectionFixtureCatalogue) &&
            reference.Name == nameof(ModelInspectionFixtureCatalogue.LoadPolicy)));
        Assert.IsTrue(references.Any(reference =>
            reference.DeclaringType == typeof(ModelInspectionFixtureCatalogue) &&
            reference.Name == nameof(ModelInspectionFixtureCatalogue.LoadDescriptors)));
        Assert.IsTrue(references.Any(reference =>
            reference.DeclaringType ==
                typeof(ModelInspectionFixtureCoverageValidator) &&
            reference.Name == nameof(ModelInspectionFixtureCoverageValidator.Validate)));
    }

    [DataTestMethod]
    [DataRow("ms-appx:///Fixtures/MI-001-inspection-progress-initial.fixture.json?x=1")]
    [DataRow("ms-appx:///Fixtures/MI-001-inspection-progress-initial.fixture.json#x")]
    [DataRow("ms-appx:///fixtures/MI-001-inspection-progress-initial.fixture.json")]
    [DataRow("ms-appx:///Fixtures\\MI-001-inspection-progress-initial.fixture.json")]
    [DataRow("ms-appx:///Fixtures/%4dI-001-inspection-progress-initial.fixture.json")]
    [DataRow("ms-appx:///Fixtures/not-listed.fixture.json")]
    public async Task PackageReader_RejectsEveryNonCanonicalUri(string value)
    {
        var reader = new ModelInspectionFixturePackageResourceReader();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            reader.ReadAsync(new Uri(value)));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow((256 * 1024) + 1)]
    public async Task PackageLoader_RejectsUnsafeResourceLength(int length)
    {
        CountingReader reader = CountingReader.Valid();
        reader.Replace(ExpectedPackageUris()[0], new byte[length]);
        var loader = new ModelInspectionFixturePackageLoader(reader);

        await Assert.ThrowsExactlyAsync<
            ModelInspectionFixtureGalleryLoadException>(
                () => loader.LoadAsync());

        Assert.IsNull(loader.CoverageCatalogue);
    }

    [TestMethod]
    public async Task PackageLoader_CancellationOnFinalReadPreventsCachePublication()
    {
        using var cancellation = new CancellationTokenSource();
        var reader = new CancelOnFinalReadReader(cancellation);
        var loader = new ModelInspectionFixturePackageLoader(reader);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            loader.LoadAsync(cancellation.Token));

        Assert.AreEqual(51, reader.ReadCount);
        Assert.IsNull(loader.CoverageCatalogue);
    }

    [TestMethod]
    public async Task PackageLoader_CancellationAfterValidationPreventsCachePublication()
    {
        using var cancellation = new CancellationTokenSource();
        int publicationBoundaries = 0;
        var loader = new ModelInspectionFixturePackageLoader(
            CountingReader.Valid(),
            beforeCoveragePublication: () =>
            {
                Assert.AreEqual(1, Interlocked.Increment(
                    ref publicationBoundaries));
                cancellation.Cancel();
            });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            loader.LoadAsync(cancellation.Token));

        Assert.AreEqual(1, publicationBoundaries);
        Assert.IsNull(loader.CoverageCatalogue,
            "Cancellation at the final publication boundary must not cache coverage.");
    }

    [DataTestMethod]
    [DataRow("..\\Arian\\private.fixture.json")]
    [DataRow("C:\\Users\\Arian\\private.fixture.json")]
    [DataRow("oversize")]
    public async Task PackageLoader_RevalidationRejectsUnsafeFilenameWithoutEcho(
        string value)
    {
        string rejected = string.Equals(value, "oversize", StringComparison.Ordinal)
            ? new string('x', 300)
            : value;
        var loader = new ModelInspectionFixturePackageLoader(
            CountingReader.Valid());
        await loader.LoadAsync();

        ModelInspectionFixtureGalleryLoadException error =
            Assert.ThrowsExactly<ModelInspectionFixtureGalleryLoadException>(() =>
                loader.RevalidateDescriptor(rejected, new byte[] { (byte)'{' }));

        Assert.AreEqual(
            "<invalid-filename>|$|revalidation.boundary",
            error.Diagnostic);
        Assert.IsFalse(error.Diagnostic.Contains(rejected, StringComparison.Ordinal));
        Assert.IsFalse(error.Message.Contains(rejected, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ViewModel_SortsFiltersAndLinksOnlyDeclaredN001Fixtures()
    {
        var viewModel = new ModelInspectionFixtureGalleryViewModel();
        await viewModel.LoadAsync(new ModelInspectionFixturePackageLoader(
            CountingReader.Valid()));

        Assert.AreEqual(49, viewModel.Items.Count);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 49).Select(index => $"MI-{index:000}").ToArray(),
            viewModel.Items.Select(item => item.Id).ToArray());
        Assert.AreEqual("Catalogue validated: 49 fixtures.",
            viewModel.ValidationStatus);
        Assert.IsTrue(viewModel.Items.Single(item => item.Id == "MI-002")
            .HasN001RealWorkerCoverage);
        Assert.IsTrue(viewModel.Items.Single(item => item.Id == "MI-003")
            .HasN001RealWorkerCoverage);
        Assert.AreEqual(2, viewModel.Items.Count(item =>
            item.HasN001RealWorkerCoverage));

        viewModel.SearchText = "MI-043";
        Assert.AreEqual("MI-043", viewModel.FilteredItems.Single().Id);
        viewModel.SearchText = "maximum-collapsed.fixture.json";
        Assert.AreEqual("MI-043", viewModel.FilteredItems.Single().Id);
        viewModel.SearchText = "worker-timeout";
        Assert.AreEqual("MI-039", viewModel.FilteredItems.Single().Id);
        viewModel.SearchText = "Granite Fixture Model";
        Assert.AreEqual(0, viewModel.FilteredItems.Count,
            "Search must not accidentally include fixture title/input copy.");
        viewModel.SearchText = string.Empty;
        viewModel.SelectedCategory = ModelInspectionFixtureCategory.Stress;
        Assert.IsTrue(viewModel.FilteredItems.Count > 0);
        Assert.IsTrue(viewModel.FilteredItems.All(item =>
            item.Category == ModelInspectionFixtureCategory.Stress));
    }

    [TestMethod]
    public void GalleryContract_ExposesBrandedCatalogueAndClosedPreset()
    {
        Assert.AreEqual(
            typeof(ValidatedModelInspectionFixtureCoverageCatalogue),
            typeof(ModelInspectionFixturePackageLoader)
                .GetMethod(
                    "LoadAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .ReturnType
                .GetGenericArguments()
                .Single());

        var preset = DebugFixturePreset.Canonical;
        Assert.AreEqual(ModelInspectionFixtureWidthProfile.Desktop1440,
            preset.Width);
        Assert.AreEqual(ModelInspectionFixtureResourceProfile.Light,
            preset.Resources);
        Assert.AreEqual(ModelInspectionFixtureTextProfile.Standard100,
            preset.Text);
        Assert.AreEqual(ModelInspectionFixtureMotionProfile.Normal,
            preset.Motion);
    }

    [TestMethod]
    public void GallerySelection_UsesExactMi001LoadedRuleForAllFixtures()
    {
        ValidatedModelInspectionFixture[] fixtures = LoadCatalogue()
            .Catalogue.Fixtures.ToArray();

        Assert.AreEqual(49, fixtures.Length);
        foreach (ValidatedModelInspectionFixture fixture in fixtures)
        {
            Assert.AreEqual(
                !string.Equals(fixture.Id, "MI-001", StringComparison.Ordinal),
                ModelInspectionFixtureGalleryPage
                    .ShouldStartInspectionOnLoaded(fixture),
                fixture.Id);
        }
    }

    [UITestMethod]
    public void HostActivation_CarriesTheExactCanonicalPreset()
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == "MI-002");
        var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: true);
        var host = new ModelInspectionFixtureHostPage();
        var activation = new ModelInspectionFixtureHostActivation(
            fixture.Input,
            session,
            startInspectionOnLoaded: true,
            preset: DebugFixturePreset.Canonical);

        host.ActivateForTesting(activation);

        Assert.AreSame(DebugFixturePreset.Canonical, host.Preset);
        host.RetireForTesting();
    }

    [TestMethod]
    public void ScenarioRunner_HasInputOnlySurfaceAndNoExpectedClosure()
    {
        Type runner = typeof(ModelInspectionFixtureScenarioRunner);
        Type[] forbiddenRoots =
        [
            typeof(ValidatedModelInspectionFixture),
            typeof(ModelInspectionFixtureDescriptor),
            typeof(ModelInspectionExpectedScreen),
            typeof(ModelInspectionPresetExpectation)
        ];
        foreach (Type candidate in SelfAndNested(runner))
        {
            foreach (Type referenced in DeclaredSignatureTypes(candidate))
            {
                Assert.IsFalse(forbiddenRoots.Any(root =>
                    ContainsType(referenced, root)),
                    $"Runner signature leaked {referenced.FullName}.");
            }
        }

        MethodInfo run = runner.GetMethod("RunAsync",
            BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic)!;
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(ValidatedModelInspectionFixtureInput),
                typeof(ModelInspectionPage),
                typeof(ModelInspectionFixtureSession),
                typeof(CancellationToken)
            },
            run.GetParameters().Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [UITestMethod]
    [DataRow("MI-001", 0, 0, 0, 0, 0, 0)]
    [DataRow("MI-003", 1, 1, 0, 0, 0, 0)]
    [DataRow("MI-024", 1, 1, 0, 0, 0, 0)]
    [DataRow("MI-031", 2, 2, 0, 0, 0, 0)]
    [DataRow("MI-032", 2, 2, 0, 0, 0, 0)]
    [DataRow("MI-033", 2, 3, 1, 0, 0, 0)]
    [DataRow("MI-034", 2, 3, 0, 1, 0, 0)]
    [DataRow("MI-035", 2, 3, 0, 0, 1, 0)]
    [DataRow("MI-036", 2, 3, 0, 0, 0, 1)]
    public async Task ScenarioRunner_RoutesRealSetupAndStopsAtObservation(
        string id,
        int serviceCalls,
        int serviceCheckpoints,
        int staleProgress,
        int staleResult,
        int staleMotion,
        int staleAnnouncement)
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == id);
        var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input,
            session,
            startInspectionOnLoaded: id != "MI-001"));
        Window window = await ShowHostAsync(host);
        try
        {
            string checkpoint = await new ModelInspectionFixtureScenarioRunner()
                .RunAsync(
                    fixture.Input,
                    host.ModelInspectionPage!,
                    session,
                    CancellationToken.None);

            Assert.AreEqual(fixture.Input.ObservationCheckpoint, checkpoint);
            Assert.AreEqual(serviceCalls, session.Evidence.ServiceCallCount);
            Assert.AreEqual(serviceCheckpoints,
                session.Evidence.ReleasedServiceCheckpoints.Count);
            Assert.AreEqual(staleProgress,
                session.Evidence.ReleasedDeferredProgressCount);
            Assert.AreEqual(staleResult,
                session.Evidence.ReleasedDeferredResultSnapshotCount);
            Assert.AreEqual(staleMotion,
                session.Evidence.ReleasedDeferredMotionCount);
            Assert.AreEqual(staleAnnouncement,
                session.Evidence.ReleasedDeferredAnnouncementCount);

            if (id == "MI-003")
            {
                Assert.AreEqual(
                    ModelInspectionFigmaState.ReadyExpanded,
                    host.ModelInspectionPage!.CurrentPresentation?.State,
                    "The runner must invoke the real disclosure.");
            }
            else if (id == "MI-024")
            {
                Assert.AreEqual(true,
                    host.ModelInspectionPage!.ViewModel?.Snapshot
                        .IsCancellationRequested,
                    "The runner must invoke the real Cancel command.");
            }
            else if (id == "MI-034")
            {
                Assert.AreEqual(
                    ModelInspectionFigmaState.ReadyCollapsed,
                    host.ModelInspectionPage!.CurrentPresentation?.State,
                    "The submitted attempt-one snapshot must remain stale after " +
                    "attempt two reaches Ready.");
            }
        }
        finally
        {
            host.RetireForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task ScenarioRunner_RejectsAnyStepAfterTheNamedObservation()
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == "MI-001");
        ValidatedModelInspectionFixtureInput invalid = CloneInput(
            fixture.Input,
            fixture.Input.SetupSteps.Concat(
            [
                new ModelInspectionFixtureSetupStepDescriptor(
                    ModelInspectionFixtureSetupStepKind.Observe,
                    null,
                    "trailing-observation",
                    null)
            ]).ToArray());
        var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: false,
            () => new ImmediateAnimationDriver());
        var page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new ModelInspectionFixtureScenarioRunner().RunAsync(
                invalid,
                page,
                session,
                CancellationToken.None));

        Assert.AreEqual(0, session.Evidence.ServiceCallCount);
        _ = page.RetireForFixture();
        session.Dispose();
    }

    [UITestMethod]
    public async Task ScenarioRunner_RejectsNonObservationStepAfterNamedObservationBeforeEffects()
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == "MI-001");
        ValidatedModelInspectionFixtureInput invalid = CloneInput(
            fixture.Input,
            fixture.Input.SetupSteps.Concat(
            [
                new ModelInspectionFixtureSetupStepDescriptor(
                    ModelInspectionFixtureSetupStepKind.InvokeCancel,
                    null,
                    null,
                    "cancel")
            ]).ToArray());
        var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: false,
            () => new ImmediateAnimationDriver());
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false);
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                new ModelInspectionFixtureScenarioRunner().RunAsync(
                    invalid,
                    page,
                    session,
                    CancellationToken.None));

            Assert.AreEqual(0, session.Evidence.ServiceCallCount);
            Assert.AreEqual(false,
                page.ViewModel?.Snapshot.IsCancellationRequested,
                "The invalid trailing action must be rejected before it runs.");
        }
        finally
        {
            _ = page.RetireForFixture();
            session.Dispose();
        }
    }

    [TestMethod]
    public void DebugGallerySourceAndIlClosure_ForbidExternalComposition()
    {
        string root = FindRepositoryRoot();
        string debugRoot = Path.Combine(root,
            "IBM Granite with TurboQuant (Intel)", "Features",
            "ModelInspection", "DebugFixtures");
        string onboardingRoot = Path.Combine(root,
            "IBM Granite with TurboQuant (Intel)", "Features",
            "Onboarding", "DebugFixtures");
        string[] files = Directory.GetFiles(debugRoot, "*.cs",
                SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(onboardingRoot, "*.cs",
                SearchOption.AllDirectories))
            .ToArray();
        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            Assert.IsTrue(source.StartsWith(
                "#if MODEL_INSPECTION_FIXTURE_GALLERY",
                StringComparison.Ordinal));
            Assert.IsTrue(source.TrimEnd().EndsWith("#endif",
                StringComparison.Ordinal));
            string guardedSource = string.Equals(
                    Path.GetFileName(file),
                    "ModelInspectionFixturePackageLoader.cs",
                    StringComparison.Ordinal)
                ? MaskExactPackageReaderSourceCalls(source)
                : source;
            string? violation = ForbiddenDebugSourceReference(guardedSource);
            Assert.IsNull(violation,
                $"{Path.GetFileName(file)} referenced {violation}.");
            if (string.Equals(
                    Path.GetFileName(file),
                    "ModelInspectionFixtureGalleryPage.xaml.cs",
                    StringComparison.Ordinal))
            {
                Assert.IsFalse(HasGalleryOwnedDestinationRegistration(source),
                    "The activation must register only its own host destination.");
            }
        }

        string[] sourceMutations =
        [
            "using System.IO; class M { void X() => File.ReadAllText(\"x\"); }",
            "using Disk = global::System.IO.File; class M { void X() => Disk.OpenRead(\"x\"); }",
            "using IO = System.IO; class M { void X() => IO.Directory.Delete(\"x\"); }",
            "using System.Diagnostics; class M { void X() => Process.Start(\"x\"); }",
            "using Net = System.Net.Http; class M { HttpClient X = new(); }",
            "using System.Net.Sockets; class M { Socket? X; }",
            "using System.IO; class M { FileInfo X = new(\"x\"); }",
            "using System.IO; class M { DirectoryInfo X = new(\"x\"); }",
            "using System.IO; class M { StreamReader X = new(\"x\"); }",
            "using System.IO; class M { StreamWriter X = new(\"x\"); }",
            "using System.IO; class M { FileSystemWatcher X = new(); }",
            "using System.IO; class M { DriveInfo X = new(\"C\"); }",
            "class M { void X() => System.IO.Compression.ZipFile.OpenRead(\"x\"); }",
            "class M { void X() => System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(\"x\"); }",
            "using Windows.Storage; class M { void X() => PathIO.ReadTextAsync(\"x\"); }",
            "using Windows.Storage; class M { object X = ApplicationData.Current.TemporaryFolder; }",
            "using Windows.Storage; class M { void X(StorageFolder f) => f.GetFilesAsync(); }",
            "using Windows.Storage; class M { void X() => StorageFile.GetFileFromPathAsync(\"x\"); }",
            "using Windows.Storage; class M { object X = KnownFolders.DocumentsLibrary; }",
            "using Windows.Storage; class M { void X() => KnownFolders.DocumentsLibrary.GetFileAsync(\"x\"); }",
            "using Windows.Storage; class M { void X(StorageFile f) => FileIO.ReadTextAsync(f); }",
            "using Windows.Storage; class M { void X(StorageFile f) => f.OpenReadAsync(); }",
            "using Windows.Storage; class M { void X() => StorageFile.GetFileFromApplicationUriAsync(new Uri(\"ms-appx:///x\")); }",
            "using Windows.Storage; class M { void X(StorageFile f) => FileIO.ReadBufferAsync(f); }",
            "using Windows.Networking.Sockets; class M { StreamSocket X = new(); }",
            "class M { ModelInspectionPage X() => new ModelInspectionPage(); }",
            "using P = GraniteEdgeAI.Features.ModelInspection.ModelInspectionPage; " +
                "class M { P X() => new P(); }"
        ];
        foreach (string mutation in sourceMutations)
        {
            Assert.IsNotNull(ForbiddenDebugSourceReference(mutation),
                $"The source guard accepted mutation: {mutation}");
        }
        Assert.IsNull(ForbiddenDebugSourceReference(
            "class M { void X() => " +
            "ModelInspectionPage.CreateForFixture(session, true); }"));
        Assert.IsTrue(HasGalleryOwnedDestinationRegistration(
            "FixtureHostFrame.Navigated += OnNavigated; " +
            "pending.RegisterDestination(host);"));

        Type[] closureTypes = typeof(ModelInspectionFixtureScenarioRunner)
            .Assembly.GetTypes()
            .Where(type => IsDebugFixtureNamespace(type.Namespace))
            .Concat(SelfAndNested(typeof(OnboardingShellPage)))
            .Distinct()
            .ToArray();
        (MethodBase Owner, MemberInfo Reference)[] sites =
            IlReferenceSites(closureTypes).ToArray();
        MemberInfo[] references = sites.Select(site => site.Reference).ToArray();
        foreach ((MethodBase owner, MemberInfo reference) in sites)
        {
            string identity = $"{owner.DeclaringType?.FullName}.{owner.Name} -> " +
                $"{reference.DeclaringType?.FullName}.{reference.Name}";
            bool exactPackageResourceSite = IsPackageResourceApi(reference) &&
                IsExactPackageReaderOwner(owner);
            bool exactScreenComparerExpectedSite =
                IsExactScreenComparerExpectedSite(owner, reference);
            Assert.IsFalse(
                IsForbiddenDebugIlReference(reference) &&
                    !exactPackageResourceSite &&
                    !exactScreenComparerExpectedSite,
                identity);
            Assert.IsFalse(
                IsForbiddenPackageResourceCallSite(owner, reference),
                identity);
        }
        foreach (Type type in closureTypes)
        {
            foreach (Type signature in DeclaredSignatureTypes(type))
            {
                Assert.IsFalse(ContainsNamespaceType(signature, "System.IO"),
                    $"{type.FullName} exposed forbidden signature " +
                    signature.FullName);
                Assert.IsFalse(
                    ContainsNamespaceType(signature, "Windows.Storage") &&
                        type != ExactPackageReaderStateMachineType(),
                    $"{type.FullName} exposed forbidden Windows.Storage " +
                    $"signature {signature.FullName}");
            }
        }
        Assert.IsTrue(ContainsNamespaceType(
            typeof(System.IO.FileSystemWatcher), "System.IO"));
        Assert.IsTrue(ContainsNamespaceType(
            typeof(Func<System.IO.DriveInfo>), "System.IO"));
        Assert.IsTrue(ContainsNamespaceType(
            typeof(Windows.Storage.StorageFile), "Windows.Storage"));
        Assert.IsTrue(ContainsNamespaceType(
            typeof(Func<Windows.Storage.StorageFile>), "Windows.Storage"));

        var packageResourceSites = sites.Where(site =>
            IsPackageResourceApi(site.Reference)).ToArray();
        Assert.AreEqual(1, packageResourceSites.Count(site =>
            IsPackageResourceApi(
                site.Reference,
                "Windows.Storage.StorageFile",
                "GetFileFromApplicationUriAsync")));
        Assert.AreEqual(1, packageResourceSites.Count(site =>
            IsPackageResourceApi(
                site.Reference,
                "Windows.Storage.FileIO",
                "ReadBufferAsync")));
        Assert.IsTrue(packageResourceSites.All(site =>
            IsExactPackageReaderOwner(site.Owner)),
            "Both package-resource calls must belong only to the exact reader.");

        Assert.IsTrue(references.Any(reference =>
            reference.DeclaringType == typeof(ModelInspectionPage) &&
            reference.Name == nameof(ModelInspectionPage.CreateForFixture)),
            "The Debug host must call the exact injected-page factory.");
        Assert.AreEqual(0, NewObjectConstructors(closureTypes).Count(value =>
            value.Constructor.DeclaringType == typeof(ModelInspectionPage)),
            "Debug callers must use CreateForFixture, not a page constructor.");

        (string TypeName, string SimpleName, string MemberName,
            bool IsConstructor, bool IsPublic, int ParameterCount)[]
            ilMutations =
            [
                ("System.IO.File", "File", "ReadAllText", false, false, 1),
                ("System.IO.Directory", "Directory", "GetFiles", false, false, 1),
                ("System.IO.FileStream", "FileStream", ".ctor", true, true, 2),
                ("System.IO.FileInfo", "FileInfo", ".ctor", true, true, 1),
                ("System.IO.DirectoryInfo", "DirectoryInfo", ".ctor", true, true, 1),
                ("System.IO.StreamReader", "StreamReader", ".ctor", true, true, 1),
                ("System.IO.StreamWriter", "StreamWriter", ".ctor", true, true, 1),
                ("System.IO.FileSystemWatcher", "FileSystemWatcher", ".ctor", true, true, 0),
                ("System.IO.DriveInfo", "DriveInfo", ".ctor", true, true, 1),
                ("System.IO.Compression.ZipFile", "ZipFile", "OpenRead", false, true, 1),
                ("System.IO.MemoryMappedFiles.MemoryMappedFile", "MemoryMappedFile", "CreateFromFile", false, true, 1),
                ("System.Diagnostics.Process", "Process", "Start", false, true, 1),
                ("System.Net.Http.HttpClient", "HttpClient", ".ctor", true, true, 0),
                ("System.Net.Sockets.Socket", "Socket", ".ctor", true, true, 2),
                ("Windows.Storage.PathIO", "PathIO", "ReadTextAsync", false, true, 1),
                ("Windows.Storage.ApplicationData", "ApplicationData", "get_TemporaryFolder", false, true, 0),
                ("Windows.Storage.StorageFolder", "StorageFolder", "GetFilesAsync", false, true, 0),
                ("Windows.Storage.StorageFile", "StorageFile", "GetFileFromPathAsync", false, true, 1),
                ("Windows.Storage.KnownFolders", "KnownFolders", "get_DocumentsLibrary", false, true, 0),
                ("Windows.Storage.StorageFolder", "StorageFolder", "GetFileAsync", false, true, 1),
                ("Windows.Storage.FileIO", "FileIO", "ReadTextAsync", false, true, 1),
                ("Windows.Storage.StorageFile", "StorageFile", "OpenReadAsync", false, true, 0),
                ("Windows.Networking.Sockets.StreamSocket", "StreamSocket", ".ctor", true, true, 0),
                (typeof(ModelInspectionPage).FullName!,
                    nameof(ModelInspectionPage), ".ctor", true, true, 0)
            ];
        foreach (var mutation in ilMutations)
        {
            Assert.IsTrue(IsForbiddenDebugIlReference(
                mutation.TypeName,
                mutation.SimpleName,
                mutation.MemberName,
                mutation.IsConstructor,
                mutation.IsPublic,
                mutation.ParameterCount),
                $"The IL guard accepted {mutation.TypeName}.{mutation.MemberName}.");
        }
        Assert.IsFalse(IsForbiddenDebugIlReference(
            typeof(ModelInspectionPage).FullName!,
            nameof(ModelInspectionPage),
            nameof(ModelInspectionPage.CreateForFixture),
            isConstructor: false,
            isPublic: false,
            parameterCount: 3));
        Assert.IsTrue(IsForbiddenDebugIlReference(
            "Windows.Storage.StorageFile",
            "StorageFile",
            "GetFileFromApplicationUriAsync",
            isConstructor: false,
            isPublic: true,
            parameterCount: 1),
            "The package API must not receive a global IL allowlist.");
        Assert.IsTrue(IsForbiddenDebugIlReference(
            "Windows.Storage.FileIO",
            "FileIO",
            "ReadBufferAsync",
            isConstructor: false,
            isPublic: true,
            parameterCount: 1),
            "The package buffer API must not receive a global IL allowlist.");
        (string OwnerType, string OwnerMethod, string ReferencedType,
            string ReferencedMember)[] packageCallSiteMutations =
            [
                (typeof(ModelInspectionFixtureGalleryPage).FullName!,
                    "SelectAsync", "Windows.Storage.StorageFile",
                    "GetFileFromApplicationUriAsync"),
                (typeof(ModelInspectionFixtureGalleryPage).FullName!,
                    "SelectAsync", "Windows.Storage.FileIO",
                    "ReadBufferAsync"),
                (typeof(OnboardingShellPage).FullName!,
                    "NavigateToFixtureGallery", "Windows.Storage.StorageFile",
                    "GetFileFromApplicationUriAsync"),
                (typeof(ModelInspectionFixtureScenarioRunner).FullName!,
                    "RunAsync", "Windows.Storage.FileIO", "ReadBufferAsync"),
                (ExactPackageReaderStateMachineType().FullName!,
                    "MoveNext", "Windows.Storage.CachedFileManager",
                    "DeferUpdates")
            ];
        foreach (var mutation in packageCallSiteMutations)
        {
            Assert.IsTrue(IsForbiddenPackageResourceCallSite(
                mutation.OwnerType,
                mutation.OwnerMethod,
                mutation.ReferencedType,
                mutation.ReferencedMember),
                $"The owner-scope IL guard accepted {mutation.OwnerType}." +
                $"{mutation.OwnerMethod} -> {mutation.ReferencedType}." +
                mutation.ReferencedMember);
        }
        string readerStateMachine = ExactPackageReaderStateMachineType().FullName!;
        Assert.IsFalse(IsForbiddenPackageResourceCallSite(
            readerStateMachine,
            "MoveNext",
            "Windows.Storage.StorageFile",
            "GetFileFromApplicationUriAsync"));
        Assert.IsFalse(IsForbiddenPackageResourceCallSite(
            readerStateMachine,
            "MoveNext",
            "Windows.Storage.FileIO",
            "ReadBufferAsync"));
    }

    [UITestMethod]
    public async Task GalleryEntry_DebugX64IsVisibleAndLoadsAtomicCatalogue()
    {
        var shell = new OnboardingShellPage();
        var frame = (Frame)shell.FindName("StageFrame");
        var initialImport = (ModelImportPage)frame.Content;
        var window = new Window { Content = shell };
        window.Activate();
        try
        {
            var button = shell.FindName("FixtureGalleryButton") as Button;
            Assert.IsNotNull(button);
            Assert.AreEqual("Fixture gallery", button.Content);
            Assert.AreEqual(Visibility.Visible, button.Visibility);
            Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);

            Invoke(button);
            var gallery = frame.Content as ModelInspectionFixtureGalleryPage;
            Assert.IsNotNull(gallery);
            await gallery.CatalogueLoaded;
            Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
            Assert.AreEqual(49, gallery.ViewModel.Items.Count);
            Assert.IsNotNull(gallery.FindName("FixtureSearchBox"));
            Assert.IsNotNull(gallery.FindName("FixtureCategoryFilter"));
            Assert.IsNotNull(gallery.FindName("FixtureList"));
            Assert.IsNotNull(gallery.FindName("FixtureHostFrame"));
            Assert.IsNotNull(gallery.FindName("FixtureFileNameText"));
            Assert.IsNotNull(gallery.FindName("FixtureIdText"));
            Assert.IsNotNull(gallery.FindName("FixtureTargetText"));
            Assert.IsNotNull(gallery.FindName("FixtureCategoryText"));
            Assert.AreEqual("Synthetic fixture",
                ((TextBlock)gallery.FindName("SyntheticFixtureBadge")).Text);
            Assert.IsNotNull(gallery.FindName("FixturePresetControls"));
            Assert.IsNotNull(gallery.FindName("FixtureInteractionPanel"));
            Assert.IsNotNull(gallery.FindName("ResetFixtureButton"));
            Assert.IsNotNull(gallery.FindName("CloseFixtureGalleryButton"));
            var status = (TextBlock)gallery.FindName(
                "FixtureValidationStatus");
            Assert.AreEqual(AutomationLiveSetting.Polite,
                AutomationProperties.GetLiveSetting(status));

            RaiseModelInspectionRequestedIfSubscribed(
                initialImport,
                CreateRequest(@"C:\Models\stale-import.gguf"));
            Assert.AreSame(gallery, frame.Content,
                "Successful gallery entry must detach the old import owner.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task VisibleGalleryLayout_AttachedWindowMaintainsExactTwoPaneGeometry()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { },
            new ImmediateScenarioRunner());
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");
            var root = (Grid)gallery.Content;
            Assert.AreEqual(2, root.ColumnDefinitions.Count);
            Assert.AreEqual(GridUnitType.Pixel,
                root.ColumnDefinitions[0].Width.GridUnitType);
            Assert.AreEqual(320d, root.ColumnDefinitions[0].Width.Value);
            Assert.AreEqual(GridUnitType.Star,
                root.ColumnDefinitions[1].Width.GridUnitType);
            Assert.AreEqual(1d, root.ColumnDefinitions[1].Width.Value);
            Assert.AreEqual(24d, root.ColumnSpacing);
            Assert.AreEqual(24d, root.Padding.Left);
            Assert.AreEqual(24d, root.Padding.Top);
            Assert.AreEqual(24d, root.Padding.Right);
            Assert.AreEqual(24d, root.Padding.Bottom);
            Grid[] panes = root.Children.OfType<Grid>().ToArray();
            Assert.AreEqual(2, panes.Length);
            Grid cataloguePane = panes.Single(pane =>
                Grid.GetColumn(pane) == 0);
            Grid hostPane = panes.Single(pane => Grid.GetColumn(pane) == 1);
            var layoutReached = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<object> layoutUpdated = (_, _) =>
            {
                if (root.ActualWidth >= 1000d && root.ActualHeight >= 600d &&
                    cataloguePane.ActualWidth > 0d &&
                    cataloguePane.ActualHeight > 0d &&
                    hostPane.ActualWidth > 0d && hostPane.ActualHeight > 0d)
                {
                    layoutReached.TrySetResult(true);
                }
            };
            root.LayoutUpdated += layoutUpdated;
            try
            {
                double scale = gallery.XamlRoot.RasterizationScale;
                window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                    (int)Math.Round(1200d * scale),
                    (int)Math.Round(800d * scale)));
                layoutUpdated(null, EventArgs.Empty);
                await layoutReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                root.UpdateLayout();
            }
            finally
            {
                root.LayoutUpdated -= layoutUpdated;
            }

            FrameworkElement[] requiredVisibleControls =
            [
                (TextBox)gallery.FindName("FixtureSearchBox"),
                (ComboBox)gallery.FindName("FixtureCategoryFilter"),
                (ListView)gallery.FindName("FixtureList"),
                (TextBlock)gallery.FindName("FixtureValidationStatus"),
                (TextBlock)gallery.FindName("SyntheticFixtureBadge"),
                (StackPanel)gallery.FindName("FixturePresetControls"),
                (Frame)gallery.FindName("FixtureHostFrame"),
                (Button)gallery.FindName("ResetFixtureButton"),
                (Button)gallery.FindName("CloseFixtureGalleryButton")
            ];
            foreach (FrameworkElement control in requiredVisibleControls)
            {
                Assert.IsTrue(control.IsLoaded, control.Name);
                Assert.AreEqual(Visibility.Visible, control.Visibility,
                    control.Name);
                Assert.IsTrue(control.ActualWidth > 0d, control.Name);
                Assert.IsTrue(control.ActualHeight > 0d, control.Name);
            }

            Windows.Foundation.Point catalogueOrigin = cataloguePane
                .TransformToVisual(root)
                .TransformPoint(new Windows.Foundation.Point());
            Windows.Foundation.Point hostOrigin = hostPane
                .TransformToVisual(root)
                .TransformPoint(new Windows.Foundation.Point());
            Assert.IsTrue(Math.Abs(cataloguePane.ActualWidth - 320d) <= 0.01d);
            double expectedHostWidth = root.ActualWidth -
                root.Padding.Left - root.Padding.Right -
                cataloguePane.ActualWidth - root.ColumnSpacing;
            Assert.IsTrue(Math.Abs(
                    hostPane.ActualWidth - expectedHostWidth) <= 1d,
                "The star host pane must consume the exact remaining column width.");
            Assert.IsTrue(catalogueOrigin.X < hostOrigin.X);
            Assert.IsTrue(
                catalogueOrigin.X + cataloguePane.ActualWidth <= hostOrigin.X,
                "The nonzero catalogue pane must remain left of the host pane.");

            string xaml = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "IBM Granite with TurboQuant (Intel)",
                "Features", "ModelInspection", "DebugFixtures", "Gallery",
                "ModelInspectionFixtureGalleryPage.xaml"));
            Assert.IsTrue(HasExactVisibleSplitGalleryLayout(xaml));
            string oneColumn = xaml.Replace(
                "<ColumnDefinition Width=\"*\" />",
                "<ColumnDefinition Width=\"0\" />",
                StringComparison.Ordinal);
            Assert.AreNotEqual(xaml, oneColumn);
            Assert.IsFalse(HasExactVisibleSplitGalleryLayout(oneColumn));
            string collapsed = xaml.Replace(
                "Grid.Column=\"1\" RowSpacing=\"12\"",
                "Grid.Column=\"1\" RowSpacing=\"12\" Visibility=\"Collapsed\"",
                StringComparison.Ordinal);
            Assert.AreNotEqual(xaml, collapsed);
            Assert.IsFalse(HasExactVisibleSplitGalleryLayout(collapsed));
            string overlapping = xaml.Replace(
                "Grid.Column=\"1\" RowSpacing=\"12\"",
                "Grid.Column=\"0\" RowSpacing=\"12\"",
                StringComparison.Ordinal);
            Assert.AreNotEqual(xaml, overlapping);
            Assert.IsFalse(HasExactVisibleSplitGalleryLayout(overlapping));
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task GalleryEntry_RealShellCloseCreatesFreshImportAndRetiresLifetime()
    {
        var shell = new OnboardingShellPage();
        var frame = (Frame)shell.FindName("StageFrame");
        var initialImport = (ModelImportPage)frame.Content;
        var window = new Window { Content = shell };
        window.Activate();
        try
        {
            Invoke((Button)shell.FindName("FixtureGalleryButton"));
            var gallery = (ModelInspectionFixtureGalleryPage)frame.Content;
            await gallery.CatalogueLoaded;
            await gallery.SelectFixtureForTestingAsync("MI-002");
            ModelInspectionFixtureSession activeSession =
                gallery.ActiveHost!.Session;

            Invoke((Button)gallery.FindName("CloseFixtureGalleryButton"));

            Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
            Assert.AreNotSame(initialImport, frame.Content,
                "Gallery Close must create a fresh Model Import page.");
            Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
            Assert.AreEqual(0, frame.BackStack.Count);
            Assert.AreEqual(0, frame.ForwardStack.Count);
            Assert.IsNull(gallery.ActiveHost);
            Assert.AreEqual(1,
                activeSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1,
                activeSession.Evidence.SessionDisposalCount);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task GallerySelection_RealRouteAppliesCanonicalPresetAndExactMi001StartRule()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-001");
            ModelInspectionFixtureHostPage initial = gallery.ActiveHost!;
            Assert.AreSame(DebugFixturePreset.Canonical, initial.Preset);
            Assert.AreEqual(0, initial.Session.Evidence.ServiceCallCount,
                "Only MI-001 must remain at the pre-start loaded screen.");

            await gallery.SelectFixtureForTestingAsync("MI-002");
            ModelInspectionFixtureHostPage started = gallery.ActiveHost!;
            Assert.AreSame(DebugFixturePreset.Canonical, started.Preset);
            Assert.AreEqual(1, started.Session.Evidence.ServiceCallCount,
                "Every non-MI-001 selection must start inspection on Loaded.");
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task GalleryLoadFailure_RendersOnlyTheSafeAtomicDiagnostic()
    {
        CountingReader reader = CountingReader.Valid();
        string fixtureUri = PackageRoot +
            "MI-049-operational-failure-detail-copy-maximum.fixture.json";
        reader.ThrowOn(fixtureUri);
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(reader),
            () => { });
        var window = new Window { Content = gallery };
        window.Activate();
        try
        {
            ModelInspectionFixtureGalleryLoadException error =
                await Assert.ThrowsExactlyAsync<
                    ModelInspectionFixtureGalleryLoadException>(async () =>
                        await gallery.CatalogueLoaded);

            var status = (TextBlock)gallery.FindName(
                "FixtureValidationStatus");
            StringAssert.Contains(status.Text, error.Diagnostic);
            StringAssert.Contains(status.Text, "MI-049");
            StringAssert.Contains(status.Text, "|");
            Assert.IsFalse(status.Text.Contains("unsafe reader detail",
                StringComparison.Ordinal));
            Assert.AreEqual(0, gallery.ViewModel.Items.Count);
            Assert.IsNull(gallery.ActiveHost);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task VisibleGalleryControls_DriveFilterSelectionResetAndClose()
    {
        int closeRequests = 0;
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => closeRequests++,
            new ImmediateScenarioRunner());
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            var search = (TextBox)gallery.FindName("FixtureSearchBox");
            var category = (ComboBox)gallery.FindName("FixtureCategoryFilter");
            var list = (ListView)gallery.FindName("FixtureList");
            var reset = (Button)gallery.FindName("ResetFixtureButton");
            var close = (Button)gallery.FindName("CloseFixtureGalleryButton");

            Task searchApplied = WaitForGalleryPropertyAsync(
                gallery.ViewModel,
                nameof(ModelInspectionFixtureGalleryViewModel.FilteredItems));
            search.Text =
                "MI-043-ready-model-name-maximum-collapsed.fixture.json";
            await WaitWithContextAsync(searchApplied, "visible search apply");
            Assert.AreEqual(1, list.Items.Count,
                "The visible search control did not reduce the real list.");
            Assert.AreEqual("MI-043",
                ((ModelInspectionFixtureListItem)list.Items[0]).Id);
            Task searchCleared = WaitForGalleryPropertyAsync(
                gallery.ViewModel,
                nameof(ModelInspectionFixtureGalleryViewModel.FilteredItems));
            search.Text = string.Empty;
            await WaitWithContextAsync(searchCleared, "visible search clear");
            Task categoryApplied = WaitForGalleryPropertyAsync(
                gallery.ViewModel,
                nameof(ModelInspectionFixtureGalleryViewModel.FilteredItems));
            object stressCategory = category.Items.Cast<object>().Single(
                item => item is ModelInspectionFixtureCategory value &&
                    value == ModelInspectionFixtureCategory.Stress);
            category.SelectedItem = stressCategory;
            await WaitWithContextAsync(categoryApplied, "visible category apply");
            CollectionAssert.AreEqual(
                new[]
                {
                    "MI-043", "MI-044", "MI-045", "MI-046", "MI-047",
                    "MI-048", "MI-049"
                },
                ((IEnumerable<ModelInspectionFixtureListItem>)list.ItemsSource)
                    .Select(item => item.Id)
                    .ToArray(),
                "The visible category route must expose the exact nonempty Stress set.");
            Task categoryCleared = WaitForGalleryPropertyAsync(
                gallery.ViewModel,
                nameof(ModelInspectionFixtureGalleryViewModel.FilteredItems));
            category.SelectedIndex = 0;
            await WaitWithContextAsync(categoryCleared, "visible category clear");
            Assert.AreEqual(49, list.Items.Count,
                "Clearing the visible category must restore the complete catalogue.");

            ModelInspectionFixtureListItem selected = gallery.ViewModel.Items
                .Single(item => item.Id == "MI-002");
            list.SelectedItem = selected;
            await gallery.SelectionCompletedForTesting;
            ModelInspectionFixtureHostPage first = gallery.ActiveHost!;
            ModelInspectionFixtureSession firstSession = first.Session;
            Assert.AreEqual(selected.FileName,
                ((TextBlock)gallery.FindName("FixtureFileNameText")).Text);
            Assert.AreEqual(selected.Id,
                ((TextBlock)gallery.FindName("FixtureIdText")).Text);
            Assert.AreEqual(selected.TargetCondition,
                ((TextBlock)gallery.FindName("FixtureTargetText")).Text);
            Assert.AreEqual(selected.Category.ToString(),
                ((TextBlock)gallery.FindName("FixtureCategoryText")).Text);

            Invoke(reset);
            await gallery.SelectionCompletedForTesting;
            Assert.AreNotSame(first, gallery.ActiveHost);
            Assert.AreEqual(1, firstSession.Evidence.SessionDisposalCount);

            Invoke(close);
            Assert.AreEqual(1, closeRequests);
            Assert.IsNull(gallery.ActiveHost);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public void GalleryActivation_IsOneShotAndRetiredIdentityIsRejected()
    {
        var active = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        object activeIdentity =
            ModelInspectionFixtureGalleryPage.CreateActivationForTesting();

        InvokeGalleryNavigation(active, "OnNavigatedTo", activeIdentity);

        Assert.IsTrue(active.IsActivatedWith(activeIdentity));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            InvokeGalleryNavigation(active, "OnNavigatedTo", activeIdentity));

        var retired = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        object retiredIdentity =
            ModelInspectionFixtureGalleryPage.CreateActivationForTesting();
        InvokeGalleryNavigation(retired, "OnNavigatedTo", retiredIdentity);

        Assert.IsTrue(retired.RaiseUnloadedForTesting());
        Assert.IsFalse(retired.IsActivatedWith(retiredIdentity));
        Assert.IsNull(retired.ActivationIdentityForTesting);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            InvokeGalleryNavigation(retired, "OnNavigatedTo", retiredIdentity));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            InvokeGalleryNavigation(
                retired,
                "OnNavigatedTo",
                ModelInspectionFixtureGalleryPage.CreateActivationForTesting()));
    }

    [UITestMethod]
    public async Task GalleryUnload_CancelsInFlightCatalogueBeforeItCanPublish()
    {
        var reader = new GatedCatalogueReader();
        var loader = new ModelInspectionFixturePackageLoader(reader);
        var gallery = new ModelInspectionFixtureGalleryPage(
            loader,
            () => { });
        var window = new Window { Content = gallery };
        window.Activate();
        try
        {
            await reader.FirstReadStarted.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.IsTrue(gallery.RaiseUnloadedForTesting());
            reader.ReleaseFirstRead();

            bool cancelled = false;
            try
            {
                await gallery.CatalogueLoaded.WaitAsync(
                    TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled,
                "A retired gallery must cancel its catalogue publication.");
            Assert.AreEqual(0, gallery.ViewModel.Items.Count);
            Assert.AreEqual(0, gallery.ViewModel.FilteredItems.Count);
            Assert.IsNull(loader.CoverageCatalogue);
            Assert.IsFalse(gallery.ViewModel.ValidationStatus.StartsWith(
                "Catalogue validated:",
                StringComparison.Ordinal));
        }
        finally
        {
            reader.ReleaseFirstRead();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public void GalleryEntry_FailedDestinationPreservesOuterOwnershipAndJournals()
    {
        var shell = new OnboardingShellPage();
        var frame = (Frame)shell.FindName("StageFrame");
        var expectedContent = (ModelImportPage)frame.Content;
        frame.BackStack.Add(new PageStackEntry(typeof(Page), null, null));
        int backCount = frame.BackStack.Count;
        frame.ForwardStack.Add(new PageStackEntry(
            typeof(Page), null, null));
        int forwardCount = frame.ForwardStack.Count;

        bool opened = shell.NavigateToFixtureGalleryForTesting(
            static _ => true);

        Assert.IsFalse(opened);
        Assert.AreSame(expectedContent, frame.Content);
        Assert.AreEqual(backCount, frame.BackStack.Count);
        Assert.AreEqual(forwardCount, frame.ForwardStack.Count);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);

        ModelInspectionRequest request = CreateRequest(
            @"C:\Models\still-attached.gguf");
        RaiseModelInspectionRequestedIfSubscribed(expectedContent, request);
        Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content);
        Assert.AreSame(request,
            ((ModelInspectionPage)frame.Content).Request,
            "Failed gallery navigation must preserve the old subscription.");
    }

    [UITestMethod]
    public void GalleryEntry_FromInspectionDetachesOwnerClearsBothJournalsAndKeepsStage()
    {
        var shell = new OnboardingShellPage();
        Assert.IsTrue(shell.NavigateToModelInspection(
            CreateRequest(@"C:\Models\fixture-entry.gguf")));
        var frame = (Frame)shell.FindName("StageFrame");
        var inspection = (ModelInspectionPage)frame.Content;
        frame.ForwardStack.Add(new PageStackEntry(typeof(Page), null, null));
        object activation =
            ModelInspectionFixtureGalleryPage.CreateActivationForTesting();

        Assert.IsTrue(shell.NavigateToFixtureGalleryForTesting(
            target => target.Navigate(
                typeof(ModelInspectionFixtureGalleryPage),
                activation)));

        var gallery = frame.Content as ModelInspectionFixtureGalleryPage;
        Assert.IsNotNull(gallery);
        Assert.AreSame(activation, gallery.ActivationIdentityForTesting);
        Assert.AreEqual(0, frame.BackStack.Count);
        Assert.AreEqual(0, frame.ForwardStack.Count);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
        Assert.IsNull(inspection.ViewModel,
            "The old inspection page must retire on successful gallery entry.");
        FieldInfo? chooseEvent = typeof(ModelInspectionPage).GetField(
            "ChooseAnotherModelRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNull(chooseEvent?.GetValue(inspection),
            "Successful gallery entry must detach the old shell handler.");
        Assert.AreSame(gallery, frame.Content,
            "A stale old shell event must be detached.");
    }

    [UITestMethod]
    public async Task GallerySelection_OwnsOneRealInjectedPageAndResetRetiresOldHost()
    {
        CountingReader reader = CountingReader.Valid();
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(reader),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");
            ModelInspectionFixtureHostPage first = gallery.ActiveHost!;
            ModelInspectionFixtureSession firstSession = first.Session;
            Assert.IsNotNull(first.ModelInspectionPage);
            Assert.AreSame(first.Session.Request,
                first.ModelInspectionPage.Request);
            Assert.AreEqual("Screen contract passed: observed.",
                gallery.ViewModel.ValidationStatus);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);

            await gallery.ResetForTestingAsync();
            Assert.AreNotSame(first, gallery.ActiveHost);
            Assert.AreEqual(1, firstSession.Evidence.SessionDisposalCount);
            Assert.AreEqual(1, firstSession.Evidence.SessionRetirementCount);
            Assert.IsNull(first.ModelInspectionPage);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
            await gallery.SelectFixtureForTestingAsync("MI-039");
            CollectionAssert.AreEqual(ExpectedPackageUris(), reader.Requests,
                "Selection, Reset and switch must reuse the retained 51 resources.");
            Assert.IsTrue(reader.Requests.All(uri => reader.Count(uri) == 1));
            gallery.CloseForTesting();
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task GallerySelection_ProjectsOnlyVisibleInteractionsAndExactN001Links()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            var panel = (StackPanel)gallery.FindName(
                "FixtureInteractionPanel");
            var n001 = (TextBlock)gallery.FindName(
                "FixtureRealWorkerCoverageText");

            await gallery.SelectFixtureThroughRealListForTestingAsync("MI-003");
            ModelInspectionFixtureListItem expanded =
                gallery.ViewModel.SelectedItem!;
            ModelInspectionPage page = gallery.ActiveHost!.ModelInspectionPage!;
            Assert.AreEqual(0, panel.Children.Count,
                "The gallery must not create descriptor proxy buttons.");
            CollectionAssert.AreEqual(
                new[] { "collapse", "choose-another", "reset" },
                expanded.Fixture.VisibleInteractions
                    .Select(interaction => interaction.Id)
                    .ToArray());
            foreach (ModelInspectionFixtureInteraction interaction in
                     expanded.Fixture.VisibleInteractions)
            {
                Button rendered = await gallery
                    .FindRenderedActionButtonForTestingAsync(interaction.Id);
                Assert.IsTrue(rendered.IsEnabled, interaction.Id);
                ModelInspectionFixtureGalleryTestHarness
                    .AssertActualRenderedControl(
                        gallery,
                        page,
                        interaction.Id,
                        rendered,
                        interaction.Id);
            }
            Assert.IsNull(
                await gallery.FindRenderedActionButtonOrNullForTestingAsync(
                    "expand"),
                "Setup-history actions must not leak into the rendered surface.");
            Assert.AreEqual("Real-worker coverage: N-001", n001.Text);
            Assert.AreEqual(Visibility.Visible, n001.Visibility);
            Assert.IsInstanceOfType<TextBlock>(n001,
                "The external evidence marker must remain read-only text.");

            await gallery.SelectFixtureThroughRealListForTestingAsync("MI-002");
            Assert.AreEqual("Real-worker coverage: N-001", n001.Text);
            Assert.AreEqual(Visibility.Visible, n001.Visibility);

            await gallery.SelectFixtureThroughRealListForTestingAsync("MI-004");
            Assert.AreEqual(string.Empty, n001.Text,
                "N-001 is linked only to MI-002 and MI-003.");
            Assert.AreEqual(Visibility.Collapsed, n001.Visibility);
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task ChooseAnother_RealMi037RouteRetiresExactHostAndStaleEventCannotRetireWinner()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            var list = (ListView)gallery.FindName("FixtureList");
            ModelInspectionFixtureListItem row = gallery.ViewModel.Items
                .Single(item => item.Id == "MI-037");
            list.SelectedItem = row;
            await gallery.SelectionCompletedForTesting;
            ModelInspectionFixtureHostPage retiredHost = gallery.ActiveHost!;
            ModelInspectionPage retiredPage = retiredHost.ModelInspectionPage!;
            ModelInspectionFixtureSession retiredSession = retiredHost.Session;
            Button chooseAnother = await gallery
                .FindRenderedActionButtonForTestingAsync("choose-another");
            ModelInspectionFixtureGalleryTestHarness.AssertActualRenderedControl(
                gallery,
                retiredPage,
                "choose-another",
                chooseAnother,
                "MI-037 choose-another");
            FieldInfo chooseAnotherEvent = typeof(ModelInspectionPage).GetField(
                "ChooseAnotherModelRequested",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var staleChooseAnother = (EventHandler?)chooseAnotherEvent.GetValue(
                retiredPage);
            Assert.IsNotNull(staleChooseAnother,
                "The active gallery must own the production page's real " +
                "ChooseAnother event route.");

            Invoke(chooseAnother);
            await DrainAsync(gallery);

            Assert.IsNull(gallery.ActiveHost);
            Assert.IsNull(gallery.HostFrame.Content);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
            Assert.IsNull(list.SelectedItem,
                "The real ChooseAnother route must clear visible selection.");
            Assert.IsNull(gallery.ViewModel.SelectedItem);
            Assert.AreEqual("gallery:no-active-fixture",
                gallery.ViewModel.ValidationStatus);
            Assert.IsNull(retiredHost.ModelInspectionPage);
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionDisposalCount);

            list.SelectedItem = row;
            await gallery.SelectionCompletedForTesting;
            ModelInspectionFixtureHostPage winner = gallery.ActiveHost!;
            ModelInspectionFixtureSession winnerSession = winner.Session;
            Assert.AreNotSame(retiredHost, winner);
            Assert.AreNotSame(retiredSession, winnerSession);

            staleChooseAnother(retiredPage, EventArgs.Empty);
            await DrainAsync(gallery);

            Assert.AreSame(winner, gallery.ActiveHost,
                "A stale ChooseAnother callback from the retired page cannot " +
                "retire the newer exact owner.");
            Assert.AreSame(winner, gallery.HostFrame.Content);
            Assert.AreSame(row, list.SelectedItem);
            Assert.AreEqual("MI-037", gallery.ViewModel.SelectedItem?.Id);
            Assert.AreEqual(0,
                winnerSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(0,
                winnerSession.Evidence.SessionDisposalCount);
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionDisposalCount);
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task GallerySwitch_CreatesDistinctHostAndRetiresPriorLifetime()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");
            ModelInspectionFixtureHostPage first = gallery.ActiveHost!;
            ModelInspectionFixtureSession firstSession = first.Session;

            await gallery.SelectFixtureForTestingAsync("MI-039");

            Assert.AreEqual("MI-039", gallery.ViewModel.SelectedItem?.Id);
            Assert.AreNotSame(first, gallery.ActiveHost);
            Assert.AreEqual(1, firstSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1, firstSession.Evidence.SessionDisposalCount);
            Assert.IsNull(first.ModelInspectionPage);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task GallerySwitch_RealMi038RouteRetiresExactLifetimeAndOwnsMi039Successor()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            var list = (ListView)gallery.FindName("FixtureList");
            ModelInspectionFixtureListItem source = gallery.ViewModel.Items
                .Single(item => item.Id == "MI-038");
            ModelInspectionFixtureListItem successor = gallery.ViewModel.Items
                .Single(item => item.Id == "MI-039");

            list.SelectedItem = source;
            await gallery.SelectionCompletedForTesting;
            ModelInspectionFixtureHostPage retiredHost = gallery.ActiveHost!;
            ModelInspectionPage retiredPage =
                retiredHost.ModelInspectionPage!;
            ModelInspectionFixtureSession retiredSession =
                retiredHost.Session;

            Assert.AreSame(retiredHost, gallery.HostFrame.Content);
            Assert.AreSame(retiredSession.Request, retiredPage.Request);
            Assert.AreSame(source, gallery.ViewModel.SelectedItem);
            Assert.AreEqual("Screen contract passed: observed.",
                gallery.ViewModel.ValidationStatus);
            Assert.AreEqual(0,
                retiredSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(0,
                retiredSession.Evidence.SessionDisposalCount);

            list.SelectedItem = successor;
            await gallery.SelectionCompletedForTesting;
            ModelInspectionFixtureHostPage successorHost =
                gallery.ActiveHost!;
            ModelInspectionPage successorPage =
                successorHost.ModelInspectionPage!;
            ModelInspectionFixtureSession successorSession =
                successorHost.Session;

            Assert.AreNotSame(retiredHost, successorHost);
            Assert.AreNotSame(retiredPage, successorPage);
            Assert.AreNotSame(retiredSession, successorSession);
            Assert.IsNull(retiredHost.ModelInspectionPage);
            Assert.IsNull(retiredPage.Request);
            Assert.IsNull(retiredPage.ViewModel);
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionDisposalCount);
            Assert.IsFalse(retiredHost.RetireForTesting());
            Assert.IsFalse(retiredPage.RetireForFixture());
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1,
                retiredSession.Evidence.SessionDisposalCount);

            Assert.AreSame(successor, list.SelectedItem);
            Assert.AreSame(successor, gallery.ViewModel.SelectedItem);
            Assert.AreSame(successorHost, gallery.ActiveHost);
            Assert.AreSame(successorHost, gallery.HostFrame.Content);
            Assert.AreSame(successorPage,
                successorHost.ModelInspectionPage);
            Assert.AreSame(successorSession.Request, successorPage.Request);
            Assert.AreEqual("Screen contract passed: observed.",
                gallery.ViewModel.ValidationStatus);
            Assert.AreEqual(0,
                successorSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(0,
                successorSession.Evidence.SessionDisposalCount);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task OverlappingSelection_LateOldCompletionCannotOverwriteNewHost()
    {
        var runner = new GatedScenarioRunner();
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { },
            runner);
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            Task first = gallery.SelectFixtureForTestingAsync("MI-002");
            await runner.FirstStarted;
            Task second = gallery.SelectFixtureForTestingAsync("MI-039");
            await runner.SecondStarted;
            runner.ReleaseSecond();
            await second;
            ModelInspectionFixtureHostPage current = gallery.ActiveHost!;
            string currentStatus = gallery.ViewModel.ValidationStatus;

            runner.ReleaseFirstLate();
            await first;

            Assert.AreEqual("MI-039", gallery.ViewModel.SelectedItem?.Id);
            Assert.AreSame(current, gallery.ActiveHost);
            Assert.AreEqual(currentStatus, gallery.ViewModel.ValidationStatus);
            Assert.AreEqual(0,
                current.Session.Evidence.SessionDisposalCount,
                "The stale first selection cannot retire the winner.");
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task ReentrantNavigationConfirmation_PreservesTheExactNewWinner()
    {
        ModelInspectionFixtureGalleryPage? gallery = null;
        Task? winnerSelection = null;
        ModelInspectionFixtureSessionEvidence? firstEvidence = null;
        int confirmationCount = 0;
        gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { },
            new ImmediateScenarioRunner(),
            beforeHostNavigationConfirmation: () =>
            {
                if (Interlocked.Increment(ref confirmationCount) != 1)
                {
                    return;
                }

                ModelInspectionFixtureHostPage first = gallery.ActiveHost ??
                    (ModelInspectionFixtureHostPage)gallery.HostFrame.Content;
                FieldInfo identity = typeof(ModelInspectionFixtureHostPage)
                    .GetField("activationIdentity",
                        BindingFlags.Instance | BindingFlags.NonPublic)!;
                firstEvidence = ((ModelInspectionFixtureHostActivation)
                    identity.GetValue(first)!).Evidence;
                winnerSelection = gallery.SelectFixtureForTestingAsync("MI-039");
            });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");
            if (winnerSelection is not null)
            {
                await winnerSelection;
            }

            Assert.IsNotNull(firstEvidence);
            Assert.AreEqual(1, firstEvidence.SessionRetirementCount);
            Assert.AreEqual(1, firstEvidence.SessionDisposalCount);
            Assert.AreEqual("MI-039", gallery.ViewModel.SelectedItem?.Id);
            ModelInspectionFixtureHostPage winner = gallery.ActiveHost!;
            Assert.AreEqual(0, winner.Session.Evidence.SessionDisposalCount);
            Assert.AreSame(winner, gallery.HostFrame.Content);
        }
        finally
        {
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    [DataRow(false, DisplayName = "Cancelled before destination")]
    [DataRow(true, DisplayName = "Throws after destination")]
    public async Task HostNavigationFailure_ClearsExactPendingOwnershipAndAllowsNextSelection(
        bool throwAfterDestination)
    {
        ModelInspectionFixtureSessionEvidence? failedEvidence = null;
        ModelInspectionFixtureHostPage? failedDestination = null;
        int hostNavigationCount = 0;
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { },
            new ImmediateScenarioRunner(),
            beforeHostNavigationConfirmation: () =>
            {
                if (throwAfterDestination &&
                    Volatile.Read(ref hostNavigationCount) == 1)
                {
                    throw new ApplicationException(
                        "injected host navigation failure");
                }
            });
        NavigatingCancelEventHandler navigating = (_, arguments) =>
        {
            if (arguments.SourcePageType !=
                typeof(ModelInspectionFixtureHostPage))
            {
                return;
            }

            int navigation = Interlocked.Increment(ref hostNavigationCount);
            if (navigation != 1)
            {
                return;
            }

            failedEvidence = ((ModelInspectionFixtureHostActivation)
                arguments.Parameter!).Evidence;
            if (!throwAfterDestination)
            {
                arguments.Cancel = true;
            }
        };
        NavigatedEventHandler navigated = (_, arguments) =>
        {
            if (Volatile.Read(ref hostNavigationCount) == 1 &&
                arguments.Content is ModelInspectionFixtureHostPage host)
            {
                failedDestination = host;
                gallery.HostFrame.BackStack.Add(new PageStackEntry(
                    typeof(Page), null, null));
                gallery.HostFrame.ForwardStack.Add(new PageStackEntry(
                    typeof(Page), null, null));
            }
        };
        gallery.HostFrame.Navigating += navigating;
        gallery.HostFrame.Navigated += navigated;
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");
            await DrainAsync(gallery);

            Assert.IsNotNull(failedEvidence);
            Assert.IsNull(gallery.PendingActivationForTesting,
                "Failed navigation must immediately relinquish its exact " +
                "pending activation ownership.");
            Assert.IsNull(gallery.ActiveHost);
            Assert.IsNull(gallery.HostFrame.Content);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
            Assert.IsNull(gallery.ViewModel.SelectedItem);
            Assert.AreEqual("Fixture unavailable: scenario.execution",
                gallery.ViewModel.ValidationStatus);
            Assert.AreEqual(1, failedEvidence.SessionRetirementCount);
            Assert.AreEqual(1, failedEvidence.SessionDisposalCount);
            if (throwAfterDestination)
            {
                Assert.IsNotNull(failedDestination);
                Assert.IsNull(failedDestination.ModelInspectionPage,
                    "An exact destination claimed before failure must retire.");
            }
            else
            {
                Assert.IsNull(failedDestination,
                    "Cancelled navigation must not manufacture a destination.");
            }

            await gallery.SelectFixtureForTestingAsync("MI-039");

            Assert.AreEqual("MI-039", gallery.ViewModel.SelectedItem?.Id);
            Assert.IsNotNull(gallery.ActiveHost);
            Assert.AreSame(gallery.ActiveHost, gallery.HostFrame.Content);
            Assert.IsNull(gallery.PendingActivationForTesting);
            Assert.AreEqual(0,
                gallery.ActiveHost.Session.Evidence.SessionDisposalCount,
                "The later successful selection must retain its fresh lifetime.");
        }
        finally
        {
            gallery.HostFrame.Navigating -= navigating;
            gallery.HostFrame.Navigated -= navigated;
            gallery.CloseForTesting();
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task InvalidSelection_RetiresBeforeRawReadAndCreatesNoReplacement()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            var list = (ListView)gallery.FindName("FixtureList");
            ModelInspectionFixtureListItem selected = gallery.ViewModel.Items
                .Single(item => item.Id == "MI-002");
            list.SelectedItem = selected;
            await gallery.SelectionCompletedForTesting;
            ModelInspectionFixtureHostPage old = gallery.ActiveHost!;
            ModelInspectionFixtureSession oldSession = old.Session;
            bool readAfterRetirement = false;

            await gallery.SelectRawFixtureForTestingAsync(
                "MI-002-ready-clean-compatible-model-collapsed.fixture.json",
                () =>
                {
                    readAfterRetirement =
                        oldSession.Evidence.SessionDisposalCount == 1 &&
                        old.ModelInspectionPage is null;
                    return Encoding.UTF8.GetBytes("{}");
                });

            Assert.IsTrue(readAfterRetirement);
            Assert.IsNull(gallery.ActiveHost);
            Assert.IsNull(list.SelectedItem,
                "Failed revalidation must clear the real visible selection.");
            Assert.IsNull(gallery.ViewModel.SelectedItem);
            Assert.IsTrue(gallery.ViewModel.ValidationStatus.StartsWith(
                "Fixture unavailable:",
                StringComparison.Ordinal));
            gallery.CloseForTesting();
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public void Host_AllExitSignalsRetirePageAndSessionExactlyOnce()
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == "MI-002");
        var session = new ModelInspectionFixtureSession(fixture.Input,
            animationsEnabled: false);
        var host = new ModelInspectionFixtureHostPage();
        var activation = new ModelInspectionFixtureHostActivation(
            fixture.Input, session, startInspectionOnLoaded: true);
        host.ActivateForTesting(activation);
        ModelInspectionPage page = host.ModelInspectionPage!;
        Assert.AreSame(session.Request, page.Request);

        host.RetireForTesting();
        host.RaiseUnloadedForTesting();
        host.RetireForTesting();

        Assert.IsNull(host.ModelInspectionPage);
        Assert.IsNull(page.ViewModel);
        Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
        Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
    }

    [UITestMethod]
    public void HostRetirement_ReleasesLifetimeAndActivationClosures()
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == "MI-002");
        var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: false);
        var host = new ModelInspectionFixtureHostPage();
        var activation = new ModelInspectionFixtureHostActivation(
            fixture.Input,
            session,
            startInspectionOnLoaded: true);
        host.ActivateForTesting(activation);
        FieldInfo lifetime = typeof(ModelInspectionFixtureHostPage).GetField(
            "lifetime",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo identity = typeof(ModelInspectionFixtureHostPage).GetField(
            "activationIdentity",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsNotNull(lifetime.GetValue(host));
        Assert.AreSame(activation, identity.GetValue(host));

        Assert.IsTrue(host.RetireForTesting());

        Assert.IsNull(lifetime.GetValue(host),
            "The host must release actions that close over page/session state.");
        Assert.IsNull(identity.GetValue(host),
            "A retired activation identity must not remain rooted.");
        Assert.IsNull(host.ModelInspectionPage);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = host.Session);
        Assert.IsFalse(host.IsActivatedWith(activation));
        Assert.IsFalse(host.RetireForTesting());
    }

    [TestMethod]
    public void HostLifetime_AttemptsOrderedCleanupAndRethrowsFirstError()
    {
        List<string> calls = [];
        var lifetime = new ModelInspectionFixtureHostLifetime(
            () =>
            {
                calls.Add("detach");
                throw new ApplicationException("detach");
            },
            () =>
            {
                calls.Add("page");
                throw new InvalidOperationException("page");
            },
            () =>
            {
                calls.Add("session");
                throw new NotSupportedException("session");
            },
            () =>
            {
                calls.Add("clear");
                throw new ArgumentException("clear");
            });

        ApplicationException error = Assert.ThrowsExactly<
            ApplicationException>(() => lifetime.Retire());

        Assert.AreEqual("detach", error.Message);
        CollectionAssert.AreEqual(
            new[] { "detach", "page", "session", "clear" },
            calls);
        Assert.IsFalse(lifetime.Retire());
        Assert.AreEqual(4, calls.Count);
    }

    [UITestMethod]
    public async Task HostRetirement_ReentrantCallKeepsExternalCallerJoined()
    {
        var host = new ModelInspectionFixtureHostPage();
        using var reentrantReturned = new ManualResetEventSlim();
        using var releaseCleanup = new ManualResetEventSlim();
        using var externalStarted = new ManualResetEventSlim();
        bool reentrantResult = true;
        var owned = new ModelInspectionFixtureHostLifetime(
            detach: () => { },
            retirePage: () =>
            {
                reentrantResult = host.RetireForTesting();
                reentrantReturned.Set();
                Assert.IsTrue(
                    releaseCleanup.Wait(TimeSpan.FromSeconds(5)),
                    "The test did not release paused host cleanup.");
            },
            retireSession: () => { },
            clear: () => { });
        FieldInfo lifetimeField = typeof(ModelInspectionFixtureHostPage)
            .GetField("lifetime", BindingFlags.Instance | BindingFlags.NonPublic)!;
        lifetimeField.SetValue(host, owned);

        Task<bool> outer = Task.Run(host.RetireForTesting);
        Assert.IsTrue(
            reentrantReturned.Wait(TimeSpan.FromSeconds(5)),
            "Host cleanup never reached the reentrant retirement callback.");
        Task<bool> external = Task.Run(() =>
        {
            externalStarted.Set();
            return host.RetireForTesting();
        });
        Assert.IsTrue(
            externalStarted.Wait(TimeSpan.FromSeconds(5)),
            "The external retirement did not start.");

        bool externalEscaped = external.Wait(TimeSpan.FromMilliseconds(250));
        releaseCleanup.Set();
        Assert.IsTrue(await outer);
        Assert.IsFalse(await external);
        Assert.IsFalse(reentrantResult);
        Assert.IsFalse(
            externalEscaped,
            "An external caller returned before reentrant host cleanup completed.");
        Assert.IsNull(lifetimeField.GetValue(host));
    }

    [UITestMethod]
    public async Task Host_NavigationAwayAndUnloadedRetireExactlyOnce()
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == "MI-002");
        var session = new ModelInspectionFixtureSession(fixture.Input,
            animationsEnabled: false,
            () => new ImmediateAnimationDriver());
        var activation = new ModelInspectionFixtureHostActivation(
            fixture.Input, session, startInspectionOnLoaded: true);
        var frame = new Frame();
        Assert.IsTrue(frame.Navigate(
            typeof(ModelInspectionFixtureHostPage),
            activation));
        var host = (ModelInspectionFixtureHostPage)frame.Content;
        var window = new Window { Content = frame };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var unloaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        host.Loaded += (_, _) => loaded.TrySetResult(true);
        host.Unloaded += (_, _) => unloaded.TrySetResult(true);
        window.Activate();
        try
        {
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await DrainAsync(host);
            Assert.IsTrue(frame.Navigate(typeof(Page)));
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(1, session.Evidence.SessionRetirementCount);
            Assert.AreEqual(1, session.Evidence.SessionDisposalCount);
            Assert.IsNull(host.ModelInspectionPage);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task WindowClose_RetiresActiveGalleryLifetimeExactlyOnce()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        await gallery.SelectFixtureForTestingAsync("MI-002");
        ModelInspectionFixtureHostPage active = gallery.ActiveHost!;
        ModelInspectionFixtureSession activeSession = active.Session;
        var unloaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        gallery.Unloaded += (_, _) => unloaded.TrySetResult(true);

        try
        {
            window.Close();
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(1, activeSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1, activeSession.Evidence.SessionDisposalCount);
            Assert.IsNull(gallery.ActiveHost);
        }
        finally
        {
            gallery.RaiseUnloadedForTesting();
        }
    }

    [UITestMethod]
    public async Task GalleryNavigationAwayBeforeLoadedCancelsCatalogueCompletion()
    {
        var frame = new Frame();
        object activation = ModelInspectionFixtureGalleryPage.CreateActivation(
            static () => true);
        Assert.IsTrue(frame.Navigate(
            typeof(ModelInspectionFixtureGalleryPage),
            activation));
        var gallery = (ModelInspectionFixtureGalleryPage)frame.Content;

        Assert.IsTrue(frame.Navigate(typeof(Page)));

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
            await gallery.CatalogueLoaded.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(0, gallery.ViewModel.Items.Count);
        Assert.IsNull(gallery.ActiveHost);
    }

    [UITestMethod]
    public void GalleryUnloadRevokesStaleCloseNavigationAuthority()
    {
        int closeRequests = 0;
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => closeRequests++);

        Assert.IsTrue(gallery.RaiseUnloadedForTesting());
        gallery.CloseForTesting();

        Assert.AreEqual(0, closeRequests);
        FieldInfo callback = typeof(ModelInspectionFixtureGalleryPage).GetField(
            "closeRequested",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsNull(callback.GetValue(gallery));
    }

    [UITestMethod]
    public async Task GalleryClose_RetiresInnerLifetimeClearsJournalsAndRequestsFreshImport()
    {
        int closeRequests = 0;
        ModelInspectionFixtureGalleryPage? gallery = null;
        ModelInspectionFixtureSession? activeSession = null;
        gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () =>
            {
                closeRequests++;
                Assert.IsNull(gallery.ActiveHost,
                    "Close must retire the active host before requesting fresh import.");
                Assert.AreEqual(1, activeSession!.Evidence.SessionRetirementCount);
                Assert.AreEqual(1, activeSession.Evidence.SessionDisposalCount);
                Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
                Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
            });
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");
            ModelInspectionFixtureHostPage active = gallery.ActiveHost!;
            activeSession = active.Session;

            gallery.CloseForTesting();

            Assert.AreEqual(1, closeRequests);
            Assert.IsNull(gallery.ActiveHost);
            Assert.AreEqual(1, activeSession.Evidence.SessionDisposalCount);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task GalleryClose_CallbackFailureLeavesInnerLifetimeRetired()
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => throw new ApplicationException("fresh-import"));
        Window window = await ShowAndLoadAsync(gallery);
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");
            ModelInspectionFixtureSession activeSession = gallery.ActiveHost!.Session;

            ApplicationException error = Assert.ThrowsExactly<
                ApplicationException>(gallery.CloseForTesting);

            Assert.AreEqual("fresh-import", error.Message);
            Assert.IsNull(gallery.ActiveHost);
            Assert.AreEqual(1, activeSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1, activeSession.Evidence.SessionDisposalCount);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [UITestMethod]
    [DataRow(false, DisplayName = "Unloaded")]
    [DataRow(true, DisplayName = "Close")]
    public async Task GalleryRetirement_DuringHostNavigationDrainsClaimedLifetime(
        bool close)
    {
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(CountingReader.Valid()),
            () => { });
        Window window = await ShowAndLoadAsync(gallery);
        ModelInspectionFixtureHostPage? destination = null;
        ModelInspectionFixtureSessionEvidence? destinationEvidence = null;
        NavigatedEventHandler? handler = null;
        handler = (_, arguments) =>
        {
            if (arguments.Content is not ModelInspectionFixtureHostPage host)
            {
                return;
            }

            gallery.HostFrame.Navigated -= handler;
            destination = host;
            destinationEvidence = ((ModelInspectionFixtureHostActivation)
                arguments.Parameter!).Evidence;
            if (close)
            {
                gallery.CloseForTesting();
            }
            else
            {
                gallery.RaiseUnloadedForTesting();
            }

            Assert.IsNull(host.ModelInspectionPage,
                "Reentrant gallery retirement must drain the claimed host before returning.");
            Assert.AreEqual(1, destinationEvidence.SessionRetirementCount);
            Assert.AreEqual(1, destinationEvidence.SessionDisposalCount);
            Assert.IsNull(gallery.ActiveHost);
            Assert.IsNull(gallery.HostFrame.Content);
            Assert.AreEqual(0, gallery.HostFrame.BackStack.Count);
            Assert.AreEqual(0, gallery.HostFrame.ForwardStack.Count);
        };
        gallery.HostFrame.Navigated += handler;
        try
        {
            await gallery.SelectFixtureForTestingAsync("MI-002");

            Assert.IsNotNull(destination);
            Assert.IsNotNull(destinationEvidence);
            Assert.IsNull(destination.ModelInspectionPage);
            Assert.AreEqual(1, destinationEvidence.SessionRetirementCount);
            Assert.AreEqual(1, destinationEvidence.SessionDisposalCount);
            Assert.IsNull(gallery.ActiveHost);
            Assert.IsNull(gallery.HostFrame.Content);
        }
        finally
        {
            gallery.HostFrame.Navigated -= handler;
            CloseWindow(window);
        }
    }

    [UITestMethod]
    public async Task Runner_ThrowingCheckpointReleaseUnsubscribesSnapshotWaiter()
    {
        ValidatedModelInspectionFixture fixture = LoadCatalogue()
            .Catalogue.Fixtures.Single(item => item.Id == "MI-002");
        var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: false);
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false);
        try
        {
            var viewModel = page.ViewModel!;
            int before = PropertyChangedSubscriberCount(viewModel);

            ApplicationException error = await Assert.ThrowsExactlyAsync<
                ApplicationException>(() =>
                    ModelInspectionFixtureScenarioRunner
                        .ReleaseAndWaitForSnapshotAsync(
                            viewModel,
                            static _ => false,
                            static () => throw new ApplicationException(
                                "release"),
                            CancellationToken.None));

            Assert.AreEqual("release", error.Message);
            Assert.AreEqual(before, PropertyChangedSubscriberCount(viewModel),
                "A failed checkpoint release must not retain its snapshot waiter.");
        }
        finally
        {
            page.RetireForFixture();
            session.Dispose();
        }
    }

    [TestMethod]
    public void GalleryTypes_HaveRequiredCampaignAttributes()
    {
        Type[] testClasses = [typeof(ModelInspectionFixtureGalleryTests)];
        foreach (Type type in testClasses)
        {
            Assert.IsTrue(type.GetCustomAttributes<TestCategoryAttribute>()
                .Any(attribute => attribute.TestCategories.Contains(
                    "ModelInspectionFixtureGallery", StringComparer.Ordinal)),
                type.Name);
        }

        Type[] serialized =
        [
            typeof(ModelInspectionFixtureGalleryTests),
            typeof(ModelInspectionFixturePageLifecycleTests),
            typeof(ModelInspectionFixtureViewModelIntegrationTests)
        ];
        foreach (Type type in serialized)
        {
            Assert.IsNotNull(type.GetCustomAttribute<DoNotParallelizeAttribute>(),
                type.Name);
        }
    }

    private static async Task<Window> ShowAndLoadAsync(
        ModelInspectionFixtureGalleryPage gallery)
    {
        var window = new Window { Content = gallery };
        window.Activate();
        await gallery.CatalogueLoaded;
        await DrainAsync(gallery);
        return window;
    }

    private static async Task DrainAsync(FrameworkElement element)
    {
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(element.DispatcherQueue.TryEnqueue(
            () => drained.TrySetResult(true)));
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task<Window> ShowHostAsync(
        ModelInspectionFixtureHostPage host)
    {
        var window = new Window { Content = host };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (host.IsLoaded)
        {
            loaded.TrySetResult(true);
        }
        else
        {
            host.Loaded += (_, _) => loaded.TrySetResult(true);
        }

        window.Activate();
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await DrainAsync(host);
        return window;
    }

    private static void CloseWindow(Window window)
    {
        window.Content = null;
        window.Close();
    }

    private static int PropertyChangedSubscriberCount(object owner)
    {
        FieldInfo field = owner.GetType().GetField(
            "PropertyChanged",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (field.GetValue(owner) as MulticastDelegate)?
            .GetInvocationList().Length ?? 0;
    }

    private static async Task WaitForGalleryPropertyAsync(
        ModelInspectionFixtureGalleryViewModel viewModel,
        string propertyName)
    {
        var changed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, arguments) =>
        {
            if (string.Equals(
                    arguments.PropertyName,
                    propertyName,
                    StringComparison.Ordinal))
            {
                changed.TrySetResult(true);
            }
        };
        viewModel.PropertyChanged += handler;
        try
        {
            await changed.Task;
        }
        finally
        {
            viewModel.PropertyChanged -= handler;
        }
    }

    private static async Task WaitWithContextAsync(
        Task operation,
        string context)
    {
        try
        {
            await operation.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Timed out waiting for {context}.");
        }
    }

    private static void Invoke(Button button)
    {
        var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(
            button);
        Assert.IsInstanceOfType<
            Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider>(
            peer.GetPattern(
                Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke))
            .Invoke();
    }

    private static void InvokeGalleryNavigation(
        ModelInspectionFixtureGalleryPage gallery,
        string methodName,
        object parameter)
    {
        NavigationEventArgs navigation = CreateNavigationEventArgs(parameter);
        MethodInfo method = typeof(ModelInspectionFixtureGalleryPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            method.Invoke(gallery, [navigation]);
        }
        catch (TargetInvocationException error)
            when (error.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(error.InnerException)
                .Throw();
        }
    }

    private static NavigationEventArgs CreateNavigationEventArgs(
        object parameter)
    {
        NavigationEventArgs? captured = null;
        var frame = new Frame();
        frame.Navigated += (_, eventArguments) => captured = eventArguments;
        Assert.IsTrue(frame.Navigate(typeof(Page), parameter));
        return captured ?? throw new AssertFailedException(
            "Navigation arguments were not captured.");
    }

    private static ModelInspectionRequest CreateRequest(string path) =>
        new(
            path,
            Path.GetFileName(path),
            new ExpectedModelFileIdentity(
                lengthBytes: 42,
                lastWriteTimeUtc: DateTimeOffset.UnixEpoch),
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite Fixture Model",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 42,
                declaredContextLength: 131_072,
                ggufVersion: 3));

    private static void RaiseModelInspectionRequestedIfSubscribed(
        ModelImportPage page,
        ModelInspectionRequest request)
    {
        FieldInfo? field = typeof(ModelImportPage).GetField(
            "ModelInspectionRequested",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = field?.GetValue(page) as
            EventHandler<ModelInspectionRequestedEventArgs>;
        handler?.Invoke(page, new ModelInspectionRequestedEventArgs(request));
    }

    private static ValidatedModelInspectionFixtureInput CloneInput(
        ValidatedModelInspectionFixtureInput source,
        IReadOnlyList<ModelInspectionFixtureSetupStepDescriptor> steps)
    {
        ConstructorInfo constructor = typeof(
            ValidatedModelInspectionFixtureInput).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(ModelInspectionFixtureInput)],
            modifiers: null)!;
        return (ValidatedModelInspectionFixtureInput)constructor.Invoke(
        [
            new ModelInspectionFixtureInput(
                source.Request,
                source.Attempts,
                steps,
                source.ObservationCheckpoint)
        ]);
    }

    private static ValidatedModelInspectionFixtureCoverageCatalogue
        LoadCatalogue() => new ModelInspectionFixturePackageLoader(
            CountingReader.Valid()).LoadAsync().GetAwaiter().GetResult();

    internal static string[] ExpectedPackageUris()
    {
        string root = FixtureRoot();
        string policy = "model-inspection-fixture-coverage-policy.json";
        string[] descriptorNames = Directory.GetFiles(root,
                "MI-*.fixture.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
        return new[]
        {
            PackageRoot + "model-inspection-fixture.schema.json",
            PackageRoot + policy
        }.Concat(descriptorNames.Select(name => PackageRoot + name)).ToArray();
    }

    private static string FixtureRoot() => Path.Combine(
        FindRepositoryRoot(), "tests", "TestFixtures",
        "ModelInspectionScenarios");

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        DirectoryInfo? cursor = new FileInfo(source).Directory;
        while (cursor is not null && !File.Exists(Path.Combine(
                   cursor.FullName,
                   "IBM Granite with TurboQuant (Intel).slnx")))
        {
            cursor = cursor.Parent;
        }

        return cursor?.FullName ?? throw new AssertFailedException(
            "Repository root was not found.");
    }

    private static IEnumerable<Type> SelfAndNested(Type root)
    {
        yield return root;
        foreach (Type nested in root.GetNestedTypes(
                     BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (Type item in SelfAndNested(nested))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<Type> DeclaredSignatureTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        if (type.BaseType is Type baseType)
        {
            yield return baseType;
        }

        foreach (Type implemented in type.GetInterfaces())
        {
            yield return implemented;
        }

        foreach (Type genericParameter in type.GetGenericArguments().Where(
                     argument => argument.IsGenericParameter))
        {
            foreach (Type constraint in genericParameter
                         .GetGenericParameterConstraints())
            {
                yield return constraint;
            }
        }

        foreach (FieldInfo field in type.GetFields(flags))
        {
            yield return field.FieldType;
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            yield return property.PropertyType;
            foreach (ParameterInfo parameter in property.GetIndexParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (EventInfo declaredEvent in type.GetEvents(flags))
        {
            if (declaredEvent.EventHandlerType is Type eventHandler)
            {
                yield return eventHandler;
            }
        }

        foreach (MethodInfo method in type.GetMethods(flags))
        {
            yield return method.ReturnType;
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
            foreach (Type genericParameter in method.GetGenericArguments().Where(
                         argument => argument.IsGenericParameter))
            {
                foreach (Type constraint in genericParameter
                             .GetGenericParameterConstraints())
                {
                    yield return constraint;
                }
            }
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(flags))
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static bool ContainsType(Type candidate, Type forbidden)
    {
        if (candidate.IsByRef || candidate.IsPointer || candidate.IsArray)
        {
            return ContainsType(candidate.GetElementType()!, forbidden);
        }

        return candidate == forbidden ||
            candidate.IsGenericType && candidate.GetGenericArguments()
                .Any(argument => ContainsType(argument, forbidden));
    }

    private static bool ContainsNamespaceType(Type candidate, string root)
    {
        if (candidate.IsByRef || candidate.IsPointer || candidate.IsArray)
        {
            return ContainsNamespaceType(candidate.GetElementType()!, root);
        }

        if (string.Equals(candidate.Namespace, root, StringComparison.Ordinal) ||
            candidate.Namespace?.StartsWith(
                root + ".",
                StringComparison.Ordinal) is true)
        {
            return true;
        }

        return candidate.IsGenericType && candidate.GetGenericArguments()
            .Any(argument => ContainsNamespaceType(argument, root));
    }

    private static bool HasExactVisibleSplitGalleryLayout(string xaml)
    {
        System.Xml.Linq.XDocument document;
        try
        {
            document = System.Xml.Linq.XDocument.Parse(xaml);
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }

        System.Xml.Linq.XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        System.Xml.Linq.XElement[] roots = document.Root?
            .Elements(presentation + "Grid").ToArray() ?? [];
        if (roots.Length != 1)
        {
            return false;
        }

        System.Xml.Linq.XElement root = roots[0];
        System.Xml.Linq.XElement[] columnDefinitions = root
            .Elements(presentation + "Grid.ColumnDefinitions")
            .SelectMany(definitions => definitions.Elements(
                presentation + "ColumnDefinition"))
            .ToArray();
        if (columnDefinitions.Length != 2 ||
            !string.Equals(root.Attribute("Padding")?.Value,
                "24", StringComparison.Ordinal) ||
            !string.Equals(root.Attribute("ColumnSpacing")?.Value,
                "24", StringComparison.Ordinal) ||
            !string.Equals(
                columnDefinitions[0].Attribute("Width")?.Value,
                "320",
                StringComparison.Ordinal) ||
            !string.Equals(
                columnDefinitions[1].Attribute("Width")?.Value,
                "*",
                StringComparison.Ordinal))
        {
            return false;
        }

        System.Xml.Linq.XElement[] panes = root
            .Elements(presentation + "Grid").ToArray();
        if (panes.Length != 2)
        {
            return false;
        }

        static string? Attribute(
            System.Xml.Linq.XElement element,
            string localName) => element.Attributes().SingleOrDefault(
                attribute => string.Equals(
                    attribute.Name.LocalName,
                    localName,
                    StringComparison.Ordinal))?.Value;
        System.Xml.Linq.XElement[] left = panes.Where(pane => string.Equals(
            Attribute(pane, "Grid.Column"),
            "0",
            StringComparison.Ordinal)).ToArray();
        System.Xml.Linq.XElement[] right = panes.Where(pane => string.Equals(
            Attribute(pane, "Grid.Column"),
            "1",
            StringComparison.Ordinal)).ToArray();
        return left.Length == 1 && right.Length == 1 && panes.All(pane =>
            Attribute(pane, "Visibility") is null or "Visible");
    }

    private static string? ForbiddenDebugSourceReference(string source)
    {
        (string Identity, string Pattern)[] forbidden =
        [
            ("System.IO namespace", @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?System\s*\.\s*IO(?![A-Za-z0-9_])"),
            ("System.IO.File", @"(?<![A-Za-z0-9_])File(?![A-Za-z0-9_])"),
            ("System.IO.Directory", @"(?<![A-Za-z0-9_])Directory(?![A-Za-z0-9_])"),
            ("System.IO.FileStream", @"(?<![A-Za-z0-9_])FileStream(?![A-Za-z0-9_])"),
            ("System.IO.FileInfo", @"(?<![A-Za-z0-9_])FileInfo(?![A-Za-z0-9_])"),
            ("System.IO.DirectoryInfo", @"(?<![A-Za-z0-9_])DirectoryInfo(?![A-Za-z0-9_])"),
            ("System.IO.StreamReader", @"(?<![A-Za-z0-9_])StreamReader(?![A-Za-z0-9_])"),
            ("System.IO.StreamWriter", @"(?<![A-Za-z0-9_])StreamWriter(?![A-Za-z0-9_])"),
            ("System.IO.FileSystemWatcher", @"(?<![A-Za-z0-9_])FileSystemWatcher(?![A-Za-z0-9_])"),
            ("System.IO.DriveInfo", @"(?<![A-Za-z0-9_])DriveInfo(?![A-Za-z0-9_])"),
            ("System.IO.Compression.ZipFile", @"(?<![A-Za-z0-9_])ZipFile(?![A-Za-z0-9_])"),
            ("System.IO.MemoryMappedFiles.MemoryMappedFile", @"(?<![A-Za-z0-9_])MemoryMappedFile(?![A-Za-z0-9_])"),
            ("System.Diagnostics.Process", @"(?<![A-Za-z0-9_])Process(?![A-Za-z0-9_])"),
            ("System.Net", @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?System\s*\.\s*Net(?![A-Za-z0-9_])"),
            ("HttpClient", @"(?<![A-Za-z0-9_])HttpClient(?![A-Za-z0-9_])"),
            ("Socket", @"(?<![A-Za-z0-9_])Socket(?![A-Za-z0-9_])"),
            ("Windows.Networking", @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?Windows\s*\.\s*Networking(?![A-Za-z0-9_])"),
            ("StreamSocket", @"(?<![A-Za-z0-9_])StreamSocket(?![A-Za-z0-9_])"),
            ("Windows.Storage namespace", @"(?<![A-Za-z0-9_])(?:global\s*::\s*)?Windows\s*\.\s*Storage(?![A-Za-z0-9_])"),
            ("Windows.Storage.StorageFile", @"(?<![A-Za-z0-9_])StorageFile(?![A-Za-z0-9_])"),
            ("Windows.Storage.FileIO", @"(?<![A-Za-z0-9_])FileIO(?![A-Za-z0-9_])"),
            ("Windows.Storage.PathIO", @"(?<![A-Za-z0-9_])PathIO(?![A-Za-z0-9_])"),
            ("Windows.Storage.ApplicationData", @"(?<![A-Za-z0-9_])ApplicationData(?![A-Za-z0-9_])"),
            ("Windows.Storage.StorageFolder", @"(?<![A-Za-z0-9_])StorageFolder(?![A-Za-z0-9_])"),
            ("Windows.Storage.KnownFolders", @"(?<![A-Za-z0-9_])KnownFolders(?![A-Za-z0-9_])"),
            ("Windows.Storage path API", @"(?<![A-Za-z0-9_])Get(?:File|Folder)FromPathAsync(?![A-Za-z0-9_])"),
            ("Windows.Storage enumeration", @"(?<![A-Za-z0-9_])Get(?:Files|Folders|Items)Async(?![A-Za-z0-9_])"),
            ("Windows.Storage file access", @"(?<![A-Za-z0-9_])(?:GetFileAsync|ReadTextAsync|OpenReadAsync)(?![A-Za-z0-9_])"),
            ("Windows.Storage package URI API", @"(?<![A-Za-z0-9_])GetFileFromApplicationUriAsync(?![A-Za-z0-9_])"),
            ("Windows.Storage package buffer API", @"(?<![A-Za-z0-9_])ReadBufferAsync(?![A-Za-z0-9_])"),
            ("FileOpenPicker", @"(?<![A-Za-z0-9_])FileOpenPicker(?![A-Za-z0-9_])"),
            ("FolderPicker", @"(?<![A-Za-z0-9_])FolderPicker(?![A-Za-z0-9_])"),
            ("ModelInspectionWorkerComposition", @"(?<![A-Za-z0-9_])ModelInspectionWorkerComposition(?![A-Za-z0-9_])"),
            ("ModelInspectionServiceComposition", @"(?<![A-Za-z0-9_])ModelInspectionServiceComposition(?![A-Za-z0-9_])"),
            ("Task.Delay", @"(?<![A-Za-z0-9_])Task\s*\.\s*Delay(?![A-Za-z0-9_])")
        ];
        foreach ((string identity, string pattern) in forbidden)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    source,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                return identity;
            }
        }

        const string QualifiedPage =
            @"(?:(?:global\s*::\s*)?[A-Za-z_][A-Za-z0-9_]*\s*(?:\.|::)\s*)*ModelInspectionPage";
        if (System.Text.RegularExpressions.Regex.IsMatch(
                source,
                @"\bnew\s+" + QualifiedPage + @"\s*(?:\(\s*\)|(?=\{))",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return "public ModelInspectionPage()";
        }

        foreach (System.Text.RegularExpressions.Match alias in
                 System.Text.RegularExpressions.Regex.Matches(
                     source,
                     @"\busing\s+(?<alias>@?[A-Za-z_][A-Za-z0-9_]*)\s*=\s*" +
                        QualifiedPage + @"\s*;",
                     System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            string aliasName = System.Text.RegularExpressions.Regex.Escape(
                alias.Groups["alias"].Value);
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    source,
                    @"\bnew\s+" + aliasName + @"\s*(?:\(\s*\)|(?=\{))",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                return "public ModelInspectionPage()";
            }
        }

        return null;
    }

    private static bool HasGalleryOwnedDestinationRegistration(string source) =>
        source.Contains(
            "FixtureHostFrame.Navigated +=",
            StringComparison.Ordinal) ||
        System.Text.RegularExpressions.Regex.IsMatch(
            source,
            @"\bpending\s*\.\s*RegisterDestination\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string MaskExactPackageReaderSourceCalls(string source)
    {
        const string ClassMarker =
            "internal sealed class ModelInspectionFixturePackageResourceReader";
        const string MethodMarker =
            "public async Task<ReadOnlyMemory<byte>> ReadAsync";
        const string UriRead = "GetFileFromApplicationUriAsync";
        const string BufferRead = "ReadBufferAsync";
        const string StorageUsing = "using Windows.Storage;";
        int classOffset = source.IndexOf(ClassMarker, StringComparison.Ordinal);
        Assert.IsTrue(classOffset >= 0,
            "The exact package-resource reader class was not found.");
        Assert.AreEqual(classOffset, source.LastIndexOf(
            ClassMarker,
            StringComparison.Ordinal));
        int methodOffset = source.IndexOf(
            MethodMarker,
            classOffset,
            StringComparison.Ordinal);
        Assert.IsTrue(methodOffset > classOffset,
            "The exact package-resource ReadAsync method was not found.");
        Assert.AreEqual(methodOffset, source.LastIndexOf(
            MethodMarker,
            StringComparison.Ordinal));
        int openingBrace = source.IndexOf('{', methodOffset);
        Assert.IsTrue(openingBrace > methodOffset);
        int closingBrace = FindMatchingBrace(source, openingBrace);
        string methodBody = source.Substring(
            openingBrace,
            checked(closingBrace - openingBrace + 1));
        Assert.AreEqual(1, CountOrdinal(source, UriRead),
            "The application-URI package API must have exactly one source call.");
        Assert.AreEqual(1, CountOrdinal(source, BufferRead),
            "The package buffer API must have exactly one source call.");
        Assert.AreEqual(1, CountOrdinal(methodBody, UriRead));
        Assert.AreEqual(1, CountOrdinal(methodBody, BufferRead));
        Assert.AreEqual(1, CountOrdinal(source, StorageUsing),
            "The exact reader must own the sole Windows.Storage import.");

        const string UriCall =
            @"(?<![A-Za-z0-9_])StorageFile\s*\.\s*GetFileFromApplicationUriAsync";
        const string BufferCall =
            @"(?<![A-Za-z0-9_])FileIO\s*\.\s*ReadBufferAsync";
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(
            methodBody,
            UriCall,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant).Count);
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(
            methodBody,
            BufferCall,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant).Count);
        string masked = System.Text.RegularExpressions.Regex.Replace(
            methodBody,
            UriCall,
            "AllowedExactPackageUriRead",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        masked = System.Text.RegularExpressions.Regex.Replace(
            masked,
            BufferCall,
            "AllowedExactPackageBufferRead",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return (source[..openingBrace] + masked + source[(closingBrace + 1)..])
            .Replace(
                StorageUsing,
                "using AllowedExactPackageResourceApis;",
                StringComparison.Ordinal);
    }

    private static int FindMatchingBrace(string source, int openingBrace)
    {
        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return index;
            }
        }

        throw new AssertFailedException(
            "The exact package-resource ReadAsync body was not closed.");
    }

    private static int CountOrdinal(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(
                    value,
                    offset,
                    StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static bool IsForbiddenDebugIlReference(MemberInfo reference)
    {
        if (reference is Type referencedType)
        {
            return IsForbiddenDebugIlReference(
                referencedType.FullName ?? referencedType.Name,
                referencedType.Name,
                ".type",
                isConstructor: false,
                isPublic: referencedType.IsPublic,
                parameterCount: 0);
        }

        Type? declaringType = reference.DeclaringType;
        return declaringType is not null && IsForbiddenDebugIlReference(
            declaringType.FullName ?? declaringType.Name,
            declaringType.Name,
            reference.Name,
            reference is ConstructorInfo,
            reference is ConstructorInfo constructor && constructor.IsPublic,
            reference is MethodBase method
                ? method.GetParameters().Length
                : 0);
    }

    private static bool IsExactScreenComparerExpectedSite(
        MethodBase owner,
        MemberInfo reference)
    {
        Type? referencedType = reference as Type ?? reference.DeclaringType;
        if (referencedType is null ||
            !(referencedType.FullName ?? referencedType.Name).Contains(
                "ModelInspectionExpected",
                StringComparison.Ordinal))
        {
            return false;
        }

        for (Type? candidate = owner.DeclaringType;
             candidate is not null;
             candidate = candidate.DeclaringType)
        {
            if (candidate == typeof(ModelInspectionFixtureScreenComparer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsForbiddenDebugIlReference(
        string typeName,
        string simpleName,
        string memberName,
        bool isConstructor,
        bool isPublic,
        int parameterCount)
    {
        static bool IsTypeOrNested(string candidate, string forbidden) =>
            string.Equals(candidate, forbidden, StringComparison.Ordinal) ||
            candidate.StartsWith(forbidden + "+", StringComparison.Ordinal);

        bool forbiddenApi =
            typeName.StartsWith("System.IO.", StringComparison.Ordinal) ||
            IsTypeOrNested(typeName, "System.Diagnostics.Process") ||
            typeName.StartsWith("System.Net.", StringComparison.Ordinal) ||
            typeName.StartsWith("Windows.Networking.", StringComparison.Ordinal) ||
            typeName.StartsWith("Windows.Storage.", StringComparison.Ordinal) ||
            string.Equals(simpleName, "HttpClient", StringComparison.Ordinal) ||
            string.Equals(simpleName, "Socket", StringComparison.Ordinal) ||
            simpleName.Contains("FileOpenPicker", StringComparison.Ordinal) ||
            simpleName.Contains("FolderPicker", StringComparison.Ordinal) ||
            typeName.Contains("ModelInspectionWorkerComposition",
                StringComparison.Ordinal) ||
            typeName.Contains("ModelInspectionServiceComposition",
                StringComparison.Ordinal) ||
            typeName.Contains("ModelInspectionExpected", StringComparison.Ordinal) ||
            string.Equals(typeName, "System.Threading.Tasks.Task",
                StringComparison.Ordinal) &&
                string.Equals(memberName, "Delay", StringComparison.Ordinal);
        if (forbiddenApi)
        {
            return true;
        }

        return isConstructor && isPublic && parameterCount == 0 &&
            string.Equals(typeName, typeof(ModelInspectionPage).FullName,
                StringComparison.Ordinal);
    }

    private static bool IsPackageResourceApi(MemberInfo reference) =>
        IsPackageResourceApi(
            reference,
            "Windows.Storage.StorageFile",
            "GetFileFromApplicationUriAsync") ||
        IsPackageResourceApi(
            reference,
            "Windows.Storage.FileIO",
            "ReadBufferAsync");

    private static bool IsPackageResourceApi(
        MemberInfo reference,
        string declaringType,
        string memberName) =>
        string.Equals(
            reference.DeclaringType?.FullName,
            declaringType,
            StringComparison.Ordinal) &&
        string.Equals(reference.Name, memberName, StringComparison.Ordinal);

    private static bool IsWindowsStorageReference(MemberInfo reference)
    {
        Type? referencedType = reference as Type ?? reference.DeclaringType;
        return referencedType is not null &&
            (string.Equals(
                 referencedType.Namespace,
                 "Windows.Storage",
                 StringComparison.Ordinal) ||
             referencedType.Namespace?.StartsWith(
                 "Windows.Storage.",
                 StringComparison.Ordinal) is true);
    }

    private static bool IsExactPackageReaderOwner(MethodBase owner) =>
        owner.DeclaringType == ExactPackageReaderStateMachineType() &&
        string.Equals(owner.Name, "MoveNext", StringComparison.Ordinal);

    private static Type ExactPackageReaderStateMachineType()
    {
        MethodInfo read = typeof(ModelInspectionFixturePackageResourceReader)
            .GetMethod(
                nameof(ModelInspectionFixturePackageResourceReader.ReadAsync),
                BindingFlags.Instance | BindingFlags.Public)!;
        return read.GetCustomAttribute<
                System.Runtime.CompilerServices.AsyncStateMachineAttribute>()?
            .StateMachineType ?? throw new AssertFailedException(
                "The exact package-resource reader is not an async state machine.");
    }

    private static bool IsForbiddenPackageResourceCallSite(
        MethodBase owner,
        MemberInfo reference) =>
        IsWindowsStorageReference(reference) &&
        !(IsPackageResourceApi(reference) && IsExactPackageReaderOwner(owner));

    private static bool IsForbiddenPackageResourceCallSite(
        string ownerType,
        string ownerMethod,
        string referencedType,
        string referencedMember)
    {
        bool storageReference = referencedType.StartsWith(
            "Windows.Storage.",
            StringComparison.Ordinal);
        bool exactApi =
            string.Equals(
                referencedType,
                "Windows.Storage.StorageFile",
                StringComparison.Ordinal) &&
            string.Equals(
                referencedMember,
                "GetFileFromApplicationUriAsync",
                StringComparison.Ordinal) ||
            string.Equals(
                referencedType,
                "Windows.Storage.FileIO",
                StringComparison.Ordinal) &&
            string.Equals(
                referencedMember,
                "ReadBufferAsync",
                StringComparison.Ordinal);
        bool exactOwner = string.Equals(
                ownerType,
                ExactPackageReaderStateMachineType().FullName,
                StringComparison.Ordinal) &&
            string.Equals(ownerMethod, "MoveNext", StringComparison.Ordinal);
        return storageReference && !(exactApi && exactOwner);
    }

    private static bool IsDebugFixtureNamespace(string? value)
    {
        const string Root =
            "GraniteEdgeAI.Features.ModelInspection.DebugFixtures";
        return string.Equals(value, Root, StringComparison.Ordinal) ||
            value?.StartsWith(Root + ".", StringComparison.Ordinal) is true;
    }

    private static IEnumerable<MemberInfo> ReachableIlReferences(Type root)
        => IlReferences(SelfAndNested(root));

    private static IEnumerable<MemberInfo> IlReferences(
        IEnumerable<Type> types) =>
        IlReferenceSites(types).Select(site => site.Reference);

    private static IEnumerable<(MethodBase Owner, MemberInfo Reference)>
        IlReferenceSites(IEnumerable<Type> types)
    {
        foreach (Type type in types)
        {
            foreach (MethodBase method in DeclaredMethodBodies(type))
            {
                if (method.GetMethodBody()?.GetILAsByteArray() is not byte[] il)
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
                    }

                    if (member is not null)
                    {
                        yield return (method, member);
                    }
                }
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
            foreach (MethodBase method in DeclaredMethodBodies(type))
            {
                if (method.GetMethodBody()?.GetILAsByteArray() is not byte[] il)
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
                    }

                    if (member is ConstructorInfo constructor)
                    {
                        yield return (method, constructor);
                    }
                }
            }
        }
    }

    private static IEnumerable<MethodBase> DeclaredMethodBodies(Type type) =>
        type.GetMethods(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic));

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
                ? MultiByte[il[offset++]]
                : SingleByte[il[offset - 1]];
            int size = OperandSize(opcode.OperandType, il, offset);
            if (opcode.OperandType is OperandType.InlineMethod or
                OperandType.InlineField or OperandType.InlineType or
                OperandType.InlineTok)
            {
                yield return (opcode, BitConverter.ToInt32(il, offset));
            }

            offset += size;
        }
    }

    private static int OperandSize(OperandType kind, byte[] il, int offset) =>
        kind switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or
                OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or
                OperandType.InlineI or OperandType.InlineMethod or
                OperandType.InlineSig or OperandType.InlineString or
                OperandType.InlineTok or OperandType.InlineType or
                OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                4 + (BitConverter.ToInt32(il, offset) * 4),
            _ => throw new InvalidOperationException()
        };

    private static OpCode[] BuildOpCodes(bool multi)
    {
        var result = new OpCode[256];
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode opcode)
            {
                ushort value = unchecked((ushort)opcode.Value);
                if (multi == value > byte.MaxValue)
                {
                    result[value & byte.MaxValue] = opcode;
                }
            }
        }

        return result;
    }

    private static readonly OpCode[] SingleByte = BuildOpCodes(false);
    private static readonly OpCode[] MultiByte = BuildOpCodes(true);

    private sealed class ImmediateAnimationDriver :
        IModelInspectionAnimationDriver
    {
        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            completed(key);

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            completed(key);

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            completed(key);

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            completed(key);

        public void CancelAll()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class GatedScenarioRunner :
        IModelInspectionFixtureScenarioRunner
    {
        private readonly TaskCompletionSource<bool> firstStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> secondStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> firstRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> secondRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int callCount;

        internal Task FirstStarted => firstStarted.Task;

        internal Task SecondStarted => secondStarted.Task;

        internal void ReleaseFirstLate() => firstRelease.TrySetResult(true);

        internal void ReleaseSecond() => secondRelease.TrySetResult(true);

        public async Task<string> RunAsync(
            ValidatedModelInspectionFixtureInput input,
            ModelInspectionPage page,
            ModelInspectionFixtureSession session,
            CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref callCount);
            if (call == 1)
            {
                firstStarted.TrySetResult(true);
                await firstRelease.Task;
            }
            else
            {
                secondStarted.TrySetResult(true);
                await secondRelease.Task;
            }

            return input.ObservationCheckpoint;
        }
    }

    private sealed class ImmediateScenarioRunner :
        IModelInspectionFixtureScenarioRunner
    {
        public Task<string> RunAsync(
            ValidatedModelInspectionFixtureInput input,
            ModelInspectionPage page,
            ModelInspectionFixtureSession session,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(input.ObservationCheckpoint);
        }
    }

    private sealed class GatedCatalogueReader :
        IModelInspectionFixturePackageResourceReader
    {
        private readonly CountingReader inner = CountingReader.Valid();
        private readonly TaskCompletionSource<bool> firstReadStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseFirstRead = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int readCount;

        internal Task FirstReadStarted => firstReadStarted.Task;

        internal IReadOnlyList<string> Requests => inner.Requests;

        internal int Count(string uri) => inner.Count(uri);

        internal void ReleaseFirstRead() =>
            releaseFirstRead.TrySetResult(true);

        public async Task<ReadOnlyMemory<byte>> ReadAsync(
            Uri packageUri,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref readCount) == 1)
            {
                firstReadStarted.TrySetResult(true);
                await releaseFirstRead.Task.WaitAsync(cancellationToken);
            }

            return await inner.ReadAsync(packageUri, cancellationToken);
        }
    }

    private sealed class CancelOnFinalReadReader :
        IModelInspectionFixturePackageResourceReader
    {
        private readonly CountingReader inner = CountingReader.Valid();
        private readonly CancellationTokenSource cancellation;

        internal CancelOnFinalReadReader(
            CancellationTokenSource cancellation) =>
            this.cancellation = cancellation;

        internal int ReadCount { get; private set; }

        public async Task<ReadOnlyMemory<byte>> ReadAsync(
            Uri packageUri,
            CancellationToken cancellationToken = default)
        {
            ReadOnlyMemory<byte> result = await inner.ReadAsync(
                packageUri,
                cancellationToken);
            ReadCount++;
            if (ReadCount == 51)
            {
                cancellation.Cancel();
            }

            return result;
        }
    }

    internal sealed class CountingReader :
        IModelInspectionFixturePackageResourceReader
    {
        private readonly Dictionary<string, byte[]> documents;
        private readonly HashSet<string> failingUris = new(
            StringComparer.Ordinal);

        private CountingReader(Dictionary<string, byte[]> documents) =>
            this.documents = documents;

        internal List<string> Requests { get; } = [];

        internal static CountingReader Valid()
        {
            string root = FixtureRoot();
            return new CountingReader(Directory.GetFiles(root, "*.json")
                .ToDictionary(
                    path => PackageRoot + Path.GetFileName(path),
                    File.ReadAllBytes,
                    StringComparer.Ordinal));
        }

        internal int Count(string uri) => Requests.Count(value =>
            string.Equals(value, uri, StringComparison.Ordinal));

        internal void Replace(string uri, byte[] bytes) =>
            documents[uri] = bytes;

        internal void ReplaceText(
            string uri,
            string oldValue,
            string newValue)
        {
            string source = Encoding.UTF8.GetString(documents[uri]);
            Assert.IsTrue(source.Contains(oldValue, StringComparison.Ordinal));
            documents[uri] = Encoding.UTF8.GetBytes(source.Replace(
                oldValue,
                newValue,
                StringComparison.Ordinal));
        }

        internal void ThrowOn(string uri) => failingUris.Add(uri);

        public Task<ReadOnlyMemory<byte>> ReadAsync(
            Uri packageUri,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string value = packageUri.OriginalString;
            Requests.Add(value);
            if (failingUris.Contains(value))
            {
                throw new InvalidOperationException("unsafe reader detail");
            }

            return Task.FromResult<ReadOnlyMemory<byte>>(
                documents.TryGetValue(value, out byte[]? bytes)
                    ? bytes
                    : throw new InvalidOperationException("missing"));
        }
    }
}
#endif
