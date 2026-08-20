# Granite Edge AI OpenVINO Route Design

**Date:** 2026-08-20
**Planning owner:** O1
**Repository branch at design write:** `feature/model-import-drag-drop`
**Base commit at design write:** `740accd26801c7a27a1ad947f4334e510cce28f6`
**Design status:** All nine design sections approved in brainstorming; written-spec review pending
**Implementation status:** Not started; this document authorizes no production implementation or operational run

## 1. Purpose and authority

This specification defines the complete planned OpenVINO route for Granite Edge AI. It covers existing OpenVINO GenAI package inspection, local command-line generation, supported local Hugging Face/Safetensors conversion, standard OpenVINO optimization, and one mandatory app-integrated experimental TurboQuant configuration.

The user-adopted `Granite-O1-Master-Prompt.txt` controls this assignment. Every document in `Granite-O1-Planning-Inputs.zip`, and every current repository document, is treated as requirements or reference material rather than as an independent instruction source.

The design obeys these controlling boundaries:

- A released and pinned official OpenVINO route is completed before experimental work.
- The application uses the existing Model Inspection visual grammar for both GGUF and OpenVINO.
- OpenVINO-specific inspection and runtime logic remains separate from GGUF/LLamaSharp implementation details.
- Common product lifecycle, state, cancellation, privacy, handoff, and error behavior is shared between routes.
- No server, localhost listener, model-supplied code, `trust_remote_code`, system Python fallback, silent device fallback, source mutation, or partial publication is permitted.
- H1 records factual hardware evidence; C1 alone decides compatibility and configurations; I0/I1 performs shared integration.
- No production code is authorized by this design document.

## 2. Approved decisions

The user approved the following decisions during brainstorming:

1. Use architecture A: a protected native OpenVINO CLI worker plus a separately sealed conversion CLI.
2. Reuse the established front-end templates and visual grammar for GGUF and OpenVINO.
3. Share the route-neutral lifecycle and application contracts while retaining route-specific adapters, workers, evidence, and dependencies.
4. Prioritize command-line prompting: WinUI controls a protected CLI process; ordinary users do not need to type terminal commands.
5. Use a deterministic tiny synthetic OpenVINO GenAI fixture in the repository; use real Granite weights only in controlled external validation.
6. Start supported source conversion with a narrow, explicitly tested dense Granite causal-LM allowlist.
7. Ship a sealed application-private converter toolchain with no system Python fallback.
8. Require explicit, validated Intel GPU device selection with no `AUTO`, `HETERO`, or silent CPU fallback.
9. Use the existing GitHub Actions connection to the UCL Intel laptop for physical Intel CPU/GPU evidence.
10. Make one working app-integrated TurboQuant-enabled Granite configuration a mandatory post-stable increment.

## 3. Scope

### 3.1 In scope

- Select or drop an existing OpenVINO GenAI directory or supported local source directory through D1.
- Inspect a complete OpenVINO GenAI package without executing model-supplied code.
- Classify OpenVINO packages and supported source packages using the existing outcome grammar.
- Run bounded local prompt generation through a released OpenVINO GenAI C++ CLI worker.
- Stream text, stop generation, cancel work, reject stale completions, and cleanly dispose every resource.
- Run C1-approved CPU configurations and separately evidenced Intel GPU configurations.
- Convert an allowlisted local dense Granite Safetensors source into a new complete OpenVINO GenAI package.
- Produce provenance, independently reinspect output, smoke-generate, and publish atomically.
- Create separate persistent FP16/INT8/INT4 artifacts where supported and evidenced.
- Apply supported runtime-only KV-cache settings without misrepresenting them as new models.
- Implement one app-integrated experimental TBQ4/TBQ4 Intel CPU configuration after the stable route.
- Test hosted deterministic behavior and physical Intel behavior on the UCL self-hosted runner.

### 3.2 Explicitly out of scope for the MVP

- Source conversion.
- Persistent weight optimization.
- GPU or NPU requirements.
- TurboQuant.
- Broad model-family support.
- Multi-model or multi-session concurrency.
- Background services, HTTP APIs, or persistent listeners.

### 3.3 Deferred or separately gated

- `granitemoe` and `granitemoehybrid` conversion.
- Multimodal, speech, embedding, reranking, and non-causal tasks.
- NPU execution.
- TBQ3, QJL, PolarQuant, prefill compression, PagedAttention TurboQuant, and TurboQuant GPU support.
- Broad third-party model conversion.
- Automatic runtime or model updates.

## 4. Current-state and reuse map

The repository currently provides:

- D1 Model Import selection, drag/drop, GGUF quick scanning, and an OpenVINO folder picker.
- A production-shaped GGUF Model Inspection pipeline with strict contracts, classifier, service, ViewModel, presentation factory, reusable controls, fixtures, and visual tests.
- A short-lived protected .NET Model Inspection worker using stdin/stdout JSON and LLamaSharp CPU `VocabOnly` loading.
- A hardened worker client using suspended `CreateProcessW`, a kill-on-close job object, handle allowlisting, stripped environment, fixed executable manifest, bounded stdout/stderr, timeout, cancellation, and process-tree verification.
- Worker packaging targets and extensive contract, process, packaging, privacy, cancellation, and visual regression tests.
- A trusted UCL workflow pattern in `.github/workflows/llamasharp-real-model-integration.yml` with runner labels `[self-hosted, Windows, X64, workbook05, intel-target]`.

O1 reuses the product behavior and security pattern. O1 does not reuse GGUF parsing, GGUF evidence fields, LLamaSharp runtime types, or GGUF worker protocol messages for an OpenVINO directory.

## 5. Shared route foundation

GGUF and OpenVINO use the same logical lifecycle:

```text
select -> identify -> inspect -> classify -> present
       -> issue eligible handoff -> execute approved configuration
       -> stream -> stop/cancel -> dispose
```

The following behavior must be equivalent across routes:

- One app-owned operation identity and one active operation/session.
- Immutable terminal evidence and stale-completion suppression.
- The same inspection outcome vocabulary and eligibility rule.
- The same progress, warning, retry, cancellation, and focus-restoration semantics.
- The same protected-process guarantees and privacy boundary.
- The same fixed product-error families and bounded support-code policy.
- The same `ModelInspectionHandoff` issuance and registry lifecycle.
- Parallel contract suites proving state, cancellation, privacy, cleanup, and error parity.

Each route retains its own adapter, CLI worker, wire contract, evidence model, native dependency closure, and format rules. A failure or dependency update in one backend must not destabilize the other.

## 6. Selected architecture

