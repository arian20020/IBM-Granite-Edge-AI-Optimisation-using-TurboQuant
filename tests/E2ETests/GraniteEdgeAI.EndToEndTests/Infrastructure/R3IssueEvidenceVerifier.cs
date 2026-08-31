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

internal sealed record R3EvidenceBlob(string Path, string Sha256, long Bytes);

internal sealed record R3IssueEvidence(
    string IssueId,
    bool ProductDefectRemains,
    string EvidenceSubjectCommit,
    string EvidenceSubjectTree,
    R3EvidenceBlob EvidenceBlob,
    IReadOnlyList<R3ExecutableEvidence> Commands,
    R3ProductionReachability? ProductionReachability);

internal sealed record R4ClosedFinding(
    string FindingId,
    IReadOnlyList<R3ExecutableEvidence> Commands,
    R3ProductionReachability? ProductionReachability);

internal sealed record R4TwoPhaseIssueEvidence(
    IReadOnlyList<R4ClosedFinding> ClosedPreflightFindings,
    IReadOnlyList<string> PendingPostAcceptanceIds,
    R3EvidenceBlob CatalogBlob,
    string CatalogSubjectCommit,
    string CatalogSubjectTree)
{
    internal bool CanStartAcceptanceMatrix =>
        ClosedPreflightFindings.Count == 28
        && PendingPostAcceptanceIds.SequenceEqual(["R3-020", "R3-022"], StringComparer.Ordinal);
}

internal sealed record R4PostAcceptanceEvidence(string Disposition, bool ClosesR3_020, bool ClosesR3_022);

internal static class R3IssueEvidenceVerifier
{
    private static readonly string[] RequiredIssueIds =
        Enumerable.Range(1, 22).Select(index => $"R3-{index:000}").ToArray();

    private static readonly HashSet<string> ExecutableEvidenceTypes =
        new(["behavioralTest", "packageJourney", "nativeJourney"], StringComparer.Ordinal);

    private static readonly string[] RequiredPreflightR3 =
        RequiredIssueIds.Where(id => id is not "R3-020" and not "R3-022").ToArray();

    private static readonly string[] RequiredR4 =
        Enumerable.Range(1, 8).Select(index => $"R4-C0-{index:000}").ToArray();

