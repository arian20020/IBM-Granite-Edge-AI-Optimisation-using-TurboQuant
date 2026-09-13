namespace GraniteEdgeAI.OpenVino.Contracts;

/// <summary>Exact path-free build identities for the verified worker closure.</summary>
public sealed record TurboQuantBuildEvidence(
    string SourceCommit,
    string ImplementationCommit,
    string PatchSeriesDigest,
    string RuntimeManifestDigest)
{
    public void Validate()
    {
        RequireGitCommit(SourceCommit, nameof(SourceCommit));
        RequireGitCommit(ImplementationCommit, nameof(ImplementationCommit));
        OpenVinoProtocol.RequireSha256(PatchSeriesDigest, nameof(PatchSeriesDigest));
        OpenVinoProtocol.RequireSha256(RuntimeManifestDigest, nameof(RuntimeManifestDigest));
    }

    private static void RequireGitCommit(string? value, string name) =>
        OpenVinoProtocol.Require(
            value is { Length: 40 } && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            name + " must be a lowercase 40-character Git object identity.");
}

public sealed record OpenVinoBuildEvidence(
    string RuntimeBuild,
    string GenAiBuild,
    string TokenizersBuild,
    string WorkerManifestDigest,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    TurboQuantBuildEvidence? TurboQuantBuild = null)
{
    public void Validate()
    {
        OpenVinoProtocol.RequireUtf8Limit(
            RuntimeBuild,
            OpenVinoProtocol.MaximumBuildIdentityUtf8Bytes,
            nameof(RuntimeBuild));
        OpenVinoProtocol.RequireUtf8Limit(
            GenAiBuild,
            OpenVinoProtocol.MaximumBuildIdentityUtf8Bytes,
            nameof(GenAiBuild));
        OpenVinoProtocol.RequireUtf8Limit(
            TokenizersBuild,
            OpenVinoProtocol.MaximumBuildIdentityUtf8Bytes,
            nameof(TokenizersBuild));
        OpenVinoProtocol.RequireSha256(
            WorkerManifestDigest,
            nameof(WorkerManifestDigest));
        TurboQuantBuild?.Validate();
    }
}
