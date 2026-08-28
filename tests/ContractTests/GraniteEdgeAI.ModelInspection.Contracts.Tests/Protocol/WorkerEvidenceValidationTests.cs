using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol;

/// <summary>
/// Defines the validation required for nested worker inspection evidence.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class WorkerEvidenceValidationTests
{
    private const string NullSpecialTokenIdsJson = """
        {
          "vocabularyCount": 32000,
          "vocabularyType": "BPE",
          "tokenizerSmokePassed": true,
          "tokenizerSmokeTokenCount": 4,
          "knownSpecialTokenIds": null
        }
        """;

    [TestMethod]
    public void ValidEvidenceFactoryCreatesCompleteEvidence()
    {
        WorkerInspectionEvidence evidence = TestJson.CreateValidEvidence();

        evidence.Validate();

        Assert.IsTrue(evidence.ModelFile.IntegrityPreserved);
        Assert.IsNotNull(evidence.Tokenizer.KnownSpecialTokenIds);
        Assert.HasCount(1, evidence.Observations);
    }

    [TestMethod]
    [DataRow("length")]
    [DataRow("timestamp")]
    [DataRow("sha256")]
    public void ModelFileIntegrityTrueRejectsContradictoryEvidence(
        string mutation)
    {
        WorkerModelFileEvidence valid = TestJson.CreateValidEvidence().ModelFile;
        WorkerModelFileEvidence contradictory = mutation switch
        {
            "length" => valid with { LengthAfter = valid.LengthAfter + 1 },
            "timestamp" => valid with
            {
                LastWriteTimeAfterUtc =
                    valid.LastWriteTimeAfterUtc.AddSeconds(1)
            },
            "sha256" => valid with { Sha256After = new string('B', 64) },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(contradictory.Validate);
    }

    [TestMethod]
    public void ModelFileIntegrityFalseAllowsMatchingSnapshots()
    {
        WorkerModelFileEvidence evidence =
            TestJson.CreateValidEvidence().ModelFile with
            {
                IntegrityPreserved = false
            };

        evidence.Validate();
    }

    [TestMethod]
    public void ModelFileIntegrityTrueAllowsCaseInsensitiveShaMatch()
    {
        WorkerModelFileEvidence valid = TestJson.CreateValidEvidence().ModelFile;
        WorkerModelFileEvidence evidence = valid with
        {
            Sha256After = valid.Sha256Before.ToLowerInvariant()
        };

        evidence.Validate();
    }

    [TestMethod]
    [DataRow("ProtocolVersion")]
    [DataRow("WorkerVersion")]
    [DataRow("RuntimeProfile")]
    [DataRow("LLamaSharpVersion")]
    [DataRow("BackendPackageVersion")]
    [DataRow("MappedLlamaCppCommit")]
    [DataRow("NativeLibraryName")]
    [DataRow("ProcessArchitecture")]
    [DataRow("InspectionMode")]
    [DataRow("UsesCuda")]
    [DataRow("UsesVulkan")]
    [DataRow("GpuLayerCount")]
    public void RuntimeIdentityRejectsInvalidField(string field)
    {
        WorkerRuntimeIdentity valid = TestJson.CreateValidEvidence().Runtime;
        WorkerRuntimeIdentity invalid = field switch
        {
            "ProtocolVersion" => valid with { ProtocolVersion = 0 },
            "WorkerVersion" => valid with { WorkerVersion = string.Empty },
            "RuntimeProfile" => valid with { RuntimeProfile = "other" },
            "LLamaSharpVersion" => valid with
            {
                LLamaSharpVersion = string.Empty
            },
            "BackendPackageVersion" => valid with
            {
                BackendPackageVersion = string.Empty
            },
            "MappedLlamaCppCommit" => valid with
            {
                MappedLlamaCppCommit = string.Empty
            },
            "NativeLibraryName" => valid with
            {
                NativeLibraryName = string.Empty
            },
            "ProcessArchitecture" => valid with
            {
                ProcessArchitecture = "Arm64"
            },
            "InspectionMode" => valid with { InspectionMode = string.Empty },
            "UsesCuda" => valid with { UsesCuda = true },
            "UsesVulkan" => valid with { UsesVulkan = true },
            "GpuLayerCount" => valid with { GpuLayerCount = 1 },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(invalid.Validate);
    }

    [TestMethod]
    [DataRow("FileName.Empty")]
    [DataRow("FileName.Path")]
    [DataRow("CanonicalPathSha256")]
    [DataRow("CanonicalPathSha256.Invalid")]
    [DataRow("LengthBefore")]
    [DataRow("LengthAfter")]
    [DataRow("LastWriteTimeBeforeUtc")]
    [DataRow("LastWriteTimeAfterUtc")]
    [DataRow("Sha256Before")]
    [DataRow("Sha256Before.Invalid")]
    [DataRow("Sha256After")]
    public void ModelFileRejectsInvalidPrimitiveField(string field)
    {
        WorkerModelFileEvidence valid = TestJson.CreateValidEvidence().ModelFile;
        WorkerModelFileEvidence invalid = field switch
        {
            "FileName.Empty" => valid with { FileName = string.Empty },
            "FileName.Path" => valid with { FileName = @"C:\Models\granite.gguf" },
            "CanonicalPathSha256" => valid with
            {
                CanonicalPathSha256 = string.Empty
            },
            "CanonicalPathSha256.Invalid" => valid with
            {
                CanonicalPathSha256 = new string('z', 64)
            },
            "LengthBefore" => valid with { LengthBefore = 0 },
            "LengthAfter" => valid with { LengthAfter = 0 },
            "LastWriteTimeBeforeUtc" => valid with
            {
                LastWriteTimeBeforeUtc = default
            },
            "LastWriteTimeAfterUtc" => valid with
            {
                LastWriteTimeAfterUtc =
                    valid.LastWriteTimeAfterUtc.ToOffset(TimeSpan.FromHours(1))
            },
            "Sha256Before" => valid with { Sha256Before = string.Empty },
            "Sha256Before.Invalid" => valid with
            {
                Sha256Before = new string('a', 63),
                Sha256After = new string('a', 63)
            },
            "Sha256After" => valid with { Sha256After = string.Empty },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(invalid.Validate);
    }

    [TestMethod]
    [DataRow("Runtime")]
    [DataRow("ModelFile")]
    [DataRow("Configuration")]
    [DataRow("Tokenizer")]
    [DataRow("ChatTemplate")]
    [DataRow("Observations")]
    [DataRow("Observations.Element")]
    public void InspectionEvidenceRejectsNullSection(string section)
    {
        WorkerInspectionEvidence valid = TestJson.CreateValidEvidence();
        WorkerInspectionEvidence invalid = section switch
        {
            "Runtime" => valid with { Runtime = null! },
            "ModelFile" => valid with { ModelFile = null! },
            "Configuration" => valid with { Configuration = null! },
            "Tokenizer" => valid with { Tokenizer = null! },
            "ChatTemplate" => valid with { ChatTemplate = null! },
            "Observations" => valid with { Observations = null! },
            "Observations.Element" => valid with
            {
                Observations = [null!]
            },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(invalid.Validate);
    }

    [TestMethod]
    public void InspectionEvidenceRejectsNullKnownSpecialTokenIds()
    {
        WorkerTokenizerEvidence tokenizer =
            DeserializeTokenizerWithNullKnownSpecialTokenIds();
        WorkerInspectionEvidence evidence = TestJson.CreateValidEvidence() with
        {
            Tokenizer = tokenizer
        };

        Assert.ThrowsExactly<WorkerProtocolException>(evidence.Validate);
    }

    [TestMethod]
    [DataRow("Configuration.LayerCount")]
    [DataRow("Configuration.TokenizerModel")]
    [DataRow("Tokenizer.VocabularyCount")]
    [DataRow("Tokenizer.SmokeCountWithoutResult")]
    [DataRow("Tokenizer.SuccessWithoutTokens")]
    [DataRow("Tokenizer.NegativeSpecialTokenId")]
    [DataRow("Tokenizer.SpecialTokenIdOutsideVocabulary")]
    [DataRow("ChatTemplate.AbsentWithLength")]
    [DataRow("ChatTemplate.PresentWithoutDigest")]
    [DataRow("ChatTemplate.PresentWithInvalidDigest")]
    [DataRow("ChatTemplate.EmptyWithWrongDigest")]
    public void InspectionEvidenceRejectsContradictoryNestedEvidence(
        string mutation)
    {
        WorkerInspectionEvidence valid = TestJson.CreateValidEvidence();
        WorkerInspectionEvidence invalid = mutation switch
        {
            "Configuration.LayerCount" => valid with
            {
                Configuration = valid.Configuration with { LayerCount = 0 }
            },
            "Configuration.TokenizerModel" => valid with
            {
                Configuration = valid.Configuration with
                {
                    TokenizerModel = " "
                }
            },
            "Tokenizer.VocabularyCount" => valid with
            {
                Tokenizer = valid.Tokenizer with { VocabularyCount = 0 }
            },
            "Tokenizer.SmokeCountWithoutResult" => valid with
            {
                Tokenizer = valid.Tokenizer with
                {
                    TokenizerSmokePassed = null,
                    TokenizerSmokeTokenCount = 1
                }
            },
            "Tokenizer.SuccessWithoutTokens" => valid with
            {
                Tokenizer = valid.Tokenizer with
                {
                    TokenizerSmokePassed = true,
                    TokenizerSmokeTokenCount = 0
                }
            },
            "Tokenizer.NegativeSpecialTokenId" => valid with
            {
                Tokenizer = valid.Tokenizer with
                {
                    KnownSpecialTokenIds = new Dictionary<string, int>
                    {
                        ["bos"] = -1
                    }
                }
            },
            "Tokenizer.SpecialTokenIdOutsideVocabulary" => valid with
            {
                Tokenizer = valid.Tokenizer with
                {
                    VocabularyCount = 32_000,
                    KnownSpecialTokenIds = new Dictionary<string, int>
                    {
                        ["bos"] = 32_000
                    }
                }
            },
            "ChatTemplate.AbsentWithLength" => valid with
            {
                ChatTemplate = valid.ChatTemplate with
                {
                    Present = false,
                    LengthCharacters = 1,
                    Sha256 = null
                }
            },
            "ChatTemplate.PresentWithoutDigest" => valid with
            {
                ChatTemplate = valid.ChatTemplate with { Sha256 = null }
            },
            "ChatTemplate.PresentWithInvalidDigest" => valid with
            {
                ChatTemplate = valid.ChatTemplate with
                {
                    Sha256 = new string('z', 64)
                }
            },
            "ChatTemplate.EmptyWithWrongDigest" => valid with
            {
                ChatTemplate = valid.ChatTemplate with
                {
                    Present = true,
                    LengthCharacters = 0,
                    Sha256 = new string('a', 64)
                }
            },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(invalid.Validate);
    }

    [TestMethod]
    public void PresentEmptyTemplateEvidenceRemainsProtocolCompatible()
    {
        WorkerInspectionEvidence valid = TestJson.CreateValidEvidence();
        WorkerInspectionEvidence legacyEmpty = valid with
        {
            ChatTemplate = valid.ChatTemplate with
            {
                Present = true,
                LengthCharacters = 0,
                Sha256 =
                    "e3b0c44298fc1c149afbf4c8996fb924" +
                    "27ae41e4649b934ca495991b7852b855"
            }
        };

        legacyEmpty.Validate();
    }

    [TestMethod]
    public void CompletedDispatcherRejectsNullKnownSpecialTokenIds()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CompletionStatus = WorkerCompletionStatus.Completed,
            Evidence = TestJson.CreateValidEvidence() with
            {
                Tokenizer = DeserializeTokenizerWithNullKnownSpecialTokenIds()
            },
            OperationalFailure = null
        };
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            message,
            CreateProtocolEquivalentOptions());

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(payload));
    }

    [TestMethod]
    [DataRow("Code")]
    [DataRow("TechnicalCategory")]
    [DataRow("TechnicalDetail")]
    public void ObservationRejectsInvalidField(string field)
    {
        WorkerObservation valid = TestJson.CreateValidEvidence().Observations[0];
        WorkerObservation invalid = field switch
        {
            "Code" => valid with { Code = string.Empty },
            "TechnicalCategory" => valid with
            {
                TechnicalCategory = string.Empty
            },
            "TechnicalDetail" => valid with
            {
                TechnicalDetail = string.Empty
            },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(invalid.Validate);
    }

    [TestMethod]
    [DataRow("Code")]
    [DataRow("Message")]
    public void OperationalFailureRejectsInvalidField(string field)
    {
        WorkerOperationalFailure valid = CreateValidOperationalFailure();
        WorkerOperationalFailure invalid = field switch
        {
            "Code" => valid with { Code = string.Empty },
            "Message" => valid with { Message = string.Empty },
            _ => throw new InvalidOperationException()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(invalid.Validate);
    }

    private static WorkerTokenizerEvidence
        DeserializeTokenizerWithNullKnownSpecialTokenIds()
    {
        WorkerTokenizerEvidence? tokenizer =
            JsonSerializer.Deserialize<WorkerTokenizerEvidence>(
                NullSpecialTokenIdsJson,
                CreateProtocolEquivalentOptions());

        Assert.IsNotNull(tokenizer);
        Assert.IsNull(tokenizer.KnownSpecialTokenIds);
        return tokenizer;
    }

    private static WorkerOperationalFailure CreateValidOperationalFailure()
    {
        return new WorkerOperationalFailure
        {
            Code = "MI-OP-TEST",
            Message = "The controlled inspection failed."
        };
    }

    private static JsonSerializerOptions CreateProtocolEquivalentOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            MaxDepth = 32,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        };
        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));
        return options;
    }
}
