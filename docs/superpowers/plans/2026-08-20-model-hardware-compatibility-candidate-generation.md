# Model/Hardware Compatibility Candidate Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate complete, deduplicated, route-valid candidate configurations from a versioned support matrix, with a deterministic fingerprint and the approved context ladder.

**Architecture:** A route-neutral `CompatibilityCandidate` shell carries identity, context, preparation and a closed route-specific configuration record. GGUF configuration is a sealed type; OpenVINO gets its own later. Generation starts from one admitted support-matrix entry per candidate — never a Cartesian product — and deduplicates by fingerprint.

**Tech Stack:** C# 12, .NET 8 (`net8.0`), MSTest 4.3.2 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-design.md`, sections 10 and 4.

**Predecessor:** `docs/superpowers/plans/2026-08-20-model-hardware-compatibility-core-foundation.md` (M1+M2, complete: 58 tests green).

**Scope:** M3a only. The GGUF resource estimator (M3b) and mode selection (M3c) are separate plans, because a mode ranks *evaluated* candidates and evaluation needs the estimator.

## Global Constraints

- Target `net8.0`, `Nullable` enabled, `TreatWarningsAsErrors` true. No WinUI, no Windows-only API.
- Types are `internal`; the test project already has `InternalsVisibleTo`.
- Every enum reserves `Unspecified = 0` and rejects it at construction.
- MSTest 4: use `[TestMethod]` with `[DataRow]`; `DataTestMethod` is deprecated and fails the build.
- A public test method cannot take an `internal` enum parameter (CS0051). Pass `nameof(...)` strings and compare against `.ToString()`.
- **No candidate may be partially filled.** Every field is set at construction or construction fails.
- Fingerprints must be culture-invariant and stable across processes.
- No path, filename, model name, hostname or raw tool output in any type or test fixture.
- Route facts never mix: a GGUF candidate can never carry an OpenVINO device or precision, and vice versa.

---

### Task 1: Route identity enums and the GGUF configuration record

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/RuntimeRouteId.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/CompatibilityBackend.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/DeviceRouteId.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufWeightFormat.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufKvCacheFormat.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GpuOffloadLevel.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/RouteConfiguration.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Routes/Gguf/GgufRouteConfiguration.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Routes/GgufRouteConfigurationTests.cs`

**Interfaces:**
- Consumes: nothing from M1/M2.
- Produces: `RuntimeRouteId`, `CompatibilityBackend`, `DeviceRouteId`, `GgufWeightFormat`, `GgufKvCacheFormat`, `GpuOffloadLevel`; abstract `RouteConfiguration` exposing `RouteId` and `CanonicalDescriptor`; `GgufRouteConfiguration.Create(GgufWeightFormat, GgufKvCacheFormat, CompatibilityBackend, DeviceRouteId, GpuOffloadLevel)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Routes;