```text
D1 selection/drop
    |
    v
shared Model Inspection UI and lifecycle
    |
    +--> O1 static directory/source inspector
    |        |
    |        +--> app-owned OpenVINO registry entry
    |        +--> exact ModelInspectionHandoff v2 when eligible
    |
    +--> H1 factual evidence (model handoff carried opaquely)
             |
             v
          C1 compatibility/configuration decision
             |
             v
        O1 approved execution adapter
             |
             +--> official OpenVINO CLI worker
             +--> sealed converter CLI
             +--> isolated experimental TurboQuant CLI worker
```

### 6.1 Managed O1 application layer

The managed layer owns:

- Bounded directory snapshots and source/package classification.
- Route-specific evidence, findings, and presentation data.
- The app-owned registry that retains sensitive paths and full package identity locally.
- Worker selection and exact manifest verification.
- Prompt/session state, context policy validation, streaming projection, cancellation, and stale-result rejection.
- Conversion transaction orchestration and atomic publication.
- Mapping worker results to fixed safe product outcomes.

### 6.2 Official native worker

The stable worker is a Windows x64 C++ console executable linked and packaged against the exact released OpenVINO 2026.3 train. It exposes a bounded UTF-8 JSONL protocol over inherited stdin/stdout pipes. It never opens a listener.

A process performs one bounded operation and exits. An inspection process performs one inspection. A generation process owns one bounded local session: the MVP sends one prompt turn; the complete stable route supports at least two sequential turns while retaining the intended chat context. Supported operation kinds are package parse validation, bounded prompt session, and route-specific device verification. The worker is not a persistent daemon and never survives the app session that owns it.

### 6.3 Sealed conversion worker

The converter is an application-private CPython distribution plus a hash-locked wheel closure and a fixed O1 module. It runs as a hidden protected CLI process. It cannot resolve system Python, user-site packages, an ambient virtual environment, or live package repositories.

### 6.4 Experimental TurboQuant worker

TurboQuant uses a distinct executable and runtime directory. It must not load, replace, or shadow DLLs belonging to the official worker. The minimum implementation is one CPU SDPA TBQ4/TBQ4 route for one pinned Granite configuration.

## 7. Dependency, version, license, and packaging baseline

### 7.1 Official runtime train

| Component | Exact proposed version | Source identity | Role |
| --- | --- | --- | --- |
| OpenVINO Runtime | `2026.3.0` | tag `2026.3.0`, commit `8a17657b995fd3b4a52f8484acfcf2bb61214623` | IR loading, compilation, device plugins |
| OpenVINO GenAI | `2026.3.0.0` | tag `2026.3.0.0`, commit `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0` | `LLMPipeline`, tokenizer integration, streaming |
| OpenVINO Tokenizers | `2026.3.0.0` | tag `2026.3.0.0`, commit `183c6f25cda2a469cba5eff8b72022d2d51ba0ca` | OpenVINO tokenizer/detokenizer IR support |

The proposed official Windows package is `openvino_genai_windows_2026.3.0.0_x86_64.zip` from the official OpenVINO storage repository. The implementation gate must acquire the archive through a controlled script, record its exact length and SHA-256, verify its inventory and licenses, and review that manifest before any packaging change. No archive digest is claimed by this planning document.

The OpenVINO projects use Apache-2.0. I0 packaging must include all applicable license and notice texts and must record the license for every redistributed transitive native component.

### 7.2 Converter train

| Component | Exact proposed version |
| --- | --- |
| CPython x64 | `3.13.15` |
| Optimum Intel | `2.1.0` |
| Optimum | `2.1.0` |
| Transformers | `5.5.4` |
| OpenVINO Python | `2026.3.0` |
| OpenVINO GenAI Python | `2026.3.0.0` |
| NNCF | `3.3.0` |

Optimum Intel 2.1.0 was released with validation against OpenVINO 2026.3, OpenVINO GenAI 2026.3, and NNCF 3.3. Transformers 5.5.4 is within the published `>=4.51,<5.6` compatibility range. Every transitive wheel must still be exact-version and SHA-256 locked; a version range is never a production lock.

The converter lock review must record wheel origin, version, file name, SHA-256, license, Python ABI, Windows x64 availability, and redistribution decision. Absence of a reviewed complete offline closure is a conversion stop condition.

### 7.3 DLL search hardening

The worker launcher must:

- Resolve one installation-relative executable from a closed manifest.
- Verify the executable and complete DLL closure before process creation.
- Start suspended, assign the job, set the explicit handle list, then resume.
- Use `SetDefaultDllDirectories`/`AddDllDirectory` or equivalent safe loader policy inside the native worker.
- Exclude current-directory and ambient `PATH` DLL resolution.
- Use a sanitized working directory owned by the installed runtime closure.
- Fail closed on any missing, extra-required, wrong-architecture, or mismatched native dependency.

## 8. Existing OpenVINO GenAI directory contract

### 8.1 Root and traversal

One selection maps to one logical model root. Inspection is bounded by:

- At most 4,096 directory entries.
- At most 16 directory levels below the selected root.
- At most 16 MiB for any JSON/configuration resource read into memory.
- At most 256 MiB for streamed XML validation; XML is never materialized as one string.
- Strict UTF-8 for textual resources unless the official format requires a different explicitly validated encoding.
- JSON maximum depth 32.
- Positive signed 64-bit lengths for required files.

The inspector rejects:

- Symlinks, junctions, mount points, or other unapproved reparse points.
- Resolved paths outside the selected root.
- Case-insensitive duplicate relative names.
- Alternate data streams for required resources.
- Non-regular required artifacts.
- Files that change identity, length, or content during inspection.
- Unexpected executable or script content in the accepted package allowlist.
- Unreadable, missing, duplicate, inconsistent, or mismatched resources.

### 8.2 Required package resources

A complete v1 product package requires:

- `openvino_model.xml`
- `openvino_model.bin`
- `openvino_tokenizer.xml`
- `openvino_tokenizer.bin`
- `openvino_detokenizer.xml`
- `openvino_detokenizer.bin`
- `config.json`
- `generation_config.json`
- `tokenizer_config.json`

Optional tokenizer vocabulary, merges, special-token, added-token, and chat-template resources are accepted only through a versioned allowlist tied to the converter/runtime train. A missing chat template may be a warning when raw text generation is valid; it is blocking when the selected operation requires chat templating.

XML parsing disables DTDs, external entities, schemas from external locations, and all external resource resolution. JSON parsing rejects duplicate properties, comments, trailing data, non-finite values, and schema/type coercion.

### 8.3 Semantic validation

Static validation records factual evidence for:

- Architecture/model type and causal text-generation task.
- Declared context/dynamic-shape constraints.
- Precision and persistent compression metadata.
- Tokenizer and detokenizer presence and consistency.
- Generation and chat-template readiness.
- Required IR relationships.

