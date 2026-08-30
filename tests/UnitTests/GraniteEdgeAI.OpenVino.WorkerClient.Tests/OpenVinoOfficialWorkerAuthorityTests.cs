using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.WorkerClient.Tests;

[TestClass]
public sealed class OpenVinoOfficialWorkerAuthorityTests
{
    private const string Digest =
        "1111111111111111111111111111111111111111111111111111111111111111";

    [TestMethod]
    public void FactoryOwnsAndValidatesTheExactOfficialInstallation()
    {
        string root = Path.GetFullPath("official-worker");

        OpenVinoWorkerInstallation installation =
            OpenVinoOfficialWorkerAuthority.CreateInstallation(root, Digest);

        Assert.AreEqual(root, installation.ApprovedWorkerRoot);
        Assert.AreEqual(
            "OpenVinoOfficial.Worker.exe",
            installation.WorkerExecutableRelativePath);
        Assert.AreEqual(OpenVinoProtocol.OfficialProtocolId, installation.ExpectedProtocolId);
        Assert.AreEqual(
            "2026.3.0-22451-8a17657b995-releases/2026/3",
            installation.ExpectedBuildEvidence.RuntimeBuild);
        Assert.AreEqual(
            "2026.3.0.0-3277-bd8d6542e3c",
            installation.ExpectedBuildEvidence.GenAiBuild);
        Assert.AreEqual(
            "2026.3.0.0-703-183c6f25cda",
            installation.ExpectedBuildEvidence.TokenizersBuild);
        Assert.AreEqual(Digest, installation.ExpectedBuildEvidence.WorkerManifestDigest);
        Assert.IsNull(installation.ExpectedBuildEvidence.TurboQuantBuild);
        Assert.AreEqual(9, installation.ExpectedBinaryMachines.Count);
        Assert.IsTrue(installation.ExpectedBinaryMachines.Values.All(
            machine => machine == OpenVinoWorkerBinaryMachine.Amd64));
        installation.Validate();
    }

    [TestMethod]
    public void BinaryInventoryCannotBeMutatedByACaller()
    {
        OpenVinoWorkerInstallation installation =
            OpenVinoOfficialWorkerAuthority.CreateInstallation(
                Path.GetFullPath("official-worker"),
                Digest);
        var mutableView = (IDictionary<string, OpenVinoWorkerBinaryMachine>)
            installation.ExpectedBinaryMachines;

        Assert.ThrowsExactly<NotSupportedException>(() => mutableView.Add(
            "unapproved.dll",
            OpenVinoWorkerBinaryMachine.Amd64));
    }

    [TestMethod]
    public void InvalidManifestIdentityIsRejectedAtTheAuthorityBoundary()
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoOfficialWorkerAuthority.CreateInstallation(
                Path.GetFullPath("official-worker"),
                "not-a-digest"));
    }
}
