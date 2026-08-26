using GraniteEdgeAI.Features.ModelOptimization.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationRestartRecoveryTests
{
    [TestMethod]
    public void InvalidJournalCannotPromoteUnreceiptedDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "geai-recovery-test-" + Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(root, "staging");
        string committed = Path.Combine(root, "committed");
        try
        {
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(committed);
            File.WriteAllText(Path.Combine(committed, ".optimization-receipts.v1.json"), "not-json");
            string orphan = Path.Combine(committed, "publication-crash123");
            Directory.CreateDirectory(orphan);
            File.WriteAllText(Path.Combine(orphan, "model.gguf"), "moved-before-receipt");

            var registry = new OptimizationOutputRegistry(staging, committed);

            Assert.AreEqual(0, registry.AdmittedCount);
            Assert.IsFalse(Directory.Exists(orphan));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
