using GraniteEdgeAI.Features.ModelInspection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies the navigation contract of ModelInspectionPage.
/// </summary>
[TestClass]
public sealed class ModelInspectionPageNavigationTests
{
    /// <summary>
    /// Verifies that Frame navigation supplies the validated model path to the
    /// inspection page.
    /// </summary>
    [UITestMethod]
    [TestCategory("WinUI")]
    public void FrameNavigation_WithModelPath_StoresSelectedModelPath()
    {
        // Arrange one authoritative model path.
        const string selectedModelPath =
            @"C:\Models\granite-4.1-3b-instruct.gguf";

        // Use a real WinUI navigation Frame.
        var frame = new Frame();

        // Navigate using the same parameter mechanism the shell will use.
        bool navigationSucceeded =
            frame.Navigate(
                typeof(ModelInspectionPage),
                selectedModelPath);

        // Read the destination page created by the Frame.
        var inspectionPage =
            frame.Content as ModelInspectionPage;

        // The destination must retain the original path.
        Assert.IsTrue(navigationSucceeded);
        Assert.IsNotNull(inspectionPage);

        Assert.AreEqual(
            selectedModelPath,
            inspectionPage.SelectedModelPath);
    }
}