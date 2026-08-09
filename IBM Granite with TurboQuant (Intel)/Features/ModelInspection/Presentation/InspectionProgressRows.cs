using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Owns the five XAML-visible progress rows retained for one inspection attempt.
/// </summary>
public sealed class InspectionProgressRows : INotifyPropertyChanged
{
    private const int StageCount = 5;
    private const string InitialSummary = "0 of 5 checks complete";

    private readonly ReadOnlyCollection<InspectionContentItemPresentation> _items;
    private ModelInspectionRenderKey _ownerKey = new(0, 0);
    private long _lastAppliedRevision = -1;
    private InspectionProgressRowsUpdate? _lastAppliedUpdate;
    private string _progressSummary = InitialSummary;

    internal InspectionProgressRows()
    {
        _items = Array.AsReadOnly(
        [
            CreateStage("1", "Check model package", showConnector: true),
            CreateStage("2", "Read model configuration", showConnector: true),
            CreateStage(
                "3",
                "Validate tokenizer and chat setup",
                showConnector: true),
            CreateStage("4", "Validate model structure", showConnector: true),
            CreateStage(
                "5",
                "Confirm core runtime compatibility",
                showConnector: false)
        ]);
    }

    /// <summary>
    /// Gets the fixed five row instances for the current attempt.
    /// </summary>
    public IReadOnlyList<InspectionContentItemPresentation> Items => _items;

    /// <summary>
    /// Gets the observable, privacy-safe completed-check summary.
    /// </summary>
    public string ProgressSummary => _progressSummary;

    /// <summary>
    /// Notifies XAML when the progress summary changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Binds a fresh owner to one attempt while retaining all five row
    /// references and clearing any generation-zero presentation state.
    /// </summary>
    internal void Reset(ModelInspectionRenderKey ownerKey)
    {
        if (ownerKey.PresentationRevision != 0)
        {
            throw new ArgumentException(
                "A progress-row owner must begin at revision zero.",
                nameof(ownerKey));
        }

        if (ownerKey.AttemptGeneration == _ownerKey.AttemptGeneration)
        {
            return;
        }

        if (_ownerKey.AttemptGeneration != 0)
        {
            throw new ArgumentException(
                "A bound progress-row owner cannot cross attempt generations.",
                nameof(ownerKey));
        }

        _ownerKey = ownerKey;
        _lastAppliedRevision = -1;
        _lastAppliedUpdate = null;
        SetProgressSummary(InitialSummary);
        foreach (InspectionContentItemPresentation item in _items)
        {
            ApplyRowValues(
                item,
                InspectionContentStatus.Waiting,
                "Waiting",
                isActive: false,
                stageFraction: null,
                detail: string.Empty,
                Visibility.Collapsed);
        }
    }

