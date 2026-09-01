using System.Security.Cryptography;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Optimization;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;
using RouteCompiledCachePolicy = GraniteEdgeAI.Features.OpenVinoRoute.Optimization.OpenVinoCompiledCachePolicy;
using ContractExecutionPayload = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OptimizationExecutionPayload;
using ContractOpenVinoBuildIdentity = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoBuildIdentity;
using ContractOpenVinoExecutionPayload = GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution.OpenVinoExecutionPayload;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("OpenVinoRoute")]
[TestCategory("OfficialNative")]
[DoNotParallelize]
public sealed class OptimizationEndToEndTests
{
    private static readonly OpenVinoOptimizationObjective[] PersistentObjectives =
    [
        OpenVinoOptimizationObjective.Quality,
        OpenVinoOptimizationObjective.Balanced,
        OpenVinoOptimizationObjective.Efficiency
    ];
    private static readonly OpenVinoOptimizationObjective[] SupportedLegacyObjectives =
    [
        OpenVinoOptimizationObjective.Automatic,
        OpenVinoOptimizationObjective.Quality
    ];
    private static readonly OpenVinoOptimizationObjective[] UnsupportedLegacyCacheObjectives =
    [
        OpenVinoOptimizationObjective.Balanced,
        OpenVinoOptimizationObjective.Efficiency
    ];

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    [Timeout(600_000)]
    public async Task SealedWorkerProducesDistinctFp16Int8Int4PackagesOffline()
    {
        string stage = RequireStage();
        OpenVinoRouteService unusedRoute = new(new UnusedWorkerClient());
        string manifestDigest = Digest(Path.Combine(stage, "converter-manifest.json"));
        SealedOpenVinoOptimizationPipeline pipeline = new(
            stage,
            manifestDigest,
            unusedRoute);
        SealedOpenVinoConversionPipeline converter = new(
            stage,
            manifestDigest,
            unusedRoute);
        Guid operationId = Guid.NewGuid();
        string root = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-OptimizationE2E-" + operationId.ToString("N"));
        string source = Path.Combine(root, "source");
        string baseline = Path.Combine(root, "baseline");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(baseline);
        CopySourceFixture(source);
        try
        {
            await converter.ConvertAsync(
                new OpenVinoConverterInvocation(
                    operationId,
                    source,
                    baseline,
                    new string('a', 64)),
                CancellationToken.None);
            Dictionary<OpenVinoOptimizationObjective, long> sizes = [];
            foreach (OpenVinoOptimizationObjective objective in PersistentObjectives)
            {
                string staging = Path.Combine(root, objective.ToString());
                Directory.CreateDirectory(staging);
                OpenVinoOptimizationCandidate candidate =
                    OpenVinoOptimizationLegacyRegistryV1.GetRequired(objective);
                OpenVinoOptimizationCompletion result = await pipeline.OptimizeAsync(
                    new OpenVinoOptimizationInvocation(
                        Guid.NewGuid(),
                        baseline,
                        staging,
                        new string('b', 64),
                        candidate),
                    CancellationToken.None);
                Assert.AreEqual(candidate.PersistentArtifact.WeightPrecision,
                    result.ActualWeightPrecision);
                Assert.IsTrue(File.Exists(Path.Combine(staging, "openvino_model.xml")));
                Assert.IsTrue(File.Exists(Path.Combine(staging, "openvino_tokenizer.xml")));
                sizes.Add(objective, new FileInfo(
                    Path.Combine(staging, "openvino_model.bin")).Length);
            }
            Assert.IsTrue(sizes[OpenVinoOptimizationObjective.Balanced] <
                sizes[OpenVinoOptimizationObjective.Quality]);
            Assert.IsTrue(sizes[OpenVinoOptimizationObjective.Efficiency] <
                sizes[OpenVinoOptimizationObjective.Balanced]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("ManualRealModel")]
    [Timeout(7_200_000)]
    public async Task SealedWorkerOptimizesTheConfiguredRealPackageToInt4Offline()
    {
        string? sourceValue = Environment.GetEnvironmentVariable(
            "GRANITE_OPENVINO_REAL_MODEL");
        if (string.IsNullOrWhiteSpace(sourceValue))
        {
            Assert.Inconclusive(
                "GRANITE_OPENVINO_REAL_MODEL is required for the real-package check.");
        }

        string source = Path.GetFullPath(sourceValue!);
        string stage = RequireStage();
        string manifestDigest = Digest(Path.Combine(stage, "converter-manifest.json"));
        SealedOpenVinoOptimizationPipeline pipeline = new(
            stage,
            manifestDigest,
            new OpenVinoRouteService(new UnusedWorkerClient()));
        string output = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-RealInt4-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            OpenVinoOptimizationCandidate candidate =
                OpenVinoOptimizationLegacyRegistryV1.GetRequired(
                    OpenVinoOptimizationObjective.Efficiency);
            OpenVinoOptimizationCompletion completion = await pipeline.OptimizeAsync(
                new OpenVinoOptimizationInvocation(
                    Guid.NewGuid(),
                    source,
                    output,
                    new string('a', 64),
                    candidate),
                CancellationToken.None);

            Assert.AreEqual(
                OpenVinoWeightPrecision.FourBit,
                completion.ActualWeightPrecision);
            string sourceWeights = Path.Combine(source, "openvino_model.bin");
            string optimizedWeights = Path.Combine(output, "openvino_model.bin");
            Assert.IsTrue(File.Exists(optimizedWeights));
            Assert.IsTrue(
                new FileInfo(optimizedWeights).Length < new FileInfo(sourceWeights).Length,
                "The real INT4 artifact must be smaller than its FP16 source.");
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    [Timeout(600_000)]
    public async Task SupportedLegacyCandidatesPublishAndEnabledCacheFailsClosed()
    {
        string converterStage = RequireStage();
        string? officialValue = Environment.GetEnvironmentVariable(
            "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        if (string.IsNullOrWhiteSpace(officialValue))
        {
            Assert.Inconclusive("OPENVINO_OFFICIAL_WORKER_STAGE_A is required.");
        }
        string officialStage = Path.GetFullPath(officialValue!);
        string manifestDigest = Digest(Path.Combine(
            converterStage, "converter-manifest.json"));
        OpenVinoRouteService route = CreateRoute(officialStage);
        SealedOpenVinoConversionPipeline converter = new(
            converterStage, manifestDigest, route);
        OpenVinoOptimizationService service = new(
            new SealedOpenVinoOptimizationPipeline(
                converterStage, manifestDigest, route),
            _ => true);
        string root = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-OptimizationServiceE2E-" +
            Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string baseline = Path.Combine(root, "baseline");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(baseline);
        CopySourceFixture(source);
        try
        {
            await converter.ConvertAsync(
                new OpenVinoConverterInvocation(
                    Guid.NewGuid(), source, baseline, new string('a', 64)),
                CancellationToken.None);
            foreach (OpenVinoOptimizationObjective objective in SupportedLegacyObjectives)
            {
                OpenVinoOptimizationCandidate candidate =
                    OpenVinoOptimizationLegacyRegistryV1.GetRequired(objective);
                string destination = Path.Combine(root, "published-" + objective);
                List<OpenVinoOptimizationStage> stages = [];
                OpenVinoOptimizationResult result = await service.OptimizeLegacyV1Async(
                    new OpenVinoOptimizationLegacyRequestV1(
                        baseline, destination, candidate, Confirmed: true),
                    new InlineProgress<OpenVinoOptimizationProgress>(value =>
                        stages.Add(value.Stage)),
                    CancellationToken.None);
                Assert.AreEqual(OpenVinoOptimizationStatus.Published, result.Status,
                    objective + ": " + result.SupportCode?.ToProtocolValue() +
                    "; stages=" + string.Join(',', stages));
                Assert.AreEqual(candidate.PersistentArtifact.WeightPrecision,
                    result.ActualWeightPrecision);
                Assert.AreEqual(candidate.Runtime.KvCachePrecision,
                    result.ActualKvCachePrecision);
                Assert.AreEqual("CPU", result.ActualDevice);
                Assert.IsTrue(File.Exists(Path.Combine(
                    destination, OpenVinoOptimizationProvenance.FileName)));
                Assert.IsFalse(File.Exists(Path.Combine(
                    destination, OpenVinoProvenance.FileName)));

                OpenVinoRouteInspectionResult independent = await route.InspectAsync(
                    destination, CancellationToken.None);
                try
                {
                    Assert.IsTrue(independent.Outcome is OpenVinoRouteInspectionOutcome.Ready or
                        OpenVinoRouteInspectionOutcome.ReadyWithWarnings,
                        independent.Failure?.SupportCode);
                }
                finally
                {
                    independent.HandoffLease?.Dispose();
                }
            }
            foreach (OpenVinoOptimizationObjective objective in
                     UnsupportedLegacyCacheObjectives)
            {
                OpenVinoOptimizationCandidate candidate =
                    OpenVinoOptimizationLegacyRegistryV1.GetRequired(objective);
                string destination = Path.Combine(root, "rejected-" + objective);
                List<OpenVinoOptimizationStage> stages = [];

                OpenVinoOptimizationResult result = await service.OptimizeLegacyV1Async(
                    new OpenVinoOptimizationLegacyRequestV1(
                        baseline, destination, candidate, Confirmed: true),
                    new InlineProgress<OpenVinoOptimizationProgress>(value =>
                        stages.Add(value.Stage)),
                    CancellationToken.None);

                Assert.AreEqual(OpenVinoOptimizationStatus.Failed, result.Status);
                Assert.AreEqual(OpenVinoSupportCode.OptimizationUnsupported,
                    result.SupportCode);
                CollectionAssert.AreEqual(
                    new[] { OpenVinoOptimizationStage.Preflight },
                    stages);
                Assert.IsFalse(Directory.Exists(destination));
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("StableRouteAcceptance")]
    [Timeout(600_000)]
    public async Task ProjectedC1PlanPublishesSchemaV2PackageThroughSealedPipeline()
    {
        string converterStage = RequireStage();
        string officialStage = RequireOfficialStage();
        string manifestDigest = Digest(Path.Combine(
            converterStage, "converter-manifest.json"));
        OpenVinoBuildEvidence currentBuilds = new(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            Digest(Path.Combine(officialStage, "worker-manifest.json")));
        OpenVinoOptimizationCapabilityEvidence currentEvidence =
            NativeCapabilityEvidence(currentBuilds);
        OpenVinoRouteService route = CreateRoute(officialStage);
        SealedOpenVinoConversionPipeline converter = new(
            converterStage, manifestDigest, route);
        OpenVinoOptimizationService service = new(
            new SealedOpenVinoOptimizationPipeline(
                converterStage, manifestDigest, route),
            _ => true);
        string root = Path.Combine(
            Path.GetTempPath(), "GraniteEdgeAI-C1-OptimizationE2E-" +
            Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string baseline = Path.Combine(root, "baseline");
        string destination = Path.Combine(root, "published");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(baseline);
        CopySourceFixture(source);
        try
        {
            await converter.ConvertAsync(
                new OpenVinoConverterInvocation(
                    Guid.NewGuid(), source, baseline, new string('a', 64)),
                CancellationToken.None);
            OpenVinoStaticPackageEvidence baselineEvidence =
                new OpenVinoStaticPackageInspector().Inspect(baseline).Evidence!;
            OpenVinoCapabilityPayload payload =
                OpenVinoOptimizationCapabilityProjector.Project(
                    currentEvidence);
            OpenVinoAdmittedConfiguration admission = payload.Admitted.Single(
                static item => item.EvidenceId == "OV-STD-CPU-INT8-U8-01");
            OptimizationCapabilitySnapshot snapshot =
                OptimizationCapabilitySnapshot.ForOpenVino(
                    "ov-native-e2e-1",
                    DigestCapability(payload),
                    payload);
            OptimizationExecutionPlan plan = IssuePlan(
                admission,
                snapshot,
                baselineEvidence,
                currentEvidence);
            OpenVinoOptimizationAdaptation adaptation =
                OpenVinoOptimizationPlanAdapter.Adapt(
                    plan,
                    snapshot,
                    currentEvidence,
                    baselineEvidence.ModelSha256,
                    checked((ulong)baselineEvidence.ModelLengthBytes));
            Assert.AreEqual(OpenVinoOptimizationAdaptationStatus.Ready,
                adaptation.Status);
            Assert.AreEqual(admission.EvidenceId, adaptation.Candidate!.EvidenceId);

            OptimizationExecutionResult result = await service.ExecuteAsync(
                new OpenVinoOptimizationRequest(
                    baseline,
                    destination,
                    plan,
                    new FixedCurrentStateProvider(new(
                        snapshot,
                        currentEvidence,
                        plan.Binding.ModelInspectionRunId,
                        plan.Binding.ModelInspectionHandoffId,
                        plan.Binding.ProductHardwareRunId,
                        plan.Binding.HardwareSnapshotSha256)),
                    Confirmed: true),
                progress: null,
                CancellationToken.None);

            Assert.AreEqual(OptimizationExecutionStatus.SucceededPersistent,
                result.Status);
            Assert.AreEqual(plan.OptimizationPlanId, result.OptimizationPlanId);
            Assert.AreEqual(plan.ConfigurationSha256, result.ConfigurationSha256);
            Assert.IsNotNull(result.OutputIdentity);
            Assert.IsNotNull(result.OutputManifestSha256);
            Assert.IsGreaterThan(0UL, result.OutputSizeBytes);
            OpenVinoOptimizationProvenance provenance =
                OpenVinoOptimizationProvenance.Read(destination);
            Assert.AreEqual(OpenVinoOptimizationProvenance.CurrentSchemaVersion,
                provenance.SchemaVersion);
            Assert.AreEqual(plan.OptimizationPlanId, provenance.OptimizationPlanId);
            Assert.AreEqual(plan.ConfigurationSha256,
                provenance.ConfigurationSha256);
            Assert.AreEqual(snapshot.SnapshotId,
                provenance.CapabilitySnapshotId);
            Assert.AreEqual(snapshot.CapabilitySnapshotSha256,
                provenance.CapabilitySnapshotSha256);
            Assert.AreEqual(result.OutputManifestSha256,
                provenance.OutputManifestSha256);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("ManualRealModel")]
    [Timeout(7_200_000)]
    public async Task ConfiguredRealPackageCompletesTheInt4ValidationAndSmokeFlow()
    {
        string? sourceValue = Environment.GetEnvironmentVariable(
            "GRANITE_OPENVINO_REAL_MODEL");
        if (string.IsNullOrWhiteSpace(sourceValue))
        {
            Assert.Inconclusive(
                "GRANITE_OPENVINO_REAL_MODEL is required for the real-package check.");
        }

        string source = Path.GetFullPath(sourceValue!);
        string converterStage = RequireStage();
        string officialStage = RequireOfficialStage();
        string manifestDigest = Digest(Path.Combine(
            converterStage, "converter-manifest.json"));
        OpenVinoBuildEvidence currentBuilds = new(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            Digest(Path.Combine(officialStage, "worker-manifest.json")));
        OpenVinoOptimizationCapabilityEvidence currentEvidence =
            NativeCapabilityEvidence(currentBuilds);
        OpenVinoCapabilityPayload payload = OpenVinoOptimizationCapabilityProjector.Project(
            currentEvidence);
        OpenVinoAdmittedConfiguration admission = payload.Admitted.Single(
            static item => item.EvidenceId == "OV-STD-CPU-INT4-U8-01");
        OptimizationCapabilitySnapshot snapshot =
            OptimizationCapabilitySnapshot.ForOpenVino(
                "ov-real-int4-e2e-1",
                DigestCapability(payload),
                payload);
        OpenVinoStaticPackageEvidence sourceEvidence =
            new OpenVinoStaticPackageInspector().Inspect(source).Evidence
            ?? throw new AssertFailedException("The configured package was not inspectable.");
        OptimizationExecutionPlan plan = IssuePlan(
            admission,
            snapshot,
            sourceEvidence,
            currentEvidence);
        OpenVinoRouteService route = CreateRoute(officialStage);
        OpenVinoOptimizationService service = new(
            new SealedOpenVinoOptimizationPipeline(
                converterStage,
                manifestDigest,
                route),
            _ => true);
        string destination = Path.Combine(
            Path.GetTempPath(),
            "GraniteEdgeAI-RealInt4Flow-" + Guid.NewGuid().ToString("N"));
        List<OpenVinoOptimizationStage> stages = [];
        try
        {
            OpenVinoOptimizationResult result = await service.OptimizeAsync(
                new OpenVinoOptimizationRequest(
                    source,
                    destination,
                    plan,
                    new FixedCurrentStateProvider(new(
                        snapshot,
                        currentEvidence,
                        plan.Binding.ModelInspectionRunId,
                        plan.Binding.ModelInspectionHandoffId,
                        plan.Binding.ProductHardwareRunId,
                        plan.Binding.HardwareSnapshotSha256)),
                    Confirmed: true),
                new InlineProgress<OpenVinoOptimizationProgress>(value =>
                    stages.Add(value.Stage)),
                CancellationToken.None);

            Assert.AreEqual(
                OpenVinoOptimizationStatus.Published,
                result.Status,
                $"route={result.SupportCode}; execution={result.ExecutionSupportCode}; "
                + $"stages={string.Join(',', stages)}");
            Assert.IsTrue(File.Exists(Path.Combine(
                destination,
                OpenVinoOptimizationProvenance.FileName)));
        }
        finally
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void SameModuleCallGraphIncludesConstructorsAndDelegateTargets()
    {
        MethodInfo fixture = typeof(OptimizationEndToEndTests).GetMethod(
            nameof(CallGraphOperandFixture),
            BindingFlags.Static | BindingFlags.NonPublic)!;

        SameModuleCallGraph graph = BuildSameModuleCallGraph(fixture);

        Assert.IsTrue(graph.Methods.Any(static method =>
            method.IsConstructor && method.DeclaringType == typeof(CallGraphFixture)));
        Assert.IsTrue(graph.Methods.Any(static method =>
            method.Name == nameof(CallGraphFixture.Combine)));
        Assert.IsTrue(graph.Methods.Any(static method =>
            method.Name == nameof(CallGraphStaticTarget)));
        Assert.IsTrue(graph.Methods.Any(static method =>
            method.Name == nameof(CallGraphFixture.VirtualTarget)));
        Assert.IsTrue(graph.References.Any(static reference =>
            reference.OpCode == OpCodes.Call));
        Assert.IsTrue(graph.References.Any(static reference =>
            reference.OpCode == OpCodes.Callvirt));
        Assert.IsTrue(graph.References.Any(static reference =>
            reference.OpCode == OpCodes.Newobj));
        Assert.IsTrue(graph.References.Any(static reference =>
            reference.OpCode == OpCodes.Ldftn));
        Assert.IsTrue(graph.References.Any(static reference =>
            reference.OpCode == OpCodes.Ldvirtftn));
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void V2AcceptingCoreRetainsC1AuthorityWithoutHiddenLegacyLookup()
    {

        MethodInfo execute = typeof(OpenVinoOptimizationService).GetMethod(
            nameof(OpenVinoOptimizationService.ExecuteAsync),
            BindingFlags.Instance | BindingFlags.Public)!;
        SameModuleCallGraph graph = BuildSameModuleCallGraph(execute);

        Assert.IsTrue(graph.Methods.Any(static method =>
                method.DeclaringType == typeof(OpenVinoOptimizationPlanAdapter) &&
                method.Name == nameof(OpenVinoOptimizationPlanAdapter.Adapt)),
            "The architecture proof must cover the strict V2 adapter reached by ExecuteAsync.");
        Assert.IsFalse(graph.References.Any(static reference =>
                reference.Target.DeclaringType ==
                    typeof(OpenVinoOptimizationLegacyRegistryV1) &&
                reference.Target.Name ==
                    nameof(OpenVinoOptimizationLegacyRegistryV1.GetRequired)),
            "The shared accepting core must not statically reference the V1 lookup.");
        Assert.IsFalse(graph.References.Any(static reference =>
                IsO1OwnedConfigurationLookup(reference.Target)),
            "The shared accepting core must not statically reference any O1-owned " +
            "configuration lookup.");
        string[] hiddenInvocationRisks = HiddenInvocationRisks(graph).ToArray();
        Assert.IsEmpty(hiddenInvocationRisks,
            "The accepting graph must not hide a call behind reflection or dynamic " +
            "invocation: " + string.Join("; ", hiddenInvocationRisks));
        AssertAllowlistedDelegateInvocationBoundaries(graph);

        AssertAuthoritativePayloadMemberTypes(graph);
        AssertNoDuplicatePayloadAuthorityInProductionModule();
        AssertConfigurationDigestDelegatesToPlanAuthority();
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void SemanticAuthorityGuardRejectsDisconnectedPropertyAndFieldDuplicates()
    {
        Type[] duplicates = FindDisallowedPayloadAuthorities(
            [
                typeof(DisconnectedPropertyPayloadFixture),
                typeof(DisconnectedFieldPayloadFixture)
            ],
            allowlist: new HashSet<Type>());

        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(DisconnectedPropertyPayloadFixture),
                typeof(DisconnectedFieldPayloadFixture)
            },
            duplicates);
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void HiddenInvocationGuardRejectsCalliDynamicAndReflectionInvokerFixtures()
    {
        SameModuleCallGraph calli = BuildSameModuleCallGraph(BuildCalliFixture());
        MethodInfo dynamicDispatch = typeof(OptimizationEndToEndTests).GetMethod(
            nameof(DynamicDispatchFixture),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        SameModuleCallGraph dynamicGraph = BuildSameModuleCallGraph(dynamicDispatch);
        MethodInfo delegateDispatch = typeof(OptimizationEndToEndTests).GetMethod(
            nameof(DelegateDispatchFixture),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        SameModuleCallGraph delegateGraph = BuildSameModuleCallGraph(delegateDispatch);
        MethodBase[] reflectionInvokers = ReflectionInvokerFixtureMethods();

        Assert.IsTrue(ContainsHiddenInvocationRisk(calli));
        Assert.IsTrue(ContainsHiddenInvocationRisk(dynamicGraph));
        Assert.IsTrue(ContainsHiddenInvocationRisk(delegateGraph));
        Assert.IsNotEmpty(reflectionInvokers,
            "This target framework must expose a reflection invoker fixture.");
        foreach (MethodBase invoker in reflectionInvokers)
        {
            Assert.IsTrue(IsReflectionInvocationOrCreationApi(invoker),
                invoker.DeclaringType?.FullName);
        }
    }

    [TestMethod]
    [TestCategory("Architecture")]
    public void CanonicalizationGuardRejectsIncrementalHashFixture()
    {
        MethodInfo fixture = typeof(OptimizationEndToEndTests).GetMethod(
            nameof(IncrementalHashFixture),
            BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.IsTrue(ContainsLocalConfigurationCanonicalization(
            BuildSameModuleCallGraph(fixture)));
    }

    private static int CallGraphOperandFixture()
    {
        CallGraphFixture fixture = new DerivedCallGraphFixture();
        Func<int> staticTarget = CallGraphStaticTarget;
        Func<int> virtualTarget = fixture.VirtualTarget;
        return fixture.Combine(staticTarget, virtualTarget);
    }

    private static int CallGraphStaticTarget() => 1;

    private static object DynamicDispatchFixture(dynamic target) => target.Hidden();

    private static int DelegateDispatchFixture(
        Func<int, int> target,
        int value) => target(value);

    private static byte[] IncrementalHashFixture(byte[] input)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(input);
        return hash.GetHashAndReset();
    }

    private static MethodInfo BuildCalliFixture()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("OpenVinoArchitectureCalliFixture_" +
                Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Fixture");
        TypeBuilder type = module.DefineType(
            "CalliFixture",
            TypeAttributes.Public | TypeAttributes.Abstract |
            TypeAttributes.Sealed);
        MethodBuilder method = type.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(int),
            [typeof(IntPtr), typeof(int)]);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Conv_I);
        il.EmitCalli(
            OpCodes.Calli,
            System.Runtime.InteropServices.CallingConvention.Cdecl,
            typeof(int),
            [typeof(int)]);
        il.Emit(OpCodes.Ret);
        return type.CreateType()!.GetMethod(
            "Invoke", BindingFlags.Public | BindingFlags.Static)!;
    }

    private static MethodBase[] ReflectionInvokerFixtureMethods()
    {
        string[] typeNames =
        [
            "System.Reflection.MethodInvoker",
            "System.Reflection.ConstructorInvoker"
        ];
        return typeNames
            .Select(name => typeof(MethodInfo).Assembly.GetType(name))
            .Where(static type => type is not null)
            .Select(static type => (MethodBase?)type!.GetMethods(
                    BindingFlags.Public | BindingFlags.Static |
                    BindingFlags.Instance)
                .FirstOrDefault() ?? type.GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault())
            .Where(static method => method is not null)
            .Cast<MethodBase>()
            .ToArray();
    }

    private sealed class DisconnectedPropertyPayloadFixture
    {
        public string ConfigurationId { get; init; } = string.Empty;
        public string Device { get; init; } = string.Empty;
        public string Maturity { get; init; } = string.Empty;
        public string EvidenceId { get; init; } = string.Empty;
    }

    private sealed class DisconnectedFieldPayloadFixture
    {
        public string ConfigurationId = string.Empty;
        public string Device = string.Empty;
        public string Maturity = string.Empty;
        public string EvidenceId = string.Empty;
    }

    private class CallGraphFixture
    {
        private readonly int offset = 1;

        internal virtual int VirtualTarget() => 2;

        internal int Combine(Func<int> first, Func<int> second) =>
            first() + second() + offset;
    }

    private sealed class DerivedCallGraphFixture : CallGraphFixture
    {
        internal override int VirtualTarget() => base.VirtualTarget();
    }

    private static SameModuleCallGraph BuildSameModuleCallGraph(
        MethodInfo entryPoint)
    {
        Module module = entryPoint.Module;
        Queue<MethodBase> pending = new();
        HashSet<MethodBase> methods = [];
        List<MethodReference> references = [];
        List<IlInstruction> instructions = [];
        pending.Enqueue(entryPoint);

        while (pending.TryDequeue(out MethodBase? method))
        {
            if (!methods.Add(method))
            {
                continue;
            }

            ReadMethodReferences(
                method, references, instructions, pending, module);
            MethodInfo? moveNext = method
                .GetCustomAttribute<AsyncStateMachineAttribute>()?
                .StateMachineType.GetMethod(
                    "MoveNext",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
            if (moveNext is not null && methods.Add(moveNext))
            {
                ReadMethodReferences(
                    moveNext, references, instructions, pending, module);
            }
        }

        return new(methods, references, instructions);
    }

    private static void ReadMethodReferences(
        MethodBase bodyOwner,
        List<MethodReference> references,
        List<IlInstruction> instructions,
        Queue<MethodBase> pending,
        Module module)
    {
        byte[]? il = bodyOwner.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            return;
        }

        int offset = 0;
        while (offset < il.Length)
        {
            int instructionOffset = offset;
            OpCode opcode = il[offset++] == 0xfe
                ? MultiByteOpCodes[il[offset++]]
                : SingleByteOpCodes[il[offset - 1]];
            instructions.Add(new(bodyOwner, instructionOffset, opcode));
            int operandSize = OperandSize(opcode.OperandType, il, offset);
            if (opcode.OperandType == OperandType.InlineMethod &&
                (opcode == OpCodes.Call ||
                 opcode == OpCodes.Callvirt ||
                 opcode == OpCodes.Newobj ||
                 opcode == OpCodes.Ldftn ||
                 opcode == OpCodes.Ldvirtftn))
            {
                MethodBase target;
                try
                {
                    target = bodyOwner.Module.ResolveMethod(
                        BitConverter.ToInt32(il, offset),
                        bodyOwner.DeclaringType?.IsGenericType is true
                            ? bodyOwner.DeclaringType.GetGenericArguments()
                            : null,
                        bodyOwner.IsGenericMethod
                            ? bodyOwner.GetGenericArguments()
                            : null) ?? throw new AssertFailedException(
                                $"A method operand at {instructionOffset} was null.");
                }
                catch (Exception exception) when (exception is ArgumentException or
                                                  BadImageFormatException)
                {
                    throw new AssertFailedException(
                        $"Unable to resolve a method operand in {bodyOwner.Name} " +
                        $"at IL offset {instructionOffset}.", exception);
                }

                references.Add(new(bodyOwner, instructionOffset, opcode, target));
                if (target.Module == module)
                {
                    pending.Enqueue(target);
                }
            }
            offset += operandSize;
        }
    }

    private static bool ContainsHiddenInvocationRisk(SameModuleCallGraph graph) =>
        HiddenInvocationRisks(graph).Any();

    private static IEnumerable<string> HiddenInvocationRisks(
        SameModuleCallGraph graph)
    {
        foreach (IlInstruction instruction in graph.Instructions.Where(
                     static instruction => instruction.OpCode == OpCodes.Calli))
        {
            yield return $"{instruction.Source.DeclaringType?.FullName}." +
                $"{instruction.Source.Name}+IL_{instruction.Offset:x4}:calli";
        }
        foreach (MethodReference reference in graph.References.Where(
                     static reference =>
                         IsReflectionInvocationOrCreationApi(reference.Target) ||
                         IsDynamicInvocationApi(reference.Target) ||
                         IsDisallowedDelegateInvocation(reference)))
        {
            yield return $"{reference.Source.DeclaringType?.FullName}." +
                $"{reference.Source.Name}+IL_{reference.Offset:x4} -> " +
                $"{reference.Target.DeclaringType?.FullName}.{reference.Target.Name}";
        }
    }

    private static bool IsReflectionInvocationOrCreationApi(MethodBase target)
    {
        Type? owner = target.DeclaringType;
        return owner?.Namespace?.StartsWith(
                "System.Reflection", StringComparison.Ordinal) is true ||
            owner == typeof(Activator) ||
            owner == typeof(Type) && target.GetParameters().Any(static parameter =>
                parameter.ParameterType == typeof(BindingFlags)) ||
            owner == typeof(Delegate) ||
            owner == typeof(RuntimeMethodHandle) ||
            owner == typeof(System.Runtime.InteropServices.Marshal) &&
                ReturnsDelegate(target) ||
            owner?.Namespace?.StartsWith(
                "System.Linq.Expressions", StringComparison.Ordinal) is true &&
                ReturnsDelegate(target);
    }

    private static bool IsDynamicInvocationApi(MethodBase target)
    {
        Type? owner = target.DeclaringType;
        string? ownerNamespace = owner?.Namespace;
        return ownerNamespace?.StartsWith(
                "Microsoft.CSharp.RuntimeBinder", StringComparison.Ordinal) is true ||
            ownerNamespace?.StartsWith(
                "System.Runtime.CompilerServices", StringComparison.Ordinal) is true &&
            (owner?.Name.StartsWith("CallSite", StringComparison.Ordinal) is true ||
             owner?.DeclaringType?.Name.StartsWith(
                 "CallSite", StringComparison.Ordinal) is true);
    }

    private static bool IsDisallowedDelegateInvocation(MethodReference reference)
    {
        MethodBase target = reference.Target;
        Type? owner = target.DeclaringType;
        if (target.Name != "Invoke" || owner is null ||
            !typeof(MulticastDelegate).IsAssignableFrom(owner))
        {
            return false;
        }

        return !IsAllowlistedDelegateInvocation(reference);
    }

    private static bool IsAllowlistedDelegateInvocation(
        MethodReference reference)
    {
        Type? delegateType = reference.Target.DeclaringType;
        if (delegateType is null || reference.Target.Name != "Invoke")
        {
            return false;
        }

        return IsExactContainingMethod(reference.Source,
                   ExactProductionMethod(
                       typeof(OpenVinoOptimizationService), "OptimizeCoreAsync")) &&
               (delegateType == ServiceDelegateType("hasSufficientSpace") ||
                delegateType == ServiceDelegateType("operationIdFactory")) ||
            IsExactContainingMethod(reference.Source,
                ExactProductionMethod(typeof(OpenVinoPackageSnapshotter), "Capture")) &&
                delegateType == SnapshotterDelegateType("observer") ||
            IsExactContainingMethod(reference.Source,
                ExactProductionMethod(
                    typeof(OpenVinoPackageSnapshotter), "DiscoverTopology")) &&
                (delegateType == SnapshotterDelegateType("enumerateEntries") ||
                 delegateType == SnapshotterDelegateType("observer")) ||
            IsExactContainingMethod(reference.Source,
                ExactProductionMethod(
                    typeof(OpenVinoPackageSnapshotter),
                    "ValidateDiscoveredPolicy")) &&
                delegateType == typeof(Func<string, bool>) ||
            IsExactContainingMethod(reference.Source,
                ExactProductionMethod(
                    typeof(OpenVinoPackageSnapshotter),
                    "ValidateLengthAndMagic")) &&
                delegateType == typeof(Func<string, bool>) ||
            IsExactContainingMethod(reference.Source,
                ExactProductionMethod(
                    typeof(OpenVinoPackageSnapshot), "ValidateStillCurrent")) &&
                delegateType == SnapshotDelegateType("validator") ||
            IsExactContainingMethod(reference.Source,
                ExactProductionMethod(typeof(OpenVinoPackageSnapshot), "Notify")) &&
                delegateType == SnapshotDelegateType("observer");
    }

    private static Type ServiceDelegateType(string fieldName) =>
        typeof(OpenVinoOptimizationService).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType;

    private static Type SnapshotterDelegateType(string fieldName) =>
        typeof(OpenVinoPackageSnapshotter).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType;

    private static Type SnapshotDelegateType(string fieldName) =>
        typeof(OpenVinoPackageSnapshot).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType;

    private static MethodInfo ExactProductionMethod(Type type, string name) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == name);

    private static bool IsExactContainingMethod(
        MethodBase source,
        MethodInfo containingMethod)
    {
        if (source == containingMethod)
        {
            return true;
        }
        if (source.Name != "MoveNext" ||
            source.DeclaringType?.DeclaringType != containingMethod.DeclaringType)
        {
            return false;
        }
        return containingMethod.GetCustomAttribute<AsyncStateMachineAttribute>()?
            .StateMachineType == source.DeclaringType;
    }

    private static void AssertAllowlistedDelegateInvocationBoundaries(
        SameModuleCallGraph graph)
    {
        MethodReference[] allowed = graph.References
            .Where(IsAllowlistedDelegateInvocation)
            .ToArray();
        Assert.IsNotEmpty(allowed,
            "The exact delegate allowlist must cover the production service seams.");

        foreach (MethodReference invocation in allowed)
        {
            MethodReference[] directReferences = graph.References
                .Where(reference => reference.Source == invocation.Source)
                .ToArray();
            Assert.IsFalse(graph.Instructions.Any(instruction =>
                    instruction.Source == invocation.Source &&
                    instruction.OpCode == OpCodes.Calli),
                $"{invocation.Source.DeclaringType?.FullName}." +
                $"{invocation.Source.Name} contains calli.");
            Assert.IsFalse(directReferences.Any(static reference =>
                    reference.Target.DeclaringType ==
                        typeof(OpenVinoOptimizationLegacyRegistryV1) &&
                    reference.Target.Name ==
                        nameof(OpenVinoOptimizationLegacyRegistryV1.GetRequired) ||
                    IsReflectionInvocationOrCreationApi(reference.Target) ||
                    IsDynamicInvocationApi(reference.Target)),
                $"{invocation.Source.DeclaringType?.FullName}." +
                $"{invocation.Source.Name} hides an authority lookup.");

            Type delegateType = invocation.Target.DeclaringType!;
            MethodInfo invoke = delegateType.GetMethod("Invoke")!;
            Type[] boundaryTypes = invoke.GetParameters()
                .Select(static parameter => parameter.ParameterType)
                .Append(invoke.ReturnType)
                .SelectMany(ContainedTypes)
                .ToArray();
            Assert.IsFalse(boundaryTypes.Any(IsExecutionAuthorityType),
                $"Allowlisted delegate {delegateType.FullName} carries a plan, " +
                "payload, candidate, or configuration authority type.");
        }
    }

    private static bool IsExecutionAuthorityType(Type type)
    {
        Type normalized = NormalizeType(type);
        if (normalized == typeof(OpenVinoOptimizationCandidate))
        {
            return true;
        }
        string? typeNamespace = normalized.Namespace;
        return typeNamespace?.StartsWith(
                "GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization",
                StringComparison.Ordinal) is true &&
            (normalized.Name.Contains("Plan", StringComparison.Ordinal) ||
             normalized.Name.Contains("Payload", StringComparison.Ordinal) ||
             normalized.Name.Contains("Candidate", StringComparison.Ordinal) ||
             normalized.Name.Contains("Configuration", StringComparison.Ordinal));
    }

    private static bool ReturnsDelegate(MethodBase method) =>
        method is MethodInfo info &&
        typeof(Delegate).IsAssignableFrom(info.ReturnType);

    private static bool IsO1OwnedConfigurationLookup(MethodBase target)
    {
        Type? owner = target.DeclaringType;
        if (owner?.Module != typeof(OpenVinoOptimizationService).Module ||
            !target.IsStatic || target is not MethodInfo method ||
            method.ReturnType != typeof(bool) &&
            method.ReturnType != typeof(OpenVinoOptimizationCandidate))
        {
            return false;
        }

        bool ownsCandidateCatalog = owner.GetProperties(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(static property => TypeContains(
                property.PropertyType, typeof(OpenVinoOptimizationCandidate)));
        bool acceptsLookupKey = method.GetParameters().Any(static parameter =>
            parameter.ParameterType == typeof(OpenVinoOptimizationObjective) ||
            parameter.ParameterType == typeof(OpenVinoOptimizationCandidate));
        return ownsCandidateCatalog && acceptsLookupKey;
    }

    private static bool TypeContains(Type container, Type expected)
    {
        Type normalized = NormalizeType(container);
        return normalized == expected || normalized.IsGenericType &&
            normalized.GetGenericArguments().Any(argument =>
                TypeContains(argument, expected));
    }

    private static void AssertAuthoritativePayloadMemberTypes(
        SameModuleCallGraph graph)
    {
        Assert.AreEqual(
            typeof(ContractExecutionPayload),
            typeof(OptimizationExecutionPlan).GetProperty(
                nameof(OptimizationExecutionPlan.ExecutionPayload))!.PropertyType);
        Assert.AreEqual(
            typeof(ContractOpenVinoExecutionPayload),
            typeof(ContractExecutionPayload).GetProperty(
                nameof(ContractExecutionPayload.OpenVino))!.PropertyType);
        Assert.AreEqual(
            typeof(ContractOpenVinoExecutionPayload),
            typeof(OpenVinoOptimizationCandidate).GetProperty(
                nameof(OpenVinoOptimizationCandidate.ExecutionPayload))!.PropertyType);

        Type[] memberTypes = ReachableSameModuleTypes(graph)
            .SelectMany(static type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(static property => property.Name == "ExecutionPayload")
                .Select(static property => property.PropertyType)
                .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .Where(static field => field.Name ==
                        "<ExecutionPayload>k__BackingField")
                    .Select(static field => field.FieldType)))
            .Distinct()
            .ToArray();
        Assert.IsNotEmpty(memberTypes,
            "The accepting graph must expose its route-native C1 payload binding.");
        Assert.IsTrue(memberTypes.All(static type =>
                type == typeof(ContractExecutionPayload) ||
                type == typeof(ContractOpenVinoExecutionPayload)),
            "Every authoritative execution-payload member must use a frozen C1 type.");

        MethodInfo configurationMatch = ConfigurationIdentityMethod();
        Assert.AreEqual(
            typeof(ContractExecutionPayload),
            configurationMatch.GetParameters()[1].ParameterType,
            "The configuration identity boundary must receive the C1 union payload.");
    }

    private static void AssertNoDuplicatePayloadAuthorityInProductionModule()
    {
        HashSet<Type> allowlist =
        [
            typeof(OpenVinoOptimizationCandidate),
            typeof(OpenVinoOptimizationProvenance),
            typeof(OpenVinoRuntimeOptimizationProfile)
        ];
        Type[] productionTypes = ProductionModuleTypes();
        Type[] substantialOverlap = FindPayloadAuthorityOverlaps(productionTypes);
        Type[] disallowed = FindDisallowedPayloadAuthorities(
            productionTypes, allowlist);

        Assert.IsTrue(substantialOverlap.Contains(typeof(OpenVinoOptimizationCandidate)),
            "The semantic overlap guard must cover the route-native candidate.");
        Assert.IsEmpty(disallowed,
            "An O1 production type substantially overlaps the C1 payload outside " +
            "the explicit candidate/provenance/profile evidence allowlist: " +
            string.Join(", ", disallowed.Select(static type => type.FullName)));

        PayloadMember[] payloadMembers = productionTypes
            .SelectMany(PayloadMembers)
            .ToArray();
        Assert.IsNotEmpty(payloadMembers,
            "The production module must expose its route-native C1 payload binding.");
        foreach (PayloadMember payload in payloadMembers)
        {
            Assert.IsTrue(allowlist.Contains(payload.Owner),
                $"{payload.Owner.FullName}.{payload.Name} is not on the explicit " +
                "candidate/provenance/profile evidence allowlist.");
            Assert.IsTrue(
                payload.MemberType == typeof(ContractExecutionPayload) ||
                payload.MemberType == typeof(ContractOpenVinoExecutionPayload),
                $"{payload.Owner.FullName}.{payload.Name} must use a frozen C1 " +
                "execution-payload type, not {payload.MemberType.FullName}.");
        }
    }

    private static Type[] ProductionModuleTypes()
    {
        Module productionModule = typeof(OpenVinoOptimizationService).Module;
        string[] productionNamespaceRoots =
        [
            "GraniteEdgeAI.Features.OpenVinoRoute",
            "GraniteEdgeAI.Features.Prompting"
        ];
        return productionModule.Assembly.GetTypes()
            .Where(type => type.Module == productionModule &&
                type.Namespace is not null &&
                productionNamespaceRoots.Any(root =>
                    type.Namespace.Equals(root, StringComparison.Ordinal) ||
                    type.Namespace.StartsWith(root + ".", StringComparison.Ordinal)) &&
                type.GetCustomAttribute<CompilerGeneratedAttribute>() is null &&
                !type.Name.StartsWith('<'))
            .ToArray();
    }

    private static Type[] FindDisallowedPayloadAuthorities(
        IEnumerable<Type> types,
        HashSet<Type> allowlist) =>
        FindPayloadAuthorityOverlaps(types)
            .Where(type => !allowlist.Contains(type))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static Type[] FindPayloadAuthorityOverlaps(IEnumerable<Type> types)
    {
        HashSet<string> authoritativeFields = typeof(ContractOpenVinoExecutionPayload)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        return types
            .Where(type => PayloadFieldNames(type)
                .Count(authoritativeFields.Contains) >= 4)
            .Distinct()
            .ToArray();
    }

    private static HashSet<string> PayloadFieldNames(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(static property => property.Name)
            .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(static field => NormalizePayloadMemberName(field.Name)))
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<PayloadMember> PayloadMembers(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Instance | BindingFlags.Public |
                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (property.Name == "ExecutionPayload")
            {
                yield return new(type, property.Name, property.PropertyType);
            }
        }
        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Instance | BindingFlags.Public |
                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (NormalizePayloadMemberName(field.Name) == "ExecutionPayload")
            {
                yield return new(type, field.Name, field.FieldType);
            }
        }
    }

    private static string NormalizePayloadMemberName(string name)
    {
        const string backingSuffix = ">k__BackingField";
        return name.StartsWith('<') && name.EndsWith(
                backingSuffix, StringComparison.Ordinal)
            ? name[1..^backingSuffix.Length]
            : name;
    }

    private static void AssertConfigurationDigestDelegatesToPlanAuthority()
    {
        MethodInfo configurationMatch = ConfigurationIdentityMethod();
        SameModuleCallGraph graph = BuildSameModuleCallGraph(configurationMatch);
        MethodInfo[] authorityCalls = graph.References
            .Select(static reference => reference.Target)
            .OfType<MethodInfo>()
            .Where(static method =>
                method.DeclaringType == typeof(OptimizationExecutionPlan) &&
                method.Name == nameof(
                    OptimizationExecutionPlan.MatchesExecutionPayload))
            .ToArray();

        Assert.HasCount(1, authorityCalls,
            "Configuration identity must delegate exactly once to the bound C1 plan.");
        Assert.IsTrue(authorityCalls[0].IsPublic && !authorityCalls[0].IsStatic);
        Assert.AreEqual(typeof(ContractExecutionPayload),
            authorityCalls[0].GetParameters()[0].ParameterType);
        Assert.IsFalse(ContainsHiddenInvocationRisk(graph),
            "Configuration identity must not hide an alternate issuer/canonicalizer call.");
        Assert.IsFalse(ContainsLocalConfigurationCanonicalization(graph),
            "Configuration identity must not perform local SHA/string/JSON canonicalization.");
    }

    private static bool ContainsLocalConfigurationCanonicalization(
        SameModuleCallGraph graph)
    {
        IEnumerable<Type> methodTypes = graph.Methods.SelectMany(MethodTypes);
        IEnumerable<Type> referenceTypes = graph.References.SelectMany(
            static reference => MethodTypes(reference.Target));
        if (methodTypes.Concat(referenceTypes)
            .SelectMany(ContainedTypes)
            .Any(IsCanonicalizationType))
        {
            return true;
        }

        return graph.References.Any(static reference =>
            reference.Target.DeclaringType == typeof(BinaryWriter) ||
            reference.Target.DeclaringType == typeof(MemoryStream) ||
            reference.Target.DeclaringType == typeof(Convert) &&
            reference.Target.Name is (nameof(Convert.ToHexString) or
                nameof(Convert.FromHexString) or
                nameof(Convert.ToBase64String) or
                nameof(Convert.FromBase64String)) ||
            reference.Target.DeclaringType == typeof(string) &&
            reference.Target.Name != nameof(string.Equals));
    }

    private static IEnumerable<Type> MethodTypes(MethodBase method)
    {
        if (method.DeclaringType is not null)
        {
            yield return method.DeclaringType;
        }
        if (method is MethodInfo info)
        {
            yield return info.ReturnType;
        }
        foreach (ParameterInfo parameter in method.GetParameters())
        {
            yield return parameter.ParameterType;
        }
        foreach (LocalVariableInfo local in
                 method.GetMethodBody()?.LocalVariables ?? [])
        {
            yield return local.LocalType;
        }
    }

    private static IEnumerable<Type> ContainedTypes(Type type)
    {
        Type normalized = NormalizeType(type);
        yield return normalized;
        if (!normalized.IsGenericType)
        {
            yield break;
        }
        foreach (Type argument in normalized.GetGenericArguments())
        {
            foreach (Type contained in ContainedTypes(argument))
            {
                yield return contained;
            }
        }
    }

    private static bool IsCanonicalizationType(Type type) =>
        type.Namespace?.StartsWith(
            "System.Security.Cryptography", StringComparison.Ordinal) is true ||
        type == typeof(System.Text.StringBuilder) ||
        typeof(System.Text.Encoding).IsAssignableFrom(type) ||
        type.Namespace?.StartsWith(
            "System.Text.Json", StringComparison.Ordinal) is true;

    private static MethodInfo ConfigurationIdentityMethod() =>
        typeof(OpenVinoOptimizationPlanAdapter).GetMethod(
            "ConfigurationIdentityMatches",
            BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new AssertFailedException(
            "OpenVinoOptimizationPlanAdapter.ConfigurationIdentityMatches is absent.");

    private static HashSet<Type> ReachableSameModuleTypes(SameModuleCallGraph graph)
    {
        Module module = typeof(OpenVinoOptimizationService).Module;
        HashSet<Type> types = [];
        Queue<Type> pending = new();
        foreach (MethodBase method in graph.Methods)
        {
            AddType(method.DeclaringType, pending);
            if (method is MethodInfo info)
            {
                AddType(info.ReturnType, pending);
            }
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AddType(parameter.ParameterType, pending);
            }
            foreach (LocalVariableInfo local in
                     method.GetMethodBody()?.LocalVariables ?? [])
            {
                AddType(local.LocalType, pending);
            }
        }

        while (pending.TryDequeue(out Type? type))
        {
            Type normalized = NormalizeType(type);
            if (normalized.Module != module || !types.Add(normalized))
            {
                continue;
            }
            foreach (PropertyInfo property in normalized.GetProperties(
                         BindingFlags.Instance | BindingFlags.Static |
                         BindingFlags.Public | BindingFlags.NonPublic))
            {
                AddType(property.PropertyType, pending);
            }
            foreach (FieldInfo field in normalized.GetFields(
                         BindingFlags.Instance | BindingFlags.Static |
                         BindingFlags.Public | BindingFlags.NonPublic))
            {
                AddType(field.FieldType, pending);
            }
        }
        return types;
    }

    private static void AddType(Type? type, Queue<Type> pending)
    {
        if (type is null)
        {
            return;
        }
        Type normalized = NormalizeType(type);
        pending.Enqueue(normalized);
        if (normalized.IsGenericType)
        {
            foreach (Type argument in normalized.GetGenericArguments())
            {
                AddType(argument, pending);
            }
        }
    }

    private static Type NormalizeType(Type type)
    {
        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }
        return type;
    }

    private sealed record SameModuleCallGraph(
        HashSet<MethodBase> Methods,
        List<MethodReference> References,
        List<IlInstruction> Instructions);

    private sealed record IlInstruction(
        MethodBase Source,
        int Offset,
        OpCode OpCode);

    private sealed record PayloadMember(
        Type Owner,
        string Name,
        Type MemberType);

    private sealed record MethodReference(
        MethodBase Source,
        int Offset,
        OpCode OpCode,
        MethodBase Target);

    private static int OperandSize(OperandType operandType, byte[] il, int offset) =>
        operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or
                OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or
                OperandType.InlineI or OperandType.InlineMethod or
                OperandType.InlineSig or OperandType.InlineString or
                OperandType.InlineTok or OperandType.InlineType or
                OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                4 + (BitConverter.ToInt32(il, offset) * 4),
            _ => throw new AssertFailedException(
                $"Unknown IL operand type {operandType}.")
        };

    private static OpCode[] BuildOpCodes(bool multiByte)
    {
        OpCode[] result = new OpCode[256];
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opcode)
            {
                continue;
            }
            ushort value = unchecked((ushort)opcode.Value);
            if (multiByte == value > byte.MaxValue)
            {
                result[value & byte.MaxValue] = opcode;
            }
        }
        return result;
    }

    private static readonly OpCode[] SingleByteOpCodes = BuildOpCodes(
        multiByte: false);
    private static readonly OpCode[] MultiByteOpCodes = BuildOpCodes(
        multiByte: true);

    private static OpenVinoRouteService CreateRoute(string stage)
    {
        string manifestDigest = Digest(Path.Combine(stage, "worker-manifest.json"));
        OpenVinoBuildEvidence evidence = new(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            "2026.3.0.0-3277-bd8d6542e3c",
            "2026.3.0.0-703-183c6f25cda",
            manifestDigest);
        Dictionary<string, OpenVinoWorkerBinaryMachine> machines =
            new(StringComparer.Ordinal)
            {
                ["OpenVinoOfficial.Worker.exe"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_genai.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_cpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_gpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_ir_frontend.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_tokenizers.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbb12.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbbbind_2_5.dll"] = OpenVinoWorkerBinaryMachine.Amd64
            };
        OpenVinoWorkerInstallation installation = new(
            stage,
            "OpenVinoOfficial.Worker.exe",
            OpenVinoProtocol.OfficialProtocolId,
            evidence,
            machines);
        return new OpenVinoRouteService(
            new OpenVinoWorkerClient(
                OpenVinoWorkerClientOptions.CreateDefault(installation)),
            evidence);
    }

    private static string RequireOfficialStage()
    {
        string? value = Environment.GetEnvironmentVariable(
            "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
        {
            Assert.Inconclusive("OPENVINO_OFFICIAL_WORKER_STAGE_A is required.");
        }
        return Path.GetFullPath(value!);
    }

    private static OpenVinoOptimizationCapabilityEvidence NativeCapabilityEvidence(
        OpenVinoBuildEvidence builds) =>
        new(
            builds,
            new OpenVinoOptimizationToolVersions(
                OpenVino: "2026.3.0",
                OpenVinoGenAi: "2026.3.0.0",
                Nncf: "3.3.0",
                Optimum: "2.3.0",
                OptimumIntel: "2.1.0",
                Transformers: "5.5.4"),
            [
                new OpenVinoOptimizationCapabilityAdmission(
                    "OV-STD-CPU-INT8-U8-01",
                    Device: "CPU",
                    OpenVinoWeightPrecision.EightBit,
                    new OpenVinoRuntimeOptimization(
                        OpenVinoKvCachePrecision.U8,
                        RouteCompiledCachePolicy.Disabled),
                    OpenVinoCapabilityPerformanceHint.Latency,
                    Streams: 1,
                    MinimumContextTokens: 4_096,
                    MaximumContextTokens: 4_096,
                    OpenVinoCapabilityMaturity.Released),
                new OpenVinoOptimizationCapabilityAdmission(
                    "OV-STD-CPU-INT4-U8-01",
                    Device: "CPU",
                    OpenVinoWeightPrecision.FourBit,
                    new OpenVinoRuntimeOptimization(
                        OpenVinoKvCachePrecision.U8,
                        RouteCompiledCachePolicy.Disabled),
                    OpenVinoCapabilityPerformanceHint.Latency,
                    Streams: 1,
                    MinimumContextTokens: 4_096,
                    MaximumContextTokens: 4_096,
                    OpenVinoCapabilityMaturity.Released)
            ]);

    private static OptimizationExecutionPlan IssuePlan(
        OpenVinoAdmittedConfiguration admission,
        OptimizationCapabilitySnapshot snapshot,
        OpenVinoStaticPackageEvidence source,
        OpenVinoOptimizationCapabilityEvidence currentEvidence)
    {
        OpenVinoRouteConfiguration configuration = OpenVinoRouteConfiguration.Create(
            admission.Weights,
            admission.KvCache,
            admission.Device,
            admission.PerformanceHint,
            admission.CompiledCache,
            admission.Streams);
        ulong sourceLength = checked((ulong)source.ModelLengthBytes);
        OptimizationCandidate candidate = OptimizationCandidate.Create(
            configuration,
            OptimizationCandidateMetrics.Create(
                EvidenceGrade.Measured,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                OptimizationAssessment.Good,
                contextTokens: 4_096,
                predictedPeakBytes: 2UL * 1024 * 1024 * 1024,
                safeBudgetBytes: 8UL * 1024 * 1024 * 1024,
                headroomBytes: 6UL * 1024 * 1024 * 1024,
                workingDiskBytes: sourceLength,
                outputDiskBytes: Math.Max(1UL, sourceLength / 2),
                requiresPersistentChange: true),
            admission.EvidenceId,
            isExperimental: false);
        OptimizationPreferenceSelection preference =
            OptimizationPreferenceSelection.Manual(50);
        ConstructorInfo selectionConstructor =
            typeof(OptimizationSelection).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [
                    typeof(OptimizationCandidate),
                    typeof(OptimizationPreferenceSelection),
                    typeof(bool)
                ],
                modifiers: null)
            ?? throw new InvalidOperationException(
                "The frozen V2 selection constructor is unavailable.");
        OptimizationSelection selection = (OptimizationSelection)
            selectionConstructor.Invoke([candidate, preference, false]);
        OpenVinoOptimizationToolVersions versions = currentEvidence.Versions;
        bool int4 = admission.Weights == OpenVinoWeightFormat.Int4;
        ContractExecutionPayload executionPayload =
            ContractExecutionPayload.ForOpenVino(
                ContractOpenVinoExecutionPayload.Create(
                    int4
                        ? "openvino.standard.cpu.int4.u8.v1"
                        : "openvino.standard.cpu.int8.u8.v1",
                    "CPU",
                    "Standard candidate",
                    admission.EvidenceId,
                    GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                        .Optimization.Execution.OpenVinoWeightPrecision.Fp16,
                    int4
                        ? GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                            .Optimization.Execution.OpenVinoWeightPrecision.FourBit
                        : GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                            .Optimization.Execution.OpenVinoWeightPrecision.EightBit,
                    GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                        .Optimization.Execution.OpenVinoKvCachePrecision.U8,
                    compiledCacheEnabled: false,
                    compiledCacheIsDisposable: true,
                    compiledCacheIsModelArtifact: false,
                    createsCompletePackage: true,
                    ContractOpenVinoBuildIdentity.Create(
                        currentEvidence.Builds.RuntimeBuild,
                        currentEvidence.Builds.GenAiBuild,
                        currentEvidence.Builds.TokenizersBuild,
                        currentEvidence.Builds.WorkerManifestDigest),
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["nncf"] = versions.Nncf,
                        ["openvino"] = versions.OpenVino,
                        ["openvino-genai"] = versions.OpenVinoGenAi,
                        ["optimum"] = versions.Optimum,
                        ["optimum-intel"] = versions.OptimumIntel,
                        ["transformers"] = versions.Transformers
                    },
                    turboQuantBuild: null));
        return OptimizationPlanIssuer.IssueLegacyV2(
            selection,
            executionPayload,
            snapshot,
            OptimizationWorkload.Create(
                "chat",
                minimumContextTokens: 512,
                OptimizationAssessment.Poor,
                [ContextTokenCount.FromTokens(4_096)]),
            OptimizationJourneyBinding.Create(
                "mi-native-e2e-1",
                "mi-handoff-native-e2e-1",
                source.ModelSha256,
                sourceLength,
                "hw-native-e2e-1",
                new string('2', 64)),
            modelLayerCount: 1,
            DateTimeOffset.UnixEpoch);
    }

    private static string DigestCapability(OpenVinoCapabilityPayload payload)
    {
        using MemoryStream canonical = new();
        using (BinaryWriter writer = new(canonical, System.Text.Encoding.UTF8, true))
        {
            writer.Write(payload.RuntimeVersion);
            foreach (OpenVinoAdmittedConfiguration admission in payload.Admitted)
            {
                writer.Write(admission.EvidenceId);
                writer.Write((int)admission.Device);
                writer.Write((int)admission.Weights);
                writer.Write((int)admission.KvCache);
                writer.Write((int)admission.PerformanceHint);
                writer.Write((int)admission.CompiledCache);
                writer.Write(admission.Streams);
                writer.Write(admission.MinimumContextTokens);
                writer.Write(admission.MaximumContextTokens);
                writer.Write((int)admission.Level);
                writer.Write(admission.RequiresEvidence);
            }
        }
        return Convert.ToHexString(SHA256.HashData(canonical.ToArray()))
            .ToLowerInvariant();
    }

    private static string RequireStage()
    {
        string? stage = Environment.GetEnvironmentVariable("GRANITE_OPENVINO_CONVERTER_STAGE");
        if (string.IsNullOrWhiteSpace(stage) || !Directory.Exists(stage))
        {
            Assert.Inconclusive("GRANITE_OPENVINO_CONVERTER_STAGE is required.");
        }
        return Path.GetFullPath(stage!);
    }

    private static string Digest(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void CopySourceFixture(string destination)
    {
        string repository = FindRepositoryRoot();
        string source = Path.Combine(
            repository, "tests", "TestFixtures", "OpenVINO", "Converter",
            "TinyGraniteV1", "source");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class UnusedWorkerClient : IOpenVinoWorkerClient
    {
        public Task<IOpenVinoEvent> InspectAsync(
            StartInspectionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("Native inspection is not part of optimizer export.");

        public Task<OpenVinoConversation> StartSessionAsync(
            StartSessionCommand command,
            CancellationToken cancellationToken) =>
            throw new AssertFailedException("Prompting is not part of optimizer export.");
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class FixedCurrentStateProvider(
        OpenVinoOptimizationCurrentState currentState) :
        IOpenVinoOptimizationCurrentStateProvider
    {
        public ValueTask<OpenVinoOptimizationCurrentState> GetCurrentStateAsync(
            OpenVinoOptimizationCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(currentState);
        }
    }
}
