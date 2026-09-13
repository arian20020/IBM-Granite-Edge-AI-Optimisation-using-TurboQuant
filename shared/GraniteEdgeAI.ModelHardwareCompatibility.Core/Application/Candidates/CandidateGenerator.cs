using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Expands the admitted support entries into complete candidates.
///
/// The only axis expanded is context: every other combination came from a person
/// writing an entry down. Conversions are generated conservatively — never
/// upward, and never as a requantisation without a trusted higher-precision
/// source — because a conversion the user did not ask for costs disk, time and
/// quality that cannot be recovered.
/// </summary>
internal static class CandidateGenerator
{
    internal static CandidateGenerationResult Generate(CandidateGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Matrix.Provenance is PolicyProvenance.Absent
            or PolicyProvenance.Unspecified)
        {
            return new CandidateGenerationResult(
                [], BaselineExclusionReason.SupportMatrixUnavailable);
        }

        // A model whose trained context limit is unknown gives no safe basis for
        // a default: substituting an entry's declared maximum in its place would
        // give such a model no model-side context constraint at all, and no
        // candidate would carry any marker that the limit was never established.
        // Consistent with PlanningContextPolicy.Resolve, which refuses the same
        // unknown rather than guessing at it.
        if (request.Facts.DeclaredContextLimit is null)
        {
            return new CandidateGenerationResult(
                [], BaselineExclusionReason.ModelContextLimitNotEstablished);
        }

        WeightQuantisation source = WeightQuantisationMap.FromGgufFileType(
            request.Facts.FileType, request.Facts.QuantisationVersion);

        List<CompatibilityCandidate> candidates = [];
        HashSet<string> seenFingerprints = [];
        CompatibilityCandidate? baseline = null;

        // Tracks whether any admitted entry produced a configuration equal to
        // the baseline's, even if no admitted context for that entry equalled
        // the baseline context. That distinction is what lets the exclusion
        // reason below tell "no entry matches the baseline shape at all" apart
        // from "an entry matches, but not at this context".
        bool baselineConfigurationAdmitted = false;

        // Tracks whether an entry declaring the baseline's exact configuration
        // shape resolved to Unavailable (genuinely not installed) versus
        // Unsupported on an Experimental entry (installed, but the user has not
        // opted in). Set before the availability filter below so the two
        // cannot be confused with each other, or with "no admitted entry
        // matches the baseline shape at all". When more than one baseline-
        // matching entry fails to admit, BaselineEntryNotInstalled takes
        // precedence: "install the backend" is the simpler, more fundamental
        // fix, and reporting the opt-in reason instead could send the user to
        // opt in to a route that would still refuse to start.
        BaselineExclusionReason baselineEntryFailure = BaselineExclusionReason.None;

        foreach (CompatibilitySupportEntry entry in request.Matrix.Entries)
        {
            if (!TryResolvePreparationKind(
                entry, source, request.TrustedSource, out bool converts, out GgufWeightFormat effectiveWeights))
            {
                continue;
            }

            // Built from the effective weight format, not entry.Weights directly:
            // an entry naming the encoding the file already has is normalised back
            // to Imported here, so it collapses onto the same configuration as the
            // Imported entry describing the same runtime shape instead of being
            // offered as a separate, falsely-labelled "conversion".
            GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                effectiveWeights, entry.KvCache, entry.Backend, entry.Device, entry.Offload);

            bool isBaselineConfiguration = configuration == request.BaselineConfiguration;

            InstallationState installation =
                request.InstallationStates.TryGetValue(entry.EntryId, out InstallationState state)
                    ? state
                    : InstallationState.Unknown;

            SupportAvailability availability =
                SupportMatrixResolver.Resolve(entry.Level, installation);

            if (availability is not (SupportAvailability.Available
                or SupportAvailability.ExperimentalAvailable))
            {
                if (isBaselineConfiguration)
                {
                    baselineEntryFailure = MoreActionable(
                        baselineEntryFailure, WhyNotAdmitted(entry.Level, installation));
                }

                continue;
            }

            if (isBaselineConfiguration)
            {
                baselineConfigurationAdmitted = true;
            }

