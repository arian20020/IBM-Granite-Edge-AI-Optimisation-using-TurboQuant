using System;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace GraniteEdgeAI.Features.GgufRuntime.Clipboard;

internal sealed class WindowsChatClipboard : IChatClipboard
{
    public bool TrySetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            return true;
        }
        catch (Exception exception) when (
            exception is COMException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            return false;
        }
    }
}