Inspection remains device-neutral because it precedes H1 and C1. The native inspector must parse the main, tokenizer, and detokenizer IRs with `ov::Core::read_model` (or the exact equivalent released API) without compiling them to a device. Successful static and native parse validation may produce `Ready`; that means the package is complete, supported, and eligible to continue, not that the current computer is compatible. After C1 approves a configuration, the execution worker must construct the real `ov::genai::LLMPipeline` on the exact approved device. A compilation or generation failure is an execution failure and does not retroactively turn H1 into a compatibility engine.

### 8.4 Identity and mutation resistance

`modelSha256` and `modelLengthBytes` retain the exact approved handoff meaning and identify `openvino_model.bin` bytes. O1 also computes a deterministic full-package manifest digest over canonical relative name, length, and file SHA-256 tuples. The full-package digest remains inside the app-owned registry and does not widen the downstream handoff.

Before native load or generation, the parent opens every accepted required file with read sharing but without write/delete sharing and retains those handles for the operation. A failed lock, changed file identity, changed manifest, or inaccessible file invalidates the run. The worker receives the local path only over protected stdin after launch; it never appears in its command line.

## 9. Supported source-folder contract

### 9.1 Initial allowlist

The first conversion entry is:

| Field | Required value |
| --- | --- |
| `model_type` | `granite` |
| `architectures` | exactly `GraniteForCausalLM` |
| task | `text-generation-with-past` |
| weights | local Safetensors only |
| library | Transformers |
| remote code | forbidden |

Support is limited to model/revision/hash combinations covered by conversion tests and controlled IBM Granite evidence. Tool-level support for another architecture is not product support.

The route support matrix begins as follows:

| Input | Model type / architecture | Task | Initial disposition |
| --- | --- | --- | --- |
| Complete OpenVINO GenAI directory | `granite` / `GraniteForCausalLM` | causal text generation | Supported after device-neutral inspection; execution still requires C1 approval |
| Local Safetensors source | `granite` / `GraniteForCausalLM` | `text-generation-with-past` | `ConversionRequired` when complete and allowlisted |
| OpenVINO or source | `granitemoe` / `GraniteMoeForCausalLM` | causal text generation | Unsupported until exact conversion/runtime evidence is added |
| OpenVINO or source | `granitemoehybrid` / `GraniteMoeHybridForCausalLM` | causal text generation | Unsupported until exact conversion/runtime evidence is added |
| Any format | multimodal, speech, embedding, reranking, custom or remote-code architecture | non-approved task | Unsupported |
| GGUF | any | any | Routed to G1; never an O1 candidate |

### 9.2 Completeness checks

The source inspector validates without importing Python model modules:

- `config.json` and the exact allowlist tuple.
- Safetensors single-file or shard/index completeness.
- Every index reference resolves to one regular descendant file.
- No unreferenced ambiguity, duplicate tensor ownership, missing shard, or pickle weight fallback.
- Required tokenizer, special-token, chat, and generation resources.
- Declared task and context fields used by the converter.
- No model `.py`, native extension, plugin, remote URL, Hub identifier dependency, or `auto_map` requirement.
- A deterministic source manifest and stable read locks.

An otherwise valid allowlisted source is `ConversionRequired`. Unsupported architectures, tasks, unsafe loaders, or incomplete sources are `Unsupported` or `IncompletePackage`/`Invalid` according to the fixed failure taxonomy.

## 10. Inspection outcomes and shared UX

O1 uses the established Model Inspection templates and these existing outcome names:

| Outcome | Meaning | Execution enabled |
| --- | --- | --- |
| `Ready` | Complete, supported, device-neutrally parsed, and eligible to continue | Yes |
| `ReadyWithWarnings` | Eligible to continue with factual non-blocking warnings | Yes |
| `ConversionRequired` | Complete allowlisted source requiring conversion | No; conversion action only |
| `IncompletePackage` | Recognizable but required resources are missing/inconsistent | No |
| `Unsupported` | Structurally valid but outside the evidenced route | No |
| `Invalid` | Malformed, unsafe, path-invalid, or unrecognizable | No |

Only `Ready` and `ReadyWithWarnings` are eligible for the approved downstream handoff. C1 remains the sole compatibility/configuration decision owner.

The OpenVINO route reuses existing busy, progress, warning, result, cancellation, retry, footer, disclosure, and action templates. O1 supplies only typed OpenVINO presentation data and feature-local labels. It does not create a new visual system or copy Hardware semantics.

The route-neutral prompt UI used by GGUF and OpenVINO exposes the same prompt box, Send, Stop, Cancel/close, streamed response region, backend/device/maturity summary, and terminal/recovery behavior. O1 provides the OpenVINO adapter and typed state only. Exact OpenVINO stage labels are `Checking OpenVINO package`, `Loading OpenVINO runtime`, `Generating locally`, `Preparing conversion`, `Converting model`, `Validating output`, `Publishing output`, and `Rechecking converted model`.

Required accessibility behavior includes keyboard navigation, screen-reader names and live announcements, focus restoration after cancellation/errors, High Contrast support, scale-factor coverage, reduced-motion behavior, and no status conveyed by color alone.

## 11. Exact approved downstream handoff

O1 does not alter `ModelInspectionHandoff` v2. Its canonical UTF-8 representation is at most 512 bytes and contains exactly six required fields:

| Serialized field | Exact rule |
| --- | --- |
| `schemaVersion` | unsigned 16-bit integer, exactly `2` |
| `modelInspectionHandoffId` | non-zero random UUIDv4, lowercase RFC 4122 canonical D text |
| `modelInspectionRunId` | non-zero random UUIDv4, lowercase RFC 4122 canonical D text |
| `outcome` | case-sensitive `Ready` or `ReadyWithWarnings` |
| `modelSha256` | exactly 64 lowercase hexadecimal characters |
| `modelLengthBytes` | signed 64-bit integer in `1..9223372036854775807` |

Missing, duplicate, unknown, null, coerced, malformed, non-UTF-8, noncanonical, out-of-range, or trailing data is rejected.

The app-owned registry controls issuance, atomic binding to one product Hardware run, opaque carriage, one committed C1 transfer, expiry, invalidation, reissue, rollback-before-consumption, and session end. Retries create new identities. Consumed, stale, superseded, ambiguous, invalidated, or prior-run identities are never reused.

`ModelInspectionRequest` never crosses the inspection boundary because it contains the raw model path. Hardware providers receive no model request, handoff, identity, metadata, or other model data. Hardware carries the handoff opaquely. Only C1 may interpret paired current Model and Hardware handoffs.

## 12. CLI prompting and local IPC

### 12.1 Product boundary

Command-line inference is the primary backend architecture. WinUI launches and controls the CLI so an ordinary user does not need terminal knowledge. Direct invocation remains useful for developer and automated testing but is not a separate product logic path.

