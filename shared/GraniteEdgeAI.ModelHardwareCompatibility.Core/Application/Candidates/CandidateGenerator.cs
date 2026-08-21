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
                [], false, BaselineExclusionReason.SupportMatrixUnavailable);
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

        foreach (CompatibilitySupportEntry entry in request.Matrix.Entries)
        {
            InstallationState installation =
                request.InstallationStates.TryGetValue(entry.EntryId, out InstallationState state)
                    ? state
                    : InstallationState.Unknown;

            SupportAvailability availability =
                SupportMatrixResolver.Resolve(entry.Level, installation);

            if (availability is not (SupportAvailability.Available
                or SupportAvailability.ExperimentalAvailable))
            {
                continue;
            }

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

            if (configuration == request.BaselineConfiguration)
            {
                baselineConfigurationAdmitted = true;
            }

            foreach (ContextTokenCount context in AdmittedContexts(entry, request))
            {
                bool isBaselineShape =
                    configuration == request.BaselineConfiguration
                    && context == request.BaselineContext;

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
            return new CandidateGenerationResult(
                candidates,
                false,
                baselineConfigurationAdmitted
                    ? BaselineExclusionReason.BaselineContextOutsideEntryBounds
                    : BaselineExclusionReason.NoAdmittedEntryMatchesTheBaseline);
        }

        // The baseline is always evaluated first: it is what the user already
        // has, and every alternative is judged relative to it.
        return new CandidateGenerationResult(
            [baseline, .. candidates], true, BaselineExclusionReason.None);
    }

    /// <summary>
    /// Decides whether an entry's weight format is reachable from the imported
    /// file, whether reaching it writes a new artifact, and the weight format the
    /// resulting configuration is actually built from. Returns false when the
    /// entry must not be offered at all.
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
        // never generated as an upgrade.
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
        int modelLimit = request.Facts.DeclaredContextLimit ?? entry.MaximumContextTokens;

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
}
