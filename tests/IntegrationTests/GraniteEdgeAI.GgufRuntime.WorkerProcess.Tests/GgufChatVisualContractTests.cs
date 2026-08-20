using System.Xml.Linq;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufChatVisualContractTests
{
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

        XElement primaryGradient = AssertThemeResource(
            theme,
            presentation,
            x,
            "GgufChatPrimaryGradientBrush");
        Assert.AreEqual(presentation + "LinearGradientBrush", primaryGradient.Name);
        AssertThemeResource(theme, presentation, x, "GgufChatPanelBorderBrush");
        XElement secondaryButtonStyle = AssertThemeResource(
            theme,
            presentation,
            x,
            "GgufChatSecondaryButtonStyle");
        XElement primaryButtonStyle = AssertThemeResource(
            theme,
            presentation,
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

        XElement highContrastDictionary = theme.Descendants(presentation + "ResourceDictionary")
            .Single(dictionary => dictionary.Attribute(x + "Key")?.Value == "HighContrast");
        XElement? highContrastForeground = highContrastDictionary.Elements()
            .SingleOrDefault(element =>
                element.Attribute(x + "Key")?.Value == "GgufChatPrimaryForegroundBrush");
        Assert.IsNotNull(highContrastForeground);
        StringAssert.Contains(
            highContrastForeground.Attribute("Color")?.Value,
            "SystemColorHighlightTextColor");

        XElement primaryButtonStyle = AssertThemeResource(
            theme,
            presentation,
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

    private static XElement AssertThemeResource(
        XDocument theme,
        XNamespace presentation,
        XNamespace x,
        string key)
    {
        XElement? resource = theme.Descendants()
            .FirstOrDefault(element => element.Attribute(x + "Key")?.Value == key);

        Assert.IsNotNull(resource, $"Theme resource '{key}' is required.");
        return resource;
    }

    private static void AssertStyleHasVisualStates(
        XElement style,
        XNamespace presentation,
        XNamespace x)
    {
        IReadOnlyCollection<string> stateNames = style
            .Descendants(presentation + "VisualState")
            .Select(state => state.Attribute(x + "Name")?.Value)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        string[] requiredStates = ["Normal", "PointerOver", "Pressed", "Disabled", "Focused"];
        Assert.IsTrue(
            requiredStates.All(stateNames.Contains),
            "The button style must define normal, interaction, disabled, and focus states.");
    }
}
