using System;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationConfigurationCard : UserControl
{
    public OptimizationConfigurationCard()
    {
        InitializeComponent();
    }

    internal void Apply(OptimizationConfigurationPresentation configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        WeightsValue.Text = configuration.Weights;
        CacheValue.Text = configuration.Cache;
        BackendDeviceValue.Text = $"{configuration.Backend} on {configuration.Device}";
        ContextValue.Text = configuration.Context;
        OffloadValue.Text = configuration.Offload;
        FlashAttentionValue.Text = configuration.FlashAttention;
        PeakValue.Text = $"{configuration.PredictedPeak} of {configuration.SafeBudget}";
        HeadroomValue.Text = configuration.Headroom;
        EvidenceValue.Text = configuration.Evidence;
        ConfigurationNewModelCopyValue.Text = configuration.NewModelCopy;
        MemoryBreakdownValue.Text =
            $"Memory: model {configuration.ModelMemory}; cache {configuration.CacheMemory}; runtime {configuration.RuntimeMemory}.";
        TradeoffValue.Text = $"Tradeoff: {configuration.Tradeoff}";
        LimitationsValue.Text = $"Limitations: {configuration.Limitations}";
    }
}
