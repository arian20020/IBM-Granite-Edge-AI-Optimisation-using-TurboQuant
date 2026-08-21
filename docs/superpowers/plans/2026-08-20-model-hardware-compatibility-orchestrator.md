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
