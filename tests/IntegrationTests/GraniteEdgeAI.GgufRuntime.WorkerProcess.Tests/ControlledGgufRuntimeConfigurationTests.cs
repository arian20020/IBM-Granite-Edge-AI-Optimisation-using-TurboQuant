namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class ControlledGgufRuntimeConfigurationTests
{
    [TestMethod]
    public void LoadRejectsUnknownFieldsAndNonSha256Digests()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                {
                  "schemaVersion": 1,
                  "packageRoot": "C:\\runtime",
                  "manifestSha256": "short",
                  "modelFile": "C:\\model.gguf",
                  "modelSha256": "short",
                  "unexpected": true
                }
                """);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                ControlledGgufRuntimeConfiguration.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
