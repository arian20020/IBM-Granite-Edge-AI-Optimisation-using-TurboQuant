#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

using DebugFixturePreset = GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets.ModelInspectionFixturePreset;

[TestClass]
[DoNotParallelize]
[TestCategory("ModelInspectionFixtureGallery")]
[TestCategory("ModelInspectionFixturePreset")]
public sealed class ModelInspectionFixturePresetTests
{
    [TestMethod]
    public void PolicyDeclaresExactlyFiftyEightDescriptorPresetPairsAndNineIdentities()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        var pairs = catalogue.Fixtures
            .SelectMany(fixture => fixture.Presets.Select(presetId =>
                (Fixture: fixture, PresetId: presetId)))
            .ToArray();

        Assert.AreEqual(58, pairs.Length);
        CollectionAssert.AreEquivalent(
            Enumerable.Range(1, 9).Select(index => $"P{index:00}").ToArray(),
            pairs.Select(pair => pair.PresetId).Distinct().ToArray());
        Assert.IsTrue(pairs.All(pair =>
            pair.Fixture.PresetExpectations.ContainsKey(pair.PresetId)));

        (string Id, ModelInspectionFixtureWidthProfile Width,
            ModelInspectionFixtureResourceProfile Resources,
            ModelInspectionFixtureTextProfile Text,
            ModelInspectionFixtureMotionProfile Motion)[] exact =
        [
            ExactPreset("P01", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal),
            ExactPreset("P02", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Reduced),
            ExactPreset("P03", ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ExactPreset("P04", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Normal),
            ExactPreset("P05", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ExactPreset("P06", ModelInspectionFixtureWidthProfile.Medium600,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Reduced),
            ExactPreset("P07", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Reduced),
            ExactPreset("P08", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.Dark,
                ModelInspectionFixtureTextProfile.Preview200,
                ModelInspectionFixtureMotionProfile.Normal),
            ExactPreset("P09", ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal)
        ];
        CollectionAssert.AreEqual(
            exact,
            catalogue.Policy.Value.Presets.Select(preset =>
                (preset.Id, preset.Width, preset.Resources, preset.Text,
                    preset.Motion)).ToArray());
    }

    private static (string Id, ModelInspectionFixtureWidthProfile Width,
        ModelInspectionFixtureResourceProfile Resources,
        ModelInspectionFixtureTextProfile Text,
        ModelInspectionFixtureMotionProfile Motion) ExactPreset(
        string id,
        ModelInspectionFixtureWidthProfile width,
        ModelInspectionFixtureResourceProfile resources,
        ModelInspectionFixtureTextProfile text,
        ModelInspectionFixtureMotionProfile motion) =>
        (id, width, resources, text, motion);

    [TestMethod]
    public void PresetIdentity_RejectsForgedIdDimensionCombinations()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new DebugFixturePreset(
                "P09",
                ModelInspectionFixtureWidthProfile.Desktop1440,
                ModelInspectionFixtureResourceProfile.Light,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new DebugFixturePreset(
                "P99",
                ModelInspectionFixtureWidthProfile.Narrow360,
                ModelInspectionFixtureResourceProfile.HighContrastPreview,
                ModelInspectionFixtureTextProfile.Standard100,
                ModelInspectionFixtureMotionProfile.Normal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void EveryRequiredPreset_MapsToPreInitializationResourcesAndExactWidth()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ResourceDictionary applicationResources = Application.Current.Resources;
        object[] applicationKeys = applicationResources.Keys.Cast<object>()
            .ToArray();
        ResourceDictionary[] applicationDictionaries = applicationResources
            .MergedDictionaries.ToArray();
        var pairs = catalogue.Fixtures
            .SelectMany(fixture => fixture.Presets.Select(presetId =>
                (Fixture: fixture, PresetId: presetId)))
            .ToArray();

        foreach (var pair in pairs)
        {
            GraniteEdgeAI.ModelInspection.Fixtures.ModelInspectionFixturePreset
                policyPreset = catalogue.Policy.Value.Presets.Single(candidate =>
                    candidate.Id == pair.PresetId);
            var preset = DebugFixturePreset.FromPolicy(policyPreset);
            Assert.AreEqual(pair.PresetId, preset.Id);

            var resources = new ResourceDictionary();
            ModelInspectionFixturePreviewResources.Configure(resources, preset);
            double scale = preset.Text ==
                ModelInspectionFixtureTextProfile.Preview200 ? 2d : 1d;
            foreach ((string key, double standardValue) in Typography())
            {
                Assert.AreEqual(standardValue * scale,
                    Assert.IsInstanceOfType<double>(resources[key]),
                    pair.PresetId);
            }

            if (preset.Resources ==
                ModelInspectionFixtureResourceProfile.HighContrastPreview)
            {
                Assert.IsTrue(SemanticBrushKeys().All(resources.ContainsKey),
                    pair.PresetId);
            }
            else
            {
                CollectionAssert.AreEquivalent(
                    Typography().Select(item => item.Key).ToArray(),
                    resources.Keys.Cast<object>().Select(key => key.ToString())
                        .ToArray(),
                    pair.PresetId);
            }

            CollectionAssert.AreEquivalent(applicationKeys,
                applicationResources.Keys.Cast<object>().ToArray(),
                "Preset configuration must remain scoped to its destination.");
            CollectionAssert.AreEqual(applicationDictionaries,
                applicationResources.MergedDictionaries.ToArray(),
                "Preset configuration cannot mutate app dictionary ownership.");

            using var applier = new ModelInspectionFixturePresetApplier(
                new ModelInspectionPage(),
                preset);
            Assert.AreEqual(ExpectedWidth(policyPreset.Width), applier.HostWidth);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task GalleryPresetSelection_RejectsUndeclaredBeforeRetirementAndSwitchesWithFreshLifetime()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        DebugFixturePreset p01 = Preset(catalogue, "P01");
        DebugFixturePreset p09 = Preset(catalogue, "P09");
        var gallery = new ModelInspectionFixtureGalleryPage();
        var window = new Window { Content = gallery };
        window.Activate();
        try
        {
            await gallery.CatalogueLoaded;
            await gallery.SelectFixtureForTestingAsync("MI-002", p01);
            ModelInspectionFixtureHostPage? first = gallery.ActiveHost;
            Assert.IsNotNull(first,
                "Phase MI-002/P01 selection must produce an active host. " +
                $"ValidationStatus: {gallery.ViewModel.ValidationStatus}");
            var firstSession = first.Session;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                gallery.SelectFixtureForTestingAsync("MI-002", p09));
            ModelInspectionFixtureHostPage? afterRejectedSelection =
                gallery.ActiveHost;
            Assert.IsNotNull(afterRejectedSelection,
                "Phase MI-002/P09 rejection must preserve the active host. " +
                $"ValidationStatus: {gallery.ViewModel.ValidationStatus}");
            Assert.AreSame(first, afterRejectedSelection,
                "Preset validation must precede retirement.");
            Assert.AreEqual(0, firstSession.Evidence.SessionRetirementCount);

            await gallery.SelectFixtureForTestingAsync("MI-003", p09);
            ModelInspectionFixtureHostPage? second = gallery.ActiveHost;
            Assert.IsNotNull(second,
                "Phase MI-003/P09 selection must produce an active host. " +
                $"ValidationStatus: {gallery.ViewModel.ValidationStatus}");
            Assert.AreNotSame(first, second);
            Assert.AreNotSame(firstSession, second.Session);
            Assert.AreEqual(1, firstSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1, firstSession.Evidence.SessionDisposalCount);
            Assert.AreEqual("P09", gallery.CurrentPreset.Id);
            CollectionAssert.Contains(VisiblePresetLabels(gallery),
                "Resources: High Contrast Preview");
            Assert.IsFalse(VisiblePresetLabels(gallery).Any(label =>
                label.Contains("compliance", StringComparison.OrdinalIgnoreCase) ||
                label.Contains("OS", StringComparison.Ordinal)));

            var secondSession = second.Session;
            await gallery.SelectFixtureForTestingAsync("MI-003", p01);
            ModelInspectionFixtureHostPage? third = gallery.ActiveHost;
            Assert.IsNotNull(third,
                "Phase MI-003/P01 selection must produce an active host. " +
                $"ValidationStatus: {gallery.ViewModel.ValidationStatus}");
            Assert.AreNotSame(second, third);
            Assert.AreNotSame(secondSession, third.Session);
            Assert.AreEqual(1, second.PresetApplierDetachCount);
        }
        finally
        {
            gallery.CloseForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task VisiblePresetSelector_OffersOnlyDeclaredIdsAndCreatesFreshLifetime()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        var gallery = new ModelInspectionFixtureGalleryPage();
        var window = new Window { Content = gallery };
        window.Activate();
        try
        {
            await gallery.CatalogueLoaded;
            var list = (ListView)gallery.FindName("FixtureList");
            list.SelectedItem = gallery.ViewModel.Items.Single(item =>
                item.Id == fixture.Id);
            await gallery.SelectionCompletedForTesting;

            var selector = (ComboBox)gallery.FindName(
                "FixturePresetSelector");
            CollectionAssert.AreEqual(fixture.Presets.ToArray(),
                selector.Items.Cast<string>().ToArray());
            Assert.AreEqual("P01", selector.SelectedItem);
            ModelInspectionFixtureHostPage first = gallery.ActiveHost!;
            ModelInspectionFixtureSession firstSession = first.Session;

            selector.SelectedItem = "P09";
            await gallery.SelectionCompletedForTesting;

            Assert.AreEqual("P09", gallery.CurrentPreset.Id);
            Assert.AreNotSame(first, gallery.ActiveHost);
            Assert.AreNotSame(firstSession, gallery.ActiveHost!.Session);
            Assert.AreEqual(1, first.PresetApplierDetachCount);
            Assert.AreEqual(1, firstSession.Evidence.SessionRetirementCount);
            Assert.AreEqual(1, firstSession.Evidence.SessionDisposalCount);
            CollectionAssert.Contains(VisiblePresetLabels(gallery),
                "Width: Narrow 360");
            CollectionAssert.Contains(VisiblePresetLabels(gallery),
                "Resources: High Contrast Preview");
            CollectionAssert.Contains(VisiblePresetLabels(gallery),
                "Text: 100%");
            CollectionAssert.Contains(VisiblePresetLabels(gallery),
                "Motion: Normal");
        }
        finally
        {
            gallery.CloseForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task EveryRequiredPreset_ObservesRealLoadedLayoutAccessibilityAndResources()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        var observer = new ModelInspectionFixtureScreenObserver();
        foreach (ValidatedModelInspectionFixture fixture in catalogue.Fixtures)
        foreach (string presetId in fixture.Presets)
        {
            DebugFixturePreset preset = Preset(catalogue, presetId);
            ModelInspectionPresetExpectation expected =
                fixture.PresetExpectations[presetId];
            using var session = new ModelInspectionFixtureSession(
                fixture.Input,
                animationsEnabled: preset.Motion ==
                    ModelInspectionFixtureMotionProfile.Normal,
                () => new ImmediateAnimationDriver());
            var host = new ModelInspectionFixtureHostPage();
            host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
                fixture.Input,
                session,
                ModelInspectionFixtureGalleryPage
                    .ShouldStartInspectionOnLoaded(fixture),
                preset));
            var window = new Window { Content = host };
            window.Activate();
            try
            {
                await host.ApplyPresetAsync(CancellationToken.None);
                ModelInspectionPage page = host.ModelInspectionPage!;
                using IModelInspectionFixtureObservationSession screenSession =
                    observer.Begin(page);
                Task<string> run = new ModelInspectionFixtureScenarioRunner()
                    .RunAsync(
                        fixture.Input,
                        page,
                        session,
                        CancellationToken.None);
                Assert.AreEqual(fixture.Input.ObservationCheckpoint, await run,
                    Pair(fixture, preset));
                ModelInspectionFixturePresetObservation actual =
                    await host.ObservePresetForTestingAsync(
                        CancellationToken.None);
                ModelInspectionObservedScreen realScreen =
                    await screenSession.CaptureAsync(CancellationToken.None);
                AssertScreenMatchesFixture(catalogue, fixture, actual.Screen,
                    Pair(fixture, preset));

                Assert.AreEqual(expected.ResponsiveLayout,
                    actual.ResponsiveLayout, Pair(fixture, preset));
                Assert.IsGreaterThanOrEqualTo(
                    expected.MinimumContentColumnWidth,
                    actual.ContentColumnWidth,
                    Pair(fixture, preset));
                Assert.IsLessThanOrEqualTo(
                    expected.MaximumContentColumnWidth,
                    actual.ContentColumnWidth,
                    Pair(fixture, preset));
                string geometrySummary =
                    $"Host={actual.Geometry.HostWidth}x" +
                    $"{actual.Geometry.HostHeight}; " +
                    $"Content=({actual.Geometry.ContentX}," +
                    $"{actual.Geometry.ContentY}) " +
                    $"{actual.Geometry.ContentWidth}x" +
                    $"{actual.Geometry.ContentHeight}; " +
                    $"Extent={actual.Geometry.HorizontalExtent}x" +
                    $"{actual.Geometry.VerticalExtent}";
                Assert.IsTrue(actual.NoClipping,
                    $"{Pair(fixture, preset)}: NoClipping was false. " +
                    geometrySummary + "; " +
                    $"Observer={actual.NoClippingDiagnostic}; " +
                    BaseTextBlockClippingFailures(page));
                Assert.IsTrue(actual.NoOverlap,
                    $"{Pair(fixture, preset)}: NoOverlap was false. " +
                    geometrySummary);
                Assert.IsTrue(actual.AllRequiredContentReachable,
                    $"{Pair(fixture, preset)}: " +
                    "AllRequiredContentReachable was false. " +
                    geometrySummary);
                Assert.AreEqual(ExpectedPageState(preset.Width),
                    actual.PageState, Pair(fixture, preset));
                Assert.IsTrue(actual.ModelStates.All(state =>
                    state == ExpectedCardState(preset.Width, "Model")));
                Assert.IsTrue(actual.ContentStates.All(state =>
                    state == ExpectedCardState(preset.Width, "Content")));
                Assert.AreEqual(ExpectedCardState(preset.Width, "Action"),
                    actual.ActionState, Pair(fixture, preset));
                string expectedTextRoles = string.Join(", ",
                    expected.TextRoles
                        .Select(role => $"{role.Id}={role.Behavior}")
                        .OrderBy(entry => entry, StringComparer.Ordinal));
                string observedTextRoles = string.Join(", ",
                    actual.TextRoles
                        .Select(role => $"{role.Key}={role.Value}")
                        .OrderBy(entry => entry, StringComparer.Ordinal));
                Assert.AreEqual(expectedTextRoles, observedTextRoles,
                    $"{Pair(fixture, preset)}: TextRoles mismatch. " +
                    $"Expected=[{expectedTextRoles}]; " +
                    $"Observed=[{observedTextRoles}].");
                Assert.AreEqual(expected.ScrollOwner, actual.ScrollOwner,
                    $"{Pair(fixture, preset)}: ScrollOwner mismatch. " +
                    $"Expected={expected.ScrollOwner ?? "<null>"}; " +
                    $"Observed={actual.ScrollOwner ?? "<null>"}.");
                CollectionAssert.AreEqual(
                    realScreen.RowsAndScroll.OrderedRowIds.ToArray(),
                    actual.OrderedRowIds.ToArray(),
                    CollectionDiagnostic(
                        Pair(fixture, preset),
                        "OrderedRowIds.ExternalVsPreset",
                        realScreen.RowsAndScroll.OrderedRowIds,
                        actual.OrderedRowIds));
                Assert.IsGreaterThanOrEqualTo(
                    expected.MinimumPointerTargetWidth,
                    actual.MinimumPointerTargetWidth,
                    $"{Pair(fixture, preset)}: MinimumPointerTargetWidth " +
                    $"was below its bound. Bound=" +
                    $"{expected.MinimumPointerTargetWidth}; Observed=" +
                    $"{actual.MinimumPointerTargetWidth}.");
                Assert.IsGreaterThanOrEqualTo(
                    expected.MinimumPointerTargetHeight,
                    actual.MinimumPointerTargetHeight,
                    $"{Pair(fixture, preset)}: MinimumPointerTargetHeight " +
                    $"was below its bound. Bound=" +
                    $"{expected.MinimumPointerTargetHeight}; Observed=" +
                    $"{actual.MinimumPointerTargetHeight}.");
                CollectionAssert.AreEqual(expected.LogicalReadingOrder.ToArray(),
                    actual.LogicalReadingOrder.ToArray(),
                    CollectionDiagnostic(
                        Pair(fixture, preset),
                        "LogicalReadingOrder",
                        expected.LogicalReadingOrder,
                        actual.LogicalReadingOrder));
                CollectionAssert.AreEqual(expected.TabOrder.ToArray(),
                    actual.TabOrder.ToArray(),
                    CollectionDiagnostic(
                        Pair(fixture, preset),
                        "TabOrder",
                        expected.TabOrder,
                        actual.TabOrder));
                Assert.AreEqual(expected.FocusTarget, actual.FocusTarget,
                    $"{Pair(fixture, preset)}: FocusTarget mismatch. " +
                    $"Expected={expected.FocusTarget}; " +
                    $"Observed={actual.FocusTarget}.");
                Assert.IsTrue(
                    actual.SemanticBrushesResolvedWithoutColorOnlyMeaning,
                    $"{Pair(fixture, preset)}: " +
                    "SemanticBrushesResolvedWithoutColorOnlyMeaning was false.");
                AssertLoadedResourceProfile(page, preset, fixture,
                    $"{Pair(fixture, preset)}: ResourceProfile");
                if (preset.Text == ModelInspectionFixtureTextProfile.Preview200)
                {
                    Assert.IsTrue(actual.NaturalTextReflow,
                        $"{Pair(fixture, preset)}: NaturalTextReflow was false.");
                }
                Assert.AreEqual(realScreen.Figma.State,
                    actual.Screen.Figma.State,
                    $"{Pair(fixture, preset)}: " +
                    "Screen.Figma.State external-vs-preset mismatch. " +
                    $"Expected={realScreen.Figma.State}; " +
                    $"Observed={actual.Screen.Figma.State}.");
                CollectionAssert.AreEqual(
                    realScreen.RowsAndScroll.OrderedRowIds.ToArray(),
                    actual.Screen.RowsAndScroll.OrderedRowIds.ToArray(),
                    CollectionDiagnostic(
                        Pair(fixture, preset),
                        "Screen.RowsAndScroll.OrderedRowIds.ExternalVsPreset",
                        realScreen.RowsAndScroll.OrderedRowIds,
                        actual.Screen.RowsAndScroll.OrderedRowIds));
                CollectionAssert.AreEqual(
                    realScreen.Actions.Items.Select(item => item.Id).ToArray(),
                    actual.Screen.Actions.Items.Select(item => item.Id).ToArray(),
                    CollectionDiagnostic(
                        Pair(fixture, preset),
                        "Screen.Actions.ItemIds.ExternalVsPreset",
                        realScreen.Actions.Items.Select(item => item.Id),
                        actual.Screen.Actions.Items.Select(item => item.Id)));
                CollectionAssert.AreEqual(
                    fixture.Expected.RetainedIdentities.Ids.ToArray(),
                    realScreen.Retention.Ids.ToArray(),
                    CollectionDiagnostic(
                        Pair(fixture, preset),
                        "Screen.Retention.Ids",
                        fixture.Expected.RetainedIdentities.Ids,
                        realScreen.Retention.Ids));
                Assert.AreEqual(ExpectedWidth(preset.Width),
                    actual.Geometry.HostWidth, 1d,
                    $"{Pair(fixture, preset)}: Geometry.HostWidth mismatch. " +
                    $"Expected={ExpectedWidth(preset.Width)}; " +
                    $"Observed={actual.Geometry.HostWidth}; Tolerance=1.");
                Assert.IsTrue(actual.DispatcherDrained,
                    $"{Pair(fixture, preset)}: DispatcherDrained was false.");
                Assert.IsTrue(actual.LayoutUpdated,
                    $"{Pair(fixture, preset)}: LayoutUpdated was false.");
                Assert.IsTrue(actual.CompositionCommitted,
                    $"{Pair(fixture, preset)}: " +
                    "CompositionCommitted was false.");
                Assert.IsGreaterThanOrEqualTo(
                    expected.MinimumAnimationStarts,
                    session.Evidence.AnimationStartCount,
                    $"{Pair(fixture, preset)}: AnimationStartCount was below " +
                    $"its minimum. Minimum={expected.MinimumAnimationStarts}; " +
                    $"Observed={session.Evidence.AnimationStartCount}.");
                Assert.IsLessThanOrEqualTo(
                    expected.MaximumAnimationStarts,
                    session.Evidence.AnimationStartCount,
                    $"{Pair(fixture, preset)}: AnimationStartCount exceeded " +
                    $"its maximum. Maximum={expected.MaximumAnimationStarts}; " +
                    $"Observed={session.Evidence.AnimationStartCount}.");
                if (preset.Motion == ModelInspectionFixtureMotionProfile.Reduced)
                {
                    Assert.AreEqual(0, session.Evidence.AnimationStartCount,
                        $"{Pair(fixture, preset)}: ReducedMotion." +
                        "AnimationStartCount must be zero. Observed=" +
                        $"{session.Evidence.AnimationStartCount}.");
                }
            }
            finally
            {
                host.RetireForTesting();
                window.Content = null;
                window.Close();
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task TextRoleObservation_DistinguishesOrdinaryAndDeclaredMissingMetadata()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        (string FixtureId, string[] Expected)[] cases =
        [
            ("MI-002", []),
            ("MI-044",
            [
                "metadata-context=Wrap",
                "metadata-model-type=Wrap",
                "metadata-publisher=Wrap"
            ])
        ];
        var observedByFixture = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        foreach (var (fixtureId, _) in cases)
        {
            observedByFixture[fixtureId] = await ObserveTextRoleEntriesAsync(
                catalogue,
                fixtureId,
                "P01");
        }

        foreach (var (fixtureId, expected) in cases)
        {
            string[] observed = observedByFixture[fixtureId];
            CollectionAssert.AreEqual(expected, observed,
                $"{fixtureId}/P01: TextRoles mismatch. " +
                $"Expected=[{string.Join(", ", expected)}]; " +
                $"Observed=[{string.Join(", ", observed)}].");
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ResponsiveOverrideMutations_AreObservedForPageAndEveryCard()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        DebugFixturePreset preset = Preset(catalogue, "P09");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session, true, preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input,
                host.ModelInspectionPage!,
                session,
                CancellationToken.None);
            ModelInspectionFixturePresetObservation baseline =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage!;
            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            InspectionContentCard[] content =
            [
                (InspectionContentCard)page.FindName(
                    "InspectionContentCardControl"),
                (InspectionContentCard)page.FindName(
                    "OutgoingProgressContentCard")
            ];
            var actions = (InspectionActionCard)page.FindName(
                "InspectionActionCardControl");

            page.ApplyFixtureResponsiveState(
                ModelInspectionFixtureWidthProfile.Desktop1440);
            await host.WaitForPresetReapplicationForTestingAsync(
                CancellationToken.None);
            Assert.AreEqual(baseline.PageState,
                (await host.ObservePresetForTestingAsync(CancellationToken.None))
                    .PageState);
            model.ApplyFixtureResponsiveState(
                ModelInspectionFixtureWidthProfile.Desktop1440);
            await host.WaitForPresetReapplicationForTestingAsync(
                CancellationToken.None);
            CollectionAssert.AreEqual(baseline.ModelStates.ToArray(),
                (await host.ObservePresetForTestingAsync(
                    CancellationToken.None)).ModelStates.ToArray());
            foreach (InspectionContentCard card in content)
            {
                card.ApplyFixtureResponsiveState(
                    ModelInspectionFixtureWidthProfile.Desktop1440);
                await host.WaitForPresetReapplicationForTestingAsync(
                    CancellationToken.None);
                CollectionAssert.AreEqual(baseline.ContentStates.ToArray(),
                    (await host.ObservePresetForTestingAsync(
                        CancellationToken.None)).ContentStates.ToArray());
            }
            actions.ApplyFixtureResponsiveState(
                ModelInspectionFixtureWidthProfile.Desktop1440);
            await host.WaitForPresetReapplicationForTestingAsync(
                CancellationToken.None);
            Assert.AreEqual(baseline.ActionState,
                (await host.ObservePresetForTestingAsync(CancellationToken.None))
                    .ActionState);
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ResponsiveMutations_ChangeEveryRawLoadedStateBeforePermanentRepair()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        DebugFixturePreset preset = Preset(catalogue, "P09");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input, animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: true,
            resources => ModelInspectionFixturePreviewResources.Configure(
                resources, preset));
        using var applier = new ModelInspectionFixturePresetApplier(page, preset);
        var window = new Window { Content = page };
        window.Activate();
        try
        {
            await applier.ApplyAsync(CancellationToken.None);
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input, page, session, CancellationToken.None);
            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            InspectionContentCard[] content =
            [
                (InspectionContentCard)page.FindName(
                    "InspectionContentCardControl"),
                (InspectionContentCard)page.FindName(
                    "OutgoingProgressContentCard")
            ];
            var actions = (InspectionActionCard)page.FindName(
                "InspectionActionCardControl");

            page.ApplyFixtureResponsiveState(
                ModelInspectionFixtureWidthProfile.Desktop1440);
            ModelInspectionFixturePresetObservation raw =
                await applier.CaptureCurrentLoadedTreeForTestingAsync(
                    CancellationToken.None);
            Assert.AreEqual(ModelInspectionFixtureResponsiveLayout.Desktop,
                raw.ResponsiveLayout);
            Assert.AreEqual("DesktopPageState", raw.PageState);
            await applier.WaitForReapplicationAsync(CancellationToken.None);
            Assert.AreEqual("NarrowPageState",
                (await applier.ObserveAsync(CancellationToken.None)).PageState);

            model.ApplyFixtureResponsiveState(
                ModelInspectionFixtureWidthProfile.Desktop1440);
            CollectionAssert.AreEqual(new[] { "WideModelState" },
                (await applier.CaptureCurrentLoadedTreeForTestingAsync(
                    CancellationToken.None)).ModelStates.ToArray());
            await applier.WaitForReapplicationAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "NarrowModelState" },
                (await applier.ObserveAsync(CancellationToken.None))
                    .ModelStates.ToArray());

            for (int index = 0; index < content.Length; index++)
            {
                content[index].ApplyFixtureResponsiveState(
                    ModelInspectionFixtureWidthProfile.Desktop1440);
                ModelInspectionFixturePresetObservation rawContent =
                    await applier.CaptureCurrentLoadedTreeForTestingAsync(
                        CancellationToken.None);
                Assert.AreEqual("WideContentState",
                    rawContent.ContentStates[index], $"content[{index}]");
                await applier.WaitForReapplicationAsync(CancellationToken.None);
                Assert.AreEqual("NarrowContentState",
                    (await applier.ObserveAsync(CancellationToken.None))
                        .ContentStates[index], $"content[{index}]");
            }

            actions.ApplyFixtureResponsiveState(
                ModelInspectionFixtureWidthProfile.Desktop1440);
            Assert.AreEqual("WideActionState",
                (await applier.CaptureCurrentLoadedTreeForTestingAsync(
                    CancellationToken.None)).ActionState);
            await applier.WaitForReapplicationAsync(CancellationToken.None);
            Assert.AreEqual("NarrowActionState",
                (await applier.ObserveAsync(CancellationToken.None)).ActionState);
        }
        finally
        {
            page.RetireForFixture();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Observation_DetectsGeometryTargetBrushTextAndOuterScrollMutations()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-048");
        DebugFixturePreset preset = Preset(catalogue, "P07");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: false,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session, true, preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input,
                host.ModelInspectionPage!,
                session,
                CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage!;
            FrameworkElement header = (FrameworkElement)page.FindName(
                "Header");
            var actionCard = (InspectionActionCard)page.FindName(
                "InspectionActionCardControl");
            var cancel = (Button)actionCard.FindName("CancelActionButton");
            Assert.AreEqual("Cancel model inspection",
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(
                    cancel));
            var title = Find<TextBlock>(page, text =>
                text.Text == "Model inspection");
            var scroll = (ScrollViewer)page.FindName(
                "InspectionPageScrollViewer");

            header.Translation = new System.Numerics.Vector3(2000, 0, 0);
            Assert.IsFalse((await host.ObservePresetForTestingAsync(
                CancellationToken.None)).AllRequiredContentReachable,
                "A translated required header must be unreachable.");
            header.Translation = System.Numerics.Vector3.Zero;
            FrameworkElement model = (FrameworkElement)page.FindName(
                "InspectionModelCardControl");
            FrameworkElement actions = (FrameworkElement)page.FindName(
                "InspectionActionCardControl");
            Windows.Foundation.Point modelOrigin = model.TransformToVisual(page)
                .TransformPoint(default);
            Windows.Foundation.Point actionOrigin = actions.TransformToVisual(page)
                .TransformPoint(default);
            actions.Translation = new System.Numerics.Vector3(
                (float)(modelOrigin.X - actionOrigin.X),
                (float)(modelOrigin.Y - actionOrigin.Y), 0);
            Assert.IsFalse((await host.ObservePresetForTestingAsync(
                CancellationToken.None)).NoOverlap,
                "Translated top-level cards must overlap.");
            actions.Translation = System.Numerics.Vector3.Zero;
            cancel.MinWidth = 0;
            cancel.MaxWidth = 12;
            cancel.Width = 12;
            cancel.MinHeight = 0;
            cancel.MaxHeight = 12;
            cancel.Height = 12;
            ModelInspectionFixturePresetObservation target =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            Assert.IsLessThanOrEqualTo(12.5d, cancel.ActualWidth);
            Assert.IsLessThanOrEqualTo(12.5d, cancel.ActualHeight);
            Assert.IsLessThan(44d, target.MinimumPointerTargetWidth);
            Assert.IsLessThan(44d, target.MinimumPointerTargetHeight);
            title.Foreground = null;
            Assert.IsFalse((await host.ObservePresetForTestingAsync(
                CancellationToken.None))
                .SemanticBrushesResolvedWithoutColorOnlyMeaning,
                "Removing the loaded title brush must be observed.");
            title.FontSize = 1d;
            Assert.IsFalse((await host.ObservePresetForTestingAsync(
                CancellationToken.None)).NaturalTextReflow,
                "A one-pixel title must fail natural text reflow.");
            scroll.VerticalScrollMode = ScrollMode.Disabled;
            Assert.AreNotEqual(fixture.PresetExpectations["P07"].ScrollOwner,
                (await host.ObservePresetForTestingAsync(
                    CancellationToken.None)).ScrollOwner);
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Observation_DetectsOutcomeGlyphOverflowBeyondSemanticIconContainer()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-002");
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input,
            session,
            ModelInspectionFixtureGalleryPage.ShouldStartInspectionOnLoaded(
                fixture),
            preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await host.ApplyPresetAsync(CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage!;
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input,
                page,
                session,
                CancellationToken.None);
            ModelInspectionFixturePresetObservation baseline =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            Assert.IsTrue(baseline.NoClipping, Pair(fixture, preset));
            Assert.AreEqual(string.Empty, baseline.NoClippingDiagnostic,
                Pair(fixture, preset));

            var outcome = (InspectionOutcomeCard)page.FindName(
                "InspectionOutcomeCardControl");
            var icon = (InspectionStatusGlyph)outcome.FindName("OutcomeIcon");
            var container = (Border)outcome.FindName("OutcomeIconContainer");
            Assert.AreEqual(20d, container.ActualWidth, 1d,
                "OutcomeIconContainer width");
            Assert.AreEqual(20d, container.ActualHeight, 1d,
                "OutcomeIconContainer height");

            Transform originalTransform = icon.RenderTransform;
            try
            {
                icon.RenderTransform = new TranslateTransform { X = 100d, Y = 100d };
                ModelInspectionFixturePresetObservation overflow =
                    await host.ObservePresetForTestingAsync(
                        CancellationToken.None);

                var translatedOrigin = icon.TransformToVisual(container)
                    .TransformPoint(default);
                Assert.IsGreaterThanOrEqualTo(80d, translatedOrigin.X,
                    "The glyph overflow mutation must escape the semantic container horizontally.");
                Assert.IsGreaterThanOrEqualTo(80d, translatedOrigin.Y,
                    "The glyph overflow mutation must escape the semantic container vertically.");
                Assert.IsFalse(overflow.NoClipping,
                    "A semantic icon outside its 20x20 container must be observed.");
                StringAssert.Contains(
                    overflow.NoClippingDiagnostic,
                    "OutcomeIcon");
            }
            finally
            {
                icon.RenderTransform = originalTransform;
            }
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DisplayOnlyItemContainers_StayOutOfTabOrderWhileDeclaredScrollOwnersRemainFocusable()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();

        await AssertDisplayOnlyContainerTabSemanticsAsync(
            catalogue,
            fixtureId: "MI-003",
            cardName: "InspectionModelCardControl",
            innerName: "InspectionChecksItemsControl",
            outerName: "InspectionChecksScrollViewer");
        await AssertDisplayOnlyContainerTabSemanticsAsync(
            catalogue,
            fixtureId: "MI-007",
            cardName: "InspectionContentCardControl",
            innerName: "ExpandedReportItemsControl",
            outerName: "ExpandedReportScrollViewer");
        await AssertDisplayOnlyContainerTabSemanticsAsync(
            catalogue,
            fixtureId: "MI-006",
            cardName: "InspectionContentCardControl",
            innerName: "FindingsItemsRepeater",
            outerName: null);
    }

    private static async Task AssertDisplayOnlyContainerTabSemanticsAsync(
        ModelInspectionFixtureCatalogue catalogue,
        string fixtureId,
        string cardName,
        string innerName,
        string? outerName)
    {
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == fixtureId);
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input,
            session,
            ModelInspectionFixtureGalleryPage.ShouldStartInspectionOnLoaded(
                fixture),
            preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await host.ApplyPresetAsync(CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage ??
                throw new AssertFailedException(
                    $"{fixtureId}/P01: host page was not created.");
            string checkpoint = await new ModelInspectionFixtureScenarioRunner()
                .RunAsync(
                    fixture.Input,
                    page,
                    session,
                    CancellationToken.None);
            Assert.AreEqual(fixture.Input.ObservationCheckpoint, checkpoint,
                Pair(fixture, preset));

            ModelInspectionFixturePresetObservation observed =
                await host.ObservePresetForTestingAsync(
                    CancellationToken.None);
            FrameworkElement card = page.FindName(cardName) as FrameworkElement ??
                throw new AssertFailedException(
                    $"{Pair(fixture, preset)}: {cardName} was not found.");
            ItemsControl inner = card.FindName(innerName) as ItemsControl ??
                throw new AssertFailedException(
                    $"{Pair(fixture, preset)}: {innerName} was not found.");

            if (outerName is not null)
            {
                ScrollViewer outer = card.FindName(outerName) as ScrollViewer ??
                    throw new AssertFailedException(
                        $"{Pair(fixture, preset)}: {outerName} was not found.");
                Assert.IsTrue(outer.IsLoaded,
                    $"{Pair(fixture, preset)}: {outerName} was not loaded.");
                Assert.IsTrue(
                    IsEffectivelyVisibleForClippingDiagnostic(outer),
                    $"{Pair(fixture, preset)}: {outerName} was not visible.");
                Assert.IsTrue(outer.IsTabStop,
                    $"{Pair(fixture, preset)}: {outerName} must remain " +
                    "keyboard reachable.");
                string accessibleName = Microsoft.UI.Xaml.Automation
                    .AutomationProperties.GetName(outer);
                Assert.IsFalse(string.IsNullOrWhiteSpace(accessibleName),
                    $"{Pair(fixture, preset)}: {outerName} needs an " +
                    "accessible name.");
                Assert.IsTrue(outer.Focus(FocusState.Programmatic),
                    $"{Pair(fixture, preset)}: {outerName} rejected " +
                    "programmatic focus.");
                Assert.AreSame(
                    outer,
                    Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(
                        page.XamlRoot),
                    $"{Pair(fixture, preset)}: {outerName} did not own focus.");
            }

            Assert.IsTrue(inner.IsLoaded,
                $"{Pair(fixture, preset)}: {innerName} was not loaded.");
            Assert.IsTrue(
                IsEffectivelyVisibleForClippingDiagnostic(inner),
                $"{Pair(fixture, preset)}: {innerName} was not visible.");
            Assert.IsFalse(inner.IsTabStop,
                $"{Pair(fixture, preset)}: {innerName} is display-only and " +
                "must not create a nested tab stop.");
            CollectionAssert.DoesNotContain(
                observed.TabOrder.ToArray(),
                $"ItemsControl:{innerName}",
                $"{Pair(fixture, preset)}: {innerName} leaked a fallback " +
                "tab-order identity.");
            ModelInspectionPresetExpectation expected =
                fixture.PresetExpectations[preset.Id];
            CollectionAssert.AreEqual(
                expected.TabOrder.ToArray(),
                observed.TabOrder.ToArray(),
                CollectionDiagnostic(
                    Pair(fixture, preset),
                    "TabOrder.DisplayOnlyContainers",
                    expected.TabOrder,
                    observed.TabOrder));
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Observation_DetectsReadingTabTextRoleAndDeclaredInnerScrollMutations()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-007");
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input, animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session, true, preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input, host.ModelInspectionPage!, session,
                CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage!;
            ModelInspectionFixturePresetObservation baseline =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            var actions = (InspectionActionCard)page.FindName(
                "InspectionActionCardControl");
            var chooseAnother = (Button)actions.FindName(
                "SecondaryActionOneButton");
            int originalTabIndex = chooseAnother.TabIndex;
            chooseAnother.TabIndex = -100;
            ModelInspectionFixturePresetObservation tabMutation =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            CollectionAssert.AreNotEqual(baseline.TabOrder.ToArray(),
                tabMutation.TabOrder.ToArray());
            chooseAnother.TabIndex = originalTabIndex;

            var chooseHost = (FrameworkElement)actions.FindName(
                "SecondaryActionOneHost");
            var primaryHost = (FrameworkElement)actions.FindName(
                "PrimaryActionHost");
            int chooseRow = Grid.GetRow(chooseHost);
            int chooseColumn = Grid.GetColumn(chooseHost);
            int primaryRow = Grid.GetRow(primaryHost);
            int primaryColumn = Grid.GetColumn(primaryHost);
            Assert.AreEqual(chooseRow, primaryRow,
                "Task 7 keeps fitted actions in one horizontal row.");
            Assert.AreNotEqual(chooseColumn, primaryColumn,
                "Visible horizontal actions require distinct columns.");
            Grid.SetColumn(chooseHost, primaryColumn);
            Grid.SetColumn(primaryHost, chooseColumn);
            ModelInspectionFixturePresetObservation readingMutation =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            CollectionAssert.AreNotEqual(baseline.LogicalReadingOrder.ToArray(),
                readingMutation.LogicalReadingOrder.ToArray());
            Grid.SetRow(chooseHost, chooseRow);
            Grid.SetColumn(chooseHost, chooseColumn);
            Grid.SetRow(primaryHost, primaryRow);
            Grid.SetColumn(primaryHost, primaryColumn);

            var content = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            var expandedRows = (ItemsControl)content.FindName(
                "ExpandedReportItemsControl");
            TextBlock row = Descendants<TextBlock>(expandedRows).First(text =>
                text.Text == "Conversion required");
            Border rowContainer = Ancestor<Border>(row);
            double originalMinHeight = rowContainer.MinHeight;
            double originalHeight = rowContainer.Height;
            double originalMaxHeight = rowContainer.MaxHeight;
            Thickness originalPadding = rowContainer.Padding;
            try
            {
                rowContainer.MinHeight = 0;
                rowContainer.Height = 1;
                rowContainer.MaxHeight = 1;
                rowContainer.Padding = new Thickness(0);
                ModelInspectionFixturePresetObservation clippingMutation =
                    await host.ObservePresetForTestingAsync(
                        CancellationToken.None);
                Assert.IsLessThanOrEqualTo(1.5d,
                    rowContainer.ActualHeight,
                    "The nested clipping mutation must realize at one pixel.");
                Assert.IsFalse(clippingMutation.NoClipping,
                    "A clipped nested content row must be observed.");
            }
            finally
            {
                rowContainer.MinHeight = originalMinHeight;
                rowContainer.Height = originalHeight;
                rowContainer.MaxHeight = originalMaxHeight;
                rowContainer.Padding = originalPadding;
            }

            var primary = (Button)actions.FindName("PrimaryActionButton");
            Windows.Foundation.Point chooseOrigin = chooseAnother
                .TransformToVisual(page).TransformPoint(default);
            Windows.Foundation.Point primaryOrigin = primary
                .TransformToVisual(page).TransformPoint(default);
            primary.Translation = new System.Numerics.Vector3(
                (float)(chooseOrigin.X - primaryOrigin.X),
                (float)(chooseOrigin.Y - primaryOrigin.Y), 0);
            Assert.IsFalse((await host.ObservePresetForTestingAsync(
                CancellationToken.None)).NoOverlap,
                "Overlapping nested actions must be observed.");
            primary.Translation = System.Numerics.Vector3.Zero;

            row.TextWrapping = TextWrapping.NoWrap;
            row.TextTrimming = TextTrimming.CharacterEllipsis;
            ModelInspectionFixturePresetObservation textMutation =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            Assert.AreEqual(ModelInspectionFixtureTextBehavior.Truncate,
                textMutation.TextRoles["conversion-required-row"]);

            var contentScroll = (ScrollViewer)content.FindName(
                "ExpandedReportScrollViewer");
            Assert.AreEqual("content-list", baseline.ScrollOwner);
            contentScroll.VerticalScrollMode = ScrollMode.Disabled;
            Assert.AreNotEqual("content-list",
                (await host.ObservePresetForTestingAsync(
                    CancellationToken.None)).ScrollOwner);
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }

        await AssertDeclaredModelScrollMutationAsync(catalogue);
    }

