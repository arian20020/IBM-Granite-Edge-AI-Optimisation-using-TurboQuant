namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Carries the selected path and the exact file identity already observed by
/// the caller.
/// </summary>
public sealed record VocabOnlyProbeRequest(
    string ModelPath,
    long ExpectedLengthBytes,
    DateTimeOffset ExpectedLastWriteTimeUtc);
