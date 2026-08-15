using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

[assembly: InternalsVisibleTo("HardwareInspection.LlmFitSpike.Tests")]

namespace HardwareInspection.LlmFitSpike.Candidate;

internal interface IFileMetadata
{
    FileAttributes GetAttributes(string path);
}

public sealed class LlmFitCandidateVerifier
{
    private const string ObservationFileName = "authenticode-observation.json";
    private const string Amd64 = "AMD64";
    private const long MaximumObservationBytes = 16 * 1024;
    private const uint GenericRead = 0x80000000;
    private const uint ShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const int FileAttributeTagInformation = 9;
    private const int MaximumWindowsPathCharacters = 32_768;
    private static readonly HashSet<string> AllowedObservationProperties =
    [
        "executableSha256",
        "rawStatus",
        "signaturePresent",
        "signerSubject",
        "signerThumbprint",
        "checkedAtUtc",
    ];
    private static readonly HashSet<string> AllowedSignatureStatuses =
    [
        "UnknownError",
        "Valid",
        "NotSigned",
        "HashMismatch",
        "NotTrusted",
    ];

    private readonly IFileMetadata _fileMetadata;

    public LlmFitCandidateVerifier()
        : this(new PhysicalFileMetadata())
    {
    }

    internal LlmFitCandidateVerifier(IFileMetadata fileMetadata)
    {
        _fileMetadata = fileMetadata ?? throw new ArgumentNullException(nameof(fileMetadata));
    }

    internal static FileStream OpenExecutableForStableRead(string path)
    {
        SafeFileHandle handle = CreateFile(
            path,
            GenericRead,
            ShareRead,
            0,
            OpenExisting,
            FileAttributeNormal | FileFlagOpenReparsePoint | FileFlagSequentialScan,
            0);
        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw CreateSafeOpenException(errorCode);
        }

