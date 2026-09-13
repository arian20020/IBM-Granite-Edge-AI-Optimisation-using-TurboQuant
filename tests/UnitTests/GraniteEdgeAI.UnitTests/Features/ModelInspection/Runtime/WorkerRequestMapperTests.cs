using System.Diagnostics;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Runtime;
using GraniteEdgeAI.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime;

[TestClass]
public sealed class WorkerRequestMapperTests
{
    private static readonly DateTimeOffset FixedUtcTime =
        new(2026, 8, 9, 1, 2, 3, TimeSpan.Zero);
    private static readonly string ModelPath = Path.GetFullPath(
        Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!,
            "Models",
            "granite.gguf"));

    [TestMethod]
    public void Map_PreservesValidatedRequestAndAddsExactExecutionIdentity()
    {
        Guid requestId = Guid.Parse("9a6e6875-fb30-4ecb-83e2-f3e73a6a4e96");
        WorkerRequestMapper mapper = new(
            () => requestId,
            () => new ParentProcessIdentity(321, FixedUtcTime));

        WorkerStartInspectionCommand command = mapper.Map(CreateRequest());

        command.Validate();
        Assert.AreEqual(WorkerProtocol.Version, command.ProtocolVersion);
        Assert.AreEqual(WorkerCommandKind.StartInspection, command.CommandType);
        Assert.AreEqual(requestId, command.RequestId);
        Assert.AreEqual(321, command.ParentProcessId);
        Assert.AreEqual(FixedUtcTime, command.ParentProcessStartTimeUtc);
        Assert.AreEqual(ModelPath, command.ModelPath);
        Assert.AreEqual(100, command.ExpectedFileIdentity.LengthBytes);
        Assert.AreEqual(FixedUtcTime, command.ExpectedFileIdentity.LastWriteTimeUtc);
        Assert.AreEqual("GGUF", command.QuickScan.Format);
        Assert.AreEqual("Granite 4.1 3B", command.QuickScan.ModelName);
        Assert.AreEqual("granite", command.QuickScan.Architecture);
        Assert.AreEqual("3B", command.QuickScan.ParameterSizeLabel);
        Assert.AreEqual("Q4_K_M", command.QuickScan.Quantisation);
        Assert.AreEqual(100, command.QuickScan.FileSizeBytes);
        Assert.AreEqual((ulong)131_072, command.QuickScan.DeclaredContextLength);
        Assert.AreEqual((uint)3, command.QuickScan.GgufVersion);
    }

    [TestMethod]
    public void Map_DefaultMapperCreatesFreshIdsForCurrentParentProcess()
    {
        WorkerRequestMapper mapper = new();
        ModelInspectionRequest request = CreateRequest();
        using Process process = Process.GetCurrentProcess();
        DateTimeOffset expectedStartUtc = new(
            process.StartTime.ToUniversalTime(),
            TimeSpan.Zero);

        WorkerStartInspectionCommand first = mapper.Map(request);
        WorkerStartInspectionCommand second = mapper.Map(request);

        Assert.AreNotEqual(Guid.Empty, first.RequestId);
        Assert.AreNotEqual(first.RequestId, second.RequestId);
        Assert.AreEqual(process.Id, first.ParentProcessId);
        Assert.AreEqual(expectedStartUtc, first.ParentProcessStartTimeUtc);
        Assert.AreEqual(process.Id, second.ParentProcessId);
        Assert.AreEqual(expectedStartUtc, second.ParentProcessStartTimeUtc);
    }

    [TestMethod]
    public void Map_NullRequestThrowsBeforeIdentityFactoriesRun()
    {
        int invocationCount = 0;
        WorkerRequestMapper mapper = new(
            () =>
            {
                invocationCount++;
                return Guid.NewGuid();
            },
            () =>
            {
                invocationCount++;
                return new ParentProcessIdentity(321, FixedUtcTime);
            });

        Assert.ThrowsExactly<ArgumentNullException>(() => mapper.Map(null!));
        Assert.AreEqual(0, invocationCount);
    }

    private static ModelInspectionRequest CreateRequest()
    {
        ExpectedModelFileIdentity identity = new(
            lengthBytes: 100,
            lastWriteTimeUtc: FixedUtcTime);
        ValidatedQuickScanSnapshot quickScan =
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite 4.1 3B",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 100,
                declaredContextLength: 131_072,
                ggufVersion: 3);
        return new ModelInspectionRequest(
            ModelPath,
            "granite.gguf",
            identity,
            quickScan);
    }
}
