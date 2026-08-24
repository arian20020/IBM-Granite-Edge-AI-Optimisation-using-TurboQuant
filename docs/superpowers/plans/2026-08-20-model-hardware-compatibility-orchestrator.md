# Model/Hardware Compatibility Orchestrator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the engine. Add the five owner seams, the run identity and result contracts, the coordinator that walks the run lifecycle, and the Continue predicate — so the feature produces a complete, honest result end to end without referencing a single owner type.

**Architecture:** Five ports, each shipping exactly one implementation today that returns a typed unavailable result with a stable reason code. A coordinator walks spec section 6's lifecycle in order, taking exactly one fresh-memory observation and sharing it across every candidate so comparisons are fair. Every unavailable seam collapses the run to `NotEstablished` naming what was missing — which is the production behaviour until the owner adapters exist.

**Tech Stack:** C# 12, .NET 8 (`net8.0`), MSTest 4.3.2 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-08-20-model-hardware-compatibility-design.md`, sections 5, 6, 7, 13 and 14.

**Predecessors (complete):** core foundation, candidate vocabulary, GGUF estimator, support matrix and generator, mode selection.

Suite command, referred to below as **the suite command**:

```bash
dotnet test tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/GraniteEdgeAI.ModelHardwareCompatibility.Tests.csproj
```

## Global Constraints

- Target `net8.0`, `Nullable` enabled, `TreatWarningsAsErrors` true. No WinUI, no Windows-only API.
- Types are `internal`; the test project has `InternalsVisibleTo`.
- Every enum reserves a zero member, and the zero member is the safe or unknown value.
- **Unknown is a typed unavailable value, never zero, never a default.**
- **C1 references no owner type.** No `HardwareSnapshot`, no `HardwareInspectionHandoff`, no `ModelInspectionHandoff`. Ports accept and return C1-owned records only. A `using` of any Hardware Inspection or Model Inspection production namespace is a defect.
- **C1 must not implement a Windows memory collector.** `IFreshSystemMemoryProbe` is a seam; its only implementation here returns unavailable.
- **One fresh-memory observation per run**, taken once immediately before the safety gate and shared across every candidate. Probing per candidate would let two candidates be judged against different machines.
- Bias is one-directional: any unavailable seam collapses the run to `NotEstablished` naming what was missing, never to an optimistic result.
- MSTest 4: `[TestMethod]` with `[DataRow]`; `DataTestMethod` fails the build.
- A public test method cannot take an `internal` enum parameter (CS0051) — pass `nameof(...)` strings and `Enum.Parse` in the body.
- No absolute path, filename, model name, hostname, credential, native error or raw tool output in any type, message, finding or test. A new string member on a Core type trips the assembly privacy canary — allowlist with a justification or treat it as a real leak; **never narrow the scan**.

## Design decisions taken in this plan

**The request reaches handoffs through the gateway, not by carrying them.** Spec section 7 records this deviation from Decision 1 and its reason: the handoff types cannot be referenced today and guessing their shape is prohibited. `ICompatibilityInputGateway` owns which handoffs are current and performs the atomic claim. A reflection test asserts the request carries no run id, RAM figure, policy version, progress, `CancellationToken`, UI string, or duplicated model/hardware field.

**Capability projection is a pure function.** `HardwareFacts` reports which devices are present and which backends are verified; projecting that onto the matrix yields an `InstallationState` per entry. Keeping it pure means the generator's admission rules stay testable without a machine.

**Findings carry codes, never text.** Section 14 forbids free-form provider payloads reaching any result. A finding is an enum plus a severity, and presentation owns the wording.

## File Structure

```
shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/
├── Domain/
│   └── CompatibilityRunId.cs                     Task 1
├── Application/
│   ├── Contracts/
│   │   ├── HardwareFacts.cs                      Task 2
│   │   ├── CompatibilityRunRequest.cs            Task 1
│   │   ├── CompatibilityRunOutcome.cs            Task 1
│   │   ├── CompatibilityFinding.cs               Task 1
│   │   ├── PolicyIdentity.cs                     Task 1
│   │   ├── CompatibilityAssessment.cs            Task 3
│   │   └── CompatibilityRunResult.cs             Task 3
│   ├── Ports/
│   │   ├── PortUnavailableReason.cs              Task 2
│   │   ├── ICompatibilityInputGateway.cs         Task 2
│   │   ├── IInspectedModelFactsSource.cs         Task 2
│   │   ├── IHardwareFactsSource.cs               Task 2
│   │   ├── IFreshSystemMemoryProbe.cs            Task 2
│   │   ├── IRuntimeVerificationRunner.cs         Task 2
│   │   └── UnavailablePorts.cs                   Task 2
│   ├── Capabilities/
│   │   └── CapabilityProjection.cs               Task 4
│   ├── ContinuePredicate.cs                      Task 6
│   └── CompatibilityRunCoordinator.cs            Task 5

tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/
├── Application/Contracts/
│   ├── CompatibilityRunRequestTests.cs           Task 1
│   └── CompatibilityRunResultTests.cs            Task 3
├── Application/Ports/UnavailablePortTests.cs     Task 2
├── Application/Capabilities/CapabilityProjectionTests.cs   Task 4
├── Application/CompatibilityRunCoordinatorTests.cs         Task 5
├── Application/ContinuePredicateTests.cs                   Task 6
└── Invariants/RunInvariantTests.cs                         Task 7
```

---

### Task 1: Run identity and request

Spec section 7's request, plus the identity and finding vocabulary a result carries.

**Files:**
- Create: `Domain/CompatibilityRunId.cs`
- Create: `Application/Contracts/CompatibilityRunRequest.cs`
- Create: `Application/Contracts/CompatibilityRunOutcome.cs`
- Create: `Application/Contracts/CompatibilityFinding.cs`
- Create: `Application/Contracts/PolicyIdentity.cs`
- Test: `tests/.../Application/Contracts/CompatibilityRunRequestTests.cs`

**Interfaces:**
- Consumes: `CompatibilityContextRequest` from the completed M1 work.
- Produces: `CompatibilityRunId.New()` / `.From(Guid)` with `Value`; `CompatibilityRunRequest(CompatibilityContextRequest Context)`; `CompatibilityRunOutcome { Unspecified = 0, Completed, NotEstablished, Failed, Cancelled }`; `CompatibilityFindingCode` enum and `CompatibilityFinding(CompatibilityFindingCode Code, FindingSeverity Severity)`; `PolicyIdentity(string PolicyName, string Version, PolicyProvenance Provenance)`. Consumed by Tasks 3, 5 and 7.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Contracts/CompatibilityRunRequestTests.cs`:

```csharp
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Contracts;

[TestClass]
public sealed class CompatibilityRunRequestTests
{
    [TestMethod]
    public void Request_CarriesOnlyTheContext()
    {
        // Spec section 7: the request carries the user's context intent and
        // nothing else. Everything else is resolved through a port or created by
        // the coordinator, so a caller cannot smuggle in a stale RAM figure or a
        // pre-chosen policy version.
        PropertyInfo[] properties = typeof(CompatibilityRunRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.Name != "EqualityContract")
            .ToArray();

        Assert.AreEqual(1, properties.Length);
        Assert.AreEqual("Context", properties[0].Name);
    }

    [TestMethod]
    [DataRow("RunId")]
    [DataRow("Id")]
    [DataRow("AvailableMemory")]
    [DataRow("SystemMemory")]
    [DataRow("PolicyVersion")]
    [DataRow("Progress")]
    [DataRow("CancellationToken")]
    [DataRow("Hardware")]
    [DataRow("Model")]
    [DataRow("Handoff")]
    public void Request_CarriesNoProhibitedMember(string prohibited)
    {
        bool present = typeof(CompatibilityRunRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(property => property.Name.Contains(prohibited, StringComparison.OrdinalIgnoreCase));

        Assert.IsFalse(present, $"The request must not carry {prohibited}.");
    }

    [TestMethod]
    public void RunId_IsUniquePerRun()
    {
        Assert.AreNotEqual(CompatibilityRunId.New().Value, CompatibilityRunId.New().Value);
    }

    [TestMethod]
    public void RunId_RoundTripsAnExplicitValue()
    {
        Guid value = Guid.NewGuid();

        Assert.AreEqual(value, CompatibilityRunId.From(value).Value);
    }

    [TestMethod]
    public void RunId_RejectsAnEmptyValue()
    {
        // An empty id cannot distinguish one run from another, which is what
        // stale-run rejection depends on.
        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityRunId.From(Guid.Empty));
    }

    [TestMethod]
    public void Finding_CarriesACodeAndNoText()
    {
        // Section 14: no free-form payload reaches a result. Presentation owns
        // the wording; the engine owns the code.
        PropertyInfo[] strings = typeof(CompatibilityFinding)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();

        Assert.AreEqual(0, strings.Length);
    }

    [TestMethod]
    public void Finding_RejectsAnUnspecifiedCode()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityFinding.Create(
                CompatibilityFindingCode.Unspecified, FindingSeverity.Information));
    }

    [TestMethod]
    public void Finding_RejectsAnUnspecifiedSeverity()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityFinding.Create(
                CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Unspecified));
    }

    [TestMethod]
    public void PolicyIdentity_RejectsABlankName()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => PolicyIdentity.Create(
                "   ", "v1", GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                    .FitAssessment.PolicyProvenance.Provisional));
    }

    [TestMethod]
    public void PolicyIdentity_RejectsABlankVersion()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => PolicyIdentity.Create(
                "estimator", "  ", GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                    .FitAssessment.PolicyProvenance.Provisional));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command. Expected: build failure — `CompatibilityRunId` not found.

- [ ] **Step 3: Implement the run identity**

