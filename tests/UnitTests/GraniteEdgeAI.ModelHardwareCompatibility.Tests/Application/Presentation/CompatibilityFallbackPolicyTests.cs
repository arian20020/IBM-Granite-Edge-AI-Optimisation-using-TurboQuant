using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Presentation;

[TestClass]
public sealed class CompatibilityFallbackPolicyTests
{
    [TestMethod]
    public void NotEstablishedProjectionIsFailClosed()
    {
        CompatibilityScreenModel result =
            CompatibilityFallbackPolicy.NotEstablished();

        Assert.AreEqual(CompatibilityScreenState.NotEstablished, result.State);
        Assert.IsFalse(result.ContinueEnabled);
        Assert.IsFalse(result.UseCurrentModelAvailable);
    }

    [TestMethod]
    public void NotEstablishedProjectionNamesOneBlockingUnexpectedFailure()
    {
        CompatibilityScreenModel result =
            CompatibilityFallbackPolicy.NotEstablished();

        Assert.AreEqual(1, result.Findings.Count);
        CompatibilityFindingView finding = result.Findings[0];
        Assert.AreEqual(CompatibilityFindingCode.UnexpectedFailure, finding.Code);
        Assert.AreEqual(FindingSeverity.Blocking, finding.Severity);
    }
}
