using System.Diagnostics.CodeAnalysis;
using GraniteEdgeAI.OpenVino.Contracts;
using ModelInspectionHandoffV2 = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionHandoffV2;
using ModelInspectionOutcomeV2 = GraniteEdgeAI.ModelInspection.Contracts.ModelInspectionOutcomeV2;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

public sealed class OpenVinoInspectionHandoffFactory
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance boundary supports future injection of the app-owned identity source.")]
    public ModelInspectionHandoffV2 Create(
        OpenVinoStaticPackageInspectionResult staticInspection,
        OpenVinoNativeValidationEvidence? nativeValidation,
        Guid modelInspectionRunId)
    {
        ArgumentNullException.ThrowIfNull(staticInspection);
        if (staticInspection.Status != OpenVinoStaticInspectionStatus.NativeValidationRequired ||
            staticInspection.SupportCode is not null ||
            staticInspection.Evidence is null)
        {
            throw new InvalidOperationException("A rejected static inspection cannot issue a handoff.");
        }

        if (nativeValidation is null ||
            !nativeValidation.MainModelParsed ||
            !nativeValidation.TokenizerParsed ||
            !nativeValidation.DetokenizerParsed)
        {
            throw new InvalidOperationException("All three device-neutral native read_model validations are required.");
        }

        OpenVinoStaticPackageEvidence staticEvidence = staticInspection.Evidence;
        if (!string.Equals(nativeValidation.PackageManifestDigest, staticEvidence.PackageManifestDigest, StringComparison.Ordinal) ||
            !string.Equals(nativeValidation.ModelSha256, staticEvidence.ModelSha256, StringComparison.Ordinal) ||
            nativeValidation.ModelLengthBytes != staticEvidence.ModelLengthBytes)
        {
            throw new InvalidOperationException("Native validation does not match the static package identity.");
        }

        if (!IsUuidV4(modelInspectionRunId))
        {
            throw new ArgumentException("The inspection run identity must be a nonzero UUIDv4.", nameof(modelInspectionRunId));
        }

        OpenVinoPackageInspection validatedInspection = new(
            nativeValidation.Outcome,
            nativeValidation.PackageManifestDigest,
            nativeValidation.ModelSha256,
            nativeValidation.ModelLengthBytes);
        try
        {
            validatedInspection.Validate();
        }
        catch (OpenVinoProtocolException exception)
        {
            throw new InvalidOperationException("Native validation evidence is invalid.", exception);
        }

        ModelInspectionHandoffV2 handoff = new(
            ModelInspectionHandoffV2.RequiredSchemaVersion,
            Guid.NewGuid(),
            modelInspectionRunId,
            nativeValidation.Outcome == ModelInspectionOutcome.Ready
                ? ModelInspectionOutcomeV2.Ready
                : ModelInspectionOutcomeV2.ReadyWithWarnings,
            staticEvidence.ModelSha256,
            staticEvidence.ModelLengthBytes);
        byte[] canonical = handoff.ToCanonicalUtf8Json();
        if (canonical.Length > ModelInspectionHandoffV2.MaximumCanonicalUtf8Bytes)
        {
            throw new InvalidOperationException("Canonical model inspection handoff exceeds its bound.");
        }

        return handoff;
    }

    private static bool IsUuidV4(Guid value)
    {
        string text = value.ToString("D");
        return value != Guid.Empty && text[14] == '4' && text[19] is '8' or '9' or 'a' or 'b';
    }
}
