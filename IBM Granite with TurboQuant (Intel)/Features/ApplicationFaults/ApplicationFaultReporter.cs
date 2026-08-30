using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ApplicationFaults;

internal enum ApplicationFaultCode
{
    CompatibilityEvaluationUnexpected,
    GgufChatOperationUnexpected,
    GgufChatRetirementUnexpected,
    GgufChatInitializationCleanupUnexpected,
}

internal enum ApplicationFaultClassification
{
    Argument,
    InvalidOperation,
    NotSupported,
    ObjectDisposed,
    NullReference,
    Unexpected,
}

internal readonly record struct ApplicationFault(
    ApplicationFaultCode Code,
    ApplicationFaultClassification Classification)
{
    internal static ApplicationFault FromException(
        ApplicationFaultCode code,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ApplicationFaultClassification classification = exception switch
        {
            ArgumentException => ApplicationFaultClassification.Argument,
            ObjectDisposedException => ApplicationFaultClassification.ObjectDisposed,
            InvalidOperationException => ApplicationFaultClassification.InvalidOperation,
            NotSupportedException => ApplicationFaultClassification.NotSupported,
            NullReferenceException => ApplicationFaultClassification.NullReference,
            _ => ApplicationFaultClassification.Unexpected,
        };
        return new ApplicationFault(code, classification);
    }
}

internal interface IApplicationFaultReporter
{
    void Report(ApplicationFault fault);
}

/// <summary>
/// Process-safe diagnostic boundary. It retains a bounded set of stable facts,
/// never exception instances, messages, stack traces, or application data.
/// </summary>
internal sealed class BoundedApplicationFaultReporter : IApplicationFaultReporter
{
    private static readonly BoundedApplicationFaultReporter s_shared = new(32);
    private readonly object _sync = new();
    private readonly ApplicationFault[] _faults;
    private int _next;
    private int _count;

    internal BoundedApplicationFaultReporter(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _faults = new ApplicationFault[capacity];
    }

    internal static IApplicationFaultReporter Shared => s_shared;

    public void Report(ApplicationFault fault)
    {
        lock (_sync)
        {
            _faults[_next] = fault;
            _next = (_next + 1) % _faults.Length;
            if (_count < _faults.Length)
            {
                _count++;
            }
        }
    }

    internal IReadOnlyList<ApplicationFault> Capture()
    {
        lock (_sync)
        {
            var result = new ApplicationFault[_count];
            int first = (_next - _count + _faults.Length) % _faults.Length;
            for (int index = 0; index < _count; index++)
            {
                result[index] = _faults[(first + index) % _faults.Length];
            }

            return result;
        }
    }
}
