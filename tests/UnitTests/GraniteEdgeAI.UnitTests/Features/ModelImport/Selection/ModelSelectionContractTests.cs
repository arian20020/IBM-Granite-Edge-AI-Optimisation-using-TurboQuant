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
    public void Results_NormalizeRootedAndUncDisplayNamesToBasenames()
    {
        ModelSelectionOperationId operationId = ModelSelectionOperationId.CreateNew();
        var diagnostic = new ModelSelectionDiagnostic(
            "unsupported-selection",
            "This item cannot be imported.");

        ModelSelectionResult rootedResult = ModelSelectionResult.Accepted(
            operationId,
            ModelSelectionRoute.Gguf,
            @"C:\private\models\model.gguf");
        ModelSelectionResult uncResult = ModelSelectionResult.Failure(
            operationId,
            @"\\server\private\model.gguf",
            diagnostic);

        Assert.AreEqual("model.gguf", rootedResult.DisplayName);
        Assert.AreEqual("model.gguf", uncResult.DisplayName);
    }

    [TestMethod]
    public void Diagnostic_RejectsMessagesContainingRootedOrUncPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic(
                "selection-failed",
                @"Could not read C:\private\models\model.gguf."));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic(
                "selection-failed",
                @"Could not read \\server\private\model.gguf."));
    }

    [TestMethod]
    public void Diagnostic_RejectsDriveRootsFollowingLabels()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic(
                "selection-failed",
                @"Path=C:\private\model.gguf"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic(
                "selection-failed",
                @"failed:C:\private\model.gguf"));
    }

    [TestMethod]
    public void Diagnostic_RejectsMessagesContainingCurrentDriveRootedPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic(
                "selection-failed",
                @"Could not read \private\models\model.gguf."));
    }

    [TestMethod]
    public void Diagnostic_RejectsMessagesContainingUnixRootedPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic(
                "selection-failed",
                "Could not read /private/models/model.gguf."));
    }

    [TestMethod]
    public void Diagnostic_RejectsMessagesContainingSlashUncPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic(
                "selection-failed",
                "Could not read //server/share/model.gguf."));
    }

    [TestMethod]
    public void Diagnostic_AcceptsSafeUrlsAndRelativeFilenameText()
    {
        var urlDiagnostic = new ModelSelectionDiagnostic(
            "selection-help",
            "See https://example.com/help for details.");
        var fileDiagnostic = new ModelSelectionDiagnostic(
            "selection-help",
            "Review model.gguf and try again.");

        Assert.AreEqual("See https://example.com/help for details.", urlDiagnostic.Message);
        Assert.AreEqual("Review model.gguf and try again.", fileDiagnostic.Message);
    }

    [TestMethod]
    public void Contracts_RejectNullOrWhitespaceSafeValuesAndMissingFailureDiagnostic()
    {
        ModelSelectionOperationId operationId = ModelSelectionOperationId.CreateNew();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelSelectionResult.Accepted(
                operationId,
                ModelSelectionRoute.Gguf,
                null!));
        Assert.ThrowsExactly<ArgumentException>(() =>
            ModelSelectionResult.Accepted(
                operationId,
                ModelSelectionRoute.Gguf,
                "   "));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ModelSelectionDiagnostic(null!, "A safe message."));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic("   ", "A safe message."));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ModelSelectionDiagnostic("selection-failed", null!));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelSelectionDiagnostic("selection-failed", "   "));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelSelectionResult.Failure(
                operationId,
                "model.gguf",
                null!));
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
