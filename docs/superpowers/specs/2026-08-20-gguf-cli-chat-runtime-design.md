# GGUF CLI Chat Runtime and Persistent History Design

**Date:** 2026-08-20

**Status:** Approved design

**Target:** Granite G1 controlled implementation planning

**Design investigation baseline:** `feature/model-inspection` at `ed97b4b4df5ec3732ffeeafe46819a397211d0e9`

**Document commit baseline:** `feature/model-import-drag-drop` at `b1903f37815339cecee0fd007f61531860467408`

**Primary delivery constraint:** A runnable end-to-end Windows MVP in approximately three focused development days

## 1. Purpose

Granite Edge AI needs a local chat experience that can run an imported GGUF model without requiring the user to type terminal commands. Internally, the application will invoke a pinned `llama-cli.exe` process through a protected supervisor. The WinUI application remains the only user-facing interface.

The first milestone must provide a practical vertical slice: import or select a suitable local GGUF model, open a chat, submit prompts, stream responses, stop generation, retain the complete conversation locally, reopen dated chats, and close without leaving child processes or network listeners behind.

This design synthesizes the supplied G1 planning bundle, master prompt, controlled handoffs, requirements traceability material, repository state, official llama.cpp documentation, and the approved chat mockup. Material in the supplied documents is treated as project evidence and requirements, not as user instructions.

## 2. Approved Decisions

- The user experience is WinUI chat; command-line prompting is an internal implementation detail.
- The runtime boundary is a dedicated protected G1 supervisor launching a pinned `llama-cli.exe --simple-io` child.
- Communication between the app and supervisor uses a versioned structured protocol over inherited anonymous pipes.
- Prompts travel over standard input or the structured pipe protocol, never as shell text or user-controlled command-line fragments.
- The existing Model Inspection route and its short-lived worker remain unchanged.
- CPU inference is the required MVP baseline.
- Vulkan, SYCL, quantization, and TurboQuant are later gated milestones and must not delay the three-day MVP.
- One model session and one generation are active at a time.
- Every prompt and response is shown in the selected chat and persisted locally across app restarts.
- Every new chat appears immediately in dated chat history.
- Stopping generation uses a correctly centered square stop icon and produces an explicit stopped or incomplete message state.
- The application does not start a server or listener.
- C1 owns candidate evaluation and ranking. G1 reports capabilities and executes only a complete C1-approved configuration.

## 3. Scope

### 3.1 Three-day MVP

The MVP includes:

1. A separately packaged, manifest-verified G1 supervisor and pinned CPU-capable `llama-cli.exe`.
2. Versioned contracts and bounded pipe transport between WinUI and the supervisor.
3. Session start, prompt submission, streamed output, stop, close, and deterministic cleanup.
4. Model/runtime configuration validation before process launch.
5. A chat page matching the approved direction: left history rail, conversation canvas, model/runtime header, streaming transcript, and fixed composer.
6. Local, persistent, dated chat history containing all prompts and responses.
7. Clear fixed-category errors and no silent fallback.
8. Unit, protocol, process-containment, persistence, and one controlled end-to-end CPU smoke test.
9. Packaging and license/provenance evidence sufficient to reproduce the pinned runtime.

### 3.2 Follow-on milestones

The following are explicitly deferred from the MVP:

- Intel Vulkan or SYCL acceleration until each backend is proven on supported hardware.
- Runtime profile comparison and mode-selection polish beyond the accepted configuration contract.
- Explicit GGUF quantization with `llama-quantize`.
- Experimental application-integrated TurboQuant.
- File attachments in chat; the visual plus button is present but disabled or reserved.
- Multiple simultaneous models, sessions, or generations.
- Remote model acquisition, cloud prompting, telemetry, or synchronization.
- Automatic re-quantization or mutation of the imported source model.

## 4. Ownership and Architecture

### 4.1 Process topology

