using System.Security.Cryptography;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class CandidateManifestTests
{
    [TestMethod]
    public void Load_accepts_candidate_bound_to_frozen_source_and_executable()
    {
        using TestDirectory directory = TestDirectory.Create();
        string executable = directory.WriteBytes("candidate.exe", [1, 2, 3, 4]);
        string manifest = directory.WriteText("candidate.json", $$"""
            {
              "schemaVersion": 1,
              "sourceCommit": "{{AuditIdentity.FrozenCommit}}",
              "sourceTree": "{{AuditIdentity.FrozenTree}}",
              "packageFamilyName": "488d3892-c214-40c5-9a6a-1154c1e69fff_test",
              "applicationId": "App",
              "executablePath": "{{Json(executable)}}",
              "executableSha256": "{{Sha256(executable)}}",
              "executableBytes": 4
            }
            """);

        CandidateManifest actual = CandidateManifest.Load(
            manifest,
            AuditIdentity.FrozenCommit,
            AuditIdentity.FrozenTree);

        Assert.AreEqual("488d3892-c214-40c5-9a6a-1154c1e69fff_test!App", actual.Aumid);
    }

    [TestMethod]
    public void Load_rejects_executable_hash_mismatch()
    {
        using TestDirectory directory = TestDirectory.Create();
        string executable = directory.WriteBytes("candidate.exe", [1, 2, 3]);
        string manifest = directory.WriteText("candidate.json", $$"""
            {
              "schemaVersion": 1,
              "sourceCommit": "{{AuditIdentity.FrozenCommit}}",
              "sourceTree": "{{AuditIdentity.FrozenTree}}",
              "packageFamilyName": "package_test",
              "applicationId": "App",
              "executablePath": "{{Json(executable)}}",
              "executableSha256": "{{new string('0', 64)}}",
              "executableBytes": 3
            }
            """);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            CandidateManifest.Load(
                manifest,
                AuditIdentity.FrozenCommit,
                AuditIdentity.FrozenTree));
        StringAssert.Contains(error.Message, "SHA-256");
    }

    [TestMethod]
    public void Load_accepts_candidate_bound_to_explicit_integrated_source()
    {
        const string integratedCommit =
            "cccccccccccccccccccccccccccccccccccccccc";
        const string integratedTree =
            "dddddddddddddddddddddddddddddddddddddddd";
        using TestDirectory directory = TestDirectory.Create();
        string executable = directory.WriteBytes("candidate.exe", [1, 2, 3, 4]);
        string manifest = directory.WriteText("candidate.json", $$"""
            {
              "schemaVersion": 1,
              "sourceCommit": "{{integratedCommit}}",
              "sourceTree": "{{integratedTree}}",
              "packageFamilyName": "integrated_test",
              "applicationId": "App",
              "executablePath": "{{Json(executable)}}",
              "executableSha256": "{{Sha256(executable)}}",
              "executableBytes": 4
            }
            """);

        CandidateManifest actual = CandidateManifest.Load(
            manifest,
            integratedCommit,
            integratedTree);

        Assert.AreEqual(integratedCommit, actual.SourceCommit);
        Assert.AreEqual(integratedTree, actual.SourceTree);
    }

    [TestMethod]
    public void Load_rejects_unbounded_package_identity()
    {
        using TestDirectory directory = TestDirectory.Create();
        string executable = directory.WriteBytes("candidate.exe", [1, 2, 3]);
        string manifest = directory.WriteText("candidate.json", $$"""
            {
              "schemaVersion": 1,
              "sourceCommit": "{{AuditIdentity.FrozenCommit}}",
              "sourceTree": "{{AuditIdentity.FrozenTree}}",
              "packageFamilyName": "package!unexpected",
              "applicationId": "App",
              "executablePath": "{{Json(executable)}}",
              "executableSha256": "{{Sha256(executable)}}",
              "executableBytes": 3
            }
            """);

        _ = Assert.ThrowsExactly<InvalidDataException>(() =>
            CandidateManifest.Load(
                manifest,
                AuditIdentity.FrozenCommit,
                AuditIdentity.FrozenTree));
    }

    internal static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    internal static string Json(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal);
}