    private static async Task AssertDeclaredModelScrollMutationAsync(
        ModelInspectionFixtureCatalogue catalogue)
    {
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input, animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session, true, preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input, host.ModelInspectionPage!, session,
                CancellationToken.None);
            var model = (InspectionModelCard)host.ModelInspectionPage!.FindName(
                "InspectionModelCardControl");
            var modelScroll = (ScrollViewer)model.FindName(
                "InspectionChecksScrollViewer");
            Assert.AreEqual("model-card",
                (await host.ObservePresetForTestingAsync(
                    CancellationToken.None)).ScrollOwner);
            modelScroll.VerticalScrollMode = ScrollMode.Disabled;
            Assert.AreNotEqual("model-card",
                (await host.ObservePresetForTestingAsync(
                    CancellationToken.None)).ScrollOwner);
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Observation_DetectsRequiredRowOutsideReachability()
    {
        await RunExpandedReadyFixtureAsync(async (page, host) =>
        {
            ModelInspectionFixturePresetObservation baseline =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            Assert.IsTrue(baseline.AllRequiredContentReachable);

            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            var rows = (ItemsControl)model.FindName(
                "InspectionChecksItemsControl");
            TextBlock packageText = Descendants<TextBlock>(rows).Single(text =>
                text.Text.StartsWith("GGUF version 3;", StringComparison.Ordinal));
            Border packageRow = Ancestor<Border>(packageText);
            Assert.AreEqual("InspectionCheckRow", packageRow.Tag);
            InspectionStatusGlyph statusIcon =
                Descendants<InspectionStatusGlyph>(packageRow)
                .Single(IsEffectivelyVisibleForClippingDiagnostic);
            Assert.AreEqual(
                InspectionStatusGlyphKind.Success,
                statusIcon.Kind);
            Assert.AreEqual(22d, statusIcon.SurfaceSize, 0.01d);
            Assert.IsTrue(packageRow.IsLoaded);
            Assert.IsGreaterThan(1d, packageRow.ActualWidth);
            Assert.IsGreaterThan(1d, packageRow.ActualHeight);
            Assert.IsTrue(statusIcon.IsLoaded);
            Assert.IsGreaterThan(1d, statusIcon.ActualWidth);
            Assert.IsGreaterThan(1d, statusIcon.ActualHeight);

            var pageScroll = (ScrollViewer)page.FindName(
                "InspectionPageScrollViewer");
            var scrollContent = (FrameworkElement)page.FindName(
                "InspectionScrollContent");
            Transform originalRenderTransform = packageRow.RenderTransform;
            Windows.Foundation.Point originalRenderTransformOrigin =
                packageRow.RenderTransformOrigin;
            Windows.Foundation.Rect baselineRowBounds = packageRow
                .TransformToVisual(scrollContent).TransformBounds(
                    new Windows.Foundation.Rect(
                        0d,
                        0d,
                        packageRow.ActualWidth,
                        packageRow.ActualHeight));
            try
            {
                packageRow.RenderTransformOrigin = default;
                packageRow.RenderTransform = new TranslateTransform
                {
                    X = -4d - baselineRowBounds.X
                };
                await WaitForLoadedTreeBoundariesAsync(page);

                Windows.Foundation.Rect rowBounds = packageRow
                    .TransformToVisual(scrollContent).TransformBounds(
                        new Windows.Foundation.Rect(
                            0d,
                            0d,
                            packageRow.ActualWidth,
                            packageRow.ActualHeight));
                Windows.Foundation.Rect iconBounds = statusIcon
                    .TransformToVisual(scrollContent).TransformBounds(
                        new Windows.Foundation.Rect(
                            0d,
                            0d,
                            statusIcon.ActualWidth,
                            statusIcon.ActualHeight));
                Assert.IsTrue(packageRow.IsLoaded);
                Assert.IsGreaterThan(1d, packageRow.ActualWidth);
                Assert.IsGreaterThan(1d, packageRow.ActualHeight);
                Assert.AreEqual(-4d, rowBounds.X, 1d,
                    "The XAML transform must realize the intended leading " +
                    "edge in scroll-content coordinates.");
                Assert.IsTrue(rowBounds.X < -1d,
                    "The required package row's leading edge must be " +
                    "physically outside the reachable extent.");
                Assert.IsTrue(statusIcon.IsLoaded);
                Assert.IsTrue(
                    IsEffectivelyVisibleForClippingDiagnostic(statusIcon),
                    "The semantic package status icon must remain visible.");
                Assert.IsGreaterThan(1d, statusIcon.ActualWidth);
                Assert.IsGreaterThan(1d, statusIcon.ActualHeight);
                Assert.IsGreaterThan(1d, iconBounds.Width);
                Assert.IsGreaterThan(1d, iconBounds.Height);
                Assert.IsTrue(iconBounds.X < 0d,
                    "The semantic status glyph must follow its translated " +
                    "required row beyond the reachable leading edge.");
                Assert.IsTrue(
                    iconBounds.X + iconBounds.Width <=
                        pageScroll.ExtentWidth,
                    "The semantic status icon's trailing edge must remain " +
                    "inside the reachable horizontal extent.");
                Assert.IsFalse((await host.ObservePresetForTestingAsync(
                        CancellationToken.None))
                    .AllRequiredContentReachable,
                    "An individually translated required row must be " +
                    "reported as unreachable.");
            }
            finally
            {
                packageRow.RenderTransformOrigin =
                    originalRenderTransformOrigin;
                packageRow.RenderTransform = originalRenderTransform;
                await WaitForLoadedTreeBoundariesAsync(page);
            }
        });
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Observation_DetectsOverlappingRequiredRows()
    {
        await RunExpandedReadyFixtureAsync(async (page, host) =>
        {
            ModelInspectionFixturePresetObservation baseline =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            Assert.IsTrue(baseline.NoOverlap);

            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            var rows = (ItemsControl)model.FindName(
                "InspectionChecksItemsControl");
            TextBlock packageText = Descendants<TextBlock>(rows).Single(text =>
                text.Text.StartsWith("GGUF version 3;", StringComparison.Ordinal));
            TextBlock configurationText = Descendants<TextBlock>(rows).Single(
                text => text.Text.StartsWith(
                    "Architecture llama;",
                    StringComparison.Ordinal));
            Border packageRow = Ancestor<Border>(packageText);
            Border configurationRow = Ancestor<Border>(configurationText);
            Assert.AreEqual("InspectionCheckRow", packageRow.Tag);
            Assert.AreEqual("InspectionCheckRow", configurationRow.Tag);
            Assert.AreNotSame(packageRow, configurationRow);
            Assert.IsTrue(packageRow.IsLoaded && configurationRow.IsLoaded);
            Assert.IsGreaterThan(1d, packageRow.ActualWidth);
            Assert.IsGreaterThan(1d, packageRow.ActualHeight);
            Assert.IsGreaterThan(1d, configurationRow.ActualWidth);
            Assert.IsGreaterThan(1d, configurationRow.ActualHeight);

            System.Numerics.Vector3 originalTranslation =
                configurationRow.Translation;
            try
            {
                Windows.Foundation.Point packageOrigin = packageRow
                    .TransformToVisual(page).TransformPoint(default);
                Windows.Foundation.Point configurationOrigin = configurationRow
                    .TransformToVisual(page).TransformPoint(default);
                configurationRow.Translation = new System.Numerics.Vector3(
                    (float)(packageOrigin.X - configurationOrigin.X),
                    (float)(packageOrigin.Y - configurationOrigin.Y),
                    0f);
                await WaitForLoadedTreeBoundariesAsync(page);

                double effectiveConfigurationX = configurationOrigin.X +
                    configurationRow.Translation.X;
                double effectiveConfigurationY = configurationOrigin.Y +
                    configurationRow.Translation.Y;
                Assert.AreEqual(
                    packageOrigin.X,
                    effectiveConfigurationX,
                    1d,
                    "The required rows must physically overlap on X.");
                Assert.AreEqual(
                    packageOrigin.Y,
                    effectiveConfigurationY,
                    1d,
                    "The required rows must physically overlap on Y.");
                Assert.IsFalse((await host.ObservePresetForTestingAsync(
                        CancellationToken.None)).NoOverlap,
                    "Overlapping individual required rows must be observed.");
            }
            finally
            {
                configurationRow.Translation = originalTranslation;
                await WaitForLoadedTreeBoundariesAsync(page);
            }
        });
    }

    private static async Task RunExpandedReadyFixtureAsync(
        Func<ModelInspectionPage, ModelInspectionFixtureHostPage, Task> assertion)
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input, animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session, true, preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input, host.ModelInspectionPage!, session,
                CancellationToken.None);
            await assertion(host.ModelInspectionPage!, host);
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Observation_RejectsExpandedInnerScrollOwnerHeightBound()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input, animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session, true, preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input, host.ModelInspectionPage!, session,
                CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage!;
            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            var owner = (ScrollViewer)model.FindName(
                "InspectionChecksScrollViewer");
            var viewport = (FrameworkElement)model.FindName(
                "InspectionDetailsViewport");
            ModelInspectionFixturePresetObservation baseline =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            Assert.AreEqual("model-card", baseline.ScrollOwner);
            Assert.AreEqual(ScrollMode.Enabled, owner.VerticalScrollMode);
            Assert.AreEqual(172d, owner.MaxHeight, 0.01d);
            Assert.AreEqual(172d, viewport.Height, 0.01d);
            Assert.IsTrue(owner.IsLoaded && viewport.IsLoaded);
            Assert.IsGreaterThan(0d, owner.ActualHeight);

