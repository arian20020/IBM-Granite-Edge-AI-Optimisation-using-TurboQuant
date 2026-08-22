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
        "GgufChatAssistantBubbleBrush",
        "GgufChatAssistantBubbleTextBrush",
        "GgufChatUserBubbleTextBrush",
        "GgufChatNavigationRestBrush",
        "GgufChatNavigationHoverBrush",
        "GgufChatNavigationPressedBrush",
        "GgufChatNavigationSelectedBrush",
        "GgufChatNavigationSelectedHoverBrush",
        "GgufChatNavigationSelectedPressedBrush",
        "GgufChatComposerBrush",
        "GgufChatComposerFocusedBorderBrush",
        "GgufChatFlyoutBrush",
        "GgufChatDateHeadingBrush",
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

    private static readonly string[] KnowledgeFilePickerExtensions = [".txt", ".md"];

    private static readonly string[] KnowledgeFilePickerNativeTypes =
        ["PickFileResult", "FileOpenPicker", "picker", "result"];

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
        StringAssert.Contains(adapter, "Title = \"Add files\"");
        StringAssert.Contains(adapter, "CommitButtonText = \"Attach\"");
        Assert.AreEqual(
            1,
            Regex.Count(adapter, @"PickMultipleFilesAsync\s*\("),
            "The adapter must make exactly one multi-select picker call.");
        Match allowedFileTypes = Regex.Match(
            adapter,
            @"private\s+static\s+readonly\s+[^;]+\s+AllowedFileTypes\s*=\s*(?<initialization>[^;]+);");
        Assert.IsTrue(allowedFileTypes.Success, "Allowed file types must be declared as static readonly.");
        MatchCollection allowedExtensions = Regex.Matches(
            allowedFileTypes.Groups["initialization"].Value,
            "\"(?<extension>\\.[^\"]+)\"");
        CollectionAssert.AreEqual(
            KnowledgeFilePickerExtensions,
            allowedExtensions.Select(match => match.Groups["extension"].Value).ToArray(),
            "Allowed file types must be exactly .txt followed by .md.");
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
                @"foreach\s*\(\s*PickFileResult\s+result\s+in\s+results\s*\)\s*\{\s*paths\.Add\s*\(\s*result\?\.Path\s*\)\s*;\s*\}"),
            "Picker result paths must be snapshotted in their returned order before background work.");
        Match taskRun = Regex.Match(
            adapter,
            @"Task\.Run\s*\(\s*\(\s*\)\s*=>\s*(?<body>[^;]+)\s*\)");
        Assert.IsTrue(taskRun.Success, "Metadata mapping must run on the thread pool.");
        StringAssert.Contains(taskRun.Groups["body"].Value, "KnowledgeFileCandidateMapper.Map(paths)");
        foreach (string nativePickerType in KnowledgeFilePickerNativeTypes)
        {
            Assert.IsFalse(
                taskRun.Groups["body"].Value.Contains(nativePickerType, StringComparison.Ordinal),
                "Task.Run may capture only the path snapshot, not picker-native objects.");
        }

        foreach (string prohibitedPattern in KnowledgeFilePickerProhibitedPatterns)
        {
            Assert.IsFalse(
                Regex.IsMatch(adapter, prohibitedPattern),
                $"The picker adapter must not read or output selected file content or paths ({prohibitedPattern}).");
        }

        string runtimeRoot = Path.GetFullPath(Path.Combine(attachmentsDirectory, ".."));
        foreach (string productionFile in Directory.EnumerateFiles(
                     runtimeRoot,
                     "*.*",
                     SearchOption.AllDirectories)
                 .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                     || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.IsFalse(
                File.ReadAllText(productionFile).Contains(
                    "Add knowledge files",
                    StringComparison.Ordinal),
                $"Obsolete attachment copy remains in {Path.GetRelativePath(root, productionFile)}.");
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
        Assert.AreEqual(
            presentation + "ThemeShadow",
            AssertRootResource(theme, x, "GgufChatSubtlePanelShadow").Name);
    }

    [TestMethod]
    public void ChatThemeDefinesCalmFluentNavigationComposerAndBubbleContracts()
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
        XElement light = GetThemeDictionary(theme, presentation, x, "Light");
        var expected = new Dictionary<string, string>
        {
            ["GgufChatAssistantBubbleBrush"] = "#EAF2FF",
            ["GgufChatAssistantBubbleTextBrush"] = "#102E6B",
            ["GgufChatUserBubbleBrush"] = "#2563EB",
            ["GgufChatUserBubbleTextBrush"] = "#FFFFFF",
            ["GgufChatNavigationRestBrush"] = "#00FFFFFF",
            ["GgufChatNavigationHoverBrush"] = "#EEF4FF",
            ["GgufChatNavigationPressedBrush"] = "#E2ECFA",
            ["GgufChatNavigationSelectedBrush"] = "#2563EB",
            ["GgufChatNavigationSelectedHoverBrush"] = "#1D4ED8",
            ["GgufChatNavigationSelectedPressedBrush"] = "#1E40AF",
            ["GgufChatComposerBrush"] = "#FFFFFFFF",
            ["GgufChatComposerFocusedBorderBrush"] = "#2563EB",
            ["GgufChatFlyoutBrush"] = "#FFFFFFFF",
            ["GgufChatDateHeadingBrush"] = "#526174",
        };

        foreach ((string resourceKey, string expectedColor) in expected)
        {
            Assert.AreEqual(
                expectedColor,
                AssertThemeResource(light, x, resourceKey, "Light").Attribute("Color")?.Value,
                resourceKey);
        }

        Assert.IsTrue(
            ContrastRatio(expected["GgufChatAssistantBubbleBrush"], expected["GgufChatAssistantBubbleTextBrush"]) >= 4.5);
        Assert.IsTrue(
            ContrastRatio(expected["GgufChatUserBubbleBrush"], expected["GgufChatUserBubbleTextBrush"]) >= 4.5);
        Assert.IsTrue(
            ContrastRatio("#FFFFFFFF", expected["GgufChatDateHeadingBrush"]) >= 4.5,
            "Light-theme date headings must meet normal-text contrast on the white rail.");

        XElement navigation = AssertRootResource(theme, x, "GgufChatNavigationButtonStyle");
        AssertStyleHasVisualStates(navigation, presentation, x);
        XElement selectedNavigation = AssertRootResource(
            theme,
            x,
            "GgufChatSelectedNavigationButtonStyle");
        AssertStyleHasVisualStates(selectedNavigation, presentation, x);
        XElement selectedPointerOver = selectedNavigation.Descendants(presentation + "VisualState")
            .Single(state => state.Attribute(x + "Name")?.Value == "PointerOver");
        AssertStateSetterUsesThemeResource(
            selectedPointerOver,
            presentation,
            "RootBorder.Background",
            "GgufChatNavigationSelectedHoverBrush");
        AssertStateSetterUsesThemeResource(
            selectedPointerOver,
            presentation,
            "ContentPresenter.Foreground",
            "GgufChatPrimaryHoverForegroundBrush");
        XElement selectedPressed = selectedNavigation.Descendants(presentation + "VisualState")
            .Single(state => state.Attribute(x + "Name")?.Value == "Pressed");
        AssertStateSetterUsesThemeResource(
            selectedPressed,
            presentation,
            "RootBorder.Background",
            "GgufChatNavigationSelectedPressedBrush");
        AssertStateSetterUsesThemeResource(
            selectedPressed,
            presentation,
            "ContentPresenter.Foreground",
            "GgufChatPrimaryPressedForegroundBrush");
        Assert.IsTrue(
            ContrastRatio(expected["GgufChatNavigationSelectedHoverBrush"], "#FFFFFF") >= 4.5);
        Assert.IsTrue(
            ContrastRatio(expected["GgufChatNavigationSelectedPressedBrush"], "#FFFFFF") >= 4.5);
        Assert.IsNotNull(navigation.Descendants(presentation + "VisualState")
            .SingleOrDefault(state => state.Attribute(x + "Name")?.Value == "Unfocused"));
        Assert.AreEqual(
            "{ThemeResource GgufChatNavigationRestBrush}",
            navigation.Elements(presentation + "Setter")
                .Single(setter => setter.Attribute("Property")?.Value == "Background")
                .Attribute("Value")?.Value);
        Assert.AreEqual(
            "0",
            navigation.Elements(presentation + "Setter")
                .Single(setter => setter.Attribute("Property")?.Value == "BorderThickness")
                .Attribute("Value")?.Value);

        XElement editor = AssertRootResource(theme, x, "GgufChatComposerTextBoxStyle");
        Assert.AreEqual(
            "0",
            editor.Elements(presentation + "Setter")
                .Single(setter => setter.Attribute("Property")?.Value == "BorderThickness")
                .Attribute("Value")?.Value);
        Assert.AreEqual(
            "12,0",
            editor.Elements(presentation + "Setter")
                .Single(setter => setter.Attribute("Property")?.Value == "Padding")
                .Attribute("Value")?.Value);
        Assert.AreEqual(
            "Center",
            editor.Elements(presentation + "Setter")
                .Single(setter => setter.Attribute("Property")?.Value == "VerticalContentAlignment")
                .Attribute("Value")?.Value);

        XElement flyoutAction = AssertRootResource(theme, x, "GgufChatFlyoutActionStyle");
        Assert.AreEqual(
            "{StaticResource GgufChatNavigationButtonStyle}",
            flyoutAction.Attribute("BasedOn")?.Value);
    }

    [TestMethod]
    public void SelectedConversationFlowsIntoTheRenderedHistoryRow()
    {
        string root = FindRepositoryRoot();
        string controller = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatDemoController.cs"));
        string page = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatPage.xaml.cs"));

        StringAssert.Contains(
            controller,
            "ChatCoordinatorSnapshot snapshot = coordinator.CaptureSnapshot();");
        StringAssert.Contains(
            controller,
            "Guid? selectedId = snapshot.SelectedConversation?.Id;");
        StringAssert.Contains(controller, "foreach (ChatHistoryGroup group in snapshot.Groups)");
        Assert.IsTrue(Regex.IsMatch(
            controller,
            @"currentHistory\.Add\s*\(\s*new HistoryRenderKey\s*\(\s*group\.Label\s*,\s*conversation\.Id\s*,\s*conversation\.Title\s*,\s*conversation\.Id\s*==\s*selectedId\s*\)\s*\)\s*;"));
        Assert.IsTrue(Regex.IsMatch(
            controller,
            @"page\.AddHistoryConversation\s*\(\s*item\.ConversationId\s*,\s*item\.Title\s*,\s*item\.IsSelected\s*\)\s*;"));
        StringAssert.Contains(
            page,
            "AddHistoryConversation(Guid id, string title, bool isSelected)");
    }

    [TestMethod]
    public void StreamingRenderingIsIncrementalAndDispatcherCoalesced()
    {
        string root = FindRepositoryRoot();
        string controller = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatDemoController.cs"));
        string page = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatPage.xaml.cs"));

        StringAssert.Contains(controller, "ChatRenderScheduler");
        StringAssert.Contains(controller, "page.SynchronizeTranscript(");
        Assert.IsFalse(controller.Contains("page.ClearTranscript();", StringComparison.Ordinal));
        Assert.IsTrue(Regex.IsMatch(
            controller,
            @"if\s*\(\s*renderedHistory\.SequenceEqual\(currentHistory\)\s*\)\s*\{\s*return;\s*\}\s*page\.ClearHistory\(\);"));
        StringAssert.Contains(page, "ShouldFollowOutput(");
        StringAssert.Contains(page, "transcriptBubbles.TryGetValue");
        Assert.IsFalse(
            page.Contains("TranscriptList.ScrollIntoView", StringComparison.Ordinal));
        StringAssert.Contains(
            page,
            "TranscriptList.LayoutUpdated += TranscriptList_LayoutUpdated;");
        StringAssert.Contains(page, "scrollViewer.ChangeView(");
        StringAssert.Contains(page, "ScrollableHeight > 0");
        StringAssert.Contains(page, "transcriptFollowOriginOffset");
        Assert.IsFalse(page.Contains("UpdateLayout()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PreviewCopyAndAssistantIdentityStayTruthfulAndUserFacing()
    {
        string root = FindRepositoryRoot();
        string controller = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatDemoController.cs"));
        string session = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Services",
            "DemoGgufChatSession.cs"));
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument bubble = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Controls",
            "ChatMessageBubble.xaml"));

        StringAssert.Contains(
            controller,
            "\"Preview mode\",");
        StringAssert.Contains(
            controller,
            "\"No model loaded\")");
        StringAssert.Contains(
            controller,
            "page.SetModelHeader(displayName, runtimeDescription);");
        StringAssert.Contains(
            controller,
            "CreateProductionAsync(");
        StringAssert.Contains(session, "Preview mode is active");
        StringAssert.Contains(
            session,
            "Import a compatible GGUF model to run local generation");
        foreach (string prohibited in new[]
                 {
                     "deterministic demo runtime",
                     "production path uses",
                     "protected GGUF CLI supervisor",
                 })
        {
            Assert.IsFalse(controller.Contains(prohibited, StringComparison.Ordinal));
            Assert.IsFalse(session.Contains(prohibited, StringComparison.Ordinal));
        }

        XElement identity = bubble.Descendants(presentation + "TextBlock")
            .Single(element => element.Attribute(x + "Name")?.Value == "AssistantIdentityText");
        Assert.AreEqual("Granite Edge AI", identity.Attribute("Text")?.Value);
    }

    [TestMethod]
    public void WindowsIdentityAndGeneratedIconsComeFromTheApprovedGraniteSymbol()
    {
        string root = FindRepositoryRoot();
        string appRoot = Path.Combine(root, "IBM Granite with TurboQuant (Intel)");
        XNamespace foundation =
            "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        XDocument package = XDocument.Load(Path.Combine(appRoot, "Package.appxmanifest"));
        Assert.AreEqual(
            "Granite Edge AI",
            package.Root?.Element(foundation + "Properties")?
                .Element(foundation + "DisplayName")?.Value);
        XElement visualElements = package.Descendants(uap + "VisualElements").Single();
        Assert.AreEqual("Granite Edge AI", visualElements.Attribute("DisplayName")?.Value);
        Assert.AreEqual("Granite Edge AI", visualElements.Attribute("Description")?.Value);

        string mainWindow = File.ReadAllText(Path.Combine(appRoot, "MainWindow.xaml.cs"));
        StringAssert.Contains(mainWindow, "Title = \"Granite Edge AI\";");
        StringAssert.Contains(mainWindow, "AppWindow.SetIcon(iconPath);");
        StringAssert.Contains(mainWindow, "\"granite-edge-ai.ico\"");

        string generatorPath = Path.Combine(
            root,
            "scripts",
            "branding",
            "Generate-WindowsAppBranding.ps1");
        Assert.IsTrue(File.Exists(generatorPath), "The Windows branding generator is required.");
        string generator = File.ReadAllText(generatorPath);
        StringAssert.Contains(generator, "docs\\Logo\\granite-edge-ai-icon.svg");
        StringAssert.Contains(generator, "Windows.Media.Geometry]::Parse");
        StringAssert.Contains(generator, "windows-icon-manifest.json");

        string evidencePath = Path.Combine(
            appRoot,
            "Assets",
            "Branding",
            "windows-icon-manifest.json");
        Assert.IsTrue(File.Exists(evidencePath), "Generated icon evidence is required.");
        using System.Text.Json.JsonDocument evidence =
            System.Text.Json.JsonDocument.Parse(File.ReadAllText(evidencePath));
        System.Text.Json.JsonElement document = evidence.RootElement;
        Assert.AreEqual(1, document.GetProperty("schemaVersion").GetInt32());

        string sourceRelative = "docs/Logo/granite-edge-ai-icon.svg";
        string sourcePath = Path.Combine(root, sourceRelative.Replace('/', Path.DirectorySeparatorChar));
        const string approvedSourceSha256 =
            "b392df072100e16675c21987070b7e1063f5e891c1a4bedb920e90e679691ba2";
        Assert.AreEqual(approvedSourceSha256, FileHash(sourcePath));
        StringAssert.Contains(generator, approvedSourceSha256);
        StringAssert.Contains(generator, "GetAttribute('transform') -ne 'translate(0 0)'");
        StringAssert.Contains(generator, "GetAttribute('x1') -ne '48'");
        StringAssert.Contains(generator, "GetAttribute('gradientUnits') -ne 'userSpaceOnUse'");
        System.Text.Json.JsonElement source = document.GetProperty("source");
        Assert.AreEqual(sourceRelative, source.GetProperty("path").GetString());
        Assert.AreEqual(FileHash(sourcePath), source.GetProperty("sha256").GetString());

        var expectedPngs = new Dictionary<string, (int Width, int Height)>(StringComparer.Ordinal)
        {
            ["IBM Granite with TurboQuant (Intel)/Assets/LockScreenLogo.scale-200.png"] = (48, 48),
            ["IBM Granite with TurboQuant (Intel)/Assets/SplashScreen.scale-200.png"] = (1240, 600),
            ["IBM Granite with TurboQuant (Intel)/Assets/Square150x150Logo.scale-200.png"] = (300, 300),
            ["IBM Granite with TurboQuant (Intel)/Assets/Square44x44Logo.scale-200.png"] = (88, 88),
            ["IBM Granite with TurboQuant (Intel)/Assets/Square44x44Logo.targetsize-24_altform-unplated.png"] = (24, 24),
            ["IBM Granite with TurboQuant (Intel)/Assets/StoreLogo.png"] = (50, 50),
            ["IBM Granite with TurboQuant (Intel)/Assets/Wide310x150Logo.scale-200.png"] = (620, 300),
        };
        System.Text.Json.JsonElement[] pngEntries = document.GetProperty("pngs")
            .EnumerateArray()
            .ToArray();
        Assert.AreEqual(expectedPngs.Count, pngEntries.Length);
        foreach (System.Text.Json.JsonElement entry in pngEntries)
        {
            string? relativeValue = entry.GetProperty("path").GetString();
            Assert.IsNotNull(relativeValue);
            string relative = relativeValue;
            Assert.IsTrue(expectedPngs.Remove(relative, out (int Width, int Height) expected));
            string outputPath = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.AreEqual(FileHash(outputPath), entry.GetProperty("sha256").GetString());
            Assert.AreEqual(new FileInfo(outputPath).Length, entry.GetProperty("bytes").GetInt64());
            Assert.AreEqual(expected.Width, entry.GetProperty("width").GetInt32());
            Assert.AreEqual(expected.Height, entry.GetProperty("height").GetInt32());
            AssertPngDimensions(outputPath, expected.Width, expected.Height);
        }
        Assert.AreEqual(0, expectedPngs.Count);

        System.Text.Json.JsonElement ico = document.GetProperty("ico");
        string? icoRelativeValue = ico.GetProperty("path").GetString();
        Assert.IsNotNull(icoRelativeValue);
        string icoRelative = icoRelativeValue;
        string icoPath = Path.Combine(root, icoRelative.Replace('/', Path.DirectorySeparatorChar));
        Assert.AreEqual(FileHash(icoPath), ico.GetProperty("sha256").GetString());
        Assert.AreEqual(new FileInfo(icoPath).Length, ico.GetProperty("bytes").GetInt64());
        int[] expectedSizes = [16, 24, 32, 48, 64, 128, 256];
        CollectionAssert.AreEqual(
            expectedSizes,
            ico.GetProperty("sizes").EnumerateArray().Select(item => item.GetInt32()).ToArray());
        AssertIcoPngFrames(icoPath, expectedSizes);
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
            "Left",
            importButton.Attribute("HorizontalContentAlignment")?.Value);
        Assert.AreEqual("4,8", importButton.Attribute("Padding")?.Value);
        Assert.AreEqual(
            "{StaticResource GgufChatNavigationButtonStyle}",
            importButton.Attribute("Style")?.Value);

        XElement newChatButton = page.Descendants(presentation + "Button")
            .Single(element => element.Attribute(x + "Name")?.Value == "NewChatButton");
        Assert.AreEqual(
            "{StaticResource GgufChatNavigationButtonStyle}",
            newChatButton.Attribute("Style")?.Value);
        Assert.AreEqual(
            "Left",
            newChatButton.Attribute("HorizontalContentAlignment")?.Value);
        Assert.AreEqual("4,8", newChatButton.Attribute("Padding")?.Value);

        XElement settingsButton = page.Descendants(presentation + "Button")
            .Single(element => element.Attribute("AutomationProperties.Name")?.Value == "Settings");
        Assert.AreEqual(
            "{StaticResource GgufChatNavigationButtonStyle}",
            settingsButton.Attribute("Style")?.Value);
        Assert.AreEqual(
            "Left",
            settingsButton.Attribute("HorizontalContentAlignment")?.Value);
        Assert.AreEqual("4,8", settingsButton.Attribute("Padding")?.Value);

        XElement historyRegion = page.Descendants(presentation + "Grid")
            .Single(element => element.Attribute(x + "Name")?.Value == "HistoryRegion");
        XElement[] historyRows = historyRegion
            .Element(presentation + "Grid.RowDefinitions")!
            .Elements(presentation + "RowDefinition")
            .ToArray();
        Assert.AreEqual(2, historyRows.Length);
        Assert.AreEqual("Auto", historyRows[0].Attribute("Height")?.Value);
        Assert.AreEqual("*", historyRows[1].Attribute("Height")?.Value);
        XElement historyList = historyRegion.Elements(presentation + "ListView")
            .Single(element => element.Attribute(x + "Name")?.Value == "ChatHistoryList");
        Assert.AreEqual("1", historyList.Attribute("Grid.Row")?.Value);
        Assert.AreEqual(
            presentation + "Grid",
            historyList.Parent?.Name,
            "The history list must be constrained by a star-sized Grid row.");

        XElement settingsFooter = page.Descendants(presentation + "Border")
            .Single(element => element.Attribute(x + "Name")?.Value == "SettingsFooter");
        Assert.AreEqual("0,12,0,0", settingsFooter.Attribute("Margin")?.Value);
        Assert.AreEqual("0,12,0,0", settingsFooter.Attribute("Padding")?.Value);
        Assert.AreEqual("0,1,0,0", settingsFooter.Attribute("BorderThickness")?.Value);

        XElement transcript = page.Descendants(presentation + "ListView")
            .Single(element => element.Attribute(x + "Name")?.Value == "TranscriptList");
        XElement transcriptMargin = transcript.Descendants(presentation + "Setter")
            .Single(element => element.Attribute("Property")?.Value == "Margin");
        Assert.AreEqual("0,0,0,12", transcriptMargin.Attribute("Value")?.Value);

        XElement brandImage = page.Descendants(presentation + "Image")
            .Single(element => element.Attribute(x + "Name")?.Value == "BrandLockup");
        Assert.AreEqual(
            "ms-appx:///Assets/Branding/granite-edge-ai-lockup-outlined.svg",
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
        Assert.AreEqual("14", conversationPanel.Attribute("CornerRadius")?.Value);
        Assert.AreEqual("24", conversationPanel.Attribute("Padding")?.Value);
        Assert.AreEqual(
            "{StaticResource GgufChatSubtlePanelShadow}",
            conversationPanel.Attribute("Shadow")?.Value);
        Assert.AreEqual("0,0,2", conversationPanel.Attribute("Translation")?.Value);

        XElement historyColumn = page.Descendants(presentation + "ColumnDefinition")
            .Single(element => element.Attribute(x + "Name")?.Value == "HistoryColumn");
        Assert.AreEqual("256", historyColumn.Attribute("Width")?.Value);
        XElement wideState = page.Descendants(presentation + "VisualState")
            .Single(element => element.Attribute(x + "Name")?.Value == "WideState");
        XElement wideHistoryWidth = wideState.Descendants(presentation + "Setter")
            .Single(element => element.Attribute("Target")?.Value == "HistoryColumn.Width");
        Assert.AreEqual("256", wideHistoryWidth.Attribute("Value")?.Value);

        XDocument historyItem = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Controls",
            "ChatHistoryItem.xaml"));
        Assert.IsFalse(historyItem.Descendants()
            .Any(element => element.Attribute(x + "Name")?.Value == "SelectionIndicator"));
        XElement historyButton = historyItem.Descendants(presentation + "Button").Single();
        Assert.AreEqual("Left", historyButton.Attribute("HorizontalContentAlignment")?.Value);
        Assert.AreEqual("4,8", historyButton.Attribute("Padding")?.Value);
        XElement? historyContentCandidate = historyButton.Element(presentation + "Grid");
        Assert.IsNotNull(historyContentCandidate, "History rows require the shared icon/text grid.");
        XElement historyContent = historyContentCandidate;
        Assert.AreEqual("HistoryContentGrid", historyContent.Attribute(x + "Name")?.Value);
        Assert.AreEqual("12", historyContent.Attribute("ColumnSpacing")?.Value);
        XElement[] historyColumns = historyContent
            .Element(presentation + "Grid.ColumnDefinitions")!
            .Elements(presentation + "ColumnDefinition")
            .ToArray();
        Assert.AreEqual(2, historyColumns.Length);
        Assert.AreEqual("20", historyColumns[0].Attribute("Width")?.Value);
        Assert.AreEqual("*", historyColumns[1].Attribute("Width")?.Value);
        Assert.AreEqual(
            "1",
            historyContent.Element(presentation + "TextBlock")?.Attribute("Grid.Column")?.Value);

        XElement prompt = composer.Descendants(presentation + "TextBox")
            .Single(element => element.Attribute(x + "Name")?.Value == "PromptTextBox");
        XElement composerSurface = composer.Descendants(presentation + "Border")
            .Single(element => element.Attribute(x + "Name")?.Value == "ComposerSurface");
        XElement promptRow = composer.Descendants(presentation + "Grid")
            .Single(element => element.Attribute(x + "Name")?.Value == "PromptRow");
        XElement attachmentButton = composer.Descendants(presentation + "Button")
            .Single(element => element.Attribute(x + "Name")?.Value == "AttachmentButton");
        XElement sendButton = composer.Descendants(presentation + "Button")
            .Single(element => element.Attribute(x + "Name")?.Value == "SendButton");
        XElement stopButton = composer.Descendants(presentation + "Button")
            .Single(element => element.Attribute(x + "Name")?.Value == "StopButton");
        XElement? composerContentCandidate = composerSurface.Element(presentation + "Grid");
        Assert.IsNotNull(composerContentCandidate);
        XElement composerContent = composerContentCandidate;
        XElement attachmentPresentation = composer.Descendants(presentation + "StackPanel")
            .Single(element => element.Attribute(x + "Name")?.Value == "AttachmentPresentation");
        XElement focusVisual = composer.Descendants(presentation + "Border")
            .Single(element => element.Attribute(x + "Name")?.Value == "ComposerFocusVisual");
        Assert.AreEqual("6,1", composerSurface.Attribute("Padding")?.Value);
        Assert.AreEqual("20", composerSurface.Attribute("CornerRadius")?.Value);
        Assert.AreEqual("0", composerContent.Attribute("RowSpacing")?.Value);
        Assert.AreEqual("0,0,0,8", attachmentPresentation.Attribute("Margin")?.Value);
        Assert.AreEqual("22", focusVisual.Attribute("CornerRadius")?.Value);
        Assert.AreEqual("40", promptRow.Attribute("MinHeight")?.Value);
        Assert.AreEqual("40", attachmentButton.Attribute("Width")?.Value);
        Assert.AreEqual("40", attachmentButton.Attribute("Height")?.Value);
        Assert.AreEqual("Attach files", attachmentButton.Attribute("AutomationProperties.Name")?.Value);
        Assert.AreEqual("Attach files", attachmentButton.Attribute("ToolTipService.ToolTip")?.Value);
        Assert.AreEqual(
            "\uE723",
            attachmentButton.Element(presentation + "FontIcon")?.Attribute("Glyph")?.Value);
        Assert.AreEqual(
            "\uE724",
            sendButton.Element(presentation + "FontIcon")?.Attribute("Glyph")?.Value);
        Assert.AreEqual("32", prompt.Attribute("MinHeight")?.Value);
        Assert.AreEqual("10,0", prompt.Attribute("Padding")?.Value);
        Assert.AreEqual("PromptTextBox_KeyDown", prompt.Attribute("KeyDown")?.Value);
        Assert.AreEqual("40", sendButton.Attribute("Height")?.Value);
        Assert.AreEqual("40", stopButton.Attribute("Height")?.Value);
        Assert.AreEqual(
            "Center",
            prompt.Attribute("VerticalContentAlignment")?.Value);
        Assert.AreEqual(
            "{StaticResource GgufChatPrimaryButtonStyle}",
            stopButton.Attribute("Style")?.Value);
        Assert.IsNull(stopButton.Attribute("Background"));

        string pageCode = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "ChatPage.xaml.cs"));
        StringAssert.Contains(pageCode, "Margin = new Thickness(0, 14, 0, 6)");

        string composerCode = File.ReadAllText(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "GgufRuntime",
            "Controls",
            "ChatComposer.xaml.cs"));
        StringAssert.Contains(composerCode, "private void UpdateSubmissionState()");
        StringAssert.Contains(
            composerCode,
            "SendButton.IsEnabled = !IsGenerating &&");
        StringAssert.Contains(
            composerCode,
            "PromptTextBox.Text.Trim().Length > 0;");

        XDocument project = XDocument.Load(Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "IBM Granite with TurboQuant (Intel).csproj"));
        XElement lockupContent = project.Descendants("Content")
            .Single(element => element.Attribute("Include")?.Value
                == "Assets\\Branding\\granite-edge-ai-lockup-outlined.svg");
        Assert.AreEqual(
            "PreserveNewest",
            lockupContent.Element("CopyToOutputDirectory")?.Value);
        Assert.AreEqual(
            "PreserveNewest",
            lockupContent.Element("CopyToPublishDirectory")?.Value);

        string sourceLockupPath = Path.Combine(
            root,
            "docs",
            "Logo",
            "granite-edge-ai-lockup.svg");
        string outlinedLockupPath = Path.Combine(
            root,
            "IBM Granite with TurboQuant (Intel)",
            "Assets",
            "Branding",
            "granite-edge-ai-lockup-outlined.svg");
        XDocument sourceLockup = XDocument.Load(sourceLockupPath);
        XDocument outlinedLockup = XDocument.Load(outlinedLockupPath);
        XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.AreEqual(0, outlinedLockup.Descendants(svg + "text").Count());
        Assert.AreEqual(0, outlinedLockup.Descendants(svg + "tspan").Count());
        Assert.IsGreaterThan(9, outlinedLockup.Descendants(svg + "path").Count());
        Assert.AreEqual(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(sourceLockupPath))).ToLowerInvariant(),
            outlinedLockup.Root?.Attribute("data-source-sha256")?.Value);

        string[] sourceMonogramPaths = sourceLockup.Descendants(svg + "path")
            .Select(path => path.Attribute("d")?.Value ?? string.Empty)
            .ToArray();
        string[] outlinedMonogramPaths = outlinedLockup.Descendants(svg + "g")
            .Single(group => group.Attribute("data-brand-element")?.Value
                == "Granite Edge AI G monogram")
            .Elements(svg + "path")
            .Select(path => path.Attribute("d")?.Value ?? string.Empty)
            .ToArray();
        CollectionAssert.AreEqual(sourceMonogramPaths, outlinedMonogramPaths);

        XElement[] outlinedGlyphs = outlinedLockup.Descendants(svg + "path")
            .Where(path => path.Attribute("data-source-char-index") is not null)
            .ToArray();
        int[] expectedCharacterIndices = [0, 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 13, 14];
        CollectionAssert.AreEqual(
            expectedCharacterIndices,
            outlinedGlyphs.Select(path => int.Parse(
                path.Attribute("data-source-char-index")!.Value,
                System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        CollectionAssert.AreEqual(
            expectedCharacterIndices.Select(index => $"translate({-4 * index} 0)").ToArray(),
            outlinedGlyphs.Select(path => path.Attribute("transform")?.Value).ToArray());

        XElement centerMark = page.Descendants(presentation + "Image")
            .Single(element => element.Attribute(x + "Name")?.Value == "EmptyStateBrandMark");
        Assert.AreEqual(
            "ms-appx:///Assets/Branding/granite-edge-ai-icon.svg",
            centerMark.Attribute("Source")?.Value);
    }

    private static string FileHash(string path) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
        .ToLowerInvariant();

    private static void AssertPngDimensions(string path, int expectedWidth, int expectedHeight)
    {
        byte[] bytes = File.ReadAllBytes(path);
        byte[] pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        CollectionAssert.AreEqual(pngSignature, bytes[..8], path);
        Assert.AreEqual(
            expectedWidth,
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
            path);
        Assert.AreEqual(
            expectedHeight,
            System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)),
            path);
    }

    private static void AssertIcoPngFrames(string path, int[] expectedSizes)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        Assert.AreEqual(0, reader.ReadUInt16(), "ICO reserved field");
        Assert.AreEqual(1, reader.ReadUInt16(), "ICO type");
        ushort count = reader.ReadUInt16();
        Assert.AreEqual(expectedSizes.Length, count);
        var frames = new List<(int Size, uint Bytes, uint Offset)>();
        for (int index = 0; index < count; index++)
        {
            byte width = reader.ReadByte();
            byte height = reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadUInt16();
            reader.ReadUInt16();
            uint bytes = reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
            int decodedWidth = width == 0 ? 256 : width;
            int decodedHeight = height == 0 ? 256 : height;
            Assert.AreEqual(decodedWidth, decodedHeight);
            frames.Add((decodedWidth, bytes, offset));
        }

        CollectionAssert.AreEqual(expectedSizes.ToArray(), frames.Select(frame => frame.Size).ToArray());
        foreach ((int size, uint bytes, uint offset) in frames)
        {
            stream.Position = offset;
            byte[] png = reader.ReadBytes(checked((int)bytes));
            byte[] pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
            CollectionAssert.AreEqual(pngSignature, png[..8]);
            Assert.AreEqual(
                size,
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
            Assert.AreEqual(
                size,
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        }
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
