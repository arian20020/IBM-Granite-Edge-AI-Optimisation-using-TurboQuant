#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
[TestCategory("ModelInspectionFixtureScreenContract")]
public sealed class ModelInspectionFixtureScreenContractTests
{
    private static readonly string[] ExactIds = Enumerable.Range(1, 49)
        .Select(value => $"MI-{value:000}")
        .ToArray();

    [UITestMethod]
    public async Task EveryDescriptor_UsesFreshRealPageAndMatchesObservedScreen()
    {
        using var culture = new CultureScope();
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        CollectionAssert.AreEqual(
            ExactIds,
            catalogue.Fixtures.Select(fixture => fixture.Id).ToArray());
        var observer = new ModelInspectionFixtureScreenObserver();
        var comparer = new ModelInspectionFixtureScreenComparer();

        foreach (ValidatedModelInspectionFixture fixture in catalogue.Fixtures)
        {
            using var session = new ModelInspectionFixtureSession(
                fixture.Input,
                animationsEnabled: true,
                () => new ImmediateAnimationDriver());
            var host = new ModelInspectionFixtureHostPage();
            var activation = new ModelInspectionFixtureHostActivation(
                fixture.Input,
                session,
                ModelInspectionFixtureGalleryPage
                    .ShouldStartInspectionOnLoaded(fixture));
            host.ActivateForTesting(activation);
            ModelInspectionPage page = host.ModelInspectionPage!;
            using IModelInspectionFixtureObservationSession observation =
                observer.Begin(page);
            var window = new Window { Content = host };

            try
            {
                Task<string> run = new ModelInspectionFixtureScenarioRunner()
                    .RunAsync(
                        fixture.Input,
                        page,
                        session,
                        CancellationToken.None);
                window.Activate();
                string checkpoint = await run;
                Assert.AreEqual(fixture.Input.ObservationCheckpoint, checkpoint,
                    fixture.FileName);

                ModelInspectionObservedScreen observed =
                    await observation.CaptureAsync(CancellationToken.None);
                IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
                    comparer.Compare(
                        fixture.FileName,
                        fixture.Expected,
                        observed,
                        catalogue.Policy.Value.CopyRegistry);

                Assert.AreEqual(0, differences.Count,
                    JoinDiagnostics(fixture.FileName, differences));
                AssertSetupInteractionEvidence(fixture, session.Evidence);
            }
            finally
            {
                host.RetireForTesting();
                window.Content = null;
                window.Close();
            }
        }
    }

    [TestMethod]
    public void ObserverBoundary_IsPageOnlyAndRecursivelyFixtureFree()
    {
        MethodInfo begin = typeof(IModelInspectionFixtureScreenObserver)
            .GetMethod(nameof(IModelInspectionFixtureScreenObserver.Begin))!;
        CollectionAssert.AreEqual(
            new[] { typeof(ModelInspectionPage) },
            begin.GetParameters().Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.AreEqual(
            typeof(IModelInspectionFixtureObservationSession),
            begin.ReturnType);
        MethodInfo capture = typeof(IModelInspectionFixtureObservationSession)
            .GetMethod(nameof(
                IModelInspectionFixtureObservationSession.CaptureAsync))!;
        CollectionAssert.AreEqual(
            new[] { typeof(CancellationToken) },
            capture.GetParameters().Select(parameter => parameter.ParameterType)
                .ToArray());
        Assert.AreEqual(
            typeof(Task<ModelInspectionObservedScreen>),
            capture.ReturnType);

        Type[] observerClosure =
        [
            typeof(IModelInspectionFixtureScreenObserver),
            typeof(IModelInspectionFixtureObservationSession),
            typeof(ModelInspectionFixtureScreenObserver),
            typeof(ModelInspectionObservedScreen)
        ];
        AssertNoFixtureDependency(observerClosure);

        Type comparer = typeof(ModelInspectionFixtureScreenComparer);
        Assert.IsTrue(AllReferencedTypes(comparer).Any(type =>
            type == typeof(ModelInspectionExpectedScreen)));
        Assert.IsFalse(observerClosure.Any(type =>
            AllReferencedTypes(type).Contains(
                typeof(ModelInspectionExpectedScreen))));
    }

    [TestMethod]
    public void Comparer_IsSoleExpectedConsumerAndCopyKeysRemainLogicalPolicy()
    {
        MethodInfo compare = typeof(IModelInspectionFixtureScreenComparer)
            .GetMethod(nameof(IModelInspectionFixtureScreenComparer.Compare))!;
        Type[] parameters = compare.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(string),
                typeof(ModelInspectionExpectedScreen),
                typeof(ModelInspectionObservedScreen),
                typeof(IReadOnlyDictionary<string, string>)
            },
            parameters);
        Assert.IsFalse(parameters.Any(type =>
            type == typeof(ModelInspectionFixtureDescriptor) ||
            type == typeof(ModelInspectionFixtureCoveragePolicy) ||
            type == typeof(ModelInspectionPresetExpectation)));

        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        foreach (ValidatedModelInspectionFixture fixture in catalogue.Fixtures)
        {
            foreach (ModelInspectionExpectedCopy copy in
                     ExpectedCopies(fixture.Expected))
            {
                Assert.IsTrue(catalogue.Policy.Value.CopyRegistry.TryGetValue(
                    copy.CopyKey,
                    out string? registered), copy.CopyKey);
                Assert.AreEqual(copy.DefaultText, registered, copy.CopyKey);
            }
        }

