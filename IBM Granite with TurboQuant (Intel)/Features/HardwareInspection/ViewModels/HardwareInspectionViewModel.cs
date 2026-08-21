using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.HardwareInspection.ViewModels;

public sealed class HardwareInspectionViewModel
{
    private readonly object _gate = new();
    private readonly IHardwareInspectionService _service;
    private readonly IHardwareInspectionStagePacer _pacer;
    private readonly HardwareInspectionPresentationFactory _presentationFactory;
    private RunOwner? _current;
    private long _revision;
    private long _attemptGeneration;
    private Guid? _nextInspectionId;

    public HardwareInspectionViewModel(
        IHardwareInspectionService service,
        IHardwareInspectionStagePacer? pacer = null,
        HardwareInspectionPresentationFactory? presentationFactory = null)
        : this(service, initialInspectionId: null, pacer, presentationFactory)
    {
    }

    internal HardwareInspectionViewModel(
        IHardwareInspectionService service,
        Guid initialInspectionId,
        IHardwareInspectionStagePacer? pacer = null,
        HardwareInspectionPresentationFactory? presentationFactory = null)
        : this(service, (Guid?)initialInspectionId, pacer, presentationFactory)
    {
    }

    private HardwareInspectionViewModel(
        IHardwareInspectionService service,
        Guid? initialInspectionId,
        IHardwareInspectionStagePacer? pacer,
        HardwareInspectionPresentationFactory? presentationFactory)
    {
        if (initialInspectionId is Guid configuredId && !IsUuidV4(configuredId))
        {
            throw new ArgumentException(
                "Initial Hardware run identity must be UUID version 4.",
                nameof(initialInspectionId));
        }

        _service = service ?? throw new ArgumentNullException(nameof(service));
        _pacer = pacer ?? new HardwareInspectionStagePacer();
        _presentationFactory = presentationFactory
            ?? new HardwareInspectionPresentationFactory();
        InitialInspectionId = initialInspectionId ?? Guid.Empty;
        _nextInspectionId = initialInspectionId;
        Snapshot = new HardwareInspectionViewState(
            0,
            0,
            null,
            false,
            _presentationFactory.CreateInvalidHandoff(),
            null,
            null,
            null,
            null);
    }

    public event EventHandler? SnapshotChanged;

    internal Guid InitialInspectionId { get; }

    public HardwareInspectionViewState Snapshot { get; private set; }

    public Task ActivateAsync()
    {
        lock (_gate)
        {
            if (_current is { IsActive: true } active)
            {
                return active.RunTask!;
            }

            return StartRunLocked();
        }
    }

    public Task RetryAsync()
    {
        lock (_gate)
        {
            RetireCurrentLocked(publishInactiveState: false);
            return StartRunLocked();
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (_current is not { IsActive: true, CancellationRequested: false } owner)
            {
                return;
            }

            owner.CancellationRequested = true;
            PublishLocked(
                owner,
                isRunActive: true,
                _presentationFactory.CreateStopping(),
                null,
                null,
                null,
                null);
            owner.Progress.Writer.TryComplete();
            owner.Cancellation.Cancel();
        }
    }

    public void Deactivate()
    {
        lock (_gate)
        {
            RetireCurrentLocked(publishInactiveState: true);
        }
    }

    private Task StartRunLocked()
    {
        Guid inspectionId = _nextInspectionId ?? Guid.NewGuid();
        _nextInspectionId = null;
        RunOwner owner = new(inspectionId, ++_attemptGeneration);
        _current = owner;
        PublishLocked(
            owner,
            isRunActive: true,
            _presentationFactory.CreateActive(
                HardwareInspectionStage.StartingHardwareInspection),
            null,
            null,
            null,
            null);
        owner.RunTask = RunCoreAsync(owner);
        return owner.RunTask;
    }

