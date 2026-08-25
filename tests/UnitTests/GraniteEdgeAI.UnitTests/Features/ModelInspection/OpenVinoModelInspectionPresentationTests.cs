using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
public sealed class OpenVinoModelInspectionPresentationTests
{
    [TestMethod]
    public void RouteProgressMapsToTheSameFiveModelInspectionStages()
    {
        (OpenVinoRouteInspectionStage route, ModelInspectionStage shared)[] cases =
        [
            (OpenVinoRouteInspectionStage.CheckModelPackage,
                ModelInspectionStage.CheckModelPackage),
            (OpenVinoRouteInspectionStage.ReadModelConfiguration,
                ModelInspectionStage.ReadModelConfiguration),
            (OpenVinoRouteInspectionStage.ValidateTokenizerAndChatSetup,
                ModelInspectionStage.ValidateTokenizerAndChatSetup),
            (OpenVinoRouteInspectionStage.ValidateModelStructure,
                ModelInspectionStage.ValidateModelStructure),
            (OpenVinoRouteInspectionStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility)
        ];

        foreach ((OpenVinoRouteInspectionStage route, ModelInspectionStage shared) in cases)
        {
            ModelInspectionProgress mapped =
                OpenVinoModelInspectionPresentationFactory.CreateProgress(
                    new OpenVinoRouteInspectionProgress(
                        route,
                        OpenVinoRouteInspectionStageStatus.Active,
                        0.4));

            Assert.AreEqual(shared, mapped.Stage);
            Assert.AreEqual(ModelInspectionStageStatus.Active, mapped.StageStatus);
            Assert.AreEqual(0.4, mapped.StageFraction);
            Assert.AreEqual(5, mapped.TotalStageCount);
        }
    }

    [TestMethod]
    public void ReadyPresentationUsesTheSharedGgufTerminalScreenStructure()
    {
        OpenVinoReadyPresentation ready =
            OpenVinoModelInspectionPresentationFactory.CreateReady(
                "Granite OpenVINO",
                Evidence(),
                hasWarnings: false,
                isInspectionDetailsExpanded: false,
                chooseAnother: new DelegateCommand(_ => { }),
                checkHardware: new DelegateCommand(_ => { }));

        Assert.AreEqual(InspectionModelCardMode.Detailed, ready.Model.DisplayMode);
        Assert.AreEqual(5, ready.Model.InspectionChecks.Count);
        Assert.AreEqual(InspectionContentCardMode.Hidden, ready.Content.Mode);
        Assert.AreEqual("Model is ready", ready.Actions.Title);
        Assert.AreEqual(
            "Choose another model or review future next steps.",
            ready.Actions.Message);
        Assert.AreEqual("Choose another model", ready.Actions.SecondaryActionOne.Text);
        Assert.AreEqual("View technical report", ready.Actions.SecondaryActionTwo.Text);
        Assert.AreEqual("Check hardware fit", ready.Actions.PrimaryAction.Text);
    }

    [TestMethod]
    public void ReadyPresentationPreservesRequestedInspectionDetailsState()
    {
        OpenVinoReadyPresentation ready =
            OpenVinoModelInspectionPresentationFactory.CreateReady(
                "Granite OpenVINO",
                Evidence(),
                hasWarnings: false,
                isInspectionDetailsExpanded: true,
                chooseAnother: new DelegateCommand(_ => { }),
                checkHardware: new DelegateCommand(_ => { }));

        Assert.IsTrue(ready.Model.IsInspectionDetailsExpanded);
        Assert.AreEqual(Visibility.Visible, ready.Model.InspectionDetailsVisibility);
    }

    [TestMethod]
    public void WarningPresentationUsesTheSharedGgufWarningStructure()
    {
        OpenVinoReadyPresentation warning =
            OpenVinoModelInspectionPresentationFactory.CreateReady(
                "Granite OpenVINO",
                Evidence(),
                hasWarnings: true,
                isInspectionDetailsExpanded: true,
                chooseAnother: new DelegateCommand(_ => { }),
                checkHardware: new DelegateCommand(_ => { }));

        Assert.AreEqual(InspectionModelCardMode.Compact, warning.Model.DisplayMode);
        Assert.AreEqual(Visibility.Collapsed, warning.Model.InspectionDetailsVisibility);
        Assert.AreEqual(0, warning.Model.InspectionChecks.Count);
        Assert.AreEqual(InspectionContentCardMode.Warnings, warning.Content.Mode);
        Assert.AreEqual(Visibility.Visible, warning.Content.DisclosureVisibility);
        Assert.IsTrue(warning.Content.IsExpanded);
        Assert.AreEqual("View full details", warning.Content.CollapsedDisclosureText);
        Assert.AreEqual("Hide full details", warning.Content.ExpandedDisclosureText);
        Assert.AreEqual("continue-hardware", warning.Actions.PrimaryAction.ActionId);
        Assert.AreEqual(
            "Continue to hardware check",
            warning.Actions.PrimaryAction.Text);
        Assert.AreEqual(
            "Continue to model hardware check",
            warning.Actions.PrimaryAction.AutomationName);
    }

    private static OpenVinoStaticPackageEvidence Evidence() => new(
        1,
        new string('a', 64),
        new string('b', 64),
        6_442_450_944,
        "granite",
        "GraniteForCausalLM",
        "text-generation-with-past",
        131_072,
        "FP16",
        "GPT2Tokenizer",
        40,
        2_560,
        32,
        8,
        12,
        true);
}
