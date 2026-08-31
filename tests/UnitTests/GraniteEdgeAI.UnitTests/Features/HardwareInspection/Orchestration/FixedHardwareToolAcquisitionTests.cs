using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[DoNotParallelize]
[TestCategory("HardwareInspection")]
[TestCategory("HardwareInspectionGate7Acceptance")]
public sealed class FixedHardwareToolAcquisitionTests
{
    [TestMethod]
    public void Acquire_ReturnsBothVerifiedToolsAndLeaseDisposalIsIdempotent()
    {
        using VerifiedPackagedToolFixture llmFit = VerifiedPackagedToolFixture.CreateLlmFit("success");
        using VerifiedPackagedToolFixture llamaCpp = VerifiedPackagedToolFixture.CreateLlamaCpp("success");
        FixedHardwareToolAcquisition acquisition = Create(
            llmFit,
            llamaCpp,
            (_, _, manifest) => TrustedToolVerificationResult.Verified(
                manifest.ToolId == LlamaCppCapabilityCommandContract.ToolId ? llamaCpp.Tool : llmFit.Tool));

        HardwareToolAcquisitionResult result = acquisition.Acquire();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Diagnostic);
        Assert.AreSame(llmFit.Tool, result.Lease!.LlmFit);
        Assert.AreSame(llamaCpp.Tool, result.Lease.LlamaCpp);
        result.Lease.Dispose();
        result.Lease.Dispose();
        Assert.IsTrue(llmFit.Tool.IsDisposed);
        Assert.IsTrue(llamaCpp.Tool.IsDisposed);
    }

    [TestMethod]
    public void Acquire_MissingOptionalLlmFitStillVerifiesProbeButRejectedLlmFitFailsClosed()
    {
        using VerifiedPackagedToolFixture llmFit = VerifiedPackagedToolFixture.CreateLlmFit("success");
        using VerifiedPackagedToolFixture llamaCpp = VerifiedPackagedToolFixture.CreateLlamaCpp("success");
        int calls = 0;
        FixedHardwareToolAcquisition missing = new(
            Path.GetDirectoryName(llmFit.PackageRoot)!,
            Path.Combine(Path.GetDirectoryName(llmFit.PackageRoot)!, "absent"),
            Path.GetDirectoryName(llamaCpp.PackageRoot)!,
            llamaCpp.PackageRoot,
            CreateProbeManifest(llamaCpp.PackageRoot),
            (_, _, _) => { calls++; return TrustedToolVerificationResult.Verified(llamaCpp.Tool); });

        HardwareToolAcquisitionResult degraded = missing.Acquire();
        Assert.IsTrue(degraded.IsSuccess);
        Assert.IsNotNull(degraded.Lease);
        Assert.AreSame(llamaCpp.Tool, degraded.Lease.LlamaCpp);
        Assert.IsNull(degraded.Lease.LlmFit);
        Assert.AreEqual(
            HardwareToolAcquisitionDiagnosticCode.ToolNotAvailable,
            degraded.Lease.LlmFitDiagnostic);
        Assert.AreEqual(1, calls);
        degraded.Lease.Dispose();

        FixedHardwareToolAcquisition rejected = Create(
            llmFit,
            llamaCpp,
            (_, _, _) => { calls++; return TrustedToolVerificationResult.Rejected(TrustedToolVerificationFailure.HashMismatch); });
        AssertFailure(rejected.Acquire(), HardwareToolAcquisitionDiagnosticCode.ToolIntegrityFailure);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void Acquire_MapsAbsentInvalidOrRejectedProbeAndDisposesPartialLlmCustody()
    {
        using VerifiedPackagedToolFixture llmFit = VerifiedPackagedToolFixture.CreateLlmFit("success");
        using VerifiedPackagedToolFixture llamaCpp = VerifiedPackagedToolFixture.CreateLlamaCpp("success");
        string probeParent = Path.GetDirectoryName(llamaCpp.PackageRoot)!;
        FixedHardwareToolAcquisition absent = new(
            Path.GetDirectoryName(llmFit.PackageRoot)!,
            llmFit.PackageRoot,
            probeParent,
            Path.Combine(probeParent, "absent"),
            CreateProbeManifest(llamaCpp.PackageRoot),
            (_, _, _) => TrustedToolVerificationResult.Verified(llmFit.Tool));

        AssertFailure(absent.Acquire(), HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable);
        Assert.IsTrue(llmFit.Tool.IsDisposed);

        using VerifiedPackagedToolFixture llmFitInvalid = VerifiedPackagedToolFixture.CreateLlmFit("nonzero");
        FixedHardwareToolAcquisition invalidManifest = new(
            Path.GetDirectoryName(llmFitInvalid.PackageRoot)!,
            llmFitInvalid.PackageRoot,
            probeParent,
            llamaCpp.PackageRoot,
            Encoding.UTF8.GetBytes("{}\n"),
            (_, _, _) => TrustedToolVerificationResult.Verified(llmFitInvalid.Tool));
        AssertFailure(invalidManifest.Acquire(), HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable);
        Assert.IsTrue(llmFitInvalid.Tool.IsDisposed);

        using VerifiedPackagedToolFixture llmFitRejectedProbe = VerifiedPackagedToolFixture.CreateLlmFit("version-mismatch");
        int calls = 0;
        FixedHardwareToolAcquisition rejectedProbe = new(
            Path.GetDirectoryName(llmFitRejectedProbe.PackageRoot)!,
            llmFitRejectedProbe.PackageRoot,
            probeParent,
            llamaCpp.PackageRoot,
            CreateProbeManifest(llamaCpp.PackageRoot),
            (_, _, _) => ++calls == 1
                ? TrustedToolVerificationResult.Verified(llmFitRejectedProbe.Tool)
                : TrustedToolVerificationResult.Rejected(TrustedToolVerificationFailure.InventoryMismatch));
        AssertFailure(rejectedProbe.Acquire(), HardwareToolAcquisitionDiagnosticCode.PackagedProbeUnavailable);
        Assert.AreEqual(2, calls);
        Assert.IsTrue(llmFitRejectedProbe.Tool.IsDisposed);
    }

    [TestMethod]
    public void CreateProduction_HasNoCallerControlledPathOrManifestParameters()
    {
        var method = typeof(FixedHardwareToolAcquisition).GetMethod(
            nameof(FixedHardwareToolAcquisition.CreateProduction),
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        Assert.AreEqual(0, method.GetParameters().Length);
        Assert.IsFalse(typeof(FixedHardwareToolAcquisition)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Any(candidate => candidate.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(string) ||
                parameter.ParameterType == typeof(byte[]))));
    }

    private static FixedHardwareToolAcquisition Create(
        VerifiedPackagedToolFixture llmFit,
        VerifiedPackagedToolFixture llamaCpp,
        Func<string, string, TrustedToolPackageManifest, TrustedToolVerificationResult> verifier) => new(
            Path.GetDirectoryName(llmFit.PackageRoot)!,
            llmFit.PackageRoot,
            Path.GetDirectoryName(llamaCpp.PackageRoot)!,
            llamaCpp.PackageRoot,
            CreateProbeManifest(llamaCpp.PackageRoot),
            verifier);

    private static byte[] CreateProbeManifest(string packageRoot)
    {
        string executable = Path.Combine(packageRoot, LlamaCppCapabilityCommandContract.ExecutableName);
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable))).ToLowerInvariant();
        string[] members = Directory.GetFiles(packageRoot).Select(Path.GetFileName).Select(name => name!).Order(StringComparer.Ordinal).ToArray();
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            toolId = LlamaCppCapabilityCommandContract.ToolId,
            version = LlamaCppCapabilityCommandContract.Version,
            executable = LlamaCppCapabilityCommandContract.ExecutableName,
            executableSha256 = hash,
            members,
            machine = "Amd64",
            disposition = "AcceptedForFunctionalEvaluation",
            commands = new object[]
            {
                new { identity = "identity", arguments = new[] { "identity", "--format", "json-v1" } },
                new { identity = "capabilities", arguments = new[] { "capabilities", "--format", "json-v1" } },
            },
        });
        return Encoding.UTF8.GetBytes(json + "\n");
    }

    private static void AssertFailure(HardwareToolAcquisitionResult result, HardwareToolAcquisitionDiagnosticCode diagnostic)
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Lease);
        Assert.AreEqual(diagnostic, result.Diagnostic);
    }
}