Create `Domain/CompatibilityRunId.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// Identifies one compatibility run.
///
/// Stale-run rejection depends on being able to tell one run from another, so an
/// empty value is refused rather than quietly matching everything.
/// </summary>
internal readonly record struct CompatibilityRunId
{
    private CompatibilityRunId(Guid value) => Value = value;

    internal Guid Value { get; }

    internal static CompatibilityRunId New() => new(Guid.NewGuid());

    internal static CompatibilityRunId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "An empty run id cannot distinguish one run from another.", nameof(value));
        }

        return new CompatibilityRunId(value);
    }

    public override string ToString() => Value.ToString("D");
}
```

- [ ] **Step 4: Implement the request and outcome**

Create `Application/Contracts/CompatibilityRunRequest.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// Everything a caller supplies: the user's context intent, and nothing else.
///
/// Recorded deviation, spec section 7. The approved decision has the request
/// carrying both handoff objects directly. Those types cannot be referenced today
/// and guessing their shape is prohibited, so the handoffs are reached through
/// ICompatibilityInputGateway, which owns which handoffs are current and performs
/// the claim. Any future change is confined to this record and one port signature.
///
/// The request deliberately carries no run id, availability figure, policy
/// version, progress sink or cancellation token: those are the coordinator's to
/// create or a port's to resolve, and letting a caller supply one is how a stale
/// number reaches a safety gate.
/// </summary>
internal sealed record CompatibilityRunRequest(CompatibilityContextRequest Context);
```

Create `Application/Contracts/CompatibilityRunOutcome.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// How a run ended. Success requires a value; cancelled and failed must not
/// retain one.
/// </summary>
internal enum CompatibilityRunOutcome
{
    Unspecified = 0,
    Completed,
    NotEstablished,
    Failed,
    Cancelled
}
```

- [ ] **Step 5: Implement findings and policy identity**

Create `Application/Contracts/CompatibilityFinding.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// What a finding is about. Stable codes only — section 14 forbids any free-form
/// payload reaching a result, and presentation owns the wording anyway.
/// </summary>
internal enum CompatibilityFindingCode
{
    Unspecified = 0,

    /// <summary>Mandatory whenever a policy in use is Provisional rather than Calibrated.</summary>
    UncalibratedEstimate,

    ModelFactsUnavailable,
    HardwareFactsUnavailable,
    FreshMemoryUnavailable,
    HandoffClaimFailed,
    SupportMatrixUnavailable,
    NoCandidateGenerated,
    PlanningContextNotEstablished,
    NoSafeConfigurationFound
}

internal enum FindingSeverity
{
    Unspecified = 0,
    Information,
    Warning,
    Blocking
}

/// <summary>
/// One recorded observation about a run, carrying a code rather than a message.
/// </summary>
internal sealed record CompatibilityFinding
{
    private CompatibilityFinding(CompatibilityFindingCode code, FindingSeverity severity)
    {
        Code = code;
        Severity = severity;
    }

    internal CompatibilityFindingCode Code { get; }

    internal FindingSeverity Severity { get; }

    internal static CompatibilityFinding Create(
        CompatibilityFindingCode code,
        FindingSeverity severity)
    {
        if (code == CompatibilityFindingCode.Unspecified)
        {
            throw new ArgumentException(
                "A finding must name what it is about.", nameof(code));
        }

        if (severity == FindingSeverity.Unspecified)
        {
            throw new ArgumentException(
                "A finding must state how much it matters.", nameof(severity));
        }

        return new CompatibilityFinding(code, severity);
    }
}
```

Create `Application/Contracts/PolicyIdentity.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// Which versioned policy produced a number, and how much it is worth.
///
/// A result carries one of these per policy in use so a reader can tell whether
/// the figures rest on measurements or on documented defaults.
/// </summary>
internal sealed record PolicyIdentity
{
    private PolicyIdentity(string policyName, string version, PolicyProvenance provenance)
    {
        PolicyName = policyName;
        Version = version;
        Provenance = provenance;
    }

    internal string PolicyName { get; }

    internal string Version { get; }

    internal PolicyProvenance Provenance { get; }

    internal static PolicyIdentity Create(
        string policyName,
        string version,
        PolicyProvenance provenance)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException(
                "A policy identity must name its policy.", nameof(policyName));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(
                "A policy identity must carry a version, or a reader cannot tell "
                + "which numbers produced this result.",
                nameof(version));
        }

        return new PolicyIdentity(policyName, version, provenance);
    }
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run the suite command. Expected: PASS — every test green.

> **If the privacy canary fails here**, it is because `PolicyIdentity.PolicyName` and `.Version` are new string members. They carry a policy name and a version tag, never user content — allowlist both with that justification. Do not narrow the scan.

- [ ] **Step 7: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Domain/CompatibilityRunId.cs shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/ tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Contracts/CompatibilityRunRequestTests.cs
git commit -m "feat(compatibility): add run identity, request and finding vocabulary"
```

---

### Task 2: The five ports

Spec section 5's seams. Each ships exactly one implementation today, returning a typed unavailable result with a stable reason code — which is what makes the whole engine compile, test and run before any owner publishes a contract.

**Files:**
- Create: `Application/Contracts/HardwareFacts.cs`
- Create: `Application/Ports/PortUnavailableReason.cs`
- Create: `Application/Ports/ICompatibilityInputGateway.cs`
- Create: `Application/Ports/IInspectedModelFactsSource.cs`
- Create: `Application/Ports/IHardwareFactsSource.cs`
- Create: `Application/Ports/IFreshSystemMemoryProbe.cs`
- Create: `Application/Ports/IRuntimeVerificationRunner.cs`
- Create: `Application/Ports/UnavailablePorts.cs`
- Test: `tests/.../Application/Ports/UnavailablePortTests.cs`

**Interfaces:**
- Consumes: `InspectedModelFacts`, `AvailableResources`, `ByteCount`, `DeviceRouteId`, `CompatibilityBackend`.
- Produces: `HardwareFacts.Create(...)`; `PortUnavailableReason`; the five interfaces; `UnavailablePorts.Gateway()` / `.ModelFacts()` / `.HardwareFacts()` / `.MemoryProbe()` / `.VerificationRunner()`. Consumed by Tasks 4, 5 and 7.

> **Why the probe is a separate seam.** The handoff's availability figure is historical; the safety gate needs a reading taken now. Merging the two would make it easy to satisfy the gate with a stale number — the exact false-safe this design exists to prevent. C1 must not implement a Windows memory collector; that fact belongs to another team.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Ports/UnavailablePortTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Ports;

