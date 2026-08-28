using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class CompatibilityFallbackPolicyTests
{
    [TestMethod]
    public void FaultUsesOneFailClosedProjection()
    {
        CompatibilityScreenModel result = CompatibilityFallbackPolicy.Execute(
            () => throw new InvalidOperationException("adapter failed"),
            CompatibilityFallbackPolicy.NotEstablished);

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
        Assert.IsFalse(result.ContinueEnabled);
        Assert.IsFalse(result.UseCurrentModelAvailable);
        Assert.AreEqual(1, result.Findings.Count);
        CompatibilityFindingView finding = result.Findings[0];
        Assert.AreEqual(CompatibilityFindingCode.UnexpectedFailure, finding.Code);
        Assert.AreEqual(FindingSeverity.Blocking, finding.Severity);
    }

    [TestMethod]
    public void CancellationIsNeverConvertedIntoFallback()
    {
        bool fallbackInvoked = false;

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            CompatibilityFallbackPolicy.Execute(
                () => throw new OperationCanceledException(),
                () =>
                {
                    fallbackInvoked = true;
                    return 0;
                }));

        Assert.IsFalse(fallbackInvoked);
    }
}
