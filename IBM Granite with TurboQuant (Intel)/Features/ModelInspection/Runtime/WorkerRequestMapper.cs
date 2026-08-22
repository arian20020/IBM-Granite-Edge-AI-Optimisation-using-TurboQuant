using System;
using System.Diagnostics;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.Features.ModelInspection.Runtime;

/// <summary>
/// Captures the process identity that lets the protected worker verify its
/// single owning application process.
/// </summary>
internal sealed record ParentProcessIdentity(
    int ProcessId,
    DateTimeOffset StartTimeUtc);

/// <summary>
/// Maps the application-owned request into one fresh worker command without
/// allowing transport types into the rest of the application.
/// </summary>
internal sealed class WorkerRequestMapper
{
    private readonly Func<Guid> _requestIdFactory;
    private readonly Func<ParentProcessIdentity> _parentIdentityProvider;

    internal WorkerRequestMapper()
        : this(Guid.NewGuid, CaptureCurrentParentIdentity)
    {
    }

    internal WorkerRequestMapper(
        Func<Guid> requestIdFactory,
        Func<ParentProcessIdentity> parentIdentityProvider)
    {
        _requestIdFactory = requestIdFactory ??
            throw new ArgumentNullException(nameof(requestIdFactory));
        _parentIdentityProvider = parentIdentityProvider ??
            throw new ArgumentNullException(nameof(parentIdentityProvider));
    }

    internal WorkerStartInspectionCommand Map(ModelInspectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ParentProcessIdentity parent = _parentIdentityProvider();
        WorkerStartInspectionCommand command = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = _requestIdFactory(),
            ParentProcessId = parent.ProcessId,
            ParentProcessStartTimeUtc = parent.StartTimeUtc,
            ModelPath = request.ModelPath,
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = request.ExpectedFileIdentity.LengthBytes,
                LastWriteTimeUtc =
                    request.ExpectedFileIdentity.LastWriteTimeUtc
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = request.QuickScan.Format,
                ModelName = request.QuickScan.ModelName,
                Architecture = request.QuickScan.Architecture,
                ParameterSizeLabel = request.QuickScan.ParameterSizeLabel,
                Quantisation = request.QuickScan.Quantisation,
                FileSizeBytes = request.QuickScan.FileSizeBytes,
                DeclaredContextLength =
                    request.QuickScan.DeclaredContextLength,
                GgufVersion = request.QuickScan.GgufVersion
            }
        };

        command.Validate();
        return command;
    }

    private static ParentProcessIdentity CaptureCurrentParentIdentity()
    {
        using Process process = Process.GetCurrentProcess();
        return new ParentProcessIdentity(
            process.Id,
            new DateTimeOffset(
                process.StartTime.ToUniversalTime(),
                TimeSpan.Zero));
    }
}
