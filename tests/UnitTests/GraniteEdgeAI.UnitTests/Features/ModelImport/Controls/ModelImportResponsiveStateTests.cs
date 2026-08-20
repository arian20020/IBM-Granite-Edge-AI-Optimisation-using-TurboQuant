using GraniteEdgeAI.Features.ModelImport.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportResponsiveStateTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void DragValidation_DoesNotReplaceTheDefaultDropCardContent()
    {
        var card = new ImportModelCard();

        card.ShowDragValidation(isValid: true);
        Assert.AreEqual(1.5d, ((Microsoft.UI.Xaml.Shapes.Rectangle)card.FindName("AwaitingSelectionBorder")).StrokeThickness);

        card.ShowDragValidation(isValid: false);
        Assert.AreEqual(1.5d, ((Microsoft.UI.Xaml.Shapes.Rectangle)card.FindName("AwaitingSelectionBorder")).StrokeThickness);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ClearDragValidation_HidesStaticStatusWithoutChangingAwaitingState()
    {
        var card = new ImportModelCard();
        card.ShowDragValidation(isValid: true);

        card.ClearDragValidation();

        Assert.AreEqual(ImportModelCardState.AwaitingSelection, card.CurrentState);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void FolderSelection_UsesTerminalCardWithoutGGUFMetadata()
    {
        var card = new ImportModelCard();

        card.ShowFolderAccepted("granite-openvino");

        Assert.AreEqual(ImportModelCardState.SelectionAccepted, card.CurrentState);
        Assert.AreEqual(
            Visibility.Visible,
            ((FrameworkElement)card.FindName("FolderAcceptedView")).Visibility);
        Assert.AreEqual("granite-openvino", ((TextBlock)card.FindName(
            "FolderAcceptedNameTextBlock")).Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ImportPage_DisablesHorizontalScrollingForCompactAndLargeTextLayouts()
    {
        var page = new GraniteEdgeAI.Features.ModelImport.ModelImportPage();
        var scroll = page.FindName("ModelImportPageScrollViewer") as ScrollViewer;

        Assert.IsNotNull(scroll);
        Assert.AreEqual(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.AreEqual(ScrollMode.Disabled, scroll.HorizontalScrollMode);
        Assert.AreEqual(HorizontalAlignment.Stretch, scroll.HorizontalContentAlignment);
    }

}
