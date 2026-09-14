using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

[TestClass, DoNotParallelize]
public sealed class OptimizedGgufChatRecoveryTests
{
    [TestMethod]
    public void LibraryCustodySurvivesTargetRetirementAndReleasesExactlyOnce()
    {
        var lease = new CountingLease();
        // this lifetime-only test deliberately does not exercise result parsing
        var target = new GgufOptimizationChatTarget(null!, "unused", "unused", 0, null!, lease);
        IDisposable libraryLease = target.RetainVerifiedCustody();
        target.Dispose();
        Assert.AreEqual(0, lease.Disposals);
        Assert.ThrowsExactly<ObjectDisposedException>(() => target.RetainVerifiedCustody());
        libraryLease.Dispose();
        libraryLease.Dispose();
        target.Dispose();
        Assert.AreEqual(1, lease.Disposals);
    }

    [UITestMethod]
    public async Task ScrimHasStablePointerStatesAndEmptyLibraryAllowsImport()
    {
        var page = new ChatPage();
        await using var host = await WinUiRenderHost.ShowAsync(page, 900, 760);
        var scrim = (Button)page.FindName("CompactNavigationScrim");
        scrim.Visibility = Visibility.Visible;
        scrim.ApplyTemplate();
        var border = (Border)VisualTreeHelper.GetChild(scrim, 0);
        var background = border.Background;
        VisualStateManager.GoToState(scrim, "PointerOver", false);
        Assert.AreSame(background, border.Background);
        VisualStateManager.GoToState(scrim, "Pressed", false);
        Assert.AreSame(background, border.Background);
        var composer = (FrameworkElement)page.FindName("Composer");
        var pill = (Button)composer.FindName("ChatComposerModelPill");
        Assert.IsTrue(pill.IsEnabled, "Empty library must still allow opening import/discovery actions.");
    }

    [TestMethod]
    public void OptimizedRegistrationUsesConfigurationAndRetainedVerifiedCustody()
    {
        string shell = File.ReadAllText(Path.Combine(RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "Features", "Onboarding", "OnboardingShellPage.xaml.cs"));
        StringAssert.Contains(shell, "target.Result.ConfigurationSha256[..24]");
        StringAssert.Contains(shell, "target.RetainVerifiedCustody()");
        StringAssert.Contains(shell, "Opening chat…");
    }

    private sealed class CountingLease : IDisposable
    {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
}
