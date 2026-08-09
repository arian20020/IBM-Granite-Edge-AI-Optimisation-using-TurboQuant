using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Classification;

/// <summary>
/// Classifies one trusted, completed GGUF inspection evidence graph.
/// </summary>
internal interface IModelInspectionClassifier
{
    ModelInspectionResult Classify(
        ModelInspectionEvidence evidence,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc);
}
