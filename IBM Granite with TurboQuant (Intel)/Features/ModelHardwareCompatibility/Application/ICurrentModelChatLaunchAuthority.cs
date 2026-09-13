using System;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;

internal enum CurrentModelChatSupportCode
{
    None = 0,
    CancelledByUser,
    BindingMismatch,
    SourceUnavailable,
    RuntimeUnavailable,
    ReplanRequired,
    LaunchFailed
}

internal sealed record CurrentModelChatLaunchResult
{
    private CurrentModelChatLaunchResult(
        bool succeeded,
        CurrentModelChatSupportCode supportCode)
    {
        Succeeded = succeeded;
        SupportCode = supportCode;
    }

    internal bool Succeeded { get; }
    internal CurrentModelChatSupportCode SupportCode { get; }

    internal static CurrentModelChatLaunchResult Success { get; } =
        new(true, CurrentModelChatSupportCode.None);

    internal static CurrentModelChatLaunchResult Failed(
        CurrentModelChatSupportCode supportCode) =>
        supportCode is CurrentModelChatSupportCode.None
            ? throw new ArgumentOutOfRangeException(nameof(supportCode))
            : new CurrentModelChatLaunchResult(false, supportCode);
}

internal interface ICurrentModelChatLaunchAuthority
{
    Task<CurrentModelChatLaunchResult> LaunchAsync(
        CurrentModelLaunchHandoff handoff,
        CancellationToken cancellationToken);
}
