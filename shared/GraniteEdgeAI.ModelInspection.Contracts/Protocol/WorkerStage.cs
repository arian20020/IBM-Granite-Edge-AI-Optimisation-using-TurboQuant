namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Identifies the five truthful lightweight inspection stages.
/// </summary>
public enum WorkerStage
{
    CheckModelPackage = 1,
    ReadModelConfiguration = 2,
    ValidateTokenizerAndChatSetup = 3,
    ValidateModelStructure = 4,
    ConfirmCoreRuntimeCompatibility = 5
}
