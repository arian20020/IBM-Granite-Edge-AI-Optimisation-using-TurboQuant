using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

internal interface IKnowledgeFilePicker
{
    Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync();
}
