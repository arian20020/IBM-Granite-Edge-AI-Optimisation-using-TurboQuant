using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Features.OpenVinoRoute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
public sealed class ConverterIsolationTests
{
    private static readonly string[] ForbiddenEnvironmentNames =
    [
        "HF_TOKEN", "HUGGING_FACE_HUB_TOKEN", "HTTP_PROXY", "HTTPS_PROXY",
        "ALL_PROXY", "NO_PROXY", "PIP_CONFIG_FILE", "PYTHONHOME",
        "PYTHONPATH", "VIRTUAL_ENV"
    ];

    [TestMethod]
    public void WorkerSourceEnforcesClosedOfflineProtocol()
    {
        string root = FindRepositoryRoot();
        string worker = Path.Combine(root, "workers", "OpenVinoConverter.Worker", "converter");
        string main = Read(Path.Combine(worker, "__main__.py"));
        string protocol = Read(Path.Combine(worker, "protocol.py"));
        string export = Read(Path.Combine(worker, "export.py"));
        string optimize = Read(Path.Combine(worker, "optimize.py"));

        StringAssert.Contains(main, "sys.flags.isolated");
        StringAssert.Contains(main, "sys.flags.no_site");
        StringAssert.Contains(main, "sys.flags.ignore_environment");
        StringAssert.Contains(main, "sys.flags.dont_write_bytecode");
        StringAssert.Contains(main, "install_network_guard");
        StringAssert.Contains(main, "assert_closed_module_paths");
        StringAssert.Contains(main, "GRANITE_CONVERTER_SCRATCH");
        StringAssert.Contains(main, "openvino_telemetry");
        StringAssert.Contains(main, "os.dup2");
        StringAssert.Contains(protocol, "sys.stdin.buffer.readline");
        StringAssert.Contains(protocol, "MAX_REQUEST_BYTES");
        StringAssert.Contains(protocol, "reject_unknown_fields");
        StringAssert.Contains(export, "local_files_only=True");
        StringAssert.Contains(export, "trust_remote_code=False");
        StringAssert.Contains(export, "convert_tokenizer=True");
        StringAssert.Contains(export, "task=\"text-generation-with-past\"");
        StringAssert.Contains(export, "library_name=\"transformers\"");
        StringAssert.Contains(export, "weight_format");
        StringAssert.Contains(export, "\"fp16\"");
        StringAssert.Contains(main, "optimize_model");
        StringAssert.Contains(optimize, "nncf.compress_weights");
        StringAssert.Contains(optimize, "CompressWeightsMode.INT8_ASYM");
        StringAssert.Contains(optimize, "CompressWeightsMode.INT4_ASYM");
        StringAssert.Contains(optimize, "ov.save_model");
    }

    [TestMethod]
    public void BuildScriptRequiresSealedClosureAndIsolatedPythonFlags()
    {
        string root = FindRepositoryRoot();
        string script = Read(Path.Combine(root, "scripts", "openvino", "Build-OpenVinoConverterWorker.ps1"));

        StringAssert.Contains(script, "closureStatus");
        StringAssert.Contains(script, "Test-OpenVinoDependencyLocks.ps1");
        StringAssert.Contains(script, "python.exe");
        StringAssert.Contains(script, "'-I', '-s', '-E'");
        StringAssert.Contains(script, "'-B'");
        StringAssert.Contains(script, "converter_build_failed");
        StringAssert.Contains(script, "converter_worker_built");
        StringAssert.Contains(script, "converter_isolation_valid");
        StringAssert.Contains(script, "Test-PathOverlap");
        StringAssert.Contains(script, "$closureRoot, $buildRoot, $stageRoot");
        StringAssert.Contains(script, "install_network_guard");
        StringAssert.Contains(script, "optimum");
        StringAssert.Contains(script, "openvino_genai");
        StringAssert.Contains(script, "__pycache__");
        StringAssert.Contains(script, "*.pyc");
        StringAssert.Contains(script, "$stageFiles");
        StringAssert.Contains(script, "Get-FileHash");
        StringAssert.Contains(script, "wheelManifestSha256");
        StringAssert.Contains(script, "requirementsLockSha256");
        StringAssert.Contains(script, "converter-manifest.json");
        foreach (string name in ForbiddenEnvironmentNames)
        {
            StringAssert.Contains(script, name);
        }
        foreach (string name in new[]
        {
            "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "HF_HOME",
            "XDG_CACHE_HOME", "TORCH_HOME", "GRANITE_CONVERTER_SCRATCH"
        })
        {
            StringAssert.Contains(script, name);
        }
    }

