using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ChatModels;

internal enum ChatModelActivationDisposition
{
    Activated = 0,
    Cancelled = 1,
    SourceUnavailable = 2,
    RuntimeUnavailable = 3,
    Failed = 4,
    Committed = 5,
    HistoryRejected = 6,
    ContextExceeded = 7,
}

internal interface IChatModelActivationTarget : IAsyncDisposable
{
    ChatModelRoute Route { get; }

    ValueTask<ChatModelActivationDisposition> ActivateAsync(
        CancellationToken cancellationToken);
}

internal enum ChatModelRegistrationDisposition
{
    Added = 0,
    AlreadyRegistered = 1,
    Conflict = 2,
}

internal enum ChatModelSwitchDisposition
{
    Activated = 0,
    AlreadyActive = 1,
    NotFound = 2,
    NotReady = 3,
    Cancelled = 4,
    SourceUnavailable = 5,
    RuntimeUnavailable = 6,
    ActivationFailed = 7,
    HistoryRejected = 8,
    ContextExceeded = 9,
}

internal sealed record ChatModelSwitchResult(
    string ModelId,
    ChatModelSwitchDisposition Disposition);

internal sealed class ChatModelLibrary : IAsyncDisposable
{
    private readonly object stateSync = new();
    private readonly SemaphoreSlim transitionGate = new(1, 1);
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly Dictionary<string, Registration> registrations =
        new(StringComparer.Ordinal);
    private readonly List<string> registrationOrder = [];
    private string? activeModelId;
    private Task? disposalTask;
    private bool closing;

    internal string? ActiveModelId
    {
        get
        {
            lock (stateSync)
            {
                ObjectDisposedException.ThrowIf(closing, this);
                return activeModelId;
            }
        }
    }

    internal IReadOnlyList<ChatModelSnapshot> Snapshots
    {
        get
        {
            lock (stateSync)
            {
                ObjectDisposedException.ThrowIf(closing, this);
                return registrationOrder
                    .Select(id => CreateSnapshot(registrations[id]))
                    .ToArray();
            }
        }
    }

