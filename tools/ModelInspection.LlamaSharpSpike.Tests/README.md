# LLamaSharp deterministic feasibility tests

**Status:** Verified — `170/170` deterministic tests passed on Windows x64  
**Verification date:** 2026-08-04  
**Successful workflow run:** `30939159409`  
**Test runner:** Microsoft.Testing.Platform through the embedded MSTest runner  
**Tool:** [Model Inspection LLamaSharp feasibility tool](../ModelInspection.LlamaSharpSpike/README.md)  
**Evidence:** [Tier 1 runtime verification](../../docs/testing/evidence/2026-08-04-llamasharp-tier1-verification.md)

## Purpose

This MSTest project verifies deterministic behaviour around the isolated
LLamaSharp feasibility tool without treating a real native model load as a unit
test.

The project references the feasibility console project directly. It does not
reference the WinUI application, and no test in this project requires the 2 GB
Granite model.

## Verified result

```text
Configuration:        Release / win-x64
Category:             Deterministic
Total:                170
Succeeded:            170
Failed:               0
Skipped:              0
Result:               PASS
```

The same workflow also compiled the trusted real-model test assembly without
executing it. Real-model pass claims remain separate from this deterministic
result.

## Test-runner configuration

The repository-level `global.json` selects Microsoft.Testing.Platform. The test
project therefore remains an executable MSTest project with:

```xml
<OutputType>Exe</OutputType>
<EnableMSTestRunner>true</EnableMSTestRunner>
<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
<TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure>
```

## Folder hierarchy

```text
ModelInspection.LlamaSharpSpike.Tests/
├── DependencyPolicy/
├── CommandLine/
├── FileSafety/
├── Metadata/
├── Progress/
├── Evidence/
├── Failures/
├── Support/
├── ModelInspection.LlamaSharpSpike.Tests.csproj
└── README.md
```

Every deterministic test class is marked:

```csharp
[TestCategory("Deterministic")]
```

This category lets normal CI run pure contracts separately from child-process
native integration and trusted real-model scenarios.

## Responsibility boundaries

### Dependency policy

The tests read actual repository files and protect:

- `LLamaSharp` exactly `0.27.0`;
- `LLamaSharp.Backend.Cpu` exactly `0.27.0`;
- no floating or ranged dependency versions;
- no CUDA, Vulkan or TurboQuant dependency in the CPU probe;
- no LLamaSharp or TurboQuant dependency in the WinUI application;
- the mapped llama.cpp commit;
- the distinction between the application runtime and the `b9870` research
  runtime;
- Microsoft.Testing.Platform configuration.

### Command line

The tests cover native-smoke and model-probe defaults, help, every supported
option order, duplicate options, missing values, blank values, Unicode and
space-containing paths, invalid cancellation delays, mutually exclusive
cancellation modes, unknown arguments and null input.

### File safety

The tests cover canonical path collision, `.` and `..` aliases,
relative/absolute aliases, Windows case handling, read-only SHA-256 snapshots,
empty files, missing files, directory inputs, pre-cancellation, cancellation
during hashing, stable path fingerprints and every integrity-comparison field.

### Metadata and chat-template evidence

The tests cover architecture-scoped metadata, missing and unknown
architectures, cross-architecture leakage, malformed and overflowing numbers,
culture-specific values, nullable unavailable fields, chat-template presence,
exact length, Unicode hashing and prevention of unsafe VocabOnly native getter
reintroduction.

### Progress

The tests verify genuine callback fractions, finite bounds, `NaN`, positive and
negative infinity, duplicate handling, concurrent reports, elapsed-time order
and independent snapshots.

### Diagnostics and result finalisation

The tests verify stable `MI-*` mappings for file, native-library, architecture,
model-load and unexpected failures. They also verify that cancellation cannot
conceal changed or unverifiable model integrity.

### Evidence and privacy

The tests verify camel-case JSON, readable enum values, atomic replacement,
preservation of existing evidence after serialization or cancellation failure,
cleanup after file-system write failure, canonical-path redaction, absence of
full chat-template text and exclusion of LLamaSharp/native/XAML types from the
serialized contract graph.

### Process support

The tests verify bounded child-process execution, exact stdout/stderr and exit
code capture, argument preservation, environment variables, caller
cancellation, timeout process-tree termination and process-lifetime observer
cleanup. These tests do not invoke LLamaSharp.

## Important assertion rules

Tests assert the behavioural contract rather than incidental platform wording
or overly specific exception subclasses. For example:

- a failed atomic move may be reported by Windows as `IOException` or
  `UnauthorizedAccessException`;
- a cancelled asynchronous task may throw `TaskCanceledException`, which is a
  valid `OperationCanceledException` subtype;
- command-line tests check stable option names and outcomes rather than complete
  prose sentences.

This keeps the suite strict about behaviour without making it fragile against
irrelevant operating-system or framework details.

## What is not a deterministic unit test

The following must run as contained child-process integration tests:

- native CPU library discovery;
- missing or corrupted native DLL behaviour;
- actual `LLamaWeights.LoadFromFileAsync`;
- Granite architecture recognition;
- real metadata, vocabulary and chat-template availability;
- cancellation while native loading is active;
- disposal after a real load;
- malformed GGUF containment;
- before/after integrity of a real GGUF;
- process-owned socket observations.

A native call must never execute inside this MSTest host because llama.cpp can
terminate the process before managed exception handling runs.

## Command

```powershell
dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.Tests\ModelInspection.LlamaSharpSpike.Tests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --filter "TestCategory=Deterministic" `
    --minimum-expected-tests 170
```

A lower minimum may be useful during focused local development, but the formal
Tier 1 result recorded here executed all 170 deterministic tests.
