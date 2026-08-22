# Inspected Model Production Chat Design

**Status:** Approved on 2026-08-22

## Problem

The onboarding **Preview Chat** action always creates the deterministic preview
controller. The real GGUF runtime is packaged and tested, and
`MainWindow.OpenProductionChatAsync` can launch it, but no user-facing action
constructs and submits a trusted production launch request. Consequently, a
user can import and inspect a model yet still receive only the canned preview
response.

## User flow

When Model Inspection reaches `Ready` or `ReadyWithWarnings`, its primary action
becomes **Open in Chat**. Activating it uses the exact immutable inspection
request and terminal inspection evidence to create a production chat launch.
The onboarding shell forwards that launch to `MainWindow`, which opens the
existing persistent chat page through `OpenProductionChatAsync`.

**Preview Chat** remains available as an explicitly deterministic, no-model
demonstration. It is never relabelled or silently upgraded, and production
launch failures return to onboarding without falling back to preview output.

## Trust and configuration

The launch builder reads the packaged `GgufRuntime/runtime-manifest.json` from
the installed application directory and snapshots its bytes. It uses the model
path and SHA-256 already bound to the immutable inspection request, then checks
that the terminal result belongs to that request and is chat-compatible.

The first production profile is a conservative CPU configuration:

- runtime build and source identity come from the trusted manifest;
- device `cpu`, zero GPU layers, and flash attention disabled;
- F16 key/value cache;
- context size bounded by the inspected model context and the runtime ceiling;
- positive thread and batch defaults bounded for ordinary Windows x64 systems;
- bounded generated-token limit;
- an evidence/profile identity that states this is the inspected CPU baseline.

The existing launch request validation re-hashes the model and validates the
runtime/configuration match immediately before process creation. Changed,
missing, incompatible, or untrusted inputs fail closed.

## Components

- `ModelInspectionPage` exposes a production-chat request only for the current
  completed Ready result.
- `OnboardingShellPage` subscribes to that request and forwards it without
  reconstructing path-bearing state.
- `MainWindow` handles the forwarded request by calling its existing trusted
  production-chat method.
- A small launch-request factory owns manifest loading and conservative CPU
  configuration construction, keeping this logic out of visual controls.

## Error handling

Launch construction and runtime startup are asynchronous and single-flight.
The action is disabled while launching. Any trust, file, configuration, or
runtime failure stays out of chat content, returns the application to
onboarding, and presents a fixed privacy-safe failure message. No absolute path
or model filename is placed in that message.

## Verification

Tests must first reproduce the missing connection, then prove:

1. Ready inspection exposes **Open in Chat**, while incomplete/failure states do
   not.
2. The exact inspected model identity and packaged manifest snapshot reach the
   production launch request.
3. MainWindow selects `CreateProductionAsync`, not the deterministic preview
   constructor.
4. Launch failures never fall back to preview and never disclose a local path.
5. The packaged WinUI path and the controlled Granite 4.1 3B real-model smoke
   both pass after integration.

## Scope boundary

This closes the current CPU local-chat journey. Hardware-fit UI, GPU backends,
TurboQuant activation, and knowledge-file retrieval remain separate gates.
Turbo3/Turbo4 continue to fail with `turboquant-runtime-required` on the
upstream runtime; TurboVec remains a vector-retrieval technology and is not part
of model inference launch.
