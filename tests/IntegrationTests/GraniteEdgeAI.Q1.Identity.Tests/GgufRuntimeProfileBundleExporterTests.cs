using System.Security.Cryptography;
using System.Text.Json;
using GraniteEdgeAI.CrossFeature.IntegrationTests;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Q1.Identity.Tests;

[TestClass]
public sealed class GgufRuntimeProfileBundleExporterTests
{
    private const ulong GiB = 1024UL * 1024 * 1024;
    private const string HardwareDigest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string[] ExpectedMemberNames =
        ["bundle-manifest.json", "model.gguf", "runtime-profile.json"];

    [TestMethod]
    public async Task ExactDeclaredProfileCreatesDeterministicThreeMemberBundle()
    {
        using var fixture = new Fixture(GgufCacheType.Turbo3);
        var progress = new List<GgufRuntimeProfileBundleExportStage>();

        GgufRuntimeProfileBundleExportResult first = await fixture.Exporter.ExportAsync(
            fixture.Result,
            fixture.Destination("first"),
            GiB,
            new InlineProgress(progress.Add),
            CancellationToken.None);
        GgufRuntimeProfileBundleExportResult second = await fixture.Exporter.ExportAsync(
            fixture.Result,
            fixture.Destination("second"),
            GiB,
            new InlineProgress(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.Succeeded,
            first.Disposition);
        Assert.AreEqual(first.BundleManifestSha256, second.BundleManifestSha256);
        CollectionAssert.AreEqual(
            ExpectedMemberNames,
            Directory.GetFiles(fixture.Destination("first"))
                .Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        CollectionAssert.AreEqual(
            fixture.SourceBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                fixture.Destination("first"), "model.gguf")));
        CollectionAssert.AreEqual(
            Enum.GetValues<GgufRuntimeProfileBundleExportStage>(),
            progress);

        using JsonDocument profile = JsonDocument.Parse(await File.ReadAllBytesAsync(
            Path.Combine(fixture.Destination("first"), "runtime-profile.json")));
        JsonElement root = profile.RootElement;
        Assert.AreEqual("granite.gguf-issued-runtime-profile.v1",
            root.GetProperty("schema").GetString());
        Assert.AreEqual("declared-not-runtime-validated",
            root.GetProperty("validation_claim").GetString());
        Assert.AreEqual("turbo3", root.GetProperty("key_cache_type").GetString());
        Assert.AreEqual("turbo3", root.GetProperty("value_cache_type").GetString());
        Assert.IsFalse(root.ToString().Contains(fixture.SourcePath,
            StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual(first.BundleManifestLengthBytes,
            checked((ulong)new FileInfo(Path.Combine(
                fixture.Destination("first"), "bundle-manifest.json")).Length));
        Assert.AreEqual(first.BundleManifestSha256,
            await Sha256Async(Path.Combine(
                fixture.Destination("first"), "bundle-manifest.json")));
        Assert.IsGreaterThan(fixture.Plan.Binding.ModelLengthBytes,
            first.BundleLengthBytes);

        using JsonDocument manifest = JsonDocument.Parse(
            await File.ReadAllBytesAsync(Path.Combine(
                fixture.Destination("first"), "bundle-manifest.json")));
        Assert.AreEqual(fixture.Plan.OptimizationPlanId.ToString("N"),
            manifest.RootElement.GetProperty("optimization_plan_id").GetString());
        Assert.AreEqual(fixture.Result.ExecutionId.ToString("N"),
            manifest.RootElement.GetProperty("execution_id").GetString());
        Assert.AreEqual(fixture.Plan.Binding.ModelSha256,
            manifest.RootElement.GetProperty("source_sha256").GetString());
        Assert.IsFalse(manifest.RootElement.ToString().Contains(
            fixture.SourcePath, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ExactCacheTokensPreserveIssuedPayloadWithoutInference()
    {
        (GgufCacheType Cache, string Token)[] cases =
        [
            (GgufCacheType.F16, "f16"),
            (GgufCacheType.Q8Zero, "q8_0"),
            (GgufCacheType.Turbo4, "turbo4"),
            (GgufCacheType.Turbo3, "turbo3"),
            (GgufCacheType.Turbo2, "turbo2"),
        ];

        foreach ((GgufCacheType cache, string token) in cases)
        {
            using var fixture = new Fixture(cache);
            string destination = fixture.Destination(token);
            GgufRuntimeProfileBundleExportResult result =
                await fixture.Exporter.ExportAsync(
                    fixture.Result, destination, GiB,
                    new InlineProgress(_ => { }), CancellationToken.None);

            Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.Succeeded,
                result.Disposition);
            using JsonDocument profile = JsonDocument.Parse(
                await File.ReadAllBytesAsync(
                    Path.Combine(destination, "runtime-profile.json")));
            Assert.AreEqual(token,
                profile.RootElement.GetProperty("key_cache_type").GetString());
        }
    }

    [TestMethod]
    public async Task SubstitutedResultAndPersistentPlanAreRejectedBeforeWriting()
    {
        using var fixture = new Fixture(GgufCacheType.F16);
        using var substitutedFixture = new Fixture(GgufCacheType.Q8Zero);
        OptimizationExecutionResult substituted = substitutedFixture.Result;

        GgufRuntimeProfileBundleExportResult result =
            await fixture.Exporter.ExportAsync(
                substituted, fixture.Destination("substituted"), GiB,
                new InlineProgress(_ => { }), CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.ResultRejected,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(fixture.Destination("substituted")));

        OptimizationExecutionPlan persistent =
            CrossFeaturePlanFixture.PersistentGgufPlan(
                fixture.Plan.Binding.ModelSha256,
                fixture.Plan.Binding.ModelLengthBytes);
        var persistentExporter = new GgufRuntimeProfileBundleExporter(
            persistent, fixture.Custody);
        OptimizationExecutionResult persistentResult =
            OptimizationExecutionResult.Succeeded(
                persistent, "persistent", new string('b', 64), 2, true,
                DateTimeOffset.UnixEpoch);
        result = await persistentExporter.ExportAsync(
            persistentResult, fixture.Destination("persistent"), GiB,
            new InlineProgress(_ => { }), CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.ResultRejected,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(fixture.Destination("persistent")));
    }

    [TestMethod]
    public async Task ExistingDestinationIsNeverOverwritten()
    {
        using var fixture = new Fixture(GgufCacheType.Q8Zero);
        string destination = fixture.Destination("existing");
        Directory.CreateDirectory(destination);
        string sentinel = Path.Combine(destination, "user.txt");
        await File.WriteAllTextAsync(sentinel, "preserve");

        GgufRuntimeProfileBundleExportResult result =
            await fixture.Exporter.ExportAsync(
                fixture.Result, destination, GiB,
                new InlineProgress(_ => { }), CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.DestinationExists,
            result.Disposition);
        Assert.AreEqual("preserve", await File.ReadAllTextAsync(sentinel));
        CollectionAssert.AreEqual(new[] { sentinel },
            Directory.GetFiles(destination));
    }

    [TestMethod]
    public async Task ChangedSourceFailsClosedAndLeavesNoDestinationOrTemporaryData()
    {
        using var fixture = new Fixture(GgufCacheType.F16);
        await File.WriteAllBytesAsync(fixture.SourcePath, "changed"u8.ToArray());

        GgufRuntimeProfileBundleExportResult result =
            await fixture.Exporter.ExportAsync(
                fixture.Result, fixture.Destination("changed"), GiB,
                new InlineProgress(_ => { }), CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.SourceChanged,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(fixture.Destination("changed")));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Root)
            .Any(path => Path.GetFileName(path).StartsWith(
                ".granite-profile-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CancellationAtEveryReportedBoundaryCleansOwnedTemporaryDirectory()
    {
        foreach (GgufRuntimeProfileBundleExportStage stage in
            Enum.GetValues<GgufRuntimeProfileBundleExportStage>())
        {
            using var fixture = new Fixture(GgufCacheType.Turbo4);
            using var cancellation = new CancellationTokenSource();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                fixture.Exporter.ExportAsync(
                    fixture.Result,
                    fixture.Destination(stage.ToString()),
                    GiB,
                    new InlineProgress(observed =>
                    {
                        if (observed == stage) cancellation.Cancel();
                    }),
                    cancellation.Token));

            Assert.IsFalse(Directory.Exists(fixture.Destination(stage.ToString())));
            Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Root)
                .Any(path => Path.GetFileName(path).StartsWith(
                    ".granite-profile-", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public async Task OversizedSourceIsRejectedWithoutCreatingDestination()
    {
        using var fixture = new Fixture(GgufCacheType.F16);

        GgufRuntimeProfileBundleExportResult result =
            await fixture.Exporter.ExportAsync(
                fixture.Result, fixture.Destination("oversized"),
                checked((ulong)fixture.SourceBytes.Length - 1),
                new InlineProgress(_ => { }), CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.Oversized,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(fixture.Destination("oversized")));
    }

    [TestMethod]
    public async Task MissingCustodyAndInvalidDestinationsFailBeforeWriting()
    {
        using var fixture = new Fixture(GgufCacheType.F16);
        fixture.Custody.Retire(Guid.ParseExact(
            fixture.Plan.Binding.ModelInspectionHandoffId, "N"));

        GgufRuntimeProfileBundleExportResult result =
            await fixture.Exporter.ExportAsync(
                fixture.Result,
                fixture.Destination("missing-source"),
                GiB,
                new InlineProgress(_ => { }),
                CancellationToken.None);
        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.SourceUnavailable,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(fixture.Destination("missing-source")));

        using var available = new Fixture(GgufCacheType.F16);
        result = await available.Exporter.ExportAsync(
            available.Result,
            "relative-bundle",
            GiB,
            new InlineProgress(_ => { }),
            CancellationToken.None);
        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.DestinationRejected,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(Path.Combine(
            available.Root, "relative-bundle")));

        string fileDestination = available.Destination("existing-file");
        await File.WriteAllTextAsync(fileDestination, "preserve");
        result = await available.Exporter.ExportAsync(
            available.Result,
            fileDestination,
            GiB,
            new InlineProgress(_ => { }),
            CancellationToken.None);
        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.DestinationExists,
            result.Disposition);
        Assert.AreEqual("preserve", await File.ReadAllTextAsync(fileDestination));
    }

    [TestMethod]
    public async Task AnyStagedMemberMutationIsRejectedBeforeAtomicPublish()
    {
        foreach (string member in ExpectedMemberNames)
        {
            using var fixture = new Fixture(GgufCacheType.Q8Zero);
            GgufRuntimeProfileBundleExportResult result =
                await fixture.Exporter.ExportAsync(
                    fixture.Result,
                    fixture.Destination("mutated-" + member),
                    GiB,
                    new InlineProgress(stage =>
                    {
                        if (stage == GgufRuntimeProfileBundleExportStage.Verifying)
                        {
                            string temporary = FindTemporary(fixture.Root);
                            File.WriteAllText(Path.Combine(temporary, member), "mutated");
                        }
                    }),
                    CancellationToken.None);

            Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.Failed,
                result.Disposition, member);
            Assert.IsFalse(Directory.Exists(
                fixture.Destination("mutated-" + member)));
        }
    }

    [TestMethod]
    public async Task UnexpectedStagedMemberIsQuarantinedWithoutBroadDeletion()
    {
        using var fixture = new Fixture(GgufCacheType.F16);

        GgufRuntimeProfileBundleExportResult result =
            await fixture.Exporter.ExportAsync(
                fixture.Result,
                fixture.Destination("unexpected"),
                GiB,
                new InlineProgress(stage =>
                {
                    if (stage == GgufRuntimeProfileBundleExportStage.Publishing)
                    {
                        File.WriteAllText(
                            Path.Combine(FindTemporary(fixture.Root), "unknown.user"),
                            "do-not-delete");
                    }
                }),
                CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.CleanupFailed,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(fixture.Destination("unexpected")));
        string quarantine = Directory.GetDirectories(
            fixture.Root, ".granite-profile-rejected-*",
            SearchOption.TopDirectoryOnly).Single();
        Assert.AreEqual("do-not-delete", await File.ReadAllTextAsync(
            Path.Combine(quarantine, "unknown.user")));
    }

    [TestMethod]
    public async Task DestinationAppearingAtPublishBoundaryWinsWithoutOverwrite()
    {
        using var fixture = new Fixture(GgufCacheType.F16);
        string destination = fixture.Destination("late-collision");

        GgufRuntimeProfileBundleExportResult result =
            await fixture.Exporter.ExportAsync(
                fixture.Result,
                destination,
                GiB,
                new InlineProgress(stage =>
                {
                    if (stage == GgufRuntimeProfileBundleExportStage.Publishing)
                    {
                        Directory.CreateDirectory(destination);
                        File.WriteAllText(
                            Path.Combine(destination, "user.txt"), "preserve");
                    }
                }),
                CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.DestinationExists,
            result.Disposition);
        Assert.AreEqual("preserve", await File.ReadAllTextAsync(
            Path.Combine(destination, "user.txt")));
    }

    [TestMethod]
    public async Task CancellationPreservesCallerSignalWhenUnexpectedDataBlocksCleanup()
    {
        using var fixture = new Fixture(GgufCacheType.F16);
        using var cancellation = new CancellationTokenSource();

        OperationCanceledException error =
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                fixture.Exporter.ExportAsync(
                    fixture.Result,
                    fixture.Destination("cancel-cleanup"),
                    GiB,
                    new InlineProgress(stage =>
                    {
                        if (stage == GgufRuntimeProfileBundleExportStage.CopyingModel)
                        {
                            File.WriteAllText(
                                Path.Combine(
                                    FindTemporary(fixture.Root), "unknown.user"),
                                "preserve");
                            cancellation.Cancel();
                        }
                    }),
                    cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsInstanceOfType<GgufRuntimeProfileBundleCleanupException>(
            error.InnerException);
        string quarantine = Directory.GetDirectories(
            fixture.Root, ".granite-profile-rejected-*",
            SearchOption.TopDirectoryOnly).Single();
        Assert.AreEqual("preserve", await File.ReadAllTextAsync(
            Path.Combine(quarantine, "unknown.user")));
    }

    [TestMethod]
    public async Task CandidateAndPayloadCacheMismatchIsRejectedBeforeSourceAccess()
    {
        using var fixture = new Fixture(GgufCacheType.F16);
        GgufExecutionPayload mismatchedPayload = GgufExecutionPayload.Create(
            fixture.Plan.ExecutionPayload.Gguf!.RuntimeBuildId,
            fixture.Plan.ExecutionPayload.Gguf.RuntimeSourceCommit,
            GgufRuntimeBackend.Cpu,
            "CPU",
            4096,
            GgufCacheType.Q8Zero,
            GgufCacheType.Q8Zero,
            0,
            true,
            4,
            128,
            "Measured",
            "declared-profile",
            256,
            GgufWeightFormat.Imported);
        var mismatchedPlan = new OptimizationExecutionPlan(
            fixture.Plan.ContractVersion,
            fixture.Plan.OptimizationPlanId,
            fixture.Plan.Binding,
            fixture.Plan.CapabilitySnapshot,
            fixture.Plan.Workload,
            fixture.Plan.Candidate,
            OptimizationExecutionPayload.ForGguf(mismatchedPayload),
            fixture.Plan.Preference,
            fixture.Plan.SharedWithAdjacentBand,
            fixture.Plan.ConfigurationSha256,
            fixture.Plan.CreatedAtUtc);
        OptimizationExecutionResult mismatchedResult =
            OptimizationExecutionResult.Succeeded(
                mismatchedPlan,
                $"gguf-profile-{mismatchedPlan.OptimizationPlanId:N}",
                mismatchedPlan.ConfigurationSha256,
                0,
                true,
                DateTimeOffset.UnixEpoch,
                fixture.Result.ExecutionId);
        var exporter = new GgufRuntimeProfileBundleExporter(
            mismatchedPlan, fixture.Custody);

        GgufRuntimeProfileBundleExportResult result = await exporter.ExportAsync(
            mismatchedResult,
            fixture.Destination("mismatched-cache"),
            GiB,
            new InlineProgress(_ => { }),
            CancellationToken.None);

        Assert.AreEqual(GgufRuntimeProfileBundleExportDisposition.ResultRejected,
            result.Disposition);
        Assert.IsFalse(Directory.Exists(fixture.Destination("mismatched-cache")));
    }

    private static string FindTemporary(string root) =>
        Directory.GetDirectories(root, ".granite-profile-*",
            SearchOption.TopDirectoryOnly)
            .Single(path => !Path.GetFileName(path).StartsWith(
                ".granite-profile-rejected-", StringComparison.Ordinal));

    private static async Task<string> Sha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream))
            .ToLowerInvariant();
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture(GgufCacheType cache)
        {
            Root = Path.Combine(Path.GetTempPath(),
                "gguf-runtime-profile-bundle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            SourcePath = Path.Combine(Root, "source.gguf");
            SourceBytes = "exact unchanged gguf bytes"u8.ToArray();
            File.WriteAllBytes(SourcePath, SourceBytes);
            string sourceSha = Convert.ToHexString(SHA256.HashData(SourceBytes))
                .ToLowerInvariant();
            Plan = CreateRuntimeOnlyPlan(sourceSha,
                checked((ulong)SourceBytes.Length), cache);
            Result = OptimizationExecutionResult.Succeeded(
                Plan,
                $"gguf-profile-{Plan.OptimizationPlanId:N}",
                Plan.ConfigurationSha256,
                0,
                sourceUnchanged: true,
                DateTimeOffset.UnixEpoch,
                Guid.Parse("77777777-7777-4777-8777-777777777777"));
            Custody = new ModelSourceCustodyRegistry();
            var key = new ModelSourceCustodyKey(
                Guid.ParseExact(Plan.Binding.ModelInspectionHandoffId, "N"),
                sourceSha,
                SourceBytes.Length,
                OptimizationRoute.Gguf);
            Assert.IsTrue(Custody.Register(new ModelSourceCustodyRecord(
                key, SourcePath)));
            Exporter = new GgufRuntimeProfileBundleExporter(Plan, Custody);
        }

        internal string Root { get; }
        internal string SourcePath { get; }
        internal byte[] SourceBytes { get; }
        internal OptimizationExecutionPlan Plan { get; }
        internal OptimizationExecutionResult Result { get; }
        internal ModelSourceCustodyRegistry Custody { get; }
        internal GgufRuntimeProfileBundleExporter Exporter { get; }

        internal string Destination(string name) => Path.Combine(Root, name);

        public void Dispose()
        {
            Custody.Dispose();
            if (Directory.Exists(Root))
            {
                foreach (string file in Directory.EnumerateFiles(
                    Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(Root, recursive: true);
            }
        }

        private static OptimizationExecutionPlan CreateRuntimeOnlyPlan(
            string sourceSha,
            ulong sourceLength,
            GgufCacheType cache)
        {
            const string evidenceId = "gguf-runtime-profile";
            var binding = OptimizationJourneyBinding.Create(
                "44444444444444448444444444444444",
                "66666666666646668666666666666666",
                sourceSha,
                sourceLength,
                "55555555555545558555555555555555",
                HardwareDigest);
            GgufKvCacheFormat routeCache = cache switch
            {
                GgufCacheType.F16 => GgufKvCacheFormat.F16,
                GgufCacheType.Q8Zero => GgufKvCacheFormat.Q8_0,
                GgufCacheType.Q4Zero => GgufKvCacheFormat.Q8_0,
                GgufCacheType.Turbo4 => GgufKvCacheFormat.TurboQuant4Bit,
                GgufCacheType.Turbo3 => GgufKvCacheFormat.TurboQuant3Bit,
                GgufCacheType.Turbo2 => GgufKvCacheFormat.TurboQuant2Bit,
                _ => throw new ArgumentOutOfRangeException(nameof(cache)),
            };
            GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                routeCache,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None);
            bool experimental = GgufTurboQuantFormatPolicy.IsTurboQuant(routeCache);
            OptimizationCandidateMetrics metrics =
                OptimizationCandidateMetrics.Create(
                    EvidenceGrade.Measured,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    OptimizationAssessment.Good,
                    4096,
                    GiB,
                    4 * GiB,
                    3 * GiB,
                    0,
                    0,
                    requiresPersistentChange: false,
                    availableDiskBytes: 500 * GiB);
            OptimizationCandidate candidate = OptimizationCandidate.Create(
                configuration, metrics, evidenceId, isExperimental: experimental);
            string runtimeBuild =
                PublishedGgufOptimizationEvidence.RuntimeBuildId;
            string runtimeCommit =
                PublishedGgufOptimizationEvidence.RuntimeSourceCommit;
            GgufExecutionPayload gguf = GgufExecutionPayload.Create(
                runtimeBuild,
                runtimeCommit,
                GgufRuntimeBackend.Cpu,
                "CPU",
                4096,
                cache,
                cache,
                0,
                true,
                4,
                128,
                "Measured",
                "declared-profile",
                256,
                GgufWeightFormat.Imported);
            OptimizationExecutionPayload payload =
                OptimizationExecutionPayload.ForGguf(gguf);
            SupportLevel support = experimental
                ? SupportLevel.Experimental
                : SupportLevel.DeclaredSupported;
            GgufAdmittedConfiguration admitted =
                GgufAdmittedConfiguration.Create(
                    evidenceId,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GgufWeightFormat.Imported,
                    routeCache,
                    GpuOffloadLevel.None,
                    512,
                    32768,
                    support,
                    requiresEvidence: experimental);
            GgufRuntimeAuthority runtimeAuthority = GgufRuntimeAuthority.Create(
                runtimeBuild,
                runtimeCommit,
                [GgufExecutionProfileAuthority.Create(
                    evidenceId,
                    EvidenceGrade.Measured,
                    "declared-profile",
                    true,
                    4,
                    128,
                    256)]);
            GgufTurboQuantImplementationIdentity? turboIdentity = experimental
                ? GgufTurboQuantImplementationIdentity.Create(
                    runtimeBuild,
                    runtimeCommit,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu)
                : null;
            OptimizationCapabilitySnapshot capability =
                OptimizationCapabilitySnapshot.ForGguf(
                    "gguf-capability-runtime-profile",
                    new string('c', 64),
                    GgufCapabilityPayload.Create(
                        runtimeBuild,
                        [admitted],
                        turboQuantImplementation: turboIdentity,
                        runtimeAuthority: runtimeAuthority));
            OptimizationWorkload workload = OptimizationWorkload.Create(
                "runtime-profile-workload",
                4096,
                OptimizationAssessment.Acceptable,
                [ContextTokenCount.FromTokens(4096)]);
            var current = CompatibilityCurrentModelInput.ForGguf(
                GgufCompatibilityModelInput.Create(
                    sourceLength, 32, 4096, 32, 8, 8192, 1, 2,
                    3_000_000_000),
                configuration);
            var hardware = CompatibilityHardwareInput.Create(
                TotalPhysicalMemory.FromBytes(64 * GiB),
                0,
                500 * GiB,
                [DeviceRouteId.Cpu],
                [CompatibilityBackend.Cpu]);
            CompatibilityProductionInput input = CompatibilityProductionInput.Create(
                Guid.Parse(binding.ModelInspectionRunId),
                Guid.Parse(binding.ProductHardwareRunId),
                current,
                CompatibilityJourneyAuthorityInput.Create(
                    Guid.Parse(binding.ModelInspectionHandoffId),
                    binding.ModelSha256,
                    CompatibilityFactDigest.ComputeModel(current),
                    HardwareDigest,
                    CompatibilityFactDigest.ComputeHardware(hardware)),
                hardware,
                CompatibilityFreshResourcesInput.Create(
                    CurrentlyAvailableMemory.FromBytes(48 * GiB),
                    0,
                    500 * GiB,
                    DateTimeOffset.UnixEpoch),
                CompatibilityOptimizationProductionInput.Create(
                    capability,
                    workload,
                    binding,
                    experimental
                        ? new HashSet<string>(StringComparer.Ordinal) { evidenceId }
                        : new HashSet<string>(StringComparer.Ordinal)));
            OptimizationIssuanceAuthority authority =
                CompatibilityEngine.CreateOptimizationIssuanceAuthority(
                    input, DateTimeOffset.UnixEpoch);
            OptimizationAdmissionProof proof = OptimizationAdmissionProof.Create(
                capability,
                workload,
                binding,
                candidate,
                support,
                requiresEvidence: experimental,
                experimental
                    ? new HashSet<string>(StringComparer.Ordinal) { evidenceId }
                    : new HashSet<string>(StringComparer.Ordinal),
                authority);
            candidate = OptimizationCandidate.AttachAdmissionProof(candidate, proof);
            OptimizationSelection selection =
                OptimizationPreferenceResolver.Resolve(
                    [candidate], OptimizationPreferenceSelection.Automatic())
                ?? throw new InvalidOperationException(
                    "The real admitted runtime profile did not resolve.");
            return OptimizationPlanIssuer.Issue(
                selection,
                payload,
                capability,
                workload,
                binding,
                modelLayerCount: 32,
                authority,
                new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        }
    }

    private sealed class InlineProgress(
        Action<GgufRuntimeProfileBundleExportStage> report)
        : IProgress<GgufRuntimeProfileBundleExportStage>
    {
        public void Report(GgufRuntimeProfileBundleExportStage value) => report(value);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
