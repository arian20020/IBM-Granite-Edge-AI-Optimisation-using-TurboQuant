using System;
using System.IO;
using System.Linq;

namespace GraniteEdgeAI.Features.ChatModels;

internal enum ChatModelRoute
{
    Gguf = 0,
    OpenVino = 1,
}

internal enum ChatModelReadiness
{
    Ready = 0,
    NeedsInspection = 1,
    Unavailable = 2,
}

internal sealed record ChatModelDescriptor
{
    private const int MaximumIdLength = 64;
    private const int MaximumDisplayNameLength = 128;
    private const int MaximumLabelLength = 96;

    private ChatModelDescriptor(
        string id,
        string displayName,
        ChatModelRoute route,
        string formatLabel,
        string runtimeLabel,
        ChatModelReadiness readiness)
    {
        Id = id;
        DisplayName = displayName;
        Route = route;
        FormatLabel = formatLabel;
        RuntimeLabel = runtimeLabel;
        Readiness = readiness;
    }

    internal string Id { get; }
    internal string DisplayName { get; }
    internal ChatModelRoute Route { get; }
    internal string FormatLabel { get; }
    internal string RuntimeLabel { get; }
    internal ChatModelReadiness Readiness { get; }

    internal static ChatModelDescriptor Create(
        string id,
        string displayName,
        ChatModelRoute route,
        string formatLabel,
        string runtimeLabel,
        ChatModelReadiness readiness)
    {
        ValidateId(id);
        ValidateLabel(displayName, MaximumDisplayNameLength, nameof(displayName));
        ValidateLabel(formatLabel, MaximumLabelLength, nameof(formatLabel));
        ValidateLabel(runtimeLabel, MaximumLabelLength, nameof(runtimeLabel));
        if (!Enum.IsDefined(route))
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }
        if (!Enum.IsDefined(readiness))
        {
            throw new ArgumentOutOfRangeException(nameof(readiness));
        }

        return new ChatModelDescriptor(
            id,
            displayName,
            route,
            formatLabel,
            runtimeLabel,
            readiness);
    }

    internal static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length > MaximumIdLength
            || !IsAsciiLowerOrDigit(id[0])
            || id.Any(character =>
                !IsAsciiLowerOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "A chat model identifier must be a bounded opaque identifier.",
                nameof(id));
        }
    }

    private static void ValidateLabel(string value, int maximumLength, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        if (value.Length > maximumLength
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsControl)
            || Path.IsPathRooted(value))
        {
            throw new ArgumentException(
                "Chat model presentation metadata is invalid.",
                name);
        }
    }

    private static bool IsAsciiLowerOrDigit(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9';
}

internal sealed record ChatModelSnapshot(
    string Id,
    string DisplayName,
    ChatModelRoute Route,
    string FormatLabel,
    string RuntimeLabel,
    ChatModelReadiness Readiness,
    bool IsActive);
