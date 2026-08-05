using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.IO;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies the immutable navigation contract of ModelInspectionPage.
/// </summary>
[TestClass]
public sealed class ModelInspectionPageNavigationTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void FrameNavigation_WithRequest_StoresRequestAndDerivedPath()
    {
        ModelInspectionRequest request = CreateRequest(
            @"C:\Models\granite-4.1-3b-instruct.gguf");
        var frame = new Frame();

        bool navigationSucceeded = frame.Navigate(
            typeof(ModelInspectionPage),
            request);

        var inspectionPage = frame.Content as ModelInspectionPage;

        Assert.IsTrue(navigationSucceeded);
        Assert.IsNotNull(inspectionPage);
        Assert.AreSame(request, inspectionPage.Request);
        Assert.AreEqual(request.ModelPath, inspectionPage.SelectedModelPath);
    }

    private static ModelInspectionRequest CreateRequest(string modelPath)
    {
        return new ModelInspectionRequest(
            modelPath,
            Path.GetFileName(modelPath),
            new ExpectedModelFileIdentity(
                lengthBytes: 64,
                lastWriteTimeUtc: new DateTimeOffset(
                    2026,
                    8,
                    5,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)),
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite 4.1 3B Instruct",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 64,
                declaredContextLength: 131_072,
                ggufVersion: 3));
    }
}
