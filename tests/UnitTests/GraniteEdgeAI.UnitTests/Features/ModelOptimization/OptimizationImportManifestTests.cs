using System.Diagnostics;
using System.Text.Json;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationImportManifestTests
{
    [TestMethod]
    public void ManifestPinsEveryAuthorityAndBansWholeBranchMerges()
    {
        string path = Path.Combine(FindRepositoryRoot(), "docs", "handoffs",
            "2026-08-26-cross-route-optimisation-import-manifest.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        string canonical = document.RootElement.GetRawText();
        foreach (string sha in new[] {
            "092589c38981ad86bb73c7c97dff01ab8b5a6c8e",
            "8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8",
            "f0189ed187ba900f27bade5fde282ae4e99e8d7b",
            "e254385997392601102b16acf19244437803bdcc",
            "bacb3f4106e0191b05b870358342f8158765396d",
            "3f7c29d318e317b63f54c558bc69803963d7d88c" })
            StringAssert.Contains(canonical, sha);
        Assert.IsFalse(canonical.Contains("git merge ", StringComparison.OrdinalIgnoreCase));

        foreach (JsonElement component in document.RootElement.GetProperty("components").EnumerateArray())
        {
            string status = component.GetProperty("status").GetString()!;
            Assert.IsTrue(status is "Pending" or "Verified");
            JsonElement[] sources = [.. component.GetProperty("sources").EnumerateArray()];
            JsonElement[] patches = [.. component.GetProperty("integrationPatches").EnumerateArray()];
            foreach (JsonElement destination in component.GetProperty("destinations").EnumerateArray())
            {
                string kind = destination.GetProperty("kind").GetString()!;
                string destinationPath = destination.GetProperty("path").GetString()!;
                Assert.IsTrue(kind is "Exact" or "Adapted" or "Created");
                Assert.AreEqual(64, destination.GetProperty("resultSha256").GetString()!.Length);
                JsonElement[] matchingSources = [.. sources.Where(source => source.GetProperty("destination").GetString() == destinationPath)];
                if (kind is "Exact" or "Adapted")
                {
                    Assert.AreEqual(1, matchingSources.Length, $"{kind} requires one commit:path:blob source.");
                    JsonElement source = matchingSources[0];
                    Assert.AreEqual(40, source.GetProperty("commit").GetString()!.Length);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(source.GetProperty("path").GetString()));
                    Assert.AreEqual(40, source.GetProperty("blob").GetString()!.Length);
                    Assert.AreEqual(64, source.GetProperty("sha256").GetString()!.Length);
                }
                else
                {
                    Assert.AreEqual(0, matchingSources.Length, "Created cannot invent a source blob.");
                }
                if (kind is "Adapted" or "Created")
                {
                    string groupId = destination.GetProperty("patchGroupId").GetString()!;
                    JsonElement[] groups = [.. patches.Where(patch => patch.GetProperty("id").GetString() == groupId)];
                    Assert.AreEqual(1, groups.Length);
                    Assert.AreEqual(40, groups[0].GetProperty("baseCommit").GetString()!.Length);
                    Assert.AreEqual(40, groups[0].GetProperty("baseTree").GetString()!.Length);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(groups[0].GetProperty("patchPath").GetString()));
                    Assert.AreEqual(64, groups[0].GetProperty("patchSha256").GetString()!.Length);
                }
            }
            if (status == "Verified")
            {
                Assert.IsTrue(component.GetProperty("allowedDestinationPaths").GetArrayLength() > 0);
                Assert.IsTrue(component.GetProperty("dependencyClosure").GetArrayLength() > 0);
                Assert.IsTrue(component.GetProperty("integrationPatches").GetArrayLength() > 0);
                Assert.IsTrue(component.GetProperty("verificationCommands").GetArrayLength() > 0);
            }
        }
    }

    [TestMethod]
    public void PendingComponentCannotAuthorizeImport()
    {
        string root = FindRepositoryRoot();
        ProcessResult result = RunPowerShell(root,
            "-File", Path.Combine(root, "scripts", "verification", "Test-CrossRouteImportManifest.ps1"),
            "-Component", "UO1", "-SourceRepository", root, "-DestinationRepository", root);
        Assert.AreNotEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, "Pending");
    }

    internal static ProcessResult RunPowerShell(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo start = new("powershell.exe") {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, output);
    }

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "IBM Granite with TurboQuant (Intel).slnx")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    internal sealed record ProcessResult(int ExitCode, string Output);
}
