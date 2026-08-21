# Model/Hardware Compatibility Support Matrix and Candidate Generation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce the versioned support matrix and the generator that walks it, so the estimator finally has candidates to evaluate.

**Architecture:** A support entry is one complete, human-authored route shape — runtime, backend, device, offload, weight format, KV format — plus its context bounds, support level, admission level and experimental flag. The matrix is a versioned asset carrying `PolicyProvenance`, exactly like the safety and estimator policies. The generator emits one candidate per admitted entry per admitted context rung, baseline first, deduplicated by the existing fingerprint. It is not a Cartesian product over enums: every combination that exists was written down by a person.

**Tech Stack:** C# 12, .NET 8 (`net8.0`), MSTest 4.3.2 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-design.md`, section 10.

**Predecessors (all complete):** core foundation (M1+M2), candidate generation vocabulary (M3a), GGUF resource estimator (M3b). Suite is **272 tests green** at the start of this plan.

Suite command, referred to below as **the suite command**:

```bash
dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj
```

**Scope:** the matrix, its resolver, and the generator. Mode selection is the next plan; it ranks candidates this plan produces.

## Global Constraints

- Target `net8.0`, `Nullable` enabled, `TreatWarningsAsErrors` true. No WinUI, no Windows-only API.
- Types are `internal`; the test project already has `InternalsVisibleTo`.
- Every enum reserves a zero member and rejects it wherever it would be meaningless.
- **Unknown is a typed unavailable value, never zero, never a default.**
- **Domain/ must not reference Routes/.** It is Routes-free and stays that way.
- No absolute path, filename, model name, hostname, credential or raw tool output in any type, message or test.
- MSTest 4: `[TestMethod]` with `[DataRow]`; `DataTestMethod` is deprecated and fails the build.
- A public test method cannot take an `internal` enum parameter (CS0051) — pass `nameof(...)` strings and `Enum.Parse` in the body.
- Bias is one-directional: an unknown or ambiguous support state resolves to `Unsupported`, never to available.

## Design decisions taken in this plan

**An entry is one complete configuration shape, not a set of options.** Spec section 10 says generation is not a Cartesian product and that each candidate starts from exactly one admitted entry. The most literal reading — and the most conservative — is that a person writes down each admitted combination explicitly. An entry therefore names one weight format and one KV format, and the only axis the generator expands is the context ladder. This keeps the matrix auditable and makes an unintended combination impossible to generate.

**Conversion needs a declared trusted source.** Spec section 10 forbids generating an upward conversion as an upgrade and forbids requantising an already-quantised GGUF by default without a suitable higher-precision source. The generator therefore takes an explicit `TrustedSourceAvailability` input. With no trusted source, only entries whose weight format is `Imported` or matches the imported file's canonical encoding can produce candidates.

## File Structure

```
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/
├── Domain/
│   ├── SupportLevel.cs                     Task 1
│   ├── InstallationState.cs                Task 1
│   └── SupportAvailability.cs              Task 1
└── Application/
    ├── Capabilities/
    │   ├── CompatibilitySupportEntry.cs    Task 2
    │   ├── SupportMatrix.cs                Task 3
    │   └── SupportMatrixResolver.cs        Task 3
    └── Candidates/
        ├── TrustedSourceAvailability.cs    Task 4
        ├── CandidateGenerationRequest.cs   Task 4
        └── CandidateGenerator.cs           Task 4

tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/
├── Application/Capabilities/
│   ├── CompatibilitySupportEntryTests.cs   Task 2
│   └── SupportMatrixResolverTests.cs       Task 3
├── Application/Candidates/
│   └── CandidateGeneratorTests.cs          Task 4
└── Invariants/
    └── GenerationInvariantTests.cs         Task 5
```

---

### Task 1: Support vocabulary

Three enums naming what a matrix entry declares and what the environment reports. Kept in `Domain/` because they carry no route knowledge.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/SupportLevel.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/InstallationState.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/SupportAvailability.cs`
- Test: none of their own — Task 3's resolver tests exercise every member.

**Interfaces:**
- Consumes: nothing.
- Produces: `SupportLevel { Unknown = 0, DeclaredSupported, Experimental }`; `InstallationState { Unknown = 0, NotInstalled, InstalledAndVerified, VerifiedAndOptedIn }`; `SupportAvailability { Unsupported = 0, Unavailable, Available, ExperimentalAvailable }`. Consumed by Tasks 2, 3 and 4.

- [ ] **Step 1: Create the three enums**

Create `Domain/SupportLevel.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// What the support matrix claims about a configuration. Anything the matrix
/// does not positively declare is Unknown, which resolves to unsupported: the
/// absence of a claim is never evidence that something works.
/// </summary>
internal enum SupportLevel
{
    Unknown = 0,
    DeclaredSupported,
    Experimental
}
```

Create `Domain/InstallationState.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// What the machine reports about a declared configuration. Verified means a
/// check ran and passed, not that a file was found on disk.
/// </summary>
internal enum InstallationState
{
    Unknown = 0,
    NotInstalled,
    InstalledAndVerified,

    /// <summary>
    /// Installed, verified, and the user has explicitly opted in to an
    /// experimental route. Opt-in is required because experimental routes may
    /// produce wrong output rather than merely failing.
    /// </summary>
    VerifiedAndOptedIn
}
```

Create `Domain/SupportAvailability.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// The resolved answer to "may this configuration be offered?". Unsupported is
/// the zero value so that a value which was never resolved cannot read as
/// available.
/// </summary>
internal enum SupportAvailability
{
    Unsupported = 0,
    Unavailable,
    Available,
    ExperimentalAvailable
}
```

- [ ] **Step 2: Build and confirm nothing broke**

Run the suite command.

Expected: PASS, still 272 — three new enums with no consumers change no behaviour.

- [ ] **Step 3: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/SupportLevel.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/InstallationState.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/SupportAvailability.cs
git commit -m "feat(compatibility): add support matrix vocabulary"
```

---

### Task 2: The support entry

