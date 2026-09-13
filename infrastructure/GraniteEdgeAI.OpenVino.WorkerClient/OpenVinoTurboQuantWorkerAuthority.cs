using System.Collections.ObjectModel;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>
/// Owns the exact identity and binary policy for the separately packaged
/// OpenVINO TurboQuant worker. The manifest digest remains caller supplied by
/// the signed application build; the worker cannot nominate its own identity.
/// </summary>
public static class OpenVinoTurboQuantWorkerAuthority
{
    private const string WorkerExecutable = "OpenVinoTurboQuant.Worker.exe";
    private const string RuntimeBuild = "2026.5.0-22950-f5f594dc0c9";
    private const string GenAiBuild =
        "2026.5.0.0-3409-6fbc103538d";
    private const string TokenizersBuild = "2026.5.0.0-737-824033c3061";
    private const string SourceCommit =
        "f5f594dc0c9e5961785f0d17743486d52eac87e7";
    private const string ImplementationCommit =
        "b9a1f201c109e0bed74763934f79483cf6c4cbf4";

    private static readonly IReadOnlyDictionary<string, OpenVinoWorkerBinaryMachine>
        BinaryMachines = new ReadOnlyDictionary<string, OpenVinoWorkerBinaryMachine>(
            new Dictionary<string, OpenVinoWorkerBinaryMachine>(StringComparer.Ordinal)
            {
                [WorkerExecutable] = OpenVinoWorkerBinaryMachine.Amd64,
                ["OpenVinoTurboQuant.Probe.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_genai.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_cpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_ir_frontend.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_tokenizers.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbb12.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbbbind_2_5.dll"] = OpenVinoWorkerBinaryMachine.Amd64
            });

    public static OpenVinoWorkerInstallation CreateInstallation(
        string approvedWorkerRoot,
        string expectedWorkerManifestDigest,
        string expectedPatchSeriesDigest,
        string expectedRuntimeManifestDigest)
    {
        var installation = new OpenVinoWorkerInstallation(
            approvedWorkerRoot,
            WorkerExecutable,
            OpenVinoProtocol.TurboQuantProtocolId,
            new OpenVinoBuildEvidence(
                RuntimeBuild,
                GenAiBuild,
                TokenizersBuild,
                expectedWorkerManifestDigest,
                new TurboQuantBuildEvidence(
                    SourceCommit,
                    ImplementationCommit,
                    expectedPatchSeriesDigest,
                    expectedRuntimeManifestDigest)),
            BinaryMachines);
        installation.Validate();
        return installation;
    }
}
