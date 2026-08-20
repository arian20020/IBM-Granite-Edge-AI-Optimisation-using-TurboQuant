# Model/Hardware Compatibility GGUF Resource Estimator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn model facts plus one complete GGUF candidate into a per-component resource estimate that feeds the existing phase composer and fit policy, or into an honest `NotEstablished` when any input is unknown.

**Architecture:** A route-specific estimator in `Routes/Gguf/` decomposes a candidate into `ResourceComponent` values charged to one pool each. Model facts arrive as a C1-owned `InspectedModelFacts` record so nothing references I1. Every tunable number lives in a versioned `EstimatorPolicy` carrying the existing `PolicyProvenance`, so no constant is invented in code. Unknown inputs collapse the whole estimate rather than defaulting to zero.

**Tech Stack:** C# 12, .NET 8 (`net8.0`), MSTest 4.3.2 on Microsoft.Testing.Platform, SDK 10.0.301 pinned by `global.json`.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-design.md`, sections 8, 9 and 15.

**Predecessors:**
- `docs/superpowers/plans/2026-08-20-model-hardware-compatibility-core-foundation.md` (M1+M2, complete)
- `docs/superpowers/plans/2026-08-20-model-hardware-compatibility-candidate-generation.md` (M3a, complete)

At the start of this plan the suite is **100 tests green**. Run the suite before Task 1 to confirm:

```bash
dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj
```

That command is referred to below as **the suite command**.

**Scope:** M3b only. Mode selection (M3c), the support matrix and generator, the orchestrator (M4) and screens (M5) are separate plans.

## Global Constraints

- Target `net8.0`, `Nullable` enabled, `TreatWarningsAsErrors` true. No WinUI, no `Microsoft.UI.*`, no Windows-only API.
- Types are `internal`; the test project already has `InternalsVisibleTo`.
- All byte quantities use **checked `ulong`** via `ByteCount`. Bits→bytes uses ceiling division. Alignment rounds upward only.
- **Unknown is a typed unavailable value, never zero, never a default.**
- Every enum reserves a zero member (`Unspecified`, `Unknown` or `None`) and rejects it at every boundary where it would be meaningless.
- RAM and dedicated VRAM are never summed. Shared GPU memory counts against system memory exactly once — already enforced by `ResourcePhaseComposer`; the estimator must not re-add it.
- Bias is one-directional: margins are added to requirements, never subtracted. Any unknown collapses the estimate to `NotEstablished`.
- MSTest 4: use `[TestMethod]` with `[DataRow]`; `DataTestMethod` is deprecated and fails the build.
- A public test method cannot take an `internal` enum parameter (CS0051). Pass `nameof(...)` strings and compare against `.ToString()`.
- No absolute path, UNC path, filename, model name, hostname, credential, native error or raw tool output in any type, message or test fixture.

## Folder Structure

New source files follow the concern-folder layout already used by `shared/GraniteEdgeAI.ModelInspection.Contracts` (`Evidence/`, `Protocol/`) and by the compatibility core itself:

```
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/
├── README.md                                   Task 10 (new)
├── Domain/
│   ├── ByteCount.cs                            Task 1 (modify)
│   ├── WeightQuantisation.cs                   Task 2 (new)
│   └── WeightQuantisationMap.cs                Task 2 (new)
├── Application/
│   ├── Contracts/
│   │   └── InspectedModelFacts.cs              Task 3 (new)
│   └── Estimation/
│       ├── EstimationStatus.cs                 Task 4 (new)
│       ├── EstimationUnavailableReason.cs      Task 4 (new)
│       ├── EstimationLimitation.cs             Task 4 (new)
│       ├── ResourceEstimate.cs                 Task 4 (new)
│       └── EstimatorPolicy.cs                  Task 5 (new)
└── Routes/Gguf/
    ├── GgufKvCacheBlockSpec.cs                 Task 6 (new)
    ├── GgufKvCacheEstimator.cs                 Task 6 (new)
    ├── GgufWeightEstimator.cs                  Task 7 (new)
    ├── GgufPoolRouting.cs                      Task 8 (new)
    └── GgufResourceEstimator.cs                Task 9 (new)
```

Test files mirror the source tree exactly, one test file per source concern:

```
tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/
├── Domain/
│   ├── ByteCountTests.cs                       Task 1 (modify)
│   └── WeightQuantisationMapTests.cs           Task 2 (new)
├── Application/
│   ├── Contracts/InspectedModelFactsTests.cs   Task 3 (new)
│   └── Estimation/
│       ├── ResourceEstimateTests.cs            Task 4 (new)
│       └── EstimatorPolicyTests.cs             Task 5 (new)
├── Routes/Gguf/
│   ├── GgufKvCacheEstimatorTests.cs            Task 6 (new)
│   ├── GgufWeightEstimatorTests.cs             Task 7 (new)
│   ├── GgufPoolRoutingTests.cs                 Task 8 (new)
│   └── GgufResourceEstimatorTests.cs           Task 9 (new)
└── Invariants/                                 Task 10 (new)
    ├── DeterministicRandom.cs
    ├── EstimationCaseGenerator.cs
    ├── MetamorphicPropertyTests.cs
    ├── EnumExhaustivenessTests.cs
    ├── ArithmeticSafetyTests.cs
    ├── CultureInvarianceTests.cs
    ├── PrivacyCanaryTests.cs
    └── EstimationSpineTests.cs
```

Task 10 also relocates the two existing test files that sit one level too shallow
(`Application/FitPolicyTests.cs` → `Application/FitAssessment/`, `Routes/GgufRouteConfigurationTests.cs`
→ `Routes/Gguf/`) so the mirror is exact.

## Known follow-up, recorded not hidden

`FitPolicy` still gates system memory only. This plan emits `DedicatedDeviceMemory` and `Storage`
components that nothing yet assesses. Extending `FitPolicy` to all three pools belongs with mode
selection, where a candidate's device route first drives a decision. Do not widen `FitPolicy` here.

---

### Task 1: Alignment and fractional byte arithmetic

Spec section 8 requires alignment that "rounds upward only at a documented allocation boundary" and
overhead terms expressed as fractions. Both are byte arithmetic, so they belong on `ByteCount`
beside the existing checked operations rather than being re-derived in each estimator.

**Files:**
- Modify: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/ByteCount.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Domain/ByteCountTests.cs`

**Interfaces:**
- Consumes: the existing `ByteCount` struct (`FromBytes`, `Bytes`, `Add`, `CeilingDivide`, comparison operators).
- Produces: `ByteCount ByteCount.AlignUpTo(ulong alignment)` and `ByteCount ByteCount.MultiplyByFraction(decimal fraction)`, used by Tasks 5, 6, 7 and 9.

- [ ] **Step 1: Write the failing tests**

Append these methods inside the existing `ByteCountTests` class:

```csharp
    [TestMethod]
    [DataRow(0UL, 4096UL, 0UL)]
    [DataRow(1UL, 4096UL, 4096UL)]
    [DataRow(4095UL, 4096UL, 4096UL)]
    [DataRow(4096UL, 4096UL, 4096UL)]
    [DataRow(4097UL, 4096UL, 8192UL)]
    public void AlignUpTo_RoundsUpwardOnly(ulong bytes, ulong alignment, ulong expected)
    {
        Assert.AreEqual(
            expected,
            ByteCount.FromBytes(bytes).AlignUpTo(alignment).Bytes);
    }

    [TestMethod]
    public void AlignUpTo_RejectsZeroAlignment()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteCount.FromBytes(1).AlignUpTo(0));
    }

    [TestMethod]
    public void AlignUpTo_RejectsNonPowerOfTwoAlignment()
    {
        // A non-power-of-two "alignment" is almost always a units mistake, and
        // silently accepting it would produce a plausible but wrong number.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteCount.FromBytes(1).AlignUpTo(3000));
    }

    [TestMethod]
    public void AlignUpTo_OverflowIsLoudRatherThanWrapped()
    {
        Assert.ThrowsExactly<OverflowException>(
            () => ByteCount.FromBytes(ulong.MaxValue).AlignUpTo(4096));
    }

    [TestMethod]
    [DataRow(1000UL, 0.10, 100UL)]
    [DataRow(1001UL, 0.10, 101UL)]
    [DataRow(0UL, 0.10, 0UL)]
    [DataRow(1000UL, 0.0, 0UL)]
    public void MultiplyByFraction_RoundsUpward(ulong bytes, double fraction, ulong expected)
    {
        // Rounding up keeps every overhead term on the conservative side of the
        // one-directional bias: an understated overhead is a false-safe result.
        Assert.AreEqual(
            expected,
            ByteCount.FromBytes(bytes).MultiplyByFraction((decimal)fraction).Bytes);
    }

    [TestMethod]
    public void MultiplyByFraction_RejectsNegativeFraction()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteCount.FromBytes(1000).MultiplyByFraction(-0.01m));
    }
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `'ByteCount' does not contain a definition for 'AlignUpTo'`, and the same for `MultiplyByFraction`.

- [ ] **Step 3: Implement the two operations**

Insert into `ByteCount`, immediately after the existing `CeilingDivide` method:

```csharp
    /// <summary>
    /// Rounds upward to an allocation boundary. Allocation granularity only ever
    /// costs more memory than requested, so this rounds up and never down.
    /// </summary>
    internal ByteCount AlignUpTo(ulong alignment)
    {
        if (alignment == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alignment),
                "An allocation boundary must be a positive number of bytes.");
        }

        if ((alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alignment),
                "An allocation boundary must be a power of two; a non-power-of-two "
                + "value is almost always a units mistake.");
        }

        ulong remainder = Bytes % alignment;
        return remainder == 0
            ? this
            : new ByteCount(checked(Bytes + (alignment - remainder)));
    }

    /// <summary>
    /// Scales by a fraction, rounding upward. Overhead terms are always rounded
    /// against the user, because an understated overhead is a false-safe result.
    /// </summary>
    internal ByteCount MultiplyByFraction(decimal fraction)
    {
        if (fraction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fraction),
                "A byte quantity cannot be scaled by a negative fraction.");
        }

        return new ByteCount((ulong)Math.Ceiling(Bytes * fraction));
    }
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 13 added in this task.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/ByteCount.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Domain/ByteCountTests.cs
git commit -m "feat(compatibility): add alignment and fractional byte arithmetic"
```

---

### Task 2: Canonical weight quantisation

Spec section 8: `ModelInspectionConfigurationEvidence` exposes `FileType : int?` and
`QuantisationVersion : int?`, not a quantisation member. C1 defines its own canonical
`WeightQuantisation` derived from those two values, and a display string is never a calculation
input.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/WeightQuantisation.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/WeightQuantisationMap.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Domain/WeightQuantisationMapTests.cs`

**Interfaces:**
- Consumes: `GgufWeightFormat` from the completed M3a work.
- Produces: the `WeightQuantisation` enum; `WeightQuantisationMap.FromGgufFileType(int? fileType, int? quantisationVersion)` returning `WeightQuantisation`; `WeightQuantisationMap.FromWeightFormat(GgufWeightFormat format)` returning `WeightQuantisation`; `WeightQuantisationMap.BitsPerWeight(WeightQuantisation quantisation)` returning `decimal`. Consumed by Task 7.

> **Verification note for the implementer.** The `fileType` values below are llama.cpp
> `LLAMA_FTYPE_MOSTLY_*` constants. No llama.cpp header is vendored in this repository
> (`third-party/` holds only a README), so the table is knowledge-sourced rather than read from
> source. Unmapped values return `Unknown`, which is safe: an unknown source quantisation only
> blocks candidates that change the weight format, never the imported baseline. Confirm the table
> against upstream `llama.h` before the estimator is calibrated.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Domain/WeightQuantisationMapTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Domain;

[TestClass]
public sealed class WeightQuantisationMapTests
{
    [TestMethod]
    [DataRow(0, nameof(WeightQuantisation.F32))]
    [DataRow(1, nameof(WeightQuantisation.F16))]
    [DataRow(7, nameof(WeightQuantisation.Q8_0))]
    [DataRow(12, nameof(WeightQuantisation.Q3_K_M))]
    [DataRow(15, nameof(WeightQuantisation.Q4_K_M))]
    [DataRow(17, nameof(WeightQuantisation.Q5_K_M))]
    [DataRow(18, nameof(WeightQuantisation.Q6_K))]
    [DataRow(32, nameof(WeightQuantisation.BF16))]
    public void FromGgufFileType_MapsKnownFileTypes(int fileType, string expected)
    {
        Assert.AreEqual(
            expected,
            WeightQuantisationMap.FromGgufFileType(fileType, quantisationVersion: 2).ToString());
    }

    [TestMethod]
    public void FromGgufFileType_UnknownFileTypeIsUnknownNotAGuess()
    {
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromGgufFileType(9999, quantisationVersion: 2).ToString());
    }

    [TestMethod]
    public void FromGgufFileType_MissingFileTypeIsUnknown()
    {
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromGgufFileType(fileType: null, quantisationVersion: 2)
                .ToString());
    }

    [TestMethod]
    public void FromGgufFileType_MissingQuantisationVersionIsUnknown()
    {
        // The version pins how the file type is to be read. A file type without
        // one is an unpinned number rather than an established fact.
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromGgufFileType(fileType: 15, quantisationVersion: null)
                .ToString());
    }

    [TestMethod]
    public void BitsPerWeight_IsMonotonicAcrossTheQualityLadder()
    {
        // The ladder is declared highest to lowest quality, so bits per weight
        // must never rise as it descends. A transposed table entry fails here.
        WeightQuantisation[] ladder =
        [
            WeightQuantisation.F32,
            WeightQuantisation.F16,
            WeightQuantisation.Q8_0,
            WeightQuantisation.Q6_K,
            WeightQuantisation.Q5_K_M,
            WeightQuantisation.Q4_K_M,
            WeightQuantisation.Q3_K_M,
            WeightQuantisation.Q2_K
        ];

        for (int index = 1; index < ladder.Length; index++)
        {
            Assert.IsTrue(
                WeightQuantisationMap.BitsPerWeight(ladder[index])
                    < WeightQuantisationMap.BitsPerWeight(ladder[index - 1]),
                $"{ladder[index]} must use fewer bits per weight than {ladder[index - 1]}.");
        }
    }

    [TestMethod]
    public void BitsPerWeight_RejectsUnknown()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => WeightQuantisationMap.BitsPerWeight(WeightQuantisation.Unknown));
    }

    [TestMethod]
    public void FromWeightFormat_ImportedHasNoCanonicalQuantisation()
    {
        // "Imported" means "whatever the file already is", which is a property of
        // the file and not of the requested format.
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromWeightFormat(GgufWeightFormat.Imported).ToString());
    }

    [TestMethod]
    [DataRow(nameof(GgufWeightFormat.BF16), nameof(WeightQuantisation.BF16))]
    [DataRow(nameof(GgufWeightFormat.F16), nameof(WeightQuantisation.F16))]
    [DataRow(nameof(GgufWeightFormat.Q8_0), nameof(WeightQuantisation.Q8_0))]
    [DataRow(nameof(GgufWeightFormat.Q6K), nameof(WeightQuantisation.Q6_K))]
    [DataRow(nameof(GgufWeightFormat.Q5KM), nameof(WeightQuantisation.Q5_K_M))]
    [DataRow(nameof(GgufWeightFormat.Q4KM), nameof(WeightQuantisation.Q4_K_M))]
    [DataRow(nameof(GgufWeightFormat.Q3KM), nameof(WeightQuantisation.Q3_K_M))]
    public void FromWeightFormat_MapsEveryGeneratableFormat(string format, string expected)
    {
        GgufWeightFormat parsed = Enum.Parse<GgufWeightFormat>(format);

        Assert.AreEqual(expected, WeightQuantisationMap.FromWeightFormat(parsed).ToString());
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'WeightQuantisationMap' could not be found`.