[TestClass]
public sealed class UnavailablePortTests
{
    [TestMethod]
    public void ModelFactsSource_ReturnsUnavailableWithAStableReason()
    {
        ModelFactsResolution resolution = UnavailablePorts.ModelFacts().Resolve("run-1");

        Assert.IsFalse(resolution.IsEstablished);
        Assert.IsNull(resolution.Facts);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), resolution.Reason.ToString());
    }

    [TestMethod]
    public void HardwareFactsSource_ReturnsUnavailableWithAStableReason()
    {
        HardwareFactsResolution resolution = UnavailablePorts.HardwareFacts().Resolve("run-1");

        Assert.IsFalse(resolution.IsEstablished);
        Assert.IsNull(resolution.Facts);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), resolution.Reason.ToString());
    }

    [TestMethod]
    public void MemoryProbe_ReturnsUnavailableRatherThanAZeroReading()
    {
        // A zero reading would be indistinguishable from a machine with no free
        // memory, and the gate would refuse for the wrong reason.
        FreshMemoryReading reading = UnavailablePorts.MemoryProbe().Probe();

        Assert.IsFalse(reading.IsEstablished);
        Assert.IsNull(reading.Resources);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), reading.Reason.ToString());
    }

    [TestMethod]
    public void Gateway_RefusesToClaim()
    {
        HandoffClaim claim = UnavailablePorts.Gateway().Claim();

        Assert.IsFalse(claim.IsClaimed);
        Assert.AreEqual(
            nameof(PortUnavailableReason.AdapterNotImplemented), claim.Reason.ToString());
    }

    [TestMethod]
    public void Gateway_RefusesToCommit()
    {
        Assert.IsFalse(UnavailablePorts.Gateway().Commit(CompatibilityRunId.New()));
    }

    [TestMethod]
    public void Gateway_RollbackIsSafeToCallWhenNothingWasClaimed()
    {
        // Rollback must be callable on the failure path without needing to know
        // whether a claim succeeded, or every caller grows the same conditional.
        UnavailablePorts.Gateway().Rollback();
    }

    [TestMethod]
    public void VerificationRunner_ReportsNotRegistered()
    {
        // Runtime verification is implemented by another team, never by C1.
        VerificationOutcome outcome = UnavailablePorts.VerificationRunner().Verify(
            CompatibilityRunId.New());

        Assert.IsFalse(outcome.IsEstablished);
        Assert.AreEqual(
            nameof(PortUnavailableReason.RunnerNotRegistered), outcome.Reason.ToString());
    }

    [TestMethod]
    public void HardwareFacts_ProjectPresenceAndVerificationSeparately()
    {
        // A device being present is not the same as its backend being verified.
        HardwareFacts facts = HardwareFacts.Create(
            installedSystemMemory: ByteCount.FromBytes(32UL * 1024 * 1024 * 1024),
            installedDedicatedDeviceMemory: ByteCount.FromBytes(8UL * 1024 * 1024 * 1024),
            freeStorage: ByteCount.FromBytes(500UL * 1024 * 1024 * 1024),
            presentDevices: new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
            verifiedBackends: new HashSet<CompatibilityBackend>());

        Assert.IsTrue(facts.PresentDevices.Contains(DeviceRouteId.Cpu));
        Assert.AreEqual(0, facts.VerifiedBackends.Count);
    }

    [TestMethod]
    public void HardwareFacts_CopyItsSetsSoLaterMutationCannotChangeThem()
    {
        HashSet<DeviceRouteId> devices = [DeviceRouteId.Cpu];

        HardwareFacts facts = HardwareFacts.Create(
            ByteCount.FromBytes(1024),
            ByteCount.Zero,
            ByteCount.FromBytes(1024),
            devices,
            new HashSet<CompatibilityBackend>());

        devices.Add(DeviceRouteId.IntelDiscreteGpu);

        Assert.AreEqual(1, facts.PresentDevices.Count);
    }

    [TestMethod]
    public void HardwareFacts_RejectsZeroInstalledSystemMemory()
    {
        // Zero installed RAM is not a machine; it is a failed read.
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => HardwareFacts.Create(
                ByteCount.Zero,
                ByteCount.Zero,
                ByteCount.FromBytes(1024),
                new HashSet<DeviceRouteId>(),
                new HashSet<CompatibilityBackend>()));
    }

    [TestMethod]
    public void EveryUnavailableReason_IsDistinct()
    {
        PortUnavailableReason[] values = Enum.GetValues<PortUnavailableReason>();

        Assert.AreEqual(values.Length, values.Distinct().Count());
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command. Expected: build failure — `UnavailablePorts` not found.

- [ ] **Step 3: Implement the hardware facts record**

Create `Application/Contracts/HardwareFacts.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// The machine facts the engine consumes, owned by C1 and shaped by the
/// calculation rather than by any upstream contract.
///
/// Installed capacity may come from a handoff. Current availability may not —
/// that is what IFreshSystemMemoryProbe is for, and the separation is deliberate.
/// </summary>
internal sealed record HardwareFacts
{
    private HardwareFacts(
        ByteCount installedSystemMemory,
        ByteCount installedDedicatedDeviceMemory,
        ByteCount freeStorage,
        IReadOnlySet<DeviceRouteId> presentDevices,
        IReadOnlySet<CompatibilityBackend> verifiedBackends)
    {
        InstalledSystemMemory = installedSystemMemory;
        InstalledDedicatedDeviceMemory = installedDedicatedDeviceMemory;
        FreeStorage = freeStorage;
        PresentDevices = presentDevices;
        VerifiedBackends = verifiedBackends;
    }

    internal ByteCount InstalledSystemMemory { get; }

    internal ByteCount InstalledDedicatedDeviceMemory { get; }

    internal ByteCount FreeStorage { get; }

    /// <summary>Devices the machine has. Presence is not verification.</summary>
    internal IReadOnlySet<DeviceRouteId> PresentDevices { get; }

    /// <summary>
    /// Backends a check has actually run against and passed. A backend being
    /// installed is not the same as its having been verified, and offering an
    /// unverified backend is how a run fails at launch rather than at planning.
    /// </summary>
    internal IReadOnlySet<CompatibilityBackend> VerifiedBackends { get; }

    internal static HardwareFacts Create(
        ByteCount installedSystemMemory,
        ByteCount installedDedicatedDeviceMemory,
        ByteCount freeStorage,
        IReadOnlySet<DeviceRouteId> presentDevices,
        IReadOnlySet<CompatibilityBackend> verifiedBackends)
    {
        ArgumentNullException.ThrowIfNull(presentDevices);
        ArgumentNullException.ThrowIfNull(verifiedBackends);

        if (installedSystemMemory == ByteCount.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(installedSystemMemory),
                "Zero installed system memory is not a machine; it is a failed read.");
        }

        return new HardwareFacts(
            installedSystemMemory,
            installedDedicatedDeviceMemory,
            freeStorage,
            new HashSet<DeviceRouteId>(presentDevices),
            new HashSet<CompatibilityBackend>(verifiedBackends));
    }
}
```

- [ ] **Step 4: Implement the port contracts**

Create `Application/Ports/PortUnavailableReason.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// Why a seam produced nothing. Stable codes, never text — a reason reaches the
/// user through presentation, and section 14 forbids a provider payload getting
/// that far.
/// </summary>
internal enum PortUnavailableReason
{
    None = 0,

    /// <summary>
    /// No adapter exists yet. This is the shipping state until the owner
    /// contracts are published, and it is why the production page reports that
    /// no compatibility conclusion can be drawn.
    /// </summary>
    AdapterNotImplemented,

    HandoffUnavailable,
    HandoffStale,
    IdentityMismatch,
    RunnerNotRegistered
}
```

Create `Application/Ports/IInspectedModelFactsSource.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>Model facts, or a named reason there are none.</summary>
internal sealed record ModelFactsResolution(
    bool IsEstablished,
    InspectedModelFacts? Facts,
    PortUnavailableReason Reason);

/// <summary>
/// Resolves an inspection identity into C1's own model facts. It never returns an
/// owner type, so this compiles before the Model Inspection handoff exists.
/// </summary>
internal interface IInspectedModelFactsSource
{
    ModelFactsResolution Resolve(string modelInspectionRunId);
}
```

Create `Application/Ports/IHardwareFactsSource.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>Hardware facts, or a named reason there are none.</summary>
internal sealed record HardwareFactsResolution(
    bool IsEstablished,
    HardwareFacts? Facts,
    PortUnavailableReason Reason);

/// <summary>
/// Resolves a hardware run identity into C1's own machine facts. Hardware
/// providers receive no model data; this seam carries facts one way only.
/// </summary>
internal interface IHardwareFactsSource
{
    HardwareFactsResolution Resolve(string productHardwareRunId);
}
```

Create `Application/Ports/IFreshSystemMemoryProbe.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>A reading taken now, or a named reason there is none.</summary>
internal sealed record FreshMemoryReading(
    bool IsEstablished,
    AvailableResources? Resources,
    PortUnavailableReason Reason);

/// <summary>
/// Re-reads available memory at the safety gate.
///
/// Deliberately separate from IHardwareFactsSource: the handoff's availability
/// figure is historical, and the gate needs a reading taken now. Merging them
/// would make it easy to satisfy the gate with a stale number.
///
/// C1 never implements this against Windows. The only implementation here
/// returns unavailable.
/// </summary>
internal interface IFreshSystemMemoryProbe
{
    FreshMemoryReading Probe();
}
```

Create `Application/Ports/ICompatibilityInputGateway.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>The outcome of atomically claiming the paired handoffs.</summary>
internal sealed record HandoffClaim(
    bool IsClaimed,
    string ModelInspectionRunId,
    string ProductHardwareRunId,
    PortUnavailableReason Reason);

/// <summary>
/// Owns which handoffs are current, claims them atomically, rolls back, and
/// commits exactly one transfer when the user confirms.
///
/// The claim is atomic because a run that read one handoff and then had the other
/// replaced underneath it would produce an assessment about two different states
/// of the world.
/// </summary>
internal interface ICompatibilityInputGateway
{
    HandoffClaim Claim();

    /// <summary>Safe to call whether or not a claim succeeded.</summary>
    void Rollback();

    bool Commit(CompatibilityRunId runId);
}
```

Create `Application/Ports/IRuntimeVerificationRunner.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>The outcome of a runtime verification attempt.</summary>
internal sealed record VerificationOutcome(
    bool IsEstablished,
    bool Succeeded,
    PortUnavailableReason Reason);

/// <summary>
/// Runs the verification screens. Implemented by the runtime teams, never by C1 —
/// this feature selects a plan, it does not execute one.
/// </summary>
internal interface IRuntimeVerificationRunner
{
    VerificationOutcome Verify(CompatibilityRunId runId);
}
```

- [ ] **Step 5: Implement the unavailable defaults**

Create `Application/Ports/UnavailablePorts.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

/// <summary>
/// The one implementation each seam ships today: a typed refusal with a stable
/// reason.
///
/// This is what lets the whole engine be built and tested before any owner
/// publishes a contract, and it is why the production page can honestly report
/// that no compatibility conclusion is available rather than showing a guess.
/// </summary>
internal static class UnavailablePorts
{
    internal static IInspectedModelFactsSource ModelFacts() => new UnavailableModelFacts();

    internal static IHardwareFactsSource HardwareFacts() => new UnavailableHardwareFacts();

    internal static IFreshSystemMemoryProbe MemoryProbe() => new UnavailableMemoryProbe();

    internal static ICompatibilityInputGateway Gateway() => new UnavailableGateway();

    internal static IRuntimeVerificationRunner VerificationRunner() =>
        new UnavailableVerificationRunner();

    private sealed class UnavailableModelFacts : IInspectedModelFactsSource
    {
        public ModelFactsResolution Resolve(string modelInspectionRunId) =>
            new(false, null, PortUnavailableReason.AdapterNotImplemented);
    }

    private sealed class UnavailableHardwareFacts : IHardwareFactsSource
    {
        public HardwareFactsResolution Resolve(string productHardwareRunId) =>
            new(false, null, PortUnavailableReason.AdapterNotImplemented);
    }

    private sealed class UnavailableMemoryProbe : IFreshSystemMemoryProbe
    {
        // Returns no reading rather than a zero one: zero would be
        // indistinguishable from a machine with no free memory, and the gate
        // would then refuse for the wrong reason.
        public FreshMemoryReading Probe() =>
            new(false, null, PortUnavailableReason.AdapterNotImplemented);
    }

    private sealed class UnavailableGateway : ICompatibilityInputGateway
    {
        public HandoffClaim Claim() =>
            new(false, string.Empty, string.Empty, PortUnavailableReason.AdapterNotImplemented);

        public void Rollback()
        {
            // Nothing was claimed, so nothing is released. Callers roll back on
            // every failure path without first asking whether a claim succeeded.
        }

        public bool Commit(CompatibilityRunId runId) => false;
    }

    private sealed class UnavailableVerificationRunner : IRuntimeVerificationRunner
    {
        public VerificationOutcome Verify(CompatibilityRunId runId) =>
            new(false, false, PortUnavailableReason.RunnerNotRegistered);
    }
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run the suite command. Expected: PASS — every test green.

> **If the privacy canary fails**, `HandoffClaim.ModelInspectionRunId` / `.ProductHardwareRunId` are the new string members. They carry run identities, not paths or model names — allowlist with that justification.

- [ ] **Step 7: Commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Ports/ shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/HardwareFacts.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Ports/
git commit -m "feat(compatibility): add the five owner seams with typed unavailable defaults"
```

---

### Task 3: Assessment and result

What a completed run hands back. Spec section 7: success requires a value; cancelled and failed must not retain one.

**Files:**
- Create: `Application/Contracts/CompatibilityAssessment.cs`
- Create: `Application/Contracts/CompatibilityRunResult.cs`
- Test: `tests/.../Application/Contracts/CompatibilityRunResultTests.cs`

**Interfaces:**
- Consumes: Task 1's identity/finding/policy types; `EvaluatedCandidate`, `CompatibilityModeSelection` from mode selection; `CandidateFingerprint`.
- Produces: `CompatibilityAssessment.Create(IReadOnlyList<EvaluatedCandidate> evaluated, IReadOnlyList<CompatibilityModeSelection> modes, CandidateFingerprint? baselineFingerprint, bool useCurrentModelAvailable)`; `CompatibilityRunResult.Completed(...)` / `.NotEstablished(...)` / `.Failed(...)` / `.Cancelled(...)`. Consumed by Tasks 5, 6 and 7.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Contracts/CompatibilityRunResultTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Contracts;

[TestClass]
public sealed class CompatibilityRunResultTests
{
    private static readonly DateTimeOffset Started =
        new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Completed =
        new(2026, 8, 20, 12, 0, 5, TimeSpan.Zero);

    private static IReadOnlyList<PolicyIdentity> Policies() =>
    [
        PolicyIdentity.Create("estimator", "estimator-policy-v1", PolicyProvenance.Provisional)
    ];

    private static CompatibilityAssessment EmptyAssessment() =>
        CompatibilityAssessment.Create(
            [],
            [CompatibilityModeSelection.NotEstablished(CompatibilityMode.Automatic)],
            baselineFingerprint: null,
            useCurrentModelAvailable: false);

    [TestMethod]
    public void Completed_CarriesTheAssessment()
    {
        CompatibilityRunResult result = CompatibilityRunResult.Completed(
            CompatibilityRunId.New(), EmptyAssessment(), [], Policies(), Started, Completed);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Completed), result.Outcome.ToString());
        Assert.IsNotNull(result.Assessment);
    }

    [TestMethod]
    public void Cancelled_RetainsNoAssessment()
    {
        // A cancelled run's partial work must not read as a conclusion.
        CompatibilityRunResult result = CompatibilityRunResult.Cancelled(
            CompatibilityRunId.New(), [], Policies(), Started, Completed);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Cancelled), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
    }

    [TestMethod]
    public void Failed_RetainsNoAssessmentAndNamesAFinding()
    {
        CompatibilityRunResult result = CompatibilityRunResult.Failed(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.HandoffClaimFailed, FindingSeverity.Blocking)],
            Policies(),
            Started,
            Completed);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Failed), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.AreEqual(1, result.Findings.Count);
    }

    [TestMethod]
    public void Failed_RejectsAnEmptyFindingList()
    {
        // A failure nobody can explain cannot be shown to a user or acted on.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityRunResult.Failed(
                CompatibilityRunId.New(), [], Policies(), Started, Completed));
    }

    [TestMethod]
    public void NotEstablished_RetainsNoAssessmentAndNamesWhatWasMissing()
    {
        CompatibilityRunResult result = CompatibilityRunResult.NotEstablished(
            CompatibilityRunId.New(),
            [CompatibilityFinding.Create(
                CompatibilityFindingCode.ModelFactsUnavailable, FindingSeverity.Blocking)],
            Policies(),
            Started,
            Completed);

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.AreEqual(1, result.Findings.Count);
    }

    [TestMethod]
    public void NotEstablished_RejectsAnEmptyFindingList()
    {
        // Section 12: screen 06 must list the exact missing evidence. A silent
        // not-established tells the user nothing to act on.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityRunResult.NotEstablished(
                CompatibilityRunId.New(), [], Policies(), Started, Completed));
    }

    [TestMethod]
    public void Result_RejectsCompletionBeforeStart()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => CompatibilityRunResult.Cancelled(
                CompatibilityRunId.New(), [], Policies(), Completed, Started));
    }

    [TestMethod]
    public void Result_CopiesFindingsSoLaterMutationCannotChangeIt()
    {
        List<CompatibilityFinding> findings =
        [
            CompatibilityFinding.Create(
                CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Information)
        ];

        CompatibilityRunResult result = CompatibilityRunResult.Completed(
            CompatibilityRunId.New(), EmptyAssessment(), findings, Policies(), Started, Completed);

        findings.Add(CompatibilityFinding.Create(
            CompatibilityFindingCode.NoSafeConfigurationFound, FindingSeverity.Warning));

        Assert.AreEqual(1, result.Findings.Count);
    }

    [TestMethod]
    public void Result_RequiresAtLeastOnePolicyIdentity()
    {
        // Every figure rests on a versioned policy; a result that names none
        // cannot be audited.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityRunResult.Cancelled(
                CompatibilityRunId.New(), [], [], Started, Completed));
    }

    [TestMethod]
    public void Assessment_RejectsAnEmptyModeList()
    {
        // All four modes are always accounted for, even when unavailable.
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityAssessment.Create(
                [], [], baselineFingerprint: null, useCurrentModelAvailable: false));
    }

    [TestMethod]
    public void Assessment_CopiesItsCollections()
    {
        List<CompatibilityModeSelection> modes =
            [CompatibilityModeSelection.NotEstablished(CompatibilityMode.Automatic)];

        CompatibilityAssessment assessment = CompatibilityAssessment.Create(
            [], modes, baselineFingerprint: null, useCurrentModelAvailable: false);

        modes.Add(CompatibilityModeSelection.NotEstablished(CompatibilityMode.Quality));

        Assert.AreEqual(1, assessment.ModeSelections.Count);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command. Expected: build failure — `CompatibilityAssessment` not found.

- [ ] **Step 3: Implement the assessment**

Create `Application/Contracts/CompatibilityAssessment.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// What a completed run concluded.
///
/// It already carries the four mode selections with their ordered selection
/// factors, so the later "choose optimisation mode" screen is a view over data
/// the engine produced rather than a second round of derivation.
/// </summary>
internal sealed record CompatibilityAssessment
{
    private CompatibilityAssessment(
        IReadOnlyList<EvaluatedCandidate> evaluatedCandidates,
        IReadOnlyList<CompatibilityModeSelection> modeSelections,
        CandidateFingerprint? baselineFingerprint,
        bool useCurrentModelAvailable)
    {
        EvaluatedCandidates = evaluatedCandidates;
        ModeSelections = modeSelections;
        BaselineFingerprint = baselineFingerprint;
        UseCurrentModelAvailable = useCurrentModelAvailable;
    }

    internal IReadOnlyList<EvaluatedCandidate> EvaluatedCandidates { get; }

    internal IReadOnlyList<CompatibilityModeSelection> ModeSelections { get; }

    /// <summary>Null when the as-imported configuration was not among the candidates.</summary>
    internal CandidateFingerprint? BaselineFingerprint { get; }

    internal bool UseCurrentModelAvailable { get; }

    internal static CompatibilityAssessment Create(
        IReadOnlyList<EvaluatedCandidate> evaluatedCandidates,
        IReadOnlyList<CompatibilityModeSelection> modeSelections,
        CandidateFingerprint? baselineFingerprint,
        bool useCurrentModelAvailable)
    {
        ArgumentNullException.ThrowIfNull(evaluatedCandidates);
        ArgumentNullException.ThrowIfNull(modeSelections);

        if (modeSelections.Count == 0)
        {
            throw new ArgumentException(
                "Every mode is always accounted for, even when unavailable; an "
                + "empty list would hide a mode rather than disabling it.",
                nameof(modeSelections));
        }

        return new CompatibilityAssessment(
            [.. evaluatedCandidates],
            [.. modeSelections],
            baselineFingerprint,
            useCurrentModelAvailable);
    }
}
```

- [ ] **Step 4: Implement the result**

Create `Application/Contracts/CompatibilityRunResult.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// One run's immutable outcome.
///
/// Only a completed run carries an assessment. A cancelled or failed run must
/// not retain one: partial work that survived into a result would be read as a
/// conclusion about whether the model runs, which is precisely the claim the run
/// failed to make.
/// </summary>
internal sealed record CompatibilityRunResult
{
    private CompatibilityRunResult(
        CompatibilityRunId runId,
        CompatibilityRunOutcome outcome,
        CompatibilityAssessment? assessment,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        RunId = runId;
        Outcome = outcome;
        Assessment = assessment;
        Findings = findings;
        PolicyIdentities = policyIdentities;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
    }

    internal CompatibilityRunId RunId { get; }

    internal CompatibilityRunOutcome Outcome { get; }

    /// <summary>Present only when the outcome is Completed.</summary>
    internal CompatibilityAssessment? Assessment { get; }

    internal IReadOnlyList<CompatibilityFinding> Findings { get; }

    internal IReadOnlyList<PolicyIdentity> PolicyIdentities { get; }

    internal DateTimeOffset StartedAtUtc { get; }

    internal DateTimeOffset CompletedAtUtc { get; }

    internal static CompatibilityRunResult Completed(
        CompatibilityRunId runId,
        CompatibilityAssessment assessment,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        return Build(
            runId, CompatibilityRunOutcome.Completed, assessment,
            findings, policyIdentities, startedAtUtc, completedAtUtc);
    }

    internal static CompatibilityRunResult NotEstablished(
        CompatibilityRunId runId,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(findings);

        // Screen 06 must list the exact missing evidence, so a silent
        // not-established would leave the user nothing to act on.
        if (findings.Count == 0)
        {
            throw new ArgumentException(
                "A not-established run must name what was missing.", nameof(findings));
        }

        return Build(
            runId, CompatibilityRunOutcome.NotEstablished, assessment: null,
            findings, policyIdentities, startedAtUtc, completedAtUtc);
    }

    internal static CompatibilityRunResult Failed(
        CompatibilityRunId runId,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(findings);

        if (findings.Count == 0)
        {
            throw new ArgumentException(
                "A failure nobody can explain cannot be shown to a user or acted on.",
                nameof(findings));
        }

        return Build(
            runId, CompatibilityRunOutcome.Failed, assessment: null,
            findings, policyIdentities, startedAtUtc, completedAtUtc);
    }

    internal static CompatibilityRunResult Cancelled(
        CompatibilityRunId runId,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc) =>
        Build(
            runId, CompatibilityRunOutcome.Cancelled, assessment: null,
            findings, policyIdentities, startedAtUtc, completedAtUtc);

    private static CompatibilityRunResult Build(
        CompatibilityRunId runId,
        CompatibilityRunOutcome outcome,
        CompatibilityAssessment? assessment,
        IReadOnlyList<CompatibilityFinding> findings,
        IReadOnlyList<PolicyIdentity> policyIdentities,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(policyIdentities);

        if (policyIdentities.Count == 0)
        {
            throw new ArgumentException(
                "Every figure rests on a versioned policy; a result naming none "
                + "cannot be audited.",
                nameof(policyIdentities));
        }

        if (completedAtUtc < startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAtUtc), "A run cannot complete before it started.");
        }

        return new CompatibilityRunResult(
            runId,
            outcome,
            assessment,
            [.. findings],
            [.. policyIdentities],
            startedAtUtc.ToUniversalTime(),
            completedAtUtc.ToUniversalTime());
    }
}
```

- [ ] **Step 5: Run the tests, then commit**

Run the suite command. Expected: PASS.

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Contracts/ tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Contracts/CompatibilityRunResultTests.cs
git commit -m "feat(compatibility): add the assessment and run result"
```

---

### Task 4: Capability projection

Turns machine facts into an installation state per matrix entry, so the generator's admission rules are exercised without a machine.

**Files:**
- Create: `Application/Capabilities/CapabilityProjection.cs`
- Test: `tests/.../Application/Capabilities/CapabilityProjectionTests.cs`

**Interfaces:**
- Consumes: `SupportMatrix`, `CompatibilitySupportEntry`, `HardwareFacts` (Task 2), `InstallationState`.
- Produces: `CapabilityProjection.Project(SupportMatrix matrix, HardwareFacts facts, IReadOnlySet<string> optedInExperimentalEntryIds)` returning `IReadOnlyDictionary<string, InstallationState>`. Consumed by Tasks 5 and 7.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/CapabilityProjectionTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Capabilities;

[TestClass]
public sealed class CapabilityProjectionTests
{
    private static HardwareFacts Facts(
        IReadOnlySet<DeviceRouteId>? devices = null,
        IReadOnlySet<CompatibilityBackend>? backends = null) =>
        HardwareFacts.Create(
            ByteCount.FromBytes(32UL * 1024 * 1024 * 1024),
            ByteCount.FromBytes(8UL * 1024 * 1024 * 1024),
            ByteCount.FromBytes(500UL * 1024 * 1024 * 1024),
            devices ?? new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
            backends ?? new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu });

    [TestMethod]
    public void Project_ReportsInstalledAndVerifiedWhenDeviceAndBackendBothCheckOut()
    {
        IReadOnlyDictionary<string, InstallationState> projected =
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(), Facts(), new HashSet<string>());

        Assert.AreEqual(
            nameof(InstallationState.InstalledAndVerified),
            projected["gguf-cpu-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_ReportsNotInstalledWhenTheDeviceIsAbsent()
    {
        // No discrete GPU present, so nothing that targets one can be offered.
        IReadOnlyDictionary<string, InstallationState> projected =
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(), Facts(), new HashSet<string>());

        Assert.AreEqual(
            nameof(InstallationState.NotInstalled),
            projected["gguf-dgpu-sycl-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_ReportsNotInstalledWhenTheDeviceIsPresentButTheBackendIsUnverified()
    {
        // A device being present is not the same as its backend having passed a
        // check. Treating presence as verification is how a run fails at launch
        // rather than at planning.
        IReadOnlyDictionary<string, InstallationState> projected =
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(),
                Facts(devices: new HashSet<DeviceRouteId>
                {
                    DeviceRouteId.Cpu, DeviceRouteId.IntelDiscreteGpu
                }),
                new HashSet<string>());

        Assert.AreEqual(
            nameof(InstallationState.NotInstalled),
            projected["gguf-dgpu-sycl-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_ReportsVerifiedAndOptedInOnlyForAnOptedInExperimentalEntry()
    {
        HardwareFacts facts = Facts(
            devices: new HashSet<DeviceRouteId>
            {
                DeviceRouteId.Cpu, DeviceRouteId.IntelDiscreteGpu
            },
            backends: new HashSet<CompatibilityBackend>
            {
                CompatibilityBackend.Cpu, CompatibilityBackend.IntelSycl
            });

        IReadOnlyDictionary<string, InstallationState> optedIn =
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(),
                facts,
                new HashSet<string> { "gguf-dgpu-sycl-imported-tq3" });

        IReadOnlyDictionary<string, InstallationState> notOptedIn =
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(), facts, new HashSet<string>());

        Assert.AreEqual(
            nameof(InstallationState.VerifiedAndOptedIn),
            optedIn["gguf-dgpu-sycl-imported-tq3"].ToString());

        Assert.AreEqual(
            nameof(InstallationState.InstalledAndVerified),
            notOptedIn["gguf-dgpu-sycl-imported-tq3"].ToString());
    }

    [TestMethod]
    public void Project_IgnoresAnOptInForANonExperimentalEntry()
    {
        // Opting in to something that needs no opt-in must not upgrade it past
        // the check its own support level demands.
        IReadOnlyDictionary<string, InstallationState> projected =
            CapabilityProjection.Project(
                SupportMatrix.ProvisionalV1(),
                Facts(),
                new HashSet<string> { "gguf-cpu-imported-f16" });

        Assert.AreEqual(
            nameof(InstallationState.InstalledAndVerified),
            projected["gguf-cpu-imported-f16"].ToString());
    }

    [TestMethod]
    public void Project_CoversEveryEntryInTheMatrix()
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        IReadOnlyDictionary<string, InstallationState> projected =
            CapabilityProjection.Project(matrix, Facts(), new HashSet<string>());

        Assert.AreEqual(matrix.Entries.Count, projected.Count);
    }

    [TestMethod]
    public void Project_OverAnAbsentMatrixIsEmptyRatherThanThrowing()
    {
        Assert.AreEqual(
            0,
            CapabilityProjection.Project(
                SupportMatrix.Absent(), Facts(), new HashSet<string>()).Count);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail, then implement**

Create `Application/Capabilities/CapabilityProjection.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;

/// <summary>
/// Projects machine facts onto the matrix, yielding one installation state per
/// entry.
///
/// Pure by design: keeping the projection free of I/O is what lets the
/// generator's admission rules be exercised without a machine, and it keeps the
/// rule "presence is not verification" in one readable place.
/// </summary>
internal static class CapabilityProjection
{
    internal static IReadOnlyDictionary<string, InstallationState> Project(
        SupportMatrix matrix,
        HardwareFacts facts,
        IReadOnlySet<string> optedInExperimentalEntryIds)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(optedInExperimentalEntryIds);

        Dictionary<string, InstallationState> projected = [];

        foreach (CompatibilitySupportEntry entry in matrix.Entries)
        {
            // Both must hold. A device the machine has, whose backend nothing has
            // verified, would fail at launch rather than at planning.
            bool runnable =
                facts.PresentDevices.Contains(entry.Device)
                && facts.VerifiedBackends.Contains(entry.Backend);

            projected[entry.EntryId] = runnable
                ? entry.Level == SupportLevel.Experimental
                    && optedInExperimentalEntryIds.Contains(entry.EntryId)
                        ? InstallationState.VerifiedAndOptedIn
                        : InstallationState.InstalledAndVerified
                : InstallationState.NotInstalled;
        }

        return projected;
    }
}
```

- [ ] **Step 3: Run the tests, then commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/Capabilities/CapabilityProjection.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/Capabilities/CapabilityProjectionTests.cs
git commit -m "feat(compatibility): project machine facts onto the support matrix"
```

