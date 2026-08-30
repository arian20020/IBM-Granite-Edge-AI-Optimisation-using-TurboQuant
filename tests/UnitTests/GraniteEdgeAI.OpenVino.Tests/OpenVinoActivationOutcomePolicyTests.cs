using System.Reflection;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
[TestCategory("OpenVinoRoute")]
public sealed class OpenVinoActivationOutcomePolicyTests
{
    [TestMethod]
    [DataRow(OpenVinoRouteInspectionOutcome.ConversionRequired, "", null)]
    [DataRow(OpenVinoRouteInspectionOutcome.IncompletePackage,
        "package_inconsistent_resource", OpenVinoSupportCode.PackageInconsistentResource)]
    [DataRow(OpenVinoRouteInspectionOutcome.Unsupported,
        "model_architecture_unsupported", OpenVinoSupportCode.ModelArchitectureUnsupported)]
    [DataRow(OpenVinoRouteInspectionOutcome.DependencyUnavailable,
        "runtime_dependency_missing", OpenVinoSupportCode.RuntimeDependencyMissing)]
    [DataRow(OpenVinoRouteInspectionOutcome.Cancelled,
        "operation_cancelled", OpenVinoSupportCode.OperationCancelled)]
    [DataRow(OpenVinoRouteInspectionOutcome.TimedOut,
        "runtime_timed_out", OpenVinoSupportCode.RuntimeTimedOut)]
    [DataRow(OpenVinoRouteInspectionOutcome.InvalidEvidence,
        "runtime_protocol_failed", OpenVinoSupportCode.RuntimeProtocolFailed)]
    [DataRow(OpenVinoRouteInspectionOutcome.StaleEvidence,
        "package_changed", OpenVinoSupportCode.PackageChanged)]
    public void HeadlessActivationPolicyPreservesEveryNonSuccessOutcome(
        OpenVinoRouteInspectionOutcome outcome,
        string protocolCode,
        OpenVinoSupportCode? expectedSupportCode)
    {
        object policyResult = InvokePolicy(
            "FromInspection",
            Inspection(outcome, protocolCode));

        Assert.AreEqual(outcome, ReadProperty<OpenVinoRouteInspectionOutcome>(
            policyResult,
            "Outcome"));
        Assert.AreEqual(expectedSupportCode, ReadProperty<OpenVinoSupportCode?>(
            policyResult,
            "SupportCode"));
        Assert.IsFalse(ReadProperty<bool>(policyResult, "IsActivated"));
    }

    [TestMethod]
    [DataRow(OpenVinoRouteInspectionOutcome.Cancelled,
        "operation_cancelled", OpenVinoSupportCode.OperationCancelled)]
    [DataRow(OpenVinoRouteInspectionOutcome.TimedOut,
        "runtime_timed_out", OpenVinoSupportCode.RuntimeTimedOut)]
    public void PostConversionPolicyPreservesCancellationAndTimeout(
        OpenVinoRouteInspectionOutcome outcome,
        string protocolCode,
        OpenVinoSupportCode expectedSupportCode)
    {
        object actual = InvokePolicy(
            "GetConversionFailureCode",
            Inspection(outcome, protocolCode));

        Assert.AreEqual(expectedSupportCode, (OpenVinoSupportCode)actual);
    }

    [TestMethod]
    public void LivePageUsesTypedActivationAndPostConversionPolicies()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelInspection",
            "ModelInspectionPage.OpenVino.cs"));

        StringAssert.Contains(source, "OpenVinoChatActivationResult");
        StringAssert.Contains(source, "OpenVinoActivationOutcomePolicy.FromInspection");
        StringAssert.Contains(source,
            "OpenVinoActivationOutcomePolicy.GetConversionFailureCode");
    }

    private static OpenVinoRouteInspectionResult Inspection(
        OpenVinoRouteInspectionOutcome outcome,
        string protocolCode) => new(
        outcome,
        HandoffLease: null,
        string.IsNullOrEmpty(protocolCode)
            ? null
            : new PromptFailure(protocolCode, "private", "private"),
        Configuration: null);

    private static object InvokePolicy(string methodName, object argument)
    {
        Type policy = typeof(OpenVinoConversionService).Assembly.GetType(
            "GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoActivationOutcomePolicy",
            throwOnError: true)!;
        MethodInfo method = policy.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new AssertFailedException($"Policy method {methodName} is absent.");
        return method.Invoke(null, [argument]) ??
            throw new AssertFailedException($"Policy method {methodName} returned null.");
    }

    private static T ReadProperty<T>(object value, string propertyName)
    {
        PropertyInfo property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            throw new AssertFailedException($"Result property {propertyName} is absent.");
        return (T)property.GetValue(value)!;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }
}
