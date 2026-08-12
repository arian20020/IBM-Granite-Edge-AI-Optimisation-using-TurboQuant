#if MODEL_INSPECTION_FIXTURE_GALLERY
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

internal interface IModelInspectionFixturePackageResourceReader
{
    Task<ReadOnlyMemory<byte>> ReadAsync(
        Uri packageUri,
        CancellationToken cancellationToken = default);
}
#endif
