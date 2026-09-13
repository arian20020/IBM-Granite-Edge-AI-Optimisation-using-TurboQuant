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

        // Fixed-header fields begin at these byte offsets.
        private const long GgufMagicOffset = 0;
        private const long GgufVersionOffset = 4;
        private const long GgufTensorCountOffset = 8;
        private const long GgufMetadataEntryCountOffset = 16;

        // Version 2 and 3 is the GGUF container format currently supported.
        private const uint MinimumSupportedGgufVersion = 2;
        private const uint MaximumSupportedGgufVersion = 3;

        // Bound the attacker-controlled top-level metadata loop.
        private const ulong MaxMetadataEntryCount = 1_000_000;

        // GGUF keys have a specification-defined maximum of 2^16 - 1 bytes.
        private const ulong MaxMetadataKeyByteLength = 65_535;

        // Application safety policy: cap aggregate key decoding and validation work.
        private const ulong MaxTotalMetadataKeyByteLength = 65_535;

        // Application safety policy: no single metadata string may exceed 16 MiB.
        private const ulong MaxMetadataStringByteLength = 16 * 1024 * 1024;

        // Application safety policy: bound one declared array before any iteration.
        private const ulong MaxArrayElementCount = 1_000_000;

        // Application safety policy: bound aggregate array work across one scan.
        private const ulong MaxTotalArrayElementCount = 4_000_000;

        // Validate Boolean-array payloads in bounded chunks instead of per element.
        private const int BooleanArrayValidationBufferSize = 4 * 1024;

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

        // optional internal checkpoint used to coordinate cancellation after the file is open
        private readonly Func<ValueTask>? _scanStartedCheckpointAsync;

        /// <summary>
        /// creates the production scanner without an external scan checkpoint
        /// </summary>
        internal GgufQuickScanner()
        {
        }

        /// <summary>
        /// creates a scanner with a controlled checkpoint immediately after file opening
        /// </summary>
        internal GgufQuickScanner(
            Func<ValueTask> scanStartedCheckpointAsync)
        {
            ArgumentNullException.ThrowIfNull(scanStartedCheckpointAsync);
            _scanStartedCheckpointAsync = scanStartedCheckpointAsync;
        }

        /// <summary>
        /// Scans the selected GGUF file and returns the completed result.
        /// </summary>
        internal async Task<ModelQuickScanResult> ScanAsync(
            string modelFilePath,
            CancellationToken cancellationToken)
        {
            // reject a null, empty, or whitespace-only path
            ArgumentException.ThrowIfNullOrWhiteSpace(modelFilePath);

            // Stop immediately when cancellation was already requested
            cancellationToken.ThrowIfCancellationRequested();

            FileStream stream;
            try
            {
                // Open the existing model file for asynchronous, read-only access.
                stream = new FileStream(
                    modelFilePath,
                    new FileStreamOptions
                    {
                        // the selected file must already exist
                        Mode = FileMode.Open,

                        // the scanner may inspect but not alter the model
                        Access = FileAccess.Read,

                        // allow another process to read the model simultaneously
                        Share = FileShare.Read,

                        // Prepare for asynchronous, beginning-to-end reading.
                        Options =
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan
                    });
            }
            catch (FileNotFoundException exception)
            {
                return CreateFileOpenFailure(
                    "file-not-found",
                    "The selected GGUF file could not be found.",
                    exception);
            }
            catch (DirectoryNotFoundException exception)
            {
                return CreateFileOpenFailure(
                    "file-not-found",
                    "The selected GGUF file could not be found.",
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                return CreateFileOpenFailure(
                    "file-access-denied",
                    "The selected GGUF file could not be opened because access was denied.",
                    exception);
            }
            catch (IOException exception)
            {
                return CreateFileOpenFailure(
                    "file-read-error",
                    "The selected GGUF file could not be opened for reading.",
                    exception);
            }

            await using (stream)
            {
                try
                {
                    // capture the scan-time write timestamp while the open
                    // read handle continues to exclude writers and replacement
                    DateTimeOffset fileLastWriteTimeUtc = new(
                        File.GetLastWriteTimeUtc(modelFilePath));

                    if (_scanStartedCheckpointAsync is not null)
                    {
                        await _scanStartedCheckpointAsync();
                    }

                    return await ScanOpenedFileAsync(
                        stream,
                        modelFilePath,
                        fileLastWriteTimeUtc,
                        cancellationToken);
                }
                catch (GgufFormatException exception)
                {
                    // Translate only expected structural input failures at this boundary.
                    return CreateFormatFailure(exception);
                }
            }
        }

        /// <summary>
        /// Validates and scans an already opened GGUF file.
        /// </summary>
        private static async Task<ModelQuickScanResult> ScanOpenedFileAsync(
            FileStream stream,
            string modelFilePath,
            DateTimeOffset fileLastWriteTimeUtc,
            CancellationToken cancellationToken)
        {
            // Read and validate all 24 bytes of the fixed GGUF header.
            GgufHeader header = await ReadHeaderAsync(stream, cancellationToken);

            // reject versions whose structure this scanner cannot interpret
            if (header.Version == 1)
            {
                return ModelQuickScanResult.CreateFailure(
                    failureCode: "obsolete-version",
                    userMessage:
                        "This model uses the obsolete GGUF version 1 format. "
                        + "Convert or download the model as GGUF version 2 or 3.",
                    technicalMessage:
                        $"GGUF version at file offset {GgufVersionOffset} is 1; "
                        + "the application supports versions 2 and 3.");
            }

            if (header.Version < MinimumSupportedGgufVersion || header.Version > MaximumSupportedGgufVersion)
            {
                return ModelQuickScanResult.CreateFailure(
                    failureCode: "unsupported-version",
                    userMessage:
                        "This model uses a GGUF version that is not supported by "
                        + "the current application.",
                    technicalMessage:
                        $"GGUF version at file offset {GgufVersionOffset} is " +
                        $"{header.Version}; supported versions are 2 and 3.");
            }

            // reject an unreasonable top-level loop before reading any entry
            if (header.MetadataEntryCount > MaxMetadataEntryCount)
            {
                return ModelQuickScanResult.CreateFailure(
                    failureCode: "excessive-metadata-count",
                    userMessage:
                        "The GGUF file declares too many metadata entries.",
                    technicalMessage:
                        $"GGUF metadata entry count at file offset " +
                        $"{GgufMetadataEntryCountOffset} is " +
                        $"{header.MetadataEntryCount:N0}; the scanner limit is " +
                        $"{MaxMetadataEntryCount:N0}.");
            }

            GgufScanState scanState = await ReadMetadataAsync(
                stream,
                header.MetadataEntryCount,
                cancellationToken);

            // a blank architecture is as unusable as an absent architecture
            if (string.IsNullOrWhiteSpace(scanState.Architecture))
            {
                string architectureDiagnostic =
                    scanState.ArchitectureStringLengthOffset is long stringLengthOffset
                        ? $"general.architecture value declared by the string length " +
                            $"at file offset {stringLengthOffset} was blank; expected " +
                            "nonblank UTF-8 architecture text."
                        : "general.architecture metadata was absent; expected one " +
                            "nonblank String value.";
                return ModelQuickScanResult.CreateFailure(
                    failureCode: "missing-required-architecture",
                    userMessage:
                        "The GGUF file does not identify its model architecture.",
                    technicalMessage:
                        $"The GGUF fixed header declares {header.TensorCount} tensor " +
                        $"descriptors and {header.MetadataEntryCount} metadata entries, " +
                        $"but {architectureDiagnostic}");
            }

            // a missing or blank optional name falls back to the selected file name
            string modelName = string.IsNullOrWhiteSpace(scanState.ModelName)
                ? Path.GetFileName(modelFilePath)
                : scanState.ModelName;
            return CreateSuccessResult(
                stream,
                header,
                scanState,
                modelName,
                fileLastWriteTimeUtc);
        }

        /// <summary>
        /// creates one controlled result for an exception raised while opening the file
        /// </summary>
        private static ModelQuickScanResult CreateFileOpenFailure(
            string failureCode,
            string userMessage,
            Exception exception)
        {
            return ModelQuickScanResult.CreateFailure(
                failureCode,
                userMessage,
                $"Opening the selected GGUF file failed with " +
                    $"{exception.GetType().Name}.");
        }

        /// <summary>
        /// creates one controlled result for a scanner-local structural failure
        /// </summary>
        private static ModelQuickScanResult CreateFormatFailure(
            GgufFormatException exception)
        {
            return ModelQuickScanResult.CreateFailure(
                exception.FailureCode,
                exception.UserMessage,
                exception.TechnicalMessage);
        }

        /// <summary>
        /// Reads and validates every field in the fixed 24-byte GGUF header.
        /// </summary>
        private static async Task<GgufHeader> ReadHeaderAsync(
            FileStream stream,
            CancellationToken cancellationToken)
        {
            // read the required signature before decoding any numeric header field
            byte[] actualMagic = new byte[GgufMagicLength];
            await ReadHeaderFieldAsync(
                stream,
                actualMagic,
                fieldName: "magic",
                fieldOffset: GgufMagicOffset,
                cancellationToken);

            // read every remaining fixed-header field before classifying complete input
            uint version = await ReadUInt32Async(
                stream,
                fieldName: "version",
                fieldOffset: GgufVersionOffset,
                cancellationToken);
            ulong tensorCount = await ReadUInt64Async(
                stream,
                fieldName: "tensor count",
                fieldOffset: GgufTensorCountOffset,
                cancellationToken);
            ulong metadataEntryCount = await ReadUInt64Async(
                stream,
                fieldName: "metadata entry count",
                fieldOffset: GgufMetadataEntryCountOffset,
                cancellationToken);

            // Reject a complete but non-GGUF signature with actual and expected bytes.
            if (!actualMagic.AsSpan().SequenceEqual(ExpectedGgufMagic))
            {
                throw new GgufFormatException(
                    failureCode: "invalid-magic",
                    userMessage: "The selected file is not a valid GGUF model.",
                    technicalMessage:
                        $"Expected GGUF magic bytes {BitConverter.ToString(ExpectedGgufMagic)} " +
                        $"at file offset {GgufMagicOffset}, but found " +
                        $"{BitConverter.ToString(actualMagic)}.");
            }

            // return the decoded header without reading tensor descriptors or metadata values
            return new GgufHeader(version, tensorCount, metadataEntryCount);
        }

        /// <summary>
        /// Consumes bounded metadata entries and returns only retained display state.
        /// </summary>
        private static async Task<GgufScanState> ReadMetadataAsync(
            FileStream stream,
            ulong metadataEntryCount,
            CancellationToken cancellationToken)
        {
            // keep retained display metadata and the aggregate array budget local to this scan
            GgufScanState scanState = new();
            ulong totalArrayElementCount = 0;
            ulong totalMetadataKeyByteLength = 0;

            // Consume each declared entry once, checking cancellation between entries.
            for (ulong entryIndex = 0;
                entryIndex < metadataEntryCount;
                entryIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GgufMetadataKey metadataKey = await ReadMetadataKeyAsync(
                    stream,
                    entryIndex,
                    totalMetadataKeyByteLength,
                    cancellationToken);
                string key = metadataKey.Value;
                totalMetadataKeyByteLength = metadataKey.TotalByteLength;
                long valueTypeOffset = stream.Position;
                GgufMetadataValueType valueType = await ReadMetadataTypeAsync(
                    stream,
                    key,
                    cancellationToken);
                totalArrayElementCount = await ReadKnownMetadataValueAsync(
                    stream,
                    key,
                    valueType,
                    valueTypeOffset,
                    scanState,
                    totalArrayElementCount,
                    cancellationToken);
            }

            return scanState;
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

            // decode the wire-format bytes explicitly rather than relying on machine endianness
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

            // decode the wire-format bytes explicitly rather than relying on machine endianness
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
                // read exactly the bytes required by the fixed header field
                await stream.ReadExactlyAsync(buffer, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                // Bound the bytes read to this field when the exact read reaches EOF.
                long bytesRead = Math.Clamp(
                    stream.Position - fieldOffset,
                    0,
                    buffer.Length);

                // preserve field context so the result identifies the malformed input
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
        private static async Task<GgufMetadataKey> ReadMetadataKeyAsync(
            FileStream stream,
            ulong entryIndex,
            ulong totalMetadataKeyByteLength,
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

            // compare by subtraction before allocation so aggregate work cannot overflow
            if (totalMetadataKeyByteLength > MaxTotalMetadataKeyByteLength ||
                keyByteLength >
                    MaxTotalMetadataKeyByteLength - totalMetadataKeyByteLength)
            {
                ulong attemptedTotal = checked(
                    totalMetadataKeyByteLength + keyByteLength);
                throw new GgufFormatException(
                    failureCode: "excessive-metadata-key-bytes",
                    userMessage:
                        "The GGUF file declares too much metadata key text.",
                    technicalMessage:
                        $"Metadata entry {entryIndex} key length at file offset " +
                        $"{lengthOffset} would raise the key-byte total from " +
                        $"{totalMetadataKeyByteLength:N0} by " +
                        $"{keyByteLength:N0} to {attemptedTotal:N0} bytes; the " +
                        $"scanner limit is {MaxTotalMetadataKeyByteLength:N0} bytes.");
            }

            ulong updatedTotalMetadataKeyByteLength =
                totalMetadataKeyByteLength + keyByteLength;
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

            return new GgufMetadataKey(
                key,
                updatedTotalMetadataKeyByteLength);
        }

        /// <summary>
        /// Validates the GGUF hierarchical lower_snake_case metadata-key grammar.
        /// </summary>
        private static void ValidateMetadataKey(string key, ulong entryIndex, long keyOffset)
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
        /// creates a safe diagnostic for a leading, trailing, or adjacent dot
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
            GgufArrayReadBuffers arrayReadBuffers,
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
                    long stringLengthOffset = stream.Position;
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
                                $"{stringByteLength:N0} bytes at file offset " +
                                $"{stringLengthOffset}; the scanner limit is " +
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
                        arrayReadBuffers,
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
            long valueTypeOffset,
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
                            scanState.ArrayReadBuffers,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.String,
                        valueTypeOffset,
                        "invalid-architecture-type");
                    scanState.ArchitectureStringLengthOffset = stream.Position;
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
                            scanState.ArrayReadBuffers,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.String,
                        valueTypeOffset,
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
                            scanState.ArrayReadBuffers,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.String,
                        valueTypeOffset,
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
                            scanState.ArrayReadBuffers,
                            cancellationToken);
                    }

                    RequireMetadataType(
                        key,
                        valueType,
                        GgufMetadataValueType.UInt32,
                        valueTypeOffset,
                        "invalid-file-type");
                    scanState.FileType = await ReadMetadataUInt32Async(
                        stream,
                        key,
                        stage: "file type",
                        cancellationToken);
                    scanState.HasFileTypeMetadata = true;
                    return totalArrayElementCount;
            }

            // once architecture is known, process its exact context key immediately
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
                        scanState.ArrayReadBuffers,
                        cancellationToken);
                }

                return await ReadContextLengthAsync(
                    stream,
                    key,
                    valueType,
                    valueTypeOffset,
                    scanState,
                    totalArrayElementCount,
                    cancellationToken);
            }

            // before architecture is known, retain only possible context candidates
            if (!scanState.HasArchitectureMetadata &&
                key.EndsWith(ContextLengthMetadataKeySuffix, StringComparison.Ordinal))
            {
                return await ReadPendingContextCandidateAsync(
                    stream,
                    key,
                    valueType,
                    valueTypeOffset,
                    scanState,
                    totalArrayElementCount,
                    cancellationToken);
            }

            return await SkipMetadataValueAsync(
                stream,
                key,
                valueType,
                totalArrayElementCount,
                scanState.ArrayReadBuffers,
                cancellationToken);
        }

        /// <summary>
        /// Reads one exact architecture context value and normalizes supported unsigned widths.
        /// </summary>
        private static async Task<ulong> ReadContextLengthAsync(
            FileStream stream,
            string key,
            GgufMetadataValueType valueType,
            long valueTypeOffset,
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

            throw CreateInvalidContextTypeException(
                key,
                valueType,
                valueTypeOffset);
        }

        /// <summary>
        /// Consumes and retains one bounded pre-architecture context candidate.
        /// </summary>
        private static async Task<ulong> ReadPendingContextCandidateAsync(
            FileStream stream,
            string key,
            GgufMetadataValueType valueType,
            long valueTypeOffset,
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
                    scanState.ArrayReadBuffers,
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
                        $"the scanner limit is {MaxPendingContextCandidateCount}. " +
                        $"Its value type begins at file offset {valueTypeOffset}.");
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
                    scanState.ArrayReadBuffers,
                    cancellationToken);
            }

            scanState.PendingContextCandidates[key] = new PendingContextValue(
                valueType,
                valueTypeOffset,
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
                        candidate.Value.ValueType,
                        candidate.Value.ValueTypeOffset);
                }

                scanState.ContextLength = candidate.Value.ContextLength;
                scanState.HasContextLengthMetadata = true;
                return;
            }
        }

        /// <summary>
        /// creates the stable failure used when an exact context key has a wrong type
        /// </summary>
        private static GgufFormatException CreateInvalidContextTypeException(
            string key,
            GgufMetadataValueType valueType,
            long valueTypeOffset)
        {
            return new GgufFormatException(
                failureCode: "invalid-context-type",
                userMessage:
                    "The GGUF file contains an invalid context length value.",
                technicalMessage:
                    $"Metadata key '{key}' has value type {valueType} at file " +
                    $"offset {valueTypeOffset}; expected UInt32 or UInt64.");
        }

        /// <summary>
        /// Compares an architecture context key exactly without constructing a large temporary string.
        /// </summary>
        private static bool IsArchitectureContextLengthKey(
            string key,
            string architecture)
        {
            // check length and suffix first so a retained multi-megabyte architecture
            // cannot force a matching-size allocation or prefix comparison per later entry
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
            long valueTypeOffset,
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
                    $"Metadata key '{key}' has value type {actualType} at file " +
                    $"offset {valueTypeOffset}; expected {expectedType}.");
        }

        /// <summary>
        /// Reads a bounded retained metadata string with strict UTF-8 validation.
        /// </summary>
        private static async Task<string> ReadRetainedStringAsync(
            FileStream stream,
            string key,
            CancellationToken cancellationToken)
        {
            long stringLengthOffset = stream.Position;
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
                        $"{stringByteLength:N0} bytes at file offset " +
                        $"{stringLengthOffset}; the scanner limit is " +
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
        /// creates the completed success result from one scan's retained state
        /// </summary>
        private static ModelQuickScanResult CreateSuccessResult(
            FileStream stream,
            GgufHeader header,
            GgufScanState scanState,
            string modelName,
            DateTimeOffset fileLastWriteTimeUtc)
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
                header.Version,
                fileLastWriteTimeUtc);
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
            GgufArrayReadBuffers buffers,
            CancellationToken cancellationToken)
        {
            return await ConsumeArrayCoreAsync(
                stream,
                key,
                nestingDepth,
                parentElementIndex: null,
                totalArrayElementCount,
                buffers,
                cancellationToken);
        }

        /// <summary>
        /// Recursively consumes arrays with buffers shared by every child.
        /// </summary>
        private static async ValueTask<ulong> ConsumeArrayCoreAsync(
            FileStream stream,
            string key,
            int nestingDepth,
            ulong? parentElementIndex,
            ulong totalArrayElementCount,
            GgufArrayReadBuffers buffers,
            CancellationToken cancellationToken)
        {
            long arrayOffset = stream.Position;
            if (nestingDepth > MaxArrayNestingDepth)
            {
                throw new GgufFormatException(
                    failureCode: "excessive-array-depth",
                    userMessage:
                        "The GGUF file contains metadata arrays nested too deeply.",
                    technicalMessage:
                        $"Metadata key '{key}' reached " +
                        $"{FormatArrayLocation(nestingDepth, parentElementIndex)} at file " +
                        $"offset {arrayOffset}; the scanner " +
                        $"limit is {MaxArrayNestingDepth}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            GgufMetadataValueType elementType = await ReadArrayMetadataTypeAsync(
                stream,
                key,
                nestingDepth,
                parentElementIndex,
                buffers.Type,
                cancellationToken);
            long elementCountOffset = stream.Position;
            ulong elementCount = await ReadArrayUInt64Async(
                stream,
                key,
                nestingDepth,
                parentElementIndex,
                buffers.UInt64,
                cancellationToken);

            if (elementCount > MaxArrayElementCount)
            {
                throw new GgufFormatException(
                    failureCode: "excessive-array-count",
                    userMessage:
                        "The GGUF file contains a metadata array that is too large.",
                    technicalMessage:
                        $"Metadata key '{key}' " +
                        $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                        $"declares {elementCount:N0} elements at file offset " +
                        $"{elementCountOffset}; the per-array scanner limit is " +
                        $"{MaxArrayElementCount:N0}.");
            }

            // compare by subtraction so attacker-controlled addition cannot overflow
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
                        $"elements at file offset {elementCountOffset}; the total " +
                        "scanner limit is " +
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
                SkipValidatedArrayBytes(
                    stream,
                    payloadByteCount,
                    key,
                    nestingDepth,
                    parentElementIndex);
                return totalArrayElementCount;
            }

            if (elementType == GgufMetadataValueType.Boolean)
            {
                if (elementCount == 0)
                {
                    return totalArrayElementCount;
                }

                await ConsumeBooleanArrayAsync(
                    stream,
                    key,
                    nestingDepth,
                    parentElementIndex,
                    elementCount,
                    buffers.Boolean,
                    cancellationToken);
                return totalArrayElementCount;
            }

            if (elementType == GgufMetadataValueType.String)
            {
                await ConsumeStringArrayAsync(
                    stream,
                    key,
                    nestingDepth,
                    parentElementIndex,
                    elementCount,
                    buffers.UInt64,
                    cancellationToken);
                return totalArrayElementCount;
            }

            // Only nested arrays remain after fixed-width, Boolean, and String handling.
            for (ulong elementIndex = 0;
                elementIndex < elementCount;
                elementIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalArrayElementCount = await ConsumeArrayCoreAsync(
                    stream,
                    key,
                    nestingDepth + 1,
                    parentElementIndex: elementIndex,
                    totalArrayElementCount,
                    buffers,
                    cancellationToken);
            }

            return totalArrayElementCount;
        }

        /// <summary>
        /// Validates a Boolean-array payload with one bounded reusable buffer.
        /// </summary>
        private static async ValueTask ConsumeBooleanArrayAsync(
            FileStream stream,
            string key,
            int nestingDepth,
            ulong? parentElementIndex,
            ulong elementCount,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            long payloadOffset = stream.Position;
            long remainingBytes = stream.Length - payloadOffset;
            if (remainingBytes < 0 ||
                elementCount > (ulong)remainingBytes)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage:
                        $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                        "Boolean payload",
                    offset: payloadOffset,
                    expectedBytes: elementCount,
                    actualBytes: Math.Max(remainingBytes, 0));
            }

            ulong consumedElementCount = 0;

            while (consumedElementCount < elementCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int chunkLength = (int)Math.Min(
                    elementCount - consumedElementCount,
                    (ulong)buffer.Length);
                long chunkOffset = stream.Position;

                try
                {
                    await stream.ReadExactlyAsync(
                        buffer.AsMemory(0, chunkLength),
                        cancellationToken);
                }
                catch (EndOfStreamException exception)
                {
                    throw CreateTruncatedMetadataException(
                        key,
                        stage:
                            $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                            "Boolean chunk starting " +
                            $"at element {consumedElementCount:N0}",
                        offset: chunkOffset,
                        expectedBytes: (ulong)chunkLength,
                        actualBytes: Math.Clamp(
                            stream.Position - chunkOffset,
                            0,
                            chunkLength),
                        exception);
                }

                for (int chunkIndex = 0; chunkIndex < chunkLength; chunkIndex++)
                {
                    byte value = buffer[chunkIndex];
                    if (value > 1)
                    {
                        ulong elementIndex =
                            consumedElementCount + (ulong)chunkIndex;
                        long valueOffset = chunkOffset + chunkIndex;
                        throw new GgufFormatException(
                            failureCode: "invalid-boolean-value",
                            userMessage:
                                "The GGUF file contains an invalid boolean value.",
                            technicalMessage:
                                $"Metadata key '{key}' " +
                                $"{FormatArrayLocation(
                                    nestingDepth,
                                    parentElementIndex)} " +
                                $"element {elementIndex:N0} has boolean byte {value} " +
                                $"at file offset {valueOffset}; expected 0 or 1.");
                    }
                }

                consumedElementCount += (ulong)chunkLength;
            }
        }

        /// <summary>
        /// Consumes a String array with one reusable length buffer and no empty seeks.
        /// </summary>
        private static async ValueTask ConsumeStringArrayAsync(
            FileStream stream,
            string key,
            int nestingDepth,
            ulong? parentElementIndex,
            ulong elementCount,
            byte[] lengthBuffer,
            CancellationToken cancellationToken)
        {
            for (ulong elementIndex = 0;
                elementIndex < elementCount;
                elementIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long lengthOffset = stream.Position;

                try
                {
                    await stream.ReadExactlyAsync(
                        lengthBuffer,
                        cancellationToken);
                }
                catch (EndOfStreamException exception)
                {
                    throw CreateTruncatedMetadataException(
                        key,
                        stage:
                            $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                            $"element {elementIndex:N0} string length",
                        lengthOffset,
                        expectedBytes: (ulong)lengthBuffer.Length,
                        actualBytes: Math.Clamp(
                            stream.Position - lengthOffset,
                            0,
                            lengthBuffer.Length),
                        exception);
                }

                ulong stringByteLength =
                    BinaryPrimitives.ReadUInt64LittleEndian(lengthBuffer);
                if (stringByteLength > MaxMetadataStringByteLength)
                {
                    throw new GgufFormatException(
                        failureCode: "metadata-string-too-long",
                        userMessage:
                            "The GGUF file contains a metadata string that is too long.",
                        technicalMessage:
                            $"Metadata key '{key}' " +
                            $"{FormatArrayLocation(
                                nestingDepth,
                                parentElementIndex)} " +
                            $"element {elementIndex:N0} declares a string of " +
                            $"{stringByteLength:N0} bytes at file offset " +
                            $"{lengthOffset}; the scanner limit is " +
                            $"{MaxMetadataStringByteLength:N0} bytes.");
                }

                if (stringByteLength == 0)
                {
                    continue;
                }

                SkipValidatedStringArrayBytes(
                    stream,
                    stringByteLength,
                    key,
                    nestingDepth,
                    parentElementIndex,
                    elementIndex);
            }
        }

        /// <summary>
        /// Reads one array value type into a buffer shared across recursive children.
        /// </summary>
        private static async ValueTask<GgufMetadataValueType> ReadArrayMetadataTypeAsync(
            FileStream stream,
            string key,
            int nestingDepth,
            ulong? parentElementIndex,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            long typeOffset = stream.Position;

            try
            {
                await stream.ReadExactlyAsync(
                    buffer.AsMemory(0, sizeof(uint)),
                    cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage:
                        $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                        "element type",
                    typeOffset,
                    expectedBytes: sizeof(uint),
                    actualBytes: Math.Clamp(
                        stream.Position - typeOffset,
                        0,
                        sizeof(uint)),
                    exception);
            }

            uint rawType = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            if (rawType > (uint)GgufMetadataValueType.Float64)
            {
                throw new GgufFormatException(
                    failureCode: "unsupported-metadata-type",
                    userMessage:
                        "The GGUF file uses an unsupported metadata value type.",
                    technicalMessage:
                        $"Metadata key '{key}' " +
                        $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                        $"element type is value type {rawType} " +
                        $"at file offset {typeOffset}; supported GGUF types are " +
                        "0 through 12.");
            }

            return (GgufMetadataValueType)rawType;
        }

        /// <summary>
        /// Reads one array UInt64 into a buffer shared across recursive children.
        /// </summary>
        private static async ValueTask<ulong> ReadArrayUInt64Async(
            FileStream stream,
            string key,
            int nestingDepth,
            ulong? parentElementIndex,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            long offset = stream.Position;

            try
            {
                await stream.ReadExactlyAsync(
                    buffer.AsMemory(0, sizeof(ulong)),
                    cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage:
                        $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                        "element count",
                    offset,
                    expectedBytes: sizeof(ulong),
                    actualBytes: Math.Clamp(
                        stream.Position - offset,
                        0,
                        sizeof(ulong)),
                    exception);
            }

            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        /// <summary>
        /// Skips one fixed-width array payload without success-path diagnostic strings.
        /// </summary>
        private static void SkipValidatedArrayBytes(
            FileStream stream,
            ulong byteCount,
            string key,
            int nestingDepth,
            ulong? parentElementIndex)
        {
            long offset = stream.Position;
            long remainingBytes = stream.Length - offset;
            if (remainingBytes < 0 ||
                byteCount > (ulong)remainingBytes)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage: FormatArrayPayloadStage(
                        nestingDepth,
                        parentElementIndex),
                    offset,
                    expectedBytes: byteCount,
                    actualBytes: Math.Max(remainingBytes, 0));
            }

            if (byteCount != 0)
            {
                stream.Seek(checked((long)byteCount), SeekOrigin.Current);
            }
        }

        /// <summary>
        /// Skips one String-array payload without success-path diagnostic strings.
        /// </summary>
        private static void SkipValidatedStringArrayBytes(
            FileStream stream,
            ulong byteCount,
            string key,
            int nestingDepth,
            ulong? parentElementIndex,
            ulong elementIndex)
        {
            long offset = stream.Position;
            long remainingBytes = stream.Length - offset;
            if (remainingBytes < 0 ||
                byteCount > (ulong)remainingBytes)
            {
                throw CreateTruncatedMetadataException(
                    key,
                    stage:
                        $"{FormatArrayLocation(nestingDepth, parentElementIndex)} " +
                        $"element {elementIndex:N0} string payload",
                    offset,
                    expectedBytes: byteCount,
                    actualBytes: Math.Max(remainingBytes, 0));
            }

            stream.Seek(checked((long)byteCount), SeekOrigin.Current);
        }

        /// <summary>
        /// Formats nested-array context only when a failure needs a diagnostic.
        /// </summary>
        private static string FormatArrayLocation(
            int nestingDepth,
            ulong? parentElementIndex)
        {
            return parentElementIndex is ulong parentIndex
                ? $"array depth {nestingDepth} parent element {parentIndex:N0}"
                : $"array depth {nestingDepth}";
        }

        /// <summary>
        /// Preserves the fixed-array payload stage while adding optional parent context.
        /// </summary>
        private static string FormatArrayPayloadStage(
            int nestingDepth,
            ulong? parentElementIndex)
        {
            return parentElementIndex is ulong parentIndex
                ? $"array payload at depth {nestingDepth}, parent element " +
                    $"{parentIndex:N0}"
                : $"array payload at depth {nestingDepth}";
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
        /// creates a stable structural failure for incomplete metadata bytes
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
        /// Reuses primitive buffers throughout the scan, across root arrays and nested children.
        /// </summary>
        private sealed class GgufArrayReadBuffers
        {
            private byte[]? boolean;

            /// <summary>Gets the shared metadata-type buffer.</summary>
            internal byte[] Type { get; } = new byte[sizeof(uint)];

            /// <summary>Gets the shared UInt64 and String-length buffer.</summary>
            internal byte[] UInt64 { get; } = new byte[sizeof(ulong)];

            /// <summary>Gets the lazily allocated Boolean validation chunk.</summary>
            internal byte[] Boolean =>
                boolean ??= new byte[BooleanArrayValidationBufferSize];
        }

        /// <summary>
        /// Holds one decoded metadata key and the updated aggregate key-byte budget.
        /// </summary>
        private readonly record struct GgufMetadataKey(
            string Value,
            ulong TotalByteLength);

        /// <summary>
        /// Holds only the display metadata retained while scanning one GGUF file.
        /// </summary>
        private sealed class GgufScanState
        {
            /// <summary>Gets buffers reused by every metadata array in this scan.</summary>
            internal GgufArrayReadBuffers ArrayReadBuffers { get; } = new();

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

            /// <summary>Gets or sets the byte offset of the architecture string length.</summary>
            internal long? ArchitectureStringLengthOffset { get; set; }

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
            long ValueTypeOffset,
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
                // retain the stable failure code required by the result contract
                FailureCode = failureCode;

                // retain the concise message intended for the quick-scan caller
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
