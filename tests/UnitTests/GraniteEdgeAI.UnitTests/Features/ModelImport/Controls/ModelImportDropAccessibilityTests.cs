using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportDropAccessibilityTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void AwaitingSelection_UsesVisibleRoundedDashedDropBorder()
    {
        var card = new ImportModelCard();
        var border = card.FindName("AwaitingSelectionBorder") as Rectangle;

        Assert.IsNotNull(border);
        Assert.IsTrue(border.StrokeDashArray.Count > 0);
        Assert.IsTrue(border.RadiusX > 0);
        Assert.IsTrue(border.RadiusY > 0);
        Assert.AreEqual(Visibility.Visible, border.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ValidDrag_UsesDropNowPresentationAndTargetIcon()
    {
        var card = new ImportModelCard();

        card.ShowDragValidation(isValid: true);

        Assert.AreEqual(ImportModelCardState.DragOverValid, card.CurrentState);
        Assert.AreEqual("Drop model now", GetDropStatus(card).Text);
        Assert.AreEqual(Visibility.Visible, GetDropIcon(card).Visibility);
        Assert.AreEqual("Valid drop target", AutomationProperties.GetName(GetDropIcon(card)));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InvalidDrag_UsesCorrectivePresentation()
    {
        var card = new ImportModelCard();

        card.ShowDragValidation(isValid: false);

        Assert.AreEqual(ImportModelCardState.DragOverInvalid, card.CurrentState);
        Assert.AreEqual(
            "Drop exactly one supported model file or folder.",
            GetDropStatus(card).Text);
        Assert.AreEqual("Invalid drop target", AutomationProperties.GetName(GetDropIcon(card)));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DragLeave_RestoresAwaitingSelectionPresentation()
    {
        var card = new ImportModelCard();
        card.ShowDragValidation(isValid: true);

        card.ClearDragValidation();

        Assert.AreEqual(ImportModelCardState.AwaitingSelection, card.CurrentState);
        Assert.AreEqual(0d, GetDropStatusPanel(card).Opacity);
        Assert.AreEqual(
            Visibility.Visible,
            ((FrameworkElement)card.FindName("AwaitingSelectionBorder")).Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DropSurface_IsHitTestableAndDescribesPickerParity()
    {
        var page = new ModelImportPage();
        var target = page.FindName("ModelDropTarget") as Grid;

        Assert.IsNotNull(target);
        Assert.IsTrue(target.AllowDrop);
        Assert.IsNotNull(target.Background);
        Assert.AreEqual("Model drop area", AutomationProperties.GetName(target));
        StringAssert.Contains(AutomationProperties.GetHelpText(target), "Choose model file");
        StringAssert.Contains(AutomationProperties.GetHelpText(target), "Choose model folder");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DropSurface_ExposesPickerTargetsAndPoliteStatus()
    {
        var page = new ModelImportPage();
        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        var fileButton = card.FindName("ChooseModelFileButton") as Button;
        var folderButton = card.FindName("ChooseModelFolderButton") as Button;
        var announcement = page.FindName("SelectionAnnouncement") as TextBlock;

        Assert.IsNotNull(fileButton);
        Assert.IsNotNull(folderButton);
        Assert.IsTrue(fileButton.MinHeight >= 44);
        Assert.IsTrue(folderButton.MinHeight >= 44);
        Assert.AreEqual("Choose model file", AutomationProperties.GetName(fileButton));
        Assert.AreEqual("Choose model folder", AutomationProperties.GetName(folderButton));
        Assert.IsNotNull(announcement);
        Assert.AreEqual("Polite", AutomationProperties.GetLiveSetting(announcement).ToString());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RejectedDrop_AnnouncesRecoverableReasonInAccessibilityTree()
    {
        var page = new ModelImportPage();
        MethodInfo reject = typeof(ModelImportPage).GetMethod(
            "RejectDroppedSelectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(reject);
        var completion = (Task)reject.Invoke(
            page,
            [new ModelSelectionDiagnostic(
                "selection-multiple-items",
                "Drop only one model file or model folder at a time.")]);
        await completion;

        var announcement = page.FindName("SelectionAnnouncement") as TextBlock;
        Assert.IsNotNull(announcement);
        Assert.AreEqual(Visibility.Visible, announcement.Visibility);
        Assert.AreEqual(
            "Drop only one model file or model folder at a time.",
            announcement.Text);
        Assert.AreEqual("Polite", AutomationProperties.GetLiveSetting(announcement).ToString());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ValidSelectionAfterRejectedDrop_ClearsStaleAnnouncement()
    {
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateCancelled()),
            classifier: new AcceptedFolderClassifier());
        MethodInfo reject = typeof(ModelImportPage).GetMethod(
            "RejectDroppedSelectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        await (Task)reject.Invoke(
            page,
            [new ModelSelectionDiagnostic(
                "selection-multiple-items",
                "Drop only one model file or model folder at a time.")]);
        await page.SubmitInputAsync(
            new ModelSelectionInput(@"C:\Models\granite", "granite", isFolder: true));

        var announcement = page.FindName("SelectionAnnouncement") as TextBlock;
        Assert.IsNotNull(announcement);
        Assert.AreEqual(Visibility.Collapsed, announcement.Visibility);
        Assert.AreEqual(string.Empty, announcement.Text);
    }

    private static TextBlock GetDropStatus(ImportModelCard card) =>
        (TextBlock)card.FindName("DropValidationStatusTextBlock");

    private static FontIcon GetDropIcon(ImportModelCard card) =>
        (FontIcon)card.FindName("DropValidationStatusIcon");

    private static FrameworkElement GetDropStatusPanel(ImportModelCard card) =>
        (FrameworkElement)card.FindName("DropValidationStatusPanel");

    private sealed class AcceptedFolderClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId id,
            ModelSelectionInput input,
            CancellationToken cancellationToken) =>
            Task.FromResult(ModelSelectionResult.Accepted(
                id,
                ModelSelectionRoute.OpenVinoDirectory,
                input.DisplayName));

}
}
