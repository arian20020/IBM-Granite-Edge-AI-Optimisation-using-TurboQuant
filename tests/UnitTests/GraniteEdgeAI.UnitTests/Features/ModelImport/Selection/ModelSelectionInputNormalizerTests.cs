using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelSelectionInputNormalizerTests
{
    [TestMethod]
    public void FromPickerPath_File_ProducesOneNonFolderInputWithSafeBaseName()
    {
        var normalizer = new ModelSelectionInputNormalizer();

        ModelSelectionInput input = normalizer.FromPickerPath(
            @"C:\Models\granite.gguf",
            isFolder: false);

        Assert.AreEqual(@"C:\Models\granite.gguf", input.LocalPath);
        Assert.AreEqual("granite.gguf", input.DisplayName);
        Assert.IsFalse(input.IsFolder);
    }

    [TestMethod]
    public void FromPickerPath_FolderWithTrailingSeparator_ProducesFolderInputWithSafeBaseName()
    {
        var normalizer = new ModelSelectionInputNormalizer();

        ModelSelectionInput input = normalizer.FromPickerPath(
            @"C:\Models\OpenVINO\",
            isFolder: true);

        Assert.AreEqual(@"C:\Models\OpenVINO\", input.LocalPath);
        Assert.AreEqual("OpenVINO", input.DisplayName);
        Assert.IsTrue(input.IsFolder);
    }

    [TestMethod]
    public void FromPickerPath_RootWithoutAnItemName_IsRejected()
    {
        var normalizer = new ModelSelectionInputNormalizer();

        Assert.ThrowsExactly<ArgumentException>(() =>
            normalizer.FromPickerPath(@"C:\", isFolder: true));
    }

    [TestMethod]
    public async Task PickerCancellation_ReturnsNoNormalizedInput()
    {
        var normalizer = new ModelSelectionInputNormalizer();
        var filePicker = new GgufModelFilePicker(
            () => Task.FromResult<Microsoft.Windows.Storage.Pickers.PickFileResult?>(null));
        var folderPicker = new OpenVINOFolderPicker(
            () => Task.FromResult<Microsoft.Windows.Storage.Pickers.PickFolderResult?>(null));

        ModelSelectionInput? fileInput = await filePicker.PickInputAsync(normalizer);
        ModelSelectionInput? folderInput = await folderPicker.PickInputAsync(normalizer);

        Assert.IsNull(fileInput);
        Assert.IsNull(folderInput);
    }
}