---

### Task 5: The run coordinator

Spec section 6's lifecycle, in order, with cancellation between phases and exactly one fresh-memory observation shared across every candidate.

**Files:**
- Create: `Application/CompatibilityRunCoordinator.cs`
- Test: `tests/.../Application/CompatibilityRunCoordinatorTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-4, plus `PlanningContextPolicy`, `CandidateGenerator`, `GgufResourceEstimator`, `ResourcePhaseComposer`, `FitPolicy`, `ModeSelector`.
- Produces: `CompatibilityRunDependencies` record; `CompatibilityRunCoordinator.Execute(CompatibilityRunRequest request, CompatibilityRunDependencies dependencies, CancellationToken cancellationToken)` returning `CompatibilityRunResult`. Consumed by Task 7.

> **The baseline configuration is an input, not a derivation.** What the user currently has is a fact about their machine and their import, and C1 cannot invent it. It sits on the dependencies record. Until the owner adapters exist, callers pass the llama.cpp default — CPU, imported weights, F16 cache, no offload — and that stand-in is recorded as a known follow-up rather than hidden inside the coordinator.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/CompatibilityRunCoordinatorTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class CompatibilityRunCoordinatorTests
{
    private const ulong Gibibyte = 1024UL * 1024 * 1024;

    private sealed class StubGateway(bool claims) : ICompatibilityInputGateway
    {
        internal int RollbackCount { get; private set; }

        public HandoffClaim Claim() => claims
            ? new HandoffClaim(true, "model-run", "hardware-run", PortUnavailableReason.None)
            : new HandoffClaim(
                false, string.Empty, string.Empty, PortUnavailableReason.HandoffUnavailable);

        public void Rollback() => RollbackCount++;

        public bool Commit(CompatibilityRunId runId) => true;
    }

    private sealed class StubModelFacts(InspectedModelFacts? facts) : IInspectedModelFactsSource
    {
        public ModelFactsResolution Resolve(string modelInspectionRunId) => facts is null
            ? new ModelFactsResolution(false, null, PortUnavailableReason.HandoffUnavailable)
            : new ModelFactsResolution(true, facts, PortUnavailableReason.None);
    }

    private sealed class StubHardwareFacts(HardwareFacts? facts) : IHardwareFactsSource
    {
        public HardwareFactsResolution Resolve(string productHardwareRunId) => facts is null
            ? new HardwareFactsResolution(false, null, PortUnavailableReason.HandoffUnavailable)
            : new HardwareFactsResolution(true, facts, PortUnavailableReason.None);
    }

    private sealed class CountingProbe(ulong? availableGibibytes) : IFreshSystemMemoryProbe
    {
        internal int ProbeCount { get; private set; }

        public FreshMemoryReading Probe()
        {
            ProbeCount++;

            return availableGibibytes is not { } gibibytes
                ? new FreshMemoryReading(false, null, PortUnavailableReason.AdapterNotImplemented)
                : new FreshMemoryReading(
                    true,
                    AvailableResources.Create(
                        ByteCount.FromBytes(gibibytes * Gibibyte),
                        ByteCount.FromBytes(8 * Gibibyte),
                        ByteCount.FromBytes(500 * Gibibyte),
                        DateTimeOffset.UtcNow),
                    PortUnavailableReason.None);
        }
    }

    private static InspectedModelFacts ModelFacts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(3 * Gibibyte),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 8192,
            fileType: 15,
            quantisationVersion: 2);

    private static HardwareFacts MachineFacts() =>
        HardwareFacts.Create(
            ByteCount.FromBytes(64 * Gibibyte),
            ByteCount.FromBytes(8 * Gibibyte),
            ByteCount.FromBytes(500 * Gibibyte),
            new HashSet<DeviceRouteId> { DeviceRouteId.Cpu },
            new HashSet<CompatibilityBackend> { CompatibilityBackend.Cpu });

    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    private static CompatibilityRunDependencies Dependencies(
        ICompatibilityInputGateway? gateway = null,
        InspectedModelFacts? modelFacts = null,
        HardwareFacts? machineFacts = null,
        IFreshSystemMemoryProbe? probe = null,
        SupportMatrix? matrix = null) =>
        new(
            gateway ?? new StubGateway(true),
            new StubModelFacts(modelFacts ?? ModelFacts()),
            new StubHardwareFacts(machineFacts ?? MachineFacts()),
            probe ?? new CountingProbe(64),
            matrix ?? SupportMatrix.ProvisionalV1(),
            EstimatorPolicy.ProvisionalV1(),
            SafetyPolicy.ProvisionalV1(),
            new HashSet<string>(),
            TrustedSourceAvailability.None(),
            Baseline(),
            ContextTokenCount.FromTokens(4096),
            TimeProvider.System);

    private static CompatibilityRunResult Run(
        CompatibilityRunDependencies? dependencies = null,
        CancellationToken cancellationToken = default) =>
        CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            dependencies ?? Dependencies(),
            cancellationToken);

    [TestMethod]
    public void Execute_CompletesOnAWorkingMachine()
    {
        CompatibilityRunResult result = Run();

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Completed), result.Outcome.ToString());
        Assert.IsNotNull(result.Assessment);
        Assert.AreEqual(4, result.Assessment!.ModeSelections.Count);
    }

    [TestMethod]
    public void Execute_ProbesFreshMemoryExactlyOncePerRun()
    {
        // Every candidate must be judged against the same observation, or two
        // candidates end up compared against different machines.
        CountingProbe probe = new(64);

        Run(Dependencies(probe: probe));

        Assert.AreEqual(1, probe.ProbeCount);
    }

    [TestMethod]
    public void Execute_FailsAndRollsBackWhenTheClaimIsRefused()
    {
        StubGateway gateway = new(claims: false);

        CompatibilityRunResult result = Run(Dependencies(gateway: gateway));

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Failed), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.AreEqual(1, gateway.RollbackCount);
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.HandoffClaimFailed));
    }

    [TestMethod]
    public void Execute_RollsBackWhenAResolutionFailsAfterASuccessfulClaim()
    {
        // A claimed handoff that is never released would block every later run.
        StubGateway gateway = new(claims: true);

        Run(Dependencies(gateway: gateway, modelFacts: null));

        Assert.AreEqual(1, gateway.RollbackCount);
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenModelFactsAreUnavailable()
    {
        CompatibilityRunResult result = Run(Dependencies(modelFacts: null));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.ModelFactsUnavailable));
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenHardwareFactsAreUnavailable()
    {
        CompatibilityRunResult result = Run(Dependencies(machineFacts: null));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.HardwareFactsUnavailable));
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenTheProbeCannotRead()
    {
        // The shipping state until an adapter exists. A historical figure must
        // never be substituted for a reading taken now.
        CompatibilityRunResult result = Run(Dependencies(probe: new CountingProbe(null)));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.FreshMemoryUnavailable));
    }

    [TestMethod]
    public void Execute_IsNotEstablishedWhenTheMatrixIsAbsent()
    {
        CompatibilityRunResult result = Run(Dependencies(matrix: SupportMatrix.Absent()));

        Assert.AreEqual(
            nameof(CompatibilityRunOutcome.NotEstablished), result.Outcome.ToString());
        Assert.IsTrue(result.Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.SupportMatrixUnavailable));
    }

    [TestMethod]
    public void Execute_WithEveryPortUnavailableIsNotEstablished()
    {
        // The production configuration today: no adapter exists for any seam.
        CompatibilityRunResult result = CompatibilityRunCoordinator.Execute(
            new CompatibilityRunRequest(CompatibilityContextRequest.ApplicationDefault()),
            new CompatibilityRunDependencies(
                UnavailablePorts.Gateway(),
                UnavailablePorts.ModelFacts(),
                UnavailablePorts.HardwareFacts(),
                UnavailablePorts.MemoryProbe(),
                SupportMatrix.ProvisionalV1(),
                EstimatorPolicy.ProvisionalV1(),
                SafetyPolicy.ProvisionalV1(),
                new HashSet<string>(),
                TrustedSourceAvailability.None(),
                Baseline(),
                ContextTokenCount.FromTokens(4096),
                TimeProvider.System),
            CancellationToken.None);

        Assert.AreNotEqual(
            nameof(CompatibilityRunOutcome.Completed), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
        Assert.IsTrue(result.Findings.Count > 0);
    }

    [TestMethod]
    public void Execute_CancelsWithoutAnAssessment()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        CompatibilityRunResult result = Run(cancellationToken: source.Token);

        Assert.AreEqual(nameof(CompatibilityRunOutcome.Cancelled), result.Outcome.ToString());
        Assert.IsNull(result.Assessment);
    }

    [TestMethod]
    public void Execute_RollsBackWhenCancelled()
    {
        using CancellationTokenSource source = new();
        source.Cancel();
        StubGateway gateway = new(claims: true);

        Run(Dependencies(gateway: gateway), source.Token);

        Assert.AreEqual(1, gateway.RollbackCount);
    }

    [TestMethod]
    public void Execute_NamesEveryPolicyItUsed()
    {
        CompatibilityRunResult result = Run();

        Assert.IsTrue(result.PolicyIdentities.Count >= 3);
        Assert.IsTrue(result.PolicyIdentities.Any(policy => policy.Version == "estimator-policy-v1"));
        Assert.IsTrue(result.PolicyIdentities.Any(policy => policy.Version == "fit-safety-policy-v1"));
        Assert.IsTrue(result.PolicyIdentities.Any(policy => policy.Version == "support-matrix-v1"));
    }

    [TestMethod]
    public void Execute_RecordsTheUncalibratedFindingWhileAnyPolicyIsProvisional()
    {
        // Mandatory per section 9: nothing built on documented defaults may be
        // presented as measured.
        Assert.IsTrue(Run().Findings.Any(finding =>
            finding.Code == CompatibilityFindingCode.UncalibratedEstimate));
    }

    [TestMethod]
    public void Execute_GivesEveryRunItsOwnIdentity()
    {
        Assert.AreNotEqual(Run().RunId.Value, Run().RunId.Value);
    }

    [TestMethod]
    public void Execute_CompletesWithinItsOwnTimeWindow()
    {
        CompatibilityRunResult result = Run();

        Assert.IsTrue(result.CompletedAtUtc >= result.StartedAtUtc);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run the suite command. Expected: build failure — `CompatibilityRunCoordinator` not found.

- [ ] **Step 3: Implement the coordinator**

Create `Application/CompatibilityRunCoordinator.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.PlanningContext;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;