One complete, human-authored route shape. Every field is required; there is no partially specified entry, for the same reason there is no partially specified candidate.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/CompatibilitySupportEntry.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/CompatibilitySupportEntryTests.cs`

**Interfaces:**
- Consumes: `SupportLevel` (Task 1); `RuntimeRouteId`, `CompatibilityBackend`, `DeviceRouteId` from `Core.Domain`; `GgufWeightFormat`, `GgufKvCacheFormat`, `GpuOffloadLevel` from `Core.Routes.Gguf`.
- Produces: `CompatibilitySupportEntry.Create(string entryId, RuntimeRouteId route, CompatibilityBackend backend, DeviceRouteId device, GpuOffloadLevel offload, GgufWeightFormat weights, GgufKvCacheFormat kvCache, int minimumContextTokens, int maximumContextTokens, SupportLevel level, bool requiresEvidence)` plus get-only properties of the same names in Pascal case. Consumed by Tasks 3, 4 and 5.

> **Route note.** This entry is GGUF-shaped because llama.cpp is the only route that exists. When OpenVINO arrives it gets its own entry type rather than optional fields on this one, matching how `RouteConfiguration` is closed per route.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/CompatibilitySupportEntryTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Capabilities;

[TestClass]
public sealed class CompatibilitySupportEntryTests
{
    private static CompatibilitySupportEntry Create(
        string entryId = "gguf-cpu-imported-f16",
        int minimumContext = 1024,
        int maximumContext = 32768,
        SupportLevel level = SupportLevel.DeclaredSupported) =>
        CompatibilitySupportEntry.Create(
            entryId,
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None,
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            minimumContext,
            maximumContext,
            level,
            requiresEvidence: false);

    [TestMethod]
    public void Create_PreservesEveryField()
    {
        CompatibilitySupportEntry entry = Create();

        Assert.AreEqual("gguf-cpu-imported-f16", entry.EntryId);
        Assert.AreEqual(nameof(RuntimeRouteId.LlamaCpp), entry.Route.ToString());
        Assert.AreEqual(nameof(CompatibilityBackend.Cpu), entry.Backend.ToString());
        Assert.AreEqual(nameof(DeviceRouteId.Cpu), entry.Device.ToString());
        Assert.AreEqual(nameof(GpuOffloadLevel.None), entry.Offload.ToString());
        Assert.AreEqual(nameof(GgufWeightFormat.Imported), entry.Weights.ToString());
        Assert.AreEqual(nameof(GgufKvCacheFormat.F16), entry.KvCache.ToString());
        Assert.AreEqual(1024, entry.MinimumContextTokens);
        Assert.AreEqual(32768, entry.MaximumContextTokens);
        Assert.AreEqual(nameof(SupportLevel.DeclaredSupported), entry.Level.ToString());
        Assert.IsFalse(entry.RequiresEvidence);
    }

    [TestMethod]
    public void Create_RejectsABlankEntryId()
    {
        // A candidate names the entry it came from, so an unnamed entry would
        // produce a candidate whose provenance cannot be audited.
        Assert.ThrowsExactly<ArgumentException>(() => Create(entryId: "   "));
    }

    [TestMethod]
    public void Create_RejectsAnUnknownSupportLevel()
    {
        // The matrix must state a claim. Absence of a claim is handled by the
        // entry not existing, not by an entry that declares nothing.
        Assert.ThrowsExactly<ArgumentException>(() => Create(level: SupportLevel.Unknown));
    }

    [TestMethod]
    [DataRow(0, 32768)]
    [DataRow(-1, 32768)]
    [DataRow(1024, 0)]
    [DataRow(1024, -1)]
    public void Create_RejectsNonPositiveContextBounds(int minimum, int maximum)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Create(minimumContext: minimum, maximumContext: maximum));
    }

    [TestMethod]
    public void Create_RejectsAnInvertedContextRange()
    {
        // A maximum below the minimum admits nothing, so the entry could never
        // produce a candidate and would silently vanish from the matrix.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => Create(minimumContext: 8192, maximumContext: 4096));
    }

    [TestMethod]
    public void Create_AllowsAMinimumEqualToTheMaximum()
    {
        CompatibilitySupportEntry entry = Create(minimumContext: 4096, maximumContext: 4096);

        Assert.AreEqual(4096, entry.MinimumContextTokens);
        Assert.AreEqual(4096, entry.MaximumContextTokens);
    }

    [TestMethod]
    public void Create_RejectsARouteAndBackendMismatch()
    {
        // An OpenVINO backend on a llama.cpp entry would let a candidate be
        // generated that no evaluator can estimate.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilitySupportEntry.Create(
                "mismatched",
                RuntimeRouteId.LlamaCpp,
                CompatibilityBackend.OpenVinoGpu,
                DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                1024,
                32768,
                SupportLevel.DeclaredSupported,
                requiresEvidence: false));
    }

    [TestMethod]
    public void Create_RejectsAConfigurationTheRouteWouldNotAccept()
    {
        // A CPU device cannot offload to a GPU. Rejecting here means an
        // unbuildable entry cannot sit in the matrix waiting to fail later.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilitySupportEntry.Create(
                "cpu-with-offload",
                RuntimeRouteId.LlamaCpp,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.Full,
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                1024,
                32768,
                SupportLevel.DeclaredSupported,
                requiresEvidence: false));
    }

    [TestMethod]
    public void ToRouteConfiguration_ProducesTheEntrysExactShape()
    {
        GgufRouteConfiguration configuration = Create().ToRouteConfiguration();

        Assert.AreEqual(nameof(GgufWeightFormat.Imported), configuration.Weights.ToString());
        Assert.AreEqual(nameof(GgufKvCacheFormat.F16), configuration.KvCache.ToString());
        Assert.AreEqual(nameof(DeviceRouteId.Cpu), configuration.Device.ToString());
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'CompatibilitySupportEntry' could not be found`.

- [ ] **Step 3: Implement the entry**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/CompatibilitySupportEntry.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// One admitted configuration shape, written down by a person.
///
/// The matrix is deliberately explicit rather than generative: every
/// combination that can be offered to a user exists because someone declared
/// it, so an unintended pairing of backend, device and format cannot appear by
/// accident. The only axis the generator expands is context length.
/// </summary>
internal sealed record CompatibilitySupportEntry
{
    private CompatibilitySupportEntry(
        string entryId,
        RuntimeRouteId route,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        EntryId = entryId;
        Route = route;
        Backend = backend;
        Device = device;
        Offload = offload;
        Weights = weights;
        KvCache = kvCache;
        MinimumContextTokens = minimumContextTokens;
        MaximumContextTokens = maximumContextTokens;
        Level = level;
        RequiresEvidence = requiresEvidence;
    }

