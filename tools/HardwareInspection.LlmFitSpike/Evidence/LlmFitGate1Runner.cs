using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using HardwareInspection.LlmFitSpike.Candidate;
using HardwareInspection.LlmFitSpike.Command;
using HardwareInspection.LlmFitSpike.Execution;
using HardwareInspection.LlmFitSpike.Inspection;
using Microsoft.Win32.SafeHandles;

namespace HardwareInspection.LlmFitSpike.Evidence;

internal interface ICandidateManifestSource
{
    LlmFitCandidateManifest Load();
}

internal interface ILlmFitCandidateVerifier
{
    LlmFitCandidateVerification Verify(string candidateRoot, LlmFitCandidateManifest manifest);
}

internal interface ILlmFitProcessRunner
{
    Task<LlmFitProcessResult> ExecuteAsync(
        LlmFitCommand command,
        TimeSpan timeout,
        Func<int, CancellationToken, Task>? observer,
        CancellationToken cancellationToken);
}

internal interface ILlmFitSocketObserver
{
    LlmFitSocketObservationSession Create(string candidateImageName);
}

internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal interface IGateOutputStore
{
    Task<GateRawOutput> WriteRawSystemJsonAsync(
        string outputDirectory,
        string rawJson,
        CancellationToken cancellationToken);
}

internal interface ILlmFitGate1EvidenceWriter
{
    Task<string> WriteAsync(
        LlmFitGate1Evidence evidence,
        string outputPath,
        CancellationToken cancellationToken);
}

internal sealed record GateRawOutput(string FileName, string Sha256);

