using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Protects the user-visible boundary between core model inspection and later
/// hardware/backend verification.
/// </summary>
[TestClass]
public sealed class InitialInspectionProgressPresentationTests
{
    /// <summary>
    /// Verifies the exact five rows and their approved order.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_ReturnsFiveApprovedCoreInspectionStages()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        string[] actualTitles = presentation.Items
            .Select(item => item.Title)
            .ToArray();

        string[] expectedTitles =
        [
            "Check model package",
            "Read model configuration",
            "Validate tokenizer and chat setup",
            "Validate model structure",
            "Confirm core runtime compatibility"
        ];

        CollectionAssert.AreEqual(
            expectedTitles,
            actualTitles);
    }

    /// <summary>
    /// Verifies the initial active/waiting state used before the real service is
    /// connected.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_ActivatesOnlyTheFirstStage()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        Assert.AreEqual(
            1,
            presentation.Items.Count(item => item.IsActive));

        InspectionContentItemPresentation firstStage =
            presentation.Items[0];

        Assert.IsTrue(firstStage.IsActive);
        Assert.AreEqual(
            InspectionContentStatus.Active,
            firstStage.Status);
        Assert.AreEqual(
            "Checking",
            firstStage.StatusText);

        foreach (InspectionContentItemPresentation waitingStage in
                 presentation.Items.Skip(1))
        {
            Assert.IsFalse(waitingStage.IsActive);
            Assert.AreEqual(
                InspectionContentStatus.Waiting,
                waitingStage.Status);
            Assert.AreEqual(
                "Waiting",
                waitingStage.StatusText);
        }
    }

    /// <summary>
    /// Verifies the tracker geometry and initial completed-stage count.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_UsesFourConnectorsAndZeroCompletedChecks()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        Assert.AreEqual(
            "0 of 5 checks complete",
            presentation.ProgressSummary);
        Assert.IsTrue(
            presentation.Items.Take(4).All(item => item.ShowConnector));
        Assert.IsFalse(
            presentation.Items[4].ShowConnector);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_ShowsDetailOnlyForTheActiveStage()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        Assert.AreEqual(
            Visibility.Visible,
            presentation.Items[0].DetailVisibility);

        foreach (InspectionContentItemPresentation waitingStage in
                 presentation.Items.Skip(1))
        {
            Assert.AreEqual(
                Visibility.Collapsed,
                waitingStage.DetailVisibility);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_UsesTitleAndStatusForStageAutomationNames()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        foreach (InspectionContentItemPresentation item in presentation.Items)
        {
            Assert.AreEqual(
                $"{item.Title}. {item.StatusText}.",
                item.AutomationName);
        }
    }

    /// <summary>
    /// Prevents backend- and hardware-specific validation from leaking into the
    /// pre-Hardware-Fit Model Inspection tracker.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_DoesNotPresentBackendVerificationAsModelInspection()
    {
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create();

        string[] forbiddenTerms =
        [
            "vulkan",
            "turboquant",
            "gpu",
            "hardware fit"
        ];

        foreach (InspectionContentItemPresentation item in presentation.Items)
        {
            string normalisedTitle = item.Title.ToLowerInvariant();

            foreach (string forbiddenTerm in forbiddenTerms)
            {
                Assert.IsFalse(
                    normalisedTitle.Contains(
                        forbiddenTerm,
                        StringComparison.Ordinal),
                    $"Stage '{item.Title}' must not include backend-specific " +
                    $"term '{forbiddenTerm}'.");
            }
        }
    }
}
