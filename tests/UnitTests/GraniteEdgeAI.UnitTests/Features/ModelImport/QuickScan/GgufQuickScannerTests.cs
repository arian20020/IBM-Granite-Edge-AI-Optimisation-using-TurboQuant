// Import the production GGUF scanner and result types.
using GraniteEdgeAI.Features.ModelImport.QuickScan;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies the observable behaviour of GgufQuickScanner.
/// </summary>
[TestClass]
public sealed class GgufQuickScannerTests
{
    /// <summary>
    /// Verifies that a null model path is rejected as an invalid API input.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_NullPath_ThrowsArgumentException()
    {
        // Arrange: create the real scanner with no test-specific substitutes.
        GgufQuickScanner scanner = new();

        // Act and assert: the public contract rejects a null path before I/O.
        await Assert.ThrowsAsync<ArgumentException>(
            () => scanner.ScanAsync(null!, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that an empty model path is rejected as an invalid API input.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_EmptyPath_ThrowsArgumentException()
    {
        // Arrange: create the real scanner with no test-specific substitutes.
        GgufQuickScanner scanner = new();

        // Act and assert: the public contract rejects an empty path before I/O.
        await Assert.ThrowsAsync<ArgumentException>(
            () => scanner.ScanAsync(string.Empty, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a whitespace-only model path is rejected as an invalid API input.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_WhitespacePath_ThrowsArgumentException()
    {
        // Arrange: create the real scanner with no test-specific substitutes.
        GgufQuickScanner scanner = new();

        // Act and assert: the public contract rejects whitespace before I/O.
        await Assert.ThrowsAsync<ArgumentException>(
            () => scanner.ScanAsync("   ", CancellationToken.None));
    }

    /// <summary>
    /// Verifies that an already-cancelled scan preserves cancellation rather than opening a file.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange: create the real scanner and request cancellation before scanning.
        GgufQuickScanner scanner = new();
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        // Act and assert: cancellation reaches the caller unchanged before file I/O.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scanner.ScanAsync("not-opened.gguf", cancellationTokenSource.Token));
    }

    /// <summary>
    /// Verifies that an empty GGUF fixture returns a controlled truncated-header failure.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_EmptyFile_ReturnsTruncatedHeaderFailure()
    {
        // Arrange: create the real scanner and locate the deployed empty fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-000-empty-file.gguf");

        // Arrange: prove the packaged test deployment supplied the intended input.
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The empty-header GGUF fixture was not found at: {fixturePath}");

        // Act: scan the fixture, which ends before its first header field.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: a structural header failure is returned instead of an exception.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("truncated-header", result.FailureCode);
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "magic");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "0");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "reading 0 bytes");
    }

    /// <summary>
    /// Verifies that a short file remains a truncated header even when its magic is invalid.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ShortInvalidMagic_ReturnsTruncatedHeaderFailure()
    {
        // Arrange: create the real scanner and locate the generated short-header fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-024-short-invalid-magic.gguf");

        // Arrange: prove the packaged deployment contains the intended generated input.
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The short invalid-magic fixture was not found at: {fixturePath}");

        // Act: scan a file that has TEST magic but ends in the tensor-count field.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the incomplete 24-byte header takes priority over invalid magic.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("truncated-header", result.FailureCode);
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "tensor count");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 8");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "reading 4 bytes");
    }

    /// <summary>
    /// Verifies that a header ending during the tensor count returns a controlled failure.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_TruncatedHeader_ReturnsTruncatedHeaderFailure()
    {
        // Arrange: create the real scanner and locate the deployed partial-header fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-003-truncated-header.gguf");

        // Arrange: prove the packaged test deployment supplied the intended input.
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The truncated-header GGUF fixture was not found at: {fixturePath}");

        // Act: scan the fixture, which ends four bytes into the tensor-count field.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: a structural header failure identifies the exact incomplete field.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("truncated-header", result.FailureCode);
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "tensor count");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 8");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "reading 4 bytes");
    }

    /// <summary>
    /// Verifies that a complete header with no metadata reports a missing architecture.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ValidHeaderWithoutArchitecture_ReturnsMissingArchitectureFailure()
    {
        // Arrange: create the real scanner and locate the deployed valid header fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            "H-001-valid-v3-header.gguf");

        // Arrange: prove the packaged test deployment supplied the intended input.
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The valid-header GGUF fixture was not found at: {fixturePath}");

        // Act: scan the complete zero-metadata header.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: a usable failure result explains the missing required metadata.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("missing-required-architecture", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
    }

    /// <summary>
    /// Verifies that a file without the required GGUF signature
    /// is rejected with the stable invalid-magic diagnostic code.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_InvalidMagic_ReturnsInvalidMagicFailure()
    {
        // Arrange: create the real GGUF scanner being tested.
        GgufQuickScanner scanner = new();

        // Arrange: build the full path to the invalid-magic fixture
        // that should be copied into the compiled test output.
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-001-invalid-magic.gguf");

        // Arrange: verify that the test input was deployed correctly.
        //
        // Without this assertion, a missing fixture could produce a
        // FileNotFoundException and make it look like the scanner is broken.
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The invalid-magic GGUF fixture was not found at: {fixturePath}");

        // Act: scan the file that deliberately begins with TEST
        // rather than the required GGUF signature.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the scanner must return a result rather than crashing.
        Assert.IsNotNull(result);

        // Assert: invalid magic must produce a controlled failure.
        Assert.AreEqual(
            ModelQuickScanOutcome.Failure,
            result.Outcome);

        // Assert: the stable diagnostic code must identify the problem.
        Assert.AreEqual(
            "invalid-magic",
            result.FailureCode);

        // Assert: the UI must receive a useful explanation.
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.UserMessage));

        // Assert: diagnostic logging must receive technical information.
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.TechnicalMessage));

        // Assert: diagnostics preserve both the expected and actual signature bytes.
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            "47-47-55-46");
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            "54-45-53-54");
    }

    /// <summary>
    /// Verifies that a GGUF file using an unsupported container version
    /// is rejected with the stable unsupported-version diagnostic code.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_UnsupportedVersion_ReturnsUnsupportedVersionFailure()
    {
        // Arrange: create the real GGUF scanner being tested.
        GgufQuickScanner scanner = new();

        // Arrange: build the path to the fixture copied into
        // the compiled test project's output directory.
        //
        // This fixture begins with valid GGUF magic but contains version 99.
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-002-unsupported-version.gguf");

        // Arrange: verify that the fixture was copied correctly.
        //
        // This gives a clear test-setup failure rather than making a missing
        // fixture look like a defect in the production scanner.
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The unsupported-version GGUF fixture was not found at: {fixturePath}");

        // Act: scan the file that deliberately declares GGUF version 99.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the scanner must return a controlled result rather than crash.
        Assert.IsNotNull(result);

        // Assert: an unsupported version is a failed scan.
        Assert.AreEqual(
            ModelQuickScanOutcome.Failure,
            result.Outcome);

        // Assert: the stable diagnostic code identifies the exact problem.
        Assert.AreEqual(
            "unsupported-version",
            result.FailureCode);

        // Assert: the UI receives a usable explanation.
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.UserMessage));

        // Assert: diagnostics contain useful technical information.
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.TechnicalMessage));

        // Assert: the technical message records the actual version found.
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            "99");
    }
}
