using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.Features.ModelInspection.SourceCustody;

internal sealed record ModelSourceCustodyKey
{
    internal ModelSourceCustodyKey(
        Guid modelInspectionHandoffId,
        string modelSha256,
        long modelLengthBytes,
        OptimizationRoute route)
    {
        if (modelInspectionHandoffId == Guid.Empty)
        {
            throw new ArgumentException(
                "A source custody key requires a handoff identity.",
                nameof(modelInspectionHandoffId));
        }
        if (!IsCanonicalSha256(modelSha256))
        {
            throw new ArgumentException(
                "A source custody key requires a canonical model digest.",
                nameof(modelSha256));
        }
        if (modelLengthBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(modelLengthBytes));
        }
        if (!Enum.IsDefined(route))
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }

        ModelInspectionHandoffId = modelInspectionHandoffId;
        ModelSha256 = modelSha256;
        ModelLengthBytes = modelLengthBytes;
        Route = route;
    }

    internal Guid ModelInspectionHandoffId { get; }
    internal string ModelSha256 { get; }
    internal long ModelLengthBytes { get; }
    internal OptimizationRoute Route { get; }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

internal sealed class ModelSourceCustodyRecord
{
    internal ModelSourceCustodyRecord(ModelSourceCustodyKey key, string sourcePath)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(sourcePath)
            || !Path.IsPathFullyQualified(sourcePath))
        {
            throw new ArgumentException(
                "Source custody requires a fully qualified private path.",
                nameof(sourcePath));
        }
        SourcePath = sourcePath;
    }

    internal ModelSourceCustodyKey Key { get; }
    internal string SourcePath { get; }
}

internal sealed class ModelSourceLease : IDisposable
{
    private Action? _release;

    internal ModelSourceLease(string sourcePath, Action release)
    {
        SourcePath = sourcePath;
        _release = release;
    }

    internal string SourcePath { get; }
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

internal sealed class ModelSourceCustodyRegistry : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<ModelSourceCustodyKey, Entry> _entries = [];
    private bool _disposed;

    internal bool Register(ModelSourceCustodyRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_entries.TryGetValue(record.Key, out Entry? existing))
            {
                return !existing.Retired
                    && string.Equals(
                        existing.SourcePath,
                        record.SourcePath,
                        StringComparison.OrdinalIgnoreCase);
            }

            _entries.Add(record.Key, new Entry(record.SourcePath));
            return true;
        }
    }

    internal bool TryAcquire(
        ModelSourceCustodyKey key,
        out ModelSourceLease? lease)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            lease = null;
            if (_disposed
                || !_entries.TryGetValue(key, out Entry? entry)
                || entry.Retired)
            {
                return false;
            }

            entry.LeaseCount++;
            lease = new ModelSourceLease(
                entry.SourcePath,
                () => Release(key, entry));
            return true;
        }
    }

    internal void Retire(Guid modelInspectionHandoffId)
    {
        lock (_gate)
        {
            foreach ((ModelSourceCustodyKey key, Entry entry) in
                _entries.ToArray())
            {
                if (key.ModelInspectionHandoffId != modelInspectionHandoffId)
                {
                    continue;
                }
                entry.Retired = true;
                if (entry.LeaseCount == 0)
                {
                    _entries.Remove(key);
                }
            }
        }
    }

    private void Release(ModelSourceCustodyKey key, Entry expected)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out Entry? current)
                || !ReferenceEquals(current, expected)
                || current.LeaseCount == 0)
            {
                return;
            }
            current.LeaseCount--;
            if (current.Retired && current.LeaseCount == 0)
            {
                _entries.Remove(key);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _entries.Clear();
        }
    }

    private sealed class Entry(string sourcePath)
    {
        internal string SourcePath { get; } = sourcePath;
        internal int LeaseCount { get; set; }
        internal bool Retired { get; set; }
    }
}
