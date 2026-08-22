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
/// The engine emits codes and never sentences, so this is the only place the
/// product speaks. Four rules shape everything below.
///
/// Plain words. Someone deciding whether to run a model should not have to
/// decode "margin", "headroom", "configuration" or "established". They get
/// "spare", "setup", and short sentences.
///
/// Never claim more certainty than exists. Every figure today rests on sensible
/// defaults rather than measurements, so the page says estimate and shows a
/// badge saying so. Calling an estimate a test would be the one lie this design
/// was built to avoid.
///
/// "We can't tell you" is a different sentence from "we checked and the answer
/// is no". Those two screens must never be confusable: the first means the user
/// should give us something, the second means the user should change something.
///
/// A problem stated without a remedy is half an answer, so every blocking
/// finding carries a line saying what would help.
/// </summary>
internal static class CompatibilityPresentationFactory
{
    private const string Title = "Model and hardware compatibility";

    internal static CompatibilityPresentation Analysing(int stageIndex)
    {
        string[] stages =
        [
            "Working out what this model needs",
            "Checking the ways it could run",
            "Checking memory and safety limits",
            "Picking the safest setup"
        ];

        int clamped = Clamp(stageIndex, stages.Length);

        return CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "This takes a few seconds. Nothing on your computer changes.",
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
            "Checking the chosen backend and device",
            "Loading the model you added",
            "Writing a short reply",
            "Saving what happened"
        ];

        int clamped = Clamp(stageIndex, stages.Length);

