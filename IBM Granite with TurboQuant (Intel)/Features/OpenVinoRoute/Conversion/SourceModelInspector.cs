using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Conversion;

public enum SourceModelInspectionStatus
{
    ConversionRequired,
    Rejected
}

public sealed record SourceModelEvidence(
    int PolicyVersion,
    string SourceManifestDigest,
    string ModelType,
    string Architecture,
    string Task,
    long ContextLength,
    IReadOnlyList<string> WeightFiles,
    int TensorCount);

public sealed class SourceModelInspectionResult : IDisposable
{
    private OpenVinoPackageSnapshot? snapshot;

    internal SourceModelInspectionResult(
        SourceModelInspectionStatus status,
        OpenVinoSupportCode? supportCode,
        SourceModelEvidence? evidence,
        OpenVinoPackageSnapshot? snapshot)
    {
        Status = status;
        SupportCode = supportCode;
        Evidence = evidence;
        this.snapshot = snapshot;
    }

    public SourceModelInspectionStatus Status { get; }
    public OpenVinoSupportCode? SupportCode { get; }
    public SourceModelEvidence? Evidence { get; }

    public bool VerifyStillCurrent() =>
        snapshot is not null &&
        snapshot.ValidateStillCurrent() == OpenVinoSnapshotFailure.None;

    public void Dispose() => Interlocked.Exchange(ref snapshot, null)?.Dispose();
}

