using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// A deterministic identity for one complete configuration, used to remove
/// duplicates. It covers only memory-relevant settings and deliberately
/// excludes path, timestamp, user, machine and free-memory values, so the same
/// logical configuration always fingerprints identically on any machine.
/// </summary>
internal readonly record struct CandidateFingerprint
{
    private CandidateFingerprint(string value) => Value = value;

    internal string Value { get; }

    internal static CandidateFingerprint Compute(
        RouteConfiguration configuration,
        ContextTokenCount context,
        CandidatePreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{configuration.CanonicalDescriptor}|ctx={context.Tokens}|prep={preparation}");

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new CandidateFingerprint(Convert.ToHexString(digest).ToLowerInvariant());
    }

    public override string ToString() => Value;
}
