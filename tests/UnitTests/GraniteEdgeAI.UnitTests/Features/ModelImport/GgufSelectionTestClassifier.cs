using GraniteEdgeAI.Features.ModelImport.Selection;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Keeps quick-scan UI tests focused on their injected scanner while still
/// exercising the route boundary introduced before Task 8.
/// </summary>
internal sealed class GgufSelectionTestClassifier : IModelSelectionClassifier
{
    public Task<ModelSelectionResult> ClassifyAsync(
        ModelSelectionOperationId operationId,
        ModelSelectionInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.IsFalse(input.IsFolder);
        Assert.AreEqual(".gguf", Path.GetExtension(input.LocalPath));

        ModelSelectionResult result = ModelSelectionResult.Accepted(
            operationId,
            ModelSelectionRoute.Gguf,
            input.DisplayName);

        Assert.AreEqual(ModelSelectionRoute.Gguf, result.Route);
        return Task.FromResult(result);
    }
}
