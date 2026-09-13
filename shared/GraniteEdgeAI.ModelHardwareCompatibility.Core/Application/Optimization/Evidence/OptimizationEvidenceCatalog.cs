namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

/// <summary>An immutable, fail-closed index of exact configuration evidence.</summary>
public sealed class OptimizationEvidenceCatalog
{
    private readonly IReadOnlyDictionary<OptimizationEvidenceKey, OptimizationEvidenceRecord> _records;

    public OptimizationEvidenceCatalog(IEnumerable<OptimizationEvidenceRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        Dictionary<OptimizationEvidenceKey, OptimizationEvidenceRecord> indexed = [];
        foreach (OptimizationEvidenceRecord record in records)
        {
            ArgumentNullException.ThrowIfNull(record);
            Validate(record);
            if (!indexed.TryAdd(record.Key, record))
            {
                throw new ArgumentException(
                    "Evidence contains more than one result for the same exact key.",
                    nameof(records));
            }
        }

        _records = indexed;
    }

    public bool TryResolve(
        OptimizationEvidenceKey key,
        out OptimizationEvidenceRecord? record)
    {
        if (!IsValid(key))
        {
            record = null;
            return false;
        }

        return _records.TryGetValue(key, out record);
    }

    public bool TryResolveArtifactConfiguration(
        OptimizationEvidenceModelFamily modelFamily,
        string modelIdentitySha256,
        ulong parameterCount,
        OptimizationRoute route,
        string runtimePackageIdentity,
        string sourceWeightRepresentation,
        string targetWeightRepresentation,
        string cacheConfiguration,
        OptimizationEvidenceBackend backend,
        OptimizationEvidenceDeviceClass deviceClass,
        int contextTokens,
        string workload,
        string methodologyIdentity,
        string memoryPerformanceProtocol,
        string executionProfile,
        out OptimizationEvidenceRecord? record)
    {
        OptimizationEvidenceRecord[] matches = [.. _records.Values.Where(item =>
            item.Key.ModelFamily == modelFamily &&
            item.Key.ModelIdentitySha256 == modelIdentitySha256 &&
            item.Key.ParameterCount == parameterCount &&
            item.Key.Route == route &&
            item.Key.RuntimePackageIdentity == runtimePackageIdentity &&
            item.Key.SourceWeightRepresentation == sourceWeightRepresentation &&
            item.Key.TargetWeightRepresentation == targetWeightRepresentation &&
            item.Key.CacheConfiguration == cacheConfiguration &&
            item.Key.Backend == backend &&
            item.Key.DeviceClass == deviceClass &&
            item.Key.ContextTokens == contextTokens &&
            item.Key.Workload == workload &&
            item.Key.MethodologyIdentity == methodologyIdentity &&
            item.Key.MemoryPerformanceProtocol == memoryPerformanceProtocol &&
            item.Key.ExecutionProfile == executionProfile)];
        record = matches.Length == 1 ? matches[0] : null;
        return record is not null;
    }

    internal bool ContainsWeightEvidence(OptimizationRoute route, string representation)
    {
        foreach (OptimizationEvidenceRecord record in _records.Values)
        {
            if (record.Key.Route == route &&
                (record.Key.SourceWeightRepresentation == representation ||
                 record.Key.TargetWeightRepresentation == representation))
            {
                return true;
            }
        }
        return false;
    }

    private static void Validate(OptimizationEvidenceRecord record)
    {
        OptimizationIdentifier.Require(
            record.EvidenceId, nameof(record.EvidenceId), "Optimization evidence");
        if (!IsValid(record.Key))
        {
            throw new ArgumentException(
                "Every evidence-key dimension must be bounded and non-empty.",
                nameof(record));
        }
    }

    private static bool IsValid(OptimizationEvidenceKey? key)
    {
        if (key is null
            || key.ContextTokens < 1
            || key.ParameterCount == 0
            || key.ModelFamily == OptimizationEvidenceModelFamily.Unspecified
            || !Enum.IsDefined(key.ModelFamily)
            || !Enum.IsDefined(key.Route)
            || key.Backend == OptimizationEvidenceBackend.Unspecified
            || !Enum.IsDefined(key.Backend)
            || key.DeviceClass == OptimizationEvidenceDeviceClass.Unspecified
            || !Enum.IsDefined(key.DeviceClass)
            || !OptimizationDigest.IsCanonical(key.ModelIdentitySha256))
        {
            return false;
        }

        string[] dimensions =
        [
            key.RuntimePackageIdentity,
            key.SourceWeightRepresentation,
            key.TargetWeightRepresentation,
            key.CacheConfiguration,
            key.Workload,
            key.MethodologyIdentity,
            key.MemoryPerformanceProtocol,
            key.ExecutionProfile
        ];

        try
        {
            foreach (string value in dimensions)
            {
                OptimizationIdentifier.Require(
                    value, nameof(key), "An optimization evidence-key dimension");
            }

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
