using GraniteEdgeAI.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Specifies the exact identity that must be trusted before the parent sends a
/// model path to a worker process.
/// </summary>
[TestClass]
public sealed class WorkerHandshakeValidatorTests
{
    [TestMethod]
    public void ExactHelloIsAccepted()
    {
        WorkerHandshakeValidator.Validate(
            WorkerClientTestData.Hello(processId: 1234),
            expectedProcessId: 1234);
    }

    [TestMethod]
    public void ProcessIdMustMatchTheCreatedProcess()
    {
        WorkerClientPolicyException error = AssertInvalid(
            WorkerClientTestData.Hello(processId: 4321),
            expectedProcessId: 1234);

        StringAssert.DoesNotContain(error.ToString(), "4321");
    }

    [TestMethod]
    public void WorkerIdMustMatchExactly()
    {
        WorkerClientPolicyException error = AssertInvalid(
            WorkerClientTestData.Hello() with
            {
                WorkerId = "unexpected-worker"
            });

        StringAssert.DoesNotContain(error.ToString(), "unexpected-worker");
    }

    [TestMethod]
    public void RuntimeProfileMustMatchExactly()
    {
        WorkerClientPolicyException error = AssertInvalid(
            WorkerClientTestData.Hello() with
            {
                RuntimeProfile = "unexpected-profile"
            });

        StringAssert.DoesNotContain(error.ToString(), "unexpected-profile");
    }

    [TestMethod]
    public void ArchitectureMustBeX64()
    {
        WorkerClientPolicyException error = AssertInvalid(
            WorkerClientTestData.Hello() with
            {
                ProcessArchitecture = "ARM64"
            });

        StringAssert.DoesNotContain(error.ToString(), "ARM64");
    }

    [TestMethod]
    public void ProtocolVersionMustMatchExactly()
    {
        WorkerClientPolicyException error = AssertInvalid(
            WorkerClientTestData.Hello() with
            {
                ProtocolVersion = WorkerProtocol.Version + 1
            });

        StringAssert.DoesNotContain(
            error.ToString(),
            (WorkerProtocol.Version + 1).ToString());
    }

    [TestMethod]
    public void WorkerVersionMustBePresent()
    {
        _ = AssertInvalid(
            WorkerClientTestData.Hello() with
            {
                WorkerVersion = " "
            });
    }

    [TestMethod]
    public void ExpectedProcessIdMustBePositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            WorkerHandshakeValidator.Validate(
                WorkerClientTestData.Hello(),
                expectedProcessId: 0));
    }

    private static WorkerClientPolicyException AssertInvalid(
        WorkerHelloMessage hello,
        int expectedProcessId = 1234)
    {
        WorkerClientPolicyException error =
            Assert.ThrowsExactly<WorkerClientPolicyException>(() =>
                WorkerHandshakeValidator.Validate(
                    hello,
                    expectedProcessId));

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerHandshakeInvalid,
            error.Failure.Code);
        Assert.AreEqual(
            "The Model Inspection worker identity could not be verified.",
            error.Failure.Message);
        return error;
    }
}
