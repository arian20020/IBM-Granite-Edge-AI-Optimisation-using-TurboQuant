using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies diagnostic cancellation timers are valid and mutually exclusive
/// before file or native work begins.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class VocabOnlyProbeCancellationConfigurationTests
{
    [TestMethod]
    public async Task RunAsync_WithNonPositivePostPreflightDelay_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await new VocabOnlyModelProbe().RunAsync(
                "missing.gguf",
                cancelAfterPreflightMilliseconds: 0,
                cancelNativeAfterMilliseconds: null,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_WithNonPositiveNativeDelay_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            async () => await new VocabOnlyModelProbe().RunAsync(
                "missing.gguf",
                cancelAfterPreflightMilliseconds: null,
                cancelNativeAfterMilliseconds: -1,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_WithBothTimers_ThrowsMutualExclusionError()
    {
        ArgumentException exception =
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                async () => await new VocabOnlyModelProbe().RunAsync(
                    "missing.gguf",
                    cancelAfterPreflightMilliseconds: 1,
                    cancelNativeAfterMilliseconds: 1,
                    CancellationToken.None));

        StringAssert.Contains(exception.Message, "mutually exclusive");
    }
}
