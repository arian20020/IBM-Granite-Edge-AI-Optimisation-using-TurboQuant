namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Provides the production, contracts-agnostic CPU VocabOnly probe seam.
/// </summary>
public interface IVocabOnlyModelProbe
{
    Task<VocabOnlyModelProbeResult> RunAsync(
        VocabOnlyProbeRequest request,
        IProgress<VocabOnlyProbeProgress>? progress,
        CancellationToken cancellationToken);
}
