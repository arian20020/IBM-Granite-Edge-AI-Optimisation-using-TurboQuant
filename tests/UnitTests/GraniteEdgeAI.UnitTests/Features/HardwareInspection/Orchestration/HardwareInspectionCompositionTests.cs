using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[TestCategory("HardwareInspection")]
public sealed class HardwareInspectionCompositionTests
{
    [TestMethod]
    public async Task CreateProduction_BuildsRealGraphWithoutStartingInspectionWork()
    {
        IHardwareInspectionService service = HardwareInspectionComposition.CreateProduction();

        Assert.IsInstanceOfType<HardwareInspectionService>(service);
        Assert.IsInstanceOfType<FixedHardwareToolAcquisition>(ReadField(service, "_toolAcquisition"));
        Assert.IsInstanceOfType<HardwareEvidenceCollectionCoordinator>(ReadField(service, "_evidenceCollection"));
        Assert.IsInstanceOfType<HardwareEvidenceResolver>(ReadField(service, "_evidenceResolver"));

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        HardwareInspectionRunResult result = await service.RunAsync(
            Guid.NewGuid(),
            new Progress<HardwareInspectionRunProgress>(),
            cancellation.Token);

        Assert.AreEqual(HardwareInspectionOutcome.Cancelled, result.Outcome);
    }

    private static object ReadField(object owner, string name)
    {
        FieldInfo? field = owner.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field.GetValue(owner)!;
    }
}
