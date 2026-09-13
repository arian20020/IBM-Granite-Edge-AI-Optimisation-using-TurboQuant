using GraniteEdgeAI.Features.OpenVinoRoute.TurboQuant;

namespace GraniteEdgeAI.OpenVino.Tests.Accessibility;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoRouteAccessibilityTests
{
    [TestMethod]
    public void SharedPromptControlsHaveNamesFocusVisualsAndBoundedLiveRegion()
    {
        string xaml = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/ChatPage.xaml")) + File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Controls/ChatComposer.xaml"));
        foreach (string name in new[]
        {
            "PromptTextBox",
            "RouteStatusText",
            "RouteCloseButton",
            "StopButton",
            "SendButton"
        })
        {
            StringAssert.Contains(xaml, $"x:Name=\"{name}\"");
        }
        foreach (string automationName in new[]
        {
            "Message",
            "Conversation messages",
            "Close local session",
            "Stop generation",
            "Send message"
        })
        {
            StringAssert.Contains(xaml, $"AutomationProperties.Name=\"{automationName}\"");
        }
        StringAssert.Contains(xaml, "AutomationProperties.LiveSetting=\"Polite\"");
        string theme = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/Presentation/GgufChatTheme.xaml"));
        StringAssert.Contains(xaml, "Property=\"UseSystemFocusVisuals\" Value=\"True\"");
        StringAssert.Contains(theme, "<Border x:Name=\"FocusVisual\"");
        StringAssert.Contains(theme, "<VisualStateGroup x:Name=\"FocusStates\">");
        StringAssert.Contains(theme, "Target=\"FocusVisual.Visibility\" Value=\"Visible\"");
        StringAssert.Contains(theme, "ResourceKey=\"SystemControlFocusVisualPrimaryBrush\"");
    }

    [TestMethod]
    public void PromptLifecycleKeepsFocusResetStopCancelAndNavigationCleanup()
    {
        string source = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.OpenVino.cs"));
        foreach (string required in new[]
        {
            "PromptInput.Focus(FocusState.Programmatic)",
            "StopActiveTurnAsync",
            "CancelActiveTurnAsync",
            "RetireForNavigationAsync",
            "DisposeAsync"
        })
        {
            StringAssert.Contains(source, required);
        }
    }

    [TestMethod]
    public void ExperimentalAndFallbackCopyIsExplicitBoundedAndTerminalFree()
    {
        string adapter = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantRouteAdapter.cs"));
        string fallback = File.ReadAllText(RepoPath(
            "IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/TurboQuant/TurboQuantFallbackService.cs"));
        StringAssert.Contains(adapter, "Experimental");
        StringAssert.Contains(adapter, "Requested/actual TBQ4/TBQ4");
        StringAssert.Contains(fallback, "explicit user confirmation");
        StringAssert.Contains(fallback, "Use verified official OpenVINO");
        StringAssert.Contains(fallback, "Use applicable verified GGUF");
        Assert.IsFalse((adapter + fallback).Contains("terminal", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse((adapter + fallback).Contains("command prompt", StringComparison.OrdinalIgnoreCase));
    }

    private static int Count(string value, string token)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(token, index,
                 StringComparison.Ordinal)) >= 0; index += token.Length)
        {
            count++;
        }
        return count;
    }

    private static string RepoPath(string relative) => Path.Combine(
        FindRepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