    [TestMethod]
    public void ConverterBuildEntrypointsUseLongPathCapablePowerShell()
    {
        string root = FindRepositoryRoot();
        string workflow = Read(Path.Combine(
            root, ".github", "workflows", "openvino-official-ci.yml"));
        string campaign = Read(Path.Combine(
            root, "scripts", "openvino", "Invoke-OpenVinoUclCampaign.ps1"));

        StringAssert.Contains(
            workflow,
            "& pwsh -NoLogo -NoProfile -NonInteractive");
        StringAssert.Contains(
            campaign,
            "& pwsh -NoLogo -NoProfile -NonInteractive");
        Assert.IsFalse(workflow.Contains(
            "& powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `\n" +
            "            -File scripts/openvino/Build-OpenVinoConverterWorker.ps1",
            StringComparison.Ordinal));
        Assert.IsFalse(campaign.Contains(
            "& powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `\n" +
            "        -File (Join-Path $PSScriptRoot 'Build-OpenVinoConverterWorker.ps1')",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void ConverterManifestVerifierUsesExtendedPathEnumeration()
    {
        string root = FindRepositoryRoot();
        string verifier = Read(Path.Combine(
            root, "scripts", "openvino", "Test-OpenVinoConverterWorkerManifest.ps1"));

        StringAssert.Contains(verifier, "$enumerationRoot");
        StringAssert.Contains(verifier, "'\\\\?\\' + $root");
        StringAssert.Contains(verifier, "$enumerationRoot.Length + 1");
    }

    [TestMethod]
    public async Task ConverterLicenceNormalizerPreservesBytesAndProvenanceWithinPackagePathBudget()
    {
        string root = FindRepositoryRoot();
        string script = Path.Combine(
            root, "scripts", "openvino", "Convert-OpenVinoConverterLicencePaths.ps1");
        string operationRoot = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-ConverterLicences-" + Guid.NewGuid().ToString("N"));
        string stage = Path.Combine(operationRoot, "stage");
        string firstRelative = Path.Combine(
            "packages", "torch-2.13.0.dist-info", "licenses", "third_party", "kineto",
            "libkineto", "third_party", "dynolog", "third_party", "prometheus-cpp",
            "3rdparty", "civetweb", "src", "third_party", "duktape-1.8.0", "LICENSE.txt");
        string secondRelative = Path.Combine(
            "packages", "sample-1.0.dist-info", "licenses", "NOTICE.txt");
        string runtimeRelative = Path.Combine("packages", "sample", "runtime.py");
        byte[] licenceBytes = Encoding.UTF8.GetBytes("fixture licence bytes\n");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(stage, firstRelative))!);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(stage, secondRelative))!);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(stage, runtimeRelative))!);
            await File.WriteAllBytesAsync(Path.Combine(stage, firstRelative), licenceBytes);
            await File.WriteAllBytesAsync(Path.Combine(stage, secondRelative), licenceBytes);
            await File.WriteAllTextAsync(
                Path.Combine(stage, runtimeRelative), "print('runtime')\n", new UTF8Encoding(false));

            ProcessStartInfo info = new("pwsh")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script,
                "-StageDirectory", stage
            })
            {
                info.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(info)
                ?? throw new InvalidOperationException("Licence normalizer did not start.");
            string standardOutput = await process.StandardOutput.ReadToEndAsync();
            string standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("converter_licence_paths_normalized", standardOutput.Trim());
            Assert.IsFalse(File.Exists(Path.Combine(stage, firstRelative)));
            Assert.IsFalse(File.Exists(Path.Combine(stage, secondRelative)));
            Assert.IsTrue(File.Exists(Path.Combine(stage, runtimeRelative)));

            string inventoryPath = Path.Combine(stage, "licenses", "wheel-license-inventory.json");
            using JsonDocument inventory = JsonDocument.Parse(
                await File.ReadAllBytesAsync(inventoryPath));
            Assert.AreEqual(1, inventory.RootElement.GetProperty("schemaVersion").GetInt32());
            JsonElement entries = inventory.RootElement.GetProperty("files");
            Assert.AreEqual(2, entries.GetArrayLength());
            string[] expectedSources =
            [
                firstRelative.Replace('\\', '/'),
                secondRelative.Replace('\\', '/')
            ];
            Array.Sort(expectedSources, StringComparer.Ordinal);
            CollectionAssert.AreEqual(
                expectedSources,
                entries.EnumerateArray()
                    .Select(entry => entry.GetProperty("sourcePath").GetString())
                    .ToArray());

            string expectedSha = Convert.ToHexString(SHA256.HashData(licenceBytes))
                .ToLowerInvariant();
            string[] stagedPaths = entries.EnumerateArray()
                .Select(entry => entry.GetProperty("stagedPath").GetString()!)
                .ToArray();
            Assert.AreEqual(stagedPaths[0], stagedPaths[1]);
            Assert.IsTrue(stagedPaths[0].Length <= 100, stagedPaths[0]);
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                Assert.AreEqual(expectedSha, entry.GetProperty("sha256").GetString());
                Assert.AreEqual(licenceBytes.LongLength, entry.GetProperty("length").GetInt64());
            }
            string preserved = Path.Combine(stage, stagedPaths[0].Replace('/', '\\'));
            CollectionAssert.AreEqual(licenceBytes, await File.ReadAllBytesAsync(preserved));
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ApprovedConverterResolverAcceptsCurrentSealedManifestWithinBoundedBudget()
    {
        string operationRoot = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-ConverterResolver-" + Guid.NewGuid().ToString("N"));
        string converterRoot = Path.Combine(
            operationRoot, "OpenVino", "Converter", "Worker");
        Directory.CreateDirectory(converterRoot);
        string manifestPath = Path.Combine(converterRoot, "converter-manifest.json");
        try
        {
            using (FileStream manifest = new(
                       manifestPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                manifest.SetLength(4_700_000);
            }
            string expectedDigest;
            using (FileStream manifest = File.OpenRead(manifestPath))
            {
                expectedDigest = Convert.ToHexString(SHA256.HashData(manifest))
                    .ToLowerInvariant();
            }

            Type? resolver = typeof(OpenVinoRouteService).Assembly.GetType(
                "GraniteEdgeAI.Features.OpenVinoRoute.ApprovedOpenVinoConverterResolver");
            Assert.IsNotNull(resolver, "The bounded converter resolver is missing.");
            MethodInfo? resolve = resolver.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string), typeof(string)],
                modifiers: null);
            Assert.IsNotNull(resolve, "The bounded converter resolver entry point is missing.");

            var actual = ((string Root, string Digest))resolve.Invoke(
                obj: null,
                parameters: [operationRoot, expectedDigest])!;

            Assert.AreEqual(Path.GetFullPath(converterRoot), actual.Root);
            Assert.AreEqual(expectedDigest, actual.Digest);
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ApprovedConverterResolverRejectsManifestBeyondBoundedBudget()
    {
        string operationRoot = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-ConverterResolver-" + Guid.NewGuid().ToString("N"));
        string converterRoot = Path.Combine(
            operationRoot, "OpenVino", "Converter", "Worker");
        Directory.CreateDirectory(converterRoot);
        string manifestPath = Path.Combine(converterRoot, "converter-manifest.json");
        try
        {
            using (FileStream manifest = new(
                       manifestPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                manifest.SetLength((8 * 1024 * 1024) + 1);
            }

            Type? resolver = typeof(OpenVinoRouteService).Assembly.GetType(
                "GraniteEdgeAI.Features.OpenVinoRoute.ApprovedOpenVinoConverterResolver");
            Assert.IsNotNull(resolver, "The bounded converter resolver is missing.");
            MethodInfo? resolve = resolver.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string), typeof(string)],
                modifiers: null);
            Assert.IsNotNull(resolve, "The bounded converter resolver entry point is missing.");

            TargetInvocationException exception = Assert.ThrowsExactly<TargetInvocationException>(
                () => resolve.Invoke(
                    obj: null,
                    parameters: [operationRoot, new string('0', 64)]));

            Assert.IsInstanceOfType<InvalidDataException>(exception.InnerException);
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ConverterProtocolContainsNoCommandLineDataParameters()
    {
        string root = FindRepositoryRoot();
        string main = Read(Path.Combine(
            root, "workers", "OpenVinoConverter.Worker", "converter", "__main__.py"));

        Assert.IsFalse(main.Contains("argparse", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("sys.argv[", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("socket.create_connection", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task IsolatedInterpreterRejectsUnknownProtocolWithoutLeakingData()
    {
        string root = FindRepositoryRoot();
        string operationRoot = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-Converter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(operationRoot);
        try
        {
            string archive = Path.Combine(operationRoot, "converter.pyz");
            CreateArchive(
                Path.Combine(root, "workers", "OpenVinoConverter.Worker", "converter"),
                archive);
            string python = await FindPython313Async();
            ProcessStartInfo info = new(python)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.ArgumentList.Add("-I");
            info.ArgumentList.Add("-s");
            info.ArgumentList.Add("-E");
            info.ArgumentList.Add("-S");
            info.ArgumentList.Add("-B");
            info.ArgumentList.Add(archive);
            info.Environment.Clear();
            info.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            info.Environment["HF_HUB_OFFLINE"] = "1";
            info.Environment["TRANSFORMERS_OFFLINE"] = "1";

            using Process process = Process.Start(info)
                ?? throw new InvalidOperationException("Converter fixture did not start.");
            string? hello = await process.StandardOutput.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsNotNull(hello);
            using (JsonDocument document = JsonDocument.Parse(hello))
            {
                Assert.AreEqual(
                    "hello",
                    document.RootElement.GetProperty("type").GetString(),
                    hello);
                Assert.IsTrue(document.RootElement.GetProperty("isolated").GetBoolean());
                Assert.IsTrue(document.RootElement.GetProperty("offline").GetBoolean());
            }

            const string secretMarker = "must-not-cross-the-boundary";
            await process.StandardInput.WriteLineAsync(
                "{\"protocol\":\"granite.openvino.converter\",\"version\":1," +
                "\"operationId\":\"00000000-0000-0000-0000-000000000001\"," +
                "\"sourcePath\":\"C:\\\\" + secretMarker + "\",\"unexpected\":true}");
            process.StandardInput.Close();
            string? failed = await process.StandardOutput.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            string standardError = await process.StandardError.ReadToEndAsync();

            Assert.AreEqual(1, process.ExitCode);
            Assert.IsNotNull(failed);
            using (JsonDocument document = JsonDocument.Parse(failed))
            {
                Assert.AreEqual("failed", document.RootElement.GetProperty("type").GetString());
                Assert.AreEqual(
                    "runtime_protocol_failed",
                    document.RootElement.GetProperty("supportCode").GetString());
            }
            Assert.IsFalse(failed.Contains(secretMarker, StringComparison.Ordinal));
            Assert.AreEqual(string.Empty, standardError);
        }
        finally
        {
            Directory.Delete(operationRoot, recursive: true);
        }
    }

    [TestMethod]
    [Timeout(600_000)]
    public async Task SealedConverterExportsRepositoryFixtureOffline()
    {
        string? stage = Environment.GetEnvironmentVariable(
            "GRANITE_OPENVINO_CONVERTER_STAGE");
        if (string.IsNullOrWhiteSpace(stage))
        {
            Assert.Inconclusive("GRANITE_OPENVINO_CONVERTER_STAGE is required.");
        }
        stage = Path.GetFullPath(stage);
        VerifyStageManifest(stage);

        string root = FindRepositoryRoot();
        string source = Path.Combine(
            root, "tests", "TestFixtures", "OpenVINO", "Converter",
            "TinyGraniteV1", "source");
        string operationRoot = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-ConverterOperation-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(operationRoot, "scratch");
        string destination = Path.Combine(operationRoot, "output");
        PrepareScratch(scratch);
        Directory.CreateDirectory(destination);
        try
        {
            ProcessStartInfo info = new(Path.Combine(stage, "python.exe"))
            {
                WorkingDirectory = stage,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false)
            };
            foreach (string argument in new[] { "-I", "-s", "-E", "-S", "-B", "-m", "converter" })
            {
                info.ArgumentList.Add(argument);
            }
            info.Environment.Clear();
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            info.Environment["SystemRoot"] = systemRoot;
            info.Environment["WINDIR"] = systemRoot;
            info.Environment["TEMP"] = Path.Combine(scratch, "temp");
            info.Environment["TMP"] = Path.Combine(scratch, "temp");
            info.Environment["HOME"] = scratch;
            info.Environment["USERPROFILE"] = scratch;
            info.Environment["APPDATA"] = Path.Combine(scratch, "roaming");
            info.Environment["LOCALAPPDATA"] = Path.Combine(scratch, "local");
            info.Environment["HF_HOME"] = Path.Combine(scratch, "hf");
            info.Environment["XDG_CACHE_HOME"] = Path.Combine(scratch, "xdg");
            info.Environment["TORCH_HOME"] = Path.Combine(scratch, "torch");
            info.Environment["HF_HUB_OFFLINE"] = "1";
            info.Environment["TRANSFORMERS_OFFLINE"] = "1";
            info.Environment["GRANITE_CONVERTER_ROOT"] = stage;
            info.Environment["GRANITE_CONVERTER_SCRATCH"] = scratch;

            using Process process = Process.Start(info)
                ?? throw new InvalidOperationException("Sealed converter did not start.");
            string? hello = await process.StandardOutput.ReadLineAsync();
            Assert.IsNotNull(hello);
            using (JsonDocument document = JsonDocument.Parse(hello))
            {
                Assert.AreEqual("hello", document.RootElement.GetProperty("type").GetString());
                Assert.IsTrue(document.RootElement.GetProperty("networkDenied").GetBoolean());
            }

            string manifestPath = Path.Combine(
                root, "tests", "TestFixtures", "OpenVINO", "Converter",
                "TinyGraniteV1", "manifest.json");
            string digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestPath)))
                .ToLowerInvariant();
            string operationId = Guid.NewGuid().ToString();
            string request = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["protocol"] = "granite.openvino.converter",
                ["version"] = 1,
                ["operationId"] = operationId,
                ["sourcePath"] = source,
                ["destinationPath"] = destination,
                ["sourceManifestSha256"] = digest,
                ["operation"] = "convert",
                ["weightPrecision"] = "fp16"
            });
            await process.StandardInput.WriteLineAsync(request);
            process.StandardInput.Close();
            string events = await process.StandardOutput.ReadToEndAsync();
            string standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(4));

            Assert.AreEqual(
                0,
                process.ExitCode,
                $"Worker events: {events}{Environment.NewLine}Worker stderr: {standardError}");
            Assert.AreEqual(string.Empty, standardError);
            string[] lines = events.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(2, lines.Length);
            using (JsonDocument completed = JsonDocument.Parse(lines[1]))
            {
                Assert.AreEqual("completed", completed.RootElement.GetProperty("type").GetString());
                Assert.AreEqual(operationId, completed.RootElement.GetProperty("operationId").GetString());
            }
            foreach (string name in new[]
            {
                "openvino_model.xml", "openvino_model.bin",
                "openvino_tokenizer.xml", "openvino_tokenizer.bin",
                "openvino_detokenizer.xml", "openvino_detokenizer.bin",
                "config.json", "generation_config.json", "tokenizer_config.json"
            })
            {
                Assert.IsTrue(File.Exists(Path.Combine(destination, name)), name);
            }
            Assert.AreEqual(
                "0",
                File.ReadAllText(Path.Combine(
                    scratch, "local", "Intel Corporation", "openvino_telemetry")));
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(Path.Combine(scratch, "hf")).Count());
            Assert.AreEqual(0, Directory.EnumerateDirectories(stage, "__pycache__", SearchOption.AllDirectories).Count());
            Assert.AreEqual(0, Directory.EnumerateFiles(stage, "*.pyc", SearchOption.AllDirectories).Count());
        }
        finally
        {
            Directory.Delete(operationRoot, recursive: true);
        }
    }

    private static string Read(string path) =>
        File.ReadAllText(path, new UTF8Encoding(false, true));

    private static void VerifyStageManifest(string stage)
    {
        string manifestPath = Path.Combine(stage, "converter-manifest.json");
        Assert.IsTrue(File.Exists(manifestPath));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement entries = manifest.RootElement.GetProperty("files");
        Assert.IsTrue(entries.GetArrayLength() > 100);
        HashSet<string> listed = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string relative = entry.GetProperty("path").GetString()
                ?? throw new InvalidDataException("Stage manifest path is missing.");
            Assert.IsTrue(listed.Add(relative), $"Duplicate stage path: {relative}");
            string path = Path.GetFullPath(Path.Combine(
                stage,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            Assert.IsTrue(path.StartsWith(stage + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
            FileInfo file = new(path);
            Assert.IsTrue(file.Exists, relative);
            Assert.AreEqual(entry.GetProperty("length").GetInt64(), file.Length, relative);
            Assert.AreEqual(
                entry.GetProperty("sha256").GetString(),
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                relative);
        }
        string[] actual = Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, manifestPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(stage, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            listed.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            actual);
    }

    private static void CreateArchive(string source, string destination)
    {
        using FileStream stream = File.Create(destination);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        ZipArchiveEntry entryPoint = archive.CreateEntry("__main__.py");
        using (StreamWriter writer = new(entryPoint.Open(), new UTF8Encoding(false)))
        {
            writer.Write(
                "import os,sys\n" +
                "from converter.protocol import ProtocolError,read_request,write_event\n" +
                "write_event({'type':'hello','isolated':bool(sys.flags.isolated and sys.flags.no_site and sys.flags.ignore_environment),'offline':os.environ.get('HF_HUB_OFFLINE')=='1' and os.environ.get('TRANSFORMERS_OFFLINE')=='1'})\n" +
                "try:\n read_request()\nexcept ProtocolError:\n write_event({'type':'failed','operationId':'00000000-0000-0000-0000-000000000000','supportCode':'runtime_protocol_failed'})\n raise SystemExit(1)\n" +
                "raise SystemExit(2)\n");
        }
        foreach (string file in Directory.EnumerateFiles(source, "*.py"))
        {
            archive.CreateEntryFromFile(
                file,
                "converter/" + Path.GetFileName(file),
                CompressionLevel.Optimal);
        }
    }

    private static void PrepareScratch(string scratch)
    {
        foreach (string relative in new[] { "temp", "roaming", "local", "hf", "xdg", "torch" })
        {
            Directory.CreateDirectory(Path.Combine(scratch, relative));
        }
        string telemetry = Path.Combine(scratch, "local", "Intel Corporation");
        Directory.CreateDirectory(telemetry);
        File.WriteAllText(
            Path.Combine(telemetry, "openvino_telemetry"),
            "0",
            new UTF8Encoding(false));
    }

    private static async Task<string> FindPython313Async()
    {
        ProcessStartInfo info = new("py")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add("-3.13");
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("import sys;print(sys.executable)");
        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Python launcher did not start.");
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, process.ExitCode, await process.StandardError.ReadToEndAsync());
        return output.Trim();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
