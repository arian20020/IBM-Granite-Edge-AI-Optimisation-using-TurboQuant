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
    [DataRow("functional")]
    [DataRow("cancellable")]
    [DataRow("bounded")]
    [DataRow("integrity-checked")]
    [DataRow("retryable")]
    public void RecommendedModelDownloadImplementsRequiredLifecycle(string requirement)
    {
        string xamlPath = AppFile(
            "Features", "ModelImport", "ModelDownload", "ModelDownloadCard.xaml");
        string code = ReadAppFile(
            "Features", "ModelImport", "ModelDownload", "ModelDownloadCard.xaml.cs");
        XElement button = XDocument.Load(xamlPath)
            .Descendants()
            .Single(element =>
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name"
                    && attribute.Value == "DownloadModelButton"));

        bool established = requirement switch
        {
            "functional" => button.Attributes().Any(attribute =>
                attribute.Name.LocalName is "Click" or "Command"
                && !string.IsNullOrWhiteSpace(attribute.Value)),
            "cancellable" => code.Contains("CancellationToken", StringComparison.Ordinal),
            "bounded" => code.Contains("ContentLength", StringComparison.Ordinal)
                || code.Contains("MaximumDownload", StringComparison.Ordinal),
            "integrity-checked" => code.Contains("SHA256", StringComparison.Ordinal),
            "retryable" => code.Contains("Retry", StringComparison.Ordinal),
            _ => throw new AssertInconclusiveException("Unknown requirement fixture.")
        };

        Assert.IsTrue(established,
            $"The recommended-model download must be {requirement}.");
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

    [TestMethod]
    [DataRow("bounded")]
    [DataRow("integrity-checked")]
    [DataRow("cancellable-and-cleaned")]
    public void PersistentExportVerifiesExactOutputAndLifecycle(string requirement)
    {
        string method = MethodBody(
            ReadAppFile("Features", "Onboarding", "OnboardingShellPage.xaml.cs"),
            "private async Task SaveOptimizedModelAsync");
        bool established = requirement switch
        {
            "bounded" => method.Contains("OutputSizeBytes", StringComparison.Ordinal),
            "integrity-checked" => method.Contains("OutputManifestSha256", StringComparison.Ordinal)
                && method.Contains("SHA256", StringComparison.Ordinal),
            "cancellable-and-cleaned" => method.Contains("CancellationToken", StringComparison.Ordinal)
                && method.Contains("Delete", StringComparison.Ordinal),
            _ => throw new AssertInconclusiveException("Unknown requirement fixture.")
        };

        Assert.IsTrue(established,
            $"Persistent export must be {requirement} against the verified result.");
    }

    [TestMethod]
    public void HardwareProbePackagingDoesNotLeakAnUnresolvedPackageItemExpression()
    {
        XDocument target = XDocument.Load(AppFile(
            "HardwareInspection.LlamaCppProbePackaging.targets"));
        string[] unresolvedPackageItems = target
            .Descendants()
            .Where(element => element.Name.LocalName == "Content")
            .SelectMany(element => element.Attributes()
                .Where(attribute => attribute.Name.LocalName == "Include"))
            .Select(attribute => attribute.Value)
            .Where(value => value.Contains("@(", StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(0, unresolvedPackageItems.Length,
            "Package-relevant Content items must be expanded to concrete files.");
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
