using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Protects the exact managed/native runtime decision recorded by ADR-001.
/// </summary>
[TestClass]
public sealed class PinnedApplicationRuntimeTests
{
    /// <summary>
    /// Ensures that the application pair remains on the approved matching
    /// LLamaSharp and CPU-backend versions.
    /// </summary>
    [TestMethod]
    public void SelectedApplicationRuntime_UsesApprovedMatchedPair()
    {
        Assert.AreEqual(
            "LLamaSharp",
            PinnedApplicationRuntime.ManagedPackageName);
        Assert.AreEqual(
            "0.27.0",
            PinnedApplicationRuntime.ManagedPackageVersion);
        Assert.AreEqual(
            "LLamaSharp.Backend.Cpu",
            PinnedApplicationRuntime.BackendPackageName);
        Assert.AreEqual(
            "0.27.0",
            PinnedApplicationRuntime.BackendPackageVersion);
        Assert.AreEqual(
            "3f7c29d318e317b63f54c558bc69803963d7d88c",
            PinnedApplicationRuntime.ExpectedLlamaCppCommit);
        Assert.AreEqual(
            "win-x64",
            PinnedApplicationRuntime.IntendedProductionRuntimeIdentifier);
    }

    /// <summary>
    /// Prevents the research runtime from being accidentally described as the
    /// embedded application runtime.
    /// </summary>
    [TestMethod]
    public void ResearchAndApplicationRuntimes_RemainExplicitlyDifferent()
    {
        Assert.AreEqual(
            "b9870",
            PinnedApplicationRuntime.ResearchRuntimeTag);
        Assert.AreEqual(
            "2d973636e292ee6f75fadcf08d29cb33511f509f",
            PinnedApplicationRuntime.ResearchRuntimeCommit);
        Assert.AreNotEqual(
            PinnedApplicationRuntime.ResearchRuntimeCommit,
            PinnedApplicationRuntime.ExpectedLlamaCppCommit);
    }
}
