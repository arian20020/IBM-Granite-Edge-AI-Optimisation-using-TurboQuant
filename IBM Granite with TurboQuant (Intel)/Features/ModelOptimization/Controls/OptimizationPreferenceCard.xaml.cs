using System;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationPreferenceCard : UserControl
{
    private bool _applying;

    public OptimizationPreferenceCard()
    {
        InitializeComponent();
    }

    internal event EventHandler<OptimizationPreferenceSelection>? PreferenceChanged;

    internal void Apply(OptimizationPreferenceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        _applying = true;
        try
        {
            bool automatic = selection.Kind == OptimizationPreferenceKind.Automatic;
            AutomaticOption.IsChecked = automatic;
            ManualOption.IsChecked = !automatic;
            PreferenceSlider.IsEnabled = !automatic;

            if (selection.PreferenceValue is int value)
            {
                PreferenceSlider.Value = value;
            }

            CurrentPreferenceLabel.Text = OptimizationPreferenceLabelPolicy.GetLabel(selection);
        }
        finally
        {
            _applying = false;
        }
    }

    private void OnAutomaticChecked(object sender, RoutedEventArgs args)
    {
        if (!_applying)
        {
            PreferenceSlider.IsEnabled = false;
            OptimizationPreferenceSelection selection = OptimizationPreferenceSelection.Automatic();
            CurrentPreferenceLabel.Text = OptimizationPreferenceLabelPolicy.GetLabel(selection);
            PreferenceChanged?.Invoke(this, selection);
        }
    }

    private void OnManualChecked(object sender, RoutedEventArgs args)
    {
        if (!_applying)
        {
            PreferenceSlider.IsEnabled = true;
            RaiseManualPreference();
        }
    }

    private void OnSliderValueChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (!_applying && ManualOption.IsChecked == true)
        {
            RaiseManualPreference();
        }
    }

    private void RaiseManualPreference()
    {
        OptimizationPreferenceSelection selection =
            OptimizationPreferenceSelection.Manual((int)Math.Round(PreferenceSlider.Value));
        CurrentPreferenceLabel.Text = OptimizationPreferenceLabelPolicy.GetLabel(selection);
        PreferenceChanged?.Invoke(this, selection);
    }
}