    /// <summary>
    /// Applies one immutable, display-safe update without replacing a row.
    /// </summary>
    internal InspectionProgressRowsApplyResult Apply(
        InspectionProgressRowsUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        ValidateUpdate(update);

        if (update.OwnerKey.PresentationRevision < _lastAppliedRevision)
        {
            throw new ArgumentException(
                "A progress update cannot precede the latest applied revision.",
                nameof(update));
        }

        if (update.OwnerKey.PresentationRevision == _lastAppliedRevision)
        {
            if (Equals(update, _lastAppliedUpdate))
            {
                return InspectionProgressRowsApplyResult.Empty;
            }

            throw new ArgumentException(
                "One render revision cannot describe two progress states.",
                nameof(update));
        }

        List<InspectionProgressRowChange> changes = [];
        int currentIndex = update.Key.Stage.HasValue
            ? (int)update.Key.Stage.Value - 1
            : -1;
        for (int index = 0; index < _items.Count; index++)
        {
            RowValues values = CreateRowValues(index, currentIndex, update.Key);
            InspectionContentItemPresentation row = _items[index];
            bool statusChanged = row.Status != values.Status ||
                !string.Equals(
                    row.StatusText,
                    values.StatusText,
                    StringComparison.Ordinal) ||
                row.IsActive != values.IsActive ||
                row.StageFraction != values.StageFraction;
            bool detailChanged = !string.Equals(
                    row.Detail,
                    values.Detail,
                    StringComparison.Ordinal) ||
                row.DetailVisibility != values.DetailVisibility;
            string automationName = CreateAutomationName(
                row.Title,
                values.StatusText,
                values.Detail,
                values.DetailVisibility);
            if (!string.Equals(
                    row.AutomationName,
                    automationName,
                    StringComparison.Ordinal) &&
                !statusChanged &&
                !detailChanged)
            {
                detailChanged = true;
            }

            if (statusChanged || detailChanged)
            {
                changes.Add(new InspectionProgressRowChange(
                    index,
                    statusChanged,
                    detailChanged));
            }

            ApplyRowValues(
                row,
                values.Status,
                values.StatusText,
                values.IsActive,
                values.StageFraction,
                values.Detail,
                values.DetailVisibility);
        }

        bool summaryChanged = !string.Equals(
            _progressSummary,
            update.ProgressSummary,
            StringComparison.Ordinal);
        SetProgressSummary(update.ProgressSummary);
        _lastAppliedRevision = update.OwnerKey.PresentationRevision;
        _lastAppliedUpdate = update;

        return changes.Count == 0 && !summaryChanged
            ? InspectionProgressRowsApplyResult.Empty
            : new InspectionProgressRowsApplyResult(changes, summaryChanged);
    }

    private static InspectionContentItemPresentation CreateStage(
        string stageNumber,
        string title,
        bool showConnector)
    {
        return new InspectionContentItemPresentation
        {
            StageNumber = stageNumber,
            Title = title,
            Detail = string.Empty,
            DetailVisibility = Visibility.Collapsed,
            Status = InspectionContentStatus.Waiting,
            StatusText = "Waiting",
            IsActive = false,
            StageFraction = null,
            ShowConnector = showConnector,
            AutomationName = $"{title}. Waiting."
        };
    }

    private static RowValues CreateRowValues(
        int index,
        int currentIndex,
        ModelInspectionProgressRegionKey key)
    {
        if (currentIndex < 0 || index > currentIndex)
        {
            return RowValues.Waiting;
        }

        if (index < currentIndex)
        {
            return RowValues.Passed;
        }

        (InspectionContentStatus status, string statusText, bool isActive) =
            key.StageStatus switch
            {
                ModelInspectionStageStatus.Active =>
                    (InspectionContentStatus.Active, "Checking", true),
                ModelInspectionStageStatus.Completed =>
                    (InspectionContentStatus.Passed, "Passed", false),
                ModelInspectionStageStatus.Warning =>
                    (InspectionContentStatus.Warning, "Warning", false),
                ModelInspectionStageStatus.Failed =>
                    (InspectionContentStatus.Error, "Failed", false),
                ModelInspectionStageStatus.Cancelled =>
                    (InspectionContentStatus.Information, "Cancelled", false),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(key),
                    key.StageStatus,
                    "Unknown Model Inspection stage status.")
            };