    private async Task RunCoreAsync(RunOwner owner)
    {
        Task progressPump = PumpProgressAsync(owner);
        HardwareInspectionRunResult result;
        try
        {
            result = await _service.RunAsync(
                owner.InspectionId,
                new InlineProgress<HardwareInspectionRunProgress>(
                    progress => AcceptProgress(owner, progress)),
                owner.Cancellation.Token);
        }
        catch (OperationCanceledException) when (owner.CancellationRequested
            || owner.Cancellation.IsCancellationRequested)
        {
            result = HardwareInspectionRunResult.CreateCancelled(owner.InspectionId);
        }
        catch (Exception)
        {
            result = HardwareInspectionRunResult.CreateFailed(
                owner.InspectionId,
                HardwareInspectionFailureKind.TransientOperation,
                "HI-OPERATION-FAILED");
        }

        owner.Progress.Writer.TryComplete();
        try
        {
            await progressPump;
        }
        catch (OperationCanceledException) when (owner.CancellationRequested
            || !owner.IsActive)
        {
        }

        try
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_current, owner) || !owner.IsActive)
                {
                    return;
                }

                if (result.InspectionId != owner.InspectionId)
                {
                    return;
                }

                if (owner.CancellationRequested
                    && result.Outcome != HardwareInspectionOutcome.Cancelled)
                {
                    return;
                }

                if (result.Outcome is HardwareInspectionOutcome.Completed
                        or HardwareInspectionOutcome.CompletedWithWarnings
                    && owner.LastReceivedStage
                        != (int)HardwareInspectionRunStage.CreatingHardwareReport)
                {
                    result = HardwareInspectionRunResult.CreateFailed(
                        owner.InspectionId,
                        HardwareInspectionFailureKind.ApplicationRepairRequired,
                        "HI-STAGE-SEQUENCE-INCOMPLETE");
                }

                PublishTerminalLocked(owner, result);
                owner.IsActive = false;
            }
        }
        finally
        {
            owner.Cancellation.Dispose();
        }
    }

    private void AcceptProgress(
        RunOwner owner,
        HardwareInspectionRunProgress progress)
    {
        lock (_gate)
        {
            int stageIndex = (int)progress.Stage;
            if (!ReferenceEquals(_current, owner)
                || !owner.IsActive
                || owner.CancellationRequested
                || progress.InspectionId != owner.InspectionId
                || progress.Sequence <= owner.LastReceivedSequence
                || stageIndex != owner.LastReceivedStage + 1)
            {
                return;
            }

            owner.LastReceivedSequence = progress.Sequence;
            owner.LastReceivedStage = stageIndex;
            owner.Progress.Writer.TryWrite(progress);
        }
    }

    private async Task PumpProgressAsync(RunOwner owner)
    {
        await foreach (HardwareInspectionRunProgress progress in
                       owner.Progress.Reader.ReadAllAsync(owner.Cancellation.Token))
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_current, owner)
                    || !owner.IsActive
                    || owner.CancellationRequested)
                {
                    return;
                }

                owner.LastDisplayedStage = (int)progress.Stage;
                HardwareInspectionStage stage = MapStage(progress.Stage);
                if (Snapshot.Presentation.Kind != HardwareInspectionPresentationKind.Active
                    || Snapshot.Presentation.Title
                        != HardwareInspectionCopyCatalog.Stage(stage).Title)
                {
                    PublishLocked(
                        owner,
                        isRunActive: true,
                        _presentationFactory.CreateActive(stage),
                        null,
                        null,
                        null,
                        null);
                }
            }

            await _pacer.WaitAsync(owner.Cancellation.Token);
        }
    }

    private void PublishTerminalLocked(
        RunOwner owner,
        HardwareInspectionRunResult result)
    {
        HardwareSummaryPresentation? summary = result.Snapshot is null
            ? null
            : HardwareSummaryPresentationFactory.Create(result.Snapshot);
        HardwareInspectionFailureClass? failureClass = result.FailureKind switch
        {
            HardwareInspectionFailureKind.CriticalEvidence =>
                HardwareInspectionFailureClass.CriticalEvidence,
            HardwareInspectionFailureKind.TransientOperation =>
                HardwareInspectionFailureClass.TransientOperation,
            HardwareInspectionFailureKind.ApplicationRepairRequired =>
                HardwareInspectionFailureClass.ApplicationRepairRequired,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        HardwareInspectionPresentationState presentation =
            _presentationFactory.CreateTerminal(
                result.Outcome,
                failureClass,
                criticalFailureRetryable: false,
                hasUsableHandoff: result.Handoff is not null,
                block3RouteRegistered: false);
        HardwareInspectionDetailsState? details = presentation.DetailsAvailable
            ? CreateDetails(result, owner.LastDisplayedStage)
            : null;
        PublishLocked(
            owner,
            isRunActive: false,
            presentation,
            summary,
            details,
            result.Handoff,
            result.SafeDiagnosticCode);
    }

    private HardwareInspectionDetailsState CreateDetails(
        HardwareInspectionRunResult result,
        int lastDisplayedStage)
    {
        List<HardwareInspectionDetailRow> rows = new(7);
        foreach (HardwareInspectionStage stage in Enum.GetValues<HardwareInspectionStage>())
        {
            HardwareInspectionStageCopy copy = HardwareInspectionCopyCatalog.Stage(stage);
            int index = (int)stage;
            string status;
            string sentence;
            if (result.Outcome is HardwareInspectionOutcome.Completed
                or HardwareInspectionOutcome.CompletedWithWarnings)
            {
                status = result.Outcome == HardwareInspectionOutcome.CompletedWithWarnings
                    && stage == HardwareInspectionStage.CheckingLocalInferenceRuntimes
                    ? "Completed with note"
                    : "Completed";
                sentence = copy.CompletedSentence;
            }
            else if (index < lastDisplayedStage)
            {
                status = "Completed";
                sentence = copy.CompletedSentence;
            }
            else if (index == Math.Max(lastDisplayedStage, 0))
            {
                status = result.Outcome == HardwareInspectionOutcome.Cancelled
                    ? "Cancelled here"
                    : result.FailureKind == HardwareInspectionFailureKind.CriticalEvidence
                        ? "Could not confirm"
                        : "Could not check";
                sentence = result.Outcome == HardwareInspectionOutcome.Cancelled
                    ? "The inspection stopped safely at this stage."
                    : "This stage did not produce a trustworthy result.";
            }
            else
            {
                status = "Not started";
                sentence = copy.WaitingSentence;
            }

            rows.Add(new HardwareInspectionDetailRow(copy.Title, sentence, status));
        }

        bool reportCreated = result.Handoff is not null;
        string safeValue = result.SafeDiagnosticCode
            ?? (reportCreated ? "HI-REPORT-CREATED" : "HI-NO-REPORT");
        return new HardwareInspectionDetailsState(
            "Seven inspection stages and safe support information",
            reportCreated
                ? "The reliable hardware information was recorded."
                : "No complete hardware report was created.",
            reportCreated
                ? "A hardware report was created for this run."
                : "No hardware report was created from this run.",
            reportCreated ? "Report created" : "No report created",
            rows,
            [
                new HardwareInspectionTechnicalGroup(
                    "Run and tool information",
                    "Safe support information for this run",
                    [new HardwareInspectionTechnicalItem("Support code", safeValue)]),
            ]);
    }

    private void RetireCurrentLocked(bool publishInactiveState)
    {
        if (_current is not { IsActive: true } owner)
        {
            return;
        }

        owner.IsActive = false;
        _current = null;
        owner.Progress.Writer.TryComplete();
        owner.Cancellation.Cancel();
        if (publishInactiveState)
        {
            Snapshot = new HardwareInspectionViewState(
                ++_revision,
                owner.AttemptGeneration,
                owner.InspectionId,
                false,
                Snapshot.Presentation,
                Snapshot.Summary,
                Snapshot.Details,
                Snapshot.Handoff,
                Snapshot.SafeDiagnosticCode);
            SnapshotChanged?.Invoke(this, EventArgs.Empty);
        }

    }

    private void PublishLocked(
        RunOwner owner,
        bool isRunActive,
        HardwareInspectionPresentationState presentation,
        HardwareSummaryPresentation? summary,
        HardwareInspectionDetailsState? details,
        HardwareInspectionHandoff? handoff,
        string? safeDiagnosticCode)
    {
        Snapshot = new HardwareInspectionViewState(
            ++_revision,
            owner.AttemptGeneration,
            owner.InspectionId,
            isRunActive,
            presentation,
            summary,
            details,
            handoff,
            safeDiagnosticCode);
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private static HardwareInspectionStage MapStage(
        HardwareInspectionRunStage stage) => stage switch
        {
            HardwareInspectionRunStage.StartingHardwareInspection =>
                HardwareInspectionStage.StartingHardwareInspection,
            HardwareInspectionRunStage.ReadingProcessorInformation =>
                HardwareInspectionStage.ReadingProcessorInformation,
            HardwareInspectionRunStage.ReadingSystemMemory =>
                HardwareInspectionStage.ReadingSystemMemory,
            HardwareInspectionRunStage.DetectingGraphicsHardware =>
                HardwareInspectionStage.DetectingGraphicsHardware,
            HardwareInspectionRunStage.CheckingLocalInferenceRuntimes =>
                HardwareInspectionStage.CheckingLocalInferenceRuntimes,
            HardwareInspectionRunStage.NormalisingHardwareInformation =>
                HardwareInspectionStage.NormalisingHardwareInformation,
            HardwareInspectionRunStage.CreatingHardwareReport =>
                HardwareInspectionStage.CreatingHardwareReport,
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };

    private static bool IsUuidV4(Guid value)
    {
        if (value == Guid.Empty)
        {
            return false;
        }

        string canonical = value.ToString("D", CultureInfo.InvariantCulture);
        char variant = canonical[19];
        return canonical[14] == '4' && variant is '8' or '9' or 'a' or 'b';
    }

    private sealed class RunOwner
    {
        internal RunOwner(Guid inspectionId, long attemptGeneration)
        {
            InspectionId = inspectionId;
            AttemptGeneration = attemptGeneration;
        }

        internal Guid InspectionId { get; }
        internal long AttemptGeneration { get; }
        internal CancellationTokenSource Cancellation { get; } = new();
        internal Channel<HardwareInspectionRunProgress> Progress { get; } =
            Channel.CreateUnbounded<HardwareInspectionRunProgress>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false,
                });
        internal Task? RunTask { get; set; }
        internal bool IsActive { get; set; } = true;
        internal bool CancellationRequested { get; set; }
        internal long LastReceivedSequence { get; set; }
        internal int LastReceivedStage { get; set; } = -1;
        internal int LastDisplayedStage { get; set; } = -1;
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        internal InlineProgress(Action<T> callback) =>
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));

        public void Report(T value) => _callback(value);
    }
}
