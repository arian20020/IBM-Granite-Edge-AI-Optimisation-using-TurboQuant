using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Verifies that the first executable MSTest project is correctly
/// compiled, discovered and run by the configured testing platform.
/// </summary>
[TestClass]
public sealed class TestingFoundationSmokeTests
{
    /// <summary>
    /// Confirms that the test is executing from the expected test assembly.
    ///
    /// This is an infrastructure smoke test. It does not claim that any
    /// application feature has been tested yet.
    /// </summary>
    [TestMethod]
    public void TestAssembly_ShouldHaveExpectedName()
    {
        // Arrange and act:
        // Read the name of the compiled assembly that contains this test.
        string? assemblyName =
            typeof(TestingFoundationSmokeTests).Assembly.GetName().Name;

        // Assert:
        // Confirm that the intended unit-test project was built and executed.
        Assert.AreEqual(
            "GraniteEdgeAI.UnitTests",
            assemblyName);
    }
}
