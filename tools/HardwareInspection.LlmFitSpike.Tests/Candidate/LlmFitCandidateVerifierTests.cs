using System.Security.Cryptography;
using System.Text;
using HardwareInspection.LlmFitSpike.Candidate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Candidate;

[TestClass]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class LlmFitCandidateVerifierTests
{
    [TestMethod]
    public void Verify_ValidUnsignedPackage_PreservesIntegrityAndArchitectureFacts()
    {
        using var package = CandidatePackage.Create(0x8664);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, package.Manifest);

        Assert.IsTrue(result.IntegrityPassed);
        Assert.IsTrue(result.MayExecuteForGate1);
        Assert.AreEqual(package.Manifest.Archive.Sha256, result.ArchiveSha256);
        Assert.AreEqual(package.Manifest.Executable.Sha256, result.ExecutableSha256);
        Assert.AreEqual("AMD64", result.PeMachine);
        Assert.IsFalse(result.AuthenticodePresent);
        Assert.AreEqual("NotSigned", result.AuthenticodeStatus);
        Assert.IsNull(result.AuthenticodeSubject);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Verify_ArchiveLengthOrHashMismatch_StopsBeforeExecutableOpen(bool changeLength)
    {
        using var package = CandidatePackage.Create(0x8664);
        LlmFitCandidateManifest manifest = changeLength
            ? package.WithArchive(package.Manifest.Archive with { LengthBytes = package.Manifest.Archive.LengthBytes + 1 })
            : package.WithArchive(package.Manifest.Archive with { Sha256 = new string('0', 64) });
        using FileStream lockedExecutable = File.Open(package.ExecutablePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.IsNull(result.ExecutableSha256);
        Assert.IsNull(result.PeMachine);
        CollectionAssert.Contains(
            result.DiagnosticCodes.ToArray(),
            changeLength ? "HI-LLMFIT-ARCHIVE-LENGTH-MISMATCH" : "HI-LLMFIT-ARCHIVE-HASH-MISMATCH");
    }

    [TestMethod]
    public void Verify_ExecutableHashMismatch_FailsBeforePeInspection()
    {
        using var package = CandidatePackage.Create(0x8664);
        string expectedExecutableHash = package.Manifest.Executable.Sha256;
        LlmFitCandidateManifest manifest = package.WithExecutable(
            package.Manifest.Executable with { Sha256 = new string('0', 64) });

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.AreEqual(expectedExecutableHash, result.ExecutableSha256);
        Assert.IsNull(result.PeMachine);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-EXECUTABLE-HASH-MISMATCH");
    }

    [TestMethod]
    public void Verify_I386Image_FailsArchitectureGate()
    {
        using var package = CandidatePackage.Create(0x014c);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, package.Manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.AreEqual("I386", result.PeMachine);
        Assert.IsFalse(result.MayExecuteForGate1);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-PE-MACHINE-MISMATCH");
    }

    [TestMethod]
    public void Verify_MissingLicense_FailsRequiredFileCheck()
    {
        using var package = CandidatePackage.Create(0x8664);
        File.Delete(Path.Combine(package.Root, "LICENSE"));

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, package.Manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.IsNull(result.ArchiveSha256);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-MISSING-REQUIRED-FILE");
    }

    [TestMethod]
    public void Verify_CanonicalMemberEscape_FailsContainmentCheck()
    {
        using var package = CandidatePackage.Create(0x8664);
        LlmFitCandidateManifest manifest = package.WithRequiredFiles(["llmfit.exe", "../outside.txt", "README.md"]);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.IsNull(result.ArchiveSha256);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-PACKAGE-PATH-INVALID");
    }

    [TestMethod]
    public void Verify_ReparsePointMember_IsRejectedBeforeFileOpen()
    {
        using var package = CandidatePackage.Create(0x8664);
        using FileStream lockedExecutable = File.Open(package.ExecutablePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var metadata = new FakeFileMetadata(package.ExecutablePath);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier(metadata).Verify(package.Root, package.Manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.IsNull(result.ArchiveSha256);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-REPARSE-POINT");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Verify_UnexpectedFileOrDirectory_IsRejectedBeforeExecutableOpen(bool createDirectory)
    {
        using var package = CandidatePackage.Create(0x8664);
        string unexpectedPath = Path.Combine(package.Root, createDirectory ? "subdirectory" : "unexpected.dll");
        if (createDirectory)
        {
            Directory.CreateDirectory(unexpectedPath);
        }
        else
        {
            File.WriteAllBytes(unexpectedPath, [1]);
        }

        using FileStream lockedExecutable = File.Open(package.ExecutablePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, package.Manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.IsNull(result.ArchiveSha256);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-UNEXPECTED-PACKAGE-MEMBER");
    }

    [TestMethod]
    public void Verify_UnsignedImage_RecordsClaimMismatchWithoutErasingFacts()
    {
        using var package = CandidatePackage.Create(0x8664);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, package.Manifest);

        Assert.IsTrue(result.IntegrityPassed);
        Assert.AreEqual("AMD64", result.PeMachine);
        Assert.IsNotNull(result.ArchiveSha256);
        Assert.IsNotNull(result.ExecutableSha256);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-SIGNATURE-CLAIM-MISMATCH");
        Assert.IsFalse(result.DiagnosticCodes is string[]);
    }

    [TestMethod]
    public void Verification_WithMutableDiagnosticCodes_DefensivelyCopiesAssignedList()
    {
        var original = new LlmFitCandidateVerification(
            true,
            "archive",
            "executable",
            "AMD64",
            false,
            "NotSigned",
            null,
            ["original"]);
        var mutableCodes = new List<string> { "replacement" };

        LlmFitCandidateVerification updated = original with { DiagnosticCodes = mutableCodes };
        mutableCodes.Add("mutated-after-assignment");

        Assert.HasCount(1, updated.DiagnosticCodes);
        Assert.AreEqual("replacement", updated.DiagnosticCodes[0]);
        Assert.AreNotSame(mutableCodes, updated.DiagnosticCodes);
        Assert.ThrowsExactly<NotSupportedException>(
            () => ((IList<string>)updated.DiagnosticCodes).Add("mutation-attempt"));
    }

    [TestMethod]
    public void Verification_WithNullDiagnosticCodes_ThrowsArgumentNull()
    {
        var original = new LlmFitCandidateVerification(
            true,
            "archive",
            "executable",
            "AMD64",
            false,
            "NotSigned",
            null,
            ["original"]);

        Assert.ThrowsExactly<ArgumentNullException>(
            () => _ = original with { DiagnosticCodes = null! });
    }

    [TestMethod]
    public void OpenRegularFileForStableRead_BlocksReplacementUntilInspectionCompletes()
    {
        using var package = CandidatePackage.Create(0x8664);
        string replacementPath = Path.Combine(package.Root, "replacement.exe");
        File.WriteAllBytes(replacementPath, PeImageInspectorTests.CreatePeImage(0x8664));

        using FileStream stream = LlmFitCandidateVerifier.OpenRegularFileForStableRead(package.ExecutablePath);

        Exception exception = Assert.Throws<Exception>(
            () => File.Move(replacementPath, package.ExecutablePath, overwrite: true));
        Assert.IsTrue(exception is IOException or UnauthorizedAccessException);
        Assert.AreEqual(512, stream.Length);
    }

    [TestMethod]
    [DataRow("package.zip")]
    [DataRow("authenticode-observation.json")]
    public void Verify_StableArchiveAndObservationHandles_BlockReplacementThroughFinalLayout(string memberName)
    {
        using var package = CandidatePackage.Create(0x8664);
        string targetPath = Path.Combine(package.Root, memberName);
        using var metadata = new ReplacementAttemptingMetadata(targetPath);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier(metadata).Verify(package.Root, package.Manifest);

        Assert.IsTrue(result.IntegrityPassed);
        Assert.IsTrue(metadata.ReplacementAttempted);
        Assert.IsTrue(metadata.ReplacementBlocked);
    }

    [TestMethod]
    public void HasExactLogicalMemberSet_CaseCollision_IsRejected()
    {
        string[] expected = ["package.zip", "llmfit.exe", "LICENSE", "README.md", "authenticode-observation.json"];
        string[] physical = ["package.zip", "llmfit.exe", "LICENSE", "license", "authenticode-observation.json"];

        Assert.IsFalse(LlmFitCandidateVerifier.HasExactLogicalMemberSet(physical, expected));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HasExactLogicalMemberSet_WrongPhysicalCount_IsRejected(bool addUnexpectedMember)
    {
        string[] expected = ["package.zip", "llmfit.exe", "LICENSE", "README.md", "authenticode-observation.json"];
        string[] physical = addUnexpectedMember
            ? [.. expected, "unexpected.dll"]
            : expected[..^1];

        Assert.IsFalse(LlmFitCandidateVerifier.HasExactLogicalMemberSet(physical, expected));
    }

    [TestMethod]
    public void Verify_ManifestCannotExpandFixedFiveMemberAllowlist()
    {
        using var package = CandidatePackage.Create(0x8664);
        File.WriteAllBytes(Path.Combine(package.Root, "unexpected.dll"), [1]);
        LlmFitCandidateManifest manifest = package.WithRequiredFiles(
            ["llmfit.exe", "LICENSE", "README.md", "unexpected.dll"]);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, manifest);

        Assert.IsFalse(result.IntegrityPassed);
        Assert.IsNull(result.ArchiveSha256);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-PACKAGE-PATH-INVALID");
    }

    [TestMethod]
    public void OpenPackageRootForStableValidation_BlocksRootReplacementUntilVerificationCompletes()
    {
        using var package = CandidatePackage.Create(0x8664);
        string movedRoot = package.Root + "-moved";

        using Microsoft.Win32.SafeHandles.SafeFileHandle handle =
            LlmFitCandidateVerifier.OpenPackageRootForStableValidation(package.Root);

        try
        {
            Exception exception = Assert.Throws<Exception>(() => Directory.Move(package.Root, movedRoot));
            Assert.IsTrue(exception is IOException or UnauthorizedAccessException);
            Assert.IsFalse(handle.IsInvalid);
        }
        finally
        {
            if (!Directory.Exists(package.Root) && Directory.Exists(movedRoot))
            {
                Directory.Move(movedRoot, package.Root);
            }
        }
    }

    [TestMethod]
    public void GetStableRootPath_ResolvesTheDirectoryBoundToTheValidatedHandle()
    {
        using var package = CandidatePackage.Create(0x8664);
        using Microsoft.Win32.SafeHandles.SafeFileHandle handle =
            LlmFitCandidateVerifier.OpenPackageRootForStableValidation(package.Root);

        string stableRoot = LlmFitCandidateVerifier.GetStableRootPath(handle);

        Assert.AreEqual(Path.GetFullPath(package.Root), stableRoot, ignoreCase: true);
    }

    [TestMethod]
    public void Verify_OversizedObservation_IsRejectedBeforeUnboundedParsing()
    {
        using var package = CandidatePackage.Create(0x8664);
        string validObservation = File.ReadAllText(package.ObservationPath);
        File.WriteAllText(package.ObservationPath, new string(' ', 20_000) + validObservation);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, package.Manifest);

        Assert.IsFalse(result.IntegrityPassed);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-OBSERVATION-INVALID");
    }

    [TestMethod]
    [DataRow("{\"executableSha256\":\"{HASH}\",\"rawStatus\":\"NotSigned\",\"signaturePresent\":false,\"signerSubject\":null,\"signerThumbprint\":null,\"checkedAtUtc\":\"2026-08-15T12:00:00Z\",\"unknown\":true}")]
    [DataRow("{\"executableSha256\":\"{HASH}\",\"executableSha256\":\"{HASH}\",\"rawStatus\":\"NotSigned\",\"signaturePresent\":false,\"signerSubject\":null,\"signerThumbprint\":null,\"checkedAtUtc\":\"2026-08-15T12:00:00Z\"}")]
    [DataRow("{\"executableSha256\":\"{HASH}\",\"rawStatus\":\"NotSigned\",\"signaturePresent\":false,\"signerSubject\":\"..\\secret\",\"signerThumbprint\":null,\"checkedAtUtc\":\"2026-08-15T12:00:00Z\"}")]
    public void Verify_InvalidObservation_IsRejectedStrictly(string observationTemplate)
    {
        using var package = CandidatePackage.Create(0x8664);
        string observation = observationTemplate.Replace("{HASH}", package.Manifest.Executable.Sha256, StringComparison.Ordinal);
        File.WriteAllText(package.ObservationPath, observation);

        LlmFitCandidateVerification result = new LlmFitCandidateVerifier().Verify(package.Root, package.Manifest);

        Assert.IsFalse(result.IntegrityPassed);
        CollectionAssert.Contains(result.DiagnosticCodes.ToArray(), "HI-LLMFIT-OBSERVATION-INVALID");
    }

    private sealed class FakeFileMetadata(string reparsePath) : IFileMetadata
    {
        public FileAttributes GetAttributes(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            return string.Equals(Path.GetFullPath(path), Path.GetFullPath(reparsePath), StringComparison.OrdinalIgnoreCase)
                ? attributes | FileAttributes.ReparsePoint
                : attributes;
        }
    }

    private sealed class ReplacementAttemptingMetadata : IFileMetadata, IDisposable
    {
        private readonly string _replacementPath = Path.GetTempFileName();
        private readonly string _targetPath;
        private int _targetAttributeReads;

        public ReplacementAttemptingMetadata(string targetPath)
        {
            _targetPath = Path.GetFullPath(targetPath);
            File.Copy(_targetPath, _replacementPath, overwrite: true);
        }

        public bool ReplacementAttempted { get; private set; }

        public bool ReplacementBlocked { get; private set; }

        public FileAttributes GetAttributes(string path)
        {
            if (string.Equals(Path.GetFullPath(path), _targetPath, StringComparison.OrdinalIgnoreCase) &&
                ++_targetAttributeReads == 2)
            {
                ReplacementAttempted = true;
                try
                {
                    File.Move(_replacementPath, _targetPath, overwrite: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    ReplacementBlocked = true;
                }
            }

            return File.GetAttributes(path);
        }

        public void Dispose()
        {
            File.Delete(_replacementPath);
        }
    }

    private sealed class CandidatePackage : IDisposable
    {
        private CandidatePackage(string root, LlmFitCandidateManifest manifest)
        {
            Root = root;
            Manifest = manifest;
        }

        public string Root { get; }

        public string ExecutablePath => Path.Combine(Root, "llmfit.exe");

        public string ObservationPath => Path.Combine(Root, "authenticode-observation.json");

        public LlmFitCandidateManifest Manifest { get; private set; }

        public static CandidatePackage Create(ushort machine)
        {
            string root = Path.Combine(Path.GetTempPath(), "HardwareInspection.LlmFitSpike.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                byte[] executable = PeImageInspectorTests.CreatePeImage(machine);
                byte[] archive = Encoding.UTF8.GetBytes("deterministic archive fixture");
                string executableHash = Convert.ToHexString(SHA256.HashData(executable)).ToLowerInvariant();
                string archiveHash = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();

                File.WriteAllBytes(Path.Combine(root, "llmfit.exe"), executable);
                File.WriteAllText(Path.Combine(root, "LICENSE"), "MIT fixture");
                File.WriteAllText(Path.Combine(root, "README.md"), "fixture readme");
                File.WriteAllBytes(Path.Combine(root, "package.zip"), archive);
                File.WriteAllText(
                    Path.Combine(root, "authenticode-observation.json"),
                    $$"""{"executableSha256":"{{executableHash}}","rawStatus":"NotSigned","signaturePresent":false,"signerSubject":null,"signerThumbprint":null,"checkedAtUtc":"2026-08-15T12:00:00Z"}""");

                var manifest = new LlmFitCandidateManifest(
                    "1.0",
                    "fixture",
                    "1.1.9",
                    "v1.1.9",
                    new string('a', 40),
                    DateTimeOffset.Parse("2026-08-09T17:07:55Z", System.Globalization.CultureInfo.InvariantCulture),
                    new LlmFitCandidateArchive("package.zip", new Uri("https://github.com/example/package.zip"), archive.Length, archiveHash),
                    new LlmFitCandidateExecutable("llmfit.exe", executableHash, "AMD64", "ObserveAndRecord"),
                    ["llmfit.exe", "LICENSE", "README.md"],
                    new LlmFitCandidateCommands(["--version"], ["--no-dashboard", "--json", "system"]),
                    new LlmFitCandidateLicense("MIT", "LICENSE"));
                return new CandidatePackage(root, manifest);
            }
            catch
            {
                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        public LlmFitCandidateManifest WithArchive(LlmFitCandidateArchive archive)
        {
            Manifest = Clone(archive: archive);
            return Manifest;
        }

        public LlmFitCandidateManifest WithExecutable(LlmFitCandidateExecutable executable)
        {
            Manifest = Clone(executable: executable);
            return Manifest;
        }

        public LlmFitCandidateManifest WithRequiredFiles(IEnumerable<string> requiredFiles)
        {
            Manifest = Clone(requiredFiles: requiredFiles);
            return Manifest;
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }

        private LlmFitCandidateManifest Clone(
            LlmFitCandidateArchive? archive = null,
            LlmFitCandidateExecutable? executable = null,
            IEnumerable<string>? requiredFiles = null)
        {
            return new LlmFitCandidateManifest(
                Manifest.SchemaVersion,
                Manifest.CandidateId,
                Manifest.Version,
                Manifest.ReleaseTag,
                Manifest.ReleaseCommit,
                Manifest.PublishedAtUtc,
                archive ?? Manifest.Archive,
                executable ?? Manifest.Executable,
                requiredFiles ?? Manifest.RequiredFiles,
                Manifest.Commands,
                Manifest.License);
        }
    }
}
#pragma warning restore CA1707
