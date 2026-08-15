using HardwareInspection.LlmFitSpike.Candidate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Candidate;

[TestClass]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class LlmFitCandidateManifestTests
{
    private static readonly string[] ExpectedSystemArguments = ["--no-dashboard", "--json", "system"];
    private static readonly string[] ExpectedVersionArguments = ["--version"];

    [TestMethod]
    public void Load_ApprovedCandidate_PreservesExactIdentity()
    {
        LlmFitCandidateManifest manifest = LoadApprovedCandidate();

        Assert.AreEqual("1.0", manifest.SchemaVersion);
        Assert.AreEqual("llmfit-v1.1.9-win-x64", manifest.CandidateId);
        Assert.AreEqual("1.1.9", manifest.Version);
        Assert.AreEqual("a02e13f1013ed69889ff44426a651bf7c68c292e", manifest.ReleaseCommit);
        Assert.AreEqual("a030269d7cc8a5bf40383f526a481655d698ec71dd792a25b06510cef9f8b738", manifest.Archive.Sha256);
        Assert.AreEqual("db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19", manifest.Executable.Sha256);
    }

    [TestMethod]
    public void Load_ApprovedCandidate_UsesOnlyReadOnlyCommands()
    {
        LlmFitCandidateManifest manifest = LoadApprovedCandidate();

        CollectionAssert.AreEqual(ExpectedSystemArguments, manifest.Commands.System.ToArray());
        CollectionAssert.AreEqual(ExpectedVersionArguments, manifest.Commands.Version.ToArray());
        CollectionAssert.DoesNotContain(manifest.Commands.System.ToArray(), "serve");
        CollectionAssert.DoesNotContain(manifest.Commands.Version.ToArray(), "serve");
    }

    [TestMethod]
    public void Load_ApprovedCandidate_RetainsMitLicense()
    {
        LlmFitCandidateManifest manifest = LoadApprovedCandidate();

        Assert.AreEqual("MIT", manifest.License.Spdx);
        Assert.AreEqual("LICENSE", manifest.License.RelativePath);
        CollectionAssert.Contains(manifest.RequiredFiles.ToArray(), "LICENSE");
        CollectionAssert.Contains(manifest.RequiredFiles.ToArray(), "README.md");
    }

    [TestMethod]
    public void Load_TraversalPath_Throws()
    {
        string json = File.ReadAllText(GetApprovedCandidatePath())
            .Replace("\"relativePath\": \"llmfit.exe\"", "\"relativePath\": \"..\\\\llmfit.exe\"", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => LlmFitCandidateManifestLoader.Parse(json));
    }

    private static LlmFitCandidateManifest LoadApprovedCandidate()
    {
        return LlmFitCandidateManifestLoader.Load(GetApprovedCandidatePath());
    }

    private static string GetApprovedCandidatePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Candidates", "llmfit-v1.1.9-win-x64.json");
    }
}
#pragma warning restore CA1707
