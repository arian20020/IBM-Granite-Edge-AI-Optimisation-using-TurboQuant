using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Contracts.Tests;

[TestClass]
public sealed class StreamingWhitespaceTests
{
    [TestMethod]
    [DataRow(" ")]
    [DataRow("\n")]
    [DataRow("\r\n\t")]
    public void StreamedWhitespaceRoundTripsWithoutLosingAnswerFormatting(string text)
    {
        TokenEvent token = new(Guid.NewGuid(), Guid.NewGuid(), 0, text);
        byte[] json = OpenVinoProtocolJson.Serialize(token);
        Assert.AreEqual(text, ((TokenEvent)OpenVinoProtocolJson.DeserializeEvent(json)).Text);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void EmptyOrMalformedStreamChunksRemainRejected(string? text)
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            new TokenEvent(Guid.NewGuid(), Guid.NewGuid(), 0, text!).Validate());
    }

    [TestMethod]
    public void UnpairedSurrogateStreamChunkRemainsRejected()
    {
        Assert.ThrowsExactly<OpenVinoProtocolException>(() =>
            new TokenEvent(Guid.NewGuid(), Guid.NewGuid(), 0, new string((char)0xd800, 1)).Validate());
    }
}
