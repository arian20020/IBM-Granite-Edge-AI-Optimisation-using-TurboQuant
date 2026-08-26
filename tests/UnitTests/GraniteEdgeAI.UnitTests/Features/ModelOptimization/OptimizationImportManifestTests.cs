using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationImportManifestTests
{
    [TestMethod]
    public void ManifestPinsExactAuthorityAndPolicySurface()
    {
        string path = Path.Combine(FindRepositoryRoot(), "docs", "handoffs",
            "2026-08-26-cross-route-optimisation-import-manifest.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        (string Name, string Ref, string Commit)[] expected =
        [
            ("planning-base", "fix/hardware-inspection-loq-baseline", "092589c38981ad86bb73c7c97dff01ab8b5a6c8e"),
            ("canonical-optimisation-ui", "origin/feature/cross-route-optimisation-ui-v1", "8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8"),
            ("openvino-route", "origin/feature/openvino-optimisation-adapter-v1", "f0189ed187ba900f27bade5fde282ae4e99e8d7b"),
            ("historical-cross-route-contracts", "origin/feature/cross-route-optimisation-contracts-v2-1", "e254385997392601102b16acf19244437803bdcc"),
            ("production-gguf-runtime", "origin/feature/gguf-cli-chat-production", "bacb3f4106e0191b05b870358342f8158765396d"),
            ("llama-cpp-quantiser", "https://github.com/ggml-org/llama.cpp.git", "3f7c29d318e317b63f54c558bc69803963d7d88c")
        ];
        JsonElement[] authorities = [.. root.GetProperty("authorities").EnumerateArray()];
        Assert.AreEqual(expected.Length, authorities.Length);
        foreach ((string name, string reference, string commit) in expected)
        {
            JsonElement authority = authorities.Single(value => value.GetProperty("name").GetString() == name);
            Assert.AreEqual(reference, authority.GetProperty("ref").GetString());
            Assert.AreEqual(commit, authority.GetProperty("commit").GetString());
        }
        Assert.AreEqual("bounded commit:path:blob verification", root.GetProperty("policy").GetProperty("importMethod").GetString());
        Assert.AreEqual("verified no-filter blob hashing and literal update-index cacheinfo", root.GetProperty("policy").GetProperty("stagingMethod").GetString());
        CollectionAssert.AreEqual(new[] {
            "docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json",
            "docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md"
        }, root.GetProperty("policy").GetProperty("manifestPaths").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [TestMethod]
    public void ComponentPolicyClosureAndExclusionsAreCompleteAndMirroredInTheNarrative()
    {
        string path = Path.Combine(FindRepositoryRoot(), "docs", "handoffs",
            "2026-08-26-cross-route-optimisation-import-manifest.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Dictionary<string, (int Closure, int Exclusions)> expectedCounts = new(StringComparer.Ordinal)
        {
            ["UO1"] = (2, 3),
            ["GGUF runtime"] = (27, 8),
            ["OpenVINO route"] = (35, 12)
        };
        Dictionary<string, string[]> expectedPatchPaths = new(StringComparer.Ordinal)
        {
            ["UO1"] = ["docs/handoffs/import-patches/uo1-v3-adaptation.patch"],
            ["GGUF runtime"] = ["docs/handoffs/import-patches/gguf-runtime-integration.patch"],
            ["OpenVINO route"] =
            [
                "docs/handoffs/import-patches/openvino-v3-adaptation.patch",
                "docs/handoffs/import-patches/openvino-shared-integration.patch",
                "docs/handoffs/import-patches/gguf-verifier-openvino-integration.patch"
            ]
        };
        Dictionary<string, string[]> expectedImportCommands = new(StringComparer.Ordinal)
        {
            ["UO1"] =
            [
                "$reviewPathspec = Join-Path $env:TEMP 'uo1-import-pathspec.bin'",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Test-CrossRouteImportManifest.ps1 -Component UO1 -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Test-CrossRouteImportManifest.ps1 -Component UO1 -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified"
            ],
            ["GGUF runtime"] =
            [
                "$reviewPathspec = Join-Path $env:TEMP 'gguf-runtime-import-pathspec.bin'",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Test-CrossRouteImportManifest.ps1 -Component GgufRuntime -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Test-CrossRouteImportManifest.ps1 -Component GgufRuntime -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified"
            ],
            ["OpenVINO route"] =
            [
                "$reviewPathspec = Join-Path $env:TEMP 'geai-task13-pathspec.bin'",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Test-CrossRouteImportManifest.ps1 -Component OpenVino -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -WriteAllowedPathspec $reviewPathspec",
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Test-CrossRouteImportManifest.ps1 -Component OpenVino -SourceRepository $env:GEAI_SOURCE_REPOSITORY -DestinationRepository $PWD -StageVerified"
            ]
        };

        JsonElement[] components = [.. document.RootElement.GetProperty("components").EnumerateArray()];
        CollectionAssert.AreEquivalent(expectedCounts.Keys.ToArray(), components.Select(ComponentName).ToArray());
        string narrative = File.ReadAllText(Path.ChangeExtension(path, ".md"));
        foreach (JsonElement component in components)
        {
            string name = ComponentName(component);
            string heading = name == "GGUF runtime" ? "### GGUF runtime" : name == "OpenVINO route" ? "### OpenVINO route" : "### UO1";
            int sectionStart = narrative.IndexOf(heading, StringComparison.Ordinal);
            int sectionEnd = narrative.IndexOf("\n### ", sectionStart + heading.Length, StringComparison.Ordinal);
            string section = narrative.Substring(sectionStart, sectionEnd < 0 ? narrative.Length - sectionStart : sectionEnd - sectionStart);
            string[] closure = Strings(component, "dependencyClosure");
            string[] exclusions = Strings(component, "excludedSharedPaths");
            string[] commands = Strings(component, "verificationCommands");
            Assert.AreEqual(expectedCounts[name].Closure, closure.Length, $"{name} closure drifted.");
            Assert.AreEqual(expectedCounts[name].Exclusions, exclusions.Length, $"{name} exclusions drifted.");
            CollectionAssert.AreEqual(closure, MarkdownBulletsAfter(section, "Closure:"), $"{name} narrative closure is not an exact mirror.");
            CollectionAssert.AreEqual(exclusions, MarkdownBulletsAfter(section, "Excluded:"), $"{name} narrative exclusions are not an exact mirror.");
            CollectionAssert.AreEqual(commands, MarkdownBulletsAfter(section, "Verification commands:"), $"{name} narrative verification commands are not an exact mirror.");
            CollectionAssert.AreEqual(expectedImportCommands[name], MarkdownBulletsAfter(section, "Import commands, in order:"), $"{name} canonical import commands drifted.");
            string[] narrativePatchPaths = Regex.Matches(section, @"docs/handoffs/import-patches/[a-z0-9.-]+\.patch")
                .Select(match => match.Value).Distinct(StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expectedPatchPaths[name], narrativePatchPaths, $"{name} narrative patch policy drifted.");
        }
        StringAssert.Contains(narrative, "The canonical staging rule is `verified no-filter blob hashing and literal update-index cacheinfo`.");
        string plan = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "superpowers", "plans",
            "2026-08-26-cross-route-optimisation-integration.md"));
        foreach (string command in expectedImportCommands.Values.SelectMany(value => value))
            Assert.AreEqual(1, Regex.Matches(plan, $"(?m)^{Regex.Escape(command)}\\r?$").Count,
                $"The plan must contain the exact canonical command once: {command}");

        static string ComponentName(JsonElement component) => component.GetProperty("name").GetString()!;
        static string[] Strings(JsonElement component, string property) =>
            component.GetProperty(property).EnumerateArray().Select(value => value.GetString()!).ToArray();
        static string[] MarkdownBulletsAfter(string section, string heading)
        {
            string[] lines = section.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            int headingIndex = Array.FindIndex(lines, line => string.Equals(line, heading, StringComparison.Ordinal));
            Assert.IsTrue(headingIndex >= 0, $"Narrative heading is missing: {heading}");
            List<string> values = [];
            for (int index = headingIndex + 1; index < lines.Length; index++)
            {
                string line = lines[index];
                if (line.Length == 0 && values.Count == 0)
                    continue;
                if (!line.StartsWith("- `", StringComparison.Ordinal) || !line.EndsWith('`'))
                    break;
                values.Add(line[3..^1]);
            }
            return [.. values];
        }
    }

    [TestMethod]
    public void GitObjectResolutionDisablesReplaceRefsAndUsesOnlyPinnedObjectNames()
    {
        string module = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "verification", "CrossRouteImportManifest.Core.psm1"));
        StringAssert.Contains(module, "GIT_NO_REPLACE_OBJECTS");
        StringAssert.Contains(module, "--end-of-options");
        StringAssert.Contains(module, "--literal-pathspecs");
        StringAssert.Contains(module, "Assert-SafeImportRepositoryConfiguration");
        StringAssert.Contains(module, "Repository-local core.fsmonitor is forbidden for import staging authority.");
        StringAssert.Contains(module, "Repository-local core.hooksPath is forbidden for import staging authority.");
        StringAssert.Contains(module, "Repository-local core.untrackedCache is forbidden for import staging authority.");
        Assert.IsFalse(module.Contains("rev-parse $", StringComparison.Ordinal), "Object resolution must not use a mutable ref expression.");
    }

    [TestMethod]
    public void ImportAuthorityUsesExactPatchRolesCommandsAndOneManifestParse()
    {
        string module = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "verification", "CrossRouteImportManifest.Core.psm1"));
        foreach (string marker in new[]
        {
            "openvino-shared-integration", "destination-head", "gguf-verifier-openvino-integration",
            "task11-gguf-verifier", "Integration-base patch member is outside its exact authorized predecessor role.",
            "verificationCommands is not the exact hard-coded command set.", "-ManifestModel $manifest",
            "CrossRouteCappedMemoryStream", "StartSuspended", "CREATE_SUSPENDED", "PROC_THREAD_ATTRIBUTE_HANDLE_LIST",
            "hash-object', '-w', '--no-filters'", "update-index', '--add', '--cacheinfo'",
            "GIT_INDEX_FILE", "Publish-VerifiedTemporaryIndex", "temporary index state changed before atomic publication"
        })
            Assert.IsTrue(
                module.Contains(marker, StringComparison.Ordinal),
                $"Missing import authority/process marker: {marker}");

        Assert.AreEqual(1, Regex.Matches(module,
            @"(?m)^\s*\$manifest\s*=\s*Get-ManifestModel\s+-ManifestPath").Count,
            "The production verifier must parse the canonical manifest exactly once.");
    }

    [TestMethod]
    public void BatchDriverBindsProductionManifestToExplicitModuleRepository()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tests", "UnitTests",
            "GraniteEdgeAI.UnitTests", "Features", "ModelOptimization", "OptimizationImportManifestTests.cs"));
        const string beginning = "private const string BatchDriver = \"\"\"";
        int start = source.IndexOf(beginning, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "The import batch driver source was not found.");
        int end = source.IndexOf("\n        \"\"\";", start + beginning.Length, StringComparison.Ordinal);
        Assert.IsTrue(end > start, "The import batch driver terminator was not found.");
        string driver = source.Substring(start, end - start);
        Assert.IsTrue(
            driver.Contains("$RepositoryRoot", StringComparison.Ordinal),
            "The temporary batch driver must receive an explicit repository root.");
        StringAssert.Contains(driver, "Batch driver repository root does not match its exact module repository.");
        StringAssert.Contains(driver, "-StageVerified");
        Assert.IsFalse(driver.Contains("@('add',('--pathspec-from-file='", StringComparison.Ordinal),
            "The verifier's review pathspec must never become git-add staging authority.");
        Assert.IsFalse(driver.Contains("$repositoryRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)",
            StringComparison.Ordinal), "A temporary driver's PSScriptRoot is not repository authority.");
    }

    [TestMethod]
    public void VerifierBindsRepositoryIdentityAndRejectsWindowsAliasOrReparsePaths()
    {
        string module = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "verification", "CrossRouteImportManifest.Core.psm1"));
        foreach (string marker in new[]
        {
            "sourcePreHead", "sourcePostHead", "destinationPreHead", "destinationPostHead",
            "sourcePreTree", "sourcePostTree", "destinationPreTree", "destinationPostTree",
            "--porcelain=v1", "ReparsePoint", "NormalizationForm.FormC", "reserved device", "trailing dot or space"
        })
            Assert.IsTrue(module.Contains(marker, StringComparison.Ordinal), $"Missing repository/path hardening marker: {marker}");
    }

    [TestMethod]
    public void ProductionPoliciesRejectEveryPendingComponentBeforeEvidenceAccess()
    {
        string root = FindRepositoryRoot();
        foreach (string component in new[] { "UO1", "GgufRuntime", "OpenVino" })
        {
            ProcessResult result = RunPowerShell(root, 120,
                "-File", Path.Combine(root, "scripts", "verification", "Test-CrossRouteImportManifest.ps1"),
                "-Component", component, "-SourceRepository", root, "-DestinationRepository", root);
            Assert.AreNotEqual(0, result.ExitCode, result.Output);
            StringAssert.Contains(result.Output, $"Component {component} is Pending; only Verified components authorize import.");
        }
    }

    internal static ProcessResult RunPowerShell(string workingDirectory, int timeoutSeconds, params string[] arguments) =>
        Task1ProcessRunner.Run("powershell.exe", workingDirectory,
            ["-NoProfile", "-ExecutionPolicy", "Bypass", .. arguments], timeoutSeconds);

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

    internal sealed record ProcessResult(int ExitCode, string Output, byte[] StandardOutput);
}

