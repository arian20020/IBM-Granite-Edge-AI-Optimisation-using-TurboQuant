using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Presentation.Progress;

/// <summary>A view-owned cadence; callbacks update existing controls, never operation state.</summary>
internal sealed class DispatcherProgressPresenter : IDisposable
{
    private readonly FrameworkElement owner;
    private readonly Action tick;
    private readonly DispatcherQueueTimer timer;
    private bool disposed;
    private bool requested;
    private readonly List<(UIElement Element, long Token)> visibilitySubscriptions = [];
    private ProgressBar? bar;
    private readonly BoundedProgressInterpolation interpolation = new();

    internal void SetValue(ProgressBar bar, double value, bool motion)
    {
        bool animate = ReferenceEquals(this.bar, bar) && owner.IsLoaded && motion;
        this.bar = bar;
        interpolation.SetTarget(value, animate);
        bar.Value = interpolation.GetFraction();
    }

    internal DispatcherProgressPresenter(
        FrameworkElement owner,
        Action tick,
        TimeSpan? interval = null)
    {
        this.owner = owner;
        this.tick = tick;
        timer = owner.DispatcherQueue.CreateTimer();
        timer.Interval = interval ?? TimeSpan.FromMilliseconds(50);
        timer.Tick += OnTick;
        owner.Unloaded += OnUnloaded;
    }

    internal void Start()
    {
        if (disposed) return;
        requested = true;
        if (!owner.IsLoaded) return;
        if (visibilitySubscriptions.Count == 0)
            for (DependencyObject? current = owner; current is not null; current = VisualTreeHelper.GetParent(current))
                if (current is UIElement element)
                    visibilitySubscriptions.Add((element, element.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, OnVisibilityChanged)));
        if (IsEffectivelyVisible(owner)) timer.Start();
    }

    internal void Stop() { requested = false; timer.Stop(); }

    private void OnVisibilityChanged(DependencyObject sender, DependencyProperty property)
    {
        if (requested && !disposed && owner.IsLoaded && IsEffectivelyVisible(owner)) timer.Start();
        else timer.Stop();
    }

    internal static bool IsEffectivelyVisible(FrameworkElement element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed }) return false;
        return true;
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (disposed || !owner.IsLoaded || !IsEffectivelyVisible(owner)) { timer.Stop(); return; }
        tick();
        if (!timer.IsRunning) return;
        if (bar is not null) bar.Value = interpolation.GetFraction();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        Stop();
        foreach (var entry in visibilitySubscriptions) entry.Element.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, entry.Token);
        visibilitySubscriptions.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        OnUnloaded(owner, new RoutedEventArgs());
        timer.Tick -= OnTick;
        owner.Unloaded -= OnUnloaded;
    }
}