- [ ] **Step 3: Implement the enum**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/WeightQuantisation.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// C1's canonical weight encoding, derived from the GGUF file type and
/// quantisation version rather than from any display string. Declared highest to
/// lowest quality.
/// </summary>
internal enum WeightQuantisation
{
    /// <summary>
    /// The encoding could not be established. This is a typed absence and is
    /// never treated as a default precision.
    /// </summary>
    Unknown = 0,
    F32,
    BF16,
    F16,
    Q8_0,
    Q6_K,
    Q5_K_M,
    Q4_K_M,
    Q3_K_M,
    Q2_K
}
```

- [ ] **Step 4: Implement the map**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/WeightQuantisationMap.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// Translates raw GGUF identifiers into C1's canonical quantisation, and states
/// the average bits per weight each encoding uses.
///
/// The K-quant figures are averages over a mixed encoding: different tensors in
/// one file use different block types. They are accurate enough to scale a file
/// length between formats and not precise enough to be presented as a
/// measurement, which is why every estimate derived from them carries the
/// WeightsScaledAcrossQuantisation limitation.
/// </summary>
internal static class WeightQuantisationMap
{
    private static readonly Dictionary<int, WeightQuantisation> FileTypes = new()
    {
        [0] = WeightQuantisation.F32,
        [1] = WeightQuantisation.F16,
        [7] = WeightQuantisation.Q8_0,
        [10] = WeightQuantisation.Q2_K,
        [12] = WeightQuantisation.Q3_K_M,
        [15] = WeightQuantisation.Q4_K_M,
        [17] = WeightQuantisation.Q5_K_M,
        [18] = WeightQuantisation.Q6_K,
        [32] = WeightQuantisation.BF16
    };

    private static readonly Dictionary<WeightQuantisation, decimal> Bits = new()
    {
        [WeightQuantisation.F32] = 32m,
        [WeightQuantisation.BF16] = 16m,
        [WeightQuantisation.F16] = 16m,
        [WeightQuantisation.Q8_0] = 8.5m,
        [WeightQuantisation.Q6_K] = 6.5625m,
        [WeightQuantisation.Q5_K_M] = 5.6875m,
        [WeightQuantisation.Q4_K_M] = 4.8125m,
        [WeightQuantisation.Q3_K_M] = 3.9062m,
        [WeightQuantisation.Q2_K] = 2.6250m
    };

    /// <summary>
    /// Resolves the encoding of the file as imported. Both identifiers are
    /// required: the version pins how the file type is to be read.
    /// </summary>
    internal static WeightQuantisation FromGgufFileType(
        int? fileType,
        int? quantisationVersion)
    {
        if (fileType is not { } type || quantisationVersion is null)
        {
            return WeightQuantisation.Unknown;
        }

        return FileTypes.TryGetValue(type, out WeightQuantisation quantisation)
            ? quantisation
            : WeightQuantisation.Unknown;
    }

    /// <summary>
    /// The canonical encoding a requested weight format produces. Imported has
    /// none of its own: it is whatever the file already contains.
    /// </summary>
    internal static WeightQuantisation FromWeightFormat(GgufWeightFormat format) => format switch
    {
        GgufWeightFormat.BF16 => WeightQuantisation.BF16,
        GgufWeightFormat.F16 => WeightQuantisation.F16,
        GgufWeightFormat.Q8_0 => WeightQuantisation.Q8_0,
        GgufWeightFormat.Q6K => WeightQuantisation.Q6_K,
        GgufWeightFormat.Q5KM => WeightQuantisation.Q5_K_M,
        GgufWeightFormat.Q4KM => WeightQuantisation.Q4_K_M,
        GgufWeightFormat.Q3KM => WeightQuantisation.Q3_K_M,
        _ => WeightQuantisation.Unknown
    };

    internal static decimal BitsPerWeight(WeightQuantisation quantisation) =>
        Bits.TryGetValue(quantisation, out decimal bits)
            ? bits
            : throw new ArgumentOutOfRangeException(
                nameof(quantisation),
                "An unknown encoding has no bit width; the caller must report "
                + "NotEstablished rather than substitute a default precision.");
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 21 added in this task.

- [ ] **Step 6: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/WeightQuantisation.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/WeightQuantisationMap.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Domain/WeightQuantisationMapTests.cs
git commit -m "feat(compatibility): derive canonical weight quantisation"
```

---

### Task 3: Inspected model facts

Spec section 3: C1 ports accept and return C1-owned calculation-domain records, never owner types.
This is the estimator's input record — shaped by what the estimator consumes, not by what I1 happens
to publish.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/InspectedModelFacts.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Contracts/InspectedModelFactsTests.cs`

**Interfaces:**
- Consumes: `ByteCount` from the completed M1 work.
- Produces: `InspectedModelFacts.Create(ByteCount fileLength, int? layerCount, int? embeddingSize, int? attentionHeadCount, int? keyValueHeadCount, int? declaredContextLimit, int? fileType, int? quantisationVersion)`, plus read-only properties `FileLength`, `LayerCount`, `EmbeddingSize`, `AttentionHeadCount`, `KeyValueHeadCount`, `DeclaredContextLimit`, `FileType`, `QuantisationVersion`. Consumed by Tasks 6, 7, 9 and 10.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Contracts/InspectedModelFactsTests.cs`:

```csharp
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Contracts;

[TestClass]
public sealed class InspectedModelFactsTests
{
    [TestMethod]
    public void Create_PreservesEveryFact()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            fileLength: ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

        Assert.AreEqual(4_000_000_000UL, facts.FileLength.Bytes);
        Assert.AreEqual(32, facts.LayerCount);
        Assert.AreEqual(4096, facts.EmbeddingSize);
        Assert.AreEqual(32, facts.AttentionHeadCount);
        Assert.AreEqual(8, facts.KeyValueHeadCount);
        Assert.AreEqual(8192, facts.DeclaredContextLimit);
        Assert.AreEqual(15, facts.FileType);
        Assert.AreEqual(2, facts.QuantisationVersion);
    }

    [TestMethod]
    public void Create_AllowsEveryArchitecturalFactToBeAbsent()
    {
        // A quick scan may establish the file length and nothing else. That must
        // be representable, because the alternative is inventing architecture.
        InspectedModelFacts facts = InspectedModelFacts.Create(
            fileLength: ByteCount.FromBytes(1024),
            layerCount: null,
            embeddingSize: null,
            attentionHeadCount: null,
            keyValueHeadCount: null,
            declaredContextLimit: null,
            fileType: null,
            quantisationVersion: null);

        Assert.IsNull(facts.LayerCount);
        Assert.IsNull(facts.EmbeddingSize);
        Assert.IsNull(facts.AttentionHeadCount);
        Assert.IsNull(facts.KeyValueHeadCount);
        Assert.IsNull(facts.DeclaredContextLimit);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveLayerCount(int layers)
    {
        // Zero is not "unknown"; null is. Accepting zero would make a model with
        // no layers indistinguishable from one whose layer count was never read.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), layers, 4096, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveEmbeddingSize(int embedding)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, embedding, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveAttentionHeadCount(int heads)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, 4096, heads, 8, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveKeyValueHeadCount(int heads)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, 4096, 32, heads, 8192, 15, 2));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_RejectsNonPositiveDeclaredContextLimit(int limit)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.FromBytes(1024), 32, 4096, 32, 8, limit, 15, 2));
    }

    [TestMethod]
    public void Create_RejectsZeroFileLength()
    {
        // File length is the one measured quantity the weight estimate rests on.
        // A zero-length model would produce a free configuration.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => InspectedModelFacts.Create(
                ByteCount.Zero, 32, 4096, 32, 8, 8192, 15, 2));
    }

    [TestMethod]
    public void Facts_CarryNoStringMember()
    {
        // Privacy canary. Section 14 forbids any path, filename or model name
        // reaching a C1 record. No string member means no place to put one.
        PropertyInfo[] strings = typeof(InspectedModelFacts)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();

        Assert.AreEqual(
            0,
            strings.Length,
            "InspectedModelFacts must expose no string member: "
            + string.Join(", ", strings.Select(property => property.Name)));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'InspectedModelFacts' could not be found`.

- [ ] **Step 3: Implement the record**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/InspectedModelFacts.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// The model facts the estimator consumes, owned by C1 and shaped by the
/// calculation rather than by any upstream contract. Every architectural fact is
/// optional because inspection may establish some and not others; absence is
/// null and never zero, so a missing figure collapses an estimate instead of
/// silently making the configuration cheaper.
/// </summary>
internal sealed record InspectedModelFacts
{
    private InspectedModelFacts(
        ByteCount fileLength,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion)
    {
        FileLength = fileLength;
        LayerCount = layerCount;
        EmbeddingSize = embeddingSize;
        AttentionHeadCount = attentionHeadCount;
        KeyValueHeadCount = keyValueHeadCount;
        DeclaredContextLimit = declaredContextLimit;
        FileType = fileType;
        QuantisationVersion = quantisationVersion;
    }

    /// <summary>The measured artifact length. Required.</summary>
    internal ByteCount FileLength { get; }

    internal int? LayerCount { get; }

    internal int? EmbeddingSize { get; }

    internal int? AttentionHeadCount { get; }

    /// <summary>
    /// Key/value head count. Grouped-query models use fewer of these than
    /// attention heads, and substituting the attention head count would overstate
    /// the KV cache several times over.
    /// </summary>
    internal int? KeyValueHeadCount { get; }

    internal int? DeclaredContextLimit { get; }

    /// <summary>Raw GGUF file type, interpreted only through the canonical map.</summary>
    internal int? FileType { get; }

    internal int? QuantisationVersion { get; }

    internal static InspectedModelFacts Create(
        ByteCount fileLength,
        int? layerCount,
        int? embeddingSize,
        int? attentionHeadCount,
        int? keyValueHeadCount,
        int? declaredContextLimit,
        int? fileType,
        int? quantisationVersion)
    {
        if (fileLength == ByteCount.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileLength),
                "A model must have a measured length; the weight estimate rests on it.");
        }

        RequirePositiveWhenPresent(layerCount, nameof(layerCount));
        RequirePositiveWhenPresent(embeddingSize, nameof(embeddingSize));
        RequirePositiveWhenPresent(attentionHeadCount, nameof(attentionHeadCount));
        RequirePositiveWhenPresent(keyValueHeadCount, nameof(keyValueHeadCount));
        RequirePositiveWhenPresent(declaredContextLimit, nameof(declaredContextLimit));

        return new InspectedModelFacts(
            fileLength,
            layerCount,
            embeddingSize,
            attentionHeadCount,
            keyValueHeadCount,
            declaredContextLimit,
            fileType,
            quantisationVersion);
    }

    private static void RequirePositiveWhenPresent(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "An established architectural fact must be positive. Absence is "
                + "expressed as null, never as zero.");
        }
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 14 added in this task.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/InspectedModelFacts.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Contracts/InspectedModelFactsTests.cs
git commit -m "feat(compatibility): add C1-owned inspected model facts"
```

---

### Task 4: Estimate result vocabulary

An estimate must be able to say "I could not establish this", carry the honest caveats behind a
number it did establish, and never hand back a zero that reads as free. This mirrors the
`PlanningContextResolution` / `PlanningContextStatus` pair already in the core.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimationStatus.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimationUnavailableReason.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimationLimitation.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/ResourceEstimate.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Estimation/ResourceEstimateTests.cs`

**Interfaces:**
- Consumes: `ResourceComponent` and `ByteCount` from the completed M1 work.
- Produces: `EstimationStatus`, `EstimationUnavailableReason`, `EstimationLimitation` enums;
  `ResourceEstimate.Established(IReadOnlyList<ResourceComponent> components, IReadOnlySet<EstimationLimitation> limitations)`;
  `ResourceEstimate.NotEstablished(EstimationUnavailableReason reason)`; properties `Status`,
  `Components`, `Limitations`, `Reason`. Consumed by Tasks 6, 7, 9 and 10.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Estimation/ResourceEstimateTests.cs`:

```csharp
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Estimation;

