using System;

namespace GraniteEdgeAI.Features.Prompting;

public sealed record PromptSurfaceState(
    string CapabilitySummary,
    string ExecutionEvidence,
    string BuildEvidence,
    string ResponseText,
    string Announcement,
    bool SendEnabled,
    bool StopEnabled,
    bool CancelEnabled);

/// <summary>
/// Reduces route-neutral prompt events into one shared prompt-surface state.
/// Runtime adapters remain outside the presenter.
/// </summary>
public sealed class PromptSessionPresenter
{
    public PromptSessionPresenter(PromptRoutePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        State = new PromptSurfaceState(
            presentation.CapabilitySummary,
            presentation.ExecutionEvidence,
            presentation.BuildEvidence,
            ResponseText: string.Empty,
            presentation.ReadyAnnouncement,
            SendEnabled: true,
            StopEnabled: false,
            CancelEnabled: true);
    }

    public PromptSurfaceState State { get; private set; }

    public void Apply(PromptEvent promptEvent)
    {
        ArgumentNullException.ThrowIfNull(promptEvent);
        State = promptEvent.Kind switch
        {
            PromptEventKind.GeneratingTurn => State with
            {
                ResponseText = string.Empty,
                Announcement = "Generating locally.",
                SendEnabled = false,
                StopEnabled = true,
                CancelEnabled = true
            },
            PromptEventKind.TextDelta => State with
            {
                ResponseText = State.ResponseText + (promptEvent.Text ?? string.Empty)
            },
            PromptEventKind.StoppingTurn => State with
            {
                Announcement = "Stopping generation.",
                SendEnabled = false,
                StopEnabled = false,
                CancelEnabled = true
            },
            PromptEventKind.TurnCompleted or PromptEventKind.SessionReady =>
                State with
                {
                    Announcement = "Local session ready.",
                    SendEnabled = true,
                    StopEnabled = false,
                    CancelEnabled = true
                },
            PromptEventKind.CancellingSession => State with
            {
                Announcement = "Cancelling local session.",
                SendEnabled = false,
                StopEnabled = false,
                CancelEnabled = false
            },
            PromptEventKind.Cancelled or PromptEventKind.SessionCompleted =>
                State with
                {
                    Announcement = "Local session closed.",
                    SendEnabled = false,
                    StopEnabled = false,
                    CancelEnabled = false
                },
            PromptEventKind.Failed => FailedState(promptEvent.Failure),
            _ => State
        };
    }

    public void ApplyFailure(PromptFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        State = FailedState(failure);
    }

    private PromptSurfaceState FailedState(PromptFailure? failure)
    {
        PromptFailure safeFailure = failure ?? new PromptFailure(
            "runtime_protocol_failed",
            "The local prompt could not continue.",
            "Close the session and choose the model again.");
        string supportCode = IsSafeSupportCode(safeFailure.SupportCode)
            ? safeFailure.SupportCode
            : "runtime_protocol_failed";
        return State with
        {
            ResponseText = $"{safeFailure.Message} {safeFailure.RecoveryAction} " +
                $"Support code: {supportCode}",
            Announcement = $"{safeFailure.Message} {safeFailure.RecoveryAction}",
            SendEnabled = false,
            StopEnabled = false,
            CancelEnabled = false
        };
    }

    private static bool IsSafeSupportCode(string supportCode)
    {
        if (string.IsNullOrWhiteSpace(supportCode))
        {
            return false;
        }

        foreach (char character in supportCode)
        {
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
