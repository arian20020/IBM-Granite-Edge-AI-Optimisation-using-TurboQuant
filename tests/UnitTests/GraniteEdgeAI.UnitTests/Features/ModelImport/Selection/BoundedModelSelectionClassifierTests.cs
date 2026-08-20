using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class BoundedModelSelectionClassifierTests
{
    [TestMethod]
    public async Task ClassifyAsync_AcceptsOrdinaryGgufFile()
    {
        using var directory = new TemporaryDirectory();
        string file = directory.WriteFile("granite.gguf", "not read by preflight");

        ModelSelectionResult result = await ClassifyAsync(file, isFolder: false);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ModelSelectionRoute.Gguf, result.Route);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsNonGgufFile()
    {
        using var directory = new TemporaryDirectory();
        string file = directory.WriteFile("notes.txt", "unsupported");

        ModelSelectionResult result = await ClassifyAsync(file, isFolder: false);

        Assert.AreEqual("selection-unsupported-file", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_AcceptsExactlyOneStemMatchedOpenVinoPair()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("model.XML", "xml");
        directory.WriteFile("model.bin", "bin");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ModelSelectionRoute.OpenVinoDirectory, result.Route);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsAmbiguousOpenVinoPairs()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("one.xml", "xml");
        directory.WriteFile("one.bin", "bin");
        directory.WriteFile("two.xml", "xml");
        directory.WriteFile("two.bin", "bin");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-ambiguous-folder", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsIncompleteOpenVinoPair()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("model.xml", "xml");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-incomplete-openvino", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsOpenVinoFolderWithACompletePairAndAnOrphan()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("model.xml", "xml");
        directory.WriteFile("model.bin", "bin");
        directory.WriteFile("orphan.xml", "xml");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-incomplete-openvino", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_StopsAfter512DirectChildren()
    {
        using var directory = new TemporaryDirectory();
        for (int index = 0; index <= ModelSelectionLimits.MaximumDirectChildren; index++)
        {
            directory.WriteFile($"entry-{index}.txt", "x");
        }

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-enumeration-limit", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_AcceptsRootOnlySupportedSourceFolder()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"]}");
        directory.WriteFile("model.safetensors", "weights");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ModelSelectionRoute.SourceModelDirectory, result.Route);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsSourceFolderWithCustomCode()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"],\"auto_map\":{\"AutoModel\":\"custom.Model\"}}");
        directory.WriteFile("model.safetensors", "weights");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-custom-code", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsUnsupportedSourceArchitecture()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"bert\",\"architectures\":[\"BertModel\"]}");
        directory.WriteFile("model.safetensors", "weights");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-unsupported-source-model", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsSourceIndexWhoseShardIsMissing()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"]}");
        directory.WriteFile("model.safetensors.index.json", "{\"weight_map\":{\"layer.weight\":\"model-00001-of-00001.safetensors\"}}");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-incomplete-source-model", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsMalformedSourceIndex()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"]}");
        directory.WriteFile("model.safetensors.index.json", "not json");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-invalid-source-model", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_AcceptsSourceIndexWhoseRootShardsExist()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"]}");
        directory.WriteFile("model.safetensors.index.json", "{\"weight_map\":{\"layer.one\":\"model-00001-of-00002.safetensors\",\"layer.two\":\"model-00002-of-00002.safetensors\"}}");
        directory.WriteFile("model-00001-of-00002.safetensors", "weights");
        directory.WriteFile("model-00002-of-00002.safetensors", "weights");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(ModelSelectionRoute.SourceModelDirectory, result.Route);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsMalformedSourceConfiguration()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "not json");
        directory.WriteFile("model.safetensors", "weights");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-invalid-source-model", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsSourceMetadataBeyondTheTotalBudget()
    {
        using var directory = new TemporaryDirectory();
        string padding = new(' ', 140 * 1024);
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"],\"padding\":\"" + padding + "\"}");
        directory.WriteFile("model.safetensors.index.json", "{\"weight_map\":{\"layer.weight\":\"model-00001-of-00001.safetensors\"},\"padding\":\"" + padding + "\"}");
        directory.WriteFile("model-00001-of-00001.safetensors", "weights");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-invalid-source-model", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsNetworkCandidateBeforeOpeningIt()
    {
        ModelSelectionResult result = await ClassifyAsync(@"\\server\share\model.gguf", isFolder: false);

        Assert.AreEqual("selection-network-location", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_MapsCandidateChangedDuringValidation()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"]}");
        directory.WriteFile("model.safetensors", "weights");
        var classifier = new BoundedModelSelectionClassifier(beforeContinuityCheck: () => directory.WriteFile("changed.txt", "changed"));

        ModelSelectionResult result = await classifier.ClassifyAsync(ModelSelectionOperationId.CreateNew(), new ModelSelectionInput(directory.Path, "model", true), CancellationToken.None);

        Assert.AreEqual("model-selection-changed", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsReparseAndOfflineCandidatesBeforeOpeningThem()
    {
        using var directory = new TemporaryDirectory();
        string file = directory.WriteFile("model.gguf", "gguf");
        var reparse = new BoundedModelSelectionClassifier(_ => FileAttributes.ReparsePoint);
        var offline = new BoundedModelSelectionClassifier(_ => FileAttributes.Offline);

        ModelSelectionResult reparseResult = await reparse.ClassifyAsync(ModelSelectionOperationId.CreateNew(), new ModelSelectionInput(file, "model.gguf", false), CancellationToken.None);
        ModelSelectionResult offlineResult = await offline.ClassifyAsync(ModelSelectionOperationId.CreateNew(), new ModelSelectionInput(file, "model.gguf", false), CancellationToken.None);

        Assert.AreEqual("selection-reparse-point", reparseResult.Diagnostic!.Code);
        Assert.AreEqual("selection-not-local", offlineResult.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_MapsElapsedLimitToTimeoutBeforeOpeningCandidate()
    {
        using var directory = new TemporaryDirectory();
        string file = directory.WriteFile("model.gguf", "gguf");
        var classifier = new BoundedModelSelectionClassifier(timeoutReached: () => true);

        ModelSelectionResult result = await classifier.ClassifyAsync(ModelSelectionOperationId.CreateNew(), new ModelSelectionInput(file, "model.gguf", false), CancellationToken.None);

        Assert.AreEqual("selection-timeout", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_MapsTimeoutRaisedDuringFolderEnumeration()
    {
        using var directory = new TemporaryDirectory();
        for (int index = 0; index < 8; index++) directory.WriteFile($"entry-{index}.txt", "x");
        int checks = 0;
        var classifier = new BoundedModelSelectionClassifier(timeoutReached: () => ++checks > 3);

        ModelSelectionResult result = await classifier.ClassifyAsync(ModelSelectionOperationId.CreateNew(), new ModelSelectionInput(directory.Path, "folder", true), CancellationToken.None);

        Assert.AreEqual("selection-timeout", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_MapsTimeoutRaisedDuringSnapshot()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("one.txt", "x");
        int checks = 0;
        var classifier = new BoundedModelSelectionClassifier(timeoutReached: () => ++checks > 2);

        ModelSelectionResult result = await classifier.ClassifyAsync(ModelSelectionOperationId.CreateNew(), new ModelSelectionInput(directory.Path, "folder", true), CancellationToken.None);

        Assert.AreEqual("selection-timeout", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_ObservesCancellationRaisedAfterMetadataRead()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("config.json", "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"]}");
        directory.WriteFile("model.safetensors", "weights");
        using var cancellation = new CancellationTokenSource();
        var classifier = new BoundedModelSelectionClassifier(afterMetadataRead: cancellation.Cancel);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => classifier.ClassifyAsync(ModelSelectionOperationId.CreateNew(), new ModelSelectionInput(directory.Path, "model", true), cancellation.Token));
    }

    [TestMethod]
    public async Task ClassifyAsync_RejectsNestedOnlySourceArtifacts()
    {
        using var directory = new TemporaryDirectory();
        string nested = Directory.CreateDirectory(System.IO.Path.Combine(directory.Path, "nested")).FullName;
        File.WriteAllText(System.IO.Path.Combine(nested, "config.json"), "{\"model_type\":\"granite\",\"architectures\":[\"GraniteForCausalLM\"]}");
        File.WriteAllText(System.IO.Path.Combine(nested, "model.safetensors"), "weights");

        ModelSelectionResult result = await ClassifyAsync(directory.Path, isFolder: true);

        Assert.AreEqual("selection-unsupported-folder", result.Diagnostic!.Code);
    }

    [TestMethod]
    public async Task ClassifyAsync_RespectsCancellation()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("model.gguf", "gguf");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new BoundedModelSelectionClassifier().ClassifyAsync(
                ModelSelectionOperationId.CreateNew(),
                new ModelSelectionInput(directory.Path, "model.gguf", false),
                cancellation.Token));
    }

    private static Task<ModelSelectionResult> ClassifyAsync(string path, bool isFolder)
    {
        return new BoundedModelSelectionClassifier().ClassifyAsync(
            ModelSelectionOperationId.CreateNew(),
            new ModelSelectionInput(path, System.IO.Path.GetFileName(path), isFolder),
            CancellationToken.None);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GraniteEdgeAI", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal string WriteFile(string name, string contents)
        {
            string path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
