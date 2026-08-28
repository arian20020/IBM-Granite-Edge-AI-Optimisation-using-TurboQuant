namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record IntegrationCandidate(string Commit, string Tree)
{
    internal static IntegrationCandidate Create(
        string commit,
        string tree,
        string previousC0Tip)
    {
        JsonContract.RequireGitObject(commit, nameof(commit));
        JsonContract.RequireGitObject(tree, nameof(tree));
        JsonContract.RequireGitObject(previousC0Tip, nameof(previousC0Tip));
        if (string.Equals(commit, previousC0Tip, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The integration candidate must be newer than the previous C0 tip.");
        }

        if (string.Equals(commit, tree, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Integration candidate commit and tree identities must be distinct.");
        }

        return new IntegrationCandidate(commit, tree);
    }
}
