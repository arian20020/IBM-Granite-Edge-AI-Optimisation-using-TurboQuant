using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.GgufQuantization.Contracts;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

internal interface IGgufQuantizationRunner
{
    string ManifestSha256 { get; }
    string ExecutableSha256 { get; }

    Task<GgufQuantizationEvent> RunAsync(
        GgufQuantizationCommand command,
        string sourcePath,
        string sourceSha256,
        ulong sourceLengthBytes,
        string outputPath,
        CancellationToken cancellationToken);
}
