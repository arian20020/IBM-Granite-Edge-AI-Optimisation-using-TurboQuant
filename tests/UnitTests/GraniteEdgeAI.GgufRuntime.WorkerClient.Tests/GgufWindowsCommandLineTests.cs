using GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Tests;

[TestClass]
public sealed class GgufWindowsCommandLineTests
{
    [TestMethod]
    public void BuildQuotesSpacesQuotesAndTrailingBackslashesForCreateProcess()
    {
        char[] result = GgufWindowsCommandLine.Build(
            "C:\\Program Files\\Granite\\worker.exe",
            ["plain", "with space", "quote\"value", "C:\\trailing\\"]);

        Assert.AreEqual(
            "\"C:\\Program Files\\Granite\\worker.exe\" \"plain\" \"with space\" " +
            "\"quote\\\"value\" \"C:\\trailing\\\\\"\0",
            new string(result));
    }

    [TestMethod]
    public void BuildRejectsEmbeddedNullBeforeNativeLaunch()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufWindowsCommandLine.Build("C:\\worker.exe", ["safe\0unsafe"]));
    }
}
