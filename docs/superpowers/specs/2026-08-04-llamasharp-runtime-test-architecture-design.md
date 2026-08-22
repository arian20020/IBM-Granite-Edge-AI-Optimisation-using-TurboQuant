# LLamaSharp Runtime Test Architecture Design

**Status:** Approved design; implementation plan pending review  
**Date:** 2026-08-04  
**Target branch:** `feature/model-inspection`  
**Scope:** LLamaSharp CPU native-library and `VocabOnly` model-probe boundary  
**Related decisions:**
- [ADR-001 — matched LLamaSharp application runtime](../../architecture/decisions/ADR-001-llamasharp-application-runtime.md)
- [ADR-002 — core inspection versus backend verification](../../architecture/decisions/ADR-002-core-inspection-versus-backend-verification.md)

## 1. Purpose

This design finalises the automated and evidence-producing tests for the
LLamaSharp CPU runtime integration completed so far.

The test architecture must answer two different questions without confusing
them:

```text
Fast deterministic question
    → Does our C# logic preserve its contracts for every normal CI change?

Real runtime question
    → Does the exact managed/native pair behave correctly with actual DLLs,
      real Granite input, malformed input, cancellation and operating-system
      failure conditions on the trusted Windows target?
```

The design is coverage-driven rather than test-count-driven. Every known or
reasonably foreseeable failure category at this boundary must have one of:

1. an automated deterministic test;
2. a hosted native integration test;
3. a trusted-runner real-model/process test; or
4. an explicit deferral with a reason and a future evidence route.

No untested condition may be silently treated as covered.

## 2. Verified baseline

The current target-laptop evidence establishes:

```text
Deterministic suite:              28 / 28 passed
Release win-x64 build:            passed
Managed package:                  LLamaSharp 0.27.0
Native CPU package:               LLamaSharp.Backend.Cpu 0.27.0
Mapped llama.cpp commit:          3f7c29d318e317b63f54c558bc69803963d7d88c
Native CPU smoke:                 passed
Real model:                       granite-4.1-3b-Q4_K_M.gguf
Real VocabOnly probe:             passed
Evidence schema:                  1.1
Original model SHA-256 preserved: yes
Native handle closed:             yes
```

Verified model evidence includes:

```text
Architecture:                granite
Model name:                  Granite 4.1 3b
File type:                   15
Quantisation version:        2
Tokenizer model:             gpt2
Declared context:            131072
Embedding size:              2560
Layer count:                 40
Attention heads:             40
KV heads:                    8
Metadata count:              31
Vocabulary count:            100352
Tokenizer smoke:             passed / 1 token
Chat template:               present
```

The parameter count remained unavailable at the safe `VocabOnly` depth and is
correctly represented as `null` rather than guessed.

Controlled cancellation remains unverified. Full CPU model allocation,
context creation, inference, Vulkan and TurboQuant remain later gates.

## 3. Goals

The testing slice will:

- protect exact package and native-revision policy;
- exercise every deterministic branch in command parsing, path safety, file
  identity, metadata projection, evidence creation, progress, failure mapping,
  redaction and result finalisation;
- isolate every native/model execution in a child process;
- preserve the normal pull-request path as fast and model-free;
- run real-model and malformed-model tests only on a trusted Windows runner;
- verify cancellation at both whole-operation and native-load scopes;
- verify model and fixture preservation before and after every process test;
- capture native crashes without killing the test host;
- distinguish infrastructure failure from model findings;
- verify that no LLamaSharp dependency leaks into the WinUI project yet;
- verify that no Vulkan or TurboQuant dependency is introduced into this CPU
  inspection boundary;
- verify that no listening HTTP port or model upload is observed;
- maintain a coverage register linking each risk to an executable test or an
  explicit deferral;
- keep test code organised into small, cohesive files and shared support
  components.

## 4. Non-goals

This design does not test:

- the WinUI Cancel button or ViewModel command;
- production `ILlamaModelProbe` or `LlamaSharpModelProbe` contracts;
- classifier outcomes such as Ready, Unsupported or Invalid;
- full tensor checking;
- full weight allocation;
- context or KV-cache creation;
- text generation or quality;
- CPU performance benchmarking;
- Vulkan loading or GPU offload;
- TurboQuant CPU or Vulkan operation;
- OpenVINO;
- Hardware Fit.

Those capabilities receive separate designs and evidence gates.

## 5. Core testing principles

### 5.1 Test behaviour, not incidental wording

Assertions will protect stable behavior and diagnostic codes. They will not
require complete human-readable sentences unless the wording itself is a
formal accessibility or product requirement.

For example, a cancellation-without-model test will assert that the message
identifies both `--cancel-after-ms` and `--model`, rather than requiring one
specific filler phrase between them.

### 5.2 Native code never runs inside the main test host

The earlier VocabOnly collector caused llama.cpp to call `GGML_ABORT`, which
terminated the process before C# cleanup or evidence writing could run.

Therefore every test that calls a native LLamaSharp entry point or opens a model
must launch the feasibility executable as a child process.

```text
MSTest host
    ↓
ProbeProcessRunner
    ↓
child feasibility process
    ↓
LLamaSharp / llama.cpp
```

The parent test records:

- process exit code;
- timeout or memory-limit action;
- stdout;
- stderr;
- JSON evidence when present;
- model/fixture hash before and after.

A native abort becomes an observed child-process result rather than a test-host
crash.

### 5.3 Ordinary CI must never depend on a 2 GB model

The controlled Granite model remains outside Git. Normal pushes and pull
requests run deterministic tests and native-library-only tests. The real-model
suite runs only by explicit request on a trusted Windows runner.

### 5.4 Evidence before claims

A test source file proves only that a check exists. A claim such as
"cancellation works" requires fresh command output showing the expected exit,
JSON and integrity result.

### 5.5 Missing evidence is not zero

Nullable runtime fields remain nullable when safe `VocabOnly` metadata does not
provide them. Tests must reject filename inference or unsafe native getters as a
substitute.

### 5.6 No silent skips

A trusted integration run with a missing model path, missing expected hash or
unreadable model must fail loudly. It must not skip and leave a green workflow.

## 6. CI tiers

## 6.1 Tier 1 — every relevant push and pull request

Runs on a GitHub-hosted Windows x64 runner.

```text
Deterministic contract tests
    +
Release win-x64 build
    +
published CPU native-library smoke
    +
negative native-backend sandbox tests
```

Properties:

- no external model;
- no user profile dependency;
- no self-hosted runner;
- bounded execution time;
- safe for repository-owned and reviewed pull requests;
- evidence artifact contains only runtime/test output.

## 6.2 Tier 2 — trusted real-model integration

Runs by `workflow_dispatch` only on the trusted target runner:

```text
self-hosted
Windows
X64
workbook05
intel-target
```

It runs:

- controlled Granite success contract;
- repeated sequential probes;
- whole-operation cancellation;
- native-load-scoped cancellation;
- malformed/hostile fixture matrix;
- locked-file and locked-output scenarios;
- socket/listener observation;
- path-redaction checks;
- integrity checks after every scenario.

The workflow must never run code from a fork pull request. It accepts only a
repository-owned, explicitly selected ref.

## 7. Target project structure

