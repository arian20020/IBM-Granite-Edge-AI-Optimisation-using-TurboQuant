# Model/Hardware Compatibility Core Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the route-neutral compatibility core — checked byte arithmetic, planning context, resource composition, safe budget and fit classification — as a WinUI-free `net8.0` library with a fast test loop.

**Architecture:** A plain `net8.0` class library holds all calculation logic so it cannot reference WinUI by construction. Owner facts arrive through C1-owned ports returning C1-owned calculation-domain records; the shipped port implementations all return typed unavailable, so the core is complete and tested before H1/I1 publish anything.

**Tech Stack:** C# 12, .NET 8 (`net8.0`), MSTest 4.3.2 on Microsoft.Testing.Platform, SDK 10.0.301 pinned by `global.json`.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-design.md`

**Scope:** Milestones M1 and M2 only. Candidates and modes (M3), orchestrator (M4), screens (M5) and owner adapters (M6) are separate plans.

## Global Constraints

- Target `net8.0`. No WinUI, no `Microsoft.UI.*`, no Windows-only API in this library.
- `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, matching `shared/GraniteEdgeAI.ModelInspection.Contracts`.
- All byte quantities use **checked `ulong`**. Bits→bytes uses ceiling division. Alignment rounds upward only.
- **Unknown is a typed unavailable value, never zero, never a default.**
- Every enum reserves `Unspecified = 0` and rejects it at every boundary.
- RAM and dedicated VRAM are never summed. Shared GPU memory counts against system memory exactly once.
- No absolute path, UNC path, filename, model name, hostname, credential, native error or raw tool output may appear in any type, message or test fixture in this library.
- Types are `internal` with `InternalsVisibleTo` for the test project, matching the Model Inspection contracts pattern.
- Bias is one-directional: a false-safe result is a crash, a false-unsafe result is an inconvenience. Margins are added to requirements, never subtracted.

---

### Task 1: Create the core library, test project, and `ByteCount`

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/GraniteEdgeAI.ModelHardwareCompatibility.Core.csproj`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/ByteCount.cs`
- Create: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Domain/ByteCountTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `ByteCount` — a readonly struct over `ulong` with `Zero`, `FromBytes(ulong)`, `Bytes`, checked `+`/`-`, `Add`, `Subtract`, `TrySubtract(ByteCount, out ByteCount)`, `Multiply(ulong)`, `CeilingDivide(ulong)`, `RatioAgainst(ByteCount)` returning `decimal`, value equality and `IComparable<ByteCount>`.

- [ ] **Step 1: Create the library project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="GraniteEdgeAI.ModelHardwareCompatibility.Tests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the test project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="2.3.2" />
    <PackageReference Include="MSTest.TestAdapter" Version="4.3.2" />
    <PackageReference Include="MSTest.TestFramework" Version="4.3.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\shared\GraniteEdgeAI.ModelHardwareCompatibility.Core\GraniteEdgeAI.ModelHardwareCompatibility.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Domain;

[TestClass]
public sealed class ByteCountTests
{
    [TestMethod]
    public void Add_Overflow_Throws()
    {
        ByteCount max = ByteCount.FromBytes(ulong.MaxValue);
        Assert.ThrowsExactly<OverflowException>(
            () => _ = max.Add(ByteCount.FromBytes(1)));
    }

    [TestMethod]
    public void Subtract_BelowZero_Throws()
    {
        ByteCount small = ByteCount.FromBytes(4);
        Assert.ThrowsExactly<OverflowException>(
            () => _ = small.Subtract(ByteCount.FromBytes(5)));
    }

    [TestMethod]
    public void TrySubtract_BelowZero_ReturnsFalseAndDoesNotThrow()
    {
        bool ok = ByteCount.FromBytes(4)
            .TrySubtract(ByteCount.FromBytes(5), out ByteCount remainder);

        Assert.IsFalse(ok);
        Assert.AreEqual(ByteCount.Zero, remainder);
    }

    [TestMethod]
    public void CeilingDivide_RoundsUpwardOnly()
    {
        Assert.AreEqual(4UL, ByteCount.FromBytes(13).CeilingDivide(4).Bytes);
        Assert.AreEqual(3UL, ByteCount.FromBytes(12).CeilingDivide(4).Bytes);
    }

    [TestMethod]
    public void CeilingDivide_NearMaxValue_DoesNotOverflow()
    {
        ByteCount result = ByteCount.FromBytes(ulong.MaxValue).CeilingDivide(2);
        Assert.AreEqual((ulong.MaxValue / 2) + 1, result.Bytes);
    }

    [TestMethod]
    public void CeilingDivide_ByZero_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = ByteCount.FromBytes(8).CeilingDivide(0));
    }

    [TestMethod]
    public void RatioAgainst_Zero_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = ByteCount.FromBytes(8).RatioAgainst(ByteCount.Zero));
    }

    [TestMethod]
    public void RatioAgainst_ComputesDecimalRatio()
    {
        decimal ratio = ByteCount.FromBytes(75).RatioAgainst(ByteCount.FromBytes(100));
        Assert.AreEqual(0.75m, ratio);
    }

    [TestMethod]
    public void Equality_IsByValue()
    {
        Assert.AreEqual(ByteCount.FromBytes(42), ByteCount.FromBytes(42));
        Assert.IsTrue(ByteCount.FromBytes(1) < ByteCount.FromBytes(2));
    }
}
```

