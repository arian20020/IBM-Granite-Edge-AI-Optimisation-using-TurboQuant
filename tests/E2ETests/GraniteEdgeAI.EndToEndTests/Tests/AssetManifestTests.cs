using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class AssetManifestTests
{
    [TestMethod]
    public void Load_accepts_only_digest_length_and_route_metadata()
    {
        using TestDirectory directory = TestDirectory.Create();
        string path = directory.WriteText("assets.json", """
            {
              "schemaVersion": 1,
              "assets": [
                { "id": "gguf-small", "route": "gguf", "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bytes": 42 }
              ]
            }
            """);

        AssetManifest actual = AssetManifest.Load(path);

        Assert.AreEqual(1, actual.Assets.Count);
        Assert.AreEqual("gguf-small", actual.Assets[0].Id);
    }

    [TestMethod]
    public void Load_rejects_path_fields_to_keep_committed_manifests_private()
    {
        using TestDirectory directory = TestDirectory.Create();
        string path = directory.WriteText("assets.json", """
            {
              "schemaVersion": 1,
              "assets": [
                { "id": "gguf-small", "route": "gguf", "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bytes": 42, "path": "C:\\private\\model.gguf" }
              ]
            }
            """);

        Assert.Throws<InvalidDataException>(() => AssetManifest.Load(path));
    }
}
