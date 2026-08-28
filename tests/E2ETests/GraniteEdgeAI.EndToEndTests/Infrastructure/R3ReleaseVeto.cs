namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal enum R3ReleaseDisposition
{
    ChangesRequired,
    BlockedByExternalEnvironment,
    ReadyForControlledReleaseReview,
}

internal sealed record R3ReleaseInputs(
    int VerifiedIssueRecords,
    bool AllIssuesExecutableGreen,
    bool KnownProductDefectRemains,
    bool SourceChecksGreen,
    bool ManagedChecksGreen,
    bool IntegrationChecksGreen,
    bool PackageConstructionChecksGreen,
    bool IdentityChecksGreen,
    bool SecurityChecksGreen,
    bool PrivacyChecksGreen,
    bool CleanupChecksGreen,
    int ProductTestFailures,
    int PackagedDiscovered,
    int AuthorizedNativeJourneysRequired,
    int AuthorizedNativeJourneysPassed,
    bool ExactPackageCandidateIdentity,
    int OwnedDescendantProcesses,
    bool FinalE1HandoffValid,
    bool FinalE1NativeReceiptValid,
    bool NativeReceiptBindsExactHandoffBytes,
    bool ExternalPrerequisiteIndependentlyProven,
    bool OnlyMissingEvidenceRequiresExternalNativeOrPolicy);

internal static class R3ReleaseVeto
{
    internal static R3ReleaseDisposition Evaluate(R3ReleaseInputs inputs)
    {
        bool allLocallyRunnableChecksGreen = inputs.SourceChecksGreen
            && inputs.ManagedChecksGreen
            && inputs.IntegrationChecksGreen
            && inputs.PackageConstructionChecksGreen
            && inputs.IdentityChecksGreen
            && inputs.SecurityChecksGreen
            && inputs.PrivacyChecksGreen
            && inputs.CleanupChecksGreen
            && inputs.OwnedDescendantProcesses == 0;
        bool productAndEvidenceGreen = inputs.VerifiedIssueRecords == 22
            && inputs.AllIssuesExecutableGreen
            && !inputs.KnownProductDefectRemains
            && inputs.ProductTestFailures == 0;
        if (!productAndEvidenceGreen || !allLocallyRunnableChecksGreen)
        {
            return R3ReleaseDisposition.ChangesRequired;
        }

        bool ready = inputs.PackagedDiscovered > 0
            && inputs.AuthorizedNativeJourneysRequired > 0
            && inputs.AuthorizedNativeJourneysPassed == inputs.AuthorizedNativeJourneysRequired
            && inputs.ExactPackageCandidateIdentity
            && inputs.OwnedDescendantProcesses == 0
            && inputs.FinalE1HandoffValid
            && inputs.FinalE1NativeReceiptValid
            && inputs.NativeReceiptBindsExactHandoffBytes;
        if (ready)
        {
            return R3ReleaseDisposition.ReadyForControlledReleaseReview;
        }

        if (inputs.ExternalPrerequisiteIndependentlyProven
            && inputs.OnlyMissingEvidenceRequiresExternalNativeOrPolicy)
        {
            return R3ReleaseDisposition.BlockedByExternalEnvironment;
        }

        return R3ReleaseDisposition.ChangesRequired;
    }
}