        try
        {
            FileAttributeTagInfo information = GetHandleInformation(handle);
            if ((information.FileAttributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            {
                throw new InvalidDataException("The executable package member must be a regular non-reparse file.");
            }

            return new FileStream(handle, FileAccess.Read, bufferSize: 4096, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static SafeFileHandle OpenPackageRootForStableValidation(string path)
    {
        SafeFileHandle handle = CreateFile(
            path,
            GenericRead,
            ShareRead,
            0,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            0);
        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw CreateSafeOpenException(errorCode);
        }

        try
        {
            FileAttributeTagInfo information = GetHandleInformation(handle);
            if ((information.FileAttributes & FileAttributes.Directory) == 0 ||
                (information.FileAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The package root must be a non-reparse directory.");
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static string GetStableRootPath(SafeFileHandle rootHandle)
    {
        ArgumentNullException.ThrowIfNull(rootHandle);
        var pathBuffer = new StringBuilder(MaximumWindowsPathCharacters);
        uint characterCount = GetFinalPathNameByHandle(
            rootHandle,
            pathBuffer,
            (uint)pathBuffer.Capacity,
            0);
        if (characterCount == 0 || characterCount >= pathBuffer.Capacity)
        {
            throw CreateSafeOpenException(Marshal.GetLastWin32Error());
        }

        string finalPath = pathBuffer.ToString();
        if (finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + finalPath[8..];
        }

        return finalPath.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? finalPath[4..]
            : finalPath;
    }

    public LlmFitCandidateVerification Verify(string packageRoot, LlmFitCandidateManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        ArgumentNullException.ThrowIfNull(manifest);

        var state = new VerificationState();
        var diagnostics = new List<string>();
        try
        {
            return VerifyCore(packageRoot, manifest, state, diagnostics);
        }
        catch (InvalidDataException)
        {
            diagnostics.Add("HI-LLMFIT-PACKAGE-INVALID");
        }
        catch (IOException)
        {
            diagnostics.Add("HI-LLMFIT-PACKAGE-IO-ERROR");
        }
        catch (UnauthorizedAccessException)
        {
            diagnostics.Add("HI-LLMFIT-PACKAGE-ACCESS-DENIED");
        }
        catch (ArgumentException)
        {
            diagnostics.Add("HI-LLMFIT-PACKAGE-PATH-INVALID");
        }
        catch (System.Security.SecurityException)
        {
            diagnostics.Add("HI-LLMFIT-PACKAGE-ACCESS-DENIED");
        }

        return CreateResult(state, integrityPassed: false, diagnostics);
    }

    private LlmFitCandidateVerification VerifyCore(
        string packageRoot,
        LlmFitCandidateManifest manifest,
        VerificationState state,
        List<string> diagnostics)
    {
        string root = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(root))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-PACKAGE-ROOT-MISSING");
        }

        if (IsReparsePoint(root))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-REPARSE-POINT");
        }

        using SafeFileHandle rootHandle = OpenPackageRootForStableValidation(root);
        string stableRoot = GetStableRootPath(rootHandle);

        if (!TryResolveExpectedMembers(stableRoot, manifest, out Dictionary<string, string>? expectedMembers))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-PACKAGE-PATH-INVALID");
        }

        foreach (string entry in Directory.EnumerateFileSystemEntries(stableRoot))
        {
            if (IsReparsePoint(entry))
            {
                return Fail(state, diagnostics, "HI-LLMFIT-REPARSE-POINT");
            }

            string memberName = Path.GetFileName(entry);
            if (!expectedMembers.ContainsKey(memberName) || Directory.Exists(entry))
            {
                return Fail(state, diagnostics, "HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER");
            }
        }

        foreach (string expectedPath in expectedMembers.Values)
        {
            if (!File.Exists(expectedPath) || Directory.Exists(expectedPath))
            {
                return Fail(state, diagnostics, "HI-LLMFIT-MISSING-REQUIRED-FILE");
            }

            if (IsReparsePoint(expectedPath))
            {
                return Fail(state, diagnostics, "HI-LLMFIT-REPARSE-POINT");
            }
        }

        string archivePath = expectedMembers[manifest.Archive.FileName];
        if (new FileInfo(archivePath).Length != manifest.Archive.LengthBytes)
        {
            return Fail(state, diagnostics, "HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH");
        }

        state.ArchiveSha256 = ComputeSha256(archivePath);
        if (!HashMatches(state.ArchiveSha256, manifest.Archive.Sha256))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-ARCHIVE-HASH-MISMATCH");
        }

        string executablePath = expectedMembers[manifest.Executable.RelativePath];
        using FileStream executableStream = OpenExecutableForStableRead(executablePath);
        state.ExecutableSha256 = ComputeSha256(executableStream);
        if (!HashMatches(state.ExecutableSha256, manifest.Executable.Sha256))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-EXECUTABLE-HASH-MISMATCH");
        }

        PeImageInspection peInspection;
        try
        {
            executableStream.Position = 0;
            peInspection = PeImageInspector.Inspect(executableStream);
        }
        catch (InvalidDataException)
        {
            return Fail(state, diagnostics, "HI-LLMFIT-PE-INVALID");
        }

        state.PeMachine = peInspection.MachineName;
        if (!string.Equals(manifest.Executable.PeMachine, Amd64, StringComparison.Ordinal) ||
            !string.Equals(state.PeMachine, manifest.Executable.PeMachine, StringComparison.Ordinal))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-PE-MACHINE-MISMATCH");
        }

        ObserveAuthenticode(executablePath, state);
        string observationPath = expectedMembers[ObservationFileName];
        if (!TryValidateObservation(observationPath, state, out AuthenticodeObservation? observation))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-OBSERVATION-INVALID");
        }

        if (!ObservationAgreesWithFreshFacts(observation, state))
        {
            return Fail(state, diagnostics, "HI-LLMFIT-OBSERVATION-MISMATCH");
        }

        if (!state.AuthenticodePresent)
        {
            diagnostics.Add("HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH");
        }

