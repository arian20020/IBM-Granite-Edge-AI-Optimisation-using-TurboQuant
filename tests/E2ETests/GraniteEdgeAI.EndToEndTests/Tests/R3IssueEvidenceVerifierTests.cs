using System.Text.Json;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class R3IssueEvidenceVerifierTests
{
    [TestMethod]
    public void Verify_accepts_exactly_22_executable_green_issue_records()
    {
        using TestDirectory directory = TestDirectory.Create();
        string manifest = WriteManifest(directory);

        IReadOnlyList<R3IssueEvidence> issues = R3IssueEvidenceVerifier.Verify(manifest);

        Assert.AreEqual(22, issues.Count);
        Assert.AreEqual("R3-001", issues[0].IssueId);
        Assert.AreEqual("R3-022", issues[^1].IssueId);
    }

    [TestMethod]
    public void Verify_rejects_missing_or_duplicate_stable_issue_id()
    {
        using TestDirectory directory = TestDirectory.Create();
        string missing = WriteManifest(directory, issues => issues.RemoveAt(19));
        string duplicate = WriteManifest(directory, issues => issues[19] = issues[18]);

        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(missing)).Message,
            "exactly R3-001 through R3-022");
        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(duplicate)).Message,
            "exactly R3-001 through R3-022");
    }

    [TestMethod]
    public void Verify_rejects_remaining_defect_or_non_green_execution()
    {
        using TestDirectory directory = TestDirectory.Create();
        string defect = WriteManifest(directory, issues => issues[0]["productDefectRemains"] = true);
        string skipped = WriteManifest(directory, issues =>
        {
            var command = Commands(issues[1])[0];
            command["passed"] = 0;
            command["skipped"] = 1;
        });
        string zero = WriteManifest(directory, issues =>
        {
            var command = Commands(issues[2])[0];
            command["discovered"] = 0;
            command["executed"] = 0;
            command["passed"] = 0;
        });
        string buildOnly = WriteManifest(directory, issues => Commands(issues[3])[0]["evidenceType"] = "buildOnly");

        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(defect)).Message, "product defect remains");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(skipped)).Message, "executable GREEN");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(zero)).Message, "executable GREEN");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(buildOnly)).Message, "evidence type");
    }

    [TestMethod]
    public void Verify_rejects_production_issue_without_exactly_one_reachable_registration()
    {
        using TestDirectory directory = TestDirectory.Create();
        string missing = WriteManifest(directory, issues => issues[3].Remove("productionReachability"));
        string duplicateRegistration = WriteManifest(directory, issues =>
        {
            var reachability = (Dictionary<string, object?>)issues[4]["productionReachability"]!;
            reachability["observedRegistrations"] = 2;
        });

        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(missing)).Message,
            "production reachability");
        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidDataException>(() => R3IssueEvidenceVerifier.Verify(duplicateRegistration)).Message,
            "exactly one production registration");
    }

    private static List<Dictionary<string, object?>> Commands(Dictionary<string, object?> issue) =>
        (List<Dictionary<string, object?>>)issue["commands"]!;

    private static string WriteManifest(
        TestDirectory directory,
        Action<List<Dictionary<string, object?>>>? mutate = null)
    {
        var issues = Enumerable.Range(1, 22).Select(index =>
        {
            string issueId = $"R3-{index:000}";
            var issue = new Dictionary<string, object?>
            {
                ["issueId"] = issueId,
                ["productDefectRemains"] = false,
                ["evidenceSubjectCommit"] = new string('a', 40),
                ["evidenceSubjectTree"] = new string('b', 40),
                ["commands"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["evidenceType"] = "behavioralTest",
                        ["discovered"] = 1,
                        ["executed"] = 1,
                        ["passed"] = 1,
                        ["failed"] = 0,
                        ["skipped"] = 0,
                    },
                },
            };
            if (index is >= 4 and <= 18)
            {
                issue["productionReachability"] = new Dictionary<string, object?>
                {
                    ["definition"] = "src/Definition.cs",
                    ["productionCaller"] = "src/Caller.cs",
                    ["registrationPoint"] = "src/Composition.cs",
                    ["behavioralRegressionTest"] = "tests/BehaviorTests.cs",
                    ["observedRegistrations"] = 1,
                };
            }
            return issue;
        }).ToList();
        mutate?.Invoke(issues);
        string path = Path.Combine(directory.Path, $"r3-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = 3, issues }));
        return path;
    }
}
