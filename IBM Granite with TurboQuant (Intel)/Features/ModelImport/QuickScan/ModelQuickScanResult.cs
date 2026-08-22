namespace GraniteEdgeAI.Features.ModelImport.QuickScan
{
    // Represents the completed outcome and data from a model quick scan.
    internal sealed class ModelQuickScanResult
    {
        // Stores whether the scan succeeded, failed, or was cancelled.
        public ModelQuickScanOutcome Outcome { get; }

        // Stores metadata discovered during a successful scan.
        public string? ModelName { get; }
        public string? Architecture { get; }
        public string? ParameterSizeLabel { get; }
        public string? Quantization { get; }
        public long? FileSizeBytes { get; }
        public System.DateTimeOffset? FileLastWriteTimeUtc { get; }
        public ulong? ContextLength { get; }
        public uint? GgufVersion { get; }

        // Stores information produced during a failed scan.
        public string? FailureCode { get; }
        public string? UserMessage { get; }
        public string? TechnicalMessage { get; }

        // Receives all possible result values and stores them in the properties.
        private ModelQuickScanResult(
            ModelQuickScanOutcome outcome,
            string? modelName = null,
            string? architecture = null,
            string? parameterSizeLabel = null,
            string? quantization = null,
            long? fileSizeBytes = null,
            System.DateTimeOffset? fileLastWriteTimeUtc = null,
            ulong? contextLength = null,
            uint? ggufVersion = null,
            string? failureCode = null,
            string? userMessage = null,
            string? technicalMessage = null)
        {
            // Store the scan outcome.
            Outcome = outcome;

            // Store successful scan metadata.
            ModelName = modelName;
            Architecture = architecture;
            ParameterSizeLabel = parameterSizeLabel;
            Quantization = quantization;
            FileSizeBytes = fileSizeBytes;
            FileLastWriteTimeUtc = fileLastWriteTimeUtc;
            ContextLength = contextLength;
            GgufVersion = ggufVersion;

            // Store failure information.
            FailureCode = failureCode;
            UserMessage = userMessage;
            TechnicalMessage = technicalMessage;
        }

        // Creates a clean result for a scan that was cancelled.
        internal static ModelQuickScanResult CreateCancelled()
        {
            // Cancellation needs only its outcome.
            return new ModelQuickScanResult(
                outcome: ModelQuickScanOutcome.Cancelled);
        }

        // Creates a successful result containing the metadata found by the scanner.
        internal static ModelQuickScanResult CreateSuccess(
            string? modelName,
            string? architecture,
            string? parameterSizeLabel,
            string? quantization,
            long fileSizeBytes,
            ulong? contextLength,
            uint ggufVersion,
            System.DateTimeOffset? fileLastWriteTimeUtc = null)
        {
            // A successful result must have a usable model name.
            System.ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

            // A successful result must identify the model architecture.
            System.ArgumentException.ThrowIfNullOrWhiteSpace(architecture);

            // A successful result must represent a non-empty model file.
            System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileSizeBytes);

            // A successful result must record a supported GGUF version.
            System.ArgumentOutOfRangeException.ThrowIfZero(ggufVersion);

            // A supplied scan-time file timestamp must be expressed in UTC.
            if (fileLastWriteTimeUtc is System.DateTimeOffset timestamp &&
                timestamp.Offset != System.TimeSpan.Zero)
            {
                throw new System.ArgumentException(
                    "The model file last-write time must use the UTC offset.",
                    nameof(fileLastWriteTimeUtc));
            }

            // Pass the successful outcome and discovered metadata to the constructor.
            return new ModelQuickScanResult(
                outcome: ModelQuickScanOutcome.Success,
                modelName: modelName,
                architecture: architecture,
                parameterSizeLabel: parameterSizeLabel,
                quantization: quantization,
                fileSizeBytes: fileSizeBytes,
                fileLastWriteTimeUtc: fileLastWriteTimeUtc,
                contextLength: contextLength,
                ggufVersion: ggufVersion);
        }
        // Creates a failed quick-scan result containing diagnostic information.
        internal static ModelQuickScanResult CreateFailure(
            string? failureCode,
            string? userMessage,
            string? technicalMessage)
        {
            // A failure result must always include a diagnostic code.
            System.ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);

            // Supply a clear message when no user-facing detail was provided.
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                userMessage = "The selected model could not be scanned.";
            }

            // Supply diagnostic text when no technical detail was provided.
            if (string.IsNullOrWhiteSpace(technicalMessage))
            {
                technicalMessage =
                    "No additional technical information was provided.";
            }

            // Create the result with a Failure outcome and pass in only failure data.
            return new ModelQuickScanResult(
                outcome: ModelQuickScanOutcome.Failure,
                failureCode: failureCode,
                userMessage: userMessage,
                technicalMessage: technicalMessage);
        }
    }
}
