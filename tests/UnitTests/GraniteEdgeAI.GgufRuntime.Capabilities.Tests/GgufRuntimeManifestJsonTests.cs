using System.Text;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Tests;

[TestClass]
public sealed class GgufRuntimeManifestJsonTests
{
    private const string ValidManifest = """
        {
          "schemaVersion": 1,
          "runtimeBuildId": "cpu-build",
          "runtimeSourceCommit": "0123456789abcdef0123456789abcdef01234567",
          "buildFlags": ["GGML_NATIVE=OFF"],
          "files": [
            {
              "relativePath": "Worker/runtime.exe",
              "length": 42,
              "sha256": "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
              "architecture": "X64",
              "role": "Supervisor",
              "licenseReference": "Cli/LICENSE.txt"
            }
          ]
        }
        """;

    [TestMethod]
    public void DeserializeAcceptsTheVersionedStringEnumShape()
    {
        GgufRuntimeManifest manifest = GgufRuntimeManifestJson.Deserialize(
            Encoding.UTF8.GetBytes(ValidManifest));

        Assert.AreEqual("cpu-build", manifest.RuntimeBuildId);
        Assert.AreEqual(GgufRuntimeFileRole.Supervisor, manifest.Files[0].Role);
    }

    [TestMethod]
    public void DeserializeRejectsUnknownFieldsAndOversizedInput()
    {
        byte[] unknown = Encoding.UTF8.GetBytes(
            ValidManifest.Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 1, \"unexpected\": true,"));

        Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
            GgufRuntimeManifestJson.Deserialize(unknown));
        Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
            GgufRuntimeManifestJson.Deserialize(new byte[(1024 * 1024) + 1]));
    }

    [TestMethod]
    public void DeserializeRejectsDuplicatePropertiesAtEveryDepth()
    {
        byte[] duplicate = Encoding.UTF8.GetBytes(
            ValidManifest.Replace(
                "\"length\": 42,",
                "\"length\": 42, \"length\": 42,",
                StringComparison.Ordinal));

        GgufRuntimeTrustException failure =
            Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
                GgufRuntimeManifestJson.Deserialize(duplicate));

        Assert.AreEqual("runtime-manifest-json-invalid", failure.Code);
    }
}
