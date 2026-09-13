using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using GraniteEdgeAI.R4Handoff.Validation;

namespace GraniteEdgeAI.SecurityAudit.Tests;

[TestClass]
public sealed class R4SchemaContractTests
{
    [TestMethod]
    public void ReceiptSchemaAcceptsS1RemoteReceiptAndRejectsTrustMutations()
    {
        JsonSchema schema = LoadSchema("R4-HANDOFF-RECEIPT-SCHEMA.json");
        JsonObject valid = ValidReceipt();
        AssertValid(schema, valid);
        Assert.IsTrue(R4HandoffSemanticValidator.HasValidReceiptArithmetic(
            JsonSerializer.SerializeToElement(valid)));

        AssertInvalid(schema, Mutate(valid, "workerId", "X1"));
        AssertInvalid(schema, Mutate(valid, "branch", "feature/not-authorized"));
        AssertInvalid(schema, Mutate(valid, "implementationSubjectCommit", new string('a', 39)));
        AssertInvalid(schema, MutateNested(valid, "report", "sha256", new string('A', 64)));
        AssertInvalid(schema, Mutate(valid, "nativeDisposition", "claimed"));
        AssertInvalid(schema, Mutate(valid, "unexpected", true));

        JsonObject remoteWithBundle = Mutate(valid, "bundle", new JsonObject
        {
            ["filename"] = "r4.bundle",
            ["sha256"] = new string('b', 64),
            ["bytes"] = 1,
        });
        AssertInvalid(schema, remoteWithBundle);
        AssertInvalid(schema, Mutate(valid, "remoteRef", null));

        JsonObject badArithmetic = (JsonObject)valid.DeepClone();
        badArithmetic["testTotals"]!["passed"] = 6;
        Assert.IsFalse(R4HandoffSemanticValidator.HasValidReceiptArithmetic(
            JsonSerializer.SerializeToElement(badArithmetic)));

        JsonObject overflowArithmetic = (JsonObject)valid.DeepClone();
        overflowArithmetic["testTotals"]!["passed"] = int.MaxValue;
        overflowArithmetic["testTotals"]!["failed"] = int.MaxValue;
        Assert.IsFalse(R4HandoffSemanticValidator.HasValidReceiptArithmetic(
            JsonSerializer.SerializeToElement(overflowArithmetic)));

        JsonObject schemaRangeArithmetic = (JsonObject)valid.DeepClone();
        schemaRangeArithmetic["testTotals"]!["discovered"] = long.MaxValue;
        schemaRangeArithmetic["testTotals"]!["executed"] = long.MaxValue;
        schemaRangeArithmetic["testTotals"]!["passed"] = long.MaxValue - 2;
        schemaRangeArithmetic["testTotals"]!["failed"] = 1;
        schemaRangeArithmetic["testTotals"]!["skipped"] = 1;
        AssertValid(schema, schemaRangeArithmetic);
        Assert.IsTrue(R4HandoffSemanticValidator.HasValidReceiptArithmetic(
            JsonSerializer.SerializeToElement(schemaRangeArithmetic)));
    }

    [TestMethod]
    public void EvidenceSchemaAcceptsMeasuredManifestAndRejectsEvidenceMutations()
    {
        JsonSchema schema = LoadSchema("R4-EVIDENCE-MANIFEST-SCHEMA.json");
        JsonObject valid = ValidEvidence();
        AssertValid(schema, valid);
        Assert.IsTrue(R4HandoffSemanticValidator.HasValidEvidenceArithmetic(
            JsonSerializer.SerializeToElement(valid)));

        AssertInvalid(schema, Mutate(valid, "workerId", "S1"));
        AssertInvalid(schema, Mutate(valid, "evidenceStatus", "complete"));
        JsonObject badGrade = (JsonObject)valid.DeepClone();
        badGrade["inputs"]![0]!["evidenceGrade"] = "asserted";
        AssertInvalid(schema, badGrade);
        AssertInvalid(schema, Mutate(valid, "unexpected", true));

        JsonObject badArithmetic = (JsonObject)valid.DeepClone();
        badArithmetic["commands"]![0]!["executed"] = 2;
        Assert.IsFalse(R4HandoffSemanticValidator.HasValidEvidenceArithmetic(
            JsonSerializer.SerializeToElement(badArithmetic)));
    }