    internal ChatModelRegistrationDisposition Register(
        ChatModelDescriptor descriptor,
        IChatModelActivationTarget? target)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (stateSync)
        {
            ObjectDisposedException.ThrowIf(closing, this);
            ValidateRegistration(descriptor, target);
            if (registrations.TryGetValue(
                    descriptor.Id,
                    out Registration? existing))
            {
                return existing.Descriptor == descriptor
                    && ReferenceEquals(existing.Target, target)
                        ? ChatModelRegistrationDisposition.AlreadyRegistered
                        : ChatModelRegistrationDisposition.Conflict;
            }
            if (target is not null
                && registrations.Values.Any(existingRegistration =>
                    ReferenceEquals(existingRegistration.Target, target)))
            {
                return ChatModelRegistrationDisposition.Conflict;
            }

            registrations.Add(descriptor.Id, new Registration(descriptor, target));
            registrationOrder.Add(descriptor.Id);
            return ChatModelRegistrationDisposition.Added;
        }
    }

    internal async Task<ChatModelSwitchResult> SwitchAsync(
        string modelId,
        CancellationToken cancellationToken)
    {
        ChatModelDescriptor.ValidateId(modelId);
        CancellationTokenSource linkedCancellation;
        lock (stateSync)
        {
            ObjectDisposedException.ThrowIf(closing, this);
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeCancellation.Token);
        }

        using var linkedCancellationLifetime = linkedCancellation;
        try
        {
            await transitionGate.WaitAsync(linkedCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new ChatModelSwitchResult(
                modelId,
                ChatModelSwitchDisposition.Cancelled);
        }

        try
        {
            Registration? registration;
            lock (stateSync)
            {
                if (closing)
                {
                    return new ChatModelSwitchResult(
                        modelId,
                        ChatModelSwitchDisposition.Cancelled);
                }
                if (!registrations.TryGetValue(modelId, out registration))
                {
                    return new ChatModelSwitchResult(
                        modelId,
                        ChatModelSwitchDisposition.NotFound);
                }
                if (registration.Descriptor.Readiness != ChatModelReadiness.Ready
                    || registration.Target is null)
                {
                    return new ChatModelSwitchResult(
                        modelId,
                        ChatModelSwitchDisposition.NotReady);
                }
                if (string.Equals(activeModelId, modelId, StringComparison.Ordinal))
                {
                    return new ChatModelSwitchResult(
                        modelId,
                        ChatModelSwitchDisposition.AlreadyActive);
                }
            }

            ChatModelActivationDisposition activation;
            try
            {
                activation = await registration.Target
                    .ActivateAsync(linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (linkedCancellation.IsCancellationRequested)
            {
                activation = ChatModelActivationDisposition.Cancelled;
            }
            catch
            {
                activation = ChatModelActivationDisposition.Failed;
            }

            ChatModelSwitchDisposition disposition = Map(activation);
            if (linkedCancellation.IsCancellationRequested && activation != ChatModelActivationDisposition.Committed)
            {
                disposition = ChatModelSwitchDisposition.Cancelled;
            }
            if (disposition == ChatModelSwitchDisposition.Activated)
            {
                lock (stateSync)
                {
                    if (closing)
                    {
                        disposition = ChatModelSwitchDisposition.Cancelled;
                    }
                    else
                    {
                        activeModelId = modelId;
                    }
                }
            }

            return new ChatModelSwitchResult(modelId, disposition);
        }
        finally
        {
            transitionGate.Release();
        }
    }

    internal async Task<bool> RemoveAsync(
        string modelId,
        CancellationToken cancellationToken)
    {
        ChatModelDescriptor.ValidateId(modelId);
        await transitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Registration? removed;
            lock (stateSync)
            {
                ObjectDisposedException.ThrowIf(closing, this);
                if (!registrations.Remove(modelId, out removed))
                {
                    return false;
                }

                registrationOrder.Remove(modelId);
                if (string.Equals(activeModelId, modelId, StringComparison.Ordinal))
                {
                    activeModelId = null;
                }
            }

            if (removed.Target is not null)
            {
                await removed.Target.DisposeAsync().ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            transitionGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (stateSync)
        {
            if (disposalTask is not null)
            {
                return new ValueTask(disposalTask);
            }

            closing = true;
            lifetimeCancellation.Cancel();
            disposalTask = DisposeCoreAsync();
            return new ValueTask(disposalTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await transitionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            IChatModelActivationTarget[] targets;
            lock (stateSync)
            {
                targets = registrations.Values
                    .Select(registration => registration.Target)
                    .OfType<IChatModelActivationTarget>()
                    .ToArray();
                registrations.Clear();
                registrationOrder.Clear();
                activeModelId = null;
            }

            Exception? firstFailure = null;
            foreach (IChatModelActivationTarget target in targets)
            {
                try
                {
                    await target.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                }
            }

            lifetimeCancellation.Dispose();
            if (firstFailure is not null)
            {
                throw firstFailure;
            }
        }
        finally
        {
            transitionGate.Release();
        }
    }

    private ChatModelSnapshot CreateSnapshot(Registration registration) => new(
        registration.Descriptor.Id,
        registration.Descriptor.DisplayName,
        registration.Descriptor.Route,
        registration.Descriptor.FormatLabel,
        registration.Descriptor.RuntimeLabel,
        registration.Descriptor.Readiness,
        string.Equals(
            activeModelId,
            registration.Descriptor.Id,
            StringComparison.Ordinal));

    private static void ValidateRegistration(
        ChatModelDescriptor descriptor,
        IChatModelActivationTarget? target)
    {
        if (descriptor.Readiness == ChatModelReadiness.Ready && target is null)
        {
            throw new ArgumentNullException(
                nameof(target),
                "A ready chat model requires an activation target.");
        }
        if (descriptor.Readiness != ChatModelReadiness.Ready && target is not null)
        {
            throw new ArgumentException(
                "A non-ready chat model cannot expose an activation target.",
                nameof(target));
        }
        if (target is not null && target.Route != descriptor.Route)
        {
            throw new ArgumentException(
                "A chat model activation target must match the model route.",
                nameof(target));
        }
    }

    private static ChatModelSwitchDisposition Map(
        ChatModelActivationDisposition disposition) =>
        disposition switch
        {
            ChatModelActivationDisposition.Activated =>
                ChatModelSwitchDisposition.Activated,
            ChatModelActivationDisposition.Committed =>
                ChatModelSwitchDisposition.Activated,
            ChatModelActivationDisposition.HistoryRejected => ChatModelSwitchDisposition.HistoryRejected,
            ChatModelActivationDisposition.ContextExceeded => ChatModelSwitchDisposition.ContextExceeded,
            ChatModelActivationDisposition.Cancelled =>
                ChatModelSwitchDisposition.Cancelled,
            ChatModelActivationDisposition.SourceUnavailable =>
                ChatModelSwitchDisposition.SourceUnavailable,
            ChatModelActivationDisposition.RuntimeUnavailable =>
                ChatModelSwitchDisposition.RuntimeUnavailable,
            ChatModelActivationDisposition.Failed =>
                ChatModelSwitchDisposition.ActivationFailed,
            _ => ChatModelSwitchDisposition.ActivationFailed,
        };

    private sealed record Registration(
        ChatModelDescriptor Descriptor,
        IChatModelActivationTarget? Target);
}
