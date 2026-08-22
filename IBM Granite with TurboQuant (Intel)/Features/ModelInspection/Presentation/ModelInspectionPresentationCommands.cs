using System;
using System.Windows.Input;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class ModelInspectionPresentationCommands
{
    internal ModelInspectionPresentationCommands(
        ICommand cancel,
        ICommand retry,
        ICommand chooseAnother)
        : this(
            cancel,
            retry,
            chooseAnother,
            chooseAnother,
            supportsOpenChat: false)
    {
    }

    internal ModelInspectionPresentationCommands(
        ICommand cancel,
        ICommand retry,
        ICommand chooseAnother,
        ICommand openChat)
        : this(cancel, retry, chooseAnother, openChat, supportsOpenChat: true)
    {
    }

    private ModelInspectionPresentationCommands(
        ICommand cancel,
        ICommand retry,
        ICommand chooseAnother,
        ICommand openChat,
        bool supportsOpenChat)
    {
        Cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
        Retry = retry ?? throw new ArgumentNullException(nameof(retry));
        ChooseAnother = chooseAnother ??
            throw new ArgumentNullException(nameof(chooseAnother));
        OpenChat = openChat ?? throw new ArgumentNullException(nameof(openChat));
        SupportsOpenChat = supportsOpenChat;
    }

    internal ICommand Cancel { get; }

    internal ICommand Retry { get; }

    internal ICommand ChooseAnother { get; }

    internal ICommand OpenChat { get; }

    internal bool SupportsOpenChat { get; }
}