    internal static R4TwoPhaseIssueEvidence VerifyTwoPhase(
        string closurePath,
        string catalogPath,
        string repositoryRoot)
    {
        using JsonDocument closureDocument = JsonContract.Open(closurePath);
        using JsonDocument catalogDocument = JsonContract.Open(catalogPath);
        JsonElement closure = closureDocument.RootElement;
        JsonElement catalog = catalogDocument.RootElement;
        JsonContract.RequireOnly(
            closure,
            "schemaVersion", "contract", "coordinator", "createdAtUtc", "productSubject",
            "evidenceCatalog", "issues", "r4Findings", "preflightPolicy",
            "postAcceptancePolicy", "nonClaims");
        if (JsonContract.RequiredInt64(closure, "schemaVersion") != 4
            || JsonContract.RequiredString(closure, "contract") != "C0-R4-ISSUE-CLOSURE-V4"
            || JsonContract.RequiredString(closure, "coordinator") != "C0")
        {
            throw new InvalidDataException("R4 issue evidence requires the exact schema-v4 closure contract.");
        }

        JsonContract.RequireOnly(
            catalog,
            "schemaVersion", "coordinator", "createdAtUtc", "productSubject", "evidenceBase",
            "commands", "issues", "r4Findings", "nonClaims");
        if (JsonContract.RequiredInt64(catalog, "schemaVersion") != 1
            || JsonContract.RequiredString(catalog, "coordinator") != "C0")
        {
            throw new InvalidDataException("R4 issue evidence catalog contract is invalid.");
        }
        RequireMatchingSubject(RequiredObject(closure, "productSubject"), RequiredObject(catalog, "productSubject"));

        JsonElement catalogBinding = RequiredObject(closure, "evidenceCatalog");
        JsonContract.RequireOnly(catalogBinding, "evidenceSubjectCommit", "evidenceSubjectTree", "evidenceBlob");
        string catalogCommit = JsonContract.RequiredString(catalogBinding, "evidenceSubjectCommit");
        string catalogTree = JsonContract.RequiredString(catalogBinding, "evidenceSubjectTree");
        JsonContract.RequireGitObject(catalogCommit, "evidenceSubjectCommit");
        JsonContract.RequireGitObject(catalogTree, "evidenceSubjectTree");
        R3EvidenceBlob catalogBlob = ParseEvidenceBlob(RequiredObject(catalogBinding, "evidenceBlob"));

        Dictionary<string, R3ExecutableEvidence> commands = ParseCatalogCommands(RequiredObject(catalog, "commands"));
        Dictionary<string, JsonElement> catalogIssues = IndexCatalogRows(RequiredArray(catalog, "issues"), "issueId");
        Dictionary<string, JsonElement> catalogR4 = IndexCatalogRows(RequiredArray(catalog, "r4Findings"), "findingId");
        RequireExactIds(catalogIssues.Keys, RequiredIssueIds, "catalog R3 identifiers");
        RequireExactIds(catalogR4.Keys, RequiredR4, "catalog R4 identifiers");

        List<R4ClosedFinding> closed = [];
        List<string> pending = [];
        Dictionary<string, JsonElement> closureIssues = IndexCatalogRows(RequiredArray(closure, "issues"), "issueId");
        Dictionary<string, JsonElement> closureR4 = IndexCatalogRows(RequiredArray(closure, "r4Findings"), "findingId");
        RequireExactIds(closureIssues.Keys, RequiredIssueIds, "closure R3 identifiers");
        RequireExactIds(closureR4.Keys, RequiredR4, "closure R4 identifiers");

        foreach (string id in RequiredIssueIds)
        {
            JsonElement row = closureIssues[id];
            if (id is "R3-020" or "R3-022")
            {
                ParsePending(row, id);
                pending.Add(id);
                continue;
            }
            closed.Add(ParseClosed(row, catalogIssues[id], id, commands, repositoryRoot, RequiresProductionReachability(id)));
        }
        foreach (string id in RequiredR4)
        {
            closed.Add(ParseClosed(closureR4[id], catalogR4[id], id, commands, repositoryRoot, requireReachability: false));
        }

        ParsePolicies(closure, closed.Select(item => item.FindingId), pending);
        return new R4TwoPhaseIssueEvidence(closed, pending, catalogBlob, catalogCommit, catalogTree);
    }

