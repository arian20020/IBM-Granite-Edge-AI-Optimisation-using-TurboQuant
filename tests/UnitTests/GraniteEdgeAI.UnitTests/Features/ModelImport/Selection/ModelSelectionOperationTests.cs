using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Threading;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelSelectionOperationTests
{
    [TestMethod]
    public void NewOperations_HaveDistinctIds()
    {
        using var first = new ModelSelectionOperation();
        using var second = new ModelSelectionOperation();

        Assert.AreNotEqual(first.Id, second.Id);
    }

    [TestMethod]
    public void Replace_PublishesNewOperationBeforeRetiringOldOperation()
    {
        using var first = new ModelSelectionOperation();
        ModelSelectionOperation? current = first;
        using var second = new ModelSelectionOperation();

        ModelSelectionOperation? retired = Interlocked.Exchange(ref current, second);

        Assert.AreSame(second, current);
        Assert.IsFalse(second.Token.IsCancellationRequested);

        retired!.Retire();

        Assert.IsTrue(first.Token.IsCancellationRequested);
        Assert.IsFalse(second.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Retire_CancelsTheOperationToken()
    {
        using var operation = new ModelSelectionOperation();

        operation.Retire();

        Assert.IsTrue(operation.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Retire_IsIdempotent()
    {
        using var operation = new ModelSelectionOperation();

        operation.Retire();
        operation.Retire();

        Assert.IsTrue(operation.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_RetiresTheOperation()
    {
        var operation = new ModelSelectionOperation();
        CancellationToken token = operation.Token;

        operation.Dispose();

        Assert.IsTrue(token.IsCancellationRequested);
        operation.Retire();
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var operation = new ModelSelectionOperation();
        CancellationToken token = operation.Token;

        operation.Dispose();
        operation.Dispose();

        Assert.IsTrue(token.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_DefersCancellationSourceReleaseUntilCompletion()
    {
        var operation = new ModelSelectionOperation();
        CancellationToken token = operation.Token;

        operation.Retire();
        operation.Dispose();

        Assert.IsTrue(token.IsCancellationRequested);
        CancellationTokenRegistration registration = token.Register(static () => { });
        registration.Dispose();

        operation.Complete();

        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = operation.Token);
    }

    [TestMethod]
    public void CompleteThenDispose_ReleasesCancellationSource()
    {
        var operation = new ModelSelectionOperation();
        CancellationToken token = operation.Token;

        operation.Complete();
        operation.Dispose();

        Assert.IsTrue(token.IsCancellationRequested);
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = operation.Token);
    }
}