[TestClass]
public sealed class ResourceEstimateTests
{
    private static ResourceComponent Weights(ulong bytes) =>
        ResourceComponent.Create(
            ResourceComponentKind.Weights,
            ResourceTarget.SystemMemory,
            ByteCount.FromBytes(bytes),
            new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration });

    [TestMethod]
    public void Established_CarriesComponentsAndLimitations()
    {
        ResourceEstimate estimate = ResourceEstimate.Established(
            [Weights(1000)],
            new HashSet<EstimationLimitation>
            {
                EstimationLimitation.WeightsDerivedFromFileLength
            });

        Assert.AreEqual(nameof(EstimationStatus.Established), estimate.Status.ToString());
        Assert.AreEqual(1, estimate.Components.Count);
        Assert.AreEqual(1, estimate.Limitations.Count);
        Assert.AreEqual(nameof(EstimationUnavailableReason.None), estimate.Reason.ToString());
    }

    [TestMethod]
    public void Established_RejectsAnEmptyComponentSet()
    {
        // An established estimate with no components would compose to a peak of
        // zero, which reads as "this configuration is free".
        Assert.ThrowsExactly<ArgumentException>(
            () => ResourceEstimate.Established(
                [],
                new HashSet<EstimationLimitation>()));
    }

    [TestMethod]
    public void Established_RejectsAnUnspecifiedLimitation()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ResourceEstimate.Established(
                [Weights(1000)],
                new HashSet<EstimationLimitation> { EstimationLimitation.Unspecified }));
    }

    [TestMethod]
    public void Established_CopiesComponentsSoLaterMutationCannotChangeIt()
    {
        List<ResourceComponent> components = [Weights(1000)];

        ResourceEstimate estimate = ResourceEstimate.Established(
            components,
            new HashSet<EstimationLimitation>());

        components.Add(Weights(9999));

        Assert.AreEqual(1, estimate.Components.Count);
    }

    [TestMethod]
    public void Established_CopiesLimitationsSoLaterMutationCannotChangeIt()
    {
        HashSet<EstimationLimitation> limitations = [];

        ResourceEstimate estimate = ResourceEstimate.Established([Weights(1000)], limitations);

        limitations.Add(EstimationLimitation.UncalibratedEstimatorPolicy);

        Assert.AreEqual(0, estimate.Limitations.Count);
    }

    [TestMethod]
    public void NotEstablished_CarriesTheReasonAndNoComponents()
    {
        ResourceEstimate estimate = ResourceEstimate.NotEstablished(
            EstimationUnavailableReason.UnknownArchitecture);

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(0, estimate.Components.Count);
        Assert.AreEqual(0, estimate.Limitations.Count);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void NotEstablished_RejectsTheNoneReason()
    {
        // A refusal without a reason cannot be explained to the user, and
        // section 12 requires every unavailable outcome to name its cause.
        Assert.ThrowsExactly<ArgumentException>(
            () => ResourceEstimate.NotEstablished(EstimationUnavailableReason.None));
    }

    [TestMethod]
    public void Estimate_CarriesNoStringMember()
    {
        // Privacy canary, section 14. No string member means no place for a
        // path, filename, model name or native error to hide.
        PropertyInfo[] strings = typeof(ResourceEstimate)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();

        Assert.AreEqual(
            0,
            strings.Length,
            "ResourceEstimate must expose no string member: "
            + string.Join(", ", strings.Select(property => property.Name)));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'ResourceEstimate' could not be found`.

- [ ] **Step 3: Implement the three enums**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimationStatus.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// Whether a resource estimate could be produced at all.
/// </summary>
internal enum EstimationStatus
{
    Unspecified = 0,
    Established,
    NotEstablished
}
```

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimationUnavailableReason.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// The single fact that stopped an estimate being produced. Stable codes, never
/// free-form text, so the reason can be shown without leaking anything.
/// </summary>
internal enum EstimationUnavailableReason
{
    None = 0,

    /// <summary>No versioned estimator constants are available.</summary>
    EstimatorPolicyUnavailable,

    /// <summary>Layers, heads or embedding size were not established.</summary>
    UnknownArchitecture,

    /// <summary>
    /// The encoding of the imported file is unknown, so a different target
    /// weight format cannot be scaled from it.
    /// </summary>
    UnknownSourceQuantisation,

    /// <summary>
    /// Partial offload declares no layer count, so weights cannot be divided
    /// between system and device memory.
    /// </summary>
    UnknownOffloadSplit,

    /// <summary>The device and offload combination is not an admitted route.</summary>
    UnsupportedDeviceRoute,

    /// <summary>
    /// The KV-cache format has no recorded block encoding. A newly added format
    /// lands here rather than being sized as free.
    /// </summary>
    UnsupportedCacheFormat,

    /// <summary>The arithmetic overflowed rather than wrapping to a smaller value.</summary>
    QuantitiesExceedRepresentableRange
}
```

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimationLimitation.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// A recorded caveat about how an established number was reached. These are what
/// pin an assessment to the Estimated evidence grade; nothing is presented as
/// measured that was not measured.
/// </summary>
internal enum EstimationLimitation
{
    Unspecified = 0,

    /// <summary>
    /// Weight memory came from the artifact's byte length plus overhead rather
    /// than from a tensor-level read. Derived from a measured quantity, but less
    /// precise than reading the tensor table.
    /// </summary>
    WeightsDerivedFromFileLength,

    /// <summary>
    /// Weight memory was scaled between two quantisations by average bits per
    /// weight. Embeddings and normalisation tensors do not scale linearly.
    /// </summary>
    WeightsScaledAcrossQuantisation,

    /// <summary>The estimator constants are provisional, not calibrated.</summary>
    UncalibratedEstimatorPolicy,

    /// <summary>
    /// One sequence was assumed. Nothing in a candidate declares parallel
    /// sequences yet, so a batched server workload is out of scope of this number.
    /// </summary>
    SingleSequenceAssumed
}
```

- [ ] **Step 4: Implement the estimate**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/ResourceEstimate.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// The outcome of estimating one candidate. Either a complete component set with
/// its recorded caveats, or a refusal naming what was missing. There is no third
/// state, and an unestablished estimate never carries components: a partial
/// component set would compose into a peak that looks like a real number.
/// </summary>
internal sealed record ResourceEstimate
{
    private ResourceEstimate(
        EstimationStatus status,
        IReadOnlyList<ResourceComponent> components,
        IReadOnlySet<EstimationLimitation> limitations,
        EstimationUnavailableReason reason)
    {
        Status = status;
        Components = components;
        Limitations = limitations;
        Reason = reason;
    }

    internal EstimationStatus Status { get; }

    internal IReadOnlyList<ResourceComponent> Components { get; }

    internal IReadOnlySet<EstimationLimitation> Limitations { get; }

    internal EstimationUnavailableReason Reason { get; }

    internal static ResourceEstimate Established(
        IReadOnlyList<ResourceComponent> components,
        IReadOnlySet<EstimationLimitation> limitations)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(limitations);

        if (components.Count == 0)
        {
            throw new ArgumentException(
                "An established estimate with no components would compose to a peak "
                + "of zero, which reads as a configuration that costs nothing.",
                nameof(components));
        }

        if (limitations.Contains(EstimationLimitation.Unspecified))
        {
            throw new ArgumentException(
                "A caveat must name itself; an unspecified limitation tells the "
                + "user nothing about why the number is uncertain.",
                nameof(limitations));
        }

        // Copy both so a later caller mutation cannot change a recorded estimate.
        return new ResourceEstimate(
            EstimationStatus.Established,
            [.. components],
            new HashSet<EstimationLimitation>(limitations),
            EstimationUnavailableReason.None);
    }

    internal static ResourceEstimate NotEstablished(EstimationUnavailableReason reason)
    {
        if (reason == EstimationUnavailableReason.None)
        {
            throw new ArgumentException(
                "A refusal must name its cause so the user can be told what is "
                + "missing and what would fix it.",
                nameof(reason));
        }

        return new ResourceEstimate(
            EstimationStatus.NotEstablished,
            [],
            new HashSet<EstimationLimitation>(),
            reason);
    }
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 8 added in this task.

- [ ] **Step 6: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/ tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Estimation/
git commit -m "feat(compatibility): add estimate result vocabulary"
```

---

### Task 5: Versioned estimator policy

Spec section 9: every reserve, allowance, threshold and margin lives in a versioned asset with an
explicit provenance, and `Absent` means evaluation returns `NotEstablished` rather than inventing a
constant. The estimator's overheads calibrate from a different dataset than the safety reserves, so
they get their own policy and their own version string.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimatorPolicy.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Estimation/EstimatorPolicyTests.cs`

**Interfaces:**
- Consumes: `ByteCount` and `ContextTokenCount` from M1; `PolicyProvenance` from the completed
  safety-policy work.
- Produces: `EstimatorPolicy.ProvisionalV1()`, `EstimatorPolicy.Absent()`, properties `Provenance`
  and `PolicyVersion`, the `Terms` property returning `EstimatorTerms` (throwing when absent), and
  `EstimatorPolicy.ComputeBufferFor(ContextTokenCount context)` returning `ByteCount`.
  `EstimatorTerms` exposes `AllocationAlignment` (`ulong`), `WeightOverheadFraction` (`decimal`),
  `ComputeBufferFloor` (`ByteCount`), `ComputeBufferBytesPerContextToken` (`ulong`),
  `CpuBackendAllocation` (`ByteCount`), `GpuBackendAllocation` (`ByteCount`),
  `StagingBufferFraction` (`decimal`), `StagingBufferFloor` (`ByteCount`) and `ApplicationOverhead`
  (`ByteCount`). Consumed by Tasks 7, 9 and 10.

> **Provisional, not measured.** Every figure in `ProvisionalV1` is a documented default taken from
> the approved workflow documents, not a measurement. Its provenance is `Provisional`, which forces
> the `UncalibratedEstimatorPolicy` limitation onto every estimate built from it. Changing these
> numbers after calibration is a data change, not a code change.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Estimation/EstimatorPolicyTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Estimation;

[TestClass]
public sealed class EstimatorPolicyTests
{
    [TestMethod]
    public void ProvisionalV1_IsProvisionalNotCalibrated()
    {
        // These numbers are documented defaults, not measurements. Claiming
        // Calibrated here would let a higher evidence grade become reachable
        // without a predicted-versus-measured dataset behind it.
        Assert.AreEqual(
            nameof(PolicyProvenance.Provisional),
            EstimatorPolicy.ProvisionalV1().Provenance.ToString());
    }

    [TestMethod]
    public void ProvisionalV1_CarriesAStableVersionString()
    {
        Assert.AreEqual("estimator-policy-v1", EstimatorPolicy.ProvisionalV1().PolicyVersion);
    }

    [TestMethod]
    public void ProvisionalV1_VersionIsDistinctFromTheSafetyPolicyVersion()
    {
        // Two policies that calibrate from different datasets must be versioned
        // separately, or recalibrating one silently invalidates the other's claim.
        Assert.AreNotEqual(
            SafetyPolicy.ProvisionalV1().PolicyVersion,
            EstimatorPolicy.ProvisionalV1().PolicyVersion);
    }

    [TestMethod]
    public void Absent_IsAbsentProvenance()
    {
        Assert.AreEqual(
            nameof(PolicyProvenance.Absent),
            EstimatorPolicy.Absent().Provenance.ToString());
    }

    [TestMethod]
    public void Absent_ExposesNoTerms()
    {
        // Reaching for a term on an absent policy is the moment a constant would
        // be invented, so it throws rather than returning zero.
        Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = EstimatorPolicy.Absent().Terms);
    }

    [TestMethod]
    public void Absent_RefusesToSizeAComputeBuffer()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => EstimatorPolicy.Absent().ComputeBufferFor(ContextTokenCount.FromTokens(4096)));
    }

    [TestMethod]
    [DataRow("AllocationAlignment")]
    [DataRow("ComputeBufferFloor")]
    [DataRow("ComputeBufferBytesPerContextToken")]
    [DataRow("CpuBackendAllocation")]
    [DataRow("GpuBackendAllocation")]
    [DataRow("StagingBufferFloor")]
    [DataRow("ApplicationOverhead")]
    public void ProvisionalV1_EveryByteTermIsPositive(string termName)
    {
        EstimatorTerms terms = EstimatorPolicy.ProvisionalV1().Terms;

        ulong value = termName switch
        {
            "AllocationAlignment" => terms.AllocationAlignment,
            "ComputeBufferFloor" => terms.ComputeBufferFloor.Bytes,
            "ComputeBufferBytesPerContextToken" => terms.ComputeBufferBytesPerContextToken,
            "CpuBackendAllocation" => terms.CpuBackendAllocation.Bytes,
            "GpuBackendAllocation" => terms.GpuBackendAllocation.Bytes,
            "StagingBufferFloor" => terms.StagingBufferFloor.Bytes,
            "ApplicationOverhead" => terms.ApplicationOverhead.Bytes,
            _ => throw new ArgumentOutOfRangeException(nameof(termName))
        };

        Assert.IsTrue(value > 0, $"{termName} must be a positive number of bytes.");
    }

    [TestMethod]
    public void ProvisionalV1_OverheadFractionsAreBetweenZeroAndOne()
    {
        EstimatorTerms terms = EstimatorPolicy.ProvisionalV1().Terms;

        Assert.IsTrue(terms.WeightOverheadFraction is > 0m and < 1m);
        Assert.IsTrue(terms.StagingBufferFraction is > 0m and < 1m);
    }

    [TestMethod]
    public void ComputeBufferFor_NeverFallsBelowTheFloor()
    {
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();

        Assert.AreEqual(
            policy.Terms.ComputeBufferFloor.Bytes,
            policy.ComputeBufferFor(ContextTokenCount.FromTokens(1)).Bytes);
    }

    [TestMethod]
    public void ComputeBufferFor_NeverShrinksAsContextGrows()
    {
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();
        ulong previous = 0;

        foreach (int tokens in new[] { 1024, 2048, 4096, 8192, 16384, 32768 })
        {
            ulong current = policy.ComputeBufferFor(ContextTokenCount.FromTokens(tokens)).Bytes;

            Assert.IsTrue(
                current >= previous,
                $"A larger context must never need a smaller compute buffer ({tokens} tokens).");

            previous = current;
        }
    }

    [TestMethod]
    public void ComputeBufferFor_ScalesWithContextOnceTheFloorIsExceeded()
    {
        EstimatorPolicy policy = EstimatorPolicy.ProvisionalV1();

        ulong atSixteenK = policy.ComputeBufferFor(ContextTokenCount.FromTokens(16384)).Bytes;
        ulong atThirtyTwoK = policy.ComputeBufferFor(ContextTokenCount.FromTokens(32768)).Bytes;

        Assert.IsTrue(
            atThirtyTwoK > atSixteenK,
            "Above the floor the compute buffer must track the context length.");
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'EstimatorPolicy' could not be found`.

- [ ] **Step 3: Implement the policy and its terms**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimatorPolicy.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// The tunable numbers the estimator needs, held together so they version and
/// calibrate as one set.
/// </summary>
internal sealed record EstimatorTerms(
    ulong AllocationAlignment,
    decimal WeightOverheadFraction,
    ByteCount ComputeBufferFloor,
    ulong ComputeBufferBytesPerContextToken,
    ByteCount CpuBackendAllocation,
    ByteCount GpuBackendAllocation,
    decimal StagingBufferFraction,
    ByteCount StagingBufferFloor,
    ByteCount ApplicationOverhead);

/// <summary>
/// Versioned estimator constants with an explicit provenance. Kept separate from
/// SafetyPolicy because reserves and estimator overheads calibrate from different
/// datasets: recalibrating one must not bump the other's version and invalidate
/// its provenance claim.
///
/// Version one ships provisional values from the approved workflow documents.
/// They are documented defaults, not measurements, which is why every estimate
/// built on them records the UncalibratedEstimatorPolicy limitation.
/// </summary>
internal sealed record EstimatorPolicy
{
    private const ulong Mebibyte = 1024UL * 1024;

    private readonly EstimatorTerms? _terms;

    private EstimatorPolicy(
        PolicyProvenance provenance,
        string policyVersion,
        EstimatorTerms? terms)
    {
        Provenance = provenance;
        PolicyVersion = policyVersion;
        _terms = terms;
    }

    internal PolicyProvenance Provenance { get; }

    internal string PolicyVersion { get; }

    internal EstimatorTerms Terms =>
        _terms ?? throw new InvalidOperationException(
            "An absent estimator policy exposes no terms; the caller must report "
            + "NotEstablished rather than fall back to an invented constant.");

    /// <summary>
    /// Scratch memory the runtime needs while evaluating a graph. It grows with
    /// context and never drops below a floor, because even a tiny context still
    /// materialises full-width intermediates.
    /// </summary>
    internal ByteCount ComputeBufferFor(ContextTokenCount context)
    {
        EstimatorTerms terms = Terms;

        ByteCount scaled = ByteCount.FromBytes(
            checked((ulong)context.Tokens * terms.ComputeBufferBytesPerContextToken));

        return scaled > terms.ComputeBufferFloor ? scaled : terms.ComputeBufferFloor;
    }

    internal static EstimatorPolicy ProvisionalV1() => new(
        PolicyProvenance.Provisional,
        policyVersion: "estimator-policy-v1",
        terms: new EstimatorTerms(
            AllocationAlignment: 4096,
            WeightOverheadFraction: 0.03m,
            ComputeBufferFloor: ByteCount.FromBytes(128 * Mebibyte),
            ComputeBufferBytesPerContextToken: 32 * 1024,
            CpuBackendAllocation: ByteCount.FromBytes(64 * Mebibyte),
            GpuBackendAllocation: ByteCount.FromBytes(256 * Mebibyte),
            StagingBufferFraction: 0.05m,
            StagingBufferFloor: ByteCount.FromBytes(64 * Mebibyte),
            ApplicationOverhead: ByteCount.FromBytes(512 * Mebibyte)));

    internal static EstimatorPolicy Absent() => new(
        PolicyProvenance.Absent,
        policyVersion: "absent",
        terms: null);
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 17 added in this task.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimatorPolicy.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Estimation/EstimatorPolicyTests.cs
git commit -m "feat(compatibility): add versioned estimator policy"
```

---

### Task 6: KV cache sizing

Spec section 8 gives the formula and one hard rule: block-quantised caches use the real encoded
block size, not the nominal bit width. The examined SYCL `TQ3_0` stores a 32-value block in 14
bytes — roughly 3.5 bits per value, not 3. Using 3 would understate the cache by about 14%, in the
false-safe direction.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufKvCacheBlockSpec.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufKvCacheEstimator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufKvCacheEstimatorTests.cs`

**Interfaces:**
- Consumes: `InspectedModelFacts` (Task 3), `EstimationUnavailableReason` (Task 4),
  `ContextTokenCount` and `ByteCount` from M1, `GgufKvCacheFormat` from M3a.
- Produces: `GgufKvCacheBlockSpec` (readonly record struct with `ValuesPerBlock` and `BytesPerBlock`
  and the static `GgufKvCacheBlockSpec.For(GgufKvCacheFormat format)`); and
  `GgufKvCacheEstimator.TryEstimate(InspectedModelFacts facts, ContextTokenCount context, GgufKvCacheFormat format, out ByteCount bytes, out EstimationUnavailableReason reason)`
  returning `bool`. Consumed by Tasks 9 and 10.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufKvCacheEstimatorTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufKvCacheEstimatorTests
{
    private static InspectedModelFacts Facts(
        int? layers = 32,
        int? embedding = 4096,
        int? attentionHeads = 32,
        int? kvHeads = 8) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layers,
            embedding,
            attentionHeads,
            kvHeads,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

    private static ulong Estimate(
        GgufKvCacheFormat format,
        int tokens = 4096,
        InspectedModelFacts? facts = null)
    {
        bool established = GgufKvCacheEstimator.TryEstimate(
            facts ?? Facts(),
            ContextTokenCount.FromTokens(tokens),
            format,
            out ByteCount bytes,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        return bytes.Bytes;
    }

    [TestMethod]
    public void F16_MatchesTheHandCalculatedFigure()
    {
        // headDim = 4096 / 32 = 128. Per layer, per tensor:
        //   4096 tokens x 8 kv heads x 128 = 4,194,304 values x 2 bytes = 8,388,608.
        // Two tensors (K and V) across 32 layers: 8,388,608 x 2 x 32 = 536,870,912.
        Assert.AreEqual(536_870_912UL, Estimate(GgufKvCacheFormat.F16));
    }

    [TestMethod]
    public void Q8_0_UsesTheEncodedBlockSizeNotTheNominalByte()
    {
        // 4,194,304 values / 32 per block = 131,072 blocks x 34 bytes = 4,456,448
        // per tensor. Two tensors, 32 layers: 285,212,672.
        // A nominal "one byte per value" would give 268,435,456 - a 6% understatement.
        Assert.AreEqual(285_212_672UL, Estimate(GgufKvCacheFormat.Q8_0));
    }

    [TestMethod]
    public void TurboQuant3Bit_UsesTheMeasuredFourteenByteBlock()
    {
        // 131,072 blocks x 14 bytes = 1,835,008 per tensor.
        // Two tensors, 32 layers: 117,440,512.
        // A nominal 3 bits per value would give 100,663,296 - a 14% understatement
        // in the false-safe direction.
        Assert.AreEqual(117_440_512UL, Estimate(GgufKvCacheFormat.TurboQuant3Bit));
    }

    [TestMethod]
    public void PartialBlocks_RoundUpToAWholeBlock()
    {
        // headDim 1, one kv head, one layer, 33 tokens: 33 values in Q8_0 needs
        // two 32-value blocks, so 68 bytes per tensor and 136 for K and V.
        InspectedModelFacts facts = Facts(layers: 1, embedding: 1, attentionHeads: 1, kvHeads: 1);

        Assert.AreEqual(136UL, Estimate(GgufKvCacheFormat.Q8_0, tokens: 33, facts: facts));
    }

    [TestMethod]
    public void ExactBlockBoundary_DoesNotAddASpareBlock()
    {
        InspectedModelFacts facts = Facts(layers: 1, embedding: 1, attentionHeads: 1, kvHeads: 1);

        Assert.AreEqual(68UL, Estimate(GgufKvCacheFormat.Q8_0, tokens: 32, facts: facts));
    }

    [TestMethod]
    public void GroupedQueryAttention_UsesKeyValueHeadsNotAttentionHeads()
    {
        // Eight kv heads against 32 attention heads is a factor of four. Using the
        // attention head count would overstate the cache four times over.
        ulong eight = Estimate(GgufKvCacheFormat.F16, facts: Facts(kvHeads: 8));
        ulong thirtyTwo = Estimate(GgufKvCacheFormat.F16, facts: Facts(kvHeads: 32));

        Assert.AreEqual(eight * 4, thirtyTwo);
    }

    [TestMethod]
    public void DoublingContext_DoublesTheCache()
    {
        Assert.AreEqual(
            Estimate(GgufKvCacheFormat.F16, tokens: 2048) * 2,
            Estimate(GgufKvCacheFormat.F16, tokens: 4096));
    }

    [TestMethod]
    [DataRow(null, 4096, 32, 8)]
    [DataRow(32, null, 32, 8)]
    [DataRow(32, 4096, null, 8)]
    [DataRow(32, 4096, 32, null)]
    public void AnyMissingArchitecturalFact_CollapsesTheEstimate(
        int? layers,
        int? embedding,
        int? attentionHeads,
        int? kvHeads)
    {
        bool established = GgufKvCacheEstimator.TryEstimate(
            Facts(layers, embedding, attentionHeads, kvHeads),
            ContextTokenCount.FromTokens(4096),
            GgufKvCacheFormat.F16,
            out ByteCount bytes,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            reason.ToString());
        Assert.AreEqual(ByteCount.Zero, bytes);
    }

    [TestMethod]
    public void NonDivisibleHeadDimension_CollapsesTheEstimate()
    {
        // 4097 / 32 is not a whole head dimension, so the architecture as read
        // does not describe a model this estimator can size.
        bool established = GgufKvCacheEstimator.TryEstimate(
            Facts(embedding: 4097),
            ContextTokenCount.FromTokens(4096),
            GgufKvCacheFormat.F16,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            reason.ToString());
    }

    [TestMethod]
    public void UnspecifiedFormat_IsRejectedRatherThanSizedAsZero()
    {
        bool established = GgufKvCacheEstimator.TryEstimate(
            Facts(),
            ContextTokenCount.FromTokens(4096),
            GgufKvCacheFormat.Unspecified,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnsupportedCacheFormat),
            reason.ToString());
    }

    [TestMethod]
    public void ExtremeArchitecture_OverflowsLoudlyRatherThanWrapping()
    {
        // A wrapped multiplication would report a tiny cache for an enormous
        // model, which is the worst possible false-safe result.
        InspectedModelFacts facts = Facts(
            layers: int.MaxValue,
            embedding: int.MaxValue,
            attentionHeads: 1,
            kvHeads: int.MaxValue);

        bool established = GgufKvCacheEstimator.TryEstimate(
            facts,
            ContextTokenCount.FromTokens(int.MaxValue),
            GgufKvCacheFormat.F16,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.QuantitiesExceedRepresentableRange),
            reason.ToString());
    }

    [TestMethod]
    [DataRow(nameof(GgufKvCacheFormat.F16), 1, 2)]
    [DataRow(nameof(GgufKvCacheFormat.Q8_0), 32, 34)]
    [DataRow(nameof(GgufKvCacheFormat.TurboQuant3Bit), 32, 14)]
    public void BlockSpec_RecordsTheRealEncoding(
        string format,
        int valuesPerBlock,
        int bytesPerBlock)
    {
        GgufKvCacheBlockSpec spec = GgufKvCacheBlockSpec.For(
            Enum.Parse<GgufKvCacheFormat>(format));

        Assert.AreEqual(valuesPerBlock, spec.ValuesPerBlock);
        Assert.AreEqual(bytesPerBlock, spec.BytesPerBlock);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'GgufKvCacheEstimator' could not be found`.

- [ ] **Step 3: Implement the block spec**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufKvCacheBlockSpec.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// How many bytes one KV-cache format actually spends on a block of values.
///
/// This is deliberately the encoded block size rather than a nominal bit width.
/// The examined SYCL TQ3_0 stores a 32-value block in 14 bytes - about 3.5 bits
/// per value, not 3 - and Q8_0 spends 34 bytes on 32 values because each block
/// carries a scale alongside its quantised values. Using the nominal width would
/// understate the cache, which is a false-safe error.
/// </summary>
internal readonly record struct GgufKvCacheBlockSpec
{
    private GgufKvCacheBlockSpec(int valuesPerBlock, int bytesPerBlock)
    {
        ValuesPerBlock = valuesPerBlock;
        BytesPerBlock = bytesPerBlock;
    }

    internal int ValuesPerBlock { get; }

    internal int BytesPerBlock { get; }

    /// <summary>
    /// Returns false for a format with no established encoding, so a new enum
    /// member can never fall through to a silently free cache.
    /// </summary>
    internal static bool TryFor(GgufKvCacheFormat format, out GgufKvCacheBlockSpec spec)
    {
        spec = format switch
        {
            GgufKvCacheFormat.F16 => new GgufKvCacheBlockSpec(1, 2),
            GgufKvCacheFormat.Q8_0 => new GgufKvCacheBlockSpec(32, 34),
            GgufKvCacheFormat.TurboQuant3Bit => new GgufKvCacheBlockSpec(32, 14),
            _ => default
        };

        return spec.ValuesPerBlock > 0;
    }

    internal static GgufKvCacheBlockSpec For(GgufKvCacheFormat format) =>
        TryFor(format, out GgufKvCacheBlockSpec spec)
            ? spec
            : throw new ArgumentOutOfRangeException(
                nameof(format),
                "This KV-cache format has no recorded encoding; sizing it would "
                + "mean inventing a block layout.");
}
```

- [ ] **Step 4: Implement the estimator**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufKvCacheEstimator.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Sizes the key/value cache for one context length and format.
///
/// Blocking is applied per layer and per tensor rather than once over the whole
/// cache, because each tensor is allocated separately and each rounds up to a
/// whole block on its own.
/// </summary>
internal static class GgufKvCacheEstimator
{
    /// <summary>
    /// Nothing in a candidate declares parallel sequences yet, so one sequence is
    /// assumed and the assumption is recorded as a limitation by the caller.
    /// </summary>
    private const ulong ParallelSequences = 1;

    internal static bool TryEstimate(
        InspectedModelFacts facts,
        ContextTokenCount context,
        GgufKvCacheFormat format,
        out ByteCount bytes,
        out EstimationUnavailableReason reason)
    {
        ArgumentNullException.ThrowIfNull(facts);

        bytes = ByteCount.Zero;

        if (!GgufKvCacheBlockSpec.TryFor(format, out GgufKvCacheBlockSpec spec))
        {
            reason = EstimationUnavailableReason.UnsupportedCacheFormat;
            return false;
        }

        if (facts.LayerCount is not { } layers ||
            facts.KeyValueHeadCount is not { } keyValueHeads ||
            facts.EmbeddingSize is not { } embedding ||
            facts.AttentionHeadCount is not { } attentionHeads)
        {
            reason = EstimationUnavailableReason.UnknownArchitecture;
            return false;
        }

        // A head dimension that does not divide evenly means the architecture as
        // read does not describe a model this estimator can size. Rounding it
        // would be inventing a shape.
        if (embedding % attentionHeads != 0)
        {
            reason = EstimationUnavailableReason.UnknownArchitecture;
            return false;
        }

        int headDimension = embedding / attentionHeads;

        try
        {
            checked
            {
                ulong valuesPerTensorPerLayer =
                    (ulong)context.Tokens
                    * (ulong)keyValueHeads
                    * (ulong)headDimension
                    * ParallelSequences;

                ulong blocks = CeilingDivide(valuesPerTensorPerLayer, (ulong)spec.ValuesPerBlock);
                ulong bytesPerTensorPerLayer = blocks * (ulong)spec.BytesPerBlock;

                // Key and value tensors are sized separately so an asymmetric
                // format can be introduced without reshaping this calculation.
                ulong keyBytes = bytesPerTensorPerLayer;
                ulong valueBytes = bytesPerTensorPerLayer;

                bytes = ByteCount.FromBytes((keyBytes + valueBytes) * (ulong)layers);
            }
        }
        catch (OverflowException)
        {
            bytes = ByteCount.Zero;
            reason = EstimationUnavailableReason.QuantitiesExceedRepresentableRange;
            return false;
        }

        reason = EstimationUnavailableReason.None;
        return true;
    }

    private static ulong CeilingDivide(ulong value, ulong divisor)
    {
        ulong quotient = value / divisor;
        return value % divisor == 0 ? quotient : checked(quotient + 1);
    }
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 17 added in this task.

- [ ] **Step 6: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufKvCacheBlockSpec.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufKvCacheEstimator.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufKvCacheEstimatorTests.cs
git commit -m "feat(compatibility): size the KV cache from the real block encoding"
```

---

### Task 7: Weight memory

Spec section 8 records this as a deliberate limitation: C1 receives model byte length and
quantisation identifiers, not per-tensor sizes. Version one bases weight memory on file length plus
an explicit alignment and overhead term, and records that limitation on the result. A future GGUF
tensor reader slots in behind this same function.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufWeightEstimator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufWeightEstimatorTests.cs`

**Interfaces:**
- Consumes: `InspectedModelFacts` (Task 3), `EstimationUnavailableReason` (Task 4),
  `EstimatorPolicy` (Task 5), `WeightQuantisationMap` (Task 2), `GgufWeightFormat` from M3a,
  `ByteCount.AlignUpTo` and `ByteCount.MultiplyByFraction` (Task 1).
- Produces:
  `GgufWeightEstimator.TryEstimate(InspectedModelFacts facts, GgufWeightFormat target, EstimatorPolicy policy, out ByteCount bytes, out bool scaledAcrossQuantisation, out EstimationUnavailableReason reason)`
  returning `bool`. Consumed by Tasks 9 and 10.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufWeightEstimatorTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufWeightEstimatorTests
{
    // File type 15 is Q4_K_M, quantisation version 2.
    private static InspectedModelFacts Facts(
        ulong fileLength = 4_000_000_000,
        int? fileType = 15,
        int? quantisationVersion = 2) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(fileLength),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType,
            quantisationVersion);

    private static ulong Estimate(
        GgufWeightFormat target,
        InspectedModelFacts? facts = null,
        EstimatorPolicy? policy = null)
    {
        bool established = GgufWeightEstimator.TryEstimate(
            facts ?? Facts(),
            target,
            policy ?? EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        return bytes.Bytes;
    }

    [TestMethod]
    public void Imported_IsFileLengthPlusOverheadAlignedUpward()
    {
        // 4,000,000,000 + ceil(4,000,000,000 x 0.03) = 4,120,000,000.
        // 4,120,000,000 mod 4096 = 1,536, so it aligns up to 4,120,002,560.
        Assert.AreEqual(4_120_002_560UL, Estimate(GgufWeightFormat.Imported));
    }

    [TestMethod]
    public void Imported_NeedsNoSourceQuantisation()
    {
        // The imported file is used unchanged, so its encoding is irrelevant to
        // its size. This is the common path and must survive an unread file type.
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(fileType: null, quantisationVersion: null),
            GgufWeightFormat.Imported,
            EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out bool scaled,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(established, $"Expected an established estimate, got {reason}.");
        Assert.IsFalse(scaled);
        Assert.IsTrue(bytes.Bytes > 0);
    }

    [TestMethod]
    public void ConvertingToALargerEncoding_ProducesMoreBytes()
    {
        // Q4_K_M at 4.8125 bits to Q8_0 at 8.5 bits is a factor of about 1.77.
        Assert.IsTrue(
            Estimate(GgufWeightFormat.Q8_0) > Estimate(GgufWeightFormat.Imported),
            "A higher-precision target must not estimate smaller than the source.");
    }

    [TestMethod]
    public void ConvertingToASmallerEncoding_ProducesFewerBytes()
    {
        Assert.IsTrue(
            Estimate(GgufWeightFormat.Q3KM) < Estimate(GgufWeightFormat.Imported),
            "A lower-precision target must not estimate larger than the source.");
    }

    [TestMethod]
    public void ConvertingToTheSourceEncoding_MatchesTheImportedEstimate()
    {
        // Source is Q4_K_M, so requesting Q4KM is a ratio of one.
        Assert.AreEqual(
            Estimate(GgufWeightFormat.Imported),
            Estimate(GgufWeightFormat.Q4KM));
    }

    [TestMethod]
    public void ScalingFlag_IsSetOnlyWhenTheEncodingChanged()
    {
        GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Imported,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out bool importedScaled,
            out _);

        GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Q8_0,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out bool convertedScaled,
            out _);

        Assert.IsFalse(importedScaled);
        Assert.IsTrue(convertedScaled);
    }

    [TestMethod]
    public void UnknownSourceEncoding_BlocksOnlyAConvertedTarget()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(fileType: 9999),
            GgufWeightFormat.Q8_0,
            EstimatorPolicy.ProvisionalV1(),
            out ByteCount bytes,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownSourceQuantisation),
            reason.ToString());
        Assert.AreEqual(ByteCount.Zero, bytes);
    }

    [TestMethod]
    public void UnspecifiedTargetFormat_IsRejected()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Unspecified,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnsupportedWeightFormat),
            reason.ToString());
    }

    [TestMethod]
    public void AbsentPolicy_RefusesRatherThanUsingAZeroOverhead()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(),
            GgufWeightFormat.Imported,
            EstimatorPolicy.Absent(),
            out _,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.EstimatorPolicyUnavailable),
            reason.ToString());
    }

    [TestMethod]
    public void Result_IsAlwaysAlignedToTheAllocationBoundary()
    {
        ulong alignment = EstimatorPolicy.ProvisionalV1().Terms.AllocationAlignment;

        foreach (ulong length in new ulong[] { 1, 4095, 4096, 4097, 1_234_567_890 })
        {
            Assert.AreEqual(
                0UL,
                Estimate(GgufWeightFormat.Imported, Facts(fileLength: length)) % alignment,
                $"A {length}-byte model must still align to {alignment}.");
        }
    }

    [TestMethod]
    public void Result_IsNeverSmallerThanTheFileItself()
    {
        // Overhead is added, never netted off. A weight estimate below the file
        // length would be a false-safe result for the imported baseline.
        foreach (ulong length in new ulong[] { 1024, 1_000_000, 8_000_000_000 })
        {
            Assert.IsTrue(
                Estimate(GgufWeightFormat.Imported, Facts(fileLength: length)) >= length);
        }
    }

    [TestMethod]
    public void ExtremeFileLength_OverflowsLoudlyRatherThanWrapping()
    {
        bool established = GgufWeightEstimator.TryEstimate(
            Facts(fileLength: ulong.MaxValue),
            GgufWeightFormat.Imported,
            EstimatorPolicy.ProvisionalV1(),
            out _,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(established);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.QuantitiesExceedRepresentableRange),
            reason.ToString());
    }
}
```

- [ ] **Step 2: Add the missing reason and run the tests to verify they fail**

`UnsupportedWeightFormat` is referenced above and belongs beside the other unsupported-input codes.
Add it to `EstimationUnavailableReason`, immediately after `UnsupportedCacheFormat`:

```csharp
    /// <summary>
    /// The requested weight format has no canonical encoding. A newly added
    /// format lands here rather than being sized against a guessed bit width.
    /// </summary>
    UnsupportedWeightFormat,
```

Then run the suite command.

Expected: build failure — `The type or namespace name 'GgufWeightEstimator' could not be found`.

- [ ] **Step 3: Implement the estimator**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufWeightEstimator.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Sizes weight memory from the artifact's measured byte length.
///
/// Recorded limitation, spec section 8: C1 receives a file length and
/// quantisation identifiers, not per-tensor sizes. This is derived from a
/// measured quantity and is less precise than a tensor-level read, so every
/// estimate built here carries WeightsDerivedFromFileLength. Converting between
/// encodings scales by average bits per weight, which additionally carries
/// WeightsScaledAcrossQuantisation because embeddings and normalisation tensors
/// do not scale linearly.
/// </summary>
internal static class GgufWeightEstimator
{
    internal static bool TryEstimate(
        InspectedModelFacts facts,
        GgufWeightFormat target,
        EstimatorPolicy policy,
        out ByteCount bytes,
        out bool scaledAcrossQuantisation,
        out EstimationUnavailableReason reason)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(policy);

        bytes = ByteCount.Zero;
        scaledAcrossQuantisation = false;

        if (policy.Provenance is PolicyProvenance.Absent or PolicyProvenance.Unspecified)
        {
            reason = EstimationUnavailableReason.EstimatorPolicyUnavailable;
            return false;
        }

        if (target == GgufWeightFormat.Unspecified)
        {
            reason = EstimationUnavailableReason.UnsupportedWeightFormat;
            return false;
        }

        try
        {
            ByteCount payload = facts.FileLength;

            if (target != GgufWeightFormat.Imported)
            {
                WeightQuantisation targetQuantisation =
                    WeightQuantisationMap.FromWeightFormat(target);

                if (targetQuantisation == WeightQuantisation.Unknown)
                {
                    reason = EstimationUnavailableReason.UnsupportedWeightFormat;
                    return false;
                }

                WeightQuantisation sourceQuantisation = WeightQuantisationMap.FromGgufFileType(
                    facts.FileType, facts.QuantisationVersion);

                // Without the source encoding there is nothing to scale from.
                // Assuming one would silently resize the whole model.
                if (sourceQuantisation == WeightQuantisation.Unknown)
                {
                    reason = EstimationUnavailableReason.UnknownSourceQuantisation;
                    return false;
                }

                decimal ratio =
                    WeightQuantisationMap.BitsPerWeight(targetQuantisation)
                    / WeightQuantisationMap.BitsPerWeight(sourceQuantisation);

                payload = payload.MultiplyByFraction(ratio);
                scaledAcrossQuantisation = true;
            }

            ByteCount overhead = payload.MultiplyByFraction(
                policy.Terms.WeightOverheadFraction);

            bytes = payload.Add(overhead).AlignUpTo(policy.Terms.AllocationAlignment);
        }
        catch (OverflowException)
        {
            bytes = ByteCount.Zero;
            scaledAcrossQuantisation = false;
            reason = EstimationUnavailableReason.QuantitiesExceedRepresentableRange;
            return false;
        }

        reason = EstimationUnavailableReason.None;
        return true;
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 12 added in this task.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufWeightEstimator.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/EstimationUnavailableReason.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufWeightEstimatorTests.cs
git commit -m "feat(compatibility): estimate weight memory from measured file length"
```

---

### Task 8: Pool routing

Spec section 8 fixes which pool each requirement is charged against, and section 10 admits only
certain device and offload pairings. `Partial` offload carries no layer count, so it has no
arithmetic meaning and must refuse rather than guess a split — an invented split would understate
one pool while overstating the other, and the understated side is a false-safe result.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufPoolRouting.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufPoolRoutingTests.cs`

**Interfaces:**
- Consumes: `ResourceTarget`, `DeviceRouteId` from M1/M3a; `GpuOffloadLevel` from M3a;
  `EstimationUnavailableReason` (Task 4).
- Produces: `GgufPoolRouting` (readonly record struct with `ModelTarget` of type `ResourceTarget`
  and `RequiresHostStaging` of type `bool`) and
  `GgufPoolRouter.TryResolve(DeviceRouteId device, GpuOffloadLevel offload, out GgufPoolRouting routing, out EstimationUnavailableReason reason)`
  returning `bool`. Consumed by Tasks 9 and 10.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufPoolRoutingTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufPoolRoutingTests
{
    [TestMethod]
    [DataRow(
        nameof(DeviceRouteId.Cpu),
        nameof(GpuOffloadLevel.None),
        nameof(ResourceTarget.SystemMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelIntegratedGpu),
        nameof(GpuOffloadLevel.None),
        nameof(ResourceTarget.SystemMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelIntegratedGpu),
        nameof(GpuOffloadLevel.Full),
        nameof(ResourceTarget.SharedDeviceMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelDiscreteGpu),
        nameof(GpuOffloadLevel.None),
        nameof(ResourceTarget.SystemMemory),
        false)]
    [DataRow(
        nameof(DeviceRouteId.IntelDiscreteGpu),
        nameof(GpuOffloadLevel.Full),
        nameof(ResourceTarget.DedicatedDeviceMemory),
        true)]
    public void AdmittedPairings_RouteToOnePoolAndDeclareStaging(
        string device,
        string offload,
        string expectedTarget,
        bool expectedStaging)
    {
        bool resolved = GgufPoolRouter.TryResolve(
            Enum.Parse<DeviceRouteId>(device),
            Enum.Parse<GpuOffloadLevel>(offload),
            out GgufPoolRouting routing,
            out EstimationUnavailableReason reason);

        Assert.IsTrue(resolved, $"Expected a routing, got {reason}.");
        Assert.AreEqual(expectedTarget, routing.ModelTarget.ToString());
        Assert.AreEqual(expectedStaging, routing.RequiresHostStaging);
    }

    [TestMethod]
    [DataRow(nameof(DeviceRouteId.Cpu))]
    [DataRow(nameof(DeviceRouteId.IntelIntegratedGpu))]
    [DataRow(nameof(DeviceRouteId.IntelDiscreteGpu))]
    public void PartialOffload_RefusesBecauseNoLayerCountExists(string device)
    {
        // GpuOffloadLevel declares no number of offloaded layers, so there is no
        // split to compute. Charging everything to one pool would understate the
        // other, and the understated pool is where a crash comes from.
        bool resolved = GgufPoolRouter.TryResolve(
            Enum.Parse<DeviceRouteId>(device),
            GpuOffloadLevel.Partial,
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(resolved);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownOffloadSplit),
            reason.ToString());
    }

    [TestMethod]
    [DataRow(nameof(DeviceRouteId.IntelNpu), nameof(GpuOffloadLevel.Full))]
    [DataRow(nameof(DeviceRouteId.IntelNpu), nameof(GpuOffloadLevel.None))]
    [DataRow(nameof(DeviceRouteId.Unspecified), nameof(GpuOffloadLevel.None))]
    [DataRow(nameof(DeviceRouteId.Cpu), nameof(GpuOffloadLevel.Unspecified))]
    [DataRow(nameof(DeviceRouteId.Cpu), nameof(GpuOffloadLevel.Full))]
    public void UnadmittedPairings_RefuseWithAStableReason(string device, string offload)
    {
        bool resolved = GgufPoolRouter.TryResolve(
            Enum.Parse<DeviceRouteId>(device),
            Enum.Parse<GpuOffloadLevel>(offload),
            out _,
            out EstimationUnavailableReason reason);

        Assert.IsFalse(resolved);
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnsupportedDeviceRoute),
            reason.ToString());
    }

    [TestMethod]
    public void EveryDeviceAndOffloadPairing_IsDecidedNeverSilentlyDefaulted()
    {
        // Exhaustive sweep. A device or offload member added later without a rule
        // here fails this test rather than routing to an unspecified pool.
        foreach (DeviceRouteId device in Enum.GetValues<DeviceRouteId>())
        {
            foreach (GpuOffloadLevel offload in Enum.GetValues<GpuOffloadLevel>())
            {
                bool resolved = GgufPoolRouter.TryResolve(
                    device, offload, out GgufPoolRouting routing, out EstimationUnavailableReason reason);

                if (resolved)
                {
                    Assert.AreNotEqual(
                        ResourceTarget.Unspecified,
                        routing.ModelTarget,
                        $"{device} with {offload} resolved to an unspecified pool.");
                }
                else
                {
                    Assert.AreNotEqual(
                        EstimationUnavailableReason.None,
                        reason,
                        $"{device} with {offload} refused without naming a reason.");
                }
            }
        }
    }

    [TestMethod]
    public void HostStaging_IsDeclaredOnlyWhenWeightsCrossToSeparateMemory()
    {
        // Integrated graphics read the same physical RAM, so there is no upload
        // and no transient host copy. Charging one would overstate the Load phase.
        GgufPoolRouter.TryResolve(
            DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.Full, out GgufPoolRouting shared, out _);
        GgufPoolRouter.TryResolve(
            DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.Full, out GgufPoolRouting dedicated, out _);

        Assert.IsFalse(shared.RequiresHostStaging);
        Assert.IsTrue(dedicated.RequiresHostStaging);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'GgufPoolRouter' could not be found`.

- [ ] **Step 3: Implement the routing**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufPoolRouting.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Which pool a candidate's model memory is charged against, and whether loading
/// it needs a transient host copy.
/// </summary>
internal readonly record struct GgufPoolRouting(
    ResourceTarget ModelTarget,
    bool RequiresHostStaging);

/// <summary>
/// Decides the pool for one device and offload pairing.
///
/// Integrated graphics are charged to shared device memory, which the phase
/// composer folds into system pressure exactly once - it is carved out of the
/// same physical RAM and never adds capacity. Discrete graphics are charged to
/// dedicated memory and additionally need a host staging buffer during load.
/// </summary>
internal static class GgufPoolRouter
{
    internal static bool TryResolve(
        DeviceRouteId device,
        GpuOffloadLevel offload,
        out GgufPoolRouting routing,
        out EstimationUnavailableReason reason)
    {
        routing = default;

        // Partial offload declares no layer count, so there is no split to
        // compute. Refusing is the only honest answer available today.
        if (offload == GpuOffloadLevel.Partial)
        {
            reason = EstimationUnavailableReason.UnknownOffloadSplit;
            return false;
        }

        switch (device, offload)
        {
            case (DeviceRouteId.Cpu, GpuOffloadLevel.None):
            case (DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.None):
            case (DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.None):
                routing = new GgufPoolRouting(ResourceTarget.SystemMemory, false);
                break;

            case (DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.Full):
                routing = new GgufPoolRouting(ResourceTarget.SharedDeviceMemory, false);
                break;

            case (DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.Full):
                routing = new GgufPoolRouting(ResourceTarget.DedicatedDeviceMemory, true);
                break;

            default:
                reason = EstimationUnavailableReason.UnsupportedDeviceRoute;
                return false;
        }

        reason = EstimationUnavailableReason.None;
        return true;
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 15 added in this task.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufPoolRouting.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufPoolRoutingTests.cs
git commit -m "feat(compatibility): route GGUF requirements to one pool each"
```

---

### Task 9: The GGUF resource estimator

Spec section 8: every mandatory component has exactly one owner, so a requirement can never be
counted twice. This assembles the pieces into the component set the phase composer consumes.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufResourceEstimator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufResourceEstimatorTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 3 to 8, plus `CompatibilityCandidate`, `GgufRouteConfiguration`
  and `CandidatePreparation` from M3a and `ResourceComponent` from M1.
- Produces:
  `GgufResourceEstimator.Estimate(InspectedModelFacts facts, CompatibilityCandidate candidate, EstimatorPolicy policy)`
  returning `ResourceEstimate`. Consumed by Task 10.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufResourceEstimatorTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;

[TestClass]
public sealed class GgufResourceEstimatorTests
{
    private static InspectedModelFacts Facts(
        int? layers = 32,
        int? embedding = 4096,
        int? attentionHeads = 32,
        int? kvHeads = 8) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layers,
            embedding,
            attentionHeads,
            kvHeads,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

    private static CompatibilityCandidate Candidate(
        DeviceRouteId device = DeviceRouteId.Cpu,
        CompatibilityBackend backend = CompatibilityBackend.Cpu,
        GpuOffloadLevel offload = GpuOffloadLevel.None,
        GgufWeightFormat weights = GgufWeightFormat.Imported,
        GgufKvCacheFormat kvCache = GgufKvCacheFormat.F16,
        CandidatePreparation preparation = CandidatePreparation.None,
        int context = 4096) =>
        CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(weights, kvCache, backend, device, offload),
            ContextTokenCount.FromTokens(context),
            preparation,
            supportEntryId: "entry-1",
            isExperimental: false,
            isBaseline: true);

    private static ResourceEstimate Estimate(
        CompatibilityCandidate? candidate = null,
        InspectedModelFacts? facts = null,
        EstimatorPolicy? policy = null) =>
        GgufResourceEstimator.Estimate(
            facts ?? Facts(),
            candidate ?? Candidate(),
            policy ?? EstimatorPolicy.ProvisionalV1());

    private static ResourceComponent Single(
        ResourceEstimate estimate,
        ResourceComponentKind kind) =>
        estimate.Components.Single(component => component.Kind == kind);

    [TestMethod]
    public void CpuBaseline_ChargesEveryComponentToSystemMemory()
    {
        ResourceEstimate estimate = Estimate();

        Assert.AreEqual(nameof(EstimationStatus.Established), estimate.Status.ToString());

        foreach (ResourceComponent component in estimate.Components)
        {
            Assert.AreEqual(
                ResourceTarget.SystemMemory,
                component.Target,
                $"{component.Kind} must be charged to system memory on a CPU route.");
        }
    }

    [TestMethod]
    public void EveryComponentKind_AppearsAtMostOnce()
    {
        // Section 8: every mandatory component has exactly one owner, so a
        // requirement can never be counted twice.
        ResourceEstimate estimate = Estimate();

        int distinct = estimate.Components.Select(component => component.Kind).Distinct().Count();

        Assert.AreEqual(estimate.Components.Count, distinct);
    }

    [TestMethod]
    public void CpuBaseline_EmitsWeightsKvComputeBackendAndApplicationOverhead()
    {
        ResourceEstimate estimate = Estimate();

        ResourceComponentKind[] kinds =
            [.. estimate.Components.Select(component => component.Kind).Order()];

        CollectionAssert.AreEquivalent(
            new[]
            {
                ResourceComponentKind.Weights,
                ResourceComponentKind.KvCache,
                ResourceComponentKind.ComputeBuffer,
                ResourceComponentKind.BackendAllocation,
                ResourceComponentKind.ApplicationOverhead
            },
            kinds);
    }

    [TestMethod]
    public void Weights_AreLiveInEveryPhase()
    {
        // Weights are resident from load until the model is released, so a peak
        // taken in any phase must include them.
        ResourceComponent weights = Single(Estimate(), ResourceComponentKind.Weights);

        Assert.AreEqual(3, weights.Phases.Count);
    }

    [TestMethod]
    public void KvCache_IsLiveOnlyDuringGeneration()
    {
        ResourceComponent kv = Single(Estimate(), ResourceComponentKind.KvCache);

        Assert.AreEqual(1, kv.Phases.Count);
        Assert.IsTrue(kv.Phases.Contains(LifecyclePhase.SteadyStateGeneration));
    }

    [TestMethod]
    public void DiscreteGpu_SplitsDeviceMemoryFromASystemStagingBuffer()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            device: DeviceRouteId.IntelDiscreteGpu,
            backend: CompatibilityBackend.IntelSycl,
            offload: GpuOffloadLevel.Full));

        Assert.AreEqual(
            ResourceTarget.DedicatedDeviceMemory,
            Single(estimate, ResourceComponentKind.Weights).Target);

        ResourceComponent staging = Single(estimate, ResourceComponentKind.StagingBuffer);

        Assert.AreEqual(ResourceTarget.SystemMemory, staging.Target);
        Assert.AreEqual(1, staging.Phases.Count);
        Assert.IsTrue(
            staging.Phases.Contains(LifecyclePhase.Load),
            "A staging buffer is transient and exists only while loading.");
    }

    [TestMethod]
    public void IntegratedGpu_ChargesSharedMemoryAndNeedsNoStaging()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            device: DeviceRouteId.IntelIntegratedGpu,
            backend: CompatibilityBackend.IntelVulkan,
            offload: GpuOffloadLevel.Full));

        Assert.AreEqual(
            ResourceTarget.SharedDeviceMemory,
            Single(estimate, ResourceComponentKind.Weights).Target);

        Assert.IsFalse(
            estimate.Components.Any(
                component => component.Kind == ResourceComponentKind.StagingBuffer),
            "Integrated graphics read the same RAM, so nothing is staged.");
    }

    [TestMethod]
    public void PartialOffload_IsNotEstablished()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            device: DeviceRouteId.IntelDiscreteGpu,
            backend: CompatibilityBackend.IntelSycl,
            offload: GpuOffloadLevel.Partial));

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownOffloadSplit),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void WeightConversion_AddsAPersistentArtifactChargedToStorage()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            weights: GgufWeightFormat.Q4KM,
            preparation: CandidatePreparation.WeightConversionRequired));

        ResourceComponent artifact = Single(estimate, ResourceComponentKind.PersistentArtifact);

        Assert.AreEqual(ResourceTarget.Storage, artifact.Target);
        Assert.IsTrue(artifact.Bytes.Bytes > 0);
    }

    [TestMethod]
    public void NoConversion_ChargesNothingToStorage()
    {
        // Running the imported file writes no new artifact, so claiming disk
        // space would be inventing a cost the user does not pay.
        Assert.IsFalse(
            Estimate().Components.Any(
                component => component.Target == ResourceTarget.Storage));
    }

    [TestMethod]
    public void ProvisionalPolicy_RecordsTheUncalibratedLimitation()
    {
        ResourceEstimate estimate = Estimate();

        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.UncalibratedEstimatorPolicy));
    }

    [TestMethod]
    public void EveryEstimate_RecordsTheFileLengthAndSingleSequenceLimitations()
    {
        ResourceEstimate estimate = Estimate();

        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.WeightsDerivedFromFileLength));
        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.SingleSequenceAssumed));
    }

    [TestMethod]
    public void ConvertedWeights_RecordTheScalingLimitation()
    {
        ResourceEstimate estimate = Estimate(Candidate(
            weights: GgufWeightFormat.Q8_0,
            preparation: CandidatePreparation.WeightConversionRequired));

        Assert.IsTrue(
            estimate.Limitations.Contains(EstimationLimitation.WeightsScaledAcrossQuantisation));
    }

    [TestMethod]
    public void ImportedWeights_RecordNoScalingLimitation()
    {
        Assert.IsFalse(
            Estimate().Limitations.Contains(
                EstimationLimitation.WeightsScaledAcrossQuantisation));
    }

    [TestMethod]
    public void AbsentPolicy_IsNotEstablished()
    {
        ResourceEstimate estimate = Estimate(policy: EstimatorPolicy.Absent());

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.EstimatorPolicyUnavailable),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void UnknownArchitecture_IsNotEstablished()
    {
        ResourceEstimate estimate = Estimate(facts: Facts(layers: null));

        Assert.AreEqual(nameof(EstimationStatus.NotEstablished), estimate.Status.ToString());
        Assert.AreEqual(
            nameof(EstimationUnavailableReason.UnknownArchitecture),
            estimate.Reason.ToString());
    }

    [TestMethod]
    public void NotEstablished_CarriesNoComponents()
    {
        // A partial component set would compose into a peak that looks like a
        // real number and would be compared against a real budget.
        Assert.AreEqual(0, Estimate(facts: Facts(layers: null)).Components.Count);
    }

    [TestMethod]
    public void LargerContext_NeverProducesASmallerKvComponent()
    {
        ulong small = Single(
            Estimate(Candidate(context: 2048)), ResourceComponentKind.KvCache).Bytes.Bytes;
        ulong large = Single(
            Estimate(Candidate(context: 8192)), ResourceComponentKind.KvCache).Bytes.Bytes;

        Assert.IsTrue(large > small);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'GgufResourceEstimator' could not be found`.

- [ ] **Step 3: Implement the estimator**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufResourceEstimator.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Decomposes one complete llama.cpp candidate into the component set the phase
/// composer turns into per-pool peaks.
///
/// Every mandatory component has exactly one owner here, so no requirement is
/// counted twice, and any unknown input collapses the whole estimate rather than
/// producing a partial component set that would read as a real number.
/// </summary>
internal static class GgufResourceEstimator
{
    private static readonly IReadOnlySet<LifecyclePhase> AllPhases =
        new HashSet<LifecyclePhase>
        {
            LifecyclePhase.Load,
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        };

    private static readonly IReadOnlySet<LifecyclePhase> GenerationOnly =
        new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration };

    private static readonly IReadOnlySet<LifecyclePhase> CompileAndGeneration =
        new HashSet<LifecyclePhase>
        {
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        };

    private static readonly IReadOnlySet<LifecyclePhase> LoadOnly =
        new HashSet<LifecyclePhase> { LifecyclePhase.Load };

    internal static ResourceEstimate Estimate(
        InspectedModelFacts facts,
        CompatibilityCandidate candidate,
        EstimatorPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.Provenance is PolicyProvenance.Absent or PolicyProvenance.Unspecified)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.EstimatorPolicyUnavailable);
        }

        if (candidate.Configuration is not GgufRouteConfiguration configuration)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.UnsupportedDeviceRoute);
        }

        if (!GgufPoolRouter.TryResolve(
                configuration.Device,
                configuration.Offload,
                out GgufPoolRouting routing,
                out EstimationUnavailableReason routingReason))
        {
            return ResourceEstimate.NotEstablished(routingReason);
        }

        if (!GgufWeightEstimator.TryEstimate(
                facts,
                configuration.Weights,
                policy,
                out ByteCount weightBytes,
                out bool scaledAcrossQuantisation,
                out EstimationUnavailableReason weightReason))
        {
            return ResourceEstimate.NotEstablished(weightReason);
        }

        if (!GgufKvCacheEstimator.TryEstimate(
                facts,
                candidate.Context,
                configuration.KvCache,
                out ByteCount kvBytes,
                out EstimationUnavailableReason kvReason))
        {
            return ResourceEstimate.NotEstablished(kvReason);
        }

        EstimatorTerms terms = policy.Terms;

        try
        {
            List<ResourceComponent> components =
            [
                ResourceComponent.Create(
                    ResourceComponentKind.Weights,
                    routing.ModelTarget,
                    weightBytes,
                    AllPhases),

                ResourceComponent.Create(
                    ResourceComponentKind.KvCache,
                    routing.ModelTarget,
                    kvBytes,
                    GenerationOnly),

                ResourceComponent.Create(
                    ResourceComponentKind.ComputeBuffer,
                    routing.ModelTarget,
                    policy.ComputeBufferFor(candidate.Context),
                    CompileAndGeneration),

                ResourceComponent.Create(
                    ResourceComponentKind.BackendAllocation,
                    routing.ModelTarget,
                    routing.ModelTarget == ResourceTarget.SystemMemory
                        ? terms.CpuBackendAllocation
                        : terms.GpuBackendAllocation,
                    AllPhases),

                // The application itself occupies RAM regardless of where the
                // model runs, and it is charged once.
                ResourceComponent.Create(
                    ResourceComponentKind.ApplicationOverhead,
                    ResourceTarget.SystemMemory,
                    terms.ApplicationOverhead,
                    AllPhases)
            ];

            if (routing.RequiresHostStaging)
            {
                ByteCount scaled = weightBytes.MultiplyByFraction(terms.StagingBufferFraction);

                components.Add(ResourceComponent.Create(
                    ResourceComponentKind.StagingBuffer,
                    ResourceTarget.SystemMemory,
                    scaled > terms.StagingBufferFloor ? scaled : terms.StagingBufferFloor,
                    LoadOnly));
            }

            if (candidate.Preparation == CandidatePreparation.WeightConversionRequired)
            {
                components.Add(ResourceComponent.Create(
                    ResourceComponentKind.PersistentArtifact,
                    ResourceTarget.Storage,
                    weightBytes,
                    LoadOnly));
            }

            HashSet<EstimationLimitation> limitations =
            [
                EstimationLimitation.WeightsDerivedFromFileLength,
                EstimationLimitation.SingleSequenceAssumed
            ];

            if (scaledAcrossQuantisation)
            {
                limitations.Add(EstimationLimitation.WeightsScaledAcrossQuantisation);
            }

            if (policy.Provenance == PolicyProvenance.Provisional)
            {
                limitations.Add(EstimationLimitation.UncalibratedEstimatorPolicy);
            }

            return ResourceEstimate.Established(components, limitations);
        }
        catch (OverflowException)
        {
            return ResourceEstimate.NotEstablished(
                EstimationUnavailableReason.QuantitiesExceedRepresentableRange);
        }
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 18 added in this task.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufResourceEstimator.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufResourceEstimatorTests.cs
git commit -m "feat(compatibility): estimate GGUF candidate resources"
```

---

### Task 10: Invariant suite, exhaustiveness sweeps and the project README

Spec section 15 names five metamorphic properties that must hold regardless of how the estimator is
later tuned, plus determinism under reordering and culture change. Those are the tests that survive
calibration: the exact figures in Tasks 6 to 9 will change when real constants arrive, and these
will not.

There is no property-testing library in this repository, so the sweep uses a small deterministic
generator written here. It is seeded and reproducible forever, which a framework `Random` is not
guaranteed to be across runtimes.

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/DeterministicRandom.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/EstimationCaseGenerator.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/MetamorphicPropertyTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/EnumExhaustivenessTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/CultureInvarianceTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/PrivacyCanaryTests.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/EstimationSpineTests.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/README.md`
- Move: `tests/.../Application/FitPolicyTests.cs` → `tests/.../Application/FitAssessment/FitPolicyTests.cs`
- Move: `tests/.../Application/SafetyPolicyTests.cs` → `tests/.../Application/FitAssessment/SafetyPolicyTests.cs`
- Move: `tests/.../Routes/GgufRouteConfigurationTests.cs` → `tests/.../Routes/Gguf/GgufRouteConfigurationTests.cs`

**Interfaces:**
- Consumes: every type produced by Tasks 1 to 9, plus `ResourcePhaseComposer`, `FitPolicy`,
  `SafetyPolicy` and `AvailableResources` from the completed M2 work.
- Produces: no production type. `DeterministicRandom` and `EstimationCaseGenerator` are test
  helpers.

- [ ] **Step 1: Write the deterministic generator**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/DeterministicRandom.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// A seeded xorshift generator. The framework Random is not contractually stable
/// across runtime versions, and a property failure that cannot be reproduced from
/// its seed is a property test that cannot be debugged.
/// </summary>
internal struct DeterministicRandom
{
    private ulong _state;

    internal DeterministicRandom(ulong seed) =>
        _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    internal ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return unchecked(_state * 0x2545F4914F6CDD1DUL);
    }

    internal int Next(int minInclusive, int maxInclusive) =>
        minInclusive + (int)(NextUInt64() % (ulong)(maxInclusive - minInclusive + 1));

    internal T Pick<T>(IReadOnlyList<T> options) => options[Next(0, options.Count - 1)];
}
```

- [ ] **Step 2: Write the case generator**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/EstimationCaseGenerator.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

internal sealed record EstimationCase(
    ulong Seed,
    InspectedModelFacts Facts,
    GgufRouteConfiguration Configuration,
    ContextTokenCount Context);

/// <summary>
/// Produces structurally valid facts and configurations across the admitted
/// routes. Every case is estimable, so a NotEstablished result during a property
/// sweep is itself a failure rather than an expected outcome.
/// </summary>
internal static class EstimationCaseGenerator
{
    private static readonly int[] HeadDimensions = [64, 80, 96, 128];
    private static readonly int[] AttentionHeadCounts = [8, 16, 32, 40];
    private static readonly int[] GroupDivisors = [1, 2, 4, 8];
    private static readonly int[] LayerCounts = [16, 24, 32, 48, 80];
    private static readonly int[] Contexts = [1024, 2048, 4096, 8192, 16384];

    private static readonly GgufKvCacheFormat[] KvFormats =
        [GgufKvCacheFormat.F16, GgufKvCacheFormat.Q8_0, GgufKvCacheFormat.TurboQuant3Bit];

    private static readonly (DeviceRouteId Device, CompatibilityBackend Backend, GpuOffloadLevel Offload)[] Routes =
    [
        (DeviceRouteId.Cpu, CompatibilityBackend.Cpu, GpuOffloadLevel.None),
        (DeviceRouteId.IntelDiscreteGpu, CompatibilityBackend.IntelSycl, GpuOffloadLevel.Full),
        (DeviceRouteId.IntelIntegratedGpu, CompatibilityBackend.IntelVulkan, GpuOffloadLevel.Full)
    ];

    internal static IEnumerable<EstimationCase> Cases(int count, ulong seed)
    {
        DeterministicRandom random = new(seed);

        for (int index = 0; index < count; index++)
        {
            ulong caseSeed = random.NextUInt64();
            DeterministicRandom local = new(caseSeed);

            int attentionHeads = local.Pick(AttentionHeadCounts);
            int headDimension = local.Pick(HeadDimensions);
            int divisor = local.Pick(GroupDivisors);
            int keyValueHeads = Math.Max(1, attentionHeads / divisor);

            InspectedModelFacts facts = InspectedModelFacts.Create(
                ByteCount.FromBytes((ulong)local.Next(200, 60_000) * 1024 * 1024),
                layerCount: local.Pick(LayerCounts),
                embeddingSize: attentionHeads * headDimension,
                attentionHeadCount: attentionHeads,
                keyValueHeadCount: keyValueHeads,
                declaredContextLimit: 32768,
                fileType: 15,
                quantisationVersion: 2);

            (DeviceRouteId device, CompatibilityBackend backend, GpuOffloadLevel offload) =
                local.Pick(Routes);

            GgufRouteConfiguration configuration = GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                local.Pick(KvFormats),
                backend,
                device,
                offload);

            yield return new EstimationCase(
                caseSeed,
                facts,
                configuration,
                ContextTokenCount.FromTokens(local.Pick(Contexts)));
        }
    }

    internal static CompatibilityCandidate Candidate(
        EstimationCase generated,
        GgufKvCacheFormat? kvOverride = null,
        ContextTokenCount? contextOverride = null)
    {
        GgufRouteConfiguration configuration = kvOverride is { } kv
            ? GgufRouteConfiguration.Create(
                generated.Configuration.Weights,
                kv,
                generated.Configuration.Backend,
                generated.Configuration.Device,
                generated.Configuration.Offload)
            : generated.Configuration;

        return CompatibilityCandidate.Create(
            configuration,
            contextOverride ?? generated.Context,
            CandidatePreparation.None,
            supportEntryId: "entry-generated",
            isExperimental: false,
            isBaseline: true);
    }
}
```

- [ ] **Step 3: Write the metamorphic property tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/MetamorphicPropertyTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties that must hold whatever the estimator constants become. The exact
/// figures asserted elsewhere will change when calibration data arrives; these
/// will not.
/// </summary>
[TestClass]
public sealed class MetamorphicPropertyTests
{
    private const int CaseCount = 250;
    private const ulong Seed = 0xC1C0_11A7_2026_0820;

    private static readonly EstimatorPolicy Policy = EstimatorPolicy.ProvisionalV1();

    private static ResourceEstimate EstimateFor(
        EstimationCase generated,
        GgufKvCacheFormat? kvOverride = null,
        ContextTokenCount? contextOverride = null)
    {
        CompatibilityCandidate candidate =
            EstimationCaseGenerator.Candidate(generated, kvOverride, contextOverride);

        ResourceEstimate estimate =
            GgufResourceEstimator.Estimate(generated.Facts, candidate, Policy);

        Assert.AreEqual(
            nameof(EstimationStatus.Established),
            estimate.Status.ToString(),
            $"Generated case {generated.Seed:x16} should be estimable but gave {estimate.Reason}.");

        return estimate;
    }

    private static ulong BytesOf(ResourceEstimate estimate, ResourceComponentKind kind) =>
        estimate.Components.Single(component => component.Kind == kind).Bytes.Bytes;

    [TestMethod]
    public void ALargerContextCanNeverReduceTheKvPayload()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ulong smaller = BytesOf(
                EstimateFor(generated, contextOverride: ContextTokenCount.FromTokens(1024)),
                ResourceComponentKind.KvCache);

            ulong larger = BytesOf(
                EstimateFor(generated, contextOverride: ContextTokenCount.FromTokens(8192)),
                ResourceComponentKind.KvCache);

            Assert.IsTrue(
                larger >= smaller,
                $"Case {generated.Seed:x16}: 8192 tokens produced a smaller cache than 1024.");
        }
    }

    [TestMethod]
    public void AddingALiveComponentCanNeverReduceItsPhaseRequirement()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ResourceEstimate estimate = EstimateFor(generated);

            ResourcePeakProfile before = ResourcePhaseComposer.Compose(estimate.Components);

            List<ResourceComponent> widened =
            [
                .. estimate.Components,
                ResourceComponent.Create(
                    ResourceComponentKind.ModelState,
                    ResourceTarget.SystemMemory,
                    ByteCount.FromBytes(1024),
                    new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration })
            ];

            ResourcePeakProfile after = ResourcePhaseComposer.Compose(widened);

            Assert.IsTrue(
                after.PeakFor(ResourceTarget.SystemMemory)
                    >= before.PeakFor(ResourceTarget.SystemMemory),
                $"Case {generated.Seed:x16}: adding a live component lowered the requirement.");
        }
    }

    [TestMethod]
    public void ChangingTheKvFormatCanNeverAlterTheWeightPayload()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ulong withF16 = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.F16),
                ResourceComponentKind.Weights);

            ulong withTurboQuant = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.TurboQuant3Bit),
                ResourceComponentKind.Weights);

            Assert.AreEqual(
                withF16,
                withTurboQuant,
                $"Case {generated.Seed:x16}: the KV format leaked into the weight estimate.");
        }
    }

    [TestMethod]
    public void SharedDeviceMemoryCanNeverIncreaseTotalSystemCapacity()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ResourceEstimate estimate = EstimateFor(generated);
            ResourcePeakProfile profile = ResourcePhaseComposer.Compose(estimate.Components);

            // Shared memory is carved out of RAM. It must add to the pressure on
            // RAM and must never appear as a separate pool that absorbs demand.
            Assert.AreEqual(
                profile.PeakFor(ResourceTarget.SystemMemory).Bytes
                    + profile.PeakFor(ResourceTarget.SharedDeviceMemory).Bytes,
                profile.SystemMemoryPressure.Bytes,
                $"Case {generated.Seed:x16}: shared memory was not charged to RAM exactly once.");

            Assert.IsTrue(
                profile.SystemMemoryPressure >= profile.PeakFor(ResourceTarget.SystemMemory),
                $"Case {generated.Seed:x16}: shared memory reduced system pressure.");
        }
    }

    [TestMethod]
    public void AQuantisedCacheCanNeverCostMoreThanAnUnquantisedOne()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ulong f16 = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.F16),
                ResourceComponentKind.KvCache);

            ulong quantised = BytesOf(
                EstimateFor(generated, kvOverride: GgufKvCacheFormat.Q8_0),
                ResourceComponentKind.KvCache);

            Assert.IsTrue(
                quantised <= f16,
                $"Case {generated.Seed:x16}: Q8_0 cost more than F16, so the block table is wrong.");
        }
    }

    [TestMethod]
    public void ComponentOrderCanNeverChangeTheComposedPeak()
    {
        foreach (EstimationCase generated in EstimationCaseGenerator.Cases(CaseCount, Seed))
        {
            ResourceEstimate estimate = EstimateFor(generated);

            ResourcePeakProfile forward = ResourcePhaseComposer.Compose(estimate.Components);
            ResourcePeakProfile reversed = ResourcePhaseComposer.Compose(
                [.. estimate.Components.Reverse()]);

            Assert.AreEqual(
                forward.SystemMemoryPressure.Bytes,
                reversed.SystemMemoryPressure.Bytes,
                $"Case {generated.Seed:x16}: composition depended on component order.");
        }
    }
}
```

