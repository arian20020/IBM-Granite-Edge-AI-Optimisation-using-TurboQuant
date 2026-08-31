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
            "evidenceManifest", "nativeLockAcquisitions", "managedExecuted", "managedPassed",
            "managedFailed", "managedSkipped", "blockedGatesCountedAsPasses", "gateEvidence", "externalBlock");
        if (JsonContract.RequiredInt64(root, "schemaVersion") != 2
            || JsonContract.RequiredString(root, "workerId") != "E1")
        {
            throw new InvalidDataException("Post-acceptance evidence is not an E1 schema-v2 receipt.");
        }
        RequireExactObject(root, "baseCommit", expectedBaseCommit);
        RequireExactObject(root, "baseTree", expectedBaseTree);
        RequireExactObject(root, "implementationSubjectCommit", expectedSubjectCommit);
        RequireExactObject(root, "implementationSubjectTree", expectedSubjectTree);
        string reportPath = VerifyFileBinding(RequiredObject(root, "report"), evidenceRoot);
        string manifestPath = VerifyFileBinding(RequiredObject(root, "evidenceManifest"), evidenceRoot);

        string disposition = JsonContract.RequiredString(root, "disposition");
        if (disposition is not ("APPROVED FOR MAIN INTEGRATION" or "CHANGES REQUIRED" or "BLOCKED BY EXTERNAL ENVIRONMENT"))
        {
            throw new InvalidDataException("Post-acceptance disposition is invalid.");
        }
        long executed = JsonContract.RequiredInt64(root, "managedExecuted");
        long passed = JsonContract.RequiredInt64(root, "managedPassed");
        long failed = JsonContract.RequiredInt64(root, "managedFailed");
        long skipped = JsonContract.RequiredInt64(root, "managedSkipped");
        if (executed <= 0 || passed < 0 || failed < 0 || skipped < 0 || executed != passed + failed + skipped
            || RequiredBoolean(root, "blockedGatesCountedAsPasses"))
        {
            throw new InvalidDataException("Post-acceptance execution arithmetic is invalid.");
        }
        long lockAcquisitions = JsonContract.RequiredInt64(root, "nativeLockAcquisitions");
        if (lockAcquisitions < 0)
        {
            throw new InvalidDataException("Post-acceptance gate evidence is inconsistent.");
        }

        using JsonDocument manifestDocument = JsonContract.Open(manifestPath);
        ManifestEvidence manifest = ParseManifest(
            manifestDocument.RootElement, evidenceRoot, reportPath, expectedSubjectCommit, expectedSubjectTree);
        JsonElement gates = RequiredObject(root, "gateEvidence");
        JsonContract.RequireOnly(gates, "package", "appControl", "native", "e2e", "cleanup");
        GateEvidence package = ParseGate(gates, "package", manifest.Commands);
        GateEvidence appControl = ParseGate(gates, "appControl", manifest.Commands);
        GateEvidence native = ParseGate(gates, "native", manifest.Commands);
        GateEvidence e2e = ParseGate(gates, "e2e", manifest.Commands);
        if (new[] { package.CommandId, appControl.CommandId, native.CommandId, e2e.CommandId }.Distinct(StringComparer.Ordinal).Count() != 4)
        {
            throw new InvalidDataException("Post-acceptance gates require discrete command evidence.");
        }
        JsonElement cleanup = RequiredObject(gates, "cleanup");
        JsonContract.RequireOnly(cleanup, "executed", "passed");
        bool cleanupExecuted = RequiredBoolean(cleanup, "executed");
        bool cleanupPassed = RequiredBoolean(cleanup, "passed");
        if (cleanupPassed && !cleanupExecuted)
        {
            throw new InvalidDataException("Cleanup cannot pass without execution.");
        }
        long gateExecuted = package.Executed + appControl.Executed + native.Executed + e2e.Executed;
        long gatePassed = package.Passed + appControl.Passed + native.Passed + e2e.Passed;
        long gateFailed = package.Failed + appControl.Failed + native.Failed + e2e.Failed;
        long gateSkipped = package.Skipped + appControl.Skipped + native.Skipped + e2e.Skipped;
        if (executed != gateExecuted || passed != gatePassed || failed != gateFailed || skipped != gateSkipped)
        {
            throw new InvalidDataException("Managed counters must exactly reconcile the discrete gate evidence.");
        }

        bool hasExternalBlock = ParseExternalBlock(
            root, disposition, manifest, expectedBaseCommit, expectedBaseTree, expectedSubjectCommit, expectedSubjectTree);
        bool closesR3_020 = native.Executed > 0 || hasExternalBlock;
        bool packagePass = package.IsPassing;
        bool appControlPass = appControl.IsPassing;
        bool nativePass = native.IsPassing;
        bool e2ePass = e2e.IsPassing;
        bool closesR3_022 = !hasExternalBlock
            && packagePass && appControlPass
            && lockAcquisitions > 0
            && nativePass && e2ePass
            && cleanupExecuted && cleanupPassed;
        if (disposition == "APPROVED FOR MAIN INTEGRATION"
            && (!closesR3_020 || !closesR3_022 || hasExternalBlock || manifest.Status != "passed"))
        {
            throw new InvalidDataException("Approval requires all package, App Control, native, cleanup and E2E evidence to execute and pass.");
        }
        return new R4PostAcceptanceEvidence(disposition, closesR3_020, closesR3_022);
    }

    private static ManifestEvidence ParseManifest(
        JsonElement root,
        string evidenceRoot,
        string expectedReportPath,
        string expectedSubjectCommit,
        string expectedSubjectTree)
    {
        JsonContract.RequireOnly(root, "schemaVersion", "workerId", "frozenSourceCommit", "evidenceSubjectCommit",
            "evidenceSubjectTree", "createdAtUtc", "route", "evidenceStatus", "report", "inputs", "outputs",
            "commands", "blockers", "nonClaims");
        if (JsonContract.RequiredInt64(root, "schemaVersion") != 2 || JsonContract.RequiredString(root, "workerId") != "E1")
        {
            throw new InvalidDataException("Bound evidence manifest is not E1 schema v2.");
        }
        RequireExactObject(root, "evidenceSubjectCommit", expectedSubjectCommit);
        RequireExactObject(root, "evidenceSubjectTree", expectedSubjectTree);
        string manifestReportPath = VerifyFileBinding(RequiredObject(root, "report"), evidenceRoot);
        if (!manifestReportPath.Equals(expectedReportPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Receipt and manifest do not bind the same report.");
        }
        string status = JsonContract.RequiredString(root, "evidenceStatus");
        if (status is not ("passed" or "failed" or "blocked" or "mixed"))
        {
            throw new InvalidDataException("Evidence manifest status is invalid.");
        }
        Dictionary<string, GateEvidence> commands = [];
        foreach (JsonElement command in RequiredArray(root, "commands").EnumerateArray())
        {
            JsonContract.RequireOnly(command, "id", "exitCode", "discovered", "executed", "passed", "failed", "skipped", "disposition", "resultSha256");
            string id = JsonContract.RequiredString(command, "id");
            long discovered = JsonContract.RequiredInt64(command, "discovered");
            long commandExecuted = JsonContract.RequiredInt64(command, "executed");
            long commandPassed = JsonContract.RequiredInt64(command, "passed");
            long commandFailed = JsonContract.RequiredInt64(command, "failed");
            long commandSkipped = JsonContract.RequiredInt64(command, "skipped");
            string commandDisposition = JsonContract.RequiredString(command, "disposition");
            long exitCode = JsonContract.RequiredInt64(command, "exitCode");
            if (discovered < 0 || commandExecuted < 0 || commandPassed < 0 || commandFailed < 0 || commandSkipped < 0
                || discovered != commandExecuted || commandExecuted != commandPassed + commandFailed + commandSkipped
                || !commands.TryAdd(id, new GateEvidence(id, commandExecuted, commandPassed, commandFailed, commandSkipped, exitCode, commandDisposition)))
            {
                throw new InvalidDataException("Evidence manifest command arithmetic or identity is invalid.");
            }
        }
        Dictionary<string, BoundEvidenceArtifact> outputs = [];
        foreach (JsonElement item in RequiredArray(root, "outputs").EnumerateArray())
        {
            JsonContract.RequireOnly(item, "kind", "id", "sha256", "bytes", "evidenceGrade");
            string kind = JsonContract.RequiredString(item, "kind");
            string id = RequireRepositoryPathValue(JsonContract.RequiredString(item, "id"));
            if (JsonContract.RequiredString(item, "evidenceGrade") != "verified")
            {
                throw new InvalidDataException("Bound output is not verified evidence.");
            }
            string path = ResolveOutputPath(evidenceRoot, id);
            VerifyBoundFile(path, item);
            if (!outputs.TryAdd(id, new BoundEvidenceArtifact(kind, id, path)))
            {
                throw new InvalidDataException("Evidence manifest output identity is duplicated.");
            }
        }
        BoundEvidenceArtifact[] reviews = outputs.Values.Where(item => item.Kind == "independent-final-review").ToArray();
        if (reviews.Length != 1)
        {
            throw new InvalidDataException("Evidence manifest requires one durable independent review.");
        }
        VerifyIndependentReview(reviews[0].Path, expectedSubjectCommit, expectedSubjectTree);
        return new ManifestEvidence(status, commands, outputs);
    }

    private static GateEvidence ParseGate(JsonElement gates, string name, IReadOnlyDictionary<string, GateEvidence> commands)
    {
        JsonElement gate = RequiredObject(gates, name);
        JsonContract.RequireOnly(gate, "commandId", "executed", "passed", "failed", "skipped");
        string id = JsonContract.RequiredString(gate, "commandId");
        if (!commands.TryGetValue(id, out GateEvidence? command))
        {
            throw new InvalidDataException($"Gate '{name}' references missing manifest command '{id}'.");
        }
        long executed = JsonContract.RequiredInt64(gate, "executed");
        long passed = JsonContract.RequiredInt64(gate, "passed");
        long failed = JsonContract.RequiredInt64(gate, "failed");
        long skipped = JsonContract.RequiredInt64(gate, "skipped");
        if (executed < 0 || passed < 0 || failed < 0 || skipped < 0 || executed != passed + failed + skipped
            || executed != command.Executed || passed != command.Passed || failed != command.Failed || skipped != command.Skipped)
        {
            throw new InvalidDataException($"Gate '{name}' differs from its candidate-bound manifest command.");
        }
        return command;
    }

    private static bool ParseExternalBlock(
        JsonElement root,
        string disposition,
        ManifestEvidence manifest,
        string expectedBaseCommit,
        string expectedBaseTree,
        string expectedSubjectCommit,
        string expectedSubjectTree)
    {
        JsonElement block = root.GetProperty("externalBlock");
        if (block.ValueKind == JsonValueKind.Null)
        {
            if (disposition == "BLOCKED BY EXTERNAL ENVIRONMENT")
            {
                throw new InvalidDataException("External-block disposition requires structured external evidence.");
            }
            return false;
        }
        if (block.ValueKind != JsonValueKind.Object || disposition != "BLOCKED BY EXTERNAL ENVIRONMENT")
        {
            throw new InvalidDataException("Structured external evidence requires the external-block disposition.");
        }
        string kind = JsonContract.RequiredString(block, "kind");
        string commandId = JsonContract.RequiredString(block, "commandId");
        string observationId = JsonContract.RequiredString(block, "observationId");
        if (!manifest.Commands.TryGetValue(commandId, out GateEvidence? command)
            || command.Executed <= 0 || command.Failed <= 0 || command.Disposition is not ("blocked" or "failed")
            || !manifest.Outputs.TryGetValue(observationId, out BoundEvidenceArtifact? artifact)
            || artifact.Kind != "external-block-observation")
        {
            throw new InvalidDataException("External block is not bound to failed command and observation evidence.");
        }
        using JsonDocument observationDocument = JsonContract.Open(artifact.Path);
        JsonElement observation = observationDocument.RootElement;
        if (kind == "missingPrerequisite")
        {
            JsonContract.RequireOnly(block, "kind", "prerequisite", "commandId", "observationId");
            string prerequisite = JsonContract.RequiredString(block, "prerequisite");
            HashSet<string> allowed = new(StringComparer.Ordinal)
            {
                "candidateManifest", "assetManifest", "h1NativeManifest", "m1NativeManifest",
                "q1NativeManifest", "openVinoStageManifest", "ggufStageManifest", "predecessorNativeReceipt",
            };
            JsonContract.RequireOnly(observation, "schemaVersion", "observer", "candidateCommit", "candidateTree",
                "implementationSubjectCommit", "implementationSubjectTree", "kind", "prerequisite",
                "observedAbsent", "observedAtUtc");
            if (!allowed.Contains(prerequisite)
                || JsonContract.RequiredInt64(observation, "schemaVersion") != 1
                || JsonContract.RequiredString(observation, "observer") != "E1"
                || JsonContract.RequiredString(observation, "candidateCommit") != expectedBaseCommit
                || JsonContract.RequiredString(observation, "candidateTree") != expectedBaseTree
                || JsonContract.RequiredString(observation, "implementationSubjectCommit") != expectedSubjectCommit
                || JsonContract.RequiredString(observation, "implementationSubjectTree") != expectedSubjectTree
                || JsonContract.RequiredString(observation, "kind") != kind
                || JsonContract.RequiredString(observation, "prerequisite") != prerequisite
                || !RequiredBoolean(observation, "observedAbsent")
                || !IsCanonicalUtc(JsonContract.RequiredString(observation, "observedAtUtc")))
            {
                throw new InvalidDataException("Missing-prerequisite block does not match candidate-bound observation evidence.");
            }
            return true;
        }
        if (kind == "appControlFailure")
        {
            JsonContract.RequireOnly(block, "kind", "commandId", "errorCode", "observationId");
            string errorCode = JsonContract.RequiredString(block, "errorCode");
            JsonContract.RequireOnly(observation, "schemaVersion", "observer", "candidateCommit", "candidateTree",
                "implementationSubjectCommit", "implementationSubjectTree", "kind", "commandId", "errorCode",
                "observedFailure", "observedAtUtc");
            if (errorCode != "0x800711C7"
                || JsonContract.RequiredInt64(observation, "schemaVersion") != 1
                || JsonContract.RequiredString(observation, "observer") != "E1"
                || JsonContract.RequiredString(observation, "candidateCommit") != expectedBaseCommit
                || JsonContract.RequiredString(observation, "candidateTree") != expectedBaseTree
                || JsonContract.RequiredString(observation, "implementationSubjectCommit") != expectedSubjectCommit
                || JsonContract.RequiredString(observation, "implementationSubjectTree") != expectedSubjectTree
                || JsonContract.RequiredString(observation, "kind") != kind
                || JsonContract.RequiredString(observation, "commandId") != commandId
                || JsonContract.RequiredString(observation, "errorCode") != errorCode
                || !RequiredBoolean(observation, "observedFailure")
                || !IsCanonicalUtc(JsonContract.RequiredString(observation, "observedAtUtc")))
            {
                throw new InvalidDataException("App Control block is not tied to an observed failed manifest command.");
            }
            return true;
        }
        throw new InvalidDataException("External block kind is not allowed.");
    }

    private static void VerifyIndependentReview(
        string reviewPath,
        string expectedSubjectCommit,
        string expectedSubjectTree)
    {
        using JsonDocument reviewDocument = JsonContract.Open(reviewPath);
        JsonElement review = reviewDocument.RootElement;
        JsonContract.RequireOnly(review, "schemaVersion", "reviewerRole", "reviewedSubjectCommit", "reviewedSubjectTree",
            "disposition", "criticalFindings", "importantFindings", "reviewComplete");
        if (JsonContract.RequiredInt64(review, "schemaVersion") != 1
            || JsonContract.RequiredString(review, "reviewerRole") != "independent"
            || JsonContract.RequiredString(review, "reviewedSubjectCommit") != expectedSubjectCommit
            || JsonContract.RequiredString(review, "reviewedSubjectTree") != expectedSubjectTree
            || JsonContract.RequiredString(review, "disposition") != "PASS_NO_REMAINING_CRITICAL_OR_IMPORTANT"
            || JsonContract.RequiredInt64(review, "criticalFindings") != 0
            || JsonContract.RequiredInt64(review, "importantFindings") != 0
            || !RequiredBoolean(review, "reviewComplete"))
        {
            throw new InvalidDataException("Independent review semantics do not permit approval or final return.");
        }
    }

    private static string ResolveOutputPath(string evidenceRoot, string id)
    {
        string direct = Path.Combine(evidenceRoot, id);
        string audit = Path.Combine(evidenceRoot, "docs", "audits", "2026-08-30", "evidence", id);
        return File.Exists(direct) ? direct : audit;
    }

    private static bool IsCanonicalUtc(string value) =>
        DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out _);

    private sealed record ManifestEvidence(
        string Status,
        IReadOnlyDictionary<string, GateEvidence> Commands,
        IReadOnlyDictionary<string, BoundEvidenceArtifact> Outputs);

    private sealed record BoundEvidenceArtifact(string Kind, string Id, string Path);

    private sealed record GateEvidence(
        string CommandId, long Executed, long Passed, long Failed, long Skipped, long ExitCode, string Disposition)
    {
        internal bool IsPassing => Executed > 0 && Passed == Executed && Failed == 0 && Skipped == 0
            && ExitCode == 0 && Disposition == "passed";
    }

    private static void RequireExactObject(JsonElement root, string name, string expected)
    {
        JsonContract.RequireGitObject(expected, name);
        if (JsonContract.RequiredString(root, name) != expected)
        {
            throw new InvalidDataException($"Post-acceptance {name} mismatch.");
        }
    }

    private static string VerifyFileBinding(JsonElement binding, string evidenceRoot)
    {
        JsonContract.RequireOnly(binding, "path", "sha256", "bytes");
        string relative = RequireRepositoryPathValue(JsonContract.RequiredString(binding, "path"));
        string root = Path.GetFullPath(evidenceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            throw new InvalidDataException("Post-acceptance file binding is absent or escaping.");
        }
        VerifyBoundFile(path, binding);
        return path;
    }

    private static void VerifyBoundFile(string path, JsonElement binding)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException("Post-acceptance bound file is absent.");
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
