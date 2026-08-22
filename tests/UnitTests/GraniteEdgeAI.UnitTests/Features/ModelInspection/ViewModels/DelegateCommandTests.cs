using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class DelegateCommandTests
{
    [TestMethod]
    public void Execute_DisabledCommand_DoesNotInvokeAction()
    {
        bool enabled = false;
        object? receivedParameter = null;
        DelegateCommand command = new(
            parameter => receivedParameter = parameter,
            _ => enabled);

        command.Execute("ignored");
        Assert.IsNull(receivedParameter);

        enabled = true;
        object expectedParameter = new();
        command.Execute(expectedParameter);

        Assert.AreSame(expectedParameter, receivedParameter);
    }

    [TestMethod]
    public void RaiseCanExecuteChanged_NotifiesCommandConsumer()
    {
        DelegateCommand command = new(_ => { });
        int notificationCount = 0;
        command.CanExecuteChanged += (_, _) => notificationCount++;

        command.RaiseCanExecuteChanged();

        Assert.AreEqual(1, notificationCount);
    }
}
