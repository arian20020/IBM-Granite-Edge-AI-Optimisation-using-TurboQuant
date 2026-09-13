using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Linq;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies that required Model Inspection visual states fail clearly when XAML and
/// code-behind drift apart.
/// </summary>
[TestClass]
public sealed class InspectionVisualStateGuardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void ResultPresentation_MissingResultState_ThrowsClearException()
    {
        InspectionActionCard control = new();
        FrameworkElement layoutRoot =
            (FrameworkElement)control.FindName("LayoutRoot");
        RemoveVisualState(layoutRoot, "CardModeStates", "ResultState");

        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                control.Presentation = new InspectionActionCardPresentation
                {
                    Mode = InspectionActionCardMode.Result
                });

        StringAssert.Contains(exception.Message, "ResultState");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DetailedPresentation_MissingDetailedState_ThrowsClearException()
    {
        InspectionModelCard control = new();
        FrameworkElement layoutRoot =
            (FrameworkElement)control.FindName("LayoutRoot");
        RemoveVisualState(layoutRoot, "DisplayModeStates", "DetailedState");

        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                control.Presentation = new InspectionModelCardPresentation
                {
                    DisplayMode = InspectionModelCardMode.Detailed
                });

        StringAssert.Contains(exception.Message, "DetailedState");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ExpandedReadyPresentation_MissingExpandedDetailsState_ThrowsClearException()
    {
        InspectionModelCard control = new();
        FrameworkElement layoutRoot =
            (FrameworkElement)control.FindName("LayoutRoot");
        RemoveVisualState(
            layoutRoot,
            "InspectionDetailsStates",
            "ExpandedDetailsState");

        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                control.Presentation = new InspectionModelCardPresentation
                {
                    DisplayMode = InspectionModelCardMode.Detailed,
                    InspectionDetailsVisibility = Visibility.Visible,
                    IsInspectionDetailsExpanded = true
                });

        StringAssert.Contains(exception.Message, "ExpandedDetailsState");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ReadyPresentation_MissingSuccessTone_ThrowsClearException()
    {
        InspectionOutcomeCard control = new();
        FrameworkElement layoutRoot =
            (FrameworkElement)control.FindName("LayoutRoot");
        RemoveVisualState(layoutRoot, "OutcomeToneStates", "SuccessTone");

        InvalidOperationException exception =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                control.Presentation = new InspectionOutcomePresentation
                {
                    Kind = InspectionOutcomePresentationKind.Ready,
                    Tone = InspectionOutcomeTone.Success,
                    Title = "Ready",
                    Message = "Ready",
                    AutomationName = "Ready"
                });

        StringAssert.Contains(exception.Message, "SuccessTone");
    }

    [TestMethod]
    public void ProgressFractionHelpers_PreserveMeasuredAndIndeterminateStates()
    {
        const BindingFlags PublicStatic =
            BindingFlags.Public | BindingFlags.Static;
        Assert.IsNull(typeof(InspectionContentCard).GetMethod(
            "IsProgressIndeterminate",
            PublicStatic));
        Assert.IsNull(typeof(InspectionContentCard).GetMethod(
            "GetProgressPercent",
            PublicStatic));

        var item = new InspectionContentItemPresentation();
        (double? Fraction, string Text)[] cases =
        [
            (null, string.Empty),
            (0.25d, "25%"),
            (0.625d, "63%"),
            (0.75d, "75%"),
            (1d, "100%")
        ];
        foreach ((double? fraction, string expected) in cases)
        {
            item.StageFraction = fraction;
            Assert.AreEqual(expected, item.StageFractionText);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PresentationChanges_AreSilentUntilExplicitCurrentAnnouncement()
    {
        var content = new InspectionContentCard();
        AutomationProperties.SetLiveSetting(
            content,
            AutomationLiveSetting.Polite);
        var progressRows = new InspectionProgressRows();
        progressRows.Apply(new InspectionProgressRowsUpdate(
            new ModelInspectionProgressRegionKey(
                ModelInspectionStage.CheckModelPackage,
                ModelInspectionStageStatus.Active,
                completedStageCount: 0,
                stageCount: 5,
                stageFraction: 0.25,
                detail: "Inspection progress updated."),
            new ModelInspectionRenderKey(0, 1),
            "0 of 5 checks complete"));
        content.Presentation =
            InitialInspectionProgressPresentationFactory.Create(progressRows);

        Assert.IsNotNull(
            FrameworkElementAutomationPeer.FromElement(content));
        StringAssert.Contains(
            AutomationProperties.GetName(content),
            "Inspection progress");
        StringAssert.Contains(
            AutomationProperties.GetName(content),
            "Check model package");
        Assert.AreEqual(0, content.LiveRegionChangeNotificationCount);

        content.AnnounceProgress(
            "Inspection progress. 0 of 5 checks complete. " +
            "Check model package. Checking.");

        Assert.AreEqual(1, content.LiveRegionChangeNotificationCount);

        var outcome = new InspectionOutcomeCard();
        AutomationProperties.SetLiveSetting(
            outcome,
            AutomationLiveSetting.Assertive);
        var ready = new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.Ready,
            Tone = InspectionOutcomeTone.Success,
            Title = "Ready",
            Message = "The model passed inspection.",
            AutomationName = "Ready. The model passed inspection."
        };
        outcome.Presentation = ready;

        Assert.IsNotNull(
            FrameworkElementAutomationPeer.FromElement(outcome));
        Assert.AreEqual(
            "Ready. The model passed inspection.",
            AutomationProperties.GetName(outcome));
        Assert.AreEqual(0, outcome.LiveRegionChangeNotificationCount);

        outcome.AnnounceOutcome("Ready. The model passed inspection.");

        Assert.AreEqual(1, outcome.LiveRegionChangeNotificationCount);

        outcome.Presentation = InspectionOutcomePresentation.Hidden;
        outcome.Presentation = ready;

        Assert.AreEqual(
            1,
            outcome.LiveRegionChangeNotificationCount,
            "Assignment and hide/show churn must not announce by itself.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("C:\\private-model-directory\\granite.gguf")]
    [DataRow("/home/private/granite.gguf")]
    [DataRow("unsafe\u0001control")]
    [DataRow("unsafe\u202Eoverride")]
    public void ExplicitAnnouncementHooks_RejectUnsafeText(string unsafeText)
    {
        var content = new InspectionContentCard();
        var outcome = new InspectionOutcomeCard();

        Assert.ThrowsExactly<ArgumentException>(() =>
            content.AnnounceProgress(unsafeText));
        Assert.ThrowsExactly<ArgumentException>(() =>
            outcome.AnnounceOutcome(unsafeText));
        Assert.AreEqual(0, content.LiveRegionChangeNotificationCount);
        Assert.AreEqual(0, outcome.LiveRegionChangeNotificationCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ExplicitAnnouncementHooks_RejectMoreThan512CodeUnits()
    {
        string oversize = new('A', 513);
        var content = new InspectionContentCard();
        var outcome = new InspectionOutcomeCard();

        Assert.ThrowsExactly<ArgumentException>(() =>
            content.AnnounceProgress(oversize));
        Assert.ThrowsExactly<ArgumentException>(() =>
            outcome.AnnounceOutcome(oversize));
    }

    private static void RemoveVisualState(
        FrameworkElement layoutRoot,
        string groupName,
        string stateName)
    {
        VisualStateGroup group = VisualStateManager
            .GetVisualStateGroups(layoutRoot)
            .Single(candidate => candidate.Name == groupName);
        VisualState state = group.States
            .Single(candidate => candidate.Name == stateName);

        group.States.Remove(state);
    }
}