/// <summary>
/// Everything a run needs, supplied by the caller.
///
/// BaselineConfiguration is an input rather than a derivation: what the user
/// already has is a fact about their import, and this feature cannot invent it.
/// Until the owner adapters exist, callers pass the llama.cpp default.
/// </summary>
internal sealed record CompatibilityRunDependencies(
    ICompatibilityInputGateway Gateway,
    IInspectedModelFactsSource ModelFactsSource,
    IHardwareFactsSource HardwareFactsSource,
    IFreshSystemMemoryProbe MemoryProbe,
    Capabilities.SupportMatrix Matrix,
    EstimatorPolicy EstimatorPolicy,
    SafetyPolicy SafetyPolicy,
    IReadOnlySet<string> OptedInExperimentalEntryIds,
    TrustedSourceAvailability TrustedSource,
    GgufRouteConfiguration BaselineConfiguration,
    ContextTokenCount BaselineContext,
    TimeProvider Clock);

/// <summary>
/// Walks the run lifecycle in order.
///
/// Two properties matter more than the sequence itself. Fresh memory is probed
/// exactly once and that single observation is shared by every candidate, so no
/// two candidates are judged against different machines. And every unavailable
/// seam collapses the run to NotEstablished naming what was missing — the
/// feature would rather say "I cannot tell you" than produce a number it cannot
/// stand behind.
/// </summary>
internal static class CompatibilityRunCoordinator
{
    internal static CompatibilityRunResult Execute(
        CompatibilityRunRequest request,
        CompatibilityRunDependencies dependencies,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(dependencies);

        CompatibilityRunId runId = CompatibilityRunId.New();
        DateTimeOffset startedAt = dependencies.Clock.GetUtcNow();

        List<CompatibilityFinding> findings = [];
        IReadOnlyList<PolicyIdentity> policies = DescribePolicies(dependencies);

        if (IsProvisional(dependencies))
        {
            findings.Add(CompatibilityFinding.Create(
                CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Warning));
        }

        CompatibilityRunResult Stop(
            CompatibilityRunOutcome outcome, CompatibilityFindingCode? code)
        {
            if (code is { } value)
            {
                findings.Add(CompatibilityFinding.Create(value, FindingSeverity.Blocking));
            }

            DateTimeOffset completedAt = dependencies.Clock.GetUtcNow();

            return outcome switch
            {
                CompatibilityRunOutcome.Cancelled => CompatibilityRunResult.Cancelled(
                    runId, findings, policies, startedAt, completedAt),
                CompatibilityRunOutcome.Failed => CompatibilityRunResult.Failed(
                    runId, findings, policies, startedAt, completedAt),
                _ => CompatibilityRunResult.NotEstablished(
                    runId, findings, policies, startedAt, completedAt)
            };
        }

        HandoffClaim claim = dependencies.Gateway.Claim();

        if (!claim.IsClaimed)
        {
            // Rollback is safe whether or not anything was claimed, so it runs on
            // every exit rather than behind a conditional every caller repeats.
            dependencies.Gateway.Rollback();
            return Stop(
                CompatibilityRunOutcome.Failed, CompatibilityFindingCode.HandoffClaimFailed);
        }

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            ModelFactsResolution model =
                dependencies.ModelFactsSource.Resolve(claim.ModelInspectionRunId);

            if (!model.IsEstablished || model.Facts is null)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.ModelFactsUnavailable);
            }

            HardwareFactsResolution hardware =
                dependencies.HardwareFactsSource.Resolve(claim.ProductHardwareRunId);

            if (!hardware.IsEstablished || hardware.Facts is null)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.HardwareFactsUnavailable);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            PlanningContextResolution context = PlanningContextPolicy.Resolve(
                request.Context, model.Facts.DeclaredContextLimit);

            if (context.Status != PlanningContextStatus.Resolved
                || context.ResolvedTokens is not { } preservationTarget)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.PlanningContextNotEstablished);
            }

            if (dependencies.Matrix.Provenance is PolicyProvenance.Absent
                or PolicyProvenance.Unspecified)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.SupportMatrixUnavailable);
            }

            CandidateGenerationResult generated = CandidateGenerator.Generate(
                CandidateGenerationRequest.Create(
                    dependencies.Matrix,
                    CapabilityProjection.Project(
                        dependencies.Matrix,
                        hardware.Facts,
                        dependencies.OptedInExperimentalEntryIds),
                    model.Facts,
                    dependencies.BaselineConfiguration,
                    dependencies.BaselineContext,
                    preservationTarget,
                    dependencies.TrustedSource));

            if (generated.Candidates.Count == 0)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.NoCandidateGenerated);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Stop(CompatibilityRunOutcome.Cancelled, null);
            }

            // One observation, taken here and shared below. Probing per candidate
            // would let two candidates be judged against different machines.
            FreshMemoryReading reading = dependencies.MemoryProbe.Probe();

            if (!reading.IsEstablished || reading.Resources is null)
            {
                return Stop(
                    CompatibilityRunOutcome.NotEstablished,
                    CompatibilityFindingCode.FreshMemoryUnavailable);
            }

            List<EvaluatedCandidate> evaluated = [];

            foreach (CompatibilityCandidate candidate in generated.Candidates)
            {
                ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                    model.Facts, candidate, dependencies.EstimatorPolicy);

                if (estimate.Status != EstimationStatus.Established)
                {
                    continue;
                }

                ResourcePeakProfile peaks =
                    ResourcePhaseComposer.Compose(estimate.Components);

                evaluated.Add(EvaluatedCandidate.Create(
                    candidate,
                    estimate,
                    peaks,
                    FitPolicy.Assess(peaks, reading.Resources, dependencies.SafetyPolicy),
                    WeightQuantisationMap.FromGgufFileType(
                        model.Facts.FileType, model.Facts.QuantisationVersion),
                    EvidenceGrade.Estimated,
                    PerformanceIndicator.NotEstablished(),
                    peaks.PeakFor(ResourceTarget.Storage)));
            }

            IReadOnlySet<string> evidenceRequiring =
                new HashSet<string>(dependencies.Matrix.Entries
                    .Where(entry => entry.RequiresEvidence)
                    .Select(entry => entry.EntryId));

            ModeSelectionOutcome modes = ModeSelector.SelectAll(
                ModeSelectionRequest.Create(
                    evaluated,
                    evidenceRequiring,
                    preservationTarget,
                    dependencies.BaselineConfiguration));

            if (!modes.Selections.Any(selection =>
                selection.Availability == ModeAvailability.Available))
            {
                findings.Add(CompatibilityFinding.Create(
                    CompatibilityFindingCode.NoSafeConfigurationFound,
                    FindingSeverity.Warning));
            }

            CompatibilityAssessment assessment = CompatibilityAssessment.Create(
                evaluated,
                modes.Selections,
                generated.BaselineIncluded
                    ? generated.Candidates[0].Fingerprint
                    : null,
                modes.UseCurrentModelAvailable);

            return CompatibilityRunResult.Completed(
                runId,
                assessment,
                findings,
                policies,
                startedAt,
                dependencies.Clock.GetUtcNow());
        }
        finally
        {
            // Nothing is committed here. The transfer commits only when the user
            // confirms a choice, which is a separate action on a separate screen.
            dependencies.Gateway.Rollback();
        }
    }

    private static bool IsProvisional(CompatibilityRunDependencies dependencies) =>
        dependencies.EstimatorPolicy.Provenance == PolicyProvenance.Provisional
        || dependencies.SafetyPolicy.Provenance == PolicyProvenance.Provisional
        || dependencies.Matrix.Provenance == PolicyProvenance.Provisional;

    private static IReadOnlyList<PolicyIdentity> DescribePolicies(
        CompatibilityRunDependencies dependencies) =>
    [
        PolicyIdentity.Create(
            "estimator",
            dependencies.EstimatorPolicy.PolicyVersion,
            dependencies.EstimatorPolicy.Provenance),
        PolicyIdentity.Create(
            "safety",
            dependencies.SafetyPolicy.PolicyVersion,
            dependencies.SafetyPolicy.Provenance),
        PolicyIdentity.Create(
            "support-matrix",
            dependencies.Matrix.MatrixVersion,
            dependencies.Matrix.Provenance)
    ];
}
```

- [ ] **Step 4: Run the tests, then commit**

Run the suite command. Expected: PASS.

> If the privacy canary fires on `PolicyIdentity.PolicyName` or the claim's run-id strings, allowlist them with the justification that they carry policy names and run identities, never user content.

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/CompatibilityRunCoordinator.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/CompatibilityRunCoordinatorTests.cs
git commit -m "feat(compatibility): walk the run lifecycle end to end"
```