    /// <summary>Stable identifier a generated candidate carries as provenance.</summary>
    internal string EntryId { get; }

    internal RuntimeRouteId Route { get; }

    internal CompatibilityBackend Backend { get; }

    internal DeviceRouteId Device { get; }

    internal GpuOffloadLevel Offload { get; }

    internal GgufWeightFormat Weights { get; }

    internal GgufKvCacheFormat KvCache { get; }

    internal int MinimumContextTokens { get; }

    internal int MaximumContextTokens { get; }

    internal SupportLevel Level { get; }

    /// <summary>
    /// True when the entry declares a quality, performance or stability
    /// threshold that measured evidence must satisfy before a candidate from it
    /// may be offered.
    /// </summary>
    internal bool RequiresEvidence { get; }

    internal static CompatibilitySupportEntry Create(
        string entryId,
        RuntimeRouteId route,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        int minimumContextTokens,
        int maximumContextTokens,
        SupportLevel level,
        bool requiresEvidence)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException(
                "An entry must be named so a candidate can carry its provenance.",
                nameof(entryId));
        }

        if (level == SupportLevel.Unknown)
        {
            throw new ArgumentException(
                "An entry must state a support claim. The absence of a claim is "
                + "expressed by the entry not existing.",
                nameof(level));
        }

        if (route != RuntimeRouteId.LlamaCpp)
        {
            throw new ArgumentException(
                "Only the llama.cpp route has an entry shape today; OpenVINO gets "
                + "its own entry type rather than optional fields on this one.",
                nameof(route));
        }

        if (minimumContextTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumContextTokens),
                "A minimum context must be a positive number of tokens.");
        }

        if (maximumContextTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumContextTokens),
                "A maximum context must be a positive number of tokens.");
        }

        if (maximumContextTokens < minimumContextTokens)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumContextTokens),
                "A maximum below the minimum admits nothing, so the entry could "
                + "never produce a candidate.");
        }

        // Building the configuration now proves the shape is one the route will
        // accept. An entry that cannot be turned into a configuration would sit
        // in the matrix and fail only when a user selected it.
        _ = GgufRouteConfiguration.Create(weights, kvCache, backend, device, offload);

        return new CompatibilitySupportEntry(
            entryId,
            route,
            backend,
            device,
            offload,
            weights,
            kvCache,
            minimumContextTokens,
            maximumContextTokens,
            level,
            requiresEvidence);
    }

    internal GgufRouteConfiguration ToRouteConfiguration() =>
        GgufRouteConfiguration.Create(Weights, KvCache, Backend, Device, Offload);
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 12 added in this task.

- [ ] **Step 5: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/CompatibilitySupportEntry.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/CompatibilitySupportEntryTests.cs
git commit -m "feat(compatibility): add the complete support matrix entry"
```

---

### Task 3: The matrix and its resolver

Spec section 10 gives four resolution rules. The fourth — "Unknown / ambiguous → Unsupported" — is the important one: it is what makes the absence of a claim mean "no", not "probably fine".

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/SupportMatrix.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/SupportMatrixResolver.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/SupportMatrixResolverTests.cs`

**Interfaces:**
- Consumes: `CompatibilitySupportEntry` (Task 2); `SupportLevel`, `InstallationState`, `SupportAvailability` (Task 1); `PolicyProvenance` from `Core.Application.FitAssessment`.
- Produces: `SupportMatrix.ProvisionalV1()`, `SupportMatrix.Absent()`, `SupportMatrix.FromEntries(string matrixVersion, PolicyProvenance provenance, IReadOnlyList<CompatibilitySupportEntry> entries)`, properties `Provenance`, `MatrixVersion`, `Entries`; and `SupportMatrixResolver.Resolve(SupportLevel level, InstallationState installation)` returning `SupportAvailability`. Consumed by Tasks 4 and 5.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/SupportMatrixResolverTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Capabilities;

[TestClass]
public sealed class SupportMatrixResolverTests
{
    [TestMethod]
    [DataRow(
        nameof(SupportLevel.DeclaredSupported),
        nameof(InstallationState.InstalledAndVerified),
        nameof(SupportAvailability.Available))]
    [DataRow(
        nameof(SupportLevel.DeclaredSupported),
        nameof(InstallationState.NotInstalled),
        nameof(SupportAvailability.Unavailable))]
    [DataRow(
        nameof(SupportLevel.Experimental),
        nameof(InstallationState.VerifiedAndOptedIn),
        nameof(SupportAvailability.ExperimentalAvailable))]
    public void Resolve_AppliesTheThreeDeclaredRules(
        string level,
        string installation,
        string expected)
    {
        SupportAvailability resolved = SupportMatrixResolver.Resolve(
            Enum.Parse<SupportLevel>(level),
            Enum.Parse<InstallationState>(installation));

        Assert.AreEqual(expected, resolved.ToString());
    }

    [TestMethod]
    public void Resolve_TreatsEveryOtherPairingAsUnsupported()
    {
        // Exhaustive sweep. The fourth rule is "unknown or ambiguous is
        // unsupported", so a pairing nobody thought about must land there rather
        // than on an available value. A new enum member fails this test.
        (SupportLevel Level, InstallationState Installation)[] declared =
        [
            (SupportLevel.DeclaredSupported, InstallationState.InstalledAndVerified),
            (SupportLevel.DeclaredSupported, InstallationState.NotInstalled),
            (SupportLevel.Experimental, InstallationState.VerifiedAndOptedIn)
        ];

        int visited = 0;

        foreach (SupportLevel level in Enum.GetValues<SupportLevel>())
        {
            foreach (InstallationState installation in Enum.GetValues<InstallationState>())
            {
                visited++;
                SupportAvailability resolved =
                    SupportMatrixResolver.Resolve(level, installation);

                if (declared.Contains((level, installation)))
                {
                    Assert.AreNotEqual(
                        SupportAvailability.Unsupported,
                        resolved,
                        $"{level} with {installation} is a declared rule.");
                    continue;
                }

                Assert.AreEqual(
                    SupportAvailability.Unsupported,
                    resolved,
                    $"{level} with {installation} is undeclared and must be unsupported.");
            }
        }

        Assert.AreEqual(
            12,
            visited,
            "An enum member was added; extend the declared-rule table above.");
    }

