#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Runtime;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using DebugFixturePreset = GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets.ModelInspectionFixturePreset;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

internal sealed class ModelInspectionFixtureHostActivation : IDisposable
{
    private ModelInspectionFixtureSession? session;
    private ModelInspectionFixtureHostPage? destination;
    private readonly Action<ModelInspectionFixtureHostPage>?
        chooseAnotherRequested;

    internal ModelInspectionFixtureHostActivation(
        ValidatedModelInspectionFixtureInput input,
        ModelInspectionFixtureSession session,
        bool startInspectionOnLoaded,
        DebugFixturePreset? preset = null,
        Action<ModelInspectionFixtureHostPage>? chooseAnotherRequested = null)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        StartInspectionOnLoaded = startInspectionOnLoaded;
        Preset = preset ?? DebugFixturePreset.Canonical;
        Evidence = session.Evidence;
        this.chooseAnotherRequested = chooseAnotherRequested;
    }

    internal ValidatedModelInspectionFixtureInput Input { get; }

    internal bool StartInspectionOnLoaded { get; }

    internal DebugFixturePreset Preset { get; }

    internal ModelInspectionFixtureSessionEvidence Evidence { get; }

    internal ModelInspectionFixtureSession ClaimSession(
        ModelInspectionFixtureHostPage owner)
    {
        RegisterDestination(owner);
        return Interlocked.Exchange(ref session, null) ??
        throw new InvalidOperationException(
            "The fixture host activation was already claimed.");
    }

    internal void RegisterDestination(ModelInspectionFixtureHostPage owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ModelInspectionFixtureHostPage? existing = Interlocked.CompareExchange(
            ref destination,
            owner,
            null);
        if (existing is not null && !ReferenceEquals(existing, owner))
        {
            throw new InvalidOperationException(
                "The fixture host activation already owns another destination.");
        }
    }

    internal bool OwnsDestination(ModelInspectionFixtureHostPage owner) =>
        ReferenceEquals(Volatile.Read(ref destination), owner);

    internal void RequestChooseAnother(ModelInspectionFixtureHostPage owner)
    {
        if (OwnsDestination(owner))
        {
            chooseAnotherRequested?.Invoke(owner);
        }
    }

    public void Dispose() => Interlocked.Exchange(ref session, null)?.Dispose();
}

internal sealed class ModelInspectionFixtureHostLifetime
{
    private readonly object gate = new();
    private Action? detach;
    private Action? retirePage;
    private Action? retireSession;
    private Action? clear;
    private bool retirementStarted;
    private bool retirementCompleted;
    private int retirementOwnerThreadId;

    internal ModelInspectionFixtureHostLifetime(
        Action detach,
        Action retirePage,
        Action retireSession,
        Action clear)
    {
        this.detach = detach ?? throw new ArgumentNullException(nameof(detach));
        this.retirePage = retirePage ??
            throw new ArgumentNullException(nameof(retirePage));
        this.retireSession = retireSession ??
            throw new ArgumentNullException(nameof(retireSession));
        this.clear = clear ?? throw new ArgumentNullException(nameof(clear));
    }

    internal bool Retire()
    {
        lock (gate)
        {
            if (retirementCompleted)
            {
                return false;
            }

            if (retirementStarted)
            {
                if (retirementOwnerThreadId ==
                    Environment.CurrentManagedThreadId)
                {
                    return false;
                }

                while (!retirementCompleted)
                {
                    Monitor.Wait(gate);
                }

                return false;
            }

            retirementStarted = true;
            retirementOwnerThreadId = Environment.CurrentManagedThreadId;
        }

        Exception? first = null;
        Attempt(Interlocked.Exchange(ref detach, null), ref first);
        Attempt(Interlocked.Exchange(ref retirePage, null), ref first);
        Attempt(Interlocked.Exchange(ref retireSession, null), ref first);
        Attempt(Interlocked.Exchange(ref clear, null), ref first);
        lock (gate)
        {
            retirementOwnerThreadId = 0;
            retirementCompleted = true;
            Monitor.PulseAll(gate);
        }

        if (first is not null)
        {
            ExceptionDispatchInfo.Capture(first).Throw();
        }

        return true;
    }

