using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Controls;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportPrivacyBoundaryTests
{
    [TestMethod]
    public void PresentationDiagnosticEventAndNavigationContracts_ExposeNoPathBearingProperty()
    {
        Type[] pathPrivateTypes =
        [
            typeof(ImportedModelCardData),
            typeof(ModelQuickScanFailureDiagnostic),
            typeof(ModelSelectionDiagnostic),
            typeof(ModelSelectionResult),
            typeof(OpenVinoInspectionRequestedEventArgs),
            typeof(SourceModelConversionRequestedEventArgs),
        ];

        foreach (Type type in pathPrivateTypes)
        {
            Assert.IsFalse(
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(property => property.Name.Contains("path", StringComparison.OrdinalIgnoreCase)),
                $"{type.Name} must not expose an absolute local path.");
        }
    }

    [TestMethod]
    public void PresentationAndDiagnosticValues_UseOnlyTheSafeFilename()
    {
        const string absolutePath = @"C:\\private\\models\\granite.gguf";
        const string fileName = "granite.gguf";
        ModelSelectionOperationId operationId = ModelSelectionOperationId.CreateNew();
        var diagnostic = new ModelSelectionDiagnostic(
            "selection-access-denied",
            "This model could not be opened. Check that it is available on this computer, then try again.");
        ModelSelectionResult result = ModelSelectionResult.Failure(
            operationId,
            absolutePath,
            diagnostic);
        var card = new ImportedModelCardData(
            fileName,
            "Granite",
            "3B",
            "granite",
            "Q4",
            "4 GB",
            "4096");
        var quickScanDiagnostic = new ModelQuickScanFailureDiagnostic(
            fileName,
            "selection-access-denied",
            diagnostic.Message);

        Assert.AreEqual(fileName, result.DisplayName);
        Assert.IsFalse(result.DisplayName.Contains(absolutePath, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(card.FileName.Contains(absolutePath, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(quickScanDiagnostic.SelectedFileName.Contains(absolutePath, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(diagnostic.Message.Contains(absolutePath, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FolderAndConversionEvents_ContainOnlyAnOpaqueOperationAndSafeDisplayName()
    {
        const string absolutePath = @"C:\\private\\models\\source-package";
        const string displayName = "source-package";
        ModelSelectionOperationId operationId = ModelSelectionOperationId.CreateNew();

        EventArgs[] eventArguments =
        [
            new OpenVinoInspectionRequestedEventArgs(operationId, displayName),
            new SourceModelConversionRequestedEventArgs(ModelSelectionResult.Accepted(
                operationId,
                ModelSelectionRoute.SourceModelDirectory,
                displayName)),
        ];

        foreach (object eventArgument in eventArguments)
        {
            string actualDisplayName = eventArgument is SourceModelConversionRequestedEventArgs conversion
                ? conversion.Selection.DisplayName
                : (string)eventArgument.GetType().GetProperty(
                    "DisplayName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                    .GetValue(eventArgument)!;

            Assert.AreEqual(displayName, actualDisplayName);
            Assert.IsFalse(actualDisplayName.Contains(absolutePath, StringComparison.OrdinalIgnoreCase));
        }
    }
}
