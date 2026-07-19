using GraniteEdgeAI.Features.ModelImport.FileImport;

namespace GraniteEdgeAI.UnitTests.Features.ModelImport.FileImport;

/// <summary>
/// Verifies the model-format and GGUF file-selection workflow without
/// opening real WinUI dialogs or Windows file pickers.
/// </summary>
[TestClass]
public sealed class ModelFilePickerTests
{
    /// <summary>
    /// Verifies the safe state before the user starts model selection.
    /// </summary>
    [TestMethod]
    public void InitialState_HasNoSelectionAndContinueIsDisabled()
    {
        // Arrange: create dependencies that would cancel if invoked.
        StubModelFormatSelectionService formatService =
            new StubModelFormatSelectionService(ModelFormatSelection.None);

        StubGgufModelFilePicker ggufPicker =
            new StubGgufModelFilePicker(null);

        // Act: create the workflow without starting user interaction.
        ModelImportWorkflow workflow =
            new ModelImportWorkflow(formatService, ggufPicker);

        // Assert: no route, path or validated model is available initially.
        Assert.AreEqual(ModelFormatSelection.None, workflow.SelectedFormat);
        Assert.IsNull(workflow.SelectedModelPath);
        Assert.IsFalse(workflow.CanContinueToModelInspection);
    }

    /// <summary>
    /// Verifies that starting Browse files requests the format dialog once.
    /// </summary>
    [TestMethod]
    public async Task BrowseFiles_RequestsFormatSelectionDialogOnce()
    {
        // Arrange: the simulated format dialog will be cancelled.
        StubModelFormatSelectionService formatService =
            new StubModelFormatSelectionService(ModelFormatSelection.None);

        StubGgufModelFilePicker ggufPicker =
            new StubGgufModelFilePicker(null);

        ModelImportWorkflow workflow =
            new ModelImportWorkflow(formatService, ggufPicker);

        // Act: start the same workflow used by the Browse files handler.
        await workflow.StartFileSelectionAsync();

        // Assert: the format-selection dependency was requested exactly once.
        Assert.AreEqual(1, formatService.CallCount);
    }

    /// <summary>
    /// Verifies that cancelling the format dialog changes no import state.
    /// </summary>
    [TestMethod]
    public async Task CancelFormatSelection_LeavesImportStateUnchanged()
    {
        // Arrange: simulate the user pressing Cancel in the format dialog.
        StubModelFormatSelectionService formatService =
            new StubModelFormatSelectionService(ModelFormatSelection.None);

        StubGgufModelFilePicker ggufPicker =
            new StubGgufModelFilePicker(@"C:\Models\unused.gguf");

        ModelImportWorkflow workflow =
            new ModelImportWorkflow(formatService, ggufPicker);

        // Act: run the selection workflow.
        await workflow.StartFileSelectionAsync();

        // Assert: cancellation records nothing and never opens the GGUF picker.
        Assert.AreEqual(ModelFormatSelection.None, workflow.SelectedFormat);
        Assert.IsNull(workflow.SelectedModelPath);
        Assert.IsFalse(workflow.CanContinueToModelInspection);
        Assert.AreEqual(0, ggufPicker.CallCount);
    }

    /// <summary>
    /// Verifies that choosing GGUF records the route and requests its picker.
    /// </summary>
    [TestMethod]
    public async Task SelectGguf_SetsFormatAndRequestsGgufPickerOnce()
    {
        // Arrange: simulate choosing GGUF, followed by cancelling its picker.
        StubModelFormatSelectionService formatService =
            new StubModelFormatSelectionService(ModelFormatSelection.Gguf);

        StubGgufModelFilePicker ggufPicker =
            new StubGgufModelFilePicker(null);

        ModelImportWorkflow workflow =
            new ModelImportWorkflow(formatService, ggufPicker);

        // Act: run the format and file-selection workflow.
        await workflow.StartFileSelectionAsync();

        // Assert: the correct route was recorded and its picker ran once.
        Assert.AreEqual(ModelFormatSelection.Gguf, workflow.SelectedFormat);
        Assert.AreEqual(1, ggufPicker.CallCount);
    }