internal sealed class GateOutputStore : IGateOutputStore
{
    private const string RawFileName = "llmfit-system.raw.json";
    private const uint CreateNew = 1;
    private const uint DeleteAccess = 0x00010000;
    private const int FileDispositionInfoClass = 4;
    private const int FileRenameInfoClass = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeTemporary = 0x00000100;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const uint ShareDelete = 0x00000004;
    private const uint ShareRead = 0x00000001;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false, true);

    public async Task<GateRawOutput> WriteRawSystemJsonAsync(
        string outputDirectory,
        string rawJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(rawJson);
        cancellationToken.ThrowIfCancellationRequested();

        string directory = ResolveOutputDirectory(outputDirectory);
        EnsureExistingComponentsAreOrdinary(directory);
        if (!Directory.Exists(directory))
        {
            throw new InvalidDataException("The raw output directory is unavailable.");
        }

        using SafeFileHandle directoryHandle = OpenStableOutputDirectory(directory);
        string stableDirectory = Path.TrimEndingDirectorySeparator(
            LlmFitCandidateVerifier.GetStableRootPath(directoryHandle));
        if (!string.Equals(directory, stableDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The raw output directory identity changed.");
        }

        string destinationPath = Path.Combine(directory, RawFileName);
        EnsureDestinationIsReplaceable(destinationPath);
        string temporaryFileName = RawFileName + ".tmp-" + Guid.NewGuid().ToString("N");
        string temporaryPath = Path.Combine(directory, temporaryFileName);
        SafeFileHandle? temporaryHandle = null;
        FileStream? temporaryStream = null;
        bool published = false;
        try
        {
            temporaryHandle = OpenOwnedTemporaryFile(temporaryPath);
            string stableTemporaryPath = LlmFitCandidateVerifier.GetStableRootPath(temporaryHandle);
            if (!string.Equals(
                    Path.GetDirectoryName(stableTemporaryPath),
                    stableDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Path.GetFileName(stableTemporaryPath),
                    temporaryFileName,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The raw temporary file identity changed.");
            }

            temporaryStream = new FileStream(
                temporaryHandle,
                FileAccess.Write,
                bufferSize: 4096,
                isAsync: true);
            temporaryHandle = null;

            byte[] bytes = Utf8WithoutBom.GetBytes(rawJson);
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await temporaryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await temporaryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            temporaryStream.Flush(flushToDisk: true);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureDestinationIsReplaceable(destinationPath);
            RenameHeldFile(
                temporaryStream.SafeFileHandle,
                directoryHandle,
                stableDirectory,
                destinationPath);
            published = true;
            return new GateRawOutput(RawFileName, sha256);
        }
        finally
        {
            if (temporaryStream is not null)
            {
                try
                {
                    if (!published)
                    {
                        MarkOwnedFileForDeletion(temporaryStream.SafeFileHandle);
                    }
                }
                finally
                {
                    await temporaryStream.DisposeAsync().ConfigureAwait(false);
                }
            }
            else if (temporaryHandle is not null)
            {
                try
                {
                    MarkOwnedFileForDeletion(temporaryHandle);
                }
                finally
                {
                    temporaryHandle.Dispose();
                }
            }
        }
    }

    private static string ResolveOutputDirectory(string outputDirectory)
    {
        try
        {
            string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) ||
                fullPath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                fullPath.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                throw new InvalidDataException();
            }

            return fullPath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException("The raw output directory is invalid.");
        }
    }

    private static void EnsureExistingComponentsAreOrdinary(string path)
    {
        string? root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidDataException("The raw output directory is invalid.");
        }

        string current = Path.TrimEndingDirectorySeparator(root);
        foreach (string segment in path[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                (attributes & FileAttributes.Directory) == 0)
            {
                throw new InvalidDataException(
                    "The raw output directory contains a non-ordinary component.");
            }
        }
    }

    private static void EnsureDestinationIsReplaceable(string destinationPath)
    {
        if (!File.Exists(destinationPath) && !Directory.Exists(destinationPath))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(destinationPath);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
        {
            throw new InvalidDataException("The raw output destination is not an ordinary file.");
        }
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
                "The raw output temporary file could not be created safely.",
                new Win32Exception(errorCode));
        }

        return handle;
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
                "The raw output directory could not be opened safely.",
                new Win32Exception(errorCode));
        }

        return handle;
    }

    private static void RenameHeldFile(
        SafeFileHandle temporaryFile,
        SafeFileHandle outputDirectory,
        string stableOutputDirectory,
        string destinationPath)
    {
        if (!string.Equals(
                Path.GetDirectoryName(destinationPath),
                stableOutputDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The raw output destination identity changed.");
        }

        if (IntPtr.Size != 8)
        {
            throw new PlatformNotSupportedException(
                "Gate 1 raw publication requires the configured x64 runtime.");
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
                    "The raw output file could not be atomically published.",
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
                "The owned raw temporary file could not be removed.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInformation
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool DeleteFile;
    }

#pragma warning disable SYSLIB1054 // Project policy forbids unsafe blocks required by LibraryImport.
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
#pragma warning restore SYSLIB1054
}

internal sealed class LlmFitSocketObservationSession
{
    private readonly Func<CancellationToken, Task<LlmFitProcessObservation>> _complete;
    private readonly Func<int, CancellationToken, Task> _observe;
    private int _entered;

    internal LlmFitSocketObservationSession(
        Func<int, CancellationToken, Task> observe,
        Func<CancellationToken, Task<LlmFitProcessObservation>> complete)
    {
        _observe = observe ?? throw new ArgumentNullException(nameof(observe));
        _complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    internal bool Entered => Volatile.Read(ref _entered) != 0;

    internal Task ObserveAsync(int processId, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _entered, 1);
        return _observe(processId, cancellationToken) ??
            Task.FromException(new InvalidOperationException());
    }

    internal Task<LlmFitProcessObservation> CompleteAsync(CancellationToken cancellationToken)
    {
        return _complete(cancellationToken) ??
            Task.FromException<LlmFitProcessObservation>(new InvalidOperationException());
    }
}

public enum LlmFitGate1Disposition
{
    Blocked,
    Rejected,
    FunctionalPassWithPackagingConcern,
    AcceptedForFunctionalEvaluation,
}

public sealed record LlmFitGate1RunResult(
    LlmFitGate1Disposition Disposition,
    string EvidencePath,
    IReadOnlyList<string> DiagnosticCodes);

public sealed class LlmFitGate1Runner
{
    private const string ApprovedReportedVersion = "llmfit 1.1.9";
    private const string ApprovedRawFileName = "llmfit-system.raw.json";
    private const string EvidenceFileName = "llmfit-gate1.evidence.json";
    private static readonly HashSet<string> NonFailureDiagnostics =
    [
        LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
        LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
        LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending,
        LlmFitGate1DiagnosticCodes.WindowsIntelMemorySemanticsGap,
        LlmFitGate1DiagnosticCodes.WindowsIntelNpuGap,
        LlmFitGate1DiagnosticCodes.SchemaDocumentationDrift,
    ];
    private readonly IClock _clock;
    private readonly ILlmFitCandidateVerifier _candidateVerifier;
    private readonly ICandidateManifestSource _manifestSource;
    private readonly IGateOutputStore _outputStore;
    private readonly ILlmFitProcessRunner _processRunner;
    private readonly ILlmFitGate1EvidenceWriter _evidenceWriter;
    private readonly ILlmFitSocketObserver _socketObserver;

    internal LlmFitGate1Runner(
        ICandidateManifestSource manifestSource,
        ILlmFitCandidateVerifier candidateVerifier,
        ILlmFitProcessRunner processRunner,
        ILlmFitSocketObserver socketObserver,
        IClock clock,
        IGateOutputStore outputStore,
        ILlmFitGate1EvidenceWriter evidenceWriter)
    {
        _manifestSource = manifestSource ?? throw new ArgumentNullException(nameof(manifestSource));
        _candidateVerifier = candidateVerifier ?? throw new ArgumentNullException(nameof(candidateVerifier));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _socketObserver = socketObserver ?? throw new ArgumentNullException(nameof(socketObserver));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _outputStore = outputStore ?? throw new ArgumentNullException(nameof(outputStore));
        _evidenceWriter = evidenceWriter ?? throw new ArgumentNullException(nameof(evidenceWriter));
    }

    public async Task<LlmFitGate1RunResult> RunAsync(
        SpikeOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        DateTimeOffset startedAtUtc = _clock.UtcNow;
        if (cancellationToken.IsCancellationRequested)
        {
            return CreateResult(
                LlmFitGate1Disposition.Blocked,
                string.Empty,
                [LlmFitGate1DiagnosticCodes.ProcessCancelled]);
        }

        LlmFitCandidateManifest manifest;
        try
        {
            manifest = _manifestSource.Load();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CreateResult(
                LlmFitGate1Disposition.Blocked,
                string.Empty,
                [LlmFitGate1DiagnosticCodes.ProcessCancelled]);
        }
        catch
        {
            return CreateResult(
                LlmFitGate1Disposition.Rejected,
                string.Empty,
                [LlmFitGate1DiagnosticCodes.ManifestInvalid]);
        }

        var diagnostics = new List<string>();
        LlmFitCandidateVerification verificationBeforeVersion;
        try
        {
            verificationBeforeVersion = _candidateVerifier.Verify(options.CandidateRoot, manifest);
        }
        catch
        {
            return CreateResult(
                LlmFitGate1Disposition.Rejected,
                string.Empty,
                [LlmFitGate1DiagnosticCodes.RequiredTestFailure]);
        }

        AddVerificationDiagnostics(diagnostics, verificationBeforeVersion);
        if (!verificationBeforeVersion.MayExecuteForGate1)
        {
            return CreateResult(LlmFitGate1Disposition.Rejected, string.Empty, diagnostics);
        }

        if (verificationBeforeVersion.AuthenticodePresent)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.SignatureStatusChanged);
        }
        else
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.SignatureClaimMismatch);
        }

        LlmFitCommand versionCommand;
        try
        {
            versionCommand = LlmFitCommandBuilder.BuildVersion(options.CandidateRoot, manifest);
        }
        catch
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ManifestInvalid);
            return CreateResult(LlmFitGate1Disposition.Rejected, string.Empty, diagnostics);
        }

        ObservedCommandResult version = await ExecuteObservedAsync(
                versionCommand,
                options.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        AddExecutionDiagnostics(diagnostics, version);
        if (HasExecutionFailure(version))
        {
            return CreateResult(
                version.Process.Cancelled
                    ? LlmFitGate1Disposition.Blocked
                    : LlmFitGate1Disposition.Rejected,
                string.Empty,
                diagnostics);
        }

        string reportedVersion = RemoveOneTrailingLineEnding(version.Process.StandardOutput);
        if (!string.Equals(reportedVersion, ApprovedReportedVersion, StringComparison.Ordinal))
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.VersionMismatch);
            return CreateResult(LlmFitGate1Disposition.Rejected, string.Empty, diagnostics);
        }

        LlmFitCandidateVerification verificationBeforeSystem;
        try
        {
            verificationBeforeSystem = _candidateVerifier.Verify(options.CandidateRoot, manifest);
        }
        catch
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.RequiredTestFailure);
            return CreateResult(LlmFitGate1Disposition.Rejected, string.Empty, diagnostics);
        }

        AddVerificationDiagnostics(diagnostics, verificationBeforeSystem);
        if (!verificationBeforeSystem.MayExecuteForGate1 ||
            !VerificationValuesEqual(verificationBeforeVersion, verificationBeforeSystem))
        {
            if (SignatureValuesDiffer(verificationBeforeVersion, verificationBeforeSystem))
            {
                AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.SignatureStatusChanged);
            }

            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.PackageChangedDuringRun);
            return CreateResult(LlmFitGate1Disposition.Rejected, string.Empty, diagnostics);
        }

        LlmFitCommand systemCommand;
        try
        {
            systemCommand = LlmFitCommandBuilder.BuildSystem(options.CandidateRoot, manifest);
        }
        catch
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ManifestInvalid);
            return CreateResult(LlmFitGate1Disposition.Rejected, string.Empty, diagnostics);
        }

        ObservedCommandResult system = await ExecuteObservedAsync(
                systemCommand,
                options.Timeout,
                cancellationToken)
            .ConfigureAwait(false);
        AddExecutionDiagnostics(diagnostics, system);
        if (HasExecutionFailure(system))
        {
            return await FinishAsync(
                    system.Process.Cancelled
                        ? LlmFitGate1Disposition.Blocked
                        : LlmFitGate1Disposition.Rejected,
                    options,
                    manifest,
                    startedAtUtc,
                    reportedVersion,
                    verificationBeforeSystem,
                    version,
                    system,
                    assessment: null,
                    rawOutput: null,
                    diagnostics,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        LlmFitSystemAssessment assessment;
        try
        {
            assessment = LlmFitSystemJsonAssessor.Assess(system.Process.StandardOutput);
            AddDiagnostics(diagnostics, assessment.DiagnosticCodes);
        }
        catch
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.JsonInvalid);
            assessment = CreateFailedAssessment();
        }

        LlmFitCandidateVerification verificationAfterSystem;
        bool packageStable;
        try
        {
            verificationAfterSystem = _candidateVerifier.Verify(options.CandidateRoot, manifest);
            AddVerificationDiagnostics(diagnostics, verificationAfterSystem);
            packageStable = verificationAfterSystem.MayExecuteForGate1 &&
                VerificationValuesEqual(verificationBeforeSystem, verificationAfterSystem);
        }
        catch
        {
            verificationAfterSystem = verificationBeforeSystem;
            packageStable = false;
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.RequiredTestFailure);
        }

        if (!packageStable)
        {
            if (SignatureValuesDiffer(verificationBeforeSystem, verificationAfterSystem))
            {
                AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.SignatureStatusChanged);
            }

            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.PackageChangedDuringRun);
        }

        GateRawOutput? rawOutput = null;
        bool noHardDiagnostic = diagnostics.All(NonFailureDiagnostics.Contains);
        if (assessment.Gate1SchemaPassed && packageStable && noHardDiagnostic)
        {
            try
            {
                GateRawOutput written = await _outputStore.WriteRawSystemJsonAsync(
                        options.OutputDirectory,
                        system.Process.StandardOutput,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(written.FileName, ApprovedRawFileName, StringComparison.Ordinal) ||
                    !IsLowercaseSha256(written.Sha256) ||
                    !string.Equals(
                        written.Sha256,
                        assessment.RawJsonSha256,
                        StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.PrivacyValidationFailed);
                }
                else
                {
                    rawOutput = written;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ProcessCancelled);
            }
            catch
            {
                AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.PrivacyValidationFailed);
            }
        }

        bool functionalPass = assessment.Gate1SchemaPassed &&
            packageStable &&
            noHardDiagnostic &&
            rawOutput is not null &&
            !diagnostics.Contains(
                LlmFitGate1DiagnosticCodes.PrivacyValidationFailed,
                StringComparer.Ordinal) &&
            !diagnostics.Contains(LlmFitGate1DiagnosticCodes.ProcessCancelled, StringComparer.Ordinal);
        LlmFitGate1Disposition disposition = functionalPass
            ? LlmFitGate1Disposition.FunctionalPassWithPackagingConcern
            : diagnostics.Contains(LlmFitGate1DiagnosticCodes.ProcessCancelled, StringComparer.Ordinal)
                ? LlmFitGate1Disposition.Blocked
                : LlmFitGate1Disposition.Rejected;
        if (functionalPass)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.DependencyLicenseInventoryPending);
        }

        return await FinishAsync(
                disposition,
                options,
                manifest,
                startedAtUtc,
                reportedVersion,
                verificationAfterSystem,
                version,
                system,
                assessment,
                rawOutput,
                diagnostics,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ObservedCommandResult> ExecuteObservedAsync(
        LlmFitCommand command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        LlmFitSocketObservationSession? session = null;
        LlmFitProcessResult process;
        try
        {
            session = _socketObserver.Create(Path.GetFileName(command.ExecutablePath));
            process = await _processRunner.ExecuteAsync(
                    command,
                    timeout,
                    session.ObserveAsync,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            process = CreateBoundaryFailure(cancelled: true);
        }
        catch
        {
            process = CreateBoundaryFailure(cancelled: false);
        }

        bool observationFailed = session is null ||
            process.ProcessId is null && !process.ProcessStartFailed;
        LlmFitProcessObservation observation = EmptyObservation;
        if (session is not null && process.ProcessId is not null)
        {
            if (!session.Entered)
            {
                observationFailed = true;
            }
            else
            {
                try
                {
                    observation = await session.CompleteAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    observationFailed = true;
                }
            }
        }

        return new ObservedCommandResult(process, observation, observationFailed);
    }

    private async Task<LlmFitGate1RunResult> FinishAsync(
        LlmFitGate1Disposition disposition,
        SpikeOptions options,
        LlmFitCandidateManifest manifest,
        DateTimeOffset startedAtUtc,
        string reportedVersion,
        LlmFitCandidateVerification verification,
        ObservedCommandResult version,
        ObservedCommandResult system,
        LlmFitSystemAssessment? assessment,
        GateRawOutput? rawOutput,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ProcessCancelled);
            return CreateResult(LlmFitGate1Disposition.Blocked, string.Empty, diagnostics);
        }

        DateTimeOffset completedAtUtc = _clock.UtcNow;
        if (completedAtUtc < startedAtUtc || completedAtUtc.Offset != TimeSpan.Zero)
        {
            completedAtUtc = startedAtUtc;
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.RequiredTestFailure);
            disposition = LlmFitGate1Disposition.Rejected;
        }

        string outputPath = Path.Combine(options.OutputDirectory, EvidenceFileName);
        LlmFitGate1Evidence evidence = CreateEvidence(
            disposition,
            manifest,
            startedAtUtc,
            completedAtUtc,
            reportedVersion,
            verification,
            version,
            system,
            assessment,
            rawOutput,
            diagnostics);
        try
        {
            string writtenPath = await _evidenceWriter.WriteAsync(
                    evidence,
                    outputPath,
                    cancellationToken)
                .ConfigureAwait(false);
            return CreateResult(disposition, writtenPath, diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ProcessCancelled);
            return CreateResult(LlmFitGate1Disposition.Blocked, string.Empty, diagnostics);
        }
        catch
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.PrivacyValidationFailed);
            return CreateResult(LlmFitGate1Disposition.Rejected, string.Empty, diagnostics);
        }
    }

    private static LlmFitGate1Evidence CreateEvidence(
        LlmFitGate1Disposition disposition,
        LlmFitCandidateManifest manifest,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        string reportedVersion,
        LlmFitCandidateVerification verification,
        ObservedCommandResult version,
        ObservedCommandResult system,
        LlmFitSystemAssessment? assessment,
        GateRawOutput? rawOutput,
        IReadOnlyCollection<string> diagnostics)
    {
        return new LlmFitGate1Evidence(
            "1.0",
            disposition.ToString(),
            manifest.CandidateId,
            manifest.Version,
            reportedVersion,
            manifest.ReleaseCommit,
            manifest.Archive.Sha256,
            verification.ArchiveSha256,
            manifest.Executable.Sha256,
            verification.ExecutableSha256,
            manifest.Executable.PeMachine,
            verification.PeMachine,
            verification.AuthenticodePresent,
            verification.AuthenticodeStatus,
            manifest.Commands.Version.ToArray(),
            manifest.Commands.System.ToArray(),
            startedAtUtc,
            completedAtUtc,
            (completedAtUtc - startedAtUtc).Ticks / TimeSpan.TicksPerMillisecond,
            version.Process.ExitCode,
            system.Process.ExitCode,
            version.Process.ProcessStartFailed || system.Process.ProcessStartFailed,
            version.ObservationFailed || system.ObservationFailed ||
                version.Process.ObserverFailed || system.Process.ObserverFailed,
            version.Process.TimedOut || system.Process.TimedOut,
            version.Process.Cancelled || system.Process.Cancelled,
            version.Process.StandardOutputTruncated || system.Process.StandardOutputTruncated,
            version.Process.StandardErrorTruncated || system.Process.StandardErrorTruncated,
            assessment?.JsonValid ?? false,
            assessment?.RequiredCpuRamPresent ?? false,
            assessment?.CpuLogicalProcessorCount,
            assessment?.TotalRamGiB,
            assessment?.AvailableRamGiB,
            assessment?.GpuReported ?? false,
            assessment?.ReportedGpuCount ?? 0,
            assessment?.IntelGpuReported ?? false,
            assessment?.DedicatedSharedMemorySemanticsEstablished ?? false,
            assessment?.IntelNpuDetectionState ?? "DetectionUnavailable",
            version.Observation.CandidateSocketObserved || version.Observation.DashboardPortObserved,
            version.Observation.DashboardPortObserved,
            system.Observation.CandidateSocketObserved || system.Observation.DashboardPortObserved,
            system.Observation.DashboardPortObserved,
            version.Observation.CandidateProcessRemainedAfterExit,
            system.Observation.CandidateProcessRemainedAfterExit,
            rawOutput?.FileName,
            rawOutput?.Sha256,
            diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool HasExecutionFailure(ObservedCommandResult result)
    {
        return result.Process.ProcessStartFailed ||
            result.Process.ObserverFailed ||
            result.ObservationFailed ||
            result.Process.TimedOut ||
            result.Process.Cancelled ||
            result.Process.ExitCode != 0 ||
            result.Process.StandardOutputTruncated ||
            result.Process.StandardErrorTruncated ||
            result.Observation.CandidateSocketObserved ||
            result.Observation.DashboardPortObserved ||
            result.Observation.CandidateProcessRemainedAfterExit;
    }

    private static void AddExecutionDiagnostics(
        List<string> diagnostics,
        ObservedCommandResult result)
    {
        if (result.Process.ProcessStartFailed)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ProcessStartFailed);
        }

        if (result.Process.ObserverFailed || result.ObservationFailed)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.SocketObservationFailed);
        }

        if (result.Process.TimedOut)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ProcessTimedOut);
        }

        if (result.Process.Cancelled)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ProcessCancelled);
        }

        if (!result.Process.ProcessStartFailed && result.Process.ExitCode != 0)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ProcessExitNonzero);
        }

        if (result.Process.StandardOutputTruncated)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.StdoutTruncated);
        }

        if (result.Process.StandardErrorTruncated)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.StderrTruncated);
        }

        if (result.Observation.CandidateSocketObserved)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.CandidateSocketObserved);
        }

        if (result.Observation.DashboardPortObserved)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.CandidateSocketObserved);
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.DashboardPortObserved);
        }

        if (result.Observation.CandidateProcessRemainedAfterExit)
        {
            AddDiagnostic(diagnostics, LlmFitGate1DiagnosticCodes.ResidualProcess);
        }
    }

    private static void AddVerificationDiagnostics(
        List<string> diagnostics,
        LlmFitCandidateVerification verification)
    {
        foreach (string diagnostic in verification.DiagnosticCodes)
        {
            AddDiagnostic(diagnostics, NormalizeVerifierDiagnostic(diagnostic));
        }
    }

    private static string NormalizeVerifierDiagnostic(string diagnostic)
    {
        return diagnostic switch
        {
            "HI-LLMFIT-ARCHIVE-HASH-MISMATCH" =>
                LlmFitGate1DiagnosticCodes.ArchiveHashMismatch,
            "HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH" =>
                LlmFitGate1DiagnosticCodes.ArchiveLengthMismatch,
            "HI-LLMFIT-EXECUTABLE-HASH-MISMATCH" =>
                LlmFitGate1DiagnosticCodes.ExecutableHashMismatch,
            "HI-LLMFIT-MISSING-REQUIRED-FILE" or "HI-LLMFIT-PACKAGE-ROOT-MISSING" =>
                LlmFitGate1DiagnosticCodes.PackageMissing,
            "HI-LLMFIT-OBSERVATION-MISMATCH" =>
                LlmFitGate1DiagnosticCodes.SignatureStatusChanged,
            "HI-LLMFIT-PACKAGE-PATH-INVALID" => LlmFitGate1DiagnosticCodes.PathEscape,
            "HI-LLMFIT-PE-INVALID" => LlmFitGate1DiagnosticCodes.PeInvalid,
            "HI-LLMFIT-PE-MACHINE-MISMATCH" =>
                LlmFitGate1DiagnosticCodes.PeArchitectureMismatch,
            "HI-LLMFIT-REPARSE-POINT" => LlmFitGate1DiagnosticCodes.ReparsePoint,
            "HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH" =>
                LlmFitGate1DiagnosticCodes.SignatureClaimMismatch,
            "HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER" =>
                LlmFitGate1DiagnosticCodes.UnexpectedPackageMember,
            "HI-LLMFIT-PACKAGE-ACCESS-DENIED" or
            "HI-LLMFIT-PACKAGE-INVALID" or
            "HI-LLMFIT-PACKAGE-IO-ERROR" or
            "HI-LLMFIT-OBSERVATION-INVALID" => LlmFitGate1DiagnosticCodes.RequiredTestFailure,
            _ => LlmFitGate1DiagnosticCodes.RequiredTestFailure,
        };
    }

    private static bool VerificationValuesEqual(
        LlmFitCandidateVerification left,
        LlmFitCandidateVerification right)
    {
        return left.IntegrityPassed == right.IntegrityPassed &&
            string.Equals(left.ArchiveSha256, right.ArchiveSha256, StringComparison.Ordinal) &&
            string.Equals(left.ExecutableSha256, right.ExecutableSha256, StringComparison.Ordinal) &&
            string.Equals(left.PeMachine, right.PeMachine, StringComparison.Ordinal) &&
            left.AuthenticodePresent == right.AuthenticodePresent &&
            string.Equals(left.AuthenticodeStatus, right.AuthenticodeStatus, StringComparison.Ordinal) &&
            string.Equals(left.AuthenticodeSubject, right.AuthenticodeSubject, StringComparison.Ordinal) &&
            left.DiagnosticCodes.SequenceEqual(right.DiagnosticCodes, StringComparer.Ordinal);
    }

    private static bool SignatureValuesDiffer(
        LlmFitCandidateVerification left,
        LlmFitCandidateVerification right)
    {
        return left.AuthenticodePresent != right.AuthenticodePresent ||
            !string.Equals(left.AuthenticodeStatus, right.AuthenticodeStatus, StringComparison.Ordinal) ||
            !string.Equals(left.AuthenticodeSubject, right.AuthenticodeSubject, StringComparison.Ordinal);
    }

    private static string RemoveOneTrailingLineEnding(string value)
    {
        if (value.EndsWith("\r\n", StringComparison.Ordinal))
        {
            return value[..^2];
        }

        return value.EndsWith('\r') || value.EndsWith('\n') ? value[..^1] : value;
    }

    private static bool IsLowercaseSha256(string value)
    {
        return value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static LlmFitProcessResult CreateBoundaryFailure(bool cancelled)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new LlmFitProcessResult(
            now,
            now,
            null,
            null,
            ProcessStartFailed: !cancelled,
            ObserverFailed: !cancelled,
            TimedOut: false,
            Cancelled: cancelled,
            StandardOutput: string.Empty,
            StandardError: string.Empty,
            StandardOutputTruncated: false,
            StandardErrorTruncated: false);
    }

    private static LlmFitSystemAssessment CreateFailedAssessment()
    {
        return new LlmFitSystemAssessment(
            false,
            false,
            false,
            null,
            null,
            null,
            null,
            false,
            0,
            false,
            false,
            "DetectionUnavailable",
            new string('0', 64),
            [LlmFitGate1DiagnosticCodes.JsonInvalid]);
    }

    private static void AddDiagnostics(List<string> diagnostics, IEnumerable<string> values)
    {
        foreach (string value in values)
        {
            AddDiagnostic(
                diagnostics,
                LlmFitGate1DiagnosticCodes.All.Contains(value)
                    ? value
                    : LlmFitGate1DiagnosticCodes.RequiredTestFailure);
        }
    }

    private static void AddDiagnostic(List<string> diagnostics, string diagnostic)
    {
        if (!diagnostics.Contains(diagnostic, StringComparer.Ordinal))
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static LlmFitGate1RunResult CreateResult(
        LlmFitGate1Disposition disposition,
        string evidencePath,
        IEnumerable<string> diagnostics)
    {
        return new LlmFitGate1RunResult(
            disposition,
            evidencePath,
            new ReadOnlyCollection<string>(diagnostics.Distinct(StringComparer.Ordinal).ToArray()));
    }

    private static readonly LlmFitProcessObservation EmptyObservation =
        new(false, false, Array.Empty<int>(), false);

    private sealed record ObservedCommandResult(
        LlmFitProcessResult Process,
        LlmFitProcessObservation Observation,
        bool ObservationFailed);
}