```text
tools/
├── ModelInspection.LlamaSharpSpike/
│   └── feasibility production source
│
├── ModelInspection.LlamaSharpSpike.Tests/
│   ├── README.md
│   ├── DependencyPolicy/
│   │   └── RuntimeDependencyPolicyTests.cs
│   ├── CommandLine/
│   │   └── SpikeOptionsParserTests.cs
│   ├── FileSafety/
│   │   ├── ModelProbeSafetyValidatorTests.cs
│   │   ├── ModelFileSnapshotServiceTests.cs
│   │   └── ModelFileIntegrityComparisonTests.cs
│   ├── Metadata/
│   │   ├── VocabOnlyMetadataProjectionTests.cs
│   │   └── ChatTemplateEvidenceFactoryTests.cs
│   ├── Progress/
│   │   └── NativeLoadProgressRecorderTests.cs
│   ├── Evidence/
│   │   ├── JsonEvidenceWriterTests.cs
│   │   └── EvidenceContractTests.cs
│   ├── Failures/
│   │   ├── ProbeFailureMapperTests.cs
│   │   ├── SensitiveTextRedactorTests.cs
│   │   └── ProbeResultFinalizerTests.cs
│   └── Support/
│       ├── TemporaryDirectory.cs
│       └── TestFileBuilder.cs
│
├── ModelInspection.LlamaSharpSpike.TestSupport/
│   ├── ProbeProcessRunner.cs
│   ├── ProbeExecutionResult.cs
│   ├── ProcessTerminationKind.cs
│   ├── EvidenceAssertions.cs
│   ├── ControlledModelManifest.cs
│   ├── TemporaryPublishedProbe.cs
│   └── SocketObservation.cs
│
├── ModelInspection.LlamaSharpSpike.NativeIntegrationTests/
│   ├── README.md
│   ├── NativeBackendSmokeProcessTests.cs
│   ├── MissingNativeBackendProcessTests.cs
│   ├── InvalidNativeBackendProcessTests.cs
│   └── RuntimeEvidenceProcessTests.cs
│
└── ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
    ├── README.md
    ├── ControlledModels/
    │   └── granite-4.1-3b-q4-k-m.json
    ├── RealModel/
    │   ├── GraniteVocabOnlySuccessTests.cs
    │   └── GraniteVocabOnlyRepeatabilityTests.cs
    ├── Cancellation/
    │   ├── WholeOperationCancellationTests.cs
    │   └── NativeLoadCancellationTests.cs
    ├── MalformedModels/
    │   └── MalformedModelProcessTests.cs
    ├── FileAccess/
    │   ├── LockedModelProcessTests.cs
    │   └── LockedOutputProcessTests.cs
    └── Security/
        ├── EvidencePrivacyTests.cs
        └── NetworkObservationTests.cs
```

`ModelInspection.LlamaSharpSpike.TestSupport` is a non-shipping test utility
project shared by the two process-based integration projects. It prevents
copying child-process, timeout, evidence and temporary-publish logic into
multiple test classes.

Existing deterministic test files may be moved into the corresponding folders
without changing their namespaces or behavior unless the implementation plan
explicitly identifies a required refactor.

## 8. Deterministic coverage

## 8.1 Dependency policy

Tests inspect actual project and documentation files.

| Contract | Required result |
|---|---|
| Managed package | `LLamaSharp` exactly `0.27.0` |
| CPU backend | `LLamaSharp.Backend.Cpu` exactly `0.27.0` |
| Version syntax | No wildcard, range or floating version |
| WinUI project | Zero `LLamaSharp*` references at this stage |
| GPU packages | No CUDA or Vulkan backend package in CPU probe |
| TurboQuant | No TurboQuant dependency in CPU probe |
| Test runner | Embedded MSTest/Microsoft.Testing.Platform remains enabled |
| Runtime identity | Mapped llama.cpp commit remains recorded |
| Research separation | `b9870` remains labelled research, not application runtime |

Tests must read configuration files at runtime. Constant-to-literal assertions
are prohibited because they cannot detect configuration drift.

## 8.2 Command-line parser

The parser matrix covers:

```text
no arguments
--help
-h
explicit native-smoke output
model path only
model plus output in every order
model plus timed cancellation in every order
model plus native-load-scoped cancellation in every order
paths containing spaces
Unicode paths
missing model value
missing output value
missing cancellation value
unknown argument
duplicate model option
duplicate output option
duplicate cancellation option
help mixed with other options
zero cancellation delay
negative cancellation delay
decimal cancellation delay
non-numeric cancellation delay
integer overflow
cancellation without a model
blank values
whitespace-only values
mutually exclusive cancellation modes
```

Stable assertions cover:

- success/failure;
- selected probe mode;
- parsed values;
- stable diagnostic code or required option names.