            foreach (ContextTokenCount context in AdmittedContexts(entry, request))
            {
                bool isBaselineShape = isBaselineConfiguration && context == request.BaselineContext;

                CandidatePreparation preparation = converts
                    ? CandidatePreparation.WeightConversionRequired
                    : isBaselineShape
                        ? CandidatePreparation.None
                        : CandidatePreparation.RuntimeProfileOnly;

                CompatibilityCandidate candidate = CompatibilityCandidate.Create(
                    configuration,
                    context,
                    preparation,
                    entry.EntryId,
                    isExperimental: availability == SupportAvailability.ExperimentalAvailable,
                    isBaseline: isBaselineShape);

                // Safe only because CandidateFingerprint.Compute covers
                // (configuration, context, preparation) while isBaselineShape
                // above covers (configuration, context): the two agree on every
                // axis fingerprint dedup can collide on, so deduping here can
                // never silently discard a candidate that isBaselineShape would
                // have told apart. A future change narrowing what Compute covers
                // must revisit this.
                if (!seenFingerprints.Add(candidate.Fingerprint.Value))
                {
                    continue;
                }

                if (isBaselineShape)
                {
                    baseline = candidate;
                    continue;
                }

                candidates.Add(candidate);
            }
        }

        if (baseline is null)
        {
            BaselineExclusionReason reason = baselineConfigurationAdmitted
                ? BaselineExclusionReason.BaselineContextOutsideEntryBounds
                : baselineEntryFailure != BaselineExclusionReason.None
                    ? baselineEntryFailure
                    : BaselineExclusionReason.NoAdmittedEntryMatchesTheBaseline;

            return new CandidateGenerationResult(candidates, reason);
        }

        // The baseline is always evaluated first: it is what the user already
        // has, and every alternative is judged relative to it.
        return new CandidateGenerationResult(
            [baseline, .. candidates], BaselineExclusionReason.None);
    }

    /// <summary>
    /// Decides whether an entry's weight format is reachable from the imported
    /// file, whether reaching it writes a new artifact, and the weight format the
    /// resulting configuration is actually built from. Returns false when the
    /// entry must not be offered at all. This remains the ordinary conversion
    /// path and deliberately has no acknowledgement override; controlled v3
    /// requantisation is admitted separately by CrossRouteCandidateGenerator.
    /// </summary>
    private static bool TryResolvePreparationKind(
        CompatibilitySupportEntry entry,
        WeightQuantisation source,
        TrustedSourceAvailability trustedSource,
        out bool converts,
        out GgufWeightFormat effectiveWeights)
    {
        converts = false;
        effectiveWeights = entry.Weights;

        if (entry.Weights == GgufWeightFormat.Imported)
        {
            return true;
        }

        WeightQuantisation target = GgufWeightFormatMap.ToCanonical(entry.Weights);

        if (target == WeightQuantisation.Unknown || source == WeightQuantisation.Unknown)
        {
            return false;
        }

        // Asking for the encoding the file already has is a no-op, not a
        // conversion, so it must be indistinguishable from the Imported entry
        // that describes the same runtime shape: normalise back to Imported
        // rather than let the two produce separate, duplicate configurations.
        if (target == source)
        {
            effectiveWeights = GgufWeightFormat.Imported;
            return true;
        }

        // Converting upward never restores quality already discarded, so it is
        // never generated as an upgrade. This compares the target against the
        // imported file's own encoding, not against the trusted source's
        // precision, and it is checked before the trusted-source availability
        // below is even consulted. That ordering is deliberate and
        // over-restrictive by choice: TrustedSourceAvailability records only
        // whether a higher-precision source exists, not its actual precision,
        // so an entry asking for Q8_0 is refused here as an "upward" request
        // even on a machine with a genuine F16 trusted source that could have
        // legitimately produced it. Refusing every such case is the
        // conservative failure mode given that the source's own precision is
        // not known to this function.
        if (WeightQuantisationMap.BitsPerWeight(target)
            > WeightQuantisationMap.BitsPerWeight(source))
        {
            return false;
        }

        if (!trustedSource.HasHigherPrecisionSource)
        {
            return false;
        }

        converts = true;
        return true;
    }

    private static IEnumerable<ContextTokenCount> AdmittedContexts(
        CompatibilitySupportEntry entry,
        CandidateGenerationRequest request)
    {
        // DeclaredContextLimit is guaranteed non-null here: Generate refuses
        // with ModelContextLimitNotEstablished before this is ever called.
        int modelLimit = request.Facts.DeclaredContextLimit!.Value;

        IReadOnlyList<ContextTokenCount> ladder = ContextLadderPolicy.Build(
            request.PreservationTarget,
            request.BaselineContext,
            modelLimit,
            entry.MinimumContextTokens);

        // The ladder offers the preservation target even above the model limit,
        // because the user asked for it. Admissibility is decided here.
        return ladder.Where(rung =>
            rung.Tokens >= entry.MinimumContextTokens
            && rung.Tokens <= entry.MaximumContextTokens
            && rung.Tokens <= modelLimit);
    }

    /// <summary>
    /// Why a baseline-matching entry did not admit.
    ///
    /// Keyed on the installation state rather than on the resolved availability,
    /// because availability collapses several distinct situations into
    /// Unsupported and the message a user needs differs in each. An
    /// experimental entry that is not installed was previously told to opt in,
    /// which is advice they cannot act on until the backend is there at all.
    /// </summary>
    private static BaselineExclusionReason WhyNotAdmitted(
        SupportLevel level, InstallationState installation) =>
        installation switch
        {
            InstallationState.NotInstalled =>
                BaselineExclusionReason.BaselineEntryNotInstalled,

            // Installed and working, and the only thing left is consent. That is
            // the one case where "opt in" is the whole of the fix.
            InstallationState.InstalledAndVerified when level == SupportLevel.Experimental =>
                BaselineExclusionReason.BaselineEntryRequiresExperimentalOptIn,

            InstallationState.Unknown =>
                BaselineExclusionReason.BaselineEntrySupportStateUnknown,

            // Every other pairing is an entry that matched but could not be
            // admitted for a reason the installation state does not explain.
            _ => BaselineExclusionReason.BaselineEntrySupportStateUnknown
        };

    /// <summary>
    /// Keeps the reason a user can most directly act on.
    ///
    /// More than one entry can match the baseline shape, and they can fail for
    /// different reasons. Installing a backend is more fundamental than opting
    /// in to one, and both beat reporting that we could not tell - which is
    /// true but leaves the user with nothing to do.
    /// </summary>
    private static BaselineExclusionReason MoreActionable(
        BaselineExclusionReason held, BaselineExclusionReason candidate) =>
        Rank(candidate) < Rank(held) ? candidate : held;

    private static int Rank(BaselineExclusionReason reason) => reason switch
    {
        BaselineExclusionReason.BaselineEntryNotInstalled => 0,
        BaselineExclusionReason.BaselineEntryRequiresExperimentalOptIn => 1,
        BaselineExclusionReason.BaselineEntrySupportStateUnknown => 2,
        _ => 3
    };
}
