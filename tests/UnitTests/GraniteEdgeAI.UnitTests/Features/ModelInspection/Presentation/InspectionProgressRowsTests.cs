using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
public sealed class InspectionProgressRowsTests
{
    private static readonly string[] ExpectedStageTitles =
    [
        "Check model package",
        "Read model configuration",
        "Validate tokenizer and chat setup",
        "Validate model structure",
        "Confirm core runtime compatibility"
    ];

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_CreatesFiveWaitingRowsAtZeroOfFive()
    {
        InspectionProgressRows rows = new();

        Assert.HasCount(5, rows.Items);
        CollectionAssert.AreEqual(
            ExpectedStageTitles,
            rows.Items.Select(item => item.Title).ToArray());
        Assert.AreEqual("0 of 5 checks complete", rows.ProgressSummary);
        Assert.IsTrue(rows.Items.All(item =>
            item.Status == InspectionContentStatus.Waiting &&
            item.StatusText == "Waiting" &&
            !item.IsActive &&
            item.StageFraction is null &&
            item.Detail.Length == 0 &&
            item.DetailVisibility == Visibility.Collapsed));
        Assert.IsTrue(rows.Items.Take(4).All(item => item.ShowConnector));
        Assert.IsFalse(rows.Items[4].ShowConnector);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<InspectionContentItemPresentation>)rows.Items).Add(
                rows.Items[0]));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_RetainsAllFiveRowsAndUpdatesSummary()
    {
        InspectionProgressRows rows = CreateOwnedRows();
        InspectionContentItemPresentation[] retained = rows.Items.ToArray();

        InspectionProgressRowsApplyResult result = rows.Apply(CreateUpdate(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completedStageCount: 1,
            revision: 1,
            detail: "Reading validated configuration.",
            stageFraction: 0.25));

        Assert.HasCount(5, rows.Items);
        for (int index = 0; index < retained.Length; index++)
        {
            Assert.AreSame(retained[index], rows.Items[index]);
        }

        Assert.AreEqual("1 of 5 checks complete", rows.ProgressSummary);
        Assert.AreEqual(InspectionContentStatus.Passed, rows.Items[0].Status);
        Assert.AreEqual(InspectionContentStatus.Active, rows.Items[1].Status);
        Assert.AreEqual(0.25, rows.Items[1].StageFraction);
        Assert.IsFalse(result.IsEmpty);
        Assert.IsTrue(result.ProgressSummaryChanged);
        CollectionAssert.AreEqual(
            new[] { 0, 1 },
            result.RowChanges.Select(change => change.RowIndex).ToArray());
    }

    [TestMethod]
    [TestCategory("WinUI")]
    [DataRow(
        (int)ModelInspectionStageStatus.Active,
        InspectionContentStatus.Active,
        "Checking",
        true)]
    [DataRow(
        (int)ModelInspectionStageStatus.Completed,
        InspectionContentStatus.Passed,
        "Passed",
        false)]
    [DataRow(
        (int)ModelInspectionStageStatus.Warning,
        InspectionContentStatus.Warning,
        "Warning",
        false)]
    [DataRow(
        (int)ModelInspectionStageStatus.Failed,
        InspectionContentStatus.Error,
        "Failed",
        false)]
    [DataRow(
        (int)ModelInspectionStageStatus.Cancelled,
        InspectionContentStatus.Information,
        "Cancelled",
        false)]
    public void Apply_MapsEveryExplicitCurrentStageStatus(
        int stageStatusValue,
        InspectionContentStatus expectedStatus,
        string expectedStatusText,
        bool expectedActive)
    {
        ModelInspectionStageStatus stageStatus =
            (ModelInspectionStageStatus)stageStatusValue;
        int completed = stageStatus is
            ModelInspectionStageStatus.Completed or
            ModelInspectionStageStatus.Warning
                ? 2
                : 1;
        InspectionProgressRows rows = CreateOwnedRows();

        rows.Apply(CreateUpdate(
            ModelInspectionStage.ReadModelConfiguration,
            stageStatus,
            completed,
            revision: 1,
            detail: "Safe stage detail."));

        InspectionContentItemPresentation current = rows.Items[1];
        Assert.AreEqual(expectedStatus, current.Status);
        Assert.AreEqual(expectedStatusText, current.StatusText);
        Assert.AreEqual(expectedActive, current.IsActive);
        Assert.AreEqual(Visibility.Visible, current.DetailVisibility);
        Assert.AreEqual("Safe stage detail.", current.Detail);
        Assert.AreEqual(
            $"{current.Title}. {expectedStatusText}. Safe stage detail.",
            current.AutomationName);
        Assert.IsTrue(rows.Items.Count(item => item.IsActive) <= 1);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_SameSemanticUpdateDoesNotNotifyOrReportChanges()
    {
        InspectionProgressRows rows = CreateOwnedRows();
        InspectionProgressRowsUpdate first = CreateUpdate(
            ModelInspectionStage.ValidateModelStructure,
            ModelInspectionStageStatus.Active,
            completedStageCount: 3,
            revision: 1,
            detail: "Validating structure.");
        rows.Apply(first);
        List<string?> ownerChanges = [];
        List<string?> rowChanges = [];
        rows.PropertyChanged += (_, args) => ownerChanges.Add(args.PropertyName);
        foreach (InspectionContentItemPresentation item in rows.Items)
        {
            item.PropertyChanged += (_, args) => rowChanges.Add(args.PropertyName);
        }

        InspectionProgressRowsApplyResult result = rows.Apply(
            new InspectionProgressRowsUpdate(
                first.Key,
                new ModelInspectionRenderKey(1, 2),
                first.ProgressSummary));

        Assert.IsTrue(result.IsEmpty);
        Assert.HasCount(0, result.RowChanges);
        Assert.IsFalse(result.ProgressSummaryChanged);
        Assert.HasCount(0, ownerChanges);
        Assert.HasCount(0, rowChanges);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_ReportsOnlyRowsAndPropertiesThatChanged()
    {
        InspectionProgressRows rows = CreateOwnedRows();
        rows.Apply(CreateUpdate(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completedStageCount: 0,
            revision: 1,
            detail: "Checking package."));
        Dictionary<int, List<string?>> notifications = rows.Items
            .Select((_, index) => index)
            .ToDictionary(index => index, _ => new List<string?>());
        for (int index = 0; index < rows.Items.Count; index++)
        {
            int capturedIndex = index;
            rows.Items[index].PropertyChanged += (_, args) =>
                notifications[capturedIndex].Add(args.PropertyName);
        }
        List<string?> ownerNotifications = [];
        rows.PropertyChanged += (_, args) =>
            ownerNotifications.Add(args.PropertyName);

        InspectionProgressRowsApplyResult result = rows.Apply(CreateUpdate(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completedStageCount: 1,
            revision: 2,
            detail: "Reading configuration."));

        CollectionAssert.AreEqual(
            new[] { 0, 1 },
            result.RowChanges.Select(change => change.RowIndex).ToArray());
        Assert.IsTrue(result.RowChanges.All(change =>
            change.StatusChanged && change.DetailChanged));
        string[] expectedRowNotifications =
        [
            nameof(InspectionContentItemPresentation.Status),
            nameof(InspectionContentItemPresentation.StatusText),
            nameof(InspectionContentItemPresentation.IsActive),
            nameof(InspectionContentItemPresentation.Detail),
            nameof(InspectionContentItemPresentation.AutomationHelpText),
            nameof(InspectionContentItemPresentation.DetailVisibility),
            nameof(InspectionContentItemPresentation.AutomationName)
        ];
        CollectionAssert.AreEqual(
            expectedRowNotifications,
            notifications[0].ToArray());
        CollectionAssert.AreEqual(
            expectedRowNotifications,
            notifications[1].ToArray());
        Assert.HasCount(expectedRowNotifications.Length, notifications[0]);
        Assert.HasCount(expectedRowNotifications.Length, notifications[1]);
        for (int index = 2; index < rows.Items.Count; index++)
        {
            Assert.HasCount(
                0,
                notifications[index],
                $"Unchanged row {index} must not notify.");
        }

        CollectionAssert.AreEqual(
            new[] { nameof(InspectionProgressRows.ProgressSummary) },
            ownerNotifications.ToArray());
        Assert.HasCount(1, ownerNotifications);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_DetailOnlyChangeDoesNotReportStatusMotion()
    {
        InspectionProgressRows rows = CreateOwnedRows();
        rows.Apply(CreateUpdate(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completedStageCount: 0,
            revision: 1,
            detail: "First safe detail."));

        InspectionProgressRowsApplyResult result = rows.Apply(CreateUpdate(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completedStageCount: 0,
            revision: 2,
            detail: "Second safe detail."));

        Assert.HasCount(1, result.RowChanges);
        Assert.AreEqual(0, result.RowChanges[0].RowIndex);
        Assert.IsFalse(result.RowChanges[0].StatusChanged);
        Assert.IsTrue(result.RowChanges[0].DetailChanged);
        Assert.AreEqual("Second safe detail.", rows.Items[0].Detail);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_ActiveThenCompletedKeepsNewestTruthfulState()
    {
        InspectionProgressRows rows = CreateOwnedRows();
        InspectionProgressRowsUpdate active = CreateUpdate(
            ModelInspectionStage.ValidateTokenizerAndChatSetup,
            ModelInspectionStageStatus.Active,
            completedStageCount: 2,
            revision: 1,
            detail: "Checking tokenizer.",
            stageFraction: 0.5);
        InspectionProgressRowsUpdate completed = CreateUpdate(
            ModelInspectionStage.ValidateTokenizerAndChatSetup,
            ModelInspectionStageStatus.Completed,
            completedStageCount: 3,
            revision: 2,
            detail: "Tokenizer validated.");

        rows.Apply(active);
        rows.Apply(completed);

        Assert.AreEqual(InspectionContentStatus.Passed, rows.Items[2].Status);
        Assert.IsFalse(rows.Items[2].IsActive);
        Assert.IsNull(rows.Items[2].StageFraction);
        Assert.AreEqual("3 of 5 checks complete", rows.ProgressSummary);
        Assert.ThrowsExactly<ArgumentException>(() => rows.Apply(active));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Reset_UnboundOwnerPromotesOnceAndNeverCrossesAttemptGeneration()
    {
        InspectionProgressRows rows = new();
        InspectionContentItemPresentation[] retained = rows.Items.ToArray();
        rows.Reset(new ModelInspectionRenderKey(7, 0));

        for (int index = 0; index < retained.Length; index++)
        {
            Assert.AreSame(retained[index], rows.Items[index]);
        }

        rows.Apply(CreateUpdate(
            ModelInspectionStage.ValidateModelStructure,
            ModelInspectionStageStatus.Active,
            completedStageCount: 3,
            revision: 1,
            detail: "Private-to-attempt detail.",
            stageFraction: 0.75,
            attempt: 7));

        Assert.ThrowsExactly<ArgumentException>(() =>
            rows.Reset(new ModelInspectionRenderKey(8, 0)));
        Assert.AreEqual(
            "Private-to-attempt detail.",
            rows.Items[(int)ModelInspectionStage.ValidateModelStructure - 1].Detail);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void LaterAttempt_UsesNewOwnerWithFiveDistinctRows()
    {
        InspectionProgressRows firstAttempt = CreateOwnedRows(attempt: 1);
        InspectionProgressRows secondAttempt = CreateOwnedRows(attempt: 2);

        Assert.AreNotSame(firstAttempt, secondAttempt);
        Assert.AreNotSame(firstAttempt.Items, secondAttempt.Items);
        Assert.HasCount(5, firstAttempt.Items);
        Assert.HasCount(5, secondAttempt.Items);
        for (int index = 0; index < firstAttempt.Items.Count; index++)
        {
            Assert.AreNotSame(firstAttempt.Items[index], secondAttempt.Items[index]);
        }

        secondAttempt.Apply(CreateUpdate(
            ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active,
            completedStageCount: 0,
            revision: 1,
            detail: "Second attempt package check.",
            attempt: 2));

        Assert.AreEqual(
            InspectionContentStatus.Waiting,
            firstAttempt.Items[0].Status);
        Assert.AreEqual(
            InspectionContentStatus.Active,
            secondAttempt.Items[0].Status);
    }

    [TestMethod]
    public void Update_PropertiesAreConstructorOnly()
    {
        foreach (string propertyName in new[]
        {
            nameof(InspectionProgressRowsUpdate.Key),
            nameof(InspectionProgressRowsUpdate.OwnerKey),
            nameof(InspectionProgressRowsUpdate.ProgressSummary)
        })
        {
            PropertyInfo? property = typeof(InspectionProgressRowsUpdate)
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(property, $"Missing property {propertyName}.");
            Assert.IsFalse(property.CanWrite, $"{propertyName} must be get-only.");
            Assert.IsNull(
                property.SetMethod,
                $"{propertyName} must not expose an init or set method.");
        }
    }

    [TestMethod]
    public void Apply_RejectsWrongOwnerAndContradictorySafeKeys()
    {
        InspectionProgressRows rows = CreateOwnedRows();
        Assert.ThrowsExactly<ArgumentException>(() => rows.Apply(
            CreateUpdate(
                ModelInspectionStage.CheckModelPackage,
                ModelInspectionStageStatus.Active,
                completedStageCount: 0,
                revision: 1,
                detail: "Wrong attempt.",
                attempt: 2)));
        Assert.ThrowsExactly<ArgumentException>(() => rows.Apply(new(
            new ModelInspectionProgressRegionKey(
                ModelInspectionStage.ReadModelConfiguration,
                ModelInspectionStageStatus.Active,
                completedStageCount: 2,
                stageCount: 5,
                stageFraction: null,
                detail: "Contradictory count."),
            new ModelInspectionRenderKey(1, 1),
            "2 of 5 checks complete")));
        Assert.ThrowsExactly<ArgumentException>(() => rows.Apply(new(
            new ModelInspectionProgressRegionKey(
                ModelInspectionStage.ReadModelConfiguration,
                ModelInspectionStageStatus.Active,
                completedStageCount: 1,
                stageCount: 4,
                stageFraction: null,
                detail: "Wrong stage total."),
            new ModelInspectionRenderKey(1, 1),
            "1 of 4 checks complete")));
    }

    [TestMethod]
    public void ApplyResult_CopiesChangesAndValidatesIndexes()
    {
        List<InspectionProgressRowChange> changes =
        [
            new InspectionProgressRowChange(
                rowIndex: 2,
                statusChanged: true,
                detailChanged: false)
        ];

        InspectionProgressRowsApplyResult result = new(
            changes,
            progressSummaryChanged: false);
        changes.Clear();

        Assert.HasCount(1, result.RowChanges);
        Assert.AreEqual(2, result.RowChanges[0].RowIndex);
        Assert.IsFalse(result.IsEmpty);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new InspectionProgressRowChange(
                rowIndex: 5,
                statusChanged: true,
                detailChanged: true));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new InspectionProgressRowsApplyResult(
                null!,
                progressSummaryChanged: false));
    }

    private static InspectionProgressRows CreateOwnedRows(long attempt = 1)
    {
        InspectionProgressRows rows = new();
        rows.Reset(new ModelInspectionRenderKey(attempt, 0));
        return rows;
    }

    private static InspectionProgressRowsUpdate CreateUpdate(
        ModelInspectionStage stage,
        ModelInspectionStageStatus status,
        int completedStageCount,
        long revision,
        string detail,
        double? stageFraction = null,
        long attempt = 1)
    {
        return new InspectionProgressRowsUpdate(
            new ModelInspectionProgressRegionKey(
                stage,
                status,
                completedStageCount,
                stageCount: 5,
                stageFraction,
                detail),
            new ModelInspectionRenderKey(attempt, revision),
            $"{completedStageCount} of 5 checks complete");
    }
}