    internal static R4PostAcceptanceEvidence VerifyPostAcceptance(
        string receiptPath,
        string evidenceRoot,
        string expectedBaseCommit,
        string expectedBaseTree,
        string expectedSubjectCommit,
        string expectedSubjectTree)
    {
        using JsonDocument document = JsonContract.Open(receiptPath);
        JsonElement root = document.RootElement;
        JsonContract.RequireOnly(root, "schemaVersion", "workerId", "baseCommit", "baseTree",
            "implementationSubjectCommit", "implementationSubjectTree", "disposition", "report",
            "evidenceManifest", "nativeLockAcquisitions", "cleanupVerified", "packageAttempted",
            "packagePassed", "appControlChecked", "appControlPassed", "nativeExecuted", "nativePassed",
            "nativeFailed", "nativeSkipped", "managedExecuted", "managedPassed", "managedFailed", "managedSkipped",
            "blockedGatesCountedAsPasses", "externalBlock");
        if (JsonContract.RequiredInt64(root, "schemaVersion") != 1
            || JsonContract.RequiredString(root, "workerId") != "E1")
        {
            throw new InvalidDataException("Post-acceptance evidence is not an E1 schema-v1 receipt.");
        }
        RequireExactObject(root, "baseCommit", expectedBaseCommit);
        RequireExactObject(root, "baseTree", expectedBaseTree);
        RequireExactObject(root, "implementationSubjectCommit", expectedSubjectCommit);
        RequireExactObject(root, "implementationSubjectTree", expectedSubjectTree);
        VerifyFileBinding(RequiredObject(root, "report"), evidenceRoot);
        VerifyFileBinding(RequiredObject(root, "evidenceManifest"), evidenceRoot);

        string disposition = JsonContract.RequiredString(root, "disposition");
        if (disposition is not ("APPROVED FOR MAIN INTEGRATION" or "CHANGES REQUIRED" or "BLOCKED BY EXTERNAL ENVIRONMENT"))
        {
            throw new InvalidDataException("Post-acceptance disposition is invalid.");
        }
        long executed = JsonContract.RequiredInt64(root, "managedExecuted");
        long passed = JsonContract.RequiredInt64(root, "managedPassed");
        long failed = JsonContract.RequiredInt64(root, "managedFailed");
        long skipped = JsonContract.RequiredInt64(root, "managedSkipped");
        long nativeExecuted = JsonContract.RequiredInt64(root, "nativeExecuted");
        long nativePassed = JsonContract.RequiredInt64(root, "nativePassed");
        long nativeFailed = JsonContract.RequiredInt64(root, "nativeFailed");
        long nativeSkipped = JsonContract.RequiredInt64(root, "nativeSkipped");
        if (executed <= 0 || passed < 0 || failed < 0 || skipped < 0 || executed != passed + failed + skipped
            || nativeExecuted < 0 || nativePassed < 0 || nativeFailed < 0 || nativeSkipped < 0
            || nativeExecuted != nativePassed + nativeFailed + nativeSkipped
            || RequiredBoolean(root, "blockedGatesCountedAsPasses"))
        {
            throw new InvalidDataException("Post-acceptance execution arithmetic is invalid.");
        }
        bool packageAttempted = RequiredBoolean(root, "packageAttempted");
        bool packagePassed = RequiredBoolean(root, "packagePassed");
        bool appControlChecked = RequiredBoolean(root, "appControlChecked");
        bool appControlPassed = RequiredBoolean(root, "appControlPassed");
        bool cleanupVerified = RequiredBoolean(root, "cleanupVerified");
        long lockAcquisitions = JsonContract.RequiredInt64(root, "nativeLockAcquisitions");
        if (lockAcquisitions < 0 || packagePassed && !packageAttempted || appControlPassed && !appControlChecked)
        {
            throw new InvalidDataException("Post-acceptance gate evidence is inconsistent.");
        }
        string externalBlock = JsonContract.RequiredString(root, "externalBlock");
        bool hasExternalBlock = !string.Equals(externalBlock, "none", StringComparison.Ordinal);
        bool closesR3_020 = nativeExecuted > 0 || hasExternalBlock;
        bool closesR3_022 = packageAttempted && packagePassed
            && appControlChecked && appControlPassed
            && lockAcquisitions > 0
            && nativeExecuted > 0 && nativeFailed == 0 && nativeSkipped == 0
            && failed == 0 && skipped == 0
            && cleanupVerified;
        if (disposition == "BLOCKED BY EXTERNAL ENVIRONMENT" && !hasExternalBlock)
        {
            throw new InvalidDataException("External-block disposition requires precise external evidence.");
        }
        if (disposition == "APPROVED FOR MAIN INTEGRATION"
            && (!closesR3_020 || !closesR3_022 || hasExternalBlock))
        {
            throw new InvalidDataException("Approval requires all package, App Control, native, cleanup and E2E evidence to execute and pass.");
        }
        return new R4PostAcceptanceEvidence(disposition, closesR3_020, closesR3_022);
    }

    private static void RequireExactObject(JsonElement root, string name, string expected)
    {
        JsonContract.RequireGitObject(expected, name);
        if (JsonContract.RequiredString(root, name) != expected)
        {
            throw new InvalidDataException($"Post-acceptance {name} mismatch.");
        }
    }

