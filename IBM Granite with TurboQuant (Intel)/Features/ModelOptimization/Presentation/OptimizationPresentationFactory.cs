using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelOptimization.Presentation;

internal static class OptimizationPresentationFactory
{
    private static readonly OptimizationStage[] OrderedStages =
    [
        OptimizationStage.Preflight,
        OptimizationStage.PrepareStaging,
        OptimizationStage.Optimise,
        OptimizationStage.Validate,
        OptimizationStage.SmokeTest,
        OptimizationStage.Reinspect,
        OptimizationStage.Publish
    ];

    internal static OptimizationPresentationState Confirmation(
        OptimizationPreferenceSelection preference,
        OptimizationConfigurationPresentation configuration,
        Guid optimizationPlanId = default,
        string configurationSha256 = "") =>
        new(
            OptimizationPageStateKind.Confirming,
            "Review this configuration",
            configuration.ProducesPersistentArtifact
                ? "A new validated model copy will be created. Your original model stays unchanged."
                : "This saves a validated runtime setup. No new model file will be created.",
            OptimizationPresentationTone.Information,
            preference,
            OptimizationPreferenceLabelPolicy.GetLabel(preference),
            configuration,
            actions:
            [
                new(OptimizationCommand.Confirm, "Start optimisation", true),
                new(OptimizationCommand.BackToCompatibility, "Back", false)
            ],
            optimizationPlanId: optimizationPlanId,
            configurationSha256: configurationSha256);

