using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.Features.ChatModels;

namespace GraniteEdgeAI.Features.GgufRuntime.Presentation;

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
