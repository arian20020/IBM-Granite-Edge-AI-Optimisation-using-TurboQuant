using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

public sealed class OpenVinoStaticPackageInspector
{
    private readonly OpenVinoPackageSnapshotter snapshotter;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = OpenVinoPackagePolicy.MaximumJsonDepth
    };

    private static readonly HashSet<string> ConfigProperties = new(StringComparer.Ordinal)
    {
        "architectures", "attention_bias", "attention_dropout", "auto_map", "bos_token_id", "eos_token_id",
        "attention_multiplier", "dtype", "embedding_multiplier", "hidden_act", "hidden_size",
        "initializer_range", "intermediate_size", "logits_scaling", "max_position_embeddings", "mlp_bias",
        "model_type", "num_attention_heads", "num_hidden_layers", "num_key_value_heads", "pad_token_id",
        "residual_multiplier", "rms_norm_eps", "rope_parameters", "rope_theta", "task", "tie_word_embeddings", "torch_dtype", "trust_remote_code",
        "transformers_version", "use_cache", "vocab_size"
    };

    private static readonly HashSet<string> GenerationProperties = new(StringComparer.Ordinal)
    {
        "bos_token_id", "do_sample", "eos_token_id", "max_new_tokens", "pad_token_id", "transformers_version"
    };

    private static readonly HashSet<string> TokenizerProperties = new(StringComparer.Ordinal)
    {
        "add_bos_token", "add_eos_token", "backend", "bos_token", "eos_token", "is_local",
        "model_max_length", "pad_token", "tokenizer_class", "unk_token"
    };

    private static readonly HashSet<string> ConfigStringProperties = new(StringComparer.Ordinal)
    {
        "dtype", "hidden_act", "model_type", "task", "torch_dtype", "transformers_version"
    };

    private static readonly HashSet<string> ConfigBooleanProperties = new(StringComparer.Ordinal)
    {
        "attention_bias", "mlp_bias", "tie_word_embeddings", "use_cache"
    };

    private static readonly HashSet<string> ConfigFloatingPointProperties = new(StringComparer.Ordinal)
    {
        "attention_dropout", "attention_multiplier", "embedding_multiplier", "initializer_range",
        "logits_scaling", "residual_multiplier", "rms_norm_eps", "rope_theta"
    };

    private static readonly HashSet<string> TokenizerJsonProperties = new(StringComparer.Ordinal)
    {
        "added_tokens", "decoder", "model", "normalizer", "padding", "post_processor",
        "pre_tokenizer", "truncation", "version"
    };

    private static readonly HashSet<string> TokenizerModelProperties = new(StringComparer.Ordinal)
    {
        "byte_fallback", "continuing_subword_prefix", "dropout", "end_of_word_suffix", "fuse_unk",
        "ignore_merges", "merges", "type", "unk_token", "vocab"
    };

    private static readonly HashSet<string> AddedTokenProperties = new(StringComparer.Ordinal)
    {
        "content", "id", "lstrip", "normalized", "rstrip", "single_word", "special"
    };

    private static readonly HashSet<string> SpecialTokenObjectProperties = new(StringComparer.Ordinal)
    {
        "content", "lstrip", "normalized", "rstrip", "single_word"
    };

    private static readonly HashSet<string> SpecialTokenMapProperties = new(StringComparer.Ordinal)
    {
        "additional_special_tokens", "bos_token", "cls_token", "eos_token", "mask_token",
        "pad_token", "sep_token", "unk_token"
    };

    public OpenVinoStaticPackageInspector()
        : this(new OpenVinoPackageSnapshotter())
    {
    }

    internal OpenVinoStaticPackageInspector(OpenVinoPackageSnapshotter snapshotter)
    {
        this.snapshotter = snapshotter ?? throw new ArgumentNullException(nameof(snapshotter));
    }

    public OpenVinoStaticPackageInspectionResult Inspect(string packageRoot) =>
        Inspect(packageRoot, CancellationToken.None, progress: null);

    public OpenVinoStaticPackageInspectionResult Inspect(
        string packageRoot,
        CancellationToken cancellationToken,
        IProgress<double>? progress)
    {
        OpenVinoPackageSnapshotCapture capture = snapshotter.Capture(
            packageRoot,
            cancellationToken,
            progress);
        if (capture.Snapshot is null)
        {
            return RejectSnapshotFailure(capture.Failure);
        }

        using OpenVinoPackageSnapshot snapshot = capture.Snapshot;
        foreach (string required in OpenVinoPackagePolicy.RequiredResources)
        {
            if (!snapshot.TryGetEntry(required, out OpenVinoPackageSnapshotEntry entry))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageMissingResource);
            }

            if (entry.Length <= 0)
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
            }
        }

        try
        {
            using JsonDocument config = ReadJson(snapshot, "config.json", ConfigProperties);
            using JsonDocument generation = ReadJson(snapshot, "generation_config.json", GenerationProperties);
            using JsonDocument tokenizer = ReadJson(snapshot, "tokenizer_config.json", TokenizerProperties);
            long vocabularySize = RequiredPositiveInt64(config.RootElement, "vocab_size");
            OptionalTokenizerFacts optionalTokenizerFacts = new();
            foreach (OpenVinoPackageSnapshotEntry optionalJson in snapshot.Entries.Where(static entry =>
                OpenVinoPackagePolicy.IsJsonResource(entry.RelativeName) &&
                !OpenVinoPackagePolicy.IsRequiredResource(entry.RelativeName)))
            {
                using JsonDocument document = ReadJson(
                    optionalJson,
                    allowedProperties: null,
                    allowNullValues: optionalJson.RelativeName == "tokenizer.json");
                ValidateOptionalJson(optionalJson.RelativeName, document.RootElement,
                    vocabularySize, optionalTokenizerFacts, snapshot);
            }

            foreach (OpenVinoPackageSnapshotEntry textResource in snapshot.Entries.Where(static entry =>
                entry.RelativeName is "chat_template.jinja" or "merges.txt"))
            {
                ValidateStrictText(textResource);
            }

            JsonElement configRoot = config.RootElement;
            JsonElement generationRoot = generation.RootElement;
            JsonElement tokenizerRoot = tokenizer.RootElement;
            if (configRoot.TryGetProperty("auto_map", out _) ||
                configRoot.TryGetProperty("trust_remote_code", out _))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.ModelArchitectureUnsupported);
            }

            ValidateConfigurationShape(configRoot, generationRoot, tokenizerRoot);

            string modelType = RequiredString(configRoot, "model_type");
            string architecture = RequiredSingleString(configRoot, "architectures");
            if (!string.Equals(modelType, "granite", StringComparison.Ordinal) ||
                !string.Equals(architecture, "GraniteForCausalLM", StringComparison.Ordinal))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.ModelArchitectureUnsupported);
            }

            string task = configRoot.TryGetProperty("task", out JsonElement taskElement)
                ? RequiredString(taskElement)
                : "text-generation-with-past";
            if (!string.Equals(task, "text-generation-with-past", StringComparison.Ordinal))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.ModelTaskUnsupported);
            }

            long contextLength = RequiredPositiveInt64(configRoot, "max_position_embeddings");
            string precision = GetConfiguredDataType(configRoot);
            if (contextLength > OpenVinoPackagePolicy.MaximumContextLength ||
                precision is not ("float32" or "float16" or "bfloat16"))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
            }

            string tokenizerClass = RequiredString(tokenizerRoot, "tokenizer_class");
            long tokenizerContext = RequiredPositiveInt64(tokenizerRoot, "model_max_length");
            if (tokenizerClass is not ("PreTrainedTokenizerFast" or "GPT2TokenizerFast" or "TokenizersBackend") ||
                tokenizerContext != contextLength)
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.TokenizerUnsupported);
            }

            if (!TokenIdentifiersAgree(configRoot, generationRoot) ||
                !TokenizerFactsAgree(configRoot, tokenizerRoot, optionalTokenizerFacts) ||
                (generationRoot.TryGetProperty("max_new_tokens", out JsonElement maximumTokens) &&
                 (!maximumTokens.TryGetInt64(out long requested) || requested <= 0 || requested > 512)))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.TokenizerUnsupported);
            }

            string irPrecision = precision switch
            {
                "float32" => "FP32",
                "float16" => "FP16",
                "bfloat16" => "BF16",
                _ => throw new InvalidDataException("Configured precision is unsupported.")
            };
            if (!ValidateXml(snapshot, "openvino_model.xml", "openvino_model.bin", ["Parameter", "Select", "Result"],
                    [new ExpectedPort("logits", irPrecision, vocabularySize)]) ||
                !ValidateXml(snapshot, "openvino_tokenizer.xml", "openvino_tokenizer.bin", ["Parameter", "StringTensorUnpack", "Result"],
                    [new ExpectedPort("input_ids", "I64", null), new ExpectedPort("attention_mask", "I64", null)]) ||
                !ValidateXml(snapshot, "openvino_detokenizer.xml", "openvino_detokenizer.bin", ["Parameter", "VocabDecoder", "Result"],
                    [new ExpectedPort("string_output", "STRING", null)]))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
            }

            OpenVinoPackageSnapshotEntry model = GetRequired(snapshot, "openvino_model.bin");
            bool hasChatTemplate = snapshot.TryGetEntry("chat_template.json", out _) ||
                snapshot.TryGetEntry("chat_template.jinja", out _);
            snapshot.Notify(OpenVinoPackageCaptureStage.BeforeInspectorFinalValidation);
            OpenVinoSnapshotFailure finalValidation = snapshot.ValidateStillCurrent();
            if (finalValidation != OpenVinoSnapshotFailure.None)
            {
                return RejectSnapshotFailure(finalValidation);
            }

            return OpenVinoStaticPackageInspectionResult.NativeValidationRequired(new OpenVinoStaticPackageEvidence(
                OpenVinoPackagePolicy.PolicyVersion,
                ComputeManifestDigest(snapshot.Entries),
                model.Sha256,
                model.Length,
                modelType,
                architecture,
                task,
                contextLength,
                precision,
                tokenizerClass,
                snapshot.Entries.Count,
                hasChatTemplate));
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or XmlException or FormatException or OverflowException or InvalidDataException)
        {
            return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
        }
    }

    internal static OpenVinoStaticPackageInspectionResult RejectSnapshotFailure(OpenVinoSnapshotFailure failure) =>
        OpenVinoStaticPackageInspectionResult.Rejected(failure switch
        {
            OpenVinoSnapshotFailure.RootMissing => OpenVinoSupportCode.PackageMissingResource,
            OpenVinoSnapshotFailure.Changed => OpenVinoSupportCode.PackageChanged,
            OpenVinoSnapshotFailure.Unreadable => OpenVinoSupportCode.PackageUnreadable,
            OpenVinoSnapshotFailure.ReparsePoint or
            OpenVinoSnapshotFailure.EscapedRoot or
            OpenVinoSnapshotFailure.CaseCollision or
            OpenVinoSnapshotFailure.AlternateDataStream or
            OpenVinoSnapshotFailure.NonRegularArtifact or
            OpenVinoSnapshotFailure.ExecutableOrScript or
            OpenVinoSnapshotFailure.EntryLimitExceeded or
            OpenVinoSnapshotFailure.DepthLimitExceeded => OpenVinoSupportCode.PackageUnsafePath,
            OpenVinoSnapshotFailure.UnrecognizedResource or
            OpenVinoSnapshotFailure.JsonTooLarge or
            OpenVinoSnapshotFailure.XmlTooLarge or
            OpenVinoSnapshotFailure.TextTooLarge => OpenVinoSupportCode.PackageInconsistentResource,
            _ => OpenVinoSupportCode.PackageInconsistentResource
        });

    private static JsonDocument ReadJson(
        OpenVinoPackageSnapshot snapshot,
        string name,
        HashSet<string>? allowedProperties)
    {
        return ReadJson(GetRequired(snapshot, name), allowedProperties);
    }

    private static JsonDocument ReadJson(
        OpenVinoPackageSnapshotEntry entry,
        HashSet<string>? allowedProperties,
        bool allowNullValues = false)
    {
        if (entry.Length <= 0 || entry.Length > OpenVinoPackagePolicy.MaximumJsonBytes || entry.Length > int.MaxValue)
        {
            throw new InvalidDataException("JSON resource length is invalid.");
        }

        byte[] bytes = new byte[checked((int)entry.Length)];
        entry.Stream.Position = 0;
        entry.Stream.ReadExactly(bytes);
        entry.Stream.Position = 0;
        string json = StrictUtf8.GetString(bytes);
        JsonDocument document = JsonDocument.Parse(json, JsonOptions);
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("JSON resource root must be an object.");
            }

            RejectDuplicateProperties(document.RootElement);
            if (!allowNullValues) RejectNullValues(document.RootElement);
            if (allowedProperties is not null)
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (!allowedProperties.Contains(property.Name))
                    {
                        throw new InvalidDataException("JSON resource contains an unrecognized property.");
                    }
                }
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("JSON resource contains a duplicate property.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }

    private static void RejectNullValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidDataException("JSON resource contains a null value.");
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                RejectNullValues(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectNullValues(item);
            }
        }
    }

    private static void ValidateOptionalJson(
        string resourceName,
        JsonElement root,
        long vocabularySize,
        OptionalTokenizerFacts facts,
        OpenVinoPackageSnapshot snapshot)
    {
        switch (resourceName)
        {
            case "vocab.json":
                facts.Vocabulary = ValidateIdentifierMap(root, vocabularySize, requireFullVocabulary: true);
                break;
            case "added_tokens.json":
                facts.AddedTokens = ValidateIdentifierMap(root, vocabularySize, requireFullVocabulary: false);
                break;
            case "special_tokens_map.json":
                facts.SpecialTokens = ValidateSpecialTokensMap(root);
                break;
            case "chat_template.json":
                ValidateClosedObject(root, new HashSet<string>(["chat_template"], StringComparer.Ordinal));
                _ = RequiredString(root, "chat_template");
                break;
            case "tokenizer.json":
                (facts.TokenizerVocabulary, facts.TokenizerAddedTokens, facts.TokenizerUnknownToken) =
                    ValidateTokenizerJson(root, vocabularySize);
                break;
            case OpenVinoProvenance.FileName:
                OpenVinoProvenance.ValidateJson(root, snapshot);
                break;
            case OpenVinoOptimizationProvenance.FileName:
                OpenVinoOptimizationProvenance.ValidateJson(root, snapshot);
                break;
            default:
                throw new InvalidDataException("Optional JSON resource has no version-one schema.");
        }
    }

    private static (
        Dictionary<string, long> Vocabulary,
        Dictionary<string, long> AddedTokens,
        string? UnknownToken) ValidateTokenizerJson(
        JsonElement root,
        long vocabularySize)
    {
        ValidateClosedObject(root, TokenizerJsonProperties);
        if (!string.Equals(RequiredString(root, "version"), "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Tokenizer JSON version is unsupported.");
        }

        foreach (string component in new[]
        {
            "truncation", "padding", "normalizer", "pre_tokenizer", "post_processor", "decoder"
        })
        {
            if (root.TryGetProperty(component, out JsonElement componentValue) &&
                componentValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object))
            {
                throw new InvalidDataException("Tokenizer component is invalid.");
            }
        }

        if (!root.TryGetProperty("added_tokens", out JsonElement addedTokens) || addedTokens.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Tokenizer added tokens must be an array.");
        }

        HashSet<long> addedTokenIds = [];
        Dictionary<string, long> addedTokenFacts = new(StringComparer.Ordinal);
        foreach (JsonElement token in addedTokens.EnumerateArray())
        {
            ValidateClosedObject(token, AddedTokenProperties);
            long id = RequiredBoundedIdentifier(token, "id", vocabularySize);
            if (!addedTokenIds.Add(id))
            {
                throw new InvalidDataException("Tokenizer added-token identifiers must be unique.");
            }

            string content = RequiredString(token, "content");
            if (!addedTokenFacts.TryAdd(content, id))
            {
                throw new InvalidDataException("Tokenizer added-token content must be unique.");
            }
            foreach (string property in new[] { "single_word", "lstrip", "rstrip", "normalized", "special" })
            {
                RequireBoolean(token, property);
            }
        }

        if (!root.TryGetProperty("model", out JsonElement model))
        {
            throw new InvalidDataException("Tokenizer model is missing.");
        }

        ValidateClosedObject(model, TokenizerModelProperties);
        string tokenizerModelType = RequiredString(model, "type");
        if (tokenizerModelType is not ("BPE" or "WordLevel"))
        {
            throw new InvalidDataException("Tokenizer model type is unsupported by policy version one.");
        }

        if (!model.TryGetProperty("vocab", out JsonElement vocabulary))
        {
            throw new InvalidDataException("Tokenizer vocabulary is missing.");
        }

        Dictionary<string, long> vocabularyFacts = ValidateIdentifierMap(vocabulary, vocabularySize, requireFullVocabulary: true);
        if (tokenizerModelType == "BPE" &&
            (!model.TryGetProperty("merges", out JsonElement merges) ||
             merges.ValueKind != JsonValueKind.Array))
        {
            throw new InvalidDataException("Tokenizer merges must be an array.");
        }

        if (tokenizerModelType == "BPE")
        {
            foreach (JsonElement merge in model.GetProperty("merges").EnumerateArray())
            {
                if (merge.ValueKind == JsonValueKind.String)
                {
                    _ = RequiredString(merge);
                    continue;
                }

                if (merge.ValueKind != JsonValueKind.Array || merge.GetArrayLength() != 2 ||
                    merge.EnumerateArray().Any(static item => item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
                {
                    throw new InvalidDataException("Tokenizer merge must be a string or two-item string array.");
                }
            }
        }

        string? unknownToken = model.TryGetProperty("unk_token", out _)
            ? RequiredString(model, "unk_token")
            : null;
        ValidateOptionalString(model, "continuing_subword_prefix");
        ValidateOptionalString(model, "end_of_word_suffix");
        ValidateOptionalBoolean(model, "fuse_unk");
        ValidateOptionalBoolean(model, "byte_fallback");
        ValidateOptionalBoolean(model, "ignore_merges");
        if (model.TryGetProperty("dropout", out JsonElement dropout) &&
            (dropout.ValueKind != JsonValueKind.Number || !dropout.TryGetDouble(out double value) || !double.IsFinite(value) || value < 0 || value > 1))
        {
            throw new InvalidDataException("Tokenizer dropout must be a finite probability.");
        }

        return (vocabularyFacts, addedTokenFacts, unknownToken);
    }

    private static Dictionary<string, long> ValidateIdentifierMap(
        JsonElement root,
        long vocabularySize,
        bool requireFullVocabulary)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Token identifier map must be an object.");
        }

        HashSet<long> identifiers = [];
        Dictionary<string, long> facts = new(StringComparer.Ordinal);
        int count = 0;
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.IsNullOrEmpty(property.Name) || property.Value.ValueKind != JsonValueKind.Number ||
                !property.Value.TryGetInt64(out long identifier) || identifier < 0 || identifier >= vocabularySize ||
                !identifiers.Add(identifier))
            {
                throw new InvalidDataException("Token identifier map is invalid.");
            }

            count++;
            facts.Add(property.Name, identifier);
        }

        if (requireFullVocabulary && count != vocabularySize)
        {
            throw new InvalidDataException("Token identifier map must agree with configured vocabulary size.");
        }

        return facts;
    }

    private static List<SpecialTokenFact> ValidateSpecialTokensMap(JsonElement root)
    {
        ValidateClosedObject(root, SpecialTokenMapProperties);
        List<SpecialTokenFact> facts = [];
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name == "additional_special_tokens")
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("Additional special tokens must be an array.");
                }

                foreach (JsonElement token in property.Value.EnumerateArray())
                {
                    facts.Add(new SpecialTokenFact(property.Name, ValidateSpecialToken(token)));
                }
            }
            else
            {
                facts.Add(new SpecialTokenFact(property.Name, ValidateSpecialToken(property.Value)));
            }
        }

        return facts;
    }

    private static string ValidateSpecialToken(JsonElement token)
    {
        if (token.ValueKind == JsonValueKind.String)
        {
            return RequiredString(token);
        }

        ValidateClosedObject(token, SpecialTokenObjectProperties);
        _ = RequiredString(token, "content");
        foreach (string property in new[] { "single_word", "lstrip", "rstrip", "normalized" })
        {
            RequireBoolean(token, property);
        }

        return RequiredString(token, "content");
    }

    private static void ValidateClosedObject(JsonElement value, HashSet<string> allowedProperties)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("JSON schema requires an object.");
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                throw new InvalidDataException("JSON schema contains an unrecognized property.");
            }
        }
    }

    private static long RequiredBoundedIdentifier(JsonElement parent, string propertyName, long upperExclusive)
    {
        long value = RequireNonNegativeToken(parent, propertyName);
        if (value >= upperExclusive)
        {
            throw new InvalidDataException("Token identifier is outside configured vocabulary.");
        }

        return value;
    }

    private static void ValidateOptionalString(JsonElement parent, string propertyName)
    {
        if (parent.TryGetProperty(propertyName, out JsonElement value))
        {
            _ = RequiredString(value);
        }
    }

    private static void ValidateOptionalBoolean(JsonElement parent, string propertyName)
    {
        if (parent.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("JSON property must be a boolean.");
        }
    }

    private static void ValidateStrictText(OpenVinoPackageSnapshotEntry entry)
    {
        if (entry.Length <= 0 || entry.Length > OpenVinoPackagePolicy.MaximumTextBytes)
        {
            throw new InvalidDataException("Text resource length is invalid.");
        }

        entry.Stream.Position = 0;
        using StreamReader reader = new(entry.Stream, StrictUtf8, detectEncodingFromByteOrderMarks: false, 64 * 1024, leaveOpen: true);
        char[] buffer = new char[64 * 1024];
        while (reader.Read(buffer, 0, buffer.Length) > 0)
        {
        }

        entry.Stream.Position = 0;
    }

    private static void ValidateConfigurationShape(JsonElement config, JsonElement generation, JsonElement tokenizer)
    {
        ValidateConfigPropertyTypes(config);
        _ = RequiredString(config, "model_type");
        _ = RequiredSingleString(config, "architectures");
        _ = RequiredPositiveBoundedInt64(config, "vocab_size", 100_000_000);
        _ = RequiredPositiveBoundedInt64(config, "max_position_embeddings", OpenVinoPackagePolicy.MaximumContextLength);
        long hiddenSize = RequiredPositiveBoundedInt64(config, "hidden_size", 1_048_576);
        _ = RequiredPositiveBoundedInt64(config, "intermediate_size", 16_777_216);
        long attentionHeads = RequiredPositiveBoundedInt64(config, "num_attention_heads", 65_536);
        long keyValueHeads = RequiredPositiveBoundedInt64(config, "num_key_value_heads", 65_536);
        _ = RequiredPositiveBoundedInt64(config, "num_hidden_layers", 65_536);
        if (keyValueHeads > attentionHeads || attentionHeads % keyValueHeads != 0 || hiddenSize % attentionHeads != 0)
        {
            throw new InvalidDataException("Granite attention dimensions are inconsistent.");
        }

        _ = GetConfiguredDataType(config);
        RequireNonNegativeToken(config, "bos_token_id");
        RequireNonNegativeToken(config, "eos_token_id");
        RequireNonNegativeToken(config, "pad_token_id");

        if (generation.TryGetProperty("max_new_tokens", out _))
        {
            _ = RequiredPositiveInt64(generation, "max_new_tokens");
        }
        if (generation.TryGetProperty("do_sample", out _)) RequireBoolean(generation, "do_sample");
        RequireNonNegativeToken(generation, "bos_token_id");
        RequireNonNegativeToken(generation, "eos_token_id");
        RequireNonNegativeToken(generation, "pad_token_id");

        _ = RequiredPositiveInt64(tokenizer, "model_max_length");
        _ = RequiredString(tokenizer, "tokenizer_class");
        _ = RequiredString(tokenizer, "bos_token");
        _ = RequiredString(tokenizer, "eos_token");
        _ = RequiredString(tokenizer, "pad_token");
        if (tokenizer.TryGetProperty("add_bos_token", out _)) RequireBoolean(tokenizer, "add_bos_token");
        if (tokenizer.TryGetProperty("add_eos_token", out _)) RequireBoolean(tokenizer, "add_eos_token");
        if (tokenizer.TryGetProperty("backend", out _) &&
            RequiredString(tokenizer, "backend") != "tokenizers")
        {
            throw new InvalidDataException("Tokenizer backend is unsupported.");
        }
        if (tokenizer.TryGetProperty("is_local", out _)) RequireBoolean(tokenizer, "is_local");
        ValidateOptionalString(tokenizer, "unk_token");
    }

    private static void ValidateConfigPropertyTypes(JsonElement config)
    {
        foreach (JsonProperty property in config.EnumerateObject())
        {
            JsonElement value = property.Value;
            if (string.Equals(property.Name, "architectures", StringComparison.Ordinal))
            {
                _ = RequiredSingleString(config, property.Name);
            }
            else if (string.Equals(property.Name, "rope_parameters", StringComparison.Ordinal))
            {
                ValidateClosedObject(value,
                    new HashSet<string>(["rope_theta", "rope_type"], StringComparer.Ordinal));
                if (!value.GetProperty("rope_theta").TryGetDouble(out double theta) ||
                    !double.IsFinite(theta) || theta <= 0 ||
                    RequiredString(value, "rope_type") != "default")
                {
                    throw new InvalidDataException("RoPE parameters are invalid.");
                }
            }
            else if (ConfigStringProperties.Contains(property.Name))
            {
                _ = RequiredString(value);
            }
            else if (ConfigBooleanProperties.Contains(property.Name))
            {
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new InvalidDataException("JSON configuration boolean has an invalid type.");
                }
            }
            else if (ConfigFloatingPointProperties.Contains(property.Name))
            {
                if (value.ValueKind != JsonValueKind.Number ||
                    !value.TryGetDouble(out double number) ||
                    !double.IsFinite(number))
                {
                    throw new InvalidDataException("JSON configuration number must be finite.");
                }
            }
            else if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
            {
                throw new InvalidDataException("JSON configuration integer has an invalid type.");
            }
        }
    }

    private static string GetConfiguredDataType(JsonElement config)
    {
        bool hasTorch = config.TryGetProperty("torch_dtype", out JsonElement torch);
        bool hasDtype = config.TryGetProperty("dtype", out JsonElement dtype);
        if (!hasTorch && !hasDtype)
        {
            throw new InvalidDataException("Configured data type is missing.");
        }
        string value = RequiredString(hasTorch ? torch : dtype);
        if (hasTorch && hasDtype && RequiredString(dtype) != value)
        {
            throw new InvalidDataException("Configured data types disagree.");
        }
        return value;
    }

    private static bool TokenIdentifiersAgree(JsonElement config, JsonElement generation)
    {
        long vocabularySize = RequiredPositiveInt64(config, "vocab_size");
        foreach (string property in new[] { "bos_token_id", "eos_token_id", "pad_token_id" })
        {
            long configValue = RequireNonNegativeToken(config, property);
            long generationValue = RequireNonNegativeToken(generation, property);
            if (configValue != generationValue || configValue >= vocabularySize)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TokenizerFactsAgree(
        JsonElement config,
        JsonElement tokenizer,
        OptionalTokenizerFacts optional)
    {
        (string Token, long Identifier)[] requiredFacts =
        [
            (RequiredString(tokenizer, "bos_token"), RequireNonNegativeToken(config, "bos_token_id")),
            (RequiredString(tokenizer, "eos_token"), RequireNonNegativeToken(config, "eos_token_id")),
            (RequiredString(tokenizer, "pad_token"), RequireNonNegativeToken(config, "pad_token_id"))
        ];
        bool tokenMapsToMultipleIdentifiers = requiredFacts
            .GroupBy(static fact => fact.Token, StringComparer.Ordinal)
            .Any(static group => group
                .Select(static fact => fact.Identifier)
                .Distinct()
                .Skip(1)
                .Any());
        bool identifierMapsToMultipleTokens = requiredFacts
            .GroupBy(static fact => fact.Identifier)
            .Any(static group => group
                .Select(static fact => fact.Token)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any());
        if (tokenMapsToMultipleIdentifiers || identifierMapsToMultipleTokens)
        {
            return false;
        }

        Dictionary<string, long> requiredTokens = requiredFacts
            .GroupBy(static fact => fact.Token, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.First().Identifier,
                StringComparer.Ordinal);

        Dictionary<string, long>?[] vocabularyCandidates = [optional.Vocabulary, optional.TokenizerVocabulary];
        Dictionary<string, long>[] vocabularies = vocabularyCandidates
                .Where(static vocabulary => vocabulary is not null)
                .Cast<Dictionary<string, long>>()
                .ToArray();
        if (vocabularies.Length == 2 && !IdentifierMapsEqual(vocabularies[0], vocabularies[1]))
        {
            return false;
        }

        foreach (Dictionary<string, long> vocabulary in vocabularies)
        {
            foreach ((string token, long identifier) in requiredTokens)
            {
                if (!vocabulary.TryGetValue(token, out long observed) || observed != identifier)
                {
                    return false;
                }
            }
        }

        Dictionary<string, long>?[] addedTokenCandidates = [optional.AddedTokens, optional.TokenizerAddedTokens];
        Dictionary<string, long>[] addedTokenSets = addedTokenCandidates
                .Where(static tokens => tokens is not null)
                .Cast<Dictionary<string, long>>()
                .ToArray();
        foreach (Dictionary<string, long> addedTokens in addedTokenSets)
        {
            foreach ((string token, long identifier) in addedTokens)
            {
                if (requiredTokens.TryGetValue(token, out long requiredIdentifier) && requiredIdentifier != identifier)
                {
                    return false;
                }

                foreach (Dictionary<string, long> vocabulary in vocabularies)
                {
                    if (!vocabulary.TryGetValue(token, out long vocabularyIdentifier) || vocabularyIdentifier != identifier)
                    {
                        return false;
                    }
                }
            }
        }

        if (addedTokenSets.Length == 2)
        {
            foreach ((string token, long identifier) in addedTokenSets[0])
            {
                if (addedTokenSets[1].TryGetValue(token, out long otherIdentifier) && otherIdentifier != identifier)
                {
                    return false;
                }
            }
        }

        if (optional.TokenizerVocabulary is not null)
        {
            if (optional.TokenizerUnknownToken is null)
            {
                return false;
            }

            HashSet<long> unknownTokenIdentifiers = [];
            foreach (Dictionary<string, long> tokenMap in vocabularies.Concat(addedTokenSets))
            {
                if (tokenMap.TryGetValue(optional.TokenizerUnknownToken, out long identifier))
                {
                    unknownTokenIdentifiers.Add(identifier);
                }
            }

            if (unknownTokenIdentifiers.Count != 1)
            {
                return false;
            }
        }

        if (optional.SpecialTokens is not null)
        {
            HashSet<string> uniqueSpecialTokens = new(StringComparer.Ordinal);
            foreach (SpecialTokenFact specialToken in optional.SpecialTokens)
            {
                if (!uniqueSpecialTokens.Add(specialToken.Content))
                {
                    return false;
                }

                string? requiredProperty = specialToken.Role switch
                {
                    "bos_token" => "bos_token",
                    "eos_token" => "eos_token",
                    "pad_token" => "pad_token",
                    _ => null
                };
                if (requiredProperty is not null &&
                    !string.Equals(specialToken.Content, RequiredString(tokenizer, requiredProperty), StringComparison.Ordinal))
                {
                    return false;
                }

                if (specialToken.Role == "unk_token" &&
                    optional.TokenizerUnknownToken is not null &&
                    !string.Equals(specialToken.Content, optional.TokenizerUnknownToken, StringComparison.Ordinal))
                {
                    return false;
                }

                if (vocabularies.Length == 0)
                {
                    if (!requiredTokens.ContainsKey(specialToken.Content))
                    {
                        return false;
                    }
                }
                else
                {
                    long? identifier = null;
                    foreach (Dictionary<string, long> vocabulary in vocabularies)
                    {
                        if (!vocabulary.TryGetValue(specialToken.Content, out long observed) ||
                            (identifier is not null && identifier != observed))
                        {
                            return false;
                        }

                        identifier = observed;
                    }

                    if (requiredTokens.TryGetValue(specialToken.Content, out long requiredIdentifier) && identifier != requiredIdentifier)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool IdentifierMapsEqual(
        Dictionary<string, long> first,
        Dictionary<string, long> second) =>
        first.Count == second.Count &&
        first.All(pair => second.TryGetValue(pair.Key, out long identifier) && identifier == pair.Value);

    private static string RequiredSingleString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("JSON property must be a one-item string array.");
        }

        JsonElement.ArrayEnumerator values = value.EnumerateArray();
        if (!values.MoveNext())
        {
            throw new InvalidDataException("JSON property must be a one-item string array.");
        }

        string result = RequiredString(values.Current);
        if (values.MoveNext())
        {
            throw new InvalidDataException("JSON property must be a one-item string array.");
        }

        return result;
    }

    private static string RequiredString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new InvalidDataException("JSON string property is missing.");
        }

        return RequiredString(value);
    }

    private static string RequiredString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException("JSON property must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static long RequiredPositiveInt64(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long result) ||
            result <= 0)
        {
            throw new InvalidDataException("JSON property must be a positive Int64.");
        }

        return result;
    }

    private static long RequiredPositiveBoundedInt64(JsonElement parent, string propertyName, long maximum)
    {
        long result = RequiredPositiveInt64(parent, propertyName);
        if (result > maximum)
        {
            throw new InvalidDataException("JSON property exceeds the policy bound.");
        }

        return result;
    }

    private static long RequireNonNegativeToken(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long result) ||
            result < 0)
        {
            throw new InvalidDataException("JSON token identifier must be a non-negative Int64.");
        }

        return result;
    }

    private static void RequireBoolean(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("JSON property must be a boolean.");
        }
    }

    private static bool ValidateXml(
        OpenVinoPackageSnapshot snapshot,
        string xmlName,
        string binName,
        IReadOnlyCollection<string> requiredLayerTypes,
        IReadOnlyCollection<ExpectedPort> requiredPorts)
    {
        OpenVinoPackageSnapshotEntry xml = GetRequired(snapshot, xmlName);
        OpenVinoPackageSnapshotEntry binary = GetRequired(snapshot, binName);
        if (xml.Length <= 0 || xml.Length > OpenVinoPackagePolicy.MaximumXmlBytes || binary.Length <= 0)
        {
            return false;
        }

        XmlReaderSettings settings = new()
        {
            CloseInput = false,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = OpenVinoPackagePolicy.MaximumXmlBytes,
            ValidationType = ValidationType.None,
            XmlResolver = null
        };
        HashSet<string> observedTypes = new(StringComparer.Ordinal);
        List<ObservedPort> observedPorts = [];
        List<GraphEdge> graphEdges = [];
        Dictionary<string, ResultLayer> resultLayers = new(StringComparer.Ordinal);
        bool validRoot = false;
        bool insideConstant = false;
        bool insideLayerInput = false;
        bool insideLayerOutput = false;
        string? currentLayerId = null;
        string? currentLayerType = null;
        string[]? currentPortNames = null;
        string? currentPortId = null;
        string? currentPortPrecision = null;
        long? currentPortLastDimension = null;
        bool currentPortIsOutput = false;
        bool insideNamedPortDimension = false;
        xml.Stream.Position = 0;
        using StreamReader text = new(xml.Stream, StrictUtf8, detectEncodingFromByteOrderMarks: false, 64 * 1024, leaveOpen: true);
        using XmlReader reader = XmlReader.Create(text, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.DocumentType)
            {
                return false;
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Depth == 0)
                {
                    validRoot = string.Equals(reader.LocalName, "net", StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(reader.GetAttribute("name"));
                }

                if (reader.HasAttributes)
                {
                    while (reader.MoveToNextAttribute())
                    {
                        if (reader.LocalName is "schemaLocation" or "noNamespaceSchemaLocation" ||
                            reader.Value.Contains("://", StringComparison.OrdinalIgnoreCase) ||
                            reader.Value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    reader.MoveToElement();
                }

                if (string.Equals(reader.LocalName, "layer", StringComparison.Ordinal))
                {
                    string? layerType = reader.GetAttribute("type");
                    currentLayerId = reader.GetAttribute("id");
                    currentLayerType = layerType;
                    insideConstant = string.Equals(layerType, "Const", StringComparison.Ordinal);
                    if (!string.IsNullOrEmpty(layerType))
                    {
                        observedTypes.Add(layerType);
                    }

                    if (string.Equals(layerType, "Result", StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(currentLayerId))
                    {
                        if (!resultLayers.TryAdd(
                            currentLayerId,
                            new ResultLayer(currentLayerId, SplitNames(reader.GetAttribute("output_names")), [])))
                        {
                            return false;
                        }
                    }
                }
                else if (insideConstant && string.Equals(reader.LocalName, "data", StringComparison.Ordinal))
                {
                    if (!long.TryParse(reader.GetAttribute("offset"), NumberStyles.None, CultureInfo.InvariantCulture, out long offset) ||
                        !long.TryParse(reader.GetAttribute("size"), NumberStyles.None, CultureInfo.InvariantCulture, out long size) ||
                        offset < 0 || size < 0 || offset > binary.Length || size > binary.Length - offset)
                    {
                        return false;
                    }
                }

                if (string.Equals(reader.LocalName, "input", StringComparison.Ordinal) && currentLayerId is not null)
                {
                    insideLayerInput = true;
                }

                if (string.Equals(reader.LocalName, "output", StringComparison.Ordinal) && currentLayerId is not null)
                {
                    insideLayerOutput = true;
                }

                if (string.Equals(reader.LocalName, "edge", StringComparison.Ordinal))
                {
                    string? fromLayer = reader.GetAttribute("from-layer");
                    string? fromPort = reader.GetAttribute("from-port");
                    string? toLayer = reader.GetAttribute("to-layer");
                    string? toPort = reader.GetAttribute("to-port");
                    if (string.IsNullOrWhiteSpace(fromLayer) ||
                        string.IsNullOrWhiteSpace(fromPort) ||
                        string.IsNullOrWhiteSpace(toLayer) ||
                        string.IsNullOrWhiteSpace(toPort))
                    {
                        return false;
                    }

                    graphEdges.Add(new GraphEdge(fromLayer, fromPort, toLayer, toPort));
                }

                if (string.Equals(reader.LocalName, "port", StringComparison.Ordinal))
                {
                    string? names = reader.GetAttribute("names");
                    currentPortNames = string.IsNullOrWhiteSpace(names)
                        ? null
                        : names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    currentPortId = reader.GetAttribute("id");
                    currentPortPrecision = reader.GetAttribute("precision");
                    currentPortLastDimension = null;
                    currentPortIsOutput = insideLayerOutput;
                    if (insideLayerInput &&
                        string.Equals(currentLayerType, "Result", StringComparison.Ordinal) &&
                        currentLayerId is not null)
                    {
                        if (string.IsNullOrWhiteSpace(currentPortId))
                        {
                            return false;
                        }

                        resultLayers[currentLayerId].InputPortIds.Add(currentPortId);
                    }

                    if (reader.IsEmptyElement)
                    {
                        if (currentPortNames is not null && !string.IsNullOrWhiteSpace(currentPortPrecision))
                        {
                            observedPorts.AddRange(currentPortNames.Select(name => new ObservedPort(
                                name,
                                currentPortPrecision,
                                currentPortLastDimension,
                                currentLayerId,
                                currentLayerType,
                                currentPortId,
                                currentPortIsOutput)));
                        }

                        currentPortNames = null;
                        currentPortId = null;
                        currentPortPrecision = null;
                        currentPortLastDimension = null;
                        currentPortIsOutput = false;
                    }
                }
                else if (currentPortNames is not null && string.Equals(reader.LocalName, "dim", StringComparison.Ordinal))
                {
                    insideNamedPortDimension = true;
                }
            }
            else if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA && insideNamedPortDimension)
            {
                if (!long.TryParse(reader.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long dimension))
                {
                    return false;
                }

                currentPortLastDimension = dimension;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.LocalName, "dim", StringComparison.Ordinal))
            {
                insideNamedPortDimension = false;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.LocalName, "port", StringComparison.Ordinal))
            {
                if (currentPortNames is not null && !string.IsNullOrWhiteSpace(currentPortPrecision))
                {
                    observedPorts.AddRange(currentPortNames.Select(name => new ObservedPort(
                        name,
                        currentPortPrecision,
                        currentPortLastDimension,
                        currentLayerId,
                        currentLayerType,
                        currentPortId,
                        currentPortIsOutput)));
                }

                currentPortNames = null;
                currentPortId = null;
                currentPortPrecision = null;
                currentPortLastDimension = null;
                currentPortIsOutput = false;
                insideNamedPortDimension = false;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.LocalName, "output", StringComparison.Ordinal))
            {
                insideLayerOutput = false;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.LocalName, "input", StringComparison.Ordinal))
            {
                insideLayerInput = false;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.LocalName, "layer", StringComparison.Ordinal))
            {
                insideConstant = false;
                insideLayerInput = false;
                insideLayerOutput = false;
                currentLayerId = null;
                currentLayerType = null;
            }
        }

        xml.Stream.Position = 0;
        return validRoot &&
            requiredLayerTypes.All(observedTypes.Contains) &&
            ResultDestinationsAreValid(graphEdges, resultLayers) &&
            requiredPorts.All(expected => IsUniqueResultConnectedOutput(expected, observedPorts, graphEdges, resultLayers));
    }

    private static bool ResultDestinationsAreValid(
        IReadOnlyCollection<GraphEdge> graphEdges,
        IReadOnlyDictionary<string, ResultLayer> resultLayers)
    {
        foreach (GraphEdge edge in graphEdges)
        {
            if (resultLayers.TryGetValue(edge.ToLayer, out ResultLayer? result) &&
                result.InputPortIds.Count(port => string.Equals(port, edge.ToPort, StringComparison.Ordinal)) != 1)
            {
                return false;
            }
        }

        return resultLayers.Values.All(result => result.InputPortIds.All(inputPort =>
            graphEdges.Count(edge =>
                string.Equals(edge.ToLayer, result.LayerId, StringComparison.Ordinal) &&
                string.Equals(edge.ToPort, inputPort, StringComparison.Ordinal)) == 1));
    }

    private static bool IsUniqueResultConnectedOutput(
        ExpectedPort expected,
        IReadOnlyCollection<ObservedPort> observedPorts,
        IReadOnlyCollection<GraphEdge> graphEdges,
        IReadOnlyDictionary<string, ResultLayer> resultLayers)
    {
        ObservedPort[] candidates = observedPorts.Where(observed =>
            string.Equals(observed.Name, expected.Name, StringComparison.Ordinal) &&
            string.Equals(observed.Precision, expected.Precision, StringComparison.Ordinal) &&
            (expected.FinalDimension is null || observed.FinalDimension == expected.FinalDimension)).ToArray();
        if (candidates.Length != 1)
        {
            return false;
        }

        ObservedPort candidate = candidates[0];
        if (!candidate.IsOutput ||
            string.IsNullOrWhiteSpace(candidate.LayerId) ||
            string.IsNullOrWhiteSpace(candidate.PortId) ||
            string.Equals(candidate.LayerType, "Result", StringComparison.Ordinal))
        {
            return false;
        }

        ResultLayer[] correspondingResults = resultLayers.Values
            .Where(result => result.OutputNames.Contains(expected.Name))
            .ToArray();
        if (correspondingResults.Length != 1 || correspondingResults[0].InputPortIds.Count != 1)
        {
            return false;
        }

        ResultLayer result = correspondingResults[0];
        string inputPort = result.InputPortIds[0];
        GraphEdge[] destinationEdges = graphEdges.Where(edge =>
            string.Equals(edge.ToLayer, result.LayerId, StringComparison.Ordinal) &&
            string.Equals(edge.ToPort, inputPort, StringComparison.Ordinal)).ToArray();
        return destinationEdges.Length == 1 &&
            string.Equals(destinationEdges[0].FromLayer, candidate.LayerId, StringComparison.Ordinal) &&
            string.Equals(destinationEdges[0].FromPort, candidate.PortId, StringComparison.Ordinal);
    }

    private static HashSet<string> SplitNames(string? names) =>
        string.IsNullOrWhiteSpace(names)
            ? new HashSet<string>(StringComparer.Ordinal)
            : names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);

    private sealed record ExpectedPort(string Name, string Precision, long? FinalDimension);

    private sealed record ObservedPort(
        string Name,
        string Precision,
        long? FinalDimension,
        string? LayerId,
        string? LayerType,
        string? PortId,
        bool IsOutput);

    private sealed record GraphEdge(string FromLayer, string FromPort, string ToLayer, string ToPort);

    private sealed record ResultLayer(string LayerId, HashSet<string> OutputNames, List<string> InputPortIds);

    private sealed record SpecialTokenFact(string Role, string Content);

    private sealed class OptionalTokenizerFacts
    {
        public Dictionary<string, long>? Vocabulary { get; set; }

        public Dictionary<string, long>? AddedTokens { get; set; }

        public Dictionary<string, long>? TokenizerVocabulary { get; set; }

        public Dictionary<string, long>? TokenizerAddedTokens { get; set; }

        public string? TokenizerUnknownToken { get; set; }

        public List<SpecialTokenFact>? SpecialTokens { get; set; }
    }

    private static OpenVinoPackageSnapshotEntry GetRequired(OpenVinoPackageSnapshot snapshot, string name)
    {
        if (!snapshot.TryGetEntry(name, out OpenVinoPackageSnapshotEntry entry))
        {
            throw new InvalidDataException("Required package resource is absent.");
        }

        return entry;
    }

    private static string ComputeManifestDigest(IEnumerable<OpenVinoPackageSnapshotEntry> entries)
    {
        using IncrementalHash manifest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (OpenVinoPackageSnapshotEntry entry in entries.OrderBy(static entry => entry.RelativeName, StringComparer.Ordinal))
        {
            string tuple = entry.RelativeName + '\0' +
                entry.Length.ToString(CultureInfo.InvariantCulture) + '\0' +
                entry.Sha256 + '\n';
            manifest.AppendData(Encoding.UTF8.GetBytes(tuple));
        }

        return Convert.ToHexString(manifest.GetHashAndReset()).ToLowerInvariant();
    }
}