[TestClass]
public sealed class GgufRouteConfigurationTests
{
    private static GgufRouteConfiguration Valid() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM,
            GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.IntelSycl,
            DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Full);

    [TestMethod]
    public void Create_ExposesTheLlamaCppRoute()
    {
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, Valid().RouteId);
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedWeightFormat()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Unspecified, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedKvCacheFormat()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Unspecified,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedBackend()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Unspecified, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedDevice()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Unspecified, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedOffload()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.Unspecified));
    }

    [TestMethod]
    // An OpenVINO backend on a llama.cpp configuration is route mixing.
    [DataRow(nameof(CompatibilityBackend.OpenVinoCpu))]
    [DataRow(nameof(CompatibilityBackend.OpenVinoGpu))]
    [DataRow(nameof(CompatibilityBackend.OpenVinoNpu))]
    public void Create_RejectsOpenVinoBackends(string backendName)
    {
        CompatibilityBackend backend = Enum.Parse<CompatibilityBackend>(backendName);

        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            backend, DeviceRouteId.Cpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsNpuDevice_BecauseNoLlamaCppNpuRouteIsAdmitted()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.IntelNpu, GpuOffloadLevel.None));
    }

    [TestMethod]
    public void Create_RejectsGpuOffloadOnACpuDevice()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.Cpu, DeviceRouteId.Cpu, GpuOffloadLevel.Full));
    }

    [TestMethod]
    public void CanonicalDescriptor_IsStableAndCultureInvariant()
    {
        System.Globalization.CultureInfo original =
            System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("tr-TR");
            string turkish = Valid().CanonicalDescriptor;

            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;
            string invariant = Valid().CanonicalDescriptor;

            Assert.AreEqual(invariant, turkish);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void CanonicalDescriptor_ChangesWithEveryMemoryRelevantField()
    {
        string baseline = Valid().CanonicalDescriptor;

        Assert.AreNotEqual(baseline, GgufRouteConfiguration.Create(
            GgufWeightFormat.Q6K, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Full).CanonicalDescriptor);

        Assert.AreNotEqual(baseline, GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.F16,
            CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Full).CanonicalDescriptor);

        Assert.AreNotEqual(baseline, GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.IntelVulkan, DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Full).CanonicalDescriptor);

        Assert.AreNotEqual(baseline, GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, GgufKvCacheFormat.Q8_0,
            CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Partial).CanonicalDescriptor);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter GgufRouteConfigurationTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the shared route enums**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// Which runtime family a configuration belongs to. Facts never cross between
/// routes: a GGUF candidate cannot carry OpenVINO precision or device values.
/// </summary>
internal enum RuntimeRouteId
{
    Unspecified = 0,
    LlamaCpp,
    OpenVinoGenAi
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

internal enum CompatibilityBackend
{
    Unspecified = 0,
    Cpu,
    IntelSycl,
    IntelVulkan,
    OpenVinoCpu,
    OpenVinoGpu,
    OpenVinoNpu
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

internal enum DeviceRouteId
{
    Unspecified = 0,
    Cpu,
    IntelIntegratedGpu,
    IntelDiscreteGpu,
    IntelNpu
}
```

- [ ] **Step 4: Implement the GGUF enums**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Candidate weight formats for the llama.cpp route, ordered from highest to
/// lowest expected quality. Imported means the file is used unchanged.
/// </summary>
internal enum GgufWeightFormat
{
    Unspecified = 0,
    Imported,
    BF16,
    F16,
    Q8_0,
    Q6K,
    Q5KM,
    Q4KM,
    Q3KM
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// Runtime KV-cache formats. TurboQuant is a single logical option here; the
/// backend-specific implementation is chosen when a backend is bound.
/// </summary>
internal enum GgufKvCacheFormat
{
    Unspecified = 0,
    F16,
    Q8_0,
    TurboQuant3Bit
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

internal enum GpuOffloadLevel
{
    Unspecified = 0,
    None,
    Partial,
    Full
}
```

- [ ] **Step 5: Implement the route configuration base and GGUF record**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Route-specific configuration. Concrete types are closed and sealed so a
/// candidate can never become a bag of optional cross-route properties.
/// </summary>
internal abstract record RouteConfiguration
{
    internal abstract RuntimeRouteId RouteId { get; }

