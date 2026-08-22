using Microsoft.Win32.SafeHandles;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Converts a Windows process handle into cancellable asynchronous completion
/// through the thread-pool wait registry. It never occupies a worker thread with
/// a blocking WaitForSingleObject call wrapped in Task.Run.
/// </summary>
internal static class WindowsProcessWaiter
{
    internal static Task WaitForExitAsync(
        SafeProcessHandle process,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ObjectDisposedException.ThrowIf(process.IsClosed, process);
        cancellationToken.ThrowIfCancellationRequested();

        bool addedReference = false;
        process.DangerousAddRef(ref addedReference);
        ManualResetEvent waitHandle = new(initialState: false);

        try
        {
            waitHandle.SafeWaitHandle = new SafeWaitHandle(
                process.DangerousGetHandle(),
                ownsHandle: false);

            TaskCompletionSource completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            RegisteredWaitHandle registration =
                ThreadPool.RegisterWaitForSingleObject(
                    waitHandle,
                    static (state, _) =>
                        ((TaskCompletionSource)state!).TrySetResult(),
                    completion,
                    Timeout.Infinite,
                    executeOnlyOnce: true);
            CancellationTokenRegistration cancellationRegistration =
                cancellationToken.Register(
                    static state =>
                    {
                        CancellationState cancellation =
                            (CancellationState)state!;
                        cancellation.Completion.TrySetCanceled(
                            cancellation.Token);
                    },
                    new CancellationState(completion, cancellationToken));

            return AwaitAndReleaseAsync(
                completion.Task,
                registration,
                cancellationRegistration,
                waitHandle,
                process,
                addedReference);
        }
        catch
        {
            waitHandle.Dispose();
            if (addedReference)
            {
                process.DangerousRelease();
            }

            throw;
        }
    }

    private static async Task AwaitAndReleaseAsync(
        Task completion,
        RegisteredWaitHandle registration,
        CancellationTokenRegistration cancellationRegistration,
        WaitHandle waitHandle,
        SafeProcessHandle process,
        bool addedReference)
    {
        try
        {
            await completion.ConfigureAwait(false);
        }
        finally
        {
            registration.Unregister(waitObject: null);
            cancellationRegistration.Dispose();
            waitHandle.Dispose();
            if (addedReference)
            {
                process.DangerousRelease();
            }
        }
    }

    private sealed record CancellationState(
        TaskCompletionSource Completion,
        CancellationToken Token);
}
