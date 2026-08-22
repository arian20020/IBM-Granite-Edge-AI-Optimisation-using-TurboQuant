# LLamaSharp Feasibility Spike Design

**Status:** Approved for staged implementation  
**Date:** 2026-08-04  
**Target branch:** `feature/model-inspection`  
**Runtime decision:** [ADR-001](../../architecture/decisions/ADR-001-llamasharp-application-runtime.md)

## Purpose

Before the WinUI Model Inspection page is connected to native runtime code, the
project must prove what the selected matched LLamaSharp/`llama.cpp` pair can do
safely and repeatably.

The spike is an isolated console tool. It exists to answer feasibility
questions and capture evidence. It is not the production
`LlamaSharpModelProbe`, and it is not referenced by the WinUI application.

## Selected runtime pair

```text
LLamaSharp 0.27.0
        ↓
LLamaSharp.Backend.Cpu 0.27.0
        ↓
llama.cpp 3f7c29d318e317b63f54c558bc69803963d7d88c
```

The standalone upstream `b9870` campaign remains research evidence and is kept
separate from application-runtime claims.

## Architectural boundary

```text
Current spike

ModelInspection.LlamaSharpSpike
        ↓
LLamaSharp native-library configuration
        ↓
matched published CPU backend
        ↓
structured local evidence

Future production

ModelInspectionViewModel
        ↓
IModelInspectionService
        ↓
ILlamaModelProbe
        ↓
LlamaSharpModelProbe
        ↓
matched published CPU backend
```

No spike type, LLamaSharp type or native handle may become part of a page,
ViewModel, service or classifier contract.

## Staged delivery

### Slice 1 — Native backend smoke

Prove only that:

- the exact packages restore;
- the managed project compiles;
- LLamaSharp can discover and dry-run the CPU native backend;
- CUDA and Vulkan selection are disabled for this CPU gate;
- selected backend metadata and native-loader logs can be written as JSON;
- runtime failure produces a non-model operational result;
- the WinUI project is unchanged.

This slice does not require a model file.

### Slice 2 — Controlled GGUF request and integrity boundary

Add:

- `--model <path>`;
- file existence/readability checks;
- pre-inspection file length, last-write time and SHA-256;
- post-inspection integrity comparison;
- an explicit controlled-model manifest entry;
- rejection of unsupported command-line combinations.

### Slice 3 — High-level lightweight probe

Use LLamaSharp `ModelParams` with `VocabOnly = true` and
`LLamaWeights.LoadFromFileAsync` to determine:

- whether a real controlled Granite GGUF opens;
- what metadata remains available;
- whether vocabulary and chat-template evidence is accessible;
- actual memory and duration;
- genuine load progress;
- cancellation behavior;
- deterministic disposal.

### Slice 4 — Lightweight-depth decision

Compare the evidence requirements with the `VocabOnly` result.

```text
Sufficient
    → production adapter can use the supported high-level path

Insufficient
    → investigate a lower-level no-allocation route behind ILlamaModelProbe

Unsafe or unavailable
    → stop before WinUI integration and revise the runtime design
```

### Slice 5 — Production adapter plan

Only after the spike gates pass, write the implementation plan for:

- project-owned runtime contracts;
- `ILlamaModelProbe`;
- `LlamaSharpModelProbe`;
- progress and cancellation mapping;
- operational failure conversion;
- runtime identity recording;
- service and ViewModel integration.

## Initial folder structure

```text
tools/
├── README.md
├── ModelInspection.LlamaSharpSpike/
│   ├── ModelInspection.LlamaSharpSpike.csproj
│   ├── Program.cs
│   ├── PinnedApplicationRuntime.cs
│   ├── SpikeOptionsParser.cs
│   ├── NativeBackendSmokeProbe.cs
│   ├── NativeBackendSmokeResult.cs
│   ├── SmokeEvidenceWriter.cs
│   └── README.md
└── ModelInspection.LlamaSharpSpike.Tests/
    ├── ModelInspection.LlamaSharpSpike.Tests.csproj
    ├── PinnedApplicationRuntimeTests.cs
    ├── SpikeOptionsParserTests.cs
    └── SmokeEvidenceWriterTests.cs
```

