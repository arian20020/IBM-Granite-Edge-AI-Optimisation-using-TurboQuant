# LLamaSharp probe process test support

**Status:** Shared non-shipping test infrastructure  
**Parent:** [Engineering tools](../README.md)

## Purpose

This project lets integration tests execute LLamaSharp and llama.cpp outside the
MSTest host.

The boundary is necessary because native code may abort the process before C#
exception handling or `finally` blocks run. A malformed model or native DLL is
therefore observed through a child-process result instead of being allowed to
terminate the complete test suite.

## Architecture

```text
MSTest process
    ↓
ProbeProcessRunner
    ↓
published feasibility executable
    ↓
LLamaSharp / llama.cpp
```

The parent retains:

- termination kind;
- exit code;
- bounded duration;
- standard output;
- standard error;
- child process identifier;
- structured JSON evidence when the child created it.

## File responsibilities

| File | Responsibility |
|---|---|
| `ProcessTerminationKind.cs` | Distinguishes normal exit, timeout, and start failure |
| `ProbeProcessRequest.cs` | Defines executable, argument, environment, working-directory, and timeout inputs |
| `ProbeExecutionResult.cs` | Returns ordinary parent-process observations |
| `ProbeProcessRunner.cs` | Runs a child, captures streams, applies timeout, and kills the complete process tree |
| `PublishedProbeLocation.cs` | Resolves the explicitly published feasibility executable |
| `TemporaryProbeSandbox.cs` | Copies a publish into a disposable directory for destructive DLL tests |
| `EvidenceAssertions.cs` | Provides MSTest-independent JSON, privacy, and artifact assertions |

## Safety rules

- `UseShellExecute` is disabled.
- Arguments use `ProcessStartInfo.ArgumentList`; shell command construction is
  not used.
- Standard output and error are redirected asynchronously.
- Every request has a positive finite timeout.
- Timeout kills the complete child process tree.
- Caller cancellation also kills the child tree before propagating.
- Destructive native-DLL tests use a copied sandbox, never the source publish.
- This project contains no LLamaSharp package reference and no production code.
- Assertion helpers throw ordinary exceptions so both hosted and trusted test
  projects can use them.

## Environment contract

Integration tests supply:

```text
LLAMASHARP_SPIKE_PUBLISH_DIR
```

The directory must contain:

```text
GraniteEdgeAI.ModelInspection.LlamaSharpSpike.exe
```

A missing directory or executable is a fail-fast configuration error, not a
skipped test.