## 8.3 Output/model collision safety

Tests cover:

```text
distinct paths
exact same path
relative and absolute equivalent paths
. path segments
.. path segments
Windows case-only differences
paths containing spaces
Unicode paths
invalid path syntax
blank paths
```

The invariant is:

```text
The evidence destination must never resolve to the selected model path.
```

Existing symlink, junction and hard-link aliases will be included where the
host file system supports creating them. Unsupported link creation must be
recorded as an explicit environment limitation rather than silently counted as
passed.

## 8.4 File snapshots and integrity

Tests cover:

```text
known byte sequence → known SHA-256
empty file hash
read-only file
missing file
directory supplied instead of file
cancellation before hashing
cancellation during hashing
canonical path fingerprint stability
unchanged comparison
changed length
changed SHA-256
changed timestamp
changed canonical-path fingerprint
```

Each comparison field receives its own focused test.

Result finalisation must enforce this rule:

> A changed or unverifiable model overrides an earlier Succeeded or Cancelled
> operation state.

Cancellation must never conceal an integrity change.

## 8.5 Metadata projection

Coverage includes:

```text
complete Granite metadata
missing architecture
unknown architecture
missing optional values
empty values
whitespace-only values
zero values
negative numbers
Int32 overflow
UInt64 overflow
malformed numbers
culture-specific numbers
wrong architecture-prefixed keys
metadata for multiple architectures
missing parameter count
```

Invariants:

```text
missing → null
malformed → null
no filename guessing
no cross-architecture leakage
invariant-culture numeric parsing
no native hyperparameter getters in VocabOnly projection
```

A source-contract test will guard the VocabOnly collector from reintroducing
known unsafe property calls such as `HeadCount`, `KVHeadCount`, `LayerCount`,
`ContextSize`, `EmbeddingSize`, `HasEncoder`, `HasDecoder`, `IsRecurrent`,
`IsDiffusion` or `Description`.

## 8.6 Chat-template evidence

A pure `ChatTemplateEvidenceFactory` will be extracted and tested for:

```text
metadata key absent
null value
empty template
whitespace-only template
ASCII template
Unicode template
stable SHA-256
correct character length
complete template absent from serialized evidence
```

Empty and whitespace-only template semantics must be explicit. The selected
rule is:

```text
key absent                 → Present = false
key present, empty value   → Present = true, length = 0, hash recorded
key present, whitespace    → Present = true, exact length/hash recorded
```

This records what the GGUF contains without silently rewriting it.

## 8.7 Progress recorder

Coverage includes:

```text
0
1
below 0
above 1
positive infinity
negative infinity
NaN
consecutive duplicates
non-consecutive repeated values
concurrent reports
independent snapshots
non-decreasing elapsed times
empty stream
```

Selected normalization rules:

```text
NaN                     → ignored
negative infinity       → 0
positive infinity       → 1
finite value            → clamp to 0..1
consecutive duplicate   → omitted
```

The recorder never manufactures intermediate percentages.

## 8.8 JSON evidence writer

Coverage includes:

```text
parent-directory creation
camel-case properties
string enum values
parseable JSON
atomic replacement
existing evidence preserved when serialization fails
existing evidence preserved when cancellation occurs
temporary file removed after success
temporary file removed after failure
destination locked
destination is a directory
blank path
null evidence object
```

A deliberately unserialisable test object will verify that an existing valid
report is not replaced by partial output.

## 8.9 Failure mapping

Private exception-switching logic will be extracted to a pure
`ProbeFailureMapper`.

| Input condition | Diagnostic code |
|---|---|
| Missing file | `MI-OP-MODEL-FILE-NOT-FOUND` |
| Access denied | `MI-OP-MODEL-FILE-ACCESS-DENIED` |
| Generic file I/O | `MI-OP-MODEL-FILE-IO` |
| Missing DLL | `MI-OP-RUNTIME-UNAVAILABLE` |
| Invalid native image | `MI-OP-RUNTIME-ARCHITECTURE-MISMATCH` |
| LLamaSharp load failure | `MI-PROBE-MODEL-LOAD-FAILED` |
| Cancellation | `MI-PROBE-CANCELLED` |
| Integrity changed | `MI-OP-MODEL-INTEGRITY-CHANGED` |
| Integrity unverifiable | `MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED` |
| Unexpected exception | `MI-OP-RUNTIME-INSPECTION-FAILED` |

