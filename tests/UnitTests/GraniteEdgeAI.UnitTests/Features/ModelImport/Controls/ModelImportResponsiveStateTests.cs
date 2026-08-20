using GraniteEdgeAI.Features.ModelImport.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportResponsiveStateTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void DragValidation_UsesStaticAccessibleStatusForValidAndInvalidDrops()
    {
        var card = new ImportModelCard();

        card.ShowDragValidation(isValid: true);
        Assert.AreEqual(Visibility.Visible, Status(card).Visibility);
        Assert.AreEqual("Drop model now", Status(card).Text);
        Assert.AreEqual("Valid drop target", AutomationProperties.GetName(StatusIcon(card)));

        card.ShowDragValidation(isValid: false);
        Assert.AreEqual("Drop exactly one supported model file or folder.", Status(card).Text);
        Assert.AreEqual("Invalid drop target", AutomationProperties.GetName(StatusIcon(card)));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ClearDragValidation_HidesStaticStatusWithoutChangingAwaitingState()
    {
        var card = new ImportModelCard();
        card.ShowDragValidation(isValid: true);

        card.ClearDragValidation();

        Assert.AreEqual(ImportModelCardState.AwaitingSelection, card.CurrentState);
        Assert.AreEqual(
            0d,
            ((FrameworkElement)card.FindName("DropValidationStatusPanel")).Opacity);
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

    private static TextBlock Status(ImportModelCard card) =>
        (TextBlock)card.FindName("DropValidationStatusTextBlock");

    private static FontIcon StatusIcon(ImportModelCard card) =>
        (FontIcon)card.FindName("DropValidationStatusIcon");
}
