using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;

/// <summary>
/// Turns the engine's screen contract into words.
///
/// The engine deliberately emits codes and never sentences, so this is the only
/// place the product speaks. Three rules shape everything below.
///
/// First, never claim more certainty than exists. Every figure the engine
/// produces today rests on documented defaults rather than measurements, so the
/// page says "estimated" and carries a badge saying so. Calling an estimate a
/// measurement would be the one lie this whole design was built to avoid.
///
/// Second, "we could not tell you" is a different sentence from "we checked and
/// the answer is no". Screens 05 and 06 must never be confusable, because the
/// first means the user should change something and the second means the user
/// should give us something.
///
/// Third, a problem stated without a remedy is half an answer. Every blocking
/// finding carries a recovery line saying what would let the question be
/// answered.
/// </summary>
internal static class CompatibilityPresentationFactory
{
    private const string Title = "Model and hardware compatibility";

    internal static CompatibilityPresentation Analysing(int stageIndex)
    {
        string[] stages =
        [
            "Analysing what this model needs",
            "Checking which ways it could run",
            "Checking memory and safety limits",
            "Choosing the safest configuration"
        ];

        int clamped = stageIndex < 0 ? 0 : stageIndex > 3 ? 3 : stageIndex;

        return CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "This takes a few seconds and changes nothing on your computer.",
            Tone = CompatibilityOutcomeTone.Neutral,
            OutcomeTitle = stages[clamped],
            OutcomeDetail = $"Step {clamped + 1} of {stages.Length}.",
            OutcomeBadge = "WORKING",
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Cancel",
            SecondaryActionEnabled = true
        };
    }

    internal static CompatibilityPresentation Verifying(int stageIndex)
    {
        string[] stages =
        [
            "Checking the selected backend and device",
            "Loading the model you imported",
            "Generating a short response",
            "Recording what actually happened"
        ];

        int clamped = stageIndex < 0 ? 0 : stageIndex > 3 ? 3 : stageIndex;

        return CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "Trying the chosen configuration for real, so the result is measured rather than estimated.",
            Tone = CompatibilityOutcomeTone.Neutral,
            OutcomeTitle = stages[clamped],
            OutcomeDetail = $"Step {clamped + 1} of {stages.Length}.",
            OutcomeBadge = "VERIFYING",
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Cancel",
            SecondaryActionEnabled = true
        };
    }

    internal static CompatibilityPresentation VerifiedCompatible() =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "This configuration was tried on this computer and it worked.",
            Tone = CompatibilityOutcomeTone.Positive,
            OutcomeTitle = "Verified — this model runs on your computer",
            OutcomeDetail =
                "The model loaded and produced a response. This result was measured on "
                + "this machine rather than calculated.",
            OutcomeBadge = "MEASURED",
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = true,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };

    internal static CompatibilityPresentation VerificationFailed() =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "The chosen configuration did not complete a test run.",
            Tone = CompatibilityOutcomeTone.Blocking,
            OutcomeTitle = "This configuration did not run",
            OutcomeDetail =
                "The estimate said it should fit, but the test run did not finish. That "
                + "gap is worth trusting over the estimate.",
            OutcomeBadge = "NOT VERIFIED",
            Recoveries =
            [
                new CompatibilityRecovery(
                    "Try a smaller configuration",
                    "A shorter context or a more compact cache leaves more headroom than the "
                    + "estimate allowed for."),
                new CompatibilityRecovery(
                    "Close other applications",
                    "Memory available now is what decides this, and it changes as you open "
                    + "and close things.")
            ],
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };

    internal static CompatibilityPresentation Cancelled() =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "You stopped this check. Nothing was changed.",
            Tone = CompatibilityOutcomeTone.Neutral,
            OutcomeTitle = "Check stopped",
            OutcomeDetail = "No conclusion was reached, and nothing on your computer was altered.",
            OutcomeBadge = string.Empty,
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Check again",
            SecondaryActionEnabled = true
        };

    /// <summary>
    /// The terminal states the engine itself decides.
    /// </summary>
    internal static CompatibilityPresentation From(CompatibilityScreenModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.State switch
        {
            CompatibilityScreenState.EstimatedCompatible => EstimatedCompatible(model),
            CompatibilityScreenState.OptimisationRequired => OptimisationRequired(model),
            CompatibilityScreenState.NoEstimatedSafeConfiguration => NoSafeConfiguration(model),
            CompatibilityScreenState.Cancelled => Cancelled(),
            _ => NotEstablished(model)
        };
    }

    private static CompatibilityPresentation EstimatedCompatible(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "Based on what this model needs and what your computer has available.",
            Tone = CompatibilityOutcomeTone.Positive,
            OutcomeTitle = "Yes — this model should run",
            OutcomeDetail =
                "A configuration fits within a safe share of your available memory, with "
                + "room left over. This is an estimate, not a measurement.",
            OutcomeBadge = "ESTIMATED",
            DisclosureTitle = "How this was calculated",
            DisclosureDetail = DisclosureText(model),
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = model.ContinueEnabled,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true,
            Recoveries = BaselineRecoveries(model)
        };

    private static CompatibilityPresentation OptimisationRequired(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "It fits, but not with much to spare.",
            Tone = CompatibilityOutcomeTone.Caution,
            OutcomeTitle = "This should run, but the margin is narrow",
            OutcomeDetail =
                "Every configuration that fits does so with little headroom. Opening other "
                + "applications while it runs could push it over.",
            OutcomeBadge = "ESTIMATED",
            DisclosureTitle = "How this was calculated",
            DisclosureDetail = DisclosureText(model),
            Recoveries =
            [
                .. BaselineRecoveries(model),
                new CompatibilityRecovery(
                    "Consider a smaller setting",
                    "A shorter context or a more compact cache would leave more room for "
                    + "everything else you are running.")
            ],
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = model.ContinueEnabled,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };

    private static CompatibilityPresentation NoSafeConfiguration(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "Every way of running this model was checked against your available memory.",
            Tone = CompatibilityOutcomeTone.Blocking,
            OutcomeTitle = "No configuration fits safely",
            OutcomeDetail =
                "Each option needs more memory than can safely be spared right now. This is "
                + "a conclusion, not a failure to check.",
            OutcomeBadge = "ESTIMATED",
            DisclosureTitle = "How this was calculated",
            DisclosureDetail = DisclosureText(model),
            Recoveries =
            [
                new CompatibilityRecovery(
                    "Close other applications",
                    "What decides this is memory free right now, not memory installed."),
                new CompatibilityRecovery(
                    "Try a smaller model",
                    "A more compact version of the same model needs less memory for its "
                    + "weights and its cache."),
                .. BaselineRecoveries(model)
            ],
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };

    /// <summary>
    /// Screen 06, and the state this feature ships in until the hardware and
    /// model inspection handoffs exist. It has to be honest about knowing
    /// nothing while still being useful, which is why every finding becomes a
    /// recovery line rather than a bare error.
    /// </summary>
    private static CompatibilityPresentation NotEstablished(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "This question could not be answered yet.",
            Tone = CompatibilityOutcomeTone.Caution,
            OutcomeTitle = "Not enough information to answer this",
            OutcomeDetail =
                "No compatibility conclusion was reached. Nothing here says the model will "
                + "not run — only that it has not been established that it will.",
            OutcomeBadge = "NOT ESTABLISHED",
            DisclosureTitle = "What is missing",
            DisclosureDetail =
                "A conclusion needs three things: what the model requires, what this computer "
                + "has, and how much memory is free at the moment of the check. Any one of "
                + "them missing makes the answer a guess, and a guess about memory is how a "
                + "computer runs out of it.",
            Recoveries = Recoveries(model),
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Check again",
            SecondaryActionEnabled = true
        };

    /// <summary>
    /// One recovery line per blocking finding. A code with no line here would
    /// reach the user as silence, so the fallback is deliberately generic rather
    /// than absent.
    /// </summary>
    private static IReadOnlyList<CompatibilityRecovery> Recoveries(
        CompatibilityScreenModel model)
    {
        List<CompatibilityRecovery> recoveries = [];

        foreach (CompatibilityFindingView finding in model.Findings)
        {
            if (finding.Severity != FindingSeverity.Blocking)
            {
                continue;
            }

            recoveries.Add(finding.Code switch
            {
                CompatibilityFindingCode.ModelFactsUnavailable => new CompatibilityRecovery(
                    "Inspect the model first",
                    "What this model needs comes from inspecting it. That step has not "
                    + "produced a result this check can use."),

                CompatibilityFindingCode.HardwareFactsUnavailable => new CompatibilityRecovery(
                    "Check your hardware first",
                    "What your computer has comes from the hardware check. That step has "
                    + "not produced a result this check can use."),

                CompatibilityFindingCode.FreshMemoryUnavailable => new CompatibilityRecovery(
                    "Memory could not be read",
                    "How much memory is free right now could not be measured. An older "
                    + "figure is not used in its place, because free memory changes as "
                    + "you open and close applications."),

                CompatibilityFindingCode.NoCandidateCouldBeEstimated => new CompatibilityRecovery(
                    "This model's shape could not be read",
                    "Working out memory needs requires details such as the number of "
                    + "layers and attention heads. They were not available, so nothing "
                    + "was calculated."),

                CompatibilityFindingCode.SupportMatrixUnavailable => new CompatibilityRecovery(
                    "No supported configurations are listed",
                    "The list of configurations this product supports could not be loaded, "
                    + "so nothing could be offered."),

                CompatibilityFindingCode.NoCandidateGenerated => new CompatibilityRecovery(
                    "Nothing runnable was found",
                    "No supported configuration matched this model together with this "
                    + "computer."),

                CompatibilityFindingCode.PlanningContextNotEstablished => new CompatibilityRecovery(
                    "The model's context length is unknown",
                    "Memory needed for the conversation cache depends on context length. "
                    + "Without a trustworthy limit, no default is assumed."),

                CompatibilityFindingCode.HandoffClaimFailed => new CompatibilityRecovery(
                    "The earlier steps could not be read together",
                    "The model and hardware results must belong to the same session. They "
                    + "could not be claimed as a matching pair."),

                _ => new CompatibilityRecovery(
                    "Try the check again",
                    "Something needed for this answer was not available.")
            });
        }

        return recoveries;
    }

    /// <summary>
    /// Why the configuration the user already has is not among the options.
    /// The remedies genuinely differ, which is why the engine distinguishes
    /// them rather than reporting one generic absence.
    /// </summary>
    private static IReadOnlyList<CompatibilityRecovery> BaselineRecoveries(
        CompatibilityScreenModel model) =>
        model.BaselineExclusionReason switch
        {
            BaselineExclusionReason.BaselineEntryNotInstalled =>
            [
                new CompatibilityRecovery(
                    "Your current setup is not installed",
                    "The way this model is set up right now needs a component that is not "
                    + "installed on this computer.")
            ],
            BaselineExclusionReason.BaselineEntryRequiresExperimentalOptIn =>
            [
                new CompatibilityRecovery(
                    "Your current setup is experimental",
                    "It is available, but it has to be turned on deliberately because it "
                    + "may produce wrong output rather than simply failing.")
            ],
            BaselineExclusionReason.BaselineContextOutsideEntryBounds =>
            [
                new CompatibilityRecovery(
                    "Your current context length is out of range",
                    "The setup is supported, but not at the conversation length currently "
                    + "chosen.")
            ],
            BaselineExclusionReason.ModelContextLimitNotEstablished =>
            [
                new CompatibilityRecovery(
                    "The model's context limit is unknown",
                    "Nothing was offered rather than assuming a limit this model may not "
                    + "actually support.")
            ],
            BaselineExclusionReason.SupportMatrixUnavailable
                or BaselineExclusionReason.NoAdmittedEntryMatchesTheBaseline =>
            [
                new CompatibilityRecovery(
                    "Your current setup is not among the options",
                    "It is not one of the configurations this product supports on this "
                    + "computer.")
            ],
            _ => []
        };

    private static string DisclosureText(CompatibilityScreenModel model)
    {
        bool uncalibrated = model.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.UncalibratedEstimate);

        string basis =
            "Memory needed is worked out from the model's size and shape, the length of "
            + "conversation you chose, and the way it would run. That total is compared "
            + "against the memory free right now, less a reserve for Windows and for "
            + "everything else you have open.";

        return uncalibrated
            ? basis
            + " The reserves and margins used here are documented defaults rather than "
            + "figures measured on machines like yours, so this is labelled an estimate."
            : basis;
    }
}
