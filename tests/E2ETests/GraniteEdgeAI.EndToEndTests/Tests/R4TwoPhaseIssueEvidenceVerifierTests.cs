using System.Text.Json;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class R4TwoPhaseIssueEvidenceVerifierTests
{
    [TestMethod]
    public void Verify_accepts_schema_v4_with_only_E1_post_acceptance_rows_pending()
    {
        using TestDirectory directory = TestDirectory.Create();
        Fixture fixture = WriteFixture(directory);

        R4TwoPhaseIssueEvidence result = R3IssueEvidenceVerifier.VerifyTwoPhase(
            fixture.Closure,
            fixture.Catalog,
            directory.Path);

        Assert.AreEqual(28, result.ClosedPreflightFindings.Count);
        CollectionAssert.AreEqual(
            new[] { "R3-020", "R3-022" },
            result.PendingPostAcceptanceIds.ToArray());
        Assert.IsTrue(result.CanStartAcceptanceMatrix);
    }

    [TestMethod]
    public void Verify_rejects_unknown_or_misplaced_pending_findings()
    {
        using TestDirectory directory = TestDirectory.Create();
        Fixture unknown = WriteFixture(directory, closure =>
            Issues(closure)[0]["issueId"] = "R3-999");
        Fixture misplacedPending = WriteFixture(directory, closure =>
        {
            Dictionary<string, object?> issue = Issues(closure)[0];
            issue.Remove("productDefectRemains");
            issue.Remove("commandIds");
            issue["phase"] = "postAcceptance";
            issue["state"] = "PENDING_E1_ACCEPTANCE";
            issue["productDefectDisposition"] = "UNDETERMINED_UNTIL_E1";
            issue["acceptanceOwner"] = "E1";
            issue["requiredEvidence"] = new[] { "candidateBoundReceipt" };
        });

        Assert.ThrowsExactly<InvalidDataException>(() =>
            R3IssueEvidenceVerifier.VerifyTwoPhase(unknown.Closure, unknown.Catalog, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            R3IssueEvidenceVerifier.VerifyTwoPhase(misplacedPending.Closure, misplacedPending.Catalog, directory.Path));
    }

    [TestMethod]
    public void Verify_rejects_non_green_or_missing_catalog_commands()
    {
        using TestDirectory directory = TestDirectory.Create();
        Fixture skipped = WriteFixture(directory, null, catalog =>
            Command(catalog)["skipped"] = 1);
        Fixture missing = WriteFixture(directory, closure =>
            Issues(closure)[0]["commandIds"] = new[] { "MISSING-COMMAND" });

        Assert.ThrowsExactly<InvalidDataException>(() =>
            R3IssueEvidenceVerifier.VerifyTwoPhase(skipped.Closure, skipped.Catalog, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            R3IssueEvidenceVerifier.VerifyTwoPhase(missing.Closure, missing.Catalog, directory.Path));
    }

    [TestMethod]
    public void Verify_post_acceptance_binds_receipt_and_keeps_blocked_gates_out_of_passes()
    {
        using TestDirectory directory = TestDirectory.Create();
        string report = Path.Combine(directory.Path, "report.md");
        string manifest = Path.Combine(directory.Path, "manifest.json");
        File.WriteAllText(report, "blocked");
        File.WriteAllText(manifest, "{}");
        string receipt = Path.Combine(directory.Path, "receipt.json");
        File.WriteAllText(receipt, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            workerId = "E1",
            baseCommit = new string('a', 40),
            baseTree = new string('b', 40),
            implementationSubjectCommit = new string('c', 40),
            implementationSubjectTree = new string('d', 40),
            disposition = "BLOCKED BY EXTERNAL ENVIRONMENT",
            report = FileBinding(report),
            evidenceManifest = FileBinding(manifest),
            nativeLockAcquisitions = 0,
            cleanupVerified = true,
            packageAttempted = false,
            appControlChecked = true,
            managedExecuted = 311,
            managedPassed = 311,
            managedFailed = 0,
            managedSkipped = 0,
            blockedGatesCountedAsPasses = false,
            externalBlock = "exact native prerequisites absent before lock"
        }));

        R4PostAcceptanceEvidence result = R3IssueEvidenceVerifier.VerifyPostAcceptance(
            receipt, directory.Path, new string('a', 40), new string('b', 40),
            new string('c', 40), new string('d', 40));

        Assert.AreEqual("BLOCKED BY EXTERNAL ENVIRONMENT", result.Disposition);
        Assert.IsTrue(result.ClosesR3_020);
        Assert.IsTrue(result.ClosesR3_022);

        string mutated = File.ReadAllText(receipt).Replace(
            "\"blockedGatesCountedAsPasses\":false", "\"blockedGatesCountedAsPasses\":true");
        File.WriteAllText(receipt, mutated);
        Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.VerifyPostAcceptance(
            receipt, directory.Path, new string('a', 40), new string('b', 40),
            new string('c', 40), new string('d', 40)));
    }

    private static object FileBinding(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return new { path = Path.GetFileName(path), sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), bytes = bytes.LongLength };
    }

    [TestMethod]
    [TestCategory("Preflight")]
    public void Exact_schema_v4_candidate_closure_and_catalog_are_committed_and_pushed()
    {
        string repositoryRoot = RequireEnvironment("GRANITE_E2E_REPOSITORY_ROOT");
        R4TwoPhaseIssueEvidence result = R3ReleaseVetoPreflight.VerifyTwoPhase(
            repositoryRoot,
            RequireEnvironment("GRANITE_E2E_R3_CLOSURE_MANIFEST"),
            RequireEnvironment("GRANITE_E2E_R3_CLOSURE_RELATIVE_PATH"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_COMMIT"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_TREE"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_REMOTE"),
            RequireEnvironment("GRANITE_E2E_CANDIDATE_REMOTE_REF"));

        Assert.IsTrue(result.CanStartAcceptanceMatrix);
    }

    [TestMethod]
    [TestCategory("PostAcceptance")]
    public void Exact_E1_post_acceptance_evidence_is_candidate_bound_and_fail_closed()
    {
        string repositoryRoot = RequireEnvironment("GRANITE_E2E_REPOSITORY_ROOT");
        R4PostAcceptanceEvidence result = R3IssueEvidenceVerifier.VerifyPostAcceptance(
            Path.Combine(repositoryRoot, "docs/audits/2026-08-30/evidence/E1-r4-post-acceptance-v2.json"),
            repositoryRoot,
            "b5d2cd34c57368efb9b122cddf16c2ffa2d3895e",
            "a3e4d82095caa9688f30d2463fa5971c788fd33f",
            RequireEnvironment("GRANITE_E2E_IMPLEMENTATION_COMMIT"),
            RequireEnvironment("GRANITE_E2E_IMPLEMENTATION_TREE"));
        Assert.IsTrue(result.ClosesR3_020);
        Assert.IsTrue(result.ClosesR3_022);
    }

    private static Fixture WriteFixture(
        TestDirectory directory,
        Action<Dictionary<string, object?>>? mutateClosure = null,
        Action<Dictionary<string, object?>>? mutateCatalog = null)
    {
        string artifact = Path.Combine(directory.Path, "evidence.txt");
        File.WriteAllText(artifact, "evidence");
        var command = new Dictionary<string, object?>
        {
            ["evidenceType"] = "behavioralTest",
            ["discovered"] = 1,
            ["executed"] = 1,
            ["passed"] = 1,
            ["failed"] = 0,
            ["skipped"] = 0,
        };
        var catalog = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["coordinator"] = "C0",
            ["createdAtUtc"] = "2026-08-31T05:01:08Z",
            ["productSubject"] = GitSubject(),
            ["evidenceBase"] = new Dictionary<string, object?>
            {
                ["commit"] = new string('c', 40),
                ["tree"] = new string('d', 40),
                ["manifest"] = Blob("evidence.txt"),
            },
            ["commands"] = new Dictionary<string, object?> { ["GREEN"] = command },
            ["issues"] = Enumerable.Range(1, 22).Select(index => CatalogIssue(index)).ToArray(),
            ["r4Findings"] = Enumerable.Range(1, 8).Select(index => new Dictionary<string, object?>
            {
                ["findingId"] = $"R4-C0-{index:000}",
                ["commandIds"] = new[] { "GREEN" },
                ["sourceArtifacts"] = new[] { "evidence.txt" },
            }).ToArray(),
            ["nonClaims"] = new[] { "none" },
        };
        mutateCatalog?.Invoke(catalog);

        List<Dictionary<string, object?>> issues = Enumerable.Range(1, 22).Select(index =>
        {
            string id = $"R3-{index:000}";
            if (index is 20 or 22)
            {
                return new Dictionary<string, object?>
                {
                    ["issueId"] = id,
                    ["phase"] = "postAcceptance",
                    ["state"] = "PENDING_E1_ACCEPTANCE",
                    ["productDefectDisposition"] = "UNDETERMINED_UNTIL_E1",
                    ["acceptanceOwner"] = "E1",
                    ["requiredEvidence"] = new[] { "candidateBoundReceipt" },
                };
            }
            return Closed("issueId", id);
        }).ToList();
        List<Dictionary<string, object?>> r4 = Enumerable.Range(1, 8)
            .Select(index => Closed("findingId", $"R4-C0-{index:000}"))
            .ToList();
        var closure = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 4,
            ["contract"] = "C0-R4-ISSUE-CLOSURE-V4",
            ["coordinator"] = "C0",
            ["createdAtUtc"] = "2026-08-31T05:01:08Z",
            ["productSubject"] = GitSubject(),
            ["evidenceCatalog"] = new Dictionary<string, object?>
            {
                ["evidenceSubjectCommit"] = new string('a', 40),
                ["evidenceSubjectTree"] = new string('b', 40),
                ["evidenceBlob"] = Blob("catalog.json"),
            },
            ["issues"] = issues,
            ["r4Findings"] = r4,
            ["preflightPolicy"] = new Dictionary<string, object?>
            {
                ["requiredClosedR3"] = issues.Where(i => (string)i["phase"]! == "preflight").Select(i => i["issueId"]).ToArray(),
                ["requiredPendingE1Acceptance"] = new[] { "R3-020", "R3-022" },
                ["requiredClosedR4"] = r4.Select(i => i["findingId"]).ToArray(),
                ["rule"] = "two phase",
            },
            ["postAcceptancePolicy"] = new Dictionary<string, object?> { ["owner"] = "E1", ["rule"] = "after attempts" },
            ["nonClaims"] = new[] { "none" },
        };
        mutateClosure?.Invoke(closure);
        string catalogPath = Path.Combine(directory.Path, $"catalog-{Guid.NewGuid():N}.json");
        File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog));
        string closurePath = Path.Combine(directory.Path, $"closure-{Guid.NewGuid():N}.json");
        File.WriteAllText(closurePath, JsonSerializer.Serialize(closure));
        return new Fixture(closurePath, catalogPath);
    }

    private static Dictionary<string, object?> Closed(string idName, string id) => new()
    {
        [idName] = id,
        ["phase"] = "preflight",
        ["state"] = "CLOSED",
        ["productDefectRemains"] = false,
        ["commandIds"] = new[] { "GREEN" },
    };

    private static Dictionary<string, object?> CatalogIssue(int index)
    {
        var issue = new Dictionary<string, object?>
        {
            ["issueId"] = $"R3-{index:000}",
            ["commandIds"] = index is 20 or 22 ? null : new[] { "GREEN" },
        };
        if (index is not 20 and not 22)
        {
            issue["sourceArtifacts"] = new[] { "evidence.txt" };
        }
        if (index is >= 4 and <= 18)
        {
            issue["productionReachability"] = new Dictionary<string, object?>
            {
                ["definition"] = "evidence.txt",
                ["productionCaller"] = "evidence.txt",
                ["registrationPoint"] = "evidence.txt",
                ["behavioralRegressionTest"] = "evidence.txt",
                ["observedRegistrations"] = 1,
            };
        }
        return issue;
    }

    private static Dictionary<string, object?> GitSubject() => new()
    {
        ["commit"] = new string('1', 40),
        ["tree"] = new string('2', 40),
    };

    private static Dictionary<string, object?> Blob(string path) => new()
    {
        ["path"] = path,
        ["sha256"] = new string('3', 64),
        ["bytes"] = 1,
    };

    private static List<Dictionary<string, object?>> Issues(Dictionary<string, object?> closure) =>
        (List<Dictionary<string, object?>>)closure["issues"]!;

    private static Dictionary<string, object?> Command(Dictionary<string, object?> catalog) =>
        (Dictionary<string, object?>)((Dictionary<string, object?>)catalog["commands"]!)["GREEN"]!;

    private static string RequireEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidDataException($"CHANGES REQUIRED: {name} is required.");

    private sealed record Fixture(string Closure, string Catalog);
}
