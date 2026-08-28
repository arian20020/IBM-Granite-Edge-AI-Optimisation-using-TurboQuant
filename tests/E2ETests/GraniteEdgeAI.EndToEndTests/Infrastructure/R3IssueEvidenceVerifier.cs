using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record R3ExecutableEvidence(
    string EvidenceType,
    long Discovered,
    long Executed,
    long Passed,
    long Failed,
    long Skipped);

internal sealed record R3ProductionReachability(
    string Definition,
    string ProductionCaller,
    string RegistrationPoint,
    string BehavioralRegressionTest,
    long ObservedRegistrations);

internal sealed record R3IssueEvidence(
    string IssueId,
    bool ProductDefectRemains,
    string EvidenceSubjectCommit,
    string EvidenceSubjectTree,
    IReadOnlyList<R3ExecutableEvidence> Commands,
    R3ProductionReachability? ProductionReachability);

internal static class R3IssueEvidenceVerifier
{
    private static readonly string[] RequiredIssueIds =
        Enumerable.Range(1, 22).Select(index => $"R3-{index:000}").ToArray();

    private static readonly HashSet<string> ExecutableEvidenceTypes =
        new(["behavioralTest", "packageJourney", "nativeJourney"], StringComparer.Ordinal);

    internal static IReadOnlyList<R3IssueEvidence> Verify(string manifestPath)
    {
        using JsonDocument document = JsonContract.Open(manifestPath);
        JsonElement root = document.RootElement;
        JsonContract.RequireOnly(root, "schemaVersion", "issues");
        if (JsonContract.RequiredInt64(root, "schemaVersion") != 3)
        {
            throw new InvalidDataException("R3 issue evidence requires schema version 3.");
        }

        JsonElement issuesElement = RequiredArray(root, "issues");
        List<R3IssueEvidence> issues = issuesElement.EnumerateArray().Select(ParseIssue).ToList();
        string[] observed = issues.Select(issue => issue.IssueId).Order(StringComparer.Ordinal).ToArray();
        if (!observed.SequenceEqual(RequiredIssueIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException("R3 issue evidence must contain exactly R3-001 through R3-022 once each.");
        }

        return issues.OrderBy(issue => issue.IssueId, StringComparer.Ordinal).ToArray();
    }

    private static R3IssueEvidence ParseIssue(JsonElement issue)
    {
        JsonContract.RequireOnly(
            issue,
            "issueId",
            "productDefectRemains",
            "evidenceSubjectCommit",
            "evidenceSubjectTree",
            "commands",
            "productionReachability");
        string issueId = JsonContract.RequiredString(issue, "issueId");
        bool productDefectRemains = RequiredBoolean(issue, "productDefectRemains");
        if (productDefectRemains)
        {
            throw new InvalidDataException($"{issueId} product defect remains.");
        }

        string subjectCommit = JsonContract.RequiredString(issue, "evidenceSubjectCommit");
        string subjectTree = JsonContract.RequiredString(issue, "evidenceSubjectTree");
        JsonContract.RequireGitObject(subjectCommit, "evidenceSubjectCommit");
        JsonContract.RequireGitObject(subjectTree, "evidenceSubjectTree");

        JsonElement commandsElement = RequiredArray(issue, "commands");
        List<R3ExecutableEvidence> commands = commandsElement.EnumerateArray().Select(ParseCommand).ToList();
        if (commands.Count == 0)
        {
            throw new InvalidDataException($"{issueId} has no executable GREEN evidence.");
        }

        R3ProductionReachability? reachability = null;
        if (RequiresProductionReachability(issueId))
        {
            if (!issue.TryGetProperty("productionReachability", out JsonElement reachabilityElement)
                || reachabilityElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"{issueId} production reachability is missing.");
            }
            reachability = ParseReachability(reachabilityElement, issueId);
        }
        else if (issue.TryGetProperty("productionReachability", out JsonElement optionalReachability))
        {
            if (optionalReachability.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"{issueId} production reachability must be an object.");
            }
            reachability = ParseReachability(optionalReachability, issueId);
        }

        return new R3IssueEvidence(issueId, productDefectRemains, subjectCommit, subjectTree, commands, reachability);
    }

    private static R3ExecutableEvidence ParseCommand(JsonElement command)
    {
        JsonContract.RequireOnly(command, "evidenceType", "discovered", "executed", "passed", "failed", "skipped");
        string evidenceType = JsonContract.RequiredString(command, "evidenceType");
        if (!ExecutableEvidenceTypes.Contains(evidenceType))
        {
            throw new InvalidDataException($"Unsupported R3 evidence type '{evidenceType}'.");
        }

        long discovered = JsonContract.RequiredInt64(command, "discovered");
        long executed = JsonContract.RequiredInt64(command, "executed");
        long passed = JsonContract.RequiredInt64(command, "passed");
        long failed = JsonContract.RequiredInt64(command, "failed");
        long skipped = JsonContract.RequiredInt64(command, "skipped");
        if (discovered <= 0 || executed <= 0 || passed <= 0 || failed != 0 || skipped != 0
            || executed != passed + failed + skipped || discovered < executed)
        {
            throw new InvalidDataException("R3 command is not executable GREEN evidence.");
        }

        return new R3ExecutableEvidence(evidenceType, discovered, executed, passed, failed, skipped);
    }

    private static R3ProductionReachability ParseReachability(JsonElement value, string issueId)
    {
        JsonContract.RequireOnly(
            value,
            "definition",
            "productionCaller",
            "registrationPoint",
            "behavioralRegressionTest",
            "observedRegistrations");
        string definition = RequireRepositoryPath(value, "definition");
        string caller = RequireRepositoryPath(value, "productionCaller");
        string registration = RequireRepositoryPath(value, "registrationPoint");
        string test = RequireRepositoryPath(value, "behavioralRegressionTest");
        long observedRegistrations = JsonContract.RequiredInt64(value, "observedRegistrations");
        if (observedRegistrations != 1)
        {
            throw new InvalidDataException($"{issueId} does not have exactly one production registration.");
        }
        return new R3ProductionReachability(definition, caller, registration, test, observedRegistrations);
    }

    private static bool RequiresProductionReachability(string issueId) =>
        int.TryParse(issueId.AsSpan(3), out int number) && number is >= 4 and <= 18;

    private static string RequireRepositoryPath(JsonElement root, string name)
    {
        string value = JsonContract.RequiredString(root, name);
        if (Path.IsPathFullyQualified(value) || value.Contains('\\') || value.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException($"R3 reachability property '{name}' must be a repository-relative path.");
        }
        return value;
    }

    private static JsonElement RequiredArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value
            : throw new InvalidDataException($"Required array property '{name}' is missing.");

    private static bool RequiredBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new InvalidDataException($"Required Boolean property '{name}' is missing.");
}
