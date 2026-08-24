# Model/Hardware Compatibility Mode Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rank evaluated candidates into the four optimisation modes, each by a lexicographic comparison key rather than a weighted score, and report why a mode is unavailable when it is.

**Architecture:** A candidate is evaluated once — estimate, per-pool peaks, fit verdict — producing an `EvaluatedCandidate`. Hard gates admit only candidates a mode may consider. Each mode is an ordered list of comparison factors; the first factor that separates two candidates decides, and a fingerprint tiebreak guarantees a total order. Every selection records the ordered factors that produced it, so the future mode-selection screen is a view over data the engine already emitted.

**Tech Stack:** C# 12, .NET 8 (`net8.0`), MSTest 4.3.2 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-design.md`, section 11.

**Predecessors (complete):** core foundation, candidate vocabulary, GGUF estimator, support matrix and generator.

Suite command, referred to below as **the suite command**:

```bash
dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj
```

## Global Constraints

- Target `net8.0`, `Nullable` enabled, `TreatWarningsAsErrors` true. No WinUI, no Windows-only API.
- Types are `internal`; the test project has `InternalsVisibleTo`.
- Every enum reserves a zero member, and the zero member is the safe or unknown value.
- **Unknown is a typed unavailable value, never zero, never a default.**
- **A mode is never silently absent.** It is `Available` with a candidate, `Unavailable` with a stable reason, or `NotEstablished`. Presentation disables it with an explanation; it is never hidden.
- Selection must be **deterministic**: a total order, fingerprint-tiebroken, independent of input order and culture.
- Bias is one-directional: a candidate that is not `Safe` or `Narrow` never reaches a mode.
- MSTest 4: `[TestMethod]` with `[DataRow]`; `DataTestMethod` fails the build.
- A public test method cannot take an `internal` enum parameter (CS0051).
- No absolute path, filename, model name, hostname, credential or raw tool output in any type, message or test. A new string member on a Core type trips the assembly privacy canary — either allowlist it with a justification or treat it as a real leak; never narrow the scan.

## Design decisions taken in this plan

**Performance is not yet a usable factor.** Section 11 lists performance in three of the four orderings, but nothing in the system measures throughput. Rather than invent a proxy, `PerformanceIndicator` is a typed `NotEstablished` today: the comparator treats every candidate as equal on that factor and the selection records `PerformanceNotEstablished` in its factor list. That keeps the ordering honest and makes the factor live as soon as a runtime verification result exists.

**The worst pool is the system pool today.** Section 11 says the worst pool controls the pressure ratio. `FitPolicy` currently gates system memory only, so the worst pool is the only pool. The comparator is written over a per-pool collection so it does not need rewriting when the device and storage gates arrive; today that collection has one member.

**Quality tier is bits per weight.** The effective encoding of a candidate is its own when it converts and the imported file's when it does not, and its bits-per-weight value orders the quality tier directly. This is the same table the estimator scales with, so quality and size stay consistent.

## File Structure

```
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/
├── Domain/
│   └── EvidenceGrade.cs                        Task 1
└── Application/ModeSelection/
    ├── PerformanceIndicator.cs                 Task 1
    ├── EvaluatedCandidate.cs                   Task 1
    ├── ModeAdmissionReason.cs                  Task 2
    ├── ModeAdmission.cs                        Task 2
    ├── CompatibilityMode.cs                    Task 3
    ├── SelectionFactor.cs                      Task 3
    ├── ModeAvailability.cs                     Task 3
    ├── CompatibilityModeSelection.cs           Task 3
    ├── ModeComparers.cs                        Task 4
    └── ModeSelector.cs                         Task 5

tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/
├── Application/ModeSelection/
│   ├── ModeAdmissionTests.cs                   Task 2
│   ├── ModeComparerTests.cs                    Task 4
│   └── ModeSelectorTests.cs                    Task 5
└── Invariants/
    └── ModeSelectionInvariantTests.cs          Task 6
```

---

### Task 1: Evaluated candidate

What a mode ranks: a candidate together with everything already computed about it. Evaluation happens once per candidate per run so every mode compares the same numbers.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/EvidenceGrade.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/PerformanceIndicator.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/EvaluatedCandidate.cs`
- Test: covered by Tasks 2, 4 and 5; no test file of its own.

**Interfaces:**
- Consumes: `CompatibilityCandidate`, `ResourceEstimate`, `ResourcePeakProfile`, `FitAssessment`, `WeightQuantisation`, `ByteCount`.
- Produces: `EvidenceGrade { Unknown = 0, Estimated, Measured, Verified }`; `PerformanceIndicator.NotEstablished()` with `IsEstablished`; `EvaluatedCandidate.Create(...)` with properties `Candidate`, `Estimate`, `Peaks`, `Fit`, `EffectiveQuantisation`, `Evidence`, `Performance`, `AddedStorageBytes`, and the convenience accessors `Context`, `Fingerprint`, `IsExperimental`, `Preparation`. Consumed by Tasks 2, 4, 5 and 6.

- [ ] **Step 1: Create the evidence grade**

Create `Domain/EvidenceGrade.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// How much is actually known about a result, ordered weakest to strongest.
///
/// A provisional policy pins everything built on it to Estimated. Higher grades
/// become reachable only when a recorded measurement exists, which is what stops
/// a calculated number being presented as an observed one.
/// </summary>
internal enum EvidenceGrade
{
    Unknown = 0,

    /// <summary>Calculated from documented defaults, not measured.</summary>
    Estimated,

    /// <summary>Backed by a recorded predicted-versus-measured dataset.</summary>
    Measured,

    /// <summary>Confirmed by a runtime verification run on this machine.</summary>
    Verified
}
```

- [ ] **Step 2: Create the performance indicator**

Create `Application/ModeSelection/PerformanceIndicator.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Throughput, when it is known.
///
/// Section 11 lists performance as an ordering factor in three of the four
/// modes, but nothing in the system measures it yet. Rather than invent a proxy
/// — which would silently decide real rankings on a fabricated number — this is
/// a typed absence. Comparators treat every candidate as equal on it and the
/// selection records that the factor was not established.
/// </summary>
internal sealed record PerformanceIndicator
{
    private PerformanceIndicator(bool isEstablished, decimal tokensPerSecond)
    {
        IsEstablished = isEstablished;
        TokensPerSecond = tokensPerSecond;
    }

    internal bool IsEstablished { get; }

    /// <summary>Meaningful only when <see cref="IsEstablished"/> is true.</summary>
    internal decimal TokensPerSecond { get; }

    internal static PerformanceIndicator NotEstablished() => new(false, 0m);

    internal static PerformanceIndicator Measured(decimal tokensPerSecond)
    {
        if (tokensPerSecond <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokensPerSecond),
                "A measured throughput must be positive; zero is not a measurement.");
        }

        return new PerformanceIndicator(true, tokensPerSecond);
    }
}
```

- [ ] **Step 3: Create the evaluated candidate**

Create `Application/ModeSelection/EvaluatedCandidate.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// One candidate with everything already computed about it.
///
/// Evaluation happens once per candidate per run, so all four modes rank the
/// same numbers against the same single fresh-memory observation. Recomputing
/// per mode would let two modes disagree about the same configuration.
/// </summary>
internal sealed record EvaluatedCandidate
{
    private EvaluatedCandidate(
        CompatibilityCandidate candidate,
        ResourceEstimate estimate,
        ResourcePeakProfile peaks,
        FitAssessment.FitAssessment fit,
        WeightQuantisation effectiveQuantisation,
        EvidenceGrade evidence,
        PerformanceIndicator performance,
        ByteCount addedStorageBytes)
    {
        Candidate = candidate;
        Estimate = estimate;
        Peaks = peaks;
        Fit = fit;
        EffectiveQuantisation = effectiveQuantisation;
        Evidence = evidence;
        Performance = performance;
        AddedStorageBytes = addedStorageBytes;
    }

