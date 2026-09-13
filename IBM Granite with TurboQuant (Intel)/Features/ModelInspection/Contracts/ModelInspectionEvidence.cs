using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Aggregates the application-owned evidence graph used by classification and
/// later presentation. It contains no worker messages or native runtime objects.
/// </summary>
internal sealed record ModelInspectionEvidence
{
    /// <summary>
    /// creates immutable evidence and defensively copies all observations
    /// </summary>
    internal ModelInspectionEvidence(
        ModelInspectionFileEvidence file,
        ModelInspectionConfigurationEvidence configuration,
        ModelInspectionTokenizerEvidence tokenizer,
        ModelInspectionChatTemplateEvidence chatTemplate,
        ModelInspectionRuntimeIdentity runtime,
        IEnumerable<ModelInspectionObservation> observations)
    {
        File = file ?? throw new ArgumentNullException(nameof(file));
        Configuration = configuration ??
            throw new ArgumentNullException(nameof(configuration));
        Tokenizer = tokenizer ??
            throw new ArgumentNullException(nameof(tokenizer));
        ChatTemplate = chatTemplate ??
            throw new ArgumentNullException(nameof(chatTemplate));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        ArgumentNullException.ThrowIfNull(observations);

        // copy the source so an already completed result cannot change later
        ModelInspectionObservation[] observationCopy = observations.ToArray();
        if (observationCopy.Any(observation => observation is null))
        {
            throw new ArgumentException(
                "Observations must not contain null entries.",
                nameof(observations));
        }

        Observations =
            new ReadOnlyCollection<ModelInspectionObservation>(observationCopy);
    }

    internal ModelInspectionFileEvidence File { get; }

    internal ModelInspectionConfigurationEvidence Configuration { get; }

    internal ModelInspectionTokenizerEvidence Tokenizer { get; }

    internal ModelInspectionChatTemplateEvidence ChatTemplate { get; }

    internal ModelInspectionRuntimeIdentity Runtime { get; }

    internal IReadOnlyList<ModelInspectionObservation> Observations { get; }
}