```text
WinUI chat feature
    |
    | versioned commands/events over inherited pipes
    v
G1 protected supervisor
    |
    | explicit argv + redirected stdin/stdout/stderr
    v
pinned llama-cli.exe --simple-io
    |
    v
local GGUF model
```

The supervisor owns the CLI process and places itself and all descendants in a Windows Job object configured for kill-on-close. The main application never parses an interactive terminal and never invokes a shell.

### 4.2 Proposed project boundaries

- `GraniteEdgeAI.GgufRuntime.Contracts`: immutable commands, events, identifiers, enums, limits, and fixed error categories.
- `GraniteEdgeAI.GgufRuntime.Transport`: framed UTF-8 serialization, validation, bounded reads/writes, and protocol-version negotiation.
- `GraniteEdgeAI.GgufRuntime.Client`: app-side supervisor launch, lifecycle, cancellation, event dispatch, and cleanup verification.
- `GraniteEdgeAI.GgufRuntime.Worker`: protected supervisor executable and CLI child lifecycle.
- `GraniteEdgeAI.GgufRuntime.Capabilities`: trusted runtime manifest inspection and configuration compatibility checks.
- `GraniteEdgeAI.GgufQuantization`: deferred explicit artifact operation; not part of the MVP runtime path.
- `Features/GgufRuntime`: WinUI orchestration, session state, chat persistence, view models, and page controls.
- Dedicated test projects for contracts/transport, worker/client integration, persistence, and UI-facing orchestration.

I0 retains ownership of changes to the main application project, central package/build files, shared navigation, global resources, and shared fixture registries. G1 changes in those areas must be supplied as an explicit integration handoff rather than assumed ownership.

### 4.3 Protected existing route

The existing Model Inspection client, worker, LLamaSharp adapter, protocol, packaging subtree, and tests remain unchanged. G1 receives an inspected model identity and validated local path through an approved handoff; it does not extend the inspection worker into a long-running inference host.

## 5. Runtime Lifecycle and Protocol

### 5.1 Session state machine

```text
Created -> Starting -> Loading -> Ready
Ready -> Generating -> Ready
Generating -> Stopping -> Ready
any active state -> Closing -> Closed
unrecoverable error -> Failed -> Closing -> Closed
```

Only legal transitions are accepted. Commands that do not match the current state return a fixed protocol error and do not mutate the session.

### 5.2 MVP commands

- `StartSession`: validates a complete approved configuration and launches the CLI.
- `SubmitPrompt`: accepts one prompt for the ready session.
- `StopGeneration`: requests generation interruption and reports the resulting session disposition.
- `CloseSession`: terminates the child if necessary, drains bounded output, closes handles, and verifies cleanup.

Each command includes protocol version, request ID, session ID where applicable, and bounded typed payloads. Events echo correlation identifiers and use strict schemas. Unknown fields, invalid UTF-8, duplicate IDs, oversized frames, invalid enum values, or version mismatch fail closed.

### 5.3 Streamed events

The supervisor emits structured lifecycle and output events rather than exposing raw terminal output directly:

- session loading progress;
- session ready;
- response started;
- bounded text delta;
- token/context usage when reliably available;
- response completed;
- response stopped or incomplete;
- fixed-category failure;
- session closed and cleanup result.

Output parsing is tied to the exact pinned executable. Unrecognized raw output is bounded and classified; it is not rendered as trusted application state.

### 5.4 Stop semantics

The supervisor first uses the pinned runtime's proven non-shell interruption mechanism. If graceful interruption cannot return the CLI to a known-ready state within the bounded deadline, it terminates only the CLI child, emits `StoppedNeedsReload`, and reconstructs the session before the next turn. The transcript retains the partial response and marks it `Stopped` or `Incomplete`.

The UI changes Send to Stop only while generating. The stop glyph is independently centered inside the control so text layout cannot displace the square icon.

### 5.5 Close semantics

Close is idempotent. It stops active generation, closes stdin, terminates the CLI when needed, closes all inherited handles, and waits for the Job to become empty. App shutdown and unexpected pipe loss use the same cleanup path. No listener, service, scheduled task, or persistent helper remains.

