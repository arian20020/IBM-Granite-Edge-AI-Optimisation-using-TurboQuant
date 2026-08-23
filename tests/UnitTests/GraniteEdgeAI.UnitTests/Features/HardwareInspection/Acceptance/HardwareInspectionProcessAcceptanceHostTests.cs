namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

[TestClass]
public sealed class HardwareInspectionProcessAcceptanceHostTests
{
    [TestMethod]
    public void ParsesPlainDesktopLaunchCommandLine()
    {
        string token = new('a', 32);
        string[] commandLine =
        [
            @"C:\Program Files\WindowsApps\GraniteEdgeAI.UnitTests.exe",
            "--hardware-inspection-process-acceptance",
            "--result-token",
            token,
        ];

        bool parsed = HardwareInspectionProcessAcceptanceHost.TryParseActivation(
            commandLine,
            out string actualToken);

        Assert.IsTrue(parsed);
        Assert.AreEqual(token, actualToken);
    }
}
