using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
public sealed class CompatibilityPresentationFactoryTests
{
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public void NoEstimateOnlySuppressesRetryWithoutChangingImportRecovery(
        bool noEstimate, bool advisory, bool mixed)
    {
        var findings = new List<CompatibilityFindingView>();
        if (noEstimate) findings.Add(new(CompatibilityFindingCode.NoCandidateCouldBeEstimated, FindingSeverity.Blocking));
        if (advisory) findings.Add(new(CompatibilityFindingCode.ModelFactsUnavailable, FindingSeverity.Warning));
        if (mixed) findings.Add(new(CompatibilityFindingCode.HardwareFactsUnavailable, FindingSeverity.Blocking));
        var presentation = CompatibilityPresentationFactory.From(CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NotEstablished, findings, [], BaselineExclusionReason.None, false, false));
        bool hidden = noEstimate && !mixed;
        Assert.AreEqual(hidden ? string.Empty : "Check again", presentation.SecondaryActionText);
        Assert.AreEqual(!hidden, presentation.SecondaryActionEnabled);
        Assert.AreEqual(hidden ? CompatibilitySecondaryActionKind.None : CompatibilitySecondaryActionKind.Retry,
            presentation.SecondaryActionKind);
        Assert.AreEqual(CompatibilityForwardActionKind.ImportAnotherModel, presentation.ForwardActionKind);
        Assert.IsFalse(presentation.PrimaryActionEnabled);
        Assert.AreEqual(CompatibilitySecondaryActionKind.Retry, CompatibilityPresentationFactory.Cancelled().SecondaryActionKind);
        Assert.AreEqual(CompatibilitySecondaryActionKind.Retry, CompatibilityPresentationFactory.OperationalFailure().SecondaryActionKind);
    }

    [TestMethod]
    public void KnownUnavailableOpenVinoConfigurationExplainsVerifiedAlternativeWithoutFalseRamClaim()
    {
        var current = CompatibilitySetupView.ForPresentation(RuntimeRouteId.OpenVinoGenAi,
            CompatibilityBackend.OpenVinoCpu, DeviceRouteId.Cpu, WeightQuantisation.F16,
            4096, CompatibilityFitState.Safe, 3 * GiB, 4 * GiB, GiB, 536870912,
            false, false, [], openVinoKvCache: OpenVinoKvCacheFormat.RouteDefault);
        var screen = CompatibilityScreenModel.ForPresentation(CompatibilityScreenState.OptimisationRequired,
            [new CompatibilityFindingView(GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts.CompatibilityFindingCode.BaselineConfigurationUnavailable,
                GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts.FindingSeverity.Warning)],
            [], BaselineExclusionReason.None, false, true, current,
            OpenVinoCacheOnlyScreen(WeightQuantisation.F16).Optimization);
        var presentation = CompatibilityPresentationFactory.From(screen);
        Assert.IsFalse(presentation.PrimaryActionEnabled,
            "A presentation-only fixture must not manufacture executable authority.");
        Assert.IsNotNull(presentation.Optimization);
        StringAssert.Contains(presentation.OutcomeDetail, "failed output checks");
        Assert.IsFalse(presentation.OutcomeDetail.Contains("more memory", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(presentation.OutcomeTitle.Contains("quantised", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(CompatibilityMemoryRecoveryReason.None, presentation.MemoryRecoveryReason);
        Assert.IsFalse(screen.UseCurrentModelAvailable);
    }

    [TestMethod]
    [TestCategory("RouteNeutralMemoryOverview")]
    public void OpenVinoCurrentFitKeepsMemoryWithoutOffersAndUsesOnlySameRouteSafeMinimum()
    {
        var budget = SystemMemoryBudgetCalculator.Calculate(CurrentlyAvailableMemory.FromBytes((9 * GiB) / 2));
        var memory = CompatibilityMachineMemory.Create(TotalPhysicalMemory.FromBytes(16 * GiB),
            budget.Available, budget.Reserve, budget.Executable);
        var setup = CompatibilitySetupView.ForPresentation(RuntimeRouteId.OpenVinoGenAi,
            CompatibilityBackend.OpenVinoCpu, DeviceRouteId.Cpu, WeightQuantisation.F16,
            4096, CompatibilityFitState.Safe, requiredBytes: 3 * GiB, safeBudgetBytes: 4 * GiB,
            headroomBytes: GiB, uncertaintyAllowanceBytes: 268_435_456, isExperimental: false,
            requiresConversion: false, [], openVinoKvCache: OpenVinoKvCacheFormat.U8);
        var screen = CompatibilityScreenModel.ForPresentation(CompatibilityScreenState.EstimatedCompatible,
            [], [], BaselineExclusionReason.None, useCurrentModelAvailable: true, continueEnabled: true, setup: setup);
        var own = OpenVinoCacheOnlyScreen(WeightQuantisation.F16, modePredictedPeakBytes: 2 * GiB).Optimization!;
        var foreign = OptimizationScreen().Optimization!;
        CompatibilityOptimizationView?[] options =
        [
            null,
            WithSafeSliderModes(foreign, foreign.Modes[0]),
            WithSafeSliderModes(own, own.Modes[0])
        ];
        for (int index = 0; index < options.Length; index++)
        {
            var result = CompatibilityPresentationFactory.From(new CompatibilityEvaluation(screen, null, null)
            { MachineMemory = memory, OptionalOptimization = options[index] });
            Assert.IsNotNull(result.MemoryOverview, "Current OpenVINO memory facts do not depend on optional qualification.");
            var overview = result.MemoryOverview!;
            Assert.AreEqual(memory.AvailableSystemMemoryBytes, overview.AvailableSystemMemoryBytes);
            Assert.AreEqual(memory.SafetyReserveBytes, overview.SafetyReserveBytes);
            Assert.AreEqual(3 * GiB, overview.CurrentRequiredBytes);
            Assert.AreEqual(4 * GiB, overview.SafeModelBudgetBytes);
            if (index == 2)
            {
                Assert.IsFalse(result.OutcomeDetail.Contains("No verified optimisation option", StringComparison.Ordinal));
                Assert.AreEqual(2 * GiB, overview.MinimumRequiredBytes);
                Assert.AreEqual("Smallest offered setup needs", overview.MinimumRequirementLabel);
            }
            else
            {
                Assert.IsTrue(result.OutcomeDetail.Contains("No verified optimisation option", StringComparison.Ordinal));
                Assert.IsNull(overview.MinimumRequiredBytes);
                Assert.IsNull(overview.MinimumRequirementLabel);
            }
        }
    }
    private const ulong GiB = 1024UL * 1024 * 1024;

    [TestMethod]
    public void ExactSafeModes_PreserveAuthoritativeOrderAndProjectExactSelection()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        Assert.IsNotNull(fixture.Evaluation.Screen.Optimization);
        CompatibilityOptimizationView source =
            fixture.Evaluation.Screen.Optimization;
        string exactIdentity = source.ExactSafeModes.Last().CandidateIdentity;

        CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
            fixture.Evaluation,
            OptimizationPreferenceSelection.Exact(exactIdentity));

        Assert.IsNotNull(presentation.Optimization);
        Assert.IsTrue(presentation.Optimization.HasAdditionalExactSafeModes);
        CollectionAssert.AreEqual(
            source.ExactSafeModes.Select(item => item.CandidateIdentity).ToArray(),
            presentation.Optimization.ExactSafeModes
                .Select(item => item.CandidateIdentity).ToArray());
        CollectionAssert.AreEqual(
            source.ExactSafeModes
                .Select(item => item.Mode.IsExperimental
                    ? CompatibilityExactOptimizationAvailability.ExperimentalPreview
                    : CompatibilityExactOptimizationAvailability.Released)
                .ToArray(),
            presentation.Optimization.ExactSafeModes
                .Select(item => item.Availability)
                .ToArray(),
            "Presentation must distinguish released setups from preview-eligible experimental setups.");
        CompatibilityExactOptimizationModePresentation selected = presentation
            .Optimization.ExactSafeModes.Single(item =>
                item.CandidateIdentity == exactIdentity);
        Assert.AreEqual(selected.Mode, presentation.Optimization.SelectedMode);
        Assert.AreEqual(OptimizationPreferenceKind.Exact,
            presentation.Optimization.Preference.Kind);
    }

    [TestMethod]
    public void SafeSliderModes_PreserveReleasedAuthoritativeOrderAndSelectedIdentity()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityOptimizationView source = fixture.Evaluation.Screen.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");
        int sourceIndex = source.SafeSliderSelectedIndex
            ?? throw new AssertFailedException("The source-backed fixture needs a selected safe setup.");
        string selectedIdentity = source.SafeSliderModes[sourceIndex].CandidateIdentity;

        CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
            fixture.Evaluation,
            OptimizationPreferenceSelection.Exact(selectedIdentity));

        CompatibilityOptimizationPresentation projected = presentation.Optimization
            ?? throw new AssertFailedException("The presentation needs optimisation choices.");
        CollectionAssert.AreEqual(
            source.SafeSliderModes.Select(item => item.CandidateIdentity).ToArray(),
            projected.SafeSliderModes.Select(item => item.CandidateIdentity).ToArray(),
            "The frontend must preserve the backend's safe RAM ordering exactly.");
        Assert.IsTrue(projected.SafeSliderModes.All(item => !item.Mode.IsExperimental),
            "The released safe slider must not promote experimental preview entries.");
        Assert.AreEqual(sourceIndex, projected.SafeSliderSelectedIndex);
        Assert.AreEqual(selectedIdentity,
            projected.SafeSliderModes[projected.SafeSliderSelectedIndex!.Value].CandidateIdentity);
        Assert.AreEqual(projected.SafeSliderModes[sourceIndex].Mode, projected.SelectedMode);
    }

    [TestMethod]
    public void SafeSliderModeLabels_FollowNormalizedRamPositionForEveryCountShape()
    {
        CollectionAssert.AreEqual(
            new[] { "Single available setup" },
            LabelsFor(1));
        CollectionAssert.AreEqual(
            new[] { "Maximum efficiency", "Highest memory" },
            LabelsFor(2));
        CollectionAssert.AreEqual(
            new[] { "Maximum efficiency", "Balanced", "Highest memory" },
            LabelsFor(3));
        CollectionAssert.AreEqual(
            new[]
            {
                "Maximum efficiency", "Efficient", "Higher memory", "Highest memory"
            },
            LabelsFor(4));
        CollectionAssert.AreEqual(
            new[]
            {
                "Maximum efficiency", "Efficient", "Balanced", "Higher memory",
                "Highest memory"
            },
            LabelsFor(5));
        CollectionAssert.AreEqual(
            new[]
            {
                "Maximum efficiency", "Efficient", "Balanced", "Balanced",
                "Higher memory", "Highest memory"
            },
            LabelsFor(6));

        static string[] LabelsFor(int count) => Enumerable.Range(0, count)
            .Select(index => CompatibilityPresentationFactory.SafeSliderModeLabel(
                index, count))
            .ToArray();
    }

    [TestMethod]
    public void SafeSliderExactSelection_UsesTheSameRamPositionLabelAsItsStop()
    {
        CompatibilityViewModelTests.RealExactPlanningFixture fixture =
            CompatibilityViewModelTests.RealExactFixture();
        CompatibilityOptimizationView source = fixture.Evaluation.Screen.Optimization
            ?? throw new AssertFailedException("The source-backed fixture needs optimisation choices.");

        for (int index = 0; index < source.SafeSliderModes.Count; index++)
        {
            string identity = source.SafeSliderModes[index].CandidateIdentity;
            CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
                fixture.Evaluation,
                OptimizationPreferenceSelection.Exact(identity));
            CompatibilityOptimizationPresentation projected = presentation.Optimization
                ?? throw new AssertFailedException("The exact selection must remain projectable.");

            CollectionAssert.AreEqual(
                source.SafeSliderModes.Select(item => item.CandidateIdentity).ToArray(),
                projected.SafeSliderModes.Select(item => item.CandidateIdentity).ToArray());
            Assert.AreEqual(
                projected.SafeSliderModes[index].Mode.Label,
                projected.SelectedMode.Label,
                "The selected summary must use the relabelled authoritative slider stop.");
            Assert.AreNotEqual("Automatic", projected.SelectedMode.Label);
        }
    }

    [TestMethod]
    public void OpenVinoRouteDefaultCache_UsesOpenVinoSpecificAutomaticCopyOnly()
    {
        CompatibilityPresentation openVino = CompatibilityPresentationFactory.From(
            OpenVinoCacheOnlyScreen(
                WeightQuantisation.Q8_0,
                currentOpenVinoCache: OpenVinoKvCacheFormat.RouteDefault));
        CompatibilityPresentation gguf = CompatibilityPresentationFactory.From(
            OptimizationScreen());

        Assert.AreEqual(
            "Automatic (OpenVINO default)",
            openVino.Optimization?.CurrentCacheFormat);
        Assert.AreEqual("F16", gguf.Optimization?.CurrentCacheFormat,
            "The OpenVINO copy must not rename GGUF cache formats.");
    }

    [TestMethod]
    public void MachineMemorySummary_FormatsExactEvaluationValues()
    {
        ulong available = 6 * GiB;
        AvailableMemorySafetyBudget budget = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes(available));
        var evaluation = new CompatibilityEvaluation(
            OptimizationScreen(),
            PlanningSession: null,
            CurrentConfiguration: null)
        {
            MachineMemory = CompatibilityMachineMemory.Create(
                TotalPhysicalMemory.FromBytes(16 * GiB),
                budget.Available,
                budget.Reserve,
                budget.Executable)
        };

        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(evaluation);

        Assert.IsNotNull(presentation.MachineMemory);
        CollectionAssert.AreEqual(
            new[]
            {
                "Installed RAM|16 GB",
                "Available now|6 GB",
                "Safety reserve|614 MB",
                "Safe for this model|5.4 GB"
            },
            presentation.MachineMemory.Facts
                .Select(fact => $"{fact.Label}|{fact.Value}")
                .ToArray());
    }

    [TestMethod]
    public void OptimisationRequired_UsesApprovedCopyAndAllFrozenChoices()
    {
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(OptimizationScreen());

        Assert.AreEqual(
            "This model needs to be quantised to run on your computer",
            presentation.OutcomeTitle);
        Assert.AreEqual(
            "Your current model needs more memory than this computer can safely spare. A smaller version can run here.",
            presentation.OutcomeDetail);
        Assert.IsNotNull(presentation.Optimization);
        Assert.AreEqual(
            "You can choose how you want to balance memory use and expected quality.",
            presentation.Optimization.Instruction);
        CollectionAssert.AreEqual(
            new[]
            {
                "Automatic",
                "Maximum efficiency",
                "Efficient",
                "Balanced",
                "High capability",
                "Maximum capability"
            },
            presentation.Optimization.Modes.Select(mode => mode.Label).ToArray());
        Assert.IsTrue(presentation.Optimization.Modes.All(mode =>
            !string.IsNullOrWhiteSpace(mode.ExpectedQualityText)));
        Assert.AreEqual("Choose optimisation", presentation.PrimaryActionText);
        Assert.AreEqual("Back", presentation.SecondaryActionText);
        Assert.AreEqual("F16", presentation.Optimization.CurrentCacheFormat);
    }

    [TestMethod]
    public void OptimisationRequired_RetainsCurrentMemoryOverviewForGgufAndOpenVino()
    {
        AvailableMemorySafetyBudget matchingBudget =
            SystemMemoryBudgetCalculator.Calculate(
                CurrentlyAvailableMemory.FromBytes((9 * GiB) / 2));
        CompatibilityMachineMemory memory = CompatibilityMachineMemory.Create(
            TotalPhysicalMemory.FromBytes(16 * GiB),
            matchingBudget.Available,
            matchingBudget.Reserve,
            matchingBudget.Executable);

        foreach (CompatibilityScreenModel screen in new[]
                 {
                     OptimizationScreen(
                         modeSafeBudgetBytes: (7 * GiB) / 2,
                         modePredictedPeakBytes: 3 * GiB + GiB / 10),
                     OpenVinoCacheOnlyScreen(
                         WeightQuantisation.F16,
                         modeSafeBudgetBytes: (7 * GiB) / 2,
                         modePredictedPeakBytes: 3 * GiB + GiB / 10)
                 })
        {
            CompatibilityPresentation before =
                CompatibilityPresentationFactory.From(screen);
            CompatibilityPresentation presentation =
                CompatibilityPresentationFactory.From(new CompatibilityEvaluation(
                    screen, PlanningSession: null, CurrentConfiguration: null)
                {
                    MachineMemory = memory
                });

            Assert.IsNotNull(presentation.Optimization);
            Assert.IsGreaterThan(0, presentation.Facts.Count);
            Assert.IsGreaterThan(0, presentation.Budget.Segments.Count);
            Assert.IsNotNull(presentation.EstimateSummary);
            Assert.IsNotNull(presentation.MemoryOverview);
            CompatibilityMemoryOverviewPresentation overview =
                presentation.MemoryOverview!;
            Assert.AreEqual((9 * GiB) / 2,
                overview.AvailableSystemMemoryBytes);
            Assert.AreEqual(4 * GiB,
                overview.SafeModelBudgetBytes);
            Assert.AreEqual(5 * GiB,
                overview.CurrentRequiredBytes);
            Assert.AreEqual(3 * GiB + GiB / 10,
                overview.MinimumRequiredBytes);
            Assert.AreEqual("Smallest offered setup needs",
                overview.MinimumRequirementLabel);
            Assert.AreEqual(before.OutcomeTitle, presentation.OutcomeTitle);
            Assert.AreEqual(before.OutcomeDetail, presentation.OutcomeDetail);
            Assert.AreEqual(before.PrimaryActionText,
                presentation.PrimaryActionText);
            Assert.AreEqual(before.SecondaryActionText,
                presentation.SecondaryActionText);
            Assert.AreEqual(before.Optimization!.SelectedMode,
                presentation.Optimization!.SelectedMode);
            Assert.IsNull(presentation.StorageShortage);
        }

        AvailableMemorySafetyBudget mismatchBudget =
            SystemMemoryBudgetCalculator.Calculate(
                CurrentlyAvailableMemory.FromBytes(5 * GiB));
        CompatibilityPresentation mismatch =
            CompatibilityPresentationFactory.From(new CompatibilityEvaluation(
                OptimizationScreen(), null, null)
            {
                MachineMemory = CompatibilityMachineMemory.Create(
                    TotalPhysicalMemory.FromBytes(16 * GiB),
                    mismatchBudget.Available,
                    mismatchBudget.Reserve,
                    mismatchBudget.Executable)
            });
        Assert.IsNull(mismatch.MemoryOverview);
        Assert.IsGreaterThan(0, mismatch.Budget.Segments.Count);

        CompatibilityScreenModel required = OptimizationScreen();
        CompatibilityScreenModel explicitMinimum =
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.OptimisationRequired,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: true,
                setup: required.CurrentSetup,
                optimization: required.Optimization,
                smallestOptimizedRequiredBytes: 2 * GiB);
        CompatibilityPresentation withCoreMinimum =
            CompatibilityPresentationFactory.From(new CompatibilityEvaluation(
                explicitMinimum, null, null)
            {
                MachineMemory = memory
            });
        Assert.AreEqual(2 * GiB,
            withCoreMinimum.MemoryOverview!.MinimumRequiredBytes);
        Assert.AreEqual("Smallest acceptable format needs",
            withCoreMinimum.MemoryOverview!.MinimumRequirementLabel);

        CompatibilityScreenModel optionalScreen =
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.EstimatedCompatible,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: true,
                continueEnabled: true,
                setup: CompatibleSetup());
        CompatibilityPresentation optional =
            CompatibilityPresentationFactory.OptionalOptimization(
                new CompatibilityEvaluation(optionalScreen, null, null)
                {
                    MachineMemory = memory
                },
                required.Optimization!,
                OptimizationPreferenceSelection.Automatic());
        Assert.IsNotNull(optional.Optimization);
        Assert.IsNotNull(optional.MemoryOverview);
        Assert.IsGreaterThan(0, optional.Budget.Segments.Count);
        Assert.AreEqual("Optimisation is optional", optional.OutcomeTitle);
        Assert.AreEqual("Optimise first", optional.PrimaryActionText);
        Assert.IsNull(optional.StorageShortage);
    }

    [TestMethod]
    public void OptimisationRequired_OfferedMinimumRejectsUnsupportedBudgetOrQuality()
    {
        AvailableMemorySafetyBudget matchingBudget =
            SystemMemoryBudgetCalculator.Calculate(
                CurrentlyAvailableMemory.FromBytes((9 * GiB) / 2));
        CompatibilityMachineMemory memory = CompatibilityMachineMemory.Create(
            TotalPhysicalMemory.FromBytes(16 * GiB),
            matchingBudget.Available,
            matchingBudget.Reserve,
            matchingBudget.Executable);

        foreach (CompatibilityScreenModel screen in new[]
                 {
                     OptimizationScreen(
                         modeSafeBudgetBytes: 5 * GiB,
                         modePredictedPeakBytes: 3 * GiB),
                     OptimizationScreen(
                         modeSafeBudgetBytes: (7 * GiB) / 2,
                         modePredictedPeakBytes: 3 * GiB,
                         allModesBelowMinimum: true)
                 })
        {
            CompatibilityPresentation presentation =
                CompatibilityPresentationFactory.From(new CompatibilityEvaluation(
                    screen, PlanningSession: null, CurrentConfiguration: null)
                {
                    MachineMemory = memory
                });

            Assert.IsNotNull(presentation.MemoryOverview);
            Assert.IsNull(presentation.MemoryOverview!.MinimumRequiredBytes);
            Assert.IsNull(presentation.MemoryOverview.MinimumRequirementLabel);
        }

        Assert.Throws<ArgumentException>(() => Mode(
            CompatibilityOptimizationLabelCode.Automatic,
            slider: null,
            GgufWeightFormat.Q3KM,
            OptimizationAssessment.Good,
            requantisation: false,
            predictedPeakBytes: 3 * GiB,
            safeBudgetBytes: 0));
    }

    [TestMethod]
    public void GgufCurrentFit_UsesOnlyReleasedSafeSliderModesForMemoryParity()
    {
        CompatibilityOptimizationView broadProjection = OptimizationScreen(
            modePredictedPeakBytes: 2 * GiB).Optimization!;
        CompatibilityOptimizationModeView q8 = Mode(
            CompatibilityOptimizationLabelCode.Automatic,
            slider: null,
            GgufWeightFormat.Q4KM,
            OptimizationAssessment.Good,
            requantisation: false,
            predictedPeakBytes: 3 * GiB,
            safeBudgetBytes: 4 * GiB);
        CompatibilityOptimizationView actualOffered = WithSafeSliderModes(
            broadProjection,
            q8);

        CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
            GgufFitEvaluation(actualOffered));

        CompatibilityMemoryOverviewPresentation overview = presentation.MemoryOverview
            ?? throw new AssertFailedException("The established GGUF fit needs its memory overview.");
        Assert.AreEqual((9 * GiB) / 2, overview.AvailableSystemMemoryBytes);
        Assert.AreEqual(4 * GiB, overview.SafeModelBudgetBytes);
        Assert.AreEqual(3 * GiB, overview.CurrentRequiredBytes);
        Assert.AreEqual(3 * GiB, overview.MinimumRequiredBytes,
            "The minimum must come from the actual SafeSliderModes entry, not the lower broad-mode alias.");
        Assert.AreEqual("Smallest offered setup needs", overview.MinimumRequirementLabel);
        Assert.IsTrue(presentation.ShowOptimiseFurtherAction);
        Assert.AreEqual("Optimise further", presentation.OptimiseFurtherActionText);
        Assert.AreEqual("Chat with model now", presentation.CurrentModelChatActionText);
        Assert.IsNull(presentation.Optimization,
            "Stage 3 must not expose the Stage 4 selector before the existing command runs.");
    }

    [TestMethod]
    public void GgufCurrentFit_ReservesUnavailableMinimumWithoutFabricatingOne()
    {
        CompatibilityPresentation missing = CompatibilityPresentationFactory.From(
            GgufFitEvaluation(optionalOptimization: null));
        CompatibilityPresentation belowQuality = CompatibilityPresentationFactory.From(
            GgufFitEvaluation(WithSafeSliderModes(
                OptimizationScreen().Optimization!,
                Mode(
                    CompatibilityOptimizationLabelCode.Automatic,
                    slider: null,
                    GgufWeightFormat.Q4KM,
                    OptimizationAssessment.Poor,
                    requantisation: false,
                    predictedPeakBytes: 3 * GiB,
                    safeBudgetBytes: 4 * GiB))));
        CompatibilityOptimizationModeView experimental =
            CompatibilityOptimizationModeView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                sliderValue: null,
                OptimizationRoute.Gguf,
                GgufWeightFormat.Q4KM,
                GgufKvCacheFormat.Q8_0,
                null,
                null,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Good,
                contextTokens: 4096,
                predictedPeakBytes: 3 * GiB,
                safeBudgetBytes: 4 * GiB,
                headroomBytes: GiB,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                qualityNotice: OptimizationQualityNotice.None,
                isExperimental: true,
                sharedWithAdjacentBand: false);
        CompatibilityPresentation previewOnly = CompatibilityPresentationFactory.From(
            GgufFitEvaluation(WithSafeSliderModes(
                OptimizationScreen().Optimization!, experimental)));

        CompatibilityOptimizationModeView wrongRoute =
            CompatibilityOptimizationModeView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                sliderValue: null,
                OptimizationRoute.OpenVino,
                null,
                null,
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U8,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Good,
                contextTokens: 4096,
                predictedPeakBytes: 3 * GiB,
                safeBudgetBytes: 4 * GiB,
                headroomBytes: GiB,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                qualityNotice: OptimizationQualityNotice.None,
                isExperimental: false,
                sharedWithAdjacentBand: false);
        CompatibilityPresentation wrongRouteOnly =
            CompatibilityPresentationFactory.From(
                GgufFitEvaluation(WithSafeSliderModes(
                    OptimizationScreen().Optimization!, wrongRoute)));
        CompatibilityPresentation overBudget =
            CompatibilityPresentationFactory.From(
                GgufFitEvaluation(WithSafeSliderModes(
                    OptimizationScreen().Optimization!,
                    Mode(
                        CompatibilityOptimizationLabelCode.Automatic,
                        slider: null,
                        GgufWeightFormat.Q4KM,
                        OptimizationAssessment.Good,
                        requantisation: false,
                        predictedPeakBytes: 3 * GiB,
                        safeBudgetBytes: 5 * GiB))));
        CompatibilityOptimizationModeView zeroPeakMode = Mode(
            CompatibilityOptimizationLabelCode.Automatic,
            slider: null,
            GgufWeightFormat.Q4KM,
            OptimizationAssessment.Good,
            requantisation: false,
            predictedPeakBytes: 3 * GiB,
            safeBudgetBytes: 4 * GiB);
        typeof(CompatibilityOptimizationModeView).GetField(
            "<PredictedPeakBytes>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(zeroPeakMode, 0UL);
        CompatibilityPresentation zeroPeak =
            CompatibilityPresentationFactory.From(
                GgufFitEvaluation(WithSafeSliderModes(
                    OptimizationScreen().Optimization!,
                    zeroPeakMode)));

        foreach (CompatibilityPresentation presentation in new[]
                 {
                     missing, belowQuality, previewOnly, wrongRouteOnly,
                     overBudget, zeroPeak
                 })
        {
            CompatibilityMemoryOverviewPresentation overview = presentation.MemoryOverview
                ?? throw new AssertFailedException("Current GGUF memory facts must survive a missing optional choice.");
            Assert.IsNull(overview.MinimumRequiredBytes);
            Assert.IsNull(overview.MinimumRequirementLabel);
            Assert.IsTrue(presentation.ShowOptimiseFurtherAction);
            Assert.AreEqual("Optimise further", presentation.OptimiseFurtherActionText);
            Assert.AreEqual("Chat with model now", presentation.CurrentModelChatActionText);
        }
    }

    [TestMethod]
    public void OpenVinoEstimatedCompatible_IgnoresGgufParityProjection()
    {
        AvailableMemorySafetyBudget budget = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes((9 * GiB) / 2));
        CompatibilityMachineMemory memory = CompatibilityMachineMemory.Create(
            TotalPhysicalMemory.FromBytes(16 * GiB),
            budget.Available,
            budget.Reserve,
            budget.Executable);
        CompatibilitySetupView openVinoSetup =
            CompatibilitySetupView.ForPresentation(
                RuntimeRouteId.OpenVinoGenAi,
                CompatibilityBackend.OpenVinoCpu,
                DeviceRouteId.Cpu,
                WeightQuantisation.F16,
                contextTokens: 4096,
                CompatibilityFitState.Safe,
                requiredBytes: 3 * GiB,
                safeBudgetBytes: 4 * GiB,
                headroomBytes: GiB,
                uncertaintyAllowanceBytes: 268_435_456,
                isExperimental: false,
                requiresConversion: false,
                [],
                openVinoKvCache: OpenVinoKvCacheFormat.U8);
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: true,
            continueEnabled: true,
            setup: openVinoSetup);
        CompatibilityPresentation baseline = CompatibilityPresentationFactory.From(
            new CompatibilityEvaluation(screen, null, null)
            {
                MachineMemory = memory
            });
        CompatibilityPresentation withForeignOptional =
            CompatibilityPresentationFactory.From(
                new CompatibilityEvaluation(screen, null, null)
                {
                    MachineMemory = memory,
                    OptionalOptimization = WithSafeSliderModes(
                        OptimizationScreen().Optimization!,
                        Mode(
                            CompatibilityOptimizationLabelCode.Automatic,
                            slider: null,
                            GgufWeightFormat.Q4KM,
                            OptimizationAssessment.Good,
                            requantisation: false))
                });

        CollectionAssert.AreEqual(
            baseline.Facts.ToArray(), withForeignOptional.Facts.ToArray());
        CollectionAssert.AreEqual(
            baseline.RuntimeRows.ToArray(), withForeignOptional.RuntimeRows.ToArray());
        CollectionAssert.AreEqual(
            baseline.CheckRows.ToArray(), withForeignOptional.CheckRows.ToArray());
        CollectionAssert.AreEqual(
            baseline.Recoveries.ToArray(), withForeignOptional.Recoveries.ToArray());
        CollectionAssert.AreEqual(
            baseline.Budget.Segments.ToArray(),
            withForeignOptional.Budget.Segments.ToArray());
        Assert.AreEqual(
            baseline.Budget.SafeLimitBytes,
            withForeignOptional.Budget.SafeLimitBytes);
        Assert.AreEqual(
            baseline.Budget.RequiredBytes,
            withForeignOptional.Budget.RequiredBytes);
        Assert.AreEqual(
            baseline.Budget.ScaleBytes,
            withForeignOptional.Budget.ScaleBytes);
        Assert.AreEqual(
            baseline.Budget.Fits,
            withForeignOptional.Budget.Fits);
        Assert.AreEqual(
            baseline.Budget.LimitingComponent,
            withForeignOptional.Budget.LimitingComponent);
        if (baseline.MachineMemory is not null
            && withForeignOptional.MachineMemory is not null)
        {
            CollectionAssert.AreEqual(
                baseline.MachineMemory.Facts.ToArray(),
                withForeignOptional.MachineMemory.Facts.ToArray());
        }
        Assert.AreEqual(
            baseline with
            {
                Facts = withForeignOptional.Facts,
                RuntimeRows = withForeignOptional.RuntimeRows,
                CheckRows = withForeignOptional.CheckRows,
                Recoveries = withForeignOptional.Recoveries,
                Budget = withForeignOptional.Budget,
                MachineMemory = withForeignOptional.MachineMemory
            },
            withForeignOptional,
            "A GGUF optional projection must not alter an OpenVINO result.");
        Assert.IsFalse(withForeignOptional.ShowOptimiseFurtherAction);
        Assert.AreEqual(string.Empty,
            withForeignOptional.OptimiseFurtherActionText);
        Assert.AreEqual(string.Empty,
            withForeignOptional.CurrentModelChatActionText);
    }

    [TestMethod]
    public void OptimisationRequired_FittingOfferedSetupDoesNotClaimSystemMemoryPressure()
    {
        CompatibilityScreenModel screen = OptimizationScreen(
            modeSafeBudgetBytes: (35 * GiB) / 10,
            modePredictedPeakBytes: (26 * GiB) / 10,
            currentRequiredBytes: (83 * GiB) / 10,
            currentSafeBudgetBytes: (38 * GiB) / 10);

        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(screen);

        Assert.AreEqual(
            CompatibilityMemoryRecoveryReason.None,
            presentation.MemoryRecoveryReason);
        Assert.AreEqual("Choose optimisation", presentation.PrimaryActionText);
        Assert.IsNotNull(presentation.Optimization);

        (string Case, CompatibilityScreenModel Screen)[] nonFittingScreens =
        [
            ("offered budget exceeds current safe budget", OptimizationScreen(
                modeSafeBudgetBytes: 4 * GiB,
                modePredictedPeakBytes: (26 * GiB) / 10,
                currentRequiredBytes: (83 * GiB) / 10,
                currentSafeBudgetBytes: (38 * GiB) / 10)),
            ("all offered modes are below minimum quality", OptimizationScreen(
                modeSafeBudgetBytes: (35 * GiB) / 10,
                modePredictedPeakBytes: (26 * GiB) / 10,
                allModesBelowMinimum: true,
                currentRequiredBytes: (83 * GiB) / 10,
                currentSafeBudgetBytes: (38 * GiB) / 10))
        ];
        foreach ((string caseName, CompatibilityScreenModel nonFitting) in
                 nonFittingScreens)
        {
            CompatibilityPresentation nonFittingPresentation =
                CompatibilityPresentationFactory.From(nonFitting);
            Assert.AreEqual(
                CompatibilityMemoryRecoveryReason.SystemMemoryPressure,
                nonFittingPresentation.MemoryRecoveryReason,
                $"{caseName}; current route={nonFitting.CurrentSetup?.Route}; "
                + $"offered route={nonFitting.Optimization?.Modes[0].Route}; "
                + $"required={nonFitting.CurrentSetup?.SystemSharedRequiredBytes}; "
                + $"safe={nonFitting.CurrentSetup?.SystemSharedSafeBudgetBytes}");
        }

        CompatibilityPresentation routeMismatch =
            CompatibilityPresentationFactory.From(OpenVinoCacheOnlyScreen(
                WeightQuantisation.Q4_K_M,
                currentRoute: RuntimeRouteId.LlamaCpp,
                modeSafeBudgetBytes: (35 * GiB) / 10,
                modePredictedPeakBytes: (26 * GiB) / 10,
                currentRequiredBytes: (83 * GiB) / 10,
                currentSafeBudgetBytes: (38 * GiB) / 10));
        Assert.AreEqual(
            CompatibilityMemoryRecoveryReason.None,
            routeMismatch.MemoryRecoveryReason);
        Assert.IsNull(routeMismatch.Optimization);

        Assert.Throws<ArgumentException>(() => Mode(
            CompatibilityOptimizationLabelCode.Automatic,
            slider: null,
            GgufWeightFormat.Q3KM,
            OptimizationAssessment.Good,
            requantisation: false,
            predictedPeakBytes: 4 * GiB,
            safeBudgetBytes: (35 * GiB) / 10));
        Assert.Throws<ArgumentException>(() =>
            CompatibilityOptimizationModeView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                sliderValue: null,
                OptimizationRoute.Gguf,
                GgufWeightFormat.Q3KM,
                GgufKvCacheFormat.Q8_0,
                openVinoWeights: null,
                openVinoKvCache: null,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Good,
                contextTokens: 4096,
                predictedPeakBytes: (26 * GiB) / 10,
                safeBudgetBytes: (35 * GiB) / 10,
                headroomBytes: (35 * GiB) / 10 - (26 * GiB) / 10,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None,
                isExperimental: false,
                sharedWithAdjacentBand: false,
                dedicatedRequiredBytes: GiB,
                dedicatedSafeBudgetBytes: null,
                dedicatedHeadroomBytes: null));
    }

    [TestMethod]
    public void Q2OnlyAndRequantisation_AreExplicitStrongWarnings()
    {
        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(OptimizationScreen());

        CompatibilityOptimizationModePresentation q2 =
            presentation.Optimization!.Modes.Single(mode =>
                mode.WeightFormat == "Q2_K");

        Assert.IsTrue(q2.HasStrongQualityWarning);
        StringAssert.Contains(q2.WarningText, "significant quality reduction");
        StringAssert.Contains(q2.WarningText, "new copy");
        StringAssert.Contains(q2.WarningText, "original model remains unchanged");
    }

    [TestMethod]
    public void ManualSelection_UsesFlattenedModeWithoutRecalculating()
    {
        CompatibilityScreenModel screen = OptimizationScreen();

        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(
                screen,
                OptimizationPreferenceSelection.Manual(72));

        Assert.AreEqual(72, presentation.Optimization!.SliderValue);
        Assert.AreEqual("High capability", presentation.Optimization.SelectedMode.Label);
        Assert.IsFalse(presentation.Optimization.IsAutomatic);
        Assert.AreEqual("Q4_K_M", presentation.Optimization.SelectedMode.WeightFormat);
    }

    [TestMethod]
    public async Task FixtureOnlyOptimization_RemainsNonActionableWhenPreferenceChanges()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(OptimizationScreen()),
            continueDestinationAvailable: true);

        await viewModel.StartAsync();
        viewModel.SelectManualPreference(72);

        Assert.AreEqual(OptimizationPreferenceKind.Manual,
            viewModel.SelectedPreference!.Kind);
        Assert.AreEqual(72, viewModel.SelectedPreference.PreferenceValue);
        Assert.IsFalse(viewModel.Presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public async Task ManualTicksWithinOneBand_UpdatePreferenceWithoutRepublishing()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(OptimizationScreen()),
            continueDestinationAvailable: true);
        await viewModel.StartAsync();
        int changes = 0;
        viewModel.PresentationChanged += (_, _) => changes++;

        viewModel.SelectManualPreference(21);
        CompatibilityPresentation firstBand = viewModel.Presentation;
        viewModel.SelectManualPreference(29);

        Assert.AreEqual(1, changes);
        Assert.AreSame(firstBand, viewModel.Presentation);
        Assert.AreEqual(29, viewModel.SelectedPreference!.PreferenceValue);

        viewModel.SelectManualPreference(41);
        Assert.AreEqual(2, changes);
        Assert.AreEqual("Balanced", viewModel.Presentation.Optimization!.SelectedMode.Label);
    }

    [TestMethod]
    public void SecondaryActionCopyAndKindStayInTheSameClosedStateMapping()
    {
        CompatibilityPresentation analysing =
            CompatibilityPresentationFactory.Analysing(0);
        Assert.AreEqual("Cancel", analysing.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Cancel,
            analysing.SecondaryActionKind);

        CompatibilityPresentation cancelled =
            CompatibilityPresentationFactory.Cancelled();
        Assert.AreEqual("Check again", cancelled.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Retry,
            cancelled.SecondaryActionKind);
        Assert.AreEqual(
            CompatibilityForwardActionKind.None,
            cancelled.ForwardActionKind);

        CompatibilityPresentation failed =
            CompatibilityPresentationFactory.OperationalFailure();
        Assert.AreEqual(
            CompatibilityForwardActionKind.None,
            failed.ForwardActionKind);

        CompatibilityPresentation unanswered = CompatibilityPresentationFactory.From(
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NotEstablished,
                [],
                [],
                BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: false));
        Assert.AreEqual("Check again", unanswered.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Retry,
            unanswered.SecondaryActionKind);
        Assert.AreEqual(
            CompatibilityForwardActionKind.ImportAnotherModel,
            unanswered.ForwardActionKind);

        CompatibilityPresentation terminal = CompatibilityPresentationFactory.From(
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.EstimatedCompatible,
                [],
                [],
                BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: true));
        Assert.AreEqual("Back", terminal.SecondaryActionText);
        Assert.AreEqual(
            CompatibilitySecondaryActionKind.Back,
            terminal.SecondaryActionKind);
        Assert.AreEqual(
            CompatibilityForwardActionKind.None,
            terminal.ForwardActionKind);
    }

    [TestMethod]
    public async Task SupersededOptimizationAttempt_CannotBecomePreferenceSource()
    {
        TaskCompletionSource<CompatibilityScreenModel> first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        CompatibilityScreenModel newer = OptimizationScreen(WeightQuantisation.Q4_K_M);
        CompatibilityScreenModel older = OptimizationScreen(WeightQuantisation.Q5_K_M);
        int calls = 0;
        var viewModel = new CompatibilityViewModel(_ =>
            ++calls == 1 ? first.Task : Task.FromResult(newer));

        Task staleAttempt = viewModel.StartAsync();
        await viewModel.StartAsync();
        first.SetResult(older);
        await staleAttempt;

        viewModel.SelectManualPreference(72);
        Assert.AreEqual("Q4_K_M",
            viewModel.Presentation.Optimization!.CurrentWeightFormat);

        viewModel.SelectAutomaticPreference();
        Assert.AreEqual("Q4_K_M",
            viewModel.Presentation.Optimization!.CurrentWeightFormat);
    }

    [TestMethod]
    public void OpenVinoUnchangedWeight_WithoutExactCurrentPrecision_FailsClosed()
    {
        CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
            OpenVinoCacheOnlyScreen(WeightQuantisation.Unknown));

        Assert.IsNull(presentation.Optimization);
        Assert.AreEqual("We can't answer this yet", presentation.OutcomeTitle);
        Assert.IsFalse(presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public void OpenVinoUnchangedWeight_WithMismatchedCurrentRoute_FailsClosed()
    {
        CompatibilityPresentation presentation = CompatibilityPresentationFactory.From(
            OpenVinoCacheOnlyScreen(
                WeightQuantisation.Q8_0,
                RuntimeRouteId.LlamaCpp));

        Assert.IsNull(presentation.Optimization);
        Assert.AreEqual("We can't answer this yet", presentation.OutcomeTitle);
        Assert.IsFalse(presentation.PrimaryActionEnabled);
    }

    [TestMethod]
    public void NoFitOpenVinoScreen_DistinguishesRawFromSmallestOptimizedRequirement()
    {
        CompatibilitySetupView raw = CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.OpenVinoGenAi,
            CompatibilityBackend.OpenVinoCpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.F16,
            contextTokens: 4096,
            CompatibilityFitState.DoesNotFit,
            requiredBytes: 8UL * GiB,
            safeBudgetBytes: 2UL * GiB,
            headroomBytes: 0,
            uncertaintyAllowanceBytes: GiB / 2,
            isExperimental: false,
            requiresConversion: false,
            [],
            openVinoKvCache: OpenVinoKvCacheFormat.RouteDefault);
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false,
            setup: raw,
            smallestOptimizedRequiredBytes: 3UL * GiB);

        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(screen);

        StringAssert.Contains(presentation.OutcomeDetail, "current model needs about 8 GB");
        StringAssert.Contains(presentation.OutcomeDetail, "smallest evaluated optimised setup");
        StringAssert.Contains(presentation.OutcomeDetail, "3 GB");
        StringAssert.Contains(presentation.OutcomeDetail, "2 GB");
        Assert.IsFalse(
            presentation.OutcomeDetail.Contains(
                "lightest verified setup", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DiskBlockedOptimisation_DoesNotTellTheUserToFreeMoreRamWhenTheFormatFits()
    {
        CompatibilitySetupView raw = CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.OpenVinoGenAi,
            CompatibilityBackend.OpenVinoCpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.F16,
            contextTokens: 4096,
            CompatibilityFitState.DoesNotFit,
            requiredBytes: 8UL * GiB,
            safeBudgetBytes: 6UL * GiB,
            headroomBytes: 0,
            uncertaintyAllowanceBytes: GiB / 2,
            isExperimental: false,
            requiresConversion: false,
            [],
            openVinoKvCache: OpenVinoKvCacheFormat.RouteDefault);
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false,
            setup: raw,
            smallestOptimizedRequiredBytes: 4UL * GiB,
            optimizationStorageRequirement:
                new CompatibilityStorageRequirementView(
                    RequiredBytes: 3UL * GiB,
                    AvailableBytes: 1UL * GiB));
        AvailableMemorySafetyBudget memory = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes(7UL * GiB));
        CompatibilityEvaluation evaluation = new(
            screen,
            PlanningSession: null,
            CurrentConfiguration: null)
        {
            MachineMemory = CompatibilityMachineMemory.Create(
                TotalPhysicalMemory.FromBytes(16UL * GiB),
                memory.Available,
                memory.Reserve,
                memory.Executable)
        };

        CompatibilityPresentation presentation =
            CompatibilityPresentationFactory.From(evaluation);

        Assert.IsNotNull(presentation.MemoryClarity);
        Assert.AreEqual(0UL, presentation.MemoryClarity.AdditionalFreeRequiredBytes);
        StringAssert.Contains(presentation.OutcomeTitle, "storage");
        StringAssert.Contains(presentation.OutcomeDetail, "2 GB");
        Assert.IsFalse(
            presentation.OutcomeDetail.Contains(
                "free more RAM", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(
            presentation.OutcomeDetail.Contains(
                "free an additional 0", StringComparison.OrdinalIgnoreCase));
    }

    private static CompatibilityScreenModel OptimizationScreen(
        WeightQuantisation currentWeights = WeightQuantisation.Q4_K_M,
        ulong modeSafeBudgetBytes = 4 * GiB,
        ulong modePredictedPeakBytes = 3_221_225_472,
        bool allModesBelowMinimum = false,
        ulong currentRequiredBytes = 5 * GiB,
        ulong currentSafeBudgetBytes = 4 * GiB)
    {
        OptimizationAssessment offeredQuality = allModesBelowMinimum
            ? OptimizationAssessment.Poor
            : OptimizationAssessment.Good;
        CompatibilityOptimizationModeView[] modes =
        [
            Mode(CompatibilityOptimizationLabelCode.Automatic, null,
                GgufWeightFormat.Q3KM, offeredQuality, false,
                modePredictedPeakBytes, modeSafeBudgetBytes),
            Mode(CompatibilityOptimizationLabelCode.MaximumEfficiency, 10,
                GgufWeightFormat.Q2K, OptimizationAssessment.Poor, true,
                modePredictedPeakBytes, modeSafeBudgetBytes),
            Mode(CompatibilityOptimizationLabelCode.Efficient, 30,
                GgufWeightFormat.Q3KM, allModesBelowMinimum
                    ? OptimizationAssessment.Poor
                    : OptimizationAssessment.Acceptable, true,
                modePredictedPeakBytes, modeSafeBudgetBytes),
            Mode(CompatibilityOptimizationLabelCode.Balanced, 50,
                GgufWeightFormat.Q3KM, offeredQuality, false,
                modePredictedPeakBytes, modeSafeBudgetBytes),
            Mode(CompatibilityOptimizationLabelCode.HighCapability, 70,
                GgufWeightFormat.Q4KM, offeredQuality, false,
                modePredictedPeakBytes, modeSafeBudgetBytes),
            Mode(CompatibilityOptimizationLabelCode.MaximumCapability, 90,
                GgufWeightFormat.Q5KM, allModesBelowMinimum
                    ? OptimizationAssessment.Poor
                    : OptimizationAssessment.Excellent, false,
                modePredictedPeakBytes, modeSafeBudgetBytes)
        ];

        CompatibilityOptimizationView optimization =
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                modes,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None);

        return CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.OptimisationRequired,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true,
            CurrentSetup(
                currentWeights,
                currentRequiredBytes,
                currentSafeBudgetBytes),
            optimization);
    }

    private static CompatibilityOptimizationModeView Mode(
        CompatibilityOptimizationLabelCode label,
        int? slider,
        GgufWeightFormat weights,
        OptimizationAssessment quality,
        bool requantisation,
        ulong predictedPeakBytes = 3_221_225_472,
        ulong safeBudgetBytes = 4 * GiB) =>
        CompatibilityOptimizationModeView.ForPresentation(
            label,
            slider,
            OptimizationRoute.Gguf,
            weights,
            GgufKvCacheFormat.Q8_0,
            null,
            null,
            DeviceRouteId.Cpu,
            quality,
            contextTokens: 4096,
            predictedPeakBytes: predictedPeakBytes,
            safeBudgetBytes: safeBudgetBytes,
            headroomBytes: safeBudgetBytes > predictedPeakBytes
                ? safeBudgetBytes - predictedPeakBytes : 0,
            requiresPersistentArtifact: requantisation,
            requiresRequantisationAcknowledgement: requantisation,
            qualityNotice: requantisation
                ? OptimizationQualityNotice.SignificantQualityReduction
                : OptimizationQualityNotice.None,
            isExperimental: false,
            sharedWithAdjacentBand: false);

    private static CompatibilityScreenModel OpenVinoCacheOnlyScreen(
        WeightQuantisation currentWeights,
        RuntimeRouteId currentRoute = RuntimeRouteId.OpenVinoGenAi,
        ulong modeSafeBudgetBytes = 4 * GiB,
        ulong modePredictedPeakBytes = 3_221_225_472,
        ulong currentRequiredBytes = 5 * GiB,
        ulong currentSafeBudgetBytes = 4 * GiB,
        OpenVinoKvCacheFormat currentOpenVinoCache = OpenVinoKvCacheFormat.U8)
    {
        CompatibilityOptimizationLabelCode[] labels =
        [
            CompatibilityOptimizationLabelCode.Automatic,
            CompatibilityOptimizationLabelCode.MaximumEfficiency,
            CompatibilityOptimizationLabelCode.Efficient,
            CompatibilityOptimizationLabelCode.Balanced,
            CompatibilityOptimizationLabelCode.HighCapability,
            CompatibilityOptimizationLabelCode.MaximumCapability
        ];
        int?[] sliders = [null, 10, 30, 50, 70, 90];
        CompatibilityOptimizationModeView[] modes = labels
            .Select((label, index) => CompatibilityOptimizationModeView.ForPresentation(
                label,
                sliders[index],
                OptimizationRoute.OpenVino,
                null,
                null,
                OpenVinoWeightFormat.Original,
                OpenVinoKvCacheFormat.U4,
                DeviceRouteId.Cpu,
                OptimizationAssessment.Excellent,
                contextTokens: 4096,
                predictedPeakBytes: modePredictedPeakBytes,
                safeBudgetBytes: modeSafeBudgetBytes,
                headroomBytes: modeSafeBudgetBytes > modePredictedPeakBytes
                    ? modeSafeBudgetBytes - modePredictedPeakBytes : 0,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                qualityNotice: OptimizationQualityNotice.None,
                isExperimental: false,
                sharedWithAdjacentBand: false))
            .ToArray();
        CompatibilityOptimizationView optimization =
            CompatibilityOptimizationView.ForPresentation(
                CompatibilityOptimizationLabelCode.Automatic,
                null,
                modes,
                requiresPersistentArtifact: false,
                requiresRequantisationAcknowledgement: false,
                OptimizationQualityNotice.None);
        CompatibilitySetupView current = CompatibilitySetupView.ForPresentation(
            currentRoute,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            currentWeights,
            contextTokens: 4096,
            CompatibilityFitState.DoesNotFit,
            requiredBytes: currentRequiredBytes,
            safeBudgetBytes: currentSafeBudgetBytes,
            headroomBytes: 0,
            uncertaintyAllowanceBytes: 268_435_456,
            isExperimental: false,
            requiresConversion: false,
            [],
            ggufKvCache: currentRoute == RuntimeRouteId.LlamaCpp
                ? GgufKvCacheFormat.F16
                : null,
            openVinoKvCache: currentRoute == RuntimeRouteId.OpenVinoGenAi
                ? currentOpenVinoCache
                : null);

        return CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.OptimisationRequired,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: true,
            setup: current,
            optimization: optimization);
    }

    private static CompatibilitySetupView CurrentSetup(
        WeightQuantisation weights,
        ulong requiredBytes = 5 * GiB,
        ulong safeBudgetBytes = 4 * GiB) =>
        CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            weights,
            contextTokens: 4096,
            CompatibilityFitState.DoesNotFit,
            requiredBytes: requiredBytes,
            safeBudgetBytes: safeBudgetBytes,
            headroomBytes: 0,
            uncertaintyAllowanceBytes: 268_435_456,
            isExperimental: false,
            requiresConversion: false,
            [],
            ggufKvCache: GgufKvCacheFormat.F16);

    private static CompatibilitySetupView CompatibleSetup() =>
        CompatibilitySetupView.ForPresentation(
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.Q4_K_M,
            contextTokens: 4096,
            CompatibilityFitState.Safe,
            requiredBytes: 3 * GiB,
            safeBudgetBytes: 4 * GiB,
            headroomBytes: GiB,
            uncertaintyAllowanceBytes: 268_435_456,
            isExperimental: false,
            requiresConversion: false,
            [],
            ggufKvCache: GgufKvCacheFormat.F16);

    private static CompatibilityEvaluation GgufFitEvaluation(
        CompatibilityOptimizationView? optionalOptimization)
    {
        AvailableMemorySafetyBudget budget = SystemMemoryBudgetCalculator.Calculate(
            CurrentlyAvailableMemory.FromBytes((9 * GiB) / 2));
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.EstimatedCompatible,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: true,
            continueEnabled: true,
            setup: CompatibleSetup());
        return new CompatibilityEvaluation(screen, null, null)
        {
            MachineMemory = CompatibilityMachineMemory.Create(
                TotalPhysicalMemory.FromBytes(16 * GiB),
                budget.Available,
                budget.Reserve,
                budget.Executable),
            OptionalOptimization = optionalOptimization
        };
    }

    private static CompatibilityOptimizationView WithSafeSliderModes(
        CompatibilityOptimizationView source,
        params CompatibilityOptimizationModeView[] modes)
    {
        CompatibilityExactOptimizationModeView[] safe = modes
            .Select((mode, index) => Assert.IsInstanceOfType<CompatibilityExactOptimizationModeView>(
                typeof(CompatibilityExactOptimizationModeView)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(constructor => constructor.GetParameters() is
                        [{ ParameterType: var identityType },
                         { ParameterType: var modeType }]
                        && identityType == typeof(string)
                        && modeType == typeof(CompatibilityOptimizationModeView))
                    .Invoke([new string((char)('a' + index), 64), mode])))
            .ToArray();
        return Assert.IsInstanceOfType<CompatibilityOptimizationView>(
            typeof(CompatibilityOptimizationView)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(constructor => constructor.GetParameters().Length == 10)
                .Invoke([
                    source.RecommendedLabelCode,
                    source.RecommendedSliderValue,
                    source.Modes,
                    source.RequiresPersistentArtifact,
                    source.RequiresRequantisationAcknowledgement,
                    source.QualityNotice,
                    safe,
                    false,
                    safe,
                    safe.Length > 0 ? 0 : null
                ]));
    }
}
