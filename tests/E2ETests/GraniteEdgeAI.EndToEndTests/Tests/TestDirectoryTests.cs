namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class TestDirectoryTests
{
    [TestMethod]
    public void Dispose_removes_nested_read_only_files_owned_by_the_test()
    {
        TestDirectory directory = TestDirectory.Create();
        string root = directory.Path;
        string nested = Path.Combine(root, "objects", "aa");
        Directory.CreateDirectory(nested);
        string file = Path.Combine(nested, "object");
        File.WriteAllText(file, "test-owned");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);

        directory.Dispose();

        Assert.IsFalse(Directory.Exists(root));
    }
}
