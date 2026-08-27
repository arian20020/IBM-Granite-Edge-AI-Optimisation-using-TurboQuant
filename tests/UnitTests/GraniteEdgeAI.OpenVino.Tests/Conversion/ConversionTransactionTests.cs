using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;

namespace GraniteEdgeAI.OpenVino.Tests.Conversion;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class ConversionTransactionTests
{
    [TestMethod]
    public void CreatesFreshOperationOwnedSiblingStagingAndPublishesAtomically()
    {
        using TransactionFixture fixture = TransactionFixture.Create();
        Guid operationId = Guid.NewGuid();
        using ConversionTransaction transaction = ConversionTransaction.Create(
            fixture.Source, fixture.Destination, operationId);

        Assert.AreEqual(operationId, transaction.OperationId);
        Assert.AreEqual(
            Directory.GetParent(fixture.Destination)!.FullName,
            Directory.GetParent(transaction.StagingDirectory)!.FullName,
            ignoreCase: true);
        StringAssert.Contains(
            Path.GetFileName(transaction.StagingDirectory),
            operationId.ToString("N"));
        Assert.IsTrue(Directory.Exists(transaction.StagingDirectory));
        File.WriteAllText(Path.Combine(transaction.StagingDirectory, "complete.txt"), "complete");

        transaction.Publish();

        Assert.IsTrue(transaction.IsPublished);
        Assert.IsFalse(Directory.Exists(transaction.StagingDirectory));
        Assert.AreEqual("complete", File.ReadAllText(
            Path.Combine(fixture.Destination, "complete.txt")));
        Assert.IsFalse(transaction.TryCleanup());
    }

    [TestMethod]
    public void ExistingDestinationAndSourceOverlapAreRejectedWithoutResidue()
    {
        using TransactionFixture fixture = TransactionFixture.Create();
        Directory.CreateDirectory(fixture.Destination);
        Assert.ThrowsExactly<ConversionTransactionException>(() =>
            ConversionTransaction.Create(fixture.Source, fixture.Destination, Guid.NewGuid()));
        Directory.Delete(fixture.Destination);

        string insideSource = Path.Combine(fixture.Source, "converted");
        Assert.ThrowsExactly<ConversionTransactionException>(() =>
            ConversionTransaction.Create(fixture.Source, insideSource, Guid.NewGuid()));
        Assert.IsFalse(Directory.Exists(insideSource));

        string sourceParent = Directory.GetParent(fixture.Source)!.FullName;
        Assert.ThrowsExactly<ConversionTransactionException>(() =>
            ConversionTransaction.Create(fixture.Source, sourceParent, Guid.NewGuid()));
    }

    [TestMethod]
    public void DestinationParentReparsePointIsRejected()
    {
        using TransactionFixture fixture = TransactionFixture.Create();
        string alias = Path.Combine(fixture.Root, "alias");
        try
        {
            Directory.CreateSymbolicLink(alias, Path.GetDirectoryName(fixture.Destination)!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Inconclusive("Directory symbolic links are unavailable on this host.");
        }

        Assert.ThrowsExactly<ConversionTransactionException>(() =>
            ConversionTransaction.Create(
                fixture.Source,
                Path.Combine(alias, "converted"),
                Guid.NewGuid()));
    }

    [TestMethod]
    public void CleanupDeletesOnlyTheRetainedOperationOwnedDirectory()
    {
        using TransactionFixture fixture = TransactionFixture.Create();
        using ConversionTransaction transaction = ConversionTransaction.Create(
            fixture.Source, fixture.Destination, Guid.NewGuid());
        string staging = transaction.StagingDirectory;
        Directory.Delete(staging);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "foreign.txt"), "foreign");

        Assert.IsFalse(transaction.TryCleanup());
        Assert.IsTrue(File.Exists(Path.Combine(staging, "foreign.txt")));
    }

    [TestMethod]
    public void DisposeCleansIncompleteStagingAndRetryUsesANewIdentity()
    {
        using TransactionFixture fixture = TransactionFixture.Create();
        Guid firstId = Guid.NewGuid();
        string firstStaging;
        using (ConversionTransaction first = ConversionTransaction.Create(
                   fixture.Source, fixture.Destination, firstId))
        {
            firstStaging = first.StagingDirectory;
            File.WriteAllText(Path.Combine(firstStaging, "partial.txt"), "partial");
        }
        Assert.IsFalse(Directory.Exists(firstStaging));

        Guid secondId = Guid.NewGuid();
        using ConversionTransaction second = ConversionTransaction.Create(
            fixture.Source, fixture.Destination, secondId);
        Assert.AreNotEqual(firstId, second.OperationId);
        Assert.AreNotEqual(firstStaging, second.StagingDirectory, ignoreCase: true);
    }

    [TestMethod]
    public void PublishedDirectoryCanBeRolledBackOnlyWhileItsIdentityIsRetained()
    {
        using TransactionFixture fixture = TransactionFixture.Create();
        using ConversionTransaction transaction = ConversionTransaction.Create(
            fixture.Source, fixture.Destination, Guid.NewGuid());
        File.WriteAllText(Path.Combine(transaction.StagingDirectory, "complete.txt"), "complete");
        transaction.Publish();

        Assert.IsTrue(transaction.TryRollbackPublished());
        Assert.IsFalse(transaction.IsPublished);
        Assert.IsFalse(Directory.Exists(fixture.Destination));
        Assert.IsTrue(Directory.Exists(transaction.StagingDirectory));
        Assert.IsTrue(transaction.TryCleanup());
    }

    [TestMethod]
    public void RollbackRefusesAReplacementAtThePublishedPath()
    {
        using TransactionFixture fixture = TransactionFixture.Create();
        using ConversionTransaction transaction = ConversionTransaction.Create(
            fixture.Source, fixture.Destination, Guid.NewGuid());
        transaction.Publish();
        Directory.Delete(fixture.Destination, recursive: true);
        Directory.CreateDirectory(fixture.Destination);
        File.WriteAllText(Path.Combine(fixture.Destination, "foreign.txt"), "foreign");

        Assert.IsFalse(transaction.TryRollbackPublished());
        Assert.IsTrue(File.Exists(Path.Combine(fixture.Destination, "foreign.txt")));
    }

    private sealed class TransactionFixture : IDisposable
    {
        private TransactionFixture(string root, string source, string destination)
        {
            Root = root;
            Source = source;
            Destination = destination;
        }

        public string Root { get; }
        public string Source { get; }
        public string Destination { get; }

        public static TransactionFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(), "GraniteEdgeAI-Transaction-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string output = Path.Combine(root, "output");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(source, "source.txt"), "source");
            return new TransactionFixture(root, source, Path.Combine(output, "converted"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
