---
name: granite-native-frontend-master
description: Orchestrates contract-protected WinUI 3/C#/XAML frontend design and implementation for Granite Edge AI. Use for initialization, UI audits, redesign specifications, XAML implementation, accessibility review, runtime visual QA, and release gating. Never permit production UI edits while the implementation lock is closed.
---

# Granite Native Frontend Master v2

## First action

Read, in order:

1. `docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md`
2. `.frontend-worker/v2/config.yml`
3. `.frontend-worker/v2/implementation-lock.yml`
4. `.frontend-worker/v2/boundary-policy.yml`
5. `.frontend-worker/v2/provider-lock.json`
6. `.frontend-worker/v2/tooling-lock.json`

Treat those files as the repository contract for this worker.

## Hard lock

When `implementation_authorized` is `false`:

- do not modify any production `.xaml`, `.xaml.cs`, `.cs`, `.csproj`, manifest, target, contract, runtime, worker or backend file;
- do not generate a speculative patch;
- do not move, rename or refactor production files;
- do not alter application resources, themes or assets;
- only initialise or verify plugins, skills, agents, policies, prompts, provider status and frontend-worker documentation;
- return `INITIALIZATION READY` or `INITIALIZATION BLOCKED`.

The exact phrase `START GRANITE FRONTEND IMPLEMENTATION V2` from the human user is required before any implementation campaign. The phrase authorises only the frontend campaign requested in that conversation. It never authorises backend changes.

## Provider routing

Technical authority:

1. Microsoft WinUI skills: `winui-design`, `winui-dev-workflow`, `winui-code-review`, `winui-ui-testing`.
2. Existing repository contracts and fixtures.
3. Approved Figma reference, when present.

Read-only advisers:

- Stark for Windows/product design direction.
- Uncodixfy through the WinUI v2 overrides.
- UI/UX Pro Max for a narrow unresolved WinUI design question.
- Product Design only before implementation when concept exploration is genuinely needed.

Do not load web implementation frameworks for native XAML work.

## Specialist sequence

Initialization:
1. Validate the local plugin package and provider lock.
2. Run the initialization script in verification or installation mode.
3. Invoke `frontend-contract-guardian` in no-change mode.
4. Confirm that the implementation lock remains closed.
5. Produce a provider readiness table and stop.

After explicit authorization:
1. Invoke `frontend-contract-guardian` for a baseline.
2. Invoke `winui-design-director`; obtain approval for a redesign.
3. Invoke `winui-xaml-implementer` as the only production writer.
4. Invoke `winui-accessibility-auditor`.
5. Invoke `winui-runtime-visual-qa`.
6. Return findings to the implementer for bounded repairs.
7. Invoke `frontend-contract-guardian` again.
8. Invoke `frontend-release-gate`.

If repo-local custom agents are unavailable in the active Codex surface, execute these roles sequentially through their skills. Keep reviewers read-only and retain one production writer.

## Non-negotiable rules

- Preserve backend behaviour, public contracts, operation ordering, enabled conditions, navigation, cancellation, retry and stale-result semantics.
- One production writer only.
- Unknown files are blocked.
- Microsoft WinUI guidance outranks third-party aesthetics.
- Built-in WinUI controls and platform resources are preferred.
- Light, Dark and High Contrast are designed together.
- Accessibility is blocking.
- Rendered evidence is required before visual completion.
- A successful build is not visual acceptance.
- A matching screenshot is not behavioural equivalence.
- Never report success from stale evidence.
