namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

using System.Collections;
using System.Reflection;

[TestClass]
public sealed class HardwareInspectionProcessAcceptanceHostTests
{
    [TestMethod]
    public void DiscoveryIncludesExactProcessAndGate7AcceptanceInventory()
    {
        MethodInfo? discover = typeof(HardwareInspectionProcessAcceptanceHost).GetMethod(
            "DiscoverTests",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(discover);
        var discovered = (IEnumerable)discover.Invoke(null, null)!;
        List<string> names = [];
        foreach (object test in discovered)
        {
            PropertyInfo? name = test.GetType().GetProperty("Name");
            Assert.IsNotNull(name);
            names.Add((string)name.GetValue(test)!);
        }

        Assert.HasCount(70, names);
        Assert.IsTrue(names.Any(name => name.Contains(
            ".HardwareInspectionServiceTests.",
            StringComparison.Ordinal)));
        Assert.IsTrue(names.Any(name => name.Contains(
            ".HardwareInspectionCompositionTests.",
            StringComparison.Ordinal)));
        Assert.AreEqual(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

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
