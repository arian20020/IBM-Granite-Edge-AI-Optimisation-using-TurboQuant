using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class OnboardingFolderInspectionNavigationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoFolderContinue_NavigatesToIntegratedInspectionRoute()
    {
        string directory = PackagedOpenVinoFixture();
        var shell = new OnboardingShellPage();
        var page = CreateFolderPage(ModelSelectionRoute.OpenVinoDirectory);
        OpenVinoInspectionRequestedEventArgs? request = null;
        page.OpenVinoInspectionRequested += (_, eventArguments) =>
            request = eventArguments;

        await page.SubmitInputAsync(new ModelSelectionInput(
            directory,
            "private-openvino-package",
            isFolder: true));

        Assert.IsFalse(
            page.TryRequestModelInspection(),
            "The request remains retryable until the shell accepts ownership.");
        Assert.IsNotNull(request);
        shell.AttachModelImportPage(page);
        Task<bool> navigation = shell.NavigateToOpenVinoInspectionAsync(request);

        var frame = (Frame)shell.FindName("StageFrame");
        ModelInspectionPage inspectionPage =
            Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content);
        Assert.IsTrue(page.HasValidatedModel);
        await inspectionPage.RetireOpenVinoInspectionAsync()
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(await navigation.WaitAsync(TimeSpan.FromSeconds(5)),
            "A detached unit-test page cannot claim loaded Frame ownership.");
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FolderContinueWithoutAShellHandler_RemainsRetryable()
    {
        var page = CreateFolderPage(ModelSelectionRoute.OpenVinoDirectory);
        int requests = 0;
        page.OpenVinoInspectionRequested += (_, _) => requests++;

        await page.SubmitInputAsync(new ModelSelectionInput(
            @"C:\Models\private-openvino-package",
            "private-openvino-package",
            isFolder: true));

        Assert.IsFalse(page.TryRequestModelInspection());
        Assert.IsFalse(page.TryRequestModelInspection());
        Assert.AreEqual(2, requests);
    }

    private static ModelImportPage CreateFolderPage(ModelSelectionRoute route) => new(
        () => Task.FromResult(ModelFormatSelection.Gguf),
        () => Task.FromResult<string?>(null),
        (_, _, _) => Task.FromResult(ModelQuickScanResult.CreateCancelled()),
        classifier: new FolderClassifier(route));

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

    private sealed class FolderClassifier(ModelSelectionRoute route) : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId id,
            ModelSelectionInput input,
            CancellationToken cancellationToken) =>
            Task.FromResult(ModelSelectionResult.Accepted(id, route, input.DisplayName));
    }
}
