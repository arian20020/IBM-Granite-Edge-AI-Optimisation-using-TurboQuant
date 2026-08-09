using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class ModelInspectionRenderCoordinator : IDisposable
{
    private const ModelInspectionPresentationRegions AllRegions =
        ModelInspectionPresentationRegions.Outcome |
        ModelInspectionPresentationRegions.Model |
        ModelInspectionPresentationRegions.Content |
        ModelInspectionPresentationRegions.Actions |
        ModelInspectionPresentationRegions.Footer |
        ModelInspectionPresentationRegions.ProgressRows |
        ModelInspectionPresentationRegions.LiveRegions;

    private readonly IModelInspectionRenderDispatcher _dispatcher;
    private readonly Func<
        ModelInspectionViewSnapshot,
        bool,
        InspectionProgressRows,
        ModelInspectionPagePresentation> _createPresentation;
    private readonly Action<ModelInspectionPresentationDelta> _applyDelta;

    private InspectionProgressRows _progressRows = new();
    private long _progressRowsGeneration;
    private bool _progressRowsBoundToAttempt;
    private ModelInspectionViewSnapshot? _latestSnapshot;
    private ModelInspectionViewSnapshot? _pendingSnapshot;
    private bool _hasAppliedInitial;
    private bool _isRenderQueued;
    private bool _isDisclosureExpanded;
    private bool _disposed;
    private long _interactionRevision;
    private long _requestVersion;

    internal ModelInspectionRenderCoordinator(
        IModelInspectionRenderDispatcher dispatcher,
        Func<ModelInspectionViewSnapshot, bool, InspectionProgressRows,
            ModelInspectionPagePresentation> createPresentation,
        Action<ModelInspectionPresentationDelta> applyDelta)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _createPresentation = createPresentation ??
            throw new ArgumentNullException(nameof(createPresentation));
        _applyDelta = applyDelta ?? throw new ArgumentNullException(nameof(applyDelta));
    }

    internal ModelInspectionRenderKey? LatestAcceptedKey { get; private set; }

    internal ModelInspectionPagePresentation? CurrentPresentation { get; private set; }

    internal long InteractionRevision => _interactionRevision;

    internal bool HasPendingRender =>
        !_disposed && (_isRenderQueued || _pendingSnapshot is not null);

    internal void ApplyInitial(ModelInspectionViewSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ThrowIfDisposed();
        if (_hasAppliedInitial)
        {
            throw new InvalidOperationException(
                "The initial presentation has already been applied.");
        }

        _hasAppliedInitial = true;
        _latestSnapshot = snapshot;
        LatestAcceptedKey = snapshot.RenderKey;
        _requestVersion = checked(_requestVersion + 1);
        SelectProgressRows(snapshot.RenderKey.AttemptGeneration, out _);

        long requestVersion = _requestVersion;
        ModelInspectionPagePresentation presentation = _createPresentation(
            snapshot,
            false,
            _progressRows);
        if (!IsDrainCurrent(
                snapshot.RenderKey,
                requestVersion,
                _interactionRevision))
        {
            return;
        }

        bool hasProgress = IsProgress(presentation);
        ModelInspectionPresentationRegions changedRegions = hasProgress
            ? AllRegions
            : AllRegions & ~ModelInspectionPresentationRegions.ProgressRows;
        ModelInspectionPresentationDelta delta = new(
            snapshot.RenderKey,
            presentation,
            changedRegions,
            hasProgress ? presentation.ProgressRowsUpdate : null,
            new ModelInspectionVisualOperationKey(
                snapshot.RenderKey,
                _interactionRevision));
        CurrentPresentation = presentation;
        _applyDelta(delta);
    }

    internal void RequestRender(ModelInspectionViewSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ThrowIfDisposed();
        EnsureInitialApplied();

        if (LatestAcceptedKey is ModelInspectionRenderKey latest &&
            Compare(snapshot.RenderKey, latest) < 0)
        {
            return;
        }

        bool advancesGeneration = LatestAcceptedKey is ModelInspectionRenderKey key &&
            snapshot.RenderKey.AttemptGeneration > key.AttemptGeneration;
        if (advancesGeneration)
        {
            _isDisclosureExpanded = false;
            IncrementInteractionRevision();
        }

        _latestSnapshot = snapshot;
        LatestAcceptedKey = snapshot.RenderKey;
        _pendingSnapshot = snapshot;
        _requestVersion = checked(_requestVersion + 1);
        TrySchedulePending();
    }

    internal bool TryRequestDisclosure(
        ModelInspectionRenderKey renderKey,
        bool isExpanded,
        out ModelInspectionVisualOperationKey operationKey)
    {
        operationKey = default;
        if (_disposed ||
            !_hasAppliedInitial ||
            _latestSnapshot is null ||
            CurrentPresentation is null ||
            CurrentPresentation.RenderKey != renderKey ||
            !IsCurrent(renderKey) ||
            !HasDisclosure(CurrentPresentation.State) ||
            _isDisclosureExpanded == isExpanded)
        {
            return false;
        }

        bool previousTarget = _isDisclosureExpanded;
        long previousInteractionRevision = _interactionRevision;
        long previousRequestVersion = _requestVersion;
        ModelInspectionViewSnapshot? previousPending = _pendingSnapshot;
        _isDisclosureExpanded = isExpanded;
        IncrementInteractionRevision();
        _requestVersion = checked(_requestVersion + 1);
        _pendingSnapshot = _latestSnapshot;

        if (!TrySchedulePending())
        {
            _isDisclosureExpanded = previousTarget;
            _interactionRevision = previousInteractionRevision;
            _requestVersion = previousRequestVersion;
            _pendingSnapshot = previousPending;
            return false;
        }

        operationKey = new ModelInspectionVisualOperationKey(
            renderKey,
            _interactionRevision);
        return true;
    }

    internal bool IsCurrent(ModelInspectionRenderKey renderKey) =>
        !_disposed && LatestAcceptedKey == renderKey;

    internal bool IsCurrent(ModelInspectionVisualOperationKey operationKey) =>
        IsCurrent(operationKey.RenderKey) &&
        operationKey.InteractionRevision == _interactionRevision;

    internal void InvalidateInteractions()
    {
        ThrowIfDisposed();
        IncrementInteractionRevision();
        _isDisclosureExpanded = CurrentPresentation is not null &&
            IsExpanded(CurrentPresentation.State);
        _requestVersion = checked(_requestVersion + 1);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IncrementInteractionRevision();
        _pendingSnapshot = null;
        _latestSnapshot = null;
        _isRenderQueued = false;
    }

    private void Drain()
    {
        if (_disposed)
        {
            _pendingSnapshot = null;
            _isRenderQueued = false;
            return;
        }

        _isRenderQueued = false;
        ModelInspectionViewSnapshot? snapshot = _pendingSnapshot;
        _pendingSnapshot = null;
        if (snapshot is null || !IsCurrent(snapshot.RenderKey))
        {
            return;
        }

        long requestVersion = _requestVersion;
        long interactionRevision = _interactionRevision;
        ModelInspectionPagePresentation? previous = CurrentPresentation;
        bool generationChanged = previous is not null &&
            previous.RenderKey.AttemptGeneration !=
                snapshot.RenderKey.AttemptGeneration;
        SelectProgressRows(
            snapshot.RenderKey.AttemptGeneration,
            out bool progressOwnerReplaced);
        if (!IsDrainCurrent(
                snapshot.RenderKey,
                requestVersion,
                interactionRevision))
        {
            return;
        }

        ModelInspectionPagePresentation collapsed = _createPresentation(
            snapshot,
            false,
            _progressRows);
        if (!IsDrainCurrent(
                snapshot.RenderKey,
                requestVersion,
                interactionRevision))
        {
            return;
        }

        bool sameOutcome = previous is not null &&
            previous.RegionKeys.Outcome == collapsed.RegionKeys.Outcome;
        ModelInspectionPagePresentation presentation = collapsed;
        if (sameOutcome &&
            _isDisclosureExpanded &&
            HasDisclosure(collapsed.State))
        {
            presentation = _createPresentation(
                snapshot,
                true,
                _progressRows);
            if (!IsDrainCurrent(
                    snapshot.RenderKey,
                    requestVersion,
                    interactionRevision))
            {
                return;
            }
        }
        else if (previous is not null && !sameOutcome)
        {
            _isDisclosureExpanded = false;
            if (!generationChanged)
            {
                IncrementInteractionRevision();
                interactionRevision = _interactionRevision;
            }
        }

        ModelInspectionPresentationRegions changedRegions = GetChangedRegions(
            previous,
            presentation,
            generationChanged,
            progressOwnerReplaced);
        if (!IsDrainCurrent(
                snapshot.RenderKey,
                requestVersion,
                interactionRevision))
        {
            return;
        }

        CurrentPresentation = presentation;
        if (changedRegions == ModelInspectionPresentationRegions.None)
        {
            return;
        }

        bool changesProgressRows = changedRegions.HasFlag(
            ModelInspectionPresentationRegions.ProgressRows);
        ModelInspectionPresentationDelta delta = new(
            snapshot.RenderKey,
            presentation,
            changedRegions,
            changesProgressRows ? presentation.ProgressRowsUpdate : null,
            new ModelInspectionVisualOperationKey(
                snapshot.RenderKey,
                interactionRevision));
        if (!IsDrainCurrent(
                snapshot.RenderKey,
                requestVersion,
                interactionRevision))
        {
            return;
        }

        _applyDelta(delta);
    }

    private ModelInspectionPresentationRegions GetChangedRegions(
        ModelInspectionPagePresentation? previous,
        ModelInspectionPagePresentation current,
        bool generationChanged,
        bool progressOwnerReplaced)
    {
        if (previous is null)
        {
            return IsProgress(current)
                ? AllRegions
                : AllRegions & ~ModelInspectionPresentationRegions.ProgressRows;
        }

        ModelInspectionPresentationRegions regions =
            ModelInspectionPresentationRegions.None;
        if (previous.RegionKeys.Outcome != current.RegionKeys.Outcome)
        {
            regions |= ModelInspectionPresentationRegions.Outcome;
        }

        if (previous.RegionKeys.Model != current.RegionKeys.Model)
        {
            regions |= ModelInspectionPresentationRegions.Model;
        }

        if (previous.RegionKeys.Content != current.RegionKeys.Content)
        {
            regions |= ModelInspectionPresentationRegions.Content;
        }

        if (previous.RegionKeys.Actions != current.RegionKeys.Actions)
        {
            regions |= ModelInspectionPresentationRegions.Actions;
        }

        if (previous.RegionKeys.Footer != current.RegionKeys.Footer)
        {
            regions |= ModelInspectionPresentationRegions.Footer;
        }

        if (previous.RegionKeys.Announcements != current.RegionKeys.Announcements)
        {
            regions |= ModelInspectionPresentationRegions.LiveRegions;
        }

        if (IsProgress(current) &&
            (generationChanged ||
             previous.RegionKeys.Progress != current.RegionKeys.Progress))
        {
            regions |= ModelInspectionPresentationRegions.ProgressRows;
        }

        if (progressOwnerReplaced &&
            IsProgress(previous) &&
            IsProgress(current))
        {
            regions |= ModelInspectionPresentationRegions.Content;
        }

        return regions;
    }

    private void SelectProgressRows(
        long attemptGeneration,
        out bool ownerReplaced)
    {
        ownerReplaced = false;
        if (attemptGeneration == _progressRowsGeneration)
        {
            return;
        }

        if (!_progressRowsBoundToAttempt &&
            _progressRowsGeneration == 0 &&
            attemptGeneration > 0)
        {
            _progressRows.Reset(new ModelInspectionRenderKey(
                attemptGeneration,
                0));
            _progressRowsGeneration = attemptGeneration;
            _progressRowsBoundToAttempt = true;
            return;
        }

        _progressRows = new InspectionProgressRows();
        if (attemptGeneration > 0)
        {
            _progressRows.Reset(new ModelInspectionRenderKey(
                attemptGeneration,
                0));
            _progressRowsBoundToAttempt = true;
        }
        else
        {
            _progressRowsBoundToAttempt = false;
        }

        _progressRowsGeneration = attemptGeneration;
        ownerReplaced = true;
    }

    private bool TrySchedulePending()
    {
        if (_isRenderQueued)
        {
            return true;
        }

        if (_pendingSnapshot is null)
        {
            return true;
        }

        _isRenderQueued = true;
        bool accepted;
        try
        {
            accepted = _dispatcher.TryEnqueue(() => Drain());
        }
        catch
        {
            _isRenderQueued = false;
            _pendingSnapshot = null;
            throw;
        }

        if (!accepted)
        {
            _isRenderQueued = false;
            _pendingSnapshot = null;
        }

        return accepted;
    }

    private bool IsDrainCurrent(
        ModelInspectionRenderKey renderKey,
        long requestVersion,
        long interactionRevision) =>
        !_disposed &&
        LatestAcceptedKey == renderKey &&
        _requestVersion == requestVersion &&
        _interactionRevision == interactionRevision;

    private static int Compare(
        ModelInspectionRenderKey left,
        ModelInspectionRenderKey right)
    {
        int generation = left.AttemptGeneration.CompareTo(
            right.AttemptGeneration);
        return generation != 0
            ? generation
            : left.PresentationRevision.CompareTo(right.PresentationRevision);
    }

    private static bool IsProgress(ModelInspectionPagePresentation presentation) =>
        presentation.State == ModelInspectionFigmaState.InspectionProgress;

    private static bool HasDisclosure(ModelInspectionFigmaState state) => state is
        ModelInspectionFigmaState.ReadyCollapsed or
        ModelInspectionFigmaState.ReadyExpanded or
        ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
        ModelInspectionFigmaState.ReadyWithWarningsExpanded or
        ModelInspectionFigmaState.ConversionRequiredCollapsed or
        ModelInspectionFigmaState.ConversionRequiredExpanded or
        ModelInspectionFigmaState.InvalidCollapsed or
        ModelInspectionFigmaState.InvalidExpanded;

    private static bool IsExpanded(ModelInspectionFigmaState state) => state is
        ModelInspectionFigmaState.ReadyExpanded or
        ModelInspectionFigmaState.ReadyWithWarningsExpanded or
        ModelInspectionFigmaState.ConversionRequiredExpanded or
        ModelInspectionFigmaState.InvalidExpanded;

    private void IncrementInteractionRevision()
    {
        _interactionRevision = checked(_interactionRevision + 1);
    }

    private void EnsureInitialApplied()
    {
        if (!_hasAppliedInitial)
        {
            throw new InvalidOperationException(
                "ApplyInitial must establish the first presentation.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
