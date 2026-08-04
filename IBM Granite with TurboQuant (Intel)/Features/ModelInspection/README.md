# Model Inspection architecture

**Status:** Living current-state documentation  
**Last reviewed:** 2026-08-04  
**Current branch:** `feature/model-inspection`  
**Reviewed application baseline:** `2c51bb0551cb5556e422e63c19888c1f3874d0e5`

[← Application feature architecture](../README.md)

## Purpose

Model Inspection is onboarding stage two. It is intended to inspect a model
package before Hardware Fit is checked, explain what was found, and eventually
produce one controlled application outcome.

The application currently implements navigation, page composition, reusable
cards and the initial five-stage presentation. The native feasibility work is
kept in an isolated console tool. That tool now contains both:

1. a matched LLamaSharp CPU native-library smoke; and
2. a read-only `VocabOnly` GGUF probe.

The WinUI page is **not yet connected** to that tool or to a production runtime
service.

The approved feature architecture remains the layered design from the
Model Inspection development plan: Page → ViewModel → inspection service →
runtime probe → LLamaSharp/llama.cpp. The feasibility tool exists to prove the
lowest runtime assumptions before the service and ViewModel are built.

## Architecture decisions

- [ADR-001 — matched LLamaSharp application runtime](../../../docs/architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002 — core inspection versus backend verification](../../../docs/architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)

ADR-002 defines the meaning of this onboarding stage:

> Model Inspection establishes lightweight **core runtime compatibility**. It
> does not prove Vulkan, GPU offload, Hardware Fit, context allocation,
> TurboQuant activation or inference.

## Current status

| Capability | Current status |
|---|---|
| Navigate from validated Model Import | Implemented |
| Receive and retain the selected path | Implemented |
| Move onboarding indicator to Inspect Model | Implemented |
| Compose outcome, model, content and action controls | Implemented |
| Display initial selected-model summary | Implemented |
| Display approved five-stage tracker | Implemented through a focused factory |
| Protect stage wording and backend separation | Test source implemented; Windows execution pending |
| Select Progress versus Findings templates | Implemented with WinUI bootstrap handling |
| Matched LLamaSharp CPU native-smoke source | Implemented; fresh Windows result pending |
| CPU `VocabOnly` model-probe source | Implemented; controlled Granite result pending |
| Read-only pre/post GGUF integrity evidence | Implemented in feasibility source; runtime verification pending |
| Native progress, cancellation and disposal evidence | Implemented in feasibility source; runtime verification pending |
| LLamaSharp package in the WinUI application | Not added |
| Production `ILlamaModelProbe` | Not implemented |
| Application inspection service and classifier | Not implemented |
| Dynamic WinUI progress | Not implemented |
| OpenVINO inspection | Not implemented |
| Vulkan or TurboQuant verification | Not implemented; later engineering gates |
| Functional page cancellation | Not implemented |
| Hardware Fit continuation | Not implemented |

## Source hierarchy

```text
Features/ModelInspection/
├── README.md
├── ModelInspectionPage.xaml
├── ModelInspectionPage.xaml.cs
├── Controls/
│   ├── README.md
│   ├── InspectionModelCard.xaml/.cs
│   ├── InspectionContentCard.xaml/.cs
│   ├── InspectionContentTemplateSelector.cs
│   ├── InspectionOutcomeCard.xaml/.cs
│   └── InspectionActionCard.xaml/.cs
├── Models/
│   ├── README.md
│   ├── presentation classes
│   └── mode, status, tone, badge and outcome enums
└── Presentation/
    ├── README.md
    └── InitialInspectionProgressPresentationFactory.cs
```

The isolated runtime feasibility source deliberately lives outside the WinUI
feature:

```text
tools/
├── README.md
├── ModelInspection.LlamaSharpSpike/
│   ├── README.md
│   ├── native-smoke source
│   └── ModelProbe/
│       ├── README.md
│       ├── read-only file identity and integrity helpers
│       ├── genuine native-progress recorder
│       ├── project-owned VocabOnly evidence contracts
│       ├── evidence collector
│       └── VocabOnly probe orchestration
└── ModelInspection.LlamaSharpSpike.Tests/
    └── deterministic feasibility tests
```