Nested `TypeInitializationException` cases are included.

The mapper may return feasibility/operational diagnostics only. It must not
produce application outcomes such as Ready, Invalid or Unsupported.

## 8.10 Sensitive-data redaction

A pure `SensitiveTextRedactor` will be extracted and tested for:

```text
full model path in error
full model path in native log
case-insensitive Windows match
multiple occurrences
mixed slash direction where Windows treats paths equivalently
unrelated path remains unchanged
filename may remain
canonical path fingerprint may remain
full chat template absent
native pointer/handle absent
```

The parent process runner also scans child stdout and stderr for the complete
model path.

## 8.11 Result finalisation

A pure `ProbeResultFinalizer` will be extracted to test the interaction among:

```text
operation status
integrity result
integrity-verification error
failure code
failure message
```

Required precedence:

```text
model changed
    → Failed / MI-OP-MODEL-INTEGRITY-CHANGED

integrity unavailable after otherwise successful or cancelled operation
    → Failed / MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED

cancelled with verified unchanged model
    → Cancelled / MI-PROBE-CANCELLED

successful with verified unchanged model
    → Succeeded
```

## 9. Hosted native integration coverage

A separate process-based project runs on GitHub-hosted Windows without a
model.

## 9.1 Successful native smoke

Required assertions:

```text
exit code = 0
JSON exists
Succeeded = true
managed version = 0.27.0
backend version = 0.27.0
mapped commit exact
process architecture = X64
native library name present
CUDA = false
Vulkan = false
no model snapshot present
```

## 9.2 Missing native backend

The test publishes/copies the feasibility executable into a temporary sandbox,
removes the native backend files, then starts the child process.

Required behavior:

```text
parent test host survives
child exits nonzero or returns controlled runtime-unavailable evidence
no model outcome is produced
stdout/stderr retained
sandbox cleaned
```

## 9.3 Invalid native backend

The sandbox replaces the expected native binary with a small invalid file.

Required behavior:

```text
parent test host survives
child result recorded as invalid native image, runtime unavailable,
controlled failure or native termination
no final model outcome
no path leak
```

The exact native loader message is not asserted because it is platform and
loader-version dependent.

## 9.4 Process timeout

Every child process has a timeout. On expiry, the runner kills the complete
process tree and records:

```text
TerminationKind = TimedOut
ExitCode = null or killed-process code
stdout/stderr retained
```

Hosted smoke timeout: 60 seconds.

## 10. Trusted real-model configuration

The workflow receives the model through runner configuration, not Git.

```text
Repository variable:
GRANITE_TEST_MODEL_PATH

Version-controlled manifest:
tools/ModelInspection.LlamaSharpSpike.RealModelIntegrationTests/
ControlledModels/granite-4.1-3b-q4-k-m.json
```

The manifest records:

```text
model ID
expected filename
expected size: 2099501664 bytes
expected SHA-256:
662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29
expected architecture: granite
expected model name: Granite 4.1 3b
expected file type: 15
expected quantisation version: 2
expected tokenizer model: gpt2
expected context: 131072
expected embedding size: 2560
expected layers: 40
expected heads: 40
expected KV heads: 8
expected vocabulary: 100352
expected chat-template presence: true
```

The implementation plan must locate or create the repository's authoritative
provenance record for this local file before the trusted workflow is treated as
formal release evidence. The runtime test itself is pinned by filename, size
and hash regardless of provenance-document status.

For the service account, the recommended machine-wide model location is:

```text
C:\ProgramData\GraniteEdgeAI\TestModels\granite-4.1-3b-Q4_K_M.gguf
```

