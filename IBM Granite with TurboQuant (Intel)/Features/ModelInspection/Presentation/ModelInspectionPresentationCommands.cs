using System;
using System.Windows.Input;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class ModelInspectionPresentationCommands
{
    internal ModelInspectionPresentationCommands(
        ICommand cancel,
        ICommand retry,
        ICommand chooseAnother,
        ICommand? checkHardware = null)
    {
        Cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
        Retry = retry ?? throw new ArgumentNullException(nameof(retry));
        ChooseAnother = chooseAnother ??
            throw new ArgumentNullException(nameof(chooseAnother));
        CheckHardware = checkHardware;
    }

    internal ICommand Cancel { get; }

    internal ICommand Retry { get; }

    internal ICommand ChooseAnother { get; }

    internal ICommand? CheckHardware { get; }
}
