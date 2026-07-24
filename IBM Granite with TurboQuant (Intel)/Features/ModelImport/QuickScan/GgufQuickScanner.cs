using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
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

        // Bound the attacker-controlled top-level metadata loop.
        private const ulong MaxMetadataEntryCount = 1_000_000;

        // GGUF keys have a specification-defined maximum of 2^16 - 1 bytes.
        private const ulong MaxMetadataKeyByteLength = 65_535;

        // Application safety policy: no single metadata string may exceed 16 MiB.
        private const ulong MaxMetadataStringByteLength = 16 * 1024 * 1024;

        // Application safety policy: bound one declared array before any iteration.
        private const ulong MaxArrayElementCount = 1_000_000;

        // Application safety policy: bound aggregate array work across one scan.
        private const ulong MaxTotalArrayElementCount = 4_000_000;

        // Application safety policy: bound recursive nested-array stack use.
        private const int MaxArrayNestingDepth = 8;

        // Reject invalid UTF-8 instead of silently replacing malformed bytes.
        private static readonly UTF8Encoding StrictUtf8 = new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

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

                // Reject an unreasonable top-level loop before reading any entry.
                if (header.MetadataEntryCount > MaxMetadataEntryCount)
                {
                    return ModelQuickScanResult.CreateFailure(
                        failureCode: "excessive-metadata-count",
                        userMessage:
                            "The GGUF file declares too many metadata entries.",
                        technicalMessage:
                            $"The metadata entry count is " +
                            $"{header.MetadataEntryCount:N0}; the scanner limit is " +
                            $"{MaxMetadataEntryCount:N0}.");
                }

                // Keep the aggregate array budget local to this individual scan.
                ulong totalArrayElementCount = 0;

                // Consume each declared entry once, checking cancellation between entries.
                for (ulong entryIndex = 0;
                    entryIndex < header.MetadataEntryCount;
                    entryIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string key = await ReadMetadataKeyAsync(
                        stream,
                        entryIndex,
                        cancellationToken);
                    GgufMetadataValueType valueType = await ReadMetadataTypeAsync(
                        stream,
                        key,
                        cancellationToken);
                    totalArrayElementCount = await SkipMetadataValueAsync(
                        stream,
                        key,
                        valueType,
                        totalArrayElementCount,
                        cancellationToken);
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

            // Read every remaining fixed-header field before classifying complete input.
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

            // Reject a complete but non-GGUF signature with actual and expected bytes.
            if (!actualMagic.AsSpan().SequenceEqual(ExpectedGgufMagic))
            {
                throw new GgufFormatException(
                    failureCode: "invalid-magic",
                    userMessage: "The selected file is not a valid GGUF model.",
                    technicalMessage:
                        $"Expected GGUF magic bytes {BitConverter.ToString(ExpectedGgufMagic)} " +
                        $"at file offset 0, but found {BitConverter.ToString(actualMagic)}.");
            }

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
                // Bound the bytes read to this field when the exact read reaches EOF.
                long bytesRead = Math.Clamp(
                    stream.Position - fieldOffset,
                    0,
                    buffer.Length);

                // Preserve field context so the result identifies the malformed input.
                throw new GgufFormatException(
                    failureCode: "truncated-header",
                    userMessage: "The selected GGUF file has an incomplete header.",
                    technicalMessage:
                        $"GGUF header field '{fieldName}' starting at file offset " +
                        $"{fieldOffset} was truncated after reading {bytesRead} bytes; " +
                        $"expected {buffer.Length} bytes.",
                    innerException: exception);
            }
        }

        /// <summary>
        /// Reads one bounded, strictly decoded ASCII GGUF metadata key.
        /// </summary>
        private static async Task<string> ReadMetadataKeyAsync(
            FileStream stream,
            ulong entryIndex,
            CancellationToken cancellationToken)
        {
            long lengthOffset = stream.Position;
            byte[] lengthBuffer = new byte[sizeof(ulong)];

            try
            {
                await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw CreateTruncatedMetadataException(
                    key: $"entry {entryIndex}",
                    stage: "key length",
                    offset: lengthOffset,
                    expectedBytes: (ulong)lengthBuffer.Length,
                    actualBytes: Math.Clamp(
                        stream.Position - lengthOffset,
                        0,
                        lengthBuffer.Length),
                    exception);
            }

            ulong keyByteLength =
                BinaryPrimitives.ReadUInt64LittleEndian(lengthBuffer);
            if (keyByteLength > MaxMetadataKeyByteLength)
            {
                throw new GgufFormatException(
                    failureCode: "metadata-key-too-long",
                    userMessage:
                        "The GGUF file contains a metadata key that is too long.",
                    technicalMessage:
                        $"Metadata entry {entryIndex} key length at file offset " +
                        $"{lengthOffset} is {keyByteLength:N0} bytes; the GGUF " +
                        $"key limit is {MaxMetadataKeyByteLength:N0} bytes.");
            }

            long valueOffset = stream.Position;
            long remainingBytes = stream.Length - valueOffset;
            if (remainingBytes < 0 ||
                keyByteLength > (ulong)remainingBytes)
            {
                throw CreateTruncatedMetadataException(
                    key: $"entry {entryIndex}",
                    stage: "key bytes",
                    offset: valueOffset,
                    expectedBytes: keyByteLength,
                    actualBytes: Math.Max(remainingBytes, 0));
            }

            byte[] keyBytes = new byte[checked((int)keyByteLength)];
            try
            {
                await stream.ReadExactlyAsync(keyBytes, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw CreateTruncatedMetadataException(
                    key: $"entry {entryIndex}",
                    stage: "key bytes",
                    offset: valueOffset,
                    expectedBytes: keyByteLength,
                    actualBytes: Math.Clamp(
                        stream.Position - valueOffset,
                        0,
                        keyBytes.Length),
                    exception);
            }

            string key;
            try
            {
                key = StrictUtf8.GetString(keyBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new GgufFormatException(
                    failureCode: "invalid-metadata-encoding",
                    userMessage:
                        "The GGUF file contains invalid metadata text encoding.",
                    technicalMessage:
                        $"Metadata entry {entryIndex} key at file offset " +
                        $"{valueOffset} is not valid UTF-8.",
                    innerException: exception);
            }

            for (int index = 0; index < key.Length; index++)
            {
                if (key[index] > 0x7f)
                {
                    throw new GgufFormatException(
                        failureCode: "invalid-metadata-key",
                        userMessage:
                            "The GGUF file contains an invalid metadata key.",
                        technicalMessage:
                            $"Metadata entry {entryIndex} key at file offset " +
                            $"{valueOffset} contains non-ASCII character " +
                            $"U+{(int)key[index]:X4} at character index {index}.");
                }
            }

            return key;
        }

        /// <summary>
        /// Reads and validates an official GGUF metadata value type identifier.
        /// </summary>
        private static async Task<GgufMetadataValueType> ReadMetadataTypeAsync(
            FileStream stream,
            string key,
            CancellationToken cancellationToken)
        {
            long typeOffset = stream.Position;
            byte[] typeBuffer = new byte[sizeof(uint)];

            try
            {
                await stream.ReadExactlyAsync(typeBuffer, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage: "value type",
                    offset: typeOffset,
                    expectedBytes: (ulong)typeBuffer.Length,
                    actualBytes: Math.Clamp(
                        stream.Position - typeOffset,
                        0,
                        typeBuffer.Length),
                    exception);
            }

            uint rawType = BinaryPrimitives.ReadUInt32LittleEndian(typeBuffer);
            if (rawType > (uint)GgufMetadataValueType.Float64)
            {
                throw new GgufFormatException(
                    failureCode: "unsupported-metadata-type",
                    userMessage:
                        "The GGUF file uses an unsupported metadata value type.",
                    technicalMessage:
                        $"Metadata key '{key}' has value type {rawType} at file " +
                        $"offset {typeOffset}; supported GGUF types are 0 through 12.");
            }

            return (GgufMetadataValueType)rawType;
        }

        /// <summary>
        /// Consumes one metadata value without retaining unneeded payload data.
        /// </summary>
        private static async Task<ulong> SkipMetadataValueAsync(
            FileStream stream,
            string key,
            GgufMetadataValueType valueType,
            ulong totalArrayElementCount,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (valueType)
            {
                case GgufMetadataValueType.UInt8:
                case GgufMetadataValueType.Int8:
                    SkipValidatedBytes(
                        stream,
                        byteCount: 1,
                        key,
                        stage: "scalar payload");
                    return totalArrayElementCount;

                case GgufMetadataValueType.Boolean:
                    long booleanOffset = stream.Position;
                    byte[] booleanBuffer = new byte[1];
                    try
                    {
                        await stream.ReadExactlyAsync(
                            booleanBuffer,
                            cancellationToken);
                    }
                    catch (EndOfStreamException exception)
                    {
                        throw CreateTruncatedMetadataException(
                            key,
                            stage: "boolean payload",
                            offset: booleanOffset,
                            expectedBytes: 1,
                            actualBytes: Math.Clamp(
                                stream.Position - booleanOffset,
                                0,
                                booleanBuffer.Length),
                            exception);
                    }

                    if (booleanBuffer[0] > 1)
                    {
                        throw new GgufFormatException(
                            failureCode: "invalid-boolean-value",
                            userMessage:
                                "The GGUF file contains an invalid boolean value.",
                            technicalMessage:
                                $"Metadata key '{key}' has boolean byte " +
                                $"{booleanBuffer[0]} at file offset " +
                                $"{booleanOffset}; expected 0 or 1.");
                    }

                    return totalArrayElementCount;

                case GgufMetadataValueType.UInt16:
                case GgufMetadataValueType.Int16:
                    SkipValidatedBytes(
                        stream,
                        byteCount: 2,
                        key,
                        stage: "scalar payload");
                    return totalArrayElementCount;

                case GgufMetadataValueType.UInt32:
                case GgufMetadataValueType.Int32:
                case GgufMetadataValueType.Float32:
                    SkipValidatedBytes(
                        stream,
                        byteCount: 4,
                        key,
                        stage: "scalar payload");
                    return totalArrayElementCount;

                case GgufMetadataValueType.UInt64:
                case GgufMetadataValueType.Int64:
                case GgufMetadataValueType.Float64:
                    SkipValidatedBytes(
                        stream,
                        byteCount: 8,
                        key,
                        stage: "scalar payload");
                    return totalArrayElementCount;

                case GgufMetadataValueType.String:
                    ulong stringByteLength = await ReadMetadataUInt64Async(
                        stream,
                        key,
                        stage: "string length",
                        cancellationToken);
                    if (stringByteLength > MaxMetadataStringByteLength)
                    {
                        throw new GgufFormatException(
                            failureCode: "metadata-string-too-long",
                            userMessage:
                                "The GGUF file contains a metadata string that is too long.",
                            technicalMessage:
                                $"Metadata key '{key}' declares a string of " +
                                $"{stringByteLength:N0} bytes; the scanner limit is " +
                                $"{MaxMetadataStringByteLength:N0} bytes.");
                    }

                    SkipValidatedBytes(
                        stream,
                        stringByteLength,
                        key,
                        stage: "string payload");
                    return totalArrayElementCount;

                case GgufMetadataValueType.Array:
                    return await ConsumeArrayAsync(
                        stream,
                        key,
                        nestingDepth: 1,
                        totalArrayElementCount,
                        cancellationToken);
            }

            throw new InvalidOperationException(
                $"Unhandled GGUF metadata value type {valueType}.");
        }

        /// <summary>
        /// Consumes one bounded GGUF array, including explicitly permitted nested arrays.
        /// </summary>
        private static async Task<ulong> ConsumeArrayAsync(
            FileStream stream,
            string key,
            int nestingDepth,
            ulong totalArrayElementCount,
            CancellationToken cancellationToken)
        {
            if (nestingDepth > MaxArrayNestingDepth)
            {
                throw new GgufFormatException(
                    failureCode: "excessive-array-depth",
                    userMessage:
                        "The GGUF file contains metadata arrays nested too deeply.",
                    technicalMessage:
                        $"Metadata key '{key}' reached array nesting depth " +
                        $"{nestingDepth}; the scanner limit is " +
                        $"{MaxArrayNestingDepth}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            GgufMetadataValueType elementType = await ReadMetadataTypeAsync(
                stream,
                key,
                cancellationToken);
            ulong elementCount = await ReadMetadataUInt64Async(
                stream,
                key,
                stage: $"array depth {nestingDepth} element count",
                cancellationToken);

            if (elementCount > MaxArrayElementCount)
            {
                throw new GgufFormatException(
                    failureCode: "excessive-array-count",
                    userMessage:
                        "The GGUF file contains a metadata array that is too large.",
                    technicalMessage:
                        $"Metadata key '{key}' array at depth {nestingDepth} " +
                        $"declares {elementCount:N0} elements; the per-array " +
                        $"scanner limit is {MaxArrayElementCount:N0}.");
            }

            // Compare by subtraction so attacker-controlled addition cannot overflow.
            if (totalArrayElementCount > MaxTotalArrayElementCount ||
                elementCount >
                    MaxTotalArrayElementCount - totalArrayElementCount)
            {
                throw new GgufFormatException(
                    failureCode: "excessive-array-count",
                    userMessage:
                        "The GGUF file declares too many metadata array elements.",
                    technicalMessage:
                        $"Metadata key '{key}' would raise the scan total from " +
                        $"{totalArrayElementCount:N0} by {elementCount:N0} " +
                        $"elements; the total scanner limit is " +
                        $"{MaxTotalArrayElementCount:N0}.");
            }

            totalArrayElementCount += elementCount;

            // Fixed-width non-boolean arrays need no element-by-element work.
            ulong elementWidth = elementType switch
            {
                GgufMetadataValueType.UInt8 or
                GgufMetadataValueType.Int8 => 1,
                GgufMetadataValueType.UInt16 or
                GgufMetadataValueType.Int16 => 2,
                GgufMetadataValueType.UInt32 or
                GgufMetadataValueType.Int32 or
                GgufMetadataValueType.Float32 => 4,
                GgufMetadataValueType.UInt64 or
                GgufMetadataValueType.Int64 or
                GgufMetadataValueType.Float64 => 8,
                _ => 0
            };

            if (elementWidth != 0)
            {
                ulong payloadByteCount = checked(elementCount * elementWidth);
                SkipValidatedBytes(
                    stream,
                    payloadByteCount,
                    key,
                    stage: $"array payload at depth {nestingDepth}");
                return totalArrayElementCount;
            }

            // Booleans, strings, and nested arrays require semantic consumption.
            for (ulong elementIndex = 0;
                elementIndex < elementCount;
                elementIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (elementType == GgufMetadataValueType.Array)
                {
                    totalArrayElementCount = await ConsumeArrayAsync(
                        stream,
                        key,
                        nestingDepth + 1,
                        totalArrayElementCount,
                        cancellationToken);
                }
                else
                {
                    totalArrayElementCount = await SkipMetadataValueAsync(
                        stream,
                        key,
                        elementType,
                        totalArrayElementCount,
                        cancellationToken);
                }
            }

            return totalArrayElementCount;
        }

        /// <summary>
        /// Reads one little-endian unsigned 64-bit metadata field exactly.
        /// </summary>
        private static async Task<ulong> ReadMetadataUInt64Async(
            FileStream stream,
            string key,
            string stage,
            CancellationToken cancellationToken)
        {
            long offset = stream.Position;
            byte[] buffer = new byte[sizeof(ulong)];

            try
            {
                await stream.ReadExactlyAsync(buffer, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage,
                    offset,
                    expectedBytes: (ulong)buffer.Length,
                    actualBytes: Math.Clamp(
                        stream.Position - offset,
                        0,
                        buffer.Length),
                    exception);
            }

            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        /// <summary>
        /// Advances over a validated byte range without ever seeking past EOF.
        /// </summary>
        private static void SkipValidatedBytes(
            FileStream stream,
            ulong byteCount,
            string key,
            string stage)
        {
            long offset = stream.Position;
            long remainingBytes = stream.Length - offset;
            if (remainingBytes < 0 ||
                byteCount > (ulong)remainingBytes)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage,
                    offset,
                    expectedBytes: byteCount,
                    actualBytes: Math.Max(remainingBytes, 0));
            }

            stream.Seek(checked((long)byteCount), SeekOrigin.Current);
        }

        /// <summary>
        /// Creates a stable structural failure for incomplete metadata bytes.
        /// </summary>
        private static GgufFormatException CreateTruncatedMetadataException(
            string key,
            string stage,
            long offset,
            ulong expectedBytes,
            long actualBytes,
            Exception? innerException = null)
        {
            return new GgufFormatException(
                failureCode: "truncated-metadata",
                userMessage: "The selected GGUF file has incomplete metadata.",
                technicalMessage:
                    $"Metadata key '{key}' stage '{stage}' at file offset " +
                    $"{offset} has {actualBytes:N0} available/read bytes; " +
                    $"expected {expectedBytes:N0} bytes.",
                innerException: innerException);
        }

        /// <summary>
        /// Represents the decoded fixed header values needed before metadata parsing begins.
        /// </summary>
        private readonly record struct GgufHeader(
            uint Version,
            ulong TensorCount,
            ulong MetadataEntryCount);

        /// <summary>
        /// Lists every metadata value type defined by the GGUF specification.
        /// </summary>
        private enum GgufMetadataValueType : uint
        {
            UInt8 = 0,
            Int8 = 1,
            UInt16 = 2,
            Int16 = 3,
            UInt32 = 4,
            Int32 = 5,
            Float32 = 6,
            Boolean = 7,
            String = 8,
            Array = 9,
            UInt64 = 10,
            Int64 = 11,
            Float64 = 12
        }

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