The workflow fails if the configured path, file, read access or manifest hash is
missing.

## 11. Real Granite success contract

The trusted runner starts the feasibility tool as a child process and requires:

```text
exit code                       0
completion status               Succeeded
schema                          1.1
managed package                 0.27.0
CPU backend                     0.27.0
mapped llama.cpp commit         exact
process architecture            X64
CUDA                            false
Vulkan                          false
GPU layers                      0
architecture                    granite
model name                      Granite 4.1 3b
file type                       15
quantisation version            2
tokenizer model                 gpt2
context                         131072
embedding                       2560
layers                          40
heads                           40
KV heads                        8
metadata count                  greater than 0
vocabulary                      100352
tokenizer smoke                 passed
chat template                   present
all progress fractions          finite and in 0..1
native handle closed            true
integrity                       preserved
before/after hash               manifest hash
full local path in JSON         absent
full local path in stdout       absent
full local path in stderr       absent
```

Parameter count remains allowed to be `null` at this depth.

## 12. Repeatability and cleanup

The real-model suite runs the same controlled probe three times sequentially.

Required assertions:

```text
all three exit 0
all three produce parseable JSON
stable architecture/model/tokenizer/structure evidence
same before/after hash on every run
native handle closes every run
no process-level abort
no stale temporary files
no output collision
```

The integration project is marked non-parallel because LLamaSharp native
configuration is process-global and the model is a shared read-only resource.

Repeatability is not a formal memory benchmark. Memory fields are recorded but
not compared to a narrow pass threshold in this slice.

## 13. Cancellation coverage

## 13.1 Whole-operation cancellation

The existing timer begins at operation start. A one-millisecond cancellation
will normally occur during preflight/hash for the 2 GB model.

Required result:

```text
completion status       Cancelled
failure code            MI-PROBE-CANCELLED
exit code               3
JSON                    present
model hash              unchanged
full path               absent
```

This proves end-to-end token propagation, cancellation classification, evidence
writing and model preservation.

## 13.2 Native-load-scoped cancellation

A diagnostic feasibility option will be added:

```text
--cancel-native-after-ms <positive integer>
```

Its timer starts immediately before `LLamaWeights.LoadFromFileAsync`, after the
pre-probe snapshot has completed.

The option is:

- valid only with `--model`;
- mutually exclusive with `--cancel-after-ms`;
- part of the feasibility tool only;
- not a production UI contract.

Required result:

```text
child process returns normally
completion status       Cancelled
failure code            MI-PROBE-CANCELLED
exit code               3
JSON                    present
model hash              unchanged
any created handle      disposed
```

If the tested model completes before the timer can interrupt native loading,
the test records `NotObserved` rather than falsely claiming native-load
cancellation. A slower controlled model or a progress-triggered diagnostic hook
will then be required as an explicit follow-up.

## 14. Malformed and hostile model matrix

Every malformed fixture runs in its own child process. The existing curated
fixtures under `tests/TestFixtures/Malformed` are discovered from a
version-controlled manifest rather than by blindly running arbitrary files.

Coverage groups:

```text
empty file
invalid GGUF magic
unsupported GGUF version
truncated header
truncated metadata
invalid metadata type
invalid key/value encoding
oversized counts or lengths
unsupported architecture
random bytes with .gguf extension
```

For each case, the parent requires:

```text
process finishes or is terminated by the harness timeout/memory guard
parent test host remains alive
fixture hash unchanged
result is not Succeeded
no Ready/Unsupported/Invalid application outcome is invented
JSON evidence or parent crash record is retained
full local path absent from retained output
```

A native abort is an observed containment result, not an automatically accepted
success. The coverage matrix records which fixture caused it and whether the
future production adapter requires worker-process isolation for that category.

Malformed child timeout: 30 seconds.

The process runner monitors working set. If a malformed probe exceeds 1024 MiB,
it terminates the child process tree and records `MemoryLimitObserved`. This is
a safety observation, not a hard Windows Job Object limit.

## 15. File-access and output failures

Trusted process tests cover:

