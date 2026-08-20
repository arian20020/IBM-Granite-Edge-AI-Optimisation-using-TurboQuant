using System.Xml.Linq;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufChatVisualContractTests
{
    private static readonly string[] SoftModernThemeResourceKeys =
    [
        "GgufChatPrimaryGradientBrush",
        "GgufChatPrimaryBrush",
        "GgufChatPrimaryHoverBrush",
        "GgufChatPrimaryPressedBrush",
        "GgufChatPrimaryForegroundBrush",
        "GgufChatPanelBorderBrush",
        "GgufChatSecondarySurfaceBrush",
        "GgufChatFocusBrush",
        "GgufChatHistoryHoverBrush",
        "GgufChatSurfaceBrush",
        "GgufChatTextBrush",
    ];

    private static readonly string[] RequiredButtonVisualStates =
    [
        "Normal",
        "PointerOver",
        "Pressed",
        "Disabled",
        "Focused",
    ];

    [TestMethod]
    public void ChatThemeDefinesSoftModernSurfaceAndControlResources()
    {
        string root = FindRepositoryRoot();
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument theme = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Presentation",
            "GgufChatTheme.xaml"));

        foreach (string themeKey in new[] { "Light", "Dark", "HighContrast" })
        {
            XElement themeDictionary = GetThemeDictionary(theme, presentation, x, themeKey);
            foreach (string resourceKey in SoftModernThemeResourceKeys)
            {
                AssertThemeResource(themeDictionary, x, resourceKey, themeKey);
            }

            XElement primaryGradient = AssertThemeResource(
                themeDictionary,
                x,
                "GgufChatPrimaryGradientBrush",
                themeKey);
            Assert.AreEqual(presentation + "LinearGradientBrush", primaryGradient.Name);
        }

        XElement secondaryButtonStyle = AssertRootResource(
            theme,
            x,
            "GgufChatSecondaryButtonStyle");
        XElement primaryButtonStyle = AssertRootResource(
            theme,
            x,
            "GgufChatPrimaryButtonStyle");

        AssertStyleHasVisualStates(secondaryButtonStyle, presentation, x);
        AssertStyleHasVisualStates(primaryButtonStyle, presentation, x);
    }

    [TestMethod]
    public void ChatThemeUsesSystemHighlightTextForHighContrastPrimaryButtons()
    {
        string root = FindRepositoryRoot();
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument theme = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Presentation",
            "GgufChatTheme.xaml"));

        XElement highContrastDictionary = GetThemeDictionary(
            theme,
            presentation,
            x,
            "HighContrast");
        XElement highContrastForeground = AssertThemeResource(
            highContrastDictionary,
            x,
            "GgufChatPrimaryForegroundBrush",
            "HighContrast");
        StringAssert.Contains(
            highContrastForeground.Attribute("Color")?.Value,
            "SystemColorHighlightTextColor");
        XElement highContrastFocus = AssertThemeResource(
            highContrastDictionary,
            x,
            "GgufChatFocusBrush",
            "HighContrast");
        StringAssert.Contains(
            highContrastFocus.Attribute("Color")?.Value,
            "SystemColorHighlightColor");

        XElement primaryButtonStyle = AssertRootResource(
            theme,
            x,
            "GgufChatPrimaryButtonStyle");
        XElement foregroundSetter = primaryButtonStyle.Descendants(presentation + "Setter")
            .Single(setter => setter.Attribute("Property")?.Value == "Foreground");
        StringAssert.Contains(
            foregroundSetter.Attribute("Value")?.Value,
            "GgufChatPrimaryForegroundBrush");
    }

    [TestMethod]
    public void ChatUsesApprovedLightBrandingAndReferenceAlignment()
    {
        string root = FindRepositoryRoot();
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument page = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatPage.xaml"));
        XDocument composer = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Controls",
            "ChatComposer.xaml"));

        Assert.AreEqual("Light", page.Root?.Attribute("RequestedTheme")?.Value);
        XElement importButton = page.Descendants(presentation + "Button")
            .Single(element => element.Attribute(x + "Name")?.Value == "ImportModelButton");
        Assert.AreEqual(
            "Center",
            importButton.Attribute("HorizontalContentAlignment")?.Value);
        XElement brandImage = page.Descendants(presentation + "Image")
            .Single(element => element.Attribute(x + "Name")?.Value == "BrandLockup");
        StringAssert.Contains(
            brandImage.ToString(),
            "granite-edge-ai-lockup.svg");

        XElement prompt = composer.Descendants(presentation + "TextBox")
            .Single(element => element.Attribute(x + "Name")?.Value == "PromptTextBox");
        Assert.AreEqual(
            "Center",
            prompt.Attribute("VerticalContentAlignment")?.Value);

        string pageCode = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatPage.xaml.cs"));
        StringAssert.Contains(pageCode, "Margin = new Thickness(0, 14, 0, 6)");

        string project = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj"));
        StringAssert.Contains(project, "docs\\Logo\\granite-edge-ai-lockup.svg");
        StringAssert.Contains(project, "CopyToOutputDirectory");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static XElement GetThemeDictionary(
        XDocument theme,
        XNamespace presentation,
        XNamespace x,
        string themeKey)
    {
        XElement? dictionaries = theme.Root?
            .Element(presentation + "ResourceDictionary.ThemeDictionaries");
        XElement? dictionary = dictionaries?
            .Elements(presentation + "ResourceDictionary")
            .SingleOrDefault(element => element.Attribute(x + "Key")?.Value == themeKey);

        Assert.IsNotNull(dictionary, $"Theme dictionary '{themeKey}' is required.");
        return dictionary;
    }

    private static XElement AssertThemeResource(
        XElement themeDictionary,
        XNamespace x,
        string key,
        string themeKey)
    {
        XElement[] resources = themeDictionary.Elements()
            .Where(element => element.Attribute(x + "Key")?.Value == key)
            .ToArray();

        Assert.AreEqual(
            1,
            resources.Length,
            $"Theme resource '{key}' must appear exactly once in the {themeKey} dictionary.");
        return resources[0];
    }

    private static XElement AssertRootResource(
        XDocument theme,
        XNamespace x,
        string key)
    {
        XElement[] resources = theme.Root!
            .Elements()
            .Where(element => element.Attribute(x + "Key")?.Value == key)
            .ToArray();

        Assert.AreEqual(1, resources.Length, $"Root theme resource '{key}' is required.");
        return resources[0];
    }

    private static void AssertStyleHasVisualStates(
        XElement style,
        XNamespace presentation,
        XNamespace x)
    {
        foreach (string stateName in RequiredButtonVisualStates)
        {
            XElement state = style.Descendants(presentation + "VisualState")
                .SingleOrDefault(element => element.Attribute(x + "Name")?.Value == stateName)
                ?? throw new AssertFailedException(
                    $"The button style must define a '{stateName}' visual state.");
            Assert.IsTrue(
                state.Descendants(presentation + "Setter").Any()
                    || state.Descendants(presentation + "Storyboard").Any(),
                $"The '{stateName}' visual state must contain a visual change.");
        }

        AssertStateChangesControlSurface(style, presentation, x, "PointerOver");
        AssertStateChangesControlSurface(style, presentation, x, "Pressed");

        XElement focusedState = style.Descendants(presentation + "VisualState")
            .Single(element => element.Attribute(x + "Name")?.Value == "Focused");
        Assert.IsTrue(
            focusedState.Descendants(presentation + "Setter")
                .Any(setter => setter.Attribute("Target")?.Value == "FocusVisual.Visibility"),
            "The focused state must display a non-colour-only focus visual.");
    }

    private static void AssertStateChangesControlSurface(
        XElement style,
        XNamespace presentation,
        XNamespace x,
        string stateName)
    {
        XElement state = style.Descendants(presentation + "VisualState")
            .Single(element => element.Attribute(x + "Name")?.Value == stateName);
        Assert.IsTrue(
            state.Descendants(presentation + "Setter")
                .Any(setter =>
                    setter.Attribute("Target")?.Value is "RootBorder.Background"
                        or "RootBorder.BorderBrush"),
            $"The '{stateName}' visual state must change the control surface.");
    }
}