- [ ] **Step 4: Run the property tests and verify they pass**

Run the suite command.

Expected: PASS. If any property fails, the assert message names the case seed; reproduce it by
constructing a one-case generator with that seed.

- [ ] **Step 5: Write the exhaustiveness sweeps**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/EnumExhaustivenessTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Sweeps that fail when a new enum member is added without a corresponding
/// rule. Without these, a new weight format or cache format would fall through a
/// switch expression and be sized as free - a silent false-safe result.
/// </summary>
[TestClass]
public sealed class EnumExhaustivenessTests
{
    [TestMethod]
    public void EveryKvCacheFormat_HasARecordedBlockEncoding()
    {
        foreach (GgufKvCacheFormat format in Enum.GetValues<GgufKvCacheFormat>())
        {
            if (format == GgufKvCacheFormat.Unspecified)
            {
                continue;
            }

            Assert.IsTrue(
                GgufKvCacheBlockSpec.TryFor(format, out GgufKvCacheBlockSpec spec),
                $"{format} has no recorded block encoding, so it would be sized as free.");

            Assert.IsTrue(spec.ValuesPerBlock > 0 && spec.BytesPerBlock > 0);
        }
    }

    [TestMethod]
    public void EveryWeightFormat_MapsToAnEncodingWithABitWidth()
    {
        foreach (GgufWeightFormat format in Enum.GetValues<GgufWeightFormat>())
        {
            if (format is GgufWeightFormat.Unspecified or GgufWeightFormat.Imported)
            {
                continue;
            }

            WeightQuantisation quantisation = WeightQuantisationMap.FromWeightFormat(format);

            Assert.AreNotEqual(
                WeightQuantisation.Unknown,
                quantisation,
                $"{format} has no canonical encoding.");

            Assert.IsTrue(WeightQuantisationMap.BitsPerWeight(quantisation) > 0m);
        }
    }

