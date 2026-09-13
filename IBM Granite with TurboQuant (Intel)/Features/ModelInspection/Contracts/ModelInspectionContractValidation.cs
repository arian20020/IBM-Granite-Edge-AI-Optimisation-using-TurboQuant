using System;
using System.IO;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Centralises the small, deterministic guards shared by the application-owned
/// Model Inspection contracts.
/// </summary>
internal static class ModelInspectionContractValidation
{
    /// <summary>
    /// returns required text after rejecting null, empty, or whitespace input
    /// </summary>
    internal static string RequireText(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    /// <summary>
    /// returns optional text after rejecting ambiguous whitespace-only input
    /// </summary>
    internal static string? RequireOptionalText(
        string? value,
        string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Optional text must be null or contain a non-whitespace value.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// returns one final filename after rejecting paths and directory segments
    /// </summary>
    internal static string RequireFinalFileName(
        string? value,
        string parameterName)
    {
        string fileName = RequireText(value, parameterName);
        if (!string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "File name must contain only the final path segment.",
                parameterName);
        }

        return fileName;
    }

    /// <summary>
    /// Returns a non-default timestamp whose offset explicitly represents UTC.
    /// </summary>
    internal static DateTimeOffset RequireUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be a non-default UTC value.",
                parameterName);
        }

        return value;
    }

    /// <summary>
    /// returns an enum value only when it is a declared contract member
    /// </summary>
    internal static TEnum RequireDefinedEnum<TEnum>(
        TEnum value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be a defined contract member.");
        }

        return value;
    }

    /// <summary>
    /// returns a hexadecimal digest of the required length without echoing the
    /// supplied value into diagnostics
    /// </summary>
    internal static string RequireHexDigest(
        string? value,
        int expectedLength,
        string parameterName)
    {
        string digest = RequireText(value, parameterName);
        if (digest.Length != expectedLength ||
            digest.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                $"Digest must contain exactly {expectedLength} hexadecimal characters.",
                parameterName);
        }

        return digest;
    }

    /// <summary>
    /// returns an optional unsigned value that is positive when available
    /// </summary>
    internal static ulong? RequireOptionalPositive(
        ulong? value,
        string parameterName)
    {
        if (value.HasValue && value.Value == 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Available values must be positive.");
        }

        return value;
    }

    /// <summary>
    /// returns an optional signed value that is positive when available
    /// </summary>
    internal static int? RequireOptionalPositive(
        int? value,
        string parameterName)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Available values must be positive.");
        }

        return value;
    }

    /// <summary>
    /// returns an optional signed value that is non-negative when available
    /// </summary>
    internal static int? RequireOptionalNonNegative(
        int? value,
        string parameterName)
    {
        if (value.HasValue && value.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Available values must not be negative.");
        }

        return value;
    }
}
