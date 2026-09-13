namespace GraniteEdgeAI.Features.GgufRuntime.Clipboard;

internal interface IChatClipboard
{
    bool TrySetText(string text);
}