    [TestMethod]
    public void EveryCanonicalQuantisation_HasABitWidth()
    {
        foreach (WeightQuantisation quantisation in Enum.GetValues<WeightQuantisation>())
        {
            if (quantisation == WeightQuantisation.Unknown)
            {
                continue;
            }

            Assert.IsTrue(
                WeightQuantisationMap.BitsPerWeight(quantisation) > 0m,
                $"{quantisation} has no recorded bit width.");
        }
    }

    [TestMethod]
    public void EveryLimitation_IsAcceptedByAnEstablishedEstimate()
    {
        foreach (EstimationLimitation limitation in Enum.GetValues<EstimationLimitation>())
        {
            if (limitation == EstimationLimitation.Unspecified)
            {
                continue;
            }

            ResourceEstimate estimate = ResourceEstimate.Established(
                [
                    ResourceComponent.Create(
                        ResourceComponentKind.Weights,
                        ResourceTarget.SystemMemory,
                        ByteCount.FromBytes(1024),
                        new HashSet<LifecyclePhase> { LifecyclePhase.Load })
                ],
                new HashSet<EstimationLimitation> { limitation });

            Assert.IsTrue(estimate.Limitations.Contains(limitation));
        }
    }

    [TestMethod]
    public void EveryUnavailableReason_IsDistinctlyNamed()
    {
        // Stable codes carry the whole explanation to the user, so a duplicated
        // value would collapse two different failures into one message.
        EstimationUnavailableReason[] values = Enum.GetValues<EstimationUnavailableReason>();

        Assert.AreEqual(values.Length, values.Distinct().Count());
    }
}
```

- [ ] **Step 6: Write the culture and privacy canaries**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/CultureInvarianceTests.cs`:

