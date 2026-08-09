using System.Text;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
[TestCategory("WinUI")]
public sealed class ModelInspectionDisplayTextPolicyTests
{
    private const string NotReported = "Not reported";
    private const string FixedRequiredDetailFallback = "Details are unavailable.";

    public static IEnumerable<object[]> UnsafeTextCases
    {
        get
        {
            yield return ["C0 control", "visible\0hidden"];
            yield return ["C1 control", "visible\u0085hidden"];
            yield return ["newline", "visible\nhidden"];
            yield return ["line separator", "visible\u2028hidden"];
            yield return ["paragraph separator", "visible\u2029hidden"];
            yield return ["bidi override", "visible\u202Ehidden"];
            yield return ["bidi isolate", "visible\u2066hidden"];
            yield return ["private use", "visible\uE000hidden"];
            yield return ["unassigned scalar", "visible\u0378hidden"];
            yield return ["unpaired high surrogate", "visible\uD800hidden"];
            yield return ["unpaired low surrogate", "visible\uDC00hidden"];
            yield return ["drive path", @"C:\Users\private\model.gguf"];
            yield return ["drive-relative path", "C:private-model.gguf"];
            yield return ["UNC path", @"\\server\private\model.gguf"];
            yield return ["POSIX path", "/home/private/model.gguf"];
            yield return ["relative slash", "private/model"];
            yield return ["relative backslash", @"private\model"];
            yield return ["HTTPS URL", "https://example.test/private-model"];
            yield return ["absolute URL without slash", "mailto:private@example.test"];
        }
    }

    public static IEnumerable<object[]> UnsafeFileNameCases
    {
        get
        {
            yield return ["drive path", @"C:\Users\private\model.gguf"];
            yield return ["drive-relative path", "C:private-model.gguf"];
            yield return ["UNC path", @"\\server\private\model.gguf"];
            yield return ["POSIX path", "/home/private/model.gguf"];
            yield return ["relative slash", "private/model.gguf"];
            yield return ["relative backslash", @"private\model.gguf"];
            yield return ["HTTPS URL", "https://example.test/private.gguf"];
            yield return ["absolute URL without slash", "mailto:private@example.test"];
        }
    }