- [ ] **Step 4: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests`
Expected: FAIL — `ByteCount` does not exist (CS0246).

- [ ] **Step 5: Implement `ByteCount`**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// One non-negative quantity of bytes using checked arithmetic so an
/// overflow becomes a loud failure rather than a silently wrapped value
/// that would understate a memory requirement.
/// </summary>
internal readonly struct ByteCount : IEquatable<ByteCount>, IComparable<ByteCount>
{
    private ByteCount(ulong bytes) => Bytes = bytes;

    internal static ByteCount Zero { get; } = new(0);

    internal ulong Bytes { get; }

    internal static ByteCount FromBytes(ulong bytes) => new(bytes);

    internal ByteCount Add(ByteCount other) => new(checked(Bytes + other.Bytes));

    internal ByteCount Subtract(ByteCount other) => new(checked(Bytes - other.Bytes));

    /// <summary>
    /// Subtracts without throwing. Used where "the budget is already
    /// exhausted" is an expected answer rather than a programming error.
    /// </summary>
    internal bool TrySubtract(ByteCount other, out ByteCount remainder)
    {
        if (other.Bytes > Bytes)
        {
            remainder = Zero;
            return false;
        }

        remainder = new ByteCount(Bytes - other.Bytes);
        return true;
    }

    internal ByteCount Multiply(ulong factor) => new(checked(Bytes * factor));

    /// <summary>
    /// Divides rounding upward. The addend form would overflow near
    /// ulong.MaxValue, so the remainder is tested instead.
    /// </summary>
    internal ByteCount CeilingDivide(ulong divisor)
    {
        if (divisor == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor));
        }

        ulong quotient = Bytes / divisor;
        return new ByteCount(Bytes % divisor == 0 ? quotient : checked(quotient + 1));
    }

    internal decimal RatioAgainst(ByteCount denominator)
    {
        if (denominator.Bytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }

        return (decimal)Bytes / denominator.Bytes;
    }

    public bool Equals(ByteCount other) => Bytes == other.Bytes;

    public override bool Equals(object? obj) => obj is ByteCount other && Equals(other);

    public override int GetHashCode() => Bytes.GetHashCode();

    public int CompareTo(ByteCount other) => Bytes.CompareTo(other.Bytes);

    public override string ToString() => $"{Bytes} B";

    public static bool operator ==(ByteCount left, ByteCount right) => left.Equals(right);

    public static bool operator !=(ByteCount left, ByteCount right) => !left.Equals(right);

    public static bool operator <(ByteCount left, ByteCount right) => left.CompareTo(right) < 0;

    public static bool operator >(ByteCount left, ByteCount right) => left.CompareTo(right) > 0;

    public static bool operator <=(ByteCount left, ByteCount right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ByteCount left, ByteCount right) => left.CompareTo(right) >= 0;
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests`
Expected: PASS, 8 tests.

- [ ] **Step 7: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests
git commit -m "feat(compatibility): add checked byte arithmetic"
```

---

### Task 2: Planning context policy

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/ContextTokenCount.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/CompatibilityContextMode.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/CompatibilityContextRequest.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/PlanningContext/PlanningContextStatus.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/PlanningContext/PlanningContextResolution.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/PlanningContext/PlanningContextPolicy.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/PlanningContextPolicyTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `ContextTokenCount.FromTokens(int)` with `Tokens`; `CompatibilityContextRequest.ApplicationDefault()` and `.UserRequested(ContextTokenCount)` exposing `Mode` and `RequestedTokens`; `PlanningContextPolicy.Resolve(CompatibilityContextRequest, ulong? declaredModelContextLimit)` returning `PlanningContextResolution` with `Status`, `ResolvedTokens`, `WithinModelLimit`.

- [ ] **Step 1: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class PlanningContextPolicyTests
{
    [TestMethod]
    public void ContextTokenCount_RejectsZeroAndNegative()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContextTokenCount.FromTokens(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContextTokenCount.FromTokens(-1));
    }

    [TestMethod]
    public void ApplicationDefault_CarriesNoExplicitCount()
    {
        CompatibilityContextRequest request = CompatibilityContextRequest.ApplicationDefault();

        Assert.AreEqual(CompatibilityContextMode.ApplicationDefault, request.Mode);
        Assert.IsNull(request.RequestedTokens);
    }

    [TestMethod]
    public void UserRequested_CarriesTheExactCount()
    {
        CompatibilityContextRequest request =
            CompatibilityContextRequest.UserRequested(ContextTokenCount.FromTokens(16384));

        Assert.AreEqual(CompatibilityContextMode.UserRequested, request.Mode);
        Assert.AreEqual(16384, request.RequestedTokens!.Tokens);
    }

    [DataTestMethod]
    [DataRow(131072UL, 4096)]
    [DataRow(4096UL, 4096)]
    [DataRow(2048UL, 2048)]
    public void Default_TakesMinimumOfFourThousandNinetySixAndModelLimit(
        ulong declaredLimit,
        int expected)
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(),
            declaredLimit);

        Assert.AreEqual(PlanningContextStatus.Resolved, resolution.Status);
        Assert.AreEqual(expected, resolution.ResolvedTokens!.Tokens);
    }

    [TestMethod]
    public void UserRequested_IsPreservedExactly_EvenAboveModelLimit()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.UserRequested(ContextTokenCount.FromTokens(16384)),
            8192UL);

        Assert.AreEqual(PlanningContextStatus.Resolved, resolution.Status);
        Assert.AreEqual(16384, resolution.ResolvedTokens!.Tokens);
        Assert.IsFalse(resolution.WithinModelLimit);
    }

    [TestMethod]
    public void UserRequested_WithinLimit_IsFlaggedWithin()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.UserRequested(ContextTokenCount.FromTokens(16384)),
            131072UL);

        Assert.IsTrue(resolution.WithinModelLimit);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow(0UL)]
    public void MissingOrZeroModelLimit_IsNotEstablished(ulong? declaredLimit)
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(),
            declaredLimit);

        Assert.AreEqual(PlanningContextStatus.NotEstablished, resolution.Status);
        Assert.IsNull(resolution.ResolvedTokens);
    }

    [TestMethod]
    public void ModelLimitAboveIntMaxValue_IsNotEstablished()
    {
        PlanningContextResolution resolution = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(),
            (ulong)int.MaxValue + 1);

        Assert.AreEqual(PlanningContextStatus.NotEstablished, resolution.Status);
    }

    [TestMethod]
    public void SameInput_ProducesValueEquivalentOutput()
    {
        PlanningContextResolution first = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(), 131072UL);
        PlanningContextResolution second = PlanningContextPolicy.Resolve(
            CompatibilityContextRequest.ApplicationDefault(), 131072UL);

        Assert.AreEqual(first, second);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter PlanningContextPolicyTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement `ContextTokenCount`**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// A strictly positive planning context length in tokens.
/// </summary>
internal readonly record struct ContextTokenCount
{
    private ContextTokenCount(int tokens) => Tokens = tokens;

    internal int Tokens { get; }

    internal static ContextTokenCount FromTokens(int tokens)
    {
        if (tokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokens),
                "A planning context must be a positive number of tokens.");
        }

        return new ContextTokenCount(tokens);
    }

    public override string ToString() => $"{Tokens} tokens";
}
```

