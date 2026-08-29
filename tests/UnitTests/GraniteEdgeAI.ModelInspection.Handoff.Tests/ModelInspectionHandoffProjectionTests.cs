using System.Reflection;
using System.Text;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Handoff.Tests;

[TestClass]
public sealed class ModelInspectionHandoffProjectionTests
{
    private static readonly Guid RunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    [TestMethod]
    public void LiveRegistryIssuanceRetainsExactPathPrivateSchemaV2Projection()
    {
        using var registry = new ModelInspectionHandoffRegistry();
        registry.ActivateModelRun(RunId);
        Assert.IsTrue(registry.TryIssue(
            RunId,
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(ModelInspectionOutcome.Ready)),
            out ModelInspectionHandoff? handoff));
        Assert.IsNotNull(handoff);

        MethodInfo? lookup = typeof(ModelInspectionHandoffRegistry).GetMethod(
            "TryGetProjection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(
            lookup,
            "The live GGUF registry does not retain the schema-v2 projection.");
        object?[] arguments = [handoff.ModelInspectionHandoffId, null];
        Assert.AreEqual(true, lookup.Invoke(registry, arguments));
        ModelInspectionProjectionV2 projection =
            (ModelInspectionProjectionV2)arguments[1]!;

        Assert.AreEqual(handoff.ModelInspectionHandoffId,
            projection.ModelInspectionHandoff.ModelInspectionHandoffId);
        Assert.AreEqual(handoff.ModelInspectionRunId,
            projection.ModelInspectionHandoff.ModelInspectionRunId);
        Assert.AreEqual(handoff.ModelSha256,
            projection.ModelInspectionHandoff.ModelSha256);
        Assert.AreEqual(handoff.ModelLengthBytes,
            projection.ModelInspectionHandoff.ModelLengthBytes);
        Assert.AreEqual(ModelInspectionRoute.Gguf, projection.ModelSource.Route);
        Assert.IsFalse(
            Encoding.UTF8.GetString(projection.ToCanonicalUtf8Json())
                .Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void InvalidatedRegistryEntryDoesNotExposeProjection()
    {
        using var registry = new ModelInspectionHandoffRegistry();
        registry.ActivateModelRun(RunId);
        Assert.IsTrue(registry.TryIssue(
            RunId,
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(ModelInspectionOutcome.Ready)),
            out ModelInspectionHandoff? handoff));
        Assert.IsNotNull(handoff);
        registry.Invalidate(handoff.ModelInspectionHandoffId);

        MethodInfo? lookup = typeof(ModelInspectionHandoffRegistry).GetMethod(
            "TryGetProjection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(lookup);
        object?[] arguments = [handoff.ModelInspectionHandoffId, null];
        Assert.AreEqual(false, lookup.Invoke(registry, arguments));
        Assert.IsNull(arguments[1]);
    }
}
