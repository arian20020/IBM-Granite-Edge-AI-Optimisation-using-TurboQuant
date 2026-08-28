using System.Text.Json;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record ProducerEvidence(string WorkerId, string SubjectCommit, string SubjectTree, IReadOnlySet<string> Kinds);

internal sealed partial record ProducerEvidenceSet(ProducerEvidence Hardware, ProducerEvidence Model, ProducerEvidence Optimization)
{
    internal static ProducerEvidenceSet Load(string h1Path, string m1Path, string q1Path) => new(
        LoadOne(h1Path, "H1", ["hardwareSnapshot", "availableMemory", "safetyBudget"]),
        LoadOne(m1Path, "M1", ["modelSource", "modelInspectionResult", "modelInspectionHandoff"]),
        LoadOne(q1Path, "Q1", ["optimizationPlan", "executionResult", "chatTarget", "exportTarget"]));

    private static ProducerEvidence LoadOne(string path, string workerId, string[] requiredKinds)
    {
        using JsonDocument document = JsonContract.Open(path);
        JsonElement root = document.RootElement;
        if (JsonContract.RequiredString(root, "workerId") != workerId || JsonContract.RequiredString(root, "frozenSourceCommit") != AuditIdentity.FrozenCommit)
        {
            throw new InvalidDataException($"{workerId} evidence is not bound to the expected worker/frozen source.");
        }

        string subjectCommit = JsonContract.RequiredString(root, "evidenceSubjectCommit");
        string subjectTree = JsonContract.RequiredString(root, "evidenceSubjectTree");
        if (!GitObject().IsMatch(subjectCommit) || !GitObject().IsMatch(subjectTree))
        {
            throw new InvalidDataException($"{workerId} evidence subject identity is malformed.");
        }

        HashSet<string> kinds = new(StringComparer.Ordinal);
        AddKinds(root, "inputs", kinds);
        AddKinds(root, "outputs", kinds);
        foreach (string kind in requiredKinds)
        {
            if (!kinds.Contains(kind))
            {
                throw new InvalidDataException($"{workerId} evidence is missing required kind '{kind}'.");
            }
        }

        if (!root.TryGetProperty("commands", out JsonElement commands) || commands.ValueKind != JsonValueKind.Array || commands.GetArrayLength() == 0)
        {
            throw new InvalidDataException($"{workerId} evidence has no command ledger.");
        }

        foreach (JsonElement command in commands.EnumerateArray())
        {
            long discovered = JsonContract.RequiredInt64(command, "discovered");
            long executed = JsonContract.RequiredInt64(command, "executed");
            long passed = JsonContract.RequiredInt64(command, "passed");
            long failed = JsonContract.RequiredInt64(command, "failed");
            long skipped = JsonContract.RequiredInt64(command, "skipped");
            if (executed != passed + failed + skipped || discovered < executed)
            {
                throw new InvalidDataException($"{workerId} command arithmetic is inconsistent.");
            }
        }

        return new ProducerEvidence(workerId, subjectCommit, subjectTree, kinds);
    }

    private static void AddKinds(JsonElement root, string property, HashSet<string> kinds)
    {
        if (!root.TryGetProperty(property, out JsonElement items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
        {
            throw new InvalidDataException($"Evidence property '{property}' must be a non-empty array.");
        }

        foreach (JsonElement item in items.EnumerateArray())
        {
            kinds.Add(JsonContract.RequiredString(item, "kind"));
        }
    }

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex GitObject();
}
