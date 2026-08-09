using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;

namespace GraniteEdgeAI.Features.ModelInspection.Runtime;

/// <summary>
/// Executes one short-lived protected worker and exposes only application-owned
/// progress and terminal evidence to the Model Inspection use case.
/// </summary>
internal sealed class WorkerProcessLlamaModelProbe : ILlamaModelProbe
{
    private readonly IInspectionWorkerClient _workerClient;
    private readonly WorkerRequestMapper _requestMapper;
    private readonly WorkerResultMapper _resultMapper;

    internal WorkerProcessLlamaModelProbe(
        IInspectionWorkerClient workerClient)
        : this(
            workerClient,
            new WorkerRequestMapper(),
            new WorkerResultMapper())
    {
    }

    internal WorkerProcessLlamaModelProbe(
        IInspectionWorkerClient workerClient,
        WorkerRequestMapper requestMapper,
        WorkerResultMapper resultMapper)
    {
        _workerClient = workerClient ??
            throw new ArgumentNullException(nameof(workerClient));
        _requestMapper = requestMapper ??
            throw new ArgumentNullException(nameof(requestMapper));
        _resultMapper = resultMapper ??
            throw new ArgumentNullException(nameof(resultMapper));
    }

    public async Task<ModelInspectionProbeResult> InspectAsync(
        ModelInspectionRequest request,
        IProgress<ModelInspectionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            WorkerStartInspectionCommand command = _requestMapper.Map(request);
            IProgress<WorkerProgressMessage>? workerProgress = progress is null
                ? null
                : new MappedWorkerProgress(
                    command.RequestId,
                    _resultMapper,
                    progress);
            WorkerClientResult result = await _workerClient.ExecuteAsync(
                    command,
                    workerProgress,
                    cancellationToken)
                .ConfigureAwait(false);
            return _resultMapper.MapResult(
                request,
                command.RequestId,
                result);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (IsRecoverableBoundaryFailure(error))
        {
            return _resultMapper.MapClientFailure();
        }
    }

    private static bool IsRecoverableBoundaryFailure(Exception error)
    {
        return error is ArgumentException or
            InvalidOperationException or
            InvalidDataException or
            IOException or
            UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or
            WorkerProtocolException or
            OverflowException;
    }

    private sealed class MappedWorkerProgress :
        IProgress<WorkerProgressMessage>
    {
        private readonly Guid _expectedRequestId;
        private readonly WorkerResultMapper _mapper;
        private readonly IProgress<ModelInspectionProgress> _target;

        internal MappedWorkerProgress(
            Guid expectedRequestId,
            WorkerResultMapper mapper,
            IProgress<ModelInspectionProgress> target)
        {
            _expectedRequestId = expectedRequestId;
            _mapper = mapper;
            _target = target;
        }

        public void Report(WorkerProgressMessage value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.RequestId != _expectedRequestId)
            {
                throw new InvalidDataException(
                    "Worker progress did not match the active request.");
            }

            _target.Report(_mapper.MapProgress(value));
        }
    }
}
