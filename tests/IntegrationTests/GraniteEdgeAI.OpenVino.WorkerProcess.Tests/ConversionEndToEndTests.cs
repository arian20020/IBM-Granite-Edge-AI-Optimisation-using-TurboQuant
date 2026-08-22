using System.Security.Cryptography;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("OpenVinoRoute")]
[TestCategory("OfficialNative")]
[DoNotParallelize]
public sealed class ConversionEndToEndTests
{
    [TestMethod]
    [Timeout(30_000)]
    public async Task CancellationInterruptsLargeClosureVerificationBeforeWorkerLaunch()
    {
        string converterStage = RequireStage("GRANITE_OPENVINO_CONVERTER_STAGE");
        string officialStage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        SealedOpenVinoConversionPipeline pipeline = new(
            converterStage,
            Digest(Path.Combine(converterStage, "converter-manifest.json")),
            CreateRoute(officialStage));
        Guid operationId = Guid.NewGuid();
        string root = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-ClosureCancel-" + operationId.ToString("N"));
        string source = Path.Combine(root, "source");
        string staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(staging);
        CopySourceFixture(source);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));
        try
        {
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                pipeline.ConvertAsync(
                    new OpenVinoConverterInvocation(
                        operationId,
                        source,
                        staging,
                        new string('a', 64)),
                    cancellation.Token));
            Assert.AreEqual(0, Directory.EnumerateFiles(staging).Count());
            Assert.IsFalse(Directory.Exists(Path.Combine(
                Path.GetTempPath(),
                "GraniteEdgeAI-ConverterScratch-" + operationId.ToString("N"))));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [Timeout(600_000)]
    public async Task ProtectedOfflineConversionPublishesAnIndependentlyReadyPackage()
    {
        string converterStage = RequireStage("GRANITE_OPENVINO_CONVERTER_STAGE");
        string officialStage = RequireStage("OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string converterManifest = Path.Combine(converterStage, "converter-manifest.json");
        string converterManifestDigest = Digest(converterManifest);
        OpenVinoRouteService route = CreateRoute(officialStage);
        SealedOpenVinoConversionPipeline pipeline = new(
            converterStage,
            converterManifestDigest,
            route);
        OpenVinoConversionService service = new(pipeline, _ => true);
        string operationRoot = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-ConversionE2E-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(operationRoot, "source");
        string output = Path.Combine(operationRoot, "output");
        string destination = Path.Combine(output, "converted");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(output);
        CopySourceFixture(source);
        Dictionary<string, string> before = Capture(source);
        List<OpenVinoConversionStage> stages = [];
        try
        {
            OpenVinoConversionResult result = await service.ConvertAsync(
                new OpenVinoConversionRequest(source, destination, Confirmed: true),
                new InlineProgress<OpenVinoConversionProgress>(value => stages.Add(value.Stage)),
                CancellationToken.None);

            Assert.AreEqual(OpenVinoConversionStatus.Published, result.Status,
                result.SupportCode?.ToProtocolValue() + "; stages=" + string.Join(',', stages));
            Assert.IsNull(result.SupportCode);
            CollectionAssert.AreEquivalent(before, Capture(source));
            Assert.IsTrue(File.Exists(Path.Combine(
                destination, OpenVinoProvenance.FileName)));
            OpenVinoRouteInspectionResult independent = await route.InspectAsync(
                destination,
                CancellationToken.None);
            try
            {
                Assert.IsTrue(independent.Outcome is OpenVinoRouteInspectionOutcome.Ready or
                    OpenVinoRouteInspectionOutcome.ReadyWithWarnings,
                    independent.Failure?.SupportCode);
                Assert.IsNotNull(independent.Handoff);
                Assert.AreNotEqual(result.InspectionRunId,
                    independent.Handoff.ModelInspectionRunId);
            }
            finally
            {
                independent.HandoffLease?.Dispose();
            }
            Assert.AreEqual(0, Directory.EnumerateDirectories(
                output, ".granite-openvino-*.staging").Count());
        }
        finally
        {
            if (Directory.Exists(operationRoot)) Directory.Delete(operationRoot, recursive: true);
        }
    }

    private static OpenVinoRouteService CreateRoute(string stage)
    {
        string manifestDigest = Digest(Path.Combine(stage, "worker-manifest.json"));
        OpenVinoBuildEvidence evidence = new(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            manifestDigest);
        Dictionary<string, OpenVinoWorkerBinaryMachine> machines =
            new(StringComparer.Ordinal)
            {
                ["OpenVinoOfficial.Worker.exe"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_genai.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_cpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_gpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_ir_frontend.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_tokenizers.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbb12.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbbbind_2_5.dll"] = OpenVinoWorkerBinaryMachine.Amd64
            };
        OpenVinoWorkerInstallation installation = new(
            stage,
            "OpenVinoOfficial.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            evidence,
            machines);
        return new OpenVinoRouteService(
            new OpenVinoWorkerClient(OpenVinoWorkerClientOptions.CreateDefault(installation)),
            evidence);
    }

    private static void CopySourceFixture(string destination)
    {
        string source = Path.Combine(
            FindRepositoryRoot(), "tests", "TestFixtures", "OpenVINO", "Converter",
            "TinyGraniteV1", "source");
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
    }

    private static Dictionary<string, string> Capture(string root) =>
        Directory.EnumerateFiles(root)
            .ToDictionary(
                path => Path.GetFileName(path) ??
                    throw new InvalidDataException("A source file name is missing."),
                Digest,
                StringComparer.Ordinal);

    private static string Digest(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string RequireStage(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Inconclusive(name + " is required.");
        return Path.GetFullPath(value!);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
