namespace GraniteEdgeAI.Features.ModelImport.Controls
{
    /// <summary>
    /// Contains only the formatted values displayed by the successful
    /// model-import card.
    /// </summary>
    internal sealed record ImportedModelCardData(
        string FileName,
        string ModelName,
        string Parameters,
        string Architecture,
        string Quantization,
        string FileSize,
        string DeclaredContext);
}
