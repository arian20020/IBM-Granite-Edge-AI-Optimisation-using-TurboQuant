using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace GraniteEdgeAI.EndToEndTests.Infrastructure;

internal sealed record TrxStageSummary(
    int Discovered,
    int Executed,
    int Passed,
    int Failed,
    int Skipped)
{
    internal static TrxStageSummary Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using XmlReader reader = XmlReader.Create(Path.GetFullPath(path), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 4 * 1024 * 1024,
            });
            XDocument document = XDocument.Load(reader, LoadOptions.None);
            XElement counters = document.Descendants().Single(element => element.Name.LocalName == "Counters");
            int total = Required(counters, "total");
            int trxExecuted = Required(counters, "executed");
            int passed = Required(counters, "passed");
            int failed = Required(counters, "failed");
            int skipped = Required(counters, "notExecuted");
            if (total <= 0 || trxExecuted < 0 || passed < 0 || failed < 0 || skipped < 0
                || trxExecuted != passed + failed || total != trxExecuted + skipped)
            {
                throw new InvalidDataException("TRX counters have zero discovery or inconsistent arithmetic.");
            }
            return new TrxStageSummary(total, passed + failed + skipped, passed, failed, skipped);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Unable to read a strict TRX stage summary.", error);
        }
    }

    private static int Required(XElement counters, string name)
    {
        XAttribute? attribute = counters.Attribute(name);
        return attribute is not null
            ? int.Parse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture)
            : throw new InvalidDataException($"TRX counter '{name}' is missing.");
    }
}
