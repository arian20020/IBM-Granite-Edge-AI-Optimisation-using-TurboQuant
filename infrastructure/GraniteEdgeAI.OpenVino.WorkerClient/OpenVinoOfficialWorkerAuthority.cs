using System.Collections.ObjectModel;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>
/// Owns the exact build and binary policy for the packaged official worker.
/// Byte, hash, topology, and PE verification remain the closure resolver's job.
/// </summary>
public static class OpenVinoOfficialWorkerAuthority
{
    private const string WorkerExecutable = "OpenVinoOfficial.Worker.exe";
    private const string RuntimeBuild =
        "2026.3.0-22451-8a17657b995-releases/2026/3";
    private const string GenAiBuild = "2026.3.0.0-3277-bd8d6542e3c";
    private const string TokenizersBuild = "2026.3.0.0-703-183c6f25cda";

    private static readonly IReadOnlyDictionary<string, OpenVinoWorkerBinaryMachine>
        BinaryMachines = new ReadOnlyDictionary<string, OpenVinoWorkerBinaryMachine>(
            new Dictionary<string, OpenVinoWorkerBinaryMachine>(StringComparer.Ordinal)
            {
                [WorkerExecutable] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_genai.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_cpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_intel_gpu_plugin.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_ir_frontend.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["openvino_tokenizers.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbb12.dll"] = OpenVinoWorkerBinaryMachine.Amd64,
                ["tbbbind_2_5.dll"] = OpenVinoWorkerBinaryMachine.Amd64
            });

    /// <summary>
    /// Creates a validated installation bound to the app-approved manifest digest.
    /// </summary>
    public static OpenVinoWorkerInstallation CreateInstallation(
        string approvedWorkerRoot,
        string expectedManifestDigest)
    {
        var installation = new OpenVinoWorkerInstallation(
            approvedWorkerRoot,
            WorkerExecutable,
            OpenVinoProtocol.OfficialProtocolId,
            new OpenVinoBuildEvidence(
                RuntimeBuild,
                GenAiBuild,
                TokenizersBuild,
                expectedManifestDigest),
            BinaryMachines);
        installation.Validate();
        return installation;
    }
}
