using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufChatVisualContractTests
{
    private static readonly string[] SoftModernThemeResourceKeys =
    [
        "GgufChatPrimaryGradientBrush",
        "GgufChatPrimaryBrush",
        "GgufChatPrimaryHoverBrush",
        "GgufChatPrimaryHoverBorderBrush",
        "GgufChatPrimaryPressedBrush",
        "GgufChatPrimaryPressedBorderBrush",
        "GgufChatPrimaryForegroundBrush",
        "GgufChatPrimaryHoverForegroundBrush",
        "GgufChatPrimaryPressedForegroundBrush",
        "GgufChatPrimaryDisabledBrush",
        "GgufChatPrimaryDisabledForegroundBrush",
        "GgufChatPrimaryDisabledBorderBrush",
        "GgufChatPanelBorderBrush",
        "GgufChatSecondarySurfaceBrush",
        "GgufChatSecondaryDisabledSurfaceBrush",
        "GgufChatSecondaryDisabledTextBrush",
        "GgufChatSecondaryDisabledBorderBrush",
        "GgufChatSecondaryPointerOverBrush",
        "GgufChatSecondaryPointerOverForegroundBrush",
        "GgufChatSecondaryPointerOverBorderBrush",
        "GgufChatSecondaryPressedBrush",
        "GgufChatSecondaryPressedForegroundBrush",
        "GgufChatSecondaryPressedBorderBrush",
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

    private static readonly string[] KnowledgeFilePickerOnlyAddOperation = ["Add"];

    private static readonly string[] KnowledgeFilePickerCandidateReturnExpressions =
        ["candidates.AsReadOnly()"];

    private static readonly string[] KnowledgeFilePickerCatchTypes =
    [
        "IOException",
        "UnauthorizedAccessException",
        "System.Security.SecurityException",
        "ArgumentException",
        "NotSupportedException",
    ];

    private static readonly string[] KnowledgeFilePickerProhibitedPatterns =
    [
        @"\b(?:System\s*\.\s*IO\s*\.\s*)?File\s*\.",
        @"\b(?:new\s+)?(?:FileInfo|DirectoryInfo)\b[\s\S]{0,160}?\.\s*(?:Open\w*|Create\w*|Delete|MoveTo|CopyTo)\s*\(",
        @"\b(?:Stream|TextReader|TextWriter|BinaryReader|BinaryWriter|FileStream|MemoryStream|BufferedStream)\b",
        @"\b(?:Read|Write|Append)\w*\s*\(",
        @"\b(?:Console|Debug|Trace|TraceSource|EventLog|EventSource|ILogger|Logger|logger|Log|log|Log[A-Z]\w*|Telemetry\w*|telemetry\w*|NLog|Serilog|ApplicationInsights)\b",
        @"\b(?:MessageBox|Clipboard|ToastNotification|AppNotification|OutputDebugString)\b",
    ];

    private static readonly (string State, string Background, string Foreground, string Border)[] PrimaryStateResourcePairs =
    [
        ("Normal", "GgufChatPrimaryGradientBrush", "GgufChatPrimaryForegroundBrush", "GgufChatPrimaryBrush"),
        ("PointerOver", "GgufChatPrimaryHoverBrush", "GgufChatPrimaryHoverForegroundBrush", "GgufChatPrimaryHoverBorderBrush"),
        ("Pressed", "GgufChatPrimaryPressedBrush", "GgufChatPrimaryPressedForegroundBrush", "GgufChatPrimaryPressedBorderBrush"),
        ("Disabled", "GgufChatPrimaryDisabledBrush", "GgufChatPrimaryDisabledForegroundBrush", "GgufChatPrimaryDisabledBorderBrush"),
    ];

    private static readonly (string State, string Background, string Foreground, string Border)[] SecondaryStateResourcePairs =
    [
        ("Normal", "GgufChatSecondarySurfaceBrush", "GgufChatTextBrush", "GgufChatPanelBorderBrush"),
        ("PointerOver", "GgufChatSecondaryPointerOverBrush", "GgufChatSecondaryPointerOverForegroundBrush", "GgufChatSecondaryPointerOverBorderBrush"),
        ("Pressed", "GgufChatSecondaryPressedBrush", "GgufChatSecondaryPressedForegroundBrush", "GgufChatSecondaryPressedBorderBrush"),
        ("Disabled", "GgufChatSecondaryDisabledSurfaceBrush", "GgufChatSecondaryDisabledTextBrush", "GgufChatSecondaryDisabledBorderBrush"),
    ];

    private static readonly (string Resource, string SystemResource)[] HighContrastPrimaryAliases =
    [
        ("GgufChatPrimaryGradientBrush", "AccentButtonBackground"),
        ("GgufChatPrimaryForegroundBrush", "AccentButtonForeground"),
        ("GgufChatPrimaryBrush", "AccentButtonBorderBrush"),
        ("GgufChatPrimaryHoverBrush", "AccentButtonBackgroundPointerOver"),
        ("GgufChatPrimaryHoverForegroundBrush", "AccentButtonForegroundPointerOver"),
        ("GgufChatPrimaryHoverBorderBrush", "AccentButtonBorderBrushPointerOver"),
        ("GgufChatPrimaryPressedBrush", "AccentButtonBackgroundPressed"),
        ("GgufChatPrimaryPressedForegroundBrush", "AccentButtonForegroundPressed"),
        ("GgufChatPrimaryPressedBorderBrush", "AccentButtonBorderBrushPressed"),
        ("GgufChatPrimaryDisabledBrush", "AccentButtonBackgroundDisabled"),
        ("GgufChatPrimaryDisabledForegroundBrush", "AccentButtonForegroundDisabled"),
        ("GgufChatPrimaryDisabledBorderBrush", "AccentButtonBorderBrushDisabled"),
    ];

    private static readonly (string Resource, string SystemResource)[] HighContrastSecondaryAliases =
    [
        ("GgufChatSecondarySurfaceBrush", "ButtonBackground"),
        ("GgufChatTextBrush", "ButtonForeground"),
        ("GgufChatPanelBorderBrush", "ButtonBorderBrush"),
        ("GgufChatSecondaryPointerOverBrush", "ButtonBackgroundPointerOver"),
        ("GgufChatSecondaryPointerOverForegroundBrush", "ButtonForegroundPointerOver"),
        ("GgufChatSecondaryPointerOverBorderBrush", "ButtonBorderBrushPointerOver"),
        ("GgufChatSecondaryPressedBrush", "ButtonBackgroundPressed"),
        ("GgufChatSecondaryPressedForegroundBrush", "ButtonForegroundPressed"),
        ("GgufChatSecondaryPressedBorderBrush", "ButtonBorderBrushPressed"),
        ("GgufChatSecondaryDisabledSurfaceBrush", "ButtonBackgroundDisabled"),
        ("GgufChatSecondaryDisabledTextBrush", "ButtonForegroundDisabled"),
        ("GgufChatSecondaryDisabledBorderBrush", "ButtonBorderBrushDisabled"),
    ];

    [TestMethod]
    public void KnowledgeFilePickerUsesTheWindowsMultiSelectBoundaryWithoutReadingContent()
    {
        string root = FindRepositoryRoot();
        string attachmentsDirectory = Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Attachments");
        string contract = File.ReadAllText(Path.Combine(
            attachmentsDirectory,
            "IKnowledgeFilePicker.cs"));
        string adapter = File.ReadAllText(Path.Combine(
            attachmentsDirectory,
            "WindowsKnowledgeFilePicker.cs"));

        StringAssert.Contains(contract, "internal interface IKnowledgeFilePicker");
        StringAssert.Contains(
            contract,
            "Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync()");

        StringAssert.Contains(adapter, "using Microsoft.Windows.Storage.Pickers;");
        StringAssert.Contains(
            adapter,
            "new FileOpenPicker(App.MainWindow.AppWindow.Id)");
        StringAssert.Contains(adapter, "Title = \"Add knowledge files\"");
        StringAssert.Contains(adapter, "CommitButtonText = \"Attach\"");
        Assert.AreEqual(
            1,
            Regex.Count(adapter, @"PickMultipleFilesAsync\s*\("),
            "The adapter must make exactly one multi-select picker call.");
        Assert.IsTrue(
            Regex.IsMatch(
                adapter,
                """private static readonly IReadOnlyList<string> AllowedFileTypes\s*=\s*Array\.AsReadOnly\s*\(\s*new\[\]\s*\{\s*".txt"\s*,\s*".md"\s*\}\s*\)\s*;"""),
            "Allowed file types must be an immutable .txt/.md list in deterministic order.");
        Assert.AreEqual(
            1,
            Regex.Count(adapter, @"FileTypeFilter\s*\.\s*Add\s*\("),
            "The adapter must have one filter mutation site.");
        Assert.IsTrue(
            Regex.IsMatch(
                adapter,
                @"foreach\s*\(\s*string\s+fileType\s+in\s+AllowedFileTypes\s*\)\s*\{\s*picker\.FileTypeFilter\.Add\s*\(\s*fileType\s*\)\s*;\s*\}"),
            "The immutable file types must be added in their declared order.");
        MatchCollection filterOperations = Regex.Matches(
            adapter,
            @"FileTypeFilter\s*\.\s*(?<operation>[A-Za-z_]\w*)");
        CollectionAssert.AreEquivalent(
            KnowledgeFilePickerOnlyAddOperation,
            filterOperations.Select(match => match.Groups["operation"].Value).ToArray(),
            "No filter mutation or access beyond the ordered Add call is allowed.");
        Assert.IsFalse(
            Regex.IsMatch(adapter, @"FileTypeFilter\s*\[[^\]]+\]\s*="),
            "The filter collection must not be index-assigned.");
        Assert.IsFalse(
            Regex.IsMatch(adapter, @"FileTypeFilter\s*="),
            "The filter collection must not be replaced.");

        StringAssert.Contains(adapter, "results is null || results.Count == 0");
        Assert.IsTrue(
            Regex.IsMatch(
                adapter,
                @"foreach\s*\(\s*PickFileResult\s+result\s+in\s+results\s*\)\s*\{\s*candidates\.Add\s*\(\s*ToCandidate\s*\(\s*result\?\.Path\s*\)\s*\)\s*;\s*\}"),
            "Picker results must be appended in their returned order.");
        MatchCollection candidateOperations = Regex.Matches(
            adapter,
            @"candidates\s*\.\s*(?<operation>Add|AddRange|Insert|Remove|RemoveAt|RemoveAll|RemoveRange|Clear|Reverse|Sort)\b");
        CollectionAssert.AreEquivalent(
            KnowledgeFilePickerOnlyAddOperation,
            candidateOperations.Select(match => match.Groups["operation"].Value).ToArray(),
            "Candidates may only be appended in the picker result loop.");
        Assert.IsFalse(
            Regex.IsMatch(
                adapter,
                @"\b(?:Reverse|Sort|Order(?:By(?:Descending)?)?|ThenBy(?:Descending)?)\s*\("),
            "Picker selections must not be reordered.");
        Assert.IsFalse(
            Regex.IsMatch(
                adapter,
                @"\b(?:results|candidates)\s*\["),
            "Picker results and candidates must not use index-based mutation or reversal.");
        Assert.IsFalse(
            Regex.IsMatch(adapter, @"\bSwap\s*\("),
            "Picker selections must not be manually swapped.");
        MatchCollection candidateReturns = Regex.Matches(
            adapter,
            @"return\s+(?<expression>[^;]*\bcandidates\b[^;]*)\s*;");
        CollectionAssert.AreEqual(
            KnowledgeFilePickerCandidateReturnExpressions,
            candidateReturns
                .Select(match => Regex.Replace(match.Groups["expression"].Value, @"\s+", string.Empty))
                .ToArray(),
            "Candidates must be returned immediately as a read-only natural-order collection.");
        StringAssert.Contains(adapter, "string.IsNullOrWhiteSpace(path)");
        StringAssert.Contains(adapter, "new FileInfo(path).Length");
        StringAssert.Contains(adapter, "new KnowledgeFileCandidate(path, sizeInBytes, true)");
        StringAssert.Contains(adapter, "new KnowledgeFileCandidate(path, 0, false)");
        MatchCollection catches = Regex.Matches(
            adapter,
            @"catch\s*(?:\(\s*(?<type>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*(?:[A-Za-z_]\w*)?\s*\))?");
        CollectionAssert.AreEquivalent(
            KnowledgeFilePickerCatchTypes,
            catches.Select(match => match.Groups["type"].Value).ToArray(),
            "Only expected metadata exceptions may be caught.");
        StringAssert.Contains(
            adapter,
            "PathTooLongException is covered by IOException",
            "PathTooLongException must be intentionally covered by the IOException catch.");

        foreach (string prohibitedPattern in KnowledgeFilePickerProhibitedPatterns)
        {
            Assert.IsFalse(
                Regex.IsMatch(adapter, prohibitedPattern),
                $"The picker adapter must not read or output selected file content or paths ({prohibitedPattern}).");
        }
    }

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

        }

        foreach (string themeKey in new[] { "Light", "Dark" })
        {
            XElement primaryGradient = AssertThemeResource(
                GetThemeDictionary(theme, presentation, x, themeKey),
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
        AssertButtonStylesAreDefinedOnlyAtRoot(theme, presentation, x);
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
        Assert.AreEqual("AccentButtonForeground", highContrastForeground.Attribute("ResourceKey")?.Value);
        XElement highContrastFocus = AssertThemeResource(
            highContrastDictionary,
            x,
            "GgufChatFocusBrush",
            "HighContrast");
        Assert.AreEqual("SystemControlFocusVisualPrimaryBrush", highContrastFocus.Attribute("ResourceKey")?.Value);

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
    public void ChatThemePrimaryButtonsMeetContrastAndHighContrastTemplateContracts()
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

        XElement primaryButtonStyle = AssertRootResource(theme, x, "GgufChatPrimaryButtonStyle");
        foreach ((string stateName, string backgroundKey, string foregroundKey, string borderKey)
                 in PrimaryStateResourcePairs)
        {
            AssertPrimaryStateUsesResources(
                primaryButtonStyle,
                presentation,
                x,
                stateName,
                backgroundKey,
                foregroundKey,
                borderKey);
        }

        XElement secondaryButtonStyle = AssertRootResource(theme, x, "GgufChatSecondaryButtonStyle");
        foreach ((string stateName, string backgroundKey, string foregroundKey, string borderKey)
                 in SecondaryStateResourcePairs)
        {
            AssertPrimaryStateUsesResources(
                secondaryButtonStyle,
                presentation,
                x,
                stateName,
                backgroundKey,
                foregroundKey,
                borderKey);
        }

        foreach (string themeKey in new[] { "Light", "Dark" })
        {
            XElement themeDictionary = GetThemeDictionary(theme, presentation, x, themeKey);
            foreach ((_, string backgroundKey, string foregroundKey, _) in PrimaryStateResourcePairs)
            {
                XElement background = AssertThemeResource(
                    themeDictionary,
                    x,
                    backgroundKey,
                    themeKey);
                XElement foreground = AssertThemeResource(
                    themeDictionary,
                    x,
                    foregroundKey,
                    themeKey);
                AssertPrimaryContrast(background, foreground, presentation, themeKey, backgroundKey);
            }
        }

        foreach (string styleKey in new[]
                 {
                     "GgufChatPrimaryButtonStyle",
                     "GgufChatSecondaryButtonStyle",
                 })
        {
            XElement style = AssertRootResource(theme, x, styleKey);
            XElement contentPresenter = style.Descendants(presentation + "ContentPresenter")
                .Single();
            Assert.AreEqual(
                "{TemplateBinding Foreground}",
                contentPresenter.Attribute("Foreground")?.Value);
            Assert.AreEqual(
                "Raw",
                contentPresenter.Attribute("AutomationProperties.AccessibilityView")?.Value);
        }

        XElement highContrast = GetThemeDictionary(theme, presentation, x, "HighContrast");
        AssertHighContrastAliases(highContrast, x, HighContrastPrimaryAliases);
        AssertHighContrastAliases(highContrast, x, HighContrastSecondaryAliases);
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
        Assert.AreEqual(
            "{StaticResource GgufChatSecondaryButtonStyle}",
            importButton.Attribute("Style")?.Value);

        XElement newChatButton = page.Descendants(presentation + "Button")
            .Single(element => element.Attribute(x + "Name")?.Value == "NewChatButton");
        Assert.AreEqual(
            "{StaticResource GgufChatPrimaryButtonStyle}",
            newChatButton.Attribute("Style")?.Value);
        Assert.AreEqual(
            "Center",
            newChatButton.Attribute("HorizontalContentAlignment")?.Value);

        XElement settingsButton = page.Descendants(presentation + "Button")
            .Single(element => element.Attribute("AutomationProperties.Name")?.Value == "Settings");
        Assert.AreEqual(
            "{StaticResource GgufChatSecondaryButtonStyle}",
            settingsButton.Attribute("Style")?.Value);
        Assert.AreEqual(
            "Left",
            settingsButton.Attribute("HorizontalContentAlignment")?.Value);

        XElement brandImage = page.Descendants(presentation + "Image")
            .Single(element => element.Attribute(x + "Name")?.Value == "BrandLockup");
        Assert.AreEqual(
            "ms-appx:///Assets/Branding/granite-edge-ai-lockup.svg",
            brandImage.Attribute("Source")?.Value);
        Assert.AreEqual("Left", brandImage.Attribute("HorizontalAlignment")?.Value);
        Assert.AreEqual("Uniform", brandImage.Attribute("Stretch")?.Value);
        AssertDimensionIsWithinRange(brandImage, "Width", 220, 232);
        AssertDimensionIsWithinRange(brandImage, "Height", 56, 64);

        XElement conversationPanel = page.Descendants(presentation + "Border")
            .Single(element => element.Attribute(x + "Name")?.Value == "ConversationPanel");
        Assert.AreEqual(
            "{ThemeResource GgufChatSurfaceBrush}",
            conversationPanel.Attribute("Background")?.Value);
        Assert.AreEqual(
            "{ThemeResource GgufChatPanelBorderBrush}",
            conversationPanel.Attribute("BorderBrush")?.Value);
        Assert.AreEqual("1", conversationPanel.Attribute("BorderThickness")?.Value);
        Assert.AreEqual("24", conversationPanel.Attribute("CornerRadius")?.Value);
        Assert.AreEqual(
            "{ThemeResource GgufChatPanelShadow}",
            conversationPanel.Attribute("Shadow")?.Value);
        AssertHasNonzeroZTranslation(conversationPanel);

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

        XDocument project = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj"));
        XElement lockupContent = project.Descendants("Content")
            .Single(element => element.Attribute("Include")?.Value
                == "..\\docs\\Logo\\granite-edge-ai-lockup.svg");
        Assert.AreEqual(
            "Assets\\Branding\\granite-edge-ai-lockup.svg",
            lockupContent.Element("Link")?.Value);
        Assert.AreEqual("PreserveNewest", lockupContent.Element("CopyToOutputDirectory")?.Value);
        Assert.AreEqual("PreserveNewest", lockupContent.Element("CopyToPublishDirectory")?.Value);
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

    private static void AssertPrimaryContrast(
        XElement background,
        XElement foreground,
        XNamespace presentation,
        string themeKey,
        string backgroundKey)
    {
        IEnumerable<string?> backgroundColors = background.Name.LocalName == "LinearGradientBrush"
            ? GetOpaqueGradientEndpointColors(background, presentation)
            : [background.Attribute("Color")?.Value];
        string foregroundColor = foreground.Attribute("Color")?.Value
            ?? throw new AssertFailedException("Primary foreground must use a static colour.");

        foreach (string? backgroundColor in backgroundColors)
        {
            Assert.IsNotNull(backgroundColor, "Primary backgrounds must specify a colour.");
            double ratio = ContrastRatio(backgroundColor!, foregroundColor);
            Assert.IsTrue(
                ratio >= 4.5,
                $"{themeKey} {backgroundKey} contrast is {ratio:F2}:1; expected at least 4.5:1.");
        }
    }

    private static void AssertPrimaryStateUsesResources(
        XElement style,
        XNamespace presentation,
        XNamespace x,
        string stateName,
        string backgroundKey,
        string foregroundKey,
        string borderKey)
    {
        XElement state = style.Descendants(presentation + "VisualState")
            .Single(element => element.Attribute(x + "Name")?.Value == stateName);
        AssertStateSetterUsesThemeResource(
            state,
            presentation,
            "RootBorder.Background",
            backgroundKey);
        AssertStateSetterUsesThemeResource(
            state,
            presentation,
            "ContentPresenter.Foreground",
            foregroundKey);
        AssertStateSetterUsesThemeResource(
            state,
            presentation,
            "RootBorder.BorderBrush",
            borderKey);
    }

    private static void AssertStateSetterUsesThemeResource(
        XElement state,
        XNamespace presentation,
        string target,
        string resourceKey)
    {
        XElement setter = state.Descendants(presentation + "Setter")
            .Single(element => element.Attribute("Target")?.Value == target);
        Assert.AreEqual(
            $"{{ThemeResource {resourceKey}}}",
            setter.Attribute("Value")?.Value,
            $"{target} must use the {resourceKey} resource.");
    }

    private static double ContrastRatio(string firstColor, string secondColor)
    {
        double firstLuminance = RelativeLuminance(firstColor);
        double secondLuminance = RelativeLuminance(secondColor);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05)
            / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double RelativeLuminance(string color)
    {
        string hex = color.TrimStart('#');
        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        Assert.AreEqual(6, hex.Length, $"'{color}' must be an RGB hex colour.");
        return (0.2126 * Linearize(Convert.ToByte(hex[..2], 16) / 255d))
            + (0.7152 * Linearize(Convert.ToByte(hex.Substring(2, 2), 16) / 255d))
            + (0.0722 * Linearize(Convert.ToByte(hex.Substring(4, 2), 16) / 255d));
    }

    private static double Linearize(double channel) => channel <= 0.04045
        ? channel / 12.92
        : Math.Pow((channel + 0.055) / 1.055, 2.4);

    private static IEnumerable<string?> GetOpaqueGradientEndpointColors(
        XElement gradient,
        XNamespace presentation)
    {
        XElement[] stops = gradient.Elements(presentation + "GradientStop").ToArray();
        Assert.IsTrue(stops.Length >= 2, "Primary gradients must have at least two stops.");
        Assert.IsTrue(
            stops.Any(stop => stop.Attribute("Offset")?.Value == "0"),
            "Primary gradients must define a start stop at offset 0.");
        Assert.IsTrue(
            stops.Any(stop => stop.Attribute("Offset")?.Value == "1"),
            "Primary gradients must define an end stop at offset 1.");
        Assert.IsFalse(
            stops.Any(stop => !IsOpaqueGradientStop(stop)),
            "Primary gradient stops must be opaque.");
        return stops.Select(stop => stop.Attribute("Color")?.Value);
    }

    private static bool IsOpaqueGradientStop(XElement stop)
    {
        string? color = stop.Attribute("Color")?.Value;
        return color is not null
            && (color.Length == 7
                || (color.Length == 9
                    && color.StartsWith("#FF", StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertButtonStylesAreDefinedOnlyAtRoot(
        XDocument theme,
        XNamespace presentation,
        XNamespace x)
    {
        foreach (string styleKey in new[]
                 {
                     "GgufChatPrimaryButtonStyle",
                     "GgufChatSecondaryButtonStyle",
                 })
        {
            AssertRootResource(theme, x, styleKey);
            bool isDuplicated = theme.Root!
                .Element(presentation + "ResourceDictionary.ThemeDictionaries")!
                .Descendants(presentation + "Style")
                .Any(style => style.Attribute(x + "Key")?.Value == styleKey);
            Assert.IsFalse(isDuplicated, $"'{styleKey}' must not be duplicated in a theme dictionary.");
        }
    }

    private static void AssertHighContrastAliases(
        XElement highContrast,
        XNamespace x,
        IEnumerable<(string Resource, string SystemResource)> aliases)
    {
        foreach ((string resourceKey, string systemResourceKey) in aliases)
        {
            XElement resource = AssertThemeResource(highContrast, x, resourceKey, "HighContrast");
            Assert.AreEqual("StaticResource", resource.Name.LocalName);
            Assert.AreEqual(systemResourceKey, resource.Attribute("ResourceKey")?.Value);
        }
    }

    private static void AssertDimensionIsWithinRange(
        XElement element,
        string attributeName,
        int minimum,
        int maximum)
    {
        bool parsed = int.TryParse(element.Attribute(attributeName)?.Value, out int dimension);
        Assert.IsTrue(parsed, $"{attributeName} must be a numeric value.");
        Assert.IsTrue(
            dimension >= minimum && dimension <= maximum,
            $"{attributeName} must be between {minimum} and {maximum}.");
    }

    private static void AssertHasNonzeroZTranslation(XElement element)
    {
        string? translation = element.Attribute("Translation")?.Value;
        Assert.IsNotNull(translation, "The conversation surface must be translated for ThemeShadow.");

        string[] components = translation.Split(',');
        Assert.AreEqual(3, components.Length, "Translation must have X, Y, and Z components.");
        bool parsed = double.TryParse(components[2], out double z);
        Assert.IsTrue(parsed, "The ThemeShadow Z translation must be numeric.");
        Assert.IsTrue(z > 0, "The conversation surface must have a nonzero ThemeShadow Z translation.");
    }
}
