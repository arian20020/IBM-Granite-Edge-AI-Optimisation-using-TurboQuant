namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("StableRouteAcceptance")]
public sealed class StableRouteAcceptanceTests
{
    [TestMethod]
    public void StableAcceptanceEntrypointsArePresentAndFailClosed()
    {
        string repository = FindRepositoryRoot();
        string script = Path.Combine(
            repository, "scripts", "openvino", "Invoke-OpenVinoStableAcceptance.ps1");
        Assert.IsTrue(File.Exists(script), "The stable acceptance entrypoint is missing.");
        string source = File.ReadAllText(script);
        foreach (string required in new[]
        {
            "ExpectedCommitSha", "OfficialStageA", "OfficialStageB", "ConverterStage",
            "ModelIdentityPath", "ConfigurationIdentityPath",
            "ResultsDirectory", "CpuEvidencePath", "GpuDisposition", "driverIdentity",
            "StableRouteAcceptance", "Test-OpenVinoEvidenceArtifactSet.ps1",
            "Test-OpenVinoOfficialWorkerManifest.ps1",
            "Test-OpenVinoConverterWorkerManifest.ps1", "stable_route_accepted"
        })
        {
            Assert.Contains(required, source, StringComparison.Ordinal);
        }
        Assert.Contains("status --porcelain", source, StringComparison.Ordinal);
        Assert.Contains("Get-Process", source, StringComparison.Ordinal);

        foreach (string workflowName in new[]
        {
            "openvino-official-ci.yml", "openvino-ucl-intel.yml"
        })
        {
            string workflow = File.ReadAllText(Path.Combine(
                repository, ".github", "workflows", workflowName));
            Assert.Contains("Invoke-OpenVinoStableAcceptance.ps1", workflow,
                StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
