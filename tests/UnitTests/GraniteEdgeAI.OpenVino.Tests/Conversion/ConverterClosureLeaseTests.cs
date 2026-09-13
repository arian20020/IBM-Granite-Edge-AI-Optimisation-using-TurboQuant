using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests.Conversion;

[TestClass]
[TestCategory("OpenVinoRoute")]
[DoNotParallelize]
public sealed class ConverterClosureLeaseTests
{
    private static readonly string[] LaunchArguments =
        ["-I", "-s", "-E", "-S", "-B", "-m", "converter"];

    [TestMethod]
    public void VerifiedLargeClosureUsesBoundedOperatingSystemHandles()
    {
        const int fileCount = 512;
        string root = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ConverterClosure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var entries = new List<object>(fileCount);
            for (int index = 0; index < fileCount; index++)
            {
                string relative = $"packages/module-{index:D4}.py";
                string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                byte[] content = [(byte)(index & 0xff)];
                File.WriteAllBytes(path, content);
                entries.Add(new
                {
                    path = relative,
                    length = content.LongLength,
                    sha256 = Digest(content)
                });
            }

            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                protocol = "granite.openvino.converter",
                protocolVersion = 1,
                pythonSha256 = new string('a', 64),
                wheelManifestSha256 = new string('b', 64),
                requirementsLockSha256 = new string('c', 64),
                wheelLockStatus = "resolved",
                maximumOperationMinutes = 120,
                launchArguments = LaunchArguments,
                files = entries
            });
            File.WriteAllBytes(Path.Combine(root, "converter-manifest.json"), manifest);
            Thread.Sleep(250);

            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            int before = process.HandleCount;
            using SealedOpenVinoConversionPipeline.ConverterClosureLease lease =
                SealedOpenVinoConversionPipeline.ConverterClosureLease.Verify(
                    root,
                    Digest(manifest),
                    CancellationToken.None);
            process.Refresh();
            int growth = process.HandleCount - before;

            Assert.IsLessThan(32, growth,
                $"Closure verification retained {growth} handles for {fileCount} files.");
            try
            {
                lease.VerifyStillCurrent();
            }
            catch (OpenVinoConversionException)
            {
                Assert.Fail("Unexpected mutation event: " + lease.ObservedMutation);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void MutationDuringLeaseFailsClosedEvenWhenOriginalBytesAreRestored()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ConverterMutation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "packages"));
        try
        {
            string relative = "packages/module.py";
            string path = Path.Combine(root, "packages", "module.py");
            byte[] original = [1, 2, 3];
            File.WriteAllBytes(path, original);
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                protocol = "granite.openvino.converter",
                protocolVersion = 1,
                pythonSha256 = new string('a', 64),
                wheelManifestSha256 = new string('b', 64),
                requirementsLockSha256 = new string('c', 64),
                wheelLockStatus = "resolved",
                maximumOperationMinutes = 120,
                launchArguments = LaunchArguments,
                files = new[]
                {
                    new
                    {
                        path = relative,
                        length = original.LongLength,
                        sha256 = Digest(original)
                    }
                }
            });
            File.WriteAllBytes(Path.Combine(root, "converter-manifest.json"), manifest);
            Thread.Sleep(250);
            using SealedOpenVinoConversionPipeline.ConverterClosureLease lease =
                SealedOpenVinoConversionPipeline.ConverterClosureLease.Verify(
                    root,
                    Digest(manifest),
                    CancellationToken.None);

            File.WriteAllBytes(path, [9, 9, 9]);
            File.WriteAllBytes(path, original);
            Assert.IsTrue(SpinWait.SpinUntil(
                () => lease.ObservedMutation is not null,
                TimeSpan.FromSeconds(2)));

            OpenVinoConversionException failure = Assert.ThrowsExactly<OpenVinoConversionException>(
                lease.VerifyStillCurrent);
            Assert.AreEqual(OpenVinoSupportCode.RuntimeIntegrityFailed, failure.SupportCode);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string Digest(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
