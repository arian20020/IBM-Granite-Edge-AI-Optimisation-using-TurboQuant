namespace GraniteEdgeAI.ModelInspection.WorkerClient.Windows;

/// <summary>
/// Contains the Windows constants locked by the Gate 2 process-boundary design.
/// Keeping them named and centralized makes accidental breakaway or inheritance
/// changes visible in review and tests.
/// </summary>
internal static class NativeConstants
{
    internal const uint ExtendedStartupInfoPresent = 0x00080000;
    internal const uint CreateNoWindow = 0x08000000;
    internal const uint CreateUnicodeEnvironment = 0x00000400;
    internal const uint CreateBreakawayFromJob = 0x01000000;
    internal const uint RequiredCreationFlags =
        ExtendedStartupInfoPresent |
        CreateNoWindow |
        CreateUnicodeEnvironment;

    internal const uint StartUseStandardHandles = 0x00000100;
    internal const uint HandleFlagInherit = 0x00000001;

    internal const uint JobObjectLimitBreakawayOk = 0x00000800;
    internal const uint JobObjectLimitSilentBreakawayOk = 0x00001000;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;

    internal const int JobObjectBasicAccountingInformationClass = 1;
    internal const int JobObjectBasicProcessIdListClass = 3;
    internal const int JobObjectExtendedLimitInformationClass = 9;

    internal const uint WaitObject0 = 0x00000000;
    internal const uint WaitTimeout = 0x00000102;
    internal const uint WaitFailed = 0xFFFFFFFF;
    internal const uint Infinite = 0xFFFFFFFF;
    internal const uint StillActive = 259;

    internal const int ErrorInsufficientBuffer = 122;
    internal const int ErrorBrokenPipe = 109;

    internal static readonly nuint ProcThreadAttributeHandleList = 0x00020002;
    internal static readonly nuint ProcThreadAttributeJobList = 0x0002000D;
}
