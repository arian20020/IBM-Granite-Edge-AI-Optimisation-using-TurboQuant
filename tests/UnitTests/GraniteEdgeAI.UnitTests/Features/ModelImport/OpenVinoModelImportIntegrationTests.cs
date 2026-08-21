using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
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
            page.OpenVinoInspectionRequested += (_, eventArguments) => request = eventArguments;

            await page.SubmitInputAsync(new ModelSelectionInput(
                directory,
                "OpenVINO package",
                isFolder: true));

            Assert.IsNull(request, "selection must not navigate before Continue");
            InvokeButton((Button)page.FindName("ContinueToModelInspectionButton"));
            Assert.IsNotNull(request);
            string? carriedDirectory = request.GetType()
                .GetProperty(
                    "DirectoryPath",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)?
                .GetValue(request) as string;
            Assert.AreEqual(directory, carriedDirectory);
            Assert.IsNull(page.SelectedModelPath,
                "the directory must not enter the GGUF file-path property");
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
        string directory = Path.Combine(Path.GetTempPath(), $"ov-onboard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            OnboardingShellPage shell = new();
            ModelImportPage page = CreatePage(ModelSelectionRoute.OpenVinoDirectory);
            shell.AttachModelImportPage(page);
            await page.SubmitInputAsync(new ModelSelectionInput(
                directory,
                "OpenVINO package",
                isFolder: true));

            InvokeButton((Button)page.FindName("ContinueToModelInspectionButton"));

            Frame frame = (Frame)shell.FindName("StageFrame");
            ModelInspectionPage inspectionPage =
                Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content);
            Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
            if (inspectionPage.CurrentOpenVinoInspectionTask is Task inspection)
            {
                await inspection;
            }
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
            InvokeButton((Button)page.FindName("ContinueToModelInspectionButton"));

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

    private static void InvokeButton(Button button)
    {
        ButtonAutomationPeer peer = new(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
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
