# Granite Native Frontend Worker v2 Design

**Status:** final bootstrap design

## Purpose

Create a repo-local Codex system for native WinUI 3/C#/XAML frontend work. It improves screen design and presentation interaction while preserving all backend, domain, worker, data, navigation, enabled-state, confirmation, cancellation, retry, persistence, networking, filesystem, and operational semantics.

The bootstrap itself must not modify production application files.

## Architecture

The system uses one orchestrator, one production writer, and independent read-only reviewers:

1. `granite-native-frontend-master` routes work and enforces authorization.
2. `frontend-contract-guardian` freezes and compares scope and behaviour.
3. `winui-design-director` creates the native screen specification.
4. `winui-xaml-implementer` is the only production writer.
5. `winui-accessibility-auditor` checks keyboard, focus, UI Automation, contrast, text scaling, Narrator, and disabled motion.
6. `winui-runtime-visual-qa` reviews fixtures, themes, scaling, windows, and visual quality.
7. `frontend-release-gate` issues the independent verdict.

## Authorization

The tracked `implementation-lock.yml` is immutable policy. Mutable authorization exists only in the ignored local state path declared by that policy. If the local state is absent or invalid, implementation is closed.

Opening a campaign requires the exact active-human phrase `AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION`, a campaign ID, bounded surfaces, an approved base revision equal to pre-edit `HEAD`, and an approved visual source. No human identity or complete prompt is stored in Git.

## Scope

- P0: immutable operational/backend/project/test/evidence files.
- P1: behaviour-sensitive UI orchestration.
- P2: presentation logic requiring output-contract review.
- P3: direct visual XAML/resources/assets after authorization.
- P4 existing fixtures: read-only by default.
- P4 worker infrastructure: initialization write zone.
- Unknown: blocked.

Classification precedence is explicit; no file name or folder proves that a file is presentation-only.

## Provider authority

Required:

- Microsoft WinUI Skills as native technical authority.
- Superpowers as development methodology.
- The bundled WinUI-adapted Uncodixfy policy as anti-generic audit.

Optional advisers:

- Stark for product-specific native direction.
- UI/UX Pro Max for narrow WinUI research.
- Figma for an approved visual source.
- Product Design for pre-implementation concept exploration.

All external revisions are pinned and all external providers have `writeAuthority: false`.

## Safety and evidence

- One production writer.
- A single canonical master prompt.
- A single mutable authorization source.
- A semantic guard that hashes all protected file types and compares declarations, behaviour-sensitive invocations, XAML action/binding contracts, packages, projects, and imports.
- Existing fixture outcomes remain frozen.
- Dedicated Windows CI validates metadata, scripts, the guard, and bootstrap isolation.
- Build success, visual similarity, accessibility, behaviour, and performance are separate gates.
