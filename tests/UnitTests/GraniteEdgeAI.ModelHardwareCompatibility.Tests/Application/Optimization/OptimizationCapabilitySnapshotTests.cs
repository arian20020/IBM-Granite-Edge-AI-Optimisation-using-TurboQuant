using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Optimization;

/// <summary>
/// The capability snapshot: one route, one sealed payload, never both.
///
/// This is the seam that keeps the shared planner honest. If a snapshot could
/// carry a GGUF payload and an OpenVINO payload at once, the shared layer would
/// have to decide which one it meant, and that decision is exactly the
/// route-specific reasoning the design puts in the executors.
/// </summary>
[TestClass]
public sealed class OptimizationCapabilitySnapshotTests
{
    private const string Digest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    private static class OpenVinoTestData
    {
        internal static OpenVinoCapabilityPayload StandardCpuPayload() =>
            OpenVinoCapabilityPayload.Create(
                "2026.1.0",
                [
                    OpenVinoAdmittedConfiguration.Create(
                        "ov-cpu-int8",
                        DeviceRouteId.Cpu,
                        OpenVinoWeightFormat.Int8,
                        OpenVinoKvCacheFormat.U8,
                        OpenVinoPerformanceHint.Latency,
                        OpenVinoCompiledCachePolicy.Enabled,
                        streams: 1,
                        minimumContextTokens: 512,
                        maximumContextTokens: 32768,
                        SupportLevel.DeclaredSupported,
                        requiresEvidence: false)
                ]);
    }

    private static class GgufTestData
    {
        internal static GgufCapabilityPayload StandardCpuPayload() =>
            GgufCapabilityPayload.Create(
                "b4321",
                [
                    GgufAdmittedConfiguration.Create(
                        "gguf-cpu-q4",
                        CompatibilityBackend.Cpu,
                        DeviceRouteId.Cpu,
                        GgufWeightFormat.Q4KM,
                        GgufKvCacheFormat.F16,
                        GpuOffloadLevel.None,
                        minimumContextTokens: 512,
                        maximumContextTokens: 32768,
                        SupportLevel.DeclaredSupported,
                        requiresEvidence: false)
                ]);
    }

    [TestMethod]
    public void SnapshotCarriesExactlyOnePayload()
    {
        OpenVinoCapabilityPayload payload = OpenVinoTestData.StandardCpuPayload();

        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino("ov-cap-01", Digest, payload);

        Assert.AreEqual(OptimizationRoute.OpenVino, snapshot.Route);
        Assert.AreSame(payload, snapshot.OpenVino);
        Assert.IsNull(snapshot.Gguf);
    }

    [TestMethod]
    public void GgufSnapshotCarriesOnlyItsOwnPayload()
    {
        GgufCapabilityPayload payload = GgufTestData.StandardCpuPayload();

        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForGguf("gguf-cap-01", Digest, payload);

        Assert.AreEqual(OptimizationRoute.Gguf, snapshot.Route);
        Assert.AreSame(payload, snapshot.Gguf);
        Assert.IsNull(snapshot.OpenVino);
    }