    [TestMethod]
    public void Resolve_DoesNotAdmitAnExperimentalRouteWithoutOptIn()
    {
        // Installed and verified is not enough for an experimental route: it may
        // produce wrong output rather than merely failing, so the user must have
        // said yes to it explicitly.
        Assert.AreEqual(
            nameof(SupportAvailability.Unsupported),
            SupportMatrixResolver.Resolve(
                SupportLevel.Experimental,
                InstallationState.InstalledAndVerified).ToString());
    }

    [TestMethod]
    public void ProvisionalV1_CarriesProvisionalProvenanceAndAStableVersion()
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        Assert.AreEqual(
            nameof(PolicyProvenance.Provisional), matrix.Provenance.ToString());
        Assert.AreEqual("support-matrix-v1", matrix.MatrixVersion);
    }

    [TestMethod]
    public void ProvisionalV1_EntryIdsAreUnique()
    {
        // A candidate carries its entry id as provenance, so a duplicate id makes
        // two different configurations indistinguishable after the fact.
        IReadOnlyList<CompatibilitySupportEntry> entries = SupportMatrix.ProvisionalV1().Entries;

        Assert.AreEqual(
            entries.Count,
            entries.Select(entry => entry.EntryId).Distinct().Count());
    }

    [TestMethod]
    public void ProvisionalV1_ContainsTheImportedCpuBaselineShape()
    {
        // Without an entry admitting the as-imported configuration on CPU, no
        // baseline candidate can ever be generated on a machine with no GPU.
        Assert.IsTrue(
            SupportMatrix.ProvisionalV1().Entries.Any(entry =>
                entry.Device == DeviceRouteId.Cpu
                && entry.Weights == Routes.Gguf.GgufWeightFormat.Imported
                && entry.Level == SupportLevel.DeclaredSupported));
    }

    [TestMethod]
    public void Absent_HasNoEntriesAndAbsentProvenance()
    {
        SupportMatrix matrix = SupportMatrix.Absent();

        Assert.AreEqual(nameof(PolicyProvenance.Absent), matrix.Provenance.ToString());
        Assert.AreEqual(0, matrix.Entries.Count);
    }

    [TestMethod]
    public void FromEntries_RejectsDuplicateEntryIds()
    {
        CompatibilitySupportEntry entry = CompatibilitySupportEntry.Create(
            "duplicate",
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            Routes.Gguf.GpuOffloadLevel.None,
            Routes.Gguf.GgufWeightFormat.Imported,
            Routes.Gguf.GgufKvCacheFormat.F16,
            1024,
            32768,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

        Assert.ThrowsExactly<ArgumentException>(
            () => SupportMatrix.FromEntries(
                "v-test", PolicyProvenance.Provisional, [entry, entry]));
    }

    [TestMethod]
    public void FromEntries_CopiesEntriesSoLaterMutationCannotChangeTheMatrix()
    {
        List<CompatibilitySupportEntry> entries = [];

        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-test", PolicyProvenance.Provisional, entries);

        entries.Add(CompatibilitySupportEntry.Create(
            "added-later",
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            Routes.Gguf.GpuOffloadLevel.None,
            Routes.Gguf.GgufWeightFormat.Imported,
            Routes.Gguf.GgufKvCacheFormat.F16,
            1024,
            32768,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false));

        Assert.AreEqual(0, matrix.Entries.Count);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'SupportMatrixResolver' could not be found`.

- [ ] **Step 3: Implement the resolver**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/SupportMatrixResolver.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// Turns a declared support level and an observed installation state into a
/// single answer.
///
/// Only three pairings admit anything. Everything else is unsupported, which is
/// what makes the absence of a claim mean "no" rather than "probably fine" — and
/// it is why a new enum member on either side lands on the safe answer without
/// anyone having to remember to handle it.
/// </summary>
internal static class SupportMatrixResolver
{
    internal static SupportAvailability Resolve(
        SupportLevel level,
        InstallationState installation) =>
        (level, installation) switch
        {
            (SupportLevel.DeclaredSupported, InstallationState.InstalledAndVerified) =>
                SupportAvailability.Available,

            (SupportLevel.DeclaredSupported, InstallationState.NotInstalled) =>
                SupportAvailability.Unavailable,

            // Opt-in is required, not merely installation: an experimental route
            // may produce wrong output rather than simply failing to start.
            (SupportLevel.Experimental, InstallationState.VerifiedAndOptedIn) =>
                SupportAvailability.ExperimentalAvailable,

            _ => SupportAvailability.Unsupported
        };
}
```

- [ ] **Step 4: Implement the matrix**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/SupportMatrix.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// The versioned set of admitted configuration shapes.
///
/// Like the safety and estimator policies this carries an explicit provenance:
/// an absent matrix admits nothing at all rather than falling back to a guess at
/// what the machine might support.
/// </summary>
internal sealed record SupportMatrix
{
    private SupportMatrix(
        PolicyProvenance provenance,
        string matrixVersion,
        IReadOnlyList<CompatibilitySupportEntry> entries)
    {
        Provenance = provenance;
        MatrixVersion = matrixVersion;
        Entries = entries;
    }

    internal PolicyProvenance Provenance { get; }

    internal string MatrixVersion { get; }

    internal IReadOnlyList<CompatibilitySupportEntry> Entries { get; }

    internal static SupportMatrix FromEntries(
        string matrixVersion,
        PolicyProvenance provenance,
        IReadOnlyList<CompatibilitySupportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (string.IsNullOrWhiteSpace(matrixVersion))
        {
            throw new ArgumentException(
                "A matrix must be versioned so a result can name what admitted it.",
                nameof(matrixVersion));
        }

        if (entries.Select(entry => entry.EntryId).Distinct().Count() != entries.Count)
        {
            throw new ArgumentException(
                "Entry ids must be unique; a candidate carries one as provenance, "
                + "so a duplicate makes two configurations indistinguishable.",
                nameof(entries));
        }

        // Copy so a later caller mutation cannot change an already-published matrix.
        return new SupportMatrix(provenance, matrixVersion, [.. entries]);
    }

    /// <summary>
    /// Version one. These shapes are the ones the workflow documents describe as
    /// admitted; the installation state of each is decided at runtime, not here.
    /// </summary>
    internal static SupportMatrix ProvisionalV1() => FromEntries(
        "support-matrix-v1",
        PolicyProvenance.Provisional,
        [
            Entry("gguf-cpu-imported-f16",
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None,
                GgufWeightFormat.Imported, GgufKvCacheFormat.F16),

            Entry("gguf-cpu-imported-q8kv",
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None,
                GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0),

            Entry("gguf-igpu-sycl-imported-f16",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.F16),

            Entry("gguf-igpu-sycl-imported-q8kv",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0),

            Entry("gguf-dgpu-sycl-imported-f16",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.F16),

            Entry("gguf-dgpu-sycl-imported-q8kv",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported, GgufKvCacheFormat.Q8_0),

            Entry("gguf-cpu-q4km-f16",
                CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None,
                GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16),

            // TurboQuant replaces the standard KV component set rather than
            // joining it, and it is not yet backed by measured evidence.
            Entry("gguf-dgpu-sycl-imported-tq3",
                CompatibilityBackend.IntelSycl, DeviceRouteId.IntelDiscreteGpu,
                GpuOffloadLevel.Full, GgufWeightFormat.Imported,
                GgufKvCacheFormat.TurboQuant3Bit,
                level: SupportLevel.Experimental, requiresEvidence: true)
        ]);

    internal static SupportMatrix Absent() =>
        new(PolicyProvenance.Absent, "absent", []);

    private static CompatibilitySupportEntry Entry(
        string entryId,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload,
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        SupportLevel level = SupportLevel.DeclaredSupported,
        bool requiresEvidence = false) =>
        CompatibilitySupportEntry.Create(
            entryId,
            RuntimeRouteId.LlamaCpp,
            backend,
            device,
            offload,
            weights,
            kvCache,
            minimumContextTokens: 1024,
            maximumContextTokens: 32768,
            level,
            requiresEvidence);
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 12 added in this task.

- [ ] **Step 6: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/ tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/SupportMatrixResolverTests.cs
git commit -m "feat(compatibility): add the versioned support matrix and its resolver"
```

---

### Task 4: The candidate generator

Walks admitted entries, expands the context ladder, applies the preparation rules, deduplicates by fingerprint, and puts the baseline first. When the baseline cannot be generated, the exact reason is preserved rather than the baseline silently missing.

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/TrustedSourceAvailability.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateGenerationRequest.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateGenerator.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Candidates/CandidateGeneratorTests.cs`

**Interfaces:**
- Consumes: `SupportMatrix`, `SupportMatrixResolver`, `CompatibilitySupportEntry` (Tasks 2-3); `ContextLadderPolicy`, `CompatibilityCandidate`, `CandidatePreparation` from M3a; `InspectedModelFacts` from M3b; `WeightQuantisationMap`, `GgufWeightFormatMap`.
- Produces: `TrustedSourceAvailability.None()` / `.HigherPrecisionAvailable()`; `BaselineExclusionReason` enum; `CandidateGenerationResult`; `CandidateGenerator.Generate(CandidateGenerationRequest request)`. Consumed by Task 5 and by the mode-selection plan.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Candidates/CandidateGeneratorTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Candidates;

[TestClass]
public sealed class CandidateGeneratorTests
{
    private static InspectedModelFacts Facts(int? fileType = 15) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 8192,
            fileType,
            quantisationVersion: 2);

    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    private static Dictionary<string, InstallationState> AllInstalled(SupportMatrix matrix) =>
        matrix.Entries.ToDictionary(
            entry => entry.EntryId,
            entry => entry.Level == SupportLevel.Experimental
                ? InstallationState.VerifiedAndOptedIn
                : InstallationState.InstalledAndVerified);

    private static CandidateGenerationResult Generate(
        SupportMatrix? matrix = null,
        IReadOnlyDictionary<string, InstallationState>? installation = null,
        InspectedModelFacts? facts = null,
        TrustedSourceAvailability? trustedSource = null,
        int preservationTokens = 4096)
    {
        SupportMatrix resolved = matrix ?? SupportMatrix.ProvisionalV1();

        return CandidateGenerator.Generate(new CandidateGenerationRequest(
            resolved,
            installation ?? AllInstalled(resolved),
            facts ?? Facts(),
            Baseline(),
            ContextTokenCount.FromTokens(4096),
            ContextTokenCount.FromTokens(preservationTokens),
            trustedSource ?? TrustedSourceAvailability.None()));
    }

    [TestMethod]
    public void Generate_PutsTheBaselineFirst()
    {
        CandidateGenerationResult result = Generate();

        Assert.IsTrue(result.Candidates.Count > 0);
        Assert.IsTrue(result.Candidates[0].IsBaseline);
        Assert.IsTrue(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.None), result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_MarksExactlyOneCandidateAsTheBaseline()
    {
        Assert.AreEqual(1, Generate().Candidates.Count(candidate => candidate.IsBaseline));
    }

    [TestMethod]
    public void Generate_ProducesNoDuplicateFingerprints()
    {
        IReadOnlyList<CompatibilityCandidate> candidates = Generate().Candidates;

        Assert.AreEqual(
            candidates.Count,
            candidates.Select(candidate => candidate.Fingerprint.Value).Distinct().Count());
    }

    [TestMethod]
    public void Generate_AdmitsNothingFromAnAbsentMatrix()
    {
        // An absent matrix means we do not know what this machine supports.
        // Offering anything would be inventing a capability.
        CandidateGenerationResult result = Generate(
            matrix: SupportMatrix.Absent(),
            installation: new Dictionary<string, InstallationState>());

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.SupportMatrixUnavailable),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_SkipsEntriesWhoseInstallationIsUnknown()
    {
        // An entry with no reported installation state resolves to Unsupported,
        // so nothing from it may be offered.
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: new Dictionary<string, InstallationState>());

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.NoAdmittedEntryMatchesTheBaseline),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_NeverOffersAnUpwardConversion()
    {
        // The source is Q4_K_M. An entry asking for Q8_0 would be an upgrade that
        // cannot restore quality already discarded, so it is never generated.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-upward",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "upward-q8",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Q8_0,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: AllInstalled(matrix),
            trustedSource: TrustedSourceAvailability.HigherPrecisionAvailable());

        Assert.AreEqual(0, result.Candidates.Count);
    }

    [TestMethod]
    public void Generate_DoesNotRequantiseWithoutATrustedHigherPrecisionSource()
    {
        // Source Q4_K_M, entry asks for Q3_K_M. That is a real downward
        // conversion, but requantising an already-quantised file compounds loss,
        // so it needs a trusted higher-precision source to convert from.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-downward",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "downward-q3",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Q3KM,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        Assert.AreEqual(
            0,
            Generate(
                matrix: matrix,
                installation: AllInstalled(matrix),
                trustedSource: TrustedSourceAvailability.None()).Candidates.Count);

        IReadOnlyList<CompatibilityCandidate> withSource = Generate(
            matrix: matrix,
            installation: AllInstalled(matrix),
            trustedSource: TrustedSourceAvailability.HigherPrecisionAvailable()).Candidates;

        Assert.IsTrue(withSource.Count > 0);
        Assert.IsTrue(withSource.All(candidate =>
            candidate.Preparation == CandidatePreparation.WeightConversionRequired));
    }

    [TestMethod]
    public void Generate_LabelsASettingsOnlyChangeAsRuntimeProfileOnly()
    {
        // Same weights, different KV format: no new file is written, so the user
        // must not be warned about a conversion that is not happening.
        CompatibilityCandidate candidate = Generate().Candidates.First(candidate =>
            candidate.Configuration is GgufRouteConfiguration configuration
            && configuration.KvCache == GgufKvCacheFormat.Q8_0
            && configuration.Device == DeviceRouteId.Cpu);

        Assert.AreEqual(
            nameof(CandidatePreparation.RuntimeProfileOnly),
            candidate.Preparation.ToString());
    }

    [TestMethod]
    public void Generate_LabelsTheBaselineAsNeedingNoPreparation()
    {
        Assert.AreEqual(
            nameof(CandidatePreparation.None),
            Generate().Candidates[0].Preparation.ToString());
    }

    [TestMethod]
    public void Generate_NeverExceedsTheModelsDeclaredContextLimit()
    {
        // The model declares 8192. A 32768 rung would be an automatic extension
        // beyond the trained limit, which is excluded.
        Assert.IsTrue(
            Generate(preservationTokens: 32768).Candidates.All(
                candidate => candidate.Context.Tokens <= 8192));
    }

    [TestMethod]
    public void Generate_NeverFallsBelowAnEntrysMinimumContext()
    {
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-min",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "high-minimum",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    minimumContextTokens: 4096,
                    maximumContextTokens: 32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        Assert.IsTrue(
            Generate(matrix: matrix, installation: AllInstalled(matrix))
                .Candidates.All(candidate => candidate.Context.Tokens >= 4096));
    }

    [TestMethod]
    public void Generate_NeverExceedsAnEntrysMaximumContext()
    {
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-max",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "low-maximum",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    minimumContextTokens: 1024,
                    maximumContextTokens: 2048,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        Assert.IsTrue(
            Generate(matrix: matrix, installation: AllInstalled(matrix))
                .Candidates.All(candidate => candidate.Context.Tokens <= 2048));
    }

    [TestMethod]
    public void Generate_FlagsExperimentalCandidatesAsExperimental()
    {
        IReadOnlyList<CompatibilityCandidate> experimental =
            [.. Generate().Candidates.Where(candidate => candidate.IsExperimental)];

        Assert.IsTrue(experimental.Count > 0);
        Assert.IsTrue(experimental.All(candidate =>
            candidate.SupportEntryId == "gguf-dgpu-sycl-imported-tq3"));
    }

    [TestMethod]
    public void Generate_IsDeterministicAcrossRuns()
    {
        string[] first = [.. Generate().Candidates.Select(c => c.Fingerprint.Value)];
        string[] second = [.. Generate().Candidates.Select(c => c.Fingerprint.Value)];

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void Generate_CarriesTheEntryIdOnEveryCandidate()
    {
        Assert.IsTrue(Generate().Candidates.All(
            candidate => !string.IsNullOrWhiteSpace(candidate.SupportEntryId)));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command.

Expected: build failure — `The type or namespace name 'CandidateGenerator' could not be found`.

- [ ] **Step 3: Implement the trusted-source input and the request**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/TrustedSourceAvailability.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Whether a trusted higher-precision source exists to convert from.
///
/// Requantising an already-quantised file compounds loss, so spec section 10
/// forbids doing it by default. Without a declared source, only the imported
/// weights may be offered.
/// </summary>
internal sealed record TrustedSourceAvailability
{
    private TrustedSourceAvailability(bool hasHigherPrecisionSource) =>
        HasHigherPrecisionSource = hasHigherPrecisionSource;

    internal bool HasHigherPrecisionSource { get; }

    internal static TrustedSourceAvailability None() => new(false);

    internal static TrustedSourceAvailability HigherPrecisionAvailable() => new(true);
}
```

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateGenerationRequest.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Everything generation needs, gathered explicitly so the generator itself
/// performs no lookup, no I/O and no clock read.
/// </summary>
internal sealed record CandidateGenerationRequest(
    SupportMatrix Matrix,
    IReadOnlyDictionary<string, InstallationState> InstallationStates,
    InspectedModelFacts Facts,
    GgufRouteConfiguration BaselineConfiguration,
    ContextTokenCount BaselineContext,
    ContextTokenCount PreservationTarget,
    TrustedSourceAvailability TrustedSource);

/// <summary>
/// Why the as-imported configuration is not among the candidates. Spec section
/// 10 requires the exact reason be preserved rather than the baseline silently
/// going missing.
/// </summary>
internal enum BaselineExclusionReason
{
    None = 0,
    SupportMatrixUnavailable,
    NoAdmittedEntryMatchesTheBaseline,
    BaselineContextOutsideEntryBounds
}

/// <summary>
/// The generated candidate set, baseline first, plus the baseline's fate.
/// </summary>
internal sealed record CandidateGenerationResult(
    IReadOnlyList<CompatibilityCandidate> Candidates,
    bool BaselineIncluded,
    BaselineExclusionReason BaselineExclusionReason);
```

- [ ] **Step 4: Implement the generator**

Create `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateGenerator.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Expands the admitted support entries into complete candidates.
///
/// The only axis expanded is context: every other combination came from a person
/// writing an entry down. Conversions are generated conservatively — never
/// upward, and never as a requantisation without a trusted higher-precision
/// source — because a conversion the user did not ask for costs disk, time and
/// quality that cannot be recovered.
/// </summary>
internal static class CandidateGenerator
{
    internal static CandidateGenerationResult Generate(CandidateGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Matrix.Provenance is PolicyProvenance.Absent
            or PolicyProvenance.Unspecified)
        {
            return new CandidateGenerationResult(
                [], false, BaselineExclusionReason.SupportMatrixUnavailable);
        }

        WeightQuantisation source = WeightQuantisationMap.FromGgufFileType(
            request.Facts.FileType, request.Facts.QuantisationVersion);

        List<CompatibilityCandidate> candidates = [];
        HashSet<string> seenFingerprints = [];
        CompatibilityCandidate? baseline = null;

        foreach (CompatibilitySupportEntry entry in request.Matrix.Entries)
        {
            InstallationState installation =
                request.InstallationStates.TryGetValue(entry.EntryId, out InstallationState state)
                    ? state
                    : InstallationState.Unknown;

            SupportAvailability availability =
                SupportMatrixResolver.Resolve(entry.Level, installation);

            if (availability is not (SupportAvailability.Available
                or SupportAvailability.ExperimentalAvailable))
            {
                continue;
            }

            if (!TryResolvePreparationKind(entry, source, request.TrustedSource, out bool converts))
            {
                continue;
            }

            GgufRouteConfiguration configuration = entry.ToRouteConfiguration();

            foreach (ContextTokenCount context in AdmittedContexts(entry, request))
            {
                bool isBaselineShape =
                    configuration == request.BaselineConfiguration
                    && context == request.BaselineContext;

                CandidatePreparation preparation = converts
                    ? CandidatePreparation.WeightConversionRequired
                    : isBaselineShape
                        ? CandidatePreparation.None
                        : CandidatePreparation.RuntimeProfileOnly;

                CompatibilityCandidate candidate = CompatibilityCandidate.Create(
                    configuration,
                    context,
                    preparation,
                    entry.EntryId,
                    isExperimental: availability == SupportAvailability.ExperimentalAvailable,
                    isBaseline: isBaselineShape);

                if (!seenFingerprints.Add(candidate.Fingerprint.Value))
                {
                    continue;
                }

                if (isBaselineShape)
                {
                    baseline = candidate;
                    continue;
                }

                candidates.Add(candidate);
            }
        }

        if (baseline is null)
        {
            return new CandidateGenerationResult(
                candidates,
                false,
                BaselineExclusionReason.NoAdmittedEntryMatchesTheBaseline);
        }

        // The baseline is always evaluated first: it is what the user already
        // has, and every alternative is judged relative to it.
        return new CandidateGenerationResult(
            [baseline, .. candidates], true, BaselineExclusionReason.None);
    }

    /// <summary>
    /// Decides whether an entry's weight format is reachable from the imported
    /// file, and whether reaching it writes a new artifact. Returns false when the
    /// entry must not be offered at all.
    /// </summary>
    private static bool TryResolvePreparationKind(
        CompatibilitySupportEntry entry,
        WeightQuantisation source,
        TrustedSourceAvailability trustedSource,
        out bool converts)
    {
        converts = false;

        if (entry.Weights == GgufWeightFormat.Imported)
        {
            return true;
        }

        WeightQuantisation target = GgufWeightFormatMap.ToCanonical(entry.Weights);

        if (target == WeightQuantisation.Unknown || source == WeightQuantisation.Unknown)
        {
            return false;
        }

        // Asking for the encoding the file already has is a no-op, not a conversion.
        if (target == source)
        {
            return true;
        }

        // Converting upward never restores quality already discarded, so it is
        // never generated as an upgrade.
        if (WeightQuantisationMap.BitsPerWeight(target)
            > WeightQuantisationMap.BitsPerWeight(source))
        {
            return false;
        }

        if (!trustedSource.HasHigherPrecisionSource)
        {
            return false;
        }

        converts = true;
        return true;
    }

    private static IEnumerable<ContextTokenCount> AdmittedContexts(
        CompatibilitySupportEntry entry,
        CandidateGenerationRequest request)
    {
        int modelLimit = request.Facts.DeclaredContextLimit ?? entry.MaximumContextTokens;

        IReadOnlyList<ContextTokenCount> ladder = ContextLadderPolicy.Build(
            request.PreservationTarget,
            request.BaselineContext,
            modelLimit,
            entry.MinimumContextTokens);

        // The ladder offers the preservation target even above the model limit,
        // because the user asked for it. Admissibility is decided here.
        return ladder.Where(rung =>
            rung.Tokens >= entry.MinimumContextTokens
            && rung.Tokens <= entry.MaximumContextTokens
            && rung.Tokens <= modelLimit);
    }
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run the suite command.

Expected: PASS — every test green, including the 15 added in this task.

- [ ] **Step 6: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/ tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Candidates/
git commit -m "feat(compatibility): generate candidates from the admitted matrix"
```

---

### Task 5: Generation invariants

Properties that must hold whatever entries the matrix later contains. These are what stop a future matrix edit from quietly producing an offer the design forbids.

**Files:**
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/GenerationInvariantTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-4 plus `GgufResourceEstimator` and `EstimatorPolicy` from M3b.
- Produces: no production type.

- [ ] **Step 1: Write the invariant tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/GenerationInvariantTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties of generation that must hold however the matrix is later edited.
/// A new entry that violates one of these is an offer the design forbids.
/// </summary>
[TestClass]
public sealed class GenerationInvariantTests
{
    private static InspectedModelFacts Facts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

    private static CandidateGenerationResult GenerateAll(
        TrustedSourceAvailability? trustedSource = null)
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        return CandidateGenerator.Generate(new CandidateGenerationRequest(
            matrix,
            matrix.Entries.ToDictionary(
                entry => entry.EntryId,
                entry => entry.Level == SupportLevel.Experimental
                    ? InstallationState.VerifiedAndOptedIn
                    : InstallationState.InstalledAndVerified),
            Facts(),
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            ContextTokenCount.FromTokens(8192),
            trustedSource ?? TrustedSourceAvailability.HigherPrecisionAvailable()));
    }

    [TestMethod]
    public void NoGeneratedCandidateEverConvertsUpward()
    {
        // The source is Q4_K_M throughout. Any candidate whose target encoding
        // uses more bits per weight would be an upgrade that cannot restore
        // quality already discarded.
        decimal sourceBits = WeightQuantisationMap.BitsPerWeight(WeightQuantisation.Q4_K_M);

        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;

            if (configuration.Weights == GgufWeightFormat.Imported)
            {
                continue;
            }

            WeightQuantisation target =
                GgufWeightFormatMap.ToCanonical(configuration.Weights);

            Assert.IsTrue(
                WeightQuantisationMap.BitsPerWeight(target) <= sourceBits,
                $"{candidate.SupportEntryId} converts upward to {target}.");
        }
    }

    [TestMethod]
    public void EveryCandidateIsEstimable()
    {
        // A generated candidate that the estimator cannot size would reach the
        // user as an option with no memory figure beside it.
        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                Facts(), candidate, EstimatorPolicy.ProvisionalV1());

            Assert.AreEqual(
                nameof(EstimationStatus.Established),
                estimate.Status.ToString(),
                $"{candidate.SupportEntryId} at {candidate.Context} gave {estimate.Reason}.");
        }
    }

    [TestMethod]
    public void EveryCandidateNamesAnEntryThatExistsInTheMatrix()
    {
        HashSet<string> entryIds =
            [.. SupportMatrix.ProvisionalV1().Entries.Select(entry => entry.EntryId)];

        Assert.IsTrue(GenerateAll().Candidates.All(
            candidate => entryIds.Contains(candidate.SupportEntryId)));
    }

    [TestMethod]
    public void EveryConversionCandidateDeclaresWeightConversionRequired()
    {
        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;

            bool changesEncoding =
                configuration.Weights != GgufWeightFormat.Imported
                && GgufWeightFormatMap.ToCanonical(configuration.Weights)
                    != WeightQuantisation.Q4_K_M;

            if (changesEncoding)
            {
                Assert.AreEqual(
                    CandidatePreparation.WeightConversionRequired,
                    candidate.Preparation,
                    $"{candidate.SupportEntryId} writes a new file without saying so.");
            }
        }
    }

    [TestMethod]
    public void NoCandidateIsGeneratedWithoutATrustedSourceExceptImportedEncodings()
    {
        foreach (CompatibilityCandidate candidate in
            GenerateAll(TrustedSourceAvailability.None()).Candidates)
        {
            Assert.AreNotEqual(
                CandidatePreparation.WeightConversionRequired,
                candidate.Preparation,
                $"{candidate.SupportEntryId} converts with no trusted source.");
        }
    }

    [TestMethod]
    public void ExperimentalEntriesProduceOnlyExperimentalCandidates()
    {
        Dictionary<string, SupportLevel> levels = SupportMatrix.ProvisionalV1().Entries
            .ToDictionary(entry => entry.EntryId, entry => entry.Level);

        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            Assert.AreEqual(
                levels[candidate.SupportEntryId] == SupportLevel.Experimental,
                candidate.IsExperimental,
                $"{candidate.SupportEntryId} disagrees with its entry's support level.");
        }
    }

    [TestMethod]
    public void GenerationIsStableUnderEntryReordering()
    {
        // The matrix's authoring order must not change which candidates exist,
        // only the order they appear in after the baseline.
        SupportMatrix forward = SupportMatrix.ProvisionalV1();
        SupportMatrix reversed = SupportMatrix.FromEntries(
            forward.MatrixVersion,
            forward.Provenance,
            [.. forward.Entries.Reverse()]);

        Dictionary<string, InstallationState> installation = forward.Entries.ToDictionary(
            entry => entry.EntryId,
            entry => entry.Level == SupportLevel.Experimental
                ? InstallationState.VerifiedAndOptedIn
                : InstallationState.InstalledAndVerified);

        CandidateGenerationRequest Request(SupportMatrix matrix) => new(
            matrix,
            installation,
            Facts(),
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            ContextTokenCount.FromTokens(8192),
            TrustedSourceAvailability.HigherPrecisionAvailable());

        HashSet<string> forwardPrints =
            [.. CandidateGenerator.Generate(Request(forward)).Candidates
                .Select(candidate => candidate.Fingerprint.Value)];

        HashSet<string> reversedPrints =
            [.. CandidateGenerator.Generate(Request(reversed)).Candidates
                .Select(candidate => candidate.Fingerprint.Value)];

        Assert.IsTrue(forwardPrints.SetEquals(reversedPrints));
    }

    [TestMethod]
    public void TheBaselineIsAlwaysFirstWhenItIsIncluded()
    {
        CandidateGenerationResult result = GenerateAll();

        Assert.IsTrue(result.BaselineIncluded);
        Assert.IsTrue(result.Candidates[0].IsBaseline);
        Assert.IsFalse(result.Candidates.Skip(1).Any(candidate => candidate.IsBaseline));
    }
}
```

- [ ] **Step 2: Run the whole suite**

Run the suite command.

Expected: PASS — every test green, including the 8 added in this task.

- [ ] **Step 3: Commit**

```bash
git add tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/GenerationInvariantTests.cs
git commit -m "test(compatibility): pin the generation invariants"
```

---

## Self-Review

**Spec coverage.** Section 10 support-matrix resolution → Task 3's resolver, with the fourth rule enforced by an exhaustive sweep. Section 10 candidate completeness and "not a Cartesian product" → Task 2's one-shape-per-entry design plus Task 4's context-only expansion. Section 10 context ladder bounds → Task 4's `AdmittedContexts`, tested at both bounds and against the model limit. Section 10 preparation rules, including "never generate an upward conversion" and "never requantise by default" → Task 4's `TryResolvePreparationKind`, pinned by Task 5. Section 10 baseline-first and reason-preserved → Task 4's result record.

**Deliberately out of scope.** Mode selection (section 11) ranks these candidates and is the next plan. The evidence and admission-level gate is recorded on the entry as `RequiresEvidence` but is not yet enforced, because no evidence source exists to test against — that gate belongs with mode selection.

**Placeholder scan.** No TBD, no "add validation", no "similar to Task N". Every code step carries complete code.

**Type consistency.** `CompatibilitySupportEntry.ToRouteConfiguration` (Task 2) is consumed by Task 4. `SupportMatrixResolver.Resolve` (Task 3) is consumed by Task 4. `ContextLadderPolicy.Build(preservationTarget, baseline, modelLimitTokens, entryMinimumTokens)` matches the M3a signature exactly. `CompatibilityCandidate.Create(configuration, context, preparation, supportEntryId, isExperimental, isBaseline)` matches M3a. `WeightQuantisationMap.BitsPerWeight` and `GgufWeightFormatMap.ToCanonical` match the M3b split.

**Known follow-up.** `CompatibilitySupportEntry.RequiresEvidence` has no consumer until mode selection. `TrustedSourceAvailability` carries only a boolean; when a real source registry exists it will need to name which source and at what encoding.