    internal CompatibilityCandidate Candidate { get; }

    internal ResourceEstimate Estimate { get; }

    internal ResourcePeakProfile Peaks { get; }

    internal FitAssessment.FitAssessment Fit { get; }

    /// <summary>
    /// The encoding this configuration actually runs: its own when it converts,
    /// the imported file's when it does not.
    /// </summary>
    internal WeightQuantisation EffectiveQuantisation { get; }

    internal EvidenceGrade Evidence { get; }

    internal PerformanceIndicator Performance { get; }

    /// <summary>Disk a conversion would newly occupy. Zero when nothing is written.</summary>
    internal ByteCount AddedStorageBytes { get; }

    internal ContextTokenCount Context => Candidate.Context;

    internal CandidateFingerprint Fingerprint => Candidate.Fingerprint;

    internal bool IsExperimental => Candidate.IsExperimental;

    internal CandidatePreparation Preparation => Candidate.Preparation;

    internal static EvaluatedCandidate Create(
        CompatibilityCandidate candidate,
        ResourceEstimate estimate,
        ResourcePeakProfile peaks,
        FitAssessment.FitAssessment fit,
        WeightQuantisation effectiveQuantisation,
        EvidenceGrade evidence,
        PerformanceIndicator performance,
        ByteCount addedStorageBytes)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(estimate);
        ArgumentNullException.ThrowIfNull(peaks);
        ArgumentNullException.ThrowIfNull(fit);
        ArgumentNullException.ThrowIfNull(performance);

        if (evidence == EvidenceGrade.Unknown)
        {
            throw new ArgumentException(
                "An evaluated candidate must carry a grade; Unknown would let an "
                + "ungraded result compete against a measured one.",
                nameof(evidence));
        }

        return new EvaluatedCandidate(
            candidate,
            estimate,
            peaks,
            fit,
            effectiveQuantisation,
            evidence,
            performance,
            addedStorageBytes);
    }
}
```

- [ ] **Step 4: Build and confirm nothing broke**

Run the suite command. Expected: PASS, count unchanged — three new types with no consumers.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/EvidenceGrade.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/
git commit -m "feat(compatibility): add the evaluated candidate mode selection ranks"
```

---

### Task 2: The admission gates

Section 11's hard gates, applied before any candidate reaches a comparison. A candidate that fails a gate is not ranked worse — it is not ranked at all.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeAdmissionReason.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeAdmission.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeAdmissionTests.cs`

**Interfaces:**
- Consumes: `EvaluatedCandidate` (Task 1), `CompatibilityFitState`, `EvidenceGrade`.
- Produces: `ModeAdmissionReason { None = 0, FitStateNotSafeOrNarrow, EvidenceBelowAdmissionLevel, BackendMismatch, EstimateNotEstablished }`; `ModeAdmission.IsAdmitted(EvaluatedCandidate candidate, bool entryRequiresEvidence, out ModeAdmissionReason reason)` returning `bool`. Consumed by Tasks 5 and 6.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeAdmissionTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.ModeSelection;

