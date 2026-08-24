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
        Assert.IsFalse(graph.References.Any(static reference =>
                IsHiddenInvocationApi(reference.Target)),
            "The accepting graph must not hide a call behind reflection or dynamic invocation.");

        AssertAuthoritativePayloadMemberTypes(graph);
        AssertNoDuplicatePayloadAuthority(graph);
        AssertConfigurationDigestDelegatesToC1Issuer();
    }

    private static int CallGraphOperandFixture()
    {
        CallGraphFixture fixture = new DerivedCallGraphFixture();
        Func<int> staticTarget = CallGraphStaticTarget;
        Func<int> virtualTarget = fixture.VirtualTarget;
        return fixture.Combine(staticTarget, virtualTarget);
    }

    private static int CallGraphStaticTarget() => 1;

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
        pending.Enqueue(entryPoint);

        while (pending.TryDequeue(out MethodBase? method))
        {
            if (!methods.Add(method))
            {
                continue;
            }

            ReadMethodReferences(method, references, pending, module);
            MethodInfo? moveNext = method
                .GetCustomAttribute<AsyncStateMachineAttribute>()?
                .StateMachineType.GetMethod(
                    "MoveNext",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
            if (moveNext is not null && methods.Add(moveNext))
            {
                ReadMethodReferences(moveNext, references, pending, module);
            }
        }

        return new(methods, references);
    }

    private static void ReadMethodReferences(
        MethodBase bodyOwner,
        List<MethodReference> references,
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

    private static bool IsHiddenInvocationApi(MethodBase target)
    {
        Type? owner = target.DeclaringType;
        string name = target.Name;
        return owner == typeof(MethodBase) && name == nameof(MethodBase.Invoke) ||
            owner == typeof(ConstructorInfo) && name == nameof(ConstructorInfo.Invoke) ||
            owner == typeof(Type) && name == nameof(Type.InvokeMember) ||
            owner == typeof(Activator) && name == nameof(Activator.CreateInstance) ||
            owner == typeof(Assembly) && name == nameof(Assembly.CreateInstance) ||
            owner == typeof(Delegate) &&
                (name == nameof(Delegate.DynamicInvoke) ||
                 name == nameof(Delegate.CreateDelegate)) ||
            owner == typeof(MethodInfo) && name == nameof(MethodInfo.CreateDelegate) ||
            owner == typeof(PropertyInfo) &&
                (name == nameof(PropertyInfo.GetValue) ||
                 name == nameof(PropertyInfo.SetValue)) ||
            owner == typeof(FieldInfo) &&
                (name == nameof(FieldInfo.GetValue) ||
                 name == nameof(FieldInfo.SetValue)) ||
            owner == typeof(EventInfo) &&
                (name == nameof(EventInfo.AddEventHandler) ||
                 name == nameof(EventInfo.RemoveEventHandler)) ||
            owner == typeof(RuntimeMethodHandle) &&
                name == nameof(RuntimeMethodHandle.GetFunctionPointer) ||
            owner == typeof(DynamicMethod) &&
                (name == nameof(DynamicMethod.Invoke) ||
                 name == nameof(DynamicMethod.CreateDelegate)) ||
            owner == typeof(System.Runtime.InteropServices.Marshal) &&
                name == nameof(System.Runtime.InteropServices.Marshal
                    .GetDelegateForFunctionPointer) ||
            owner?.Namespace == "System.Linq.Expressions" && name == "Compile";
    }

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

    private static void AssertNoDuplicatePayloadAuthority(SameModuleCallGraph graph)
    {
        HashSet<string> authoritativeFields = typeof(ContractOpenVinoExecutionPayload)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<Type> allowlist =
        [
            typeof(OpenVinoOptimizationCandidate),
            typeof(OpenVinoOptimizationProvenance),
            typeof(OpenVinoRuntimeOptimizationProfile)
        ];
        Type[] substantialOverlap = ReachableSameModuleTypes(graph)
            .Where(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Select(static property => property.Name)
                .Count(authoritativeFields.Contains) >= 4)
            .ToArray();

        Assert.IsTrue(substantialOverlap.Contains(typeof(OpenVinoOptimizationCandidate)),
            "The semantic overlap guard must cover the route-native candidate.");
        Assert.IsTrue(substantialOverlap.Contains(typeof(OpenVinoOptimizationProvenance)),
            "The semantic overlap guard must cover persistent result evidence.");
        Assert.IsTrue(substantialOverlap.All(allowlist.Contains),
            "A reachable O1 type substantially overlaps the C1 payload outside the " +
            "explicit route-native candidate/result-evidence allowlist: " +
            string.Join(", ", substantialOverlap
                .Where(type => !allowlist.Contains(type))
                .Select(static type => type.FullName)));
        foreach (Type allowed in allowlist)
        {
            PropertyInfo? payload = allowed.GetProperty("ExecutionPayload");
            if (payload is not null)
            {
                Assert.AreEqual(typeof(ContractOpenVinoExecutionPayload),
                    payload.PropertyType, allowed.FullName);
            }
        }
    }

    private static void AssertConfigurationDigestDelegatesToC1Issuer()
    {
        MethodInfo configurationMatch = ConfigurationIdentityMethod();
        SameModuleCallGraph graph = BuildSameModuleCallGraph(configurationMatch);
        MethodInfo[] issuerCalls = graph.References
            .Select(static reference => reference.Target)
            .OfType<MethodInfo>()
            .Where(static method =>
                method.DeclaringType == typeof(OptimizationPlanIssuer) &&
                method.Name == nameof(OptimizationPlanIssuer.Issue))
            .ToArray();

        Assert.HasCount(1, issuerCalls,
            "Configuration identity must delegate exactly once to the public C1 issuer.");
        Assert.IsTrue(issuerCalls[0].IsPublic && issuerCalls[0].IsStatic);
        Assert.AreEqual(typeof(ContractExecutionPayload),
            issuerCalls[0].GetParameters()[1].ParameterType);
        Assert.IsFalse(graph.References.Any(static reference =>
                IsHiddenInvocationApi(reference.Target)),
            "Configuration identity must not hide an alternate issuer/canonicalizer call.");
        Assert.IsFalse(graph.References.Any(static reference =>
                IsLocalConfigurationCanonicalizationApi(reference.Target)),
            "Configuration identity must not perform local SHA/string/JSON canonicalization.");
    }

    private static bool IsLocalConfigurationCanonicalizationApi(MethodBase target)
    {
        Type? owner = target.DeclaringType;
        if (owner is null)
        {
            return false;
        }
        if (typeof(HashAlgorithm).IsAssignableFrom(owner) ||
            owner == typeof(System.Text.StringBuilder) ||
            typeof(System.Text.Encoding).IsAssignableFrom(owner) ||
            owner == typeof(BinaryWriter) ||
            owner == typeof(MemoryStream) ||
            owner.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) is true)
        {
            return true;
        }
        if (owner == typeof(Convert) && target.Name is
            nameof(Convert.ToHexString) or nameof(Convert.FromHexString) or
            nameof(Convert.ToBase64String) or nameof(Convert.FromBase64String))
        {
            return true;
        }
        return owner == typeof(string) && target.Name != nameof(string.Equals);
    }

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
        List<MethodReference> References);

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
        OptimizationSelection selection = OptimizationPreferenceResolver.Resolve(
            [candidate], OptimizationPreferenceSelection.Manual(50))!;
        OpenVinoOptimizationToolVersions versions = currentEvidence.Versions;
        ContractExecutionPayload executionPayload =
            ContractExecutionPayload.ForOpenVino(
                ContractOpenVinoExecutionPayload.Create(
                    "openvino.standard.cpu.int8.u8.v1",
                    "CPU",
                    "Standard candidate",
                    admission.EvidenceId,
                    GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                        .Optimization.Execution.OpenVinoWeightPrecision.Fp16,
                    GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
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
        return OptimizationPlanIssuer.Issue(
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
