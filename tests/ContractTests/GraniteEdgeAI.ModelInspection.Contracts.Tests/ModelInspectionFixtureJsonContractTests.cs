using System.Buffers;
using System.Text;
using System.Text.Json.Nodes;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

[TestClass]
[TestCategory("Architecture")]
[TestCategory("Contract")]
public sealed class ModelInspectionFixtureJsonContractTests
{
    [TestMethod]
    public void StrictJson_RejectsUnknownAndDuplicateProperties()
    {
        string unknownProperty = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"title\":\"Ready model\"",
            "\"title\":\"Ready model\",\"unexpected\":true",
            StringComparison.Ordinal);
        string duplicateProperty = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"title\":\"Ready model\"",
            "\"title\":\"Ready model\",\"title\":\"Duplicate\"",
            StringComparison.Ordinal);
        string escapedDuplicateProperty = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"title\":\"Ready model\"",
            "\"title\":\"Ready model\",\"t\\u0069tle\":\"Duplicate\"",
            StringComparison.Ordinal);

        AssertInvalid(unknownProperty);
        AssertInvalid(duplicateProperty);
        AssertInvalid(escapedDuplicateProperty);
    }

    [TestMethod]
    public void StrictJson_AcceptsOneCanonicalDocumentAndScopedPropertyNames()
    {
        ModelInspectionFixtureCatalogue catalogue = Load(
            FixtureContractDocuments.ValidDescriptorJson);

        Assert.AreEqual(1, catalogue.Fixtures.Count);
        Assert.AreEqual("MI-001", catalogue.Fixtures[0].Id);
    }

    [TestMethod]
    public void StrictJson_RejectsUnknownMembersAtEveryObjectDepth()
    {
        Action<JsonObject>[] mutations =
        [
            root => root["unexpected"] = true,
            root => root["coverage"]!.AsObject()["unexpected"] = true,
            root => root["input"]!.AsObject()["unexpected"] = true,
            root => root["input"]!["request"]!.AsObject()["unexpected"] = true,
            root => root["input"]!["attempts"]![0]!.AsObject()["unexpected"] = true,
            root => root["input"]!["attempts"]![0]!["serviceSteps"]![0]!
                .AsObject()["unexpected"] = true,
            root => root["input"]!["attempts"]![0]!["serviceSteps"]![0]!["trigger"]!
                .AsObject()["unexpected"] = true,
            root => root["input"]!["attempts"]![0]!["serviceSteps"]![0]!["effect"]!
                .AsObject()["unexpected"] = true,
            root => root["input"]!["attempts"]![0]!["serviceSteps"]![0]!["effect"]!["progress"] =
                new JsonObject
                {
                    ["stage"] = "checkModelPackage",
                    ["status"] = "active",
                    ["completedStageCount"] = 0,
                    ["fraction"] = null,
                    ["detailProfile"] = "default",
                    ["unexpected"] = true
                },
            root => root["input"]!["setupSteps"]![0]!.AsObject()["unexpected"] = true,
            root => root["expected"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["figma"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["outcome"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["outcome"]!["title"]!
                .AsObject()["unexpected"] = true,
            root => root["expected"]!["model"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["model"]!["metadata"]!.AsArray().Add(
                new JsonObject
                {
                    ["id"] = "metadata",
                    ["label"] = root["expected"]!["outcome"]!["title"]!.DeepClone(),
                    ["value"] = root["expected"]!["outcome"]!["title"]!.DeepClone(),
                    ["unexpected"] = true
                }),
            root => root["expected"]!["model"]!["checks"]!.AsArray().Add(
                new JsonObject
                {
                    ["id"] = "check",
                    ["text"] = root["expected"]!["outcome"]!["title"]!.DeepClone(),
                    ["status"] = "passed",
                    ["unexpected"] = true
                }),
            root => root["expected"]!["content"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["content"]!["rows"]!.AsArray().Add(
                new JsonObject
                {
                    ["id"] = "row",
                    ["primaryText"] = root["expected"]!["outcome"]!["title"]!.DeepClone(),
                    ["secondaryText"] = null,
                    ["status"] = "passed",
                    ["unexpected"] = true
                }),
            root => root["expected"]!["actions"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["actions"]!["items"]![0]!
                .AsObject()["unexpected"] = true,
            root => root["expected"]!["footer"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["footer"]!["rows"]![0]!
                .AsObject()["unexpected"] = true,
            root => root["expected"]!["focus"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["automation"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["automation"]!["controls"]![0]!
                .AsObject()["unexpected"] = true,
            root => root["expected"]!["announcements"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["announcements"]!["items"]!.AsArray().Add(
                new JsonObject
                {
                    ["copyKey"] = "fixture.ready.title",
                    ["defaultText"] = "Ready",
                    ["unexpected"] = true
                }),
            root => root["expected"]!["rowsAndScroll"]!.AsObject()["unexpected"] = true,
            root => root["expected"]!["retainedIdentities"]!
                .AsObject()["unexpected"] = true,
            root => root["presetExpectations"]!["P01"]!
                .AsObject()["unexpected"] = true,
            root => root["presetExpectations"]!["P01"]!["textRoles"]!.AsArray().Add(
                new JsonObject
                {
                    ["id"] = "title",
                    ["behavior"] = "wrap",
                    ["unexpected"] = true
                }),
            root => root["interactions"]!.AsArray().Add(
                new JsonObject
                {
                    ["id"] = "current",
                    ["kind"] = "reset",
                    ["sourceCheckpoint"] = "ready-observed",
                    ["target"] = "ready-observed",
                    ["expectedFocus"] = null,
                    ["expectedAnnouncementCount"] = 0,
                    ["expectedFooterStatus"] = "complete",
                    ["lifetimeEffect"] = "none",
                    ["unexpected"] = true
                })
        ];

        foreach (Action<JsonObject> mutation in mutations)
        {
            AssertInvalid(FixtureContractDocuments.MutateDescriptor(mutation));
        }
    }

    [TestMethod]
    public void StrictJson_RejectsEnumCaseIntegerAndUnknownValues()
    {
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root["category"] = "Screen"));
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root["category"] = 0));
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root["category"] = "unknown"));
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(root =>
        {
            JsonNode? title = root["title"]!.DeepClone();
            root.Remove("title");
            root["Title"] = title;
        }));
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root["expected"]!["figma"]!["state"] = "ReadyCollapsed"));
    }

    [TestMethod]
    public void StrictJson_RejectsNullAndMissingRequiredMembers()
    {
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root["title"] = null));
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root.Remove("expected")));
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root["input"]!.AsObject().Remove("request")));
        AssertInvalid(FixtureContractDocuments.MutateDescriptor(
            root => root["expected"]!["actions"]!["items"] = null));
    }

    [TestMethod]
    public void StrictJson_RejectsExcessiveBytesAndDepth()
    {
        byte[] oversized = new byte[StrictModelInspectionFixtureJson.MaximumDocumentBytes + 1];
        Array.Fill(oversized, (byte)' ');

        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(
                FixtureContractDocuments.SchemaSource());
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(
                FixtureContractDocuments.PolicySource(),
                schema);

        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            ModelInspectionFixtureCatalogue.LoadDescriptors(
                [new(FixtureContractDocuments.ValidFileName, oversized)],
                policy,
                schema));

        string nested = "{\"x\":" +
            new string('[', StrictModelInspectionFixtureJson.MaximumDepth + 1) +
            new string(']', StrictModelInspectionFixtureJson.MaximumDepth + 1) +
            "}";
        AssertInvalid(nested);
    }

    [TestMethod]
    public void StrictJson_RejectsNonFiniteNumbersMultipleDocumentsCommentsAndTrailingCommas()
    {
        string nonFinite = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"minimumContentColumnWidth\":480",
            "\"minimumContentColumnWidth\":NaN",
            StringComparison.Ordinal);
        string infinity = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "\"maximumContentColumnWidth\":960",
            "\"maximumContentColumnWidth\":Infinity",
            StringComparison.Ordinal);
        string multiple = FixtureContractDocuments.ValidDescriptorJson + "{}";
        string comment = FixtureContractDocuments.ValidDescriptorJson.Replace(
            "{",
            "{/*comment*/",
            StringComparison.Ordinal);
        string trailingComma = FixtureContractDocuments.ValidDescriptorJson.TrimEnd()[..^1] + ",}";

        AssertInvalid(nonFinite);
        AssertInvalid(infinity);
        AssertInvalid(multiple);
        AssertInvalid(comment);
        AssertInvalid(trailingComma);
    }

    [TestMethod]
    public void ValidatedFixture_RetainsRawBytesHashAndSeparateInputBrand()
    {
        ModelInspectionFixtureCatalogue catalogue = Load(
            FixtureContractDocuments.ValidDescriptorJson);
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single();
        byte[] expectedBytes = Encoding.UTF8.GetBytes(
            FixtureContractDocuments.ValidDescriptorJson);

        CollectionAssert.AreEqual(expectedBytes, fixture.RawUtf8.ToArray());
        Assert.AreEqual(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expectedBytes)),
            fixture.Sha256);
        Assert.AreEqual(
            typeof(ValidatedModelInspectionFixtureInput),
            fixture.Input.GetType());
        Assert.AreNotEqual(fixture.Input.GetType(), fixture.Expected.GetType());

        ValidatedModelInspectionFixture revalidated =
            ModelInspectionFixtureCatalogue.RevalidateDescriptor(
                FixtureContractDocuments.DescriptorSource(),
                catalogue.Policy,
                catalogue.Schema,
                catalogue.Index);
        Assert.AreEqual(fixture.Sha256, revalidated.Sha256);

        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            ModelInspectionFixtureCatalogue.RevalidateDescriptor(
                FixtureContractDocuments.DescriptorSource(
                    FixtureContractDocuments.ValidDescriptorJson + " "),
                catalogue.Policy,
                catalogue.Schema,
                catalogue.Index));
    }

    [TestMethod]
    public void DocumentApis_SnapshotCallerBytesBeforeValidationAndBranding()
    {
        byte[] schemaBytes = Encoding.UTF8.GetBytes(FixtureContractDocuments.SchemaJson);
        byte[] changedSchemaBytes = Encoding.UTF8.GetBytes(
            FixtureContractDocuments.SchemaJson.Replace(
                "fixture descriptor",
                "fixture xescriptor",
                StringComparison.Ordinal));
        using FirstAccessMemoryManager schemaMemory =
            new(schemaBytes, changedSchemaBytes);
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(new(
                "model-inspection-fixture.schema.json",
                schemaMemory.ReadOnlyMemory));
        CollectionAssert.AreEqual(schemaBytes, schema.RawUtf8.ToArray());

        byte[] policyBytes = Encoding.UTF8.GetBytes(FixtureContractDocuments.PolicyJson);
        byte[] changedPolicyBytes = Encoding.UTF8.GetBytes(
            FixtureContractDocuments.PolicyJson.Replace(
                "\"fixture.ready.title\":\"Ready\"",
                "\"fixture.ready.title\":\"Xeady\"",
                StringComparison.Ordinal));
        using FirstAccessMemoryManager policyMemory =
            new(policyBytes, changedPolicyBytes);
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(new(
                "model-inspection-fixture-coverage-policy.json",
                policyMemory.ReadOnlyMemory), schema);
        CollectionAssert.AreEqual(policyBytes, policy.RawUtf8.ToArray());
        Assert.AreEqual("Ready", policy.Value.CopyRegistry["fixture.ready.title"]);

        byte[] descriptorBytes = Encoding.UTF8.GetBytes(
            FixtureContractDocuments.ValidDescriptorJson);
        byte[] changedDescriptorBytes = Encoding.UTF8.GetBytes(
            FixtureContractDocuments.ValidDescriptorJson.Replace(
                "\"title\":\"Ready model\"",
                "\"title\":\"Xeady model\"",
                StringComparison.Ordinal));
        using FirstAccessMemoryManager descriptorMemory =
            new(descriptorBytes, changedDescriptorBytes);
        ModelInspectionFixtureCatalogue catalogue =
            ModelInspectionFixtureCatalogue.LoadDescriptors(
                [new(FixtureContractDocuments.ValidFileName, descriptorMemory.ReadOnlyMemory)],
                policy,
                schema);
        ValidatedModelInspectionFixture fixture = catalogue.Fixtures.Single();
        CollectionAssert.AreEqual(descriptorBytes, fixture.RawUtf8.ToArray());
        Assert.AreEqual("Ready model", fixture.Title);

        using FirstAccessMemoryManager revalidationMemory =
            new(descriptorBytes, changedDescriptorBytes);
        ValidatedModelInspectionFixture revalidated =
            ModelInspectionFixtureCatalogue.RevalidateDescriptor(
                new(FixtureContractDocuments.ValidFileName, revalidationMemory.ReadOnlyMemory),
                policy,
                schema,
                catalogue.Index);
        CollectionAssert.AreEqual(descriptorBytes, revalidated.RawUtf8.ToArray());
        Assert.AreEqual(fixture.Sha256, revalidated.Sha256);
    }

    private static ModelInspectionFixtureCatalogue Load(string descriptorJson)
    {
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(
                FixtureContractDocuments.SchemaSource());
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(
                FixtureContractDocuments.PolicySource(),
                schema);
        return ModelInspectionFixtureCatalogue.LoadDescriptors(
            [FixtureContractDocuments.DescriptorSource(descriptorJson)],
            policy,
            schema);
    }

    internal static void AssertInvalid(
        string descriptorJson,
        string fileName = FixtureContractDocuments.ValidFileName)
    {
        VerifiedModelInspectionFixtureSchema schema =
            ModelInspectionFixtureCatalogue.VerifySchema(
                FixtureContractDocuments.SchemaSource());
        ValidatedModelInspectionFixtureCoveragePolicy policy =
            ModelInspectionFixtureCatalogue.LoadPolicy(
                FixtureContractDocuments.PolicySource(),
                schema);

        Assert.ThrowsExactly<ModelInspectionFixtureValidationException>(() =>
            ModelInspectionFixtureCatalogue.LoadDescriptors(
                [FixtureContractDocuments.DescriptorSource(descriptorJson, fileName)],
                policy,
                schema));
    }

    private sealed class FirstAccessMemoryManager : MemoryManager<byte>
    {
        private readonly byte[] _first;
        private readonly byte[] _later;
        private int _accessCount;

        internal FirstAccessMemoryManager(byte[] first, byte[] later)
        {
            Assert.AreEqual(first.Length, later.Length);
            _first = first;
            _later = later;
        }

        internal ReadOnlyMemory<byte> ReadOnlyMemory =>
            CreateMemory(_first.Length);

        public override Span<byte> GetSpan() =>
            Interlocked.Increment(ref _accessCount) == 1 ? _first : _later;

        public override MemoryHandle Pin(int elementIndex = 0) =>
            throw new NotSupportedException();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}

