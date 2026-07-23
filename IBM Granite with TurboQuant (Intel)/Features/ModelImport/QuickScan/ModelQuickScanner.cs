using GraniteEdgeAI.Features.ModelImport.FileImport;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.QuickScan
{
    /// <summary>
    /// Routes a model quick-scan request to the scanner
    /// responsible for the selected model format.
    /// </summary>
    internal sealed class ModelQuickScanner
    {
        // Store the GGUF scanner so the same dependency can be reused
        // whenever this router receives a GGUF scan request.
        private readonly GgufQuickScanner _ggufQuickScanner;

        /// <summary>
        /// Creates the normal production router.
        /// </summary>
        internal ModelQuickScanner()
            : this(new GgufQuickScanner())
        {
        }

        /// <summary>
        /// Creates the router with a supplied GGUF scanner.
        /// </summary>
        internal ModelQuickScanner(
            GgufQuickScanner ggufQuickScanner)
        {
            // The router cannot function without its GGUF scanner.
            ArgumentNullException.ThrowIfNull(ggufQuickScanner);

            // Store the supplied dependency for later scan requests.
            _ggufQuickScanner = ggufQuickScanner;
        }

        /// <summary>
        /// Routes the selected model to the correct
        /// format-specific scanner.
        /// </summary>
        internal async Task<ModelQuickScanResult> ScanAsync(ModelFormatSelection format, string modelFilePath, CancellationToken cancellationToken)
        {
            try
            {
                switch (format)
                {
                    // The user cancelled the format-selection dialog.
                    case ModelFormatSelection.None:
                        return ModelQuickScanResult.CreateCancelled();

                    // Route GGUF files to the GGUF-specific scanner.
                    case ModelFormatSelection.Gguf:
                        ArgumentException.ThrowIfNullOrWhiteSpace(modelFilePath);

                        return await _ggufQuickScanner.ScanAsync(
                            modelFilePath,
                            cancellationToken);

                    // OpenVINO scanning will be connected here later.
                    case ModelFormatSelection.OpenVino:
                        return ModelQuickScanResult.CreateFailure(
                            failureCode: "openvino-scan-not-implemented",
                            userMessage:
                                "OpenVINO model scanning is not available yet.",
                            technicalMessage:
                                "No OpenVINO quick scanner has been implemented.");

                    // Handle corrupted, cast, or future unrecognised enum values.
                    default:
                        return ModelQuickScanResult.CreateFailure(
                            failureCode: "unsupported-model-format",
                            userMessage:
                                "The selected model format is not supported.",
                            technicalMessage:
                                $"Unexpected model format value: {format}.");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Convert expected user cancellation into a normal result.
                return ModelQuickScanResult.CreateCancelled();
            }
        }
    }
}