// Import the production quick-scan result types.
using GraniteEdgeAI.Features.ModelImport.QuickScan;

// Import MSTest attributes and assertions.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

// Marks this class as a collection of MSTest tests.
[TestClass]
public sealed class ModelQuickScanResultTests
{
    // Verifies that the cancellation factory records the correct outcome.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateCancelled_SetsCancelledOutcome()
    {
        // Act: create a cancelled quick-scan result.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateCancelled();

        // Assert: the result must explicitly report cancellation.
        Assert.AreEqual(
            ModelQuickScanOutcome.Cancelled,
            result.Outcome);
    }

    // Verifies that cancellation does not contain unrelated data.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateCancelled_LeavesAllOtherPropertiesEmpty()
    {
        // Act: create a cancelled quick-scan result.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateCancelled();

        // Assert: cancelled scans must not contain model metadata.
        Assert.IsNull(result.ModelName);
        Assert.IsNull(result.Architecture);
        Assert.IsNull(result.ParameterSizeLabel);
        Assert.IsNull(result.Quantization);
        Assert.IsNull(result.FileSizeBytes);
        Assert.IsNull(result.ContextLength);
        Assert.IsNull(result.GgufVersion);

        // Assert: cancellation must not be represented as a failure.
        Assert.IsNull(result.FailureCode);
        Assert.IsNull(result.UserMessage);
        Assert.IsNull(result.TechnicalMessage);
    }

    // Verifies that successful scan metadata is preserved by the result.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_StoresOutcomeAndMetadata()
    {
        // Arrange: provide representative GGUF metadata.
        const string modelName = "Granite 3.3 2B Instruct";
        const string architecture = "granite";
        const string parameterSizeLabel = "2B";
        const string quantization = "Q4_K_M";
        const long fileSizeBytes = 1_500_000_000L;
        const ulong contextLength = 131_072UL;
        const uint ggufVersion = 3U;

        // Act: create a successful result with the supplied metadata.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateSuccess(
                modelName,
                architecture,
                parameterSizeLabel,
                quantization,
                fileSizeBytes,
                contextLength,
                ggufVersion);

        // Assert: the result must explicitly report success.
        Assert.AreEqual(
            ModelQuickScanOutcome.Success,
            result.Outcome);