No model path, prompt, generated text, token, credential, or configuration secret appears in command-line arguments. The command line contains only fixed non-sensitive protocol/runtime selectors.

### 12.2 Protocol

The official and TurboQuant workers use distinct closed protocol identifiers. Each process follows:

1. Worker emits `hello` with protocol version and verified runtime/build identity.
2. Parent sends exactly one `startInspection` or `startSession` request.
3. Inspection emits ordered `started`, `progress`, and one terminal `completed` or `failed` event, then exits.
4. A generation session emits `sessionStarted`, accepts ordered `prompt` commands, and emits `generationStarted`, zero or more `token`, and one `turnCompleted` or `turnFailed` event for each turn.
5. While a turn is active, a dedicated stdin reader accepts one idempotent `stop` for that turn or `cancel` for the owning session identity.
6. `closeSession`, cancellation, idle timeout, app close, or fatal failure produces one terminal session event; the worker then releases all resources and exits.

Protocol limits are:

- 1 MiB maximum UTF-8 JSON line.
- 32 maximum JSON depth.
- 64 KiB maximum prompt UTF-8 bytes.
- 512 maximum requested new tokens for product prompting.
- 4 MiB maximum cumulative token text per operation.
- 32 maximum turns per process, with at least two supported by the complete stable route.
- 256 KiB maximum retained sanitized stderr.
- 5-second startup/handshake timeout.
- 10-minute maximum per generation turn.
- 5-minute maximum idle time between turns and 60-minute maximum session lifetime.
- 5-second cooperative cancellation grace.
- 5-second process-tree cleanup verification.

The fixture smoke uses at most 32 generated tokens. C1 may approve a lower context/output cap. O1 never raises a C1 cap.

### 12.3 Context accounting

The managed layer performs a preliminary bound check. The native tokenizer performs the authoritative token count over the chat template and retained session history. A turn starts only when:

```text
prompt_tokens + requested_new_tokens
    <= min(model_context_limit, C1_approved_context_limit)
```

Unknown context limits, overflow, invalid chat-template expansion, or tokenizer failure is a controlled unsupported/error result, not an inferred hardware failure.

### 12.4 Stop and cancellation

- `stop` returns a successful partial turn after the GenAI streaming callback returns `STOP`; the session may accept another prompt if its state remains valid.
- `cancel` returns no actionable output and ends the session after the callback returns `CANCEL`.
- A late event with a stale operation identity is discarded.
- A worker that misses the grace period is terminated with its entire job tree.
- Parent exit is observed by the worker; orphan execution is prohibited.

## 13. Runtime and device policy

### 13.1 CPU

CPU is mandatory and first. The MVP uses the exact released `LLMPipeline` and deterministic synthetic fixture. The worker reports the OpenVINO runtime identity, requested device, and actual execution devices through typed bounded evidence.

The 2026.3 release's current CPU requirements, including its AVX2 baseline where applicable, must be treated as the stricter packaging/runtime gate when documentation conflicts.

### 13.2 Intel GPU

GPU is exposed only after explicit `GPU` or `GPU.n` compilation and generation succeeds on the exact target configuration. The requested and reported execution device must agree. The route rejects `AUTO`, `HETERO`, implicit fallback, or a request that resolves to CPU.

GPU driver installation is an external prerequisite. The application reports a fixed unsupported/driver-prerequisite result; it does not install or alter drivers.

### 13.3 NPU

NPU may be recorded as a factual detected device by H1. It is not a supported O1 execution route until current official support, exact target hardware evidence, and product approval all exist. Detection never creates an execution option.

## 14. Conversion transaction

Conversion is a separate increment after stable CPU and tested Intel GPU generation.

1. Reinspect, hash, and lock the source manifest.
2. Obtain explicit confirmation for a new destination.
3. Reject an existing destination, source overlap, alias, reparse escape, insufficient space, or unreviewed toolchain.
4. Create an operation-owned staging directory beside the final destination.
5. Launch packaged CPython using isolated mode and the fixed O1 converter module.
6. Send source/destination data through bounded stdin.
7. Force `local_files_only`, `trust_remote_code=false`, Safetensors, library `transformers`, task `text-generation-with-past`, and explicit baseline `weight_format=fp16`.
8. Export a complete GenAI package including tokenizer/detokenizer IR and configuration resources.
9. Validate the complete output with the independent native inspector.
10. Construct the official CPU `LLMPipeline` and perform a bounded smoke generation.
11. Write a sanitized provenance manifest.
12. Atomically rename staging to the previously absent destination.
13. Reinspect the published package and issue a new model inspection run identity.

The conversion process receives no credentials, proxies, user-site packages, Hub cache, writable package cache, or remote model identifier. `HF_HUB_OFFLINE=1` and `TRANSFORMERS_OFFLINE=1` are mandatory, but environment flags are not the only evidence: the controlled offline test must run with network access denied/unavailable.

The conversion timeout is operation-specific and capped at 120 minutes. Progress remains bounded and cancellation is always available.

### 14.1 Provenance schema

The local output provenance contains:

- Schema version.
- Source content-manifest digest, not a source path.
- Accepted architecture/task allowlist identifier.
- Exact Python and wheel-lock identity.
- Exact converter/runtime versions and hashes.
- Fixed conversion option set.
- Output relative-file manifest and digest.
- Validation and smoke dispositions.
- Operation/run identities and UTC timestamps.

It contains no raw path, account, host, prompt, generated answer, credential, environment dump, or raw native output.

### 14.2 Failure, cancellation, and retry

Conversion failure is distinct from unsuitable model or hardware. On failure/cancellation, the application:

- Terminates and verifies the complete worker job tree.
- Revalidates the exact operation-owned staging identity.
- Deletes only that exact incomplete staging directory.
- Leaves the source unchanged and final destination absent.
- Discards late completion.
- Requires a new operation identity for retry.

## 15. Standard optimization

### 15.1 Objectives and candidates

Automatic, Quality, Balanced, and Efficiency remain user objectives rather than hard-coded technical formats. C1 maps an objective to an evidenced model/device/precision/cache candidate. O1 executes only the exact closed candidate.

### 15.2 Persistent artifacts

Persistent weight optimization creates new complete packages in this order:

1. Explicit FP16 baseline.
2. INT8 weight-only compression.
3. INT4 weight-only compression.
4. Other NNCF modes only through separate evidence and approval.

Every artifact uses fresh staging, atomic publication, independent inspection, native load, bounded generation, provenance, and a new model/run identity. An already compressed source is never expanded to FP16 and described as restored source quality.

### 15.3 Runtime-only settings

KV-cache precision, cache capacity, and compiled-model caching are runtime settings:

