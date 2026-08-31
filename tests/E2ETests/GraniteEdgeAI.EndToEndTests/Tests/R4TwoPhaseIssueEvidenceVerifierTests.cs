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
    public void External_block_closes_R3_020_but_not_R3_022()
    {
        using TestDirectory directory = TestDirectory.Create();
        string receipt = WritePostReceipt(directory);

        R4PostAcceptanceEvidence result = R3IssueEvidenceVerifier.VerifyPostAcceptance(
            receipt, directory.Path, new string('a', 40), new string('b', 40),
            new string('c', 40), new string('d', 40));

        Assert.AreEqual("BLOCKED BY EXTERNAL ENVIRONMENT", result.Disposition);
        Assert.IsTrue(result.ClosesR3_020);
        Assert.IsFalse(result.ClosesR3_022);
    }

    [TestMethod]
    public void Arbitrary_malformed_or_disposition_mismatched_external_blocks_are_rejected()
    {
        using TestDirectory directory = TestDirectory.Create();
        string arbitrary = WritePostReceipt(directory, mutate: (receipt, _, _) =>
            receipt["externalBlock"] = new Dictionary<string, object?> { ["kind"] = "weather", ["prerequisite"] = "rain", ["observed"] = true });
        string blank = WritePostReceipt(directory, mutate: (receipt, _, _) =>
            receipt["externalBlock"] = MissingPrerequisite(""));
        string mismatch = WritePostReceipt(directory, mutate: (receipt, _, _) =>
            receipt["disposition"] = "CHANGES REQUIRED");

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(arbitrary, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(blank, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(mismatch, directory.Path));
    }

    [TestMethod]
    public void Observed_App_Control_failure_is_valid_structured_external_evidence()
    {
        using TestDirectory directory = TestDirectory.Create();
        string receipt = WritePostReceipt(directory, mutate: (value, _, _) => value["externalBlock"] = new Dictionary<string, object?>
        {
            ["kind"] = "appControlFailure", ["commandId"] = "APP-CONTROL-GATE",
            ["errorCode"] = "0x800711C7", ["observationId"] = "external-observation.json",
        }, mutateObservation: observation =>
        {
            observation["kind"] = "appControlFailure";
            observation.Remove("prerequisite");
            observation.Remove("observedAbsent");
            observation["commandId"] = "APP-CONTROL-GATE";
            observation["errorCode"] = "0x800711C7";
            observation["observedFailure"] = true;
        });
        R4PostAcceptanceEvidence result = VerifyPostFixture(receipt, directory.Path);
        Assert.IsTrue(result.ClosesR3_020);
        Assert.IsFalse(result.ClosesR3_022);
    }

    [TestMethod]
    public void Approval_requires_discrete_executed_passing_package_App_Control_native_cleanup_and_E2E_gates()
    {
        using TestDirectory directory = TestDirectory.Create();
        string receipt = WritePostReceipt(directory, approvedEvidence: true);
        R4PostAcceptanceEvidence result = VerifyPostFixture(receipt, directory.Path);
        Assert.IsTrue(result.ClosesR3_020);
        Assert.IsTrue(result.ClosesR3_022);
    }

    [TestMethod]
    public void Aggregate_only_or_missing_E2E_evidence_cannot_approve()
    {
        using TestDirectory directory = TestDirectory.Create();
        string aggregateOnly = WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, _, _) => receipt.Remove("gateEvidence"));
        string missingE2E = WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, _, _) =>
            GateEvidence(receipt).Remove("e2e"));

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(aggregateOnly, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(missingE2E, directory.Path));
    }

    [TestMethod]
    public void Fabricated_gate_values_or_aggregate_counters_cannot_approve()
    {
        using TestDirectory directory = TestDirectory.Create();
        string fabricated = WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, manifest, _) =>
        {
            Dictionary<string, object?> command = ManifestCommand(manifest, "E2E-GATE");
            command["passed"] = 0L;
            command["failed"] = 1L;
        });
        string aggregateLie = WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, _, _) =>
        {
            receipt["managedPassed"] = 999L;
            receipt["managedExecuted"] = 999L;
        });

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(fabricated, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(aggregateLie, directory.Path));
    }

    [TestMethod]
    public void Mismatched_manifest_subject_or_independent_review_semantics_cannot_approve()
    {
        using TestDirectory directory = TestDirectory.Create();
        string manifestMismatch = WritePostReceipt(directory, approvedEvidence: true, mutate: (_, manifest, _) =>
            manifest["evidenceSubjectCommit"] = new string('9', 40));
        string reviewMismatch = WritePostReceipt(directory, approvedEvidence: true, mutate: (_, _, review) =>
            review["importantFindings"] = 1L);

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(manifestMismatch, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(reviewMismatch, directory.Path));
    }

    [TestMethod]
    public void Unbound_or_mismatched_external_observation_cannot_close_R3_020()
    {
        using TestDirectory directory = TestDirectory.Create();
        string unbound = WritePostReceipt(directory, mutate: (receipt, _, _) =>
            ((Dictionary<string, object?>)receipt["externalBlock"]!)["observationId"] = "missing-observation.json");
        string mismatch = WritePostReceipt(directory, mutateObservation: observation =>
            observation["prerequisite"] = "assetManifest");

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(unbound, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(mismatch, directory.Path));
    }

    [TestMethod]
    public void Zero_package_or_lock_execution_cannot_close_R3_022()
    {
        using TestDirectory directory = TestDirectory.Create();
        string zeroPackage = WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, manifest, _) =>
        {
            SetGateCounts(Gate(receipt, "package"), 0, 0, 0, 0);
            SetGateCounts(ManifestCommand(manifest, "PACKAGE-GATE"), 0, 0, 0, 0);
        });
        string zeroLock = WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, _, _) =>
            receipt["nativeLockAcquisitions"] = 0L);

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(zeroPackage, directory.Path));
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(zeroLock, directory.Path));
    }

    private static R4PostAcceptanceEvidence VerifyPostFixture(string receipt, string root) =>
        R3IssueEvidenceVerifier.VerifyPostAcceptance(
            receipt, root, new string('a', 40), new string('b', 40),
            new string('c', 40), new string('d', 40));

    private static string WritePostReceipt(
        TestDirectory directory,
        bool approvedEvidence = false,
        Action<Dictionary<string, object?>, Dictionary<string, object?>, Dictionary<string, object?>>? mutate = null,
        Action<Dictionary<string, object?>>? mutateObservation = null)
    {
        string report = Path.Combine(directory.Path, "report.md");
        string manifestPath = Path.Combine(directory.Path, $"manifest-{Guid.NewGuid():N}.json");
        string reviewPath = Path.Combine(directory.Path, $"review-{Guid.NewGuid():N}.json");
        string observationPath = Path.Combine(directory.Path, "external-observation.json");
        File.WriteAllText(report, "evidence");
        long passed = approvedEvidence ? 1 : 0;
        long failed = approvedEvidence ? 0 : 1;
        var review = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1, ["reviewerRole"] = "independent",
            ["reviewedSubjectCommit"] = new string('c', 40), ["reviewedSubjectTree"] = new string('d', 40),
            ["disposition"] = "PASS_NO_REMAINING_CRITICAL_OR_IMPORTANT",
            ["criticalFindings"] = 0L, ["importantFindings"] = 0L, ["reviewComplete"] = true,
        };
        var commands = new List<Dictionary<string, object?>>
        {
            ManifestGate("PACKAGE-GATE", passed, failed),
            ManifestGate("APP-CONTROL-GATE", passed, failed),
            ManifestGate("NATIVE-GATE", passed, failed),
            ManifestGate("E2E-GATE", passed, failed),
        };
        if (!approvedEvidence) commands.Add(ManifestGate("EXTERNAL-PREREQUISITE-PREFLIGHT", 0, 1));
        var observation = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1, ["observer"] = "E1",
            ["candidateCommit"] = new string('a', 40), ["candidateTree"] = new string('b', 40),
            ["implementationSubjectCommit"] = new string('c', 40), ["implementationSubjectTree"] = new string('d', 40),
            ["kind"] = "missingPrerequisite", ["prerequisite"] = "candidateManifest",
            ["observedAbsent"] = true, ["observedAtUtc"] = "2026-08-31T20:00:00Z",
        };
        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2, ["workerId"] = "E1", ["frozenSourceCommit"] = new string('f', 40),
            ["evidenceSubjectCommit"] = new string('c', 40), ["evidenceSubjectTree"] = new string('d', 40),
            ["createdAtUtc"] = "2026-08-31T20:00:00Z", ["route"] = new[] { "shared" },
            ["evidenceStatus"] = approvedEvidence ? "passed" : "blocked", ["report"] = FileBinding(report),
            ["inputs"] = Array.Empty<object>(), ["outputs"] = new List<Dictionary<string, object?>>(),
            ["commands"] = commands, ["blockers"] = approvedEvidence ? Array.Empty<string>() : new[] { "candidate manifest missing" },
            ["nonClaims"] = new[] { "none" },
        };
        var receipt = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2, ["workerId"] = "E1",
            ["baseCommit"] = new string('a', 40), ["baseTree"] = new string('b', 40),
            ["implementationSubjectCommit"] = new string('c', 40), ["implementationSubjectTree"] = new string('d', 40),
            ["disposition"] = approvedEvidence ? "APPROVED FOR MAIN INTEGRATION" : "BLOCKED BY EXTERNAL ENVIRONMENT",
            ["report"] = FileBinding(report),
            ["nativeLockAcquisitions"] = approvedEvidence ? 1 : 0,
            ["managedExecuted"] = approvedEvidence ? 4 : 4, ["managedPassed"] = approvedEvidence ? 4 : 0,
            ["managedFailed"] = approvedEvidence ? 0 : 4, ["managedSkipped"] = 0,
            ["blockedGatesCountedAsPasses"] = false,
            ["gateEvidence"] = new Dictionary<string, object?>
            {
                ["package"] = Gate("PACKAGE-GATE", passed, failed),
                ["appControl"] = Gate("APP-CONTROL-GATE", passed, failed),
                ["native"] = Gate("NATIVE-GATE", passed, failed),
                ["e2e"] = Gate("E2E-GATE", passed, failed),
                ["cleanup"] = new Dictionary<string, object?> { ["executed"] = true, ["passed"] = true },
            },
            ["externalBlock"] = approvedEvidence ? null : MissingPrerequisite("candidateManifest"),
        };
        mutate?.Invoke(receipt, manifest, review);
        mutateObservation?.Invoke(observation);
        File.WriteAllText(reviewPath, JsonSerializer.Serialize(review));
        if (!approvedEvidence) File.WriteAllText(observationPath, JsonSerializer.Serialize(observation));
        ((List<Dictionary<string, object?>>)manifest["outputs"]!).Add(new Dictionary<string, object?>
        {
            ["kind"] = "independent-final-review", ["id"] = Path.GetFileName(reviewPath),
            ["sha256"] = Digest(reviewPath), ["bytes"] = new FileInfo(reviewPath).Length, ["evidenceGrade"] = "verified",
        });
        if (!approvedEvidence)
        {
            ((List<Dictionary<string, object?>>)manifest["outputs"]!).Add(new Dictionary<string, object?>
            {
                ["kind"] = "external-block-observation", ["id"] = Path.GetFileName(observationPath),
                ["sha256"] = Digest(observationPath), ["bytes"] = new FileInfo(observationPath).Length, ["evidenceGrade"] = "verified",
            });
        }
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        receipt["evidenceManifest"] = FileBinding(manifestPath);
        string path = Path.Combine(directory.Path, $"receipt-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(receipt));
        return path;
    }

    private static Dictionary<string, object?> MissingPrerequisite(string prerequisite) => new()
    {
        ["kind"] = "missingPrerequisite", ["prerequisite"] = prerequisite,
        ["commandId"] = "EXTERNAL-PREREQUISITE-PREFLIGHT", ["observationId"] = "external-observation.json",
    };

    private static Dictionary<string, object?> ManifestGate(string id, long passed, long failed) => new()
    {
        ["id"] = id, ["exitCode"] = failed == 0 ? 0 : 1, ["discovered"] = passed + failed,
        ["executed"] = passed + failed, ["passed"] = passed, ["failed"] = failed, ["skipped"] = 0L,
        ["disposition"] = failed == 0 ? "passed" : "blocked",
    };

    private static Dictionary<string, object?> Gate(string id, long passed, long failed) => new()
    {
        ["commandId"] = id, ["executed"] = passed + failed, ["passed"] = passed, ["failed"] = failed, ["skipped"] = 0L,
    };

    private static Dictionary<string, object?> Gate(Dictionary<string, object?> receipt, string name) =>
        (Dictionary<string, object?>)GateEvidence(receipt)[name]!;

    private static Dictionary<string, object?> GateEvidence(Dictionary<string, object?> receipt) =>
        (Dictionary<string, object?>)receipt["gateEvidence"]!;

    private static Dictionary<string, object?> ManifestCommand(Dictionary<string, object?> manifest, string id) =>
        ((List<Dictionary<string, object?>>)manifest["commands"]!).Single(command => (string)command["id"]! == id);

    private static void SetGateCounts(Dictionary<string, object?> value, long executed, long passed, long failed, long skipped)
    {
        value["executed"] = executed; value["passed"] = passed; value["failed"] = failed; value["skipped"] = skipped;
        if (value.ContainsKey("discovered")) value["discovered"] = executed;
    }

    private static object FileBinding(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return new { path = Path.GetFileName(path), sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), bytes = bytes.LongLength };
    }

    private static string Digest(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

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
        Assert.IsFalse(result.ClosesR3_022);
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
