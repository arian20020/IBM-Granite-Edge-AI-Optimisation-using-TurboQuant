using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    /// <summary>
    /// Identifies the model format selected by the user.
    /// </summary>
    public enum ModelFormatSelection
    {
        // No format was selected, normally because the user cancelled.
        None,

        // The user selected the GGUF and llama.cpp workflow.
        Gguf,

        // The user selected the OpenVINO workflow.
        OpenVino
    }

    /// <summary>
    /// Provides the model-format choice without exposing WinUI controls
    /// to the workflow or its unit tests.
    /// </summary>
    public interface IModelFormatSelectionService
    {
        /// <summary>
        /// Displays or simulates the format choice and returns the result.
        /// </summary>
        Task<ModelFormatSelection> SelectFormatAsync();
    }

    /// <summary>
    /// Provides GGUF file selection without coupling the workflow to the
    /// Windows file-picker implementation.
    /// </summary>
    public interface IGgufModelFilePicker
    {
        /// <summary>
        /// Returns the selected GGUF path, or null when selection is cancelled.
        /// </summary>
        Task<string?> PickGgufAsync();
    }

    /// <summary>
    /// Stores the file extensions supported by the GGUF file picker.
    /// </summary>
    public static class GgufPickerConfiguration
    {
        // Keep this list in pure .NET code so it can be checked without
        // constructing a Windows FileOpenPicker during a unit test.
        public static IReadOnlyList<string> AllowedFileExtensions { get; }
            = new[] { ".gguf" };
    }

    /// <summary>
    /// Coordinates format selection and GGUF file selection while keeping
    /// WinUI and Windows picker details outside the testable workflow.
    /// </summary>
    public sealed class ModelImportWorkflow
    {
        // Supplies the user's model-format choice.
        private readonly IModelFormatSelectionService _formatSelectionService;

        // Supplies the selected GGUF path when the GGUF route is chosen.
        private readonly IGgufModelFilePicker _ggufModelFilePicker;

        /// <summary>
        /// Creates the workflow with replaceable dialog and picker dependencies.
        /// </summary>
        public ModelImportWorkflow(
            IModelFormatSelectionService formatSelectionService,
            IGgufModelFilePicker ggufModelFilePicker)
        {
            // Reject missing dependencies immediately so failures are clear.
            _formatSelectionService = formatSelectionService
                ?? throw new System.ArgumentNullException(nameof(formatSelectionService));

            _ggufModelFilePicker = ggufModelFilePicker
                ?? throw new System.ArgumentNullException(nameof(ggufModelFilePicker));
        }

        /// <summary>
        /// Gets the format selected during the latest completed workflow.
        /// </summary>
        public ModelFormatSelection SelectedFormat { get; private set; }
            = ModelFormatSelection.None;

        /// <summary>
        /// Gets the selected model path, or null when no file was selected.
        /// </summary>
        public string? SelectedModelPath { get; private set; }

        /// <summary>
        /// Remains false until the later model-validation stage proves that
        /// the selected model is ready for inspection.
        /// </summary>
        public bool CanContinueToModelInspection => false;

        /// <summary>
        /// Starts the format-selection workflow and, for GGUF, requests one
        /// model file from the configured picker.
        /// </summary>
        public async Task StartFileSelectionAsync()
        {
            // Ask which import route the user wants to use.
            ModelFormatSelection selectedFormat =
                await _formatSelectionService.SelectFormatAsync();

            // Cancelling the format dialog must leave all workflow state unchanged.
            if (selectedFormat == ModelFormatSelection.None)
            {
                return;
            }

            // Retain the chosen route for the page and later workflow stages.
            SelectedFormat = selectedFormat;

            // OpenVINO selection is deliberately retained but not processed yet.
            if (selectedFormat != ModelFormatSelection.Gguf)
            {
                return;
            }

            // Ask the Windows picker adapter for one GGUF model path.
            string? selectedPath = await _ggufModelFilePicker.PickGgufAsync();

            // Cancelling the Windows picker must not create a model selection.
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            // Retain the path only; GGUF validation and model loading happen later.
            SelectedModelPath = selectedPath;
        }
    }
}
