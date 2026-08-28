using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class TestWorkspaceTests
{
    [TestMethod]
    public void Create_allocates_unique_children_and_dispose_preserves_siblings()
    {
        using TestDirectory root = TestDirectory.Create();
        string sibling = root.WriteText("keep.txt", "keep");
        string firstPath;
        using (TestWorkspace first = TestWorkspace.Create(root.Path, "same test"))
        using (TestWorkspace second = TestWorkspace.Create(root.Path, "same test"))
        {
            firstPath = first.Path;
            Assert.AreNotEqual(first.Path, second.Path);
            Assert.IsTrue(Directory.Exists(first.Path));
            Assert.IsTrue(Directory.Exists(second.Path));
        }

        Assert.IsFalse(Directory.Exists(firstPath));
        Assert.IsTrue(File.Exists(sibling));
    }
}
