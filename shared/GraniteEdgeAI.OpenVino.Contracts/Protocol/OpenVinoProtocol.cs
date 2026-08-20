using System.Text;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.OpenVino.Contracts;

/// <summary>Closed limits and wire identities for OpenVINO workers.</summary>
public static class OpenVinoProtocol
{
    public const string OfficialProtocolId = "openvino.official/1";
    public const string TurboQuantProtocolId = "openvino.turboquant/1";
    public const int MaximumJsonDepth = 32;
    public const int MaximumLineBytes = 1024 * 1024;
    public const int MaximumPromptUtf8Bytes = 64 * 1024;
    public const int MaximumPackagePathUtf8Bytes = 32 * 1024;
    public const int MaximumBuildIdentityUtf8Bytes = 256;
    public const int MaximumDeviceIdentityUtf8Bytes = 128;
    public const int MaximumActualExecutionDevices = 8;
    public const int MaximumNewTokens = 512;
    public const int MaximumOperationTextUtf8Bytes = 4 * 1024 * 1024;
    public const int MaximumTurns = 32;

    internal static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static readonly Regex LowercaseSha256 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    internal static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new OpenVinoProtocolException(message);
        }
    }

    internal static void RequireUuid(Guid value, string name) =>
        Require(value != Guid.Empty, name + " must be a non-empty UUID.");

    internal static void RequireText(string? value, string name) =>
        Require(!string.IsNullOrWhiteSpace(value), name + " must not be empty.");

    internal static void RequireUtf8Limit(string? value, int maximumBytes, string name)
    {
        RequireText(value, name);
        try
        {
            Require(StrictUtf8.GetByteCount(value!) <= maximumBytes, name + " exceeds its permitted UTF-8 length.");
        }
        catch (EncoderFallbackException)
        {
            throw new OpenVinoProtocolException(name + " must be valid UTF-8 text.");
        }
    }

    internal static void RequirePackagePath(string? value, string name)
    {
        RequireUtf8Limit(value, MaximumPackagePathUtf8Bytes, name);
        Require(
            Path.IsPathFullyQualified(value!) &&
            !value!.Any(char.IsControl),
            name + " must be an absolute control-free path.");
    }

    internal static void RequireSha256(string? value, string name) =>
        Require(
            value is not null && LowercaseSha256.IsMatch(value),
            name + " must be a lowercase SHA-256 digest.");
}

/// <summary>Reports a safe, typed rejection at the OpenVINO protocol boundary.</summary>
public sealed class OpenVinoProtocolException : Exception
{
    public OpenVinoProtocolException(string message)
        : base(message)
    {
    }
}