- [ ] **Step 4: Implement the context request**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

internal enum CompatibilityContextMode
{
    Unspecified = 0,
    ApplicationDefault,
    UserRequested
}
```

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// The user's context intent. The two modes are mutually exclusive:
/// a default carries no number, an explicit request always carries one.
/// </summary>
internal sealed record CompatibilityContextRequest
{
    private CompatibilityContextRequest(
        CompatibilityContextMode mode,
        ContextTokenCount? requestedTokens)
    {
        Mode = mode;
        RequestedTokens = requestedTokens;
    }

    internal CompatibilityContextMode Mode { get; }

    internal ContextTokenCount? RequestedTokens { get; }

    internal static CompatibilityContextRequest ApplicationDefault() =>
        new(CompatibilityContextMode.ApplicationDefault, requestedTokens: null);

    internal static CompatibilityContextRequest UserRequested(
        ContextTokenCount requestedTokens) =>
        new(CompatibilityContextMode.UserRequested, requestedTokens);
}
```

- [ ] **Step 5: Implement the resolution and policy**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;

internal enum PlanningContextStatus
{
    Unspecified = 0,
    Resolved,
    NotEstablished
}
```

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;

internal sealed record PlanningContextResolution
{
    private PlanningContextResolution(
        PlanningContextStatus status,
        ContextTokenCount? resolvedTokens,
        bool withinModelLimit)
    {
        Status = status;
        ResolvedTokens = resolvedTokens;
        WithinModelLimit = withinModelLimit;
    }

    internal PlanningContextStatus Status { get; }

    internal ContextTokenCount? ResolvedTokens { get; }

    /// <summary>
    /// False when an explicit request exceeds the model's trained limit.
    /// The request is still preserved; downstream decides what to do.
    /// </summary>
    internal bool WithinModelLimit { get; }

    internal static PlanningContextResolution Resolved(
        ContextTokenCount tokens,
        bool withinModelLimit) =>
        new(PlanningContextStatus.Resolved, tokens, withinModelLimit);

    internal static PlanningContextResolution NotEstablished() =>
        new(PlanningContextStatus.NotEstablished, resolvedTokens: null, withinModelLimit: false);
}
```

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;

/// <summary>
/// Decision 3. Pure policy: no hardware, no I/O, no clock.
/// </summary>
internal static class PlanningContextPolicy
{
    /// <summary>
    /// The application default ceiling. Deliberately conservative so an
    /// untouched default never drives a large speculative KV cache.
    /// </summary>
    internal const int ApplicationDefaultCeilingTokens = 4096;

    internal static PlanningContextResolution Resolve(
        CompatibilityContextRequest request,
        ulong? declaredModelContextLimit)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A model whose trained limit is unknown or zero gives no safe basis
        // for a default, and an out-of-range limit is not trustworthy input.
        if (declaredModelContextLimit is not > 0 ||
            declaredModelContextLimit > int.MaxValue)
        {
            return PlanningContextResolution.NotEstablished();
        }

        int modelLimit = (int)declaredModelContextLimit.Value;

        return request.Mode switch
        {
            CompatibilityContextMode.ApplicationDefault =>
                PlanningContextResolution.Resolved(
                    ContextTokenCount.FromTokens(
                        Math.Min(ApplicationDefaultCeilingTokens, modelLimit)),
                    withinModelLimit: true),

            // An explicit request is never silently clamped. It is preserved
            // exactly and flagged, so the user sees their own number.
            CompatibilityContextMode.UserRequested =>
                PlanningContextResolution.Resolved(
                    request.RequestedTokens!.Value,
                    withinModelLimit: request.RequestedTokens!.Value.Tokens <= modelLimit),

            _ => PlanningContextResolution.NotEstablished()
        };
    }
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter PlanningContextPolicyTests`
Expected: PASS, 12 test cases.

- [ ] **Step 7: Commit**

```bash
git add shared tests
git commit -m "feat(compatibility): resolve planning context"
```

---

### Task 3: Resource targets, components and phase composition

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/ResourceTarget.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/LifecyclePhase.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/ResourceComponentKind.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/ResourceComponent.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/ResourcePeakProfile.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Estimation/ResourcePhaseComposer.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ResourcePhaseComposerTests.cs`

**Interfaces:**
- Consumes: `ByteCount` from Task 1.
- Produces: `ResourceComponent.Create(ResourceComponentKind, ResourceTarget, ByteCount, IReadOnlySet<LifecyclePhase>)`; `ResourcePhaseComposer.Compose(IReadOnlyList<ResourceComponent>)` returning `ResourcePeakProfile` with `PeakFor(ResourceTarget)` and `SystemMemoryPressure`.