        Assert.IsFalse(
            typeof(ModelInspectionObservedScreen).GetProperties()
                .Any(property => property.Name.Contains(
                    "CopyKey", StringComparison.Ordinal)),
            "The observer reports visible default text, not logical copy keys.");
        string[] comparerOwnedLogicalPaths =
        [
            "$.Expected.*.CopyKey",
            "$.Expected.Model.DisplayFileName"
        ];
        Assert.AreEqual(2, comparerOwnedLogicalPaths.Length,
            "Copy-key registry membership and the display-file role mapping " +
            "are comparer-owned logical cross-checks; they " +
            "must never be described as direct visual reads.");
        Assert.IsFalse(typeof(ModelInspectionObservedScreen).GetProperties()
            .Any(property => comparerOwnedLogicalPaths.Any(path =>
                path.EndsWith(property.Name, StringComparison.Ordinal))));
    }

    [TestMethod]
    public void FooterBoundary_ContainsOnlyAggregatePageEmission()
    {
        const BindingFlags instanceMembers = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;

        Assert.IsNull(typeof(ModelInspectionExpectedFooter).GetProperty(
            "Rows",
            instanceMembers),
            "Page-only screen expectations must not declare fabricated " +
            "inspection-stage footer rows.");
        Assert.IsNotNull(typeof(ModelInspectionExpectedFooter).GetProperty(
            "Status",
            instanceMembers));
        Assert.IsNull(typeof(ModelInspectionObservedFooter).GetProperty(
            "Rows",
            instanceMembers),
            "The observer must report only the page's real shell-bound " +
            "aggregate footer emission.");
        Assert.IsNotNull(typeof(ModelInspectionObservedFooter).GetProperty(
            "Status",
            instanceMembers));
        Assert.IsFalse(typeof(ModelInspectionObservedScreen).Assembly.GetTypes()
            .Any(type => string.Equals(
                type.Name,
                "ModelInspectionObservedFooterRow",
                StringComparison.Ordinal)));
    }

    [UITestMethod]
    public async Task Observer_DetectsRealVisualTreeMutationsWithoutPresentationEcho()
    {
        using var culture = new CultureScope();
        foreach (VisualMutation mutation in VisualMutations())
        {
            ScreenFixture fixture = CreateScreenFixture(mutation.FixtureId);
            using (fixture)
            using (IModelInspectionFixtureObservationSession observation =
                   new ModelInspectionFixtureScreenObserver().Begin(fixture.Page))
            {
                await fixture.RunAsync();
                object? presentation = fixture.Page.CurrentPresentation;
                mutation.Apply(fixture.Page);
                Assert.AreSame(presentation, fixture.Page.CurrentPresentation,
                    mutation.Name);

                EventHandler<object>? maintainMutation =
                    mutation.MaintainThroughRendering
                        ? (_, _) => mutation.Apply(fixture.Page)
                        : null;
                ModelInspectionObservedScreen observed;
                if (maintainMutation is not null)
                {
                    CompositionTarget.Rendering += maintainMutation;
                }

                try
                {
                    observed = await observation.CaptureAsync(
                        CancellationToken.None);
                }
                finally
                {
                    if (maintainMutation is not null)
                    {
                        CompositionTarget.Rendering -= maintainMutation;
                    }
                }

                IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
                    new ModelInspectionFixtureScreenComparer().Compare(
                        fixture.Descriptor.FileName,
                        fixture.Descriptor.Expected,
                        observed,
                        fixture.Catalogue.Policy.Value.CopyRegistry);
                Assert.IsTrue(differences.Count > 0, mutation.Name);
            }
        }
    }

    [UITestMethod]
    public async Task Observer_ProjectsModesBadgesRowsAndFractionsFromLoadedTree()
    {
        using var culture = new CultureScope();
        List<string> unchanged = [];
        foreach (ObservedProjectionMutation mutation in
                 ObservedProjectionMutations())
        {
            using ScreenFixture fixture = CreateScreenFixture(mutation.FixtureId);
            using IModelInspectionFixtureObservationSession observation =
                new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
            await fixture.RunAsync();

            ModelInspectionObservedScreen before =
                await observation.CaptureAsync(CancellationToken.None);
            string baseline = mutation.Project(before);
            EventHandler<object> maintainMutation = (_, _) =>
                mutation.Apply(fixture.Page);
            ModelInspectionObservedScreen after;
            CompositionTarget.Rendering += maintainMutation;
            try
            {
                mutation.Apply(fixture.Page);
                after = await observation.CaptureAsync(
                    CancellationToken.None);
            }
            finally
            {
                CompositionTarget.Rendering -= maintainMutation;
            }
            if (string.Equals(
                    baseline,
                    mutation.Project(after),
                    StringComparison.Ordinal))
            {
                unchanged.Add(mutation.Name);
            }
        }

        Assert.AreEqual(
            0,
            unchanged.Count,
            "Observer projections ignored loaded-tree mutations: " +
            string.Join(", ", unchanged));
    }

    [UITestMethod]
    public async Task Observer_ProjectsAutomationControlTypesFromLoadedPeers()
    {
        using var culture = new CultureScope();
        IReadOnlyList<(string FixtureId, string ControlId,
            Func<ModelInspectionPage, FrameworkElement> Element)> cases =
        [
            ("MI-002", "model-card", page => FindNamed<InspectionModelCard>(
                page,
                "InspectionModelCardControl")),
            ("MI-014", "progress-list", page => FindNamed<ItemsRepeater>(
                page,
                "ProgressItemsRepeater")),
            ("MI-014", "progress-1", page => FirstProgressRow(page, 0)),
            ("MI-002", "hardware-fit", page => FindNamed<Button>(
                page,
                "PrimaryActionButton"))
        ];
        List<string> unchanged = [];

        foreach ((string fixtureId, string controlId,
                 Func<ModelInspectionPage, FrameworkElement> element) in cases)
        {
            using ScreenFixture fixture = CreateScreenFixture(fixtureId);
            using IModelInspectionFixtureObservationSession observation =
                new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
            await fixture.RunAsync();

            string before = (await observation.CaptureAsync(
                    CancellationToken.None)).Automation.Controls.Single(
                    control => string.Equals(
                        control.Id,
                        controlId,
                        StringComparison.Ordinal)).ControlType;
            AutomationProperties.SetAutomationControlType(
                element(fixture.Page),
                AutomationControlType.Pane);
            string after = (await observation.CaptureAsync(
                    CancellationToken.None)).Automation.Controls.Single(
                    control => string.Equals(
                        control.Id,
                        controlId,
                        StringComparison.Ordinal)).ControlType;
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                unchanged.Add(controlId);
            }
        }

        Assert.AreEqual(
            0,
            unchanged.Count,
            "Observer control types ignored loaded automation peers: " +
            string.Join(", ", unchanged));
    }

    [UITestMethod]
    public async Task ObserverAutomation_ProjectsLoadedLiveSettingAndComparerReportsExactPath()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-014");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        int controlIndex = fixture.Descriptor.Expected.Automation.Controls
            .Select((control, index) => new { control, index })
            .Single(value => string.Equals(
                value.control.Id,
                "progress-list",
                StringComparison.Ordinal))
            .index;
        ItemsRepeater progress = FindNamed<ItemsRepeater>(
            fixture.Page,
            "ProgressItemsRepeater");
        Assert.AreEqual(
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(progress),
            "The loaded progress-list owner must expose its declared live " +
            "setting before observer projection.");
        ModelInspectionObservedScreen baseline = await observation.CaptureAsync(
            CancellationToken.None);
        Assert.AreEqual(
            "Polite",
            baseline.Automation.Controls.Single(control => string.Equals(
                control.Id,
                "progress-list",
                StringComparison.Ordinal)).LiveSetting);

        object? presentation = fixture.Page.CurrentPresentation;
        void ApplyMutation() => AutomationProperties.SetLiveSetting(
            progress,
            AutomationLiveSetting.Assertive);
        ApplyMutation();
        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);

        EventHandler<object> maintainMutation = (_, _) => ApplyMutation();
        ModelInspectionObservedScreen observed;
        CompositionTarget.Rendering += maintainMutation;
        try
        {
            observed = await observation.CaptureAsync(CancellationToken.None);
        }
        finally
        {
            CompositionTarget.Rendering -= maintainMutation;
        }

        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        Assert.AreEqual(
            "Assertive",
            observed.Automation.Controls.Single(control => string.Equals(
                control.Id,
                "progress-list",
                StringComparison.Ordinal)).LiveSetting);
        IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
            new ModelInspectionFixtureScreenComparer().Compare(
                fixture.Descriptor.FileName,
                fixture.Descriptor.Expected,
                observed,
                fixture.Catalogue.Policy.Value.CopyRegistry);
        Assert.HasCount(1, differences);
        ModelInspectionFixtureScreenDifference difference = differences[0];
        Assert.IsTrue(string.Equals(
            $"$.Automation.Controls[{controlIndex}].LiveSetting",
            difference.ExpectedPath,
            StringComparison.Ordinal));
        Assert.AreEqual("Polite", difference.ExpectedToken);
        Assert.AreEqual("Assertive", difference.ObservedToken);
    }

    [TestMethod]
    public void ObserverAutomation_HasNoSyntheticPeerFallback()
    {
        Type[] syntheticPeers = AllReferencedTypes(
                typeof(ModelInspectionFixtureScreenObserver))
            .Where(type =>
                typeof(AutomationPeer).IsAssignableFrom(type) &&
                string.Equals(
                    type.Name,
                    "ObservationAutomationPeer",
                    StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            syntheticPeers.Length,
            "The observer must consume peers created by the loaded UI. It " +
            "must not fabricate a peer when the application exposes none.");
    }

    [UITestMethod]
    public async Task ObserverAutomation_ExcludesOwnerOutsideControlView()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-002");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        const string actionId = "hardware-fit";
        Button owner = FindNamed<Button>(
            fixture.Page,
            "PrimaryActionButton");
        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.IsNull(before.Model.Badge,
            "The detailed model tree exposes no visible status badge.");
        Assert.IsTrue(before.Automation.Controls.Any(control => string.Equals(
            control.Id,
            actionId,
            StringComparison.Ordinal)));
        Assert.IsNotNull(
            FrameworkElementAutomationPeer.FromElement(owner) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(owner),
            "The canonical action must have a real loaded Button peer.");
        Assert.AreNotEqual(
            AccessibilityView.Raw,
            AutomationProperties.GetAccessibilityView(owner));

        object? presentation = fixture.Page.CurrentPresentation;
        AutomationProperties.SetAccessibilityView(owner, AccessibilityView.Raw);
        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);

        Assert.IsFalse(after.Automation.Controls.Any(control => string.Equals(
                control.Id,
                actionId,
                StringComparison.Ordinal)),
            "An owner removed from the automation Control view must not be " +
            "reported as an observable automation control.");
    }

    [UITestMethod]
    public async Task Observer_DetailedBadgeIgnoresHiddenCompactStatusCopy()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-002");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        Assert.AreEqual(
            Visibility.Collapsed,
            FindNamed<Border>(fixture.Page, "CompactView").Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            FindNamed<Border>(fixture.Page, "DetailedView").Visibility);
        TextBlock hiddenStatus = FindNamed<TextBlock>(
            fixture.Page,
            "CompactStatusText");
        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.IsNull(before.Model.Badge,
            "The detailed model tree exposes no visible status badge.");
        const string mutatedText = "RESULT UNKNOWN";
        Assert.AreNotEqual(mutatedText, hiddenStatus.Text);
        object? presentation = fixture.Page.CurrentPresentation;
        EventHandler<object> maintainMutation = (_, _) =>
            hiddenStatus.Text = mutatedText;

        ModelInspectionObservedScreen after;
        CompositionTarget.Rendering += maintainMutation;
        try
        {
            hiddenStatus.Text = mutatedText;
            after = await observation.CaptureAsync(CancellationToken.None);
        }
        finally
        {
            CompositionTarget.Rendering -= maintainMutation;
        }

        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        Assert.AreEqual(mutatedText, hiddenStatus.Text);
        Assert.IsNull(
            after.Model.Badge,
            "A detailed-card badge must come from an effectively visible " +
            "cue, not from copy under the collapsed compact view.");
    }

    [UITestMethod]
    public async Task Observer_MetadataLabelFollowsVisibleCopyNotAutomationName()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-002");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        Border publisher = FindNamed<Border>(fixture.Page, "PublisherField");
        TextBlock visibleLabel =
            DescendantsForMutation<TextBlock>(publisher).First();
        string automationName = AutomationProperties.GetName(visibleLabel);
        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        ModelInspectionObservedMetadataField beforePublisher =
            before.Model.Metadata.Single(field => string.Equals(
                field.Id,
                "metadata-publisher",
                StringComparison.Ordinal));
        Assert.AreEqual(visibleLabel.Text, beforePublisher.Label);
        const string mutatedText = "Publisher";
        object? presentation = fixture.Page.CurrentPresentation;
        EventHandler<object> maintainMutation = (_, _) =>
            visibleLabel.Text = mutatedText;

        ModelInspectionObservedScreen after;
        CompositionTarget.Rendering += maintainMutation;
        try
        {
            visibleLabel.Text = mutatedText;
            after = await observation.CaptureAsync(CancellationToken.None);
        }
        finally
        {
            CompositionTarget.Rendering -= maintainMutation;
        }

        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        Assert.AreEqual(
            automationName,
            AutomationProperties.GetName(visibleLabel),
            "Only visible label copy is mutated by this oracle.");
        int publisherIndex = after.Model.Metadata
            .Select((field, index) => (field, index))
            .Single(item => string.Equals(
                item.field.Id,
                "metadata-publisher",
                StringComparison.Ordinal))
            .index;
        Assert.AreEqual(
            mutatedText,
            after.Model.Metadata[publisherIndex].Label,
            "Metadata label observation must follow the visible TextBlock.");
        Assert.AreNotEqual(
            beforePublisher.Label,
            after.Model.Metadata[publisherIndex].Label);
        IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
            new ModelInspectionFixtureScreenComparer().Compare(
                fixture.Descriptor.FileName,
                fixture.Descriptor.Expected,
                after,
                fixture.Catalogue.Policy.Value.CopyRegistry);
        Assert.IsTrue(differences.Any(difference => string.Equals(
                difference.ExpectedPath,
                $"$.Model.Metadata[{publisherIndex}].Label.DefaultText",
                StringComparison.Ordinal)),
            "The visible metadata-label mutation must produce a label diff.");
    }

    [UITestMethod]
    public async Task Observer_ActionOrderFollowsLoadedGridGeometry()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-009");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        string[] beforeIds = before.Actions.Items
            .Select(action => action.Id)
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { "technical-report", "choose-another" },
            beforeIds);
        StackPanel technicalReportHost = FindNamed<StackPanel>(
            fixture.Page,
            "SecondaryActionOneHost");
        StackPanel chooseAnotherHost = FindNamed<StackPanel>(
            fixture.Page,
            "PrimaryActionHost");
        object? presentation = fixture.Page.CurrentPresentation;
        void PutChooseAnotherFirst()
        {
            Grid.SetRow(technicalReportHost, 0);
            Grid.SetColumn(technicalReportHost, 2);
            Grid.SetColumnSpan(technicalReportHost, 1);
            Grid.SetRow(chooseAnotherHost, 0);
            Grid.SetColumn(chooseAnotherHost, 0);
            Grid.SetColumnSpan(chooseAnotherHost, 1);
        }
        EventHandler<object> maintainMutation = (_, _) =>
            PutChooseAnotherFirst();

        ModelInspectionObservedScreen after;
        CompositionTarget.Rendering += maintainMutation;
        try
        {
            PutChooseAnotherFirst();
            after = await observation.CaptureAsync(CancellationToken.None);
        }
        finally
        {
            CompositionTarget.Rendering -= maintainMutation;
        }

        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        Assert.AreEqual(0, Grid.GetColumn(chooseAnotherHost));
        Assert.AreEqual(2, Grid.GetColumn(technicalReportHost));
        CollectionAssert.AreEqual(
            beforeIds.Reverse().ToArray(),
            after.Actions.Items.Select(action => action.Id).ToArray(),
            "Observed action order must follow loaded visual geometry rather " +
            "than a hard-coded sequence of field names.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsRealizedNoncanonicalContentGeometry()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-002");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        FrameworkElement host = FindNamed<FrameworkElement>(
            fixture.Page,
            "InspectionContentHost");
        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        FrameworkElement scrollContent = FindNamed<FrameworkElement>(
            fixture.Page,
            "InspectionScrollContent");
        FrameworkElement reflowHost = FindNamed<FrameworkElement>(
            fixture.Page,
            "InspectionReflowHost");
        Assert.AreEqual(
            "Canonical",
            before.Figma.GeometryProfile,
            $"host={host.ActualWidth}x{host.ActualHeight}; " +
            $"scroll={scrollContent.ActualWidth}x" +
            $"{scrollContent.ActualHeight}; margin={host.Margin}; " +
            $"reflow={reflowHost.ActualWidth}x" +
            $"{reflowHost.ActualHeight}; reflowMargin=" +
            $"{reflowHost.Margin}; max={host.MaxWidth}; " +
            $"width={host.Width}; height={host.Height}");
        Assert.IsTrue(before.Figma.ContentWidth > 2d);
        Assert.IsTrue(before.Figma.ContentHeight > 2d);
        double mutatedWidth = before.Figma.ContentWidth / 2d;
        double mutatedHeight = before.Figma.ContentHeight / 2d;
        object? presentation = fixture.Page.CurrentPresentation;
        void ApplyNoncanonicalGeometry()
        {
            host.Width = mutatedWidth;
            host.Height = mutatedHeight;
        }
        EventHandler<object> maintainMutation = (_, _) =>
            ApplyNoncanonicalGeometry();

        ModelInspectionObservedScreen after;
        CompositionTarget.Rendering += maintainMutation;
        try
        {
            ApplyNoncanonicalGeometry();
            after = await observation.CaptureAsync(CancellationToken.None);
        }
        finally
        {
            CompositionTarget.Rendering -= maintainMutation;
        }

        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        Assert.IsTrue(host.ActualWidth > 0d);
        Assert.IsTrue(host.ActualHeight > 0d);
        Assert.AreNotEqual(before.Figma.ContentWidth, host.ActualWidth);
        Assert.AreNotEqual(before.Figma.ContentHeight, host.ActualHeight);
        Assert.AreEqual(host.ActualWidth, after.Figma.ContentWidth, 0d);
        Assert.AreEqual(host.ActualHeight, after.Figma.ContentHeight, 0d);
        Assert.AreNotEqual(
            "Canonical",
            after.Figma.GeometryProfile,
            "Positive dimensions alone do not prove canonical geometry.");

        IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
            new ModelInspectionFixtureScreenComparer().Compare(
                fixture.Descriptor.FileName,
                fixture.Descriptor.Expected,
                after,
                fixture.Catalogue.Policy.Value.CopyRegistry);
        Assert.IsTrue(differences.Any(difference => string.Equals(
                difference.ExpectedPath,
                "$.Figma.GeometryProfile",
                StringComparison.Ordinal)),
            "The comparer must identify the noncanonical geometry profile.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsCanonicalProgressFractions()
    {
        using var culture = new CultureScope();
        using (ScreenFixture fractionless = CreateScreenFixture("MI-014"))
        using (IModelInspectionFixtureObservationSession observation =
               new ModelInspectionFixtureScreenObserver().Begin(
                   fractionless.Page))
        {
            await fractionless.RunAsync();
            ModelInspectionObservedScreen observed =
                await observation.CaptureAsync(CancellationToken.None);
            Assert.IsNull(observed.Content.Rows[0].StageFraction,
                "MI-014 progress row 0 must remain indeterminate.");
        }

        using (ScreenFixture bounded = CreateScreenFixture("MI-028"))
        using (IModelInspectionFixtureObservationSession observation =
               new ModelInspectionFixtureScreenObserver().Begin(bounded.Page))
        {
            await bounded.RunAsync();
            ModelInspectionObservedScreen observed =
                await observation.CaptureAsync(CancellationToken.None);
            Assert.IsNotNull(observed.Content.Rows[1].StageFraction);
            Assert.AreEqual(
                0.75d,
                observed.Content.Rows[1].StageFraction!.Value,
                0d,
                "MI-028 progress row 1 must expose its exact bounded fraction.");
        }
    }

    [UITestMethod]
    public async Task Observer_ProjectsReorderedProgressRowIdentities()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-014");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen beforeScreen =
            await observation.CaptureAsync(CancellationToken.None);
        string[] before = beforeScreen.RowsAndScroll.OrderedRowIds.ToArray();
        string[] beforeAutomation = ProgressAutomationIds(beforeScreen);
        ReverseItems(FindNamed<ItemsRepeater>(
            fixture.Page,
            "ProgressItemsRepeater"));
        ModelInspectionObservedScreen afterScreen =
            await observation.CaptureAsync(CancellationToken.None);
        string[] after = afterScreen.RowsAndScroll.OrderedRowIds.ToArray();
        string[] afterAutomation = ProgressAutomationIds(afterScreen);

        CollectionAssert.AreEqual(
            before.Reverse().ToArray(),
            after,
            "Progress row identity order must follow the loaded ItemsSource.");
        CollectionAssert.AreEqual(
            beforeAutomation.Reverse().ToArray(),
            afterAutomation,
            "Progress automation identity order must follow the loaded rows.");
    }

    private static string[] ProgressAutomationIds(
        ModelInspectionObservedScreen screen) => screen.Automation.Controls
        .Select(control => control.Id)
        .Where(id => id.StartsWith("progress-", StringComparison.Ordinal) &&
            !string.Equals(id, "progress-list", StringComparison.Ordinal))
        .ToArray();

    [UITestMethod]
    public async Task Observer_ExcludesActionsUnderCollapsedActiveView()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-002");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        Button button = FindNamed<Button>(fixture.Page, "PrimaryActionButton");
        Border activeView = FindNamed<Border>(fixture.Page, "ResultView");
        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.IsTrue(before.Actions.Items.Any(action => string.Equals(
            action.Id,
            "hardware-fit",
            StringComparison.Ordinal)));
        Assert.AreEqual(Visibility.Visible, button.Visibility);

        activeView.Visibility = Visibility.Collapsed;
        Assert.AreEqual(
            Visibility.Visible,
            button.Visibility,
            "The descendant must remain locally visible for this oracle.");
        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);

        Assert.IsFalse(after.Actions.Items.Any(action => string.Equals(
                action.Id,
                "hardware-fit",
                StringComparison.Ordinal)),
            "A locally visible button under a collapsed view is not effective.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsModelCheckStatusFromVisibleIconAndTextTuple()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-003");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        string beforeStatus = before.Model.Checks.Single(check =>
            string.Equals(
                check.Id,
                "check-package",
                StringComparison.Ordinal)).Status;
        Border row = FirstModelCheckRow(fixture.Page);
        Grid statusOwner = Find<Grid>(row,
            grid => Grid.GetColumn(grid) == 2);
        TextBlock statusText = Find<TextBlock>(statusOwner,
            text => text.Visibility == Visibility.Visible);
        string unchangedText = statusText.Text;
        Border statusIcon = DescendantsForMutation<Border>(row).Single(border =>
            string.Equals(
                border.Tag as string,
                "InspectionCheckStatusIcon",
                StringComparison.Ordinal) &&
            border.Visibility == Visibility.Visible);
        Brush errorSurface = (Brush)Application.Current.Resources[
            "InspectionErrorSurfaceBrush"];
        Assert.AreNotSame(errorSurface, statusIcon.Background);
        statusIcon.Background = errorSurface;
        Assert.AreEqual(unchangedText, statusText.Text);

        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);
        string afterStatus = after.Model.Checks.Single(check => string.Equals(
            check.Id,
            "check-package",
            StringComparison.Ordinal)).Status;
        Assert.AreNotEqual(
            beforeStatus,
            afterStatus,
            "Model-check status must include the loaded status-icon visual.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsContentStatusFromVisibleMarkerAndTextTuple()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-004");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Border row = FirstFindingRow(fixture.Page);
        Border statusPill = DescendantsForMutation<Border>(row).Single(
            border => Grid.GetColumn(border) == 2);
        TextBlock statusText = Find<TextBlock>(statusPill);
        string unchangedText = statusText.Text;
        Border marker = DescendantsForMutation<Border>(row).Single(border =>
            border.Visibility == Visibility.Visible &&
            border.Child is SymbolIcon);
        var markerSymbol = (SymbolIcon)marker.Child;
        Brush errorSurface = (Brush)Application.Current.Resources[
            "InspectionErrorSurfaceBrush"];
        Assert.AreNotSame(errorSurface, marker.Background);
        Assert.AreNotEqual(Symbol.Cancel, markerSymbol.Symbol);

        marker.Background = errorSurface;
        markerSymbol.Symbol = Symbol.Cancel;
        Assert.AreEqual(unchangedText, statusText.Text);

        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.AreNotEqual(
            before.Content.Rows[0].Status,
            after.Content.Rows[0].Status,
            "Content status must include the visible marker brush/symbol " +
            "and status-text tuple.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsFindingDetailFromVisibleTextInsteadOfAutomationHelpText()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-004");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        object? presentation = fixture.Page.CurrentPresentation;
        Border row = FirstFindingRow(fixture.Page);
        StackPanel copy = Find<StackPanel>(row,
            panel => Grid.GetColumn(panel) == 1);
        TextBlock detail = DescendantsForMutation<TextBlock>(copy)
            .Single(text => text.Visibility == Visibility.Visible &&
                !string.Equals(
                    text.Text,
                    before.Content.Rows[0].PrimaryText,
                    StringComparison.Ordinal));
        string helpText = AutomationProperties.GetHelpText(row);
        const string mutation = " visible-tree mutation";
        string mutatedText = detail.Text + mutation;
        EventHandler<object> maintainMutation = (_, _) =>
            detail.Text = mutatedText;

        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        Assert.AreEqual(helpText, AutomationProperties.GetHelpText(row),
            "The accessibility presentation must remain unchanged for this " +
            "visible-text oracle.");
        ModelInspectionObservedScreen after;
        CompositionTarget.Rendering += maintainMutation;
        try
        {
            detail.Text = mutatedText;
            after = await observation.CaptureAsync(CancellationToken.None);
        }
        finally
        {
            CompositionTarget.Rendering -= maintainMutation;
        }

        Assert.AreEqual(mutatedText, detail.Text,
            "The loaded-tree mutation must remain applied through capture.");

        Assert.AreNotEqual(
            before.Content.Rows[0].SecondaryText,
            after.Content.Rows[0].SecondaryText,
            "Finding secondary text must follow the visible detail TextBlock.");
        Assert.AreEqual(detail.Text, after.Content.Rows[0].SecondaryText);
        IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
            new ModelInspectionFixtureScreenComparer().Compare(
                fixture.Descriptor.FileName,
                fixture.Descriptor.Expected,
                after,
                fixture.Catalogue.Policy.Value.CopyRegistry);
        Assert.IsTrue(differences.Any(difference => string.Equals(
                difference.ExpectedPath,
                "$.Content.Rows[0].SecondaryText.DefaultText",
                StringComparison.Ordinal)),
            "The visible detail mutation must produce a secondary-text diff.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsDetailedMetadataMembershipAndTreeOrder()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-044");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        string[] beforeIds = before.Model.Metadata
            .Select(field => field.Id)
            .ToArray();
        CollectionAssert.Contains(beforeIds, "metadata-publisher");
        CollectionAssert.Contains(beforeIds, "metadata-model-type");

        Grid metadata = FindNamed<Grid>(fixture.Page, "MetadataGrid");
        Border publisher = FindNamed<Border>(metadata, "PublisherField");
        Border modelType = FindNamed<Border>(metadata, "ModelTypeField");
        Border format = FindNamed<Border>(metadata, "FormatField");
        publisher.Visibility = Visibility.Collapsed;
        Assert.IsTrue(metadata.Children.Remove(modelType));
        int formatIndex = metadata.Children.IndexOf(format);
        Assert.IsTrue(formatIndex >= 0);
        metadata.Children.Insert(formatIndex, modelType);

        string[] expected = beforeIds.Where(id => !string.Equals(
                id,
                "metadata-publisher",
                StringComparison.Ordinal) &&
            !string.Equals(
                id,
                "metadata-model-type",
                StringComparison.Ordinal))
            .Prepend("metadata-model-type")
            .ToArray();
        string[] after = (await observation.CaptureAsync(
                CancellationToken.None)).Model.Metadata
            .Select(field => field.Id)
            .ToArray();

        CollectionAssert.AreEqual(
            expected,
            after,
            "Metadata membership and order must follow visible loaded " +
            "fields, including Publisher and ModelType.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsDisclosureStateFromVisibleNamedTree()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-003");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        InspectionModelCard card = FindNamed<InspectionModelCard>(
            fixture.Page,
            "InspectionModelCardControl");
        object presentation = card.Presentation;
        InspectionDisclosure disclosure = FindNamed<InspectionDisclosure>(
            card,
            "InspectionDetailsDisclosure");
        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.IsTrue(before.Model.DisclosureExpanded);
        Assert.IsTrue(disclosure.IsExpanded);

        disclosure.Visibility = Visibility.Collapsed;
        Assert.AreSame(presentation, card.Presentation);
        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);

        Assert.IsFalse(
            after.Model.DisclosureExpanded,
            "A hidden named disclosure is not visibly expanded even when " +
            "the presentation-backed ActiveDisclosure remains expanded.");
    }

    [UITestMethod]
    public async Task Observer_ExcludesHiddenNamedDisclosureFromAutomationAndRetention()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-002");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        const string disclosureId = "inspection-details-disclosure";
        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.IsTrue(before.Automation.Controls.Any(control =>
            string.Equals(control.Id, disclosureId, StringComparison.Ordinal)));
        Assert.IsTrue(before.Retention.Ids.Contains(
            disclosureId,
            StringComparer.Ordinal));
        InspectionModelCard card = FindNamed<InspectionModelCard>(
            fixture.Page,
            "InspectionModelCardControl");
        object? presentation = fixture.Page.CurrentPresentation;
        InspectionDisclosure disclosure = FindNamed<InspectionDisclosure>(
            card,
            "InspectionDetailsDisclosure");
        Assert.IsFalse(disclosure.IsExpanded);

        disclosure.Visibility = Visibility.Collapsed;
        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        Assert.IsNotNull(card.ActiveDisclosure,
            "The synthesized ActiveDisclosure seam must remain present for " +
            "this loaded-tree oracle.");
        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);

        Assert.IsFalse(after.Automation.Controls.Any(control =>
                string.Equals(
                    control.Id,
                    disclosureId,
                    StringComparison.Ordinal)),
            "A hidden named disclosure has no observable automation identity.");
        Assert.IsFalse(after.Retention.Ids.Contains(
                disclosureId,
                StringComparer.Ordinal),
            "A hidden named disclosure has no observable retained identity.");
    }

    [UITestMethod]
    public async Task Observer_DoesNotReportDisabledChecksScrollViewerAsScrollOwner()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-003");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.AreEqual("model-card", before.RowsAndScroll.ScrollOwner);
        object? presentation = fixture.Page.CurrentPresentation;
        ScrollViewer scroll = FindNamed<ScrollViewer>(
            fixture.Page,
            "InspectionChecksScrollViewer");
        Assert.AreEqual(ScrollMode.Enabled, scroll.VerticalScrollMode);

        scroll.VerticalScrollMode = ScrollMode.Disabled;
        Assert.AreSame(presentation, fixture.Page.CurrentPresentation);
        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);

        Assert.AreNotEqual(
            "model-card",
            after.RowsAndScroll.ScrollOwner,
            "A disabled loaded ScrollViewer must not be reported as the " +
            "model-card scroll owner.");
    }

    [UITestMethod]
    public async Task Observer_ProjectsVisibleOutcomeBadgeFromLoadedTree()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-002");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.RunAsync();

        ModelInspectionObservedScreen before =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.IsNull(before.Outcome.Badge);
        Border card = FindNamed<Border>(fixture.Page, "OutcomeCardBorder");
        var layout = (Grid)card.Child;
        var badge = new TextBlock
        {
            Name = "OutcomeBadgeText",
            Tag = "InspectionOutcomeBadge",
            Text = "UNEXPECTED OUTCOME BADGE",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Visible
        };
        Grid.SetColumn(badge, 2);
        layout.Children.Add(badge);

        ModelInspectionObservedScreen after =
            await observation.CaptureAsync(CancellationToken.None);
        Assert.AreEqual(
            badge.Text,
            after.Outcome.Badge?.Text,
            "A visible tagged outcome badge must not be reported as null.");
    }

    [UITestMethod]
    public async Task EveryExpectedLeafCollectionNullAndOrderMutationIsRejected()
    {
        using var culture = new CultureScope();
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        var comparer = new ModelInspectionFixtureScreenComparer();
        foreach (ValidatedModelInspectionFixture fixture in catalogue.Fixtures)
        {
            using ScreenFixture screen = CreateScreenFixture(fixture.Id);
            using IModelInspectionFixtureObservationSession observation =
                new ModelInspectionFixtureScreenObserver().Begin(screen.Page);
            await screen.RunAsync();
            ModelInspectionObservedScreen observed =
                await observation.CaptureAsync(CancellationToken.None);

            IReadOnlyList<ExpectedMutation> mutations =
                ExpectedMutationRegistry.Create(fixture.Expected);
            CollectionAssert.AreEquivalent(
                ExpectedMutationRegistry.ReflectivePaths(fixture.Expected)
                    .ToArray(),
                mutations.Select(mutation => mutation.Path).ToArray(),
                $"Future expected property escaped the mutation registry for {fixture.Id}.");
            foreach (ExpectedMutation mutation in mutations)
            {
                IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
                    comparer.Compare(
                        fixture.FileName,
                        mutation.Value,
                        observed,
                        catalogue.Policy.Value.CopyRegistry);
                Assert.IsTrue(differences.Count > 0,
                    $"{fixture.FileName}: {mutation.Path}");
            }
        }
    }

    [TestMethod]
    public void Catalogue_ClosesProductionEnumsInteractionsAndCurrentMaxima()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFigmaState>()
                .Select(value => value.ToString()).ToArray(),
            catalogue.Fixtures.SelectMany(fixture =>
                    fixture.Coverage.FigmaStates)
                .Select(value => value.ToString()).Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionStage>()
                .Select(value => value.ToString()).ToArray(),
            catalogue.Fixtures.SelectMany(fixture => fixture.Coverage.Stages)
                .Select(value => value.ToString()).Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionOutcome>()
                .Select(value => value.ToString()).ToArray(),
            catalogue.Fixtures.SelectMany(fixture => fixture.Coverage.Outcomes)
                .Select(value => value.ToString()).Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ModelInspectionFixtureInteractionKind>(),
            catalogue.Fixtures.SelectMany(fixture =>
                    fixture.Coverage.Interactions)
                .Distinct().ToArray());
        Assert.AreEqual(13, catalogue.Fixtures.Select(fixture =>
                fixture.Expected.Figma.State).Distinct().Count());

        AssertProgress(catalogue, "MI-014", completed: 0, total: 5);
        AssertProgress(catalogue, "MI-028", completed: 1, total: 5);
        ValidatedModelInspectionFixture mi014 = catalogue.Fixtures.Single(
            fixture => fixture.Id == "MI-014");
        Assert.IsNull(mi014.Input.Attempts[0].ServiceSteps[0]
            .Effect.Progress!.Fraction);
        ValidatedModelInspectionFixture mi028 = catalogue.Fixtures.Single(
            fixture => fixture.Id == "MI-028");
        CollectionAssert.AreEqual(
            new double?[] { 0.25d, 0.75d },
            mi028.Input.Attempts[0].ServiceSteps
                .Where(step => step.Effect.Progress is not null)
                .Select(step => step.Effect.Progress!.Fraction).ToArray());
        Assert.AreEqual(1, mi028.Expected.Announcements.Count,
            "A fraction-only update must not repeat its announcement.");
        Assert.AreEqual(5, catalogue.Fixtures.Single(fixture =>
            fixture.Id == "MI-045").Expected.Model.Checks.Count);
        foreach (ValidatedModelInspectionFixture fixture in
                 catalogue.Fixtures.Where(fixture =>
                     fixture.Expected.Model.Mode ==
                         ModelInspectionExpectedModelMode.Compact))
        {
            Assert.AreEqual(0, fixture.Expected.Model.Metadata.Count,
                $"{fixture.Id} compact metadata must describe only the " +
                "currently loadable visual region.");
            Assert.AreEqual(0, fixture.Expected.Model.Checks.Count,
                $"{fixture.Id} compact checks must describe only the " +
                "currently loadable visual region.");
        }
        foreach (ValidatedModelInspectionFixture fixture in
                 catalogue.Fixtures.Where(fixture =>
                     fixture.Expected.Model.Mode ==
                         ModelInspectionExpectedModelMode.Detailed &&
                     !fixture.Expected.Model.DisclosureExpanded))
        {
            Assert.AreEqual(0, fixture.Expected.Model.Checks.Count,
                $"{fixture.Id} collapsed checks must describe only the " +
                "currently visible disclosure region.");
        }
        Assert.AreEqual(1, catalogue.Fixtures.Single(fixture =>
            fixture.Id == "MI-046").Expected.Content.Rows.Count);
        Assert.AreEqual(1, catalogue.Fixtures.Single(fixture =>
            fixture.Id == "MI-047").Expected.Content.Rows.Count);
    }

    [UITestMethod]
    [DataRow("MI-002", "MI-003")]
    [DataRow("MI-004", "MI-005")]
    [DataRow("MI-006", "MI-007")]
    [DataRow("MI-010", "MI-011")]
    public async Task DisclosurePairs_RetainSameSessionItemsContainersAndScrollOwner(
        string collapsedId,
        string expandedId)
    {
        using var culture = new CultureScope();
        using ScreenFixture expanded = CreateScreenFixture(expandedId);
        await expanded.RunAsync();
        using IModelInspectionFixtureObservationSession expandedObservation =
            new ModelInspectionFixtureScreenObserver().Begin(expanded.Page);

        InspectionModelCard model = FindNamed<InspectionModelCard>(
            expanded.Page,
            "InspectionModelCardControl");
        InspectionContentCard content = FindNamed<InspectionContentCard>(
            expanded.Page,
            "InspectionContentCardControl");
        InspectionDisclosure disclosure = model.ActiveDisclosure ??
            content.ActiveDisclosure ?? throw new AssertFailedException(
                "The expanded fixture has no active disclosure.");
        Assert.IsTrue(disclosure.IsExpanded);

        disclosure.RequestTargetState(isExpanded: false);
        _ = await expandedObservation.CaptureAsync(CancellationToken.None);
        Assert.IsFalse(disclosure.IsExpanded);

        disclosure.RequestTargetState(isExpanded: true);
        ModelInspectionObservedScreen observed =
            await expandedObservation.CaptureAsync(CancellationToken.None);
        Assert.IsTrue(disclosure.IsExpanded);

        Assert.AreEqual(collapsedId,
            LoadCatalogue().Policy.Value.DisclosurePairs.Single(pair =>
                pair.ExpandedId == expandedId).CollapsedId);
        Assert.IsTrue(observed.Retention.ItemsSourceRetained);
        Assert.IsTrue(observed.Retention.RowContainersRetained);
        Assert.IsTrue(observed.Retention.ScrollOwnerRetained);
        Assert.IsTrue(observed.Retention.SampledBeforeDisclosure);
        Assert.IsTrue(observed.Retention.SampledAfterDisclosure);
    }

    [UITestMethod]
    public async Task TemporalObserver_CapturesAnnouncementsAndDeterministicBarrier()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-014");
        var observer = new ModelInspectionFixtureScreenObserver();
        using IModelInspectionFixtureObservationSession observation =
            observer.Begin(fixture.Page);
        Assert.IsFalse(fixture.Page.IsLoaded);

        await fixture.RunAsync();
        ModelInspectionObservedScreen observed =
            await observation.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(
            fixture.Descriptor.Expected.Announcements.Count,
            observed.Announcements.Count);
        Assert.IsTrue(observed.RenderBarrier.DispatcherDrained);
        Assert.IsTrue(observed.RenderBarrier.LayoutUpdated);
        Assert.IsTrue(observed.RenderBarrier.CompositionCommitted);
        Assert.IsFalse(AllReferencedTypes(
                typeof(ModelInspectionFixtureScreenObserver))
            .Any(type => type == typeof(Task) &&
                type.Name.Contains("Delay", StringComparison.Ordinal)));
        AssertNoDelayOrPolling(typeof(ModelInspectionFixtureScreenObserver));
    }

    [UITestMethod]
    public async Task TemporalObserver_ProjectsEachReleasedFractionWithoutRepeatingAnnouncement()
    {
        using var culture = new CultureScope();
        using ScreenFixture fixture = CreateScreenFixture("MI-028");
        using IModelInspectionFixtureObservationSession observation =
            new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
        await fixture.ActivateAsync();

        await ModelInspectionFixtureScenarioRunner
            .ReleaseAndWaitForSnapshotAsync(
                fixture.Page.ViewModel!,
                snapshot => snapshot.Progress?.StageFraction == 0.25d,
                () => fixture.Session.Service.ReleaseServiceCheckpoint(
                    attempt: 1,
                    "fraction-25"),
                CancellationToken.None);
        ModelInspectionObservedScreen quarter =
            await observation.CaptureAsync(CancellationToken.None);
        ModelInspectionObservedContentRow quarterRow = quarter.Content.Rows
            .Single(row => string.Equals(
                row.Status,
                "Active",
                StringComparison.Ordinal));
        Assert.AreEqual("progress-2", quarterRow.Id);
        Assert.IsNotNull(quarterRow.StageFraction);
        Assert.AreEqual(0.25d, quarterRow.StageFraction.Value, 0d);
        Assert.AreEqual(1, quarter.Announcements.Count);

        await ModelInspectionFixtureScenarioRunner
            .ReleaseAndWaitForSnapshotAsync(
                fixture.Page.ViewModel!,
                snapshot => snapshot.Progress?.StageFraction == 0.75d,
                () => fixture.Session.Service.ReleaseServiceCheckpoint(
                    attempt: 1,
                    "fraction-75"),
                CancellationToken.None);
        ModelInspectionObservedScreen threeQuarters =
            await observation.CaptureAsync(CancellationToken.None);
        ModelInspectionObservedContentRow threeQuarterRow =
            threeQuarters.Content.Rows.Single(row => string.Equals(
                row.Status,
                "Active",
                StringComparison.Ordinal));
        Assert.AreEqual("progress-2", threeQuarterRow.Id);
        Assert.IsNotNull(threeQuarterRow.StageFraction);
        Assert.AreEqual(0.75d, threeQuarterRow.StageFraction.Value, 0d);
        Assert.AreEqual(
            quarter.Announcements.Count,
            threeQuarters.Announcements.Count,
            "A fraction-only update must not repeat the active-stage " +
            "announcement.");
    }

    [UITestMethod]
    public async Task Mi037AndMi038_ComparePreRetirementScreenOnly()
    {
        using var culture = new CultureScope();
        foreach (string id in new[] { "MI-037", "MI-038" })
        {
            using ScreenFixture fixture = CreateScreenFixture(id);
            using IModelInspectionFixtureObservationSession observation =
                new ModelInspectionFixtureScreenObserver().Begin(fixture.Page);
            await fixture.RunAsync();
            ModelInspectionObservedScreen observed =
                await observation.CaptureAsync(CancellationToken.None);
            IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
                new ModelInspectionFixtureScreenComparer().Compare(
                    fixture.Descriptor.FileName,
                    fixture.Descriptor.Expected,
                    observed,
                    fixture.Catalogue.Policy.Value.CopyRegistry);
            Assert.AreEqual(
                0,
                differences.Count,
                JoinDiagnostics(fixture.Descriptor.FileName, differences));
            Assert.AreEqual(0, fixture.Session.Evidence.SessionRetirementCount);
            Assert.IsTrue(fixture.Descriptor.Interactions.Any(interaction =>
                interaction.LifetimeEffect !=
                    ModelInspectionFixtureInteractionLifetimeEffect.None));
        }

        string[] lifetimeTruthTests =
        [
            nameof(ModelInspectionFixtureGalleryTests
                .ChooseAnother_RealMi037RouteRetiresExactHostAndStaleEventCannotRetireWinner),
            nameof(ModelInspectionFixtureGalleryTests
                .GallerySwitch_CreatesDistinctHostAndRetiresPriorLifetime)
        ];
        foreach (string test in lifetimeTruthTests)
        {
            MethodInfo method = typeof(ModelInspectionFixtureGalleryTests)
                .GetMethod(test, BindingFlags.Instance | BindingFlags.Public)!;
            Assert.IsNotNull(method.GetCustomAttribute<UITestMethodAttribute>(),
                test);
        }
    }

    [TestMethod]
    public void Diagnostics_AreStableRuleCodedAndSafe()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures[0];
        ModelInspectionObservedScreen observed =
            ModelInspectionObservedScreen.Empty.WithVisibleTextMutation(
                "Outcome.Title", "C:\\secret\\model.gguf\n{payload}");
        IReadOnlyList<ModelInspectionFixtureScreenDifference> first =
            new ModelInspectionFixtureScreenComparer().Compare(
                fixture.FileName,
                fixture.Expected,
                observed,
                catalogue.Policy.Value.CopyRegistry);
        IReadOnlyList<ModelInspectionFixtureScreenDifference> second =
            new ModelInspectionFixtureScreenComparer().Compare(
                fixture.FileName,
                fixture.Expected,
                observed,
                catalogue.Policy.Value.CopyRegistry);

        CollectionAssert.AreEqual(
            first.Select(item => item.Diagnostic).ToArray(),
            second.Select(item => item.Diagnostic).ToArray());
        Assert.IsTrue(first.Count > 0);
        Assert.IsTrue(first.All(item =>
            item.Diagnostic.StartsWith(fixture.FileName + "|MI-SCREEN-",
                StringComparison.Ordinal)));
        Assert.IsTrue(first.All(item =>
            !item.Diagnostic.Contains("C:\\", StringComparison.Ordinal) &&
            !item.Diagnostic.Contains("{payload}", StringComparison.Ordinal) &&
            !item.Diagnostic.Contains('\n')));
    }

    [TestMethod]
    public void Diagnostics_HashDelimiterBearingTokensIntoExactlyFiveFields()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures[0];
        const string injected = "visible|text|must-not-escape";
        ModelInspectionObservedScreen observed =
            ModelInspectionObservedScreen.Empty.WithVisibleTextMutation(
                "DelimiterProbe",
                injected);

        ModelInspectionFixtureScreenDifference difference =
            new ModelInspectionFixtureScreenComparer().Compare(
                    fixture.FileName,
                    fixture.Expected,
                    observed,
                    catalogue.Policy.Value.CopyRegistry)
                .Single(item => string.Equals(
                    item.ExpectedPath,
                    "$.Observed.DelimiterProbe",
                    StringComparison.Ordinal));

        Assert.AreEqual(
            5,
            difference.Diagnostic.Split('|').Length,
            "A diagnostic must retain its five-field wire shape.");
        Assert.IsFalse(
            difference.Diagnostic.Contains(injected, StringComparison.Ordinal),
            "Delimiter-bearing text must be represented by a safe token.");
    }

    [UITestMethod]
    public async Task Gallery_PublishesInjectedObserverAndComparerResult()
    {
        using var culture = new CultureScope();
        var observer = new RecordingObserver();
        var comparer = new RecordingComparer();
        var gallery = new ModelInspectionFixtureGalleryPage(
            new ModelInspectionFixturePackageLoader(),
            static () => { },
            screenObserver: observer,
            screenComparer: comparer);
        var window = new Window { Content = gallery };
        window.Activate();
        try
        {
            await gallery.CatalogueLoaded;
            await gallery.SelectFixtureForTestingAsync(
                gallery.ViewModel.Items[0].Id);
            Assert.AreEqual(1, observer.BeginCount);
            Assert.AreEqual(1, comparer.CompareCount);
            Assert.IsTrue(gallery.ViewModel.ValidationStatus.StartsWith(
                "Screen contract passed:",
                StringComparison.Ordinal));
        }
        finally
        {
            gallery.CloseForTesting();
            window.Content = null;
            window.Close();
        }
    }

    private static void AssertSetupInteractionEvidence(
        ValidatedModelInspectionFixture fixture,
        ModelInspectionFixtureSessionEvidence evidence)
    {
        int checkpointSteps = fixture.Input.SetupSteps.Count(step =>
            step.Kind == ModelInspectionFixtureSetupStepKind
                .ReleaseServiceCheckpoint);
        Assert.AreEqual(checkpointSteps,
            evidence.ReleasedServiceCheckpoints.Count,
            fixture.FileName);
        int cancelSteps = fixture.Input.SetupSteps.Count(step =>
            step.Kind == ModelInspectionFixtureSetupStepKind.InvokeCancel);
        Assert.AreEqual(cancelSteps, evidence.CancellationObservationCount,
            fixture.FileName);
        int recoverySteps = fixture.Input.SetupSteps.Count(step =>
            step.Kind is ModelInspectionFixtureSetupStepKind.InvokeRetry or
                ModelInspectionFixtureSetupStepKind.InvokeRestart);
        Assert.AreEqual(
            fixture.Input.Attempts.Count == 0
                ? 0
                : 1 + recoverySteps,
            evidence.ServiceCallCount,
            fixture.FileName);
    }

    private static void AssertProgress(
        ModelInspectionFixtureCatalogue catalogue,
        string id,
        int completed,
        int total)
    {
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == id);
        ModelInspectionFixtureProgressDescriptor[] progress = fixture.Input
            .Attempts.SelectMany(attempt => attempt.ServiceSteps)
            .Where(step => step.Effect.Progress is not null)
            .Select(step => step.Effect.Progress!)
            .ToArray();
        Assert.IsTrue(progress.Any(item =>
            item.CompletedStageCount == completed), id);
        Assert.AreEqual(5, total);
        Assert.IsTrue(progress.All(item => item.CompletedStageCount <= total));
    }

    private static string JoinDiagnostics(
        string fileName,
        IReadOnlyList<ModelInspectionFixtureScreenDifference> differences) =>
        differences.Count == 0
            ? fileName
            : string.Join(Environment.NewLine,
                differences.Select(difference => difference.Diagnostic));

    private static IEnumerable<ModelInspectionExpectedCopy> ExpectedCopies(
        object? value)
    {
        if (value is null || value is string || value.GetType().IsEnum)
        {
            yield break;
        }

        if (value is ModelInspectionExpectedCopy copy)
        {
            yield return copy;
            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                foreach (ModelInspectionExpectedCopy nested in
                         ExpectedCopies(item))
                {
                    yield return nested;
                }
            }
            yield break;
        }

        foreach (PropertyInfo property in value.GetType().GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (ModelInspectionExpectedCopy nested in
                     ExpectedCopies(property.GetValue(value)))
            {
                yield return nested;
            }
        }
    }

    private static IReadOnlyList<VisualMutation> VisualMutations() =>
    [
        new("MI-002", "outcome-text", page =>
            FindNamed<TextBlock>(page, "OutcomeTitle").Text += " altered"),
        new("MI-002", "outcome-tone", page =>
        {
            Border border = FindNamed<Border>(page, "OutcomeCardBorder");
            border.Background = (Brush)Application.Current.Resources[
                "InspectionErrorSurfaceBrush"];
            border.BorderBrush = (Brush)Application.Current.Resources[
                "InspectionErrorBorderBrush"];
        }, MaintainThroughRendering: true),
        new("MI-002", "model-text", page =>
            Find<TextBlock>(FindNamed<Border>(page,
                "ModelNameField"), text =>
                    !string.Equals(
                        text.Text,
                        "MODEL NAME",
                        StringComparison.Ordinal)).Text += " altered"),
        new("MI-014", "content-text", page =>
            Find<TextBlock>(FindNamed<Grid>(page, "ProgressView")).Text +=
                " altered"),
        new("MI-002", "action-text", page =>
            Find<TextBlock>(FindNamed<Button>(page, "PrimaryActionButton"))
                .Text += " altered"),
        new("MI-002", "visibility", page =>
            FindNamed<InspectionOutcomeCard>(page,
                "InspectionOutcomeCardControl").Visibility =
                Visibility.Collapsed),
        new("MI-002", "enabled", page =>
            FindNamed<Button>(page, "PrimaryActionButton").IsEnabled = true),
        new("MI-002", "automation", page => AutomationProperties.SetName(
            FindNamed<Button>(page, "PrimaryActionButton"),
            "altered automation name")),
        new("MI-002", "focus", page =>
        {
            page.IsTabStop = true;
            Assert.IsTrue(page.Focus(FocusState.Programmatic));
        }),
        new("MI-014", "row-order", page => ReverseItems(
            FindNamed<ItemsRepeater>(page, "ProgressItemsRepeater"))),
        new("MI-014", "scroll-owner", page =>
            FindNamed<ScrollViewer>(page, "InspectionPageScrollViewer")
                .VerticalScrollMode = ScrollMode.Disabled)
    ];

    private static IReadOnlyList<ObservedProjectionMutation>
        ObservedProjectionMutations() =>
    [
        new("MI-002", "model-mode", screen => screen.Model.Mode, page =>
        {
            FindNamed<Border>(page, "DetailedView").Visibility =
                Visibility.Collapsed;
            FindNamed<Border>(page, "CompactView").Visibility =
                Visibility.Visible;
        }),
        new("MI-004", "model-badge", screen => screen.Model.Badge ?? "<null>", page =>
            FindNamed<TextBlock>(page, "CompactStatusText").Text =
                "RESULT UNKNOWN"),
        new("MI-004", "model-compact-name", screen =>
            screen.Model.DisplayName, page =>
            Find<TextBlock>(FindNamed<StackPanel>(page,
                "CompactModelSummaryPanel")).Text =
                "Altered compact model"),
        new("MI-003", "model-check-membership", screen => string.Join("|",
            screen.Model.Checks.Select(row => row.Id)), page =>
            FindNamed<ItemsControl>(page,
                "InspectionChecksItemsControl").ItemsSource =
                Array.Empty<object>()),
        new("MI-003", "model-check-detail", screen => string.Join("|",
            screen.Model.Checks.Select(row => row.Text)), page =>
        {
            Border row = FirstModelCheckRow(page);
            StackPanel copy = Find<StackPanel>(row,
                panel => Grid.GetColumn(panel) == 1);
            DescendantsForMutation<TextBlock>(copy).Skip(1).First().Text =
                "Altered check detail";
        }),
        new("MI-003", "model-check-status", screen => string.Join("|",
            screen.Model.Checks.Select(row => row.Status)), page =>
        {
            Border row = FirstModelCheckRow(page);
            Grid status = Find<Grid>(row, grid => Grid.GetColumn(grid) == 2);
            Find<TextBlock>(status,
                text => text.Visibility == Visibility.Visible).Text = "Warning";
        }),
        new("MI-014", "content-mode", screen => screen.Content.Mode, page =>
        {
            FindNamed<Grid>(page, "ProgressView").Visibility =
                Visibility.Collapsed;
            FindNamed<Grid>(page, "FindingsView").Visibility =
                Visibility.Visible;
        }),
        new("MI-014", "content-row-title", screen =>
            screen.Content.Rows[0].PrimaryText, page =>
        {
            FrameworkElement row = FirstProgressRow(page, 0);
            Find<TextBlock>(Find<StackPanel>(row,
                panel => Grid.GetColumn(panel) == 1)).Text =
                "Altered progress title";
        }),
        new("MI-014", "content-row-status", screen =>
            screen.Content.Rows[0].Status, page =>
        {
            FrameworkElement row = FirstProgressRow(page, 0);
            Find<TextBlock>(row,
                text => Grid.GetColumn(text) == 2).Text = "Waiting";
        }),
        new("MI-028", "content-stage-fraction", screen =>
            screen.Content.Rows[1].StageFraction?.ToString(
                "R",
                CultureInfo.InvariantCulture) ?? "<null>", page =>
            Find<ProgressRing>(FirstProgressRow(page, 1),
                ring => ring.Visibility == Visibility.Visible).Value = 10d),
        new("MI-002", "action-mode", screen => screen.Actions.Mode, page =>
        {
            FindNamed<Border>(page, "ResultView").Visibility =
                Visibility.Collapsed;
            FindNamed<StackPanel>(page, "InspectingView").Visibility =
                Visibility.Visible;
        })
    ];

    private static Border FirstModelCheckRow(ModelInspectionPage page)
    {
        ItemsControl items = FindNamed<ItemsControl>(
            page,
            "InspectionChecksItemsControl");
        FrameworkElement container =
            (FrameworkElement?)items.ContainerFromIndex(0) ??
            throw new InvalidOperationException(
                "The first inspection-check row was not realized.");
        return Find<Border>(container, border =>
            string.Equals(
                border.Tag as string,
                "InspectionCheckRow",
                StringComparison.Ordinal));
    }

    private static Border FirstFindingRow(ModelInspectionPage page)
    {
        ItemsControl items = FindNamed<ItemsControl>(
            page,
            "FindingsItemsRepeater");
        FrameworkElement container =
            (FrameworkElement?)items.ContainerFromIndex(0) ??
            throw new InvalidOperationException(
                "The first finding row was not realized.");
        return Find<Border>(container, border =>
            AutomationProperties.GetAutomationControlType(border) ==
                AutomationControlType.ListItem);
    }

    private static FrameworkElement FirstProgressRow(
        ModelInspectionPage page,
        int index) =>
        FindNamed<ItemsRepeater>(page, "ProgressItemsRepeater")
            .TryGetElement(index) as FrameworkElement ??
        throw new InvalidOperationException(
            $"Progress row {index} was not realized.");

    private static IEnumerable<T> DescendantsForMutation<T>(
        DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in DescendantsForMutation<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void ReverseItems(ItemsRepeater repeater)
    {
        object[] items = ((IEnumerable)repeater.ItemsSource)
            .Cast<object>().Reverse().ToArray();
        Assert.IsTrue(items.Length > 1);
        repeater.ItemsSource = items;
    }

    private static T FindNamed<T>(DependencyObject root, string name)
        where T : FrameworkElement => Find<T>(root, element =>
            string.Equals(element.Name, name, StringComparison.Ordinal));

    private static T Find<T>(DependencyObject root, Predicate<T>? predicate = null)
        where T : DependencyObject
    {
        if (root is T match && (predicate is null || predicate(match)))
        {
            return match;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            try
            {
                return Find(VisualTreeHelper.GetChild(root, index), predicate);
            }
            catch (InvalidOperationException)
            {
            }
        }

        throw new InvalidOperationException($"No {typeof(T).Name} matched.");
    }

    private static ScreenFixture CreateScreenFixture(string id)
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture descriptor = catalogue.Fixtures.Single(
            fixture => fixture.Id == id);
        var session = new ModelInspectionFixtureSession(
            descriptor.Input,
            animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            descriptor.Input,
            session,
            ModelInspectionFixtureGalleryPage
                .ShouldStartInspectionOnLoaded(descriptor)));
        return new ScreenFixture(catalogue, descriptor, session, host);
    }

    private static ModelInspectionFixtureCatalogue LoadCatalogue() =>
        LoadCatalogueFromFiles();

    private static ModelInspectionFixtureCatalogue LoadCatalogueFromFiles()
    {
        string root = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "ModelInspectionScenarios");
        ModelInspectionFixtureDocumentSource Read(string fileName) => new(
            fileName,
            File.ReadAllBytes(Path.Combine(root, fileName)));
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(Read(
                "model-inspection-fixture.schema.json"));
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(Read(
                "model-inspection-fixture-coverage-policy.json"), schema);
        ModelInspectionFixtureCatalogue catalogue =
            ModelInspectionFixtureCatalogue.LoadDescriptors(
                policy.Value.Fixtures.Select(entry => Read(entry.FileName))
                    .ToArray(),
                policy,
                schema);
        return ModelInspectionFixtureCoverageValidator.Validate(catalogue)
            .Catalogue;
    }

    private static void AssertNoFixtureDependency(IEnumerable<Type> roots)
    {
        foreach (Type root in roots)
        {
            Type? violation = AllReferencedTypes(root).FirstOrDefault(type =>
                type.Namespace?.StartsWith(
                    "GraniteEdgeAI.ModelInspection.Fixtures",
                    StringComparison.Ordinal) == true ||
                type.Name.Contains("Expected", StringComparison.Ordinal) ||
                type.Name.Contains("Descriptor", StringComparison.Ordinal) ||
                type.Name.Contains("Preset", StringComparison.Ordinal) ||
                type.Name.Contains("Policy", StringComparison.Ordinal));
            Assert.IsNull(violation,
                $"{root.FullName} depends on {violation?.FullName}.");
        }
    }

    private static HashSet<Type> AllReferencedTypes(Type root)
    {
        var found = new HashSet<Type>();
        var pending = new Queue<Type>();
        pending.Enqueue(root);
        while (pending.TryDequeue(out Type? type))
        {
            Type normalized = Normalize(type);
            if (!found.Add(normalized))
            {
                continue;
            }

            foreach (Type next in DirectReferences(normalized))
            {
                if (next.Assembly == root.Assembly &&
                    next.Namespace?.StartsWith(
                        "GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation",
                        StringComparison.Ordinal) == true)
                {
                    pending.Enqueue(next);
                }
                else
                {
                    found.Add(Normalize(next));
                }
            }
        }
        return found;
    }

    private static IEnumerable<Type> DirectReferences(Type type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }
        foreach (Type item in type.GetInterfaces()) yield return item;
        foreach (FieldInfo field in type.GetFields(AllMembers))
            foreach (Type item in Expand(field.FieldType)) yield return item;
        foreach (PropertyInfo property in type.GetProperties(AllMembers))
            foreach (Type item in Expand(property.PropertyType)) yield return item;
        foreach (MethodBase method in type.GetMethods(AllMembers)
                     .Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
        {
            if (method is MethodInfo information)
                foreach (Type item in Expand(information.ReturnType))
                    yield return item;
            foreach (ParameterInfo parameter in method.GetParameters())
                foreach (Type item in Expand(parameter.ParameterType))
                    yield return item;
            foreach (Type item in IlReferencedTypes(method)) yield return item;
        }
        foreach (Type nested in type.GetNestedTypes(AllMembers)) yield return nested;
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return Normalize(type);
        if (type.HasElementType)
            foreach (Type item in Expand(type.GetElementType()!)) yield return item;
        foreach (Type argument in type.GetGenericArguments())
            foreach (Type item in Expand(argument)) yield return item;
    }

    private static Type Normalize(Type type) =>
        type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    private static IEnumerable<Type> IlReferencedTypes(MethodBase method)
    {
        MethodBody? body = method.GetMethodBody();
        if (body is null) yield break;
        byte[] bytes = body.GetILAsByteArray() ?? [];
        int offset = 0;
        while (offset < bytes.Length)
        {
            OpCode opcode = bytes[offset++] == 0xFE
                ? MultiByte[bytes[offset++]]
                : SingleByte[bytes[offset - 1]];
            int size = OperandSize(opcode.OperandType, bytes, offset);
            if (opcode.OperandType is OperandType.InlineType or
                OperandType.InlineField or OperandType.InlineMethod or
                OperandType.InlineTok)
            {
                int token = BitConverter.ToInt32(bytes, offset);
                MemberInfo? member = null;
                try
                {
                    member = method.Module.ResolveMember(token,
                        method.DeclaringType?.GetGenericArguments(),
                        method.IsGenericMethod
                            ? method.GetGenericArguments()
                            : null);
                }
                catch (ArgumentException)
                {
                }
                if (member is Type referenced) yield return referenced;
                if (member?.DeclaringType is not null)
                    yield return member.DeclaringType;
            }
            offset += size;
        }
    }

    private static int OperandSize(OperandType type, byte[] bytes, int offset) =>
        type switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or
                OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI or OperandType.InlineBrTarget or
                OperandType.InlineField or OperandType.InlineMethod or
                OperandType.InlineSig or OperandType.InlineString or
                OperandType.InlineTok or OperandType.InlineType or
                OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 +
                4 * BitConverter.ToInt32(bytes, offset),
            _ => throw new InvalidOperationException(type.ToString())
        };

    private static void AssertNoDelayOrPolling(Type type)
    {
        foreach (MethodBase method in type.GetMethods(AllMembers))
        {
            string[] forbidden = ["Delay", "Timer", "PeriodicTimer"];
            foreach (Type reference in IlReferencedTypes(method))
            {
                Assert.IsFalse(forbidden.Any(token => reference.Name.Contains(
                    token, StringComparison.Ordinal)), method.Name);
            }
        }
    }

    private const BindingFlags AllMembers = BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly OpCode[] SingleByte = BuildOpCodes(false);
    private static readonly OpCode[] MultiByte = BuildOpCodes(true);

    private static OpCode[] BuildOpCodes(bool multi)
    {
        var result = new OpCode[256];
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode opcode)
            {
                ushort value = unchecked((ushort)opcode.Value);
                if ((value > byte.MaxValue) == multi)
                {
                    result[value & byte.MaxValue] = opcode;
                }
            }
        }
        return result;
    }

    private sealed record VisualMutation(
        string FixtureId,
        string Name,
        Action<ModelInspectionPage> Apply,
        bool MaintainThroughRendering = false);

    private sealed record ObservedProjectionMutation(
        string FixtureId,
        string Name,
        Func<ModelInspectionObservedScreen, string> Project,
        Action<ModelInspectionPage> Apply);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo originalCulture =
            CultureInfo.CurrentCulture;
        private readonly CultureInfo originalUiCulture =
            CultureInfo.CurrentUICulture;

        internal CultureScope()
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [DoNotParallelize]
    private sealed class ScreenFixture : IDisposable
    {
        private readonly Window window;

        internal ScreenFixture(
            ModelInspectionFixtureCatalogue catalogue,
            ValidatedModelInspectionFixture descriptor,
            ModelInspectionFixtureSession session,
            ModelInspectionFixtureHostPage host)
        {
            Catalogue = catalogue;
            Descriptor = descriptor;
            Session = session;
            Host = host;
            Page = host.ModelInspectionPage!;
            window = new Window { Content = host };
        }

        internal ModelInspectionFixtureCatalogue Catalogue { get; }
        internal ValidatedModelInspectionFixture Descriptor { get; }
        internal ModelInspectionFixtureSession Session { get; }
        internal ModelInspectionFixtureHostPage Host { get; }
        internal ModelInspectionPage Page { get; }

        internal async Task ActivateAsync()
        {
            if (Page.IsLoaded)
            {
                return;
            }

            var loaded = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler? handler = null;
            handler = (_, _) =>
            {
                Page.Loaded -= handler;
                loaded.TrySetResult(true);
            };
            Page.Loaded += handler;
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(1, Session.Evidence.ServiceCallCount,
                "The real Loaded path must start one MI-028 service call.");
        }

        internal async Task RunAsync()
        {
            Task<string> run = new ModelInspectionFixtureScenarioRunner()
                .RunAsync(
                    Descriptor.Input,
                    Page,
                    Session,
                    CancellationToken.None);
            window.Activate();
            Assert.AreEqual(Descriptor.Input.ObservationCheckpoint, await run);
        }

        public void Dispose()
        {
            Host.RetireForTesting();
            window.Content = null;
            window.Close();
            Session.Dispose();
        }
    }

    private sealed class ImmediateAnimationDriver :
        IModelInspectionAnimationDriver
    {
        public void StartStageStatus(UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) => completed(key);
        public void StartActiveDetail(UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) => completed(key);
        public void StartDisclosure(UIElement chevron, FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements, bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) => completed(key);
        public void StartTerminal(UIElement outgoing, UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) => completed(key);
        public void CancelAll() { }
        public void Dispose() { }
    }

    private sealed class RecordingObserver : IModelInspectionFixtureScreenObserver
    {
        internal int BeginCount { get; private set; }
        public IModelInspectionFixtureObservationSession Begin(
            ModelInspectionPage page)
        {
            BeginCount++;
            return new RecordingObservationSession();
        }
    }

    private sealed class RecordingObservationSession :
        IModelInspectionFixtureObservationSession
    {
        public Task<ModelInspectionObservedScreen> CaptureAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(ModelInspectionObservedScreen.Empty);
        public void Dispose() { }
    }

    private sealed class RecordingComparer : IModelInspectionFixtureScreenComparer
    {
        internal int CompareCount { get; private set; }
        public IReadOnlyList<ModelInspectionFixtureScreenDifference> Compare(
            string fileName,
            ModelInspectionExpectedScreen expected,
            ModelInspectionObservedScreen observed,
            IReadOnlyDictionary<string, string> copyRegistry)
        {
            CompareCount++;
            return Array.Empty<ModelInspectionFixtureScreenDifference>();
        }
    }

    private sealed record ExpectedMutation(
        string Path,
        ModelInspectionExpectedScreen Value);

    private static class ExpectedMutationRegistry
    {
        internal static IReadOnlyList<ExpectedMutation> Create(
            ModelInspectionExpectedScreen expected) => ReflectivePaths(expected)
            .Select(path => new ExpectedMutation(
                path,
                (ModelInspectionExpectedScreen)Mutate(
                    expected,
                    path,
                    "$",
                    typeof(ModelInspectionExpectedScreen))!))
            .ToArray();

        internal static IEnumerable<string> ReflectivePaths(object root) =>
            Paths(root, "$");

        private static IEnumerable<string> Paths(object? value, string path)
        {
            if (value is null || IsLeaf(value.GetType()))
            {
                yield return path;
                yield break;
            }
            if (value is IEnumerable enumerable)
            {
                var items = enumerable.Cast<object?>().ToArray();
                yield return path + ".null";
                if (items.Length > 0) yield return path + ".collection";
                if (items.Length > 1) yield return path + ".order";
                for (int index = 0; index < items.Length; index++)
                    foreach (string nested in Paths(items[index],
                                 $"{path}[{index}]")) yield return nested;
                yield break;
            }
            foreach (PropertyInfo property in value.GetType().GetProperties(
                         BindingFlags.Instance | BindingFlags.Public))
                foreach (string nested in Paths(property.GetValue(value),
                             path + "." + property.Name)) yield return nested;
        }

        private static object? Mutate(
            object? value,
            string target,
            string path,
            Type declaredType)
        {
            if (target == path || target == path + ".null")
                return MutatedLeaf(
                    value,
                    declaredType,
                    target.EndsWith(".null", StringComparison.Ordinal));
            if (value is null || IsLeaf(value.GetType())) return value;
            if (value is IEnumerable enumerable && value is not string)
            {
                object?[] items = enumerable.Cast<object?>().ToArray();
                Type itemType = CollectionItemType(declaredType);
                if (target == path + ".collection")
                    items = items.Skip(1).ToArray();
                else if (target == path + ".order")
                    (items[0], items[1]) = (items[1], items[0]);
                else
                    for (int index = 0; index < items.Length; index++)
                        items[index] = Mutate(items[index], target,
                            $"{path}[{index}]", itemType);
                return RebuildCollection(value.GetType(), items);
            }
            PropertyInfo[] properties = value.GetType().GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            object?[] arguments = properties.Select(property => Mutate(
                property.GetValue(value),
                target,
                path + "." + property.Name,
                property.PropertyType))
                .ToArray();
            return Activator.CreateInstance(value.GetType(), arguments)!;
        }

        private static object? MutatedLeaf(
            object? value,
            Type declaredType,
            bool makeNull)
        {
            if (makeNull) return null;
            if (value is null)
            {
                Type targetType = Nullable.GetUnderlyingType(declaredType) ??
                    declaredType;
                if (targetType == typeof(string)) return "[mutated-null]";
                if (targetType == typeof(ModelInspectionExpectedCopy))
                    return new ModelInspectionExpectedCopy(
                        "fixture.action.cancel",
                        "Cancel inspection");
                if (targetType == typeof(ModelInspectionExpectedModelBadge))
                    return ModelInspectionExpectedModelBadge.ModelSelected;
                throw new InvalidOperationException(
                    $"No null mutation exists for {targetType.FullName}.");
            }
            Type type = value.GetType();
            if (type == typeof(string)) return (string)value + " [mutated]";
            if (type == typeof(bool)) return !(bool)value;
            if (type == typeof(int)) return checked((int)value + 1);
            if (type == typeof(double)) return (double)value + 0.125d;
            if (type.IsEnum)
            {
                Array values = Enum.GetValues(type);
                int index = Array.IndexOf(values, value);
                return values.GetValue((index + 1) % values.Length);
            }
            throw new InvalidOperationException(type.FullName);
        }

        private static object RebuildCollection(Type type, object?[] items)
        {
            Type itemType = CollectionItemType(type);
            Array array = Array.CreateInstance(itemType, items.Length);
            for (int index = 0; index < items.Length; index++)
                array.SetValue(items[index], index);
            return array;
        }

        private static Type CollectionItemType(Type type) => type.IsArray
            ? type.GetElementType()!
            : type.GetGenericArguments().FirstOrDefault() ?? typeof(object);

        private static bool IsLeaf(Type type) =>
            type == typeof(string) || type == typeof(bool) ||
            type == typeof(int) || type == typeof(double) || type.IsEnum;
    }
}
#endif
