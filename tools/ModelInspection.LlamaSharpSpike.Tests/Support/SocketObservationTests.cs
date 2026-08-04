using GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies TCP observations are filtered to the exact child PID and relevant
/// socket states without invoking LLamaSharp.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class SocketObservationTests
{
    [TestMethod]
    public void ParseNetstatOutput_ReturnsListeningAndEstablishedForTargetPidOnly()
    {
        const string output = """
          Proto  Local Address          Foreign Address        State           PID
          TCP    0.0.0.0:5000           0.0.0.0:0              LISTENING       1234
          TCP    127.0.0.1:5001         127.0.0.1:6000         ESTABLISHED     1234
          TCP    127.0.0.1:5002         127.0.0.1:6001         TIME_WAIT       1234
          TCP    0.0.0.0:7000           0.0.0.0:0              LISTENING       9999
          UDP    0.0.0.0:5353           *:*                                    1234
        """;

        IReadOnlyList<TcpSocketObservation> result =
            SocketObservation.ParseNetstatOutput(output, 1234);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("LISTENING", result[0].State);
        Assert.AreEqual("0.0.0.0:5000", result[0].LocalEndpoint);
        Assert.AreEqual("ESTABLISHED", result[1].State);
        Assert.AreEqual("127.0.0.1:6000", result[1].RemoteEndpoint);
        Assert.IsTrue(result.All(entry => entry.ProcessId == 1234));
    }

    [TestMethod]
    public void ParseNetstatOutput_WithIpv6Endpoints_PreservesEndpointText()
    {
        const string output = """
          TCP    [::]:5000              [::]:0                 LISTENING       42
          TCP    [::1]:5001             [::1]:6000             ESTABLISHED     42
        """;

        IReadOnlyList<TcpSocketObservation> result =
            SocketObservation.ParseNetstatOutput(output, 42);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("[::]:5000", result[0].LocalEndpoint);
        Assert.AreEqual("[::1]:6000", result[1].RemoteEndpoint);
    }

    [TestMethod]
    public void ParseNetstatOutput_WithHeadersMalformedRowsAndOtherPid_ReturnsEmpty()
    {
        const string output = """
          Active Connections
          Proto Local Address Foreign Address State PID
          not a valid row
          TCP 0.0.0.0:5000 0.0.0.0:0 LISTENING not-a-pid
          TCP 0.0.0.0:5001 0.0.0.0:0 LISTENING 99
        """;

        Assert.AreEqual(
            0,
            SocketObservation.ParseNetstatOutput(output, 42).Count);
    }

    [TestMethod]
    public void ParseNetstatOutput_WithNullOutput_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => SocketObservation.ParseNetstatOutput(null!, 42));
    }

    [TestMethod]
    public void ParseNetstatOutput_WithNonPositivePid_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => SocketObservation.ParseNetstatOutput(string.Empty, 0));
    }

    [TestMethod]
    public void Constructor_WithNonPositiveInterval_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new SocketObservation(TimeSpan.Zero));
    }
}
