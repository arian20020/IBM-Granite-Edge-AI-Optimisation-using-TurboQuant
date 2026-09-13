using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;

/// <summary>
/// turns the engine's screen contract into words
///
/// the engine emits codes and never sentences, so this is the only place the
/// product speaks. four rules shape everything below
///
/// plain words. someone deciding whether to run a model should not have to
/// decode "margin", "headroom", "configuration" or "established". they get
/// "spare", "setup", and short sentences
///
/// never claim more certainty than exists. every figure today rests on sensible
/// defaults rather than measurements, so the page says estimate and shows a
/// badge saying so. calling an estimate a test would be the one lie this design
/// was built to avoid
///
/// "We can't tell you" is a different sentence from "we checked and the answer
/// is no". Those two screens must never be confusable: the first means the user
/// should give us something, the second means the user should change something.
///
/// a problem stated without a remedy is half an answer, so every blocking
/// finding carries a line saying what would help
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
            SecondaryActionEnabled = true,
            SecondaryActionKind = CompatibilitySecondaryActionKind.Cancel
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
            SecondaryActionEnabled = true,
            SecondaryActionKind = CompatibilitySecondaryActionKind.Cancel
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
            SecondaryActionEnabled = true,
            SecondaryActionKind = CompatibilitySecondaryActionKind.Retry
        };

    internal static CompatibilityPresentation OperationalFailure() =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "The check stopped safely before reaching an answer.",
            Tone = CompatibilityOutcomeTone.Blocking,
            OutcomeTitle = "The compatibility check could not finish",
            OutcomeDetail = "Nothing was changed. You can safely check again.",
            OutcomeBadge = string.Empty,
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Check again",
            SecondaryActionEnabled = true,
            SecondaryActionKind = CompatibilitySecondaryActionKind.Retry
        };

    /// <summary>
    /// the states the engine itself decides
    /// </summary>
    internal static CompatibilityPresentation From(CompatibilityScreenModel model) =>
        From(model, OptimizationPreferenceSelection.Automatic());

    internal static CompatibilityPresentation From(
        CompatibilityEvaluation evaluation) =>
        From(evaluation, OptimizationPreferenceSelection.Automatic());

    internal static CompatibilityPresentation From(
        CompatibilityEvaluation evaluation,
        OptimizationPreferenceSelection preference)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        CompatibilityOptimizationView? memoryOptimization =
            evaluation.Screen.State == CompatibilityScreenState.EstimatedCompatible
                ? evaluation.OptionalOptimization
                : evaluation.Screen.Optimization;
        CompatibilityPresentation presentation = WithMemoryClarity(
            WithMemoryOverview(
                WithMachineMemory(
                    From(evaluation.Screen, preference),
                    evaluation.MachineMemory),
                evaluation.Screen,
                evaluation.MachineMemory,
                memoryOptimization),
            evaluation.Screen,
            evaluation.MachineMemory);
        if (evaluation.Screen.State == CompatibilityScreenState.EstimatedCompatible
            && !HasCurrentRouteOptionalOptimization(evaluation)
            && evaluation.Screen.Optimization is null)
        {
            presentation = presentation with
            {
                OutcomeDetail = presentation.OutcomeDetail
                    + " No verified optimisation option is available for this model. The current setup is estimated to fit."
            };
        }
        return presentation;
    }

    private static bool HasCurrentRouteOptionalOptimization(CompatibilityEvaluation evaluation)
    {
        if (evaluation.OptionalOptimization is not { } optional)
        {
            return false;
        }
        OptimizationRoute? route = evaluation.Screen.Setup?.Route switch
        {
            RuntimeRouteId.LlamaCpp => OptimizationRoute.Gguf,
            RuntimeRouteId.OpenVinoGenAi => OptimizationRoute.OpenVino,
            _ => null
        };
        return route is null
            || optional.Modes.Any(mode => mode.Route == route)
            || optional.ExactSafeModes.Any(item => item.Mode.Route == route)
            || optional.SafeSliderModes.Any(item => item.Mode.Route == route);
    }

    internal static CompatibilityPresentation From(
        CompatibilityScreenModel model,
        OptimizationPreferenceSelection preference)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(preference);

        return model.State switch
        {
            CompatibilityScreenState.EstimatedCompatible =>
                WithSetup(EstimatedCompatible(model), model),
            CompatibilityScreenState.OptimisationRequired =>
                OptimizationRequired(model, preference),
            CompatibilityScreenState.NoEstimatedSafeConfiguration =>
                WithSetup(NothingFits(model), model),
            CompatibilityScreenState.Cancelled => Cancelled(),
            _ => NotEstablished(model)
        };
    }

    /// <summary>
    /// adds the figures behind a verdict
    ///
    /// a concluded screen without them states a conclusion and hides its
    /// working, which is exactly the shape of claim this design set out not to
    /// make. when no setup was evaluated the cards stay empty rather than
    /// filling with zeros, because a zero here would read as a model that costs
    /// nothing
    /// </summary>
    private static CompatibilityPresentation WithSetup(
        CompatibilityPresentation presentation, CompatibilityScreenModel model)
    {
        if (model.Setup is not { } setup)
        {
            return presentation;
        }

        string outcomeDetail = model.State ==
            CompatibilityScreenState.NoEstimatedSafeConfiguration
                ? NoFitMemoryDetail(model, setup)
                : presentation.OutcomeDetail;

        return presentation with
        {
            OutcomeDetail = outcomeDetail,
            Facts = CompatibilitySetupNarrative.Facts(setup),
            Budget = CompatibilitySetupNarrative.Budget(setup),
            EstimateSummary = CompatibilitySetupNarrative.EstimateSummary(setup),
            RuntimeCardTitle = "What would run",
            RuntimeRows = CompatibilitySetupNarrative.RuntimeRows(setup),
            ChecksCardTitle = "What we checked",
            CheckRows = CompatibilitySetupNarrative.CheckRows(setup)
        };
    }

    private static string NoFitMemoryDetail(
        CompatibilityScreenModel model,
        CompatibilitySetupView currentSetup)
    {
        string current = CompatibilityBudget.Describe(currentSetup.RequiredBytes);
        string safe = CompatibilityBudget.Describe(currentSetup.SafeBudgetBytes);
        if (model.SmallestOptimizedRequiredBytes is { } optimized)
        {
            return "The current model needs about " + current + " of RAM"
                + ". The smallest evaluated optimised setup needs about "
                + CompatibilityBudget.Describe(optimized)
                + ", but only " + safe
                + " is available within the safety limit right now. Optimisation is "
                + "available when enough memory is free; close unused applications "
                + "and browser tabs, then restart hardware inspection to check again.";
        }

        ulong shortfall = currentSetup.RequiredBytes > currentSetup.SafeBudgetBytes
            ? currentSetup.RequiredBytes - currentSetup.SafeBudgetBytes : 0;
        string additional = Math.Ceiling(shortfall / (1024m * 1024m))
            .ToString("N0", System.Globalization.CultureInfo.CurrentCulture) + " MB";
        return "The current model needs about " + current
            + " of RAM, but only " + safe
            + " is available to it after the safety reserve. No smaller setup has been verified for this model. "
            + "Free up about " + additional + " more RAM by closing unused applications and browser tabs, "
            + "then restart hardware inspection to check again. The reserve and available memory can change. "
            + "Free disk space is not the same as free RAM.";
    }

    private static CompatibilityPresentation OptimizationRequired(
        CompatibilityScreenModel model,
        OptimizationPreferenceSelection preference)
    {
        if (model.CurrentSetup is not { } current
            || model.Optimization is not { } optimization)
        {
            return NotEstablished(model);
        }

        return OptimizationSelection(
            model,
            current,
            optimization,
            preference,
            isRequired: true);
    }

    internal static CompatibilityPresentation OptionalOptimization(
        CompatibilityScreenModel model,
        CompatibilityOptimizationView optimization,
        OptimizationPreferenceSelection preference)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(optimization);
        ArgumentNullException.ThrowIfNull(preference);
        return model.State == CompatibilityScreenState.EstimatedCompatible
            && model.Setup is { } current
            ? OptimizationSelection(
                model,
                current,
                optimization,
                preference,
                isRequired: false)
            : NotEstablished(model);
    }

    internal static CompatibilityPresentation OptionalOptimization(
        CompatibilityEvaluation evaluation,
        CompatibilityOptimizationView optimization,
        OptimizationPreferenceSelection preference)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return WithMemoryOverview(
            WithMachineMemory(
                OptionalOptimization(evaluation.Screen, optimization, preference),
                evaluation.MachineMemory),
            evaluation.Screen,
            evaluation.MachineMemory,
            optimization);
    }

    private static CompatibilityPresentation WithMemoryOverview(
        CompatibilityPresentation presentation,
        CompatibilityScreenModel model,
        CompatibilityMachineMemory? memory,
        CompatibilityOptimizationView? optimization)
    {
        if (memory is null)
        {
            return presentation;
        }

        CompatibilitySetupView? current = model.State switch
        {
            CompatibilityScreenState.OptimisationRequired => model.CurrentSetup,
            CompatibilityScreenState.EstimatedCompatible => model.Setup,
            _ => null
        };
        if (current is null
            || current.SystemSharedSafeBudgetBytes != memory.SafeModelBudgetBytes)
        {
            return presentation;
        }

        bool currentFit = model.State == CompatibilityScreenState.EstimatedCompatible;
        if (!currentFit
            && (presentation.Optimization is null || optimization is null))
        {
            return presentation;
        }

        ulong? minimum;
        string? minimumLabel;
        if (currentFit)
        {
            OptimizationRoute currentRoute = current.Route == RuntimeRouteId.LlamaCpp
                ? OptimizationRoute.Gguf
                : OptimizationRoute.OpenVino;
            minimumLabel = "Smallest offered setup needs";
            minimum = optimization?.SafeSliderModes
                .Where(item => !item.Mode.IsExperimental
                    && item.Mode.Route == currentRoute
                    && item.Mode.SystemSharedSafeBudgetBytes > 0
                    && item.Mode.SystemSharedSafeBudgetBytes
                        <= memory.SafeModelBudgetBytes
                    && item.Mode.SystemSharedPredictedPeakBytes > 0
                    && item.Mode.ExpectedQualityLevel is
                        OptimizationQualityLevel.Acceptable
                        or OptimizationQualityLevel.Fair
                        or OptimizationQualityLevel.Good
                        or OptimizationQualityLevel.VeryGood
                        or OptimizationQualityLevel.Excellent)
                .Select(item => item.Mode.SystemSharedPredictedPeakBytes)
                .DefaultIfEmpty()
                .Min();
            if (minimum == 0)
            {
                minimum = null;
            }
            if (minimum is null)
            {
                minimumLabel = null;
            }
        }
        else
        {
            minimum = model.SmallestOptimizedRequiredBytes;
            minimumLabel = minimum is null
                ? null
                : "Smallest acceptable format needs";
            if (minimum is null)
            {
                OptimizationRoute currentRoute = current.Route == RuntimeRouteId.LlamaCpp
                    ? OptimizationRoute.Gguf
                    : OptimizationRoute.OpenVino;
                ulong[] offered =
                [
                    .. optimization!.Modes
                        .Where(mode => mode.Route == currentRoute
                            && mode.SystemSharedSafeBudgetBytes > 0
                            && mode.SystemSharedSafeBudgetBytes
                                <= memory.SafeModelBudgetBytes
                            && mode.ExpectedQualityLevel is
                                OptimizationQualityLevel.Acceptable
                                or OptimizationQualityLevel.Fair
                                or OptimizationQualityLevel.Good
                                or OptimizationQualityLevel.VeryGood
                                or OptimizationQualityLevel.Excellent)
                        .Select(mode => mode.SystemSharedPredictedPeakBytes)
                ];
                if (offered.Length > 0)
                {
                    minimum = offered.Min();
                    minimumLabel = "Smallest offered setup needs";
                }
            }
        }

        return presentation with
        {
            MemoryOverview = new CompatibilityMemoryOverviewPresentation(
                memory.AvailableSystemMemoryBytes,
                memory.SafetyReserveBytes,
                memory.SafeModelBudgetBytes,
                current.SystemSharedRequiredBytes,
                current.ContextTokens,
                minimum,
                minimumLabel)
        };
    }

    private static CompatibilityPresentation WithMachineMemory(
        CompatibilityPresentation presentation,
        CompatibilityMachineMemory? memory) => memory is null
            ? presentation
            : presentation with
            {
                MachineMemory = new CompatibilityMachineMemoryPresentation(
                [
                    new CompatibilityFact(
                        "Installed RAM",
                        CompatibilityBudget.Describe(
                            memory.InstalledSystemMemoryBytes),
                        "Physical memory in this computer"),
                    new CompatibilityFact(
                        "Available now",
                        CompatibilityBudget.Describe(
                            memory.AvailableSystemMemoryBytes),
                        "Free when this check ran"),
                    new CompatibilityFact(
                        "Safety reserve",
                        CompatibilityBudget.Describe(memory.SafetyReserveBytes),
                        "Kept for Windows and other applications"),
                    new CompatibilityFact(
                        "Safe for this model",
                        CompatibilityBudget.Describe(memory.SafeModelBudgetBytes),
                        "Available after the safety reserve")
                ])
            };

    private static CompatibilityPresentation WithMemoryClarity(
        CompatibilityPresentation presentation,
        CompatibilityScreenModel model,
        CompatibilityMachineMemory? memory)
    {
        if (model.State != CompatibilityScreenState.NoEstimatedSafeConfiguration
            || memory is null
            || model.SmallestOptimizedRequiredBytes is not { } smallestOptimized
            || model.Setup is not { } currentSetup)
        {
            return presentation;
        }

        ulong minimumFree = SaturatingAdd(
            smallestOptimized,
            memory.SafetyReserveBytes);
        ulong additionalFree = minimumFree > memory.AvailableSystemMemoryBytes
            ? minimumFree - memory.AvailableSystemMemoryBytes
            : 0;
        CompatibilityStorageRequirementView? storageRequirement =
            model.OptimizationStorageRequirement;
        bool storageShortage = storageRequirement is not null
            && storageRequirement.RequiredBytes > storageRequirement.AvailableBytes
            && smallestOptimized <= memory.SafeModelBudgetBytes;
        ulong additionalStorage = storageShortage
            ? storageRequirement!.RequiredBytes - storageRequirement.AvailableBytes
            : 0;
        string current = CompatibilityBudget.Describe(currentSetup.RequiredBytes);
        string available = CompatibilityBudget.Describe(
            memory.AvailableSystemMemoryBytes);
        string reserve = CompatibilityBudget.Describe(memory.SafetyReserveBytes);
        string safe = CompatibilityBudget.Describe(memory.SafeModelBudgetBytes);
        string minimum = CompatibilityBudget.Describe(minimumFree);
        string additional = Math.Ceiling(additionalFree / (1024m * 1024m))
            .ToString("N0", System.Globalization.CultureInfo.CurrentCulture) + " MB";
        string storageRequired = CompatibilityBudget.Describe(
            storageRequirement?.RequiredBytes ?? 0);
        string storageAvailable = CompatibilityBudget.Describe(
            storageRequirement?.AvailableBytes ?? 0);
        string storageAdditional = CompatibilityBudget.Describe(additionalStorage);

        return presentation with
        {
            MemoryClarity = new CompatibilityMemoryClarityPresentation(
                memory.AvailableSystemMemoryBytes,
                memory.SafetyReserveBytes,
                memory.SafeModelBudgetBytes,
                currentSetup.RequiredBytes,
                smallestOptimized,
                minimumFree,
                additionalFree,
                currentSetup.ContextTokens),
            StorageShortage = storageShortage
                ? new CompatibilityStorageShortagePresentation(
                    storageRequirement!.RequiredBytes,
                    storageRequirement.AvailableBytes,
                    additionalStorage)
                : null,
            OutcomeTitle = storageShortage
                ? "More disk space is needed"
                : "Not enough free RAM",
            OutcomeDetail = storageShortage
                ? "Your computer has enough RAM for the smallest acceptable format, "
                    + "but creating it needs " + storageRequired + " of storage and only "
                    + storageAvailable + " is available. Free " + storageAdditional
                    + " of storage, then check again."
                : "The current setup is estimated to peak at " + current
                    + " while it runs. " + available
                    + " is free now, including the " + reserve
                    + " safety reserve kept for Windows and other applications. That leaves "
                    + safe + " safe for the model. The smallest evaluated optimisation needs "
                    + minimum + " free in total under the current reserve. More RAM needed now: " + additional
                    + ". Free up about that much more RAM, then restart hardware inspection to check again. "
                    + "The reserve and available memory can change. This is RAM, not disk space.",
            Recoveries = storageShortage
                ?
                [
                    new CompatibilityRecovery(
                        "Free some storage space, then check again",
                        "Move or remove files until at least " + storageAdditional
                        + " more storage is available for the optimised model.")
                ]
                : presentation.Recoveries,
            MemoryRecoveryReason = storageShortage
                ? CompatibilityMemoryRecoveryReason.None
                : presentation.MemoryRecoveryReason
        };
    }

    private static ulong SaturatingAdd(ulong left, ulong right) =>
        left > ulong.MaxValue - right ? ulong.MaxValue : left + right;

    private static CompatibilityPresentation OptimizationSelection(
        CompatibilityScreenModel model,
        CompatibilitySetupView current,
        CompatibilityOptimizationView optimization,
        OptimizationPreferenceSelection preference,
        bool isRequired)
    {

        List<CompatibilityOptimizationModePresentation> modes = [];
        foreach (CompatibilityOptimizationModeView mode in optimization.Modes)
        {
            CompatibilityOptimizationModePresentation? described =
                DescribeMode(mode, current);
            if (described is null)
            {
                return NotEstablished(model);
            }

            modes.Add(described);
        }
        List<CompatibilityExactOptimizationModePresentation> exactModes = [];
        var exactIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (CompatibilityExactOptimizationModeView exact in
                 optimization.ExactSafeModes)
        {
            if (!exactIdentities.Add(exact.CandidateIdentity))
            {
                return NotEstablished(model);
            }
            CompatibilityOptimizationModePresentation? described =
                DescribeMode(exact.Mode, current);
            if (described is null)
            {
                return NotEstablished(model);
            }

            exactModes.Add(new(
                exact.CandidateIdentity,
                described,
                described.IsExperimental
                    ? CompatibilityExactOptimizationAvailability.ExperimentalPreview
                    : CompatibilityExactOptimizationAvailability.Released));
        }

        List<CompatibilityExactOptimizationModePresentation> safeSliderModes = [];
        var safeSliderIdentities = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < optimization.SafeSliderModes.Count; index++)
        {
            CompatibilityExactOptimizationModeView safe =
                optimization.SafeSliderModes[index];
            if (!safeSliderIdentities.Add(safe.CandidateIdentity))
            {
                return NotEstablished(model);
            }

            CompatibilityOptimizationModePresentation? described =
                DescribeMode(safe.Mode, current);
            if (described is null || described.IsExperimental)
            {
                return NotEstablished(model);
            }
            described = described with
            {
                Label = SafeSliderModeLabel(
                    index, optimization.SafeSliderModes.Count)
            };

            safeSliderModes.Add(new(
                safe.CandidateIdentity,
                described,
                CompatibilityExactOptimizationAvailability.Released));
        }

        int? safeSliderSelectedIndex = optimization.SafeSliderSelectedIndex;
        if (preference.Kind == OptimizationPreferenceKind.Exact)
        {
            int[] matches = safeSliderModes
                .Select((mode, index) => new { mode.CandidateIdentity, index })
                .Where(item => string.Equals(
                    item.CandidateIdentity,
                    preference.ExactCandidateIdentity,
                    StringComparison.Ordinal))
                .Select(item => item.index)
                .ToArray();
            safeSliderSelectedIndex = matches.Length == 1 ? matches[0] : null;
        }
        else if (safeSliderSelectedIndex is not { } sourceIndex
                 || (uint)sourceIndex >= (uint)safeSliderModes.Count)
        {
            safeSliderSelectedIndex = null;
        }

        CompatibilityOptimizationModePresentation selected;
        if (preference.Kind == OptimizationPreferenceKind.Exact)
        {
            CompatibilityExactOptimizationModePresentation[] matches =
            [.. safeSliderModes.Where(mode => string.Equals(
                mode.CandidateIdentity,
                preference.ExactCandidateIdentity,
                StringComparison.Ordinal))];
            if (matches.Length == 0)
            {
                matches =
                [.. exactModes.Where(mode => string.Equals(
                    mode.CandidateIdentity,
                    preference.ExactCandidateIdentity,
                    StringComparison.Ordinal))];
            }
            if (matches.Length != 1)
            {
                return NotEstablished(model);
            }
            selected = matches[0].Mode;
        }
        else
        {
            CompatibilityOptimizationLabelCode selectedCode = preference.Kind ==
                OptimizationPreferenceKind.Automatic
                    ? CompatibilityOptimizationLabelCode.Automatic
                    : preference.Band switch
                    {
                        OptimizationPreferenceBand.MaximumEfficiency =>
                            CompatibilityOptimizationLabelCode.MaximumEfficiency,
                        OptimizationPreferenceBand.Efficient =>
                            CompatibilityOptimizationLabelCode.Efficient,
                        OptimizationPreferenceBand.Balanced =>
                            CompatibilityOptimizationLabelCode.Balanced,
                        OptimizationPreferenceBand.HighCapability =>
                            CompatibilityOptimizationLabelCode.HighCapability,
                        OptimizationPreferenceBand.MaximumCapability =>
                            CompatibilityOptimizationLabelCode.MaximumCapability,
                        _ => throw new ArgumentOutOfRangeException(nameof(preference))
                    };
            selected = modes.Single(mode => string.Equals(
                mode.Label,
                Label(selectedCode),
                StringComparison.Ordinal));
        }

        bool unavailableCurrentConfiguration = isRequired
            && current.Route == RuntimeRouteId.OpenVinoGenAi
            && model.Findings.Any(item => item.Code == CompatibilityFindingCode.BaselineConfigurationUnavailable);

        return CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = unavailableCurrentConfiguration
                ? "Choose a verified configuration for this model."
                : isRequired
                ? "Choose a smaller setup that fits this computer safely."
                : "The current model fits. You can still optimise it for your preferred balance.",
            Tone = isRequired
                ? CompatibilityOutcomeTone.Caution
                : CompatibilityOutcomeTone.Positive,
            OutcomeTitle = unavailableCurrentConfiguration
                ? "The current configuration is unavailable"
                : isRequired
                ? "This model needs to be quantised to run on your computer"
                : "Optimisation is optional",
            OutcomeDetail = unavailableCurrentConfiguration
                ? "The current configuration failed output checks. A separately verified alternative is available; choose it before chatting. Your model files remain unchanged."
                : isRequired
                ? "Your current model needs more memory than this computer can safely spare. A smaller version can run here."
                : "You can chat with the model as it is, or create a setup with different memory and quality trade-offs.",
            OutcomeBadge = unavailableCurrentConfiguration
                ? "VERIFIED ALTERNATIVE AVAILABLE"
                : isRequired
                ? "OPTIMISATION REQUIRED"
                : "CURRENT MODEL FITS",
            Facts = CompatibilitySetupNarrative.Facts(current),
            Budget = CompatibilitySetupNarrative.Budget(current),
            EstimateSummary = CompatibilitySetupNarrative.EstimateSummary(current),
            DisclosureTitle = "How we worked this out",
            DisclosureDetail = DisclosureText(model),
            PrimaryActionText = isRequired
                ? "Choose optimisation"
                : "Optimise first",
            PrimaryActionEnabled = model.ContinueEnabled,
            SecondaryActionText = isRequired
                ? "Back"
                : "Back",
            SecondaryActionEnabled = true,
            MemoryRecoveryReason = isRequired
                ? MemoryRecoveryReason(model)
                : CompatibilityMemoryRecoveryReason.None,
            ForwardActionKind = isRequired
                && current.Route == RuntimeRouteId.LlamaCpp
                && MemoryRecoveryReason(model) == CompatibilityMemoryRecoveryReason.None
                    ? CompatibilityForwardActionKind.ImportAnotherModel
                    : CompatibilityForwardActionKind.None,
            Optimization = new CompatibilityOptimizationPresentation(
                isRequired
                    ? "You can choose how you want to balance memory use and expected quality."
                    : "Choose an optional balance, or keep the current model unchanged.",
                current.Route == RuntimeRouteId.LlamaCpp
                    ? OptimizationRoute.Gguf
                    : OptimizationRoute.OpenVino,
                modes,
                selected,
                preference.Kind == OptimizationPreferenceKind.Automatic,
                preference.PreferenceValue ?? optimization.RecommendedSliderValue ?? 50,
                Weight(current.Route, current.Weights),
                CurrentCache(current),
                $"{current.ContextTokens:N0} tokens",
                CompatibilityBudget.Describe(current.SystemSharedRequiredBytes),
                CompatibilityBudget.Describe(current.SystemSharedSafeBudgetBytes),
                CompatibilityBudget.Describe(current.SystemSharedHeadroomBytes),
                DescribeOptional(current.DedicatedRequiredBytes),
                DescribeOptional(current.DedicatedSafeBudgetBytes),
                DescribeOptional(current.DedicatedHeadroomBytes),
                model.ContinueEnabled,
                exactModes,
                optimization.HasAdditionalExactSafeModes,
                safeSliderModes,
                safeSliderSelectedIndex,
                string.Empty,
                preference)
        };
    }

    private static CompatibilityOptimizationModePresentation? DescribeMode(
        CompatibilityOptimizationModeView mode,
        CompatibilitySetupView current)
    {
        string? weight = RecommendedWeight(mode, current);
        if (weight is null)
        {
            return null;
        }

        string warning = mode.QualityNotice switch
        {
            OptimizationQualityNotice.SignificantQualityReduction =>
                "Warning: this setting may cause a significant quality reduction.",
            OptimizationQualityNotice.NoticeableQualityReduction =>
                "This setting may cause a noticeable quality reduction.",
            OptimizationQualityNotice.SomeQualityReduction =>
                "This setting may cause some quality reduction.",
            _ => string.Empty
        };
        bool strong = mode.QualityNotice ==
            OptimizationQualityNotice.SignificantQualityReduction
            || mode.GgufWeights == GgufWeightFormat.Q2K
            || mode.GgufKvCache == GgufKvCacheFormat.TurboQuant3Bit
            || mode.OpenVinoKvCache == OpenVinoKvCacheFormat.TurboQuantTbq3;
        if (strong && string.IsNullOrEmpty(warning))
        {
            warning = "Warning: this setting may cause a significant quality reduction.";
        }

        if (mode.RequiresRequantisationAcknowledgement)
        {
            warning += (warning.Length == 0 ? string.Empty : " ")
                + "Quantisation creates a new copy; your original model remains unchanged.";
        }

        return new CompatibilityOptimizationModePresentation(
            Label(mode.LabelCode),
            mode.SliderValue,
            $"Expected quality: {Quality(mode.ExpectedQualityLevel)}",
            weight,
            mode.Route == OptimizationRoute.Gguf
                ? Cache(mode.GgufKvCache!.Value)
                : Cache(mode.OpenVinoKvCache!.Value),
            $"{mode.ContextTokens:N0} tokens",
            CompatibilityBudget.Describe(mode.SystemSharedPredictedPeakBytes),
            CompatibilityBudget.Describe(mode.SystemSharedSafeBudgetBytes),
            CompatibilityBudget.Describe(mode.SystemSharedHeadroomBytes),
            DescribeOptional(mode.DedicatedRequiredBytes),
            DescribeOptional(mode.DedicatedSafeBudgetBytes),
            DescribeOptional(mode.DedicatedHeadroomBytes),
            mode.IsExperimental,
            strong,
            warning.Trim(),
            mode.RequiresPersistentArtifact,
            mode.RequiresRequantisationAcknowledgement);
    }

    private static string? RecommendedWeight(
        CompatibilityOptimizationModeView mode,
        CompatibilitySetupView current)
    {
        if (mode.Route == OptimizationRoute.Gguf)
        {
            if (current.Route != RuntimeRouteId.LlamaCpp
                || mode.GgufWeights is not { } gguf
                || mode.OpenVinoWeights is not null)
            {
                return null;
            }

            if (gguf != GgufWeightFormat.Imported)
            {
                return gguf == GgufWeightFormat.Unspecified ? null : Weight(gguf);
            }

            return current.Weights == WeightQuantisation.Unknown
                ? null
                : Weight(current.Route, current.Weights);
        }

        if (mode.Route != OptimizationRoute.OpenVino
            || current.Route != RuntimeRouteId.OpenVinoGenAi
            || mode.OpenVinoWeights is not { } openVino
            || mode.GgufWeights is not null)
        {
            return null;
        }

        if (openVino != OpenVinoWeightFormat.Original)
        {
            return openVino == OpenVinoWeightFormat.Unspecified
                ? null
                : Weight(openVino);
        }

        return current.Weights switch
        {
            WeightQuantisation.F16 => "FP16",
            WeightQuantisation.Q8_0 => "INT8",
            WeightQuantisation.Q4_K_M => "INT4",
            _ => null
        };
    }

    private static string Label(CompatibilityOptimizationLabelCode code) => code switch
    {
        CompatibilityOptimizationLabelCode.Automatic =>
            OptimizationPreferenceLabelPolicy.AutomaticLabel,
        CompatibilityOptimizationLabelCode.MaximumEfficiency =>
            OptimizationPreferenceLabelPolicy.GetLabel(
                OptimizationPreferenceBand.MaximumEfficiency),
        CompatibilityOptimizationLabelCode.Efficient =>
            OptimizationPreferenceLabelPolicy.GetLabel(OptimizationPreferenceBand.Efficient),
        CompatibilityOptimizationLabelCode.Balanced =>
            OptimizationPreferenceLabelPolicy.GetLabel(OptimizationPreferenceBand.Balanced),
        CompatibilityOptimizationLabelCode.HighCapability =>
            OptimizationPreferenceLabelPolicy.GetLabel(OptimizationPreferenceBand.HighCapability),
        CompatibilityOptimizationLabelCode.MaximumCapability =>
            OptimizationPreferenceLabelPolicy.GetLabel(OptimizationPreferenceBand.MaximumCapability),
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };

    internal static string SafeSliderModeLabel(int index, int count)
    {
        if (count < 1 || index < 0 || index >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        if (count == 1)
        {
            return "Single available setup";
        }

        string[] anchors =
        [
            "Maximum efficiency",
            "Efficient",
            "Balanced",
            "Higher memory",
            "Highest memory"
        ];
        int anchor = (int)Math.Round(
            index * 4d / (count - 1),
            MidpointRounding.AwayFromZero);
        return anchors[anchor];
    }

    private static string Quality(OptimizationAssessment quality) => quality switch
    {
        OptimizationAssessment.Poor => "Low",
        OptimizationAssessment.Acceptable => "Acceptable",
        OptimizationAssessment.Good => "Good",
        OptimizationAssessment.Excellent => "Excellent",
        _ => "Not established"
    };

    private static string Quality(OptimizationQualityLevel quality) => quality switch
    {
        OptimizationQualityLevel.Acceptable => "Acceptable",
        OptimizationQualityLevel.Fair => "Fair",
        OptimizationQualityLevel.Good => "Good",
        OptimizationQualityLevel.VeryGood => "Very good",
        OptimizationQualityLevel.Excellent => "Excellent",
        _ => "Not established"
    };

    private static string Weight(GgufWeightFormat format) => format switch
    {
        GgufWeightFormat.Q6K => "Q6_K",
        GgufWeightFormat.Q5KM => "Q5_K_M",
        GgufWeightFormat.Q4KM => "Q4_K_M",
        GgufWeightFormat.Q3KM => "Q3_K_M",
        GgufWeightFormat.Q2K => "Q2_K",
        _ => format.ToString().ToUpperInvariant()
    };

    private static string Weight(OpenVinoWeightFormat format) => format switch
    {
        OpenVinoWeightFormat.Fp16 => "FP16",
        OpenVinoWeightFormat.Int8 => "INT8",
        OpenVinoWeightFormat.Int4 => "INT4",
        _ => "Original"
    };

    private static string Weight(WeightQuantisation format) => format.ToString();

    private static string Weight(RuntimeRouteId route, WeightQuantisation format) =>
        route == RuntimeRouteId.OpenVinoGenAi
            ? format switch
            {
                WeightQuantisation.F16 => "FP16",
                WeightQuantisation.Q8_0 => "INT8",
                WeightQuantisation.Q4_K_M => "INT4",
                _ => Weight(format)
            }
            : Weight(format);

    private static string Cache(GgufKvCacheFormat format) => format switch
    {
        GgufKvCacheFormat.TurboQuant3Bit => "TurboQuant 3-bit",
        _ => format.ToString()
    };

    private static string Cache(OpenVinoKvCacheFormat format) => format switch
    {
        OpenVinoKvCacheFormat.RouteDefault => "Automatic (OpenVINO default)",
        OpenVinoKvCacheFormat.TurboQuantTbq4 => "TurboQuant TBQ4",
        OpenVinoKvCacheFormat.TurboQuantTbq3 => "TurboQuant TBQ3",
        _ => format.ToString().ToUpperInvariant()
    };

    private static string CurrentCache(CompatibilitySetupView setup) =>
        setup.GgufKvCache is { } gguf
            ? Cache(gguf)
            : setup.OpenVinoKvCache is { } openVino
                ? Cache(openVino)
                : "Not reported";

    private static string DescribeOptional(ulong? bytes) => bytes.HasValue
        ? CompatibilityBudget.Describe(bytes.Value)
        : "Not used";

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
            ShowOptimiseFurtherAction = model.Setup?.Route == RuntimeRouteId.LlamaCpp,
            OptimiseFurtherActionText = model.Setup?.Route == RuntimeRouteId.LlamaCpp
                ? "Optimise further"
                : string.Empty,
            CurrentModelChatActionText = model.Setup?.Route == RuntimeRouteId.LlamaCpp
                ? "Chat with model now"
                : string.Empty,
            Recoveries = BaselineRecoveries(model)
        };

    private static CompatibilityPresentation NothingFits(CompatibilityScreenModel model) =>
        CompatibilityPresentation.Empty with
        {
            PageTitle = Title,
            PageLede = "We compared the evaluated setups with the RAM available now.",
            Tone = CompatibilityOutcomeTone.Blocking,
            OutcomeTitle = "Not enough free RAM",
            OutcomeDetail =
                "Every verified setup needs more memory than you can safely spare right "
                + "now. Close unused applications and browser tabs to free memory, then "
                + "check again.",
            OutcomeBadge = "ESTIMATE",
            DisclosureTitle = "How we worked this out",
            DisclosureDetail = DisclosureText(model),
            Recoveries =
            [
                new CompatibilityRecovery(
                    "Free some memory, then check again",
                    "Close unused applications and browser tabs. What matters is the "
                    + "memory free right now, not how much memory your computer has in "
                    + "total."),
                new CompatibilityRecovery(
                    "Try a smaller model",
                    "A smaller version of the same model needs less memory to run."),
                .. BaselineRecoveries(model)
            ],
            PrimaryActionText = "Continue",
            PrimaryActionEnabled = false,
            SecondaryActionText = "Back",
            SecondaryActionEnabled = true,
            MemoryRecoveryReason = MemoryRecoveryReason(model)
        };

    private static CompatibilityMemoryRecoveryReason MemoryRecoveryReason(
        CompatibilityScreenModel model)
    {
        CompatibilitySetupView? setup = model.State switch
        {
            CompatibilityScreenState.OptimisationRequired => model.CurrentSetup,
            CompatibilityScreenState.NoEstimatedSafeConfiguration => model.Setup,
            _ => null
        };
        if (setup?.Fit != CompatibilityFitState.DoesNotFit
            || setup.SystemSharedRequiredBytes <= setup.SystemSharedSafeBudgetBytes
            || model.Findings.Any(finding => finding.Severity == FindingSeverity.Blocking))
        {
            return CompatibilityMemoryRecoveryReason.None;
        }

        bool anyDedicated = setup.DedicatedRequiredBytes.HasValue
            || setup.DedicatedSafeBudgetBytes.HasValue
            || setup.DedicatedHeadroomBytes.HasValue;
        bool completeDedicated = setup.DedicatedRequiredBytes.HasValue
            && setup.DedicatedSafeBudgetBytes.HasValue
            && setup.DedicatedHeadroomBytes.HasValue;
        if (anyDedicated
            && (!completeDedicated
                || setup.DedicatedRequiredBytes > setup.DedicatedSafeBudgetBytes))
        {
            // mixed or incomplete evidence is not authority to offer a memory-
            // only recovery as though it addressed the whole refusal
            return CompatibilityMemoryRecoveryReason.None;
        }

        if (model.State == CompatibilityScreenState.OptimisationRequired
            && HasComparableFittingOfferedMode(model, setup))
        {
            return CompatibilityMemoryRecoveryReason.None;
        }

        return CompatibilityMemoryRecoveryReason.SystemMemoryPressure;
    }

    private static bool HasComparableFittingOfferedMode(
        CompatibilityScreenModel model,
        CompatibilitySetupView current)
    {
        OptimizationRoute? route = current.Route switch
        {
            RuntimeRouteId.LlamaCpp => OptimizationRoute.Gguf,
            RuntimeRouteId.OpenVinoGenAi => OptimizationRoute.OpenVino,
            _ => null
        };
        if (route is null || model.Optimization is not { } optimization)
        {
            return false;
        }

        return optimization.Modes.Any(mode =>
            mode.Route == route
            && mode.SystemSharedPredictedPeakBytes > 0
            && mode.SystemSharedSafeBudgetBytes > 0
            && mode.SystemSharedSafeBudgetBytes
                <= current.SystemSharedSafeBudgetBytes
            && mode.SystemSharedPredictedPeakBytes
                <= mode.SystemSharedSafeBudgetBytes
            && mode.ExpectedQualityLevel is
                OptimizationQualityLevel.Acceptable
                or OptimizationQualityLevel.Fair
                or OptimizationQualityLevel.Good
                or OptimizationQualityLevel.VeryGood
                or OptimizationQualityLevel.Excellent
            && DedicatedModeFits(mode));
    }

    private static bool DedicatedModeFits(
        CompatibilityOptimizationModeView mode)
    {
        bool any = mode.DedicatedRequiredBytes.HasValue
            || mode.DedicatedSafeBudgetBytes.HasValue
            || mode.DedicatedHeadroomBytes.HasValue;
        if (!any)
        {
            return true;
        }

        return mode.DedicatedRequiredBytes is > 0
            && mode.DedicatedSafeBudgetBytes is > 0
            && mode.DedicatedHeadroomBytes.HasValue
            && mode.DedicatedRequiredBytes <= mode.DedicatedSafeBudgetBytes
            && mode.DedicatedHeadroomBytes
                == mode.DedicatedSafeBudgetBytes - mode.DedicatedRequiredBytes;
    }

    /// <summary>
    /// the state this feature ships in until the hardware and model checks hand
    /// it something to work with. it has to admit it knows nothing while still
    /// being useful, which is why every finding becomes a line about what would
    /// help rather than a bare error
    /// </summary>
    private static CompatibilityPresentation NotEstablished(CompatibilityScreenModel model)
    {
        CompatibilityFindingView[] blockers = model.Findings
            .Where(finding => finding.Severity == FindingSeverity.Blocking).ToArray();
        bool noEstimateOnly = blockers.Length > 0 && blockers.All(finding =>
            finding.Code == CompatibilityFindingCode.NoCandidateCouldBeEstimated);
        return CompatibilityPresentation.Empty with
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
            SecondaryActionText = noEstimateOnly ? string.Empty : "Check again",
            SecondaryActionEnabled = !noEstimateOnly,
            SecondaryActionKind = noEstimateOnly
                ? CompatibilitySecondaryActionKind.None : CompatibilitySecondaryActionKind.Retry,
            ForwardActionKind = CompatibilityForwardActionKind.ImportAnotherModel
        };
    }

    /// <summary>
    /// one line per blocking finding. a code with no line here would reach the
    /// user as silence, so the fallback is deliberately generic rather than
    /// missing
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
    /// why the setup the user already has isn't among the options. the remedies
    /// genuinely differ, which is why the engine tells them apart instead of
    /// reporting one vague absence
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