    private static void VerifyFileBinding(JsonElement binding, string evidenceRoot)
    {
        JsonContract.RequireOnly(binding, "path", "sha256", "bytes");
        string relative = RequireRepositoryPathValue(JsonContract.RequiredString(binding, "path"));
        string root = Path.GetFullPath(evidenceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            throw new InvalidDataException("Post-acceptance file binding is absent or escaping.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        if (JsonContract.RequiredString(binding, "sha256") != digest
            || JsonContract.RequiredInt64(binding, "bytes") != bytes.LongLength)
        {
            throw new InvalidDataException("Post-acceptance file binding differs from disk.");
        }
    }

    private static R4ClosedFinding ParseClosed(
        JsonElement closureRow,
        JsonElement catalogRow,
        string id,
        IReadOnlyDictionary<string, R3ExecutableEvidence> commands,
        string repositoryRoot,
        bool requireReachability)
    {
        if (JsonContract.RequiredString(closureRow, "phase") != "preflight"
            || JsonContract.RequiredString(closureRow, "state") != "CLOSED"
            || RequiredBoolean(closureRow, "productDefectRemains"))
        {
            throw new InvalidDataException($"{id} is not a closed preflight finding.");
        }
        string[] commandIds = ReadUniqueStrings(RequiredArray(closureRow, "commandIds"), $"{id} commandIds");
        if (commandIds.Length == 0)
        {
            throw new InvalidDataException($"{id} has no executable GREEN command.");
        }
        string[] catalogCommandIds = catalogRow.TryGetProperty("commandIds", out JsonElement catalogCommands)
            && catalogCommands.ValueKind == JsonValueKind.Array
            ? ReadUniqueStrings(catalogCommands, $"{id} catalog commandIds")
            : [];
        if (!commandIds.Order(StringComparer.Ordinal).SequenceEqual(catalogCommandIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{id} command binding differs from the evidence catalog.");
        }
        R3ExecutableEvidence[] resolved = commandIds.Select(commandId =>
            commands.TryGetValue(commandId, out R3ExecutableEvidence? command)
                ? command
                : throw new InvalidDataException($"{id} references missing command {commandId}.")).ToArray();
        R3ProductionReachability? reachability = ValidateCatalogPaths(catalogRow, repositoryRoot, id, requireReachability);
        return new R4ClosedFinding(id, resolved, reachability);
    }

    private static void ParsePending(JsonElement row, string id)
    {
        JsonContract.RequireOnly(
            row,
            "issueId", "phase", "state", "productDefectDisposition", "acceptanceOwner", "requiredEvidence");
        if (JsonContract.RequiredString(row, "phase") != "postAcceptance"
            || JsonContract.RequiredString(row, "state") != "PENDING_E1_ACCEPTANCE"
            || JsonContract.RequiredString(row, "productDefectDisposition") != "UNDETERMINED_UNTIL_E1"
            || JsonContract.RequiredString(row, "acceptanceOwner") != "E1"
            || ReadUniqueStrings(RequiredArray(row, "requiredEvidence"), $"{id} requiredEvidence").Length == 0)
        {
            throw new InvalidDataException($"{id} is not an honest E1 post-acceptance finding.");
        }
    }

    private static Dictionary<string, R3ExecutableEvidence> ParseCatalogCommands(JsonElement value)
    {
        Dictionary<string, R3ExecutableEvidence> result = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!result.TryAdd(property.Name, ParseCommand(property.Value)))
            {
                throw new InvalidDataException("Duplicate catalog command identifier.");
            }
        }
        return result;
    }

    private static Dictionary<string, JsonElement> IndexCatalogRows(JsonElement rows, string idProperty)
    {
        Dictionary<string, JsonElement> result = new(StringComparer.Ordinal);
        foreach (JsonElement row in rows.EnumerateArray())
        {
            string id = JsonContract.RequiredString(row, idProperty);
            if (!result.TryAdd(id, row))
            {
                throw new InvalidDataException($"Duplicate {idProperty} '{id}'.");
            }
        }
        return result;
    }

    private static R3ProductionReachability? ValidateCatalogPaths(
        JsonElement row,
        string repositoryRoot,
        string id,
        bool requireReachability)
    {
        R3ProductionReachability? reachability = null;
        if (row.TryGetProperty("productionReachability", out JsonElement value))
        {
            reachability = ParseReachability(value, id);
            foreach (string path in new[] { reachability.Definition, reachability.ProductionCaller, reachability.RegistrationPoint, reachability.BehavioralRegressionTest })
            {
                RequireExistingRepositoryPath(repositoryRoot, path);
            }
        }
        else if (requireReachability)
        {
            throw new InvalidDataException($"{id} production reachability is missing.");
        }
        if (row.TryGetProperty("sourceArtifacts", out JsonElement artifacts))
        {
            foreach (string path in ReadUniqueStrings(artifacts, $"{id} sourceArtifacts"))
            {
                RequireExistingRepositoryPath(repositoryRoot, path);
            }
        }
        return reachability;
    }