public sealed class SourceModelInspector
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = OpenVinoPackagePolicy.MaximumJsonDepth
    };

    private readonly OpenVinoPackageSnapshotter snapshotter;

    public SourceModelInspector()
        : this(new OpenVinoPackageSnapshotter(policy: SourceModelPolicy.SnapshotPolicy))
    {
    }

    internal SourceModelInspector(OpenVinoPackageSnapshotter snapshotter) =>
        this.snapshotter = snapshotter ?? throw new ArgumentNullException(nameof(snapshotter));

    public SourceModelInspectionResult Inspect(string sourceRoot)
    {
        OpenVinoPackageSnapshotCapture capture = snapshotter.Capture(sourceRoot);
        if (capture.Snapshot is null)
        {
            return Rejected(MapSnapshotFailure(capture.Failure));
        }

        OpenVinoPackageSnapshot snapshot = capture.Snapshot;
        try
        {
            foreach (string required in SourceModelPolicy.RequiredJsonResources)
            {
                if (!snapshot.TryGetEntry(required, out _))
                {
                    snapshot.Dispose();
                    return Rejected(OpenVinoSupportCode.PackageMissingResource);
                }
            }

            using JsonDocument config = ReadJson(snapshot, "config.json");
            using JsonDocument generation = ReadJson(snapshot, "generation_config.json");
            using JsonDocument tokenizer = ReadJson(snapshot, "tokenizer_config.json");
            using JsonDocument tokenizerModel = ReadJson(snapshot, "tokenizer.json");
            using JsonDocument specialTokens = ReadJson(snapshot, "special_tokens_map.json");
            foreach (JsonElement documentRoot in new[]
            {
                config.RootElement,
                generation.RootElement,
                tokenizer.RootElement,
                tokenizerModel.RootElement,
                specialTokens.RootElement
            })
            {
                RejectRemoteCode(documentRoot);
            }

            string modelType = RequiredString(config.RootElement, "model_type");
            string architecture = RequiredSingleString(config.RootElement, "architectures");
            if (modelType != "granite" || architecture != "GraniteForCausalLM")
            {
                snapshot.Dispose();
                return Rejected(OpenVinoSupportCode.ModelArchitectureUnsupported);
            }

            string task = RequiredString(config.RootElement, "task");
            if (task != "text-generation-with-past")
            {
                snapshot.Dispose();
                return Rejected(OpenVinoSupportCode.ModelTaskUnsupported);
            }

            long context = RequiredPositiveInt64(config.RootElement, "max_position_embeddings");
            if (context > SourceModelPolicy.MaximumContextLength)
            {
                throw new InvalidDataException();
            }
            ValidateGeneration(config.RootElement, generation.RootElement);
            ValidateTokenizer(snapshot, context, tokenizer.RootElement, tokenizerModel.RootElement, specialTokens.RootElement);

            WeightEvidence weights = ValidateWeights(snapshot);
            if (snapshot.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
            {
                snapshot.Dispose();
                return Rejected(OpenVinoSupportCode.PackageChanged);
            }

            SourceModelEvidence evidence = new(
                SourceModelPolicy.PolicyVersion,
                ComputeManifestDigest(snapshot.Entries),
                modelType,
                architecture,
                task,
                context,
                weights.Files,
                weights.TensorCount);
            return new SourceModelInspectionResult(
                SourceModelInspectionStatus.ConversionRequired,
                null,
                evidence,
                snapshot);
        }
        catch (UnsafeSourceException)
        {
            snapshot.Dispose();
            return Rejected(OpenVinoSupportCode.ModelArchitectureUnsupported);
        }
        catch (MissingSourceResourceException)
        {
            snapshot.Dispose();
            return Rejected(OpenVinoSupportCode.PackageMissingResource);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or DecoderFallbackException or OverflowException)
        {
            snapshot.Dispose();
            return Rejected(OpenVinoSupportCode.PackageInconsistentResource);
        }
    }

    private static WeightEvidence ValidateWeights(OpenVinoPackageSnapshot snapshot)
    {
        bool hasSingle = snapshot.TryGetEntry("model.safetensors", out OpenVinoPackageSnapshotEntry? single);
        bool hasIndex = snapshot.TryGetEntry("model.safetensors.index.json", out _);
        string[] shards = snapshot.Entries
            .Select(static entry => entry.RelativeName)
            .Where(SourceModelPolicy.IsCanonicalShard)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (hasSingle)
        {
            if (hasIndex || shards.Length != 0) throw new InvalidDataException();
            HashSet<string> tensors = ReadSafeTensors(single!);
            return new WeightEvidence(["model.safetensors"], tensors.Count);
        }
        if (!hasIndex)
        {
            throw new MissingSourceResourceException();
        }

        using JsonDocument index = ReadJson(snapshot, "model.safetensors.index.json");
        JsonElement root = RequireObject(index.RootElement);
        RequireOnlyProperties(root, "metadata", "weight_map");
        JsonElement weightMap = RequiredProperty(root, "weight_map");
        if (weightMap.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        Dictionary<string, string> ownership = new(StringComparer.Ordinal);
        HashSet<string> referenced = new(StringComparer.Ordinal);
        foreach (JsonProperty tensor in weightMap.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(tensor.Name) ||
                tensor.Value.ValueKind != JsonValueKind.String ||
                !ownership.TryAdd(tensor.Name, tensor.Value.GetString()!))
            {
                throw new InvalidDataException();
            }
            string shard = ownership[tensor.Name];
            if (!SourceModelPolicy.IsCanonicalShard(shard)) throw new InvalidDataException();
            referenced.Add(shard);
        }
        if (ownership.Count == 0) throw new InvalidDataException();
        foreach (string referencedShard in referenced)
        {
            if (!snapshot.TryGetEntry(referencedShard, out _))
            {
                throw new MissingSourceResourceException();
            }
        }

        HashSet<int> shardIndices = [];
        int expectedShardCount = shards.Length;
        foreach (string shard in shards)
        {
            if (!SourceModelPolicy.TryParseCanonicalShard(
                    shard,
                    out int shardIndex,
                    out int shardCount) ||
                shardCount != expectedShardCount ||
                !shardIndices.Add(shardIndex))
            {
                throw new InvalidDataException();
            }
        }
        if (shardIndices.Count != expectedShardCount ||
            Enumerable.Range(1, expectedShardCount).Any(index => !shardIndices.Contains(index)))
        {
            throw new InvalidDataException();
        }
        if (!referenced.SetEquals(shards)) throw new InvalidDataException();

        HashSet<string> observed = new(StringComparer.Ordinal);
        foreach (string shard in shards)
        {
            if (!snapshot.TryGetEntry(shard, out OpenVinoPackageSnapshotEntry entry))
            {
                throw new MissingSourceResourceException();
            }
            foreach (string tensor in ReadSafeTensors(entry))
            {
                if (!observed.Add(tensor) ||
                    !ownership.TryGetValue(tensor, out string? owner) || owner != shard)
                {
                    throw new InvalidDataException();
                }
            }
        }
        if (!observed.SetEquals(ownership.Keys)) throw new InvalidDataException();
        return new WeightEvidence(shards, observed.Count);
    }

    private static HashSet<string> ReadSafeTensors(OpenVinoPackageSnapshotEntry entry)
    {
        FileStream stream = entry.Stream;
        if (entry.Length < 10) throw new InvalidDataException();
        Span<byte> lengthBytes = stackalloc byte[8];
        stream.Position = 0;
        if (stream.Read(lengthBytes) != lengthBytes.Length) throw new InvalidDataException();
        ulong encodedLength = BinaryPrimitives.ReadUInt64LittleEndian(lengthBytes);
        if (encodedLength is 0 or > SourceModelPolicy.MaximumSafeTensorsHeaderBytes ||
            encodedLength > checked((ulong)entry.Length - 9UL))
        {
            throw new InvalidDataException();
        }
        byte[] header = new byte[checked((int)encodedLength)];
        stream.ReadExactly(header);
        long dataLength = entry.Length - 8 - header.Length;
        using JsonDocument document = JsonDocument.Parse(StrictUtf8.GetString(header), JsonOptions);
        JsonElement root = RequireObject(document.RootElement);
        HashSet<string> tensors = new(StringComparer.Ordinal);
        List<(long Start, long End)> intervals = [];
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name == "__metadata__") continue;
            if (!tensors.Add(property.Name) || string.IsNullOrWhiteSpace(property.Name))
            {
                throw new InvalidDataException();
            }
            JsonElement descriptor = RequireObject(property.Value);
            RequireOnlyProperties(descriptor, "dtype", "shape", "data_offsets");
            string dtype = RequiredString(descriptor, "dtype");
            int bytesPerElement = GetDataTypeWidth(dtype);
            JsonElement shape = RequiredProperty(descriptor, "shape");
            JsonElement offsets = RequiredProperty(descriptor, "data_offsets");
            if (shape.ValueKind != JsonValueKind.Array || offsets.ValueKind != JsonValueKind.Array ||
                offsets.GetArrayLength() != 2 ||
                !offsets[0].TryGetInt64(out long start) ||
                !offsets[1].TryGetInt64(out long end) ||
                start < 0 || end <= start || end > dataLength)
            {
                throw new InvalidDataException();
            }
            long elementCount = 1;
            foreach (JsonElement dimension in shape.EnumerateArray())
            {
                if (!dimension.TryGetInt64(out long size) || size < 0)
                {
                    throw new InvalidDataException();
                }
                elementCount = checked(elementCount * size);
            }
            if (checked(elementCount * bytesPerElement) != end - start)
            {
                throw new InvalidDataException();
            }
            intervals.Add((start, end));
        }
        if (tensors.Count == 0) throw new InvalidDataException();
        intervals.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        if (intervals[0].Start != 0) throw new InvalidDataException();
        for (int index = 1; index < intervals.Count; index++)
        {
            if (intervals[index].Start != intervals[index - 1].End)
            {
                throw new InvalidDataException();
            }
        }
        if (intervals[^1].End != dataLength) throw new InvalidDataException();
        stream.Position = 0;
        return tensors;
    }

    private static int GetDataTypeWidth(string dtype) => dtype switch
    {
        "BOOL" or "I8" or "U8" or "F8_E4M3" or "F8_E5M2" => 1,
        "I16" or "U16" or "F16" or "BF16" => 2,
        "I32" or "U32" or "F32" => 4,
        "I64" or "U64" or "F64" or "C64" => 8,
        "C128" => 16,
        _ => throw new InvalidDataException()
    };

    private static void RejectRemoteCode(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException();
                if (property.Name is "auto_map" or "trust_remote_code" or "custom_pipelines" or
                    "code_revision" or "_name_or_path")
                {
                    throw new UnsafeSourceException();
                }
                RejectRemoteCode(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) RejectRemoteCode(item);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()!;
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("hf://", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnsafeSourceException();
            }
        }
    }

    private static void ValidateGeneration(JsonElement config, JsonElement generation)
    {
        _ = RequireObject(generation);
        foreach (string name in new[] { "bos_token_id", "eos_token_id", "pad_token_id" })
        {
            if (RequiredInt64(config, name) != RequiredInt64(generation, name))
            {
                throw new InvalidDataException();
            }
        }
    }

    private static void ValidateTokenizer(
        OpenVinoPackageSnapshot snapshot,
        long context,
        JsonElement tokenizer,
        JsonElement tokenizerModel,
        JsonElement specialTokens)
    {
        string tokenizerClass = RequiredString(tokenizer, "tokenizer_class");
        if (tokenizerClass is not ("PreTrainedTokenizerFast" or "GPT2TokenizerFast" or "TokenizersBackend") ||
            RequiredPositiveInt64(tokenizer, "model_max_length") != context)
        {
            throw new InvalidDataException();
        }
        bool hasEmbeddedChat = tokenizer.TryGetProperty("chat_template", out JsonElement chat) &&
            chat.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(chat.GetString());
        if (!hasEmbeddedChat && string.IsNullOrWhiteSpace(ReadText(snapshot, "chat_template.jinja")))
        {
            throw new InvalidDataException();
        }
        _ = RequireObject(tokenizerModel);
        _ = RequiredString(tokenizerModel, "version");
        _ = RequireObject(RequiredProperty(tokenizerModel, "model"));
        foreach (string name in new[] { "bos_token", "eos_token", "pad_token" })
        {
            if (RequiredString(tokenizer, name) != RequiredString(specialTokens, name))
            {
                throw new InvalidDataException();
            }
        }
    }

    private static JsonDocument ReadJson(OpenVinoPackageSnapshot snapshot, string name)
    {
        if (!snapshot.TryGetEntry(name, out OpenVinoPackageSnapshotEntry entry))
        {
            throw new MissingSourceResourceException();
        }
        entry.Stream.Position = 0;
        byte[] bytes = new byte[checked((int)entry.Length)];
        entry.Stream.ReadExactly(bytes);
        entry.Stream.Position = 0;
        JsonDocument document = JsonDocument.Parse(StrictUtf8.GetString(bytes), JsonOptions);
        ValidateUniqueProperties(document.RootElement);
        return document;
    }

    private static string ReadText(OpenVinoPackageSnapshot snapshot, string name)
    {
        if (!snapshot.TryGetEntry(name, out OpenVinoPackageSnapshotEntry entry))
        {
            throw new MissingSourceResourceException();
        }
        entry.Stream.Position = 0;
        byte[] bytes = new byte[checked((int)entry.Length)];
        entry.Stream.ReadExactly(bytes);
        entry.Stream.Position = 0;
        return StrictUtf8.GetString(bytes);
    }

    private static void ValidateUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException();
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) ValidateUniqueProperties(item);
        }
    }

    private static JsonElement RequireObject(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object ? value : throw new InvalidDataException();

    private static JsonElement RequiredProperty(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement property) ? property : throw new InvalidDataException();

    private static string RequiredString(JsonElement value, string name) =>
        RequiredString(RequiredProperty(value, name));

    private static string RequiredString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException();

    private static string RequiredSingleString(JsonElement value, string name)
    {
        JsonElement array = RequiredProperty(value, name);
        return array.ValueKind == JsonValueKind.Array && array.GetArrayLength() == 1
            ? RequiredString(array[0])
            : throw new InvalidDataException();
    }

    private static long RequiredInt64(JsonElement value, string name) =>
        RequiredProperty(value, name).TryGetInt64(out long result) ? result : throw new InvalidDataException();

    private static long RequiredPositiveInt64(JsonElement value, string name)
    {
        long result = RequiredInt64(value, name);
        return result > 0 ? result : throw new InvalidDataException();
    }

    private static void RequireOnlyProperties(JsonElement value, params string[] names)
    {
        HashSet<string> allowed = new(names, StringComparer.Ordinal);
        foreach (JsonProperty property in RequireObject(value).EnumerateObject())
        {
            if (!allowed.Remove(property.Name)) throw new InvalidDataException();
        }
        if (allowed.Count != 0) throw new InvalidDataException();
    }

    private static string ComputeManifestDigest(IEnumerable<OpenVinoPackageSnapshotEntry> entries)
    {
        StringBuilder manifest = new();
        foreach (OpenVinoPackageSnapshotEntry entry in entries.OrderBy(static item => item.RelativeName, StringComparer.Ordinal))
        {
            manifest.Append(entry.RelativeName).Append('\0')
                .Append(entry.Length).Append('\0').Append(entry.Sha256).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(manifest.ToString())))
            .ToLowerInvariant();
    }

    private static OpenVinoSupportCode MapSnapshotFailure(OpenVinoSnapshotFailure failure) => failure switch
    {
        OpenVinoSnapshotFailure.RootMissing => OpenVinoSupportCode.PackageMissingResource,
        OpenVinoSnapshotFailure.ExecutableOrScript or OpenVinoSnapshotFailure.UnrecognizedResource =>
            OpenVinoSupportCode.ModelArchitectureUnsupported,
        OpenVinoSnapshotFailure.ReparsePoint or OpenVinoSnapshotFailure.EscapedRoot or
            OpenVinoSnapshotFailure.CaseCollision or OpenVinoSnapshotFailure.AlternateDataStream =>
            OpenVinoSupportCode.PackageUnsafePath,
        OpenVinoSnapshotFailure.Changed => OpenVinoSupportCode.PackageChanged,
        OpenVinoSnapshotFailure.Unreadable => OpenVinoSupportCode.PackageUnreadable,
        _ => OpenVinoSupportCode.PackageInconsistentResource
    };

    private static SourceModelInspectionResult Rejected(OpenVinoSupportCode code) =>
        new(SourceModelInspectionStatus.Rejected, code, null, null);

    private sealed record WeightEvidence(IReadOnlyList<string> Files, int TensorCount);
    private sealed class UnsafeSourceException : Exception;
    private sealed class MissingSourceResourceException : Exception;
}
