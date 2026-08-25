using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;

public sealed partial class HardwareInspectionProgressCard : UserControl
{
    public HardwareInspectionProgressCard()
    {
        InitializeComponent();
    }

    internal ObservableCollection<HardwareInspectionProgressRowViewData> Rows { get; } = [];

    internal HardwareInspectionPresentationState? CurrentState { get; private set; }

    internal void Apply(HardwareInspectionPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Kind != HardwareInspectionPresentationKind.Active)
        {
            throw new ArgumentException("Progress card accepts only Active presentation state.", nameof(state));
        }

        if (state.StageRows.Count != 7
            || state.StageRows.Count(row => row.State == HardwareInspectionStageRowState.Active) != 1)
        {
            throw new ArgumentException("Active presentation must contain exactly seven rows and one active row.", nameof(state));
        }

        CurrentState = state;
        KickerTextBlock.Text = state.Kicker;
        TitleTextBlock.Text = state.Title;
        BodyTextBlock.Text = state.Body;
        CountTextBlock.Text = $"{state.CompletedStageCount} of 7";
        CountLabelTextBlock.Text = "checks complete";

        Rows.Clear();
        for (int index = 0; index < state.StageRows.Count; index++)
        {
            Rows.Add(new HardwareInspectionProgressRowViewData(index + 1, state.StageRows[index]));
        }
    }
}

internal sealed class HardwareInspectionProgressRowViewData
{
    internal HardwareInspectionProgressRowViewData(int number, HardwareInspectionStageRow row)
    {
        Number = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Title = row.Title;
        Sentence = row.Sentence;
        AccessibleName = row.AccessibleName;
        Status = row.State switch
        {
            HardwareInspectionStageRowState.Complete => "Complete",
            HardwareInspectionStageRowState.Active => "Active",
            HardwareInspectionStageRowState.Waiting => "Waiting",
            _ => throw new ArgumentOutOfRangeException(nameof(row)),
        };
        CompletedVisibility = row.State == HardwareInspectionStageRowState.Complete
            ? Visibility.Visible
            : Visibility.Collapsed;
        ActiveVisibility = row.State == HardwareInspectionStageRowState.Active
            ? Visibility.Visible
            : Visibility.Collapsed;
        WaitingVisibility = row.State == HardwareInspectionStageRowState.Waiting
            ? Visibility.Visible
            : Visibility.Collapsed;
        IsActive = row.State == HardwareInspectionStageRowState.Active;
    }

    public string Number { get; }
    public string Title { get; }
    public string Sentence { get; }
    public string AccessibleName { get; }
    public string Status { get; }
    public Visibility CompletedVisibility { get; }
    public Visibility ActiveVisibility { get; }
    public Visibility WaitingVisibility { get; }
    public bool IsActive { get; }
}
