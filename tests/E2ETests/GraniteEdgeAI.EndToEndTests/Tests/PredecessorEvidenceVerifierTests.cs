using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class PredecessorEvidenceVerifierTests
{
    [TestMethod]
    public void Verify_accepts_closed_cleanup_verified_F1_chain()
    {
        using TestDirectory directory = TestDirectory.Create();
        Chain chain = WriteChain(directory, "F1");

        PredecessorEvidence evidence = PredecessorEvidenceVerifier.Verify(
            "F1", directory.Path, chain.Handoff, chain.Native, evidenceManifestPath: null);

        Assert.AreEqual(new string('c', 40), evidence.FinalTip);
        Assert.AreEqual(new string('d', 40), evidence.FinalTree);
    }

    [TestMethod]
    public void Verify_rejects_native_receipt_not_bound_to_handoff_bytes()
    {
        using TestDirectory directory = TestDirectory.Create();
        Chain chain = WriteChain(directory, "F1", handoffDigestOverride: new string('0', 64));

        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
            PredecessorEvidenceVerifier.Verify("F1", directory.Path, chain.Handoff, chain.Native, null));

        StringAssert.Contains(error.Message, "handoff receipt SHA-256");
    }

    [TestMethod]
    public void Verify_requires_evidence_manifest_for_producer_worker()
    {
        using TestDirectory directory = TestDirectory.Create();
        Chain chain = WriteChain(directory, "H1");

        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
            PredecessorEvidenceVerifier.Verify("H1", directory.Path, chain.Handoff, chain.Native, null));

        StringAssert.Contains(error.Message, "evidence manifest");
    }

    [TestMethod]
    public void Verify_accepts_H1_manifest_with_subject_report_kinds_and_arithmetic_joined()
    {
        using TestDirectory directory = TestDirectory.Create();
        Chain chain = WriteChain(directory, "H1", includeEvidence: true);

        PredecessorEvidence evidence = PredecessorEvidenceVerifier.Verify(
            "H1", directory.Path, chain.Handoff, chain.Native, chain.Manifest);

        Assert.AreEqual(new string('a', 40), evidence.EvidenceSubjectCommit);
    }

    private static Chain WriteChain(
        TestDirectory directory,
        string workerId,
        string? handoffDigestOverride = null,
        bool includeEvidence = false)
    {
        string reportRelative = $"docs/audits/2026-08-28/{workerId}.md";
        string report = Path.Combine(directory.Path, reportRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, "sanitized report");
        string reportSha = Sha256(report);
        long reportBytes = new FileInfo(report).Length;
        string? manifestPath = null;
        string? manifestSha = null;
        long manifestBytes = 0;
        if (includeEvidence)
        {
            object manifest = new
            {
                schemaVersion = 1,
                workerId,
                frozenSourceCommit = AuditIdentity.FrozenCommit,
                evidenceSubjectCommit = new string('a', 40),
                evidenceSubjectTree = new string('b', 40),
                createdAtUtc = "2026-08-28T09:59:00Z",
                route = "shared",
                evidenceStatus = "passed",
                report = new { path = reportRelative, sha256 = reportSha, bytes = reportBytes },
                inputs = new[] { new { kind = "hardwareSnapshot", id = "hardware", evidenceGrade = "measured" } },
                outputs = new[]
                {
                    new { kind = "availableMemory", id = "memory", evidenceGrade = "measured" },
                    new { kind = "safetyBudget", id = "budget", evidenceGrade = "verified" },
                },
                commands = new[] { new { id = "H1.TEST", exitCode = 0, discovered = 1, executed = 1, passed = 1, failed = 0, skipped = 0 } },
                blockers = Array.Empty<string>(),
                nonClaims = Array.Empty<string>(),
            };
            manifestPath = directory.WriteText($"{workerId}-evidence.json", JsonSerializer.Serialize(manifest));
            manifestSha = Sha256(manifestPath);
            manifestBytes = new FileInfo(manifestPath).Length;
        }
        object handoff = new
        {
            schemaVersion = 1,
            workerId,
            frozenSourceCommit = AuditIdentity.FrozenCommit,
            frozenSourceTree = AuditIdentity.FrozenTree,
            branch = $"audit/{workerId.ToLowerInvariant()}-r2",
            finalTip = new string('c', 40),
            finalTree = new string('d', 40),
            worktreeClean = true,
            completedAtUtc = "2026-08-28T10:00:00Z",
            report = new { path = reportRelative, sha256 = reportSha, bytes = reportBytes },
            transport = "remote",
            remoteRef = $"refs/remotes/origin/audit/{workerId.ToLowerInvariant()}-r2",
            bundle = (object?)null,
            evidenceManifest = includeEvidence
                ? new
                {
                    path = $"docs/audits/2026-08-28/evidence/{workerId}.json",
                    sha256 = manifestSha,
                    bytes = manifestBytes,
                    evidenceSubjectCommit = new string('a', 40),
                    evidenceSubjectTree = new string('b', 40),
                }
                : (object?)null,
            testTotals = new { discovered = 1, executed = 1, passed = 1, failed = 0, skipped = 0 },
            nativeDisposition = "blocked",
        };
        string handoffPath = directory.WriteText($"{workerId}-handoff.json", JsonSerializer.Serialize(handoff));
        object native = new
        {
            workerId,
            phaseClosed = true,
            disposition = "blocked",
            frozenSourceCommit = AuditIdentity.FrozenCommit,
            frozenSourceTree = AuditIdentity.FrozenTree,
            evidenceSubjectCommit = new string('a', 40),
            evidenceSubjectTree = new string('b', 40),
            branch = $"audit/{workerId.ToLowerInvariant()}-r2",
            finalTip = new string('c', 40),
            finalTree = new string('d', 40),
            reportSha256 = reportSha,
            evidenceManifestSha256 = manifestSha,
            completedAtUtc = "2026-08-28T10:01:00Z",
            processCleanupVerified = true,
            handoffReceiptSha256 = handoffDigestOverride ?? Sha256(handoffPath),
        };
        string nativePath = directory.WriteText($"{workerId}-native.json", JsonSerializer.Serialize(native));
        return new Chain(handoffPath, nativePath, manifestPath);
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record Chain(string Handoff, string Native, string? Manifest);
}
