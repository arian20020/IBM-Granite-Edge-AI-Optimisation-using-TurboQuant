using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class OpenVinoActivationResultTests
{
    [TestMethod]
    public async Task WorkerFailureBecomesBoundedCategoryWithoutRawDetail()
    {
        const string privateDetail = "C:\\Users\\private\\model provider-token=secret";

        OpenVinoActivationResult result = await OpenVinoActivationRunner.RunAsync(
            _ => Task.FromException<PromptRouteSessionActivation>(
                new InvalidDataException(privateDetail)),
            CancellationToken.None);

        Assert.AreEqual(OpenVinoActivationDisposition.Failed, result.Disposition);
        Assert.AreEqual(
            OpenVinoActivationFailureCategory.UnexpectedFailure,
            result.FailureCategory);
        Assert.IsNull(result.Activation);
        Assert.IsFalse(result.ToString().Contains(
            privateDetail,
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void WorkerSupportCodeMapsToBoundedCategory()
    {
        Assert.AreEqual(
            OpenVinoActivationFailureCategory.WorkerUnavailable,
            OpenVinoActivationRunner.Classify(
                OpenVinoSupportCode.RuntimeDependencyMissing));
    }

    [TestMethod]
    public async Task CallerCancellationRemainsTypedCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OpenVinoActivationResult result = await OpenVinoActivationRunner.RunAsync(
            token => Task.FromCanceled<PromptRouteSessionActivation>(token),
            cancellation.Token);

        Assert.AreEqual(OpenVinoActivationDisposition.Cancelled, result.Disposition);
        Assert.AreEqual(
            OpenVinoActivationFailureCategory.Cancelled,
            result.FailureCategory);
        Assert.IsNull(result.Activation);
    }
}
