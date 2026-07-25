using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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

        // Architecture-specific context fields use this exact metadata-key suffix.
        private const string ContextLengthMetadataKeySuffix = ".context_length";

        // Application safety policy: retain only a small set of pre-architecture context keys.
        private const int MaxPendingContextCandidateCount = 64;

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

                // Keep retained display metadata and the aggregate array budget local to this scan.
                GgufScanState scanState = new();
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
                    totalArrayElementCount = await ReadKnownMetadataValueAsync(
                        stream,
                        key,
                        valueType,
                        scanState,
                        totalArrayElementCount,
                        cancellationToken);
                }

                // A blank architecture is as unusable as an absent architecture.
                if (string.IsNullOrWhiteSpace(scanState.Architecture))
                {
                    return ModelQuickScanResult.CreateFailure(
                        failureCode: "missing-required-architecture",
                        userMessage:
                            "The GGUF file does not identify its model architecture.",
                        technicalMessage:
                            $"The GGUF fixed header declares {header.TensorCount} tensor " +
                            $"descriptors and {header.MetadataEntryCount} metadata entries, " +
                            "but no usable general.architecture metadata was parsed.");
                }

                // A missing or blank optional name falls back to the selected file name.
                string modelName = string.IsNullOrWhiteSpace(scanState.ModelName)
                    ? Path.GetFileName(modelFilePath)
                    : scanState.ModelName;
                return CreateSuccessResult(stream, header, scanState, modelName);
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

            ValidateMetadataKey(key, entryIndex, valueOffset);

            return key;
        }

        /// <summary>
        /// Validates the GGUF hierarchical lower_snake_case metadata-key grammar.
        /// </summary>
        private static void ValidateMetadataKey(
            string key,
            ulong entryIndex,
            long keyOffset)
        {
            if (key.Length == 0)
            {
                throw new GgufFormatException(
                    failureCode: "invalid-metadata-key",
                    userMessage:
                        "The GGUF file contains an invalid metadata key.",
                    technicalMessage:
                        $"Metadata entry {entryIndex} key at file offset " +
                        $"{keyOffset} is empty; expected one or more nonempty " +
                        "lower_snake_case segments.");
            }

            int segmentLength = 0;
            for (int index = 0; index < key.Length; index++)
            {
                char character = key[index];
                if (character == '.')
                {
                    if (segmentLength == 0)
                    {
                        throw CreateInvalidKeySegmentException(
                            entryIndex,
                            keyOffset,
                            index);
                    }

                    segmentLength = 0;
                    continue;
                }

                bool isLowerSnakeCaseCharacter =
                    character is >= 'a' and <= 'z' ||
                    character is >= '0' and <= '9' ||
                    character == '_';
                if (!isLowerSnakeCaseCharacter)
                {
                    throw new GgufFormatException(
                        failureCode: "invalid-metadata-key",
                        userMessage:
                            "The GGUF file contains an invalid metadata key.",
                        technicalMessage:
                            $"Metadata entry {entryIndex} key at file offset " +
                            $"{keyOffset} has invalid character code " +
                            $"U+{(int)character:X4} at character index {index}; " +
                            "expected lowercase ASCII letters, digits, " +
                            "underscores, or dot separators.");
                }

                segmentLength++;
            }

            if (segmentLength == 0)
            {
                throw CreateInvalidKeySegmentException(
                    entryIndex,
                    keyOffset,
                    key.Length);
            }
        }

        /// <summary>
        /// Creates a safe diagnostic for a leading, trailing, or adjacent dot.
        /// </summary>
        private static GgufFormatException CreateInvalidKeySegmentException(
            ulong entryIndex,
            long keyOffset,
            int characterIndex)
        {
            return new GgufFormatException(
                failureCode: "invalid-metadata-key",
                userMessage:
                    "The GGUF file contains an invalid metadata key.",
                technicalMessage:
                    $"Metadata entry {entryIndex} key at file offset " +
                    $"{keyOffset} has an empty segment at character index " +
                    $"{characterIndex}; every dot-separated segment must be " +
                    "nonempty lower_snake_case.");
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
        /// Reads the first scanner-relevant value or structurally consumes a duplicate.
        /// </summary>
        private static async Task<ulong> ReadKnownMetadataValueAsync(
            FileStream stream,
            string key,
            GgufMetadataValueType valueType,
            GgufScanState scanState,
            ulong totalArrayElementCount,
            CancellationToken cancellationToken)
        {
            switch (key)
            {
                case "general.architecture":
                    if (scanState.HasArchitectureMetadata)
                    {
                        return await SkipMetadataValueAsync(
                            stream,
                            key,
                            valueType,
                            totalArrayElementCount,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.String,
                        "invalid-architecture-type");
                    scanState.Architecture = await ReadRetainedStringAsync(
                        stream,
                        key,
                        cancellationToken);
                    scanState.HasArchitectureMetadata = true;
                    ResolvePendingContextCandidate(scanState);
                    return totalArrayElementCount;

                case "general.name":
                    if (scanState.HasModelNameMetadata)
                    {
                        return await SkipMetadataValueAsync(
                            stream,
                            key,
                            valueType,
                            totalArrayElementCount,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.String,
                        "invalid-name-type");
                    scanState.ModelName = await ReadRetainedStringAsync(
                        stream,
                        key,
                        cancellationToken);
                    scanState.HasModelNameMetadata = true;
                    return totalArrayElementCount;

                case "general.size_label":
                    if (scanState.HasParameterSizeLabelMetadata)
                    {
                        return await SkipMetadataValueAsync(
                            stream,
                            key,
                            valueType,
                            totalArrayElementCount,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.String,
                        "invalid-size-label-type");
                    string sizeLabel = await ReadRetainedStringAsync(
                        stream,
                        key,
                        cancellationToken);
                    scanState.ParameterSizeLabel = string.IsNullOrWhiteSpace(sizeLabel)
                        ? null
                        : sizeLabel;
                    scanState.HasParameterSizeLabelMetadata = true;
                    return totalArrayElementCount;

                case "general.file_type":
                    if (scanState.HasFileTypeMetadata)
                    {
                        return await SkipMetadataValueAsync(
                            stream,
                            key,
                            valueType,
                            totalArrayElementCount,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.UInt32,
                        "invalid-file-type");
                    scanState.FileType = await ReadMetadataUInt32Async(
                        stream,
                        key,
                        stage: "file type",
                        cancellationToken);
                    scanState.HasFileTypeMetadata = true;
                    return totalArrayElementCount;
            }

            // Once architecture is known, process its exact context key immediately.
            if (scanState.HasArchitectureMetadata &&
                scanState.Architecture is not null &&
                IsArchitectureContextLengthKey(key, scanState.Architecture))
            {
                if (scanState.HasContextLengthMetadata)
                {
                    return await SkipMetadataValueAsync(
                        stream,
                        key,
                        valueType,
                        totalArrayElementCount,
                        cancellationToken);
                }

                return await ReadContextLengthAsync(
                    stream,
                    key,
                    valueType,
                    scanState,
                    totalArrayElementCount,
                    cancellationToken);
            }

            // Before architecture is known, retain only possible context candidates.
            if (!scanState.HasArchitectureMetadata &&
                key.EndsWith(ContextLengthMetadataKeySuffix, StringComparison.Ordinal))
            {
                return await ReadPendingContextCandidateAsync(
                    stream,
                    key,
                    valueType,
                    scanState,
                    totalArrayElementCount,
                    cancellationToken);
            }

            return await SkipMetadataValueAsync(
                stream,
                key,
                valueType,
                totalArrayElementCount,
                cancellationToken);
        }

        /// <summary>
        /// Reads one exact architecture context value and normalizes supported unsigned widths.
        /// </summary>
        private static async Task<ulong> ReadContextLengthAsync(
            FileStream stream,
            string key,
            GgufMetadataValueType valueType,
            GgufScanState scanState,
            ulong totalArrayElementCount,
            CancellationToken cancellationToken)
        {
            if (valueType == GgufMetadataValueType.UInt32)
            {
                scanState.ContextLength = await ReadMetadataUInt32Async(
                    stream,
                    key,
                    stage: "context length",
                    cancellationToken);
                scanState.HasContextLengthMetadata = true;
                return totalArrayElementCount;
            }

            if (valueType == GgufMetadataValueType.UInt64)
            {
                scanState.ContextLength = await ReadMetadataUInt64Async(
                    stream,
                    key,
                    stage: "context length",
                    cancellationToken);
                scanState.HasContextLengthMetadata = true;
                return totalArrayElementCount;
            }

            throw CreateInvalidContextTypeException(key, valueType);
        }

        /// <summary>
        /// Consumes and retains one bounded pre-architecture context candidate.
        /// </summary>
        private static async Task<ulong> ReadPendingContextCandidateAsync(
            FileStream stream,
            string key,
            GgufMetadataValueType valueType,
            GgufScanState scanState,
            ulong totalArrayElementCount,
            CancellationToken cancellationToken)
        {
            if (scanState.PendingContextCandidates.ContainsKey(key))
            {
                return await SkipMetadataValueAsync(
                    stream,
                    key,
                    valueType,
                    totalArrayElementCount,
                    cancellationToken);
            }

            if (scanState.PendingContextCandidates.Count >= MaxPendingContextCandidateCount)
            {
                throw new GgufFormatException(
                    failureCode: "excessive-context-candidate-count",
                    userMessage:
                        "The GGUF file declares too many context metadata candidates.",
                    technicalMessage:
                        $"Pre-architecture context candidate '{key}' makes the " +
                        $"candidate count {scanState.PendingContextCandidates.Count + 1}; " +
                        $"the scanner limit is {MaxPendingContextCandidateCount}.");
            }

            ulong? contextLength = null;
            if (valueType == GgufMetadataValueType.UInt32)
            {
                contextLength = await ReadMetadataUInt32Async(
                    stream,
                    key,
                    stage: "context length",
                    cancellationToken);
            }
            else if (valueType == GgufMetadataValueType.UInt64)
            {
                contextLength = await ReadMetadataUInt64Async(
                    stream,
                    key,
                    stage: "context length",
                    cancellationToken);
            }
            else
            {
                totalArrayElementCount = await SkipMetadataValueAsync(
                    stream,
                    key,
                    valueType,
                    totalArrayElementCount,
                    cancellationToken);
            }

            scanState.PendingContextCandidates[key] = new PendingContextValue(
                valueType,
                contextLength);
            return totalArrayElementCount;
        }

        /// <summary>
        /// Resolves one matching retained candidate without constructing an architecture key.
        /// </summary>
        private static void ResolvePendingContextCandidate(GgufScanState scanState)
        {
            string? architecture = scanState.Architecture;
            if (string.IsNullOrWhiteSpace(architecture))
            {
                return;
            }

            foreach (KeyValuePair<string, PendingContextValue> candidate in
                scanState.PendingContextCandidates)
            {
                if (!IsArchitectureContextLengthKey(candidate.Key, architecture))
                {
                    continue;
                }

                if (candidate.Value.ValueType is not GgufMetadataValueType.UInt32 and
                    not GgufMetadataValueType.UInt64)
                {
                    throw CreateInvalidContextTypeException(
                        candidate.Key,
                        candidate.Value.ValueType);
                }

                scanState.ContextLength = candidate.Value.ContextLength;
                scanState.HasContextLengthMetadata = true;
                return;
            }
        }

        /// <summary>
        /// Creates the stable failure used when an exact context key has a wrong type.
        /// </summary>
        private static GgufFormatException CreateInvalidContextTypeException(
            string key,
            GgufMetadataValueType valueType)
        {
            return new GgufFormatException(
                failureCode: "invalid-context-type",
                userMessage:
                    "The GGUF file contains an invalid context length value.",
                technicalMessage:
                    $"Metadata key '{key}' has value type {valueType}; " +
                    "expected UInt32 or UInt64.");
        }

        /// <summary>
        /// Compares an architecture context key exactly without constructing a large temporary string.
        /// </summary>
        private static bool IsArchitectureContextLengthKey(
            string key,
            string architecture)
        {
            // Check length and suffix first so a retained multi-megabyte architecture
            // cannot force a matching-size allocation or prefix comparison per later entry.
            if (key.Length != architecture.Length + ContextLengthMetadataKeySuffix.Length ||
                !key.EndsWith(
                    ContextLengthMetadataKeySuffix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return key.StartsWith(architecture, StringComparison.Ordinal);
        }

        /// <summary>
        /// Requires a known metadata key to use its specified GGUF value type.
        /// </summary>
        private static void RequireMetadataType(
            string key,
            GgufMetadataValueType actualType,
            GgufMetadataValueType expectedType,
            string failureCode)
        {
            if (actualType == expectedType)
            {
                return;
            }

            throw new GgufFormatException(
                failureCode,
                userMessage: "The GGUF file contains metadata with an invalid value type.",
                technicalMessage:
                    $"Metadata key '{key}' has value type {actualType}; " +
                    $"expected {expectedType}.");
        }

        /// <summary>
        /// Reads a bounded retained metadata string with strict UTF-8 validation.
        /// </summary>
        private static async Task<string> ReadRetainedStringAsync(
            FileStream stream,
            string key,
            CancellationToken cancellationToken)
        {
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

            long valueOffset = stream.Position;
            long remainingBytes = stream.Length - valueOffset;
            if (remainingBytes < 0 || stringByteLength > (ulong)remainingBytes)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage: "string payload",
                    offset: valueOffset,
                    expectedBytes: stringByteLength,
                    actualBytes: Math.Max(remainingBytes, 0));
            }

            byte[] stringBytes = new byte[checked((int)stringByteLength)];
            try
            {
                await stream.ReadExactlyAsync(stringBytes, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage: "string payload",
                    offset: valueOffset,
                    expectedBytes: stringByteLength,
                    actualBytes: Math.Clamp(
                        stream.Position - valueOffset,
                        0,
                        stringBytes.Length),
                    exception);
            }

            try
            {
                return StrictUtf8.GetString(stringBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new GgufFormatException(
                    failureCode: "invalid-metadata-encoding",
                    userMessage:
                        "The GGUF file contains invalid metadata text encoding.",
                    technicalMessage:
                        $"Metadata key '{key}' string payload at file offset " +
                        $"{valueOffset} is not valid UTF-8.",
                    innerException: exception);
            }
        }

        /// <summary>
        /// Creates the completed success result from one scan's retained state.
        /// </summary>
        private static ModelQuickScanResult CreateSuccessResult(
            FileStream stream,
            GgufHeader header,
            GgufScanState scanState,
            string modelName)
        {
            return ModelQuickScanResult.CreateSuccess(
                modelName,
                scanState.Architecture,
                scanState.ParameterSizeLabel,
                scanState.FileType is uint fileType
                    ? MapFileTypeToQuantization(fileType)
                    : null,
                stream.Length,
                scanState.ContextLength,
                header.Version);
        }

        /// <summary>
        /// Maps GGUF file types from llama.cpp's current llama_ftype enum.
        /// Historical labels 4-6 and 33-35 are retained for compatibility; source:
        /// https://github.com/ggml-org/llama.cpp/blob/master/include/llama.h
        /// </summary>
        private static string MapFileTypeToQuantization(uint fileType)
        {
            return fileType switch
            {
                0 => "F32",
                1 => "F16",
                2 => "Q4_0",
                3 => "Q4_1",
                4 => "Q4_1_SOME_F16",
                5 => "Q4_2",
                6 => "Q4_3",
                7 => "Q8_0",
                8 => "Q5_0",
                9 => "Q5_1",
                10 => "Q2_K",
                11 => "Q3_K_S",
                12 => "Q3_K_M",
                13 => "Q3_K_L",
                14 => "Q4_K_S",
                15 => "Q4_K_M",
                16 => "Q5_K_S",
                17 => "Q5_K_M",
                18 => "Q6_K",
                19 => "IQ2_XXS",
                20 => "IQ2_XS",
                21 => "Q2_K_S",
                22 => "IQ3_XS",
                23 => "IQ3_XXS",
                24 => "IQ1_S",
                25 => "IQ4_NL",
                26 => "IQ3_S",
                27 => "IQ3_M",
                28 => "IQ2_S",
                29 => "IQ2_M",
                30 => "IQ4_XS",
                31 => "IQ1_M",
                32 => "BF16",
                33 => "Q4_0_4_4",
                34 => "Q4_0_4_8",
                35 => "Q4_0_8_8",
                36 => "TQ1_0",
                37 => "TQ2_0",
                38 => "MXFP4_MOE",
                39 => "NVFP4",
                40 => "Q1_0",
                41 => "Q2_0",
                _ => $"Unknown (file type {fileType})"
            };
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
        /// Reads one little-endian unsigned 32-bit metadata field exactly.
        /// </summary>
        private static async Task<uint> ReadMetadataUInt32Async(
            FileStream stream,
            string key,
            string stage,
            CancellationToken cancellationToken)
        {
            long offset = stream.Position;
            byte[] buffer = new byte[sizeof(uint)];

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

            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
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
        /// Holds only the display metadata retained while scanning one GGUF file.
        /// </summary>
        private sealed class GgufScanState
        {
            /// <summary>Gets retained pre-architecture context values by exact metadata key.</summary>
            internal Dictionary<string, PendingContextValue> PendingContextCandidates { get; } = new(
                StringComparer.Ordinal);

            /// <summary>Gets or sets whether general.name has already been consumed.</summary>
            internal bool HasModelNameMetadata { get; set; }

            /// <summary>Gets or sets whether general.architecture has already been consumed.</summary>
            internal bool HasArchitectureMetadata { get; set; }

            /// <summary>Gets or sets whether general.size_label has already been consumed.</summary>
            internal bool HasParameterSizeLabelMetadata { get; set; }

            /// <summary>Gets or sets whether general.file_type has already been consumed.</summary>
            internal bool HasFileTypeMetadata { get; set; }

            /// <summary>Gets or sets whether the exact architecture context has been consumed.</summary>
            internal bool HasContextLengthMetadata { get; set; }

            /// <summary>Gets or sets the optional model name.</summary>
            internal string? ModelName { get; set; }

            /// <summary>Gets or sets the required model architecture.</summary>
            internal string? Architecture { get; set; }

            /// <summary>Gets or sets the optional parameter size label.</summary>
            internal string? ParameterSizeLabel { get; set; }

            /// <summary>Gets or sets the optional GGUF file-type identifier.</summary>
            internal uint? FileType { get; set; }

            /// <summary>Gets or sets the optional architecture context length.</summary>
            internal ulong? ContextLength { get; set; }
        }

        /// <summary>
        /// Holds only the declared type and normalized value needed for one retained candidate.
        /// </summary>
        private readonly record struct PendingContextValue(
            GgufMetadataValueType ValueType,
            ulong? ContextLength);

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
