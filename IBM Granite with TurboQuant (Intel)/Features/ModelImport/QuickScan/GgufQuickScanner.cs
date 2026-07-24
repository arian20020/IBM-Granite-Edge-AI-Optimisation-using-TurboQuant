using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.QuickScan
{
    /// <summary>
    /// Performs a lightweight validation and metadata scan of a GGUF file.
    /// </summary>
    internal sealed class GgufQuickScanner
    {
        // Every GGUF signature contains exactly four bytes.
        private const int GgufMagicLength = 4;

        // Version 3 is the GGUF container format currently supported.
        private const uint SupportedGgufVersion = 3;

        // Store the four bytes every valid GGUF file must begin with.
        private static readonly byte[] ExpectedGgufMagic =
        {
            (byte)'G',
            (byte)'G',
            (byte)'U',
            (byte)'F'
        };

        /// <summary>
        /// Scans the selected GGUF file and returns the completed result.
        /// </summary>
        internal async Task<ModelQuickScanResult> ScanAsync(
            string modelFilePath,
            CancellationToken cancellationToken)
        {
            // Reject a null, empty, or whitespace-only path.
            ArgumentException.ThrowIfNullOrWhiteSpace(modelFilePath);

            // Stop immediately when cancellation was already requested.
            cancellationToken.ThrowIfCancellationRequested();

            // Open the existing model file for asynchronous, read-only access.
            await using FileStream stream = new(
                modelFilePath,
                new FileStreamOptions
                {
                    // The selected file must already exist.
                    Mode = FileMode.Open,

                    // The scanner may inspect but not alter the model.
                    Access = FileAccess.Read,

                    // Allow another process to read the model simultaneously.
                    Share = FileShare.Read,

                    // Prepare for asynchronous, beginning-to-end reading.
                    Options =
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan
                });

            try
            {
                // Read and validate all 24 bytes of the fixed GGUF header.
                GgufHeader header = await ReadHeaderAsync(stream, cancellationToken);

                // Reject versions whose structure this scanner cannot interpret.
                if (header.Version != SupportedGgufVersion)
                {
                    return ModelQuickScanResult.CreateFailure(
                        failureCode: "unsupported-version",
                        userMessage:
                            "This GGUF file uses a version that is not supported.",
                        technicalMessage:
                        $"Expected GGUF version {SupportedGgufVersion}, " +
                            $"but found version {header.Version}.");
                }

                // No metadata entries are available yet to supply the required architecture.
                return ModelQuickScanResult.CreateFailure(
                    failureCode: "missing-required-architecture",
                    userMessage:
                        "The GGUF file does not identify its model architecture.",
                    technicalMessage:
                        $"The GGUF fixed header declares {header.TensorCount} tensor " +
                        $"descriptors and {header.MetadataEntryCount} metadata entries, " +
                        "but no architecture metadata was parsed.");
            }
            catch (GgufFormatException exception)
            {
                // Translate only expected structural input failures at this boundary.
                return ModelQuickScanResult.CreateFailure(
                    exception.FailureCode,
                    exception.UserMessage,
                    exception.TechnicalMessage);
            }
        }

        /// <summary>
        /// Reads and validates every field in the fixed 24-byte GGUF header.
        /// </summary>
        private static async Task<GgufHeader> ReadHeaderAsync(
            FileStream stream,
            CancellationToken cancellationToken)
        {
            // Read the required signature before decoding any numeric header field.
            byte[] actualMagic = new byte[GgufMagicLength];
            await ReadHeaderFieldAsync(
                stream,
                actualMagic,
                fieldName: "magic",
                fieldOffset: 0,
                cancellationToken);

            // Reject a complete but non-GGUF signature with its established failure code.
            if (!actualMagic.AsSpan().SequenceEqual(ExpectedGgufMagic))
            {
                throw new GgufFormatException(
                    failureCode: "invalid-magic",
                    userMessage: "The selected file is not a valid GGUF model.",
                    technicalMessage: "Expected GGUF magic bytes at file offset 0.");
            }

            // Decode the version and both declared counts using the GGUF little-endian layout.
            uint version = await ReadUInt32Async(
                stream,
                fieldName: "version",
                fieldOffset: 4,
                cancellationToken);
            ulong tensorCount = await ReadUInt64Async(
                stream,
                fieldName: "tensor count",
                fieldOffset: 8,
                cancellationToken);
            ulong metadataEntryCount = await ReadUInt64Async(
                stream,
                fieldName: "metadata entry count",
                fieldOffset: 16,
                cancellationToken);

            // Return the decoded header without reading tensor descriptors or metadata values.
            return new GgufHeader(version, tensorCount, metadataEntryCount);
        }

        /// <summary>
        /// Reads a little-endian unsigned 32-bit fixed-header field.
        /// </summary>
        private static async Task<uint> ReadUInt32Async(
            FileStream stream,
            string fieldName,
            long fieldOffset,
            CancellationToken cancellationToken)
        {
            // Allocate only the four bytes required by one uint32 field.
            byte[] buffer = new byte[sizeof(uint)];
            await ReadHeaderFieldAsync(
                stream,
                buffer,
                fieldName,
                fieldOffset,
                cancellationToken);

            // Decode the wire-format bytes explicitly rather than relying on machine endianness.
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a little-endian unsigned 64-bit fixed-header field.
        /// </summary>
        private static async Task<ulong> ReadUInt64Async(
            FileStream stream,
            string fieldName,
            long fieldOffset,
            CancellationToken cancellationToken)
        {
            // Allocate only the eight bytes required by one uint64 field.
            byte[] buffer = new byte[sizeof(ulong)];
            await ReadHeaderFieldAsync(
                stream,
                buffer,
                fieldName,
                fieldOffset,
                cancellationToken);

            // Decode the wire-format bytes explicitly rather than relying on machine endianness.
            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        /// <summary>
        /// Reads one fixed-size GGUF header field or reports its exact truncation location.
        /// </summary>
        private static async Task ReadHeaderFieldAsync(
            FileStream stream,
            Memory<byte> buffer,
            string fieldName,
            long fieldOffset,
            CancellationToken cancellationToken)
        {
            try
            {
                // Read exactly the bytes required by the fixed header field.
                await stream.ReadExactlyAsync(buffer, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                // Preserve field context so the result identifies the malformed input.
                throw new GgufFormatException(
                    failureCode: "truncated-header",
                    userMessage: "The selected GGUF file has an incomplete header.",
                    technicalMessage:
                        $"GGUF header field '{fieldName}' starting at file offset " +
                        $"{fieldOffset} was truncated; expected {buffer.Length} bytes.",
                    innerException: exception);
            }
        }

        /// <summary>
        /// Represents the decoded fixed header values needed before metadata parsing begins.
        /// </summary>
        private readonly record struct GgufHeader(
            uint Version,
            ulong TensorCount,
            ulong MetadataEntryCount);

        /// <summary>
        /// Carries a controlled GGUF structural failure to the scanner result boundary.
        /// </summary>
        private sealed class GgufFormatException : Exception
        {
            /// <summary>
            /// Initializes a structural failure with its stable code and technical details.
            /// </summary>
            internal GgufFormatException(
                string failureCode,
                string userMessage,
                string technicalMessage,
                Exception? innerException = null)
                : base(technicalMessage, innerException)
            {
                // Retain the stable failure code required by the result contract.
                FailureCode = failureCode;

                // Retain the concise message intended for the quick-scan caller.
                UserMessage = userMessage;

                // Retain precise parser diagnostics without exposing them to the UI directly.
                TechnicalMessage = technicalMessage;
            }

            /// <summary>
            /// Gets the stable code returned to the quick-scan caller.
            /// </summary>
            internal string FailureCode { get; }

            /// <summary>
            /// Gets the concise message returned to the quick-scan caller.
            /// </summary>
            internal string UserMessage { get; }

            /// <summary>
            /// Gets the parser detail used for diagnostics.
            /// </summary>
            internal string TechnicalMessage { get; }
        }
    }
}