    private static JsonSchema LoadSchema(string filename)
    {
        string root = FindRepositoryRoot();
        string text = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "audits",
            "2026-08-30",
            "schemas",
            filename));
        return JsonSchema.FromText(text);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json"))
                && (Directory.Exists(Path.Combine(current.FullName, ".git"))
                    || File.Exists(Path.Combine(current.FullName, ".git"))))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("The repository root could not be resolved.");
    }

    private static void AssertValid(JsonSchema schema, JsonObject instance) =>
        Assert.IsTrue(schema.Evaluate(JsonSerializer.SerializeToElement(instance)).IsValid);

    private static void AssertInvalid(JsonSchema schema, JsonObject instance) =>
        Assert.IsFalse(schema.Evaluate(JsonSerializer.SerializeToElement(instance)).IsValid);

    private static JsonObject Mutate(JsonObject source, string key, JsonNode? value)
    {
        JsonObject clone = (JsonObject)source.DeepClone();
        clone[key] = value?.DeepClone();
        return clone;
    }

    private static JsonObject MutateNested(
        JsonObject source,
        string parent,
        string key,
        JsonNode? value)
    {
        JsonObject clone = (JsonObject)source.DeepClone();
        clone[parent]![key] = value?.DeepClone();
        return clone;
    }

    private static JsonObject ValidReceipt() => new()
    {
        ["schemaVersion"] = 2,
        ["workerId"] = "S1",
        ["frozenSourceCommit"] = "4748fe04f19afdf6b27c4c12502b84db325e7294",
        ["frozenSourceTree"] = "fe1fa8fb5fe4de8e7c1d867a83e08375bc1d0c91",
        ["baseCommit"] = new string('a', 40),
        ["baseTree"] = new string('b', 40),
        ["branch"] = "audit/ucl-s1-security-remediation-r4",
        ["implementationSubjectCommit"] = new string('c', 40),
        ["implementationSubjectTree"] = new string('d', 40),
        ["worktreeCleanAtHandoff"] = true,
        ["completedAtUtc"] = "2026-08-30T12:00:00Z",
        ["report"] = new JsonObject
        {
            ["path"] = "docs/audits/2026-08-30/S1-security-packaging-r4.md",
            ["sha256"] = new string('e', 64),
            ["bytes"] = 100,
        },
        ["transport"] = "remote",
        ["remoteRef"] = "refs/remotes/origin/audit/ucl-s1-security-remediation-r4",
        ["bundle"] = null,
        ["evidenceManifest"] = null,
        ["testTotals"] = new JsonObject
        {
            ["discovered"] = 7,
            ["executed"] = 7,
            ["passed"] = 7,
            ["failed"] = 0,
            ["skipped"] = 0,
        },
        ["nativeDisposition"] = "blocked",
    };

    private static JsonObject ValidEvidence() => new()
    {
        ["schemaVersion"] = 2,
        ["workerId"] = "H1",
        ["frozenSourceCommit"] = "4748fe04f19afdf6b27c4c12502b84db325e7294",
        ["evidenceSubjectCommit"] = new string('a', 40),
        ["evidenceSubjectTree"] = new string('b', 40),
        ["createdAtUtc"] = "2026-08-30T12:00:00Z",
        ["route"] = "shared",
        ["evidenceStatus"] = "passed",
        ["report"] = new JsonObject
        {
            ["path"] = "docs/audits/2026-08-30/H1-hardware-r4.md",
            ["sha256"] = new string('c', 64),
            ["bytes"] = 100,
        },
        ["inputs"] = new JsonArray(new JsonObject
        {
            ["kind"] = "commit",
            ["id"] = "source",
            ["evidenceGrade"] = "verified",
        }),
        ["outputs"] = new JsonArray(new JsonObject
        {
            ["kind"] = "test-result",
            ["id"] = "managed",
            ["evidenceGrade"] = "measured",
        }),
        ["commands"] = new JsonArray(new JsonObject
        {
            ["id"] = "MANAGED_TESTS",
            ["exitCode"] = 0,
            ["discovered"] = 1,
            ["executed"] = 1,
            ["passed"] = 1,
            ["failed"] = 0,
            ["skipped"] = 0,
            ["disposition"] = "passed",
        }),
        ["blockers"] = new JsonArray(),
        ["nonClaims"] = new JsonArray(),
    };
}
