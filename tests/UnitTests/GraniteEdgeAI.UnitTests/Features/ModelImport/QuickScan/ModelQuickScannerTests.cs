// Import the production model-format selection type.
using GraniteEdgeAI.Features.ModelImport.FileImport;

// Import the production quick-scan types.
using GraniteEdgeAI.Features.ModelImport.QuickScan;

// Import MSTest attributes and assertions.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

// Marks this class as a collection of router behaviour tests.
[TestClass]
public sealed class ModelQuickScannerTests
{
    // Verifies that a cancelled format selection returns a cancelled result.
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_None_ReturnsCancelledResult()
    {
        // Arrange: create the normal router and a cancelled format selection.
        ModelQuickScanner scanner = new();

        // Act: scan the cancelled selection with a harmless path.
        ModelQuickScanResult result = await scanner.ScanAsync(
            ModelFormatSelection.None,
            "C:\\Models\\unused.gguf",
            CancellationToken.None);

        // Assert: cancelling format selection must produce a cancelled result.
        Assert.IsNotNull(result);
        Assert.AreEqual(
            ModelQuickScanOutcome.Cancelled,
            result.Outcome);
    }

    // Verifies that cancellation is handled before a path is validated.
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_None_WithEmptyPath_ReturnsCancelledResult()
    {
        // Arrange: create the normal router with a cancelled selection and no path.
        ModelQuickScanner scanner = new();

        // Act: scan the cancelled selection using an empty path.
        ModelQuickScanResult result = await scanner.ScanAsync(
            ModelFormatSelection.None,
            string.Empty,
            CancellationToken.None);

        // Assert: the cancellation route must not reject the unused path.
        Assert.AreEqual(
            ModelQuickScanOutcome.Cancelled,
            result.Outcome);
    }

    // Verifies that the unavailable OpenVINO route returns a clear failure.
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_OpenVino_ReturnsNotImplementedFailure()
    {
        // Arrange: create the normal router for an OpenVINO selection.
        ModelQuickScanner scanner = new();

        // Act: request an OpenVINO scan using a harmless folder path.
        ModelQuickScanResult result = await scanner.ScanAsync(
            ModelFormatSelection.OpenVino,
            "C:\\Models\\OpenVino",
            CancellationToken.None);

        // Assert: the deferred route must report its stable diagnostic code.
        Assert.AreEqual(
            ModelQuickScanOutcome.Failure,
            result.Outcome);
        Assert.AreEqual(
            "openvino-scan-not-implemented",
            result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
    }

    // Verifies that an unexpected enum value returns a controlled failure.
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_UnknownFormat_ReturnsUnsupportedFormatFailure()
    {
        // Arrange: create a value outside the known model-format options.
        ModelQuickScanner scanner = new();
        ModelFormatSelection unknownFormat = (ModelFormatSelection)999;

        // Act: scan the unsupported format using a harmless path.
        ModelQuickScanResult result = await scanner.ScanAsync(
            unknownFormat,
            "C:\\Models\\unknown.model",
            CancellationToken.None);

        // Assert: unexpected values must return useful failure diagnostics.
        Assert.AreEqual(
            ModelQuickScanOutcome.Failure,
            result.Outcome);
        Assert.AreEqual(
            "unsupported-model-format",
            result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "999");
    }

    // Verifies that the GGUF route rejects a missing model path.
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_GgufWithNullPath_ThrowsArgumentNullException()
    {
        // Arrange: create the normal router with a missing GGUF path.
        ModelQuickScanner scanner = new();

        // Act and assert: a null path must be rejected before scanning begins.
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            scanner.ScanAsync(
                ModelFormatSelection.Gguf,
                null!,
                CancellationToken.None));
    }

    // Verifies that the GGUF route rejects blank model paths.
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public async Task ScanAsync_GgufWithEmptyOrWhitespacePath_ThrowsArgumentException(
        string invalidPath)
    {
        // Arrange: create the normal router with an invalid GGUF path.
        ModelQuickScanner scanner = new();

        // Act and assert: blank paths must be rejected before scanning begins.
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            scanner.ScanAsync(
                ModelFormatSelection.Gguf,
                invalidPath,
                CancellationToken.None));
    }

    // TODO: Remove or replace this test when GGUF scanning returns results.
    // Verifies that a valid GGUF path reaches the currently unfinished scanner.
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_GgufWithNonBlankPath_ReachesUnimplementedScanner()
    {
        // Arrange: create the normal router with a valid-looking GGUF path.
        ModelQuickScanner scanner = new();

        // Act and assert: the current GGUF scanner deliberately remains unfinished.
        await Assert.ThrowsExactlyAsync<NotImplementedException>(() =>
            scanner.ScanAsync(
                ModelFormatSelection.Gguf,
                "C:\\Models\\granite-model.gguf",
                CancellationToken.None));
    }

    // Verifies that the dependency-taking constructor rejects a missing scanner.
    [TestMethod]
    [TestCategory("Unit")]
    public void Constructor_NullGgufQuickScanner_ThrowsArgumentNullException()
    {
        // Arrange: the dependency-taking constructor requires a GGUF scanner.

        // Act and assert: a missing dependency must be rejected immediately.
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new ModelQuickScanner(null!));
    }
}
