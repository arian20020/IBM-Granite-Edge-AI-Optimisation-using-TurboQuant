# ADR-001: Use the officially matched LLamaSharp CPU runtime for the application

**Status:** Accepted  
**Date:** 2026-08-04  
**Decision owner:** Project team  
**Affected feature:** Model Inspection and later GGUF inference  
**Related requirement areas:** model inspection, offline local execution, runtime traceability, model preservation  
**Related development plan:** `Development Plan for Model Inspection.docx`

## Context

The project already has controlled research evidence for standalone upstream
`llama.cpp` tag `b9870`, commit
`2d973636e292ee6f75fadcf08d29cb33511f509f`.

The Model Inspection application feature is implemented in C# and WinUI 3. Its
selected architecture places the native runtime behind `ILlamaModelProbe`, with
`LlamaSharpModelProbe` as the first implementation. LLamaSharp is a managed
wrapper over native `llama.cpp` libraries, so the managed wrapper and native
backend must use a compatible revision.

The latest stable packages selected for the first application integration are:

| Component | Exact identity |
|---|---|
| Managed wrapper | `LLamaSharp` `0.27.0` |
| Native CPU backend package | `LLamaSharp.Backend.Cpu` `0.27.0` |
| LLamaSharp source tag | `v0.27.0` |
| LLamaSharp release commit | `7cbbc45e421d55794d5050d126e0b96511007007` |
| Mapped native `llama.cpp` commit | `3f7c29d318e317b63f54c558bc69803963d7d88c` |
| Initial application runtime identifier | `win-x64` |
| Initial backend type | CPU |

LLamaSharp 0.27.0 release notes map the managed package to the exact
`llama.cpp` commit shown above. The published CPU backend package is therefore
the simplest matched managed/native pair for the first application integration.

## Decision

1. Retain the existing `b9870` upstream campaign as research and comparison
   evidence.
2. Do not describe the `b9870` build as the embedded WinUI application runtime.
3. Use `LLamaSharp` `0.27.0` together with
   `LLamaSharp.Backend.Cpu` `0.27.0` for the first application GGUF runtime.
4. Use that same matched pair for Model Inspection and later in-application
   GGUF inference unless a later ADR explicitly replaces it.
5. Introduce the packages first in an isolated feasibility project. Do not add
   them to the WinUI application project until the feasibility gates pass.
6. Keep all LLamaSharp and native types below the future `ILlamaModelProbe`
   boundary. Page, ViewModel, service, classifier, result and Hardware Fit code
   must use project-owned data contracts instead.

## Why this decision was selected

The selected pair removes the immediate need to maintain a custom LLamaSharp
fork or audit a newer native ABI before the feature has proved which runtime
evidence it can obtain.

It also preserves the existing research campaign rather than invalidating or
repeating it. The research and application tracks answer different questions:

```text
Research track
    standalone llama.cpp b9870
    → benchmark, backend and comparison evidence

Application track
    LLamaSharp 0.27.0
    + LLamaSharp.Backend.Cpu 0.27.0
    + mapped llama.cpp 3f7c29d...
    → C# integration and application-runtime evidence
```

## Alternatives considered

### Adapt LLamaSharp to `b9870`

This would preserve one native revision across the existing research and the
application. It was not selected for the first implementation because it may
require a maintained LLamaSharp fork, shared-library build changes and native
binding/ABI verification before we know whether the managed API exposes the
required lightweight inspection evidence.

### Replace the research runtime and rerun the upstream campaign

This would align every experiment with the application runtime, but it would
repeat a substantial research campaign unnecessarily. The existing campaign
remains valid for its stated research purpose.

### Call `llama.cpp` command-line tools from the WinUI application

This would bypass LLamaSharp but introduce process orchestration, text parsing,
packaging and error-mapping work. The selected architecture calls the runtime
through a managed adapter and keeps worker-process isolation as a possible
future hardening route.

## Consequences

### Positive

- managed and native package versions are officially paired;
- no custom native build is required for the first feasibility work;
- the application can use one runtime pair for inspection and later inference;
- native details remain isolated behind a replaceable adapter boundary;
- the existing `b9870` research remains useful and traceable.

### Costs and limitations

- application-specific LLamaSharp tests must be run separately from the
  `b9870` research campaign;
- a successful `b9870` result does not prove application-runtime compatibility;
- the published CPU backend must be included in dependency, licence and release
  packaging reviews;
- the lightweight inspection depth is still unproven;
- no claim is made yet that Granite 4.1 loads successfully through the selected
  application pair.

## Feasibility gates before WinUI integration

The isolated spike must prove, on Windows x64:

1. the published CPU backend can be discovered and loaded;
2. runtime identity can be recorded;
3. a controlled Granite GGUF can be opened through the matched pair;
4. metadata, vocabulary/tokenizer and chat-template evidence availability is
   known;
5. genuine native progress is captured without inventing percentages;
6. cancellation is reported separately from failure;
7. native resources are disposed after success, cancellation and failure;
8. the selected GGUF remains unchanged;
9. missing or broken runtime infrastructure is not classified as an invalid
   model;
10. a lightweight pre-Hardware-Fit inspection route is demonstrated or its
    absence is explicitly recorded.

## Review triggers

Create a new ADR or supersede this one when:

- either package version changes;
- the mapped `llama.cpp` revision changes;
- a GPU backend is selected;
- a custom native backend or LLamaSharp fork is introduced;
- the application changes from in-process probing to a worker process;
- inspection and inference would otherwise use different native revisions;
- the feasibility spike shows that the selected pair cannot meet the safety or
  evidence requirements.

## Authoritative references

- LLamaSharp NuGet package `0.27.0`
- LLamaSharp CPU backend NuGet package `0.27.0`
- LLamaSharp tag `v0.27.0`
- LLamaSharp release commit `7cbbc45e421d55794d5050d126e0b96511007007`
- mapped `llama.cpp` commit `3f7c29d318e317b63f54c558bc69803963d7d88c`
- project upstream research runtime tag `b9870`, commit
  `2d973636e292ee6f75fadcf08d29cb33511f509f`

## Non-claims

This ADR does not claim that:

- the native backend has already loaded inside the project;
- a Granite model has already passed LLamaSharp inspection;
- `VocabOnly` is sufficient for lightweight inspection;
- progress, cancellation, disposal or file integrity have been verified;
- LLamaSharp is already referenced by the WinUI application;
- the Model Inspection UI is connected to a real runtime service.
