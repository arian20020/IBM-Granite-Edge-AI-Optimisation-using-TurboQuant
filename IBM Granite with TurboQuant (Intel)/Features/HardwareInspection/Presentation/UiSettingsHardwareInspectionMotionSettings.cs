using System;
using Windows.UI.ViewManagement;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation;

internal sealed class UiSettingsHardwareInspectionMotionSettings
    : IHardwareInspectionMotionSettings
{
    private UISettings? _settings;

    public bool AnimationsEnabled
    {
        get
        {
            try
            {
                _settings ??= new UISettings();
                return _settings.AnimationsEnabled;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                return false;
            }
        }
    }

    private static bool IsRecoverable(Exception exception) => exception is not
        (OutOfMemoryException or StackOverflowException or AccessViolationException);
}
