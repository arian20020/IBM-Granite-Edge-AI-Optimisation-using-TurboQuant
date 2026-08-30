namespace GraniteEdgeAI.GgufRuntime.WorkerClient;

public sealed class GgufWorkerPolicyException : InvalidOperationException
{
    internal GgufWorkerPolicyException(string code, string message)
        : this(code, message, null)
    {
    }

    internal GgufWorkerPolicyException(
        string code,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }

    internal static GgufWorkerPolicyException EnvironmentFailure()
    {
        return new GgufWorkerPolicyException(
            "worker-environment-policy-failed",
            "The GGUF runtime worker environment could not be secured.");
    }
}
