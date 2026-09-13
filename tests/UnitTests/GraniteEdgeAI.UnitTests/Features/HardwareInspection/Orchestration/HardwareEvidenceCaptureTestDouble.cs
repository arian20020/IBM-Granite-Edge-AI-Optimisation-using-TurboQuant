using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

internal sealed class HardwareEvidenceCaptureTestDouble : IHardwareEvidenceCapture
{
    internal static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 24, 2, 3, 4, TimeSpan.Zero);
    private readonly object _sync = new();
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _fail = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _changed = NewSignal();
    private int _activeNative;
    private int _activeExternal;

    internal HardwareEvidenceCaptureTestDouble()
    {
        Processor = WindowsProcessorEvidence.Unavailable(
            WindowsProcessorDiagnosticCode.NativeApiUnavailable, CapturedAtUtc);
        System = new(4, 3, 2, CapturedAtUtc, "Windows", "1", "X64");
        Storage = WindowsStorageEvidence.Unavailable(
            WindowsStorageDiagnosticCode.NativeApiUnavailable, CapturedAtUtc);
        Graphics = DxgiGraphicsEvidence.Unavailable(
            DxgiGraphicsDiagnosticCode.NativeApiUnavailable, CapturedAtUtc);
        NeuralProcessor = NeuralProcessorEvidence.NotPresent(CapturedAtUtc);
        LlmFit = LlmFitHardwareEvidence.Unavailable(
            "llmfit", "1.1.9", CapturedAtUtc, LlmFitDiagnosticCode.VersionStartFailed);
        LlamaCpp = LlamaCppCapabilityEvidence.Unavailable(
            CapturedAtUtc, LlamaCppCapabilityDiagnosticCode.IdentityStartFailed);
    }

    internal bool Hold { get; set; }
    internal bool FailFirstNative { get; set; }
    internal bool FailingProviderStarted { get; private set; }
    internal string? FailingProvider { get; set; }
    internal WindowsSystemSnapshotDiagnosticCode? SystemFailure { get; set; }
    internal int NativeStarted { get; private set; }
    internal int ExternalStarted { get; private set; }
    internal int Completed { get; private set; }
    internal int MaximumNative { get; private set; }
    internal int MaximumExternal { get; private set; }
    internal bool LanesOverlapped { get; private set; }
    internal int[] Calls { get; } = new int[7];
    internal WindowsProcessorEvidence Processor { get; }
    internal WindowsSystemSnapshot System { get; }
    internal WindowsStorageEvidence Storage { get; }
    internal DxgiGraphicsEvidence Graphics { get; }
    internal NeuralProcessorEvidence NeuralProcessor { get; }
    internal LlmFitHardwareEvidence LlmFit { get; }
    internal LlamaCppCapabilityEvidence LlamaCpp { get; }

    public async ValueTask<WindowsProcessorEvidence> CaptureProcessorAsync(CancellationToken token) =>
        await RunNativeAsync("processor", 0, Processor, token);

    public async ValueTask<WindowsSystemSnapshot> CaptureSystemAsync(CancellationToken token)
    {
        if (SystemFailure is WindowsSystemSnapshotDiagnosticCode diagnostic)
        {
            Calls[1]++;
            throw new WindowsSystemSnapshotException(diagnostic switch
            {
                WindowsSystemSnapshotDiagnosticCode.MemoryUnavailable =>
                    WindowsSystemSnapshotProvider.MemoryUnavailableCode,
                WindowsSystemSnapshotDiagnosticCode.MemoryOverflow =>
                    WindowsSystemSnapshotProvider.MemoryOverflowCode,
                WindowsSystemSnapshotDiagnosticCode.MemoryInconsistent =>
                    WindowsSystemSnapshotProvider.MemoryInconsistentCode,
                _ => throw new ArgumentOutOfRangeException(nameof(diagnostic)),
            });
        }

        return await RunNativeAsync("system", 1, System, token);
    }

    public async ValueTask<WindowsStorageEvidence> CaptureStorageAsync(CancellationToken token) =>
        await RunNativeAsync("storage", 2, Storage, token);

    public async ValueTask<DxgiGraphicsEvidence> CaptureGraphicsAsync(CancellationToken token) =>
        await RunNativeAsync("graphics", 3, Graphics, token);

    public async ValueTask<NeuralProcessorEvidence> CaptureNeuralProcessorAsync(CancellationToken token) =>
        await RunNativeAsync("neural", 4, NeuralProcessor, token);

    public Task<LlmFitHardwareEvidence> CaptureLlmFitAsync(VerifiedTrustedTool tool, CancellationToken token) =>
        RunExternalAsync("llmfit", 5, LlmFit, token);

    public Task<LlamaCppCapabilityEvidence> CaptureLlamaCppAsync(VerifiedTrustedTool tool, CancellationToken token) =>
        RunExternalAsync("llamacpp", 6, LlamaCpp, token);

    internal void Release() => _release.TrySetResult();
    internal void Fail() => _fail.TrySetResult();

    internal async Task WaitForStartsAsync(int native, int external)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        while (true)
        {
            Task signal;
            lock (_sync)
            {
                if (NativeStarted >= native && ExternalStarted >= external)
                {
                    return;
                }

                signal = _changed.Task;
            }

            await signal.WaitAsync(timeout.Token);
        }
    }

    private async Task<T> RunNativeAsync<T>(string name, int index, T evidence, CancellationToken token)
    {
        lock (_sync)
        {
            if (FailFirstNative) FailingProvider ??= name;
        }
        Enter(native: true, index);
        lock (_sync)
        {
            if (string.Equals(FailingProvider, name, StringComparison.Ordinal))
                FailingProviderStarted = true;
        }
        try
        {
            await WaitAsync(name, token);
            return evidence;
        }
        finally
        {
            Exit(native: true);
        }
    }

    private async Task<T> RunExternalAsync<T>(string name, int index, T evidence, CancellationToken token)
    {
        Enter(native: false, index);
        try
        {
            await WaitAsync(name, token);
            return evidence;
        }
        finally
        {
            Exit(native: false);
        }
    }

    private async Task WaitAsync(string name, CancellationToken token)
    {
        if (string.Equals(FailingProvider, name, StringComparison.Ordinal))
        {
            await _fail.Task.WaitAsync(token);
            throw new InvalidOperationException("private provider text");
        }

        if (Hold)
        {
            await _release.Task.WaitAsync(token);
        }
    }

    private void Enter(bool native, int index)
    {
        lock (_sync)
        {
            Calls[index]++;
            if (native)
            {
                NativeStarted++;
                MaximumNative = Math.Max(MaximumNative, ++_activeNative);
            }
            else
            {
                ExternalStarted++;
                MaximumExternal = Math.Max(MaximumExternal, ++_activeExternal);
            }

            LanesOverlapped |= _activeNative > 0 && _activeExternal > 0;
            Pulse();
        }
    }

    private void Exit(bool native)
    {
        lock (_sync)
        {
            if (native) _activeNative--; else _activeExternal--;
            Completed++;
            Pulse();
        }
    }

    private void Pulse()
    {
        _changed.TrySetResult();
        _changed = NewSignal();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
