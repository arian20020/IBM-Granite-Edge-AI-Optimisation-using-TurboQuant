using GraniteEdgeAI.GgufQuantization.Capabilities;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.GgufQuantization.Capabilities.Tests;

[TestClass]
public sealed class GgufRequantizationAuthorizationTests
{
    [TestMethod]
    public void UnquantizedAndSameFormatConversionsNeedNoAuthorization()
    {
        Assert.IsFalse(GgufRequantizationAuthorization.IsRequired(
            GgufQuantizationFormat.F16, GgufQuantizationFormat.Q4KM));
        Assert.IsFalse(GgufRequantizationAuthorization.IsRequired(
            GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q4KM));
    }

    [TestMethod]
    public void QuantizedReductionRequiresAuthorization()
    {
        Assert.IsTrue(GgufRequantizationAuthorization.IsRequired(
            GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM));
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufRequantizationAuthorization.Create(
                Guid.NewGuid(), Digest('a'), Digest('b'), 42,
                GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM,
                Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion,
                admittedTarget: Admitted(GgufQuantizationFormat.Q3KM),
                policy: null));
    }

    [TestMethod]
    public void ConsentForOneTargetCannotAuthorizeAnotherTarget()
    {
        string model = Digest('b');
        GgufRequantisationPolicy policy = Policy(
            model, 42, GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q2K);

        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufRequantizationAuthorization.Create(
                Guid.NewGuid(), Digest('a'), model, 42,
                GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM,
                Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion,
                Admitted(GgufQuantizationFormat.Q3KM), policy));
    }

    [TestMethod]
    public void EveryBoundFieldChangesTheAuthorizationDigest()
    {
        Guid plan = Guid.NewGuid();
        string baseline = Create(plan, Digest('a'), Digest('b'), 42,
            GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM,
            Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion);

        string[] changed =
        {
            Create(Guid.NewGuid(), Digest('a'), Digest('b'), 42, GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM, Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion),
            Create(plan, Digest('d'), Digest('b'), 42, GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM, Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion),
            Create(plan, Digest('a'), Digest('d'), 42, GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM, Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion),
            Create(plan, Digest('a'), Digest('b'), 43, GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM, Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion),
            Create(plan, Digest('a'), Digest('b'), 42, GgufQuantizationFormat.Q5KM, GgufQuantizationFormat.Q3KM, Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion),
            Create(plan, Digest('a'), Digest('b'), 42, GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q2K, Digest('c'), GgufQuantizationProtocol.RequantizationPolicyVersion),
            Create(plan, Digest('a'), Digest('b'), 42, GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM, Digest('d'), GgufQuantizationProtocol.RequantizationPolicyVersion),
        };

        Assert.IsTrue(changed.All(value => !string.Equals(value, baseline, StringComparison.Ordinal)));
        Assert.ThrowsExactly<ArgumentException>(() =>
            Create(plan, Digest('a'), Digest('b'), 42,
                GgufQuantizationFormat.Q4KM, GgufQuantizationFormat.Q3KM,
                Digest('c'), "gguf-requantisation-v2"));
    }

    private static string Create(
        Guid plan,
        string configuration,
        string model,
        ulong length,
        GgufQuantizationFormat source,
        GgufQuantizationFormat target,
        string manifest,
        string policy) =>
        GgufRequantizationAuthorization.Create(
            plan, configuration, model, length, source, target, manifest, policy,
            Admitted(target), Policy(model, length, source, target)).Sha256;

    private static GgufRequantisationPolicy Policy(
        string modelSha256,
        ulong modelLengthBytes,
        GgufQuantizationFormat source,
        GgufQuantizationFormat target)
    {
        OptimizationJourneyBinding journey = OptimizationJourneyBinding.Create(
            "model-run", "model-handoff", modelSha256, modelLengthBytes,
            "hardware-run", Digest('e'));
        GgufAdmittedConfiguration admitted = Admitted(target);
        GgufConversionSourceBinding sourceBinding = GgufConversionSourceBinding.Create(
            GgufQuantizerFormatMap.ToCanonicalPrecision(source), journey);
        GgufQuantiserIdentity quantiser = GgufQuantiserIdentity.Create(
            "quantizer-package", "quantizer-version", Digest('f'));

        return GgufRequantisationPolicy.Create(
            explicitlyAcknowledged: true,
            preserveOriginal: true,
            requireNewOutput: true,
            admitted,
            quantiser,
            sourceBinding);
    }

    private static GgufAdmittedConfiguration Admitted(GgufQuantizationFormat target) =>
        GgufAdmittedConfiguration.Create(
            "admitted-config", CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu, GgufQuantizerFormatMap.ToCore(target),
            GgufKvCacheFormat.F16, GpuOffloadLevel.None, 512, 4096,
            SupportLevel.DeclaredSupported, requiresEvidence: false);

    private static string Digest(char value) => new(value, 64);
}
