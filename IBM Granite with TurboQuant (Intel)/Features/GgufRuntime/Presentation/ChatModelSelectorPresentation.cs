using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using GraniteEdgeAI.Features.ChatModels;

namespace GraniteEdgeAI.Features.GgufRuntime.Presentation;

internal static class ChatModelDisplayName
{
    internal static string OpenVino(string? candidate, string? origin = null)
    {
        string name = Clean(candidate);
        if (name.Length == 0) return "Imported OpenVINO model";
        if (IsGenerated(name)) name = Clean(origin);
        if (IsGenerated(name)) name = string.Empty;
        return name.Length == 0 ? "Optimised OpenVINO model" : name;
    }

    private static bool IsGenerated(string name)
    {
        string[] parts = name.Split('-');
        return parts.Length is 2 or 3
            && string.Equals(parts[0], "output", StringComparison.OrdinalIgnoreCase)
            && parts[1].Length is 32 or 64
            && parts[1].All(Uri.IsHexDigit)
            && (parts.Length == 2 || parts[2].Length > 0 && parts[2].All(char.IsAsciiDigit));
    }

    private static string Clean(string? value)
    {
        string name = Path.GetFileName(value?.Trim().TrimEnd('\\', '/') ?? string.Empty);
        name = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        return name.Length <= 128 ? name : name[..128];
    }
}

// These inputs describe the requested configuration, not runtime activation evidence.
internal static class ChatModelFormatLabel
{
    internal static string Gguf(string? weights, string? keyCache, string? valueCache)
    {
        string key = GgufCache(keyCache);
        string value = GgufCache(valueCache);
        string cache = key == "unknown" && value == "unknown"
            ? "cache unknown"
            : key == value
                ? $"{key} cache (selected)"
                : $"K: {key} / V: {value} cache (selected)";
        return $"{cache} · {Weights(weights)} · GGUF";
    }

    internal static string OpenVino(string? weights, string? cachePrecision)
    {
        string cache = cachePrecision switch
        {
            "tbq3" or "Tbq3" => "TQ3 cache (selected)",
            "tbq4" or "Tbq4" => "TQ4 cache (selected)",
            "u4" or "U4" => "U4 cache (selected)",
            "u8" or "U8" => "U8 cache (selected)",
            "released-default" or "ReleasedDefault" => "default cache (unverified)",
            _ => "cache unknown",
        };
        return $"{cache} · {Weights(weights)} · OpenVINO";
    }

    private static string GgufCache(string? format) => format switch
    {
        "Turbo2" => "TQ2",
        "Turbo3" => "TQ3",
        "Turbo4" => "TQ4",
        "F16" => "F16",
        "Q8Zero" => "Q8_0",
        "Q4Zero" => "Q4_0",
        _ => "unknown",
    };

    private static string Weights(string? format)
    {
        string? label = format switch
        {
            "Q2K" => "Q2_K",
            "Q3KM" => "Q3_K_M",
            "Q4KM" => "Q4_K_M",
            "Q5KM" => "Q5_K_M",
            "Q6K" => "Q6_K",
            "Fp16" => "FP16",
            "EightBit" => "INT8",
            "FourBit" => "INT4",
            "MxFp4" => "MXFP4",
            null or "" or "Imported" or "Unspecified" => null,
            _ => format.Length <= 16 && format.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                ? format : null,
        };
        return label is null ? "weights unknown" : $"{label} weights";
    }
}

public sealed record ChatModelSelectorItem(
    string Id,
    string DisplayName,
    string FormatLabel,
    string RuntimeLabel,
    string AutomationId,
    string AutomationName,
    bool IsSelectable,
    bool IsActive)
{
    public string SelectedGlyph => IsActive ? "\uE73E" : string.Empty;
}

internal sealed record ChatModelSelectorPresentation(
    IReadOnlyList<ChatModelSelectorItem> Models,
    string? ActiveModelId,
    string CurrentLabel,
    string CurrentAutomationName)
{
    internal static ChatModelSelectorPresentation Create(
        IReadOnlyList<ChatModelSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        ChatModelSelectorItem[] models = snapshots
            .Where(snapshot => snapshot.Readiness == ChatModelReadiness.Ready)
            .Select((snapshot, index) => new ChatModelSelectorItem(
                snapshot.Id,
                snapshot.DisplayName,
                snapshot.FormatLabel,
                snapshot.RuntimeLabel,
                $"ChatModelOption-{index + 1}",
                snapshot.IsActive
                    ? $"Current model {snapshot.DisplayName}, {snapshot.FormatLabel}, {snapshot.RuntimeLabel}"
                    : $"Select {snapshot.DisplayName}, {snapshot.FormatLabel}, {snapshot.RuntimeLabel}",
                IsSelectable: true,
                snapshot.IsActive))
            .ToArray();
        ChatModelSnapshot? active = snapshots.FirstOrDefault(snapshot =>
            snapshot.Readiness == ChatModelReadiness.Ready && snapshot.IsActive);
        string currentLabel = active is null
            ? "Choose model"
            : active.FormatLabel;
        string currentAutomationName = active is null
            ? "Choose a chat model"
            : $"Current model {active.DisplayName}, {active.FormatLabel}";

        return new ChatModelSelectorPresentation(
            models,
            active?.Id,
            currentLabel,
            currentAutomationName);
    }
}

internal sealed record ChatModelSwitchPresentation(
    bool IsSwitching,
    bool CanCancel,
    bool IsFailure,
    bool RestoreFocus,
    string StatusMessage)
{
    internal static ChatModelSwitchPresentation Starting { get; } = new(
        IsSwitching: true,
        CanCancel: true,
        IsFailure: false,
        RestoreFocus: false,
        StatusMessage: "Switching model. The previous model remains active until this one is ready.");

    internal static ChatModelSwitchPresentation Idle { get; } = new(
        IsSwitching: false,
        CanCancel: false,
        IsFailure: false,
        RestoreFocus: false,
        StatusMessage: string.Empty);

    internal static ChatModelSwitchPresentation FromResult(
        ChatModelSwitchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Disposition switch
        {
            ChatModelSwitchDisposition.Activated => Idle,
            ChatModelSwitchDisposition.AlreadyActive => Idle,
            ChatModelSwitchDisposition.Cancelled => new(
                false,
                false,
                false,
                true,
                "Model switch cancelled. The previous model remains active."),
            ChatModelSwitchDisposition.NotFound => Failure(
                "That model is no longer available. The previous model remains active."),
            ChatModelSwitchDisposition.NotReady => Failure(
                "That model is not ready for chat. The previous model remains active."),
            ChatModelSwitchDisposition.SourceUnavailable => Failure(
                "That model source is no longer available. Import it again to use it."),
            ChatModelSwitchDisposition.RuntimeUnavailable => Failure(
                "The local runtime could not start that model. The previous model remains active."),
            ChatModelSwitchDisposition.HistoryRejected => Failure(
                "This conversation contains an incomplete reply. Start a new chat to use another model."),
            ChatModelSwitchDisposition.ContextExceeded => Failure(
                "This conversation is too long for that model. Start a new chat to use it. The previous model remains active."),
            _ => Failure(
                "That model could not be opened. The previous model remains active."),
        };
    }

    private static ChatModelSwitchPresentation Failure(string message) => new(
        IsSwitching: false,
        CanCancel: false,
        IsFailure: true,
        RestoreFocus: true,
        StatusMessage: message);
}
