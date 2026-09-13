using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.State;

public sealed record HardwareInspectionStageCopy(
    string Title,
    string ActiveExplanation,
    string CompletedSentence,
    string WaitingSentence);

public sealed record HardwareInspectionAction
{
    public HardwareInspectionAction(
        HardwareInspectionActionKind kind,
        string label,
        bool isVisible,
        bool isEnabled,
        string? accessibleHelp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Kind = kind;
        Label = label;
        IsVisible = isVisible;
        IsEnabled = isEnabled;
        AccessibleHelp = accessibleHelp;
    }

    public HardwareInspectionActionKind Kind { get; }
    public string Label { get; }
    public bool IsVisible { get; }
    public bool IsEnabled { get; }
    public string? AccessibleHelp { get; }
}

public sealed record HardwareInspectionStageRow
{
    public HardwareInspectionStageRow(
        HardwareInspectionStage stage,
        HardwareInspectionStageRowState state,
        string title,
        string sentence,
        string accessibleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sentence);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);
        Stage = stage;
        State = state;
        Title = title;
        Sentence = sentence;
        AccessibleName = accessibleName;
    }

    public HardwareInspectionStage Stage { get; }
    public HardwareInspectionStageRowState State { get; }
    public string Title { get; }
    public string Sentence { get; }
    public string AccessibleName { get; }
}

public sealed class HardwareInspectionPresentationState
{
    public HardwareInspectionPresentationState(
        HardwareInspectionPresentationKind kind,
        string subtitle,
        string kicker,
        string title,
        string body,
        string announcement,
        string defaultFocusKey,
        IEnumerable<HardwareInspectionStageRow> stageRows,
        IEnumerable<HardwareInspectionAction> actions,
        int completedStageCount,
        bool detailsAvailable,
        bool reportCreated,
        int unresolvedReviewCount = 0,
        int resolvedInformationCount = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subtitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(kicker);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(announcement);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultFocusKey);
        ArgumentNullException.ThrowIfNull(stageRows);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentOutOfRangeException.ThrowIfNegative(completedStageCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(completedStageCount, 7);
        ArgumentOutOfRangeException.ThrowIfNegative(unresolvedReviewCount);
        ArgumentOutOfRangeException.ThrowIfNegative(resolvedInformationCount);

        HardwareInspectionStageRow[] rowCopy = stageRows.ToArray();
        HardwareInspectionAction[] actionCopy = actions.ToArray();
        if (rowCopy.Any(row => row is null) || actionCopy.Any(action => action is null))
        {
            throw new ArgumentException("Presentation collections cannot contain null.");
        }

        Kind = kind;
        Subtitle = subtitle;
        Kicker = kicker;
        Title = title;
        Body = body;
        Announcement = announcement;
        DefaultFocusKey = defaultFocusKey;
        StageRows = Array.AsReadOnly(rowCopy);
        Actions = Array.AsReadOnly(actionCopy);
        CompletedStageCount = completedStageCount;
        DetailsAvailable = detailsAvailable;
        ReportCreated = reportCreated;
        UnresolvedReviewCount = unresolvedReviewCount;
        ResolvedInformationCount = resolvedInformationCount;
    }

    public HardwareInspectionPresentationKind Kind { get; }
    internal int? CompletedChecks { get; private init; }
    internal int? TotalChecks { get; private init; }
    internal object ProgressOwner { get; private init; } = new();
    internal IReadOnlyDictionary<HardwareInspectionStage, (int Completed, int Total)> GroupChecks { get; private init; }
        = new Dictionary<HardwareInspectionStage, (int Completed, int Total)>();
    internal HardwareInspectionPresentationState WithGroupChecks(
        IReadOnlyDictionary<HardwareInspectionStage, (int Completed, int Total)> groups, object owner)
    {
        return new HardwareInspectionPresentationState(Kind, Subtitle, Kicker, Title, Body, Announcement, DefaultFocusKey,
            StageRows, Actions, CompletedStageCount, DetailsAvailable, ReportCreated, UnresolvedReviewCount, ResolvedInformationCount)
        { CompletedChecks = CompletedChecks, TotalChecks = TotalChecks, ProgressOwner = owner,
          GroupChecks = new System.Collections.ObjectModel.ReadOnlyDictionary<HardwareInspectionStage, (int Completed, int Total)>(
              new Dictionary<HardwareInspectionStage, (int Completed, int Total)>(groups)) };
    }
    internal HardwareInspectionPresentationState WithMeasuredChecks(int? completed, int? total)
    {
        if (completed.HasValue != total.HasValue || completed < 0 || total <= 0 || completed > total)
            throw new ArgumentOutOfRangeException(nameof(completed));
        return new HardwareInspectionPresentationState(Kind, Subtitle, Kicker, Title, Body, Announcement, DefaultFocusKey,
            StageRows, Actions, CompletedStageCount, DetailsAvailable, ReportCreated, UnresolvedReviewCount, ResolvedInformationCount)
        { CompletedChecks = completed, TotalChecks = total, GroupChecks = GroupChecks, ProgressOwner = ProgressOwner };
    }
    public string Subtitle { get; }
    public string Kicker { get; }
    public string Title { get; }
    public string Body { get; }
    public string Announcement { get; }
    public string DefaultFocusKey { get; }
    public IReadOnlyList<HardwareInspectionStageRow> StageRows { get; }
    public IReadOnlyList<HardwareInspectionAction> Actions { get; }
    public int CompletedStageCount { get; }
    public bool DetailsAvailable { get; }
    public bool ReportCreated { get; }
    public int UnresolvedReviewCount { get; }
    public int ResolvedInformationCount { get; }
}
