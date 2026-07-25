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

    /// <summary>
    /// Verifies that the GGUF route returns the complete Granite fixture result.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_GgufWithValidFixture_ReturnsSuccessResult()
    {
        // Arrange: resolve the deployed complete-metadata fixture.
        ModelQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            "V-001-complete-metadata-v3.gguf");

        Assert.IsTrue(
            File.Exists(fixturePath),
            $"Expected deployed fixture was not found: {fixturePath}");

        // Act: route the valid fixture through the model quick scanner.
        ModelQuickScanResult result = await scanner.ScanAsync(
            ModelFormatSelection.Gguf,
            fixturePath,
            CancellationToken.None);

        // Assert: the router returns every expected Granite metadata field.
        Assert.AreEqual(ModelQuickScanOutcome.Success, result.Outcome);
        Assert.AreEqual("IBM Granite Fixture Model", result.ModelName);
        Assert.AreEqual("granite", result.Architecture);
        Assert.AreEqual("3B", result.ParameterSizeLabel);
        Assert.AreEqual("Q4_K_M", result.Quantization);
        Assert.AreEqual(320L, result.FileSizeBytes);
        Assert.AreEqual(131_072UL, result.ContextLength);
        Assert.AreEqual(3U, result.GgufVersion);
        Assert.IsNull(result.FailureCode);
        Assert.IsNull(result.UserMessage);
        Assert.IsNull(result.TechnicalMessage);
    }

    /// <summary>
    /// Verifies that the GGUF route preserves scanner failure diagnostics.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_GgufWithInvalidFixture_ReturnsScannerFailure()
    {
        // Arrange: resolve the deployed invalid-magic fixture.
        ModelQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-001-invalid-magic.gguf");

        Assert.IsTrue(
            File.Exists(fixturePath),
            $"Expected deployed fixture was not found: {fixturePath}");

        // Act: route the malformed fixture through the model quick scanner.
        ModelQuickScanResult result = await scanner.ScanAsync(
            ModelFormatSelection.Gguf,
            fixturePath,
            CancellationToken.None);

        // Assert: the format scanner's stable failure reaches the caller.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("invalid-magic", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
    }

    /// <summary>
    /// Verifies that requested GGUF cancellation becomes a cancelled result.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_GgufWithPreCancelledToken_ReturnsCancelledResult()
    {
        // Arrange: cancel before routing a harmless nonblank path.
        ModelQuickScanner scanner = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        // Act: route the already-cancelled request.
        ModelQuickScanResult result = await scanner.ScanAsync(
            ModelFormatSelection.Gguf,
            "C:\\Models\\unused.gguf",
            cancellation.Token);

        // Assert: expected cancellation is returned rather than thrown.
        Assert.AreEqual(ModelQuickScanOutcome.Cancelled, result.Outcome);
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