        // Assert: every supplied metadata value must be preserved.
        Assert.AreEqual(modelName, result.ModelName);
        Assert.AreEqual(architecture, result.Architecture);
        Assert.AreEqual(
            parameterSizeLabel,
            result.ParameterSizeLabel);
        Assert.AreEqual(quantization, result.Quantization);
        Assert.AreEqual(
            (long?)fileSizeBytes,
            result.FileSizeBytes);
        Assert.AreEqual(
            (ulong?)contextLength,
            result.ContextLength);
        Assert.AreEqual(
            (uint?)ggufVersion,
            result.GgufVersion);
    }

    // Verifies that a successful result contains no failure information.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_LeavesFailureDetailsEmpty()
    {
        // Act: create a representative successful result.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: "granite",
                parameterSizeLabel: "2B",
                quantization: "Q4_K_M",
                fileSizeBytes: 1_500_000_000L,
                contextLength: 131_072UL,
                ggufVersion: 3U);

        // Assert: success must not contain failure information.
        Assert.IsNull(result.FailureCode);
        Assert.IsNull(result.UserMessage);
        Assert.IsNull(result.TechnicalMessage);
    }

    // Verifies that failure information is preserved by the result.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateFailure_StoresOutcomeAndFailureDetails()
    {
        // Arrange: provide representative GGUF validation failure data.
        const string failureCode = "invalid-magic";
        const string userMessage =
            "The selected file is not a valid GGUF model.";
        const string technicalMessage =
            "Expected the GGUF magic value at byte offset 0.";

        // Act: create a failed quick-scan result.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateFailure(
                failureCode,
                userMessage,
                technicalMessage);

        // Assert: the result must explicitly report failure.
        Assert.AreEqual(
            ModelQuickScanOutcome.Failure,
            result.Outcome);

        // Assert: all supplied failure information must be preserved.
        Assert.AreEqual(
            failureCode,
            result.FailureCode);
        Assert.AreEqual(
            userMessage,
            result.UserMessage);
        Assert.AreEqual(
            technicalMessage,
            result.TechnicalMessage);
    }

    // Verifies that a failed result contains no misleading model metadata.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateFailure_LeavesMetadataEmpty()
    {
        // Act: create a representative failed result.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateFailure(
                failureCode: "invalid-magic",
                userMessage:
                    "The selected file is not a valid GGUF model.",
                technicalMessage:
                    "Expected the GGUF magic value at byte offset 0.");

        // Assert: failure must not expose successful scan metadata.
        Assert.IsNull(result.ModelName);
        Assert.IsNull(result.Architecture);
        Assert.IsNull(result.ParameterSizeLabel);
        Assert.IsNull(result.Quantization);
        Assert.IsNull(result.FileSizeBytes);
        Assert.IsNull(result.ContextLength);
        Assert.IsNull(result.GgufVersion);
    }

    // Verifies that a blank user-facing failure message is replaced
    // with a safe default message that can be displayed by the UI.
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public void CreateFailure_BlankUserMessage_UsesDefaultMessage(
        string? blankUserMessage)
    {
        // Arrange: provide a valid diagnostic code and technical message,
        // but deliberately provide a blank user message.
        const string failureCode = "invalid-magic";
        const string technicalMessage =
            "Expected the GGUF magic value at byte offset 0.";

        // Define the default message that the result should provide.
        const string expectedUserMessage =
            "The selected model could not be scanned.";

        // Act: create a failed scan result using the blank user message.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateFailure(
                failureCode,
                blankUserMessage,
                technicalMessage);

        // Assert: the result should still clearly report a failure.
        Assert.AreEqual(
            ModelQuickScanOutcome.Failure,
            result.Outcome);

        // Assert: the blank message should be replaced by the default.
        Assert.AreEqual(
            expectedUserMessage,
            result.UserMessage);

        // Assert: the valid technical message should remain unchanged.
        Assert.AreEqual(
            technicalMessage,
            result.TechnicalMessage);
    }

    // Verifies that blank technical details are replaced with a safe default.
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public void CreateFailure_BlankTechnicalMessage_UsesDefaultMessage(
        string? blankTechnicalMessage)
    {
        // Arrange: provide a valid code and user message, but no technical detail.
        const string expectedTechnicalMessage =
            "No additional technical information was provided.";

        // Act: create the failure result with a blank technical message.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateFailure(
                failureCode: "invalid-magic",
                userMessage: "The selected file is not a valid GGUF model.",
                technicalMessage: blankTechnicalMessage);

        // Assert: the result must include the default technical message.
        Assert.AreEqual(
            expectedTechnicalMessage,
            result.TechnicalMessage);
    }

    // Verifies that each blank diagnostic code is rejected.
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public void CreateFailure_BlankFailureCode_ThrowsArgumentException(
        string? blankFailureCode)
    {
        // Act and assert: failure results must have a usable diagnostic code.
        Assert.Throws<ArgumentException>(() =>
            ModelQuickScanResult.CreateFailure(
                failureCode: blankFailureCode,
                userMessage: "The selected model could not be scanned.",
                technicalMessage: "The GGUF magic value was invalid."));
    }

    // Verifies that each blank model name is rejected for a successful result.
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public void CreateSuccess_BlankModelName_ThrowsArgumentException(
        string? blankModelName)
    {
        // Act and assert: a successful result must have a usable model name.
        Assert.Throws<ArgumentException>(() =>
            ModelQuickScanResult.CreateSuccess(
                modelName: blankModelName,
                architecture: "granite",
                parameterSizeLabel: null,
                quantization: null,
                fileSizeBytes: 1L,
                contextLength: null,
                ggufVersion: 1U));
    }

    // Verifies that each blank architecture is rejected for a successful result.
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [TestCategory("Unit")]
    public void CreateSuccess_BlankArchitecture_ThrowsArgumentException(
        string? blankArchitecture)
    {
        // Act and assert: a successful result must have an architecture.
        Assert.Throws<ArgumentException>(() =>
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: blankArchitecture,
                parameterSizeLabel: null,
                quantization: null,
                fileSizeBytes: 1L,
                contextLength: null,
                ggufVersion: 1U));
    }

    // Verifies that zero is not a valid successful model file size.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_ZeroFileSizeBytes_ThrowsArgumentOutOfRangeException()
    {
        // Act and assert: successful results must represent a non-empty file.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: "granite",
                parameterSizeLabel: null,
                quantization: null,
                fileSizeBytes: 0L,
                contextLength: null,
                ggufVersion: 1U));
    }

    // Verifies that a negative file size is not valid for a successful result.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_NegativeFileSizeBytes_ThrowsArgumentOutOfRangeException()
    {
        // Act and assert: successful results cannot represent a negative file size.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: "granite",
                parameterSizeLabel: null,
                quantization: null,
                fileSizeBytes: -1L,
                contextLength: null,
                ggufVersion: 1U));
    }

    // Verifies that zero is not a valid GGUF version for a successful result.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_ZeroGgufVersion_ThrowsArgumentOutOfRangeException()
    {
        // Act and assert: successful results must have a positive GGUF version.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: "granite",
                parameterSizeLabel: null,
                quantization: null,
                fileSizeBytes: 1L,
                contextLength: null,
                ggufVersion: 0U));
    }

    // Verifies that parameter size remains optional for successful results.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_NullParameterSizeLabel_IsStoredAsNull()
    {
        // Act: create success without parameter size metadata.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: "granite",
                parameterSizeLabel: null,
                quantization: "Q4_K_M",
                fileSizeBytes: 1L,
                contextLength: 131_072UL,
                ggufVersion: 3U);

        // Assert: the optional value remains absent.
        Assert.IsNull(result.ParameterSizeLabel);
    }

    // Verifies that quantization remains optional for successful results.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_NullQuantization_IsStoredAsNull()
    {
        // Act: create success without quantization metadata.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: "granite",
                parameterSizeLabel: "2B",
                quantization: null,
                fileSizeBytes: 1L,
                contextLength: 131_072UL,
                ggufVersion: 3U);

        // Assert: the optional value remains absent.
        Assert.IsNull(result.Quantization);
    }

    // Verifies that context length remains optional for successful results.
    [TestMethod]
    [TestCategory("Unit")]
    public void CreateSuccess_NullContextLength_IsStoredAsNull()
    {
        // Act: create success without context length metadata.
        ModelQuickScanResult result =
            ModelQuickScanResult.CreateSuccess(
                modelName: "Granite 3.3 2B Instruct",
                architecture: "granite",
                parameterSizeLabel: "2B",
                quantization: "Q4_K_M",
                fileSizeBytes: 1L,
                contextLength: null,
                ggufVersion: 3U);

        // Assert: the optional value remains absent.
        Assert.IsNull(result.ContextLength);
    }
}
