using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class PrivacyRedactorTests
{
    [TestMethod]
    public void Redact_removes_drive_and_unc_paths_from_diagnostics()
    {
        string input = "Opened C:\\Users\\private\\model.gguf and \\\\server\\share\\model.xml";

        string actual = PrivacyRedactor.Redact(input);

        Assert.IsFalse(actual.Contains("private", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(actual.Contains("server", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(actual, "<redacted-path>");
    }
}
