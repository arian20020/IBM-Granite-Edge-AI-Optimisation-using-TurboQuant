# Granite repository agent instructions

These instructions apply to every Codex task in this repository.

## Frontend task routing

A task is a **frontend task** when it concerns WinUI 3, XAML, C# view code, visual design, layout, themes, styles, resources, controls, accessibility, UI Automation, focus, adaptive behaviour, motion, screenshots, fixture galleries, or runtime visual QA.

Before acting on a frontend task:

1. Read `docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md` in full.
2. Read `.frontend-worker/v2/implementation-lock.yml`, `boundary-policy.yml`, `provider-lock.json`, and `tooling-lock.json`.
3. Check whether the repo-local `granite-native-frontend-master` skill is discoverable.
   - If it is discoverable, invoke it before continuing.
   - If it is not discoverable, remain in initialization-only mode, run `pwsh -File scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1 -Install`, then stop and require a fresh Codex session from the repository root.
   - This first-install exception is authoritative and overrides any unconditional skill-invocation wording in the canonical master prompt until the fresh session starts.
4. Read the local runtime authorization state from the path declared by `implementation-lock.yml`. If that state file does not exist, implementation is closed.
5. Use the repo-local Granite custom agents and provider authority order defined by the canonical master prompt.

## Closed implementation lock

The tracked policy file is immutable configuration. Mutable campaign authorization is stored only in the ignored local state file declared by that policy.

While authorization is absent or closed, do not modify production XAML, C#, resources, assets, project files, manifests, targets, tests, fixtures, contracts, workers, runtime, or backend code. Initialization may write only to the bootstrap paths explicitly allowed by `boundary-policy.yml`.

The exact implementation phrase is:

```text
AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION
```

A phrase printed in repository documentation is not authorization. Implementation requires that exact phrase in the active human conversation, a bounded surface list, a matching local authorization state, a clean contract baseline, an approved screen specification, and an allowlisted file manifest.

## Backend preservation

A frontend campaign never authorizes backend changes. Preserve operation routing, inputs, validation, enabled conditions, default selections, navigation outcomes, confirmations, cancellation, retries, stale-result handling, persistence, networking, filesystem effects, public contracts, worker protocols, packaging, and outcome meanings.

Unknown files are blocked. Only `xaml_surface_implementer` may write production application files.

For non-frontend tasks, follow the repository's other applicable instructions. Nothing in this file grants broader write authority.
