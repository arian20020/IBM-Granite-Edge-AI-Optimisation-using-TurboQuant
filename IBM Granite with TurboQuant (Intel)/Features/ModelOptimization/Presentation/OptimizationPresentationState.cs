using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Presentation;

internal enum OptimizationPageStateKind
{
    Confirming,
    Running,
    Cancelled,
    ReplanRequired,
    Failed,
    SucceededPersistent,
    SucceededRuntimeProfile
}

internal enum OptimizationStage
{
    Preflight,
    PrepareStaging,
    Optimise,
    Validate,
    SmokeTest,
    Reinspect,
    Publish
}

internal enum OptimizationStageStatus
{
    Waiting,
    Active,
    Completed,
    NotApplicable
}

internal enum OptimizationPresentationTone
{
    Neutral,
    Information,
    Success,
    Warning,
    Error
}

internal sealed record OptimizationConfigurationPresentation(
    string Weights,
    string Cache,
    string Backend,
    string Device,
    string Context,
    string Offload,
    string FlashAttention,
    string ModelMemory,
    string CacheMemory,
    string RuntimeMemory,
    string PredictedPeak,
    string SafeBudget,
    string Headroom,
    string Evidence,
    string Tradeoff,
    string Limitations,
    bool ProducesPersistentArtifact,
    string Output,
    string WorkingDisk,
    string FinalDisk,
    string RouteValidation)
{
    internal string NewModelCopy => ProducesPersistentArtifact ? "Yes" : "No";
}

internal sealed record OptimizationProgressRow(
    OptimizationStage Stage,
    string Title,
    string Description,
    OptimizationStageStatus Status)
{
    internal bool IsActive => Status == OptimizationStageStatus.Active;
}

internal sealed record OptimizationActionPresentation(
    OptimizationCommand Command,
    string Text,
    bool IsPrimary,
    bool IsEnabled = true);

internal sealed record OptimizationPresentationState
{
    internal OptimizationPresentationState(
        OptimizationPageStateKind kind,
        string title,
        string summary,
        OptimizationPresentationTone tone,
        OptimizationPreferenceSelection? preference,
        string preferenceLabel,
        OptimizationConfigurationPresentation configuration,
        IEnumerable<OptimizationProgressRow>? progressRows = null,
        IEnumerable<OptimizationActionPresentation>? actions = null,
        bool canCancel = false,
        OptimizationSupportCode supportCode = OptimizationSupportCode.None,
        Guid optimizationPlanId = default,
        string configurationSha256 = "")
    {
        Kind = kind;
        Title = RequireCopy(title, nameof(title));
        Summary = RequireCopy(summary, nameof(summary));
        Tone = tone;
        Preference = preference;
        PreferenceLabel = RequireCopy(preferenceLabel, nameof(preferenceLabel));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ProgressRows = Array.AsReadOnly((progressRows ?? []).ToArray());
        Actions = Array.AsReadOnly((actions ?? []).ToArray());
        CanCancel = canCancel;
        SupportCode = supportCode;
        OptimizationPlanId = optimizationPlanId;
        ConfigurationSha256 = configurationSha256;

        bool hasPlanIdentity = optimizationPlanId != Guid.Empty;
        bool hasConfigurationIdentity = configurationSha256.Length == 64
            && configurationSha256.All(character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f');
        if (hasPlanIdentity != hasConfigurationIdentity)
        {
            throw new ArgumentException(
                "Plan and configuration identities must be present together.");
        }

        if (kind == OptimizationPageStateKind.Running && ProgressRows.Count != 7)
        {
            throw new ArgumentException(
                "Running presentation state requires the complete seven-stage journey.",
                nameof(progressRows));
        }
    }

    internal OptimizationPageStateKind Kind { get; }

    internal string Title { get; }

    internal string Summary { get; }

    internal OptimizationPresentationTone Tone { get; }

    internal OptimizationPreferenceSelection? Preference { get; }

    internal string PreferenceLabel { get; }

    internal OptimizationConfigurationPresentation Configuration { get; }

    internal IReadOnlyList<OptimizationProgressRow> ProgressRows { get; }

    internal IReadOnlyList<OptimizationActionPresentation> Actions { get; }

    internal bool CanCancel { get; }

    internal OptimizationSupportCode SupportCode { get; }

    internal Guid OptimizationPlanId { get; }

    internal string ConfigurationSha256 { get; }

    private static string RequireCopy(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Visible presentation copy must not be blank.", parameter);
        }

        if (value.Length > 512 || value.Contains('\\') || value.Contains('/'))
        {
            throw new ArgumentException(
                "Visible presentation copy must be bounded and must not expose paths.",
                parameter);
        }

        return value;
    }
}