```text
missing model
directory supplied as model
model held open with FileShare.None
read-only model
output file held with exclusive lock
output destination is a directory
output parent cannot be created through normal path shape
output path equals model
```

True Windows ACL denial is covered deterministically by `ProbeFailureMapper`
until a disposable ACL sandbox is approved. The suite must not alter ACLs on
the real Granite file.

## 16. Security and offline observations

## 16.1 File and evidence privacy

Every real-model run verifies:

```text
model hash unchanged
model not copied into artifact directory
complete model path absent from JSON
complete model path absent from stdout/stderr
full chat template absent from JSON
native pointer/handle absent from JSON
```

## 16.2 Network observation

The process runner polls process-owned TCP connections while the child is
alive.

Required observation:

```text
no listening TCP socket owned by probe process
no established TCP socket observed for probe process
```

This is recorded as "no network use observed," not as proof that every possible
network operation is impossible.

A source/dependency contract test also rejects use of known server/listener
APIs in the feasibility project:

```text
HttpListener
TcpListener
WebApplication
Kestrel
ASP.NET server packages
```

## 16.3 Offline execution

The trusted workflow runs the already restored and built executable with
`--no-build`. GitHub Actions itself requires networking, so this does not prove
physical disconnection.

A physically disconnected-machine run is retained as an explicit manual
security evidence item in the coverage matrix.

## 17. Evidence artifacts

## 17.1 Hosted artifact

Contains only:

```text
deterministic test result
native integration test result
runtime-smoke JSON
negative-backend child stdout/stderr
commit and runtime identity
```

## 17.2 Trusted artifact

Contains only:

```text
controlled model manifest copy
independent before/after hashes
real-model probe JSON
cancellation JSON
repeatability summaries
malformed-fixture process matrix
file-access summaries
socket observations
child stdout/stderr with path redaction
runner identity
application commit
```

It must never contain:

```text
GGUF model bytes
copied model file
full chat template
full personal model path
secrets
native memory dumps containing model data
```

## 18. Coverage register

Create:

```text
docs/testing/LLamaSharp-Runtime-Test-Coverage-Matrix.md
```

Columns:

```text
Risk ID
Failure scenario
Boundary/layer
Automated test or evidence command
Expected result/code
CI tier
Evidence location
Status
Reason if deferred
```

Suggested risk families:

```text
LS-DEP-*   dependency/version policy
LS-CLI-*   command parsing
LS-PATH-*  path safety and privacy
LS-FILE-*  file access and integrity
LS-META-*  metadata projection
LS-TOK-*   vocabulary/tokenizer/chat template
LS-PROG-*  progress and cancellation
LS-EVID-*  evidence writing/schema
LS-NATIVE-* native backend and process behavior
LS-SEC-*   network/privacy/offline
LS-RES-*   disposal and repeatability
```

The matrix is the completeness source of truth. A raw test count is not.

## 19. Workflow design

## 19.1 Hosted workflow

Update:

```text
.github/workflows/llamasharp-feasibility-smoke.yml
```

Steps:

```text
checkout required paths
setup exact .NET SDK
restore deterministic/native integration projects
run deterministic tests
build/publish win-x64 feasibility executable
run native integration process tests
validate native smoke JSON
upload non-model evidence
```

The minimum deterministic test count is updated only after implementation and
fresh discovery output. It is a guard against zero-test success, not the main
coverage measure.

## 19.2 Trusted workflow

Create:

```text
.github/workflows/llamasharp-real-model-integration.yml
```

Properties:

```text
trigger                workflow_dispatch only
runner                 self-hosted Windows X64 workbook05 intel-target
fork PR execution      prohibited
concurrency            one real-model run at a time
timeout                 30 minutes
model source           machine-local configured path
model upload           prohibited
evidence upload        allowed after redaction
```

The workflow validates the selected ref is repository-owned before executing
it on the self-hosted runner.

## 20. Test naming and code-quality rules

Test methods use:

```text
Method_Scenario_ExpectedBehaviour
```

Examples:

```text
Parse_WithDuplicateOutputOption_ReturnsControlledError
CaptureAsync_WhenCancelledDuringHash_ThrowsOperationCanceled
Map_WithMissingNativeLibrary_ReturnsRuntimeUnavailable
Run_WithControlledGranite_PreservesOriginalHash
```

Rules:

```text
one behavioural contract per test
no test-order dependency
no hard-coded user profile path
no brittle whole-sentence assertions
no unbounded wait
no arbitrary Thread.Sleep for synchronization
no shared writable model fixture
no native call in deterministic test host
no giant all-purpose test class
no final model outcome in runtime-probe tests
cleanup through IDisposable/IAsyncDisposable
```

Tests that change process culture, filesystem links or global state are marked
non-parallel and restore state in `finally`.

## 21. Explicitly deferred scenarios

The following are not silently claimed as covered:

| Scenario | Current evidence route |
|---|---|
| Physically disconnected computer | Manual target-laptop security run |
| True x86 native backend on x64 host | Add only when a controlled x86 fixture/package is available |
| Disk completely full | Future disposable virtual-disk sandbox |
| Real valid GGUF without chat template | Add when a provenance-recorded controlled model is available |
| Native cancellation after a nonterminal progress callback | Use native-scoped timer first; add slower model or progress hook if not observed |
| Hard OS memory limit on malformed native process | Future Windows Job Object hardening; current runner observes and kills on threshold |
| OS termination or power loss during native call | Worker-process recovery design, not unit/integration simulation |
| Full model allocation and generation | Post-Hardware-Fit full CPU verification stage |
| Vulkan/TurboQuant | Later backend-verification stages |

## 22. Completion criteria

This testing slice is complete only when:

1. every current risk in the coverage matrix has automated evidence or an
   explicit deferral;
2. hosted deterministic tests pass;
3. hosted CPU native smoke passes;
4. missing/invalid native-backend process tests pass;
5. trusted real Granite success contract passes;
6. three-run repeatability passes;
7. whole-operation cancellation passes;
8. native-load-scoped cancellation is observed or explicitly recorded as not
   observable with the current model and followed by a controlled next route;
9. malformed-input child-process matrix completes without killing the test
   host;
10. locked-model and locked-output failures are controlled;
11. model/fixture hashes remain unchanged after every process scenario;
12. no complete local model path or full chat template leaks into evidence;
13. no listening HTTP/TCP port or established socket is observed;
14. no model file is included in an uploaded artifact;
15. READMEs and the coverage matrix match the fresh evidence.

## 23. Implementation sequence

The implementation plan will order work as follows:

```text
1. Add coverage matrix and reorganise deterministic tests
2. Extract pure failure/redaction/finalisation/chat-template helpers
3. Complete deterministic edge-case coverage
4. Add shared child-process support
5. Add hosted native integration project and workflow coverage
6. Add controlled-model manifest and trusted integration project
7. Add whole-operation and native-load cancellation diagnostics
8. Add malformed/file-access/security process matrices
9. Add trusted workflow
10. Run hosted and target verification
11. Reconcile every README and coverage status
```

Implementation does not begin until this written design has been reviewed and
accepted.

## 24. Textbook basis

- **The Art of Unit Testing** — separate deterministic unit tests from tests
  that require native libraries, files, processes or operating-system state;
  preserve readable, trustworthy tests with one clear reason to fail.
- **Why Programs Fail** — isolate the first failing boundary and contain native
  process termination so one malformed input does not destroy the diagnostic
  environment.
- **Code Complete** — extract cohesive helpers for parsing, redaction, failure
  mapping, finalisation and process execution rather than growing one large
  probe or test class.
- **Designing Secure Software** — treat GGUF files and native libraries as
  untrusted input, minimise privileges, preserve original files, redact local
  paths, avoid network listeners and retain auditable evidence.
- **Fundamentals of Software Architecture** — keep deterministic tests, hosted
  native tests, trusted real-model tests, UI, and later Vulkan/TurboQuant gates
  as independently understandable components with explicit contracts.