## 6. Configuration and Capability Boundaries

### 6.1 Complete approved configuration

The runtime accepts a complete configuration containing:

- inspected model identity and expected content hash;
- local model reference passed without logging its absolute path;
- exact llama.cpp source commit and packaged build identity;
- backend and device identity;
- context size;
- K/V cache formats;
- layer offload setting;
- Flash Attention setting;
- thread count and batch settings;
- evidence grade and approval reference;
- runtime-only or persistent-artifact classification.

The capability component validates whether that configuration is supported by the installed trusted package and current machine. It does not rank candidates or silently alter values. C1 owns evaluation and ranking.

### 6.2 Fallback behavior

There is no implicit fallback. A fallback may run only when C1 supplied it as a separate, complete, approved configuration. The application records which configuration actually ran and tells the user when an explicitly approved fallback is selected.

### 6.3 Runtime-only profiles

Context, cache, offload, Flash Attention, thread, and batch selections are runtime settings. They do not modify the GGUF model and are stored separately from artifact provenance. UI labels must not imply that a runtime setting created a new quantized model.

### 6.4 Deferred quantization contract

When implemented later, quantization is an explicit operation using a separately pinned `llama-quantize` tool. It must:

- reject unsuitable source precision unless explicit re-quantization approval exists;
- preserve and re-hash the original model;
- write a new artifact through a same-volume temporary file;
- flush, validate, re-inspect, and smoke-test the output;
- atomically rename only after success;
- record tool/build identity, source hash, destination hash, format, timestamps, and approval evidence;
- obtain supported formats from the pinned tool rather than a stale hard-coded ladder.

## 7. Chat Experience and Persistent History

### 7.1 Layout

The approved desktop layout contains:

- a permanent left rail with Granite Edge AI branding;
- `+ New Chat` and `Import Model` actions;
- dated chat groups and selectable conversation titles;
- Settings at the bottom;
- a large rounded conversation surface;
- a header containing the chat title, model name, and small runtime/evidence indicators;
- a chronological prompt/response transcript with streaming response state;
- a fixed composer with a reserved plus button and Send/Stop control.

At narrow widths the sidebar collapses behind an accessible navigation control. The page must remain usable at 200% scaling, with keyboard navigation, screen-reader names, visible focus, and high-contrast support.

### 7.2 Conversation behavior

- `New Chat` immediately creates and selects a record titled `New chat`.
- After the first prompt, the title becomes a bounded, sanitized summary derived locally from that prompt; the user can later rename it.
- Every submitted prompt is added before generation starts.
- Every response is streamed into the same ordered transcript.
- Selecting a history entry loads all persisted prompts and responses for that chat.
- Interrupted responses retain their partial text and explicit completion status.
- A failed response remains visible with a safe fixed-category error and can be retried as a new turn.

### 7.3 Local conversation record

Each conversation stores:

- random conversation ID;
- created, updated, and last-opened timestamps;
- bounded display title;
- ordered message IDs and timestamps;
- role (`User` or `Assistant`);
- text and completion status;
- model identity reference without an absolute path;
- runtime profile identity and actual fallback identity, if any;
- token/context accounting only when reliably available.

Records are grouped by local calendar time into Today, Yesterday, Previous 7 Days, and older dated groups. Grouping is derived at display time so it stays correct across restarts and date changes.

### 7.4 Storage and recovery

Conversation data is stored only in the packaged application's per-user local data area under the current Windows user's access controls. The store is versioned and written atomically. A corrupt record is quarantined or ignored with a recoverable local-data error; it must not prevent other conversations from loading.

The storage layer enforces bounded title, message, conversation, and total-store sizes. It provides delete-one, clear-all, and configurable retention operations. Deletion removes the selected local records and does not affect model artifacts.

