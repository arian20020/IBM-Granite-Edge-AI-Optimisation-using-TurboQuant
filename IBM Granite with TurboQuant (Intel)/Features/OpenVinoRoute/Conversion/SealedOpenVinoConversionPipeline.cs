using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Conversion;

internal sealed class SealedOpenVinoConversionPipeline : IOpenVinoConversionPipeline
{
    internal const int SmokeRequestedTokens = 2;
    private static readonly string[] FixedArguments =
        ["-I", "-s", "-E", "-S", "-B", "-m", "converter"];
    private static readonly IReadOnlyDictionary<string, string> RequiredVersions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nncf"] = "3.3.0",
            ["openvino"] = "2026.3.0",
            ["openvino-genai"] = "2026.3.0.0",
            ["optimum"] = "2.3.0",
            ["optimum-intel"] = "2.1.0",
            ["transformers"] = "5.5.4"
        };
    private static readonly string[] ScratchDirectories =
        ["temp", "roaming", "local", "hf", "xdg", "torch"];
    private static readonly TimeSpan MaximumOperation = TimeSpan.FromMinutes(120);
    private readonly string converterRoot;
    private readonly string expectedManifestSha256;
    private readonly OpenVinoRouteService routeService;
    private readonly IProtectedWorkerSessionFactory sessionFactory;

    internal SealedOpenVinoConversionPipeline(
        string converterRoot,
        string expectedManifestSha256,
        OpenVinoRouteService routeService,
        IProtectedWorkerSessionFactory? sessionFactory = null)
    {
        this.converterRoot = Path.GetFullPath(converterRoot);
        this.expectedManifestSha256 = RequireDigest(expectedManifestSha256);
        this.routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        this.sessionFactory = sessionFactory ?? new ProtectedWorkerSessionFactory();
    }

    public Task<OpenVinoConverterCompletion> ConvertAsync(
        OpenVinoConverterInvocation invocation,
        CancellationToken cancellationToken) =>
        RunWorkerAsync(invocation, "convert", "fp16", cancellationToken);

    internal Task<OpenVinoConverterCompletion> OptimizePackageAsync(
        Guid operationId,
        string sourceDirectory,
        string stagingDirectory,
        string sourceManifestSha256,
        string weightPrecision,
        CancellationToken cancellationToken) =>
        RunWorkerAsync(
            new OpenVinoConverterInvocation(
                operationId,
                sourceDirectory,
                stagingDirectory,
                sourceManifestSha256),
            "optimize",
            weightPrecision,
            cancellationToken);

    private async Task<OpenVinoConverterCompletion> RunWorkerAsync(
        OpenVinoConverterInvocation invocation,
        string operationName,
        string weightPrecision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (operationName is not ("convert" or "optimize") ||
            weightPrecision is not ("fp16" or "int8" or "int4" or "mxfp4") ||
            operationName == "convert" && weightPrecision != "fp16")
        {
            throw new OpenVinoConversionException(OpenVinoSupportCode.OptimizationUnsupported);
        }
        using ConverterClosureLease closure = ConverterClosureLease.Verify(
            converterRoot,
            expectedManifestSha256,
            cancellationToken);
        using VerifiedWorkerExecutable executable =
            new WorkerExecutableResolver("python.exe").Resolve(converterRoot);
        string scratch = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-ConverterScratch-" + invocation.OperationId.ToString("N"));
        if (Directory.Exists(scratch) || File.Exists(scratch))
        {
            throw new OpenVinoConversionException(OpenVinoSupportCode.ConversionPreflightFailed);
        }
        PrepareScratch(scratch);
        ProtectedWorkerSession? session = null;
        try
        {
            ProtectedWorkerLaunchSpec spec = new(
                executable,
                FixedArguments,
                CreateEnvironment(converterRoot, scratch),
                MaximumStandardInputLineBytes: 16 * 1024,
                MaximumStandardOutputLineBytes: 16 * 1024,
                MaximumStandardErrorBytes: 16 * 1024,
                StartupTimeout: TimeSpan.FromSeconds(5),
                CancellationGrace: TimeSpan.FromSeconds(5),
                CleanupTimeout: TimeSpan.FromSeconds(5));
            session = await sessionFactory.StartAsync(spec, cancellationToken)
                .ConfigureAwait(false);
            using CancellationTokenSource timeout = new(MaximumOperation);
            using CancellationTokenSource operation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            JsonElement hello = await ReadEventAsync(session, operation.Token).ConfigureAwait(false);
            RequireEventType(hello, "hello", "type", "protocol", "version", "isolated",
                "offline", "networkDenied");
            JsonElement helloVersion = hello.GetProperty("version");
            if (!StringEquals(hello.GetProperty("protocol"), "granite.openvino.converter") ||
                helloVersion.ValueKind != JsonValueKind.Number ||
                !helloVersion.TryGetInt32(out int protocolVersion) || protocolVersion != 1 ||
                hello.GetProperty("isolated").ValueKind != JsonValueKind.True ||
                hello.GetProperty("offline").ValueKind != JsonValueKind.True ||
                hello.GetProperty("networkDenied").ValueKind != JsonValueKind.True)
            {
                throw ProtocolFailure();
            }
            byte[] request = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
            {
                ["protocol"] = "granite.openvino.converter",
                ["version"] = 1,
                ["operationId"] = invocation.OperationId.ToString(),
                ["sourcePath"] = invocation.SourceDirectory,
                ["destinationPath"] = invocation.StagingDirectory,
                ["sourceManifestSha256"] = invocation.SourceManifestSha256,
                ["operation"] = operationName,
                ["weightPrecision"] = weightPrecision
            });
            await session.StandardInput.WriteLineAsync(request, operation.Token).ConfigureAwait(false);
            JsonElement started = await ReadEventAsync(session, operation.Token).ConfigureAwait(false);
            RequireEventType(started, "started", "type", "operationId");
            RequireOperation(started, invocation.OperationId);
            JsonElement terminal = await ReadEventAsync(session, operation.Token).ConfigureAwait(false);
            string type = terminal.ValueKind == JsonValueKind.Object &&
                terminal.TryGetProperty("type", out JsonElement typeValue) &&
                typeValue.ValueKind == JsonValueKind.String
                    ? typeValue.GetString() ?? string.Empty
                    : string.Empty;
            if (type == "failed")
            {
                RequireEventType(terminal, "failed", "type", "operationId", "supportCode");
                RequireOperation(terminal, invocation.OperationId);
                JsonElement supportCode = terminal.GetProperty("supportCode");
                throw new OpenVinoConversionException(ParseSupportCode(
                    supportCode.ValueKind == JsonValueKind.String
                        ? supportCode.GetString()
                        : null));
            }
            RequireEventType(terminal, "completed", "type", "operationId",
                "options", "sourceManifestSha256", "versions");
            RequireOperation(terminal, invocation.OperationId);
            if (!StringEquals(terminal.GetProperty("sourceManifestSha256"),
                    invocation.SourceManifestSha256))
            {
                throw ProtocolFailure();
            }
            ValidateOptions(terminal.GetProperty("options"), weightPrecision);
            IReadOnlyDictionary<string, string> versions =
                ValidateVersions(terminal.GetProperty("versions"));
            await session.CompleteInputAsync().ConfigureAwait(false);
            await session.WaitForExitAsync(operation.Token).ConfigureAwait(false);
            StandardErrorSnapshot stderr = await session.ReadStandardErrorAsync()
                .ConfigureAwait(false);
            if (session.GetExitCode() != 0 || session.GetActiveProcessCount() != 0 ||
                stderr.RetainedText.Length != 0 || stderr.IsTruncated || stderr.InvalidUtf8Detected)
            {
                throw ProtocolFailure();
            }
            closure.VerifyStillCurrent();
            return new OpenVinoConverterCompletion(
                closure.PythonSha256,
                closure.WheelManifestSha256,
                expectedManifestSha256,
                versions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (session is not null &&
                !await session.TerminateAndVerifyEmptyAsync().ConfigureAwait(false))
            {
                throw new OpenVinoConversionException(
                    OpenVinoSupportCode.RuntimeIntegrityFailed);
            }
            throw;
        }
        catch (OperationCanceledException)
        {
            if (session is not null &&
                !await session.TerminateAndVerifyEmptyAsync().ConfigureAwait(false))
            {
                throw new OpenVinoConversionException(
                    OpenVinoSupportCode.RuntimeIntegrityFailed);
            }
            throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeTimedOut);
        }
        catch (WorkerClientPolicyException)
        {
            throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
        }
        finally
        {
            if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
            if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
        }
    }

    public async Task<OpenVinoConversionValidation> ValidateAsync(
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        OpenVinoRouteInspectionResult result = await routeService.InspectAsync(
            stagingDirectory,
            cancellationToken).ConfigureAwait(false);
        if (result.Outcome is not (OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings) || result.HandoffLease is null)
        {
            result.HandoffLease?.Dispose();
            result.ConversionOffer?.Dispose();
            throw new OpenVinoConversionException(
                OpenVinoActivationOutcomePolicy.GetConversionFailureCode(result));
        }
        return new OpenVinoConversionValidation(
            result.Handoff!.ModelInspectionRunId,
            result.HandoffLease);
    }

    public async Task SmokeAsync(
        OpenVinoConversionValidation validation,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        _ = await SmokeAsync(
            validation,
            stagingDirectory,
            OpenVinoRuntimeOptions.ReleasedDefault,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<(SessionStartedEvent Startup, PromptTurnResult Turn)> SmokeAsync(
        OpenVinoConversionValidation validation,
        string stagingDirectory,
        OpenVinoRuntimeOptions runtimeOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validation);
        OpenVinoRouteHandoffLease lease = validation.ConsumeLease();
        await using OpenVinoRouteSession session = await routeService.StartSessionAsync(
            lease,
            static _ => { },
            runtimeOptions,
            cancellationToken).ConfigureAwait(false);
        PromptTurnResult result = await session.GenerateAsync(
            "Hello",
            requestedNewTokens: SmokeRequestedTokens,
            cancellationToken).ConfigureAwait(false);
        if (result.Status != PromptTurnStatus.Completed || result.GeneratedTokenCount <= 0)
        {
            throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeLoadFailed);
        }
        SessionStartedEvent startup = session.StartupEvidence ??
            throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeProtocolFailed);
        await session.CloseAsync(cancellationToken).ConfigureAwait(false);
        return (startup, result);
    }

    public void BeforePublish()
    {
    }

    public async Task<Guid> ReinspectPublishedAsync(
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        OpenVinoRouteInspectionResult result = await routeService.InspectAsync(
            destinationDirectory,
            cancellationToken).ConfigureAwait(false);
        try
        {
            if (result.Outcome is not (OpenVinoRouteInspectionOutcome.Ready or
                OpenVinoRouteInspectionOutcome.ReadyWithWarnings) || result.Handoff is null)
            {
                throw new OpenVinoConversionException(
                    OpenVinoActivationOutcomePolicy.GetConversionFailureCode(result));
            }
            return result.Handoff.ModelInspectionRunId;
        }
        finally
        {
            result.HandoffLease?.Dispose();
            result.ConversionOffer?.Dispose();
        }
    }

    private static Dictionary<string, string> CreateEnvironment(
        string root,
        string scratch)
    {
        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = systemRoot,
            ["WINDIR"] = systemRoot,
            ["TEMP"] = Path.Combine(scratch, "temp"),
            ["TMP"] = Path.Combine(scratch, "temp"),
            ["HOME"] = scratch,
            ["USERPROFILE"] = scratch,
            ["APPDATA"] = Path.Combine(scratch, "roaming"),
            ["LOCALAPPDATA"] = Path.Combine(scratch, "local"),
            ["HF_HOME"] = Path.Combine(scratch, "hf"),
            ["XDG_CACHE_HOME"] = Path.Combine(scratch, "xdg"),
            ["TORCH_HOME"] = Path.Combine(scratch, "torch"),
            ["HF_HUB_OFFLINE"] = "1",
            ["TRANSFORMERS_OFFLINE"] = "1",
            ["GRANITE_CONVERTER_ROOT"] = root,
            ["GRANITE_CONVERTER_SCRATCH"] = scratch
        };
    }

    private static void PrepareScratch(string scratch)
    {
        foreach (string relative in ScratchDirectories)
        {
            Directory.CreateDirectory(Path.Combine(scratch, relative));
        }
        string telemetry = Path.Combine(scratch, "local", "Intel Corporation");
        Directory.CreateDirectory(telemetry);
        File.WriteAllText(Path.Combine(telemetry, "openvino_telemetry"), "0",
            new UTF8Encoding(false));
    }

    private static async Task<JsonElement> ReadEventAsync(
        ProtectedWorkerSession session,
        CancellationToken cancellationToken)
    {
        byte[]? line = await session.StandardOutput.ReadLineAsync(cancellationToken)
            .ConfigureAwait(false);
        if (line is null) throw ProtocolFailure();
        using JsonDocument document = JsonDocument.Parse(line, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16
        });
        ValidateUniqueProperties(document.RootElement);
        return document.RootElement.Clone();
    }

    private static void RequireEventType(
        JsonElement value,
        string type,
        params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.EnumerateObject().Select(static item => item.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(fields.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            !StringEquals(value.GetProperty("type"), type))
        {
            throw ProtocolFailure();
        }
    }

    private static void RequireOperation(JsonElement value, Guid operationId)
    {
        if (!StringEquals(value.GetProperty("operationId"), operationId.ToString()))
        {
            throw ProtocolFailure();
        }
    }

    private static void ValidateOptions(JsonElement options, string weightPrecision)
    {
        string[] fields = ["library", "localFilesOnly", "task", "trustRemoteCode",
            "weightFormat"];
        if (options.ValueKind != JsonValueKind.Object ||
            !options.EnumerateObject().Select(static item => item.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(fields.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            !StringEquals(options.GetProperty("library"), "transformers") ||
            options.GetProperty("localFilesOnly").ValueKind != JsonValueKind.True ||
            !StringEquals(options.GetProperty("task"), "text-generation-with-past") ||
            options.GetProperty("trustRemoteCode").ValueKind != JsonValueKind.False ||
            !StringEquals(options.GetProperty("weightFormat"), weightPrecision))
        {
            throw ProtocolFailure();
        }
    }

    private static Dictionary<string, string> ValidateVersions(JsonElement versions)
    {
        if (versions.ValueKind != JsonValueKind.Object ||
            !versions.EnumerateObject().Select(static item => item.Name)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(RequiredVersions.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw ProtocolFailure();
        }
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach ((string name, string requiredVersion) in RequiredVersions)
        {
            JsonElement version = versions.GetProperty(name);
            if (!StringEquals(version, requiredVersion))
            {
                throw ProtocolFailure();
            }
            result.Add(name, requiredVersion);
        }
        return result;
    }

    private static void ValidateUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw ProtocolFailure();
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray()) ValidateUniqueProperties(item);
        }
    }

    private static OpenVinoSupportCode ParseSupportCode(string? value) => value switch
    {
        "conversion_preflight_failed" => OpenVinoSupportCode.ConversionPreflightFailed,
        "conversion_output_invalid" => OpenVinoSupportCode.ConversionOutputInvalid,
        "runtime_integrity_failed" => OpenVinoSupportCode.RuntimeIntegrityFailed,
        "runtime_protocol_failed" => OpenVinoSupportCode.RuntimeProtocolFailed,
        "package_missing_resource" => OpenVinoSupportCode.PackageMissingResource,
        "package_inconsistent_resource" => OpenVinoSupportCode.PackageInconsistentResource,
        "package_unsafe_path" => OpenVinoSupportCode.PackageUnsafePath,
        "package_changed" => OpenVinoSupportCode.PackageChanged,
        "package_unreadable" => OpenVinoSupportCode.PackageUnreadable,
        "model_architecture_unsupported" => OpenVinoSupportCode.ModelArchitectureUnsupported,
        "model_task_unsupported" => OpenVinoSupportCode.ModelTaskUnsupported,
        "tokenizer_unsupported" => OpenVinoSupportCode.TokenizerUnsupported,
        "runtime_load_failed" => OpenVinoSupportCode.RuntimeLoadFailed,
        _ => OpenVinoSupportCode.ConversionFailed
    };

    private static string RequireDigest(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(static item =>
                item is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A lowercase SHA-256 digest is required.", nameof(value));
        }
        return value;
    }

    private static OpenVinoConversionException ProtocolFailure() =>
        new(OpenVinoSupportCode.RuntimeProtocolFailed);

    private static bool StringEquals(JsonElement value, string expected) =>
        value.ValueKind == JsonValueKind.String && value.GetString() == expected;

    internal sealed class ConverterClosureLease : IDisposable
    {
        private readonly string root;
        private readonly IReadOnlyList<VerifiedFile> files;
        private readonly ClosureMutationMonitor mutationMonitor;

        private ConverterClosureLease(
            string root,
            string pythonSha256,
            string wheelManifestSha256,
            IReadOnlyList<VerifiedFile> files,
            ClosureMutationMonitor mutationMonitor)
        {
            this.root = root;
            PythonSha256 = pythonSha256;
            WheelManifestSha256 = wheelManifestSha256;
            this.files = files;
            this.mutationMonitor = mutationMonitor;
        }

        public string PythonSha256 { get; }
        public string WheelManifestSha256 { get; }

        public static ConverterClosureLease Verify(
            string root,
            string expectedManifest,
            CancellationToken cancellationToken)
        {
            try
            {
                return VerifyCore(root, expectedManifest, cancellationToken);
            }
            catch (OpenVinoConversionException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               JsonException or InvalidOperationException or
                                               ArgumentException or OverflowException)
            {
                throw new OpenVinoConversionException(
                    OpenVinoSupportCode.RuntimeIntegrityFailed);
            }
        }

        private static ConverterClosureLease VerifyCore(
            string root,
            string expectedManifest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root) ||
                (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            {
                throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
            }
            ClosureMutationMonitor monitor = new(root);
            try
            {
                string manifestPath = Path.Combine(root, "converter-manifest.json");
                if ((File.GetAttributes(manifestPath) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                }
                byte[] manifestBytes = File.ReadAllBytes(manifestPath);
                if (!string.Equals(
                        Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant(),
                        expectedManifest,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                }
                using JsonDocument document = JsonDocument.Parse(manifestBytes);
                ValidateUniqueProperties(document.RootElement);
                JsonElement manifest = document.RootElement;
                string[] manifestFields =
                [
                    "schemaVersion", "protocol", "protocolVersion", "pythonSha256",
                    "wheelManifestSha256", "requirementsLockSha256", "wheelLockStatus",
                    "maximumOperationMinutes", "launchArguments", "files"
                ];
                RequireExactProperties(manifest, manifestFields);
                string python = RequireDigest(manifest.GetProperty("pythonSha256").GetString()!);
                string wheels = RequireDigest(manifest.GetProperty("wheelManifestSha256").GetString()!);
                _ = RequireDigest(manifest.GetProperty("requirementsLockSha256").GetString()!);
                if (manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
                    manifest.GetProperty("protocol").GetString() != "granite.openvino.converter" ||
                    manifest.GetProperty("protocolVersion").GetInt32() != 1 ||
                    manifest.GetProperty("wheelLockStatus").GetString() != "resolved" ||
                    manifest.GetProperty("maximumOperationMinutes").GetInt32() != 120)
                {
                    throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                }
                JsonElement launchArguments = manifest.GetProperty("launchArguments");
                if (launchArguments.ValueKind != JsonValueKind.Array ||
                    !launchArguments.EnumerateArray().Select(static value =>
                        value.ValueKind == JsonValueKind.String ? value.GetString() : null)
                        .SequenceEqual(FixedArguments, StringComparer.Ordinal))
                {
                    throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                }
                JsonElement fileEntries = manifest.GetProperty("files");
                if (fileEntries.ValueKind != JsonValueKind.Array || fileEntries.GetArrayLength() == 0)
                {
                    throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                }
                HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase);
                List<VerifiedFile> verified = [];
                byte[] hashBuffer = GC.AllocateUninitializedArray<byte>(1024 * 1024);
                foreach (JsonElement entry in fileEntries.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RequireExactProperties(entry, ["path", "length", "sha256"]);
                    string relative = entry.GetProperty("path").GetString()!;
                    string[] segments = relative.Split('/');
                    if (string.IsNullOrWhiteSpace(relative) || !expected.Add(relative) ||
                        relative.Contains(':') || relative.Contains('\\') ||
                        relative.StartsWith('/') ||
                        Path.IsPathFullyQualified(relative) ||
                        segments.Any(static segment => segment is "" or "." or ".."))
                    {
                        throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                    }
                    string path = Path.GetFullPath(Path.Combine(
                        root, relative.Replace('/', Path.DirectorySeparatorChar)));
                    string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) +
                        Path.DirectorySeparatorChar;
                    if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                    }
                    long length = entry.GetProperty("length").GetInt64();
                    if (length < 0)
                    {
                        throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                    }
                    VerifiedFile file = new(relative, path, length,
                        RequireDigest(entry.GetProperty("sha256").GetString()!));
                    file.Verify(hashBuffer, cancellationToken);
                    verified.Add(file);
                }
                string[] actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(path => !string.Equals(path, manifestPath,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                    .ToArray();
                if (actual.Length != expected.Count || actual.Any(path => !expected.Contains(path)))
                {
                    throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
                }
                monitor.ThrowIfChanged();
                return new ConverterClosureLease(root, python, wheels, verified, monitor);
            }
            catch
            {
                monitor.Dispose();
                throw;
            }
        }

        public void VerifyStillCurrent()
        {
            mutationMonitor.ThrowIfChanged();
            byte[] hashBuffer = GC.AllocateUninitializedArray<byte>(1024 * 1024);
            foreach (VerifiedFile file in files)
            {
                file.Verify(hashBuffer, CancellationToken.None);
            }
            string manifestPath = Path.Combine(root, "converter-manifest.json");
            int count = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Count(path => !string.Equals(path, manifestPath,
                    StringComparison.OrdinalIgnoreCase));
            HashSet<string> expected = files.Select(static file => file.Relative)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, manifestPath,
                    StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .ToArray();
            if (count != files.Count || actual.Length != expected.Count ||
                actual.Any(path => !expected.Contains(path)))
            {
                throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
            }
            mutationMonitor.ThrowIfChanged();
        }

        internal string? ObservedMutation => mutationMonitor.ObservedMutation;

        private static void RequireExactProperties(JsonElement value, string[] names)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.EnumerateObject().Select(static property => property.Name)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new OpenVinoConversionException(OpenVinoSupportCode.RuntimeIntegrityFailed);
            }
        }

        public void Dispose()
        {
            mutationMonitor.Dispose();
        }

        private sealed record VerifiedFile(
            string Relative,
            string Path,
            long Length,
            string Sha256)
        {
            public void Verify(byte[] hashBuffer, CancellationToken cancellationToken)
            {
                using FileStream stream = new(
                    Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                if (stream.Length != Length) throw new OpenVinoConversionException(
                    OpenVinoSupportCode.RuntimeIntegrityFailed);
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                int read;
                while ((read = stream.Read(hashBuffer, 0, hashBuffer.Length)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hash.AppendData(hashBuffer, 0, read);
                }
                string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if (actual != Sha256) throw new OpenVinoConversionException(
                    OpenVinoSupportCode.RuntimeIntegrityFailed);
            }
        }

        private sealed class ClosureMutationMonitor : IDisposable
        {
            private readonly FileSystemWatcher watcher;
            private int changed;
            private string? observedMutation;

            public ClosureMutationMonitor(string root)
            {
                watcher = new(root)
                {
                    IncludeSubdirectories = true,
                    Filter = "*",
                    InternalBufferSize = 64 * 1024,
                    NotifyFilter = NotifyFilters.FileName |
                        NotifyFilters.DirectoryName |
                        NotifyFilters.LastWrite |
                        NotifyFilters.Size
                };
                watcher.Changed += OnMutation;
                watcher.Created += OnMutation;
                watcher.Deleted += OnMutation;
                watcher.Renamed += OnMutation;
                watcher.Error += OnError;
                watcher.EnableRaisingEvents = true;
            }

            public void ThrowIfChanged()
            {
                if (Volatile.Read(ref changed) != 0)
                {
                    throw new OpenVinoConversionException(
                        OpenVinoSupportCode.RuntimeIntegrityFailed);
                }
            }

            public void Dispose() => watcher.Dispose();

            public string? ObservedMutation => Volatile.Read(ref observedMutation);

            private void OnMutation(object sender, FileSystemEventArgs args)
            {
                if (args.ChangeType == WatcherChangeTypes.Changed &&
                    Directory.Exists(args.FullPath))
                {
                    return;
                }
                Interlocked.CompareExchange(
                    ref observedMutation,
                    args.ChangeType + ":" + args.Name,
                    null);
                Interlocked.Exchange(ref changed, 1);
            }

            private void OnError(object sender, ErrorEventArgs args)
            {
                Interlocked.CompareExchange(ref observedMutation, "WatcherError", null);
                Interlocked.Exchange(ref changed, 1);
            }
        }
    }
}
