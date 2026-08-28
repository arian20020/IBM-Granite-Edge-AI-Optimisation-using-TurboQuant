using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AppOutcome = GraniteEdgeAI.Features.ModelInspection.Contracts.ModelInspectionOutcome;
using OpenVinoOutcome = GraniteEdgeAI.OpenVino.Contracts.ModelInspectionOutcome;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionProjectionFactoryTests
{
    private static readonly Guid RunId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    [TestMethod]
    public void GgufAndOpenVinoRouteValidatorsProduceTheSharedSchemaV2Projection()
    {
        ModelInspectionExecutionResult ggufTerminal =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(AppOutcome.Ready));

        Assert.IsTrue(ModelInspectionProjectionFactory.TryCreateGguf(
            RunId,
            RunId,
            ggufTerminal,
            out ModelInspectionProjectionV2? gguf));
        ModelInspectionProjectionV2 openVino =
            ModelInspectionProjectionFactory.CreateOpenVino(
                StaticResult(),
                NativeEvidence(),
                RunId);

        Assert.IsNotNull(gguf);
        Assert.AreEqual(ModelInspectionRoute.Gguf, gguf.ModelSource.Route);
        Assert.AreEqual("gguf", gguf.ModelSource.ModelType);
        Assert.AreEqual(ModelInspectionRoute.OpenVino, openVino.ModelSource.Route);
        Assert.AreEqual("openvino-ir", openVino.ModelSource.ModelType);
        Assert.AreEqual(
            gguf.ModelInspectionResult.ModelInspectionRunId,
            gguf.ModelInspectionHandoff.ModelInspectionRunId);
        Assert.AreEqual(
            openVino.ModelInspectionResult.ModelInspectionRunId,
            openVino.ModelInspectionHandoff.ModelInspectionRunId);
        Assert.IsFalse(
            System.Text.Encoding.UTF8.GetString(gguf.ToCanonicalUtf8Json())
                .Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(
            System.Text.Encoding.UTF8.GetString(openVino.ToCanonicalUtf8Json())
                .Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RouteSpecificValidationRejectsStaleOrMismatchedEvidenceBeforeProjection()
    {
        ModelInspectionExecutionResult ggufTerminal =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(AppOutcome.Ready));
        OpenVinoNativeValidationEvidence mismatched = NativeEvidence() with
        {
            ModelSha256 = new string('b', 64)
        };

        Assert.IsFalse(ModelInspectionProjectionFactory.TryCreateGguf(
            RunId,
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            ggufTerminal,
            out _));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ModelInspectionProjectionFactory.CreateOpenVino(
                StaticResult(),
                mismatched,
                RunId));
    }

    private static OpenVinoStaticPackageInspectionResult StaticResult() =>
        OpenVinoStaticPackageInspectionResult.NativeValidationRequired(
            new OpenVinoStaticPackageEvidence(
                OpenVinoPackagePolicy.PolicyVersion,
                new string('c', 64),
                new string('d', 64),
                88,
                "granite",
                "GraniteForCausalLM",
                "text-generation-with-past",
                64,
                "float32",
                "PreTrainedTokenizerFast",
                9,
                HasChatTemplate: false));

    private static OpenVinoNativeValidationEvidence NativeEvidence() =>
        new(
            OpenVinoOutcome.ReadyWithWarnings,
            new string('c', 64),
            new string('d', 64),
            88,
            MainModelParsed: true,
            TokenizerParsed: true,
            DetokenizerParsed: true);
}