    [UITestMethod]
    public void SafeInternationalText_IsFormCTrimmedAndCollapsesOnlyOrdinarySpaces()
    {
        string modelName = ModelInspectionDisplayTextPolicy.ProjectModelName(
            "  Cafe\u0301   東京  ",
            "unused",
            "unused.gguf");
        string optionalLabel = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            "  Größe   Δοκιμή  ");
        string requiredDetail = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            "  Résumé   模型 😀  ",
            "unused fallback");
        string nonOrdinarySpaces = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            "A\u00A0\u00A0B");

        Assert.AreEqual("Café 東京", modelName);
        Assert.AreEqual("Größe Δοκιμή", optionalLabel);
        Assert.AreEqual("Résumé 模型 😀", requiredDetail);
        Assert.AreEqual("A\u00A0\u00A0B", nonOrdinarySpaces);
        Assert.IsTrue(modelName.IsNormalized(NormalizationForm.FormC));
        Assert.IsTrue(optionalLabel.IsNormalized(NormalizationForm.FormC));
        Assert.IsTrue(requiredDetail.IsNormalized(NormalizationForm.FormC));
    }

    [UITestMethod]
    public void ProjectModelName_UsesFirstSafeCandidateInApprovedOrder()
    {
        Assert.AreEqual(
            "Completed name",
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                "Completed name",
                "Quick scan name",
                "file-name.gguf"));
        Assert.AreEqual(
            "Quick scan name",
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                "unsafe/name",
                "Quick scan name",
                "file-name.gguf"));
        Assert.AreEqual(
            "file-name",
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                "unsafe\nname",
                "unsafe\\name",
                "file-name.gguf"));
        Assert.AreEqual(
            NotReported,
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                "unsafe/name",
                "unsafe\\name",
                "https://example.test/private.gguf"));
    }

    [TestMethod]
    [DynamicData(nameof(UnsafeTextCases))]
    public void PathShapedOrControlBearingText_UsesSafeFallback(
        string caseName,
        string unsafeText)
    {
        string modelName = ModelInspectionDisplayTextPolicy.ProjectModelName(
            unsafeText,
            "Safe quick scan name",
            "safe-file.gguf");
        string optionalLabel = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(unsafeText);
        string requiredDetail = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            unsafeText,
            "Safe generic detail.");

        Assert.AreEqual("Safe quick scan name", modelName, caseName);
        Assert.AreEqual(NotReported, optionalLabel, caseName);
        Assert.AreEqual("Safe generic detail.", requiredDetail, caseName);
        Assert.IsFalse(modelName.Contains(unsafeText, StringComparison.Ordinal), caseName);
        Assert.IsFalse(optionalLabel.Contains(unsafeText, StringComparison.Ordinal), caseName);
        Assert.IsFalse(requiredDetail.Contains(unsafeText, StringComparison.Ordinal), caseName);
    }

    [TestMethod]
    [DynamicData(nameof(UnsafeFileNameCases))]
    public void FileNameFallback_RejectsPathOrUrlBeforeRemovingGguf(
        string caseName,
        string unsafeFileName)
    {
        string projected = ModelInspectionDisplayTextPolicy.ProjectModelName(
            completedName: null,
            quickScanName: null,
            unsafeFileName);

        Assert.AreEqual(NotReported, projected, caseName);
        Assert.IsFalse(projected.Contains(unsafeFileName, StringComparison.Ordinal), caseName);
    }

    [TestMethod]
    [DataRow("Granite-3.3-2B-Instruct.gguf", "Granite-3.3-2B-Instruct")]
    [DataRow("Granite.GGUF", "Granite")]
    [DataRow("Granite.gguf.gguf", "Granite.gguf")]
    [DataRow("Granite.gguf.backup", "Granite.gguf.backup")]
    [DataRow("Granite", "Granite")]
    public void FileNameFallback_StripsOnlyExactFinalGgufCaseInsensitively(
        string fileName,
        string expected)
    {
        Assert.AreEqual(
            expected,
            ModelInspectionDisplayTextPolicy.ProjectModelName(null, null, fileName));
    }

    [UITestMethod]
    public void ExactUtf16Caps_AcceptValuesAtEachBoundary()
    {
        string modelName = BuildUtf16Value(160);
        string optionalLabel = BuildUtf16Value(96);
        string requiredDetail = BuildUtf16Value(512);

        Assert.AreEqual(
            modelName,
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                modelName,
                "fallback",
                "fallback.gguf"));
        Assert.AreEqual(
            optionalLabel,
            ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(optionalLabel));
        Assert.AreEqual(
            requiredDetail,
            ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
                requiredDetail,
                "fallback"));
    }

    [UITestMethod]
    public void ExactUtf16Caps_RejectValuesOneCodeUnitOverEachBoundary()
    {
        string modelName = BuildUtf16Value(160) + "x";
        string optionalLabel = BuildUtf16Value(96) + "x";
        string requiredDetail = BuildUtf16Value(512) + "x";

        Assert.AreEqual(
            "Safe quick scan name",
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                modelName,
                "Safe quick scan name",
                "safe-file.gguf"));
        Assert.AreEqual(
            NotReported,
            ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(optionalLabel));
        Assert.AreEqual(
            "Safe generic detail.",
            ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
                requiredDetail,
                "Safe generic detail."));
    }

    [TestMethod]
    public void FileNameFallback_EnforcesTheModelNameCapAfterRemovingGguf()
    {
        string acceptedBaseName = new('a', 160);
        string rejectedBaseName = new('b', 161);

        Assert.AreEqual(
            acceptedBaseName,
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                completedName: null,
                quickScanName: null,
                acceptedBaseName + ".gguf"));
        Assert.AreEqual(
            NotReported,
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                completedName: null,
                quickScanName: null,
                rejectedBaseName + ".gguf"));
    }

    [UITestMethod]
    public void OverCapBeforeNormalization_IsRejectedEvenWhenFormCWouldFit()
    {
        string modelName = string.Concat(Enumerable.Repeat("e\u0301", 80)) + "x";
        string optionalLabel = string.Concat(Enumerable.Repeat("e\u0301", 48)) + "x";
        string requiredDetail = string.Concat(Enumerable.Repeat("e\u0301", 256)) + "x";

        Assert.IsTrue(modelName.Normalize(NormalizationForm.FormC).Length <= 160);
        Assert.IsTrue(optionalLabel.Normalize(NormalizationForm.FormC).Length <= 96);
        Assert.IsTrue(requiredDetail.Normalize(NormalizationForm.FormC).Length <= 512);
        Assert.AreEqual(
            "Safe quick scan name",
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                modelName,
                "Safe quick scan name",
                "safe-file.gguf"));
        Assert.AreEqual(
            NotReported,
            ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(optionalLabel));
        Assert.AreEqual(
            "Safe generic detail.",
            ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
                requiredDetail,
                "Safe generic detail."));
    }

    [UITestMethod]
    public void NormalizationExpansionBeyondCap_IsRejectedAfterFormC()
    {
        string modelName = new('\u0344', 81);
        string optionalLabel = new('\u0344', 49);
        string requiredDetail = new('\u0344', 257);

        Assert.IsTrue(modelName.Length <= 160);
        Assert.IsTrue(optionalLabel.Length <= 96);
        Assert.IsTrue(requiredDetail.Length <= 512);
        Assert.IsTrue(modelName.Normalize(NormalizationForm.FormC).Length > 160);
        Assert.IsTrue(optionalLabel.Normalize(NormalizationForm.FormC).Length > 96);
        Assert.IsTrue(requiredDetail.Normalize(NormalizationForm.FormC).Length > 512);
        Assert.AreEqual(
            "Safe quick scan name",
            ModelInspectionDisplayTextPolicy.ProjectModelName(
                modelName,
                "Safe quick scan name",
                "safe-file.gguf"));
        Assert.AreEqual(
            NotReported,
            ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(optionalLabel));
        Assert.AreEqual(
            "Safe generic detail.",
            ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
                requiredDetail,
                "Safe generic detail."));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("unsafe/private/path")]
    [DataRow("unsafe\u202Ehidden")]
    [DataRow("https://example.test/private")]
    public void UnsafeOrBlankRequiredFallback_FailsClosedWithoutEchoingInput(
        string? genericFallback)
    {
        const string RejectedValue = "private\nvalue";

        string projected = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            RejectedValue,
            genericFallback!);

        Assert.AreEqual(FixedRequiredDetailFallback, projected);
        Assert.IsFalse(projected.Contains(RejectedValue, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(genericFallback))
        {
            Assert.IsFalse(projected.Contains(genericFallback, StringComparison.Ordinal));
        }
    }

    [UITestMethod]
    public void BlankValues_UseSafeFixedFallbacks()
    {
        Assert.AreEqual(
            NotReported,
            ModelInspectionDisplayTextPolicy.ProjectModelName("  ", "\u00A0", ".gguf"));
        Assert.AreEqual(
            NotReported,
            ModelInspectionDisplayTextPolicy.ProjectOptionalLabel("\u00A0"));
        Assert.AreEqual(
            "Safe generic detail.",
            ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
                "\u00A0",
                "Safe generic detail."));
    }

    private static string BuildUtf16Value(int codeUnitCount)
    {
        Assert.AreEqual(0, codeUnitCount % 2);
        string value = string.Concat(Enumerable.Repeat("😀", codeUnitCount / 2));
        Assert.AreEqual(codeUnitCount, value.Length);
        return value;
    }
}