```csharp
using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

[TestClass]
public sealed class CultureInvarianceTests
{
    private static readonly string[] Cultures = ["", "tr-TR", "de-DE"];

    private static (ulong Weights, ulong Kv, string Fingerprint) MeasureUnder(string culture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = culture.Length == 0
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);

            InspectedModelFacts facts = InspectedModelFacts.Create(
                ByteCount.FromBytes(4_000_000_000),
                layerCount: 32,
                embeddingSize: 4096,
                attentionHeadCount: 32,
                keyValueHeadCount: 8,
                declaredContextLimit: 32768,
                fileType: 15,
                quantisationVersion: 2);

            CompatibilityCandidate candidate = CompatibilityCandidate.Create(
                GgufRouteConfiguration.Create(
                    GgufWeightFormat.Q4KM,
                    GgufKvCacheFormat.Q8_0,
                    CompatibilityBackend.IntelSycl,
                    DeviceRouteId.IntelDiscreteGpu,
                    GpuOffloadLevel.Full),
                ContextTokenCount.FromTokens(8192),
                CandidatePreparation.RuntimeProfileOnly,
                supportEntryId: "entry-1",
                isExperimental: false,
                isBaseline: false);

            ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                facts, candidate, EstimatorPolicy.ProvisionalV1());

            return (
                estimate.Components
                    .Single(component => component.Kind == ResourceComponentKind.Weights)
                    .Bytes.Bytes,
                estimate.Components
                    .Single(component => component.Kind == ResourceComponentKind.KvCache)
                    .Bytes.Bytes,
                candidate.Fingerprint.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void EstimatesAndFingerprints_AreIdenticalUnderEveryCulture()
    {
        // Turkish lowercases I differently and German uses a comma as the decimal
        // separator. Either could change a fingerprint or a parsed constant if
        // any formatting slipped into the calculation path.
        (ulong Weights, ulong Kv, string Fingerprint) baseline = MeasureUnder(Cultures[0]);

        foreach (string culture in Cultures)
        {
            Assert.AreEqual(baseline, MeasureUnder(culture), $"Culture {culture} changed a result.");
        }
    }
}
```

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/PrivacyCanaryTests.cs`:

```csharp
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Section 14 forbids any path, filename, model name, hostname, device
/// identifier or raw tool output entering a C1 type. The cheapest durable
/// enforcement is to know every string member in the library by name.
/// </summary>
[TestClass]
public sealed class PrivacyCanaryTests
{
    private static readonly HashSet<string> AllowedStringMembers =
    [
        "SafetyPolicy.PolicyVersion",
        "EstimatorPolicy.PolicyVersion",
        "CompatibilityCandidate.SupportEntryId",
        "RouteConfiguration.CanonicalDescriptor",
        "GgufRouteConfiguration.CanonicalDescriptor",
        "CandidateFingerprint.Value"
    ];

