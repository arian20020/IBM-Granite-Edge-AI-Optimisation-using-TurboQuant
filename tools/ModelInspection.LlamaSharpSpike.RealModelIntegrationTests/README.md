# LLamaSharp trusted real-model integration tests

**Status:** Trusted-suite source implemented; target-runner execution pending  
**CI tier:** Tier 2 — manual repository-owned workflow only  
**Shared support:** [Probe process test support](../ModelInspection.LlamaSharpSpike.TestSupport/README.md)  
**Coverage register:** [LLamaSharp Runtime Test Coverage Matrix](../../docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md)

## Purpose

This Microsoft.Testing.Platform project verifies the exact LLamaSharp CPU
runtime against the controlled Granite 4.1 3B Q4_K_M model and every committed
malformed GGUF fixture.

All native/model scenarios execute the published feasibility tool as a child
process. The MSTest host never loads llama.cpp directly.

## Controlled model identity

```text
Filename:                    granite-4.1-3b-Q4_K_M.gguf
Byte length:                 2,099,501,664
SHA-256:                     662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
Architecture:                granite
Model name:                  Granite 4.1 3b
GGUF file type:              15
Quantisation version:        2
Tokenizer model:             gpt2
Declared context:            131,072
Embedding size:              2,560
Layers:                      40
Attention heads:             40
KV heads:                    8
Metadata count:              31
Vocabulary count:            100,352
Tokenizer smoke token count: 1
Chat template:               present
```

The model remains outside Git. Only its identity and expected safe-depth
evidence are committed in:

```text
ControlledModels/granite-4.1-3b-q4-k-m.json
```

## Required environment

```text
LLAMASHARP_SPIKE_PUBLISH_DIR
    Release / win-x64 published feasibility tool

GRANITE_TEST_MODEL_PATH
    Runner-readable path to the exact controlled Granite model

LLAMASHARP_REAL_MODEL_EVIDENCE_DIR
    Optional retained evidence root; local runs use disposable temp evidence
```

Missing configuration, wrong filename, wrong byte length, wrong SHA-256, or an
unreadable file fails loudly before the native runtime is called.

## Test hierarchy

```text
RealModelIntegrationTests/
├── AssemblyInfo.cs
├── ControlledModels/
├── Support/
│   ├── ControlledModelConfigurationTests.cs
│   ├── RealModelEvidenceDirectory.cs
│   └── RealModelTestContext.cs
├── RealModel/
│   ├── GraniteVocabOnlySuccessTests.cs
│   └── GraniteVocabOnlyRepeatabilityTests.cs
├── Cancellation/
│   ├── WholeOperationCancellationTests.cs
│   └── NativeLoadCancellationTests.cs
├── MalformedModels/
│   └── MalformedModelProcessTests.cs
├── FileAccess/
│   ├── MissingAndDirectoryModelProcessTests.cs
│   ├── LockedModelProcessTests.cs
│   ├── LockedOutputProcessTests.cs
│   └── UnsafeOutputProcessTests.cs
└── Security/
    ├── EvidencePrivacyTests.cs
    └── NetworkObservationTests.cs
```

The assembly is non-parallel because scenarios share one large read-only model
and native-runtime package while retaining separate child processes and evidence
paths.

## Covered scenarios

### Controlled success and repeatability

- exact package/runtime/model evidence;
- real vocabulary/tokenizer/chat-template evidence;
- safe nullable parameter count;
- genuine progress fractions;
- deterministic native disposal;
- before/after model SHA-256;
- three sequential independent child runs;
- stable metadata and chat-template hash;
- no temporary writer files.

### Cancellation

```text
--cancel-after-ms
    timer begins after the initial integrity snapshot

--cancel-native-after-ms
    timer begins immediately before LLamaWeights.LoadFromFileAsync
```

Both must return:

```text
CompletionStatus = Cancelled
FailureCode = MI-PROBE-CANCELLED
Process exit code = 3
Structured JSON present
Original model hash unchanged
```

`Ctrl+C` remains active throughout preflight and native work but is tested later
through the production ViewModel/UI integration rather than simulated in this
MSTest project.

### Malformed and hostile input

Every committed `I-*` entry in `tests/TestFixtures/fixture-manifest.json` runs
in an independent child process. The suite records:

- fixture ID, path-relative filename, size and expected hash;
- child termination and exit code;
- structured failure code where JSON exists;
- native termination where JSON cannot be produced;
- before/after fixture hash;
- one matrix JSON and Markdown summary.

A deterministic random-byte `.gguf` is also tested outside the retained
evidence tree and deleted afterwards.

### File and output failures

- missing model;
- directory supplied as model;
- model held with `FileShare.None`;
- evidence output held with `FileShare.None`;
- output parent that is a regular file;
- evidence output resolving to the model path.

The controlled real model is never used as a writable fixture.

### Privacy and offline observations

- canonical model path absent from JSON, stdout, and stderr;
- no `modelPath` or full chat-template property;
- evidence contains only chat-template presence, length, and SHA-256;
- no `.gguf`, model-sized file, oversized file, or file matching model SHA-256
  under the artifact root;
- `netstat.exe` polls the exact child PID and records any TCP `LISTENING` or
  `ESTABLISHED` endpoint;
- the expected result is zero observed endpoints.

The network test is an observation, not a physical disconnected-network test.
A separate manual disconnected-machine run remains documented as deferred.

## Local execution

```powershell
$RepositoryRoot = (
    git rev-parse --show-toplevel
).Trim()

$PublishDirectory = Join-Path `
    $env:TEMP `
    "GraniteEdgeAI-LlamaSharp-Publish"

Remove-Item `
    -LiteralPath $PublishDirectory `
    -Recurse `
    -Force `
    -ErrorAction SilentlyContinue

dotnet publish `
    "tools\ModelInspection.LlamaSharpSpike\ModelInspection.LlamaSharpSpike.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $PublishDirectory

$env:LLAMASHARP_SPIKE_PUBLISH_DIR = $PublishDirectory
$env:GRANITE_TEST_MODEL_PATH = Join-Path `
    $env:USERPROFILE `
    "Downloads\granite-4.1-3b-Q4_K_M.gguf"
$env:LLAMASHARP_REAL_MODEL_EVIDENCE_DIR = Join-Path `
    $RepositoryRoot `
    "artifacts\model-inspection\llamasharp\real-model"

dotnet test `
    "tools\ModelInspection.LlamaSharpSpike.RealModelIntegrationTests\ModelInspection.LlamaSharpSpike.RealModelIntegrationTests.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --filter "TestCategory=RealModelIntegration" `
    --minimum-expected-tests 20
```

## Trusted-runner requirements

The GitHub Actions service account must be able to read the model. A path under
an interactive user's Downloads folder is usually not suitable for the
restricted runner service. Stage the exact model in a runner-readable,
read-only location and configure the repository/environment variable:

```text
GRANITE_TEST_MODEL_PATH
```

The workflow is `workflow_dispatch` only and uses:

```text
self-hosted
Windows
X64
workbook05
intel-target
```

Before upload it scans retained evidence for model files, model-sized files,
oversized files, and files whose SHA-256 equals the controlled model.

## Non-claims

This suite does not test:

- WinUI Cancel button wiring;
- production `ILlamaModelProbe` or inspection service;
- full tensor checking or full model allocation;
- context or KV-cache creation;
- generation or quality;
- CPU benchmarking;
- Vulkan or GPU offload;
- TurboQuant;
- OpenVINO;
- Hardware Fit.

Source presence is not a pass claim. The trusted suite becomes verified only
after its workflow or equivalent local commands complete and evidence is
reviewed.