The projects are deliberately not added to the main WinUI solution in the
first slice. They are restored, built, tested and run independently so the
native dependency cannot silently become part of the application package.

## Slice 1 command contract

```text
ModelInspection.LlamaSharpSpike
    [--output <json-path>]
    [--help]
```

Default output:

```text
artifacts/model-inspection/llamasharp/runtime-smoke.json
```

`artifacts/` is ignored by Git. Formal evidence will later be copied into a
controlled run directory only after the target-machine run is reviewed.

## Slice 1 evidence contract

The JSON result records:

- schema version;
- start and completion times;
- success/failure;
- exact managed package and backend package versions;
- LLamaSharp source tag and release commit;
- expected native `llama.cpp` commit;
- intended production runtime identifier;
- actual process architecture, operating system and .NET framework;
- selected native library implementation type;
- native library name, AVX level, CUDA flag and Vulkan flag where available;
- native loader logs;
- controlled failure code and message.

The result must not contain:

- native pointers;
- native handles;
- model contents;
- environment secrets;
- arbitrary exception object serialization.

## Failure categories

Slice 1 uses operational codes only:

```text
MI-OP-RUNTIME-UNAVAILABLE
MI-OP-RUNTIME-INITIALISATION-FAILED
MI-OP-EVIDENCE-WRITE-FAILED
MI-OP-INVALID-SPIKE-ARGUMENTS
```

These failures must never be labelled as an invalid or unsupported model,
because no model has been inspected in Slice 1.

## Security and safety rules

- Use only published packages pinned to exact versions.
- Disable CUDA and Vulkan selection for the first CPU gate.
- Do not download native libraries at runtime.
- Do not accept a model path in Slice 1.
- Write evidence locally and atomically.
- Sanitize recorded exception output to type and message.
- Keep runtime logs bounded by the normal single dry-run operation.
- Do not modify the WinUI project file.
- Do not claim a successful Windows x64 run until it has executed on the target
  Windows machine.

## Test strategy

### Unit tests

Verify:

- exact runtime pins;
- the research and application runtime identities remain distinct;
- default command-line options;
- explicit output path parsing;
- help parsing;
- rejection of unknown or incomplete arguments;
- JSON evidence writing and reading;
- atomic overwrite behavior.

### Integration verification

Run the console tool once on the Windows x64 target and verify:

- exit code `0` for a successful dry run;
- exit code `1` for a controlled runtime failure;
- exit code `2` for invalid arguments;
- the selected backend is CPU and not CUDA/Vulkan;
- the JSON file exists and is parseable;
- package/native logs identify the loading path;
- no application source or package reference changed.

## Acceptance criteria for Slice 1

1. Exact package versions are present only in the spike project.
2. Unit tests pass.
3. The spike project builds in Release.
4. A Windows x64 run writes one successful JSON result.
5. The result records the expected mapped `llama.cpp` commit.
6. The selected backend metadata does not report CUDA or Vulkan.
7. The application project file has no LLamaSharp package reference.
8. The licence register contains the selected dependency pair.
9. The Model Inspection README links to the spike and preserves runtime
   non-claims.
10. No model-inspection outcome is produced by this native-library-only gate.

## Non-goals

This design does not yet implement:

- model loading;
- `VocabOnly` probing;
- tensor validation;
- tokenizer or chat-template classification;
- native progress from model loading;
- cancellation of model loading;
- memory measurement;
- file hash comparison;
- production `ILlamaModelProbe`;
- application service, classifier or ViewModel integration;
- OpenVINO inspection;
- GPU runtime selection.
