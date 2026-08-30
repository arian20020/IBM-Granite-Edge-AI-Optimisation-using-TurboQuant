using System.Xml.Linq;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class RemediationGapContractTests
{
    [TestMethod]
    public void ApplicationComposesExactlyOneOnboardingShell()
    {
        string source = ReadAppFile("MainWindow.xaml.cs");
        const string composition = "rootFrame.Navigate(typeof(OnboardingShellPage))";

        Assert.AreEqual(1, Count(source, composition));
    }

    [TestMethod]
    public void ChatStageHasNoOnboardingFooter()
    {
        string shell = ReadAppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");
        string chat = ReadAppFile("Features", "GgufRuntime", "ChatPage.xaml");

        StringAssert.Contains(shell, "value == OnboardingStage.ReadyToChat");
        StringAssert.Contains(shell, "? Visibility.Collapsed");
        Assert.IsFalse(chat.Contains("OnboardingStageIndicator", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RecommendedModelDownloadActionIsFunctionallyWired()
    {
        var failures = new List<string>();
        string xamlPath = AppFile(
            "Features", "ModelImport", "ModelDownload", "ModelDownloadCard.xaml");
        XElement button = XDocument.Load(xamlPath)
            .Descendants()
            .Single(element =>
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name"
                    && attribute.Value == "DownloadModelButton"));

        if (!button.Attributes().Any(attribute =>
                attribute.Name.LocalName is "Click" or "Command"
                && !string.IsNullOrWhiteSpace(attribute.Value)))
        {
            failures.Add("the recommended-model action has no Click or Command binding");
        }

        string downloadRoot = AppFile(
            "Features", "ModelImport", "ModelDownload");
        string catalogPath = Path.Combine(downloadRoot, "PinnedGraniteModelCatalog.cs");
        string servicePath = Path.Combine(
            downloadRoot, "ResumableVerifiedModelDownloadService.cs");
        string coordinatorPath = Path.Combine(downloadRoot, "ModelDownloadCoordinator.cs");
        RequireFile(catalogPath, "the compiled pinned catalogue", failures);
        RequireFile(servicePath, "the resumable verified download service", failures);
        RequireFile(coordinatorPath, "the cancellation/restart coordinator", failures);

        if (File.Exists(catalogPath))
        {
            string catalog = File.ReadAllText(catalogPath);
            string[] exactPins =
            [
                "ibm-granite/granite-4.0-h-micro-GGUF",
                "51ce07a9c9cfa971ca359d9625836bf8a4a1b61f",
                "granite-4.0-h-micro-Q2_K.gguf", "1_226_247_840", "e60b313fc0ce2a0a2c3903f4399ac61fe1048c38f219bcbd9f7a708e168b8ead",
                "granite-4.0-h-micro-Q3_K_M.gguf", "1_555_472_032", "bb684177d546a2c6d3aec9cc591fc8fa75d9c16a27e798cbb2400fe52d9b7e29",
                "granite-4.0-h-micro-Q4_K_M.gguf", "1_942_564_512", "c698c78e895740f0e707eb7f8e92894f83f6d5b3f2f2f0b446dfe9635fa0063e",
                "granite-4.0-h-micro-Q5_K_M.gguf", "2_273_455_776", "69857575412143ea74d66e4d54ad70ec420d42452eadfff4a7cc043fe445ed4c",
                "granite-4.0-h-micro-Q8_0.gguf", "3_397_676_704", "a009111abf2865b7aad1e66326a6c772cddc29bccd22898f470292068b27bb59"
            ];
            foreach (string pin in exactPins)
            {
                if (!catalog.Contains(pin, StringComparison.Ordinal))
                    failures.Add($"catalogue pin missing: {pin}");
            }
        }

        if (File.Exists(servicePath))
        {
            string service = File.ReadAllText(servicePath);
            foreach (string token in new[]
            {
                "CancellationToken", "Content-Range", "ExpectedByteLength",
                "ExpectedSha256", "Publish", "Recover", "Resume"
            })
            {
                if (!service.Contains(token, StringComparison.Ordinal))
                    failures.Add($"download transaction token missing: {token}");
            }
        }

        string importPage = ReadAppFile(
            "Features", "ModelImport", "ModelImportPage.xaml.cs");
        if (!importPage.Contains("SubmitInputAsync", StringComparison.Ordinal))
            failures.Add("verified completion does not converge on SubmitInputAsync");

        Assert.AreEqual(0, failures.Count,
            "Download composition fitness gaps: " + string.Join("; ", failures));
    }

    [TestMethod]
    public void OpenVinoChatConsumesExactResultBoundConfiguration()
    {
        var failures = new List<string>();
        string shell = ReadAppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");
        string method = MethodBody(shell,
            "private async Task LaunchOptimizedChatAsync");
        string export = MethodBody(shell,
            "private async Task SaveOptimizedModelAsync");
        string selection = ReadAppFile(
            "Features", "ModelImport", "ModelImportPage.Selection.cs");

        RequireToken(method, "openVinoResult.ConfigurationSha256",
            "Chat does not bind the result configuration", failures);
        RequireNormalizedToken(method,
            "CreateChatTargetAsync(state.Result,CancellationToken.None)",
            "Chat lacks CreateChatTargetAsync(state.Result, token)", failures);
        RejectToken(method, "LastPublishedDirectory",
            "Chat still trusts LastPublishedDirectory", failures);
        RequireNormalizedToken(export,
            "ExportPersistentAsync(state.Result,destination,maximumBytes,CancellationToken.None)",
            "export lacks ExportPersistentAsync(state.Result, destination, maximumBytes, token)",
            failures);
        RejectToken(export, "LastPublishedDirectory",
            "export still trusts LastPublishedDirectory", failures);
        RequireToken(selection, "SourceModelConversionRequested",
            "source-model selection has no conversion intent", failures);
        RejectToken(selection,
            "SourceModelConversionRequested?.Invoke(this, new OpenVinoInspectionRequestedEventArgs",
            "source conversion bypasses conversion as an inspection request", failures);
        RequireToken(selection, "ConvertAndInspectAsync",
            "source conversion intent has no real conversion-and-inspection composition",
            failures);

        Assert.AreEqual(0, failures.Count,
            "Exact result/source conversion composition gaps: "
            + string.Join("; ", failures));
    }

    [TestMethod]
    public void RuntimeOnlyOpenVinoResultCannotEnterModelFileExport()
    {
        string method = MethodBody(
            ReadAppFile("Features", "Onboarding", "OnboardingShellPage.xaml.cs"),
            "private async Task SaveOptimizedModelAsync");

        StringAssert.Contains(method,
            "Status: OptimizationExecutionStatus.SucceededPersistent");
        Assert.IsFalse(method.Contains(
            "OptimizationExecutionStatus.SucceededRuntimeProfile",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void LightShellAndChatComposerKeepKeyboardAndEnabledActionGuards()
    {
        string chat = ReadAppFile("Features", "GgufRuntime", "ChatPage.xaml");
        string composer = ReadAppFile(
            "Features", "GgufRuntime", "Controls", "ChatComposer.xaml.cs");
        string shell = ReadAppFile(
            "Features", "Onboarding", "OnboardingShellPage.xaml.cs");

        StringAssert.Contains(chat, "RequestedTheme=\"Light\"");
        StringAssert.Contains(chat, "x:Name=\"SettingsFooter\"");
        StringAssert.Contains(shell, "OnboardingStage.ReadyToChat");
        StringAssert.Contains(shell, "Visibility.Collapsed");
        StringAssert.Contains(composer,
            "key == VirtualKey.Enter && !isShiftPressed");
        StringAssert.Contains(composer,
            "if (IsGenerating || prompt.Length == 0)");
        StringAssert.Contains(composer,
            "SendButton.IsEnabled = !IsGenerating");
    }

    private static string MethodBody(string source, string start)
    {
        int startIndex = source.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            Assert.Fail($"Method marker not found: {start}");
        }
        int endIndex = source.IndexOf("\n        private ", startIndex + start.Length,
            StringComparison.Ordinal);
        if (endIndex <= startIndex)
        {
            Assert.Fail($"Method boundary not found after: {start}");
        }
        return source[startIndex..endIndex];
    }

    private static int Count(string value, string fragment)
    {
        int count = 0;
        int position = 0;
        while ((position = value.IndexOf(fragment, position,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += fragment.Length;
        }
        return count;
    }

    private static void RequireFile(
        string path,
        string description,
        ICollection<string> failures)
    {
        if (!File.Exists(path))
            failures.Add($"{description} is absent");
    }

    private static void RequireToken(
        string source,
        string token,
        string failure,
        ICollection<string> failures)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static void RejectToken(
        string source,
        string token,
        string failure,
        ICollection<string> failures)
    {
        if (source.Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static void RequireNormalizedToken(
        string source,
        string token,
        string failure,
        ICollection<string> failures)
    {
        string normalized = string.Concat(source.Where(
            character => !char.IsWhiteSpace(character)));
        if (!normalized.Contains(token, StringComparison.Ordinal))
            failures.Add(failure);
    }

    private static string ReadAppFile(params string[] segments) =>
        File.ReadAllText(AppFile(segments));

    private static string AppFile(params string[] segments) =>
        Path.Combine([RepositoryRoot(), "IBM Granite with TurboQuant (Intel)", .. segments]);

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
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
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
