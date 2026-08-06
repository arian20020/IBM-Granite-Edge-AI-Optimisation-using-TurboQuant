using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Proves the empty-by-default child environment using only reported key names
/// and a fixed diagnostics-disabled boolean—never environment values.
/// </summary>
[TestClass]
public sealed class WorkerEnvironmentAndHandleTests
{
    [TestMethod]
    public async Task WorkerReceivesOnlyApprovedEnvironmentKeys()
    {
        await using PublishedFixture fixture =
            await PublishedFixture.CreateAsync().ConfigureAwait(false);
        InspectionWorkerClient client = WorkerProcessTestData.CreateClient(
            fixture,
            "echo-environment-keys");

        WorkerClientResult result = await client.ExecuteAsync(
                WorkerProcessTestData.StartCommand(),
                progress: null,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        result.Validate();
        Assert.IsNull(result.Failure);
        StringAssert.Contains(
            result.RetainedStandardError,
            "FIXTURE:DIAGNOSTICS_DISABLED:true");

        string keyLine = result.RetainedStandardError
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(static line =>
                line.StartsWith(
                    "FIXTURE:ENV_KEYS:",
                    StringComparison.Ordinal));
        HashSet<string> keys = keyLine["FIXTURE:ENV_KEYS:".Length..]
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string required in new[]
                 {
                     "SystemRoot",
                     "WINDIR",
                     "TEMP",
                     "TMP",
                     "DOTNET_EnableDiagnostics",
                     "DOTNET_EnableDiagnostics_IPC",
                     "DOTNET_EnableDiagnostics_Debugger",
                     "DOTNET_EnableDiagnostics_Profiler"
                 })
        {
            Assert.IsTrue(keys.Contains(required), required);
        }

        foreach (string forbidden in new[]
                 {
                     "PATH",
                     "PATHEXT",
                     "GITHUB_TOKEN",
                     "NUGET_AUTH_TOKEN",
                     "AWS_SECRET_ACCESS_KEY",
                     "AZURE_CLIENT_SECRET",
                     "OPENAI_API_KEY",
                     "HF_TOKEN"
                 })
        {
            Assert.IsFalse(keys.Contains(forbidden), forbidden);
        }

        await WorkerProcessTestData.AssertNoFixtureProcessRemainsAsync()
            .ConfigureAwait(false);
    }
}
