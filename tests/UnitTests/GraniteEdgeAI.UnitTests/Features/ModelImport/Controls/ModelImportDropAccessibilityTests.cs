using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        Assert.IsNotNull(border.Stroke);
        Assert.IsTrue(border.StrokeDashArray.Count > 0);
        Assert.AreEqual(6d, border.StrokeDashArray[0]);
        Assert.AreEqual(5d, border.StrokeDashArray[1]);
        Assert.IsTrue(border.RadiusX > 0);
        Assert.IsTrue(border.RadiusY > 0);
        Assert.AreEqual(20d, border.RadiusX);
        Assert.AreEqual(20d, border.RadiusY);
        Assert.AreEqual("#FAFBFD", BrushColor(border.Fill));
        Assert.AreEqual("#C7D2E1", BrushColor(border.Stroke));
        Assert.AreEqual(Visibility.Visible, border.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ValidDrag_SubtlyDarkensDropCardSurface()
    {
        var card = new ImportModelCard();

        card.ShowDragValidation(isValid: true);

        Assert.AreEqual(ImportModelCardState.DragOverValid, card.CurrentState);
        Assert.AreEqual(1.5d, GetRectangle(card, "AwaitingSelectionBorder").StrokeThickness);
        var hoverOverlay = GetRectangle(card, "ValidDragHoverOverlay");
        Assert.AreEqual(Visibility.Visible, hoverOverlay.Visibility);
        Assert.AreEqual("#000000", BrushColor(hoverOverlay.Fill));
        Assert.AreEqual(0x10, BrushOpacity(hoverOverlay.Fill));
        Assert.IsNull(card.FindName("DropValidationStatusTextBlock"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InvalidDrag_PreservesBaselineDropCardPresentation()
    {
        var card = new ImportModelCard();

        card.ShowDragValidation(isValid: false);

        Assert.AreEqual(ImportModelCardState.DragOverInvalid, card.CurrentState);
        Assert.AreEqual(1.5d, GetRectangle(card, "AwaitingSelectionBorder").StrokeThickness);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetRectangle(card, "ValidDragHoverOverlay").Visibility);
        Assert.IsNull(card.FindName("DropValidationStatusTextBlock"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DragLeave_RestoresAwaitingSelectionPresentation()
    {
        var card = new ImportModelCard();
        card.ShowDragValidation(isValid: true);

        card.ClearDragValidation();

        Assert.AreEqual(ImportModelCardState.AwaitingSelection, card.CurrentState);
        Assert.AreEqual(1.5d, GetRectangle(card, "AwaitingSelectionBorder").StrokeThickness);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetRectangle(card, "ValidDragHoverOverlay").Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            ((FrameworkElement)card.FindName("AwaitingSelectionBorder")).Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ValidDrag_InDarkTheme_UsesTheSameNeutralSurfaceOverlay()
    {
        var card = new ImportModelCard { RequestedTheme = ElementTheme.Dark };

        card.ShowDragValidation(isValid: true);

        var hoverOverlay = GetRectangle(card, "ValidDragHoverOverlay");
        Assert.AreEqual(Visibility.Visible, hoverOverlay.Visibility);
        Assert.AreEqual("#000000", BrushColor(hoverOverlay.Fill));
        Assert.AreEqual(0x10, BrushOpacity(hoverOverlay.Fill));
        Assert.AreEqual(1.5d, GetRectangle(card, "AwaitingSelectionBorder").StrokeThickness);
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
        StringAssert.Contains(AutomationProperties.GetHelpText(target), "OpenVINO model folder");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DropSurface_ExposesOneAccessiblePickerAction()
    {
        var page = new ModelImportPage();
        var card = (ImportModelCard)page.FindName("ImportModelCardControl");
        var fileButton = (Button)card.FindName("ChooseModelFileButton");
        Assert.IsTrue(fileButton.MinHeight >= 44);
        Assert.AreEqual("Choose model file", AutomationProperties.GetName(fileButton));
        StringAssert.Contains(AutomationProperties.GetHelpText(fileButton), "OpenVINO");
        Assert.IsNull(card.FindName("ChooseModelFolderButton"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RejectedDrop_AnnouncesRecoverableReasonInAccessibilityTree()
    {
        var page = new ModelImportPage();
        MethodInfo reject = typeof(ModelImportPage).GetMethod(
            "RejectDroppedSelectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("RejectDroppedSelectionAsync was not found.");

        var completion = reject.Invoke(
            page,
            [new ModelSelectionDiagnostic(
                "selection-multiple-items",
                "Drop only one model file or model folder at a time.")]) as Task
            ?? throw new AssertFailedException("RejectDroppedSelectionAsync did not return a Task.");
        await completion;

        var announcement = (TextBlock)page.FindName("SelectionAnnouncement");
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
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("RejectDroppedSelectionAsync was not found.");

        await (reject.Invoke(
            page,
            [new ModelSelectionDiagnostic(
                "selection-multiple-items",
                "Drop only one model file or model folder at a time.")]) as Task
            ?? throw new AssertFailedException("RejectDroppedSelectionAsync did not return a Task."));
        await page.SubmitInputAsync(
            new ModelSelectionInput(@"C:\Models\granite", "granite", isFolder: true));

        var announcement = (TextBlock)page.FindName("SelectionAnnouncement");
        Assert.AreEqual(Visibility.Collapsed, announcement.Visibility);
        Assert.AreEqual(string.Empty, announcement.Text);
    }

    private static Rectangle GetRectangle(ImportModelCard card, string name) =>
        (Rectangle)card.FindName(name);

    private static string BrushColor(Brush brush) =>
        $"#{((SolidColorBrush)brush).Color.R:X2}{((SolidColorBrush)brush).Color.G:X2}{((SolidColorBrush)brush).Color.B:X2}";

    private static byte BrushOpacity(Brush brush) =>
        ((SolidColorBrush)brush).Color.A;

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
