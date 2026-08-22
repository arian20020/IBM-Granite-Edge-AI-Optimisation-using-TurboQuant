#if DEBUG
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.DebugFixtures;

/// <summary>One rendered state, named so it can be asked for.</summary>
internal sealed record CompatibilityFixture(
    string Id,
    string Title,
    CompatibilityPresentation Presentation);

/// <summary>
/// Every screen state, renderable without hardware, handoffs or a real model.
///
/// Nine of the ten states are unreachable today: the adapters that would feed
/// the engine do not exist, so a real run always ends at "no answer yet". Without
/// these, nine screens could not be looked at, reviewed, or regression-tested
/// until another team shipped — and a design nobody can see is a design nobody
/// can correct.
///
/// Compiled only in Debug. These are presentation inputs, never run results, so
/// nothing here can be mistaken for a conclusion about a real computer.
/// </summary>
internal static class CompatibilityFixtureCatalogue
{
    internal static IReadOnlyList<CompatibilityFixture> All { get; } =
    [
        new("CMP-001", "Working — step 1 of 4",
            CompatibilityPresentationFactory.Analysing(0)),
        new("CMP-002", "Working — step 2 of 4",
            CompatibilityPresentationFactory.Analysing(1)),
        new("CMP-003", "Working — step 3 of 4",
            CompatibilityPresentationFactory.Analysing(2)),
        new("CMP-004", "Working — step 4 of 4",
            CompatibilityPresentationFactory.Analysing(3)),

        new("CMP-010", "Yes, this should run",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.EstimatedCompatible,
                continueEnabled: true,
                useCurrentModel: true))),

        new("CMP-011", "Yes, but your current setup is not offered",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.EstimatedCompatible,
                continueEnabled: true,
                useCurrentModel: false,
                baseline: BaselineExclusionReason.BaselineEntryRequiresExperimentalOptIn))),

        new("CMP-020", "Runs, but very little memory spare",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.OptimisationRequired,
                continueEnabled: true,
                useCurrentModel: true))),

        new("CMP-030", "Nothing fits safely",
            CompatibilityPresentationFactory.From(Concluded(
                CompatibilityScreenState.NoEstimatedSafeConfiguration,
                continueEnabled: false,
                useCurrentModel: false,
                availability: ModeAvailability.Unavailable,
                reason: ModeAdmissionReason.FitStateNotSafeOrNarrow))),

        // The state this feature actually ships in.
        new("CMP-040", "No answer yet — nothing has been handed over",
            CompatibilityPresentationFactory.From(NotEstablished(
                CompatibilityFindingCode.ModelFactsUnavailable,
                CompatibilityFindingCode.HardwareFactsUnavailable,
                CompatibilityFindingCode.FreshMemoryUnavailable))),

        new("CMP-041", "No answer yet — the model's shape could not be read",
            CompatibilityPresentationFactory.From(NotEstablished(
                CompatibilityFindingCode.NoCandidateCouldBeEstimated))),

        new("CMP-042", "No answer yet — memory could not be read",
            CompatibilityPresentationFactory.From(NotEstablished(
                CompatibilityFindingCode.FreshMemoryUnavailable))),

        new("CMP-050", "Testing — step 1 of 4",
            CompatibilityPresentationFactory.Verifying(0)),
        new("CMP-051", "Testing — step 4 of 4",
            CompatibilityPresentationFactory.Verifying(3)),

        new("CMP-060", "Tested and it runs",
            CompatibilityPresentationFactory.VerifiedCompatible()),

        new("CMP-070", "Tested and it did not run",
            CompatibilityPresentationFactory.VerificationFailed()),

        new("CMP-080", "Check stopped",
            CompatibilityPresentationFactory.Cancelled())
    ];

    internal static CompatibilityFixture? ById(string id)
    {
        foreach (CompatibilityFixture fixture in All)
        {
            if (string.Equals(fixture.Id, id, System.StringComparison.Ordinal))
            {
                return fixture;
            }
        }

        return null;
    }

    private static CompatibilityScreenModel Concluded(
        CompatibilityScreenState state,
        bool continueEnabled,
        bool useCurrentModel,
        BaselineExclusionReason baseline = BaselineExclusionReason.None,
        ModeAvailability availability = ModeAvailability.Available,
        ModeAdmissionReason reason = ModeAdmissionReason.None) =>
        CompatibilityScreenModel.ForPresentation(
            state,
            [
                new CompatibilityFindingView(
                    CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Warning)
            ],
            [
                Mode(CompatibilityMode.Automatic, availability, reason),
                Mode(CompatibilityMode.Quality, availability, reason),
                Mode(CompatibilityMode.Balanced, availability, reason),
                Mode(CompatibilityMode.Efficiency, availability, reason)
            ],
            baseline,
            useCurrentModel,
            continueEnabled);

    private static CompatibilityModeView Mode(
        CompatibilityMode mode,
        ModeAvailability availability,
        ModeAdmissionReason reason) =>
        new(mode, availability, reason, availability == ModeAvailability.Available);

    private static CompatibilityScreenModel NotEstablished(
        params CompatibilityFindingCode[] codes)
    {
        List<CompatibilityFindingView> findings = [];

        foreach (CompatibilityFindingCode code in codes)
        {
            findings.Add(new CompatibilityFindingView(code, FindingSeverity.Blocking));
        }

        return CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NotEstablished,
            findings,
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false);
    }
}
#endif
