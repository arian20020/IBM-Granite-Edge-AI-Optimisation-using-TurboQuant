namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests;

[TestClass]
public sealed class LlamaCppNativeCapabilityResultTests
{
    [TestMethod]
    public void AvailableRejectsAnUnboundedSequenceAfterInspectingOnlySeventeenDevices()
    {
        GuardedUnboundedDevices devices = new();

        Assert.ThrowsExactly<ArgumentException>(() =>
            LlamaCppNativeCapabilityResult.Available(devices));

        Assert.AreEqual(17, devices.MoveNextCount);
    }

    [TestMethod]
    public void AvailableRejectsANullDeviceAsAContractViolation()
    {
        LlamaCppNativeDevice?[] devices = [new(0, "CPU"), null];

        Assert.ThrowsExactly<ArgumentException>(() =>
            LlamaCppNativeCapabilityResult.Available(devices!));
    }

    private sealed class GuardedUnboundedDevices : IEnumerable<LlamaCppNativeDevice>
    {
        internal int MoveNextCount { get; private set; }

        public IEnumerator<LlamaCppNativeDevice> GetEnumerator()
        {
            for (int ordinal = 0; ; ordinal++)
            {
                MoveNextCount++;
                if (MoveNextCount > 17)
                {
                    throw new InvalidOperationException("The sequence was enumerated beyond the closed bound.");
                }

                yield return new LlamaCppNativeDevice(ordinal, "CPU");
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
