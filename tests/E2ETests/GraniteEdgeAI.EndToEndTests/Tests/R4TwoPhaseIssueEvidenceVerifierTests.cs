using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public void Exact_App_Control_prediscovery_block_closes_only_R3_020()
    {
        using TestDirectory directory = TestDirectory.Create();
        string receipt = WritePostReceipt(directory, appControlZeroEvidence: true);

        R4PostAcceptanceEvidence result = VerifyPostFixture(receipt, directory.Path);

        Assert.AreEqual("BLOCKED BY EXTERNAL ENVIRONMENT", result.Disposition);
        Assert.IsTrue(result.ClosesR3_020);
        Assert.IsFalse(result.ClosesR3_022);
    }

    [TestMethod]
    public void App_Control_zero_execution_requires_the_exact_blocked_receipt_and_command_shape()
    {
        using TestDirectory directory = TestDirectory.Create();
        var cases = new List<string>
        {
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => receipt["externalBlock"] = null),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => receipt["externalBlock"] = new Dictionary<string, object?> { ["kind"] = "weather" }),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => receipt["disposition"] = "CHANGES REQUIRED"),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => ((Dictionary<string, object?>)receipt["externalBlock"]!)["commandId"] = "PACKAGE-GATE"),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, manifest, _) => ((List<Dictionary<string, object?>>)manifest["commands"]!).RemoveAll(command => (string)command["id"]! == "E1-FOCUSED-HARNESS")),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (_, manifest, _) => ManifestCommand(manifest, "E1-FOCUSED-HARNESS")["exitCode"] = 0L),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (_, manifest, _) => ManifestCommand(manifest, "E1-FOCUSED-HARNESS")["discovered"] = 1L),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (_, manifest, _) => SetManifestCounts(ManifestCommand(manifest, "E1-FOCUSED-HARNESS"), 1, 0, 1, 0)),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (_, manifest, _) => SetManifestCounts(ManifestCommand(manifest, "E1-FOCUSED-HARNESS"), 1, 1, 0, 0)),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (_, manifest, _) => SetManifestCounts(ManifestCommand(manifest, "E1-FOCUSED-HARNESS"), 1, 0, 0, 1)),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => receipt["managedPassed"] = 1L),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => Gate(receipt, "native")["passed"] = 1L),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => receipt["blockedGatesCountedAsPasses"] = true),
            WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, manifest, _) =>
            {
                receipt["managedExecuted"] = 0L; receipt["managedPassed"] = 0L;
                foreach (string gate in new[] { "package", "appControl", "native", "e2e" }) SetGateCounts(Gate(receipt, gate), 0, 0, 0, 0);
                foreach (Dictionary<string, object?> command in (List<Dictionary<string, object?>>)manifest["commands"]!) SetManifestCounts(command, 0, 0, 0, 0);
            }),
            WritePostReceipt(directory, approvedEvidence: true, mutate: (receipt, _, _) => receipt["externalBlock"] = AppControlBlock()),
            WritePostReceipt(directory, appControlZeroEvidence: true, mutate: (receipt, _, _) => receipt["externalBlock"] = MissingPrerequisite("candidateManifest")),
            WritePostReceipt(directory, mutate: (receipt, _, _) => receipt["externalBlock"] = AppControlBlock()),
        };

        foreach (string receipt in cases)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, directory.Path), receipt);
        }
    }

    [TestMethod]
    public void App_Control_observation_requires_exact_attempt_identity_error_and_assembly_binding()
    {
        using TestDirectory directory = TestDirectory.Create();
        Action<Dictionary<string, object?>>[] mutations =
        [
            observation => observation["attempted"] = false,
            observation => observation["errorCode"] = "0x00000000",
            observation => observation["candidateCommit"] = new string('9', 40),
            observation => observation["candidateTree"] = new string('9', 40),
            observation => observation["implementationSubjectCommit"] = new string('9', 40),
            observation => observation["implementationSubjectTree"] = new string('9', 40),
            observation => observation["assemblySha256"] = new string('9', 64),
            observation => observation["assemblyBytes"] = 999L,
        ];
        foreach (Action<Dictionary<string, object?>> mutation in mutations)
        {
            string receipt = WritePostReceipt(directory, appControlZeroEvidence: true, mutateObservation: mutation);
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, directory.Path));
        }
    }

    [TestMethod]
    public void App_Control_observation_rejects_missing_empty_oversized_inaccessible_and_reparse_files()
    {
        using (TestDirectory missingDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(missingDirectory, appControlZeroEvidence: true);
            File.Delete(Path.Combine(missingDirectory.Path, "external-observation.json"));
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, missingDirectory.Path));
        }
        using (TestDirectory emptyDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(emptyDirectory, appControlZeroEvidence: true);
            RebindObservation(receipt, emptyDirectory.Path, []);
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, emptyDirectory.Path));
        }
        using (TestDirectory oversizedDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(oversizedDirectory, appControlZeroEvidence: true);
            RebindObservation(receipt, oversizedDirectory.Path, new byte[(1024 * 1024) + 1]);
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, oversizedDirectory.Path));
        }
        using (TestDirectory lockedDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(lockedDirectory, appControlZeroEvidence: true);
            using FileStream custody = new(Path.Combine(lockedDirectory.Path, "external-observation.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, lockedDirectory.Path));
        }
        using (TestDirectory reparseDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(reparseDirectory, appControlZeroEvidence: true);
            string junction = RebindObservationThroughJunction(receipt, reparseDirectory.Path);
            try
            {
                Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, reparseDirectory.Path));
            }
            finally
            {
                if (Directory.Exists(junction)) Directory.Delete(junction);
            }
        }
    }

    [TestMethod]
    public void Exact_final_App_Control_artifact_shape_is_accepted_without_closing_R3_022()
    {
        using TestDirectory directory = TestDirectory.Create();
        string receipt = WritePostReceipt(directory, appControlZeroEvidence: true);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(receipt));
        JsonElement root = document.RootElement;

        Assert.AreEqual(0, root.GetProperty("managedExecuted").GetInt64());
        Assert.AreEqual("appControlFailure", root.GetProperty("externalBlock").GetProperty("kind").GetString());
        Assert.AreEqual("E1-FOCUSED-HARNESS", root.GetProperty("externalBlock").GetProperty("commandId").GetString());
        R4PostAcceptanceEvidence result = VerifyPostFixture(receipt, directory.Path);
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
    public void App_Control_evidence_using_missing_prerequisite_positive_failure_shape_is_rejected()
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
        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, directory.Path));
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
    public void Approval_rejects_wrapped_Int64_gate_and_aggregate_arithmetic()
    {
        using TestDirectory directory = TestDirectory.Create();
        string receipt = WritePostReceipt(directory, approvedEvidence: true, mutate: (value, manifest, _) =>
        {
            long[] counts = [long.MaxValue, long.MaxValue, 4L, 1L];
            string[] gateNames = ["package", "appControl", "native", "e2e"];
            string[] commandIds = ["PACKAGE-GATE", "APP-CONTROL-GATE", "NATIVE-GATE", "E2E-GATE"];
            for (int index = 0; index < counts.Length; index++)
            {
                SetGateCounts(Gate(value, gateNames[index]), counts[index], counts[index], 0, 0);
                SetManifestCounts(ManifestCommand(manifest, commandIds[index]), counts[index], counts[index], 0, 0);
            }
            value["managedExecuted"] = 3L;
            value["managedPassed"] = 3L;
        });

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, directory.Path));
    }

    [TestMethod]
    public void Missing_prerequisite_evidence_rejects_the_App_Control_zero_command_shape()
    {
        using TestDirectory directory = TestDirectory.Create();
        string receipt = WritePostReceipt(directory, appControlZeroEvidence: true,
            mutate: (value, manifest, _) =>
            {
                value["externalBlock"] = MissingPrerequisite("candidateManifest");
                ((List<Dictionary<string, object?>>)manifest["commands"]!).Add(
                    ManifestGate("EXTERNAL-PREREQUISITE-PREFLIGHT", 0, 0, zeroBlocked: true));
            },
            mutateObservation: observation =>
            {
                foreach (string property in new[] { "commandId", "attempted", "discovered", "executed", "passed", "failed", "skipped", "exitCode", "errorCode", "observedFailure", "assemblySha256", "assemblyBytes" })
                {
                    observation.Remove(property);
                }
                observation["kind"] = "missingPrerequisite";
                observation["prerequisite"] = "candidateManifest";
                observation["observedAbsent"] = true;
            });

        Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, directory.Path));
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

    [TestMethod]
    public void Bound_evidence_rejects_empty_oversized_inaccessible_and_reparse_files()
    {
        using (TestDirectory emptyDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(emptyDirectory);
            RebindReport(receipt, emptyDirectory.Path, []);
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, emptyDirectory.Path));
        }

        using (TestDirectory oversizedDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(oversizedDirectory);
            RebindReport(receipt, oversizedDirectory.Path, new byte[(1024 * 1024) + 1]);
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, oversizedDirectory.Path));
        }

        using (TestDirectory lockedDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(lockedDirectory);
            string report = Path.Combine(lockedDirectory.Path, "report.md");
            using FileStream custody = new(report, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, lockedDirectory.Path));
        }

        using (TestDirectory reparseDirectory = TestDirectory.Create())
        {
            string receipt = WritePostReceipt(reparseDirectory);
            string junction = RebindReviewThroughJunction(receipt, reparseDirectory.Path);
            try
            {
                Assert.ThrowsExactly<InvalidDataException>(() => VerifyPostFixture(receipt, reparseDirectory.Path));
            }
            finally
            {
                if (Directory.Exists(junction)) Directory.Delete(junction);
            }
        }
    }

    private static R4PostAcceptanceEvidence VerifyPostFixture(string receipt, string root) =>
        R3IssueEvidenceVerifier.VerifyPostAcceptance(
            receipt, root, new string('a', 40), new string('b', 40),
            new string('c', 40), new string('d', 40));

    private static void RebindReport(string receiptPath, string root, byte[] bytes)
    {
        string reportPath = Path.Combine(root, "report.md");
        File.WriteAllBytes(reportPath, bytes);
        JsonObject receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!.AsObject();
        string manifestName = receipt["evidenceManifest"]!["path"]!.GetValue<string>();
        string manifestPath = Path.Combine(root, manifestName);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        JsonObject binding = BindingNode(reportPath);
        manifest["report"] = binding.DeepClone();
        receipt["report"] = binding.DeepClone();
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        receipt["evidenceManifest"] = BindingNode(manifestPath);
        File.WriteAllText(receiptPath, receipt.ToJsonString());
    }

    private static void RebindObservation(string receiptPath, string root, byte[] bytes)
    {
        string observationPath = Path.Combine(root, "external-observation.json");
        File.WriteAllBytes(observationPath, bytes);
        JsonObject receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!.AsObject();
        string manifestName = receipt["evidenceManifest"]!["path"]!.GetValue<string>();
        string manifestPath = Path.Combine(root, manifestName);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        JsonObject observation = manifest["outputs"]!.AsArray().Select(node => node!.AsObject())
            .Single(item => item["kind"]!.GetValue<string>() == "external-block-observation");
        JsonObject binding = BindingNode(observationPath);
        observation["sha256"] = binding["sha256"]!.DeepClone();
        observation["bytes"] = binding["bytes"]!.DeepClone();
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        receipt["evidenceManifest"] = BindingNode(manifestPath);
        File.WriteAllText(receiptPath, receipt.ToJsonString());
    }

    private static string RebindObservationThroughJunction(string receiptPath, string root)
    {
        string source = Path.Combine(root, "external-observation.json");
        string target = Path.Combine(root, "observation-target");
        string junction = Path.Combine(root, "observation-junction");
        Directory.CreateDirectory(target);
        File.Copy(source, Path.Combine(target, "external-observation.json"));
        CreateJunction(junction, target);
        JsonObject receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!.AsObject();
        string manifestName = receipt["evidenceManifest"]!["path"]!.GetValue<string>();
        string manifestPath = Path.Combine(root, manifestName);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        JsonObject observation = manifest["outputs"]!.AsArray().Select(node => node!.AsObject())
            .Single(item => item["kind"]!.GetValue<string>() == "external-block-observation");
        observation["id"] = "observation-junction/external-observation.json";
        ((JsonObject)receipt["externalBlock"]!)["observationId"] = "observation-junction/external-observation.json";
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        receipt["evidenceManifest"] = BindingNode(manifestPath);
        File.WriteAllText(receiptPath, receipt.ToJsonString());
        return junction;
    }

    private static string RebindReviewThroughJunction(string receiptPath, string root)
    {
        JsonObject receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!.AsObject();
        string manifestName = receipt["evidenceManifest"]!["path"]!.GetValue<string>();
        string manifestPath = Path.Combine(root, manifestName);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        JsonObject review = manifest["outputs"]!.AsArray().Select(node => node!.AsObject())
            .Single(item => item["kind"]!.GetValue<string>() == "independent-final-review");
        string reviewName = review["id"]!.GetValue<string>();
        string target = Path.Combine(root, "review-target");
        string junction = Path.Combine(root, "review-junction");
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(root, reviewName), Path.Combine(target, reviewName));
        CreateJunction(junction, target);
        review["id"] = $"review-junction/{reviewName}";
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        receipt["evidenceManifest"] = BindingNode(manifestPath);
        File.WriteAllText(receiptPath, receipt.ToJsonString());
        return junction;
    }

    private static JsonObject BindingNode(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return new JsonObject
        {
            ["path"] = Path.GetFileName(path),
            ["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            ["bytes"] = bytes.LongLength,
        };
    }

    private static void CreateJunction(string link, string target)
    {
        using Process process = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", $"New-Item -ItemType Junction -Path '{link.Replace("'", "''")}' -Target '{target.Replace("'", "''")}' | Out-Null" },
        }) ?? throw new InvalidOperationException("PowerShell did not start.");
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode);
    }

    private static string WritePostReceipt(
        TestDirectory directory,
        bool approvedEvidence = false,
        bool appControlZeroEvidence = false,
        Action<Dictionary<string, object?>, Dictionary<string, object?>, Dictionary<string, object?>>? mutate = null,
        Action<Dictionary<string, object?>>? mutateObservation = null)
    {
        string report = Path.Combine(directory.Path, "report.md");
        string manifestPath = Path.Combine(directory.Path, $"manifest-{Guid.NewGuid():N}.json");
        string reviewPath = Path.Combine(directory.Path, $"review-{Guid.NewGuid():N}.json");
        string observationPath = Path.Combine(directory.Path, "external-observation.json");
        File.WriteAllText(report, "evidence");
        long passed = approvedEvidence ? 1 : 0;
        long failed = approvedEvidence || appControlZeroEvidence ? 0 : 1;
        string blockedAssemblyPath = Path.Combine(directory.Path, "blocked-e1-assembly.dll");
        File.WriteAllBytes(blockedAssemblyPath, [1, 2, 3, 4]);
        string blockedAssemblySha256 = Digest(blockedAssemblyPath);
        var review = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1, ["reviewerRole"] = "independent",
            ["reviewedSubjectCommit"] = new string('c', 40), ["reviewedSubjectTree"] = new string('d', 40),
            ["disposition"] = "PASS_NO_REMAINING_CRITICAL_OR_IMPORTANT",
            ["criticalFindings"] = 0L, ["importantFindings"] = 0L, ["reviewComplete"] = true,
        };
        var commands = appControlZeroEvidence
            ? new List<Dictionary<string, object?>>
            {
                ManifestGate("PACKAGE-GATE", 0, 0, zeroBlocked: true),
                ManifestGate("E1-FOCUSED-HARNESS", 0, 0, zeroBlocked: true, resultSha256: blockedAssemblySha256),
                ManifestGate("NATIVE-GATE", 0, 0, zeroBlocked: true),
                ManifestGate("E2E-GATE", 0, 0, zeroBlocked: true),
            }
            : new List<Dictionary<string, object?>>
            {
                ManifestGate("PACKAGE-GATE", passed, failed),
                ManifestGate("APP-CONTROL-GATE", passed, failed),
                ManifestGate("NATIVE-GATE", passed, failed),
                ManifestGate("E2E-GATE", passed, failed),
            };
        if (!approvedEvidence && !appControlZeroEvidence) commands.Add(ManifestGate("EXTERNAL-PREREQUISITE-PREFLIGHT", 0, 1));
        var observation = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1, ["observer"] = "E1",
            ["candidateCommit"] = new string('a', 40), ["candidateTree"] = new string('b', 40),
            ["implementationSubjectCommit"] = new string('c', 40), ["implementationSubjectTree"] = new string('d', 40),
            ["kind"] = appControlZeroEvidence ? "appControlFailure" : "missingPrerequisite",
            ["observedAtUtc"] = "2026-09-01T10:00:00Z",
        };
        if (appControlZeroEvidence)
        {
            observation["commandId"] = "E1-FOCUSED-HARNESS"; observation["attempted"] = true;
            observation["discovered"] = 0L; observation["executed"] = 0L; observation["passed"] = 0L;
            observation["failed"] = 0L; observation["skipped"] = 0L; observation["exitCode"] = 1L;
            observation["errorCode"] = "0x800711C7"; observation["observedFailure"] = true;
            observation["assemblySha256"] = blockedAssemblySha256;
            observation["assemblyBytes"] = new FileInfo(blockedAssemblyPath).Length;
        }
        else
        {
            observation["prerequisite"] = "candidateManifest"; observation["observedAbsent"] = true;
        }
        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2, ["workerId"] = "E1", ["frozenSourceCommit"] = new string('f', 40),
            ["evidenceSubjectCommit"] = new string('c', 40), ["evidenceSubjectTree"] = new string('d', 40),
            ["createdAtUtc"] = "2026-08-31T20:00:00Z", ["route"] = new[] { "shared" },
            ["evidenceStatus"] = approvedEvidence ? "passed" : "blocked", ["report"] = FileBinding(report),
            ["inputs"] = appControlZeroEvidence
                ? new object[] { new Dictionary<string, object?>
                {
                    ["kind"] = "app-control-blocked-assembly", ["id"] = "E1-FINAL-SUBJECT-ASSEMBLY",
                    ["digest"] = blockedAssemblySha256, ["bytes"] = new FileInfo(blockedAssemblyPath).Length,
                    ["evidenceGrade"] = "blocked",
                } }
                : Array.Empty<object>(),
            ["outputs"] = new List<Dictionary<string, object?>>(),
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
            ["managedExecuted"] = appControlZeroEvidence ? 0 : 4, ["managedPassed"] = approvedEvidence ? 4 : 0,
            ["managedFailed"] = approvedEvidence || appControlZeroEvidence ? 0 : 4, ["managedSkipped"] = 0,
            ["blockedGatesCountedAsPasses"] = false,
            ["gateEvidence"] = new Dictionary<string, object?>
            {
                ["package"] = Gate("PACKAGE-GATE", passed, failed),
                ["appControl"] = Gate(appControlZeroEvidence ? "E1-FOCUSED-HARNESS" : "APP-CONTROL-GATE", passed, failed),
                ["native"] = Gate("NATIVE-GATE", passed, failed),
                ["e2e"] = Gate("E2E-GATE", passed, failed),
                ["cleanup"] = new Dictionary<string, object?> { ["executed"] = true, ["passed"] = true },
            },
            ["externalBlock"] = approvedEvidence ? null : appControlZeroEvidence ? AppControlBlock() : MissingPrerequisite("candidateManifest"),
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

    private static Dictionary<string, object?> AppControlBlock() => new()
    {
        ["kind"] = "appControlFailure", ["commandId"] = "E1-FOCUSED-HARNESS",
        ["errorCode"] = "0x800711C7", ["observationId"] = "external-observation.json",
    };

    private static Dictionary<string, object?> ManifestGate(
        string id, long passed, long failed, bool zeroBlocked = false, string? resultSha256 = null)
    {
        var result = new Dictionary<string, object?>
        {
            ["id"] = id, ["exitCode"] = failed == 0 && !zeroBlocked ? 0 : 1, ["discovered"] = passed + failed,
            ["executed"] = passed + failed, ["passed"] = passed, ["failed"] = failed, ["skipped"] = 0L,
            ["disposition"] = failed == 0 && !zeroBlocked ? "passed" : "blocked",
        };
        if (resultSha256 is not null) result["resultSha256"] = resultSha256;
        return result;
    }

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

    private static void SetManifestCounts(Dictionary<string, object?> value, long executed, long passed, long failed, long skipped)
    {
        SetGateCounts(value, executed, passed, failed, skipped);
        value["exitCode"] = executed == passed && failed == 0 && skipped == 0 && executed > 0 ? 0L : 1L;
        value["disposition"] = value["exitCode"]!.Equals(0L) ? "passed" : "blocked";
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
