using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Linq;

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
        Assert.IsTrue(InspectionContentCard.IsProgressIndeterminate(null));
        Assert.AreEqual(0d, InspectionContentCard.GetProgressPercent(null));

        Assert.IsFalse(InspectionContentCard.IsProgressIndeterminate(0.625d));
        Assert.AreEqual(
            62.5d,
            InspectionContentCard.GetProgressPercent(0.625d));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PresentationChanges_CreateMeaningfulLiveRegionPeers()
    {
        var content = new InspectionContentCard();
        AutomationProperties.SetLiveSetting(
            content,
            AutomationLiveSetting.Polite);
        content.Presentation =
            InitialInspectionProgressPresentationFactory.Create();

        Assert.IsNotNull(
            FrameworkElementAutomationPeer.FromElement(content));
        StringAssert.Contains(
            AutomationProperties.GetName(content),
            "Inspection progress");
        StringAssert.Contains(
            AutomationProperties.GetName(content),
            "Check model package");
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
        Assert.AreEqual(1, outcome.LiveRegionChangeNotificationCount);

        outcome.Presentation = InspectionOutcomePresentation.Hidden;
        outcome.Presentation = ready;

        Assert.AreEqual(2, outcome.LiveRegionChangeNotificationCount);
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
