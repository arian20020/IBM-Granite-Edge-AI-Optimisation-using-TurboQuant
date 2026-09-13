using GraniteEdgeAI.GgufRuntime.Contracts.Failures;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

public sealed class GgufRuntimeStartupException : InvalidOperationException
{
    internal GgufRuntimeStartupException(GgufRuntimeFailure failure)
        : base($"The GGUF runtime could not start ({failure?.Code}).")
    {
        Failure = failure ?? throw new ArgumentNullException(nameof(failure));
    }

    public GgufRuntimeFailure Failure { get; }
}
