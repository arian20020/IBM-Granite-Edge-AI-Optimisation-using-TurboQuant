# GGUF chat production closure

The application integration seam is `MainWindow.OpenProductionChatAsync` with
one immutable `GgufChatLaunchRequest`. The caller must supply all of the inputs
approved by the GGUF chat design:

- an existing fixed package root;
- the trusted manifest bytes embedded or otherwise supplied by the approved
  packaging boundary;
- the inspected local GGUF path and display name; and
- a complete C1-approved `GgufRuntimeConfiguration`.

The request snapshots the manifest, requires local absolute inputs, and rejects
a runtime build/source identity that differs from the trusted manifest. It also
enforces the CPU-only MVP profile and re-hashes the selected model against the
inspected SHA-256 before process launch. Opening production chat performs the
detached-manifest and package verification in
`GgufRuntimeClient.CreateFromPackage`. Any rejection returns to onboarding and
does not substitute preview mode or expose the absolute model path.

Conversation lifecycle is now production-safe: initialization filters history
by model/profile, each new or selected chat recreates the CLI with only that
conversation's durable turns, and `StoppedNeedsReload` recreates the session
from the persisted partial transcript.

Preview remains independently executable through `scripts/Run-ChatPreview.ps1`.
The controlled real-model smoke remains gated by externally approved llama.cpp
and model inputs; an absent controlled configuration is an explicit skip, not a
production success claim.
