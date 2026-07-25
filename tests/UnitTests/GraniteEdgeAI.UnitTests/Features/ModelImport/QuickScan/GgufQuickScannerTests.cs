// Import the production GGUF scanner and result types.
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Verifies that a declared metadata count above the scanner safety limit is rejected.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ExcessiveMetadataCount_ReturnsExcessiveMetadataCountFailure()
    {
        // Arrange: create the real scanner and locate the generated limit-plus-one fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-007-excessive-metadata-count.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The excessive-metadata-count fixture was not found at: {fixturePath}");

        // Act: scan a header that declares 1,000,001 metadata entries.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the attacker-controlled loop is rejected before metadata is read.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("excessive-metadata-count", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "1,000,001");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "1,000,000");
    }

    /// <summary>
    /// Verifies that a metadata key beyond the GGUF byte-length limit is rejected.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_OversizedKeyLength_ReturnsMetadataKeyTooLongFailure()
    {
        // Arrange: create the real scanner and locate the generated key-limit fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-005-oversized-key-length.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The oversized-key-length fixture was not found at: {fixturePath}");

        // Act: scan an entry whose key declares 65,536 bytes.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the key is rejected before narrowing or allocation.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("metadata-key-too-long", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "65,536");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "65,535");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 24");
    }

    /// <summary>
    /// Verifies that a metadata value type outside the official range is rejected.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_UnknownMetadataType_ReturnsUnsupportedMetadataTypeFailure()
    {
        // Arrange: create the real scanner and locate the generated unknown-type fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-006-unknown-value-type.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The unknown-metadata-type fixture was not found at: {fixturePath}");

        // Act: scan an entry whose raw metadata value type is 99.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: unsupported dispatch is reported with its key, raw type, and range.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("unsupported-metadata-type", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "fixture.unknown");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "99");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "0 through 12");
    }

    /// <summary>
    /// Verifies that a metadata string ending before its declared length fails safely.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_TruncatedMetadataValue_ReturnsTruncatedMetadataFailure()
    {
        // Arrange: create the real scanner and locate the generated truncated-value fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-004-truncated-metadata-value.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The truncated-metadata-value fixture was not found at: {fixturePath}");

        // Act: scan a general.name string that declares 20 bytes but provides only three.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the structural failure identifies the key, stage, offset, and byte counts.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("truncated-metadata", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "general.name");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "string payload");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 56");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "3");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "20");
    }

    /// <summary>
    /// Verifies that a GGUF boolean byte other than zero or one is rejected.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_InvalidBooleanValue_ReturnsInvalidBooleanFailure()
    {
        // Arrange: create the real scanner and locate the generated invalid-boolean fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-010-invalid-boolean-value.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The invalid-boolean-value fixture was not found at: {fixturePath}");

        // Act: scan an entry whose boolean payload is the invalid byte value two.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the exact key, value, and one-byte boolean domain are reported.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("invalid-boolean-value", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "fixture.enabled");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "2");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "0 or 1");
    }

    /// <summary>
    /// Verifies that an array ending before its declared element count fails safely.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_TruncatedArray_ReturnsTruncatedMetadataFailure()
    {
        // Arrange: create the real scanner and locate the generated truncated-array fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-011-truncated-array.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The truncated-array fixture was not found at: {fixturePath}");

        // Act: scan a uint32 array that declares three elements but supplies one.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the array byte range is validated before any seek beyond EOF.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("truncated-metadata", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "fixture.values");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "array payload");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 62");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "4");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "12");
    }

    /// <summary>
    /// Verifies that an empty metadata key violates the hierarchical key grammar.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_EmptyMetadataKey_ReturnsInvalidMetadataKeyFailure()
    {
        // Arrange: create the real scanner and locate the generated empty-key fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-025-empty-metadata-key.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The empty-metadata-key fixture was not found at: {fixturePath}");

        // Act: scan a complete metadata entry whose key contains zero characters.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the diagnostic identifies the entry, safe offset, and empty-key rule.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("invalid-metadata-key", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "entry 0");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 32");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "empty");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "nonempty");
    }

    /// <summary>
    /// Verifies that adjacent separators create a forbidden empty key segment.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_EmptyMetadataKeySegment_ReturnsInvalidMetadataKeyFailure()
    {
        // Arrange: create the real scanner and locate the generated empty-segment fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-026-empty-key-segment.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The empty-key-segment fixture was not found at: {fixturePath}");

        // Act: scan a complete entry whose key is general..name.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the safe diagnostic reports the second separator, not the raw key.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("invalid-metadata-key", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "entry 0");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 32");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "empty segment");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "character index 8");
    }

    /// <summary>
    /// Verifies that an ASCII space is outside lower_snake_case key segments.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_MetadataKeyWithSpace_ReturnsInvalidMetadataKeyFailure()
    {
        // Arrange: create the real scanner and locate the generated space-key fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-027-key-with-space.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The key-with-space fixture was not found at: {fixturePath}");

        // Act: scan a complete entry whose key includes U+0020 at index seven.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the unsafe key text is replaced by precise index/code diagnostics.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("invalid-metadata-key", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "entry 0");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 32");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "character index 7");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "U+0020");
    }

    /// <summary>
    /// Verifies that uppercase ASCII letters are outside lower_snake_case segments.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_UppercaseMetadataKey_ReturnsInvalidMetadataKeyFailure()
    {
        // Arrange: create the real scanner and locate the generated uppercase-key fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-028-uppercase-key.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The uppercase-key fixture was not found at: {fixturePath}");

        // Act: scan a complete entry whose first key character is uppercase G.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the grammar failure reports safe position/code details.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("invalid-metadata-key", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "entry 0");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 32");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "character index 0");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "U+0047");
    }

    /// <summary>
    /// Verifies that metadata lacking the required architecture returns a stable failure.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_MissingArchitecture_ReturnsMissingArchitectureFailure()
    {
        // Arrange: create the real scanner and locate the deployed malformed fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-008-missing-required-architecture.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The missing-architecture fixture was not found at: {fixturePath}");

        // Act: scan metadata that intentionally omits general.architecture.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the scanner returns a usable, stable missing-architecture failure.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("missing-required-architecture", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
    }

    /// <summary>
    /// Verifies that general.architecture must use the GGUF string metadata type.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ArchitectureWithWrongType_ReturnsInvalidArchitectureTypeFailure()
    {
        // Arrange: create the real scanner and locate the deployed wrong-type fixture.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-009-wrong-architecture-type.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The wrong-architecture-type fixture was not found at: {fixturePath}");

        // Act: scan an architecture entry encoded as uint32 rather than string.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: the type boundary is reported before the payload is accepted.
        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("invalid-architecture-type", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "general.architecture");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "UInt32");
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "String");
    }

    /// <summary>
    /// Verifies that complete GGUF metadata produces every expected success field.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_CompleteMetadata_ReturnsExpectedSuccessResult()
    {
        // Arrange: create the real scanner and load the deployed typed expectation.
        GgufQuickScanner scanner = new();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            "V-001-complete-metadata-v3.gguf");
        string expectedPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "ExpectedMetadata",
            "V-001-complete-metadata-v3.json");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The complete-metadata fixture was not found at: {fixturePath}");
        Assert.IsTrue(
            File.Exists(expectedPath),
            $"The complete-metadata expectation was not found at: {expectedPath}");
        ExpectedFixture expected = JsonSerializer.Deserialize<ExpectedFixture>(
            await File.ReadAllTextAsync(expectedPath))
            ?? throw new AssertInconclusiveException(
                $"The complete-metadata expectation could not be deserialized: {expectedPath}");

        // Act: scan the complete generated GGUF fixture.
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        // Assert: every success field matches the checked-in typed expectation.
        Assert.AreEqual(ModelQuickScanOutcome.Success, result.Outcome);
        Assert.AreEqual(expected.ExpectedMetadata.ModelName, result.ModelName);
        Assert.AreEqual(expected.ExpectedMetadata.Architecture, result.Architecture);
        Assert.AreEqual(expected.ExpectedMetadata.ParameterSizeLabel, result.ParameterSizeLabel);
        Assert.AreEqual(expected.ExpectedMetadata.Quantization, result.Quantization);
        Assert.AreEqual(expected.ExpectedMetadata.FileSizeBytes, result.FileSizeBytes);
        Assert.AreEqual(expected.ExpectedMetadata.ContextLength, result.ContextLength);
        Assert.AreEqual(expected.ExpectedMetadata.GgufVersion, result.GgufVersion);
        Assert.IsNull(result.FailureCode);
        Assert.IsNull(result.UserMessage);
        Assert.IsNull(result.TechnicalMessage);
    }

    private sealed record ExpectedFixture
    {
        [JsonPropertyName("fixtureId")]
        public required string FixtureId { get; init; }

        [JsonPropertyName("fixtureFile")]
        public required string FixtureFile { get; init; }

        [JsonPropertyName("expectedOutcome")]
        public required string ExpectedOutcome { get; init; }

        [JsonPropertyName("expectedMetadata")]
        public required ExpectedMetadata ExpectedMetadata { get; init; }
    }

    private sealed record ExpectedMetadata
    {
        [JsonPropertyName("modelName")]
        public required string ModelName { get; init; }

        [JsonPropertyName("architecture")]
        public required string Architecture { get; init; }

        [JsonPropertyName("parameterSizeLabel")]
        public string? ParameterSizeLabel { get; init; }

        [JsonPropertyName("quantization")]
        public string? Quantization { get; init; }

        [JsonPropertyName("fileSizeBytes")]
        public long FileSizeBytes { get; init; }

        [JsonPropertyName("contextLength")]
        public ulong? ContextLength { get; init; }

        [JsonPropertyName("ggufVersion")]
        public uint GgufVersion { get; init; }
    }
}