            double originalOwnerMaxHeight = owner.MaxHeight;
            double originalViewportHeight = viewport.Height;
            try
            {
                owner.MaxHeight = 500d;
                viewport.Height = 500d;
                await WaitForLoadedTreeBoundariesAsync(page);

                Assert.AreEqual(ScrollMode.Enabled, owner.VerticalScrollMode,
                    "The bound mutation must not disable scrolling.");
                Assert.AreEqual(500d, owner.MaxHeight, 0.01d);
                Assert.AreEqual(500d, viewport.ActualHeight, 1d,
                    "The expanded production viewport must be realized.");
                Assert.IsGreaterThan(172d, owner.ActualHeight,
                    "The enabled inner owner must physically exceed its " +
                    "approved 172px bound.");
                Assert.AreNotEqual("model-card",
                    (await host.ObservePresetForTestingAsync(
                        CancellationToken.None)).ScrollOwner,
                    "An enabled but unbounded inner scroll owner must not be " +
                    "reported as the approved model-card owner.");
            }
            finally
            {
                owner.MaxHeight = originalOwnerMaxHeight;
                viewport.Height = originalViewportHeight;
                await WaitForLoadedTreeBoundariesAsync(page);
            }
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Observation_DetectsProgressAndContentRowSemanticCueMutations()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        await AssertRowSemanticCueMutationAsync(
            catalogue, "MI-001", "Waiting");
        await AssertRowSemanticCueMutationAsync(
            catalogue, "MI-007", "Information");
    }

    private static async Task AssertRowSemanticCueMutationAsync(
        ModelInspectionFixtureCatalogue catalogue,
        string fixtureId,
        string statusText)
    {
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == fixtureId);
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input, animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session,
            ModelInspectionFixtureGalleryPage.ShouldStartInspectionOnLoaded(
                fixture), preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input, host.ModelInspectionPage!, session,
                CancellationToken.None);
            ModelInspectionFixturePresetObservation baseline =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            Assert.IsTrue(
                baseline.SemanticBrushesResolvedWithoutColorOnlyMeaning,
                fixtureId);
            var content = (InspectionContentCard)host.ModelInspectionPage!
                .FindName("InspectionContentCardControl");
            TextBlock cue = Descendants<TextBlock>(content).First(text =>
                text.Visibility == Visibility.Visible &&
                text.Text == statusText && text.Foreground is not null);
            cue.Foreground = null;

            Assert.IsFalse((await host.ObservePresetForTestingAsync(
                    CancellationToken.None))
                .SemanticBrushesResolvedWithoutColorOnlyMeaning, fixtureId);
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Preview200_IsConfiguredBeforeInitializeAndReflowsLaterRealizedRows()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-049");
        DebugFixturePreset preset = Preset(catalogue, "P08");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input, session, true, preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input,
                host.ModelInspectionPage!,
                session,
                CancellationToken.None);
            ModelInspectionFixturePresetObservation actual =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage!;
            TextBlock[] realizedText = Descendants<TextBlock>(page)
                .Where(text => text.Visibility == Visibility.Visible &&
                    text.ActualWidth > 0 && text.ActualHeight > 0 &&
                    !HasAncestor<InspectionStatusGlyph>(text))
                .ToArray();
            TextBlock pageTitle = (TextBlock)page.FindName("PageTitle");
            var content = (InspectionContentCard)page.FindName(
                "InspectionContentCardControl");
            TextBlock section = (TextBlock)content.FindName(
                "FindingsSectionTitle");
            TextBlock body = realizedText.First(text =>
                text.Text == "Model result unavailable");
            TextBlock helper = Descendants<TextBlock>(content).First(text =>
                text.Text.StartsWith(
                    "Operational failure detail remains bounded",
                    StringComparison.Ordinal) &&
                text.Visibility == Visibility.Visible &&
                text.ActualWidth > 0 && text.ActualHeight > 0);
            var model = (InspectionModelCard)page.FindName(
                "InspectionModelCardControl");
            TextBlock label = Descendants<TextBlock>(model).First(text =>
                text.Text == "GGUF");

            Assert.AreEqual(64d, pageTitle.FontSize);
            Assert.AreEqual(36d, section.FontSize);
            Assert.AreEqual(28d, body.FontSize);
            Assert.AreEqual(24d, helper.FontSize);
            Assert.AreEqual(20d, label.FontSize);
            Assert.AreEqual(20d, section.Margin.Top);
            Assert.IsTrue(
                section.ActualHeight + 1d < section.DesiredSize.Height,
                "The named heading must retain its margin-bearing desired " +
                "height so this oracle distinguishes it from clipping.");
            double sectionHeightIncludingMargin = section.ActualHeight +
                Math.Max(0d, section.Margin.Top) +
                Math.Max(0d, section.Margin.Bottom);
            Assert.IsTrue(
                sectionHeightIncludingMargin + 1d >=
                    section.DesiredSize.Height,
                "Natural reflow must compare margin-normalized height.");
            Assert.IsTrue(actual.NoClipping,
                actual.NoClippingDiagnostic);
            Assert.IsTrue(actual.NaturalTextReflow,
                "A naturally sized heading with positive vertical margin " +
                "must not be reported as clipped text.");
            Assert.IsTrue(realizedText.All(text =>
                text.ActualHeight +
                    Math.Max(0d, text.Margin.Top) +
                    Math.Max(0d, text.Margin.Bottom) + 1d >=
                        text.DesiredSize.Height));
            Assert.IsTrue(host.ResourcesConfiguredBeforeInitializeForTesting);

            double originalFontSize = section.FontSize;
            try
            {
                section.FontSize = 1d;
                ModelInspectionFixturePresetObservation clipped =
                    await host.ObservePresetForTestingAsync(
                        CancellationToken.None);

                Assert.IsTrue(section.IsLoaded,
                    "The named section must remain loaded after mutation.");
                Assert.AreEqual(Visibility.Visible, section.Visibility,
                    "The named section must remain visible after mutation.");
                Assert.AreEqual(1d, section.FontSize,
                    "The loaded section typography mutation must remain applied.");
                Assert.IsFalse(clipped.NaturalTextReflow,
                    "A later-realized heading with the wrong loaded font size " +
                    "must fail natural text reflow.");
            }
            finally
            {
                section.FontSize = originalFontSize;
            }
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task EveryRequiredPreset_NormalAndReducedMotionTwinsReachEquivalentFinalScreenAndGeometry()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        foreach (ValidatedModelInspectionFixture fixture in catalogue.Fixtures)
        foreach (string presetId in fixture.Presets)
        {
            DebugFixturePreset preset = Preset(catalogue, presetId);
            PresetRun normal = await RunPresetTwinAsync(
                fixture,
                preset,
                animationsEnabled: true);
            PresetRun reduced = await RunPresetTwinAsync(
                fixture,
                preset,
                animationsEnabled: false);

            if (preset.Motion == ModelInspectionFixtureMotionProfile.Normal)
            {
                ModelInspectionPresetExpectation selectedExpectation =
                    fixture.PresetExpectations[presetId];
                Assert.IsGreaterThanOrEqualTo(
                    selectedExpectation.MinimumAnimationStarts,
                    normal.AnimationStartCount,
                    Pair(fixture, preset));
                Assert.IsLessThanOrEqualTo(
                    selectedExpectation.MaximumAnimationStarts,
                    normal.AnimationStartCount,
                    Pair(fixture, preset));
            }
            Assert.AreEqual(0, reduced.AnimationStartCount,
                Pair(fixture, preset));
            AssertScreenMatchesFixture(catalogue, fixture, normal.Screen,
                $"{Pair(fixture, preset)}/normal");
            AssertScreenMatchesFixture(catalogue, fixture, reduced.Screen,
                $"{Pair(fixture, preset)}/reduced");
            AssertObservedScreensEquivalent(normal.Screen, reduced.Screen,
                Pair(fixture, preset));
            AssertGeometryEqual(normal.Geometry, reduced.Geometry,
                Pair(fixture, preset));
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task GalleryNormalPreset_UsesAuditedProductionAnimationDriver()
    {
        var gallery = new ModelInspectionFixtureGalleryPage();
        var window = new Window { Content = gallery };
        window.Activate();
        try
        {
            await gallery.CatalogueLoaded;
            ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
            await gallery.SelectFixtureForTestingAsync(
                "MI-003",
                Preset(catalogue, "P01"));

            ModelInspectionFixtureSession session = gallery.ActiveHost!.Session;
            Assert.IsGreaterThan(0, session.Evidence.AnimationStartCount);
            Assert.AreEqual(
                "WinUiModelInspectionAnimationDriver",
                InnerAnimationDriver(session).GetType().Name);
        }
        finally
        {
            gallery.CloseForTesting();
            window.Content = null;
            window.Close();
        }
    }

    private static object InnerAnimationDriver(
        ModelInspectionFixtureSession session)
    {
        object audited = typeof(ModelInspectionFixtureSession).GetField(
                "animationDriver",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(session)!;
        return audited.GetType().GetField(
                "inner",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(audited)!;
    }

    private static async Task<PresetRun> RunPresetTwinAsync(
        ValidatedModelInspectionFixture fixture,
        DebugFixturePreset preset,
        bool animationsEnabled)
    {
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input,
            session,
            ModelInspectionFixtureGalleryPage.ShouldStartInspectionOnLoaded(
                fixture),
            preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await new ModelInspectionFixtureScenarioRunner().RunAsync(
                fixture.Input,
                host.ModelInspectionPage!,
                session,
                CancellationToken.None);
            ModelInspectionFixturePresetObservation observed =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            return new PresetRun(
                observed.Screen,
                observed.Geometry,
                session.Evidence.AnimationStartCount);
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    private static async Task<string[]> ObserveTextRoleEntriesAsync(
        ModelInspectionFixtureCatalogue catalogue,
        string fixtureId,
        string presetId)
    {
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == fixtureId);
        DebugFixturePreset preset = Preset(catalogue, presetId);
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: preset.Motion ==
                ModelInspectionFixtureMotionProfile.Normal,
            () => new ImmediateAnimationDriver());
        var host = new ModelInspectionFixtureHostPage();
        host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
            fixture.Input,
            session,
            ModelInspectionFixtureGalleryPage.ShouldStartInspectionOnLoaded(
                fixture),
            preset));
        var window = new Window { Content = host };
        window.Activate();
        try
        {
            await host.ApplyPresetAsync(CancellationToken.None);
            ModelInspectionPage page = host.ModelInspectionPage ??
                throw new AssertFailedException(
                    $"{fixtureId}/{presetId}: host page was not created.");
            string checkpoint = await new ModelInspectionFixtureScenarioRunner()
                .RunAsync(
                    fixture.Input,
                    page,
                    session,
                    CancellationToken.None);
            Assert.AreEqual(fixture.Input.ObservationCheckpoint, checkpoint,
                $"{fixtureId}/{presetId}");

            ModelInspectionFixturePresetObservation observed =
                await host.ObservePresetForTestingAsync(CancellationToken.None);
            return observed.TextRoles
                .Select(role => $"{role.Key}={role.Value}")
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            host.RetireForTesting();
            window.Content = null;
            window.Close();
        }
    }

    private static void AssertGeometryEqual(
        ModelInspectionObservedFixtureGeometry expected,
        ModelInspectionObservedFixtureGeometry actual,
        string message)
    {
        Assert.AreEqual(expected.HostWidth, actual.HostWidth, 1d, message);
        Assert.AreEqual(expected.HostHeight, actual.HostHeight, 1d, message);
        Assert.AreEqual(expected.ContentX, actual.ContentX, 1d, message);
        Assert.AreEqual(expected.ContentY, actual.ContentY, 1d, message);
        Assert.AreEqual(expected.ContentWidth, actual.ContentWidth, 1d, message);
        Assert.AreEqual(expected.ContentHeight, actual.ContentHeight, 1d, message);
        Assert.AreEqual(expected.HorizontalExtent, actual.HorizontalExtent, 1d,
            message);
        Assert.AreEqual(expected.VerticalExtent, actual.VerticalExtent, 1d,
            message);
    }

    private static void AssertObservedScreensEquivalent(
        ModelInspectionObservedScreen expected,
        ModelInspectionObservedScreen actual,
        string message)
    {
        Assert.AreEqual(expected.Figma, actual.Figma, message);
        Assert.AreEqual(expected.Outcome, actual.Outcome, message);
        Assert.AreEqual(expected.Model with
        {
            Metadata = Array.Empty<ModelInspectionObservedMetadataField>(),
            Checks = Array.Empty<ModelInspectionObservedCheckRow>()
        }, actual.Model with
        {
            Metadata = Array.Empty<ModelInspectionObservedMetadataField>(),
            Checks = Array.Empty<ModelInspectionObservedCheckRow>()
        }, message);
        CollectionAssert.AreEqual(expected.Model.Metadata.ToArray(),
            actual.Model.Metadata.ToArray(), message);
        CollectionAssert.AreEqual(expected.Model.Checks.ToArray(),
            actual.Model.Checks.ToArray(), message);
        Assert.AreEqual(expected.Content with
        {
            Rows = Array.Empty<ModelInspectionObservedContentRow>()
        }, actual.Content with
        {
            Rows = Array.Empty<ModelInspectionObservedContentRow>()
        }, message);
        CollectionAssert.AreEqual(expected.Content.Rows.ToArray(),
            actual.Content.Rows.ToArray(), message);
        Assert.AreEqual(expected.Actions with
        {
            Items = Array.Empty<ModelInspectionObservedAction>()
        }, actual.Actions with
        {
            Items = Array.Empty<ModelInspectionObservedAction>()
        }, message);
        CollectionAssert.AreEqual(expected.Actions.Items.ToArray(),
            actual.Actions.Items.ToArray(), message);
        Assert.AreEqual(expected.Footer, actual.Footer, message);
        Assert.AreEqual(expected.Focus, actual.Focus, message);
        CollectionAssert.AreEqual(expected.Automation.Controls.ToArray(),
            actual.Automation.Controls.ToArray(), message);
        Assert.AreEqual(expected.Announcements.Count,
            actual.Announcements.Count, message);
        CollectionAssert.AreEqual(expected.Announcements.Items.ToArray(),
            actual.Announcements.Items.ToArray(), message);
        CollectionAssert.AreEqual(expected.RowsAndScroll.OrderedRowIds.ToArray(),
            actual.RowsAndScroll.OrderedRowIds.ToArray(), message);
        Assert.AreEqual(expected.RowsAndScroll.ScrollOwner,
            actual.RowsAndScroll.ScrollOwner, message);
        CollectionAssert.AreEqual(expected.Retention.Ids.ToArray(),
            actual.Retention.Ids.ToArray(), message);
        Assert.AreEqual(expected.Retention with
        {
            Ids = Array.Empty<string>()
        }, actual.Retention with
        {
            Ids = Array.Empty<string>()
        }, message);
        Assert.AreEqual(expected.RenderBarrier, actual.RenderBarrier, message);
        CollectionAssert.AreEquivalent(
            expected.VisibleTextMutations.ToArray(),
            actual.VisibleTextMutations.ToArray(), message);
    }

    private sealed record PresetRun(
        ModelInspectionObservedScreen Screen,
        ModelInspectionObservedFixtureGeometry Geometry,
        int AnimationStartCount);

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task HighContrastPreview_UsesScopedSemanticBrushesAndHonestLabel()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        DebugFixturePreset preset = Preset(catalogue, "P09");
        var resources = new ResourceDictionary();
        ModelInspectionFixturePreviewResources.Configure(resources, preset);
        string[] semanticKeys =
        [
            "InspectionTextPrimaryBrush",
            "InspectionPrimaryBlueBrush",
            "InspectionSuccessTextBrush",
            "InspectionWarningTextBrush",
            "InspectionErrorTextBrush",
            "InspectionSurfaceBrush",
            "InspectionBorderStrongBrush"
        ];
        Assert.IsTrue(semanticKeys.All(key =>
            resources[key] is SolidColorBrush),
            "Preview brushes must be materialized in the local scope.");
        Assert.AreEqual("High Contrast Preview",
            ModelInspectionFixturePreviewResources.ResourceLabel(
                preset.Resources));
        Assert.AreEqual("200% Preview",
            ModelInspectionFixturePreviewResources.TextLabel(
                ModelInspectionFixtureTextProfile.Preview200));
        Assert.IsFalse(ModelInspectionFixturePreviewResources.ResourceLabel(
            preset.Resources).Contains("compliance",
                StringComparison.OrdinalIgnoreCase));
        await Task.CompletedTask;
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task WidthProfiles_UseExactRealHostAndBoundedHorizontalScrollWithoutViewbox()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        foreach ((string id, string presetId) in new[]
                 {
                     ("MI-043", "P02"),
                     ("MI-045", "P04"),
                     ("MI-003", "P09")
                 })
        {
            ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
                item => item.Id == id);
            DebugFixturePreset preset = Preset(catalogue, presetId);
            using var session = new ModelInspectionFixtureSession(
                fixture.Input,
                animationsEnabled: preset.Motion ==
                    ModelInspectionFixtureMotionProfile.Normal,
                () => new ImmediateAnimationDriver());
            var host = new ModelInspectionFixtureHostPage();
            host.ActivateForTesting(new ModelInspectionFixtureHostActivation(
                fixture.Input, session, true, preset));
            var window = new Window { Content = host };
            window.Activate();
            try
            {
                await new ModelInspectionFixtureScenarioRunner().RunAsync(
                    fixture.Input,
                    host.ModelInspectionPage!,
                    session,
                    CancellationToken.None);
                ModelInspectionFixturePresetObservation actual =
                    await host.ObservePresetForTestingAsync(
                        CancellationToken.None);
                var horizontal = (ScrollViewer)host.FindName(
                    "FixturePresetHorizontalScrollViewer");
                var vertical = (ScrollViewer)host.ModelInspectionPage!.FindName(
                    "InspectionPageScrollViewer");

                Assert.AreEqual(ExpectedWidth(preset.Width),
                    actual.Geometry.HostWidth, 1d, Pair(fixture, preset));
                Assert.AreEqual(ScrollMode.Enabled,
                    horizontal.HorizontalScrollMode);
                Assert.AreEqual(ScrollMode.Disabled,
                    horizontal.VerticalScrollMode);
                Assert.IsNotInstanceOfType<Viewbox>(horizontal.Content);
                Assert.AreEqual(ExpectedWidth(preset.Width),
                    horizontal.ExtentWidth, 1d, Pair(fixture, preset));
                Assert.IsGreaterThan(0d, horizontal.ViewportWidth,
                    Pair(fixture, preset));
                if (horizontal.ViewportWidth < horizontal.ExtentWidth - 1d)
                {
                    Assert.AreEqual(
                        horizontal.ExtentWidth - horizontal.ViewportWidth,
                        horizontal.ScrollableWidth,
                        1d,
                        Pair(fixture, preset));
                }
                var previewSurface = (Grid)host.FindName(
                    "FixturePreviewSurface");
                Assert.AreSame(previewSurface, horizontal.Content);
                Assert.AreEqual(ExpectedWidth(preset.Width),
                    previewSurface.Width, 1d, Pair(fixture, preset));
                var pageHost = (ContentControl)host.FindName("FixturePageHost");
                Assert.AreSame(host.ModelInspectionPage, pageHost.Content);
                Assert.AreEqual(ScrollMode.Enabled, vertical.VerticalScrollMode);
            }
            finally
            {
                host.RetireForTesting();
                window.Content = null;
                window.Close();
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task DesktopPreset_RemainsExactInsideNarrowRealXamlRootAcrossRepeatedBoundaries()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        DebugFixturePreset preset = Preset(catalogue, "P01");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input,
            animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: false,
            resources => ModelInspectionFixturePreviewResources.Configure(
                resources,
                preset));
        var applier = new ModelInspectionFixturePresetApplier(page, preset);
        var previewSurface = new Grid
        {
            Width = applier.HostWidth,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        previewSurface.Children.Add(page);
        var horizontalScroll = new ScrollViewer
        {
            Content = previewSurface,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ZoomMode = ZoomMode.Disabled
        };
        var window = new Window { Content = horizontalScroll };
        Task loaded = WaitForLoadedAsync(page);
        window.Activate();
        try
        {
            await loaded;
            await ResizeClientAndWaitForXamlRootAsync(
                window,
                horizontalScroll,
                width: 360d,
                height: 700d);
            Assert.IsLessThan(applier.HostWidth, page.XamlRoot.Size.Width,
                "The real XamlRoot must stay narrower than the fixed desktop preview.");
            Assert.AreEqual(3, applier.ActiveHandlerCount);

            await applier.ApplyAsync(CancellationToken.None);
            for (int boundary = 0; boundary < 4; boundary++)
            {
                await WaitForLoadedTreeBoundariesAsync(page);
                Assert.AreEqual(1440d, page.Width, 0.01,
                    $"boundary {boundary}");
                Assert.AreEqual("DesktopPageState",
                    page.FixturePageResponsiveStateName,
                    $"boundary {boundary}");
                Assert.AreEqual("WideModelState",
                    page.FixtureModelResponsiveStateName,
                    $"boundary {boundary}");
                Assert.AreEqual("WideContentState",
                    page.FixtureContentResponsiveStateName,
                    $"boundary {boundary}, active content");
                Assert.AreEqual("WideContentState",
                    page.FixtureOutgoingContentResponsiveStateName,
                    $"boundary {boundary}, outgoing content");
                Assert.AreEqual("WideActionState",
                    page.FixtureActionResponsiveStateName,
                    $"boundary {boundary}");
                Assert.AreEqual(3, applier.ActiveHandlerCount,
                    $"boundary {boundary}: handlers must not detach or multiply");
            }
        }
        finally
        {
            applier.Dispose();
            page.RetireForFixture();
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PresetApplier_DisposeDetachesEveryInstanceAndStaticHandlerIdempotently()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        var page = new ModelInspectionPage();
        var applier = new ModelInspectionFixturePresetApplier(
            page,
            Preset(catalogue, "P01"));

        Assert.AreEqual(3, applier.ActiveHandlerCount,
            "SizeChanged, LayoutUpdated, and static Rendering must be owned.");

        applier.Dispose();
        Assert.AreEqual(0, applier.ActiveHandlerCount);
        applier.Dispose();
        Assert.AreEqual(0, applier.ActiveHandlerCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PresetApplier_DisposeStopsBehavioralRepairAcrossRealBoundaries()
    {
        ModelInspectionFixtureCatalogue catalogue = LoadCatalogue();
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single(
            item => item.Id == "MI-003");
        DebugFixturePreset preset = Preset(catalogue, "P09");
        using var session = new ModelInspectionFixtureSession(
            fixture.Input, animationsEnabled: true,
            () => new ImmediateAnimationDriver());
        ModelInspectionPage page = ModelInspectionPage.CreateForFixture(
            session,
            startInspectionOnLoaded: true,
            resources => ModelInspectionFixturePreviewResources.Configure(
                resources, preset));
        var applier = new ModelInspectionFixturePresetApplier(page, preset);
        var window = new Window { Content = page };
        window.Activate();
        try
        {
            await applier.ApplyAsync(CancellationToken.None);
            applier.Dispose();
            page.ApplyFixtureResponsiveState(
                ModelInspectionFixtureWidthProfile.Desktop1440);
            page.Width = 1439;
            await WaitForLoadedTreeBoundariesAsync(page);

            Assert.AreEqual("DesktopPageState",
                page.FixturePageResponsiveStateName);
            Assert.AreEqual("WideModelState",
                page.FixtureModelResponsiveStateName);
            Assert.AreEqual(1439d, page.Width);
            Assert.AreEqual(0, applier.ActiveHandlerCount);
        }
        finally
        {
            applier.Dispose();
            page.RetireForFixture();
            window.Content = null;
            window.Close();
        }
    }

    private static async Task WaitForLoadedTreeBoundariesAsync(
        FrameworkElement element)
    {
        var layout = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void LayoutHandler(object? sender, object args) =>
            layout.TrySetResult(true);
        element.LayoutUpdated += LayoutHandler;
        try
        {
            element.InvalidateMeasure();
            element.UpdateLayout();
            await layout.Task;
        }
        finally
        {
            element.LayoutUpdated -= LayoutHandler;
        }

        var rendered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<object> rendering = (_, _) => rendered.TrySetResult(true);
        CompositionTarget.Rendering += rendering;
        try
        {
            await rendered.Task;
        }
        finally
        {
            CompositionTarget.Rendering -= rendering;
        }
    }

    private static async Task WaitForLoadedAsync(FrameworkElement element)
    {
        if (element.IsLoaded)
        {
            return;
        }

        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler handler = (_, _) => loaded.TrySetResult(true);
        element.Loaded += handler;
        try
        {
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            element.Loaded -= handler;
        }
    }

    private static async Task ResizeClientAndWaitForXamlRootAsync(
        Window window,
        FrameworkElement root,
        double width,
        double height)
    {
        var resized = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(object? sender, object args)
        {
            if (Math.Abs(root.XamlRoot.Size.Width - width) <= 1d &&
                Math.Abs(root.XamlRoot.Size.Height - height) <= 1d)
            {
                resized.TrySetResult(true);
            }
        }

        root.LayoutUpdated += Observe;
        try
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            double requestedWidth = width;
            double requestedHeight = height;
            do
            {
                double scale = root.XamlRoot.RasterizationScale;
                window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                    (int)Math.Round(requestedWidth * scale),
                    (int)Math.Round(requestedHeight * scale)));
                Observe(null, EventArgs.Empty);
                if (resized.Task.IsCompleted)
                {
                    root.UpdateLayout();
                    return;
                }

                Task completed = await Task.WhenAny(
                    resized.Task,
                    Task.Delay(TimeSpan.FromMilliseconds(250)));
                if (ReferenceEquals(completed, resized.Task))
                {
                    root.UpdateLayout();
                    return;
                }

                requestedWidth = Math.Max(
                    1d,
                    requestedWidth + width - root.XamlRoot.Size.Width);
                requestedHeight = Math.Max(
                    1d,
                    requestedHeight + height - root.XamlRoot.Size.Height);
            }
            while (DateTime.UtcNow < deadline);

            throw new TimeoutException(
                $"XamlRoot did not reach {width:F0}x{height:F0}; " +
                $"actual={root.XamlRoot.Size.Width:F2}x" +
                $"{root.XamlRoot.Size.Height:F2}, " +
                $"scale={root.XamlRoot.RasterizationScale:F2}.");
        }
        finally
        {
            root.LayoutUpdated -= Observe;
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
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

            foreach (T nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static string BaseTextBlockClippingFailures(
        ModelInspectionPage page)
    {
        string[] failures = Descendants<TextBlock>(page)
            .Where(text => text.IsLoaded &&
                IsEffectivelyVisibleForClippingDiagnostic(text) &&
                !string.IsNullOrEmpty(text.Text))
            .Where(FailsBaseTextBlockClippingCriterion)
            .Take(5)
            .Select(DescribeTextBlockForClipping)
            .ToArray();

        return failures.Length == 0
            ? "BaseTextBlockFailures=<none>"
            : $"BaseTextBlockFailures=[{string.Join(" | ", failures)}]";
    }

    private static bool IsEffectivelyVisibleForClippingDiagnostic(
        FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is FrameworkElement owner &&
                (owner.Visibility != Visibility.Visible ||
                 !(owner.Opacity > 0d) ||
                 !(owner.ActualWidth > 0d) ||
                 !(owner.ActualHeight > 0d)))
            {
                return false;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return true;
    }

    private static bool FailsBaseTextBlockClippingCriterion(
        TextBlock text) =>
        !double.IsFinite(text.ActualWidth) ||
        text.ActualWidth <= 0d ||
        !double.IsFinite(text.ActualHeight) ||
        text.ActualHeight <= 0d ||
        text.ActualHeight + 1d < text.FontSize;

    private static string DescribeTextBlockForClipping(TextBlock text) =>
        FormattableString.Invariant(
            $"Name=\"{TruncateDiagnosticText(text.Name, 32)}\" Text=\"{TruncateDiagnosticText(text.Text, 80)}\" Actual={text.ActualWidth:G17}x{text.ActualHeight:G17} Desired={text.DesiredSize.Width:G17}x{text.DesiredSize.Height:G17} Margin=({text.Margin.Left:G17},{text.Margin.Top:G17},{text.Margin.Right:G17},{text.Margin.Bottom:G17}) FontSize={text.FontSize:G17}");

    private static string TruncateDiagnosticText(
        string? value,
        int maximumTextElements)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<empty>";
        }

        string singleLine = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Replace("\"", "'", StringComparison.Ordinal);
        var textInfo = new System.Globalization.StringInfo(singleLine);
        return textInfo.LengthInTextElements <= maximumTextElements
            ? singleLine
            : textInfo.SubstringByTextElements(0, maximumTextElements) + "…";
    }

    private static T Find<T>(DependencyObject root, Predicate<T> predicate)
        where T : DependencyObject
    {
        if (root is T match && predicate(match))
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

    private static T Ancestor<T>(DependencyObject element)
        where T : DependencyObject
    {
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        throw new InvalidOperationException(
            $"No {typeof(T).Name} ancestor was found.");
    }

    private static bool HasAncestor<T>(DependencyObject element)
        where T : DependencyObject
    {
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static string Pair(
        ValidatedModelInspectionFixture fixture,
        DebugFixturePreset preset) => $"{fixture.Id}/{preset.Id}";

    private static string CollectionDiagnostic(
        string pair,
        string property,
        IEnumerable<string> expected,
        IEnumerable<string> observed) =>
        $"{pair}: {property} mismatch. " +
        $"Expected=[{string.Join(", ", expected)}]; " +
        $"Observed=[{string.Join(", ", observed)}].";

    private static (string Key, double StandardValue)[] Typography() =>
    [
        ("InspectionPageTitleFontSize", 32d),
        ("InspectionSectionTitleFontSize", 18d),
        ("InspectionBodyFontSize", 14d),
        ("InspectionHelperFontSize", 12d),
        ("InspectionLabelFontSize", 10d)
    ];

    private static string[] SemanticBrushKeys() =>
    [
        "InspectionTextPrimaryBrush",
        "InspectionPrimaryBlueBrush",
        "InspectionSuccessTextBrush",
        "InspectionWarningTextBrush",
        "InspectionErrorTextBrush",
        "InspectionSurfaceBrush",
        "InspectionBorderStrongBrush"
    ];

    private static void AssertScreenMatchesFixture(
        ModelInspectionFixtureCatalogue catalogue,
        ValidatedModelInspectionFixture fixture,
        ModelInspectionObservedScreen screen,
        string message)
    {
        IReadOnlyList<ModelInspectionFixtureScreenDifference> differences =
            new ModelInspectionFixtureScreenComparer().Compare(
                fixture.FileName,
                fixture.Expected,
                screen,
                catalogue.Policy.Value.CopyRegistry);
        Assert.AreEqual(0, differences.Count,
            $"{message}: {string.Join(Environment.NewLine, differences.Select(
                difference => difference.Diagnostic))}");
    }

    private static void AssertLoadedResourceProfile(
        ModelInspectionPage page,
        DebugFixturePreset preset,
        ValidatedModelInspectionFixture fixture,
        string message)
    {
        ElementTheme expectedTheme = preset.Resources ==
            ModelInspectionFixtureResourceProfile.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        Assert.AreEqual(expectedTheme, page.RequestedTheme, message);

        bool highContrast = preset.Resources ==
            ModelInspectionFixtureResourceProfile.HighContrastPreview;
        foreach (string key in SemanticBrushKeys())
        {
            Assert.AreEqual(highContrast, page.Resources.ContainsKey(key),
                $"{message}/{key}/scope");
            if (highContrast)
            {
                object local = page.Resources[key];
                object source = ThemeBrush("HighContrast", key);
                Assert.IsInstanceOfType<SolidColorBrush>(local);
                Assert.AreNotSame(source, local,
                    $"{message}/{key} must be freshly materialized.");
            }
        }

        string themeKey = highContrast ? "HighContrast" :
            expectedTheme == ElementTheme.Dark ? "Dark" : "Light";
        SolidColorBrush title = Assert.IsInstanceOfType<SolidColorBrush>(
            ((TextBlock)page.FindName("PageTitle")).Foreground);
        SolidColorBrush expectedTitle = Assert.IsInstanceOfType<SolidColorBrush>(
            highContrast
                ? page.Resources["InspectionTextPrimaryBrush"]
                : ThemeBrush(themeKey, "InspectionTextPrimaryBrush"));
        Assert.AreEqual(expectedTitle.Color, title.Color,
            $"{message}/PageTitle.Foreground");

        if (!fixture.Expected.Outcome.Visible)
        {
            return;
        }

        string statusKey = fixture.Expected.Outcome.Tone switch
        {
            ModelInspectionExpectedOutcomeTone.Success =>
                "InspectionSuccessTextStrongBrush",
            ModelInspectionExpectedOutcomeTone.Warning =>
                "InspectionWarningTextStrongBrush",
            ModelInspectionExpectedOutcomeTone.Error =>
                "InspectionErrorTextStrongBrush",
            _ => "InspectionTextPrimaryBrush"
        };
        var outcome = (InspectionOutcomeCard)page.FindName(
            "InspectionOutcomeCardControl");
        SolidColorBrush status = Assert.IsInstanceOfType<SolidColorBrush>(
            ((TextBlock)outcome.FindName("OutcomeTitle")).Foreground);
        SolidColorBrush expectedStatus = Assert.IsInstanceOfType<SolidColorBrush>(
            highContrast ? page.Resources[statusKey] :
                ThemeBrush(themeKey, statusKey));
        Assert.AreEqual(expectedStatus.Color, status.Color,
            $"{message}/OutcomeTitle.Foreground");
    }

    private static object ThemeBrush(string theme, string key)
    {
        foreach (ResourceDictionary dictionary in
                 Application.Current.Resources.MergedDictionaries)
        {
            if (dictionary.ThemeDictionaries.ContainsKey(theme) &&
                dictionary.ThemeDictionaries[theme] is
                    ResourceDictionary themeDictionary &&
                themeDictionary.ContainsKey(key))
            {
                return themeDictionary[key];
            }
        }

        throw new AssertFailedException(
            $"Theme resource '{theme}/{key}' was not found.");
    }

    private static string ExpectedPageState(
        ModelInspectionFixtureWidthProfile width) => width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => "DesktopPageState",
            ModelInspectionFixtureWidthProfile.Medium600 => "MediumPageState",
            ModelInspectionFixtureWidthProfile.Narrow360 => "NarrowPageState",
            _ => throw new AssertFailedException($"Unknown width {width}.")
        };

    private static string ExpectedCardState(
        ModelInspectionFixtureWidthProfile width,
        string suffix) => width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => $"Wide{suffix}State",
            ModelInspectionFixtureWidthProfile.Medium600 => $"Medium{suffix}State",
            ModelInspectionFixtureWidthProfile.Narrow360 => $"Narrow{suffix}State",
            _ => throw new AssertFailedException($"Unknown width {width}.")
        };

    private static DebugFixturePreset Preset(
        ModelInspectionFixtureCatalogue catalogue,
        string id) => DebugFixturePreset.FromPolicy(
            catalogue.Policy.Value.Presets.Single(preset => preset.Id == id));

    private static string[] VisiblePresetLabels(
        ModelInspectionFixtureGalleryPage gallery) =>
        ((StackPanel)gallery.FindName("FixturePresetControls"))
            .Children.OfType<TextBlock>()
            .Select(text => text.Text)
            .ToArray();

    private static double ExpectedWidth(
        ModelInspectionFixtureWidthProfile width) => width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => 1440d,
            ModelInspectionFixtureWidthProfile.Medium600 => 600d,
            ModelInspectionFixtureWidthProfile.Narrow360 => 360d,
            _ => throw new AssertFailedException($"Unknown width {width}.")
        };

    private static ModelInspectionFixtureCatalogue LoadCatalogue()
    {
        string root = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures");
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

    private sealed class ImmediateAnimationDriver :
        GraniteEdgeAI.Features.ModelInspection.Presentation.IModelInspectionAnimationDriver
    {
        public void StartStageStatus(UIElement target,
            GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey key,
            Action<GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey> completed) => completed(key);
        public void StartActiveDetail(UIElement target,
            GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey key,
            Action<GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey> completed) => completed(key);
        public void StartDisclosure(UIElement chevron, FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements, bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey key,
            Action<GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey> completed) => completed(key);
        public void StartTerminal(UIElement outgoing, UIElement incoming,
            GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey key,
            Action<GraniteEdgeAI.Features.ModelInspection.Presentation.ModelInspectionVisualOperationKey> completed) => completed(key);
        public void CancelAll() { }
        public void Dispose() { }
    }
}
#endif
