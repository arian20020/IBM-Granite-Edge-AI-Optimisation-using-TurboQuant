namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal sealed class GgufUnsupportedChatTemplateException : Exception
{
    internal GgufUnsupportedChatTemplateException()
        : base("The model chat template does not support the required system role.")
    {
    }

    internal GgufUnsupportedChatTemplateException(Exception innerException)
        : base(
            "The model chat template does not support the required system role.",
            innerException)
    {
    }
}
