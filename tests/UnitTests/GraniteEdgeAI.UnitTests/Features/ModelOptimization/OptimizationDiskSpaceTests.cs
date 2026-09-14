using System;
using System.IO;
using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Storage;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationDiskSpaceTests
{
    [TestMethod]
    public void ProductionCandidateIncludesGgufSourceForPersistentAndCacheOnlyPlans()
    {
        var generator = typeof(OptimizationExecutionPlan).Assembly.GetType(
            "GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.CrossRouteCandidateGenerator")!;
        var compute = generator.GetMethod("ProductionWorkingDiskBytes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        ulong Calculate(bool gguf, ulong output, ulong source) =>
            (ulong)compute.Invoke(null, new object[] { gguf, output, source })!;
        Assert.AreEqual(9_000UL, Calculate(true, 2_000, 7_000));
        Assert.AreEqual(7_000UL, Calculate(true, 0, 7_000));
        Assert.AreEqual(2_000UL, Calculate(false, 2_000, 7_000));
    }

    [TestMethod]
    public void GgufPreflightConsumesCandidateTotalWithoutAddingSourceAgain()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "Features", "ModelOptimization", "Execution", "Gguf",
            "GgufOptimizationJourneyInfrastructure.cs"));
        StringAssert.Contains(source, "plan.Candidate.Metrics.DiskObligationBytes");
        Assert.IsFalse(source.Contains("OptimizationDiskSpace.RequiredForGguf(", StringComparison.Ordinal));
    }
    [TestMethod]
    public void SourceAndOutputCoexistDuringGgufPreparation()
    {
        Assert.AreEqual(9_000_000_000UL, OptimizationDiskSpace.RequiredForGguf(7_000_000_000, 2_000_000_000));
        Assert.ThrowsExactly<OverflowException>(() => OptimizationDiskSpace.RequiredForGguf(ulong.MaxValue, 1));
    }

    [TestMethod]
    public void OnlyWindowsDiskFullCodesAreClassifiedAsDiskFull()
    {
        Assert.IsTrue(OptimizationDiskSpace.IsDiskFull(new IOException("private", unchecked((int)0x80070070))));
        Assert.IsTrue(OptimizationDiskSpace.IsDiskFull(new IOException("private", unchecked((int)0x80070027))));
        Assert.IsFalse(OptimizationDiskSpace.IsDiskFull(new IOException("not enough space", unchecked((int)0x80070005))));
    }

    [TestMethod]
    public void InsufficientVolumeThrowsMeasuredShortageWithoutWritingFiles()
    {
        var failure = Assert.ThrowsExactly<OptimizationDiskSpaceException>(() =>
            OptimizationDiskSpace.Require(Path.GetTempPath(), ulong.MaxValue));
        Assert.AreEqual(ulong.MaxValue, failure.Requirement.RequiredBytes);
        Assert.IsTrue(failure.Requirement.AdditionalBytes > 0);
        Assert.AreEqual(failure.Requirement.RequiredBytes - failure.Requirement.AvailableBytes,
            failure.Requirement.AdditionalBytes);
    }

    [TestMethod]
    public void FailurePresentationPreservesCodeAndReportsDiskRequirements()
    {
        var configuration = OptimizationFixtureCatalog.All.First().Presentation.Configuration;
        var state = OptimizationPresentationFactory.Failed(
            OptimizationPreferenceSelection.Automatic(), configuration,
            supportCode: OptimizationSupportCode.InsufficientDiskSpace,
            diskSpaceRequirement: new(9_000_000_001, 3_000_000_000));
        Assert.AreEqual("More disk space is needed", state.Title);
        Assert.AreEqual(OptimizationSupportCode.InsufficientDiskSpace, state.SupportCode);
        StringAssert.Contains(state.Summary, "9.001");
        StringAssert.Contains(state.Summary, "6.001");
        StringAssert.Contains(state.Summary, "on the optimisation drive");
        Assert.IsTrue(state.Actions.Any(action => action.Text == "Check again"));
    }

    [TestMethod]
    public void UnknownDiskMeasurementIsNotInventedAndOtherCodesArePreserved()
    {
        var configuration = OptimizationFixtureCatalog.All.First().Presentation.Configuration;
        var disk = OptimizationPresentationFactory.Failed(
            OptimizationPreferenceSelection.Automatic(), configuration,
            supportCode: OptimizationSupportCode.InsufficientDiskSpace);
        Assert.IsFalse(disk.Summary.Contains("GB", StringComparison.Ordinal));
        var failed = OptimizationPresentationFactory.Failed(
            OptimizationPreferenceSelection.Automatic(), configuration,
            supportCode: OptimizationSupportCode.SmokeTestFailed);
        Assert.AreEqual(OptimizationSupportCode.SmokeTestFailed, failed.SupportCode);
        Assert.AreEqual("Optimisation could not finish", failed.Title);
        Assert.IsTrue(failed.Actions.Any(action => action.Text == "Try again"));
    }
}
