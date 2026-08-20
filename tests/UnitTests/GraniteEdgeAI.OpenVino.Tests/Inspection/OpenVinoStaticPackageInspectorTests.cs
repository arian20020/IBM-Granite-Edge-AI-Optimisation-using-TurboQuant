using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Tests.Inspection;

[TestClass]
public sealed class OpenVinoStaticPackageInspectorTests
{
    private const string FixtureModelSha256 = "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";
    private const string FixtureManifestSha256 = "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";

    [TestMethod]
    public void CompleteFixtureProducesDeterministicPathFreeEvidenceButNotReady()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
        Assert.IsNull(result.SupportCode);
        Assert.IsNotNull(result.Evidence);
        Assert.AreEqual(FixtureManifestSha256, result.Evidence.PackageManifestDigest);
        Assert.AreEqual(FixtureModelSha256, result.Evidence.ModelSha256);
        Assert.AreEqual(88L, result.Evidence.ModelLengthBytes);
        Assert.AreEqual("granite", result.Evidence.ModelType);
        Assert.AreEqual("GraniteForCausalLM", result.Evidence.Architecture);
        Assert.AreEqual("text-generation-with-past", result.Evidence.Task);
        Assert.AreEqual(64L, result.Evidence.ContextLength);
        Assert.AreEqual("float32", result.Evidence.Precision);
        Assert.AreEqual("PreTrainedTokenizerFast", result.Evidence.TokenizerClass);
        Assert.AreEqual(9, result.Evidence.ResourceCount);
        Assert.IsFalse(result.Evidence.HasChatTemplate);
        AssertPathFree(package, result);
    }

    [TestMethod]
    public void ManifestIdentityIsIndependentOfCreationOrderAndChangesWithContent()
    {
        using TemporaryPackage first = TemporaryPackage.CopyFixture();
        using TemporaryPackage second = TemporaryPackage.CopyFixture(reverseOrder: true);

        OpenVinoStaticPackageInspectionResult firstResult = Inspect(first);
        OpenVinoStaticPackageInspectionResult secondResult = Inspect(second);
        Assert.AreEqual(firstResult.Evidence!.PackageManifestDigest, secondResult.Evidence!.PackageManifestDigest);

        second.AppendByte("openvino_model.bin", 0x5a);
        OpenVinoStaticPackageInspectionResult changed = Inspect(second);
        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, changed.Status);
        Assert.AreNotEqual(firstResult.Evidence.PackageManifestDigest, changed.Evidence!.PackageManifestDigest);
        Assert.AreNotEqual(firstResult.Evidence.ModelSha256, changed.Evidence.ModelSha256);
    }

    [TestMethod]
    public void InspectionLocksAreReleasedWhenTheOperationCompletes()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, Inspect(package).Status);

        using FileStream exclusive = new(
            package.File("openvino_model.bin"),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        Assert.IsTrue(exclusive.CanWrite);
    }

    [TestMethod]
    public void MissingRequiredResourceUsesTheFixedPathFreeCode()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        File.Delete(package.File("openvino_detokenizer.bin"));

        AssertRejected(package, OpenVinoSupportCode.PackageMissingResource);
    }

    [TestMethod]
    public void InconsistentXmlBinRelationshipUsesTheFixedCode()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        using (FileStream stream = new(package.File("openvino_model.bin"), FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(1);
        }

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void SnapshotterStopsAtThe4097thEntry()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        for (int index = 0; index < 4_088; index++)
        {
            package.Write($"padding-{index:D4}.txt", "x");
        }

        OpenVinoPackageSnapshotCapture capture = new OpenVinoPackageSnapshotter().Capture(package.Root);

        Assert.IsNull(capture.Snapshot);
        Assert.AreEqual(OpenVinoSnapshotFailure.EntryLimitExceeded, capture.Failure);
        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    public void SnapshotterRejectsDepth17()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string relative = string.Join(Path.DirectorySeparatorChar, Enumerable.Range(1, 17).Select(static value => $"d{value:D2}"));
        package.Write(Path.Combine(relative, "leaf.txt"), "x");

        OpenVinoPackageSnapshotCapture capture = new OpenVinoPackageSnapshotter().Capture(package.Root);

        Assert.IsNull(capture.Snapshot);
        Assert.AreEqual(OpenVinoSnapshotFailure.DepthLimitExceeded, capture.Failure);
        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    public void JsonOver16MiBIsRejectedBeforeMaterialization()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetSparseLength("config.json", OpenVinoPackagePolicy.MaximumJsonBytes + 1L);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void JsonDepth33IsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("config.json", "{\"model_type\":" + new string('[', 33) + "0" + new string(']', 33) + "}");

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [DataRow("duplicate")]
    [DataRow("comment")]
    [DataRow("trailing")]
    [DataRow("unknown")]
    [DataRow("null")]
    [DataRow("coercion")]
    [DataRow("nonfinite")]
    [DataRow("optional-null")]
    [DataRow("optional-coercion")]
    [DataRow("quoted-nonfinite")]
    [DataRow("invalid-utf8")]
    public void StrictJsonRejectsMalformedConfiguration(string mutation)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string path = package.File("config.json");
        string json = File.ReadAllText(path);
        switch (mutation)
        {
            case "duplicate":
                File.WriteAllText(path, json.Replace("\"model_type\": \"granite\",", "\"model_type\": \"granite\",\n  \"model_type\": \"granite\","), new UTF8Encoding(false));
                break;
            case "comment":
                File.WriteAllText(path, "/* forbidden */" + json, new UTF8Encoding(false));
                break;
            case "trailing":
                File.WriteAllText(path, json + "{}", new UTF8Encoding(false));
                break;
            case "unknown":
                package.SetJson("config.json", "unreviewed", JsonValue.Create(true));
                break;
            case "null":
                package.SetJson("config.json", "model_type", null);
                break;
            case "coercion":
                package.SetJson("config.json", "max_position_embeddings", JsonValue.Create("64"));
                break;
            case "nonfinite":
                File.WriteAllText(path, json.Replace("\"max_position_embeddings\": 64", "\"max_position_embeddings\": NaN"), new UTF8Encoding(false));
                break;
            case "optional-null":
                package.SetJson("config.json", "attention_bias", null);
                break;
            case "optional-coercion":
                package.SetJson("config.json", "use_cache", JsonValue.Create("true"));
                break;
            case "quoted-nonfinite":
                package.SetJson("config.json", "attention_dropout", JsonValue.Create("NaN"));
                break;
            case "invalid-utf8":
                File.WriteAllBytes(path, [0xff, 0xfe, 0xfd]);
                break;
            default:
                Assert.Fail("Unknown test mutation.");
                break;
        }

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void XmlOver256MiBIsRejectedWithoutMaterializingIt()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetSparseLength("openvino_model.xml", OpenVinoPackagePolicy.MaximumXmlBytes + 1L);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [DataRow("<!DOCTYPE net [<!ENTITY xxe SYSTEM \"file:///forbidden\">]><net name=\"x\" version=\"11\"><layers>&xxe;</layers></net>")]
    [DataRow("<net xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"file:///forbidden\" name=\"x\" version=\"11\"><layers /></net>")]
    public void XmlRejectsDtdEntitiesAndExternalSchemaResolution(string xml)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("openvino_model.xml", xml);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void ReparsePointEscapingTheRootIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string outside = package.WriteOutside("outside.json", "{}");
        File.CreateSymbolicLink(package.File("vocab.json"), outside);

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    public void CaseInsensitiveRelativeNameCollisionIsRejected()
    {
        OpenVinoSnapshotFailure failure = OpenVinoPackageSnapshotter.ValidateRelativeNames(
            ["vocab.json", "VOCAB.JSON"]);
        OpenVinoStaticPackageInspectionResult result = OpenVinoStaticPackageInspector.RejectSnapshotFailure(failure);

        Assert.AreEqual(OpenVinoSnapshotFailure.CaseCollision, failure);
        Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, result.Status);
        Assert.AreEqual(OpenVinoSupportCode.PackageUnsafePath, result.SupportCode);
    }

    [TestMethod]
    public void AlternateDataStreamOnAFileIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.WriteAlternateStream(package.File("config.json"), "hidden", "payload");

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    public void AlternateDataStreamOnADirectoryIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.WriteAlternateStream(package.Root, "hidden", "payload");

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    public void NonRegularRequiredArtifactIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        File.Delete(package.File("openvino_model.bin"));
        Directory.CreateDirectory(package.File("openvino_model.bin"));

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    [DataRow("payload.exe", "MZ")]
    [DataRow("vocab.json", "#!/bin/sh\necho forbidden")]
    public void ExecutableOrScriptContentIsRejected(string relativeName, string content)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write(relativeName, content);

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    public void UnreadableRequiredArtifactUsesTheFixedCode()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        using FileStream blocker = new(package.File("openvino_model.bin"), FileMode.Open, FileAccess.Read, FileShare.None);

        AssertRejected(package, OpenVinoSupportCode.PackageUnreadable);
    }

    [TestMethod]
    public void ActiveWriterIsTreatedAsPackageMutation()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        using FileStream writer = new(package.File("openvino_model.bin"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        AssertRejected(package, OpenVinoSupportCode.PackageChanged);
    }

    [TestMethod]
    [DataRow("model_type", "granitemoe")]
    [DataRow("model_type", "granitemoehybrid")]
    [DataRow("architectures", "GraniteMoeForCausalLM")]
    [DataRow("architectures", "GraniteMoeHybridForCausalLM")]
    [DataRow("architectures", "GraniteVisionForConditionalGeneration")]
    public void UnsupportedArchitectureUsesTheFixedCode(string property, string value)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        JsonNode replacement = property == "architectures"
            ? new JsonArray(JsonValue.Create(value))
            : JsonValue.Create(value)!;
        package.SetJson("config.json", property, replacement);

        AssertRejected(package, OpenVinoSupportCode.ModelArchitectureUnsupported);
    }

    [TestMethod]
    public void NonTextTaskUsesTheFixedCode()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetJson("config.json", "task", JsonValue.Create("automatic-speech-recognition"));

        AssertRejected(package, OpenVinoSupportCode.ModelTaskUnsupported);
    }

    [TestMethod]
    [DataRow("auto_map")]
    [DataRow("trust_remote_code")]
    public void CustomOrRemoteCodeConfigurationUsesTheArchitectureCode(string property)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        JsonNode value = property == "auto_map"
            ? new JsonObject { ["AutoModelForCausalLM"] = "custom.Model" }
            : JsonValue.Create(true)!;
        package.SetJson("config.json", property, value);

        AssertRejected(package, OpenVinoSupportCode.ModelArchitectureUnsupported);
    }

    [TestMethod]
    [DataRow("tokenizer_class", "RemoteTokenizer")]
    [DataRow("model_max_length", "65")]
    public void InvalidTokenizerOrConfigConsistencyUsesTheFixedCode(string property, string value)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        JsonNode replacement = property == "model_max_length"
            ? JsonValue.Create(long.Parse(value, CultureInfo.InvariantCulture))!
            : JsonValue.Create(value)!;
        package.SetJson("tokenizer_config.json", property, replacement);

        AssertRejected(package, OpenVinoSupportCode.TokenizerUnsupported);
    }

    [TestMethod]
    [DataRow("max_position_embeddings", "0")]
    [DataRow("max_position_embeddings", "1048577")]
    [DataRow("torch_dtype", "float64")]
    public void InvalidBoundedContextOrPrecisionIsRejected(string property, string value)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        JsonNode replacement = property == "torch_dtype"
            ? JsonValue.Create(value)!
            : JsonValue.Create(long.Parse(value, CultureInfo.InvariantCulture))!;
        package.SetJson("config.json", property, replacement);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void UnrecognizedOptionalResourceIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("README.md", "not part of package v1");

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    private static OpenVinoStaticPackageInspectionResult Inspect(TemporaryPackage package) =>
        new OpenVinoStaticPackageInspector().Inspect(package.Root);

    private static void AssertRejected(TemporaryPackage package, OpenVinoSupportCode expectedCode)
    {
        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, result.Status);
        Assert.AreEqual(expectedCode, result.SupportCode);
        Assert.IsNull(result.Evidence);
        AssertPathFree(package, result);
    }

    private static void AssertPathFree(TemporaryPackage package, OpenVinoStaticPackageInspectionResult result)
    {
        string serialized = JsonSerializer.Serialize(result);
        Assert.IsFalse(serialized.Contains(package.OperationRoot, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.ToString().Contains(package.OperationRoot, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TemporaryPackage : IDisposable
    {
        private static readonly string FixtureRoot = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "OpenVINO",
            "GenAI",
            "TinySyntheticV1",
            "package");
        private static readonly string TestRoot = Path.Combine(Path.GetTempPath(), "granite-openvino-package-tests");
        private readonly List<string> reparsePoints = [];

        private TemporaryPackage(bool reverseOrder)
        {
            OperationRoot = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            Root = Path.Combine(OperationRoot, "package");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.Combine(OperationRoot, "outside"));
            string[] files = Directory.GetFiles(FixtureRoot, "*", SearchOption.AllDirectories);
            if (reverseOrder)
            {
                Array.Reverse(files);
            }

            foreach (string source in files)
            {
                string relative = Path.GetRelativePath(FixtureRoot, source);
                string destination = File(relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                System.IO.File.Copy(source, destination);
            }
        }

        public string OperationRoot { get; }

        public string Root { get; }

        public static TemporaryPackage CopyFixture(bool reverseOrder = false) => new(reverseOrder);

        public string File(string relativeName) => Path.Combine(Root, relativeName);

        public void Write(string relativeName, string content)
        {
            string path = File(relativeName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        public string WriteOutside(string relativeName, string content)
        {
            string path = Path.Combine(OperationRoot, "outside", relativeName);
            System.IO.File.WriteAllText(path, content, new UTF8Encoding(false));
            reparsePoints.Add(File("vocab.json"));
            return path;
        }

        public void AppendByte(string relativeName, byte value)
        {
            using FileStream stream = new(File(relativeName), FileMode.Append, FileAccess.Write, FileShare.None);
            stream.WriteByte(value);
        }

        public void SetSparseLength(string relativeName, long length)
        {
            using FileStream stream = new(File(relativeName), FileMode.Open, FileAccess.Write, FileShare.None);
            stream.SetLength(length);
        }

        public void SetJson(string relativeName, string property, JsonNode? value)
        {
            string path = File(relativeName);
            JsonObject document = JsonNode.Parse(System.IO.File.ReadAllText(path))!.AsObject();
            document[property] = value;
            System.IO.File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The package helper owns every test mutation operation.")]
        public void WriteAlternateStream(string path, string streamName, string content) =>
            System.IO.File.WriteAllText(path + ":" + streamName, content, new UTF8Encoding(false));

        public void Dispose()
        {
            foreach (string reparsePoint in reparsePoints)
            {
                if (System.IO.File.Exists(reparsePoint))
                {
                    System.IO.File.Delete(reparsePoint);
                }
            }

            string canonicalOperationRoot = Path.GetFullPath(OperationRoot);
            string canonicalTestRoot = Path.GetFullPath(TestRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (canonicalOperationRoot.StartsWith(canonicalTestRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(canonicalOperationRoot))
            {
                Directory.Delete(canonicalOperationRoot, recursive: true);
            }
        }

    }
}
