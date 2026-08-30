using GraniteEdgeAI.Features.ApplicationFaults;

namespace GraniteEdgeAI.UnitTests.Features.ApplicationFaults;

[TestClass]
public sealed class ApplicationFaultReporterTests
{
    [TestMethod]
    public void ReporterRetainsOnlyBoundedStableFaultFacts()
    {
        var reporter = new BoundedApplicationFaultReporter(capacity: 2);

        reporter.Report(ApplicationFault.FromException(
            ApplicationFaultCode.CompatibilityEvaluationUnexpected,
            new InvalidOperationException(@"private C:\Users\person\model.gguf")));
        reporter.Report(ApplicationFault.FromException(
            ApplicationFaultCode.GgufChatOperationUnexpected,
            new ArgumentException("private prompt")));
        reporter.Report(ApplicationFault.FromException(
            ApplicationFaultCode.GgufChatRetirementUnexpected,
            new NullReferenceException("private provider output")));

        IReadOnlyList<ApplicationFault> faults = reporter.Capture();
        Assert.HasCount(2, faults);
        Assert.AreEqual(ApplicationFaultCode.GgufChatOperationUnexpected, faults[0].Code);
        Assert.AreEqual(ApplicationFaultClassification.Argument, faults[0].Classification);
        Assert.AreEqual(ApplicationFaultCode.GgufChatRetirementUnexpected, faults[1].Code);
        Assert.AreEqual(ApplicationFaultClassification.NullReference, faults[1].Classification);
        Assert.IsFalse(string.Join('|', faults).Contains("private", StringComparison.Ordinal));
        Assert.IsFalse(typeof(ApplicationFault).GetProperties().Any(property =>
            typeof(Exception).IsAssignableFrom(property.PropertyType)
            || property.PropertyType == typeof(string)));
    }
}
