# Granite Native Frontend Worker v2 state

## Bootstrap status

- Repo-local Codex marketplace and worker plugin: defined.
- Provider revisions and authority: pinned.
- Semantic boundary and authorization policy: defined.
- Canonical master prompt: `docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md`.
- Default implementation state: **closed**.
- Production XAML/C# changes: **not authorized**.

This directory contains policy and local run state, not application source.

## Single authorization source

`.frontend-worker/v2/implementation-lock.yml` declares the ignored local state path:

```text
.frontend-worker/v2/.state/authorization.json
```

If that file is absent, malformed, stale, or closed, implementation is closed. The tracked `authorization.template.json` is only a schema/default template and is never an open authorization record.

## Required before the first UI campaign

1. Run `scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1 -Install` on the Windows development machine.
2. Review required and optional provider readiness.
3. Start a fresh Codex session after plugin installation so discovery is current.
4. Run `scripts/Test-GraniteNativeFrontendWorkerV2.ps1`.
5. Ask Codex to initialise/verify the worker. It must leave authorization closed.
6. When one bounded surface is ready, use `IMPLEMENTATION_START_COMMAND.md`.

## Provider expectations

Required: Microsoft WinUI Skills, Superpowers, and the bundled WinUI-adapted Uncodixfy policy.

Optional advisers: Stark, UI/UX Pro Max, Figma, and Product Design. Their absence must never cause Codex to substitute web frameworks or weaken native WinUI, accessibility, evidence, or backend-preservation requirements.

## Run artifacts

Authorized campaigns store specifications under `specs/<surface>/`, baselines under `baselines/<base-sha>/`, and same-revision evidence under `evidence/<run-id>/`. The local authorization state, screenshots, machine-specific logs, and provider-status file are ignored by default.