[TestClass]
public sealed class ModeAdmissionTests
{
    private static EvaluatedCandidate Candidate(
        CompatibilityFitState state = CompatibilityFitState.Safe,
        EvidenceGrade evidence = EvidenceGrade.Estimated,
        EstimationStatus estimateStatus = EstimationStatus.Established)
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None,
            supportEntryId: "entry-1",
            isExperimental: false,
            isBaseline: true);

        ResourceEstimate estimate = estimateStatus == EstimationStatus.Established
            ? ResourceEstimate.Established(
                [
                    ResourceComponent.Create(
                        ResourceComponentKind.Weights,
                        ResourceTarget.SystemMemory,
                        ByteCount.FromBytes(1024),
                        new HashSet<LifecyclePhase> { LifecyclePhase.Load })
                ],
                new HashSet<EstimationLimitation>())
            : ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.UnknownArchitecture);

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                state,
                FitLimitingReason.None,
                ByteCount.FromBytes(8192),
                ByteCount.FromBytes(1024),
                ByteCount.FromBytes(7168),
                PressureRatio: 0.125m),
            WeightQuantisation.Q4_K_M,
            evidence,
            PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityFitState.Safe), true)]
    [DataRow(nameof(CompatibilityFitState.Narrow), true)]
    [DataRow(nameof(CompatibilityFitState.DoesNotFit), false)]
    [DataRow(nameof(CompatibilityFitState.Unsupported), false)]
    [DataRow(nameof(CompatibilityFitState.NotEstablished), false)]
    public void IsAdmitted_OnlyAdmitsSafeOrNarrow(string state, bool expected)
    {
        bool admitted = ModeAdmission.IsAdmitted(
            Candidate(Enum.Parse<CompatibilityFitState>(state)),
            entryRequiresEvidence: false,
            out ModeAdmissionReason reason);

        Assert.AreEqual(expected, admitted);

        if (!expected)
        {
            Assert.AreEqual(
                nameof(ModeAdmissionReason.FitStateNotSafeOrNarrow), reason.ToString());
        }
    }

    [TestMethod]
    public void IsAdmitted_RefusesWhenAnEntryNeedsEvidenceAndOnlyAnEstimateExists()
    {
        // Section 11: where an entry declares a threshold but no evidence exists
        // to test it, the candidate is unsupported rather than optimistically
        // admitted on a calculated number.
        bool admitted = ModeAdmission.IsAdmitted(
            Candidate(evidence: EvidenceGrade.Estimated),
            entryRequiresEvidence: true,
            out ModeAdmissionReason reason);

        Assert.IsFalse(admitted);
        Assert.AreEqual(
            nameof(ModeAdmissionReason.EvidenceBelowAdmissionLevel), reason.ToString());
    }

    [TestMethod]
    [DataRow(nameof(EvidenceGrade.Measured))]
    [DataRow(nameof(EvidenceGrade.Verified))]
    public void IsAdmitted_AdmitsAnEvidenceRequiringEntryOnceMeasured(string grade)
    {
        Assert.IsTrue(ModeAdmission.IsAdmitted(
            Candidate(evidence: Enum.Parse<EvidenceGrade>(grade)),
            entryRequiresEvidence: true,
            out _));
    }

    [TestMethod]
    public void IsAdmitted_DoesNotRequireEvidenceWhenTheEntryDeclaresNoThreshold()
    {
        Assert.IsTrue(ModeAdmission.IsAdmitted(
            Candidate(evidence: EvidenceGrade.Estimated),
            entryRequiresEvidence: false,
            out _));
    }

    [TestMethod]
    public void IsAdmitted_RefusesAnUnestablishedEstimate()
    {
        // Without an estimate there is no requirement to compare, so the fit
        // verdict beside it cannot mean anything.
        bool admitted = ModeAdmission.IsAdmitted(
            Candidate(estimateStatus: EstimationStatus.NotEstablished),
            entryRequiresEvidence: false,
            out ModeAdmissionReason reason);

        Assert.IsFalse(admitted);
        Assert.AreEqual(
            nameof(ModeAdmissionReason.EstimateNotEstablished), reason.ToString());
    }

    [TestMethod]
    public void IsAdmitted_ReportsNoReasonWhenItAdmits()
    {
        ModeAdmission.IsAdmitted(Candidate(), false, out ModeAdmissionReason reason);

        Assert.AreEqual(nameof(ModeAdmissionReason.None), reason.ToString());
    }

    [TestMethod]
    public void IsAdmitted_ChecksTheEstimateBeforeTheFitState()
    {
        // An unestablished estimate produces a NotEstablished fit state too. The
        // more specific reason is the useful one to show a user.
        ModeAdmission.IsAdmitted(
            Candidate(
                state: CompatibilityFitState.NotEstablished,
                estimateStatus: EstimationStatus.NotEstablished),
            entryRequiresEvidence: false,
            out ModeAdmissionReason reason);

        Assert.AreEqual(
            nameof(ModeAdmissionReason.EstimateNotEstablished), reason.ToString());
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command. Expected: build failure — `ModeAdmission` not found.

- [ ] **Step 3: Implement the reason enum**

Create `Application/ModeSelection/ModeAdmissionReason.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Why a candidate never reached a mode's comparison. Stable codes, never free
/// text, so a disabled mode can explain itself without leaking anything.
/// </summary>
internal enum ModeAdmissionReason
{
    None = 0,

    /// <summary>No estimate exists, so there is no requirement to compare.</summary>
    EstimateNotEstablished,

    /// <summary>The candidate does not fit, or fitting could not be established.</summary>
    FitStateNotSafeOrNarrow,

    /// <summary>
    /// The entry declares a quality, performance or stability threshold and no
    /// evidence exists to test it against.
    /// </summary>
    EvidenceBelowAdmissionLevel,

    /// <summary>The requested backend is not the one the candidate binds.</summary>
    BackendMismatch
}
```

- [ ] **Step 4: Implement the gate**

Create `Application/ModeSelection/ModeAdmission.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Section 11's hard gates.
///
/// A candidate that fails one of these is not ranked worse — it is not ranked at
/// all. Letting a does-not-fit candidate compete and lose would mean it could win
/// whenever nothing better existed, which is exactly the false-safe result the
/// whole design is built to avoid.
/// </summary>
internal static class ModeAdmission
{
    internal static bool IsAdmitted(
        EvaluatedCandidate candidate,
        bool entryRequiresEvidence,
        out ModeAdmissionReason reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // Checked first because an unestablished estimate also produces a
        // NotEstablished fit state, and the estimate is the more specific answer.
        if (candidate.Estimate.Status != EstimationStatus.Established)
        {
            reason = ModeAdmissionReason.EstimateNotEstablished;
            return false;
        }

        if (candidate.Fit.State is not (CompatibilityFitState.Safe
            or CompatibilityFitState.Narrow))
        {
            reason = ModeAdmissionReason.FitStateNotSafeOrNarrow;
            return false;
        }

        // A declared threshold with nothing to test it against is not a pass.
        if (entryRequiresEvidence && candidate.Evidence < EvidenceGrade.Measured)
        {
            reason = ModeAdmissionReason.EvidenceBelowAdmissionLevel;
            return false;
        }

        reason = ModeAdmissionReason.None;
        return true;
    }
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run the suite command. Expected: PASS — every test green, including the 13 added here.

- [ ] **Step 6: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeAdmission.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeAdmissionReason.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeAdmissionTests.cs
git commit -m "feat(compatibility): gate candidates before mode selection"
```

---

### Task 3: Selection vocabulary

What a mode reports: which candidate it chose, whether it could choose at all, and the ordered factors that decided it. Section 11 requires the factor list so a later screen is a view over data the engine already produced rather than a second round of derivation.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/CompatibilityMode.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/SelectionFactor.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeAvailability.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/CompatibilityModeSelection.cs`
- Test: covered by Task 5.

**Interfaces:**
- Consumes: `CandidateFingerprint`, `ModeAdmissionReason` (Task 2).
- Produces: `CompatibilityMode { Unspecified = 0, Automatic, Quality, Balanced, Efficiency }`; `SelectionFactor` enum; `ModeAvailability { NotEstablished = 0, Available, Unavailable }`; `CompatibilityModeSelection.Available(mode, fingerprint, factors)` / `.Unavailable(mode, reason)` / `.NotEstablished(mode)` with properties `Mode`, `Availability`, `SelectedFingerprint`, `Reason`, `Factors`. Consumed by Tasks 5 and 6.

- [ ] **Step 1: Create the four vocabulary types**

Create `Application/ModeSelection/CompatibilityMode.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// The four optimisation intents a user can express. "Use current model" is not
/// one of them: it is a separate action that creates a runtime profile rather
/// than choosing between alternatives.
/// </summary>
internal enum CompatibilityMode
{
    Unspecified = 0,
    Automatic,
    Quality,
    Balanced,
    Efficiency
}
```

Create `Application/ModeSelection/SelectionFactor.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// One comparison step in a mode's ordering, recorded in the order it was
/// applied. The list is what lets a screen say why this configuration won
/// without re-deriving the decision.
/// </summary>
internal enum SelectionFactor
{
    Unspecified = 0,
    PreservesRequestedContext,
    QualityTier,
    EvidenceGrade,
    NonExperimental,
    LeastDestructivePreparation,
    Performance,
    Headroom,
    WorstPoolPressureRatio,
    AddedStorage,
    MaximiseContext,
    DistanceFromImportedConfiguration,
    Fingerprint,

    /// <summary>
    /// Recorded when performance was part of the ordering but nothing measured
    /// it, so the factor separated nothing.
    /// </summary>
    PerformanceNotEstablished
}
```

Create `Application/ModeSelection/ModeAvailability.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Whether a mode could pick anything. A mode is never silently missing: it is
/// available with a candidate, unavailable with a reason, or not established.
/// </summary>
internal enum ModeAvailability
{
    NotEstablished = 0,
    Available,
    Unavailable
}
```

Create `Application/ModeSelection/CompatibilityModeSelection.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// One mode's answer.
///
/// An unavailable mode carries a reason so presentation can disable it with an
/// explanation rather than hiding it — a hidden option looks like an option that
/// never existed, which is a different and misleading claim.
/// </summary>
internal sealed record CompatibilityModeSelection
{
    private CompatibilityModeSelection(
        CompatibilityMode mode,
        ModeAvailability availability,
        CandidateFingerprint? selectedFingerprint,
        ModeAdmissionReason reason,
        IReadOnlyList<SelectionFactor> factors)
    {
        Mode = mode;
        Availability = availability;
        SelectedFingerprint = selectedFingerprint;
        Reason = reason;
        Factors = factors;
    }

    internal CompatibilityMode Mode { get; }

    internal ModeAvailability Availability { get; }

    /// <summary>Set only when available.</summary>
    internal CandidateFingerprint? SelectedFingerprint { get; }

    internal ModeAdmissionReason Reason { get; }

    /// <summary>The ordering applied, in the order it was applied.</summary>
    internal IReadOnlyList<SelectionFactor> Factors { get; }

    internal static CompatibilityModeSelection Available(
        CompatibilityMode mode,
        CandidateFingerprint selectedFingerprint,
        IReadOnlyList<SelectionFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        RequireMode(mode);

        if (factors.Count == 0)
        {
            throw new ArgumentException(
                "An available mode must record the ordering that decided it, or a "
                + "screen cannot explain the choice without re-deriving it.",
                nameof(factors));
        }

        return new CompatibilityModeSelection(
            mode, ModeAvailability.Available, selectedFingerprint,
            ModeAdmissionReason.None, [.. factors]);
    }

    internal static CompatibilityModeSelection Unavailable(
        CompatibilityMode mode,
        ModeAdmissionReason reason)
    {
        RequireMode(mode);

        if (reason == ModeAdmissionReason.None)
        {
            throw new ArgumentException(
                "An unavailable mode must name why, so it can be disabled with an "
                + "accessible explanation rather than hidden.",
                nameof(reason));
        }

        return new CompatibilityModeSelection(
            mode, ModeAvailability.Unavailable, null, reason, []);
    }

    internal static CompatibilityModeSelection NotEstablished(CompatibilityMode mode)
    {
        RequireMode(mode);

        return new CompatibilityModeSelection(
            mode, ModeAvailability.NotEstablished, null, ModeAdmissionReason.None, []);
    }

    private static void RequireMode(CompatibilityMode mode)
    {
        if (mode == CompatibilityMode.Unspecified)
        {
            throw new ArgumentException(
                "A selection must name its mode.", nameof(mode));
        }
    }
}
```

- [ ] **Step 2: Build and commit**

Run the suite command. Expected: PASS, count unchanged.

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/
git commit -m "feat(compatibility): add the mode selection vocabulary"
```

---

### Task 4: The four comparison keys

Each mode is a lexicographic ordering, not a weighted score: the first factor that separates two candidates decides, and later factors never outvote earlier ones. A fingerprint tiebreak makes every ordering total, so selection cannot depend on input order.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeComparers.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeComparerTests.cs`

**Interfaces:**
- Consumes: `EvaluatedCandidate` (Task 1), `SelectionFactor` (Task 3), `WeightQuantisationMap`.
- Produces: `ModeComparers.For(CompatibilityMode mode, ContextTokenCount preservationTarget, GgufRouteConfiguration baselineConfiguration)` returning `IComparer<EvaluatedCandidate>` where **less is better**; and `ModeComparers.FactorsFor(CompatibilityMode mode)` returning `IReadOnlyList<SelectionFactor>`. Consumed by Tasks 5 and 6.

> **Direction convention.** Every comparer orders best-first: `Compare(a, b) < 0` means `a` is the better candidate. Sorting a list with it and taking index 0 yields the winner.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeComparerTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.ModeSelection;

[TestClass]
public sealed class ModeComparerTests
{
    private static GgufRouteConfiguration Configuration(
        GgufWeightFormat weights = GgufWeightFormat.Imported,
        GgufKvCacheFormat kv = GgufKvCacheFormat.F16,
        DeviceRouteId device = DeviceRouteId.Cpu,
        CompatibilityBackend backend = CompatibilityBackend.Cpu,
        GpuOffloadLevel offload = GpuOffloadLevel.None) =>
        GgufRouteConfiguration.Create(weights, kv, backend, device, offload);

    private static EvaluatedCandidate Candidate(
        int context = 4096,
        WeightQuantisation quantisation = WeightQuantisation.Q4_K_M,
        EvidenceGrade evidence = EvidenceGrade.Estimated,
        bool experimental = false,
        CandidatePreparation preparation = CandidatePreparation.None,
        decimal pressureRatio = 0.5m,
        ulong headroom = 1_000_000,
        ulong addedStorage = 0,
        GgufRouteConfiguration? configuration = null,
        string entryId = "entry-1")
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            configuration ?? Configuration(),
            ContextTokenCount.FromTokens(context),
            preparation,
            entryId,
            experimental,
            isBaseline: false);

        ResourceEstimate estimate = ResourceEstimate.Established(
            [
                ResourceComponent.Create(
                    ResourceComponentKind.Weights,
                    ResourceTarget.SystemMemory,
                    ByteCount.FromBytes(1024),
                    new HashSet<LifecyclePhase> { LifecyclePhase.Load })
            ],
            new HashSet<EstimationLimitation>());

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                CompatibilityFitState.Safe,
                FitLimitingReason.None,
                ByteCount.FromBytes(8192),
                ByteCount.FromBytes(1024),
                ByteCount.FromBytes(headroom),
                pressureRatio),
            quantisation,
            evidence,
            PerformanceIndicator.NotEstablished(),
            ByteCount.FromBytes(addedStorage));
    }

    private static IComparer<EvaluatedCandidate> Comparer(
        CompatibilityMode mode, int preservation = 4096) =>
        ModeComparers.For(
            mode, ContextTokenCount.FromTokens(preservation), Configuration());

    private static void AssertPrefers(
        IComparer<EvaluatedCandidate> comparer,
        EvaluatedCandidate better,
        EvaluatedCandidate worse)
    {
        Assert.IsTrue(comparer.Compare(better, worse) < 0, "Expected the first to win.");
        Assert.IsTrue(comparer.Compare(worse, better) > 0, "Comparison must be antisymmetric.");
    }

    [TestMethod]
    public void Quality_PrefersPreservingTheRequestedContext()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(context: 4096),
            Candidate(context: 2048));
    }

    [TestMethod]
    public void Quality_PrefersAHigherQualityTierOnceContextTies()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(quantisation: WeightQuantisation.Q8_0),
            Candidate(quantisation: WeightQuantisation.Q4_K_M));
    }

    [TestMethod]
    public void Quality_PutsContextAheadOfQuality()
    {
        // Lexicographic, not weighted: a better quality tier cannot buy back a
        // context the user asked to keep.
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(context: 4096, quantisation: WeightQuantisation.Q3_K_M),
            Candidate(context: 2048, quantisation: WeightQuantisation.Q8_0));
    }

    [TestMethod]
    public void Quality_PrefersNonExperimentalOnceQualityAndEvidenceTie()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(experimental: false),
            Candidate(experimental: true));
    }

    [TestMethod]
    public void Quality_PrefersTheLeastDestructivePreparation()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Quality),
            Candidate(preparation: CandidatePreparation.None),
            Candidate(preparation: CandidatePreparation.WeightConversionRequired));
    }

    [TestMethod]
    public void Efficiency_PrefersTheLowerWorstPoolPressureRatio()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(pressureRatio: 0.25m),
            Candidate(pressureRatio: 0.75m));
    }

    [TestMethod]
    public void Efficiency_PutsPressureAheadOfContext()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(pressureRatio: 0.25m, context: 1024),
            Candidate(pressureRatio: 0.75m, context: 8192));
    }

    [TestMethod]
    public void Efficiency_PrefersLessAddedStorageOncePressureTies()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(addedStorage: 0),
            Candidate(addedStorage: 5_000_000_000));
    }

    [TestMethod]
    public void Efficiency_PrefersMoreContextOncePressureAndStorageTie()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Efficiency),
            Candidate(context: 8192),
            Candidate(context: 2048));
    }

    [TestMethod]
    public void Automatic_PrefersAvoidingAWeightConversion()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Automatic),
            Candidate(preparation: CandidatePreparation.RuntimeProfileOnly),
            Candidate(preparation: CandidatePreparation.WeightConversionRequired));
    }

    [TestMethod]
    public void Automatic_PrefersTheConfigurationClosestToTheImportedOne()
    {
        // Two changes from the baseline lose to one, all else equal.
        AssertPrefers(
            Comparer(CompatibilityMode.Automatic),
            Candidate(configuration: Configuration(kv: GgufKvCacheFormat.Q8_0)),
            Candidate(configuration: Configuration(
                kv: GgufKvCacheFormat.Q8_0,
                device: DeviceRouteId.IntelDiscreteGpu,
                backend: CompatibilityBackend.IntelSycl,
                offload: GpuOffloadLevel.Full)));
    }

    [TestMethod]
    public void Automatic_PutsNonExperimentalAheadOfAvoidingConversion()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Automatic),
            Candidate(experimental: false, preparation: CandidatePreparation.WeightConversionRequired),
            Candidate(experimental: true, preparation: CandidatePreparation.None));
    }

    [TestMethod]
    public void Balanced_PrefersMoreHeadroomOnceContextAndQualityTie()
    {
        AssertPrefers(
            Comparer(CompatibilityMode.Balanced),
            Candidate(headroom: 8_000_000),
            Candidate(headroom: 1_000_000));
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_BreaksTiesByFingerprintSoTheOrderIsTotal(string mode)
    {
        // Two candidates identical in every ranked respect but from different
        // entries. Without a tiebreak the winner would depend on input order.
        IComparer<EvaluatedCandidate> comparer =
            Comparer(Enum.Parse<CompatibilityMode>(mode));

        EvaluatedCandidate first = Candidate(configuration: Configuration());
        EvaluatedCandidate second = Candidate(
            configuration: Configuration(kv: GgufKvCacheFormat.Q8_0));

        Assert.AreNotEqual(0, comparer.Compare(first, second));
        Assert.AreEqual(
            -Math.Sign(comparer.Compare(first, second)),
            Math.Sign(comparer.Compare(second, first)));
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_ComparesACandidateToItselfAsEqual(string mode)
    {
        EvaluatedCandidate candidate = Candidate();

        Assert.AreEqual(
            0,
            Comparer(Enum.Parse<CompatibilityMode>(mode)).Compare(candidate, candidate));
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_RecordsPerformanceAsNotEstablished(string mode)
    {
        // Performance is in three of the four orderings but nothing measures it.
        // The factor list must say so rather than implying it separated anything.
        IReadOnlyList<SelectionFactor> factors =
            ModeComparers.FactorsFor(Enum.Parse<CompatibilityMode>(mode));

        Assert.IsFalse(
            factors.Contains(SelectionFactor.Performance),
            "Performance cannot be a live factor while nothing measures it.");
    }

    [TestMethod]
    [DataRow(nameof(CompatibilityMode.Automatic))]
    [DataRow(nameof(CompatibilityMode.Quality))]
    [DataRow(nameof(CompatibilityMode.Balanced))]
    [DataRow(nameof(CompatibilityMode.Efficiency))]
    public void EveryMode_EndsItsFactorListWithTheFingerprintTiebreak(string mode)
    {
        IReadOnlyList<SelectionFactor> factors =
            ModeComparers.FactorsFor(Enum.Parse<CompatibilityMode>(mode));

        Assert.AreEqual(SelectionFactor.Fingerprint, factors[^1]);
    }

    [TestMethod]
    public void For_RejectsAnUnspecifiedMode()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ModeComparers.For(
                CompatibilityMode.Unspecified,
                ContextTokenCount.FromTokens(4096),
                Configuration()));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command. Expected: build failure — `ModeComparers` not found.

- [ ] **Step 3: Implement the comparers**

Create `Application/ModeSelection/ModeComparers.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// The four lexicographic orderings from section 11.
///
/// Each is a list of comparison steps applied in order: the first step that
/// separates two candidates decides, and no later step can outvote an earlier
/// one. That is deliberately not a weighted score — a score lets a large win on
/// a minor axis overturn a small loss on the axis the user actually chose.
///
/// Every ordering ends with a fingerprint tiebreak, which makes it a total order
/// and therefore independent of the order candidates arrive in.
///
/// Convention: less is better. Compare(a, b) &lt; 0 means a wins.
/// </summary>
internal static class ModeComparers
{
    internal static IReadOnlyList<SelectionFactor> FactorsFor(CompatibilityMode mode) =>
        mode switch
        {
            CompatibilityMode.Quality =>
            [
                SelectionFactor.PreservesRequestedContext,
                SelectionFactor.QualityTier,
                SelectionFactor.EvidenceGrade,
                SelectionFactor.NonExperimental,
                SelectionFactor.LeastDestructivePreparation,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.Headroom,
                SelectionFactor.Fingerprint
            ],
            CompatibilityMode.Efficiency =>
            [
                SelectionFactor.WorstPoolPressureRatio,
                SelectionFactor.AddedStorage,
                SelectionFactor.MaximiseContext,
                SelectionFactor.QualityTier,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.NonExperimental,
                SelectionFactor.Fingerprint
            ],
            CompatibilityMode.Balanced =>
            [
                SelectionFactor.PreservesRequestedContext,
                SelectionFactor.QualityTier,
                SelectionFactor.Headroom,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.LeastDestructivePreparation,
                SelectionFactor.Fingerprint
            ],
            CompatibilityMode.Automatic =>
            [
                SelectionFactor.PreservesRequestedContext,
                SelectionFactor.NonExperimental,
                SelectionFactor.LeastDestructivePreparation,
                SelectionFactor.DistanceFromImportedConfiguration,
                SelectionFactor.QualityTier,
                SelectionFactor.EvidenceGrade,
                SelectionFactor.Headroom,
                SelectionFactor.PerformanceNotEstablished,
                SelectionFactor.Fingerprint
            ],
            _ => throw new ArgumentException(
                "A comparison must name its mode.", nameof(mode))
        };

    internal static IComparer<EvaluatedCandidate> For(
        CompatibilityMode mode,
        ContextTokenCount preservationTarget,
        GgufRouteConfiguration baselineConfiguration)
    {
        ArgumentNullException.ThrowIfNull(baselineConfiguration);

        // Validates the mode and gives the comparer its ordering.
        IReadOnlyList<SelectionFactor> factors = FactorsFor(mode);

        return new FactorComparer(factors, preservationTarget, baselineConfiguration);
    }

    private sealed class FactorComparer(
        IReadOnlyList<SelectionFactor> factors,
        ContextTokenCount preservationTarget,
        GgufRouteConfiguration baselineConfiguration)
        : IComparer<EvaluatedCandidate>
    {
        public int Compare(EvaluatedCandidate? left, EvaluatedCandidate? right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            foreach (SelectionFactor factor in factors)
            {
                int verdict = Apply(factor, left, right);

                if (verdict != 0)
                {
                    return verdict;
                }
            }

            return 0;
        }

        private int Apply(SelectionFactor factor, EvaluatedCandidate a, EvaluatedCandidate b) =>
            factor switch
            {
                // Booleans: true is better, so the true case sorts first.
                SelectionFactor.PreservesRequestedContext =>
                    Flag(a.Context == preservationTarget, b.Context == preservationTarget),

                SelectionFactor.NonExperimental =>
                    Flag(!a.IsExperimental, !b.IsExperimental),

                // Higher is better.
                SelectionFactor.QualityTier =>
                    Bits(b).CompareTo(Bits(a)),

                SelectionFactor.EvidenceGrade =>
                    b.Evidence.CompareTo(a.Evidence),

                SelectionFactor.Headroom =>
                    b.Fit.Headroom.CompareTo(a.Fit.Headroom),

                SelectionFactor.MaximiseContext =>
                    b.Context.Tokens.CompareTo(a.Context.Tokens),

                // Lower is better.
                SelectionFactor.LeastDestructivePreparation =>
                    Destructiveness(a).CompareTo(Destructiveness(b)),

                SelectionFactor.WorstPoolPressureRatio =>
                    WorstPoolRatio(a).CompareTo(WorstPoolRatio(b)),

                SelectionFactor.AddedStorage =>
                    a.AddedStorageBytes.CompareTo(b.AddedStorageBytes),

                SelectionFactor.DistanceFromImportedConfiguration =>
                    Distance(a).CompareTo(Distance(b)),

                // Nothing measures throughput yet, so this separates nothing.
                SelectionFactor.PerformanceNotEstablished => 0,

                SelectionFactor.Fingerprint => string.CompareOrdinal(
                    a.Fingerprint.Value, b.Fingerprint.Value),

                _ => 0
            };

        private static int Flag(bool a, bool b) => a == b ? 0 : a ? -1 : 1;

        private static decimal Bits(EvaluatedCandidate candidate) =>
            candidate.EffectiveQuantisation == WeightQuantisation.Unknown
                ? 0m
                : WeightQuantisationMap.BitsPerWeight(candidate.EffectiveQuantisation);

        private static int Destructiveness(EvaluatedCandidate candidate) =>
            candidate.Preparation switch
            {
                CandidatePreparation.None => 0,
                CandidatePreparation.RuntimeProfileOnly => 1,
                CandidatePreparation.WeightConversionRequired => 2,
                _ => 3
            };

        /// <summary>
        /// The worst pool controls. FitPolicy gates system memory alone today, so
        /// that is the only ratio; written over a collection so the device and
        /// storage gates slot in without reshaping the ordering.
        /// </summary>
        private static decimal WorstPoolRatio(EvaluatedCandidate candidate)
        {
            decimal[] ratios = [candidate.Fit.PressureRatio];

            return ratios.Max();
        }

        private int Distance(EvaluatedCandidate candidate)
        {
            if (candidate.Candidate.Configuration is not GgufRouteConfiguration configuration)
            {
                return int.MaxValue;
            }

            int distance = 0;

            if (configuration.Weights != baselineConfiguration.Weights) { distance++; }
            if (configuration.KvCache != baselineConfiguration.KvCache) { distance++; }
            if (configuration.Backend != baselineConfiguration.Backend) { distance++; }
            if (configuration.Device != baselineConfiguration.Device) { distance++; }
            if (configuration.Offload != baselineConfiguration.Offload) { distance++; }

            return distance;
        }
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command. Expected: PASS — every test green, including the 26 added here.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeComparers.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeComparerTests.cs
git commit -m "feat(compatibility): order candidates by four lexicographic modes"
```

---

### Task 5: The mode selector

Resolves all four modes over one evaluated set, plus the separate "use current model" action.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeSelector.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeSelectorTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-4.
- Produces: `ModeSelectionRequest` record; `ModeSelectionOutcome` record with `Selections` and `UseCurrentModelAvailable`; `ModeSelector.SelectAll(ModeSelectionRequest request)`. Consumed by Task 6 and by the orchestrator plan.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeSelectorTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.ModeSelection;

[TestClass]
public sealed class ModeSelectorTests
{
    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    private static EvaluatedCandidate Candidate(
        GgufKvCacheFormat kv = GgufKvCacheFormat.F16,
        int context = 4096,
        CompatibilityFitState state = CompatibilityFitState.Safe,
        bool isBaseline = false,
        bool requiresEvidence = false,
        EvidenceGrade evidence = EvidenceGrade.Estimated,
        ulong headroom = 1_000_000)
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                kv,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(context),
            CandidatePreparation.None,
            supportEntryId: requiresEvidence ? "needs-evidence" : "entry-1",
            isExperimental: false,
            isBaseline);

        ResourceEstimate estimate = ResourceEstimate.Established(
            [
                ResourceComponent.Create(
                    ResourceComponentKind.Weights,
                    ResourceTarget.SystemMemory,
                    ByteCount.FromBytes(1024),
                    new HashSet<LifecyclePhase> { LifecyclePhase.Load })
            ],
            new HashSet<EstimationLimitation>());

        return EvaluatedCandidate.Create(
            candidate,
            estimate,
            ResourcePhaseComposer.Compose(estimate.Components),
            new FitAssessment(
                state,
                state == CompatibilityFitState.Safe
                    ? FitLimitingReason.None
                    : FitLimitingReason.InsufficientSystemMemory,
                ByteCount.FromBytes(8192),
                ByteCount.FromBytes(1024),
                ByteCount.FromBytes(headroom),
                PressureRatio: 0.5m),
            WeightQuantisation.Q4_K_M,
            evidence,
            PerformanceIndicator.NotEstablished(),
            ByteCount.Zero);
    }

    private static ModeSelectionOutcome Select(
        IReadOnlyList<EvaluatedCandidate> candidates,
        IReadOnlySet<string>? evidenceRequiringEntries = null) =>
        ModeSelector.SelectAll(new ModeSelectionRequest(
            candidates,
            evidenceRequiringEntries ?? new HashSet<string>(),
            ContextTokenCount.FromTokens(4096),
            Baseline()));

    [TestMethod]
    public void SelectAll_AlwaysReturnsAllFourModes()
    {
        // A mode is never silently missing — it is disabled with a reason.
        ModeSelectionOutcome outcome = Select([Candidate()]);

        Assert.AreEqual(4, outcome.Selections.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                CompatibilityMode.Automatic,
                CompatibilityMode.Quality,
                CompatibilityMode.Balanced,
                CompatibilityMode.Efficiency
            },
            outcome.Selections.Select(selection => selection.Mode).ToArray());
    }

    [TestMethod]
    public void SelectAll_MarksEveryModeAvailableWhenACandidateIsAdmitted()
    {
        Assert.IsTrue(Select([Candidate()]).Selections.All(
            selection => selection.Availability == ModeAvailability.Available));
    }

    [TestMethod]
    public void SelectAll_RecordsTheOrderingThatDecidedEachAvailableMode()
    {
        foreach (CompatibilityModeSelection selection in Select([Candidate()]).Selections)
        {
            Assert.IsTrue(selection.Factors.Count > 0, $"{selection.Mode} recorded no ordering.");
            Assert.AreEqual(SelectionFactor.Fingerprint, selection.Factors[^1]);
        }
    }

    [TestMethod]
    public void SelectAll_ReportsUnavailableWithAReasonWhenNothingIsAdmitted()
    {
        ModeSelectionOutcome outcome =
            Select([Candidate(state: CompatibilityFitState.DoesNotFit)]);

        foreach (CompatibilityModeSelection selection in outcome.Selections)
        {
            Assert.AreEqual(ModeAvailability.Unavailable, selection.Availability);
            Assert.AreEqual(
                nameof(ModeAdmissionReason.FitStateNotSafeOrNarrow),
                selection.Reason.ToString());
            Assert.IsNull(selection.SelectedFingerprint);
        }
    }

    [TestMethod]
    public void SelectAll_ReportsNotEstablishedWhenThereAreNoCandidatesAtAll()
    {
        // No candidates is different from candidates that all failed a gate:
        // nothing was assessed, so there is no reason to give.
        ModeSelectionOutcome outcome = Select([]);

        Assert.IsTrue(outcome.Selections.All(
            selection => selection.Availability == ModeAvailability.NotEstablished));
    }

    [TestMethod]
    public void SelectAll_ExcludesCandidatesWhoseEntryNeedsEvidenceThatDoesNotExist()
    {
        ModeSelectionOutcome outcome = Select(
            [Candidate(requiresEvidence: true)],
            new HashSet<string> { "needs-evidence" });

        Assert.IsTrue(outcome.Selections.All(
            selection => selection.Availability == ModeAvailability.Unavailable));
        Assert.IsTrue(outcome.Selections.All(selection =>
            selection.Reason == ModeAdmissionReason.EvidenceBelowAdmissionLevel));
    }

    [TestMethod]
    public void SelectAll_QualityPrefersThePreservedContext()
    {
        ModeSelectionOutcome outcome = Select(
            [Candidate(kv: GgufKvCacheFormat.Q8_0, context: 2048), Candidate(context: 4096)]);

        CompatibilityModeSelection quality =
            outcome.Selections.Single(s => s.Mode == CompatibilityMode.Quality);

        Assert.AreEqual(ModeAvailability.Available, quality.Availability);
    }

    [TestMethod]
    public void SelectAll_IsIndependentOfCandidateOrder()
    {
        EvaluatedCandidate first = Candidate(kv: GgufKvCacheFormat.F16);
        EvaluatedCandidate second = Candidate(kv: GgufKvCacheFormat.Q8_0);

        ModeSelectionOutcome forward = Select([first, second]);
        ModeSelectionOutcome reversed = Select([second, first]);

        foreach (CompatibilityMode mode in new[]
        {
            CompatibilityMode.Automatic,
            CompatibilityMode.Quality,
            CompatibilityMode.Balanced,
            CompatibilityMode.Efficiency
        })
        {
            Assert.AreEqual(
                forward.Selections.Single(s => s.Mode == mode).SelectedFingerprint?.Value,
                reversed.Selections.Single(s => s.Mode == mode).SelectedFingerprint?.Value,
                $"{mode} depended on the order candidates arrived in.");
        }
    }

    [TestMethod]
    public void SelectAll_OffersUseCurrentModelWhenTheBaselineIsSafe()
    {
        Assert.IsTrue(Select([Candidate(isBaseline: true)]).UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_OffersUseCurrentModelWhenTheBaselineIsNarrow()
    {
        Assert.IsTrue(
            Select([Candidate(isBaseline: true, state: CompatibilityFitState.Narrow)])
                .UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_WithholdsUseCurrentModelWhenTheBaselineDoesNotFit()
    {
        Assert.IsFalse(
            Select([Candidate(isBaseline: true, state: CompatibilityFitState.DoesNotFit)])
                .UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_WithholdsUseCurrentModelWhenThereIsNoBaseline()
    {
        Assert.IsFalse(Select([Candidate(isBaseline: false)]).UseCurrentModelAvailable);
    }

    [TestMethod]
    public void SelectAll_SelectsOnlyFromAdmittedCandidates()
    {
        // One admitted, one not. Every mode must pick the admitted one.
        EvaluatedCandidate admitted = Candidate(kv: GgufKvCacheFormat.F16);
        EvaluatedCandidate rejected = Candidate(
            kv: GgufKvCacheFormat.Q8_0, state: CompatibilityFitState.DoesNotFit);

        ModeSelectionOutcome outcome = Select([rejected, admitted]);

        Assert.IsTrue(outcome.Selections.All(selection =>
            selection.SelectedFingerprint?.Value == admitted.Fingerprint.Value));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command. Expected: build failure — `ModeSelector` not found.

- [ ] **Step 3: Implement the selector**

Create `Application/ModeSelection/ModeSelector.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Everything mode selection needs, gathered explicitly so the selector performs
/// no lookup, no I/O and no clock read.
/// </summary>
internal sealed record ModeSelectionRequest(
    IReadOnlyList<EvaluatedCandidate> Candidates,
    IReadOnlySet<string> EvidenceRequiringEntryIds,
    ContextTokenCount PreservationTarget,
    GgufRouteConfiguration BaselineConfiguration);

/// <summary>
/// All four modes plus the separate "use current model" action.
/// </summary>
internal sealed record ModeSelectionOutcome(
    IReadOnlyList<CompatibilityModeSelection> Selections,
    bool UseCurrentModelAvailable);

/// <summary>
/// Resolves the four modes over one evaluated set.
///
/// All four rank the same evaluated candidates, so two modes can never disagree
/// about the same configuration's memory or fit. A mode with nothing to pick is
/// reported unavailable with the reason its candidates failed on, never omitted:
/// a missing option looks like an option that never existed.
/// </summary>
internal static class ModeSelector
{
    private static readonly CompatibilityMode[] Modes =
    [
        CompatibilityMode.Automatic,
        CompatibilityMode.Quality,
        CompatibilityMode.Balanced,
        CompatibilityMode.Efficiency
    ];

    internal static ModeSelectionOutcome SelectAll(ModeSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<EvaluatedCandidate> admitted = [];
        ModeAdmissionReason firstRefusal = ModeAdmissionReason.None;

        foreach (EvaluatedCandidate candidate in request.Candidates)
        {
            bool requiresEvidence = request.EvidenceRequiringEntryIds.Contains(
                candidate.Candidate.SupportEntryId);

            if (ModeAdmission.IsAdmitted(candidate, requiresEvidence, out ModeAdmissionReason reason))
            {
                admitted.Add(candidate);
                continue;
            }

            // The first refusal explains a mode that ends up with nothing.
            if (firstRefusal == ModeAdmissionReason.None)
            {
                firstRefusal = reason;
            }
        }

        List<CompatibilityModeSelection> selections = [];

        foreach (CompatibilityMode mode in Modes)
        {
            if (admitted.Count == 0)
            {
                // Nothing assessed at all is a different answer from everything
                // assessed and rejected, so it carries no reason.
                selections.Add(request.Candidates.Count == 0
                    ? CompatibilityModeSelection.NotEstablished(mode)
                    : CompatibilityModeSelection.Unavailable(mode, firstRefusal));

                continue;
            }

            IComparer<EvaluatedCandidate> comparer = ModeComparers.For(
                mode, request.PreservationTarget, request.BaselineConfiguration);

            EvaluatedCandidate winner = admitted[0];

            foreach (EvaluatedCandidate contender in admitted.Skip(1))
            {
                if (comparer.Compare(contender, winner) < 0)
                {
                    winner = contender;
                }
            }

            selections.Add(CompatibilityModeSelection.Available(
                mode, winner.Fingerprint, ModeComparers.FactorsFor(mode)));
        }

        // Separate from the four modes: it creates a runtime profile, not an
        // artifact, and it stands whenever what the user already has is safe.
        bool useCurrentModel = request.Candidates.Any(candidate =>
            candidate.Candidate.IsBaseline
            && candidate.Fit.State is CompatibilityFitState.Safe
                or CompatibilityFitState.Narrow);

        return new ModeSelectionOutcome(selections, useCurrentModel);
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command. Expected: PASS — every test green, including the 13 added here.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ModeSelection/ModeSelector.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ModeSelection/ModeSelectorTests.cs
git commit -m "feat(compatibility): resolve the four optimisation modes"
```

---

### Task 6: Mode selection invariants

Properties that hold however the orderings are later tuned.

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/ModeSelectionInvariantTests.cs`

**Interfaces:**
- Consumes: Tasks 1-5 plus `SupportMatrix`, `CandidateGenerator`, `GgufResourceEstimator`, `FitPolicy`.
- Produces: no production type.

- [ ] **Step 1: Write the invariant tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/ModeSelectionInvariantTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties of mode selection that survive any retuning of the orderings.
/// </summary>
[TestClass]
public sealed class ModeSelectionInvariantTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static InspectedModelFacts Facts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    /// <summary>
    /// The whole spine: matrix, generation, estimation, fit, then selection.
    /// </summary>
    private static (ModeSelectionOutcome Outcome, IReadOnlyList<EvaluatedCandidate> Evaluated)
        RunSpine(ulong availableGibibytes)
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        CandidateGenerationResult generated = CandidateGenerator.Generate(
            new CandidateGenerationRequest(
                matrix,
                matrix.Entries.ToDictionary(
                    entry => entry.EntryId,
                    entry => entry.Level == SupportLevel.Experimental
                        ? InstallationState.VerifiedAndOptedIn
                        : InstallationState.InstalledAndVerified),
                Facts(),
                Baseline(),
                ContextTokenCount.FromTokens(4096),
                ContextTokenCount.FromTokens(4096),
                TrustedSourceAvailability.None()));

        AvailableResources available = AvailableResources.Create(
            ByteCount.FromBytes(availableGibibytes * Gibibyte),
            dedicatedDeviceMemory: ByteCount.FromBytes(8 * Gibibyte),
            storage: ByteCount.FromBytes(500 * Gibibyte),
            observedAtUtc: DateTimeOffset.UtcNow);

        List<EvaluatedCandidate> evaluated = [];

        foreach (CompatibilityCandidate candidate in generated.Candidates)
        {
            ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                Facts(), candidate, EstimatorPolicy.ProvisionalV1());

            if (estimate.Status != EstimationStatus.Established)
            {
                continue;
            }

            ResourcePeakProfile peaks = ResourcePhaseComposer.Compose(estimate.Components);

            evaluated.Add(EvaluatedCandidate.Create(
                candidate,
                estimate,
                peaks,
                FitPolicy.Assess(peaks, available, SafetyPolicy.ProvisionalV1()),
                WeightQuantisation.Q4_K_M,
                EvidenceGrade.Estimated,
                PerformanceIndicator.NotEstablished(),
                peaks.PeakFor(ResourceTarget.Storage)));
        }

        HashSet<string> evidenceRequiring =
            [.. matrix.Entries.Where(entry => entry.RequiresEvidence)
                .Select(entry => entry.EntryId)];

        return (
            ModeSelector.SelectAll(new ModeSelectionRequest(
                evaluated,
                evidenceRequiring,
                ContextTokenCount.FromTokens(4096),
                Baseline())),
            evaluated);
    }

    [TestMethod]
    public void EveryModeIsAlwaysAccountedFor()
    {
        foreach (ulong memory in new ulong[] { 64, 8, 2 })
        {
            (ModeSelectionOutcome outcome, _) = RunSpine(memory);

            Assert.AreEqual(4, outcome.Selections.Count, $"at {memory} GiB");
            Assert.IsTrue(
                outcome.Selections.All(s => s.Availability != ModeAvailability.NotEstablished
                    || s.SelectedFingerprint is null),
                "a not-established mode must carry no selection");
        }
    }

    [TestMethod]
    public void NoModeEverSelectsACandidateThatDoesNotFit()
    {
        // The one result that must never happen: a mode recommending a
        // configuration the machine cannot run.
        foreach (ulong memory in new ulong[] { 64, 16, 8, 4, 2 })
        {
            (ModeSelectionOutcome outcome, IReadOnlyList<EvaluatedCandidate> evaluated) =
                RunSpine(memory);

            foreach (CompatibilityModeSelection selection in outcome.Selections)
            {
                if (selection.SelectedFingerprint is not { } fingerprint)
                {
                    continue;
                }

                EvaluatedCandidate chosen = evaluated.Single(
                    candidate => candidate.Fingerprint.Value == fingerprint.Value);

                Assert.IsTrue(
                    chosen.Fit.State is CompatibilityFitState.Safe
                        or CompatibilityFitState.Narrow,
                    $"{selection.Mode} chose a {chosen.Fit.State} candidate at {memory} GiB.");
            }
        }
    }

    [TestMethod]
    public void NoModeEverSelectsAnEvidenceRequiringEntryWithoutEvidence()
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();
        HashSet<string> requiring =
            [.. matrix.Entries.Where(entry => entry.RequiresEvidence)
                .Select(entry => entry.EntryId)];

        (ModeSelectionOutcome outcome, IReadOnlyList<EvaluatedCandidate> evaluated) =
            RunSpine(64);

        foreach (CompatibilityModeSelection selection in outcome.Selections)
        {
            if (selection.SelectedFingerprint is not { } fingerprint)
            {
                continue;
            }

            EvaluatedCandidate chosen = evaluated.Single(
                candidate => candidate.Fingerprint.Value == fingerprint.Value);

            Assert.IsFalse(
                requiring.Contains(chosen.Candidate.SupportEntryId),
                $"{selection.Mode} chose {chosen.Candidate.SupportEntryId}, which needs evidence.");
        }
    }

    [TestMethod]
    public void SelectionIsStableUnderCandidateReordering()
    {
        (ModeSelectionOutcome _, IReadOnlyList<EvaluatedCandidate> evaluated) = RunSpine(64);

        HashSet<string> requiring = [];

        ModeSelectionOutcome Run(IReadOnlyList<EvaluatedCandidate> order) =>
            ModeSelector.SelectAll(new ModeSelectionRequest(
                order, requiring, ContextTokenCount.FromTokens(4096), Baseline()));

        ModeSelectionOutcome forward = Run(evaluated);
        ModeSelectionOutcome reversed = Run([.. evaluated.Reverse()]);

        foreach (CompatibilityModeSelection selection in forward.Selections)
        {
            Assert.AreEqual(
                selection.SelectedFingerprint?.Value,
                reversed.Selections.Single(s => s.Mode == selection.Mode)
                    .SelectedFingerprint?.Value,
                $"{selection.Mode} depended on candidate order.");
        }
    }

    [TestMethod]
    public void AnUnavailableModeAlwaysNamesItsReason()
    {
        (ModeSelectionOutcome outcome, _) = RunSpine(2);

        foreach (CompatibilityModeSelection selection in outcome.Selections)
        {
            if (selection.Availability == ModeAvailability.Unavailable)
            {
                Assert.AreNotEqual(
                    ModeAdmissionReason.None,
                    selection.Reason,
                    $"{selection.Mode} is unavailable but explains nothing.");
            }
        }
    }

    [TestMethod]
    public void UseCurrentModelTracksTheBaselinesOwnFitAndNothingElse()
    {
        foreach (ulong memory in new ulong[] { 64, 2 })
        {
            (ModeSelectionOutcome outcome, IReadOnlyList<EvaluatedCandidate> evaluated) =
                RunSpine(memory);

            EvaluatedCandidate? baseline = evaluated.FirstOrDefault(
                candidate => candidate.Candidate.IsBaseline);

            bool expected = baseline is not null
                && baseline.Fit.State is CompatibilityFitState.Safe
                    or CompatibilityFitState.Narrow;

            Assert.AreEqual(expected, outcome.UseCurrentModelAvailable, $"at {memory} GiB");
        }
    }
}
```

- [ ] **Step 2: Run the whole suite**

Run the suite command. Expected: PASS — every test green, including the 6 added here.

- [ ] **Step 3: Commit**

```bash
git add tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/ModeSelectionInvariantTests.cs
git commit -m "test(compatibility): pin the mode selection invariants"
```

---

## Self-Review

**Spec coverage.** Section 11 hard gates → Task 2, with the evidence gate returning `EvidenceBelowAdmissionLevel` exactly as the spec names it. Section 11's four orderings → Task 4, each a lexicographic factor list rather than a weighted score. Section 11 availability triple → Task 3's `ModeAvailability` and Task 5's resolution, with an unavailable mode always naming a reason. Section 11 `Use current model` → Task 5, kept separate from the four modes and tied only to the baseline's own fit. Section 11's future-screen requirement that the assessment already carry the ordered factors → `CompatibilityModeSelection.Factors`.

**Deliberately deferred, and why.** Performance ordering is a typed absence rather than a proxy, recorded as `PerformanceNotEstablished` in every factor list that would have used it. The worst-pool ratio reads one pool because `FitPolicy` gates one pool; the comparator is written over a collection so the device and storage gates need no reshaping. The `BackendMismatch` admission reason exists but has no producer until a request can carry a requested backend, which arrives with the orchestrator.

**Placeholder scan.** No TBD, no "add validation", no "similar to Task N". Every code step carries complete code.

**Type consistency.** `EvaluatedCandidate.Create` (Task 1) is called identically in Tasks 2, 4, 5 and 6. `ModeAdmission.IsAdmitted(candidate, entryRequiresEvidence, out reason)` (Task 2) matches Task 5's call. `ModeComparers.For(mode, preservationTarget, baselineConfiguration)` and `.FactorsFor(mode)` (Task 4) match Task 5. `CompatibilityModeSelection.Available/Unavailable/NotEstablished` (Task 3) match Task 5's construction. `FitAssessment` is referenced as `FitAssessment.FitAssessment` inside `EvaluatedCandidate` because the type and its namespace share a name.

**Known follow-up.** `ModeAdmissionReason.BackendMismatch` is unproduced until the orchestrator carries a requested backend. `EvidenceGrade.Measured` and `.Verified` are unreachable until runtime verification exists, so the evidence-requiring entry in the matrix currently admits nothing — which is the honest outcome, and the invariant test pins it.