    /// <summary>
    /// A stable, culture-invariant description of every memory-relevant
    /// setting. It feeds the candidate fingerprint, so it must change whenever
    /// any such setting changes and never vary by machine or locale.
    /// </summary>
    internal abstract string CanonicalDescriptor { get; }
}
```

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

/// <summary>
/// A complete llama.cpp configuration. Every field is required; there is no
/// partially specified GGUF configuration anywhere in the system.
/// </summary>
internal sealed record GgufRouteConfiguration : RouteConfiguration
{
    private GgufRouteConfiguration(
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload)
    {
        Weights = weights;
        KvCache = kvCache;
        Backend = backend;
        Device = device;
        Offload = offload;
    }

    internal GgufWeightFormat Weights { get; }

    internal GgufKvCacheFormat KvCache { get; }

    internal CompatibilityBackend Backend { get; }

    internal DeviceRouteId Device { get; }

    internal GpuOffloadLevel Offload { get; }

    internal override RuntimeRouteId RouteId => RuntimeRouteId.LlamaCpp;

    internal override string CanonicalDescriptor =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"gguf|w={Weights}|kv={KvCache}|be={Backend}|dev={Device}|off={Offload}");

    internal static GgufRouteConfiguration Create(
        GgufWeightFormat weights,
        GgufKvCacheFormat kvCache,
        CompatibilityBackend backend,
        DeviceRouteId device,
        GpuOffloadLevel offload)
    {
        if (weights == GgufWeightFormat.Unspecified)
        {
            throw new ArgumentException(
                "A GGUF configuration must declare a weight format.", nameof(weights));
        }

        if (kvCache == GgufKvCacheFormat.Unspecified)
        {
            throw new ArgumentException(
                "A GGUF configuration must declare a KV-cache format.", nameof(kvCache));
        }

        if (backend is CompatibilityBackend.Unspecified
            or CompatibilityBackend.OpenVinoCpu
            or CompatibilityBackend.OpenVinoGpu
            or CompatibilityBackend.OpenVinoNpu)
        {
            throw new ArgumentException(
                "A llama.cpp configuration must use a llama.cpp backend; "
                + "OpenVINO backends belong to the OpenVINO route.",
                nameof(backend));
        }

        if (device is DeviceRouteId.Unspecified or DeviceRouteId.IntelNpu)
        {
            throw new ArgumentException(
                "A llama.cpp configuration must target CPU or an Intel GPU; "
                + "no NPU route is admitted for llama.cpp.",
                nameof(device));
        }

        if (offload == GpuOffloadLevel.Unspecified)
        {
            throw new ArgumentException(
                "A GGUF configuration must declare its GPU offload level.",
                nameof(offload));
        }

        if (device == DeviceRouteId.Cpu && offload != GpuOffloadLevel.None)
        {
            throw new ArgumentException(
                "A CPU device cannot offload layers to a GPU.", nameof(offload));
        }

        return new GgufRouteConfiguration(weights, kvCache, backend, device, offload);
    }
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter GgufRouteConfigurationTests`
Expected: PASS, 13 test cases.

- [ ] **Step 7: Commit**

```bash
git add shared tests
git commit -m "feat(compatibility): add closed GGUF route configuration"
```

---

### Task 2: Candidate fingerprint and the candidate record

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidateFingerprint.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CandidatePreparation.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/CompatibilityCandidate.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/CompatibilityCandidateTests.cs`

**Interfaces:**
- Consumes: `RouteConfiguration`, `ContextTokenCount`.
- Produces: `CandidateFingerprint.Compute(RouteConfiguration, ContextTokenCount, CandidatePreparation)` returning a value object with `Value` (64 lowercase hex); `CompatibilityCandidate.Create(RouteConfiguration, ContextTokenCount, CandidatePreparation, string supportEntryId, bool isExperimental, bool isBaseline)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class CompatibilityCandidateTests
{
    private static GgufRouteConfiguration Config(
        GgufKvCacheFormat kv = GgufKvCacheFormat.Q8_0) =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, kv,
            CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Full);

    private static CompatibilityCandidate Candidate(
        GgufKvCacheFormat kv = GgufKvCacheFormat.Q8_0,
        int tokens = 8192) =>
        CompatibilityCandidate.Create(
            Config(kv),
            ContextTokenCount.FromTokens(tokens),
            CandidatePreparation.RuntimeProfileOnly,
            supportEntryId: "gguf-granite-sycl-v1",
            isExperimental: false,
            isBaseline: false);