- [ ] **Step 1: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class ResourcePhaseComposerTests
{
    private static ResourceComponent Component(
        ResourceComponentKind kind,
        ResourceTarget target,
        ulong bytes,
        params LifecyclePhase[] phases) =>
        ResourceComponent.Create(
            kind,
            target,
            ByteCount.FromBytes(bytes),
            new HashSet<LifecyclePhase>(phases));

    [TestMethod]
    public void Peak_IsMaximumPhaseSum_NotTotalOfAllComponents()
    {
        // Staging exists only during Load; KV cache only during generation.
        // Summing everything would overstate the requirement by 300.
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.Load, LifecyclePhase.SteadyStateGeneration),
            Component(ResourceComponentKind.StagingBuffer, ResourceTarget.SystemMemory, 300,
                LifecyclePhase.Load),
            Component(ResourceComponentKind.KvCache, ResourceTarget.SystemMemory, 500,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.AreEqual(1500UL, profile.PeakFor(ResourceTarget.SystemMemory).Bytes);
    }

    [TestMethod]
    public void Targets_AreNeverSummedTogether()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.SteadyStateGeneration),
            Component(ResourceComponentKind.Weights, ResourceTarget.DedicatedDeviceMemory, 700,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.AreEqual(1000UL, profile.PeakFor(ResourceTarget.SystemMemory).Bytes);
        Assert.AreEqual(700UL, profile.PeakFor(ResourceTarget.DedicatedDeviceMemory).Bytes);
    }

    [TestMethod]
    public void SharedDeviceMemory_CountsAgainstSystemPressureExactlyOnce()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.SteadyStateGeneration),
            Component(ResourceComponentKind.KvCache, ResourceTarget.SharedDeviceMemory, 400,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.AreEqual(1400UL, profile.SystemMemoryPressure.Bytes);
        Assert.AreEqual(1000UL, profile.PeakFor(ResourceTarget.SystemMemory).Bytes);
    }

    [TestMethod]
    public void SharedDeviceMemory_NeverIncreasesSystemCapacity()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose(
        [
            Component(ResourceComponentKind.KvCache, ResourceTarget.SharedDeviceMemory, 400,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.IsTrue(profile.SystemMemoryPressure >=
            profile.PeakFor(ResourceTarget.SystemMemory));
    }

    [TestMethod]
    public void Component_WithNoPhase_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 10));
    }

    [TestMethod]
    public void Component_WithUnspecifiedEnum_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Component(ResourceComponentKind.Unspecified, ResourceTarget.SystemMemory, 10,
                LifecyclePhase.Load));
        Assert.ThrowsExactly<ArgumentException>(
            () => Component(ResourceComponentKind.Weights, ResourceTarget.Unspecified, 10,
                LifecyclePhase.Load));
    }

    [TestMethod]
    public void AddingALiveComponent_CanNeverReduceItsPhaseRequirement()
    {
        ResourceComponent[] baseline =
        [
            Component(ResourceComponentKind.Weights, ResourceTarget.SystemMemory, 1000,
                LifecyclePhase.SteadyStateGeneration),
        ];

        ResourcePeakProfile before = ResourcePhaseComposer.Compose(baseline);
        ResourcePeakProfile after = ResourcePhaseComposer.Compose(
        [
            .. baseline,
            Component(ResourceComponentKind.ComputeBuffer, ResourceTarget.SystemMemory, 1,
                LifecyclePhase.SteadyStateGeneration),
        ]);

        Assert.IsTrue(after.PeakFor(ResourceTarget.SystemMemory) >
            before.PeakFor(ResourceTarget.SystemMemory));
    }

    [TestMethod]
    public void Compose_IsOrderIndependent()
    {
        ResourceComponent a = Component(ResourceComponentKind.Weights,
            ResourceTarget.SystemMemory, 1000, LifecyclePhase.Load);
        ResourceComponent b = Component(ResourceComponentKind.KvCache,
            ResourceTarget.SystemMemory, 500, LifecyclePhase.Load);

        Assert.AreEqual(
            ResourcePhaseComposer.Compose([a, b]).PeakFor(ResourceTarget.SystemMemory),
            ResourcePhaseComposer.Compose([b, a]).PeakFor(ResourceTarget.SystemMemory));
    }

    [TestMethod]
    public void EmptyComponentSet_ProducesZeroPeaks()
    {
        ResourcePeakProfile profile = ResourcePhaseComposer.Compose([]);
        Assert.AreEqual(ByteCount.Zero, profile.PeakFor(ResourceTarget.SystemMemory));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter ResourcePhaseComposerTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the enums**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// A physical pool a requirement is charged against. These are never summed
/// into one capacity figure.
/// </summary>
internal enum ResourceTarget
{
    Unspecified = 0,
    SystemMemory,
    DedicatedDeviceMemory,

    /// <summary>
    /// Integrated-GPU memory carved out of system RAM. It contributes to
    /// system-memory pressure and never adds capacity.
    /// </summary>
    SharedDeviceMemory,
    Storage
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

internal enum LifecyclePhase
{
    Unspecified = 0,
    Load,
    Compile,
    SteadyStateGeneration
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

internal enum ResourceComponentKind
{
    Unspecified = 0,
    Weights,
    KvCache,
    ComputeBuffer,
    BackendAllocation,
    StagingBuffer,
    ModelState,
    ApplicationOverhead,
    PersistentArtifact
}
```

- [ ] **Step 4: Implement the component**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// One estimated memory requirement, charged to exactly one target and
/// present during one or more lifecycle phases.
/// </summary>
internal sealed record ResourceComponent
{
    private ResourceComponent(
        ResourceComponentKind kind,
        ResourceTarget target,
        ByteCount bytes,
        IReadOnlySet<LifecyclePhase> phases)
    {
        Kind = kind;
        Target = target;
        Bytes = bytes;
        Phases = phases;
    }

    internal ResourceComponentKind Kind { get; }

    internal ResourceTarget Target { get; }

    internal ByteCount Bytes { get; }

    internal IReadOnlySet<LifecyclePhase> Phases { get; }

    internal static ResourceComponent Create(
        ResourceComponentKind kind,
        ResourceTarget target,
        ByteCount bytes,
        IReadOnlySet<LifecyclePhase> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);

        if (kind == ResourceComponentKind.Unspecified)
        {
            throw new ArgumentException(
                "A component must declare its kind.", nameof(kind));
        }

        if (target == ResourceTarget.Unspecified)
        {
            throw new ArgumentException(
                "A component must declare the pool it is charged against.", nameof(target));
        }

        if (phases.Count == 0)
        {
            throw new ArgumentException(
                "A component with no phase could never be composed into a peak.",
                nameof(phases));
        }

        if (phases.Contains(LifecyclePhase.Unspecified))
        {
            throw new ArgumentException(
                "A component must not declare an unspecified phase.", nameof(phases));
        }

        // Copy so a later caller mutation cannot change a composed estimate.
        return new ResourceComponent(
            kind, target, bytes, new HashSet<LifecyclePhase>(phases));
    }
}
```

- [ ] **Step 5: Implement the peak profile and composer**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

internal sealed record ResourcePeakProfile
{
    private readonly IReadOnlyDictionary<ResourceTarget, ByteCount> _peaks;

    internal ResourcePeakProfile(
        IReadOnlyDictionary<ResourceTarget, ByteCount> peaks,
        ByteCount systemMemoryPressure)
    {
        _peaks = peaks;
        SystemMemoryPressure = systemMemoryPressure;
    }

    /// <summary>
    /// System memory plus shared device memory, which is carved out of the
    /// same physical RAM. This is the figure the RAM safety gate uses.
    /// </summary>
    internal ByteCount SystemMemoryPressure { get; }

    internal ByteCount PeakFor(ResourceTarget target) =>
        _peaks.TryGetValue(target, out ByteCount peak) ? peak : ByteCount.Zero;
}
```

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;

/// <summary>
/// Composes components into a per-target peak. The peak is the largest
/// phase, not the total of every component, because components that never
/// coexist must not be added together.
/// </summary>
internal static class ResourcePhaseComposer
{
    internal static ResourcePeakProfile Compose(IReadOnlyList<ResourceComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        LifecyclePhase[] phases =
        [
            LifecyclePhase.Load,
            LifecyclePhase.Compile,
            LifecyclePhase.SteadyStateGeneration
        ];

        ResourceTarget[] targets =
        [
            ResourceTarget.SystemMemory,
            ResourceTarget.DedicatedDeviceMemory,
            ResourceTarget.SharedDeviceMemory,
            ResourceTarget.Storage
        ];

        Dictionary<ResourceTarget, ByteCount> peaks = [];

        foreach (ResourceTarget target in targets)
        {
            ByteCount peak = ByteCount.Zero;

            foreach (LifecyclePhase phase in phases)
            {
                ByteCount phaseTotal = ByteCount.Zero;

                foreach (ResourceComponent component in components)
                {
                    if (component.Target == target && component.Phases.Contains(phase))
                    {
                        phaseTotal = phaseTotal.Add(component.Bytes);
                    }
                }

                if (phaseTotal > peak)
                {
                    peak = phaseTotal;
                }
            }

            peaks[target] = peak;
        }

        // Shared device memory lives in system RAM, so it is charged to system
        // pressure exactly once. It is never treated as additional capacity.
        ByteCount pressure = peaks[ResourceTarget.SystemMemory]
            .Add(peaks[ResourceTarget.SharedDeviceMemory]);

        return new ResourcePeakProfile(peaks, pressure);
    }
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter ResourcePhaseComposerTests`
Expected: PASS, 9 tests.

- [ ] **Step 7: Commit**

```bash
git add shared tests
git commit -m "feat(compatibility): compose per-pool resource peaks"
```

---

### Task 4: Safety policy asset with provenance

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/PolicyProvenance.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/FitThresholds.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/SafetyPolicy.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/SafetyPolicyTests.cs`

**Interfaces:**
- Consumes: `ByteCount`.
- Produces: `SafetyPolicy` with `Provenance`, `PolicyVersion`, `OsAllowanceFor(ResourceTarget)`, `OperationalReserveFor(ResourceTarget)`, `CalibrationMarginFor(ByteCount predictedPeak)`, `Thresholds`; and `SafetyPolicy.ProvisionalV1()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class SafetyPolicyTests
{
    [TestMethod]
    public void ProvisionalV1_DeclaresItsProvenanceHonestly()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        Assert.AreEqual(PolicyProvenance.Provisional, policy.Provenance);
        Assert.AreEqual("fit-safety-policy-v1", policy.PolicyVersion);
    }

    [TestMethod]
    public void ProvisionalV1_UsesTheApprovedThresholds()
    {
        FitThresholds thresholds = SafetyPolicy.ProvisionalV1().Thresholds;

        Assert.AreEqual(0.75m, thresholds.ComfortableCeiling);
        Assert.AreEqual(0.90m, thresholds.ModerateHeadroomCeiling);
        Assert.AreEqual(1.00m, thresholds.NarrowCeiling);
    }

    [TestMethod]
    public void CalibrationMargin_IsTheLargerOfFloorAndPercentage()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        // Small peak: the absolute floor dominates.
        ByteCount small = policy.CalibrationMarginFor(ByteCount.FromBytes(100));
        Assert.AreEqual(policy.CalibrationMarginFloor, small);

        // Large peak: the percentage dominates.
        ByteCount large = policy.CalibrationMarginFor(
            ByteCount.FromBytes(100UL * 1024 * 1024 * 1024));
        Assert.IsTrue(large > policy.CalibrationMarginFloor);
    }

    [TestMethod]
    public void CalibrationMargin_IsNeverNegativeOrReducing()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        // A margin that shrank a requirement would create a false-safe result.
        Assert.IsTrue(policy.CalibrationMarginFor(ByteCount.FromBytes(1)) > ByteCount.Zero);
    }

    [TestMethod]
    public void CalibrationMargin_IsMonotonicInPredictedPeak()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        ByteCount smaller = policy.CalibrationMarginFor(ByteCount.FromBytes(1_000_000_000));
        ByteCount larger = policy.CalibrationMarginFor(ByteCount.FromBytes(2_000_000_000));

        Assert.IsTrue(larger >= smaller);
    }

    [TestMethod]
    public void AbsentPolicy_ExposesNoConstants()
    {
        SafetyPolicy policy = SafetyPolicy.Absent();

        Assert.AreEqual(PolicyProvenance.Absent, policy.Provenance);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = policy.Thresholds);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter SafetyPolicyTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement provenance and thresholds**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Where a policy's numbers came from. This is what separates a documented