    private static void ParsePolicies(JsonElement closure, IEnumerable<string> closedIds, IReadOnlyList<string> pendingIds)
    {
        JsonElement policy = RequiredObject(closure, "preflightPolicy");
        string[] declaredClosedR3 = ReadUniqueStrings(RequiredArray(policy, "requiredClosedR3"), "requiredClosedR3");
        string[] declaredPending = ReadUniqueStrings(RequiredArray(policy, "requiredPendingE1Acceptance"), "requiredPendingE1Acceptance");
        string[] declaredClosedR4 = ReadUniqueStrings(RequiredArray(policy, "requiredClosedR4"), "requiredClosedR4");
        RequireExactIds(declaredClosedR3, RequiredPreflightR3, "preflight policy R3 identifiers");
        RequireExactIds(declaredPending, pendingIds, "preflight policy pending identifiers");
        RequireExactIds(declaredClosedR4, RequiredR4, "preflight policy R4 identifiers");
        JsonElement post = RequiredObject(closure, "postAcceptancePolicy");
        if (JsonContract.RequiredString(post, "owner") != "E1")
        {
            throw new InvalidDataException("Post-acceptance ownership is not E1.");
        }
        _ = closedIds.Count();
    }

    private static void RequireMatchingSubject(JsonElement left, JsonElement right)
    {
        string leftCommit = JsonContract.RequiredString(left, "commit");
        string leftTree = JsonContract.RequiredString(left, "tree");
        JsonContract.RequireGitObject(leftCommit, "productSubject.commit");
        JsonContract.RequireGitObject(leftTree, "productSubject.tree");
        if (leftCommit != JsonContract.RequiredString(right, "commit")
            || leftTree != JsonContract.RequiredString(right, "tree"))
        {
            throw new InvalidDataException("Closure and catalog product subjects differ.");
        }
    }

    private static void RequireExactIds(IEnumerable<string> observedIds, IEnumerable<string> requiredIds, string description)
    {
        string[] observed = observedIds.Order(StringComparer.Ordinal).ToArray();
        string[] required = requiredIds.Order(StringComparer.Ordinal).ToArray();
        if (!observed.SequenceEqual(required, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Invalid {description}.");
        }
    }

    private static string[] ReadUniqueStrings(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{description} must be an array.");
        }
        string[] result = value.EnumerateArray().Select(item =>
            item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())
                ? item.GetString()!
                : throw new InvalidDataException($"{description} contains an invalid value.")).ToArray();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidDataException($"{description} contains duplicates.");
        }
        return result;
    }

    private static void RequireExistingRepositoryPath(string repositoryRoot, string relativePath)
    {
        string safe = RequireRepositoryPathValue(relativePath);
        string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, safe.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || (!File.Exists(full) && !Directory.Exists(full)))
        {
            throw new InvalidDataException("Declared repository evidence path is absent or escaping.");
        }
    }

    private static string RequireRepositoryPathValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathFullyQualified(value) || value.Contains('\\')
            || value.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("Evidence path must be repository-relative.");
        }
        return value;
    }

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
            "evidenceBlob",
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
        R3EvidenceBlob evidenceBlob = ParseEvidenceBlob(RequiredObject(issue, "evidenceBlob"));

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

        return new R3IssueEvidence(issueId, productDefectRemains, subjectCommit, subjectTree, evidenceBlob, commands, reachability);
    }

    private static R3EvidenceBlob ParseEvidenceBlob(JsonElement value)
    {
        JsonContract.RequireOnly(value, "path", "sha256", "bytes");
        string path = RequireRepositoryPath(value, "path");
        string sha256 = JsonContract.RequiredString(value, "sha256");
        JsonContract.RequireSha256(sha256, "sha256");
        long bytes = JsonContract.RequiredInt64(value, "bytes");
        if (bytes <= 0 || bytes > 1024 * 1024)
        {
            throw new InvalidDataException("R3 evidence blob violates its closed byte bound.");
        }
        return new R3EvidenceBlob(path, sha256, bytes);
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

    private static JsonElement RequiredObject(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidDataException($"Required object property '{name}' is missing.");

    private static bool RequiredBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new InvalidDataException($"Required Boolean property '{name}' is missing.");
}
