using HardwareInspection.LlmFitSpike.Candidate;
using HardwareInspection.LlmFitSpike.Command;
using HardwareInspection.LlmFitSpike.Evidence;
using HardwareInspection.LlmFitSpike.Execution;
using HardwareInspection.LlmFitSpike;

using var interruptLifetime = new CancellationTokenSource();
ConsoleCancelEventHandler interruptHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    interruptLifetime.Cancel();
};
Console.CancelKeyPress += interruptHandler;
try
{
    return await SpikeProgram.RunAsync(args, Console.Out, interruptLifetime.Token)
        .ConfigureAwait(false);
}
finally
{
    Console.CancelKeyPress -= interruptHandler;
}

namespace HardwareInspection.LlmFitSpike
{
    internal static class SpikeProgram
    {
        internal static async Task<int> RunAsync(
            string[] arguments,
            TextWriter output,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(output);

            SpikeOptions options;
            try
            {
                options = SpikeOptions.Parse(arguments);
                options.CreateOutputDirectory();
            }
            catch
            {
                WriteStableOutput(output, "Blocked", ["INVALID-COMMAND-LINE"], "none");
                return 2;
            }

            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            LlmFitGate1RunResult result;
            try
            {
                var runner = new LlmFitGate1Runner(
                    new AssemblyCandidateManifestSource(),
                    new CandidateVerifierBoundary(new LlmFitCandidateVerifier()),
                    new ProcessRunnerBoundary(new LlmFitProcessRunner()),
                    new TcpListenerObserverBoundary(),
                    new SystemClock(),
                    new GateOutputStore(),
                    new EvidenceWriterBoundary(new LlmFitGate1EvidenceWriter()));
                result = await runner.RunAsync(options, linkedCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                result = new LlmFitGate1RunResult(
                    LlmFitGate1Disposition.Blocked,
                    string.Empty,
                    [LlmFitGate1DiagnosticCodes.ProcessCancelled]);
            }
            catch
            {
                result = new LlmFitGate1RunResult(
                    LlmFitGate1Disposition.Rejected,
                    string.Empty,
                    [LlmFitGate1DiagnosticCodes.RequiredTestFailure]);
            }

            string evidenceName = string.IsNullOrEmpty(result.EvidencePath)
                ? "none"
                : Path.GetFileName(result.EvidencePath);
            WriteStableOutput(
                output,
                result.Disposition.ToString(),
                result.DiagnosticCodes,
                evidenceName);

            if (result.DiagnosticCodes.Contains(
                    LlmFitGate1DiagnosticCodes.ProcessCancelled,
                    StringComparer.Ordinal))
            {
                return 3;
            }

            return result.Disposition == LlmFitGate1Disposition.FunctionalPassWithPackagingConcern
                ? 0
                : 1;
        }

        private static void WriteStableOutput(
            TextWriter output,
            string disposition,
            IEnumerable<string> diagnostics,
            string evidenceName)
        {
            output.WriteLine("disposition=" + disposition);
            foreach (string diagnostic in diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                output.WriteLine("diagnostic=" + diagnostic);
            }

            output.WriteLine("evidence=" + evidenceName);
        }
    }

    internal sealed class AssemblyCandidateManifestSource : ICandidateManifestSource
    {
        private const string ManifestFileName = "llmfit-v1.1.9-win-x64.json";

        public LlmFitCandidateManifest Load()
        {
            string candidatesDirectory = Path.Combine(AppContext.BaseDirectory, "Candidates");
            string manifestPath = Path.Combine(candidatesDirectory, ManifestFileName);
            FileAttributes directoryAttributes = File.GetAttributes(candidatesDirectory);
            FileAttributes manifestAttributes = File.GetAttributes(manifestPath);
            if (!string.Equals(
                    Path.GetFileName(manifestPath),
                    ManifestFileName,
                    StringComparison.Ordinal) ||
                (directoryAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) !=
                    FileAttributes.Directory ||
                (manifestAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                throw new InvalidDataException("The committed candidate manifest is unavailable.");
            }

            return LlmFitCandidateManifestLoader.Load(manifestPath);
        }
    }

    internal sealed class CandidateVerifierBoundary : ILlmFitCandidateVerifier
    {
        private readonly LlmFitCandidateVerifier _verifier;

        internal CandidateVerifierBoundary(LlmFitCandidateVerifier verifier)
        {
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        }

        public LlmFitCandidateVerification Verify(
            string candidateRoot,
            LlmFitCandidateManifest manifest)
        {
            return _verifier.Verify(candidateRoot, manifest);
        }
    }

    internal sealed class ProcessRunnerBoundary : ILlmFitProcessRunner
    {
        private readonly LlmFitProcessRunner _runner;

        internal ProcessRunnerBoundary(LlmFitProcessRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public Task<LlmFitProcessResult> ExecuteAsync(
            LlmFitCommand command,
            TimeSpan timeout,
            Func<int, CancellationToken, Task>? observer,
            CancellationToken cancellationToken)
        {
            return _runner.ExecuteAsync(command, timeout, observer, cancellationToken);
        }
    }

    internal sealed class TcpListenerObserverBoundary : ILlmFitSocketObserver
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

        public LlmFitSocketObservationSession Create(string candidateImageName)
        {
            var observer = new TcpListenerObserver(candidateImageName, PollInterval);
            return new LlmFitSocketObservationSession(
                observer.ObserveWhileRunningAsync,
                observer.CompleteAsync);
        }
    }

    internal sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    internal sealed class EvidenceWriterBoundary : ILlmFitGate1EvidenceWriter
    {
        private readonly LlmFitGate1EvidenceWriter _writer;

        internal EvidenceWriterBoundary(LlmFitGate1EvidenceWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public Task<string> WriteAsync(
            LlmFitGate1Evidence evidence,
            string outputPath,
            CancellationToken cancellationToken)
        {
            return _writer.WriteAsync(evidence, outputPath, cancellationToken);
        }
    }
}