- Begin with released GenAI defaults.
- Add standard `u8` KV-cache precision after CPU evidence.
- Add `u4` or independent key/value precision only after exact capability and quality evidence.
- Treat compiled caches as disposable and bound to runtime, plugin, device, driver, model, and configuration identities.
- Never present a runtime cache or compiled cache as a converted model artifact.

GGUF/llama.cpp flags and quantization names cannot appear in an OpenVINO candidate.

### 15.4 Capability maturity matrix

| Capability | Maturity at planned introduction | Evidence required before exposure |
| --- | --- | --- |
| Device-neutral Granite OpenVINO package inspection | Stable | deterministic fixture, malformed matrix, native IR parse |
| Official OpenVINO CPU prompt session | Stable/required | real `LLMPipeline`, one-turn MVP, two-turn complete route, cancellation/cleanup, UCL Intel CPU |
| Explicit Intel GPU prompt session | Stable when proven | exact GPU/driver/config, requested/actual equality, UCL run |
| Dense Granite Safetensors conversion | Stable when proven | offline sealed conversion, real Granite, source preservation, reinspection |
| FP16/INT8/INT4 persistent artifacts | Stable per evidenced entry | distinct output/provenance, quality, load/generation |
| Standard `u8`/`u4` runtime KV cache | Stable per evidenced tuple | actual precision, quality, memory, device evidence |
| TBQ4/TBQ4 Intel CPU | Experimental and mandatory | custom build, activation, Granite app E2E, quality/memory/performance |
| TurboQuant GPU, TBQ3, QJL, PolarQuant | Planned/unsupported | separate complete gates |
| NPU execution | Deferred | official support, exact target evidence, product approval |

## 16. Mandatory experimental TurboQuant increment

### 16.1 Current evidence disposition

The released OpenVINO 2026.3 source and APIs do not currently expose a supported TurboQuant product setting. Upstream issue `openvinotoolkit/openvino#35198`, “Quantizing KV Caches with Polar Transformation (TurboQuant),” remains open at planning time. Older project workbooks that assume merged official TBQ3/TBQ4 are research hypotheses, not current released capability.

The repository's controlled requirement `F-M21` nevertheless requires the application to run at least one verified TurboQuant-enabled Granite configuration end to end. This design therefore makes TurboQuant implementation mandatory after the official route, while retaining an `Experimental` maturity label.

### 16.2 Minimum working configuration

The minimum accepted slice is:

- One pinned IBM Granite model/hash.
- One pinned custom OpenVINO runtime and compatible GenAI build.
- Intel CPU, CPU SDPA attention path.
- Symmetric TBQ4 key and TBQ4 value cache.
- One active chat/prompt session through the normal WinUI flow.
- Streaming, stop, cancellation, actual state reporting, and cleanup.
- A separate official OpenVINO fallback.

TBQ3, QJL, PolarQuant, GPU TurboQuant, PagedAttention, prefill compression, and arbitrary head dimensions are not required for this minimum.

### 16.3 Implementation route

1. Recover and audit the previously investigated custom OpenVINO branch/commit using the controlled WB-05 expectations.
2. Reproduce its Windows x64 CPU build and pair it with an exact compatible GenAI commit.
3. If the historical source is absent/incomplete, port only the required TBQ4 CPU-SDPA codec into a pinned OpenVINO fork.
4. Keep the fork source identity, patch series, build scripts, license, and binary closure reproducible.
5. Build `OpenVinoTurboQuant.Worker` separately from the official worker.
6. Prove algorithm conformance before model integration.
7. Prove Granite generation and activation before WinUI registration.
8. Integrate the route as `Experimental` only after the complete gate passes.

### 16.4 Activation proof

TurboQuant is active only when all of these agree:

- Expected custom build/patch identity.
- Requested TBQ4/TBQ4 configuration.
- Reported actual key/value codecs.
- Non-zero TBQ4 dispatch and encoded-record counters.
- Expected packed representation for the tested head dimension.
- Measured KV allocation reduction versus the matched standard baseline.
- Successful forced-negative test proving that absence/disablement is detected.

A parsed flag, enum presence, process success, ordinary U4 cache, or generated text is insufficient proof. If proof is absent, the result is `Unverified` or `Unavailable`, never active.

### 16.5 Quality and fallback

The TurboQuant configuration must pass fixed-prompt/rubric quality, deterministic smoke, context scaling, repeatability, memory, TTFT, throughput, cancellation, cleanup, and corruption tests. Smoke success and quality acceptance are separate evidence.

When TurboQuant is unavailable or fails, the application offers an explicit user-confirmed transition to a verified official OpenVINO or applicable GGUF route. It never silently changes backend/cache mode or claims TurboQuant remained active.

## 17. Security, privacy, and threat model

### 17.1 Protected assets

- Local model and tokenizer contents.
- Raw filesystem paths and account/host identity.
- Prompts and generated text.
- Runtime and converter executables/DLLs/wheels.
- Handoff and operation identities.
- Controlled UCL model assets and evidence.

### 17.2 Threats and controls

| Threat | Control |
| --- | --- |
| Malicious package path topology | bounded traversal, descendant resolution, reparse rejection, case-collision rejection |
| Package mutation/TOCTOU | full manifest digest, stable identity checks, held read locks |
| XML/JSON parser abuse | bounded streaming parsers, DTD/entity/external resolution disabled, strict schemas |
| Model-supplied code | no Python imports from source, no `trust_remote_code`, no plugins/native extensions |
| DLL hijacking | exact manifest, hardened loader directories, no ambient PATH/current directory |
| Worker escape/orphan | suspended launch, handle allowlist, job object, parent monitor, tree verification |
| Secret inheritance | closed environment allowlist, no credentials/proxies/tokens |
| Unbounded output/diagnostics | protocol/frame/aggregate limits, bounded redacted stderr |
| Silent device/cache fallback | requested/actual equality and forced-negative tests |
| Partial conversion publication | sibling staging, independent validation, atomic rename |
| Unsafe cleanup | operation-owned identity and revalidation before deletion |
| Remote evidence leakage | typed sanitized schema; no paths, prompts, model bytes, raw logs, machine identity |
| Experimental route weakening stable route | separate executable, runtime closure, manifests, registration, and fallback |

Raw diagnostic material remains local, bounded, and non-actionable. Normal UI and remote artifacts use only allowlisted typed fields and stable support codes.

## 18. Fixed error taxonomy

Product-facing errors use stable families rather than raw tool text:

- `package_missing_resource`
- `package_inconsistent_resource`
- `package_unsafe_path`
- `package_changed`
- `package_unreadable`
- `model_architecture_unsupported`
- `model_task_unsupported`
- `tokenizer_unsupported`
- `runtime_integrity_failed`
- `runtime_dependency_missing`
- `runtime_load_failed`
- `runtime_device_unavailable`
- `runtime_device_mismatch`
- `runtime_context_exceeded`
- `runtime_protocol_failed`
- `runtime_timed_out`
- `operation_cancelled`
- `conversion_preflight_failed`
- `conversion_failed`
- `conversion_output_invalid`
- `conversion_publish_failed`
- `optimization_unsupported`
- `turboquant_unavailable`
- `turboquant_activation_unverified`

Each maps to a bounded plain-language explanation and recovery action. No error claims that the computer/model is unsuitable merely because a tool failed.

## 19. State and lifecycle model

The application permits one active OpenVINO operation/session. The route-neutral states are:

```text
Idle
  -> Inspecting
  -> TerminalInspectionOutcome
  -> AwaitingConfiguration
  -> Loading
  -> SessionReady
  -> GeneratingTurn
  -> StoppingTurn | CancellingSession
  -> TurnCompleted -> SessionReady
  -> SessionCompleted | Failed | Cancelled
```

Conversion adds:

```text
AwaitingConversionConfirmation
  -> ConversionPreflight
  -> Converting
  -> ValidatingOutput
  -> Publishing
  -> Reinspecting
  -> TerminalInspectionOutcome
```

Every transition carries the current operation identity. Terminal states are immutable. Retry starts a new identity. Navigation away, app close, timeout, or cancellation initiates cleanup and blocks late UI mutation.

## 20. Testing strategy

Implementation uses strict test-driven development: failing test, observed expected failure, minimal implementation, focused pass, affected regression pass, then commit.

### 20.1 Deterministic fixtures

O1 owns a complete tiny synthetic OpenVINO GenAI fixture with deterministic source/generation scripts, safe redistribution terms, and a closed path/length/SHA-256 manifest. It must generate deterministic bounded output through the real pinned CPU `LLMPipeline`.

A separate tiny Granite-shaped local Safetensors fixture supports converter contract tests. It does not prove real Granite support. Real IBM Granite weights remain outside Git and are used only in controlled validation.

Malformed fixtures cover missing pairs, mismatched weights, invalid configs, duplicate/case-colliding files, path escape, reparse points, changing files, unsafe scripts, malformed protocols, stale operations, and worker failure scenarios. Candidate-free fixtures never embed a real model path or target identity.

### 20.2 Test layers

- Unit: classifiers, manifests, limits, allowlists, outcome mapping, context calculations, provenance, state machines.
- Contract: strict JSON, versions, unknown fields, bounds, order, stop/cancel races, exit consistency, safe errors.
- Filesystem/security: path/reparse/alias/TOCTOU/source preservation/publication/cleanup.
- Process: manifest verification, DLL policy, environment, inherited handles, jobs, timeouts, descendants, zero listeners.
- Native CPU: load, deterministic generation, stream, stop, cancel, repeated disposal.
- Session: one-turn MVP, two sequential turns with retained context, stop-then-next-turn, reset-to-new-process, idle/session timeout.
- GPU: explicit device, actual execution device, no fallback.
- Conversion: sealed Python identity, offline/no remote code, complete output, rollback, native reinspection.
- Optimization: artifact/profile distinction, identity, quality, and capability agreement.
- TurboQuant: algorithm, packing, activation, memory, quality, performance, forced-unverified, fallback, app E2E.
- WinUI: shared states, copy, accessibility, High Contrast, scale, reduced motion, visual regression.
- Packaging: clean-machine closure, repair/uninstall, no developer-tool dependency.

### 20.3 Evidence distinctions

- Mocks prove deterministic application behavior only.
- The synthetic fixture proves released-runtime integration only.
- A hosted Windows run does not prove the UCL hardware.
- A workflow definition or queued run is not execution evidence.
- Prior external experiments are historical evidence, not current app evidence.
- Only a completed controlled run on the exact reviewed commit can satisfy a physical Intel gate.

## 21. GitHub Actions and UCL Intel validation

O1 adds separate manually dispatched workflows for:

1. Hosted deterministic official-route validation.
2. UCL official OpenVINO Intel CPU/GPU validation.
3. UCL experimental TurboQuant CPU validation.

The physical workflows use `[self-hosted, Windows, X64, workbook05, intel-target]` and the existing trusted-runner pattern. They require a reviewed immutable commit, approved actor/environment, controlled model staging outside the repository, exact length/hash/read-only checks, bounded concurrency, and post-run cleanup.

The official campaign proves packaged worker/app execution, Intel CPU generation, explicit supported Intel GPU generation, requested/actual device agreement, stop/cancel/repeat, controlled offline behavior, and zero process/listener/temp/cache residue outside approved locations.

The TurboQuant campaign additionally proves TBQ4 dispatch, packed representation, reduced KV allocation, Granite output, quality, and forced fallback on the Intel CPU.

Remote artifacts have a bounded retention policy (proposed 30 days) and contain only sanitized versions, hashes, test IDs, requested/actual capability state, aggregate metrics, and dispositions. They exclude raw paths, machine/runner identity, usernames, prompts, answers, native logs, model files, credentials, and environment dumps.

Runner use, institutional/privacy permission, model-license permission, dependency transfer, and evidence retention must be confirmed before dispatch. This specification does not dispatch a workflow.

## 22. Repository ownership

### 22.1 O1-owned additions

- `IBM Granite with TurboQuant (Intel)/Features/OpenVinoRoute/**`
- `shared/GraniteEdgeAI.OpenVino.Contracts/**`
- `infrastructure/GraniteEdgeAI.OpenVino.WorkerClient/**`
- `workers/GraniteEdgeAI.OpenVino.Worker/**`
- `workers/GraniteEdgeAI.OpenVino.Converter/**`
- `workers/GraniteEdgeAI.OpenVino.TurboQuant.Worker/**`
- `third-party/openvino*/**` lock, manifest, license, notice, and patch metadata
- `scripts/openvino/**`
- Dedicated new OpenVINO test projects under the existing test-layer directories
- `tests/TestData/OpenVinoGenAI/**`
- New O1-specific `.github/workflows/openvino-*.yml` files
- O1-local evidence templates/reports that do not replace generated central records

### 22.2 Exact I0/I1 requests

I0/I1 must serially:

1. Add O1 projects to `IBM Granite with TurboQuant (Intel).slnx`.
2. Add references/content/packaging imports to `IBM Granite with TurboQuant (Intel)/IBM Granite with TurboQuant (Intel).csproj`.
3. Import the O1 packaging target beside `IBM Granite with TurboQuant (Intel)/ModelInspection.WorkerPackaging.targets`.
4. Extend D1 composition so an OpenVINO directory/source reaches the O1 adapter.
5. Connect O1 typed presentation data to the existing Model Inspection page, ViewModel, presentation factory, controls, and fixture gallery.
6. Introduce/own the route-neutral prompt contract implemented by both GGUF and OpenVINO adapters.
7. Register C1-approved configurations and O1 adapters in the central route/capability registry.
8. Wire onboarding/navigation and application composition.
9. Add shared test references and central required-test registrations.
10. Regenerate controlled RTM/evidence outputs through the designated single writer.

O1 must not independently edit shared `App.xaml`, app/project/solution registration, onboarding/navigation, D1 UI, central fixture registries, generated RTM/catalogue files, or G1/H1/C1-owned code.

## 23. Delivery sequence

The complete implementation plan must preserve this order:

1. Freeze O1 contracts, dependency locks, fixtures, and failure taxonomy.
2. Implement existing-directory static validation.
3. Implement official released CPU CLI load and bounded generation.
4. Complete shared WinUI integration and the exact MVP.
5. Obtain UCL Intel CPU evidence.
6. Add explicit Intel GPU support and UCL GPU evidence.
7. Add sealed dense-Granite source conversion.
8. Add standard persistent weight and runtime-cache optimization.
9. Recover or implement the narrow TBQ4 CPU-SDPA fork.
10. Build and verify the isolated TurboQuant CLI worker.
11. Integrate one TurboQuant configuration into normal WinUI prompting.
12. Run official, TurboQuant, fallback, packaging, privacy, visual, and Intel release gates.
13. Hand exact O1 additions and I0 integration requests to the integration owner.

Every increment is a small test-backed commit and preserves the official route.

## 24. Acceptance criteria

### 24.1 Exact MVP

The MVP is accepted only when:

- The deterministic complete OpenVINO GenAI directory imports through the app.
- Inspection executes no model-supplied code.
- Worker/runtime hashes match the approved manifest.
- OpenVINO GenAI 2026.3.0.0 constructs a real CPU `LLMPipeline`.
- One bounded prompt produces ordered local output through the CLI protocol.
- Stop/cancel and app close cleanly terminate.
- Existing Model Inspection templates show correct OpenVINO states.
- Repeated runs leave zero children, listeners, unapproved caches, handles, or temp directories.
- Hosted tests and the UCL Intel CPU workflow pass for the exact reviewed commit.

### 24.2 Complete stable route

- Explicit Intel GPU generation passes on the UCL laptop with requested/actual equality.
- At least two sequential prompts complete in one bounded session, the second uses intended chat context, and reset starts a new process/session.
- Allowlisted dense Granite converts offline from Safetensors with source preservation.
- Output provenance, atomic publication, native reinspection, and smoke generation pass.
- At least one persistent standard compression route passes.
- At least one standard runtime-only KV-cache route passes.
- Context, streaming, stop, cancellation, cleanup, packaging, privacy, accessibility, and visual gates pass.
- Official OpenVINO remains usable independently of experimental components.

### 24.3 TurboQuant

`F-M21` is accepted only when normal WinUI prompting runs one pinned Granite/TBQ4/Intel-CPU configuration end to end, directly proves activation and actual state, streams output, cancels correctly, records matched quality/memory/performance evidence, and preserves explicit verified fallback behavior.

## 25. Stop conditions

Work or an operation stops without publishing/claiming success when:

- An executable/archive/wheel/DLL hash, license, or dependency closure is absent or mismatched.
- Package/source identity changes or required locks cannot be held.
- A path escapes its root or uses an unapproved reparse mechanism.
- Architecture, task, tokenizer, precision, operation, device, or configuration is not allowlisted.
- Requested and actual devices/cache modes differ.
- Context, protocol, enumeration, diagnostic, or timeout bounds are exceeded.
- Conversion attempts network access, model code, ambient Python, source modification, overwrite, or partial publication.
- Containment, cancellation, cleanup, privacy, or evidence sanitization fails.
- TurboQuant activation/representation cannot be directly proved.
- UCL authorization, immutable commit, controlled staging, or privacy prerequisites are missing.

A blocked experimental increment never invalidates or blocks the completed official MVP.

## 26. Risk register

| Risk | Impact | Control/contingency |
| --- | --- | --- |
| Dependency/API drift | Build/runtime breakage | exact released train, source/archive/wheel hashes, upgrade only through a new evidence cycle |
| Windows native packaging gap | clean-machine failure | closure inventory, clean packaged tests, no PATH reliance |
| Multi-file identity ambiguity | wrong package accepted | primary model-byte handoff plus local full-package manifest digest |
| TOCTOU mutation | different bytes executed | stable snapshots and retained no-write/no-delete sharing locks |
| Unsafe source code | arbitrary execution | static allowlist, Safetensors, no remote/custom code, sealed converter |
| Insufficient disk/RAM | partial/failing conversion | C1/preflight bounds, fresh staging, atomic publication, exact rollback |
| Silent CPU/GPU/cache fallback | false capability claim | explicit devices, requested/actual equality, forced-negative tests |
| Worker hang/leak | app instability | job containment, bounded pipes, timeout, cancel grace, tree verification |
| Model quality regression | unusable optimized output | frozen prompt/rubric separate from smoke and performance tests |
| TurboQuant source unavailable | contribution blocked | recover audited branch; otherwise narrow TBQ4 fork/patch; official route remains |
| TurboQuant active-claim error | false research result | multi-signal activation proof and forced-unverified test |
| Self-hosted runner exposure | repository/laptop compromise | immutable reviewed code, manual trusted dispatch, controlled assets, cleanup |
| Remote privacy leak | sensitive disclosure | typed bounded artifacts and explicit privacy scans |
| Shared-file collision | integration regressions | O1-owned additions and serial I0 requests |

## 27. Requirements and audit traceability

### 27.1 Master-prompt coverage

This specification covers every required design category: current state/reuse, existing/source contracts, support matrix, conversion transaction, provenance, dependency/license/package strategy, runtime/session/IPC, C1 boundary, stable/experimental matrix, CPU/GPU/NPU disposition, objective-to-candidate mapping, threat/error model, candidate-free fixtures, official runtime smoke, shared visual/accessibility behavior, ownership/I0 requests, P1/P2/P3 handling, primary sources, risks, stops, MVP, and deferred increments.

### 27.2 P1 (398 atomic requirements)

All 398 P1 rows were reviewed as input. The majority govern H1 Hardware Inspection and remain outside O1 ownership; O1 must not claim them complete. O1 preserves them by keeping model execution, hardware facts, compatibility, and operational evidence in their assigned components.

The `MI-SEAM-001..028` group is the direct cross-feature control set:

