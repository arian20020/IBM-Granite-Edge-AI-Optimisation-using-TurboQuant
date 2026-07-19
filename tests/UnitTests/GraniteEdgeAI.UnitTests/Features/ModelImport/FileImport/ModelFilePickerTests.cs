namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Contains tests for the model-format and GGUF file-selection workflow.
/// 
/// Each test will be implemented and reviewed separately.
/// </summary>
[TestClass]
public sealed class ModelFilePickerTests
{
    /// <summary>
    /// Verifies that the model-import page begins with no selected model
    /// and that the Continue to model inspection button is disabled.
    /// </summary>
    [TestMethod]
    public void InitialState_HasNoSelectedModelAndContinueIsDisabled()
    {
        // This test will be implemented separately.
        Assert.Inconclusive("Test not implemented yet.");
    }

    /// <summary>
    /// Verifies that clicking Browse files requests the
    /// model-format selection dialog.
    /// </summary>
    [TestMethod]
    public void BrowseFiles_Click_OpensModelFormatSelectionDialog()
    {
        // This test will be implemented separately.
        Assert.Inconclusive("Test not implemented yet.");
    }

    /// <summary>
    /// Verifies that cancelling the format-selection dialog closes it
    /// without changing the model-import page state.
    /// </summary>
    [TestMethod]
    public void CancelFormatSelection_LeavesModelImportStateUnchanged()
    {
        // This test will be implemented separately.
        Assert.Inconclusive("Test not implemented yet.");
    }

    /// <summary>
    /// Verifies that choosing the GGUF option records GGUF as the
    /// selected model format.
    /// </summary>
    [TestMethod]
    public void SelectGguf_RecordsGgufAsSelectedFormat()
    {
        // This test will be implemented separately.
        Assert.Inconclusive("Test not implemented yet.");
    }

    /// <summary>
    /// Verifies that cancelling the Windows GGUF picker returns no file
    /// and keeps Continue to model inspection disabled.
    /// </summary>
    [TestMethod]
    public void CancelGgufPicker_ReturnsNoFileAndKeepsContinueDisabled()
    {
        // This test will be implemented separately.
        Assert.Inconclusive("Test not implemented yet.");
    }

    /// <summary>
    /// Verifies that selecting a GGUF file returns the same file that
    /// was selected, while model validation remains a later stage.
    /// </summary>
    [TestMethod]
    public void SelectGgufFile_ReturnsSelectedFileAndKeepsContinueDisabled()
    {
        // This test will be implemented separately.
        Assert.Inconclusive("Test not implemented yet.");
    }

    /// <summary>
    /// Verifies that the GGUF file picker accepts only files using
    /// the .gguf extension.
    /// </summary>
    [TestMethod]
    public void GgufPicker_AllowsOnlyGgufFiles()
    {
        // This test will be implemented separately.
        Assert.Inconclusive("Test not implemented yet.");
    }
}
