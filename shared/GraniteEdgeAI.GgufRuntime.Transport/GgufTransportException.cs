namespace GraniteEdgeAI.GgufRuntime.Transport;

public sealed class GgufTransportException : IOException
{
    public GgufTransportException(string message)
        : base(message)
    {
    }
}
