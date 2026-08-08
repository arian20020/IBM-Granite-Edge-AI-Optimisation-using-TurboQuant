namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Reports either the completed package checkpoint or one genuine normalized
/// native-load fraction.
/// </summary>
public sealed record VocabOnlyProbeProgress(
    bool PackageValidated,
    float? NativeFraction);
