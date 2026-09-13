namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Represents a controlled failure to satisfy the worker protocol contract.
/// </summary>
public sealed class WorkerProtocolException : Exception
{
    /// <summary>
    /// Creates a protocol exception containing a stable, non-sensitive reason.
    /// </summary>
    public WorkerProtocolException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Provides small shared guards used by the immutable protocol records.
/// </summary>
internal static class WorkerProtocolValidation
{
    internal static void Require(
        bool condition,
        string propertyName,
        string requirement)
    {
        if (!condition)
        {
            throw new WorkerProtocolException(
                $"{propertyName} {requirement}.");
        }
    }

    internal static T RequireNotNull<T>(
        T? value,
        string propertyName)
        where T : class
    {
        if (value is null)
        {
            throw new WorkerProtocolException(
                $"{propertyName} must be present.");
        }

        return value;
    }

    internal static void RequireProtocolVersion(int protocolVersion)
    {
        Require(
            protocolVersion == WorkerProtocol.Version,
            nameof(protocolVersion),
            $"must equal {WorkerProtocol.Version}");
    }

    internal static void RequireRequestId(Guid requestId)
    {
        Require(
            requestId != Guid.Empty,
            nameof(requestId),
            "must be a non-empty GUID");
    }

    internal static void RequireUtcTimestamp(
        DateTimeOffset timestamp,
        string propertyName)
    {
        Require(
            timestamp != default && timestamp.Offset == TimeSpan.Zero,
            propertyName,
            "must be a non-default UTC timestamp");
    }

    internal static void RequireDefinedEnum<TEnum>(
        TEnum value,
        string propertyName)
        where TEnum : struct, Enum
    {
        Require(
            Enum.IsDefined(value),
            propertyName,
            "must be a defined protocol value");
    }

    internal static void RequireOptionalText(
        string? value,
        string propertyName)
    {
        Require(
            value is null || !string.IsNullOrWhiteSpace(value),
            propertyName,
            "must be null or contain non-whitespace text");
    }

    internal static void RequireOptionalPositive<T>(
        T? value,
        string propertyName)
        where T : struct, IComparable<T>
    {
        Require(
            !value.HasValue || value.Value.CompareTo(default) > 0,
            propertyName,
            "must be positive when present");
    }

    internal static void RequireOptionalNonNegative<T>(
        T? value,
        string propertyName)
        where T : struct, IComparable<T>
    {
        Require(
            !value.HasValue || value.Value.CompareTo(default) >= 0,
            propertyName,
            "must not be negative when present");
    }

    internal static void RequireHexDigest(
        string? value,
        string propertyName)
    {
        Require(
            value is { Length: 64 } && value.All(Uri.IsHexDigit),
            propertyName,
            "must contain exactly 64 hexadecimal characters");
    }
}