Child documentation:

- [Inspection controls](./Controls/README.md)
- [Inspection presentation models](./Models/README.md)
- [Presentation construction](./Presentation/README.md)
- [LLamaSharp feasibility tool](../../../tools/ModelInspection.LlamaSharpSpike/README.md)
- [VocabOnly model probe](../../../tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md)

## Application page responsibility

`ModelInspectionPage` currently owns presentation composition only:

```text
Model inspection heading
Common explanatory subtitle
InspectionOutcomeCard
InspectionModelCard
InspectionContentCard
InspectionActionCard
```

It does not:

- reference LLamaSharp;
- open or parse a GGUF through native code;
- interpret native exceptions;
- classify model outcomes;
- choose a hardware backend;
- initialise Vulkan;
- activate TurboQuant;
- retain native model handles.

### Page lifecycle

```text
constructor
    → InitializeComponent
    → subscribe to Loaded

OnNavigatedTo
    → require non-empty model path
    → retain SelectedModelPath
    → reset one-time presentation guard

Loaded
    → require SelectedModelPath
    → apply initial model, progress, outcome and action presentations
```

Receiving navigation data and manipulating loaded controls remain separate
lifecycle responsibilities.

## Initial page presentation

```text
Outcome card
    → Hidden

Model card
    → Compact
    → Model selected
    → filename and basic format inferred from path
    → no invented architecture, quantisation, parameters or compatibility

Content card
    → Progress
    → 0 of 5 checks complete
    → stage 1 active
    → stages 2–5 waiting

Action card
    → Inspecting layout
    → Cancel visible but disabled until a real cancellable service exists
```

The progress presentation is created by:

```text
InitialInspectionProgressPresentationFactory.Create()
```

This keeps the exact stage contract outside the page code-behind.

## Five user-visible progress stages

```text
1. Check model package
2. Read model configuration
3. Validate tokenizer and chat setup
4. Validate model structure
5. Confirm core runtime compatibility
```

The final stage deliberately does not say `Confirm runtime support`, because
Hardware Fit and backend-specific verification have not occurred yet.

### Intended future mapping

| Visible row | Future real work |
|---|---|
| Check model package | Validate request, file identity and quick-scan continuity |
| Read model configuration | Initialise the matched core runtime and read lightweight model information |
| Validate tokenizer and chat setup | Inspect vocabulary, special tokens and embedded chat-template evidence |
| Validate model structure | Inspect architecture, context, parameters and runtime characteristics |
| Confirm core runtime compatibility | Compare evidence, classify core findings and determine whether Hardware Fit may run |

Stage counts are honest progress. A numeric fraction may appear only where
LLamaSharp supplies a genuine fraction for the active native operation.

## Selected application runtime

```text
Managed package
    LLamaSharp 0.27.0

Native CPU backend package
    LLamaSharp.Backend.Cpu 0.27.0

Mapped llama.cpp commit
    3f7c29d318e317b63f54c558bc69803963d7d88c

Initial intended RID
    win-x64
```

The separate standalone upstream campaign remains research evidence:

```text
llama.cpp b9870
2d973636e292ee6f75fadcf08d29cb33511f509f
```

The two runtime identities must not be presented as the same build.

## Feasibility tool stages

### Gate 1 — native CPU backend smoke

The no-model mode:

```text
Program
    → CPU-only LLamaSharp configuration
    → NativeLibraryConfig.LLama.DryRun
    → selected backend identity and logs
    → local JSON evidence
```

It proves native-library discovery only. It does not inspect a model.

### Gate 2 — CPU `VocabOnly` model probe

The new model mode:

```text
selected local GGUF
    ↓
reject unsafe output/model path equality
    ↓
read-only file snapshot before
    ↓
matched CPU backend dry run
    ↓
LLamaWeights.LoadFromFileAsync
    VocabOnly = true
    GpuLayerCount = 0
    UseMemorymap = true
    UseMemoryLock = false
    ↓
collect lightweight runtime evidence
    ↓
dispose LLamaWeights/native handle
    ↓
read-only file snapshot after
    ↓
compare length, timestamp and SHA-256
    ↓
write project-owned JSON evidence
```

The probe does not create a context, allocate a KV cache, run inference, use
Vulkan or activate TurboQuant.

## VocabOnly evidence

When the selected runtime exposes it, the probe records:

### Runtime identity

- exact LLamaSharp and CPU backend package versions;
- mapped llama.cpp commit;
- operating system, process architecture and .NET runtime;
- selected native library, AVX level, CUDA and Vulkan flags;
- bounded native-loader logs.

### Model identity and preservation

- filename;
- SHA-256 fingerprint of the canonical local path rather than the full path;
- file length;
- last-write time;
- SHA-256 before and after;
- explicit integrity comparison.

The evidence output is rejected if it resolves to the model file. Failure
messages and native logs redact the canonical model path before serialization.

### Runtime model information

- description;
- metadata count and sorted metadata key names;
- selected architecture/name/file-type/quantisation metadata;
- context size;
- runtime-reported model size;
- parameter, embedding, layer, attention-head and KV-head values;
- encoder, decoder, recurrent and diffusion characteristics.

Zeros and missing values are retained as observed. The probe does not guess
values from the filename.

### Tokenizer and chat setup

- vocabulary count and type;
- known special-token identifiers and optional decoded text;
- fixed non-sensitive tokenizer smoke using `Hello`;
- embedded chat-template presence;
- chat-template character count and SHA-256, not the complete template text.

### Progress, memory and disposal

- genuine `IProgress<float>` samples only;
- load duration;
- working-set observations before load, after load and after disposal;
- process peak working set;
- whether the native handle reports closed after deterministic disposal.

These are feasibility observations, not final performance or Hardware Fit
results.

## Completion and failures

The tool uses operation states:

```text
Succeeded
Cancelled
Failed
```

These are not application model outcomes such as `Ready`, `Unsupported` or
`Invalid`.

Stable feasibility codes include:

```text
MI-OP-MODEL-FILE-NOT-FOUND
MI-OP-MODEL-FILE-ACCESS-DENIED
MI-OP-MODEL-FILE-IO
MI-OP-RUNTIME-UNAVAILABLE
MI-OP-RUNTIME-ARCHITECTURE-MISMATCH
MI-PROBE-MODEL-LOAD-FAILED
MI-PROBE-CANCELLED
MI-OP-MODEL-INTEGRITY-CHANGED
MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED
MI-OP-RUNTIME-INSPECTION-FAILED
```

A native infrastructure failure is not an invalid model. A model-load failure
is technical evidence for the future classifier, not a final classifier result.

## Cancellation

The feasibility command supports:

```text
Ctrl+C
--cancel-after-ms <positive integer>
```

Cancellation reaches `LoadFromFileAsync` through a `CancellationToken`, retains
progress already observed, disposes any native handle, performs the post-probe
integrity check, writes evidence, and returns exit code `3`.

The WinUI Cancel button remains disabled because the production service and
ViewModel have not yet been implemented.

## Engineering gate ladder

The development gates remain separate from the five user-visible rows:

```text
1. Matched LLamaSharp CPU native-library smoke
        ↓
2. CPU lightweight Granite VocabOnly probe
        ↓
3. Ordinary Vulkan baseline
        ↓
4. TurboQuant fork CPU correctness baseline
        ↓
5. TurboQuant fork Vulkan acceleration
        ↓
6. LLamaSharp/custom TurboQuant backend compatibility
        ↓
7. Production inspection and inference integration
```

Vulkan and TurboQuant are not required to determine whether the core runtime
can read the model. They are later backend-verification questions after core
inspection and Hardware Fit selection.

## Tests and evidence paths

### WinUI presentation and navigation

- [`InitialInspectionProgressPresentationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/InitialInspectionProgressPresentationTests.cs)
- [`ModelInspectionPageNavigationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelInspection/ModelInspectionPageNavigationTests.cs)
- [`OnboardingModelInspectionNavigationTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/OnboardingModelInspectionNavigationTests.cs)
- [`ModelImportNavigationRequestTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/ModelImport/ModelImportNavigationRequestTests.cs)
- [`InspectionContentTemplateSelectorTests.cs`](../../../tests/UnitTests/GraniteEdgeAI.UnitTests/Features/Onboarding/Controls/InspectionContentTemplateSelectorTests.cs)

### LLamaSharp feasibility

- [`PinnedApplicationRuntimeTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/PinnedApplicationRuntimeTests.cs)
- [`SpikeOptionsParserTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/SpikeOptionsParserTests.cs)
- [`SmokeEvidenceWriterTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/SmokeEvidenceWriterTests.cs)
- [`ModelProbeSafetyValidatorTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/ModelProbeSafetyValidatorTests.cs)
- [`ModelFileSnapshotServiceTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/ModelFileSnapshotServiceTests.cs)
- [`NativeLoadProgressRecorderTests.cs`](../../../tools/ModelInspection.LlamaSharpSpike.Tests/NativeLoadProgressRecorderTests.cs)

The deterministic test source is implemented. A fresh Windows restore, test
run, Release build, native smoke and controlled Granite model run remain
required before pass claims are made.

## Implemented now

- Model Import → Model Inspection event-driven navigation;
- onboarding stage synchronization;
- Model Inspection page shell and four reusable controls;
- initial model, progress, outcome and action presentations;
- focused five-stage progress factory;
- stable core-runtime wording;
- template-selector bootstrap handling;
- matched LLamaSharp/CPU dependency pins in the isolated tool only;
- native-library dry-run source;
- CPU `VocabOnly` model-probe source;
- read-only model identity snapshots;
- output/model collision protection;
- genuine native-progress recording;
- lightweight model/tokenizer/chat-template evidence contracts;
- deterministic native disposal path;
- pre/post model-integrity comparison;
- cancellation and controlled failure mapping;
- generic atomic JSON evidence writing;
- source-adjacent README hierarchy and deterministic test contracts.

## Not implemented and non-claims

- no LLamaSharp reference in the WinUI application project;
- no production `ILlamaModelProbe` or `LlamaSharpModelProbe`;
- no `IModelInspectionService`, classifier or ViewModel;
- no dynamic progress connection to the page;
- no successful real Granite VocabOnly result is claimed yet;
- no proof yet that `VocabOnly` exposes sufficient production evidence;
- no full tensor checking or model allocation;
- no context, KV cache or generation;
- no OpenVINO adapter;
- no Vulkan baseline;
- no TurboQuant CPU or Vulkan result;
- no Hardware Fit handoff;
- no final Ready/Warning/Conversion/Unsupported/Invalid classification.

## Next decision

After one controlled Granite result is reviewed:

```text
VocabOnly supplies sufficient evidence
    → define production domain contracts and ILlamaModelProbe

VocabOnly is insufficient
    → investigate the lower-level no-allocation path behind the same adapter boundary
```

Only after the production core probe is stable should the service, classifier,
ViewModel and dynamic page progression be connected.

## Related documentation

- [Application feature architecture](../README.md)
- [Model Import architecture](../ModelImport/README.md)
- [Onboarding architecture](../Onboarding/README.md)
- [Inspection controls](./Controls/README.md)
- [Inspection presentation models](./Models/README.md)
- [Presentation construction](./Presentation/README.md)
- [LLamaSharp feasibility tool](../../../tools/ModelInspection.LlamaSharpSpike/README.md)
- [VocabOnly model probe](../../../tools/ModelInspection.LlamaSharpSpike/ModelProbe/README.md)
- [VocabOnly probe design](../../../docs/superpowers/specs/2026-08-04-llamasharp-vocab-only-model-probe-design.md)
- [VocabOnly probe implementation plan](../../../docs/superpowers/plans/2026-08-04-llamasharp-vocab-only-model-probe.md)
