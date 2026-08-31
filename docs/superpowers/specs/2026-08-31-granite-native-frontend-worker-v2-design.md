# Granite Native Frontend Worker v2 Design

**Status:** approved bootstrap design

## Purpose

Create a repo-local Codex frontend system for the Granite Edge AI WinUI 3 application. It improves screen design and presentation interaction while preserving all backend, domain, worker, data, navigation, cancellation, retry, persistence, and operational semantics.

The bootstrap itself must not modify production application files.

## Active implementation profile

- WinUI 3
- Windows App SDK
- C#
- XAML
- .NET 8
- Native Windows accessibility and UI Automation

Web frameworks and browser-oriented builders are excluded from native implementation.

## Architecture

The system uses one orchestrator, one production writer, and independent read-only reviewers:

1. `granite-native-frontend-master` routes the workflow and enforces the lock.
2. `frontend-contract-guardian` freezes and compares behaviour and scope.
3. `winui-design-director` creates the native screen specification.
4. `winui-xaml-implementer` is the only production-code writer after authorization.
5. `winui-accessibility-auditor` independently checks keyboard, focus, UI Automation, contrast, text scaling, Narrator, and motion-disabled behaviour.
6. `winui-runtime-visual-qa` audits rendered fixtures, themes, scaling, window sizes, and state coverage.
7. `frontend-release-gate` issues the final verdict.

## Hard authorization gate

`.frontend-worker/v2/authorization.json` starts with `implementation_authorized: false`.

The worker may not open the lock itself. A user must provide the exact phrase `AUTHORIZE GRANITE FRONTEND V2 IMPLEMENTATION`, bounded surfaces, a base revision, and a visual source. Authorization remains limited to those surfaces and the recorded base commit.

## Scope model

- P0: immutable operational and backend files.
- P1: behaviour-sensitive UI orchestration and code-behind.
- P2: presentation logic requiring output-contract review.
- P3: direct visual XAML/resources/assets after authorization.
- P4: UI fixtures, evidence, documentation, and worker infrastructure.
- Unknown: blocked.

No path or file suffix proves that a file is presentation-only.

## Provider authority

1. User contract, backend truth, security, and existing behaviour.
2. Accessibility and native Windows correctness.
3. Approved screen specification or Figma source.
4. Existing repository architecture.
5. Microsoft WinUI skills as technical authority.
6. Granite design-system decisions.
7. Stark as a read-only native design adviser.
8. WinUI-adapted Uncodixfy as an anti-generic audit.
9. UI/UX Pro Max for targeted supplementary research.
10. Product Design only for pre-implementation concept exploration.

Superpowers provides planning, TDD, debugging, and verification discipline; it is not visual authority.

## Provider safety

Every provider is pinned to a reviewed revision. No floating `latest`, silent provider update, opaque binary, or third-party write hook is accepted. External providers are not vendored by the bootstrap. Missing optional providers use local fallback policy and are reported honestly.

## WinUI standards

- Search grounded native examples before creating a custom control.
- Prefer existing or built-in controls and lightweight styles.
- Keep global resources minimal and feature resources feature-scoped.
- Use semantic theme resources with Light, Dark, and HighContrast results.
- Prefer Segoe UI Variable for native shell/general UI unless an approved branded exception exists.
- Prefer Segoe Fluent Icons and native icon slots.
- Apply Mica once per window and Acrylic only to suitable transient surfaces.
- Use content-derived adaptive visual states rather than web breakpoints.
- Preserve virtualization and keep UI-thread work nonblocking.
- Use code-behind only for view-owned focus, visual states, lifecycle, title-bar, accessibility announcement, and animation concerns.
- Do not add a package or project dependency for visual convenience.

## Uncodixfy adaptation

Retain warnings against generic card grids, nested cards, giant radii, pill overload, decorative labels, fake metrics/charts, glows, filler copy, excessive padding, ornamental motion, and misleading affordances.

Reject web-specific bans on Segoe UI, blue, headings, Mica, asymmetry, native translation/opacity motion, and CSS-specific dimensions.

## Evidence and release

A passing build is not visual acceptance, and a screenshot is not behavioural proof.

Release requires same-revision evidence for scope, action parity, build/tests, accessibility, runtime state coverage, visual quality, and performance. Visual baselines are never accepted automatically. Independent reviewers do not fix their own findings. Missing evidence produces `NOT READY`.

## Bootstrap deliverables

- Root `AGENTS.md`.
- Repo-local Codex marketplace and plugin.
- Routed worker skills.
- Provider adapters and exact provider lock.
- Closed authorization record.
- Semantic boundary policy.
- Complete master prompt and implementation start command.
- Safe initialization, verification, and authorization scripts.
- Third-party provenance.
- No production XAML/C#, project, manifest, test, fixture, backend, or experiment changes.
