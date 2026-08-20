using System.Text.Json;
using System.Text.Json.Serialization;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Tests;

[TestClass]
public sealed class GgufRuntimePackageLoaderTests
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    [TestMethod]
    public void VerifyRequiresDetachedManifestToMatchTrustedBytes()
    {
        using var package =
            GgufRuntimeManifestVerifierTests.TemporaryRuntimePackage.Create();
        byte[] trusted = Serialize(package.Manifest);
        string detached = Path.Combine(package.Root, "runtime-manifest.json");
        File.WriteAllBytes(detached, trusted);

        VerifiedGgufRuntimePackage verified = GgufRuntimePackageLoader.Verify(
            package.Root,
            trusted,
            detached);

        Assert.AreEqual(package.CliPath, verified.CliExecutable);

        File.AppendAllText(detached, " ");
        GgufRuntimeTrustException exception =
            Assert.ThrowsExactly<GgufRuntimeTrustException>(() =>
                GgufRuntimePackageLoader.Verify(package.Root, trusted, detached));
        Assert.AreEqual("runtime-manifest-copy-mismatch", exception.Code);
    }

    private static byte[] Serialize(GgufRuntimeManifest manifest)
        => JsonSerializer.SerializeToUtf8Bytes(manifest, SerializerOptions);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
