namespace GraniteEdgeAI.OpenVino.Contracts;

/// <summary>Exact path-free build identities for the verified worker closure.</summary>
public sealed record OpenVinoBuildEvidence(
    string RuntimeBuild,
    string GenAiBuild,
    string TokenizersBuild,
    string WorkerManifestDigest)
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
    }
}
