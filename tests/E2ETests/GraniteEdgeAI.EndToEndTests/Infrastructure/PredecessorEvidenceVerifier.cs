using System.Security.Cryptography;
using System.Text.Json;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record PredecessorEvidence(
    string WorkerId,
    string FinalTip,
    string FinalTree,
    string EvidenceSubjectCommit,
    string EvidenceSubjectTree);

internal static class PredecessorEvidenceVerifier
{
    private static readonly IReadOnlyDictionary<string, string[]> RequiredKinds =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["H1"] = ["hardwareSnapshot", "availableMemory", "safetyBudget"],
            ["M1"] = ["modelSource", "modelInspectionResult", "modelInspectionHandoff"],
            ["Q1"] = ["optimizationPlan", "executionResult", "chatTarget", "exportTarget"],
        };

    internal static PredecessorEvidence Verify(
        string workerId,
        string repositoryRoot,
        string handoffPath,
        string nativeReceiptPath,
        string? evidenceManifestPath)
    {
        if (workerId is not ("H1" or "M1" or "Q1" or "F1"))
        {
            throw new ArgumentOutOfRangeException(nameof(workerId));
        }

        string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using JsonDocument handoffDocument = JsonContract.Open(handoffPath);
        JsonElement handoff = handoffDocument.RootElement;
        RequireWorkerAndFrozen(handoff, workerId, "handoff");
        if (JsonContract.RequiredInt64(handoff, "schemaVersion") != 1 || !RequiredBoolean(handoff, "worktreeClean"))
        {
            throw new InvalidDataException($"{workerId} handoff is not final and clean.");
        }

        string finalTip = RequiredGit(handoff, "finalTip");
        string finalTree = RequiredGit(handoff, "finalTree");
        string branch = JsonContract.RequiredString(handoff, "branch");
        JsonElement report = RequiredObject(handoff, "report");
        string reportPath = ResolveRelative(root, JsonContract.RequiredString(report, "path"));
        string reportSha = RequiredSha(report, "sha256");
        long reportBytes = JsonContract.RequiredInt64(report, "bytes");
        VerifyFile(reportPath, reportSha, reportBytes, $"{workerId} report");
        VerifyArithmetic(RequiredObject(handoff, "testTotals"), $"{workerId} handoff totals");

        using JsonDocument nativeDocument = JsonContract.Open(nativeReceiptPath);
        JsonElement native = nativeDocument.RootElement;
        RequireWorkerAndFrozen(native, workerId, "native receipt");
        if (!RequiredBoolean(native, "phaseClosed") || !RequiredBoolean(native, "processCleanupVerified"))
        {
            throw new InvalidDataException($"{workerId} native phase is not closed and cleanup-verified.");
        }

        RequireEqual(finalTip, RequiredGit(native, "finalTip"), $"{workerId} final tip");
        RequireEqual(finalTree, RequiredGit(native, "finalTree"), $"{workerId} final tree");
        RequireEqual(branch, JsonContract.RequiredString(native, "branch"), $"{workerId} branch");
        RequireEqual(reportSha, RequiredSha(native, "reportSha256"), $"{workerId} report SHA-256");
        RequireEqual(Sha256(handoffPath), RequiredSha(native, "handoffReceiptSha256"), $"{workerId} handoff receipt SHA-256");
        string subjectCommit = RequiredGit(native, "evidenceSubjectCommit");
        string subjectTree = RequiredGit(native, "evidenceSubjectTree");

        if (RequiredKinds.TryGetValue(workerId, out string[]? kinds))
        {
            if (string.IsNullOrWhiteSpace(evidenceManifestPath))
            {
                throw new InvalidDataException($"{workerId} requires an evidence manifest.");
            }

            VerifyEvidenceManifest(
                workerId,
                evidenceManifestPath,
                handoff,
                native,
                subjectCommit,
                subjectTree,
                reportSha,
                reportBytes,
                kinds);
        }

        return new PredecessorEvidence(workerId, finalTip, finalTree, subjectCommit, subjectTree);
    }

    private static void VerifyEvidenceManifest(
        string workerId,
        string path,
        JsonElement handoff,
        JsonElement native,
        string subjectCommit,
        string subjectTree,
        string reportSha,
        long reportBytes,
        string[] requiredKinds)
    {
        JsonElement handoffManifest = RequiredObject(handoff, "evidenceManifest");
        string manifestSha = RequiredSha(handoffManifest, "sha256");
        long manifestBytes = JsonContract.RequiredInt64(handoffManifest, "bytes");
        VerifyFile(path, manifestSha, manifestBytes, $"{workerId} evidence manifest");
        RequireEqual(subjectCommit, RequiredGit(handoffManifest, "evidenceSubjectCommit"), $"{workerId} evidence subject commit");
        RequireEqual(subjectTree, RequiredGit(handoffManifest, "evidenceSubjectTree"), $"{workerId} evidence subject tree");
        RequireEqual(manifestSha, RequiredSha(native, "evidenceManifestSha256"), $"{workerId} evidence manifest SHA-256");

        using JsonDocument evidenceDocument = JsonContract.Open(path);
        JsonElement evidence = evidenceDocument.RootElement;
        RequireWorkerAndFrozen(evidence, workerId, "evidence manifest", requireTree: false);
        RequireEqual(subjectCommit, RequiredGit(evidence, "evidenceSubjectCommit"), $"{workerId} evidence subject commit");
        RequireEqual(subjectTree, RequiredGit(evidence, "evidenceSubjectTree"), $"{workerId} evidence subject tree");
        JsonElement evidenceReport = RequiredObject(evidence, "report");
        RequireEqual(reportSha, RequiredSha(evidenceReport, "sha256"), $"{workerId} evidence report SHA-256");
        if (JsonContract.RequiredInt64(evidenceReport, "bytes") != reportBytes)
        {
            throw new InvalidDataException($"{workerId} evidence report byte count does not match its handoff.");
        }

        HashSet<string> observedKinds = new(StringComparer.Ordinal);
        AddKinds(evidence, "inputs", observedKinds);
        AddKinds(evidence, "outputs", observedKinds);
        foreach (string kind in requiredKinds)
        {
            if (!observedKinds.Contains(kind))
            {
                throw new InvalidDataException($"{workerId} evidence is missing required kind '{kind}'.");
            }
        }

        JsonElement commands = RequiredArray(evidence, "commands");
        if (commands.GetArrayLength() == 0)
        {
            throw new InvalidDataException($"{workerId} evidence command ledger is empty.");
        }
        foreach (JsonElement command in commands.EnumerateArray())
        {
            VerifyArithmetic(command, $"{workerId} evidence command");
        }
    }

    private static void AddKinds(JsonElement root, string name, HashSet<string> kinds)
    {
        JsonElement items = RequiredArray(root, name);
        if (items.GetArrayLength() == 0)
        {
            throw new InvalidDataException($"Evidence property '{name}' must be non-empty.");
        }
        foreach (JsonElement item in items.EnumerateArray())
        {
            kinds.Add(JsonContract.RequiredString(item, "kind"));
        }
    }

    private static void VerifyArithmetic(JsonElement value, string description)
    {
        long discovered = JsonContract.RequiredInt64(value, "discovered");
        long executed = JsonContract.RequiredInt64(value, "executed");
        long passed = JsonContract.RequiredInt64(value, "passed");
        long failed = JsonContract.RequiredInt64(value, "failed");
        long skipped = JsonContract.RequiredInt64(value, "skipped");
        if (discovered < 0 || executed < 0 || passed < 0 || failed < 0 || skipped < 0
            || executed != passed + failed + skipped || discovered < executed)
        {
            throw new InvalidDataException($"{description} arithmetic is inconsistent.");
        }
    }

    private static void RequireWorkerAndFrozen(JsonElement root, string workerId, string description, bool requireTree = true)
    {
        RequireEqual(workerId, JsonContract.RequiredString(root, "workerId"), $"{description} worker");
        RequireEqual(AuditIdentity.FrozenCommit, JsonContract.RequiredString(root, "frozenSourceCommit"), $"{description} frozen commit");
        if (requireTree)
        {
            RequireEqual(AuditIdentity.FrozenTree, JsonContract.RequiredString(root, "frozenSourceTree"), $"{description} frozen tree");
        }
    }

    private static JsonElement RequiredObject(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidDataException($"Required object property '{name}' is missing.");

    private static JsonElement RequiredArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value
            : throw new InvalidDataException($"Required array property '{name}' is missing.");

    private static bool RequiredBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new InvalidDataException($"Required Boolean property '{name}' is missing.");

    private static string RequiredGit(JsonElement root, string name)
    {
        string value = JsonContract.RequiredString(root, name);
        JsonContract.RequireGitObject(value, name);
        return value;
    }

    private static string RequiredSha(JsonElement root, string name)
    {
        string value = JsonContract.RequiredString(root, name);
        JsonContract.RequireSha256(value, name);
        return value;
    }

    private static string ResolveRelative(string root, string relative)
    {
        if (Path.IsPathFullyQualified(relative) || relative.Contains('\\'))
        {
            throw new InvalidDataException("Committed evidence paths must be relative and use forward slashes.");
        }
        string resolved = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Committed evidence path escapes the repository root.");
        }
        return resolved;
    }

    private static void VerifyFile(string path, string expectedSha, long expectedBytes, string description)
    {
        FileInfo file = new(path);
        if (!file.Exists || expectedBytes <= 0 || file.Length != expectedBytes || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"{description} byte count or regular-file requirement failed.");
        }
        RequireEqual(expectedSha, Sha256(path), $"{description} SHA-256");
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void RequireEqual(string expected, string actual, string description)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{description} does not match.");
        }
    }
}
