using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using System;
using System.Threading;

namespace GraniteEdgeAI.Features.HardwareInspection.Orchestration;

internal enum HardwareToolAcquisitionDiagnosticCode
{
    ToolNotAvailable,
    ToolIntegrityFailure,
    PackagedProbeUnavailable,
}

internal interface IHardwareToolAcquisition
{
    HardwareToolAcquisitionResult Acquire();
}

internal sealed class HardwareToolLease : IDisposable
{
    private readonly VerifiedTrustedTool _llmFit;
    private readonly VerifiedTrustedTool _llamaCpp;
    private int _disposed;

    internal HardwareToolLease(
        VerifiedTrustedTool llmFit,
        VerifiedTrustedTool llamaCpp)
    {
        _llmFit = llmFit ?? throw new ArgumentNullException(nameof(llmFit));
        _llamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
        if (ReferenceEquals(llmFit, llamaCpp))
        {
            throw new ArgumentException("Tool custody objects must be distinct.", nameof(llamaCpp));
        }
    }

    internal VerifiedTrustedTool LlmFit => GetLive(_llmFit);

    internal VerifiedTrustedTool LlamaCpp => GetLive(_llamaCpp);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _llamaCpp.Dispose();
        }
        finally
        {
            _llmFit.Dispose();
        }
    }

    private VerifiedTrustedTool GetLive(VerifiedTrustedTool tool)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return tool;
    }
}

internal sealed class HardwareToolAcquisitionResult
{
    private HardwareToolAcquisitionResult(
        HardwareToolLease? lease,
        HardwareToolAcquisitionDiagnosticCode? diagnostic)
    {
        Lease = lease;
        Diagnostic = diagnostic;
    }

    internal bool IsSuccess => Lease is not null;

    internal HardwareToolLease? Lease { get; }

    internal HardwareToolAcquisitionDiagnosticCode? Diagnostic { get; }

    internal static HardwareToolAcquisitionResult Success(HardwareToolLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new(lease, diagnostic: null);
    }

    internal static HardwareToolAcquisitionResult Failure(
        HardwareToolAcquisitionDiagnosticCode diagnostic)
    {
        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        return new(lease: null, diagnostic);
    }
}