    [TestMethod]
    public void Fingerprint_IsSixtyFourLowercaseHexCharacters()
    {
        string value = Candidate().Fingerprint.Value;

        Assert.AreEqual(64, value.Length);
        Assert.IsTrue(value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
    }

    [TestMethod]
    public void Fingerprint_IsIdenticalForIdenticalConfiguration()
    {
        Assert.AreEqual(Candidate().Fingerprint, Candidate().Fingerprint);
    }

    [TestMethod]
    public void Fingerprint_DiffersWhenTheKvCacheFormatDiffers()
    {
        Assert.AreNotEqual(
            Candidate(kv: GgufKvCacheFormat.Q8_0).Fingerprint,
            Candidate(kv: GgufKvCacheFormat.F16).Fingerprint);
    }

    [TestMethod]
    public void Fingerprint_DiffersWhenTheContextDiffers()
    {
        Assert.AreNotEqual(
            Candidate(tokens: 8192).Fingerprint,
            Candidate(tokens: 16384).Fingerprint);
    }

    [TestMethod]
    public void Fingerprint_IsCultureInvariant()
    {
        System.Globalization.CultureInfo original =
            System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("tr-TR");
            CandidateFingerprint turkish = Candidate().Fingerprint;

            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;
            CandidateFingerprint invariant = Candidate().Fingerprint;

            Assert.AreEqual(invariant, turkish);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void Create_RejectsAMissingSupportEntryId()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityCandidate.Create(
            Config(), ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None, supportEntryId: "  ",
            isExperimental: false, isBaseline: false));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedPreparation()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityCandidate.Create(
            Config(), ContextTokenCount.FromTokens(4096),
            CandidatePreparation.Unspecified, supportEntryId: "entry",
            isExperimental: false, isBaseline: false));
    }

    [TestMethod]
    public void Create_RejectsANullConfiguration()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CompatibilityCandidate.Create(
            null!, ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None, supportEntryId: "entry",
            isExperimental: false, isBaseline: false));
    }

    [TestMethod]
    public void Candidate_ExposesItsRouteFromTheConfiguration()
    {
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, Candidate().RouteId);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter CompatibilityCandidateTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement preparation and fingerprint**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// What must happen before a candidate can run.
/// </summary>
internal enum CandidatePreparation
{
    Unspecified = 0,

    /// <summary>The imported artifact runs directly.</summary>
    None,

    /// <summary>Runtime settings change; no new model file is created.</summary>
    RuntimeProfileOnly,

    /// <summary>A new artifact is produced from a trusted higher-precision source.</summary>
    WeightConversionRequired
}
```

```csharp
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// A deterministic identity for one complete configuration, used to remove
/// duplicates. It covers only memory-relevant settings, and deliberately
/// excludes path, timestamp, user, machine and free-memory values so the same
/// logical configuration always fingerprints identically.
/// </summary>
internal readonly record struct CandidateFingerprint
{
    private CandidateFingerprint(string value) => Value = value;

    internal string Value { get; }

    internal static CandidateFingerprint Compute(
        RouteConfiguration configuration,
        ContextTokenCount context,
        CandidatePreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string canonical = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{configuration.CanonicalDescriptor}|ctx={context.Tokens}|prep={preparation}");

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new CandidateFingerprint(Convert.ToHexString(digest).ToLowerInvariant());
    }

    public override string ToString() => Value;
}
```

- [ ] **Step 4: Implement the candidate**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// One complete, immutable configuration. There is no partially filled
/// candidate: construction either produces a fully specified configuration or
/// it fails, which is what lets later mode selection be a pure lookup.
/// </summary>
internal sealed record CompatibilityCandidate
{
    private CompatibilityCandidate(
        RouteConfiguration configuration,
        ContextTokenCount context,
        CandidatePreparation preparation,
        string supportEntryId,
        bool isExperimental,
        bool isBaseline,
        CandidateFingerprint fingerprint)
    {
        Configuration = configuration;
        Context = context;
        Preparation = preparation;
        SupportEntryId = supportEntryId;
        IsExperimental = isExperimental;
        IsBaseline = isBaseline;
        Fingerprint = fingerprint;
    }

    internal RouteConfiguration Configuration { get; }

    internal ContextTokenCount Context { get; }

    internal CandidatePreparation Preparation { get; }

    /// <summary>The admitted support-matrix entry this candidate came from.</summary>
    internal string SupportEntryId { get; }

    internal bool IsExperimental { get; }

    /// <summary>True for the as-imported configuration.</summary>
    internal bool IsBaseline { get; }

    internal CandidateFingerprint Fingerprint { get; }

    internal RuntimeRouteId RouteId => Configuration.RouteId;

    internal static CompatibilityCandidate Create(
        RouteConfiguration configuration,
        ContextTokenCount context,
        CandidatePreparation preparation,
        string supportEntryId,
        bool isExperimental,
        bool isBaseline)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (preparation == CandidatePreparation.Unspecified)
        {
            throw new ArgumentException(
                "A candidate must declare what preparation it requires.",
                nameof(preparation));
        }

        if (string.IsNullOrWhiteSpace(supportEntryId))
        {
            throw new ArgumentException(
                "A candidate must name the admitted support entry it came from, "
                + "so an unadmitted configuration can never be generated.",
                nameof(supportEntryId));
        }

        return new CompatibilityCandidate(
            configuration,
            context,
            preparation,
            supportEntryId,
            isExperimental,
            isBaseline,
            CandidateFingerprint.Compute(configuration, context, preparation));
    }
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter CompatibilityCandidateTests`
Expected: PASS, 9 tests.