        return new RowValues(
            status,
            statusText,
            isActive,
            isActive ? key.StageFraction : null,
            key.Detail,
            Visibility.Visible);
    }

    private static void ApplyRowValues(
        InspectionContentItemPresentation row,
        InspectionContentStatus status,
        string statusText,
        bool isActive,
        double? stageFraction,
        string detail,
        Visibility detailVisibility)
    {
        row.Status = status;
        row.StatusText = statusText;
        row.IsActive = isActive;
        row.StageFraction = stageFraction;
        row.Detail = detail;
        row.DetailVisibility = detailVisibility;
        row.AutomationName = CreateAutomationName(
            row.Title,
            statusText,
            detail,
            detailVisibility);
    }

    private static string CreateAutomationName(
        string title,
        string statusText,
        string detail,
        Visibility detailVisibility)
    {
        return detailVisibility == Visibility.Visible && detail.Length > 0
            ? $"{title}. {statusText}. {detail}"
            : $"{title}. {statusText}.";
    }

    private void ValidateUpdate(InspectionProgressRowsUpdate update)
    {
        if (update.OwnerKey.AttemptGeneration != _ownerKey.AttemptGeneration)
        {
            throw new ArgumentException(
                "The progress update does not belong to this row owner.",
                nameof(update));
        }

        ModelInspectionProgressRegionKey key = update.Key;
        if (!key.Stage.HasValue)
        {
            if (key.CompletedStageCount != 0 ||
                key.StageCount is not (0 or StageCount) ||
                key.StageFraction.HasValue ||
                key.Detail.Length != 0 ||
                !string.Equals(
                    update.ProgressSummary,
                    InitialSummary,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Initial progress values are contradictory.",
                    nameof(update));
            }

            return;
        }

        int stageOrdinal = (int)key.Stage.Value;
        int expectedCompleted = key.StageStatus is
            ModelInspectionStageStatus.Completed or
            ModelInspectionStageStatus.Warning
                ? stageOrdinal
                : stageOrdinal - 1;
        if (key.StageCount != StageCount ||
            key.CompletedStageCount != expectedCompleted ||
            !string.Equals(
                update.ProgressSummary,
                $"{key.CompletedStageCount} of {StageCount} checks complete",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Progress stage, status and completed count disagree.",
                nameof(update));
        }
    }

    private void SetProgressSummary(string value)
    {
        if (string.Equals(_progressSummary, value, StringComparison.Ordinal))
        {
            return;
        }

        _progressSummary = value;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(ProgressSummary)));
    }

    private readonly record struct RowValues(
        InspectionContentStatus Status,
        string StatusText,
        bool IsActive,
        double? StageFraction,
        string Detail,
        Visibility DetailVisibility)
    {
        internal static RowValues Waiting { get; } = new(
            InspectionContentStatus.Waiting,
            "Waiting",
            IsActive: false,
            StageFraction: null,
            Detail: string.Empty,
            Visibility.Collapsed);

        internal static RowValues Passed { get; } = new(
            InspectionContentStatus.Passed,
            "Passed",
            IsActive: false,
            StageFraction: null,
            Detail: string.Empty,
            Visibility.Collapsed);
    }
}

internal readonly record struct InspectionProgressRowChange
{
    internal InspectionProgressRowChange(
        int rowIndex,
        bool statusChanged,
        bool detailChanged)
    {
        if (rowIndex is < 0 or >= 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        RowIndex = rowIndex;
        StatusChanged = statusChanged;
        DetailChanged = detailChanged;
    }

    internal int RowIndex { get; }

    internal bool StatusChanged { get; }

    internal bool DetailChanged { get; }
}

internal sealed class InspectionProgressRowsApplyResult
{
    internal static InspectionProgressRowsApplyResult Empty { get; } = new(
        [],
        progressSummaryChanged: false);

    internal InspectionProgressRowsApplyResult(
        IReadOnlyList<InspectionProgressRowChange> rowChanges,
        bool progressSummaryChanged)
    {
        ArgumentNullException.ThrowIfNull(rowChanges);
        InspectionProgressRowChange[] copy = rowChanges.ToArray();
        for (int index = 1; index < copy.Length; index++)
        {
            if (copy[index - 1].RowIndex >= copy[index].RowIndex)
            {
                throw new ArgumentException(
                    "Progress row changes must be unique and ordered.",
                    nameof(rowChanges));
            }
        }

        RowChanges = Array.AsReadOnly(copy);
        ProgressSummaryChanged = progressSummaryChanged;
    }

    internal IReadOnlyList<InspectionProgressRowChange> RowChanges { get; }

    internal bool ProgressSummaryChanged { get; }

    internal bool IsEmpty => RowChanges.Count == 0 && !ProgressSummaryChanged;
}
