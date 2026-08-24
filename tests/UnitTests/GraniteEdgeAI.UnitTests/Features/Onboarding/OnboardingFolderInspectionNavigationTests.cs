using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
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
public sealed class OnboardingFolderInspectionNavigationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoFolderContinue_FailsClosedWhenNoO1InspectionPortExists()
    {
        var shell = new OnboardingShellPage();
        var page = CreateFolderPage(ModelSelectionRoute.OpenVinoDirectory);
        shell.AttachModelImportPage(page);

        await page.SubmitInputAsync(new ModelSelectionInput(
            @"C:\Models\private-openvino-package",
            "private-openvino-package",
            isFolder: true));

        Assert.IsFalse(page.TryRequestModelInspection());

        var frame = (Frame)shell.FindName("StageFrame");
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.IsFalse(page.HasValidatedModel);

        Assert.IsFalse(page.TryRequestModelInspection());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SourceFolderContinue_DispatchesConversionRequiredIntentWithoutNavigation()
    {
        var shell = new OnboardingShellPage();
        var page = CreateFolderPage(ModelSelectionRoute.SourceModelDirectory);
        shell.AttachModelImportPage(page);
        SourceModelConversionRequestedEventArgs? received = null;
        shell.SourceModelConversionRequested += (_, eventArguments) =>
            received = eventArguments;

        await page.SubmitInputAsync(new ModelSelectionInput(
            @"C:\Models\private-source-package",
            "private-source-package",
            isFolder: true));

        Assert.IsTrue(page.TryRequestModelInspection());

        var frame = (Frame)shell.FindName("StageFrame");
        Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
        Assert.IsNotNull(received);
        Assert.AreEqual("private-source-package", received.Selection.DisplayName);
        Assert.IsFalse(typeof(SourceModelConversionRequestedEventArgs).GetProperties()
            .Any(property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)));
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

    private sealed class FolderClassifier(ModelSelectionRoute route) : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId id,
            ModelSelectionInput input,
            CancellationToken cancellationToken) =>
            Task.FromResult(ModelSelectionResult.Accepted(id, route, input.DisplayName));
    }
}
