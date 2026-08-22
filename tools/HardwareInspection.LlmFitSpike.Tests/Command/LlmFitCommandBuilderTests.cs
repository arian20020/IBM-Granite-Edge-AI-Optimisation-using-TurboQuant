using System.Diagnostics;
using HardwareInspection.LlmFitSpike.Candidate;
using HardwareInspection.LlmFitSpike.Command;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Command;

[TestClass]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class LlmFitCommandBuilderTests
{
    private static readonly string[] ExpectedSystemArguments = ["--no-dashboard", "--json", "system"];
    private static readonly string[] ExpectedVersionArguments = ["--version"];
    private static readonly string[] ShellExecutableNames = ["cmd.exe", "powershell.exe", "pwsh.exe"];

    [TestMethod]
    public void BuildSystem_UsesOnlyPinnedReadOnlyArguments()
    {
        const string candidateRoot = @"C:\candidate root";
        LlmFitCandidateManifest manifest = LoadApprovedCandidate();
        LlmFitCommand command = LlmFitCommandBuilder.BuildSystem(candidateRoot, manifest);

        CollectionAssert.AreEqual(ExpectedSystemArguments, command.Arguments);
        Assert.AreNotSame(manifest.Commands.System, command.Arguments);
        Assert.AreEqual(Path.GetFullPath(@"C:\candidate root\llmfit.exe"), command.ExecutablePath);
        Assert.AreEqual(Path.GetFullPath(candidateRoot), command.WorkingDirectory);

        ProcessStartInfo startInfo = command.CreateStartInfo();
        Assert.AreEqual(command.ExecutablePath, startInfo.FileName);
        Assert.AreEqual(command.WorkingDirectory, startInfo.WorkingDirectory);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.IsTrue(startInfo.RedirectStandardOutput);
        Assert.IsTrue(startInfo.RedirectStandardError);
        Assert.IsTrue(startInfo.CreateNoWindow);
        CollectionAssert.AreEqual(command.Arguments, startInfo.ArgumentList.ToArray());
        AssertNoShellIntermediary(startInfo);
    }

    [TestMethod]
    public void BuildVersion_UsesOnlyVersionArgument()
    {
        LlmFitCandidateManifest manifest = LoadApprovedCandidate();
        LlmFitCommand command = LlmFitCommandBuilder.BuildVersion(@"C:\candidate", manifest);

        CollectionAssert.AreEqual(ExpectedVersionArguments, command.Arguments);
        Assert.AreNotSame(manifest.Commands.Version, command.Arguments);
        Assert.AreEqual(Path.GetFullPath(@"C:\candidate\llmfit.exe"), command.ExecutablePath);

        ProcessStartInfo startInfo = command.CreateStartInfo();
        Assert.AreEqual(command.ExecutablePath, startInfo.FileName);
        Assert.AreEqual(command.WorkingDirectory, startInfo.WorkingDirectory);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.IsTrue(startInfo.RedirectStandardOutput);
        Assert.IsTrue(startInfo.RedirectStandardError);
        Assert.IsTrue(startInfo.CreateNoWindow);
        CollectionAssert.AreEqual(command.Arguments, startInfo.ArgumentList.ToArray());
        AssertNoShellIntermediary(startInfo);
    }

    [TestMethod]
    public void BuildSystem_ExecutableOutsideRoot_Throws()
    {
        LlmFitCandidateManifest approvedManifest = LoadApprovedCandidate();
        LlmFitCandidateManifest traversalManifest = new(
            approvedManifest.SchemaVersion,
            approvedManifest.CandidateId,
            approvedManifest.Version,
            approvedManifest.ReleaseTag,
            approvedManifest.ReleaseCommit,
            approvedManifest.PublishedAtUtc,
            approvedManifest.Archive,
            approvedManifest.Executable with { RelativePath = @"..\llmfit.exe" },
            approvedManifest.RequiredFiles,
            approvedManifest.Commands,
            approvedManifest.License);

        Assert.ThrowsExactly<InvalidDataException>(
            () => LlmFitCommandBuilder.BuildSystem(@"C:\candidate", traversalManifest));
    }

    private static void AssertNoShellIntermediary(ProcessStartInfo startInfo)
    {
        Assert.IsFalse(
            ShellExecutableNames.Contains(Path.GetFileName(startInfo.FileName), StringComparer.OrdinalIgnoreCase));
    }

    private static LlmFitCandidateManifest LoadApprovedCandidate()
    {
        return LlmFitCandidateManifestLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "Candidates", "llmfit-v1.1.9-win-x64.json"));
    }
}
#pragma warning restore CA1707
