using System.Text.Json;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class ProducerEvidenceSetTests
{
    [TestMethod]
    public void Load_accepts_identity_bound_manifests_with_required_stable_kinds()
    {
        using TestDirectory directory = TestDirectory.Create();
        ProducerEvidenceSet evidence = ProducerEvidenceSet.Load(
            Write(directory, "H1", ["hardwareSnapshot", "availableMemory", "safetyBudget"]),
            Write(directory, "M1", ["modelSource", "modelInspectionResult", "modelInspectionHandoff"]),
            Write(directory, "Q1", ["optimizationPlan", "executionResult", "chatTarget", "exportTarget"]));

        Assert.AreEqual("H1", evidence.Hardware.WorkerId);
        Assert.IsTrue(evidence.Optimization.Kinds.Contains("exportTarget"));
    }

    [TestMethod]
    public void Load_rejects_inconsistent_command_arithmetic()
    {
        using TestDirectory directory = TestDirectory.Create();
        string h1 = Write(directory, "H1", ["hardwareSnapshot", "availableMemory", "safetyBudget"], executed: 2);

        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() => ProducerEvidenceSet.Load(
            h1,
            Write(directory, "M1", ["modelSource", "modelInspectionResult", "modelInspectionHandoff"]),
            Write(directory, "Q1", ["optimizationPlan", "executionResult", "chatTarget", "exportTarget"])));
        StringAssert.Contains(error.Message, "arithmetic");
    }

    [TestMethod]
    public void Load_rejects_missing_stable_kind()
    {
        using TestDirectory directory = TestDirectory.Create();
        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() => ProducerEvidenceSet.Load(
            Write(directory, "H1", ["hardwareSnapshot", "availableMemory", "safetyBudget"]),
            Write(directory, "M1", ["modelSource", "modelInspectionResult"]),
            Write(directory, "Q1", ["optimizationPlan", "executionResult", "chatTarget", "exportTarget"])));
        StringAssert.Contains(error.Message, "modelInspectionHandoff");
    }

    private static string Write(TestDirectory directory, string workerId, string[] kinds, int executed = 1)
    {
        object manifest = new
        {
            workerId,
            frozenSourceCommit = AuditIdentity.FrozenCommit,
            evidenceSubjectCommit = new string('a', 40),
            evidenceSubjectTree = new string('b', 40),
            inputs = new[] { new { kind = kinds[0] } },
            outputs = kinds.Skip(1).Select(kind => new { kind }).ToArray(),
            commands = new[] { new { discovered = executed, executed, passed = 1, failed = 0, skipped = 0 } },
        };
        return directory.WriteText($"{workerId}.json", JsonSerializer.Serialize(manifest));
    }
}