    /// <summary>
    /// Verifies that cancelling the Windows picker creates no model path.
    /// </summary>
    [TestMethod]
    public async Task CancelGgufPicker_LeavesPathEmptyAndContinueDisabled()
    {
        // Arrange: choose GGUF but return null from the simulated file picker.
        StubModelFormatSelectionService formatService =
            new StubModelFormatSelectionService(ModelFormatSelection.Gguf);

        StubGgufModelFilePicker ggufPicker =
            new StubGgufModelFilePicker(null);

        ModelImportWorkflow workflow =
            new ModelImportWorkflow(formatService, ggufPicker);

        // Act: run the workflow through the cancelled Windows picker.
        await workflow.StartFileSelectionAsync();

        // Assert: the route is known, but no selected or validated model exists.
        Assert.AreEqual(ModelFormatSelection.Gguf, workflow.SelectedFormat);
        Assert.IsNull(workflow.SelectedModelPath);
        Assert.IsFalse(workflow.CanContinueToModelInspection);
    }

    /// <summary>
    /// Verifies that selecting a file retains its path but does not validate it.
    /// </summary>
    [TestMethod]
    public async Task SelectGgufFile_StoresPathButKeepsContinueDisabled()
    {
        // Arrange: configure the simulated picker to return a known GGUF path.
        const string expectedPath = @"C:\Models\granite-3b.gguf";

        StubModelFormatSelectionService formatService =
            new StubModelFormatSelectionService(ModelFormatSelection.Gguf);

        StubGgufModelFilePicker ggufPicker =
            new StubGgufModelFilePicker(expectedPath);

        ModelImportWorkflow workflow =
            new ModelImportWorkflow(formatService, ggufPicker);

        // Act: complete the format and file-selection stages.
        await workflow.StartFileSelectionAsync();

        // Assert: the path is retained, but validation has not happened yet.
        Assert.AreEqual(ModelFormatSelection.Gguf, workflow.SelectedFormat);
        Assert.AreEqual(expectedPath, workflow.SelectedModelPath);
        Assert.IsFalse(workflow.CanContinueToModelInspection);
    }

    /// <summary>
    /// Verifies that the GGUF picker configuration exposes no other extensions.
    /// </summary>
    [TestMethod]
    public void GgufPickerConfiguration_AllowsOnlyGgufExtension()
    {
        // Act: read the extension list used by the real Windows picker adapter.
        IReadOnlyList<string> allowedExtensions =
            GgufPickerConfiguration.AllowedFileExtensions;

        // Assert: exactly one lowercase GGUF extension is configured.
        Assert.AreEqual(1, allowedExtensions.Count);
        Assert.AreEqual(".gguf", allowedExtensions[0]);
    }

    /// <summary>
    /// Simulates the format dialog and records how often it was requested.
    /// </summary>
    private sealed class StubModelFormatSelectionService
        : IModelFormatSelectionService
    {
        // Stores the format that this deterministic test double will return.
        private readonly ModelFormatSelection _result;

        public StubModelFormatSelectionService(ModelFormatSelection result)
        {
            // Retain the result configured by the individual test.
            _result = result;
        }

        // Exposes the interaction count for workflow assertions.
        public int CallCount { get; private set; }

        public Task<ModelFormatSelection> SelectFormatAsync()
        {
            // Record one simulated dialog request.
            CallCount++;

            // Return immediately without creating a real ContentDialog.
            return Task.FromResult(_result);
        }
    }

    /// <summary>
    /// Simulates the GGUF picker and records how often it was requested.
    /// </summary>
    private sealed class StubGgufModelFilePicker : IGgufModelFilePicker
    {
        // Stores the path or cancellation result configured by the test.
        private readonly string? _result;

        public StubGgufModelFilePicker(string? result)
        {
            // Retain the deterministic result for this test run.
            _result = result;
        }

        // Exposes the interaction count for workflow assertions.
        public int CallCount { get; private set; }

        public Task<string?> PickGgufAsync()
        {
            // Record one simulated Windows picker request.
            CallCount++;

            // Return immediately without opening operating-system UI.
            return Task.FromResult(_result);
        }
    }
}
