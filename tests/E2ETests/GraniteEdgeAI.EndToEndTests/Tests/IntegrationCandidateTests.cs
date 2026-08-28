using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class IntegrationCandidateTests
{
    private const string PreviousC0 = "a5ef3558334e50587889140dafba194853938765";

    [TestMethod]
    public void Create_accepts_a_new_explicit_commit_and_tree()
    {
        IntegrationCandidate candidate = IntegrationCandidate.Create(
            new string('c', 40),
            new string('d', 40),
            PreviousC0);

        Assert.AreEqual(new string('c', 40), candidate.Commit);
        Assert.AreEqual(new string('d', 40), candidate.Tree);
    }

    [TestMethod]
    public void Create_rejects_previous_C0_tip_as_native_candidate()
    {
        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
            IntegrationCandidate.Create(PreviousC0, new string('d', 40), PreviousC0));

        StringAssert.Contains(error.Message, "newer than the previous C0 tip");
    }

    [TestMethod]
    public void Create_rejects_malformed_or_duplicate_commit_tree_identity()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            IntegrationCandidate.Create("HEAD", new string('d', 40), PreviousC0));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            IntegrationCandidate.Create(new string('c', 40), new string('c', 40), PreviousC0));
    }
}