    internal static OptimizationPresentationState Running(
        OptimizationPreferenceSelection preference,
        OptimizationConfigurationPresentation configuration,
        OptimizationStage activeStage,
        bool canCancel = true)
    {
        int activeIndex = Array.IndexOf(OrderedStages, activeStage);
        if (activeIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeStage));
        }

        List<OptimizationProgressRow> rows = [];
        for (int index = 0; index < OrderedStages.Length; index++)
        {
            OptimizationStage stage = OrderedStages[index];
            rows.Add(new(
                stage,
                Title(stage),
                Description(stage),
                index < activeIndex
                    ? OptimizationStageStatus.Completed
                    : index == activeIndex
                        ? OptimizationStageStatus.Active
                        : OptimizationStageStatus.Waiting));
        }

        return new(
            OptimizationPageStateKind.Running,
            "Optimising your model",
            "Keep this window open while the validated configuration is prepared.",
            OptimizationPresentationTone.Information,
            preference,
            OptimizationPreferenceLabelPolicy.GetLabel(preference),
            configuration,
            rows,
            canCancel
                ? [new(OptimizationCommand.Cancel, "Cancel", false)]
                : [],
            canCancel);
    }

    internal static OptimizationPresentationState Cancelled(
        OptimizationPreferenceSelection preference,
        OptimizationConfigurationPresentation configuration,
        OptimizationJourneyOrigin origin = OptimizationJourneyOrigin.Required) =>
        Terminal(
            OptimizationPageStateKind.Cancelled,
            "Optimisation cancelled",
            "Nothing was published. Your original model remains unchanged.",
            OptimizationPresentationTone.Neutral,
            preference,
            configuration,
            OptimizationSupportCode.CancelledByUser,
            RecoveryActions(origin));

    internal static OptimizationPresentationState ReplanRequired(
        OptimizationPreferenceSelection preference,
        OptimizationConfigurationPresentation configuration) =>
        Terminal(
            OptimizationPageStateKind.ReplanRequired,
            "Configuration needs another review",
            "The model, computer, or available optimisation capability changed before work began.",
            OptimizationPresentationTone.Warning,
            preference,
            configuration,
            OptimizationSupportCode.CapabilityDrift,
            [new(OptimizationCommand.BackToCompatibility, "Review again", true)]);

    internal static OptimizationPresentationState Failed(
        OptimizationPreferenceSelection preference,
        OptimizationConfigurationPresentation configuration,
        OptimizationJourneyOrigin origin = OptimizationJourneyOrigin.Required) =>
        Terminal(
            OptimizationPageStateKind.Failed,
            "Optimisation could not finish",
            "Nothing was published. You can retry the same validated configuration.",
            OptimizationPresentationTone.Error,
            preference,
            configuration,
            OptimizationSupportCode.ValidationFailed,
            RecoveryActions(origin));

    internal static OptimizationPresentationState Success(
        OptimizationPreferenceSelection preference,
        OptimizationConfigurationPresentation configuration,
        Guid optimizationPlanId = default,
        string configurationSha256 = "")
    {
        bool persistent = configuration.ProducesPersistentArtifact;
        return Terminal(
            persistent
                ? OptimizationPageStateKind.SucceededPersistent
                : OptimizationPageStateKind.SucceededRuntimeProfile,
            persistent ? "Your optimised model is ready" : "Your optimised setup is ready",
            persistent
                ? "The new model copy was validated and your original remains unchanged."
                : "This optimisation changes how the model runs; it does not create a new model file.",
            OptimizationPresentationTone.Success,
            preference,
            configuration,
            OptimizationSupportCode.None,
            [
                new(OptimizationCommand.Chat, "Chat with this model", true),
                persistent
                    ? new(OptimizationCommand.Save, "Save model to this computer", false)
                    : new(OptimizationCommand.Done, "Done", false)
            ],
            optimizationPlanId,
            configurationSha256);
    }

    private static IReadOnlyList<OptimizationActionPresentation> RecoveryActions(
        OptimizationJourneyOrigin origin) => origin == OptimizationJourneyOrigin.Optional
        ?
        [
            new(OptimizationCommand.Retry, "Try again", true),
            new(OptimizationCommand.BackToCompatibility, "Back", false),
            new(OptimizationCommand.ChatWithOriginal, "Chat with original model", false)
        ]
        :
        [
            new(OptimizationCommand.Retry, "Try again", true),
            new(OptimizationCommand.BackToCompatibility, "Back", false)
        ];

    private static OptimizationPresentationState Terminal(
        OptimizationPageStateKind kind,
        string title,
        string summary,
        OptimizationPresentationTone tone,
        OptimizationPreferenceSelection preference,
        OptimizationConfigurationPresentation configuration,
        OptimizationSupportCode supportCode,
        IReadOnlyList<OptimizationActionPresentation> actions,
        Guid optimizationPlanId = default,
        string configurationSha256 = "") =>
        new(
            kind,
            title,
            summary,
            tone,
            preference,
            OptimizationPreferenceLabelPolicy.GetLabel(preference),
            configuration,
            actions: actions,
            supportCode: supportCode,
            optimizationPlanId: optimizationPlanId,
            configurationSha256: configurationSha256);

    private static string Title(OptimizationStage stage) => stage switch
    {
        OptimizationStage.Preflight => "Preflight",
        OptimizationStage.PrepareStaging => "Prepare staging",
        OptimizationStage.Optimise => "Optimise",
        OptimizationStage.Validate => "Validate",
        OptimizationStage.SmokeTest => "Smoke test",
        OptimizationStage.Reinspect => "Reinspect",
        OptimizationStage.Publish => "Publish",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    private static string Description(OptimizationStage stage) => stage switch
    {
        OptimizationStage.Preflight => "Check the confirmed plan and current computer state.",
        OptimizationStage.PrepareStaging => "Prepare an isolated working area.",
        OptimizationStage.Optimise => "Create the confirmed model or runtime setup.",
        OptimizationStage.Validate => "Verify the complete output and its identity.",
        OptimizationStage.SmokeTest => "Run the route's bounded readiness check.",
        OptimizationStage.Reinspect => "Confirm the optimised result still matches the model.",
        OptimizationStage.Publish => "Make the validated result available atomically.",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };
}
