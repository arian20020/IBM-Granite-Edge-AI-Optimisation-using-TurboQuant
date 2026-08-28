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
        string xamlPath = AppFile(
            "Features", "ModelImport", "ModelDownload", "ModelDownloadCard.xaml");
        XElement button = XDocument.Load(xamlPath)
            .Descendants()
            .Single(element =>
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name"
                    && attribute.Value == "DownloadModelButton"));

        Assert.IsTrue(button.Attributes().Any(attribute =>
                attribute.Name.LocalName is "Click" or "Command"
                && !string.IsNullOrWhiteSpace(attribute.Value)),
            "The recommended-model download action must be functionally wired.");
    }

    [TestMethod]
    public void OpenVinoChatConsumesExactResultBoundConfiguration()
    {
        string method = MethodBody(
            ReadAppFile("Features", "Onboarding", "OnboardingShellPage.xaml.cs"),
            "private async Task LaunchOptimizedChatAsync");

        StringAssert.Contains(method, "openVinoResult.ConfigurationSha256");
        Assert.IsFalse(method.Contains("LastPublishedDirectory", StringComparison.Ordinal));
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
