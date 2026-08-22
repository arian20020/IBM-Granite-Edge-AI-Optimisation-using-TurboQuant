using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests.Conversion;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class SourceModelInspectorTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] SingleWeightFile = ["model.safetensors"];
    private static readonly string[] ShardedWeightFiles =
    [
        "model-00001-of-00002.safetensors",
        "model-00002-of-00002.safetensors"
    ];
    private static readonly string[] RequiredTokenizerResources =
    [
        "generation_config.json",
        "tokenizer.json",
        "tokenizer_config.json",
        "special_tokens_map.json"
    ];
    private static readonly int[] OneDimension = [1];
    private static readonly int[] OneByteOffsets = [0, 1];

    [TestMethod]
    public void CompleteSingleFileGraniteIsConversionRequiredAndRetainsReadLease()
    {
        using SourceFixture fixture = SourceFixture.CreateSingle();
        SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);
        using (result)
        {
            Assert.AreEqual(SourceModelInspectionStatus.ConversionRequired, result.Status);
            Assert.IsNull(result.SupportCode);
            Assert.IsNotNull(result.Evidence);
            Assert.AreEqual("granite", result.Evidence.ModelType);
            Assert.AreEqual("GraniteForCausalLM", result.Evidence.Architecture);
            Assert.AreEqual("text-generation-with-past", result.Evidence.Task);
            CollectionAssert.AreEqual(
                SingleWeightFile,
                result.Evidence.WeightFiles.ToArray());
            Assert.ThrowsExactly<IOException>(() => File.Open(
                Path.Combine(fixture.Root, "model.safetensors"),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None));
            Assert.IsTrue(result.VerifyStillCurrent());
        }

        using FileStream exclusive = File.Open(
            Path.Combine(fixture.Root, "model.safetensors"),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
    }

    [TestMethod]
    public void CompleteIndexedShardsRequireExactTensorOwnership()
    {
        using SourceFixture fixture = SourceFixture.CreateSharded();
        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        Assert.AreEqual(SourceModelInspectionStatus.ConversionRequired, result.Status);
        CollectionAssert.AreEqual(
            ShardedWeightFiles,
            result.Evidence!.WeightFiles.ToArray());
    }

    [TestMethod]
    public void MissingIndexedShardIsIncomplete()
    {
        using SourceFixture fixture = SourceFixture.CreateSharded();
        File.Delete(Path.Combine(fixture.Root, "model-00002-of-00002.safetensors"));

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        AssertRejected(result, OpenVinoSupportCode.PackageMissingResource);
    }

    [TestMethod]
    public void DuplicateOrMismatchedTensorOwnershipIsRejected()
    {
        using SourceFixture fixture = SourceFixture.CreateSharded();
        SourceFixture.WriteSafeTensors(
            Path.Combine(fixture.Root, "model-00002-of-00002.safetensors"),
            "layer.0");

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        AssertRejected(result, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void MissingTokenizerSpecialTokenChatOrGenerationResourceIsIncomplete()
    {
        foreach (string missing in RequiredTokenizerResources)
        {
            using SourceFixture fixture = SourceFixture.CreateSingle();
            File.Delete(Path.Combine(fixture.Root, missing));
            using SourceModelInspectionResult result =
                new SourceModelInspector().Inspect(fixture.Root);
            AssertRejected(result, OpenVinoSupportCode.PackageMissingResource, missing);
        }

        using SourceFixture missingChat = SourceFixture.CreateSingle(
            tokenizerMutation: root => root.Remove("chat_template"));
        using SourceModelInspectionResult missingChatResult =
            new SourceModelInspector().Inspect(missingChat.Root);
        AssertRejected(missingChatResult, OpenVinoSupportCode.PackageMissingResource);
    }

    [TestMethod]
    public void RemoteCodeAndExecutableLoaderArtifactsAreRejected()
    {
        using (SourceFixture fixture = SourceFixture.CreateSingle(configMutation: root =>
               root["auto_map"] = new { AutoModel = "modeling_granite.CustomModel" }))
        using (SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root))
        {
            AssertRejected(result, OpenVinoSupportCode.ModelArchitectureUnsupported);
        }

        using (SourceFixture fixture = SourceFixture.CreateSingle(configMutation: root =>
               root["_name_or_path"] = "ibm-granite/granite-3.3-2b-instruct"))
        using (SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root))
        {
            AssertRejected(result, OpenVinoSupportCode.ModelArchitectureUnsupported);
        }

        using (SourceFixture fixture = SourceFixture.CreateSingle())
        {
            File.WriteAllText(Path.Combine(fixture.Root, "modeling_granite.py"), "raise SystemExit");
            using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);
            AssertRejected(result, OpenVinoSupportCode.ModelArchitectureUnsupported);
        }
    }

    [TestMethod]
    public void PickleAndNativeWeightFallbacksAreRejected()
    {
        foreach (string hostile in new[] { "pytorch_model.bin", "model.pt", "weights.pth", "plugin.dll" })
        {
            using SourceFixture fixture = SourceFixture.CreateSingle();
            File.WriteAllBytes(Path.Combine(fixture.Root, hostile), [0x80, 0x04, 0x4e, 0x2e]);
            using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);
            AssertRejected(result, OpenVinoSupportCode.ModelArchitectureUnsupported, hostile);
        }
    }

    [TestMethod]
    public void OnlyDenseGraniteAndExactTaskAreAccepted()
    {
        (string ModelType, string Architecture, string Task, OpenVinoSupportCode Code)[] rejected =
        [
            ("granitemoe", "GraniteMoeForCausalLM", "text-generation-with-past", OpenVinoSupportCode.ModelArchitectureUnsupported),
            ("granitemoehybrid", "GraniteMoeHybridForCausalLM", "text-generation-with-past", OpenVinoSupportCode.ModelArchitectureUnsupported),
            ("granite", "GraniteForConditionalGeneration", "image-text-to-text", OpenVinoSupportCode.ModelArchitectureUnsupported),
            ("granite", "GraniteForCausalLM", "automatic-speech-recognition", OpenVinoSupportCode.ModelTaskUnsupported)
        ];
        foreach ((string modelType, string architecture, string task, OpenVinoSupportCode code) in rejected)
        {
            using SourceFixture fixture = SourceFixture.CreateSingle(root =>
            {
                root["model_type"] = modelType;
                root["architectures"] = new[] { architecture };
                root["task"] = task;
            });
            using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);
            AssertRejected(result, code, modelType);
        }
    }

    [TestMethod]
    public void AmbiguousOrNoncontiguousShardSetsAreRejected()
    {
        using (SourceFixture fixture = SourceFixture.CreateSingle())
        {
            File.WriteAllText(
                Path.Combine(fixture.Root, "model.safetensors.index.json"),
                "{\"metadata\":{},\"weight_map\":{\"weight\":\"model.safetensors\"}}");
            using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);
            AssertRejected(result, OpenVinoSupportCode.PackageInconsistentResource);
        }

        using (SourceFixture fixture = SourceFixture.CreateSharded(
                   secondShard: "model-00003-of-00003.safetensors"))
        using (SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root))
        {
            AssertRejected(result, OpenVinoSupportCode.PackageInconsistentResource);
        }
    }

    [TestMethod]
    public void DuplicateJsonPropertiesAreRejected()
    {
        using SourceFixture fixture = SourceFixture.CreateSingle();
        File.WriteAllText(
            Path.Combine(fixture.Root, "config.json"),
            "{\"model_type\":\"granite\",\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"],\"task\":\"text-generation-with-past\",\"max_position_embeddings\":4096,\"vocab_size\":32000,\"bos_token_id\":0,\"eos_token_id\":1,\"pad_token_id\":2}");

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        AssertRejected(result, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void OverlappingSafeTensorOffsetsAreRejected()
    {
        using SourceFixture fixture = SourceFixture.CreateSingle();
        SourceFixture.WriteRawSafeTensors(
            Path.Combine(fixture.Root, "model.safetensors"),
            "{\"a\":{\"dtype\":\"U8\",\"shape\":[1],\"data_offsets\":[0,1]},\"b\":{\"dtype\":\"U8\",\"shape\":[1],\"data_offsets\":[0,1]}}",
            dataLength: 1);

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        AssertRejected(result, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void SafeTensorShapeMustMatchDtypeAndByteRange()
    {
        using SourceFixture fixture = SourceFixture.CreateSingle();
        SourceFixture.WriteRawSafeTensors(
            Path.Combine(fixture.Root, "model.safetensors"),
            "{\"weight\":{\"dtype\":\"F32\",\"shape\":[2],\"data_offsets\":[0,1]}}",
            dataLength: 1);

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        AssertRejected(result, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void SafeTensorDataRangesMustCoverThePayloadWithoutGaps()
    {
        using SourceFixture fixture = SourceFixture.CreateSingle();
        SourceFixture.WriteRawSafeTensors(
            Path.Combine(fixture.Root, "model.safetensors"),
            "{\"weight\":{\"dtype\":\"U8\",\"shape\":[1],\"data_offsets\":[1,2]}}",
            dataLength: 2);

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        AssertRejected(result, OpenVinoSupportCode.PackageInconsistentResource);
    }

    [TestMethod]
    public void TransformerFiveStandaloneChatAndTokenizerBackendAreAccepted()
    {
        using SourceFixture fixture = SourceFixture.CreateSingle(tokenizerMutation: root =>
        {
            root["tokenizer_class"] = "TokenizersBackend";
            root.Remove("chat_template");
        });
        File.WriteAllText(
            Path.Combine(fixture.Root, "chat_template.jinja"),
            "{{ bos_token }}{{ messages[0].content }}");
        File.WriteAllText(
            Path.Combine(fixture.Root, "generation_config.json"),
            "{\"bos_token_id\":0,\"eos_token_id\":1,\"pad_token_id\":2," +
            "\"transformers_version\":\"5.5.4\"}");

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(fixture.Root);

        Assert.AreEqual(SourceModelInspectionStatus.ConversionRequired, result.Status);
    }

    [TestMethod]
    public void RepositoryTinyGraniteFixtureIsAccepted()
    {
        string fixture = Path.Combine(
            RepositoryRoot,
            "tests",
            "TestFixtures",
            "OpenVINO",
            "Converter",
            "TinyGraniteV1");
        string source = Path.Combine(fixture, "source");

        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(fixture, "manifest.json")));
        JsonElement entries = manifest.RootElement.GetProperty("files");
        Assert.AreEqual(7, entries.GetArrayLength());
        HashSet<string> listed = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string relative = entry.GetProperty("path").GetString()
                ?? throw new InvalidDataException("Fixture manifest path is missing.");
            Assert.IsTrue(listed.Add(relative), $"Duplicate fixture path: {relative}");
            string path = Path.GetFullPath(Path.Combine(
                fixture,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsTrue(path.StartsWith(fixture + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
            FileInfo file = new(path);
            Assert.IsTrue(file.Exists, relative);
            Assert.AreEqual(entry.GetProperty("length").GetInt64(), file.Length, relative);
            Assert.AreEqual(
                entry.GetProperty("sha256").GetString(),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                relative);
        }
        string[] actual = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(fixture, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            listed.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            actual);

        using SourceModelInspectionResult result = new SourceModelInspector().Inspect(source);

        Assert.AreEqual(SourceModelInspectionStatus.ConversionRequired, result.Status);
        Assert.AreEqual("granite", result.Evidence!.ModelType);
        Assert.AreEqual("GraniteForCausalLM", result.Evidence.Architecture);
    }

    private static void AssertRejected(
        SourceModelInspectionResult result,
        OpenVinoSupportCode expected,
        string? message = null)
    {
        Assert.AreEqual(SourceModelInspectionStatus.Rejected, result.Status, message);
        Assert.AreEqual(expected, result.SupportCode, message);
        Assert.IsNull(result.Evidence, message);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class SourceFixture : IDisposable
    {
        private SourceFixture(string root) => Root = root;
        public string Root { get; }

        public static SourceFixture CreateSingle(
            Action<Dictionary<string, object?>>? configMutation = null,
            Action<Dictionary<string, object?>>? tokenizerMutation = null)
        {
            SourceFixture fixture = CreateBase(configMutation, tokenizerMutation);
            WriteSafeTensors(Path.Combine(fixture.Root, "model.safetensors"), "model.embed_tokens.weight");
            return fixture;
        }

        public static SourceFixture CreateSharded(
            string secondShard = "model-00002-of-00002.safetensors")
        {
            SourceFixture fixture = CreateBase();
            const string first = "model-00001-of-00002.safetensors";
            string second = secondShard;
            WriteSafeTensors(Path.Combine(fixture.Root, first), "layer.0");
            WriteSafeTensors(Path.Combine(fixture.Root, second), "layer.1");
            WriteJson(Path.Combine(fixture.Root, "model.safetensors.index.json"), new
            {
                metadata = new { total_size = 2 },
                weight_map = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["layer.0"] = first,
                    ["layer.1"] = second
                }
            });
            return fixture;
        }

        private static SourceFixture CreateBase(
            Action<Dictionary<string, object?>>? configMutation = null,
            Action<Dictionary<string, object?>>? tokenizerMutation = null)
        {
            string root = Path.Combine(Path.GetTempPath(), "GraniteEdgeAI-Source-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SourceFixture fixture = new(root);
            Dictionary<string, object?> config = new(StringComparer.Ordinal)
            {
                ["model_type"] = "granite",
                ["architectures"] = new[] { "GraniteForCausalLM" },
                ["task"] = "text-generation-with-past",
                ["max_position_embeddings"] = 4096,
                ["vocab_size"] = 32000,
                ["bos_token_id"] = 0,
                ["eos_token_id"] = 1,
                ["pad_token_id"] = 2,
                ["torch_dtype"] = "float16"
            };
            configMutation?.Invoke(config);
            WriteJson(Path.Combine(root, "config.json"), config);
            WriteJson(Path.Combine(root, "generation_config.json"), new
            {
                bos_token_id = 0,
                eos_token_id = 1,
                pad_token_id = 2
            });
            Dictionary<string, object?> tokenizer = new(StringComparer.Ordinal)
            {
                ["tokenizer_class"] = "PreTrainedTokenizerFast",
                ["model_max_length"] = 4096,
                ["bos_token"] = "<s>",
                ["eos_token"] = "</s>",
                ["pad_token"] = "<pad>",
                ["chat_template"] = "{{ bos_token }}{{ messages[0]['content'] }}"
            };
            tokenizerMutation?.Invoke(tokenizer);
            WriteJson(Path.Combine(root, "tokenizer_config.json"), tokenizer);
            WriteJson(Path.Combine(root, "tokenizer.json"), new
            {
                version = "1.0",
                model = new { type = "BPE", vocab = new Dictionary<string, int> { ["hello"] = 0 } }
            });
            WriteJson(Path.Combine(root, "special_tokens_map.json"), new
            {
                bos_token = "<s>",
                eos_token = "</s>",
                pad_token = "<pad>"
            });
            return fixture;
        }

        public static void WriteSafeTensors(string path, string tensor)
        {
            string header = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [tensor] = new { dtype = "U8", shape = OneDimension, data_offsets = OneByteOffsets }
            });
            WriteRawSafeTensors(path, header, dataLength: 1);
        }

        public static void WriteRawSafeTensors(string path, string headerJson, int dataLength)
        {
            byte[] header = Encoding.UTF8.GetBytes(headerJson);
            byte[] bytes = new byte[8 + header.Length + dataLength];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, checked((ulong)header.Length));
            header.CopyTo(bytes.AsSpan(8));
            File.WriteAllBytes(path, bytes);
        }

        private static void WriteJson(string path, object value) =>
            File.WriteAllText(path, JsonSerializer.Serialize(value));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
