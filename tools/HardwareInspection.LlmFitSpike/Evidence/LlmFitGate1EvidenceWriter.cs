using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace HardwareInspection.LlmFitSpike.Evidence;

public sealed class LlmFitGate1EvidenceWriter
{
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

#pragma warning disable CA1822 // Task 7 binds this concrete writer behind an instance interface.
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

        Directory.CreateDirectory(outputDirectory);
        EnsureOrdinaryDirectory(outputDirectory);
        EnsureOrdinaryDestination(fullOutputPath);

        string temporaryPath = fullOutputPath + ".tmp-" + Guid.NewGuid().ToString("N");
        bool ownsTemporaryFile = false;
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            };
            await using (var stream = new FileStream(temporaryPath, options))
            {
                ownsTemporaryFile = true;
                byte[] bytes = Utf8WithoutBom.GetBytes(json);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            EnsureOrdinaryTemporaryFile(temporaryPath, outputDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullOutputPath, overwrite: true);
            ownsTemporaryFile = false;
            return fullOutputPath;
        }
        finally
        {
            if (ownsTemporaryFile && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
#pragma warning restore CA1822

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
            IsSafeSchemaVersion(evidence.SchemaVersion) &&
            ApprovedDispositions.Contains(evidence.Disposition) &&
            IsSafeIdentifier(evidence.CandidateId, 128) &&
            IsSafeVersion(evidence.ExpectedVersion) &&
            (evidence.ReportedVersion is null || IsSafeReportedVersion(evidence.ReportedVersion)) &&
            IsLowercaseHex(evidence.ReleaseCommit, 40) &&
            IsLowercaseHex(evidence.ExpectedArchiveSha256, 64) &&
            IsOptionalLowercaseHex(evidence.ObservedArchiveSha256, 64) &&
            IsLowercaseHex(evidence.ExpectedExecutableSha256, 64) &&
            IsOptionalLowercaseHex(evidence.ObservedExecutableSha256, 64) &&
            IsSafeMachineName(evidence.ExpectedPeMachine) &&
            (evidence.ObservedPeMachine is null || IsSafeMachineName(evidence.ObservedPeMachine)) &&
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
            HasValidDiagnosticCodes(evidence.DiagnosticCodes);

        if (!valid)
        {
            throw new InvalidDataException(InvalidEvidenceMessage);
        }
    }

    private static bool IsSafeSchemaVersion(string? value)
    {
        return value is not null && value.Length is > 0 and <= 16 &&
            value.All(static character => char.IsAsciiDigit(character) || character == '.');
    }

    private static bool IsSafeIdentifier(string? value, int maximumLength)
    {
        return value is not null && value.Length is > 0 && value.Length <= maximumLength &&
            value.All(static character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static bool IsSafeVersion(string? value)
    {
        return value is not null && value.Length is > 0 and <= 32 &&
            value.All(static character =>
                char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '+');
    }

    private static bool IsSafeReportedVersion(string value)
    {
        return value.Length is > 0 and <= 64 &&
            value.All(static character =>
                char.IsAsciiLetterOrDigit(character) || character is ' ' or '.' or '-' or '+');
    }

    private static bool IsSafeMachineName(string? value)
    {
        return value is not null && value.Length is > 0 and <= 32 &&
            value.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_');
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

        return fileName.Length is > 0 and <= 128 &&
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

    private static string ResolveOutputPath(string outputPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(outputPath);
            string? directory = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
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

    private static void EnsureOrdinaryDirectory(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The evidence output directory is not an ordinary directory.");
        }
    }

    private static void EnsureOrdinaryDestination(string destination)
    {
        if (File.Exists(destination) &&
            (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The evidence destination is not an ordinary file.");
        }
    }

    private static void EnsureOrdinaryTemporaryFile(string temporaryPath, string outputDirectory)
    {
        string? temporaryDirectory = Path.GetDirectoryName(Path.GetFullPath(temporaryPath));
        if (!string.Equals(temporaryDirectory, outputDirectory, StringComparison.OrdinalIgnoreCase) ||
            (File.GetAttributes(temporaryPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The evidence temporary file identity changed.");
        }
    }
}
