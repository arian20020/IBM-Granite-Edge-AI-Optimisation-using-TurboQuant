using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.Support;

/// <summary>
/// Provides one validated controlled model, published probe, and isolated
/// evidence root to a trusted integration test.
/// </summary>
internal sealed class RealModelTestContext : IDisposable
{
    private static readonly Lazy<Task<ControlledModelConfiguration>>
        ValidatedModel = new(
            LoadAndValidateModelAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly RealModelEvidenceDirectory _evidence = new();

    private RealModelTestContext(
        ControlledModelConfiguration model,
        string publishedProbeDirectory)
    {
        Model = model;
        PublishedProbeDirectory = publishedProbeDirectory;
    }

    internal ControlledModelConfiguration Model { get; }

    internal string PublishedProbeDirectory { get; }

    internal string EvidenceRoot => _evidence.Path;

    internal static async Task<RealModelTestContext> CreateAsync()
    {
        ControlledModelConfiguration model = await ValidatedModel.Value;

        return new RealModelTestContext(
            model,
            PublishedProbeLocation.RequireFromEnvironment());
    }

    internal string CreateEvidencePath(
        string scenario,
        string fileName = "result.json")
    {
        return _evidence.CreateFile(scenario, fileName);
    }

    internal TemporaryProbeSandbox CreateProbeSandbox()
    {
        return TemporaryProbeSandbox.Create(PublishedProbeDirectory);
    }

    public void Dispose()
    {
        _evidence.Dispose();
    }

    private static async Task<ControlledModelConfiguration>
        LoadAndValidateModelAsync()
    {
        string manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            "ControlledModels",
            "granite-4.1-3b-q4-k-m.json");
        ControlledModelConfiguration configuration =
            ControlledModelConfiguration.Load(manifestPath);

        await configuration.ValidateSha256Async(
            CancellationToken.None);

        return configuration;
    }
}
