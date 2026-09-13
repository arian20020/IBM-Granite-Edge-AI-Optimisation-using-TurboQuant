using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.WorkerClient.Tests;

[TestClass]
public sealed class OpenVinoTurboQuantWorkerAuthorityTests
{
    [TestMethod]
    public void InstallationPinsTheTestedTurboQuantWorkerIdentity()
    {
        OpenVinoWorkerInstallation installation =
            OpenVinoTurboQuantWorkerAuthority.CreateInstallation(
                Path.GetFullPath("turbo-worker"),
                new string('1', 64),
                new string('2', 64),
                new string('3', 64));

        Assert.AreEqual(OpenVinoProtocol.TurboQuantProtocolId,
            installation.ExpectedProtocolId);
        Assert.AreEqual("OpenVinoTurboQuant.Worker.exe",
            installation.WorkerExecutableRelativePath);
        Assert.AreEqual("2026.5.0-22950-f5f594dc0c9",
            installation.ExpectedBuildEvidence.RuntimeBuild);
        Assert.AreEqual("6fbc103538d30d42da4b0b5130a4792a20f728ba",
            installation.ExpectedBuildEvidence.GenAiBuild);
        Assert.AreEqual("2026.5.0",
            installation.ExpectedBuildEvidence.TokenizersBuild);
        Assert.AreEqual("f5f594dc0c9e5961785f0d17743486d52eac87e7",
            installation.ExpectedBuildEvidence.TurboQuantBuild!.SourceCommit);
        Assert.AreEqual("b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            installation.ExpectedBuildEvidence.TurboQuantBuild.ImplementationCommit);
        Assert.AreEqual(OpenVinoWorkerBinaryMachine.Amd64,
            installation.ExpectedBinaryMachines["OpenVinoTurboQuant.Worker.exe"]);
    }

    [TestMethod]
    public void InstallationRejectsUnpinnedDigests()
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            OpenVinoTurboQuantWorkerAuthority.CreateInstallation(
                Path.GetFullPath("turbo-worker"),
                "not-a-digest",
                new string('2', 64),
                new string('3', 64)));
    }
}
