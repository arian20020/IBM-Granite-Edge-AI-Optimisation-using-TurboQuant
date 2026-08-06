namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Carries one validated WorkerClient policy failure through internal control
/// flow without exposing raw process or protocol data.
/// </summary>
public sealed class WorkerClientPolicyException : Exception
{
    /// <summary>
    /// Creates a policy exception from one validated stable failure.
    /// </summary>
    /// <param name="failure">The safe controlled failure.</param>
    public WorkerClientPolicyException(WorkerClientFailure failure)
        : this(failure, null)
    {
    }

    /// <summary>
    /// Creates a policy exception while preserving a controlled underlying cause.
    /// </summary>
    /// <param name="failure">The safe controlled failure.</param>
    /// <param name="innerException">The underlying implementation exception.</param>
    public WorkerClientPolicyException(
        WorkerClientFailure failure,
        Exception? innerException)
        : base(GetValidatedMessage(failure), innerException)
    {
        Failure = failure;
    }

    /// <summary>
    /// Gets the validated stable failure carried by this exception.
    /// </summary>
    public WorkerClientFailure Failure { get; }

    /// <summary>
    /// Creates a policy exception without allowing an arbitrary public failure
    /// code outside the approved taxonomy.
    /// </summary>
    public static WorkerClientPolicyException For(string code, string message)
    {
        return new WorkerClientPolicyException(
            new WorkerClientFailure(code, message));
    }

    private static string GetValidatedMessage(WorkerClientFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        failure.Validate();
        return failure.Message;
    }
}
