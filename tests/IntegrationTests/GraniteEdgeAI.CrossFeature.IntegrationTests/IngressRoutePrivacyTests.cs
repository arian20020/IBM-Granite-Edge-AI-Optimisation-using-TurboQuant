using GraniteEdgeAI.Features.ModelImport.Selection;

namespace GraniteEdgeAI.CrossFeature.IntegrationTests;

[TestClass]
public sealed class IngressRoutePrivacyTests
{
    [TestMethod]
    public async Task PickerGgufClassificationPublishesRouteWithoutRootedPath()
    {
        // Characterization: the frozen implementation already enforces this
        // boundary. A regression that publishes LocalPath as DisplayName makes
        // the privacy canary fail.
        string root = Path.Combine(
            Path.GetTempPath(),
            "T1-private-user-canary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string modelPath = Path.Combine(root, "granite.gguf");
        await File.WriteAllBytesAsync(modelPath, [0x47, 0x47, 0x55, 0x46]);

        try
        {
            var normalizer = new ModelSelectionInputNormalizer();
            ModelSelectionInput input = normalizer.FromPickerPath(
                modelPath,
                isFolder: false);
            var classifier = new BoundedModelSelectionClassifier();

            ModelSelectionResult result = await classifier.ClassifyAsync(
                ModelSelectionOperationId.CreateNew(),
                input,
                CancellationToken.None);

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(ModelSelectionRoute.Gguf, result.Route);
            Assert.AreEqual("granite.gguf", result.DisplayName);
            Assert.IsFalse(result.DisplayName.Contains(root, StringComparison.Ordinal));
            Assert.IsNull(result.Diagnostic);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task DroppedOpenVinoPackagePublishesRouteWithoutFolderPath()
    {
        // Characterization: picker and drop normalize to the same classifier
        // input, so the route result must carry only the leaf display name.
        string root = CreatePrivateRoot("openvino-package");
        try
        {
            foreach (string stem in new[]
            {
                "openvino_model",
                "openvino_tokenizer",
                "openvino_detokenizer"
            })
            {
                await File.WriteAllTextAsync(Path.Combine(root, stem + ".xml"), "<net />");
                await File.WriteAllBytesAsync(Path.Combine(root, stem + ".bin"), [0x01]);
            }

            ModelSelectionResult result = await ClassifyFolderAsync(root);

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(ModelSelectionRoute.OpenVinoDirectory, result.Route);
            Assert.AreEqual(Path.GetFileName(root), result.DisplayName);
            Assert.IsFalse(result.DisplayName.Contains(
                Path.GetDirectoryName(root)!,
                StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadedSourceFolderPublishesConversionIntentWithoutPath()
    {
        // Characterization: a complete downloaded Granite source package is a
        // distinct conversion route, not evidence that OpenVINO already exists.
        string root = CreatePrivateRoot("downloaded-granite");
        try
        {
            const string configuration =
                "{\"model_type\":\"granite\",\"task\":\"text-generation\"," +
                "\"architectures\":[\"GraniteForCausalLM\"]}";
            await File.WriteAllTextAsync(Path.Combine(root, "config.json"), configuration);
            await File.WriteAllBytesAsync(Path.Combine(root, "model.safetensors"), [0x01]);

            ModelSelectionResult result = await ClassifyFolderAsync(root);

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(ModelSelectionRoute.SourceModelDirectory, result.Route);
            Assert.AreEqual(Path.GetFileName(root), result.DisplayName);
            Assert.IsFalse(result.DisplayName.Contains(
                Path.GetDirectoryName(root)!,
                StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task IncompleteOpenVinoPackageFailsClosedWithoutPathDisclosure()
    {
        string root = CreatePrivateRoot("incomplete-openvino");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "openvino_model.xml"),
                "<net />");

            ModelSelectionResult result = await ClassifyFolderAsync(root);

            Assert.IsFalse(result.IsAccepted);
            Assert.IsNull(result.Route);
            Assert.AreEqual("selection-incomplete-openvino", result.Diagnostic!.Code);
            Assert.IsFalse(result.Diagnostic.Message.Contains(root, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreatePrivateRoot(string suffix)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "T1-private-user-canary-" + suffix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<ModelSelectionResult> ClassifyFolderAsync(string root)
    {
        var normalizer = new ModelSelectionInputNormalizer();
        ModelSelectionInput input = normalizer.FromPickerPath(root, isFolder: true);
        var classifier = new BoundedModelSelectionClassifier();
        return await classifier.ClassifyAsync(
            ModelSelectionOperationId.CreateNew(),
            input,
            CancellationToken.None);
    }
}
