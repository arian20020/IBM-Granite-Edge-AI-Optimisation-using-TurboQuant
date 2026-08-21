using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelFilePickerTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void InitialState_HasNoSelectedModelAndContinueIsDisabled()
    {
        var page = new ModelImportPage();
        var continueButton = (Button)page.FindName(
            "ContinueToModelInspectionButton");

        Assert.IsNull(page.SelectedModelPath);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(continueButton.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task BrowseFiles_Click_OpensModelFormatSelectionDialog()
    {
        var formatInteractionCount = 0;
        var page = new ModelImportPage(
            () =>
            {
                formatInteractionCount++;
                return Task.FromResult(ModelFormatSelection.None);
            },
            () => Task.FromResult<string?>(null));

        await page.BrowseFilesAsync();

        Assert.AreEqual(1, formatInteractionCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CancelFormatSelection_LeavesModelImportStateUnchanged()
    {
        var pickerInteractionCount = 0;
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.None),
            () =>
            {
                pickerInteractionCount++;
                return Task.FromResult<string?>(null);
            });

        await page.BrowseFilesAsync();

        Assert.AreEqual(0, pickerInteractionCount);
        Assert.IsNull(page.SelectedModelPath);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(GetContinueButton(page).IsEnabled);
        Assert.IsInstanceOfType<ModelImportPage>(page);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SelectGguf_RecordsGgufAsSelectedFormat()
    {
        var dialog = new ModelFormatSelectionCard();

        dialog.SelectFormat(ModelFormatSelection.Gguf);

        Assert.AreEqual(ModelFormatSelection.Gguf, dialog.SelectedFormat);
        Assert.AreNotEqual(ModelFormatSelection.OpenVino, dialog.SelectedFormat);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CancelGgufPicker_ReturnsNoFileAndKeepsContinueDisabled()
    {
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(null));

        await page.BrowseFilesAsync();

        Assert.IsNull(page.SelectedModelPath);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(GetContinueButton(page).IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SelectGgufFile_ReturnsSelectedFileAndKeepsContinueDisabled()
    {
        const string selectedPath = @"C:\Models\Granite 3.3.gguf";
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath));

        await page.BrowseFilesAsync();

        Assert.AreEqual(selectedPath, page.SelectedModelPath);
        Assert.IsFalse(page.HasValidatedModel);
        Assert.IsFalse(GetContinueButton(page).IsEnabled);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GgufPicker_AllowsOnlyGgufFiles()
    {
        CollectionAssert.AreEqual(
            new[] { ".gguf" },
            GgufModelFilePicker.AllowedFileTypes.ToArray());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SelectGgufFile_AfterPreviousSelection_ReplacesPath()
    {
        var selectedPath = @"C:\Models\first.gguf";
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.Gguf),
            () => Task.FromResult<string?>(selectedPath));

        await page.BrowseFilesAsync();
        selectedPath = @"C:\Models\second.gguf";
        await page.BrowseFilesAsync();

        Assert.AreEqual(@"C:\Models\second.gguf", page.SelectedModelPath);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OpenVinoSelection_UsesFolderPickerAndNormalSelectionRoute()
    {
        const string selectedDirectory = @"C:\Models\OpenVINO Granite";
        var ggufPickerInteractionCount = 0;
        var openVinoPickerInteractionCount = 0;
        var page = new ModelImportPage(
            () => Task.FromResult(ModelFormatSelection.OpenVino),
            () =>
            {
                ggufPickerInteractionCount++;
                return Task.FromResult<string?>(null);
            },
            classifier: new AcceptingOpenVinoClassifier(),
            pickOpenVinoPathAsync: () =>
            {
                openVinoPickerInteractionCount++;
                return Task.FromResult<string?>(selectedDirectory);
            });

        await page.BrowseFilesAsync();

        Assert.AreEqual(0, ggufPickerInteractionCount);
        Assert.AreEqual(1, openVinoPickerInteractionCount);
        Assert.IsNull(page.SelectedModelPath);
        Assert.AreEqual(ModelSelectionRoute.OpenVinoDirectory, page.CurrentRoute);
        Assert.IsTrue(page.HasValidatedModel);
        Assert.IsTrue(GetContinueButton(page).IsEnabled);
    }

    private static Button GetContinueButton(ModelImportPage page)
    {
        return (Button)page.FindName("ContinueToModelInspectionButton");
    }

    private sealed class AcceptingOpenVinoClassifier : IModelSelectionClassifier
    {
        public Task<ModelSelectionResult> ClassifyAsync(
            ModelSelectionOperationId operationId,
            ModelSelectionInput input,
            CancellationToken cancellationToken)
        {
            Assert.AreEqual(@"C:\Models\OpenVINO Granite", input.LocalPath);
            Assert.AreEqual("OpenVINO Granite", input.DisplayName);
            Assert.IsTrue(input.IsFolder);
            return Task.FromResult(ModelSelectionResult.Accepted(
                operationId,
                ModelSelectionRoute.OpenVinoDirectory,
                input.DisplayName));
        }
    }
}
