using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionHardwareActionTests
{
    [TestMethod]
    public async Task CheckHardware_RequiresRegisteredRouteAndEligibleCurrentResult()
    {
        var service = new QueueInspectionService(
            Completed(ModelInspectionOutcome.Ready));
        var viewModel = new ModelInspectionViewModel(
            service,
            PresentationTestData.CreateRequest());

        viewModel.SetHardwareRouteAvailable(true);
        Assert.IsFalse(viewModel.CheckHardwareCommand.CanExecute(null));

        await viewModel.StartAsync();

        Assert.IsTrue(viewModel.CheckHardwareCommand.CanExecute(null));
        ModelInspectionRenderKey enabledKey = viewModel.Snapshot.RenderKey;
        viewModel.SetHardwareRouteAvailable(false);
        Assert.IsFalse(viewModel.CheckHardwareCommand.CanExecute(null));
        Assert.IsTrue(
            viewModel.Snapshot.RenderKey.PresentationRevision >
            enabledKey.PresentationRevision);
    }

    [TestMethod]
    public async Task CheckHardware_RaisesOnlyAPathFreeCurrentHandoff()
    {
        var service = new QueueInspectionService(
            Completed(ModelInspectionOutcome.ReadyWithWarnings));
        var viewModel = new ModelInspectionViewModel(
            service,
            PresentationTestData.CreateRequest());
        ModelInspectionHandoff? requested = null;
        ModelInspectionHandoff? repeated = null;
        int eventCount = 0;
        viewModel.HardwareInspectionRequested += (_, args) =>
        {
            eventCount++;
            if (requested is null)
            {
                requested = args.Handoff;
            }
            else
            {
                repeated = args.Handoff;
            }
        };
        viewModel.SetHardwareRouteAvailable(true);
        await viewModel.StartAsync();

        viewModel.CheckHardwareCommand.Execute(null);
        viewModel.CheckHardwareCommand.Execute(null);

        Assert.AreEqual(2, eventCount);
        Assert.IsNotNull(requested);
        Assert.AreSame(requested, repeated);
        Assert.AreEqual(
            viewModel.Snapshot.ModelInspectionRunId,
            requested.ModelInspectionRunId);
        Assert.AreEqual(
            ModelInspectionOutcome.ReadyWithWarnings,
            requested.Outcome);
        string propertyNames = string.Join(
            ",",
            typeof(ModelInspectionHandoff)
                .GetProperties(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Select(property => property.Name));
        StringAssert.DoesNotContain(
            propertyNames,
            "Path",
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            propertyNames,
            "FileName",
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            propertyNames,
            "Request",
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            propertyNames,
            "Result",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RetryOrIneligibleResult_CannotRaiseAStaleHandoff()
    {
        var service = new QueueInspectionService(
            Completed(ModelInspectionOutcome.Ready),
            Completed(ModelInspectionOutcome.Unsupported));
        var viewModel = new ModelInspectionViewModel(
            service,
            PresentationTestData.CreateRequest());
        int eventCount = 0;
        viewModel.HardwareInspectionRequested += (_, _) => eventCount++;
        viewModel.SetHardwareRouteAvailable(true);
        await viewModel.StartAsync();
        Guid firstRun = viewModel.Snapshot.ModelInspectionRunId;

        await viewModel.StartAsync();

        Assert.AreNotEqual(firstRun, viewModel.Snapshot.ModelInspectionRunId);
        Assert.IsFalse(viewModel.CheckHardwareCommand.CanExecute(null));
        viewModel.CheckHardwareCommand.Execute(null);
        Assert.AreEqual(0, eventCount);
    }

    private static ModelInspectionExecutionResult Completed(
        ModelInspectionOutcome outcome) =>
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(outcome));

    private sealed class QueueInspectionService : IModelInspectionService
    {
        private readonly Queue<ModelInspectionExecutionResult> results;

        internal QueueInspectionService(
            params ModelInspectionExecutionResult[] results)
        {
            this.results = new Queue<ModelInspectionExecutionResult>(results);
        }

        public Task<ModelInspectionExecutionResult> InspectAsync(
            ModelInspectionRequest request,
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(results.Dequeue());
    }
}
