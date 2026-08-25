using HardwareInspection.LlmFitSpike.Evidence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace HardwareInspection.LlmFitSpike.Tests;

[TestClass]
[DoNotParallelize]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class SpikeOptionsTests
{
    [TestMethod]
    public void Parse_ApprovedArguments_ReturnsCanonicalOptions()
    {
        using var root = new OwnedTemporaryDirectory();
        string candidateRoot = Path.Combine(root.Path, "candidate");
        string outputDirectory = Path.Combine(root.Path, "output");
        Directory.CreateDirectory(candidateRoot);

        SpikeOptions options = SpikeOptions.Parse(
        [
            "--candidate-root",
            Path.Combine(candidateRoot, "."),
            "--output",
            outputDirectory,
            "--timeout-seconds",
            "27",
        ]);

        Assert.AreEqual(Path.GetFullPath(candidateRoot), options.CandidateRoot);
        Assert.AreEqual(Path.GetFullPath(outputDirectory), options.OutputDirectory);
        Assert.AreEqual(TimeSpan.FromSeconds(27), options.Timeout);
        Assert.IsFalse(Directory.Exists(outputDirectory), "Parsing must not create the output directory.");
    }

    [TestMethod]
    public void Parse_ArbitraryExecutableOrArguments_IsRejected()
    {
        using var root = new OwnedTemporaryDirectory();
        string candidateRoot = Path.Combine(root.Path, "candidate");
        string outputDirectory = Path.Combine(root.Path, "output");
        Directory.CreateDirectory(candidateRoot);

        string[][] rejectedArguments =
        [
            ["--candidate-root", candidateRoot, "--output", outputDirectory, "--executable", "private.exe"],
            ["--candidate-root", candidateRoot, "--output", outputDirectory, "--command", "serve"],
            ["--candidate-root", candidateRoot, "--output", outputDirectory, "--", "recommend"],
            ["--candidate-root", candidateRoot, "--output", outputDirectory, "fit"],
            ["--candidate-root", candidateRoot, "--candidate-root", candidateRoot, "--output", outputDirectory],
        ];

        foreach (string[] arguments in rejectedArguments)
        {
            Assert.ThrowsExactly<ArgumentException>(() => SpikeOptions.Parse(arguments));
        }

        Assert.IsFalse(Directory.Exists(outputDirectory), "Rejected input must not create output.");
    }

    [TestMethod]
    public void Parse_OverlappingMissingOrFileOccupiedDirectories_AreRejectedWithoutCreation()
    {
        using var root = new OwnedTemporaryDirectory();
        string candidateRoot = Path.Combine(root.Path, "candidate");
        string nestedOutput = Path.Combine(candidateRoot, "output");
        string occupiedOutput = Path.Combine(root.Path, "occupied");
        Directory.CreateDirectory(candidateRoot);
        File.WriteAllText(occupiedOutput, "occupied");

        string[][] rejectedArguments =
        [
            ["--candidate-root", candidateRoot, "--output", nestedOutput],
            ["--candidate-root", root.Path, "--output", candidateRoot],
            ["--candidate-root", Path.Combine(root.Path, "missing"), "--output", Path.Combine(root.Path, "output")],
            ["--candidate-root", candidateRoot, "--output", occupiedOutput],
            ["--candidate-root", candidateRoot, "--output", Path.Combine(occupiedOutput, "nested")],
            ["--candidate-root", candidateRoot, "--output", Path.Combine(root.Path, "output"), "--timeout-seconds", "0"],
            ["--candidate-root", candidateRoot, "--output", Path.Combine(root.Path, "output"), "--timeout-seconds", "+5"],
        ];

        foreach (string[] arguments in rejectedArguments)
        {
            Assert.ThrowsExactly<ArgumentException>(() => SpikeOptions.Parse(arguments));
        }

        Assert.IsFalse(Directory.Exists(nestedOutput));
    }

    [TestMethod]
    public void CreateOutputDirectory_ValidatedOptions_CreatesOrdinaryDirectoryAfterParsing()
    {
        using var root = new OwnedTemporaryDirectory();
        string candidateRoot = Path.Combine(root.Path, "candidate");
        string outputDirectory = Path.Combine(root.Path, "nested", "output");
        Directory.CreateDirectory(candidateRoot);
        SpikeOptions options = SpikeOptions.Parse(
            ["--candidate-root", candidateRoot, "--output", outputDirectory]);

        Assert.AreEqual(TimeSpan.FromSeconds(15), options.Timeout);
        Assert.IsFalse(Directory.Exists(outputDirectory));

        options.CreateOutputDirectory();

        Assert.IsTrue(Directory.Exists(outputDirectory));
        Assert.AreEqual(
            FileAttributes.Directory,
            File.GetAttributes(outputDirectory) & (FileAttributes.Directory | FileAttributes.ReparsePoint));
    }

    [TestMethod]
    public async Task RunAsync_InvalidCommandLine_ReturnsTwoWithoutEchoingInput()
    {
        const string PrivateValue = @"C:\Users\Arian\private-candidate";
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        int exitCode = await SpikeProgram.RunAsync(
                ["--candidate-root", PrivateValue, "--executable", "private.exe"],
                output,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(2, exitCode);
        string text = output.ToString();
        Assert.IsFalse(text.Contains(PrivateValue, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(text.Contains("private.exe", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(text.Contains("disposition=Blocked", StringComparison.Ordinal));
        Assert.IsTrue(text.Contains("diagnostic=none", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("INVALID-COMMAND-LINE", StringComparison.Ordinal));
        Assert.IsTrue(text.Contains("evidence=none", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_ReparseCandidateOrOutputAncestor_IsRejectedBeforeCreation()
    {
        using var root = new OwnedTemporaryDirectory();
        string physicalCandidate = Path.Combine(root.Path, "physical-candidate");
        string linkedCandidate = Path.Combine(root.Path, "linked-candidate");
        string physicalOutput = Path.Combine(root.Path, "physical-output");
        string linkedOutput = Path.Combine(root.Path, "linked-output");
        Directory.CreateDirectory(physicalCandidate);
        Directory.CreateDirectory(physicalOutput);
        Directory.CreateSymbolicLink(linkedCandidate, physicalCandidate);
        Directory.CreateSymbolicLink(linkedOutput, physicalOutput);

        try
        {
            Assert.ThrowsExactly<ArgumentException>(() => SpikeOptions.Parse(
                ["--candidate-root", linkedCandidate, "--output", Path.Combine(root.Path, "safe-output")]));
            Assert.ThrowsExactly<ArgumentException>(() => SpikeOptions.Parse(
                ["--candidate-root", physicalCandidate, "--output", Path.Combine(linkedOutput, "nested")]));
            Assert.IsFalse(Directory.Exists(Path.Combine(physicalOutput, "nested")));
        }
        finally
        {
            Directory.Delete(linkedCandidate);
            Directory.Delete(linkedOutput);
        }
    }

    [TestMethod]
    public void Parse_DeviceOrUncCandidateAndOutput_AreRejectedBeforeCreation()
    {
        using var root = new OwnedTemporaryDirectory();
        string candidateRoot = Path.Combine(root.Path, "candidate");
        string outputDirectory = Path.Combine(root.Path, "output");
        Directory.CreateDirectory(candidateRoot);
        string deviceCandidate = @"\\?\" + candidateRoot;
        string deviceOutput = @"\\?\" + outputDirectory;
        string uncCandidate = @"\\localhost\C$" + candidateRoot[2..];
        string uncOutput = @"\\localhost\C$" + outputDirectory[2..];

        string[][] rejectedArguments =
        [
            ["--candidate-root", deviceCandidate, "--output", outputDirectory],
            ["--candidate-root", uncCandidate, "--output", outputDirectory],
            ["--candidate-root", candidateRoot, "--output", deviceOutput],
            ["--candidate-root", candidateRoot, "--output", uncOutput],
        ];

        foreach (string[] arguments in rejectedArguments)
        {
            Assert.ThrowsExactly<ArgumentException>(() => SpikeOptions.Parse(arguments));
        }

        Assert.IsFalse(Directory.Exists(outputDirectory));
    }

    [TestMethod]
    public void Parse_OutputInsideCandidateThroughSubstAlias_IsRejectedBeforeCreation()
    {
        using var root = new OwnedTemporaryDirectory();
        string candidateRoot = Path.Combine(root.Path, "Candidate Directory With Long Name");
        Directory.CreateDirectory(candidateRoot);
        string driveName = FindUnusedDriveName();
        string aliasedOutput = Path.Combine(driveName + Path.DirectorySeparatorChar, "output");
        SetSubst(driveName, candidateRoot);
        try
        {
            Assert.ThrowsExactly<ArgumentException>(() => SpikeOptions.Parse(
                ["--candidate-root", candidateRoot, "--output", aliasedOutput]));
        }
        finally
        {
            RemoveSubst(driveName);
        }

        Assert.IsFalse(Directory.Exists(Path.Combine(candidateRoot, "output")));
    }

    [TestMethod]
    public async Task RunAsync_CancelledCaller_ReturnsThreeWithStableOutput()
    {
        using var root = new OwnedTemporaryDirectory();
        string candidateRoot = Path.Combine(root.Path, "candidate");
        string outputDirectory = Path.Combine(root.Path, "output");
        Directory.CreateDirectory(candidateRoot);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        int exitCode = await SpikeProgram.RunAsync(
                ["--candidate-root", candidateRoot, "--output", outputDirectory],
                output,
                cancellation.Token)
            .ConfigureAwait(false);

        Assert.AreEqual(3, exitCode);
        string text = output.ToString();
        Assert.IsTrue(text.Contains("disposition=Blocked", StringComparison.Ordinal));
        Assert.IsTrue(
            text.Contains(
                "diagnostic=" + LlmFitGate1DiagnosticCodes.ProcessCancelled,
                StringComparison.Ordinal));
        Assert.IsTrue(text.Contains("evidence=none", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains(candidateRoot, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void GetExitCode_AcceptedForFunctionalEvaluation_ReturnsZero()
    {
        var result = new LlmFitGate1RunResult(
            LlmFitGate1Disposition.AcceptedForFunctionalEvaluation,
            string.Empty,
            Array.Empty<string>());

        Assert.AreEqual(0, SpikeProgram.GetExitCode(result));
    }

    private sealed class OwnedTemporaryDirectory : IDisposable
    {
        internal OwnedTemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GraniteEdgeAI-SpikeOptions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static string FindUnusedDriveName()
    {
        for (char letter = 'Z'; letter >= 'D'; letter--)
        {
            string driveName = letter + ":";
            if (!Directory.Exists(driveName + Path.DirectorySeparatorChar))
            {
                return driveName;
            }
        }

        throw new AssertFailedException("No unused drive name is available for the alias test.");
    }

    private static void SetSubst(string driveName, string target)
    {
        RunSubst(driveName, target);
    }

    private static void RemoveSubst(string driveName)
    {
        RunSubst(driveName, "/d");
    }

    private static void RunSubst(string driveName, string secondArgument)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "subst.exe"),
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(driveName);
        startInfo.ArgumentList.Add(secondArgument);
        using Process process = Process.Start(startInfo) ??
            throw new AssertFailedException("The alias helper could not start.");
        Assert.IsTrue(process.WaitForExit(10_000));
        Assert.AreEqual(0, process.ExitCode);
    }
}
#pragma warning restore CA1707
