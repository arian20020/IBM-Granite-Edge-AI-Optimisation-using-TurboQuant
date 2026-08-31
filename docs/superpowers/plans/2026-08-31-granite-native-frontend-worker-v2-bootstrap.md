# Granite Native Frontend Worker v2 Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to execute future implementation campaigns task-by-task.

**Goal:** Initialise a contract-protected WinUI 3 frontend worker without modifying production UI or backend code.

**Architecture:** A repo-local Codex plugin routes one master, one sole writer, and independent reviewers. Required providers are pinned, optional advisers degrade safely, authorization is local and ignored, and a repository-owned semantic guard plus Windows CI verifies isolation.

**Tech Stack:** Codex plugins/skills, PowerShell 7, Git, JSON/YAML/TOML, .NET 8, Roslyn, WinUI 3 repository conventions.

**Spec:** `docs/superpowers/specs/2026-08-31-granite-native-frontend-worker-v2-design.md`

## Global constraints

- No production application, backend, test, fixture, project, manifest, target, or worker change during bootstrap.
- The exact future phrase is `AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION`.
- Authorization state is ignored and local, never committed.
- Microsoft WinUI guidance is the native technical authority.
- External providers have no production write authority.
- Optional providers cannot block native implementation when their documented fallback exists.

## Completed bootstrap tasks

- [x] Register the repo-local marketplace and plugin.
- [x] Add routed master, guardian, design, implementation, accessibility, visual-QA, and release-gate skills.
- [x] Add repo-local custom agents with one production writer.
- [x] Add one canonical master prompt and root routing instructions.
- [x] Add one local authorization-state policy and exact start command.
- [x] Pin Microsoft WinUI, Superpowers, Uncodixfy source provenance, Stark, UI/UX Pro Max, Figma, and Product Design.
- [x] Mark Stark, UI/UX Pro Max, Figma, and Product Design optional with explicit fallbacks.
- [x] Add semantic P0–P4 boundary policy with explicit precedence.
- [x] Add deterministic authorization, initialization, and verification scripts.
- [x] Add a Roslyn/XAML semantic guard that compares protected hashes, declarations, invocations, XAML contracts, and dependencies.
- [x] Add guard regression tests.
- [x] Add dedicated Windows bootstrap CI.

## Machine initialization still required

- [ ] Run `pwsh -File scripts/Initialize-GraniteNativeFrontendWorkerV2.ps1 -Install` on the Windows development machine.
- [ ] Start a fresh Codex session after provider installation.
- [ ] Run `pwsh -File scripts/Test-GraniteNativeFrontendWorkerV2.ps1`.
- [ ] Confirm the initialization guardian reports no production change.

No UI implementation begins as part of this plan.
