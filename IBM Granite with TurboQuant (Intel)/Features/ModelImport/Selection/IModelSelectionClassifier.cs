using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal interface IModelSelectionClassifier
{
    Task<ModelSelectionResult> ClassifyAsync(
        ModelSelectionOperationId operationId,
        ModelSelectionInput input,
        CancellationToken cancellationToken);
}
