// Import the production GGUF scanner and result types.
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using System.Buffers.Binary;
using System.Globalization;
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
        // that should be copied into the compiled test output
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-001-invalid-magic.gguf");

        // Arrange: verify that the test input was deployed correctly.
        //
        // without this assertion, a missing fixture could produce a
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
        // the compiled test project's output directory
        //
        // This fixture begins with valid GGUF magic but contains version 99.
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-002-unsupported-version.gguf");

        // Arrange: verify that the fixture was copied correctly.
        //
        // this gives a clear test-setup failure rather than making a missing
        // fixture look like a defect in the production scanner
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

        // Assert: the technical message records the fixed-field offset and actual version.
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            "file offset 4");
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
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            FormatNumber(1_000_001));
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            FormatNumber(1_000_000));
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 16");
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
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            FormatNumber(65_536));
        StringAssert.Contains(
            result.TechnicalMessage ?? string.Empty,
            FormatNumber(65_535));
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
    /// Verifies that individually valid keys cannot compose into excessive decode work.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ExcessiveTotalKeyBytes_ReturnsControlledFailure()
    {
        await AssertFailureFixtureAsync(
            "I-029-excessive-total-key-bytes.gguf",
            "excessive-metadata-key-bytes",
            "file offset 38",
            $"to {FormatNumber(65_536)} bytes",
            $"limit is {FormatNumber(65_535)} bytes");
    }

    /// <summary>
    /// Verifies that Boolean arrays report an invalid value across a validation boundary.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_InvalidBooleanArrayValue_ReportsElementAndOffset()
    {
        await AssertFailureFixtureAsync(
            "I-030-invalid-boolean-array-value.gguf",
            "invalid-boolean-value",
            "array depth 1",
            $"element {FormatNumber(4_096)}",
            "file offset 4165",
            "boolean byte 2",
            "0 or 1");
    }

    /// <summary>
    /// Verifies that string arrays reuse bounded parsing across many empty elements.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_OversizedStringArrayValue_ReportsElementAndOffset()
    {
        await AssertFailureFixtureAsync(
            "I-031-oversized-string-array-value.gguf",
            "metadata-string-too-long",
            "array depth 1",
            $"element {FormatNumber(4_096)}",
            "file offset 32836",
            $"{FormatNumber(16_777_217)} bytes",
            $"limit is {FormatNumber(16_777_216)} bytes");
    }

    /// <summary>
    /// Verifies that a malformed child after many empty arrays retains its parent index.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_TruncatedNestedArray_ReportsParentElementAndOffset()
    {
        await AssertFailureFixtureAsync(
            "I-032-truncated-nested-array.gguf",
            "truncated-metadata",
            "array depth 2",
            $"parent element {FormatNumber(4_096)}",
            "file offset 49225",
            "has 4 available/read bytes",
            "expected 8 bytes");
    }

    /// <summary>
    /// Verifies the retained-string allocation guard before a large buffer is created.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_OversizedRetainedString_ReturnsControlledFailure()
    {
        await AssertFailureFixtureAsync(
            "I-033-oversized-retained-string.gguf",
            "metadata-string-too-long",
            "general.architecture",
            "file offset 56",
            $"{FormatNumber(16_777_217)} bytes",
            $"limit is {FormatNumber(16_777_216)} bytes");
    }

    /// <summary>
    /// Verifies the aggregate array budget across separate metadata entries.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_CrossEntryArrayTotal_ReturnsControlledFailure()
    {
        await AssertFailureFixtureAsync(
            "I-034-cross-entry-array-total.gguf",
            "excessive-array-count",
            $"scan total from {FormatNumber(4_000_000)}",
            "by 1 elements",
            "file offset 4000211",
            $"limit is {FormatNumber(4_000_000)}");
    }

    /// <summary>
    /// Verifies exact-read and strict-decoding failures at metadata field boundaries.
    /// </summary>
    [TestMethod]
    [DataRow(
        "I-035-truncated-key-length.gguf",
        "truncated-metadata",
        "stage 'key length'",
        "file offset 24",
        "has 4 available/read bytes",
        "expected 8 bytes")]
    [DataRow(
        "I-036-truncated-key-bytes.gguf",
        "truncated-metadata",
        "stage 'key bytes'",
        "file offset 32",
        "has 2 available/read bytes",
        "expected 5 bytes")]
    [DataRow(
        "I-037-truncated-value-type.gguf",
        "truncated-metadata",
        "stage 'value type'",
        "file offset 33",
        "has 2 available/read bytes",
        "expected 4 bytes")]
    [DataRow(
        "I-038-invalid-utf8-key.gguf",
        "invalid-metadata-encoding",
        "entry 0",
        "key at file offset 32",
        "not valid UTF-8",
        "key")]
    [TestCategory("Unit")]
    public async Task ScanAsync_MalformedMetadataField_ReturnsControlledFailure(
        string fixtureFileName,
        string expectedFailureCode,
        string expectedDetail1,
        string expectedDetail2,
        string expectedDetail3,
        string expectedDetail4)
    {
        await AssertFailureFixtureAsync(
            fixtureFileName,
            expectedFailureCode,
            expectedDetail1,
            expectedDetail2,
            expectedDetail3,
            expectedDetail4);
    }

    /// <summary>
    /// Verifies cancellation requested after scanning starts escapes unchanged.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_InFlightCancellation_ThrowsOperationCanceledException()
    {
        TaskCompletionSource<bool> scanStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> continueScan = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        GgufQuickScanner scanner = new(
            async () =>
            {
                scanStarted.TrySetResult(true);
                await continueScan.Task;
            });
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            "I-032-truncated-nested-array.gguf");
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The nested-array cancellation fixture was not found at: {fixturePath}");
        using CancellationTokenSource cancellationTokenSource = new();

        Task<ModelQuickScanResult> scanTask = scanner.ScanAsync(
            fixturePath,
            cancellationTokenSource.Token);
        await scanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationTokenSource.Cancel();
        continueScan.TrySetResult(true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await scanTask);
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
        StringAssert.Contains(result.TechnicalMessage ?? string.Empty, "file offset 52");
    }

    /// <summary>
    /// Verifies that one declared string cannot exceed the 16 MiB application limit.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_OversizedMetadataString_ReturnsMetadataStringTooLongFailure()
    {
        await AssertFailureFixtureAsync(
            "I-012-oversized-metadata-string.gguf",
            "metadata-string-too-long",
            "file offset 60");
    }

    /// <summary>
    /// Verifies that one array cannot declare more than one million elements.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ExcessiveArrayCount_ReturnsExcessiveArrayCountFailure()
    {
        await AssertFailureFixtureAsync(
            "I-013-excessive-array-count.gguf",
            "excessive-array-count",
            "file offset 63");
    }

    /// <summary>
    /// Verifies that nested arrays share the four-million-element scan budget.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ExcessiveTotalArrayCount_ReturnsExcessiveArrayCountFailure()
    {
        await AssertFailureFixtureAsync(
            "I-014-excessive-total-array-count.gguf",
            "excessive-array-count",
            "file offset 111");
    }

    /// <summary>
    /// Verifies that a ninth nested array level is rejected before descent.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ExcessiveArrayDepth_ReturnsExcessiveArrayDepthFailure()
    {
        await AssertFailureFixtureAsync(
            "I-015-excessive-array-depth.gguf",
            "excessive-array-depth",
            "file offset 148");
    }

    /// <summary>
    /// Verifies that an exact architecture context key requires an unsigned integer.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ContextWithWrongType_ReturnsInvalidContextTypeFailure()
    {
        await AssertFailureFixtureAsync(
            "I-016-invalid-context-type.gguf",
            "invalid-context-type",
            "file offset 54");
    }

    /// <summary>
    /// Verifies that only 64 distinct pre-architecture context keys may be retained.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ExcessiveContextCandidates_ReturnsControlledFailure()
    {
        await AssertFailureFixtureAsync(
            "I-017-excessive-context-candidates.gguf",
            "excessive-context-candidate-count",
            "file offset 2746");
    }

    /// <summary>
    /// Verifies that malformed UTF-8 is rejected rather than replacement-decoded.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_InvalidUtf8Architecture_ReturnsInvalidEncodingFailure()
    {
        await AssertFailureFixtureAsync(
            "I-018-invalid-utf8-architecture.gguf",
            "invalid-metadata-encoding");
    }

    /// <summary>
    /// Verifies that metadata keys accept only the documented ASCII grammar.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_NonAsciiKey_ReturnsInvalidMetadataKeyFailure()
    {
        await AssertFailureFixtureAsync(
            "I-019-non-ascii-key.gguf",
            "invalid-metadata-key");
    }

    /// <summary>
    /// Verifies that general.name must be encoded as a GGUF string.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_NameWithWrongType_ReturnsInvalidNameTypeFailure()
    {
        await AssertFailureFixtureAsync(
            "I-020-wrong-name-type.gguf",
            "invalid-name-type",
            "file offset 91");
    }

    /// <summary>
    /// Verifies that general.size_label must be encoded as a GGUF string.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_SizeLabelWithWrongType_ReturnsInvalidSizeLabelTypeFailure()
    {
        await AssertFailureFixtureAsync(
            "I-021-wrong-size-label-type.gguf",
            "invalid-size-label-type",
            "file offset 97");
    }

    /// <summary>
    /// Verifies that general.file_type must be encoded as an unsigned 32-bit integer.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_FileTypeWithWrongType_ReturnsInvalidFileTypeFailure()
    {
        await AssertFailureFixtureAsync(
            "I-022-wrong-file-type.gguf",
            "invalid-file-type",
            "file offset 96");
    }

    /// <summary>
    /// Verifies that whitespace-only architecture metadata remains unusable.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_BlankArchitecture_ReturnsMissingArchitectureFailure()
    {
        await AssertFailureFixtureAsync(
            "I-023-blank-architecture.gguf",
            "missing-required-architecture",
            "file offset 56");
    }

    /// <summary>
    /// Verifies that every official scalar type and nested arrays are consumed safely.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_AllOfficialMetadataTypes_ReturnsExpectedSuccessResult()
    {
        GgufQuickScanner scanner = new();
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-009-all-official-metadata-types.gguf",
            "V-009-all-official-metadata-types.json",
            "V-009");
    }

    /// <summary>
    /// Verifies that an unassigned numeric file type receives a stable display label.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_UnknownFileType_ReturnsDocumentedLabel()
    {
        GgufQuickScanner scanner = new();
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-010-unknown-file-type.gguf",
            "V-010-unknown-file-type.json",
            "V-010");
    }

    /// <summary>
    /// Verifies that a missing operational input is translated into a stable result.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_MissingFile_ReturnsFileNotFoundFailure()
    {
        GgufQuickScanner scanner = new();
        string missingPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            $"missing-{Guid.NewGuid():N}.gguf");
        Assert.IsFalse(File.Exists(missingPath));

        ModelQuickScanResult result = await scanner.ScanAsync(
            missingPath,
            CancellationToken.None);

        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual("file-not-found", result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
    }

    /// <summary>
    /// Verifies that an omitted general.name uses the selected GGUF file name.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_MissingName_UsesFileNameFallback()
    {
        // Arrange: create the real scanner for the generated missing-name fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: the fixture expectation includes the file-name fallback.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-002-missing-name.gguf",
            "V-002-missing-name.json",
            "V-002");
    }

    /// <summary>
    /// Verifies that an omitted architecture-specific context value remains unavailable.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_MissingContext_ReturnsSuccessWithNullContext()
    {
        // Arrange: create the real scanner for the generated missing-context fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: missing optional context produces a successful null value.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-003-missing-context.gguf",
            "V-003-missing-context.json",
            "V-003");
    }

    /// <summary>
    /// Verifies that an omitted general.size_label remains unavailable.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_MissingSizeLabel_ReturnsSuccessWithNullSizeLabel()
    {
        // Arrange: create the real scanner for the generated missing-size-label fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: missing optional size metadata produces a successful null value.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-004-missing-size-label.gguf",
            "V-004-missing-size-label.json",
            "V-004");
    }

    /// <summary>
    /// Verifies that unknown scalar, boolean, and array metadata is consumed safely.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_UnknownMetadata_SkipsValuesAndReturnsExpectedResult()
    {
        // Arrange: create the real scanner for the generated forward-compatible fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: unrecognised values do not disrupt known metadata extraction.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-005-unknown-metadata.gguf",
            "V-005-unknown-metadata.json",
            "V-005");
    }

    /// <summary>
    /// Verifies that a context field preceding general.architecture is resolved later.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_UnusualMetadataOrder_ReturnsExpectedResult()
    {
        // Arrange: create the real scanner for the generated context-before-architecture fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: metadata order does not affect the completed result.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-006-unusual-metadata-order.gguf",
            "V-006-unusual-metadata-order.json",
            "V-006");
    }

    /// <summary>
    /// Verifies that a UInt32 context length is normalised to the result's UInt64 value.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ContextEncodedAsUInt32_NormalisesToUInt64()
    {
        // Arrange: create the real scanner for the generated UInt32-context fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: the result exposes the normalised unsigned 64-bit value.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-007-context-uint32.gguf",
            "V-007-context-uint32.json",
            "V-007");
    }

    /// <summary>
    /// Verifies that an omitted general.file_type leaves quantization unavailable.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_MissingFileType_ReturnsSuccessWithNullQuantization()
    {
        // Arrange: create the real scanner for the generated missing-file-type fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: missing optional file type produces a successful null value.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-008-missing-file-type.gguf",
            "V-008-missing-file-type.json",
            "V-008");
    }

    /// <summary>
    /// Verifies the current official llama.cpp file type 41 displays as Q2_0.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_CurrentQ2_0FileType_ReturnsExpectedResult()
    {
        // Arrange: create the real scanner for the generated file-type 41 fixture.
        GgufQuickScanner scanner = new();

        // Act and assert: the public scan result exposes the current official label.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-011-current-q2_0-file-type.gguf",
            "V-011-current-q2_0-file-type.json",
            "V-011");
    }

    /// <summary>
    /// Verifies scanner-relevant duplicate keys deterministically retain the first occurrence.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_DuplicateRelevantMetadata_RetainsFirstOccurrences()
    {
        // Arrange: create the real scanner for generated duplicate metadata.
        GgufQuickScanner scanner = new();

        // Act and assert: architecture and its context remain coherent and first wins.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-012-duplicate-relevant-metadata.gguf",
            "V-012-duplicate-relevant-metadata.json",
            "V-012");
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
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-001-complete-metadata-v3.gguf",
            "V-001-complete-metadata-v3.json",
            "V-001");
    }

    /// <summary>
    /// Verifies that a valid little-endian GGUF version 2 file is accepted
    /// and produces the same metadata as the equivalent version 3 fixture.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ValidVersion2_ReturnsSuccess()
    {
        // Arrange: create a valid version 2 variant of the complete metadata fixture.
        GgufQuickScanner scanner = new();
        string fixturePath =
            await CreateCompleteMetadataVersionVariantAsync(version: 2);

        try
        {
            // Act: scan the valid GGUF version 2 fixture.
            ModelQuickScanResult result = await scanner.ScanAsync(
                fixturePath,
                CancellationToken.None);

            // Assert: version 2 is accepted and all expected metadata is retained.
            Assert.AreEqual(
                ModelQuickScanOutcome.Success,
                result.Outcome);

            Assert.AreEqual(
                2u,
                result.GgufVersion);

            Assert.AreEqual(
                "IBM Granite Fixture Model",
                result.ModelName);

            Assert.AreEqual(
                "granite",
                result.Architecture);

            Assert.AreEqual(
                "3B",
                result.ParameterSizeLabel);

            Assert.AreEqual(
                "Q4_K_M",
                result.Quantization);

            Assert.AreEqual(
                320L,
                result.FileSizeBytes);

            Assert.AreEqual(
                131_072UL,
                result.ContextLength);

            Assert.IsNull(result.FailureCode);
            Assert.IsNull(result.UserMessage);
            Assert.IsNull(result.TechnicalMessage);
        }
        finally
        {
            // Cleanup: remove the temporary version-variant fixture.
            File.Delete(fixturePath);
        }
    }

    /// <summary>
    /// Verifies that the current valid GGUF version 3 fixture remains supported.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_ValidVersion3_ReturnsSuccess()
    {
        // Arrange: create the real scanner.
        GgufQuickScanner scanner = new();

        // Act and assert: reuse the existing typed expectation to verify
        // all observable metadata, including GGUF version 3.
        await AssertExpectedFixtureResultAsync(
            scanner,
            "V-001-complete-metadata-v3.gguf",
            "V-001-complete-metadata-v3.json",
            "V-001");
    }

    /// <summary>
    /// Verifies that obsolete GGUF version 1 files receive a specific
    /// failure that distinguishes them from unknown future versions.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_Version1_ReturnsObsoleteVersionFailure()
    {
        // Arrange: create a structurally complete file declaring GGUF version 1.
        GgufQuickScanner scanner = new();
        string fixturePath =
            await CreateCompleteMetadataVersionVariantAsync(version: 1);

        try
        {
            // Act: scan the obsolete-version fixture.
            ModelQuickScanResult result = await scanner.ScanAsync(
                fixturePath,
                CancellationToken.None);

            // Assert: version 1 is rejected using the dedicated obsolete-version contract.
            Assert.AreEqual(
                ModelQuickScanOutcome.Failure,
                result.Outcome);

            Assert.AreEqual(
                "obsolete-version",
                result.FailureCode);

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(result.UserMessage));

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(result.TechnicalMessage));

            StringAssert.Contains(
                result.UserMessage ?? string.Empty,
                "obsolete");

            StringAssert.Contains(
                result.TechnicalMessage ?? string.Empty,
                "file offset 4");

            StringAssert.Contains(
                result.TechnicalMessage ?? string.Empty,
                "is 1");

            // failure results must not expose partially parsed success metadata
            Assert.IsNull(result.ModelName);
            Assert.IsNull(result.Architecture);
            Assert.IsNull(result.ParameterSizeLabel);
            Assert.IsNull(result.Quantization);
            Assert.IsNull(result.FileSizeBytes);
            Assert.IsNull(result.ContextLength);
            Assert.IsNull(result.GgufVersion);
        }
        finally
        {
            // Cleanup: remove the temporary version-variant fixture.
            File.Delete(fixturePath);
        }
    }

    /// <summary>
    /// Verifies that the first unknown future GGUF version is rejected
    /// rather than being parsed using assumptions from version 3.
    /// </summary>
    [TestMethod]
    [TestCategory("Unit")]
    public async Task ScanAsync_FutureVersion_ReturnsUnsupportedVersionFailure()
    {
        // Arrange: version 4 represents the first version newer than
        // the application's maximum currently supported version
        GgufQuickScanner scanner = new();
        string fixturePath =
            await CreateCompleteMetadataVersionVariantAsync(version: 4);

        try
        {
            // Act: scan the future-version fixture.
            ModelQuickScanResult result = await scanner.ScanAsync(
                fixturePath,
                CancellationToken.None);

            // Assert: unknown future versions fail safely and predictably.
            Assert.AreEqual(
                ModelQuickScanOutcome.Failure,
                result.Outcome);

            Assert.AreEqual(
                "unsupported-version",
                result.FailureCode);

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(result.UserMessage));

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(result.TechnicalMessage));

            StringAssert.Contains(
                result.UserMessage ?? string.Empty,
                "not supported");

            StringAssert.Contains(
                result.TechnicalMessage ?? string.Empty,
                "file offset 4");

            StringAssert.Contains(
                result.TechnicalMessage ?? string.Empty,
                "is 4");

            // failure results must not expose partially parsed success metadata
            // failure results must not expose partially parsed success metadata
            Assert.IsNull(result.ModelName);
            Assert.IsNull(result.Architecture);
            Assert.IsNull(result.ParameterSizeLabel);
            Assert.IsNull(result.Quantization);
            Assert.IsNull(result.FileSizeBytes);
            Assert.IsNull(result.ContextLength);
            Assert.IsNull(result.GgufVersion);
        }
        finally
        {
            // Cleanup: remove the temporary version-variant fixture.
            File.Delete(fixturePath);
        }
    }

    /// <summary>
    /// Creates a temporary copy of the complete valid GGUF fixture and changes
    /// only the uint32 version field stored at byte offset 4.
    /// </summary>
    private static async Task<string>
        CreateCompleteMetadataVersionVariantAsync(uint version)
    {
        const int GgufVersionOffset = 4;
        const int GgufVersionByteLength = sizeof(uint);

        string sourceFixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            "V-001-complete-metadata-v3.gguf");

        Assert.IsTrue(
            File.Exists(sourceFixturePath),
            $"The complete GGUF fixture was not found at: {sourceFixturePath}");

        byte[] fixtureBytes =
            await File.ReadAllBytesAsync(sourceFixturePath);

        Assert.IsTrue(
            fixtureBytes.Length >=
                GgufVersionOffset + GgufVersionByteLength,
            "The complete GGUF fixture is too short to contain its version field.");

        // GGUF stores the version as a little-endian uint32 beginning at offset 4.
        BinaryPrimitives.WriteUInt32LittleEndian(
            fixtureBytes.AsSpan(
                GgufVersionOffset,
                GgufVersionByteLength),
            version);

        string temporaryFixturePath = Path.Combine(
            Path.GetTempPath(),
            $"granite-edge-gguf-version-{version}-" +
                $"{Guid.NewGuid():N}.gguf");

        await File.WriteAllBytesAsync(
            temporaryFixturePath,
            fixtureBytes);

        return temporaryFixturePath;
    }

    /// <summary>
    /// Scans one deployed malformed fixture and checks its stable controlled failure.
    /// </summary>
    private static async Task AssertFailureFixtureAsync(
        string fixtureFileName,
        string expectedFailureCode,
        params string[] expectedTechnicalDetails)
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "Malformed",
            fixtureFileName);
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The malformed GGUF fixture was not found at: {fixturePath}");

        GgufQuickScanner scanner = new();
        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        Assert.AreEqual(ModelQuickScanOutcome.Failure, result.Outcome);
        Assert.AreEqual(expectedFailureCode, result.FailureCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.UserMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.TechnicalMessage));
        foreach (string expectedTechnicalDetail in expectedTechnicalDetails)
        {
            StringAssert.Contains(
                result.TechnicalMessage ?? string.Empty,
                expectedTechnicalDetail);
        }
    }

    /// <summary>
    /// Scans one deployed valid fixture and compares every observable result field with typed JSON.
    /// </summary>
    private static async Task AssertExpectedFixtureResultAsync(
        GgufQuickScanner scanner,
        string fixtureFileName,
        string expectedFileName,
        string expectedFixtureId)
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "GGUF",
            fixtureFileName);
        string expectedPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "ExpectedMetadata",
            expectedFileName);
        Assert.IsTrue(
            File.Exists(fixturePath),
            $"The GGUF fixture was not found at: {fixturePath}");
        Assert.IsTrue(
            File.Exists(expectedPath),
            $"The metadata expectation was not found at: {expectedPath}");

        ExpectedFixture? deserializedExpected =
            JsonSerializer.Deserialize<ExpectedFixture>(
                await File.ReadAllTextAsync(expectedPath));
        Assert.IsNotNull(
            deserializedExpected,
            $"The metadata expectation deserialized to null: {expectedPath}");
        ExpectedFixture expected = deserializedExpected!;
        Assert.AreEqual(expectedFixtureId, expected.FixtureId);
        Assert.AreEqual(fixtureFileName, expected.FixtureFile);

        ModelQuickScanResult result = await scanner.ScanAsync(
            fixturePath,
            CancellationToken.None);

        Assert.AreEqual(expected.ExpectedOutcome, result.Outcome.ToString());
        Assert.AreEqual(ModelQuickScanOutcome.Success, result.Outcome);
        Assert.AreEqual(expected.ExpectedMetadata.ModelName, result.ModelName);
        Assert.AreEqual(expected.ExpectedMetadata.Architecture, result.Architecture);
        Assert.AreEqual(expected.ExpectedMetadata.ParameterSizeLabel, result.ParameterSizeLabel);
        Assert.AreEqual(expected.ExpectedMetadata.Quantization, result.Quantization);
        Assert.AreEqual(expected.ExpectedMetadata.FileSizeBytes, result.FileSizeBytes);
        Assert.AreEqual(
            new DateTimeOffset(File.GetLastWriteTimeUtc(fixturePath)),
            result.FileLastWriteTimeUtc);
        Assert.AreEqual(expected.ExpectedMetadata.ContextLength, result.ContextLength);
        Assert.AreEqual(expected.ExpectedMetadata.GgufVersion, result.GgufVersion);
        Assert.IsNull(result.FailureCode);
        Assert.IsNull(result.UserMessage);
        Assert.IsNull(result.TechnicalMessage);
    }

    /// <summary>
    /// Formats diagnostic numbers using the culture exercised by the scanner.
    /// </summary>
    private static string FormatNumber(ulong value)
    {
        return value.ToString("N0", CultureInfo.CurrentCulture);
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