    [TestMethod]
    public void NoUnreviewedStringMemberExistsInTheCompatibilityCore()
    {
        // A new string member is where a path or model name would first appear.
        // Adding one is allowed; adding one without review is not.
        string[] found = typeof(ByteCount).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(string))
                .Select(property => $"{type.Name}.{property.Name}"))
            .Where(name => !AllowedStringMembers.Contains(name))
            .Where(name => !name.Contains("EqualityContract", StringComparison.Ordinal))
            .Order()
            .ToArray();

        Assert.AreEqual(
            0,
            found.Length,
            "Unreviewed string members found. Confirm each carries no path, "
            + "filename, model name or native error, then add it to the allowlist: "
            + string.Join(", ", found));
    }

    [TestMethod]
    public void EveryAllowedStringValue_IsFreeOfPathLikeContent()
    {
        string[] forbidden = ["\\", "/", ":", ".gguf", ".."];

        string[] samples =
        [
            Core.Application.FitAssessment.SafetyPolicy.ProvisionalV1().PolicyVersion,
            Core.Application.Estimation.EstimatorPolicy.ProvisionalV1().PolicyVersion
        ];

        foreach (string sample in samples)
        {
            foreach (string fragment in forbidden)
            {
                Assert.IsFalse(
                    sample.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                    $"'{sample}' contains path-like content '{fragment}'.");
            }
        }
    }
}
```

> If the `Core.Application...` qualification above does not resolve, add
> `using Core = GraniteEdgeAI.ModelHardwareCompatibility.Core;` at the top of the file rather than
> widening the existing usings, which would shadow the `Domain` import.

- [ ] **Step 7: Write the end-to-end spine test**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/EstimationSpineTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// The whole computational spine: model facts to components to per-pool peaks to
/// a fit verdict. Each stage is unit-tested on its own; this proves they compose.
/// </summary>
[TestClass]
public sealed class EstimationSpineTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private static InspectedModelFacts Facts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4 * Gibibyte),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

    private static CompatibilityCandidate Candidate() =>
        CompatibilityCandidate.Create(
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

    private static FitAssessment AssessWith(ulong availableGibibytes, SafetyPolicy? policy = null)
    {
        ResourceEstimate estimate = GgufResourceEstimator.Estimate(
            Facts(), Candidate(), EstimatorPolicy.ProvisionalV1());

        Assert.AreEqual(nameof(EstimationStatus.Established), estimate.Status.ToString());

        return FitPolicy.Assess(
            ResourcePhaseComposer.Compose(estimate.Components),
            AvailableResources.Create(
                ByteCount.FromBytes(availableGibibytes * Gibibyte),
                dedicatedDeviceMemory: ByteCount.Zero,
                storage: ByteCount.FromBytes(200 * Gibibyte),
                observedAtUtc: DateTimeOffset.UtcNow),
            policy ?? SafetyPolicy.ProvisionalV1());
    }

    [TestMethod]
    public void AGenerousMachine_ReachesASafeVerdict()
    {
        Assert.AreEqual(
            nameof(CompatibilityFitState.Safe),
            AssessWith(availableGibibytes: 48).State.ToString());
    }

    [TestMethod]
    public void AConstrainedMachine_ReachesADoesNotFitVerdict()
    {
        FitAssessment assessment = AssessWith(availableGibibytes: 6);

        Assert.AreEqual(
            nameof(CompatibilityFitState.DoesNotFit),
            assessment.State.ToString());
        Assert.AreEqual(
            nameof(FitLimitingReason.InsufficientSystemMemory),
            assessment.LimitingReason.ToString());
    }

    [TestMethod]
    public void AnAbsentSafetyPolicy_ReachesNotEstablishedRatherThanAGuess()
    {
        FitAssessment assessment = AssessWith(availableGibibytes: 48, SafetyPolicy.Absent());

        Assert.AreEqual(
            nameof(CompatibilityFitState.NotEstablished),
            assessment.State.ToString());
        Assert.AreEqual(
            nameof(FitLimitingReason.SafetyPolicyUnavailable),
            assessment.LimitingReason.ToString());
    }

    [TestMethod]
    public void TheRequirementAlwaysExceedsTheRawPeak()
    {
        // The calibration margin is added to the requirement and never subtracted
        // from it, so an uncalibrated estimator errs toward refusing to run.
        ResourceEstimate estimate = GgufResourceEstimator.Estimate(
            Facts(), Candidate(), EstimatorPolicy.ProvisionalV1());

        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(estimate.Components);
        FitAssessment assessment = AssessWith(availableGibibytes: 48);

        Assert.IsTrue(
            assessment.RequiredBytes > profile.SystemMemoryPressure,
            "The safety margin must be inside the required figure.");
    }
}
```

