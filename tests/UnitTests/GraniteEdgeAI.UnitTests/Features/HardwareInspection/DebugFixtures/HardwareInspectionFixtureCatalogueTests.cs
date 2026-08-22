#if HARDWARE_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.HardwareInspection.DebugFixtures;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Linq;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.DebugFixtures;

[TestClass]
public sealed class HardwareInspectionFixtureCatalogueTests
{
    [TestMethod]
    public void Create_CoversEveryPresentationKindAndActiveStage()
    {
        HardwareInspectionFixtureScenario[] scenarios =
            [.. HardwareInspectionFixtureCatalogue.Create()];

        CollectionAssert.AreEquivalent(
            Enum.GetValues<HardwareInspectionPresentationKind>(),
            scenarios.Select(item => item.Presentation.Kind).Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<HardwareInspectionStage>(),
            scenarios
                .Where(item => item.ActiveStage is not null)
                .Select(item => item.ActiveStage!.Value)
                .ToArray());
    }

    [TestMethod]
    public void Create_HasUniqueStableIdsAndCompleteRenderablePayloads()
    {
        HardwareInspectionFixtureScenario[] scenarios =
            [.. HardwareInspectionFixtureCatalogue.Create()];

        Assert.AreEqual(15, scenarios.Length);
        Assert.AreEqual(
            scenarios.Length,
            scenarios.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(scenarios.All(item => !string.IsNullOrWhiteSpace(item.Title)));
        Assert.IsTrue(scenarios.All(item =>
            !item.Presentation.DetailsAvailable || item.Details is not null));
        Assert.IsTrue(scenarios
            .Where(item => item.Presentation.Kind is
                HardwareInspectionPresentationKind.Completed or
                HardwareInspectionPresentationKind.CompletedWithWarnings)
            .All(item => item.Summary is not null));
    }

    [TestMethod]
    public void Create_NeverEnablesCompatibilityContinue()
    {
        var continueActions = HardwareInspectionFixtureCatalogue.Create()
            .SelectMany(item => item.Presentation.Actions)
            .Where(action => action.Kind ==
                HardwareInspectionActionKind.ContinueToCompatibility)
            .ToArray();

        Assert.AreEqual(2, continueActions.Length);
        Assert.IsTrue(continueActions.All(action => !action.IsEnabled));
        Assert.IsTrue(continueActions.All(action =>
            !string.IsNullOrWhiteSpace(action.AccessibleHelp)));
    }

    [UITestMethod]
    public void Gallery_SelectScenarioRendersExactFixtureWithoutStartingInspection()
    {
        HardwareInspectionFixtureGalleryPage gallery = new();

        Assert.AreEqual(15, gallery.Scenarios.Count);
        Assert.IsNotNull(gallery.FindName("ScenarioList"));
        Assert.IsNotNull(gallery.FindName("FixturePreview"));
        Assert.AreEqual(
            "Synthetic fixture",
            ((TextBlock)gallery.FindName("SyntheticFixtureBadge")).Text);

        gallery.SelectScenarioForTesting("HI-COMPLETED");

        Assert.AreEqual("HI-COMPLETED", gallery.SelectedScenario.Id);
        Assert.AreEqual(
            HardwareInspectionPresentationKind.Completed,
            gallery.PreviewPage.CurrentState!.Kind);
        Assert.IsTrue(gallery.PreviewPage.CurrentState.Actions
            .Single(action => action.Kind ==
                HardwareInspectionActionKind.ContinueToCompatibility)
            .IsEnabled is false);
    }

    [UITestMethod]
    public void OnboardingEntry_OpensAndClosesHardwareFixtureGallery()
    {
        OnboardingShellPage shell = new();
        Frame frame = (Frame)shell.FindName("StageFrame");

        Assert.IsNotNull(shell.FindName("HardwareFixtureGalleryButton"));
        Assert.IsTrue(shell.NavigateToHardwareFixtureGalleryForTesting());
        HardwareInspectionFixtureGalleryPage gallery =
            Assert.IsInstanceOfType<HardwareInspectionFixtureGalleryPage>(frame.Content);

        gallery.CloseForTesting();

        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }
}
#endif
