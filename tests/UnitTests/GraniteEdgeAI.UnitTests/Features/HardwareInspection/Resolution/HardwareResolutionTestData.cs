using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.Features.HardwareInspection.Resolution;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Resolution;

internal static class HardwareResolutionTestData
{
    internal const ulong GiB = 1UL << 30;

    internal static readonly DateTimeOffset Now =
        new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    internal static WindowsSystemSnapshot WindowsSystem(DateTimeOffset? capturedAtUtc = null) =>
        new(
            physicallyInstalledBytes: 32 * GiB,
            osUsablePhysicalBytes: 31 * GiB,
            availablePhysicalBytes: 20 * GiB,
            capturedAtUtc: capturedAtUtc ?? Now,
            operatingSystemName: "Windows 11",
            operatingSystemVersion: "10.0.26100",
            operatingSystemArchitecture: "x64");

    internal static LlmFitHardwareEvidence LlmFit(DateTimeOffset? capturedAtUtc = null) =>
        LlmFitHardwareEvidence.Available(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            capturedAtUtc ?? Now,
            "Intel Core Ultra 7 155H",
            22,
            31,
            20,
            LlmFitGpuDetectionState.Reported,
            [new LlmFitReportedGpu("Intel Arc Graphics", 1)],
            new string('0', 64));

    internal static WindowsProcessorEvidence WindowsProcessor(
        DateTimeOffset? capturedAtUtc = null) =>
        WindowsProcessorEvidence.Available(
            "Intel Core Ultra 7 155H",
            WindowsProcessorArchitecture.X64,
            physicalCoreCount: 16,
            logicalProcessorCount: 22,
            capturedAtUtc ?? Now);

    internal static DxgiGraphicsEvidence Dxgi(DateTimeOffset? capturedAtUtc = null) =>
        DxgiGraphicsEvidence.Available(
            [
                new DxgiAdapterEvidence(
                    "Intel Arc Graphics",
                    DxgiAdapterKind.Hardware,
                    vendorId: 0x8086,
                    deviceId: 0x7D55,
                    dedicatedVideoMemoryBytes: 8 * GiB,
                    dedicatedSystemMemoryBytes: 0,
                    sharedSystemMemoryBytes: 16 * GiB,
                    ordinal: 0),
            ],
            capturedAtUtc ?? Now);

    internal static WindowsStorageEvidence Storage(DateTimeOffset? capturedAtUtc = null) =>
        WindowsStorageEvidence.Available(
            capacityBytes: 1_000_000_000_000,
            availableToCallerBytes: 500_000_000_000,
            capturedAtUtc ?? Now);

    internal static NeuralProcessorEvidence NeuralProcessor(DateTimeOffset? capturedAtUtc = null) =>
        NeuralProcessorEvidence.DetectionUnavailable(
            NeuralProcessorDiagnosticCode.EnumerationMechanismNotApproved,
            capturedAtUtc ?? Now);

    internal static LlamaCppCapabilityEvidence LlamaCpp(DateTimeOffset? capturedAtUtc = null) =>
        LlamaCppCapabilityEvidence.Available(
            LlamaCppRuntimeIdentity.PinnedCpu,
            capturedAtUtc ?? Now,
            [LlamaCppBackend.Cpu],
            [new LlamaCppVisibleDevice(0, "CPU")]);

    internal static CollectedHardwareEvidence CompleteEvidence() =>
        new(
            LlmFit(),
            WindowsProcessor(),
            WindowsSystemEvidenceObservation.Available(WindowsSystem()),
            Storage(),
            Dxgi(),
            NeuralProcessor(),
            LlamaCpp());

    internal sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
