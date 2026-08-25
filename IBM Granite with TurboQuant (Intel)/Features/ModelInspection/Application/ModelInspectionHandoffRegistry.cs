using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

internal enum ModelInspectionHandoffLifecycleState
{
    Issued,
    BoundToHardwareRun,
    Invalidated
}

/// <summary>
/// An opaque capability proving ownership of one atomic Hardware claim.
/// </summary>
internal readonly record struct ModelInspectionHandoffClaim
{
    internal ModelInspectionHandoffClaim(
        Guid modelInspectionHandoffId,
        Guid modelInspectionRunId,
        Guid productHardwareRunId,
        Guid claimToken)
    {
        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelInspectionRunId = modelInspectionRunId;
        ProductHardwareRunId = productHardwareRunId;
        ClaimToken = claimToken;
    }

    internal Guid ModelInspectionHandoffId { get; }

    internal Guid ModelInspectionRunId { get; }

    internal Guid ProductHardwareRunId { get; }

    internal Guid ClaimToken { get; }
}

/// <summary>
/// Owns the process-local, one-use lifecycle of Model Inspection handoffs.
/// Mutable lifecycle data is never serialized into the handoff value.
/// </summary>
internal sealed class ModelInspectionHandoffRegistry : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = [];
    private readonly HashSet<Guid> attemptedProductHardwareRunIds = [];
    private Guid currentModelRunId;
    private bool disposed;

    internal void ActivateModelRun(Guid modelInspectionRunId)
    {
        if (!ModelInspectionHandoff.IsUuidV4(modelInspectionRunId))
        {
            throw new ArgumentException(
                "Current Model run identity must be UUID version 4.",
                nameof(modelInspectionRunId));
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (currentModelRunId == modelInspectionRunId)
            {
                return;
            }

            foreach (Entry entry in entries.Values)
            {
                if (entry.State is
                    ModelInspectionHandoffLifecycleState.Issued or
                    ModelInspectionHandoffLifecycleState.BoundToHardwareRun)
                {
                    entry.Invalidate();
                }
            }

            currentModelRunId = modelInspectionRunId;
        }
    }

    internal bool TryIssue(
        Guid terminalModelInspectionRunId,
        ModelInspectionExecutionResult terminal,
        out ModelInspectionHandoff? handoff)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        lock (gate)
        {
            handoff = null;
            if (disposed ||
                currentModelRunId == Guid.Empty ||
                terminalModelInspectionRunId != currentModelRunId ||
                HasLiveHandoffLocked(currentModelRunId) ||
                !ModelInspectionHandoffProjector.TryProject(
                    currentModelRunId,
                    terminalModelInspectionRunId,
                    terminal,
                    out ModelInspectionHandoff? candidate))
            {
                return false;
            }

            entries.Add(
                candidate!.ModelInspectionHandoffId,
                new Entry(candidate));
            handoff = candidate;
            return true;
        }
    }

    internal bool TryRegisterIssued(ModelInspectionHandoff handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        lock (gate)
        {
            if (disposed ||
                currentModelRunId == Guid.Empty ||
                handoff.ModelInspectionRunId != currentModelRunId)
            {
                return false;
            }

            if (entries.TryGetValue(
                handoff.ModelInspectionHandoffId,
                out Entry? existing))
            {
                return existing.State ==
                        ModelInspectionHandoffLifecycleState.Issued &&
                    Matches(existing.Handoff, handoff);
            }

            if (HasLiveHandoffLocked(currentModelRunId))
            {
                return false;
            }

            entries.Add(handoff.ModelInspectionHandoffId, new Entry(handoff));
            return true;
        }
    }

    internal bool TryBindToHardwareRun(
        ModelInspectionHandoff handoff,
        Guid expectedModelInspectionRunId,
        Guid productHardwareRunId,
        out ModelInspectionHandoffClaim claim)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        lock (gate)
        {
            claim = default;
            if (disposed ||
                !ModelInspectionHandoff.IsUuidV4(productHardwareRunId) ||
                productHardwareRunId == expectedModelInspectionRunId ||
                productHardwareRunId == handoff.ModelInspectionHandoffId ||
                expectedModelInspectionRunId != currentModelRunId ||
                !entries.TryGetValue(
                    handoff.ModelInspectionHandoffId,
                    out Entry? entry) ||
                entry.State != ModelInspectionHandoffLifecycleState.Issued ||
                attemptedProductHardwareRunIds.Contains(productHardwareRunId) ||
                !Matches(entry.Handoff, handoff) ||
                handoff.ModelInspectionRunId != expectedModelInspectionRunId)
            {
                return false;
            }

            Guid token = Guid.NewGuid();
            entry.State = ModelInspectionHandoffLifecycleState.BoundToHardwareRun;
            entry.ProductHardwareRunId = productHardwareRunId;
            entry.ClaimToken = token;
            entry.HardwareStarted = false;
            attemptedProductHardwareRunIds.Add(productHardwareRunId);
            claim = new ModelInspectionHandoffClaim(
                handoff.ModelInspectionHandoffId,
                expectedModelInspectionRunId,
                productHardwareRunId,
                token);
            return true;
        }
    }

    internal bool TryMarkHardwareStarted(ModelInspectionHandoffClaim claim)
    {
        lock (gate)
        {
            if (!TryGetExactBoundEntryLocked(claim, out Entry entry) ||
                entry.HardwareStarted)
            {
                return false;
            }

            entry.HardwareStarted = true;
            return true;
        }
    }

    internal bool TryRollbackBeforeHardwareStart(
        ModelInspectionHandoffClaim claim)
    {
        lock (gate)
        {
            if (!TryGetExactBoundEntryLocked(claim, out Entry entry) ||
                entry.HardwareStarted)
            {
                return false;
            }

            entry.State = ModelInspectionHandoffLifecycleState.Issued;
            entry.ProductHardwareRunId = Guid.Empty;
            entry.ClaimToken = Guid.Empty;
            return true;
        }
    }

    internal bool TryAcceptReissue(
        Guid priorModelInspectionHandoffId,
        ModelInspectionHandoff replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        lock (gate)
        {
            if (disposed ||
                !entries.TryGetValue(priorModelInspectionHandoffId, out Entry? prior) ||
                prior.Handoff.ModelInspectionRunId != currentModelRunId ||
                prior.State !=
                    ModelInspectionHandoffLifecycleState.BoundToHardwareRun ||
                !prior.HardwareStarted ||
                replacement.ModelInspectionHandoffId ==
                    prior.Handoff.ModelInspectionHandoffId ||
                replacement.ModelInspectionHandoffId ==
                    prior.ProductHardwareRunId ||
                entries.ContainsKey(replacement.ModelInspectionHandoffId) ||
                !MatchesReissue(prior.Handoff, replacement))
            {
                return false;
            }

            prior.Invalidate();
            entries.Add(
                replacement.ModelInspectionHandoffId,
                new Entry(replacement));
            return true;
        }
    }

    internal void Invalidate(Guid modelInspectionHandoffId)
    {
        lock (gate)
        {
            if (!disposed &&
                entries.TryGetValue(modelInspectionHandoffId, out Entry? entry))
            {
                entry.Invalidate();
            }
        }
    }

    internal ModelInspectionHandoffLifecycleState? GetState(
        Guid modelInspectionHandoffId)
    {
        lock (gate)
        {
            return entries.TryGetValue(modelInspectionHandoffId, out Entry? entry)
                ? entry.State
                : null;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            foreach (Entry entry in entries.Values)
            {
                entry.Invalidate();
            }

            disposed = true;
            currentModelRunId = Guid.Empty;
        }
    }

    private bool HasLiveHandoffLocked(Guid modelInspectionRunId)
    {
        foreach (Entry entry in entries.Values)
        {
            if (entry.Handoff.ModelInspectionRunId == modelInspectionRunId &&
                entry.State is
                    ModelInspectionHandoffLifecycleState.Issued or
                    ModelInspectionHandoffLifecycleState.BoundToHardwareRun)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetExactBoundEntryLocked(
        ModelInspectionHandoffClaim claim,
        out Entry entry)
    {
        entry = null!;
        if (disposed ||
            !entries.TryGetValue(claim.ModelInspectionHandoffId, out Entry? found) ||
            found.State !=
                ModelInspectionHandoffLifecycleState.BoundToHardwareRun ||
            found.Handoff.ModelInspectionRunId != claim.ModelInspectionRunId ||
            found.ProductHardwareRunId != claim.ProductHardwareRunId ||
            found.ClaimToken == Guid.Empty ||
            found.ClaimToken != claim.ClaimToken)
        {
            return false;
        }

        entry = found;
        return true;
    }

    private static bool Matches(
        ModelInspectionHandoff expected,
        ModelInspectionHandoff actual) =>
        expected.SchemaVersion == actual.SchemaVersion &&
        expected.Route == actual.Route &&
        expected.ModelInspectionHandoffId == actual.ModelInspectionHandoffId &&
        expected.ModelInspectionRunId == actual.ModelInspectionRunId &&
        expected.Outcome == actual.Outcome &&
        string.Equals(
            expected.ModelSha256,
            actual.ModelSha256,
            StringComparison.Ordinal) &&
        expected.ModelLengthBytes == actual.ModelLengthBytes;

    private static bool MatchesReissue(
        ModelInspectionHandoff prior,
        ModelInspectionHandoff replacement) =>
        prior.SchemaVersion == replacement.SchemaVersion &&
        prior.Route == replacement.Route &&
        prior.ModelInspectionRunId == replacement.ModelInspectionRunId &&
        prior.Outcome == replacement.Outcome &&
        string.Equals(
            prior.ModelSha256,
            replacement.ModelSha256,
            StringComparison.Ordinal) &&
        prior.ModelLengthBytes == replacement.ModelLengthBytes;

    private sealed class Entry
    {
        internal Entry(ModelInspectionHandoff handoff)
        {
            Handoff = handoff;
        }

        internal ModelInspectionHandoff Handoff { get; }

        internal ModelInspectionHandoffLifecycleState State { get; set; } =
            ModelInspectionHandoffLifecycleState.Issued;

        internal Guid ProductHardwareRunId { get; set; }

        internal Guid ClaimToken { get; set; }

        internal bool HardwareStarted { get; set; }

        internal void Invalidate()
        {
            State = ModelInspectionHandoffLifecycleState.Invalidated;
            ProductHardwareRunId = Guid.Empty;
            ClaimToken = Guid.Empty;
            HardwareStarted = false;
        }
    }
}
