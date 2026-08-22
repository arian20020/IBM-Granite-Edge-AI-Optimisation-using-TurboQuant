using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol;

/// <summary>
/// Defines the strict bounded JSON rules shared by the app and worker.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class WorkerProtocolJsonTests
{
    [TestMethod]
    public void DeserializeMessage_AllowsUnknownAdditiveProperty()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "messageType": "hello",
              "workerId": "GraniteEdgeAI.ModelInspection.Worker",
              "workerVersion": "1.0.0",
              "workerProcessId": 123,
              "runtimeProfile": "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
              "processArchitecture": "X64",
              "futureField": "ignored"
            }
            """;

        object message = WorkerProtocolJson.DeserializeMessage(
            TestJson.Utf8(json));

        Assert.IsInstanceOfType<WorkerHelloMessage>(message);
    }

    [TestMethod]
    public void DeserializeMessage_RejectsDuplicatePropertyAtNestedDepth()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "messageType": "completed",
              "requestId": "11111111-1111-1111-1111-111111111111",
              "completionStatus": "operationalFailure",
              "operationalFailure": {
                "code": "MI-OP-TEST",
                "code": "MI-OP-OVERRIDE",
                "message": "failed"
              }
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsPayloadAboveOneMiB()
    {
        byte[] bytes = new byte[WorkerProtocol.MaximumMessageBytes + 1];

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(bytes));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsEmptyPayload()
    {
        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(ReadOnlySpan<byte>.Empty));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsInvalidUtf8()
    {
        byte[] bytes =
        [
            (byte)'{',
            (byte)'"',
            0xC3,
            0x28,
            (byte)'"',
            (byte)':',
            (byte)'1',
            (byte)'}'
        ];

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(bytes));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsComments()
    {
        const string json = """
            {
              "protocolVersion": 1,
              // comments are not protocol data
              "messageType": "hello"
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsTrailingComma()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "messageType": "hello",
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsNonObjectRoot()
    {
        const string json = "[1, 2, 3]";

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsMissingDiscriminator()
    {
        const string json = """
            {
              "protocolVersion": 1
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsWrongCaseDiscriminator()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "MessageType": "hello"
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsUnknownMessageKind()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "messageType": "futureMessage"
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsWrongProtocolVersion()
    {
        const string json = """
            {
              "protocolVersion": 2,
              "messageType": "hello",
              "workerId": "GraniteEdgeAI.ModelInspection.Worker",
              "workerVersion": "1.0.0",
              "workerProcessId": 123,
              "runtimeProfile": "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
              "processArchitecture": "X64"
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeMessage_RejectsInvalidEnumValue()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "messageType": "progress",
              "requestId": "11111111-1111-1111-1111-111111111111",
              "stage": "notAStage",
              "stageStatus": "active",
              "completedStageCount": 0,
              "totalStageCount": 5,
              "stageFraction": null
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeMessage(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void DeserializeCommand_ParsesAndValidatesStartCommand()
    {
        byte[] bytes = WorkerProtocolJson.Serialize(TestJson.CreateValidStart());

        object command = WorkerProtocolJson.DeserializeCommand(bytes);

        WorkerStartInspectionCommand start =
            Assert.IsInstanceOfType<WorkerStartInspectionCommand>(command);
        Assert.AreEqual(@"C:\Models\granite.gguf", start.ModelPath);
    }

    [TestMethod]
    public void DeserializeCommand_RejectsUnknownCommandKind()
    {
        const string json = """
            {
              "protocolVersion": 1,
              "commandType": "futureCommand"
            }
            """;

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.DeserializeCommand(TestJson.Utf8(json)));
    }

    [TestMethod]
    public void CancelCommand_RoundTripsWithCamelCaseStringEnum()
    {
        WorkerCancelInspectionCommand expected = TestJson.CreateValidCancel();

        byte[] bytes = WorkerProtocolJson.Serialize(expected);
        object command = WorkerProtocolJson.DeserializeCommand(bytes);

        WorkerCancelInspectionCommand actual =
            Assert.IsInstanceOfType<WorkerCancelInspectionCommand>(command);
        Assert.AreEqual(expected, actual);
        StringAssert.Contains(
            Encoding.UTF8.GetString(bytes),
            "\"commandType\":\"cancelInspection\"");
    }

    [TestMethod]
    public void Serialize_ProducesCompactCamelCaseJsonWithoutNewline()
    {
        byte[] bytes = WorkerProtocolJson.Serialize(TestJson.CreateValidHello());
        string json = Encoding.UTF8.GetString(bytes);

        StringAssert.Contains(json, "\"protocolVersion\":1");
        StringAssert.Contains(json, "\"messageType\":\"hello\"");
        Assert.IsFalse(json.Contains('\n', StringComparison.Ordinal));
        Assert.IsFalse(json.Contains('\r', StringComparison.Ordinal));
    }

    [TestMethod]
    public void Serialize_RejectsInvalidRecordBeforeWriting()
    {
        WorkerHelloMessage invalid = TestJson.CreateValidHello() with
        {
            WorkerId = string.Empty
        };

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.Serialize(invalid));
    }

    [TestMethod]
    public void Serialize_RejectsPayloadAboveOneMiB()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CompletionStatus = WorkerCompletionStatus.OperationalFailure,
            Evidence = null,
            OperationalFailure = new WorkerOperationalFailure
            {
                Code = "MI-OP-TEST",
                Message = new string('x', WorkerProtocol.MaximumMessageBytes)
            }
        };

        Assert.ThrowsExactly<WorkerProtocolException>(() =>
            WorkerProtocolJson.Serialize(message));
    }
}
