using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

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
            Assert.IsInstanceOfType<OnboardingShellPage>(rootFrame.Content);
        }
        finally
        {
            mainWindow.Close();
        }
    }

    [TestMethod]
    public void PackagedRuntimeRoot_IsTheGgufRuntimeChildDirectory()
    {
        string root = MainWindow.GetPackagedGgufRuntimeRoot(
            @"C:\GraniteEdgeAI");

        Assert.AreEqual(@"C:\GraniteEdgeAI\GgufRuntime", root);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task UntrustedProductionChatRequestReturnsToOnboardingWithoutPreviewFallback()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string model = Path.Combine(root, "granite.gguf");
        File.WriteAllBytes(model, [1]);
        var mainWindow = new MainWindow();
        try
        {
            var request = new GgufChatLaunchRequest(
                root,
                System.Text.Encoding.UTF8.GetBytes(
                    $$"""
                    {"schemaVersion":1,"runtimeBuildId":"llama-build","runtimeSourceCommit":"{{new string('b', 40)}}","buildFlags":[],"files":[]}
                    """),
                model,
                "Granite",
                Configuration());

            await Assert.ThrowsExactlyAsync<GgufRuntimeTrustException>(() =>
                mainWindow.OpenProductionChatAsync(request));

            var windowContent = Assert.IsInstanceOfType<FrameworkElement>(
                mainWindow.Content);
            var rootFrame = Assert.IsInstanceOfType<Frame>(
                windowContent.FindName("rootFrame"));
            Assert.IsInstanceOfType<OnboardingShellPage>(rootFrame.Content);
        }
        finally
        {
            mainWindow.Close();
            Directory.Delete(root, recursive: true);
        }
    }

    private static GgufRuntimeConfiguration Configuration() => new(
        "granite-test",
        new string('a', 64),
        "llama-build",
        new string('b', 40),
        GgufRuntimeBackend.Cpu,
        "cpu",
        4096,
        GgufCacheType.F16,
        GgufCacheType.F16,
        0,
        false,
        4,
        256,
        "inspected",
        "cpu-default");
}
