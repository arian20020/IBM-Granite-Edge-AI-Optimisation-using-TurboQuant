namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

internal static class GgufNativeConstants
{
    internal const uint ExtendedStartupInfoPresent = 0x00080000;
    internal const uint CreateNoWindow = 0x08000000;
    internal const uint CreateUnicodeEnvironment = 0x00000400;
    internal const uint RequiredCreationFlags =
        ExtendedStartupInfoPresent | CreateNoWindow | CreateUnicodeEnvironment;
    internal const uint StartUseStandardHandles = 0x00000100;
    internal const uint HandleFlagInherit = 0x00000001;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;
    internal const int JobObjectBasicAccountingInformationClass = 1;
    internal const int JobObjectBasicProcessIdListClass = 3;
    internal const int JobObjectExtendedLimitInformationClass = 9;
    internal const int ErrorInsufficientBuffer = 122;
    internal static readonly nuint ProcThreadAttributeHandleList = 0x00020002;
    internal static readonly nuint ProcThreadAttributeJobList = 0x0002000D;
}
