using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Protects the distinction between cooperative user cancellation and forced
/// process termination, which is an operational failure rather than success.
/// </summary>
[TestClass]
public sealed class ModelInspectionExecutionResultAdditionalTests
{
    [TestMethod]
    public void CancelledExecution_NonCooperativeTermination_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            ModelInspectionExecutionResult.Cancelled(cooperative: false));
    }
}
