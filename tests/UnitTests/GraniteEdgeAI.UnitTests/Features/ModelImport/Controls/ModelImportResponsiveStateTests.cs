using GraniteEdgeAI.Features.ModelImport.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    public void OpenVinoFolderSelection_MatchesImportedModelCardChrome()
    {
        var card = new ImportModelCard();

        card.ShowFolderAccepted("granite-openvino");

        var folderView = (Border)card.FindName("FolderAcceptedView");
        var successView = (Border)card.FindName("SuccessView");

        Assert.AreEqual(ImportModelCardState.SelectionAccepted, card.CurrentState);
        Assert.AreEqual(Visibility.Visible, folderView.Visibility);
        Assert.AreEqual(successView.Height, folderView.Height);
        Assert.AreEqual(successView.Padding, folderView.Padding);
        Assert.AreEqual(successView.BorderThickness, folderView.BorderThickness);
        Assert.AreEqual(successView.CornerRadius, folderView.CornerRadius);
        Assert.AreEqual(
            ((SolidColorBrush)successView.Background).Color,
            ((SolidColorBrush)folderView.Background).Color);
        Assert.AreEqual(
            ((SolidColorBrush)successView.BorderBrush).Color,
            ((SolidColorBrush)folderView.BorderBrush).Color);
        Assert.AreEqual("granite-openvino", ((TextBlock)card.FindName(
            "FolderAcceptedNameTextBlock")).Text);
        Assert.AreEqual("OpenVINO", ((TextBlock)card.FindName(
            "FolderAcceptedFormatTextBlock")).Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OpenVinoFolderSelection_RemoveButtonClearsTheSharedSelection()
    {
        var card = new ImportModelCard();
        var cancelRequests = 0;
        card.CancelScanRequested += (_, _) => cancelRequests++;
        card.ShowFolderAccepted("granite-openvino");

        var button = (Button)card.FindName("RemoveFolderAcceptedModelButton");
        Assert.AreEqual("RemoveFolderAcceptedModelButton",
            AutomationProperties.GetAutomationId(button));
        Assert.AreEqual("Remove selected OpenVINO model",
            AutomationProperties.GetName(button));
        var peer = new ButtonAutomationPeer(button);
        var provider = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke);
        provider.Invoke();

        Assert.AreEqual(1, cancelRequests);
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

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ImportPage_DoesNotExposeManualDownloadedModelSearch()
    {
        var page = new GraniteEdgeAI.Features.ModelImport.ModelImportPage();

        Assert.IsNull(page.FindName("FindDownloadedModelsButton"));
        Assert.IsFalse(ContainsButtonWithContent(page, "Find downloaded models"));
    }

    private static bool ContainsButtonWithContent(DependencyObject parent, string content)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is Button button && string.Equals(button.Content?.ToString(), content,
                    System.StringComparison.Ordinal))
            {
                return true;
            }

            if (ContainsButtonWithContent(child, content))
            {
                return true;
            }
        }

        return false;
    }

}
