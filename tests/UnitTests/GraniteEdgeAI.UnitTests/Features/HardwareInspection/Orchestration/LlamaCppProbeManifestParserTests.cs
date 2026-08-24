using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[TestCategory("HardwareInspection")]
public sealed class LlamaCppProbeManifestParserTests
{
    private const string FailureMessage = "The packaged probe manifest is invalid.";

    [TestMethod]
    public void Parse_AcceptsOnlyThePinnedContractAndCopiesManifestData()
    {
        byte[] bytes = CreateManifestBytes();

        TrustedToolPackageManifest manifest = LlamaCppProbeManifestParser.Parse(bytes);

        Assert.AreEqual(LlamaCppCapabilityCommandContract.ToolId, manifest.ToolId);
        Assert.AreEqual(LlamaCppCapabilityCommandContract.Version, manifest.Version);
        Assert.AreEqual(LlamaCppCapabilityCommandContract.ExecutableName, manifest.ExecutableRelativePath);
        Assert.AreEqual(new string('a', 64), manifest.ExecutableSha256);
        CollectionAssert.AreEqual(new[] { LlamaCppCapabilityCommandContract.ExecutableName, "llama.dll" }, manifest.RequiredMembers.ToArray());
        Assert.AreEqual(PeMachine.Amd64, manifest.RequiredMachine);
        Assert.AreEqual(TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation, manifest.Disposition);
        AssertCommand(manifest.Commands[0], "identity", "identity", "--format", "json-v1");
        AssertCommand(manifest.Commands[1], "capabilities", "capabilities", "--format", "json-v1");
    }

    [TestMethod]
    public void Parse_RejectsIdentitySchemaAndAuthorityDriftWithOneSafeMessage()
    {
        (string property, JsonNode? value)[] mutations =
        [
            ("schemaVersion", 2),
            ("toolId", "other"),
            ("version", "other"),
            ("executable", "other.exe"),
            ("executableSha256", new string('A', 64)),
            ("executableSha256", "abc"),
            ("machine", "Arm64"),
            ("disposition", "FunctionalPassWithPackagingConcern"),
        ];

        foreach ((string property, JsonNode? value) in mutations)
        {
            JsonObject root = CreateManifestNode();
            root[property] = value;
            AssertRejected(Serialize(root));
        }

        JsonObject unknown = CreateManifestNode();
        unknown["unknown"] = true;
        AssertRejected(Serialize(unknown));

        JsonObject caseDrift = CreateManifestNode();
        caseDrift["ToolId"] = caseDrift["toolId"]!.DeepClone();
        caseDrift.Remove("toolId");
        AssertRejected(Serialize(caseDrift));

        string duplicate = Encoding.UTF8.GetString(CreateManifestBytes())
            .Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal);
        AssertRejected(Encoding.UTF8.GetBytes(duplicate));
    }

    [TestMethod]
    public void Parse_RejectsNonCanonicalEncodingFramingAndJson()
    {
        byte[] canonical = CreateManifestBytes();
        AssertRejected([0xef, 0xbb, 0xbf, .. canonical]);
        AssertRejected([.. canonical[..^1], (byte)'\r', (byte)'\n']);
        AssertRejected(canonical[..^1]);
        AssertRejected([.. canonical, (byte)'\n']);
        AssertRejected([0xc3, 0x28, (byte)'\n']);
        AssertRejected(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,}\n"));
        AssertRejected(Encoding.UTF8.GetBytes("{/*x*/\"schemaVersion\":1}\n"));
        AssertRejected(Encoding.UTF8.GetBytes(new string('[', 9) + new string(']', 9) + "\n"));
        AssertRejected(new byte[(64 * 1024) + 1]);
    }

    [TestMethod]
    public void Parse_RejectsUnsafeDuplicateUnsortedEmptyAndOversizedInventories()
    {
        string[][] invalidMembers =
        [
            [],
            [LlamaCppCapabilityCommandContract.ExecutableName, "../llama.dll"],
            [LlamaCppCapabilityCommandContract.ExecutableName, "folder/llama.dll"],
            [LlamaCppCapabilityCommandContract.ExecutableName, "LLAMA.dll", "llama.dll"],
            ["llama.dll", LlamaCppCapabilityCommandContract.ExecutableName],
            [.. Enumerable.Range(0, 64).Select(index => $"member-{index:D2}.dll"), LlamaCppCapabilityCommandContract.ExecutableName],
        ];

        foreach (string[] members in invalidMembers)
        {
            JsonObject root = CreateManifestNode();
            root["members"] = new JsonArray(
                members.Select(member => JsonValue.Create(member)).ToArray<JsonNode?>());
            AssertRejected(Serialize(root));
        }
    }

    [TestMethod]
    public void Parse_RejectsCommandShapeOrderArgumentsAndProperties()
    {
        JsonObject reversed = CreateManifestNode();
        JsonArray commands = reversed["commands"]!.AsArray();
        (commands[0], commands[1]) = (commands[1], commands[0]);
        AssertRejected(Serialize(reversed));

        JsonObject changedArgument = CreateManifestNode();
        changedArgument["commands"]![0]!["arguments"]![2] = "text";
        AssertRejected(Serialize(changedArgument));

        JsonObject extraCommand = CreateManifestNode();
        extraCommand["commands"]!.AsArray().Add(new JsonObject
        {
            ["identity"] = "extra",
            ["arguments"] = new JsonArray("extra"),
        });
        AssertRejected(Serialize(extraCommand));

        JsonObject unknownProperty = CreateManifestNode();
        unknownProperty["commands"]![0]!["unknown"] = true;
        AssertRejected(Serialize(unknownProperty));

        string duplicateProperty = Encoding.UTF8.GetString(CreateManifestBytes())
            .Replace("\"identity\":\"identity\"", "\"identity\":\"identity\",\"identity\":\"identity\"", StringComparison.Ordinal);
        AssertRejected(Encoding.UTF8.GetBytes(duplicateProperty));
    }

    private static JsonObject CreateManifestNode() => new()
    {
        ["schemaVersion"] = 1,
        ["toolId"] = LlamaCppCapabilityCommandContract.ToolId,
        ["version"] = LlamaCppCapabilityCommandContract.Version,
        ["executable"] = LlamaCppCapabilityCommandContract.ExecutableName,
        ["executableSha256"] = new string('a', 64),
        ["members"] = new JsonArray(
            LlamaCppCapabilityCommandContract.ExecutableName,
            "llama.dll"),
        ["machine"] = "Amd64",
        ["disposition"] = "AcceptedForFunctionalEvaluation",
        ["commands"] = new JsonArray(
            new JsonObject
            {
                ["identity"] = "identity",
                ["arguments"] = new JsonArray("identity", "--format", "json-v1"),
            },
            new JsonObject
            {
                ["identity"] = "capabilities",
                ["arguments"] = new JsonArray("capabilities", "--format", "json-v1"),
            }),
    };

    private static byte[] CreateManifestBytes() => Serialize(CreateManifestNode());

    private static byte[] Serialize(JsonNode node) =>
        Encoding.UTF8.GetBytes(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");

    private static void AssertRejected(byte[] bytes)
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => LlamaCppProbeManifestParser.Parse(bytes));
        Assert.AreEqual(FailureMessage, error.Message);
        Assert.IsNull(error.InnerException);
    }

    private static void AssertCommand(TrustedToolCommand command, string identity, params string[] arguments)
    {
        Assert.AreEqual(identity, command.Identity);
        CollectionAssert.AreEqual(arguments, command.Arguments.ToArray());
    }
}
