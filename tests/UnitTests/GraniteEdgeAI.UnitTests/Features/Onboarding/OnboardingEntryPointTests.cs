using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class OnboardingEntryPointTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void MainWindow_Constructor_NavigatesRootFrameToOnboardingShell()
    {
        var mainWindow = new MainWindow();

        try
        {
            var windowContent = mainWindow.Content as FrameworkElement;
            Assert.IsNotNull(windowContent);

            var rootFrame = windowContent.FindName("rootFrame") as Frame;
            Assert.IsNotNull(rootFrame);
            var shell = rootFrame.Content as OnboardingShellPage;
            Assert.IsNotNull(shell);
            PropertyInfo? routeProperty = typeof(OnboardingShellPage).GetProperty(
                "IsHardwareRouteRegistered",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(routeProperty);
            Assert.IsTrue((bool)routeProperty.GetValue(shell)!);
        }
        finally
        {
            mainWindow.Close();
        }
    }
}
