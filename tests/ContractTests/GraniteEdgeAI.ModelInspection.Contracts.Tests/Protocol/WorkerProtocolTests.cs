using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests.Protocol;

/// <summary>
/// Defines the required identity and invariants of the worker protocol records.
/// </summary>
[TestClass]
[TestCategory("Contract")]
public sealed class WorkerProtocolTests
{
    [TestMethod]
    public void WorkerProtocol_UsesApprovedIdentityLimitsAndTimeouts()
    {
        // Read compile-time constants through the public metadata surface so
        // this verifies the published contract rather than asserting a value
        // the test compiler has already substituted into its own assembly.
        int version = ReadPublicConstant<int>(nameof(WorkerProtocol.Version));
        string workerId =
            ReadPublicConstant<string>(nameof(WorkerProtocol.WorkerId));
        string runtimeProfile =
            ReadPublicConstant<string>(nameof(WorkerProtocol.RuntimeProfile));
        int maximumMessageBytes =
            ReadPublicConstant<int>(nameof(WorkerProtocol.MaximumMessageBytes));
        int maximumRetainedStandardErrorBytes = ReadPublicConstant<int>(
            nameof(WorkerProtocol.MaximumRetainedStandardErrorBytes));

        Assert.AreEqual(1, version);
        Assert.AreEqual("GraniteEdgeAI.ModelInspection.Worker", workerId);
        Assert.AreEqual(
            "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
            runtimeProfile);
        Assert.AreEqual(1024 * 1024, maximumMessageBytes);
        Assert.AreEqual(
            256 * 1024,
            maximumRetainedStandardErrorBytes);
        Assert.AreEqual(TimeSpan.FromSeconds(5), WorkerProtocol.StartupTimeout);
        Assert.AreEqual(TimeSpan.FromMinutes(5), WorkerProtocol.OverallTimeout);
        Assert.AreEqual(
            TimeSpan.FromSeconds(5),
            WorkerProtocol.CancellationGracePeriod);
    }

    [TestMethod]
    public void Hello_ValidApprovedIdentity_PassesValidation()
    {
        WorkerHelloMessage message = CreateValidHello();

        message.Validate();
    }

    [TestMethod]
    public void Hello_EmptyWorkerId_Throws()
    {
        WorkerHelloMessage message = CreateValidHello() with
        {
            WorkerId = string.Empty
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void Hello_NonPositiveProcessId_Throws()
    {
        WorkerHelloMessage message = CreateValidHello() with
        {
            WorkerProcessId = 0
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void Start_EmptyRequestId_Throws()
    {
        WorkerStartInspectionCommand command = CreateValidStart() with
        {
            RequestId = Guid.Empty
        };

        Assert.ThrowsExactly<WorkerProtocolException>(command.Validate);
    }

    [TestMethod]
    public void Start_DefaultParentStartTime_Throws()
    {
        WorkerStartInspectionCommand command = CreateValidStart() with
        {
            ParentProcessStartTimeUtc = default
        };

        Assert.ThrowsExactly<WorkerProtocolException>(command.Validate);
    }

    [TestMethod]
    public void Start_RelativeModelPath_Throws()
    {
        WorkerStartInspectionCommand command = CreateValidStart() with
        {
            ModelPath = "models/granite.gguf"
        };

        Assert.ThrowsExactly<WorkerProtocolException>(command.Validate);
    }

    [TestMethod]
    public void Start_NonPositiveExpectedLength_Throws()
    {
        WorkerStartInspectionCommand command = CreateValidStart() with
        {
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 0,
                LastWriteTimeUtc = DateTimeOffset.UtcNow
            }
        };

        Assert.ThrowsExactly<WorkerProtocolException>(command.Validate);
    }

    [TestMethod]
    public void Start_NonGgufSnapshot_Throws()
    {
        WorkerStartInspectionCommand command = CreateValidStart() with
        {
            QuickScan = CreateValidQuickScan() with
            {
                Format = "OpenVINO"
            }
        };

        Assert.ThrowsExactly<WorkerProtocolException>(command.Validate);
    }

    [TestMethod]
    public void Cancel_EmptyRequestId_Throws()
    {
        WorkerCancelInspectionCommand command = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.CancelInspection,
            RequestId = Guid.Empty
        };

        Assert.ThrowsExactly<WorkerProtocolException>(command.Validate);
    }

    [TestMethod]
    public void Progress_FractionOutsideRange_Throws()
    {
        WorkerProgressMessage message = CreateValidProgress() with
        {
            StageFraction = 1.01
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void Progress_DecreasingStageCountInvariant_RejectsInvalidCount()
    {
        WorkerProgressMessage message = CreateValidProgress() with
        {
            CompletedStageCount = 6
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void CompletedStatus_RequiresEvidence()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.Completed,
            Evidence = null,
            OperationalFailure = null
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void CompletedStatus_ForbidsOperationalFailure()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.Completed,
            Evidence = TestJson.CreateValidEvidence(),
            OperationalFailure = CreateValidOperationalFailure()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void CancelledStatus_ForbidsEvidence()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.Cancelled,
            Evidence = TestJson.CreateValidEvidence(),
            OperationalFailure = null
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void CancelledStatus_ForbidsOperationalFailure()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.Cancelled,
            Evidence = null,
            OperationalFailure = CreateValidOperationalFailure()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void OperationalFailureStatus_RequiresFailure()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.OperationalFailure,
            Evidence = null,
            OperationalFailure = null
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void OperationalFailureStatus_ForbidsEvidence()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.OperationalFailure,
            Evidence = TestJson.CreateValidEvidence(),
            OperationalFailure = CreateValidOperationalFailure()
        };

        Assert.ThrowsExactly<WorkerProtocolException>(message.Validate);
    }

    [TestMethod]
    public void CompletedStatus_WithEvidenceOnly_PassesValidation()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.Completed,
            Evidence = TestJson.CreateValidEvidence(),
            OperationalFailure = null
        };

        message.Validate();
    }

    [TestMethod]
    public void CancelledStatus_WithoutTerminalData_PassesValidation()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.Cancelled,
            Evidence = null,
            OperationalFailure = null
        };

        message.Validate();
    }

