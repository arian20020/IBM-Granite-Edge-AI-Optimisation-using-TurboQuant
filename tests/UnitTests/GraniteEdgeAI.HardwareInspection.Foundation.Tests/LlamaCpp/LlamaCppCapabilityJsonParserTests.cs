using System.Text;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlamaCpp;

[TestClass]
public sealed class LlamaCppCapabilityJsonParserTests
{
    private const string ValidIdentity =
        "{\"schemaVersion\":1," +
        "\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\"," +
        "\"managedPackage\":\"LLamaSharp\"," +
        "\"managedVersion\":\"0.27.0\"," +
        "\"backendPackage\":\"LLamaSharp.Backend.Cpu\"," +
        "\"backendVersion\":\"0.27.0\"," +
        "\"llamaSharpCommit\":\"7cbbc45e421d55794d5050d126e0b96511007007\"," +
        "\"mappedLlamaCppCommit\":\"3f7c29d318e317b63f54c558bc69803963d7d88c\"," +
        "\"runtimeIdentifier\":\"win-x64\"}\n";

    private const string ValidCapabilities =
        "{\"schemaVersion\":1," +
        "\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\"," +
        "\"backends\":[\"cpu\"]," +
        "\"devices\":[{\"ordinal\":0,\"bufferType\":\"CPU\"}]}\n";

    [TestMethod]
    public void IdentityParserAcceptsOnlyTheExactPinnedIdentity()
    {
        LlamaCppIdentityParseResult result =
            LlamaCppCapabilityJsonParser.ParseIdentity(ValidIdentity);

        Assert.IsTrue(result.IsValid);
        Assert.AreSame(LlamaCppRuntimeIdentity.PinnedCpu, result.RuntimeIdentity);
    }

    [TestMethod]
    public void CapabilitiesParserMapsExactCpuAndVisibleDevices()
    {
        LlamaCppCapabilitiesParseResult result =
            LlamaCppCapabilityJsonParser.ParseCapabilities(ValidCapabilities);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(LlamaCppBackend.Cpu, result.Backends.Single());
        Assert.AreEqual(new LlamaCppVisibleDevice(0, "CPU"), result.Devices.Single());
    }

    [TestMethod]
    public void ParsersRejectInvalidFraming()
    {
        string[] invalid =
        [
            string.Empty,
            ValidCapabilities.TrimEnd('\n'),
            ValidCapabilities.TrimEnd('\n') + "\r\n",
            "\ufeff" + ValidCapabilities,
            ValidCapabilities + "\n",
            ValidCapabilities + "{}",
        ];

        foreach (string value in invalid)
        {
            Assert.IsFalse(LlamaCppCapabilityJsonParser.ParseCapabilities(value).IsValid, value);
        }
    }

    [TestMethod]
    public void IdentityParserRejectsSchemaAndIdentityDrift()
    {
        string[] invalid =
        [
            ValidIdentity.Replace("\"schemaVersion\":1,", string.Empty, StringComparison.Ordinal),
            ValidIdentity.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            ValidIdentity.Replace("\"managedVersion\":\"0.27.0\"", "\"managedVersion\":\"9.9.9\"", StringComparison.Ordinal),
            ValidIdentity.Replace("\"managedPackage\"", "\"ManagedPackage\"", StringComparison.Ordinal),
            ValidIdentity.Replace("\"runtimeIdentifier\":\"win-x64\"", "\"runtimeIdentifier\":\"win-arm64\"", StringComparison.Ordinal),
            ValidIdentity.Replace("\"runtimeIdentifier\":", "\"unknown\":true,\"runtimeIdentifier\":", StringComparison.Ordinal),
            ValidIdentity.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal),
        ];

