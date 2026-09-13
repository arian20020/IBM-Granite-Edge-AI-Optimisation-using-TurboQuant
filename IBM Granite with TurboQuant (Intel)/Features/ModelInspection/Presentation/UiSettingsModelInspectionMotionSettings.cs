using System;
using Windows.UI.ViewManagement;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal interface IModelInspectionAnimationsEnabledSource : IDisposable
{
    bool AnimationsEnabled { get; }

    event EventHandler? AnimationsEnabledChanged;
}

internal sealed class UiSettingsModelInspectionMotionSettings :
    IModelInspectionMotionSettings
{
    private readonly IModelInspectionAnimationsEnabledSource source;
    private bool isDisposed;

    internal UiSettingsModelInspectionMotionSettings()
        : this(new UiSettingsAnimationsEnabledSource())
    {
    }

    internal UiSettingsModelInspectionMotionSettings(
        IModelInspectionAnimationsEnabledSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        source.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
    }

    public bool AnimationsEnabled
    {
        get
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return source.AnimationsEnabled;
        }
    }

    public event EventHandler? AnimationsEnabledChanged;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        source.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        source.Dispose();
        AnimationsEnabledChanged = null;
    }

    private void OnAnimationsEnabledChanged(object? sender, EventArgs args)
    {
        if (!isDisposed)
        {
            AnimationsEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

internal sealed class UiSettingsAnimationsEnabledSource :
    IModelInspectionAnimationsEnabledSource
{
    private readonly UISettings settings;
    private bool isDisposed;

    internal UiSettingsAnimationsEnabledSource()
    {
        settings = new UISettings();
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            settings.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
        }
    }

    public bool AnimationsEnabled
    {
        get
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return settings.AnimationsEnabled;
        }
    }

    public event EventHandler? AnimationsEnabledChanged;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            settings.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
        }
        AnimationsEnabledChanged = null;
    }

    private void OnAnimationsEnabledChanged(
        UISettings sender,
        UISettingsAnimationsEnabledChangedEventArgs args)
    {
        if (!isDisposed)
        {
            AnimationsEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