    [TestMethod]
    public void OperationalFailureStatus_WithFailureOnly_PassesValidation()
    {
        WorkerCompletedMessage message = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.OperationalFailure,
            Evidence = null,
            OperationalFailure = CreateValidOperationalFailure()
        };

        message.Validate();
    }

    [TestMethod]
    public void ChatTemplateEvidence_ExposesOnlyApprovedSummaryProperties()
    {
        string[] propertyNames = typeof(WorkerChatTemplateEvidence)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "LengthCharacters", "Present", "Sha256" },
            propertyNames);
    }

    [TestMethod]
    public void ConfigurationEvidence_AllowsUnavailableStructuralValues()
    {
        WorkerModelConfigurationEvidence evidence = new();

        Assert.IsNull(evidence.ParameterCount);
        Assert.IsNull(evidence.DeclaredContextLength);
        Assert.IsNull(evidence.LayerCount);
        Assert.IsNull(evidence.AttentionHeadCount);
        Assert.IsNull(evidence.KvHeadCount);
    }

    private static T ReadPublicConstant<T>(string fieldName)
    {
        FieldInfo? field = typeof(WorkerProtocol).GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Static);

        if (field is null || !field.IsLiteral || field.IsInitOnly)
        {
            throw new AssertFailedException(
                $"WorkerProtocol.{fieldName} is not a public constant.");
        }

        object? rawValue = field.GetRawConstantValue();
        if (rawValue is not T typedValue)
        {
            throw new AssertFailedException(
                $"WorkerProtocol.{fieldName} is not a {typeof(T).Name} constant.");
        }

        return typedValue;
    }

    private static WorkerHelloMessage CreateValidHello()
    {
        return new WorkerHelloMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Hello,
            WorkerId = WorkerProtocol.WorkerId,
            WorkerVersion = "1.0.0",
            WorkerProcessId = 1234,
            RuntimeProfile = WorkerProtocol.RuntimeProfile,
            ProcessArchitecture = "X64"
        };
    }

    private static WorkerStartInspectionCommand CreateValidStart()
    {
        return new WorkerStartInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.NewGuid(),
            ParentProcessId = 4321,
            ParentProcessStartTimeUtc = DateTimeOffset.UtcNow,
            ModelPath = @"C:\Models\granite.gguf",
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 100,
                LastWriteTimeUtc = DateTimeOffset.UtcNow
            },
            QuickScan = CreateValidQuickScan()
        };
    }

    private static WorkerQuickScanSnapshot CreateValidQuickScan()
    {
        return new WorkerQuickScanSnapshot
        {
            Format = "GGUF",
            ModelName = "Granite 4.1 3B",
            Architecture = "granite",
            ParameterSizeLabel = "3B",
            Quantisation = "Q4_K_M",
            FileSizeBytes = 100,
            DeclaredContextLength = 131_072,
            GgufVersion = 3
        };
    }

    private static WorkerProgressMessage CreateValidProgress()
    {
        return new WorkerProgressMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Progress,
            RequestId = Guid.NewGuid(),
            Stage = WorkerStage.ReadModelConfiguration,
            StageStatus = WorkerStageStatus.Active,
            CompletedStageCount = 1,
            TotalStageCount = 5,
            StageFraction = null
        };
    }

    private static WorkerOperationalFailure CreateValidOperationalFailure()
    {
        return new WorkerOperationalFailure
        {
            Code = "MI-OP-TEST",
            Message = "The controlled inspection failed."
        };
    }
}