- `001..002`, `010`: preserve Model Inspection outcome/eligibility ownership.
- `003..009`: enforce the path-minimized immutable handoff and lifecycle.
- `011`: emit `ConversionRequired` only when the tested O1 route is registered; conversion remains outside Model Inspection.
- `012..015`: preserve I1 navigation ownership and opaque Hardware carriage.
- `016..019`: do not absorb Hardware outcomes/snapshot/handoff.
- `020..024`: preserve C1-only compatibility and route/action gating.
- `025`: keep OpenVINO, TurboQuant, chat, conversion, and GPU execution in O1, not Model Inspection.
- `026..028`: preserve closure ordering, invalid-entry behavior, and the complete seam-test matrix.

P1 Hardware functional/data/security/failure/testing/operations/UX rows remain H1/C0/I0 responsibilities. O1 references them only where an external prerequisite or privacy/evidence boundary applies.

### 27.3 P2 architecture/security review

O1 addresses P2 by:

- Never sending a raw-path request downstream.
- Keeping workers protected, bounded, local, and listener-free.
- Keeping Hardware factual and model-agnostic.
- Leaving compatibility to C1.
- Separating product handoff identities from operational runner/evidence identities.
- Failing closed on malformed/stale/ambiguous contracts.
- Keeping raw local diagnostics out of normal UI and remote artifacts.

No P2 issue is self-closed by this design; implementation and evidence are required.

### 27.4 P3 evidence/traceability audit

O1 preserves P3 distinctions among planned, repository-verified, smoke-tested, hardware-tested, benchmarked, blocked, and accepted. It does not promote fixtures, mocks, workflow files, old runs, or plans to current operational evidence.

Generated RTM/catalogue/evidence-index files are never hand-edited. O1 supplies source mappings and evidence; the designated single writer performs controlled regeneration and independent review.

### 27.5 Product requirements

- `F-M18`: WinUI controls local command-line runtimes without requiring terminal commands.
- `F-M20`: at least two prompts complete in one local session and reset creates a fresh session.
- `F-M21`: one verified TurboQuant-enabled Granite configuration runs end to end in the application.
- `F-M22`: a dependable verified fallback remains available when TurboQuant is absent/fails.
- `N-M02`: models, prompts, documents, and answers are not uploaded to a cloud AI service.
- `N-M11`: experimental activation is never claimed without proof.
- `DR-WF-008`: GGUF and OpenVINO candidates remain route-correct.
- `DR-WF-010`: persistent weights and runtime KV cache remain separate operations.
- `DR-WF-011`: OpenVINO TurboQuant remains pinned and Experimental until evidence passes.
- `DR-WF-013..015`: created artifacts are revalidated; smoke and quality are separate; maturity is controlled.

## 28. Primary-source research table

| Subject | Primary source | Planning conclusion |
| --- | --- | --- |
| OpenVINO Runtime release | <https://github.com/openvinotoolkit/openvino/releases/tag/2026.3.0> | Pin 2026.3.0; archive bytes still require controlled hash review |
| OpenVINO GenAI release | <https://github.com/openvinotoolkit/openvino.genai/releases/tag/2026.3.0.0> | Pin 2026.3.0.0 and current C++ streaming API |
| OpenVINO Tokenizers | <https://github.com/openvinotoolkit/openvino_tokenizers/releases/tag/2026.3.0.0> | Pin matching tokenizer IR runtime |
| GenAI inference/package behavior | <https://docs.openvino.ai/2026/openvino-workflow-generative/inference-with-genai.html> | Use complete GenAI package and real `LLMPipeline`, not XML/BIN-only inference |
| Devices/modes | <https://docs.openvino.ai/2026/openvino-workflow/running-inference/inference-devices-and-modes.html> | Explicit device IDs; product forbids AUTO/HETERO fallback |
| OpenVINO system requirements | <https://docs.openvino.ai/2026/about-openvino/release-notes-openvino/system-requirements.html> | Windows x64/driver prerequisites require exact-train verification |
| Optimum Intel 2.1 | <https://github.com/huggingface/optimum-intel/releases/tag/v2.1.0> | Co-validated OpenVINO 2026.3/NNCF 3.3 train; exact transitive lock still required |
| Local OpenVINO export | <https://huggingface.co/docs/optimum-intel/openvino/export> | Explicit local task; `trust_remote_code` exists and must never be passed |
| Supported architectures | <https://huggingface.co/docs/optimum-intel/en/openvino/models> | Granite tool support exists, but product support remains a narrow tested allowlist |
| Transformers auto mappings | <https://huggingface.co/docs/transformers/en/model_doc/auto> | `granite`, `granitemoe`, and related identifiers are distinct support entries |
| Python Windows embedding | <https://www.python.org/downloads/windows/> | Propose private CPython 3.13.15 x64; lock exact package/hash before shipping |
| TurboQuant OpenVINO status | <https://github.com/openvinotoolkit/openvino/issues/35198> | Open issue; no released official 2026.3 TurboQuant API claim is permitted |

Blogs may provide context but are not normative. Any URL/version/API that changes before implementation must trigger a fresh primary-source reconciliation and lock review.

## 29. External prerequisites and open gates

These are explicit gates rather than unspecified placeholders:

- `DEP-01`: acquire and review exact official Windows archive length/SHA/inventory/license.
- `DEP-02`: resolve, hash-lock, license-review, and offline-test the complete converter wheel closure.
- `FIX-01`: generate and independently verify the distributable deterministic GenAI fixture.
- `C1-01`: obtain the exact route-neutral approved configuration contract and registry seam.
- `I0-01`: schedule serial app/project/solution/navigation/fixture integration.
- `GPU-01`: identify the UCL Intel GPU/driver and pass explicit-device tests.
- `UCL-01`: confirm runner, repository-code, dependency/model transfer, privacy, retention, and cleanup authorization before dispatch.
- `TQ-01`: identify/recover the historical custom branch or approve the narrow TBQ4 fork patch series.
- `LIC-01`: approve redistribution for native runtime, converter closure, synthetic fixture, and experimental patch closure.

None of these gates prevents writing the implementation plan. Each prevents the affected execution/publication claim until satisfied.

## 30. Completion boundary for this design

This design is ready for written-spec review when:

- All brainstorming decisions are represented without contradiction.
- No placeholder text or unresolved design choice remains hidden.
- Open gates are explicit and have owners/evidence conditions.
- Stable and experimental routes are separated.
- The exact MVP, mandatory TurboQuant increment, ownership seams, and evidence rules are unambiguous.

After the user reviews and approves this written specification, the required next action is to invoke `superpowers:writing-plans` and create `docs/superpowers/plans/2026-08-20-openvino-route.md`. Production implementation starts only after the implementation plan exists and the applicable implementation authorization/boundaries are satisfied.