    internal bool IsRetirementCompleted
    {
        get
        {
            lock (gate)
            {
                return retirementCompleted;
            }
        }
    }

    private static void Attempt(Action? action, ref Exception? first)
    {
        if (action is null)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception error)
        {
            first ??= error;
        }
    }
}

public sealed partial class ModelInspectionFixtureHostPage : Page
{
    private ModelInspectionFixtureHostLifetime? lifetime;
    private ModelInspectionFixtureSession? session;
    private ModelInspectionPage? modelInspectionPage;
    private object? activationIdentity;
    private DebugFixturePreset? preset;
    private int activationStarted;

    public ModelInspectionFixtureHostPage()
    {
        InitializeComponent();
        Unloaded += ModelInspectionFixtureHostPage_Unloaded;
    }

    internal ModelInspectionFixtureSession Session => session ??
        throw new InvalidOperationException(
            "The fixture host no longer owns a session.");

    internal ModelInspectionPage? ModelInspectionPage => modelInspectionPage;

    internal DebugFixturePreset? Preset => preset;

    internal bool IsActivatedWith(object activation) =>
        ReferenceEquals(activationIdentity, activation) &&
        modelInspectionPage is not null;

    protected override void OnNavigatedTo(NavigationEventArgs eventArguments)
    {
        base.OnNavigatedTo(eventArguments);
        if (eventArguments.Parameter is not ModelInspectionFixtureHostActivation
            activation)
        {
            throw new ArgumentException(
                "The fixture host requires its one-shot activation.",
                nameof(eventArguments));
        }

        Activate(activation);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs eventArguments)
    {
        base.OnNavigatedFrom(eventArguments);
        RetireHost();
    }

    internal void ActivateForTesting(
        ModelInspectionFixtureHostActivation activation) => Activate(activation);

    internal bool RetireForTesting() => RetireHost();

    internal bool RaiseUnloadedForTesting() => RetireHost();

    internal bool RaiseNavigatedFromForTesting() => RetireHost();

    private void Activate(ModelInspectionFixtureHostActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        if (Interlocked.CompareExchange(ref activationStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "The fixture host can be activated only once.");
        }

        ModelInspectionFixtureSession claimed = activation.ClaimSession(this);
        ModelInspectionPage? createdPage = null;
        session = claimed;
        activationIdentity = activation;
        preset = activation.Preset;
        EventHandler? chooseAnotherHandler = null;
        lifetime = new ModelInspectionFixtureHostLifetime(
            detach: () =>
            {
                if (createdPage is not null &&
                    chooseAnotherHandler is not null)
                {
                    createdPage.ChooseAnotherModelRequested -=
                        chooseAnotherHandler;
                }

                chooseAnotherHandler = null;
            },
            retirePage: () => createdPage?.RetireForFixture(),
            retireSession: claimed.Dispose,
            clear: () =>
            {
                FixturePageHost.Content = null;
                modelInspectionPage = null;
                session = null;
                activationIdentity = null;
                preset = null;
                createdPage = null;
            });

        try
        {
            createdPage = ModelInspectionPage.CreateForFixture(
                claimed,
                activation.StartInspectionOnLoaded);
            chooseAnotherHandler = (_, _) =>
                activation.RequestChooseAnother(this);
            createdPage.ChooseAnotherModelRequested += chooseAnotherHandler;
            modelInspectionPage = createdPage;
            FixturePageHost.Content = createdPage;
        }
        catch (Exception error)
        {
            try
            {
                RetireHost();
            }
            catch
            {
            }

            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private bool RetireHost()
    {
        ModelInspectionFixtureHostLifetime? owned = Volatile.Read(ref lifetime);
        if (owned is null)
        {
            return false;
        }

        try
        {
            return owned.Retire();
        }
        finally
        {
            if (owned.IsRetirementCompleted)
            {
                Interlocked.CompareExchange(ref lifetime, null, owned);
            }
        }
    }

    private void ModelInspectionFixtureHostPage_Unloaded(
        object sender,
        RoutedEventArgs eventArguments) => RetireHost();
}
#endif
