namespace GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

public sealed class GgufRuntimeTrustException : InvalidOperationException
{
    public GgufRuntimeTrustException(string code)
        : base("The packaged GGUF runtime failed trust verification.")
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A stable trust code is required.", nameof(code));
        }

        Code = code;
    }

    public string Code { get; }
}
