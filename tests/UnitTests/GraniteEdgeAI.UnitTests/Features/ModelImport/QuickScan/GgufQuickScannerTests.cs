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