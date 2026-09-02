using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TurboVec.PdfExtractionSpike.Tests;

[TestClass]
public sealed class ProgramContractTests
{
    [TestMethod]
    public void UnknownSwitchReturnsClosedPathFreeJson()
    {
        var writer = new StringWriter(); var errors = new StringWriter();
        var oldOut = Console.Out; var oldError = Console.Error;
        try
        {
            Console.SetOut(writer); Console.SetError(errors);
            var exit = TurboVec.PdfExtractionSpike.Program.Main(["--unknown", "secret", "--expected-sha256", new string('0', 64), "--max-pages", "1", "--max-scalars", "1"]);
            Assert.AreEqual(2, exit);
            using var json = JsonDocument.Parse(writer.ToString());
            Assert.AreEqual("invalid_arguments", json.RootElement.GetProperty("failure_code").GetString());
            Assert.IsFalse(errors.ToString().Contains("secret", StringComparison.Ordinal));
        }
        finally { Console.SetOut(oldOut); Console.SetError(oldError); }
    }
}
