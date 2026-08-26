using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.GgufQuantization.WorkerClient;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

internal sealed class VerifiedGgufQuantizationRunner : IGgufQuantizationRunner
{
    private readonly VerifiedGgufQuantizerPackage _package;
    private readonly GgufQuantizationWorkerClient _client;

    internal VerifiedGgufQuantizationRunner(
        string packageDirectory,
        string expectedManifestSha256,
        TimeSpan timeout)
    {
        _package = GgufQuantizerPackageVerifier.Verify(
            packageDirectory,
            expectedManifestSha256);
        _client = new GgufQuantizationWorkerClient(timeout);
    }

    public string ManifestSha256 => _package.ManifestSha256;
    public string ExecutableSha256 => _package.ExecutableSha256;

    public async Task<GgufQuantizationEvent> RunAsync(
        GgufQuantizationCommand command,
        string sourcePath,
        string sourceSha256,
        ulong sourceLengthBytes,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using GgufQuantizationFileLease lease =
            GgufQuantizationFileLease.Create(
                sourcePath,
                sourceSha256,
                sourceLengthBytes,
                outputPath);
        return await _client.ExecuteAsync(
            command,
            _package,
            lease,
            cancellationToken).ConfigureAwait(false);
    }
}