- [ ] **Step 6: Commit**

```bash
git add shared tests
git commit -m "feat(compatibility): add complete candidate with deterministic fingerprint"
```

---

### Task 3: Context ladder policy

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Candidates/ContextLadderPolicy.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ContextLadderPolicyTests.cs`

**Interfaces:**
- Consumes: `ContextTokenCount`.
- Produces: `ContextLadderPolicy.Build(ContextTokenCount preservationTarget, ContextTokenCount? baseline, int modelLimitTokens, int entryMinimumTokens)` returning `IReadOnlyList<ContextTokenCount>`.

- [ ] **Step 1: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class ContextLadderPolicyTests
{
    private static int[] Build(
        int target, int? baseline, int modelLimit, int entryMinimum = 1024) =>
        ContextLadderPolicy.Build(
            ContextTokenCount.FromTokens(target),
            baseline is null ? null : ContextTokenCount.FromTokens(baseline.Value),
            modelLimit,
            entryMinimum)
            .Select(c => c.Tokens)
            .ToArray();

    [TestMethod]
    public void PreservationTarget_ComesFirst()
    {
        Assert.AreEqual(8192, Build(8192, null, 131072)[0]);
    }

    [TestMethod]
    public void BaselineComesSecond_WhenItDiffersFromTheTarget()
    {
        int[] ladder = Build(8192, 4096, 131072);

        Assert.AreEqual(8192, ladder[0]);
        Assert.AreEqual(4096, ladder[1]);
    }

    [TestMethod]
    public void BaselineIsNotRepeated_WhenItEqualsTheTarget()
    {
        int[] ladder = Build(8192, 8192, 131072);

        Assert.AreEqual(1, ladder.Count(t => t == 8192));
    }

    [TestMethod]
    public void LowerRungsFollow_InDescendingOrder()
    {
        int[] ladder = Build(8192, null, 131072);

        CollectionAssert.AreEqual(new[] { 8192, 4096, 2048, 1024 }, ladder);
    }

    [TestMethod]
    public void NeverExceedsTheModelLimit()
    {
        int[] ladder = Build(4096, null, 4096);

        Assert.IsTrue(ladder.All(t => t <= 4096));
    }

    [TestMethod]
    public void NeverExceedsExplicitUserIntent()
    {
        // A generous model limit must not push the ladder above the request.
        int[] ladder = Build(2048, null, 131072);

        Assert.IsTrue(ladder.All(t => t <= 2048));
    }

    [TestMethod]
    public void NeverGoesBelowTheSupportEntryMinimum()
    {
        int[] ladder = Build(8192, null, 131072, entryMinimum: 4096);

        Assert.IsTrue(ladder.All(t => t >= 4096));
        CollectionAssert.AreEqual(new[] { 8192, 4096 }, ladder);
    }

    [TestMethod]
    public void ContainsNoDuplicates()
    {
        int[] ladder = Build(4096, 4096, 131072);

        Assert.AreEqual(ladder.Length, ladder.Distinct().Count());
    }

    [TestMethod]
    public void ATargetAboveTheModelLimit_IsStillOfferedFirst()
    {
        // The user asked for it explicitly, so it is preserved and offered;
        // whether it is admissible is a later support and fit decision.
        int[] ladder = Build(16384, null, 8192);

        Assert.AreEqual(16384, ladder[0]);
    }

    [TestMethod]
    public void IsDeterministic()
    {
        CollectionAssert.AreEqual(Build(8192, 4096, 131072), Build(8192, 4096, 131072));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter ContextLadderPolicyTests`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Implement the ladder**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// Builds the ordered set of context lengths a candidate set may use.
