using System.Diagnostics;
using System.Security.Cryptography;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class GitEvidenceVerifierTests
{
    [TestMethod]
    public void VerifyBlob_reads_exact_committed_blob_not_working_tree()
    {
        using TestDirectory directory = TestDirectory.Create();
        Repository repository = CreateRepository(directory);

        GitEvidenceVerifier.VerifyBlob(
            repository.Working,
            repository.Commit,
            repository.Tree,
            "docs/evidence.json",
            repository.BlobSha256,
            repository.BlobBytes);

        File.WriteAllText(Path.Combine(repository.Working, "docs", "evidence.json"), "working-tree-only mutation");
        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
            GitEvidenceVerifier.VerifyBlob(
                repository.Working,
                repository.Commit,
                repository.Tree,
                "docs/evidence.json",
                Sha256("working-tree-only mutation"),
                "working-tree-only mutation"u8.Length));
        StringAssert.Contains(error.Message, "Git blob");
    }

    [TestMethod]
    public void VerifyBlob_rejects_wrong_tree_hash_bytes_or_escaping_path()
    {
        using TestDirectory directory = TestDirectory.Create();
        Repository repository = CreateRepository(directory);

        Assert.ThrowsExactly<InvalidDataException>(() => GitEvidenceVerifier.VerifyBlob(
            repository.Working, repository.Commit, new string('f', 40), "docs/evidence.json", repository.BlobSha256, repository.BlobBytes));
        Assert.ThrowsExactly<InvalidDataException>(() => GitEvidenceVerifier.VerifyBlob(
            repository.Working, repository.Commit, repository.Tree, "docs/evidence.json", new string('0', 64), repository.BlobBytes));
        Assert.ThrowsExactly<InvalidDataException>(() => GitEvidenceVerifier.VerifyBlob(
            repository.Working, repository.Commit, repository.Tree, "docs/evidence.json", repository.BlobSha256, repository.BlobBytes + 1));
        Assert.ThrowsExactly<InvalidDataException>(() => GitEvidenceVerifier.VerifyBlob(
            repository.Working, repository.Commit, repository.Tree, "../evidence.json", repository.BlobSha256, repository.BlobBytes));
    }

    [TestMethod]
    public void VerifyPushedRef_rejects_stale_remote_and_unpushed_commit()
    {
        using TestDirectory directory = TestDirectory.Create();
        Repository repository = CreateRepository(directory);

        GitEvidenceVerifier.VerifyPushedRef(
            repository.Working, "origin", "refs/heads/integration/r3-candidate", repository.Commit);

        File.WriteAllText(Path.Combine(repository.Working, "docs", "second.json"), "second");
        Git(repository.Working, "add", "docs/second.json");
        Git(repository.Working, "commit", "-m", "second");
        string unpushed = Git(repository.Working, "rev-parse", "HEAD").Trim();

        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
            GitEvidenceVerifier.VerifyPushedRef(
                repository.Working, "origin", "refs/heads/integration/r3-candidate", unpushed));
        StringAssert.Contains(error.Message, "pushed remote ref");
    }

    private static Repository CreateRepository(TestDirectory directory)
    {
        string working = Path.Combine(directory.Path, "working");
        string remote = Path.Combine(directory.Path, "remote.git");
        Directory.CreateDirectory(working);
        Git(directory.Path, "init", "--bare", remote);
        Git(working, "init");
        Git(working, "config", "user.name", "E1 Test");
        Git(working, "config", "user.email", "e1@example.invalid");
        Directory.CreateDirectory(Path.Combine(working, "docs"));
        const string contents = "sanitized committed evidence";
        File.WriteAllText(Path.Combine(working, "docs", "evidence.json"), contents);
        Git(working, "add", "docs/evidence.json");
        Git(working, "commit", "-m", "evidence");
        Git(working, "remote", "add", "origin", remote);
        Git(working, "push", "origin", "HEAD:refs/heads/integration/r3-candidate");
        string commit = Git(working, "rev-parse", "HEAD").Trim();
        string tree = Git(working, "rev-parse", "HEAD^{tree}").Trim();
        return new Repository(
            working,
            commit,
            tree,
            Sha256(contents),
            System.Text.Encoding.UTF8.GetByteCount(contents));
    }

    private static string Git(string workingDirectory, params string[] arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git.exe",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, error);
        return output;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record Repository(
        string Working,
        string Commit,
        string Tree,
        string BlobSha256,
        long BlobBytes);
}
