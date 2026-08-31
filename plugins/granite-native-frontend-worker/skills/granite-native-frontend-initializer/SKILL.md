---
name: granite-native-frontend-initializer
description: Use when preparing or verifying the Granite WinUI frontend worker before any production UI campaign.
---

# Granite Native Frontend Initializer

Read the canonical master prompt and all `.frontend-worker/v2` policy files.

## Invariants

- Treat a missing runtime authorization state as closed.
- Do not modify production application, test, fixture, project, manifest, worker, runtime, or backend files.
- Use only the canonical initialization and verification scripts.
- Do not install optional providers unless requested or enabled by the initialization command.
- Never elevate, enable Developer Mode, or modify application dependencies.

## Procedure

1. Run `pwsh -File scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1 -Install`.
2. If plugin discovery changed, return `INITIALIZATION RESTART REQUIRED` and stop.
3. In a fresh Codex session, run `pwsh -File scripts/Test-GraniteNativeFrontendWorkerV2.ps1`.
4. Invoke `frontend-contract-guardian` in initialization mode.
5. Return `INITIALIZATION READY` or exact blockers. Do not begin design or implementation.