---

### Task 6: The Continue predicate

Spec section 13. Six conditions plus the hardware outcome; any false or unknown condition leaves Continue visible-disabled.

**Files:**
- Create: `Application/ContinuePredicate.cs`
- Test: `tests/.../Application/ContinuePredicateTests.cs`

**Interfaces:**
- Produces: `HardwareOutcome { Unknown = 0, Completed, CompletedWithWarnings, Failed }`; `ContinueConditions` record; `ContinuePredicate.IsEnabled(HardwareOutcome outcome, ContinueConditions conditions)` returning `bool`.

- [ ] **Step 1: Write the failing tests**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ContinuePredicateTests.cs`:

```csharp
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class ContinuePredicateTests
{
    private static ContinueConditions AllTrue() => new(
        HasCurrentValidModelHandoff: true,
        HasCurrentUsableHardwareHandoff: true,
        HasRegisteredAvailableRoute: true,
        HandoffsBoundToCurrentIdentities: true,
        ModelHandoffIsFresh: true,
        NavigationTransactionCompleted: true);

    [TestMethod]
    [DataRow(nameof(HardwareOutcome.Completed), true)]
    [DataRow(nameof(HardwareOutcome.CompletedWithWarnings), true)]
    [DataRow(nameof(HardwareOutcome.Failed), false)]
    [DataRow(nameof(HardwareOutcome.Unknown), false)]
    public void IsEnabled_RequiresAUsableHardwareOutcome(string outcome, bool expected)
    {
        Assert.AreEqual(
            expected,
            ContinuePredicate.IsEnabled(
                Enum.Parse<HardwareOutcome>(outcome), AllTrue()));
    }

    [TestMethod]
    public void IsEnabled_WhenEveryConditionHolds()
    {
        Assert.IsTrue(
            ContinuePredicate.IsEnabled(HardwareOutcome.Completed, AllTrue()));
    }

    [TestMethod]
    [DataRow("HasCurrentValidModelHandoff")]
    [DataRow("HasCurrentUsableHardwareHandoff")]
    [DataRow("HasRegisteredAvailableRoute")]
    [DataRow("HandoffsBoundToCurrentIdentities")]
    [DataRow("ModelHandoffIsFresh")]
    [DataRow("NavigationTransactionCompleted")]
    public void IsEnabled_IsFalseWhenAnySingleConditionFails(string condition)
    {
        // Every condition is individually load-bearing. A predicate that passed
        // with one of these false would enable a step the user cannot complete.
        ContinueConditions conditions = condition switch
        {
            "HasCurrentValidModelHandoff" =>
                AllTrue() with { HasCurrentValidModelHandoff = false },
            "HasCurrentUsableHardwareHandoff" =>
                AllTrue() with { HasCurrentUsableHardwareHandoff = false },
            "HasRegisteredAvailableRoute" =>
                AllTrue() with { HasRegisteredAvailableRoute = false },
            "HandoffsBoundToCurrentIdentities" =>
                AllTrue() with { HandoffsBoundToCurrentIdentities = false },
            "ModelHandoffIsFresh" =>
                AllTrue() with { ModelHandoffIsFresh = false },
            "NavigationTransactionCompleted" =>
                AllTrue() with { NavigationTransactionCompleted = false },
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };

        Assert.IsFalse(ContinuePredicate.IsEnabled(HardwareOutcome.Completed, conditions));
    }

    [TestMethod]
    public void IsEnabled_IsFalseWhenEverythingIsFalse()
    {
        Assert.IsFalse(ContinuePredicate.IsEnabled(
            HardwareOutcome.Unknown,
            new ContinueConditions(false, false, false, false, false, false)));
    }
}
```

- [ ] **Step 2: Implement**

Create `Application/ContinuePredicate.cs`:

```csharp
namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application;