        return CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "We're trying the setup for real, so the answer is tested rather than estimated.",
            Tone = CompatibilityOutcomeTone.Neutral,
            OutcomeTitle = stages[clamped],
            OutcomeDetail = $"Step {clamped + 1} of {stages.Length}.",
            OutcomeBadge = "TESTING",
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
            PageLede = "We tried this setup on your computer and it worked.",
            Tone = CompatibilityOutcomeTone.Positive,
            OutcomeTitle = "Tested — this model runs on your computer",
            OutcomeDetail =
                "The model loaded and wrote a reply. We tested this on your computer "
                + "instead of working it out on paper.",
            OutcomeBadge = "TESTED",
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = true,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };

    internal static CompatibilityPresentation VerificationFailed() =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "The setup we picked didn't finish a test run.",
            Tone = CompatibilityOutcomeTone.Blocking,
            OutcomeTitle = "This setup didn't run",
            OutcomeDetail =
                "We expected it to fit, but the test didn't finish. Trust the test over "
                + "our estimate.",
            OutcomeBadge = "DIDN'T RUN",
            Recoveries =
            [
                new CompatibilityRecovery(
                    "Try a smaller setup",
                    "A shorter context, or a smaller way of storing it, leaves more memory "
                    + "spare than we allowed for."),
                new CompatibilityRecovery(
                    "Close other apps",
                    "What matters is the memory free right now, and that changes as you "
                    + "open and close things.")
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
            OutcomeDetail = "We didn't reach an answer, and nothing on your computer was changed.",
            OutcomeBadge = string.Empty,
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Check again",
            SecondaryActionEnabled = true
        };

    /// <summary>
    /// The states the engine itself decides.
    /// </summary>
    internal static CompatibilityPresentation From(CompatibilityScreenModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.State switch
        {
            CompatibilityScreenState.EstimatedCompatible =>
                WithSetup(EstimatedCompatible(model), model),
            CompatibilityScreenState.OptimisationRequired =>
                WithSetup(OnlyJustFits(model), model),
            CompatibilityScreenState.NoEstimatedSafeConfiguration =>
                WithSetup(NothingFits(model), model),
            CompatibilityScreenState.Cancelled => Cancelled(),
            _ => NotEstablished(model)
        };
    }

    /// <summary>
    /// Adds the figures behind a verdict.
    ///
    /// A concluded screen without them states a conclusion and hides its
    /// working, which is exactly the shape of claim this design set out not to
    /// make. When no setup was evaluated the cards stay empty rather than
    /// filling with zeros, because a zero here would read as a model that costs
    /// nothing.
    /// </summary>
    private static CompatibilityPresentation WithSetup(
        CompatibilityPresentation presentation, CompatibilityScreenModel model) =>
        model.Setup is not { } setup
            ? presentation
            : presentation with
            {
                Facts = CompatibilitySetupNarrative.Facts(setup),
                Budget = CompatibilitySetupNarrative.Budget(setup),
                RuntimeCardTitle = "What would run",
                RuntimeRows = CompatibilitySetupNarrative.RuntimeRows(setup),
                ChecksCardTitle = "What we checked",
                CheckRows = CompatibilitySetupNarrative.CheckRows(setup)
            };

    private static CompatibilityPresentation EstimatedCompatible(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "Based on what this model needs and what your computer has free.",
            Tone = CompatibilityOutcomeTone.Positive,
            OutcomeTitle = "Yes — this model should run",
            OutcomeDetail =
                "One setup fits in the memory you have free, with room to spare. This is "
                + "our best estimate, not a test.",
            OutcomeBadge = "ESTIMATE",
            DisclosureTitle = "How we worked this out",
            DisclosureDetail = DisclosureText(model),
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = model.ContinueEnabled,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true,
            Recoveries = BaselineRecoveries(model)
        };

    private static CompatibilityPresentation OnlyJustFits(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "It fits, but only just.",
            Tone = CompatibilityOutcomeTone.Caution,
            OutcomeTitle = "This should run, but there's very little memory spare",
            OutcomeDetail =
                "Every setup that fits leaves almost nothing free. If you open other apps "
                + "while it runs, it could run out of memory.",
            OutcomeBadge = "ESTIMATE",
            DisclosureTitle = "How we worked this out",
            DisclosureDetail = DisclosureText(model),
            Recoveries =
            [
                new CompatibilityRecovery(
                    "Try a shorter context",
                    "The longer the context, the more memory it needs while it runs."),
                .. BaselineRecoveries(model)
            ],
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = model.ContinueEnabled,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };

    private static CompatibilityPresentation NothingFits(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "We checked every way of running this against your free memory.",
            Tone = CompatibilityOutcomeTone.Blocking,
            OutcomeTitle = "No setup fits safely",
            OutcomeDetail =
                "Every way of running this needs more memory than you can safely spare "
                + "right now. We checked them all — this is an answer, not a failure to "
                + "check.",
            OutcomeBadge = "ESTIMATE",
            DisclosureTitle = "How we worked this out",
            DisclosureDetail = DisclosureText(model),
            Recoveries =
            [
                new CompatibilityRecovery(
                    "Close other apps",
                    "What matters is the memory free right now, not how much your computer "
                    + "has in total."),
                new CompatibilityRecovery(
                    "Try a smaller model",
                    "A smaller version of the same model needs less memory to run."),
                .. BaselineRecoveries(model)
            ],
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true
        };

    /// <summary>
    /// The state this feature ships in until the hardware and model checks hand
    /// it something to work with. It has to admit it knows nothing while still
    /// being useful, which is why every finding becomes a line about what would
    /// help rather than a bare error.
    /// </summary>
    private static CompatibilityPresentation NotEstablished(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "We couldn't answer this yet.",
            Tone = CompatibilityOutcomeTone.Caution,
            OutcomeTitle = "We can't answer this yet",
            OutcomeDetail =
                "We didn't reach an answer. This doesn't mean the model won't run — only "
                + "that we haven't been able to show that it will.",
            OutcomeBadge = "NO ANSWER YET",
            DisclosureTitle = "What's missing",
            DisclosureDetail =
                "To answer this we need three things: what the model needs, what your "
                + "computer has, and how much memory is free at the moment we check. If "
                + "any one is missing, the answer would be a guess — and guessing about "
                + "memory is how a computer runs out of it.",
            Recoveries = Recoveries(model),
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Check again",
            SecondaryActionEnabled = true
        };

    /// <summary>
    /// One line per blocking finding. A code with no line here would reach the
    /// user as silence, so the fallback is deliberately generic rather than
    /// missing.
    /// </summary>
    private static IReadOnlyList<CompatibilityRecovery> Recoveries(CompatibilityScreenModel model)
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
                    "Check the model first",
                    "We find out what a model needs by looking inside it. That step hasn't "
                    + "given us anything to use yet."),

                CompatibilityFindingCode.HardwareFactsUnavailable => new CompatibilityRecovery(
                    "Check your computer first",
                    "We find out what your computer has from the hardware check. That step "
                    + "hasn't given us anything to use yet."),

                CompatibilityFindingCode.FreshMemoryUnavailable => new CompatibilityRecovery(
                    "We couldn't read your free memory",
                    "We need to know how much is free right now. We won't use an older "
                    + "number instead, because it changes as you open and close apps."),

                CompatibilityFindingCode.NoCandidateCouldBeEstimated => new CompatibilityRecovery(
                    "We couldn't read this model's shape",
                    "To work out memory we need details like how many layers it has. They "
                    + "weren't there, so we didn't work anything out."),

                CompatibilityFindingCode.SupportMatrixUnavailable => new CompatibilityRecovery(
                    "We couldn't load the list of supported setups",
                    "Without that list there's nothing we can offer."),

                CompatibilityFindingCode.NoCandidateGenerated => new CompatibilityRecovery(
                    "Nothing here can run this model",
                    "None of the setups we support work for this model on this computer."),

                CompatibilityFindingCode.PlanningContextNotEstablished => new CompatibilityRecovery(
                    "We don't know this model's context limit",
                    "Memory for the context depends on it. We won't guess a number the "
                    + "model may not handle."),

                CompatibilityFindingCode.HandoffClaimFailed => new CompatibilityRecovery(
                    "The earlier steps don't match up",
                    "The model check and the hardware check have to come from the same "
                    + "session. We couldn't pair them."),

                CompatibilityFindingCode.UnexpectedFailure => new CompatibilityRecovery(
                    "Something went wrong on our side",
                    "This is our fault, not a problem with your computer or your model. "
                    + "Nothing was changed. Trying again is worth doing."),

                _ => new CompatibilityRecovery(
                    "Try the check again",
                    "Something we needed wasn't there.")
            });
        }

        return recoveries;
    }

    /// <summary>
    /// Why the setup the user already has isn't among the options. The remedies
    /// genuinely differ, which is why the engine tells them apart instead of
    /// reporting one vague absence.
    /// </summary>
    private static IReadOnlyList<CompatibilityRecovery> BaselineRecoveries(
        CompatibilityScreenModel model) =>
        model.BaselineExclusionReason switch
        {
            BaselineExclusionReason.BaselineEntryNotInstalled =>
            [
                new CompatibilityRecovery(
                    "Your current setup isn't installed",
                    "The way this model is set up right now needs something that isn't "
                    + "installed on this computer.")
            ],
            BaselineExclusionReason.BaselineEntryRequiresExperimentalOptIn =>
            [
                new CompatibilityRecovery(
                    "Your current setup is experimental",
                    "It's here, but you have to switch it on yourself. Experimental setups "
                    + "can give wrong answers rather than just failing.")
            ],
            BaselineExclusionReason.BaselineContextOutsideEntryBounds =>
            [
                new CompatibilityRecovery(
                    "Your current context is out of range",
                    "The setup works, but not at the context length you've picked.")
            ],
            BaselineExclusionReason.ModelContextLimitNotEstablished =>
            [
                new CompatibilityRecovery(
                    "We don't know this model's limit",
                    "We'd rather offer nothing than guess a limit the model may not handle.")
            ],
            BaselineExclusionReason.BaselineEntrySupportStateUnknown =>
            [
                new CompatibilityRecovery(
                    "We couldn't check your current setup",
                    "Your setup is one we know about, but we couldn't tell whether what "
                    + "it needs is installed. That's not the same as it being missing.")
            ],
            BaselineExclusionReason.SupportMatrixUnavailable
                or BaselineExclusionReason.NoAdmittedEntryMatchesTheBaseline =>
            [
                new CompatibilityRecovery(
                    "Your current setup isn't one of the options",
                    "It isn't one of the setups we support on this computer.")
            ],
            _ => []
        };

    private static string DisclosureText(CompatibilityScreenModel model)
    {
        string basis =
            "We work out the memory needed from the model's size and shape, the "
            + "context length you picked, and how it would run. We compare that with the memory "
            + "free right now, minus some we set aside for Windows and your other apps.";

        bool uncalibrated = model.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.UncalibratedEstimate);

        return uncalibrated
            ? basis
            + " The amounts we set aside are sensible defaults, not numbers measured on "
            + "computers like yours. That's why we call this an estimate."
            : basis;
    }

    private static int Clamp(int value, int length) =>
        value < 0 ? 0 : value >= length ? length - 1 : value;
}