Conversation text is sensitive content. It must never enter diagnostics, telemetry, evidence bundles, screenshots, process arguments, raw runtime logs, or error messages. Encryption-at-rest beyond Windows per-user protection is a separately testable security enhancement, not an unverified MVP claim.

## 8. Security, Privacy, and Trust

### 8.1 Package trust

The runtime package is loaded only from its fixed application package subtree. A signed or build-generated manifest records every executable/library's relative path, length, SHA-256, architecture, role, source commit, build flags, and applicable license metadata.

Before launch, the client verifies canonical containment, exact manifest membership, length, hash, and architecture. It never searches `PATH`, the current working directory, the registry, download caches, or user-selected executable locations. Failure is terminal and classified as unavailable or untrusted runtime.

### 8.2 Process creation

The Windows launcher uses `CreateProcessW` with explicit executable path and correctly encoded argument vector. It uses `STARTUPINFOEX` with an inherited-handle allowlist, redirected standard handles, a minimal environment, and creation-time Job assignment or an equivalently race-free suspended-assignment sequence.

No command shell, PowerShell wrapper, batch file, server mode, socket listener, named global endpoint, service, scheduled task, or autorun entry is allowed.

### 8.3 Environment and offline behavior

The child environment strips credentials, proxy variables, model-hub tokens, runtime override variables, and .NET diagnostic controls not required for execution. The pinned CLI is launched with settings that prevent network downloads or remote model resolution. Tests verify that no listening socket is created.

### 8.4 Data handling

Model paths and prompts remain local and transient outside the explicitly approved conversation store. Absolute paths, prompt text, response text, environment values, and raw terminal logs never enter structured diagnostics or evidence. Any future opt-in raw-log facility requires a separate privacy design and is disabled in the MVP.

## 9. Error Model

User-visible failures map to fixed, actionable categories:

- runtime unavailable;
- runtime untrusted;
- unsupported configuration;
- model changed since inspection;
- model load failed;
- context limit reached;
- generation failed;
- generation stopped;
- operation timed out;
- protocol violation;
- output limit exceeded;
- cleanup failed;
- chat history unavailable or corrupt;
- quantization input unsuitable;
- quantized output invalid;
- insufficient storage.

Internal detail is bounded and path-free. A failure never triggers an unapproved backend, device, context, cache, or artifact fallback.

## 10. Verification Strategy

### 10.1 Contract and transport tests

- exact round trips for each command and event;
- protocol version mismatch;
- invalid state transitions;
- malformed/oversized frames and invalid UTF-8;
- duplicate/cross-session identifiers;
- bounded backpressure and cancellation;
- path and sensitive-text redaction.

### 10.2 Process security tests

- manifest hash/length/path/architecture validation;
- rejection of PATH, CWD, symlink/reparse, and unlisted executable substitution;
- inherited-handle allowlist;
- minimal stripped environment;
- parent crash, client disconnect, timeout, and close cleanup;
- Job empty after every terminal path;
- no listener or persistent helper.

### 10.3 Runtime integration tests

A controlled, non-sensitive tiny inference-capable GGUF fixture is required because the existing N-001 vocabulary-only, zero-tensor fixture cannot generate text. If licensing or size prevents committing it, the integration test uses a documented local fixture injection and skips with an explicit reason when absent.

The controlled smoke test covers load, two-turn prompting, streaming, stop, reconstruction when required, and close. Assertions focus on lifecycle and non-empty bounded output rather than brittle prose equality.

### 10.4 Persistence tests

- create a chat and show it immediately in history;
- persist every user and assistant message in order;
- reload the full transcript after application restart;
- compute Today/Yesterday/Previous 7 Days/older groups across boundary dates;
- retain stopped/incomplete status;
- atomic write interruption and corrupt-record isolation;
- delete one, clear all, retention, and storage bounds;
- confirm that absolute model paths are absent.

### 10.5 UI and accessibility tests

