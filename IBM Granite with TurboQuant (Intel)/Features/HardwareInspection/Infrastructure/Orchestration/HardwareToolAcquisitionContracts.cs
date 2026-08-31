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
    private readonly VerifiedTrustedTool? _llmFit;
    private readonly VerifiedTrustedTool _llamaCpp;
    private int _disposed;

    internal HardwareToolLease(
        VerifiedTrustedTool? llmFit,
        VerifiedTrustedTool llamaCpp,
        HardwareToolAcquisitionDiagnosticCode? llmFitDiagnostic = null)
    {
        _llamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
        if (llmFit is null != llmFitDiagnostic.HasValue)
        {
            throw new ArgumentException(
                "Missing LLM Fit custody requires exactly one closed diagnostic.",
                nameof(llmFitDiagnostic));
        }

        if (llmFitDiagnostic.HasValue && !Enum.IsDefined(llmFitDiagnostic.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(llmFitDiagnostic));
        }

        if (llmFit is not null && ReferenceEquals(llmFit, llamaCpp))
        {
            throw new ArgumentException("Tool custody objects must be distinct.", nameof(llamaCpp));
        }

        _llmFit = llmFit;
        LlmFitDiagnostic = llmFitDiagnostic;
    }

    internal VerifiedTrustedTool? LlmFit => GetLive(_llmFit);

    internal HardwareToolAcquisitionDiagnosticCode? LlmFitDiagnostic { get; }

    internal VerifiedTrustedTool LlamaCpp => GetRequiredLive(_llamaCpp);

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
            _llmFit?.Dispose();
        }
    }

    private T? GetLive<T>(T? value) where T : class
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return value;
    }

    private T GetRequiredLive<T>(T value) where T : class
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return value;
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