        return CreateResult(state, integrityPassed: true, diagnostics);
    }

    private static bool TryResolveExpectedMembers(
        string root,
        LlmFitCandidateManifest manifest,
        out Dictionary<string, string> expectedMembers)
    {
        expectedMembers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var memberNames = new List<string>(manifest.RequiredFiles.Count + 2)
        {
            manifest.Archive.FileName,
            ObservationFileName,
        };
        memberNames.AddRange(manifest.RequiredFiles);

        if (!manifest.RequiredFiles.Contains(manifest.Executable.RelativePath, StringComparer.OrdinalIgnoreCase) ||
            !manifest.RequiredFiles.Contains(manifest.License.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (string memberName in memberNames)
        {
            if (!IsSimpleMemberName(memberName) || !expectedMembers.TryAdd(memberName, string.Empty))
            {
                return false;
            }

            string resolvedPath = Path.GetFullPath(Path.Combine(root, memberName));
            if (!IsContainedByRoot(root, resolvedPath))
            {
                return false;
            }

            expectedMembers[memberName] = resolvedPath;
        }

        return IsSimpleMemberName(manifest.Executable.RelativePath) &&
            IsSimpleMemberName(manifest.License.RelativePath);
    }

    private static bool IsSimpleMemberName(string memberName)
    {
        return !string.IsNullOrWhiteSpace(memberName) &&
            !Path.IsPathFullyQualified(memberName) &&
            !memberName.Contains(':', StringComparison.Ordinal) &&
            !memberName.Contains('/', StringComparison.Ordinal) &&
            !memberName.Contains('\\', StringComparison.Ordinal) &&
            !string.Equals(memberName, ".", StringComparison.Ordinal) &&
            !string.Equals(memberName, "..", StringComparison.Ordinal) &&
            string.Equals(Path.GetFileName(memberName), memberName, StringComparison.Ordinal);
    }

    private static bool IsContainedByRoot(string root, string path)
    {
        string rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IOException CreateSafeOpenException(int errorCode)
    {
        return new IOException(
            "The executable package member could not be opened safely.",
            new System.ComponentModel.Win32Exception(errorCode));
    }

    private static FileAttributeTagInfo GetHandleInformation(SafeFileHandle handle)
    {
        int informationSize = Marshal.SizeOf<FileAttributeTagInfo>();
        if (!GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInformation,
                out FileAttributeTagInfo information,
                (uint)informationSize))
        {
            throw CreateSafeOpenException(Marshal.GetLastWin32Error());
        }

        return information;
    }

    private bool IsReparsePoint(string path)
    {
        return (_fileMetadata.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return ComputeSha256(stream);
    }

    private static string ComputeSha256(Stream stream)
    {
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool HashMatches(string actualHex, string expectedHex)
    {
        try
        {
            byte[] actual = Convert.FromHexString(actualHex);
            byte[] expected = Convert.FromHexString(expectedHex);
            return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void ObserveAuthenticode(string executablePath, VerificationState state)
    {
        try
        {
#pragma warning disable SYSLIB0057 // The task contract requires local certificate-presence observation.
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(executablePath);
#pragma warning restore SYSLIB0057
            state.AuthenticodePresent = true;
            state.AuthenticodeStatus = "PresentUnverified";
            state.AuthenticodeSubject = certificate.Subject;
            state.AuthenticodeThumbprint = certificate.GetCertHashString();
        }
        catch (CryptographicException)
        {
            state.AuthenticodePresent = false;
            state.AuthenticodeStatus = "NotSigned";
            state.AuthenticodeSubject = null;
            state.AuthenticodeThumbprint = null;
        }
    }

    private static bool TryValidateObservation(
        string path,
        VerificationState state,
        out AuthenticodeObservation observation)
    {
        observation = default!;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            if (stream.Length <= 0 || stream.Length > MaximumObservationBytes)
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!AllowedObservationProperties.Contains(property.Name) || !seen.Add(property.Name))
                {
                    return false;
                }
            }

            if (seen.Count != AllowedObservationProperties.Count ||
                !TryGetSafeString(root, "executableSha256", out string? executableSha256) ||
                !IsLowercaseHex(executableSha256, 64) ||
                !TryGetSafeString(root, "rawStatus", out string? rawStatus) ||
                !AllowedSignatureStatuses.Contains(rawStatus) ||
                !root.TryGetProperty("signaturePresent", out JsonElement presentElement) ||
                presentElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !TryGetNullableSafeString(root, "signerSubject", out string? signerSubject) ||
                !TryGetNullableSafeString(root, "signerThumbprint", out string? signerThumbprint) ||
                !TryGetSafeString(root, "checkedAtUtc", out string? checkedAtUtc) ||
                !DateTimeOffset.TryParseExact(
                    checkedAtUtc,
                    "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTimeOffset checkedAt) ||
                checkedAt.Offset != TimeSpan.Zero)
            {
                return false;
            }

            bool signaturePresent = presentElement.GetBoolean();
            if (signaturePresent)
            {
                if (string.IsNullOrWhiteSpace(signerSubject) || !IsHex(signerThumbprint, 40) || rawStatus == "NotSigned")
                {
                    return false;
                }
            }
            else if (signerSubject is not null || signerThumbprint is not null || rawStatus != "NotSigned")
            {
                return false;
            }

            observation = new AuthenticodeObservation(
                executableSha256,
                rawStatus,
                signaturePresent,
                signerSubject,
                signerThumbprint,
                checkedAt);
            return HashMatches(state.ExecutableSha256!, executableSha256);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetSafeString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            return false;
        }

        value = element.GetString()!;
        return !LooksPathLike(value);
    }

    private static bool TryGetNullableSafeString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out JsonElement element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            return false;
        }

        value = element.GetString();
        return !LooksPathLike(value!);
    }

    private static bool LooksPathLike(string value)
    {
        return value.Contains("..", StringComparison.Ordinal) ||
            value.Contains('/', StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal) ||
            (value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':') ||
            Path.IsPathFullyQualified(value);
    }

    private static bool IsLowercaseHex(string value, int length)
    {
        return value.Length == length &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool IsHex(string? value, int length)
    {
        return value is not null && value.Length == length && Uri.IsHexDigit(value.AsSpan()[0]) &&
            value.All(Uri.IsHexDigit);
    }

    private static bool ObservationAgreesWithFreshFacts(
        AuthenticodeObservation observation,
        VerificationState state)
    {
        if (observation.SignaturePresent != state.AuthenticodePresent)
        {
            return false;
        }

        if (!state.AuthenticodePresent)
        {
            return observation.RawStatus == "NotSigned";
        }

        return observation.RawStatus != "NotSigned" &&
            string.Equals(observation.SignerSubject, state.AuthenticodeSubject, StringComparison.Ordinal) &&
            string.Equals(observation.SignerThumbprint, state.AuthenticodeThumbprint, StringComparison.OrdinalIgnoreCase);
    }

    private static LlmFitCandidateVerification Fail(
        VerificationState state,
        List<string> diagnostics,
        string diagnosticCode)
    {
        diagnostics.Add(diagnosticCode);
        return CreateResult(state, integrityPassed: false, diagnostics);
    }

    private static LlmFitCandidateVerification CreateResult(
        VerificationState state,
        bool integrityPassed,
        IReadOnlyList<string> diagnostics)
    {
        return new LlmFitCandidateVerification(
            integrityPassed,
            state.ArchiveSha256,
            state.ExecutableSha256,
            state.PeMachine,
            state.AuthenticodePresent,
            state.AuthenticodeStatus,
            state.AuthenticodeSubject,
            diagnostics);
    }

    private sealed class PhysicalFileMetadata : IFileMetadata
    {
        public FileAttributes GetAttributes(string path)
        {
            return File.GetAttributes(path);
        }
    }

    private sealed class VerificationState
    {
        public string? ArchiveSha256 { get; set; }

        public string? ExecutableSha256 { get; set; }

        public string? PeMachine { get; set; }

        public bool AuthenticodePresent { get; set; }

        public string AuthenticodeStatus { get; set; } = "NotSigned";

        public string? AuthenticodeSubject { get; set; }

        public string? AuthenticodeThumbprint { get; set; }
    }

    private sealed record AuthenticodeObservation(
        string ExecutableSha256,
        string RawStatus,
        bool SignaturePresent,
        string? SignerSubject,
        string? SignerThumbprint,
        DateTimeOffset CheckedAtUtc);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public FileAttributes FileAttributes;

        public uint ReparseTag;
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

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out FileAttributeTagInfo fileInformation,
        uint bufferSize);

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
#pragma warning restore CA1838
#pragma warning restore SYSLIB1054
}
