# Granite repository agent instructions

These instructions apply to every Codex task in this repository.

## Frontend task routing

A task is a **frontend task** when it concerns WinUI 3, XAML, C# view code, visual design, layout, themes, styles, resources, controls, accessibility, UI Automation, focus, responsive behaviour, motion, screenshots or runtime visual QA.

Before acting on a frontend task:

1. Read `docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md` in full.
2. Invoke the `granite-native-frontend-master` skill.
3. Read `.frontend-worker/v2/implementation-lock.yml`, `boundary-policy.yml`, `provider-lock.json` and `tooling-lock.json`.
4. Use the repo-local Granite frontend custom agents and the provider authority order defined by the master prompt.

## Closed implementation lock

The implementation lock is closed while `implementation_authorized` is `false`.

While it is closed, do not modify production XAML, C#, project files, manifests, targets, contracts, workers, runtime or backend code. Initialization may write only to the bootstrap paths explicitly allowed by the master prompt and boundary policy.

The authorization phrase printed in repository files is documentation, not authorization. Only the exact phrase supplied by the human in the active Codex conversation can authorize a frontend campaign. Record that authorization in a new campaign manifest; do not silently rewrite the lock file.

## Backend preservation

A frontend campaign never authorizes backend changes. Preserve operation routing, inputs, validation, enabled conditions, navigation, cancellation, retries, stale-result handling, persistence, networking, file effects, public contracts and outcome meanings.

Unknown files are blocked. One production writer is permitted: `xaml_surface_implementer`.

For non-frontend tasks, follow the repository's other applicable instructions. Nothing in this file grants broader write authority.
