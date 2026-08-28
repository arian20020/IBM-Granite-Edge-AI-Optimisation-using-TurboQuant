using System.Diagnostics;

namespace GraniteEdgeAI.EndToEndTests.Automation;

internal static class ConditionWait
{
    internal static bool Until(Func<bool> condition, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (timeout < TimeSpan.Zero || pollInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(timeout < TimeSpan.Zero ? nameof(timeout) : nameof(pollInterval));
        }

        Stopwatch elapsed = Stopwatch.StartNew();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition())
            {
                return true;
            }

            if (pollInterval > TimeSpan.Zero)
            {
                cancellationToken.WaitHandle.WaitOne(pollInterval);
            }
        }
        while (elapsed.Elapsed < timeout);

        return false;
    }
}
