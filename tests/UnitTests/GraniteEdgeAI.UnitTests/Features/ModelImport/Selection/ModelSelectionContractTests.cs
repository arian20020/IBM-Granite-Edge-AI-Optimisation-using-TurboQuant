using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Linq;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelSelectionContractTests
{
    [TestMethod]
    public void Routes_DeclareTheSupportedSelectionDestinations()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                ModelSelectionRoute.Gguf,
                ModelSelectionRoute.OpenVinoDirectory,
                ModelSelectionRoute.SourceModelDirectory,
            },
            Enum.GetValues<ModelSelectionRoute>());
    }

    [TestMethod]
    public void Failure_ExposesSafeDisplayNameWithoutALocalPath()
    {
        ModelSelectionOperationId operationId = ModelSelectionOperationId.CreateNew();
        var diagnostic = new ModelSelectionDiagnostic(
            "unsupported-selection",
            "This item cannot be imported.");

        ModelSelectionResult result = ModelSelectionResult.Failure(
            operationId,
            "model.gguf",
            diagnostic);

        Assert.AreEqual("model.gguf", result.DisplayName);
        Assert.AreEqual(diagnostic, result.Diagnostic);
        Assert.IsFalse(
            typeof(ModelSelectionResult)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(property => property.Name.Contains("path", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Accepted_ResultHasRouteAndNoDiagnostic()
    {
        ModelSelectionOperationId operationId = ModelSelectionOperationId.CreateNew();

        ModelSelectionResult result = ModelSelectionResult.Accepted(
            operationId,
            ModelSelectionRoute.Gguf,
            "model.gguf");

        Assert.AreEqual(operationId, result.OperationId);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ModelSelectionRoute.Gguf, result.Route);
        Assert.AreEqual("model.gguf", result.DisplayName);
        Assert.IsNull(result.Diagnostic);
    }

    [TestMethod]
    public void Failure_ResultHasDiagnosticAndNoRoute()
    {
        ModelSelectionOperationId operationId = ModelSelectionOperationId.CreateNew();
        var diagnostic = new ModelSelectionDiagnostic(
            "invalid-selection",
            "Choose a supported model.");

        ModelSelectionResult result = ModelSelectionResult.Failure(
            operationId,
            "model-folder",
            diagnostic);

        Assert.AreEqual(operationId, result.OperationId);
        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.Route);
        Assert.AreEqual("model-folder", result.DisplayName);
        Assert.AreEqual(diagnostic, result.Diagnostic);
    }

    [TestMethod]
    public void Contracts_ExposeNoWritableInstanceProperties()
    {
        Type[] contractTypes =
        [
            typeof(ModelSelectionOperationId),
            typeof(ModelSelectionInput),
            typeof(ModelSelectionDiagnostic),
            typeof(ModelSelectionResult),
        ];

        foreach (Type contractType in contractTypes)
        {
            PropertyInfo[] writableProperties = contractType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.SetMethod is not null)
                .ToArray();

            Assert.AreEqual(
                0,
                writableProperties.Length,
                $"{contractType.Name} must be immutable.");
        }
    }
}