- new chat, selection, rename, deletion, and transcript restoration;
- Send/Stop state and centered stop glyph;
- long prompts/responses, streaming scroll behavior, and error rows;
- keyboard-only operation, accessible names, high contrast, and 200% scaling;
- sidebar collapse at constrained widths.

## 11. Three-Day Delivery Sequence

### Day 1: Trusted runtime vertical slice

- establish contracts, transport, manifest verification, and supervisor packaging;
- launch pinned CPU `llama-cli.exe --simple-io` without a shell;
- implement session start, prompt streaming, stop/kill-and-reload, and close;
- prove cleanup and a local controlled smoke test.

**Day-1 exit:** a test harness can complete a local model turn and leave no child process or listener.

### Day 2: WinUI chat and local history

- add feature orchestration and I0 integration handoff;
- implement the approved chat page and state model;
- implement atomic versioned history storage and dated grouping;
- wire new chat, transcript loading, Send/Stop, and failure states.

**Day-2 exit:** a user can chat through WinUI, stop generation, create multiple chats, restart the app, and recover every transcript.

### Day 3: Hardening and executable handoff

- complete security, lifecycle, corruption, accessibility, and package tests;
- validate manifest/provenance/license material;
- run build, unit, integration, UI smoke, and cleanup verification;
- document known limitations and follow-on backend/quantization/TurboQuant gates.

**Day-3 exit:** a clean-machine-style packaged build can run the CPU chat MVP with an imported compatible model and pass the defined acceptance checks.

This sequence is an execution target, not a guarantee independent of build availability, fixture access, I0 integration timing, or defects discovered in the pinned CLI. The cut line protects the vertical slice: optional acceleration and artifact transformation do not enter the MVP critical path.

## 12. Acceptance Criteria

The MVP is accepted when all of the following are demonstrated:

1. A compatible imported local GGUF model can be loaded through the manifest-verified CPU CLI package.
2. The user can submit at least two turns from WinUI and see streamed assistant responses.
3. The user never needs to type or see a llama.cpp terminal command.
4. Stop visibly halts output, retains partial text with the correct status, and leaves or restores a usable session.
5. New chats appear immediately in history and every prompt/response survives an application restart.
6. History is correctly grouped by date and selecting a chat restores its full ordered transcript.
7. Conversation content and absolute model paths are absent from diagnostics and process arguments.
8. Unsupported or untrusted configurations fail explicitly without silent fallback.
9. Closing, cancellation, crash simulation, and protocol failure leave no runtime child or listener.
10. The existing Model Inspection route and its tests remain unchanged and passing.
11. Build and test evidence identifies the exact pinned llama.cpp source/build and all deferred capabilities.

## 13. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| `llama-cli` interactive output changes | Pin an exact commit/build, use `--simple-io`, parse only proven bounded markers, and test against the packaged binary. |
| Graceful stop does not restore a clean session | Terminate only the CLI child, report `StoppedNeedsReload`, and reconstruct from persisted transcript. |
| No redistributable tiny inference fixture | Support explicit local fixture injection and keep the controlled integration test separately gated. |
| Three-day scope expands into acceleration/quantization | Enforce the MVP cut line and capture follow-ons as independently gated milestones. |
| User chat content leaks into diagnostics | Keep content out of errors/logs/evidence, store only in per-user local history, and add redaction tests. |
| Existing protected worker is accidentally broadened | Use separate G1 projects/package subtree and regression-test Model Inspection unchanged. |
| I0-owned project integration delays the slice | Prepare a minimal, explicit integration handoff early on Day 2 and keep G1 projects independently buildable. |
| Conversation store corruption blocks the app | Use atomic versioned records, bounded reads, and per-record quarantine/recovery. |

## 14. Implementation Planning Rule

The implementation plan derived from this specification must use test-driven, small, reviewable tasks with exact paths, commands, expected results, and commit boundaries. It must distinguish G1-owned work from I0 integration handoffs and must stop at planning: this approval does not authorize production implementation, model downloads, workflow dispatch, pushes, pull requests, or merges.
