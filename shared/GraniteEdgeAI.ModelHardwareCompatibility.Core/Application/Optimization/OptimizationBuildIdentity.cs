namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// What an opaque vendor build identity is allowed to be.
///
/// Deliberately separate from <see cref="OptimizationIdentifier"/>, and not a
/// relaxation of it. The two guard different kinds of value:
///
/// An identifier is something this product chose - an evidence id, a device id,
/// a package id. It has no reason to contain a separator, so a slash in one is
/// evidence that a path has been passed where an identifier was expected, and
/// refusing it is right.
///
/// A build identity is something a vendor chose. The official OpenVINO runtime
/// reports
/// <c>2026.3.0-22451-8a17657b995-releases/2026/3</c>,
/// where the slashes are release-channel structure. Refusing that leaves an
/// executor with no way to record the authoritative identity except by
/// truncating or rewriting it, and substituting a value is the one thing this
/// contract forbids everywhere.
///
/// So the slash is admitted here and nowhere else, and admitted narrowly: it may
/// separate non-empty segments and nothing more. Everything that would make the
/// value path-shaped - a leading or trailing slash, an empty segment, a
/// traversal segment, a backslash, a drive colon - is still refused, so the
/// concession cannot become a real path.
///
/// The value is never normalised, case-folded, truncated, escaped or otherwise
/// transformed, and never passed to a filesystem or path API. It is compared,
/// stored and hashed, and nothing else.
/// </summary>
internal static class OptimizationBuildIdentity
{
    /// <summary>
    /// The same ceiling identifiers use. Past it, something other than a
    /// version is being carried. Kept at 128 because no published vendor
    /// identity approaches it - the official one is 42 characters.
    /// </summary>
    internal const int MaximumLength = 128;

    internal static void Require(string? value, string parameter, string what)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"{what} must be named.", parameter);
        }

        if (value.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"{what} is longer than {MaximumLength} characters, which is past "
                + "any published vendor identity and into the range where unbounded "
                + "content is being carried.",
                parameter);
        }

        // Checked before the character sweep so the message names the actual
        // problem: a value that is otherwise valid but padded is a value
        // something trimmed badly, not a value with an illegal character.
        if (value[0] == ' ' || value[^1] == ' ')
        {
            throw new ArgumentException(
                $"{what} has leading or trailing whitespace. The identity is compared "
                + "ordinally, so a padded copy would not match the runtime that "
                + "reported it.",
                parameter);
        }

        foreach (char character in value)
        {
            if (!IsAdmitted(character))
            {
                throw new ArgumentException(
                    Describe(character, what),
                    parameter);
            }
        }

        RequireSlashesAreStructure(value, parameter, what);
    }

    /// <summary>
    /// Printable ASCII, restricted to what a version or build string needs.
    ///
    /// Whitespace, control characters and non-ASCII are all excluded by
    /// omission rather than by a separate check, so there is one list to read
    /// and no gap between two rules.
    /// </summary>
    private static bool IsAdmitted(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '.' or '_' or '+' or '-' or '/';

    /// <summary>
    /// The slash may only separate non-empty, non-traversal segments.
    ///
    /// This is what keeps the concession from becoming a path. A leading slash
    /// is an absolute location, a trailing one is a directory, an empty segment
    /// is a malformed path, and a dot segment is traversal. None of them is
    /// release-channel structure.
    /// </summary>
    private static void RequireSlashesAreStructure(
        string value, string parameter, string what)
    {
        if (value[0] == '/' || value[^1] == '/')
        {
            throw new ArgumentException(
                $"{what} starts or ends with a slash, which is the shape of a "
                + "filesystem location rather than release-channel structure.",
                parameter);
        }

        foreach (string segment in value.Split('/'))
        {
            if (segment.Length == 0)
            {
                throw new ArgumentException(
                    $"{what} contains an empty slash segment.",
                    parameter);
            }

            if (segment is "." or "..")
            {
                throw new ArgumentException(
                    $"{what} contains a traversal segment, which is a path and not "
                    + "a version.",
                    parameter);
            }
        }
    }

    /// <summary>
    /// Names the specific character class that was refused, so a caller holding
    /// a genuine vendor string can tell a policy decision from a typo. The
    /// offending character itself is not echoed: the value may be adapter
    /// supplied, and a rejection message is a place content leaks.
    /// </summary>
    private static string Describe(char character, string what) => character switch
    {
        '\\' => $"{what} contains a backslash, which is a filesystem separator "
            + "rather than release-channel structure.",

        ':' => $"{what} contains a colon, which is a drive or scheme separator.",

        _ when char.IsWhiteSpace(character) =>
            $"{what} contains whitespace, and the identity is compared ordinally.",

        _ when char.IsControl(character) =>
            $"{what} contains a control character.",

        _ when character > 127 =>
            $"{what} contains a non-ASCII character; published vendor identities "
            + "are ASCII.",

        _ => $"{what} contains a character that is not part of a version or build "
            + "identity. Letters, digits, dot, underscore, plus, hyphen and slash "
            + "are admitted."
    };
}
