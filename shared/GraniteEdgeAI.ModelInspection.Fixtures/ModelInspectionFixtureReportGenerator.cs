using System.Text;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public sealed record VerifiedModelInspectionFixtureExternalEvidence(
    string FixtureId,
    string EvidenceId);

public static class ModelInspectionFixtureReportGenerator
{
    private const string PolicyFileName =
        "model-inspection-fixture-coverage-policy.json";
    private const string ExternalEvidencePath = "$.externalEvidenceLinks";
    private const string ExternalEvidenceRule = "report.external-evidence";
    private const string SyntheticProvenance =
        "synthetic deterministic fixture";
    private const string RealWorkerProvenance =
        "N-001 real-worker coverage";
    private const string SemanticRenderStatus = "Verified(Task7)";
    private const string LifetimeStatus = "Verified(Task9)";

    private static readonly Encoding Utf8WithoutBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] GenerateUtf8(
        ValidatedModelInspectionFixtureCoverageCatalogue catalogue,
        IReadOnlyList<VerifiedModelInspectionFixtureExternalEvidence>
            verifiedExternalEvidence)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(verifiedExternalEvidence);
        ModelInspectionFixtureCatalogue value = catalogue.Catalogue;
        IReadOnlyList<ModelInspectionFixtureExternalEvidenceLink> expectedEvidence =
            value.Policy.Value.ExternalEvidenceLinks;
        ValidateExternalEvidence(expectedEvidence, verifiedExternalEvidence);

        Dictionary<string, ModelInspectionFixtureExternalEvidenceLink> evidenceById =
            expectedEvidence.ToDictionary(
                link => link.FixtureId,
                StringComparer.Ordinal);
        var report = new StringBuilder(capacity: 32768);
        AppendLine(report, "# Model Inspection Fixture Catalog");
        AppendLine(report, string.Empty);
        AppendLine(
            report,
            "This report is generated deterministically from the validated Model " +
            "Inspection fixture catalogue and separately verified external-evidence " +
            "joins.");
        AppendLine(report, string.Empty);
        AppendLine(
            report,
            "All gallery rows describe synthetic data; separately verified " +
            "real-worker evidence is identified only in the final column. " +
            "Verified(Task7) means Debug/x64 loaded-tree semantic/render " +
            "coverage, not strict approved-Figma-PNG, actual OS High " +
            "Contrast/200% text-scale, or Narrator evidence. Verified(Task9) " +
            "means declared synthetic interaction/lifetime coverage, not " +
            "real-worker execution.");
        AppendLine(report, string.Empty);
        AppendLine(
            report,
            "ReadyWithWarnings, ConversionRequired, IncompletePackage, Unsupported, " +
            "Invalid, and operational-failure variants are not " +
            "real-worker-classified fixtures.");
        AppendLine(report, string.Empty);
        AppendLine(
            report,
            "| ID | Filename | Target condition | Category | Coverage tags | " +
            "Initial screen | Expected screen | Interactions | Presets | " +
            "Semantic/render | Lifetime | Provenance | Real-worker evidence |");
        AppendLine(
            report,
            "|---|---|---|---|---|---|---|---|---|---|---|---|---|");

        for (int index = 0; index < value.Fixtures.Count; index++)
        {
            ValidatedModelInspectionFixture fixture = value.Fixtures[index];
            ModelInspectionFixturePolicyEntry policy =
                value.Policy.Value.Fixtures[index];
            string interactions = string.Join(
                "; ",
                fixture.Interactions.Select(interaction =>
                    $"{interaction.Id}:{interaction.Kind}:" +
                    $"{interaction.SourceCheckpoint}->{interaction.Target}"));
            string realWorkerEvidence = evidenceById.TryGetValue(
                fixture.Id,
                out ModelInspectionFixtureExternalEvidenceLink? link)
                    ? $"{RealWorkerProvenance}; {link.SourcePath}; {link.JourneyTest}"
                    : "None";

            AppendLine(
                report,
                $"| {fixture.Id} | {fixture.FileName} | " +
                $"{fixture.TargetCondition} | {fixture.Category} | " +
                $"{string.Join("; ", policy.RequiredCoverageTags)} | " +
                $"InspectionProgress | {fixture.Expected.Figma.State} | " +
                $"{interactions} | {string.Join("; ", fixture.Presets)} | " +
                $"{SemanticRenderStatus} | {LifetimeStatus} | " +
                $"{SyntheticProvenance} | " +
                $"{realWorkerEvidence} |");
        }

        return Utf8WithoutBom.GetBytes(report.ToString());
    }

    private static void ValidateExternalEvidence(
        IReadOnlyList<ModelInspectionFixtureExternalEvidenceLink> expected,
        IReadOnlyList<VerifiedModelInspectionFixtureExternalEvidence> actual)
    {
        if (actual.Count != expected.Count || actual.Any(item => item is null))
        {
            throw ExternalEvidenceFailure();
        }

        VerifiedModelInspectionFixtureExternalEvidence[] orderedActual = actual
            .OrderBy(item => item.FixtureId, StringComparer.Ordinal)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();
        ModelInspectionFixtureExternalEvidenceLink[] orderedExpected = expected
            .OrderBy(item => item.FixtureId, StringComparer.Ordinal)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();

        for (int index = 0; index < orderedExpected.Length; index++)
        {
            if (!string.Equals(
                    orderedActual[index].FixtureId,
                    orderedExpected[index].FixtureId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    orderedActual[index].EvidenceId,
                    orderedExpected[index].EvidenceId,
                    StringComparison.Ordinal))
            {
                throw ExternalEvidenceFailure();
            }
        }
    }

    private static void AppendLine(StringBuilder destination, string value)
    {
        destination.Append(value);
        destination.Append('\n');
    }

    private static ModelInspectionFixtureValidationException
        ExternalEvidenceFailure() => new(
            PolicyFileName,
            ExternalEvidencePath,
            ExternalEvidenceRule);
}
