using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Tests.Inspection;

[TestClass]
public sealed class OpenVinoInspectionHandoffFactoryTests
{
    private const string ManifestDigest = "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";
    private const string ModelDigest = "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";
    private static readonly Guid RunId = Guid.Parse("52412daa-dfc7-4f60-8a3d-0b74bad83663");
    private static readonly string[] HandoffFieldNames =
        ["schemaVersion", "modelInspectionHandoffId", "modelInspectionRunId", "outcome", "modelSha256", "modelLengthBytes"];

    [TestMethod]
    public void StaticEvidenceAloneCannotIssueAHandoff()
    {
        OpenVinoStaticPackageInspectionResult staticResult = StaticResult();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new OpenVinoInspectionHandoffFactory().Create(staticResult, nativeValidation: null, RunId));
    }

    [TestMethod]
    public void AllThreeNativeReadModelProofsAreRequired()
    {
        OpenVinoStaticPackageInspectionResult staticResult = StaticResult();
        OpenVinoNativeValidationEvidence incomplete = NativeEvidence(mainModelParsed: true, tokenizerParsed: true, detokenizerParsed: false);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new OpenVinoInspectionHandoffFactory().Create(staticResult, incomplete, RunId));
    }

    [TestMethod]
    public void NativeProofMustMatchTheStaticManifestAndModelIdentity()
    {
        OpenVinoStaticPackageInspectionResult staticResult = StaticResult();
        OpenVinoNativeValidationEvidence mismatched = NativeEvidence() with
        {
            PackageManifestDigest = new string('a', 64)
        };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new OpenVinoInspectionHandoffFactory().Create(staticResult, mismatched, RunId));
    }

    [TestMethod]
    [DataRow(ModelInspectionOutcome.Ready)]
    [DataRow(ModelInspectionOutcome.ReadyWithWarnings)]
    public void ExplicitNativeValidatedOutcomeIssuesOnlyTheSixFieldCanonicalHandoff(ModelInspectionOutcome outcome)
    {
        OpenVinoStaticPackageInspectionResult staticResult = StaticResult();
        OpenVinoNativeValidationEvidence native = NativeEvidence() with { Outcome = outcome };

        ModelInspectionHandoffV2 handoff = new OpenVinoInspectionHandoffFactory().Create(staticResult, native, RunId);
        byte[] payload = handoff.ToCanonicalUtf8Json();

        Assert.AreEqual(ModelInspectionHandoffV2.RequiredSchemaVersion, handoff.SchemaVersion);
        Assert.AreEqual(RunId, handoff.ModelInspectionRunId);
        Assert.AreEqual(outcome, handoff.Outcome);
        Assert.AreEqual(ModelDigest, handoff.ModelSha256);
        Assert.AreEqual(88L, handoff.ModelLengthBytes);
        Assert.IsTrue(IsUuidV4(handoff.ModelInspectionHandoffId));
        Assert.IsTrue(payload.Length <= ModelInspectionHandoffV2.MaximumCanonicalUtf8Bytes);
        CollectionAssert.AreEquivalent(
            HandoffFieldNames,
            System.Text.Json.JsonDocument.Parse(payload).RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.IsFalse(System.Text.Encoding.UTF8.GetString(payload).Contains("manifest", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(handoff, ModelInspectionHandoffV2.Parse(payload));
    }

    [TestMethod]
    public void EachIssueUsesAFreshNonzeroLowercaseUuidV4()
    {
        OpenVinoInspectionHandoffFactory factory = new();
        ModelInspectionHandoffV2 first = factory.Create(StaticResult(), NativeEvidence(), RunId);
        ModelInspectionHandoffV2 second = factory.Create(StaticResult(), NativeEvidence(), RunId);

        Assert.AreNotEqual(first.ModelInspectionHandoffId, second.ModelInspectionHandoffId);
        Assert.IsTrue(IsUuidV4(first.ModelInspectionHandoffId));
        Assert.IsTrue(IsUuidV4(second.ModelInspectionHandoffId));
        Assert.AreEqual(first.ModelInspectionHandoffId.ToString("D"), first.ModelInspectionHandoffId.ToString("D").ToLowerInvariant());
    }

    [TestMethod]
    public void CanonicalPayloadOver512BytesIsRejected()
    {
        ModelInspectionHandoffV2 handoff = new OpenVinoInspectionHandoffFactory().Create(StaticResult(), NativeEvidence(), RunId);
        byte[] canonical = handoff.ToCanonicalUtf8Json();
        byte[] oversized = new byte[ModelInspectionHandoffV2.MaximumCanonicalUtf8Bytes + 1];
        canonical.CopyTo(oversized, 0);
        Array.Fill(oversized, (byte)' ', canonical.Length, oversized.Length - canonical.Length);

        Assert.ThrowsExactly<OpenVinoProtocolException>(() => ModelInspectionHandoffV2.Parse(oversized));
    }

    [TestMethod]
    public void RejectedStaticResultAndInvalidRunIdentityCannotIssue()
    {
        OpenVinoStaticPackageInspectionResult rejected = OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageMissingResource);
        OpenVinoInspectionHandoffFactory factory = new();

        Assert.ThrowsExactly<InvalidOperationException>(() => factory.Create(rejected, NativeEvidence(), RunId));
        Assert.ThrowsExactly<ArgumentException>(() => factory.Create(StaticResult(), NativeEvidence(), Guid.Empty));
    }

    private static OpenVinoStaticPackageInspectionResult StaticResult() =>
        OpenVinoStaticPackageInspectionResult.NativeValidationRequired(new OpenVinoStaticPackageEvidence(
            OpenVinoPackagePolicy.PolicyVersion,
            ManifestDigest,
            ModelDigest,
            88,
            "granite",
            "GraniteForCausalLM",
            "text-generation-with-past",
            64,
            "float32",
            "PreTrainedTokenizerFast",
            9,
            HasChatTemplate: false));

    private static OpenVinoNativeValidationEvidence NativeEvidence(
        bool mainModelParsed = true,
        bool tokenizerParsed = true,
        bool detokenizerParsed = true) =>
        new(
            ModelInspectionOutcome.ReadyWithWarnings,
            ManifestDigest,
            ModelDigest,
            88,
            mainModelParsed,
            tokenizerParsed,
            detokenizerParsed);

    private static bool IsUuidV4(Guid value)
    {
        string text = value.ToString("D");
        return value != Guid.Empty && text[14] == '4' && text[19] is '8' or '9' or 'a' or 'b';
    }
}
