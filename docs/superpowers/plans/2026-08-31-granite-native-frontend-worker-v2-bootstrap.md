# Granite Native Frontend Worker v2 Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Initialise the repo-local WinUI 3 frontend worker, its pinned design providers, safety policies, master prompt and verification commands without modifying production UI or backend code.

**Architecture:** A local Codex marketplace exposes one master plugin containing routed specialist skills. External providers remain pinned and read-only; a repository implementation lock prevents production XAML/C# edits until explicit human authorization. A no-change guardian and verification script prove that bootstrap work remains outside the application.

**Tech Stack:** OpenAI Codex plugins and skills, PowerShell 7, Git, JSON, YAML, WinUI 3 repository conventions.

**Spec:** `docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md`

## Global Constraints

- Production UI changes are forbidden during bootstrap.
- Backend, runtime, worker, contract, project, manifest and packaging files are immutable.
- External provider revisions are pinned.
- External providers have no production write authority.
- Microsoft WinUI guidance is the native technical authority.
- The implementation lock remains closed.
- The exact later authorization phrase is `START GRANITE FRONTEND IMPLEMENTATION V2`.

---

### Task 1: Register the local Codex marketplace and plugin

**Files:**
- Create: `.agents/plugins/marketplace.json`
- Create: `plugins/granite-native-frontend-worker/.codex-plugin/plugin.json`
- Create: `plugins/granite-native-frontend-worker/agents/openai.yaml`

**Interfaces:**
- Produces: `granite-native-frontend-worker@granite-native-frontend`

- [x] Define the local marketplace.
- [x] Define plugin identity, capabilities and skill root.
- [x] Define the Codex-facing interface metadata.

### Task 2: Add the master and specialist skills

**Files:**
- Create: `plugins/granite-native-frontend-worker/skills/**/SKILL.md`

**Interfaces:**
- Produces: master, guardian, design, implementation, accessibility, runtime-QA and release-gate skills.

- [x] Add the initialization hard lock.
- [x] Add one-writer routing.
- [x] Add backend preservation and evidence gates.
- [x] Add native WinUI implementation requirements.

### Task 3: Add custom agents

**Files:**
- Create: `.codex/agents/*.toml`

**Interfaces:**
- Produces: one master, one sole writer and independent read-only reviewers.

- [x] Add the master agent.
- [x] Add the contract guardian.
- [x] Add the design director.
- [x] Add the sole XAML implementer.
- [x] Add accessibility and visual auditors.

### Task 4: Add pinned provider and scope policy

**Files:**
- Create: `.frontend-worker/v2/provider-lock.json`
- Create: `.frontend-worker/v2/tooling-lock.json`
- Create: `AGENTS.md`
- Create: `.frontend-worker/v2/config.yml`
- Create: `.frontend-worker/v2/implementation-lock.yml`
- Create: `.frontend-worker/v2/boundary-policy.yml`
- Create: `plugins/granite-native-frontend-worker/adapters/providers.yml`
- Create: `plugins/granite-native-frontend-worker/rules/*.yml`

**Interfaces:**
- Produces: machine-readable provider authority, immutable scope and authorization contract.

- [x] Pin every reviewed external provider.
- [x] Deny external production write authority.
- [x] Classify P0–P4 scope.
- [x] Record WinUI-specific Uncodixfy overrides.
- [x] Keep implementation authorization false.
- [x] Route future Codex frontend tasks through root AGENTS.md.
- [x] Record initialization and later-stage native tooling prerequisites.

### Task 5: Add deterministic initialization and verification commands

**Files:**
- Create: `scripts/frontend-worker/Initialize-GraniteNativeFrontendWorkerV2.ps1`
- Create: `scripts/frontend-worker/Test-GraniteNativeFrontendWorkerV2.ps1`

**Interfaces:**
- Produces: idempotent provider installation/verification and a no-production-change structural gate.

- [x] Register local and external marketplaces.
- [x] Install required providers at pinned revisions.
- [x] Install the pinned project-level Uncodixfy skill.
- [x] Install UI/UX Pro Max CLI 2.5.0 for Codex.
- [x] Generate local provider status.
- [x] Verify plugin identity, provider pins, write authority and lock state.

### Task 6: Add the full worker prompt and operator documentation

**Files:**
- Create: `docs/frontend-worker/GRANITE-NATIVE-FRONTEND-WORKER-V2-MASTER-PROMPT.md`
- Create: `plugins/granite-native-frontend-worker/README.md`
- Create: `plugins/granite-native-frontend-worker/THIRD_PARTY_NOTICES.md`

**Interfaces:**
- Produces: the complete prompt the user can give to the Codex worker.

- [x] Document initialization-only execution.
- [x] Document later authorization and implementation workflow.
- [x] Document native WinUI standards and evidence requirements.
- [x] Document provider provenance and licences.

### Task 7: Verify the bootstrap branch

**Files:**
- Test: all bootstrap files on the branch.

**Interfaces:**
- Consumes: Tasks 1–6.
- Produces: a draft PR containing no production application changes.

- [ ] Run `pwsh -File scripts/frontend-worker/Test-GraniteNativeFrontendWorkerV2.ps1` in a local checkout.
- [ ] Run the initialization script with `-Install` on the Windows development machine.
- [ ] Start a fresh Codex thread and run the master prompt in initialization mode.
- [ ] Confirm the contract guardian reports no production change.
