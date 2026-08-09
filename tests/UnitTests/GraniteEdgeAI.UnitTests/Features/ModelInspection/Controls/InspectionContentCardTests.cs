using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
public sealed class InspectionContentCardTests
{
    [TestMethod]
    public void HiddenPresentations_DoNotShareProgressFallbackOrItems()
    {
        InspectionContentCardPresentation first =
            InspectionContentCardPresentation.Hidden;
        InspectionContentCardPresentation second =
            InspectionContentCardPresentation.Hidden;

        Assert.IsNull(GetStoredProgressRows(first));
        Assert.IsNull(GetStoredProgressRows(second));

        InspectionProgressRows firstOwner = first.ProgressRows;
        InspectionProgressRows secondOwner = second.ProgressRows;

        Assert.AreSame(firstOwner, first.ProgressRows);
        Assert.AreSame(secondOwner, second.ProgressRows);
        Assert.AreNotSame(firstOwner, secondOwner);
        Assert.AreNotSame(firstOwner.Items, secondOwner.Items);
        Assert.AreNotSame(first.Items, second.Items);
        Assert.HasCount(0, first.Items);
        Assert.HasCount(0, second.Items);
    }

    [TestMethod]
    public void NonProgressGetters_DoNotAllocateProgressOwner()
    {
        InspectionContentCardPresentation hidden =
            InspectionContentCardPresentation.Hidden;
        var terminalItems = Array.AsReadOnly(
            new[]
            {
                new InspectionContentItemPresentation
                {
                    Title = "Safe terminal finding"
                }
            });
        var terminal = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            ProgressSummary = "Complete",
            Items = terminalItems
        };

        Assert.IsNull(GetStoredProgressRows(hidden));
        Assert.IsNull(GetStoredProgressRows(terminal));

        Assert.AreEqual(string.Empty, hidden.ProgressSummary);
        Assert.HasCount(0, hidden.Items);
        Assert.AreEqual("Complete", terminal.ProgressSummary);
        Assert.AreSame(terminalItems, terminal.Items);

        Assert.IsNull(GetStoredProgressRows(hidden));
        Assert.IsNull(GetStoredProgressRows(terminal));
    }

    [TestMethod]
    public void ExplicitProgressOwner_IsStoredDirectlyAndNullIsRejected()
    {
        InspectionProgressRows owner = new();
        var presentation = new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress,
            ProgressRows = owner
        };

        Assert.AreSame(owner, GetStoredProgressRows(presentation));
        Assert.AreSame(owner, presentation.ProgressRows);
        Assert.AreSame(owner.Items, presentation.Items);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _ = new InspectionContentCardPresentation
            {
                ProgressRows = null!
            });
    }

    [TestMethod]
    [DataRow((int)InspectionContentStatus.Passed, true, false, false, (int)Symbol.Accept)]
    [DataRow((int)InspectionContentStatus.Warning, true, false, false, (int)Symbol.Important)]
    [DataRow((int)InspectionContentStatus.Error, true, false, false, (int)Symbol.Cancel)]
    [DataRow((int)InspectionContentStatus.Information, true, false, false, (int)Symbol.Help)]
    [DataRow((int)InspectionContentStatus.Active, false, true, false, (int)Symbol.Clock)]
    [DataRow((int)InspectionContentStatus.Waiting, false, false, true, (int)Symbol.Clock)]
    [DataRow((int)InspectionContentStatus.Neutral, false, false, false, (int)Symbol.Help)]
    public void StatusMarkers_AreMutuallyExclusiveForEveryProgressStatus(
        int statusValue,
        bool terminalVisible,
        bool activeVisible,
        bool waitingVisible,
        int symbolValue)
    {
        InspectionContentStatus status = (InspectionContentStatus)statusValue;

        Assert.AreEqual(
            terminalVisible ? Visibility.Visible : Visibility.Collapsed,
            InspectionContentCard.GetTerminalMarkerVisibility(status));
        Assert.AreEqual(
            activeVisible ? Visibility.Visible : Visibility.Collapsed,
            InspectionContentCard.GetActiveVisibility(status));
        Assert.AreEqual(
            waitingVisible ? Visibility.Visible : Visibility.Collapsed,
            InspectionContentCard.GetWaitingVisibility(status));
        Assert.AreEqual((Symbol)symbolValue, InspectionContentCard.GetStatusSymbol(status));
        Assert.AreEqual(
            status == InspectionContentStatus.Neutral ? 0 : 1,
            new[] { terminalVisible, activeVisible, waitingVisible }.Count(value => value),
            status is InspectionContentStatus.Neutral
                ? "Neutral intentionally renders no progress marker."
                : $"{status} must render exactly one marker.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ProgressBindings_ObserveOwnerWithoutReplacingPresentation()
    {
        InspectionProgressRows rows = new();
        rows.Reset(new ModelInspectionRenderKey(1, 0));
        InspectionContentCardPresentation presentation =
            InitialInspectionProgressPresentationFactory.Create(rows);
        var control = new InspectionContentCard
        {
            Presentation = presentation
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window
        {
            Content = control
        };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            rows.Apply(new InspectionProgressRowsUpdate(
                new ModelInspectionProgressRegionKey(
                    ModelInspectionStage.CheckModelPackage,
                    ModelInspectionStageStatus.Warning,
                    completedStageCount: 1,
                    stageCount: 5,
                    stageFraction: null,
                    detail: "Package check completed with a warning."),
                new ModelInspectionRenderKey(1, 1),
                "1 of 5 checks complete"));
            await Task.Yield();
            control.UpdateLayout();

            IReadOnlyList<DependencyObject> descendants =
                EnumerateDescendants(control).ToArray();
            string[] visibleText = descendants
                .OfType<TextBlock>()
                .Where(text => text.Visibility == Visibility.Visible)
                .Select(text => text.Text)
                .ToArray();
            SymbolIcon[] visibleSymbols = descendants
                .OfType<SymbolIcon>()
                .Where(icon => icon.Visibility == Visibility.Visible)
                .ToArray();

            Assert.AreSame(presentation, control.Presentation);
            CollectionAssert.Contains(visibleText, "1 of 5 checks complete");
            CollectionAssert.Contains(visibleText, "Warning");
            Assert.IsTrue(visibleSymbols.Any(icon => icon.Symbol == Symbol.Important));
            Assert.IsFalse(descendants
                .OfType<ProgressRing>()
                .Any(ring => ring.Visibility == Visibility.Visible));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static IEnumerable<DependencyObject> EnumerateDescendants(
        DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static InspectionProgressRows? GetStoredProgressRows(
        InspectionContentCardPresentation presentation)
    {
        FieldInfo[] ownerFields = typeof(InspectionContentCardPresentation)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(InspectionProgressRows))
            .ToArray();
        Assert.HasCount(
            1,
            ownerFields,
            "The presentation must have exactly one progress-owner storage field.");
        return (InspectionProgressRows?)ownerFields[0].GetValue(presentation);
    }
}
