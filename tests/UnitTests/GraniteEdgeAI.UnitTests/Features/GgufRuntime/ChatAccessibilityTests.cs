using GraniteEdgeAI.Features.GgufRuntime;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class ChatAccessibilityTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void PrimaryActionsAndTranscriptExposeAccessibleNames()
    {
        var page = new ChatPage();
        Button newChat = Assert.IsInstanceOfType<Button>(page.FindName("NewChatButton"));
        ListView transcript = Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList"));

        Assert.AreEqual("Start a new chat", AutomationProperties.GetName(newChat));
        Assert.AreEqual("Conversation messages", AutomationProperties.GetName(transcript));
        Assert.AreEqual("Polite", AutomationProperties.GetLiveSetting(transcript).ToString());
    }
}
