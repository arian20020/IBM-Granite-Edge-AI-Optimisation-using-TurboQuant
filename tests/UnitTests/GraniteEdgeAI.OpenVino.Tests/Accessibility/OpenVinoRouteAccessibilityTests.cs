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
            "IBM Granite with TurboQuant (Intel)/Features/ModelInspection/ModelInspectionPage.xaml"));
        foreach (string name in new[]
        {
            "PromptInput",
            "PromptResponseText",
            "PromptCancelButton",
            "PromptStopButton",
            "PromptSendButton"
        })
        {
            StringAssert.Contains(xaml, $"x:Name=\"{name}\"");
        }
        foreach (string automationName in new[]
        {
            "Prompt for the local model",
            "Generated response",
            "Cancel local session",
            "Stop generation",
            "Send prompt"
        })
        {
            StringAssert.Contains(xaml, $"AutomationProperties.Name=\"{automationName}\"");
        }
        StringAssert.Contains(xaml, "AutomationProperties.LiveSetting=\"Polite\"");
        Assert.IsGreaterThanOrEqualTo(3,
            Count(xaml, "UseSystemFocusVisuals=\"True\""));
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