/// default from a fabricated constant: a provisional value is used, but it
/// is labelled and it caps the evidence grade of every result built on it.
/// </summary>
internal enum PolicyProvenance
{
    Unspecified = 0,

    /// <summary>No values available. Evaluation must return NotEstablished.</summary>
    Absent,

    /// <summary>Documented defaults, not yet validated against measured runs.</summary>
    Provisional,

    /// <summary>Backed by a recorded predicted-versus-measured dataset.</summary>
    Calibrated
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Ratios of required bytes to safe budget that separate the fit states.
/// </summary>
internal sealed record FitThresholds(
    decimal ComfortableCeiling,
    decimal ModerateHeadroomCeiling,
    decimal NarrowCeiling);
```

- [ ] **Step 4: Implement the policy**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Versioned safety numbers. Version one ships provisional values taken from
/// the approved workflow documents; they are not measurements and every
/// result built on them is pinned to the Estimated evidence grade.
/// </summary>
internal sealed record SafetyPolicy
{
    private readonly FitThresholds? _thresholds;

    private SafetyPolicy(
        PolicyProvenance provenance,
        string policyVersion,
        FitThresholds? thresholds,
        ByteCount osAllowance,
        ByteCount operationalReserve,
        ByteCount calibrationMarginFloor,
        decimal calibrationMarginFraction)
    {
        Provenance = provenance;
        PolicyVersion = policyVersion;
        _thresholds = thresholds;
        OsAllowance = osAllowance;
        OperationalReserve = operationalReserve;
        CalibrationMarginFloor = calibrationMarginFloor;
        CalibrationMarginFraction = calibrationMarginFraction;
    }

    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    internal PolicyProvenance Provenance { get; }

    internal string PolicyVersion { get; }

    internal ByteCount OsAllowance { get; }

    internal ByteCount OperationalReserve { get; }

    internal ByteCount CalibrationMarginFloor { get; }

    internal decimal CalibrationMarginFraction { get; }

    internal FitThresholds Thresholds =>
        _thresholds ?? throw new InvalidOperationException(
            "An absent policy exposes no thresholds; evaluation must report NotEstablished.");

    /// <summary>
    /// The margin added to a predicted peak to cover underprediction. It is
    /// always added and never subtracted, so an uncalibrated estimator errs
    /// toward reporting "does not fit" rather than crashing the machine.
    /// </summary>
    internal ByteCount CalibrationMarginFor(ByteCount predictedPeak)
    {
        ulong fromFraction = (ulong)Math.Ceiling(
            predictedPeak.Bytes * CalibrationMarginFraction);

        return fromFraction > CalibrationMarginFloor.Bytes
            ? ByteCount.FromBytes(fromFraction)
            : CalibrationMarginFloor;
    }

    internal static SafetyPolicy ProvisionalV1() => new(
        PolicyProvenance.Provisional,
        policyVersion: "fit-safety-policy-v1",
        thresholds: new FitThresholds(
            ComfortableCeiling: 0.75m,
            ModerateHeadroomCeiling: 0.90m,
            NarrowCeiling: 1.00m),
        osAllowance: ByteCount.FromBytes(2 * Gibibyte),
        operationalReserve: ByteCount.FromBytes(Gibibyte),
        calibrationMarginFloor: ByteCount.FromBytes(Gibibyte / 2),
        calibrationMarginFraction: 0.10m);

    internal static SafetyPolicy Absent() => new(
        PolicyProvenance.Absent,
        policyVersion: "absent",
        thresholds: null,
        osAllowance: ByteCount.Zero,
        operationalReserve: ByteCount.Zero,
        calibrationMarginFloor: ByteCount.Zero,
        calibrationMarginFraction: 0m);
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter SafetyPolicyTests`
Expected: PASS, 6 tests.

- [ ] **Step 6: Commit**

```bash
git add shared tests
git commit -m "feat(compatibility): add versioned safety policy with provenance"
```

---

### Task 5: Safe budget and fit classification

**Files:**
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/CompatibilityFitState.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/FitLimitingReason.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/AvailableResources.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/FitAssessment.cs`
- Create: `shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/FitAssessment/FitPolicy.cs`
- Test: `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/FitPolicyTests.cs`

**Interfaces:**
- Consumes: `ByteCount`, `ResourcePeakProfile`, `SafetyPolicy`.
- Produces: `FitPolicy.Assess(ResourcePeakProfile, AvailableResources, SafetyPolicy)` returning `FitAssessment` with `State`, `LimitingReason`, `SafeBudget`, `RequiredBytes`, `Headroom`, `PressureRatio`.

- [ ] **Step 1: Write the failing tests**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class FitPolicyTests
{
    private const ulong Gib = 1024UL * 1024 * 1024;

    private static ResourcePeakProfile SystemPeak(ulong bytes) =>
        ResourcePhaseComposer.Compose(
        [
            ResourceComponent.Create(
                ResourceComponentKind.Weights,
                ResourceTarget.SystemMemory,
                ByteCount.FromBytes(bytes),
                new HashSet<LifecyclePhase> { LifecyclePhase.SteadyStateGeneration }),
        ]);

    private static AvailableResources Available(ulong systemBytes) =>
        AvailableResources.Create(
            systemMemory: ByteCount.FromBytes(systemBytes),
            dedicatedDeviceMemory: ByteCount.FromBytes(0),
            storage: ByteCount.FromBytes(500 * Gib),
            observedAtUtc: DateTimeOffset.UnixEpoch);

    [TestMethod]
    public void AbsentPolicy_AlwaysNotEstablished_AndInventsNoNumber()
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(64 * Gib), SafetyPolicy.Absent());

        Assert.AreEqual(CompatibilityFitState.NotEstablished, assessment.State);
        Assert.AreEqual(FitLimitingReason.SafetyPolicyUnavailable, assessment.LimitingReason);
    }

    [TestMethod]
    public void SafeBudget_SubtractsAllowanceAndReserveFromFreshAvailable()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(20 * Gib), policy);

        // 20 GiB available - 2 GiB OS allowance - 1 GiB operational reserve.
        Assert.AreEqual(17 * Gib, assessment.SafeBudget.Bytes);
    }

    [TestMethod]
    public void Required_IncludesTheCalibrationMargin()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();
        ByteCount peak = ByteCount.FromBytes(10 * Gib);

        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(peak.Bytes), Available(20 * Gib), policy);

        Assert.AreEqual(
            peak.Add(policy.CalibrationMarginFor(peak)).Bytes,
            assessment.RequiredBytes.Bytes);
    }

    [TestMethod]
    public void ExhaustedBudget_DoesNotFit_RatherThanUnderflowing()
    {
        // Available is below the allowance plus reserve, so the budget is zero.
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), Available(Gib), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.DoesNotFit, assessment.State);
        Assert.AreEqual(ByteCount.Zero, assessment.SafeBudget);
        Assert.AreEqual(ByteCount.Zero, assessment.Headroom);
    }

    [DataTestMethod]
    // budget is 17 GiB. required = peak + max(0.5 GiB, 10% of peak).
    [DataRow(10UL, CompatibilityFitState.Safe)]        // 11.0 / 17 = 0.65 comfortable
    [DataRow(14UL, CompatibilityFitState.Safe)]        // 15.4 / 17 = 0.90 moderate
    [DataRow(15UL, CompatibilityFitState.Narrow)]      // 16.5 / 17 = 0.97 narrow
    [DataRow(17UL, CompatibilityFitState.DoesNotFit)]  // 18.7 / 17 = 1.10 over
    public void State_FollowsTheApprovedThresholdBands(
        ulong peakGib,
        CompatibilityFitState expected)
    {
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(peakGib * Gib), Available(20 * Gib), SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(expected, assessment.State);
    }

    [TestMethod]
    public void EqualToBudget_CountsAsFitting_BecauseMarginsAreAlreadyIncluded()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();

        // Choose a peak whose required total lands exactly on the 17 GiB budget.
        ulong peak = 17 * Gib - (ulong)Math.Ceiling(17 * Gib * 0.10m / 1.10m) - 1;
        FitAssessment atLimit = FitPolicy.Assess(
            SystemPeak(peak), Available(20 * Gib), policy);

        Assert.IsTrue(atLimit.RequiredBytes <= atLimit.SafeBudget);
        Assert.AreNotEqual(CompatibilityFitState.DoesNotFit, atLimit.State);
    }

    [TestMethod]
    public void OneByteOverBudget_DoesNotFit()
    {
        SafetyPolicy policy = SafetyPolicy.ProvisionalV1();
        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(20 * Gib), Available(20 * Gib), policy);

        Assert.AreEqual(CompatibilityFitState.DoesNotFit, assessment.State);
        Assert.AreEqual(FitLimitingReason.InsufficientSystemMemory, assessment.LimitingReason);
    }

    [TestMethod]
    public void StaleAvailability_IsRejectedAsNotEstablished()
    {
        AvailableResources stale = AvailableResources.Create(
            systemMemory: ByteCount.FromBytes(20 * Gib),
            dedicatedDeviceMemory: ByteCount.Zero,
            storage: ByteCount.FromBytes(500 * Gib),
            observedAtUtc: DateTimeOffset.UnixEpoch,
            isFresh: false);

        FitAssessment assessment = FitPolicy.Assess(
            SystemPeak(Gib), stale, SafetyPolicy.ProvisionalV1());

        Assert.AreEqual(CompatibilityFitState.NotEstablished, assessment.State);
        Assert.AreEqual(FitLimitingReason.FreshAvailabilityUnavailable, assessment.LimitingReason);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter FitPolicyTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the state and reason enums**

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

internal enum CompatibilityFitState
{
    Unspecified = 0,
    Safe,
    Narrow,
    DoesNotFit,
    Unsupported,
    NotEstablished
}
```

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

internal enum FitLimitingReason
{
    None = 0,
    InsufficientSystemMemory,
    InsufficientDedicatedDeviceMemory,
    InsufficientStorage,
    ContextExceedsModelLimit,
    EvidenceBelowAdmissionLevel,
    SafetyPolicyUnavailable,
    FreshAvailabilityUnavailable
}
```

- [ ] **Step 4: Implement available resources**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// A capacity observation. Freshness is explicit because a historical
/// availability figure must never satisfy the safety gate.
/// </summary>
internal sealed record AvailableResources
{
    private AvailableResources(
        ByteCount systemMemory,
        ByteCount dedicatedDeviceMemory,
        ByteCount storage,
        DateTimeOffset observedAtUtc,
        bool isFresh)
    {
        SystemMemory = systemMemory;
        DedicatedDeviceMemory = dedicatedDeviceMemory;
        Storage = storage;
        ObservedAtUtc = observedAtUtc;
        IsFresh = isFresh;
    }

    internal ByteCount SystemMemory { get; }

    internal ByteCount DedicatedDeviceMemory { get; }

    internal ByteCount Storage { get; }

    internal DateTimeOffset ObservedAtUtc { get; }

    internal bool IsFresh { get; }

    internal static AvailableResources Create(
        ByteCount systemMemory,
        ByteCount dedicatedDeviceMemory,
        ByteCount storage,
        DateTimeOffset observedAtUtc,
        bool isFresh = true) =>
        new(systemMemory, dedicatedDeviceMemory, storage,
            observedAtUtc.ToUniversalTime(), isFresh);
}
```

- [ ] **Step 5: Implement the assessment and policy**

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

internal sealed record FitAssessment(
    CompatibilityFitState State,
    FitLimitingReason LimitingReason,
    ByteCount SafeBudget,
    ByteCount RequiredBytes,
    ByteCount Headroom,
    decimal PressureRatio);
```

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

/// <summary>
/// Decision 6. Compares a predicted peak against a conservative budget.
/// The bias is one-directional: a false-safe answer crashes the user's
/// machine, so every unknown collapses to NotEstablished and every margin
/// is added to the requirement rather than the budget.
/// </summary>
internal static class FitPolicy
{
    internal static FitAssessment Assess(
        ResourcePeakProfile profile,
        AvailableResources available,
        SafetyPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(policy);

        if (policy.Provenance is PolicyProvenance.Absent or PolicyProvenance.Unspecified)
        {
            return NotEstablished(FitLimitingReason.SafetyPolicyUnavailable);
        }

        if (!available.IsFresh)
        {
            return NotEstablished(FitLimitingReason.FreshAvailabilityUnavailable);
        }

        // Shared device memory is already folded into system pressure.
        ByteCount predictedPeak = profile.SystemMemoryPressure;
        ByteCount required = predictedPeak.Add(policy.CalibrationMarginFor(predictedPeak));

        // An exhausted budget is an expected answer, not an arithmetic error.
        ByteCount budget = ByteCount.Zero;
        if (available.SystemMemory.TrySubtract(policy.OsAllowance, out ByteCount afterOs))
        {
            afterOs.TrySubtract(policy.OperationalReserve, out budget);
        }

        if (budget == ByteCount.Zero)
        {
            return new FitAssessment(
                CompatibilityFitState.DoesNotFit,
                FitLimitingReason.InsufficientSystemMemory,
                ByteCount.Zero,
                required,
                ByteCount.Zero,
                PressureRatio: decimal.MaxValue);
        }

        decimal ratio = required.RatioAgainst(budget);
        _ = budget.TrySubtract(required, out ByteCount headroom);

        FitThresholds thresholds = policy.Thresholds;

        (CompatibilityFitState state, FitLimitingReason reason) = ratio switch
        {
            _ when ratio <= thresholds.ModerateHeadroomCeiling =>
                (CompatibilityFitState.Safe, FitLimitingReason.None),
            _ when ratio <= thresholds.NarrowCeiling =>
                (CompatibilityFitState.Narrow, FitLimitingReason.None),
            _ => (CompatibilityFitState.DoesNotFit,
                  FitLimitingReason.InsufficientSystemMemory)
        };

        return new FitAssessment(state, reason, budget, required, headroom, ratio);
    }

    private static FitAssessment NotEstablished(FitLimitingReason reason) =>
        new(CompatibilityFitState.NotEstablished,
            reason,
            ByteCount.Zero,
            ByteCount.Zero,
            ByteCount.Zero,
            PressureRatio: 0m);
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests --filter FitPolicyTests`
Expected: PASS, 11 test cases.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests`
Expected: PASS, all tests, zero failures, zero skipped.

- [ ] **Step 8: Commit**

```bash
git add shared tests
git commit -m "feat(compatibility): classify fit against a conservative budget"
```

---

## Self-Review

**Spec coverage.** Section 8 arithmetic → Task 1. Section 7 planning context → Task 2. Section 8 targets and phase composition → Task 3. Section 9 provenance and thresholds → Task 4. Section 9 budget, bias and fit states → Task 5. Sections 5, 10, 11, 12, 13 (ports, candidates, modes, presentation, Continue) are M3–M5 and are explicitly out of this plan's scope.

**Placeholder scan.** No TBD, no "add error handling", no "similar to Task N". Every code step carries real code.

**Type consistency.** `ByteCount` members used in Tasks 3–5 all exist in Task 1: `Add`, `TrySubtract`, `RatioAgainst`, `Zero`, `FromBytes`, `Bytes`, comparison operators. `ResourcePeakProfile.SystemMemoryPressure` defined in Task 3 and consumed in Task 5. `SafetyPolicy.CalibrationMarginFor`, `OsAllowance`, `OperationalReserve`, `Thresholds`, `Provenance` defined in Task 4 and consumed in Task 5. `FitThresholds.ModerateHeadroomCeiling` and `NarrowCeiling` are the two used by `FitPolicy`; `ComfortableCeiling` is asserted in Task 4 and is reserved for presentation banding in M5.

**Known follow-up.** `SafetyPolicy` currently returns one allowance and reserve for all targets rather than per-target values; Task 5 only assesses system memory. Dedicated device memory and storage gates arrive with the candidate work in M3, where a candidate first declares a device route.