/// Automatic extension beyond the model's trained limit is excluded, and the
/// ladder never rises above what the user actually asked for.
/// </summary>
internal static class ContextLadderPolicy
{
    private static readonly int[] StandardRungs =
        [1024, 2048, 4096, 8192, 16384, 32768];

    internal static IReadOnlyList<ContextTokenCount> Build(
        ContextTokenCount preservationTarget,
        ContextTokenCount? baseline,
        int modelLimitTokens,
        int entryMinimumTokens)
    {
        if (modelLimitTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modelLimitTokens));
        }

        if (entryMinimumTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryMinimumTokens));
        }

        List<ContextTokenCount> ladder = [];
        HashSet<int> seen = [];

        // The preservation target is always offered first, even when it exceeds
        // the model limit: the user asked for it, and admissibility is decided
        // later by support and fit rather than silently here.
        if (seen.Add(preservationTarget.Tokens))
        {
            ladder.Add(preservationTarget);
        }

        if (baseline is { } baselineContext && seen.Add(baselineContext.Tokens))
        {
            ladder.Add(baselineContext);
        }

        // Lower rungs give the user something smaller to fall back to, in
        // descending order so the best remaining option comes first.
        foreach (int rung in StandardRungs.OrderByDescending(rung => rung))
        {
            if (rung > preservationTarget.Tokens ||
                rung > modelLimitTokens ||
                rung < entryMinimumTokens ||
                !seen.Add(rung))
            {
                continue;
            }

            ladder.Add(ContextTokenCount.FromTokens(rung));
        }

        return ladder;
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter ContextLadderPolicyTests`
Expected: PASS, 10 tests.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests`
Expected: PASS, all tests, zero failures, zero skipped.

- [ ] **Step 6: Commit**

```bash
git add shared tests
git commit -m "feat(compatibility): build the approved context ladder"
```

---

## Self-Review

**Spec coverage.** Section 10 candidate completeness and fingerprint → Tasks 1 and 2. Section 10 context ladder → Task 3. Section 10 preparation rules → the `CandidatePreparation` enum in Task 2; the "never generate an upward conversion" and "never requantise by default" rules are enforced by the generator, which belongs to the support-matrix work and is deliberately not in this plan. Section 4 route separation → Task 1's backend and device guards.

**Placeholder scan.** No TBD, no "add validation", no "similar to Task N". Every code step carries real code.

**Type consistency.** `RouteConfiguration.CanonicalDescriptor` defined in Task 1 and consumed by `CandidateFingerprint.Compute` in Task 2. `ContextTokenCount.FromTokens` and `.Tokens` come from the completed M1 work. `CandidatePreparation` defined in Task 2 and consumed by the fingerprint in the same task.

**Deliberately deferred to the next plan.** The support matrix itself (`CompatibilitySupportEntry`, the resolver, and the JSON asset) and the generator that walks entries to emit candidates. This plan produces the vocabulary those need; splitting keeps each plan independently testable.
