using System.Text.Json.Nodes;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests.Conversion;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoProvenanceTests
{
    [TestMethod]
    public void ExactProvenanceForTheActualPackageSnapshotIsAccepted()
    {
        using PackageFixture package = PackageFixture.Create();
        WriteValidProvenance(package.Root);

        OpenVinoStaticPackageInspectionResult result =
            new OpenVinoStaticPackageInspector().Inspect(package.Root);

        Assert.AreEqual(OpenVinoStaticInspectionStatus.NativeValidationRequired, result.Status);
        Assert.IsNull(result.SupportCode);
    }

    [TestMethod]
    public void TamperedManifestVersionAndPrivacyFieldsAreRejected()
    {
        foreach (Action<JsonObject> tamper in new Action<JsonObject>[]
        {
            root => root["outputManifestSha256"] = new string('d', 64),
            root => root["sourcePath"] = @"C:\\Users\\account\\private-model",
            root => ((JsonObject)root["converterVersions"]!)["transformers"] = "5.5.5"
        })
        {
            using PackageFixture package = PackageFixture.Create();
            WriteValidProvenance(package.Root);
            string path = Path.Combine(package.Root, OpenVinoProvenance.FileName);
            JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            tamper(root);
            File.WriteAllText(path, root.ToJsonString());

            OpenVinoStaticPackageInspectionResult result =
                new OpenVinoStaticPackageInspector().Inspect(package.Root);

            Assert.AreEqual(OpenVinoStaticInspectionStatus.Rejected, result.Status);
            Assert.AreEqual(OpenVinoSupportCode.PackageInconsistentResource, result.SupportCode);
        }
    }

    [TestMethod]
    public void WriterRejectsPathBearingOutputMetadata()
    {
        using PackageFixture package = PackageFixture.Create();
        OpenVinoProvenance valid = WriteValidProvenance(package.Root);
        OpenVinoOutputArtifact privatePath = new(
            @"C:\Users\account\private-model.xml",
            1,
            new string('e', 64));
        OpenVinoProvenance unsafeProvenance = valid with
        {
            OutputFiles = [privatePath],
            OutputManifestSha256 = OpenVinoProvenance.ComputeOutputManifestDigest([privatePath])
        };

        Assert.ThrowsExactly<InvalidDataException>(() =>
            unsafeProvenance.Write(package.Root));
    }

    private static OpenVinoProvenance WriteValidProvenance(string root)
    {
        IReadOnlyList<OpenVinoOutputArtifact> output = OpenVinoProvenance.CaptureOutput(root);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        OpenVinoProvenance provenance = new(
            OpenVinoProvenance.CurrentSchemaVersion,
            Guid.NewGuid(),
            Guid.NewGuid(),
            now,
            now,
            new string('a', 64),
            OpenVinoProvenance.DenseGraniteAllowlist,
            new string('b', 64),
            new string('c', 64),
            new string('d', 64),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nncf"] = "3.3.0",
                ["openvino"] = "2026.3.0",
                ["openvino-genai"] = "2026.3.0.0",
                ["optimum"] = "2.3.0",
                ["optimum-intel"] = "2.1.0",
                ["transformers"] = "5.5.4"
            },
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["library"] = "transformers",
                ["localFilesOnly"] = true,
                ["task"] = "text-generation-with-past",
                ["trustRemoteCode"] = false,
                ["weightFormat"] = "fp16"
            },
            output,
            OpenVinoProvenance.ComputeOutputManifestDigest(output),
            "passed",
            "passed");
        provenance.Write(root);
        return provenance;
    }

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string root) => Root = root;

        internal string Root { get; }

        internal static PackageFixture Create()
        {
            string source = Path.Combine(
                AppContext.BaseDirectory, "TestFixtures", "OpenVINO", "GenAI",
                "TinySyntheticV1", "package");
            string root = Path.Combine(
                Path.GetTempPath(), "ov-provenance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            return new PackageFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