/// <summary>
/// How the preceding hardware step ended. Unknown is the zero value so an
/// unread outcome cannot enable anything.
/// </summary>
internal enum HardwareOutcome
{
    Unknown = 0,
    Completed,
    CompletedWithWarnings,
    Failed
}

/// <summary>
/// The six conditions spec section 13 requires. Each is a plain answered
/// question; an unanswered one is false, because the predicate must never treat
/// "we did not check" as "it is fine".
/// </summary>
internal sealed record ContinueConditions(
    bool HasCurrentValidModelHandoff,
    bool HasCurrentUsableHardwareHandoff,
    bool HasRegisteredAvailableRoute,
    bool HandoffsBoundToCurrentIdentities,

    /// <summary>
    /// False when the model handoff is stale, superseded, expired, consumed,
    /// ambiguously reissued, or associated with a failed rollback.
    /// </summary>
    bool ModelHandoffIsFresh,

    /// <summary>
    /// False when the navigation transaction is pending, failed, rolled back,
    /// duplicated or ambiguous.
    /// </summary>
    bool NavigationTransactionCompleted);

/// <summary>
/// Decides whether Continue is enabled.
///
/// Every condition is a conjunct: any false or unknown answer leaves Continue
/// visible-disabled rather than hidden, so the user can see the step exists and
/// be told why it is not yet available.
/// </summary>
internal static class ContinuePredicate
{
    internal static bool IsEnabled(HardwareOutcome outcome, ContinueConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        if (outcome is not (HardwareOutcome.Completed
            or HardwareOutcome.CompletedWithWarnings))
        {
            return false;
        }

        return conditions.HasCurrentValidModelHandoff
            && conditions.HasCurrentUsableHardwareHandoff
            && conditions.HasRegisteredAvailableRoute
            && conditions.HandoffsBoundToCurrentIdentities
            && conditions.ModelHandoffIsFresh
            && conditions.NavigationTransactionCompleted;
    }
}
```

- [ ] **Step 3: Run the tests, then commit**

```bash
git add shared/GraniteEdgeAI.ModelHardwareCompatibility.Core/Application/ContinuePredicate.cs tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Application/ContinuePredicateTests.cs
git commit -m "feat(compatibility): add the Continue predicate"
```

---

### Task 7: Run invariants

**Files:**
- Create: `tests/.../Invariants/RunInvariantTests.cs`

- [ ] **Step 1: Write the invariants**

Create `tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/RunInvariantTests.cs`:

```csharp
using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties of a run that must hold however the lifecycle is later extended.
/// </summary>
[TestClass]
public sealed class RunInvariantTests
{
    [TestMethod]
    public void EveryPortShipsAnUnavailableImplementation()
    {
        // The whole engine is buildable and testable before any owner publishes
        // a contract precisely because each seam has a typed refusal.
        Assert.IsNotNull(UnavailablePorts.Gateway());
        Assert.IsNotNull(UnavailablePorts.ModelFacts());
        Assert.IsNotNull(UnavailablePorts.HardwareFacts());
        Assert.IsNotNull(UnavailablePorts.MemoryProbe());
        Assert.IsNotNull(UnavailablePorts.VerificationRunner());
    }

