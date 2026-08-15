using System.Collections.ObjectModel;

namespace HardwareInspection.LlmFitSpike.Candidate;

public sealed record LlmFitCandidateManifest
{
    public LlmFitCandidateManifest(
        string schemaVersion,
        string candidateId,
        string version,
        string releaseTag,
        string releaseCommit,
        DateTimeOffset publishedAtUtc,
        LlmFitCandidateArchive archive,
        LlmFitCandidateExecutable executable,
        IEnumerable<string> requiredFileValues,
        LlmFitCandidateCommands commands,
        LlmFitCandidateLicense license)
    {
        SchemaVersion = schemaVersion;
        CandidateId = candidateId;
        Version = version;
        ReleaseTag = releaseTag;
        ReleaseCommit = releaseCommit;
        PublishedAtUtc = publishedAtUtc;
        Archive = archive;
        Executable = executable;
        RequiredFiles = new ReadOnlyCollection<string>(requiredFileValues.ToArray());
        Commands = commands;
        License = license;
    }

    public string SchemaVersion { get; }

    public string CandidateId { get; }

    public string Version { get; }

    public string ReleaseTag { get; }

    public string ReleaseCommit { get; }

    public DateTimeOffset PublishedAtUtc { get; }

    public LlmFitCandidateArchive Archive { get; }

    public LlmFitCandidateExecutable Executable { get; }

    public IReadOnlyList<string> RequiredFiles { get; }

    public LlmFitCandidateCommands Commands { get; }

    public LlmFitCandidateLicense License { get; }
}

public sealed record LlmFitCandidateArchive(
    string FileName,
    Uri DownloadUri,
    long LengthBytes,
    string Sha256);

public sealed record LlmFitCandidateExecutable(
    string RelativePath,
    string Sha256,
    string PeMachine,
    string AuthenticodePolicy);

public sealed record LlmFitCandidateCommands
{
    public LlmFitCandidateCommands(IEnumerable<string> versionValues, IEnumerable<string> systemValues)
    {
        Version = new ReadOnlyCollection<string>(versionValues.ToArray());
        System = new ReadOnlyCollection<string>(systemValues.ToArray());
    }

    public IReadOnlyList<string> Version { get; }

    public IReadOnlyList<string> System { get; }
}

public sealed record LlmFitCandidateLicense(string Spdx, string RelativePath);
