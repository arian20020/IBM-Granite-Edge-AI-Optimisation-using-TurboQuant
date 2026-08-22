using System.Text;

namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Writes fixed test coordination markers to stderr. Markers contain no model
/// path, request identifier, secret, or user-controlled text.
/// </summary>
internal sealed class FixtureMilestoneWriter
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly Stream _standardError;

    internal FixtureMilestoneWriter(Stream standardError)
    {
        _standardError = standardError;
    }

    internal async Task WriteAsync(string fixedMilestone)
    {
        byte[] payload = StrictUtf8.GetBytes(fixedMilestone + "\n");
        await _standardError.WriteAsync(payload).ConfigureAwait(false);
        await _standardError.FlushAsync().ConfigureAwait(false);
    }
}