    [TestMethod]
    public void NoUnavailablePortReportsSuccess()
    {
        Assert.IsFalse(UnavailablePorts.Gateway().Claim().IsClaimed);
        Assert.IsFalse(UnavailablePorts.ModelFacts().Resolve("x").IsEstablished);
        Assert.IsFalse(UnavailablePorts.HardwareFacts().Resolve("x").IsEstablished);
        Assert.IsFalse(UnavailablePorts.MemoryProbe().Probe().IsEstablished);
    }

    [TestMethod]
    public void TheCoreReferencesNoOwnerFeatureType()
    {
        // This feature owns its own calculation records precisely so it compiles
        // before Hardware Inspection and Model Inspection publish anything.
        string[] forbidden = ["HardwareInspection", "ModelInspection"];

        string[] offenders = typeof(CompatibilityRunResult).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbidden.Any(f => name.Contains(f, StringComparison.Ordinal)))
            .ToArray();

        Assert.AreEqual(
            0,
            offenders.Length,
            "The compatibility core must not reference an owner feature assembly: "
            + string.Join(", ", offenders));
    }

    [TestMethod]
    public void OnlyACompletedResultCanCarryAnAssessment()
    {
        // Reflection over the factories would not prove this; the type's own
        // construction paths do. Cancelled, Failed and NotEstablished each pass
        // null, and there is no setter.
        PropertyInfo assessment = typeof(CompatibilityRunResult)
            .GetProperty(
                "Assessment",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        Assert.IsFalse(assessment.CanWrite, "Assessment must not be settable after construction.");
    }
}
```

- [ ] **Step 2: Run the whole suite, then commit**

```bash
git add tests/UnitTests/GraniteEdgeAI.ModelHardwareCompatibility.Tests/Invariants/RunInvariantTests.cs
git commit -m "test(compatibility): pin the run invariants"
```

---

## Self-Review

**Spec coverage.** Section 5's five seams → Task 2, each with a typed unavailable default. Section 6's lifecycle → Task 5, in the spec's order, with one probe shared across candidates. Section 7's request, outcome and result → Tasks 1 and 3, including the reflection test that the request carries no prohibited member and the rule that only a completed run carries a value. Section 13's Continue predicate → Task 6, all six conditions plus the hardware outcome. Section 14 privacy → findings carry codes not text, and the assembly-reference invariant in Task 7.

**Deliberately deferred.** `IRuntimeVerificationRunner` is declared and defaulted but never called: verification is screens 07–09 and belongs to the runtime teams. `CompatibilityRunDependencies.BaselineConfiguration` is a caller-supplied stand-in until an adapter can report what was actually imported. Progress reporting is not modelled; the request deliberately carries no progress sink.

**Placeholder scan.** No TBD, no "add validation", no "similar to Task N". Every code step carries complete code.

**Type consistency.** `CandidateGenerationRequest.Create` and `ModeSelectionRequest.Create` are the factory forms introduced by the review fixes, not the older positional constructors. `PlanningContextPolicy.Resolve(request, declaredLimit)` matches the M1 signature. `EvaluatedCandidate.Create` takes the eight arguments mode selection defined. `FitPolicy.Assess(peaks, available, safetyPolicy)` matches M2.

**Known follow-up.** The coordinator's `finally` always rolls back and never commits, because commit belongs to the user's confirmation on a later screen; when that screen exists it calls `Gateway.Commit` itself. A run whose candidates all fail estimation currently completes with an empty evaluated set and four unavailable modes rather than reporting `NoCandidateGenerated` — worth revisiting when a second route exists.
