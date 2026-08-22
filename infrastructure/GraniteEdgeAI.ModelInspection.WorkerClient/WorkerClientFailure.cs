namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Describes one controlled application-side worker infrastructure failure.
/// The message must remain safe for diagnostics and must not include model
/// paths, environment values, request identifiers, or raw protocol content.
/// </summary>
/// <param name="Code">One approved stable WorkerClient failure code.</param>
/// <param name="Message">A safe human-readable technical explanation.</param>
public sealed record WorkerClientFailure(string Code, string Message)
{
    /// <summary>
    /// Verifies that the failure uses the closed taxonomy and a meaningful safe
    /// message before it crosses the WorkerClient boundary.
    /// </summary>
    public void Validate()
    {
        if (!WorkerClientFailureCodes.IsKnown(Code))
        {
            throw new InvalidOperationException(
                "WorkerClient failures must use an approved stable code.");
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new InvalidOperationException(
                "WorkerClient failures must include a non-blank safe message.");
        }
    }
}