internal static class FixtureContractDocuments
{
    internal const string ValidFileName =
        "MI-001-ready-clean-compatible-model-collapsed.fixture.json";

    internal const string SchemaJson =
        """
        {
          "$schema":"https://json-schema.org/draft/2020-12/schema",
          "$id":"model-inspection-fixture.schema.json",
          "title":"Model Inspection fixture descriptor",
          "type":"object",
          "schemaVersion":1,
          "additionalProperties":false
        }
        """;

    internal const string PolicyJson =
        """
        {
          "$schema":"model-inspection-fixture.schema.json",
          "schemaVersion":1,
          "fixtures":[{
            "id":"MI-001",
            "fileName":"MI-001-ready-clean-compatible-model-collapsed.fixture.json",
            "targetCondition":"ready-clean-compatible-model",
            "variant":"collapsed",
            "pairedWithId":null,
            "canonicalFigmaState":"readyCollapsed",
            "requiredCoverageTags":[],
            "requiredInteractions":[],
            "requiredPresets":["P01"]
          }],
          "presets":[{
            "id":"P01",
            "width":"desktop1440",
            "resources":"light",
            "text":"standard100",
            "motion":"normal"
          }],
          "disclosurePairs":[],
          "gallerySwitchPairs":[],
          "copyRegistry":{
            "fixture.ready.title":"Ready",
            "fixture.model.name":"Synthetic Granite",
            "fixture.model.file":"synthetic-granite.gguf",
            "fixture.content.heading":"Inspection complete",
            "fixture.action.choose":"Choose another model"
          },
          "externalEvidenceLinks":[]
        }
        """;

