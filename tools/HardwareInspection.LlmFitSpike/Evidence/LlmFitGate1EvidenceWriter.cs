using System.Collections.Frozen;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace HardwareInspection.LlmFitSpike.Evidence;

public sealed class LlmFitGate1EvidenceWriter
{
    private const uint CreateNew = 1;
    private const uint DeleteAccess = 0x00010000;
    private const int FileDispositionInfoClass = 4;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const int FileRenameInfoClass = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeTemporary = 0x00000100;
    private const uint FileReadAttributes = 0x00000080;
    private const uint GenericWrite = 0x40000000;
    private const int MaximumWindowsPathCharacters = 32_768;
    private const uint OpenExisting = 3;
    private const uint ShareDelete = 0x00000004;
    private const uint ShareRead = 0x00000001;
    private const string ApprovedCandidateId = "llmfit-v1.1.9-win-x64";
    private const string ApprovedExpectedArchiveSha256 =
        "a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738";
    private const string ApprovedExpectedExecutableSha256 =
        "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19";
    private const string ApprovedExpectedVersion = "1.1.9";
    private const string ApprovedPeMachine = "AMD64";
    private const string ApprovedRawCaptureFileName = "llmfit-system.raw.json";
    private const string ApprovedReleaseCommit = "a02e13f1013ed69889ff44426a651bf7c68c292e";
    private const string ApprovedReportedVersion = "llmfit 1.1.9";
    private const string ApprovedSchemaVersion = "1.0";
    private const string InvalidEvidenceMessage =
        "The evidence record failed privacy-safe validation.";
    private static readonly string[] ApprovedSystemArguments =
        ["--no-dashboard", "--json", "system"];
    private static readonly string[] ApprovedVersionArguments = ["--version"];
    private static readonly FrozenSet<string> ApprovedAuthenticodeStatuses = new[]
    {
        "NotSigned",
        "PresentUnverified",
    }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> ApprovedDispositions = new[]
    {
        "Blocked",
        "Rejected",
        "FunctionalPassWithPackagingConcern",
        "AcceptedForFunctionalEvaluation",
    }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private static readonly UTF8Encoding Utf8WithoutBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly Action<string>? _beforePublish;

    public LlmFitGate1EvidenceWriter()
    {
    }

    internal LlmFitGate1EvidenceWriter(Action<string> beforePublish)
    {
        _beforePublish = beforePublish ?? throw new ArgumentNullException(nameof(beforePublish));
    }

    public async Task<string> WriteAsync(
        LlmFitGate1Evidence evidence,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();

        LlmFitGate1Evidence snapshot = CreateValidatedSnapshot(evidence);
        string json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        string fullOutputPath = ResolveOutputPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath)!;

