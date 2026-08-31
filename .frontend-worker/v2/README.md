# Granite Native Frontend Worker v2 state

## Bootstrap status

- Repo-local Codex marketplace: initialised.
- Granite worker plugin and routed skills: initialised.
- Provider revisions and authority: pinned.
- Semantic boundary policy: initialised.
- Master prompt: initialised.
- Implementation lock: **closed**.
- Production XAML/C# changes: **not authorised**.

This directory contains policy and run state, not application source.

## Required before the first UI campaign

1. Run `scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1` on the Windows development machine.
2. Review the readiness report and install/enable any required native provider deliberately.
3. Run `scripts/Test-GraniteNativeFrontendWorkerV2.ps1`.
4. Start a fresh Codex session from the repository root so `AGENTS.md` and repo-local skills are discovered.
5. Ask Codex to initialise/verify the worker. It must leave the lock closed.
6. When a specific surface is ready, use the exact prompt in `IMPLEMENTATION_START_COMMAND.md`.

## Provider expectations

Microsoft WinUI and Superpowers are required. Stark, Figma, Product Design, and UI/UX Pro Max are optional advisers with local fallback policy. The original Uncodixfy skill is not required at runtime because the reviewed WinUI adaptation is stored in the plugin.

A provider marked optional may improve evidence or design reasoning, but its absence must never cause Codex to substitute a web framework or weaken native WinUI requirements.

## Run artifacts

Authorized campaigns store specifications under `specs/<surface>/`, baselines under `baselines/<base-sha>/`, and same-revision evidence under `evidence/<run-id>/`. Do not commit generated screenshots or machine-specific logs without an explicit evidence-retention decision.
