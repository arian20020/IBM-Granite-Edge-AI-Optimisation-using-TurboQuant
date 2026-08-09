using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Classification;

/// <summary>
/// Produces the two model outcomes currently justified by the trusted GGUF
/// VocabOnly inspection route.
/// </summary>
internal sealed class ModelInspectionClassifier : IModelInspectionClassifier
{
    private const string MissingChatTemplateCode =
        "MI-WARN-CHAT-TEMPLATE-MISSING";

    public ModelInspectionResult Classify(
        ModelInspectionEvidence evidence,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.Tokenizer.TokenizerSmokePassed is not true)
        {
            throw new InvalidOperationException(
                "Completed evidence requires a successful tokenizer smoke check.");
        }

        if (!evidence.ChatTemplate.Present.HasValue)
        {
            throw new InvalidOperationException(
                "Completed evidence requires a known chat-template state.");
        }

        if (evidence.ChatTemplate.Present is true)
        {
            return new ModelInspectionResult(
                outcome: ModelInspectionOutcome.Ready,
                evidence: evidence,
                findings: Array.Empty<ModelInspectionFinding>(),
                summary: "Model inspection completed successfully.",
                recommendedAction: "Continue to Hardware Fit.",
                verifiedConversionRouteId: null,
                startedAtUtc: startedAtUtc,
                completedAtUtc: completedAtUtc);
        }

        ModelInspectionFinding missingTemplateWarning = new(
            code: MissingChatTemplateCode,
            severity: ModelInspectionFindingSeverity.Warning,
            title: "No embedded chat template",
            explanation: "The model does not include an embedded chat template.",
            recommendedAction:
                "Configure and verify a compatible chat template before starting chat.",
            technicalDetail:
                "Completed GGUF evidence reported chat_template_present=false.");

        return new ModelInspectionResult(
            outcome: ModelInspectionOutcome.ReadyWithWarnings,
            evidence: evidence,
            findings: new[] { missingTemplateWarning },
            summary: "Model inspection completed with one warning.",
            recommendedAction: "Review the warning before continuing to Hardware Fit.",
            verifiedConversionRouteId: null,
            startedAtUtc: startedAtUtc,
            completedAtUtc: completedAtUtc);
    }
}