    [TestMethod]
    public void NullPayloadIsRefused()
    {
        // A snapshot with no payload names a route and proves nothing about it,
        // which is the shape of an unevidenced capability claim.
        Assert.ThrowsExactly<ArgumentNullException>(
            () => OptimizationCapabilitySnapshot.ForOpenVino("ov-cap-01", Digest, null!));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void EmptySnapshotIdIsRefused(string id)
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCapabilitySnapshot.ForOpenVino(
                id, Digest, OpenVinoTestData.StandardCpuPayload()));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-hash")]
    [DataRow("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [DataRow("abcdef0123456789abcdef0123456789abcdef0123456789abcdef012345678")]
    [DataRow("abcdef0123456789abcdef0123456789abcdef0123456789abcdef01234567890")]
    public void MalformedCapabilityDigestIsRefused(string digest)
    {
        // Lowercase, exactly 64 hex characters. An executor recomputes this and
        // compares it ordinally, so an uppercase digest would fail to match a
        // snapshot that had not actually changed.
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-cap-01", digest, OpenVinoTestData.StandardCpuPayload()));
    }

    [TestMethod]
    public void PayloadWithNoAdmittedConfigurationIsRefused()
    {
        // An empty admitted set is not a route with modest capability, it is a
        // route with no evidence. Generating from it would produce nothing and
        // report it as though the machine had been assessed.
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoCapabilityPayload.Create("2026.1.0", []));
    }

    [TestMethod]
    public void PayloadCopiesItsAdmittedConfigurations()
    {
        // A caller still holding the list could otherwise add an unadmitted
        // combination after the snapshot was hashed.
        List<OpenVinoAdmittedConfiguration> admitted =
        [
            OpenVinoAdmittedConfiguration.Create(
                "ov-cpu-int8", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                SupportLevel.DeclaredSupported, false)
        ];

        OpenVinoCapabilityPayload payload =
            OpenVinoCapabilityPayload.Create("2026.1.0", admitted);

        admitted.Clear();

        Assert.AreEqual(1, payload.Admitted.Count);
    }

    [TestMethod]
    public void CapabilityAdmissionCollectionsCannotBeMutatedThroughADowncast()
    {
        OpenVinoCapabilityPayload openVino = OpenVinoTestData.StandardCpuPayload();
        GgufCapabilityPayload gguf = GgufTestData.StandardCpuPayload();

        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<OpenVinoAdmittedConfiguration>)openVino.Admitted)[0] =
                openVino.Admitted[0]);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<GgufAdmittedConfiguration>)gguf.Admitted)[0] =
                gguf.Admitted[0]);
    }

    [TestMethod]
    public void PublicFactoriesSnapshotHostileCollectionsExactlyOnce()
    {
        OpenVinoAdmittedConfiguration firstOpenVino =
            OpenVinoAdmittedConfiguration.Create(
                "first-ov", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                SupportLevel.DeclaredSupported, false);
        OpenVinoAdmittedConfiguration laterOpenVino =
            OpenVinoAdmittedConfiguration.Create(
                "later-ov", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                SupportLevel.DeclaredSupported, false);
        GgufAdmittedConfiguration firstGguf = GgufAdmittedConfiguration.Create(
            "first-gguf", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, false);
        GgufAdmittedConfiguration laterGguf = GgufAdmittedConfiguration.Create(
            "later-gguf", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, false);

        TimeVaryingReadOnlyList<OpenVinoAdmittedConfiguration> openVinoAdmissions =
            new(count: 0, [firstOpenVino], [laterOpenVino]);
        TimeVaryingReadOnlyList<OpenVinoExecutionAuthority> openVinoAuthorities =
            new(
                count: 0,
                [OpenVinoAuthority(firstOpenVino.EvidenceId)],
                [OpenVinoAuthority(laterOpenVino.EvidenceId)]);
        TimeVaryingReadOnlyList<GgufAdmittedConfiguration> ggufAdmissions =
            new(count: 0, [firstGguf], [laterGguf]);
        TimeVaryingReadOnlyList<ContextTokenCount> contexts = new(
            count: 0,
            [ContextTokenCount.FromTokens(1024)],
            [ContextTokenCount.FromTokens(2048)]);

        OpenVinoCapabilityPayload openVino = OpenVinoCapabilityPayload.Create(
            "runtime", openVinoAdmissions, openVinoAuthorities);
        GgufCapabilityPayload gguf = GgufCapabilityPayload.Create(
            "runtime", ggufAdmissions);
        OptimizationWorkload workload = OptimizationWorkload.Create(
            "workload", 1, OptimizationAssessment.Poor, contexts);

        Assert.AreEqual("first-ov", openVino.Admitted.Single().EvidenceId);
        Assert.IsTrue(openVino.ExecutionAuthorities.ContainsKey("first-ov"));
        Assert.IsFalse(openVino.ExecutionAuthorities.ContainsKey("later-ov"));
        Assert.AreEqual("first-gguf", gguf.Admitted.Single().EvidenceId);
        Assert.AreEqual(1024, workload.CandidateContexts.Single().Tokens);
        Assert.AreEqual(1, openVinoAdmissions.EnumerationCount);
        Assert.AreEqual(1, openVinoAuthorities.EnumerationCount);
        Assert.AreEqual(1, ggufAdmissions.EnumerationCount);
        Assert.AreEqual(1, contexts.EnumerationCount);
    }

    [TestMethod]
    public void AnalogousAuthorityFactoriesSnapshotTheirPublicCollectionsOnce()
    {
        Dictionary<string, string> firstVersions = new(StringComparer.Ordinal)
        {
            ["openvino"] = "2026.1.0"
        };
        Dictionary<string, string> laterVersions = new(StringComparer.Ordinal)
        {
            ["injected"] = "later"
        };
        TimeVaryingReadOnlyDictionary<string, string> authorityVersions =
            new(firstVersions, laterVersions);
        TimeVaryingReadOnlyDictionary<string, string> payloadVersions =
            new(firstVersions, laterVersions);
        TimeVaryingReadOnlyList<GgufExecutionProfileAuthority> profiles =
            new(
                count: 0,
                [GgufExecutionProfileAuthority.Create(
                    "first", EvidenceGrade.Estimated, "profile", false, 1, 1, 1)],
                [GgufExecutionProfileAuthority.Create(
                    "later", EvidenceGrade.Estimated, "profile", false, 1, 1, 1)]);
        OpenVinoExecutionAuthority authority = OpenVinoExecutionAuthority.Create(
            "evidence", "configuration", OpenVinoWeightPrecision.Fp16,
            OpenVinoBuildIdentity.Create("runtime", "genai", "tokenizers", Digest),
            authorityVersions,
            compiledCacheIsDisposable: true);
        OpenVinoExecutionPayload payload = OpenVinoExecutionPayload.Create(
            "configuration", "CPU", "Released candidate", "evidence",
            OpenVinoWeightPrecision.Fp16, OpenVinoWeightPrecision.Fp16,
            OpenVinoKvCachePrecision.U8, false, true, false, true,
            OpenVinoBuildIdentity.Create("runtime", "genai", "tokenizers", Digest),
            payloadVersions);
        GgufRuntimeAuthority gguf = GgufRuntimeAuthority.Create(
            "runtime", "0123456789abcdef0123456789abcdef01234567",
            profiles);

        CollectionAssert.AreEqual(new[] { "openvino" }, authority.OptimizerVersions.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { "openvino" }, payload.OptimizerVersions.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { "first" }, gguf.Profiles.Keys.ToArray());
        Assert.AreEqual(1, authorityVersions.EnumerationCount);
        Assert.AreEqual(1, payloadVersions.EnumerationCount);
        Assert.AreEqual(1, profiles.EnumerationCount);
    }

    private static OpenVinoExecutionAuthority OpenVinoAuthority(string evidenceId) =>
        OpenVinoExecutionAuthority.Create(
            evidenceId, "configuration", OpenVinoWeightPrecision.Fp16,
            OpenVinoBuildIdentity.Create("runtime", "genai", "tokenizers", Digest),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openvino"] = "2026.1.0"
            },
            compiledCacheIsDisposable: true);

    [TestMethod]
    public void AdmittedConfigurationRejectsInvertedContextBounds()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoAdmittedConfiguration.Create(
                "ov-cpu-int8", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1,
                minimumContextTokens: 32768,
                maximumContextTokens: 512,
                SupportLevel.DeclaredSupported,
                requiresEvidence: false));
    }

    [TestMethod]
    [DataRow((int)SupportLevel.Unknown)]
    [DataRow(-1)]
    [DataRow(3)]
    public void OpenVinoAdmittedConfigurationRejectsInvalidSupportLevels(int rawLevel)
    {
        // Unknown fails closed: an entry whose support level was never
        // established must not be planned against.
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoAdmittedConfiguration.Create(
                "ov-cpu-int8", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                (SupportLevel)rawLevel,
                requiresEvidence: false));
    }

    [TestMethod]
    [DataRow((int)SupportLevel.Unknown)]
    [DataRow(-1)]
    [DataRow(3)]
    public void GgufAdmittedConfigurationRejectsInvalidSupportLevels(int rawLevel)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufAdmittedConfiguration.Create(
                "gguf-cpu-q4", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
                GpuOffloadLevel.None, 512, 32768,
                (SupportLevel)rawLevel, requiresEvidence: false));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(5)]
    public void WorkloadRejectsUndefinedQualityFloors(int rawQuality)
    {
        Assert.ThrowsExactly<ArgumentException>(() => OptimizationWorkload.Create(
            "workload", 1, (OptimizationAssessment)rawQuality,
            [ContextTokenCount.FromTokens(1024)]));
    }

    [TestMethod]
    public void AdmittedConfigurationRejectsAnEmptyEvidenceId()
    {
        // The evidence id is how a candidate carries its provenance back to the
        // record that admitted it. Without one, nothing can be traced.
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoAdmittedConfiguration.Create(
                "  ", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                SupportLevel.DeclaredSupported, false));
    }

    [TestMethod]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq4)]
    [DataRow(OpenVinoKvCacheFormat.TurboQuantTbq3)]
    public void ExperimentalTurboQuantIsOnlyAdmissibleAsAnExperimentalEntry(
        OpenVinoKvCacheFormat cache)
    {
        // TurboQuant is gated on exact evidence. Declaring it as ordinary
        // released support is the one mislabelling that would let it run
        // without anyone opting in.
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoAdmittedConfiguration.Create(
                "ov-cpu-tbq4", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int4,
                cache, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                SupportLevel.DeclaredSupported,
                requiresEvidence: true));
    }

    [TestMethod]
    public void ExperimentalTurboQuantIsAdmissibleWithExactEvidence()
    {
        OpenVinoAdmittedConfiguration admitted = OpenVinoAdmittedConfiguration.Create(
            "ov-cpu-tbq4", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int4,
            OpenVinoKvCacheFormat.TurboQuantTbq4, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
            SupportLevel.Experimental,
            requiresEvidence: true);

        Assert.AreEqual(SupportLevel.Experimental, admitted.Level);
        Assert.AreEqual(OpenVinoKvCacheFormat.TurboQuantTbq4, admitted.KvCache);
    }

    [TestMethod]
    public void OpenVinoAuthorityTurboIdentityMustMatchTheAdmittedCacheFamily()
    {
        OpenVinoAdmittedConfiguration released = OpenVinoAdmittedConfiguration.Create(
            "ov", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
            SupportLevel.DeclaredSupported, false);
        OpenVinoBuildIdentity build = OpenVinoBuildIdentity.Create(
            "2026.1.0", "genai", "tokenizers", Digest);
        TurboQuantBuildIdentity turbo = TurboQuantBuildIdentity.Create(
            "0123456789abcdef0123456789abcdef01234567",
            "89abcdef0123456789abcdef0123456789abcdef",
            Digest, Digest);

        Assert.ThrowsExactly<ArgumentException>(() =>
            OpenVinoCapabilityPayload.Create(
                "2026.1.0", [released],
                [OpenVinoExecutionAuthority.Create(
                    "ov", "configuration", OpenVinoWeightPrecision.Fp16,
                    build, new Dictionary<string, string> { ["openvino"] = "2026.1.0" },
                    compiledCacheIsDisposable: true,
                    turboQuantBuild: turbo)]));
    }

    [TestMethod]
    public void RouteAuthoritiesAreImmutableSortedAndRejectDuplicateEvidence()
    {
        Dictionary<string, string> versions = new(StringComparer.Ordinal)
        {
            ["z-optimizer"] = "2",
            ["a-optimizer"] = "1"
        };
        OpenVinoExecutionAuthority openVino = OpenVinoExecutionAuthority.Create(
            "ov", "configuration", OpenVinoWeightPrecision.Fp16,
            OpenVinoBuildIdentity.Create("runtime", "genai", "tokenizers", Digest),
            versions,
            compiledCacheIsDisposable: true);
        versions.Clear();

        CollectionAssert.AreEqual(
            new[] { "a-optimizer", "z-optimizer" },
            openVino.OptimizerVersions.Keys.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IDictionary<string, string>)openVino.OptimizerVersions)["injected"] = "3");
        Assert.ThrowsExactly<ArgumentException>(() => GgufRuntimeAuthority.Create(
            "runtime", "0123456789abcdef0123456789abcdef01234567",
            [
                GgufExecutionProfileAuthority.Create(
                    "same", EvidenceGrade.Estimated, "profile-a", false, 1, 1, 1),
                GgufExecutionProfileAuthority.Create(
                    "same", EvidenceGrade.Estimated, "profile-b", false, 1, 1, 1)
            ]));
    }

    private sealed class TimeVaryingReadOnlyList<T>(
        int count,
        IReadOnlyList<T> first,
        IReadOnlyList<T> later) : IReadOnlyList<T>
    {
        private int enumerations;
        public int EnumerationCount => Volatile.Read(ref enumerations);
        public int Count => count;
        public T this[int index] => first[index];
        public IEnumerator<T> GetEnumerator() =>
            (Interlocked.Increment(ref enumerations) == 1 ? first : later)
            .GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    private sealed class TimeVaryingReadOnlyDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> first,
        IReadOnlyDictionary<TKey, TValue> later) : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private int enumerations;
        public int EnumerationCount => Volatile.Read(ref enumerations);
        public int Count => 0;
        public IEnumerable<TKey> Keys => first.Keys;
        public IEnumerable<TValue> Values => first.Values;
        public TValue this[TKey key] => first[key];
        public bool ContainsKey(TKey key) => first.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) =>
            first.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            (Interlocked.Increment(ref enumerations) == 1 ? first : later)
            .GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    [TestMethod]
    [DataRow(SupportLevel.DeclaredSupported, true)]
    [DataRow(SupportLevel.Experimental, false)]
    public void GgufTurboQuantRequiresExperimentalEvidenceAdmission(
        SupportLevel level,
        bool requiresEvidence)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufAdmittedConfiguration.Create(
                "turbo3",
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.TurboQuant3Bit,
                GpuOffloadLevel.Full,
                512,
                32768,
                level,
                requiresEvidence));
    }

    [TestMethod]
    public void GgufTurboQuantIdentityAcceptsOnlyPinnedBackendImplementations()
    {
        GgufTurboQuantImplementationIdentity atomicBot =
            GgufTurboQuantImplementationIdentity.Create(
                "turbo3",
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu);
        GgufTurboQuantImplementationIdentity animehacker =
            GgufTurboQuantImplementationIdentity.Create(
                "tq3_0",
                "5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc",
                CompatibilityBackend.IntelSycl,
                DeviceRouteId.IntelDiscreteGpu);

        Assert.AreEqual("turbo3", atomicBot.RuntimeName);
        Assert.AreEqual("tq3_0", animehacker.RuntimeName);
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufTurboQuantImplementationIdentity.Create(
                "turbo3",
                "5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc",
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu));
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufTurboQuantImplementationIdentity.Create(
                "turbo3",
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                CompatibilityBackend.IntelSycl,
                DeviceRouteId.IntelIntegratedGpu));
    }

    [TestMethod]
    public void GgufTurboQuantCapabilityRequiresMatchingPinnedIdentity()
    {
        GgufAdmittedConfiguration turbo = GgufAdmittedConfiguration.Create(
            "turbo3", CompatibilityBackend.IntelVulkan,
            DeviceRouteId.IntelIntegratedGpu, GgufWeightFormat.Imported,
            GgufKvCacheFormat.TurboQuant3Bit, GpuOffloadLevel.Full,
            512, 32768, SupportLevel.Experimental, requiresEvidence: true);
        GgufTurboQuantImplementationIdentity identity =
            GgufTurboQuantImplementationIdentity.Create(
                "turbo3",
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu);

        Assert.ThrowsExactly<ArgumentException>(
            () => GgufCapabilityPayload.Create("turbo3", [turbo]));
        Assert.ThrowsExactly<ArgumentException>(() => GgufCapabilityPayload.Create(
            "different-runtime", [turbo], turboQuantImplementation: identity));

        GgufAdmittedConfiguration wrongBackend = GgufAdmittedConfiguration.Create(
            "tq3_0", CompatibilityBackend.IntelSycl,
            DeviceRouteId.IntelDiscreteGpu, GgufWeightFormat.Imported,
            GgufKvCacheFormat.TurboQuant3Bit, GpuOffloadLevel.Full,
            512, 32768, SupportLevel.Experimental, requiresEvidence: true);
        Assert.ThrowsExactly<ArgumentException>(() => GgufCapabilityPayload.Create(
            "turbo3", [wrongBackend], turboQuantImplementation: identity));

        GgufCapabilityPayload payload = GgufCapabilityPayload.Create(
            "turbo3", [turbo], turboQuantImplementation: identity);

        Assert.AreSame(identity, payload.TurboQuantImplementation);
    }

    [TestMethod]
    public void NonTurboGgufCapabilityForbidsTurboQuantIdentity()
    {
        GgufTurboQuantImplementationIdentity identity =
            GgufTurboQuantImplementationIdentity.Create(
                "turbo3",
                "519f0c594a8e31467d2e2f2cf17054c9e7e11536",
                CompatibilityBackend.IntelVulkan,
                DeviceRouteId.IntelIntegratedGpu);

        Assert.ThrowsExactly<ArgumentException>(() => GgufCapabilityPayload.Create(
            "b4321",
            [GgufAdmittedConfiguration.Create(
                "standard", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
                GgufWeightFormat.Imported, GgufKvCacheFormat.F16,
                GpuOffloadLevel.None, 512, 32768,
                SupportLevel.DeclaredSupported, requiresEvidence: false)],
            turboQuantImplementation: identity));
    }

    [TestMethod]
    public void DuplicateEvidenceIdsAreRefusedForBothRoutes()
    {
        OpenVinoAdmittedConfiguration ov = OpenVinoAdmittedConfiguration.Create(
            "duplicate", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
            OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
            OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
            SupportLevel.DeclaredSupported, false);
        GgufAdmittedConfiguration gguf = GgufAdmittedConfiguration.Create(
            "duplicate", CompatibilityBackend.Cpu, DeviceRouteId.Cpu,
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
            GpuOffloadLevel.None, 512, 32768,
            SupportLevel.DeclaredSupported, false);

        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoCapabilityPayload.Create("2026.1.0", [ov, ov]));
        Assert.ThrowsExactly<ArgumentException>(
            () => GgufCapabilityPayload.Create("b4321", [gguf, gguf]));
    }

    [TestMethod]
    public void ConversionSourceBindingRequiresExactIdentityAndEstablishedPrecision()
    {
        OptimizationJourneyBinding binding = OptimizationJourneyBinding.Create(
            "source-run", "source-handoff", Digest, 4096, "source-hardware", Digest);

        GgufConversionSourceBinding source = GgufConversionSourceBinding.Create(
            WeightQuantisation.F16, binding);

        Assert.AreEqual(Digest, source.SourceSha256);
        Assert.AreEqual(4096UL, source.SourceLengthBytes);
        Assert.AreEqual(WeightQuantisation.F16, source.Precision);
        Assert.AreSame(binding, source.Journey);
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufConversionSourceBinding.Create(WeightQuantisation.Unknown, binding));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            GgufConversionSourceBinding.Create((WeightQuantisation)999, binding));
    }

    [TestMethod]
    public void EveryRouteHasAFactory()
    {
        // A route with no way to build a snapshot could never be planned for,
        // and the gap would show up as an unexplained absence rather than a
        // compile error.
        foreach (OptimizationRoute route in Enum.GetValues<OptimizationRoute>())
        {
            OptimizationCapabilitySnapshot snapshot = route switch
            {
                OptimizationRoute.Gguf => OptimizationCapabilitySnapshot.ForGguf(
                    "id", Digest, GgufTestData.StandardCpuPayload()),
                OptimizationRoute.OpenVino => OptimizationCapabilitySnapshot.ForOpenVino(
                    "id", Digest, OpenVinoTestData.StandardCpuPayload()),
                _ => throw new AssertFailedException($"{route} has no snapshot factory.")
            };

            Assert.AreEqual(route, snapshot.Route);
        }
    }

    [TestMethod]
    [DataRow(@"C:\models\granite.gguf")]
    [DataRow("/home/user/model")]
    [DataRow(@"..\..\secrets")]
    [DataRow("share|name")]
    public void IdentifiersThatLookLikeAPathAreRefused(string identifier)
    {
        // Identifiers travel into plans, manifests and support records. An
        // adapter passing a filesystem path as its evidence id would publish
        // that path wherever those go, and the canary would keep passing
        // because the member was reviewed once while holding something
        // harmless. So the shape is enforced, not trusted.
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoAdmittedConfiguration.Create(
                identifier, DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                SupportLevel.DeclaredSupported, false));

        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCapabilitySnapshot.ForOpenVino(
                identifier, Digest, OpenVinoTestData.StandardCpuPayload()));
    }

    [TestMethod]
    public void UnboundedIdentifierContentIsRefused()
    {
        // Past any length an identifier needs is the range where something else
        // is being carried.
        Assert.ThrowsExactly<ArgumentException>(
            () => OptimizationCapabilitySnapshot.ForOpenVino(
                new string('a', 129), Digest, OpenVinoTestData.StandardCpuPayload()));
    }

    [TestMethod]
    public void RuntimeVersionThatLooksLikeAPathIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => OpenVinoCapabilityPayload.Create(
                @"C:\Program Files\openvino",
                [
                    OpenVinoAdmittedConfiguration.Create(
                        "ov-cpu-int8", DeviceRouteId.Cpu, OpenVinoWeightFormat.Int8,
                        OpenVinoKvCacheFormat.U8, OpenVinoPerformanceHint.Latency,
                        OpenVinoCompiledCachePolicy.Enabled, 1, 512, 32768,
                        SupportLevel.DeclaredSupported, false)
                ]));
    }
}
