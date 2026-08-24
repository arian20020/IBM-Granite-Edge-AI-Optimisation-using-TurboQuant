namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// What an identifier supplied from outside this assembly is allowed to be.
///
/// Evidence ids, snapshot ids and runtime versions all arrive from adapters, so
/// they are the one place in this contract where a caller chooses the text. An
/// adapter that passed a filesystem path as its "evidence id" would put that
/// path into a plan, a manifest and eventually a support record - and the
/// privacy canary would keep passing, because the member was reviewed once when
/// it held something harmless.
///
/// So the rule is enforced rather than trusted: bounded length, no separators,
/// no traversal, no whitespace runs. The canary's allowlist entry then states a
/// property the type actually guarantees instead of an intention.
/// </summary>
internal static class OptimizationIdentifier
{
    internal const int MaximumLength = 128;

    private const string PathLike = @"/\:?*""<>|";

    internal static void Require(string? value, string parameter, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{what} must be named.", parameter);
        }

        if (value.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"{what} is longer than {MaximumLength} characters, which is past "
                + "anything an identifier needs and into the range where unbounded "
                + "content is being carried.",
                parameter);
        }

        if (value.IndexOfAny(PathLike.ToCharArray()) >= 0 || value.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{what} looks like a path. Identifiers reach plans, manifests and "
                + "support records, so a path passed as one would be published "
                + "wherever those go.",
                parameter);
        }
    }
}