    internal const string ValidDescriptorJson =
        """
        {
          "$schema":"model-inspection-fixture.schema.json",
          "schemaVersion":1,
          "id":"MI-001",
          "targetCondition":"ready-clean-compatible-model",
          "variant":"collapsed",
          "title":"Ready model",
          "category":"screen",
          "coverage":{
            "figmaStates":["readyCollapsed"],
            "stages":[],
            "stageStatuses":[],
            "outcomes":["ready"],
            "interactions":[],
            "lifecycleTags":[],
            "failureProfiles":[],
            "stressTags":[]
          },
          "input":{
            "request":{
              "displayName":"Synthetic Granite",
              "displayFileName":"synthetic-granite.gguf",
              "evidenceProfile":"compatible"
            },
            "attempts":[{
              "attempt":1,
              "serviceSteps":[{
                "trigger":{"kind":"checkpoint","checkpoint":"service-ready"},
                "effect":{
                  "kind":"completed",
                  "progress":null,
                  "outcome":"ready",
                  "evidenceProfile":"compatible",
                  "failureProfile":null,
                  "deferredCheckpoint":null
                }
              }]
            }],
            "setupSteps":[
              {
                "kind":"release-service-checkpoint",
                "attempt":1,
                "checkpoint":"service-ready",
                "interactionId":null
              },
              {
                "kind":"observe",
                "attempt":null,
                "checkpoint":"ready-observed",
                "interactionId":null
              }
            ],
            "observationCheckpoint":"ready-observed"
          },
          "expected":{
            "figma":{
              "state":"readyCollapsed",
              "geometryProfile":"canonical"
            },
            "outcome":{
              "visible":true,
              "kind":"ready",
              "tone":"success",
              "badge":null,
              "title":{"copyKey":"fixture.ready.title","defaultText":"Ready"},
              "supportingText":null
            },
            "model":{
              "visible":true,
              "mode":"compact",
              "badge":"inspected",
              "displayName":{"copyKey":"fixture.model.name","defaultText":"Synthetic Granite"},
              "displayFileName":{"copyKey":"fixture.model.file","defaultText":"synthetic-granite.gguf"},
              "metadata":[],
              "checks":[],
              "disclosureExpanded":false
            },
            "content":{
              "visible":true,
              "mode":"hidden",
              "heading":{"copyKey":"fixture.content.heading","defaultText":"Inspection complete"},
              "rows":[]
            },
            "actions":{
              "visible":true,
              "mode":"result",
              "items":[{
                "id":"choose-another",
                "label":{"copyKey":"fixture.action.choose","defaultText":"Choose another model"},
                "visible":true,
                "enabled":true,
                "helpText":null
              }]
            },
            "footer":{
              "rows":[
                {"stage":"checkModelPackage","status":"complete"},
                {"stage":"readModelConfiguration","status":"complete"},
                {"stage":"validateTokenizerAndChatSetup","status":"complete"},
                {"stage":"validateModelStructure","status":"complete"},
                {"stage":"confirmCoreRuntimeCompatibility","status":"complete"}
              ],
              "status":"complete"
            },
            "focus":{"target":"choose-another"},
            "automation":{
              "controls":[{
                "id":"choose-another",
                "accessibleName":{"copyKey":"fixture.action.choose","defaultText":"Choose another model"},
                "controlType":"button",
                "liveSetting":"off",
                "helpText":null
              }]
            },
            "announcements":{"count":0,"items":[]},
            "rowsAndScroll":{"orderedRowIds":[],"scrollOwner":null},
            "retainedIdentities":{"ids":[]}
          },
          "presetExpectations":{
            "P01":{
              "responsiveLayout":"desktop",
              "minimumContentColumnWidth":480,
              "maximumContentColumnWidth":960,
              "noClipping":true,
              "noOverlap":true,
              "allRequiredContentReachable":true,
              "textRoles":[],
              "scrollOwner":null,
              "minimumPointerTargetWidth":44,
              "minimumPointerTargetHeight":44,
              "logicalReadingOrder":["outcome","model","content","actions"],
              "tabOrder":["choose-another"],
              "focusTarget":"choose-another",
              "resources":"light",
              "textScale":"standard100",
              "motion":"normal",
              "expectedAnimationStarts":0
            }
          },
          "interactions":[],
          "presets":["P01"]
        }
        """;

    internal static ModelInspectionFixtureDocumentSource SchemaSource() =>
        Source("model-inspection-fixture.schema.json", SchemaJson);

    internal static ModelInspectionFixtureDocumentSource PolicySource(
        string json = PolicyJson) =>
        Source("model-inspection-fixture-coverage-policy.json", json);

    internal static ModelInspectionFixtureDocumentSource DescriptorSource(
        string json = ValidDescriptorJson,
        string fileName = ValidFileName) =>
        Source(fileName, json);

    internal static ModelInspectionFixtureDocumentSource Source(
        string fileName,
        string json) =>
        new(fileName, Encoding.UTF8.GetBytes(json));

    internal static string MutateDescriptor(Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(ValidDescriptorJson)!.AsObject();
        mutation(root);
        return root.ToJsonString();
    }

    internal static string MutatePolicy(Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(PolicyJson)!.AsObject();
        mutation(root);
        return root.ToJsonString();
    }
}