        EnsureExistingPathComponentsAreOrdinary(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        EnsureExistingPathComponentsAreOrdinary(fullOutputPath);

        using StableDirectoryChain outputDirectoryChain = OpenStableDirectoryChain(
            outputDirectory);
        SafeFileHandle outputDirectoryHandle = outputDirectoryChain.LeafHandle;
        string stableOutputDirectory = GetStablePath(outputDirectoryHandle);
        if (!string.Equals(
                stableOutputDirectory,
                Path.TrimEndingDirectorySeparator(outputDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The evidence output directory identity changed.");
        }

        string temporaryFileName = Path.GetFileName(fullOutputPath) +
            ".tmp-" + Guid.NewGuid().ToString("N");
        string temporaryPath = Path.Combine(outputDirectory, temporaryFileName);
        SafeFileHandle? temporaryHandle = null;
        FileStream? temporaryStream = null;
        bool published = false;
        try
        {
            temporaryHandle = OpenOwnedTemporaryFile(temporaryPath);
            string stableTemporaryPath = GetStablePath(temporaryHandle);
            if (!string.Equals(
                    Path.GetDirectoryName(stableTemporaryPath),
                    stableOutputDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Path.GetFileName(stableTemporaryPath),
                    temporaryFileName,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The evidence temporary file identity changed.");
            }

            temporaryStream = new FileStream(
                temporaryHandle,
                FileAccess.Write,
                bufferSize: 4096,
                isAsync: true);
            temporaryHandle = null;

            byte[] bytes = Utf8WithoutBom.GetBytes(json);
            await temporaryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await temporaryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            temporaryStream.Flush(flushToDisk: true);
            cancellationToken.ThrowIfCancellationRequested();
            _beforePublish?.Invoke(temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();

            RenameHeldFile(
                temporaryStream.SafeFileHandle,
                outputDirectoryHandle,
                fullOutputPath);
            published = true;
            return fullOutputPath;
        }
        finally
        {
            if (temporaryStream is not null)
            {
                if (!published)
                {
                    MarkOwnedFileForDeletion(temporaryStream.SafeFileHandle);
                }

                await temporaryStream.DisposeAsync().ConfigureAwait(false);
            }
            else if (temporaryHandle is not null)
            {
                MarkOwnedFileForDeletion(temporaryHandle);
                temporaryHandle.Dispose();
            }
        }
    }

    private static LlmFitGate1Evidence CreateValidatedSnapshot(LlmFitGate1Evidence evidence)
    {
        string[] versionArguments;
        string[] systemArguments;
        string[] diagnosticCodes;
        try
        {
            versionArguments = evidence.VersionInvocationArguments?.ToArray() ??
                throw new InvalidDataException(InvalidEvidenceMessage);
            systemArguments = evidence.SystemInvocationArguments?.ToArray() ??
                throw new InvalidDataException(InvalidEvidenceMessage);
            diagnosticCodes = evidence.DiagnosticCodes?.ToArray() ??
                throw new InvalidDataException(InvalidEvidenceMessage);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch
        {
            throw new InvalidDataException(InvalidEvidenceMessage);
        }

        var snapshot = evidence with
        {
            VersionInvocationArguments = versionArguments,
            SystemInvocationArguments = systemArguments,
            DiagnosticCodes = diagnosticCodes,
        };
        Validate(snapshot);
        Array.Sort(diagnosticCodes, StringComparer.Ordinal);
        return snapshot;
    }

    private static void Validate(LlmFitGate1Evidence evidence)
    {
        bool valid =
            string.Equals(evidence.SchemaVersion, ApprovedSchemaVersion, StringComparison.Ordinal) &&
            ApprovedDispositions.Contains(evidence.Disposition) &&
            string.Equals(evidence.CandidateId, ApprovedCandidateId, StringComparison.Ordinal) &&
            string.Equals(evidence.ExpectedVersion, ApprovedExpectedVersion, StringComparison.Ordinal) &&
            (evidence.ReportedVersion is null ||
                string.Equals(evidence.ReportedVersion, ApprovedReportedVersion, StringComparison.Ordinal)) &&
            string.Equals(evidence.ReleaseCommit, ApprovedReleaseCommit, StringComparison.Ordinal) &&
            string.Equals(
                evidence.ExpectedArchiveSha256,
                ApprovedExpectedArchiveSha256,
                StringComparison.Ordinal) &&
            IsOptionalLowercaseHex(evidence.ObservedArchiveSha256, 64) &&
            string.Equals(
                evidence.ExpectedExecutableSha256,
                ApprovedExpectedExecutableSha256,
                StringComparison.Ordinal) &&
            IsOptionalLowercaseHex(evidence.ObservedExecutableSha256, 64) &&
            string.Equals(evidence.ExpectedPeMachine, ApprovedPeMachine, StringComparison.Ordinal) &&
            (evidence.ObservedPeMachine is null ||
                string.Equals(evidence.ObservedPeMachine, ApprovedPeMachine, StringComparison.Ordinal)) &&
            ApprovedAuthenticodeStatuses.Contains(evidence.AuthenticodeStatus) &&
            evidence.AuthenticodePresent ==
                string.Equals(evidence.AuthenticodeStatus, "PresentUnverified", StringComparison.Ordinal) &&
            evidence.VersionInvocationArguments.SequenceEqual(
                ApprovedVersionArguments,
                StringComparer.Ordinal) &&
            evidence.SystemInvocationArguments.SequenceEqual(
                ApprovedSystemArguments,
                StringComparer.Ordinal) &&
            evidence.GateStartedAtUtc.Offset == TimeSpan.Zero &&
            evidence.GateCompletedAtUtc.Offset == TimeSpan.Zero &&
            evidence.GateCompletedAtUtc >= evidence.GateStartedAtUtc &&
            evidence.DurationMilliseconds >= 0 &&
            evidence.DurationMilliseconds ==
                (evidence.GateCompletedAtUtc - evidence.GateStartedAtUtc).Ticks /
                    TimeSpan.TicksPerMillisecond &&
            IsOptionalPositive(evidence.CpuLogicalProcessorCount) &&
            IsOptionalNonnegativeFinite(evidence.TotalRamGiB) &&
            IsOptionalNonnegativeFinite(evidence.AvailableRamGiB) &&
            (evidence.TotalRamGiB is null ||
                evidence.AvailableRamGiB is null ||
                evidence.AvailableRamGiB <= evidence.TotalRamGiB) &&
            (!evidence.RequiredCpuRamPresent ||
                evidence.CpuLogicalProcessorCount is not null &&
                evidence.TotalRamGiB is not null &&
                evidence.AvailableRamGiB is not null) &&
            evidence.ReportedGpuCount >= 0 &&
            (evidence.GpuReported ||
                evidence.ReportedGpuCount == 0 && !evidence.IntelGpuReported) &&
            (!evidence.IntelGpuReported || evidence.GpuReported && evidence.ReportedGpuCount > 0) &&
            string.Equals(
                evidence.IntelNpuDetectionState,
                "DetectionUnavailable",
                StringComparison.Ordinal) &&
            (!evidence.VersionDashboardPortObserved || evidence.VersionCandidateSocketObserved) &&
            (!evidence.SystemDashboardPortObserved || evidence.SystemCandidateSocketObserved) &&
            HasValidRawCapturePair(evidence.RawSystemJsonFileName, evidence.RawSystemJsonSha256) &&
            (evidence.RawSystemJsonFileName is null || evidence.JsonValid) &&
            HasValidDiagnosticCodes(evidence.DiagnosticCodes) &&
            HasConsistentDisposition(evidence);

        if (!valid)
        {
            throw new InvalidDataException(InvalidEvidenceMessage);
        }
    }

    private static bool IsLowercaseHex(string? value, int expectedLength)
    {
        return value is not null && value.Length == expectedLength && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool IsOptionalLowercaseHex(string? value, int expectedLength)
    {
        return value is null || IsLowercaseHex(value, expectedLength);
    }

    private static bool IsOptionalPositive(int? value)
    {
        return value is null or > 0;
    }

    private static bool IsOptionalNonnegativeFinite(double? value)
    {
        return value is null || double.IsFinite(value.Value) && value.Value >= 0;
    }

    private static bool HasValidRawCapturePair(string? fileName, string? sha256)
    {
        if (fileName is null || sha256 is null)
        {
            return fileName is null && sha256 is null;
        }

        return string.Equals(fileName, ApprovedRawCaptureFileName, StringComparison.Ordinal) &&
            string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) &&
            !Path.IsPathRooted(fileName) &&
            fileName is not ("." or "..") &&
            !fileName.Contains(':', StringComparison.Ordinal) &&
            !fileName.Contains('/', StringComparison.Ordinal) &&
            !fileName.Contains('\\', StringComparison.Ordinal) &&
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            IsLowercaseHex(sha256, 64);
    }

    private static bool HasValidDiagnosticCodes(string[] codes)
    {
        return codes.All(static code =>
                code is not null && LlmFitGate1DiagnosticCodes.All.Contains(code)) &&
            codes.Distinct(StringComparer.Ordinal).Count() == codes.Length;
    }

    private static bool HasConsistentDisposition(LlmFitGate1Evidence evidence)
    {
        bool accepted = evidence.Disposition is
            "FunctionalPassWithPackagingConcern" or "AcceptedForFunctionalEvaluation";
        if (!accepted)
        {
            return true;
        }

        return evidence.ReportedVersion is not null &&
            string.Equals(
                evidence.ObservedArchiveSha256,
                evidence.ExpectedArchiveSha256,
                StringComparison.Ordinal) &&
            string.Equals(
                evidence.ObservedExecutableSha256,
                evidence.ExpectedExecutableSha256,
                StringComparison.Ordinal) &&
            string.Equals(evidence.ObservedPeMachine, evidence.ExpectedPeMachine, StringComparison.Ordinal) &&
            evidence.VersionExitCode == 0 &&
            evidence.SystemExitCode == 0 &&
            !evidence.ProcessStartFailed &&
            !evidence.SocketObservationFailed &&
            !evidence.TimedOut &&
            !evidence.Cancelled &&
            !evidence.StandardOutputTruncated &&
            !evidence.StandardErrorTruncated &&
            evidence.JsonValid &&
            evidence.RequiredCpuRamPresent &&
            !evidence.VersionCandidateSocketObserved &&
            !evidence.VersionDashboardPortObserved &&
            !evidence.SystemCandidateSocketObserved &&
            !evidence.SystemDashboardPortObserved &&
            !evidence.VersionCandidateProcessRemainedAfterExit &&
            !evidence.SystemCandidateProcessRemainedAfterExit &&
            evidence.RawSystemJsonFileName is not null &&
            evidence.RawSystemJsonSha256 is not null;
    }

    private static string ResolveOutputPath(string outputPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(outputPath);
            string? directory = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(directory) ||
                string.IsNullOrWhiteSpace(fileName) ||
                fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new InvalidDataException();
            }

            return fullPath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or IOException or NotSupportedException)
        {
            throw new InvalidDataException("The evidence output path is invalid.");
        }
    }

    private static StableDirectoryChain OpenStableDirectoryChain(string directory)
    {
        string fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string? root = Path.GetPathRoot(fullDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidDataException("The evidence output path is invalid.");
        }

        var componentPaths = new List<string> { root };
        string current = root;
        foreach (string segment in fullDirectory[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            componentPaths.Add(current);
        }

        var handles = new List<SafeFileHandle>(componentPaths.Count);
        try
        {
            foreach (string componentPath in componentPaths)
            {
                SafeFileHandle handle = OpenStableOutputDirectory(componentPath);
                handles.Add(handle);
                if (!string.Equals(
                        GetStablePath(handle),
                        Path.TrimEndingDirectorySeparator(componentPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "The evidence output directory identity changed.");
                }
            }

            return new StableDirectoryChain(handles.ToArray());
        }
        catch
        {
            foreach (SafeFileHandle handle in handles)
            {
                handle.Dispose();
            }

            throw;
        }
    }

    private static SafeFileHandle OpenStableOutputDirectory(string directory)
    {
        SafeFileHandle handle = CreateFile(
            directory,
            FileReadAttributes,
            ShareRead,
            0,
            OpenExisting,
            FileFlagBackupSemantics,
            0);
        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException(
                "The evidence output directory could not be opened safely.",
                new Win32Exception(errorCode));
        }

        return handle;
    }

    private static SafeFileHandle OpenOwnedTemporaryFile(string temporaryPath)
    {
        SafeFileHandle handle = CreateFile(
            temporaryPath,
            GenericWrite | DeleteAccess | FileReadAttributes,
            ShareRead | ShareDelete,
            0,
            CreateNew,
            FileAttributeNormal | FileAttributeTemporary |
                FileFlagOverlapped | FileFlagWriteThrough,
            0);
        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException(
                "The evidence temporary file could not be created safely.",
                new Win32Exception(errorCode));
        }

        return handle;
    }

    private static string GetStablePath(SafeFileHandle handle)
    {
        var pathBuffer = new StringBuilder(MaximumWindowsPathCharacters);
        uint characterCount = GetFinalPathNameByHandle(
            handle,
            pathBuffer,
            (uint)pathBuffer.Capacity,
            0);
        if (characterCount == 0 || characterCount >= pathBuffer.Capacity)
        {
            throw new IOException(
                "The evidence file identity could not be resolved.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        string finalPath = pathBuffer.ToString();
        if (finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + finalPath[8..];
        }

        return Path.TrimEndingDirectorySeparator(
            finalPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? finalPath[4..]
                : finalPath);
    }

    private static void RenameHeldFile(
        SafeFileHandle temporaryFile,
        SafeFileHandle outputDirectory,
        string destinationPath)
    {
        string stableOutputDirectory = GetStablePath(outputDirectory);
        if (!string.Equals(destinationPath, Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetDirectoryName(destinationPath),
                stableOutputDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The evidence destination file name is invalid.");
        }

        if (IntPtr.Size != 8)
        {
            throw new PlatformNotSupportedException(
                "Gate 1 evidence publication requires the configured x64 runtime.");
        }

        byte[] fileNameBytes = Encoding.Unicode.GetBytes(destinationPath);
        const int FileNameOffset = 20;
        int bufferSize = checked(FileNameOffset + fileNameBytes.Length + sizeof(char));
        nint information = Marshal.AllocHGlobal(bufferSize);
        try
        {
            for (int offset = 0; offset < bufferSize; offset++)
            {
                Marshal.WriteByte(information, offset, 0);
            }

            Marshal.WriteByte(information, 0, 1);
            Marshal.WriteIntPtr(information, 8, 0);
            Marshal.WriteInt32(information, 16, fileNameBytes.Length);
            Marshal.Copy(fileNameBytes, 0, information + FileNameOffset, fileNameBytes.Length);

            bool renamed = SetFileInformationByHandleForRename(
                temporaryFile,
                FileRenameInfoClass,
                information,
                (uint)bufferSize);
            GC.KeepAlive(outputDirectory);
            if (!renamed)
            {
                throw new IOException(
                    "The evidence file could not be atomically published.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(information);
        }
    }

    private static void MarkOwnedFileForDeletion(SafeFileHandle ownedFile)
    {
        if (ownedFile.IsInvalid || ownedFile.IsClosed)
        {
            return;
        }

        var information = new FileDispositionInformation { DeleteFile = true };
        if (!SetFileInformationByHandleForDisposition(
                ownedFile,
                FileDispositionInfoClass,
                ref information,
                (uint)Marshal.SizeOf<FileDispositionInformation>()))
        {
            throw new IOException(
                "The owned evidence temporary file could not be removed.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static void EnsureExistingPathComponentsAreOrdinary(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidDataException("The evidence output path is invalid.");
        }

        string current = Path.TrimEndingDirectorySeparator(root);
        string relativePath = fullPath[root.Length..];
        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                index < segments.Length - 1 && (attributes & FileAttributes.Directory) == 0)
            {
                throw new InvalidDataException(
                    "The evidence output path contains a non-ordinary component.");
            }
        }
    }

    private sealed class StableDirectoryChain : IDisposable
    {
        private readonly SafeFileHandle[] _handles;

        internal StableDirectoryChain(SafeFileHandle[] handles)
        {
            _handles = handles;
        }

        internal SafeFileHandle LeafHandle => _handles[^1];

        public void Dispose()
        {
            for (int index = _handles.Length - 1; index >= 0; index--)
            {
                _handles[index].Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInformation
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool DeleteFile;
    }

#pragma warning disable SYSLIB1054 // Project policy forbids the unsafe blocks required by LibraryImport.
#pragma warning disable CA1838 // Unsafe character buffers are forbidden by this project's build policy.
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        [Out] StringBuilder filePath,
        uint filePathCharacters,
        uint flags);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "SetFileInformationByHandle",
        ExactSpelling = true,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandleForRename(
        SafeFileHandle file,
        int fileInformationClass,
        nint fileInformation,
        uint bufferSize);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "SetFileInformationByHandle",
        ExactSpelling = true,
        SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandleForDisposition(
        SafeFileHandle file,
        int fileInformationClass,
        ref FileDispositionInformation fileInformation,
        uint bufferSize);
#pragma warning restore CA1838
#pragma warning restore SYSLIB1054
}