internal static class Task1ProcessRunner
{
    private static readonly SemaphoreSlim ProcessGate = new(1, 1);
    private sealed record ExecutableIdentity(string Path, long Length, DateTime LastWriteTimeUtc, DateTime CreationTimeUtc, string Sha256);

    private sealed class Task1CappedMemoryStream : MemoryStream
    {
        private readonly long _maximumLength;

        internal Task1CappedMemoryStream(long maximumLength) => _maximumLength = maximumLength;

        private void Ensure(int count)
        {
            if (count < 0 || Length > _maximumLength - count)
                throw new IOException("Test process output exceeded its byte limit.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Ensure(count);
            base.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Ensure(count);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }
    }

    private sealed class Task1ProcessJob : IDisposable
    {
        private IntPtr _handle;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr job, int informationClass, IntPtr information, uint length);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimit { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass, SchedulingClass; }
        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimit { public BasicLimit BasicLimitInformation; public IoCounters IoInfo; public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }

        internal Task1ProcessJob()
        {
            _handle = CreateJobObject(IntPtr.Zero, null);
            if (_handle == IntPtr.Zero) throw new InvalidOperationException("Unable to create test process Job Object.");
            ExtendedLimit value = new();
            value.BasicLimitInformation.LimitFlags = 0x00002000;
            int size = Marshal.SizeOf<ExtendedLimit>();
            IntPtr memory = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, memory, false);
                if (!SetInformationJobObject(_handle, 9, memory, (uint)size))
                    throw new InvalidOperationException("Unable to configure test process Job Object.");
            }
            finally { Marshal.FreeHGlobal(memory); }
        }

        internal void Assign(Process process)
        {
            if (!AssignProcessToJobObject(_handle, process.Handle))
                throw new InvalidOperationException("Unable to assign test process to Job Object.");
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private sealed class Task1OwnedProcess : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int InheritHandle; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo { public int Size; public string? Reserved, Desktop, Title; public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags; public short ShowWindow, Reserved2; public IntPtr ReservedPointer, StandardInput, StandardOutput, StandardError; }
        [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr AttributeList; }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr readPipe, out IntPtr writePipe, ref SecurityAttributes attributes, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFile(string name, uint access, uint share, ref SecurityAttributes attributes, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string currentDirectory, ref StartupInfoEx startupInfo, out ProcessInformation information);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private const uint WaitObject0 = 0, WaitTimeout = 258, WaitFailed = uint.MaxValue;

        internal Process Process { get; }
        internal FileStream StandardOutput { get; }
        internal FileStream StandardError { get; }
        private IntPtr _nativeProcess;

        private Task1OwnedProcess(Process process, IntPtr nativeProcess, FileStream output, FileStream error)
        {
            Process = process; _nativeProcess = nativeProcess; StandardOutput = output; StandardError = error;
        }

        private static string Quote(string value)
        {
            if (value.Length != 0 && value.All(character => character != ' ' && character != '\t' && character != '"')) return value;
            StringBuilder result = new("\"");
            int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') { result.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
                result.Append('\\', slashes).Append(character); slashes = 0;
            }
            result.Append('\\', slashes * 2).Append('"');
            return result.ToString();
        }

        internal static Task1OwnedProcess Start(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments,
            Task1ProcessJob job,
            int constructionFaultDelayMilliseconds = -1)
        {
            SecurityAttributes security = new() { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = 1 };
            IntPtr outputRead = IntPtr.Zero, outputWrite = IntPtr.Zero, errorRead = IntPtr.Zero, errorWrite = IntPtr.Zero, input = IntPtr.Zero;
            IntPtr list = IntPtr.Zero, handleArray = IntPtr.Zero;
            ProcessInformation information = default;
            Process? process = null;
            FileStream? output = null, error = null;
            Microsoft.Win32.SafeHandles.SafeFileHandle? outputHandle = null, errorHandle = null;
            bool assignedToJob = false;
            try
            {
                if (!CreatePipe(out outputRead, out outputWrite, ref security, 0) || !SetHandleInformation(outputRead, 1, 0) ||
                    !CreatePipe(out errorRead, out errorWrite, ref security, 0) || !SetHandleInformation(errorRead, 1, 0))
                    throw new InvalidOperationException("Unable to create bounded test process pipes.");
                input = CreateFile("NUL", 0x80000000, 3, ref security, 3, 0x80, IntPtr.Zero);
                if (input == new IntPtr(-1)) throw new InvalidOperationException("Unable to open bounded test process input.");
                IntPtr listSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
                list = Marshal.AllocHGlobal(listSize);
                if (!InitializeProcThreadAttributeList(list, 1, 0, ref listSize)) throw new InvalidOperationException("Unable to initialize test handle allowlist.");
                handleArray = Marshal.AllocHGlobal(IntPtr.Size * 3);
                Marshal.WriteIntPtr(handleArray, 0, input); Marshal.WriteIntPtr(handleArray, IntPtr.Size, outputWrite); Marshal.WriteIntPtr(handleArray, IntPtr.Size * 2, errorWrite);
                if (!UpdateProcThreadAttribute(list, 0, new IntPtr(0x00020002), handleArray, new IntPtr(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero))
                    throw new InvalidOperationException("Unable to apply test inherited-handle allowlist.");
                StartupInfoEx startup = new(); startup.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>(); startup.StartupInfo.Flags = 0x100; startup.StartupInfo.StandardInput = input; startup.StartupInfo.StandardOutput = outputWrite; startup.StartupInfo.StandardError = errorWrite; startup.AttributeList = list;
                StringBuilder command = new(Quote(executable)); foreach (string argument in arguments) command.Append(' ').Append(Quote(argument));
                if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, true, 0x00080004, IntPtr.Zero, workingDirectory, ref startup, out information))
                    throw new InvalidOperationException("Unable to create suspended test process.");
                using Process processForAssignment = Process.GetProcessById((int)information.ProcessId);
                job.Assign(processForAssignment);
                assignedToJob = true;
                if (ResumeThread(information.Thread) == uint.MaxValue) throw new InvalidOperationException("Unable to resume Job-owned test process.");
                CloseHandle(information.Thread); information.Thread = IntPtr.Zero;
                CloseHandle(outputWrite); outputWrite = IntPtr.Zero; CloseHandle(errorWrite); errorWrite = IntPtr.Zero; CloseHandle(input); input = IntPtr.Zero;
                process = Process.GetProcessById((int)information.ProcessId);
                outputHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(outputRead, true); outputRead = IntPtr.Zero;
                output = new FileStream(outputHandle, FileAccess.Read); outputHandle = null;
                errorHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(errorRead, true); errorRead = IntPtr.Zero;
                error = new FileStream(errorHandle, FileAccess.Read); errorHandle = null;
                if (constructionFaultDelayMilliseconds >= 0)
                {
                    Thread.Sleep(constructionFaultDelayMilliseconds);
                    throw new InvalidOperationException("Injected Task1 owned-process construction failure.");
                }
                IntPtr native = information.Process; information.Process = IntPtr.Zero;
                Task1OwnedProcess result = new(process, native, output, error);
                process = null; output = null; error = null;
                return result;
            }
            catch
            {
                List<Exception> cleanupFailures = new();
                if (assignedToJob)
                {
                    try { job.Dispose(); }
                    catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
                }
                if (information.Process != IntPtr.Zero)
                {
                    uint waitResult = WaitForSingleObject(information.Process, assignedToJob ? 5_000u : 0u);
                    if (waitResult == WaitTimeout)
                    {
                        if (!TerminateProcess(information.Process, 1))
                        {
                            cleanupFailures.Add(new InvalidOperationException("Unable to terminate the exact partially constructed test process."));
                        }
                        else
                        {
                            waitResult = WaitForSingleObject(information.Process, 5_000u);
                            if (waitResult != WaitObject0)
                                cleanupFailures.Add(new InvalidOperationException("The exact partially constructed test process did not exit within five seconds."));
                        }
                    }
                    else if (waitResult == WaitFailed)
                    {
                        cleanupFailures.Add(new InvalidOperationException("Unable to wait for the exact partially constructed test process."));
                    }
                }
                try { output?.Dispose(); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
                try { error?.Dispose(); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
                try { process?.Dispose(); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
                if (cleanupFailures.Count != 0)
                    throw new AggregateException("Task1 owned process construction cleanup failed.", cleanupFailures);
                throw;
            }
            finally
            {
                outputHandle?.Dispose(); errorHandle?.Dispose();
                if (information.Thread != IntPtr.Zero) CloseHandle(information.Thread); if (information.Process != IntPtr.Zero) CloseHandle(information.Process);
                if (outputRead != IntPtr.Zero) CloseHandle(outputRead); if (outputWrite != IntPtr.Zero) CloseHandle(outputWrite); if (errorRead != IntPtr.Zero) CloseHandle(errorRead); if (errorWrite != IntPtr.Zero) CloseHandle(errorWrite); if (input != IntPtr.Zero && input != new IntPtr(-1)) CloseHandle(input);
                if (list != IntPtr.Zero) { DeleteProcThreadAttributeList(list); Marshal.FreeHGlobal(list); } if (handleArray != IntPtr.Zero) Marshal.FreeHGlobal(handleArray);
            }
        }

        internal int GetExitCode()
        {
            if (!GetExitCodeProcess(_nativeProcess, out uint exitCode) || exitCode == 259) throw new InvalidOperationException("Unable to read authoritative test process exit code.");
            return unchecked((int)exitCode);
        }

        public void Dispose()
        {
            StandardOutput.Dispose(); StandardError.Dispose(); Process.Dispose();
            if (_nativeProcess != IntPtr.Zero) { CloseHandle(_nativeProcess); _nativeProcess = IntPtr.Zero; }
        }
    }

    private static ExecutableIdentity CaptureExactRegularExecutable(string path, string label)
    {
        string fullPath = Path.GetFullPath(path);
        for (string? cursor = fullPath; !string.IsNullOrWhiteSpace(cursor); cursor = Path.GetDirectoryName(cursor))
        {
            FileAttributes attributes = File.GetAttributes(cursor);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"{label} has reparse-point ancestry: {cursor}");
            string? parent = Path.GetDirectoryName(cursor);
            if (string.Equals(parent, cursor, StringComparison.OrdinalIgnoreCase)) break;
        }
        FileInfo item = new(fullPath);
        if (!item.Exists || (item.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new InvalidOperationException($"{label} must be an exact regular non-reparse file.");
        string sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant();
        return new(fullPath, item.Length, item.LastWriteTimeUtc, item.CreationTimeUtc, sha256);
    }

    private static void AssertIdentityUnchanged(ExecutableIdentity before, ExecutableIdentity after, string label)
    {
        if (!string.Equals(before.Path, after.Path, StringComparison.OrdinalIgnoreCase) ||
            before.Length != after.Length ||
            before.LastWriteTimeUtc != after.LastWriteTimeUtc ||
            before.CreationTimeUtc != after.CreationTimeUtc ||
            !string.Equals(before.Sha256, after.Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException($"{label} identity changed during the bounded operation.");
    }

    private static void TerminateExactOwnedTree(Process process, int expectedProcessId, DateTime expectedStartTimeUtc)
    {
        if (process.HasExited) return;
        if (process.Id != expectedProcessId)
            throw new InvalidOperationException("Timed-out process identity no longer matches its captured PID.");
        using (Process live = Process.GetProcessById(expectedProcessId))
        {
            if (live.StartTime.ToUniversalTime() != expectedStartTimeUtc)
                throw new InvalidOperationException("Timed-out process PID/start-time ownership validation failed.");
        }
        string taskkillPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe");
        ExecutableIdentity taskkillBefore = CaptureExactRegularExecutable(taskkillPath, "System32 taskkill");
        ProcessStartInfo start = new(taskkillBefore.Path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("/PID");
        start.ArgumentList.Add(expectedProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("/T");
        start.ArgumentList.Add("/F");
        using Process killer = Process.Start(start) ?? throw new InvalidOperationException("Unable to start exact System32 taskkill.");
        Task<string> killerOutput = killer.StandardOutput.ReadToEndAsync();
        Task<string> killerError = killer.StandardError.ReadToEndAsync();
        if (!killer.WaitForExit(10_000))
        {
            killer.Kill(entireProcessTree: true);
            if (!killer.WaitForExit(5_000))
                throw new InvalidOperationException("Hung taskkill could not be terminated within five seconds.");
            if (!Task.WaitAll(new Task[] { killerOutput, killerError }, 5_000))
                throw new InvalidOperationException("Hung taskkill stream capture did not finish within five seconds.");
            throw new InvalidOperationException("System32 taskkill exceeded its ten-second bound.");
        }
        if (!Task.WaitAll(new Task[] { killerOutput, killerError }, 5_000))
            throw new InvalidOperationException("System32 taskkill stream capture did not finish within five seconds.");
        if (killer.ExitCode != 0)
            throw new InvalidOperationException($"System32 taskkill failed with exit code {killer.ExitCode}.");
        ExecutableIdentity taskkillAfter = CaptureExactRegularExecutable(taskkillBefore.Path, "System32 taskkill");
        AssertIdentityUnchanged(taskkillBefore, taskkillAfter, "System32 taskkill");
        if (!process.HasExited && !process.WaitForExit(5_000))
            throw new InvalidOperationException("Timed-out process root did not exit within five seconds after verified taskkill.");
        if (!process.HasExited)
            throw new InvalidOperationException("Timed-out process root remained alive after verified taskkill.");
    }

    internal static OptimizationImportManifestTests.ProcessResult Run(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        int timeoutSeconds = 120,
        bool forceVerifiedTimeoutFallback = false)
    {
        ProcessGate.Wait();
        try
        {
            string exactExecutable = executable.ToLowerInvariant() switch
            {
                "powershell.exe" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                "cmd.exe" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                "git.exe" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
                _ when Path.IsPathFullyQualified(executable) => executable,
                _ => throw new InvalidOperationException("Test executable must be one of the exact ruled executables or an absolute path.")
            };
            string executableLabel = Path.GetFileName(exactExecutable);
            ExecutableIdentity executableBefore = CaptureExactRegularExecutable(exactExecutable, "test executable");
            using Task1ProcessJob job = new();
            using Task1OwnedProcess ownedProcess = Task1OwnedProcess.Start(executableBefore.Path, workingDirectory, arguments, job);
            Process process = ownedProcess.Process;
            int processId = process.Id;
            DateTime processStartTimeUtc = process.StartTime.ToUniversalTime();
            using MemoryStream standardOutput = new Task1CappedMemoryStream(16 * 1024 * 1024);
            using MemoryStream standardError = new Task1CappedMemoryStream(2 * 1024 * 1024);
            Task outputTask = ownedProcess.StandardOutput.CopyToAsync(standardOutput);
            Task errorTask = ownedProcess.StandardError.CopyToAsync(standardError);
            if (!process.WaitForExit(checked(timeoutSeconds * 1000)))
            {
                bool requiresVerifiedFallback = forceVerifiedTimeoutFallback;
                if (!forceVerifiedTimeoutFallback)
                {
                    job.Dispose();
                    requiresVerifiedFallback = !process.HasExited && !process.WaitForExit(5_000);
                }
                if (requiresVerifiedFallback)
                {
                    TerminateExactOwnedTree(process, processId, processStartTimeUtc);
                    job.Dispose();
                    throw new TimeoutException($"{executableLabel} timed out; verified taskkill fallback terminated its exact process tree.");
                }
                throw new TimeoutException($"{executableLabel} timed out after {timeoutSeconds} seconds; its process tree was terminated.");
            }
            int exitCode = ownedProcess.GetExitCode();
            job.Dispose();
            if (!Task.WaitAll(new[] { outputTask, errorTask }, 5_000))
                throw new TimeoutException($"{executableLabel} output capture did not finish within five seconds of process exit.");
            byte[] bytes = standardOutput.ToArray();
            string output = Encoding.UTF8.GetString(bytes) + Encoding.UTF8.GetString(standardError.ToArray());
            ExecutableIdentity executableAfter = CaptureExactRegularExecutable(executableBefore.Path, "test executable");
            AssertIdentityUnchanged(executableBefore, executableAfter, "test executable");
            return new(exitCode, output, bytes);
        }
        finally
        {
            ProcessGate.Release();
        }
    }

    internal static void RunConstructionFaultProbe(
        string executable,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        int constructionFaultDelayMilliseconds)
    {
        ProcessGate.Wait();
        try
        {
            string exactExecutable = executable.ToLowerInvariant() switch
            {
                "powershell.exe" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                "cmd.exe" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                "git.exe" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
                _ when Path.IsPathFullyQualified(executable) => executable,
                _ => throw new InvalidOperationException("Test executable must be one of the exact ruled executables or an absolute path.")
            };
            ExecutableIdentity executableBefore = CaptureExactRegularExecutable(exactExecutable, "test executable");
            using Task1ProcessJob job = new();
            try
            {
                using Task1OwnedProcess unexpected = Task1OwnedProcess.Start(
                    executableBefore.Path,
                    workingDirectory,
                    arguments,
                    job,
                    constructionFaultDelayMilliseconds);
                throw new InvalidOperationException("The Task1 construction-fault probe did not activate.");
            }
            catch (InvalidOperationException exception) when (
                exception.Message == "Injected Task1 owned-process construction failure.")
            {
                ExecutableIdentity executableAfter = CaptureExactRegularExecutable(executableBefore.Path, "test executable");
                AssertIdentityUnchanged(executableBefore, executableAfter, "test executable");
                throw;
            }
        }
        finally
        {
            ProcessGate.Release();
        }
    }
}

internal static class Task1OwnedTemporaryCleanup
{
    private const uint DeleteAccess = 0x00010000, ReadAttributes = 0x80, WriteAttributes = 0x100, ShareReadWrite = 3, OpenExisting = 3,
        BackupSemantics = 0x02000000, OpenReparsePoint = 0x00200000, ReparsePoint = 0x400, ReadOnly = 1;
    [StructLayout(LayoutKind.Sequential)] private struct BasicInfo { public long CreationTime, LastAccessTime, LastWriteTime, ChangeTime; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct DispositionInfo { [MarshalAs(UnmanagedType.Bool)] public bool DeleteFile; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandleEx(IntPtr handle, int informationClass, out BasicInfo information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(IntPtr handle, int informationClass, ref BasicInfo information, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(IntPtr handle, int informationClass, ref DispositionInfo information, uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    internal static void Delete(string path, string requiredPrefix)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(fullPath), tempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullPath).StartsWith(requiredPrefix, StringComparison.Ordinal) ||
            Path.GetFileName(fullPath).Length != requiredPrefix.Length + 32)
            throw new InvalidOperationException("Refusing cleanup outside the exact operation-owned test root.");
        DeleteEntry(fullPath, DateTime.UtcNow.AddSeconds(10).Ticks);
    }

    private static void DeleteEntry(string path, long deadlineTicks)
    {
        if (DateTime.UtcNow.Ticks >= deadlineTicks) throw new TimeoutException("Exact GUID cleanup exceeded ten seconds.");
        IntPtr handle = CreateFile(path, DeleteAccess | ReadAttributes | WriteAttributes, ShareReadWrite, IntPtr.Zero, OpenExisting,
            BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (handle == new IntPtr(-1))
        {
            int error = Marshal.GetLastWin32Error();
            if (error is 2 or 3) return;
            throw new IOException($"Unable to open exact GUID cleanup entry (Win32 {error}).");
        }
        try
        {
            if (!GetFileInformationByHandleEx(handle, 0, out BasicInfo basic, (uint)Marshal.SizeOf<BasicInfo>()))
                throw new IOException("Unable to inspect exact GUID cleanup entry.");
            if ((basic.Attributes & ReparsePoint) == 0 && (basic.Attributes & 0x10) != 0)
                foreach (string entry in Directory.GetFileSystemEntries(path)) DeleteEntry(entry, deadlineTicks);
            if ((basic.Attributes & ReadOnly) != 0)
            {
                basic.Attributes &= ~ReadOnly;
                if (basic.Attributes == 0) basic.Attributes = 0x80;
                if (!SetFileInformationByHandle(handle, 0, ref basic, (uint)Marshal.SizeOf<BasicInfo>()))
                    throw new IOException("Unable to clear exact GUID cleanup attributes.");
            }
            DispositionInfo disposition = new() { DeleteFile = true };
            if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<DispositionInfo>()))
                throw new IOException("Unable to delete exact GUID cleanup entry.");
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}

[TestClass]
public sealed class OptimizationImportManifestVerifierProcessTests
{
    [TestMethod]
    public void JobObjectKillsAChildThatRetainsStreamsAfterItsParentExits()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"geai-import-job-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string started = Path.Combine(temporary, "started.txt");
            string escaped = Path.Combine(temporary, "escaped.txt");
            string childBatch = Path.Combine(temporary, "child.cmd");
            File.WriteAllText(childBatch, $"@echo off\r\necho started>\"{started}\"\r\nping -n 30 127.0.0.1 >nul\r\necho escaped>\"{escaped}\"\r\n", Encoding.ASCII);
            string driver = Path.Combine(temporary, "job-driver.ps1");
            File.WriteAllText(driver, """
                param([string]$ModulePath,[string]$ChildBatch,[string]$Started,[string]$Escaped)
                $ErrorActionPreference='Stop'
                Import-Module -Force -Name $ModulePath
                $cmd=Join-Path -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) -ChildPath 'cmd.exe'
                $stopwatch=[Diagnostics.Stopwatch]::StartNew()
                $result=Invoke-CrossRouteProcess -FilePath $cmd -WorkingDirectory (Split-Path -Parent $ChildBatch) -Arguments @('/d','/c','start','','/b',$cmd,'/d','/c',$ChildBatch) -TimeoutSeconds 15
                if($result.ExitCode-ne0){throw 'parent command failed'}
                if(-not[IO.File]::Exists($Started)){throw 'descendant did not start'}
                if([IO.File]::Exists($Escaped)){throw 'descendant survived the owned Job Object'}
                if($stopwatch.Elapsed.TotalSeconds-ge12){throw 'descendant stream ownership was not bounded'}
                'JOB_DESCENDANT_OK'
                """);
            string module = Path.Combine(root, "scripts", "verification", "CrossRouteImportManifest.Core.psm1");
            var result = OptimizationImportManifestTests.RunPowerShell(temporary, 30,
                "-File", driver, "-ModulePath", module, "-ChildBatch", childBatch, "-Started", started, "-Escaped", escaped);
            Assert.AreEqual(0, result.ExitCode, result.Output);
            StringAssert.Contains(result.Output, "JOB_DESCENDANT_OK");
        }
        finally
        {
            Task1OwnedTemporaryCleanup.Delete(temporary, "geai-import-job-");
        }
    }

    [TestMethod]
    public void RepositoryJunctionAncestryIsDeterministicallyRejected()
    {
        string root = OptimizationImportManifestTests.FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"geai-import-reparse-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string junction = Path.Combine(temporary, "repository-junction");
            var create = Task1ProcessRunner.Run("cmd.exe", temporary, ["/d", "/c", "mklink", "/J", junction, root], 30);
            Assert.AreEqual(0, create.ExitCode, "Junction coverage is mandatory and creation failed: " + create.Output);
            Assert.IsTrue((File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0, "mklink did not create a reparse point.");
            var result = OptimizationImportManifestTests.RunPowerShell(temporary, 30,
                "-File", Path.Combine(root, "scripts", "verification", "Test-CrossRouteImportManifest.ps1"),
                "-Component", "UO1", "-SourceRepository", root, "-DestinationRepository", junction);
            Assert.AreNotEqual(0, result.ExitCode, result.Output);
            StringAssert.Contains(result.Output, "ReparsePoint ancestry is forbidden");
        }
        finally
        {
            Task1OwnedTemporaryCleanup.Delete(temporary, "geai-import-reparse-");
        }
    }

    [TestMethod]
    public void AllImportVerificationCasesRunInOneBatchDriver()
    {
        List<ImportFixture> fixtures = [];
        string batchRoot = Path.Combine(Path.GetTempPath(), $"geai-import-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(batchRoot);
        try
        {
            List<BatchCase> cases = [];
            ImportFixture valid = AddFixture();
            cases.Add(new("valid-exact-adapted-created", valid.Repository, valid.ManifestPath, valid.SourceRepository, valid.Pathspec, true, false, valid.Policy));
            ImportFixture exactNull = AddFixture();
            exactNull.MutateManifest("exact-patch-group-null");
            cases.Add(new("exact-patch-group-null", exactNull.Repository, exactNull.ManifestPath, exactNull.SourceRepository, exactNull.Pathspec, true, false, exactNull.Policy));
            ImportFixture replaceRef = AddFixture();
            replaceRef.UseReplaceRefSubstitution();
            cases.Add(new("replace-ref-substitution-ignored", replaceRef.Repository, replaceRef.ManifestPath,
                replaceRef.SourceRepository, replaceRef.Pathspec, true, false, replaceRef.Policy));
            ImportFixture sentinel = AddFixture();
            byte[] sentinelBytes = Encoding.UTF8.GetBytes("pre-existing import pathspec sentinel");
            File.WriteAllBytes(sentinel.Pathspec, sentinelBytes);
            cases.Add(new("pre-existing-pathspec-sentinel", sentinel.Repository, sentinel.ManifestPath, sentinel.SourceRepository,
                sentinel.Pathspec, false, false, sentinel.Policy, Convert.ToBase64String(sentinelBytes), true));
            ImportFixture insidePathspec = AddFixture();
            cases.Add(new("pathspec-inside-repository", insidePathspec.Repository, insidePathspec.ManifestPath,
                insidePathspec.SourceRepository, Path.Combine(insidePathspec.Repository, "paths.bin"), false, false, insidePathspec.Policy, null, true));

            string[] manifestMutations =
            [
                "authority-name", "authority-ref", "authority-commit-decoy", "extra-authority", "missing-authority",
                "duplicate-authority-name", "merge-method", "extra-manifest-path", "missing-manifest-path",
                "extra-allowed-path", "duplicate-allowed-path", "undeclared-patch-base", "source-unauthorized",
                "source-mode", "source-identity-conflict", "source-symlink", "source-submodule", "source-missing", "source-blob", "source-tree",
                "result-hash", "worktree-result", "separate-base", "created-preexists", "malformed-patch", "extra-patch",
                "empty-patch", "forbidden-owned-path", "created-wrong-permitted-base", "adapted-result-mode-missing",
                "adapted-patch-group-absent", "adapted-patch-group-null", "exact-patch-group-non-null",
                "traversal", "absolute", "control", "alternate-data-stream", "reserved-device", "trailing-dot",
                "trailing-space", "backslash", "case-collision", "unicode-collision", "duplicate-json"
            ];
            foreach (string mutation in manifestMutations)
            {
                ImportFixture fixture = AddFixture();
                fixture.MutateManifest(mutation);
                cases.Add(new(mutation, fixture.Repository, fixture.ManifestPath, fixture.SourceRepository, fixture.Pathspec, false, false, fixture.Policy));
            }

            foreach (string mutation in new[] { "staged-blob", "staged-mode", "extra-staged", "rename-endpoint", "copy-endpoint" })
            {
                ImportFixture fixture = AddFixture();
                fixture.StageAll();
                fixture.MutateIndex(mutation);
                cases.Add(new(mutation, fixture.Repository, fixture.ManifestPath, fixture.SourceRepository, fixture.Pathspec, false, true, fixture.Policy));
            }

            foreach (string mode in new[] { "120000", "160000" })
            {
                ImportFixture fixture = AddFixture();
                fixture.UseGenuineForbiddenGitObject(mode);
                cases.Add(new($"genuine-mode-{mode}", fixture.Repository, fixture.ManifestPath, fixture.SourceRepository, fixture.Pathspec, false, false, fixture.Policy));
            }

            string casePath = Path.Combine(batchRoot, "cases.json");
            string driverPath = Path.Combine(batchRoot, "driver.ps1");
            Assert.AreEqual(56, cases.Count, "Fixed import verifier case inventory drifted.");
            File.WriteAllText(casePath, JsonSerializer.Serialize(cases));
            File.WriteAllText(driverPath, BatchDriver);
            string repositoryRoot = OptimizationImportManifestTests.FindRepositoryRoot();
            string modulePath = Path.Combine(repositoryRoot, "scripts", "verification", "CrossRouteImportManifest.Core.psm1");
            string source = Path.GetFullPath(Environment.GetEnvironmentVariable("GEAI_SOURCE_REPOSITORY") ?? @"C:\GEAI-LOQ");
            var result = OptimizationImportManifestTests.RunPowerShell(batchRoot, 600,
                "-File", driverPath, "-ModulePath", modulePath, "-RepositoryRoot", repositoryRoot,
                "-SourceRepository", source, "-CasePath", casePath);
            Assert.AreEqual(0, result.ExitCode, result.Output);
            StringAssert.Contains(result.Output, "IMPORT_BATCH_CASES=56");

            ImportFixture AddFixture()
            {
                ImportFixture fixture = new();
                fixtures.Add(fixture);
                return fixture;
            }
        }
        finally
        {
            foreach (ImportFixture fixture in fixtures) fixture.Dispose();
            Task1OwnedTemporaryCleanup.Delete(batchRoot, "geai-import-batch-");
        }
    }

    private sealed record BatchCase(
        string Name,
        string Repository,
        string ManifestPath,
        string SourceRepository,
        string Pathspec,
        bool ExpectedSuccess,
        bool VerifyStaged,
        FixturePolicy? Policy = null,
        string? ExpectedPathspecBase64 = null,
        bool WritePathspecOnFailure = false);

    private sealed record FixturePolicy(string FixturePolicyId, string ManifestName, string SourceRef, string SourceCommit, string[] PermittedCommits);

    private const string BatchDriver = """
        param([string]$ModulePath,[string]$RepositoryRoot,[string]$SourceRepository,[string]$CasePath)
        $ErrorActionPreference='Stop'
        $repositoryRoot=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path).TrimEnd('\','/')
        $moduleFullPath=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ModulePath -ErrorAction Stop).Path)
        $moduleRepository=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $moduleFullPath))
        if(-not[StringComparer]::OrdinalIgnoreCase.Equals($repositoryRoot,$moduleRepository)){throw'Batch driver repository root does not match its exact module repository.'}
        Import-Module -Force -Name $ModulePath
        $coreModule=Get-Module -Name CrossRouteImportManifest.Core
        & $coreModule {
          param($repositoryRoot)
          $productionManifest=Get-ManifestModel -ManifestPath (Join-Path -Path $repositoryRoot -ChildPath 'docs\handoffs\2026-08-26-cross-route-optimisation-import-manifest.json')
          Assert-AuthorityAndPolicy -Manifest $productionManifest
          foreach($selector in @('UO1','GgufRuntime','OpenVino')){
            $productionPolicy=Get-ApprovedComponentPolicy -Component $selector
            $record=@($productionManifest.components|Where-Object{[string]$_.name-ceq[string]$productionPolicy.manifestName})[0]
            Assert-ComponentPolicyBoundary -Policy $productionPolicy -DestinationPath @() -PatchPath @() -Record $record
            if(-not[StringComparer]::Ordinal.Equals((@($productionPolicy.allowedKinds)-join','),'Exact,Adapted,Created')){throw($selector+' destination kinds drifted')}
            if(@($productionPolicy.verificationCommands).Count-eq0){throw($selector+' hard-coded verification commands are empty')}
            Assert-VerificationCommandPolicy -Policy $productionPolicy -Command ([string[]]$productionPolicy.verificationCommands)
            $commandRejected=$false
            try{Assert-VerificationCommandPolicy -Policy $productionPolicy -Command @('arbitrary command')}catch{$commandRejected=[StringComparer]::Ordinal.Equals($_.Exception.Message,'verificationCommands is not the exact hard-coded command set.')}
            if(-not$commandRejected){throw($selector+' arbitrary verification command was accepted')}
            $probeBySelector=@{
              UO1=@('IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/OwnedProbe.cs','IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml','docs/handoffs/import-patches/uo1-v3-adaptation.patch')
              GgufRuntime=@('IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/OwnedProbe.cs','IBM Granite with TurboQuant (Intel)/Features/GgufRuntime/GgufInspectedModelLaunchFactory.cs','docs/handoffs/import-patches/gguf-runtime-integration.patch')
              OpenVino=@('IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/OwnedProbe.cs','tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/Optimization/OpenVinoV2TestPayload.cs','docs/handoffs/import-patches/openvino-v3-adaptation.patch')
            }
            $probe=$probeBySelector[$selector]
            Assert-ComponentPolicyBoundary -Policy $productionPolicy -DestinationPath @($probe[0]) -PatchPath @($probe[2]) -Record $record
            foreach($negative in @(
              [pscustomobject]@{Path=[string]$probe[1];Message=('Destination path is excluded by the hard-coded component policy: '+$probe[1])}
              [pscustomobject]@{Path='outside/component-boundary.txt';Message='Destination path is outside the hard-coded component ownership closure: outside/component-boundary.txt'}
            )){
              $boundaryRejected=$false
              try{Assert-ComponentPolicyBoundary -Policy $productionPolicy -DestinationPath @($negative.Path) -PatchPath @() -Record $record}catch{$boundaryRejected=[StringComparer]::Ordinal.Equals($_.Exception.Message,$negative.Message)}
              if(-not$boundaryRejected){throw($selector+' production destination policy negative probe failed')}
            }
            $patchRejected=$false
            try{Assert-ComponentPolicyBoundary -Policy $productionPolicy -DestinationPath @() -PatchPath @('docs/handoffs/import-patches/not-authorized.patch') -Record $record}catch{$patchRejected=[StringComparer]::Ordinal.Equals($_.Exception.Message,'Patch path is outside the hard-coded component patch policy: docs/handoffs/import-patches/not-authorized.patch')}
            if(-not$patchRejected){throw($selector+' production patch policy negative probe failed')}
          }
          $uo1=Get-ApprovedComponentPolicy -Component UO1
          if($uo1.createdBaseCommit-cne'092589c38981ad86bb73c7c97dff01ab8b5a6c8e'-or$uo1.patchPathRules[0]-cne'docs/handoffs/import-patches/uo1-v3-adaptation.patch'){throw'UO1 base/patch policy drifted'}
          $gguf=Get-ApprovedComponentPolicy -Component GgufRuntime
          if($gguf.createdBaseCommit-cne'092589c38981ad86bb73c7c97dff01ab8b5a6c8e'-or$gguf.patchPathRules[0]-cne'docs/handoffs/import-patches/gguf-runtime-integration.patch'){throw'GGUF base/patch policy drifted'}
          $openvino=Get-ApprovedComponentPolicy -Component OpenVino
          $sharedRole=$openvino.integrationBaseRoles['docs/handoffs/import-patches/openvino-shared-integration.patch']
          $ggufRole=$openvino.integrationBaseRoles['docs/handoffs/import-patches/gguf-verifier-openvino-integration.patch']
          if($sharedRole.role-cne'destination-head'-or$ggufRole.role-cne'task11-gguf-verifier'){throw'OpenVINO predecessor roles drifted'}
          $task13Created=@(
            'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoPackagedToolContextResolver.cs'
            'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/OpenVinoProductionComposition.cs'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/Features/OpenVinoRoute/OpenVinoProductionCompositionTests.cs'
          )
          $contractProject='tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GraniteEdgeAI.OpenVino.Contracts.Tests.csproj'
          $expectedSharedMembers=@(
            'IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/**'
            'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
            'IBM Granite with TurboQuant (Intel).slnx'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'
          )+$task13Created+@($contractProject)
          [void](Assert-ExactStringSet -Actual ([string[]]$sharedRole.memberRules) -Expected ([string[]]$expectedSharedMembers) -Name 'OpenVINO destination-head member rules')
          $expectedIntegrationOnly=@(
            'IBM Granite with TurboQuant (Intel)/OpenVino.ConverterPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/OpenVino.TurboQuantPackaging.targets'
            'IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Execution/OpenVino/**'
            'IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj'
            'IBM Granite with TurboQuant (Intel).slnx'
            'tests/UnitTests/GraniteEdgeAI.UnitTests/GraniteEdgeAI.UnitTests.csproj'
            'scripts/gguf-runtime/Invoke-GgufChatVerification.ps1'
          )+$task13Created+@($contractProject)
          [void](Assert-ExactStringSet -Actual ([string[]]$openvino.integrationOnlyPaths) -Expected ([string[]]$expectedIntegrationOnly) -Name 'OpenVINO integration-only paths')
          foreach($createdPath in $task13Created){
            if(-not(Test-AnyPathRule -Path $createdPath -Rule ([string[]]$sharedRole.memberRules))){throw('Task 13 Created path lacks destination-head authority: '+$createdPath)}
            if(-not(Test-AnyPathRule -Path $createdPath -Rule ([string[]]$openvino.integrationOnlyPaths))){throw('Task 13 Created path is not integration-only: '+$createdPath)}
          }
          if(-not(Test-AnyPathRule -Path $contractProject -Rule ([string[]]$sharedRole.memberRules))){throw'Current OpenVINO contract-test project lacks destination-head authority'}
          if(-not(Test-AnyPathRule -Path $contractProject -Rule ([string[]]$openvino.integrationOnlyPaths))){throw'Current OpenVINO contract-test project is not integration-only'}
          if(Test-AnyPathRule -Path 'IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/Integration/UnruledComposition.cs' -Rule ([string[]]$sharedRole.memberRules)){throw'OpenVINO destination-head role widened beyond exact Created files'}
          if(Test-AnyPathRule -Path 'workers/OpenVinoOfficial.Worker/Program.cs' -Rule ([string[]]$sharedRole.memberRules)){throw'OpenVINO shared predecessor role widened into pinned route files'}
          if(-not(Test-AnyPathRule -Path 'scripts/gguf-runtime/Invoke-GgufChatVerification.ps1' -Rule ([string[]]$ggufRole.memberRules))){throw'OpenVINO Task-11 predecessor role is vacuous'}
          $contractSources=@(
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ArchitectureBoundaryTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/DependencyLockContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/FixtureContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/GpuDeviceContractTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolJsonTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/ProtocolSequenceTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/SupportCodeTests.cs'
            'tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/Task7ProtocolExtensionTests.cs'
          )
          foreach($contractSource in $contractSources){Assert-ComponentPolicyBoundary -Policy $openvino -DestinationPath @($contractSource) -PatchPath @() -Record $record}
          Assert-ComponentPolicyBoundary -Policy $openvino -DestinationPath @($contractProject) -PatchPath @() -Record $record
          $packageLock='tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/packages.lock.json'
          if(-not(Test-AnyPathRule -Path $packageLock -Rule ([string[]]$openvino.excludedSharedPaths))){throw'OpenVINO contract packages.lock.json is not explicitly excluded'}
          $unruledRejected=$false
          try{Assert-ComponentPolicyBoundary -Policy $openvino -DestinationPath @('tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/UnruledContractTests.cs') -PatchPath @() -Record $record}catch{$unruledRejected=$_.Exception.Message-ceq'Destination path is outside the hard-coded component ownership closure: tests/ContractTests/GraniteEdgeAI.OpenVino.Contracts.Tests/UnruledContractTests.cs'}
          if(-not$unruledRejected){throw'OpenVINO contract-test source policy accepted an unruled adjacent file'}
        } $repositoryRoot
        $emptySha256=Get-Sha256 -Bytes ([byte[]]::new(0))
        if($emptySha256-cne'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'){throw 'Empty SHA-256 contract mismatch.'}
        $nativeExit=Invoke-CrossRouteProcess -FilePath (Get-Command cmd.exe).Source -WorkingDirectory $repositoryRoot -Arguments @('/d','/c','exit 7') -TimeoutSeconds 10
        if($nativeExit.ExitCode-ne7){throw 'Native process exit code was not authoritative.'}
        $verificationRoot=Split-Path -Parent $ModulePath
        $structurePaths=@(
          $ModulePath
          (Join-Path -Path $verificationRoot -ChildPath 'PackagedCheckpoint.Core.psm1')
          (Join-Path -Path $verificationRoot -ChildPath 'Assert-ChangedPaths.ps1')
          (Join-Path -Path $verificationRoot -ChildPath 'Test-CrossRouteImportManifest.ps1')
          (Join-Path -Path $verificationRoot -ChildPath 'Invoke-PackagedTestCheckpoint.ps1')
        )
        Assert-VerificationPowerShellStructure -Path $structurePaths|Out-Null
        $cases=Get-Content -Raw -LiteralPath $CasePath|ConvertFrom-Json
        $expectedErrorFragments=@{
          'pre-existing-pathspec-sentinel'='WriteAllowedPathspec target must be absent; a pre-existing sentinel is never overwritten.'
          'pathspec-inside-repository'='WriteAllowedPathspec must be outside the repository worktree.'
          'authority-name'='Authority name missing, duplicated, or rebound: planning-base'
          'authority-ref'='Authority tuple mismatch: planning-base'
          'authority-commit-decoy'='Authority tuple mismatch: planning-base'
          'extra-authority'='Authority table must contain exactly six records.'
          'missing-authority'='Authority table must contain exactly six records.'
          'duplicate-authority-name'='Authority name missing, duplicated, or rebound: canonical-optimisation-ui'
          'merge-method'='Import method policy is not the exact bounded policy.'
          'extra-manifest-path'='policy.manifestPaths is not the exact required set.'
          'missing-manifest-path'='policy.manifestPaths is not the exact required set.'
          'extra-allowed-path'='allowedDestinationPaths is not the exact required set.'
          'duplicate-allowed-path'='allowedDestinationPaths contains an OrdinalIgnoreCase or Unicode normalization collision: imports/exact.txt'
          'undeclared-patch-base'='Commit is not permitted for this component and evidence kind.'
          'source-unauthorized'='Commit is not permitted for this component and evidence kind.'
          'source-mode'='Source commit:path has a forbidden or mismatched Git mode/type.'
          'source-identity-conflict'='Source records make differing identity claims for the same commit and path: README.md'
          'source-symlink'='Source declares a forbidden Git mode.'
          'source-submodule'='Source declares a forbidden Git mode.'
          'source-missing'='Required commit:path is absent: 8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8:missing-source-path'
          'source-blob'='Declared source blob does not match commit:path.'
          'source-tree'='Source commit:path has a forbidden or mismatched Git mode/type.'
          'result-hash'='Reconstructed or destination result SHA-256 differs.'
          'worktree-result'='Exact destination bytes differ from commit:path:blob authority.'
          'separate-base'='Adapted patch base is not the exact hard-coded authority commit/tree.'
          'created-preexists'='Created destination already exists at the checked integration base.'
          'malformed-patch'='Integration patch failed structured git apply --check.'
          'extra-patch'='patch reconstructed paths is not the exact required set.'
          'empty-patch'='Integration patch must be non-empty.'
          'forbidden-owned-path'='Destination path is excluded by the hard-coded component policy: IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml'
          'created-wrong-permitted-base'='Created patch base must be the exact planning authority commit/tree.'
          'adapted-result-mode-missing'='Destination result mode must be a regular-file mode.'
          'adapted-patch-group-absent'='Adapted/Created destination lacks exactly one patch group.'
          'adapted-patch-group-null'='Adapted/Created destination lacks exactly one patch group.'
          'exact-patch-group-non-null'='Exact destinations cannot use a patch group.'
          'traversal'='Absolute, non-canonical, control-bearing, or traversing Git path rejected.'
          'absolute'='Absolute, non-canonical, control-bearing, or traversing Git path rejected.'
          'control'='Absolute, non-canonical, control-bearing, or traversing Git path rejected.'
          'alternate-data-stream'='Absolute, non-canonical, control-bearing, or traversing Git path rejected.'
          'reserved-device'='A Git path segment cannot use a reserved device name.'
          'trailing-dot'='A Git path segment cannot have a trailing dot or space.'
          'trailing-space'='A Git path segment cannot have a trailing dot or space.'
          'backslash'='A canonical Git path cannot contain a backslash.'
          'case-collision'='allowedDestinationPaths contains an OrdinalIgnoreCase or Unicode normalization collision: IMPORTS/exact.txt'
          'unicode-collision'='A Git path must use Unicode NormalizationForm.FormC.'
          'duplicate-json'='Duplicate JSON property rejected: components'
          'staged-blob'='Staged blob differs from verified worktree bytes: imports/exact.txt'
          'staged-mode'='Staged destination identity differs: imports/exact.txt'
          'extra-staged'='staged path endpoints is not the exact required set.'
          'rename-endpoint'='staged path endpoints is not the exact required set.'
          'copy-endpoint'='staged path endpoints is not the exact required set.'
          'genuine-mode-120000'='Source declares a forbidden Git mode.'
          'genuine-mode-160000'='Source declares a forbidden Git mode.'
        }
        $negativeNames=@($cases|Where-Object{-not$_.ExpectedSuccess}|ForEach-Object{[string]$_.Name})
        if($negativeNames.Count-ne53-or$expectedErrorFragments.Count-ne53){throw 'fixed negative import rejection inventory drifted'}
        foreach($negativeName in $negativeNames){if(-not$expectedErrorFragments.ContainsKey($negativeName)){throw('negative case lacks an exact rejection contract: '+$negativeName)}}
        foreach($contractName in $expectedErrorFragments.Keys){if($contractName-cnotin$negativeNames){throw('orphaned rejection contract: '+$contractName)}}
        $configurationProbeRepository=[string]$cases[0].Repository
        & $coreModule {
          param($repository)
          Assert-SafeImportRepositoryConfiguration -Repository $repository
          foreach($probe in @(
            [pscustomobject]@{Key='core.fsmonitor';Value='true';Message='Repository-local core.fsmonitor is forbidden for import staging authority.'}
            [pscustomobject]@{Key='core.hooksPath';Value='hooks';Message='Repository-local core.hooksPath is forbidden for import staging authority.'}
            [pscustomobject]@{Key='core.untrackedCache';Value='true';Message='Repository-local core.untrackedCache is forbidden for import staging authority.'}
          )){
            [void](Invoke-CrossRouteGit -Repository $repository -Arguments @('config','--local',[string]$probe.Key,[string]$probe.Value))
            $rejected=$false
            try{Assert-SafeImportRepositoryConfiguration -Repository $repository}catch{$rejected=[StringComparer]::Ordinal.Equals($_.Exception.Message,[string]$probe.Message)}
            finally{[void](Invoke-CrossRouteGit -Repository $repository -Arguments @('config','--local','--unset-all',[string]$probe.Key) -AllowFailure)}
            if(-not$rejected){throw('repository-local configuration probe did not receive its exact rejection: '+$probe.Key)}
            Assert-SafeImportRepositoryConfiguration -Repository $repository
            $configPath=Join-Path -Path $repository -ChildPath '.git\config'
            [IO.File]::AppendAllText($configPath,("`n[core]`n`t"+$probe.Key.Substring(5)+" =`n"),(New-Object Text.UTF8Encoding($false)))
            $emptyRejected=$false
            try{Assert-SafeImportRepositoryConfiguration -Repository $repository}catch{$emptyRejected=[StringComparer]::Ordinal.Equals($_.Exception.Message,[string]$probe.Message)}
            finally{[void](Invoke-CrossRouteGit -Repository $repository -Arguments @('config','--local','--unset-all',[string]$probe.Key) -AllowFailure)}
            if(-not$emptyRejected){throw('explicit-empty repository-local configuration probe did not receive its exact rejection: '+$probe.Key)}
            Assert-SafeImportRepositoryConfiguration -Repository $repository
          }
        } $configurationProbeRepository
        $publicationRoot=Join-Path -Path (Split-Path -Parent $CasePath) -ChildPath ('pathspec-binding-'+[Guid]::NewGuid().ToString('N'))
        $movedRoot=$publicationRoot+'-moved'
        [IO.Directory]::CreateDirectory($publicationRoot)|Out-Null
        try{
          $publicationRejected=& $coreModule {
            param($publicationRoot,$movedRoot,$repository)
            $target=Assert-PathspecTarget -Path (Join-Path -Path $publicationRoot -ChildPath 'review.bin') -Repository @($repository)
            [IO.Directory]::Move($publicationRoot,$movedRoot)
            [IO.Directory]::CreateDirectory($publicationRoot)|Out-Null
            try{Write-AtomicPathspec -Target $target -Bytes ([byte[]](1,2,3));return $false}catch{return [StringComparer]::Ordinal.Equals($_.Exception.Message,'Pathspec parent identity changed before publication.')}
          } $publicationRoot $movedRoot $configurationProbeRepository
          if(-not$publicationRejected){throw 'Pathspec publication parent-rebind probe did not receive its exact rejection.'}
        }finally{
          if([IO.Directory]::Exists($publicationRoot)){[IO.Directory]::Delete($publicationRoot)}
          if([IO.Directory]::Exists($movedRoot)){[IO.Directory]::Delete($movedRoot)}
        }
        $failures=New-Object 'Collections.Generic.List[string]'
        function Get-Identity {
          [CmdletBinding()]
          param([Parameter(Mandatory)][string]$Repository)
          [pscustomobject]@{
            Head=(Invoke-CrossRouteGit -Repository $Repository -Arguments @('rev-parse','HEAD')).Stdout.Trim()
            Tree=(Invoke-CrossRouteGit -Repository $Repository -Arguments @('rev-parse','HEAD^{tree}')).Stdout.Trim()
            Status=[Convert]::ToBase64String((Invoke-CrossRouteGit -Repository $Repository -Arguments @('status','--porcelain=v1','-z','--untracked-files=all')).StdoutBytes)
          }
        }
        function Assert-Identity {
          [CmdletBinding()]
          param([Parameter(Mandatory)]$Before,[Parameter(Mandatory)]$After)
          if($Before.Head-cne$After.Head-or$Before.Tree-cne$After.Tree-or$Before.Status-cne$After.Status){throw 'repository HEAD/tree/status mutated during verification'}
        }
        function Invoke-FixtureVerification {
          [CmdletBinding()]
          param([Parameter(Mandatory)]$Case,[Parameter(Mandatory)]$Policy,[switch]$VerifyStaged,[switch]$WritePathspec,[switch]$StageVerified)
          & $coreModule {
            param($Case,$Policy,$VerifyStaged,$WritePathspec,$StageVerified)
            $effective=Get-EffectiveComponentPolicy -Component UO1 -Policy $Policy
            $arguments=@{
              Component='UO1'
              SourceRepository=[string]$Case.SourceRepository
              DestinationRepository=[string]$Case.Repository
              ManifestPath=[string]$Case.ManifestPath
              Policy=$effective
              VerifyStaged=[bool]$VerifyStaged
              StageVerified=[bool]$StageVerified
            }
            if($WritePathspec){$arguments.WriteAllowedPathspec=[string]$Case.Pathspec}
            Invoke-CrossRouteImportVerificationInternal @arguments|Out-Null
          } $Case $Policy ([bool]$VerifyStaged) ([bool]$WritePathspec) ([bool]$StageVerified)
        }
        foreach($case in @($cases)){
          try{
            if($null-eq$case.Policy){throw 'fixture case omitted its strictly internal policy'}
            $activePolicy=$case.Policy
            $sourceBefore=Get-Identity -Repository $case.SourceRepository
            $destinationBefore=Get-Identity -Repository $case.Repository
            if($case.ExpectedSuccess){
              Invoke-FixtureVerification -Case $case -Policy $activePolicy -WritePathspec
              Assert-Identity -Before $sourceBefore -After (Get-Identity -Repository $case.SourceRepository)
              Assert-Identity -Before $destinationBefore -After (Get-Identity -Repository $case.Repository)
              Invoke-FixtureVerification -Case $case -Policy $activePolicy -StageVerified
              Invoke-FixtureVerification -Case $case -Policy $activePolicy -VerifyStaged
            }else{
              $rejected=$false
              $rejectionMessage=$null
              try{
                if($case.VerifyStaged){Invoke-FixtureVerification -Case $case -Policy $activePolicy -VerifyStaged}
                elseif($case.WritePathspecOnFailure){Invoke-FixtureVerification -Case $case -Policy $activePolicy -WritePathspec}
                else{Invoke-FixtureVerification -Case $case -Policy $activePolicy}
              }catch{$rejected=$true;$rejectionMessage=$_.Exception.Message}
              if(-not$rejected){throw 'mutation unexpectedly verified'}
              $expectedFragment=[string]$expectedErrorFragments[[string]$case.Name]
              if(-not[StringComparer]::Ordinal.Equals($rejectionMessage,$expectedFragment)){throw('expected exact rejection "'+$expectedFragment+'" but received "'+$rejectionMessage+'"')}
              Assert-Identity -Before $sourceBefore -After (Get-Identity -Repository $case.SourceRepository)
              Assert-Identity -Before $destinationBefore -After (Get-Identity -Repository $case.Repository)
              if($null-ne$case.ExpectedPathspecBase64){
                $actual=[Convert]::ToBase64String([IO.File]::ReadAllBytes($case.Pathspec))
                if($actual-cne[string]$case.ExpectedPathspecBase64){throw 'pre-existing import pathspec sentinel changed'}
              }
            }
          }catch{$failures.Add($case.Name+': '+$_.Exception.Message)}
        }
        if($failures.Count-ne0){throw($failures-join"`n")}
        if(@($cases).Count-ne56){throw('fixed import case count drifted: '+@($cases).Count)}
        'IMPORT_BATCH_CASES=56'
        """;

    private sealed class ImportFixture : IDisposable
    {
        private const string Planning = "092589c38981ad86bb73c7c97dff01ab8b5a6c8e";
        private const string Ui = "8d304d765cf8138ae68e0e8d3afa9f4ad2bad9a8";
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"geai-import-fixture-{Guid.NewGuid():N}");
        private string _source;
        private JsonObject _manifest = null!;
        public string SourceRepository => _source;
        public FixturePolicy? Policy { get; private set; }
        public string Repository => Path.Combine(_root, "destination");
        public string ManifestPath => Path.Combine(Repository, "docs", "handoffs", "2026-08-26-cross-route-optimisation-import-manifest.json");
        public string Pathspec => Path.Combine(_root, "paths.bin");

        public ImportFixture()
        {
            _source = Path.GetFullPath(Environment.GetEnvironmentVariable("GEAI_SOURCE_REPOSITORY") ?? @"C:\GEAI-LOQ");
            Directory.CreateDirectory(Repository);
            RunGit(Repository, "init", "--quiet");
            RunGit(Repository, "config", "user.email", "tests@example.invalid");
            RunGit(Repository, "config", "user.name", "Tests");
            RunGit(Repository, "config", "core.autocrlf", "false");
            File.WriteAllText(Path.Combine(Repository, ".gitignore"), "paths.bin\n");
            RunGit(Repository, "add", ".gitignore");
            RunGit(Repository, "commit", "--quiet", "-m", "fixture base");
            BuildValidFixture();
            Policy = new("internal-controlled-uo1-v1", "UO1", "origin/feature/cross-route-optimisation-ui-v1", Ui, [Ui, Planning]);
        }

        public void StageAll() => RunGit(Repository, "add", "--all");

        public void UseGenuineForbiddenGitObject(string mode)
        {
            string source = Path.Combine(_root, $"source-{mode}");
            Directory.CreateDirectory(source);
            RunGit(source, "init", "--quiet");
            RunGit(source, "config", "user.email", "tests@example.invalid");
            RunGit(source, "config", "user.name", "Tests");
            RunGit(source, "config", "core.autocrlf", "false");
            File.WriteAllText(Path.Combine(source, "seed.txt"), "seed");
            RunGit(source, "add", "seed.txt");
            RunGit(source, "commit", "--quiet", "-m", "seed");

            string path = mode == "120000" ? "imports/genuine-link" : "imports/genuine-gitlink";
            string objectId;
            if (mode == "120000")
            {
                string target = Path.Combine(source, "link-target.txt");
                File.WriteAllText(target, "seed.txt");
                objectId = RunGitOutput(source, "hash-object", "-w", "link-target.txt");
            }
            else if (mode == "160000")
            {
                objectId = RunGitOutput(source, "rev-parse", "HEAD");
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            RunGit(source, "update-index", "--add", "--cacheinfo", $"{mode},{objectId},{path}");
            RunGit(source, "commit", "--quiet", "-m", $"genuine {mode}");
            string commit = RunGitOutput(source, "rev-parse", "HEAD");
            Assert.AreEqual(mode, RunGitOutput(source, "ls-tree", commit, "--", path).Split(' ')[0], "Fixture must contain the genuine Git object mode.");

            _source = source;
            Policy = new("internal-controlled-uo1-v1", "UO1", "refs/heads/fixture", commit, [commit]);
            JsonObject component = _manifest["components"]![0]!.AsObject();
            component["sourceRef"] = Policy.SourceRef;
            component["sourceCommit"] = commit;
            component["sources"] = new JsonArray(new JsonObject
            {
                ["commit"] = commit,
                ["path"] = path,
                ["mode"] = mode,
                ["blob"] = objectId,
                ["sha256"] = new string('0', 64),
                ["destination"] = "imports/forbidden-object"
            });
            component["destinations"] = new JsonArray(new JsonObject
            {
                ["path"] = "imports/forbidden-object",
                ["kind"] = "Exact",
                ["resultMode"] = "100644",
                ["resultSha256"] = new string('0', 64)
            });
            component["allowedDestinationPaths"] = new JsonArray("imports/forbidden-object");
            WriteBytes("imports/forbidden-object", []);
            WriteManifest();
        }

        public void UseReplaceRefSubstitution()
        {
            string source = Path.Combine(_root, "source-replace-ref");
            var clone = Task1ProcessRunner.Run("git.exe", _root,
                ["clone", "--quiet", "--no-checkout", "--shared", _source, source]);
            Assert.AreEqual(0, clone.ExitCode, clone.Output);
            RunGit(source, "replace", Ui, Planning);
            Assert.AreEqual(Planning, RunGitOutput(source, "rev-parse", $"refs/replace/{Ui}"), "Fixture replace ref must actively substitute the pinned object.");
            _source = source;
        }

        public void MutateIndex(string mutation)
        {
            string exact = Path.Combine(Repository, "imports", "exact.txt");
            switch (mutation)
            {
                case "staged-blob":
                    File.WriteAllText(exact, "wrong index bytes"); RunGit(Repository, "add", "imports/exact.txt");
                    File.WriteAllBytes(exact, ReadSourceBlob("954ebaa8db899ec37bfcf8c8b8813986c5bf9d0b")); break;
                case "staged-mode": RunGit(Repository, "update-index", "--chmod=+x", "imports/exact.txt"); break;
                case "extra-staged": File.WriteAllText(Path.Combine(Repository, "extra.txt"), "extra"); RunGit(Repository, "add", "extra.txt"); break;
                case "rename-endpoint": RunGit(Repository, "mv", "imports/exact.txt", "renamed.txt"); File.WriteAllBytes(exact, ReadSourceBlob("954ebaa8db899ec37bfcf8c8b8813986c5bf9d0b")); break;
                case "copy-endpoint": File.Copy(exact, Path.Combine(Repository, "copied.txt")); RunGit(Repository, "add", "copied.txt"); break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        }

        public void MutateManifest(string mutation)
        {
            JsonArray authorities = _manifest["authorities"]!.AsArray();
            JsonObject component = _manifest["components"]![0]!.AsObject();
            JsonArray groups = component["integrationPatches"]!.AsArray();
            JsonArray destinations = component["destinations"]!.AsArray();
            JsonArray sources = component["sources"]!.AsArray();
            switch (mutation)
            {
                case "authority-name": authorities[0]!["name"] = "renamed"; break;
                case "authority-ref": authorities[0]!["ref"] = "refs/heads/decoy"; break;
                case "authority-commit-decoy": authorities[0]!["commit"] = new string('0', 40); _manifest["decoy"] = Planning; break;
                case "extra-authority": authorities.Add(new JsonObject { ["name"]="extra", ["ref"]="extra", ["commit"]=new string('1',40) }); break;
                case "missing-authority": authorities.RemoveAt(authorities.Count - 1); break;
                case "duplicate-authority-name": authorities[1]!["name"] = "PLANNING-BASE"; break;
                case "merge-method": _manifest["policy"]!["importMethod"] = "cherry-pick and merge bounded import"; break;
                case "extra-manifest-path": _manifest["policy"]!["manifestPaths"]!.AsArray().Add("extra.json"); break;
                case "missing-manifest-path": _manifest["policy"]!["manifestPaths"]!.AsArray().RemoveAt(1); break;
                case "extra-allowed-path": component["allowedDestinationPaths"]!.AsArray().Add("extra.txt"); break;
                case "duplicate-allowed-path": component["allowedDestinationPaths"]!.AsArray().Add("imports/exact.txt"); break;
                case "undeclared-patch-base": groups[1]!["baseCommit"] = "bacb3f4106e0191b05b870358342f8158765396d"; groups[1]!["baseTree"] = RunGitOutput(_source, "rev-parse", "bacb3f4106e0191b05b870358342f8158765396d^{tree}"); break;
                case "source-unauthorized": sources[0]!["commit"] = "bacb3f4106e0191b05b870358342f8158765396d"; break;
                case "source-mode": sources[0]!["mode"] = "100755"; sources[1]!["mode"] = "100755"; break;
                case "source-identity-conflict": sources[1]!["mode"] = "100755"; break;
                case "source-symlink": sources[0]!["mode"] = "120000"; sources[1]!["mode"] = "120000"; break;
                case "source-submodule": sources[0]!["mode"] = "160000"; sources[1]!["mode"] = "160000"; break;
                case "source-missing": sources[0]!["path"] = "missing-source-path"; break;
                case "source-blob": sources[0]!["blob"] = new string('0', 40); sources[1]!["blob"] = new string('0', 40); break;
                case "source-tree": sources[0]!["path"] = "docs"; sources[0]!["blob"] = RunGitOutput(_source, "rev-parse", $"{Ui}:docs"); break;
                case "result-hash": destinations[1]!["resultSha256"] = new string('0',64); break;
                case "worktree-result": File.WriteAllText(Path.Combine(Repository, "imports", "exact.txt"), "wrong worktree result"); break;
                case "separate-base": groups[0]!["baseCommit"] = Planning; groups[0]!["baseTree"] = RunGitOutput(_source, "rev-parse", $"{Planning}^{{tree}}"); break;
                case "created-preexists":
                    byte[] baseReadme = RunGitBytes(_source, "show", $"{Planning}:README.md");
                    byte[] changedReadme = [.. baseReadme, .. Encoding.UTF8.GetBytes("\nintegration-owned change\n")];
                    byte[] modifyPatch = CreatePatch("created-preexists", "README.md", baseReadme, changedReadme, false);
                    WriteBytes("README.md", changedReadme); WriteBytes("docs/handoffs/import-patches/created.patch", modifyPatch);
                    destinations[2]!["path"] = "README.md"; destinations[2]!["resultSha256"] = Sha(changedReadme); component["allowedDestinationPaths"]![2] = "README.md"; groups[1]!["patchSha256"] = Sha(modifyPatch); break;
                case "malformed-patch":
                    byte[] malformed = Encoding.UTF8.GetBytes("not a unified patch\n"); WriteBytes("docs/handoffs/import-patches/adapted.patch", malformed); groups[0]!["patchSha256"] = Sha(malformed); break;
                case "extra-patch":
                    byte[] extraPatch = CreatePatch("extra", "extra.txt", [], Encoding.UTF8.GetBytes("extra\n"), true);
                    byte[] declaredPatch = File.ReadAllBytes(Path.Combine(Repository,"docs","handoffs","import-patches","adapted.patch")); byte[] combinedPatch = [.. declaredPatch, .. extraPatch];
                    WriteBytes("docs/handoffs/import-patches/adapted.patch", combinedPatch); groups[0]!["patchSha256"] = Sha(combinedPatch); break;
                case "empty-patch":
                    byte[] emptyPatch = []; WriteBytes("docs/handoffs/import-patches/adapted.patch", emptyPatch); groups[0]!["patchSha256"] = Sha(emptyPatch); break;
                case "forbidden-owned-path":
                    string forbidden = "IBM Granite with TurboQuant (Intel)/Features/ModelOptimization/Controls/OptimizationPreferenceCard.xaml";
                    byte[] forbiddenBytes = ReadSourceBlob("954ebaa8db899ec37bfcf8c8b8813986c5bf9d0b");
                    WriteBytes(forbidden, forbiddenBytes);
                    sources[0]!["destination"] = forbidden; destinations[0]!["path"] = forbidden; component["allowedDestinationPaths"]![0] = forbidden; break;
                case "created-wrong-permitted-base":
                    groups[1]!["baseCommit"] = Ui; groups[1]!["baseTree"] = RunGitOutput(_source, "rev-parse", $"{Ui}^{{tree}}"); break;
                case "adapted-result-mode-missing": destinations[1]!.AsObject().Remove("resultMode"); break;
                case "adapted-patch-group-absent": destinations[1]!.AsObject().Remove("patchGroupId"); break;
                case "adapted-patch-group-null": destinations[1]!["patchGroupId"] = null; break;
                case "exact-patch-group-null": destinations[0]!["patchGroupId"] = null; break;
                case "exact-patch-group-non-null": destinations[0]!["patchGroupId"] = "adapted"; break;
                case "traversal": destinations[0]!["path"] = "../exact.txt"; component["allowedDestinationPaths"]![0] = "../exact.txt"; sources[0]!["destination"] = "../exact.txt"; break;
                case "absolute": destinations[0]!["path"] = "C:/exact.txt"; component["allowedDestinationPaths"]![0] = "C:/exact.txt"; sources[0]!["destination"] = "C:/exact.txt"; break;
                case "control": destinations[0]!["path"] = "imports/control\u0001.txt"; component["allowedDestinationPaths"]![0] = "imports/control\u0001.txt"; sources[0]!["destination"] = "imports/control\u0001.txt"; break;
                case "alternate-data-stream": destinations[0]!["path"] = "imports/exact.txt:payload"; component["allowedDestinationPaths"]![0] = "imports/exact.txt:payload"; sources[0]!["destination"] = "imports/exact.txt:payload"; break;
                case "reserved-device": destinations[0]!["path"] = "imports/CON.txt"; component["allowedDestinationPaths"]![0] = "imports/CON.txt"; sources[0]!["destination"] = "imports/CON.txt"; break;
                case "trailing-dot": destinations[0]!["path"] = "imports/exact."; component["allowedDestinationPaths"]![0] = "imports/exact."; sources[0]!["destination"] = "imports/exact."; break;
                case "trailing-space": destinations[0]!["path"] = "imports/exact "; component["allowedDestinationPaths"]![0] = "imports/exact "; sources[0]!["destination"] = "imports/exact "; break;
                case "backslash": destinations[0]!["path"] = "imports\\exact.txt"; component["allowedDestinationPaths"]![0] = "imports\\exact.txt"; sources[0]!["destination"] = "imports\\exact.txt"; break;
                case "case-collision": component["allowedDestinationPaths"]!.AsArray().Add("IMPORTS/exact.txt"); break;
                case "unicode-collision": component["allowedDestinationPaths"]!.AsArray().Add("imports/cafe\u0301.txt"); component["allowedDestinationPaths"]!.AsArray().Add("imports/caf\u00e9.txt"); break;
                case "duplicate-json": File.WriteAllText(ManifestPath, "{\"components\":[],\"components\":[]}"); return;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            WriteManifest();
        }

        private void BuildValidFixture()
        {
            byte[] sourceBytes = ReadSourceBlob("954ebaa8db899ec37bfcf8c8b8813986c5bf9d0b");
            byte[] adaptedBytes = [.. sourceBytes, .. Encoding.UTF8.GetBytes("\nADAPTED\n")];
            byte[] createdBytes = Encoding.UTF8.GetBytes("integration created\n");
            byte[] emptyBytes = [];
            WriteBytes("imports/exact.txt", sourceBytes); WriteBytes("imports/adapted.txt", adaptedBytes); WriteBytes("imports/created.txt", createdBytes); WriteBytes("imports/exact-empty.txt", emptyBytes);
            byte[] adaptedPatch = CreatePatch("adapted", "imports/adapted.txt", sourceBytes, adaptedBytes, false);
            byte[] createdPatch = CreatePatch("created", "imports/created.txt", [], createdBytes, true);
            WriteBytes("docs/handoffs/import-patches/adapted.patch", adaptedPatch); WriteBytes("docs/handoffs/import-patches/created.patch", createdPatch);
            JsonArray authorities = new(
                Authority("planning-base","fix/hardware-inspection-loq-baseline",Planning), Authority("canonical-optimisation-ui","origin/feature/cross-route-optimisation-ui-v1",Ui),
                Authority("openvino-route","origin/feature/openvino-optimisation-adapter-v1","f0189ed187ba900f27bade5fde282ae4e99e8d7b"), Authority("historical-cross-route-contracts","origin/feature/cross-route-optimisation-contracts-v2-1","e254385997392601102b16acf19244437803bdcc"),
                Authority("production-gguf-runtime","origin/feature/gguf-cli-chat-production","bacb3f4106e0191b05b870358342f8158765396d"), Authority("llama-cpp-quantiser","https://github.com/ggml-org/llama.cpp.git","3f7c29d318e317b63f54c558bc69803963d7d88c"));
            JsonObject component = new() {
                ["name"]="UO1",["sourceRef"]="origin/feature/cross-route-optimisation-ui-v1",["sourceCommit"]=Ui,["status"]="Verified",
                ["sources"]=new JsonArray(Source("imports/exact.txt",sourceBytes),Source("imports/adapted.txt",sourceBytes),new JsonObject{{"commit",Ui},{"path",".github/ISSUE_TEMPLATE/.gitkeep"},{"mode","100644"},{"blob","e69de29bb2d1d6434b8b29ae775ad8c2e48c5391"},{"sha256","e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"},{"destination","imports/exact-empty.txt"}}),
                ["destinations"]=new JsonArray(Destination("imports/exact.txt","Exact",sourceBytes,null),Destination("imports/adapted.txt","Adapted",adaptedBytes,"adapted"),new JsonObject{{"path","imports/created.txt"},{"kind","Created"},{"integrationOwned",true},{"resultMode","100644"},{"resultSha256",Sha(createdBytes)},{"patchGroupId","created"}},Destination("imports/exact-empty.txt","Exact",emptyBytes,null)),
                ["allowedDestinationPaths"]=new JsonArray("imports/exact.txt","imports/adapted.txt","imports/created.txt","imports/exact-empty.txt"),["dependencyClosure"]=new JsonArray("fixture"),["excludedSharedPaths"]=new JsonArray(),
                ["integrationPatches"]=new JsonArray(Group("adapted",Ui,RunGitOutput(_source,"rev-parse",$"{Ui}^{{tree}}"),"docs/handoffs/import-patches/adapted.patch",adaptedPatch),Group("created",Planning,RunGitOutput(_source,"rev-parse",$"{Planning}^{{tree}}"),"docs/handoffs/import-patches/created.patch",createdPatch)),["verificationCommands"]=new JsonArray("fixture") };
            _manifest = new JsonObject {
                ["schemaVersion"]=1,["policy"]=new JsonObject{{"importMethod","bounded commit:path:blob verification"},{"stagingMethod","verified no-filter blob hashing and literal update-index cacheinfo"},{"manifestPaths",new JsonArray("docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.json","docs/handoffs/2026-08-26-cross-route-optimisation-import-manifest.md")}},["authorities"]=authorities,
                ["components"]=new JsonArray(component,Pending("GGUF runtime","origin/feature/gguf-cli-chat-production","bacb3f4106e0191b05b870358342f8158765396d"),Pending("OpenVINO route","origin/feature/openvino-optimisation-adapter-v1","f0189ed187ba900f27bade5fde282ae4e99e8d7b")) };
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!); WriteManifest(); File.WriteAllText(Path.ChangeExtension(ManifestPath,".md"),"fixture ledger\n");
        }

        private JsonObject Source(string destination, byte[] bytes) => new(){{"commit",Ui},{"path","README.md"},{"mode","100644"},{"blob","954ebaa8db899ec37bfcf8c8b8813986c5bf9d0b"},{"sha256",Sha(bytes)},{"destination",destination}};
        private static JsonObject Destination(string path,string kind,byte[] bytes,string? group){JsonObject value=new(){{"path",path},{"kind",kind},{"resultMode","100644"},{"resultSha256",Sha(bytes)}};if(group is not null)value["patchGroupId"]=group;return value;}
        private static JsonObject Authority(string name,string reference,string commit)=>new(){{"name",name},{"ref",reference},{"commit",commit}};
        private static JsonObject Group(string id,string commit,string tree,string path,byte[] patch)=>new(){{"id",id},{"baseCommit",commit},{"baseTree",tree},{"patchPath",path},{"patchSha256",Sha(patch)}};
        private static JsonObject Pending(string name,string reference,string commit)
        {
            string[] commands = name == "GGUF runtime"
                ? ["powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\gguf-runtime\\Invoke-GgufChatVerification.ps1", "dotnet test --project $core --configuration Release", "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~GgufRuntime'"]
                : ["dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.Tests/GraniteEdgeAI.OpenVino.Tests.csproj -c Release -p:UseAppHost=false", "dotnet test tests/UnitTests/GraniteEdgeAI.OpenVino.WorkerClient.Tests/GraniteEdgeAI.OpenVino.WorkerClient.Tests.csproj -c Release -p:UseAppHost=false", "dotnet test tests/IntegrationTests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests/GraniteEdgeAI.OpenVino.WorkerProcess.Tests.csproj -c Release -p:UseAppHost=false --filter 'FullyQualifiedName~OptimizationEndToEndTests'", "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\verification\\Invoke-PackagedTestCheckpoint.ps1 -Filter 'FullyQualifiedName~OpenVinoProductionCompositionTests'"];
            JsonArray commandArray = [];
            foreach (string command in commands) commandArray.Add(command);
            return new(){{"name",name},{"sourceRef",reference},{"sourceCommit",commit},{"status","Pending"},{"sources",new JsonArray()},{"destinations",new JsonArray()},{"allowedDestinationPaths",new JsonArray()},{"dependencyClosure",new JsonArray()},{"excludedSharedPaths",new JsonArray()},{"integrationPatches",new JsonArray()},{"verificationCommands",commandArray}};
        }
        private void WriteManifest()=>File.WriteAllText(ManifestPath,_manifest.ToJsonString(new JsonSerializerOptions{WriteIndented=true,TypeInfoResolver=new DefaultJsonTypeInfoResolver()}));
        private void WriteBytes(string relative,byte[] bytes){string full=Path.Combine(Repository,relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllBytes(full,bytes);}

        private byte[] CreatePatch(string name,string relative,byte[] before,byte[] after,bool created)
        {
            string repo=Path.Combine(_root,"patch-"+name);Directory.CreateDirectory(repo);RunGit(repo,"init","--quiet");RunGit(repo,"config","user.email","tests@example.invalid");RunGit(repo,"config","user.name","Tests");RunGit(repo,"config","core.autocrlf","false");
            string full=Path.Combine(repo,relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            if(created){File.WriteAllText(Path.Combine(repo,".gitignore"),"seed\n");RunGit(repo,"add",".gitignore");RunGit(repo,"commit","--quiet","-m","base");File.WriteAllBytes(full,after);RunGit(repo,"add","-N",relative);}
            else{File.WriteAllBytes(full,before);RunGit(repo,"add",relative);RunGit(repo,"commit","--quiet","-m","base");File.WriteAllBytes(full,after);}
            return RunGitBytes(repo,"diff","--binary","--no-ext-diff","--",relative);
        }

        private byte[] ReadSourceBlob(string blob)=>RunGitBytes(_source,"cat-file","blob",blob);
        private static string Sha(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        private static string RunGitOutput(string root,params string[] args)=>Encoding.UTF8.GetString(RunGitBytes(root,args)).Trim();
        private static void RunGit(string root,params string[] args){var result=Task1ProcessRunner.Run("git.exe",root,args);Assert.AreEqual(0,result.ExitCode,result.Output);}
        private static byte[] RunGitBytes(string root,params string[] args){var result=Task1ProcessRunner.Run("git.exe",root,args);Assert.AreEqual(0,result.ExitCode,result.Output);return result.StandardOutput;}
        public void Dispose() => Task1OwnedTemporaryCleanup.Delete(_root, "geai-import-fixture-");
    }
}
