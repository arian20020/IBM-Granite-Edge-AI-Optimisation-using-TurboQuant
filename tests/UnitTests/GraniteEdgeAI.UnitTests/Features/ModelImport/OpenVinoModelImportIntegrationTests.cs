using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class OpenVinoModelImportIntegrationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoDirectoryIsCarriedOnlyWhenContinueRequestsInspection()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ov-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            ModelImportPage page = CreatePage(ModelSelectionRoute.OpenVinoDirectory);
            object? request = null;
            int requestCount = 0;
            page.OpenVinoInspectionRequested += (_, eventArguments) =>
            {
                request = eventArguments;
                requestCount++;
            };

            await page.SubmitInputAsync(new ModelSelectionInput(
                directory,
                "OpenVINO package",
                isFolder: true));

            Assert.IsNull(request, "selection must not navigate before Continue");
            Assert.IsFalse(page.TryRequestModelInspection(),
                "without an accepting shell, the navigation request is not accepted");
            Assert.IsNotNull(request);
            Assert.IsNull(request.GetType().GetProperty(
                "DirectoryPath",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic),
                "the local directory must remain in page custody, not in the navigation payload");
            Assert.IsNull(page.SelectedModelPath,
                "the directory must not enter the GGUF file-path property");
            Assert.AreEqual(1, requestCount);
            Assert.IsFalse(page.TryRequestModelInspection(),
                "without an accepting shell, a repeated request is still not accepted");
            Assert.AreEqual(2, requestCount);
            Assert.AreEqual(ModelSelectionRoute.OpenVinoDirectory, page.CurrentRoute);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoContinueNavigatesOnboardingToTheExistingInspectionPage()
    {
        string directory = PackagedOpenVinoFixture();
        OnboardingShellPage shell = new();
        ModelImportPage page = CreatePage(ModelSelectionRoute.OpenVinoDirectory);
        OpenVinoInspectionRequestedEventArgs? request = null;
        page.OpenVinoInspectionRequested += (_, eventArguments) =>
            request = eventArguments;
        await page.SubmitInputAsync(new ModelSelectionInput(
            directory,
            "OpenVINO package",
            isFolder: true));

        Assert.IsFalse(
            page.TryRequestModelInspection(),
            "The request remains retryable until the shell accepts ownership.");
        Assert.IsNotNull(request);
        shell.AttachModelImportPage(page);
        Task<bool> navigation = shell.NavigateToOpenVinoInspectionAsync(request);

        Frame frame = (Frame)shell.FindName("StageFrame");
        ModelInspectionPage inspectionPage =
            Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content);
        await inspectionPage.RetireOpenVinoInspectionAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(await navigation.WaitAsync(TimeSpan.FromSeconds(5)),
            "A detached unit-test page cannot claim loaded Frame ownership.");
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task NavigationAwayRevokesUntransferredOpenVinoDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"ov-import-retire-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            ModelImportPage page = CreatePage(ModelSelectionRoute.OpenVinoDirectory);
            int requests = 0;
            page.OpenVinoInspectionRequested += (_, _) => requests++;
            await page.SubmitInputAsync(new ModelSelectionInput(
                directory,
                "OpenVINO package",
                isFolder: true));

            NavigationEventArgs? navigation = null;
            Frame frame = new();
            frame.Navigated += (_, eventArguments) => navigation = eventArguments;
            Assert.IsTrue(frame.Navigate(typeof(Page)));
            Assert.IsNotNull(navigation);
            typeof(ModelImportPage).GetMethod(
                "OnNavigatedFrom",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
                .Invoke(page, [navigation]);

            Assert.IsFalse(page.TryRequestModelInspection());
            Assert.AreEqual(0, requests);
            Assert.IsNull(page.CurrentRoute);
            Assert.IsFalse(page.HasValidatedModel);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task GgufSelectionStillUsesQuickScanAndGgufInspectionEvent()
    {
        string path = Path.Combine(Path.GetTempPath(), $"gguf-{Guid.NewGuid():N}.gguf");
        await File.WriteAllBytesAsync(path, new byte[64]);
        try
        {
            int scans = 0;
            ModelImportPage page = new(
                null,
                null,
                (_, _, _) =>
                {
                    scans++;
                    return Task.FromResult(ModelQuickScanResult.CreateSuccess(
                        "Granite", "granite", "3B", "Q4", 64, 4096, 3,
                        new DateTimeOffset(File.GetLastWriteTimeUtc(path))));
                },
                classifier: new StubClassifier(ModelSelectionRoute.Gguf));
            int ggufRequests = 0;
            int openVinoRequests = 0;
            page.ModelInspectionRequested += (_, _) => ggufRequests++;
            page.OpenVinoInspectionRequested += (_, _) => openVinoRequests++;

            await page.SubmitInputAsync(new ModelSelectionInput(
                path,
                Path.GetFileName(path),
                isFolder: false));
            Assert.IsTrue(page.TryRequestModelInspection());

            Assert.AreEqual(ModelSelectionRoute.Gguf, page.CurrentRoute);
            Assert.AreEqual(1, scans);
            Assert.AreEqual(1, ggufRequests);
            Assert.AreEqual(0, openVinoRequests);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ModelImportPage CreatePage(ModelSelectionRoute route) => new(
        null,
        null,
        (_, _, _) => throw new AssertFailedException("directory route must not quick-scan"),
        classifier: new StubClassifier(route));

    private static string PackagedOpenVinoFixture()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "OpenVINO",
            "GenAI",
            "TinySyntheticV1",
            "package");
        Assert.IsTrue(Directory.Exists(path));
        return path;
    }

    private sealed class StubClassifier(ModelSelectionRoute route) :
        IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId operationId,
            ModelSelectionInput input,
            CancellationToken cancellationToken) => Task.FromResult(
                ModelSelectionResult.Accepted(operationId, route, input.DisplayName));
    }
}