- [ ] **Step 8: Run the whole suite**

Run the suite command.

Expected: PASS, everything green. Note the total; it is the count Task 10 finishes on.

- [ ] **Step 9: Align the two shallow test files with the mirrored tree**

```bash
mkdir -p tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/FitAssessment
mkdir -p tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf
git mv tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/FitPolicyTests.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/FitAssessment/FitPolicyTests.cs
git mv tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/SafetyPolicyTests.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/FitAssessment/SafetyPolicyTests.cs
git mv tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/GgufRouteConfigurationTests.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/Gguf/GgufRouteConfigurationTests.cs
```

Then update the namespace declaration in each moved file so it matches its new folder:

- `FitPolicyTests.cs` and `SafetyPolicyTests.cs`:
  `namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.FitAssessment;`
- `GgufRouteConfigurationTests.cs`:
  `namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes.Gguf;`

`FitPolicyTests.cs` and `SafetyPolicyTests.cs` already carry
`using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;`. Once their own
namespace ends in `FitAssessment`, that using becomes redundant but stays valid; leave it in place
so the diff is a pure move plus one line.

- [ ] **Step 10: Run the suite again and confirm the move changed nothing**

Run the suite command.

Expected: PASS with exactly the same total as Step 8. A different total means a file was dropped
from the build.

- [ ] **Step 11: Write the project README**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/README.md`, following the shape of
`shared/GraniteEdgeAI.ModelInspection.Contracts/README.md`:

````markdown
# Model/hardware compatibility core

**Status:** Byte arithmetic, planning context, phase composition, safety policy, fit
classification, candidate vocabulary and the GGUF resource estimator implemented and tested.
Support matrix, candidate generation, mode selection, orchestration, screens and owner adapters
remain later gates.
**Last reviewed:** 2026-08-20

## Purpose

This pure `net8.0` project answers one question: given what we know about a model and what we know
about this computer, which configurations could run safely, and how much memory would each need?

```text
Model Inspection handoff  ─┐
                           ├─→  GraniteEdgeAI.ModelHardwareCompatibility.Core  ─→  selected plan
Hardware Inspection handoff┘
```

It selects a plan. It does not collect hardware, parse models, run converters, run inference or
optimise artifacts.

## Owned responsibilities

- checked byte arithmetic with typed unknowns;
- planning-context resolution;
- per-component resource estimation and per-pool peak composition;
- versioned safety and estimator policies with explicit provenance;
- safe-budget comparison and fit classification;
- complete candidate configurations with a deterministic fingerprint.

## Forbidden responsibilities

- WinUI, XAML, pages, ViewModels or navigation;
- any reference to Hardware Inspection or Model Inspection production types;
- Windows-only APIs, memory collection or device enumeration;
- any path, filename, model name, hostname, credential or raw tool output.

## Why the numbers are conservative

A false-safe answer crashes the user's machine; a false-unsafe answer is an inconvenience. So every
margin is added to the requirement and never subtracted, equality counts as fitting only after all
mandatory margins are included, and any unknown input collapses a candidate to `NotEstablished`
rather than defaulting to zero.

Both policies currently ship `Provisional` provenance: their values are documented defaults, not
measurements. Every estimate built on them records the limitation, and moving to `Calibrated` is a
data change rather than a code change.
````

- [ ] **Step 12: Commit**

```bash
git add tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/ shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/README.md
git commit -m "test(compatibility): add estimator invariant and exhaustiveness suite"
```

---

## Self-Review

**Spec coverage.** Section 8 arithmetic and alignment → Task 1. Section 8 canonical quantisation →
Task 2. Section 3 C1-owned calculation records → Task 3. Section 8 unknown-as-typed-absence →
Task 4. Section 9 provenance for estimator constants → Task 5. Section 8 KV formula and real block
encoding → Task 6. Section 8 weight memory and its recorded limitation → Task 7. Section 8 resource
targets and the shared-memory rule → Task 8. Section 8 peak composition ownership → Task 9.
Section 15 metamorphic properties, determinism and privacy canaries → Task 10.

Deliberately out of scope, with the reason: section 9's per-target budgets (`FitPolicy` gates system
memory only until a candidate's device route drives a decision, which is mode-selection work);
section 10's support matrix and generator (M3c); section 11's modes (M3c); sections 12 and 13
(M5); section 5's ports (M4 prerequisite, not needed by a pure estimator).

**Placeholder scan.** No TBD, no "add validation", no "similar to Task N". Every code step carries
complete code. The one forward reference — `UnsupportedWeightFormat` used in Task 7's tests — is
resolved inside Task 7 Step 2 rather than left dangling.

**Type consistency.** `ByteCount.AlignUpTo` and `MultiplyByFraction` (Task 1) are used in Tasks 5,
7 and 9. `WeightQuantisationMap.FromGgufFileType` / `FromWeightFormat` / `BitsPerWeight` (Task 2)
are used only in Task 7 and Task 10's sweeps. `InspectedModelFacts` property names (Task 3) match
every later reference. `ResourceEstimate.Established` / `NotEstablished` (Task 4) match Tasks 6 to
10. `EstimatorPolicy.Terms` and `ComputeBufferFor` (Task 5) match Tasks 7 and 9.
`GgufKvCacheBlockSpec.TryFor` is used by both the estimator (Task 6) and the exhaustiveness sweep
(Task 10), which is why it exists alongside the throwing `For`. `GgufPoolRouter.TryResolve` (Task 8)
returns the `GgufPoolRouting` shape Task 9 destructures.

**Arithmetic checked by hand.** The KV figures asserted in Task 6 derive from headDim 128, 8 KV
heads, 4096 tokens and 32 layers: 4,194,304 values per tensor per layer, giving 536,870,912 bytes
at F16, 285,212,672 at Q8_0 and 117,440,512 at TurboQuant3Bit. The weight figure in Task 7 derives
from 4,000,000,000 plus 3% overhead aligned up to 4096, giving 4,120,002,560.

**Known follow-up carried forward.** `FitPolicy` still assesses system memory only. Task 9 emits
dedicated-device and storage components that nothing gates yet; the spine test in Task 10 therefore
asserts a system-memory verdict, not a device or storage one.