        foreach (string value in invalid)
        {
            Assert.IsFalse(LlamaCppCapabilityJsonParser.ParseIdentity(value).IsValid, value);
        }
    }

    [TestMethod]
    public void CapabilitiesParserRejectsMalformedAndContradictoryFacts()
    {
        string[] invalid =
        [
            "{\"schemaVersion\":1,\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\",\"backends\":[\"cpu\"],\"devices\":[}\n",
            ValidCapabilities.Replace("\"backends\":[\"cpu\"]", "\"backends\":[]", StringComparison.Ordinal),
            ValidCapabilities.Replace("[\"cpu\"]", "[\"cpu\",\"cpu\"]", StringComparison.Ordinal),
            ValidCapabilities.Replace("[\"cpu\"]", "[\"gpu\"]", StringComparison.Ordinal),
            ValidCapabilities.Replace("\"devices\":[{\"ordinal\":0,\"bufferType\":\"CPU\"}]", "\"devices\":[]", StringComparison.Ordinal),
            ValidCapabilities.Replace("\"ordinal\":0", "\"ordinal\":1", StringComparison.Ordinal),
            ValidCapabilities.Replace("\"bufferType\":\"CPU\"", "\"bufferType\":\" leading\"", StringComparison.Ordinal),
            ValidCapabilities.Replace("\"bufferType\":\"CPU\"", "\"bufferType\":\"format\\u202ename\"", StringComparison.Ordinal),
            ValidCapabilities.Replace("\"devices\":", "\"unknown\":true,\"devices\":", StringComparison.Ordinal),
            ValidCapabilities.Replace("\"ordinal\":0", "\"ordinal\":0,\"ordinal\":0", StringComparison.Ordinal),
            ValidCapabilities.Replace("\"devices\":[", "\"devices\":[[[[[[[[[", StringComparison.Ordinal),
        ];

        foreach (string value in invalid)
        {
            Assert.IsFalse(LlamaCppCapabilityJsonParser.ParseCapabilities(value).IsValid, value);
        }
    }

    [TestMethod]
    public void CapabilitiesParserRejectsSeventeenthDeviceBeforeRetention()
    {
        string devices = string.Join(
            ',',
            Enumerable.Range(0, 17).Select(index =>
                $"{{\"ordinal\":{index},\"bufferType\":\"CPU {index}\"}}"));
        string json =
            "{\"schemaVersion\":1," +
            "\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\"," +
            "\"backends\":[\"cpu\"]," +
            $"\"devices\":[{devices}]}}\n";

        LlamaCppCapabilitiesParseResult result =
            LlamaCppCapabilityJsonParser.ParseCapabilities(json);

        Assert.IsFalse(result.IsValid);
        Assert.IsEmpty(result.Backends);
        Assert.IsEmpty(result.Devices);
    }

    [TestMethod]
    public void InvalidCapabilitiesNeverRetainPartiallyParsedFacts()
    {
        string invalid = ValidCapabilities.Replace(
            "}]}",
            "},{\"ordinal\":2,\"bufferType\":\"Attacker supplied\"}]}",
            StringComparison.Ordinal);

        LlamaCppCapabilitiesParseResult result =
            LlamaCppCapabilityJsonParser.ParseCapabilities(invalid);

        Assert.IsFalse(result.IsValid);
        Assert.IsEmpty(result.Backends);
        Assert.IsEmpty(result.Devices);
    }

    [TestMethod]
    public void BoundedMalformedPrimitiveFuzzNeverThrowsOrExpandsResults()
    {
        var random = new Random(230823);
        for (int index = 0; index < 512; index++)
        {
            byte[] bytes = new byte[random.Next(0, 256)];
            random.NextBytes(bytes);
            string value = Encoding.Latin1.GetString(bytes) + "\n";

            LlamaCppCapabilitiesParseResult result =
                LlamaCppCapabilityJsonParser.ParseCapabilities(value);

            Assert.IsLessThanOrEqualTo(8, result.Backends.Count);
            Assert.IsLessThanOrEqualTo(16, result.Devices.Count);
        }
    }
}
