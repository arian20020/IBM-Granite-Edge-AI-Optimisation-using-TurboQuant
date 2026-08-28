using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class R3ReleaseVetoTests
{
    [TestMethod]
    public void Evaluate_returns_ready_only_for_full_packaged_native_identity_and_receipt_closure()
    {
        R3ReleaseInputs ready = ReadyInputs();

        Assert.AreEqual(
            R3ReleaseDisposition.ReadyForControlledReleaseReview,
            R3ReleaseVeto.Evaluate(ready));

        R3ReleaseInputs[] missingPredicates =
        [
            ready with { ProductTestFailures = 1 },
            ready with { PackagedDiscovered = 0 },
            ready with { AuthorizedNativeJourneysPassed = 19 },
            ready with { ExactPackageCandidateIdentity = false },
            ready with { OwnedDescendantProcesses = 1 },
            ready with { FinalE1HandoffValid = false },
            ready with { FinalE1NativeReceiptValid = false },
            ready with { NativeReceiptBindsExactHandoffBytes = false },
        ];
        foreach (R3ReleaseInputs missing in missingPredicates)
        {
            Assert.AreEqual(R3ReleaseDisposition.ChangesRequired, R3ReleaseVeto.Evaluate(missing));
        }
    }

    [TestMethod]
    public void Evaluate_gives_product_and_locally_runnable_failures_veto_precedence()
    {
        R3ReleaseInputs blocked = ReadyInputs() with
        {
            PackagedDiscovered = 0,
            AuthorizedNativeJourneysPassed = 0,
            ExactPackageCandidateIdentity = false,
            FinalE1HandoffValid = false,
            FinalE1NativeReceiptValid = false,
            NativeReceiptBindsExactHandoffBytes = false,
            ExternalPrerequisiteIndependentlyProven = true,
            OnlyMissingEvidenceRequiresExternalNativeOrPolicy = true,
        };

        R3ReleaseInputs[] productOrLocalFailures =
        [
            blocked with { VerifiedIssueRecords = 21 },
            blocked with { AllIssuesExecutableGreen = false },
            blocked with { KnownProductDefectRemains = true },
            blocked with { SourceChecksGreen = false },
            blocked with { ManagedChecksGreen = false },
            blocked with { IntegrationChecksGreen = false },
            blocked with { PackageConstructionChecksGreen = false },
            blocked with { IdentityChecksGreen = false },
            blocked with { SecurityChecksGreen = false },
            blocked with { PrivacyChecksGreen = false },
            blocked with { CleanupChecksGreen = false },
            blocked with { ProductTestFailures = 1 },
        ];

        foreach (R3ReleaseInputs failure in productOrLocalFailures)
        {
            Assert.AreEqual(R3ReleaseDisposition.ChangesRequired, R3ReleaseVeto.Evaluate(failure));
        }
    }

    [TestMethod]
    public void Evaluate_allows_external_block_only_when_independently_proven_and_exclusive()
    {
        R3ReleaseInputs blocked = ReadyInputs() with
        {
            PackagedDiscovered = 0,
            AuthorizedNativeJourneysPassed = 0,
            ExactPackageCandidateIdentity = false,
            FinalE1HandoffValid = false,
            FinalE1NativeReceiptValid = false,
            NativeReceiptBindsExactHandoffBytes = false,
            ExternalPrerequisiteIndependentlyProven = true,
            OnlyMissingEvidenceRequiresExternalNativeOrPolicy = true,
        };

        Assert.AreEqual(
            R3ReleaseDisposition.BlockedByExternalEnvironment,
            R3ReleaseVeto.Evaluate(blocked));
        Assert.AreEqual(
            R3ReleaseDisposition.ChangesRequired,
            R3ReleaseVeto.Evaluate(blocked with { ExternalPrerequisiteIndependentlyProven = false }));
        Assert.AreEqual(
            R3ReleaseDisposition.ChangesRequired,
            R3ReleaseVeto.Evaluate(blocked with { OnlyMissingEvidenceRequiresExternalNativeOrPolicy = false }));
    }

    private static R3ReleaseInputs ReadyInputs() => new(
        VerifiedIssueRecords: 22,
        AllIssuesExecutableGreen: true,
        KnownProductDefectRemains: false,
        SourceChecksGreen: true,
        ManagedChecksGreen: true,
        IntegrationChecksGreen: true,
        PackageConstructionChecksGreen: true,
        IdentityChecksGreen: true,
        SecurityChecksGreen: true,
        PrivacyChecksGreen: true,
        CleanupChecksGreen: true,
        ProductTestFailures: 0,
        PackagedDiscovered: 67,
        AuthorizedNativeJourneysRequired: 37,
        AuthorizedNativeJourneysPassed: 37,
        ExactPackageCandidateIdentity: true,
        OwnedDescendantProcesses: 0,
        FinalE1HandoffValid: true,
        FinalE1NativeReceiptValid: true,
        NativeReceiptBindsExactHandoffBytes: true,
        ExternalPrerequisiteIndependentlyProven: false,
        OnlyMissingEvidenceRequiresExternalNativeOrPolicy: false);
}
