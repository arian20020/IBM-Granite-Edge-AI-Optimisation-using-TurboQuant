using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.ViewModels;

internal interface IModelInspectionStartupPresentationBarrier
{
    ValueTask WaitForPresentationAsync(CancellationToken cancellationToken);
}
