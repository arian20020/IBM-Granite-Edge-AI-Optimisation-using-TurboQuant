using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
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
        Assert.AreEqual("float32", result.Evidence.WeightPrecision);
        Assert.AreEqual("PreTrainedTokenizerFast", result.Evidence.TokenizerClass);
        Assert.AreEqual(1, result.Evidence.LayerCount);
        Assert.AreEqual(4, result.Evidence.EmbeddingSize);
        Assert.AreEqual(1, result.Evidence.AttentionHeadCount);
        Assert.AreEqual(1, result.Evidence.KeyValueHeadCount);
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
    public void SnapshotHashingReportsMonotonicProgressAcrossBothIntegrityPasses()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        List<double> observed = [];
        var progress = new InlineProgress<double>(observed.Add);

        using OpenVinoPackageSnapshot snapshot = new OpenVinoPackageSnapshotter()
            .Capture(package.Root, CancellationToken.None, progress)
            .Snapshot!;

        Assert.IsNotNull(snapshot);
        Assert.IsGreaterThan(2, observed.Count);
        Assert.AreEqual(0d, observed[0]);
        Assert.AreEqual(1d, observed[^1]);
        for (int index = 1; index < observed.Count; index++)
        {
            Assert.IsGreaterThanOrEqualTo(observed[index - 1], observed[index]);
        }
    }

    [TestMethod]
    public void SnapshotHashingHonoursCancellationReportedDuringTheFirstIntegrityPass()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        using CancellationTokenSource cancellation = new();
        List<double> observed = [];
        var progress = new InlineProgress<double>(fraction =>
        {
            observed.Add(fraction);
            if (fraction > 0d)
            {
                cancellation.Cancel();
            }
        });

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            new OpenVinoPackageSnapshotter().Capture(
                package.Root,
                cancellation.Token,
                progress));
        Assert.IsTrue(observed.Any(static fraction => fraction > 0d));
        Assert.IsFalse(observed.Contains(1d));
    }

    [TestMethod]
    [MalformedCase("missing-required", OpenVinoSupportCode.PackageMissingResource)]
    public void MissingRequiredResourceUsesTheFixedPathFreeCode()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        File.Delete(package.File("openvino_detokenizer.bin"));

        AssertRejected(package, OpenVinoSupportCode.PackageMissingResource);
    }

    [TestMethod]
    [MalformedCase("inconsistent-pair", OpenVinoSupportCode.PackageInconsistentResource)]
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
    [MalformedCase("entry-4097", OpenVinoSupportCode.PackageUnsafePath)]
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
    [MalformedCase("depth-17", OpenVinoSupportCode.PackageUnsafePath)]
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
    [MalformedCase("json-over-limit", OpenVinoSupportCode.PackageInconsistentResource)]
    public void JsonOver16MiBIsRejectedBeforeMaterialization()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetSparseLength("config.json", OpenVinoPackagePolicy.MaximumJsonBytes + 1L);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [MalformedCase("json-depth-33", OpenVinoSupportCode.PackageInconsistentResource)]
    public void JsonDepth33IsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("config.json", "{\"model_type\":" + new string('[', 33) + "0" + new string(']', 33) + "}");

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [MalformedCase("json-strictness", OpenVinoSupportCode.PackageInconsistentResource)]
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
    public void VersionOneOptionalJsonResourcesUseClosedTypedSchemas()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("vocab.json", "{\"<|pad|>\":0,\"<|bos|>\":1,\"<|eos|>\":2,\"<unk>\":3,\"a\":4,\"b\":5,\"c\":6,\"d\":7}");
        package.Write("added_tokens.json", "{\"<|pad|>\":0,\"<|bos|>\":1,\"<|eos|>\":2}");
        package.Write("special_tokens_map.json", "{\"bos_token\":{\"content\":\"<|bos|>\",\"lstrip\":false,\"normalized\":false,\"rstrip\":false,\"single_word\":false},\"eos_token\":\"<|eos|>\",\"pad_token\":\"<|pad|>\",\"unk_token\":\"<unk>\"}");
        package.Write("chat_template.json", "{\"chat_template\":\"{% for message in messages %}{{ message.content }}{% endfor %}\"}");
        package.Write("tokenizer.json", "{\"version\":\"1.0\",\"added_tokens\":[{\"id\":0,\"content\":\"<|pad|>\",\"single_word\":false,\"lstrip\":false,\"rstrip\":false,\"normalized\":false,\"special\":true}],\"model\":{\"type\":\"BPE\",\"vocab\":{\"<|pad|>\":0,\"<|bos|>\":1,\"<|eos|>\":2,\"<unk>\":3,\"a\":4,\"b\":5,\"c\":6,\"d\":7},\"merges\":[\"a b\"],\"unk_token\":\"<unk>\",\"fuse_unk\":false,\"byte_fallback\":false,\"ignore_merges\":false}}");

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
        Assert.AreEqual(14, result.Evidence!.ResourceCount);
        Assert.IsTrue(result.Evidence.HasChatTemplate);
    }

    [TestMethod]
    [DataRow("tokenizer.json", "{\"version\":\"2.0\",\"added_tokens\":[],\"model\":{\"type\":\"BPE\",\"vocab\":{\"a\":0},\"merges\":[]}}")]
    [DataRow("tokenizer.json", "{\"version\":\"1.0\",\"added_tokens\":[{\"id\":\"0\",\"content\":\"a\",\"single_word\":false,\"lstrip\":false,\"rstrip\":false,\"normalized\":false,\"special\":false}],\"model\":{\"type\":\"BPE\",\"vocab\":{\"a\":0},\"merges\":[]}}")]
    [DataRow("tokenizer.json", "{\"version\":\"1.0\",\"added_tokens\":[],\"model\":{\"type\":\"BPE\",\"vocab\":{\"a\":0},\"merges\":[],\"unreviewed\":true}}")]
    [DataRow("vocab.json", "{\"a\":\"0\"}")]
    [DataRow("added_tokens.json", "{\"a\":0,\"b\":0}")]
    [DataRow("special_tokens_map.json", "{\"bos_token\":{\"content\":\"<bos>\",\"lstrip\":false,\"normalized\":false,\"rstrip\":false,\"single_word\":false,\"remote\":true}}")]
    [DataRow("special_tokens_map.json", "{\"bos_token\":null}")]
    [DataRow("chat_template.json", "{\"chat_template\":42}")]
    [DataRow("chat_template.json", "{\"chat_template\":\"ok\",\"unknown\":true}")]
    [DataRow("vocab.json", "{\"a\":0,\"a\":1}")]
    [DataRow("tokenizer.json", "{\"version\":\"1.0\",\"added_tokens\":[],\"model\":{\"type\":\"BPE\",\"vocab\":{\"a\":0},\"merges\":[],\"dropout\":NaN}}")]
    public void OptionalJsonRejectsUnknownWrongCoercedNullDuplicateOrInvalidValues(string resource, string json)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write(resource, json);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void OptionalJsonRejectsExcessiveDepth()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("chat_template.json", "{\"chat_template\":" + new string('[', 33) + "\"x\"" + new string(']', 33) + "}");

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void OptionalJsonIsBoundedBeforeMaterialization()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("vocab.json", "{}");
        package.SetSparseLength("vocab.json", OpenVinoPackagePolicy.MaximumJsonBytes + 1L);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [DataRow("chat_template.jinja")]
    [DataRow("merges.txt")]
    public void OptionalTextResourcesRejectMalformedUtf8(string resource)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        File.WriteAllBytes(package.File(resource), [0xff, 0xfe, 0xfd]);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void OptionalTextResourcesAcceptStrictUtf8()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("chat_template.jinja", "{{ messages | length }}");
        package.Write("merges.txt", "#version: 0.2\na b");

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
        Assert.IsTrue(result.Evidence!.HasChatTemplate);
    }

    [TestMethod]
    [DataRow("chat_template.jinja")]
    [DataRow("merges.txt")]
    public void OptionalTextResourcesAreBoundedBeforeDecoding(string resource)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write(resource, "x");
        package.SetSparseLength(resource, OpenVinoPackagePolicy.MaximumTextBytes + 1L);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [MalformedCase("xml-over-limit", OpenVinoSupportCode.PackageInconsistentResource)]
    public void XmlOver256MiBIsRejectedWithoutMaterializingIt()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetSparseLength("openvino_model.xml", OpenVinoPackagePolicy.MaximumXmlBytes + 1L);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void ValidXmlBetweenTextAndXmlCapsIsStreamedAndAccepted()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.PadXmlWithComment("openvino_model.xml", OpenVinoPackagePolicy.MaximumTextBytes + 1L);

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
    }

    [TestMethod]
    [MalformedCase("xml-dtd-entity-external", OpenVinoSupportCode.PackageInconsistentResource)]
    [MalformedCase("xml-external-schema", OpenVinoSupportCode.PackageInconsistentResource)]
    [DataRow("<!DOCTYPE net [<!ENTITY xxe SYSTEM \"file:///forbidden\">]><net name=\"x\" version=\"11\"><layers>&xxe;</layers></net>")]
    [DataRow("<net xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"file:///forbidden\" name=\"x\" version=\"11\"><layers /></net>")]
    public void XmlRejectsDtdEntitiesAndExternalSchemaResolution(string xml)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("openvino_model.xml", xml);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [MalformedCase("reparse-escape", OpenVinoSupportCode.PackageUnsafePath)]
    public void ReparsePointEscapingTheRootIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string outside = package.WriteOutside("outside.json", "{}");
        string link = package.File("vocab.json");
        File.CreateSymbolicLink(link, outside);
        package.TrackFileReparsePoint(link);

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
    [MalformedCase("file-alternate-stream", OpenVinoSupportCode.PackageUnsafePath)]
    public void AlternateDataStreamOnAFileIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.WriteAlternateStream(package.File("config.json"), "hidden", "payload");

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    [MalformedCase("directory-alternate-stream", OpenVinoSupportCode.PackageUnsafePath)]
    public void AlternateDataStreamOnADirectoryIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.WriteAlternateStream(package.Root, "hidden", "payload");

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    [MalformedCase("non-regular-required", OpenVinoSupportCode.PackageUnsafePath)]
    public void NonRegularRequiredArtifactIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        File.Delete(package.File("openvino_model.bin"));
        Directory.CreateDirectory(package.File("openvino_model.bin"));

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    [MalformedCase("executable-content", OpenVinoSupportCode.PackageUnsafePath)]
    [MalformedCase("script-content", OpenVinoSupportCode.PackageUnsafePath)]
    [DataRow("payload.exe", "MZ")]
    [DataRow("vocab.json", "#!/bin/sh\necho forbidden")]
    public void ExecutableOrScriptContentIsRejected(string relativeName, string content)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write(relativeName, content);

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    [MalformedCase("unreadable", OpenVinoSupportCode.PackageUnreadable)]
    public void UnreadableRequiredArtifactUsesTheFixedCode()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        using FileStream blocker = new(package.File("openvino_model.bin"), FileMode.Open, FileAccess.Read, FileShare.None);

        AssertRejected(package, OpenVinoSupportCode.PackageUnreadable);
    }

    [TestMethod]
    [MalformedCase("mutating", OpenVinoSupportCode.PackageChanged)]
    public void ActiveWriterIsTreatedAsPackageMutation()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        using FileStream writer = new(package.File("openvino_model.bin"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        AssertRejected(package, OpenVinoSupportCode.PackageChanged);
    }

    [TestMethod]
    public void LaterFileReplacementByOutsideSymlinkIsRejectedAsUnsafe()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string outside = package.WriteOutside("replacement-tokenizer.json", "{}");
        string victim = package.File("tokenizer_config.json");
        OpenVinoPackageSnapshotter snapshotter = new(observer: (stage, relativeName) =>
        {
            if (stage == OpenVinoPackageCaptureStage.BeforeAcquireFile && relativeName == "tokenizer_config.json")
            {
                File.Delete(victim);
                File.CreateSymbolicLink(victim, outside);
                package.TrackFileReparsePoint(victim);
            }
        });

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath, snapshotter);
    }

    [TestMethod]
    [DataRow("replace")]
    [DataRow("delete")]
    public void RequiredFileReplacementOrDisappearanceDuringCaptureIsChanged(string mutation)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string victim = package.File("tokenizer_config.json");
        OpenVinoPackageSnapshotter snapshotter = new(observer: (stage, relativeName) =>
        {
            if (stage != OpenVinoPackageCaptureStage.BeforeAcquireFile || relativeName != "tokenizer_config.json")
            {
                return;
            }

            File.Delete(victim);
            if (mutation == "replace")
            {
                File.WriteAllText(victim, "{}", new UTF8Encoding(false));
            }
        });

        AssertRejected(package, OpenVinoSupportCode.PackageChanged, snapshotter);
    }

    [TestMethod]
    [DataRow("directory")]
    [DataRow("junction")]
    public void FileReplacementByDirectoryOrJunctionDuringFinalAcquisitionIsUnsafe(string replacement)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string victim = package.File("tokenizer_config.json");
        string junctionTarget = package.CreateOutsideDirectory("acquisition-junction-target");
        OpenVinoPackageSnapshotter snapshotter = new(observer: (stage, relativeName) =>
        {
            if (stage != OpenVinoPackageCaptureStage.BeforeAcquireFile || relativeName != "tokenizer_config.json")
            {
                return;
            }

            File.Delete(victim);
            if (replacement == "directory")
            {
                Directory.CreateDirectory(victim);
            }
            else
            {
                package.CreateJunction(victim, junctionTarget);
            }
        });

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath, snapshotter);
    }

    [TestMethod]
    public void EntryDeletedAfterEnumerationButBeforeDiscoveryOpenIsChanged()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string victim = package.File("tokenizer_config.json");
        OpenVinoPackageSnapshotter snapshotter = new(observer: (stage, relativeName) =>
        {
            if (stage == OpenVinoPackageCaptureStage.BeforeDiscoverOpen && relativeName == "tokenizer_config.json")
            {
                File.Delete(victim);
            }
        });

        AssertRejected(package, OpenVinoSupportCode.PackageChanged, snapshotter);
    }

    [TestMethod]
    public void DriveRootNormalizationPreservesTheRootSeparator()
    {
        string driveRoot = Path.GetPathRoot(Environment.SystemDirectory)!;

        string normalized = OpenVinoPackageSnapshotter.NormalizePackageRoot(driveRoot);

        Assert.AreEqual(driveRoot, normalized);
        Assert.IsTrue(Path.EndsInDirectorySeparator(normalized));
    }

    [TestMethod]
    public void EntryAddedAfterAllHandlesAreAcquiredIsChanged()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        OpenVinoPackageSnapshotter snapshotter = new(observer: (stage, _) =>
        {
            if (stage == OpenVinoPackageCaptureStage.AfterAllHandlesAcquired)
            {
                package.Write("vocab.json", "{\"fixture\":0}");
            }
        });

        AssertRejected(package, OpenVinoSupportCode.PackageChanged, snapshotter);
    }

    [TestMethod]
    [DataRow("AfterHashesCompleted")]
    [DataRow("BeforeInspectorFinalValidation")]
    public void EntryAddedDuringEveryFinalValidationWindowIsChanged(string stageName)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        OpenVinoPackageCaptureStage mutationStage = Enum.Parse<OpenVinoPackageCaptureStage>(stageName);
        OpenVinoPackageSnapshotter snapshotter = new(observer: (stage, _) =>
        {
            if (stage == mutationStage)
            {
                package.Write("vocab.json", "{\"fixture\":0}");
            }
        });

        AssertRejected(package, OpenVinoSupportCode.PackageChanged, snapshotter);
    }

    [TestMethod]
    public void RetainedHandlesAllowReadersAndBlockWritersAndDeletion()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        bool readerOpened = false;
        bool writerBlocked = false;
        bool deleteBlocked = false;
        OpenVinoPackageSnapshotter snapshotter = new(observer: (stage, _) =>
        {
            if (stage != OpenVinoPackageCaptureStage.AfterAllHandlesAcquired)
            {
                return;
            }

            using (FileStream reader = new(package.File("openvino_model.bin"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                readerOpened = reader.CanRead;
            }

            try
            {
                using FileStream writerAttempt = new(package.File("openvino_model.bin"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (IOException)
            {
                writerBlocked = true;
            }

            try
            {
                File.Delete(package.File("openvino_model.bin"));
            }
            catch (IOException)
            {
                deleteBlocked = true;
            }
        });

        OpenVinoStaticPackageInspectionResult result = Inspect(package, snapshotter);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
        Assert.IsTrue(readerOpened);
        Assert.IsTrue(writerBlocked);
        Assert.IsTrue(deleteBlocked);
    }

    [TestMethod]
    public void RootDirectorySymlinkIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string selectedRoot = Path.Combine(package.OperationRoot, "selected-link");
        Directory.CreateSymbolicLink(selectedRoot, package.Root);
        package.TrackDirectoryReparsePoint(selectedRoot);

        AssertRejected(selectedRoot, package.OperationRoot, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    public void DescendantJunctionIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        string target = package.CreateOutsideDirectory("junction-target");
        string junction = package.File("tokenizer-assets");
        package.CreateJunction(junction, target);

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath);
    }

    [TestMethod]
    [MalformedCase("case-collision", OpenVinoSupportCode.PackageUnsafePath)]
    public void FullCaptureRejectsInjectedCaseSensitiveCollision()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("vocab.json", "{}");
        OpenVinoPackageSnapshotter snapshotter = new(enumerateEntries: directory =>
        {
            string[] actual = Directory.GetFileSystemEntries(directory);
            return string.Equals(Path.GetFullPath(directory), Path.GetFullPath(package.Root), StringComparison.OrdinalIgnoreCase)
                ? actual.Append(package.File("VOCAB.JSON"))
                : actual;
        });

        AssertRejected(package, OpenVinoSupportCode.PackageUnsafePath, snapshotter);
    }

    [TestMethod]
    [MalformedCase("architecture", OpenVinoSupportCode.ModelArchitectureUnsupported)]
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
    [MalformedCase("task", OpenVinoSupportCode.ModelTaskUnsupported)]
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
    [MalformedCase("tokenizer", OpenVinoSupportCode.TokenizerUnsupported)]
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
    [DataRow("duplicate-config-id")]
    [DataRow("duplicate-tokenizer-string")]
    [DataRow("vocab-id-mismatch")]
    [DataRow("tokenizer-json-id-mismatch")]
    [DataRow("added-token-id-mismatch")]
    [DataRow("special-map-role-mismatch")]
    [DataRow("special-map-duplicate")]
    public void CrossResourceTokenizerFactsRejectMismatchOrAmbiguityWithFixedCode(string mutation)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        const string consistentVocabulary = "{\"<|pad|>\":0,\"<|bos|>\":1,\"<|eos|>\":2,\"<unk>\":3,\"a\":4,\"b\":5,\"c\":6,\"d\":7}";
        const string swappedVocabulary = "{\"<|pad|>\":0,\"<|bos|>\":2,\"<|eos|>\":1,\"<unk>\":3,\"a\":4,\"b\":5,\"c\":6,\"d\":7}";
        switch (mutation)
        {
            case "duplicate-config-id":
                package.SetJson("config.json", "bos_token_id", JsonValue.Create(2));
                package.SetJson("generation_config.json", "bos_token_id", JsonValue.Create(2));
                break;
            case "duplicate-tokenizer-string":
                package.SetJson("tokenizer_config.json", "bos_token", JsonValue.Create("<|eos|>"));
                break;
            case "vocab-id-mismatch":
                package.Write("vocab.json", swappedVocabulary);
                break;
            case "tokenizer-json-id-mismatch":
                package.Write("tokenizer.json", "{\"version\":\"1.0\",\"added_tokens\":[],\"model\":{\"type\":\"BPE\",\"vocab\":" + swappedVocabulary + ",\"merges\":[]}}");
                break;
            case "added-token-id-mismatch":
                package.Write("added_tokens.json", "{\"<|pad|>\":0,\"<|bos|>\":2,\"<|eos|>\":1}");
                break;
            case "special-map-role-mismatch":
                package.Write("vocab.json", consistentVocabulary);
                package.Write("special_tokens_map.json", "{\"bos_token\":\"<|eos|>\",\"eos_token\":\"<|bos|>\",\"pad_token\":\"<|pad|>\"}");
                break;
            case "special-map-duplicate":
                package.Write("vocab.json", consistentVocabulary);
                package.Write("special_tokens_map.json", "{\"bos_token\":\"<|bos|>\",\"eos_token\":\"<|eos|>\",\"pad_token\":\"<|pad|>\",\"additional_special_tokens\":[\"<|bos|>\",\"<|bos|>\"]}");
                break;
            default:
                Assert.Fail("Unknown tokenizer mutation.");
                break;
        }

        AssertRejected(package, OpenVinoSupportCode.TokenizerUnsupported);
    }

    [TestMethod]
    public void MatchingBosAndEosAliasIsAcceptedForGranitePackages()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetJson("config.json", "bos_token_id", JsonValue.Create(2));
        package.SetJson("generation_config.json", "bos_token_id", JsonValue.Create(2));
        package.SetJson("tokenizer_config.json", "bos_token", JsonValue.Create("<|eos|>"));

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(
            OpenVinoStaticInspectionStatus.NativeValidationRequired,
            result.Status,
            "A Granite tokenizer may intentionally use the same exact token and identifier for BOS and EOS.");
        Assert.IsNull(result.SupportCode);
        Assert.IsNotNull(result.Evidence);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("unresolved")]
    [DataRow("special-map-mismatch")]
    public void TokenizerUnknownTokenMustBePresentResolvableAndConsistent(string mutation)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        const string vocabulary = "{\"<|pad|>\":0,\"<|bos|>\":1,\"<|eos|>\":2,\"<unk>\":3,\"a\":4,\"b\":5,\"c\":6,\"d\":7}";
        const string tokenizerJson = "{\"version\":\"1.0\",\"added_tokens\":[],\"model\":{\"type\":\"BPE\",\"vocab\":" + vocabulary + ",\"merges\":[],\"unk_token\":\"<unk>\"}}";
        switch (mutation)
        {
            case "missing":
                package.Write("tokenizer.json", tokenizerJson.Replace(",\"unk_token\":\"<unk>\"", "", StringComparison.Ordinal));
                break;
            case "unresolved":
                package.Write("tokenizer.json", tokenizerJson.Replace("\"unk_token\":\"<unk>\"", "\"unk_token\":\"<missing>\"", StringComparison.Ordinal));
                break;
            case "special-map-mismatch":
                package.Write("tokenizer.json", tokenizerJson);
                package.Write("special_tokens_map.json", "{\"unk_token\":\"a\"}");
                break;
            default:
                Assert.Fail("Unknown tokenizer unknown-token mutation.");
                break;
        }

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
    [DataRow("float16")]
    [DataRow("bfloat16")]
    public void ReducedPrecisionGranitePackageAcceptsStableFp32Logits(string configuredPrecision)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetJson("config.json", "torch_dtype", JsonValue.Create(configuredPrecision));

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(
            OpenVinoStaticInspectionStatus.NativeValidationRequired,
            result.Status,
            "A reduced-precision Granite export may retain its public logits output in FP32.");
        Assert.IsNull(result.SupportCode);
        Assert.IsNotNull(result.Evidence);
    }

    [TestMethod]
    public void Fp32BoundaryWithFp16StoredWeightsReportsWeightPrecisionSeparately()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetFloatingConstantPrecision("f16");

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
        Assert.IsNotNull(result.Evidence);
        Assert.AreEqual("float32", result.Evidence.Precision);
        Assert.AreEqual("float16", result.Evidence.WeightPrecision);
    }

    [TestMethod]
    public void QuantizedU4ConstantsReportInt4WeightPrecision()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetFloatingConstantPrecision("u4");

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
        Assert.IsNotNull(result.Evidence);
        Assert.AreEqual("float32", result.Evidence.Precision);
        Assert.AreEqual("int4", result.Evidence.WeightPrecision);
    }

    [TestMethod]
    [DataRow("hidden_size", 0L)]
    [DataRow("hidden_size", 1_048_577L)]
    [DataRow("intermediate_size", 0L)]
    [DataRow("intermediate_size", 16_777_217L)]
    [DataRow("num_attention_heads", 0L)]
    [DataRow("num_attention_heads", 3L)]
    [DataRow("num_key_value_heads", 2L)]
    [DataRow("num_hidden_layers", 65_537L)]
    [DataRow("vocab_size", 100_000_001L)]
    public void GraniteNumericInvariantsRejectImpossibleConfiguration(string property, long value)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.SetJson("config.json", property, JsonValue.Create(value));

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [DataRow("model-logits-dimension")]
    [DataRow("model-logits-precision")]
    [DataRow("tokenizer-input-ids")]
    [DataRow("tokenizer-attention-mask")]
    [DataRow("detokenizer-string-output")]
    public void IrPortsMustAgreeWithApprovedConfigurationAndTokenizerFacts(string mutation)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        switch (mutation)
        {
            case "model-logits-dimension":
                package.MutateNamedPort("openvino_model.xml", "logits", port => port.Elements("dim").Last().Value = "7");
                break;
            case "model-logits-precision":
                package.MutateNamedPort("openvino_model.xml", "logits", port => port.SetAttributeValue("precision", "FP16"));
                break;
            case "tokenizer-input-ids":
                package.MutateNamedPort("openvino_tokenizer.xml", "input_ids", port => port.SetAttributeValue("names", "unreviewed_ids"));
                break;
            case "tokenizer-attention-mask":
                package.MutateNamedPort("openvino_tokenizer.xml", "attention_mask", port => port.SetAttributeValue("names", "unreviewed_mask"));
                break;
            case "detokenizer-string-output":
                package.MutateNamedPort("openvino_detokenizer.xml", "string_output", port => port.SetAttributeValue("names", "unreviewed_text"));
                break;
            default:
                Assert.Fail("Unknown IR mutation.");
                break;
        }

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [DataRow("openvino_model.xml", "logits", "FP32", "8")]
    [DataRow("openvino_tokenizer.xml", "input_ids", "I64", null)]
    [DataRow("openvino_detokenizer.xml", "string_output", "STRING", null)]
    public void DisconnectedDecoyNamedPortCannotSatisfyRequiredGraphOutput(
        string resource,
        string portName,
        string precision,
        string? finalDimension)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.AddDisconnectedDecoyGraphOutput(resource, portName, precision, finalDimension);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void IntermediateTensorMayShareTheNameOfTheUniqueResultConnectedOutput()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.AddIntermediateDuplicateNamedPort(
            "openvino_model.xml",
            "logits",
            "FP32",
            "8");

        OpenVinoStaticPackageInspectionResult result = Inspect(package);

        Assert.AreEqual(
            OpenVinoStaticInspectionStatus.NativeValidationRequired,
            result.Status,
            "An intermediate tensor name must not hide the unique output wired to the declared Result node.");
        Assert.IsNull(result.SupportCode);
        Assert.IsNotNull(result.Evidence);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("wrong")]
    [DataRow("extra-wrong")]
    [DataRow("duplicate")]
    public void ResultDestinationPortMustBeDeclaredAndUniquelyConnected(string mutation)
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.MutateResultDestinationEdge("openvino_model.xml", "logits", mutation);

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    [MalformedCase("unknown-optional", OpenVinoSupportCode.PackageInconsistentResource)]
    public void UnrecognizedOptionalResourceIsRejected()
    {
        using TemporaryPackage package = TemporaryPackage.CopyFixture();
        package.Write("README.md", "not part of package v1");

        AssertRejected(package, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void MalformedMatrixExactlyMatchesExecutingCaseIds()
    {
        string matrixPath = Path.Combine(AppContext.BaseDirectory, "TestFixtures", "OpenVINO", "Malformed", "matrix.json");
        using JsonDocument matrix = JsonDocument.Parse(File.ReadAllBytes(matrixPath));
        string[] matrixCases = matrix.RootElement.GetProperty("cases").EnumerateArray()
            .Select(static item => $"{item.GetProperty("id").GetString()}|{item.GetProperty("supportCode").GetString()}")
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        MalformedCaseAttribute[] executingAttributes = typeof(OpenVinoStaticPackageInspectorTests).GetMethods()
            .Where(static method => method.GetCustomAttributes(typeof(TestMethodAttribute), inherit: false).Length == 1)
            .SelectMany(static method => method.GetCustomAttributes(typeof(MalformedCaseAttribute), inherit: false))
            .Cast<MalformedCaseAttribute>()
            .ToArray();
        string[] executingCases = executingAttributes
            .Select(static attribute => $"{attribute.Id}|{attribute.SupportCode.ToProtocolValue()}")
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(matrixCases.Length, matrixCases.Distinct(StringComparer.Ordinal).Count(), "Matrix ID/code pairs must be unique.");
        Assert.AreEqual(
            executingAttributes.Length,
            executingAttributes.Select(static attribute => attribute.Id).Distinct(StringComparer.Ordinal).Count(),
            "Executing case IDs must be unique.");
        CollectionAssert.AreEqual(matrixCases, executingCases);
    }

    private static OpenVinoStaticPackageInspectionResult Inspect(
        TemporaryPackage package,
        OpenVinoPackageSnapshotter? snapshotter = null) =>
        snapshotter is null
            ? new OpenVinoStaticPackageInspector().Inspect(package.Root)
            : new OpenVinoStaticPackageInspector(snapshotter).Inspect(package.Root);

    private static void AssertRejected(
        TemporaryPackage package,
        OpenVinoSupportCode expectedCode,
        OpenVinoPackageSnapshotter? snapshotter = null)
    {
        OpenVinoStaticPackageInspectionResult result = Inspect(package, snapshotter);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, result.Status);
        Assert.AreEqual(expectedCode, result.SupportCode);
        Assert.IsNull(result.Evidence);
        AssertPathFree(package, result);
    }

    private static void AssertRejected(string root, string operationRoot, OpenVinoSupportCode expectedCode)
    {
        OpenVinoStaticPackageInspectionResult result = new OpenVinoStaticPackageInspector().Inspect(root);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, result.Status);
        Assert.AreEqual(expectedCode, result.SupportCode);
        Assert.IsFalse(JsonSerializer.Serialize(result).Contains(operationRoot, StringComparison.OrdinalIgnoreCase));
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
        private readonly List<(string Path, bool IsDirectory)> reparsePoints = [];

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
            return path;
        }

        public string CreateOutsideDirectory(string relativeName)
        {
            string path = Path.Combine(OperationRoot, "outside", relativeName);
            Directory.CreateDirectory(path);
            return path;
        }

        public void TrackFileReparsePoint(string path) => reparsePoints.Add((path, false));

        public void TrackDirectoryReparsePoint(string path) => reparsePoints.Add((path, true));

        public void CreateJunction(string junction, string target)
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { "/d", "/c", "mklink", "/J", junction, target }
            })!;
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, process.StandardError.ReadToEnd());
            TrackDirectoryReparsePoint(junction);
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

        public void PadXmlWithComment(string relativeName, long minimumLength)
        {
            string path = File(relativeName);
            string xml = System.IO.File.ReadAllText(path);
            int closingTag = xml.LastIndexOf("</net>", StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, closingTag);
            using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024);
            using StreamWriter writer = new(stream, new UTF8Encoding(false), 64 * 1024, leaveOpen: false);
            writer.Write(xml.AsSpan(0, closingTag));
            writer.Write("<!--");
            char[] padding = Enumerable.Repeat(' ', 64 * 1024).ToArray();
            long remaining = minimumLength - closingTag - 4 - 3 - (xml.Length - closingTag);
            while (remaining > 0)
            {
                int count = (int)Math.Min(padding.Length, remaining);
                writer.Write(padding, 0, count);
                remaining -= count;
            }

            writer.Write("-->");
            writer.Write(xml.AsSpan(closingTag));
        }

        public void SetJson(string relativeName, string property, JsonNode? value)
        {
            string path = File(relativeName);
            JsonObject document = JsonNode.Parse(System.IO.File.ReadAllText(path))!.AsObject();
            document[property] = value;
            System.IO.File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        public void MutateNamedPort(string relativeName, string portName, Action<XElement> mutation)
        {
            string path = File(relativeName);
            XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            XElement port = document.Descendants("port").Single(element =>
                ((string?)element.Attribute("names"))?.Split(',').Contains(portName, StringComparer.Ordinal) == true);
            mutation(port);
            System.IO.File.WriteAllText(path, document.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
        }

        public void AddDisconnectedDecoyGraphOutput(
            string relativeName,
            string portName,
            string precision,
            string? finalDimension)
        {
            string path = File(relativeName);
            XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            XElement connectedOutput = document.Descendants("port").Single(element =>
                ((string?)element.Attribute("names"))?.Split(',').Contains(portName, StringComparer.Ordinal) == true);
            connectedOutput.SetAttributeValue("names", $"displaced_{portName}");
            XElement decoy = document.Descendants("layer")
                .First(element => (string?)element.Attribute("type") == "Const")
                .Element("output")!
                .Element("port")!;
            decoy.SetAttributeValue("names", portName);
            decoy.SetAttributeValue("precision", precision);
            if (finalDimension is not null)
            {
                XElement? dimension = decoy.Elements("dim").LastOrDefault();
                if (dimension is null)
                {
                    decoy.Add(new XElement("dim", finalDimension));
                }
                else
                {
                    dimension.Value = finalDimension;
                }
            }

            System.IO.File.WriteAllText(path, document.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
        }

        public void SetFloatingConstantPrecision(string elementType)
        {
            string path = File("openvino_model.xml");
            XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            XElement[] floatingConstants = document.Descendants("layer")
                .Where(layer => (string?)layer.Attribute("type") == "Const")
                .Select(layer => layer.Element("data"))
                .Where(data => data is not null
                    && (string?)data.Attribute("element_type") == "f32")
                .Cast<XElement>()
                .ToArray();
            Assert.IsGreaterThan(0, floatingConstants.Length);
            foreach (XElement data in floatingConstants)
            {
                data.SetAttributeValue("element_type", elementType);
            }
            System.IO.File.WriteAllText(
                path,
                document.ToString(SaveOptions.DisableFormatting),
                new UTF8Encoding(false));
        }

        public void AddIntermediateDuplicateNamedPort(
            string relativeName,
            string portName,
            string precision,
            string finalDimension)
        {
            string path = File(relativeName);
            XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            XElement intermediate = document.Descendants("layer")
                .First(element =>
                    (string?)element.Attribute("type") != "Result" &&
                    element.Element("output")?.Element("port") is not null &&
                    !element.Descendants("port").Any(port =>
                        ((string?)port.Attribute("names"))?.Split(',').Contains(portName, StringComparer.Ordinal) == true))
                .Element("output")!
                .Element("port")!;
            intermediate.SetAttributeValue("names", portName);
            intermediate.SetAttributeValue("precision", precision);
            XElement? dimension = intermediate.Elements("dim").LastOrDefault();
            if (dimension is null)
            {
                intermediate.Add(new XElement("dim", finalDimension));
            }
            else
            {
                dimension.Value = finalDimension;
            }

            System.IO.File.WriteAllText(path, document.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
        }

        public void MutateResultDestinationEdge(string relativeName, string portName, string mutation)
        {
            string path = File(relativeName);
            XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            XElement producerPort = document.Descendants("port").Single(element =>
                ((string?)element.Attribute("names"))?.Split(',').Contains(portName, StringComparer.Ordinal) == true);
            XElement producerLayer = producerPort.Ancestors("layer").Single();
            XElement resultLayer = document.Descendants("layer").Single(element =>
                (string?)element.Attribute("type") == "Result" &&
                ((string?)element.Attribute("output_names"))?.Split(',').Contains(portName, StringComparer.Ordinal) == true);
            XElement resultInputPort = resultLayer.Element("input")!.Elements("port").Single();
            XElement edge = document.Descendants("edge").Single(element =>
                (string?)element.Attribute("from-layer") == (string?)producerLayer.Attribute("id") &&
                (string?)element.Attribute("from-port") == (string?)producerPort.Attribute("id") &&
                (string?)element.Attribute("to-layer") == (string?)resultLayer.Attribute("id") &&
                (string?)element.Attribute("to-port") == (string?)resultInputPort.Attribute("id"));
            switch (mutation)
            {
                case "missing":
                    edge.Attribute("to-port")!.Remove();
                    break;
                case "wrong":
                    edge.SetAttributeValue("to-port", "nonexistent");
                    break;
                case "extra-wrong":
                    XElement wrongDestination = new(edge);
                    wrongDestination.SetAttributeValue("to-port", "nonexistent");
                    edge.AddAfterSelf(wrongDestination);
                    break;
                case "duplicate":
                    edge.AddAfterSelf(new XElement(edge));
                    break;
                default:
                    Assert.Fail("Unknown Result-edge mutation.");
                    break;
            }

            System.IO.File.WriteAllText(path, document.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
        }

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The package helper owns every test mutation operation.")]
        public void WriteAlternateStream(string path, string streamName, string content) =>
            System.IO.File.WriteAllText(path + ":" + streamName, content, new UTF8Encoding(false));

        public void Dispose()
        {
            foreach ((string path, bool isDirectory) in reparsePoints)
            {
                if (isDirectory && Directory.Exists(path))
                {
                    Directory.Delete(path);
                }
                else if (!isDirectory && System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class MalformedCaseAttribute(string id, OpenVinoSupportCode supportCode) : Attribute
{
    public string Id { get; } = id;

    public OpenVinoSupportCode SupportCode { get; } = supportCode;
}
